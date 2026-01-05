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

//#define TRACE_COUNTING

// ReSharper disable VariableLengthStringHexEscapeSequence
namespace FoundationDB.Client
{
	using System.Net;
	using System.Threading.Tasks;
	using FoundationDB.Client.Status;

	public static partial class Fdb
	{

		/// <summary>Helper class for reading from the reserved System subspace</summary>
		[PublicAPI]
		public static class System
		{
			//REVIEW: what happens if someone mutates (by mistake or not) the underlying buffer of the defaults keys ?
			// => eg. Fdb.System.MaxValue.Array[0] = 42;

			/// <summary>"\xFF\xFF"</summary>
			public static readonly Slice MaxValue = Slice.FromBytes([ 0xFF, 0xFF ]);

			/// <summary>"\xFF\x00"</summary>
			public static readonly Slice MinValue = Slice.FromBytes([ 0xFF, 0x00 ]);

			/// <summary>"\xFF\xFF"</summary>
			public static readonly Slice SpecialKeyPrefix = Slice.FromBytes([ 0xFF, 0xFF ]);

			/// <summary>"\xFF/metadataVersion"</summary>
			public static readonly Slice MetadataVersionKey = Slice.FromByteString("\xff/metadataVersion");

			/// <summary>"\xFF/metadataVersion\x00"</summary>
			/// <remarks>Used to add a read conflict range on the metadataVersion key</remarks>
			internal static readonly Slice MetadataVersionKeyEnd = Slice.FromByteString("\xff/metadataVersion\x00");

			/// <summary>Placeholder value used when updating the metadataVersion key (80-bit version stamp + 32 bit offset)</summary>
			internal static readonly Slice MetadataVersionValue = Slice.Zero(14);

			/// <summary>"\xFF/backupDataFormat"</summary>
			public static readonly Slice BackupDataFormat = Slice.FromByteString("\xFF/backupDataFormat");

			/// <summary>`\xFF/conf/...`</summary>
			public static readonly Slice ConfigPrefix = Slice.FromByteString("\xFF/conf/");

			/// <summary>`\xFF/coordinators`</summary>
			public static readonly Slice Coordinators = Slice.FromByteString("\xFF/coordinators");

			/// <summary>`\xFF/globals/...`</summary>
			public static readonly Slice GlobalsPrefix = Slice.FromByteString("\xFF/globals/");

			/// <summary><c>`\xFF/init_id`</c></summary>
			public static readonly Slice InitId = Slice.FromByteString("\xFF/init_id");

			/// <summary><c>`\xFF/keyServer/(key_boundary)`</c> => (..., node_id, ...)</summary>
			public static readonly Slice KeyServers = Slice.FromByteString("\xFF/keyServers/");

			/// <summary><c>`\xFF/serverKeys/(node_id)/(key_boundary)`</c> => ('' | '1')</summary>
			public static readonly Slice ServerKeys = Slice.FromByteString("\xFF/serverKeys/");

			/// <summary><c>`\xFF/serverList/(node_id)`</c> => (..., node_id, machine_id, datacenter_id, ...)</summary>
			public static readonly Slice ServerList = Slice.FromByteString("\xFF/serverList/");

			/// <summary><c>`\xFF/workers/(ip:port)/...`</c> => datacenter + machine + mclass</summary>
			public static readonly Slice WorkersPrefix = Slice.FromByteString("\xFF/workers/");

			/// <summary><c>`\xFF\xFF/status/json`</c> => { JSON }</summary>
			public static readonly Slice StatusJson = Slice.FromByteString("\xFF/\xFF/status/json");

			/// <summary><c>`\xFF\xFF/transaction/conflicting_keys/`...</c> => ('0' | '1')</summary>
			public static readonly Slice TransactionConflictingKeysPrefix = Slice.FromByteString("\xFF\xFF/transaction/conflicting_keys/");

			#region JSON Status

			/// <summary>Query the current status of the cluster</summary>
			/// <returns>Task that returns a <see cref="FdbSystemStatus"/> instance that wraps the parsed JSON document.</returns>
			/// <remarks>
			/// <para>This method will read the <c>`\xFF\xFF/status/json`</c> special key, and parse the result JSON bytes.</para>
			/// </remarks>
			public static async Task<FdbSystemStatus> GetStatusAsync(IFdbReadOnlyTransaction trans)
			{
				Contract.NotNull(trans);

				// read the special key "\xFF\xFF/status/json"
				var doc = await trans.GetAsync(
					FdbSystemKey.StatusJson,
					(value, found) => found ? CrystalJson.Parse(value).AsObject() : null
				).ConfigureAwait(false);

				// grab the read version if we have valid data
				long rv = 0;
				if (doc?.ContainsKey("cluster") ?? false)
				{
					rv = await trans.GetReadVersionAsync().ConfigureAwait(false);
				}

				return new FdbSystemStatus(doc, rv);
			}

