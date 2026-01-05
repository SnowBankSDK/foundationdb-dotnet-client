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

namespace SnowBank.Data.Json
{
#if NET8_0_OR_GREATER
	using System.Globalization;
	using System.Numerics;
#endif

	public abstract partial class JsonValue
#if NET8_0_OR_GREATER
		: INumberBase<JsonValue>, IComparisonOperators<JsonValue, JsonValue, bool>
#endif
	{

		#region Dark Magic!

		// In order to be able to write `if (obj["Hello"]) ... else ...`, then JsonValue must implement or operators `op_true` and `op_false`
		// In order to be able to write `if (obj["Hello"] && obj["World"]) ....` (resp. `||`) then JsonValue must implement the operator `op_&` (resp: `op_|`),
		// and it must return a JsonValue instance (which will then be passed to `op_true`/`op_false` to produce a boolean).

		/// <summary>Test if this value is logically equivalent to <see langword="true"/></summary>
		public static bool operator true(JsonValue? obj) => obj is not null && obj.ToBoolean();

		/// <summary>Test if this value is logically equivalent to <see langword="false"/></summary>
		public static bool operator false(JsonValue? obj) => obj is null || obj.ToBoolean();

		/// <summary>Perform a logical AND operation between two JSON values</summary>
		public static JsonValue operator &(JsonValue? left, JsonValue? right)
		{
			//REVIEW:TODO: maybe handle the cases were both values are JSON Numbers, and perform a bit-wise AND instead?
			return left is not null && right is not null && left.ToBoolean() && right.ToBoolean() ? JsonBoolean.True : JsonBoolean.False;
		}

		/// <summary>Perform a logical OR operation between two JSON values</summary>
		public static JsonValue operator |(JsonValue? left, JsonValue? right)
		{
			//REVIEW:TODO: maybe handle the cases were both values are JSON Numbers, and perform a bit-wise OR instead?
			return (left is not null && left.ToBoolean()) || (right is not null && right.ToBoolean()) ? JsonBoolean.True : JsonBoolean.False;
		}

		#endregion

#if NET8_0_OR_GREATER


		#region IComparisonOperators...

		//note: it is unknown currently if it is safe to implement "==" and "!=" implicity, because all "if (obj == null)" would be intercepted, and change their behavior
		// => for the moment, we only implement then on the INumber side of things.

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IEqualityOperators<JsonValue, JsonValue, bool>.operator ==(JsonValue? left, JsonValue? right)
			=> (left ?? JsonNull.Null).Equals(right ?? JsonNull.Null);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IEqualityOperators<JsonValue, JsonValue, bool>.operator !=(JsonValue? left, JsonValue? right)
			=> !(left ?? JsonNull.Null).Equals(right ?? JsonNull.Null);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IComparisonOperators<JsonValue, JsonValue, bool>.operator <(JsonValue? left, JsonValue? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) < 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IComparisonOperators<JsonValue, JsonValue, bool>.operator <=(JsonValue? left, JsonValue? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) <= 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IComparisonOperators<JsonValue, JsonValue, bool>.operator >(JsonValue? left, JsonValue? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) > 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IComparisonOperators<JsonValue, JsonValue, bool>.operator >=(JsonValue? left, JsonValue? right)
			=> (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) >= 0;

		#endregion

		#region INumberBase...

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.Zero => JsonNumber.Zero;

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.One => JsonNumber.One;

		/// <inheritdoc />
		static int INumberBase<JsonValue>.Radix => 2;
		//note: the radix is 10 for "decimal"

		/// <inheritdoc />
		static JsonValue IAdditionOperators<JsonValue, JsonValue, JsonValue>.operator +(JsonValue left, JsonValue right) =>
			(left, right) switch
			{
				(JsonNumber x, JsonNumber y) => x.Plus(y),
				(JsonNumber x, JsonNull) => x,
				(JsonNull, JsonNumber y) => y,
				(JsonNull x, JsonNull y) => x.CompareTo(y) <= 0 ? x : y,
				_ => throw new NotSupportedException($"Cannot add a JSON {left.Type} to a JSON {right.Type}")
			};

		/// <inheritdoc />
		static JsonValue ISubtractionOperators<JsonValue, JsonValue, JsonValue>.operator -(JsonValue left, JsonValue right) =>
			(left, right) switch
			{
				(JsonNumber x, JsonNumber y) => x.Minus(y),
				(JsonNumber x, JsonNull) => x,
				(JsonNull, JsonNumber y) => -y,
				(JsonNull x, JsonNull y) => x.CompareTo(y) <= 0 ? x : y,
				_ => throw new NotSupportedException($"Cannot subtract a JSON {right.Type} from a JSON {left.Type}")
			};

		/// <inheritdoc />
		static JsonValue IAdditiveIdentity<JsonValue, JsonValue>.AdditiveIdentity => JsonNumber.Zero;

		/// <inheritdoc />
		static JsonValue IIncrementOperators<JsonValue>.operator ++(JsonValue value) =>
			value is JsonNumber num ? ++num : throw new NotSupportedException($"Cannot increment JSON {value.Type}");

		/// <inheritdoc />
		static JsonValue IDecrementOperators<JsonValue>.operator --(JsonValue value) =>
			value is JsonNumber num ? --num : throw new NotSupportedException($"Cannot decrement JSON {value.Type}");

		/// <inheritdoc />
		static JsonValue IMultiplicativeIdentity<JsonValue, JsonValue>.MultiplicativeIdentity => JsonNumber.One;

		/// <inheritdoc />
		static JsonValue IMultiplyOperators<JsonValue, JsonValue, JsonValue>.operator *(JsonValue left, JsonValue right) =>
			(left, right) switch
			{
				(JsonNumber x, JsonNumber y) => x * y,
				(JsonNumber, JsonNull) => JsonNumber.NaN,
				(JsonNull, JsonNumber) => JsonNumber.NaN,
				_ => throw new NotSupportedException($"Cannot multiply a JSON {left.Type} by a JSON {right.Type}")
			};

		/// <inheritdoc />
		static JsonValue IDivisionOperators<JsonValue, JsonValue, JsonValue>.operator /(JsonValue left, JsonValue right) =>
			(left, right) switch
			{
				(JsonNumber x, JsonNumber y) => x / y,
				(JsonNumber, JsonNull) => JsonNumber.NaN,
				(JsonNull, JsonNumber) => JsonNumber.NaN,
				_ => throw new NotSupportedException($"Cannot divide a JSON {left.Type} by a JSON {right.Type}")
			};

		/// <inheritdoc />
		static JsonValue IUnaryNegationOperators<JsonValue, JsonValue>.operator -(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => -x,
				JsonNull n => n,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static JsonValue IUnaryPlusOperators<JsonValue, JsonValue>.operator +(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => x,
				JsonNull n => n,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsZero(JsonValue value)
		{
			return value is JsonNumber num && JsonNumber.IsZero(num);
		}

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.Abs(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.Abs(x),
				JsonNull n => n,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsCanonical(JsonValue value) =>
			(value) switch
			{
				JsonNumber or JsonNull => true,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsInteger(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsInteger(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsComplexNumber(JsonValue value) =>
			(value) switch
			{
				JsonNumber or JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsRealNumber(JsonValue value) =>
			(value) switch
			{
				JsonNumber or JsonNull => true,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsImaginaryNumber(JsonValue value) =>
			(value) switch
			{
				JsonNumber or JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsEvenInteger(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsEvenInteger(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsOddInteger(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsOddInteger(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};
		
		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsFinite(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsFinite(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsInfinity(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsInfinity(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsPositiveInfinity(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsPositiveInfinity(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsNegativeInfinity(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsNegativeInfinity(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsNaN(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsNaN(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsPositive(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsPositive(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsNegative(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => x.IsNegative,
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsNormal(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsNormal(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.IsSubnormal(JsonValue value) =>
			(value) switch
			{
				JsonNumber x => JsonNumber.IsSubnormal(x),
				JsonNull => false,
				_ => throw new NotSupportedException($"This operation is not allowed on a JSON {value.Type}")
			};

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.MaxMagnitude(JsonValue x, JsonValue y) =>
			(x, y) switch
			{
				(JsonNumber n1, JsonNumber n2) => JsonNumber.MaxMagnitude(n1, n2),
				(JsonNumber n1, JsonNull) => n1,
				(JsonNull, JsonNumber n2) => n2,
				(JsonNull n1, JsonNull n2) => n1.CompareTo(n2) >= 0 ? n1 : n2,
				_ => throw new NotSupportedException($"This operation is not allowed between a JSON {x.Type} and a JSON {y.Type}")
			};

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.MaxMagnitudeNumber(JsonValue x, JsonValue y) =>
			(x, y) switch
			{
				(JsonNumber n1, JsonNumber n2) => JsonNumber.MaxMagnitudeNumber(n1, n2),
				(JsonNumber n1, JsonNull) => n1,
				(JsonNull, JsonNumber n2) => n2,
				(JsonNull n1, JsonNull n2) => n1.CompareTo(n2) >= 0 ? n1 : n2,
				_ => throw new NotSupportedException($"This operation is not allowed between a JSON {x.Type} and a JSON {y.Type}")
			};

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.MinMagnitude(JsonValue x, JsonValue y) =>
			(x, y) switch
			{
				(JsonNumber n1, JsonNumber n2) => JsonNumber.MinMagnitude(n1, n2),
				(JsonNumber, JsonNull n2) => n2,
				(JsonNull n1, JsonNumber) => n1,
				(JsonNull n1, JsonNull n2) => n1.CompareTo(n2) <= 0 ? n1 : n2,
				_ => throw new NotSupportedException($"This operation is not allowed between a JSON {x.Type} and a JSON {y.Type}")
			};

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.MinMagnitudeNumber(JsonValue x, JsonValue y) =>
			(x, y) switch
			{
				(JsonNumber n1, JsonNumber n2) => JsonNumber.MaxMagnitude(n1, n2),
				(JsonNumber, JsonNull n2) => n2,
				(JsonNull n1, JsonNumber) => n1,
				(JsonNull n1, JsonNull n2) => n1.CompareTo(n2) <= 0 ? n1 : n2,
				_ => throw new NotSupportedException($"This operation is not allowed between a JSON {x.Type} and a JSON {y.Type}")
			};

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
		{
			return JsonNumber.Parse(s, provider);
		}

		/// <inheritdoc />
		static JsonValue INumberBase<JsonValue>.Parse(string s, NumberStyles style, IFormatProvider? provider)
		{
			return JsonNumber.Parse(s, provider);
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryConvertFromChecked<TOther>(TOther value, [MaybeNullWhen(false)] out JsonValue result)
		{
			if (!JsonNumber.TryConvertFromChecked<TOther>(value, out var num))
			{
				result = null;
				return false;
			}
			result = num;
			return true;
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryConvertFromSaturating<TOther>(TOther value, [MaybeNullWhen(false)] out JsonValue result)
		{
			if (!JsonNumber.TryConvertFromSaturating<TOther>(value, out var num))
			{
				result = null;
				return false;
			}
			result = num;
			return true;
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryConvertFromTruncating<TOther>(TOther value, [MaybeNullWhen(false)] out JsonValue result)
		{
			if (!JsonNumber.TryConvertFromTruncating<TOther>(value, out var num))
			{
				result = null;
				return false;
			}
			result = num;
			return true;
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryConvertToChecked<TOther>(JsonValue value, [MaybeNullWhen(false)] out TOther result)
		{
			if (value is not JsonNumber x)
			{
				result = default;
				return false;
			}
			return JsonNumber.TryConvertToChecked<TOther>(x, out result);
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryConvertToSaturating<TOther>(JsonValue value, [MaybeNullWhen(false)] out TOther result)
		{
			if (value is not JsonNumber x)
			{
				result = default;
				return false;
			}
			return JsonNumber.TryConvertToSaturating<TOther>(x, out result);
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryConvertToTruncating<TOther>(JsonValue value, [MaybeNullWhen(false)] out TOther result)
		{
			if (value is not JsonNumber x)
			{
				result = default;
				return false;
			}
			return JsonNumber.TryConvertToTruncating<TOther>(x, out result);
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonValue result)
		{
			//TODO: BUGBUG: style?
			return JsonNumber.TryParse(s, provider, out result);
		}

		/// <inheritdoc />
		static bool INumberBase<JsonValue>.TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonValue result)
		{
			//TODO: BUGBUG: style?
			return JsonNumber.TryParse(s, provider, out result);
		}

		#endregion

#endif

	}

}
