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

namespace FoundationDB.FdbLite.Benchmarks
{
	using BenchmarkDotNet.Attributes;
	using SnowBank.Collections.CacheOblivious;
	using SnowBank.Data.Tuples;
	using FoundationDB.Storage;

	/// <summary>Measures the per-key dispatch cost of a full scan over a committed store, per seam shape: IEnumerable, interface cursor, generic struct cursor.</summary>
	/// <remarks>These series measure whether a scan loop gains from a concrete struct cursor: the answer is the per-key delta between the interface series and the generic-struct series.</remarks>
	[MemoryDiagnoser]
	[JsonExporterAttribute.Brief]
	public class CommittedStoreScanBenchmarks
	{

		/// <summary>Number of key/value pairs in the committed keyspace</summary>
		[Params(100_000)]
		public int N;

		private ColaCommittedStore Store = null!;

		private Arena Arena = null!;

		[GlobalSetup]
		public void Setup()
		{
			this.Arena = new Arena(1024 * 1024, 16 * 1024 * 1024, System.Buffers.ArrayPool<byte>.Create());
			var data = new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default);
			var value = this.Arena.InternValue(Slice.Repeat(0x78, 100));
			for (int i = 0; i < this.N; i++)
			{ // tuple-shaped keys (~20 B), doc-chunk sized values (100 B)
				data[new Key(TuPack.EncodeKey("bench", i))] = value;
			}
			this.Store = new ColaCommittedStore(data);
		}

		/// <summary>The train-1 shape of the committed-only range read: IEnumerable, one allocation + interface dispatch per item</summary>
		[Benchmark(Baseline = true)]
		public long ScanEnumerable()
		{
			long sum = 0;
			foreach (var kv in this.Store.IterateOrdered())
			{
				sum += kv.Value.Count;
			}
			return sum;
		}

		/// <summary>Cursor loop through the non-generic interface: one indirect call per Next()/Current, no inlining</summary>
		[Benchmark]
		public long ScanInterfaceCursor() => ScanCore(this.Store);

		private static long ScanCore(IFdbCommittedStore store)
		{
			var cursor = store.GetCursor();
			long sum = 0;
			if (cursor.SeekFirst())
			{
				do { sum += cursor.CopyCurrent().Value.Count; } while (cursor.Next());
			}
			return sum;
		}

		/// <summary>Cursor loop monomorphized over the concrete struct cursor: per-key calls devirtualize and inline</summary>
		[Benchmark]
		public long ScanGenericStructCursor() => ScanCore<ColaCommittedCursor>(this.Store);

		private static long ScanCore<TCursor>(IFdbCommittedStore<TCursor> store)
			where TCursor : struct, IFdbCommittedCursor
		{
			var cursor = store.GetCursor();
			long sum = 0;
			if (cursor.SeekFirst())
			{
				do { sum += cursor.CopyCurrent().Value.Count; } while (cursor.Next());
			}
			return sum;
		}

	}

	/// <summary>The same scan shapes over the PERSISTENT backend, where devirtualization applies, plus the raw engine cursor as the zero-copy ceiling.</summary>
	[MemoryDiagnoser]
	[JsonExporterAttribute.Brief]
	public class FdbLiteCommittedStoreScanBenchmarks
	{

		[Params(100_000)]
		public int N;

		private FoundationDB.FdbLite.FdbLiteEngine Engine = null!;

		private FoundationDB.FdbLite.FdbLiteCommittedStore Store = null!;

		[GlobalSetup]
		public void Setup()
		{
			var engine = FoundationDB.FdbLite.FdbLiteEngine.Create(new FoundationDB.FdbLite.FdbLiteHeapPager(FoundationDB.FdbLite.FdbLiteGeometry.Hypothesis));
			var writer = engine.BeginWrite();
			Span<byte> value = stackalloc byte[100];
			value.Fill(0x78);
			for (int i = 0; i < this.N; i++)
			{
				writer.Insert(TuPack.EncodeKey("bench", i).Span, value);
			}
			engine.Commit(writer, 1);
			this.Engine = engine;
			this.Store = new FoundationDB.FdbLite.FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);
		}

		[GlobalCleanup]
		public void Cleanup() => this.Engine.Dispose();

		/// <summary>Seam enumerable (materializes one Key/Value copy per item)</summary>
		[Benchmark(Baseline = true)]
		public long ScanEnumerable()
		{
			long sum = 0;
			foreach (var kv in this.Store.IterateOrdered())
			{
				sum += kv.Value.Count;
			}
			return sum;
		}

		/// <summary>Seam cursor through the non-generic interface (boxed struct, indirect per-key calls, materializing copies)</summary>
		[Benchmark]
		public long ScanInterfaceCursor()
		{
			IFdbCommittedCursor cursor = ((IFdbCommittedStore) this.Store).GetCursor();
			long sum = 0;
			if (cursor.SeekFirst())
			{
				do { sum += cursor.CopyCurrent().Value.Count; } while (cursor.Next());
			}
			return sum;
		}

		/// <summary>Seam cursor monomorphized over the concrete struct (still materializing copies)</summary>
		[Benchmark]
		public long ScanGenericStructCursor()
		{
			var cursor = this.Store.GetCursor();
			long sum = 0;
			if (cursor.SeekFirst())
			{
				do { sum += cursor.CopyCurrent().Value.Count; } while (cursor.Next());
			}
			return sum;
		}

		/// <summary>The raw engine cursor over page spans, no materialization: the zero-copy ceiling for any scan loop above it</summary>
		[Benchmark]
		public long ScanEngineCursorRaw()
		{
			var cursor = new FoundationDB.FdbLite.FdbLiteTreeCursor(this.Engine.Pager, this.Engine.Durable.RootPageId);
			long sum = 0;
			if (cursor.SeekFirst())
			{
				do { sum += cursor.CurrentValue.Length; } while (cursor.MoveNext());
			}
			return sum;
		}

	}

}
