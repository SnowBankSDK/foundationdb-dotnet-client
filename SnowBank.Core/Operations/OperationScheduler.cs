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

namespace SnowBank.Operations
{
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using SnowBank.Data.Json;

	/// <summary>Settings of an operation type</summary>
	[PublicAPI]
	public sealed record OperationTypeOptions
	{
		/// <summary><c>true</c> when the code of the operation checks <see cref="IOperationContext.StopRequested"/>; a stop request on any other type cancels it at once</summary>
		public bool Stoppable { get; init; }

		/// <summary>Time a run has to return after a stop request, before its token fires</summary>
		public TimeSpan StopGrace { get; init; } = TimeSpan.FromSeconds(30);

		/// <summary>Number of runs of this type that execute at once; runs beyond it wait as <see cref="OperationPhase.Queued"/>. 0 means no limit</summary>
		public int MaxConcurrency { get; init; }

		/// <summary>Time an operation stays known after it ended</summary>
		public TimeSpan EndedRetention { get; init; } = TimeSpan.FromMinutes(5);

		/// <summary>Time a suspended operation keeps its continuation; past it, the operation becomes <see cref="OperationPhase.Abandoned"/></summary>
		public TimeSpan SuspendedRetention { get; init; } = TimeSpan.FromHours(1);

		/// <summary>Decides, at start, who may observe and control an operation of this type; by default only the principal that started it</summary>
		public Func<OperationActor?, OperationAccess>? Access { get; init; }
	}

	/// <summary>Options of one start of an operation</summary>
	[PublicAPI]
	public sealed record OperationStartOptions
	{
		/// <summary>Principal that starts the operation, if known</summary>
		public OperationActor? StartedBy { get; init; }

		/// <summary>Id of the operation (32 lowercase hexadecimal digits), or <c>null</c> for a new random id</summary>
		public string? Id { get; init; }
	}

	/// <summary>Handle on a started operation</summary>
	/// <typeparam name="TResult">Type of the result</typeparam>
	[PublicAPI]
	public sealed class OperationHandle<TResult>
	{
		internal OperationHandle(IOperationState state, Task<OperationOutcome<TResult>> completion)
		{
			this.State = state;
			this.Completion = completion;
		}

		/// <summary>Id of the operation</summary>
		public string Id => this.State.Id;

		/// <summary>Live view of the operation</summary>
		public IOperationState State { get; }

		/// <summary>Completes when the operation reaches a phase it never leaves (succeeded, failed, cancelled, abandoned); a suspension is not an end</summary>
		public Task<OperationOutcome<TResult>> Completion { get; }
	}

