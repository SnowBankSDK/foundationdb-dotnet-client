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
	using FoundationDB.FdbLite;
	using SnowBank.Data.Tuples;

	/// <summary>The four candidate geometries against the reference workload classes (heap pager: pure CPU/layout cost, no disk noise). The write-amplification axis is measured separately, in exact bytes, by the write-amplification facts.</summary>
	[MemoryDiagnoser]
	[SimpleJob(warmupCount: 3, iterationCount: 8)]
	[JsonExporterAttribute.Brief]
	public class FdbLiteGeometryMatrixBenchmarks
	{

		/// <summary>The four candidate geometries</summary>
		[ParamsSource(nameof(GeometryValues))]
		public string Geometry = "";

		public static IEnumerable<string> GeometryValues => BenchSlice.Filter([ "u16K", "u32K", "u64K", "b16K/p64K" ], BenchSlice.Geometry);

		private const int ChunkCount = 50_000;

		private const int IndexCount = 50_000;

		private const int ProbeCount = 2_000;

		private FdbLiteEngine Engine = null!;

		private byte[][] ChunkProbes = null!;

		private byte[][] IndexProbes = null!;

		private ulong NextVersion;

		private int TinySequence;

		private static FdbLiteGeometry Parse(string name) => name switch
		{
			"u16K" => FdbLiteGeometry.Uniform(14),
			"u32K" => FdbLiteGeometry.Uniform(15),
			"u64K" => FdbLiteGeometry.Uniform(16),
			_ => FdbLiteGeometry.Hypothesis,
		};

		[GlobalSetup]
		public void Setup()
		{
			this.Engine = FdbLiteEngine.Create(new FdbLiteHeapPager(Parse(this.Geometry), regionSizeInBytes: 16 << 20));
			var rnd = new Random(1789);

			// the 25 B document-chunk class: rid-keyed, ~1 KB values
			var writer = this.Engine.BeginWrite();
			var chunkValue = new byte[1_000];
			rnd.NextBytes(chunkValue);
			for (int i = 0; i < ChunkCount; i++)
			{
				writer.Insert(TuPack.EncodeKey("D", rnd.Next(), i).Span, chunkValue);
			}
			this.Engine.Commit(writer, 10);

			// the 100 B index class: long collated payloads, empty values
			writer = this.Engine.BeginWrite();
			var padding = new byte[70];
			for (int i = 0; i < IndexCount; i++)
			{
				rnd.NextBytes(padding);
				writer.Insert(TuPack.EncodeKey("I", Convert.ToHexString(padding[..35]), i).Span, default);
			}
			this.Engine.Commit(writer, 11);
			this.NextVersion = 12;

			// probe keys: sampled by re-scanning (values of rnd differ per key, so replay the generator instead)
			this.ChunkProbes = new byte[ProbeCount][];
			this.IndexProbes = new byte[ProbeCount][];
			var cursor = new FdbLiteTreeCursor(this.Engine.Pager, this.Engine.Durable.RootPageId);
			int total = (int) this.Engine.Durable.KeyCount;
			int step = Math.Max(1, total / ProbeCount);
			int filledChunk = 0, filledIndex = 0, index = 0;
			if (cursor.SeekFirst())
			{
				do
				{
					if (index++ % step != 0) { continue; }
					var key = cursor.CurrentKey.ToArray();
					if (key[1] == (byte) 'D')
					{
						if (filledChunk < ProbeCount) { this.ChunkProbes[filledChunk++] = key; }
					}
					else if (filledIndex < ProbeCount)
					{
						this.IndexProbes[filledIndex++] = key;
					}
				}
				while (cursor.MoveNext());
			}
			// wrap-fill any tail slots
			for (int i = filledChunk; i < ProbeCount; i++) { this.ChunkProbes[i] = this.ChunkProbes[i % Math.Max(1, filledChunk)]; }
			for (int i = filledIndex; i < ProbeCount; i++) { this.IndexProbes[i] = this.IndexProbes[i % Math.Max(1, filledIndex)]; }
		}

		[GlobalCleanup]
		public void Cleanup() => this.Engine.Dispose();

		/// <summary>Point reads at the 25 B chunk-key class (values ~1 KB inline)</summary>
		[Benchmark(OperationsPerInvoke = ProbeCount)]
		public long PointRead_ChunkKeys()
		{
			long sum = 0;
			var pager = this.Engine.Pager;
			uint root = this.Engine.Durable.RootPageId;
			foreach (var probe in this.ChunkProbes)
			{
				if (FdbLiteTreeReader.TryGetValue(pager, root, probe, out var v)) { sum += v.Length; }
			}
			return sum;
		}

		/// <summary>Point reads at the 100 B index-key class (empty values)</summary>
		[Benchmark(OperationsPerInvoke = ProbeCount)]
		public long PointRead_IndexKeys()
		{
			long sum = 0;
			var pager = this.Engine.Pager;
			uint root = this.Engine.Durable.RootPageId;
			foreach (var probe in this.IndexProbes)
			{
				if (FdbLiteTreeReader.TryGetValue(pager, root, probe, out var v)) { sum += v.Length + 1; }
			}
			return sum;
		}

		/// <summary>Full ordered scan of the 100k keys (the raw engine cursor)</summary>
		[Benchmark]
		public long RangeScan_All()
		{
			var cursor = new FdbLiteTreeCursor(this.Engine.Pager, this.Engine.Durable.RootPageId);
			long sum = 0;
			if (cursor.SeekFirst())
			{
				do { sum += cursor.CurrentKey.Length; } while (cursor.MoveNext());
			}
			return sum;
		}

		/// <summary>One tiny committed transaction (a single small key/value): the sustained-ingest CPU cost of the COW path</summary>
		[Benchmark]
		public void TinyCommit()
		{
			var writer = this.Engine.BeginWrite();
			writer.Insert(TuPack.EncodeKey("T", this.TinySequence++).Span, "0123456789ABCDEF"u8);
			this.Engine.Commit(writer, this.NextVersion++);
		}

		/// <summary>One committed 100,000-byte extent value: the large-value class</summary>
		[Benchmark]
		public void ExtentCommit100KB()
		{
			var writer = this.Engine.BeginWrite();
			var value = new byte[100_000];
			value[0] = (byte) this.TinySequence;
			writer.Insert(TuPack.EncodeKey("X", this.TinySequence++ & 15).Span, value);
			this.Engine.Commit(writer, this.NextVersion++);
		}

	}

}
