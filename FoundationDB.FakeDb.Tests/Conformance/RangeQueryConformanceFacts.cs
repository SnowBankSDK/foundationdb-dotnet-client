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

// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable ReplaceAsyncWithTaskReturn
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace FoundationDB.Client.Tests
{
	using System.Diagnostics;
	using SnowBank.Data.Tuples;
	using SnowBank.Buffers;
	using FoundationDB.Testing;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.InteropServices;
	using SnowBank.Linq.Async.Iterators;

	/// <summary>Range-query conformance suite (GetRange chunks, paging, LINQ pipeline, merge sort, intersect and except), shared by every backend head below.</summary>
	public abstract class RangeQueryConformanceFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Get_Range_Chunk()
		{
			// test that we can get a chunk of data

			const int N = 200; // total item count
			//note: should be small enough so that a WantAll read all of it in one chunk, but large enough that Iterator does not!

			void Verify(FdbRangeChunk chunk, ReadOnlySpan<(Slice Key, Slice Value)> expected)
			{
				Assert.That(chunk.Count, Is.EqualTo(expected.Length));

				for (int i = 0; i < chunk.Count; i++)
				{
					Assert.That(chunk[i].Key, Is.EqualTo(expected[i].Key), $"[{i}].Key");
					Assert.That(chunk[i].Value, Is.EqualTo(expected[i].Value), $"[{i}].Value");

					Assert.That(chunk.Items[i].Key, Is.EqualTo(expected[i].Key), $"Items[{i}].Key");
					Assert.That(chunk.Items[i].Value, Is.EqualTo(expected[i].Value), $"Items[{i}].Value");

					Assert.That(chunk.Keys[i], Is.EqualTo(expected[i].Key), $"Keys[{i}]");
					Assert.That(chunk.Values[i], Is.EqualTo(expected[i].Value), $"Values[{i}]");
				}

				Log($"> First = {chunk.First:K}");
				Assert.That(chunk.First, Is.EqualTo(expected[0].Key));
				Log($"> Last = {chunk.Last:K}");
				Assert.That(chunk.Last, Is.EqualTo(expected[chunk.Count - 1].Key));

				// Total Bytes (Keys + Values)
				long total = 0;
				long totalKeys = 0;
				long totalValues = 0;
				foreach(var kv in chunk.Items)
				{
					total += kv.Key.Count + kv.Value.Count;
					totalKeys += kv.Key.Count;
					totalValues += kv.Value.Count;
				}
				Log($"> TotalBytes = {chunk.TotalBytes}");
				Assert.That(chunk.TotalBytes, Is.EqualTo(total), $"Total Bytes does not match (keys={totalKeys}, values={totalValues})");

				// ConcatValues
				var ms = new MemoryStream();
				foreach (var kv in expected)
				{
					ms.Write(kv.Value.Span);
				}
				var expectedValues = ms.ToSlice();
				var values = chunk.ConcatValues();
				if (!expectedValues.Equals(values))
				{
					DumpVersus(values, expectedValues);
					Assert.That(values.ToArray(), Is.EqualTo(expectedValues.ToArray()));
				}

				// AppendValues
				var sw = new SliceWriter();
				sw.WriteString("ThereIsAlreadySomethingInTheBuffer");
				int before = sw.Position;
				chunk.AppendValues(ref sw);
				Assert.That(sw.Position, Is.EqualTo(before + expectedValues.Count));
				expectedValues = Slice.FromString("ThereIsAlreadySomethingInTheBuffer").Concat(expectedValues);
				values = sw.ToSlice();
				if (!expectedValues.Equals(values))
				{
					DumpVersus(values, expectedValues);
					Assert.That(values.ToArray(), Is.EqualTo(expectedValues.ToArray()));
				}
			}

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				Log($"Inserting {N:N0} keys...");
				var insert = Stopwatch.StartNew();

				var data = await db.ReadWriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					var items = Enumerable.Range(0, N).Select(i => (subspace.Key(i).ToSlice(), Slice.FromInt32(i))).ToArray();
					tr.SetValues(items);
					return items;
				}, this.Cancellation);

				insert.Stop();

				Log($"> Committed {N:N0} keys in {insert.Elapsed.TotalMilliseconds:N1} ms");

				// Read All
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					Log("Getting range (WantAll)...");
					var ts = Stopwatch.StartNew();
					var chunk = await tr.GetRangeAsync(
						folder.Key(0),
						folder.Key(N),
						FdbRangeOptions.WantAll
					);
					ts.Stop();
					Assert.That(chunk, Is.Not.Null);
					Log($"> Read {chunk.Count:N0} results in {ts.Elapsed.TotalMilliseconds:N1} ms (TotalBytes={chunk.TotalBytes})");

					Assert.That(chunk.Count, Is.EqualTo(N), "Reading a small chunk in WantAll should return all results in one page! If this changes, you may need to tweak the parameters of the test!");
					Assert.That(chunk.IsEmpty, Is.False, "Should not be empty");
					Assert.That(chunk.HasMore, Is.False, "Should have all the results");
					Assert.That(chunk.Items, Is.Not.Null.And.Length.EqualTo(chunk.Count), "Items array should match result count");
					Assert.That(chunk.FetchMode, Is.EqualTo(FdbFetchMode.KeysAndValues));
					Assert.That(chunk.Reversed, Is.False);
					Assert.That(chunk.Keys.Count, Is.EqualTo(chunk.Count), "Keys collection count does not match");
					Assert.That(chunk.Values.Count, Is.EqualTo(chunk.Count), "Values collection count does not match");

					Verify(chunk, data.AsSpan(0, chunk.Count));
				}

				// Read Iterator
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					Log("Getting range (Iterator)...");
					var ts = Stopwatch.StartNew();
					var chunk = await tr.GetRangeAsync(
						folder.Key(0),
						folder.Key(N),
						FdbRangeOptions.Default
					);
					ts.Stop();
					Assert.That(chunk, Is.Not.Null);
					Log($"> Read {chunk.Count:N0} results in {ts.Elapsed.TotalMilliseconds:N1} ms (TotalBytes={chunk.TotalBytes})");
					// chunk sizing is client-implementation-specific: a backend may serve any non-empty prefix in the first chunk
					Assert.That(chunk.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(N), "The first chunk must serve a non-empty prefix of the range");
					if (!chunk.HasMore) Assert.That(chunk.Count, Is.EqualTo(N), "A chunk announcing no more results must have served the whole range");
					Assert.That(chunk.Items, Is.Not.Null.And.Length.EqualTo(chunk.Count), "Items array should match result count");
					Assert.That(chunk.FetchMode, Is.EqualTo(FdbFetchMode.KeysAndValues));
					Assert.That(chunk.Reversed, Is.False);
					Assert.That(chunk.Keys.Count, Is.EqualTo(chunk.Count), "Keys collection count does not match");
					Assert.That(chunk.Values.Count, Is.EqualTo(chunk.Count), "Values collection count does not match");

					Verify(chunk, data.AsSpan(0, chunk.Count));

				}, this.Cancellation);

			}
		}

		[Test]
		[Obsolete()]
		public async Task Test_Can_Get_Range_Chunk_Decoded()
		{
			// test that we can get a chunk of data using a decoder

			const int N = 200; // total item count
			//note: should be small enough so that a WantAll read all of it in one chunk, but large enough that Iterator does not!

			void Verify<TResult>(FdbRangeChunk<TResult> chunk, ReadOnlySpan<TResult> expected)
			{
				for (int i = 0; i < chunk.Count; i++)
				{
					Assert.That(chunk[i], Is.EqualTo(expected[i]), $"[{i}]");
					Assert.That(chunk.Items.Span[i], Is.EqualTo(expected[i]), $"Items[{i}]");
					Assert.That(chunk[new Index(i)], Is.EqualTo(expected[i]), $"[{i}]");
				}
			}

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				Log($"Inserting {N:N0} keys...");
				var insert = Stopwatch.StartNew();

				var data = Enumerable.Range(0, N).Select(i => (Key: i, Value: "SomeValue:" + i.ToString("D08"))).ToArray();

				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.SetValues(data, kv => subspace.Key(kv.Key), kv => FdbValue.ToTextUtf8(kv.Value));
				}, this.Cancellation);

				insert.Stop();

				Log($"> Committed {N:N0} keys in {insert.Elapsed.TotalMilliseconds:N1} ms");

				// Read All
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					Log("Getting range (WantAll)...");
					var ts = Stopwatch.StartNew();
					var chunk = await tr.GetRangeAsync(
						folder.Key(0),
						folder.Key(N),
						folder,
						static (folder, key, value) => (folder.Decode<int>(key), value.ToStringUtf8()),
						FdbRangeOptions.WantAll
					);
					ts.Stop();
					Assert.That(chunk, Is.Not.Null);
					Log($"> Read {chunk.Count:N0} results in {ts.Elapsed.TotalMilliseconds:N1} ms");
					//DumpCompact(chunk.Items.ToArray());

					Assert.That(chunk.Count, Is.EqualTo(N), "Reading a small chunk in WantAll should return all results in one page! If this changes, you may need to tweak the parameters of the test!");
					Assert.That(chunk.IsEmpty, Is.False, "Should not be empty");
					Assert.That(chunk.HasMore, Is.False, "Should have all the results");
					Assert.That(chunk.Items, Is.Not.Null.And.Length.EqualTo(chunk.Count), "Items array should match result count");
					Assert.That(chunk.FetchMode, Is.EqualTo(FdbFetchMode.KeysAndValues));
					Assert.That(chunk.Reversed, Is.False);

					Verify(chunk, data);
					Assert.That(chunk.First, Is.EqualTo(folder.Key(0)));
					Assert.That(chunk.Last, Is.EqualTo(folder.Key(N - 1)));
				}

				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					Log("Getting range (Iterator)...");
					var ts = Stopwatch.StartNew();
					var chunk = await tr.GetRangeAsync(
						folder.Key(0),
						folder.Key(N),
						folder,
						static (folder, key, value) => (folder.Decode<int>(key), value.ToStringUtf8()),
						FdbRangeOptions.Default
					);
					ts.Stop();
					Assert.That(chunk, Is.Not.Null);
					Log($"> Read {chunk.Count:N0} results in {ts.Elapsed.TotalMilliseconds:N1} ms");
					//DumpCompact(chunk.Items.ToArray());

					// chunk sizing is client-implementation-specific: a backend may serve any non-empty prefix in the first chunk
					Assert.That(chunk.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(N), "The first chunk must serve a non-empty prefix of the range");
					if (!chunk.HasMore) Assert.That(chunk.Count, Is.EqualTo(N), "A chunk announcing no more results must have served the whole range");
					Assert.That(chunk.Items, Is.Not.Null.And.Length.EqualTo(chunk.Count), "Items array should match result count");
					Assert.That(chunk.FetchMode, Is.EqualTo(FdbFetchMode.KeysAndValues));
					Assert.That(chunk.Reversed, Is.False);
					Assert.That(chunk.Iteration, Is.EqualTo(1));

