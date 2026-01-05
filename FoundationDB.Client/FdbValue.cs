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

namespace FoundationDB.Client
{
	using System;

	/// <summary>Factory class for values</summary>
	[PublicAPI]
	public static class FdbValue
	{

		public const int MaxSize = Fdb.MaxValueSize;

		public static readonly Slice Empty = Slice.Empty;

		#region Generic...

		/// <summary>Returns a value that encodes an instance of <typeparamref name="TValue"/> with the given encoder</summary>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <typeparam name="TEncoder">Type of the encoder, that implements <see cref="ISpanEncoder{TValue}"/></typeparam>
		/// <param name="value">Value to encode</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<TValue, TEncoder> Create<TValue, TEncoder>(TValue value)
			where TEncoder : struct, ISpanEncoder<TValue>
		{
			return new(value);
		}

		#endregion

		#region Binary...

		/// <summary>Returns a value that wraps a <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue ToBytes(Slice value) => new(value, FdbValueTypeHint.Binary);

		/// <summary>Returns a value that wraps a byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue ToBytes(byte[] value) => new(value.AsSlice(), FdbValueTypeHint.Binary);

		/// <summary>Returns a value that wraps a byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue ToBytes(byte[] value, int start, int length) => new(value.AsSlice(start, length), FdbValueTypeHint.Binary);

		/// <summary>Returns a value that wraps a memory region</summary>
		public static FdbRawMemoryValue ToBytes(ReadOnlyMemory<byte> value) => new(value, FdbValueTypeHint.Binary);

#if NET9_0_OR_GREATER
		/// <summary>Returns a value that wraps a span of bytes</summary>
		public static FdbRawSpanValue ToBytes(ReadOnlySpan<byte> value) => new(value, FdbValueTypeHint.Binary);
#else
		/// <summary>Returns a value that wraps a span of bytes</summary>
		/// <remarks>
		/// <para><b>WARNING:</b> on .NET 8.0 (or lower) this method has to copy the span into a Slice, which causes memory allocations.</para>
		/// <para>Please consider using <see cref="Slice"/> instead, or upgrade to .NET 9.0 or higher.</para>
		/// </remarks>
		[OverloadResolutionPriority(-1)]
		public static FdbRawValue ToBytes(ReadOnlySpan<byte> value) => new(Slice.FromBytes(value), FdbValueTypeHint.Binary);
#endif

		/// <summary>Returns a value that wraps the content of a <see cref="MemoryStream"/></summary>
		/// <remarks>The stream will be written from the start, and NOT the current position.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<MemoryStream, SpanEncoders.RawEncoder> ToBytes(MemoryStream value) => new(value, FdbValueTypeHint.Binary);

		#endregion

		#region Tuples...

		/// <summary>Returns a value that wraps a tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleValue FromTuple(IVarTuple value) => new(value);

		/// <summary>Returns a value that wraps a tuple with a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1> FromTuple<T1>(in ValueTuple<T1> tuple)
			=> new(tuple.Item1);

		/// <summary>Returns a value that wraps a tuple with 2 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> FromTuple<T1, T2>(in ValueTuple<T1, T2> tuple)
			=> new(tuple.Item1, tuple.Item2);

		/// <summary>Returns a value that wraps a tuple with 3 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> FromTuple<T1, T2, T3>(in ValueTuple<T1, T2, T3> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3);

		/// <summary>Returns a value that wraps a tuple with 4 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> FromTuple<T1, T2, T3, T4>(in ValueTuple<T1, T2, T3, T4> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

		/// <summary>Returns a value that wraps a tuple with 5 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> FromTuple<T1, T2, T3, T4, T5>(in ValueTuple<T1, T2, T3, T4, T5> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);

		/// <summary>Returns a value that wraps a tuple with 6 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> FromTuple<T1, T2, T3, T4, T5, T6>(in ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);

		/// <summary>Returns a value that wraps a tuple with 7 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> FromTuple<T1, T2, T3, T4, T5, T6, T7>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);

		/// <summary>Returns a value that wraps a tuple with 8 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> FromTuple<T1, T2, T3, T4, T5, T6, T7, T8>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8);

