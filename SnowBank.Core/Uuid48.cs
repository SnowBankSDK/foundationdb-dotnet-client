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
	using System.Globalization;
	using System.Numerics;
	using System.Security.Cryptography;
	using SnowBank.Data.Binary;
	using SnowBank.Text;

	/// <summary>Represents a 48-bit UUID that is stored in high-endian format on the wire</summary>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct Uuid48 : IEquatable<Uuid48>, IComparable<Uuid48>, IEquatable<ulong>, IComparable<ulong>, IEquatable<long>, IComparable<long>, IEquatable<uint>, IComparable<uint>, IEquatable<int>, IComparable<int>, IEquatable<Slice>, ISpanFormattable, ISpanEncodable
#if NET7_0_OR_GREATER
		// generic math interfaces and ISpanParsable (.NET 7+); the operators themselves are plain operators and remain available on all targets
		, IComparisonOperators<Uuid48, Uuid48, bool>
		, IAdditiveIdentity<Uuid48, int>
		, IAdditiveIdentity<Uuid48, uint>
		, IAdditiveIdentity<Uuid48, long>
		, IAdditiveIdentity<Uuid48, ulong>
		, IIncrementOperators<Uuid48>
		, IAdditionOperators<Uuid48, int, Uuid48>
		, IAdditionOperators<Uuid48, long, Uuid48>
		, IAdditionOperators<Uuid48, uint, Uuid48>
		, IAdditionOperators<Uuid48, ulong, Uuid48>
		, IDecrementOperators<Uuid48>
		, ISubtractionOperators<Uuid48, int, Uuid48>
		, ISubtractionOperators<Uuid48, long, Uuid48>
		, ISubtractionOperators<Uuid48, uint, Uuid48>
		, ISubtractionOperators<Uuid48, ulong, Uuid48>
		, IBitwiseOperators<Uuid48, Uuid48, Uuid48>
		, ISpanParsable<Uuid48>
