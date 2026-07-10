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

// enable this to help debug Transactions
//#define DEBUG_TRANSACTIONS

namespace FoundationDB.Client
{
	using System.Buffers.Binary;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using FoundationDB.Filters.Logging;

	/// <summary>FoundationDB transaction handle.</summary>
	/// <remarks>An instance of this class can be used to read from and/or write to a snapshot of a FoundationDB database.</remarks>
	[DebuggerDisplay("Id={Id}, StillAlive={StillAlive}, Size={Size}")]
	public sealed partial class FdbTransaction : IFdbTransaction, IFdbTransactionOptions
	{

		#region Private Members...

		internal const int STATE_INIT = 0;
		internal const int STATE_READY = 1;
		internal const int STATE_COMMITTED = 2;
		internal const int STATE_CANCELED = 3;
		internal const int STATE_FAILED = 4;
		internal const int STATE_DISPOSED = -1;

		/// <summary>Current state of the transaction</summary>
		private int m_state;

		/// <summary>Unique internal id for this transaction (for debugging purpose)</summary>
		private readonly int m_id;

		/// <summary>True if the transaction has been opened in read-only mode</summary>
		private readonly bool m_readOnly;
		// => to bit flag so that we can have more options? ("write only", etc...)

		private readonly IFdbTransactionHandler m_handler;

		/// <summary>Timeout (in ms) of this transaction</summary>
		private int m_timeout;

		/// <summary>Retry Limit of this transaction</summary>
		private int m_retryLimit;

		/// <summary>Max Retry Delay (in ms) of this transaction</summary>
		private int m_maxRetryDelay;

		/// <summary>Tracing options of this transaction</summary>
		private FdbTracingOptions m_tracingOptions;

		/// <summary>Cancellation source specific to this instance.</summary>
		private readonly CancellationTokenSource m_cts;

		/// <summary>CancellationToken that should be used for all async operations executing inside this transaction</summary>
		private CancellationToken m_cancellation;

		/// <summary>Random token (but constant per transaction retry) used to generate incomplete VersionStamps</summary>
		private ulong m_versionStampToken;

		/// <summary>Contains the log used by this transaction (or null if logging is disabled)</summary>
		private FdbTransactionLog? m_log;

		private Action<FdbTransactionLog>? m_logHandler;

		#endregion

		#region Constructors...

		internal FdbTransaction(FdbDatabase db, FdbTenant? tenant, FdbOperationContext context, int id, IFdbTransactionHandler handler, FdbTransactionMode mode)
		{
			Contract.Debug.Requires(db != null && context != null && handler != null);
			Contract.Debug.Requires(context.Database != null);

			this.Context = context;
			this.Database = db;
			this.Tenant = tenant;
			m_id = id;
			//REVIEW: the operation context may already have created its own CTS, maybe we can merge them ?
			m_cts = CancellationTokenSource.CreateLinkedTokenSource(context.Cancellation);
			m_cancellation = m_cts.Token;

			m_readOnly = (mode & FdbTransactionMode.ReadOnly) != 0;
			m_handler = handler;
		}

		#endregion

		#region Public Properties...

		/// <inheritdoc />
		public int Id => m_id;

		/// <inheritdoc />
		public bool IsSnapshot => false;

		/// <inheritdoc />
		public FdbOperationContext Context { get; }

		/// <summary>Database instance that manages this transaction</summary>
		public FdbDatabase Database { get; }

		/// <inheritdoc />
		IFdbDatabase IFdbReadOnlyTransaction.Database => this.Database;

		/// <summary>Tenant where this transaction will be executed</summary>
		public FdbTenant? Tenant { get; }

		/// <inheritdoc />
		IFdbTenant? IFdbReadOnlyTransaction.Tenant => this.Tenant;

		/// <summary>Returns the handler for this transaction</summary>
		internal IFdbTransactionHandler Handler => m_handler;

		/// <summary>If true, the transaction is still pending (not committed or rolled back).</summary>
		internal bool StillAlive => this.State == STATE_READY;

		/// <inheritdoc />
		public CancellationToken Cancellation => m_cancellation;

		/// <inheritdoc />
		public bool IsReadOnly => m_readOnly;

		#endregion

		/// <inheritdoc />
		public long Size => m_handler.Size;

		/// <inheritdoc />
		public (int Keys, long Size) GetWriteStatistics() => m_handler.GetWriteStatistics();

		/// <inheritdoc />
		public (int Keys, long Size) GetReadStatistics() => m_handler.GetReadStatistics();

		#region Options..

		#region Properties...

		/// <inheritdoc />
		public int Timeout
		{
			get => m_timeout;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Timeout value cannot be negative");
				SetOption(FdbTransactionOption.Timeout, value);
				m_timeout = value;
			}
		}

		/// <inheritdoc />
		public int RetryLimit
		{
			get => m_retryLimit;
			set
			{
				if (value < -1) throw new ArgumentOutOfRangeException(nameof(value), value, "Retry count cannot be negative");
				SetOption(FdbTransactionOption.RetryLimit, value);
				m_retryLimit = value;
			}
		}

