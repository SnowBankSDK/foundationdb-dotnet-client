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

namespace SnowBank.Data.Tuples.Binary
{
	/// <summary>
	/// Constants for the various tuple value types
	/// </summary>
	[PublicAPI]
	public static class TupleTypes
	{
		/// <summary>Null/Empty/Void</summary>
		public const byte Nil = 0;

		/// <summary>ASCII String (<c>01 ... 00</c>)</summary>
		public const byte Bytes = 0x1;

		/// <summary>UTF-8 String (<c>02 ... 00</c>)</summary>
		public const byte Utf8 = 0x2;

		/// <summary>Nested tuple start [OBSOLETE]</summary>
		/// <remarks>Deprecated and should not be used anymore</remarks>
		public const byte LegacyTupleStart = 0x3;

		/// <summary>Nested tuple end [OBSOLETE]</summary>
		/// <remarks>Deprecated and should not be used anymore</remarks>
		public const byte LegacyTupleEnd = 0x4;

		/// <summary>Nested tuple (<c>05 ... 00</c>)</summary>
		public const byte EmbeddedTuple = 0x5;

		/// <summary>Negative arbitrary-precision integer (8-bit count, 9 to 255 bytes)</summary>
		/// <remarks><c>0B ## xx xx xx ...</c></remarks>
		public const byte NegativeBigInteger = 0x0B;

		/// <summary>Negative Integer (8 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>0C xx xx xx xx xx xx xx xx</c></remarks>
		public const byte IntNeg8 = 0x0C;

		/// <summary>Negative Integer (7 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>0E xx xx xx xx xx xx xx</c></remarks>
		public const byte IntNeg7 = 0x0D;

		/// <summary>Negative Integer (6 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>0E xx xx xx xx xx xx</c></remarks>
		public const byte IntNeg6 = 0x0E;

		/// <summary>Negative Integer (5 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>0F xx xx xx xx xx</c></remarks>
		public const byte IntNeg5 = 0x0F;

		/// <summary>Negative Integer (4 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>10 xx xx xx xx</c></remarks>
		public const byte IntNeg4 = 0x10;

		/// <summary>Negative Integer (3 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>11 xx xx xx</c></remarks>
		public const byte IntNeg3 = 0x11;

		/// <summary>Negative Integer (2 bytes, Big-Endian, One's Complement)</summary>
		/// <remarks><c>12 xx xx</c></remarks>
		public const byte IntNeg2 = 0x12;

		/// <summary>Negative Integer (1 byte, Big-Endian, One's Complement)</summary>
		/// <remarks><c>13 xx</c></remarks>
		public const byte IntNeg1 = 0x13;

		/// <summary>Zero</summary>
		public const byte IntZero = 0x14;

		/// <summary>Positive Integer (1 byte, Big-Endian)</summary>
		/// <remarks><c>15 xx</c></remarks>
		public const byte IntPos1 = 0x15;

		/// <summary>Positive Integer (2 bytes, Big-Endian)</summary>
		/// <remarks><c>16 xx xx</c></remarks>
		public const byte IntPos2 = 0x16;

		/// <summary>Positive Integer (3 bytes, Big-Endian)</summary>
		/// <remarks><c>17 xx xx xx</c></remarks>
		public const byte IntPos3 = 0x17;

		/// <summary>Positive Integer (4 bytes, Big-Endian)</summary>
		/// <remarks><c>18 xx xx xx xx</c></remarks>
		public const byte IntPos4 = 0x18;

		/// <summary>Positive Integer (5 bytes, Big-Endian)</summary>
		/// <remarks><c>19 xx xx xx xx xx</c></remarks>
		public const byte IntPos5 = 0x19;

		/// <summary>Positive Integer (6 bytes, Big-Endian)</summary>
		/// <remarks><c>1A xx xx xx xx xx xx</c></remarks>
		public const byte IntPos6 = 0x1A;

		/// <summary>Positive Integer (7 bytes, Big-Endian)</summary>
		/// <remarks><c>1B xx xx xx xx xx xx xx</c></remarks>
		public const byte IntPos7 = 0x1B;

		/// <summary>Positive Integer (8 bytes, Big-Endian)</summary>
		/// <remarks><c>1C xx xx xx xx xx xx xx xx</c></remarks>
		public const byte IntPos8 = 0x1C;

		/// <summary>Positive arbitrary-precision integer (8-bit count, 9 to 255 bytes)</summary>
		/// <remarks><c>1D ## xx xx xx ...</c></remarks>
		public const byte PositiveBigInteger = 0x1D;

