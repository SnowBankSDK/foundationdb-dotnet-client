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
	using System.ComponentModel;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB database implementations.</summary>
	[PublicAPI]
	public static class FdbDatabaseExtensions
	{

		#region Transactions...

		/// <summary>Start a new read-only transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		[Pure]
		[Obsolete("Use BeginReadOnlyTransaction() instead")]
		public static ValueTask<IFdbReadOnlyTransaction> BeginReadOnlyTransactionAsync(this IFdbDatabase db, CancellationToken ct)
		{
			Contract.NotNull(db);
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IFdbReadOnlyTransaction>(ct);
			return ValueTask.FromResult<IFdbReadOnlyTransaction>(db.BeginTransaction(FdbTransactionMode.ReadOnly, ct));
		}

		/// <summary>Start a new read-only transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		[Pure]
		public static IFdbReadOnlyTransaction BeginReadOnlyTransaction(this IFdbDatabase db, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.BeginTransaction(FdbTransactionMode.ReadOnly, ct);
		}

		/// <summary>Start a new transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		[Pure]
		[Obsolete("Use BeginTransaction() instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static ValueTask<IFdbTransaction> BeginTransactionAsync(this IFdbDatabase db, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.BeginTransactionAsync(FdbTransactionMode.Default, ct);
		}

		/// <summary>Start a new transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		[Pure]
		public static IFdbTransaction BeginTransaction(this IFdbDatabase db, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.BeginTransaction(FdbTransactionMode.Default, ct);
		}

		#endregion

		#region Tenants...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant(this IFdbDatabase db, Slice name)
		{
			Contract.NotNull(db);
			if (name.IsNullOrEmpty) throw new ArgumentException("Tenant name cannot be empty", nameof(name));
			return db.GetTenant(FdbTenantName.Create(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant(this IFdbDatabase db, ReadOnlySpan<byte> name)
		{
			Contract.NotNull(db);
			if (name.Length == 0) throw new ArgumentException("Tenant name cannot be empty", nameof(name));
			return db.GetTenant(FdbTenantName.Create(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant<TTuple>(this IFdbDatabase db, TTuple name)
			where TTuple : IVarTuple
		{
			Contract.NotNull(db);
			Contract.NotNull(name);
			if (name.Count == 0) throw new ArgumentException("Tenant name cannot be empty", nameof(name));
			return db.GetTenant(FdbTenantName.FromTuple(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant<T1>(this IFdbDatabase db, ValueTuple<T1> name)
		{
			Contract.NotNull(db);
			Contract.NotNull(name);
			return db.GetTenant(FdbTenantName.FromTuple(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant<T1, T2>(this IFdbDatabase db, ValueTuple<T1, T2> name)
		{
			Contract.NotNull(db);
			Contract.NotNull(name);
			return db.GetTenant(FdbTenantName.FromTuple(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant<T1, T2, T3>(this IFdbDatabase db, ValueTuple<T1, T2, T3> name)
		{
			Contract.NotNull(db);
			Contract.NotNull(name);
			return db.GetTenant(FdbTenantName.FromTuple(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant<T1, T2, T3, T4>(this IFdbDatabase db, ValueTuple<T1, T2, T3, T4> name)
		{
			Contract.NotNull(db);
			Contract.NotNull(name);
			return db.GetTenant(FdbTenantName.FromTuple(name));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbTenant GetTenant<T1, T2, T3, T4, T5>(this IFdbDatabase db, ValueTuple<T1, T2, T3, T4, T5> name)
		{
			Contract.NotNull(db);
			Contract.NotNull(name);
			return db.GetTenant(FdbTenantName.FromTuple(name));
		}

		#endregion

		#region Options...

		/// <summary>Set the default Timeout value for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public static IFdbDatabaseOptions WithDefaultTimeout(this IFdbDatabaseOptions options, TimeSpan timeout)
		{
			options.DefaultTimeout = timeout == TimeSpan.Zero ? 0 : checked((int) Math.Ceiling(timeout.TotalMilliseconds));
			return options;
		}

		/// <summary>Set the default Timeout value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public static IFdbDatabaseOptions WithDefaultTimeout(this IFdbDatabaseOptions options, int timeout)
		{
			options.DefaultTimeout = timeout;
			return options;
		}

		/// <summary>Set the default Retry Limit value for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public static IFdbDatabaseOptions WithDefaultRetryLimit(this IFdbDatabaseOptions options, int limit)
		{
			options.DefaultRetryLimit = limit;
			return options;
		}

		/// <summary>Set the default maximum retry delay value for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public static IFdbDatabaseOptions WithDefaultMaxRetryDelay(this IFdbDatabaseOptions options, TimeSpan timeout)
		{
			options.DefaultMaxRetryDelay = timeout == TimeSpan.Zero ? 0 : checked((int) Math.Ceiling(timeout.TotalMilliseconds));
			return options;
		}

		/// <summary>Set the default maximum retry delay value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public static IFdbDatabaseOptions WithDefaultMaxRetryDelay(this IFdbDatabaseOptions options, int timeout)
		{
			options.DefaultMaxRetryDelay = timeout;
			return options;
		}

		/// <summary>Set the size of the client location cache. Raising this value can boost performance in very large databases where clients access data in a near-random pattern. Defaults to 100000.</summary>
		/// <param name="options">Database instance</param>
		/// <param name="size">Max location cache entries</param>
		public static IFdbDatabaseOptions WithLocationCacheSize(this IFdbDatabaseOptions options, int size)
		{
			if (size < 0) throw new FdbException(FdbError.InvalidOptionValue, "Location cache size must be a positive integer");

			return options.SetOption(FdbDatabaseOption.LocationCacheSize, size);
		}

		/// <summary>Set the maximum number of watches allowed to be outstanding on a database connection. Increasing this number could result in increased resource usage. Reducing this number will not cancel any outstanding watches. Defaults to 10000 and cannot be larger than 1000000.</summary>
		/// <param name="options">Database instance</param>
		/// <param name="count">Max outstanding watches</param>
		public static IFdbDatabaseOptions WithMaxWatches(this IFdbDatabaseOptions options, int count)
		{
			if (count < 0) throw new FdbException(FdbError.InvalidOptionValue, "Maximum outstanding watches count must be a positive integer");

			return options.SetOption(FdbDatabaseOption.MaxWatches, count);
		}

		/// <summary>Specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		/// <param name="options">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static IFdbDatabaseOptions WithMachineId(this IFdbDatabaseOptions options, string hexId)
		{
			return options.SetOption(FdbDatabaseOption.MachineId, hexId.AsSpan());
		}

		/// <summary>Specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		/// <param name="options">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static IFdbDatabaseOptions WithMachineId(this IFdbDatabaseOptions options, ReadOnlySpan<char> hexId)
		{
			return options.SetOption(FdbDatabaseOption.MachineId, hexId);
		}

		/// <summary>Specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		/// <param name="options">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static IFdbDatabaseOptions WithDataCenterId(this IFdbDatabaseOptions options, string hexId)
		{
			return options.SetOption(FdbDatabaseOption.DataCenterId, hexId.AsSpan());
		}

		/// <summary>Specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		/// <param name="options">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static IFdbDatabaseOptions WithDataCenterId(this IFdbDatabaseOptions options, ReadOnlySpan<char> hexId)
		{
			return options.SetOption(FdbDatabaseOption.DataCenterId, hexId);
		}

		/// <summary>Specify the default tracing options for all transactions created from this database</summary>
		/// <param name="options">Database instance</param>
		/// <param name="tracing">Tracing options</param>
		/// <returns></returns>
		public static IFdbDatabaseOptions WithTracing(this IFdbDatabaseOptions options, FdbTracingOptions tracing)
		{
			options.DefaultTracing = tracing;
			return options;
		}

		#endregion

		#region Standard Operations

		//REVIEW: this is too dangerous!
		// Users may call GetAsync() or SetAsync() multiple times, outside of a transaction !!!

		/// <summary>Read a single key from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to read several keys at once, use a version of <see cref="GetValuesAsync(FoundationDB.Client.IFdbReadOnlyRetryable,System.Collections.Generic.IEnumerable{System.Slice},System.Threading.CancellationToken)"/>.
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<Slice> GetAsync(this IFdbReadOnlyRetryable db, Slice key, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.ReadAsync((tr) => tr.GetAsync(key), ct);
		}

		/// <summary>Read a list of keys from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyRetryable db, Slice[] keys, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.ReadAsync((tr) => tr.GetValuesAsync(keys), ct);
		}

		/// <summary>Read a sequence of keys from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyRetryable db, IEnumerable<Slice> keys, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.ReadAsync((tr) => tr.GetValuesAsync(keys), ct);
		}

		/// <summary>Resolve a single key selector from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<Slice> GetKeyAsync(this IFdbReadOnlyRetryable db, KeySelector keySelector, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.ReadAsync((tr) => tr.GetKeyAsync(keySelector), ct);
		}

		/// <summary>Resolve a list of key selectors from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyRetryable db, KeySelector[] keySelectors, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(keySelectors);
			return db.ReadAsync((tr) => tr.GetKeysAsync(keySelectors), ct);
		}

		/// <summary>Resolve a sequence of key selectors from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyRetryable db, IEnumerable<KeySelector> keySelectors, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(keySelectors);
			return db.ReadAsync((tr) => tr.GetKeysAsync(keySelectors), ct);
		}

		/// <summary>Read a single page of a range query from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyRetryable db, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options, int iteration, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.ReadAsync((tr) => tr.GetRangeAsync(beginInclusive, endExclusive, options, iteration), ct);
		}

		/// <summary>Set the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task SetAsync(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Set(key, value), ct);
		}

		/// <summary>Set the values of a sequence of keys in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task SetValuesAsync(this IFdbRetryable db, IEnumerable<KeyValuePair<Slice, Slice>> items, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) =>
			{
				foreach (var kv in items)
				{
					tr.Set(kv.Key, kv.Value);
				}
			}, ct);
		}

		/// <summary>Set the values of a sequence of keys in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task SetValuesAsync(this IFdbRetryable db, IEnumerable<(Slice Key, Slice Value)> items, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) =>
			{
				foreach (var kv in items)
				{
					tr.Set(kv.Key, kv.Value);
				}
			}, ct);
		}

		/// <summary>Clear a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task ClearAsync(this IFdbRetryable db, Slice key, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Clear(key), ct);
		}

		/// <summary>Clear a single range in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task ClearRangeAsync(this IFdbRetryable db, Slice beginKeyInclusive, Slice endKeyExclusive, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.ClearRange(beginKeyInclusive, endKeyExclusive), ct);
		}

		/// <summary>Clear a single range in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task ClearRangeAsync(this IFdbRetryable db, KeyRange range, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.ClearRange(range), ct);
		}

		/// <summary>Atomically add to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicAdd(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Add), ct);
		}

		/// <summary>Atomically compare and optionally clear the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicCompareAndClear(this IFdbRetryable db, Slice key, Slice comparand, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, comparand, FdbMutationType.CompareAndClear), ct);
		}

		/// <summary>Atomically perform a bitwise AND to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicBitAnd(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitAnd), ct);
		}

		/// <summary>Atomically perform a bitwise OR to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicBitOr(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitOr), ct);
		}

		/// <summary>Atomically perform a bitwise XOR to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicBitXor(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitXor), ct);
		}

		/// <summary>Atomically update a value if it is larger than the value in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicMax(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Max), ct);
		}

		/// <summary>Atomically update a value if it is smaller than the value in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[Obsolete("Call this method on a transaction inside a retry-loop")]
		public static Task AtomicMin(this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db);
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Min), ct);
		}

		#endregion

		#region Back Compat

		/// <summary>Returns the protocol version reported by the coordinator this client is connected to.</summary>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that returns the current protocol version</returns>
		/// <remarks>This will never complete if the remote server is running a protocol from FDB 5.0 or older.</remarks>
		public static Task<FdbProtocolVersion> GetServerProtocolVersionAsync(this IFdbDatabase db, CancellationToken ct)
			=> db.GetServerProtocolVersionAsync(FdbProtocolVersion.None, ct);

		/// <summary>Return the protocol version reported by the coordinator the client is connected to.</summary>
		[Obsolete("Use GetServerProtocolVersionAsync() instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static async Task<ulong> GetServerProtocolAsync(this IFdbDatabase db, CancellationToken ct)
		{
			var pv = await db.GetServerProtocolVersionAsync(FdbProtocolVersion.None, ct).ConfigureAwait(false);
			// the original implementation returned the version without the flags
			return (ulong) pv.Version;
		}

		#endregion

	}

}