#if NET5_0_OR_GREATER
					Verify(chunk, data[..chunk.Count]);
#else
					// range indexing on arrays requires RuntimeHelpers.GetSubArray, which netstandard2.0/netfx lacks
					Verify(chunk, data.AsSpan(0, chunk.Count));
#endif
					Assert.That(chunk.First, Is.EqualTo(folder.Key(0)));
					Assert.That(chunk.Last, Is.EqualTo(folder.Key(chunk.Count - 1)));

					// a backend may have served the whole range in the first chunk; the continuation protocol only applies when it did not
					if (chunk.Count == N) return;

					// read the next chunk
					ts.Restart();
					var chunk2 = await tr.GetRangeAsync(
						FdbKey.FromBytes(chunk.Last).Successor(),
						folder.Key(N),
						folder,
						static (folder, key, value) => (folder.Decode<int>(key), value.ToStringUtf8()),
						FdbRangeOptions.Default,
						chunk.Iteration
					);
					ts.Stop();
					Assert.That(chunk2, Is.Not.Null);
					Log($"> Read {chunk2.Count:N0} results in {ts.Elapsed.TotalMilliseconds:N1} ms");
					//DumpCompact(chunk2.Items.ToArray());

#if NET5_0_OR_GREATER
					Verify(chunk2, data[chunk.Count..][..chunk2.Count]);
#else
					// range indexing on arrays requires RuntimeHelpers.GetSubArray, which netstandard2.0/netfx lacks
					Verify(chunk2, data.AsSpan(chunk.Count, chunk2.Count));
