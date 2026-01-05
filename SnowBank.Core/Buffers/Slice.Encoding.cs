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
	using System.Buffers;
	using System.Buffers.Binary;
	using System.ComponentModel;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;

	public partial struct Slice
	{

		#region FromXXX...

		/// <summary>Decode a Base64 encoded string into a slice</summary>
		[Pure]
		public static Slice FromBase64(string? base64String)
		{
			return base64String == null ? default : base64String.Length == 0 ? Empty : new Slice(Convert.FromBase64String(base64String));
		}

		#region 8-bit integers...

		/// <summary>Encode an unsigned 8-bit integer into a slice</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)] //used as a shortcut by a lot of other methods
		public static Slice FromByte(byte value)
		{
			return new Slice(ByteSprite, value, 1);
		}

		/// <summary>Encode an unsigned 8-bit integer into a slice</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)] //used as a shortcut by a lot of other methods
		public static Slice FromByte(int value)
		{
			if ((uint) value > 255) throw ThrowHelper.ArgumentOutOfRangeException(nameof(value));
			return new Slice(ByteSprite, value, 1);
		}

		#endregion

		#region 16-bit integers

		/// <summary>Encode a signed 16-bit integer into a variable size slice (1 or 2 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromInt16(short value) => value switch
		{
			< 0      => FromFixed16(value),
			< 1 << 8 => FromByte((byte) value),
			_        => new Slice([ (byte) (value & 0xFF), (byte) (value >> 8) ], 0, 2)
		};

		/// <summary>Encode a signed 16-bit integer into a variable size slice (1 or 2 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromInt16BE(short value) => value switch
		{
			< 0      => FromFixed16BE(value),
			< 1 << 8 => FromByte((byte) value),
			_        => new Slice([ (byte) (value >> 8), (byte) (value & 0xFF) ], 0, 2)
		};

		/// <summary>Encode a signed 16-bit integer into a 2-byte slice in little-endian</summary>
		[Pure]
		public static Slice FromFixed16(short value)
		{
			return new Slice([ (byte) value, (byte) (value >> 8) ], 0, 2);
		}

		/// <summary>Encode a signed 16-bit integer into a 2-byte slice in little-endian</summary>
		[Pure]
		public static Slice FromFixed16BE(short value)
		{
			return new Slice([ (byte) (value >> 8), (byte) (value & 0xFF) ], 0, 2);
		}

		/// <summary>Encode an unsigned 16-bit integer into a variable size slice (1 or 2 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromUInt16(ushort value)
		{
			return value <= 255 ? FromByte((byte) value) : FromFixedU16(value);
		}

		/// <summary>Encode an unsigned 16-bit integer into a variable size slice (1 or 2 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromUInt16BE(ushort value)
		{
			return value <= 255 ? FromByte((byte) value) : FromFixedU16BE(value);
		}

		/// <summary>Encode an unsigned 16-bit integer into a 2-byte slice in little-endian</summary>
		/// <remarks>0x1122 => 11 22</remarks>
		[Pure]
		public static Slice FromFixedU16(ushort value) //REVIEW: we could drop the 'U' here
		{
			return new Slice([ (byte) (value & 0xFF), (byte) (value >> 8) ], 0, 2);
		}

		/// <summary>Encode an unsigned 16-bit integer into a 2-byte slice in big-endian</summary>
		/// <remarks>0x1122 => 22 11</remarks>
		[Pure]
		public static Slice FromFixedU16BE(ushort value) //REVIEW: we could drop the 'U' here
		{
			return new Slice([ (byte) (value >> 8), (byte) (value & 0xFF) ], 0, 4);
		}

		/// <summary>Encode an unsigned 16-bit integer into 7-bit encoded unsigned int (aka 'Varint16')</summary>
		[Pure]
		public static Slice FromVarint16(ushort value)
		{
			if (value < 128)
			{
				return FromByte((byte)value);
			}

			var writer = new SliceWriter(3);
			writer.WriteVarInt16(value);
			return writer.ToSlice();
		}

		#endregion

		#region 32-bit integers

		/// <summary>Encode a signed 32-bit integer into a variable size slice (1 to 4 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromInt32(int value) => value switch
		{
			< 0 or >= 1 << 24 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24) ], 0, 4)),
			< 1 << 8  => FromByte(value),
			< 1 << 16 => unchecked(new Slice([ (byte) value, (byte) (value >> 8) ], 0, 2)),
			_ => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16) ], 0, 3)),
		};

		/// <summary>Encode a signed 32-bit integer into a 4-byte slice in little-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed32(int value)
		{
			return unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24) ], 0, 4));
		}

		/// <summary>Encode a signed 32-bit integer into a variable size slice (1 to 4 bytes) in big-endian</summary>
		[Pure]
		public static Slice FromInt32BE(int value) => value switch
		{
			< 0 or >= 1 << 24 => unchecked(new Slice([ (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 4)),
			< 1 << 8 => FromByte(value),
			< 1 << 16 => unchecked(new Slice([ (byte) (value >> 8), (byte) value ], 0, 2)),
			_ => unchecked(new Slice([ (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 3)),
		};

		/// <summary>Encode a signed 32-bit integer into a 4-byte slice in big-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed32BE(int value)
		{
			return unchecked(new Slice([ (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 4));
		}

		/// <summary>Encode an unsigned 32-bit integer into a variable size slice (1 to 4 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromUInt32(uint value) => value switch
		{
			< 1 << 8  => FromByte((byte) value),
			< 1 << 16 => unchecked(new Slice([ (byte) value, (byte) (value >> 8) ], 0, 2)),
			< 1 << 24 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16) ], 0, 3)),
			_         => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24) ], 0, 4))
		};

		/// <summary>Encode an unsigned 32-bit integer into a 4-byte slice in little-endian</summary>
		/// <remarks>0x11223344 => 11 22 33 44</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixedU32(uint value) //REVIEW: we could drop the 'U' here
		{
			return new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24) ], 0, 4);
		}

		/// <summary>Encode an unsigned 32-bit integer into a variable size slice (1 to 4 bytes) in big-endian</summary>
		[Pure]
		public static Slice FromUInt32BE(uint value)
		{
			if (value <= (1 << 8) - 1)
			{
				return FromByte((byte)value);
			}
			if (value <= (1 << 16) - 1)
			{
				return unchecked(new Slice([ (byte) (value >> 8), (byte) value ], 0, 2));
			}
			if (value <= (1 << 24) - 1)
			{
				return unchecked(new Slice([ (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 3));
			}
			return FromFixedU32BE(value);
		}

		/// <summary>Encode an unsigned 32-bit integer into a 4-byte slice in big-endian</summary>
		/// <remarks>0x11223344 => 44 33 22 11</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixedU32BE(uint value) //REVIEW: we could drop the 'U' here
		{
			return unchecked(new Slice([ (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 4));
		}

		/// <summary>Encode an unsigned 32-bit integer into 7-bit encoded unsigned int (aka 'Varint32')</summary>
		[Pure]
		public static Slice FromVarint32(uint value)
		{
			if (value <= 127)
			{ // single byte slices are cached
				return FromByte((byte) value);
			}

			var writer = new SliceWriter(value <= (1 << 14) - 1 ? 2 : 5);
			writer.WriteVarInt32(value);
			return writer.ToSlice();
		}

		#endregion

		#region 64-bit integers

		/// <summary>Encode a signed 64-bit integer into a variable size slice (1 to 8 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromInt64(long value) => value switch
		{
			< 0L or >= (1L << 56) => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40), (byte) (value >> 48), (byte) (value >> 56) ], 0, 8)),
			< 1L << 8 => FromByte(unchecked((int) value)),
			< 1L << 16 => unchecked(new Slice([ (byte) value, (byte) (value >> 8) ], 0, 2)),
			< 1L << 24 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16) ], 0, 3)),
			< 1L << 32 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24) ], 0, 4)),
			< 1L << 40 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32) ], 0, 5)),
			< 1L << 48 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40) ], 0, 6)),
			_ => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40), (byte) (value >> 48) ], 0, 7)),
		};

		/// <summary>Encode a signed 64-bit integer into an 8-byte slice in little-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed64(long value)
		{
			return unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40), (byte) (value >> 48), (byte) (value >> 56) ], 0, 8));
		}

		/// <summary>Encode a signed 64-bit integer into a variable size slice (1 to 8 bytes) in big-endian</summary>
		[Pure]
		public static Slice FromInt64BE(long value) => value switch
		{
			< 0 or >= 1L << 56 => unchecked(new Slice([ (byte) (value >> 56), (byte) (value >> 48), (byte) (value >> 40), (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 8)),
			< 1L << 8 => FromByte(unchecked((int) value)),
			< 1L << 16 => unchecked(new Slice([ (byte) (value >> 8), (byte) value ], 0, 2)),
			< 1L << 24 => unchecked(new Slice([ (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 3)),
			< 1L << 32 => unchecked(new Slice([ (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 4)),
			< 1L << 40 => unchecked(new Slice([ (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 5)),
			< 1L << 48 => unchecked(new Slice([ (byte) (value >> 40), (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 6)),
			_ => unchecked(new Slice([ (byte) (value >> 48), (byte) (value >> 40), (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 7)),
		};

		/// <summary>Encode a signed 64-bit integer into an 8-byte slice in big-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed64BE(long value)
		{
			return unchecked(new Slice([ (byte) (value >> 56), (byte) (value >> 48), (byte) (value >> 40), (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 8));
		}

		/// <summary>Encode an unsigned 64-bit integer into a variable size slice (1 to 8 bytes) in little-endian</summary>
		[Pure]
		public static Slice FromUInt64(ulong value) => value switch
		{
			<= (1UL << 32) - 1 => FromUInt32(unchecked((uint) value)),
			<= (1UL << 40) - 1 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32) ], 0, 5)),
			<= (1UL << 48) - 1 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40) ], 0, 6)),
			<= (1UL << 56) - 1 => unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40), (byte) (value >> 48) ], 0, 7)),
			_ => FromFixedU64(value)
		};

		/// <summary>Encode an unsigned 64-bit integer into an 8-byte slice in little-endian</summary>
		/// <remarks>0x1122334455667788 => 11 22 33 44 55 66 77 88</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixedU64(ulong value) //REVIEW: we could drop the 'U' here
		{
			return unchecked(new Slice([ (byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24), (byte) (value >> 32), (byte) (value >> 40), (byte) (value >> 48), (byte) (value >> 56) ], 0, 8));
		}

		/// <summary>Encode an unsigned 64-bit integer into a variable size slice (1 to 8 bytes) in big-endian</summary>
		[Pure]
		public static Slice FromUInt64BE(ulong value)
		{
			if (value <= (1UL << 32) - 1)
			{
				return FromInt32BE((int) value);
			}
			if (value <= (1UL << 40) - 1)
			{
				return unchecked(new Slice([ (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 5));
			}
			if (value <= (1UL << 48) - 1)
			{
				return unchecked(new Slice([ (byte)(value >> 40), (byte)(value >> 32), (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value ], 0, 6));
			}
			if (value <= (1UL << 56) - 1)
			{
				return unchecked(new Slice([ (byte) (value >> 48), (byte) (value >> 40), (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 7));
			}
			return FromFixedU64BE(value);
		}

		/// <summary>Encode an unsigned 64-bit integer into an 8-byte slice in big-endian</summary>
		/// <remarks>0x1122334455667788 => 88 77 66 55 44 33 22 11</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixedU64BE(ulong value) //REVIEW: we could drop the 'U' here
		{
			return unchecked(new Slice([ (byte) (value >> 56), (byte) (value >> 48), (byte) (value >> 40), (byte) (value >> 32), (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value ], 0, 8));
		}

		/// <summary>Encode an unsigned 64-bit integer into 7-bit encoded unsigned int (aka 'Varint64')</summary>
		[Pure]
		public static Slice FromVarint64(ulong value)
		{
			if (value <= 127)
			{ // single byte slices are cached
				return FromByte((byte)value);
			}

			SliceWriter writer;
			if (value <= uint.MaxValue)
			{
				writer = new SliceWriter(value <= (1 << 14) - 1 ? 2 : 5);
				writer.WriteVarInt32((uint) value);
			}
			else
			{
				writer = new SliceWriter(10);
				writer.WriteVarInt64(value);
			}
			return writer.ToSlice();
		}

		#endregion

		#region 128-bit integers

#if NET8_0_OR_GREATER

		private static void DeconstructInt128(in Int128 value, out ulong upper, out ulong lower)
		{
			upper = (ulong) (value >> 64);
			lower = (ulong) value;
		}

		private static void DeconstructUInt128(in UInt128 value, out ulong upper, out ulong lower)
		{
			upper = (ulong) (value >> 64);
			lower = (ulong) value;
		}

		/// <summary>Encode a signed 64-bit integer into an 8-byte slice in little-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromInt128(Int128 value)
		{
			if (value == 0)
			{
				return FromByte(0);
			}

			if (value < 0)
			{ // negative values always require 16 bytes
				return FromFixed128(value);
			}

			// split into two 64-bit parts
			DeconstructInt128(in value, out var upper, out var lower);

			if (upper == 0)
			{ // only the lower 64-bits are used
				return FromUInt64(lower);
			}

			if (upper > 0x00FFFFFFFFFFFFFFUL)
			{ // 128 bits required
				return FromFixed128(value);
			}

			return FromFixed128Slow(value);

			static Slice FromFixed128Slow(Int128 value)
			{
				// we now that we need between 9 and 15 bytes
				// we will write the full 16 bytes into the stack,
				// find the highest non-zero byte, and return this chunk as a new slice
				Span<byte> tmp = stackalloc byte[16];
				BinaryPrimitives.WriteInt128LittleEndian(tmp, value);
				int p = tmp.Length;
				while (p-- > 0)
				{
					if (tmp[p] != 0)
					{ // copy this into a slice
						return new Slice(tmp[..(p + 1)].ToArray());
					}
				}
				return FromByte(0);
			}

		}

		/// <summary>Encode a signed 64-bit integer into an 8-byte slice in little-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromUInt128(UInt128 value)
		{
			if (value == 0)
			{
				return FromByte(0);
			}

			// split into two 64-bit parts
			DeconstructUInt128(in value, out var upper, out var lower);

			if (upper == 0)
			{ // only the lower 64-bits are used
				return FromUInt64(lower);
			}

			if (upper > 0x00FFFFFFFFFFFFFFUL)
			{ // 128 bits required
				return FromFixedU128(value);
			}

			return FromFixedU128Slow(value);

			static Slice FromFixedU128Slow(UInt128 value)
			{
				// we now that we need between 9 and 15 bytes
				// we will write the full 16 bytes into the stack,
				// find the highest non-zero byte, and return this chunk as a new slice
				Span<byte> tmp = stackalloc byte[16];
				BinaryPrimitives.WriteUInt128LittleEndian(tmp, value);
				int p = tmp.Length;
				while (p-- > 0)
				{
					if (tmp[p] != 0)
					{ // copy this into a slice
						return new Slice(tmp[..(p + 1)].ToArray());
					}
				}
				return FromByte(0);
			}

		}

		/// <summary>Encode a signed 128-bit integer into a 16-byte slice in little-endian</summary>
		public static Slice FromFixed128(Int128 value)
		{
			var tmp = new byte[16];
			BinaryPrimitives.WriteInt128LittleEndian(tmp, value);
			return new Slice(tmp);
		}

		/// <summary>Encode an unsigned 128-bit integer into a 16-byte slice in little-endian</summary>
		public static Slice FromFixedU128(UInt128 value)
		{
			var tmp = new byte[16];
			BinaryPrimitives.WriteUInt128LittleEndian(tmp, value);
			return new Slice(tmp);
		}

		/// <summary>Encode a signed 128-bit integer into a 16-byte slice in big-endian</summary>
		public static Slice FromFixed128BE(Int128 value)
		{
			var tmp = new byte[16];
			BinaryPrimitives.WriteInt128BigEndian(tmp, value);
			return new Slice(tmp);
		}

		/// <summary>Encode an unsigned 128-bit integer into a 16-byte slice in big-endian</summary>
		public static Slice FromFixedU128BE(UInt128 value)
		{
			var tmp = new byte[16];
			BinaryPrimitives.WriteUInt128BigEndian(tmp, value);
			return new Slice(tmp);
		}

#endif

		// these overloads are there for .NET 6.0 that does not support Int128. We split it into an upper and lower 64-bit integers, similar to the Int128(ulong, ulong) ctor

		/// <summary>Encode a signed 128-bit integer into a 16-byte slice in little-endian</summary>
		/// <param name="upper">Upper 64-bit of the value</param>
		/// <param name="lower">Lower 64-bit of the value</param>
		/// <remarks>This method is equivalent to calling <c>FromFixed128(new Int128(upper, lower))</c></remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed128(ulong upper, ulong lower)
		{
			var tmp = new byte[16];
#if NET8_0_OR_GREATER
			BinaryPrimitives.WriteInt128LittleEndian(tmp, new Int128(upper, lower));
#else
			if (BitConverter.IsLittleEndian)
			{
				Unsafe.WriteUnaligned(ref tmp[0], upper);
				Unsafe.WriteUnaligned(ref tmp[8], lower);
			}
			else
			{
				Unsafe.WriteUnaligned(ref tmp[0], BinaryPrimitives.ReverseEndianness(lower));
				Unsafe.WriteUnaligned(ref tmp[8], BinaryPrimitives.ReverseEndianness(upper));
			}
#endif
			return new Slice(tmp);
		}

		/// <summary>Encode a signed 128-bit integer into a 16-byte slice in little-endian</summary>
		/// <param name="upper">Upper 64-bit of the value</param>
		/// <param name="lower">Lower 64-bit of the value</param>
		/// <remarks>This method is equivalent to calling <c>FromFixed128(new Int128((ulong) upper, lower))</c></remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed128(long upper, ulong lower)
		{
			var tmp = new byte[16];
#if NET8_0_OR_GREATER
			BinaryPrimitives.WriteInt128LittleEndian(tmp, new Int128((ulong) upper, lower));
#else
			if (BitConverter.IsLittleEndian)
			{
				Unsafe.WriteUnaligned(ref tmp[0], upper);
				Unsafe.WriteUnaligned(ref tmp[8], lower);
			}
			else
			{
				Unsafe.WriteUnaligned(ref tmp[0], BinaryPrimitives.ReverseEndianness(lower));
				Unsafe.WriteUnaligned(ref tmp[8], BinaryPrimitives.ReverseEndianness(upper));
			}
#endif
			return new Slice(tmp);
		}

		/// <summary>Encode a signed 128-bit integer into a 16-byte slice in big-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed128BE(ulong upper, ulong lower)
		{
			var tmp = new byte[16];
#if NET8_0_OR_GREATER
			BinaryPrimitives.WriteInt128LittleEndian(tmp, new Int128(upper, lower));
#else
			if (BitConverter.IsLittleEndian)
			{
				Unsafe.WriteUnaligned(ref tmp[0], BinaryPrimitives.ReverseEndianness(lower));
				Unsafe.WriteUnaligned(ref tmp[8], BinaryPrimitives.ReverseEndianness(upper));
			}
			else
			{
				Unsafe.WriteUnaligned(ref tmp[0], upper);
				Unsafe.WriteUnaligned(ref tmp[8], lower);
			}
#endif
			return new Slice(tmp);
		}

		/// <summary>Encode a signed 128-bit integer into a 16-byte slice in big-endian</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromFixed128BE(long upper, ulong lower)
		{
			var tmp = new byte[16];
#if NET8_0_OR_GREATER
			BinaryPrimitives.WriteInt128LittleEndian(tmp, new Int128((ulong) upper, lower));
#else
			if (BitConverter.IsLittleEndian)
			{
				Unsafe.WriteUnaligned(ref tmp[0], BinaryPrimitives.ReverseEndianness(lower));
				Unsafe.WriteUnaligned(ref tmp[8], BinaryPrimitives.ReverseEndianness(upper));
			}
			else
			{
				Unsafe.WriteUnaligned(ref tmp[0], upper);
				Unsafe.WriteUnaligned(ref tmp[8], lower);
			}
#endif
			return new Slice(tmp);
		}

		#endregion

		#region decimals

		/// <summary>Encode a 32-bit decimal into an 4-byte slice</summary>
		[Pure]
		public static Slice FromSingle(float value)
		{
			byte[] tmp = new byte[4];
			BinaryPrimitives.WriteSingleLittleEndian(tmp, value);
			return new Slice(tmp, 0, 4);
		}

		/// <summary>Encode a 32-bit decimal into an 4-byte slice (in network order)</summary>
		[Pure]
		public static Slice FromSingleBE(float value)
		{
			byte[] tmp = new byte[4];
			BinaryPrimitives.WriteSingleBigEndian(tmp, value);
			return new Slice(tmp, 0, 4);
		}

		/// <summary>Encodes a 64-bit decimal into an 8-byte slice</summary>
		[Pure]
		public static Slice FromDouble(double value)
		{
			byte[] tmp = new byte[8];
			BinaryPrimitives.WriteDoubleLittleEndian(tmp, value);
			return new Slice(tmp, 0, 8);
		}

		/// <summary>Encodes a 64-bit decimal into an 8-byte slice (in network order)</summary>
		[Pure]
		public static Slice FromDoubleBE(double value)
		{
			byte[] tmp = new byte[8];
			BinaryPrimitives.WriteDoubleBigEndian(tmp, value);
			return new Slice(tmp, 0, 8);
		}

		/// <summary>Encodes a 64-bit decimal into an 8-byte slice</summary>
		[Pure]
		public static Slice FromHalf(Half value)
		{
			byte[] tmp = new byte[2];
			BinaryPrimitives.WriteHalfLittleEndian(tmp, value);
			return new Slice(tmp, 0, 2);
		}

		/// <summary>Encodes a 64-bit decimal into an 8-byte slice (in network order)</summary>
		[Pure]
		public static Slice FromHalfBE(Half value)
		{
			byte[] tmp = new byte[2];
			BinaryPrimitives.WriteHalfLittleEndian(tmp, value);
			return new Slice(tmp, 0, 2);
		}

		/// <summary>Encodes a 128-bit decimal into an 16-byte slice</summary>
		public static Slice FromDecimal(decimal value)
		{
			//TODO: may not work on BE platforms?
			byte[] tmp = new byte[16];
			unsafe
			{
				fixed (byte* ptr = &tmp[0])
				{
					*((decimal*) ptr) = value;
				}
			}
			return new Slice(tmp, 0, 16);
		}

		#endregion

		/// <summary>Create a 16-byte slice containing a System.Guid encoding according to RFC 4122 (Big Endian)</summary>
		/// <remarks>WARNING: Slice.FromGuid(guid).GetBytes() will not produce the same result as guid.ToByteArray() !
		/// If you need to produce Microsoft compatible byte arrays, use Slice.Create(guid.ToByteArray()) but then you should NEVER use Slice.ToGuid() to decode such a value !</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromGuid(Guid value)
		{
			// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
			// => we will convert the GUID into a UUID under the hood, and hope that it gets converted back when read from the db

			return new Uuid128(value).ToSlice();
		}

		/// <summary>Create a 16-byte slice containing an RFC 4122 compliant 128-bit UUID</summary>
		/// <remarks>You should never call this method on a slice created from the result of calling System.Guid.ToByteArray() !</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromUuid128(Uuid128 value)
		{
			// UUID should already be in the RFC 4122 ordering
			return value.ToSlice();
		}

		/// <summary>Create a 12-byte slice containing a 96-bit UUID</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromUuid96(Uuid96 value)
		{
			return value.ToSlice();
		}

		/// <summary>Create a 10-byte slice containing an 80-bit UUID</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromUuid80(Uuid80 value)
		{
			return value.ToSlice();
		}

		/// <summary>Create an 8-byte slice containing a 64-bit UUID</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromUuid64(Uuid64 value)
		{
			return value.ToSlice();
		}

		/// <summary>Encoding used to produce UTF-8 slices</summary>
		internal static readonly UTF8Encoding Utf8NoBomEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

		/// <summary>Dangerously create a slice containing string converted to the local ANSI code page. All non-ANSI characters may be corrupted or converted to '?', and this slice may not decode properly on a different system.</summary>
		/// <remarks>
		/// <para>WARNING: if you put a string that contains non-ANSI chars, it will be silently corrupted! This should only be used to store keywords or 'safe' strings, and when the decoding will only happen on the same system, or systems using the same codepage.</para>
		/// <para>Slices encoded by this method are not guaranteed to be decoded without loss. <b>YOU'VE BEEN WARNED!</b></para>
		/// </remarks>
		/// <seealso cref="FromStringUtf8(string)"/>
		/// <seealso cref="FromStringAscii(string)"/>
		[Pure]
		public static Slice FromStringAnsi(string? text)
		{
			return text == null ? Slice.Nil : FromStringAnsi(text.AsSpan());
		}

		/// <summary>Dangerously create a slice containing string converted to the local ANSI code page. All non-ANSI characters may be corrupted or converted to '?', and this slice may not decode properly on a different system.</summary>
		/// <remarks>
		/// <para>WARNING: if you put a string that contains non-ANSI chars, it will be silently corrupted! This should only be used to store keywords or 'safe' strings, and when the decoding will only happen on the same system, or systems using the same codepage.</para>
		/// <para>Slices encoded by this method are not guaranteed to be decoded without loss. <b>YOU'VE BEEN WARNED!</b></para>
		/// </remarks>
		/// <seealso cref="FromStringUtf8(string)"/>
		/// <seealso cref="FromStringAscii(string)"/>
		[Pure]
		public static Slice FromStringAnsi(ReadOnlySpan<char> text)
		{
			if (text.Length == 0)
			{
				return Slice.Empty;
			}

			int capacity = Encoding.Default.GetByteCount(text);
			var tmp = new byte[capacity];
			int written = Encoding.Default.GetBytes(text, tmp);
			return new Slice(tmp, 0, written);
		}

		/// <summary>Create a slice from an ASCII string, where all the characters map directory into bytes (0..255). The string will be checked before being encoded.</summary>
		/// <remarks>
		/// <para>This method will check each character and fail if at least one is greater than 255.</para>
		/// <para>Slices encoded by this method are only guaranteed to roundtrip if decoded with <see cref="ToByteString"/>. If the original string only contained ASCII characters (0..127) then it can also be decoded by <see cref="ToUnicode"/>.</para>
		/// <para>The only difference between this method and <see cref="FromByteString(string)"/> is that the later will truncate non-ASCII characters to their lowest 8 bits, while the former will throw an exception.</para>
		/// <para>For example, <c>Slice.FromStringAscii("\xff")</c> will return a <see cref="Slice"/> with the first byte set to 0xFF, as expected. But <c>Slice.FromStringAscii("\u1234")</c> will throw an exception.</para>
		/// </remarks>
		/// <exception cref="FormatException">If at least one character is greater than 255.</exception>
		/// <seealso cref="FromByteString(string)"/>
		[Pure]
		public static Slice FromStringAscii(string? value)
		{
			if (value == null) return Slice.Nil;
			if (value.Length == 0) return Slice.Empty;
			byte[]? _ = null;
			return ConvertByteStringChecked(value.AsSpan(), ref _);
		}

		/// <summary>Create a slice from an ASCII string, where all the characters map directory into bytes (0..255). The string will be checked before being encoded.</summary>
		/// <remarks>
		/// <para>This method will check each character and fail if at least one is greater than 255.</para>
		/// <para>Slices encoded by this method are only guaranteed to roundtrip if decoded with <see cref="ToByteString"/>. If the original string only contained ASCII characters (0..127) then it can also be decoded by <see cref="ToUnicode"/>.</para>
		/// <para>The only difference between this method and <see cref="FromByteString(ReadOnlySpan{char})"/> is that the later will truncate non-ASCII characters to their lowest 8 bits, while the former will throw an exception.</para>
		/// <para>For example, <c>Slice.FromStringAscii("\xff")</c> will return a <see cref="Slice"/> with the first byte set to 0xFF, as expected. But <c>Slice.FromStringAscii("\u1234")</c> will throw an exception.</para>
		/// </remarks>
		/// <exception cref="FormatException">If at least one character is greater than 255.</exception>
		/// <seealso cref="FromByteString(ReadOnlySpan{char})"/>
		[Pure]
		public static Slice FromStringAscii(ReadOnlySpan<char> value)
		{
			if (value.Length == 0) return Slice.Empty;
			byte[]? _ = null;
			return ConvertByteStringChecked(value, ref _);
		}

		/// <summary>Create a slice from an ASCII string, where all the characters map directory into bytes (0..255). The string will be checked before being encoded.</summary>
		/// <remarks>
		/// <para>This method will check each character and fail if at least one is greater than 255.</para>
		/// <para>Slices encoded by this method are only guaranteed to roundtrip if decoded with <see cref="ToByteString"/>. If the original string only contained ASCII characters (0..127) then it can also be decoded by <see cref="ToUnicode"/>.</para>
		/// <para>The only difference between this method and <see cref="FromByteString(string?)"/> is that the later will truncate non-ASCII characters to their lowest 8 bits, while the former will throw an exception.</para>
		/// <para>For example, <c>Slice.FromStringAscii("\xff")</c> will return a <see cref="Slice"/> with the first byte set to 0xFF, as expected. But <c>Slice.FromStringAscii("\u1234")</c> will throw an exception.</para>
		/// </remarks>
		/// <exception cref="FormatException">If at least one character is greater than 255.</exception>
		/// <seealso cref="FromByteString(ReadOnlySpan{char},ref byte[])"/>
		[Pure]
		public static Slice FromStringAscii(ReadOnlySpan<char> value, ref byte[]? buffer)
		{
			if (value.Length == 0) return Slice.Empty;
			return ConvertByteStringChecked(value, ref buffer);
		}

		/// <summary>Create a slice from a byte string, where all the characters map directly into bytes (0..255), without performing any validation</summary>
		/// <remarks>
		/// <para>This method does not make any effort to detect characters above 255, which will be truncated to their lower 8 bits, introducing corruption when the string will be decoded. Please MAKE SURE to not call this with untrusted data.</para>
		/// <para>Slices encoded by this method are ONLY compatible with UTF-8 encoding if all characters are between 0 and 127. If this is not the case, then decoding it as a UTF-8 sequence may introduce corruption.</para>
		/// <para>For example, <c>Slice.FromByteString("\xff")</c> will return a <see cref="Slice"/> with the first byte set to 0xFF, as expected. But <c>Slice.FromByteString("\u1234")</c> will have the first byte equal to 0x34 (truncated).</para>
		/// </remarks>
		/// <seealso cref="FromStringAscii(string)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromByteString(string? value)
		{
			if (value == null) return default;
			byte[]? _ = null;
			return FromByteString(value.AsSpan(), ref _);
		}

		/// <summary>Create a slice from a byte string, where all the characters map directly into bytes (0..255), without performing any validation</summary>
		/// <remarks>
		/// <para>This method does not make any effort to detect characters above 255, which will be truncated to their lower 8 bits, introducing corruption when the string will be decoded. Please MAKE SURE to not call this with untrusted data.</para>
		/// <para>Slices encoded by this method are ONLY compatible with UTF-8 encoding if all characters are between 0 and 127. If this is not the case, then decoding it as a UTF-8 sequence may introduce corruption.</para>
		/// <para>For example, <c>Slice.FromByteString("\xff")</c> will return a <see cref="Slice"/> with the first byte set to 0xFF, as expected. But <c>Slice.FromByteString("\u1234")</c> will have the first byte equal to 0x34 (truncated).</para>
		/// </remarks>
		/// <seealso cref="FromStringAscii(System.ReadOnlySpan{char})"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromByteString(ReadOnlySpan<char> value)
		{
			byte[]? _ = null;
			return FromByteString(value, ref _);
		}

		/// <summary>Create a slice from a byte string, where all the characters map directly into bytes (0..255), without performing any validation</summary>
		/// <remarks>
		/// <para>This method does not make any effort to detect characters above 255, which will be truncated to their lower 8 bits, introducing corruption when the string will be decoded. Please MAKE SURE to not call this with untrusted data.</para>
		/// <para>Slices encoded by this method are ONLY compatible with UTF-8 encoding if all characters are between 0 and 127. If this is not the case, then decoding it as a UTF-8 sequence may introduce corruption.</para>
		/// <para>For example, <c>Slice.FromByteString("\xff")</c> will return a <see cref="Slice"/> with the first byte set to 0xFF, as expected. But <c>Slice.FromByteString("\u1234")</c> will have the first byte equal to 0x34 (truncated).</para>
		/// </remarks>
		/// <seealso cref="FromStringAscii(System.ReadOnlySpan{char},ref byte[])"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromByteString(ReadOnlySpan<char> value, ref byte[]? buffer)
		{
			return value.Length != 0 ? ConvertByteStringNoCheck(value, ref buffer) : Slice.Empty;
		}

		[Pure]
		internal static Slice ConvertByteStringChecked(ReadOnlySpan<char> value, ref byte[]? buffer)
		{
			int n = value.Length;
			if (n == 1)
			{
				char c = value[0];
				if (c > 0xFF) goto InvalidChar;
				if (buffer?.Length > 0)
				{
					buffer[0] = (byte) c;
					return new Slice(buffer, 0, 1);
				}
				return FromByte((byte) c);
			}

			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, n);
			if (!TryConvertBytesStringChecked(new Span<byte>(tmp, 0, n), value)) goto InvalidChar;
			return new Slice(tmp, 0, n);
		InvalidChar:
			throw ThrowHelper.FormatException("The specified string contains characters that cannot be safely truncated to 8 bits. If you are encoding natural text, you should use UTF-8 encoding.");
		}

		[Pure]
		private static bool TryConvertBytesStringChecked(Span<byte> buffer, ReadOnlySpan<char> value)
		{
			int n = value.Length;
			if ((uint) buffer.Length < (uint) n) return false;
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(buffer))
				fixed (char* pChars = &MemoryMarshal.GetReference(value))
				{
					char* inp = pChars;
					byte* outp = pBytes;

					while (n > 0)
					{
						char c = *inp;
						if (c > 0xFF) return false;
						*outp++ = (byte)(*inp++);
						--n;
					}
				}
			}
			return true;
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>This method is optimized for strings that usually contain only ASCII characters.</para>
		/// <para>DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.</para>
		/// <para>For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(string)"/>.</para>
		/// </remarks>
		/// <see cref="FromByteString(string)"/>
		/// <see cref="FromStringAscii(string)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice FromString(string? value)
		{
			//REVIEW: what if people call FromString("\xFF/some/system/path") by mistake?
			// Should be special case when the string starts with \xFF (or \xFF\xFF)? What about \xFE ?
			if (value == null) return default;
			byte[]? _ = null;
			return FromString(value.AsSpan(), ref _);
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>This method is optimized for strings that usually contain only ASCII characters.</para>
		/// <para>DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.</para>
		/// <para>For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(ReadOnlySpan{char})"/>.</para>
		/// </remarks>
		/// <see cref="FromByteString(ReadOnlySpan{char})"/>
		/// <see cref="FromStringAscii(ReadOnlySpan{char})"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice FromString(ReadOnlySpan<char> value)
		{
			byte[]? _ = null;
			return FromString(value, ref _);
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>This method is optimized for strings that usually contain only ASCII characters.</para>
		/// <para>DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.</para>
		/// <para>For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(ReadOnlySpan{char})"/>.</para>
		/// </remarks>
		/// <see cref="FromByteString(ReadOnlySpan{char},ref byte[])"/>
		/// <see cref="FromStringAscii(ReadOnlySpan{char},ref byte[])"/>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice FromString(ReadOnlySpan<char> value, ref byte[]? buffer)
		{
			if (value.Length == 0)
			{
				return Empty;
			}

#if NET8_0_OR_GREATER
			if (Ascii.IsValid(value))
			{
				return ConvertByteStringNoCheck(value, ref buffer);
			}
#else
			if (UnsafeHelpers.IsAsciiString(value))
			{
				return ConvertByteStringNoCheck(value, ref buffer);
			}
#endif
			return FromStringSlow(value, ref buffer);

			static unsafe Slice FromStringSlow(ReadOnlySpan<char> value, [NotNull] ref byte[]? buffer)
			{
				fixed (char* chars = &MemoryMarshal.GetReference(value))
				{
					int capa = Utf8NoBomEncoding.GetByteCount(chars, value.Length);
					var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, capa);
					fixed (byte* ptr = &tmp[0])
					{
						if (Utf8NoBomEncoding.GetBytes(chars, value.Length, ptr, capa) != capa)
						{
#if DEBUG
							// there is a mismatch between GetByteCount() and what GetBytes() produced!
							if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
							throw new InvalidOperationException("UTF-8 byte capacity estimation failed.");
						}
						return new Slice(tmp, 0, capa);
					}
				}
			}
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>The slice will NOT include the UTF-8 BOM.</para>
		/// <para>This method will not try to identify ASCII-only strings:
		/// If the string provided can ONLY contain ASCII, you should use <see cref="FromStringAscii(string)"/>.
		/// If it is more frequent for the string to be ASCII-only than having UNICODE characters, consider using <see cref="FromString(string)"/>.
		/// </para>
		/// <para>
		/// DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(string)"/>.
		/// </para>
		/// </remarks>
		/// <see cref="FromByteString(string)"/>
		/// <see cref="FromStringAscii(string)"/>
		[Pure]
		public static Slice FromStringUtf8(string? value)
		{
			//REVIEW: what if people call FromString("\xFF/some/system/path") by mistake?
			// Should be special case when the string starts with \xFF (or \xFF\xFF)? What about \xFE ?
			return value == null ? default
			     : value.Length == 0 ? Empty
			     : new Slice(Utf8NoBomEncoding.GetBytes(value));
		}

		/// <summary>Create a <see cref="Slice"/> containing the UTF-8 bytes of subsection of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>The slice will NOT include the UTF-8 BOM.</para>
		/// <para>This method will not try to identify ASCII-only strings:
		/// If the string provided can ONLY contain ASCII, you should use <see cref="FromStringAscii(System.ReadOnlySpan{char})"/>.
		/// If it is more frequent for the string to be ASCII-only than having UNICODE characters, consider using <see cref="FromString(System.ReadOnlySpan{char})"/>.
		/// </para>
		/// <para>
		/// DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(ReadOnlySpan{char})"/>.
		/// </para>
		/// </remarks>
		/// <see cref="FromByteString(System.ReadOnlySpan{char})"/>
		/// <see cref="FromStringAscii(System.ReadOnlySpan{char})"/>
		[Pure]
		public static Slice FromStringUtf8(ReadOnlySpan<char> value)
		{
			if (value.Length == 0) return Empty;

			var capacity = Utf8NoBomEncoding.GetByteCount(value);
			var buffer = new byte[capacity];

			int written = Utf8NoBomEncoding.GetBytes(value, buffer);
			Contract.Debug.Assert(written == capacity);

			return new(buffer, 0, written);
		}

		/// <summary>Creates a <see cref="SliceOwner"/> containing the UTF-8 bytes of subsection of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>The slice will NOT include the UTF-8 BOM.</para>
		/// <para>This method will not try to identify ASCII-only strings:
		/// If the string provided can ONLY contain ASCII, you should use <see cref="FromStringAscii(System.ReadOnlySpan{char},ref byte[])"/>.
		/// If it is more frequent for the string to be ASCII-only than having UNICODE characters, consider using <see cref="FromString(System.ReadOnlySpan{char},ref byte[])"/>.
		/// </para>
		/// <para>
		/// DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(System.ReadOnlySpan{char},ref byte[])"/>.
		/// </para>
		/// </remarks>
		/// <see cref="FromByteString(System.ReadOnlySpan{char},ref byte[])"/>
		/// <see cref="FromStringAscii(System.ReadOnlySpan{char},ref byte[])"/>
		[Pure, MustDisposeResource]
		public static SliceOwner FromStringUtf8(ReadOnlySpan<char> value, ArrayPool<byte> pool)
		{
			if (value.Length == 0) return SliceOwner.Empty;

			var capacity = Utf8NoBomEncoding.GetByteCount(value);
			var buffer = pool.Rent(capacity);

			int written = Utf8NoBomEncoding.GetBytes(value, buffer);
			Contract.Debug.Assert(written == capacity);

			return SliceOwner.Create(new(buffer, 0, written), pool);
		}

		/// <summary>Create a slice containing the UTF-8 bytes of subsection of the string <paramref name="value"/>.</summary>
		/// <remarks>
		/// <para>The slice will NOT include the UTF-8 BOM.</para>
		/// <para>This method will not try to identify ASCII-only strings:
		/// If the string provided can ONLY contain ASCII, you should use <see cref="FromStringAscii(ReadOnlySpan{char})"/>.
		/// If it is more frequent for the string to be ASCII-only than having UNICODE characters, consider using <see cref="FromString(ReadOnlySpan{char})"/>.
		/// </para>
		/// <para>
		/// DO NOT call this method to encode special strings that contain binary prefixes, like "\xFF/some/system/path" or "\xFE\x01\x02\x03", because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(ReadOnlySpan{char})"/>.
		/// </para>
		/// </remarks>
		[Pure]
		public static Slice FromStringUtf8(ReadOnlySpan<char> value, ref byte[]? buffer, out bool asciiOnly)
		{
			if (value.Length == 0)
			{
				asciiOnly = true;
				return Empty;
			}

			unsafe
			{
				//note: there is no direct way to GetBytes(..) from a segment of a string, without going to char pointers :(
				fixed (char* inp = &MemoryMarshal.GetReference(value))
				{
					int len = Utf8NoBomEncoding.GetByteCount(inp, value.Length);
					Contract.Debug.Assert(len > 0);

					//TODO: we could optimize conversion if we know it is only ascii!
					asciiOnly = len == value.Length;

					// write UTF-8 bytes to buffer
					var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, len);
					fixed (byte* outp = &tmp[0])
					{
						//TODO: PERF: if len == count, we know it is ASCII only and could optimize for that case?
						if (len != Utf8NoBomEncoding.GetBytes(inp, value.Length, outp, len))
						{
#if DEBUG
							// there is a mismatch between GetByteCount() and what GetBytes() produced!
							if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
							throw new InvalidOperationException("UTF-8 string size estimation failed.");
						}
						return new Slice(tmp, 0, len);
					}
				}
			}
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>, prefixed by the UTF-8 BOM.</summary>
		/// <remarks>
		/// <para>If the string is <c>null</c>, the <see cref="Slice.Nil"/> slice is returned.</para>
		/// <para>If the string is empty, the UTF-8 BOM is returned.</para>
		/// <para>
		/// DO NOT call this method to encode special strings that contain binary prefixes, like <c>"\xFF/some/system/path"</c> or <c>"\xFE\x01\x02\x03"</c>, because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(string)"/>.
		/// </para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromStringUtf8WithBom(string? value)
		{
			//REVIEW: what if people call FromString("\xFF/some/system/path") by mistake?
			// Should be special case when the string starts with \xFF (or \xFF\xFF)? What about \xFE ?
			if (value == null) return Slice.Nil;
			byte[]? __ = null;
			return FromStringUtf8WithBom(value.AsSpan(), ref __, out _);
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>, prefixed by the UTF-8 BOM.</summary>
		/// <remarks>
		/// If the string is null, an empty slice is returned.
		/// If the string is empty, the UTF-8 BOM is returned.
		/// DO NOT call this method to encode special strings that contain binary prefixes, like "\xFF/some/system/path" or "\xFE\x01\x02\x03", because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(string)"/>.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromStringUtf8WithBom(ReadOnlySpan<char> value)
		{
			byte[]? __ = null;
			return FromStringUtf8WithBom(value, ref __, out _);
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>, prefixed by the UTF-8 BOM.</summary>
		/// <remarks>
		/// If the string is null, an empty slice is returned.
		/// If the string is empty, the UTF-8 BOM is returned.
		/// DO NOT call this method to encode special strings that contain binary prefixes, like "\xFF/some/system/path" or "\xFE\x01\x02\x03", because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(ReadOnlySpan{char})"/>.
		/// </remarks>
		[Pure]
		public static Slice FromStringUtf8WithBom(ReadOnlySpan<char> value, [NotNull] ref byte[]? buffer, out bool asciiOnly)
		{
			if (value.Length == 0)
			{
				//note: cannot use a singleton buffer because it could be mutated by the caller!
				var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, 8);
				tmp[0] = 0xEF;
				tmp[1] = 0xBB;
				tmp[2] = 0xBF;
				asciiOnly = true;
				return new Slice(tmp, 0, 3);
			}
			unsafe
			{
				fixed (char* pchars = &MemoryMarshal.GetReference(value))
				{
					int len = Utf8NoBomEncoding.GetByteCount(pchars, value.Length);

					//TODO: we could optimize conversion if we know it is only ascii!
					asciiOnly = len == value.Length;

					// account for the BOM!
					len = checked(3 + len);

					// write UTF-8 bytes to buffer
					var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, len);
					fixed (byte* outp = &tmp[0])
					{
						outp[0] = 0xEF;
						outp[1] = 0xBB;
						outp[2] = 0xBF;
						Utf8NoBomEncoding.GetBytes(pchars, value.Length, outp + 3, tmp.Length - 3);
					}
					return new Slice(tmp, 0, len);
				}
			}
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/>, prefixed by the UTF-8 BOM.</summary>
		/// <remarks>
		/// If the string is null, an empty slice is returned.
		/// If the string is empty, the UTF-8 BOM is returned.
		/// DO NOT call this method to encode special strings that contain binary prefixes, like "\xFF/some/system/path" or "\xFE\x01\x02\x03", because they do not map to UTF-8 directly.
		/// For these case, or when you know that the string only contains ASCII only (with 100% certainty), you should use <see cref="FromByteString(ReadOnlySpan{char})"/>.
		/// </remarks>
		[Pure]
		private static Slice ConvertByteStringNoCheck(ReadOnlySpan<char> value, ref byte[]? buffer)
		{
			int len = value.Length;
			if (len == 0) return Empty;
			if (len == 1) return FromByte((byte) value[0]);

			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, len);
			unsafe
			{
				fixed (byte* pBytes = &tmp[0])
				fixed (char* pChars = &MemoryMarshal.GetReference(value))
				{
					byte* outp = pBytes;
					byte* stop = pBytes + len;
					char* inp = pChars;
					while (outp < stop)
					{
						*outp++ = (byte) *inp++;
					}
				}
			}
			return new Slice(tmp, 0, len);
		}

		/// <summary>Create a slice that holds the UTF-8 encoded representation of <paramref name="value"/></summary>
		/// <param name="value"></param>
		/// <returns>The returned slice is only guaranteed to hold 1 byte for ASCII chars (0..127). For non-ASCII chars, the size can be from 1 to 6 bytes.
		/// If you need to use ASCII chars, you should use Slice.FromByte() instead</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice FromChar(char value)
		{
#if NET8_0_OR_GREATER
			return Ascii.IsValid(value) ? FromByte((byte) value) : FromCharSlow(value);
#else
			return value < 128 ? FromByte((byte) value) : FromCharSlow(value);
#endif

			static Slice FromCharSlow(char value)
			{
				byte[]? _ = null;
				return FromChar(value, ref _);
			}
		}

		/// <summary>Create a slice that holds the UTF-8 encoded representation of <paramref name="value"/></summary>
		/// <returns>The returned slice is only guaranteed to hold 1 byte for ASCII chars (0..127). For non-ASCII chars, the size can be from 1 to 6 bytes.
		/// If you need to use ASCII chars, you should use Slice.FromByte() instead</returns>
		[Pure]
		public static Slice FromChar(char value, ref byte[]? buffer)
		{
#if NET8_0_OR_GREATER
			return Ascii.IsValid(value) ? FromByte((byte) value) : FromCharSlow(value, ref buffer);
#else
			return value < 128 ? FromByte((byte) value) : FromCharSlow(value, ref buffer);
#endif

			static Slice FromCharSlow(char value, ref byte[]? buffer)
			{
				// note: Encoding.UTF8.GetMaxByteCount(1) returns 6, but allocate 8 to stay aligned
				var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, 8);
				unsafe
				{
					fixed (byte* ptr = &tmp[0])
					{
						int n = Utf8NoBomEncoding.GetBytes(&value, 1, ptr, tmp.Length);
						return n == 1 ? FromByte(tmp[0]) : new Slice(tmp, 0, n);
					}
				}
			}
		}

		/// <summary>Convert a hexadecimal encoded string ("1234AA7F") into a slice</summary>
		/// <param name="hexString">String contains a sequence of pairs of hexadecimal digits with no separating spaces.</param>
		/// <returns>Slice containing the decoded byte array, or an exception if the string is empty or has an odd length</returns>
		[Pure]
		public static Slice FromHexString(string? hexString)
		{
			if (string.IsNullOrEmpty(hexString))
			{
				return hexString == null ? default : Empty;
			}

			return new Slice(Convert.FromHexString(hexString));
		}

		/// <summary>Convert a hexadecimal encoded string ("1234AA7F") into a slice</summary>
		/// <param name="hexString">String contains a sequence of pairs of hexadecimal digits with no separating spaces.</param>
		/// <returns>Slice containing the decoded byte array, or an exception if the string is empty or has an odd length</returns>
		[Pure]
		public static Slice FromHexString(ReadOnlySpan<char> hexString)
		{
			if (hexString.Length == 0)
			{
				return Empty;
			}

			return new Slice(Convert.FromHexString(hexString));
		}


		/// <summary>Convert a hexadecimal encoded string ("1234AA7F") into a slice</summary>
		/// <param name="hexString">String contains a sequence of pairs of hexadecimal digits with no separating spaces.</param>
		/// <param name="separator">Allowed separator character between hex pairs, or <c>'\0'</c> for no separator allowed. Note strings with no separators are always allowed</param>
		/// <returns>Slice containing the decoded byte array, or an exception if the string is empty or has an odd length</returns>
#if NET8_0_OR_GREATER
		public static Slice FromHexString(string? hexString, [ConstantExpected] char separator)
#else
		public static Slice FromHexString(string? hexString, char separator)
#endif
			=> separator == '\0' ? FromHexString(hexString) : FromHexa(hexString, separator);

		/// <summary>Convert a hexadecimal encoded string ("1234AA7F") into a slice, ignoring any spaces characters.</summary>
		/// <param name="hexString">String contains a sequence of pairs of hexadecimal digits, and optional separating white-spaces.</param>
		/// <param name="separator">Allowed separator character between hex pairs, or <c>'\0'</c> for no separator allowed. Note strings with no separators are always allowed</param>
		/// <returns>Slice containing the decoded byte array, or an exception if the string is empty or has an odd length</returns>
		/// <remarks>If the string is known to not have any spaces or separators, it is faster to call <see cref="Slice.FromHexString(string)"/>.</remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice FromHexa(
			string? hexString,
#if NET8_0_OR_GREATER
			[ConstantExpected]
#endif
			char separator = ' '
		)
		{
			if (string.IsNullOrEmpty(hexString))
			{
				return hexString == null ? default : Empty;
			}

			return FromHexa(hexString.AsSpan(), separator);
		}

		/// <summary>Convert a hexadecimal encoded string ("1234AA7F") into a slice</summary>
		/// <param name="hexString">String contains a sequence of pairs of hexadecimal digits with no separating spaces.</param>
		/// <param name="separator">Allowed separator character between hex pairs, or <c>'\0'</c> for no separator allowed. Note strings with no separators are always allowed</param>
		/// <returns>Slice containing the decoded byte array, or an exception if the string is empty or has an odd length</returns>
		/// <remarks>If the string is known to not have any spaces or separators, it is faster to call <see cref="Slice.FromHexString(string)"/>.</remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice FromHexa(
			ReadOnlySpan<char> hexString,
#if NET8_0_OR_GREATER
			[ConstantExpected]
#endif
			char separator = ' '
		)
		{
			if (hexString.Length == 0)
			{
				return Empty;
			}

			if (separator == '\0' || (hexString.Length & 2) == 0 && !hexString.Contains(separator))
			{ // looks like a compact string, use the optimized runtime version
				return new(Convert.FromHexString(hexString));
			}

			// slower version
			byte[]? buffer = null;
			int written = UnsafeHelpers.FromHexa(hexString, ref buffer, separator);
			return new(buffer, 0, written);
		}

		/// <summary>Decode the string that was generated by slice.ToString() or Slice.Dump(), back into the original slice</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		public static Slice Unescape(string? value) //REVIEW: rename this to Decode() if we changed Dump() to Encode()
		{
			if (string.IsNullOrEmpty(value))
			{
				return value == null ? default : Empty;
			}

			byte[]? buffer = null;
			int written = UnsafeHelpers.Unescape(value.AsSpan(), ref buffer);
			return new Slice(buffer, 0, written);
		}

		/// <summary>Decode the string that was generated by slice.ToString() or Slice.Dump(), back into the original slice</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		public static Slice Unescape(ReadOnlySpan<char> value) //REVIEW: rename this to Decode() if we changed Dump() to Encode()
		{
			if (value.Length == 0)
			{
				return Empty;
			}

			byte[]? buffer = null;
			int written = UnsafeHelpers.Unescape(value, ref buffer);
			return new Slice(buffer, 0, written);
		}

		#endregion

		#region ToXXX

		/// <summary>Stringify a slice containing characters in the operating system's current ANSI codepage</summary>
		/// <returns>Decoded string, or null if the slice is <see cref="Nil"/></returns>
		/// <remarks>
		/// Calling this method on a slice that is not ANSI, or was generated with different codepage than the current process, will return a corrupted string!
		/// This method should ONLY be used to interop with the Win32 API or unmanaged libraries that require the ANSI codepage!
		/// You SHOULD *NOT* use this to expose data to other systems or locale (via sockets, files, ...)
		/// If you are decoding natural text, you should probably change the encoding at the source to be UTF-8!
		/// If you are decoding identifiers or keywords that are known to be ASCII only, you should use <see cref="ToStringAscii"/> instead (safe).
		/// If these identifiers can contain 'special' bytes (like \xFF or \xFE), you should use <see cref="ToByteString"/> instead (unsafe).
		/// </remarks>
		[Pure]
		public string? ToStringAnsi()
		{
			if (this.Count == 0) return this.Array != null ? string.Empty : null;
			//note: Encoding.GetString() will do the bound checking for us
			return Encoding.Default.GetString(this.Array, this.Offset, this.Count);
		}

		/// <summary>Stringify a slice containing 7-bit ASCII characters only</summary>
		/// <returns>Decoded string, or null if the slice is null</returns>
		/// <remarks>
		/// This method should ONLY be used to decoded data that is GUARANTEED to be in the range 0..127.
		/// This method will THROW if any byte in the slice has bit 7 set to 1 (ie: >= 0x80)
		/// If you are decoding identifiers or keywords with 'special' bytes (like \xFF or \xFE), you should use <see cref="ToByteString"/> instead.
		/// If you are decoding natural text, or text from unknown origin, you should use <see cref="ToStringUtf8"/> or <see cref="ToUnicode"/> instead.
		/// If you are attempting to decode a string obtain from a Win32 or unmanaged library call, you should use <see cref="ToStringAnsi"/> instead.
		/// </remarks>
		[Pure]
		public string? ToStringAscii()
		{
			if (this.Count == 0)
			{
				return this.Array != null ? string.Empty : null;
			}

			var span = this.Span;
#if NET8_0_OR_GREATER
			if (Ascii.IsValid(span))
			{
				return UnsafeHelpers.ConvertToByteString(span);
			}
#else
			if (UnsafeHelpers.IsAsciiBytes(span))
			{
				return UnsafeHelpers.ConvertToByteString(span);
			}
#endif

			throw new DecoderFallbackException("The slice contains at least one non-ASCII character");
		}

		/// <summary>Stringify a slice containing only ASCII chars</summary>
		/// <returns>ASCII string, or null if the slice is null</returns>
		[Pure]
		public string? ToByteString() //REVIEW: rename to ToStringSOMETHING(): ToStringByte()? ToStringRaw()?
		{
			return this.Count == 0
				? (this.Array != null ? string.Empty : null)
				: UnsafeHelpers.ConvertToByteString(this.Span);
		}

		/// <summary>Stringify a slice containing either 7-bit ASCII, or UTF-8 characters</summary>
		/// <returns>Decoded string, or null if the slice is null. The encoding will be automatically detected</returns>
		/// <remarks>
		/// This should only be used for slices produced by any of the <see cref="FromString(string?)"/>, <see cref="FromStringUtf8(string?)"/>, <see cref="FromStringUtf8WithBom(string?)"/>, <see cref="FromByteString(string?)"/> or <see cref="FromStringAscii(string?)"/> methods.
		/// This is NOT compatible with slices produced by <see cref="FromStringAnsi(string?)"/> or encoded with any specific encoding or code page.
		/// This method will NOT automatically remove the UTF-8 BOM if present (use <see cref="ToStringUtf8"/> if you need this)
		/// </remarks>
		[Pure]
		public string? ToUnicode() //REVIEW: rename this to ToStringUnicode() ?
		{
			var span = this.Span;
			return span.Length == 0 ? (this.Array != null ? string.Empty : null)
#if NET8_0_OR_GREATER
				: System.Text.Ascii.IsValid(span) ? UnsafeHelpers.ConvertToByteString(span) : Utf8NoBomEncoding.GetString(span);
#else
				: UnsafeHelpers.IsAsciiBytes(span) ? UnsafeHelpers.ConvertToByteString(span) : Utf8NoBomEncoding.GetString(span);
#endif
		}

		[Pure]
		private static bool HasUtf8Bom(byte[] array, int offset, int count)
		{
			return count >= 3
			    && (uint) (offset + count) <= (uint) array.Length
			    && array[offset + 0] == 0xEF
			    && array[offset + 1] == 0xBB
			    && array[offset + 2] == 0xBF;
		}

		/// <summary>Decode a slice that is known to contain a UTF-8 encoded string with an optional UTF-8 BOM</summary>
		/// <returns>Decoded string, or null if the slice is null</returns>
		/// <exception cref="DecoderFallbackException">If the slice contains one or more invalid UTF-8 sequences</exception>
		/// <remarks>
		/// This method will THROW if the slice does not contain valid UTF-8 sequences.
		/// This method will remove any UTF-8 BOM if present. If you need to keep the BOM as the first character of the string, use <see cref="ToUnicode"/>
		/// </remarks>
		[Pure]
		public string? ToStringUtf8()
		{
			int count = this.Count;
			var array = this.Array;
			if (count == 0) return array != null ? string.Empty : null;

			// detect BOM
			int offset = this.Offset;
			if (HasUtf8Bom(array, offset, count))
			{ // skip it!
				offset += 3;
				count -= 3;
				if (count == 0) return string.Empty;
			}
			return Slice.Utf8NoBomEncoding.GetString(array, offset, count);
		}

		/// <summary>Converts a slice using Base64 encoding</summary>
		[Pure]
		public string? ToBase64()
		{
			if (this.Count == 0) return this.Array != null ? string.Empty : null;
			//note: Convert.ToBase64String() will do the bound checking for us
			return Convert.ToBase64String(this.Array, this.Offset, this.Count);
		}

		/// <summary>Converts a slice into a string with each byte encoded into uppercase hexadecimal (<c>0-9A-F</c>)</summary>
		/// <returns>ex: <c>"0123456789ABCDEF"</c></returns>
		/// <remarks>The result is identical to the <c>"N"</c> format when calling <see cref="ToString(string?,System.IFormatProvider?)"/> or <see cref="TryFormat"/></remarks>
		public string ToHexString()
			=> Convert.ToHexString(this.Span);

		/// <summary>Converts a slice into a string with each byte encoded into lowercase hexadecimal (<c>0-9a-f</c>)</summary>
		/// <returns>ex: <c>"0123456789abcdef"</c></returns>0
		/// <remarks>The result is identical to the <c>"n"</c> format when calling <see cref="ToString(string?,System.IFormatProvider?)"/> or <see cref="TryFormat"/></remarks>
		public string ToHexStringLower()
#if NET9_0_OR_GREATER
			=> Convert.ToHexStringLower(this.Span);
#else
			=> this.Span.ToHexaString('\0', lowerCase: true);
#endif

		/// <summary>Converts a slice into a string with each byte encoded into uppercase hexadecimal (<c>0-9A-F</c>), separated by a character</summary>
		/// <param name="sep">Character used to separate the hexadecimal pairs (ex: <c>' '</c>)</param>
		/// <returns>"01 23 45 67 89 AB CD EF"</returns>
		/// <remarks>When <paramref name="sep"/> is <c>' '</c>, the result is identical to the <c>"X"</c> format when calling <see cref="ToString(string?,System.IFormatProvider?)"/> or <see cref="TryFormat"/></remarks>
		[Pure]
		public string ToHexString(char sep)
		{
			return sep != '\0' ? this.Span.ToHexaString(sep, lowerCase: false) : Convert.ToHexString(this.Span);
		}

		/// <summary>Converts a slice into a string with each byte encoded into lowercase hexadecimal (<c>0-9a-f</c>), separated by a character</summary>
		/// <param name="sep">Character used to separate the hexadecimal pairs (ex: <c>' '</c>)</param>
		/// <returns>"01 23 45 67 89 ab cd ef"</returns>
		/// <remarks>When <paramref name="sep"/> is <c>' '</c>, the result is identical to the <c>"n"</c> format when calling <see cref="ToString(string?,System.IFormatProvider?)"/> or <see cref="TryFormat"/></remarks>
		[Pure]
		public string ToHexStringLower(char sep)
		{
			return sep != '\0' ? this.Span.ToHexaString(sep, lowerCase: true) : ToHexStringLower();
		}

		internal static StringBuilder EscapeString(StringBuilder? sb, ReadOnlySpan<byte> buffer, Encoding encoding)
		{
			int n = encoding.GetCharCount(buffer);
			if (n <= 4096)
			{
				Span<char> tmp = stackalloc char[n];
				n = encoding.GetChars(buffer, tmp);
				return EscapeString(sb, tmp[..n]);
			}
			else
			{
				var buf = ArrayPool<char>.Shared.Rent(n);
				n = encoding.GetChars(buffer, buf);
				return EscapeString(sb, buf.AsSpan(0, n));
			}
		}

		internal static StringBuilder EscapeString(StringBuilder? sb, ReadOnlySpan<char> chars)
		{
			sb ??= new(chars.Length + 16);
			foreach (var c in chars)
			{
				switch (c)
				{
					case (>= ' ' and <= '~') or (>= '\u0370' and <= '\u07ff') or (>= '\u3040' and <= '\u312f'):
					{ // printable character
						sb.Append(c);
						break;
					}
					case '\0':
					{ // NUL
						sb.Append(@"\0");
						break;
					}
					case '\n':
					{ // LF
						sb.Append(@"\n");
						break;
					}
					case '\r':
					{ // CR
						sb.Append(@"\r");
						break;
					}
					case '\t':
					{ // TAB
						sb.Append(@"\t");
						break;
					}
					case > '\x7F':
					{ // UNICODE
						sb.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
						break;
					}
					default:
					{ // ASCII
						sb.Append(@"\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
						break;
					}
				}
			}
			return sb;
		}

		/// <summary>Helper method that dumps the slice as a string (if it contains only printable ascii chars) or a hex array if it contains non-printable chars. It should only be used for logging and troubleshooting !</summary>
		/// <returns>Returns either "'abc'", "&lt;00 42 7F&gt;", or "{ ...JSON... }". Returns "''" for Slice.Empty, and "" for <see cref="Slice.Nil"/></returns>
		[Pure]
		public string PrettyPrint()
		{
			if (this.Count == 0) return this.Array != null ? "''" : string.Empty;
			return PrettyPrint(this.Span, DefaultPrettyPrintSize, biasKey: false, lower: false); //REVIEW: constant for max size!
		}

		/// <summary>Helper method that dumps the slice as a string (if it contains only printable ascii chars) or a hex array if it contains non-printable chars. It should only be used for logging and troubleshooting !</summary>
		/// <param name="maxLen">Truncate the slice if it exceeds this size</param>
		/// <returns>Returns either "'abc'", "&lt;00 42 7F&gt;", or "{ ...JSON... }". Returns "''" for Slice.Empty, and "" for <see cref="Slice.Nil"/></returns>
		[Pure]
		public string PrettyPrint(int maxLen)
		{
			if (this.Count == 0) return this.Array != null ? "''" : string.Empty;
			return PrettyPrint(this.Span, maxLen, biasKey: false, lower: false);
		}

		internal const int DefaultPrettyPrintSize = 1024;

		/// <summary>Helper method that dumps the slice as a string (if it contains only printable ascii chars) or a hex array if it contains non-printable chars. It should only be used for logging and troubleshooting !</summary>
		[Pure]
		internal static string PrettyPrint(ReadOnlySpan<byte> buffer, int maxLen, bool? biasKey, bool lower)
		{
			// this method tries to guess what the hxll is in the slice in order to render an intelligible text (to logs, for the debugger, ...)
			// We can have any of the following (all with the same size)
			// - Text: should be rendered inside quotes ('...')
			//   - regular ASCII text "Hello World"
			//   - regular UTF-8 encoded text "Héllo Wôrld!"
			//   - text with control codes like \r, \n, \t or '\' that must be escaped nicely(ex: '\n' => "\\n")
			//   - text in simple binary encodings with some prefix/suffix (ex: "<02>Hello World!<00>" for tuples)
			// - Binary: any fixed or variable size integer, GUID, Date, ...
			//   - for small 32/64 bits values, it would be nice to also see the converted decimal value
			//   - keys will probably be high-endian (ordered)
			//   - values will probably be little-endian (unordered)
			//   - compressed or random values that have absolutely no discernable features
			// - Any mixture of the two (ex: a GUID followed by an utf-8 string

			var count = buffer.Length;
			if (count == 0) return "''";

			if (biasKey != true)
			{
				// look for UTF-8 BOM
				if (count >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
				{ // this is supposed to be a UTF-8 string
					return EscapeString(new StringBuilder(count).Append('\''), count > maxLen ? buffer[..maxLen] : buffer, Slice.Utf8NoBomEncoding).Append('\'').ToString();
				}

				if (count >= 2)
				{
					// look for JSON objets or arrays
					if ((buffer[0] == '{' && buffer[^1] == '}') || (buffer[0] == '[' && buffer[^1] == ']'))
					{
						try
						{
							return count <= maxLen
								? EscapeString(new StringBuilder(count + 16), buffer, Slice.Utf8NoBomEncoding).ToString()
								: EscapeString(new StringBuilder(count + 16), buffer[..maxLen], Slice.Utf8NoBomEncoding).Append("[\u2026]").Append(buffer[^1]).ToString();
						}
						catch (DecoderFallbackException)
						{
							// sometimes, binary data "looks" like valid JSON but is not, so we just ignore it (even if we may have done a bunch of work for nothing)
						}
					}
				}

				// is it all zeroes?
				if (!buffer.ContainsAnyExcept((byte) 0))
				{
					return DumpLower(buffer, maxLen);
				}
			}

			// do a first pass on the slice to look for non-printable characters
			bool mustEscape = false;
			int n = count;
			int p = 0;
			while (n-- > 0)
			{
				byte b = buffer[p++];
				if (b is >= 32 and < 127)
				{ // printable character
					continue;
				}

				// we accept via escaping the following special chars: CR, LF, TAB
				if (b is 0 or 10 or 13 or 9 or 127)
				{ // non-printable, but has a printable version, like \r, \n, \0, etc...
					mustEscape = true;
					continue;
				}

				// this looks like binary
				//TODO: if biasKey == false && count == 2|4|8, maybe try to decode as an integer?
				return lower
					? Slice.DumpLower(buffer, maxLen)
					: Slice.Dump(buffer, maxLen);
			}

			if (!mustEscape)
			{ // only printable chars found
				return count <= maxLen
					? "'" + Encoding.ASCII.GetString(buffer) + "'"
					: "'" + Encoding.ASCII.GetString(buffer[..maxLen]) + "[\u2026]'"; // Unicode for '...'
			}

			// some escaping required
			return count <= maxLen
				? EscapeString(new StringBuilder(count + 2).Append('\''), buffer, Slice.Utf8NoBomEncoding).Append('\'').ToString()
				: EscapeString(new StringBuilder(count + 2).Append('\''), buffer[..maxLen], Slice.Utf8NoBomEncoding).Append("[\u2026]'").ToString();
		}

		private string ToDebuggerDisplay()
		{
			if (this.Count == 0) return this.Array == null ? "<null>" : "<empty>";
			return $"[{Count}] {PrettyPrint()}";
		}

		/// <summary>Converts a slice into a byte</summary>
		/// <returns>Value of the first and only byte of the slice, or 0 if the slice is null or empty.</returns>
		/// <exception cref="System.FormatException">If the slice has more than one byte</exception>
		[Pure]
		public byte ToByte()
		{
			switch (this.Count)
			{
				case 0: return 0;
				case 1: return this.Array[this.Offset];
				default:
					if (this.Count < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
					return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<byte>(1);
			}
		}

		/// <summary>Converts a slice into a signed byte (-128...+127)</summary>
		/// <returns>Value of the first and only byte of the slice, or 0 if the slice is null or empty.</returns>
		/// <exception cref="System.FormatException">If the slice has more than one byte</exception>
		[Pure]
		public sbyte ToSByte()
		{
			switch (this.Count)
			{
				case 0: return 0;
				case 1: return (sbyte)this.Array[this.Offset];
				default:
					if (this.Count < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
					return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<sbyte>(1);
			}
		}

		/// <summary>Converts a slice into a boolean.</summary>
		/// <returns>False if the slice is empty, or is equal to the byte 0; otherwise, true.</returns>
		[Pure]
		public bool ToBool()
		{
			EnsureSliceIsValid();
			// Anything apart from nil/empty, or the byte 0 itself is considered truthy.
			return this.Count > 1 || (this.Count == 1 && this.Array[this.Offset] != 0);
			//TODO: consider checking if the slice consist of only zeroes ? (ex: Slice.FromFixed32(0) could be considered falsy ...)
		}

		#region 16 bits...

		
		/// <summary>Converts a slice into a little-endian encoded, signed 16-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the slice</exception>
		[Pure]
		public short ToInt16()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadInt16LittleEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<short>(2)
			};
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 16-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the slice</exception>
		[Pure]
		public short ToInt16BE()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadInt16BigEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<short>(2)
			};
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 16-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the slice</exception>
		[Pure]
		public ushort ToUInt16()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<ushort>(2),
			};
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 16-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the slice</exception>
		[Pure]
		public ushort ToUInt16BE()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16BigEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<ushort>(2),
			};
		}

		/// <summary>Read a variable-length, little-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 2)</param>
		/// <returns>Decoded unsigned short.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 2.</exception>
		[Pure]
		public ushort ReadUInt16(int offset, int bytes)
		{
			if ((uint) bytes > 2) goto fail;

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);
			switch (bytes)
			{
				case 0: return 0;
				case 1: return buffer[p];
				default: return (ushort)(buffer[p] | (buffer[p + 1] << 8));
			}
		fail:
			throw new ArgumentOutOfRangeException(nameof(bytes));
		}

		/// <summary>Read a variable-length, big-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 2)</param>
		/// <returns>Decoded unsigned short.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 2.</exception>
		[Pure]
		public ushort ReadUInt16BE(int offset, int bytes)
		{
			if ((uint) bytes > 2) goto fail;

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);
			switch (bytes)
			{
				case 0: return 0;
				case 1: return buffer[p];
				default: return (ushort)(buffer[p + 1] | (buffer[p] << 8));
			}
		fail:
			throw new ArgumentOutOfRangeException(nameof(bytes));
		}

		#endregion

		#region 24 bits...

		//note: all 'Int24' and 'UInt24' are represented in memory as Int32/UInt32 using only the lowest 24 bits (upper 8 bits will be IGNORED)
		//note: 'FF FF' is equivalent to '00 FF FF', so is considered to be positive (= 65535)

		/// <summary>Converts a slice into a little-endian encoded, signed 24-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the slice</exception>
		[Pure]
		public int ToInt24()
		{
			EnsureSliceIsValid();
			int count = this.Count;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = &DangerousGetPinnableReference())
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16LE(ptr); // cannot be negative
						case 3: return UnsafeHelpers.LoadInt24LE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(3);
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 24-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the slice</exception>
		[Pure]
		public int ToInt24BE()
		{
			EnsureSliceIsValid();
			int count = this.Count;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = &DangerousGetPinnableReference())
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16BE(ptr);
						case 3: return UnsafeHelpers.LoadInt24BE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(3);
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 24-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the slice</exception>
		[Pure]
		public uint ToUInt24()
		{
			EnsureSliceIsValid();
			int count = this.Count;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = &DangerousGetPinnableReference())
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16LE(ptr);
						case 3: return UnsafeHelpers.LoadUInt24LE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(3);
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 24-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the slice</exception>
		[Pure]
		public uint ToUInt24BE()
		{
			EnsureSliceIsValid();
			int count = this.Count;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = &DangerousGetPinnableReference())
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16BE(ptr);
						case 3: return UnsafeHelpers.LoadUInt24BE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(3);
		}

		/// <summary>Read a variable-length, little-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 2)</param>
		/// <returns>Decoded unsigned short.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 3.</exception>
		[Pure]
		public uint ReadUInt24(int offset, int bytes)
		{
			if ((uint) bytes > 3) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);
			switch (bytes)
			{
				case 0: return 0;
				case 1: return buffer[p];
				case 2: return (uint)(buffer[p] | (buffer[p + 1] << 8));
				default: return (uint)(buffer[p] | (buffer[p + 1] << 8) | (buffer[p + 2] << 16));
			}
		}

		/// <summary>Read a variable-length, big-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 2)</param>
		/// <returns>Decoded unsigned short.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 3.</exception>
		[Pure]
		public ushort ReadUInt24BE(int offset, int bytes)
		{
			if ((uint) bytes > 3) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);
			switch (bytes)
			{
				case 0: return 0;
				case 1: return buffer[p];
				case 2: return (ushort)(buffer[p + 1] | (buffer[p] << 8));
				default: return (ushort)(buffer[p + 2] | (buffer[p + 1] << 8) | (buffer[p] << 16));
			}
		}

		#endregion

		#region 32 bits...

		/// <summary>Converts a slice into a little-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public int ToInt32()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				3 => span[0] | (span[1] << 8) | (span[2] << 16),
				4 => BinaryPrimitives.ReadInt32LittleEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(4)
			};
		}

		/// <summary>Converts a slice into a little-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToInt32LittleEndian(out int value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadInt16LittleEndian(span); return true;
				case 3: value = span[0] | (span[1] << 8) | (span[2] << 16); return true;
				case 4: value = BinaryPrimitives.ReadInt32LittleEndian(span); return true;
				default: value = 0; return false;
			}
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public int ToInt32BE()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16BigEndian(span),
				3 => span[2] | (span[1] << 8) | (span[0] << 16),
				4 => BinaryPrimitives.ReadInt32BigEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(4)
			};
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToInt32BigEndian(out int value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16BigEndian(span); return true;
				case 3: value = span[2] | (span[1] << 8) | (span[0] << 16); return true;
				case 4: value = BinaryPrimitives.ReadInt32BigEndian(span); return true;
				default: value = 0; return false;
			}
		}
		/// <summary>Converts a slice into a little-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public uint ToUInt32()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				3 => unchecked((uint) (span[0] | (span[1] << 8) | (span[2] << 16))),
				4 => BinaryPrimitives.ReadUInt32LittleEndian(span),
				_ => throw UnsafeHelpers.Errors.SliceTooLargeForConversion<uint>(4)
			};
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToUInt32LittleEndian(out uint value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16LittleEndian(span); return true;
				case 3: value = unchecked((uint) (span[0] | (span[1] << 8) | (span[2] << 16))); return true;
				case 4: value = BinaryPrimitives.ReadUInt32LittleEndian(span); return true;
				default: value = 0; return false;
			}
		}

		/// <summary>Converts a slice into a big-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public uint ToUInt32BE()
		{
			var span = this.Span;
			return this.Count switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16BigEndian(span),
				3 => (uint) ((span[0] << 16) | (span[1] << 8) | span[2]),
				4 => BinaryPrimitives.ReadUInt32BigEndian(span),
				_ => UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(4)
			};
		}

		/// <summary>Converts a slice into a big-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToUInt32BigEndian(out uint value)
		{
			var span = this.Span;
			switch (this.Count)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16BigEndian(span); return true;
				case 3: value = (uint)((span[0] << 16) | (span[1] << 8) | span[2]); return true;
				case 4: value = BinaryPrimitives.ReadUInt32BigEndian(span); return true;
				default: value = 0; return false;
			}
		}

		/// <summary>Read a variable-length, little-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 4)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 4.</exception>
		[Pure]
		public uint ReadUInt32(int offset, int bytes)
		{
			if (bytes == 0) return 0;
			if ((uint) bytes > 4) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset) + bytes - 1;

			uint value = buffer[p--];
			while (--bytes > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		}

		/// <summary>Read a variable-length, big-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 4)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 4.</exception>
		[Pure]
		public uint ReadUInt32BE(int offset, int bytes)
		{
			if (bytes == 0) return 0;
			if ((uint) bytes > 4) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);

			uint value = buffer[p++];
			while (--bytes > 0)
			{
				value = (value << 8) | buffer[p++];
			}
			return value;
		}

		#endregion

		#region 64 bits...

		/// <summary>Converts a slice into a little-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public long ToInt64()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				4 => BinaryPrimitives.ReadUInt32LittleEndian(span),
				8 => BinaryPrimitives.ReadInt64LittleEndian(span),
				_ => ToInt64Slow(span),
			};

			static long ToInt64Slow(ReadOnlySpan<byte> span)
			{
				int n = span.Length;
				if ((uint) n > 8) goto fail;

				int p = n - 1;
				long value = span[p--];
				while (--n > 0)
				{
					value = (value << 8) | span[p--];
				}
				return value;

			fail:
				throw UnsafeHelpers.Errors.SliceTooLargeForConversion<int>(8);
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToInt64LittleEndian(out long value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16LittleEndian(span); return true;
				case 4: value = BinaryPrimitives.ReadUInt32LittleEndian(span); return true;
				case 8: value = BinaryPrimitives.ReadInt64LittleEndian(span); return true;
				default: return ToInt64Slow(span, out value);
			}

			static bool ToInt64Slow(ReadOnlySpan<byte> span, out long value)
			{
				int n = span.Length;
				if (n > 8)
				{
					value = 0;
					return false;
				}

				int p = n - 1;
				value = span[p--];
				while (--n > 0)
				{
					value = (value << 8) | span[p--];
				}
				return true;
			}
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public long ToInt64BE()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16BigEndian(span),
				4 => BinaryPrimitives.ReadUInt32BigEndian(span),
				8 => BinaryPrimitives.ReadInt64BigEndian(span),
				_ => ToInt64BESlow(span),
			};

			static long ToInt64BESlow(ReadOnlySpan<byte> span)
			{
				Contract.Debug.Requires(span.Length > 2);
				if (span.Length > 8) goto fail;

				long value = 0;
				foreach (var b in span)
				{
					value = (value << 8) | b;
				}
				return value;

			fail:
				throw new FormatException("Cannot convert slice into an Int64 because it is larger than 8 bytes.");
			}
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToInt64BigEndian(out long value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16BigEndian(span); return true;
				case 4: value = BinaryPrimitives.ReadUInt32BigEndian(span); return true;
				case 8: value = BinaryPrimitives.ReadInt64BigEndian(span); return true;
				default: return ToInt64BESlow(span, out value);
			}

			static bool ToInt64BESlow(ReadOnlySpan<byte> span, out long value)
			{
				Contract.Debug.Requires(span.Length > 2);
				if (span.Length > 8)
				{
					value = 0;
					return false;
				}

				value = 0;
				foreach (var b in span)
				{
					value = (value << 8) | b;
				}
				return true;
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public ulong ToUInt64()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				4 => BinaryPrimitives.ReadUInt32LittleEndian(span),
				8 => BinaryPrimitives.ReadUInt64LittleEndian(span),
				_ => ToUInt64Slow(span),
			};

			static ulong ToUInt64Slow(ReadOnlySpan<byte> buffer)
			{
				int n = buffer.Length;
				if (n > 8) goto fail;

				int p = n - 1;
				ulong value = buffer[p--];
				while (--n > 0)
				{
					value = (value << 8) | buffer[p--];
				}
				return value;

			fail:
				throw new FormatException("Cannot convert slice into an UInt64 because it is larger than 8 bytes.");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToUInt64LittleEndian(out ulong value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16LittleEndian(span); return true;
				case 4: value = BinaryPrimitives.ReadUInt32LittleEndian(span); return true;
				case 8: value = BinaryPrimitives.ReadUInt64LittleEndian(span); return true;
				default: return ToUInt64Slow(span, out value);
			}

			static bool ToUInt64Slow(ReadOnlySpan<byte> buffer, out ulong value)
			{
				int n = buffer.Length;
				if (n > 8)
				{
					value = 0;
					return false;
				}

				int p = n - 1;
				value = buffer[p--];
				while (--n > 0)
				{
					value = (value << 8) | buffer[p--];
				}
				return true;
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public ulong ToUInt64BE()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => 0,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16BigEndian(span),
				4 => BinaryPrimitives.ReadUInt32BigEndian(span),
				8 => BinaryPrimitives.ReadUInt64BigEndian(span),
				_ => ToUInt64BESlow(span),
			};

			static ulong ToUInt64BESlow(ReadOnlySpan<byte> buffer)
			{
				Contract.Debug.Requires(buffer.Length > 2);
				if (buffer.Length > 8)
				{
					goto fail;
				}

				ulong value = 0;
				foreach(var b in buffer)
				{
					value = (value << 8) | b;
				}
				return value;

			fail:
				throw new FormatException("Cannot convert slice into an UInt64 because it is larger than 8 bytes.");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToUInt64BigEndian(out ulong value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = 0; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16BigEndian(span); return true;
				case 4: value = BinaryPrimitives.ReadUInt32BigEndian(span); return true;
				case 8: value = BinaryPrimitives.ReadUInt64BigEndian(span); return true;
				default: return ToUInt64BESlow(span, out value);
			}

			static bool ToUInt64BESlow(ReadOnlySpan<byte> buffer, out ulong value)
			{
				Contract.Debug.Requires(buffer.Length > 2);
				if (buffer.Length > 8)
				{
					value = 0;
					return false;
				}

				value = 0;
				foreach(var b in buffer)
				{
					value = (value << 8) | b;
				}
				return true;
			}
		}

		/// <summary>Read a variable-length, little-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 8)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 8.</exception>
		[Pure]
		public ulong ReadUInt64(int offset, int bytes)
		{
			if (bytes == 0) return 0UL;
			if ((uint) bytes > 8) goto fail;

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset) + bytes - 1;

			ulong value = buffer[p--];
			while (--bytes > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		fail:
			throw new ArgumentOutOfRangeException(nameof(bytes));
		}

		/// <summary>Read a variable-length, big-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 8)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 8.</exception>
		[Pure]
		public ulong ReadUInt64BE(int offset, int bytes)
		{
			if (bytes == 0) return 0UL;
			if ((uint) bytes > 8) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);

			ulong value = buffer[p++];
			while (--bytes > 0)
			{
				value = (value << 8) | buffer[p++];
			}
			return value;
		}

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 8-byte array, or an ASCII string of 16, 17 or 19 chars</remarks>
		[Pure]
		public Uuid64 ToUuid64()
		{
			if (this.Count == 0) return default;
			EnsureSliceIsValid();

			switch (this.Count)
			{
				case 8:
				{ // binary (8 bytes)
					return Uuid64.Read(this);
				}

				case 16: // hex8
				case 17: // hex8-hex8
				case 19: // {hex8-hex8}
				{
					//PERF: optimize to remove allocation!
					return Uuid64.Parse(ToByteString()!);
				}
			}

			throw new FormatException("Cannot convert slice into an Uuid64 because it has an incorrect size.");
		}

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 8-byte array, or an ASCII string of 16, 17 or 19 chars</remarks>
		[Pure]
		public bool TryConvertToUuid64(out Uuid64 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0:
				{
					value = default;
					return true;
				}
				case 8:
				{ // binary (8 bytes)
					return Uuid64.TryRead(span, out value);
				}

				case 16: // hex8
				case 17: // hex8-hex8
				case 19: // {hex8-hex8}
				{
					return Uuid64.TryParse(span, out value);
				}
				default:
				{
					value = default;
					return false;
				}
			}
		}

		#endregion

		#region Floating Point...

		/// <summary>Converts a slice into a 32-bit IEEE floating point.</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the slice</exception>
		[Pure]
		public float ToSingle()
		{
			var span = this.Span;
			if (span.Length == 0) return 0f;
			if (span.Length != 4) goto fail;
			return BinaryPrimitives.ReadSingleLittleEndian(span);
		fail:
			throw new FormatException("Cannot convert slice into a Single because it is not exactly 4 bytes long.");
		}

		/// <summary>Converts a slice into a 32-bit IEEE floating point.</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToSingleLittleEndian(out float value)
		{
			var span = this.Span;
			if (span.Length != 4)
			{
				value = 0f;
				return span.Length == 0;
			}
			value = BinaryPrimitives.ReadSingleLittleEndian(span);
			return true;
		}

		/// <summary>Converts a slice into a 32-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the slice</exception>
		[Pure]
		public float ToSingleBE()
		{
			var span = this.Span;
			if (span.Length == 0) return 0f;
			if (span.Length != 4) goto fail;
			return BinaryPrimitives.ReadSingleBigEndian(span);
		fail:
			throw new FormatException("Cannot convert slice into a Single because it is not exactly 4 bytes long.");
		}

		/// <summary>Converts a slice into a 32-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToSingleBigEndian(out float value)
		{
			var span = this.Span;
			if (span.Length != 4)
			{
				value = 0f;
				return span.Length == 0;
			}
			value = BinaryPrimitives.ReadSingleBigEndian(span);
			return true;
		}

		/// <summary>Converts a slice into a 64-bit IEEE floating point.</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public double ToDouble()
		{
			var span = this.Span;
			if (span.Length == 0) return 0f;
			if (span.Length != 8) goto fail;
			return BinaryPrimitives.ReadDoubleLittleEndian(span);
		fail:
			throw new FormatException("Cannot convert slice into a Double because it is not exactly 8 bytes long.");
		}

		/// <summary>Converts a slice into a 64-bit IEEE floating point.</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToDoubleLittleEndian(out double value)
		{
			var span = this.Span;
			if (span.Length != 8)
			{
				value = 0d;
				return span.Length == 0;
			}
			value = BinaryPrimitives.ReadDoubleLittleEndian(span);
			return true;
		}

		/// <summary>Converts a slice into a 64-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public double ToDoubleBE()
		{
			var span = this.Span;
			if (span.Length == 0) return 0f;
			if (span.Length != 8) goto fail;
			return BinaryPrimitives.ReadDoubleBigEndian(span);
		fail:
			throw new FormatException("Cannot convert slice into a Double because it is not exactly 8 bytes long.");
		}

		/// <summary>Converts a slice into a 64-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToDoubleBigEndian(out double value)
		{
			var span = this.Span;
			if (span.Length != 8)
			{
				value = 0d;
				return span.Length == 0;
			}
			value = BinaryPrimitives.ReadDoubleBigEndian(span);
			return true;
		}