		// note: 0x1E is reserved for big integers (with 16-bit count?) but not used anywhere

		// note: 0x1F is currently unused

		/// <summary>Single precision decimals (32-bit, Big-Endian)</summary>
		/// <remarks><c>20 xx xx xx xx</c></remarks>
		public const byte Single = 0x20;

		/// <summary>Double precision decimals (64-bit, Big-Endian)</summary>
		/// <remarks><c>21 xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Double = 0x21;

		/// <summary>Triple precision decimals (80-bit, Big-Endian)</summary>
		/// <remarks><c>22 xx xx xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Triple = 0x22; //note: javascript numbers

		/// <summary>Quadruple precision decimals (128-bit, Big-Endian)</summary>
		/// <remarks><c>23 xx xx xx xx xx xx xx xx xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Quadruple = 0x23;

		// note: 0x24 is currently unused

		/// <summary>True Value [OBSOLETE]</summary>
		/// <remarks>Deprecated and should not be used anymore</remarks>
		public const byte LegacyTrue = 0x25;

		/// <summary>False Value</summary>
		/// <remarks><c>26</c></remarks>
		public const byte False = 0x26;

		/// <summary>True Value</summary>
		/// <remarks><c>27</c></remarks>
		public const byte True = 0x27;

		/// <summary>RFC4122 UUID (128 bits)</summary>
		/// <remarks><c>30 xx xx xx xx xx xx xx xx xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Uuid128 = 0x30;

		/// <summary>UUID (64 bits)</summary>
		/// <remarks><c>31 xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Uuid64 = 0x31;

		/// <summary>UUID (80 bits) / VersionStamp</summary>
		/// <remarks><c>32 xx xx xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Uuid80 = 0x32;

		/// <summary>UUID (96 bits) / VersionStamp</summary>
		/// <remarks><c>33 xx xx xx xx xx xx xx xx xx xx xx xx</c></remarks>
		public const byte Uuid96 = 0x33;

		/// <summary>Length-Prefixed Byte String (up to 255 bytes)</summary>
		/// <remarks>
		/// <para>An 8-bit integer representing the length, followed by the given number of bytes</para>
		/// <para>Well suited for storing hashes, or similar binary blobs that may contain a lot of 0x00 bytes, without having to escape them.</para>
		/// <para>This is an extension of the original Tuple specification, and may not be supported by all bindings.</para>
		/// </remarks>
		public const byte VarBytes1 = 0x34;

		/// <summary>Length-Prefixed Byte String (from 256 to 65535 bytes)</summary>
		/// <remarks>
		/// <para>A 16-bit big-endian integer representing the length, followed by the given number of bytes</para>
		/// <para>Well suited for storing hashes, or similar binary blobs that may contain a lot of 0x00 bytes, without having to escape them.</para>
		/// <para>Lengths of 255 or less should use <see cref="VarBytes1"/> instead.</para>
		/// <para>This is an extension of the original Tuple specification, and may not be supported by all bindings.</para>
		/// </remarks>
		public const byte VarBytes2 = 0x35;

		/// <summary>Reserved Type 0 (application specific)</summary>
		/// <remarks><c>40 ?? ...</c></remarks>
		public const byte UserType0 = 0x40;

		/// <summary>Reserved Type 1 (application specific)</summary>
		/// <remarks><c>41 ?? ...</c></remarks>
		public const byte UserType1 = 0x41;

		/// <summary>Reserved Type 2 (application specific)</summary>
		/// <remarks><c>42 ?? ...</c></remarks>
		public const byte UserType2 = 0x42;

		/// <summary>Reserved Type 3 (application specific)</summary>
		/// <remarks><c>43 ?? ...</c></remarks>
		public const byte UserType3 = 0x43;

		/// <summary>Reserved Type 4 (application specific)</summary>
		/// <remarks><c>44 ?? ...</c></remarks>
		public const byte UserType4 = 0x44;

		/// <summary>Reserved Type 5 (application specific)</summary>
		/// <remarks><c>45 ?? ...</c></remarks>
		public const byte UserType5 = 0x45;

		/// <summary>Reserved Type 6 (application specific)</summary>
		/// <remarks><c>46 ?? ...</c></remarks>
		public const byte UserType6 = 0x46;

		/// <summary>Reserved Type 7 (application specific)</summary>
		/// <remarks><c>47 ?? ...</c></remarks>
		public const byte UserType7 = 0x47;