	/// <summary>Runs operations, and keeps their state for observers</summary>
	/// <remarks>
	/// <para>Every change of phase is applied under one lock, and the first change wins: a resume and the expiry of a suspension, or a stop and the end of a run, never both apply.</para>
	/// <para>The outcome of a run is the one its code returns. A stop request only sets <see cref="IOperationContext.StopRequested"/>, and fires the token of the run once the grace period has passed.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class OperationScheduler
	{

		/// <summary>Creates a scheduler</summary>
		/// <param name="time">Clock used for durations, grace periods and retentions</param>
		/// <param name="publisher">Receives the urgent changes of every operation, or <c>null</c> to discard them</param>
		public OperationScheduler(TimeProvider time, IOperationPublisher? publisher = null)
		{
			Contract.NotNull(time);
			this.Time = time;
			this.Publisher = publisher ?? NoOpOperationPublisher.Instance;
		}

		internal TimeProvider Time { get; }

		private IOperationPublisher Publisher { get; }

		internal object Gate { get; } = new();

		private Dictionary<string, OperationRegistration> Registrations { get; } = new(StringComparer.Ordinal);

		private Dictionary<string, OperationEntry> Entries { get; } = new(StringComparer.Ordinal);

		/// <summary>Registers an operation type</summary>
		/// <param name="type">Type of the operation</param>
		/// <param name="operation">Code of the operation</param>
		/// <param name="options">Settings of the type, or <c>null</c> for the defaults</param>
		public void Register<TArgs, TResult>(OperationType type, IOperation<TArgs, TResult> operation, OperationTypeOptions? options = null)
		{
			Contract.NotNull(type);
			Contract.NotNull(operation);
			lock (this.Gate)
			{
				if (this.Registrations.ContainsKey(type.Name)) throw new InvalidOperationException($"Operation type '{type.Name}' is already registered.");
				this.Registrations[type.Name] = new OperationRegistration<TArgs, TResult>(type, operation, options ?? new());
			}
		}

		/// <summary>Starts an operation</summary>
		/// <param name="type">Type of the operation, registered with <see cref="Register{TArgs,TResult}"/></param>
		/// <param name="args">Arguments of the operation</param>
		/// <param name="options">Options of this start</param>
		/// <returns>Handle whose id is known at once</returns>
		public OperationHandle<TResult> Start<TArgs, TResult>(OperationType type, TArgs args, OperationStartOptions? options = null)
		{
			Contract.NotNull(type);
			OperationEntry entry;
			lock (this.Gate)
			{
				if (!this.Registrations.TryGetValue(type.Name, out var reg) || reg is not OperationRegistration<TArgs, TResult> typed)
				{
					throw new InvalidOperationException($"Operation type '{type.Name}' is not registered with arguments {typeof(TArgs).Name} and result {typeof(TResult).Name}.");
				}
				var id = options?.Id ?? Guid.NewGuid().ToString("N");
				if (this.Entries.ContainsKey(id)) throw new InvalidOperationException($"Operation '{id}' already exists.");

				var starter = options?.StartedBy;
				entry = new OperationEntry(this, typed, id, args, starter, typed.Options.Access?.Invoke(starter) ?? OperationAccess.StarterOnly(starter));
				this.Entries[id] = entry;
				Admit(entry);
			}
			Notify(entry);
			return new(entry, ConvertCompletion<TResult>(entry.Completion.Task));
		}

		private static async Task<OperationOutcome<TResult>> ConvertCompletion<TResult>(Task<(OperationOutcome Outcome, object? Result)> completion)
		{
			var (outcome, result) = await completion.ConfigureAwait(false);
			return outcome.Kind == OperationOutcomeKind.Succeeded ? OperationOutcome<TResult>.Success((TResult) result!) : outcome;
		}

		/// <summary>Returns a live view of an operation known to this scheduler</summary>
		public IOperationState? TryGet(string id)
		{
			lock (this.Gate)
			{
				return this.Entries.TryGetValue(id, out var entry) ? entry : null;
			}
		}

		/// <summary>Lists the operations known to this scheduler: queued, running, suspended, or ended within their retention</summary>
		public IOperationState[] List()
		{
			lock (this.Gate)
			{
				return [ ..this.Entries.Values ];
			}
		}

		/// <summary>Asks an operation to stop at its next clean point</summary>
		/// <param name="id">Id of the operation</param>
		/// <param name="actor">Principal that asks, checked against the access of the operation, or <c>null</c> for the host itself</param>
		/// <remarks>A queued operation leaves the queue as cancelled. A type that is not <see cref="OperationTypeOptions.Stoppable"/> is cancelled at once.</remarks>
		public OperationControlStatus Stop(string id, OperationActor? actor)
		{
			OperationEntry? entry;
			OperationControlStatus status;
			lock (this.Gate)
			{
				status = Find(id, actor, out entry);
				if (status != OperationControlStatus.Ok) return status;
				status = entry!.RequestStop();
			}
			Notify(entry);
			return status;
		}

		/// <summary>Cancels an operation: its run token fires, or it leaves the queue, or its continuation is dropped</summary>
		/// <param name="id">Id of the operation</param>
		/// <param name="actor">Principal that asks, checked against the access of the operation, or <c>null</c> for the host itself</param>
		public OperationControlStatus Cancel(string id, OperationActor? actor)
		{
			OperationEntry? entry;
			OperationControlStatus status;
			lock (this.Gate)
			{
				status = Find(id, actor, out entry);
				if (status != OperationControlStatus.Ok) return status;
				status = entry!.RequestCancel();
			}
			Notify(entry);
			return status;
		}

		/// <summary>Starts a new run of a suspended operation, which receives the continuation of the previous run</summary>
		/// <param name="id">Id of the operation</param>
		/// <param name="actor">Principal that asks, checked against the access of the operation, or <c>null</c> for the host itself</param>
		public OperationControlStatus Resume(string id, OperationActor? actor)
		{
			OperationEntry? entry;
			OperationControlStatus status;
			lock (this.Gate)
			{
				status = Find(id, actor, out entry);
				if (status != OperationControlStatus.Ok) return status;
				status = entry!.RequestResume();
			}
			Notify(entry);
			return status;
		}

		private OperationControlStatus Find(string id, OperationActor? actor, out OperationEntry? entry)
		{
			if (!this.Entries.TryGetValue(id, out entry)) return OperationControlStatus.NotFound;
			if (actor is not null && !entry.Access.CanControl(actor)) return OperationControlStatus.Denied;
			return OperationControlStatus.Ok;
		}

		/// <summary>Starts a run of the entry, or queues it when its type is at its concurrency limit (called under the lock)</summary>
		internal void Admit(OperationEntry entry)
		{
			var reg = entry.Registration;
			if (reg.Options.MaxConcurrency > 0 && reg.Running >= reg.Options.MaxConcurrency)
			{
				entry.Phase = OperationPhase.Queued;
				entry.Version++;
				reg.Queue.Enqueue(entry);
				return;
			}
			reg.Running++;
			entry.StartRun();
		}

		/// <summary>A run released its slot: starts the next queued entry of the same type (called under the lock)</summary>
		internal void Release(OperationRegistration reg)
		{
			reg.Running--;
			while (reg.Queue.Count > 0)
			{
				var next = reg.Queue.Dequeue();
				if (next.Phase != OperationPhase.Queued) continue; // left the queue on a stop or a cancel
				reg.Running++;
				next.StartRun();
				ThreadPool.QueueUserWorkItem(static s => ((OperationEntry) s!).Notify(), next);
				return;
			}
		}

		/// <summary>Forgets an entry at the end of its retention</summary>
		internal void Remove(OperationEntry entry)
		{
			lock (this.Gate)
			{
				if (!this.Entries.TryGetValue(entry.Id, out var current) || !ReferenceEquals(current, entry)) return;
				this.Entries.Remove(entry.Id);
			}
			try
			{
				this.Publisher.OnRemoved(entry);
			}
			catch (Exception)
			{
				// a publisher logs its own errors: its failure must not reach the scheduler
			}
		}

		/// <summary>Hands an urgent change to the publisher</summary>
		/// <remarks>This runs on the thread of the operation (a step that opens, a run that ends): a publisher error must never fail the operation, so it is ignored here. A publisher logs its own errors.</remarks>
		internal void Notify(OperationEntry entry)
		{
			try
			{
				this.Publisher.OnChanged(entry);
			}
			catch (Exception)
			{
				// see remarks
			}
		}

	}

