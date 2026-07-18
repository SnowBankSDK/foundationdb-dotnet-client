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
	using System.Text;
	using BenchmarkDotNet.Attributes;
	using SnowBank.Text;

	/// <summary>Baselines the CrystalJson string interner on its hot case: interning a repeated field name (a cache hit,
	/// the norm when parsing many objects with the same keys) against just decoding the bytes to a fresh string. Answers
	/// whether the current lossy L1/L2 + FNV interner is a real per-call cost (a perf reason to redesign) or already cheap
	/// and allocation-free on the hot path (so the redesign would be about elegance, not speed).</summary>
	[MemoryDiagnoser]
	public class StringTableBenchmarks
	{

		private static readonly byte[] FieldName = "description"u8.ToArray();

		private StringTable Table { get; set; } = null!;

		[GlobalSetup]
		public void Setup()
		{
			this.Table = StringTable.GetInstance();
			_ = this.Table.Add(FieldName); // prime the cache so the measured Add() is a hit
		}

		[Benchmark(Baseline = true)]
		public string Intern_Hit() => this.Table.Add(FieldName);

		[Benchmark]
		public string Decode_NoIntern() => Encoding.UTF8.GetString(FieldName);

	}
}
