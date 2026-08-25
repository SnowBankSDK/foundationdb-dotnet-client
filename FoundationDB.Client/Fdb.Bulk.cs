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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable MemberHidesStaticFromOuterClass

namespace FoundationDB.Client
{
	public static partial class Fdb
	{
		/// <summary>Helper class for bulk operations</summary>
		[PublicAPI]
		public static class Bulk
		{

			#region Writing...

			/// <summary>Options bag for bulk write operations</summary>
			public sealed class WriteOptions
			{

				/// <summary>Default options</summary>
				public WriteOptions()
				{
					this.MaxConcurrentTransactions = 1;
				}

				/// <summary>Maximum number of concurrent transactions to use.</summary>
				/// <remarks>The default value is 1.</remarks>
				public int MaxConcurrentTransactions { get; set; }

				/// <summary>Maximum number of items per batch</summary>
				/// <remarks>If null, the default value should be used.</remarks>
				public int? BatchCount { get; set; }

				/// <summary>Maximum size (in bytes) per batch</summary>
				/// <remarks>If null, the default value should be used.</remarks>
				public int? BatchSize { get; set; }

				/// <summary>Used to report progress during the operation</summary>
				/// <remarks>Since progress notification is async, this instance could be called even after the bulk operation has completed!</remarks>
				public IProgress<long>? Progress { get; set; }
			}

			const int DEFAULT_WRITE_BATCH_COUNT = 2 * 1024;
			const int DEFAULT_WRITE_BATCH_SIZE = 256 * 1024; //REVIEW: 256KB seems reasonable, but maybe we could also try with multiple transactions in // ?

			#region Bulk Write...

			// Use Case: MEMORY-IN => DATABASE-OUT

			// Bulk writing consists of inserting a lot of precomputed keys into the database, as fast as possible.
			// => the latency will come exclusively from committing the transaction to the database (i.e: network delay, disk delay)

			/// <summary>Writes a potentially large sequence of key/value pairs into the database, by using as many transactions as necessary, and automatically scaling the size of each batch.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="data">Sequence of key/value pairs</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Total number of values inserted in the database</returns>
			/// <remarks>In case of a non-retry-able error, some of the keys may remain in the database. Other transactions running at the same time may observe only a fraction of the keys until the operation completes.</remarks>
			public static Task<long> WriteAsync(IFdbDatabase db, IEnumerable<KeyValuePair<Slice, Slice>> data, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(data);

				ct.ThrowIfCancellationRequested();

				return RunWriteOperationAsync(
					db,
					data.Select(x => (x.Key, x.Value)),
					new WriteOptions(),
					ct
				);
			}

			/// <summary>Writes a potentially large sequence of key/value pairs into the database, by using as many transactions as necessary, and automatically scaling the size of each batch.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="data">Sequence of key/value pairs</param>
			/// <param name="options">Custom options used to configure the behaviour of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Total number of values inserted in the database</returns>
			/// <remarks>In case of a non retry-able error, some of the keys may remain in the database. Other transactions running at the same time may observe only a fraction of the keys until the operation completes.</remarks>
			public static Task<long> WriteAsync(IFdbDatabase db, IEnumerable<KeyValuePair<Slice, Slice>> data, WriteOptions? options, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(data);

				ct.ThrowIfCancellationRequested();

				return RunWriteOperationAsync(
					db,
					data.Select(x => (x.Key, x.Value)),
					options ?? new WriteOptions(),
					ct
				);
			}

			/// <summary>Writes a potentially large sequence of key/value pairs into the database, by using as many transactions as necessary, and automatically scaling the size of each batch.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="data">Sequence of key/value pairs</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Total number of values inserted in the database</returns>
			/// <remarks>In case of a non retry-able error, some of the keys may remain in the database. Other transactions running at the same time may observe only a fraction of the keys until the operation completes.</remarks>
			public static Task<long> WriteAsync(IFdbDatabase db, IEnumerable<(Slice Key, Slice Value)> data, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(data);

				ct.ThrowIfCancellationRequested();

				return RunWriteOperationAsync(
					db,
					data,
					new WriteOptions(),
					ct
				);
			}

			/// <summary>Writes a potentially large sequence of key/value pairs into the database, by using as many transactions as necessary, and automatically scaling the size of each batch.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="data">Sequence of key/value pairs</param>
			/// <param name="options">Custom options used to configure the behaviour of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Total number of values inserted in the database</returns>
			/// <remarks>In case of a non retry-able error, some of the keys may remain in the database. Other transactions running at the same time may observe only a fraction of the keys until the operation completes.</remarks>
			public static Task<long> WriteAsync(IFdbDatabase db, IEnumerable<(Slice Key, Slice Value)> data, WriteOptions? options, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(data);

				ct.ThrowIfCancellationRequested();

				return RunWriteOperationAsync(
					db,
					data,
					options ?? new WriteOptions(),
					ct
				);
			}

			internal static async Task<long> RunWriteOperationAsync(IFdbDatabase db, IEnumerable<(Slice Key, Slice Value)> data, WriteOptions options, CancellationToken ct)
			{
				Contract.Debug.Requires(db != null && data != null && options != null);

				ct.ThrowIfCancellationRequested();

				// We will batch keys into chunks (bounding by count and bytes), then attempt to insert that batch in the database.
				// Each transaction should try to never exceed ~1MB of size

				int maxBatchCount = options.BatchCount ?? DEFAULT_WRITE_BATCH_COUNT;
				int maxBatchSize = options.BatchSize ?? DEFAULT_WRITE_BATCH_SIZE;
				var progress = options.Progress;

				if (options.MaxConcurrentTransactions > 1)
				{
					//TODO: implement concurrent transactions when writing !
					throw new NotImplementedException("Multiple concurrent transactions are not yet supported");
				}

				var chunk = new List<(Slice Key, Slice Value)>();

				long items = 0;
				using (var iterator = data.GetEnumerator())
				{
					progress?.Report(0);

					while (!ct.IsCancellationRequested)
					{
						chunk.Clear();
						int bytes = 0;

						while (iterator.MoveNext())
						{
							var pair = iterator.Current;
							chunk.Add(pair);
							bytes += pair.Key.Count + pair.Value.Count;

							if (chunk.Count >= maxBatchCount || bytes >= maxBatchSize)
							{ // chunk is big enough
								break;
							}
						}

						if (chunk.Count == 0)
						{ // no more data, we are done
							break;
						}

						await db.WriteAsync((tr) =>
						{
							foreach (var pair in chunk)
							{
								tr.Set(pair.Key, pair.Value);
							}
						}, ct).ConfigureAwait(false);

						items += chunk.Count;

						progress?.Report(items);
					}
				}

				ct.ThrowIfCancellationRequested();

				return items;
			}

