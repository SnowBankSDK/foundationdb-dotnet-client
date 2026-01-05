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

	/// <summary>FoundationDB API Error Code</summary>
	[PublicAPI]
	public enum FdbError
	{
		/// <summary>Success</summary>
		Success = 0,

		/// <summary>Operation failed</summary>
		OperationFailed = 1000,
		/// <summary>Shard is not available from this server</summary>
		WrongShardServer = 1001,
		/// <summary>Operation result no longer necessary</summary>
		OperationObsolete = 1002,
		/// <summary>Cache server is not warm for this range</summary>
		ColdCacheServer = 1003,
		/// <summary> Operation timed out</summary>
		TimedOut = 1004,
		/// <summary>Conflict occurred while changing coordination information</summary>
		CoordinatedStateConflict = 1005,
		/// <summary>All alternatives failed</summary>
		AllAlternativesFailed = 1006,
		/// <summary>Transaction is too old to perform reads or be committed</summary>
		TransactionTooOld = 1007,
		/// <summary>Transaction is too old to perform reads or be committed.</summary>
		/// <remarks>This has been renamed to <see cref="TransactionTooOld"/>.</remarks>
		[Obsolete("Use TransactionTooOld instead!")]
		PastVersion = 1007, // previous name of TransactionTooOld
		/// <summary>Not enough physical servers available</summary>
		NoMoreServers = 1008,
		/// <summary>Request for future version</summary>
		FutureVersion = 1009,
		/// <summary>Conflicting attempts to change data distribution</summary>
		MoveKeysConflict = 1010,
		/// <summary>TLog stopped</summary>
		TlogStopped = 1011,
		/// <summary>Server request queue is full</summary>
		ServerRequestQueueFull = 1012,
		/// <summary>Transaction not committed due to conflict with another transaction</summary>
		NotCommitted = 1020,
		/// <summary>Transaction may or may not have committed</summary>
		CommitUnknownResult = 1021,
		/// <summary>Idempotency id for transaction may have expired, so the commit status of the transaction cannot be determined</summary>
		CommitUnknownResultFatal = 1022,
		/// <summary>Operation aborted because the transaction was cancelled</summary>
		TransactionCancelled = 1025,
		/// <summary>Network connection failed</summary>
		ConnectionFailed = 1026,
		/// <summary>Coordination servers have changed</summary>
		CoordinatorsChanged = 1027,
		/// <summary>New coordination servers did not respond in a timely way</summary>
		NewCoordinatorsTimedOut = 1028,
		/// <summary>Watch cancelled because storage server watch limit exceeded</summary>
		WatchCancelled = 1029,
		/// <summary>Request may or may not have been delivered</summary>
		RequestMaybeDelivered = 1030,
		/// <summary>Operation aborted because the transaction timed out</summary>
		TransactionTimedOut = 1031,
		/// <summary>Too many watches currently set</summary>
		TooManyWatches = 1032,
		/// <summary>Locality information not available</summary>
		LocalityInformationUnavailable = 1033,
		/// <summary>Watches cannot be set if read your writes is disabled</summary>
		WatchesDisabled = 1034,
		/// <summary>Default error for an ErrorOr object</summary>
		DefaultErrorOr = 1035,
		/// <summary>Read or wrote an unreadable key</summary>
		AccessedUnreadable = 1036,
		/// <summary>Storage process does not have recent mutations</summary>
		ProcessBehind = 1037,
		/// <summary>Database is locked</summary>
		DatabaseLocked = 1038,
		/// <summary>Cluster has been upgraded to a new protocol version</summary>
		ClusterVersionChanged = 1039,
		/// <summary>External client has already been loaded</summary>
		ExternalClientAlreadyLoaded = 1040,
		/// <summary>DNS lookup failed</summary>
		LookupFailed = 1041,
		/// <summary>CommitProxy commit memory limit exceeded</summary>
		ProxyMemoryLimitExceeded = 1042,
		/// <summary>Operation no longer supported due to shutdown</summary>
		ShutdownInProgress = 1043,
		/// <summary>Failed to deserialize an object</summary>
		SerializationFailed = 1044,
		/// <summary>No peer references for connection</summary>
		ConnectionUnreferenced = 1048,
		/// <summary>Connection closed after idle timeout</summary>
		ConnectionIdle = 1049,
		/// <summary>The disk queue adpater reset</summary>
		DiskAdapterReset = 1050,
		/// <summary>Batch GRV request rate limit exceeded</summary>
		BatchTransactionThrottled = 1051,
		/// <summary>Data distribution components cancelled</summary>
		DataDistributionCancelled = 1052,
		/// <summary>Data distributor not found</summary>
		DataDistributorNotFound = 1053,
		/// <summary>Connection file mismatch</summary>
		WrongConnectionFile = 1054,
		/// <summary>The requested changes have been compacted away</summary>
		VersionAlreadyCompacted = 1055,
		/// <summary>Local configuration file has changed. Restart and apply these changes</summary>
		LocalConfigChanged = 1056,
		/// <summary>Failed to reach quorum from configuration database nodes. Retry sending these requests</summary>
		FailedToReachQuorum = 1057,
		/// <summary>Format version not supported</summary>
		UnsupportedFormatVersion = 1058,
		/// <summary>Change feed not found</summary>
		ChangeFeedNotFound = 1059,
		/// <summary>Change feed not registered</summary>
		ChangeFeedNotRegistered = 1060,
		/// <summary>Conflicting attempts to assign blob granules</summary>
		BlobGranuleAssignmentConflict = 1061,
		/// <summary>Change feed was cancelled</summary>
		ChangeFeedCancelled = 1062,
		/// <summary>Error loading a blob file during granule materialization</summary>
		BlobGranuleFileLoadError = 1063,
		/// <summary>Read version is older than blob granule history supports</summary>
		BlobGranuleTransactionTooOld = 1064,
		/// <summary>This blob manager has been replaced</summary>
		BlobManagerReplaced = 1065,
		/// <summary>Tried to read a version older than what has been popped from the change feed</summary>
		ChangeFeedPopped = 1066,
		/// <summary>The remote key-value store is cancelled</summary>
		RemoveKvsCancelled = 1067,
		/// <summary>Page header does not match location on disk</summary>
		PageHeaderWrongPageId = 1068,
		/// <summary>Page header checksum failed</summary>
		PageHeaderChecksumFailed = 1069,
		/// <summary>Page header version is not supported</summary>
		PageHeaderVersionNotSupported = 1070,
		/// <summary>Page encoding type is not supported or not valid</summary>
		PageEncodingNotSupported = 1071,
		/// <summary>Page content decoding failed</summary>
		PageDecodingFailed = 1072,
		/// <summary>Page content decoding failed</summary>
		PageEncodingUnexpectedType = 1073,
		/// <summary>Encryption key not found</summary>
		EncryptionKeyNotFound = 1074,
		/// <summary>Data move was cancelled</summary>
		DataMoveCancelled = 1075,
		/// <summary>Dest team was not found for data move</summary>
		DataMoveDestTeamNotFound = 1076,
		/// <summary>Blob worker cannot take on more granule assignments</summary>
		BlobWorkerFull = 1077,
		/// <summary>GetReadVersion proxy memory limit exceeded</summary>
		GrvProxyMemoryLimitExceeded = 1078,
		/// <summary>BlobGranule request failed</summary>
		BlobGranuleRequestFailed = 1079,
		/// <summary>Too many feed streams to a single storage server</summary>
		StorageTooManyFeedStreams = 1080,
		/// <summary>Storage engine was never successfully initialized</summary>
		StorageEngineNotInitialized = 1081,
		/// <summary>Storage engine type is not recognized</summary>
		StorageEngineUnknown = 1082,
		/// <summary>A duplicate snapshot request has been sent, the old request is discarded</summary>
		DuplicateSnapshotRequest = 1083,
		/// <summary>DataDistribution configuration changed</summary>
		DataDistributionConfigChanged = 1084,

		/// <summary>Broken Promise</summary>
		BrokenPromise = 1100,
		/// <summary>Asynchronous operation cancelled</summary>
		OperationCancelled = 1101,
		/// <summary>Future has been released</summary>
		FutureReleased = 1102,
		/// <summary>Connection object leaked</summary>
		ConnectionLeaked = 1103,
		/// <summary>Never reply to the request</summary>
		NeverReply = 1104,
		/// <summary>Retry operation</summary>
		RetryOperation = 1105,

		/// <summary>Recruitment of a server failed</summary>
		RecruitmentFailed = 1200,
		/// <summary>Attempt to move keys to a storage server that was removed</summary>
		MoveToRemovedServer = 1201,
		/// <summary>Normal worker shut down</summary>
		WorkerRemoved = 1202,
		/// <summary>Cluster recovery failed</summary>
		ClusterRecoveryFailed = 1203,
		/// <summary>Master hit maximum number of versions in flight</summary>
		MasterMaxVersionsInFlight = 1204,
		/// <summary>Cluster recovery terminating because a TLog failed</summary>
		TlogFailed = 1205,
		/// <summary>Recovery of a worker process failed</summary>
		WorkerRecoveryFailed = 1206,
		/// <summary>Reboot of server process requested</summary>
		PleaseReboot = 1207,
		/// <summary>Reboot of server process requested, with deletion of state</summary>
		PleaseRebootDelete = 1208,
		/// <summary>Master terminating because a CommitProxy failed</summary>
		CommitProxyFailed = 1209,
		/// <summary>Cluster recovery terminating because a Resolver failed</summary>
		ResolverFailed = 1210,
		/// <summary>Server is under too much load and cannot respond</summary>
		ServerOverloaded = 1211,
		/// <summary>Cluster recovery terminating because a backup worker failed</summary>
		BackupWorkerFailed = 1212,
		/// <summary>Transaction tag is being throttled</summary>
		TagThrottled = 1213,
		/// <summary>Cluster recovery terminating because a GRVProxy failed</summary>
		GrvProxyFailed = 1214,
		/// <summary>The data distribution tracker has been cancelled</summary>
		DataDistributionTrackerCancelled = 1215,
		/// <summary>Process has failed to make sufficient progress</summary>
		FailedToProgress = 1216,
		/// <summary>Attempted to join cluster with a different cluster ID</summary>
		InvalidClusterId = 1217,
		/// <summary>Restart cluster controller process</summary>
		RestartClusterController = 1218,
		/// <summary>Need to reboot the storage engine</summary>
		PleaseRebootKvStore = 1219,
		/// <summary>Current software does not support database format</summary>
		IncompatibleSoftwareVersion = 1220,
		/// <summary>Validate storage consistency operation failed</summary>
		AuditStorageFailed = 1221,
		/// <summary>Exceeded the max number of allowed concurrent audit storage requests</summary>
		AuditStorageExceededRequestLimit = 1222,
		/// <summary>Exceeded maximum proxy tag throttling duration</summary>
		ProxyTagThrottled = 1223,
		/// <summary>Exceeded maximum time allowed to read or write</summary>
		KeyValueStoreDeadlineExceeded = 1224,
		/// <summary>Exceeded the maximum storage quota allocated to the tenant</summary>
		StorageQuotaExceeded = 1225,
		/// <summary>Found data corruption</summary>
		AuditStorageError = 1226,
		/// <summary>Cluster recovery terminating because master has failed</summary>
		MasterFailed = 1227,
		/// <summary>Test failed</summary>
		TestFailed = 1228,
		/// <summary>Need background datamove cleanup</summary>
		RetryCleanUpDatamoveTombstoneAdded = 1229,
		/// <summary>Persist new audit metadata error</summary>
		PersistNewAuditMetadataError = 1230,

		/// <summary>Platform error</summary>
		PlatformError = 1500,
		/// <summary>Large block allocation failed</summary>
		LargeAllocFailed = 1501,
		/// <summary>QueryPerformanceCounter error</summary>
		PerformanceCounterError = 1502,
		/// <summary>Null allocator was used to allocate memory</summary>
		BadAllocator = 1503,

		/// <summary>Disk i/o operation failed</summary>
		IoError = 1510, //note: was spelled "IOError" previously!
		/// <summary>File not found</summary>
		FileNotFound = 1511,
		/// <summary>Unable to bind to network</summary>
		BindFailed = 1512,
		/// <summary>File could not be read from</summary>
		FileNotReadable = 1513,
		/// <summary>File could not be written to</summary>
		FileNotWriteable = 1514,
		/// <summary>No cluster file found in current directory or default location</summary>
		NoClusterFileFound = 1515,
		/// <summary>Cluster file to large to be read</summary>
		ClusterFileTooLarge = 1516,
		/// <summary>Non sequential file operation not allowed</summary>
		NonSequentialOp = 1517,
		/// <summary>HTTP response was badly formed</summary>
		HttpBadResponse = 1518,
		/// <summary>HTTP request not accepted</summary>
		HttpNotAccepted = 1519,
		/// <summary>A data checksum failed</summary>
		ChecksumFailed = 1520,
		/// <summary>A disk IO operation failed to complete in a timely manner</summary>
		IoTimeout = 1521,
		/// <summary>A structurally corrupt data file was detected</summary>
		FileCorrupt = 1522,
		/// <summary>HTTP response code not received or indicated failure</summary>
		HttpRequestFailed = 1523,
		/// <summary>HTTP request failed due to bad credentials</summary>
		HttpAuthFailed = 1524,
		/// <summary>HTTP response contained an unexpected X-Request-ID header</summary>
		HttpBadRequestId = 1525,
		/// <summary>Invalid REST URI</summary>
		RestInvalidUri = 1526,
		/// <summary>Invalid RESTClient knob</summary>
		RestInvalidRestClientKnob = 1527,
		/// <summary>ConnectKey not found in connection pool</summary>
		RestConnectPoolKeyNotFound = 1528,
		/// <summary>Unable to lock the file</summary>
		LockFileFailure = 1529,
		/// <summary>Unsupported REST protocol</summary>
		RestUnsupportedProtocol = 1530,
		/// <summary>Malformed REST response</summary>
		RestMalformedResponse = 1531,
		/// <summary>Max BaseCipher length violation</summary>
		RestMaxBaseCipherLen = 1532,

		/// <summary>The client made an invalid API call</summary>
		ClientInvalidOperation = 2000,
		/// <summary>Commit with incomplete read</summary>
		CommitReadIncomplete = 2002,
		/// <summary>The test specification is invalid</summary>
		TestSpecificationInvalid = 2003,
		/// <summary>The specified key was outside the legal range</summary>
		KeyOutsideLegalRange = 2004,
		/// <summary>The specified range has a begin key larger than the end key</summary>
		InvertedRange = 2005,
		/// <summary>An invalid value was passed with the specified option</summary>
		InvalidOptionValue = 2006,
		/// <summary>Option not valid in this context</summary>
		InvalidOption = 2007,
		/// <summary>Action not possible before the network is configured</summary>
		NetworkNotSetup = 2008,
		/// <summary>Network can be configured only once</summary>
		NetworkAlreadySetup = 2009,
		/// <summary>Transaction already has a read version set</summary>
		ReadVersionAlreadySet = 2010,
		/// <summary>Version not valid</summary>
		VersionInvalid = 2011,
		/// <summary>getRange limits not valid</summary>
		RangeLimitsInvalid = 2012,
		/// <summary>Database name not supported in this version</summary>
		InvalidDatabaseName = 2013,
		/// <summary>Attribute not found in string</summary>
		AttributeNotFound = 2014,
		/// <summary>The future has not been set</summary>
		FutureNotSet = 2015,
		/// <summary>The future is not an error</summary>
		FutureNotError = 2016,
		/// <summary>An operation was issued while a commit was outstanding</summary>
		UsedDuringCommit = 2017,
		/// <summary>An invalid atomic mutation type was issued</summary>
		InvalidMutationType = 2018,
		/// <summary>Transaction does not have a valid commit version</summary>
		TransactionInvalidVersion = 2020,
		/// <summary>Transaction is read-only and therefore does not have a commit version</summary>
		NoCommitVersion = 2021,
		/// <summary>Environment variable network option could not be set</summary>
		EnvironmentVariableNetworkOptionFailed = 2022,
		/// <summary>Attempted to commit a transaction specified as read-only</summary>
		TransactionReadOnly = 2023,
		/// <summary>Invalid cache eviction policy, only <c>random</c> and <c>lru</c> are supported</summary>
		InvalidCacheEvictionPolicy = 2024,
		/// <summary>Network can onlyt be started once</summary>
		NetworkCannotBeRestarted = 2025,
		/// <summary>Detected a deadlock in a callback called from the network thread</summary>
		BlockedFromNetworkThread = 2026,
		/// <summary>Invalid configuration database range read</summary>
		InvalidConfigDbRangeRead = 2027,
		/// <summary>Invalid configuration database key provided</summary>
		InvalidConfigDbKey = 2028,
		/// <summary>Invalid configuration path</summary>
		InvalidConfigPath = 2029,
		/// <summary>The index in K[] or V[] is not a valid number or out of range</summary>
		MapperBadIndex = 2030,
		/// <summary>A mapped key is not set in database</summary>
		MapperNoSuchKey = 2031,
		/// <summary><c>"{...}"</c> must be the last element of the mapper tuple</summary>
		MapperBadRangeDescriptor = 2032,
		/// <summary>One of the mapped range queries is too large</summary>
		QuickGetKeyValuesHasMore = 2033,
		/// <summary>Found a mapped key that is not served in the same SS</summary>
		QuickGetValueMiss = 2034,
		/// <summary>Found a mapped range that is not served in the same SS</summary>
		QuickGetKeyValuesMiss = 2035,
		/// <summary>Blob Granule Read Transactions must be specified as ryw-disabled</summary>
		BlobGranuleNoRyw = 2036,
		/// <summary>Blob Granule Read was not materialized</summary>
		BlobGranuleNotMaterialized = 2037,
		/// <summary>getMappedRange does not support continuation for now</summary>
		GetMappedKeyValuesHasMore = 2038,
		/// <summary>getMappedRange tries to read data that were previously written in the transaction</summary>
		GetMappedRangeReadsYourWrites = 2039,
		/// <summary>Checkpoint not found</summary>
		CheckpointNotFound = 2040,
		/// <summary>The key cannot be parsed as a tuple</summary>
		KeyNotTuple = 2041,
		/// <summary>The value cannot be parsed as a tuple</summary>
		ValueNotTuple = 2042,
		/// <summary>The mapper cannot be parsed as a tuple</summary>
		MapperNotTuple = 2043,
		/// <summary>Invalid checkpoint forma</summary>
		InvalidCheckpointFormat = 2044,
		/// <summary>Invalid quota value. Note that reserved_throughput cannot exceed total_throughput</summary>
		InvalidThrottleQuotaValue = 2045,
		/// <summary>Failed to create a checkpoint</summary>
		FailedToCreateCheckpoint = 2046,
		/// <summary>Failed to restore a checkpoint</summary>
		FailedToRestoreCheckpoint = 2047,
		/// <summary>Failed to dump shard metadata for a checkpoint to a sst file</summary>
		FailedToCreateCheckpointShardMetadata = 2048,
		/// <summary>Failed to parse address</summary>
		AddressParseError = 2049,

		/// <summary>Incompatible protocol version</summary>
		IncompatibleProtocolVersion = 2100,
		/// <summary>Transaction too large</summary>
		TransactionTooLarge = 2101,
		/// <summary>Key too large</summary>
		KeyTooLarge = 2102,
		/// <summary>Value too large</summary>
		ValueTooLarge = 2103,
		/// <summary>Connection string invalid</summary>
		ConnectionStringInvalid = 2104,
		/// <summary>Local address in use</summary>
		AddressInUse = 2105,
		/// <summary>Invalid local address</summary>
		InvalidLocalAddress = 2106,
		/// <summary>TLS error</summary>
		TlsError = 2107,
		/// <summary>Operation is not supported</summary>
		UnsupportedOperation = 2108,
		/// <summary>Too many tags set on transaction</summary>
		TooManyTags = 2109,
		/// <summary>Tag set on transaction is too long</summary>
		TagTooLong = 2110,
		/// <summary>Too many tag throttles have been created</summary>
		TooManyTagThrottles = 2111,
		/// <summary>Special key space range read crosses modules.</summary>
		/// <remarks>Refer to the <see cref="FdbTransactionOption.SpecialKeySpaceRelaxed"/> transaction option for more details.</remarks>
		SpecialKeysCrossModuleRead = 2112,
		/// <summary>Special key space range read does not intersect a module.</summary>
		/// <remarks>Refer to the <see cref="FdbTransactionOption.SpecialKeySpaceRelaxed"/> transaction option for more details.</remarks>
		SpecialKeysNoModuleFound = 2113,
		/// <summary>Special key space is not allowed to write by default.</summary>
		/// <remarks>Refer to the <see cref="FdbTransactionOption.SpecialKeySpaceEnableWrites"/> transaction option for more details.</remarks>
		SpecialKeysWriteDisabled = 2114,
		/// <summary>Special key space key or keyrange in set or clear does not intersect a module.</summary>
		SpecialKeysNoWriteModuleFound = 2115,
		/// <summary>Special key space clear crosses modules</summary>
		SpecialKeysCrossModuleWrite = 2116,
		/// <summary>Api call through special keys failed.</summary>
		/// <remarks>For more information, read the <c>0xff0xff/error_message</c> key</remarks>
		SpecialKeysApiFailure = 2117,
		/// <summary>Invalid client library metadata</summary>
		ClientLibInvalidMetadata  = 2118,
		/// <summary>Client library with same identifier already exists on the cluster</summary>
		ClientLibAlreadyExists = 2119,
		/// <summary>Client library for the given identifier not found</summary>
		ClientLibNotFound = 2120,
		/// <summary>Client library exists, but is not available for download</summary>
		ClientLibNotAvailable = 2121,
		/// <summary>Invalid client library binary</summary>
		ClientLibInvalidBinary = 2122,
		/// <summary>No external client library provided</summary>
		NoExternalClientProvided = 2123,
		/// <summary>All external clients have failed</summary>
		AllExternalClientsFailed = 2124,
		/// <summary>None of the available clients match the protocol version of the cluster</summary>
		IncompatibleClient = 2125,

		/// <summary>Tenant name must be specified to access data in the cluster</summary>
		TenantNameRequired = 2130,
		/// <summary>Tenant does not exist</summary>
		TenantNotFound = 2131,
		/// <summary>A tenant with the given name already exists</summary>
		TenantAlreadyExists = 2132,
		/// <summary>Cannot delete a non-empty tenant</summary>
		TenantNotEmpty = 2133,
		/// <summary>Tenant name cannot begin with <c>\xff</c></summary>
		InvalidTenantName = 2134,
		/// <summary>The database already has keys stored at the prefix allocated for the tenant</summary>
		TenantPrefixAllocatorConflict = 2135,
		/// <summary>Tenants have been disabled in the cluster</summary>
		TenantsDisabled = 2136,
		/// <summary>Tenant is not available from this server</summary>
		UnknownTenant = 2137, //note: not present in error_definitions.h ?
		/// <summary>Illegal tenant access</summary>
		IllegalTenantAccess = 2138,
		/// <summary>Tenant group name cannot begin with <c>\xff</c></summary>
		InvalidTenantGroupName = 2139,
		/// <summary>Tenant configuration is invalid</summary>
		InvalidTenantConfiguration = 2140,
		/// <summary>Cluster does not have capacity to perform the specified operation</summary>
		ClusterNoCapacity = 2141,
		/// <summary>The tenant was removed</summary>
		TenantRemoved = 2142,
		/// <summary>Operation cannot be applied to tenant in its current state</summary>
		InvalidTenantState = 2143,
		/// <summary>Tenant is locked</summary>
		TenantLocked = 2144,

		/// <summary>Data cluster name cannot begin with <c>\xff</c></summary>
		InvalidClusterName = 2160,
		/// <summary>Metacluster operation performed on non-metacluster</summary>
		InvalidMetaClusterOperation = 2161,
		/// <summary>"A data cluster with the given name already exists</summary>
		ClusterAlreadyExists = 2162,
		/// <summary>Data cluster does not exist</summary>
		ClusterNotFound = 2163,
		/// <summary>Cluster must be empty</summary>
		ClusterNotEmpty = 2164,
		/// <summary>Data cluster is already registered with a metacluster</summary>
		ClusterAlreadyRegistered = 2165,
		/// <summary>Metacluster does not have capacity to create new tenants</summary>
		MetaClusterNoCapacity = 2166,
		/// <summary>Standard transactions cannot be run against the management cluster</summary>
		ManagementClusterInvalidAccess = 2167,
		/// <summary>The tenant creation did not complete in a timely manner and has permanently failed</summary>
		TenantCreationPermanentlyFailed = 2168,
		/// <summary>The cluster is being removed from the metacluster</summary>
		ClusterRemoved = 2169,
		/// <summary>The cluster is being restored to the metacluster</summary>
		ClusterRestoring = 2170,
		/// <summary>The data cluster being restored has no record of its metacluster</summary>
		InvalidDataCluster = 2171,
		/// <summary>The cluster does not have the expected name or is associated with a different metacluster</summary>
		MetaClusterMismatch = 2172,
		/// <summary>Another restore is running for the same data cluster</summary>
		ConflictingRestore = 2173,
		/// <summary>Metacluster configuration is invalid</summary>
		InvalidMetaClusterConfiguration = 2174,
		/// <summary>Client is not compatible with the metacluster</summary>
		UnsupportedMetaClusterVersion = 2175,

		/// <summary>Api version must be set</summary>
		ApiVersionUnset = 2200,
		/// <summary>Api version may be set only once</summary>
		ApiVersionAlreadySet = 2201,
		/// <summary>Api version not valid</summary>
		ApiVersionInvalid = 2202,
		/// <summary>Api version not supported in this version or binding</summary>
		ApiVersionNotSupported = 2203,
		/// <summary>Failed to load a required FDB API function</summary>
		ApiFunctionMissing = 2204,
		/// <summary>EXACT streaming mode requires limits, but none were given</summary>
		ExactModeWithoutLimits = 2210,

		InvalidTupleDataType = 2250,
		InvalidTupleIndex = 2251,
		KeyNotInSubspace = 2252,
		ManualPrefixesNotEnabled = 2253,
		PrefixInPartition = 2254,
		CannotOpenRootDirectory = 2255,
		DirectoryAlreadyExists = 2256,
		DirectoryDoesNotExist = 2257,
		ParentDirectoryDoesNotExist = 2258,
		MismatchedLayer = 2259,
		InvalidDirectoryLayerMetadata = 2260,
		CannotMoveDirectoryBetweenPartitions = 2261,
		CannotUsePartitionAsSubspace = 2262,
		IncompatibleDirectoryVersion = 2263,
		DirectoryPrefixNotEmpty = 2264,
		DirectoryPrefixInUse = 2265,
		InvalidDestinationDirectory = 2266,
		CannotModifyRootDirectory = 2267,
		InvalidUuidSize = 2268,
		InvalidVersionStampSize = 2269,

		BackupError = 2300,
		RestoreError = 2301,
		BackupDuplicate = 2311,
		BackupUneeded = 2312,
		BackupBadBlockSize = 2313,
		BackupInvalidUrl = 2314,
		BackupInvalidInfo = 2315,
		BackupCannotExpire = 2316,
		BackupAuthMissing = 2317,
		BackupAuthUnreadable = 2318,
		BackupDoesNotExist = 2319,
		BackupNotFilterableWithKeyRanges = 2320,
		BackupNotOverlappedWithKeysFilter = 2321,
		RestoreInvalidVersion = 2361,
		RestoreCorruptedData = 2362,
		RestoreMissingData = 2363,
		RestoreDuplicateTag = 2364,
		RestoreUnknownTag = 2365,
		RestoreUnknownFileType = 2366,
		RestoreUnsupportedFileVersion = 2367,
		RestoreBadRead = 2368,
		RestoreCorruptedDataPadding = 2369,
		RestoreDestinationNotEmpty = 2370,
		RestoreDuplicateUuid = 2371,
		TaskInvalidVersion = 2381,
		TaskInterrupted = 2382,
		InvalidEncryptionKeyFile = 2383,
		BlobRestoreMissingLogs = 2384,
		BlobRestoreCorruptedLogs = 2385,
		BlobRestoreInvalidManifestUrl = 2386,
		BlobRestoreCorruptedManifest = 2387,
		BlobRestoreMissingManifest = 2388,
		BlobMigratorReplaced = 2389,

		/// <summary>Expected key is missing</summary>
		KeyNotFound = 2400,
		/// <summary>JSON string was malformed</summary>
		JsonMalformed = 2401,
		/// <summary>JSON string did not terminate where expected</summary>
		JsonEofExpected = 2402,

		SnapshotDisableTlogPopFailed = 2500,
		SnapshotStorageFailed = 2501,
		SnapshotTlogFailed = 2502,
		SnapshotCoordinatorFailed = 2503,
		SnapshotEnableTlogPopFailed = 2504,
		SnapshotPathNotWhitelisted = 2505,
		SnapshotNotFullyRecoveredUnsupported = 2506,
		SnapshotLogAntiQuorumUnsupported = 2507,
		SnapshotWithRecoveryUnsupported = 2508,
		SnapshotInvalidUidString = 2509,

		EncryptOpsError = 2700,
		EncryptHeaderMetadataMismatch = 2701,
		EncryptKeyNotFound = 2702,
		EncryptKeyTtlExpired = 2703,
		EncryptHeaderAuthTokenMismatch = 2704,
		EncryptUpdateCipher = 2705,
		EncryptInvalidId = 2706,
		EncryptKeysFetchFailed = 2707,
		EncryptInvalidKmsConfig = 2708,
		EncryptUnsupported = 2709,
		EncryptModeMismatch = 2710,
		EncryptKeyCheckValueMismatch = 2711,
		EncryptMaxBaseCipherLen = 2712,

		/// <summary>An unknown error occurred</summary>
		UnknownError = 4000,

		/// <summary>An internal error occurred</summary>
		InternalError = 4100,

		/// <summary>Not implemented yet</summary>
		NotImplemented = 4200,

		/// <summary>Client tried to access unauthorized data</summary>
		PermissionDenied = 6000,
		/// <summary>A untrusted client tried to send a message to a private endpoint</summary>
		UnauthorizedAttempt = 6001,
		/// <summary>Digital signature operation error</summary>
		DigitalSignatureOpsError = 6002,
		/// <summary>Failed to verify authorization token</summary>
		AuthorizationTokenVerifyFailed = 6003,
		/// <summary>Failed to decode public/private key</summary>
		PkeyDecodeError = 6004,
		/// <summary>Failed to encode public/private key</summary>
		PkeyEncodeError = 6005,
	}

}