		/// <inheritdoc />
		public int MaxRetryDelay
		{
			get => m_maxRetryDelay;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Max retry delay cannot be negative");
				SetOption(FdbTransactionOption.MaxRetryDelay, value);
				m_maxRetryDelay = value;
			}
		}

		/// <inheritdoc/>
		public FdbTracingOptions Tracing
		{
			get => m_tracingOptions;
			set => m_tracingOptions = value;
		}

		#endregion

		/// <inheritdoc/>
		public IFdbTransactionOptions Options => this;

		/// <inheritdoc/>
		int IFdbTransactionOptions.ApiVersion => this.Database.GetApiVersion();

		/// <inheritdoc/>
		public IFdbTransactionOptions SetOption(FdbTransactionOption option)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()}");

			m_log?.Annotate($"SetOption({option})");
			m_handler.SetOption(option, default);
			return this;
		}

		/// <inheritdoc/>
		public IFdbTransactionOptions SetOption(FdbTransactionOption option, ReadOnlySpan<char> value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to '{value.ToString()}'");

			m_log?.Annotate($"SetOption({option}, \"{value.ToString()}\")");
			var data = FdbNative.ToNativeString(value, nullTerminated: false);
			m_handler.SetOption(option, data.Span);
			return this;
		}

		/// <inheritdoc/>
		public IFdbTransactionOptions SetOption(FdbTransactionOption option, ReadOnlySpan<byte> value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to '{Slice.Dump(value)}'");

			m_log?.Annotate($"SetOption({option}, '{Slice.Dump(value)}')");
			m_handler.SetOption(option, value);
			return this;
		}

		/// <inheritdoc />
		public IFdbTransactionOptions SetOption(FdbTransactionOption option, long value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to {value}");

			m_log?.Annotate($"SetOption({option}, {value})");

			// Spec says: "If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8."
			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteInt64LittleEndian(tmp, value);
			m_handler.SetOption(option, tmp);
			return this;
		}

		#endregion

		#region Logging...

		/// <summary>Log of all operations performed on this transaction (if logging was enabled on the database or transaction)</summary>
		public FdbTransactionLog? Log => m_log;

		/// <summary>Return <see langword="true"/> if logging is enabled on this transaction</summary>
		/// <remarks>
		/// If logging is enabled, the transaction will track all the operations performed by this transaction until it completes.
		/// The log can be accessed via the <see cref="Log"/> property.
		/// Comments can be added via the <see cref="Annotate(string)"/> method.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsLogged() => m_log != null;

		/// <summary>Add a comment to the transaction log</summary>
		/// <param name="comment">Line of text that will be added to the log</param>
		/// <remarks>This method does nothing if logging is disabled. To prevent unnecessary allocations, you may check <see cref="IsLogged"/> first</remarks>
		/// <example><code>tr.Annonate("Reticulating splines");</code></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Annotate(string comment)
		{
			m_log?.Annotate(comment);
		}

		/// <summary>Add a comment to the transaction log</summary>
		/// <param name="comment">Line of text that will be added to the log</param>
		/// <remarks>This method does nothing if logging is disabled. To prevent unnecessary allocations, you may check <see cref="IsLogged"/> first</remarks>
		/// <example><code>tr.Annonate($"Reticulated {splines.Count:N0} splines");</code></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Annotate(ref InvariantInterpolatedStringHandler comment)
		{
			m_log?.Annotate(comment.ToStringAndClear());
		}

		/// <summary>If logging was previously enabled on this transaction, clear the log and stop logging any new operations</summary>
		/// <remarks>Any log handler attached to this transaction will not be called</remarks>
		public void StopLogging()
		{
			m_log = null;
		}

		internal void SetLogHandler(Action<FdbTransactionLog> handler, FdbLoggingOptions options, DateTimeOffset now)
		{
			Contract.Debug.Requires(handler is not null && options is not null);

			//note: it is safe not to take a lock here, because we are called before the instance is returned to the caller
			if (m_log != null) throw new InvalidOperationException("There is already a log handler attached to this transaction.");

			var uuid = Guid.NewGuid(); //TODO: use an external UUID generator?

			var fields = options.Fields;

			var log = new FdbTransactionLog()
			{
				Id = this.Id, 
				Uuid = uuid,
				SessionId = options.SessionId,
				Origin = options.Origin,
				Fields = fields,
				Commands = [ ],
				IsReadOnly = this.IsReadOnly,
				CallSite = (fields & FdbLoggedFields.RecordCreationStackTrace) != 0 ? FdbTransactionLog.CaptureStackTrace(4) : null,
			};

			m_logHandler = handler;
			m_log = log;
			log.Start(this, now);
		}

		#endregion

		#region Versions...

		private Task<long>? CachedReadVersion;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Task<long> GetReadVersionAsync()
		{
			// can be called after the transaction has been committed
			EnsureCanRetry();
			return this.CachedReadVersion ?? GetReadVersionSlow();
		}

		/// <summary>Get the read version when it is not in cache</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private Task<long> GetReadVersionSlow()
		{
			lock (this.Context.PadLock)
			{
				return this.CachedReadVersion ??= PerformGetReadVersionOperation();
			}
		}

		/// <summary>Return the observed read version, if it was requested at some point</summary>
		/// <param name="readVersion">If the method returns <see langword="true"/>, receives the read version that a call to <see cref="GetReadVersionAsync"/> produced</param>
		internal bool TryGetCachedReadVersion(out long readVersion)
		{
			lock (this.Context.PadLock)
			{
				if (this.CachedReadVersion?.Status == TaskStatus.RanToCompletion)
				{
					readVersion = this.CachedReadVersion.Result;
					return true;
				}

				readVersion = 0;
				return false;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Task<long> PerformGetReadVersionOperation()
		{
			return m_log is null
				? m_handler.GetReadVersionAsync(m_cancellation)
				: ExecuteLogged(this);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<long> ExecuteLogged(FdbTransaction self)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetReadVersionCommand(),
					(tr, _) => tr.m_handler.GetReadVersionAsync(tr.m_cancellation)
				);
		}

		/// <inheritdoc />
		public long GetCommittedVersion()
		{
			//TODO: should we only allow calls if transaction is in state "COMMITTED" ?
			EnsureNotFailedOrDisposed();

			return m_handler.GetCommittedVersion();
		}

		/// <inheritdoc />
		public void SetReadVersion(long version)
		{
			Contract.GreaterThan(version, 0);
			EnsureCanRead();

			m_log?.Annotate($"Set read version to {version:N0}");
			m_handler.SetReadVersion(version);
		}

		/// <inheritdoc />
		public Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key = default)
		{
			return PerformGetMetadataVersionKeyAsync(key.IsNull ? Fdb.System.MetadataVersionKey : key, snapshot: false);
		}

		/// <inheritdoc />
		public void TouchMetadataVersionKey(Slice key = default)
		{
			SetMetadataVersionKey(key.IsNull ? Fdb.System.MetadataVersionKey : key);
		}

		// all access to this should be made under lock!
		private Dictionary<Slice, (Task<VersionStamp?> Task, bool Snapshot)>? MetadataVersionKeysCache;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Dictionary<Slice, (Task<VersionStamp?> Task, bool Snapshot)> GetMetadataVersionKeysCache()
		{
			return this.MetadataVersionKeysCache ?? GetMetadataVersionKeysCacheSlow();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Dictionary<Slice, (Task<VersionStamp?> Task, bool Snapshot)> GetMetadataVersionKeysCacheSlow()
		{
			lock (this.Context.PadLock)
			{
				return this.MetadataVersionKeysCache ??= new Dictionary<Slice, (Task<VersionStamp?>, bool)>(Slice.Comparer.Default);
			}
		}

		private static readonly Task<VersionStamp?> PoisonedMetadataVersion = Task.FromResult<VersionStamp?>(null);

		/// <summary>Fetch and parse the metadataVersion system key</summary>
		/// <param name="key">Key to read</param>
		/// <param name="snapshot">If false, add a read conflict range on the key.</param>
		internal Task<VersionStamp?> PerformGetMetadataVersionKeyAsync(Slice key, bool snapshot)
		{
			if (key.IsNull) throw new ArgumentNullException(nameof(key));

			EnsureCanRetry();

			(Task<VersionStamp?> Task, bool Snapshot) t;

			// only add the range if we read in non-snapshot isolation for the first time
			bool mustAddConflictRange = false;

			// note: we have to lock because there could be another thread calling TouchMetadataVersionKey at the same time!
			// concurrent calls will be serialized internally by the network thread, which should prevent a concurrent write from causing us to throw error code 1036

			lock (this.Context.PadLock)
			{
				var cache = GetMetadataVersionKeysCache();
				if (!cache.TryGetValue(key, out t) || t.Task == null!) //BUGBUG: should this be null or PoisonedMetadataVersion ?
				{
					mustAddConflictRange = !snapshot;
					t = (ReadAndParseMetadataVersionSlow(key), snapshot);
					cache[key] = t;
				}
				else if (t.Snapshot)
				{ // previous reads were done in snapshot isolation

					if (!snapshot)
					{ // but this read is not, so we have to add the conflict range!
						mustAddConflictRange = true;
						t.Snapshot = false;
						cache[key] = t;
					}
				}
			}

			// for non-snapshot reads on writable transactions, we have to add the conflict range ourselves.
			if (mustAddConflictRange && !this.IsReadOnly)
			{
				//TODO: only on the first non-snapshot read!!!
				var range = KeyRange.FromKey(key);
				AddConflictRange(range.Begin.Span, range.End.Span, FdbConflictRangeType.Read);
			}
			return t.Task;
		}

		private async Task<VersionStamp?> ReadAndParseMetadataVersionSlow(Slice key)
		{
			Contract.Debug.Requires(!key.IsNull);

			Slice value;
			try
			{
				// this can fail if the value has been changed earlier in the transaction!
				value = await PerformGetOperation(key.Span, snapshot: true).ConfigureAwait(false);
			}
			catch (FdbException e)
			{
				if (e.Code == FdbError.AccessedUnreadable)
				{ // happens whenever the value has already been changed in the transaction!
					// Basically this means "we don't know yet", and the caller should decide on a case by case how to deal with it

					//note: the transaction will FAIL to commit! this only helps for transactions that decide to not commit given this result.
					lock (this.Context.PadLock)
					{
						// poison this key!
						GetMetadataVersionKeysCache()[key] = (FdbTransaction.PoisonedMetadataVersion, false);
					}

					return null;
				}
				throw;
			}

			// if the db is new, it is possible that the version is not yet present, we will return the empty stamp
			return value.IsNullOrEmpty ? VersionStamp.Complete(0, 0) : VersionStamp.ReadFrom(value);
		}

		internal void SetMetadataVersionKey(Slice key)
		{
			//The C API does not fail immediately if the mutation type is not valid, and only fails at commit time.
			EnsureMutationTypeIsSupported(FdbMutationType.VersionStampedValue);

			// note: we have to lock because there could be another thread calling GetMetadataVersionKey at the same time!
			// concurrent calls will be serialized internally by the network thread, which should prevent a concurrent read from throwing error code 1036

			lock (this.Context.PadLock)
			{
				var cache = GetMetadataVersionKeysCache();
				if (cache.TryGetValue(key, out var t) && t.Task == PoisonedMetadataVersion)
				{ // already done!
					return;
				}

				// mark this key as dirty to prevent any future read
				cache[key] = (PoisonedMetadataVersion, false);

				// update the key with a new versionstamp
				PerformAtomicOperation(key.Span, Fdb.System.MetadataVersionValue.Span, FdbMutationType.VersionStampedValue);
			}
		}

		/// <inheritdoc />
		public Task<VersionStamp> GetVersionStampAsync()
		{
			EnsureNotFailedOrDisposed();
			if (!this.StillAlive)
			{ // we have already been committed or cancelled?
				ThrowOnInvalidState(this);
			}
			return m_handler.GetVersionStampAsync(m_cancellation);
		}

		private ulong GenerateNewVersionStampToken()
		{
			// We need to generate an 80-bits stamp, and also need to mark it as 'incomplete' by forcing the highest bit to 1.
			// Since this is supposed to be a version number with a ~1M tick-rate per seconds, we will play it safe, and force the 8 highest bits to 1,
			// meaning that we only reduce the database potential lifetime but 1/256th, before getting into trouble.
			//
			// By doing some empirical testing, it also seems that the last 16 bits are a transaction batch order which is usually a low number.
			// Again, we will force the 4 highest bit to 1 to reduce the chances of collision with a complete version stamp.
			//
			// So the final token will look like:  'FF xx xx xx xx xx xx xx Fy yy', were 'x' is the random token, and 'y' will lowest 12 bits of the transaction retry count

			ulong x;
			unsafe
			{
				// use a 128-bit guid as the source of entropy for our new token
				var rnd = Guid.NewGuid();
				ulong* p = (ulong*) &rnd;
				x = p[0] ^ p[1];
			}
			x |= 0xFF00000000000000UL;

			lock (this.Context.PadLock)
			{
				ulong token = m_versionStampToken;
				if (token == 0)
				{
					token = x;
					m_versionStampToken = x;
				}
				return token;
			}
		}

		/// <inheritdoc />
		[Pure]
		public VersionStamp CreateVersionStamp()
		{
			var token = m_versionStampToken;
			if (token == 0) token = GenerateNewVersionStampToken();
			return VersionStamp.Custom(token, (ushort) (this.Context.Retries | 0xF000), incomplete: true);
		}

		/// <inheritdoc />
		public VersionStamp CreateVersionStamp(int userVersion)
		{
			var token = m_versionStampToken;
			if (token == 0) token = GenerateNewVersionStampToken();

			return VersionStamp.Custom(token, (ushort) (this.Context.Retries | 0xF000), userVersion, incomplete: true);
		}

		/// <summary>Counter used to generated a unique unique versionstamps for this transaction.</summary>
		private int m_versionStampCounter;

		/// <inheritdoc />
		[Pure]
		public VersionStamp CreateUniqueVersionStamp()
		{
			int userVersion = Interlocked.Increment(ref m_versionStampCounter);
			if (userVersion > 0xFFF) throw new InvalidOperationException("Cannot generate more than 65535 unique VersionStamps per transaction!");
			return CreateVersionStamp(userVersion);
		}

		#endregion

		#region Get...

		/// <inheritdoc />
		public Task<Slice> GetAsync(ReadOnlySpan<byte> key)
		{
			FdbKey.EnsureKeyIsValid(key);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", $"Getting value for '{key.ToString()}'");
#endif

			return PerformGetOperation(key, snapshot: false);
		}

		/// <inheritdoc />
		public Task<TResult> GetAsync<TResult>(ReadOnlySpan<byte> key, FdbValueDecoder<TResult> valueDecoder)
		{
			FdbKey.EnsureKeyIsValid(key);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", $"Getting value for '{key.ToString()}'");
#endif

			return PerformGetOperation(key, snapshot: false, valueDecoder);
		}

		/// <inheritdoc />
		public Task<TResult> GetAsync<TState, TResult>(ReadOnlySpan<byte> key, TState valueState, FdbValueDecoder<TState, TResult> valueDecoder)
		{
			FdbKey.EnsureKeyIsValid(key);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", $"Getting value for '{key.ToString()}'");
#endif

			return PerformGetOperation(key, snapshot: false, valueState, valueDecoder);
		}

		private Task<Slice> PerformGetOperation(ReadOnlySpan<byte> key, bool snapshot)
		{
			FdbClientInstrumentation.ReportGet(this);

			return m_log is null
				? m_handler.GetAsync(key, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, key, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<Slice> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetCommand<Slice>(self.m_log.Grab(key), snapshot),
					(tr, cmd) => tr.m_handler.GetAsync(cmd.Key.Span, cmd.Snapshot, tr.m_cancellation)
				);
		}

		private Task<TResult> PerformGetOperation<TResult>(ReadOnlySpan<byte> key, bool snapshot, FdbValueDecoder<TResult> valueDecoder)
		{
			FdbClientInstrumentation.ReportGet(this);

			return m_log is null
				? m_handler.GetAsync(key, snapshot: snapshot, valueDecoder, m_cancellation)
				: ExecuteLogged(this, key, snapshot, valueDecoder);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<TResult> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key, bool snapshot, FdbValueDecoder<TResult> valueDecoder)
				=> self.m_log!.ExecuteAsync(
					(Transaction: self, Decoder: valueDecoder),
					new FdbTransactionLog.GetCommand<TResult>(self.m_log.Grab(key), snapshot),
					(s, cmd) => s.Transaction.m_handler.GetAsync(cmd.Key.Span, cmd.Snapshot, s.Decoder, s.Transaction.m_cancellation)
				);
		}

		private Task<TResult> PerformGetOperation<TValueState, TResult>(ReadOnlySpan<byte> key, bool snapshot, TValueState valueState, FdbValueDecoder<TValueState, TResult> valueDecoder)
		{
			FdbClientInstrumentation.ReportGet(this);

			return m_log is null
				? m_handler.GetAsync(key, snapshot: snapshot, valueState, valueDecoder, m_cancellation)
				: ExecuteLogged(this, key, snapshot, valueState, valueDecoder);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<TResult> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key, bool snapshot, TValueState valueState, FdbValueDecoder<TValueState, TResult> valueDecoder)
				=> self.m_log!.ExecuteAsync(
					(Transaction: self, State: valueState, Decoder: valueDecoder),
					new FdbTransactionLog.GetCommand<TResult>(self.m_log.Grab(key), snapshot),
					(s, cmd) => s.Transaction.m_handler.GetAsync(cmd.Key.Span, cmd.Snapshot, s.State, s.Decoder, s.Transaction.m_cancellation)
				);
		}

		/// <inheritdoc />
		public Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected)
		{
			FdbKey.EnsureKeyIsValid(key);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ValueCheckAsync", $"Checking the value for '{key.ToString()}'");
#endif

			return PerformValueCheckOperation(key, expected, snapshot: false);
		}

		private Task<(FdbValueCheckResult Result, Slice Actual)> PerformValueCheckOperation(ReadOnlySpan<byte> key, Slice expected, bool snapshot)
		{
			FdbClientInstrumentation.ReportGet(this); //REVIEW: use a specific operation type for this?

			return m_log is null
				? m_handler.CheckValueAsync(key, expected, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, key, expected, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<(FdbValueCheckResult Result, Slice Actual)> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key, Slice expected, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.CheckValueCommand(self.m_log.Grab(key), self.m_log.Grab(expected), snapshot),
					(tr, cmd) => tr.m_handler.CheckValueAsync(cmd.Key.Span, cmd.Expected, cmd.Snapshot, tr.m_cancellation)
				);
		}

		#endregion

		#region GetValues...

		/// <inheritdoc />
		public Task<Slice[]> GetValuesAsync(ReadOnlySpan<Slice> keys)
		{
			FdbKey.EnsureKeysAreValid(keys);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", $"Getting batch of {keys.Length} values ...");
#endif

			return PerformGetValuesOperation(keys, snapshot: false);
		}

		private Task<Slice[]> PerformGetValuesOperation(ReadOnlySpan<Slice> keys, bool snapshot)
		{
			FdbClientInstrumentation.ReportGet(this, keys.Length);

			return m_log is null
				? m_handler.GetValuesAsync(keys, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, keys, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<Slice[]> ExecuteLogged(FdbTransaction self, ReadOnlySpan<Slice> keys, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetValuesCommand(self.m_log.Grab(keys), snapshot),
					(tr, cmd) => tr.m_handler.GetValuesAsync(cmd.Keys.AsSpan(), cmd.Snapshot, tr.m_cancellation)
				);
		}

		public Task GetValuesAsync<TResult>(ReadOnlySpan<Slice> keys, Memory<TResult> results, FdbValueDecoder<TResult> valueDecoder)
		{
			FdbKey.EnsureKeysAreValid(keys);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", $"Getting batch of {keys.Length} values ...");
#endif

			return PerformGetValuesOperation(keys, results, valueDecoder, snapshot: false);
		}

		private Task PerformGetValuesOperation<TValue>(ReadOnlySpan<Slice> keys, Memory<TValue> values, FdbValueDecoder<TValue> valueDecoder, bool snapshot)
		{
			FdbClientInstrumentation.ReportGet(this, keys.Length);

			return m_log is null
				? m_handler.GetValuesAsync(keys, values, valueDecoder, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, keys, values, valueDecoder, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task ExecuteLogged(FdbTransaction self, ReadOnlySpan<Slice> keys, Memory<TValue> values, FdbValueDecoder<TValue> valueDecoder, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					(Transaction: self, Values: values, Decoder: valueDecoder),
					new FdbTransactionLog.GetValuesCommand<TValue>(self.m_log.Grab(keys), new TValue[keys.Length], snapshot),
					(s, cmd) => s.Transaction.m_handler.GetValuesAsync(cmd.Keys.AsSpan(), cmd.Values, s.Decoder, cmd.Snapshot, s.Transaction.m_cancellation),
					(s, cmd, _) => cmd.Values.CopyTo(s.Values)
				);
		}

		public Task GetValuesAsync<TValueState, TResult>(ReadOnlySpan<Slice> keys, Memory<TResult> results, TValueState valueState, FdbValueDecoder<TValueState, TResult> valueDecoder)
		{
			FdbKey.EnsureKeysAreValid(keys);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", $"Getting batch of {keys.Length} values ...");
#endif

			return PerformGetValuesOperation(keys, results, valueState, valueDecoder, snapshot: false);
		}

		private Task PerformGetValuesOperation<TValueState, TValue>(ReadOnlySpan<Slice> keys, Memory<TValue> values, TValueState valueState, FdbValueDecoder<TValueState, TValue> valueDecoder, bool snapshot)
		{
			FdbClientInstrumentation.ReportGet(this, keys.Length);

			return m_log is null
				? m_handler.GetValuesAsync(keys, values, valueState, valueDecoder, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, keys, values, valueState, valueDecoder, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task ExecuteLogged(FdbTransaction self, ReadOnlySpan<Slice> keys, Memory<TValue> values, TValueState valueState, FdbValueDecoder<TValueState, TValue> valueDecoder, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					(Transaction: self, Values: values, State: valueState, Decoder: valueDecoder),
					new FdbTransactionLog.GetValuesCommand<TValue>(self.m_log.Grab(keys), new TValue[keys.Length], snapshot),
					(s, cmd) => s.Transaction.m_handler.GetValuesAsync(cmd.Keys.AsSpan(), cmd.Values, s.State, s.Decoder, cmd.Snapshot, s.Transaction.m_cancellation),
					(s, cmd, _) => cmd.Values.CopyTo(s.Values)
				);
		}

		#endregion

		#region GetRangeAsync...

		/// <inheritdoc />
		public Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options, int iteration)
			=> GetRangeAsync(beginInclusive.ToSpan(), endExclusive.ToSpan(), options, iteration);

		/// <inheritdoc />
		public Task<FdbRangeChunk> GetRangeAsync(KeySpanSelector beginInclusive, KeySpanSelector endExclusive, FdbRangeOptions? options, int iteration)
		{
			FdbKey.EnsureKeyIsValid(beginInclusive.Key);
			FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);

			// GetRange() may be used to read special keys after a failed commit (ex: \xff\xff/transaction/conflicting_keys/...), so we have to allow some cases
			if (FdbKey.IsSpecialKey(beginInclusive.Key))
			{
				EnsureStillValid(allowFromNetworkThread: false, allowFailedState: true);
			}
			else
			{
				EnsureCanRead();
			}

			options = FdbRangeOptions.EnsureDefaults(options, FdbStreamingMode.Iterator, FdbFetchMode.KeysAndValues);
			options.EnsureLegalValues(iteration);

			// The iteration value is only needed when in iterator mode, but then it should start from 1
			if (iteration == 0) iteration = 1;

			return PerformGetRangeOperation(beginInclusive, endExclusive, snapshot: false, options, iteration);
		}

		/// <inheritdoc />
		public Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(KeySelector beginInclusive, KeySelector endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options, int iteration)
			=> GetRangeAsync(beginInclusive.ToSpan(), endExclusive.ToSpan(), state, decoder, options, iteration);

		/// <inheritdoc />
		public Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(KeySpanSelector beginInclusive, KeySpanSelector endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options, int iteration)
		{
			FdbKey.EnsureKeyIsValid(beginInclusive.Key);
			FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);

			// GetRange() may be used to read special keys after a failed commit (ex: \xff\xff/transaction/conflicting_keys/...), so we have to allow some cases
			if (FdbKey.IsSpecialKey(beginInclusive.Key))
			{
				EnsureStillValid(allowFromNetworkThread: false, allowFailedState: true);
			}
			else
			{
				EnsureCanRead();
			}

			options = FdbRangeOptions.EnsureDefaults(options, FdbStreamingMode.Iterator, FdbFetchMode.KeysAndValues);
			options.EnsureLegalValues(iteration);

			// The iteration value is only needed when in iterator mode, but then it should start from 1
			if (iteration == 0) iteration = 1;

			return PerformGetRangeOperation(beginInclusive, endExclusive, snapshot: false, state, decoder, options, iteration);
		}

		private Task<FdbRangeChunk> PerformGetRangeOperation(
			KeySpanSelector beginInclusive,
			KeySpanSelector endExclusive,
			bool snapshot,
			FdbRangeOptions options,
			int iteration
		)
		{
			FdbClientInstrumentation.ReportGetRange(this);

			return m_log is null
				? m_handler.GetRangeAsync(beginInclusive, endExclusive, options, iteration, snapshot, m_cancellation)
				: ExecuteLogged(this, beginInclusive, endExclusive, snapshot, options, iteration);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<FdbRangeChunk> ExecuteLogged(FdbTransaction self, KeySpanSelector beginInclusive, KeySpanSelector endExclusive, bool snapshot, FdbRangeOptions options, int iteration)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetRangeCommand(
						self.m_log.Grab(beginInclusive),
						self.m_log.Grab(endExclusive),
						snapshot,
						options with { },
						iteration
					),
					(tr, cmd) => tr.m_handler.GetRangeAsync(
						cmd.Begin.ToSpan(),
						cmd.End.ToSpan(),
						cmd.Options,
						cmd.Iteration,
						cmd.Snapshot,
						tr.m_cancellation
					)
				);
		}

		private Task<FdbRangeChunk<TResult>> PerformGetRangeOperation<TState, TResult>(
			KeySpanSelector beginInclusive,
			KeySpanSelector endExclusive,
			bool snapshot,
			TState state,
			FdbKeyValueDecoder<TState, TResult> decoder,
			FdbRangeOptions options,
			int iteration
		)
		{
			FdbClientInstrumentation.ReportGetRange(this);

			return m_log is null
				? m_handler.GetRangeAsync(beginInclusive, endExclusive, snapshot, state, decoder, options, iteration, m_cancellation)
				: ExecuteLogged(this, beginInclusive, endExclusive, snapshot, state, decoder, options, iteration);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<FdbRangeChunk<TResult>> ExecuteLogged(FdbTransaction self, KeySpanSelector beginInclusive, KeySpanSelector endExclusive, bool snapshot, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions options, int iteration)
				=> self.m_log!.ExecuteAsync(
					(Transaction: self, State: state, Decoder: decoder),
					new FdbTransactionLog.GetRangeCommand<TResult>(
						self.m_log.Grab(beginInclusive),
						self.m_log.Grab(endExclusive),
						snapshot,
						options,
						iteration
					),
					(s, cmd) => s.Transaction.m_handler.GetRangeAsync<TState, TResult>(
						cmd.Begin.ToSpan(),
						cmd.End.ToSpan(),
						cmd.Snapshot,
						s.State,
						s.Decoder,
						cmd.Options,
						cmd.Iteration,
						s.Transaction.m_cancellation
					)
				);
		}

		private Task<FdbRangeResult> PerformVisitRangeOperation<TState>(
			KeySelector beginInclusive,
			KeySelector endExclusive,
			bool snapshot,
			TState state,
			FdbKeyValueAction<TState> visitor,
			FdbRangeOptions options,
			int iteration
		)
		{
			FdbClientInstrumentation.ReportGetRange(this);

			return m_log is null
				? m_handler.VisitRangeAsync<TState>(beginInclusive.ToSpan(), endExclusive.ToSpan(), snapshot, state, visitor, options, iteration, m_cancellation)
				: ExecuteLogged(this, beginInclusive, endExclusive, snapshot, state, visitor, options, iteration);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<FdbRangeResult> ExecuteLogged(FdbTransaction self, KeySelector beginInclusive, KeySelector endExclusive, bool snapshot, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions options, int iteration)
				=> self.m_log!.ExecuteAsync(
					(Transaction: self, State: state, Visitor: visitor),
					new FdbTransactionLog.VisitRangeCommand(
						self.m_log.Grab(beginInclusive),
						self.m_log.Grab(endExclusive),
						snapshot,
						options,
						iteration
					),
					(s, cmd) => s.Transaction.m_handler.VisitRangeAsync<TState>(
						cmd.Begin.ToSpan(),
						cmd.End.ToSpan(),
						cmd.Snapshot,
						s.State,
						s.Visitor,
						cmd.Options,
						cmd.Iteration,
						s.Transaction.m_cancellation
					)
				);
		}

		#endregion

		#region GetRange...

		[Pure, LinqTunnel]
		internal FdbRangeQuery<TState, TResult> GetRangeCore<TState, TResult>(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options, bool snapshot, TState state, FdbKeyValueDecoder<TState, TResult> decoder)
		{
			Contract.Debug.Requires(decoder != null);
			FdbKey.EnsureKeyIsValid(beginInclusive.Key);
			FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);
			EnsureCanRead();

			options = FdbRangeOptions.EnsureDefaults(options, FdbStreamingMode.Iterator, FdbFetchMode.KeysAndValues);
			options.EnsureLegalValues(0);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeCore", $"Getting range '{beginInclusive.ToString()} <= x < {endExclusive.ToString()}'");
