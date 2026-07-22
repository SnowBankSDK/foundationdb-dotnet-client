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
	using System.Text;
	using FoundationDB.Client;

	/// <summary>Derives the coverage cells a scenario hits, from its script and (when recorded) its golden trace.</summary>
	/// <remarks>
	/// <para>Scenarios run against a cleaned partition, so a linear walk over the steps can track the committed store and every actor's uncommitted overlay exactly; outcome-dependent dimensions (commit errors, watch fires) come from the golden trace.</para>
	/// <para>Deliberately conservative: when a dimension cannot be resolved (an unknowable post-atomic value, a missing golden), no cell is emitted for it - the ledger under-counts, it never over-counts.</para>
	/// </remarks>
	public static class CoverageClassifier
	{

		/// <summary>Classifies one scenario into the inventory cells it hits.</summary>
		public static HashSet<string> Classify(Scenario scenario, ScenarioTrace? golden) => new Walk(scenario, golden).Run();

		private sealed class Walk
		{

			public Walk(Scenario scenario, ScenarioTrace? golden)
			{
				this.Scenario = scenario;
				this.Golden = golden;
			}

			private Scenario Scenario { get; }

			private ScenarioTrace? Golden { get; }

			private HashSet<string> Hits { get; } = new(StringComparer.Ordinal);

			/// <summary>Committed store: key to value bytes (<see cref="Slice.Nil"/> = present, bytes unknowable after an atomic).</summary>
			private Dictionary<Slice, Slice> Committed { get; } = new();

			private Dictionary<string, ActorState> Actors { get; } = new(StringComparer.Ordinal);

			private Dictionary<int, WatchState> Watches { get; } = new();

			private List<TxRecord> CompletedTxs { get; } = [ ];

			/// <summary>Step intervals of the successfully committed transactions that carried a versionstamped op or stamp future.</summary>
			private List<(int Begin, int Commit)> StampedCommits { get; } = [ ];

			public HashSet<string> Run()
			{
				var steps = this.Scenario.Steps;
				for (int i = 0; i < steps.Count; i++)
				{
					Visit(i, steps[i]);
				}
				ClassifyStampMonotonicity();
				ClassifyStampReadbacks();
				ClassifyErrorOutcomes();
				return this.Hits;
			}

			private void Emit(string cellId)
			{
				// no inventory filter here: an id drifting from the inventory must FAIL the structural fact, not vanish
				this.Hits.Add(cellId);
			}

			private ActorState GetActor(string? name)
			{
				name ??= "";
				if (!this.Actors.TryGetValue(name, out var actor)) this.Actors[name] = actor = new();
				return actor;
			}

			private JsonObject? OutcomeOf(int step)
			{
				var events = this.Golden?.Events;
				if (events is null) return null;
				if (step < events.Count && events[step].Step == step) return events[step].Outcome;
				return events.FirstOrDefault(e => e.Step == step)?.Outcome;
			}

			private string? ErrorOf(int step) => OutcomeOf(step)?.Get<string?>("error", null);

			private void Visit(int i, ScenarioStep step)
			{
				var actor = GetActor(step.Actor);
				switch (step.Op)
				{
					case ScenarioOp.Begin:
					{
						actor.StartTx(i);
						break;
					}
					case ScenarioOp.Commit:
					{
						VisitCommit(i, actor);
						break;
					}
					case ScenarioOp.Reset:
					{
						SettleUnarmedWatches(actor, "watches/settle-on-reset");
						EmitStampFateCancelled(actor);
						actor.StartTx(i);
						break;
					}
					case ScenarioOp.Dispose:
					{
						SettleUnarmedWatches(actor, "watches/settle-on-dispose");
						EmitStampFateCancelled(actor);
						actor.EndTx();
						break;
					}
					case ScenarioOp.Set:
					{
						actor.Write(step.Key, "set", step.Value);
						break;
					}
					case ScenarioOp.Clear:
					{
						actor.Write(step.Key, "clear", Slice.Nil);
						break;
					}
					case ScenarioOp.ClearRange:
					{
						actor.ClearedRanges.Add((step.Key, step.EndKey));
						break;
					}
					case ScenarioOp.Atomic:
					{
						VisitAtomic(step, actor);
						break;
					}
					case ScenarioOp.SetVersionstampedKey:
					{
						int offset = step.StampOffset ?? 0;
						Emit(offset == 0 ? "versionstamps/placement-key-begin"
							: offset + 10 == step.Key.Count ? "versionstamps/placement-key-end"
							: "versionstamps/placement-key-mid");
						actor.StampedOps++;
						// the stamped key itself is unknowable pre-commit; track the write for conflict/watch purposes under the placeholder bytes
						actor.Write(step.Key, "stamped", Slice.Nil);
						break;
					}
					case ScenarioOp.SetVersionstampedValue:
					{
						Emit("versionstamps/placement-value");
						actor.StampedOps++;
						actor.Write(step.Key, "stamped", Slice.Nil);
						break;
					}
					case ScenarioOp.Get:
					{
						VisitRead(i, step, actor, step.Snapshot ? null : "get", step.Key, step.Key);
						if (!step.Snapshot) { actor.HasNonSnapshotRead = true; }
						else { actor.SnapshotReadKeys.Add(step.Key); actor.HasSnapshotRead = true; }
						break;
					}
					case ScenarioOp.GetKey:
					{
						if (step.Selector is not null)
						{
							ClassifySelector(step.Selector, actor);
							VisitRead(i, step, actor, step.Snapshot ? null : "selector", step.Selector.Key, step.Selector.Key);
							if (!step.Snapshot) { actor.HasNonSnapshotRead = true; actor.HasSelectorRead = true; }
						}
						break;
					}
					case ScenarioOp.GetRange:
					{
						if (step.Selector is not null) ClassifySelector(step.Selector, actor);
						if (step.EndSelector is not null) ClassifySelector(step.EndSelector, actor);
						var begin = step.Selector?.Key ?? Slice.Nil;
						var end = step.EndSelector?.Key ?? Slice.Nil;
						string? kind = step.Snapshot ? null : (step.Reverse ? "range-rev" : "range-fwd");
						VisitRead(i, step, actor, kind, begin, end);
						if (!step.Snapshot && step.Limit is not null) VisitRead(i, step, actor, "range-limited", begin, end);
						if (!step.Snapshot) { actor.HasNonSnapshotRead = true; actor.HasRangeRead = true; }
						break;
					}
					case ScenarioOp.GetReadVersion:
					{
						Emit("introspection/read-version");
						break;
					}
					case ScenarioOp.GetCommittedVersion:
					{
						Emit("introspection/committed-version");
						actor.SawCommittedVersion = true;
						break;
					}
					case ScenarioOp.GetVersionstamp:
					{
						Emit("introspection/versionstamp-future");
						actor.HasStampFuture = true;
						break;
					}
					case ScenarioOp.Watch:
					{
						if (step.HandleId is int handle)
						{
							this.Watches[handle] = new() { Actor = step.Actor ?? "", Key = step.Key };
							actor.TxWatches.Add(handle);
						}
						break;
					}
					case ScenarioOp.WatchMetadataVersion:
					{
						Emit("watches/metadata-version-watch");
						break;
					}
					case ScenarioOp.ExpectFired:
					{
						if (step.HandleId is int handle) ClassifyExpect(handle, fired: true);
						break;
					}
					case ScenarioOp.ExpectPending:
					{
						if (step.HandleId is int handle) ClassifyExpect(handle, fired: false);
						break;
					}
					case ScenarioOp.ExpectVersionstamp:
					{
						break; // the future's fate is classified at its transaction's end; its error outcome by the generic scan
					}
					case ScenarioOp.SetOption:
					{
						VisitSetOption(step, actor);
						break;
					}
					case ScenarioOp.TouchMetadataVersion:
					{
						Emit("introspection/metadata-version-touch");
						break;
					}
					case ScenarioOp.GetMetadataVersion:
					{
						Emit("introspection/metadata-version-get");
						break;
					}
				}
			}

			private void VisitAtomic(ScenarioStep step, ActorState actor)
			{
				string? op = step.Mutation switch
				{
					FdbMutationType.Add => "add",
					FdbMutationType.BitAnd => "bit-and",
					FdbMutationType.BitOr => "bit-or",
					FdbMutationType.BitXor => "bit-xor",
					FdbMutationType.Max => "max",
					FdbMutationType.Min => "min",
					FdbMutationType.ByteMin => "byte-min",
					FdbMutationType.ByteMax => "byte-max",
					FdbMutationType.AppendIfFits => "append-if-fits",
					FdbMutationType.CompareAndClear => "compare-and-clear",
					_ => null,
				};
				if (op is not null)
				{
					var (present, value) = LookupMerged(actor, step.Key);
					string? cas = present switch
					{
						false => "absent",
						true when !value.IsNull => step.Value.Count < value.Count ? "shorter" : step.Value.Count == value.Count ? "equal" : "longer",
						_ => null, // unknowable existing value: no case cell
					};
					if (cas is not null) Emit($"atomics/{op}-{cas}");
				}
				actor.Write(step.Key, "atomic", Slice.Nil);
			}

			private void VisitRead(int i, ScenarioStep step, ActorState actor, string? kind, Slice begin, Slice end)
			{
				// the poisoned-read leg of the RYW-disable-after-read ruling, visible only in the golden outcome
				if (actor.RywDisabledAfterRead && ErrorOf(i) == "ClientInvalidOperation")
				{
					Emit("ryw/disable-after-read-poisons-reads");
				}

				var states = OverlayStatesUnder(actor, begin, end);
				if (states.Count == 0) return;

				if (step.Snapshot)
				{
					Emit(actor.SnapshotRywDisabled ? "ryw/snapshot-ryw-disabled" : "ryw/snapshot-sees-own-writes-default");
					return;
				}
				if (kind is null) return;
				foreach (var state in states)
				{
					Emit($"ryw/{kind}-{state}");
				}
				if (begin == end) actor.ReadOwnWrite = true; // point read that hit the actor's own overlay
			}

			/// <summary>Returns the overlay-state suffixes (<c>over-set</c>, ...) of the actor's uncommitted writes under a read of [begin, end] (a point read when begin == end).</summary>
			private static List<string> OverlayStatesUnder(ActorState actor, Slice begin, Slice end)
			{
				var states = new List<string>();
				foreach (var (key, overlay) in actor.Writes)
				{
					bool covered = begin == end ? key == begin : key.CompareTo(begin) >= 0 && key.CompareTo(end) < 0;
					if (!covered) continue;
					states.Add(overlay.Kinds[^1] switch
					{
						"set" => "over-set",
						"clear" => "over-clear",
						"stamped" => "over-set", // a stamped write reads back as a set value
						_ => "over-atomic",
					});
					if (overlay.Kinds.Distinct().Count() >= 2) states.Add("over-chain");
				}
				foreach (var (rangeBegin, rangeEnd) in actor.ClearedRanges)
				{
					bool covered = begin == end
						? begin.CompareTo(rangeBegin) >= 0 && begin.CompareTo(rangeEnd) < 0
						: rangeBegin.CompareTo(end) < 0 && begin.CompareTo(rangeEnd) < 0;
					if (covered) states.Add("over-clearrange");
				}
				return states.Distinct().ToList();
			}

			private void ClassifySelector(ScenarioSelector selector, ActorState actor)
			{
				string baseName; int extra;
				if (selector.OrEqual)
				{
					if (selector.Offset >= 1) { baseName = "fgt"; extra = selector.Offset - 1; }
					else { baseName = "lle"; extra = selector.Offset; }
				}
				else
				{
					if (selector.Offset >= 1) { baseName = "fge"; extra = selector.Offset - 1; }
					else { baseName = "llt"; extra = selector.Offset; }
				}
				string offset = extra == 0 ? "off0" : extra > 0 ? "pos" : "neg";

				var keys = MergedKeys(actor);
				string landing = keys.Count == 0 ? "edge"
					: keys.Contains(selector.Key) ? "hit"
					: selector.Key.CompareTo(keys.Min) < 0 || selector.Key.CompareTo(keys.Max) > 0 ? "edge"
					: "gap";
				Emit($"selectors/{baseName}-{offset}-{landing}");
			}

			/// <summary>The keys visible to the actor: the committed store merged with its uncommitted overlay.</summary>
			private SortedSet<Slice> MergedKeys(ActorState actor)
			{
				var keys = new SortedSet<Slice>(this.Committed.Keys);
				foreach (var (rangeBegin, rangeEnd) in actor.ClearedRanges)
				{
					keys.RemoveWhere(k => k.CompareTo(rangeBegin) >= 0 && k.CompareTo(rangeEnd) < 0);
				}
				foreach (var (key, overlay) in actor.Writes)
				{
					if (overlay.Kinds[^1] == "clear") keys.Remove(key); else keys.Add(key);
				}
				return keys;
			}

			/// <summary>Resolves presence and value of a key as the actor sees it (<c>null</c> presence or Nil value = unknowable).</summary>
			private (bool? Present, Slice Value) LookupMerged(ActorState actor, Slice key)
			{
				if (actor.Writes.TryGetValue(key, out var overlay))
				{
					return overlay.Kinds[^1] switch
					{
						"set" => (true, overlay.Value),
						"clear" => (false, Slice.Nil),
						"atomic" => (null, Slice.Nil), // CompareAndClear can remove: even presence is unknowable
						_ => (true, Slice.Nil),
					};
				}
				foreach (var (rangeBegin, rangeEnd) in actor.ClearedRanges)
				{
					if (key.CompareTo(rangeBegin) >= 0 && key.CompareTo(rangeEnd) < 0) return (false, Slice.Nil);
				}
				return this.Committed.TryGetValue(key, out var value) ? (true, value) : (false, Slice.Nil);
			}

			private void VisitSetOption(ScenarioStep step, ActorState actor)
			{
				switch (step.Option)
				{
					case ScenarioTransactionOption.ReadYourWritesDisable:
					{
						bool anyRead = actor.HasNonSnapshotRead || actor.HasSnapshotRead;
						if (anyRead)
						{
							actor.RywDisabledAfterRead = true; // the poisoned reads/commit emit on their golden outcomes
						}
						else
						{
							Emit("options/ryw-disable-accepted");
							Emit(actor.HasWrite ? "ryw/disable-writes-only" : "ryw/disable-before-read");
						}
						break;
					}
					case ScenarioTransactionOption.SnapshotReadYourWritesDisable:
					{
						Emit("options/snapshot-ryw-disable-accepted");
						actor.SnapshotRywDisabled = true;
						break;
					}
				}
			}

			private void VisitCommit(int i, ActorState actor)
			{
				string? error = ErrorOf(i);
				bool success = error is null;

				if (!success)
				{
					if (actor.RywDisabledAfterRead && error == "ClientInvalidOperation") Emit("ryw/disable-after-read-poisons-commit");
					if (error == "NotCommitted")
					{
						if (actor.HasNonSnapshotRead) Emit("conflicts/read-write-conflict-loses");
						if (actor.HasRangeRead) Emit("conflicts/conflict-after-range-read");
						if (actor.HasSelectorRead) Emit("conflicts/conflict-after-selector-read");
					}
					SettleUnarmedWatches(actor, "watches/settle-on-conflicted-commit");
					if (actor.IsStamped) Emit("versionstamps/fate-conflicted");
					this.CompletedTxs.Add(new(actor.BeginStep, i, actor.WrittenKeys(), Success: false));
					actor.EndTx();
					return;
				}

				// conflict-free relations this successful commit proves
				if (actor.ReadOwnWrite) Emit("conflicts/own-read-own-write-no-conflict");
				bool blind = !actor.HasNonSnapshotRead && !actor.HasSnapshotRead;
				var written = actor.WrittenKeys();
				foreach (var past in this.CompletedTxs)
				{
					if (!past.Success || past.Commit <= actor.BeginStep) continue; // only transactions that overlapped this one
					if (blind && actor.HasWrite && past.Written.Overlaps(written)) Emit("conflicts/write-write-no-conflict");
					if (actor.HasSnapshotRead && !actor.HasNonSnapshotRead && past.Written.Overlaps(actor.SnapshotReadKeys)) Emit("conflicts/snapshot-read-no-conflict");
				}

				RecordWatchTouches(actor);
				ApplyOverlay(actor);
				ArmWatches(actor);

				if (actor.IsStamped)
				{
					Emit("versionstamps/fate-committed");
					if (actor.StampedOps >= 2) Emit("versionstamps/two-stamps-user-versions");
					if (actor.HasStampFuture && actor.SawCommittedVersion) Emit("versionstamps/stamp-equals-commit-version");
					this.StampedCommits.Add((actor.BeginStep, i));
				}

				this.CompletedTxs.Add(new(actor.BeginStep, i, written, Success: true));
				actor.EndTx();
			}

			/// <summary>Records, on every ARMED watch whose key this commit touches, the touch kind and its value relations (against the pre-commit committed state).</summary>
			private void RecordWatchTouches(ActorState actor)
			{
				foreach (var watch in this.Watches.Values)
				{
					if (!watch.Armed) continue;

					string? kind = null; bool identical = false; bool netIdenticalAba = false; Slice newValue = Slice.Nil; bool newPresent = true;
					if (actor.Writes.TryGetValue(watch.Key, out var overlay))
					{
						kind = overlay.Kinds[^1] switch { "set" => "set", "clear" => "clear", "stamped" => "versionstamped", _ => "atomic" };
						this.Committed.TryGetValue(watch.Key, out var before);
						if (kind == "set")
						{
							newValue = overlay.Value;
							identical = overlay.Kinds.Count == 1 && !before.IsNull && before == overlay.Value;
							netIdenticalAba = overlay.Kinds.Count >= 2 && !before.IsNull && before == overlay.Value;
						}
						else if (kind == "clear")
						{
							newPresent = false;
						}
					}
					else
					{
						foreach (var (rangeBegin, rangeEnd) in actor.ClearedRanges)
						{
							if (watch.Key.CompareTo(rangeBegin) >= 0 && watch.Key.CompareTo(rangeEnd) < 0) { kind = "clearrange"; newPresent = false; break; }
						}
					}
					if (kind is not null)
					{
						watch.Touches.Add(new(kind, identical, netIdenticalAba, newValue, newPresent));
						// the two-commit ABA proof: the watch already fired on an intermediate change, and this later commit restores the baseline
						if (watch.ObservedFired && watch.Touches.Count >= 2 && kind == "set" && watch.BaselinePresent && newValue == watch.BaselineValue)
						{
							Emit("watches/fire-two-commit-aba");
						}
					}
				}
			}

			private void ApplyOverlay(ActorState actor)
			{
				foreach (var (rangeBegin, rangeEnd) in actor.ClearedRanges)
				{
					foreach (var key in this.Committed.Keys.Where(k => k.CompareTo(rangeBegin) >= 0 && k.CompareTo(rangeEnd) < 0).ToList())
					{
						this.Committed.Remove(key);
					}
				}
				foreach (var (key, overlay) in actor.Writes)
				{
					switch (overlay.Kinds[^1])
					{
						case "set": this.Committed[key] = overlay.Value; break;
						case "clear": this.Committed.Remove(key); break;
						default: this.Committed[key] = Slice.Nil; break; // present, bytes unknowable
					}
				}
			}

			private void ArmWatches(ActorState actor)
			{
				foreach (var handle in actor.TxWatches)
				{
					if (!this.Watches.TryGetValue(handle, out var watch)) continue;
					watch.Armed = true;
					watch.BaselinePresent = this.Committed.TryGetValue(watch.Key, out var baseline);
					watch.BaselineValue = baseline;
					if (actor.Writes.ContainsKey(watch.Key)) Emit("watches/own-tx-write-baseline");
				}
			}

			private void SettleUnarmedWatches(ActorState actor, string cellId)
			{
				foreach (var handle in actor.TxWatches)
				{
					if (this.Watches.TryGetValue(handle, out var watch) && !watch.Armed) Emit(cellId);
				}
			}

			private void EmitStampFateCancelled(ActorState actor)
			{
				if (actor.IsStamped) Emit("versionstamps/fate-cancelled");
			}

			private void ClassifyExpect(int handle, bool fired)
			{
				if (!this.Watches.TryGetValue(handle, out var watch)) return;
				var touches = watch.Touches;
				if (fired)
				{
					watch.ObservedFired = true;
					if (touches.Count >= 2)
					{
						var last = touches[^1];
						bool backToBaseline = last.Kind == "set" && !last.NewValue.IsNull && watch.BaselinePresent && last.NewValue == watch.BaselineValue;
						if (backToBaseline) Emit("watches/fire-two-commit-aba");
					}
					var trigger = touches.LastOrDefault(t => !t.Identical);
					if (trigger is not null) Emit($"watches/fire-on-{trigger.Kind}");
				}
				else
				{
					if (touches.Count == 0) Emit("watches/pending-untouched-key");
					else if (touches.All(t => t.Identical)) Emit("watches/no-fire-identical-value");
					if (touches.Any(t => t.NetIdenticalAba)) Emit("watches/no-fire-single-commit-aba");
				}
			}

			private void ClassifyStampMonotonicity()
			{
				var commits = this.StampedCommits;
				for (int a = 0; a < commits.Count; a++)
				{
					for (int b = a + 1; b < commits.Count; b++)
					{
						bool overlaps = commits[a].Begin < commits[b].Commit && commits[b].Begin < commits[a].Commit;
						Emit(overlaps ? "versionstamps/monotonic-interleaved" : "versionstamps/monotonic-sequential");
					}
				}
			}

			/// <summary>Post-commit reads whose golden outcome renders an observed stamp (<c>&lt;vN#o&gt;</c>) prove the stamped bytes read back materialized.</summary>
			private void ClassifyStampReadbacks()
			{
				var events = this.Golden?.Events;
				if (events is null || this.StampedCommits.Count == 0) return;
				int firstStamped = this.StampedCommits.Min(c => c.Commit);
				foreach (var e in events)
				{
					if (e.Step <= firstStamped || (e.Op != "Get" && e.Op != "GetRange")) continue;
					var rendered = e.Outcome.ToJsonText();
					if (!rendered.Contains("<v", StringComparison.Ordinal) || !rendered.Contains('#')) continue;
					Emit(e.Op == "Get" ? "versionstamps/stamped-value-readback" : "versionstamps/stamped-key-readback");
				}
			}

			/// <summary>Maps every fdb error outcome recorded in the golden to its <c>errors/</c> cell.</summary>
			private void ClassifyErrorOutcomes()
			{
				var events = this.Golden?.Events;
				if (events is null) return;
				foreach (var e in events)
				{
					var error = e.Outcome.Get<string?>("error", null);
					if (error is not null) Emit($"errors/{ToKebab(error)}");
				}
			}

			private static string ToKebab(string pascal)
			{
				var sb = new StringBuilder(pascal.Length + 4);
				foreach (char c in pascal)
				{
					if (char.IsUpper(c))
					{
						if (sb.Length > 0) sb.Append('-');
						sb.Append(char.ToLowerInvariant(c));
					}
					else
					{
						sb.Append(c);
					}
				}
				return sb.ToString();
			}

		}

		private sealed class ActorState
		{

			public int BeginStep = -1;

			public Dictionary<Slice, OverlayEntry> Writes = new();

			public List<(Slice Begin, Slice End)> ClearedRanges = [ ];

			public bool HasNonSnapshotRead, HasSnapshotRead, HasRangeRead, HasSelectorRead, HasWrite, ReadOwnWrite;

			public bool RywDisabledAfterRead, SnapshotRywDisabled, SawCommittedVersion, HasStampFuture;

			public int StampedOps;

			public List<int> TxWatches = [ ];

			public HashSet<Slice> SnapshotReadKeys = new();

			public bool IsStamped => this.StampedOps > 0 || this.HasStampFuture;

			public void StartTx(int step)
			{
				EndTx();
				this.BeginStep = step;
			}

			public void EndTx()
			{
				this.BeginStep = -1;
				this.Writes = new();
				this.ClearedRanges = [ ];
				this.HasNonSnapshotRead = this.HasSnapshotRead = this.HasRangeRead = this.HasSelectorRead = this.HasWrite = this.ReadOwnWrite = false;
				this.RywDisabledAfterRead = this.SnapshotRywDisabled = this.SawCommittedVersion = this.HasStampFuture = false;
				this.StampedOps = 0;
				this.TxWatches = [ ];
				this.SnapshotReadKeys = new();
			}

			public void Write(Slice key, string kind, Slice value)
			{
				this.HasWrite = true;
				if (!this.Writes.TryGetValue(key, out var overlay)) this.Writes[key] = overlay = new();
				overlay.Kinds.Add(kind);
				overlay.Value = kind == "set" ? value : Slice.Nil;
			}

			public HashSet<Slice> WrittenKeys() => [ .. this.Writes.Keys ];

		}

		private sealed class OverlayEntry
		{

			public List<string> Kinds = [ ];

			public Slice Value = Slice.Nil;

		}

		private sealed record TxRecord(int Begin, int Commit, HashSet<Slice> Written, bool Success);

		private sealed record Touch(string Kind, bool Identical, bool NetIdenticalAba, Slice NewValue, bool NewPresent);

		private sealed class WatchState
		{

			public required string Actor { get; init; }

			public required Slice Key { get; init; }

			public bool Armed;

			public bool ObservedFired;

			public bool BaselinePresent;

			public Slice BaselineValue = Slice.Nil;

			public List<Touch> Touches { get; } = [ ];

		}

	}

}
