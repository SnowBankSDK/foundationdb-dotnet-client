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
	using System.Runtime.InteropServices;
	using FoundationDB.Client.Native;

	/// <summary>The default native library lookup checks fixed paths only</summary>
	[TestFixture]
	[Category("Fdb-Client-InProc")]
	public sealed class FdbNativeLibraryFacts
	{

		[Test]
		public void Default_Candidates_On_Windows()
		{
			var (fileName, rid, candidates) = FdbNative.GetDefaultNativeLibraryCandidates(@"C:\App", OSPlatform.Windows, Architecture.X64, @"C:\Program Files");
			Assert.That(fileName, Is.EqualTo("fdb_c.dll"));
			Assert.That(rid, Is.EqualTo("win-x64"));
			Assert.That(candidates, Is.EqualTo(new[]
			{
				Path.Combine(@"C:\App", "runtimes", "win-x64", "native", "fdb_c.dll"),
				Path.Combine(@"C:\App", "fdb_c.dll"),
				Path.Combine(@"C:\Program Files", "foundationdb", "bin", "fdb_c.dll"),
			}));
		}

		[Test]
		public void Default_Candidates_On_Linux([Values(Architecture.X64, Architecture.Arm64)] Architecture arch)
		{
			var (fileName, rid, candidates) = FdbNative.GetDefaultNativeLibraryCandidates("/app", OSPlatform.Linux, arch, "");
			string expectedRid = arch == Architecture.X64 ? "linux-x64" : "linux-arm64";
			Assert.That(fileName, Is.EqualTo("libfdb_c.so"));
			Assert.That(rid, Is.EqualTo(expectedRid));
			Assert.That(candidates, Is.EqualTo(new[]
			{
				Path.Combine("/app", "runtimes", expectedRid, "native", "libfdb_c.so"),
				Path.Combine("/app", "libfdb_c.so"),
				"/usr/lib/libfdb_c.so",
			}));
		}

		[Test]
		public void Default_Candidates_On_MacOS([Values(Architecture.X64, Architecture.Arm64)] Architecture arch)
		{
			var (fileName, rid, candidates) = FdbNative.GetDefaultNativeLibraryCandidates("/app", OSPlatform.OSX, arch, "");
			string expectedRid = arch == Architecture.X64 ? "osx-x64" : "osx-arm64";
			Assert.That(fileName, Is.EqualTo("libfdb_c.dylib"));
			Assert.That(rid, Is.EqualTo(expectedRid));
			Assert.That(candidates, Is.EqualTo(new[]
			{
				Path.Combine("/app", "runtimes", expectedRid, "native", "libfdb_c.dylib"),
				Path.Combine("/app", "libfdb_c.dylib"),
				"/usr/local/lib/libfdb_c.dylib",
			}));
		}

		[Test]
		public void Default_Candidates_Reject_Platforms_Without_A_Client()
		{
			// no Windows build for Arm64, no 32-bit client, no client for other operating systems
			Assert.That(() => FdbNative.GetDefaultNativeLibraryCandidates(@"C:\App", OSPlatform.Windows, Architecture.Arm64, @"C:\Program Files"), Throws.TypeOf<PlatformNotSupportedException>());
			Assert.That(() => FdbNative.GetDefaultNativeLibraryCandidates("/app", OSPlatform.Linux, Architecture.X86, ""), Throws.TypeOf<PlatformNotSupportedException>());
			Assert.That(() => FdbNative.GetDefaultNativeLibraryCandidates("/app", OSPlatform.Create("FREEBSD"), Architecture.X64, ""), Throws.TypeOf<PlatformNotSupportedException>());
		}

	}

}
