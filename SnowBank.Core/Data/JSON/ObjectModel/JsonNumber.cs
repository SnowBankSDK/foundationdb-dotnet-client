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

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable RedundantNameQualifier

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.ComponentModel;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using NodaTime;
	using SnowBank.Buffers;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>JSON number</summary>
	[DebuggerDisplay("JSON Number({" + nameof(m_literal) + ",nq})")]
	[PublicAPI]
#if !NETSTANDARD2_0
	// System.Text.Json interop is disabled on the netstandard2.0 build
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
#endif
	[DebuggerNonUserCode]
	public sealed class JsonNumber : JsonValue, IEquatable<JsonNumber>, IComparable<JsonNumber>
		, IEquatable<JsonString>
		, IEquatable<JsonBoolean>
		, IEquatable<JsonDateTime>
		, IEquatable<int>
		, IComparable<int>
		, IEquatable<long>
		, IComparable<long>
		, IEquatable<uint>
		, IComparable<uint>
		, IEquatable<ulong>
		, IComparable<ulong>
		, IEquatable<float>
		, IComparable<float>
		, IEquatable<double>
		, IComparable<double>
		, IEquatable<decimal>
		, IEquatable<short>
		, IEquatable<ushort>
		, IEquatable<TimeSpan>
#if NET5_0_OR_GREATER
		// System.Half does not exist on netstandard2.0
		, IEquatable<Half>
#endif
		, IEquatable<Uuid48>
#if NET8_0_OR_GREATER
		, IEquatable<Int128>
		, IEquatable<UInt128>
		, System.Numerics.INumberBase<JsonNumber>
		, System.Numerics.IComparisonOperators<JsonNumber, long, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, ulong, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, float, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, double, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, decimal, bool>
		, System.Numerics.IAdditionOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, decimal, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, decimal, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, decimal, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, decimal, JsonNumber>
