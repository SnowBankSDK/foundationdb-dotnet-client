#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

	/// <summary>Defines a set of options for a transaction</summary>
	[PublicAPI]
	public enum FdbTransactionOption
	{
		/// <summary>None</summary>
		None = 0,

		/// <summary>The transaction, if not self-conflicting, may be committed a second time after commit succeeds, in the event of a fault</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		CausalWriteRisky = 10,

		/// <summary>The read version will be committed, and usually will be the latest committed, but might not be the latest committed in the event of a fault or partition</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		CausalReadRisky = 20,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		CausalReadDisable = 21,

		//note: there is no entry for 22,

		/// <summary>Addresses returned by <see cref="IFdbReadOnlyTransaction.GetAddressesForKeyAsync"/> include the port when enabled. This will be enabled by default in api version 700, and this option will be deprecated.</summary>
		IncludePortInAddress = 23,

		/// <summary>The next write performed on this transaction will not generate a write conflict range.
		/// As a result, other transactions which read the key(s) being modified by the next write will not conflict with this transaction.
		/// Care needs to be taken when using this option on a transaction that is shared between multiple threads.
		/// When setting this option, write conflict ranges will be disabled on the next write operation, regardless of what thread it is on.</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		NextWriteNoWriteConflictRange = 30,

		/// <summary>Committing this transaction will bypass the normal load balancing across proxies and go directly to the specifically nominated 'first proxy'.</summary>
		CommitOnFirstProxy = 40, // hidden

		/// <summary>For internal use only.</summary>
		CheckWritesEnable = 50, // hidden

		/// <summary>Reads performed by a transaction will not see any prior mutations that occurred in that transaction, instead seeing the value which was in the database at the transaction's read version.
		/// This option may provide a small performance benefit for the client, but also disables a number of client-side optimizations which are beneficial for transactions which tend to read and write the same keys within a single transaction.
		/// Also note that with this option invoked any outstanding reads will return errors when transaction commit is called (rather than the normal behavior of commit waiting for outstanding reads to complete).</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		ReadYourWritesDisable = 51,

		/// <summary>Deprecated</summary>
		[Obsolete("This option is not available anymore")]
		ReadAheadDisable = 52,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DurabilityDataCenter = 110,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DurabilityRisky = 120,

		/// <summary>Deprecated</summary>
		[Obsolete("This option is not available anymore")]
		DevNullIsWebScale = 130,

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one.
		/// Use is discouraged outside low-level tools</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		PrioritySystemImmediate = 200,

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first.
		/// Useful for doing batch work simultaneously with latency-sensitive work</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		PriorityBatch = 201,

		/// <summary>This is a write-only transaction which sets the initial configuration.
		/// This option is designed for use by database system tools only.</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		InitializeNewDatabase = 300,

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		AccessSystemKeys = 301,

		/// <summary>Allows this transaction to read system keys (those that start with the byte 0xFF)</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		ReadSystemKeys = 302,

		/// <summary>Allows this transaction to access the raw key-space when tenant mode is on.</summary>
		RawAccess = 303,

		/// <summary>Allows this transaction to bypass storage quota enforcement.</summary>
		/// <remarks>Should only be used for transactions that directly or indirectly decrease the size of the tenant group's data.</remarks>
		BypassStorageQuota = 304,

		/// <summary>Enables dumping the set of mutations generated by a transaction, at commit time.</summary>
		DebugDump = 400, // hidden

		/// <summary>Sets a client provided identifier for the transaction that will be used for logging during retries.</summary>
		/// <remarks>Parameter: (String) Optional transaction name</remarks>
		DebugRetryLogging = 401,

		/// <summary>Enables tracing for this transaction and logs results to the client traces logs.</summary>
		/// <remarks>Parameter: (String) Optional transaction name</remarks>
		/// <remarks>This option has been split into multiple sub-options: <see cref="DebugTransactionIdentifier"/> and <see cref="LogTransaction"/></remarks>
		[Obsolete("This option is deprecated.")]
		TransactionLoggingEnable = 402,

		/// <summary>Sets a client provided identifier for the transaction that will be used in scenarios like tracing or profiling.
		/// Client trace logging or transaction profiling must be separately enabled.</summary>
		/// <remarks>
		/// <para>Parameter: (String) String identifier to be used when tracing or profiling this transaction. The identifier must not exceed 100 characters.</para>
		/// </remarks>
		DebugTransactionIdentifier = 403,

		/// <summary>Enables tracing for this transaction and logs results to the client trace logs. The <see cref="DebugTransactionIdentifier"/> option must be set before using this option, and client trace logging must be enabled and to get log output.</summary>
		LogTransaction = 404,

		/// <summary>Sets the maximum escaped length of key and value fields to be logged to the trace file via the <see cref="LogTransaction"/> option, after which the field will be truncated. A negative value disables truncation.</summary>
		/// <remarks>Parameter: (Int32) Maximum length of escaped key and value fields.</remarks>
		TransactionLoggingMaxFieldLength = 405,

		/// <summary>Sets an identifier for server tracing of this transaction.</summary>
		/// <remarks>
		/// <para>When committed, this identifier triggers logging when each part of the transaction authority encounters it, which is helpful in diagnosing slowness in misbehaving clusters.</para>
		/// <para>The identifier is randomly generated.</para>
		/// <para>When there is also a <see cref="DebugTransactionIdentifier"/>, both IDs are logged together.</para>
		/// </remarks>
		ServerRequestTracing = 406,

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [0, int.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// Like all transaction options, a timeout must be reset after a call to onError. This behavior allows the user to make the timeout dynamic.</summary>
		/// <remarks>Parameter: (Int32) value in milliseconds of timeout</remarks>
		Timeout = 500, // persistent

		/// <summary>Set a maximum number of retries after which additional calls to onError will throw the most recently seen error code.
		/// Valid parameter values are [-1, int.MaxValue]. If set to -1, will disable the retry limit.
		/// Parameter: (Int32) number of times to retry
		/// </summary>
		RetryLimit = 501, // persistent

		/// <summary>Set the maximum amount of backoff delay incurred in the call to onError if the error is retryable.
		/// Defaults to 1000 ms. Valid parameter values are [0, int.MaxValue].
		/// Like all transaction options, the maximum retry delay must be reset after a call to onError.
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// Parameter: (Int32) value in milliseconds of maximum delay
		/// </summary>
		MaxRetryDelay = 502, // persistent

		/// <summary>Set the transaction size limit in bytes.
		/// The size is calculated by combining the sizes of all keys and values written or mutated, all key ranges cleared, and all read and write conflict ranges. (In other words, it includes the total size of all data included in the request to the cluster to commit the transaction.)
		/// Large transactions can cause performance problems on FoundationDB clusters, so setting this limit to a smaller value than the default can help prevent the client from accidentally degrading the cluster's performance.
		/// This value must be at least 32 and cannot be set to higher than 10,000,000, the default transaction size limit.</summary>
		/// <remarks>Parameter: (Int32) value in bytes</remarks>
		SizeLimit = 503,

		/// <summary>Associate this transaction with this ID for the purpose of checking whether this transaction has already committed.</summary>
		/// <remarks>
		/// <para>Parameter: (String) Unique ID. Must be at least 16 bytes and less than 256 bytes</para>
		/// <para>This feature is in development and not ready for general use.</para>
		/// <para>Unless the <see cref="AutomaticIdempotency"/> option is set after this option, the client will not automatically attempt to remove this id from the cluster after a successful commit.</para>
		/// </remarks>
		IdempotencyId = 504,

		/// <summary>Automatically assign a random 16 byte idempotency id for this transaction.</summary>
		/// <remarks>
		/// <para>Prevents commits from failing with <see cref="FdbError.CommitUnknownResult"/>.</para>
		/// <para>WARNING: If you are also using the multiversion client or transaction timeouts, if either cluster_version_changed or transaction_timed_out was thrown during a commit, then that commit may have already succeeded or may succeed in the future.</para>
		/// <para>This feature is in development and not ready for general use.</para>
		/// </remarks>
		AutomaticIdempotency = 505,

		/// <summary>Storage server should cache disk blocks needed for subsequent read requests in this transaction.</summary>
		/// <remarks>This is the default behavior.</remarks>
		ReadServerSizeCacheEnable = 507,

		/// <summary>Storage server should not cache disk blocks needed for subsequent read requests in this transaction.</summary>
		/// <remarks>This can be used to avoid cache pollution for reads not expected to be repeated.</remarks>
		ReadServerSizeCacheDisable = 508,

		/// <summary>Use normal read priority for subsequent read requests in this transaction.</summary>
		/// <remarks>This is the default behavior.</remarks>
		ReadPriorityNormal = 509,

		/// <summary>Use low read priority for subsequent read requests in this transaction.</summary>
		ReadPriorityLow = 510,

		/// <summary>Use high read priority for subsequent read requests in this transaction.</summary>
		ReadPriorityHigh = 511,

		/// <summary>Snapshot read operations will see the results of writes done in the same transaction.</summary>
		SnapshotReadYourWritesEnable = 600,

		/// <summary>Snapshot read operations will not see the results of writes done in the same transaction.</summary>
		SnapshotReadYourWritesDisable = 601,

		/// <summary>The transaction can read and write to locked databases, and is responsible for checking that it took the lock.</summary>
		LockAware = 700,

		/// <summary>By default, operations that are performed on a transaction while it is being committed will not only fail themselves, but they will attempt to fail other in-flight operations (such as the commit) as well.
		/// This behavior is intended to help developers discover situations where operations could be unintentionally executed after the transaction has been reset.
		/// Setting this option removes that protection, causing only the offending operation to fail.</summary>
		UsedDuringCommitProtectionDisable = 701,

		/// <summary>The transaction can read from locked databases.</summary>
		ReadLockAware = 702,

		/// <summary>No other transactions will be applied before this transaction within the same commit version.</summary>
		FirstInBatch = 710, // hidden

		/// <summary>This option should only be used by tools which change the database configuration.</summary>
		UseProvisionalProxies = 711,

		/// <summary>The transaction can retrieve keys that are conflicting with other transactions.</summary>
		ReportConflictingKeys = 712,

		/// <summary>Allow reading from zero or more modules.</summary>
		/// <remarks>
		/// <para>By default, the special key space will only allow users to read from exactly one module (a subspace in the special key space). Use this option to allow reading from zero or more modules.</para>
		/// <para>Users who set this option should be prepared for new modules, which may have different behaviors than the modules they're currently reading. For example, a new module might block or return an error.</para>
		/// </remarks>
		SpecialKeySpaceRelaxed = 713,

		/// <summary>By default, users are not allowed to write to special keys.</summary>
		/// <remarks>
		/// <para>Enabling this option will implicitly enable all options required to achieve the configuration change.</para>
		/// </remarks>
		SpecialKeySpaceEnableWrites = 714,

		/// <summary>Adds a tag to the transaction that can be used to apply manual targeted throttling.</summary>
		/// <remarks>
		/// <para>Parameter: (String) String identifier used to associate this transaction with a throttling group. Must not exceed 16 characters.</para>
		/// <para>At most 5 tags can be set on a transaction.</para>
		/// </remarks>
		Tag = 800,

		/// <summary>Adds a tag to the transaction that can be used to apply manual or automatic targeted throttling.</summary>
		/// <remarks>
		/// <para>Parameter: (String) String identifier used to associate this transaction with a throttling group. Must not exceed 16 characters.</para>
		/// <para>At most 5 tags can be set on a transaction.</para>
		/// </remarks>
		AutoThrottleTag = 801,

		/// <summary>Adds a parent to the Span of this transaction. Used for transaction tracing. A span can be identified with any 16 bytes.</summary>
		SpanParent = 900,

		/// <summary>Asks storage servers for how many bytes a clear key range contains. Otherwise, uses the location cache to roughly estimate this.</summary>
		ExpensiveClearCostEstimationEnable = 1000,

		/// <summary>Allows <c>get</c> operations to read from sections of keyspace that have become unreadable because of VersionStamp operations.</summary>
		/// <remarks>
		/// <para>These reads will view VersionStamp operations as if they were set operations that did not fill in the VersionStamp.</para>
		/// </remarks>
		BypassUnreadable = 1100,

		/// <summary>Allows this transaction to use cached GRV from the database context.</summary>
		/// <remarks>
		/// <para>Defaults to off.</para>
		/// <para>Upon first usage, starts a background updater to periodically update the cache to avoid stale read versions.</para>
		/// <para>The <see cref="FdbNetworkOption.DisableClientBypass"/> option must also be set.</para>
		/// </remarks>
		UseGrvCache = 1101,

		/// <summary>Specifically instruct this transaction to NOT use cached GRV.</summary>
		/// <remarks>Primarily used for the read version cache's background updater to avoid attempting to read a cached entry in specific situations.</remarks>
		SkipGrvCache = 1102, // hidden

		/// <summary>Attach given authorization token to the transaction such that subsequent tenant-aware requests are authorized.</summary>
		/// <remarks>
		/// <para>Parameter: (String) A JSON Web Token authorized to access data belonging to one or more tenants, indicated by 'tenants' claim of the token's payload.</para>
		/// <para>Attach given authorization token to the transaction such that subsequent tenant-aware requests are authorized</para>
		/// </remarks>
		AuthorizationToken = 2000, // persistent + sensitive

		/// <summary>Enables replica consistency check</summary>
		/// <remarks>
		/// <para>Compares the results returned by storage server replicas (as many as specified by <see cref="ConsistencyCheckRequiredReplicas"/> option) for a given read request, in client-side load balancer.</para>
		/// </remarks>
		EnableReplicaConsistencyCheck = 4000,

		/// <summary>Specifies the number of storage server replica results that the load balancer needs to compare when the <see cref="EnableReplicaConsistencyCheck"/> option is set.</summary>
		/// <remarks>
		/// <para>Parameter: (int) Number of storage replicas over which the load balancer consistency check is done.</para>
		/// </remarks>
		ConsistencyCheckRequiredReplicas = 4001,

	}

}
