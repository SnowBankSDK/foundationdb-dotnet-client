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

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace FoundationDB.Client
{
	using System.ComponentModel;

	/// <summary>Represents the protocol version of a node, interface or service in a FoundationDB cluster.</summary>
	[DebuggerDisplay("Version={Version}")]
	[PublicAPI]
	public readonly struct FdbProtocolVersion : IEquatable<FdbProtocolVersion>, IEquatable<string>, IEquatable<ulong>, ISpanFormattable, ISpanParsable<FdbProtocolVersion>
	{

		private const ulong VERSION_FLAG_MASK = 0x0FFFFFFFFFFFFFFFUL;
		private const ulong OBJECT_SERIALIZER_FLAG = 0x1000000000000000UL;
		private const ulong COMPATIBLE_PROTOCOL_VERSION_MASK = 0xFFFFFFFFFFFF0000UL;
		private const ulong MIN_VALID_PROTOCOL_VERSION = 0x0FDB00A200060001UL;
		private const ulong INVALID_PROTOCOL_VERSION = 0x0FDB00A100000000UL;

		/// <summary>Represents a missing version (empty string, or <c>0</c>)</summary>
		public static readonly FdbProtocolVersion None = new(0);

		/// <summary>Represents an invalid protocol version (<c>0FDB00A100000000</c>)</summary>
		public static readonly FdbProtocolVersion Invalid = new(INVALID_PROTOCOL_VERSION);

		public FdbProtocolVersion(ulong value)
		{
			this.Value = value;
		}

		/// <summary>Raw value that combines the version and flags</summary>
		private readonly ulong Value;

		/// <summary>Returns a normalized protocol version that will be the same for all compatible versions</summary>
		public FdbProtocolVersion GetNormalizedVersion() => new(this.Value & COMPATIBLE_PROTOCOL_VERSION_MASK);

		public bool IsValid() => this.Version >= (long) MIN_VALID_PROTOCOL_VERSION;

		public bool IsInvalid() => this.Version == (long) INVALID_PROTOCOL_VERSION;

		/// <summary>Returns the version of this protocol</summary>
		/// <remarks>This valid does not include the flags</remarks>
		public long Version => unchecked((long) (this.Value & VERSION_FLAG_MASK));

		/// <summary>Returns the raw value of this protocol version, including the flags</summary>
		public ulong VersionWithFlags => this.Value;

		/// <summary>Returns <c>true</c> if this protocol version has the Object Serializer flag set.</summary>
		public bool HasObjectSerializerFlag() => (this.Value & OBJECT_SERIALIZER_FLAG) != 0;

		/// <summary>Returns a version of this protocol with the Object Serializer flag set</summary>
		public FdbProtocolVersion WithObjectSerializerFlag() => new(this.Value | OBJECT_SERIALIZER_FLAG);

		/// <summary>Returns a version of this protocol without the Object Serializer flag</summary>
		public FdbProtocolVersion WithoutObjectSerializerFlag() => new(this.Value & ~OBJECT_SERIALIZER_FLAG);

		/// <summary>Extracts the version number from the protocol number</summary>
		/// <returns>Ex: <c>1fdb00b074000000</c> => <c>7.4</c></returns>
		public Version ToVersion()
		{
			// 0x________XYZR____ => X.Y.Z.R
			int bits = unchecked((int) ((this.Value >> 16) & 0xFFFF));
			var major = bits >> 12;
			var minor = (bits >> 8) & 0xF;
			var build = (bits >> 4) & 0xF;
			var rev = bits & 0xF;
			return rev != 0 ? new Version(major, minor, build, rev)
				: build != 0 ? new Version(major, minor, build)
				: new Version(major, minor);
		}

		#region Formatting...

		public override string ToString() => ToString(null);

		public string ToString(string? format, IFormatProvider? provider = null) => this.Value != 0 ? this.Value.ToString("x016") : "";

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> this.Value.TryFormat(destination, out charsWritten, "x016", provider ?? CultureInfo.InvariantCulture);

		public static FdbProtocolVersion Parse(string? s, IFormatProvider? provider = null)
		{
			return !string.IsNullOrEmpty(s)
				? new(ulong.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
				: None;
		}

		public static FdbProtocolVersion Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
		{
			return s.Length != 0
				? new(ulong.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
				: None;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(string? s, IFormatProvider? provider, out FdbProtocolVersion result)
			=> TryParse(s, out result);

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FdbProtocolVersion result)
			=> TryParse(s, out result);

		public static bool TryParse(ReadOnlySpan<char> s, out FdbProtocolVersion result)
		{
			if (s.Length == 0)
			{
				result = None;
				return true;
			}

			if (!ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
			{
				result = default;
				return false;
			}
			result = new(value);
			return true;
		}

		#endregion

		#region Equality...

		//note: like the C++ implementation, all comparisons only look at the version number, and exclude any flags.

		public override int GetHashCode() => this.Version.GetHashCode();

		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			FdbProtocolVersion pv => Equals(pv),
			string s              => Equals(s),
			_                     => false,
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbProtocolVersion other) => this.Version == other.Version;

		public bool Equals(string? s) => s is not null && ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) && (this.Value & VERSION_FLAG_MASK) == value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ulong v) => (this.Value & VERSION_FLAG_MASK) == v;

		public static bool operator ==(FdbProtocolVersion left, FdbProtocolVersion right) => left.Version == right.Version;

		public static bool operator !=(FdbProtocolVersion left, FdbProtocolVersion right) => left.Version != right.Version;

		public static bool operator >(FdbProtocolVersion left, FdbProtocolVersion right) => left.Version > right.Version;

		public static bool operator >=(FdbProtocolVersion left, FdbProtocolVersion right) => left.Version >= right.Version;

		public static bool operator <(FdbProtocolVersion left, FdbProtocolVersion right) => left.Version < right.Version;

		public static bool operator <=(FdbProtocolVersion left, FdbProtocolVersion right) => left.Version <= right.Version;

		#endregion

	}

	[PublicAPI]
	public static class FdbProtocolVersionExtensions
	{

		#region Feature Support Map

		extension(FdbProtocolVersion pv)
		{

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>Watches</c> feature</summary>
			public static FdbProtocolVersion Watches => new(FdbProtocolVersionMap.FDB_PV_WATCHES);
			/// <summary>Tests if this version supports the <c>Watches</c> feature</summary>
			public bool SupportsWatches() => pv.Version >= FdbProtocolVersionMap.FDB_PV_WATCHES;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>MovableCoordinatedState</c> (v1) feature</summary>
			public static FdbProtocolVersion MovableCoordinatedState => new(FdbProtocolVersionMap.FDB_PV_MOVABLE_COORDINATED_STATE);
			/// <summary>Tests if this version supports the <c>MovableCoordinatedState</c> (v1) feature</summary>
			public bool SupportsMovableCoordinatedState() => pv.Version >= FdbProtocolVersionMap.FDB_PV_MOVABLE_COORDINATED_STATE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ProcessId</c> feature</summary>
			public static FdbProtocolVersion ProcessId => new(FdbProtocolVersionMap.FDB_PV_PROCESS_ID);
			/// <summary>Tests if this version supports the <c>ProcessId</c> feature</summary>
			public bool SupportsProcessId() => pv.Version >= FdbProtocolVersionMap.FDB_PV_PROCESS_ID;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>OpenDatabase</c> feature</summary>
			public static FdbProtocolVersion OpenDatabase => new(FdbProtocolVersionMap.FDB_PV_OPEN_DATABASE);
			/// <summary>Tests if this version supports the <c>OpenDatabase</c> feature</summary>
			public bool SupportsOpenDatabase() => pv.Version >= FdbProtocolVersionMap.FDB_PV_OPEN_DATABASE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>Locality</c> feature</summary>
			public static FdbProtocolVersion Locality => new(FdbProtocolVersionMap.FDB_PV_LOCALITY);
			/// <summary>Tests if this version supports the <c>Locality</c> feature</summary>
			public bool SupportsLocality() => pv.Version >= FdbProtocolVersionMap.FDB_PV_LOCALITY;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>MultiGenerationTLog</c> feature</summary>
			public static FdbProtocolVersion MultiGenerationTLog => new(FdbProtocolVersionMap.FDB_PV_MULTIGENERATION_TLOG);
			/// <summary>Tests if this version supports the <c>MultiGenerationTLog</c> feature</summary>
			public bool SupportsMultiGenerationTLog() => pv.Version >= FdbProtocolVersionMap.FDB_PV_MULTIGENERATION_TLOG;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>SharedMutations</c> feature</summary>
			public static FdbProtocolVersion SharedMutations => new(FdbProtocolVersionMap.FDB_PV_SHARED_MUTATIONS);
			/// <summary>Tests if this version supports the <c>SharedMutations</c> feature</summary>
			public bool SupportsSharedMutations() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SHARED_MUTATIONS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>InexpensiveMultiVersionClient</c> feature</summary>
			public static FdbProtocolVersion InexpensiveMultiVersionClient => new(FdbProtocolVersionMap.FDB_PV_INEXPENSIVE_MULTIVERSION_CLIENT);
			/// <summary>Tests if this version supports the <c>InexpensiveMultiVersionClient</c> feature</summary>
			public bool SupportsInexpensiveMultiVersionClient() => pv.Version >= FdbProtocolVersionMap.FDB_PV_INEXPENSIVE_MULTIVERSION_CLIENT;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TagLocality</c> feature</summary>
			public static FdbProtocolVersion TagLocality => new(FdbProtocolVersionMap.FDB_PV_TAG_LOCALITY);
			/// <summary>Tests if this version supports the <c>TagLocality</c> feature</summary>
			public bool SupportsTagLocality() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TAG_LOCALITY;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>Fearless</c> feature</summary>
			public static FdbProtocolVersion Fearless => new(FdbProtocolVersionMap.FDB_PV_FEARLESS);
			/// <summary>Tests if this version supports the <c>Fearless</c> feature</summary>
			public bool SupportsFearless() => pv.Version >= FdbProtocolVersionMap.FDB_PV_FEARLESS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>EndpointAddrList</c> feature</summary>
			public static FdbProtocolVersion EndpointAddrList => new(FdbProtocolVersionMap.FDB_PV_ENDPOINT_ADDR_LIST);
			/// <summary>Tests if this version supports the <c>EndpointAddrList</c> feature</summary>
			public bool SupportsEndpointAddrList() => pv.Version >= FdbProtocolVersionMap.FDB_PV_ENDPOINT_ADDR_LIST;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>IPv6</c> feature</summary>
			public static FdbProtocolVersion IPv6 => new(FdbProtocolVersionMap.FDB_PV_IPV6);
			/// <summary>Tests if this version supports the <c>IPv6</c> feature</summary>
			public bool SupportsIPv6() => pv.Version >= FdbProtocolVersionMap.FDB_PV_IPV6;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TLogVersion</c> feature</summary>
			public static FdbProtocolVersion TLogVersion => new(FdbProtocolVersionMap.FDB_PV_TLOG_VERSION);
			/// <summary>Tests if this version supports the <c>TLogVersion</c> feature</summary>
			public bool SupportsTLogVersion() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TLOG_VERSION;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>PseudoLocalities</c> feature</summary>
			public static FdbProtocolVersion PseudoLocalities => new(FdbProtocolVersionMap.FDB_PV_PSEUDO_LOCALITIES);
			/// <summary>Tests if this version supports the <c>PseudoLocalities</c> feature</summary>
			public bool SupportsPseudoLocalities() => pv.Version >= FdbProtocolVersionMap.FDB_PV_PSEUDO_LOCALITIES;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ShardedTxsTags</c> feature</summary>
			public static FdbProtocolVersion ShardedTxsTags => new(FdbProtocolVersionMap.FDB_PV_SHARDED_TXS_TAGS);
			/// <summary>Tests if this version supports the <c>ShardedTxsTags</c> feature</summary>
			public bool SupportsShardedTxsTags() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SHARDED_TXS_TAGS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TLogQueueEntryRef</c> feature</summary>
			public static FdbProtocolVersion TLogQueueEntryRef => new(FdbProtocolVersionMap.FDB_PV_TLOG_QUEUE_ENTRY_REF);
			/// <summary>Tests if this version supports the <c>TLogQueueEntryRef</c> feature</summary>
			public bool SupportsTLogQueueEntryRef() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TLOG_QUEUE_ENTRY_REF;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>GenerationRegVal</c> feature</summary>
			public static FdbProtocolVersion GenerationRegVal => new(FdbProtocolVersionMap.FDB_PV_GENERATION_REG_VAL);
			/// <summary>Tests if this version supports the <c>GenerationRegVal</c> feature</summary>
			public bool SupportsGenerationRegVal() => pv.Version >= FdbProtocolVersionMap.FDB_PV_GENERATION_REG_VAL;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>MovableCoordinatedStateV2</c> feature</summary>
			public static FdbProtocolVersion MovableCoordinatedStateV2 => new(FdbProtocolVersionMap.FDB_PV_MOVABLE_COORDINATED_STATE_V2);
			/// <summary>Tests if this version supports the <c>MovableCoordinatedStateV2</c> feature</summary>
			public bool SupportsMovableCoordinatedStateV2() => pv.Version >= FdbProtocolVersionMap.FDB_PV_MOVABLE_COORDINATED_STATE_V2;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>KeyServerValue</c> (v1) feature</summary>
			public static FdbProtocolVersion KeyServerValue => new(FdbProtocolVersionMap.FDB_PV_KEY_SERVER_VALUE);
			/// <summary>Tests if this version supports the <c>KeyServerValue</c> (v1) feature</summary>
			public bool SupportsKeyServerValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_KEY_SERVER_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>LogsValue</c> feature</summary>
			public static FdbProtocolVersion LogsValue => new(FdbProtocolVersionMap.FDB_PV_LOGS_VALUE);
			/// <summary>Tests if this version supports the <c>LogsValue</c> feature</summary>
			public bool SupportsLogsValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_LOGS_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ServerTagValue</c> feature</summary>
			public static FdbProtocolVersion ServerTagValue => new(FdbProtocolVersionMap.FDB_PV_SERVER_TAG_VALUE);
			/// <summary>Tests if this version supports the <c>ServerTagValue</c> feature</summary>
			public bool SupportsServerTagValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SERVER_TAG_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TagLocalityListValue</c> feature</summary>
			public static FdbProtocolVersion TagLocalityListValue => new(FdbProtocolVersionMap.FDB_PV_TAG_LOCALITY_LIST_VALUE);
			/// <summary>Tests if this version supports the <c>TagLocalityListValue</c> feature</summary>
			public bool SupportsTagLocalityListValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TAG_LOCALITY_LIST_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>DatacenterReplicasValue</c> feature</summary>
			public static FdbProtocolVersion DatacenterReplicasValue => new(FdbProtocolVersionMap.FDB_PV_DATACENTER_REPLICAS_VALUE);
			/// <summary>Tests if this version supports the <c>DatacenterReplicasValue</c> feature</summary>
			public bool SupportsDatacenterReplicasValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_DATACENTER_REPLICAS_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ProcessClassValue</c> feature</summary>
			public static FdbProtocolVersion ProcessClassValue => new(FdbProtocolVersionMap.FDB_PV_PROCESS_CLASS_VALUE);
			/// <summary>Tests if this version supports the <c>ProcessClassValue</c> feature</summary>
			public bool SupportsProcessClassValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_PROCESS_CLASS_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>WorkerListValue</c> feature</summary>
			public static FdbProtocolVersion WorkerListValue => new(FdbProtocolVersionMap.FDB_PV_WORKER_LIST_VALUE);
			/// <summary>Tests if this version supports the <c>WorkerListValue</c> feature</summary>
			public bool SupportsWorkerListValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_WORKER_LIST_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BackupStartValue</c> feature</summary>
			public static FdbProtocolVersion BackupStartValue => new(FdbProtocolVersionMap.FDB_PV_BACKUP_START_VALUE);
			/// <summary>Tests if this version supports the <c>BackupStartValue</c> feature</summary>
			public bool SupportsBackupStartValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BACKUP_START_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>LogRangeEncodeValue</c> feature</summary>
			public static FdbProtocolVersion LogRangeEncodeValue => new(FdbProtocolVersionMap.FDB_PV_LOG_RANGE_ENCODE_VALUE);
			/// <summary>Tests if this version supports the <c>LogRangeEncodeValue</c> feature</summary>
			public bool SupportsLogRangeEncodeValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_LOG_RANGE_ENCODE_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>HealthyZoneValue</c> feature</summary>
			public static FdbProtocolVersion HealthyZoneValue => new(FdbProtocolVersionMap.FDB_PV_HEALTHY_ZONE_VALUE);
			/// <summary>Tests if this version supports the <c>HealthyZoneValue</c> feature</summary>
			public bool SupportsHealthyZoneValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_HEALTHY_ZONE_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>DRBackupRanges</c> feature</summary>
			public static FdbProtocolVersion DRBackupRanges => new(FdbProtocolVersionMap.FDB_PV_DR_BACKUP_RANGES);
			/// <summary>Tests if this version supports the <c>DRBackupRanges</c> feature</summary>
			public bool SupportsDRBackupRanges() => pv.Version >= FdbProtocolVersionMap.FDB_PV_DR_BACKUP_RANGES;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>RegionConfiguration</c> feature</summary>
			public static FdbProtocolVersion RegionConfiguration => new(FdbProtocolVersionMap.FDB_PV_REGION_CONFIGURATION);
			/// <summary>Tests if this version supports the <c>RegionConfiguration</c> feature</summary>
			public bool SupportsRegionConfiguration() => pv.Version >= FdbProtocolVersionMap.FDB_PV_REGION_CONFIGURATION;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ReplicationPolicy</c> feature</summary>
			public static FdbProtocolVersion ReplicationPolicy => new(FdbProtocolVersionMap.FDB_PV_REPLICATION_POLICY);
			/// <summary>Tests if this version supports the <c>ReplicationPolicy</c> feature</summary>
			public bool SupportsReplicationPolicy() => pv.Version >= FdbProtocolVersionMap.FDB_PV_REPLICATION_POLICY;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BackupMutations</c> feature</summary>
			public static FdbProtocolVersion BackupMutations => new(FdbProtocolVersionMap.FDB_PV_BACKUP_MUTATIONS);
			/// <summary>Tests if this version supports the <c>BackupMutations</c> feature</summary>
			public bool SupportsBackupMutations() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BACKUP_MUTATIONS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ClusterControllerPriorityInfo</c> feature</summary>
			public static FdbProtocolVersion ClusterControllerPriorityInfo => new(FdbProtocolVersionMap.FDB_PV_CLUSTER_CONTROLLER_PRIORITY_INFO);
			/// <summary>Tests if this version supports the <c>ClusterControllerPriorityInfo</c> feature</summary>
			public bool SupportsClusterControllerPriorityInfo() => pv.Version >= FdbProtocolVersionMap.FDB_PV_CLUSTER_CONTROLLER_PRIORITY_INFO;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ProcessIDFile</c> feature</summary>
			public static FdbProtocolVersion ProcessIdFile => new(FdbProtocolVersionMap.FDB_PV_PROCESS_ID_FILE);
			/// <summary>Tests if this version supports the <c>ProcessIDFile</c> feature</summary>
			public bool SupportsProcessIdFile() => pv.Version >= FdbProtocolVersionMap.FDB_PV_PROCESS_ID_FILE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>CloseUnusedConnection</c> feature</summary>
			public static FdbProtocolVersion CloseUnusedConnection => new(FdbProtocolVersionMap.FDB_PV_CLOSE_UNUSED_CONNECTION);
			/// <summary>Tests if this version supports the <c>CloseUnusedConnection</c> feature</summary>
			public bool SupportsCloseUnusedConnection() => pv.Version >= FdbProtocolVersionMap.FDB_PV_CLOSE_UNUSED_CONNECTION;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>DbCoreState</c> feature</summary>
			public static FdbProtocolVersion DbCoreState => new(FdbProtocolVersionMap.FDB_PV_DB_CORE_STATE);
			/// <summary>Tests if this version supports the <c>DbCoreState</c> feature</summary>
			public bool SupportsDbCoreState() => pv.Version >= FdbProtocolVersionMap.FDB_PV_DB_CORE_STATE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TagThrottleValue</c> feature</summary>
			public static FdbProtocolVersion TagThrottleValue => new(FdbProtocolVersionMap.FDB_PV_TAG_THROTTLE_VALUE);
			/// <summary>Tests if this version supports the <c>TagThrottleValue</c> feature</summary>
			public bool SupportsTagThrottleValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TAG_THROTTLE_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>StorageCacheValue</c> feature</summary>
			public static FdbProtocolVersion StorageCacheValue => new(FdbProtocolVersionMap.FDB_PV_STORAGE_CACHE_VALUE);
			/// <summary>Tests if this version supports the <c>StorageCacheValue</c> feature</summary>
			public bool SupportsStorageCacheValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_STORAGE_CACHE_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>RestoreStatusValue</c> feature</summary>
			public static FdbProtocolVersion RestoreStatusValue => new(FdbProtocolVersionMap.FDB_PV_RESTORE_STATUS_VALUE);
			/// <summary>Tests if this version supports the <c>RestoreStatusValue</c> feature</summary>
			public bool SupportsRestoreStatusValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_RESTORE_STATUS_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>RestoreRequestValue</c> feature</summary>
			public static FdbProtocolVersion RestoreRequestValue => new(FdbProtocolVersionMap.FDB_PV_RESTORE_REQUEST_VALUE);
			/// <summary>Tests if this version supports the <c>RestoreRequestValue</c> feature</summary>
			public bool SupportsRestoreRequestValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_RESTORE_REQUEST_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>RestoreRequestDoneVersionValue</c> feature</summary>
			public static FdbProtocolVersion RestoreRequestDoneVersionValue => new(FdbProtocolVersionMap.FDB_PV_RESTORE_REQUEST_DONE_VERSION_VALUE);
			/// <summary>Tests if this version supports the <c>RestoreRequestDoneVersionValue</c> feature</summary>
			public bool SupportsRestoreRequestDoneVersionValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_RESTORE_REQUEST_DONE_VERSION_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>RestoreRequestTriggerValue</c> feature</summary>
			public static FdbProtocolVersion RestoreRequestTriggerValue => new(FdbProtocolVersionMap.FDB_PV_RESTORE_REQUEST_TRIGGER_VALUE);
			/// <summary>Tests if this version supports the <c>RestoreRequestTriggerValue</c> feature</summary>
			public bool SupportsRestoreRequestTriggerValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_RESTORE_REQUEST_TRIGGER_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>RestoreWorkerInterfaceValue</c> feature</summary>
			public static FdbProtocolVersion RestoreWorkerInterfaceValue => new(FdbProtocolVersionMap.FDB_PV_RESTORE_WORKER_INTERFACE_VALUE);
			/// <summary>Tests if this version supports the <c>RestoreWorkerInterfaceValue</c> feature</summary>
			public bool SupportsRestoreWorkerInterfaceValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_RESTORE_WORKER_INTERFACE_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BackupProgressValue</c> feature</summary>
			public static FdbProtocolVersion BackupProgressValue => new(FdbProtocolVersionMap.FDB_PV_BACKUP_PROGRESS_VALUE);
			/// <summary>Tests if this version supports the <c>BackupProgressValue</c> feature</summary>
			public bool SupportsBackupProgressValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BACKUP_PROGRESS_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>KeyServerValueV2</c> feature</summary>
			public static FdbProtocolVersion KeyServerValueV2 => new(FdbProtocolVersionMap.FDB_PV_KEY_SERVER_VALUE_V2);
			/// <summary>Tests if this version supports the <c>KeyServerValueV2</c> feature</summary>
			public bool SupportsKeyServerValueV2() => pv.Version >= FdbProtocolVersionMap.FDB_PV_KEY_SERVER_VALUE_V2;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BackupWorker</c> feature</summary>
			public static FdbProtocolVersion BackupWorker => new(FdbProtocolVersionMap.FDB_PV_BACKUP_WORKER);
			/// <summary>Tests if this version supports the <c>BackupWorker</c> feature</summary>
			public bool SupportsBackupWorker() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BACKUP_WORKER;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ReportConflictingKeys</c> feature</summary>
			public static FdbProtocolVersion ReportConflictingKeys => new(FdbProtocolVersionMap.FDB_PV_REPORT_CONFLICTING_KEYS);
			/// <summary>Tests if this version supports the <c>ReportConflictingKeys</c> feature</summary>
			public bool SupportsReportConflictingKeys() => pv.Version >= FdbProtocolVersionMap.FDB_PV_REPORT_CONFLICTING_KEYS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>SmallEndpoints</c> feature</summary>
			public static FdbProtocolVersion SmallEndpoints => new(FdbProtocolVersionMap.FDB_PV_SMALL_ENDPOINTS);
			/// <summary>Tests if this version supports the <c>SmallEndpoints</c> feature</summary>
			public bool SupportsSmallEndpoints() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SMALL_ENDPOINTS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>CacheRole</c> feature</summary>
			public static FdbProtocolVersion CacheRole => new(FdbProtocolVersionMap.FDB_PV_CACHE_ROLE);
			/// <summary>Tests if this version supports the <c>CacheRole</c> feature</summary>
			public bool SupportsCacheRole() => pv.Version >= FdbProtocolVersionMap.FDB_PV_CACHE_ROLE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>UnifiedTlogSpilling</c> feature</summary>
			public static FdbProtocolVersion UnifiedTlogSpilling => new(FdbProtocolVersionMap.FDB_PV_UNIFIED_TLOG_SPILLING);
			/// <summary>Tests if this version supports the <c>UnifiedTlogSpilling</c> feature</summary>
			public bool SupportsUnifiedTlogSpilling() => pv.Version >= FdbProtocolVersionMap.FDB_PV_UNIFIED_TLOG_SPILLING;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>StableInterfaces</c> feature</summary>
			public static FdbProtocolVersion StableInterfaces => new(FdbProtocolVersionMap.FDB_PV_STABLE_INTERFACES);
			/// <summary>Tests if this version supports the <c>StableInterfaces</c> feature</summary>
			public bool SupportsStableInterfaces() => pv.Version >= FdbProtocolVersionMap.FDB_PV_STABLE_INTERFACES;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ServerListValue</c> feature</summary>
			public static FdbProtocolVersion ServerListValue => new(FdbProtocolVersionMap.FDB_PV_SERVER_LIST_VALUE);
			/// <summary>Tests if this version supports the <c>ServerListValue</c> feature</summary>
			public bool SupportsServerListValue() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SERVER_LIST_VALUE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TagThrottleValueReason</c> feature</summary>
			public static FdbProtocolVersion TagThrottleValueReason => new(FdbProtocolVersionMap.FDB_PV_TAG_THROTTLE_VALUE_REASON);
			/// <summary>Tests if this version supports the <c>TagThrottleValueReason</c> feature</summary>
			public bool SupportsTagThrottleValueReason() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TAG_THROTTLE_VALUE_REASON;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>SpanContext</c> feature</summary>
			public static FdbProtocolVersion SpanContext => new(FdbProtocolVersionMap.FDB_PV_SPAN_CONTEXT);
			/// <summary>Tests if this version supports the <c>SpanContext</c> feature</summary>
			public bool SupportsSpanContext() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SPAN_CONTEXT;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>TSS</c> feature</summary>
			public static FdbProtocolVersion TSS => new(FdbProtocolVersionMap.FDB_PV_TSS);
			/// <summary>Tests if this version supports the <c>TSS</c> feature</summary>
			public bool SupportsTSS() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TSS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ChangeFeed</c> feature</summary>
			public static FdbProtocolVersion ChangeFeed => new(FdbProtocolVersionMap.FDB_PV_CHANGE_FEED);
			/// <summary>Tests if this version supports the <c>ChangeFeed</c> feature</summary>
			public bool SupportsChangeFeed() => pv.Version >= FdbProtocolVersionMap.FDB_PV_CHANGE_FEED;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BlobGranule</c> feature</summary>
			public static FdbProtocolVersion BlobGranule => new(FdbProtocolVersionMap.FDB_PV_BLOB_GRANULE);
			/// <summary>Tests if this version supports the <c>BlobGranule</c> feature</summary>
			public bool SupportsBlobGranule() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BLOB_GRANULE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>NetworkAddressHostnameFlag</c> feature</summary>
			public static FdbProtocolVersion NetworkAddressHostnameFlag => new(FdbProtocolVersionMap.FDB_PV_NETWORK_ADDRESS_HOSTNAME_FLAG);
			/// <summary>Tests if this version supports the <c>NetworkAddressHostnameFlag</c> feature</summary>
			public bool SupportsNetworkAddressHostnameFlag() => pv.Version >= FdbProtocolVersionMap.FDB_PV_NETWORK_ADDRESS_HOSTNAME_FLAG;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>StorageMetadata</c> feature</summary>
			public static FdbProtocolVersion StorageMetadata => new(FdbProtocolVersionMap.FDB_PV_STORAGE_METADATA);
			/// <summary>Tests if this version supports the <c>StorageMetadata</c> feature</summary>
			public bool SupportsStorageMetadata() => pv.Version >= FdbProtocolVersionMap.FDB_PV_STORAGE_METADATA;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>PerpetualWiggleMetadata</c> feature</summary>
			public static FdbProtocolVersion PerpetualWiggleMetadata => new(FdbProtocolVersionMap.FDB_PV_PERPETUAL_WIGGLE_METADATA);
			/// <summary>Tests if this version supports the <c>PerpetualWiggleMetadata</c> feature</summary>
			public bool SupportsPerpetualWiggleMetadata() => pv.Version >= FdbProtocolVersionMap.FDB_PV_PERPETUAL_WIGGLE_METADATA;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>StorageInterfaceReadiness</c> feature</summary>
			public static FdbProtocolVersion StorageInterfaceReadiness => new(FdbProtocolVersionMap.FDB_PV_STORAGE_INTERFACE_READINESS);
			/// <summary>Tests if this version supports the <c>StorageInterfaceReadiness</c> feature</summary>
			public bool SupportsStorageInterfaceReadiness() => pv.Version >= FdbProtocolVersionMap.FDB_PV_STORAGE_INTERFACE_READINESS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>Tenants</c> feature</summary>
			public static FdbProtocolVersion Tenants => new(FdbProtocolVersionMap.FDB_PV_TENANTS);
			/// <summary>Tests if this version supports the <c>Tenants</c> feature</summary>
			public bool SupportsTenants() => pv.Version >= FdbProtocolVersionMap.FDB_PV_TENANTS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ResolverPrivateMutations</c> feature</summary>
			public static FdbProtocolVersion ResolverPrivateMutations => new(FdbProtocolVersionMap.FDB_PV_RESOLVER_PRIVATE_MUTATIONS);
			/// <summary>Tests if this version supports the <c>ResolverPrivateMutations</c> feature</summary>
			public bool SupportsResolverPrivateMutations() => pv.Version >= FdbProtocolVersionMap.FDB_PV_RESOLVER_PRIVATE_MUTATIONS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>OTELSpanContext</c> feature</summary>
			public static FdbProtocolVersion OTELSpanContext => new(FdbProtocolVersionMap.FDB_PV_OTEL_SPAN_CONTEXT);
			/// <summary>Tests if this version supports the <c>OTELSpanContext</c> feature</summary>
			public bool SupportsOTELSpanContext() => pv.Version >= FdbProtocolVersionMap.FDB_PV_OTEL_SPAN_CONTEXT;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>SWVersionTracking</c> feature</summary>
			public static FdbProtocolVersion SWVersionTracking => new(FdbProtocolVersionMap.FDB_PV_SW_VERSION_TRACKING);
			/// <summary>Tests if this version supports the <c>SWVersionTracking</c> feature</summary>
			public bool SupportsSWVersionTracking() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SW_VERSION_TRACKING;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>EncryptionAtRest</c> feature</summary>
			public static FdbProtocolVersion EncryptionAtRest => new(FdbProtocolVersionMap.FDB_PV_ENCRYPTION_AT_REST);
			/// <summary>Tests if this version supports the <c>EncryptionAtRest</c> feature</summary>
			public bool SupportsEncryptionAtRest() => pv.Version >= FdbProtocolVersionMap.FDB_PV_ENCRYPTION_AT_REST;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ShardEncodeLocationMetaData</c> feature</summary>
			public static FdbProtocolVersion ShardEncodeLocationMetaData => new(FdbProtocolVersionMap.FDB_PV_SHARD_ENCODE_LOCATION_METADATA);
			/// <summary>Tests if this version supports the <c>ShardEncodeLocationMetaData</c> feature</summary>
			public bool SupportsShardEncodeLocationMetaData() => pv.Version >= FdbProtocolVersionMap.FDB_PV_SHARD_ENCODE_LOCATION_METADATA;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BlobGranuleFile</c> feature</summary>
			public static FdbProtocolVersion BlobGranuleFile => new(FdbProtocolVersionMap.FDB_PV_BLOB_GRANULE_FILE);
			/// <summary>Tests if this version supports the <c>BlobGranuleFile</c> feature</summary>
			public bool SupportsBlobGranuleFile() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BLOB_GRANULE_FILE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>EncryptedSnapshotBackupFile</c> feature</summary>
			public static FdbProtocolVersion EncryptedSnapshotBackupFile => new(FdbProtocolVersionMap.FDB_ENCRYPTED_SNAPSHOT_BACKUP_FILE);
			/// <summary>Tests if this version supports the <c>EncryptedSnapshotBackupFile</c> feature</summary>
			public bool SupportsEncryptedSnapshotBackupFile() => pv.Version >= FdbProtocolVersionMap.FDB_ENCRYPTED_SNAPSHOT_BACKUP_FILE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>ClusterIdSpecialKey</c> feature</summary>
			public static FdbProtocolVersion ClusterIdSpecialKey => new(FdbProtocolVersionMap.FDB_PV_CLUSTER_ID_SPECIAL_KEY);
			/// <summary>Tests if this version supports the <c>ClusterIdSpecialKey</c> feature</summary>
			public bool SupportsClusterIdSpecialKey() => pv.Version >= FdbProtocolVersionMap.FDB_PV_CLUSTER_ID_SPECIAL_KEY;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BlobGranuleFileLogicalSize</c> feature</summary>
			public static FdbProtocolVersion BlobGranuleFileLogicalSize => new(FdbProtocolVersionMap.FDB_PV_BLOB_GRANULE_FILE_LOGICAL_SIZE);
			/// <summary>Tests if this version supports the <c>BlobGranuleFileLogicalSize</c> feature</summary>
			public bool SupportsBlobGranuleFileLogicalSize() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BLOB_GRANULE_FILE_LOGICAL_SIZE;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>BlobRangeChangeLog</c> feature</summary>
			public static FdbProtocolVersion BlobRangeChangeLog => new(FdbProtocolVersionMap.FDB_PV_BLOB_RANGE_CHANGE_LOG);
			/// <summary>Tests if this version supports the <c>BlobRangeChangeLog</c> feature</summary>
			public bool SupportsBlobRangeChangeLog() => pv.Version >= FdbProtocolVersionMap.FDB_PV_BLOB_RANGE_CHANGE_LOG;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>GcTxnGenerations</c> feature</summary>
			public static FdbProtocolVersion GcTxnGenerations => new(FdbProtocolVersionMap.FDB_PV_GC_TXN_GENERATIONS);
			/// <summary>Tests if this version supports the XYZ feature</summary>
			public bool SupportsGcTxnGenerations() => pv.Version >= FdbProtocolVersionMap.FDB_PV_GC_TXN_GENERATIONS;

			/// <summary>Minimum <see cref="FdbProtocolVersion"/> required for support of the <c>MutationChecksum</c> feature</summary>
			public static FdbProtocolVersion MutationChecksum => new(FdbProtocolVersionMap.FDB_PV_MUTATION_CHECKSUM);
			/// <summary>Tests if this version supports the <c>MutationChecksum</c> feature</summary>
			public bool SupportsMutationChecksum() => pv.Version >= FdbProtocolVersionMap.FDB_PV_MUTATION_CHECKSUM;

		}

		#endregion

	}

	/// <summary>Contains the </summary>
	[PublicAPI]
	public sealed class FdbProtocolVersionMap
	{

		internal FdbProtocolVersionMap(Version version, ulong defaultVersion, ulong futureVersion, ulong minCompatibleVersion, ulong minInvalidVersion)
		{
			this.Version = version;
			this.DefaultVersion = new(defaultVersion);
			this.FutureVersion = new(futureVersion);
			this.MinCompatibleVersion = new(minCompatibleVersion);
			this.MinInvalidVersion = new(minInvalidVersion);
		}

		/// <summary>Version of the FoundationDB cluster (major.minor)</summary>
		public Version Version { get; }

		/// <summary>Default protocol version for this version of FoundationDB</summary>
		public FdbProtocolVersion DefaultVersion { get; }

		/// <summary>Version of the next version of FoundationDB (when upgrading from this version to the next)</summary>
		/// <example>For example, a <c>7.3</c> database could be upgraded to the <c>7.4</c> format.</example>
		public FdbProtocolVersion FutureVersion { get; }

		/// <summary>Minimum version to which the current database format could be downgraded</summary>
		/// <remarks>For example, a <c>7.3</c> database could be downgraded to the <c>7.2</c> format,</remarks>
		public FdbProtocolVersion MinCompatibleVersion { get; }

		/// <summary>Minimum future version that will <b>not</b> be compatible with the current version</summary>
		/// <example>For example, a <c>7.3</c> cluster would not be compatible with the <c>7.5</c> format.</example>
		public FdbProtocolVersion MinInvalidVersion { get; }

		#region Version Singletons...

		#region 7.1...

		// from ProtocolVersion.h in release-7.1 branch
		private const ulong FDB_71_PV_DEFAULT_VERSION                      = 0x0FDB00B071010000L;
		private const ulong FDB_71_PV_FUTURE_VERSION                       = 0x0FDB00B072000000L; // unknown, this is only a guess!
		private const ulong FDB_71_PV_MIN_COMPATIBLE_VERSION               = 0x0FDB00B070000000L; // unknown, this is only a guess!
		private const ulong FDB_71_PV_MIN_INVALID_VERSION                  = 0x0FDB00B074000000L;

		/// <summary>Version ranges supported by FoundationDB v7.1</summary>
		public static FdbProtocolVersionMap Version71 { get; } = new(
			new Version(7, 3),
			FDB_71_PV_DEFAULT_VERSION,
			FDB_71_PV_FUTURE_VERSION,
			FDB_71_PV_MIN_COMPATIBLE_VERSION,
			FDB_71_PV_MIN_INVALID_VERSION
		);

		#endregion

		#region 7.2...

		// from ProtocolVersions.cmake in release-7.2 branch
		private const ulong FDB_72_PV_DEFAULT_VERSION                      = 0x0FDB00B072000000L;
		private const ulong FDB_72_PV_FUTURE_VERSION                       = 0x0FDB00B073000000L;
		private const ulong FDB_72_PV_MIN_COMPATIBLE_VERSION               = 0x0FDB00B071000000L;
		private const ulong FDB_72_PV_MIN_INVALID_VERSION                  = 0x0FDB00B074000000L;

		/// <summary>Version ranges supported by FoundationDB v7.2</summary>
		public static FdbProtocolVersionMap Version72 { get; } = new(
			new Version(7, 3),
			FDB_72_PV_DEFAULT_VERSION,
			FDB_72_PV_FUTURE_VERSION,
			FDB_72_PV_MIN_COMPATIBLE_VERSION,
			FDB_72_PV_MIN_INVALID_VERSION
		);

		#endregion

		#region 7.3...

		// from ProtocolVersions.cmake in release-7.3 branch
		private const ulong FDB_73_PV_DEFAULT_VERSION                      = 0x0FDB00B073000000ul;
		private const ulong FDB_73_PV_FUTURE_VERSION                       = 0x0FDB00B074000000ul;
		private const ulong FDB_73_PV_MIN_COMPATIBLE_VERSION               = 0x0FDB00B072000000ul;
		private const ulong FDB_73_PV_MIN_INVALID_VERSION                  = 0x0FDB00B075000000ul;

		/// <summary>Version ranges supported by FoundationDB v7.3</summary>
		public static FdbProtocolVersionMap Version73 { get; } = new(
			new Version(7, 3),
			FDB_73_PV_DEFAULT_VERSION,
			FDB_73_PV_FUTURE_VERSION,
			FDB_73_PV_MIN_COMPATIBLE_VERSION,
			FDB_73_PV_MIN_INVALID_VERSION
		);

		#endregion

		#region 7.4...

		// from ProtocolVersions.cmake in release-7.4 branch
		private const ulong FDB_74_PV_DEFAULT_VERSION                      = 0x0FDB00B074000000ul;
		private const ulong FDB_74_PV_FUTURE_VERSION                       = 0x0FDB00B080000000ul;
		private const ulong FDB_74_PV_MIN_COMPATIBLE_VERSION               = 0x0FDB00B073000000ul;
		private const ulong FDB_74_PV_MIN_INVALID_VERSION                  = 0x0FDB00B081000000ul;

		/// <summary>Version ranges supported by FoundationDB v7.3</summary>
		public static FdbProtocolVersionMap Version74 { get; } = new(
			new Version(7, 4),
			FDB_74_PV_DEFAULT_VERSION,
			FDB_74_PV_FUTURE_VERSION,
			FDB_74_PV_MIN_COMPATIBLE_VERSION,
			FDB_74_PV_MIN_INVALID_VERSION
		);

		#endregion

		#endregion

		#region Feature Table

		// These are extracted from ProtocolVersions.cmake, and encode the protocol version at which these features where introduced.
		// - At each new X.Y version, features will be added to this list.
		// - A node with protocol version >= to one of these values, means that the node supports the corresponding feature.

		public const long FDB_PV_WATCHES                              = 0x0FDB00A200090000L;
		public const long FDB_PV_MOVABLE_COORDINATED_STATE            = 0x0FDB00A2000D0000L;
		public const long FDB_PV_PROCESS_ID                           = 0x0FDB00A340000000L;
		public const long FDB_PV_OPEN_DATABASE                        = 0x0FDB00A400040000L;
		public const long FDB_PV_LOCALITY                             = 0x0FDB00A446020000L;
		public const long FDB_PV_MULTIGENERATION_TLOG                 = 0x0FDB00A460010000L;
		public const long FDB_PV_SHARED_MUTATIONS                     = 0x0FDB00A460010000L;
		public const long FDB_PV_INEXPENSIVE_MULTIVERSION_CLIENT      = 0x0FDB00A551000000L;
		public const long FDB_PV_TAG_LOCALITY                         = 0x0FDB00A560010000L;
		public const long FDB_PV_FEARLESS                             = 0x0FDB00B060000000L;
		public const long FDB_PV_ENDPOINT_ADDR_LIST                   = 0x0FDB00B061020000L;
		public const long FDB_PV_IPV6                                 = 0x0FDB00B061030000L;
		public const long FDB_PV_TLOG_VERSION                         = 0x0FDB00B061030000L;
		public const long FDB_PV_PSEUDO_LOCALITIES                    = 0x0FDB00B061070000L;
		public const long FDB_PV_SHARDED_TXS_TAGS                     = 0x0FDB00B061070000L;
		public const long FDB_PV_TLOG_QUEUE_ENTRY_REF                 = 0x0FDB00B062010001L;
		public const long FDB_PV_GENERATION_REG_VAL                   = 0x0FDB00B062010001L;
		public const long FDB_PV_MOVABLE_COORDINATED_STATE_V2         = 0x0FDB00B062010001L;
		public const long FDB_PV_KEY_SERVER_VALUE                     = 0x0FDB00B062010001L;
		public const long FDB_PV_LOGS_VALUE                           = 0x0FDB00B062010001L;
		public const long FDB_PV_SERVER_TAG_VALUE                     = 0x0FDB00B062010001L;
		public const long FDB_PV_TAG_LOCALITY_LIST_VALUE              = 0x0FDB00B062010001L;
		public const long FDB_PV_DATACENTER_REPLICAS_VALUE            = 0x0FDB00B062010001L;
		public const long FDB_PV_PROCESS_CLASS_VALUE                  = 0x0FDB00B062010001L;
		public const long FDB_PV_WORKER_LIST_VALUE                    = 0x0FDB00B062010001L;
		public const long FDB_PV_BACKUP_START_VALUE                   = 0x0FDB00B062010001L;
		public const long FDB_PV_LOG_RANGE_ENCODE_VALUE               = 0x0FDB00B062010001L;
		public const long FDB_PV_HEALTHY_ZONE_VALUE                   = 0x0FDB00B062010001L;
		public const long FDB_PV_DR_BACKUP_RANGES                     = 0x0FDB00B062010001L;
		public const long FDB_PV_REGION_CONFIGURATION                 = 0x0FDB00B062010001L;
		public const long FDB_PV_REPLICATION_POLICY                   = 0x0FDB00B062010001L;
		public const long FDB_PV_BACKUP_MUTATIONS                     = 0x0FDB00B062010001L;
		public const long FDB_PV_CLUSTER_CONTROLLER_PRIORITY_INFO     = 0x0FDB00B062010001L;
		public const long FDB_PV_PROCESS_ID_FILE                      = 0x0FDB00B062010001L;
		public const long FDB_PV_CLOSE_UNUSED_CONNECTION              = 0x0FDB00B062010001L;
		public const long FDB_PV_DB_CORE_STATE                        = 0x0FDB00B063010000L;
		public const long FDB_PV_TAG_THROTTLE_VALUE                   = 0x0FDB00B063010000L;
		public const long FDB_PV_STORAGE_CACHE_VALUE                  = 0x0FDB00B063010000L;
		public const long FDB_PV_RESTORE_STATUS_VALUE                 = 0x0FDB00B063010000L;
		public const long FDB_PV_RESTORE_REQUEST_VALUE                = 0x0FDB00B063010000L;
		public const long FDB_PV_RESTORE_REQUEST_DONE_VERSION_VALUE   = 0x0FDB00B063010000L;
		public const long FDB_PV_RESTORE_REQUEST_TRIGGER_VALUE        = 0x0FDB00B063010000L;
		public const long FDB_PV_RESTORE_WORKER_INTERFACE_VALUE       = 0x0FDB00B063010000L;
		public const long FDB_PV_BACKUP_PROGRESS_VALUE                = 0x0FDB00B063010000L;
		public const long FDB_PV_KEY_SERVER_VALUE_V2                  = 0x0FDB00B063010000L;
		public const long FDB_PV_UNIFIED_TLOG_SPILLING                = 0x0FDB00B063000000L;
		public const long FDB_PV_BACKUP_WORKER                        = 0x0FDB00B063010000L;
		public const long FDB_PV_REPORT_CONFLICTING_KEYS              = 0x0FDB00B063010000L;
		public const long FDB_PV_SMALL_ENDPOINTS                      = 0x0FDB00B063010000L;
		public const long FDB_PV_CACHE_ROLE                           = 0x0FDB00B063010000L;
		public const long FDB_PV_STABLE_INTERFACES                    = 0x0FDB00B070010000L;
		public const long FDB_PV_SERVER_LIST_VALUE                    = 0x0FDB00B070010001L;
		public const long FDB_PV_TAG_THROTTLE_VALUE_REASON            = 0x0FDB00B070010001L;
		public const long FDB_PV_SPAN_CONTEXT                         = 0x0FDB00B070010001L;
		public const long FDB_PV_TSS                                  = 0x0FDB00B070010001L;
		public const long FDB_PV_CHANGE_FEED                          = 0x0FDB00B071010000L;
		public const long FDB_PV_BLOB_GRANULE                         = 0x0FDB00B071010000L;
		public const long FDB_PV_NETWORK_ADDRESS_HOSTNAME_FLAG        = 0x0FDB00B071010000L;
		public const long FDB_PV_STORAGE_METADATA                     = 0x0FDB00B071010000L;
		public const long FDB_PV_PERPETUAL_WIGGLE_METADATA            = 0x0FDB00B071010000L;
		public const long FDB_PV_STORAGE_INTERFACE_READINESS          = 0x0FDB00B071010000L;
		public const long FDB_PV_TENANTS                              = 0x0FDB00B071010000L;
		public const long FDB_PV_RESOLVER_PRIVATE_MUTATIONS           = 0x0FDB00B071010000L;
		public const long FDB_PV_OTEL_SPAN_CONTEXT                    = 0x0FDB00B072000000L;
		public const long FDB_PV_SW_VERSION_TRACKING                  = 0x0FDB00B072000000L;
		public const long FDB_PV_ENCRYPTION_AT_REST                   = 0x0FDB00B072000000L;
		public const long FDB_PV_SHARD_ENCODE_LOCATION_METADATA       = 0x0FDB00B072000000L;
		public const long FDB_PV_BLOB_GRANULE_FILE                    = 0x0FDB00B072000000L;
		public const long FDB_ENCRYPTED_SNAPSHOT_BACKUP_FILE          = 0x0FDB00B072000000L;
		public const long FDB_PV_CLUSTER_ID_SPECIAL_KEY               = 0x0FDB00B072000000L;
		public const long FDB_PV_BLOB_GRANULE_FILE_LOGICAL_SIZE       = 0x0FDB00B072000000L;
		public const long FDB_PV_BLOB_RANGE_CHANGE_LOG                = 0x0FDB00B072000000L;
		public const long FDB_PV_GC_TXN_GENERATIONS                   = 0x0FDB00B073000000L;
		public const long FDB_PV_MUTATION_CHECKSUM                    = 0x0FDB00B074000000L;

		#endregion

	}

}