	internal abstract class OperationRegistration
	{
		protected OperationRegistration(OperationType type, OperationTypeOptions options)
		{
			this.Type = type;
			this.Options = options;
		}

		public OperationType Type { get; }

		public OperationTypeOptions Options { get; }

		/// <summary>Runs that execute now (guarded by the scheduler lock)</summary>
		public int Running { get; set; }

		/// <summary>Entries that wait for a slot (guarded by the scheduler lock)</summary>
		public Queue<OperationEntry> Queue { get; } = new();

		public abstract ValueTask<(OperationOutcome Outcome, object? Result)> Execute(IOperationContext ctx, object? args, CancellationToken ct);
	}

	internal sealed class OperationRegistration<TArgs, TResult> : OperationRegistration
	{
		public OperationRegistration(OperationType type, IOperation<TArgs, TResult> operation, OperationTypeOptions options) : base(type, options)
		{
			this.Operation = operation;
		}

		private IOperation<TArgs, TResult> Operation { get; }

		public override async ValueTask<(OperationOutcome Outcome, object? Result)> Execute(IOperationContext ctx, object? args, CancellationToken ct)
		{
			var outcome = await this.Operation.RunAsync(ctx, (TArgs) args!, ct).ConfigureAwait(false);
			return (outcome.WithoutResult(), outcome.Succeeded ? outcome.Result : null);
		}
	}

	/// <summary>Node of the step tree of an operation (guarded by the scheduler lock)</summary>
	internal sealed class OperationStepNode
	{
		public OperationStepNode(OperationStepNode? parent, string id, string label)
		{
			this.Parent = parent;
			this.Id = id;
			this.Label = label;
		}

		public OperationStepNode? Parent { get; }

		public string Id { get; }

		public string Label { get; set; }

		public OperationStepPhase Phase { get; set; } = OperationStepPhase.Planned;

		public long StartTimestamp { get; set; }

		public long EndTimestamp { get; set; }

		public OperationError? Error { get; set; }

