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

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace FoundationDB.Client.Status
{

	/// <summary>Snapshot of the state of a FoundationDB cluster</summary>
	[PublicAPI]
	public sealed record FdbSystemStatus : MetricsBase
	{

		internal FdbSystemStatus(JsonObject? doc, long readVersion)
			: base(doc)
		{
			if (doc is not null)
			{
				this.Client = new ClientStatus(doc.GetObjectOrDefault("client"));
				this.Cluster = new ClusterStatus(doc.GetObjectOrDefault("cluster"));
				this.ReadVersion = readVersion;
			}
			else
			{
				this.Client = new ClientStatus(null);
				this.Cluster = new ClusterStatus(null);
				this.ReadVersion = 0;
			}
		}

		/// <summary>Checks if this instance contains valid status data</summary>
		public bool IsValid => this.ReadVersion != 0;

		/// <summary>Details about the local Client</summary>
		public ClientStatus Client { get; }

		/// <summary>Details about the remote Cluster</summary>
		public ClusterStatus Cluster { get; }

		/// <summary>Read Version of the snapshot</summary>
		public long ReadVersion { get; }

		/// <summary>Raw JSON text of this snapshot.</summary>
		/// <remarks>This is the same value that is returned by running 'status json' in fdbcli, decoded as utf-8</remarks>
		public string RawJson => m_data?.ToJsonTextIndented() ?? string.Empty;

		/// <summary>Parsed JSON data of this snapshot.</summary>
		public JsonObject? JsonData => m_data;

	}

	#region Common...

	/// <summary>Details about a notification, alert or error, as reported by a component of a FoundationDB cluster</summary>
	[DebuggerDisplay("{Name}")]
	public readonly struct Message : IEquatable<Message>
	{
		/// <summary>Code for this message</summary>
		public readonly string Name;

		/// <summary>User-friendly description of this message</summary>
		public readonly string Description;

		/// <summary>If specified, an unique ID representing this message</summary>
		public readonly int? ReasonId;

		internal Message(string name, string description, int? reasonId)
		{
			this.Name = name;
			this.Description = description;
			this.ReasonId = reasonId;
		}

		internal static Message From(JsonObject? data, string field)
		{
			if (data == null || !data.TryGetObject(field, out var obj))
			{
				return new Message("", "", null);
			}

			var key = obj.Get<string>("name", "");
			var value = obj.Get<string>("description", "");
			var reasonId = obj.Get<int?>("reasonId", null);
			return new Message(key, value, reasonId);
		}

		internal static Message[] FromArray(JsonObject? data, string field)
		{
			if (data == null || !data.TryGetArray(field, out var array) || array.Count == 0)
			{
				return [];
			}

			var res = new Message[array.Count];
			for (int i = 0; i < res.Length; i++)
			{
				if (array.TryGetObject(i, out var obj))
				{
					var key = obj.Get<string>("name", "");
					var value = obj.Get<string>("description", "");
					var reasonId = obj.Get<int?>("reasonId", null);
					res[i] = new Message(key, value, reasonId);
				}
				else
				{
					res[i] = new Message("", "", null);
				}
			}
			return res;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"[{this.Name}] {this.Description}";
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(this.Name);
		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			return obj is Message message && Equals(message);
		}

		/// <inheritdoc />
		public bool Equals(Message other)
		{
			return string.Equals(this.Name, other.Name)
				&& string.Equals(this.Description, other.Description);
		}
	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Hz={Hz}")]
	public record RateCounter : MetricsBase
	{

		internal RateCounter(JsonObject? data)
			: base(data)
		{
			this.Hz = GetDouble("hz") ?? 0;
		}

		/// <summary>Rate of change, per seconds, since the last snapshot ("UNIT per seconds")</summary>
		public double Hz { get; }

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Hz={0:N1}", this.Hz);
		}
	}

	[DebuggerDisplay("Counter={Counter}, Hz={Hz}")]
	public record LoadCounter : RateCounter
	{
		internal LoadCounter(JsonObject? data) : base(data)
		{
			this.Counter = GetInt64("counter") ?? 0;
		}

		/// <summary>Absolute value, since the start (ex: "UNIT")</summary>
		public long Counter { get; }

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}", this.Counter, this.Hz);
		}

	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Counter={Counter}, Hz={Hz}, Roughness={Roughness}")]
	public sealed record RoughnessCounter : LoadCounter
	{

		internal RoughnessCounter(JsonObject? data) : base(data)
		{
			this.Roughness = GetDouble("roughness") ?? 0;
		}

		public double Roughness { get; }

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}, Roughness={2:N2}", this.Counter, this.Hz, this.Roughness);
		}
	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Counter={Counter}, Hz={Hz}, Sectors={Sectors}")]
	public sealed record DiskCounter : LoadCounter
	{

		internal DiskCounter(JsonObject? data) : base(data)
		{
			this.Sectors = GetInt64("sectors") ?? 0;
		}
		public long Sectors { get; }

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}, Sectors={2:N0}", this.Counter, this.Hz, this.Sectors);
		}
	}

	[DebuggerDisplay("Seconds={Seconds}, Versions={Versions}")]
	public sealed record LagCounter : MetricsBase
	{
		internal LagCounter(JsonObject? data) : base(data)
		{
			this.Seconds = GetDouble("seconds") ?? 0;
			this.Versions = GetInt64("versions") ?? 0;
		}

		public double Seconds { get; }

		public long Versions { get; }

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Seconds={0:N3}, Versions={1:N0}", this.Seconds, this.Versions);
		}

	}

	/// <summary>Base class for all metrics containers</summary>
	[PublicAPI]
	public abstract record MetricsBase : IJsonSerializable, IJsonPackable
	{
		protected readonly JsonObject? m_data;

		protected MetricsBase(JsonObject? data)
		{
			m_data = data;
		}

		/// <summary>Returns <see langword="true"/> if this section was present in the parent</summary>
		/// <remarks>If <see langword="false"/>, the content of this instance should be discarded</remarks>
		public bool Exists() => m_data != null;

		public JsonValue this[string name] => m_data?.GetValueOrDefault(name) ?? JsonNull.Missing;

		protected JsonObject? GetObject(string field) => m_data?.GetObjectOrDefault(field);

		protected JsonArray? GetArray(string field) => m_data?.GetArrayOrDefault(field);

		protected string? GetString(string field) => m_data?.Get<string?>(field, null);

		protected string? GetString(string field1, string field2) => m_data != null && m_data.TryGetObject(field1, out var v1) && v1.TryGet<string>(field2, out var v2) ? v2 : null;

		protected int? GetInt32(string field) => m_data?.Get<int?>(field, null);

		protected int? GetInt32(string field1, string field2) => m_data != null && m_data.TryGetObject(field1, out var v1) && v1.TryGet<int>(field2, out var v2) ? v2 : null;

		protected long? GetInt64(string field) => m_data?.Get<long?>(field, null);

		protected long? GetInt64(string field1, string field2) => m_data != null && m_data.TryGetObject(field1, out var v1) && v1.TryGet<long>(field2, out var v2) ? v2 : null;

		protected double? GetDouble(string field) => m_data?.Get<double?>(field, null);

		protected double? GetDouble(string field1, string field2) => m_data != null && m_data.TryGetObject(field1, out var v1) && v1.TryGet<double>(field2, out var v2) ? v2 : null;

		protected bool? GetBoolean(string field) => m_data?.Get<bool?>(field, null);

		protected bool? GetBoolean(string field1, string field2) => m_data != null && m_data.TryGetObject(field1, out var v1) && v1.TryGet<bool>(field2, out var v2) ? v2 : null;

		protected FdbEndPoint GetEndpoint(string field) => FdbEndPoint.TryParse(GetString(field), out var ep) ? ep : FdbEndPoint.Invalid;

		protected FdbEndPoint[] GetEndpoints(string field) => GetArray(field).OrEmpty().Select(value => FdbEndPoint.TryParse(value.ToStringOrDefault(), out var ep) ? ep : FdbEndPoint.Invalid).ToArray();

		public override string ToString() => m_data?.ToJsonText(CrystalJsonSettings.JsonIndented) ?? string.Empty;

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => (m_data ?? JsonNull.Null).JsonSerialize(writer);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_data ?? JsonNull.Null;

	}

	#endregion

	#region Client...

	/// <summary>Description of the current status of the local FoundationDB client</summary>
	public sealed record ClientStatus : MetricsBase
	{
		internal ClientStatus(JsonObject? data) : base(data) { }

		private Message[]? m_messages;

		/// <summary>Path to the '.cluster' file used by the client to connect to the cluster</summary>
		public string? ClusterFilePath => GetString("cluster_file", "path");

		/// <summary>Indicates if the content of the '.cluster' file is up to date with the current topology of the cluster</summary>
		public bool ClusterFileUpToDate => GetBoolean("cluster_file", "up_to_date") ?? false;

		/// <summary>Liste of active messages for the client</summary>
		/// <remarks>The most common client messages are listed in <see cref="ClientMessages"/>.</remarks>
		public Message[] Messages => m_messages ??= Message.FromArray(m_data, "messages");

		/// <summary>Timestamp of the local client (unix time)</summary>
		/// <remarks>Number of seconds since 1970-01-01Z, using the local system clock</remarks>
		public long Timestamp => GetInt64("timestamp") ?? 0;

		/// <summary>Local system time on the client</summary>
		public DateTime SystemTime => new DateTime(checked(621355968000000000L + this.Timestamp * TimeSpan.TicksPerSecond), DateTimeKind.Utc);

		/// <summary>Specifies if the local client was able to connect to the cluster</summary>
		public bool DatabaseAvailable => GetBoolean("database_status", "available") ?? false;

		/// <summary>Specifies if the database is currently healthy</summary>
		//REVIEW: what does it mean if available=true, but healthy=false ?
		public bool DatabaseHealthy => GetBoolean("database_status", "healthy") ?? false;
	}

	/// <summary>List of well known client messages</summary>
	public static class ClientMessages
	{

		public const string InconsistentClusterFile = "inconsistent_cluster_file";

		public const string NoClusterController = "no_cluster_controller";

		public const string QuorumNotReachable = "quorum_not_reachable";

		public const string StatusIncompleteClient = "status_incomplete_client";

		public const string StatusIncompleteCluster = "status_incomplete_cluster";

		public const string StatusIncompleteCoordinators = "status_incomplete_coordinators";

		public const string StatusIncompleteError = "status_incomplete_error";

		public const string StatusIncompleteTimeout = "status_incomplete_timeout ";

		public const string UnreachableClusterController = "unreachable_cluster_controller";

	}

	#endregion

	#region Cluster...

	/// <summary>Description of the current status of a FoundationDB cluster</summary>
	public sealed record ClusterStatus : MetricsBase
	{

		internal ClusterStatus(JsonObject? data)
			: base(data)
		{ }

		private Message[]? m_messages;
		private ClusterConfiguration? m_configuration;
		private DataMetrics? m_dataMetrics;
		private LatencyMetrics? m_latency;
		private QosMetrics? m_qos;
		private WorkloadMetrics? m_workload;
		private ClusterClientsMetrics? m_clients;
		private ClusterTenantsMetrics? m_tenants;
		private Dictionary<string, ProcessStatus>? m_processes;
		private Dictionary<string, MachineStatus>? m_machines;

		/// <summary><c>cluster_controller_timestamp</c>: Unix time of the cluster controller</summary>
		/// <remarks>Number of seconds since the Unix epoch (1970-01-01Z)</remarks>
		public long ClusterControllerTimestamp => GetInt64("cluster_controller_timestamp") ?? 0;

		/// <summary><c>connection_string</c></summary>
		public string ConnectionString => GetString("connection_string") ?? string.Empty;

		/// <summary><c>database_available</c></summary>
		public bool DatabaseAvailable => GetBoolean("database_available") ?? false;

		/// <summary><c>database_locked</c></summary>
		public bool DatabaseLocked => GetBoolean("database_locked") ?? false;

		/// <summary><c>protocol_version</c></summary>
		public FdbProtocolVersion ProtocolVersion => FdbProtocolVersion.Parse(GetString("protocol_version") ?? string.Empty);

		/// <summary><c>newest_protocol_version</c></summary>
		public FdbProtocolVersion NewestProtocolVersion => FdbProtocolVersion.Parse(GetString("newest_protocol_version") ?? string.Empty);

		/// <summary><c>lowest_compatible_protocol_version</c></summary>
		public FdbProtocolVersion LowestCompatibleProtocolVersion => FdbProtocolVersion.Parse(GetString("lowest_compatible_protocol_version") ?? string.Empty);

		/// <summary><c>full_replication</c></summary>
		public bool FullReplication => GetBoolean("full_replication") ?? false;

		/// <summary><c>generation</c></summary>
		public long Generation => GetInt64("generation") ?? 0;

		/// <summary><c>license</c>: License string of the cluster</summary>
		public string License => GetString("license") ?? string.Empty;

		/// <summary><c>messages</c>: List of currently active messages</summary>
		/// <remarks>Includes notifications, warnings, errors, ...</remarks>
		public Message[] Messages => m_messages ??= Message.FromArray(m_data, "messages");

		/// <summary><c>recovery_state</c>: Recovery state of the cluster</summary>
		public Message RecoveryState => Message.From(m_data, "recovery_state");

		/// <summary><c>configuration</c></summary>
		public ClusterConfiguration Configuration => m_configuration ??= new ClusterConfiguration(GetObject("configuration"));

		/// <summary><c>data</c></summary>
		public DataMetrics Data => m_dataMetrics ??= new DataMetrics(GetObject("data"));

		/// <summary><c>latency_probe</c></summary>
		public LatencyMetrics Latency => m_latency ??= new LatencyMetrics(GetObject("latency_probe"));

		/// <summary><c>qos</c>: QoS metrics</summary>
		public QosMetrics Qos => m_qos ??= new QosMetrics(GetObject("qos"));

		/// <summary><c>workload</c>: Workload metrics</summary>
		public WorkloadMetrics Workload => m_workload ??= new WorkloadMetrics(GetObject("workload"));

		/// <summary><c>clients</c></summary>
		public ClusterClientsMetrics Clients => m_clients ??= new ClusterClientsMetrics(GetObject("clients"));

		/// <summary><c>tenants</c></summary>
		public ClusterTenantsMetrics Tenants => m_tenants ??= new ClusterTenantsMetrics(GetObject("tenants"));


		/// <summary><c>processes</c>: List of the processes that are currently active in the cluster</summary>
		public IReadOnlyDictionary<string, ProcessStatus> Processes => m_processes ??= ComputeProcesses();

		private Dictionary<string, ProcessStatus> ComputeProcesses()
		{
			var obj = GetObject("processes");
			var processes = new Dictionary<string, ProcessStatus>(obj?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
			if (obj != null)
			{
				//REVIEW: are ids case-sensitive?
				foreach (var kvp in obj)
				{
					var item = (JsonObject?) kvp.Value;
					processes[kvp.Key] = new ProcessStatus(item, kvp.Key);
				}
			}
			return processes;
		}

		/// <summary><c>machines</c>: List of the machines that are currently active in the cluster</summary>
		public IReadOnlyDictionary<string, MachineStatus> Machines => m_machines ??= ComputeMachines();

		private Dictionary<string, MachineStatus> ComputeMachines()
		{
			var obj = GetObject("machines");
			var machines = new Dictionary<string, MachineStatus>(obj?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
			if (obj != null)
			{
				//REVIEW: are ids case-sensitive?
				foreach (var kvp in obj)
				{
					var item = (JsonObject?) kvp.Value;
					machines[kvp.Key] = new MachineStatus(item, kvp.Key);
				}
			}
			return machines;
		}

	}

	/// <summary>List of well known cluster messages</summary>
	public static class ClusterMessages
	{

		public const string UnreachableClusterController = "unreachable_cluster_controller";

		public const string ClientIssues = "client_issues";

		public const string CommitTimeout = "commit_timeout";

		public const string ReadTimeout = "read_timeout";

		public const string StatusIncomplete = "status_incomplete";

		public const string StorageServersError = "storage_servers_error";

		public const string TransactionStartTimeout = "transaction_start_timeout";

		public const string UnreachableMasterWorker = "unreachable_master_worker";

		public const string UnreachableProcesses = "unreachable_processes";

		public const string UnreadableConfiguration = "unreadable_configuration";

	}

	public sealed record ClusterConfiguration : MetricsBase
	{
		internal ClusterConfiguration(JsonObject? data) : base(data)
		{
		}

		private string[]? m_excludedServers;

		public int CoordinatorsCount => GetInt32("coordinators_count") ?? 0;

		public int? Resolvers => GetInt32("resolvers");

		public int? Proxies => GetInt32("proxies");

		public int? Logs => GetInt32("logs");

		public int? CommitProxies => GetInt32("commit_proxies");

		public int? GrvProxies => GetInt32("grv_proxies");

		public int? UsableRegions => GetInt32("usable_regions");

		public int? LogSpill => GetInt32("log_spill");

		public string? LogEngine => GetString("log_engine");

		public string? StorageEngine => GetString("storage_engine");

		[Obsolete("Deprecated, use RedundancyMode instead")]
		public string RedundancyFactor => GetString("redundancy", "factor") ?? string.Empty;

		public string? RedundancyMode => GetString("redundancy_mode");

		public string? StorageMigrationType => GetString("storage_migration_type");

		public int? PerpetualStorageWiggle => GetInt32("perpetual_storage_wiggle");

		public string? PerpetualStorageWiggleEngine => GetString("perpetual_storage_wiggle_engine");

		public string? PerpetualStorageWiggleLocality => GetString("perpetual_storage_wiggle_locality");

		public string? TenantMode => GetString("tenant_mode");

		public string? EncryptionAtRestMode => GetString("encryption_at_rest_mode");

		public int? BackupWorkerEnabled => GetInt32("backup_worker_enabled");

		public int? BlobGranulesEnabled => GetInt32("blob_granules_enabled");

		public IReadOnlyList<string> ExcludedServers
		{
			get
			{
				if (m_excludedServers == null)
				{
					var arr = GetArray("excluded_servers");
					var res = arr?.Count > 0 ? new string[arr.Count] : [ ];
					if (arr != null)
					{
						for (int i = 0; i < res.Length; i++)
						{
							res[i] = arr[i].Get("address", "");
						}
					}
					m_excludedServers = res;
				}
				return m_excludedServers;
			}
		}
	}

	public sealed record LatencyMetrics : MetricsBase
	{
		internal LatencyMetrics(JsonObject? data) : base(data)
		{
			//REVIEW: TimeSpans?
			this.CommitSeconds = GetDouble("commit_seconds") ?? 0;
			this.ReadSeconds = GetDouble("read_seconds") ?? 0;
			this.TransactionStartSeconds = GetDouble("transaction_start_seconds") ?? 0;
		}

		public double CommitSeconds { get; }

		public double ReadSeconds { get; set; }

		public double TransactionStartSeconds { get; }
	}

	/// <summary>Details about the volume of data stored in the cluster</summary>
	public sealed record DataMetrics : MetricsBase
	{
		internal DataMetrics(JsonObject? data) : base(data) { }

		public long AveragePartitionSizeBytes => GetInt64("average_partition_size_bytes") ?? 0;

		public long LeastOperatingSpaceBytesLogServer => GetInt64("least_operating_space_bytes_log_server") ?? 0;

		public long LeastOperatingSpaceBytesStorageServer => GetInt64("least_operating_space_bytes_storage_server") ?? 0;

		public long MovingDataInFlightBytes => GetInt64("moving_data", "in_flight_bytes") ?? 0;

		public long MovingDataInQueueBytes => GetInt64("moving_data", "in_queue_bytes") ?? 0;

		public long MovingDataHighestPriority => GetInt64("moving_data", "highest_priority") ?? 0;

		public long MovingDataTotalWrittenBytes => GetInt64("moving_data", "total_written_bytes") ?? 0;

		public long PartitionsCount => GetInt64("partitions_count") ?? 0;

		public long TotalDiskUsedBytes => GetInt64("total_disk_used_bytes") ?? 0;

		public long TotalKVUsedBytes => GetInt64("total_kv_size_bytes") ?? 0;

		public long SystemKVSizeBytes => GetInt64("system_kv_size_bytes") ?? 0;

		public bool StateHealthy => GetBoolean("state", "healthy") ?? false;

		public string StateName => GetString("state", "name") ?? "";

		public int? StateMinReplicasRemaining => GetInt32("state", "min_replicas_remaining");

	}

	/// <summary>Details about the quality of service offered by the cluster</summary>
	public sealed record QosMetrics : MetricsBase
	{
		internal QosMetrics(JsonObject? data) : base(data) { }

		/// <summary>Current limiting factor for the performance of the cluster</summary>
		public Message PerformanceLimitedBy => Message.From(m_data, "performance_limited_by");

		public Message BatchPerformanceLimitedBy => Message.From(m_data, "batch_performance_limited_by");

		public long WorstQueueBytesLogServer => GetInt64("worst_queue_bytes_log_server") ?? 0;

		public long WorstQueueBytesStorageServer => GetInt64("worst_queue_bytes_storage_server") ?? 0;

		public int? TransactionsPerSecondLimit => GetInt32("transactions_per_second_limit");

		public int? BatchTransactionsPerSecondLimit => GetInt32("batch_transactions_per_second_limit");

	}

	/// <summary>Details about the current wokrload of the cluster</summary>
	public sealed record WorkloadMetrics : MetricsBase
	{
		internal WorkloadMetrics(JsonObject? data) : base(data) { }

		private WorkloadBytesMetrics? m_bytes;
		private WorkloadOperationsMetrics? m_operations;
		private WorkloadTransactionsMetrics? m_transactions;

		/// <summary>Performance counters for the volume of data processed by the database</summary>
		public WorkloadBytesMetrics Bytes => m_bytes ??= new WorkloadBytesMetrics(GetObject("bytes"));

		/// <summary>Performance counters for the operations on the keys in the database</summary>
		public WorkloadOperationsMetrics Operations => m_operations ??= new WorkloadOperationsMetrics(GetObject("operations"));

		/// <summary>Performance counters for the transactions.</summary>
		public WorkloadTransactionsMetrics Transactions => m_transactions ??= new WorkloadTransactionsMetrics(GetObject("transactions"));
	}

	/// <summary>Throughput of a FoundationDB cluster</summary>
	public sealed record WorkloadBytesMetrics : MetricsBase
	{
		internal WorkloadBytesMetrics(JsonObject? data) : base(data)
		{
			this.Written = new RoughnessCounter(GetObject("written"));
		}

		/// <summary>Bytes written</summary>
		//REVIEW: this looks like the size of writes in transactions, NOT the number of bytes written to the disk!
		public RoughnessCounter Written { get; }

	}

	/// <summary>Operations workload of a FoundationDB cluster</summary>
	public sealed record WorkloadOperationsMetrics : MetricsBase
	{
		internal WorkloadOperationsMetrics(JsonObject? data) : base(data)
		{
			this.Reads = new RoughnessCounter(GetObject("reads"));
			this.Writes = new RoughnessCounter(GetObject("writes"));
			this.ReadRequests = new RoughnessCounter(GetObject("read_requests"));
		}

		/// <summary>Details about read operations</summary>
		public RoughnessCounter Reads { get; }

		/// <summary>Details about write operations</summary>
		public RoughnessCounter Writes { get; }

		public RoughnessCounter ReadRequests { get; }
	}

	/// <summary>Transaction workload of a FoundationDB cluster</summary>
	public sealed record WorkloadTransactionsMetrics : MetricsBase
	{
		internal WorkloadTransactionsMetrics(JsonObject? data) : base(data)
		{
			this.Committed = new RoughnessCounter(GetObject("committed"));
			this.Conflicted = new RoughnessCounter(GetObject("conflicted"));
			this.Started = new RoughnessCounter(GetObject("started"));
		}

		public RoughnessCounter Committed { get; }

		public RoughnessCounter Conflicted { get; }

		public RoughnessCounter Started { get; }
	}

	public sealed record ClusterClientsMetrics : MetricsBase
	{
		internal ClusterClientsMetrics(JsonObject? data) : base(data)
		{
			this.Count = (int) (GetInt64("count") ?? 0);
		}

		public int Count { get; }

		//TODO: "supported_versions"
	}

	public sealed record ClusterTenantsMetrics : MetricsBase
	{

		internal ClusterTenantsMetrics(JsonObject? data) : base(data)
		{
			this.Count = (int) (GetInt64("num_tenants") ?? 0);
		}

		/// <summary><c>num_tenants</c></summary>
		public int Count { get; }

	}

	#endregion

	#region Processes...

	/// <summary>Details about a FoundationDB process</summary>
	public sealed record ProcessStatus : MetricsBase
	{

		internal ProcessStatus(JsonObject? data, string id) : base(data)
		{
			this.Id = id;
		}

		private string? m_machineId;
		private string? m_address;
		private Message[]? m_messages;
		private ProcessNetworkMetrics? m_network;
		private ProcessCpuMetrics? m_cpu;
		private ProcessDiskMetrics? m_disk;
		private ProcessMemoryMetrics? m_memory;
		private LocalityConfiguration? m_locality;
		private ProcessRoleMetrics[]? m_roles;

		/// <summary>Unique identifier for this process.</summary>
		//TODO: is it stable across reboots? what are the conditions for a process to change its ID ?
		public string Id { get; }

		/// <summary>Identifier of the machine that is hosting this process</summary>
		/// <remarks>All processes that have the same MachineId are running on the same (physical) machine.</remarks>
		public string MachineId => m_machineId ??= GetString("machine_id") ?? string.Empty;

		/// <summary>Version of this process</summary>
		/// <example>"3.0.4"</example>
		public string Version => GetString("version") ?? string.Empty;

		public TimeSpan Uptime => TimeSpan.FromSeconds(GetDouble("uptime_seconds") ?? 0);

		/// <summary>Address and port of this process, with syntax "IP_ADDRESS:port"</summary>
		/// <example>"10.1.2.34:4500"</example>
		public string Address => m_address ??= GetString("address") ?? string.Empty;

		public string ClassSource => GetString("class_source") ?? string.Empty;

		public string ClassType => GetString("class_type") ?? string.Empty;

		/// <summary>Command line that was used to start this process</summary>
		public string CommandLine => GetString("command_line") ?? string.Empty;

		/// <summary>If true, this process is currently excluded from the cluster</summary>
		public bool Excluded => GetBoolean("excluded") ?? false;

		public string FaultDomain => GetString("fault_domain") ?? string.Empty;

		/// <summary>List of messages that are currently published by this process</summary>
		public Message[] Messages => m_messages ??= Message.FromArray(m_data, "messages");

		/// <summary>Network performance counters</summary>
		public ProcessNetworkMetrics Network => m_network ??= new ProcessNetworkMetrics(GetObject("network"));

		/// <summary>CPU performance counters</summary>
		public ProcessCpuMetrics Cpu => m_cpu ??= new ProcessCpuMetrics(GetObject("cpu"));

		/// <summary>Disk performance counters</summary>
		public ProcessDiskMetrics Disk => m_disk ??= new ProcessDiskMetrics(GetObject("disk"));

		/// <summary>Memory performance counters</summary>
		public ProcessMemoryMetrics Memory => m_memory ??= new ProcessMemoryMetrics(GetObject("memory"));

		public LocalityConfiguration Locality => m_locality ??= new LocalityConfiguration(GetObject("locality"));

		/// <summary>List of the roles assumed by this process</summary>
		/// <remarks>The key is the unique role ID in the cluster, and the value is the type of the role itself</remarks>
		public ProcessRoleMetrics[] Roles
		{
			get
			{
				if (m_roles == null)
				{
					//REVIEW: should we have (K=id, V=role) or (K=role, V=id) ?

					var arr = GetArray("roles");
					var res = arr?.Count > 0 ? new ProcessRoleMetrics[arr.Count] : [ ];
					if (arr != null)
					{
						for (int i = 0; i < res.Length; i++)
						{
							res[i] = ProcessRoleMetrics.Create((JsonObject?) arr[i]);
						}
					}
					m_roles = res;
				}
				return m_roles;
			}
		}

	}

	public record ProcessRoleMetrics : MetricsBase
	{
		internal ProcessRoleMetrics(JsonObject? data, string role) : base(data)
		{
			this.Role = role;
			this.Id = GetString("id") ?? string.Empty;
		}

		public string Id { get; }

		public string Role { get; }

		//TODO: values will vary depending on the "Role" !

		public static ProcessRoleMetrics Create(JsonObject? data)
		{
			string? role = data?.Get<string?>("role", null);
			return role switch
			{
				null => null!, //invalid!
				"master" => new MasterRoleMetrics(data),
				"proxy" => new ProxyRoleMetrics(data),
				"commit_proxy" => new CommitProxyRoleMetrics(data),
				"grv_proxy" => new GrvProxyRoleMetrics(data),
				"resolver" => new ResolverRoleMetrics(data),
				"cluster_controller" => new ClusterControllerRoleMetrics(data),
				"log" => new LogRoleMetrics(data),
				"storage" => new StorageRoleMetrics(data),
				"ratekeeper" => new RateKeeperRoleMetrics(data),
				"data_distributor" => new DataDistributorRoleMetrics(data),
				_ => new ProcessRoleMetrics(data, role)
			};
		}

	}

	/// <summary>Metrics related to the <c>proxy_proxy</c> role</summary>
	public sealed record ProxyRoleMetrics : ProcessRoleMetrics
	{
		public ProxyRoleMetrics(JsonObject? data)
			: base(data, "proxy")
		{ }
	}

	/// <summary>Metrics related to the <c>commit_proxy</c> role</summary>
	public sealed record CommitProxyRoleMetrics : ProcessRoleMetrics
	{
		public CommitProxyRoleMetrics(JsonObject? data) : base(data, "commit_proxy")
		{ }

		private MetricStatistics? m_commitBatchingWindowSize;
		private MetricStatistics? m_commitLatencyStatistics;

		public MetricStatistics CommitBatchingWindowSize => m_commitBatchingWindowSize ??= new MetricStatistics(GetObject("commit_batching_window_size"));

		public MetricStatistics CommitLatencyStatistics => m_commitLatencyStatistics ??= new MetricStatistics(GetObject("commit_latency_statistics"));

	}

	/// <summary>Metrics related to the <c>grv_proxy</c> role</summary>
	public sealed record GrvProxyRoleMetrics : ProcessRoleMetrics
	{
		public GrvProxyRoleMetrics(JsonObject? data) : base(data, "grv_proxy")
		{ }

		private MetricStatistics? m_batchGrvLatencyStatistics;
		private MetricStatistics? m_defaultGrvLatencyStatistics;

		public MetricStatistics BatchGrvLatencyStatistics => m_batchGrvLatencyStatistics ??= new MetricStatistics(GetObject("grv_latency_statistics")?.GetObjectOrDefault("batch", null));

		public MetricStatistics DefaultGrvLatencyStatistics => m_defaultGrvLatencyStatistics ??= new MetricStatistics(GetObject("grv_latency_statistics")?.GetObjectOrDefault("default", null));

	}

	/// <summary>Characteristics of a measured value (count, max, avg, percentiles, ...)</summary>
	[DebuggerDisplay("Count={Count}, Mean={Mean}, Min={Min}, P25={P25}, Med={Median}, P90={P90}, P95={P95}, P99={P99}, P99.9={P999}, Max={Max}")]
	public sealed record MetricStatistics : MetricsBase
	{

		internal MetricStatistics(JsonObject? data) : base(data) { }

		/// <summary>Number of elements in the series</summary>
		public long Count => GetInt64("count") ?? 0;

		/// <summary>Average value</summary>
		public double Mean => GetDouble("mean") ?? 0;

		/// <summary>Minimum value (0th percentile)</summary>
		public double Min => GetDouble("min") ?? 0;

		/// <summary>25th percentile</summary>
		public double P25 => GetDouble("p925") ?? 0;

		/// <summary>Median value (50th percentile)</summary>
		public double Median => GetDouble("median") ?? 0;

		public double P90 => GetDouble("p90") ?? 0;

		/// <summary>95th percential</summary>
		public double P95 => GetDouble("p95") ?? 0;

		/// <summary>99th percentile</summary>
		public double P99 => GetDouble("p99") ?? 0;

		/// <summary>99.9th percentile</summary>
		public double P999 => GetDouble("p99.9") ?? 0;

		/// <summary>Maximum value (100th percentile)</summary>
		public double Max => GetDouble("max") ?? 0;

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Count={0:N0}, Min={1}, Med={2}, Max={3}", this.Count, this.Min, this.Median, this.Max);
		}

	}

	/// <summary>Metrics related to the <c>master</c> role</summary>
	public sealed record MasterRoleMetrics : ProcessRoleMetrics
	{
		public MasterRoleMetrics(JsonObject? data) : base(data, "master")
		{ }
	}

	/// <summary>Metrics related to the <c>resolver</c> role</summary>
	public sealed record ResolverRoleMetrics : ProcessRoleMetrics
	{
		public ResolverRoleMetrics(JsonObject? data) : base(data, "resolver")
		{ }
	}

	/// <summary>Metrics related to the <c>cluster_controller</c> role</summary>
	public sealed record ClusterControllerRoleMetrics : ProcessRoleMetrics
	{
		public ClusterControllerRoleMetrics(JsonObject? data) : base(data, "cluster_controller")
		{ }
	}

	/// <summary>Metrics related to the <c>ratekeeper</c> role</summary>
	public sealed record RateKeeperRoleMetrics : ProcessRoleMetrics
	{
		public RateKeeperRoleMetrics(JsonObject? data) : base(data, "ratekeeper")
		{ }
	}

	/// <summary>Metrics related to the <c>data_distributor</c> role</summary>
	public sealed record DataDistributorRoleMetrics : ProcessRoleMetrics
	{
		public DataDistributorRoleMetrics(JsonObject? data) : base(data, "data_distributor")
		{ }
	}

	public abstract record DiskBasedRoleMetrics : ProcessRoleMetrics
	{
		protected DiskBasedRoleMetrics(JsonObject? data, string role) : base(data, role)
		{
			this.DurableBytes = new RoughnessCounter(GetObject("durable_bytes"));
			this.InputBytes = new RoughnessCounter(GetObject("input_bytes"));
		}

		public long DataVersion => GetInt64("data_version") ?? 0;

		public RoughnessCounter DurableBytes { get; }

		public RoughnessCounter InputBytes { get; }

		public long KVStoreAvailableBytes => GetInt64("kvstore_available_bytes") ?? 0;

		public long KVStoreFreeBytes => GetInt64("kvstore_free_bytes") ?? 0;

		public long KVStoreTotalBytes => GetInt64("kvstore_total_bytes") ?? 0;

		public long KVStoreTotalNodes => GetInt64("kvstore_total_nodes") ?? 0;

		public long KVStoreTotalSize => GetInt64("kvstore_total_size") ?? 0;

		public long KVStoreUsedBytes => GetInt64("kvstore_used_bytes") ?? 0;

	}

	/// <summary>Metrics related to the <c>storage</c> role</summary>
	public sealed record StorageRoleMetrics : DiskBasedRoleMetrics
	{
		internal StorageRoleMetrics(JsonObject? data) : base(data, "storage")
		{
			this.StorageMetadata = new StorageMetadataMetrics(GetObject("storage_metadata"));
			this.BytesQueried = new RoughnessCounter(GetObject("bytes_queried"));
			this.FinishedQueries = new RoughnessCounter(GetObject("finished_queries"));
			this.KeysQueried = new RoughnessCounter(GetObject("keys_queried"));
			this.MutationBytes = new RoughnessCounter(GetObject("mutation_bytes"));
			this.Mutations = new RoughnessCounter(GetObject("mutations"));
			this.TotalQueries = new RoughnessCounter(GetObject("total_queries"));
			this.DataLag = new LagCounter(GetObject("data_lag"));
			this.DurabilityLag = new LagCounter(GetObject("durability_lag"));
			this.FetchedVersions = new RoughnessCounter(GetObject("fetched_versions"));
			this.FetchesFromLogs = new RoughnessCounter(GetObject("fetched_versions"));
			this.LowPriorityQueries = new RoughnessCounter(GetObject("low_priority_queries"));
			this.ReadLatencyStatistics = new MetricStatistics(GetObject("read_latency_statistics"));
		}

		public int QueryQueueMax => (int) (GetInt64("query_queue_max") ?? 0);

		public long StoredBytes => GetInt64("stored_bytes") ?? 0;

		public RoughnessCounter BytesQueried { get; }

		public RoughnessCounter FinishedQueries { get; }

		public RoughnessCounter KeysQueried { get; }

		public RoughnessCounter MutationBytes { get; }

		public RoughnessCounter Mutations { get; }

		public RoughnessCounter TotalQueries { get; }

		public RoughnessCounter FetchedVersions { get; }

		public RoughnessCounter FetchesFromLogs { get; }

		public LagCounter DataLag { get; }

		public long DurableVersion => GetInt64("durable_version") ?? 0;

		public LagCounter DurabilityLag { get; }

		public long LocalRate => GetInt64("local_rate") ?? 0; //note: int or double? (only seen '100')

		public RoughnessCounter LowPriorityQueries { get; }

		public MetricStatistics ReadLatencyStatistics { get; }

		/// <summary></summary>
		public StorageMetadataMetrics StorageMetadata { get; }

	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("StorageEngine={StorageEngine}")]
	public sealed record StorageMetadataMetrics : MetricsBase
	{

		internal StorageMetadataMetrics(JsonObject? data)
			: base(data)
		{
			this.StorageEngine = GetString("storage_engine");
			this.CreatedTimeDateTime = GetString("created_time_datetime");
			this.CreatedTimeStamp = GetDouble("created_time_timestamp");
		}

		/// <summary><c>storage_engine</c></summary>
		public string? StorageEngine { get; }

		/// <summary><c>created_time_datetime</c></summary>
		/// <remarks>Use <see cref="DateTimeOffset.Parse(string)"/> to parse this string</remarks>
		/// <example>"2023-10-14 17:12:53.128 +0000"</example>
		public string? CreatedTimeDateTime { get; }

		/// <summary><c>created_time_timestamp</c></summary>
		public double? CreatedTimeStamp { get; }

	}

	/// <summary>Metrics related to the <c>log</c> role</summary>
	public sealed record LogRoleMetrics : DiskBasedRoleMetrics
	{
		internal LogRoleMetrics(JsonObject? data) : base(data, "log")
		{ }

		public long QueueDiskAvailableBytes => GetInt64("queue_disk_available_bytes") ?? 0;

		public long QueueDiskFreeBytes => GetInt64("queue_disk_free_bytes") ?? 0;

		public long QueueDiskTotalBytes => GetInt64("queue_disk_total_bytes") ?? 0;

		public long QueueDiskUsedBytes => GetInt64("queue_disk_used_bytes") ?? 0;

	}

	/// <summary>List of well known process messages</summary>
	public static class ProcessMessages
	{

		public const string FileOpenError = "file_open_error";

		public const string UnableToWriteClusterFile = "unable_to_write_cluster_file";

		public const string IoError = "io_error";

		public const string PlatformError = "platform_error";

		public const string ProcessError = "process_error";

	}

	/// <summary>Memory performance counters for a FoundationDB process</summary>
	public sealed record ProcessMemoryMetrics : MetricsBase
	{
		internal ProcessMemoryMetrics(JsonObject? data) : base(data)
		{ }

		public long AvailableBytes => GetInt64("available_bytes") ?? 0;

		/// <summary>Memory allocated by the process</summary>
		public long UsedBytes => GetInt64("used_bytes") ?? 0;
		// On linux, this is the VmSize as reported by /proc/[pid]/statm
		// On Window, this is PPROCESS_MEMORY_COUNTERS.PagefileUsage

		/// <summary>Memory that has been allocated but is currently unused</summary>
		public long UnusedAllocatedMemory => GetInt64("unused_allocated_memory") ?? 0;
		//note: this is currently the sum of all unused memory in the various slab allocators

		public long LimitBytes => GetInt64("limit_bytes") ?? 0;

	}

	/// <summary>CPU performance counters for a FoundationDB process</summary>
	public sealed record ProcessCpuMetrics : MetricsBase
	{
		internal ProcessCpuMetrics(JsonObject? data) : base(data)
		{ }

		public double UsageCores => GetDouble("usage_cores") ?? 0;
	}

	/// <summary>Disk performance counters for a FoundationDB process</summary>
	[DebuggerDisplay("Busy={Busy}, Free={FreeBytes}/{TotalBytes}, Reads={Reads.Hz}, Writes={Writes.Hz}")]
	public sealed record ProcessDiskMetrics : MetricsBase
	{

		internal ProcessDiskMetrics(JsonObject? data) : base(data)
		{
			this.Busy = GetDouble("busy") ?? 0;
			this.FreeBytes = GetInt64("free_bytes") ?? 0;
			this.TotalBytes = GetInt64("total_bytes") ?? 0;
			this.Reads = new DiskCounter(GetObject("reads"));
			this.Writes = new DiskCounter(GetObject("writes"));
		}

		public double Busy { get; }

		public long FreeBytes { get; }

		public long TotalBytes { get; }

		public DiskCounter Reads { get; }

		public DiskCounter Writes { get; }

	}

	/// <summary>Network performance counters for a FoundationDB process or machine</summary>
	public sealed record ProcessNetworkMetrics : MetricsBase
	{
		internal ProcessNetworkMetrics(JsonObject? data) : base(data)
		{
			this.CurrentConnections = (int) (GetInt64("current_connections") ?? 0);
			this.MegabitsReceived = new RateCounter(GetObject("megabits_received"));
			this.MegabitsSent = new RateCounter(GetObject("megabits_sent"));
			this.ConnectionErrors = new RateCounter(GetObject("connection_errors"));
			this.ConnectionsClosed = new RateCounter(GetObject("connections_closed"));
			this.ConnectionsEstablished = new RateCounter(GetObject("connections_established"));
		}

		public RateCounter MegabitsReceived { get; }

		public RateCounter MegabitsSent { get; }

		public int CurrentConnections { get; }

		public RateCounter ConnectionErrors { get; }

		public RateCounter ConnectionsClosed { get; }

		public RateCounter ConnectionsEstablished { get; }

	}

	#endregion

	#region Machines...

	public sealed record MachineStatus : MetricsBase
	{

		internal MachineStatus(JsonObject? data, string id) : base(data)
		{
			this.Id = id;
		}

		private string? m_address;
		private MachineNetworkMetrics? m_network;
		private MachineCpuMetrics? m_cpu;
		private MachineMemoryMetrics? m_memory;
		private LocalityConfiguration? m_locality;

		/// <summary>Unique identifier for this machine.</summary>
		//TODO: is it stable across reboots? what are the conditions for a process to change its ID ?
		public string Id { get; }

		/// <summary>Identifier of the data center that is hosting this machine</summary>
		/// <remarks>All machines that have the same DataCenterId are probably running on the same (physical) network.</remarks>
		public string DataCenterId => GetString("datacenter_id") ?? string.Empty;

		/// <summary>Address of this machine</summary>
		/// <example>"10.1.2.34"</example>
		public string Address => m_address ??= GetString("address") ?? string.Empty;

		/// <summary>If true, this process is currently excluded from the cluster</summary>
		public bool Excluded => GetBoolean("excluded") ?? false;

		/// <summary>Network performance counters</summary>
		public MachineNetworkMetrics Network => m_network ??= new MachineNetworkMetrics(GetObject("network"));

		/// <summary>CPU performance counters</summary>
		public MachineCpuMetrics Cpu => m_cpu ??= new MachineCpuMetrics(GetObject("cpu"));

		/// <summary>Memory performance counters</summary>
		public MachineMemoryMetrics Memory => m_memory ??= new MachineMemoryMetrics(GetObject("memory"));

		public LocalityConfiguration Locality => m_locality ??= new LocalityConfiguration(GetObject("locality"));

		public int ContributingWorkers => (int) (GetInt64("contributing_workers") ?? 0);
	}

	/// <summary>Memory performance counters for machine hosting one or more FoundationDB processes</summary>
	public sealed record MachineMemoryMetrics : MetricsBase
	{
		internal MachineMemoryMetrics(JsonObject? data) : base(data)
		{
			this.CommittedBytes = GetInt64("committed_bytes") ?? 0;
			this.FreeBytes = GetInt64("free_bytes") ?? 0;
			this.TotalBytes = GetInt64("total_bytes") ?? 0;
		}

		public long CommittedBytes { get; }

		public long FreeBytes { get; }

		public long TotalBytes { get; }

	}

	/// <summary>CPU performance counters for machine hosting one or more FoundationDB processes</summary>
	public sealed record MachineCpuMetrics : MetricsBase
	{
		internal MachineCpuMetrics(JsonObject? data) : base(data)
		{
			this.LogicalCoreUtilization = GetDouble("logical_core_utilization") ?? 0;
		}

		public double LogicalCoreUtilization { get; }

	}

	/// <summary>Network performance counters for machine hosting one or more FoundationDB processes</summary>
	public sealed record MachineNetworkMetrics : MetricsBase
	{
		internal MachineNetworkMetrics(JsonObject? data) : base(data)
		{
			this.MegabitsReceived = new RateCounter(GetObject("megabits_received"));
			this.MegabitsSent = new RateCounter(GetObject("megabits_sent"));
			this.TcpSegmentsRetransmitted = new RateCounter(GetObject("tcp_segments_retransmitted"));
		}

		public RateCounter MegabitsReceived { get; }

		public RateCounter MegabitsSent { get; }

		public RateCounter TcpSegmentsRetransmitted { get; }

	}

	public sealed record LocalityConfiguration : MetricsBase
	{
		internal LocalityConfiguration(JsonObject? data) : base(data)
		{
			this.MachineId = GetString("machineid") ?? string.Empty;
			this.ProcessId = GetString("processid") ?? string.Empty;
			this.ZoneId = GetString("zoneid") ?? string.Empty;
		}

		public string MachineId { get; }

		public string ProcessId { get; }

		public string ZoneId { get; }

	}

	#endregion

}
