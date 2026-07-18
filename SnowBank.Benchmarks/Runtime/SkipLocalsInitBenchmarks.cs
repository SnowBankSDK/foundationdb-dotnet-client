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
	using BenchmarkDotNet.Attributes;

	/// <summary>Measures what <c>[SkipLocalsInit]</c> buys, and separates a FIXED-size <c>stackalloc</c> (what real SnowBank
	/// code uses, e.g. <c>stackalloc char[128]</c>: a fixed frame slot) from a VARIABLE-size one (a <c>localloc</c>, which
	/// must stack-probe when it is NOT zero-inited, so the zeroing otherwise doubles as the probe). Each method allocates a
	/// buffer, touches only the two ends (maximum benefit), and returns a checksum. Default job (not ShortRun) for low noise.</summary>
	[MemoryDiagnoser]
	public class SkipLocalsInitBenchmarks
	{

		// runtime values (never const-folded) so the stackalloc below becomes a variable-size localloc
		private int nSmall = 16;
		private int nLarge = 1024;

		// --- fixed-size stackalloc: a fixed frame slot, no localloc probe (representative of real code) ---

		[Benchmark(Baseline = true)] public int Fixed16_Zero() => Fixed16Z();
		[Benchmark] public int Fixed16_Skip() => Fixed16S();
		[Benchmark] public int Fixed1024_Zero() => Fixed1024Z();
		[Benchmark] public int Fixed1024_Skip() => Fixed1024S();

		// --- variable-size stackalloc: a localloc, which carries a stack-probe cost when zero-init is skipped ---

		[Benchmark] public int Var16_Zero() => VarZ(this.nSmall);
		[Benchmark] public int Var16_Skip() => VarS(this.nSmall);
		[Benchmark] public int Var1024_Zero() => VarZ(this.nLarge);
		[Benchmark] public int Var1024_Skip() => VarS(this.nLarge);

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int Fixed16Z() { Span<byte> b = stackalloc byte[16]; b[0] = 1; b[15] = 2; return b[0] + b[15]; }

		[SkipLocalsInit, MethodImpl(MethodImplOptions.NoInlining)]
		private static int Fixed16S() { Span<byte> b = stackalloc byte[16]; b[0] = 1; b[15] = 2; return b[0] + b[15]; }

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int Fixed1024Z() { Span<byte> b = stackalloc byte[1024]; b[0] = 1; b[1023] = 2; return b[0] + b[1023]; }

		[SkipLocalsInit, MethodImpl(MethodImplOptions.NoInlining)]
		private static int Fixed1024S() { Span<byte> b = stackalloc byte[1024]; b[0] = 1; b[1023] = 2; return b[0] + b[1023]; }

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int VarZ(int n) { Span<byte> b = stackalloc byte[n]; b[0] = 1; b[n - 1] = 2; return b[0] + b[n - 1]; }

		[SkipLocalsInit, MethodImpl(MethodImplOptions.NoInlining)]
		private static int VarS(int n) { Span<byte> b = stackalloc byte[n]; b[0] = 1; b[n - 1] = 2; return b[0] + b[n - 1]; }

	}
}