		public List<OperationStepNode> Children { get; } = [ ];

		public List<OperationProgressTracker> Progress { get; } = [ ];

		public OperationStepNode? FindChild(string id) => this.Children.Find(c => c.Id == id);

		public void AttachProgress(OperationProgress progress)
		{
			this.Progress.RemoveAll(p => p.Counter.Id == progress.Id);
			this.Progress.Add(new(progress));
		}
	}

	/// <summary>A counter, plus what the snapshots saw of it, to measure how long its value has not moved</summary>
	internal sealed class OperationProgressTracker
	{
		public OperationProgressTracker(OperationProgress counter)
		{
			this.Counter = counter;
			this.LastDone = counter.Done;
		}

		public OperationProgress Counter { get; }

		public long LastDone { get; set; }

		public long LastChangeTimestamp { get; set; }
	}

	/// <summary>An operation known to a scheduler: its phase, its step tree, and its current run</summary>
	internal sealed class OperationEntry : IOperationState
	{
		public OperationEntry(OperationScheduler scheduler, OperationRegistration registration, string id, object? args, OperationActor? startedBy, OperationAccess access)
		{
			this.Scheduler = scheduler;
			this.Registration = registration;
			this.Id = id;
			this.Args = args;
			this.StartedBy = startedBy;
			this.Access = access;
			this.Root = new(null, "", "");
		}

		public OperationScheduler Scheduler { get; }

		public OperationRegistration Registration { get; }

		public string Id { get; }

		public OperationType Type => this.Registration.Type;

		public object? Args { get; }

		public OperationActor? StartedBy { get; }

		public OperationAccess Access { get; }

		// everything below is guarded by the scheduler lock

		public OperationPhase Phase { get; set; } = OperationPhase.Queued;

		public long Version { get; set; }

		public int RunNumber { get; private set; }

		public OperationStepNode Root { get; }

		public JsonValue? Continuation { get; private set; }

		public OperationError? Error { get; private set; }

		public long RunStartTimestamp { get; private set; }

		public long RunEndTimestamp { get; private set; }

		public OperationRunContext? Context { get; private set; }

		private CancellationTokenSource? RunCancellation { get; set; }

		private CancellationTokenSource? StopCancellation { get; set; }

		private ITimer? Timer { get; set; }

		public TaskCompletionSource<(OperationOutcome Outcome, object? Result)> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		private TimeProvider Time => this.Scheduler.Time;

		public void Notify() => this.Scheduler.Notify(this);

		/// <summary>Starts the next run (called under the lock, after the slot was taken)</summary>
		public void StartRun()
		{
			this.RunNumber++;
			this.Phase = OperationPhase.Running;
			this.Error = null;
			this.Version++;
			this.RunStartTimestamp = this.Time.GetTimestamp();
			this.RunEndTimestamp = 0;
			this.RunCancellation = new();
			this.StopCancellation = new();
			var ctx = new OperationRunContext(this, this.RunNumber, this.Continuation, this.StopCancellation.Token, this.RunCancellation.Token);
			this.Context = ctx;
			_ = Task.Run(() => ExecuteRun(ctx));
		}

