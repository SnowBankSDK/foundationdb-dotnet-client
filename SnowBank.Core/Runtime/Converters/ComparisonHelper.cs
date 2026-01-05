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

namespace SnowBank.Runtime.Converters
{
	using System.Collections.Concurrent;
	using System.Globalization;
	using SnowBank.Data.Tuples;

	/// <summary>Helper class used to compare object of "compatible" types</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class ComparisonHelper
	{

		/// <summary>Pair of types that can be used as a key in a dictionary</summary>
		public readonly struct TypePair : IEquatable<TypePair>
		{
			public readonly Type Left;
			public readonly Type Right;

			public TypePair(Type left, Type right)
			{
				this.Left = left;
				this.Right = right;
			}

			public override bool Equals(object? obj) => obj is TypePair tp && Equals(tp);

			public bool Equals(TypePair other) => this.Left == other.Left && this.Right == other.Right;

			public override int GetHashCode() => HashCode.Combine(this.Left, this.Right);
		}

		/// <summary>Helper class to use TypePair as keys in a dictionary</summary>
		public sealed class TypePairComparer : IEqualityComparer<TypePair>
		{ // REVIEW: this is redundant with TypeConverters.TypePairComparer!

			public static readonly TypePairComparer Default = new();

			private TypePairComparer() { }

			public bool Equals(TypePair x, TypePair y) => x.Left == y.Left && x.Right == y.Right;

			public int GetHashCode(TypePair obj) => HashCode.Combine(obj.Left, obj.Right);
		}

		private sealed record TypeComparisonMethods
		{
			public required Type Left { get; init; }

			public required Type Right { get; init; }

			public required IEqualityComparer<object> EqualityComparer { get; init; }

			public required Func<object?, object?, bool> RelaxedEqualityHandler { get; init; }

			public required Func<object?, object?, bool> StrictEqualityHandler { get; init; }

			public required Func<object?, object?, int> RelaxedComparisonHandler { get; init; }

			public required Func<object?, object?, int> StrictComparisonHandler { get; init; }

		}

		/// <summary>Cache of all the comparison lambda for a pair of types</summary>
		/// <remarks>Contains lambda that can compare two objects (of different types) for "similarity"</remarks>
		private static readonly ConcurrentDictionary<TypePair, TypeComparisonMethods> CachedComparers = new(TypePairComparer.Default);

		/// <summary>Tries to convert an object into an equivalent string representation (for equality comparison)</summary>
		/// <param name="value">Object to adapt</param>
		/// <returns>String equivalent of the object</returns>
		public static string? TryAdaptToString(object? value) => value switch
		{
			null => null,
			string s => s,
			char c => new string(c, 1),
			Slice sl => sl.ToStringUtf8(), //BUGBUG: ASCII? Ansi? UTF8?
			byte[] bytes => bytes.AsSlice().ToStringUtf8(), //BUGBUG: ASCII? Ansi? UTF8?
			IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
			_ => null
		};

		/// <summary>Tries to convert an object into an equivalent double representation (for equality comparison)</summary>
		/// <param name="value">Object to adapt</param>
		/// <param name="type">Type of the object to adapt</param>
		/// <param name="result">Double equivalent of the object</param>
		/// <returns>True if <paramref name="value"/> is compatible with a decimal. False if the type is not compatible</returns>
		public static bool TryAdaptToDecimal(object? value, Type type, out double result)
		{
			if (value != null)
			{
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Int16:   { result = (short) value; return true; }
					case TypeCode.UInt16:  { result = (ushort) value; return true; }
					case TypeCode.Int32:   { result = (int) value; return true; }
					case TypeCode.UInt32:  { result = (uint) value; return true; }
					case TypeCode.Int64:   { result = (long) value; return true; }
					case TypeCode.UInt64:  { result = (ulong) value; return true; }
					case TypeCode.Single:  { result = (float) value; return true; }
					case TypeCode.Double:  { result = (double) value; return true; }
					case TypeCode.Decimal: { result = (double) (decimal) value; return true;}
					//TODO: string?
				}
			}
			result = 0;
			return false;
		}