		/// <summary>Returns a value that encodes as a tuple with a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1> ToTuple<T1>(T1 item1) => new(item1);

		/// <summary>Returns a value that encodes as a tuple with 2 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> ToTuple<T1, T2>(T1 item1, T2 item2) => new(item1, item2);

		/// <summary>Returns a value that encodes as a tuple with 3 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> ToTuple<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(item1, item2, item3);

		/// <summary>Returns a value that encodes as a tuple with 4 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> ToTuple<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(item1, item2, item3, item4);

		/// <summary>Returns a value that encodes as a tuple with 5 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> ToTuple<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(item1, item2, item3, item4, item5);

		/// <summary>Returns a value that encodes as a tuple with 6 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> ToTuple<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(item1, item2, item3, item4, item5, item6);

		/// <summary>Returns a value that encodes as a tuple with 7 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> ToTuple<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(item1, item2, item3, item4, item5, item6, item7);

		/// <summary>Returns a value that encodes as a tuple with 8 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> ToTuple<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(item1, item2, item3, item4, item5, item6, item7, item8);

		#endregion

		#region Text...

		#region UTF-8...

		/// <summary>Returns a value that wraps a string, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(string? value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a StringBuilder, encoded as UTF-8 bytes</summary>
		public static FdbValue<StringBuilder, SpanEncoders.Utf8Encoder> ToTextUtf8(StringBuilder? value) => new(value, FdbValueTypeHint.Utf8);

		/// <summary>Returns a value that wraps a char array, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(char[] value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a segment of a char array, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(char[] value, int start, int length) => new(value.AsMemory(start, length));

		/// <summary>Returns a value that wraps a span of char, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(ReadOnlyMemory<char> value) => new(value);

#if NET9_0_OR_GREATER
		/// <summary>Returns a value that wraps a span of char, encoded as UTF-8 bytes</summary>
		public static FdbUtf8SpanValue ToTextUtf8(ReadOnlySpan<char> value) => new(value);
#else
		/// <summary>Returns a value that copies the contents of a span of char, encoded as UTF-8 bytes</summary>
		/// <remarks>
		/// <para><b>WARNING:</b> on .NET 8.0 (or lower) this method has to copy the span into a string, which causes memory allocations.</para>
		/// <para>Please consider using <see cref="ReadOnlyMemory{char}"/> instead, or upgrade to .NET 9.0 or higher.</para>
		/// </remarks>
		[OverloadResolutionPriority(-1)]
		public static FdbUtf8Value ToTextUtf8(ReadOnlySpan<char> value) => new(value.ToArray().AsMemory());
#endif

		#endregion

		#region UTF-16...

		/// <summary>Returns a value that wraps a string, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(string? value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a StringBuilder, encoded as UTF-16 bytes</summary>
		public static FdbValue<StringBuilder, SpanEncoders.Utf16Encoder> ToTextUtf16(StringBuilder? value) => new(value);

		/// <summary>Returns a value that wraps a char array, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(char[] value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a segment of a char array, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(char[] value, int start, int length) => new(value.AsMemory(start, length));

		/// <summary>Returns a value that wraps a span of char, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(ReadOnlyMemory<char> value) => new(value);

#if NET9_0_OR_GREATER
		/// <summary>Returns a value that wraps a span of char, encoded as UTF-16 bytes</summary>
		public static FdbUtf16SpanValue ToTextUtf16(ReadOnlySpan<char> value) => new(value);
#else
		/// <summary>Returns a value that copies the contents of a span of char, encoded as UTF-16 bytes</summary>
		/// <remarks>
		/// <para><b>WARNING:</b> on .NET 8.0 (or lower) this method has to copy the span into a string, which causes memory allocations.</para>
		/// <para>Please consider using <see cref="ReadOnlyMemory{char}"/> instead, or upgrade to .NET 9.0 or higher.</para>
		/// </remarks>
		[OverloadResolutionPriority(-1)]
		public static FdbUtf16Value ToTextUtf16(ReadOnlySpan<char> value) => new(value.ToArray().AsMemory());
#endif

