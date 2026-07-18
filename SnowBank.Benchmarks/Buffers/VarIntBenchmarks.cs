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

namespace SnowBank.Benchmarks
{
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using BenchmarkDotNet.Attributes;
	using SnowBank.Buffers;

	/// <summary>Baselines the current <see cref="SliceReader.ReadVarInt32"/> against a single-bounds-check "fast path"
	/// candidate, across value size (1/3/5 bytes) and buffer position (a full 5-byte window ahead vs right at the end).
	/// The question: does the fast path help long values with room, and does it DE-OPT small values or values near the
	/// end of the buffer, where the window guard fails and it falls back anyway?</summary>
	[ShortRunJob]
	[MemoryDiagnoser]
	public class VarIntBenchmarks
	{

		/// <summary>Number of bytes the encoded varint occupies (1, 3 or 5).</summary>
		[Params(1, 3, 5)]
		public int Size;

		/// <summary>When true, the varint sits at the very end of the buffer (no 5-byte window ahead); when false, 8 padding bytes follow it.</summary>
		[Params(false, true)]
		public bool NearEnd;

		private byte[] Data = [ ];

		[GlobalSetup]
		public void Setup()
		{
			uint value = 1u << (7 * (this.Size - 1)); // smallest value that encodes to exactly Size bytes
			var writer = new SliceWriter(16);
			writer.WriteVarInt32(value);
			byte[] encoded = writer.ToSlice().ToArray();

			this.Data = this.NearEnd ? encoded : [ ..encoded, 0, 0, 0, 0, 0, 0, 0, 0 ];
		}

		[Benchmark(Baseline = true)]
		public uint Current()
		{
			var reader = new SliceReader(this.Data);
			return reader.ReadVarInt32();
		}

		[Benchmark]
		public uint FastPath()
		{
			int pos = 0;
			return ReadVarInt32FastPath(this.Data, ref pos);
		}

		/// <summary>Candidate: one bounds check for the 5-byte worst case, then an unrolled loop with no per-byte checks;
		/// falls back to a checked per-byte loop only within 5 bytes of the end.</summary>
		private static uint ReadVarInt32FastPath(byte[] buffer, ref int pos)
		{
			ref byte b0 = ref MemoryMarshal.GetArrayDataReference(buffer);
			int p = pos;
			int len = buffer.Length;
			ulong x = 0;

			if (len - p >= 5)
			{ // the 5-byte worst case fits: no per-byte bounds check
				for (int s = 0; s < 35; s += 7)
				{
					byte b = Unsafe.Add(ref b0, p++);
					x |= (b & 0x7FUL) << s;
					if (b < 0x80) { pos = p; return (uint) x; }
				}
				throw new FormatException("Malformed Varint");
			}

			for (int s = 0; p < len; s += 7)
			{ // near the end: check every byte
				byte b = Unsafe.Add(ref b0, p++);
				x |= (b & 0x7FUL) << s;
				if (b < 0x80) { pos = p; return (uint) x; }
			}
			throw new FormatException("Truncated Varint");
		}

	}
}