#if NET8_0_OR_GREATER

		/// <summary>Converts a slice into a 16-bit IEEE floating point.</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public Half ToHalf()
		{
			var span = this.Span;
			if (span.Length == 0) return default;
			if (span.Length != 2) goto fail;
			return BinaryPrimitives.ReadHalfLittleEndian(span);
		fail:
			throw new FormatException("Cannot convert slice into a Half because it is not exactly 2 bytes long.");
		}

		/// <summary>Converts a slice into a 16-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public Half ToHalfBE()
		{
			var span = this.Span;
			if (span.Length == 0) return default;
			if (span.Length != 2) goto fail;
			return BinaryPrimitives.ReadHalfBigEndian(span);
			fail:
			throw new FormatException("Cannot convert slice into a Half because it is not exactly 2 bytes long.");
		}

#endif

		/// <summary>Converts a slice into a 128-bit IEEE floating point.</summary>
		/// <returns>0 of the slice is null or empty, a floating point number, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the slice</exception>
		[Pure]
		public decimal ToDecimal()
		{
			if (this.Count == 0) return 0m;
			if (this.Count != 16) goto fail;
			EnsureSliceIsValid();

			unsafe
			{
				fixed (byte* ptr = &DangerousGetPinnableReference())
				{
					return *((decimal*)ptr);
				}
			}
		fail:
			throw new FormatException("Cannot convert slice into a Decimal because it is not exactly 16 bytes long.");
		}

		#endregion

		#region 128 bits...

		/// <summary>Converts a slice into a Guid.</summary>
		/// <returns>Native Guid decoded from the Slice.</returns>
		/// <remarks>The slice can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public Guid ToGuid()
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0:
				{
					return Guid.Empty;
				}
				case 16:
				{ // direct byte array

					// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
					// we need to swap the byte order of the Data1, Data2 and Data3 chunks, to ensure that Guid.ToString() will return the proper value.
					return Uuid128.ReadUnsafe(span);
				}
				case 36:
				{ // string representation (ex: "da846709-616d-4e82-bf55-d1d3e9cde9b1")
#if NET10_0_OR_GREATER
					return Guid.Parse(span);
#else
					return Guid.Parse(ToByteString()!);
#endif
				}
				default:
				{
					throw ThrowHelper.FormatException("Cannot convert slice into a Guid because it has an incorrect size.");
				}
			}
		}

		/// <summary>Converts a slice into a Guid.</summary>
		/// <returns>Native Guid decoded from the Slice.</returns>
		/// <remarks>The slice can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public bool TryConvertToGuid(out Guid value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0:
				{
					value = Guid.Empty;
					return true;
				}
				case 16:
				{ // direct byte array

					// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
					// we need to swap the byte order of the Data1, Data2 and Data3 chunks, to ensure that Guid.ToString() will return the proper value.
					value = Uuid128.ReadUnsafe(span);
					return true;
				}
				case 36:
				{ // string representation (ex: "da846709-616d-4e82-bf55-d1d3e9cde9b1")
#if NET10_0_OR_GREATER
					return Guid.TryParse(span, out value);
#else
					return Guid.TryParse(ToByteString()!, out value);
#endif
				}
				default:
				{
					value = Guid.Empty;
					return false;
				}
			}
		}
		/// <summary>Converts this slice into a 128-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public Uuid128 ToUuid128()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => default,
				16 => Uuid128.Read(span),
				36 => Uuid128.Parse(span),
				_ => throw ThrowHelper.FormatException("Cannot convert slice into an Uuid128 because it has an incorrect size.")
			};
		}

		/// <summary>Converts this slice into a 128-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public bool TryConvertToUuid128(out Uuid128 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = default; return true;
				case 16: return Uuid128.TryRead(span, out value);
				case 36: return Uuid128.TryParse(span, out value);
				default: value = default; return false;
			}
		}