			#endregion

			#region Bulk Insert...

			// Use Case: CPU-IN => DATABASE-OUT

			// Bulk inserting consists of inserting a lot of documents that must be serialized to slices, into the database, as fast as possible.
			// => we expect the serialization of the data to be somewhat fast (ie: JSON serialization, Tuple encoding, ....)
			// => the latency will come mostly from committing the transaction to the database (i.e: network delay, disk delay)
			// => with multiple concurrent transaction, the commit latency could be used to serialize the next batch of data

			#region Single...

			// Items in the source are processed one by one, which may limit the throughput per transaction (by adding the latency of all the items)
			// This is intended for Layers who can only process items one by one, and when there is no easy way to parallelize the work.

			/// <summary>Inserts a potentially large sequence of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non-retryable error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Action<T, IFdbTransaction> handler, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunInsertOperationAsync<T>(
					db,
					source,
					handler,
					new WriteOptions(),
					ct
				);
			}

			/// <summary>Inserts a potentially large sequence of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="options">Custom options used to configure the behaviour of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non-retryable error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Action<T, IFdbTransaction> handler, WriteOptions? options, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunInsertOperationAsync<T>(
					db,
					source,
					handler,
					options ?? new WriteOptions(),
					ct
				);
			}

			/// <summary>Inserts a potentially large sequence of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non-retryable error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Func<T, IFdbTransaction, Task> handler, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunInsertOperationAsync<T>(
					db,
					source,
					handler,
					new WriteOptions(),
					ct
				);
			}

			/// <summary>Inserts a potentially large sequence of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="options">Custom options used to configure the behaviour of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non-retryable error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Func<T, IFdbTransaction, Task> handler, WriteOptions? options, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunInsertOperationAsync<T>(
					db,
					source,
					handler,
					options ?? new WriteOptions(),
					ct
				);
			}

			/// <summary>Runs a long duration bulk insertion, where items are processed one by one</summary>
			internal static async Task<long> RunInsertOperationAsync<TSource>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Delegate body,
				WriteOptions options,
				CancellationToken ct
			)
			{
				Contract.Debug.Requires(db != null && source != null && body != null && options != null);

				int batchCount = options.BatchCount ?? DEFAULT_WRITE_BATCH_COUNT;
				int sizeThreshold = options.BatchSize ?? DEFAULT_WRITE_BATCH_SIZE;

				if (batchCount <= 0) throw new InvalidOperationException("Batch count must be a positive integer.");
				if (sizeThreshold <= 0) throw new InvalidOperationException("Batch size must be a positive integer.");
				if (options.MaxConcurrentTransactions > 1)
				{
					//TODO: implement concurrent transactions when writing !
					throw new NotImplementedException("Multiple concurrent transactions are not yet supported");
				}

				var bodyAsync = body as Func<TSource, IFdbTransaction, Task>;
				var bodyBlocking = body as Action<TSource, IFdbTransaction>;
				if (bodyAsync == null && bodyBlocking == null)
				{
					throw new ArgumentException($"Unsupported delegate type {body.GetType().FullName} for body", nameof(body));
				}

				var batch = new List<TSource>(batchCount);
				long itemCount = 0;

				using (var trans = db.BeginTransaction(ct))
				{
					long? timerStart = db.Time.GetTimestamp(); // null once stopped, mirroring Stopwatch.Reset() (stops and never restarts)

					async Task CommitBatch()
					{
#if FULL_DEBUG
						Trace.WriteLine("> commit called with " + batch.Count.ToString("N0") + " items and " + trans.Size.ToString("N0") + " bytes");
#endif

						// if transaction Size is bigger than Fdb.MaxTransactionSize (10MB) then commit will fail, but we will retry with a smaller batch anyway

						try
						{
							await trans.CommitAsync().ConfigureAwait(false);
						}
						catch (FdbException error)
						{ // we failed to commit this batch, we need to retry...

#if FULL_DEBUG
							Trace.WriteLine("> commit failed : " + error);
#endif

							if (error.Code == FdbError.TransactionTooLarge)
							{
								if (batch.Count == 1)
								{
									throw new InvalidOperationException("Cannot insert one the item of the source collection because it exceeds the maximum size allowed per transaction");
								}
							}
							else
							{
								await trans.OnErrorAsync(error.Code).ConfigureAwait(false);
							}

							int half = checked(batch.Count + 1) >> 1;
							// retry the first half
							await RetryChunk(trans, batch, 0, half, bodyAsync, bodyBlocking).ConfigureAwait(false);
							// retry the second half
							await RetryChunk(trans, batch, half, batch.Count - half, bodyAsync, bodyBlocking).ConfigureAwait(false);
						}

						// success!
						batch.Clear();
						trans.Reset();
						timerStart = null;
					}

					foreach (var item in source)
					{
						if (ct.IsCancellationRequested) break;

						// store it (in case we need to retry)
						batch.Add(item);
						++itemCount;

						if (bodyAsync != null)
						{
							await bodyAsync(item, trans).ConfigureAwait(false);
						}
						else
						{
							Contract.Debug.Assert(bodyBlocking != null);
							bodyBlocking(item, trans);
						}

						// commit the batch if ...
						if (trans.Size >= sizeThreshold      // transaction is starting to get big...
						 || batch.Count >= batchCount        // too many items would need to be retried...
						 || (timerStart is long ts && db.Time.GetElapsedTime(ts).TotalSeconds >= 4)  // it's getting late...
						)
						{
							await CommitBatch().ConfigureAwait(false);
							Contract.Debug.Assert(batch.Count == 0);
						}
					}

					ct.ThrowIfCancellationRequested();

					// handle the last (or only) batch
					if (batch.Count > 0)
					{
						await CommitBatch().ConfigureAwait(false);
					}
				}

				return itemCount;
			}

			/// <summary>Retry commiting a segment of a chunk, splitting it in sub-segments as needed</summary>
			private static async Task RetryChunk<TSource>(IFdbTransaction trans, List<TSource> chunk, int offset, int count, Func<TSource, IFdbTransaction, Task>? bodyAsync, Action<TSource, IFdbTransaction>? bodyBlocking)
			{
				Contract.Debug.Requires(trans != null && chunk != null && offset >= 0 && count >= 0 && (bodyAsync != null || bodyBlocking != null));

				// Steps:
				// - reset transaction
				// - replay the items in the batch segment
				// - try to commit
				// - it still fails, split segment into and retry each half

				if (count <= 0) return;

			localRetry:

#if FULL_DEBUG
				Trace.WriteLine("> replaying chunk @" + offset + ", " + count);
#endif

				trans.Cancellation.ThrowIfCancellationRequested();

				trans.Reset();

				if (bodyAsync != null)
				{
					for (int i = 0; i < count; i++)
					{
						await bodyAsync(chunk[offset + i], trans).ConfigureAwait(false);
					}
				}
				else
				{
					Contract.Debug.Assert(bodyBlocking != null);
					for (int i = 0; i < count; i++)
					{
						bodyBlocking(chunk[offset + i], trans);
					}
				}

				try
				{
#if FULL_DEBUG
					Trace.WriteLine("> retrying chunk...");
#endif
					await trans.CommitAsync().ConfigureAwait(false);
					return;
				}
				catch (FdbException error)
				{
#if FULL_DEBUG
					Trace.WriteLine("> oh noes " + error);
#endif

					// it failed again
					if (error.Code == FdbError.TransactionTooLarge)
					{
						// retrying won't help if a single item is too big
						if (count == 1)
						{
							throw new InvalidOperationException("Cannot insert one the item of the source collection because it exceeds the maximum size allowed per transaction");
						}
					}
					else
					{
						await trans.OnErrorAsync(error.Code).ConfigureAwait(false);

					}
				}

				//TODO: for the moment we do a recursive call, which could potentially cause a stack overflow in addition to being ugly.
				// => rewrite this to use a stack of work to do, without the need to recurse!

				if (count == 1)
				{ // protect against stackoverflow if we retry on a single item
					goto localRetry;
				}

				int half = checked(count + 1) >> 1;
				await RetryChunk(trans, chunk, offset, half, bodyAsync, bodyBlocking).ConfigureAwait(false);
				await RetryChunk(trans, chunk, offset + half, count - half, bodyAsync, bodyBlocking).ConfigureAwait(false);
			}

			#endregion

			#region Batch...

			// Items in the source are grouped and processed in batches, which allows the caller to process items concurrently within a batch, and maximize the throughput per transaction.
			// This is intended for Layers that have optimized ways to process multiple items at a time - something like MultiLoad(...) or InsertMultiple(..), or when the caller wants to handle the problem by himself.

			/// <summary>Inserts a potentially large number of batches of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non-retryable error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertBatchedAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Action<T[], IFdbTransaction> handler, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunBatchedInsertOperationAsync<T>(
					db,
					source,
					handler,
					new WriteOptions(),
					ct
				);
			}

			/// <summary>Inserts a potentially large number of batches of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="options">Custom options used to configure the behaviour of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non retry-able error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertBatchedAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Action<T[], IFdbTransaction> handler, WriteOptions? options, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunBatchedInsertOperationAsync<T>(
					db,
					source,
					handler,
					options ?? new WriteOptions(),
					ct
				);
			}

			/// <summary>Inserts a potentially large number of batches of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non retry-able error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertBatchedAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Func<T[], IFdbTransaction, Task> handler, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunBatchedInsertOperationAsync<T>(
					db,
					source,
					handler,
					new WriteOptions(),
					ct
				);
			}

			/// <summary>Inserts a potentially large number of batches of items into the database, by using as many transactions as necessary, and automatically retrying if needed.</summary>
			/// <typeparam name="T">Type of the items in the <paramref name="source"/> sequence</typeparam>
			/// <param name="db">Database used for the operation</param>
			/// <param name="source">Sequence of items to be processed</param>
			/// <param name="handler">Lambda called at least once for each item in the source. The method may not have any side effect outside the passed transaction.</param>
			/// <param name="options">Custom options used to configure the behaviour of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of items that have been inserted</returns>
			/// <remarks>In case of a non retry-able error, some of the items may remain in the database. Other transactions running at the same time may observe only a fraction of the items until the operation completes.</remarks>
			public static Task<long> InsertBatchedAsync<T>(IFdbDatabase db, IEnumerable<T> source, [InstantHandle] Func<T[], IFdbTransaction, Task> handler, WriteOptions? options, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(handler);

				ct.ThrowIfCancellationRequested();

				return RunBatchedInsertOperationAsync<T>(
					db,
					source,
					handler,
					options ?? new WriteOptions(),
					ct
				);
			}

			/// <summary>Runs a long duration bulk insertion, where items are processed in batch</summary>
			internal static async Task<long> RunBatchedInsertOperationAsync<TSource>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Delegate body,
				WriteOptions options,
				CancellationToken ct
			)
			{
				Contract.Debug.Requires(db != null && source != null && body != null && options != null);

				int batchCount = options.BatchCount ?? DEFAULT_WRITE_BATCH_COUNT;
				int sizeThreshold = options.BatchSize ?? DEFAULT_WRITE_BATCH_SIZE;

				if (batchCount <= 0) throw new InvalidOperationException("Batch count must be a positive integer.");
				if (sizeThreshold <= 0) throw new InvalidOperationException("Batch size must be a positive integer.");
				if (options.MaxConcurrentTransactions > 1)
				{
					//TODO: implement concurrent transactions when writing !
					throw new NotImplementedException("Multiple concurrent transactions are not yet supported");
				}

				var bodyAsync = body as Func<TSource[], IFdbTransaction, Task>;
				var bodyBlocking = body as Action<TSource[], IFdbTransaction>;
				if (bodyAsync == null && bodyBlocking == null)
				{
					throw new ArgumentException($"Unsupported delegate type {body.GetType().FullName} for body", nameof(body));
				}

				var chunk = new List<TSource>(batchCount); // holds all the items processed in the current transaction cycle
				long itemCount = 0; // total number of items processed

				using (var trans = db.BeginTransaction(ct))
				{
					long? timerStart = db.Time.GetTimestamp(); // null once stopped, mirroring Stopwatch.Reset() (stops and never restarts)

					async Task CommitBatch()
					{
#if FULL_DEBUG
						Trace.WriteLine("> commit called with " + batch.Count.ToString("N0") + " items and " + trans.Size.ToString("N0") + " bytes");
#endif

						// if transaction Size is bigger than Fdb.MaxTransactionSize (10MB) then commit will fail, but we will retry with a smaller batch anyway

						try
						{
							await trans.CommitAsync().ConfigureAwait(false);

							// recompute the batch count, so that we do about 4 batch per transaction
							// we observed that 'chunk.Count' items produced 'trans.Size' bytes.
							// we would like to have 'sizeThreshold' bytes, and about 8 batch per transaction
							batchCount = (int) (((long) chunk.Count * sizeThreshold) / (trans.Size * 8L));
							//Console.WriteLine("New batch size is {0}", batchCount);
						}
						catch (FdbException error)
						{ // we failed to commit this batch, we need to retry...

#if FULL_DEBUG
							Trace.WriteLine("> commit failed : " + error);
#endif

							if (error.Code == FdbError.TransactionTooLarge)
							{
								if (chunk.Count == 1) throw new InvalidOperationException("Cannot insert one the item of the source collection because it exceeds the maximum size allowed per transaction");
								// reduce the size of future batches
								if (batchCount > 1) batchCount <<= 1;
							}
							else
							{
								await trans.OnErrorAsync(error.Code).ConfigureAwait(false);
							}

							int half = checked(chunk.Count + 1) >> 1;
							// retry the first half
							await RetryChunk(trans, chunk, 0, half, bodyAsync, bodyBlocking).ConfigureAwait(false);
							// retry the second half
							await RetryChunk(trans, chunk, half, chunk.Count - half, bodyAsync, bodyBlocking).ConfigureAwait(false);
						}

						// success!
						chunk.Clear();
						trans.Reset();
						timerStart = null;
					}

					int offset = 0; // offset of the current batch in the chunk

					foreach (var item in source)
					{
						if (ct.IsCancellationRequested) break;

						// store it (in case we need to retry)
						chunk.Add(item);
						++itemCount;

						if (chunk.Count - offset >= batchCount
						 || trans.Size >= sizeThreshold)
						{ // we have enough items to fill a batch

							var batch = new TSource[chunk.Count - offset];
							chunk.CopyTo(offset, batch, 0, batch.Length);
							if (bodyAsync != null)
							{
								await bodyAsync(batch, trans).ConfigureAwait(false);
							}
							else
							{
								Contract.Debug.Assert(bodyBlocking != null);
								bodyBlocking(batch, trans);
							}
							offset += batch.Length;

							// commit the batch if ...
							if (trans.Size >= sizeThreshold         // transaction is starting to get big...
							 || (timerStart is long ts && db.Time.GetElapsedTime(ts).TotalSeconds >= 4))    // it's getting late...
							{
								await CommitBatch().ConfigureAwait(false);

								offset = 0;
							}
						}
					}

					ct.ThrowIfCancellationRequested();

					// handle the last (or only) batch
					if (chunk.Count > 0)
					{
						var batch = new TSource[chunk.Count - offset];
						chunk.CopyTo(offset, batch, 0, batch.Length);
						if (bodyAsync != null)
						{
							await bodyAsync(batch, trans).ConfigureAwait(false);
						}
						else
						{
							Contract.Debug.Assert(bodyBlocking != null);
							bodyBlocking(batch, trans);
						}

						await CommitBatch().ConfigureAwait(false);
					}
				}

				return itemCount;
			}

			/// <summary>Retry commiting a segment of a chunk, splitting it in sub-segments as needed</summary>
			private static async Task RetryChunk<TSource>(IFdbTransaction trans, List<TSource> chunk, int offset, int count, Func<TSource[], IFdbTransaction, Task>? bodyAsync, Action<TSource[], IFdbTransaction>? bodyBlocking)
			{
				Contract.Debug.Requires(trans != null && chunk != null && offset >= 0 && count >= 0 && (bodyAsync != null || bodyBlocking != null));

				// Steps:
				// - reset transaction
				// - replay the items in the batch segment
				// - try to commit
				// - it still fails, split segment into and retry each half

				if (count <= 0) return;

			localRetry:

#if FULL_DEBUG
				Trace.WriteLine("> replaying chunk @" + offset + ", " + count);
#endif

				trans.Cancellation.ThrowIfCancellationRequested();

				trans.Reset();

				// get the slice of the original batch to retry
				var items = new TSource[count];
				chunk.CopyTo(offset, items, 0, count);

				if (bodyAsync != null)
				{
					await bodyAsync(items, trans).ConfigureAwait(false);
				}
				else
				{
					Contract.Debug.Assert(bodyBlocking != null);
					bodyBlocking(items, trans);
				}

				try
				{
#if FULL_DEBUG
					Trace.WriteLine("> retrying chunk...");
#endif
					await trans.CommitAsync().ConfigureAwait(false);
					return;
				}
				catch (FdbException error)
				{
#if FULL_DEBUG
					Trace.WriteLine("> oh noes " + error);
#endif

					// it failed again
					if (error.Code == FdbError.TransactionTooLarge)
					{
						// retrying won't help if a single item is too big
						if (count == 1)
						{
							throw new InvalidOperationException("Cannot insert one the item of the source collection because it exceeds the maximum size allowed per transaction");
						}
					}
					else
					{
						await trans.OnErrorAsync(error.Code).ConfigureAwait(false);
					}
				}

				//TODO: for the moment we do a recursive call, which could potentially cause a stack overflow in addition to being ugly.
				// => rewrite this to use a stack of work to do, without the need to recurse!

				if (count == 1)
				{ // protect against stackoverflow if we retry on a single item
					goto localRetry;
				}

				int half = checked(count + 1) >> 1;
				await RetryChunk(trans, chunk, offset, half, bodyAsync, bodyBlocking).ConfigureAwait(false);
				await RetryChunk(trans, chunk, offset + half, count - half, bodyAsync, bodyBlocking).ConfigureAwait(false);
			}

			#endregion

			#endregion

			#endregion

			#region Batching...

			//REVIEW: what's a good value for these ?
			private const int DefaultInitialBatchSize = 100; 
			private const int DefaultMinimumBatchSize = 4;
			private const int DefaultMaximumBatchSize = 10000;

			/// <summary>Context for a long-running batched read operation</summary>
			public sealed class BatchOperationContext
			{
				/// <summary>Transaction corresponding to the current generation</summary>
				public IFdbReadOnlyTransaction Transaction { get; }

				/// <summary>Offset of the current batch from the start of the source sequence</summary>
				public long Position { get; internal set; }

				/// <summary>Generation number of the transaction (starting from 0 for the initial transaction)</summary>
				public int Generation { get; internal set; }

				/// <summary>Current batch size target</summary>
				public int Step { get; internal set; }

				/// <summary>Time source used to measure the elapsed timers below</summary>
				internal TimeProvider Time { get; }

				/// <summary>Timestamp captured at the start of the bulk operation</summary>
				internal long TotalStart { get; }

				/// <summary>Timestamp captured at the start of the current transaction window</summary>
				internal long GenerationStart { get; set; }

				/// <summary>Cooldown timer used for scaling up and down the step size</summary>
				public int Cooldown { get; internal set; }

				/// <summary>Total elapsed time since the start of this bulk operation</summary>
				public TimeSpan ElapsedTotal => this.Time.GetElapsedTime(this.TotalStart);

				/// <summary>Elapsed time since the start of the current transaction window</summary>
				public TimeSpan ElapsedGeneration => this.Time.GetElapsedTime(this.GenerationStart);

				/// <summary>Returns true if all values processed up to this point used the same transaction, or false if more than one transaction was used.</summary>
				public bool IsTransactional => this.Generation == 0;

				/// <summary>Returns true if at least one un-retryable exception happened during the process of one batch</summary>
				public bool Failed { get; internal set; }

				/// <summary>If called, will stop immediately after processing this batch, even if the source sequence contains more elements</summary>
				public void Stop()
				{
					this.Abort = true;
				}

				/// <summary>Gets or sets the abort flag</summary>
				public bool Abort { get; set; }

				internal BatchOperationContext(IFdbReadOnlyTransaction trans, int batchSize, TimeProvider time)
				{
					Contract.Debug.Requires(trans != null && batchSize > 0 && time != null);
					this.Transaction = trans;
					this.Step = batchSize;
					this.Time = time;
					this.TotalStart = time.GetTimestamp();
					this.GenerationStart = time.GetTimestamp();
				}

			}

			#region ForEach...

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TLocal">Type of the local immutable state that is flowed accross all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="localFinally">Lambda function that will be called after the last batch, and will be passed the last known state.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="ct"/> is triggered</returns>
			public static Task ForEachAsync<TSource, TLocal>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				[InstantHandle] Func<TLocal> localInit,
				[InstantHandle] Func<TSource[], BatchOperationContext, TLocal, Task<TLocal>> body,
				[InstantHandle] Action<TLocal> localFinally,
				CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(localInit);
				Contract.NotNull(body);
				Contract.NotNull(localFinally);

				return RunBatchedReadOperationAsync<TSource, TLocal, object>(db, source, localInit, body, localFinally, DefaultInitialBatchSize, ct);
			}

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TLocal">Type of the local immutable state that is flowed across all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retry-able lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="localFinally">Lambda function that will be called after the last batch, and will be passed the last known state.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non retry-able error occurs, or <paramref name="ct"/> is triggered</returns>
			public static Task ForEachAsync<TSource, TLocal>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				[InstantHandle] Func<TLocal> localInit,
				[InstantHandle] Func<TSource[], BatchOperationContext, TLocal, TLocal> body,
				[InstantHandle] Action<TLocal> localFinally,
				CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(localInit);
				Contract.NotNull(body);
				Contract.NotNull(localFinally);

				//REVIEW: what is the point if the body is not async ?
				// > either is can read and generate past_version errors then it needs to be async
				// > either it's not async, then it could only Write/Clear, and in which case we need a writable transaction ... ? (and who will commit and when ??)
				// It could maybe make sense if the source was an IAsyncEnumerable<T> because you could not use Parallel.ForEach(...) for that

				return RunBatchedReadOperationAsync<TSource, TLocal, object>(db, source, localInit, body, localFinally, DefaultInitialBatchSize, ct);
			}

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="body">Retry-able lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non retry-able error occurs, or <paramref name="ct"/> is triggered</returns>
			public static Task ForEachAsync<TSource>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				[InstantHandle] Func<TSource[], BatchOperationContext, Task> body,
				CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(body);

				return RunBatchedReadOperationAsync<TSource, object, object>(db, source, null, body, null, DefaultInitialBatchSize, ct);
			}

			/// <summary>Execute a potentially long read-only operation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="body">Retry-able lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non retry-able error occurs, or <paramref name="ct"/> is triggered</returns>
			[Obsolete("EXPERIMENTAL: do not use yet!")]
			public static Task ForEachAsync<TSource>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				[InstantHandle] Action<TSource[], BatchOperationContext> body,
				CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(body);

				//REVIEW: what is the point if the body is not async ?
				// > either is can read and generate past_version errors then it needs to be async
				// > either it's not async, then it could only Write/Clear, and in which case we need a writable transaction ... ? (and who will commit and when ??)
				// It could maybe make sense if the source was an IAsyncEnumerable<T> because you could not use Parallel.ForEach(...) for that

				return RunBatchedReadOperationAsync<TSource, object, object>(db, source, null, body, null, DefaultInitialBatchSize, ct);
			}

			#endregion

			#region Aggregate...

			private static class AggregateLambda<TAggregate>
			{
				public static readonly Func<TAggregate, TAggregate> Identity = (x) => x;
			}

			/// <summary>Execute a potentially long aggregation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TAggregate">Type of the local immutable aggregate that is flowed across all batch operations</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="localInit">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retry-able lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non retry-able error occurs, or <paramref name="ct"/> is triggered</returns>
			public static Task<TAggregate> AggregateAsync<TSource, TAggregate>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				[InstantHandle] Func<TAggregate> localInit,
				[InstantHandle] Func<TSource[], BatchOperationContext, TAggregate, Task<TAggregate>> body,
				CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(localInit);
				Contract.NotNull(body);

				return RunBatchedReadOperationAsync<TSource, TAggregate, TAggregate>(db, source, localInit, body, AggregateLambda<TAggregate>.Identity, DefaultInitialBatchSize, ct);
			}

			/// <summary>Execute a potentially long aggregation on batches of elements from a source sequence, using as many transactions as necessary, and automatically scaling the size of each batch to maximize the throughput.</summary>
			/// <typeparam name="TSource">Type of elements in the source sequence</typeparam>
			/// <typeparam name="TAggregate">Type of the local immutable aggregate that is flowed across all batch operations</typeparam>
			/// <typeparam name="TResult">Type of the result of the operation</typeparam>
			/// <param name="db">Source database</param>
			/// <param name="source">Source sequence that will be split into batch. The size of each batch will scale up and down automatically depending on the speed of execution</param>
			/// <param name="init">Lambda function that is called once, and returns the initial state that will be passed to the first batch</param>
			/// <param name="body">Retryable lambda function that receives a batch of elements from the source sequence, the current context, and the previous state. If the transaction expires while this lambda function is running, it will be automatically retried with a new transaction, and a smaller batch.</param>
			/// <param name="transform">Lambda function called with the aggregate returned by the last batch, and which will compute the final result of the operation</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Task that completes when all the elements of <paramref name="source"/> have been processed, a non-retryable error occurs, or <paramref name="ct"/> is triggered</returns>
			public static Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				[InstantHandle] Func<TAggregate> init,
				[InstantHandle] Func<TSource[], BatchOperationContext, TAggregate, Task<TAggregate>> body,
				[InstantHandle] Func<TAggregate, TResult> transform,
				CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(source);
				Contract.NotNull(init);
				Contract.NotNull(body);
				Contract.NotNull(transform);

				return RunBatchedReadOperationAsync<TSource, TAggregate, TResult>(db, source, init, body, transform, DefaultInitialBatchSize, ct);
			}

			#endregion

			/// <summary>Runs a long duration bulk read</summary>
			internal static async Task<TResult> RunBatchedReadOperationAsync<TSource, TLocal, TResult>(
				IFdbDatabase db,
				IEnumerable<TSource> source,
				Func<TLocal>? localInit,
				Delegate body,
				Delegate? localFinally,
				int initialBatchSize,
				CancellationToken ct
			)
			{
				Contract.Debug.Requires(db != null && source != null && initialBatchSize > 0 && body != null);

				var bodyAsyncWithContextAndState = body as Func<TSource[], BatchOperationContext, TLocal, Task<TLocal>>;
				var bodyWithContextAndState = body as Func<TSource[], BatchOperationContext, TLocal, TLocal>;
				var bodyAsyncWithContext = body as Func<TSource[], BatchOperationContext, Task>;
				var bodyWithContext = body as Action<TSource[], BatchOperationContext>;

				if (bodyAsyncWithContextAndState == null &&
					bodyAsyncWithContext == null &&
					bodyWithContextAndState == null &&
					bodyWithContext == null)
				{
					throw new ArgumentException($"Unsupported delegate type {body.GetType().FullName} for body", nameof(body));
				}

				var localFinallyVoid = localFinally as Action<TLocal>;
				var localFinallyWithResult = localFinally as Func<TLocal, TResult>;
				if (localFinally != null && 
					localFinallyVoid == null &&
					localFinallyWithResult == null)
				{
					throw new ArgumentException($"Unsupported delegate type {body.GetType().FullName} for local finally", nameof(localFinally));
				}

				int batchSize = initialBatchSize;
				var batch = new List<TSource>(batchSize);

				bool localInitialized = false;
				TLocal localValue = default!;
				TSource[]? items = null;
				TResult result = default!;

				using (var iterator = source.GetEnumerator())
				{
					using (var trans = db.BeginReadOnlyTransaction(ct))
					{
						var ctx = new BatchOperationContext(trans, batchSize, db.Time);

						try
						{
							if (localInit != null)
							{ // need to initialize the state
								//TODO: maybe defer only if there are things in the source sequence?
								localValue = localInit();
								localInitialized = true;
							}

							while (!ctx.Abort && !ct.IsCancellationRequested)
							{
								FillNextBatch<TSource>(iterator, batch, ctx.Step);

								if (batch.Count == 0)
								{
									break;
								}

								int offsetInCurrentBatch = 0;
								while (!ctx.Abort && !ct.IsCancellationRequested)
								{
									var r = Math.Min(ctx.Step, batch.Count - offsetInCurrentBatch);
									if (items == null || items.Length != r)
									{
										items = new TSource[r];
									}
									batch.CopyTo(offsetInCurrentBatch, items, 0, items.Length);

									FdbException? error = null;
									try
									{
										var swStart = db.Time.GetTimestamp();
										if (bodyAsyncWithContextAndState != null)
										{
											localValue = await bodyAsyncWithContextAndState(items, ctx, localValue).ConfigureAwait(false);
										}
										else if (bodyWithContextAndState != null)
										{
											localValue = bodyWithContextAndState(items, ctx, localValue);
										}
										else if (bodyAsyncWithContext != null)
										{
											await bodyAsyncWithContext(items, ctx).ConfigureAwait(false);
										}
										else
										{
											Contract.Debug.Assert(bodyWithContext != null);
											bodyWithContext(items, ctx);
										}
										var swElapsed = db.Time.GetElapsedTime(swStart);

										if (!ctx.Abort)
										{
											offsetInCurrentBatch += items.Length;
											if (offsetInCurrentBatch >= batch.Count)
											{
												// scale up the batch size if everything was super quick !
												if (ctx.Cooldown > 0) ctx.Cooldown--;
												if (ctx.Cooldown <= 0 && swElapsed.TotalSeconds < (5.0 - ctx.ElapsedGeneration.TotalSeconds) / 2)//REVIEW: magical number!
												{
													ctx.Step = Math.Min(ctx.Step * 2, DefaultMaximumBatchSize); //REVIEW: magical number!
													//REVIEW: magical number!
													ctx.Cooldown = 2;
												}
												break;
											}
										}
									}
									catch (Exception e)
									{
										error = e as FdbException;
										// if the callback uses task.Wait() or task.Result the exception may be wrapped in an AggregateException
										if (error == null && e is AggregateException aggEx)
										{
											error = aggEx.InnerException as FdbException;
										}
									}

									if (error != null)
									{
										if (error.Code == FdbError.TransactionTooOld)
										{ // this generation lasted too long, we need to start a new one and try again...
											trans.Reset();
											ctx.GenerationStart = ctx.Time.GetTimestamp();
											ctx.Generation++;

											// scale back batch size
											if (ctx.Step > DefaultMinimumBatchSize)
											{
												ctx.Step = Math.Max(ctx.Step >> 1, DefaultMinimumBatchSize);
												//REVIEW: magical number!
												ctx.Cooldown = 10;
											}
										}
										else
										{ // the error may be retry-able...
											await trans.OnErrorAsync(error.Code).ConfigureAwait(false);
											ctx.GenerationStart = ctx.Time.GetTimestamp();
											ctx.Generation++;
											//REVIEW: magical number!
											if (ctx.Cooldown < 2) ctx.Cooldown = 2;

										}
									}
								}
								ct.ThrowIfCancellationRequested();

								ctx.Position += offsetInCurrentBatch;
								batch.Clear();
							}
							ct.ThrowIfCancellationRequested();
						}
						catch(Exception)
						{
							ctx.Failed = true;
							throw;
						}
						finally
						{
							if (localFinally != null && localInitialized)
							{ // we need to clean up the state whatever happens
								if (localFinallyVoid != null)
								{
									localFinallyVoid(localValue);
								}
								else if (localFinallyWithResult != null)
								{
									result = localFinallyWithResult(localValue);
								}
							}
						}
					}
				}

				return result;
			}

			/// <summary>Tries to fill a batch with items from the source</summary>
			/// <typeparam name="T">Type of source items</typeparam>
			/// <param name="iterator">Source iterator</param>
			/// <param name="batch">Batch where the read items will be appended.</param>
			/// <param name="size">Maximum capacity of the batch</param>
			/// <returns>Number items added to the batch, or 0 if it was already full</returns>
			private static int FillNextBatch<T>(IEnumerator<T> iterator, List<T> batch, int size)
			{
				Contract.Debug.Requires(iterator != null && batch != null && size > 0);

				int count = 0;
				while (batch.Count < size && iterator.MoveNext())
				{
					batch.Add(iterator.Current);
					++count;
				}
				return count;
			}

			#endregion

			#region Import/Export...

			/// <summary>Export the content of a potentially large range of keys defined by a pair of begin and end keys.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="beginInclusive">Key defining the start range (included)</param>
			/// <param name="endExclusive">Key defining the end of the range (excluded)</param>
			/// <param name="handler">Lambda that will be called for each batch of data read from the database. The first argument is the array of ordered key/value pairs in the batch, taken from the same database snapshot. The second argument is the offset of the first item in the array, from the start of the range. The third argument is a token should be used by any async i/o performed by the lambda.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys exported</returns>
			/// <remarks>This method cannot guarantee that all data will be read from the same snapshot of the database, which means that writes committed while the export is running may be seen partially. Only the items inside a single batch are guaranteed to be from the same snapshot of the database.</remarks>
			public static Task<long> ExportAsync(IFdbDatabase db, Slice beginInclusive, Slice endExclusive, [InstantHandle] Func<KeyValuePair<Slice, Slice>[], long, CancellationToken, Task> handler, CancellationToken ct)
			{
				return ExportAsync(db, KeySelector.FirstGreaterOrEqual(beginInclusive), KeySelector.FirstGreaterOrEqual(endExclusive), handler, ct);
			}

			/// <summary>Export the content of a potentially large range of keys defined by a pair of begin and end keys.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Pair of keys defining the start range</param>
			/// <param name="handler">Lambda that will be called for each batch of data read from the database. The first argument is the array of ordered key/value pairs in the batch, taken from the same database snapshot. The second argument is the offset of the first item in the array, from the start of the range. The third argument is a token should be used by any async i/o performed by the lambda.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys exported</returns>
			/// <remarks>This method cannot guarantee that all data will be read from the same snapshot of the database, which means that writes committed while the export is running may be seen partially. Only the items inside a single batch are guaranteed to be from the same snapshot of the database.</remarks>
			public static Task<long> ExportAsync(IFdbDatabase db, KeyRange range, [InstantHandle] Func<KeyValuePair<Slice, Slice>[], long, CancellationToken, Task> handler, CancellationToken ct)
			{
				return ExportAsync(db, KeySelector.FirstGreaterOrEqual(range.Begin), KeySelector.FirstGreaterOrEqual(range.End), handler, ct);
			}

			/// <summary>Export the content of a potentially large range of keys defined by a pair of selectors.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="begin">Selector defining the start of the range (included)</param>
			/// <param name="end">Selector defining the end of the range (excluded)</param>
			/// <param name="handler">Lambda that will be called for each batch of data read from the database. The first argument is the array of ordered key/value pairs in the batch, taken from the same database snapshot. The second argument is the offset of the first item in the array, from the start of the range. The third argument is a token should be used by any async i/o performed by the lambda.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys exported</returns>
			/// <remarks>This method cannot guarantee that all data will be read from the same snapshot of the database, which means that writes committed while the export is running may be seen partially. Only the items inside a single batch are guaranteed to be from the same snapshot of the database.</remarks>
			public static async Task<long> ExportAsync(IFdbDatabase db, KeySelector begin, KeySelector end, [InstantHandle] Func<KeyValuePair<Slice, Slice>[], long, CancellationToken, Task> handler, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(handler);
				ct.ThrowIfCancellationRequested();

				// to maximize throughput, we want to read as much as possible per transaction, so that means that we should prefetch the next batch while the current batch is processing

				// If handler() is always faster than the prefetch(), the bottleneck is the database (or possibly the network).
				//	R: [ Read B1 ... | Read B2 ........ | Read B3 ..... ]
				//	W:               [ Process B1 ]-----[ Process B2 ]--[ Process B3]X

				// TODO: alternative method that we could use to almost double the throughput (second thread that exports backwards starting from the end)
				//	R: [ Read B1 ... | Read B2 ..... | Read B3 ..... | ...
				//	R:		| Read B10 ...... | Read B9 ..... | Read B8 ..... | ...
				//	W:               [ Process B1 | Process B10 | Process B2 | Process B9 | Process B3 | ...

				// If handler() is always slower than the prefetch(), the bottleneck is the local processing (or possibly local disk if writing to disk)
				//	R: [ Read B1 | Read B2 ]----------[ Read B3 ]-------[ Read B4 ]--------
				//	W:           [ Process B1 ....... | Process B2 .... | Process B3 .... | Process B4 ]

				// If handler() does some buffering, and only flush to disk every N batches, then reading may stall because we could have prefetch more pages (TODO: we could prefetch more pages in queue ?)
				//	R: [ Read B1 | Read B2 | Read B3 | Read B4 | Read B5 ]------------------[ Read B6 | Read B7 ]....
				//	W:           [*B1]-----[*B2]-----[*B3]-----[ *B4 + flush to disk ...... |*B5]-----[*B6]------....

				// this lambda should be applied on any new or reset transaction
				Func<IFdbReadOnlyTransaction, Task> reset = (tr) =>
				{
					// should export be lower priority? TODO: make if configurable!
					tr.Options.WithPriorityBatch();
					return Task.CompletedTask;
				};

				using (var tr = db.BeginReadOnlyTransaction(ct))
				{
					await reset(tr).ConfigureAwait(false);

					//TODO: make options configurable!
					var options = new FdbRangeOptions
					{
						// serial mode is optimized for a single client with maximum throughput
						Streaming = FdbStreamingMode.Serial,
					};

					long count = 0;
					long chunks = 0;
					long waitForFetch = 0;

					// read the first batch
					var page = await FetchNextBatchAsync(tr, begin, end, options, reset).ConfigureAwait(false);
					++waitForFetch;

					while (page.HasMore)
					{
						// prefetch the next one (don't wait for the task yet)
						var next = FetchNextBatchAsync(tr, KeySelector.FirstGreaterThan(page.Last), end, options, reset);

						// process the current one
						if (page.Count > 0)
						{
							ct.ThrowIfCancellationRequested();
							await handler(page.Items, count, ct).ConfigureAwait(false);
							++chunks;
							count += page.Count;
						}

						if (next.Status != TaskStatus.RanToCompletion) ++waitForFetch;
						page = await next.ConfigureAwait(false);
					}

					// process the last page, if any
					if (page.Count > 0)
					{
						ct.ThrowIfCancellationRequested();
						await handler(page.Items, count, ct).ConfigureAwait(false);
						++chunks;
						count += page.Count;
					}

#if DEBUG
					tr.Annotate($"Exported {count} items in {chunks} chunks ({(chunks > 0 ? (100.0 * waitForFetch / chunks) : 0.0):N1}% network)");
#endif

					return count;
				}
			}

			/// <summary>Read the next batch from a transaction, and retries if needed</summary>
			/// <param name="tr">Transaction used to read, which may be reset if a retry is needed</param>
			/// <param name="begin">Begin selector</param>
			/// <param name="end">End selector</param>
			/// <param name="options">Range read options</param>
			/// <param name="onReset">Action (optional) that can reconfigure a transaction whenever it gets reset inside the retry loop.</param>
			/// <returns>Task that will return the next batch</returns>
			private static async Task<FdbRangeChunk> FetchNextBatchAsync(IFdbReadOnlyTransaction tr, KeySelector begin, KeySelector end, FdbRangeOptions options, Func<IFdbReadOnlyTransaction, Task>? onReset = null)
			{
				Contract.Debug.Requires(tr != null && options != null);

				// read the next batch from the db, retrying if needed
				while (true)
				{
					try
					{
						return await tr.GetRangeAsync(begin, end, options).ConfigureAwait(false);
					}
					catch (FdbException e)
					{
						if (e.Code == FdbError.TransactionTooOld)
						{
							tr.Reset();
						}
						else
						{
							await tr.OnErrorAsync(e.Code).ConfigureAwait(false);
						}
						// before retrying, we need to re-configure the transaction if needed
						if (onReset != null) await onReset(tr).ConfigureAwait(false);
					}
				}
			}

			/// <summary>Export the content of a potentially large range of keys defined by a pair of selectors.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="path">Path of the subspace to export</param>
			/// <param name="handler">Lambda that will be called for each batch of data read from the database. The first argument is the array of ordered key/value pairs in the batch, taken from the same database snapshot. The second argument is the offset of the first item in the array, from the start of the range. The third argument is a token should be used by any async i/o performed by the lambda.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <returns>Number of keys exported</returns>
			/// <remarks>This method cannot guarantee that all data will be read from the same snapshot of the database, which means that writes committed while the export is running may be seen partially. Only the items inside a single batch are guaranteed to be from the same snapshot of the database.</remarks>
			public static async Task<long> ExportAsync(IFdbDatabase db, ISubspaceLocation path, [InstantHandle] Func<KeyValuePair<Slice, Slice>[], IKeySubspace, long, CancellationToken, Task> handler, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(handler);
				ct.ThrowIfCancellationRequested();

				// to maximize throughput, we want to read as much as possible per transaction, so that means that we should prefetch the next batch while the current batch is processing

				// If handler() is always faster than the prefetch(), the bottleneck is the database (or possibly the network).
				//	R: [ Read B1 ... | Read B2 ........ | Read B3 ..... ]
				//	W:               [ Process B1 ]-----[ Process B2 ]--[ Process B3]X

				// TODO: alternative method that we could use to almost double the throughput (second thread that exports backwards starting from the end)
				//	R: [ Read B1 ... | Read B2 ..... | Read B3 ..... | ...
				//	R:		| Read B10 ...... | Read B9 ..... | Read B8 ..... | ...
				//	W:               [ Process B1 | Process B10 | Process B2 | Process B9 | Process B3 | ...

				// If handler() is always slower than the prefetch(), the bottleneck is the local processing (or possibly local disk if writing to disk)
				//	R: [ Read B1 | Read B2 ]----------[ Read B3 ]-------[ Read B4 ]--------
				//	W:           [ Process B1 ....... | Process B2 .... | Process B3 .... | Process B4 ]

				// If handler() does some buffering, and only flush to disk every N batches, then reading may stall because we could have prefetch more pages (TODO: we could prefetch more pages in queue ?)
				//	R: [ Read B1 | Read B2 | Read B3 | Read B4 | Read B5 ]------------------[ Read B6 | Read B7 ]....
				//	W:           [*B1]-----[*B2]-----[*B3]-----[ *B4 + flush to disk ...... |*B5]-----[*B6]------....

				Slice previous = default;
				IKeySubspace location = null!;
				KeySelector begin = default;
				KeySelector end = default;

				// this lambda should be applied on any new or reset transaction
				Func<IFdbReadOnlyTransaction, Task> reset = async (tr) =>
				{
					// should export be lower priority? TODO: make if configurable!
					tr.Options.WithPriorityBatch();

					var folder = await path.TryResolve(tr).ConfigureAwait(false);
					if (previous.IsNull)
					{
						if (folder == null) throw new InvalidOperationException($"Failed to export the content of subspace {path} because it was not found.");

						previous = folder.GetPrefix();
						location = folder;
						var range = folder.ToRange();
						begin = range.ToBeginSelector();
						end = range.ToEndSelector();
					}
					else
					{
						if (folder == null || folder.GetPrefix() != previous)
						{
							throw new InvalidOperationException($"Failed to export the content of subspace {path} because it was deleted, moved or renamed during the operation.");
						}
						location = folder;
					}
				};

				using (var tr = db.BeginReadOnlyTransaction(ct))
				{
					await reset(tr).ConfigureAwait(false);

					//TODO: make options configurable!
					var options = new FdbRangeOptions
					{
						// serial mode is optimized for a single client with maximum throughput
						Streaming = FdbStreamingMode.Serial,
					};

					long count = 0;
					long chunks = 0;
					long waitForFetch = 0;

					// read the first batch
					var page = await FetchNextBatchAsync(tr, begin, end, options, reset).ConfigureAwait(false);
					++waitForFetch;

					while (page.HasMore)
					{
						// prefetch the next one (don't wait for the task yet)
						var next = FetchNextBatchAsync(tr, KeySelector.FirstGreaterThan(page.Last), end, options, reset);

						// process the current one
						if (page.Count > 0)
						{
							ct.ThrowIfCancellationRequested();
							await handler(page.Items, location, count, ct).ConfigureAwait(false);
							++chunks;
							count += page.Count;
						}

						if (next.Status != TaskStatus.RanToCompletion) ++waitForFetch;
						page = await next.ConfigureAwait(false);
					}

					// process the last page, if any
					if (page.Count > 0)
					{
						ct.ThrowIfCancellationRequested();
						await handler(page.Items, location, count, ct).ConfigureAwait(false);
						++chunks;
						count += page.Count;
					}

#if DEBUG
					tr.Annotate($"Exported {count} items in {chunks} chunks ({(chunks > 0 ? (100.0 * waitForFetch / chunks) : 0.0):N1}% network)");
#endif

					return count;
				}
			}

			#endregion

		}

	}

}
