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
	using BenchmarkDotNet.Running;

	public static class Program
	{
		// Run all: `dotnet run -c Release -- --filter *`
		// Run one: `dotnet run -c Release -- --filter *Durability*`
		// Slice the file-store series: `dotnet run -c Release -- --filter "*FileStoreBenchmarks*" --tier=cold --geometry=u32K` (recipes in README.md)
		// Check whether the OS cache purge (cold-tier prerequisite) works on this machine: `dotnet run -c Release -- --purgecheck`
		public static void Main(string[] args)
		{
			if (args is [ "--purgecheck", .. ])
			{
				Console.WriteLine("OS cache purge: " + OsCachePurge.Purge());
				return;
			}
			if (args is [ "--chunklog", var geometry, var tier, .. ])
			{ // cache-cliff detector: per-10k-op throughput over a big file store, cold or restart
				int keyCount = args.Length > 3 && int.TryParse(args[3], out var kc) ? kc : 1_000_000;
				FileStoreChunkLog.Run(geometry, tier, keyCount);
				return;
			}

			// slice selectors (--geometry= / --tier=) are ours, not BenchmarkDotNet's: consume them before the switcher sees the args
			var remaining = new List<string>(args.Length);
			foreach (var arg in args)
			{
				if (arg.StartsWith("--geometry=", StringComparison.OrdinalIgnoreCase)) { BenchSlice.Geometry = arg["--geometry=".Length..]; }
				else if (arg.StartsWith("--tier=", StringComparison.OrdinalIgnoreCase)) { BenchSlice.Tier = arg["--tier=".Length..]; }
				else { remaining.Add(arg); }
			}

			var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(remaining.ToArray());

			// a fully-specified cold/restart slice always emits its cache-cliff chunk log into the artifacts,
			// so the per-cell cliff evidence travels with the reports instead of depending on a separate manual run
			// (skipped in list mode, where the switcher still returns summaries without running anything)
			bool listOnly = remaining.Any(a => a.StartsWith("--list", StringComparison.OrdinalIgnoreCase));
			if (!listOnly && summaries.Any() && BenchSlice.Geometry is { } g && BenchSlice.Tier is { } t && (t is "cold" or "restart"))
			{
				FileStoreChunkLog.Run(g, t, FdbLiteFileStoreFixture.KeyCount, teeToArtifacts: true);
			}
		}
	}
}
