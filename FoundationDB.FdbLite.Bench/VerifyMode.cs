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

namespace FoundationDB.FdbLite.Bench
{

	/// <summary>The correctness pass: every engine replays every workload next to the <see cref="ReferenceModel"/>, and four checks per (workload, engine) compare the two.</summary>
	/// <remarks>
	/// <list type="number">
	/// <item>the read checksum of the timed steps equals the model's;</item>
	/// <item>a full scan of the store equals the model's content, pair by pair;</item>
	/// <item>the tree audit of the committed generation reports no problem (FdbLite engines);</item>
	/// <item>after a close and a reopen from disk, the full scan still equals the model (file engines).</item>
	/// </list>
	/// <para>Exit code 1 when any check fails or an engine throws, 0 otherwise.</para>
	/// </remarks>
	public static class VerifyMode
	{

		public static int Run(BenchOptions options)
		{
			var loads = options.Loads();
			options.PrintHeader(loads.Count);
			int failures = 0, checks = 0;
			var sw = Stopwatch.StartNew();
			foreach (var load in loads)
			{
				var line = new System.Text.StringBuilder();
				line.Append($"== {load.Name,-30}");
				var problems = new List<string>();

				using var model = new ReferenceModel();
				Replay.Run(model, load.Prepare, out _);
				Replay.Run(model, load.Steps, out long expectedChecksum);
				var expected = model.Scan(Slice.Empty, Slice.Nil, 0);

				foreach (var engine in options.Engines)
				{
					int ok = 0, bad = 0;
					try
					{
						using var d = options.Open(engine, load.Name);
						Replay.Run(d, load.Prepare, out _);
						Replay.Run(d, load.Steps, out long checksum);

						// 1. read checksum
						if (checksum == expectedChecksum) ok++;
						else { bad++; problems.Add($"{engine}: read checksum {checksum:N0}, model {expectedChecksum:N0}"); }

						// 2. full scan against the model
						if (Compare(engine, "scan", d.Scan(Slice.Empty, Slice.Nil, 0), expected, problems)) ok++; else bad++;

						// 3. tree audit
						var audit = d.Audit();
						if (audit is not null)
						{
							if (audit.Count == 0) ok++;
							else { bad++; problems.Add($"{engine}: audit: {string.Join("; ", audit.Take(5))}"); }
						}

						// 4. reopen from disk and scan again
						if (d.Reopen())
						{
							if (Compare(engine, "scan after reopen", d.Scan(Slice.Empty, Slice.Nil, 0), expected, problems)) ok++; else bad++;
						}
					}
					catch (Exception e)
					{
						bad++;
						problems.Add($"{engine}: {e.GetType().Name}: {e.Message}");
					}
					checks += ok + bad;
					failures += bad;
					line.Append($"  {engine} {(bad == 0 ? "ok" : "FAIL")}({ok}/{ok + bad})");
				}
				Console.WriteLine(line);
				foreach (var p in problems) Console.WriteLine($"   !! {p}");
			}
			Console.WriteLine();
			Console.WriteLine(failures == 0
				? $"VERIFY OK: {checks} checks over {loads.Count} workloads x {options.Engines.Length} engines in {sw.Elapsed.TotalSeconds:N1} s"
				: $"VERIFY FAILED: {failures} of {checks} checks over {loads.Count} workloads x {options.Engines.Length} engines in {sw.Elapsed.TotalSeconds:N1} s");
			return failures == 0 ? 0 : 1;
		}

		/// <summary>Compares two scans pair by pair. The first 3 differences are reported, with the position.</summary>
		private static bool Compare(string engine, string what, List<KeyValuePair<Slice, Slice>> actual, List<KeyValuePair<Slice, Slice>> expected, List<string> problems)
		{
			if (actual.Count != expected.Count)
			{
				problems.Add($"{engine}: {what}: {actual.Count:N0} pairs, model {expected.Count:N0}");
				return false;
			}
			int bad = 0;
			for (int i = 0; i < expected.Count && bad < 3; i++)
			{
				if (!actual[i].Key.Equals(expected[i].Key))
				{
					problems.Add($"{engine}: {what}: key at {i:N0} is {actual[i].Key:K}, model {expected[i].Key:K}");
					bad++;
				}
				else if (!actual[i].Value.Equals(expected[i].Value))
				{
					problems.Add($"{engine}: {what}: value of {expected[i].Key:K} is {actual[i].Value.Count} bytes, model {expected[i].Value.Count} bytes");
					bad++;
				}
			}
			return bad == 0;
		}

	}

}