#endif

			//note: we perform a defensive copy of the selector keys, in case they were allocated using a pool.
			// => it would be catastrophic if the caller keeps a reference on the query after the keys would be released to the pool!

			return new(this, beginInclusive.Memoize(), endExclusive.Memoize(), state, null, decoder, snapshot, options);
		}

		[Pure, LinqTunnel]
		internal FdbKeyValueRangeQuery GetRangeCore(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options, bool snapshot)
		{
			FdbKey.EnsureKeyIsValid(beginInclusive.Key);
			FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);

			// GetRange() may be used to read special keys after a failed commit (ex: \xff\xff/transaction/conflicting_keys/...), so we have to allow some cases
			if (FdbKey.IsSpecialKey(beginInclusive.Key))
			{
				EnsureStillValid(allowFromNetworkThread: false, allowFailedState: true);
			}
			else
			{
				EnsureCanRead();
			}

			options = FdbRangeOptions.EnsureDefaults(options, FdbStreamingMode.Iterator, FdbFetchMode.KeysAndValues);
			options.EnsureLegalValues(0);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeCore", $"Getting range '{beginInclusive.ToString()} <= x < {endExclusive.ToString()}'");
#endif

			switch (options.Fetch)
			{
				case FdbFetchMode.KeysOnly:
				{
					return new(
						this,
						beginInclusive,
						endExclusive,
						static (s, k, _) => new(s.Intern(k), default),
						snapshot,
						options
					);
				}
				case FdbFetchMode.ValuesOnly:
				{
					return new(
						this,
						beginInclusive,
						endExclusive,
						static (s, _, v) => new(default, s.Intern(v)),
						snapshot,
						options
					);
				}
				default:
				{
					return new(
						this,
						beginInclusive,
						endExclusive,
						static (s, k, v) => new(s.Intern(k), s.Intern(v)),
						snapshot,
						options
					);
				}
			}
		}

		[LinqTunnel]
		internal async Task<long> VisitRangeCore<TState>(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options, bool snapshot, TState state, FdbKeyValueAction<TState> handler)
		{
			Contract.Debug.Requires(handler != null);
			FdbKey.EnsureKeyIsValid(beginInclusive.Key);
			FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);
			EnsureCanRead();

			options = FdbRangeOptions.EnsureDefaults(options, FdbStreamingMode.Iterator, FdbFetchMode.KeysAndValues);
			options.EnsureLegalValues(0);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "VisitRangeCore", $"Getting range '{beginInclusive.ToString()} <= x < {endExclusive.ToString()}'");