#endif
					Assert.That(chunk2.First, Is.EqualTo(folder.Key(chunk.Count)));
					Assert.That(chunk2.Last, Is.EqualTo(folder.Key(chunk.Count + chunk2.Count - 1)));

				}, this.Cancellation);

			}
		}

		[Test]
		[Obsolete("Obsolete")]
		public async Task Test_Can_Get_Range()
		{
			// test that we can get a range of keys

			const int N = 1000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				Log($"Inserting {N:N0} keys...");

				var insert = Stopwatch.StartNew();
				await db.WriteAsync(async tr =>
				{
					var folder = await location.Resolve(tr);
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(folder.Key(i), FdbValue.ToCompactLittleEndian(i));
					}
				}, this.Cancellation);
				insert.Stop();

				Log($"Committed {N:N0} keys in {insert.Elapsed.TotalMilliseconds:N1} ms");

				// GetRange values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var query = tr.GetRange(folder.Key(0), folder.Key(N));
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Transaction, Is.SameAs(tr));
					Assert.That(query.Begin.Key, Is.EqualTo(folder.Key(0)));
					Assert.That(query.End.Key, Is.EqualTo(folder.Key(N)));
					Assert.That(query.Limit, Is.Null);
					Assert.That(query.TargetBytes, Is.Null);
					Assert.That(query.IsReversed, Is.False);
					Assert.That(query.Streaming, Is.EqualTo(FdbStreamingMode.Iterator));
					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.KeysAndValues));
					Assert.That(query.IsSnapshot, Is.False);
					Assert.That(query.Range.Begin, Is.EqualTo(query.Begin));
					Assert.That(query.Range.End, Is.EqualTo(query.End));

					Log($"Getting range {query.Range} ...");

					var ts = Stopwatch.StartNew();
					var items = await query.ToListAsync();
					ts.Stop();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));
					Log($"Took {ts.Elapsed.TotalMilliseconds:N1} ms to get {items.Count:N0} results");

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						var key = folder.Unpack(kvp.Key);

						if (i % 128 == 0) Log($"... {key} = {kvp.Value}");

						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(-1), Is.EqualTo(i));

						// value should be equal to the index
						Assert.That(kvp.Value.ToInt32(), Is.EqualTo(i));
					}
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_Paged()
		{
			// test that we can get a range of keys

			const int N = 1000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				Log($"Inserting {N:N0} keys...");

				var insert = Stopwatch.StartNew();
				await db.WriteAsync(async tr =>
				{
					var folder = await location.Resolve(tr);
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(folder.Key(i), FdbValue.ToCompactLittleEndian(i));
					}
				}, this.Cancellation);
				insert.Stop();

				Log($"Committed {N:N0} keys in {insert.Elapsed.TotalMilliseconds:N1} ms");

				// GetRange values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var query = tr
						.GetRange(folder.Key(0), folder.Key(N))
						.Paged();

					Assert.That(query, Is.Not.Null);
					Assert.That(query.Transaction, Is.SameAs(tr));
					Assert.That(query.Begin.Key, Is.EqualTo(folder.Key(0)));
					Assert.That(query.End.Key, Is.EqualTo(folder.Key(N)));
					Assert.That(query.Limit, Is.Null);
					Assert.That(query.TargetBytes, Is.Null);
					Assert.That(query.IsReversed, Is.False);
					Assert.That(query.Streaming, Is.EqualTo(FdbStreamingMode.Iterator));
					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.KeysAndValues));
					Assert.That(query.IsSnapshot, Is.False);
					Assert.That(query.Range.Begin, Is.EqualTo(query.Begin));
					Assert.That(query.Range.End, Is.EqualTo(query.End));

					Log($"Getting range {query.Range} ...");

					var items = new List<KeyValuePair<Slice, Slice>>();
					var ts = Stopwatch.StartNew();
					await foreach (var page in query)
					{
						Log($"- Batch: {page.Length,3:N0} result(s): {TuPack.Unpack(page.Span[0].Key)} .. {TuPack.Unpack(page.Span[^1].Key)}");
#if NET8_0_OR_GREATER
						items.AddRange(page.Span);
#else
						foreach (var item in MemoryMarshal.ToEnumerable(page))
						{
							items.Add(item);
						}
#endif
					}
					ts.Stop();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));
					Log($"Took {ts.Elapsed.TotalMilliseconds:N1} ms to get {items.Count:N0} results");

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						var key = folder.Unpack(kvp.Key);

						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(-1), Is.EqualTo(i));

						// value should be equal to the index
						Assert.That(kvp.Value.ToInt32(), Is.EqualTo(i));
					}
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_Only_Keys()
		{
			// test that we can get a range of with only the keys, or only the values

			const int N = 10_000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				await db.WriteAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					tr.SetValues(Enumerable.Range(0, N), i => folder.Key(i), FdbValue.ToCompactLittleEndian);
				}, this.Cancellation);

				// via FdbReadMode.Keys option
				// => returns a chunk of KV<Slice, Slice> but with Value == Slice.Nil
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var chunk = await tr.GetRangeAsync(
						folder.Key(0),
						folder.Key(N),
						FdbRangeOptions.WantAllKeysOnly
					);
					// note: this will not read ALL the keys in one chunk !
					Assert.That(chunk.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(N));
					Assert.That(chunk.HasMore, Is.EqualTo(chunk.Count < N), "HasMore flag is invalid");
					Assert.That(chunk.FetchMode, Is.EqualTo(FdbFetchMode.KeysOnly));
					Assert.That(chunk.First, Is.EqualTo(folder.Key(0)), "First key does not match");
					Assert.That(chunk.Last, Is.EqualTo(folder.Key(chunk.Count - 1)), "Last key does not match");

					for (int i = 0; i < chunk.Count; i++)
					{
						var kvp = chunk[i];

						// key should be a tuple in the correct order
						var key = folder.Unpack(kvp.Key);
						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(-1), Is.EqualTo(i));

						// value should be nil!
						Assert.That(kvp.Value, Is.EqualTo(Slice.Nil), "Reading with read mode 'Keys' should return nil values");
					}
				}, this.Cancellation);

				// via FdbReadMode.Keys option
				// => returns a sequence of KV<Slice, Slice> but with Value == Slice.Nil
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var query = tr.GetRange(folder.Key(0), folder.Key(N), FdbRangeOptions.KeysOnly);
					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.KeysOnly));

					var items = await query.ToListAsync();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						var key = folder.Unpack(kvp.Key);
						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(-1), Is.EqualTo(i));

						// value should be nil!
						Assert.That(kvp.Value, Is.EqualTo(Slice.Nil), "Reading with read mode 'Keys' should return nil values");
					}
				}, this.Cancellation);

				// via GetRangeKeys()
				// => return only a sequence of Slice
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var query = tr.GetRangeKeys(folder.Key(0), folder.Key(N));

					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.KeysOnly));

					var items = await query.ToListAsync();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));

					for (int i = 0; i < N; i++)
					{
						// key should be a tuple in the correct order
						var key = folder.Unpack(items[i]);
						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(^1), Is.EqualTo(i));
					}
				}, this.Cancellation);

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_Only_Values()
		{
			// test that we can get a range of with only the keys, or only the values

			const int N = 10_000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				await db.WriteAsync(async tr =>
				{
					var folder = await location.Resolve(tr);
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(folder.Key(i), FdbValue.ToCompactLittleEndian(i));
					}

				}, this.Cancellation);

				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var chunk = await tr.GetRangeAsync(
						folder.Key(0),
						folder.Key(N),
						FdbRangeOptions.WantAllValuesOnly
					);
					// note: this will not read ALL the keys in one chunk !
					Assert.That(chunk.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(N));
					Assert.That(chunk.HasMore, Is.EqualTo(chunk.Count < N), "HasMore flag is invalid");
					Assert.That(chunk.FetchMode, Is.EqualTo(FdbFetchMode.ValuesOnly));
					Assert.That(chunk.First, Is.EqualTo(folder.Key(0)), "The chunk should still read the first key (even in Values only mode)");
					Assert.That(chunk.Last, Is.EqualTo(folder.Key(chunk.Count - 1)), "The chunk should still read the last key (even in Values only mode)");

					for (int i = 0; i < chunk.Count; i++)
					{
						var kvp = chunk[i];

						// key should be a tuple in the correct order
						Assert.That(kvp.Key, Is.EqualTo(Slice.Nil), "Reading with read mode 'Values' should return nil keys");

						// value should be equal to the index
						Assert.That(chunk[i], Is.Not.EqualTo(Slice.Nil));
						Assert.That(kvp.Value.ToInt32(), Is.EqualTo(i));
					}
				}, this.Cancellation);

				// via FdbReadMode.Values option
				// => returns a sequence of KV<Slice, Slice> but with Key == Slice.Nil
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var query = tr.GetRange(
						folder.Key(0),
						folder.Key(N),
						FdbRangeOptions.ValuesOnly
					);
					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.ValuesOnly));

					var items = await query.ToListAsync();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						Assert.That(kvp.Key, Is.EqualTo(Slice.Nil), "Reading with read mode 'Values' should return nil keys");

						// value should be equal to the index
						Assert.That(items[i], Is.Not.EqualTo(Slice.Nil));
						Assert.That(kvp.Value.ToInt32(), Is.EqualTo(i));
					}
				}, this.Cancellation);

				// via GetRangeValues()
				// => return only a sequence of Slice
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var query = tr.GetRangeValues(folder.Key(0), folder.Key(N));

					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.ValuesOnly));

					var items = await query.ToListAsync();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));

					for (int i = 0; i < N; i++)
					{
						// value should be equal to the index
						Assert.That(items[i], Is.Not.EqualTo(Slice.Nil));
						Assert.That(items[i].ToInt32(), Is.EqualTo(i));
					}
				}, this.Cancellation);

				// if the range needs to read multiple chunks, it will need the last (or first) key for read the next chunk!
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var query = tr.GetRangeValues(
						folder.Key(0),
						folder.Key(N),
						new FdbRangeOptions { Streaming = FdbStreamingMode.Small }
					);

					Assert.That(query.Fetch, Is.EqualTo(FdbFetchMode.ValuesOnly));

					var items = await query.ToListAsync();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));

					for (int i = 0; i < N; i++)
					{
						// value should be equal to the index
						Assert.That(items[i], Is.Not.EqualTo(Slice.Nil));
						Assert.That(items[i].ToInt32(), Is.EqualTo(i));
					}
				}, this.Cancellation);

			}

		}

		[Test]
		public async Task Test_Can_Get_Range_Transformed()
		{
			// test that we can get a range of keys and convert them into another type

			const int N = 1000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				// insert all values (batched)
				await db.WriteAsync(async tr =>
				{
					var folder = await location.Resolve(tr);
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(folder.Key(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);

				{ // GetRange<TState, TResult>

					await db.ReadAsync(async tr =>
					{
						var folder = await location.Resolve(tr);

						var query = tr.GetRange(
							folder.Key(0),
							folder.Key(N),
							folder,
							static (f, k, v) => (Index: f.DecodeLast<int>(k), Score: v.ToInt32()),
							new FdbRangeOptions { Limit = N / 2 }
						);
						Assert.That(query, Is.Not.Null);

						var items = await query.ToListAsync();

						Assert.That(items, Is.Not.Null);
						Assert.That(items.Count, Is.EqualTo(N / 2));

						for (int i = 0; i < N / 2; i++)
						{
							Assert.That(items[i].Index, Is.EqualTo(i));
							Assert.That(items[i].Score, Is.EqualTo(i));
						}
					}, this.Cancellation);
				}

				{ // GetRange(...).Decode<TState, TResult>(...)

					await db.ReadAsync(async tr =>
					{
						var folder = await location.Resolve(tr);

						var query = tr
							.GetRange(
								folder.Key(0),
								folder.Key(N),
								new FdbRangeOptions { Limit = N / 2 }
							)
							.Decode(
								folder,
								static (f, k, v) => (Index: f.DecodeLast<int>(k), Score: v.ToInt32())
							);
						Assert.That(query, Is.Not.Null);

						var items = await query.ToListAsync();

						Assert.That(items, Is.Not.Null);
						Assert.That(items.Count, Is.EqualTo(N / 2));

						for (int i = 0; i < N / 2; i++)
						{
							Assert.That(items[i].Index, Is.EqualTo(i));
							Assert.That(items[i].Score, Is.EqualTo(i));
						}
					}, this.Cancellation);
				}

				{ // GetRange(...).Decode<TState, TResult>(...)

					await db.ReadAsync(async tr =>
					{
						var folder = await location.Resolve(tr);

						var query = tr
							.GetRange(
								folder.Key(0),
								folder.Key(N),
								new FdbRangeOptions { Limit = N / 2 }
							)
							.Decode(
								folder,
								static (f, k, v) => (Index: f.DecodeLast<int>(k), Score: v.ToInt32())
							);
						Assert.That(query, Is.Not.Null);

						var items = await query.ToListAsync();

						Assert.That(items, Is.Not.Null);
						Assert.That(items.Count, Is.EqualTo(N / 2));

						for (int i = 0; i < N / 2; i++)
						{
							Assert.That(items[i].Index, Is.EqualTo(i));
							Assert.That(items[i].Score, Is.EqualTo(i));
						}
					}, this.Cancellation);
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_First_Single_And_Last()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				var a = location.WithPrefix(TuPack.EncodeKey("a"));
				var b = location.WithPrefix(TuPack.EncodeKey("b"));
				var c = location.WithPrefix(TuPack.EncodeKey("c"));

				// insert a bunch of keys under 'a', only one under 'b', and nothing under 'c'
				await db.WriteAsync(async (tr) =>
				{
					var fa = await a.Resolve(tr);
					var fb = await b.Resolve(tr);
					for (int i = 0; i < 10; i++)
					{
						tr.Set(fa.Key(i), Slice.FromInt32(i));
					}
					tr.Set(fb.Key(0), Slice.FromInt32(42));
				}, this.Cancellation);

				KeyValuePair<Slice, Slice> res;

				// A: more than one item
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var fa = await a.Resolve(tr);

					var query = tr.GetRange(fa.ToRange());

					// should return the first one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(0)));

					// should return the first one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(0)));

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(9)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(9)));

					// should return the last one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(9)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(9)));

					// should fail because there is more than one
					Assert.That(async () => await query.SingleOrDefaultAsync(), Throws.InstanceOf<InvalidOperationException>(), "SingleOrDefaultAsync should throw if the range returns more than 1 result");

					// should fail because there is more than one
					Assert.That(async () => await query.SingleAsync(), Throws.InstanceOf<InvalidOperationException>(), "SingleAsync should throw if the range returns more than 1 result");
				}

				// B: exactly one item
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var fb = await b.Resolve(tr);

					var query = tr.GetRange(fb.ToRange());

					// should return the first one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fb.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(fb.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fb.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the last one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(fb.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.SingleOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fb.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.SingleAsync();
					Assert.That(res.Key, Is.EqualTo(fb.Key(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));
				}

				// C: no items
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var fc = await c.Resolve(tr);

					var query = tr.GetRange(fc.ToRange());

					// should return nothing
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should return the first one
					Assert.That(async () => await query.FirstAsync(), Throws.InstanceOf<InvalidOperationException>(), "FirstAsync should throw if the range returns nothing");

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should return the last one
					Assert.That(async () => await query.LastAsync(), Throws.InstanceOf<InvalidOperationException>(), "LastAsync should throw if the range returns nothing");

					// should fail because there is more than one
					res = await query.SingleOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should fail because there is none
					Assert.That(async () => await query.SingleAsync(), Throws.InstanceOf<InvalidOperationException>(), "SingleAsync should throw if the range returns nothing");
				}

				// A: with a size limit
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var fa = await a.Resolve(tr);

					var query = tr.GetRange(fa.ToRange()).Take(5);

					// should return the fifth one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(4)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(4)));

					// should return the fifth one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(4)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(4)));
				}

				// A: with an offset
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var fa = await a.Resolve(tr);

					var query = tr.GetRange(fa.ToRange()).Skip(5);

					// should return the fifth one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(5)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(5)));

					// should return the fifth one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(fa.Key(5)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(5)));
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_With_Limit()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				// put test values in a namespace
				await CleanLocation(db);

				// insert a bunch of keys under 'a'
				await db.WriteAsync(async tr =>
				{
					var subspace = await  db.Root.Resolve(tr);

					for (int i = 0; i < 10; i++)
					{
						tr.Set(subspace.Key("a", i), FdbValue.ToCompactLittleEndian(i));
					}
					// add guard keys
					tr.Set(subspace.Key(), FdbValue.MaxValue32);
					tr.Set(subspace.Last(), FdbValue.MaxValue32);
				}, this.Cancellation);

				// Take(5) should return the first 5 items

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);

					var query = tr.GetRange(subspace.Key("a").ToRange()).Take(5);
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Limit, Is.EqualTo(5));

					var elements = await query.ToListAsync();
					Assert.That(elements, Is.Not.Null);
					Assert.That(elements.Count, Is.EqualTo(5));
					for (int i = 0; i < 5; i++)
					{
						Assert.That(elements[i].Key, Is.EqualTo(subspace.Key("a", i)));
						Assert.That(elements[i].Value, Is.EqualTo(Slice.FromInt32(i)));
					}
				}

				// Take(12) should return only the 10 items

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);

					var query = tr.GetRange(subspace.Key("a").ToRange()).Take(12);
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Limit, Is.EqualTo(12));

					var elements = await query.ToListAsync();
					Assert.That(elements, Is.Not.Null);
					Assert.That(elements.Count, Is.EqualTo(10));
					for (int i = 0; i < 10; i++)
					{
						Assert.That(elements[i].Key, Is.EqualTo(subspace.Key("a", i)));
						Assert.That(elements[i].Value, Is.EqualTo(Slice.FromInt32(i)));
					}
				}

				// Take(0) should return nothing

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var fa = await db.Root.Resolve(tr);

					var query = tr.GetRange(fa.ToRange()).Take(0);
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Limit, Is.Zero);

					var elements = await query.ToListAsync();
					Assert.That(elements, Is.Not.Null);
					Assert.That(elements.Count, Is.Zero);
				}

			}
		}

		[Test]
		public async Task Test_Can_Skip()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				var dataSet = Enumerable.Range(0, 100).Select(x => (Index: x, Value: Slice.FromFixed32(x))).ToArray();

				// import test data
				await db.WriteAsync(async tr =>
				{
					var folder = await db.Root.Resolve(tr);
					foreach(var (k, v) in dataSet)
					{
						tr.Set(folder.Key(k), v);
					}
				}, this.Cancellation);

				// from the start
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var folder = await db.Root.Resolve(tr);

					var query = tr.GetRange(folder.ToRange());
					var data = dataSet.Select(kv => new KeyValuePair<Slice, Slice>(folder.Key(kv.Index).ToSlice(), kv.Value)).ToArray();

					// |>>>>>>>>>>>>(50---------->99)|
					var res = await query.Skip(50).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(50).ToList()), "50 --> 99");

					// |>>>>>>>>>>>>>>>>(60------>99)|
					res = await query.Skip(50).Skip(10).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(60).ToList()), "60 --> 99");

					// |xxxxxxxxxxxxxxxxxxxxxxx(99->)|
					res = await query.Skip(99).ToListAsync();
					Assert.That(res.Count, Is.EqualTo(1));
					Assert.That(res, Is.EqualTo(data.Skip(99).ToList()), "99 --> 99");

					// |xxxxxxxxxxxxxxxxxxxxxxxxxxxxx|(100->)
					res = await query.Skip(100).ToListAsync();
					Assert.That(res.Count, Is.Zero, "100 --> 99");

					// |xxxxxxxxxxxxxxxxxxxxxxxxxxxxx|_____________(150->)
					res = await query.Skip(150).ToListAsync();
					Assert.That(res.Count, Is.Zero, "150 --> 100");
				}

				// from the end
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var folder = await db.Root.Resolve(tr);

					var query = tr.GetRange(folder.ToRange());
					var data = dataSet.Select(kv => new KeyValuePair<Slice, Slice>(folder.Key(kv.Index).ToSlice(), kv.Value)).ToList();

					// |(0 <--------- 49)<<<<<<<<<<<<<|
					var res = await query.Reverse().Skip(50).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Reverse<KeyValuePair<Slice, Slice>>().Skip(50).ToList()), "0 <-- 49");

					// |(0 <----- 39)<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(50).Skip(10).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Reverse<KeyValuePair<Slice, Slice>>().Skip(60).ToList()), "0 <-- 39");

					// |(<- 0)<<<<<<<<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(99).ToListAsync();
					Assert.That(res.Count, Is.EqualTo(1));
					Assert.That(res, Is.EqualTo(data.Reverse<KeyValuePair<Slice, Slice>>().Skip(99).ToList()), "0 <-- 0");

					// (<- -1)|<<<<<<<<<<<<<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(100).ToListAsync();
					Assert.That(res.Count, Is.Zero, "0 <-- -1");

					// (<- -51)<<<<<<<<<<<<<|<<<<<<<<<<<<<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(100).ToListAsync();
					Assert.That(res.Count, Is.Zero, "0 <-- -51");
				}

				// from both sides
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var folder = await db.Root.Resolve(tr);

					var query = tr.GetRange(folder.ToRange());
					var data = dataSet.Select(kv => new KeyValuePair<Slice, Slice>(folder.Key(kv.Index).ToSlice(), kv.Value)).ToArray();

					// |>>>>>>>>>(25<------------74)<<<<<<<<|
					var res = await query.Skip(25).Reverse().Skip(25).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(25).Reverse().Skip(25).ToList()), "25 <-- 74");

					// |>>>>>>>>>(25------------>74)<<<<<<<<|
					res = await query.Skip(25).Reverse().Skip(25).Reverse().ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(25).Reverse().Skip(25).Reverse().ToList()), "25 --> 74");
				}
			}
		}

		[Test]
		public async Task Test_Original_Range_Does_Not_Overflow()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = db.Root;
				await CleanLocation(db, location);

				var dataSet = Enumerable.Range(0, 300).Select(x => (Index: x, Value: Slice.FromFixed32(x))).ToArray();

				// import test data
				await db.WriteAsync(async tr =>
				{
					var folder = await location.Resolve(tr);
					foreach (var (k, v) in dataSet)
					{
						tr.Set(folder.Key(k), v);
					}
				}, this.Cancellation);

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var query = tr
						.GetRange(folder.Key(10), folder.Key(20)) // 10 -> 19
						.Take(20) // 10 -> 19 (limit 20)
						.Reverse(); // 19 -> 10 (limit 20)
					Log($"query: {query}");

					// set a limit that overflows, and then reverse from it
					var res = await query.ToListAsync();
					Assert.That(res.Count, Is.EqualTo(10));
				}

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var query = tr
						.GetRange(folder.Key(10), folder.Key(20)) // 10 -> 19
						.Reverse() // 19 -> 10
						.Take(20)  // 19 -> 10 (limit 20)
						.Reverse(); // 10 -> 19 (limit 20)
					Log($"query: {query}");

					var res = await query.ToListAsync();
					Assert.That(res.Count, Is.EqualTo(10));
				}
			}
		}

		[Test]
		public async Task Test_Can_MergeSort()
		{
			const int K = 3;
			const int N = 100;

			// create K lists:
			// lists[0] contains all multiples of K ([0, 0], [K, 1], [2K, 2], ...)
			// lists[1] contains all multiples of K, offset by 1 ([1, 0], [K+1, 1], [2K+1, 2], ...)
			// lists[k-1] contains all multiples of K, offset by k-1 ([K-1, 0], [2K-1, 1], [3K-1, 2], ...)
			// more generally: lists[k][i] = (..., MergeSort, k, (i * K) + k) = (k, i)
			IKeySubspace GetList(IKeySubspace folder, int k)
			{
				return folder.Key(k).ToSubspace();
			}

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root;
				await CleanLocation(db, location);

				for (int k = 0; k < K; k++)
				{
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						var folder = await location.Resolve(tr);

						var list = GetList(folder, k);
						for (int i = 0; i < N; i++)
						{
							tr.Set(list.Key((i * K) + k), TuPack.EncodeKey(k, i));
						}
						await tr.CommitAsync();
					}
				}

				// MergeSorting all lists together should produce all integers from 0 to (K*N)-1, in order
				// we use the last part of the key for sorting

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var lists = Enumerable.Range(0, K).Select(k => GetList(folder, k)).ToArray();

					var merge = tr.MergeSort(
						lists.Select(list => KeySelectorPair.Create(list.ToRange().ToKeyRange())),
						kvp => folder.DecodeLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<MergeSortAsyncIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);
					Assert.That(results.Count, Is.EqualTo(K * N));

					for (int i = 0; i < K * N; i++)
					{
						Assert.That(folder.ExtractKey(results[i].Key), Is.EqualTo(TuPack.EncodeKey(i % K, i)));
						Assert.That(results[i].Value, Is.EqualTo(TuPack.EncodeKey(i % K, i / K)));
					}
				}
			}
		}

		[Test]
		public async Task Test_Range_Intersect()
		{
			const int K = 3;
			const int N = 100;

			// lists[0] contains all multiples of 1
			// lists[1] contains all multiples of 2
			// lists[k-1] contains all multiples of K
			// more generally: lists[k][i] = (..., Intersect, k, i * (k + 1)) = (k, i)
			IKeySubspace GetList(IKeySubspace folder, int k)
			{
				return folder.Key(k).ToSubspace();
			}

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root;
				await CleanLocation(db, location);

				var series = Enumerable.Range(1, K).Select(k => Enumerable.Range(1, N).Select(x => k * x).ToArray()).ToArray();
				//foreach(var serie in series)
				//{
				//	Log(String.Join(", ", serie));
				//}

				for (int k = 0; k < K; k++)
				{
					//Log("> k = " + k);
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						var folder = await location.Resolve(tr);
						var list = GetList(folder, k);
						for (int i = 0; i < N; i++)
						{
							var key = list.Key(series[k][i]);
							var value = TuPack.EncodeKey(k, i);
							//Log("> " + key + " = " + value);
							tr.Set(key, value);
						}
						await tr.CommitAsync();
					}
				}

				// Intersect all lists together should produce all integers that are multiples of numbers from 1 to K
				IEnumerable<int> xs = series[0];
				for (int i = 1; i < K; i++) xs = xs.Intersect(series[i]);
				var expected = xs.ToArray();
				Log($"Expected: {string.Join(", ", expected)}");

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var lists = Enumerable.Range(0, K).Select(k => GetList(folder, k)).ToArray();

					var merge = tr.Intersect(
						lists.Select(list => KeySelectorPair.Create(list.ToRange().ToKeyRange())),
						kvp => folder.DecodeLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<IntersectAsyncIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);

					Assert.That(results.Count, Is.EqualTo(expected.Length));

					for (int i = 0; i < results.Count; i++)
					{
						Assert.That(folder.DecodeLast<int>(results[i].Key), Is.EqualTo(expected[i]));
					}
				}
			}

		}

		[Test]
		public async Task Test_Range_Except()
		{
			const int K = 3;
			const int N = 100;

			// lists[0] contains all multiples of 1
			// lists[1] contains all multiples of 2
			// lists[k-1] contains all multiples of K
			// more generally: lists[k][i] = (..., Intersect, k, i * (k + 1)) = (k, i)
			IKeySubspace GetList(IKeySubspace folder, int k)
			{
				return folder.Key(k).ToSubspace();
			}

			using (var db = await OpenTestPartitionAsync())
			{
				// get a clean new directory
				var location = db.Root;
				await CleanLocation(db, location);

				var series = Enumerable.Range(1, K).Select(k => Enumerable.Range(1, N).Select(x => k * x).ToArray()).ToArray();
				//foreach(var serie in series)
				//{
				//	Log(String.Join(", ", serie));
				//}

				for (int k = 0; k < K; k++)
				{
					//Log("> k = " + k);
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						var folder = await location.Resolve(tr);
						var list = GetList(folder, k);
						for (int i = 0; i < N; i++)
						{
							var key = list.Key(series[k][i]);
							var value = TuPack.EncodeKey(k, i);
							//Log("> " + key + " = " + value);
							tr.Set(key, value);
						}
						await tr.CommitAsync();
					}
				}

				// Intersect all lists together should produce all integers that are prime numbers
				IEnumerable<int> xs = series[0];
				for (int i = 1; i < K; i++) xs = xs.Except(series[i]);
				var expected = xs.ToArray();
				Log($"Expected: {string.Join(", ", expected)}");

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await location.Resolve(tr);

					var lists = Enumerable.Range(0, K).Select(k => GetList(folder, k)).ToArray();

					var merge = tr.Except(
						lists.Select(list => KeySelectorPair.Create(list.ToRange().ToKeyRange())),
						kvp => folder.DecodeLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<ExceptAsyncIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);

					Assert.That(results.Count, Is.EqualTo(expected.Length));

					for (int i = 0; i < results.Count; i++)
					{
						Assert.That(folder.DecodeLast<int>(results[i].Key), Is.EqualTo(expected[i]));
					}
				}

			}

		}

		[Test]
		public async Task Test_Range_Except_Composite_Key()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				// Items contains a list of all ("user", id) that were created
				var locItems = db.Root.WithPrefix(TuPack.EncodeKey("Items"));
				// Processed contain the list of all ("user", id) that were processed
				var locProcessed = db.Root.WithPrefix(TuPack.EncodeKey("Processed"));

				// the goal is to have a query that returns the list of all unprocessed items (ie: in Items but not in Processed)

				await db.WriteAsync(async tr =>
				{
					var items = await locItems.Resolve(tr);
					var processed = await locProcessed.Resolve(tr);

					// Items
					tr.Set(items.Key("userA", 10093), Slice.Empty);
					tr.Set(items.Key("userA", 19238), Slice.Empty);
					tr.Set(items.Key("userB", 20003), Slice.Empty);
					// Processed
					tr.Set(processed.Key("userA", 19238), Slice.Empty);
				}, this.Cancellation);

				// the query (Items ∩ Processed) should return (userA, 10093) and (userB, 20003)

				// First Method: pass in a list of key ranges, and merge on the (Slice, Slice) pairs
				Log("Method 1:");
				var results = await db.QueryAsync(async tr =>
				{
					var items = await locItems.Resolve(tr);
					var processed = await locProcessed.Resolve(tr);

					var query = tr.Except(
						[ items.ToRange().ToKeyRange(), processed.ToRange().ToKeyRange() ],
						(kv) => TuPack.Unpack(kv.Key)[^2..], // note: keys come from any of the two ranges, so we must only keep the last 2 elements of the tuple
						TupleComparisons.Composite<string, int>() // compares t[0] as a string, and t[1] as an int
					);

					// problem: Except() still returns the original (Slice, Slice) pairs from the first range,
					// meaning that we still need to unpack again the key (this time knowing the location)
					return query.Select(kv => items.Decode<string, int>(kv.Key));
				}, this.Cancellation);

				foreach(var r in results)
				{
					Log(r);
				}
				Assert.That(results.Count, Is.EqualTo(2));
				Assert.That(results[0], Is.EqualTo(("userA", 10093)));
				Assert.That(results[1], Is.EqualTo(("userB", 20003)));

				// Second Method: pre-parse the queries, and merge on the results directly
				Log("Method 2:");
				results = await db.QueryAsync(async tr =>
				{
					var items = await locItems.Resolve(tr);
					var processed = await locProcessed.Resolve(tr);

					var resItems = tr
						.GetRange(items.ToRange())
						.Select(kv => items.Decode<string, int>(kv.Key));

					var resProcessed = tr
						.GetRange(processed.ToRange())
						.Select(kv => processed.Decode<string, int>(kv.Key));

					// items and processed are lists of (string, int) tuples, we can compare them directly
					var query = resItems.Except(resProcessed, TupleComparisons.Composite<string?, int>());

					// query is already a list of tuples, nothing more to do
					return query;
				}, this.Cancellation);

				foreach (var r in results)
				{
					Log(r);
				}
				Assert.That(results.Count, Is.EqualTo(2));
				Assert.That(results[0], Is.EqualTo(("userA", 10093)));
				Assert.That(results[1], Is.EqualTo(("userB", 20003)));
			}
		}

		[Test]
		public async Task Test_Can_Visit_Range()
		{
			// test that we can visit the key/values of a range, without allocation managed memory

			const int N = 1000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				// insert all values (batched)
				Log($"Inserting {N:N0} keys...");

				var insert = Stopwatch.StartNew();
				await db.WriteAsync(async tr =>
				{
					var folder = await db.Root.Resolve(tr);
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(folder.Key(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);
				insert.Stop();

				Log($"Committed {N:N0} keys in {insert.Elapsed.TotalMilliseconds:N1} ms");

				// GetRange values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await db.Root.Resolve(tr);

					Log("Visiting range ...");

					var ts = Stopwatch.StartNew();
					var items = new List<(IVarTuple Key, int Value)>(N);
					await tr.VisitRangeAsync(folder.Key(0), folder.Key(N), items, (state, k, v) =>
					{
#if NET9_0_OR_GREATER
						state.Add((folder.Unpack(k).ToTuple(), v.ToInt32()));
#else
						state.Add((folder.Unpack(k.ToSlice()), v.ToInt32()));
#endif
					});
					ts.Stop();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));
					Log($"Took {ts.Elapsed.TotalMilliseconds:N1} ms to visit {items.Count:N0} results");

					for (int i = 0; i < N; i++)
					{
						var (key, value) = items[i];
						if (i % 128 == 0) Log($"... {key} = {value}");

						// key should be a tuple in the correct order
						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(-1), Is.EqualTo(i));
						// value should be equal to the index
						Assert.That(value, Is.EqualTo(i));
					}
				}

			}
		}

		[Test]
		public async Task Test_Visit_Range_Honors_The_Limit_Across_Pages()
		{
			// a visit with a Limit smaller than the range must stop at the limit, even when the first page
			// reports more data: the remaining budget shrinks with every page instead of restarting at the limit

			const int N = 1000;
			const int LIMIT = 10;

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				await db.WriteAsync(async tr =>
				{
					var folder = await db.Root.Resolve(tr);
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(folder.Key(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var folder = await db.Root.Resolve(tr);

					var seen = new List<int>();
					var visited = await tr.VisitRangeAsync(folder.Key(0), folder.Key(N), seen, (state, k, v) => state.Add(v.ToInt32()), new FdbRangeOptions { Limit = LIMIT });

					Assert.That(visited, Is.EqualTo(LIMIT), "the visit must stop at the limit");
					Assert.That(seen, Is.EqualTo(Enumerable.Range(0, LIMIT).ToList()), "the first LIMIT keys, in order");

					// a limit larger than the range visits everything once
					seen.Clear();
					visited = await tr.VisitRangeAsync(folder.Key(0), folder.Key(N), seen, (state, k, v) => state.Add(v.ToInt32()), new FdbRangeOptions { Limit = N + 5 });
					Assert.That(visited, Is.EqualTo(N));
					Assert.That(seen.Count, Is.EqualTo(N));
				}
			}
		}

		[Test]
		public async Task Test_Range_Query_Supports_Composed_Linq_Operators()
		{
			// pins GetRange(...).Where(...).Select(...).ToListAsync() in both operator orders: the query's Where
			// must build its own iterator rather than tunnel into the AsyncQuery.Where extension, which dispatches
			// back to the query for any IAsyncLinqQuery source
			const int N = 50;

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root;
				await CleanLocation(db, location);

				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					for (var i = 0; i < N; i++)
					{
						tr.Set(subspace.Key(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);

				var (filtered, mapped) = await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);
					var f = await tr.GetRange(folder.Key(0), folder.Key(N))
						.Where(kv => kv.Value.ToInt32() % 2 == 0)
						.Select(kv => kv.Value.ToInt32())
						.ToListAsync();
					var m = await tr.GetRange(folder.Key(0), folder.Key(N))
						.Select(kv => kv.Value.ToInt32())
						.Where(v => v % 2 == 1)
						.ToListAsync();
					return (f, m);
				}, this.Cancellation);

				Assert.That(filtered, Is.EqualTo(Enumerable.Range(0, N).Where(i => i % 2 == 0)), "filter then map must stream every matching record");
				Assert.That(mapped, Is.EqualTo(Enumerable.Range(0, N).Where(i => i % 2 == 1)), "map then filter must stream every matching record");
			}
		}


	}

	/// <summary>Runs the range-query conformance suite against the FakeDb emulator.</summary>
	[TestFixture]
	public class RangeQueryFakeDbFacts : RangeQueryConformanceFacts
	{

		/// <summary>Store shared by all databases opened during a single test, reset between tests.</summary>
		private FakeDbStore? Store { get; set; }

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetFakeDbStore() => this.Store = null;

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false)
		{
			// mirror FdbTest.OpenTestDatabaseAsync: the real-cluster head seeds a 15s default timeout via FdbConnectionOptions.DefaultTimeout
			var db = (this.Store ??= TestBuggify.ChaosStore()).OpenDatabase(FdbPath.Root, readOnly);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null)
		{
			var db = (this.Store ??= TestBuggify.ChaosStore()).OpenDatabase(GetTestPartitionPath(testMethod), readOnly: false);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

	}

	/// <summary>Runs the range-query conformance suite against a real FoundationDB cluster (Testcontainers). Run explicitly from the Unit Test Sessions UI; requires a local Docker daemon.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class RangeQueryRealClusterFacts : RangeQueryConformanceFacts
	{
	}

}