			/// <summary>Queries the current status of the cluster</summary>
			/// <returns>Task that returns a <see cref="FdbSystemStatus"/> instance that wraps the parsed JSON document.</returns>
			/// <remarks>
			/// <para>This method will read the <c>`\xFF\xFF/status/json`</c> special key, and parse the result JSON bytes.</para>
			/// </remarks>
			public static Task<FdbSystemStatus> GetStatusAsync(IFdbDatabase db, CancellationToken ct)
			{
				Contract.NotNull(db);

				// we should not retry the read to the status key!
				return db.ReadAsync(static tr =>
				{
					tr.Options.WithPrioritySystemImmediate();
					//note: in v3.x, the status key does not need the access to system key option.

					//TODO: set a custom timeout?
					return GetStatusAsync(tr);
				}, ct);
			}

			/// <summary>Queries the current status of the cluster</summary>
			/// <returns>Task that returns a <see cref="FdbSystemStatus"/> instance that wraps the parsed JSON document.</returns>
			/// <remarks>
			/// <para>This method will read the <c>`\xFF\xFF/status/json`</c> special key, and parse the result JSON bytes.</para>
			/// </remarks>
			public static async Task<FdbSystemStatus> GetStatusAsync(IFdbDatabaseProvider db, CancellationToken ct)
			{
				return await GetStatusAsync(await db.GetDatabase(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
			}

			/// <summary>Queries the current status of the client</summary>
			/// <returns>Task that returns a <see cref="FdbClientStatus"/> instance that wraps the parsed JSON document.</returns>
			/// <remarks>
			/// <para>Requires API version <c>730</c> or greater</para>
			/// </remarks>
			public static async Task<FdbClientStatus> GetClientStatusAsync(IFdbDatabase db, CancellationToken ct)
			{
				Contract.NotNull(db);

				// we should not retry the read to the status key!
				var data = await db.GetClientStatus(ct).ConfigureAwait(false);

				if (data.IsNullOrEmpty) return new (null, Slice.Nil);

				var doc = CrystalJson.Parse(data).AsObject();

				return new FdbClientStatus(doc, data);
			}

			/// <summary>Queries the current status of the client</summary>
			/// <remarks>
			/// <para>Requires API version <c>730</c> or greater</para>
			/// </remarks>
			public static async Task<FdbClientStatus> GetClientStatusAsync(IFdbDatabaseProvider db, CancellationToken ct)
			{
				return await GetClientStatusAsync(await db.GetDatabase(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
			}

			#endregion

			/// <summary>Returns an object describing the list of the current coordinators for the cluster</summary>
			/// <param name="tr">Transaction to use for the operation</param>
			/// <remarks>
			/// <para>Caution: This operation will set the <see cref="FdbTransactionOption.ReadSystemKeys"/> and <see cref="FdbTransactionOption.PrioritySystemImmediate"/> options on the transaction.
			/// You should avoid mixing this method and other read-heavy operations in the same transaction.</para>
			/// <para>Since the list of coordinators may change at any time, the results may already be obsolete once this method completes!</para>
			/// </remarks>
			public static async Task<FdbClusterConnectionString> GetCoordinatorsAsync(IFdbReadOnlyTransaction tr)
			{
				Contract.NotNull(tr);

				tr.Options.WithReadAccessToSystemKeys();
				tr.Options.WithPrioritySystemImmediate();
				//note: we ask for high priority, because this method maybe called by a monitoring system than has to run when the cluster is clogged up in requests

				var coordinators = await tr.GetAsync(FdbKey.ToSystemKey("/coordinators")).ConfigureAwait(false);
				if (coordinators.IsNull) throw new InvalidOperationException("Failed to read the list of coordinators from the cluster's system keyspace.");

				return FdbClusterConnectionString.Parse(coordinators.ToStringAscii()!);
			}

			/// <summary>Returns an object describing the list of the current coordinators for the cluster</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <remarks>Since the list of coordinators may change at any time, the results may already be obsolete once this method completes!</remarks>
			public static Task<FdbClusterConnectionString> GetCoordinatorsAsync(IFdbDatabase db, CancellationToken ct)
			{
				Contract.NotNull(db);

				return db.ReadAsync(GetCoordinatorsAsync, ct);
			}

			/// <summary>Returns an object describing the list of the current coordinators for the cluster</summary>
			/// <param name="db">Database Provider to use for the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <remarks>Since the list of coordinators may change at any time, the results may already be obsolete once this method completes!</remarks>
			public static Task<FdbClusterConnectionString> GetCoordinatorsAsync(IFdbDatabaseProvider db, CancellationToken ct)
			{
				Contract.NotNull(db);

				return db.ReadAsync(GetCoordinatorsAsync, ct);
			}

			public static bool TryParseAddress(ReadOnlySpan<char> address, [MaybeNullWhen(false)] out IPAddress host, out int port, out bool tls)
			{
				host = null;
				port = 0;
				tls = false;
				if (address.IsEmpty)
				{
					return false;
				}

				if (address.EndsWith(":tls"))
				{
					tls = true;
					address = address[..^4];
				}

				// split host and port
				int p = address.IndexOf(':');
				if (p <= 0) return false;

				// parse host (we assume an IP address)
				// TODO: change this if it could be a host name
				if (!IPAddress.TryParse(address[..p], out host))
				{
					return false;
				}

				// parse the port
				if (!int.TryParse(address[(p + 1)..], out port))
				{
					return false;
				}

				return true;
			}

			#region Special Keys...

			/// <summary>Returns the value of a special key (located under the <c>\xFF\xFF</c> prefix)</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the special key (ex: <c>`/management/tenant_mode`</c>)</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of <c>\xFF\xFF/management/tenant_mode</c></returns>
			public static Task<Slice> GetSpecialKeyAsync(IFdbDatabase db, Slice name, CancellationToken ct)
			{
				Contract.NotNull(db);
				if (name.IsNullOrEmpty) throw new ArgumentException("Special key name cannot be null or empty", nameof(name));

				return db.ReadAsync<Slice>((tr) =>
				{
					tr.Options.WithPrioritySystemImmediate();
					//note: we ask for high priority, because this method maybe called by a monitoring system than has to run when the cluster is clogged up in requests

					return tr.GetAsync(SpecialKey(name));
				}, ct);
			}

			/// <summary>Returns the value of a special key (located under the <c>\xFF\xFF</c> prefix)</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the special key (ex: <c>`/management/tenant_mode`</c>)</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of <c>\xFF\xFF/management/tenant_mode</c></returns>
			public static async Task<Slice> GetSpecialKeyAsync(IFdbDatabaseProvider db, Slice name, CancellationToken ct)
			{
				return await GetSpecialKeyAsync(await db.GetDatabase(ct).ConfigureAwait(false), name, ct).ConfigureAwait(false);
			}

			/// <summary>Returns the value of a special key (located under the <c>\xFF\xFF</c> prefix)</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the special key (ex: <c>"/management/tenant_mode"</c>)</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of <c>\xFF\xFF/management/tenant_mode</c></returns>
			public static Task<Slice> GetSpecialKeyAsync(IFdbDatabase db, string name, CancellationToken ct)
			{
				Contract.NotNull(db);
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Special key name cannot be null or empty", nameof(name));

				return db.ReadAsync<Slice>((tr) =>
				{
					tr.Options.WithPrioritySystemImmediate();
					//note: we ask for high priority, because this method maybe called by a monitoring system than has to run when the cluster is clogged up in requests

					return tr.GetAsync(SpecialKey(name));
				}, ct);
			}

			/// <summary>Returns the value of a special key (located under the <c>\xFF\xFF</c> prefix)</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the special key (ex: <c>`/management/tenant_mode`</c>)</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of <c>\xFF\xFF/management/tenant_mode</c></returns>
			public static async Task<Slice> GetSpecialKeyAsync(IFdbDatabaseProvider db, string name, CancellationToken ct)
			{
				return await GetSpecialKeyAsync(await db.GetDatabase(ct).ConfigureAwait(false), name, ct).ConfigureAwait(false);
			}

			/// <summary>Returns the corresponding key for a special key attribute</summary>
			/// <param name="name">Name of the special key, for example: <c>`/foo/bar`</c></param>
			/// <returns>Name prefixed by <c>\xFF\xFF</c>, for example: <c>`\xFF\xFF/foo/bar`</c></returns>
			/// <example><c>SpecialKey(Slice.FromString("/foo/bar"))</c> => <c>`\xFF\xFF/foo/bar`</c></example>
			public static Slice SpecialKey(Slice name)
			{
				if (name.IsNullOrEmpty) throw new ArgumentException("Special key name cannot be null or empty", nameof(name));
				return SpecialKeyPrefix + name;
			}

			/// <summary>Returns the corresponding key for a special key attribute</summary>
			/// <param name="name">Name of the special key, for example: <c>"/foo/bar"</c></param>
			/// <returns>Name prefixed by <c>\xFF\xFF</c>, for example: <c>"\xFF\xFF/foo/bar"</c></returns>
			/// <example><c>SpecialKey("/foo/bar")</c> => <c>`\xFF\xFF/foo/bar`</c></example>
			public static Slice SpecialKey(string name)
			{
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Special key name cannot be null or empty", nameof(name));
				return SpecialKeyPrefix + Slice.FromByteString(name);
			}

			#endregion

			#region Config Parameters...

			/// <summary>Returns the value of a configuration parameter (located under '\xFF/conf/')</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the configuration key (ex: "storage_engine")</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of '\xFF/conf/storage_engine'</returns>
			public static Task<Slice> GetConfigParameterAsync(IFdbDatabase db, Slice name, CancellationToken ct)
			{
				Contract.NotNull(db);
				if (name.IsNullOrEmpty) throw new ArgumentException("Config key name cannot be null or empty", nameof(name));

				return db.ReadAsync<Slice>((tr) =>
				{
					tr.Options.WithReadAccessToSystemKeys();
					tr.Options.WithPrioritySystemImmediate();
					//note: we ask for high priority, because this method maybe called by a monitoring system than has to run when the cluster is clogged up in requests

					return tr.GetAsync(Fdb.System.ConfigKey(name));
				}, ct);
			}

			/// <summary>Returns the value of a configuration parameter (located under '\xFF/conf/')</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the configuration key (ex: "storage_engine")</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of '\xFF/conf/storage_engine'</returns>
			public static async Task<Slice> GetConfigParameterAsync(IFdbDatabaseProvider db, Slice name, CancellationToken ct)
			{
				return await GetConfigParameterAsync(await db.GetDatabase(ct).ConfigureAwait(false), name, ct).ConfigureAwait(false);
			}

			/// <summary>Returns the value of a configuration parameter (located under '\xFF/conf/')</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the configuration key (ex: "storage_engine")</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of '\xFF/conf/storage_engine'</returns>
			public static Task<Slice> GetConfigParameterAsync(IFdbDatabase db, string name, CancellationToken ct)
			{
				Contract.NotNull(db);
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Config key name cannot be null or empty", nameof(name));

				return db.ReadAsync<Slice>((tr) =>
				{
					tr.Options.WithReadAccessToSystemKeys();
					tr.Options.WithPrioritySystemImmediate();
					//note: we ask for high priority, because this method maybe called by a monitoring system than has to run when the cluster is clogged up in requests

					return tr.GetAsync(Fdb.System.ConfigKey(name));
				}, ct);
			}

			/// <summary>Returns the value of a configuration parameter (located under '\xFF/conf/')</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="name">Name of the configuration key (ex: "storage_engine")</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Value of '\xFF/conf/storage_engine'</returns>
			public static async Task<Slice> GetConfigParameterAsync(IFdbDatabaseProvider db, string name, CancellationToken ct)
			{
				return await GetConfigParameterAsync(await db.GetDatabase(ct).ConfigureAwait(false), name, ct).ConfigureAwait(false);
			}

			/// <summary>Return the corresponding key for a config attribute</summary>
			/// <param name="name">"foo"</param>
			/// <returns>"\xFF/conf/foo"</returns>
			public static Slice ConfigKey(Slice name)
			{
				if (name.IsNullOrEmpty) throw new ArgumentException("Config key name cannot be null or empty", nameof(name));
				return ConfigPrefix + name;
			}

			/// <summary>Return the corresponding key for a config attribute</summary>
			/// <param name="name">"foo"</param>
			/// <returns>"\xFF/conf/foo"</returns>
			public static Slice ConfigKey(string name)
			{
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Config key name cannot be null or empty", nameof(name));
				return ConfigPrefix + Slice.FromByteString(name);
			}

			#endregion

			/// <summary>Return the corresponding key for a global attribute</summary>
			/// <param name="name"><c>"foo"</c></param>
			/// <returns><c>"\xFF/globals/foo"</c></returns>
			public static Slice GlobalsKey(string name)
			{
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Attribute name cannot be null or empty", nameof(name));
				return GlobalsPrefix + Slice.FromByteString(name);
			}

			/// <summary>Return the corresponding key for a global attribute</summary>
			/// <param name="id"><c>"ABC123"</c></param>
			/// <param name="name"><c>"foo"</c></param>
			/// <returns><c>"\xFF/workers/ABC123/foo"</c></returns>
			public static Slice WorkersKey(string id, string name)
			{
				if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id cannot be null or empty", nameof(id));
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Attribute name cannot be null or empty", nameof(name));
				return WorkersPrefix + Slice.FromByteString(id) + Slice.FromChar('/') + Slice.FromByteString(name);
			}

			/// <summary>Returns the current storage engine mode of the cluster</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Returns either "memory" or "ssd"</returns>
			/// <remarks>Will return a string starting with "unknown" if the storage engine mode is not recognized</remarks>
			public static async Task<string> GetStorageEngineModeAsync(IFdbDatabase db, CancellationToken ct)
			{
				// The '\xFF/conf/storage_engine' keys has value "0" for ssd-1 engine, "1" for memory engine and "2" for ssd-2 engine

				var value = await GetConfigParameterAsync(db, "storage_engine", ct).ConfigureAwait(false);

				if (value.IsNull) throw new InvalidOperationException("Failed to read the storage engine mode from the cluster's system keyspace");

				switch(value.ToUnicode())
				{
					case "0": return "ssd-1"; // was called "ssd" in earlier versions
					case "1": return "memory";
					case "2": return "ssd-2";
					case "3": return "ssd-redwood-1-experimental";
					case "4": return "memory-radixtree-beta";
					case "5": return "ssd-rocksdb-v1";
					default:
					{
						// welcome to the future!
						return "unknown(" + value.PrettyPrint() + ")";
					}
				}
			}

			/// <summary>Returns the current storage engine mode of the cluster</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Returns either "memory" or "ssd"</returns>
			/// <remarks>Will return a string starting with "unknown" if the storage engine mode is not recognized</remarks>
			public static async Task<string> GetStorageEngineModeAsync(IFdbDatabaseProvider db, CancellationToken ct)
			{
				return await GetStorageEngineModeAsync(await db.GetDatabase(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
			}

			/// <summary>Returns a list of keys k such that <paramref name="beginInclusive"/> &lt;= k &lt; <paramref name="endExclusive"/> and k is located at the start of a contiguous range stored on a single server</summary>
			/// <param name="trans">Transaction to use for the operation</param>
			/// <param name="beginInclusive">First key (inclusive) of the range to inspect</param>
			/// <param name="endExclusive">End key (exclusive) of the range to inspect</param>
			/// <returns>List of keys that mark the start of a new chunk</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Transaction object it is passed, but the returned boundaries are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<Slice>> GetBoundaryKeysAsync(IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive)
			{
				Contract.NotNull(trans);
				Contract.Debug.Requires(trans.Context != null && trans.Context.Database != null);

				var readVersion = await trans.GetReadVersionAsync().ConfigureAwait(false);

				// We don't want to change the state of the transaction, so we will create another one at the same read version
				return await trans.Context.Database.ReadAsync(shadow =>
				{
					shadow.SetReadVersion(readVersion);

					//TODO: we may need to also copy options like RetryLimit and Timeout ?

					return GetBoundaryKeysInternalAsync(shadow, beginInclusive, endExclusive);
				}, trans.Cancellation).ConfigureAwait(false);
			}

			/// <summary>Returns a list of keys <c>k</c> such that <paramref name="beginInclusive"/> &lt;= <c>k</c> &lt; <paramref name="endExclusive"/> and <c>k</c> is located at the start of a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="beginInclusive">First key (inclusive) of the range to inspect</param>
			/// <param name="endExclusive">End key (exclusive) of the range to inspect</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>List of keys that mark the start of a new chunk</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned boundaries are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static Task<List<Slice>> GetBoundaryKeysAsync(IFdbDatabase db, Slice beginInclusive, Slice endExclusive, CancellationToken ct)
			{
				Contract.NotNull(db);

				return db.ReadAsync((trans) => GetBoundaryKeysInternalAsync(trans, beginInclusive, endExclusive), ct);
			}

			/// <summary>Returns a list of keys <c>k</c> such that <paramref name="beginInclusive"/> &lt;= <c>k</c> &lt; <paramref name="endExclusive"/> and <c>k</c> is located at the start of a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="beginInclusive">First key (inclusive) of the range to inspect</param>
			/// <param name="endExclusive">End key (exclusive) of the range to inspect</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>List of keys that mark the start of a new chunk</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned boundaries are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<Slice>> GetBoundaryKeysAsync(IFdbDatabaseProvider db, Slice beginInclusive, Slice endExclusive, CancellationToken ct)
			{
				return await GetBoundaryKeysAsync(await db.GetDatabase(ct).ConfigureAwait(false), beginInclusive, endExclusive, ct).ConfigureAwait(false);
			}

			//REVIEW: should we call this chunks? shard? fragments? contiguous ranges?

			/// <summary>Split a range of keys into smaller chunks where each chunk represents a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="range">Range of keys to split up into smaller chunks</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>List of one or more chunks that constitutes the range, where each chunk represents a contiguous range stored on a single server. If the list contains a single range, that means that the range is small enough to fit inside a single chunk.</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned ranges are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static Task<List<KeyRange>> GetChunksAsync(IFdbDatabase db, KeyRange range, CancellationToken ct)
			{
				//REVIEW: maybe rename this to SplitIntoChunksAsync or SplitIntoShardsAsync or GetFragmentsAsync ?
				return GetChunksAsync(db, range.Begin, range.End, ct);
			}

			/// <summary>Split a range of keys into smaller chunks where each chunk represents a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="range">Range of keys to split up into smaller chunks</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>List of one or more chunks that constitutes the range, where each chunk represents a contiguous range stored on a single server. If the list contains a single range, that means that the range is small enough to fit inside a single chunk.</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned ranges are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<KeyRange>> GetChunksAsync(IFdbDatabaseProvider db, KeyRange range, CancellationToken ct)
			{
				return await GetChunksAsync(await db.GetDatabase(ct).ConfigureAwait(false), range.Begin, range.End, ct).ConfigureAwait(false);
			}

			/// <summary>Split a range of keys into chunks representing a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="beginInclusive">First key (inclusive) of the range to inspect</param>
			/// <param name="endExclusive">End key (exclusive) of the range to inspect</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>List of one or more chunks that constitutes the range, where each chunk represents a contiguous range stored on a single server. If the list contains a single range, that means that the range is small enough to fit inside a single chunk.</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned ranges are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<KeyRange>> GetChunksAsync(IFdbDatabase db, Slice beginInclusive, Slice endExclusive, CancellationToken ct)
			{
				//REVIEW: maybe rename this to SplitIntoChunksAsync or SplitIntoShardsAsync or GetFragmentsAsync ?

				Contract.NotNull(db);
				if (endExclusive < beginInclusive) throw new ArgumentException("The end key cannot be less than the begin key", nameof(endExclusive));

				var boundaries = await GetBoundaryKeysAsync(db, beginInclusive, endExclusive, ct).ConfigureAwait(false);

				int count = boundaries.Count;
				var chunks = new List<KeyRange>(count + 2);

				if (count == 0)
				{ // the range does not cross any boundary, and is contained in just one chunk
					chunks.Add(new KeyRange(beginInclusive, endExclusive));
					return chunks;
				}

				var k = boundaries[0];
				if (k != beginInclusive) chunks.Add(new KeyRange(beginInclusive, k));

				for (int i = 1; i < boundaries.Count; i++)
				{
					chunks.Add(new KeyRange(k, boundaries[i]));
					k = boundaries[i];
				}

				if (k != endExclusive) chunks.Add(new KeyRange(k, endExclusive));

				return chunks;
			}

			/// <summary>Split a range of keys into chunks representing a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="beginInclusive">First key (inclusive) of the range to inspect</param>
			/// <param name="endExclusive">End key (exclusive) of the range to inspect</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>List of one or more chunks that constitutes the range, where each chunk represents a contiguous range stored on a single server. If the list contains a single range, that means that the range is small enough to fit inside a single chunk.</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned ranges are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<KeyRange>> GetChunksAsync(IFdbDatabaseProvider db, Slice beginInclusive, Slice endExclusive, CancellationToken ct)
			{
				return await GetChunksAsync(await db.GetDatabase(ct).ConfigureAwait(false), beginInclusive, endExclusive, ct).ConfigureAwait(false);
			}

			private static async Task<List<Slice>> GetBoundaryKeysInternalAsync(IFdbReadOnlyTransaction trans, Slice begin, Slice end)
			{
				Contract.Debug.Requires(trans != null && end >= begin);

#if TRACE_COUNTING
				trans.Annotate("Get boundary keys in range {0}", KeyRange.Create(begin, end));
#endif

				trans.Options.WithReadAccessToSystemKeys();

				var results = new List<Slice>();
				int iterations = 0;
				while (begin < end)
				{
					FdbException? error = null;
					Slice lastBegin = begin;
					try
					{
						var chunk = await trans.Snapshot.GetRangeAsync(KeyServers + begin, KeyServers + end, FdbRangeOptions.WantAll, iterations).ConfigureAwait(false);
						++iterations;
						if (chunk.Count > 0)
						{
							foreach (var kvp in chunk)
							{
								results.Add(kvp.Key.Substring(KeyServers.Count));
							}
							begin = chunk.Last.Substring(KeyServers.Count) + 0;
						}
						if (!chunk.HasMore)
						{
							begin = end;
						}
					}
					catch (FdbException e)
					{
						error = e;
					}

					if (error != null)
					{
						if (error.Code == FdbError.TransactionTooOld && begin != lastBegin)
						{ // if we get a PastVersion and *something* has happened, then we are no longer transactional
							trans.Reset();
						}
						else
						{
							await trans.OnErrorAsync(error.Code).ConfigureAwait(false);
						}
						iterations = 0;
						trans.Options.WithReadAccessToSystemKeys();
					}
				}

#if TRACE_COUNTING
				if (results.Count == 0)
				{
					trans.Annotate("There is no chunk boundary in range {0}", KeyRange.Create(begin, end));
				}
				else
				{
					trans.Annotate("Found {0} boundaries in {1} iteration(s)", results.Count, iterations);
				}
#endif

				return results;
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync(IFdbDatabase db, KeyRange range, CancellationToken ct)
			{
				return EstimateCountAsync(db, range.Begin, range.End, null, ct);
				//BUGBUG: REFACTORING: deal with null value for End!
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync<TKeyRange>(IFdbDatabase db, in TKeyRange range, CancellationToken ct)
				where TKeyRange : struct, IFdbKeyRange
			{
				var r = range.ToKeyRange();
				return EstimateCountAsync(db, r.Begin, r.End, null, ct);
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync(IFdbDatabaseProvider db, KeyRange range, CancellationToken ct)
			{
				return EstimateCountAsync(db, range.Begin, range.End, null, ct);
				//BUGBUG: REFACTORING: deal with null value for End!
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="onProgress">Optional callback called every time the count is updated. The first argument is the current count, and the second argument is the last key that was found.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync(IFdbDatabase db, KeyRange range, IProgress<(long Count, Slice Current)> onProgress, CancellationToken ct)
			{
				return EstimateCountAsync(db, range.Begin, range.End, onProgress, ct);
				//BUGBUG: REFACTORING: deal with null value for End!
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="onProgress">Optional callback called every time the count is updated. The first argument is the current count, and the second argument is the last key that was found.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync<TKeyRange>(IFdbDatabase db, in TKeyRange range, IProgress<(long Count, Slice Current)> onProgress, CancellationToken ct)
				where TKeyRange : struct, IFdbKeyRange
			{
				var r = range.ToKeyRange();
				return EstimateCountAsync(db, r.Begin, r.End, onProgress, ct);
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="onProgress">Optional callback called every time the count is updated. The first argument is the current count, and the second argument is the last key that was found.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync(IFdbDatabaseProvider db, KeyRange range, IProgress<(long Count, Slice Current)> onProgress, CancellationToken ct)
			{
				return EstimateCountAsync(db, range.Begin, range.End, onProgress, ct);
				//BUGBUG: REFACTORING: deal with null value for End!
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="beginInclusive">Key defining the beginning of the range</param>
			/// <param name="endExclusive">Key defining the end of the range</param>
			/// <param name="onProgress">Optional callback called every time the count is updated. The first argument is the current count, and the second argument is the last key that was found.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that <paramref name="beginInclusive"/> &lt;= k &gt; <paramref name="endExclusive"/></returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static async Task<long> EstimateCountAsync(IFdbDatabase db, Slice beginInclusive, Slice endExclusive, IProgress<(long Count, Slice Current)>? onProgress, CancellationToken ct)
			{
				const int INIT_WINDOW_SIZE = 1 << 8; // start at 256 //1024
				const int MAX_WINDOW_SIZE = 1 << 13; // never use more than 4096
				const int MIN_WINDOW_SIZE = 64; // use range reads when the windows size is smaller than 64

				Contract.NotNull(db);
				if (endExclusive < beginInclusive) throw new ArgumentException("The end key cannot be less than the begin key", nameof(endExclusive));

				ct.ThrowIfCancellationRequested();

				// To count the number of items in the range, we will scan it using a key selector with an offset equal to our window size
				// > if the returned key is still inside the range, we add the window size to the counter, and start again from the current key
				// > if the returned key is outside the range, we reduce the size of the window, and start again from the previous key
				// > if the returned key is exactly equal to the end of range, OR if the window size was 1, then we stop

				// Since we don't know in advance if the range contains 1 key or 1 Billion keys, choosing a good value for the window size is critical:
				// > if it is too small and the range is very large, we will need too many sequential reads and the network latency will quickly add up
				// > if it is too large and the range is small, we will spend too many times halving the window size until we get the correct value

				// A few optimizations are possible:
				// > we could start with a small window size, and then double its size on every full segment (up to a maximum)
				// > for the last segment, we don't need to wait for a GetKey to complete before issuing the next, so we could split the segment into 4 (or more), do the GetKeyAsync() in parallel, detect the quarter that cross the boundary, and iterate again until the size is small
				// > once the window size is small enough, we can switch to using GetRange to read the last segment in one shot, instead of iterating with window size 16, 8, 4, 2 and 1 (the wost case being 2^N - 1 items remaining)

				// note: we make a copy of the keys because the operation could take a long time and the key's could prevent a potentially large underlying buffer from being GCed
				var cursor = beginInclusive.Copy();
				var end = endExclusive.Copy();

				using (var tr = db.BeginReadOnlyTransaction(ct))
				{
#if TRACE_COUNTING
					tr.Annotate("Estimating number of keys in range {0}", KeyRange.Create(beginInclusive, endExclusive));
#endif

					tr.Options.WithReadYourWritesDisable();

					// start looking for the first key in the range
					cursor = await tr.Snapshot.GetKeyAsync(KeySelector.FirstGreaterOrEqual(cursor)).ConfigureAwait(false);
					if (cursor >= end)
					{ // the range is empty !
						return 0;
					}

					// we already have seen one key, so add it to the count
#if TRACE_COUNTING
					int iter = 1;
#endif
					long counter = 1;
					// start with a medium-sized window
					int windowSize = INIT_WINDOW_SIZE;
					bool last = false;

					while (cursor < end)
					{
						Contract.Debug.Assert(windowSize > 0);

						var selector = KeySelector.FirstGreaterOrEqual(cursor) + windowSize;
						Slice next = Slice.Nil;
						FdbException? error = null;
						try
						{
							next = await tr.Snapshot.GetKeyAsync(selector).ConfigureAwait(false);
#if TRACE_COUNTING
							++iter;
#endif
						}
						catch (FdbException e)
						{
							error = e;
						}

						if (error != null)
						{
							// => from this point, the count returned will not be transactionally accurate
							if (error.Code == FdbError.TransactionTooOld)
							{ // the transaction used up its time window
								tr.Reset();
							}
							else
							{ // check to see if we can continue...
								await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
							}
							// retry
							tr.Options.WithReadYourWritesDisable();
							continue;
						}

						//BUGBUG: GetKey(...) always truncate the result to \xFF if the selected key would be past the end,
						// so we need to fall back immediately to the binary search and/or get_range if next == \xFF

						if (next > end)
						{ // we have reached past the end, switch to binary search

							last = true;

							// if window size is already 1, then we have counted everything (the range.End key does not exist in the db)
							if (windowSize == 1) break;

							if (windowSize <= MIN_WINDOW_SIZE)
							{ // The window is small enough to switch to reading for counting (will be faster than binary search)
#if TRACE_COUNTING
								tr.Annotate("Switch to reading all items (window size = {0})", windowSize);
#endif

								// Count the keys by reading them. Also, we know that there can not be more than windowSize - 1 remaining
								int n = await tr.Snapshot
									.GetRange(
										KeySelector.FirstGreaterThan(cursor), // cursor has already been counted once
										KeySelector.FirstGreaterOrEqual(end),
										new() { Limit = windowSize - 1 }
									)
									.CountAsync()
									.ConfigureAwait(false);

								counter += n;
								onProgress?.Report((counter, end));
#if TRACE_COUNTING
								++iter;
#endif
								break;
							}

							windowSize >>= 1;
							continue;
						}

						// the range is not finished, advance the cursor
						counter += windowSize;
						cursor = next;
						onProgress?.Report((counter, cursor));

						if (!last)
						{ // double the size of the window if we are not in the last segment
							windowSize = Math.Min(windowSize << 1, MAX_WINDOW_SIZE);
						}
					}
#if TRACE_COUNTING
					tr.Annotate("Found {0} keys in {1} iterations", counter, iter);
#endif
					return counter;
				}
			}

			/// <summary>Estimates the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="beginInclusive">Key defining the beginning of the range</param>
			/// <param name="endExclusive">Key defining the end of the range</param>
			/// <param name="onProgress">Optional callback called every time the count is updated. The first argument is the current count, and the second argument is the last key that was found.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that <paramref name="beginInclusive"/> &lt;= k &gt; <paramref name="endExclusive"/></returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static async Task<long> EstimateCountAsync(IFdbDatabaseProvider db, Slice beginInclusive, Slice endExclusive, IProgress<(long Count, Slice Current)>? onProgress, CancellationToken ct)
			{
				return await EstimateCountAsync(await db.GetDatabase(ct).ConfigureAwait(false), beginInclusive, endExclusive, onProgress, ct).ConfigureAwait(false);
			}
		}

	}
}
