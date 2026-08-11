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
// enable this to capture the stacktrace of the ctor, when troubleshooting leaked transaction handles
//#define CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using FoundationDB.Client.Core;

	/// <summary>Wraps a native FDB_TRANSACTION handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Size={m_payloadBytes}, Closed={m_handle.IsClosed}")]
	internal class FdbNativeTransaction : IFdbTransactionHandler, IEquatable<FdbNativeTransaction>
	{
		/// <summary>FDB_DATABASE* handle</summary>
		private readonly FdbNativeDatabase m_database;

		/// <summary>FDB_TENANT* handle (optional)</summary>
		private readonly FdbNativeTenant? m_tenant;

		/// <summary>FDB_TRANSACTION* handle</summary>
		private readonly TransactionHandle m_handle;

		/// <summary>Number of writes performed by this transaction</summary>
		/// <remarks>A <c>ClearRange</c> will increment the counter by one, even if it can update a large number of keys</remarks>
		private int m_keyWriteCount;

		/// <summary>Estimated current size of the transaction mutations</summary>
		private long m_payloadBytes;
		//TODO: this is redundant with GetApproximateSize which does the exact bookkeeping (but is async!). Should we keep it? or remove it?

		/// <summary>Number of keys read by this transaction</summary>
		/// <remarks>Note: a <c>GetRange</c> will increment the counter by the number of results</remarks>
		private int m_keyReadCount;

		/// <summary>Estimated current size of the number of bytes read from the cluster</summary>
		/// <remarks>Includes the size of the both keys and values</remarks>
		private long m_keyReadSize;

#if CAPTURE_STACKTRACES
		private StackTrace m_stackTrace;
#endif

		public FdbNativeTransaction(FdbNativeDatabase db, FdbNativeTenant? tenant, TransactionHandle? handle)
		{
			Contract.NotNull(db);
			Contract.NotNull(handle);

			m_database = db;
			m_tenant = tenant;
			m_handle = handle;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

#if DEBUG
		// We add a destructor in DEBUG builds to help track leaks of transactions...
		~FdbNativeTransaction()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A transaction handle (" + m_handle + ", " + m_payloadBytes + " bytes written) was leaked by " + m_stackTrace);
#endif
			// If you break here, that means that a native transaction handler was leaked by a FdbTransaction instance (or that the transaction instance was leaked)
			if (Debugger.IsAttached) Debugger.Break();
			Dispose(false);
		}
#endif

		#region Properties...

		public bool IsClosed => m_handle.IsClosed;

		/// <summary>Native FDB_TRANSACTION* handle</summary>
		public TransactionHandle Handle => m_handle;

		/// <summary>Database handler that owns this transaction</summary>
		public FdbNativeDatabase Database => m_database;

		/// <summary>Tenant handler that owns this transaction (optional)</summary>
		public FdbNativeTenant? Tenant => m_tenant;

		/// <inheritdoc />
		public long Size => m_payloadBytes;
		//TODO: this is redundant with GetApproximateSize which does the exact bookkeeping (but is async!). Should we keep it? or get remove it?

		public (int Keys, long Size) GetWriteStatistics() => (Volatile.Read(ref m_keyWriteCount), Volatile.Read(ref m_payloadBytes));

		public (int Keys, long Size) GetReadStatistics() => (Volatile.Read(ref m_keyReadCount), Volatile.Read(ref m_keyReadSize));

		#endregion

		#region Options...

		/// <inheritdoc />
		public void SetOption(FdbTransactionOption option, ReadOnlySpan<byte> data)
		{
			Fdb.EnsureNotOnNetworkThread();

			// idempotency ids landed in api level 720 (fdb 7.2); reject early with a clear message rather than a cryptic native error or a silent no-op against an older cluster
			if ((option is FdbTransactionOption.IdempotencyId or FdbTransactionOption.AutomaticIdempotency) && this.Database.GetApiVersion() < 720)
			{
				throw new NotSupportedException($"The {option} transaction option requires API level 720 or greater. Your application has selected API version {this.Database.GetApiVersion()} which is too low. Select API version 720 or greater to use FoundationDB idempotency.");
			}

			unsafe
			{
				fixed (byte* ptr = data)
				{
					FdbNative.DieOnError(FdbNative.TransactionSetOption(m_handle, option, ptr, data.Length));
				}
			}
		}

		#endregion

		#region Reading...

		private void AccountReadOperation(int count, long payload)
		{
			Interlocked.Increment(ref m_keyReadCount);
			Interlocked.Add(ref m_keyReadSize, payload);
		}

		/// <inheritdoc />
		public Task<long> GetReadVersionAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

			var future = FdbNative.TransactionGetReadVersion(m_handle);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					// for 610 and below, we must use fdb_future_get_version
					// for 620 or above, we must use fdb_future_get_int64
					var err = Fdb.BindingVersion < 620 
						? FdbNative.FutureGetVersion(h, out var version)
						: FdbNative.FutureGetInt64(h, out version);

#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetReadVersion() => err=" + err + ", version=" + version);
#endif
					FdbNative.DieOnError(err);
					return version;
				},
				ct
			);
		}

		public void SetReadVersion(long version)
		{
			FdbNative.TransactionSetReadVersion(m_handle, version);
		}

		private static bool TryPeekValueResultBytes(FutureHandle h, out ReadOnlySpan<byte> result)
		{
			Contract.Debug.Requires(h != null);
			var err = FdbNative.FutureGetValue(h, out bool present, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryPeekValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			return present;
		}

		private static Slice GetValueResultBytes(FutureHandle h, out int readBytes)
		{
			Contract.Debug.Requires(h != null);

			readBytes = 0;
			var err = FdbNative.FutureGetValue(h, out bool present, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			readBytes = result.Length;
			return present ? Slice.FromBytes(result) : Slice.Nil;
		}

		private static TResult GetValueResultBytes<TResult>(FutureHandle h, FdbValueDecoder<TResult> decoder, out int readBytes)
		{
			Contract.Debug.Requires(h != null);

			readBytes = 0;
			var err = FdbNative.FutureGetValue(h, out bool present, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			readBytes = result.Length;
			return decoder(result, present);
		}

		private static TResult GetValueResultBytes<TState, TResult>(FutureHandle h, TState state, FdbValueDecoder<TState, TResult> decoder, out int readBytes)
		{
			Contract.Debug.Requires(h != null);

			readBytes = 0;
			var err = FdbNative.FutureGetValue(h, out bool present, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			readBytes = result.Length;
			return decoder(state, result, present);
		}

		private static bool GetValueResultBytes(FutureHandle h, IBufferWriter<byte> writer, out int readBytes)
		{
			Contract.Debug.Requires(h != null);
			Contract.Debug.Requires(writer != null);

			readBytes = 0;
			var err = FdbNative.FutureGetValue(h, out bool present, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryGetValueResult() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			readBytes = result.Length;

			if (present)
			{
				writer.Write(result);
			}

			return present;
		}

		public Task<Slice> GetAsync(ReadOnlySpan<byte> key, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice>(ct);

			var future = FdbNative.TransactionGet(m_handle, key, snapshot);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (handle, tr) =>
				{
					var res = GetValueResultBytes(handle, out int read);
					tr.AccountReadOperation(1, read);
					return res;
				},
				ct
			);
		}

		public Task<TResult> GetAsync<TResult>(ReadOnlySpan<byte> key, bool snapshot, FdbValueDecoder<TResult> decoder, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);

			var future = FdbNative.TransactionGet(m_handle, key, snapshot);

			return FdbFuture.CreateTaskFromHandle(
				future,
				(Transaction: this, Decoder: decoder),
				static (h, s) =>
				{
					var res = GetValueResultBytes(h, s.Decoder, out int read);
					s.Transaction.AccountReadOperation(1, read);
					return res;
				},
				ct
			);
		}

		public Task<TResult> GetAsync<TState, TResult>(ReadOnlySpan<byte> key, bool snapshot, TState state, FdbValueDecoder<TState, TResult> decoder, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);

			var future = FdbNative.TransactionGet(m_handle, key, snapshot);

			return FdbFuture.CreateTaskFromHandle(
				future,
				(Transaction: this, State: state, Decoder: decoder),
				static (h, s) =>
				{
					var res = GetValueResultBytes(h, s.State, s.Decoder, out int read);
					s.Transaction.AccountReadOperation(1, read);
					return res;
				},
				ct
			);
		}

		/// <inheritdoc />
		public Task<Slice[]> GetValuesAsync(ReadOnlySpan<Slice> keys, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

			if (keys.Length == 0)
			{
				return Task.FromResult(Array.Empty<Slice>());
			}

			//HACKHACK: as of now (730), there is no way to read multiple keys or values in a single API call
			// so we have to start one get request per key, and hide them all inside on "meta" future.
			// this is still has the same overhead for interop with the native library, but reduces the number of Task objects allocated by the CLR.

			var futures = new FutureHandle[keys.Length];
			try
			{
				//note: if one of the operation triggers an error, the array will be partially filled, but all previous futures will be canceled in the catch block below
				for (int i = 0; i < keys.Length; i++)
				{
					futures[i] = FdbNative.TransactionGet(m_handle, keys[i].Span, snapshot);
				}
			}
			catch
			{
				// we need to cancel any future created before the error
				foreach(var future in futures)
				{
					if (future == null!) break;
					future.Dispose();
				}
				throw;
			}

			return FdbFuture.CreateTaskFromHandleArray(
				futures,
				(Transaction: this, Buffer: new Slice[futures.Length]),
				static (h, idx, state) =>
				{
					//note: this is called once per key!
					state.Buffer[idx] = GetValueResultBytes(h, out int read);
					state.Transaction.AccountReadOperation(1, read);
				},
				static (state) => state.Buffer,
				ct
			);
		}

		/// <summary>Box that maintains a running sum of the length of keys/values</summary>
		[DebuggerDisplay("Value={Accumulator}")]
		private sealed class SizeAccumulator
		{

			/// <summary>Running total</summary>
			private long Accumulator;

			/// <summary>Current value of the accumulator</summary>
			public long Value => this.Accumulator;

			/// <summary>Adds a length to the accumulator</summary>
			public void Add(int length) => Interlocked.Add(ref this.Accumulator, length);

			/// <summary>Adds the length of a key or value to the accumulator</summary>
			public void Add(Slice data) => Interlocked.Add(ref this.Accumulator, data.Count);

			/// <summary>Adds the length of a key or value to the accumulator</summary>
			public void Add(ReadOnlySpan<byte> data) => Interlocked.Add(ref this.Accumulator, data.Length);

		}

		/// <inheritdoc />
		public Task<long> GetValuesAsync<TValue>(ReadOnlySpan<Slice> keys, Memory<TValue> values, FdbValueDecoder<TValue> decoder, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

			if (keys.Length == 0)
			{
				return Task.FromResult(0L);
			}

			//HACKHACK: as of now (730), there is no way to read multiple keys or values in a single API call
			// so we have to start one get request per key, and hide them all inside on "meta" future.
			// this is still has the same overhead for interop with the native library, but reduces the number of Task objects allocated by the CLR.

			var futures = new FutureHandle[keys.Length];
			try
			{
				//note: if one of the operation triggers an error, the array will be partially filled, but all previous futures will be canceled in the catch block below
				for (int i = 0; i < keys.Length; i++)
				{
					futures[i] = FdbNative.TransactionGet(m_handle, keys[i].Span, snapshot);
				}
			}
			catch
			{
				// we need to cancel any future created before the error
				foreach(var future in futures)
				{
					if (future == null!) break;
					future.Dispose();
				}
				throw;
			}

			return FdbFuture.CreateTaskFromHandleArray(
				futures,
				(Transaction: this, Buffer: values, Decoder: decoder, TotalSize: new SizeAccumulator()),
				static (h, idx, args) =>
				{
					//note: this is called once per key!
					args.Buffer.Span[idx] = GetValueResultBytes(h, args.Decoder, out int read);
					args.Transaction.AccountReadOperation(1, read);
					args.TotalSize.Add(read);
				},
				static (state) => state.TotalSize.Value,
				ct
			);
		}

		/// <inheritdoc />
		public Task<long> GetValuesAsync<TState, TValue>(ReadOnlySpan<Slice> keys, Memory<TValue> values, TState state, FdbValueDecoder<TState, TValue> decoder, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

			if (keys.Length == 0)
			{
				return Task.FromResult(0L);
			}

			//HACKHACK: as of now (730), there is no way to read multiple keys or values in a single API call
			// so we have to start one get request per key, and hide them all inside on "meta" future.
			// this is still has the same overhead for interop with the native library, but reduces the number of Task objects allocated by the CLR.

			var futures = new FutureHandle[keys.Length];
			try
			{
				//note: if one of the operation triggers an error, the array will be partially filled, but all previous futures will be canceled in the catch block below
				for (int i = 0; i < keys.Length; i++)
				{
					futures[i] = FdbNative.TransactionGet(m_handle, keys[i].Span, snapshot);
				}
			}
			catch
			{
				// we need to cancel any future created before the error
				foreach(var future in futures)
				{
					if (future == null!) break;
					future.Dispose();
				}
				throw;
			}

			return FdbFuture.CreateTaskFromHandleArray(
				futures,
				(Transaction: this, Buffer: values[..futures.Length], State: state, Decoder: decoder, TotalSize: new SizeAccumulator()),
				static (h, idx, args) =>
				{
					//note: this is called once per key!
					args.Buffer.Span[idx] = GetValueResultBytes(h, args.State, args.Decoder, out int read);
					args.TotalSize.Add(read);
				},
				static (state) =>
				{
					var total = state.TotalSize.Value;
					state.Transaction.AccountReadOperation(state.Buffer.Length, total);
					return total;
				}, //TODO: how can we accumulate the total size of the values, without allocating too much?
				ct
			);
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="pool">Optional pool used to allocate the buffer for the keys and values (use the heap if null)</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <param name="buffer">Receives the buffer used to store the keys and values (so that it can be returned to the pool at a later time)</param>
		/// <param name="dataBytes">Total size of keys and values</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResult(FutureHandle h, ArrayPool<byte>? pool, out bool more, out Slice first, out Slice last, out SliceOwner buffer, out int dataBytes)
		{
			var err = FdbNative.FutureGetKeyValueArray(h, pool, out var result, out more, out buffer, out dataBytes);
			FdbNative.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Debug.Ensures(result != null);
			first = result.Length > 0 ? result[0].Key : default;
			last = result.Length > 0 ? result[^1].Key : default;
			return result;
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <param name="dataBytes">Total size of keys</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResultKeysOnly(FutureHandle h, out bool more, out Slice first, out Slice last, out int dataBytes)
		{
			var err = FdbNative.FutureGetKeyValueArrayKeysOnly(h, out var result, out more, out dataBytes);
			FdbNative.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Debug.Ensures(result != null);
			first = result.Length > 0 ? result[0].Key : default;
			last = result.Length > 0 ? result[^1].Key : default;
			return result;
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <param name="dataBytes">Total size of values</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResultValuesOnly(FutureHandle h, out bool more, out Slice first, out Slice last, out int dataBytes)
		{
			var err = FdbNative.FutureGetKeyValueArrayValuesOnly(h, out var result, out more, out first, out last, out dataBytes);
			FdbNative.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Debug.Ensures(result != null);
			return result;
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<FdbRangeChunk> GetRangeAsync(KeySpanSelector beginInclusive, KeySpanSelector endExclusive, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken ct)
		{
			Contract.Debug.Requires(options != null);
			if (ct.IsCancellationRequested) return Task.FromCanceled<FdbRangeChunk>(ct);

			var future = FdbNative.TransactionGetRange(
				m_handle,
				beginInclusive,
				endExclusive,
				options.Limit ?? 0,
				options.TargetBytes ?? 0,
				options.Streaming ?? FdbStreamingMode.Iterator,
				iteration,
				snapshot,
				options.IsReversed
			);

			return FdbFuture.CreateTaskFromHandle(
				future,
				(Transaction: this, Options: options, Iteration: iteration),
				static (h, s) =>
				{
					KeyValuePair<Slice, Slice>[] items;
					bool hasMore;
					Slice first, last;
					int totalBytes;

					SliceOwner buffer = default;

					switch (s.Options.Fetch ?? FdbFetchMode.KeysAndValues)
					{
						case FdbFetchMode.KeysAndValues:
						{
							items = GetKeyValueArrayResult(h, s.Options.Pool, out hasMore, out first, out last, out buffer, out totalBytes);
							break;
						}
						case FdbFetchMode.KeysOnly:
						{
							items = GetKeyValueArrayResultKeysOnly(h, out hasMore, out first, out last, out totalBytes);
							break;
						}
						case FdbFetchMode.ValuesOnly:
						{
							items = GetKeyValueArrayResultValuesOnly(h, out hasMore, out first, out last, out totalBytes);
							break;
						}
						default:
						{
							throw new InvalidOperationException();
						}
					}

					s.Transaction.AccountReadOperation(items.Length, totalBytes);

					return new FdbRangeChunk(items, hasMore, s.Iteration, s.Options, first, last, totalBytes, buffer);
				},
				ct
			);
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		public Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(KeySpanSelector beginInclusive, KeySpanSelector endExclusive, bool snapshot, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions options, int iteration, CancellationToken ct)
		{
			Contract.Debug.Requires(decoder != null && options != null);
			if (ct.IsCancellationRequested) return Task.FromCanceled<FdbRangeChunk<TResult>>(ct);

			var future = FdbNative.TransactionGetRange(
				m_handle,
				beginInclusive,
				endExclusive,
				options.Limit ?? 0,
				options.TargetBytes ?? 0,
				options.Streaming ?? FdbStreamingMode.Iterator,
				iteration,
				snapshot,
				options.IsReversed
			);

			return FdbFuture.CreateTaskFromHandle(
				future,
				(Transaction: this, Options: options, Iteration: iteration, State: state, Decoder: decoder),
				static (h, s) =>
				{
					// note: we don't have a way currently to do KeyOnly or ValueOnly, we always have both coming from the native client
					// but since we don't have to copy them into the managed heap, this is less of an issue

					var items = FdbNative.FutureGetKeyValueArray(h, s.State, s.Decoder, out var hasMore, out var first, out var last, out var totalBytes);

					s.Transaction.AccountReadOperation(items.Length, totalBytes);

					return new FdbRangeChunk<TResult>(items, hasMore, s.Iteration, s.Options, first, last, totalBytes);
				},
				ct
			);
		}

		/// <summary>Asynchronously fetch a new page of results and visit all returned key-value pairs</summary>
		public Task<FdbRangeResult> VisitRangeAsync<TState>(KeySpanSelector beginInclusive, KeySpanSelector endExclusive, bool snapshot, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions options, int iteration, CancellationToken ct)
		{
			Contract.Debug.Requires(visitor != null && options != null);
			if (ct.IsCancellationRequested) return Task.FromCanceled<FdbRangeResult>(ct);

			var future = FdbNative.TransactionGetRange(
				m_handle,
				beginInclusive,
				endExclusive,
				options.Limit ?? 0,
				options.TargetBytes ?? 0,
				options.Streaming ?? FdbStreamingMode.Iterator,
				iteration,
				snapshot,
				options.IsReversed
			);

			return FdbFuture.CreateTaskFromHandle(
				future,
				(Transaction: this, Options: options, Iteration: iteration, State: state, Visitor: visitor),
				static (h, s) =>
				{
					// note: we don't have a way currently to do KeyOnly or ValueOnly, we always have both coming from the native client
					// but since we don't have to copy them into the managed heap, this is less of an issue

					var count = FdbNative.VisitKeyValueArray(h, s.State, s.Visitor, out var hasMore, out var first, out var last, out var totalBytes);

					s.Transaction.AccountReadOperation(count, totalBytes);

					return new FdbRangeResult(count, hasMore, s.Iteration, s.Options, first, last, totalBytes);
				},
				ct
			);
		}

		private static Slice GetKeyResult(FutureHandle h, out int bytesRead)
		{
			Contract.Debug.Requires(h != null);

			bytesRead = 0;
			var err = FdbNative.FutureGetKey(h, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetKeyResult() => err=" + err + ", result=" + result.ToString());
#endif
			FdbNative.DieOnError(err);

			bytesRead = result.Length;
			return Slice.FromBytes(result);
		}

		public Task<Slice> GetKeyAsync(KeySpanSelector selector, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice>(ct);

			var future = FdbNative.TransactionGetKey(m_handle, selector, snapshot);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, tr) =>
				{
					var res = GetKeyResult(h, out var bytesRead);
					tr.AccountReadOperation(1, bytesRead);
					return res;
				},
				ct
			);
		}

		public Task<Slice[]> GetKeysAsync(ReadOnlySpan<KeySelector> selectors, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

			if (selectors.Length == 0)
			{
				return Task.FromResult<Slice[]>([ ]);
			}

			//HACKHACK: as of now (730), there is no way to read multiple keys or values in a single API call
			// so we have to start one get request per key, and hide them all inside on "meta" future.
			// this is still has the same overhead for interop with the native library, but reduces the number of Task objects allocated by the CLR.

			var futures = new FutureHandle[selectors.Length];
			try
			{
				//note: if one of the operation triggers an error, the array will be partially filled, but all previous futures will be canceled in the catch block below
				for (int i = 0; i < selectors.Length; i++)
				{
					futures[i] = FdbNative.TransactionGetKey(m_handle, selectors[i].ToSpan(), snapshot);
				}
			}
			catch
			{
				// we need to cancel any future created before the error
				foreach (var future in futures)
				{
					if (future == null!) break;
					future.Dispose();
				}

				throw;
			}
			return FdbFuture.CreateTaskFromHandleArray(
				futures,
				(Transaction: this, Buffer: new Slice[futures.Length]),
				static (h, idx, state) =>
				{
					//note: this is called once per key!
					state.Buffer[idx] = GetKeyResult(h, out var bytesRead);
					state.Transaction.AccountReadOperation(1, bytesRead);
				},
				static (state) => state.Buffer,
				ct
			);
		}

		public ValueTask<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return new(Task.FromCanceled<(FdbValueCheckResult Result, Slice Actual)>(ct));

			var future = FdbNative.TransactionGet(m_handle, key, snapshot);

			return FdbFuture.CreateValueTaskFromHandle(
				future,
				(Transaction: this, Expected: expected),
				static (h, s) =>
				{
					if (TryPeekValueResultBytes(h, out var actual))
					{ // key exists
						s.Transaction.AccountReadOperation(1, actual.Length);
						return !s.Expected.IsNull && s.Expected.Span.SequenceEqual(actual) ? (FdbValueCheckResult.Success, s.Expected) : (FdbValueCheckResult.Failed, Slice.FromBytes(actual));
					}
					else
					{ // key does not exist, pass only if expected is Nil
						s.Transaction.AccountReadOperation(1, 0);
						return s.Expected.IsNull ? (FdbValueCheckResult.Success, Slice.Nil) : (FdbValueCheckResult.Failed, Slice.Nil);
					}
				},
				ct
			);
		}

		#endregion

		#region Writing...

		private void AccountWriteOperation(int payload)
		{
			Interlocked.Increment(ref m_keyWriteCount);
			Interlocked.Add(ref m_payloadBytes, payload);
		}

		/// <inheritdoc />
		public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			FdbNative.TransactionSet(m_handle, key, value);

			// There is a 28-byte overhead pet Set(..) in a transaction
			// cf http://community.foundationdb.com/questions/547/transaction-size-limit
			AccountWriteOperation(key.Length + value.Length + 28);
		}

		/// <inheritdoc />
		public void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType type)
		{
			FdbNative.TransactionAtomicOperation(m_handle, key, param, type);

			//TODO: what is the overhead for atomic operations?
			AccountWriteOperation(key.Length + param.Length);

		}

		/// <inheritdoc />
		public void Clear(ReadOnlySpan<byte> key)
		{
			FdbNative.TransactionClear(m_handle, key);
			// The key is converted to range [key, key.'\0'), and there is an overhead of 28-byte per operation
			AccountWriteOperation((key.Length * 2) + 28 + 1);
		}

		/// <inheritdoc />
		public void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			FdbNative.TransactionClearRange(m_handle, beginKeyInclusive, endKeyExclusive);
			// There is an overhead of 28-byte per operation
			AccountWriteOperation(beginKeyInclusive.Length + endKeyExclusive.Length + 28);
		}

		/// <inheritdoc />
		public void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			FdbError err = FdbNative.TransactionAddConflictRange(m_handle, beginKeyInclusive, endKeyExclusive, type);
			FdbNative.DieOnError(err);
		}

		/// <inheritdoc />
		public Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<string[]>(ct);

			var future = FdbNative.TransactionGetAddressesForKey(m_handle, key);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					Contract.Debug.Requires(h != null);

					var err = FdbNative.FutureGetStringArray(h, out var result);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[].FutureGetStringArray() => err=" + err + ", results=" + (result == null ? "<null>" : result.Length.ToString()));
#endif
					FdbNative.DieOnError(err);
					Contract.Debug.Ensures(result != null); // can only be null in case of an error
					return result;
				},
				ct
			);
		}

		/// <inheritdoc />
		public Task<Slice[]> GetRangeSplitPointsAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

			var future = FdbNative.TransactionGetRangeSplitPoints(m_handle, beginKey, endKey, chunkSize);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					Contract.Debug.Requires(h != null);

					var err = FdbNative.FutureGetKeyArray(h, out var result);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[].FutureGetKeyArray() => err=" + err + ", results=" + (result == null ? "<null>" : result.Length.ToString()));
