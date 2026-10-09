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
	using Microsoft.Win32.SafeHandles;
	using SnowBank.Data.Tuples;

	/// <summary>Commit latency against the platform's flush primitive: the two-fsync durable commit, and the flush primitive alone.</summary>
	/// <remarks>DISK-TOUCHING (temp-directory files, real flushes): run it on the machine whose durability story is being measured. On Apple platforms the flush primitive is <c>F_FULLFSYNC</c> (a real media barrier, expensive and device-dependent), which is exactly what this measures - the number that decides whether a relaxed-durability mode is worth building.</remarks>
	[MemoryDiagnoser]
	[SimpleJob(warmupCount: 2, iterationCount: 6)]
	[JsonExporterAttribute.Brief]
	public class FdbLiteDurabilityBenchmarks
	{

		private FdbLiteEngine Engine = null!;

		private string StorePath = "";

		private string FlushPath = "";

		private SafeFileHandle FlushHandle = null!;

		private readonly byte[] FlushPayload = new byte[4096];

		private ulong NextVersion = 100;

		private int Sequence;

		[GlobalSetup]
		public void Setup()
		{
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-bench");
			Directory.CreateDirectory(dir);

			this.StorePath = Path.Combine(dir, $"durability-{Guid.NewGuid():N}.sbkv");
			this.Engine = BenchEngines.OpenFilePinned(this.StorePath, FdbLiteGeometry.Hypothesis);
			var writer = this.Engine.BeginWrite();
			writer.Insert("seed"u8, "x"u8);
			this.Engine.Commit(writer, 99);

			this.FlushPath = Path.Combine(dir, $"flush-{Guid.NewGuid():N}.bin");
			this.FlushHandle = File.OpenHandle(this.FlushPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, FileOptions.None);
		}

		[GlobalCleanup]
		public void Cleanup()
		{
			this.Engine.Dispose();
			this.FlushHandle.Dispose();
			try { File.Delete(this.StorePath); } catch { }
			try { File.Delete(this.FlushPath); } catch { }
		}

		/// <summary>One durable tiny commit: the COW page path, the fresh free-list chain, and BOTH flush barriers</summary>
		[Benchmark]
		public void DurableTinyCommit()
		{
			var writer = this.Engine.BeginWrite();
			writer.Insert(TuPack.EncodeKey("T", this.Sequence++).Span, "0123456789ABCDEF"u8);
			this.Engine.Commit(writer, this.NextVersion++);
		}

		/// <summary>The flush primitive alone (dirty one 4 KiB write, then RandomAccess.FlushToDisk): isolates the fsync / FlushFileBuffers / F_FULLFSYNC cost</summary>
		[Benchmark]
		public void FlushPrimitive()
		{
			this.FlushPayload[0]++;
			RandomAccess.Write(this.FlushHandle, this.FlushPayload, 0);
			RandomAccess.FlushToDisk(this.FlushHandle);
		}

	}

}