#endif

			// the iteration starts at 1
			int iteration = 1;
			var cursor = beginInclusive;
			long visited = 0;

			while (true)
			{
				// visit the next page of results
				var result = await PerformVisitRangeOperation(cursor, endExclusive, snapshot, state, handler, options, iteration).ConfigureAwait(false);
				visited += result.Count;

				if (!result.HasMore)
				{ // we are done
					break;
				}

				// update the cursor to continue reading after the last key of this chunk
				cursor = KeySelector.FirstGreaterThan(result.Last);
				++iteration;
			}

			return visited;
		}

		/// <inheritdoc />
		public IFdbKeyValueRangeQuery GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			return GetRangeCore(
				beginInclusive,
				endExclusive,
				options,
				snapshot: false
			);
		}

		/// <inheritdoc />
		[Obsolete("REFACTOR IN PROGRESS: fix handling of keys/values only")]
		public IFdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions? options = null)
		{
			return GetRangeCore(
				beginInclusive,
				endExclusive,
				options,
				snapshot: false,
				state: (Selector: selector, Pool: new SliceBuffer()),
				decoder: (s, k, v) => s.Selector(new KeyValuePair<Slice, Slice>(s.Pool.Intern(k), s.Pool.Intern(v)))
			);
		}

		/// <inheritdoc />
		public IFdbRangeQuery<TResult> GetRange<TState, TResult>(KeySelector beginInclusive, KeySelector endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			return GetRangeCore(
				beginInclusive,
				endExclusive,
				options,
				snapshot: false,
				state: state,
				decoder: decoder
			);
		}

		/// <inheritdoc />
		public Task<long> VisitRangeAsync<TState>(KeySelector beginInclusive, KeySelector endExclusive, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
		{
			// we have to memoize the selectors since we have to store them in the query :/
			var beginSelector = beginInclusive.Memoize();
			var endSelector = endExclusive.Memoize();

			return VisitRangeCore(
				beginSelector,
				endSelector,
				options,
				snapshot: false,
				state: state,
				handler: visitor
			);
		}

		#endregion

		#region GetKey...

		/// <inheritdoc />
		public Task<Slice> GetKeyAsync(KeySelector selector)
			=> GetKeyAsync(selector.ToSpan());

		/// <inheritdoc />
		public Task<Slice> GetKeyAsync(KeySpanSelector selector)
		{
			FdbKey.EnsureKeyIsValid(selector.Key);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeyAsync", $"Getting key '{selector.ToString()}'");
#endif

			return PerformGetKeyOperation(selector, snapshot: false);
		}

		private Task<Slice> PerformGetKeyOperation(KeySpanSelector selector, bool snapshot)
		{
			FdbClientInstrumentation.ReportGetKey(this);

			return m_log is null
				? m_handler.GetKeyAsync(selector, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, selector, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<Slice> ExecuteLogged(FdbTransaction self, KeySpanSelector selector, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetKeyCommand(self.m_log.Grab(selector), snapshot),
					(tr, cmd) => tr.m_handler.GetKeyAsync(cmd.Selector.ToSpan(), cmd.Snapshot, tr.m_cancellation)
				);
		}

		#endregion

		#region GetKeys..

		/// <inheritdoc />
		public Task<Slice[]> GetKeysAsync(ReadOnlySpan<KeySelector> selectors)
		{
			for (int i = 0; i < selectors.Length; i++)
			{
				FdbKey.EnsureKeyIsValid(selectors[i].Key);
			}
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeysAsync", $"Getting batch of {selectors.Length} keys ...");
#endif

			return PerformGetKeysOperation(selectors, snapshot: false);
		}


		private Task<Slice[]> PerformGetKeysOperation(ReadOnlySpan<KeySelector> selectors, bool snapshot)
		{
			FdbClientInstrumentation.ReportGetKey(this, selectors.Length);

			return m_log is null
				? m_handler.GetKeysAsync(selectors, snapshot: snapshot, m_cancellation)
				: ExecuteLogged(this, selectors, snapshot);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<Slice[]> ExecuteLogged(FdbTransaction self, ReadOnlySpan<KeySelector> selectors, bool snapshot)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetKeysCommand(self.m_log.Grab(selectors), snapshot),
					(tr, cmd) => tr.m_handler.GetKeysAsync(cmd.Selectors, cmd.Snapshot, tr.m_cancellation)
				);
		}

		#endregion

		#region Set...

		/// <inheritdoc />
		public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);
			FdbKey.EnsureValueIsValid(value);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Set", $"Setting '{FdbKey.Dump(key)}' = {Slice.Dump(value)}");