		private async Task ExecuteRun(OperationRunContext ctx)
		{
			OperationOutcome outcome;
			object? result = null;
			try
			{
				(outcome, result) = await this.Registration.Execute(ctx, this.Args, ctx.RunToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (ctx.RunToken.IsCancellationRequested || ctx.StopRequested)
			{
				outcome = OperationOutcome.Cancelled();
			}
			catch (Exception e)
			{
				outcome = OperationOutcome.Failed(new() { Code = "exception", Message = e.Message });
			}
			Settle(ctx, outcome, result);
		}

		/// <summary>Applies the outcome of a run</summary>
		private void Settle(OperationRunContext ctx, OperationOutcome outcome, object? result)
		{
			lock (this.Scheduler.Gate)
			{
				if (!ReferenceEquals(this.Context, ctx)) return;
				this.Context = null;
				this.Timer?.Dispose();
				this.Timer = null;
				this.RunEndTimestamp = this.Time.GetTimestamp();

				var open = outcome.Kind switch
				{
					OperationOutcomeKind.Succeeded => OperationStepPhase.Succeeded,
					OperationOutcomeKind.Failed => OperationStepPhase.Failed,
					OperationOutcomeKind.Cancelled => OperationStepPhase.Cancelled,
					_ => OperationStepPhase.Suspended,
				};
				EndOpenSteps(this.Root, open, outcome.Kind == OperationOutcomeKind.Suspended ? null : OperationStepPhase.Skipped);

				this.Error = outcome.Error;
				if (outcome.Kind == OperationOutcomeKind.Suspended)
				{
					this.Phase = OperationPhase.Suspended;
					this.Continuation = outcome.Continuation;
					this.Timer = this.Time.CreateTimer(static s => ((OperationEntry) s!).Abandon(), this, this.Registration.Options.SuspendedRetention, Timeout.InfiniteTimeSpan);
				}
				else
				{
					End(outcome.Kind switch
					{
						OperationOutcomeKind.Succeeded => OperationPhase.Succeeded,
						OperationOutcomeKind.Failed => OperationPhase.Failed,
						_ => OperationPhase.Cancelled,
					}, outcome, result);
				}
				this.Version++;
				this.Scheduler.Release(this.Registration);
			}
			Notify();
		}

		/// <summary>Moves to a final phase (called under the lock)</summary>
		private void End(OperationPhase phase, OperationOutcome outcome, object? result)
		{
			this.Phase = phase;
			this.Continuation = null;
			this.Timer?.Dispose();
			this.Timer = this.Time.CreateTimer(static s => { var e = (OperationEntry) s!; e.Scheduler.Remove(e); }, this, this.Registration.Options.EndedRetention, Timeout.InfiniteTimeSpan);
			this.Completion.TrySetResult((outcome, result));
		}

		private static void EndOpenSteps(OperationStepNode node, OperationStepPhase running, OperationStepPhase? planned)
		{
			foreach (var child in node.Children)
			{
				if (child.Phase == OperationStepPhase.Running)
				{
					child.Phase = running;
				}
				else if (child.Phase == OperationStepPhase.Planned && planned is not null)
				{
					child.Phase = planned.Value;
				}
				EndOpenSteps(child, running, planned);
			}
		}

		/// <summary>The suspension outlived its retention</summary>
		private void Abandon()
		{
			lock (this.Scheduler.Gate)
			{
				if (this.Phase != OperationPhase.Suspended) return;
				EndOpenSteps(this.Root, OperationStepPhase.Cancelled, OperationStepPhase.Skipped);
				SuspendedToEnd(OperationPhase.Abandoned);
				this.Version++;
			}
			Notify();
		}

		private void SuspendedToEnd(OperationPhase phase)
		{
			// a suspended operation has no run: the steps it left suspended will never continue
			MarkSuspendedSteps(this.Root, OperationStepPhase.Cancelled);
			End(phase, OperationOutcome.Cancelled(), null);
		}

		private static void MarkSuspendedSteps(OperationStepNode node, OperationStepPhase phase)
		{
			foreach (var child in node.Children)
			{
				if (child.Phase == OperationStepPhase.Suspended) child.Phase = phase;
				MarkSuspendedSteps(child, phase);
			}
		}

		public OperationControlStatus RequestStop()
		{
			switch (this.Phase)
			{
				case OperationPhase.Queued:
				{
					// the queue skips entries that are no longer queued
					this.Version++;
					End(OperationPhase.Cancelled, OperationOutcome.Cancelled(), null);
					return OperationControlStatus.Ok;
				}
				case OperationPhase.Running:
				{
					this.Phase = OperationPhase.Stopping;
					this.Version++;
					if (!this.Registration.Options.Stoppable)
					{
						this.RunCancellation!.Cancel();
						return OperationControlStatus.Ok;
					}
					this.StopCancellation!.Cancel();
					this.Timer?.Dispose();
					this.Timer = this.Time.CreateTimer(static s => ((OperationEntry) s!).EscalateStop(), this, this.Registration.Options.StopGrace, Timeout.InfiniteTimeSpan);
					return OperationControlStatus.Ok;
				}
				case OperationPhase.Stopping:
				case OperationPhase.Suspended:
				{
					return OperationControlStatus.Ok;
				}
				default:
				{
					return OperationControlStatus.Ended;
				}
			}
		}

		private void EscalateStop()
		{
			lock (this.Scheduler.Gate)
			{
				if (this.Phase != OperationPhase.Stopping) return;
				this.RunCancellation?.Cancel();
			}
		}

		public OperationControlStatus RequestCancel()
		{
			switch (this.Phase)
			{
				case OperationPhase.Queued:
				{
					this.Version++;
					End(OperationPhase.Cancelled, OperationOutcome.Cancelled(), null);
					return OperationControlStatus.Ok;
				}
				case OperationPhase.Running:
				case OperationPhase.Stopping:
				{
					this.Phase = OperationPhase.Stopping;
					this.Version++;
					this.RunCancellation!.Cancel();
					return OperationControlStatus.Ok;
				}
				case OperationPhase.Suspended:
				{
					this.Version++;
					EndOpenSteps(this.Root, OperationStepPhase.Cancelled, OperationStepPhase.Skipped);
					SuspendedToEnd(OperationPhase.Cancelled);
					return OperationControlStatus.Ok;
				}
				default:
				{
					return OperationControlStatus.Ended;
				}
			}
		}

		public OperationControlStatus RequestResume()
		{
			switch (this.Phase)
			{
				case OperationPhase.Suspended:
				{
					this.Timer?.Dispose();
					this.Timer = null;
					this.Scheduler.Admit(this);
					return OperationControlStatus.Ok;
				}
				case OperationPhase.Succeeded or OperationPhase.Failed or OperationPhase.Cancelled or OperationPhase.Abandoned:
				{
					return OperationControlStatus.Ended;
				}
				default:
				{
					return OperationControlStatus.NotSuspended;
				}
			}
		}

		public OperationSnapshot GetSnapshot()
		{
			lock (this.Scheduler.Gate)
			{
				var now = this.Time.GetTimestamp();
				var ctx = this.Context;
				return new()
				{
					Id = this.Id,
					Type = this.Type,
					Phase = this.Phase,
					Run = this.RunNumber,
					Version = this.Version,
					StartedBy = this.StartedBy,
					Access = this.Access,
					Elapsed = this.RunStartTimestamp == 0 ? TimeSpan.Zero : this.Time.GetElapsedTime(this.RunStartTimestamp, this.RunEndTimestamp != 0 ? this.RunEndTimestamp : now),
					Progress = SnapshotProgress(this.Root, now),
					Steps = SnapshotSteps(this.Root, now),
					Error = this.Error,
					CanStop = this.Phase == OperationPhase.Running && this.Registration.Options.Stoppable && ctx is not null,
					CanResume = this.Phase == OperationPhase.Suspended,
				};
			}
		}

		private List<OperationProgressSnapshot> SnapshotProgress(OperationStepNode node, long now)
		{
			var list = new List<OperationProgressSnapshot>(node.Progress.Count);
			foreach (var tracker in node.Progress)
			{
				var done = tracker.Counter.Done;
				if (done != tracker.LastDone || tracker.LastChangeTimestamp == 0)
				{
					tracker.LastDone = done;
					tracker.LastChangeTimestamp = now;
				}
				list.Add(new()
				{
					Id = tracker.Counter.Id,
					Unit = tracker.Counter.Unit,
					Done = done,
					Total = tracker.Counter.Total,
					Idle = this.Time.GetElapsedTime(tracker.LastChangeTimestamp, now),
				});
			}
			return list;
		}

		private List<OperationStepSnapshot> SnapshotSteps(OperationStepNode node, long now)
		{
			var list = new List<OperationStepSnapshot>(node.Children.Count);
			foreach (var child in node.Children)
			{
				list.Add(new()
				{
					Id = child.Id,
					Label = child.Label,
					Phase = child.Phase,
					Elapsed = child.StartTimestamp == 0 ? null : this.Time.GetElapsedTime(child.StartTimestamp, child.EndTimestamp != 0 ? child.EndTimestamp : now),
					Error = child.Error,
					Progress = SnapshotProgress(child, now),
					Steps = SnapshotSteps(child, now),
				});
			}
			return list;
		}

	}

	/// <summary>Context of one run of an operation</summary>
	internal sealed class OperationRunContext : IOperationContext
	{

		/// <summary>Step open in the current flow of execution; parallel branches of one run each see their own</summary>
		private static readonly AsyncLocal<OperationStepNode?> CurrentStep = new();

		public OperationRunContext(OperationEntry entry, int run, JsonValue? continuation, CancellationToken stopToken, CancellationToken runToken)
		{
			this.Entry = entry;
			this.Run = run;
			this.Continuation = continuation;
			this.StopToken = stopToken;
			this.RunToken = runToken;
		}

		private OperationEntry Entry { get; }

		public string Id => this.Entry.Id;

		public OperationType Type => this.Entry.Type;

		public int Run { get; }

		public OperationActor? StartedBy => this.Entry.StartedBy;

		public JsonValue? Continuation { get; }

		public bool StopRequested => this.StopToken.IsCancellationRequested;

		public CancellationToken StopToken { get; }

		public CancellationToken RunToken { get; }

		/// <summary>Outcome the code announced with <see cref="Suspend"/> or <see cref="Fail"/> since the last step opened; steps closed while unwinding take it</summary>
		private OperationOutcomeKind PendingKind { get; set; }

		private object Gate => this.Entry.Scheduler.Gate;

		private OperationStepNode CurrentParent()
		{
			var current = CurrentStep.Value;
			// a node of another operation (or of a previous run that left the async flow) is not a parent here
			return current is not null && IsInTree(current) && current.Phase == OperationStepPhase.Running ? current : this.Entry.Root;
		}

		private bool IsInTree(OperationStepNode node)
		{
			while (node.Parent is not null) node = node.Parent;
			return ReferenceEquals(node, this.Entry.Root);
		}

		public void PlanSteps(params (string Id, string Label)[] steps)
		{
			Contract.NotNull(steps);
			lock (this.Gate)
			{
				var parent = CurrentParent();
				foreach (var (id, label) in steps)
				{
					var node = parent.FindChild(id);
					if (node is null)
					{
						parent.Children.Add(new(parent, id, label));
					}
					else if (node.Phase == OperationStepPhase.Planned)
					{
						node.Label = label;
					}
				}
				this.Entry.Version++;
			}
			this.Entry.Notify();
		}

		public OperationStepScope Step(string id, string? label = null)
		{
			Contract.NotNullOrEmpty(id);
			OperationStepNode node;
			lock (this.Gate)
			{
				var parent = CurrentParent();
				node = parent.FindChild(id) ?? AddChild(parent, id, label);
				if (label is not null) node.Label = label;
				node.Phase = OperationStepPhase.Running;
				node.Error = null;
				node.StartTimestamp = this.Entry.Scheduler.Time.GetTimestamp();
				node.EndTimestamp = 0;
				this.Entry.Version++;
			}
			this.PendingKind = default;
			CurrentStep.Value = node;
			this.Entry.Notify();
			return new(this, node);

			static OperationStepNode AddChild(OperationStepNode parent, string id, string? label)
			{
				var child = new OperationStepNode(parent, id, label ?? id);
				parent.Children.Add(child);
				return child;
			}
		}

		internal void CloseStep(OperationStepNode node, OperationStepPhase? phase, OperationError? error)
		{
			lock (this.Gate)
			{
				if (node.Phase != OperationStepPhase.Running) return; // already closed, or closed by the end of the run
				node.Phase = phase ?? (this.RunToken.IsCancellationRequested ? OperationStepPhase.Cancelled : this.PendingKind switch
				{
					OperationOutcomeKind.Suspended => OperationStepPhase.Suspended,
					OperationOutcomeKind.Failed => OperationStepPhase.Failed,
					_ => OperationStepPhase.Succeeded,
				});
				node.Error = error;
				node.EndTimestamp = this.Entry.Scheduler.Time.GetTimestamp();
				this.Entry.Version++;
			}
			if (ReferenceEquals(CurrentStep.Value, node))
			{
				CurrentStep.Value = node.Parent?.Parent is null ? null : node.Parent;
			}
			this.Entry.Notify();
		}

		public OperationProgress Progress(string id, long? total = null, OperationProgressUnit unit = OperationProgressUnit.None, long done = 0)
		{
			Contract.NotNullOrEmpty(id);
			var progress = new OperationProgress(id, total, unit, done);
			lock (this.Gate)
			{
				CurrentParent().AttachProgress(progress);
			}
			return progress;
		}

		public OperationOutcome<TResult> Success<TResult>(TResult result) => OperationOutcome<TResult>.Success(result);

		public OperationOutcome Fail(OperationError error)
		{
			this.PendingKind = OperationOutcomeKind.Failed;
			return OperationOutcome.Failed(error);
		}

		public OperationOutcome Suspend(JsonValue continuation, OperationError? error = null)
		{
			this.PendingKind = OperationOutcomeKind.Suspended;
			return OperationOutcome.Suspended(continuation, error);
		}

	}

}