		/// <summary>Reserved Type 8 (application specific)</summary>
		/// <remarks><c>48 ?? ...</c></remarks>
		public const byte UserType8 = 0x48;

		/// <summary>Reserved Type 9 (application specific)</summary>
		/// <remarks><c>49 ?? ...</c></remarks>
		public const byte UserType9 = 0x49;

		/// <summary>Reserved Type 10 (application specific)</summary>
		/// <remarks><c>4A ?? ...</c></remarks>
		public const byte UserTypeA = 0x4A;

		/// <summary>Reserved Type 11 (application specific)</summary>
		/// <remarks><c>4B ?? ...</c></remarks>
		public const byte UserTypeB = 0x4B;

		/// <summary>Reserved Type 12 (application specific)</summary>
		/// <remarks><c>4C ?? ...</c></remarks>
		public const byte UserTypeC = 0x4C;

		/// <summary>Reserved Type 13 (application specific)</summary>
		/// <remarks><c>4D ?? ...</c></remarks>
		public const byte UserTypeD = 0x4D;

		/// <summary>Reserved Type 14 (application specific)</summary>
		/// <remarks><c>4E ?? ...</c></remarks>
		public const byte UserTypeE = 0x4E;

		/// <summary>Reserved Type 15 (application specific)</summary>
		/// <remarks><c>4F ?? ...</c></remarks>
		public const byte UserTypeF = 0x4F;

		/// <summary>Marks the end of the tuple encoding (extension)</summary>
		/// <remarks>
		/// <para>This type indicates that the following bytes are not part of the tuple and are handled using a custom key encoding.</para>
		/// <para>This is an extension of the original Tuple specification, and may not be supported by all bindings.</para>
		/// </remarks>
		public const byte EndOfTuple = 0xF0;

		/// <summary>Standard prefix of the Directory Layer</summary>
		/// <remarks>This is not a part of the tuple encoding itself, but helps the tuple decoder pretty-print tuples that would otherwise be unparsable.</remarks>
		public const byte Directory = 0xFE;

		/// <summary>Standard prefix of the System keys, or frequent suffix with key ranges</summary>
		public const byte Escape = 0xFF;

		/// <summary>Returns the type of tuple segment, from its header</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TupleSegmentType DecodeSegmentType(Slice segment) => DecodeSegmentType(segment.Span);

		/// <summary>Return the type of tuple segment, from its header</summary>
		[Pure]
		public static TupleSegmentType DecodeSegmentType(ReadOnlySpan<byte> segment)
		{
			if (segment.Length == 0) return TupleSegmentType.Nil;

			return segment[0] switch
			{
				Nil => TupleSegmentType.Nil,
				Bytes => TupleSegmentType.ByteString,
				Utf8 => TupleSegmentType.UnicodeString,
				EmbeddedTuple => TupleSegmentType.Tuple,
				NegativeBigInteger => TupleSegmentType.BigInteger,
				>= IntNeg8 and <= IntPos8 => TupleSegmentType.Integer,
				PositiveBigInteger => TupleSegmentType.BigInteger,
				Single => TupleSegmentType.Single,
				Double => TupleSegmentType.Double,
				Triple => TupleSegmentType.Triple,
				Quadruple => TupleSegmentType.Decimal,
				False or True  => TupleSegmentType.Boolean,
				Uuid128 => TupleSegmentType.Uuid128,
				Uuid64 => TupleSegmentType.Uuid64,
				Uuid80 => TupleSegmentType.VersionStamp80,
				Uuid96 => TupleSegmentType.VersionStamp96,
				>= UserType0 and <= UserTypeF => TupleSegmentType.UserType,
				_ => TupleSegmentType.Invalid,
			};
		}

	}

	/// <summary>Logical type of packed element of a tuple</summary>
	public enum TupleSegmentType
	{
		Invalid = -1,
		Nil = 0,
		ByteString = 0x01,
		UnicodeString = 0x02,
		Tuple = 0x03,
		Integer = 0x14, // centered on 0, but all from 0x0C to 0x1C
		BigInteger = 0x1D,
		Single = 0x20,
		Double = 0x21,
		Triple = 0x22,
		Decimal = 0x23,
		Boolean = 0x26, // either False or True
		Uuid128 = 0x30,
		Uuid64 = 0x31,
		VersionStamp80 = 0x32,
		VersionStamp96 = 0x33,
		UserType = 0x40, // 0x40 - 0x4F
	}

}