#endif
	{
		/// <summary>Cache of all small numbers, from <see cref="CACHED_SIGNED_MIN"/> to <see cref="CACHED_SIGNED_MAX"/> (included)</summary>
		private static readonly JsonNumber[] SmallNumbers = PreGenSmallNumbers();
		//NOTE: SmallNumbers must be initialized before ALL the other static fields that rely on it!

		internal const int CACHED_SIGNED_MIN = -128;
		internal const int CACHED_SIGNED_MAX = 999; //note: must be at least 255 because all bytes must be in the cache
		private const uint CACHED_OFFSET_ZERO = -CACHED_SIGNED_MIN;

		private static JsonNumber[] PreGenSmallNumbers()
		{
			var cache = new JsonNumber[CACHED_SIGNED_MAX - CACHED_SIGNED_MIN + 1];
			for (int i = 0; i < cache.Length; i++)
			{
				int x = i + CACHED_SIGNED_MIN;
				cache[i] = new JsonNumber(new Number(x), Kind.Signed, StringConverters.ToString(x));
			}
			return cache;
		}

		/// <summary><see langword="0"/> (signed int)</summary>

		public static JsonNumber Zero { get; } = SmallNumbers[0 + CACHED_OFFSET_ZERO];

		/// <summary><see langword="1"/> (signed int)</summary>
		public static JsonNumber One { get; } = SmallNumbers[1 + CACHED_OFFSET_ZERO];

		/// <summary><see langword="-1"/> (signed int)</summary>
		public static JsonNumber MinusOne { get; } = SmallNumbers[-1 + CACHED_OFFSET_ZERO];

		/// <summary><see langword="0.0"/> (double)</summary>
		public static readonly JsonNumber DecimalZero = new(new Number(0d), Kind.Double, "0"); //REVIEW: "0.0" ?

		/// <summary><see langword="1.0"/> (double)</summary>
		public static readonly JsonNumber DecimalOne = new(new Number(1d), Kind.Double, "1"); //REVIEW: "1.0" ?

		/// <summary><see langword="NaN"/> (double)</summary>
		public static readonly JsonNumber NaN = new(new Number(double.NaN), Kind.Double, "NaN");

		/// <summary><see langword="+∞"/> (double)</summary>
		public static readonly JsonNumber PositiveInfinity = new(new Number(double.PositiveInfinity), Kind.Double, "Infinity");

		/// <summary><see langword="-∞"/> (double)</summary>
		public static readonly JsonNumber NegativeInfinity = new(new Number(double.NegativeInfinity), Kind.Double, "-Infinity");

		/// <summary><see langword="π"/> ~= <see langword="3.1415926535897931"/> (double)</summary>
		// ReSharper disable once InconsistentNaming
		public static readonly JsonNumber PI = new(new Number(Math.PI), Kind.Double, "3.1415926535897931");

		/// <summary><see langword="τ"/> ~= <see langword="6.283185307179586"/> (double)</summary>
#if NET5_0_OR_GREATER
		public static readonly JsonNumber Tau = new(new Number(Math.Tau), Kind.Double, "6.283185307179586");
#else
		// Math.Tau is not on netstandard2.0
		public static readonly JsonNumber Tau = new(new Number(6.283185307179586), Kind.Double, "6.283185307179586");
#endif

		#region Nested Types...

		private enum Kind
		{
			/// <summary>Integer that can be cast to Int64 (long.MinValue &lt;= x &lt;= long.MaxValue)</summary>
			Signed = 0,
			/// <summary>Positive integer that requires an UInt64 (x > long.MaxValue)</summary>
			Unsigned,
			/// <summary>128-bits decimal floating point number</summary>
			Decimal,
			/// <summary>64-bits IEEE floating point number</summary>
			Double,
		}

		[DebuggerDisplay("Signed={Signed}, Unsigned={Unsigned}, Double={Double}, Decimal={Decimal}")]
		[StructLayout(LayoutKind.Explicit)]
		private readonly struct Number
		{
			//REVIEW: Because we need to support Decimal, the struct takes 16 octets, instead of 8.

			#region Fields...

			[FieldOffset(0)]
			public readonly decimal Decimal;

			[FieldOffset(0)]
			public readonly double Double;

			[FieldOffset(0)]
			public readonly long Signed;

			[FieldOffset(0)]
			public readonly ulong Unsigned;

#if NET8_0_OR_GREATER

			[FieldOffset(0)]
			public readonly Int128 Signed128;

			[FieldOffset(0)]
			public readonly UInt128 Unsigned128;

#endif

			//TODO: another view for Int128/UInt28?

			#endregion

			#region Constructors...

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(decimal value) : this()
			{
				this.Decimal = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(double value) : this()
			{
				this.Double = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(int value) : this()
			{
				this.Signed = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(uint value) : this()
			{
				this.Unsigned = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(long value) : this()
			{
				this.Signed = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(ulong value) : this()
			{
				this.Unsigned = value;
			}

#if NET8_0_OR_GREATER

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(Int128 value)
			{
				this.Signed128 = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(UInt128 value)
			{
				this.Unsigned128 = value;
			}

#endif

			#endregion

			[Pure]
			public bool IsZero(Kind kind) => kind switch
			{
				Kind.Signed => this.Signed == 0L,
				Kind.Unsigned => this.Unsigned == 0UL,
				Kind.Decimal => this.Decimal == 0m,
				Kind.Double => this.Double == 0d,
				_ => false,
			};

			[Pure]
			public bool IsOne(Kind kind) => kind switch
			{
				Kind.Signed => this.Signed == 1L,
				Kind.Unsigned => this.Unsigned == 1UL,
				Kind.Decimal => this.Decimal == 1m,
				Kind.Double => this.Double == 1d,
				_ => false,
			};

			[Pure]
			public bool IsNegative(Kind kind) => kind switch
			{
				Kind.Signed => this.Signed < 0L,
				Kind.Unsigned => false,
				Kind.Decimal => this.Decimal < 0m,
				Kind.Double => this.Double < 0d,
				_ => false
			};

			public static bool Equals(in Number x, Kind xKind, in Number y, Kind yKind)
			{
				if (xKind == yKind)
				{ // same kind, direct comparison
					return xKind switch
					{
						Kind.Decimal => x.Decimal == y.Decimal,
						_ => x.Unsigned == y.Unsigned
					};
				}
				// need to adapt one or the other to the "largest" type
				if (xKind == Kind.Decimal) return x.Decimal == y.ToDecimal(yKind);
				if (yKind == Kind.Decimal) return y.Decimal == x.ToDecimal(xKind);
				if (xKind == Kind.Double) return x.Double == y.ToDouble(yKind);
				if (yKind == Kind.Double) return y.Double == x.ToDouble(xKind);
				if (xKind == Kind.Signed) return (x.Signed == y.Signed) & (y.Signed >= 0); // x signed, y unsigned
				return (x.Signed == y.Signed) & (x.Signed >= 0); // x unsigned, y signed
			}

			public static int CompareTo(in Number x, Kind xKind, in Number y, Kind yKind) => yKind switch
			{
				Kind.Decimal => x.CompareTo(xKind, y.ToDecimal(yKind)),
				Kind.Double => x.CompareTo(xKind, y.ToDouble(yKind)),
				Kind.Signed => x.CompareTo(xKind, y.ToInt64(yKind)),
				_ => x.CompareTo(xKind, y.ToUInt64(yKind))
			};

			/// <summary>Compare this JSON Number with a signed integer</summary>
			/// <returns><see langword="+1"/> if we are bigger than <paramref name="value"/>, <see langword="-1"/> if we are smaller than <paramref name="value"/>, or <see langword="0"/> if we are equal to  <paramref name="value"/></returns>
			/// <remarks>Note that <see cref="JsonNumber.NaN"/> will always return <see langword="-1"/> when compared to an integer</remarks>
			[Pure]
			public int CompareTo(Kind kind, long value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo(value),
				Kind.Double => this.Double.CompareTo(value),
				Kind.Signed => this.Signed.CompareTo(value),
				_ => value < 0 ? +1 : this.Unsigned.CompareTo((ulong) value)
			};

			/// <summary>Compare this JSON Number with an unsigned integer</summary>
			/// <returns><see langword="+1"/> if we are bigger than <paramref name="value"/>, <see langword="-1"/> if we are smaller than <paramref name="value"/>, or <see langword="0"/> if we are equal to  <paramref name="value"/></returns>
			/// <remarks>Note that <see cref="JsonNumber.NaN"/> will always return <see langword="-1"/> when compared to an integer</remarks>
			[Pure]
			public int CompareTo(Kind kind, ulong value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo(value),
				Kind.Double => this.Double.CompareTo(value),
				Kind.Signed => this.Signed < 0 ? -1 : ((ulong) this.Signed).CompareTo(value),
				_ => this.Unsigned.CompareTo(value)
			};

			/// <summary>Compare this JSON Number with a decimal number</summary>
			/// <returns><see langword="+1"/> if we are bigger than <paramref name="value"/>, <see langword="-1"/> if we are smaller than <paramref name="value"/>, or <see langword="0"/> if we are equal to  <paramref name="value"/></returns>
			/// <remarks>Note that <see cref="double.NaN"/> will return 0 (equal) if compared to itself, but <see langword="-1"/> (smaller) in all other cases</remarks>
			[Pure]
			public int CompareTo(Kind kind, double value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo((decimal) value),
				Kind.Double => this.Double.CompareTo(value),
				Kind.Signed => ((double) this.Signed).CompareTo(value),
				_ => ((double) this.Unsigned).CompareTo(value)
			};

			/// <summary>Compare this JSON Number with a decimal integer</summary>
			/// <returns><see langword="+1"/> if we are bigger than <paramref name="value"/>, <see langword="-1"/> if we are smaller than <paramref name="value"/>, or <see langword="0"/> if we are equal to  <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, decimal value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo(value),
				Kind.Double => new decimal(this.Double).CompareTo(value),
				Kind.Signed => new decimal((double) this.Signed).CompareTo(value),
				_ => new decimal((double) this.Unsigned).CompareTo(value)
			};

			#region Conversion...

			[Pure]
			public object? ToObject(Kind kind) => kind switch
			{
				Kind.Decimal => this.Decimal,
				Kind.Double => this.Double,
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => null
			};

			[Pure]
			public bool ToBoolean(Kind kind) => kind switch
			{
				Kind.Decimal => this.Decimal != 0,
				Kind.Double => this.Double != 0,
				Kind.Signed => this.Signed != 0,
				Kind.Unsigned => this.Unsigned != 0,
				_ => false
			};

			[Pure]
			public byte ToByte(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToByte(this.Decimal),
				Kind.Double => checked((byte) this.Double),
				Kind.Signed => checked((byte) this.Signed),
				Kind.Unsigned => checked((byte) this.Unsigned),
				_ => 0
			};

			[Pure]
			public sbyte ToSByte(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToSByte(this.Decimal),
				Kind.Double => checked((sbyte) this.Double),
				Kind.Signed => checked((sbyte) this.Signed),
				Kind.Unsigned => checked((sbyte) this.Unsigned),
				_ => 0
			};

			[Pure]
			public short ToInt16(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToInt16(this.Decimal),
				Kind.Double => checked((short) this.Double),
				Kind.Signed => checked((short) this.Signed),
				Kind.Unsigned => checked((short) this.Unsigned),
				_ => 0
			};

			[Pure]
			public ushort ToUInt16(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToUInt16(this.Decimal),
				Kind.Double => checked((ushort) this.Double),
				Kind.Signed => checked((ushort) this.Signed),
				Kind.Unsigned => checked((ushort) this.Unsigned),
				_ => 0
			};

			[Pure]
			public int ToInt32(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToInt32(this.Decimal),
				Kind.Double => checked((int) this.Double),
				Kind.Signed => checked((int) this.Signed),
				Kind.Unsigned => checked((int) this.Unsigned),
				_ => 0
			};

			[Pure]
			public uint ToUInt32(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToUInt32(this.Decimal),
				Kind.Double => checked((uint) this.Double),
				Kind.Signed => checked((uint) this.Signed),
				Kind.Unsigned => checked((uint) this.Unsigned),
				_ => 0
			};

			[Pure]
			public long ToInt64(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToInt64(this.Decimal),
				Kind.Double => checked((long) this.Double),
				Kind.Signed => this.Signed,
				Kind.Unsigned => checked((long) this.Unsigned),
				_ => 0
			};

			[Pure]
			public ulong ToUInt64(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToUInt64(this.Decimal),
				Kind.Double => checked((ulong) this.Double),
				Kind.Signed => checked((ulong) this.Signed),
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

#if NET8_0_OR_GREATER

			[Pure]
			public Int128 ToInt128(Kind kind) => kind switch
			{
				Kind.Decimal => (Int128)this.Decimal,
				Kind.Double => checked((Int128) this.Double),
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

			[Pure]
			public UInt128 ToUInt128(Kind kind) => kind switch
			{
				Kind.Decimal => (UInt128) this.Decimal,
				Kind.Double => checked((UInt128) this.Double),
				Kind.Signed => checked((UInt128) this.Signed),
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

#endif

			[Pure]
			public float ToSingle(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToSingle(this.Decimal),
				Kind.Double => (float) this.Double,
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

			[Pure]
			public double ToDouble(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToDouble(this.Decimal),
				Kind.Double => this.Double,
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

#if NET5_0_OR_GREATER
			// System.Half does not exist on netstandard2.0
			[Pure]
			public Half ToHalf(Kind kind) => kind switch
			{
				Kind.Decimal => (Half) decimal.ToDouble(this.Decimal),
				Kind.Double => (Half) this.Double,
				Kind.Signed => (Half) this.Signed,
				Kind.Unsigned => (Half) this.Unsigned,
				_ => default
			};
#endif

			[Pure]
			public decimal ToDecimal(Kind kind) => kind switch
			{
				Kind.Decimal => this.Decimal,
				Kind.Double => new decimal(this.Double),
				Kind.Signed => new decimal(this.Signed),
				Kind.Unsigned => new decimal(this.Unsigned),
				_ => 0
			};

			#endregion

			#region Arithmetic...

			/// <summary>Add a number to another number</summary>
			public static void Add(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: AddSignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   AddSignedDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  AddSignedDecimal (xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedUnsigned  (yValue.Signed,   xValue.Unsigned, out xKind, out xValue); return;
							case Kind.Unsigned: AddUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   AddUnsignedDouble  (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  AddUnsignedDecimal (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedDouble  (yValue.Signed,   xValue.Double,  out xKind, out xValue); return;
							case Kind.Unsigned: AddUnsignedDouble(yValue.Unsigned, xValue.Double,  out xKind, out xValue); return;
							case Kind.Double:   AddDoubleDouble  (xValue.Double,   yValue.Double,  out xKind, out xValue); return;
							case Kind.Decimal:  AddDoubleDecimal (xValue.Double,   yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedDecimal  (yValue.Signed,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Unsigned: AddUnsignedDecimal(yValue.Unsigned, xValue.Decimal, out xKind, out xValue); return;
							case Kind.Double:   AddDoubleDecimal  (yValue.Double,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Decimal:  AddDecimalDecimal (xValue.Decimal,  yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
				}

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				static void AddSignedSigned(long x, long y, out Kind kind, out Number result)
				{
					//note: adding large numbers could overflow signed but still be valid for unsigned
					if (x >= 0 && y >= 0)
					{ // result will be positive
						if (x > 4611686018427387903 || y > 4611686018427387903)
						{ // result may overflow signed
							ulong r = checked((ulong) x + (ulong) y);
							kind = r <= long.MaxValue ? Kind.Signed : Kind.Unsigned;
							result = new Number(r);
							return;
						}
					}

					kind = Kind.Signed;
					result = new Number(checked(x + y));
				}

				static void AddSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
				{
					ulong val;
					if (x < 0)
					{ // inverse the order of the arguments, to simplify dealing with negative numbers
						if (x == long.MinValue)
							val = checked(y - 0x8000000000000000UL); // cannot do -(long.MinValue), so special case this one
						else
							val = checked(y - (ulong)(-x)); // (-X) + Y == Y - X
					}
					else
					{
						val = checked((ulong) x + y);
					}

					if (val <= long.MaxValue)
					{ // fits in a signed
						kind = Kind.Signed;
						result = new Number((long)val);
					}
					else
					{ // upgrade to unsigned
						kind = Kind.Unsigned;
						result = new Number(val);
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddSignedDouble(long x, double y, out Kind kind, out Number result)
				{
					kind = Kind.Double;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddSignedDecimal(long x, decimal y, out Kind kind, out Number result)
				{
					kind = Kind.Decimal;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					rKind = Kind.Unsigned;
					result = new Number(checked(x + y));
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x + y);
				}

				static void AddDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number((Decimal) x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number(x + y);
				}

			}

			/// <summary>Subtract a number from another number</summary>
			public static void Subtract(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractSignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: SubtractSignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDoubleDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal(xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractUnsignedSigned  (xValue.Unsigned, yValue.Signed, out xKind, out xValue); return;
							case Kind.Unsigned: SubtractUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDoubleDouble    (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal  (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractDoubleDouble (xValue.Double, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: SubtractDoubleDouble (xValue.Double, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDoubleDouble (xValue.Double, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal((decimal) xValue.Double, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractDecimalDecimal(xValue.Decimal, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: SubtractDecimalDecimal(xValue.Decimal, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDecimalDecimal(xValue.Decimal, (decimal) yValue.Double, out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal(xValue.Decimal, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
				}

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractSignedSigned(long x, long y, out Kind kind, out Number result)
				{
					if (x >= 0 && y < 0)
					{
						ulong r = (ulong) x + (y == long.MinValue ? 9223372036854775808UL : (ulong) (-y));
						if (r > long.MaxValue)
						{
							kind = Kind.Unsigned;
							result = new Number(r);
						}
						else
						{
							kind = Kind.Signed;
							result = new Number((long) r);
						}
					}
					else
					{
						kind = Kind.Signed;
						result = new Number(checked(x - y));
					}
				}

				static void SubtractSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
				{
					if (x > 0)
					{
						if ((ulong) x == y)
						{ // X - X = 0
							kind = Kind.Signed;
							result = default;
						}
						else if (y < (ulong) x)
						{ // the result will still be positive
							kind = Kind.Signed;
							result = new Number(x - (long) y);
						}
						else
						{ // the result will become negative, ex: for x = 123 and y = 456 then x - y = -333

							// it's easier to do the reverse subtraction (which is positive), and then flip the sign: x - y = -(y - x)
							ulong z = y - (ulong) x; // z == y - x == 456 - 123 == 333
							long r = checked(-((long) z)); // r == -333
							kind = Kind.Signed;
							result = new Number(r);
						}
					}
					else
					{ // the result will be negative
						// note that if X < 0 then X - Y = -|X| - Y = -(|X| + Y)
						// ex: x == -123 and y == 456 then x - y == -579
						ulong nx = x == long.MinValue ? 9223372036854775808UL : (ulong) -x; // nx == 123
						ulong z = checked(y + nx); // z == 456 + 123 == 579
						long r = checked(-(long) z); // r == -579
						kind = Kind.Signed;
						result = new Number(r);
					}
				}

				static void SubtractUnsignedSigned(ulong x, long y, out Kind kind, out Number result)
				{
					if (y >= 0)
					{ // If y >= 0 then X - Y == X - |Y|
						SubtractUnsignedUnsigned(x, (ulong) y, out kind, out result);
					}
					else
					{ // If y < 0 then X - Y == X - (-|Y|) == X + |Y|

						ulong ny = y == long.MinValue ? 9223372036854775808UL : (ulong) -y;
						ulong r = checked(x + ny);
						if (r > long.MaxValue)
						{
							kind = Kind.Unsigned;
							result = new Number(r);
						}
						else
						{
							kind = Kind.Signed;
							result = new Number((long) r);
						}
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					if (y > x)
					{ // result will be negative
						// X - Y == -(Y - X)
						ulong r = y - x;
						rKind = Kind.Signed;
						result = new Number(checked(-(long) r));
					}
					else
					{ // result will be positive
						rKind = Kind.Unsigned;
						result = new Number(checked(x - y));
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x - y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					decimal r = x - y;

#if NET8_0_OR_GREATER
					if (decimal.IsInteger(r) && r >= long.MinValue)
					{ // attempt to convert it to a signed/unsigned integer

						if (r <= long.MinValue)
						{ // signed
							rKind = Kind.Signed;
							result = new Number((long) r);
							return;
						}
						if (r <= ulong.MaxValue)
						{ // unsigned
							rKind = Kind.Unsigned;
							result = new Number((ulong) r);
							return;
						}
						// too large to represent
					}
#endif

					rKind = Kind.Decimal;
					result = new Number(r);
				}
			}

			/// <summary>Multiply a number with another number</summary>
			public static void Multiply(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: MultiplySignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   MultiplySignedDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  MultiplySignedDecimal (xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedUnsigned  (yValue.Signed,   xValue.Unsigned, out xKind, out xValue); return;
							case Kind.Unsigned: MultiplyUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   MultiplyUnsignedDouble  (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  MultiplyUnsignedDecimal (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedDouble  (yValue.Signed,   xValue.Double,  out xKind, out xValue); return;
							case Kind.Unsigned: MultiplyUnsignedDouble(yValue.Unsigned, xValue.Double,  out xKind, out xValue); return;
							case Kind.Double:   MultiplyDoubleDouble  (xValue.Double,   yValue.Double,  out xKind, out xValue); return;
							case Kind.Decimal:  MultiplyDoubleDecimal (xValue.Double,   yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedDecimal  (yValue.Signed,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Unsigned: MultiplyUnsignedDecimal(yValue.Unsigned, xValue.Decimal, out xKind, out xValue); return;
							case Kind.Double:   MultiplyDoubleDecimal  (yValue.Double,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Decimal:  MultiplyDecimalDecimal (xValue.Decimal,  yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
				}

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplySignedSigned(long x, long y, out Kind kind, out Number result)
				{
					if (y == -1 && x != long.MinValue)
					{ // frequently used to compute the absolute value of a negative number
						kind = Kind.Signed;
						result = new Number(-x);
						return;
					}

					if (x >= 0)
					{
						if (y >= 0)
						{ // return will be positive
							ulong r = checked((ulong) x * (ulong) y);
							kind = r <= long.MaxValue ? Kind.Signed : Kind.Unsigned;
							result = new Number(r);
							return;
						}
					}
					else
					{
						if (y is < 0 and > long.MinValue)
						{ // return will be positive: x * y = |x| * |y| when x < 0 and y < 0
							ulong r = checked((ulong) -x * (ulong) -y);
							kind = r <= long.MaxValue ? Kind.Signed : Kind.Unsigned;
							result = new Number(r);
							return;
						}
					}

					kind = Kind.Signed;
					result = new Number(checked(x * y));
				}

				static void MultiplySignedUnsigned(long x, ulong y, out Kind kind, out Number result)
				{
					if (x < 0)
					{ // the result must be signed!
						kind = Kind.Signed;
						result = new Number(checked(x * (long) y));
						return;
					}
					ulong val = checked((ulong) x * y);

					if (val <= long.MaxValue)
					{ // fits in a signed
						kind = Kind.Signed;
						result = new Number((long) val);
					}
					else
					{ // upgrade to unsigned
						kind = Kind.Unsigned;
						result = new Number(val);
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplySignedDouble(long x, double y, out Kind kind, out Number result)
				{
					kind = Kind.Double;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplySignedDecimal(long x, decimal y, out Kind kind, out Number result)
				{
					kind = Kind.Decimal;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					rKind = Kind.Unsigned;
					result = new Number(checked(x * y));
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x * y);
				}

				static void MultiplyDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number((decimal) x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number(x * y);
				}

			}

			/// <summary>Divide a number with another number</summary>
			public static void Divide(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: DivideSignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   DivideSignedDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  DivideSignedDecimal (xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedUnsigned  (yValue.Signed,   xValue.Unsigned, out xKind, out xValue); return;
							case Kind.Unsigned: DivideUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   DivideUnsignedDouble  (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  DivideUnsignedDecimal (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedDouble  (yValue.Signed,   xValue.Double,  out xKind, out xValue); return;
							case Kind.Unsigned: DivideUnsignedDouble(yValue.Unsigned, xValue.Double,  out xKind, out xValue); return;
							case Kind.Double:   DivideDoubleDouble  (xValue.Double,   yValue.Double,  out xKind, out xValue); return;
							case Kind.Decimal:  DivideDoubleDecimal (xValue.Double,   yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedDecimal  (yValue.Signed,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Unsigned: DivideUnsignedDecimal(yValue.Unsigned, xValue.Decimal, out xKind, out xValue); return;
							case Kind.Double:   DivideDoubleDecimal  (yValue.Double,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Decimal:  DivideDecimalDecimal (xValue.Decimal,  yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
				}

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideSignedSigned(long x, long y, out Kind kind, out Number result)
				{
					kind = Kind.Signed;
					result = new Number(x / y);
				}

				static void DivideSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
				{
					if (x < 0)
					{ // the result must be signed!
						kind = Kind.Signed;
						result = new Number(checked(x / (long) y));
						return;
					}
					ulong val = checked((ulong) x / y);

					if (val <= long.MaxValue)
					{ // fits in a signed
						kind = Kind.Signed;
						result = new Number((long) val);
					}
					else
					{ // upgrade to unsigned
						kind = Kind.Unsigned;
						result = new Number(val);
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideSignedDouble(long x, double y, out Kind kind, out Number result)
				{
					kind = Kind.Double;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideSignedDecimal(long x, decimal y, out Kind kind, out Number result)
				{
					kind = Kind.Decimal;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					rKind = Kind.Unsigned;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x / y);
				}

				static void DivideDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number((decimal) x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number(x / y);
				}

			}

			#endregion

		}

		#endregion

		#region Private Fields...

		// instance = 16 bytes
		private readonly Number m_value;   // 16 bytes
		private readonly Kind m_kind;      //  4 bytes

		private string? m_literal; //  8 + (26 + length * 2)

		// Memory Footprint:
		// -----------------
		//
		// MEM = (16 + 16 + 4 + 8 + (4)) + (26 + length*2)
		//     = 74 + length*2
		//
		// -> "1"            =>  76 bytes !
		// -> "1.0"          =>  80 bytes !
		// -> "1234"         =>  82 bytes !
		// -> systime        => 102 bytes !!
		// -> Math.PI        => 114 bytes !!
		
		// By comparison:
		// -> long, double	 =>  8 bytes
		// -> decimal        => 16 bytes
		// -> boxed(long)    => 24 bytes
		// -> boxed(double)  => 24 bytes
		// -> boxed(decimal) => 32 bytes

		// int[60] => 264 bytes
		// long[60] => 504 bytes
		// JsonArray[60 x JsonNumber]=> 64 + 60*8 + 60*~82 => 5,464 bytes => x10 !!!

		#endregion

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private JsonNumber(Number value, Kind kind, string? literal)
		{
			m_value = value;
			m_kind = kind;
			m_literal = literal;
		}

		#region Factories...

		/// <summary>Returns a singleton from the small numbers cache</summary>
		/// <param name="value">Value that must be in the range [-128, +255]</param>
		/// <returns>Cached JsonNumber for this value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static JsonNumber GetCachedSmallNumber(int value)
		{
			Contract.Debug.Requires(value is >= CACHED_SIGNED_MIN and <= CACHED_SIGNED_MAX);
			return SmallNumbers[value - CACHED_SIGNED_MIN];
		}

		#region Create(...)

		/// <summary>Special helper to create a number from its constituents</summary>
		[Pure]
		private static JsonNumber Create(in Number value, Kind kind) => kind switch
		{
			Kind.Signed   => Create(value.Signed),
			Kind.Unsigned => Create(value.Unsigned),
			Kind.Double   => Create(value.Double),
			Kind.Decimal  => Create(value.Decimal),
			_ => throw new NotSupportedException()
		};

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Create(string value)
		{
			Contract.NotNull(value);
			return CrystalJsonParser.ParseJsonNumber(value) ?? Zero;
		}

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Create(ReadOnlySpan<char> value) => CrystalJsonParser.ParseJsonNumber(value) ?? Zero;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Create(byte value) => SmallNumbers[value + CACHED_OFFSET_ZERO];

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Create(sbyte value) => SmallNumbers[value + CACHED_OFFSET_ZERO];

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Create(short value) => Create((int) value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Create(ushort value) => Create((uint) value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure]
		public static JsonNumber Create(int value)
		{
			//note: this method has been optimized looking at the JIT disassembly, to maximize inlining (as of .NET 9)

			// check if this is a cached number, removing an extra bound check
			var xs = SmallNumbers;
			uint p = unchecked((uint) (value - CACHED_SIGNED_MIN));
			if (p < xs.Length)
			{
				return xs[p];
			}

			// Currently, value.ToString(null) is inlined for positive numbers, but will use the CurrentCulture for the negative sign,
			// so we have to pre-check and either call with a null provider if positive, or NumberFormatInfo.InvariantInfo if negative.
			return new JsonNumber(new Number(value), Kind.Signed, value < 0 ? value.ToString(NumberFormatInfo.InvariantInfo) : value.ToString(default(IFormatProvider)));
		}

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure]
		public static JsonNumber Create(uint value)
		{
			//note: this method has been optimized looking at the JIT disassembly, to maximize inlining (as of .NET 9)

			// check if this is a cached number, removing an extra bound check
			var xs = SmallNumbers;
			long p = (long) value + CACHED_OFFSET_ZERO;
			if (p < xs.Length)
			{
				return xs[p];
			}

			// Currently, value.ToString(null) is inlined for positive numbers
			return new JsonNumber(new Number(value), Kind.Signed, value.ToString(default(IFormatProvider)));
		}

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values (between -128 and 255) a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure]
		public static JsonNumber Create(long value)
		{
			//note: this method has been optimized looking at the JIT disassembly, to maximize inlining (as of .NET 9)

			// check if this is a cached number, removing an extra bound check
			var xs = SmallNumbers;
			long p = unchecked(value - CACHED_SIGNED_MIN);
			if (p >= 0 && p < xs.Length)
			{
				return xs[p];
			}

			// Currently, value.ToString(null) is inlined for positive numbers, but will use the CurrentCulture for the negative sign,
			// so we have to pre-check and either call with a null provider if positive, or NumberFormatInfo.InvariantInfo if negative.
			return new JsonNumber(new Number(value), Kind.Signed, value < 0 ? value.ToString(NumberFormatInfo.InvariantInfo) : value.ToString(default(IFormatProvider)));
		}

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values (between 0 and 255) a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure]
		public static JsonNumber Create(ulong value)
		{
			//note: this method has been optimized looking at the JIT disassembly, to maximize inlining (as of .NET 9)

			// check if this is a cached number, removing an extra bound check
			var xs = SmallNumbers;
			if (value < CACHED_SIGNED_MAX)
			{
				return xs[value + CACHED_OFFSET_ZERO];
			}

			// Currently, value.ToString(null) is inlined for positive numbers, but will use the CurrentCulture for the negative sign,
			// so we have to pre-check and either call with a null provider if positive, or NumberFormatInfo.InvariantInfo if negative.
			return new JsonNumber(new Number(value), Kind.Unsigned, value.ToString(default(IFormatProvider)));
		}

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Decimal value</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>For <see langword="0"/>, <see langword="1"/> and <c>NaN</c> a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure]
		public static JsonNumber Create(double value) =>
			value == 0d ? DecimalZero
			: value == 1d ? DecimalOne
			: double.IsNaN(value) ? NaN
			: new JsonNumber(new Number(value), Kind.Double, StringConverters.ToString(value));

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Decimal value</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>For <see langword="0"/>, <see langword="1"/> and <c>NaN</c> a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure]
		public static JsonNumber Create(float value) =>
			value == 0f ? DecimalZero
			: value == 1f ? DecimalOne
			: float.IsNaN(value) ? NaN
			: new JsonNumber(new Number(value), Kind.Double, StringConverters.ToString(value));

#if NET8_0_OR_GREATER
		
		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Decimal value</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>For <see langword="0"/>, <see langword="1"/> and <c>NaN</c> a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Create(Half value) =>
			value == Half.Zero ? DecimalZero
			: value == Half.One ? DecimalOne
			: Half.IsNaN(value) ? NaN
			: new JsonNumber(new Number((double) value), Kind.Double, StringConverters.ToString(value));

#else

#if NET5_0_OR_GREATER
		// System.Half does not exist on netstandard2.0
		private static readonly Half HalfZero = (Half) 0;
		private static readonly Half HalfOne = (Half) 1;

		[Pure]
		public static JsonNumber Create(Half value) =>
			  value == HalfZero ? DecimalZero
			: value == HalfOne ? DecimalOne
			: Half.IsNaN(value) ? NaN
			: new JsonNumber(new Number((double) value), Kind.Double, StringConverters.ToString(value));
#endif

#endif

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure]
		public static JsonNumber Create(decimal value) => new(new Number(value), Kind.Decimal, StringConverters.ToString(value));

#if NET8_0_OR_GREATER

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure]
		public static JsonNumber Create(Int128 value) => new(new Number(value), Kind.Signed, StringConverters.ToString(value));

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure]
		public static JsonNumber Create(UInt128 value) => new(new Number(value), Kind.Unsigned, StringConverters.ToString(value));

#endif

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure]
		public static JsonNumber Create(Uuid48 value) => new(new Number(value.ToUInt64()), Kind.Unsigned, StringConverters.ToString(value.ToUInt64()));

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds in the interval</summary>
		/// <param name="value">Interval (in seconds)</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>Since <see cref="TimeSpan.TotalSeconds"/> can introduce rounding errors, this value may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the number of <see cref="TimeSpan.Ticks"/> instead.</para>
		/// </remarks>
		[Pure]
		public static JsonNumber Create(TimeSpan value) => value == TimeSpan.Zero ? DecimalZero : Create(value.TotalSeconds);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">DateTime to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>By convention, <see cref="DateTime.MinValue"/> is equivalent to <see langword="0"/>, and <see cref="DateTime.MaxValue"/> is equivalent to <c>NaN</c>.</para>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonDateTime.Return(DateTime)">as a string</see> instead.</para>
		/// </remarks>
		[Pure]
		public static JsonNumber Create(DateTime value)
		{
			// Converted as the number of seconds elapsed since 1970-01-01Z
			// By convention, DateTime.MinValue is 0 (since it is equal to default(DateTime)), and DateTime.MaxValue is "NaN"
			const long UNIX_EPOCH_TICKS = 621355968000000000L;
			return value == DateTime.MinValue ? DecimalZero
				: value == DateTime.MaxValue ? NaN
				: Create((double) (value.ToUniversalTime().Ticks - UNIX_EPOCH_TICKS) / TimeSpan.TicksPerSecond);
		}

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">DateTimeOffset to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>By convention, <see cref="DateTime.MinValue"/> is equivalent to <see langword="0"/>, and <see cref="DateTime.MaxValue"/> is equivalent to <c>NaN</c>.</para>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonDateTime.Return(DateTime)">as a string</see> instead.</para>
		/// </remarks>
		[Pure]
		public static JsonNumber Create(DateTimeOffset value)
		{
			// Converted as the number of seconds elapsed since 1970-01-01Z
			// By convention, DateTime.MinValue is 0 (since it is equal to default(DateTime)), and DateTime.MaxValue is "NaN"
			const long UNIX_EPOCH_TICKS = 621355968000000000L;
			return value == DateTimeOffset.MinValue ? DecimalZero
				: value == DateTimeOffset.MaxValue ? NaN
				: Create((double) (value.ToUniversalTime().Ticks - UNIX_EPOCH_TICKS) / TimeSpan.TicksPerSecond);
		}

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of days elapsed since the UNIX Epoch</summary>
		[Pure]
		public static JsonNumber Create(DateOnly value)
			=> value == DateOnly.MinValue ? DecimalZero
				: value == DateOnly.MaxValue ? NaN
#if NET6_0_OR_GREATER
				: Create((value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalDays);
#else
				// DateTime.UnixEpoch is not on netstandard2.0
				: Create((value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - new DateTime(621355968000000000L, DateTimeKind.Utc)).TotalDays);
#endif

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since midnight</summary>
		/// <param name="value">Time to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonString.Return(Instant)">as a string</see> instead.</para>
		/// </remarks>
		[Pure]
		public static JsonNumber Create(TimeOnly value) => Create(value.ToTimeSpan());

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">Instant to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonString.Return(Instant)">as a string</see> instead.</para>
		/// </remarks>
		[Pure]
		public static JsonNumber Create(NodaTime.Instant value) => value != default ? Create((value - default(NodaTime.Instant)).TotalSeconds) : DecimalZero;

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed</summary>
		[Pure]
		public static JsonNumber Create(NodaTime.Duration value) => value == NodaTime.Duration.Zero ? DecimalZero : Create((double)value.BclCompatibleTicks / NodaTime.NodaConstants.TicksPerSecond);

		#endregion

		#region Return(...)

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(string value) => CrystalJsonParser.ParseJsonNumber(value) ?? Zero;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(byte value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(byte? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(sbyte value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(sbyte? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(short value) => Create((int) value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(short? value) => value.HasValue ? Create((int) value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ushort value) => Create((uint) value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ushort? value) => value.HasValue ? Create((uint) value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(int value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value, that can be null.</param>
		/// <returns>JSON value that will be serialized as an integer, or <see cref="JsonNull.Null"/>.</returns>
		/// <remarks>For small values a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(int? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(uint value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value, that can be null.</param>
		/// <returns>JSON value that will be serialized as an integer, or <see cref="JsonNull.Null"/>.</returns>
		/// <remarks>For small values a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(uint? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values (between -128 and 255) a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(long value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(long? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Integer value</param>
		/// <returns>JSON value that will be serialized as an integer.</returns>
		/// <remarks>For small values (between 0 and 255) a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ulong value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ulong? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Decimal value</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>For <see langword="0"/>, <see langword="1"/> and <c>NaN</c> a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(double value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(double? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Decimal value</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>For <see langword="0"/>, <see langword="1"/> and <c>NaN</c> a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(float value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(float? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

#if NET5_0_OR_GREATER
		// System.Half does not exist on netstandard2.0
		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		/// <param name="value">Decimal value</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>For <see langword="0"/>, <see langword="1"/> and <c>NaN</c> a cached singleton is returned. For others values, a new instance will be allocated.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Half value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Half? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;
#endif

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(decimal value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(decimal? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

#if NET8_0_OR_GREATER

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Int128 value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Int128? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(UInt128 value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(UInt128? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

#endif

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Uuid48 value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Uuid48? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;


		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds in the interval</summary>
		/// <param name="value">Interval (in seconds)</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>Since <see cref="TimeSpan.TotalSeconds"/> can introduce rounding errors, this value may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the number of <see cref="TimeSpan.Ticks"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(TimeSpan value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds in the interval</summary>
		/// <param name="value">Interval to convert, or <see langword="null"/></param>
		/// <returns>JSON value that will be serialized as a decimal value, or <see cref="JsonNull.Null"/>.</returns>
		/// <remarks>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the number of <see cref="TimeSpan.Ticks"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(TimeSpan? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">DateTime to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>By convention, <see cref="DateTime.MinValue"/> is equivalent to <see langword="0"/>, and <see cref="DateTime.MaxValue"/> is equivalent to <c>NaN</c>.</para>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonDateTime.Return(DateTime)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTime value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">DateTime to convert, or <see langword="null"/></param>
		/// <returns>JSON value that will be serialized as a decimal value, or <see cref="JsonNull.Null"/>.</returns>
		/// <remarks>
		/// <para>By convention, <see cref="DateTime.MinValue"/> is equivalent to <see langword="0"/>, and <see cref="DateTime.MaxValue"/> is equivalent to <c>NaN</c>.</para>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonDateTime.Return(DateTime)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTime? value) => value is not null ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">DateTimeOffset to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>By convention, <see cref="DateTime.MinValue"/> is equivalent to <see langword="0"/>, and <see cref="DateTime.MaxValue"/> is equivalent to <c>NaN</c>.</para>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonDateTime.Return(DateTime)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTimeOffset value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">DateTimeOffset to convert, or <see langword="null"/></param>
		/// <returns>JSON value that will be serialized as a decimal value, or <see cref="JsonNull.Null"/>.</returns>
		/// <remarks>
		/// <para>By convention, <see cref="DateTime.MinValue"/> is equivalent to <see langword="0"/>, and <see cref="DateTime.MaxValue"/> is equivalent to <c>NaN</c>.</para>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonDateTime.Return(DateTime)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTimeOffset? value) => value is not null ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of days elapsed since the UNIX Epoch</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateOnly value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of days elapsed since the UNIX Epoch</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateOnly? value) => value is not null ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since midnight</summary>
		/// <param name="value">Time to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonString.Return(Instant)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(TimeOnly value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since midnight</summary>
		/// <param name="value">Time to convert, or <see langword="null"/></param>
		/// <returns>JSON value that will be serialized as a decimal value, or <see cref="JsonNull.Null"/>.</returns>
		/// <remarks>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonString.Return(Instant)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(TimeOnly? value) => value is null ? JsonNull.Null : Create(value.Value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		/// <param name="value">Instant to convert</param>
		/// <returns>JSON value that will be serialized as a decimal value.</returns>
		/// <remarks>
		/// <para>This method can introduce rounding errors, so <paramref name="value"/> may not round-trip in all cases.</para>
		/// <para>If an exact representation is required, please serialize the date <see cref="JsonString.Return(Instant)">as a string</see> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed since UNIX Epoch</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Duration value) => Create(value);

		/// <summary>Returns a <see cref="JsonNumber"/> corresponding to the number of seconds elapsed</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Duration? value) => value.HasValue ? Create(value.Value) : JsonNull.Null;

		#endregion

		#region Parse(...)

		[Pure]
		internal static JsonNumber ParseSigned(long value, ReadOnlySpan<char> literal, string? original)
		{
			if (value is >= CACHED_SIGNED_MIN and <= CACHED_SIGNED_MAX)
			{ // use the small integers cache
				var num = SmallNumbers[value - CACHED_SIGNED_MIN];
				if (literal.Length == 0 || literal.SequenceEqual(num.Literal))
				{
					return num;
				}
			}

			return new JsonNumber(new Number(value), Kind.Signed, original ?? (literal.Length > 0 ? literal.ToString() : null));
		}

		[Pure]
		internal static JsonNumber ParseUnsigned(ulong value, ReadOnlySpan<char> literal, string? original)
		{
			if (value <= CACHED_SIGNED_MAX)
			{ // use the small integers cache
				var num = SmallNumbers[value + CACHED_OFFSET_ZERO];
				if (literal.Length == 0 || literal.SequenceEqual(num.Literal))
				{
					return num;
				}
			}

			return new JsonNumber(new Number(value), value <= long.MaxValue ? Kind.Signed : Kind.Unsigned, original ?? (literal.Length > 0 ? literal.ToString() : null));
		}

		[Pure]
		internal static JsonNumber Parse(double value, ReadOnlySpan<char> literal, string? original)
		{
			// first try to coerce to an integer
			long l = (long) value;
			if (l == value)
			{ // we can probably use a cached number for this
				return ParseSigned(l, literal, original);
			}

			if (double.IsNaN(value))
			{
				return JsonNumber.NaN;
			}

			return new JsonNumber(new Number(value), Kind.Double, original ?? (literal.Length > 0 ? literal.ToString() : null));
		}

		[Pure]
		internal static JsonNumber Parse(decimal value, ReadOnlySpan<char> literal, string? original)
		{
			// first try to coerce to a smaller integer (only within Int64 range: a decimal->long cast throws if it overflows)
			if (value is >= long.MinValue and <= long.MaxValue)
			{
				long l = (long) value;
				if (l == value)
				{ // we can probably use a cached number for this
					return ParseSigned(l, literal, original);
				}
			}

			return new JsonNumber(new Number(value), Kind.Decimal, original ?? (literal.Length > 0 ? literal.ToString() : null));
		}

		[Pure]
		internal static JsonValue Parse(ReadOnlySpan<byte> value)
		{
			if (value.Length <= 0) throw ThrowHelper.ArgumentException(nameof(value), "Size must be at least one");

#if NET5_0_OR_GREATER
			if (value.Length <= 32)
			{
				Span<char> buffer = stackalloc char[value.Length];
				if (System.Text.Ascii.ToUtf16(value, buffer, out int written) == OperationStatus.Done)
				{
					return Parse(buffer[..written]);
				}
			}
#else
			// Ascii.ToUtf16 is not on netstandard2.0: manual widening loop
			if (value.Length <= 32)
			{
				Span<char> buffer = stackalloc char[value.Length];
				int written = 0;
				while (written < value.Length && value[written] <= 0x7F)
				{
					buffer[written] = (char) value[written];
					++written;
				}
				if (written == value.Length)
				{
					return Parse(buffer[..written]);
				}
			}
#endif

			unsafe
			{
				fixed(byte* ptr = value)
				{
					return Parse(ptr, value.Length);
				}
			}
		}

		[Pure]
		internal static unsafe JsonValue Parse(byte* ptr, int size)
		{
			Contract.PointerNotNull(ptr);
			if (size <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(size), size, "Size must be at least one");

			//TODO: optimize!
			var literal = new string((sbyte*) ptr, 0, size); //ASCII is ok

			return CrystalJsonParser.ParseJsonNumber(literal) ?? throw ThrowHelper.FormatException($"Invalid number literal '{literal}'.");
		}

		#endregion

		#endregion

		#endregion

		/// <summary>Returns <see langword="true"/> for decimal number (ex: "1.23", "123E-2"), or <see langword="false"/> for integers ("123", "1.23E10", ...)</summary>
		/// <remarks>It is possible, in some cases, that "2.0" would be considered a decimal number!</remarks>
		[Pure]
		public bool IsDecimal => m_kind >= Kind.Double;

		/// <summary>Returns <see langword="true"/> for positive integers (ex: "123"), or <see langword="false"/> for negative integers (ex: "-123") or decimal numbers (ex: "1.23")</summary>
		[Pure]
		public bool IsUnsigned => m_kind == Kind.Unsigned;

		/// <summary>Returns <see langword="true"/> for negative numbers (ex: "-123", "-1.23"), or <see langword="false"/> for positive numbers ("0", "123", "1.23")</summary>
		[Pure]
		public bool IsNegative => m_value.IsNegative(m_kind);

		/// <summary>Returns the literal representation of the number (as it appeared in the original JSON document)</summary>
		/// <remarks>Could return "1.0" or "1", depending on the original format.</remarks>
		public string Literal => m_literal ?? ComputeLiteral();

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private string ComputeLiteral()
		{
			string literal = ComputeLiteral(in m_value, m_kind);
			m_literal = literal;
			return literal;
		}

		private static string ComputeLiteral(in Number number, Kind kind) => kind switch
		{
			Kind.Signed => StringConverters.ToString(number.Signed),
			Kind.Unsigned => StringConverters.ToString(number.Unsigned),
			Kind.Double => StringConverters.ToString(number.Double),
			Kind.Decimal => StringConverters.ToString(number.Decimal),
			_ => throw new ArgumentOutOfRangeException(nameof(kind))
		};

		/// <summary>Tests if the number is between the specified bounds (both included)</summary>
		/// <param name="minInclusive">Minimum value (included)</param>
		/// <param name="maxInclusive">Maximum value (included)</param>
		/// <returns><see langword="true"/> if <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(long minInclusive, long maxInclusive) => (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;

		/// <summary>Tests if the number is between the specified bounds (both included)</summary>
		/// <param name="minInclusive">Minimum value (included)</param>
		/// <param name="maxInclusive">Maximum value (included)</param>
		/// <returns><see langword="true"/> if <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(ulong minInclusive, ulong maxInclusive) => (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;

		/// <summary>Tests if the number is between the specified bounds (both included)</summary>
		/// <param name="minInclusive">Minimum value (included)</param>
		/// <param name="maxInclusive">Maximum value (included)</param>
		/// <returns><see langword="true"/> if <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(double minInclusive, double maxInclusive) => (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;

		#region JsonValue Members...

		/// <inheritdoc />
		public override JsonType Type => JsonType.Number;

		/// <inheritdoc />
		public override bool IsDefault => m_value.IsZero(m_kind);

		/// <inheritdoc />
		public override bool IsReadOnly => true; //note: numbers are immutable

		/// <summary>Converts this number into a type that closely matches the value (integer or decimal)</summary>
		/// <returns>Return either an int/long for integers, or a double/decimal for floating point numbers</returns>
		/// <remarks>For integers: If the value is between int.MinValue and int.MaxValue, it will be cast to <see cref="Int32"/>; otherwise, it will be cast to <see cref="Int64"/>.</remarks>
		public override object? ToObject() => m_value.ToObject(m_kind);

		/// <inheritdoc />
		public override TValue? Bind<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
			where TValue : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build

			if (default(TValue) is null)
			{
				if (typeof(TValue) == typeof(bool?)) return (TValue) (object) ToBoolean();
				if (typeof(TValue) == typeof(byte?)) return (TValue) (object) ToByte();
				if (typeof(TValue) == typeof(sbyte?)) return (TValue) (object) ToSByte();
				if (typeof(TValue) == typeof(char?)) return (TValue) (object) ToChar();
				if (typeof(TValue) == typeof(short?)) return (TValue) (object) ToInt16();
				if (typeof(TValue) == typeof(ushort?)) return (TValue) (object) ToUInt16();
				if (typeof(TValue) == typeof(int?)) return (TValue) (object) ToInt32();
				if (typeof(TValue) == typeof(uint?)) return (TValue) (object) ToUInt32();
				if (typeof(TValue) == typeof(ulong?)) return (TValue) (object) ToUInt64();
				if (typeof(TValue) == typeof(long?)) return (TValue) (object) ToInt64();
				if (typeof(TValue) == typeof(float?)) return (TValue) (object) ToSingle();
				if (typeof(TValue) == typeof(double?)) return (TValue) (object) ToDouble();
				if (typeof(TValue) == typeof(decimal?)) return (TValue) (object) ToDecimal();
				if (typeof(TValue) == typeof(TimeSpan?)) return (TValue) (object) ToTimeSpan();
				if (typeof(TValue) == typeof(DateTime?)) return (TValue) (object) ToDateTime();
				if (typeof(TValue) == typeof(DateTimeOffset?)) return (TValue) (object) ToDateTimeOffset();
				if (typeof(TValue) == typeof(DateOnly?)) return (TValue) (object) ToDateOnly();
				if (typeof(TValue) == typeof(TimeOnly?)) return (TValue) (object) ToTimeOnly();
				if (typeof(TValue) == typeof(Guid?)) return (TValue) (object) ToGuid();
				if (typeof(TValue) == typeof(Uuid128?)) return (TValue) (object) ToUuid128();
				if (typeof(TValue) == typeof(Uuid96?)) return (TValue) (object) ToUuid96();
				if (typeof(TValue) == typeof(Uuid80?)) return (TValue) (object) ToUuid80();
				if (typeof(TValue) == typeof(Uuid64?)) return (TValue) (object) ToUuid64();
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return (TValue) (object) ToInstant();
				if (typeof(TValue) == typeof(NodaTime.Duration?)) return (TValue) (object) ToDuration();
			}
			else
			{
				if (typeof(TValue) == typeof(bool)) return (TValue) (object) ToBoolean();
				if (typeof(TValue) == typeof(byte)) return (TValue) (object) ToByte();
				if (typeof(TValue) == typeof(sbyte)) return (TValue) (object) ToSByte();
				if (typeof(TValue) == typeof(char)) return (TValue) (object) ToChar();
				if (typeof(TValue) == typeof(short)) return (TValue) (object) ToInt16();
				if (typeof(TValue) == typeof(ushort)) return (TValue) (object) ToUInt16();
				if (typeof(TValue) == typeof(int)) return (TValue) (object) ToInt32();
				if (typeof(TValue) == typeof(uint)) return (TValue) (object) ToUInt32();
				if (typeof(TValue) == typeof(ulong)) return (TValue) (object) ToUInt64();
				if (typeof(TValue) == typeof(long)) return (TValue) (object) ToInt64();
				if (typeof(TValue) == typeof(float)) return (TValue) (object) ToSingle();
				if (typeof(TValue) == typeof(double)) return (TValue) (object) ToDouble();
				if (typeof(TValue) == typeof(decimal)) return (TValue) (object) ToDecimal();
				if (typeof(TValue) == typeof(TimeSpan)) return (TValue) (object) ToTimeSpan();
				if (typeof(TValue) == typeof(DateTime)) return (TValue) (object) ToDateTime();
				if (typeof(TValue) == typeof(DateTimeOffset)) return (TValue) (object) ToDateTimeOffset();
				if (typeof(TValue) == typeof(DateOnly)) return (TValue) (object) ToDateOnly();
				if (typeof(TValue) == typeof(TimeOnly)) return (TValue) (object) ToTimeOnly();
				if (typeof(TValue) == typeof(Guid)) return (TValue) (object) ToGuid();
				if (typeof(TValue) == typeof(Uuid128)) return (TValue) (object) ToUuid128();
				if (typeof(TValue) == typeof(Uuid96)) return (TValue) (object) ToUuid96();
				if (typeof(TValue) == typeof(Uuid80)) return (TValue) (object) ToUuid80();
				if (typeof(TValue) == typeof(Uuid64)) return (TValue) (object) ToUuid64();
				if (typeof(TValue) == typeof(NodaTime.Instant)) return (TValue) (object) ToInstant();
				if (typeof(TValue) == typeof(NodaTime.Duration)) return (TValue) (object) ToDuration();
			}

			#endregion

			return (TValue?) Bind(typeof(TValue), resolver) ?? defaultValue;
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (type is null || type == typeof(object))
			{
				return ToObject();
			}

			if (type == typeof(string))
			{
				return ToString();
			}

			if (type.IsAssignableTo(typeof(JsonValue)))
			{
				if (type == typeof(JsonValue) || type == typeof(JsonNumber))
				{
					return this;
				}
				throw JsonBindingException.CannotBindJsonNumberToThisType(this, type);
			}

			if (type.IsPrimitive)
			{
				// notes:
				// - enums have the TypeCode of their underlying type (usually Int32)
				// - decimal and DateTime are NOT IsPrimitive !
				switch (System.Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: return ToBoolean();
					case TypeCode.Char: return ToChar();
					case TypeCode.SByte: return ToSByte();
					case TypeCode.Byte: return ToByte();
					case TypeCode.Int16: return ToInt16();
					case TypeCode.UInt16: return ToUInt16();
					case TypeCode.Int32: return ToInt32();
					case TypeCode.UInt32: return ToUInt32();
					case TypeCode.Int64: return ToInt64();
					case TypeCode.UInt64: return ToUInt64();
					case TypeCode.Single: return ToSingle();
					case TypeCode.Double: return ToDouble();
					case TypeCode.Object:
					{
						if (type == typeof(IntPtr))
						{
							return new IntPtr(ToInt64());
						}
					}
						
					// maybe the BCL will find a way... ?
					return Convert.ChangeType(m_value, type, NumberFormatInfo.InvariantInfo);
				}
			}

			if (type == typeof(string))
			{ // return the original number literal
				return ToString();
			}

			if (type.IsEnum)
			{ // Enumeration
				
				// first convert to an integer, since decimal => enum is not supported.
				// note: enums may not use Int32! we have to bind to the UnderlyingType
				// OPTIMIZE: but 99+% do, maybe hot path for int?
				return Enum.ToObject(type, Bind(type.GetEnumUnderlyingType(), resolver)!);
			}

			if (type == typeof(DateTime))
			{ // Number of days since Unix Epoch
				return ToDateTime();
			}

			if (type == typeof(TimeSpan))
			{ // Number of elapsed seconds
				return ToTimeSpan();
			}

			if (typeof(NodaTime.Duration) == type)
			{ // Number of elapsed seconds
				return ToDuration();
			}

			if (typeof(NodaTime.Instant) == type)
			{ // Number of seconds since Unix Epoch
				return ToInstant();
			}

			if (type == typeof(decimal))
			{ // note: this is not a Primitive type
				return ToDecimal();
			}

			if (type == typeof(JsonValue) || type == typeof(JsonNumber))
			{ // this is already a JSON value!
				return this;
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType is not null)
			{ // We already know that we are not null, so simply decode using the underlying type
				Contract.Debug.Assert(nullableType != type);
				return Bind(nullableType, resolver);
			}

			resolver ??= CrystalJson.DefaultResolver;

			// maybe we have a custom binder?
			if (resolver.TryResolveTypeDefinition(type, out var def) && def.CustomBinder is not null)
			{
				return def.CustomBinder(this, type, resolver);
			}

			// cannot bind a number to this type
			throw JsonBindingException.CannotBindJsonNumberToThisType(this, type);
		}

		internal override bool IsSmallValue() => true;

		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable

		/// <inheritdoc />
		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			// We want to keep the original literal intact, in order to maximize the chances of "perfect" round-tripping.
			// -> for example, if the original JSON contained either '42', '42.0', '4.2E1' etc... we should try to output the same token (as long as it was legal JSON)

			if (m_literal is not null)
			{ // we will output the original literal unless we need to do some special formatting...

				if (m_kind == Kind.Double)
				{
					var d = m_value.Double;
					if (double.IsNaN(d) || double.IsInfinity(d))
					{ // delegate the actual formatting to the writer
						writer.WriteValue(d);
						return;
					}
				}
				writer.WriteRaw(m_literal);
				return;
			}

			switch (m_kind)
			{
				case Kind.Signed:
				{
					writer.WriteValue(m_value.Signed); break;
				}
				case Kind.Unsigned:
				{
					writer.WriteValue(m_value.Unsigned); break;
				}
				case Kind.Double:
				{
					writer.WriteValue(m_value.Double); break;
				}
				case Kind.Decimal:
				{
					writer.WriteValue(m_value.Decimal); break;
				}
			}
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			// we have to special case for NaN and +/-Infinity!
			if (m_kind == Kind.Double)
			{
				if (double.IsNaN(m_value.Double))
				{
					return (format is "J" or "j" ? JsonTokens.JavaScriptNaN : JsonTokens.SymbolNaN).TryCopyTo(destination, out charsWritten);
				}
				if (double.IsPositiveInfinity(m_value.Double))
				{
					return (format is "J" or "j" ? JsonTokens.JavaScriptInfinityPos : JsonTokens.SymbolInfinityPos).TryCopyTo(destination, out charsWritten);
				}
				if (double.IsNegativeInfinity(m_value.Double))
				{
					return (format is "J" or "j" ? JsonTokens.JavaScriptInfinityNeg : JsonTokens.SymbolInfinityNeg).TryCopyTo(destination, out charsWritten);
				}
			}

			var literal = m_literal;
			if (literal is not null)
			{ // we will output the original literal unless we need to do some special formatting...
				return literal.TryCopyTo(destination, out charsWritten);
			}

			switch (m_kind)
			{
				case Kind.Signed:
				{
					return m_value.Signed.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
				}
				case Kind.Unsigned:
				{
					return m_value.Unsigned.TryFormat(destination, out charsWritten);
				}
				case Kind.Double:
				{
					return m_value.Double.TryFormat(destination, out charsWritten, "R", CultureInfo.InvariantCulture);
				}
				case Kind.Decimal:
				{
					return m_value.Decimal.TryFormat(destination, out charsWritten, "R", CultureInfo.InvariantCulture);
				}
				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			// we have to special case for NaN and +/-Infinity!
			if (m_kind == Kind.Double)
			{
				if (double.IsNaN(m_value.Double))
				{
					return (format is "J" or "j" ? "Number.NaN"u8 : "NaN"u8).TryCopyTo(destination, out bytesWritten);
				}
				if (double.IsPositiveInfinity(m_value.Double))
				{
					return (format is "J" or "j" ? "Number.POSITIVE_INFINITY"u8 : "Infinity"u8).TryCopyTo(destination, out bytesWritten);
				}
				if (double.IsNegativeInfinity(m_value.Double))
				{
					return (format is "J" or "j" ? "Number.NEGATIVE_INFINITY"u8 : "-Infinity"u8).TryCopyTo(destination, out bytesWritten);
				}
			}

			var literal = m_literal;
			if (literal is not null)
			{ // we will output the original literal unless we need to do some special formatting...

				return JsonEncoding.Utf8NoBom.TryGetBytes(literal, destination, out bytesWritten);
			}

			switch (m_kind)
			{
				case Kind.Signed:
				{
					return m_value.Signed.TryFormat(destination, out bytesWritten, default, CultureInfo.InvariantCulture);
				}
				case Kind.Unsigned:
				{
					return m_value.Unsigned.TryFormat(destination, out bytesWritten);
				}
				case Kind.Double:
				{
					return m_value.Double.TryFormat(destination, out bytesWritten, "R", CultureInfo.InvariantCulture);
				}
				case Kind.Decimal:
				{
					return m_value.Decimal.TryFormat(destination, out bytesWritten, "R", CultureInfo.InvariantCulture);
				}
				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

#endif

		#endregion

		#region ToXXX() ...

		/// <inheritdoc />
		public override string ToString() => this.Literal;

		/// <inheritdoc />
		public override bool ToBoolean(bool _ = false) => m_value.ToBoolean(m_kind);

		/// <inheritdoc />
		public override byte ToByte(byte _ = 0) => m_value.ToByte(m_kind);

		/// <inheritdoc />
		public override sbyte ToSByte(sbyte _ = 0) => m_value.ToSByte(m_kind);

		/// <inheritdoc />
		public override short ToInt16(short _ = 0) => m_value.ToInt16(m_kind);

		/// <inheritdoc />
		public override ushort ToUInt16(ushort  _ = 0) => m_value.ToUInt16(m_kind);

		/// <inheritdoc />
		public override int ToInt32(int _ = 0) => m_value.ToInt32(m_kind);

		/// <inheritdoc />
		public override uint ToUInt32(uint _ = 0) => m_value.ToUInt32(m_kind);

		/// <inheritdoc />
		public override long ToInt64(long _ = 0) => m_value.ToInt64(m_kind);

		/// <inheritdoc />
		public override ulong ToUInt64(ulong _ = 0) => m_value.ToUInt64(m_kind);

		/// <inheritdoc />
		public override float ToSingle(float _ = 0) => m_value.ToSingle(m_kind);

		/// <inheritdoc />
		public override double ToDouble(double _ = 0) => m_value.ToDouble(m_kind);

#if NET5_0_OR_GREATER
		// System.Half does not exist on netstandard2.0
		/// <inheritdoc />
		public override Half ToHalf(Half _ = default) => m_value.ToHalf(m_kind);
#endif

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override Int128 ToInt128(Int128 _ = default) => m_value.ToInt128(m_kind);

		/// <inheritdoc />
		public override UInt128 ToUInt128(UInt128 _ = default) => m_value.ToUInt128(m_kind);

#endif

		/// <inheritdoc />
		public override decimal ToDecimal(decimal _ = 0) => m_value.ToDecimal(m_kind);

		/// <inheritdoc />
		public override Uuid48 ToUuid48(Uuid48 _ = default) => !IsNaN(this) ? Uuid48.FromUInt64(m_value.ToUInt64(m_kind)) : Uuid48.MaxValue;

		/// <summary>Converts this <see cref="JsonNumber"/>, interpreted as the number of seconds since Unix Epoch, into a <see cref="DateTime"/> (UTC)</summary>
		/// <returns><see cref="DateTime"/> (in UTC) equal to epoch(1970-1-1Z) + seconds(value)</returns>
		public override DateTime ToDateTime(DateTime _ = default)
		{
			// By convention, NaN is mapped to DateTime.MaxValue
			double value = ToDouble();
			if (double.IsNaN(value)) return DateTime.MaxValue;

			// DateTime is stored as the number of seconds since Unix Epoch
			const long UNIX_EPOCH_TICKS = 621355968000000000L;
			double ticks = Math.Round(value * NodaTime.NodaConstants.TicksPerSecond, MidpointRounding.AwayFromZero) + UNIX_EPOCH_TICKS;

			// bound checking
			if (ticks >= long.MaxValue) return DateTime.MaxValue;
			if (ticks <= 0) return DateTime.MinValue;

			return new DateTime((long) ticks, DateTimeKind.Utc);
		}

		/// <summary>Converts this <see cref="JsonNumber"/>, interpreted as the number of seconds since Unix Epoch, into a <see cref="DateTimeOffset"/></summary>
		/// <returns><see cref="DateTimeOffset"/> (UTC) equal to epoch(1970-1-1Z) + seconds(value)</returns>
		/// <remarks>Since the original TimeZone information is lost, the result with have a time offset of <see langword="0"/>.</remarks>.
		public override DateTimeOffset ToDateTimeOffset(DateTimeOffset _ = default)
		{
			// By convention, NaN is mapped to DateTime.MaxValue
			double value = ToDouble();
			if (double.IsNaN(value)) return DateTimeOffset.MaxValue;

			// DateTime is stored as the number of seconds since Unix Epoch
			const long UNIX_EPOCH_TICKS = 621355968000000000L;
			double ticks = Math.Round(value * NodaTime.NodaConstants.TicksPerSecond, MidpointRounding.AwayFromZero) + UNIX_EPOCH_TICKS;

			// bound checking
			if (ticks >= long.MaxValue) return DateTimeOffset.MaxValue;
			if (ticks <= 0) return DateTimeOffset.MinValue;

			//note: since we don't have any knowledge of the original TimeZone, we will a time offset of 0 by convention!
			return new DateTimeOffset((long) ticks, TimeSpan.Zero);
		}

		/// <summary>Converts this <see cref="JsonNumber"/>, interpreted as the number of days since Unix Epoch, into a <see cref="DateOnly"/></summary>
		/// <returns><see cref="DateOnly"/> equal to epoch(1970-1-1Z) + days(value)</returns>
		[Pure]
		public override DateOnly ToDateOnly(DateOnly _ = default)
			=> !IsNaN(this) ? new DateOnly(1970, 1, 1).AddDays(ToInt32()) : DateOnly.MaxValue;

		/// <summary>Converts this <see cref="JsonNumber"/>, interpreted as the number of seconds since midnight, into a <see cref="DateOnly"/></summary>
		/// <returns><see cref="TimeOnly"/> (UTC) equal to <see cref="TimeOnly.MinValue"/> + seconds(value)</returns>
		[Pure]
		public override TimeOnly ToTimeOnly(TimeOnly _ = default)
			=> !IsNaN(this) ? TimeOnly.FromTimeSpan(ToTimeSpan()) : TimeOnly.MaxValue;

		/// <inheritdoc />
		public override TimeSpan ToTimeSpan(TimeSpan _ = default)
		{
			// Timespan is encoded as a number of elapsed seconds (decimal number)

			// Convert into BCL ticks
			double ticks = Math.Round(this.ToDouble() * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);

			const double TIME_SPAN_MAX_VALUE_IN_TICKS = 9.2233720368547758E+18d;
			const double TIME_SPAN_MIN_VALUE_IN_TICKS = -9.2233720368547758E+18d;

			// clamp the number of seconds as to not overflow TimeSpan.MaxValue or TimeSpan.MinValue
			if (ticks >= TIME_SPAN_MAX_VALUE_IN_TICKS) return TimeSpan.MaxValue;
			if (ticks <= TIME_SPAN_MIN_VALUE_IN_TICKS) return TimeSpan.MinValue;

			return new TimeSpan((long)ticks);
		}

		private NodaTime.Duration ConvertSecondsToDurationUnsafe(double seconds)
		{
			if (seconds < 100_000_000) // ~1157 days
			{
				// this is small enough to not lose the nanoseconds precision
				return Duration.FromSeconds(seconds);
			}
			
			// BUGBUG: there is a precision loss issue when the number of nanoseconds is too large
			// => System.Double only has 56 bits of mantissa, which means that we cannot represent the original
			//    number of nanoseconds, and must round the last digits
			// We can only attempt to step down to a precision of 1/10_000_000th of a sec (BCL ticks),
			// with the caveat that some Instant values will not round trip to and from JSON.

			// We also try to use System.Decimal, since it works in base 10 which is what we use to represent nanoseconds,
			// whereas double will use base 2 which will add another layer of rounding error

			decimal sec2 = decimal.Parse(this.Literal, CultureInfo.InvariantCulture);
			decimal sec = Math.Truncate(sec2);
			decimal ns = sec2 - sec;
			ns *= 1_000_000_000;
			return Duration.FromSeconds((long) sec2).Plus(Duration.FromNanoseconds((long) ns));
		}

		/// <inheritdoc />
		public override NodaTime.Duration ToDuration(NodaTime.Duration _ = default)
		{
			// Duration is encoded as a number of elapsed seconds (decimal number)

			var seconds = ToDouble();

			// NaN is mapped to "MaxValue"
			if (double.IsNaN(seconds)) return NodaTime.Duration.MaxValue;

			// bound checking
			if (seconds <= -1449551462400d /* MinValue in seconds */) return NodaTime.Duration.MinValue;
			if (seconds >= 1449551462400 /* MaxValue in seconds */) return NodaTime.Duration.MaxValue;

			return ConvertSecondsToDurationUnsafe(seconds);
		}

		/// <inheritdoc />
		public override NodaTime.Instant ToInstant(NodaTime.Instant _ = default)
		{
			// Instant is stored as the number of seconds since Unix Epoch (decimal number)
			var secondsSinceEpoch = ToDouble();

			// NaN is mapped to "MaxValue"
			if (double.IsNaN(secondsSinceEpoch)) return NodaTime.Instant.MaxValue;

			// bound checking
			if (secondsSinceEpoch <= -1449551462400d /* MinValue in seconds */) return NodaTime.Instant.MinValue;
			if (secondsSinceEpoch >= 1449551462400 /* MaxValue in seconds */) return NodaTime.Instant.MaxValue;

			return default(NodaTime.Instant).Plus(ConvertSecondsToDurationUnsafe(secondsSinceEpoch));
		}

		/// <inheritdoc />
		public override char ToChar(char _ = default) => (char) ToInt16();

		/// <inheritdoc />
		public override TEnum ToEnum<TEnum>(TEnum _ = default)
		{
			//BUGBUG: this only works for enums that are backed by int (or smaller) !
			int value = ToInt32();
			var o = Convert.ChangeType(value, typeof(TEnum));
			return (TEnum) o;
		}

		#endregion

		#region IEquatable<...>

		/// <inheritdoc />
		public override bool Equals(object? value)
		{
			if (value is null) return false;
			switch (System.Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Int32: return Equals((int) value);
				case TypeCode.Int64: return Equals((long) value);
				case TypeCode.UInt32: return Equals((int) value);
				case TypeCode.UInt64: return Equals((long) value);
				case TypeCode.Single: return Equals((float) value);
				case TypeCode.Double: return Equals((double) value);
				case TypeCode.Decimal: return Equals((decimal) value);
				case TypeCode.Object:
				{
					if (value is TimeSpan ts) return Equals(ts);
					break;
				}
			}
			return base.Equals(value);
		}

		/// <inheritdoc />
		public override bool Equals(JsonValue? value) => value switch
		{
			JsonNumber num => Equals(num),
			JsonString str => Equals(str),
			JsonBoolean b => Equals(b),
			JsonDateTime dt => Equals(dt),
			_ => false
		};

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other is JsonNumber num && Equals(num);

		public bool StrictEquals(JsonNumber? other) => other is not null && Equals(other);

		/// <inheritdoc />
		public override bool ValueEquals<TValue>(TValue? value, IEqualityComparer<TValue>? comparer = null) where TValue : default
		{
			if (default(TValue) is null)
			{
				if (value is null) return false;

				if (typeof(TValue) == typeof(int?)) return comparer?.Equals((TValue) (object) ToInt32(), value) ?? Equals((int) (object) value);
				if (typeof(TValue) == typeof(long?)) return comparer?.Equals((TValue) (object) ToInt64(), value) ?? Equals((long) (object) value);
				if (typeof(TValue) == typeof(uint?)) return comparer?.Equals((TValue) (object) ToUInt32(), value) ?? Equals((uint) (object) value);
				if (typeof(TValue) == typeof(ulong?)) return comparer?.Equals((TValue) (object) ToUInt64(), value) ?? Equals((ulong) (object) value);
				if (typeof(TValue) == typeof(float?)) return comparer?.Equals((TValue) (object) ToSingle(), value) ?? Equals((float) (object) value);
				if (typeof(TValue) == typeof(double?)) return comparer?.Equals((TValue) (object) ToDouble(), value) ?? Equals((double) (object) value);
				if (typeof(TValue) == typeof(short?)) return comparer?.Equals((TValue) (object) ToInt16(), value) ?? Equals((short) (object) value);
				if (typeof(TValue) == typeof(ushort?)) return comparer?.Equals((TValue) (object) ToUInt16(), value) ?? Equals((ushort) (object) value);
				if (typeof(TValue) == typeof(decimal?)) return comparer?.Equals((TValue) (object) ToDecimal(), value) ?? Equals((decimal) (object) value);
#if NET5_0_OR_GREATER
				// System.Half does not exist on netstandard2.0
				if (typeof(TValue) == typeof(Half?)) return comparer?.Equals((TValue) (object) ToHalf(), value) ?? Equals((Half) (object) value);
#endif
#if NET8_0_OR_GREATER
				if (typeof(TValue) == typeof(Int128?)) return comparer?.Equals((TValue) (object) ToInt128(), value) ?? Equals((Int128) (object) value);
				if (typeof(TValue) == typeof(UInt128?)) return comparer?.Equals((TValue) (object) ToUInt128(), value) ?? Equals((UInt128) (object) value);
#endif

				if (value is JsonValue j) return Equals(j);
			}
			else
			{
				if (typeof(TValue) == typeof(int)) return comparer?.Equals((TValue) (object) ToInt32(), value) ?? Equals((int) (object) value!);
				if (typeof(TValue) == typeof(long)) return comparer?.Equals((TValue) (object) ToInt64(), value) ?? Equals((long) (object) value!);
				if (typeof(TValue) == typeof(uint)) return comparer?.Equals((TValue) (object) ToUInt32(), value) ?? Equals((uint) (object) value!);
				if (typeof(TValue) == typeof(ulong)) return comparer?.Equals((TValue) (object) ToUInt64(), value) ?? Equals((ulong) (object) value!);
				if (typeof(TValue) == typeof(float)) return comparer?.Equals((TValue) (object) ToSingle(), value) ?? Equals((float) (object) value!);
				if (typeof(TValue) == typeof(double)) return comparer?.Equals((TValue) (object) ToDouble(), value) ?? Equals((double) (object) value!);
				if (typeof(TValue) == typeof(short)) return comparer?.Equals((TValue) (object) ToInt16(), value) ?? Equals((short) (object) value!);
				if (typeof(TValue) == typeof(ushort)) return comparer?.Equals((TValue) (object) ToUInt16(), value) ?? Equals((ushort) (object) value!);
				if (typeof(TValue) == typeof(decimal)) return comparer?.Equals((TValue) (object) ToDecimal(), value) ?? Equals((decimal) (object) value!);
#if NET5_0_OR_GREATER
				// System.Half does not exist on netstandard2.0
				if (typeof(TValue) == typeof(Half)) return comparer?.Equals((TValue) (object) ToHalf(), value) ?? Equals((Half) (object) value!);
#endif
#if NET8_0_OR_GREATER
				if (typeof(TValue) == typeof(Int128)) return comparer?.Equals((TValue) (object) ToInt128(), value) ?? Equals((Int128) (object) value!);
				if (typeof(TValue) == typeof(UInt128)) return comparer?.Equals((TValue) (object) ToUInt128(), value) ?? Equals((UInt128) (object) value!);
#endif
			}

			return false;
		}

		/// <inheritdoc />
		public bool Equals(JsonNumber? value) => value is not null && Number.Equals(in m_value, m_kind, in value.m_value, value.m_kind);

		/// <inheritdoc />
		public bool Equals(JsonString? value)
		{
			if (value is null) return false;

			var text = value.Value;
			if (string.IsNullOrEmpty(text)) return false;

			// quick check if the first char is valid for a number
			var c = text[0];
			if (!char.IsDigit(c) && c != '-' && c != '+') return false;

			switch (m_kind)
			{
				case Kind.Decimal:
				{
					return value.TryConvertToDecimal(out var x) && m_value.Decimal == x;
				}
				case Kind.Double:
				{
					return value.TryConvertToDouble(out var x) && m_value.Double == x;
				}
				case Kind.Signed:
				{
					return value.TryConvertToInt64(out var x) && m_value.Signed == x;
				}
				case Kind.Unsigned:
				{
					return value.TryConvertToUInt64(out var x) && m_value.Unsigned == x;
				}
			}

			return false;
		}

		/// <inheritdoc />
		public bool Equals(JsonBoolean? value) => value is not null && ToBoolean() == value.Value;

		/// <inheritdoc />
		public bool Equals(JsonDateTime? value) => value is not null && ToDouble() == value.ToDouble();

		/// <inheritdoc />
		public bool Equals(short value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == (ulong) value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(ushort value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => m_value.Unsigned == value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(int value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == (ulong) value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(uint value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => m_value.Unsigned == value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(long value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == (ulong) value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(ulong value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == (long) value,
			Kind.Unsigned => m_value.Unsigned == value,
			_ => false
		};

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public bool Equals(Int128 value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == (decimal) value,
			Kind.Double => m_value.Double == (double) value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == (ulong) value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(UInt128 value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == (decimal) value,
			Kind.Double => m_value.Double == (double) value,
			Kind.Signed => m_value.Signed == (long) value,
			Kind.Unsigned => m_value.Unsigned == value,
			_ => false
		};

#endif

		/// <inheritdoc />
		public bool Equals(Uuid48 value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == value.ToUInt64(),
			Kind.Double => m_value.Double == value.ToUInt64(),
			Kind.Signed => m_value.Signed == value.ToInt64(),
			Kind.Unsigned => m_value.Unsigned == value.ToUInt64(),
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(float value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double.Equals(value),
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(double value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double.Equals(value),
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == value,
			_ => false
		};

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public bool Equals(Half value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == (decimal) value,
			Kind.Double => m_value.Double == (double) value,
			Kind.Signed => m_value.Signed == (double) value,
			Kind.Unsigned => Half.IsPositive(value) && m_value.Unsigned == (double) value,
			_ => false
		};
#else
#if NET5_0_OR_GREATER
		// System.Half does not exist on netstandard2.0
		public bool Equals(Half value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == (decimal) (double) value,
			Kind.Double => m_value.Double == (double) value,
			Kind.Signed => m_value.Signed == (double) value,
			Kind.Unsigned => (double) value >= 0 && m_value.Unsigned == (double) value,
			_ => false
		};
#endif
#endif

		/// <inheritdoc />
		public bool Equals(decimal value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == value,
			Kind.Double => new decimal(m_value.Double) == value,
			Kind.Signed => new decimal(m_value.Signed) == value,
			Kind.Unsigned => value >= 0 && new decimal(m_value.Unsigned) == value,
			_ => false
		};

		/// <inheritdoc />
		public bool Equals(TimeSpan value) => ToDouble() == value.TotalSeconds;

		/// <inheritdoc />
		public override int GetHashCode()
		{
			// note: we have to return the same hash code for 1, 1UL, 1.0d or 1m !
			
			switch(m_kind)
			{
				case Kind.Decimal:
				{
					decimal d = m_value.Decimal;
					long l = (long) d;
					return (l == d) ? l.GetHashCode() : d.GetHashCode();
				}
				case Kind.Double:
				{
					double d = m_value.Double;
					if (d < 0)
					{
						long x = (long)d;
						if (x == d) return x.GetHashCode();
					}
					else
					{
						ulong x = (ulong)d;
						if (x == d) return x.GetHashCode();
					}
					return d.GetHashCode();
				}
				case Kind.Signed: return m_value.Signed.GetHashCode();
				case Kind.Unsigned: return m_value.Unsigned.GetHashCode();
				default: return -1;
			}
		}

		#endregion

		#region IComparable<...>

		/// <inheritdoc />
		public override int CompareTo(JsonValue? other) => other switch
		{
			JsonNumber jn => CompareTo(jn),
			JsonString js => CompareTo(js),
			_ => base.CompareTo(other)
		};

		/// <inheritdoc />
		public int CompareTo(JsonNumber? other) => other switch
		{
			null => +1,
			_ => Number.CompareTo(in m_value, m_kind, in other.m_value, other.m_kind)
		};

		/// <inheritdoc />
		public int CompareTo(int value) => m_value.CompareTo(m_kind, value);

		/// <inheritdoc />
		public int CompareTo(long value) => m_value.CompareTo(m_kind, value);

		/// <inheritdoc />
		public int CompareTo(uint value) => m_value.CompareTo(m_kind, value);

		/// <inheritdoc />
		public int CompareTo(ulong value) => m_value.CompareTo(m_kind, value);

		/// <inheritdoc />
		public int CompareTo(float value) => m_value.CompareTo(m_kind, value);

		/// <inheritdoc />
		public int CompareTo(double value) => m_value.CompareTo(m_kind, value);

		public int CompareTo(JsonString? other)
		{
			if (other is null) return +1;

			var text = other.Value;
			if (string.IsNullOrEmpty(text)) return +1; //REVIEW: ??

			// shortcut if the first character is not valid for a number
			var c = text[0];
			if (char.IsDigit(c) || c == '-' || c == '+')
			{
				switch (m_kind)
				{
					case Kind.Decimal:
					{
						if (other.TryConvertToDecimal(out var x))
						{
							return m_value.Decimal.CompareTo(x);
						}
						break;
					}
					case Kind.Double:
					{
						if (other.TryConvertToDouble(out var x))
						{
							return m_value.Double.CompareTo(x);
						}
						break;
					}
					case Kind.Signed:
					{
						if (other.TryConvertToInt64(out var x))
						{
							return m_value.Signed.CompareTo(x);
						}
						break;
					}
					case Kind.Unsigned:
					{
						if (other.TryConvertToUInt64(out var x))
						{
							return m_value.Unsigned.CompareTo(x);
						}
						break;
					}
				}
			}

			// compare using string representation
			return string.CompareOrdinal(this.ToString(), other.Value);
		}

		#endregion

		#region Arithmetic operators

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(int value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(long value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(uint value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(ulong value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(float value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(double value) => Create(value);

		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(decimal value) => Create(value);

#if NET5_0_OR_GREATER
		// System.Half does not exist on netstandard2.0
		/// <summary>Returns the equivalent <see cref="JsonNumber"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonNumber(System.Half value) => Create(value);
#endif

		/// <summary>Tests if two <see cref="JsonNumber"/> are considered equal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(JsonNumber? left, JsonNumber? right)
			=> left?.Equals(right) ?? right is null;

		/// <summary>Tests if two <see cref="JsonNumber"/> are considered not equal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(JsonNumber? left, JsonNumber? right)
			=> !(left?.Equals(right) ?? right is null);

		/// <summary>Tests if a <see cref="JsonNumber"/> is less than the other</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(JsonNumber? left, JsonNumber? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) < 0;

		/// <summary>Tests if a <see cref="JsonNumber"/> is less than or equal to the other</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(JsonNumber? left, JsonNumber? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) <= 0;

		/// <summary>Tests if a <see cref="JsonNumber"/> is greater than the other</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(JsonNumber? left, JsonNumber? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) > 0;

		/// <summary>Tests if a <see cref="JsonNumber"/> is greater than or equal to the other</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(JsonNumber? left, JsonNumber? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) >= 0;

		/// <summary>Adds two <see cref="JsonNumber"/> together</summary>
		public static JsonNumber operator +(JsonNumber value) => value;

		/// <summary>Substracts a <see cref="JsonNumber"/> from another number</summary>
		public static JsonNumber operator -(JsonNumber value) => MinusOne.Multiply(value);

		/// <summary>Tests if a <see cref="JsonNumber"/> and an integer are considered equal</summary>
		public static bool operator ==(JsonNumber? number, long value) => number is not null && number.Equals(value);

		/// <summary>Tests if a <see cref="JsonNumber"/> and an integer are not considered equal</summary>
		public static bool operator !=(JsonNumber? number, long value) => number is null || !number.Equals(value);

		/// <summary>Tests if a <see cref="JsonNumber"/> is less than an integer</summary>
		public static bool operator <(JsonNumber? number, long value) => number is not null && number.CompareTo(value) < 0;

		/// <summary>Tests if a <see cref="JsonNumber"/> is less than or equal to an integer</summary>
		public static bool operator <=(JsonNumber? number, long value) => number is not null && number.CompareTo(value) <= 0;

		/// <summary>Tests if a <see cref="JsonNumber"/> is greater than an integer</summary>
		public static bool operator >(JsonNumber? number, long value) => number is not null && number.CompareTo(value) > 0;

		/// <summary>Tests if a <see cref="JsonNumber"/> is greater than or equal to an integer</summary>
		public static bool operator >=(JsonNumber? number, long value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, ulong value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, ulong value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, float value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, float value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, float value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, float value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, float value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, float value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, double value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, double value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, double value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, double value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, double value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, double value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, decimal value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, decimal value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) >= 0;

		/// <summary>Adds this number with another number</summary>
		[Pure]
		public JsonNumber Plus(JsonNumber number)
		{
			// x + 0 == x
			if (number.m_value.IsZero(m_kind)) return this;

			var kind = m_kind;
			var value = m_value;
			//  0 + y == y
			if (m_value.IsZero(m_kind)) return number;

			Number.Add(ref value, ref kind, in number.m_value, number.m_kind);
			return Create(value, kind);
		}

		/// <summary>Subtracts a number from this number</summary>
		[Pure]
		public JsonNumber Minus(JsonNumber number)
		{
			// x - 0 == x
			if (number.m_value.IsZero(m_kind)) return this;

			var kind = m_kind;
			var value = m_value;

			Number.Subtract(ref value, ref kind, number.m_value, number.m_kind);
			return Create(value, kind);
		}

		/// <summary>Adds two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, JsonNumber right) => left.Plus(right);

		/// <summary>Adds two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, long right)
		{
			if (right == 0) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Adds two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, ulong right)
		{
			if (right == 0) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Adds two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, double right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Adds two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, float right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Adds two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, decimal right)
		{
			if (right == 0) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Adds one to a number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator ++(JsonNumber left) => left.Plus(One);

		/// <summary>Subtracts a number from another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, JsonNumber right) => left.Minus(right);

		/// <summary>Subtracts a number from another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, long right)
		{
			if (right == 0) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Subtracts a number from another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, ulong right)
		{
			if (right == 0) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Subtracts a number from another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, double right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Subtracts a number from another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, float right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Subtracts a number from another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, decimal right)
		{
			if (right == 0) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Subtracts one from a number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator --(JsonNumber left) => left.Plus(MinusOne);

		/// <summary>Multiply this number with another number</summary>
		[Pure]
		public JsonNumber Multiply(JsonNumber number)
		{
			// x * 1 == x
			if (m_value.IsOne(m_kind)) return number;

			var kind = m_kind;
			var value = m_value;

			Number.Multiply(ref value, ref kind, in number.m_value, number.m_kind);
			return Create(value, kind);
		}

		/// <summary>Multiply two numbers together</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator *(JsonNumber left, JsonNumber right) => left.Multiply(right);

		/// <summary>Multiply two numbers together</summary>
		[Pure]
		public static JsonNumber operator *(JsonNumber left, long right)
		{
			if (right == 1) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Multiply two numbers together</summary>
		[Pure]
		public static JsonNumber operator *(JsonNumber left, ulong right)
		{
			if (right == 1) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Multiply two numbers together</summary>
		[Pure]
		public static JsonNumber operator *(JsonNumber left, double right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Multiply two numbers together</summary>
		[Pure]
		public static JsonNumber operator *(JsonNumber left, float right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Multiply two numbers together</summary>
		[Pure]
		public static JsonNumber operator *(JsonNumber left, decimal right)
		{
			if (right == 1) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Divides this number by another number</summary>
		[Pure]
		public JsonNumber Divide(JsonNumber number)
		{
			// x / 1 == x
			if (m_value.IsOne(m_kind)) return number;

			var kind = m_kind;
			var value = m_value;

			Number.Divide(ref value, ref kind, in number.m_value, number.m_kind);
			return Create(value, kind);
		}

		/// <summary>Divides a number by another number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator /(JsonNumber left, JsonNumber right) => left.Divide(right);

		/// <summary>Divides a number by another number</summary>
		[Pure]
		public static JsonNumber operator /(JsonNumber left, long right)
		{
			if (right == 1) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Divides a number by another number</summary>
		[Pure]
		public static JsonNumber operator /(JsonNumber left, ulong right)
		{
			if (right == 1) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Divides a number by another number</summary>
		[Pure]
		public static JsonNumber operator /(JsonNumber left, double right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Divides a number by another number</summary>
		[Pure]
		public static JsonNumber operator /(JsonNumber left, float right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Divides a number by another number</summary>
		[Pure]
		public static JsonNumber operator /(JsonNumber left, decimal right)
		{
			if (right == 1) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Create(value, kind);
		}

		/// <summary>Returns <see cref="JsonNumber.Zero"/></summary>
		public static JsonNumber AdditiveIdentity => JsonNumber.Zero;

		/// <summary>Returns <see cref="JsonNumber.One"/></summary>
		public static JsonNumber MultiplicativeIdentity => JsonNumber.One;

		#endregion

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			//TODO: if javascript we have to special case for thins like NaN, infinities, ... !
			return this.Literal;
		}

		#region ISliceSerializable...

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteStringAscii(m_literal);
		}

		#endregion

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public new static JsonNumber Parse(string s, IFormatProvider? provider) => CrystalJsonParser.ParseJsonNumber(s) ?? Zero;

		/// <inheritdoc />
		public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonNumber result)
		{
			try
			{
				result = CrystalJsonParser.ParseJsonNumber(s);
				return result is not null;
			}
			catch (Exception)
			{ // not a valid number
				result = null;
				return false;
			}
		}

		/// <inheritdoc />
		public new static JsonNumber Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => CrystalJsonParser.ParseJsonNumber(s) ?? Zero;

		/// <inheritdoc />
		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonNumber result)
		{
			try
			{
				result = CrystalJsonParser.ParseJsonNumber(s.ToString());
				return result is not null;
			}
			catch (Exception)
			{ // not a valid number
				result = null;
				return false;
			}
		}

		static JsonNumber System.Numerics.INumberBase<JsonNumber>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => throw new NotSupportedException();

		static JsonNumber System.Numerics.INumberBase<JsonNumber>.Parse(string s, NumberStyles style, IFormatProvider? provider) => throw new NotSupportedException();

		static bool System.Numerics.INumberBase<JsonNumber>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out JsonNumber result) => throw new NotSupportedException();

		static bool System.Numerics.INumberBase<JsonNumber>.TryParse(string? s, NumberStyles style, IFormatProvider? provider, out JsonNumber result) => throw new NotSupportedException();

		static int System.Numerics.INumberBase<JsonNumber>.Radix => 2;
		// note: this is wrong if we store a decimal, which has Radix == 10, but we can't really do anything here

		static bool System.Numerics.INumberBase<JsonNumber>.IsNegative(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => long.IsNegative(value.m_value.Signed),
			Kind.Double => double.IsNegative(value.m_value.Double),
			Kind.Decimal => decimal.IsNegative(value.m_value.Decimal),
			_ => false,
		};

#endif

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Abs(JsonNumber value) => value.IsNegative ? value.Multiply(MinusOne) : value;

		/// <inheritdoc />
		public static bool IsZero(JsonNumber value) => value.m_value.IsZero(value.m_kind);

		/// <inheritdoc />
		public static bool IsPositive(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => value.m_value.Signed >= 0,
			Kind.Unsigned => true,
#if NET8_0_OR_GREATER
			Kind.Double => double.IsPositive(value.m_value.Double),
			Kind.Decimal => decimal.IsPositive(value.m_value.Decimal),
#else
			Kind.Double => value.m_value.Double >= 0,
			Kind.Decimal => value.m_value.Decimal >= 0,
#endif
			_ => false,
		};

		/// <inheritdoc />
		public static bool IsInteger(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed or Kind.Unsigned => true,
#if NET8_0_OR_GREATER
			Kind.Double => double.IsInteger(value.m_value.Double),
			Kind.Decimal => decimal.IsInteger(value.m_value.Decimal),
#else
#if NET5_0_OR_GREATER
			Kind.Double => double.IsFinite(value.m_value.Double) && Math.Truncate(value.m_value.Double) == value.m_value.Double,
#else
			// double.IsFinite is not on netstandard2.0
			Kind.Double => !double.IsNaN(value.m_value.Double) && !double.IsInfinity(value.m_value.Double) && Math.Truncate(value.m_value.Double) == value.m_value.Double,
#endif
			Kind.Decimal => decimal.Truncate(value.m_value.Decimal) == value.m_value.Decimal,
#endif
			_ => false,
		};

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public static bool IsEvenInteger(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => long.IsEvenInteger(value.m_value.Signed),
			Kind.Unsigned => ulong.IsEvenInteger(value.m_value.Unsigned),
			Kind.Double => double.IsEvenInteger(value.m_value.Double),
			Kind.Decimal => decimal.IsEvenInteger(value.m_value.Decimal),
			_ => false,
		};

		/// <inheritdoc />
		public static bool IsOddInteger(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => long.IsOddInteger(value.m_value.Signed),
			Kind.Unsigned => ulong.IsOddInteger(value.m_value.Unsigned),
			Kind.Double => double.IsOddInteger(value.m_value.Double),
			Kind.Decimal => decimal.IsOddInteger(value.m_value.Decimal),
			_ => false,
		};

		/// <inheritdoc />
		public static bool IsRealNumber(JsonNumber value) => value.m_kind switch
		{
			Kind.Double => double.IsRealNumber(value.m_value.Double), // returns false for NaN
			Kind.Decimal => true, // decimal.IsRealNumber always returns true
			_ => false,
		};
		
		/// <inheritdoc />
		public static bool IsFinite(JsonNumber value) => value.m_kind switch
		{
			Kind.Double => double.IsFinite(value.m_value.Double),
			Kind.Signed or Kind.Unsigned or Kind.Decimal => true,
			_ => false,
		};

		static bool System.Numerics.INumberBase<JsonNumber>.IsImaginaryNumber(JsonNumber value) => false;

		static bool System.Numerics.INumberBase<JsonNumber>.IsCanonical(JsonNumber value) => true;

		static bool System.Numerics.INumberBase<JsonNumber>.IsComplexNumber(JsonNumber value) => false;

#endif

		/// <inheritdoc />
		public static bool IsNaN(JsonNumber value) => value.m_kind == Kind.Double && double.IsNaN(value.m_value.Double);

		/// <inheritdoc />
		public static bool IsInfinity(JsonNumber value) => value.m_kind == Kind.Double && double.IsInfinity(value.m_value.Double);

		/// <inheritdoc />
		public static bool IsPositiveInfinity(JsonNumber value) => value.m_kind == Kind.Double && double.IsPositiveInfinity(value.m_value.Double);

		/// <inheritdoc />
		public static bool IsNegativeInfinity(JsonNumber value) => value.m_kind == Kind.Double && double.IsNegativeInfinity(value.m_value.Double);

		/// <inheritdoc />
		public static bool IsNormal(JsonNumber value) => value.m_kind switch
		{
#if NET5_0_OR_GREATER
			Kind.Double => double.IsNormal(value.m_value.Double),
#else
			// double.IsNormal is not on netstandard2.0: finite, non-zero, and at least the smallest normal double
			Kind.Double => !double.IsInfinity(value.m_value.Double) && Math.Abs(value.m_value.Double) >= 2.2250738585072014E-308,
#endif
			_ => !IsZero(value),
		};

		/// <inheritdoc />
#if NET5_0_OR_GREATER
		public static bool IsSubnormal(JsonNumber value) => value.m_kind == Kind.Double && double.IsSubnormal(value.m_value.Double);
#else
		// double.IsSubnormal is not on netstandard2.0: non-zero and strictly below the smallest normal double
		public static bool IsSubnormal(JsonNumber value) => value.m_kind == Kind.Double && value.m_value.Double != 0 && Math.Abs(value.m_value.Double) < 2.2250738585072014E-308;
#endif

		/// <inheritdoc />
		public static JsonNumber MaxMagnitude(JsonNumber x, JsonNumber y) => Abs(x).CompareTo(Abs(y)) < 0 ? y : x;

		/// <inheritdoc />
		public static JsonNumber MaxMagnitudeNumber(JsonNumber x, JsonNumber y) => IsNaN(x) ? y : IsNaN(y) ? x : Abs(x).CompareTo(Abs(y)) < 0 ? y : x;

		/// <inheritdoc />
		public static JsonNumber MinMagnitude(JsonNumber x, JsonNumber y) => Abs(x).CompareTo(Abs(y)) > 0 ? y : x;

		/// <inheritdoc />
		public static JsonNumber MinMagnitudeNumber(JsonNumber x, JsonNumber y) => IsNaN(x) ? y : IsNaN(y) ? x : Abs(x).CompareTo(Abs(y)) > 0 ? y : x;

#if NET8_0_OR_GREATER

		public static bool TryConvertFrom<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result)
			where TOther : System.Numerics.INumberBase<TOther>
		{
			//note: this will be optimized by the JIT in Release builds, but will be VERY slow in DEBUG builds
			if (typeof(TOther) == typeof(int))
			{
				result = Create((int) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(long))
			{
				result = Create((long) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(double))
			{
				result = Create((double) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(float))
			{
				result = Create((float) (object) value);
				return true;
			}
#if NET8_0_OR_GREATER
			if (typeof(TOther) == typeof(Half))
			{
				result = Create((Half) (object) value);
				return true;
			}
#endif
			if (typeof(TOther) == typeof(decimal))
			{
				result = Create((decimal) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(short))
			{
				result = Create((short) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(sbyte))
			{
				result = Create((sbyte) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(byte))
			{
				result = Create((byte) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(ulong))
			{
				result = Create((ulong) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(uint))
			{
				result = Create((uint) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(ushort))
			{
				result = Create((ushort) (object) value);
				return true;
			}

			result = null;
			return false;
		}

		/// <inheritdoc />
		public static bool TryConvertFromChecked<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result)
			where TOther : System.Numerics.INumberBase<TOther>
			=> TryConvertFrom<TOther>(value, out result);

		/// <inheritdoc />
		public static bool TryConvertFromSaturating<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result)
			where TOther : System.Numerics.INumberBase<TOther>
			=> TryConvertFrom<TOther>(value, out result);

		/// <inheritdoc />
		public static bool TryConvertFromTruncating<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result)
			where TOther : System.Numerics.INumberBase<TOther>
			=> TryConvertFrom<TOther>(value, out result);

		/// <inheritdoc />
		public static bool TryConvertToChecked<TOther>(JsonNumber value, [MaybeNullWhen(false)] out TOther result)
			where TOther : System.Numerics.INumberBase<TOther>
			=>
			value.m_kind switch
			{
				Kind.Signed => TOther.TryConvertFromChecked(value.m_value.Signed, out result),
				Kind.Unsigned => TOther.TryConvertFromChecked(value.m_value.Unsigned, out result),
				Kind.Decimal => TOther.TryConvertFromChecked(value.m_value.Decimal, out result),
				Kind.Double => TOther.TryConvertFromChecked(value.m_value.Double, out result),
				_ => throw new InvalidOperationException()
			};

		/// <inheritdoc />
		public static bool TryConvertToSaturating<TOther>(JsonNumber value, [MaybeNullWhen(false)] out TOther result)
			where TOther : System.Numerics.INumberBase<TOther>
			=>
			value.m_kind switch
			{
				Kind.Signed => TOther.TryConvertFromSaturating(value.m_value.Signed, out result),
				Kind.Unsigned => TOther.TryConvertFromSaturating(value.m_value.Unsigned, out result),
				Kind.Decimal => TOther.TryConvertFromSaturating(value.m_value.Decimal, out result),
				Kind.Double => TOther.TryConvertFromSaturating(value.m_value.Double, out result),
				_ => throw new InvalidOperationException()
			};

		/// <inheritdoc />
		public static bool TryConvertToTruncating<TOther>(JsonNumber value, [MaybeNullWhen(false)] out TOther result)
			where TOther : System.Numerics.INumberBase<TOther>
			=>
			value.m_kind switch
			{
				Kind.Signed => TOther.TryConvertFromTruncating(value.m_value.Signed, out result),
				Kind.Unsigned => TOther.TryConvertFromTruncating(value.m_value.Unsigned, out result),
				Kind.Decimal => TOther.TryConvertFromTruncating(value.m_value.Decimal, out result),
				Kind.Double => TOther.TryConvertFromTruncating(value.m_value.Double, out result),
				_ => throw new InvalidOperationException()
			};

#endif

	}

}
