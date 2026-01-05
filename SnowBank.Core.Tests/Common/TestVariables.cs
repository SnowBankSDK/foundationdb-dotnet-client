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

// ReSharper disable InconsistentNaming

namespace SnowBank.Testing
{
	using JetBrains.Annotations;

	/// <summary>Various keys and values frequently used in unit tests.</summary>
	/// <remarks>This class is best used via a "using static" statement</remarks>
	[DebuggerStepThrough]
	public static class TestVariables
	{

		public static readonly Slice AAA = Slice.FromByteString("AAA");
		public static readonly Slice BBB = Slice.FromByteString("BBB");
		public static readonly Slice CCC = Slice.FromByteString("CCC");
		public static readonly Slice DDD = Slice.FromByteString("DDD");
		public static readonly Slice EEE = Slice.FromByteString("EEE");
		public static readonly Slice FFF = Slice.FromByteString("FFF");
		public static readonly Slice GGG = Slice.FromByteString("GGG");
		public static readonly Slice HHH = Slice.FromByteString("HHH");
		public static readonly Slice III = Slice.FromByteString("III");
		public static readonly Slice ZZZ = Slice.FromByteString("ZZZ");

		/// <summary>Helper function to simplify creating key/value pairs in unit tests</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (Slice Key, Slice Value) KV(Slice key, Slice value)
		{
			return (key, value);
		}

		/// <summary>Helper function to simplify creating batches of key/value pairs in unit tests</summary>
		[Pure]
		public static (Slice Key, Slice Value)[] Batch(params (Slice, Slice)[] items)
		{
			// caller can write "db.DirectSet(pair0, pair1, ... pairN-1)"
			return items;
		}

		/// <summary>Return a key range</summary>
		[Pure]
		public static (Slice Begin, Slice End) Range(Slice begin, Slice end)
		{
			return (begin, end);
		}

		[Pure]
		public static (Slice Begin, Slice End) Range(string begin, string end)
		{
			return (Key(begin), Key(end));
		}

		/// <summary>Create a key from a byte string</summary>
		[Pure]
		public static Slice Key(string byteString)
		{
			return Slice.FromByteString(byteString);
		}

		/// <summary>Create a key from a formatted number</summary>
		[Pure]
		public static Slice Key(int number, string? format = null)
		{
			return Slice.FromByteString(number.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Create a variable-length key from a compacted number that preserves ordering</summary>
		[Pure]
		public static Slice KeyCounter(ulong number)
		{
			// Slice will start with the number of bytes required to store the number, followed by the bytes themselves (in big-endian)
			// 0x42 => 01 42
			// 0x1234 => 02 12 34
			// 0xDEADBEEF => 04 DE AD BE EF
			var num = Number(number);
			return Slice.FromByte((byte)num.Count) + num;
		}

		/// <summary>Create a variable-length key from a compacted number that preserves ordering</summary>
		[Pure]
		public static Slice KeyCounter(long number)
		{
			// Slice will start with the number of bytes required to store the number, followed by the bytes themselves (in big-endian)
			// 0x42 => 01 42
			// 0x1234 => 02 12 34
			// 0xDEADBEEF => 04 DE AD BE EF
			var num = Number(number);
			return Slice.FromByte((byte)num.Count) + num;
		}

		/// <summary>Create a 32-bit key using Big-Endian encoding</summary>
		[Pure]
		public static Slice Key32(int value)
		{
			return Slice.FromFixed32BE(value);
		}

		/// <summary>Create a 32-bit key using Big-Endian encoding</summary>
		[Pure]
		public static Slice Key32(uint value)
		{
			return Slice.FromFixedU32BE(value);
		}

		/// <summary>Create a 64-bit key using Big-Endian encoding</summary>
		[Pure]
		public static Slice Key64(long value)
		{
			return Slice.FromFixed64BE(value);
		}

		/// <summary>Create a 64-bit key using Big-Endian encoding</summary>
		[Pure]
		public static Slice Key64(ulong value)
		{
			return Slice.FromFixedU64BE(value);
		}

		/// <summary>Create a value from a unicode string</summary>
		[Pure]
		public static Slice Value(string text)
		{
			return Slice.FromString(text);
		}

		/// <summary>Create a value from a 128-bit GUID</summary>
		[Pure]
		public static Slice Value(Guid value)
		{
			return Slice.FromGuid(value);
		}

		/// <summary>Create a variable-length key using Big-Endian encoding</summary>
		public static Slice Number(int value)
		{
			return Slice.FromInt32BE(value);
		}

		/// <summary>Create a variable-length key using Big-Endian encoding</summary>
		public static Slice Number(uint value)
		{
			return Slice.FromUInt32BE(value);
		}

		/// <summary>Create a variable-length key using Big-Endian encoding</summary>
		public static Slice Number(long value)
		{
			return Slice.FromInt64BE(value);
		}

		/// <summary>Create a variable-length key using Big-Endian encoding</summary>
		public static Slice Number(ulong value)
		{
			return Slice.FromUInt64BE(value);
		}

		/// <summary>Create a 32-bit value using Little-Endian encoding</summary>
		public static Slice Counter32(int value)
		{
			return Slice.FromFixed32(value);
		}

		/// <summary>Create a 32-bit value using Little-Endian encoding</summary>
		public static Slice Counter32(uint value)
		{
			return Slice.FromFixedU32(value);
		}

		/// <summary>Create a 64-bit value using Little-Endian encoding</summary>
		public static Slice Counter64(long value)
		{
			return Slice.FromFixed64(value);
		}

		/// <summary>Create a 64-bit value using Little-Endian encoding</summary>
		public static Slice Counter64(ulong value)
		{
			return Slice.FromFixedU64(value);
		}

		public static Slice PadRight(Slice value, int size, byte pad = 0)
		{
			if (value.Count >= size) return value;
			var tmp = Slice.Repeat(pad, size);
			value.CopyTo(tmp.Array, tmp.Offset);
			return tmp;
		}

		public static Slice PadLeft(Slice value, int size, byte pad = 0)
		{
			if (value.Count >= size) return value;
			var tmp = Slice.Repeat(pad, size);
			value.CopyTo(tmp.Array, tmp.Offset + size - value.Count);
			return tmp;
		}

	}

}
