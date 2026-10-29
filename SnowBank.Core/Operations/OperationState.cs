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

	/// <summary>State of an operation at one point in time, as observers see it</summary>
	/// <remarks>Every time value is a duration measured on the clock of the node that runs the operation.</remarks>
	[PublicAPI]
	public sealed record OperationSnapshot
	{
		/// <summary>Id of the operation</summary>
		public required string Id { get; init; }

		/// <summary>Type of the operation</summary>
		public required OperationType Type { get; init; }

		/// <summary>Phase of the operation</summary>
		public required OperationPhase Phase { get; init; }

		/// <summary>Number of the current or last run (0 while the first run is queued)</summary>
		public required int Run { get; init; }

		/// <summary>Number that grows with every change of phase or step, for observers that compare two snapshots</summary>
		public required long Version { get; init; }

		/// <summary>Principal that started the operation, if known</summary>
		public OperationActor? StartedBy { get; init; }

		/// <summary>Who may observe and control the operation</summary>
		public required OperationAccess Access { get; init; }

		/// <summary>Time since the start of the current run, or the duration of the last run</summary>
		public TimeSpan Elapsed { get; init; }

		/// <summary>Counters attached to the operation itself</summary>
		public IReadOnlyList<OperationProgressSnapshot> Progress { get; init; } = [ ];

		/// <summary>Steps of the operation, in the order they were planned or opened</summary>
		public IReadOnlyList<OperationStepSnapshot> Steps { get; init; } = [ ];

		/// <summary>Error of a failed operation, or the interruption of a suspended one</summary>
		public OperationError? Error { get; init; }

		/// <summary><c>true</c> when a stop request would end the run at a clean point</summary>
		public bool CanStop { get; init; }

		/// <summary><c>true</c> when a resume would start a new run from the continuation</summary>
		public bool CanResume { get; init; }

		/// <summary>Tests whether the operation reached a phase it never leaves</summary>
		public bool IsEnded => this.Phase is OperationPhase.Succeeded or OperationPhase.Failed or OperationPhase.Cancelled or OperationPhase.Abandoned;
	}

	/// <summary>State of one step of an operation</summary>
	[PublicAPI]
	public sealed record OperationStepSnapshot
	{
		/// <summary>Id of the step</summary>
		public required string Id { get; init; }

		/// <summary>Label of the step for people</summary>
		public required string Label { get; init; }

		/// <summary>Phase of the step</summary>
		public required OperationStepPhase Phase { get; init; }

		/// <summary>Time since the step started, or its duration once ended; <c>null</c> for a step that never started</summary>
		public TimeSpan? Elapsed { get; init; }

		/// <summary>Error of a failed step</summary>
		public OperationError? Error { get; init; }

		/// <summary>Counters attached to the step</summary>
		public IReadOnlyList<OperationProgressSnapshot> Progress { get; init; } = [ ];

		/// <summary>Steps nested in this one</summary>
		public IReadOnlyList<OperationStepSnapshot> Steps { get; init; } = [ ];
	}

	/// <summary>Value of a progress counter</summary>
	[PublicAPI]
	public sealed record OperationProgressSnapshot
	{
		/// <summary>Id of the counter</summary>
		public required string Id { get; init; }

		/// <summary>Unit of the counter</summary>
		public required OperationProgressUnit Unit { get; init; }

		/// <summary>Current value</summary>
		public required long Done { get; init; }

		/// <summary>Expected final value, if known</summary>
		public long? Total { get; init; }

		/// <summary>Time since the value last changed, as seen by successive snapshots</summary>
		public TimeSpan Idle { get; init; }
	}

	/// <summary>Live view of an operation known to a scheduler</summary>
	[PublicAPI]
	public interface IOperationState
	{
		/// <summary>Id of the operation</summary>
		string Id { get; }

		/// <summary>Type of the operation</summary>
		OperationType Type { get; }

		/// <summary>Captures the current state of the operation</summary>
		/// <remarks>The cost is one lock and a copy of the step tree. A publisher calls it at its own rate.</remarks>
		OperationSnapshot GetSnapshot();
	}

	/// <summary>Makes the state of operations visible outside the scheduler (a Teleport document, a local UI, a test probe)</summary>
	/// <remarks>
	/// <para>The scheduler notifies the publisher of urgent changes only: a phase change of the operation or of a step, a new step, the end.
	/// Progress counters change without any notification: a publisher that shows progress samples <see cref="IOperationState.GetSnapshot"/> on a timer.</para>
	/// <para>Notifications run on the thread that made the change: a publisher must return quickly and never block.</para>
	/// </remarks>
	[PublicAPI]
	public interface IOperationPublisher
	{
		/// <summary>An operation changed in a way that observers should see at once</summary>
		void OnChanged(IOperationState operation);

		/// <summary>An operation reached the end of its retention and is no longer known to the scheduler</summary>
		void OnRemoved(IOperationState operation);
	}

	/// <summary>Publisher that discards every notification</summary>
	[PublicAPI]
	public sealed class NoOpOperationPublisher : IOperationPublisher
	{
		/// <summary>Shared instance</summary>
		public static readonly NoOpOperationPublisher Instance = new();

		/// <inheritdoc />
		public void OnChanged(IOperationState operation) { }

		/// <inheritdoc />
		public void OnRemoved(IOperationState operation) { }
	}

}
