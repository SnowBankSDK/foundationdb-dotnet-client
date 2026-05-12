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

// ReSharper disable MemberHidesStaticFromOuterClass

namespace SnowBank.Testing
{
	using System.Linq.Expressions;
	using SnowBank.Data.Tuples;
	using NUnit.Framework.Constraints;
	using NUnit.Framework.Internal;

	/// <summary>JSON Assertions</summary>
	[PublicAPI]
	public static class IsJson
	{

		public static JsonConstraintExpression Not
		{
			get
			{
				var expr = new JsonConstraintExpression();
				expr.Append(new JsonNotOperator());
				return expr;
			}
		}

		/// <summary>Assert that the value is null or missing</summary>
		/// <remarks>Includes all variants of <see cref="JsonNull"/></remarks>
		public static JsonConstraint Null => new JsonTypeConstraint(JsonType.Null);

		/// <summary>Assert that the value is a JSON Object</summary>
		public static JsonConstraint Object => new JsonTypeConstraint(JsonType.Object);

		/// <summary>Assert that the value is a JSON Array</summary>
		public static JsonConstraint Array => new JsonTypeConstraint(JsonType.Array);

		/// <summary>Assert that the value is a JSON String</summary>
		public static JsonConstraint String => new JsonTypeConstraint(JsonType.String);

		/// <summary>Assert that the value is a JSON Number</summary>
		public static JsonConstraint Number => new JsonTypeConstraint(JsonType.Number);

		/// <summary>Assert that the value is a JSON Boolean</summary>
		public static JsonConstraint Boolean => new JsonTypeConstraint(JsonType.Boolean);

		/// <summary>Assert that the value is an explicit JSON Null literal</summary>
		/// <remarks>This only accept the singleton <see cref="JsonNull.Null"/>. If you want to also include <see cref="JsonNull.Missing"/>, you should use the <see cref="Null"/> assertion.</remarks>
		public static JsonConstraint ExplicitNull => new JsonEqualConstraint(JsonComparisonOperator.SameAs, JsonNull.Null);

		/// <summary>Assert that the value is missing</summary>
		/// <remarks>This only accept the singleton <see cref="JsonNull.Missing"/>. If you want to also include <see cref="JsonNull.Null"/>, you should use the <see cref="Null"/> assertion.</remarks>
		public static JsonConstraint Missing => new JsonEqualConstraint(JsonComparisonOperator.SameAs, JsonNull.Missing);

		/// <summary>Assert that the value is out of bounds of its parent array.</summary>
		public static JsonConstraint Error => new JsonEqualConstraint(JsonComparisonOperator.SameAs, JsonNull.Error);

		/// <summary>Assert that the value is <see cref="JsonBoolean.False"/></summary>
		public static JsonConstraint False => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonBoolean.False);

