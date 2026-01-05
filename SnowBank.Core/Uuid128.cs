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

namespace System
{
	using System.Buffers.Binary;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;
	using SnowBank.Data.Binary;
	using SnowBank.Text;
#if NET8_0_OR_GREATER
	using System.Buffers.Text;
#endif

	/// <summary>Represents an RFC 4122 compliant 128-bit UUID</summary>
	/// <remarks>You should use this type if you are primarily exchanging UUIDs with non-.NET platforms, that use the RFC 4122 byte ordering (big endian). The type System.Guid uses the Microsoft encoding (little endian) and is not compatible.</remarks>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), StructLayout(LayoutKind.Explicit), PublicAPI, Serializable]
	public readonly struct Uuid128 : IComparable, IEquatable<Uuid128>, IComparable<Uuid128>, IEquatable<Guid>, ISliceSerializable, ISpanEncodable
#if NET8_0_OR_GREATER
		, ISpanFormattable
		, ISpanParsable<Uuid128>
		, IUtf8SpanFormattable
		, IUtf8SpanParsable<Uuid128>
#endif
	{
		// This is just a wrapper struct on System.Guid that makes sure that ToByteArray() and Parse(byte[]) and new(byte[]) will parse according to RFC 4122 (http://www.ietf.org/rfc/rfc4122.txt)
		// For performance reasons, we will store the UUID as a System.GUID (Microsoft in-memory format), and swap the bytes when needed.

		// cf 4.1.2. Layout and Byte Order

		//    The fields are encoded as 16 octets, with the sizes and order of the
		//    fields defined above, and with each field encoded with the Most
		//    Significant Byte first (known as network byte order).  Note that the
		//    field names, particularly for multiplexed fields, follow historical
		//    practice.

		//    0                   1                   2                   3
		//    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |                          time_low                             |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |       time_mid                |         time_hi_and_version   |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |clk_seq_hi_res |  clk_seq_low  |         node (0-1)            |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |                         node (2-5)                            |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

		// packed "view"

		[FieldOffset(0)]
		private readonly Guid m_packed;

		// UUID "view"

		[FieldOffset(0)]
		private readonly uint m_timeLow;
		[FieldOffset(4)]
		private readonly ushort m_timeMid;
		[FieldOffset(6)]
		private readonly ushort m_timeHiAndVersion;
		[FieldOffset(8)]
		private readonly byte m_clkSeqHiRes;
		[FieldOffset(9)]
		private readonly byte m_clkSeqLow;
		[FieldOffset(10)]
		private readonly byte m_node0;
		[FieldOffset(11)]
		private readonly byte m_node1;
		[FieldOffset(12)]
		private readonly byte m_node2;
		[FieldOffset(13)]
		private readonly byte m_node3;
		[FieldOffset(14)]
		private readonly byte m_node4;
		[FieldOffset(15)]
		private readonly byte m_node5;

		/// <summary>Returns the 16 upper bits <c>xxxx....-....-....-....-............</c></summary>
		public ushort Upper16 => (ushort) (m_timeLow >> 16);

		/// <summary>Returns the 32 upper bits <c>xxxxxxxx-....-....-....-............</c></summary>
		/// <seealso cref="Lower96"/>
		public uint Upper32 => m_timeLow;

		/// <summary>Returns the 48 upper bits <c>xxxxxxxx-xxxx-....-....-............</c></summary>
		/// <seealso cref="Lower80"/>
		public Uuid48 Upper48 => new Uuid48(((ulong) m_timeLow << 16) | m_timeMid);

		/// <summary>Returns the 64 upper bits <c>xxxxxxxx-xxxx-xxxx-....-............</c></summary>
		/// <seealso cref="Lower64"/>
		public ulong Upper64 => ((ulong) m_timeLow << 32) | ((ulong) m_timeMid << 16) | m_timeHiAndVersion;

		/// <summary>Returns the 80 upper bits <c>xxxxxxxx-xxxx-xxxx-xxxx-............</c></summary>
		/// <seealso cref="Lower48"/>
		public Uuid80 Upper80 => Uuid80.FromUpper64Lower16(((ulong) m_timeLow << 32) | ((ulong) m_timeMid << 16) | m_timeHiAndVersion, (ushort) (((uint) m_clkSeqHiRes << 8) | m_clkSeqLow));

		/// <summary>Returns the 80 upper bits <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxx........</c></summary>
		/// <seealso cref="Lower32"/>
		public Uuid96 Upper96 => Uuid96.FromUpper64Lower32(((ulong) m_timeLow << 32) | ((ulong) m_timeMid << 16) | m_timeHiAndVersion, (((uint) m_clkSeqHiRes << 24) | ((uint) m_clkSeqLow << 16) | ((uint) m_node0 << 8) | m_node1));

		//note: Upper112 if we ever do an Uuid112 ?

		/// <summary>Returns the 16 lower bits <c>........-....-....-....-........xxxx</c></summary>
		public ushort Lower16 => (ushort) (((uint) m_node4 << 8) | m_node5);

		/// <summary>Returns the 32 lower bits <c>........-....-....-....-....xxxxxxxx</c></summary>
		/// <seealso cref="Upper96"/>
		public uint Lower32 => ((uint) m_node2 << 24) | ((uint) m_node3 << 16) | ((uint) m_node4 << 8) | m_node5;

		/// <summary>Returns the 48 lower bits <c>........-....-....-....-xxxxxxxxxxxx</c></summary>
		/// <seealso cref="Upper80"/>
		public Uuid48 Lower48 => new Uuid48(((ulong) m_node0 << 40) | ((ulong) m_node1 << 32) | ((ulong) m_node2 << 24) | ((ulong) m_node3 << 16) | ((ulong) m_node4 << 8) | m_node5);

		/// <summary>Returns the 64 lower bits <c>........-....-....-xxxx-xxxxxxxxxxxx</c></summary>
		/// <seealso cref="Upper64"/>
		public ulong Lower64 => ((ulong) m_clkSeqHiRes << 56) | ((ulong) m_clkSeqLow << 48) | ((ulong) m_node0 << 40) | ((ulong) m_node1 << 32) | ((ulong) m_node2 << 24) | ((ulong) m_node3 << 16) | ((ulong) m_node4 << 8) | m_node5;

		/// <summary>Returns the 80 lower bits <c>........-....-xxxx-xxxx-xxxxxxxxxxxx</c></summary>
		/// <seealso cref="Upper48"/>
		public Uuid80 Lower80 => Uuid80.FromUpper16Lower64(m_timeHiAndVersion, this.Lower64);

		/// <summary>Returns the 96 lower bits <c>........-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c></summary>
		/// <seealso cref="Upper32"/>
		public Uuid96 Lower96 => Uuid96.FromUpper32Lower64(((uint) m_timeMid << 16) | m_timeHiAndVersion, this.Lower64);

		//note: Lower112 if we ever do an Uuid112 ?

		#region Constructors...

		/// <summary>Constructs a <see cref="Uuid128"/> from a <see cref="Guid"/> value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		public Uuid128(Guid guid)
		{
			m_packed = guid;
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from a string literal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Prefer using Uuid128.Parse() or Uuid128.TryParse()")]
		public Uuid128(string value)
		{
			m_packed = new Guid(value);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from a <see cref="Slice"/></summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		[Obsolete("Prefer using Uuid128.Read() or Uuid128.TryRead()")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Uuid128(Slice slice)
		{
			m_packed = ReadGuidExact(slice.Span);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from a byte array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		[Obsolete("Prefer using Uuid128.Read() or Uuid128.TryRead()")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Uuid128(byte[] bytes)
		{
			m_packed = ReadGuidExact(bytes.AsSpan());
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from a span of bytes</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		[Obsolete("Prefer using Uuid128.Read() or Uuid128.TryRead()")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Uuid128(ReadOnlySpan<byte> bytes)
		{
			m_packed = ReadGuidExact(bytes);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from its constituent parts</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		public Uuid128(int a, short b, short c, byte[] d)
		{
			m_packed = new Guid(a, b, c, d);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from its constituent parts</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		public Uuid128(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
		{
			m_packed = new Guid(a, b, c, d, e, f, g, h, i, j, k);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from its constituent parts</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		public Uuid128(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
		{
			m_packed = new Guid(a, b, c, d, e, f, g, h, i, j, k);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from its constituent parts</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		public Uuid128(Uuid64 a, Uuid64 b)
		{
			m_packed = Convert(a, b);
		}

		/// <summary>Constructs a <see cref="Uuid128"/> from its constituent parts</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		public Uuid128(ulong a, ulong b)
		{
			m_packed = Convert(a, b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Guid(Uuid128 uuid) => uuid.m_packed;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(Guid guid) => new(guid);

		/// <summary>Constructs a <see cref="Uuid128"/> from a smaller 32-bit value</summary>
		/// <remarks>The 96 upper bits will be set to 0.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt32(uint low) => new(0, low);

		/// <summary>Constructs a <see cref="Uuid128"/> from a smaller 64-bit value</summary>
		/// <remarks>The 64 upper bits will be set to 0.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt64(ulong low) => new(0, low);

		/// <summary>Constructs a <see cref="Uuid128"/> from two smaller 64-bit values</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt64(ulong high, ulong low) => new(high, low);

#if NET8_0_OR_GREATER

		/// <summary>Constructs a <see cref="Uuid128"/> from an <see cref="Int128"/> value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Int128 a) : this() => m_packed = Convert((UInt128) a);

		/// <summary>Constructs a <see cref="Uuid128"/> from a <see cref="UInt128"/> value</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(UInt128 a) : this() => m_packed = Convert(a);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(Int128 a) => new(a);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(UInt128 a) => new(a);

		/// <summary>Constructs a <see cref="Uuid128"/> from a <see cref="UInt128"/> value</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt128(UInt128 value) => new(value);

#endif

		/// <summary><see cref="Uuid128"/> with all bits set to <c>0</c> (<c>"00000000-0000-0000-0000-000000000000"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public static readonly Uuid128 Empty;

		/// <summary><see cref="Uuid128"/> with all bits set to <c>1</c> (<c>"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public static readonly Uuid128 AllBitsSet
#if NET9_0_OR_GREATER
			= new(Guid.AllBitsSet);
#else
			= new(new Guid(-1, -1, -1, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
#endif

		/// <summary><see cref="Uuid128"/> with all bits set to <c>0</c> (<c>"00000000-0000-0000-0000-000000000000"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid128 MinValue;

		/// <summary><see cref="Uuid128"/> with all bits set to <c>0</c> (<c>"00000000-0000-0000-0000-000000000000"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid128 MaxValue
#if NET9_0_OR_GREATER
			= new(Guid.AllBitsSet);
#else
			= new(new Guid(-1, -1, -1, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
#endif

		/// <summary>Size is 16 bytes</summary>
		public const int SizeOf = 16;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static FormatException FailInvalidFormat() => ThrowHelper.FormatException($"Invalid {nameof(Uuid128)} format");

		/// <summary>Generate a new random 128-bit UUID.</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 NewUuid()
		{
			return new(Guid.NewGuid());
		}

		/// <summary>Returns a <see cref="Guid"/> created from the contents of a <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use either Read() or ReadExact() instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal static Guid Convert(Slice source) => ReadGuid(source.Span);

		/// <summary>Returns a <see cref="Guid"/> created from the contents of a span of bytes</summary>
		[Pure]
		[Obsolete("Use either Read() or ReadExact() instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Guid Convert(ReadOnlySpan<byte> source)
			=> source.Length == 0 ? Guid.Empty
			 : source.Length >= 16 ? ReadUnsafe(source)
			 : throw ErrorSourceBufferTooSmall();

		/// <summary>Returns a <see cref="Guid"/> created from two smaller <see cref="Uuid64"/></summary>
		internal static Guid Convert(Uuid64 a, Uuid64 b)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[SizeOf];
				BinaryPrimitives.WriteUInt64BigEndian(buf, a.ToUInt64());
				BinaryPrimitives.WriteUInt64BigEndian(buf[8..], b.ToUInt64());
				return ReadUnsafe(buf);
			}
		}

		/// <summary>Returns a <see cref="Guid"/> created from two smaller 64-bit integers</summary>
		internal static Guid Convert(ulong a, ulong b)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[SizeOf];
				BinaryPrimitives.WriteUInt64BigEndian(buf, a);
				BinaryPrimitives.WriteUInt64BigEndian(buf[8..], b);
				return ReadUnsafe(buf);
			}
		}

#if NET8_0_OR_GREATER

		/// <summary>Returns a <see cref="Guid"/> created from an <see cref="Uuid128"/></summary>
		internal static Guid Convert(UInt128 a)
		{
			Span<byte> tmp = stackalloc byte[16];
			BinaryPrimitives.WriteUInt128BigEndian(tmp, a);
			return ReadUnsafe(tmp);
		}

#endif

		#endregion

		#region Parsing...

		/// <summary>Parses a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		public static Uuid128 Parse(string input) => new(Guid.Parse(input));

		/// <summary>Parses a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid128 Parse(string input, IFormatProvider? provider) => new(Guid.Parse(input));

		/// <summary>Parses a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		public static Uuid128 Parse(ReadOnlySpan<char> input) => new(Guid.Parse(input));

		/// <summary>Parses a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid128 Parse(ReadOnlySpan<char> input, IFormatProvider? provider) => new(Guid.Parse(input));

#if NET8_0_OR_GREATER

		/// <summary>Parse a Base62 encoded string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromBase62(string buffer)
		{
			Contract.NotNull(buffer);
			return TryParseBase62(buffer.AsSpan(), out var value) ? value : throw FailInvalidFormat();
		}

		/// <summary>Parse a Base62 encoded string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromBase62(ReadOnlySpan<char> buffer)
		{
			return TryParseBase62(buffer, out var value) ? value : throw FailInvalidFormat();
		}

#endif

		/// <summary>Parses a span of UTF-8 characters into a <see cref="Uuid128"/></summary>
		/// <param name="utf8Text">The span of UTF-8 characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.FormatException"><paramref name="utf8Text" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="utf8Text" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="utf8Text" />.</returns>
		public static Uuid128 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
		{
			//TODO: REVIEW: there is currently (as of .NET 8) no overload for Guid.TryParse(RoS<byte>,...), so we have to use Utf8Parser.TryParse
			// => the issue is that is only returns false, without any hint on the actual error.

			// Guid.Parse/TryParse accept extra whitespaces (on both sides), so we have to trim...
			utf8Text = utf8Text.Trim(" \t\r\n"u8);

			if (utf8Text.Length == 38 && utf8Text[0] == '{' && utf8Text[37] == '}')
			{
				utf8Text = utf8Text[1..^1];
			}
			if (utf8Text.Length == 36)
			{
#if NET8_0_OR_GREATER
				if (!Utf8Parser.TryParse(utf8Text, out Guid g, out int consumed) || consumed != 36)
				{
					throw ThrowHelper.FormatException("Input is not a valid Uuid128 literal.");
				}
				return new(g);
#else
				// copy to a tmp buf and parse as string!
				if (UnsafeHelpers.IsAsciiBytes(utf8Text))
				{
					Span<char> tmp = stackalloc char[36];
					System.Text.Encoding.ASCII.GetChars(utf8Text, tmp);
					if (Guid.TryParse(tmp, out Guid g))
					{
						return new(g);
					}
				}
				throw ThrowHelper.FormatException("Input is not a valid Uuid128 literal.");
#endif
			}
			throw ThrowHelper.FormatException("Unrecognized Uuid128 format");
		}

		/// <summary>Tries to parse a span of UTF-8 characters into a <see cref="Uuid128"/>.</summary>
		/// <param name="utf8Text">The span of UTF-8 characters to parse.</param>
		/// <param name="provider">An object that provides culture-specific formatting information about <paramref name="utf8Text" />.</param>
		/// <param name="result">On return, contains the result of successfully parsing <paramref name="utf8Text" /> or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="utf8Text" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Uuid128 result)
			=> TryParse(utf8Text, out result);

		/// <summary>Tries to parse a span of UTF-8 characters into a <see cref="Uuid128"/>.</summary>
		/// <param name="utf8Text">The span of UTF-8 characters to parse.</param>
		/// <param name="result">On return, contains the result of successfully parsing <paramref name="utf8Text" /> or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="utf8Text" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Uuid128 result)
		{
			//TODO: REVIEW: there is currently (as of .NET 8) no overload for Guid.TryParse(RoS<byte>,...), so we have to use Utf8Parser.TryParse
			// => the issue is that is only returns false, without any hint on the actual error.

			// Guid.Parse/TryParse accept extra whitespaces (on both sides), so we have to trim...
			utf8Text = utf8Text.Trim(" \t\r\n"u8);

			if (utf8Text.Length == 38 && utf8Text[0] == '{' && utf8Text[37] == '}')
			{
				utf8Text = utf8Text[1..^1];
			}
			if (utf8Text.Length == 36)
			{
#if NET8_0_OR_GREATER
				if (Utf8Parser.TryParse(utf8Text, out Guid g, out int consumed) && consumed == 36)
				{
					result = new(g);
					return true;
				}
#else
				// copy to a tmp buf and parse as string!
				if (UnsafeHelpers.IsAsciiBytes(utf8Text))
				{
					Span<char> tmp = stackalloc char[36];
					System.Text.Encoding.ASCII.GetChars(utf8Text, tmp);
					if (Guid.TryParse(tmp, out Guid g))
					{
						result = new(g);
						return true;
					}
				}
#endif
			}
			result = default;
			return false;
		}

		/// <summary>Parses a string representation of an UUid128</summary>
		public static Uuid128 ParseExact(
			string input,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			string format
		)
		{
#if NET8_0_OR_GREATER
			if (format.Length == 1)
			{
				var c = format[0] | 0x20;
				if (c == 'd') goto parse_guid;
				if (c == 'c') return input.Length is > 0 and <= 22 ? FromBase62(input) : throw FailInvalidFormat();
				if (c == 'z') return input.Length == 22 ? FromBase62(input) : throw FailInvalidFormat();
			}
		parse_guid:
#endif
			return new(Guid.ParseExact(input, format));
		}

		/// <summary>Parses a string representation of an UUid128</summary>
		public static Uuid128 ParseExact(
			ReadOnlySpan<char> input,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			ReadOnlySpan<char> format
		) => new(Guid.ParseExact(input, format));

		/// <summary>Tries to parse a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		public static bool TryParse([NotNullWhen(true)] string? input, out Uuid128 result)
		{
			if (!Guid.TryParse(input, out Guid guid))
			{
				result = default;
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		/// <summary>Tries to parse a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse([NotNullWhen(true)] string? input, IFormatProvider? provider, out Uuid128 result)
			=> TryParse(input, out result);

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		public static bool TryParse(ReadOnlySpan<char> input, out Uuid128 result)
		{
			if (!Guid.TryParse(input, out var g))
			{
				result = default;
				return false;
			}

			result = new(g);
			return true;
		}

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out Uuid128 result)
			=> TryParse(input, out result);

		/// <summary>Parse a string representation of an UUid128</summary>
		public static bool TryParseExact(string input, string format, out Uuid128 result)
		{
			if (!Guid.TryParseExact(input, format, out var guid))
			{
				result = default;
				return false;
			}
			result = new(guid);
			return true;
		}

		/// <summary>Parse a string representation of an UUid128</summary>
		public static bool TryParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format, out Uuid128 result)
		{
			if (!Guid.TryParseExact(input, format, out Guid guid))
			{
				result = default;
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

#if NET8_0_OR_GREATER

		public static bool TryParseBase62(string? input, out Uuid128 result)
		{
			if (input == null)
			{
				result = default;
				return false;
			}
			return TryParseBase62(input.AsSpan(), out result);
		}

		public static bool TryParseBase62(ReadOnlySpan<char> input, out Uuid128 result)
		{
			if (input.Length == 0)
			{
				result = default;
				return true;
			}

			if (input.Length <= 22 && Base62Encoding.TryDecodeUInt128(input, out UInt128 x, Base62FormattingOptions.Lexicographic))
			{
				result = new Uuid128(x);
				return true;
			}

			result = default;
			return false;
		}

#endif

#endregion

		/// <summary>Returns the timestamp field of this uuid.</summary>
		public long Timestamp
		{
			[Pure]
			get
			{
				long ts = m_timeLow;
				ts |= ((long) m_timeMid) << 32;
				ts |= ((long) (m_timeHiAndVersion & 0x0FFF)) << 48;
				return ts;
			}
		}

		/// <summary>Returns the version field of this uuid.</summary>
		public int Version
		{
			[Pure]
			get => m_timeHiAndVersion >> 12;
		}

		/// <summary>Returns the clock sequence field of this uuid.</summary>
		public int ClockSequence
		{
			[Pure]
			get
			{
				int clk = m_clkSeqLow;
				clk |= (m_clkSeqHiRes & 0x3F) << 8;
				return clk;
			}
		}

		/// <summary>Returns the node field of this uuid.</summary>
		public long Node
		{
			[Pure]
			get
			{
				long node;
				node = ((long)m_node0) << 40;
				node |= ((long)m_node1) << 32;
				node |= ((long)m_node2) << 24;
				node |= ((long)m_node3) << 16;
				node |= ((long)m_node4) << 8;
				node |= m_node5;
				return node;
			}
		}

		#region Unsafe I/O...

		/// <summary>Reads a <see cref="Uuid128"/> from a byte buffer, if it is large enough.</summary>
		/// <param name="source">Source buffer, that should have at least 16 bytes.</param>
		/// <param name="result">Value stored in the buffer</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryRead(ReadOnlySpan<byte> source, out Uuid128 result)
		{
			switch (source.Length)
			{
				case 0:
				{
					result = Empty;
					return true;
				}
				case >= SizeOf:
				{
					result = Read(source);
					return true;
				}
				default:
				{
					result = Empty;
					return false;
				}
			}
		}

		/// <summary>Reads a <see cref="Uuid128"/> from a byte buffer that must contain exactly 16 bytes.</summary>
		/// <param name="source">Source buffer of length 16.</param>
		/// <param name="result">Value stored in the buffer</param>
		/// <returns><c>true</c> if the buffer has a length of 16; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryReadExact(ReadOnlySpan<byte> source, out Uuid128 result)
		{
			if (source.Length != 16)
			{
				result = default;
				return false;
			}
			result = new(ReadUnsafe(source));
			return true;
		}

		// ReSharper disable once NotResolvedInText
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException ErrorSourceBufferTooSmall() => new("The source buffer must be at least 16 bytes long", "source");

		// ReSharper disable once NotResolvedInText
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException ErrorSourceBufferInvalidSize() => new("The source buffer must be have a length of 16 bytes", "source");

		/// <summary>Reads a 128-bit <see cref="Guid"/> from a byte array.</summary>
		/// <param name="source">Source buffer, that should either be empty, or hold at least 16 bytes.</param>
		/// <returns>Value stored in the buffer</returns>
		/// <exception cref="ArgumentException"> if the buffer is too small.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 Read(byte[]? source) => Read(new ReadOnlySpan<byte>(source));

		/// <summary>Reads a 128-bit <see cref="Guid"/> from a byte buffer.</summary>
		/// <param name="source">Source buffer, that should either be empty, or hold at least 16 bytes.</param>
		/// <returns>Value stored in the buffer</returns>
		/// <exception cref="ArgumentException"> if the buffer is too small.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 Read(Slice source) => Read(source.Span);

		/// <summary>Reads a <see cref="Uuid128"/> from a byte buffer.</summary>
		/// <param name="source">Source buffer, that should either be empty, or hold at least 16 bytes.</param>
		/// <returns>Value stored in the buffer</returns>
		/// <exception cref="ArgumentException"> if the buffer is too small.</exception>
		[Pure]
		public static Uuid128 Read(ReadOnlySpan<byte> source)
		{
			return source.Length == 0 ? Empty
				 : source.Length >= 16 ? new(ReadUnsafe(source))
				 : throw ErrorSourceBufferTooSmall();
		}

		/// <summary>Reads a <see cref="Uuid128"/> from a byte buffer that must contain exactly 16 bytes.</summary>
		/// <param name="source">Source buffer of length 16.</param>
		/// <returns>Value stored in the buffer</returns>
		/// <exception cref="ArgumentException"> if the buffer does not have a length of 16 bytes.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 ReadExact(ReadOnlySpan<byte> source)
			=> source.Length == 16 ? new(ReadUnsafe(source)) : throw ErrorSourceBufferInvalidSize();

		/// <summary>Reads a 128-bit <see cref="Guid"/> from a byte buffer.</summary>
		/// <param name="source">Source buffer, that should either be empty, or hold at least 16 bytes.</param>
		/// <returns>Value stored in the buffer</returns>
		/// <exception cref="ArgumentException"> if the buffer is too small.</exception>
		[Pure]
		public static Guid ReadGuid(ReadOnlySpan<byte> source)
		{
			return source.Length == 0 ? Guid.Empty
				: source.Length >= 16 ? ReadUnsafe(source)
				: throw ErrorSourceBufferTooSmall();
		}

		/// <summary>Reads a 128-bit <see cref="Guid"/> from a byte buffer that must contain exactly 16 bytes.</summary>
		/// <param name="source">Source buffer of length 16.</param>
		/// <returns>Value stored in the buffer</returns>
		/// <exception cref="ArgumentException"> if the buffer does not have a length of 16 bytes.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid ReadGuidExact(ReadOnlySpan<byte> source)
			=> source.Length == 16 ? ReadUnsafe(source) : throw ErrorSourceBufferInvalidSize();

		internal static unsafe Guid ReadUnsafe(ReadOnlySpan<byte> source)
		{
			Guid tmp;
			fixed (byte* src = &MemoryMarshal.GetReference(source))
			{
				if (BitConverter.IsLittleEndian)
				{
					byte* ptr = (byte*) &tmp;

					// Data1: 32 bits, must swap
					ptr[0] = src[3];
					ptr[1] = src[2];
					ptr[2] = src[1];
					ptr[3] = src[0];
					// Data2: 16 bits, must swap
					ptr[4] = src[5];
					ptr[5] = src[4];
					// Data3: 16 bits, must swap
					ptr[6] = src[7];
					ptr[7] = src[6];
					// Data4: 64 bits, no swap required
					*(long*) (ptr + 8) = *(long*) (src + 8);
				}
				else
				{
					long* ptr = (long*) &tmp;
					ptr[0] = *(long*) (src);
					ptr[1] = *(long*) (src + 8);
				}
			}
			return tmp;
		}

		/// <summary>Writes a <see cref="Guid"/> into a buffer, if it is large enough.</summary>
		/// <param name="value">Value to write</param>
		/// <param name="buffer">Destination buffer, that must have a length of at least 16 bytes.</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c></returns>
		public static bool TryWrite(in Guid value, Span<byte> buffer)
		{
			if (buffer.Length < 16)
			{
				return false;
			}
			Write(in value, buffer);
			return true;
		}

		// ReSharper disable once NotResolvedInText
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException ErrorDestinationBufferTooSmall() => new("The destination buffer is too small", "buffer");

		/// <summary>Writes a <see cref="Guid"/> into a buffer.</summary>
		/// <param name="value">Value to write</param>
		/// <param name="buffer">Destination buffer, that must have a length of at least 16 bytes.</param>
		/// <exception cref="ArgumentException"> if the buffer was too small</exception>
		public static unsafe void Write(in Guid value, Span<byte> buffer)
		{
			if (buffer.Length < 16) throw ErrorDestinationBufferTooSmall();

			fixed (Guid* inp = &value)
			fixed (byte* outp = &MemoryMarshal.GetReference(buffer))
			{
				if (BitConverter.IsLittleEndian)
				{
					byte* src = (byte*) inp;

					// Data1: 32 bits, must swap
					outp[0] = src[3];
					outp[1] = src[2];
					outp[2] = src[1];
					outp[3] = src[0];
					// Data2: 16 bits, must swap
					outp[4] = src[5];
					outp[5] = src[4];
					// Data3: 16 bits, must swap
					outp[6] = src[7];
					outp[7] = src[6];
					// Data4: 64 bits, no swap required
					*(long*) (outp + 8) = *(long*) (src + 8);
				}
				else
				{
					long* src = (long*) inp;
					*(long*) (outp) = src[0];
					*(long*) (outp + 8) = src[1];
				}
			}
		}

		/// <summary>[OBSOLETE] => WriteTo()</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Renamed to WriteTo(..)")] //TODO: remove me next time!
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void WriteToUnsafe(Span<byte> buffer) => Write(in m_packed, buffer);

		/// <summary>Writes the bytes of this instance to the specified <paramref name="buffer"/></summary>
		/// <param name="buffer">Buffer where the bytes will be written to, with a capacity of at least 16 bytes</param>
		/// <exception cref="ArgumentException">If <paramref name="buffer"/> is smaller than 16 bytes</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(Span<byte> buffer) => Write(in m_packed, buffer);

		/// <summary>Writes the bytes of this instance to the specified <paramref name="buffer"/>, if it is large enough</summary>
		/// <param name="buffer">Buffer where the bytes will be written to, with a capacity of at least 16 bytes</param>
		/// <returns><see langword="true"/> if <paramref name="buffer"/> was large enough; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryWriteTo(Span<byte> buffer)
		{
			if (buffer.Length < SizeOf)
			{
				return false;
			}
			Write(in m_packed, buffer);
			return true;
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="buffer"/>, if it is large enough</summary>
		/// <param name="buffer">Buffer where the bytes will be written to, with a capacity of at least 16 bytes</param>
		/// <param name="bytesWritten">Receives the number of bytes written (either <c>16</c> or <c>0</c>)</param>
		/// <returns><see langword="true"/> if <paramref name="buffer"/> was large enough; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryWriteTo(Span<byte> buffer, out int bytesWritten)
		{
			if (buffer.Length < SizeOf)
			{
				bytesWritten = 0;
				return false;
			}
			Write(in m_packed, buffer);
			bytesWritten = SizeOf;
			return true;
		}

#if NET8_0_OR_GREATER

		/// <summary>Return the equivalent <see cref="UInt128"/></summary>
		/// <remarks>The integer correspond to the big-endian version of this instance serialized as a byte array</remarks>
		public UInt128 ToUInt128()
		{
			Span<byte> tmp = stackalloc byte[16];
			Write(in m_packed, tmp);
			return BinaryPrimitives.ReadUInt128BigEndian(tmp);
		}

		/// <summary>Return the equivalent <see cref="Int128"/></summary>
		/// <remarks>The integer correspond to the big-endian version of this instance serialized as a byte array</remarks>
		public Int128 ToInt128()
		{
			Span<byte> tmp = stackalloc byte[16];
			Write(in m_packed, tmp);
			return BinaryPrimitives.ReadInt128BigEndian(tmp);
		}

#endif

		#endregion

		#region Decomposition...

		/// <summary>Splits this 128-bit UUID into two 64-bit UUIDs</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 8 bytes (in network order) of this UUID</param>
		public void Split(out Uuid64 high, out Uuid64 low)
		{
			Deconstruct(out high, out low);
		}

		/// <summary>Splits this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="a"><c>xxxxxxxx-xxxx-xxxx-....-............</c></param>
		/// <param name="b"><c>........-....-....-xxxx-xxxxxxxxxxxx</c></param>
		public void Split(out ulong a, out ulong b)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buffer, SizeOf));
				a = UnsafeHelpers.LoadUInt64BE(buffer + 0);
				b = UnsafeHelpers.LoadUInt64BE(buffer + 8);
			}
		}

		/// <summary>Splits this 128-bit UUID into two 64-bit UUIDs</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 8 bytes (in network order) of this UUID</param>
		public void Deconstruct(out Uuid64 high, out Uuid64 low)
		{
			unsafe
			{
				Span<byte> buffer = stackalloc byte[SizeOf];
				Write(in m_packed, buffer);
				high = new Uuid64(BinaryPrimitives.ReadInt64BigEndian(buffer));
				low = new Uuid64(BinaryPrimitives.ReadInt64BigEndian(buffer.Slice(8)));
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="a"><c>xxxxxxxx-xxxx-xxxx-....-............</c></param>
		/// <param name="b"><c>........-....-....-xxxx-xxxx........</c></param>
		/// <param name="c"><c>........-....-....-....-....xxxxxxxx</c></param>
		public void Deconstruct(out ulong a, out uint b, out uint c)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buffer, SizeOf));
				a = UnsafeHelpers.LoadUInt64BE(buffer + 0);
				b = UnsafeHelpers.LoadUInt32BE(buffer + 8);
				c = UnsafeHelpers.LoadUInt32BE(buffer + 12);
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="a"><c>xxxxxxxx-....-....-....-............</c></param>
		/// <param name="b"><c>........-xxxx-xxxx-....-............</c></param>
		/// <param name="c"><c>........-....-....-xxxx-xxxx........</c></param>
		/// <param name="d"><c>........-....-....-....-....xxxxxxxx</c></param>
		public void Deconstruct(out uint a, out uint b, out uint c, out uint d)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buffer, SizeOf));
				a = UnsafeHelpers.LoadUInt32BE(buffer + 0);
				b = UnsafeHelpers.LoadUInt32BE(buffer + 4);
				c = UnsafeHelpers.LoadUInt32BE(buffer + 8);
				d = UnsafeHelpers.LoadUInt32BE(buffer + 12);
			}
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 96-bit and lower 32-bit parts</summary>
		/// <param name="hi">96 upper bits  (<c>xxxxxxxx-xxxx-xxxx-xxxx-xxxx........</c>)</param>
		/// <param name="low">32 lower bits (<c>........-....-....-....-....xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper96Lower32(Uuid96 hi, uint low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			hi.WriteToUnsafe(buf);
			BinaryPrimitives.WriteUInt32BigEndian(buf[12..], low);
			return Read(buf);
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 80-bit and lower 48-bit parts</summary>
		/// <param name="hi">80 upper bits  (<c>xxxxxxxx-xxxx-xxxx-xxxx-............</c>)</param>
		/// <param name="low">48 lower bits (<c>........-....-....-....-xxxxxxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper80Lower48(Uuid80 hi, Uuid48 low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			hi.WriteToUnsafe(buf);
			low.WriteToUnsafe(buf[10..]);
			return Read(buf);
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 64-bit and lower 64-bit parts</summary>
		/// <param name="hi">64 upper bits  (<c>xxxxxxxx-xxxx-xxxx-....-............</c>)</param>
		/// <param name="low">64 lower bits (<c>........-....-....-xxxx-xxxxxxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUpper64Lower64(ulong hi, ulong low) => new(hi, low);

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 64-bit and lower 64-bit parts</summary>
		/// <param name="hi">64 upper bits  (<c>xxxxxxxx-xxxx-xxxx-....-............</c>)</param>
		/// <param name="low">64 lower bits (<c>........-....-....-xxxx-xxxxxxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUpper64Lower64(Uuid64 hi, Uuid64 low) => new(hi, low);

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 48-bit and lower 80-bit parts</summary>
		/// <param name="hi">48 upper bits  (<c>xxxxxxxx-xxxx-....-....-............</c>)</param>
		/// <param name="low">80 lower bits (<c>........-....-xxxx-xxxx-xxxxxxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper48Lower80(Uuid48 hi, Uuid80 low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			hi.WriteToUnsafe(buf);
			low.WriteToUnsafe(buf[6..]);
			return Read(buf);
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 32-bit and lower 96-bit parts</summary>
		/// <param name="hi">32 upper bits  (<c>xxxxxxxx-....-....-....-............</c>)</param>
		/// <param name="low">96 lower bits (<c>........-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper32Lower96(uint hi, Uuid96 low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			BinaryPrimitives.WriteUInt32BigEndian(buf, hi);
			low.WriteToUnsafe(buf[4..]);
			return Read(buf);
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 32-bit, middle 32-bit and lower 64-bit parts</summary>
		/// <param name="hi">32 upper bits (<c>xxxxxxxx-....-....-....-............</c>)</param>
		/// <param name="middle">32 middle bits (<c>........-xxxx-xxxx-....-............</c>)</param>
		/// <param name="low">64 lower bits (<c>........-....-....-xxxx-xxxxxxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper32Middle32Lower64(uint hi, uint middle, ulong low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			BinaryPrimitives.WriteUInt32BigEndian(buf, hi);
			BinaryPrimitives.WriteUInt32BigEndian(buf[4..], middle);
			BinaryPrimitives.WriteUInt64BigEndian(buf[8..], low);
			return Read(buf);
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 32-bit, middle 64-bit and lower 32-bit parts</summary>
		/// <param name="hi">32 upper bits (<c>xxxxxxxx-....-....-....-............</c>)</param>
		/// <param name="middle">64 middle bits (<c>........-xxxx-xxxx-xxxx-xxxx........</c>)</param>
		/// <param name="low">32 lower bits (<c>........-....-....-....-....xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper32Middle64Lower32(uint hi, ulong middle, uint low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			BinaryPrimitives.WriteUInt32BigEndian(buf, hi);
			BinaryPrimitives.WriteUInt64BigEndian(buf[4..], middle);
			BinaryPrimitives.WriteUInt32BigEndian(buf[12..], low);
			return Read(buf);
		}

		/// <summary>Creates a <see cref="Uuid128"/> from the upper 64-bit, middle 32-bit and lower 32-bit parts</summary>
		/// <param name="hi">64 upper bits (<c>xxxxxxxx-xxxx-xxxx-....-............</c>)</param>
		/// <param name="middle">32 middle bits (<c>........-....-....-xxxx-xxxx........</c>)</param>
		/// <param name="low">32 lower bits (<c>........-....-....-....-....xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid128 FromUpper64Middle32Lower32(ulong hi, uint middle, uint low)
		{
			Span<byte> buf = stackalloc byte[SizeOf];
			BinaryPrimitives.WriteUInt64BigEndian(buf, hi);
			BinaryPrimitives.WriteUInt32BigEndian(buf[8..], middle);
			BinaryPrimitives.WriteUInt32BigEndian(buf[12..], low);
			return Read(buf);
		}

		#endregion

		#region Conversion...

		/// <summary>Returns the equivalent <see cref="Guid"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Guid ToGuid() => m_packed;

		/// <summary>Returns newly allocated array of bytes that represents this UUID</summary>
		[Pure]
		public byte[] ToByteArray()
		{
			// We must use Big Endian when serializing the UUID
			var res = new byte[SizeOf];
			Write(in m_packed, res.AsSpan());
			return res;
		}

		/// <summary>Returns a newly allocated <see cref="Slice"/> that represents this UUID</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
			=> new(ToByteArray()); //TODO: OPTIMIZE: optimize this ?

		/// <summary>Writes this UUID to the specified writer</summary>
		public void WriteTo(ref SliceWriter writer)
		{
			WriteTo(writer.AllocateSpan(SizeOf));
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid128"/> structure.</summary>
		/// <returns>The value of this Guid, formatted by using the "D" format specifier as follows: <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => m_packed.ToString();

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid128"/> structure.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this <see cref="Uuid128"/>. The format parameter can be "N", "D", "B", "P", "C", "Z" or "X". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid128"/>, represented as a series of lowercase hexadecimal digits in the specified format.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			string? format
		)
		{
#if NET8_0_OR_GREATER
			return format switch
			{
				null or "D" => m_packed.ToString(),
				"C" or "c" => ToBase62(padded: false), // base 62, compact, up to 22 characters
				"Z" or "z" => ToBase62(padded: true),  // base 62, padded to 22 characters
				_ => m_packed.ToString(format)
			};
#else
			return m_packed.ToString(format);
#endif
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid128"/> structure.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this <see cref="Uuid128"/>. The format parameter can be "N", "D", "B", "P", "C", "Z" or "X". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="provider">An object that supplies culture-specific formatting information.</param>
		/// <returns>The value of this <see cref="Uuid128"/>, represented as a series of lowercase hexadecimal digits in the specified format.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			string? format,
			IFormatProvider? provider
		)
		{
#if NET8_0_OR_GREATER
			return format switch
			{
				null or "D" => m_packed.ToString(format, provider),
				"C" or "c" => ToBase62(padded: false), // base 62, compact, up to 22 characters
				"Z" or "z" => ToBase62(padded: true),  // base 62, padded to 22 characters
				_ => m_packed.ToString(format, provider)
			};
#else
			return m_packed.ToString(format, provider);
#endif
		}

#if NET8_0_OR_GREATER

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid128"/> structure in Base62 format.</summary>
		/// <param name="padded">If <see langword="false"/> (default), the shortest possible literal is returned. If <see langword="true"/>, the literal is padded with <c>0</c> so that the resulting string can be sorted lexicographically</param>
		/// <returns>The value of this <see cref="Uuid128"/>, represented using the Base62 characters that are safe to be included in any Uri without escaping.</returns>
		public string ToBase62(bool padded = false)
		{
			return Base62Encoding.Encode(this.ToUInt128(), padded ? Base62FormattingOptions.Lexicographic | Base62FormattingOptions.Padded : Base62FormattingOptions.Lexicographic);
		}

		/// <summary>Tries to format the value of this instance of the <see cref="Uuid128"/> structure in Base62 format to the specified destination.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="charsWritten">Receives the number of characters written to <paramref name="destination"/>></param>
		/// <param name="padded">If <see langword="false"/> (default), the shortest possible literal is returned. If <see langword="true"/>, the literal is padded with <c>0</c> so that the resulting string can be sorted lexicographically</param>
		/// <returns><see langword="true"/> if the buffer was large enough; otherwise, <see langword="false"/></returns>
		private bool TryFormatToBase62(Span<char> destination, out int charsWritten, bool padded = false)
		{
			return Base62Encoding.TryEncodeTo(destination, out charsWritten, this.ToUInt128(), padded ? Base62FormattingOptions.Lexicographic | Base62FormattingOptions.Padded : Base62FormattingOptions.Lexicographic);
		}

#endif

		/// <summary>Tries to format the value of the current instance into the provided span of characters.</summary>
		/// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
		/// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="destination" />.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<char> destination,
			out int charsWritten,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		)
		{
#if NET8_0_OR_GREATER
			return format switch
			{
				"" or "D" => m_packed.TryFormat(destination, out charsWritten),
				"C" or "c" => TryFormatToBase62(destination, out charsWritten, padded: false), // base 62, compact, up to 22 characters
				"Z" or "z" => TryFormatToBase62(destination, out charsWritten, padded: true), // base 62, padded to 22 characters
				_ => m_packed.TryFormat(destination, out charsWritten, format)
			};
#else
			return m_packed.TryFormat(destination, out charsWritten, format);
#endif
		}

#if NET8_0_OR_GREATER

		/// <summary>Tries to format the value of the current instance UTF-8 into the provided span of bytes.</summary>
		/// <param name="utf8Destination">The span in which to write this instance's value formatted as a span of bytes.</param>
		/// <param name="bytesWritten">When this method returns, contains the number of bytes that were written in <paramref name="utf8Destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="utf8Destination" />.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<byte> utf8Destination,
			out int bytesWritten,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		) => m_packed.TryFormat(utf8Destination, out bytesWritten, format);

#endif

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] int value)
		{
			Contract.Debug.Requires(value >= 0);
			return Increment(checked((ulong) value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] long value)
		{
			Contract.Debug.Requires(value >= 0);
			return Increment(checked((ulong) value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Value to add to this UUID</param>
		/// <returns>Incremented UUID</returns>
		[Pure]
		public Uuid128 Increment(ulong value)
		{
			unsafe
			{
				// serialize GUID into High Endian format
				byte* buf = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buf, SizeOf));

				// Add the low 64 bits (in HE)
				ulong sum = unchecked(UnsafeHelpers.LoadUInt64BE(buf + 8) + value);
				if (sum < value)
				{ // overflow occured, we must carry to the high 64 bits (in HE)
					UnsafeHelpers.StoreUInt64BE(buf, unchecked(UnsafeHelpers.LoadUInt64BE(buf) + 1));
				}
				UnsafeHelpers.StoreUInt64BE(buf + 8, sum);
				// deserialize back to GUID
				return new(ReadUnsafe(new ReadOnlySpan<byte>(buf, SizeOf)));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 operator +(Uuid128 left, long right)
		{
			return left.Increment(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 operator +(Uuid128 left, ulong right)
		{
			return left.Increment(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 operator ++(Uuid128 left)
		{
			return left.Increment(1);
		}

		//TODO: Decrement

		#endregion

		#region Equality / Comparison ...

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			if (obj == null) return false;
			if (obj is Uuid128 u128) return m_packed == u128.m_packed;
			if (obj is Guid g) return m_packed == g;
			//TODO: Slice? string?
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid128 other)
		{
			return m_packed == other.m_packed;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Guid other)
		{
			return m_packed == other;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Uuid128 a, Uuid128 b)
		{
			return a.m_packed == b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Uuid128 a, Uuid128 b)
		{
			return a.m_packed != b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Uuid128 a, Guid b)
		{
			return a.m_packed == b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Uuid128 a, Guid b)
		{
			return a.m_packed != b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Guid a, Uuid128 b)
		{
			return a == b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Guid a, Uuid128 b)
		{
			return a != b.m_packed;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return m_packed.GetHashCode();
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid128 other)
		{
			return m_packed.CompareTo(other.m_packed);
		}

		/// <inheritdoc />
		public int CompareTo(object? obj)
		{
			switch (obj)
			{
				case null: return 1;
				case Uuid128 u128: return m_packed.CompareTo(u128.m_packed);
				case Guid g: return m_packed.CompareTo(g);
			}
			return m_packed.CompareTo(obj);
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid128 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid128>, IComparer<Uuid128>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer()
			{ }

			/// <inheritdoc />
			public bool Equals(Uuid128 x, Uuid128 y)
			{
				return x.m_packed.Equals(y.m_packed);
			}

			/// <inheritdoc />
			public int GetHashCode(Uuid128 obj)
			{
				return obj.m_packed.GetHashCode();
			}

			/// <inheritdoc />
			public int Compare(Uuid128 x, Uuid128 y)
			{
				return x.m_packed.CompareTo(y.m_packed);
			}
		}

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint) { sizeHint = SizeOf; return true; }

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryEncode(scoped Span<byte> destination, out int bytesWritten)
			=> TryWriteTo(destination, out bytesWritten);

		#endregion

	}

}
