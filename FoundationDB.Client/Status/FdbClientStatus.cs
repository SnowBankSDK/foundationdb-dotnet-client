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

namespace FoundationDB.Client.Status
{

	public sealed record FdbClientStatus : MetricsBase
	{
		internal FdbClientStatus(JsonObject? doc, Slice raw)
			: base(doc)
		{
			this.RawData = raw;
		}

		/// <summary>Raw JSON data of this snapshot.</summary>
		/// <remarks>This is the same value that is returned by calling the native 'fdb_database_get_client_status' API.</remarks>
		public Slice RawData { get; }

		/// <summary>Raw JSON text of this snapshot.</summary>
		/// <remarks>This is the same value that is returned by calling the native 'fdb_database_get_client_status' API, decoded as utf-8</remarks>
		public string RawJson => this.RawData.ToStringUtf8() ?? string.Empty;

		/// <summary>Parsed JSON data of this snapshot.</summary>
		public JsonObject? JsonData => m_data;
		
		public bool? Healthy => GetBoolean("Healthy");

		public int? InitializationError => GetInt32("InitializationError");

		public int? NumConnectionsFailed => GetInt32("NumConnectionsFailed");

		public string? ClusterId => GetString("ClusterID");

		public FdbEndPoint CurrentCoordinator => m_currentCoordinator ??= GetEndpoint("CurrentCoordinator");
		private FdbEndPoint? m_currentCoordinator;

		public FdbEndPoint[] Coordinators => m_coordinators ??= GetEndpoints("Coordinators");
		private FdbEndPoint[]? m_coordinators;

		public FdbEndPoint[] CommitProxies => m_commitProxies ??= GetEndpoints("CommitProxies");
		private FdbEndPoint[]? m_commitProxies;

		public FdbEndPoint[] GrvProxies => m_grvProxies ??= GetEndpoints("GrvProxies");
		private FdbEndPoint[]? m_grvProxies;

		public ConnectionStatus[] Connections => m_connections ??= GetArray("Connections")?.Select(obj => new ConnectionStatus(JsonValueExtensions.AsObjectOrDefault(obj))).ToArray() ?? [];
		private ConnectionStatus[]? m_connections;

		public StorageServerStatus[] StorageServers => m_storageServers ??= GetArray("StorageServers")?.Select(obj => new StorageServerStatus(obj.AsObjectOrDefault())).ToArray() ?? [];
		private StorageServerStatus[]? m_storageServers;

		#region Nested Types...

		public sealed record ConnectionStatus : MetricsBase
		{
			internal ConnectionStatus(JsonObject? data) : base(data) { }

			public FdbEndPoint Address => m_address ??= GetEndpoint("Address");
			private FdbEndPoint? m_address;

			public string? Status => GetString("Status");

			public FdbProtocolVersion ProtocolVersion => FdbProtocolVersion.Parse(GetString("ProtocolVersion") ?? string.Empty);
			//note: sometimes missing?

			public bool? Compatible => GetBoolean("Compatible");

			public long? BytesReceived => GetInt64("BytesReceived");

			public long? BytesSent => GetInt64("BytesSent");

			public long? PingCount => GetInt64("PingCount");

			public long? PingTimeoutCount => GetInt64("PingTimeoutCount");

			public double? BytesSampleTime => GetDouble("BytesSampleTime");

			public double? LastConnectTime => GetDouble("LastConnectTime");

		}

		public sealed record StorageServerStatus : MetricsBase
		{
			internal StorageServerStatus(JsonObject? data) : base(data) { }

			public FdbEndPoint Address => m_address ??= GetEndpoint("Address");
			private FdbEndPoint? m_address;

			public string? SSID => GetString("SSID");

		}

		#endregion

	}

}