#endif
					FdbNative.DieOnError(err);
					Contract.Debug.Ensures(result != null); // can only be null in case of an error
					return result;
				},
				ct
			);
		}

		/// <inheritdoc />
		public Task<long> GetEstimatedRangeSizeBytesAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

			var future = FdbNative.TransactionGetEstimatedRangeSizeBytes(m_handle, beginKey, endKey);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					var err = FdbNative.FutureGetInt64(h, out long size);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetEstimatedRangeSizeBytesAsync() => err=" + err + ", size=" + size);
#endif
					FdbNative.DieOnError(err);
					return size;
				},
				ct
			);
		}

		/// <inheritdoc />
		public Task<long> GetApproximateSizeAsync(CancellationToken ct)
		{
			// API was introduced in 6.2
			if (this.Database.GetApiVersion() < 620) throw new NotSupportedException($"The GetApproximateSize method is only available for version 6.2 or greater. Your application has selected API version {this.Database.GetApiVersion()} which is too low. You will need to select API version 620 or greater.");
			//TODO: for lesser version, maybe we could return our own estimation?

			if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

			var future = FdbNative.TransactionGetApproximateSize(m_handle);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					var err = FdbNative.FutureGetInt64(h, out long size);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetApproximateSize() => err=" + err + ", size=" + size);
#endif
					FdbNative.DieOnError(err);
					return size;
				},
				ct
			);
		}

		#endregion

		#region Watches...

		public FdbWatch Watch(Slice key, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var future = FdbNative.TransactionWatch(m_handle, key.Span);
			return new FdbWatch(
				FdbFuture.FromHandle(
					future,
					key,
					static (_, k) => k,
					ct
				),
				key
			);
		}

		#endregion

		#region State management...

		public long GetCommittedVersion()
		{
			var err = FdbNative.TransactionGetCommittedVersion(m_handle, out long version);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].GetCommittedVersion() => err=" + err + ", version=" + version);
