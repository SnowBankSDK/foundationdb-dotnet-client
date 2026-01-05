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

	/// <summary>Transaction that allows read and write operations</summary>
	[PublicAPI]
	public interface IFdbTransaction : IFdbReadOnlyTransaction
	{
		/// <summary>Returns <see langword="true"/> if this transaction instance only allows read operations</summary>
		/// <remarks>Attempting to call a write method on a read-only transaction will immediately throw an exception</remarks>
		bool IsReadOnly { get; }

		/// <summary>Ensures that the transaction is in a valid state for issuing write operations.</summary>
		/// <exception cref="System.ObjectDisposedException">If <see cref="IDisposable.Dispose">Dispose()</see> has already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If the transaction as already been committed, or if the database connection has been closed</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token has been cancelled</exception>
		void EnsureCanWrite();

		/// <summary>Estimated payload size of the transaction (in bytes)</summary>
		/// <remarks>This is not guaranteed to be accurate, and should only be used as a hint.</remarks>
		long Size { get; }

		/// <summary>Returns the number of keys changed by this transaction, as well as the estimated payload size</summary>
		/// <remarks>The counters are reset everytime the transaction is recycled (either via <see cref="IFdbReadOnlyTransaction.Reset"/> on <see cref="IFdbReadOnlyTransaction.OnErrorAsync"/>)</remarks>
		(int Keys, long Size) GetWriteStatistics();

		/// <summary>Sets the value of a key in the database.</summary>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <remarks>
		/// <para>Modifies the database snapshot represented by transaction to change the given key to have the given value.</para>
		/// <para>If the given key was not previously present in the database it is inserted.</para>
		/// <para>The modification affects the actual database only if transaction is later committed with CommitAsync().</para>
		/// </remarks>
		void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

		/// <summary>Performs an atomic operation that will mutate a key in the database</summary>
		/// <param name="key">Name of the key to be mutated in the database.</param>
		/// <param name="param">Parameter with which the atomic operation will mutate the value associated with <paramref name="key"/>.</param>
		/// <param name="mutation">Type of mutation that should be performed on the key</param>
		/// <remarks>
		/// <para>Modifies the database snapshot represented by this transaction to perform the operation indicated by <paramref name="mutation"/> with operand <paramref name="param"/> to the value stored by the given key.</para>
		/// </remarks>
		void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType mutation);

		/// <summary>Removes a key from the database.</summary>
		/// <param name="key">Name of the key to be removed from the database.</param>
		/// <remarks>
		/// <para>Modifies the database snapshot represented by this transaction to remove the given key from the database.</para>
		/// <para>If the key was not previously present in the database, there is no effect.</para>
		/// </remarks>
		void Clear(ReadOnlySpan<byte> key);

		/// <summary>Removes a range of keys from the database.</summary>
		/// <param name="beginKeyInclusive">Name of the key specifying the beginning of the range to clear.</param>
		/// <param name="endKeyExclusive">Name of the key specifying the end of the range to clear.</param>
		/// <remarks>
		/// <para>Modifies the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.</para>
		/// <para>Sets and clears affect the actual database only if transaction is later committed with CommitAsync().</para>
		/// </remarks>
		void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive);

		/// <summary>Adds a conflict range to a transaction without performing the associated read or write.</summary>
		/// <param name="beginKeyInclusive">Key specifying the beginning of the conflict range. The key is included</param>
		/// <param name="endKeyExclusive">Key specifying the end of the conflict range. The key is excluded</param>
		/// <param name="type">One of the <see cref="FdbConflictRangeType"/> values indicating what type of conflict range is being set.</param>
		void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type);

		/// <summary>Commits any changes performed by this transaction to the database.</summary>
		/// <returns>Task that succeeds if the transaction was committed successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>
		/// <para>Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database.</para>
		/// <para>The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation.</para>
		/// <para>If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.</para>
		/// <para>
		/// As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded.
		/// In these cases, <see cref="CommitAsync"/> will throw <see cref="FdbError.CommitUnknownResult"/> error.
		/// The <see cref="IFdbReadOnlyTransaction.OnErrorAsync"/> function treats this error as retryable,
		/// so retry loops that don't check for <see cref="FdbError.CommitUnknownResult"/> could execute the transaction twice.
		/// In these cases, you must consider the idempotence of the transaction.
		/// </para>
		/// </remarks>
		Task CommitAsync();

		/// <summary>Retrieves the database version number at which a given transaction was committed.</summary>
		/// <returns>Version of the database after a successful commit, or <see langword="-1"/> if no commit has been performed yet, or if it was unsuccessful.</returns>
		/// <remarks>
		/// <para><see cref="CommitAsync"/> must have been called on this transaction and the resulting task must have completed successfully before this function is called, or the behavior is undefined.</para>
		/// <para>Read-only transactions do not modify the database when committed and will have a committed version of <see langword="-1"/>.</para>
		/// <para>Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.</para>
		/// </remarks>
		[Pure]
		long GetCommittedVersion();

		/// <summary>Bumps the value of a metadata key of the database snapshot represented by the current transaction.</summary>
		/// <param name="key">Key to mutate. If <see cref="Slice.Nil"/>, mutate the global <c>\xff/metadataVersion</c> key</param>
		/// <remarks>
		/// <para>The value of the key will be updated to a value higher than any previous value, once the transaction commits.</para>
		/// <para>Until this happens, any additional call to <see cref="IFdbReadOnlyTransaction.GetMetadataVersionKeyAsync"/> will return <c>null</c>.</para>
		/// <para>If the value of the <paramref name="key"/> is read via a regular <see cref="IFdbReadOnlyTransaction.GetAsync"/> or <see cref="IFdbReadOnlyTransaction.GetRange"/> call, the transaction will fail to commit!</para>
		/// <para>This method requires API version <b>610</b> or greater.</para>
		/// </remarks>
		void TouchMetadataVersionKey(Slice key = default);

		//TODO: better message!
		/// <summary>Returns the approximate size of the mutation list that this transaction will send to the server.</summary>
		Task<long> GetApproximateSizeAsync();

		/// <summary>Returns the <see cref="VersionStamp"/> which was used by VersionStamped operations in this transaction.</summary>
		/// <remarks>
		/// <para>The Task will be ready only after the successful completion of a call to <see cref="CommitAsync"/> on this transaction.</para>
		/// <para>Read-only transactions do not modify the database when committed and will result in the Task completing with an error.</para>
		/// <para>Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.</para>
		/// </remarks>
		Task<VersionStamp> GetVersionStampAsync();
		//REVIEW: we should not return a Task<VersionStamp> but some sort of struct that is awaitable (like FdbWatch), to prevent misuse and potential deadlocks!

		/// <summary>Returns a place-holder 80-bit <see cref="VersionStamp"/>, whose value is not yet known, but will be filled by the database at commit time.</summary>
		/// <returns>This value can be used to generate temporary keys or value, for use with the <see cref="FdbMutationType.VersionStampedKey"/> or <see cref="FdbMutationType.VersionStampedValue"/> mutations</returns>
		/// <remarks>
		/// <para>The generate placeholder will use a random value that is unique per transaction (and changes at each retry).</para>
		/// <para>If you need to generate multiple different stamps per transaction (ex: adding multiple items to the same subspace), either call <see cref="CreateVersionStamp(int)"/> or <see cref="CreateUniqueVersionStamp()"/>!</para>
		/// <para>If the key contains the exact 80-bit byte signature of this token, the corresponding location will be tagged and replaced with the actual VersionStamp at commit time.</para>
		/// <para>If another part of the key contains (by random chance) the same exact byte sequence, then an error will be triggered, and hopefully the transaction will retry with another byte sequence.</para>
		/// </remarks>
		[Pure]
		VersionStamp CreateVersionStamp();

		/// <summary>Returns a place-holder 96-bit <see cref="VersionStamp"/> with an attached user version, whose value is not yet known, but will be filled by the database at commit time.</summary>
		/// <returns>This value can be used to generate temporary keys or value, for use with the <see cref="FdbMutationType.VersionStampedKey"/> or <see cref="FdbMutationType.VersionStampedValue"/> mutations</returns>
		/// <remarks>
		/// <para>The generate placeholder will use a random value that is unique per transaction (and changes at reach retry).</para>
		/// <para>If the key contains the exact 80-bit byte signature of this token, the corresponding location will be tagged and replaced with the actual VersionStamp at commit time.</para>
		/// <para>If another part of the key contains (by random chance) the same exact byte sequence, then an error will be triggered, and hopefully the transaction will retry with another byte sequence.</para>
		/// </remarks>
		[Pure]
		VersionStamp CreateVersionStamp(int userVersion);

		/// <summary>Returns a place-holder 96-bit <see cref="VersionStamp"/> with a unique user version _per_ transaction.</summary>
		/// <remarks>Use this method, instead of <see cref="CreateVersionStamp()"/> if you intend to add multiple stamped keys to the same subspace, inside the same transaction!</remarks>
		[Pure]
		VersionStamp CreateUniqueVersionStamp();

		/// <summary>Watches a key for any future change in the database.</summary>
		/// <param name="key">Name of the key that will be watched</param>
		/// <param name="ct">Token used to abort the watch if the caller doesn't want to wait anymore.</param>
		/// <returns><see cref="FdbWatch"/> instance that can be awaited and will complete when the key has changed in the database, or cancellation occurs.</returns>
		/// <remarks>
		/// <para>The watch will only become active if the transaction successfully commits, and should <b>NOT</b> be awaited from within the same or another transaction.</para>
		/// <para>You can directly <see langword="await"/> an <see cref="FdbWatch"/>, or use the <see cref="FdbWatch.Task"/> property.</para>
		/// <para>You can call <see cref="FdbWatch.Cancel"/>> at any time if you are not interested in watching the key anymore.</para>
		/// <para>You <b>MUST</b> always call <see cref="FdbWatch.Dispose"/> if the watch completes or is cancelled, to ensure that resources are released properly.</para>
		/// <para>It is possible (though rare) that a Watch fires even if the key did not change.</para>
		/// </remarks>
		[Pure]
		FdbWatch Watch(ReadOnlySpan<byte> key, CancellationToken ct);

	}

}