		/// <summary>Tries to convert an object into an equivalent Int64 representation (for equality comparison)</summary>
		/// <param name="value">Object to adapt</param>
		/// <param name="type">Type of the object to adapt</param>
		/// <param name="result">Int64 equivalent of the object</param>
		/// <returns>True if <paramref name="value"/> is compatible with a decimal. False if the type is not compatible</returns>
		public static bool TryAdaptToInteger(object? value, Type type, out long result)
		{
			if (value != null)
			{
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: { result = (bool)value ? 1 : 0; return true; }
					case TypeCode.Int16:   { result = (short)value; return true; }
					case TypeCode.UInt16:  { result = (ushort)value; return true; }
					case TypeCode.Int32:   { result = (int)value; return true; }
					case TypeCode.UInt32:  { result = (uint)value; return true; }
					case TypeCode.Int64:   { result = (long)value; return true; }
					case TypeCode.UInt64:  { result = (long)(ulong)value; return true; }
					case TypeCode.Single:  { result = (long)(float)value; return true; }
					case TypeCode.Double:  { result = (long)(double)value; return true; }
					case TypeCode.Decimal: { result = (long)(decimal)value; return true; }
				}
			}
			result = 0;
			return false;
		}

		private static (Func<object?, object?, bool> RelaxedEqualityComparer, Func<object?, object?, bool>? StrictEqualityComparer, Func<object?, object?, int> RelaxedComparer, Func<object?, object?, int>? StrictComparer) CreateTypeComparators(Type t1, Type t2)
		{
			Contract.Debug.Requires(t1 != null && t2 != null);

			// note: the most common scenarios will be when we compare 'A' to "A", or (int)123 to (long)123, Guids in string or System.Guid form, ...
			// We should not try too hard to compare complex objects (what about dates ? timespans? Guids?)

			// first, handle the easy cases
			if (t1 == t2)
			{
				return Type.GetTypeCode(t1) switch
				{
					TypeCode.Char => (
						(x, y) => x == null ? y == null : y != null && (char) x == (char) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((char) x).CompareTo((char) y),
						null
					),
					TypeCode.Byte => (
						(x, y) => x == null ? y == null : y != null && (byte) x == (byte) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((byte) x).CompareTo((byte) y),
						null
					),
					TypeCode.SByte => (
						(x, y) => x == null ? y == null : y != null && (sbyte) x == (sbyte) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((sbyte) x).CompareTo((sbyte) y),
						null
					),
					TypeCode.Int16 => (
						(x, y) => x == null ? y == null : y != null && (short) x == (short) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((short) x).CompareTo((short) y),
						null
					),
					TypeCode.UInt16 => (
						(x, y) => x == null ? y == null : y != null && (ushort) x == (ushort) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((ushort) x).CompareTo((ushort) y),
						null
					),
					TypeCode.Int32 => (
						(x, y) => x == null ? y == null : y != null && (int) x == (int) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((int) x).CompareTo((int) y),
						null
					),
					TypeCode.UInt32 => (
						(x, y) => x == null ? y == null : y != null && (uint) x == (uint) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((uint) x).CompareTo((uint) y),
						null
					),
					TypeCode.Int64 => (
						(x, y) => x == null ? y == null : y != null && (long) x == (long) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((long) x).CompareTo((long) y),
						null
					),
					TypeCode.UInt64 => (
						(x, y) => x == null ? y == null : y != null && (ulong) x == (ulong) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((ulong) x).CompareTo((ulong) y),
						null
					),
					// ReSharper disable CompareOfFloatsByEqualityOperator
					TypeCode.Single => (
						(x, y) => x == null ? y == null : y != null && (float) x == (float) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((float) x).CompareTo((float) y),
						null
					),
					TypeCode.Double => (
						(x, y) => x == null ? y == null : y != null && (double) x == (double) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((double) x).CompareTo((double) y),
						null
					),
					TypeCode.Decimal => (
						(x, y) => x == null ? y == null : y != null && (decimal) x == (decimal) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((decimal) x).CompareTo((decimal) y),
						null
					),
					// ReSharper restore CompareOfFloatsByEqualityOperator
					TypeCode.String => (
						(x, y) => x == null ? y == null : y != null && (string) x == (string) y,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : string.CompareOrdinal((string) x, (string) y),
						null
					),
					_ => (
						(x, y) => x is null ? y is null : ReferenceEquals(x, y) || x.Equals(y),
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : x is IComparable xc ? xc.CompareTo(y) : y is IComparable yc ? -yc.CompareTo(x) : throw ErrorCannotCompareTypes(x, y),
						null
					),
				};
			}

			if (IsStringType(t1) || IsStringType(t2))
			{
				return (
					(x, y) => x == null ? y == null : y != null && (ReferenceEquals(x, y) || (TryAdaptToString(x) == TryAdaptToString(y))),
					t1 != typeof(char) && t2 != typeof(char) ? ((x, y) => ReferenceEquals(x, y) || (x is string && y is string && x == y)) : null,
					(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : string.CompareOrdinal(TryAdaptToString(x), TryAdaptToString(y)),
					t1 != typeof(char) && t2 != typeof(char) ? ((x, y) => ReferenceEquals(x, y) ? 0 : x is null ? -1 : y is null ? +1 : CompareByTypes(x, y)) : null
				);
			}

			if (t1 == typeof(bool) && IsNumericType(t2))
			{
				return (
					(x, y) => x == null ? y == null : y != null && TryAdaptToInteger(y, t2, out long l2) && ((bool) x ? 1 : 0) == l2,
					(_, _) => false,
					(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : TryAdaptToInteger(y, t2, out long l2) ? ((bool) x ? 1 : 0).CompareTo(l2) : +1,
					(_, _) => +1
				);
			}
			if (t2 == typeof(bool) && IsNumericType(t1))
			{
				return (
					(x, y) => x == null ? y == null : y != null && TryAdaptToInteger(x, t1, out long l1) && (l1 == ((bool) y ? 1 : 0)),
					(_, _) => false,
					(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : TryAdaptToInteger(x, t1, out long l1) ? l1.CompareTo((bool) x ? 1 : 0) : -1,
					(_, _) => -1
				);
			}

			if (IsNumericType(t1) || IsNumericType(t2))
			{
				if (IsDecimalType(t1) || IsDecimalType(t2))
				{
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					return (
						(x, y) => x == null ? y == null : y != null && TryAdaptToDecimal(x, t1, out double d1) && TryAdaptToDecimal(y, t2, out double d2) && d1 == d2,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : TryAdaptToDecimal(x, t1, out double d1) && TryAdaptToDecimal(y, t2, out double d2) ? d1.CompareTo(d2) : CompareByTypes(x, y),
						null
					);
				}
				else
				{
					//TODO: handle UInt64 with values > long.MaxValue that will overflow to negative values when cast down to Int64
					return (
						(x, y) => x == null ? y == null : y != null && TryAdaptToInteger(x, t1, out long l1) && TryAdaptToInteger(y, t2, out long l2) && l1 == l2,
						null,
						(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : TryAdaptToInteger(x, t1, out long l1) && TryAdaptToInteger(y, t2, out long l2) ? l1.CompareTo(l2) : CompareByTypes(x, y),
						null
					);
				}
			}

			if (typeof(IVarTuple).IsAssignableFrom(t1) && typeof(IVarTuple).IsAssignableFrom(t2))
			{
				return (
					(x, y) => x == null ? y == null : y != null && ((IVarTuple) x).Equals((IVarTuple) y),
					null,
					(x, y) => x is null ? (y is null ? 0 : -1) : y is null ? +1 : ((IVarTuple) x).CompareTo((IVarTuple) y),
					null
				);
			}

			//TODO: some other way to compare ?
			return (
				object.ReferenceEquals,
				null,
				(x, y) => ReferenceEquals(x, y) ? 0 : throw ErrorCannotCompareTypes(x, y),
				null
			);

		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static NotSupportedException ErrorCannotCompareTypes(object? x, object? y) => throw new NotSupportedException($"Does not know how to compare instances of types {x?.GetType().GetFriendlyName()} and {y?.GetType().GetFriendlyName()}");

		private static int GetTypeRank(object? x)
		{
			// ranks from "lower" to "higher":
			// - null
			// - bytes
			// - string
			// - tuples
			// - int
			// - decimal
			// - bool
			// - guid
			// - versionstamp
			// - other
			return (x) switch
			{
				null => 0,
				(Slice or byte[]) => 1,
				(string) => 2,
				(int or long or uint or ulong or short or ushort) => 3,
				(float or double or decimal) => 4,
				(bool) => 5,
				(Guid or Uuid128) => 6,
				(VersionStamp) => 7,
				_ => -1,
			};
		}

		private static int CompareByTypes(object x, object y)
		{
			Contract.Debug.Requires(x is not null && y is not null);

			// we know that they are not of the same type, but will apply the following rules

			var xr = GetTypeRank(x);
			var yr = GetTypeRank(y);
			if (xr < 0 || yr < 0) throw ErrorCannotCompareTypes(x, y);
			return xr.CompareTo(yr);
		}

		private static TypeComparisonMethods GetTypeMethods(Type t1, Type t2)
		{
			var pair = new TypePair(t1, t2);
			if (!CachedComparers.TryGetValue(pair, out var slot))
			{
				var (eqRelaxed, eqStrict, cmpRelaxed, cmpStrict) = CreateTypeComparators(t1, t2);
				slot = new()
				{
					Left = t1,
					Right = t2,
					EqualityComparer = EqualityComparer<object>.Create(eqStrict ?? eqRelaxed),
					RelaxedEqualityHandler = eqRelaxed,
					StrictEqualityHandler = eqStrict ?? eqRelaxed,
					RelaxedComparisonHandler = cmpRelaxed,
					StrictComparisonHandler = cmpStrict ?? cmpRelaxed
				};
				CachedComparers.TryAdd(pair, slot);
			}
			return slot;
		}

		[Pure]
		public static IEqualityComparer<object> GetTypeEqualityComparer(Type t1, Type t2)
		{
			return GetTypeMethods(t1, t2).EqualityComparer;
		}

		[Pure]
		[Obsolete("Use either GetTypeEqualityComparatorRelaxed() or GetTypeEqualityComparatorStrict() to specify how strings are compared to numbers.")]
		public static Func<object?, object?, bool> GetTypeEqualityComparator(Type t1, Type t2)
		{
			return GetTypeMethods(t1, t2).RelaxedEqualityHandler;
		}

		[Pure]
		public static Func<object?, object?, bool> GetTypeEqualityComparatorRelaxed(Type t1, Type t2)
		{
			return GetTypeMethods(t1, t2).RelaxedEqualityHandler;
		}

		[Pure]
		public static Func<object?, object?, bool> GetTypeEqualityComparatorStrict(Type t1, Type t2)
		{
			return GetTypeMethods(t1, t2).StrictEqualityHandler;
		}

		[Pure]
		public static Func<object?, object?, int> GetTypeComparatorRelaxed(Type t1, Type t2)
		{
			return GetTypeMethods(t1, t2).RelaxedComparisonHandler;
		}

		[Pure]
		public static Func<object?, object?, int> GetTypeComparatorStrict(Type t1, Type t2)
		{
			return GetTypeMethods(t1, t2).StrictComparisonHandler;
		}

		/// <summary>Tries to compare any two object for "equality", where string "123" is considered equal to integer 123</summary>
		/// <param name="x">Left object to compare</param>
		/// <param name="y">Right object to compare</param>
		/// <returns>True if both objects are "similar" (ie: the represent the same logical value)</returns>
		/// <example>
		/// AreSimilar("123", 123) => true
		/// AreSimilar('A', "A") => true
		/// AreSimilar(false, 0) => true
		/// AreSimilar(true, 1) => true
		/// </example>
		[Obsolete("Use AreSimilarRelaxed or AreSimilarStrict to specify how strings are compared to numbers.")]
		public static bool AreSimilar(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return true;
			if (x == null || y == null) return false;

			var comparator = GetTypeEqualityComparator(x.GetType(), y.GetType());
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		/// <summary>Tries to compare any two object for "equality", where both the string <c>"123"</c> and double <c>123d</c> are considered equal to integer <c>123</c></summary>
		/// <param name="x">Left object to compare</param>
		/// <param name="y">Right object to compare</param>
		/// <returns>True if both objects are "similar" (ie: the represent the same logical value)</returns>
		/// <example><code>
		/// AreSimilarRelaxed(123, 123.0) => true // integers and decimals are compared
		/// AreSimilarRelaxed("123", 123) => true // strings are compared to numbers
		/// AreSimilarRelaxed('A', "A") => true
		/// AreSimilarRelaxed(false, 0) => true // booleans are compared to numbers
		/// AreSimilarRelaxed(true, 1) => true
		/// </code></example>
		public static bool AreSimilarRelaxed(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return true;
			if (x == null || y == null) return false;

			var comparator = GetTypeEqualityComparatorRelaxed(x.GetType(), y.GetType());
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		/// <summary>Tries to compare any two object for "equality", where the double <c>123d</c> is considered equal to the integer <c>123</c>, but the string <c>"123"</c> is not.</summary>
		/// <param name="x">Left object to compare</param>
		/// <param name="y">Right object to compare</param>
		/// <returns>True if both objects are "similar" (ie: the represent the same logical value)</returns>
		/// <example><code>
		/// AreSimilarStrict(123, 123.0) => true // integers and decimals are compared
		/// AreSimilarStrict("123", 123) => false // strings are NOT compared to numbers
		/// AreSimilarStrict('A', "A") => true
		/// AreSimilarStrict(false, 0) => false // booleans are NOT compared to numbers
		/// AreSimilarStrict(true, 1) => false
		/// </code></example>
		public static bool AreSimilarStrict(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return true;
			if (x == null || y == null) return false;

			var comparator = GetTypeEqualityComparatorStrict(x.GetType(), y.GetType());
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		[Obsolete("Use AreSimilarRelaxed or AreSimilarStrict to specify how strings are compared to numbers.")]
		public static bool AreSimilar<T1, T2>(T1 x, T2 y)
		{
			var comparator = GetTypeEqualityComparator(typeof(T1), typeof(T2));
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		[Pure]
		public static bool AreSimilarRelaxed<T1, T2>(T1 x, T2 y)
		{
			var comparator = GetTypeEqualityComparatorRelaxed(typeof(T1), typeof(T2));
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		[Pure]
		public static bool AreSimilarStrict<T1, T2>(T1 x, T2 y)
		{
			var comparator = GetTypeEqualityComparatorStrict(typeof(T1), typeof(T2));
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		/// <summary>Returns true if both types are considered "the same"</summary>
		/// <returns>Returns true if t1 is equal to t2</returns>
		private static bool AreEquivalentTypes(Type t1, Type t2)
		{
			return t1 == t2 || t1.IsEquivalentTo(t2);
		}

		public static int CompareSimilarRelaxed(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x is null) return -1;
			if (y is null) return +1;

			var comparator = GetTypeComparatorRelaxed(x.GetType(), y.GetType());
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		public static int CompareSimilarStrict(object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x is null) return -1;
			if (y is null) return +1;

			var comparator = GetTypeComparatorStrict(x.GetType(), y.GetType());
			Contract.Debug.Assert(comparator != null);
			return comparator(x, y);
		}

		/// <summary>Return true if the type is considered to be a "string" (string, char)</summary>
		/// <returns>True for <see cref="System.String">string</see> and <see cref="System.Char">char</see>.</returns>
		public static bool IsStringType(Type t)
		{
			return t == typeof(string) || t == typeof(char);
		}

		/// <summary>Returns true if the specified type is considered to be a "number"</summary>
		/// <returns>True for integers (8, 16, 32 or 64 bits, signed or unsigned) and their Nullable versions. Bytes and Chars are not considered to be numbers because they are custom serialized</returns>
		private static bool IsNumericType(Type t)
		{
			// Return true for valuetypes that are considered numbers.
			// Notes:
			// - We do not consider 'byte' to be a number, because we will serialize it as a byte[1]
			// - We do not consider 'char' to be a number, because we will serialize it as a string
			// - For nullable types, we consider the underlying type, so "int?" is considered to be a number"

			switch (Type.GetTypeCode(t))
			{
				case TypeCode.SByte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
					return true;

				case TypeCode.Object:
				{
					// Could be a Nullable<T>
					var nullableType = Nullable.GetUnderlyingType(t);
					return nullableType != null && IsNumericType(nullableType);
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>Returns true if the specified type is considered to be a "decimal"</summary>
		/// <returns>True for <see cref="System.Double">double</see> and <see cref="System.Single">float</see>.</returns>
		private static bool IsDecimalType(Type t)
		{
			return t == typeof(double) || t == typeof(float);
			//TODO: System.Decimal?
		}

	}

}
