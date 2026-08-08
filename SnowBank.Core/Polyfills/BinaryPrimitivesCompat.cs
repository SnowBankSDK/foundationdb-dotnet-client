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

// netstandard2.0's BinaryPrimitives lacks the Single/Double overloads (added in .NET 5). The netstandard2.0 build redirects
// the BinaryPrimitives name to this shim via a file-local alias in the sources that need float/double I/O;
// everything else delegates to the real implementation.
//
// The missing overloads are implemented by reinterpreting the IEEE bits over the integer overloads (Unsafe.As for
// float, BitConverter.DoubleToInt64Bits/Int64BitsToDouble for double), bit-exact with the modern BCL on every
// input including NaN payloads, no allocation. Internal: plumbing for the sources only (Compat-branded name
// must never appear in application code, per the public-shim policy).

#if NETSTANDARD2_0

namespace SnowBank.Compat
{
	using System;
	using System.Runtime.CompilerServices;
	using BclBinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

	internal static class BinaryPrimitivesCompat
	{

		// --- delegated to the real BCL implementation (present on netstandard2.0) ---

		public static short ReadInt16LittleEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadInt16LittleEndian(source);
		public static short ReadInt16BigEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadInt16BigEndian(source);
		public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadUInt16LittleEndian(source);
		public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadUInt16BigEndian(source);
		public static int ReadInt32LittleEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadInt32LittleEndian(source);
		public static int ReadInt32BigEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadInt32BigEndian(source);
		public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadUInt32LittleEndian(source);
		public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadUInt32BigEndian(source);
		public static long ReadInt64LittleEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadInt64LittleEndian(source);
		public static long ReadInt64BigEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadInt64BigEndian(source);
		public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadUInt64LittleEndian(source);
		public static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> source) => BclBinaryPrimitives.ReadUInt64BigEndian(source);

		public static void WriteInt16LittleEndian(Span<byte> destination, short value) => BclBinaryPrimitives.WriteInt16LittleEndian(destination, value);
		public static void WriteInt16BigEndian(Span<byte> destination, short value) => BclBinaryPrimitives.WriteInt16BigEndian(destination, value);
		public static void WriteUInt16LittleEndian(Span<byte> destination, ushort value) => BclBinaryPrimitives.WriteUInt16LittleEndian(destination, value);
		public static void WriteUInt16BigEndian(Span<byte> destination, ushort value) => BclBinaryPrimitives.WriteUInt16BigEndian(destination, value);
		public static void WriteInt32LittleEndian(Span<byte> destination, int value) => BclBinaryPrimitives.WriteInt32LittleEndian(destination, value);
		public static void WriteInt32BigEndian(Span<byte> destination, int value) => BclBinaryPrimitives.WriteInt32BigEndian(destination, value);
		public static void WriteUInt32LittleEndian(Span<byte> destination, uint value) => BclBinaryPrimitives.WriteUInt32LittleEndian(destination, value);
		public static void WriteUInt32BigEndian(Span<byte> destination, uint value) => BclBinaryPrimitives.WriteUInt32BigEndian(destination, value);
		public static void WriteInt64LittleEndian(Span<byte> destination, long value) => BclBinaryPrimitives.WriteInt64LittleEndian(destination, value);
		public static void WriteInt64BigEndian(Span<byte> destination, long value) => BclBinaryPrimitives.WriteInt64BigEndian(destination, value);
		public static void WriteUInt64LittleEndian(Span<byte> destination, ulong value) => BclBinaryPrimitives.WriteUInt64LittleEndian(destination, value);
		public static void WriteUInt64BigEndian(Span<byte> destination, ulong value) => BclBinaryPrimitives.WriteUInt64BigEndian(destination, value);

		public static bool TryWriteUInt64BigEndian(Span<byte> destination, ulong value) => BclBinaryPrimitives.TryWriteUInt64BigEndian(destination, value);

		public static short ReverseEndianness(short value) => BclBinaryPrimitives.ReverseEndianness(value);
		public static ushort ReverseEndianness(ushort value) => BclBinaryPrimitives.ReverseEndianness(value);
		public static int ReverseEndianness(int value) => BclBinaryPrimitives.ReverseEndianness(value);
		public static uint ReverseEndianness(uint value) => BclBinaryPrimitives.ReverseEndianness(value);
		public static long ReverseEndianness(long value) => BclBinaryPrimitives.ReverseEndianness(value);
		public static ulong ReverseEndianness(ulong value) => BclBinaryPrimitives.ReverseEndianness(value);

		// --- missing from netstandard2.0: Single/Double overloads (added in .NET 5) ---

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ReadSingleLittleEndian(ReadOnlySpan<byte> source)
		{
			int bits = BclBinaryPrimitives.ReadInt32LittleEndian(source);
			return Unsafe.As<int, float>(ref bits);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ReadSingleBigEndian(ReadOnlySpan<byte> source)
		{
			int bits = BclBinaryPrimitives.ReadInt32BigEndian(source);
			return Unsafe.As<int, float>(ref bits);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ReadDoubleLittleEndian(ReadOnlySpan<byte> source)
			=> BitConverter.Int64BitsToDouble(BclBinaryPrimitives.ReadInt64LittleEndian(source));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
			=> BitConverter.Int64BitsToDouble(BclBinaryPrimitives.ReadInt64BigEndian(source));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteSingleLittleEndian(Span<byte> destination, float value)
			=> BclBinaryPrimitives.WriteInt32LittleEndian(destination, Unsafe.As<float, int>(ref value));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteSingleBigEndian(Span<byte> destination, float value)
			=> BclBinaryPrimitives.WriteInt32BigEndian(destination, Unsafe.As<float, int>(ref value));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDoubleLittleEndian(Span<byte> destination, double value)
			=> BclBinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDoubleBigEndian(Span<byte> destination, double value)
			=> BclBinaryPrimitives.WriteInt64BigEndian(destination, BitConverter.DoubleToInt64Bits(value));

	}

}

#endif
