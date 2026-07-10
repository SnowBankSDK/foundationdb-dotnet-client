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

namespace FoundationDB.Client.Tests
{
	using System.IO;
	using System.Runtime.CompilerServices;

	/// <summary>Locates, loads and saves the committed golden traces (<c>Scenarios/Goldens/&lt;scenario&gt;.json</c>).</summary>
	/// <remarks>
	/// <para>The record fixture writes goldens into the <b>source</b> tree (anchored on this file's compile-time path); the build copies them to the output folder, from which replay runs can also load them.</para>
	/// <para>Re-record policy: goldens are re-recorded when the corpus changes, and whenever the fdb server container image version bumps (that diff is reviewed as "server behavior changed").</para>
	/// </remarks>
	public static class ScenarioGoldens
	{

		/// <summary>The goldens folder in the source tree (compile-time anchor; exists on any machine that built this assembly from the repo).</summary>
		private static string SourceDirectory { get; } = ComputeSourceDirectory();

		private static string ComputeSourceDirectory([CallerFilePath] string sourcePath = "") => Path.Combine(Path.GetDirectoryName(sourcePath)!, "Goldens");

		/// <summary>The goldens folder copied next to the test assembly by the build.</summary>
		private static string OutputDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Scenarios", "Goldens");

		/// <summary>Path of a scenario's golden file in the source tree.</summary>
		public static string GetPath(string scenarioName) => Path.Combine(SourceDirectory, scenarioName + ".json");

		/// <summary>Loads the golden trace of a scenario, if one was recorded (source tree first, so a fresh record wins over a stale output copy).</summary>
		public static bool TryLoad(string scenarioName, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ScenarioTrace golden)
		{
			foreach (var dir in (ReadOnlySpan<string>) [ SourceDirectory, OutputDirectory ])
			{
				var path = Path.Combine(dir, scenarioName + ".json");
				if (File.Exists(path))
				{
					golden = ScenarioTrace.FromJsonText(File.ReadAllText(path));
					return true;
				}
			}
			golden = null;
			return false;
		}

		/// <summary>Writes (or refreshes) a golden trace in the source tree, and returns its path.</summary>
		public static string Save(ScenarioTrace trace)
		{
			Directory.CreateDirectory(SourceDirectory);
			var path = GetPath(trace.ScenarioName);
			File.WriteAllText(path, trace.ToJsonText() + Environment.NewLine);
			return path;
		}

	}

}
