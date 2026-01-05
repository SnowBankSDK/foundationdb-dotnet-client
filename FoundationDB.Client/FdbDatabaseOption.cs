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

	/// <summary>Defines a set of options for the database connection</summary>
	[PublicAPI]
	public enum FdbDatabaseOption
	{
		/// <summary>No option defined</summary>
		None = 0,

		/// <summary>Set the size of the client location cache. Raising this value can boost performance in very large databases where clients access data in a near-random pattern. Defaults to 100000.</summary>
		/// <remarks>Parameter: (Int) Max location cache entries</remarks>
		LocationCacheSize = 10,

		/// <summary>Set the maximum number of watches allowed to be outstanding on a database connection.
		/// Increasing this number could result in increased resource usage.
		/// Reducing this number will not cancel any outstanding watches.
		/// Defaults to 10000 and cannot be larger than 1000000.</summary>
		/// <remarks>Parameter: (Int) Max outstanding watches</remarks>
		MaxWatches = 20,

		/// <summary>Specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		/// <remarks>Parameter: (String) Hexadecimal ID</remarks>
		MachineId = 21,

		/// <summary>Specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		/// <remarks>Parameter: (String) Hexadecimal ID</remarks>
		DataCenterId = 22,

		/// <summary>Snapshot read operations will see the results of writes done in the same transaction.
		/// This is the default behavior.</summary>
		SnapshotReadYourWritesEnable = 26,

		/// <summary>Snapshot read operations will not see the results of writes done in the same transaction.
		/// This was the default behavior prior to API version 300.</summary>
		SnapshotReadYourWritesDisable = 27,

		/// <summary>Sets the maximum escaped length of key and value fields to be logged to the trace file via the LOG_TRANSACTION option.
		/// This sets the <see cref="FdbTransactionOption.TransactionLoggingMaxFieldLength"/> option of each transaction created by this database.
		/// See the transaction option description for more information.</summary>
		TransactionLoggingMaxFieldLength = 405,

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause each transaction automatically to be cancelled.
		/// This sets the <see cref="FdbTransactionOption.Timeout"/> option of each transaction created by this database.
		/// See the transaction option description for more information. Using this option requires that the API version is 610 or higher.</summary>
		TransactionTimeout = 500,

		/// <summary>Set a maximum number of retries after which additional calls to ``onError`` will throw the most recently seen error code.
		/// This sets the <see cref="FdbTransactionOption.RetryLimit"/> option of each transaction created by this database.
		/// See the transaction option description for more information. Using this option requires that the API version is 610 or higher.</summary>
		TransactionRetryLimit = 501,

		/// <summary>Set the maximum amount of backoff delay incurred in the call to ``onError`` if the error is retryable.
		/// This sets the <see cref="FdbTransactionOption.MaxRetryDelay"/> option of each transaction created by this database.
		/// See the transaction option description for more information.</summary>
		TransactionMaxRetryDelay = 502,

		/// <summary>Set the maximum transaction size in bytes.
		/// This sets the <see cref="FdbTransactionOption.SizeLimit"/> option on each transaction created by this database.
		/// See the transaction option description for more information.</summary>
		TransactionSizeLimit = 503,

		/// <summary>The read version will be committed, and usually will be the latest committed, but might not be the latest committed in the event of a simultaneous fault and misbehaving clock.
		/// This sets the <see cref="FdbTransactionOption.CausalReadRisky"/> option of each transaction created by this database.
		/// </summary>
		TransactionCausalReadRisky = 504,

		/// <summary>Addresses returned by <see cref="IFdbReadOnlyTransaction.GetAddressesForKeyAsync"/> include the port when enabled.
		/// This will be enabled by default in api version 700, and this option will be deprecated.</summary>
		TransactionIncludePortInAddress = 505,

		/// <summary>Set a random idempotency id for all transactions. See the transaction option description for more information.</summary>
		/// <remarks>This feature is in development and not ready for general use.</remarks>
		TransactionAutomaticIdempotency = 506,

		/// <summary>Allows <c>get</c> operations to read from sections of keyspace that have become unreadable because of versionstamp operations. This sets the <see cref="FdbTransactionOption.BypassUnreadable"/> option of each transaction created by this database.</summary>
		TransactionBypassUnreadable = 700,

		/// <summary>Disable the protection that abort any pending operation on a transaction when at least one of them fails</summary>
		/// <remarks>
		/// By default, operations that are performed on a transaction while it is being committed will not only fail themselves, but they will attempt to fail other in-flight operations (such as the commit) as well.
		/// This behavior is intended to help developers discover situations where operations could be unintentionally executed after the transaction has been reset.
		/// Setting this option removes that protection, causing only the offending operation to fail.
		/// </remarks>
		TransactionUsedDuringCommitProtectionDisable = 701,

		/// <summary>Enables conflicting key reporting on all transactions, allowing them to retrieve the keys that are conflicting with other transactions.</summary>
		TransactionReportConflictingKeys = 702,

		/// <summary>Use configuration database.</summary>
		UseConfigDatabase = 800,

		/// <summary>Enables verification of causal read risky by checking whether clients are able to read stale data when they detect a recovery, and logging an error if so.</summary>
		/// <remarks>
		/// <para>Parameter: (Int) integer between 0 and 100 expressing the probability a client will verify it can't read stale data</para>
		/// </remarks>
		TestCausalReadRisky = 900,

	}

}
