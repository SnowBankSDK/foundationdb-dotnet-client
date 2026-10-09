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
	using System.Diagnostics;
	using FoundationDB.FdbLite;
	using SnowBank.Data.Tuples;

	/// <summary>Segmented cold/restart read log: runs random point reads in fixed-size chunks and prints per-chunk throughput, so a mid-run performance CLIFF (the OS-cache capacity boundary) is visible directly - the thing BenchmarkDotNet means hide.</summary>
	/// <remarks>Invoked from Program with <c>--chunklog &lt;geometry&gt; &lt;cold|restart&gt; [keyCount]</c>, and automatically after every fully-specified cold/restart slice. The cold variant purges the OS cache first (needs elevation; reports NOT COLD honestly if it cannot). With <paramref name="teeToArtifacts"/> the log is also written to <c>BenchmarkDotNet.Artifacts/results/</c>, so the cliff evidence travels with the exported reports.</remarks>
	public static class FileStoreChunkLog
	{

		public static void Run(string geometryName, string tier, int keyCount, bool teeToArtifacts = false, string? artifactsRoot = null)
		{
			StreamWriter? tee = null;
			if (teeToArtifacts)
			{
				var resultsDir = Path.Combine(artifactsRoot ?? Path.Combine(Environment.CurrentDirectory, "BenchmarkDotNet.Artifacts"), "results");
				Directory.CreateDirectory(resultsDir);
				tee = new StreamWriter(Path.Combine(resultsDir, $"chunklog-{geometryName.Replace('/', '-')}-{tier}.log"), append: false);
			}
			try
			{
				RunCore(geometryName, tier, keyCount, line => { Console.WriteLine(line); tee?.WriteLine(line); });
			}
			finally
			{
				tee?.Dispose();
			}
		}

		private static void RunCore(string geometryName, string tier, int keyCount, Action<string> emit)
		{
			var geometry = geometryName switch
			{
				"u16K" => FdbLiteGeometry.Uniform(14),
				"u32K" => FdbLiteGeometry.Uniform(15),
				"u64K" => FdbLiteGeometry.Uniform(16),
				"b16K/p64K" or "split" => FdbLiteGeometry.Hypothesis,
				_ => throw new ArgumentException($"unknown geometry '{geometryName}'"),
			};

			string canonicalName = geometryName == "split" ? "b16K/p64K" : geometryName;
			string path;
			if (keyCount == FdbLiteFileStoreFixture.KeyCount)
			{ // the sliced default: share the matrix store (same seed, keys and values), no second ~1 GiB build
				path = FdbLiteFileStoreFixture.EnsureStore(canonicalName);
			}
			else
			{
				var dir = Path.Combine(Path.GetTempPath(), "fdblite-bench");
				Directory.CreateDirectory(dir);
				path = Path.Combine(dir, $"chunklog-{canonicalName.Replace('/', '-')}.sbkv");

				// build (or reuse) the custom-sized store
				bool build = true;
				if (File.Exists(path) && new FileInfo(path).Length > 0)
				{
					using var probe = BenchEngines.OpenFilePinned(path, geometry);
					build = probe.Durable.KeyCount != (ulong) keyCount;
				}
				if (build)
				{
					File.Delete(path);
					emit($"building {keyCount:N0}-key store at {geometryName}...");
					using var builder = BenchEngines.OpenFilePinned(path, geometry);
					var rndBuild = new Random(20260722);
					var val = new byte[1_000];
					var w = builder.BeginWrite();
					for (int i = 0; i < keyCount; i++)
					{
						rndBuild.NextBytes(val);
						w.Insert(TuPack.EncodeKey("D", i).GetBytes()!, val);
					}
					builder.Commit(w, 1);
				}
			}

			long datasetBytes = new FileInfo(path).Length;
			emit($"geometry={geometryName} tier={tier}");
			emit(MachineInfo.Describe(datasetBytes));

			if (tier == "cold")
			{
				var purge = OsCachePurge.Purge();
				emit($"cache purge: {purge}");
				if (!purge.Purged)
				{
					emit("### tier reported as RESTART (cache-hot), NOT COLD - rerun elevated for true cold numbers");
				}
			}

			using var engine = BenchEngines.OpenFilePinned(path, geometry);
			var pager = engine.Pager;
			uint root = engine.Durable.RootPageId;

			const int ChunkOps = 10_000;
			int chunks = 20;
			var rnd = new Random(4242);
			emit($"chunk (10k reads) | throughput (reads/s) | ns/read");

			double firstChunkRate = 0;
			for (int c = 0; c < chunks; c++)
			{
				var sw = Stopwatch.StartNew();
				long sum = 0;
				for (int i = 0; i < ChunkOps; i++)
				{
					if (FdbLiteTreeReader.TryGetValue(pager, root, TuPack.EncodeKey("D", rnd.Next(keyCount)).GetBytes()!, out var v)) { sum += v.Length; }
				}
				sw.Stop();
				double rate = ChunkOps / sw.Elapsed.TotalSeconds;
				double nsPer = sw.Elapsed.TotalMilliseconds * 1e6 / ChunkOps;
				if (c == 0) { firstChunkRate = rate; }
				string flag = c > 0 && rate < firstChunkRate * 0.5 ? "  <-- CLIFF (>2x slower than chunk 0: cache boundary or cold ramp)" : "";
				emit($"{c,3}               | {rate,18:N0} | {nsPer,8:N0}{flag} (checksum {sum & 0xFF})");
			}
		}

	}

}