#endif
			PerformSetOperation(key, value);
		}

		private void PerformSetOperation(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			FdbClientInstrumentation.ReportSet(this);

			if (m_log == null)
			{
				m_handler.Set(key, value);
			}
			else
			{
				ExecuteLogged(this, key, value);
			}

			static void ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
				=> self.m_log!.Execute(
					self,
					new FdbTransactionLog.SetCommand(self.m_log.Grab(key), self.m_log.Grab(value)),
					(tr, cmd) => tr.m_handler.Set(cmd.Key.Span, cmd.Value.Span)
				);
		}

		#endregion

		#region Atomic Ops...

		/// <summary>Checks that this type of mutation is supported by the currently selected API level</summary>
		/// <param name="mutation">Mutation type</param>
		/// <exception cref="FdbException">An error with code <see cref="FdbError.InvalidMutationType"/> if the type of mutation is not supported by this API level.</exception>
		private void EnsureMutationTypeIsSupported(FdbMutationType mutation)
		{
			int selectedApiVersion = this.Database.GetApiVersion();
			if (selectedApiVersion < 200)
			{ // mutations were not available at this time

				if (this.Database.Handler.GetMaxApiVersion() >= 200)
				{ // but the installed client could support it
					throw new NotSupportedException("Atomic mutations are only supported starting from API level 200. You need to select API level 200 or more at the start of your process.");
				}
				else
				{ // not supported by the local client
					throw new NotSupportedException("Atomic mutations are only supported starting from client version 2.x. You need to update the version of the client, and select API level 200 or more at the start of your process.");
				}
			}

			switch (mutation)
			{

				case FdbMutationType.Add:
				case FdbMutationType.BitAnd:
				case FdbMutationType.BitOr:
				case FdbMutationType.BitXor:
				{ // these mutations are available since v200
					return;
				}

				case FdbMutationType.Max:
				case FdbMutationType.Min:
				{ // these mutations are available since v300
					if (selectedApiVersion < 300)
					{
						if (Fdb.GetMaxApiVersion() >= 300)
						{
							throw new NotSupportedException("Atomic mutations Max and Min are only supported starting from API level 300. You need to select API level 300 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutations Max and Min are only supported starting from client version 3.x. You need to update the version of the client, and select API level 300 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.VersionStampedKey:
				case FdbMutationType.VersionStampedValue:
				{
					if (selectedApiVersion < 400)
					{
						if (Fdb.GetMaxApiVersion() >= 400)
						{
							throw new NotSupportedException("Atomic mutations for VersionStamps are only supported starting from API level 400. You need to select API level 400 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutations Max and Min are only supported starting from client version 4.x. You need to update the version of the client, and select API level 400 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.AppendIfFits:
				{
					if (selectedApiVersion < 520)
					{
						if (Fdb.GetMaxApiVersion() >= 520)
						{
							throw new NotSupportedException("Atomic mutation AppendIfFits is only supported starting from API level 520. You need to select API level 520 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutation AppendIfFits is only supported starting from client version 5.2. You need to update the version of the client, and select API level 520 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.ByteMin:
				case FdbMutationType.ByteMax:
				{
					// shipped with fdb 5.2, same wave as AppendIfFits (they had no case here at all, so the
					// default branch rejected them as "invalid mutation type" at every API level)
					if (selectedApiVersion < 520)
					{
						if (Fdb.GetMaxApiVersion() >= 520)
						{
							throw new NotSupportedException("Atomic mutations ByteMin and ByteMax are only supported starting from API level 520. You need to select API level 520 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutations ByteMin and ByteMax are only supported starting from client version 5.2. You need to update the version of the client, and select API level 520 or more at the start of your process.");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.CompareAndClear:
				{
					if (selectedApiVersion < 610)
					{
						if (Fdb.GetMaxApiVersion() >= 610)
						{
							throw new NotSupportedException("Atomic mutation CompareAndClear is only supported starting from API level 610. You need to select API level 610 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutation CompareAndClear is only supported starting from client version 6.1. You need to update the version of the client, and select API level 610 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				default:
				{
					// this could be a new mutation type, or an invalid value.
					throw new NotSupportedException($"An invalid mutation type '{mutation}' was issued. If you are attempting to call a new mutation type, you will need to update the version of this assembly, and select the latest API level.");
				}
			}
		}

		/// <inheritdoc />
		public void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType mutation)
		{
			//note: this method as many names in the various bindings:
			// - C API   : fdb_transaction_atomic_op(...)
			// - Java    : tr.Mutate(..)
			// - Node.js : tr.add(..), tr.max(..), ...

			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);
			FdbKey.EnsureValueIsValid(param);

			//The C API does not fail immediately if the mutation type is not valid, and only fails at commit time.
			EnsureMutationTypeIsSupported(mutation);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "AtomicCore", $"Atomic {mutation.ToString()} on '{FdbKey.Dump(key)}' = {Slice.Dump(param)}");
#endif

			PerformAtomicOperation(key, param, mutation);
		}

		private void PerformAtomicOperation(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType type)
		{
			FdbClientInstrumentation.ReportAtomicOp(this, type);

			if (m_log == null)
			{
				m_handler.Atomic(key, param, type);
			}
			else
			{
				if (type is FdbMutationType.VersionStampedKey or FdbMutationType.VersionStampedValue)
				{ // we want to capture the actual versionstamp used by this transaction (when it commits)
					m_log.RequiresVersionStamp = true;
				}
				ExecuteLogged(this, key, param, type);
			}

			static void ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType type)
				=> self.m_log!.Execute(
					self,
					new FdbTransactionLog.AtomicCommand(self.m_log.Grab(key), self.m_log.Grab(param), type),
					(tr, cmd) => tr.m_handler.Atomic(cmd.Key.Span, cmd.Param.Span, cmd.Mutation)
				);
		}

		#endregion

		#region Clear...

		/// <inheritdoc />
		public void Clear(ReadOnlySpan<byte> key)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Clear", $"Clearing '{FdbKey.Dump(key)}'");
#endif

			PerformClearOperation(key);
		}

		private void PerformClearOperation(ReadOnlySpan<byte> key)
		{
			FdbClientInstrumentation.ReportClear(this);

			if (m_log == null)
			{
				m_handler.Clear(key);
			}
			else
			{
				ExecuteLogged(this, key);
			}

			static void ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key)
				=> self.m_log!.Execute(
					self,
					new FdbTransactionLog.ClearCommand(self.m_log.Grab(key)),
					(tr, cmd) => tr.m_handler.Clear(cmd.Key.Span)
				);
		}

		#endregion

		#region Clear Range...

		/// <inheritdoc />
		public void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(beginKeyInclusive);
			FdbKey.EnsureKeyIsValid(endKeyExclusive, endExclusive: true);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ClearRange", $"Clearing Range '{beginKeyInclusive.ToString()}' <= k < '{endKeyExclusive.ToString()}'");
#endif

			PerformClearRangeOperation(beginKeyInclusive, endKeyExclusive);
		}

		private void PerformClearRangeOperation(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			FdbClientInstrumentation.ReportClearRange(this);

			if (m_log == null)
			{
				m_handler.ClearRange(beginKeyInclusive, endKeyExclusive);
			}
			else
			{
				ExecuteLogged(this, beginKeyInclusive, endKeyExclusive);
			}

			static void ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
				=> self.m_log!.Execute(
					self,
					new FdbTransactionLog.ClearRangeCommand(self.m_log.Grab(beginKeyInclusive), self.m_log.Grab(endKeyExclusive)),
					(tr, cmd) => tr.m_handler.ClearRange(cmd.Begin.Span, cmd.End.Span)
				);
		}

		#endregion

		#region Conflict Range...

		/// <inheritdoc />
		public void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(beginKeyInclusive);
			FdbKey.EnsureKeyIsValid(endKeyExclusive, endExclusive: true);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "AddConflictRange", String.Format("Adding {2} conflict range '{0}' <= k < '{1}'", beginKeyInclusive.ToString(), endKeyExclusive.ToString(), type.ToString()));
#endif

			PerformAddConflictRangeOperation(beginKeyInclusive, endKeyExclusive, type);
		}

		private void PerformAddConflictRangeOperation(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			if (m_log == null)
			{
				m_handler.AddConflictRange(beginKeyInclusive, endKeyExclusive, type);
			}
			else
			{
				ExecuteLogged(this, beginKeyInclusive, endKeyExclusive, type);
			}

			static void ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
				=> self.m_log!.Execute(
					self,
					new FdbTransactionLog.AddConflictRangeCommand(self.m_log.Grab(beginKeyInclusive), self.m_log.Grab(endKeyExclusive), type),
					(tr, cmd) => tr.m_handler.AddConflictRange(cmd.Begin.Span, cmd.End.Span, cmd.Type)
				);
		}

		#endregion

		#region GetAddressesForKey...

		/// <inheritdoc />
		public Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key)
		{
			FdbKey.EnsureKeyIsValid(key);
			EnsureCanRead();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAddressesForKeyAsync", $"Getting addresses for key '{FdbKey.Dump(key)}'");
#endif

			return PerformGetAddressesForKeyOperation(key);
		}

		private Task<string[]> PerformGetAddressesForKeyOperation(ReadOnlySpan<byte> key)
		{
			return m_log is null
				? m_handler.GetAddressesForKeyAsync(key, m_cancellation)
				: ExecuteLogged(this, key);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<string[]> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> key)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetAddressesForKeyCommand(self.m_log.Grab(key)),
					(tr, cmd) => tr.m_handler.GetAddressesForKeyAsync(cmd.Key.Span, tr.m_cancellation)
				);
		}

		#endregion

		#region GetRangeSplitPoints...

		/// <inheritdoc />
		public Task<Slice[]> GetRangeSplitPointsAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize)
		{
			FdbKey.EnsureKeyIsValid(beginKey);
			FdbKey.EnsureKeyIsValid(endKey);
			Contract.Positive(chunkSize);
			EnsureCanRead();

			// available since 7.0
			if (this.Database.GetApiVersion() < 700)
			{
				if (this.Database.Handler.GetMaxApiVersion() >= 700)
				{ // but the installed client could support it
					throw new NotSupportedException($"Getting range split points in only supported starting from API level 700 but you have selected API level {this.Database.GetApiVersion()}. You need to select API level 700 or more at the start of your process.");
				}
				else
				{ // not supported by the local client
					throw new NotSupportedException("Getting range split points is only supported starting from client version 7.0. You need to update the version of the client, and select API level 700 or more at the start of your process.");
				}
			}

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeSplitPointsAsync", $"Getting split points for range '{FdbKey.Dump(beginKey)}'..'{FdbKey.Dump(endKey)}'");
#endif

			return PerformGetRangeSplitPointsOperation(beginKey, endKey, chunkSize);
		}

		private Task<Slice[]> PerformGetRangeSplitPointsOperation(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize)
		{
			return m_log is null
				? m_handler.GetRangeSplitPointsAsync(beginKey, endKey, chunkSize, m_cancellation)
				: ExecuteLogged(this, beginKey, endKey, chunkSize);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<Slice[]> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetRangeSplitPointsCommand(self.m_log.Grab(beginKey), self.m_log.Grab(endKey), chunkSize),
					(tr, cmd) => tr.m_handler.GetRangeSplitPointsAsync(cmd.Begin.Span, cmd.End.Span, cmd.ChunkSize, tr.m_cancellation)
				);
		}

		#endregion

		#region GetEstimatedRangeSizeBytes...

		/// <inheritdoc />
		public Task<long> GetEstimatedRangeSizeBytesAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
		{
			FdbKey.EnsureKeyIsValid(beginKey);
			FdbKey.EnsureKeyIsValid(endKey);
			EnsureCanRead();

			// available since 7.0
			if (this.Database.GetApiVersion() < 700)
			{
				if (this.Database.Handler.GetMaxApiVersion() >= 700)
				{ // but the installed client could support it
					throw new NotSupportedException($"Getting range split points in only supported starting from API level 700 but you have selected API level {this.Database.GetApiVersion()}. You need to select API level 700 or more at the start of your process.");
				}
				else
				{ // not supported by the local client
					throw new NotSupportedException("Getting range split points is only supported starting from client version 7.0. You need to update the version of the client, and select API level 700 or more at the start of your process.");
				}
			}

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetEstimatedRangeSizeBytesAsync", $"Getting estimate size for range '{FdbKey.Dump(beginKey)}'..'{FdbKey.Dump(endKey)}'");
#endif

			return PerformGetEstimatedRangeSizeBytesOperation(beginKey, endKey);
		}

		private Task<long> PerformGetEstimatedRangeSizeBytesOperation(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
		{
			return m_log is null
				? m_handler.GetEstimatedRangeSizeBytesAsync(beginKey, endKey, m_cancellation)
				: ExecuteLogged(this, beginKey, endKey);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<long> ExecuteLogged(FdbTransaction self, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetEstimatedRangeSizeBytesCommand(self.m_log.Grab(beginKey), self.m_log.Grab(endKey)),
					(tr, cmd) => tr.m_handler.GetEstimatedRangeSizeBytesAsync(cmd.Begin.Span, cmd.End.Span, tr.m_cancellation)
				);
		}

		#endregion

		#region GetApproximateSize...

		/// <inheritdoc />
		public Task<long> GetApproximateSizeAsync()
		{
			EnsureCanWrite();

			return PerformGetApproximateSizeOperation();
		}

		private Task<long> PerformGetApproximateSizeOperation()
		{
			return m_log is null
				? m_handler.GetApproximateSizeAsync(m_cancellation)
				: ExecuteLogged(this);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task<long> ExecuteLogged(FdbTransaction self)
				=> self.m_log!.ExecuteAsync(
					self,
					new FdbTransactionLog.GetApproximateSizeCommand(),
					(tr, _) => tr.m_handler.GetApproximateSizeAsync(tr.m_cancellation)
				);
		}

		#endregion

		#region Commit...

		/// <inheritdoc />
		public async Task CommitAsync()
		{
			EnsureCanWrite();

			if (Logging.On) Logging.Verbose(this, "CommitAsync", "Committing transaction...");

			//TODO: need a STATE_COMMITTING ?
			try
			{
				await PerformCommitOperation().ConfigureAwait(false);

				if (Interlocked.CompareExchange(ref m_state, STATE_COMMITTED, STATE_READY) == STATE_READY)
				{
					if (Logging.On) Logging.Verbose(this, "CommitAsync", "Transaction has been committed");
				}
			}
			catch (Exception e)
			{
				if (Interlocked.CompareExchange(ref m_state, STATE_FAILED, STATE_READY) == STATE_READY)
				{
					if (Logging.On) Logging.Exception(this, "CommitAsync", e);
				}
				throw;
			}
		}

		private Task PerformCommitOperation()
		{
			return m_log is null
				? m_handler.CommitAsync(m_cancellation)
				: ExecuteLogged(this);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task ExecuteLogged(FdbTransaction self)
			{
				long size = self.Size;
				var log = self.m_log!;
				log.CommitSize = size;
				log.TotalCommitSize += size;
				log.Attempts++;

				var tvs = log.RequiresVersionStamp ? self.m_handler.GetVersionStampAsync(self.m_cancellation) : null;

				return log.ExecuteAsync(
					self,
					new FdbTransactionLog.CommitCommand(),
					(tr, _) => tr.m_handler.CommitAsync(tr.m_cancellation),
					(tr, cmd, log) =>
					{
						log.CommittedUtc = DateTimeOffset.UtcNow;
						var cv = tr.GetCommittedVersion();
						log.CommittedVersion = cv;
						cmd.CommitVersion = cv;
						if (tvs != null) log.VersionStamp = tvs.GetAwaiter().GetResult();
					}
				);

			}
		}

		#endregion

		#region Watches...

		/// <inheritdoc />
		[Pure]
		public FdbWatch Watch(ReadOnlySpan<byte> key, CancellationToken ct)
		{
			//note: the caller CANNOT use the transaction's own token, or else the watch would not survive after the commit, rendering it useless
			if (ct.CanBeCanceled && ct.Equals(m_cancellation))
			{
				throw new ArgumentException("You cannot use the transaction's own cancellation token, because the Watch will need to execute after the transaction has completed. You may use the same token that was used by the parent retry loop, or any other token.");
			}

			ct.ThrowIfCancellationRequested();
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);

			// keep a copy of the key
			// > don't keep a reference on a potentially large buffer while the watch is active, preventing it from being garbage collected
			// > allow the caller to reuse freely the slice underlying buffer, without changing the value that we will return when the task completes
			var keyCopy = Slice.FromBytes(key);

#if DEBUG
			if (Logging.On) Logging.Verbose(this, "WatchAsync", $"Watching key '{keyCopy.ToString()}'");
#endif

			// Note: the FDBFuture returned by 'fdb_transaction_watch()' outlives the transaction, and can only be cancelled with 'fdb_future_cancel()' or 'fdb_future_destroy()'
			// Since Task<T> does not expose any cancellation mechanism by itself (and we don't want to force the caller to create a CancellationTokenSource every time),
			// we will return the FdbWatch that wraps the FdbFuture<Slice> directly, since it knows how to cancel itself.

			return PerformWatchOperation(keyCopy, ct);
		}

		private FdbWatch PerformWatchOperation(Slice key, CancellationToken ct)
		{
			if (m_log is null)
			{
				return m_handler.Watch(key, ct);
			}
			else
			{
				return ExecuteLogged(this, key, ct);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static FdbWatch ExecuteLogged(FdbTransaction self, Slice key, CancellationToken ct)
			{
				self.m_log!.RequiresVersionStamp = false;
				return self.m_log.Execute(
					(Transaction: self, Lifetime: ct),
					new FdbTransactionLog.WatchCommand(self.m_log.Grab(key), ct),
					(s, cmd) => s.Transaction.m_handler.Watch(cmd.Key, s.Lifetime)
				);
			}
		}

		#endregion

		#region OnError...

		/// <inheritdoc />
		public async Task OnErrorAsync(FdbError code)
		{
			EnsureCanRetry();

			await PerformOnErrorOperation(code).ConfigureAwait(false);

			// If fdb_transaction_on_error succeeds, that means that the transaction has been reset and is usable again
			var state = this.State;
			if (state != STATE_DISPOSED) Interlocked.CompareExchange(ref m_state, STATE_READY, state);

			RestoreDefaultSettings();
		}

		private Task PerformOnErrorOperation(FdbError code)
		{
			return m_log is null
				? m_handler.OnErrorAsync(code, ct: m_cancellation)
				: ExecuteLogged(this, code);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Task ExecuteLogged(FdbTransaction self, FdbError code)
			{
				self.m_log!.RequiresVersionStamp = false;
				return self.m_log.ExecuteAsync(
					self,
					new FdbTransactionLog.OnErrorCommand(code),
					(tr, cmd) => tr.m_handler.OnErrorAsync(cmd.Code, tr.m_cancellation)
				);
			}
		}

		#endregion

		#region Reset/Rollback/Cancel...

		internal void UseSettings(IFdbDatabaseOptions options)
		{
			m_timeout = 0;
			m_retryLimit = 0;
			m_maxRetryDelay = 0;
			m_tracingOptions = options.DefaultTracing;

			var timeout = Math.Max(0, options.DefaultTimeout);
			var retryLimit = Math.Max(0, options.DefaultRetryLimit);
			var maxRetryDelay = Math.Max(0, options.DefaultMaxRetryDelay);

			if (timeout > 0) this.Timeout = timeout;
			if (retryLimit > 0) this.RetryLimit = retryLimit;
			if (maxRetryDelay > 0) this.MaxRetryDelay = maxRetryDelay;
		}

		private void RestoreDefaultSettings()
		{
			// resetting the state of a transaction automatically clears the RetryLimit and Timeout settings
			// => we need to set the settings again!
			UseSettings(this.Database.Options);

			// if we have used a random token for VersionStamps, we need to clear it (and generate a new one)
			// => this ensures that if the error was due to a collision between the token and another part of the key,
			//    a transaction retry will hopefully use a different token that does not collide.
			m_versionStampToken = 0;
			m_versionStampCounter = 0;

			// clear any cached local data!
			this.CachedReadVersion = null;
			this.MetadataVersionKeysCache = null;
			this.Context.ClearAllLocalData();
		}

		/// <inheritdoc />
		public void Reset()
		{
			EnsureCanRetry();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Reset", "Resetting transaction");

			PerformResetOperation();

			m_state = STATE_READY;

			RestoreDefaultSettings();

			this.Context.ResetInternals();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Reset", "Transaction has been reset");
		}

		private void PerformResetOperation()
		{
			if (m_log == null)
			{
				m_handler.Reset();
			}
			else
			{
				ExecuteLogged(this);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void ExecuteLogged(FdbTransaction self)
			{
				self.m_log!.RequiresVersionStamp = false;
				self.m_log.Execute(
					self,
					new FdbTransactionLog.ResetCommand(),
					(tr, _) => tr.m_handler.Reset()
				);
			}
		}

		/// <inheritdoc />
		public void Cancel()
		{
			var state = Interlocked.CompareExchange(ref m_state, STATE_CANCELED, STATE_READY);
			if (state != STATE_READY)
			{
				switch(state)
				{
					case STATE_CANCELED: return; // already the case !
					case STATE_DISPOSED: return; // it's already dead !

					case STATE_COMMITTED: throw new InvalidOperationException("Cannot cancel transaction that has already been committed");
					case STATE_FAILED: throw new InvalidOperationException("Cannot cancel transaction because it is in a failed state");
					default: throw new InvalidOperationException($"Cannot cancel transaction because it is in unknown state {state}");
				}
			}

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Cancel", "Canceling transaction...");

			PerformCancelOperation();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Cancel", "Transaction has been canceled");
		}

		private void PerformCancelOperation()
		{
			if (m_log == null)
			{
				m_handler.Cancel();
			}
			else
			{
				ExecuteLogged(this);
			}

			static void ExecuteLogged(FdbTransaction self)
				=> self.m_log!.Execute(
					self,
					new FdbTransactionLog.CancelCommand(),
					(tr, _) => tr.m_handler.Cancel()
				);
		}

		#endregion

		#region IDisposable...

		/// <summary>Get/Sets the internal state of the exception</summary>
		internal int State
		{
			get => Volatile.Read(ref m_state);
			set
			{
				Contract.Debug.Requires(value >= STATE_DISPOSED && value <= STATE_FAILED, "Invalid state value");
				Volatile.Write(ref m_state, value);
			}
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing) and that we can proceed with a read operation</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCanRead()
		{
			// note: read operations are async, so they can NOT be called from the network without deadlocking the system !
			EnsureStillValid(allowFromNetworkThread: false, allowFailedState: false);
		}

		/// <summary>Throws if the transaction is not in a valid state (for writing) and that we can proceed with a write operation</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCanWrite()
		{
			if (m_readOnly) throw ThrowReadOnlyTransaction(this);
			// note: write operations are not async, and cannnot block, so it is (somewhat) safe to call them from the network thread itself.
			EnsureStillValid(allowFromNetworkThread: true, allowFailedState: false);
		}

		/// <summary>Throws if the transaction is not safely retryable</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCanRetry()
		{
			EnsureStillValid(allowFromNetworkThread: false, allowFailedState: true);
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing) and that we can proceed with a read or write operation</summary>
		/// <param name="allowFromNetworkThread">If true, this operation is allowed to run from a callback on the network thread and should NEVER block.</param>
		/// <param name="allowFailedState">If true, this operation can run even if the transaction is in a failed state.</param>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If CommitAsync() or Rollback() have already been called on the transaction, or if the database has been closed</exception>
		internal void EnsureStillValid(bool allowFromNetworkThread = false, bool allowFailedState = false)
		{
			// We must not be disposed
			if (allowFailedState ? this.State == STATE_DISPOSED : this.State != STATE_READY)
			{
				ThrowOnInvalidState(this);
			}

			// The cancellation token should not be signaled
			m_cancellation.ThrowIfCancellationRequested();

			// We cannot be called from the network thread (or else we will deadlock)
			if (!allowFromNetworkThread) Fdb.EnsureNotOnNetworkThread();

			// Ensure that the DB is still opened and that this transaction is still registered with it
			this.Database.EnsureTransactionIsValid(this);

			// we are ready to go !
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing)</summary>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		public void EnsureNotFailedOrDisposed()
		{
			switch (this.State)
			{
				case STATE_INIT:
				case STATE_READY:
				case STATE_COMMITTED:
				case STATE_CANCELED:
				{ // We are still valid
					// checks that the DB has not been disposed behind our back
					this.Database.EnsureTransactionIsValid(this);
					return;
				}

				default:
				{
					ThrowOnInvalidState(this);
					return;
				}
			}
		}

		[DoesNotReturn, StackTraceHidden]
		internal static void ThrowOnInvalidState(FdbTransaction trans)
		{
			switch (trans.State)
			{
				case STATE_INIT: throw new InvalidOperationException("The transaction has not been initialized properly");
				case STATE_DISPOSED: throw new ObjectDisposedException("FdbTransaction", "This transaction has already been disposed and cannot be used anymore");
				case STATE_FAILED: throw new InvalidOperationException("The transaction is in a failed state and cannot be used anymore");
				case STATE_COMMITTED: throw new InvalidOperationException("The transaction has already been committed");
				case STATE_CANCELED: throw new FdbException(FdbError.TransactionCancelled, "The transaction has already been cancelled");
				default: throw new InvalidOperationException($"The transaction is unknown state {trans.State}");
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception ThrowReadOnlyTransaction(FdbTransaction trans)
		{
			return new InvalidOperationException("Cannot write to a read-only transaction");
		}

		/// <summary>
		/// Destroy the transaction and release all allocated resources, including all non-committed changes.
		/// </summary>
		/// <remarks>This instance will not be usable again and most methods will throw an ObjectDisposedException.</remarks>
		public void Dispose()
		{
			// note: we can be called by user code, or by the FdbDatabase when it is terminating with pending transactions
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					this.Database.UnregisterTransaction(this);
					using (m_cts)
					{
						if (!m_cts.IsCancellationRequested)
						{
							try { m_cts.Cancel(); } catch(ObjectDisposedException) { }
						}
					}

					if (Logging.On) Logging.Verbose(this, "Dispose", $"Transaction #{m_id} has been disposed");
				}
				finally
				{
					// Dispose of the handler
					try { m_handler.Dispose(); }
					catch(Exception e)
					{
						if (Logging.On) Logging.Error(this, "Dispose", $"Transaction #{m_id} failed to dispose the transaction handler: [{e.GetType().Name}] {e.Message}");
					}

					var context = this.Context;
					context.ReleaseTransaction(this);
					if (!context.Shared)
					{
						context.Dispose();
					}
					m_cts.Dispose();

					if (m_log?.Stop(this) == true)
					{
						if (m_logHandler != null)
						{
							try
							{
								m_logHandler.Invoke(m_log);
							}
#if DEBUG
							catch(Exception e)
							{
								System.Diagnostics.Debug.WriteLine($"Logged transaction handler failed: {e}");
							}
#else
							catch { }
#endif
						}
					}
				}
			}
		}

		#endregion

	}

}