#if NET8_0_OR_GREATER // System.Int128 and System.UInt128 are only usable starting from .NET 8.0 (technically 7.0, but we don't support it)

		/// <summary>Converts a slice into a little-endian encoded, signed 128-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public Int128 ToInt128()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => default,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				4 => BinaryPrimitives.ReadUInt32LittleEndian(span),
				8 => BinaryPrimitives.ReadUInt64LittleEndian(span),
				16 => BinaryPrimitives.ReadInt128LittleEndian(span),
				_ => ToInt128Slow(span),
			};

			static Int128 ToInt128Slow(ReadOnlySpan<byte> buffer)
			{
				int n = buffer.Length;
				if (n > 16) goto fail;

				int p = n - 1;
				Int128 value = buffer[p--];
				while (--n > 0)
				{
					value = (value << 8) | buffer[p--];
				}
				return value;

			fail:
				throw new FormatException("Cannot convert slice into an Int128 because it is larger than 16 bytes.");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, signed 128-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToInt128(out Int128 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = default; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16LittleEndian(span); return true;
				case 4: value = BinaryPrimitives.ReadUInt32LittleEndian(span); return true;
				case 8: value = BinaryPrimitives.ReadUInt64LittleEndian(span); return true;
				case 16: value = BinaryPrimitives.ReadInt128LittleEndian(span); return true;
				default: return ToInt128Slow(span, out value);
			}

			static bool ToInt128Slow(ReadOnlySpan<byte> buffer, out Int128 value)
			{
				int n = buffer.Length;
				if (n > 16)
				{
					value = default;
					return false;
				}

				int p = n - 1;
				value = buffer[p--];
				while (--n > 0)
				{
					value = (value << 8) | buffer[p--];
				}
				return true;
			}
		}

		/// <summary>Converts a slice into a big-endian encoded, signed 128-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public Int128 ToInt128BE()
		{
			var span = ValidateSpan();
			return span.Length switch
			{
				0 => default,
				1 => this[0],
				2 => BinaryPrimitives.ReadUInt16BigEndian(span),
				4 => BinaryPrimitives.ReadUInt32BigEndian(span),
				8 => BinaryPrimitives.ReadUInt64BigEndian(span),
				16 => BinaryPrimitives.ReadInt128BigEndian(span),
				_ => ToInt128BESlow(span),
			};

			static Int128 ToInt128BESlow(ReadOnlySpan<byte> buffer)
			{
				if (buffer.Length > 16) goto fail;

				Int128 value = default;
				foreach(var b in buffer)
				{
					value = (value << 8) | b;
				}
				return value;

			fail:
				throw new FormatException("Cannot convert slice into an Int128 because it is larger than 16 bytes.");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 128-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 16 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 16 bytes in the slice</exception>
		[Pure]
		public UInt128 ToUInt128()
		{
			var span = this.Span;
			return span.Length switch
			{
				0 => default,
				1 => span[0],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
				4 => BinaryPrimitives.ReadUInt32LittleEndian(span),
				8 => BinaryPrimitives.ReadUInt64LittleEndian(span),
				16 => BinaryPrimitives.ReadUInt128LittleEndian(span),
				_ => ToUInt128Slow(span),
			};

			static UInt128 ToUInt128Slow(ReadOnlySpan<byte> buffer)
			{
				int n = buffer.Length;
				if (n > 16) goto fail;

				int p = n - 1;
				UInt128 value = buffer[p--];
				while (--n > 0)
				{
					value = (value << 8) | buffer[p--];
				}
				return value;

			fail:
				throw new FormatException("Cannot convert slice into an UInt128 because it is larger than 16 bytes.");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 128-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 16 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 16 bytes in the slice</exception>
		[Pure]
		public bool TryConvertToUInt128LittleEndian(out UInt128 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0: value = default; return true;
				case 1: value = span[0]; return true;
				case 2: value = BinaryPrimitives.ReadUInt16LittleEndian(span); return true;
				case 4: value = BinaryPrimitives.ReadUInt32LittleEndian(span); return true;
				case 8: value = BinaryPrimitives.ReadUInt64LittleEndian(span); return true;
				case 16: value = BinaryPrimitives.ReadUInt128LittleEndian(span); return true;
				default: return ToUInt128Slow(span, out value);
			}

			static bool ToUInt128Slow(ReadOnlySpan<byte> buffer, out UInt128 value)
			{
				int n = buffer.Length;
				if (n > 16)
				{
					value = default;
					return false;
				}

				int p = n - 1;
				value = buffer[p--];
				while (--n > 0)
				{
					value = (value << 8) | buffer[p--];
				}
				return true;
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 128-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 16 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 16 bytes in the slice</exception>
		[Pure]
		public UInt128 ToUInt128BE()
		{
			int n = this.Count;
			if (n == 0) return 0L;
			if ((uint) n > 16) goto fail;
			EnsureSliceIsValid();

			var buffer = this.Array;
			int p = this.Offset;

			UInt128 value = buffer[p++];
			while (--n > 0)
			{
				value = (value << 8) | buffer[p++];
			}
			return value;
		fail:
			throw new FormatException("Cannot convert slice into an UInt128 because it is larger than 16 bytes.");
		}

		/// <summary>Read a variable-length, little-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 16)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 16.</exception>
		[Pure]
		public UInt128 ReadUInt128(int offset, int bytes)
		{
			if (bytes == 0) return 0UL;
			if ((uint) bytes > 16) goto fail;

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset) + bytes - 1;

			UInt128 value = buffer[p--];
			while (--bytes > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		fail:
			throw new ArgumentOutOfRangeException(nameof(bytes));
		}

		/// <summary>Read a variable-length, big-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 16)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 16.</exception>
		[Pure]
		public UInt128 ReadUInt128BE(int offset, int bytes)
		{
			if (bytes == 0) return 0UL;
			if ((uint) bytes > 8) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);

			UInt128 value = buffer[p++];
			while (--bytes > 0)
			{
				value = (value << 8) | buffer[p++];
			}
			return value;
		}

#endif

		#endregion

		#region 48 bits...

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 10-byte array, or an ASCII string of 20, 22 or 24 chars</remarks>
		[Pure]
		public Uuid48 ToUuid48()
		{
			if (this.Count == 0) return default;
			EnsureSliceIsValid();

			switch (this.Count)
			{
				case 6:
				{ // binary (6 bytes)
					return Uuid48.Read(this);
				}

				case 12: // XXXXXXXXXXXX
				case 13: // XXXX-XXXXXXXX
				case 15: // {XXXX-XXXXXXXX}
				{
					//PERF: optimize to remove allocation!
					return Uuid48.Parse(ToByteString()!); 
				}
			}

			throw new FormatException("Cannot convert slice into an Uuid80 because it has an incorrect size.");
		}

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 10-byte array, or an ASCII string of 20, 22 or 24 chars</remarks>
		[Pure]
		public bool TryConvertToUuid48(out Uuid48 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0:
				{
					value = default;
					return true;
				}
				case 6:
				{ // binary (6 bytes)
					return Uuid48.TryRead(span, out value);
				}

				case 12: // XXXXXXXXXXXX
				case 13: // XXXX-XXXXXXXX
				case 15: // {XXXX-XXXXXXXX}
				{
					return Uuid48.TryParse(span, out value); 
				}
				default:
				{
					value = default;
					return true;
				}
			}
		}

		#endregion

		#region 80 bits...

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 10-byte array, or an ASCII string of 20, 22 or 24 chars</remarks>
		[Pure]
		public Uuid80 ToUuid80()
		{
			if (this.Count == 0) return default;
			EnsureSliceIsValid();

			switch (this.Count)
			{
				case 10:
				{ // binary (10 bytes)
					return Uuid80.Read(this);
				}

				case 20: // XXXXXXXXXXXXXXXXXXXX
				case 22: // XXXX-XXXXXXXX-XXXXXXXX
				case 24: // {XXXX-XXXXXXXX-XXXXXXXX}
				{
					//PERF: optimize to remove allocation!
					return Uuid80.Parse(ToByteString()!);
				}
			}

			throw new FormatException("Cannot convert slice into an Uuid80 because it has an incorrect size.");
		}

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 10-byte array, or an ASCII string of 20, 22 or 24 chars</remarks>
		[Pure]
		public bool TryConvertToUuid80(out Uuid80 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0:
				{
					value = default;
					return true;
				}
				case 10:
				{ // binary (10 bytes)
					return Uuid80.TryRead(span, out value);
				}

				case 20: // XXXXXXXXXXXXXXXXXXXX
				case 22: // XXXX-XXXXXXXX-XXXXXXXX
				case 24: // {XXXX-XXXXXXXX-XXXXXXXX}
				{
					return Uuid80.TryParse(span, out value);
				}
				default:
				{
					value = default;
					return false;
				}
			}
		}

		#endregion

		#region 96 bits...

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 12-byte array, or an ASCII string of 24, 26 or 28 chars</remarks>
		[Pure]
		public Uuid96 ToUuid96()
		{
			if (this.Count == 0) return default;
			EnsureSliceIsValid();

			switch (this.Count)
			{
				case 12:
				{ // binary (12 bytes)
					return Uuid96.Read(this);
				}

				case 24: // XXXXXXXXXXXXXXXXXXXXXXXX
				case 26: // XXXXXXXX-XXXXXXXX-XXXXXXXX
				case 28: // {XXXXXXXX-XXXXXXXX-XXXXXXXX}
				{
					//PERF: optimize to remove allocation!
					return Uuid96.Parse(ToByteString()!);
				}
			}

			throw new FormatException("Cannot convert slice into an Uuid96 because it has an incorrect size.");
		}

		/// <summary>Converts this slice into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be an 12-byte array, or an ASCII string of 24, 26 or 28 chars</remarks>
		[Pure]
		public bool TryConvertToUuid96(out Uuid96 value)
		{
			var span = this.Span;
			switch (span.Length)
			{
				case 0:
				{
					value = default;
					return true;
				}
				case 12:
				{ // binary (12 bytes)
					return Uuid96.TryRead(span, out value);
				}

				case 24: // XXXXXXXXXXXXXXXXXXXXXXXX
				case 26: // XXXXXXXX-XXXXXXXX-XXXXXXXX
				case 28: // {XXXXXXXX-XXXXXXXX-XXXXXXXX}
				{
					return Uuid96.TryParse(span, out value);
				}
				default:
				{
					value = default;
					return false;
				}
			}
		}

		#endregion

		#endregion

	}

}
