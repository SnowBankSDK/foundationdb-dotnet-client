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
	using System.Linq;
	using SnowBank.Data.Json;

	/// <summary>Identifies a kind of operation, for example <c>files/copy</c> version 1</summary>
	/// <param name="Name">Name of the operation type, unique on a node</param>
	/// <param name="Version">Version of the type; a continuation is only valid for the version that wrote it</param>
	[PublicAPI]
	public sealed record OperationType(string Name, int Version = 1)
	{
		/// <inheritdoc />
		public override string ToString() => this.Name + "@" + this.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>Phase of an operation, as observers see it</summary>
	[PublicAPI]
	public enum OperationPhase
	{
		/// <summary>The operation waits for a free slot of its type</summary>
		Queued,
		/// <summary>A run of the operation executes</summary>
		Running,
		/// <summary>A stop was requested, and the run has not returned yet</summary>
		Stopping,
		/// <summary>The operation ended with a result</summary>
		Succeeded,
		/// <summary>The operation ended with an error</summary>
		Failed,
		/// <summary>The operation was cancelled</summary>
		Cancelled,
		/// <summary>The last run ended at a clean point, with a continuation that a new run can resume from</summary>
		Suspended,
		/// <summary>The operation stayed suspended past its retention, and its continuation was dropped</summary>
		Abandoned,
	}

	/// <summary>Phase of one step of an operation</summary>
	[PublicAPI]
	public enum OperationStepPhase
	{
		/// <summary>The step was declared up front and has not started</summary>
		Planned,
		/// <summary>The step executes</summary>
		Running,
		/// <summary>The step completed</summary>
		Succeeded,
		/// <summary>The step ended with an error</summary>
		Failed,
		/// <summary>The step was interrupted by a cancellation</summary>
		Cancelled,
		/// <summary>The step was interrupted by a stop or a resumable error; a later run continues it</summary>
		Suspended,
		/// <summary>The step was planned, and the operation ended without running it</summary>
		Skipped,
	}

	/// <summary>Unit of a progress counter, used by observers to format the values</summary>
	[PublicAPI]
	public enum OperationProgressUnit
	{
		/// <summary>A plain number</summary>
		None,
		/// <summary>A count of items (files, records, chunks)</summary>
		Items,
		/// <summary>A count of bytes</summary>
		Bytes,
	}

	/// <summary>Error that ended an operation, or a step of it</summary>
	/// <remarks>The exception that caused it, if any, stays in the logs of the node: it is never part of the observable state.</remarks>
	[PublicAPI]
	public sealed record OperationError
	{
		/// <summary>Code of the error condition, for programs (for example <c>source_not_found</c>)</summary>
		public required string Code { get; init; }

		/// <summary>Description of the error condition, for people</summary>
		public required string Message { get; init; }

		/// <summary>Additional details, if any</summary>
		public JsonValue? Details { get; init; }
	}

	/// <summary>Identity of the principal that starts or controls an operation</summary>
	/// <param name="Id">Unique identifier of the principal (for example <c>user:4512</c>)</param>
	/// <param name="Roles">Roles of the principal</param>
	/// <remarks>The host maps its own principal type to this value. The model has no dependency on any authentication stack.</remarks>
	[PublicAPI]
	public sealed record OperationActor(string Id, IReadOnlyList<string> Roles)
	{
		/// <summary>Creates an actor with no role</summary>
		public static OperationActor User(string id) => new(id, [ ]);
	}

	/// <summary>Grants access to an operation to a single principal or to every principal of a role</summary>
	[PublicAPI]
	public sealed record OperationGrant
	{
		private OperationGrant(bool isRole, string value)
		{
			this.IsRole = isRole;
			this.Value = value;
		}

		/// <summary><c>true</c> when <see cref="Value"/> is a role, <c>false</c> when it is a principal id</summary>
		public bool IsRole { get; }

		/// <summary>The role or the principal id</summary>
		public string Value { get; }

		/// <summary>Grants access to the principal with this id</summary>
		public static OperationGrant User(string id)
		{
			Contract.NotNullOrEmpty(id);
			return new(false, id);
		}

		/// <summary>Grants access to every principal with this role</summary>
		public static OperationGrant Role(string role)
		{
			Contract.NotNullOrEmpty(role);
			return new(true, role);
		}

		/// <summary>Tests whether the grant covers an actor</summary>
		public bool Covers(OperationActor actor) => this.IsRole ? actor.Roles.Contains(this.Value) : actor.Id == this.Value;

		/// <inheritdoc />
		public override string ToString() => (this.IsRole ? "role:" : "user:") + this.Value;
	}

	/// <summary>Who may observe an operation, and who may control it (stop, cancel, resume)</summary>
	/// <remarks>The application decides it once, when the operation starts. Recording the decision as data lets every node on the path between the operation and an observer check it.</remarks>
	[PublicAPI]
	public sealed record OperationAccess
	{
		/// <summary>Grants that allow reading the state of the operation</summary>
		public IReadOnlyList<OperationGrant> Observe { get; init; } = [ ];

		/// <summary>Grants that allow stopping, cancelling or resuming the operation</summary>
		public IReadOnlyList<OperationGrant> Control { get; init; } = [ ];

		/// <summary>Access for the principal that started the operation, and nobody else</summary>
		public static OperationAccess StarterOnly(OperationActor? starter) => starter is null
			? new()
			: new() { Observe = [ OperationGrant.User(starter.Id) ], Control = [ OperationGrant.User(starter.Id) ] };

		/// <summary>Tests whether an actor may observe the operation</summary>
		public bool CanObserve(OperationActor actor) => Covers(this.Observe, actor) || Covers(this.Control, actor);

		/// <summary>Tests whether an actor may control the operation</summary>
		public bool CanControl(OperationActor actor) => Covers(this.Control, actor);

		private static bool Covers(IReadOnlyList<OperationGrant> grants, OperationActor actor)
		{
			foreach (var grant in grants)
			{
				if (grant.Covers(actor)) return true;
			}
			return false;
		}
	}

	/// <summary>Result of a request to stop, cancel or resume an operation</summary>
	[PublicAPI]
	public enum OperationControlStatus
	{
		/// <summary>The request was applied, or it was already in effect</summary>
		Ok,
		/// <summary>No operation with this id exists on this node</summary>
		NotFound,
		/// <summary>The actor may not control this operation</summary>
		Denied,
		/// <summary>A resume was requested on an operation that is not suspended</summary>
		NotSuspended,
		/// <summary>The operation already ended</summary>
		Ended,
	}

}
