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
	using System.Collections.Generic;
	using System.Text;
	using BenchmarkDotNet.Attributes;
	using SnowBank.Text;

	/// <summary>Compares the current CrystalJson interner (lossy L1/L2 + FNV) on its hot cache-hit path against modern
	/// <c>Dictionary.AlternateLookup</c> replacements (probe the map straight from a span, no lossy double-probe), to size
	/// the perf ceiling of a redesign. A byte-keyed lookup is the apples-to-apples equivalent (the parser has UTF-8 bytes);
	/// a char-keyed lookup with the BCL's <see cref="StringComparer.Ordinal"/> is the upper bound (SIMD string hashing).
	/// All measure a hit for a repeated field name; a plain decode is the "no interning" reference.</summary>
	[MemoryDiagnoser]
	public class StringTableBenchmarks
	{

		private const string Field = "description";
		private static readonly byte[] FieldUtf8 = Encoding.UTF8.GetBytes(Field);

		private StringTable Table { get; set; } = null!;

		private Dictionary<string, string>.AlternateLookup<ReadOnlySpan<byte>> ByteLookup;
		private Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> CharLookup;

		[GlobalSetup]
		public void Setup()
		{
			this.Table = StringTable.GetInstance();
			_ = this.Table.Add(FieldUtf8); // prime: the measured Add() is a hit

			var byteDict = new Dictionary<string, string>(Utf8OrdinalComparer.Instance) { [Field] = Field };
			this.ByteLookup = byteDict.GetAlternateLookup<ReadOnlySpan<byte>>();

			var charDict = new Dictionary<string, string>(StringComparer.Ordinal) { [Field] = Field };
			this.CharLookup = charDict.GetAlternateLookup<ReadOnlySpan<char>>();
		}

		[Benchmark(Baseline = true)]
		public string Current_Intern_Hit() => this.Table.Add(FieldUtf8);

		[Benchmark]
		public string? AltLookup_Bytes_Hit() => this.ByteLookup.TryGetValue(FieldUtf8, out var s) ? s : null;

		[Benchmark]
		public string? AltLookup_Chars_Hit() => this.CharLookup.TryGetValue(Field.AsSpan(), out var s) ? s : null;

		/// <summary>The realistic redesign path: the parser has UTF-8 bytes, so transcode to a stack char buffer and then use the
		/// BCL char lookup. This is the end-to-end cost that must beat <see cref="Current_Intern_Hit"/> for the redesign to win on speed.</summary>
		[Benchmark]
		public string? Transcode_Then_CharLookup_Hit()
		{
			Span<char> chars = stackalloc char[FieldUtf8.Length]; // char count <= byte count for UTF-8
			int n = Encoding.UTF8.GetChars(FieldUtf8, chars);
			return this.CharLookup.TryGetValue(chars[..n], out var s) ? s : null;
		}

		[Benchmark]
		public string Decode_NoIntern() => Encoding.UTF8.GetString(FieldUtf8);

		/// <summary>Ordinal string comparer that can also probe/insert from a UTF-8 <see cref="ReadOnlySpan{Byte}"/> (ASCII field
		/// names, the common case). Both hash paths feed the same bytes into <see cref="HashCode"/> so span-hash == string-hash.</summary>
		private sealed class Utf8OrdinalComparer : IEqualityComparer<string>, IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
		{
			public static readonly Utf8OrdinalComparer Instance = new();

			public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

			public int GetHashCode(string obj)
			{
				Span<byte> bytes = obj.Length <= 256 ? stackalloc byte[obj.Length] : new byte[obj.Length];
				for (int i = 0; i < obj.Length; i++) bytes[i] = (byte) obj[i]; // ASCII
				var hc = new HashCode();
				hc.AddBytes(bytes);
				return hc.ToHashCode();
			}

			public bool Equals(ReadOnlySpan<byte> alternate, string other)
			{
				if (alternate.Length != other.Length) return false;
				for (int i = 0; i < alternate.Length; i++)
				{
					if (alternate[i] != (byte) other[i]) return false;
				}
				return true;
			}

			public int GetHashCode(ReadOnlySpan<byte> alternate)
			{
				var hc = new HashCode();
				hc.AddBytes(alternate);
				return hc.ToHashCode();
			}

			public string Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);
		}

	}
}
