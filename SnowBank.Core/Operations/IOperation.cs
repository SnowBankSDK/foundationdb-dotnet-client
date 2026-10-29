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
	using System.Threading;
	using System.Threading.Tasks;
	using SnowBank.Data.Json;

	/// <summary>A piece of work that runs on a node, and whose state observers can follow</summary>
	/// <typeparam name="TArgs">Type of the arguments of the operation</typeparam>
	/// <typeparam name="TResult">Type of the result of the operation</typeparam>
	/// <example><code>
	/// public async ValueTask&lt;OperationOutcome&lt;long&gt;&gt; RunAsync(IOperationContext ctx, CopyArgs args, CancellationToken ct)
	/// {
	///     var bytes = ctx.Progress("bytes", total: src.Length, unit: OperationProgressUnit.Bytes);
	///     while ((n = await src.ReadAsync(buffer, ct)) &gt; 0)
	///     {
	///         await dst.WriteAsync(buffer.AsMemory(0, n), ct);
	///         bytes.Add(n);
	///         if (ctx.StopRequested) return ctx.Suspend(JsonObject.Create("offset", bytes.Done));
	///     }
	///     return ctx.Success(bytes.Done);
	/// }
	/// </code></example>
	[PublicAPI]
	public interface IOperation<in TArgs, TResult>
	{
		/// <summary>Executes one run of the operation</summary>
		/// <param name="ctx">Context of the run: steps, progress counters, stop request, continuation of a previous run</param>
		/// <param name="args">Arguments of the operation (the same for every run)</param>
		/// <param name="ct">Token that fires when the operation is cancelled, or when a stop request outlives its grace period</param>
		/// <returns>The outcome of the run, built with <see cref="IOperationContext.Success{TResult}"/>, <see cref="IOperationContext.Fail(OperationError)"/> or <see cref="IOperationContext.Suspend"/></returns>
		ValueTask<OperationOutcome<TResult>> RunAsync(IOperationContext ctx, TArgs args, CancellationToken ct);
	}

	/// <summary>Context of one run of an operation</summary>
	/// <remarks>
	/// <para>Every member is cheap: updating a counter or reading <see cref="StopRequested"/> touches memory only. The host publishes the state of the run apart from the code of the operation, at a rate of its choice.</para>
	/// </remarks>
	[PublicAPI]
	public interface IOperationContext
	{
		/// <summary>Unique id of the operation (32 lowercase hexadecimal digits), the same for every run</summary>
		string Id { get; }

		/// <summary>Type of the operation</summary>
		OperationType Type { get; }

		/// <summary>Number of this run, starting at 1; a resume starts the next run</summary>
		int Run { get; }

		/// <summary>Principal that started the operation, if known</summary>
		OperationActor? StartedBy { get; }

		/// <summary>Continuation left by the previous run, or <c>null</c> for the first run</summary>
		JsonValue? Continuation { get; }

		/// <summary><c>true</c> once a stop was requested: the code should return <see cref="Suspend"/> at its next clean point</summary>
		bool StopRequested { get; }

		/// <summary>Token that fires when a stop is requested, for code that prefers a callback over reading <see cref="StopRequested"/></summary>
		CancellationToken StopToken { get; }

		/// <summary>Declares the steps of the operation up front, so observers can show the ones not started yet</summary>
		/// <param name="steps">Id and label of each step, in order</param>
		/// <remarks>A planned step that never runs ends as <see cref="OperationStepPhase.Skipped"/>.</remarks>
		void PlanSteps(params (string Id, string Label)[] steps);

		/// <summary>Opens a step, nested under the step that is currently open</summary>
		/// <param name="id">Id of the step, unique among its siblings</param>
		/// <param name="label">Label of the step for people, or <c>null</c> to keep the planned label or use the id</param>
		/// <returns>Scope that closes the step when disposed</returns>
		/// <remarks>
		/// <para>Disposing the scope ends the step as <see cref="OperationStepPhase.Cancelled"/> when the token of the run fired, as <see cref="OperationStepPhase.Suspended"/> or <see cref="OperationStepPhase.Failed"/> when the code called <see cref="Suspend"/> or <see cref="Fail"/> since the last step opened, and as <see cref="OperationStepPhase.Succeeded"/> otherwise.</para>
		/// <para>A <c>using</c> block cannot see an exception that leaves it: call <see cref="OperationStepScope.Fail"/> before leaving, or use <see cref="OperationContextExtensions.StepAsync(IOperationContext, string, string?, Func{CancellationToken, ValueTask}, CancellationToken)"/>, which observes the exception.</para>
		/// </remarks>
		OperationStepScope Step(string id, string? label = null);

		/// <summary>Creates a progress counter, attached to the step that is currently open, or to the operation when no step is open</summary>
		/// <param name="id">Id of the counter</param>
		/// <param name="total">Expected final value, if known</param>
		/// <param name="unit">Unit of the counter</param>
		/// <param name="done">Initial value (for example the offset a resumed run starts from)</param>
		OperationProgress Progress(string id, long? total = null, OperationProgressUnit unit = OperationProgressUnit.None, long done = 0);

		/// <summary>Returns a successful outcome</summary>
		OperationOutcome<TResult> Success<TResult>(TResult result);

		/// <summary>Returns a failed outcome</summary>
		OperationOutcome Fail(OperationError error);

		/// <summary>Returns a suspended outcome: a later run can resume from <paramref name="continuation"/></summary>
		/// <param name="continuation">State that the next run reads from <see cref="Continuation"/> (for example <c>{ "offset": 1288490188 }</c>)</param>
		/// <param name="error">Error that interrupted the run (for example a lost connection), or <c>null</c> when the run stopped on request</param>
		OperationOutcome Suspend(JsonValue continuation, OperationError? error = null);
	}

	/// <summary>How a run of an operation ended</summary>
	[PublicAPI]
	public enum OperationOutcomeKind
	{
		/// <summary>The run produced a result</summary>
		Succeeded = 1,
		/// <summary>The run ended with an error</summary>
		Failed,
		/// <summary>The run was cancelled</summary>
		Cancelled,
		/// <summary>The run stopped at a clean point, with a continuation</summary>
		Suspended,
	}

	/// <summary>Outcome of a run that carries no result: a failure, a cancellation or a suspension</summary>
	/// <remarks>Converts implicitly to <see cref="OperationOutcome{TResult}"/>, so an operation can <c>return ctx.Fail(...)</c> from any <c>RunAsync</c>.</remarks>
	[PublicAPI]
	public readonly record struct OperationOutcome
	{
		internal OperationOutcome(OperationOutcomeKind kind, OperationError? error, JsonValue? continuation)
		{
			this.Kind = kind;
			this.Error = error;
			this.Continuation = continuation;
		}

		/// <summary>How the run ended</summary>
		public OperationOutcomeKind Kind { get; }

		/// <summary>Error of a failed run, or the interruption of a suspended run</summary>
		public OperationError? Error { get; }

		/// <summary>Continuation of a suspended run</summary>
		public JsonValue? Continuation { get; }

		/// <summary>Outcome of a failed run</summary>
		public static OperationOutcome Failed(OperationError error)
		{
			Contract.NotNull(error);
			return new(OperationOutcomeKind.Failed, error, null);
		}

		/// <summary>Outcome of a cancelled run</summary>
		public static OperationOutcome Cancelled() => new(OperationOutcomeKind.Cancelled, null, null);

		/// <summary>Outcome of a suspended run</summary>
		public static OperationOutcome Suspended(JsonValue continuation, OperationError? error = null)
		{
			Contract.NotNull(continuation);
			return new(OperationOutcomeKind.Suspended, error, continuation);
		}
	}

	/// <summary>Outcome of a run of an operation</summary>
	/// <typeparam name="TResult">Type of the result</typeparam>
	[PublicAPI]
	public readonly record struct OperationOutcome<TResult>
	{
		internal OperationOutcome(OperationOutcomeKind kind, TResult? result, OperationError? error, JsonValue? continuation)
		{
			this.Kind = kind;
			this.Result = result;
			this.Error = error;
			this.Continuation = continuation;
		}

		/// <summary>How the run ended</summary>
		public OperationOutcomeKind Kind { get; }

		/// <summary>Result of a successful run</summary>
		public TResult? Result { get; }

		/// <summary>Error of a failed run, or the interruption of a suspended run</summary>
		public OperationError? Error { get; }

		/// <summary>Continuation of a suspended run</summary>
		public JsonValue? Continuation { get; }

		/// <summary>Tests whether the run produced a result</summary>
		[MemberNotNullWhen(true, nameof(Result))]
		public bool Succeeded => this.Kind == OperationOutcomeKind.Succeeded;

		/// <summary>Outcome of a successful run</summary>
		public static OperationOutcome<TResult> Success(TResult result) => new(OperationOutcomeKind.Succeeded, result, null, null);

		/// <summary>Converts an outcome that carries no result</summary>
		public static implicit operator OperationOutcome<TResult>(OperationOutcome outcome) => new(outcome.Kind, default, outcome.Error, outcome.Continuation);

		/// <summary>Drops the result, for code that handles every type of operation the same way</summary>
		public OperationOutcome WithoutResult() => new(this.Kind == OperationOutcomeKind.Succeeded ? OperationOutcomeKind.Succeeded : this.Kind, this.Error, this.Continuation);
	}

	/// <summary>Progress counter of an operation or of one of its steps</summary>
	/// <remarks>Updating a counter is an interlocked write to memory. Observers see the new value at the next publication of the state.</remarks>
	[PublicAPI]
	public sealed class OperationProgress
	{
		internal OperationProgress(string id, long? total, OperationProgressUnit unit, long done)
		{
			this.Id = id;
			this.Unit = unit;
			this.DoneValue = done;
			this.TotalValue = total ?? -1;
		}

		/// <summary>Id of the counter</summary>
		public string Id { get; }

		/// <summary>Unit of the counter</summary>
		public OperationProgressUnit Unit { get; }

		private long DoneValue;

		private long TotalValue;

		/// <summary>Current value</summary>
		public long Done => Volatile.Read(ref this.DoneValue);

		/// <summary>Expected final value, or <c>null</c> when unknown</summary>
		public long? Total => Volatile.Read(ref this.TotalValue) is var total and >= 0 ? total : null;

		/// <summary>Adds to the current value</summary>
		public void Add(long amount) => Interlocked.Add(ref this.DoneValue, amount);

		/// <summary>Sets the current value</summary>
		public void Set(long value) => Volatile.Write(ref this.DoneValue, value);

		/// <summary>Sets the expected final value, or <c>null</c> when unknown</summary>
		public void SetTotal(long? total) => Volatile.Write(ref this.TotalValue, total ?? -1);
	}

	/// <summary>Scope of an open step: disposing it closes the step</summary>
	[PublicAPI]
	public readonly struct OperationStepScope : IDisposable
	{
		internal OperationStepScope(OperationRunContext context, OperationStepNode node)
		{
			this.Context = context;
			this.Node = node;
		}

		private OperationRunContext? Context { get; }

		private OperationStepNode? Node { get; }

		/// <summary>Ends the step as failed; the step keeps this outcome when the scope is disposed</summary>
		public void Fail(OperationError error) => End(OperationStepPhase.Failed, error);

		/// <summary>Ends the step with an explicit phase; the step keeps it when the scope is disposed</summary>
		public void End(OperationStepPhase phase, OperationError? error = null) => this.Context?.CloseStep(this.Node!, phase, error);

		/// <summary>Closes the step</summary>
		public void Dispose() => this.Context?.CloseStep(this.Node!, null, null);
	}

	/// <summary>Helpers for writing operations</summary>
	[PublicAPI]
	public static class OperationContextExtensions
	{

		/// <summary>Runs a step: the step ends as failed when <paramref name="body"/> throws, and as cancelled when the token fires</summary>
		public static async ValueTask StepAsync(this IOperationContext ctx, string id, string? label, Func<CancellationToken, ValueTask> body, CancellationToken ct)
		{
			var scope = ctx.Step(id, label);
			try
			{
				await body(ct).ConfigureAwait(false);
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				scope.Fail(new() { Code = "exception", Message = e.Message });
				throw;
			}
			finally
			{
				scope.Dispose();
			}
		}

		/// <summary>Runs a step that returns a value: the step ends as failed when <paramref name="body"/> throws, and as cancelled when the token fires</summary>
		public static async ValueTask<T> StepAsync<T>(this IOperationContext ctx, string id, string? label, Func<CancellationToken, ValueTask<T>> body, CancellationToken ct)
		{
			var scope = ctx.Step(id, label);
			try
			{
				return await body(ct).ConfigureAwait(false);
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				scope.Fail(new() { Code = "exception", Message = e.Message });
				throw;
			}
			finally
			{
				scope.Dispose();
			}
		}

		/// <summary>Runs another operation as a child step of this one, on the same node and in the same run</summary>
		/// <remarks>The child's steps and counters nest under the step <paramref name="id"/>. The outcome of the child sets the phase of that step, and returns to the parent, which decides what to do with it.</remarks>
		public static async ValueTask<OperationOutcome<TResult>> RunChildAsync<TArgs, TResult>(this IOperationContext ctx, string id, string? label, IOperation<TArgs, TResult> child, TArgs args, CancellationToken ct)
		{
			Contract.NotNull(child);
			var scope = ctx.Step(id, label);
			try
			{
				var outcome = await child.RunAsync(ctx, args, ct).ConfigureAwait(false);
				switch (outcome.Kind)
				{
					case OperationOutcomeKind.Failed:
					{
						scope.End(OperationStepPhase.Failed, outcome.Error);
						break;
					}
					case OperationOutcomeKind.Suspended:
					{
						scope.End(OperationStepPhase.Suspended, outcome.Error);
						break;
					}
					case OperationOutcomeKind.Cancelled:
					{
						scope.End(OperationStepPhase.Cancelled);
						break;
					}
				}
				return outcome;
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				scope.Fail(new() { Code = "exception", Message = e.Message });
				throw;
			}
			finally
			{
				scope.Dispose();
			}
		}

	}

}
