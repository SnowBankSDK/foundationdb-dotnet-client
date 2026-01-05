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
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using System.Security.Cryptography;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;
	using SnowBank.Data.Binary;

	/// <summary>Represents an 80-bit UUID that is stored in high-endian format on the wire</summary>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct Uuid80 : IFormattable, IEquatable<Uuid80>, IComparable<Uuid80>, IEquatable<Slice>, ISliceSerializable, ISpanEncodable
#if NET8_0_OR_GREATER
		, ISpanFormattable
		, ISpanParsable<Uuid80>
#endif
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		/// <summary><see cref="Uuid80"/> with all bits set to <c>0</c> (<c>"0000-00000000-00000000"</c>)</summary>
		public static readonly Uuid80 Empty;

		/// <summary><see cref="Uuid80"/> with only bit 0 set to <c>1</c> (<c>"0000-00000000-00000001"</c>)</summary>
		public static readonly Uuid80 One = new(0, 1);

		/// <summary><see cref="Uuid80"/> with all bits set to <c>1</c> (<c>"FFFF-FFFFFFFF-FFFFFFFF"</c>)</summary>
		public static readonly Uuid80 AllBitsSet = new(ushort.MaxValue, ulong.MaxValue);

		/// <summary><see cref="Uuid80"/> with all bits set to <c>0</c> (<c>"0000-00000000-00000000"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid80 MinValue;

		/// <summary><see cref="Uuid80"/> with all bits set to <c>1</c> (<c>"FFFF-FFFFFFFF-FFFFFFFF"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid80 MaxValue = new(ushort.MaxValue, ulong.MaxValue);

		/// <summary>Size is 10 bytes</summary>
		public const int SizeOf = 10;

		//note: these two fields are stored in host order (so probably Little-Endian) in order to simplify parsing and ordering

		/// <summary>The 16 upper bits (<c>xxxx-........-........</c>)</summary>
		private readonly ushort High;

		/// <summary>The 64 lower bits (<c>....-xxxxxxxx-xxxxxxxx</c>)</summary>
		private readonly ulong Low;

		private const ulong MASK_48 = (1UL << 48) - 1;
		private const ulong MASK_32 = (1UL << 32) - 1;
		private const ulong MASK_16 = (1UL << 16) - 1;

		/// <summary>Returns the 16 upper bits <c>xxxx-........-........</c></summary>
		/// <seealso cref="Lower64"/>
		public uint Upper16 => this.High;

		/// <summary>Returns the 32 upper bits <c>xxxx-xxxx....-........</c></summary>
		/// <seealso cref="Lower48"/>
		public uint Upper32 => unchecked(((uint) this.High << 16) | (uint) (this.Low >> 48));

		/// <summary>Returns the 48 upper bits <c>xxxx-xxxxxxxx-........</c></summary>
		/// <seealso cref="Lower32"/>
		public Uuid48 Upper48 => new(((ulong) this.High << 32) | (this.Low >> 32));

		/// <summary>Returns the 64 upper bits <c>xxxx-xxxxxxxx-xxxx....</c></summary>
		/// <seealso cref="Lower16"/>
		public ulong Upper64 => ((ulong) this.High << 48) | (this.Low >> 16);

		/// <summary>Returns the 16 lower bits <c>....-........-....xxxx</c></summary>
		/// <seealso cref="Upper64"/>
		public ushort Lower16 => unchecked((ushort) this.Low);

		/// <summary>Returns the 32 lower bits <c>....-........-xxxxxxxx</c></summary>
		/// <seealso cref="Upper48"/>
		public uint Lower32 => unchecked((uint) this.Low);

		/// <summary>Returns the 48 lower bits <c>....-....xxxx-xxxxxxxx</c></summary>
		/// <seealso cref="Upper32"/>
		public Uuid48 Lower48 => new(this.Low & MASK_48);

		/// <summary>Returns the 64 lower bits <c>....-xxxxxxxx-xxxxxxxx</c></summary>
		/// <seealso cref="Upper16"/>
		public ulong Lower64 => this.Low;

		#region Constructors...

		/// <summary>Pack components into an 80-bit UUID</summary>
		/// <param name="a"><c>XXXX-........-........</c></param>
		/// <param name="b"><c>....-XXXXXXXX-XXXXXXXX</c></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, ulong b)
		{
			this.High = a;
			this.Low = b;
		}

		/// <summary>Pack components into an 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, long b)
		{
			this.High = a;
			this.Low = (ulong) b;
		}

		/// <summary>Pack components into an 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(int a, long b)
		{
			Contract.Debug.Requires((uint) a <= 0xFFFF);
			this.High = (ushort) a;
			this.Low = (ulong) b;
		}

		/// <summary>Pack components into an 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-........</param>
		/// <param name="c">....-........-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, uint b, uint c)
		{
			this.High = a;
			this.Low = ((ulong) b) << 32 | c;
		}

		/// <summary>Pack components into an 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-........</param>
		/// <param name="c">....-........-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(int a, int b, int c)
		{
			Contract.Debug.Requires((uint) a <= 0xFFFF);
			this.High = (ushort) a;
			this.Low = ((ulong) (uint) b) << 32 | (uint) c;
		}

		/// <summary>Pack components into an 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXX....-........</param>
		/// <param name="c">....-....XXXX-........</param>
		/// <param name="d">....-........-XXXX....</param>
		/// <param name="e">....-........-....XXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, ushort b, ushort c, ushort d, ushort e)
		{
			this.High = a;
			this.Low = ((ulong) b) << 48 | ((ulong) c) << 32 | ((ulong) d) << 16 | e;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromInt16(short value) => new(0, value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUInt16(ushort value) => new(0, value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromInt32(int value) => new(0, value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUInt32(uint value) => new(0, value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromInt48(long value) => FromUInt48((ulong) value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUInt48(ulong value) => value < (1UL << 48) ? new(0, value) : throw new ArgumentOutOfRangeException(nameof(value), "Value must be less than 2^48.");

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromInt64(long value) => new(0, value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUInt64(ulong value) => new(0, value);

#if NET8_0_OR_GREATER

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromInt128(Int128 value) => FromUInt128(unchecked((UInt128) value));

		[Pure]
		public static Uuid80 FromUInt128(UInt128 value)
		{
			if (value >= UInt128.One << 80)
			{
				throw new ArgumentOutOfRangeException(nameof(value), "Value must be less than 2^80.");
			}

			Span<byte> tmp = stackalloc byte[16];
			System.Buffers.Binary.BinaryPrimitives.WriteUInt128BigEndian(tmp, value);
			// [0..5] must be ZERO !
			// [6..15] contains the valid bits
			return ReadUnsafe(tmp);
		}

#endif

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException FailInvalidBufferSize([InvokerParameterName] string arg)
		{
			return ThrowHelper.ArgumentException(arg, "Value must be 10 bytes long");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static FormatException FailInvalidFormat()
		{
			return ThrowHelper.FormatException("Invalid " + nameof(Uuid80) + " format");
		}

		/// <summary>Generate a new random 80-bit UUID.</summary>
		/// <remarks>If you need sequential or cryptographic uuids, you should use a different generator.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 NewUuid()
		{
			unsafe
			{
				// we use Guid.NewGuid() as a source of ~128 bits of entropy, and we fold the extra 48 bits onto the first 80 bits
				var x = Guid.NewGuid();
				ushort* p = (ushort*) &x;
				return new((ushort) (p[0] ^ p[5]), (ushort) (p[1] ^ p[6]), (ushort) (p[2] ^ p[7]), p[3], p[4]);
			}
		}

		#endregion

		#region Decomposition...

		/// <summary>Split into two fragments</summary>
		/// <param name="a">xxxx-........-........</param>
		/// <param name="b">....-xxxxxxxx-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort a, out ulong b)
		{
			a = this.High;
			b = this.Low;
		}

		/// <summary>Split into three fragments</summary>
		/// <param name="a">xxxx-........-........</param>
		/// <param name="b">....-xxxxxxxx-........</param>
		/// <param name="c">....-........-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort a, out uint b, out uint c)
		{
			a = this.High;
			b = (uint) (this.Low >> 32);
			c = (uint) this.Low;
		}

		#endregion

		#region Factory...

		/// <summary>Creates a <see cref="Uuid80"/> from the upper 16-bit and lower 64-bit parts</summary>
		/// <param name="hi">16 upper bits (<c>xxxx-........-........</c>)</param>
		/// <param name="low">64 lower bits (<c>....-xxxxxxxx-xxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUpper16Lower64(ushort hi, ulong low)
		{
			return new(hi, low);
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the upper 32-bit and lower 48-bit parts</summary>
		/// <param name="hi">32 upper bits (<c>xxxx-xxxx....-........</c>)</param>
		/// <param name="low">48 lower bits (<c>....-....xxxx-xxxxxxxx</c>)</param>
		/// <remarks>The upper 16 bits of <paramref name="low"/> will be ignored.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUpper32Lower48(uint hi, ulong low)
		{
			return new((ushort) (hi >> 16), ((ulong) hi << 48) | (low & MASK_48));
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the upper 48-bit and lower 32-bit parts</summary>
		/// <param name="hi">48 upper bits (<c>xxxx-xxxxxxxx-........</c>)</param>
		/// <param name="low">32 lower bits (<c>....-........-xxxxxxxx</c>)</param>
		/// <remarks>The upper 16 bits of <paramref name="hi"/> will be ignored.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUpper48Lower32(ulong hi, uint low)
		{
			return new(unchecked((ushort) (hi >> 32)), (hi << 32) | low);
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the upper 64-bit and lower 16-bit parts</summary>
		/// <param name="hi">64 upper bits (<c>xxxx-xxxxxxxx-xxxx....</c>)</param>
		/// <param name="low">16 lower bits (<c>....-........-....xxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromUpper64Lower16(ulong hi, ushort low)
		{
			return new(unchecked((ushort) (hi >> 48)), (hi << 16) | low);
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the lower 16 bits, with the upper 64 bits all set to <c>0</c></summary>
		/// <param name="low">16 lower bits (<c>....-........-....xxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromLower16(ushort low)
		{
			return new(0, low);
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the lower 32 bits, with the upper 48 bits all set to <c>0</c></summary>
		/// <param name="low">32 lower bits (<c>....-........-xxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromLower32(uint low)
		{
			return new(0, low);
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the lower 48 bits, with the upper 32 bits all set to <c>0</c></summary>
		/// <param name="low">48 lower bits (<c>....-....xxxx-xxxxxxxx</c>)</param>
		/// <remarks>The upper 16 bits of <paramref name="low"/> will be ignored.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromLower48(ulong low)
		{
			return new(0, low & Uuid80.MASK_48);
		}

		/// <summary>Creates a <see cref="Uuid80"/> from the lower 64 bits, with the upper 16 bits all set to <c>0</c></summary>
		/// <param name="low">64 lower bits (<c>....-xxxxxxxx-xxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 FromLower64(ulong low)
		{
			return new(0, low);
		}

		#endregion

		#region Reading...

		/// <summary>Reads an 80-bit UUID from a byte array</summary>
		/// <param name="value">Array of bytes that is either empty, or holds at least 10 bytes</param>
		/// <returns>Corresponding <see cref="Uuid80"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 10 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Read(byte[]? value) => Read(new ReadOnlySpan<byte>(value));

		/// <summary>Reads an 80-bit UUID from slice of memory</summary>
		/// <param name="value">Slice of bytes that is either empty, or holds at least 10 bytes</param>
		/// <returns>Corresponding <see cref="Uuid80"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 10 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Read(Slice value) => Read(value.Span);

		/// <summary>Reads an 80-bit UUID from slice of memory</summary>
		/// <param name="value">Span of bytes that is either empty, or holds at least 10 bytes</param>
		/// <returns>Corresponding <see cref="Uuid80"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 10 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure]
		public static Uuid80 Read(ReadOnlySpan<byte> value)
			=> value.Length == 0 ? Empty
			 : value.Length >= SizeOf ? ReadUnsafe(value)
			 : throw FailInvalidBufferSize(nameof(value));

		/// <summary>Reads an 80-bit UUID from slice of memory</summary>
		/// <param name="value">Array of bytes that is either empty, or holds at least 10 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid80"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>10</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 10 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead(byte[]? value, out Uuid80 result) => TryRead(new ReadOnlySpan<byte>(value), out result);

		/// <summary>Reads an 80-bit UUID from slice of memory</summary>
		/// <param name="value">Slice of bytes that is either empty, or holds at least 10 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid80"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>10</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 10 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead(Slice value, out Uuid80 result) => TryRead(value.Span, out result);

		/// <summary>Reads an 80-bit UUID from slice of memory</summary>
		/// <param name="value">Span of bytes that is either empty, or holds at least 10 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid80"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>10</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 10 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure]
		public static bool TryRead(ReadOnlySpan<byte> value, out Uuid80 result)
		{
			switch (value.Length)
			{
				case 0:
				{
					result = default;
					return true;
				}
				case >= SizeOf:
				{
					result = ReadUnsafe(value);
					return true;
				}
				default:
				{
					result = default;
					return false;
				}
			}
		}

		#endregion

		#region Parsing...

		/// <summary>Parse a string representation of an <see cref="Uuid80"/></summary>
		/// <param name="input">String in either formats: <c>""</c>, <c>"xxxx-xxxxxxxx-xxxxxxxx"</c>, <c>"xxxxxxxxxxxxxxxxxxxx"</c>, <c>"{xxxx-xxxxxxxx-xxxxxxxx}"</c>, <c>"{xxxxxxxxxxxxxxxxxxxx}"</c></param>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid80.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Parse(string input)
		{
			Contract.NotNull(input);
			if (!TryParse(input.AsSpan(), out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an <see cref="Uuid80"/></summary>
		/// <param name="input">String in either formats: <c>""</c>, <c>"xxxx-xxxxxxxx-xxxxxxxx"</c>, <c>"xxxxxxxxxxxxxxxxxxxx"</c>, <c>"{xxxx-xxxxxxxx-xxxxxxxx}"</c>, <c>"{xxxxxxxxxxxxxxxxxxxx}"</c></param>
		/// <param name="provider">This parameter is ignored</param>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid80.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid80 Parse(string input, IFormatProvider? provider)
			=> Parse(input);

		/// <summary>Parse a string representation of an <see cref="Uuid80"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Parse(ReadOnlySpan<char> input)
		{
			if (!TryParse(input, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an <see cref="Uuid80"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid80 Parse(ReadOnlySpan<char> input, IFormatProvider? provider)
			=> Parse(input);

		/// <summary>Try parsing a string representation of an <see cref="Uuid80"/></summary>
		[Pure]
		public static bool TryParse([NotNullWhen(true)] string? input, out Uuid80 result)
		{
			if (input == null)
			{
				result = default;
				return false;
			}
			return TryParse(input.AsSpan(), out result);
		}

		/// <summary>Try parsing a string representation of an <see cref="Uuid80"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse([NotNullWhen(true)] string? input, IFormatProvider? provider, out Uuid80 result)
			=> TryParse(input, out result);

		/// <summary>Try parsing a string representation of an <see cref="Uuid80"/></summary>
		[Pure]
		public static bool TryParse(ReadOnlySpan<char> input, out Uuid80 result)
		{
			// we support the following formats: "{hex4-hex8-hex8}", "{hex20}", "hex4-hex8-hex8", "hex20" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// note: Guid.Parse accepts leading and trailing whitespaces, so we have to replicate the behavior here
			input = input.Trim();

			// remove "{...}" if there is any
			if (input.Length > 2 && input[0] == '{' && input[^1] == '}')
			{
				input = input[1..^1];
			}

			switch (input.Length)
			{
				case 0:
				{ // empty is NOT allowed
					result = default;
					return false;
				}
				case 20:
				{ // xxxxxxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(input, separator: false, out result);
				}
				case 22:
				{ // xxxx-xxxxxxxx-xxxxxxxx
					if (input[4] != '-' || input[13] != '-')
					{
						result = default;
						return false;
					}
					return TryDecode16Unsafe(input, separator: true, out result);
				}
				default:
				{
					result = default;
					return false;
				}
			}
		}

		/// <summary>Try parsing a string representation of an Uuid80</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out Uuid80 result)
			=> TryParse(input, out result);

		/// <summary>Try parsing a UTF-8 string representation of an <see cref="Uuid80"/></summary>
		[Pure]
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Uuid80 result)
		{
			// we support the following formats: "{hex4-hex8-hex8}", "{hex20}", "hex4-hex8-hex8", "hex20" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// remove "{...}" if there is any
			if (utf8Text.Length > 2 && utf8Text[0] == '{' && utf8Text[^1] == '}')
			{
				utf8Text = utf8Text[1..^1];
			}

			switch (utf8Text.Length)
			{
				case 0:
				{ // empty is NOT allowed
					result = default;
					return false;
				}
				case 20:
				{ // xxxxxxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(utf8Text, separator: false, out result);
				}
				case 22:
				{ // xxxx-xxxxxxxx-xxxxxxxx
					if (utf8Text[4] != '-' || utf8Text[13] != '-')
					{
						result = default;
						return false;
					}
					return TryDecode16Unsafe(utf8Text, separator: true, out result);
				}
				default:
				{
					result = default;
					return false;
				}
			}
		}

		#endregion

		#region IFormattable...

		/// <summary>Returns a newly allocated <see cref="Slice"/> that represents this UUID</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			var writer = new SliceWriter(SizeOf);
			WriteUnsafe(this.High, this.Low, writer.AllocateSpan(SizeOf));
			return writer.ToSlice();
		}

		/// <summary>Writes this UUID to the specified writer</summary>
		public void WriteTo(ref SliceWriter writer)
		{
			WriteUnsafe(this.High, this.Low, writer.AllocateSpan(SizeOf));
		}

		/// <summary>Returns newly allocated array of bytes that represents this UUID</summary>
		[Pure]
		public byte[] ToByteArray()
		{
			var tmp = new byte[SizeOf];
			unsafe
			{
				fixed (byte* ptr = &tmp[0])
				{
					UnsafeHelpers.StoreUInt16BE(ptr, this.High);
					UnsafeHelpers.StoreUInt64BE(ptr + 2, this.Low);
				}
			}
			return tmp;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format "xxxxxxxx-xxxxxxxx", where 'x' is a lower-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString("D", null);
		}

		/// <summary>Returns a string representation of the value of this <see cref="Uuid80"/> instance, according to the provided format specifier.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "B", "X", "G", "Z" or "N". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid80"/>, using the specified format.</returns>
		/// <remarks>See <see cref="ToString(string, IFormatProvider)"/> for a description of the different formats</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid80"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid80"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as three groups of hexadecimal digits, separated by a hyphen: "aaaa-bbbbbbbb-cccccccc" (22 characters).</p>
		/// <p>The <b>X</b> and <b>N</b> format encodes the value as a single group of 20 hexadecimal digits: "aaaabbbbbbbbcccccccc" (20 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with '{' and '}': "{aaaa-bbbbbbbb-cccccccc}" (24 characters).</p>
		/// </example>
		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = "D";

			switch(format)
			{
				case "D":
				{ // Default format is "XXXX-XXXXXXXX-XXXXXXXX"
					return Encode16(this.High, this.Low, separator: true, quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxx-xxxxxxxx-xxxxxxxx"
					return Encode16(this.High, this.Low, separator: true, quotes: false, upper: false);
				}
				case "X": //note: Guid.ToString("X") returns "{0x.....,0x.....,...}" but we prefer the "N" format
				case "N":
				{ // "XXXXXXXXXXXXXXXXXXXX"
					return Encode16(this.High, this.Low, separator: false, quotes: false, upper: true);
				}
				case "x":
				case "n":
				{ // "xxxxxxxxxxxxxxxxxxxx"
					return Encode16(this.High, this.Low, separator: false, quotes: false, upper: false);
				}

				case "B":
				{ // "{XXXX-XXXXXXXX-XXXXXXXX}"
					return Encode16(this.High, this.Low, separator: true, quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxx-xxxxxxxx-xxxxxxxx}"
					return Encode16(this.High, this.Low, separator: true, quotes: true, upper: false);
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid80) + " format specification.");
				}
			}
		}

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider = null)
		{
			//PERF: implement this without memory allocations!
			string literal;

			switch(format)
			{
				case "D":
				{ // Default format is "XXXX-XXXXXXXX-XXXXXXXX"
					literal = Encode16(this.High, this.Low, separator: true, quotes: false, upper: true);
					break;
				}
				case "d":
				{ // Default format is "xxxx-xxxxxxxx-xxxxxxxx"
					literal = Encode16(this.High, this.Low, separator: true, quotes: false, upper: false);
					break;
				}
				case "X": //note: Guid.ToString("X") returns "{0x.....,0x.....,...}" but we prefer the "N" format
				case "N":
				{ // "XXXXXXXXXXXXXXXXXXXX"
					literal = Encode16(this.High, this.Low, separator: false, quotes: false, upper: true);
					break;
				}
				case "x":
				case "n":
				{ // "xxxxxxxxxxxxxxxxxxxx"
					literal = Encode16(this.High, this.Low, separator: false, quotes: false, upper: false);
					break;
				}

				case "B":
				{ // "{XXXX-XXXXXXXX-XXXXXXXX}"
					literal = Encode16(this.High, this.Low, separator: true, quotes: true, upper: true);
					break;
				}
				case "b":
				{ // "{xxxx-xxxxxxxx-xxxxxxxx}"
					literal = Encode16(this.High, this.Low, separator: true, quotes: true, upper: false);
					break;
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid80) + " format specification.");
				}
			}

			return literal.TryCopyTo(destination, out charsWritten);
		}

		#endregion

		#region IEquatable / IComparable...

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			switch (obj)
			{
				case Uuid80 uuid: return Equals(uuid);
				case Slice bytes: return Equals(bytes);
			}
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			return this.High.GetHashCode() ^ this.Low.GetHashCode();
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid80 other)
		{
			return this.High == other.High && this.Low == other.Low;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid80 other)
		{
			int cmp = this.High.CompareTo(other.High);
			if (cmp == 0) cmp = this.Low.CompareTo(other.Low);
			return cmp;
		}

		/// <inheritdoc />
		public bool Equals(Slice other) => TryRead(other.Span, out var res) && res == this;

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#else
		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns><c>true</c> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <c>false</c>.</returns>
#endif
		public bool Equals(ReadOnlySpan<byte> other) => TryRead(other, out var res) && res == this;

		#endregion

		#region Base16 encoding...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static char HexToLowerChar(uint a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'a') : (char)(a + '0');
		}

		private static unsafe char* HexToLowerChars(char* ptr, ushort a)
		{
			Contract.Debug.Requires(ptr != null);
			ptr[0] = HexToLowerChar((uint) a >> 12);
			ptr[1] = HexToLowerChar((uint) a >> 8);
			ptr[2] = HexToLowerChar((uint) a >> 4);
			ptr[3] = HexToLowerChar(a);
			return ptr + 4;
		}

		private static unsafe char* HexToLowerChars(char* ptr, uint a)
		{
			Contract.Debug.Requires(ptr != null);
			ptr[0] = HexToLowerChar(a >> 28);
			ptr[1] = HexToLowerChar(a >> 24);
			ptr[2] = HexToLowerChar(a >> 20);
			ptr[3] = HexToLowerChar(a >> 16);
			ptr[4] = HexToLowerChar(a >> 12);
			ptr[5] = HexToLowerChar(a >> 8);
			ptr[6] = HexToLowerChar(a >> 4);
			ptr[7] = HexToLowerChar(a);
			return ptr + 8;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static char HexToUpperChar(uint a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'A') : (char)(a + '0');
		}

		private static unsafe char* Hex16ToUpperChars(char* ptr, ushort a)
		{
			Contract.Debug.Requires(ptr != null);
			ptr[0] = HexToUpperChar((uint) a >> 12);
			ptr[1] = HexToUpperChar((uint) a >> 8);
			ptr[2] = HexToUpperChar((uint) a >> 4);
			ptr[3] = HexToUpperChar(a);
			return ptr + 4;
		}

		private static unsafe char* Hex32ToUpperChars(char* ptr, uint a)
		{
			Contract.Debug.Requires(ptr != null);
			ptr[0] = HexToUpperChar(a >> 28);
			ptr[1] = HexToUpperChar(a >> 24);
			ptr[2] = HexToUpperChar(a >> 20);
			ptr[3] = HexToUpperChar(a >> 16);
			ptr[4] = HexToUpperChar(a >> 12);
			ptr[5] = HexToUpperChar(a >> 8);
			ptr[6] = HexToUpperChar(a >> 4);
			ptr[7] = HexToUpperChar(a);
			return ptr + 8;
		}

		[Pure]
		private static unsafe string Encode16(ushort hi, ulong lo, bool separator, bool quotes, bool upper)
		{
			int size = 20 + (separator ? 2 : 0) + (quotes ? 2 : 0);
			char* buffer = stackalloc char[24];

			char* ptr = buffer;
			if (quotes) *ptr++ = '{';
			ptr = upper
				? Hex16ToUpperChars(ptr, hi)
				: HexToLowerChars(ptr, hi);
			if (separator) *ptr++ = '-';
			ptr = upper
				? Hex32ToUpperChars(ptr, (uint) (lo >> 32))
				: HexToLowerChars(ptr, (uint) (lo >> 32));
			if (separator) *ptr++ = '-';
			ptr = upper
				? Hex32ToUpperChars(ptr, (uint) lo)
				: HexToLowerChars(ptr, (uint) lo);
			if (quotes) *ptr++ = '}';

			Contract.Debug.Ensures(ptr == buffer + size);
			return new(buffer, 0, size);
		}

		private const int INVALID_CHAR = -1;

		[Pure]
		private static int CharToHex(char c) => c switch
		{
			<= '9' => c >= '0' ? (c - 48) : INVALID_CHAR,
			<= 'F' => c >= 'A' ? (c - 55) : INVALID_CHAR,
			<= 'f' => c >= 'a' ? (c - 87) : INVALID_CHAR,
			_ => INVALID_CHAR
		};

		private static bool TryCharsToHex16(ReadOnlySpan<char> chars, out ushort result)
		{
			int word = 0;
			for (int i = 0; i < 4; i++)
			{
				int a = CharToHex(chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (ushort) word;
			return true;
		}

		private static bool TryCharsToHex16(ReadOnlySpan<byte> chars, out ushort result)
		{
			int word = 0;
			for (int i = 0; i < 4; i++)
			{
				int a = CharToHex((char) chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (ushort) word;
			return true;
		}

		private static bool TryCharsToHex32(ReadOnlySpan<char> chars, out uint result)
		{
			int word = 0;
			for (int i = 0; i < 8; i++)
			{
				int a = CharToHex(chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (uint)word;
			return true;
		}

		private static bool TryCharsToHex32(ReadOnlySpan<byte> chars, out uint result)
		{
			int word = 0;
			for (int i = 0; i < 8; i++)
			{
				int a = CharToHex((char) chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (uint)word;
			return true;
		}

		private static bool TryDecode16Unsafe(ReadOnlySpan<char> chars, bool separator, out Uuid80 result)
		{
			// aaaabbbbbbbbcccccccc
			// aaaa-bbbbbbbb-cccccccc
			if ((!separator || (chars[4] == '-' && chars[13] == '-'))
			 && TryCharsToHex16(chars, out ushort hi)
			 && TryCharsToHex32(chars.Slice(separator ? 5 : 4), out uint med)
			 && TryCharsToHex32(chars.Slice(separator ? 14 : 12), out uint lo))
			{
				result = new(hi, med, lo);
				return true;
			}
			result = default(Uuid80);
			return false;
		}

		private static bool TryDecode16Unsafe(ReadOnlySpan<byte> chars, bool separator, out Uuid80 result)
		{
			// aaaabbbbbbbbcccccccc
			// aaaa-bbbbbbbb-cccccccc
			if ((!separator || (chars[4] == '-' && chars[13] == '-'))
			 && TryCharsToHex16(chars, out ushort hi)
			 && TryCharsToHex32(chars.Slice(separator ? 5 : 4), out uint med)
			 && TryCharsToHex32(chars.Slice(separator ? 14 : 12), out uint lo))
			{
				result = new(hi, med, lo);
				return true;
			}
			result = default(Uuid80);
			return false;
		}

		#endregion

		#region Unsafe I/O...

		internal static Uuid80 ReadUnsafe(ReadOnlySpan<byte> src)
		{
			//Paranoid.Requires(src.Length >= 10)
#if NET8_0_OR_GREATER
			return new(
				UnsafeHelpers.ReadUInt16BE(in src[0]),
				UnsafeHelpers.ReadUInt64BE(in src[2])
			);
#else
			unsafe
			{
				fixed (byte* ptr = src)
				{
					return new Uuid80(
						UnsafeHelpers.LoadUInt16BE(ptr),
						UnsafeHelpers.LoadUInt64BE(ptr + 2)
					);
				}
			}
#endif
		}

		internal static void WriteUnsafe(ushort hi, ulong lo, Span<byte> buffer)
		{
			//Paranoid.Requires(buffer.Length >= 10);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
				{
					UnsafeHelpers.StoreUInt16BE(ptr, hi);
					UnsafeHelpers.StoreUInt64BE(ptr + 2, lo);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteToUnsafe(Span<byte> buf)
		{
			WriteUnsafe(this.High, this.Low, buf);
		}

		public void WriteTo(Span<byte> destination)
		{
			if (destination.Length < SizeOf) throw FailInvalidBufferSize(nameof(destination));
			WriteUnsafe(this.High, this.Low, destination);
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/>, if it is large enough.</summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 10 bytes</param>
		/// <returns><c>true</c> if the destination is large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> destination)
		{
			if (destination.Length < SizeOf) return false;
			WriteUnsafe(this.High, this.Low, destination);
			return true;
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/>, if it is large enough.</summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 10 bytes</param>
		/// <param name="bytesWritten">Receives the number of bytes written (either <c>10</c> or <c>0</c>)</param>
		/// <returns><c>true</c> if the destination is large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> destination, out int bytesWritten)
		{
			if (destination.Length < SizeOf)
			{
				bytesWritten = 0;
				return false;
			}

			WriteUnsafe(this.High, this.Low, destination);
			bytesWritten = SizeOf;
			return true;
		}

#if NET8_0_OR_GREATER

		/// <summary>Return the equivalent <see cref="UInt128"/></summary>
		/// <remarks>The integer correspond to the big-endian version of this instance, serialized as a byte array</remarks>
		public UInt128 ToUInt128()
		{
			Span<byte> tmp = stackalloc byte[16];
			tmp.Clear();
			WriteUnsafe(this.High, this.Low, tmp);
			return System.Buffers.Binary.BinaryPrimitives.ReadUInt128BigEndian(tmp);
		}

		/// <summary>Return the equivalent <see cref="Int128"/></summary>
		/// <remarks>The integer correspond to the big-endian version of this instance, serialized as a byte array</remarks>
		public Int128 ToInt128()
		{
			Span<byte> tmp = stackalloc byte[16];
			tmp.Clear();
			WriteUnsafe(this.High, this.Low, tmp);
			return System.Buffers.Binary.BinaryPrimitives.ReadInt128BigEndian(tmp);
		}

#endif

		#endregion

		#region Operators...

		public static bool operator ==(Uuid80 left, Uuid80 right)
		{
			return left.High == right.High & left.Low == right.Low;
		}

		public static bool operator !=(Uuid80 left, Uuid80 right)
		{
			return left.High != right.High | left.Low != right.Low;
		}

		public static bool operator >(Uuid80 left, Uuid80 right)
		{
			return left.High > right.High || (left.High == right.High && left.Low > right.Low);
		}

		public static bool operator >=(Uuid80 left, Uuid80 right)
		{
			return left.High > right.High || (left.High == right.High && left.Low >= right.Low);
		}

		public static bool operator <(Uuid80 left, Uuid80 right)
		{
			return left.High < right.High || (left.High == right.High && left.Low < right.Low);
		}

		public static bool operator <=(Uuid80 left, Uuid80 right)
		{
			return left.High < right.High || (left.High == right.High && left.Low <= right.Low);
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid80 operator +(Uuid80 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				ushort hi = left.High;
				ulong lo = left.Low + (ulong) right;
				if (lo < left.Low) // overflow!
				{
					++hi;
				}
				return new(hi, lo);
			}
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid80 operator +(Uuid80 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				ushort hi = left.High;
				ulong lo = left.Low + right;
				if (lo < left.Low) // overflow!
				{
					++hi;
				}
				return new(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid80 operator -(Uuid80 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				ushort hi = left.High;
				ulong lo = left.Low - (ulong) right;
				if (lo > left.Low) // overflow!
				{
					--hi;
				}
				return new(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid80 operator -(Uuid80 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				ushort hi = left.High;
				ulong lo = left.Low - right;
				if (lo > left.Low) // overflow!
				{
					--hi;
				}
				return new(hi, lo);
			}
		}

		/// <summary>Increments the value of this instance</summary>
		public static Uuid80 operator ++(Uuid80 value)
		{
			return value + 1;
		}

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid80 operator --(Uuid80 value)
		{
			return value - 1;
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid80 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid80>, IComparer<Uuid80>
		{

			public static readonly Comparer Default = new();

			private Comparer()
			{ }

			/// <inheritdoc />
			public bool Equals(Uuid80 x, Uuid80 y)
			{
				return x.High == y.High & x.Low == y.Low;
			}

			/// <inheritdoc />
			public int GetHashCode(Uuid80 obj)
			{
				return obj.GetHashCode();
			}

			/// <inheritdoc />
			public int Compare(Uuid80 x, Uuid80 y)
			{
				return x.CompareTo(y);
			}
			
		}

		/// <summary>Generates 80-bit UUIDs using a secure random number generator</summary>
		/// <remarks>Methods of this type are thread-safe.</remarks>
		[PublicAPI]
		public sealed class RandomGenerator
		{

			/// <summary>Default instance of a random generator</summary>
			/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
			public static readonly RandomGenerator Default = new();

			private RandomNumberGenerator Rng { get; }

			/// <summary>Constructs a new random UUID generator</summary>
			public RandomGenerator()
				: this(null)
			{ }

			/// <summary>Constructs a new random UUID generator, using a specific random number generator</summary>
			public RandomGenerator(RandomNumberGenerator? generator)
			{
				this.Rng = generator ?? RandomNumberGenerator.Create();
			}

			/// <summary>Return a new random 80-bit UUID</summary>
			/// <returns>Uuid80 that contains 80 bits worth of randomness.</returns>
			/// <remarks>
			/// <p>This method needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each thread.</p>
			/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
			/// </remarks>
			[Pure]
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Uuid80 NewUuid()
			{
				lock (this.Rng)
				{
					Span<byte> scratch = stackalloc byte[SizeOf];
					// get 10 bytes of randomness (0x00 is allowed)
					this.Rng.GetBytes(scratch);
					// read back
					return ReadUnsafe(scratch);
				}
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
