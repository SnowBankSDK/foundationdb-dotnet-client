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

namespace SnowBank.Testing
{
	using System.ComponentModel;
	using System.IO;
	using System.Reflection;

	/// <summary>Legacy base class.</summary>
	[DebuggerNonUserCode]
	[Obsolete("Please derive from SimpleTest instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public abstract class DoxenseTest : SimpleTest
	{

		// this contains deprecated methods that must be kept around for a little bit more time...

		/// <summary>[DANGEROUS] Computes the path to a file that is located inside the test project</summary>
		/// <returns>Absolute path to the file</returns>
		[Obsolete("This may not be reliable! Please consider using MapPathRelativeToCallerSourcePath(...) instead!")]
		public string MapPathInSource(params string[] paths)
		{
			return MapPathRelativeToProject(paths.Length == 1 ? paths[0] : Path.Combine(paths), this.GetType().Assembly);
		}

		/// <summary>[TEST ONLY] Computes the path to a file inside the project currently tested</summary>
		/// <param name="relativePath">Relative path (ex: "Foo/bar.png")</param>
		/// <param name="assembly">Assembly that corresponds to the test project (if null, will use the calling assembly)</param>
		/// <returns><c>"C:\Path\To\Your\Solution_X\Project_Y\Foo\bar.png"</c></returns>
		/// <remarks>This function will remove the "\bin\Debug\..." bit from the assembly path.</remarks>
		[Obsolete("This may not be reliable! Please consider using MapPathRelativeToCallerSourcePath(...) instead!")]
		public string MapPathRelativeToProject(string relativePath, Assembly assembly)
		{
			Contract.NotNull(relativePath);
			Contract.NotNull(assembly);
			var res = Path.GetFullPath(Path.Combine(GetCurrentVsProjectPath(assembly), relativePath));
#if DEBUG
			Log($"# MapPathInSource(\"{relativePath}\", {assembly.GetName().Name}) => {res}");
#endif
			return res;
		}

		/// <summary>Attempts to compute the path to the root of the project currently tested</summary>
		[Obsolete("This may not be reliable! Please consider using MapPathRelativeToCallerSourcePath(...) instead!")]
		private static string GetCurrentVsProjectPath(Assembly assembly)
		{
			Contract.Debug.Requires(assembly != null);

			string path = GetAssemblyLocation(assembly);

			// If we are running in DEBUG from Visual Studio, the path will look like "X:\..\Solution\src\Project\bin\Debug" or "X:\..\Solution\src\Project\bin\net8.0\Debug\...",
			// The possible prefixes are usually "*\bin\Debug\" for AnyCPU, but also "*\bin\x86\Debug\" for x86

			int p = path.LastIndexOf(@"\bin\", StringComparison.Ordinal);
			if (p > 0)
			{
				path = path[..p];
			}

			// When running inside TeamCity, the path could also look like "X:\...\work\{guid}\Service\Binaries",
			if (path.Contains(@"\work\"))
			{ // TeamCity ?
				p = path.LastIndexOf(@"\Binaries", StringComparison.Ordinal);
				if (p > 0) return path[..p];
			}

			return path;
		}

	}

}
