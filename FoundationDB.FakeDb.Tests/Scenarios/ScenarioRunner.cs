#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client;
	using SnowBank.Linq;

	/// <summary>Executes a scenario against any <see cref="IFdbDatabase"/> backend and records the trace: the runner is the recorder.</summary>
	/// <remarks>
	/// <para>Scenarios are cooperative single-threaded scripts: at most one in-flight operation at a time, watches and versionstamps settled only at explicit observation steps, determinism is a property of the model, not of the capture.</para>
	/// <para>The database's root location must resolve to a non-empty prefix (open the database on a test partition path), so that "key outside the scenario subspace" renders identically on every backend.</para>
	/// </remarks>
	public sealed class ScenarioRunner
	{

		/// <summary>Bounded wait applied by <see cref="ScenarioOp.ExpectFired"/> and <see cref="ScenarioOp.ExpectVersionstamp"/> (a real cluster settles watch futures asynchronously).</summary>
		private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

		/// <summary>Grace delay sampled by <see cref="ScenarioOp.ExpectPending"/> before asserting that a watch has not fired.</summary>
		private static readonly TimeSpan PendingGrace = TimeSpan.FromMilliseconds(250);

		/// <summary>Rendered form of a resolved key that falls outside the scenario subspace (normalized: the actual bytes differ per backend/run).</summary>
		private const string OutsideMarker = "!outside";

		private sealed class ActorState
		{

			public IFdbTransaction? Transaction { get; set; }

			public IKeySubspace? Subspace { get; set; }

		}

		private ScenarioRunner(Scenario scenario, IFdbDatabase db, CancellationToken ct)
		{
			this.Scenario = scenario;
			this.Db = db;
			this.Cancellation = ct;
		}

		private Scenario Scenario { get; }

		private IFdbDatabase Db { get; }

		private CancellationToken Cancellation { get; }

		private VersionSymbolizer Symbols { get; } = new();

		private Dictionary<string, ActorState> Actors { get; } = new(StringComparer.Ordinal);

		private Dictionary<int, FdbWatch> Watches { get; } = new();

		private Dictionary<int, Task<VersionStamp>> Stamps { get; } = new();

		/// <summary>Prefix of the scenario subspace, captured on the first resolution (identical for every actor: they all resolve the database root).</summary>
		private Slice Prefix { get; set; } = Slice.Nil;

		/// <summary>Runs the scenario and returns the recorded trace (one event per step, plus the final subspace dump).</summary>
		public static async Task<ScenarioTrace> RunAsync(Scenario scenario, IFdbDatabase db, CancellationToken ct)
		{
			var runner = new ScenarioRunner(scenario, db, ct);
			try
			{
				return await runner.ExecuteAsync();
			}
			finally
			{
				runner.Cleanup();
			}
		}

		private async Task<ScenarioTrace> ExecuteAsync()
		{
			var events = new List<TraceEvent>(this.Scenario.Steps.Count);
			for (int i = 0; i < this.Scenario.Steps.Count; i++)
			{
				var step = this.Scenario.Steps[i];
				var args = step.ToJson();
				args.Remove("op");
				args.Remove("actor");

				var outcome = new JsonObject();
				try
				{
					await ExecuteStep(step, outcome);
				}
				catch (FdbException e)
				{
					outcome["error"] = e.Code.ToString();
					if (step.Op == ScenarioOp.Commit)
					{ // a failed commit ends the actor's transaction (retrying is not part of the scenario model)
						DisposeActorTransaction(step.Actor!);
					}
				}
				catch (OperationCanceledException) when (!this.Cancellation.IsCancellationRequested)
				{
					// an observed future (watch, versionstamp) settled by cancellation, not the runner's own token: a comparable outcome
					outcome["cancelled"] = true;
				}
				catch (Exception e) when (e is not OperationCanceledException)
				{
					// a backend threw something that is not an fdb error (e.g. an unimplemented emulator path): record it as a comparable outcome
					outcome["exception"] = e.GetType().Name;
				}

				events.Add(new()
				{
					Step = i,
					Op = step.Op.ToString(),
					Actor = step.Actor,
					Args = args,
					Outcome = outcome,
				});
			}

			var finalState = await DumpFinalState();

			return new()
			{
				ScenarioName = this.Scenario.Name,
				Events = events,
				FinalState = finalState,
			};
		}

		private async Task ExecuteStep(ScenarioStep step, JsonObject outcome)
		{
			switch (step.Op)
			{
				case ScenarioOp.Begin:
				{
					var actor = GetActor(step);
					actor.Transaction?.Dispose(); // an actor owns at most one open transaction: Begin replaces any previous one
					actor.Transaction = null;
					actor.Subspace = null;
					// note: the subspace is resolved lazily at the first key-using step, NOT here, the resolution is a
					// directory READ, and a step like SetOption(ReadYourWritesDisable) must be able to precede any read
					actor.Transaction = this.Db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
					break;
				}
				case ScenarioOp.Commit:
				{
					await GetTransaction(step).CommitAsync();
					break;
				}
				case ScenarioOp.Reset:
				{
					GetTransaction(step).Reset();
					GetActor(step).Subspace = null; // a reset transaction re-resolves at its next key-using step, like a fresh one
					break;
				}
				case ScenarioOp.Dispose:
				{
					DisposeActorTransaction(step.Actor ?? throw AuthoringError(step, "Dispose requires an actor"));
					break;
				}
				case ScenarioOp.Set:
				{
					await EnsureSubspaceResolved(step);
					GetTransaction(step).Set(AbsoluteKey(step.Key).Span, step.Value.Span);
					break;
				}
				case ScenarioOp.Clear:
				{
					await EnsureSubspaceResolved(step);
					GetTransaction(step).Clear(AbsoluteKey(step.Key).Span);
					break;
				}
				case ScenarioOp.ClearRange:
				{
					await EnsureSubspaceResolved(step);
					GetTransaction(step).ClearRange(AbsoluteKey(step.Key).Span, AbsoluteKey(step.EndKey).Span);
					break;
				}
				case ScenarioOp.Atomic:
				{
					await EnsureSubspaceResolved(step);
					GetTransaction(step).Atomic(AbsoluteKey(step.Key).Span, step.Value.Span, step.Mutation ?? throw AuthoringError(step, "Atomic requires a mutation type"));
					break;
				}
				case ScenarioOp.SetVersionstampedKey:
				{
					int offset = step.StampOffset ?? throw AuthoringError(step, "SetVersionstampedKey requires a stamp offset");
					await EnsureSubspaceResolved(step);
					// the placeholder offset is relative to the scenario key: shift it by the resolved prefix length
					GetTransaction(step).SetVersionStampedKey(AbsoluteKey(step.Key), this.Prefix.Count + offset, step.Value);
					break;
				}
				case ScenarioOp.SetVersionstampedValue:
				{
					int offset = step.StampOffset ?? throw AuthoringError(step, "SetVersionstampedValue requires a stamp offset");
					await EnsureSubspaceResolved(step);
					GetTransaction(step).SetVersionStampedValue(AbsoluteKey(step.Key), step.Value, offset);
					break;
				}
				case ScenarioOp.Get:
				{
					await EnsureSubspaceResolved(step);
					var value = await GetReader(step).GetAsync(AbsoluteKey(step.Key).Span);
					outcome["value"] = this.Symbols.Render(value);
					break;
				}
				case ScenarioOp.GetKey:
				{
					var selector = step.Selector ?? throw AuthoringError(step, "GetKey requires a selector");
					await EnsureSubspaceResolved(step);
					var resolved = await GetReader(step).GetKeyAsync(AbsoluteSelector(selector));
					outcome["key"] = RenderKey(resolved);
					break;
				}
				case ScenarioOp.GetRange:
				{
					var begin = step.Selector ?? throw AuthoringError(step, "GetRange requires a begin selector");
					var end = step.EndSelector ?? throw AuthoringError(step, "GetRange requires an end selector");
					await EnsureSubspaceResolved(step);
					var options = new FdbRangeOptions
					{
						Limit = step.Limit,
						IsReversed = step.Reverse,
					};
					// page through the range like a real consumer: the trace captures the database semantics
					// (which keys/values the read yields), not the client's chunking, which is implementation-specific
					// (the native client conservatively under-fills chunks when local writes merge into the read)
					var items = await GetReader(step).GetRange(AbsoluteSelector(begin), AbsoluteSelector(end), options).ToListAsync();
					outcome["items"] = JsonArray.FromValues(items, kv => JsonObject.Create([ ("key", RenderKey(kv.Key)), ("value", this.Symbols.Render(kv.Value)) ]));
					break;
				}
				case ScenarioOp.GetReadVersion:
				{
					outcome["version"] = this.Symbols.Version(await GetTransaction(step).GetReadVersionAsync());
					break;
				}
				case ScenarioOp.GetCommittedVersion:
				{
					outcome["version"] = this.Symbols.Version(GetTransaction(step).GetCommittedVersion());
					break;
				}
				case ScenarioOp.GetVersionstamp:
				{
					int handle = step.HandleId ?? throw AuthoringError(step, "GetVersionstamp requires a handle id");
					this.Stamps[handle] = GetTransaction(step).GetVersionStampAsync();
					break;
				}
				case ScenarioOp.Watch:
				{
					int handle = step.HandleId ?? throw AuthoringError(step, "Watch requires a handle id");
					await EnsureSubspaceResolved(step);
					// the watch outlives the transaction: it must be bound to the runner's token, never the transaction's own
					this.Watches[handle] = GetTransaction(step).Watch(AbsoluteKey(step.Key).Span, this.Cancellation);
					break;
				}
				case ScenarioOp.ExpectFired:
				{
					var watch = GetWatch(step);
					if (await Task.WhenAny(watch.Task, Task.Delay(SettleTimeout, this.Cancellation)) != watch.Task)
					{
						outcome["watch"] = "Timeout";
						break;
					}
					await watch.Task; // may surface an FdbException, recorded as the outcome
					outcome["watch"] = "Fired";
					break;
				}
				case ScenarioOp.ExpectPending:
				{
					var watch = GetWatch(step);
					await Task.Delay(PendingGrace, this.Cancellation);
					if (!watch.Task.IsCompleted)
					{
						outcome["watch"] = "Pending";
						break;
					}
					await watch.Task; // may surface an FdbException, recorded as the outcome
					outcome["watch"] = "Fired";
					break;
				}
				case ScenarioOp.ExpectVersionstamp:
				{
					int handle = step.HandleId ?? throw AuthoringError(step, "ExpectVersionstamp requires a handle id");
					if (!this.Stamps.TryGetValue(handle, out var task)) throw AuthoringError(step, $"unknown versionstamp handle #{handle}");
					if (await Task.WhenAny(task, Task.Delay(SettleTimeout, this.Cancellation)) != task)
					{
						outcome["stamp"] = "Timeout";
						break;
					}
					outcome["stamp"] = this.Symbols.Stamp(await task);
					break;
				}
				case ScenarioOp.SetOption:
				{
					var tr = GetTransaction(step);
					switch (step.Option)
					{
						case ScenarioTransactionOption.ReadYourWritesDisable:
						{
							tr.Options.WithReadYourWritesDisable();
							break;
						}
						case ScenarioTransactionOption.SnapshotReadYourWritesDisable:
						{
							tr.Options.SetOption(FdbTransactionOption.SnapshotReadYourWritesDisable);
							break;
						}
						default:
						{
							throw AuthoringError(step, "SetOption requires a supported option");
						}
					}
					break;
				}
				case ScenarioOp.TouchMetadataVersion:
				{
					GetTransaction(step).TouchMetadataVersionKey();
					break;
				}
				case ScenarioOp.GetMetadataVersion:
				{
					var stamp = await GetTransaction(step).GetMetadataVersionKeyAsync();
					outcome["stamp"] = stamp is null ? null : this.Symbols.Stamp(stamp.Value);
					break;
				}
				case ScenarioOp.WatchMetadataVersion:
				{
					int handle = step.HandleId ?? throw AuthoringError(step, "WatchMetadataVersion requires a handle id");
					this.Watches[handle] = GetTransaction(step).Watch(Fdb.System.MetadataVersionKey.Span, this.Cancellation);
					break;
				}
				default:
				{
					throw AuthoringError(step, $"unsupported operation {step.Op}");
				}
			}
		}

		/// <summary>Resolves the actor's subspace inside its own transaction, lazily at the first key-using step (the resolution is a directory READ, which must not precede option steps).</summary>
		private async ValueTask EnsureSubspaceResolved(ScenarioStep step)
		{
			var actor = GetActor(step);
			if (actor.Subspace is null)
			{
				var tr = actor.Transaction ?? throw AuthoringError(step, $"actor '{step.Actor}' has no open transaction");
				// pin the read version HERE, explicitly: on a long-lived real database handle the directory resolution
				// below can be served from a warm cache without issuing any read, which would let the read version
				// float to the actor's first actual read and desynchronize the backends' conflict windows
				_ = await tr.GetReadVersionAsync();
				actor.Subspace = await this.Db.Root.Resolve(tr);
				if (this.Prefix.IsNull) this.Prefix = actor.Subspace.GetPrefix();
			}
		}

		private ActorState GetActor(ScenarioStep step)
		{
			var name = step.Actor ?? throw AuthoringError(step, $"{step.Op} requires an actor");
			if (!this.Actors.TryGetValue(name, out var actor))
			{
				actor = new();
				this.Actors[name] = actor;
			}
			return actor;
		}

		private IFdbTransaction GetTransaction(ScenarioStep step)
		{
			return GetActor(step).Transaction ?? throw AuthoringError(step, $"actor '{step.Actor}' has no open transaction");
		}

		/// <summary>Returns the transaction (or its snapshot view when the step asks for snapshot isolation).</summary>
		private IFdbReadOnlyTransaction GetReader(ScenarioStep step)
		{
			var tr = GetTransaction(step);
			return step.Snapshot ? tr.Snapshot : tr;
		}

		private FdbWatch GetWatch(ScenarioStep step)
		{
			int handle = step.HandleId ?? throw AuthoringError(step, $"{step.Op} requires a handle id");
			return this.Watches.TryGetValue(handle, out var watch) ? watch : throw AuthoringError(step, $"unknown watch handle #{handle}");
		}

		private void DisposeActorTransaction(string actorName)
		{
			if (this.Actors.TryGetValue(actorName, out var actor))
			{
				actor.Transaction?.Dispose();
				actor.Transaction = null;
			}
		}

		/// <summary>A scenario authoring error: aborts the run instead of being recorded, since it does not describe backend behavior.</summary>
		private static InvalidOperationException AuthoringError(ScenarioStep step, string message) => new($"Invalid scenario step ({step.Op}): {message}");

		private Slice AbsoluteKey(Slice relativeKey)
		{
			if (relativeKey.IsNull) throw new InvalidOperationException("Invalid scenario step: missing key operand.");
			if (this.Prefix.IsNull) throw new InvalidOperationException("Invalid scenario: no transaction was ever opened, the scenario subspace is unresolved.");
			return this.Prefix + relativeKey;
		}

		private KeySelector AbsoluteSelector(ScenarioSelector selector) => new(AbsoluteKey(selector.Key), selector.OrEqual, selector.Offset);

		/// <summary>Renders an absolute key relative to the scenario subspace; keys outside it are normalized (their bytes differ per backend/run).</summary>
		private string RenderKey(Slice absoluteKey)
		{
			if (!absoluteKey.StartsWith(this.Prefix)) return OutsideMarker;
			return this.Symbols.Render(absoluteKey.Substring(this.Prefix.Count))!;
		}

		/// <summary>Dumps the final content of the scenario subspace (fresh read-only transaction, byte order).</summary>
		private Task<List<KeyValuePair<string, string>>> DumpFinalState()
		{
			return this.Db.ReadAsync(async tr =>
			{
				var subspace = await this.Db.Root.Resolve(tr);
				var items = await tr.GetRange(subspace.ToRange()).ToListAsync();
				return items.Select(kv => new KeyValuePair<string, string>(RenderKey(kv.Key), this.Symbols.Render(kv.Value)!)).ToList();
			}, this.Cancellation);
		}

		private void Cleanup()
		{
			foreach (var watch in this.Watches.Values)
			{
				try { watch.Dispose(); } catch { /* a watch bound to a dead transaction can throw on dispose; nothing to salvage during cleanup */ }
			}
			this.Watches.Clear();
			foreach (var actor in this.Actors.Values)
			{
				actor.Transaction?.Dispose();
				actor.Transaction = null;
			}
		}

	}

}
