#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

// Static BCL types that are entirely absent from netstandard2.0. Declared here in their real namespaces
// so the existing `using` directives in the shared sources resolve them. netstandard2.0 build only.

#if NETSTANDARD2_0

namespace System.Text
{
	using System;

	/// <summary>Minimal netstandard2.0 backport of <c>System.Text.Ascii</c> (subset used by the SDK).</summary>
	/// <remarks>Scalar byte-by-byte loops, versus the SIMD-vectorized BCL implementation: same results, slower on large
	/// inputs. No allocations.</remarks>
	public static class Ascii
	{

		public static bool IsValid(byte value) => value <= 0x7F;

		public static bool IsValid(char value) => value <= 0x7F;

		public static bool IsValid(ReadOnlySpan<byte> value)
		{
			foreach (var b in value)
			{
				if (b > 0x7F) return false;
			}
			return true;
		}

		public static bool IsValid(ReadOnlySpan<char> value)
		{
			foreach (var c in value)
			{
				if (c > 0x7F) return false;
			}
			return true;
		}

		/// <summary>Compares two ASCII buffers for exact (case-sensitive) equality.</summary>
		public static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
			=> left.SequenceEqual(right);

		/// <summary>Compares an ASCII byte buffer with a UTF-16 buffer for exact (case-sensitive) equality.</summary>
		public static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<char> right)
		{
			if (left.Length != right.Length) return false;
			for (int i = 0; i < left.Length; i++)
			{
				if (left[i] != right[i]) return false;
			}
			return true;
		}

	}

}

namespace System.Numerics
{

	/// <summary>Minimal netstandard2.0 backport of <c>System.Numerics.BitOperations</c> (subset used by the SDK).</summary>
	public static class BitOperations
	{

		public static bool IsPow2(int value) => (value & (value - 1)) == 0 && value > 0;

		public static bool IsPow2(uint value) => (value & (value - 1)) == 0 && value != 0;

		public static bool IsPow2(long value) => (value & (value - 1)) == 0 && value > 0;

		public static bool IsPow2(ulong value) => (value & (value - 1)) == 0 && value != 0;

		/// <summary>Returns the integer (floor) base-2 logarithm; matches the BCL contract of returning 0 for an input of 0.</summary>
		public static int Log2(uint value)
		{
			int r = 0;
			while ((value >>= 1) != 0) r++;
			return r;
		}

		public static int Log2(ulong value)
		{
			int r = 0;
			while ((value >>= 1) != 0) r++;
			return r;
		}

		public static uint RoundUpToPowerOf2(uint value)
		{
			if (value <= 1) return 1;
			value--;
			value |= value >> 1;
			value |= value >> 2;
			value |= value >> 4;
			value |= value >> 8;
			value |= value >> 16;
			return value + 1;
		}

		public static ulong RoundUpToPowerOf2(ulong value)
		{
			if (value <= 1) return 1;
			value--;
			value |= value >> 1;
			value |= value >> 2;
			value |= value >> 4;
			value |= value >> 8;
			value |= value >> 16;
			value |= value >> 32;
			return value + 1;
		}

		/// <summary>Returns the population count (number of bits set) of a mask (software fallback, no POPCNT intrinsic).</summary>
		public static int PopCount(uint value)
		{
			value -= (value >> 1) & 0x55555555u;
			value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
			value = (((value + (value >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24;
			return (int) value;
		}

		/// <summary>Returns the population count (number of bits set) of a mask (software fallback, no POPCNT intrinsic).</summary>
		public static int PopCount(ulong value)
		{
			value -= (value >> 1) & 0x5555555555555555ul;
			value = (value & 0x3333333333333333ul) + ((value >> 2) & 0x3333333333333333ul);
			value = (((value + (value >> 4)) & 0x0F0F0F0F0F0F0F0Ful) * 0x0101010101010101ul) >> 56;
			return (int) value;
		}

		public static int TrailingZeroCount(uint value)
		{
			if (value == 0) return 32;
			int c = 0;
			while ((value & 1) == 0) { c++; value >>= 1; }
			return c;
		}

		public static int TrailingZeroCount(int value) => TrailingZeroCount((uint) value);

		public static int TrailingZeroCount(ulong value)
		{
			if (value == 0) return 64;
			int c = 0;
			while ((value & 1) == 0) { c++; value >>= 1; }
			return c;
		}

	}

}

namespace System.Security.Cryptography
{
	using System;

	/// <summary>Minimal netstandard2.0 backport of <c>System.Security.Cryptography.CryptographicOperations</c> (subset used by the SDK).</summary>
	public static class CryptographicOperations
	{

		//REVIEW: the real CryptographicOperations.ZeroMemory guarantees the zeroing is NOT elided by the JIT (it is a
		// security primitive: sensitive bytes must actually be wiped). Span.Clear() carries no such guarantee — in principle
		// the optimizer could remove it if it proves the buffer is never read again. In practice Clear() is not currently
		// elided on the .NET Framework JIT, and our callers (wiping pooled buffers before returning them) still benefit,
		// but this is weaker than the modern contract. Revisit if a hardened non-elidable wipe is ever required here.
		public static void ZeroMemory(Span<byte> buffer) => buffer.Clear();

	}

}

namespace System.Collections.Generic
{
	/// <summary>Backport of the non-generic <c>KeyValuePair</c> factory class (.NET Core 2.0+).</summary>
	public static class KeyValuePair
	{
		/// <summary>Creates a new key/value pair instance using provided values.</summary>
		public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value) => new(key, value);
	}
}

namespace System.Text.Unicode
{
	using System;
	using System.Buffers;
	using System.Text;

	/// <summary>Minimal netstandard2.0 backport of <c>System.Text.Unicode.Utf8</c> (subset used by the SDK).</summary>
	/// <remarks>Bridges through <see cref="Encoding.UTF8"/> and an intermediate array (the modern version transcodes
	/// straight into the destination span). Invalid UTF-16 is replaced with U+FFFD by the UTF8 encoder's replacement
	/// fallback, matching <c>replaceInvalidSequences: true</c>; the <c>isFinalBlock: false</c> streaming mode is not
	/// modeled (this always behaves as a final block).</remarks>
	public static class Utf8
	{

		public static OperationStatus FromUtf16(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
		{
			byte[] encoded = Encoding.UTF8.GetBytes(source.ToArray());
			if (encoded.Length > destination.Length)
			{
				charsRead = 0;
				bytesWritten = 0;
				return OperationStatus.DestinationTooSmall;
			}
			encoded.CopyTo(destination);
			charsRead = source.Length;
			bytesWritten = encoded.Length;
			return OperationStatus.Done;
		}

	}

}

#endif