		/// <summary>Assert that the value is <see cref="JsonBoolean.True"/></summary>
		public static JsonConstraint True => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonBoolean.True);

		/// <summary>Assert that the value is <see cref="JsonNumber.Zero"/></summary>
		public static JsonConstraint Zero => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonNumber.Zero);

		/// <summary>Assert that the value is empty</summary>
		/// <remarks>Supports Objects, Arrays and Strings.</remarks>
		public static JsonConstraint Empty => new JsonSizeConstraint(0, ExpressionType.Equal);

		/// <summary>Assert that the value is an Array, Object or String with the specified size</summary>
		/// <remarks>Supports Objects, Arrays and Strings.</remarks>
		public static JsonConstraint OfSize(int expected) => new JsonSizeConstraint(expected, ExpressionType.Equal);

		/// <summary>Assert that the value is an Array, Object or String with a size greater than or equal to the specified amount</summary>
		public static JsonConstraint OfSizeAtLeast(int expected) => new JsonSizeConstraint(expected, ExpressionType.GreaterThanOrEqual);

		/// <summary>Assert that the value is an Array, Object or String with a size strictly greater than the specified amount</summary>
		public static JsonConstraint OfSizeGreaterThan(int expected) => new JsonSizeConstraint(expected, ExpressionType.GreaterThan);

		/// <summary>Assert that the value is an Array, Object or String with a size less than or equal to the specified amount</summary>
		public static JsonConstraint OfSizeAtMost(int expected) => new JsonSizeConstraint(expected, ExpressionType.LessThanOrEqual);

		/// <summary>Assert that the value is an Array, Object or String with a size strictly less than the specified amount</summary>
		public static JsonConstraint OfSizeLessThan(int expected) => new JsonSizeConstraint(expected, ExpressionType.LessThan);

		/// <summary>Assert that the value is read-only.</summary>
		public static JsonConstraint ReadOnly => new JsonReadOnlyConstraint(readOnly: true);

		/// <summary>Assert that the value is not read-only.</summary>
		public static JsonConstraint Mutable => new JsonReadOnlyConstraint(readOnly: false);

		#region JsonValue...
		
		/// <summary>Assert that the value is a JSON Value equal to the expected value</summary>
		public static JsonConstraint EqualTo(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, expected);

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(IEnumerable<JsonValue?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, expected);

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, expected);

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, expected);

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(JsonValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, expected);

		#endregion

		#region Generic...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo<TValue>(TValue expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonValue.FromValue(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo<TValue>(TValue[] expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo<TValue>(ReadOnlySpan<TValue> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo<TValue>(IEnumerable<TValue> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		#endregion

		#region Boolean...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(bool expected) => new JsonEqualConstraint<bool>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(bool? expected) => new JsonEqualConstraint<bool?>(expected);

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<bool> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<bool?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		#endregion

		#region Int32...

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(int expected) => new JsonEqualConstraint<int>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(int? expected) => new JsonEqualConstraint<int?>(expected);

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<int> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<int?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(int expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(int expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(int expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(int expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Int64...
		
		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(long expected) => new JsonEqualConstraint<long>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(long? expected) => new JsonEqualConstraint<long?>(expected);

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<long> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<long?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(long expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(long expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(long expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(long expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Float...

		/// <summary>Assert that the value is equal to the expected value, within 1 ULPS</summary>
		public static JsonConstraint EqualTo(float expected) => new JsonEqualConstraint<float>(expected, UlpsToleranceComparer.OneUlps);

		/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
		public static JsonConstraint EqualTo(float expected, float tolerance) => new JsonEqualConstraint<float>(expected, CreateToleranceComparer(tolerance));

		/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
		public static JsonConstraint EqualTo(float expected, IEqualityComparer<float>? comparer) => new JsonEqualConstraint<float>(expected, comparer);

		/// <summary>Assert that the value is equal to the expected value, within 1 ULPS</summary>
		public static JsonConstraint EqualTo(float? expected) => new JsonEqualConstraint<float?>(expected, UlpsToleranceComparer.OneUlps);

		/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
		public static JsonConstraint EqualTo(float? expected, float? tolerance) => new JsonEqualConstraint<float?>(expected, CreateToleranceComparer(tolerance));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<float> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<float?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(float expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(float expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(float expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(float expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Double...
		
		/// <summary>Assert that the value is equal to the expected value, within 1 ULPS</summary>
		public static JsonConstraint EqualTo(double expected) => new JsonEqualConstraint<double>(expected, UlpsToleranceComparer.OneUlps);

		/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
		public static JsonConstraint EqualTo(double expected, double tolerance) => new JsonEqualConstraint<double>(expected, CreateToleranceComparer(tolerance));

		/// <summary>Assert that the value is equal to the expected value, within the given comparer</summary>
		public static JsonConstraint EqualTo(double expected, IEqualityComparer<double>? comparer) => new JsonEqualConstraint<double>(expected, comparer);

		/// <summary>Assert that the value is equal to the expected value, within 1 ULPS</summary>
		public static JsonConstraint EqualTo(double? expected) => new JsonEqualConstraint<double?>(expected, UlpsToleranceComparer.OneUlps);

		/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
		public static JsonConstraint EqualTo(double? expected, double? tolerance) => new JsonEqualConstraint<double?>(expected, CreateToleranceComparer(tolerance));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<double> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<double?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(double expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(double expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(double expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(double expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

		#endregion

		#region Strings...

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(string? expected) => new JsonEqualConstraint<string>(expected, StringComparer.Ordinal);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(string? expected, IEqualityComparer<string>? comparer) => new JsonEqualConstraint<string>(expected, comparer ?? StringComparer.Ordinal);

		/// <summary>Assert that the value is a JSON Array with the expected content</summary>
		public static JsonConstraint EqualTo(ReadOnlySpan<string?> expected) => new JsonEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

		/// <summary>Assert that the value is strictly greater than the expected value</summary>
		public static JsonConstraint GreaterThan(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThan, JsonString.Return(expected));

		/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
		public static JsonConstraint GreaterThanOrEqualTo(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonString.Return(expected));

		/// <summary>Assert that the value is strictly less than the expected value</summary>
		public static JsonConstraint LessThan(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThan, JsonString.Return(expected));

		/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
		public static JsonConstraint LessThanOrEqualTo(string? expected) => new JsonEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonString.Return(expected));

		#endregion

		#region Guids...

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Guid expected) => new JsonEqualConstraint<Guid>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Guid? expected) => new JsonEqualConstraint<Guid?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid128 expected) => new JsonEqualConstraint<Uuid128>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid128? expected) => new JsonEqualConstraint<Uuid128?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid96 expected) => new JsonEqualConstraint<Uuid96>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid96? expected) => new JsonEqualConstraint<Uuid96?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid80 expected) => new JsonEqualConstraint<Uuid80>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid80? expected) => new JsonEqualConstraint<Uuid80?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid64 expected) => new JsonEqualConstraint<Uuid64>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(Uuid64? expected) => new JsonEqualConstraint<Uuid64?>(expected);

		#endregion

		#region Dates and times...

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(DateTime expected) => new JsonEqualConstraint<DateTime>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(DateTime? expected) => new JsonEqualConstraint<DateTime?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(DateTimeOffset expected) => new JsonEqualConstraint<DateTimeOffset>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(DateTimeOffset? expected) => new JsonEqualConstraint<DateTimeOffset?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(DateOnly expected) => new JsonEqualConstraint<DateOnly>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(DateOnly? expected) => new JsonEqualConstraint<DateOnly?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(TimeOnly expected) => new JsonEqualConstraint<TimeOnly>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(TimeOnly? expected) => new JsonEqualConstraint<TimeOnly?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(NodaTime.Instant expected) => new JsonEqualConstraint<NodaTime.Instant>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(NodaTime.Instant? expected) => new JsonEqualConstraint<NodaTime.Instant?>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(NodaTime.Duration expected) => new JsonEqualConstraint<NodaTime.Duration>(expected);

		/// <summary>Assert that the value is equal to the expected value</summary>
		public static JsonConstraint EqualTo(NodaTime.Duration? expected) => new JsonEqualConstraint<NodaTime.Duration?>(expected);

		#endregion

		private static JsonValue? CoerceToJsonValue<TActual>(TActual actual) => actual switch
		{
			null                      => JsonNull.Null,
			JsonValue value           => value,
			ObservableJsonValue value => value.ToJsonValue(),
			MutableJsonValue value    => value.ToJsonValue(),
			IJsonPackable packable    => packable.JsonPack(CrystalJsonSettings.Json, CrystalJson.DefaultResolver),
			_                         => null
		};

		private static string Jsonify(JsonValue value) => value switch
		{
			JsonNull when ReferenceEquals(value, JsonNull.Null) => "<null>",
			JsonNull when ReferenceEquals(value, JsonNull.Missing) => "<missing>",
			JsonNull when ReferenceEquals(value, JsonNull.Error) => "<error>",
			_ => value.ToJsonText(CrystalJsonSettings.JsonIndented).Replace("\r\n", "\r\n  "),
		};

		private static string Stringify<TValue>(TValue? value) => value switch
		{
			null => "<null>",
			JsonValue j => Jsonify(j),
			_ => STuple.Formatter.Stringify(value).Replace("\r\n", "\r\n  "),
		};

		internal enum JsonComparisonOperator
		{
			Equal,
			SameAs,
			GreaterThan,
			GreaterThanOrEqual,
			LessThan,
			LessThanOrEqual,
		}

		[PublicAPI]
		public abstract class JsonConstraint : Constraint
		{

			protected JsonConstraint(params object?[] args)
				: base(args)
			{ }

			public new JsonConstraintExpression And
			{
				get
				{
					var builder = this.Builder;
					if (builder == null)
					{
						builder = new ConstraintBuilder();
						builder.Append(this);
					}
					builder.Append(new JsonAndOperator());
					return new JsonConstraintExpression(builder);
				}
			}

			public new JsonConstraintExpression Or
			{
				get
				{
					var builder = this.Builder;
					if (builder == null)
					{
						builder = new ConstraintBuilder();
						builder.Append(this);
					}
					builder.Append(new JsonOrOperator());
					return new JsonConstraintExpression(builder);
				}
			}

		}

		internal sealed class JsonEqualConstraint : JsonConstraint
		{

			public JsonComparisonOperator Operator { get; }

			public JsonValue Expected { get; }

			public JsonEqualConstraint(JsonComparisonOperator op, JsonValue expected) : base(expected)
			{
				this.Operator = op;
				this.Expected = expected;
			}

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var obj = CoerceToJsonValue(actual);
				if (obj is null)
				{ // the actual value is NOT a JsonValue
					return new Result<TActual>(this, actual, actual, false);
				}
				return this.Operator switch
				{
					JsonComparisonOperator.Equal => new Result<TActual>(this, actual, obj, obj.Equals(this.Expected)),
					JsonComparisonOperator.SameAs => new Result<TActual>(this, actual, obj, ReferenceEquals(obj, this.Expected)),
					JsonComparisonOperator.GreaterThan => new Result<TActual>(this, actual, obj, obj.CompareTo(this.Expected) > 0),
					JsonComparisonOperator.GreaterThanOrEqual => new Result<TActual>(this, actual, obj, obj.CompareTo(this.Expected) >= 0),
					JsonComparisonOperator.LessThan => new Result<TActual>(this, actual, obj, obj.CompareTo(this.Expected) < 0),
					JsonComparisonOperator.LessThanOrEqual => new Result<TActual>(this, actual, obj, obj.CompareTo(this.Expected) <= 0),
					_ => throw new NotSupportedException()
				};
			}

			public override string Description => Jsonify(this.Expected);

			internal class Result<TActual> : ConstraintResult
			{
				private JsonValue ExpectedValue { get; }

				private JsonComparisonOperator Operator { get; }

				private TActual? OriginalValue { get; }

				public Result(JsonEqualConstraint constraint, TActual? original, object? actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.OriginalValue = original; // original "actual", before being coerced into a JsonValue
					this.Operator = constraint.Operator;
					this.ExpectedValue = constraint.Expected;
				}

				private void WriteDifferences(MessageWriter writer, string? op, string message)
				{
					writer.WriteMessageLine(message);
					writer.Write(TextMessageWriter.Pfx_Expected);
					if (op != null) writer.Write(op + " ");
					if (this.ActualValue is JsonValue value)
					{
						bool showType = value.Type != this.ExpectedValue.Type;

						if (showType && this.ExpectedValue is not JsonNull)
						{
							writer.Write($"<{this.ExpectedValue.Type}> ");
						}
						writer.WriteLine(Jsonify(this.ExpectedValue));

						writer.Write(TextMessageWriter.Pfx_Actual);
						if (showType && value is not JsonNull)
						{
							writer.Write($"<{value.Type}> ");
						}
						writer.WriteLine(Jsonify(value));
					}
					else if (this.ActualValue is null)
					{
						writer.WriteLine(Jsonify(this.ExpectedValue));
						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.WriteLine("null");
					}
					else
					{
						if (this.ExpectedValue is not JsonNull)
						{
							writer.Write($"<{this.ExpectedValue.Type}> ");
						}
						writer.WriteLine(Jsonify(this.ExpectedValue));

						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.Write($"<{(this.OriginalValue ?? this.ActualValue).GetType().GetFriendlyName()}> ");
						writer.WriteActualValue(this.ActualValue);
					}
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					string prefix = "JSON value";
					if (this.OriginalValue is IJsonProxyNode node)
					{
						var path = node.GetPath();
						if (!path.IsEmpty())
						{
							prefix = $"JSON value at `{path}`";
						}
					}

					switch (this.Operator)
					{
						case JsonComparisonOperator.Equal:
						{
							WriteDifferences(writer, null, $"{prefix} does not match the expected value");
							break;
						}
						case JsonComparisonOperator.SameAs:
						{
							WriteDifferences(writer, "===", $"{prefix} should be the same instance as the expected value");
							break;
						}
						case JsonComparisonOperator.GreaterThan:
						{
							WriteDifferences(writer, ">", $"{prefix} should be greater than the expected value");
							break;
						}
						case JsonComparisonOperator.GreaterThanOrEqual:
						{
							WriteDifferences(writer, ">=", $"{prefix} should be greater than or equal to the expected value");
							break;
						}
						case JsonComparisonOperator.LessThan:
						{
							WriteDifferences(writer, "<", $"{prefix} should be less than the expected value");
							break;
						}
						case JsonComparisonOperator.LessThanOrEqual:
						{
							WriteDifferences(writer, "<=", $"{prefix} should be less than or equal to the expected value");
							break;
						}
						default:
						{
							throw new NotSupportedException("This type of comparison is not supported.");
						}
					}
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					if (this.ActualValue is JsonValue value)
					{
						writer.WriteLine(Jsonify(value));
					}
					else
					{
						base.WriteActualValueTo(writer);
					}
				}
			}

		}

		internal sealed class JsonEqualConstraint<TValue> : JsonConstraint
		{

			public TValue? Expected { get; }

			public IEqualityComparer<TValue> Comparer { get; }

			public JsonEqualConstraint(TValue? expected, IEqualityComparer<TValue>? comparer = null) : base(expected)
			{
				this.Expected = expected;
				this.Comparer = comparer ?? EqualityComparer<TValue>.Default;
			}

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				// TActual is expected to be either null, or a JsonValue
				var obj = CoerceToJsonValue(actual);

				if (obj is null)
				{ // the actual value is NOT a JsonValue
					return new Result<TActual>(this, actual, actual, false);
				}

				bool isEqual;
				if (this.Expected is null)
				{
					isEqual = obj is JsonNull;
				}
				else if (this.Expected is JsonValue j)
				{
					isEqual = obj.Equals(j);
				}
				else
				{
					isEqual = obj.ValueEquals(this.Expected, this.Comparer);
				}

				return new Result<TActual>(this, actual, obj, isEqual);
			}

			public override string Description => $"<{(this.Expected?.GetType() ?? typeof(TValue)).GetFriendlyName()}> {Stringify(this.Expected)}";

			internal class Result<TActual> : ConstraintResult
			{

				private TValue? ExpectedValue { get; }

				private TActual? OriginalValue { get; }

				public Result(JsonEqualConstraint<TValue> constraint, TActual? original, object? actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.OriginalValue = original;
					this.ExpectedValue = constraint.Expected;
				}

				private void WriteDifferences(MessageWriter writer, string? op, string message)
				{
					writer.WriteMessageLine(message);
					writer.Write(TextMessageWriter.Pfx_Expected);
					if (op != null) writer.Write(op + " ");
					if (this.ActualValue is TValue value)
					{
						if (this.ExpectedValue is not (null or JsonNull))
						{
							writer.Write($"<{(this.ExpectedValue.GetType()).GetFriendlyName()}> ");
						}
						writer.WriteLine(Stringify(this.ExpectedValue));

						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.Write($"<{(value.GetType()).GetFriendlyName()}> ");
						writer.WriteLine(Stringify(value));
					}
					else if (this.ActualValue is null)
					{
						writer.WriteLine(Stringify(this.ExpectedValue));
						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.WriteLine("null");
					}
					else if (this.ActualValue is JsonValue j)
					{
						if (this.ExpectedValue is not (null or JsonNull))
						{
							writer.Write($"<{(this.ExpectedValue.GetType()).GetFriendlyName()}> ");
						}
						writer.WriteLine(Stringify(this.ExpectedValue));

						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.WriteLine($"<{j.GetType().GetFriendlyName()}> {Jsonify(j)}");
						if (TryBind(j, out var casted))
						{
							writer.Write("  Decoded:  ");
							writer.WriteLine($"<{(casted?.GetType() ?? typeof(TValue)).GetFriendlyName()}> {Stringify(casted)}");
						}
					}
					else
					{
						if (this.ExpectedValue is not (null or JsonNull))
						{
							writer.Write($"<{(this.ExpectedValue.GetType()).GetFriendlyName()}> ");
						}
						writer.WriteLine(Stringify(this.ExpectedValue));

						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.Write($"<{(this.OriginalValue ?? this.ActualValue).GetType().GetFriendlyName()}> ");
						writer.WriteActualValue(this.ActualValue);
					}
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					string message = this.OriginalValue is IJsonProxyNode node
						? $"JSON value at `{node.GetPath()}` does not match the expected value"
						: "JSON value does not match the expected value";

					WriteDifferences(writer, null, message);
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					if (this.ActualValue is JsonNull jn)
					{
						writer.WriteLine(Jsonify(jn));

					}
					else if (this.ActualValue is JsonValue j)
					{
						writer.WriteLine($"<{j.GetType().GetFriendlyName()}> {Jsonify(j)}");
					}
					else
					{
						base.WriteActualValueTo(writer);
					}
				}

				private bool TryBind(JsonValue value, out TValue? result)
				{
					try
					{
						result = value.Bind<TValue>();
						return true;
					}
					catch (Exception)
					{
						result = default;
						return false;
					}
				}

			}

		}

		internal sealed class JsonTypeConstraint : JsonConstraint
		{

			public JsonType Expected { get; }

			public JsonTypeConstraint(JsonType expected) : base(expected)
			{
				this.Expected = expected;
			}

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var value = CoerceToJsonValue(actual);
				if (value is null)
				{
					throw new ArgumentException("The actual value must be a JSON value. The value passed was of type " + typeof(TActual).Name, nameof(actual));
				}
				return new Result(this, value, value.Type == this.Expected);
			}

			public override string Description => this.Expected.ToString();

			internal class Result : ConstraintResult
			{

				private JsonType ExpectedType { get; }

				private JsonValue ActualJsonValue { get; }

				public Result(JsonTypeConstraint constraint, JsonValue actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.ExpectedType = constraint.Expected;
					this.ActualJsonValue = actual;
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					writer.WriteMessageLine("JSON value is of a different type than expected.");
					writer.Write(TextMessageWriter.Pfx_Expected);
					writer.WriteLine($"<{this.ExpectedType.ToString()}>");
					writer.Write(TextMessageWriter.Pfx_Actual);
					writer.Write($"<{this.ActualJsonValue.Type}> ");
					writer.WriteLine(Jsonify(this.ActualJsonValue));
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					writer.WriteLine(Jsonify(this.ActualJsonValue));
				}

			}

		}

		internal sealed class JsonSizeConstraint : JsonConstraint
		{

			public JsonSizeConstraint(int expectedSize, ExpressionType type)
			{
				Contract.Positive(expectedSize);
				Contract.Debug.Requires(type is (ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual));
				this.ExpectedSize = expectedSize;
				this.Type = type;
			}

			public int ExpectedSize { get; }

			public ExpressionType Type { get; }

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var val = CoerceToJsonValue(actual);
				if (val is null) throw new ArgumentException("The actual value must be a JSON value. The value passed was of type " + typeof(TActual).Name, nameof(actual));

				int? count = val switch
				{
					JsonArray arr  => arr.Count,
					JsonObject obj => obj.Count,
					JsonString str => str.Length,
					_              => null,
				};
				if (count == null)
				{
					return new Result(this, val, 0, false);
				}
				switch (this.Type)
				{
					case ExpressionType.Equal:              return new Result(this, val, count.Value, count.Value == this.ExpectedSize);
					case ExpressionType.NotEqual:           return new Result(this, val, count.Value, count.Value != this.ExpectedSize);
					case ExpressionType.GreaterThan:        return new Result(this, val, count.Value, count.Value > this.ExpectedSize);
					case ExpressionType.GreaterThanOrEqual: return new Result(this, val, count.Value, count.Value >= this.ExpectedSize);
					case ExpressionType.LessThan:           return new Result(this, val, count.Value, count.Value < this.ExpectedSize);
					case ExpressionType.LessThanOrEqual:    return new Result(this, val, count.Value, count.Value <= this.ExpectedSize);
					default:                                throw new InvalidOperationException();
				}
			}

			public override string Description => this.Type switch
			{
				ExpressionType.Equal              => this.ExpectedSize == 0 ? "<empty>" : $"of size {this.ExpectedSize}",
				ExpressionType.NotEqual           => this.ExpectedSize == 0 ? "<not empty>" : $"of size != {this.ExpectedSize}",
				ExpressionType.GreaterThan        => $"of size > {this.ExpectedSize}",
				ExpressionType.GreaterThanOrEqual => $"of size >= {this.ExpectedSize}",
				ExpressionType.LessThan           => $"of size < {this.ExpectedSize}",
				ExpressionType.LessThanOrEqual    => $"of size <= {this.ExpectedSize}",
				_                                 => "???",
			};

			private class Result : ConstraintResult
			{

				private int ActualSize { get; }

				private JsonValue ActualJsonValue { get; }

				public Result(JsonSizeConstraint constraint, JsonValue actual, int actualSize, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.ActualSize = actualSize;
					this.ActualJsonValue = actual;
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					if (this.ActualJsonValue is JsonArray or JsonObject or JsonString)
					{
						writer.Write(TextMessageWriter.Pfx_Expected);
						writer.WriteMessageLine(this.Description);

						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.WriteMessageLine(this.ActualSize == 0 ? "<empty>" : $"of size {this.ActualSize}");

						writer.WriteActualValue(Jsonify(this.ActualJsonValue));
					}
					else
					{
						writer.Write(TextMessageWriter.Pfx_Expected);
						writer.WriteMessageLine(this.Description);

						writer.Write(TextMessageWriter.Pfx_Actual);
						writer.Write($"<{this.ActualJsonValue.Type}> ");
						writer.WriteLine(Jsonify(this.ActualJsonValue));
					}
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					writer.WriteLine($"{this.ActualJsonValue.Type} of size {this.ActualSize}");
					writer.WriteMessageLine(Jsonify(this.ActualJsonValue));
				}

			}

		}

		internal sealed class JsonReadOnlyConstraint : JsonConstraint
		{

			public bool ReadOnly { get; }

			public JsonReadOnlyConstraint(bool readOnly)
			{
				this.ReadOnly = readOnly;
			}

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				return actual switch
				{
					null => new Result(this, JsonNull.Null, true),
					JsonValue val => new Result(this, val, this.ReadOnly ? val.IsReadOnly : !val.IsReadOnly),
					_ => throw new ArgumentException("The actual value must be a JSON value. The value passed was of type " + typeof(TActual).Name, nameof(actual)),
				};
			}

			public override string Description => this.ReadOnly ? "<read-only>" : "<mutable>";

			internal class Result : ConstraintResult
			{

				private JsonValue ActualJsonValue { get; }

				private bool ReadOnly { get; }

				public Result(JsonReadOnlyConstraint constraint, JsonValue actual, bool hasSucceeded)
					: base(constraint, actual, hasSucceeded)
				{
					this.ReadOnly = constraint.ReadOnly;
					this.ActualJsonValue = actual;
				}

				public override void WriteMessageTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteMessageTo(writer);
						return;
					}

					writer.WriteLine(TextMessageWriter.Pfx_Expected + (this.ReadOnly ? "<read-only>" : "<mutable>"));
					writer.Write(TextMessageWriter.Pfx_Actual);
					WriteActualValueTo(writer);
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					writer.WriteLine(this.ActualJsonValue.IsReadOnly ? "<read-only>" : "<mutable>");
					writer.WriteMessageLine(Jsonify(this.ActualJsonValue));
				}

			}

		}

		internal sealed class JsonNotConstraint : PrefixConstraint
		{

			public JsonNotConstraint(IConstraint baseConstraint)
				: base(baseConstraint, "not")
			{ }

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var constraintResult = this.BaseConstraint.ApplyTo(actual);
				return new Result(this, constraintResult.ActualValue, constraintResult);
			}

			private class Result : ConstraintResult
			{

				private readonly ConstraintResult InnerResult;

				public Result(JsonNotConstraint constraint, object? actual, ConstraintResult result)
					: base(constraint, actual, !result.IsSuccess)
				{
					this.InnerResult = result;
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteActualValueTo(writer);
					}
					else
					{
						this.InnerResult.WriteActualValueTo(writer);
					}
				}

				public override void WriteAdditionalLinesTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteAdditionalLinesTo(writer);
					}
					else
					{
						this.InnerResult.WriteAdditionalLinesTo(writer);
					}
				}

			}

		}

		internal sealed class JsonNotOperator : PrefixOperator
		{

			/// <summary>Constructs a new NotOperator</summary>
			public JsonNotOperator() => this.left_precedence = this.right_precedence = 1;

			/// <summary>Returns a NotConstraint applied to its argument.</summary>
			public override IConstraint ApplyPrefix(IConstraint constraint)
			{
				return new JsonNotConstraint(constraint);
			}

		}

		internal sealed class JsonAndConstraint : BinaryConstraint
		{

			public JsonAndConstraint(IConstraint left, IConstraint right) : base(left, right) { }

			public override string Description => this.Left.Description + " and " + this.Right.Description;

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				var leftResult = this.Left.ApplyTo(actual);
				var rightResult = leftResult.IsSuccess ? this.Right.ApplyTo(actual) : new ConstraintResult(this.Right, actual);
				return new Result(this, actual, leftResult, rightResult);
			}

			private class Result : ConstraintResult
			{

				private readonly ConstraintResult LeftResult;

				private readonly ConstraintResult RightResult;

				public Result(JsonAndConstraint constraint, object? actual, ConstraintResult leftResult, ConstraintResult rightResult)
					: base(constraint, actual, leftResult.IsSuccess && rightResult.IsSuccess)
				{
					this.LeftResult = leftResult;
					this.RightResult = rightResult;
				}

				public override void WriteActualValueTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteActualValueTo(writer);
					}
					else if (!this.LeftResult.IsSuccess)
					{
						this.LeftResult.WriteActualValueTo(writer);
					}
					else
					{
						this.RightResult.WriteActualValueTo(writer);
					}
				}

				public override void WriteAdditionalLinesTo(MessageWriter writer)
				{
					if (this.IsSuccess)
					{
						base.WriteAdditionalLinesTo(writer);
					}
					else if (!this.LeftResult.IsSuccess)
					{
						this.LeftResult.WriteAdditionalLinesTo(writer);
					}
					else
					{
						this.RightResult.WriteAdditionalLinesTo(writer);
					}
				}

			}

		}

		internal sealed class JsonAndOperator : BinaryOperator
		{
			public JsonAndOperator() => this.left_precedence = this.right_precedence = 2;

			public override IConstraint ApplyOperator(IConstraint left, IConstraint right)
			{
				return new JsonAndConstraint(left, right);
			}
		}

		internal sealed class JsonOrConstraint : BinaryConstraint
		{

			public JsonOrConstraint(IConstraint left, IConstraint right) : base(left, right) { }

			public override string Description => this.Left.Description + " or " + this.Right.Description;

			public override ConstraintResult ApplyTo<TActual>(TActual actual)
			{
				bool isSuccess = this.Left.ApplyTo(actual).IsSuccess || this.Right.ApplyTo(actual).IsSuccess;
				return new ConstraintResult(this, actual, isSuccess);
			}

		}

		internal sealed class JsonOrOperator : BinaryOperator
		{
			public JsonOrOperator() => this.left_precedence = this.right_precedence = 2;

			public override IConstraint ApplyOperator(IConstraint left, IConstraint right)
			{
				return new JsonOrConstraint(left, right);
			}
		}
		
		[PublicAPI]
		public sealed class JsonConstraintExpression : IResolveConstraint
		{

			private ResolvableConstraintExpression Expression { get; }

			public JsonConstraintExpression() => this.Expression = new ResolvableConstraintExpression();

			public JsonConstraintExpression(ResolvableConstraintExpression expression) => this.Expression = expression;

			public JsonConstraintExpression(ConstraintBuilder builder) => this.Expression = new ResolvableConstraintExpression(builder);

			internal JsonConstraintExpression Append(ConstraintOperator op)
			{
				this.Expression.Append(op);
				return this;
			}

			internal JsonConstraintExpression Append(Constraint constraint)
			{
				this.Expression.Append(constraint);
				return this;
			}

			IConstraint IResolveConstraint.Resolve()
			{
				if (this.Expression is IResolveConstraint resolvable)
				{
					return resolvable.Resolve();
				}
				else
				{
					throw new NotSupportedException("Only resolvable constraint expressions are allowed!");
				}
			}

			private JsonConstraintExpression AddTypeConstraint(JsonType expected) => this.Append(new JsonTypeConstraint(expected));

			private JsonConstraintExpression AddSameConstraint(JsonValue expected) => this.Append(new JsonEqualConstraint(JsonComparisonOperator.SameAs, expected));

			private JsonConstraintExpression AddEqualConstraint(JsonValue expected) => this.Append(new JsonEqualConstraint(JsonComparisonOperator.Equal, expected));

			private JsonConstraintExpression AddTypedEqualConstraint<TValue>(TValue? expected, IEqualityComparer<TValue>? comparer = null)
				=> this.Append(new JsonEqualConstraint<TValue>(expected, comparer));

			private JsonConstraintExpression AddEqualConstraint(JsonComparisonOperator op, JsonValue expected) => this.Append(new JsonEqualConstraint(op, expected));

			public JsonConstraintExpression Not => this.Append(new JsonNotOperator());

			/// <summary>Combine two assertions that must both be true</summary>
			public JsonConstraintExpression And => this.Append(new JsonAndOperator());

			/// <summary>Combine two assertions with at least one that must be true</summary>
			public JsonConstraintExpression Or => this.Append(new JsonOrOperator());

			/// <summary>Assert that the value is null or missing</summary>
			/// <remarks>Includes all variants of <see cref="JsonNull"/></remarks>
			public JsonConstraintExpression Null => AddTypeConstraint(JsonType.Null);

			/// <summary>Assert that the value is a JSON Object</summary>
			public JsonConstraintExpression Object => AddTypeConstraint(JsonType.Object);

			/// <summary>Assert that the value is a empty JSON Object</summary>
			public JsonConstraintExpression EmptyObject => AddEqualConstraint(JsonObject.ReadOnly.Empty);

			/// <summary>Assert that the value is a JSON Array</summary>
			public JsonConstraintExpression Array => AddTypeConstraint(JsonType.Array);

			/// <summary>Assert that the value is an empty JSON Array</summary>
			public JsonConstraintExpression EmptyArray => AddEqualConstraint(JsonArray.ReadOnly.Empty);

			/// <summary>Assert that the value is a JSON String</summary>
			public JsonConstraintExpression String => AddTypeConstraint(JsonType.String);

			/// <summary>Assert that the value is a JSON Number</summary>
			public JsonConstraintExpression Number => AddTypeConstraint(JsonType.Number);

			/// <summary>Assert that the value is a JSON Boolean</summary>
			public JsonConstraintExpression Boolean => AddTypeConstraint(JsonType.Boolean);

			/// <summary>Assert that the value is an explicit JSON Null literal</summary>
			/// <remarks>This only accept the singleton <see cref="JsonNull.Null"/>. If you want to also include <see cref="JsonNull.Missing"/>, you should use the <see cref="Null"/> assertion.</remarks>
			public JsonConstraintExpression ExplicitNull => AddSameConstraint(JsonNull.Null);

			/// <summary>Assert that the value is missing</summary>
			/// <remarks>This only accept the singleton <see cref="JsonNull.Missing"/>. If you want to also include <see cref="JsonNull.Null"/>, you should use the <see cref="Null"/> assertion.</remarks>
			public JsonConstraintExpression Missing => AddSameConstraint(JsonNull.Missing);

			/// <summary>Assert that the value is out of bounds of its parent array.</summary>
			public JsonConstraintExpression Error => AddSameConstraint(JsonNull.Error);

			/// <summary>Assert that the value is an Array, Object or String that is empty</summary>
			public JsonConstraintExpression Empty => this.Append(new JsonSizeConstraint(0, ExpressionType.Equal));

			/// <summary>Assert that the value is an Array, Object or String with the specified size</summary>
			public JsonConstraintExpression OfSize(int expected) => this.Append(new JsonSizeConstraint(expected, ExpressionType.Equal));

			/// <summary>Assert that the value is an Array, Object or String with a size greater than or equal to the specified amount</summary>
			public JsonConstraintExpression OfSizeAtLeast(int expected) => this.Append(new JsonSizeConstraint(expected, ExpressionType.GreaterThanOrEqual));

			/// <summary>Assert that the value is an Array, Object or String with a size strictly greater than the specified amount</summary>
			public JsonConstraintExpression OfSizeGreaterThan(int expected) => this.Append(new JsonSizeConstraint(expected, ExpressionType.GreaterThan));

			/// <summary>Assert that the value is an Array, Object or String with a size less than or equal to the specified amount</summary>
			public JsonConstraintExpression OfSizeAtMost(int expected) => this.Append(new JsonSizeConstraint(expected, ExpressionType.LessThanOrEqual));

			/// <summary>Assert that the value is an Array, Object or String with a size strictly less than the specified amount</summary>
			public JsonConstraintExpression OfSizeLessThan(int expected) => this.Append(new JsonSizeConstraint(expected, ExpressionType.LessThan));

			/// <summary>Assert that the value is read-only.</summary>
			public JsonConstraintExpression ReadOnly => this.Append(new JsonReadOnlyConstraint(readOnly: true));

			/// <summary>Assert that the value is not read-only.</summary>
			public JsonConstraintExpression Mutable => this.Append(new JsonReadOnlyConstraint(readOnly: false));

			/// <summary>Assert that the value is <see cref="JsonBoolean.False"/></summary>
			public JsonConstraintExpression False => AddEqualConstraint(JsonBoolean.False);

			/// <summary>Assert that the value is <see cref="JsonBoolean.True"/></summary>
			public JsonConstraintExpression True => AddEqualConstraint(JsonBoolean.True);

			/// <summary>Assert that the value is <see cref="JsonNumber.Zero"/></summary>
			public JsonConstraintExpression Zero => AddEqualConstraint(JsonNumber.Zero);

			#region JsonValue...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(JsonValue expected) => AddEqualConstraint(expected);

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo(IEnumerable<JsonValue?> expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, expected);

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, expected);

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, expected);

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(JsonValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, expected);

			#endregion

			#region Generic...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonValue.FromValue(expected));

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo<TValue>(TValue[] expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo<TValue>(ReadOnlySpan<TValue> expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is a JSON Array with the expected content</summary>
			public JsonConstraintExpression EqualTo<TValue>(IEnumerable<TValue> expected) => AddEqualConstraint(JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonValue.FromValue(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonValue.FromValue(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonValue.FromValue(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo<TValue>(TValue expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonValue.FromValue(expected));

			#endregion

			#region String...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(string? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(string? expected, IEqualityComparer<string>? comparer) => AddTypedEqualConstraint(expected, comparer);

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<string?> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(string? expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonString.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(string? expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonString.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(string? expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonString.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(string? expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonString.Return(expected));

			#endregion

			#region Boolean...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(bool expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(bool? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<bool> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<bool?> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			#endregion

			#region Int32...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(int expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(int? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<int> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<int?> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(int expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(int expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(int expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(int expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Int64...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(long expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(long? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<long> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression EqualTo(ReadOnlySpan<long?> expected) => AddEqualConstraint(JsonComparisonOperator.Equal, JsonArray.FromValues(expected));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(long expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(long expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(long expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(long expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Single...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(float expected) => AddTypedEqualConstraint(expected, UlpsToleranceComparer.OneUlps);

			/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
			public JsonConstraintExpression EqualTo(float expected, IEqualityComparer<float>? comparer) => AddTypedEqualConstraint(expected, comparer);

			/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
			public JsonConstraintExpression EqualTo(float expected, float tolerance) => AddTypedEqualConstraint(expected, CreateToleranceComparer(tolerance));

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(float? expected) => AddTypedEqualConstraint(expected, UlpsToleranceComparer.OneUlps);

			/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
			public JsonConstraintExpression EqualTo(float? expected, float? tolerance) => AddTypedEqualConstraint(expected, CreateToleranceComparer(tolerance));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(float expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(float expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(float expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(float expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Double...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(double expected) => AddTypedEqualConstraint(expected, UlpsToleranceComparer.OneUlps);

			/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
			public JsonConstraintExpression EqualTo(double expected, double tolerance) => AddTypedEqualConstraint(expected, CreateToleranceComparer(tolerance));

			/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
			public JsonConstraintExpression EqualTo(double expected, IEqualityComparer<double>? comparer) => AddTypedEqualConstraint(expected, comparer);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(double? expected) => AddTypedEqualConstraint(expected, UlpsToleranceComparer.OneUlps);

			/// <summary>Assert that the value is equal to the expected value, within the given tolerance</summary>
			public JsonConstraintExpression EqualTo(double? expected, double? tolerance) => AddTypedEqualConstraint<double?>(expected, CreateToleranceComparer(tolerance));

			/// <summary>Assert that the value is strictly greater than the expected value</summary>
			public JsonConstraintExpression GreaterThan(double expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is greater than, or equal to, the expected value</summary>
			public JsonConstraintExpression GreaterThanOrEqualTo(double expected) => AddEqualConstraint(JsonComparisonOperator.GreaterThanOrEqual, JsonNumber.Return(expected));

			/// <summary>Assert that the value is strictly less than the expected value</summary>
			public JsonConstraintExpression LessThan(double expected) => AddEqualConstraint(JsonComparisonOperator.LessThan, JsonNumber.Return(expected));

			/// <summary>Assert that the value is less than, or equal to, the expected value</summary>
			public JsonConstraintExpression LessThanOrEqualTo(double expected) => AddEqualConstraint(JsonComparisonOperator.LessThanOrEqual, JsonNumber.Return(expected));

			#endregion

			#region Guids...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Guid expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Guid? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid128 expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid128? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid96 expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid96? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid80 expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid80? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid64 expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(Uuid64? expected) => AddTypedEqualConstraint(expected);

			#endregion

			#region Dates and Times...

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(DateTime expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(DateTime? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(DateTimeOffset expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(DateTimeOffset? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(DateOnly expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(DateOnly? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(TimeOnly expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(TimeOnly? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(NodaTime.Instant expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(NodaTime.Instant? expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(NodaTime.Duration expected) => AddTypedEqualConstraint(expected);

			/// <summary>Assert that the value is equal to the expected value</summary>
			public JsonConstraintExpression EqualTo(NodaTime.Duration? expected) => AddTypedEqualConstraint(expected);

			#endregion

		}

		#region Tolerance

#if NET8_0_OR_GREATER

		private static IEqualityComparer<T> CreateToleranceComparer<T>(T tolerance)
			where T : struct, System.Numerics.INumberBase<T>, System.Numerics.IComparisonOperators<T, T, bool>
			=> new WithinToleranceComparer<T>(tolerance);

		private static IEqualityComparer<T?> CreateToleranceComparer<T>(T? tolerance)
			where T : struct, System.Numerics.INumberBase<T>, System.Numerics.IComparisonOperators<T, T, bool>
			=> new WithinToleranceComparer<T>(tolerance);

		private sealed class WithinToleranceComparer<T> : IEqualityComparer<T>, IEqualityComparer<T?>
			where T : struct, System.Numerics.INumberBase<T>, System.Numerics.IComparisonOperators<T, T, bool>
		{

			private T? Tolerance { get; }

			public WithinToleranceComparer(T tolerance) => this.Tolerance = tolerance;

			public WithinToleranceComparer(T? tolerance) => this.Tolerance = tolerance;

			/// <inheritdoc />
			public bool Equals(T x, T y)
				=> T.Abs(x - y) <= (this.Tolerance ?? default);

			public bool Equals(T? x, T? y) =>
				x is null
					? (y is null && this.Tolerance is null)
					: y is not null && T.Abs(x.Value - y.Value) <= (this.Tolerance ?? default);

			int IEqualityComparer<T>.GetHashCode(T obj) => throw new NotSupportedException("This type is only expected to test for equality");

			int IEqualityComparer<T?>.GetHashCode(T? obj) => throw new NotSupportedException("This type is only expected to test for equality");

		}

#else

			private static IEqualityComparer<double> CreateToleranceComparer(double tolerance) => new WithinToleranceComparer(tolerance);

			private static IEqualityComparer<double?> CreateToleranceComparer(double? tolerance) => new WithinToleranceComparer(tolerance);

			private static IEqualityComparer<float> CreateToleranceComparer(float tolerance) => new WithinToleranceComparer(tolerance);

			private static IEqualityComparer<float?> CreateToleranceComparer(float? tolerance) => new WithinToleranceComparer(tolerance);

			private sealed class WithinToleranceComparer : IEqualityComparer<double>, IEqualityComparer<double?>, IEqualityComparer<float>, IEqualityComparer<float?>
			{

				private double? DoubleTolerance { get; }

				private float? FloatTolerance { get; }

				public WithinToleranceComparer(double? tolerance) => this.DoubleTolerance = tolerance;

				public WithinToleranceComparer(float? tolerance) => this.FloatTolerance = tolerance;

				/// <inheritdoc />
				public bool Equals(double x, double y) => Math.Abs(x - y) <= (this.DoubleTolerance ?? 0);

				/// <inheritdoc />
				public bool Equals(double? x, double? y) => x == null
					? (y == null && this.DoubleTolerance == null)
					: (y != null && Math.Abs(x.Value - y.Value) <= (this.DoubleTolerance ?? 0));

				/// <inheritdoc />
				public bool Equals(float x, float y) => Math.Abs(x - y) <= (this.FloatTolerance ?? 0);

				/// <inheritdoc />
				public bool Equals(float? x, float? y) => x == null
					? (y == null && this.FloatTolerance == null)
					: (y != null && Math.Abs(x.Value - y.Value) <= (this.FloatTolerance ?? 0));

				int IEqualityComparer<double>.GetHashCode(double obj) => throw new NotSupportedException("This type is only expected to test for equality");

				int IEqualityComparer<double?>.GetHashCode(double? obj) => throw new NotSupportedException("This type is only expected to test for equality");

				int IEqualityComparer<float>.GetHashCode(float obj) => throw new NotSupportedException("This type is only expected to test for equality");

				int IEqualityComparer<float?>.GetHashCode(float? obj) => throw new NotSupportedException("This type is only expected to test for equality");

			}

			private sealed class FloatToleranceComparer : IEqualityComparer<float>
			{

				private float Tolerance { get; }

				public FloatToleranceComparer(float tolerance) => this.Tolerance = tolerance;

				/// <inheritdoc />
				public bool Equals(float x, float y) => MathF.Abs(x - y) <= this.Tolerance;

				/// <inheritdoc />
				int IEqualityComparer<float>.GetHashCode(float obj) => throw new NotSupportedException("This type is only expected to test for equality");
			}

#endif


		#endregion

	}

	/// <summary>Extension method for extending NUnit's <see cref="NUnit.Framework.Is"/> constraint factory.</summary>
	[PublicAPI]
	public static class IsJsonExtensions
	{

		extension(NUnit.Framework.Is)
		{

			/// <summary>Provides assertions for testing JSON values</summary>
			[Pure]
			public static IsJson.JsonConstraintExpression Json => new();

		}

	}

}
