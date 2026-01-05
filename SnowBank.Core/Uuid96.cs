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
	using SnowBank.Text;

	/// <summary>Represents a 96-bit UUID that is stored in high-endian format on the wire</summary>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct Uuid96 : IFormattable, IEquatable<Uuid96>, IComparable<Uuid96>, IEquatable<Slice>, ISliceSerializable, ISpanEncodable
#if NET8_0_OR_GREATER
		, ISpanFormattable
		, ISpanParsable<Uuid96>
#endif
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		/// <summary><see cref="Uuid96"/> with all bits set to <c>0</c> (<c>"00000000-00000000-00000000"</c>)</summary>
		public static readonly Uuid96 Empty;

		/// <summary><see cref="Uuid96"/> with only bit 0 set to <c>1</c> (<c>"00000000-00000000-00000001"</c>)</summary>
		public static readonly Uuid96 One = new(0, 1);

		/// <summary><see cref="Uuid96"/> with all bits set to <c>1</c> (<c>"FFFFFFFF-FFFFFFFF-FFFFFFFF"</c>)</summary>
		public static readonly Uuid96 AllBitsSet = new(uint.MaxValue, ulong.MaxValue);

		/// <summary><see cref="Uuid96"/> with all bits set to <c>0</c> (<c>"00000000-00000000-00000000"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid96 MinValue;

		/// <summary><see cref="Uuid96"/> with all bits set to <c>1</c> (<c>"FFFFFFFF-FFFFFFFF-FFFFFFFF"</c>)</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid96 MaxValue = new(uint.MaxValue, ulong.MaxValue);

		/// <summary>Size is 12 bytes</summary>
		public const int SizeOf = 12;

		//note: these two fields are stored in host order (so probably Little-Endian) in order to simplify parsing and ordering

		/// <summary>The upper 32-bits (<c>XXXXXXXX-........-........</c>)</summary>
		private readonly uint High;

		/// <summary>The lower 48-bits (<c>........-XXXXXXXX-XXXXXXXX</c>)</summary>
		private readonly ulong Low;

		private const ulong MASK_48 = (1UL << 48) - 1;
		private const ulong MASK_32 = (1UL << 32) - 1;
		private const ulong MASK_16 = (1UL << 16) - 1;

		/// <summary>Returns the 16 upper bits (<c>xxxx....-........-........</c>)</summary>
		/// <seealso cref="Lower80"/>
		public ushort Upper16 => (ushort) (this.High >> 16);

		/// <summary>Returns the 32 upper bits (<c>xxxxxxxx-........-........</c>)</summary>
		/// <seealso cref="Lower64"/>
		public uint Upper32 => this.High;

		/// <summary>Returns the 48 upper bits (<c>xxxxxxxx-xxxx....-........</c>)</summary>
		/// <seealso cref="Lower48"/>
		public Uuid48 Upper48 => new(((ulong) this.High << 16) | (this.Low >> 48));

		/// <summary>Returns the 64 upper bits (<c>xxxxxxxx-xxxxxxxx-........</c>)</summary>
		/// <seealso cref="Lower32"/>
		public ulong Upper64 => ((ulong) this.High << 32) | (this.Low >> 32);

		/// <summary>Returns the 80 upper bits (<c>xxxxxxxx-xxxxxxxx-xxxx....</c>)</summary>
		/// <seealso cref="Lower16"/>
		public Uuid80 Upper80 => new(unchecked((ushort) (this.High >> 16)), unchecked((uint) (((this.High & MASK_16) << 16) | (this.Low >> 48))), unchecked((uint) (this.Low >> 16)));

		/// <summary>Returns the 16 lower bits (<c>........-........-....xxxx</c>)</summary>
		/// <seealso cref="Upper80"/>
		public ushort Lower16 => unchecked((ushort) this.Low);

		/// <summary>Returns the 32 lower bits (<c>........-........-xxxxxxxx</c>)</summary>
		/// <seealso cref="Upper64"/>
		public uint Lower32 => unchecked((uint) this.Low);

		/// <summary>Returns the 48 lower bits (<c>........-....xxxx-xxxxxxxx</c>)</summary>
		/// <seealso cref="Upper48"/>
		public Uuid48 Lower48 => new(this.Low & MASK_48);

		/// <summary>Returns the 64 lower bits (<c>........-xxxxxxxx-xxxxxxxx</c>)</summary>
		/// <seealso cref="Upper32"/>
		public ulong Lower64 => this.Low;

		/// <summary>Returns the 80 lower bits (<c>....xxxx-xxxxxxxx-xxxxxxxx</c>)</summary>
		/// <seealso cref="Upper16"/>
		public Uuid80 Lower80 => new(unchecked((ushort) (this.High)), this.Low);

		/// <summary>Returns the 32 middle bits (<c>........-xxxxxxxx-........</c>)</summary>
		/// <seealso cref="Upper32"/>
		/// <seealso cref="Lower32"/>
		public uint Middle32 => unchecked((uint) (this.Low >> 32));

		#region Constructors...

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="upper32"><c>XXXXXXXX-........-........</c></param>
		/// <param name="lower64"><c>........-XXXXXXXX-XXXXXXXX</c></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(uint upper32, ulong lower64)
		{
			this.High = upper32;
			this.Low = lower64;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="upper32"><c>XXXXXXXX-........-........</c></param>
		/// <param name="lower64"><c>........-XXXXXXXX-XXXXXXXX</c></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(uint upper32, long lower64)
		{
			this.High = upper32;
			this.Low = (ulong) lower64;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="upper32"><c>XXXXXXXX-........-........</c></param>
		/// <param name="lower64"><c>........-XXXXXXXX-XXXXXXXX</c></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(int upper32, long lower64)
		{
			this.High = (uint) upper32;
			this.Low = (ulong) lower64;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="upper32"><c>XXXXXXXX-........-........</c></param>
		/// <param name="middle32"><c>........-XXXXXXXX-........</c></param>
		/// <param name="lower32"><c>........-........-XXXXXXXX</c></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(uint upper32, uint middle32, uint lower32)
		{
			this.High = upper32;
			this.Low = (((ulong) middle32) << 32) | lower32;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="upper32"><c>XXXXXXXX-........-........</c></param>
		/// <param name="middle32"><c>........-XXXXXXXX-........</c></param>
		/// <param name="lower32"><c>........-........-XXXXXXXX</c></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(int upper32, int middle32, int lower32)
		{
			this.High = (uint) upper32;
			this.Low = (((ulong) ((uint) middle32)) << 32) | ((uint) lower32);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 64 bits, with the upper 32 bits all set to <c>0</c></summary>
		/// <param name="value">64 lower bits (<c>.........-xxxxxxxx-xxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 FromUInt64(ulong value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 64 bits, with the upper 32 bits all set to <c>0</c></summary>
		/// <param name="value">64 lower bits (<c>.........-xxxxxxxx-xxxxxxxx</c>)</param>
		/// <exception cref="OverflowException">If <paramref name="value"/> is negative</exception>
		[Pure]
		public static Uuid96 FromInt64(long value) => value >= 0 ? new(0, value) : throw new OverflowException();

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 48 bits, with the upper 64 bits all set to <c>0</c></summary>
		/// <param name="value">48 lower bits (<c>.........-....xxxx-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUInt48(ulong value) => value <= MASK_48 ? new(0, value) : throw new OverflowException();

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 32 bits, with the upper 64 bits all set to <c>0</c></summary>
		/// <param name="value">32 lower bits (<c>.........-........-xxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 FromUInt32(uint value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 32 bits, with the upper 64 bits all set to <c>0</c></summary>
		/// <param name="value">32 lower bits (<c>.........-........-xxxxxxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 FromInt32(int value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 16 bits, with the upper 80 bits all set to <c>0</c></summary>
		/// <param name="value">16 lower bits (<c>.........-........-....xxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 FromUInt16(ushort value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 16 bits, with the upper 80 bits all set to <c>0</c></summary>
		/// <param name="value">16 lower bits (<c>.........-........-....xxxx</c>)</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 FromInt16(short value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid80"/> from a string literal encoded in Base-1024</summary>
		/// <param name="value">Base-1024 string literal</param>
		/// <seealso cref="Base1024Encoding"/>
		public static Uuid96 FromBase1024(string value) => Base1024Encoding.DecodeUuid96Value(value);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidBufferSize([InvokerParameterName] string arg)
		{
			return ThrowHelper.ArgumentException(arg, "Value must be 12 bytes long");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidFormat()
		{
			return ThrowHelper.FormatException("Invalid " + nameof(Uuid96) + " format");
		}

		/// <summary>Generate a new random 96-bit UUID.</summary>
		/// <remarks>If you need sequential or cryptographic uuids, you should use a different generator.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 NewUuid()
		{
			unsafe
			{
				// we use Guid.NewGuid() as a source of ~128 bits of entropy, and we fold the extra 32 bits into the first 96 bits
				var x = Guid.NewGuid();
				uint* p = (uint*) &x;
				return new Uuid96(p[0], p[1] ^ p[3], p[2]);
			}
		}

		#endregion

		#region Decomposition...

		/// <summary>Split into two fragments</summary>
		/// <param name="a">xxxxxxxx-........-........</param>
		/// <param name="b">........-xxxxxxxx-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out uint a, out ulong b)
		{
			a = this.Upper32;
			b = this.Lower64;
		}
		/// <summary>Split into three fragments</summary>
		/// <param name="a">xxxxxxxx-........-........</param>
		/// <param name="b">........-xxxxxxxx-........</param>
		/// <param name="c">........-........-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out uint a, out uint b, out uint c)
		{
			a = this.Upper32;
			b = this.Middle32;
			c = this.Lower32;
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 96 bits of a <see cref="Uuid128"/></summary>
		/// <param name="value">Only the 96 upper bits will be used (<c>xxxxxxxx-xxxx-xxxx-xxxx-xxxx........</c>)</param>
		public static Uuid96 FromUpper96(Uuid128 value)
		{
			value.Deconstruct(out _, out uint a, out uint b, out uint c);
			return new(a, b, c);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 96 bits of a <see cref="Uuid128"/></summary>
		/// <param name="value">Only the 96 lower bits will be used (<c>........-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>)</param>
		public static Uuid96 FromLower96(Uuid128 value)
		{
			value.Deconstruct(out _, out uint a, out uint b, out uint c);
			return new(a, b, c);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 80 bits, with the upper 16 bits all set to <c>0</c></summary>
		/// <param name="value">80 lower bits (<c>.....xxxx-xxxxxxxx-xxxxxxxx</c>)</param>
		public static Uuid96 FromLower80(Uuid80 value) => new(value.Upper16, value.Lower64);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 64 bits, with the upper 32 bits all set to <c>0</c></summary>
		/// <param name="value">64 lower bits (<c>.........-xxxxxxxx-xxxxxxxx</c>)</param>
		public static Uuid96 FromLower64(ulong value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 32 bits, with the upper 64 bits all set to <c>0</c></summary>
		/// <param name="value">48 lower bits (<c>.........-....xxxx-xxxxxxxx</c>)</param>
		public static Uuid96 FromLower48(ulong value) => new(0, value & MASK_48);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 32 bits, with the upper 64 bits all set to <c>0</c></summary>
		/// <param name="value">32 lower bits (<c>.........-........-xxxxxxxx</c>)</param>
		public static Uuid96 FromLower32(uint value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the lower 16 bits, with the upper 80 bits all set to <c>0</c></summary>
		/// <param name="value">16 lower bits (<c>.........-........-....xxxx</c>)</param>
		public static Uuid96 FromLower16(ushort value) => new(0, value);

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 16-bit and lower 80-bit parts</summary>
		/// <param name="hi">16 upper bits (<c>xxxx....-........-........</c>)</param>
		/// <param name="low">80 lower bits (<c>....xxxx-xxxxxxxx-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper16Lower80(ushort hi, Uuid80 low)
		{
			return new(((uint) hi << 16) | low.Upper16, low.Lower64);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 32-bit and lower 64-bit parts</summary>
		/// <param name="hi">32 upper bits (<c>xxxxxxxx-........-........</c>)</param>
		/// <param name="low">64 lower bits (<c>........-xxxxxxxx-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper32Lower64(uint hi, ulong low)
		{
			return new(hi, low);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 32-bit and lower 64-bit parts</summary>
		/// <param name="hi">32 upper bits (<c>xxxxxxxx-........-........</c>)</param>
		/// <param name="low">64 lower bits (<c>........-xxxxxxxx-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper32Lower64(uint hi, Uuid64 low)
		{
			return new(hi, low.ToUInt64());
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 48-bit and lower 48-bit parts</summary>
		/// <param name="hi">48 upper bits (<c>xxxxxxxx-xxxx....-........</c>)</param>
		/// <param name="low">48 lower bits (<c>........-....xxxx-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper48Lower48(ulong hi, ulong low)
		{
			return new(unchecked((uint) (hi >> 16)), (hi << 48) | (low & MASK_48));
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 48-bit and lower 48-bit parts</summary>
		/// <param name="hi">48 upper bits (<c>xxxxxxxx-xxxx....-........</c>)</param>
		/// <param name="low">48 lower bits (<c>........-....xxxx-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper48Lower48(Uuid48 hi, Uuid48 low)
		{
			return new(hi.Upper32, ((ulong) hi.Lower16 << 48) | low.ToUInt64());
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 64-bit and lower 32-bit parts</summary>
		/// <param name="hi">64 upper bits (<c>xxxxxxxx-xxxxxxxx-........</c>)</param>
		/// <param name="low">32 lower bits (<c>........-........-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper64Lower32(ulong hi, uint low)
		{
			return new(unchecked((uint) (hi >> 32)), ((hi & 0xFFFFFFFF) << 32) | low);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 64-bit and lower 32-bit parts</summary>
		/// <param name="hi">64 upper bits (<c>xxxxxxxx-xxxxxxxx-........</c>)</param>
		/// <param name="middle">16 middle bits (<c>........-........-xxxx....</c>)</param>
		/// <param name="low">16 lower bits (<c>........-........-....xxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper64Middle16Lower16(ulong hi, ushort middle, ushort low)
		{
			return new(unchecked((uint) (hi >> 32)), ((hi & 0xFFFFFFFF) << 32) | ((uint) middle << 16) | low);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 64-bit and lower 32-bit parts</summary>
		/// <param name="hi">64 upper bits (<c>xxxxxxxx-xxxxxxxx-........</c>)</param>
		/// <param name="low">32 lower bits (<c>........-........-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper64Lower32(Uuid64 hi, uint low)
		{
			var hiValue = hi.ToUInt64();
			return new(unchecked((uint) (hiValue >> 32)), ((hiValue & 0xFFFFFFFF) << 32) | low);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 80-bit and lower 16-bit parts</summary>
		/// <param name="hi">80 upper bits (<c>xxxxxxxx-xxxxxxxx-xxxx....</c>)</param>
		/// <param name="low">16 lower bits (<c>........-........-....xxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper80Lower16(Uuid80 hi, ushort low)
		{
			return new(hi.Upper32, ((ulong) hi.Lower48 << 16) | low);
		}

		/// <summary>Creates a <see cref="Uuid96"/> from the upper 32-bit, middle 32-bit and lower 32-bit parts</summary>
		/// <param name="hi">32 upper bits (<c>xxxxxxxx-........-........</c>)</param>
		/// <param name="middle">32 middle bits (<c>........-xxxxxxxx-........</c>)</param>
		/// <param name="low">32 lower bits (<c>........-........-xxxxxxxx</c>)</param>
		[Pure]
		public static Uuid96 FromUpper32Middle32Lower32(uint hi, uint middle, uint low)
		{
			return new(hi, ((ulong) middle << 32) | low);
		}

		#endregion

		#region Reading...

		/// <summary>Reads a 96-bit UUID from slice of memory</summary>
		/// <param name="value">Array of bytes that is either empty, or holds at least 12 bytes</param>
		/// <returns>Corresponding <see cref="Uuid80"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 12 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Read(byte[]? value) => Read(value.AsSpan());

		/// <summary>Reads a 96-bit UUID from slice of memory</summary>
		/// <param name="value">Slice of bytes that is either empty, or holds at least 12 bytes</param>
		/// <returns>Corresponding <see cref="Uuid80"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 12 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Read(Slice value) => Read(value.Span);

		/// <summary>Reads a 96-bit UUID from slice of memory</summary>
		/// <param name="value">Span of bytes that is either empty, or holds at least 12 bytes</param>
		/// <returns>Corresponding <see cref="Uuid80"/> if the read is successful</returns>
		/// <exception cref="ArgumentException">If <paramref name="value"/> has an invalid length</exception>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 12 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure]
		public static Uuid96 Read(ReadOnlySpan<byte> value)
			=> value.Length == 0 ? default
			 : value.Length >= SizeOf ? ReadUnsafe(value)
			 : throw FailInvalidBufferSize(nameof(value));

		/// <summary>Reads an 96-bit UUID from slice of memory</summary>
		/// <param name="value">Array of bytes that is either empty, or holds at least 12 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid96"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>12</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 12 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead(byte[]? value, out Uuid96 result) => TryRead(new ReadOnlySpan<byte>(value), out result);

		/// <summary>Reads an 96-bit UUID from slice of memory</summary>
		/// <param name="value">Slice of bytes that is either empty, or holds at least 12 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid96"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>12</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 12 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead(Slice value, out Uuid96 result) => TryRead(value.Span, out result);

		/// <summary>Reads an 96-bit UUID from slice of memory</summary>
		/// <param name="value">Span of bytes that is either empty, or holds at least 12 bytes</param>
		/// <param name="result">Corresponding <see cref="Uuid96"/>, if the read is successful</param>
		/// <returns><c>true</c> if <paramref name="value"/> has length <c>0</c> or at least <c>12</c>; otherwise, <c>false</c>.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is larger than 12 bytes, the additional bytes will be ignored.</para>
		/// </remarks>
		[Pure]
		public static bool TryRead(ReadOnlySpan<byte> value, out Uuid96 result)
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

		/// <summary>Parse a string representation of an Uuid96</summary>
		/// <param name="input">String in either formats: <c>""</c>, <c>"xxxxxxxx-xxxxxxxx-xxxxxxxx"</c>, <c>"xxxxxxxxxxxxxxxxxxxxxxxx"</c>, <c>"{xxxxxxxx-xxxxxxxx-xxxxxxxx}"</c>, <c>"{xxxxxxxxxxxxxxxxxxxxxxxx}"</c></param>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid96.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Parse(string input)
		{
			Contract.NotNull(input);
			if (!TryParse(input, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an Uuid96</summary>
		/// <param name="input">String in either formats: "", "badc0ffe-e0ddf00d", "badc0ffee0ddf00d", "{badc0ffe-e0ddf00d}", "{badc0ffee0ddf00d}"</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid96.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid96 Parse(string input, IFormatProvider? provider)
			=> Parse(input);

		/// <summary>Parse a string representation of an Uuid96</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Parse(ReadOnlySpan<char> input)
		{
			if (!TryParse(input, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an Uuid96</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid96 Parse(ReadOnlySpan<char> input, IFormatProvider? provider)
			=> Parse(input);

		/// <summary>Try parsing a string representation of an Uuid96</summary>
		[Pure]
		public static bool TryParse([NotNullWhen(true)] string? input, out Uuid96 result)
		{
			if (input == null)
			{
				result = default;
				return false;
			}
			return TryParse(input.AsSpan(), out result);
		}

		/// <summary>Try parsing a string representation of an Uuid96</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse([NotNullWhen(true)] string? input, IFormatProvider? provider, out Uuid96 result)
			=> TryParse(input, out result);

		/// <summary>Try parsing a string representation of an Uuid96</summary>
		[Pure]
		public static bool TryParse(ReadOnlySpan<char> input, out Uuid96 result)
		{
			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// note: Guid.Parse accepts leading and trailing whitespaces, so we have to replicate the behavior here
			input = input.Trim();

			// remove "{...}" if there is any
			if (input.Length > 2 && input[0] == '{' && input[^1] == '}')
			{
				input = input.Slice(1, input.Length - 2);
			}

			result = default(Uuid96);
			switch (input.Length)
			{
				case 0:
				{ // empty
					return true;
				}
				case 24:
				{ // xxxxxxxxxxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(input, separator: false, out result);
				}
				case 26:
				{ // xxxxxxxx-xxxxxxxx-xxxxxxxx
					if (input[8] != '-' || input[17] != '-') return false;
					return TryDecode16Unsafe(input, separator: true, out result);
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>Try parsing a string representation of an Uuid96</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out Uuid96 result)
			=> TryParse(input, out result);

		/// <summary>Try parsing a UTF-8 string representation of an Uuid96</summary>
		[Pure]
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Uuid96 result)
		{
			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// remove "{...}" if there is any
			if (utf8Text.Length > 2 && utf8Text[0] == '{' && utf8Text[^1] == '}')
			{
				utf8Text = utf8Text.Slice(1, utf8Text.Length - 2);
			}

			result = default(Uuid96);
			switch (utf8Text.Length)
			{
				case 0:
				{ // empty
					return true;
				}
				case 24:
				{ // xxxxxxxxxxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(utf8Text, separator: false, out result);
				}
				case 26:
				{ // xxxxxxxx-xxxxxxxx-xxxxxxxx
					if (utf8Text[8] != '-' || utf8Text[17] != '-') return false;
					return TryDecode16Unsafe(utf8Text, separator: true, out result);
				}
				default:
				{
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
					UnsafeHelpers.StoreUInt32BE(ptr, this.High);
					UnsafeHelpers.StoreUInt64BE(ptr + 4, this.Low);
				}
			}
			return tmp;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format "XXXXXXXX-XXXXXXXX-XXXXXXXX", where 'X' is an upper-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString("D", null);
		}

		/// <summary>Returns a string representation of the value of this <see cref="Uuid96"/> instance, according to the provided format specifier.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "B", "X", "G", "Z" or "N". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid96"/>, using the specified format.</returns>
		/// <remarks>See <see cref="ToString(string, IFormatProvider)"/> for a description of the different formats</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid96"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid96"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as three groups of hexadecimal digits, separated by a hyphen: <c>"aaaaaaaa-bbbbbbbb-cccccccc"</c> (26 characters).</p>
		/// <p>The <b>X</b> and <b>N</b> format encodes the value as a single group of 24 hexadecimal digits: <c>"aaaaaaaabbbbbbbbcccccccc"</c> (24 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with <c>'{'</c> and <c>'}'</c>: <c>"{aaaaaaaa-bbbbbbbb-cccccccc}"</c> (28 characters).</p>
		/// </example>
		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = "D";
			switch(format)
			{
				case "D":
				{ // Default format is "XXXXXXXX-XXXXXXXX-XXXXXXXX"
					return Encode16(this.High, this.Low, separator: true, quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxxxxxx-xxxxxxxx-xxxxxxxx"
					return Encode16(this.High, this.Low, separator: true, quotes: false, upper: false);
				}
				case "X": //note: Guid.ToString("X") returns "{0x.....,0x.....,...}" but we prefer the "N" format
				case "N":
				{ // "XXXXXXXXXXXXXXXXXXXXXXXX"
					return Encode16(this.High, this.Low, separator: false, quotes: false, upper: true);
				}
				case "x":
				case "n":
				{ // "xxxxxxxxxxxxxxxxxxxxxxxx"
					return Encode16(this.High, this.Low, separator: false, quotes: false, upper: false);
				}

				case "B":
				{ // "{XXXXXXXX-XXXXXXXX-XXXXXXXX}"
					return Encode16(this.High, this.Low, separator: true, quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxxxxxx-xxxxxxxx-xxxxxxxx}"
					return Encode16(this.High, this.Low, separator: true, quotes: true, upper: false);
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid96) + " format specification.");
				}
			}
		}

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider = null)
		{
			//PERF: implement this without memory allocations!
			string literal;

			switch (format)
			{
				case "" or "D":
				{ // Default format is "XXXXXXXX-XXXXXXXX-XXXXXXXX"
					literal = Encode16(this.High, this.Low, separator: true, quotes: false, upper: true);
					break;
				}
				case "d":
				{ // Default format is "xxxxxxxx-xxxxxxxx-xxxxxxxx"
					literal = Encode16(this.High, this.Low, separator: true, quotes: false, upper: false);
					break;
				}
				case "X": //note: Guid.ToString("X") returns "{0x.....,0x.....,...}" but we prefer the "N" format
				case "N":
				{ // "XXXXXXXXXXXXXXXXXXXXXXXX"
					literal = Encode16(this.High, this.Low, separator: false, quotes: false, upper: true);
					break;
				}
				case "x":
				case "n":
				{ // "xxxxxxxxxxxxxxxxxxxxxxxx"
					literal = Encode16(this.High, this.Low, separator: false, quotes: false, upper: false);
					break;
				}

				case "B":
				{ // "{XXXXXXXX-XXXXXXXX-XXXXXXXX}"
					literal = Encode16(this.High, this.Low, separator: true, quotes: true, upper: true);
					break;
				}
				case "b":
				{ // "{xxxxxxxx-xxxxxxxx-xxxxxxxx}"
					literal = Encode16(this.High, this.Low, separator: true, quotes: true, upper: false);
					break;
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid96) + " format specification.");
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
				case Uuid96 uuid: return Equals(uuid);
				case Slice bytes: return Equals(bytes);
			}
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.High.GetHashCode() ^ this.Low.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid96 other) => this.High == other.High && this.Low == other.Low;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid96 other)
		{
			int cmp = this.High.CompareTo(other.High);
			if (cmp == 0) cmp = this.Low.CompareTo(other.Low);
			return cmp;
		}

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

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static char HexToLowerChar(uint a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'a') : (char)(a + '0');
		}

		private static unsafe char* Hex32ToLowerChars([System.Diagnostics.CodeAnalysis.NotNull] char* ptr, uint a)
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

		private static unsafe char* Hex32ToUpperChars([System.Diagnostics.CodeAnalysis.NotNull] char* ptr, uint a)
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
		private static unsafe string Encode16(uint hi, ulong lo, bool separator, bool quotes, bool upper)
		{
			int size = SizeOf * 2 + (separator ? 2 : 0) + (quotes ? 2 : 0);
			char* buffer = stackalloc char[32]; // max 26 mais on arrondi a 32

			char* ptr = buffer;
			if (quotes) *ptr++ = '{';
			if (upper)
			{
				ptr = Hex32ToUpperChars(ptr, hi);
				if (separator) *ptr++ = '-';
				ptr = Hex32ToUpperChars(ptr, (uint) (lo >> 32));
				if (separator) *ptr++ = '-';
				ptr = Hex32ToUpperChars(ptr, (uint) lo);
			}
			else
			{
				ptr = Hex32ToLowerChars(ptr, hi);
				if (separator) *ptr++ = '-';
				ptr = Hex32ToLowerChars(ptr, (uint) (lo >> 32));
				if (separator) *ptr++ = '-';
				ptr = Hex32ToLowerChars(ptr, (uint) lo);
			}
			if (quotes) *ptr++ = '}';

			Contract.Debug.Ensures(ptr == buffer + size);
			return new string(buffer, 0, size);
		}

		private const int INVALID_CHAR = -1;

		[Pure]
		private static int CharToHex(char c)
		{
			if (c <= '9')
			{
				return c >= '0' ? (c - 48) : INVALID_CHAR;
			}
			if (c <= 'F')
			{
				return c >= 'A' ? (c - 55) : INVALID_CHAR;
			}
			if (c <= 'f')
			{
				return c >= 'a' ? (c - 87) : INVALID_CHAR;
			}
			return INVALID_CHAR;
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

		private static bool TryDecode16Unsafe(ReadOnlySpan<char> chars, bool separator, out Uuid96 result)
		{
			// aaaaaaaabbbbbbbbcccccccc
			// aaaaaaaa-bbbbbbbb-cccccccc
			if ((!separator || (chars[8] == '-' && chars[17] == '-'))
			 && TryCharsToHex32(chars, out uint hi)
			 && TryCharsToHex32(chars.Slice(separator ? 9 : 8), out uint med)
			 && TryCharsToHex32(chars.Slice(separator ? 18 : 16), out uint lo))
			{
				result = new Uuid96(hi, med, lo);
				return true;
			}
			result = default(Uuid96);
			return false;
		}

		private static bool TryDecode16Unsafe(ReadOnlySpan<byte> chars, bool separator, out Uuid96 result)
		{
			// aaaaaaaabbbbbbbbcccccccc
			// aaaaaaaa-bbbbbbbb-cccccccc
			if ((!separator || (chars[8] == '-' && chars[17] == '-'))
			    && TryCharsToHex32(chars, out uint hi)
			    && TryCharsToHex32(chars.Slice(separator ? 9 : 8), out uint med)
			    && TryCharsToHex32(chars.Slice(separator ? 18 : 16), out uint lo))
			{
				result = new Uuid96(hi, med, lo);
				return true;
			}
			result = default(Uuid96);
			return false;
		}

		#endregion

		#region Unsafe I/O...

		internal static Uuid96 ReadUnsafe(ReadOnlySpan<byte> source)
		{
			Contract.Debug.Requires(source.Length >= SizeOf);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(source))
				{
					return new Uuid96(UnsafeHelpers.LoadUInt32BE(ptr), UnsafeHelpers.LoadUInt64BE(ptr + 4));
				}
			}
		}

		internal static void WriteUnsafe(uint hi, ulong lo, Span<byte> destination)
		{
			Contract.Debug.Requires(destination.Length >= SizeOf);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(destination))
				{
					UnsafeHelpers.StoreUInt32BE(ptr, hi);
					UnsafeHelpers.StoreUInt64BE(ptr + 4, lo);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteToUnsafe(Span<byte> destination)
		{
			WriteUnsafe(this.High, this.Low, destination);
		}

		public void WriteTo(Span<byte> destination)
		{
			if (destination.Length < SizeOf) throw FailInvalidBufferSize(nameof(destination));
			WriteUnsafe(this.High, this.Low, destination);
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/>, if it is large enough.</summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 12 bytes</param>
		/// <returns><c>true</c> if the destination is large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> destination)
		{
			if (destination.Length < SizeOf) return false;
			WriteUnsafe(this.High, this.Low, destination);
			return true;
		}

		/// <summary>Writes the bytes of this instance to the specified <paramref name="destination"/>, if it is large enough.</summary>
		/// <param name="destination">Buffer where the bytes will be written to, with a capacity of at least 12 bytes</param>
		/// <param name="bytesWritten">Receives the number of bytes written (either <c>12</c> or <c>0</c>)</param>
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

		#endregion

		#region Operators...

		public static bool operator ==(Uuid96 left, Uuid96 right)
		{
			return left.High == right.High & left.Low == right.Low;
		}

		public static bool operator !=(Uuid96 left, Uuid96 right)
		{
			return left.High != right.High | left.Low != right.Low;
		}

		public static bool operator >(Uuid96 left, Uuid96 right)
		{
			return left.High > right.High || (left.High == right.High && left.Low > right.Low);
		}

		public static bool operator >=(Uuid96 left, Uuid96 right)
		{
			return left.High > right.High || (left.High == right.High && left.Low >= right.Low);
		}

		public static bool operator <(Uuid96 left, Uuid96 right)
		{
			return left.High < right.High || (left.High == right.High && left.Low < right.Low);
		}

		public static bool operator <=(Uuid96 left, Uuid96 right)
		{
			return left.High < right.High || (left.High == right.High && left.Low <= right.Low);
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid96 operator +(Uuid96 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				uint hi = left.High;
				ulong lo = left.Low + (ulong) right;
				if (lo < left.Low) // overflow!
				{
					++hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid96 operator +(Uuid96 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				uint hi = left.High;
				ulong lo = left.Low + right;
				if (lo < left.Low) // overflow!
				{
					++hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid96 operator -(Uuid96 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				uint hi = left.High;
				ulong lo = left.Low - (ulong) right;
				if (lo > left.Low) // overflow!
				{
					--hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid96 operator -(Uuid96 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				uint hi = left.High;
				ulong lo = left.Low - right;
				if (lo > left.Low) // overflow!
				{
					--hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Increments the value of this instance</summary>
		public static Uuid96 operator ++(Uuid96 value)
		{
			return value + 1;
		}

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid96 operator --(Uuid96 value)
		{
			return value - 1;
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid96 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid96>, IComparer<Uuid96>
		{

			public static readonly Comparer Default = new();

			private Comparer()
			{ }

			/// <inheritdoc />
			public bool Equals(Uuid96 x, Uuid96 y)
			{
				return x.High == y.High & x.Low == y.Low;
			}

			/// <inheritdoc />
			public int GetHashCode(Uuid96 obj)
			{
				return obj.GetHashCode();
			}

			/// <inheritdoc />
			public int Compare(Uuid96 x, Uuid96 y)
			{
				return x.CompareTo(y);
			}
		}

		/// <summary>Generates 96-bit UUIDs using a secure random number generator</summary>
		/// <remarks>Methods of this type are thread-safe.</remarks>
		[PublicAPI]
		public sealed class RandomGenerator
		{

			/// <summary>Default instance of a random generator</summary>
			/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
			public static readonly Uuid96.RandomGenerator Default = new();

			private RandomNumberGenerator Rng { get; }

			/// <summary>Create a new instance of a random UUID generator</summary>
			public RandomGenerator()
				: this(null)
			{ }

			/// <summary>Create a new instance of a random UUID generator, using a specific random number generator</summary>
			public RandomGenerator(RandomNumberGenerator? generator)
			{
				this.Rng = generator ?? RandomNumberGenerator.Create();
			}

			/// <summary>Return a new random 96-bit UUID</summary>
			/// <returns>Uuid96 that contains 96 bits worth of randomness.</returns>
			/// <remarks>
			/// <p>This method needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each thread.</p>
			/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
			/// </remarks>
			[Pure]
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Uuid96 NewUuid()
			{
				lock (this.Rng)
				{
					Span<byte> scratch = stackalloc byte[SizeOf];
					// get 12 bytes of randomness (0x00 is allowed)
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