#endif
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		private const ulong MASK_48 = 0xFFFFFFFFFFFFul;

		/// <summary><see cref="Uuid48"/> with all bits set to <c>0</c> (<c>"0000-00000000"</c>)</summary>
		public static readonly Uuid48 Empty;

		/// <summary><see cref="Uuid48"/> with only bit 0 set to <c>1</c> (<c>"0000-00000001"</c>)</summary>
		public static readonly Uuid48 One = new(1);

		/// <summary><see cref="Uuid48"/> with all bits set to <c>1</c> (<c>"FFFF-FFFFFFFF"</c>)</summary>
		public static readonly Uuid48 AllBitsSet = new(MASK_48);

		/// <summary><see cref="Uuid48"/> with all bits set to <c>0</c> (<c>"0000-00000000"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid48 MinValue;

		/// <summary><see cref="Uuid48"/> with all bits set to <c>1</c> (<c>"FFFF-FFFFFFFF"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid48 MaxValue = new(MASK_48);

		/// <summary>Maximum integer value that can be represented by a <see cref="Uuid48"/> (2^48 - 1)</summary>
		public const ulong MaxRawValue = MASK_48;

		/// <summary>Size is 6 bytes</summary>
		public const int SizeOf = 6;

		//note: this will be in host order (so probably Little-Endian) in order to simplify parsing and ordering

		private readonly ulong Value;

		/// <summary>Returns the 16 upper bits <c>xxxx-........</c></summary>
		/// <seealso cref="Lower32"/>
		[Pure]
		public ushort Upper16 => unchecked((ushort) (this.Value >> 32));

		/// <summary>Returns the 32 upper bits <c>xxxx-xxxx....</c></summary>
		/// <seealso cref="Lower16"/>
		[Pure]
		public uint Upper32 => unchecked((uint) (this.Value >> 16));

		/// <summary>Returns the 16 lower bits <c>....-....xxxx</c></summary>
		/// <seealso cref="Upper32"/>
		[Pure]
		public ushort Lower16 => unchecked((ushort) this.Value);

		/// <summary>Returns the 32 lower bits <c>....-xxxxxxxx</c></summary>
		/// <seealso cref="Upper16"/>
		[Pure]
		public uint Lower32 => unchecked((uint) this.Value);

		#region Constructors...

		/// <summary>Creates a new <see cref="Uuid48"/> from a 48-bit unsigned integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid48(ulong value) => this.Value = (value & MASK_48);

		/// <summary>Creates a new <see cref="Uuid48"/> from a 48-bit signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid48(long value) => this.Value = unchecked((ulong) value) & MASK_48;

		/// <summary>Creates a new <see cref="Uuid48"/> from two 32-bits components</summary>
		/// <param name="a">Upper 16 bits (<c>XXXX-........</c>)</param>
		/// <param name="b">Lower 32 bits (<c>....-XXXXXXXX</c>)</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid48(ushort a, uint b)
		{
			this.Value = ((ulong) a << 32) | b;
		}

		/// <summary>Creates a new <see cref="Uuid48"/> from three 16-bits components</summary>
		/// <param name="a">Upper 16 bits of the first part  (<c>xxxx-........</c>)</param>
		/// <param name="b">Middle 16 bits of the first part  (<c>....-xxxx....</c>)</param>
		/// <param name="c">Lower 16 bits of the second part (<c>....-xxxxxxxx</c>)</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid48(ushort a, ushort b, ushort c)
		{
			this.Value = ((ulong) a << 32) | ((ulong) b << 16) | c;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidBufferSize([InvokerParameterName] string arg) => ThrowHelper.ArgumentException(arg, "Value must be 6 bytes long");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidFormat() => ThrowHelper.FormatException($"Invalid {nameof(Uuid48)} format");

		/// <summary>Generate a new random 48-bit UUID.</summary>
		/// <remarks>If you need sequential or cryptographic uuids, you should use a different generator.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 NewUuid()
		{
			// we use Guid.NewGuid() as a source of ~128 bits of entropy
			var g = Guid.NewGuid();

			// fold both 64-bits parts into a single value
			ref ulong ptr = ref Unsafe.As<Guid, ulong>(ref g);
			var mixed64 = ptr ^ Unsafe.Add(ref ptr, 1);

			// spread the upper 16 bits three times other the lower 48 bits
			var mixed48 = mixed64 & MASK_48;
			var upper16 = mixed64 >> 48;
			mixed48 ^= (upper16 << 32);
			mixed48 ^= (upper16 << 16);
			mixed48 ^= upper16;

			return new Uuid48(mixed48);
		}

		/// <summary>Generates a new random 48-bit UUID, using the specified random number generator</summary>
		/// <param name="rng">Random number generator</param>
		/// <returns>A random <see cref="Uuid48"/> that is less than <see cref="Uuid48.MaxValue"/></returns>
		public static Uuid48 Random(Random rng) => new (rng.NextInt64(0, (long) (MASK_48 + 1)));

		/// <summary>Generates a new random 48-bit UUID, using the specified random number generator</summary>
		/// <param name="rng">Random number generator</param>
		/// <param name="minValue">The inclusive lower bound of the random UUID to be generated.</param>
		/// <param name="maxValue">The exclusive upper bound of the random UUID to be generated.</param>
		/// <returns>A random <see cref="Uuid48"/> that is greater than or equal to <paramref name="minValue"/> and less than <paramref name="maxValue"/></returns>
		public static Uuid48 Random(Random rng, Uuid48 minValue, Uuid48 maxValue) => Random(rng, minValue.Value, maxValue.Value);

		/// <summary>Generates a new random 48-bit UUID, using the specified random number generator</summary>
		/// <param name="rng">Random number generator</param>
		/// <param name="minValue">The inclusive lower bound of the random UUID to be generated.</param>
		/// <param name="maxValue">The exclusive upper bound of the random UUID to be generated.</param>
		/// <returns>A random <see cref="Uuid48"/> that is greater than or equal to <paramref name="minValue"/> and less than <paramref name="maxValue"/></returns>
		public static Uuid48 Random(Random rng, ulong minValue, ulong maxValue)
		{
			if (minValue >= maxValue || minValue > MASK_48) throw new ArgumentOutOfRangeException(nameof(minValue));
			if (maxValue > MASK_48 + 1) throw new ArgumentOutOfRangeException(nameof(minValue));

			ulong x = (ulong) rng.NextInt64(0, (long) (maxValue - minValue));
			x += minValue;
			return new Uuid48(x);
		}

		/// <summary>Generates a new random 48-bit UUID, using the specified random number generator</summary>
		/// <param name="rng">Random number generator</param>
		/// <param name="maxValue">The exclusive upper bound of the random UUID to be generated.</param>
		/// <returns>A random <see cref="Uuid48"/> that is less than <paramref name="maxValue"/></returns>
		public static Uuid48 Random(Random rng, Uuid48 maxValue) => Random(rng, maxValue.Value);

		/// <summary>Generates a new random 48-bit UUID, using the specified random number generator</summary>
		/// <param name="rng">Random number generator</param>
		/// <param name="maxValue">The exclusive upper bound of the random UUID to be generated.</param>
		/// <returns>A random <see cref="Uuid48"/> that is less than <paramref name="maxValue"/></returns>
		public static Uuid48 Random(Random rng, ulong maxValue)
		{
			return maxValue <= MASK_48 + 1 ? new Uuid48(rng.NextInt64(0, (long) maxValue)) : throw new ArgumentOutOfRangeException(nameof(maxValue));
		}

		#endregion

		#region Decomposition...

		/// <summary>Split into three 16-bits parts</summary>
		/// <param name="a">xxxx-........</param>
		/// <param name="b">....-xxxx....</param>
		/// <param name="c">....-....xxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort a, out ushort b, out ushort c)
		{
			ulong value = this.Value;
			a = unchecked((ushort) (value >> 32));
			b = unchecked((ushort) (value >> 16));
			c = unchecked((ushort) value);
		}

		#endregion

		#region Reading...

		/// <summary>Read a 48-bit UUID from a byte array</summary>
		/// <param name="value">Array of bytes that is either empty, or holds at least 6 bytes</param>
		/// <returns>Corresponding <see cref="Uuid48"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 6 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 Read(byte[] value) => Read(value.AsSpan());

		/// <summary>Read a 48-bit UUID from slice of memory</summary>
		/// <param name="value">Slice of bytes that is either empty, or holds at least 6 bytes</param>
		/// <returns>Corresponding <see cref="Uuid48"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 6 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 Read(Slice value) => Read(value.Span);

		/// <summary>Read a 48-bit UUID from slice of memory</summary>
		/// <param name="value">Span of bytes that is either empty, or holds at least 6 bytes</param>
		/// <returns>Corresponding <see cref="Uuid48"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 6 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure]
		public static Uuid48 Read(ReadOnlySpan<byte> value)
			=> value.Length == 0 ? default
			 : value.Length >= SizeOf ? new Uuid48(BinaryPrimitives.ReadUInt16BigEndian(value), BinaryPrimitives.ReadUInt32BigEndian(value[2..]))
			 : throw FailInvalidBufferSize(nameof(value));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ReadUnsafe(ReadOnlySpan<byte> value, out Uuid48 result)
		{
			Contract.Debug.Requires(value.Length == SizeOf);
			result = new(BinaryPrimitives.ReadUInt16BigEndian(value), BinaryPrimitives.ReadUInt32BigEndian(value[2..]));
		}

		/// <summary>Reads a 48-bit UUID from slice of memory</summary>
		/// <param name="value">Array of bytes that is either empty, or holds at least 6 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid48"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>6</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 6 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead(byte[]? value, out Uuid48 result) => TryRead(new ReadOnlySpan<byte>(value), out result);

		/// <summary>Reads a 48-bit UUID from slice of memory</summary>
		/// <param name="value">Slice of bytes that is either empty, or holds at least 6 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid48"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>6</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 6 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead(Slice value, out Uuid48 result) => TryRead(value.Span, out result);

		/// <summary>Reads a 48-bit UUID from slice of memory</summary>
		/// <param name="value">Span of bytes that is either empty, or holds at least 6 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid48"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>6</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 6 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure]
		public static bool TryRead(ReadOnlySpan<byte> value, out Uuid48 result)
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
					ReadUnsafe(value, out result);
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

		/// <summary>Parses a string into a <see cref="Uuid48"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 Parse(string input)
		{
			Contract.NotNull(input);
			return TryParse(input.AsSpan(), out var value) ? value : throw FailInvalidFormat();
		}

		/// <summary>Parses a string into a <see cref="Uuid48"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid48 Parse(string input, IFormatProvider? provider)
		{
			Contract.NotNull(input);
			return TryParse(input.AsSpan(), out var value) ? value : throw FailInvalidFormat();
		}

		/// <summary>Parses a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 Parse(ReadOnlySpan<char> input) => TryParse(input, out var value) ? value : throw FailInvalidFormat();

		/// <summary>Parses a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid48 Parse(ReadOnlySpan<char> input, IFormatProvider? provider) => TryParse(input, out var value) ? value : throw FailInvalidFormat();

		/// <summary>Returns a <see cref="Uuid48"/> from a <see cref="int"/></summary>
		/// <param name="value">Value that must be positive</param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		/// <remarks>The upper 16-bits are set to <c>0</c></remarks>
		/// <exception cref="OverflowException">If <paramref name="value"/> is negative</exception>
		[Pure]
		public static Uuid48 FromInt32(int value) => value >= 0 ? new(value) : throw new OverflowException();

		/// <summary>Returns a <see cref="Uuid48"/> from a <see cref="uint"/></summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		/// <remarks>The upper 16-bits are set to <c>0</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromUInt32(uint value) => new(value);

		/// <summary>Returns a <see cref="Uuid48"/> from a <see cref="long"/></summary>
		/// <param name="value">Value that must be positive and not greater than <see cref="MaxRawValue"/></param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		/// <exception cref="OverflowException">If <paramref name="value"/> is negative, or greater than <see cref="MaxRawValue"/></exception>
		[Pure]
		public static Uuid48 FromInt64(long value) => value is >= 0 and <= (long) MaxRawValue ? new(value) : throw new OverflowException();

		/// <summary>Returns a <see cref="Uuid48"/> from a <see cref="long"/></summary>
		/// <param name="value">Value that must not be greater than <see cref="MaxRawValue"/></param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		/// <exception cref="OverflowException">If <paramref name="value"/> is greater than <see cref="MaxRawValue"/></exception>
		[Pure]
		public static Uuid48 FromUInt64(ulong value) => value <= MaxRawValue ? new(value) : throw new OverflowException();

		/// <summary>Returns a <see cref="Uuid48"/> from a <see cref="int"/></summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		/// <remarks>The upper 16-bits are set to <c>0</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromLower32(int value) => new(((uint) value) & MASK_48);

		/// <summary>Returns a <see cref="Uuid48"/> from a <see cref="int"/></summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		/// <remarks>The upper 16-bits are set to <c>0</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromLower32(uint value) => new(value);

		/// <summary>Returns a <see cref="Uuid48"/> from the lower 48 bits of a <see cref="long"/></summary>
		/// <param name="value">Value to convert (upper 16 bits are ignored)</param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromLower48(long value) => new((ulong)value & MASK_48);

		/// <summary>Returns a <see cref="Uuid48"/> from the lower 48 bits of a <see cref="long"/></summary>
		/// <param name="value">Value to convert (upper 16 bits are ignored)</param>
		/// <returns>Equivalent <see cref="Uuid48"/></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromLower48(ulong value) => new(value & MASK_48);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromUpper16Lower32(ushort upper16, uint lower32) => new(((ulong) upper16 << 32) | lower32);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromUpper32Lower16(uint upper32, ushort lower16) => new(((ulong) upper32 << 16) | lower16);

		/// <summary>Parse a Base62 encoded string representation of an Uuid48</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromBase62(string buffer)
		{
			Contract.NotNull(buffer);
			return TryParseBase62(buffer.AsSpan(), out var value) ? value : throw FailInvalidFormat();
		}

		/// <summary>Parse a Base62 encoded string representation of an Uuid48</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48 FromBase62(ReadOnlySpan<char> buffer)
		{
			return TryParseBase62(buffer, out var value) ? value : throw FailInvalidFormat();
		}

		/// <summary>Tries to parse a string into a <see cref="Uuid48"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[Pure]
		public static bool TryParse([NotNullWhen(true)] string? input, out Uuid48 result)
		{
			if (input == null)
			{
				result = default;
				return false;
			}
			return TryParse(input.AsSpan(), out result);
		}

		/// <summary>Tries to parse a string into a <see cref="Uuid48"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse([NotNullWhen(true)] string? input, IFormatProvider? provider, out Uuid48 result)
			=> TryParse(input, out result);

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid48"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out Uuid48 result)
			=> TryParse(input, out result);

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid48"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[Pure]
		public static bool TryParse(ReadOnlySpan<char> input, out Uuid48 result)
		{
			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// note: Guid.Parse accepts leading and trailing whitespaces, so we have to replicate the behavior here
			input = input.Trim();

			switch (input.Length)
			{
				case 0:
				{ // empty is NOT allowed
					result = default;
					return false;
				}
				case 12:
				{ // xxxxxxxxxxxx
					return TryDecode16Unsafe(input, separator: false, out result);
				}
				case 13:
				{ // xxxx-xxxxxxxx
					if (input[4] != '-')
					{
						result = default;
						return false;
					}

					return TryDecode16Unsafe(input, separator: true, out result);
				}
				case 14:
				{ // {xxxxxxxxxxxx}
					if (input[0] != '{' || input[13] != '}')
					{
						result = default;
						return false;
					}
					return TryDecode16Unsafe(input[1..^1], separator: false, out result);
				}
				case 15:
				{ // {xxxx-xxxxxxxx}
					if (input[0] != '{' || input[5] != '-' || input[14] != '}')
					{
						result = default;
						return false;
					}
					return TryDecode16Unsafe(input[1..^1], separator: true, out result);
				}
				default:
				{
					result = default;
					return false;
				}
			}


			static bool TryDecode16Unsafe(ReadOnlySpan<char> chars, bool separator, out Uuid48 result)
			{
#if NET5_0_OR_GREATER
				if ((!separator || chars[4] == '-')
				    && ushort.TryParse(chars[..4], NumberStyles.HexNumber, null, out ushort hi)
				    && uint.TryParse(chars[(separator ? 5 : 4)..], NumberStyles.HexNumber, null, out uint lo))
#else
				if ((!separator || chars[4] == '-')
				    && ushort.TryParse(chars[..4].ToString(), NumberStyles.HexNumber, null, out ushort hi) // ushort.TryParse(ReadOnlySpan<char>) is not on netstandard2.0
				    && uint.TryParse(chars[(separator ? 5 : 4)..].ToString(), NumberStyles.HexNumber, null, out uint lo))
#endif
				{
					result = new Uuid48(hi, lo);
					return true;
				}
				result = default(Uuid48);
				return false;
			}

		}

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid48"/></summary>
		/// <param name="utf8Text">The span of characters to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="utf8Text" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="utf8Text" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[Pure]
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Uuid48 result)
		{
			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			switch (utf8Text.Length)
			{
				case 0:
				{ // empty is NOT allowed
					result = default;
					return false;
				}
				case 12:
				{ // xxxxxxxxxxxx
					return TryDecode16Unsafe(utf8Text, separator: false, out result);
				}
				case 13:
				{ // xxxx-xxxxxxxx
					if (utf8Text[4] != '-')
					{
						result = default;
						return false;
					}

					return TryDecode16Unsafe(utf8Text, separator: true, out result);
				}
				case 14:
				{ // {xxxxxxxxxxxx}
					if (utf8Text[0] != '{' || utf8Text[13] != '}')
					{
						result = default;
						return false;
					}
					return TryDecode16Unsafe(utf8Text[1..^1], separator: false, out result);
				}
				case 15:
				{ // {xxxx-xxxxxxxx}
					if (utf8Text[0] != '{' || utf8Text[5] != '-' || utf8Text[14] != '}')
					{
						result = default;
						return false;
					}
					return TryDecode16Unsafe(utf8Text[1..^1], separator: true, out result);
				}
				default:
				{
					result = default;
					return false;
				}
			}


			static bool TryDecode16Unsafe(ReadOnlySpan<byte> chars, bool separator, out Uuid48 result)
			{
#if NET5_0_OR_GREATER
				if ((!separator || chars[4] == '-')
				    && ushort.TryParse(chars[..4], NumberStyles.HexNumber, null, out ushort hi)
				    && uint.TryParse(chars[(separator ? 5 : 4)..], NumberStyles.HexNumber, null, out uint lo))
#else
				// netstandard2.0 has no UTF-8 span parsing: decode the ASCII hex bytes to a string first (the input is known ASCII here).
				if ((!separator || chars[4] == '-')
				    && ushort.TryParse(System.Text.Encoding.ASCII.GetString(chars[..4].ToArray()), NumberStyles.HexNumber, null, out ushort hi)
				    && uint.TryParse(System.Text.Encoding.ASCII.GetString(chars[(separator ? 5 : 4)..].ToArray()), NumberStyles.HexNumber, null, out uint lo))
#endif
				{
					result = new Uuid48(hi, lo);
					return true;
				}
				result = default(Uuid48);
				return false;
			}

		}

		/// <summary>Tries to parse a base-62 encoded literal into a <see cref="Uuid48"/></summary>
		public static bool TryParseBase62(string? input, out Uuid48 result)
		{
			if (input == null)
			{
				result = default;
				return false;
			}
			return TryParseBase62(input.AsSpan(), out result);
		}

		/// <summary>Tries to parse a base-62 encoded literal into a <see cref="Uuid48"/></summary>
		public static bool TryParseBase62(ReadOnlySpan<char> input, out Uuid48 result)
		{
			if (input.Length == 0)
			{
				result = default;
				return true;
			}

			if (input.Length <= 9 && Base62Encoding.TryDecodeUInt64(input, out ulong x, Base62FormattingOptions.Lexicographic) && x <= MASK_48)
			{
				result = new Uuid48(x);
				return true;
			}

			result = default;
			return false;
		}

		#endregion

		#region Casting...

		//note: we cannot use implicit casting because it causes too many conflicts with "out" parameters and Deconstruction

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid48(ulong value)
		{
			return value <= MASK_48 ? new Uuid48(value) : throw new OverflowException();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ulong(Uuid48 value)
		{
			return value.Value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid48(long value)
		{
			return unchecked((ulong) value) <= MASK_48 ? new Uuid48(value) : throw new OverflowException();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator long(Uuid48 value)
		{
			return unchecked((long) value.Value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid48(int value)
		{
			return value >= 0 ? new Uuid48(value) : throw new OverflowException();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid48(uint value)
		{
			return new Uuid48(value);
		}

		#endregion

		#region IFormattable...

		/// <summary>Returns the equivalent 64-bit signed integer</summary>
		/// <remarks>The 16 upper bits will be set to <c>0</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long ToInt64() => unchecked((long) this.Value);

		/// <summary>Returns the equivalent 64-bit unsigned integer</summary>
		/// <remarks>The 16 upper bits will be set to <c>0</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ToUInt64() => this.Value;

		/// <summary>Converts this instance into a <see cref="Slice"/> of <c>6</c> bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			var buffer = new byte[6];
			WriteToUnsafe(buffer.AsSpan());
			return new Slice(buffer);
		}

		/// <summary>Converts this instance into a byte array</summary>
		[Pure]
		public byte[] ToByteArray()
		{
			var buffer = new byte[6];
			WriteToUnsafe(buffer.AsSpan());
			return buffer;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format <c>"xxxx-xxxxxxxx"</c>, where <c>x</c> is a lower-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString("D");
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid48"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid48"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as two groups of 8 hexadecimal digits, separated by a hyphen: <c>"01234567-89abcdef"</c> (17 characters).</p>
		/// <p>The <b>X</b> format encodes the value as a single group of 16 hexadecimal digits: <c>"0123456789abcdef"</c> (16 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with <c>'{'</c> and <c>'}'</c>: <c>"{01234567-89abcdef}"</c> (19 characters).</p>
		/// <p>The <b>R</b> format encodes the value as a decimal number <c>"1234567890"</c> (1 to 20 characters) which can be parsed as an UInt64 without loss.</p>
		/// <p>The <b>C</b> format uses a compact base-62 encoding that preserves lexicographical ordering, composed of digits, uppercase alpha and lowercase alpha, suitable for compact representation that can fit in a querystring.</p>
		/// <p>The <b>Z</b> format is equivalent to the <b>C</b> format, but with extra padding so that the string is always 9 characters long.</p>
		/// </example>
		public string ToString(string? format, IFormatProvider? formatProvider = null)
		{
			switch(format)
			{
				case null or "" or "D":
				{ // Default format is "XXXX-XXXXXXXX"
					return EncodeTwoParts(this.Value, separator: '-', quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxx-xxxxxxxx"
					return EncodeTwoParts(this.Value, separator: '-', quotes: false, upper: false);
				}

				case "C":
				case "c":
				{ // base 62, compact, no padding
					return ToBase62(padded: false);
				}
				case "Z":
				case "z":
				{ // base 62, padded with '0' up to 9 chars
					return ToBase62(padded: true);
				}

				case "R":
				case "r":
				{ // Integer: "1234567890"
					return this.Value.ToString(null, formatProvider ?? CultureInfo.InvariantCulture);
				}

				case "X": //note: Guid.ToString("X") returns "{0x.....,0x.....,...}" but we prefer the "N" format
				case "N":
				{ // "XXXXXXXXXXXX"
					return EncodeOnePart(this.Value, quotes: false, upper: true);
				}
				case "x":
				case "n":
				{ // "xxxxxxxxxxxx"
					return EncodeOnePart(this.Value, quotes: false, upper: false);
				}

				case "B":
				{ // "{XXXX-XXXXXXXX}"
					return EncodeTwoParts(this.Value, separator: '-', quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxx-xxxxxxxx}"
					return EncodeTwoParts(this.Value, separator: '-', quotes: true, upper: false);
				}

				case "V":
				{ // "XX-XX-XX-XX-XX-XX"
					return EncodeSixParts(this.Value, separator: '-', quotes: false, upper: true);
				}
				case "v":
				{ // "xx-xx-xx-xx-xx-xx"
					return EncodeSixParts(this.Value, separator: '-', quotes: false, upper: false);
				}

				case "M":
				{ // "XX:XX:XX:XX:XX:XX"
					return EncodeSixParts(this.Value, separator: ':', quotes: false, upper: true);
				}
				case "m":
				{ // "xx:xx:xx:xx:xx:xx"
					return EncodeSixParts(this.Value, separator: ':', quotes: false, upper: false);
				}

				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid48) + " format specification.");
				}
			}
		}

		/// <summary>Encodes this value into a base-62 encoded text literal</summary>
		/// <remarks>This literal can be parsed back into a <see cref="Uuid48"/> by calling <see cref="FromBase62(string)"/> or <see cref="TryParseBase62(string?,out System.Uuid48)"/></remarks>
		public string ToBase62(bool padded = false)
		{
			return Base62Encoding.Encode64(this.Value, 48, padded ? Base62FormattingOptions.Lexicographic | Base62FormattingOptions.Padded : Base62FormattingOptions.Lexicographic);
		}

		/// <summary>Tries to format the value of the current instance into the provided span of characters.</summary>
		/// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
		/// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="destination" />.</param>
		/// <param name="provider">An optional object that supplies culture-specific formatting information for <paramref name="destination" />.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<char> destination,
			out int charsWritten,
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		)
		{
			//TODO: BUGBUG: OPTIMIZE: this should be changed to not allocate memory!

			string literal;
			switch(format)
			{
				case "" or "D":
				{ // Default format is "XXXXXXXX-XXXXXXXX"
					literal = EncodeTwoParts(this.Value, separator: '-', quotes: false, upper: true);
					break;
				}
				case "d":
				{ // Default format is "xxxxxxxx-xxxxxxxx"
					literal = EncodeTwoParts(this.Value, separator: '-', quotes: false, upper: false);
					break;
				}

				case "C":
				case "c":
				{ // base 62, compact, no padding
					literal = Base62.Encode(this.Value, padded: false);
					break;
				}
				case "Z":
				case "z":
				{ // base 62, padded with '0' up to 9 chars
					literal = Base62.Encode(this.Value, padded: true);
					break;
				}

				case "R":
				case "r":
				{ // Integer: "1234567890"
					literal = this.Value.ToString(null, provider ?? CultureInfo.InvariantCulture);
					break;
				}

				case "X": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "N":
				{ // "XXXXXXXXXXXXXXXX"
					literal = EncodeOnePart(this.Value, quotes: false, upper: true);
					break;
				}
				case "x": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "n":
				{ // "xxxxxxxxxxxxxxxx"
					literal = EncodeOnePart(this.Value, quotes: false, upper: false);
					break;
				}

				case "B":
				{ // "{XXXXXXXX-XXXXXXXX}"
					literal = EncodeTwoParts(this.Value, separator: '-', quotes: true, upper: true);
					break;
				}
				case "b":
				{ // "{xxxxxxxx-xxxxxxxx}"
					literal = EncodeTwoParts(this.Value, separator: '-', quotes: true, upper: false);
					break;
				}

				case "V":
				{ // "XX-XX-XX-XX-XX-XX-XX-XX"
					literal = EncodeSixParts(this.Value, separator: '-', quotes: false, upper: true);
					break;
				}
				case "v":
				{ // "xx-xx-xx-xx-xx-xx-xx-xx"
					literal = EncodeSixParts(this.Value, separator: '-', quotes: false, upper: false);
					break;
				}

				case "M":
				{ // "XX:XX:XX:XX:XX:XX:XX:XX"
					literal = EncodeSixParts(this.Value, separator: ':', quotes: false, upper: true);
					break;
				}
				case "m":
				{ // "xx:xx:xx:xx:xx:xx:xx:xx"
					literal = EncodeSixParts(this.Value, separator: ':', quotes: false, upper: false);
					break;
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid48) + " format specification.");
				}
			}

			return literal.TryCopyTo(destination, out charsWritten);
		}

		#endregion

		#region IEquatable / IComparable...

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			Uuid48 u48 => Equals(u48),
			ulong ul => this.Value == ul,
			long l => l >= 0 && this.Value == (ulong) l,
			uint ui => this.Value == ui,
			int i => i >= 0 && this.Value == (ulong) i,
			Slice bytes => Equals(bytes),
			_ => false
		};

		/// <inheritdoc />
		public override int GetHashCode()
		{
			// fold the 16 upper bits
			return unchecked((int) this.Value) ^ unchecked((int) (this.Value >> 32));
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid48 other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid48 other) => this.Value < other.Value ? -1 : this.Value > other.Value ? +1 : 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ulong other) => this.Value == other;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ulong other) => this.Value < other ? -1 : this.Value > other ? +1 : 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(long other) => other >= 0 && this.Value == (ulong) other;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(long other) => other >= 0 ? CompareTo((ulong) other) : +1;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(uint other) => this.Value == other;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(uint other) => CompareTo((ulong) other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(int other) => other >= 0 && this.Value == (ulong) other;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(int other) => other >= 0 ? CompareTo((ulong) other) : +1;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => TryRead(other.Span, out var res) && res == this;

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#else
		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns><c>true</c> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <c>false</c>.</returns>
#endif
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => TryRead(other, out var res) && res == this;

		#endregion

		#region Base16 encoding...

		[Pure]
		private static char HexToLowerChar(int a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'a') : (char)(a + '0');
		}

		private static unsafe char* Hex8ToLowerChars(char* ptr, byte a)
		{
			Contract.Debug.Requires(ptr != null);
			ptr[0] = HexToLowerChar(a >> 4);
			ptr[1] = HexToLowerChar(a);
			return ptr + 2;
		}

		private static unsafe char* Hex32ToLowerChars(char* ptr, int a)
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

		[Pure]
		private static char HexToUpperChar(int a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'A') : (char)(a + '0');
		}

		private static unsafe char* Hex8ToUpperChars(char* ptr, int a)
		{
			Contract.Debug.Requires(ptr != null);
			ptr[0] = HexToUpperChar(a >> 4);
			ptr[1] = HexToUpperChar(a);
			return ptr + 2;
		}

		private static unsafe char* Hex32ToUpperChars(char* ptr, int a)
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
		private static string EncodeOnePart(ulong value, bool quotes, bool upper)
		{
			ushort hi = unchecked((ushort) (value >> 32));
			uint lo = unchecked((uint) value);

			switch (quotes, upper)
			{
				case (true, true): return $"{{{hi:X04}{lo:X08}}}";
				case (true, false): return $"{{{hi:x04}{lo:x08}}}";
				case (false, true): return $"{hi:X04}{lo:X08}";
				case (false, false): return $"{hi:x04}{lo:x08}";
			}
		}

		[Pure]
		private static string EncodeTwoParts(ulong value, char separator, bool quotes, bool upper)
		{
			ushort hi = unchecked((ushort) (value >> 32));
			uint lo = unchecked((uint) value);

			//note: string interpolation should be optimized by the compiler (should call ushort.TryFormat(...) and uint.TryFormat(...)
			if (quotes)
			{
				return upper ? $"{{{hi:X04}-{lo:X08}}}" : $"{{{hi:x04}-{lo:x08}}}";
			}
			else
			{
				return upper ? $"{hi:X04}-{lo:X08}" : $"{hi:x04}-{lo:x08}";
			}
		}

		[Pure]
		private static string EncodeSixParts(ulong value, char separator, bool quotes, bool upper)
		{
			Contract.Debug.Requires(value <= Uuid48.MASK_48 && separator is (':' or '-'));

			byte a = unchecked((byte) (value >> 40));
			byte b = unchecked((byte) (value >> 32));
			byte c = unchecked((byte) (value >> 24));
			byte d = unchecked((byte) (value >> 16));
			byte e = unchecked((byte) (value >> 8));
			byte f = unchecked((byte) (value));

			return (quotes, upper, separator) switch
			{
				(true, true, ':') => $"{{{a:X02}:{b:X02}:{c:X02}:{d:X02}:{e:X02}:{f:X02}}}",
				(true, true, _) => $"{{{a:X02}-{b:X02}-{c:X02}-{d:X02}-{e:X02}-{f:X02}}}",
				(true, false, ':') => $"{{{a:x02}:{b:x02}:{c:x02}:{d:x02}:{e:x02}:{f:x02}}}",
				(true, false, _) => $"{{{a:x02}-{b:x02}-{c:x02}-{d:x02}-{e:x02}-{f:x02}}}",
				(false, true, ':') => $"{a:X02}:{b:X02}:{c:X02}:{d:X02}:{e:X02}:{f:X02}",
				(false, true, _) => $"{a:X02}-{b:X02}-{c:X02}-{d:X02}-{e:X02}-{f:X02}",
				(false, false, ':') => $"{a:x02}:{b:x02}:{c:x02}:{d:x02}:{e:x02}:{f:x02}",
				_ => $"{a:x02}-{b:x02}-{c:x02}-{d:x02}-{e:x02}-{f:x02}",
			};
		}

		#endregion

		#region Base62 encoding...

		//NOTE: this version of base62 encoding puts the digits BEFORE the letters, to ensure that the string representation of an Uuid48 is in the same order as its byte[] or ulong version.
		// => This scheme use the "0-9A-Za-z" ordering, while most other base62 encoder use "a-zA-Z0-9"

		[PublicAPI]
		private static class Base62
		{
			//note: nested static class, so that we only allocate the internal buffers if Base62 encoding is actually used

			private static readonly int[] Base62Values =
			[
				/* 32.. 63 */ -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, -1, -1, -1, -1, -1,
				/* 64.. 95 */ -1, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, -1, -1, -1, -1, -1,
				/* 96..127 */ -1, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1
			];

			/// <summary>Encode a 48-bit value into a base-62 string</summary>
			/// <param name="value">48-bit value to encode</param>
			/// <param name="padded">If true, keep the leading '0' to return a string of length 9. If false, discards all extra leading '0' digits.</param>
			/// <returns>String that contains only digits, lower and upper case letters. The string will be lexicographically ordered, which means that sorting by string will give the same order as sorting by value.</returns>
			/// <sample>
			/// Encode62(0, false) => "0"
			/// Encode62(0, true) => "00000000000"
			/// Encode62(0xDEADBEEF) => ""
			/// </sample>
			public static string Encode(ulong value, bool padded)
			{
				return Base62Encoding.Encode64(value, 48, padded ? Base62FormattingOptions.Lexicographic | Base62FormattingOptions.Padded : Base62FormattingOptions.Lexicographic | Base62FormattingOptions.None);
			}

			public static bool TryDecode(ReadOnlySpan<char> s, out ulong value)
			{
				if (s.Length == 0 || s.Length > 9)
				{ // fail: too small/too big
					value = 0;
					return false;
				}

				// we know that the original value is exactly 48 bits, and any missing digit is '0'
				ulong factor = 1UL;
				ulong acc = 0UL;
				int p = s.Length - 1;
				int[] bv = Base62Values;
				while (p >= 0)
				{
					// read digit
					int a = s[p];
					// decode base62 digit
					a = a >= 32 && a < 128 ? bv[a - 32] : -1;
					if (a == -1)
					{ // fail: invalid character
						value = 0;
						return false;
					}
					// accumulate, while checking for overflow
					acc = checked(acc + ((ulong) a * factor));
					if (p-- > 0) factor *= 62;
				}
				value = acc;
				return true;
			}

		}

		#endregion

		#region Unsafe I/O...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteToUnsafe(Span<byte> destination)
		{
			BinaryPrimitives.WriteUInt16BigEndian(destination, unchecked((ushort) (this.Value >> 32)));
			BinaryPrimitives.WriteUInt32BigEndian(destination[2..], unchecked((uint) this.Value));
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/></summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 6 bytes</param>
		/// <exception cref="ArgumentException">If <paramref name="destination"/> is smaller than 8 bytes</exception>
		public void WriteTo(Span<byte> destination)
		{
			if (!TryWriteTo(destination))
			{
				throw FailInvalidBufferSize(nameof(destination));
			}
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/>, if it is large enough.</summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 6 bytes</param>
		/// <returns><c>true</c> if the destination is large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> destination)
		{
			if (destination.Length < 6)
			{
				return false;
			}
			WriteToUnsafe(destination);
			return true;
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/>, if it is large enough.</summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 6 bytes</param>
		/// <param name="bytesWritten">Receives the number of bytes written (either <c>6</c> or <c>0</c>)</param>
		/// <returns><c>true</c> if the destination is large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> destination, out int bytesWritten)
		{
			if (destination.Length >= 6)
			{
				WriteToUnsafe(destination);
				bytesWritten = 6;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

		#endregion

		#region Operators...

		#region Equality...

		public static bool operator ==(Uuid48 left, Uuid48 right)
		{
			return left.Value == right.Value;
		}

		public static bool operator !=(Uuid48 left, Uuid48 right)
		{
			return left.Value != right.Value;
		}

		public static bool operator >(Uuid48 left, Uuid48 right)
		{
			return left.Value > right.Value;
		}

		public static bool operator >=(Uuid48 left, Uuid48 right)
		{
			return left.Value >= right.Value;
		}

		public static bool operator <(Uuid48 left, Uuid48 right)
		{
			return left.Value < right.Value;
		}

		public static bool operator <=(Uuid48 left, Uuid48 right)
		{
			return left.Value <= right.Value;
		}

		public static bool operator ==(Uuid48 left, long right)
		{
			return right >= 0 && left.Value == (ulong) right;
		}

		public static bool operator ==(Uuid48 left, ulong right)
		{
			return left.Value == right;
		}

		public static bool operator ==(Uuid48 left, int right)
		{
			return right >= 0 && left.Value == (ulong) right;
		}

		public static bool operator ==(Uuid48 left, uint right)
		{
			return left.Value == right;
		}

		public static bool operator !=(Uuid48 left, long right)
		{
			return right < 0 || left.Value != (ulong) right;
		}

		public static bool operator !=(Uuid48 left, ulong right)
		{
			return left.Value != right;
		}

		public static bool operator !=(Uuid48 left, int right)
		{
			return right < 0 || left.Value != (ulong) right;
		}

		public static bool operator !=(Uuid48 left, uint right)
		{
			return left.Value != right;
		}

		#endregion

		#region Artithmetic...

#if NET7_0_OR_GREATER
		// explicit generic-math interface members (.NET 7+); the plain operators below remain available on all targets
		/// <inheritdoc />
		static int IAdditiveIdentity<Uuid48, int>.AdditiveIdentity => 0;

		/// <inheritdoc />
		static uint IAdditiveIdentity<Uuid48, uint>.AdditiveIdentity => 0;

		/// <inheritdoc />
		static long IAdditiveIdentity<Uuid48, long>.AdditiveIdentity => 0;

		/// <inheritdoc />
		static ulong IAdditiveIdentity<Uuid48, ulong>.AdditiveIdentity => 0;
#endif

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator +(Uuid48 left, int right) => FromLower48(unchecked((long) left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator checked +(Uuid48 left, int right) => FromInt64(checked((long) left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator +(Uuid48 left, long right) => FromLower48(unchecked((long) left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator checked +(Uuid48 left, long right) => FromInt64(checked((long) left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator +(Uuid48 left, ulong right) => FromLower48(unchecked(left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator checked +(Uuid48 left, ulong right) => FromUInt64(checked(left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator +(Uuid48 left, uint right) => FromLower48(unchecked(left.Value + right));

		/// <summary>Adds a value from this instance</summary>
		public static Uuid48 operator checked +(Uuid48 left, uint right) => FromUInt64(checked(left.Value + right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator -(Uuid48 left, int right) => FromLower48(unchecked((long) left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator checked -(Uuid48 left, int right) => FromInt64(checked((long) left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator -(Uuid48 left, long right) => FromLower48(unchecked((long) left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator checked -(Uuid48 left, long right) => FromInt64(checked((long) left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator -(Uuid48 left, uint right) => FromLower48(unchecked(left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator checked -(Uuid48 left, uint right) => FromUInt64(checked(left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator -(Uuid48 left, ulong right) => FromLower48(unchecked(left.Value - right));

		/// <summary>Subtracts a value from this instance</summary>
		public static Uuid48 operator checked -(Uuid48 left, ulong right) => FromUInt64(checked(left.Value - right));

		/// <summary>Increments the value of this instance</summary>
		public static Uuid48 operator ++(Uuid48 value) => FromLower48(unchecked(value.Value + 1));

		/// <summary>Increments the value of this instance</summary>
		public static Uuid48 operator checked ++(Uuid48 value) => FromUInt64(checked(value.Value + 1));

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid48 operator --(Uuid48 value) => FromLower48(unchecked(value.Value - 1));

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid48 operator checked --(Uuid48 value) => FromUInt64(checked(value.Value - 1));

		/// <inheritdoc />
		public static Uuid48 operator &(Uuid48 value, Uuid48 mask) => new(value.Value & mask.Value);

		/// <inheritdoc />
		public static Uuid48 operator |(Uuid48 value, Uuid48 mask) => new(value.Value | mask.Value);

		/// <inheritdoc />
		public static Uuid48 operator ^(Uuid48 value, Uuid48 mask) => new(value.Value ^ mask.Value);

		/// <inheritdoc />
		public static Uuid48 operator ~(Uuid48 value) => new((~value.Value) & MASK_48);

		#endregion

		#endregion

		/// <summary>Compares <see cref="Uuid48"/> instances for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid48>, IComparer<Uuid48>
		{

			/// <summary>Default comparer for <see cref="Uuid48"/>s</summary>
			public static readonly Comparer Default = new();

			private Comparer()
			{ }

			/// <inheritdoc />
			public bool Equals(Uuid48 x, Uuid48 y)
			{
				return x.Value == y.Value;
			}

			/// <inheritdoc />
			public int GetHashCode(Uuid48 obj)
			{
				return obj.Value.GetHashCode();
			}

			/// <inheritdoc />
			public int Compare(Uuid48 x, Uuid48 y)
			{
				return x.Value.CompareTo(y.Value);
			}
			
		}

		/// <summary>Generates 48-bit UUIDs using a secure random number generator</summary>
		/// <remarks>Methods of this type are thread-safe.</remarks>
		[PublicAPI]
		public sealed class RandomGenerator
		{

			/// <summary>Default instance of a random generator</summary>
			/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
			public static readonly RandomGenerator Default = new();

			private RandomNumberGenerator Rng { get; }

			private readonly byte[] Scratch = new byte[SizeOf];

			/// <summary>Create a new instance of a random UUID generator</summary>
			public RandomGenerator()
				: this(null)
			{ }

			/// <summary>Create a new instance of a random UUID generator, using a specific random number generator</summary>
			public RandomGenerator(RandomNumberGenerator? generator)
			{
				this.Rng = generator ?? RandomNumberGenerator.Create();
			}

			/// <summary>Return a new random 48-bit UUID</summary>
			/// <returns>Uuid48 that contains 48 bits worth of randomness.</returns>
			/// <remarks>
			/// <p>This method needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each thread.</p>
			/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
			/// </remarks>
			[Pure]
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Uuid48 NewUuid()
			{
				//OPTIMIZE: use a per-thread instance of the rng and scratch buffer?
				// => right now, NewUuid() is a Global Lock for the whole process!
				lock (this.Rng)
				{
					// get 6 bytes of randomness (0 allowed)
					this.Rng.GetBytes(this.Scratch);
					//note: do *NOT* call GetBytes(byte[], int, int) because it creates a temp buffer, calls GetBytes(byte[]) and copy the result back! (as of .NET 4.7.1)
					//TODO: PERF: use Span<byte> APIs once (if?) they become available!
					return Uuid48.Read(this.Scratch);
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
