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
	using FoundationDB.Filters.Logging;

	/// <summary>Transaction that allows read operations</summary>
	[PublicAPI]
	public interface IFdbReadOnlyTransaction : IDisposable
	{

		/// <summary>Local id of the transaction</summary>
		/// <remarks>This id is only guaranteed unique inside the current AppDomain or process and is reset on every restart. It should only be used for diagnostics and/or logging.</remarks>
		int Id { get; }

		/// <summary>Database of this transaction</summary>
		IFdbDatabase Database { get; }

		/// <summary>Tenant of this transaction</summary>
		/// <remarks>If <see langword="null"/>, the transaction can interact with the complete keyspace</remarks>
		IFdbTenant? Tenant { get; }

		/// <summary>Context of this transaction.</summary>
		FdbOperationContext Context { get; }

		/// <summary>If <see langword="true"/>, the transaction is operating in Snapshot mode</summary>
		bool IsSnapshot { get; }

		/// <summary>Return a Snapshot version of this transaction, or the transaction itself it is already operating in Snapshot mode.</summary>
		IFdbReadOnlyTransaction Snapshot { get; }

		/// <summary>Cancellation Token linked to the lifetime of the transaction</summary>
		/// <remarks>Will be triggered if the transaction is aborted or disposed</remarks>
		CancellationToken Cancellation { get; }

		/// <summary>Ensure that the transaction is in a valid state for issuing read operations.</summary>
		/// <exception cref="System.ObjectDisposedException">If <see cref="IDisposable.Dispose">Dispose()</see> has already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If the transaction as already been committed, or if the database connection has been closed</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token has been cancelled</exception>
		void EnsureCanRead();

		/// <summary>Returns the number of keys read byte this transaction, as well as their total size</summary>
		(int Keys, long Size) GetReadStatistics();

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		Task<Slice> GetAsync(ReadOnlySpan<byte> key);

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="valueDecoder">Decoder that will extract the result from the value found in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		Task<TResult> GetAsync<TResult>(ReadOnlySpan<byte> key, FdbValueDecoder<TResult> valueDecoder);

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValueState">Type of the state the is passed to the decoder</typeparam>
		/// <typeparam name="TResult">Type of the result decoded from the value found in the database</typeparam>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="valueState">State that will be forwarded to the <paramref name="valueDecoder"/></param>
		/// <param name="valueDecoder">Decoder that will extract the result from the value found in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		Task<TResult> GetAsync<TValueState, TResult>(ReadOnlySpan<byte> key, TValueState valueState, FdbValueDecoder<TValueState, TResult> valueDecoder);

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		Task<Slice[]> GetValuesAsync(ReadOnlySpan<Slice> keys);

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <typeparam name="TResult">Type of the result decoded from the value found in the database</typeparam>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="valueDecoder">Decoder that will extract the result from the value found in the database.</param>
		/// <returns>Task that will complete once all the keys have been read and values decoded.</returns>
		Task GetValuesAsync<TResult>(ReadOnlySpan<Slice> keys, Memory<TResult> results, FdbValueDecoder<TResult> valueDecoder);

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <typeparam name="TValueState">Type of the state the is passed to the decoder</typeparam>
		/// <typeparam name="TResult">Type of the result decoded from the value found in the database</typeparam>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="valueState">State that will be forwarded to the <paramref name="valueDecoder"/></param>
		/// <param name="valueDecoder">Decoder that will extract the result from the value found in the database.</param>
		/// <returns>Task that will complete once all the keys have been read and values decoded.</returns>
		Task GetValuesAsync<TValueState, TResult>(ReadOnlySpan<Slice> keys, Memory<TResult> results, TValueState valueState, FdbValueDecoder<TValueState, TResult> valueDecoder);

		/// <summary>Resolves a key selector against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		Task<Slice> GetKeyAsync(KeySelector selector);

		/// <summary>Resolves a key selector against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		Task<Slice> GetKeyAsync(KeySpanSelector selector);

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="selectors">Key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		Task<Slice[]> GetKeysAsync(ReadOnlySpan<KeySelector> selectors);

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the beginning key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If <see cref="FdbRangeOptions.Streaming">streaming mode</see> is <see cref="FdbStreamingMode.Iterator"/>, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns>Chunk of results</returns>
		Task<FdbRangeChunk> GetRangeAsync(
			KeySelector beginInclusive,
			KeySelector endExclusive,
			FdbRangeOptions? options = null,
			int iteration = 0
		);

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the beginning key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If <see cref="FdbRangeOptions.Streaming">streaming mode</see> is <see cref="FdbStreamingMode.Iterator"/>, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns>Chunk of results</returns>
		Task<FdbRangeChunk> GetRangeAsync(
			KeySpanSelector beginInclusive,
			KeySpanSelector endExclusive,
			FdbRangeOptions? options = null,
			int iteration = 0
		);

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the beginning key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If <see cref="FdbRangeOptions.Streaming">streaming mode</see> is <see cref="FdbStreamingMode.Iterator"/>, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns>Chunk of results</returns>
		Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(
			KeySelector beginInclusive,
			KeySelector endExclusive,
			TState state,
			FdbKeyValueDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null,
			int iteration = 0
		);

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the beginning key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If <see cref="FdbRangeOptions.Streaming">streaming mode</see> is <see cref="FdbStreamingMode.Iterator"/>, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns>Chunk of results</returns>
		Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(
			KeySpanSelector beginInclusive,
			KeySpanSelector endExclusive,
			TState state,
			FdbKeyValueDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null,
			int iteration = 0
		);

		/// <summary>
		/// Creates a new range query that will read all key-value pairs in the database snapshot represented by the transaction
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		[Pure, LinqTunnel]
		IFdbKeyValueRangeQuery GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null);

		/// <summary>
		/// Creates a new range query that will read all key-value pairs in the database snapshot represented by the transaction, and transform them into a result of type <typeparamref name="TResult"/>
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="selector">Selector used to convert each key-value pair into an element of type <typeparamref name="TResult"/></param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		[Pure, LinqTunnel]
		IFdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions? options = null);

		/// <summary>
		/// Creates a new range query that will read all key-value pairs in the database snapshot represented by the transaction, and transform them into a result of type <typeparamref name="TResult"/>
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="selector"/></param>
		/// <param name="selector">Selector used to convert each key-value pair into an element of type <typeparamref name="TResult"/></param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		[Pure, LinqTunnel]
		IFdbRangeQuery<TResult> GetRange<TState, TResult>(KeySelector beginInclusive, KeySelector endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> selector, FdbRangeOptions? options = null);

		/// <summary>Visits all key-value pairs in the database snapshot represent by the transaction</summary>
		/// <typeparam name="TState">Type of the state that is passed to the handler</typeparam>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="visitor"/></param>
		/// <param name="visitor">Lambda called for each key-value pair, in order.</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Number of key/value pairs visited</returns>
		Task<long> VisitRangeAsync<TState>(KeySelector beginInclusive, KeySelector endExclusive, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null);

		/// <summary>Check if the value from the database snapshot represented by the current transaction is equal to some <paramref name="expected"/> value.</summary>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="expected">Expected value for this key</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		/// <returns>Return the result of the check, plus the actual value of the key.</returns>
		Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected);

		/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="key"/> and its associated value</summary>
		/// <param name="key">Name of the key whose location is to be queried.</param>
		/// <returns>Task that will return an array of strings, or an exception</returns>
		/// <remarks>Depending on the API level or whether database option <see cref="FdbTransactionOption.IncludePortInAddress"/> is set, the returned string may or may not include the port numbers</remarks>
		Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key);

		/// <summary>Returns a list of keys that can split the given range into (roughly) equally sized chunks based on <paramref name="chunkSize"/>.</summary>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <param name="chunkSize">Size of chunks that will be used to split the range</param>
		/// <returns>Task that will return an array of keys that split the range in equally sized chunks, or an exception</returns>
		/// <remarks>The returned split points contain the start key and end key of the given range</remarks>
		Task<Slice[]> GetRangeSplitPointsAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize);

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		Task<long> GetEstimatedRangeSizeBytesAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey);

		/// <summary>Returns this transaction snapshot read version.</summary>
		Task<long> GetReadVersionAsync();

		/// <summary>Safely read a key containing a <see cref="VersionStamp"/> representing the version of some metadata or schema information stored in the database.</summary>
		/// <param name="key">Key to read. If <see cref="Slice.Nil"/>, read the global <c>\xff/metadataVersion</c> key</param>
		/// <remarks>Either the current value of the key, or <see cref="Slice.Nil"/> if the key has already changed in this transaction</remarks>
		/// <remarks>
		/// This should be used when implementing caching layers that use one or more "metadata version" keys to detect local changes.
		/// If the same key has already been changed within the transaction (via <see cref="IFdbTransaction.TouchMetadataVersionKey"/>) this method will return <c>null</c>.
		/// When this happens, the caller must consider that any previous cached state is invalid, and should not construct any new cache state inside this transaction!
		/// Please note that attempting to read or write this key using regular <see cref="GetAsync"/> or <see cref="FdbMutationType.VersionStampedValue"/> atomic operations can render the transaction unable to commit.
		/// If the key does not exist in the database, the zero <see cref="VersionStamp"/> will be returned instead.
		/// </remarks>
		Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key = default);

		/// <summary>
		/// Sets the snapshot read version used by a transaction. This is not needed in simple cases.
		/// </summary>
		/// <param name="version">Read version to use in this transaction</param>
		/// <remarks>
		/// If the given version is too old, subsequent reads will fail with error_code_past_version; if it is too new, subsequent reads may be delayed indefinitely and/or fail with error_code_future_version.
		/// If any of Get*() methods have been called on this transaction already, the result is undefined.
		/// </remarks>
		void SetReadVersion(long version);

		/// <summary>Cancels the transaction. All pending or future uses of the transaction will return a TransactionCancelled error code. The transaction can be used again after it is reset.</summary>
		void Cancel();

		/// <summary>
		/// Reset transaction to its initial state.
		/// </summary>
		/// <remarks>This is similar to disposing the transaction and recreating a new one.  The only state that persists through a transaction reset is that which is related to the back-off logic used by OnErrorAsync()</remarks>
		void Reset();

		/// <summary>
		/// Implements the recommended retry and back-off behavior for a transaction.
		///
		/// This function knows which of the error codes generated by other query functions represent temporary error conditions and which represent application errors that should be handled by the application. 
		/// It also implements an exponential back-off strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
		/// </summary>
		/// <param name="code">FdbError code thrown by the previous command</param>
		/// <returns>Returns a task that completes if the operation can be safely retried, or that rethrows the original exception if the operation is not retry-able.</returns>
		Task OnErrorAsync(FdbError code);

		/// <summary>Helper that can set options for this transaction</summary>
		IFdbTransactionOptions Options { get; }

		/// <summary>Log of all operations performed on this transaction (if logging was enabled on the database or transaction)</summary>
		FdbTransactionLog? Log { get; }

		/// <summary>Return <see langword="true"/> if logging is enabled on this transaction</summary>
		/// <remarks>
		/// If logging is enabled, the transaction will track all the operations performed by this transaction until it completes.
		/// The log can be accessed via the <see cref="Log"/> property.
		/// Comments can be added via the <see cref="Annotate(string)"/> method.
		/// </remarks>
		bool IsLogged();

		/// <summary>Add a comment to the transaction log</summary>
		/// <param name="comment">Line of text that will be added to the log</param>
		/// <remarks>This method does nothing if logging is disabled. To prevent unnecessary allocations, you may check <see cref="IsLogged"/> first</remarks>
		/// <example><code>tr.Annotate("Reticulating splines...");</code></example>
		void Annotate(string comment);

		/// <summary>Add a comment to the transaction log</summary>
		/// <param name="comment">Line of text that will be added to the log</param>
		/// <remarks>This method does nothing if logging is disabled. To prevent unnecessary allocations, you may check <see cref="IsLogged"/> first</remarks>
		/// <example><code>tr.Annotate($"Reticulated {splines.Count} splines");</code></example>
		void Annotate(ref InvariantInterpolatedStringHandler comment);

		/// <summary>If logging was previously enabled on this transaction, clear the log and stop logging any new operations</summary>
		/// <remarks>Any log handler attached to this transaction will not be called</remarks>
		void StopLogging();

	}

	/// <summary>Delegate that will decode a key or value read from the database</summary>
	/// <typeparam name="TState">Type of the state provided by the caller</typeparam>
	/// <typeparam name="TResult">Type of the decoded result</typeparam>
	/// <param name="state">State that will be passed to the delegate, in order to reduce scope allocations</param>
	/// <param name="bytes">Binary data representing the key or value to decode</param>
	/// <returns>Decoded value</returns>
	public delegate TResult FdbRangeDecoder<in TState, out TResult>(TState state, ReadOnlySpan<byte> bytes);

	/// <summary>Delegate that will decode a key or value read from the database</summary>
	/// <typeparam name="TResult">Type of the decoded result</typeparam>
	/// <param name="bytes">Binary data representing the key or value to decode</param>
	/// <returns>Decoded value</returns>
	public delegate TResult FdbRangeDecoder<out TResult>(ReadOnlySpan<byte> bytes);

	/// <summary>Delegate that will decode a value read from the database</summary>
	/// <typeparam name="TState">Type of the state provided by the caller</typeparam>
	/// <typeparam name="TResult">Type of the decoded result</typeparam>
	/// <param name="state">State that will be passed to the delegate, in order to reduce scope allocations</param>
	/// <param name="bytes">Binary data representing the value to decode</param>
	/// <param name="found">If <see langword="false"/>, the value was not found, and the returned value should represent a "logical" null for this type.</param>
	/// <returns>Decoded value</returns>
	public delegate TResult FdbValueDecoder<in TState, out TResult>(TState state, ReadOnlySpan<byte> bytes, bool found);

	/// <summary>Delegate that will decode a value read from the database</summary>
	/// <typeparam name="TResult">Type of the decoded result</typeparam>
	/// <param name="bytes">Binary data representing the value to decode</param>
	/// <param name="found">If <see langword="false"/>, the value was not found, and the returned value should represent a "logical" null for this type.</param>
	/// <returns>Decoded value</returns>
	public delegate TResult FdbValueDecoder<out TResult>(ReadOnlySpan<byte> bytes, bool found);

	/// <summary>Delegate that will observe a key/value pair read from the database</summary>
	/// <typeparam name="TState">Type of the state provided by the caller</typeparam>
	/// <param name="state">State that will be passed to the delegate, in order to reduce scope allocations</param>
	/// <param name="keyBytes">Binary key representing the key to observe. Empty span if the key was not found</param>
	/// <param name="valueBytes">Binary data representing the value to observe</param>
	public delegate void FdbKeyValueAction<in TState>(TState state, ReadOnlySpan<byte> keyBytes, ReadOnlySpan<byte> valueBytes);

	/// <summary>Delegate that will decode a key/value pair read from the database</summary>
	/// <typeparam name="TState">Type of the state provided by the caller</typeparam>
	/// <typeparam name="TResult">Type of the decoded result</typeparam>
	/// <param name="state">State that will be passed to the delegate, in order to reduce scope allocations</param>
	/// <param name="keyBytes">Binary key representing the key to decode. Empty span if the key was not found</param>
	/// <param name="valueBytes">Binary data representing the value to decode</param>
	/// <returns>Decoded value</returns>
	public delegate TResult FdbKeyValueDecoder<in TState, out TResult>(TState state, ReadOnlySpan<byte> keyBytes, ReadOnlySpan<byte> valueBytes);

	/// <summary>Delegate that will decode a key/value pair read from the database</summary>
	/// <typeparam name="TResult">Type of the decoded result</typeparam>
	/// <param name="keyBytes">Binary key representing the key to decode. Empty span if the key was not found</param>
	/// <param name="valueBytes">Binary data representing the value to decode</param>
	/// <returns>Decoded value</returns>
	public delegate TResult FdbKeyValueDecoder<out TResult>(ReadOnlySpan<byte> keyBytes, ReadOnlySpan<byte> valueBytes);

}
