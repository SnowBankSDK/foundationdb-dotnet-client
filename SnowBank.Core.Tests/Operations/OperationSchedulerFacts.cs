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

namespace SnowBank.Operations.Tests
{
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Time.Testing;
	using SnowBank.Data.Json;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class OperationSchedulerFacts : SimpleTest
	{

		private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

		/// <summary>Copies a stream, in chunks of 64 KiB, and stops at a chunk boundary on request</summary>
		private sealed class CopyOperation : IOperation<(Stream Source, Stream Target), long>
		{
			public static readonly OperationType Type = new("files/copy");

			public async ValueTask<OperationOutcome<long>> RunAsync(IOperationContext ctx, (Stream Source, Stream Target) args, CancellationToken ct)
			{
				var offset = ctx.Continuation?.Get<long>("offset", 0) ?? 0;
				args.Source.Position = offset;
				args.Target.Position = offset;

				using (ctx.Step("copy", "Copy the file"))
				{
					var bytes = ctx.Progress("bytes", total: args.Source.Length, unit: OperationProgressUnit.Bytes, done: offset);
					var buffer = new byte[64 * 1024];
					int n;
					while ((n = await args.Source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
					{
						await args.Target.WriteAsync(buffer, 0, n, ct);
						bytes.Add(n);
						if (ctx.StopRequested)
						{
							return ctx.Suspend(JsonObject.Create("offset", bytes.Done));
						}
					}
				}
				return ctx.Success(args.Target.Length);
			}
		}

		/// <summary>Counts to a total, one item per release of <see cref="Tick"/>, and stops between two items on request</summary>
		private sealed class TickOperation : IOperation<int, long>
		{
			public static readonly OperationType Type = new("test/tick");

			public SemaphoreSlim Tick { get; } = new(0);

			public bool IgnoreStop { get; init; }

			public async ValueTask<OperationOutcome<long>> RunAsync(IOperationContext ctx, int total, CancellationToken ct)
			{
				var start = ctx.Continuation?.Get<long>("offset", 0) ?? 0;
				ctx.PlanSteps(("loop", "Count"), ("finish", "Finish"));
				using (ctx.Step("loop"))
				{
					var items = ctx.Progress("items", total, OperationProgressUnit.Items, start);
					for (var i = start; i < total; i++)
					{
						await this.Tick.WaitAsync(ct);
						items.Add(1);
						if (!this.IgnoreStop && ctx.StopRequested)
						{
							return ctx.Suspend(JsonObject.Create("offset", items.Done));
						}
					}
				}
				using (ctx.Step("finish")) { }
				return ctx.Success((long) total);
			}
		}

		private sealed class RecordingPublisher : IOperationPublisher
		{
			public int Changes;

			public List<string> Removed { get; } = [ ];

			public void OnChanged(IOperationState operation) => Interlocked.Increment(ref this.Changes);

			public void OnRemoved(IOperationState operation)
			{
				lock (this.Removed) this.Removed.Add(operation.Id);
			}
		}

		private static long ItemsDone(OperationSnapshot snapshot) => snapshot.Steps.FirstOrDefault(s => s.Id == "loop")?.Progress.FirstOrDefault()?.Done ?? -1;

		[Test]
		public async Task Test_Copy_Runs_To_Completion_With_Its_Step_And_Counter()
		{
			var data = new byte[1_000_000];
			new Random(42).NextBytes(data);
			var target = new MemoryStream();

			var scheduler = new OperationScheduler(TimeProvider.System);
			scheduler.Register(CopyOperation.Type, new CopyOperation());

			var handle = scheduler.Start<(Stream, Stream), long>(CopyOperation.Type, (new MemoryStream(data), target), new() { StartedBy = OperationActor.User("user:1") });
			Assert.That(handle.Id, Has.Length.EqualTo(32));

			var outcome = await handle.Completion.WaitAsync(Timeout);
			Assert.That(outcome.Kind, Is.EqualTo(OperationOutcomeKind.Succeeded));
			Assert.That(outcome.Result, Is.EqualTo(data.Length));
			Assert.That(target.ToArray(), Is.EqualTo(data));

			var snapshot = handle.State.GetSnapshot();
			Log(snapshot);
			Assert.That(snapshot.Phase, Is.EqualTo(OperationPhase.Succeeded));
			Assert.That(snapshot.Run, Is.EqualTo(1));
			Assert.That(snapshot.StartedBy?.Id, Is.EqualTo("user:1"));
			var step = snapshot.Steps.Single();
			Assert.That(step.Id, Is.EqualTo("copy"));
			Assert.That(step.Label, Is.EqualTo("Copy the file"));
			Assert.That(step.Phase, Is.EqualTo(OperationStepPhase.Succeeded));
			Assert.That(step.Progress.Single().Done, Is.EqualTo(data.Length));
			Assert.That(step.Progress.Single().Total, Is.EqualTo(data.Length));
			Assert.That(step.Progress.Single().Unit, Is.EqualTo(OperationProgressUnit.Bytes));
		}

		[Test]
		public async Task Test_Stop_Suspends_At_A_Clean_Point_And_Resume_Continues_From_The_Offset()
		{
			var op = new TickOperation();
			var scheduler = new OperationScheduler(TimeProvider.System);
			scheduler.Register(TickOperation.Type, op, new() { Stoppable = true });

			var handle = scheduler.Start<int, long>(TickOperation.Type, 10);
			op.Tick.Release(3);
			await WaitUntil(() => ItemsDone(handle.State.GetSnapshot()) == 3, Timeout, "first items");
			Assert.That(handle.State.GetSnapshot().CanStop, Is.True);

			Assert.That(scheduler.Stop(handle.Id, null), Is.EqualTo(OperationControlStatus.Ok));
			Assert.That(handle.State.GetSnapshot().Phase, Is.EqualTo(OperationPhase.Stopping));

			// the run sees the stop after its next item, and returns a continuation
			op.Tick.Release(1);
			await WaitUntil(() => handle.State.GetSnapshot().Phase == OperationPhase.Suspended, Timeout, "suspended");
			var suspended = handle.State.GetSnapshot();
			Log(suspended);
			Assert.That(suspended.CanResume, Is.True);
			Assert.That(suspended.CanStop, Is.False);
			Assert.That(ItemsDone(suspended), Is.EqualTo(4));
			Assert.That(suspended.Steps.Single(s => s.Id == "loop").Phase, Is.EqualTo(OperationStepPhase.Suspended));
			Assert.That(suspended.Steps.Single(s => s.Id == "finish").Phase, Is.EqualTo(OperationStepPhase.Planned));
			Assert.That(handle.Completion.IsCompleted, Is.False, "a suspension is not an end");

			// a second run starts under the same id, from the offset
			Assert.That(scheduler.Resume(handle.Id, null), Is.EqualTo(OperationControlStatus.Ok));
			op.Tick.Release(6);
			var outcome = await handle.Completion.WaitAsync(Timeout);
			Assert.That(outcome.Kind, Is.EqualTo(OperationOutcomeKind.Succeeded));
			Assert.That(outcome.Result, Is.EqualTo(10));

			var ended = handle.State.GetSnapshot();
			Log(ended);
			Assert.That(ended.Run, Is.EqualTo(2));
			Assert.That(ItemsDone(ended), Is.EqualTo(10));
			Assert.That(ended.Steps.Select(s => s.Phase), Is.EqualTo(new[] { OperationStepPhase.Succeeded, OperationStepPhase.Succeeded }));
		}

		[Test]
		public async Task Test_Stop_On_A_Type_That_Is_Not_Stoppable_Cancels_It()
		{
			var op = new TickOperation();
			var scheduler = new OperationScheduler(TimeProvider.System);
			scheduler.Register(TickOperation.Type, op);

			var handle = scheduler.Start<int, long>(TickOperation.Type, 10);
			op.Tick.Release(2);
			await WaitUntil(() => ItemsDone(handle.State.GetSnapshot()) == 2, Timeout, "first items");
			Assert.That(handle.State.GetSnapshot().CanStop, Is.False);

			Assert.That(scheduler.Stop(handle.Id, null), Is.EqualTo(OperationControlStatus.Ok));
			var outcome = await handle.Completion.WaitAsync(Timeout);
			Assert.That(outcome.Kind, Is.EqualTo(OperationOutcomeKind.Cancelled));
			var snapshot = handle.State.GetSnapshot();
			Assert.That(snapshot.Phase, Is.EqualTo(OperationPhase.Cancelled));
			Assert.That(snapshot.Steps.Select(s => s.Phase), Is.EqualTo(new[] { OperationStepPhase.Cancelled, OperationStepPhase.Skipped }));
		}

		[Test]
		public async Task Test_Stop_Past_Its_Grace_Period_Cancels_The_Run()
		{
			var time = new FakeTimeProvider();
			var op = new TickOperation() { IgnoreStop = true };
			var scheduler = new OperationScheduler(time);
			scheduler.Register(TickOperation.Type, op, new() { Stoppable = true, StopGrace = TimeSpan.FromSeconds(30) });

			var handle = scheduler.Start<int, long>(TickOperation.Type, 10);
			op.Tick.Release(1);
			await WaitUntil(() => ItemsDone(handle.State.GetSnapshot()) == 1, Timeout, "first item");

			Assert.That(scheduler.Stop(handle.Id, null), Is.EqualTo(OperationControlStatus.Ok));
			time.Advance(TimeSpan.FromSeconds(29));
			await Task.Delay(50);
			Assert.That(handle.Completion.IsCompleted, Is.False, "the grace period is not over");

			time.Advance(TimeSpan.FromSeconds(2));
			var outcome = await handle.Completion.WaitAsync(Timeout);
			Assert.That(outcome.Kind, Is.EqualTo(OperationOutcomeKind.Cancelled));
			Assert.That(handle.State.GetSnapshot().Phase, Is.EqualTo(OperationPhase.Cancelled));
		}

		[Test]
		public async Task Test_Queued_Runs_Start_In_Order_And_A_Stop_Removes_One_From_The_Queue()
		{
			var op = new TickOperation();
			var scheduler = new OperationScheduler(TimeProvider.System);
			scheduler.Register(TickOperation.Type, op, new() { MaxConcurrency = 1 });

			var a = scheduler.Start<int, long>(TickOperation.Type, 1);
			var b = scheduler.Start<int, long>(TickOperation.Type, 1);
			var c = scheduler.Start<int, long>(TickOperation.Type, 1);
			Assert.That(a.State.GetSnapshot().Phase, Is.EqualTo(OperationPhase.Running));
			Assert.That(b.State.GetSnapshot().Phase, Is.EqualTo(OperationPhase.Queued));
			Assert.That(c.State.GetSnapshot().Phase, Is.EqualTo(OperationPhase.Queued));

			Assert.That(scheduler.Stop(c.Id, null), Is.EqualTo(OperationControlStatus.Ok));
			Assert.That((await c.Completion.WaitAsync(Timeout)).Kind, Is.EqualTo(OperationOutcomeKind.Cancelled));
			Assert.That(c.State.GetSnapshot().Run, Is.EqualTo(0), "a cancelled queued operation never ran");

			op.Tick.Release(1);
			Assert.That((await a.Completion.WaitAsync(Timeout)).Kind, Is.EqualTo(OperationOutcomeKind.Succeeded));
			await WaitUntil(() => b.State.GetSnapshot().Phase == OperationPhase.Running, Timeout, "b starts after a");
			op.Tick.Release(1);
			Assert.That((await b.Completion.WaitAsync(Timeout)).Kind, Is.EqualTo(OperationOutcomeKind.Succeeded));
		}

		[Test]
		public async Task Test_Resume_Only_Applies_To_A_Suspended_Operation()
		{
			var time = new FakeTimeProvider();
			var op = new TickOperation();
			var scheduler = new OperationScheduler(time);
			scheduler.Register(TickOperation.Type, op, new() { Stoppable = true, SuspendedRetention = TimeSpan.FromHours(1) });

			var handle = scheduler.Start<int, long>(TickOperation.Type, 10);
			Assert.That(scheduler.Resume(handle.Id, null), Is.EqualTo(OperationControlStatus.NotSuspended), "running");

			scheduler.Stop(handle.Id, null);
			Assert.That(scheduler.Resume(handle.Id, null), Is.EqualTo(OperationControlStatus.NotSuspended), "stopping: there is never more than one live run");
			op.Tick.Release(1);
			await WaitUntil(() => handle.State.GetSnapshot().Phase == OperationPhase.Suspended, Timeout, "suspended");

			// past its retention, the suspension is abandoned, and nothing can resume it
			time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
			var outcome = await handle.Completion.WaitAsync(Timeout);
			Assert.That(outcome.Kind, Is.EqualTo(OperationOutcomeKind.Cancelled));
			var snapshot = handle.State.GetSnapshot();
			Assert.That(snapshot.Phase, Is.EqualTo(OperationPhase.Abandoned));
			Assert.That(snapshot.CanResume, Is.False);
			Assert.That(snapshot.Steps.Select(s => s.Phase), Is.EqualTo(new[] { OperationStepPhase.Cancelled, OperationStepPhase.Skipped }));
			Assert.That(scheduler.Resume(handle.Id, null), Is.EqualTo(OperationControlStatus.Ended));
		}

		[Test]
		public async Task Test_Control_Requires_A_Grant()
		{
			var op = new TickOperation();
			var scheduler = new OperationScheduler(TimeProvider.System);
			scheduler.Register(TickOperation.Type, op, new()
			{
				Stoppable = true,
				Access = starter => new() { Observe = [ OperationGrant.Role("support") ], Control = [ OperationGrant.User(starter!.Id), OperationGrant.Role("admin") ] },
			});

			var handle = scheduler.Start<int, long>(TickOperation.Type, 10, new() { StartedBy = OperationActor.User("user:alice") });
			var access = handle.State.GetSnapshot().Access;
			Assert.That(access.CanObserve(new("user:bob", [ "support" ])), Is.True);
			Assert.That(access.CanControl(new("user:bob", [ "support" ])), Is.False);

			Assert.That(scheduler.Cancel(handle.Id, OperationActor.User("user:bob")), Is.EqualTo(OperationControlStatus.Denied));
			Assert.That(scheduler.Stop(handle.Id, new("user:bob", [ "support" ])), Is.EqualTo(OperationControlStatus.Denied));
			Assert.That(scheduler.Stop(handle.Id, new("user:carol", [ "admin" ])), Is.EqualTo(OperationControlStatus.Ok));
			Assert.That(scheduler.Stop("0123456789abcdef0123456789abcdef", null), Is.EqualTo(OperationControlStatus.NotFound));

			Assert.That(scheduler.Cancel(handle.Id, OperationActor.User("user:alice")), Is.EqualTo(OperationControlStatus.Ok));
			Assert.That((await handle.Completion.WaitAsync(Timeout)).Kind, Is.EqualTo(OperationOutcomeKind.Cancelled));
		}

		private sealed class FailingOperation : IOperation<int, int>
		{
			public static readonly OperationType Type = new("test/fail");

			public async ValueTask<OperationOutcome<int>> RunAsync(IOperationContext ctx, int args, CancellationToken ct)
			{
				ctx.PlanSteps(("prepare", "Prepare"), ("explode", "Explode"), ("never", "Never"));
				await ctx.StepAsync("prepare", null, _ => default, ct);
				await ctx.StepAsync("explode", null, _ => throw new InvalidOperationException("boom"), ct);
				return ctx.Success(1);
			}
		}

		[Test]
		public async Task Test_An_Exception_Fails_The_Operation_And_Its_Step()
		{
			var scheduler = new OperationScheduler(TimeProvider.System);
			scheduler.Register(FailingOperation.Type, new FailingOperation());

			var handle = scheduler.Start<int, int>(FailingOperation.Type, 0);
			var outcome = await handle.Completion.WaitAsync(Timeout);
			Assert.That(outcome.Kind, Is.EqualTo(OperationOutcomeKind.Failed));
			Assert.That(outcome.Error?.Code, Is.EqualTo("exception"));

			var snapshot = handle.State.GetSnapshot();
			Log(snapshot);
			Assert.That(snapshot.Phase, Is.EqualTo(OperationPhase.Failed));
			Assert.That(snapshot.Steps.Select(s => s.Phase), Is.EqualTo(new[] { OperationStepPhase.Succeeded, OperationStepPhase.Failed, OperationStepPhase.Skipped }));
			Assert.That(snapshot.Steps[1].Error?.Message, Is.EqualTo("boom"));
		}

		[Test]
		public async Task Test_An_Ended_Operation_Is_Forgotten_After_Its_Retention()
		{
			var time = new FakeTimeProvider();
			var publisher = new RecordingPublisher();
			var op = new TickOperation();
			var scheduler = new OperationScheduler(time, publisher);
			scheduler.Register(TickOperation.Type, op, new() { EndedRetention = TimeSpan.FromMinutes(5) });

			var handle = scheduler.Start<int, long>(TickOperation.Type, 1);
			op.Tick.Release(1);
			await handle.Completion.WaitAsync(Timeout);
			Assert.That(publisher.Changes, Is.GreaterThan(0));

			time.Advance(TimeSpan.FromMinutes(4));
			Assert.That(scheduler.TryGet(handle.Id), Is.Not.Null);
			time.Advance(TimeSpan.FromMinutes(2));
			Assert.That(scheduler.TryGet(handle.Id), Is.Null);
			Assert.That(publisher.Removed, Is.EqualTo(new[] { handle.Id }));
		}

	}

}