		#endregion

		#endregion

		#region Fixed Size...

		/// <summary>Four 0x00 bytes (<c>`00 00 00 00`</c>)</summary>
		public static readonly FdbLittleEndianUInt32Value Zero32;

		/// <summary>Eight 0x00 bytes (<c>`00 00 00 00 00 00 00 00`</c>)</summary>
		public static readonly FdbLittleEndianUInt64Value Zero64;

		/// <summary>Four 0xFF bytes (<c>`FF FF FF FF`</c>)</summary>
		public static readonly FdbLittleEndianUInt32Value MaxValue32 = new(uint.MaxValue);

		/// <summary>Eight 0xFF bytes (<c>`FF FF FF FF FF FF FF FF`</c>)</summary>
		public static readonly FdbLittleEndianUInt64Value MaxValue64 = new(ulong.MaxValue);

		#region Little Endian...

		/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as 4 bytes in little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbLittleEndianUInt32Value ToFixed32LittleEndian(int value) => new(unchecked((uint) value));

		/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as 4 bytes in little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbLittleEndianUInt32Value ToFixed32LittleEndian(uint value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as 8 bytes in little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 00 00 00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbLittleEndianUInt64Value ToFixed64LittleEndian(long value) => new(unchecked((ulong) value));

		/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as 8 bytes in little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 00 00 00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbLittleEndianUInt64Value ToFixed64LittleEndian(ulong value) => new(value);

		#endregion

		#region Big Endian...

		/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as 4 bytes in big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbBigEndianUInt32Value ToFixed32BigEndian(int value) => new(unchecked((uint) value));

		/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as 4 bytes in big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbBigEndianUInt32Value ToFixed32BigEndian(uint value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as 8 bytes in big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00 00 00 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbBigEndianUInt64Value ToFixed64BigEndian(long value) => new(unchecked((ulong) value));

		/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as 8 bytes in big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00 00 00 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbBigEndianUInt64Value ToFixed64BigEndian(ulong value) => new(value);

		#endregion

		#endregion

		#region Compact...

		#region Little Endian...

		/// <summary>Returns a value that wraps a signed 32-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactLittleEndianUInt32Value ToCompactLittleEndian(int value) => new(unchecked((uint) value));

		/// <summary>Returns a value that wraps an unsigned 32-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactLittleEndianUInt32Value ToCompactLittleEndian(uint value) => new(value);

		/// <summary>Returns a value that wraps a signed 64-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactLittleEndianUInt64Value ToCompactLittleEndian(long value) => new(unchecked((ulong) value));

		/// <summary>Returns a value that wraps an unsigned 64-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactLittleEndianUInt64Value ToCompactLittleEndian(ulong value) => new(value);

		#endregion

		#region Big Endian...

		/// <summary>Returns a value that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactBigEndianUInt32Value ToCompactBigEndian(int value) => new(unchecked((uint) value));

		/// <summary>Returns a value that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactBigEndianUInt32Value ToCompactBigEndian(uint value) => new(value);

		/// <summary>Returns a value that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactBigEndianUInt64Value ToCompactBigEndian(long value) => new(unchecked((ulong) value));

		/// <summary>Returns a value that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbCompactBigEndianUInt64Value ToCompactBigEndian(ulong value) => new(value);

		#endregion

		#endregion

		#region Uuids...

		/// <summary>Returns a value that wraps a 128-bit UUID, encoded as 16 bytes</summary>
		public static FdbValue<Guid, SpanEncoders.FixedSizeUuidEncoder> ToUuid128(Guid value) => new(value);

		#endregion

		#region JSON...

		/// <summary>Returns a value that wraps a <see cref="JsonValue"/>, encoded as UTF-8 bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static FdbJsonValue ToJson(JsonValue value, CrystalJsonSettings? settings = null)
		{
			return new(value, settings);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbJsonValue<T> ToJson<T>(T? value, IJsonSerializer<T>? serializer = null, CrystalJsonSettings? settings = null)
		{
			return new(value, serializer, settings);
		}

		#endregion

	}

}