#endif
			FdbNative.DieOnError(err);
			return version;
		}

		public Task<VersionStamp> GetVersionStampAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<VersionStamp>(ct);

			var future = FdbNative.TransactionGetVersionStamp(m_handle);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					Contract.Debug.Requires(h != null);
					var err = FdbNative.FutureGetVersionStamp(h, out VersionStamp stamp);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].FutureGetVersionStamp() => err=" + err + ", vs=" + stamp + ")");
#endif
					FdbNative.DieOnError(err);

					return stamp;
				},
				ct
			);
		}

		/// <summary>
		/// Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <returns>Task that succeeds if the transaction was committed successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don't check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		public Task CommitAsync(CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var future = FdbNative.TransactionCommit(m_handle);

			return FdbFuture.CreateTaskFromHandle(
				future,
				ct
			);
		}

		public Task OnErrorAsync(FdbError code, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var future = FdbNative.TransactionOnError(m_handle, code);

			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (_, tr) =>
				{
					tr.ResetInternal();
					return default(object?);
				},
				ct
			);
		}

		public void Reset()
		{
			FdbNative.TransactionReset(m_handle);
			ResetInternal();
		}

		public void Cancel()
		{
			FdbNative.TransactionCancel(m_handle);
		}

		private void ResetInternal()
		{
			m_keyWriteCount = 0;
			m_payloadBytes = 0;
			m_keyReadCount = 0;
			m_keyReadSize = 0;
		}

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Dispose of the handle
				if (!m_handle.IsClosed) m_handle.Dispose();
			}
		}

		#endregion

		#region IEquatable...

		public override string ToString() => $"FdbTransaction(0x{m_handle.Handle:x})";

		public bool Equals(FdbNativeTransaction? other) => ReferenceEquals(other, this) || (other != null && other.m_handle.Equals(m_handle));

		public override bool Equals(object? obj) => obj is FdbNativeTransaction tr && Equals(tr);

		public override int GetHashCode() => m_handle.GetHashCode();

		#endregion

	}

}
