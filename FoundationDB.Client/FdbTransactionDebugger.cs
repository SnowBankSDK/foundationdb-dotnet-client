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

namespace FoundationDB.Client
{
	using System.ComponentModel;
	using FoundationDB.Client.Core;

	/// <summary>Exposes the internal lifecycle state of transactions, for test harnesses and diagnostics.</summary>
	/// <remarks>
	/// <para>This is the equivalent, for the real client, of <c>FakeDbDebugger</c> in the FakeDb emulator: a small proxy that lets test code observe internal state without requiring an <c>InternalsVisibleTo</c> grant.</para>
	/// <para>Application logic should never take decisions based on these observations: they are inherently racy, and only meaningful from the code path that owns the transaction.</para>
	/// <para>The transaction must be a concrete <see cref="FdbTransaction"/> (not a filter or logging wrapper), otherwise an <see cref="InvalidCastException"/> is thrown.</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class FdbTransactionDebugger
	{

		/// <summary>Lifecycle states of a transaction, as observed by <see cref="GetState"/>.</summary>
		/// <remarks>The numeric values must match the <c>FdbTransaction.STATE_*</c> internal constants.</remarks>
		public enum TransactionState
		{
			/// <summary>The transaction has been disposed.</summary>
			Disposed = -1,
			/// <summary>The transaction is being initialized.</summary>
			Init = 0,
			/// <summary>The transaction is ready to process operations.</summary>
			Ready = 1,
			/// <summary>The transaction has been successfully committed.</summary>
			Committed = 2,
			/// <summary>The transaction has been canceled.</summary>
			Canceled = 3,
			/// <summary>The transaction has failed.</summary>
			Failed = 4,
		}

		/// <summary>Returns the current lifecycle state of this transaction.</summary>
		public static TransactionState GetState(IFdbReadOnlyTransaction trans) => (TransactionState) ((FdbTransaction) trans).State;

		/// <summary>Returns <see langword="true"/> if this transaction is still in a state where operations can be performed (i.e. <see cref="TransactionState.Ready"/>).</summary>
		public static bool IsStillAlive(IFdbReadOnlyTransaction trans) => ((FdbTransaction) trans).StillAlive;

		/// <summary>Returns the low-level handler that implements this transaction (native client, FakeDb store, ...).</summary>
		public static IFdbTransactionHandler GetHandler(IFdbReadOnlyTransaction trans) => ((FdbTransaction) trans).Handler;

	}

}
