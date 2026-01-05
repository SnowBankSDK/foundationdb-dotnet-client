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

	/// <summary>Extension methods for <see cref="IFdbTransactionOptions"/></summary>
	[PublicAPI]
	public static class FdbTransactionOptionsExtensions
	{
		/// <summary>Allows this transaction to read system keys (those that start with the byte 0xFF)</summary>
		/// <remarks>See <see cref="FdbTransactionOption.ReadSystemKeys"/></remarks>
		public static IFdbTransactionOptions WithReadAccessToSystemKeys(this IFdbTransactionOptions options)
		{
			return options.SetOption(options.ApiVersion >= 300 ? FdbTransactionOption.ReadSystemKeys : FdbTransactionOption.AccessSystemKeys);
		}

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		/// <remarks>See <see cref="FdbTransactionOption.AccessSystemKeys"/></remarks>
		public static IFdbTransactionOptions WithWriteAccessToSystemKeys(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.AccessSystemKeys);
		}

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one. Use is discouraged outside low-level tools</summary>
		/// <remarks>See <see cref="FdbTransactionOption.PrioritySystemImmediate"/></remarks>
		public static IFdbTransactionOptions WithPrioritySystemImmediate(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.PrioritySystemImmediate);
		}

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first. Useful for doing batch work simultaneously with latency-sensitive work</summary>
		/// <remarks>See <see cref="FdbTransactionOption.PriorityBatch"/></remarks>
		public static IFdbTransactionOptions WithPriorityBatch(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.PriorityBatch);
		}

		/// <summary>Use low read priority for subsequent read requests in this transaction.</summary>
		/// <remarks>See <see cref="FdbTransactionOption.ReadPriorityLow"/></remarks>
		public static IFdbTransactionOptions WithReadPriorityLow(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ReadPriorityLow);
		}

		/// <summary>Use normal read priority for subsequent read requests in this transaction.</summary>
		/// <remarks>See <see cref="FdbTransactionOption.ReadPriorityNormal"/></remarks>
		public static IFdbTransactionOptions WithReadPriorityNormal(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ReadPriorityNormal);
		}

		/// <summary>Use high read priority for subsequent read requests in this transaction.</summary>
		/// <remarks>See <see cref="FdbTransactionOption.ReadPriorityHigh"/></remarks>
		public static IFdbTransactionOptions WithReadPriorityHigh(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ReadPriorityHigh);
		}

		/// <summary>Reads performed by a transaction will not see any prior mutations that occurred in that transaction, instead seeing the value which was in the database at the transaction's read version. This option may provide a small performance benefit for the client, but also disables a number of client-side optimizations which are beneficial for transactions which tend to read and write the same keys within a single transaction. Also note that with this option invoked any outstanding reads will return errors when transaction commit is called (rather than the normal behavior of commit waiting for outstanding reads to complete).</summary>
		/// <remarks>See <see cref="FdbTransactionOption.ReadYourWritesDisable"/></remarks>
		public static IFdbTransactionOptions WithReadYourWritesDisable(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ReadYourWritesDisable);
		}

		/// <summary>Snapshot reads performed by a transaction will see the results of writes done in the same transaction.</summary>
		/// <remarks>See <see cref="FdbTransactionOption.SnapshotReadYourWritesEnable"/></remarks>
		public static IFdbTransactionOptions WithSnapshotReadYourWritesEnable(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.SnapshotReadYourWritesEnable);
		}

		/// <summary>Reads performed by a transaction will not see the results of writes done in the same transaction.</summary>
		/// <remarks>See <see cref="FdbTransactionOption.SnapshotReadYourWritesDisable"/></remarks>
		public static IFdbTransactionOptions WithSnapshotReadYourWritesDisable(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.SnapshotReadYourWritesDisable);
		}

		/// <summary>The next write performed on this transaction will not generate a write conflict range. As a result, other transactions which read the key(s) being modified by the next write will not conflict with this transaction. Care needs to be taken when using this option on a transaction that is shared between multiple threads. When setting this option, write conflict ranges will be disabled on the next write operation, regardless of what thread it is on.</summary>
		/// <remarks>See <see cref="FdbTransactionOption.NextWriteNoWriteConflictRange"/></remarks>
		public static IFdbTransactionOptions WithNextWriteNoWriteConflictRange(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.NextWriteNoWriteConflictRange);
		}

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [TimeSpan.Zero, TimeSpan.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// </summary>
		/// <param name="options">Transaction to use for the operation</param>
		/// <param name="timeout">Timeout (rounded up to milliseconds), or TimeSpan.Zero for infinite timeout</param>
		public static IFdbTransactionOptions WithTimeout(this IFdbTransactionOptions options, TimeSpan timeout)
		{
			return options.WithTimeout(timeout == TimeSpan.Zero ? 0 : (int) Math.Ceiling(timeout.TotalMilliseconds));
		}

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [0, int.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// </summary>
		/// <param name="options">Transaction to use for the operation</param>
		/// <param name="milliseconds">Timeout in millisecond, or 0 for infinite timeout</param>
		public static IFdbTransactionOptions WithTimeout(this IFdbTransactionOptions options, int milliseconds)
		{
			options.Timeout = milliseconds;
			return options;
		}

		/// <summary>Set a maximum number of retries after which additional calls to onError will throw the most recently seen error code.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="retries">Number of times to retry. If set to -1, will disable the retry limit.</param>
		public static IFdbTransactionOptions WithRetryLimit(this IFdbTransactionOptions options, int retries)
		{
			options.RetryLimit = retries;
			return options;
		}

		/// <summary>Set the maximum amount of backoff delay incurred in the call to onError if the error is retryable.
		/// Defaults to 1000 ms. Valid parameter values are [0, int.MaxValue].
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// </summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="milliseconds">Maximum retry delay (in milliseconds)</param>
		public static IFdbTransactionOptions WithMaxRetryDelay(this IFdbTransactionOptions options, int milliseconds)
		{
			options.MaxRetryDelay = milliseconds;
			return options;
		}

		/// <summary>Set the maximum amount of backoff delay incurred in the call to onError if the error is retryable.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="delay">Maximum retry delay (rounded up to milliseconds)</param>
		/// <remarks>
		/// <para>Defaults to 1000 ms. Valid parameter values are [<c>TimeSpan.Zero</c>, <c>TimeSpan.MaxValue</c>].</para>
		/// <para>If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithMaxRetryDelay(this IFdbTransactionOptions options, TimeSpan delay)
		{
			options.MaxRetryDelay = delay == TimeSpan.Zero ? 0 : (int) Math.Ceiling(delay.TotalMilliseconds);
			return options;
		}

		/// <summary>Set the transaction tracing options.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="tracing">Tracing options</param>
		public static IFdbTransactionOptions WithTracing(this IFdbTransactionOptions options, FdbTracingOptions tracing)
		{
			options.Tracing = tracing;
			return options;
		}

		/// <summary>Set the transaction size limit in bytes.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="limit">Value in bytes. This value must be at least 32 and cannot be set to higher than 10,000,000, the default transaction size limit.</param>
		/// <remarks>The size is calculated by combining the sizes of all keys and values written or mutated, all key ranges cleared, and all read and write conflict ranges. (In other words, it includes the total size of all data included in the request to the cluster to commit the transaction.)
		/// Large transactions can cause performance problems on FoundationDB clusters, so setting this limit to a smaller value than the default can help prevent the client from accidentally degrading the cluster's performance.</remarks>
		public static IFdbTransactionOptions WithSizeLimit(this IFdbTransactionOptions options, int limit)
		{
			return options.SetOption(FdbTransactionOption.SizeLimit, limit);
		}

		/// <summary>Enables tracing for this transaction and logs results to the client trace logs.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">String identifier to be used when tracing or profiling this transaction. The identifier must not exceed 100 characters.</param>
		/// <param name="maxFieldLength">If non-null, sets the maximum escaped length of key and value fields to be logged to the trace file via the LOG_TRANSACTION option, after which the field will be truncated. A negative value disables truncation.</param>
		/// <remarks>Client trace logging must be enabled via <see cref="Fdb.Options.TracePath"/> before the network thread is started, in order to get log output.</remarks>
		public static IFdbTransactionOptions WithTransactionLog(this IFdbTransactionOptions options, string id, int? maxFieldLength = null)
		{
			return options.WithTransactionLog(id.AsSpan(), maxFieldLength);
		}

		/// <summary>Enables tracing for this transaction and logs results to the client trace logs.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">String identifier to be used when tracing or profiling this transaction. The identifier must not exceed 100 characters.</param>
		/// <param name="maxFieldLength">If non-null, sets the maximum escaped length of key and value fields to be logged to the trace file via the LOG_TRANSACTION option, after which the field will be truncated. A negative value disables truncation.</param>
		/// <remarks>Client trace logging must be enabled via <see cref="Fdb.Options.TracePath"/> before the network thread is started, in order to get log output.</remarks>
		public static IFdbTransactionOptions WithTransactionLog(this IFdbTransactionOptions options, ReadOnlySpan<char> id, int? maxFieldLength = null)
		{
			options.SetOption(FdbTransactionOption.DebugTransactionIdentifier, id);
			if (maxFieldLength != null) options.SetOption(FdbTransactionOption.TransactionLoggingMaxFieldLength, maxFieldLength.Value);
			options.SetOption(FdbTransactionOption.LogTransaction);
			return options;
		}

		/// <summary>No other transactions will be applied before this transaction within the same commit version.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		public static IFdbTransactionOptions WithFirstInBatch(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.FirstInBatch);
		}

		/// <summary>The transaction can read and write to locked databases, and is responsible for checking that it took the lock.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		public static IFdbTransactionOptions WithLockAware(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.LockAware);
		}

		/// <summary>The transaction can read from locked databases.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		public static IFdbTransactionOptions WithReadLockAware(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ReadLockAware);
		}

		/// <summary>Allows this transaction to access the raw key-space when tenant mode is on.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>See <see cref="FdbTransactionOption.RawAccess"/></remarks>
		public static IFdbTransactionOptions WithRawAccess(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.RawAccess);
		}

		/// <summary>Allows this transaction to bypass storage quota enforcement.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>See <see cref="FdbTransactionOption.BypassStorageQuota"/></remarks>
		public static IFdbTransactionOptions WithBypassStorageQuota(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.BypassStorageQuota);
		}

		/// <summary>Sets an identifier for server tracing of this transaction.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">String identifier to be used when tracing or profiling this transaction. The identifier must not exceed 100 characters.</param>
		/// <remarks>See <see cref="FdbTransactionOption.ServerRequestTracing"/></remarks>
		public static IFdbTransactionOptions WithDebugTransactionIdentifier(this IFdbTransactionOptions options, string id)
		{
			return options.SetOption(FdbTransactionOption.DebugTransactionIdentifier, id.AsSpan());
		}

		/// <summary>Sets an identifier for server tracing of this transaction.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">String identifier to be used when tracing or profiling this transaction. The identifier must not exceed 100 characters.</param>
		/// <remarks>See <see cref="FdbTransactionOption.ServerRequestTracing"/></remarks>
		public static IFdbTransactionOptions WithDebugTransactionIdentifier(this IFdbTransactionOptions options, ReadOnlySpan<char> id)
		{
			return options.SetOption(FdbTransactionOption.DebugTransactionIdentifier, id);
		}

		/// <summary>Sets an identifier for server tracing of this transaction.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>See <see cref="FdbTransactionOption.ServerRequestTracing"/></remarks>
		public static IFdbTransactionOptions WithServerRequestTracing(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ServerRequestTracing);
		}

		/// <summary>Associate this transaction with this ID for the purpose of checking whether this transaction has already committed.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">Unique ID. Must be at least 16 bytes and less than 256 bytes.</param>
		/// <remarks>Requires API level 720 or greater. See <see cref="FdbTransactionOption.IdempotencyId"/></remarks>
		public static IFdbTransactionOptions WithIdempotencyId(this IFdbTransactionOptions options, string id)
		{
			return options.SetOption(FdbTransactionOption.IdempotencyId, id.AsSpan());
		}

		/// <summary>Associate this transaction with this ID for the purpose of checking whether this transaction has already committed.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">Unique ID. Must be at least 16 bytes and less than 256 bytes.</param>
		/// <remarks>Requires API level 720 or greater. See <see cref="FdbTransactionOption.IdempotencyId"/>.</remarks>
		public static IFdbTransactionOptions WithIdempotencyId(this IFdbTransactionOptions options, ReadOnlySpan<char> id)
		{
			return options.SetOption(FdbTransactionOption.IdempotencyId, id);
		}

		/// <summary>Automatically assign a random 16 byte idempotency id for this transaction.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.AutomaticIdempotency"/>.</para>
		/// <para>Prevents commits from failing with <see cref="FdbError.CommitUnknownResult"/>.</para>
		/// <para>WARNING: If you are also using the multiversion client or transaction timeouts, if either cluster_version_changed or transaction_timed_out was thrown during a commit, then that commit may have already succeeded or may succeed in the future.</para>
		/// <para>This feature is in development and not ready for general use.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithAutomaticIdempotency(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.AutomaticIdempotency);
		}

		/// <summary>The transaction can retrieve keys that are conflicting with other transactions.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.ReportConflictingKeys"/>.</para>
		/// <para>When <see cref="IFdbTransaction.CommitAsync"/> fails with error <see cref="FdbError.NotCommitted"/> (1020), you can retrieve the conflicting keys by performing a read range under the special key <c>`\xFF\xFF/transaction/conflicting_keys/`</c></para>
		/// </remarks>
		public static IFdbTransactionOptions WithReportConflictingKeys(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.ReportConflictingKeys);
		}

		/// <summary>Allow reading from zero or more modules.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.SpecialKeySpaceRelaxed"/>.</para>
		/// <para>By default, the special key space will only allow users to read from exactly one module (a subspace in the special key space). Use this option to allow reading from zero or more modules.</para>
		/// <para>Users who set this option should be prepared for new modules, which may have different behaviors than the modules they're currently reading. For example, a new module might block or return an error.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithSpecialKeySpaceRelaxed(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.SpecialKeySpaceRelaxed);
		}

		/// <summary>By default, users are not allowed to write to special keys.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.SpecialKeySpaceEnableWrites"/>.</para>
		/// <para>Enabling this option will implicitly enable all options required to achieve the configuration change.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithSpecialKeySpaceEnableWrites(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.SpecialKeySpaceEnableWrites);
		}

		/// <summary>Associate this transaction with this ID for the purpose of checking whether this transaction has already committed.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="tag">String identifier used to associate this transaction with a throttling group. Must not exceed 16 characters.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.Tag"/></para>
		/// <para>At most 5 tags can be set on a transaction.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithTag(this IFdbTransactionOptions options, string tag)
		{
			return options.SetOption(FdbTransactionOption.Tag, tag.AsSpan());
		}

		/// <summary>Associate this transaction with this ID for the purpose of checking whether this transaction has already committed.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="tag">String identifier used to associate this transaction with a throttling group. Must not exceed 16 characters.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.Tag"/></para>
		/// <para>At most 5 tags can be set on a transaction.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithTag(this IFdbTransactionOptions options, ReadOnlySpan<char> tag)
		{
			return options.SetOption(FdbTransactionOption.Tag, tag);
		}

		/// <summary>Adds a tag to the transaction that can be used to apply manual or automatic targeted throttling.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="tag">String identifier used to associate this transaction with a throttling group. Must not exceed 16 characters.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.Tag"/></para>
		/// <para>At most 5 tags can be set on a transaction.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithAutoThrottleTag(this IFdbTransactionOptions options, string tag)
		{
			return options.SetOption(FdbTransactionOption.AutoThrottleTag, tag.AsSpan());
		}

		/// <summary>Adds a tag to the transaction that can be used to apply manual or automatic targeted throttling.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="tag">String identifier used to associate this transaction with a throttling group. Must not exceed 16 characters.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.Tag"/></para>
		/// <para>At most 5 tags can be set on a transaction.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithAutoThrottleTag(this IFdbTransactionOptions options, ReadOnlySpan<char> tag)
		{
			return options.SetOption(FdbTransactionOption.AutoThrottleTag, tag);
		}

		/// <summary>Adds a parent to the Span of this transaction.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">A span can be identified with any 16 bytes.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.SpanParent"/></para>
		/// <para>Used for transaction tracing.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithSpanParent(this IFdbTransactionOptions options, Slice id)
		{
			return options.SetOption(FdbTransactionOption.SpanParent, id.Span);
		}

		/// <summary>Adds a parent to the Span of this transaction.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="id">A span can be identified with any 16 bytes.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.SpanParent"/></para>
		/// <para>Used for transaction tracing.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithSpanParent(this IFdbTransactionOptions options, ReadOnlySpan<byte> id)
		{
			return options.SetOption(FdbTransactionOption.SpanParent, id);
		}

		/// <summary>Allows <c>get</c> operations to read from sections of keyspace that have become unreadable because of VersionStamp operations.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.BypassUnreadable"/>.</para>
		/// <para>These reads will view VersionStamp operations as if they were set operations that did not fill in the VersionStamp.</para>
		/// </remarks>
		public static IFdbTransactionOptions WithBypassUnreadable(this IFdbTransactionOptions options)
		{
			return options.SetOption(FdbTransactionOption.BypassUnreadable);
		}

		/// <summary>Attach given authorization token to the transaction such that subsequent tenant-aware requests are authorized.</summary>
		/// <param name="options">Transaction that will be configured for the current attempt.</param>
		/// <param name="token">A JSON Web Token authorized to access data belonging to one or more tenants, indicated by 'tenants' claim of the token's payload.</param>
		/// <remarks>
		/// <para>See <see cref="FdbTransactionOption.AuthorizationToken"/></para>
		/// <para>Attach given authorization token to the transaction such that subsequent tenant-aware requests are authorized</para>
		/// </remarks>
		public static IFdbTransactionOptions WithAuthorizationToken(this IFdbTransactionOptions options, ReadOnlySpan<char> token)
		{
			return options.SetOption(FdbTransactionOption.AuthorizationToken, token);
		}

	}

}
