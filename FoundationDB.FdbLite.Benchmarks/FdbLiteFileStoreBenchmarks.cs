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
	using FoundationDB.Storage;
	using BenchmarkDotNet.Attributes;
	using BenchmarkDotNet.Engines;
	using FoundationDB.Client;
	using FoundationDB.FdbLite;
	using SnowBank.Data.Tuples;

	/// <summary>Shared corpus of the file-store series: one ~1 GiB store file per geometry under the temp folder (reused across runs), plus the deterministic probe set.</summary>
	public static class FdbLiteFileStoreFixture
	{

		/// <summary>1M keys x ~1 KB values: ~1 GiB of data, well past L3</summary>
		public const int KeyCount = 1_000_000;

		public const int ProbeCount = 4_096;

		public const int AggregateCount = 100_000;

		public static readonly string[] GeometryNames = [ "u16K", "u32K", "u64K", "b16K/p64K" ];

		public static FdbLiteGeometry Parse(string name) => name switch
		{
			"u16K" => FdbLiteGeometry.Uniform(14),
			"u32K" => FdbLiteGeometry.Uniform(15),
			"u64K" => FdbLiteGeometry.Uniform(16),
			_ => FdbLiteGeometry.Hypothesis,
		};

		public static byte[] MakeKey(int i) => TuPack.EncodeKey("D", i).GetBytes()!;

		/// <summary>Builds (or reuses) the geometry's store file and returns its path.</summary>
		public static string EnsureStore(string geometryName)
		{
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-bench");
			Directory.CreateDirectory(dir);
			var path = Path.Combine(dir, $"matrix-{geometryName.Replace('/', '-')}.sbkv");
			var geometry = Parse(geometryName);

			bool build = true;
			if (File.Exists(path) && new FileInfo(path).Length > 0)
			{
				using var probe = BenchEngines.OpenFilePinned(path, geometry);
				build = probe.Durable.KeyCount != KeyCount;
			}
			if (build)
			{
				File.Delete(path);
				using var builder = BenchEngines.OpenFilePinned(path, geometry);
				var rnd = new Random(20260722);
				var value = new byte[1_000];
				var writer = builder.BeginWrite();
				for (int i = 0; i < KeyCount; i++)
				{
					rnd.NextBytes(value);
					writer.Insert(MakeKey(i), value);
				}
				builder.Commit(writer, 1);
			}
			return path;
		}

		public static int[] MakeProbeIds()
		{
			var probeRnd = new Random(4242);
			var ids = new int[ProbeCount];
			for (int i = 0; i < ProbeCount; i++)
			{
				ids[i] = probeRnd.Next(KeyCount);
			}
			return ids;
		}

	}

	/// <summary>The candidate geometries on a FILE-backed store much larger than L3, at the two process-lifecycle tiers: COLD (device, cache purged) and RESTART (process-cold but OS-cache-hot). The HOT (steady-state) tier lives in <see cref="FdbLiteFileStoreHotBenchmarks"/>, under a measurement strategy that fits it.</summary>
	/// <remarks>
	/// <para>DISK-TOUCHING and disk-space-hungry: builds one ~1 GiB store file per geometry under the temp folder (reused across runs).</para>
	/// <para>Tiers, honestly: <b>cold</b> tears down the store and PURGES the OS file cache (<see cref="OsCachePurge"/>, needs elevation) before reopening - if the purge cannot run, the result is labeled NOT COLD in the console, never silently reported as cold. <b>restart</b> tears down and reopens with the cache hot (a fresh mapping over cached pages: the everyday agent-restart case). Monitoring strategy + one invocation per iteration keeps each measurement a single real reopen, not an amortized average.</para>
	/// <para>SLICEABLE: <c>--geometry=</c> and <c>--tier=</c> restrict the matrix to one cell per invocation (see the recipes in README.md); the full matrix is assembled from slices.</para>
	/// </remarks>
	[MemoryDiagnoser]
	[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 8, invocationCount: 1)]
	[JsonExporterAttribute.Brief]
	public class FdbLiteFileStoreBenchmarks
	{

		/// <summary>The four candidate geometries (sliced by <c>--geometry=</c>)</summary>
		[ParamsSource(nameof(GeometryValues))]
		public string Geometry = "";

		/// <summary>Process-lifecycle tiers, worst-to-best (sliced by <c>--tier=</c>; <c>hot</c> selects the sibling class instead)</summary>
		[ParamsSource(nameof(TierValues))]
		public string Tier = "";

		public static IEnumerable<string> GeometryValues => BenchSlice.Filter(FdbLiteFileStoreFixture.GeometryNames, BenchSlice.Geometry);

		public static IEnumerable<string> TierValues
			=> BenchSlice.Tier is { } tier && string.Equals(tier, "hot", StringComparison.OrdinalIgnoreCase)
				? throw new ArgumentException("--tier=hot selects the steady-state class: use --filter \"*FileStoreHot*\" (see the slice recipes in README.md)")
				: BenchSlice.Filter([ "cold", "restart" ], BenchSlice.Tier);

		private string SourcePath = "";

		private FdbLiteEngine Engine = null!;

		private int[] ProbeIds = null!;

		private bool WarnedThisRun;

		[GlobalSetup]
		public void Setup()
		{
			this.SourcePath = FdbLiteFileStoreFixture.EnsureStore(this.Geometry);
			this.ProbeIds = FdbLiteFileStoreFixture.MakeProbeIds();
			this.Engine = BenchEngines.OpenFilePinned(this.SourcePath, FdbLiteFileStoreFixture.Parse(this.Geometry));
		}

		[GlobalCleanup]
		public void Cleanup() => this.Engine.Dispose();

		/// <summary>Prepares the engine to the current tier's thermal state (not part of the measurement).</summary>
		[IterationSetup]
		public void PrepareTier()
		{
			switch (this.Tier)
			{
				case "restart":
				{ // tear down mappings and reopen; the OS page cache still holds the file
					this.Engine.Dispose();
					this.Engine = BenchEngines.OpenFilePinned(this.SourcePath, FdbLiteFileStoreFixture.Parse(this.Geometry));
					break;
				}
				case "cold":
				{ // tear down, purge the OS cache, reopen: reads must reach the device
					this.Engine.Dispose();
					var purge = OsCachePurge.Purge();
					if (!purge.Purged && !this.WarnedThisRun)
					{
						this.WarnedThisRun = true;
						Console.Error.WriteLine($"### COLD TIER NOT COLD [{this.Geometry}]: {purge}. Run elevated (see README.md), or these rows are RESTART-tier, not COLD.");
					}
					this.Engine = BenchEngines.OpenFilePinned(this.SourcePath, FdbLiteFileStoreFixture.Parse(this.Geometry));
					break;
				}
			}
		}

		/// <summary>Uniform-random single-key reads over the ~1 GiB store at the current tier.</summary>
		[Benchmark(OperationsPerInvoke = FdbLiteFileStoreFixture.ProbeCount)]
		public long RandomPointReads()
		{
			long sum = 0;
			var pager = this.Engine.Pager;
			uint root = this.Engine.Durable.RootPageId;
			foreach (var id in this.ProbeIds)
			{
				if (FdbLiteTreeReader.TryGetValue(pager, root, FdbLiteFileStoreFixture.MakeKey(id), out var v)) { sum += v.Length; }
			}
			return sum;
		}

		/// <summary>One 100k-key aggregate read through the FULL transaction seam (emulator handler, read-your-writes merge, chunk paging) at the current tier.</summary>
		[Benchmark]
		public async Task<int> AggregateGetRange_FullSeam()
		{
			// a store over the (already tier-prepared) engine; it must NOT dispose the shared engine between iterations,
			// so it is created per invocation over the same engine and only the database is torn down
			var store = new FdbLiteStore(this.Engine, disposeEngine: false);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
			var begin = FdbLiteFileStoreFixture.MakeKey(400_000).AsSlice();
			var end = FdbLiteFileStoreFixture.MakeKey(400_000 + FdbLiteFileStoreFixture.AggregateCount).AsSlice();
			return await db.ReadAsync(tr => tr.GetRange(begin, end).ToListAsync(), CancellationToken.None) is { } items ? items.Count : 0;
		}

	}

	/// <summary>The HOT (steady-state) tier of the file-store series, under the default throughput strategy: BenchmarkDotNet auto-scales invocations per iteration, so per-read resolution is real statistics instead of one 2 ms monitoring sample (the min-iteration-time warnings of the previous single-class shape).</summary>
	/// <remarks>Same store files and probe set as <see cref="FdbLiteFileStoreBenchmarks"/>; sliced by <c>--geometry=</c>.</remarks>
	[MemoryDiagnoser]
	[JsonExporterAttribute.Brief]
	public class FdbLiteFileStoreHotBenchmarks
	{

		/// <summary>The four candidate geometries (sliced by <c>--geometry=</c>)</summary>
		[ParamsSource(nameof(GeometryValues))]
		public string Geometry = "";

		public static IEnumerable<string> GeometryValues => BenchSlice.Filter(FdbLiteFileStoreFixture.GeometryNames, BenchSlice.Geometry);

		private FdbLiteEngine Engine = null!;

		private int[] ProbeIds = null!;

		[GlobalSetup]
		public void Setup()
		{
			var path = FdbLiteFileStoreFixture.EnsureStore(this.Geometry);
			this.ProbeIds = FdbLiteFileStoreFixture.MakeProbeIds();
			this.Engine = BenchEngines.OpenFilePinned(path, FdbLiteFileStoreFixture.Parse(this.Geometry));

			// reach steady state once; the measurement loop maintains it from here
			var pager = this.Engine.Pager;
			uint root = this.Engine.Durable.RootPageId;
			foreach (var id in this.ProbeIds)
			{
				FdbLiteTreeReader.TryGetValue(pager, root, FdbLiteFileStoreFixture.MakeKey(id), out _);
			}
		}

		[GlobalCleanup]
		public void Cleanup() => this.Engine.Dispose();

		/// <summary>Uniform-random single-key reads over the ~1 GiB store, steady state.</summary>
		[Benchmark(OperationsPerInvoke = FdbLiteFileStoreFixture.ProbeCount)]
		public long RandomPointReads()
		{
			long sum = 0;
			var pager = this.Engine.Pager;
			uint root = this.Engine.Durable.RootPageId;
			foreach (var id in this.ProbeIds)
			{
				if (FdbLiteTreeReader.TryGetValue(pager, root, FdbLiteFileStoreFixture.MakeKey(id), out var v)) { sum += v.Length; }
			}
			return sum;
		}

		/// <summary>One 100k-key aggregate read through the FULL transaction seam, steady state.</summary>
		[Benchmark]
		public async Task<int> AggregateGetRange_FullSeam()
		{
			var store = new FdbLiteStore(this.Engine, disposeEngine: false);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
			var begin = FdbLiteFileStoreFixture.MakeKey(400_000).AsSlice();
			var end = FdbLiteFileStoreFixture.MakeKey(400_000 + FdbLiteFileStoreFixture.AggregateCount).AsSlice();
			return await db.ReadAsync(tr => tr.GetRange(begin, end).ToListAsync(), CancellationToken.None) is { } items ? items.Count : 0;
		}

	}

}
