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

// ReSharper disable IdentifierTypo
// ReSharper disable PreferConcreteValueOverDefault
// ReSharper disable RedundantExplicitArrayCreation
// ReSharper disable RedundantCast
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable StringLiteralTypo

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable IDE0004 // Remove Unnecessary Cast
#pragma warning disable JSON001 // JSON issue: 'x' unexpected
#pragma warning disable NUnit2050 // NUnit 4 no longer supports string.Format specification

namespace SnowBank.Data.Json.Tests
{
	using System.Collections.Immutable;
	using System.IO.Compression;
#if !NETFRAMEWORK
	// System.Text.Json interop is disabled on the netstandard2.0 build of SnowBank.Core
	using System.Text.Json;
	using System.Text.Json.Serialization;
#endif
	using NodaTime;
	using SnowBank.Buffers;
	using SnowBank.Data.Tuples;
	using SnowBank.Runtime;

	public partial class CrystalJsonTest
	{
		// the netstandard2.0 build formats floating-point values with "R" (17 significant digits on netfx),
		// while the modern targets emit the shortest round-trippable form: compute the expected text at runtime
		private static readonly string PiDoubleText = Math.PI.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
		private static readonly string PiSingleText = ((float) Math.PI).ToString("R", System.Globalization.CultureInfo.InvariantCulture);


		private static Slice SerializeToSlice(JsonValue value)
		{
			var writer = new SliceWriter();
			value.WriteTo(ref writer);
			return writer.ToSlice();
		}


		[Test]
		public void Test_CrystalJsonWriter_WriteValue()
		{
			static string Execute(Action<CrystalJsonWriter> handler, CrystalJsonSettings? settings = null)
			{
				using var writer = new CrystalJsonWriter(0, settings ?? CrystalJsonSettings.Json, CrystalJson.DefaultResolver);
				handler(writer);
				var json = writer.GetString();
				Log(json);
				return json;
			}

			#region String-like

			// WriteValue(string)
			Assert.That(Execute((w) => w.WriteValue(default(string))), Is.EqualTo("null"));
			Assert.That(Execute((w) => w.WriteValue("")), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue("Hello, World!")), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue("Hello, \"World\"!")), Is.EqualTo(@"""Hello, \""World\""!"""));
			Assert.That(Execute((w) => w.WriteValue("\\o/")), Is.EqualTo(@"""\\o/"""));
			// WriteValue(StringBuilder)
			Assert.That(Execute((w) => w.WriteValue(default(StringBuilder))), Is.EqualTo("null"));
			Assert.That(Execute((w) => w.WriteValue(new StringBuilder())), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue(new StringBuilder().Append("Hello, ").Append("World!"))), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue(new StringBuilder().Append("Hello, ").Append("\"World\"!"))), Is.EqualTo(@"""Hello, \""World\""!"""));
			// WriteValue(ReadOnlySpan<char>)
			Assert.That(Execute((w) => w.WriteValue(default(ReadOnlySpan<char>))), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, World!***".AsSpan(3, 13))), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, \"World\"!***".AsSpan(3, 15))), Is.EqualTo(@"""Hello, \""World\""!"""));
			// WriteValue(ReadOnlyMemory<char>)
			Assert.That(Execute((w) => w.WriteValue(default(ReadOnlyMemory<char>))), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, World!***".AsMemory(3, 13))), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, \"World\"!***".AsMemory(3, 15))), Is.EqualTo(@"""Hello, \""World\""!"""));

			// WriteValue(Guid)
			Assert.That(Execute((w) => w.WriteValue(default(Guid))), Is.EqualTo("null")); // Guid.Empty is mapped to null/missing
			Assert.That(Execute((w) => w.WriteValue(Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"""8d6643a7-a84d-4eab-8394-0b349798bee2"""));
			Assert.That(Execute((w) => w.WriteValue((Guid?) Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"""8d6643a7-a84d-4eab-8394-0b349798bee2"""));

			#endregion

			#region Booleans...

			Assert.That(Execute(w => w.WriteValue(true)), Is.EqualTo("true"));
			Assert.That(Execute(w => w.WriteValue(false)), Is.EqualTo("false"));
			Assert.That(Execute(w => w.WriteValue(default(bool?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((bool?) true)), Is.EqualTo("true"));
			Assert.That(Execute(w => w.WriteValue((bool?) false)), Is.EqualTo("false"));

			#endregion

			#region Numbers...

			Assert.That(Execute(w => w.WriteValue(0)), Is.EqualTo("0"));
			Assert.That(Execute(w => w.WriteValue(1)), Is.EqualTo("1"));
			Assert.That(Execute(w => w.WriteValue(10)), Is.EqualTo("10"));
			Assert.That(Execute(w => w.WriteValue(-1)), Is.EqualTo("-1"));
			Assert.That(Execute(w => w.WriteValue(42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(42U)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(42L)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(42UL)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue((byte) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue((short) 42)), Is.EqualTo("42"));

			Assert.That(Execute(w => w.WriteValue(-42)), Is.EqualTo("-42"));
			Assert.That(Execute(w => w.WriteValue(-42L)), Is.EqualTo("-42"));
			Assert.That(Execute(w => w.WriteValue((sbyte) -42)), Is.EqualTo("-42"));
			Assert.That(Execute(w => w.WriteValue((short) -42)), Is.EqualTo("-42"));

			Assert.That(Execute(w => w.WriteValue(1234)), Is.EqualTo("1234"));
			Assert.That(Execute(w => w.WriteValue(1234U)), Is.EqualTo("1234"));
			Assert.That(Execute(w => w.WriteValue(1234L)), Is.EqualTo("1234"));
			Assert.That(Execute(w => w.WriteValue(1234UL)), Is.EqualTo("1234"));

			Assert.That(Execute(w => w.WriteValue(-1234)), Is.EqualTo("-1234"));
			Assert.That(Execute(w => w.WriteValue(-1234L)), Is.EqualTo("-1234"));

			Assert.That(Execute(w => w.WriteValue(int.MaxValue)), Is.EqualTo("2147483647"));
			Assert.That(Execute(w => w.WriteValue(int.MinValue)), Is.EqualTo("-2147483648"));
			Assert.That(Execute(w => w.WriteValue(uint.MaxValue)), Is.EqualTo("4294967295"));
			Assert.That(Execute(w => w.WriteValue(long.MaxValue)), Is.EqualTo("9223372036854775807"));
			Assert.That(Execute(w => w.WriteValue(long.MinValue)), Is.EqualTo("-9223372036854775808"));

			Assert.That(Execute(w => w.WriteValue(1f)), Is.EqualTo("1"));
			Assert.That(Execute(w => w.WriteValue((float) Math.PI)), Is.EqualTo(PiSingleText));

			Assert.That(Execute(w => w.WriteValue(1d)), Is.EqualTo("1"));
			Assert.That(Execute(w => w.WriteValue(Math.PI)), Is.EqualTo(PiDoubleText));
			Assert.That(Execute(w => w.WriteValue(double.MinValue)), Is.EqualTo("-1.7976931348623157E+308"));
			Assert.That(Execute(w => w.WriteValue(double.MaxValue)), Is.EqualTo("1.7976931348623157E+308"));
			Assert.That(Execute(w => w.WriteValue(double.Epsilon)), Is.EqualTo(double.Epsilon.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

			Assert.That(Execute(w => w.WriteValue(default(int?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((int?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(uint?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((uint?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(long?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((long?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(ulong?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((ulong?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(float?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((float?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(double?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((double?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(decimal?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((decimal?) 42)), Is.EqualTo("42"));

			Assert.That(Execute(w => w.WriteEnumInteger(DummyJsonEnumInt32.Two)), Is.EqualTo("2"));
			Assert.That(Execute(w => w.WriteEnumString(DummyJsonEnumInt32.Two)), Is.EqualTo("\"Two\""));
			Assert.That(Execute(w => w.WriteEnumInteger(DummyJsonEnumShort.Two)), Is.EqualTo("2"));
			Assert.That(Execute(w => w.WriteEnumString(DummyJsonEnumShort.Two)), Is.EqualTo("\"Two\""));
			Assert.That(Execute(w => w.WriteEnumInteger(DummyJsonEnumInt64.Two)), Is.EqualTo("2"));
			Assert.That(Execute(w => w.WriteEnumString(DummyJsonEnumInt64.Two)), Is.EqualTo("\"Two\""));

#if NET8_0_OR_GREATER
			Assert.That(Execute(w => w.WriteValue((Half) Math.PI)), Is.EqualTo("3.14"));
			Assert.That(Execute(w => w.WriteValue(default(Half?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((Half?) 42)), Is.EqualTo("42"));
#endif

			#endregion
		}

		[Test]
		public void Test_CrystalJsonWriter_WriteField()
		{
			static string Execute(Action<CrystalJsonWriter> handler, CrystalJsonSettings? settings = null)
			{
				using var writer = new CrystalJsonWriter(0, settings ?? CrystalJsonSettings.Json, CrystalJson.DefaultResolver);
				var state = writer.BeginObject();
				handler(writer);
				writer.EndObject(state);
				return writer.GetString();
			}

			#region String-like

			// string
			Assert.That(Execute((w) => w.WriteField("foo", default(string))), Is.EqualTo("{ }"));
			Assert.That(Execute((w) => w.WriteField("foo", "")), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "Hello, World!")), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "Hello, \"World\"!")), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "\\o/")), Is.EqualTo(@"{ ""foo"": ""\\o/"" }"));
			// StringBuilder
			Assert.That(Execute((w) => w.WriteField("foo", default(StringBuilder))), Is.EqualTo("{ }"));
			Assert.That(Execute((w) => w.WriteField("foo", new StringBuilder())), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", new StringBuilder().Append("Hello, ").Append("World!"))), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", new StringBuilder().Append("Hello, ").Append("\"World\"!"))), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			// ReadOnlySpan<char>
			Assert.That(Execute((w) => w.WriteField("foo", default(ReadOnlySpan<char>))), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, World!***".AsSpan(3, 13))), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, \"World\"!***".AsSpan(3, 15))), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			// ReadOnlyMemory<char>
			Assert.That(Execute((w) => w.WriteField("foo", default(ReadOnlyMemory<char>))), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, World!***".AsMemory(3, 13))), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, \"World\"!***".AsMemory(3, 15))), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			// Guid
			Assert.That(Execute((w) => w.WriteField("foo", default(Guid))), Is.EqualTo(@"{ ""foo"": null }")); // Guid.Empty is mapped to null/missing
			Assert.That(Execute((w) => w.WriteField("foo", Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"{ ""foo"": ""8d6643a7-a84d-4eab-8394-0b349798bee2"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", (Guid?) Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"{ ""foo"": ""8d6643a7-a84d-4eab-8394-0b349798bee2"" }"));

			#endregion

			#region Booleans...

			Assert.That(Execute(w => w.WriteField("foo", true)), Is.EqualTo(@"{ ""foo"": true }"));
			Assert.That(Execute(w => w.WriteField("foo", false)), Is.EqualTo(@"{ ""foo"": false }"));
			Assert.That(Execute(w => w.WriteField("foo", default(bool?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (bool?) true)), Is.EqualTo(@"{ ""foo"": true }"));
			Assert.That(Execute(w => w.WriteField("foo", (bool?) false)), Is.EqualTo(@"{ ""foo"": false }"));

			#endregion

			#region Numbers...

			Assert.That(Execute(w => w.WriteField("foo", 0)), Is.EqualTo(@"{ ""foo"": 0 }"));
			Assert.That(Execute(w => w.WriteField("foo", 1)), Is.EqualTo(@"{ ""foo"": 1 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42U)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42L)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42UL)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", (byte) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", (short) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", int.MaxValue)), Is.EqualTo(@"{ ""foo"": 2147483647 }"));
			Assert.That(Execute(w => w.WriteField("foo", int.MinValue)), Is.EqualTo(@"{ ""foo"": -2147483648 }"));
			Assert.That(Execute(w => w.WriteField("foo", uint.MaxValue)), Is.EqualTo(@"{ ""foo"": 4294967295 }"));
			Assert.That(Execute(w => w.WriteField("foo", long.MaxValue)), Is.EqualTo(@"{ ""foo"": 9223372036854775807 }"));
			Assert.That(Execute(w => w.WriteField("foo", long.MinValue)), Is.EqualTo(@"{ ""foo"": -9223372036854775808 }"));
			Assert.That(Execute(w => w.WriteField("foo", Math.PI)), Is.EqualTo(@"{ ""foo"": " + PiDoubleText + @" }"));
			Assert.That(Execute(w => w.WriteField("foo", (float) Math.PI)), Is.EqualTo(@"{ ""foo"": " + PiSingleText + @" }"));

			Assert.That(Execute(w => w.WriteField("foo", default(int?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (int?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(uint?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (uint?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(long?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (long?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(ulong?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (ulong?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(float?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (float?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(double?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (double?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(decimal?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (decimal?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
#if NET5_0_OR_GREATER
			Assert.That(Execute(w => w.WriteField("foo", default(Half?))), Is.EqualTo("{ }"));
#endif

#if NET8_0_OR_GREATER
			Assert.That(Execute(w => w.WriteField("foo", (Half) Math.PI)), Is.EqualTo(@"{ ""foo"": 3.14 }"));
			Assert.That(Execute(w => w.WriteField("foo", (Half?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
#endif

			#endregion

			#region Combo...

			Assert.That(Execute((_) => { }), Is.EqualTo("{ }"));

			Assert.That(Execute((w) =>
			{
				w.WriteField("foo", "hello");
				w.WriteField("bar", 123);
				w.WriteField("baz", true);
				w.WriteField("missing", default(string));
			}), Is.EqualTo("""{ "foo": "hello", "bar": 123, "baz": true }"""));

			#endregion
		}

		static void CheckSerialize<T>(
			T? value,
			CrystalJsonSettings? settings,
			string expected,
			string? message = null,
			[CallerArgumentExpression(nameof(value))] string valueExpression = "",
			[CallerArgumentExpression(nameof(settings))] string settingsExpression = "",
			[CallerArgumentExpression(nameof(expected))] string constraintExpression = ""
		)
		{
			settings ??= CrystalJsonSettings.Json;

			string? expr = value?.ToString();
			if (string.IsNullOrEmpty(expr))
			{
				expr = valueExpression;
			}

			if (value is IFormattable fmt)
			{
				Log($"# ({settings}) {typeof(T).GetFriendlyName()}: {fmt}");
			}
			else
			{
				Log($"# ({settings}) {typeof(T).GetFriendlyName()}");
			}

			var expectedSlice = Slice.FromStringUtf8(expected);

			{ // CrystalJson.Serialize<T>
				string actual = CrystalJson.Serialize<T>(value, settings);
				Log($"> `{actual}`");
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.Serialize)}({expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // CrystalJson.Serialize(object, Type)
				string actual = CrystalJson.Serialize(value, typeof(T), settings);
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.Serialize)}({expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // CrystalJson.ToSlice<T>
				Slice actualSlice = CrystalJson.ToSlice<T>(value, settings);
				if (!expectedSlice.Equals(actualSlice))
				{
					Assert.That(actualSlice.ToArray(), Is.EqualTo(expectedSlice.ToArray()), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.ToSlice)}({expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // CrystalJsonWriter.VisitValue<T>
				using var sw = new StringWriter();
				using (var writer = new CrystalJsonWriter(sw, 0, settings, CrystalJson.DefaultResolver))
				{
					writer.VisitValue(value);
				}
				string actual = sw.ToString();
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"(writer) => writer.{nameof(CrystalJsonWriter.VisitValue)}({expr})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // Output to TextWriter
				using var sw = new StringWriter();
				CrystalJson.SerializeTo(sw, value, settings);
				string actual = sw.ToString();
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.SerializeTo)}(StringWriter, {expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // Output to Stream
				using var ms = new MemoryStream();
				CrystalJson.SerializeTo(ms, value, settings);
				var actualStream = ms.ToArray();
				if (!expectedSlice.Equals(actualStream))
				{
					Assert.That(actualStream, Is.EqualTo(expectedSlice.ToArray()), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.SerializeTo)}(MemoryStream, {expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

		}

		[Test]
		public void Test_JsonSerialize_Null()
		{
			CheckSerialize<string>(null, default, "null");
			CheckSerialize<string>(null, CrystalJsonSettings.Json, "null");
			CheckSerialize<string>(null, CrystalJsonSettings.JsonCompact, "null");
		}

		[Test]
		public void Test_JsonSerialize_String_Types()
		{
			// trust, but verify...
			Assume.That(typeof(string).IsPrimitive, Is.False);

			// string
			CheckSerialize(string.Empty, default, "\"\"");
			CheckSerialize("foo", default, "\"foo\"");
			CheckSerialize("foo\"bar", default, "\"foo\\\"bar\"");
			CheckSerialize("foo'bar", default, "\"foo'bar\"");

			// StringBuilder
			CheckSerialize(new StringBuilder(), default, "\"\"");
			CheckSerialize(new StringBuilder("Foo"), default, "\"Foo\"");
			CheckSerialize(new StringBuilder("Foo").Append('"').Append("Bar"), default, "\"Foo\\\"Bar\"");
			CheckSerialize(new StringBuilder("Foo").Append('\'').Append("Bar"), default, "\"Foo'Bar\"");
		}

		[Test]
		public void Test_JavaScriptSerialize_String_Types()
		{
			// trust, but verify
			Assume.That(typeof(string).IsPrimitive, Is.False);

			// string
			CheckSerialize(string.Empty, CrystalJsonSettings.JavaScript, "''");
			CheckSerialize("foo", CrystalJsonSettings.JavaScript, "'foo'");
			CheckSerialize("foo\"bar", CrystalJsonSettings.JavaScript, "'foo\\x22bar'");
			CheckSerialize("foo'bar", CrystalJsonSettings.JavaScript, "'foo\\x27bar'");

			// StringBuilder
			CheckSerialize(new StringBuilder(), CrystalJsonSettings.JavaScript, "''");
			CheckSerialize(new StringBuilder("Foo"), CrystalJsonSettings.JavaScript, "'Foo'");
			CheckSerialize(new StringBuilder("Foo").Append('"').Append("Bar"), CrystalJsonSettings.JavaScript, "'Foo\\x22Bar'");
			CheckSerialize(new StringBuilder("Foo").Append('\'').Append("Bar"), CrystalJsonSettings.JavaScript, "'Foo\\x27Bar'");
		}

		[Test]
		public void Test_JsonSerialize_Primitive_Types()
		{
			// boolean
			CheckSerialize(true, default, "true");
			CheckSerialize(false, default, "false");

			// int32
			CheckSerialize((int)0, default, "0");
			CheckSerialize((int)1, default, "1");
			CheckSerialize((int)-1, default, "-1");
			CheckSerialize((int)123, default, "123");
			CheckSerialize((int)-999, default, "-999");
			CheckSerialize(int.MaxValue, default, "2147483647");
			CheckSerialize(int.MinValue, default, "-2147483648");

			// int64
			CheckSerialize((long)0, default, "0");
			CheckSerialize((long)1, default, "1");
			CheckSerialize((long)-1, default, "-1");
			CheckSerialize((long)123, default, "123");
			CheckSerialize((long)-999, default, "-999");
			CheckSerialize(long.MaxValue, default, "9223372036854775807");
			CheckSerialize(long.MinValue, default, "-9223372036854775808");

			// single
			CheckSerialize(0f, default, "0");
			CheckSerialize(1f, default, "1");
			CheckSerialize(-1f, default, "-1");
			CheckSerialize(123f, default, "123");
			CheckSerialize(123.456f, default, "123.456");
			CheckSerialize(-999.9f, default, "-999.9");
			CheckSerialize(float.MaxValue, default, float.MaxValue.ToString("R"));
			CheckSerialize(float.MinValue, default, float.MinValue.ToString("R"));
			CheckSerialize(float.Epsilon, default, float.Epsilon.ToString("R"));
			CheckSerialize(float.NaN, default, "NaN", "Not a standard, but most serializers behave like this");
			CheckSerialize(float.PositiveInfinity, default, "Infinity", "Not a standard, but most serializers behave like this");
			CheckSerialize(float.NegativeInfinity, default, "-Infinity", "Not a standard, but most serializers behave like this");
			{ // NaN => 'NaN'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Symbol);
				CheckSerialize(float.NaN, settings, "NaN", "Not a standard, but most serializers behave like this");
				CheckSerialize(float.PositiveInfinity, settings, "Infinity", "Not a standard, but most serializers behave like this");
				CheckSerialize(float.NegativeInfinity, settings, "-Infinity", "Not a standard, but most serializers behave like this");
			}
			{ // NaN => '"NaN"'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.String);
				CheckSerialize(float.NaN, settings, "\"NaN\"", "Comme le fait JSON.Net");
				CheckSerialize(float.PositiveInfinity, settings, "\"Infinity\"", "Comme le fait JSON.Net");
				CheckSerialize(float.NegativeInfinity, settings, "\"-Infinity\"", "Comme le fait JSON.Net");
			}
			{ // NaN => 'null'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Null);
				CheckSerialize(float.NaN, settings, "null", "A défaut d'autre chose...");
				CheckSerialize(float.PositiveInfinity, settings, "null", "A défaut d'autre chose...");
				CheckSerialize(float.NegativeInfinity, settings, "null", "A défaut d'autre chose...");
			}

			// double
			CheckSerialize(0d, default, "0");
			CheckSerialize(1d, default, "1");
			CheckSerialize(-1d, default, "-1");
			CheckSerialize(123d, default, "123");
			CheckSerialize(123.456d, default, "123.456");
			CheckSerialize(-999.9d, default, "-999.9");
			CheckSerialize(double.MaxValue, default, double.MaxValue.ToString("R"));
			CheckSerialize(double.MinValue, default, double.MinValue.ToString("R"));
			CheckSerialize(double.Epsilon, default, double.Epsilon.ToString("R"));
			CheckSerialize(double.NaN, default, "NaN", "Not a standard, but most serializers behave like this");
			CheckSerialize(double.PositiveInfinity, default, "Infinity", "Not a standard, but most serializers behave like this");
			CheckSerialize(double.NegativeInfinity, default, "-Infinity", "Not a standard, but most serializers behave like this");
			{ // NaN => 'NaN'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Symbol);
				CheckSerialize(double.NaN, settings, "NaN", "Not a standard, but most serializers behave like this");
				CheckSerialize(double.PositiveInfinity, settings, "Infinity", "Not a standard, but most serializers behave like this");
				CheckSerialize(double.NegativeInfinity, settings, "-Infinity", "Not a standard, but most serializers behave like this");
			}
			{ // NaN => '"NaN"'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.String);
				CheckSerialize(double.NaN, settings, "\"NaN\"", "Comme le fait JSON.Net");
				CheckSerialize(double.PositiveInfinity, settings, "\"Infinity\"", "Comme le fait JSON.Net");
				CheckSerialize(double.NegativeInfinity, settings, "\"-Infinity\"", "Comme le fait JSON.Net");
			}
			{ // NaN => 'null'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Null);
				CheckSerialize(double.NaN, settings, "null"); // by convention
				CheckSerialize(double.PositiveInfinity, settings, "null"); // by convention
				CheckSerialize(double.NegativeInfinity, settings, "null"); // by convention
			}

			// char
			CheckSerialize('A', default, "\"A\"");
			CheckSerialize('\0', default, "null");
			CheckSerialize('\"', default, "\"\\\"\"");

			// JavaScript exceptions:
			CheckSerialize(double.NaN, CrystalJsonSettings.JavaScript, "Number.NaN"); // Not standard, but most serializers behave like this
			CheckSerialize(double.PositiveInfinity, CrystalJsonSettings.JavaScript, "Number.POSITIVE_INFINITY"); // Not standard, but most serializers behave like this
			CheckSerialize(double.NegativeInfinity, CrystalJsonSettings.JavaScript, "Number.NEGATIVE_INFINITY"); // Not standard, but most serializers behave like this

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				CheckSerialize(i, default, i.ToString());
				CheckSerialize(-i, default, (-i).ToString());

				int x = rnd.Next() * (rnd.Next(2) == 1 ? -1 : 1);
				CheckSerialize(x, default, x.ToString());
				CheckSerialize((uint) x, default, ((uint) x).ToString());

				long y = (long)x * rnd.Next() * (rnd.Next(2) == 1 ? -1L : 1L);
				CheckSerialize(y, default, y.ToString());
				CheckSerialize((ulong) y, default, ((ulong) y).ToString());
			}
		}

		[Test]
		public void Test_JsonValue_ToString_Formattable()
		{
			JsonValue num = 123;
			JsonValue flg = true;
			JsonValue txt = "Hello\"World";
			JsonValue dtz = new DateTime(1969, 7, 20, 13, 32, 00, DateTimeKind.Utc);
			JsonValue dto = new DateTimeOffset(1969, 7, 20, 9, 32, 00, TimeSpan.FromHours(4));
			JsonValue arr = JsonArray.FromValues(Enumerable.Range(1, 20));
			JsonValue obj = JsonObject.Create(
			[
				("Foo", 123),
				("Bar", "Narf Zort!"),
				("Baz", JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ])),
				("Jazz", JsonArray.FromValues(Enumerable.Range(1, 5)))
			]);

			// "D" = Default
			Assert.That(num.ToString("D"), Is.EqualTo("123"));
			Assert.That(flg.ToString("D"), Is.EqualTo("true"));
			Assert.That(txt.ToString("D"), Is.EqualTo("Hello\"World"), "Strings should not be encoded in default format");
			Assert.That(dtz.ToString("D"), Is.EqualTo("1969-07-20T13:32:00Z"));
			Assert.That(dto.ToString("D"), Is.EqualTo("1969-07-20T09:32:00+04:00"));
			Assert.That(arr.ToString("D"), Is.EqualTo("[ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]"));
			Assert.That(obj.ToString("D"), Is.EqualTo(@"{ ""Foo"": 123, ""Bar"": ""Narf Zort!"", ""Baz"": { ""X"": 1, ""Y"": 2, ""Z"": 3 }, ""Jazz"": [ 1, 2, 3, 4, 5 ] }"));

			// "" => "D"
			Assert.That(num.ToString(), Is.EqualTo("123"));
			Assert.That(flg.ToString(), Is.EqualTo("true"));
			Assert.That(txt.ToString(), Is.EqualTo("Hello\"World"), "Strings should not be encoded in default format");
			Assert.That(dtz.ToString(), Is.EqualTo("1969-07-20T13:32:00Z"));
			Assert.That(dto.ToString(), Is.EqualTo("1969-07-20T09:32:00+04:00"));
			Assert.That(arr.ToString(), Is.EqualTo("[ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]"));
			Assert.That(obj.ToString(), Is.EqualTo(@"{ ""Foo"": 123, ""Bar"": ""Narf Zort!"", ""Baz"": { ""X"": 1, ""Y"": 2, ""Z"": 3 }, ""Jazz"": [ 1, 2, 3, 4, 5 ] }"));

			// "N" = Normal (=> ToJson)
			Assert.That(num.ToString("N"), Is.EqualTo("123"));
			Assert.That(flg.ToString("N"), Is.EqualTo("true"));
			Assert.That(txt.ToString("N"), Is.EqualTo(@"""Hello\""World"""));
			Assert.That(dtz.ToString("N"), Is.EqualTo("\"1969-07-20T13:32:00Z\""));
			Assert.That(dto.ToString("N"), Is.EqualTo("\"1969-07-20T09:32:00+04:00\""));
			Assert.That(arr.ToString("N"), Is.EqualTo("[ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]"));
			Assert.That(obj.ToString("N"), Is.EqualTo(@"{ ""Foo"": 123, ""Bar"": ""Narf Zort!"", ""Baz"": { ""X"": 1, ""Y"": 2, ""Z"": 3 }, ""Jazz"": [ 1, 2, 3, 4, 5 ] }"));

			// "C" = Compact (=> ToJsonCompact)
			Assert.That(num.ToString("C"), Is.EqualTo("123"));
			Assert.That(flg.ToString("C"), Is.EqualTo("true"));
			Assert.That(txt.ToString("C"), Is.EqualTo(@"""Hello\""World"""));
			Assert.That(dtz.ToString("C"), Is.EqualTo("\"1969-07-20T13:32:00Z\""));
			Assert.That(dto.ToString("C"), Is.EqualTo("\"1969-07-20T09:32:00+04:00\""));
			Assert.That(arr.ToString("C"), Is.EqualTo("[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]"));
			Assert.That(obj.ToString("C"), Is.EqualTo(@"{""Foo"":123,""Bar"":""Narf Zort!"",""Baz"":{""X"":1,""Y"":2,""Z"":3},""Jazz"":[1,2,3,4,5]}"));

			// "P" = Prettified (=> ToJsonIndented)
			Assert.That(num.ToString("P"), Is.EqualTo("123"));
			Assert.That(flg.ToString("P"), Is.EqualTo("true"));
			Assert.That(txt.ToString("P"), Is.EqualTo(@"""Hello\""World"""));
			Assert.That(dtz.ToString("P"), Is.EqualTo("\"1969-07-20T13:32:00Z\""));
			Assert.That(dto.ToString("P"), Is.EqualTo("\"1969-07-20T09:32:00+04:00\""));
			Assert.That(arr.ToString("P"), Is.EqualTo("[\r\n\t1,\r\n\t2,\r\n\t3,\r\n\t4,\r\n\t5,\r\n\t6,\r\n\t7,\r\n\t8,\r\n\t9,\r\n\t10,\r\n\t11,\r\n\t12,\r\n\t13,\r\n\t14,\r\n\t15,\r\n\t16,\r\n\t17,\r\n\t18,\r\n\t19,\r\n\t20\r\n]"));
			Assert.That(obj.ToString("P"), Is.EqualTo("{\r\n\t\"Foo\": 123,\r\n\t\"Bar\": \"Narf Zort!\",\r\n\t\"Baz\": {\r\n\t\t\"X\": 1,\r\n\t\t\"Y\": 2,\r\n\t\t\"Z\": 3\r\n\t},\r\n\t\"Jazz\": [\r\n\t\t1,\r\n\t\t2,\r\n\t\t3,\r\n\t\t4,\r\n\t\t5\r\n\t]\r\n}"));

			// "Q" = Quick (=> GetCompactRepresentation)
			Assert.That(num.ToString("Q"), Is.EqualTo("123"));
			Assert.That(flg.ToString("Q"), Is.EqualTo("true"));
			Assert.That(txt.ToString("Q"), Is.EqualTo(@"'Hello""World'"));
			Assert.That(arr.ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 16 more */ ]"));
			Assert.That(obj.ToString("Q"), Is.EqualTo("{ Foo: 123, Bar: 'Narf Zort!', Baz: { X: 1, Y: 2, Z: 3 }, Jazz: [ 1, 2, 3, /* … 2 more */ ] }"));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4 ]"));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4, 5 ]).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, 5 ]"));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4, 5, 6 ]).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 2 more */ ]"));
			Assert.That(JsonArray.FromValues(Enumerable.Range(1, 60)).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 56 more */ ]"));
			Assert.That(
				JsonArray.Create(
					"This is a test of the emergency broadcast system!",
					JsonArray.Create("This is a test of the emergency broadcast system!"),
					JsonArray.Create(JsonArray.Create("This is a test of the emergency broadcast system!")),
					JsonArray.Create(JsonArray.Create(JsonArray.Create("This is a test of the emergency broadcast system!")))
				).ToString("Q"),
				Is.EqualTo("[ 'This is a test of the emergency broadcast system!', [ 'This is a test of the e[…]gency broadcast system!' ], [ [ 'This is a test of[…]broadcast system!' ] ], [ [ [ '…' ] ] ] ]")
			);
			Assert.That(
				JsonArray.Create(
					JsonArray.Create(1, 2, 3),
					JsonArray.FromValues(Enumerable.Range(1, 60)),
					JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.FromValues(Enumerable.Range(1, 60)))
				).ToString("Q"),
				Is.EqualTo("[ [ 1, 2, 3 ], [ 1, 2, 3, /* … 57 more */ ], [ [ 1, 2, 3 ], [ /* 60 Numbers */ ] ] ]")
			);

			// "J" = Javascript
			Assert.That(num.ToString("J"), Is.EqualTo("123"));
			Assert.That(flg.ToString("J"), Is.EqualTo("true"));
			Assert.That(txt.ToString("J"), Is.EqualTo(@"'Hello\x22World'"));
			Assert.That(arr.ToString("J"), Is.EqualTo("[ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]"));
			Assert.That(obj.ToString("J"), Is.EqualTo(@"{ Foo: 123, Bar: 'Narf Zort\x21', Baz: { X: 1, Y: 2, Z: 3 }, Jazz: [ 1, 2, 3, 4, 5 ] }"));
		}

		[Test]
		public void Test_JsonValue_Required_Of_T()
		{
			//Value Type
			Assert.That(JsonNumber.Return(123).Required<int>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").Required<int>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(() => JsonNull.Null.Required<int>(), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => JsonNull.Null.Required<int>(null), Throws.InstanceOf<JsonBindingException>());

			//Nullable Type: should induce a compiler warning on '<int?>' since the method can never return null, so expecting a nullable int is probably a mistake ?
			Assert.That(JsonNumber.Return(123).Required<int?>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").Required<int?>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(() => JsonNull.Null.Required<int?>(), Throws.InstanceOf<JsonBindingException>());

			//Reference Primitive Type
			Assert.That(JsonNumber.Return(123).Required<string>(), Is.Not.Null.And.EqualTo("123"));
			Assert.That(JsonString.Return("123").Required<string>(), Is.Not.Null.And.EqualTo("123"));
			Assert.That(() => JsonNull.Null.Required<string>(), Throws.InstanceOf<JsonBindingException>());

			//Value Type Array
			Assert.That(JsonArray.Create(1, 2, 3).Required<int[]>(), Is.Not.Null.And.EqualTo(new [] { 1, 2, 3 }));
			Assert.That(() => JsonNull.Null.Required<int[]>(), Throws.InstanceOf<JsonBindingException>());

			//Ref Type Array
			Assert.That(JsonArray.Create("a", "b", "c").Required<string[]>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
			Assert.That(() => JsonNull.Null.Required<string[]>(), Throws.InstanceOf<JsonBindingException>());

			//Value Type List
			Assert.That(JsonArray.Create(1, 2, 3).Required<List<int>>(), Is.Not.Null.And.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(() => JsonNull.Null.Required<List<int>>(), Throws.InstanceOf<JsonBindingException>());

			//Ref Type List
			Assert.That(JsonArray.Create("a", "b", "c").Required<List<string>>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
			Assert.That(() => JsonNull.Null.Required<List<string>>(), Throws.InstanceOf<JsonBindingException>());

			//Format Exceptions
			Assert.That(() => JsonString.Return("foo").Required<int>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonArray.Create("foo").Required<int[]>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonArray.Create("foo").Required<List<int>>(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_JsonValue_As_Of_T()
		{
			var guid = Guid.NewGuid();
			var now = DateTime.Now;

			//Value Type

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Return(123).As<int>(), Is.EqualTo(123));
				Assert.That(JsonString.Return("123").As<int>(), Is.EqualTo(123));
				Assert.That(JsonNumber.Return(123).As<int>(456), Is.EqualTo(123));
				Assert.That(JsonBoolean.False.As<bool>(), Is.False);
				Assert.That(JsonBoolean.True.As<bool>(), Is.True);
				Assert.That(JsonString.Return(guid).As<Guid>(), Is.EqualTo(guid));
				// As<T>()
				Assert.That(JsonDateTime.Return(now).As<DateTime>(), Is.EqualTo(now));
				Assert.That(JsonNull.Null.As<string>(), Is.Null);
				Assert.That(JsonNull.Null.As<string>("hello"), Is.EqualTo("hello"));
				Assert.That(JsonNull.Null.As<string>(null), Is.Null);
				Assert.That(JsonNull.Null.As<int>(), Is.EqualTo(0));
				Assert.That(JsonNull.Null.As<int>(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As<int?>(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As<int?>(null), Is.Null);
				Assert.That(JsonNull.Null.As<bool>(), Is.False);
				Assert.That(JsonNull.Null.As<bool>(false), Is.False);
				Assert.That(JsonNull.Null.As<bool>(true), Is.True);
				Assert.That(JsonNull.Null.As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(JsonNull.Null.As<Guid>(guid), Is.EqualTo(guid));
				Assert.That(JsonNull.Null.As<DateTime>(), Is.EqualTo(DateTime.MinValue));
				Assert.That(JsonNull.Null.As<DateTime>(now), Is.EqualTo(now));
				// As() non generic
				Assert.That(JsonNull.Null.As("hello"), Is.EqualTo("hello"));
				Assert.That(JsonNull.Null.As(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As(false), Is.False);
				Assert.That(JsonNull.Null.As(true), Is.True);
				Assert.That(JsonNull.Null.As(guid), Is.EqualTo(guid));
				Assert.That(JsonNull.Null.As(now), Is.EqualTo(now));
				Assert.That(default(JsonValue).As<int>(), Is.EqualTo(0));
				Assert.That(default(JsonValue).As<int>(123), Is.EqualTo(123));
				Assert.That(default(JsonValue).As<bool>(), Is.False);
				Assert.That(default(JsonValue).As<bool>(false), Is.False);
				Assert.That(default(JsonValue).As<bool>(true), Is.True);
				Assert.That(default(JsonValue).As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(default(JsonValue).As<Guid>(guid), Is.EqualTo(guid));
				Assert.That(default(JsonValue).As<DateTime>(), Is.EqualTo(DateTime.MinValue));
				Assert.That(default(JsonValue).As<DateTime>(now), Is.EqualTo(now));
			}

			//Nullable Type
			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Return(123).As<int?>(), Is.Not.Null.And.EqualTo(123));
				Assert.That(JsonString.Return("123").As<int?>(), Is.Not.Null.And.EqualTo(123));
				Assert.That(JsonNumber.Return(123).As<int?>(456), Is.Not.Null.And.EqualTo(123));
				Assert.That(JsonBoolean.True.As<bool?>(), Is.True);
				Assert.That(JsonNull.Null.As<int?>(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As<int?>(), Is.Null);
				Assert.That(JsonNull.Null.As<bool?>(), Is.Null);
				Assert.That(JsonNull.Null.As<bool?>(false), Is.False);
				Assert.That(JsonNull.Null.As<bool?>(true), Is.True);
				Assert.That(JsonNull.Null.As<Guid?>(), Is.Null);
				Assert.That(JsonNull.Null.As<Guid?>(guid), Is.EqualTo(guid));
				Assert.That(default(JsonValue).As<int?>(null), Is.Null);
				Assert.That(default(JsonValue).As<int?>(123), Is.EqualTo(123));
				Assert.That(default(JsonValue).As<bool?>(), Is.Null);
				Assert.That(default(JsonValue).As<bool?>(false), Is.False);
				Assert.That(default(JsonValue).As<bool?>(true), Is.True);
				Assert.That(default(JsonValue).As<Guid?>(), Is.Null);
				Assert.That(default(JsonValue).As<Guid?>(guid), Is.EqualTo(guid));
			}

			//Reference Primitive Type

			using (Assert.EnterMultipleScope())
			{
				//Nullable Type
				Assert.That(JsonNumber.Return(123).As<string>(), Is.Not.Null.And.EqualTo("123"));
				Assert.That(JsonString.Return("123").As<string>(), Is.Not.Null.And.EqualTo("123"));
				Assert.That(JsonNull.Null.As<string>(), Is.Null);
				Assert.That(JsonNull.Null.As<string>("not_found"), Is.EqualTo("not_found"));
				Assert.That(default(JsonValue).As<string>(), Is.Null);
				Assert.That(default(JsonValue).As<string>("not_found"), Is.EqualTo("not_found"));

				//Value Type Array
				Assert.That(JsonArray.Create(1, 2, 3).As<int[]>(), Is.Not.Null.And.EqualTo(new [] { 1, 2, 3 }));
				Assert.That(JsonNull.Null.As<int[]>(), Is.Null);
				Assert.That(JsonNull.Null.As<int[]>([ 1, 2, 3 ]), Is.EqualTo([ 1, 2, 3 ]));
				Assert.That(default(JsonValue).As<int[]>(), Is.Null);
				Assert.That(default(JsonValue).As<int[]>([ 1, 2, 3 ]), Is.EqualTo([ 1, 2, 3 ]));

				//Ref Type Array
				Assert.That(JsonArray.Create("a", "b", "c").As<string[]>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
				Assert.That(JsonNull.Null.As<string[]>(), Is.Null);
				Assert.That(JsonNull.Null.As<string[]>([ "a", "b", "c" ]), Is.EqualTo([ "a", "b", "c" ]));
				Assert.That(default(JsonValue).As<string[]>(), Is.Null);
				Assert.That(default(JsonValue).As<string[]>([ "a", "b", "c" ]), Is.EqualTo([ "a", "b", "c" ]));

				//Value Type List
				Assert.That(JsonArray.Create(1, 2, 3).As<List<int>>(), Is.Not.Null.And.EqualTo(new[] { 1, 2, 3 }));
				Assert.That(JsonNull.Null.As<List<int>>(), Is.Null);

				//Ref Type List
				Assert.That(JsonArray.Create("a", "b", "c").As<List<string>>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
				Assert.That(JsonNull.Null.As<List<string>>(), Is.Null);
			}

			// JsonNull
			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNull.Null.As<JsonValue>(), Is.SameAs(JsonNull.Null));
				Assert.That(JsonNull.Null.As<JsonNull>(), Is.SameAs(JsonNull.Null));
				Assert.That(JsonNull.Missing.As<JsonValue>(), Is.SameAs(JsonNull.Missing));
				Assert.That(JsonNull.Missing.As<JsonNull>(), Is.SameAs(JsonNull.Missing));
				Assert.That(default(JsonValue).As<JsonValue>(), Is.SameAs(JsonNull.Null));
				Assert.That(default(JsonValue).As<JsonNull>(), Is.SameAs(JsonNull.Null));
			}

			//Format Exceptions
			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => JsonString.Return("foo").As<int>(), Throws.InstanceOf<FormatException>());
				Assert.That(() => JsonArray.Create("foo").As<int[]>(), Throws.InstanceOf<FormatException>());
				Assert.That(() => JsonArray.Create("foo").As<List<int>>(), Throws.InstanceOf<FormatException>());
			}
		}

		[Test]
		public void Test_JsonValue_AsObject()
		{
			{ // null
				JsonValue? value = null;
				Assert.That(() => value.AsObject(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsObjectOrDefault(), Is.Null);
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(JsonObject.ReadOnly.Empty));
			}
			{ // JsonNull
				JsonValue value = JsonNull.Null;
				Assert.That(() => value.AsObject(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsObjectOrDefault(), Is.Null);
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(JsonObject.ReadOnly.Empty));
			}
			{ // empty object
				JsonValue value = JsonObject.Create();
				Assert.That(value.AsObject(), Is.SameAs(value));
				Assert.That(value.AsObjectOrDefault(), Is.SameAs(value));
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(value));
			}
			{ // non empty object
				JsonValue value = JsonObject.Create("hello", "world");
				Assert.That(value.AsObject(), Is.SameAs(value));
				Assert.That(value.AsObjectOrDefault(), Is.SameAs(value));
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(value));
			}
			{ // not an object
				JsonValue value = JsonArray.Create("hello", "world");
				Assert.That(() => value.AsObject(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsObjectOrDefault(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsObjectOrEmpty(), Throws.InstanceOf<JsonBindingException>());
			}
		}

		[Test]
		public void Test_JsonValue_GetObject()
		{
			var foo = JsonObject.Create();
			var bar = JsonObject.Create([ ("x", 1), ("y", 2), ("z", 3) ]);
			{
				var obj = JsonObject.Create(
				[
					("foo", foo),
					("bar", bar),
					("baz", JsonNull.Null),
					("other", JsonArray.Create([ "hello", "world" ])),
					("text", "hello, there!"),
					("number", 123),
					("boolean", true),
				]);

				{ // GetObject()
					Assert.That(obj.GetObject("foo"), Is.SameAs(foo));
					Assert.That(obj.GetObject("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetObject("baz"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("not_found"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetObjectOrDefault()
					Assert.That(obj.GetObjectOrDefault("foo"), Is.SameAs(foo));
					Assert.That(obj.GetObjectOrDefault("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetObjectOrDefault("baz"), Is.Null);
					Assert.That(() => obj.GetObjectOrDefault("not_found"), Is.Null);
					Assert.That(() => obj.GetObjectOrDefault("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrDefault("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrDefault("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrDefault("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetObjectOrEmpty()
					Assert.That(obj.GetObjectOrEmpty("foo"), Is.SameAs(foo));
					Assert.That(obj.GetObjectOrEmpty("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetObjectOrEmpty("baz"), Is.SameAs(JsonObject.ReadOnly.Empty));
					Assert.That(() => obj.GetObjectOrEmpty("not_found"), Is.SameAs(JsonObject.ReadOnly.Empty));
					Assert.That(() => obj.GetObjectOrEmpty("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrEmpty("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrEmpty("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrEmpty("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
			}
			{
				var arr = new JsonArray()
				{
					/*0*/ foo,
					/*1*/ bar,
					/*2*/ JsonNull.Null,
					/*3*/ JsonArray.Create("hello", "world"),
					/*4*/ "hello, there!",
					/*5*/ 123,
					/*6*/ true,
				};

				{ // GetArray()
					Assert.That(arr.GetObject(0), Is.SameAs(foo));
					Assert.That(arr.GetObject(1), Is.SameAs(bar));
					Assert.That(() => arr.GetObject(2), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(6), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(arr.GetObjectOrDefault(0), Is.SameAs(foo));
					Assert.That(arr.GetObjectOrDefault(1), Is.SameAs(bar));
					Assert.That(() => arr.GetObjectOrDefault(2), Is.Null);
					Assert.That(() => arr.GetObjectOrDefault(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrDefault(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrDefault(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrDefault(6), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrEmpty()
					Assert.That(arr.GetObjectOrEmpty(0), Is.SameAs(foo));
					Assert.That(arr.GetObjectOrEmpty(1), Is.SameAs(bar));
					Assert.That(() => arr.GetObjectOrEmpty(2), Is.SameAs(JsonObject.ReadOnly.Empty));
					Assert.That(() => arr.GetObjectOrEmpty(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrEmpty(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrEmpty(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrEmpty(6), Throws.InstanceOf<JsonBindingException>());
				}
			}
		}

		[Test]
		public void Test_JsonValue_AsArray()
		{
			{ // null
				JsonValue? value = null;
				Assert.That(() => value.AsArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsArrayOrDefault(), Is.Null);
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(JsonArray.ReadOnly.Empty));
			}
			{ // JsonNull
				JsonValue value = JsonNull.Null;
				Assert.That(() => value.AsArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsArrayOrDefault(), Is.Null);
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(JsonArray.ReadOnly.Empty));
			}
			{ // empty array
				JsonValue value = JsonArray.Create();
				Assert.That(value.AsArray(), Is.SameAs(value));
				Assert.That(value.AsArrayOrDefault(), Is.SameAs(value));
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(value));
			}
			{ // non empty array
				JsonValue value = JsonArray.Create("hello", "world");
				Assert.That(value.AsArray(), Is.SameAs(value));
				Assert.That(value.AsArrayOrDefault(), Is.SameAs(value));
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(value));
			}
			{ // not an array
				JsonValue value = JsonObject.Create("hello", "world");
				Assert.That(() => value.AsArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsArrayOrDefault(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsArrayOrEmpty(), Throws.InstanceOf<JsonBindingException>());
			}
		}

		[Test]
		public void Test_JsonValue_GetArray()
		{
			var foo = JsonArray.Create();
			var bar = JsonArray.Create("x", 1, "y", 2, "z", 3);
			{
				var obj = new JsonObject()
				{
					["foo"] = foo,
					["bar"] = bar,
					["baz"] = JsonNull.Null,
					["other"] = JsonObject.Create("hello", "world"),
					["text"] = "hello, there!",
					["number"] = 123,
					["boolean"] = true,
				};

				{ // GetArray()
					Assert.That(obj.GetArray("foo"), Is.SameAs(foo));
					Assert.That(obj.GetArray("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetArray("baz"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("not_found"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(obj.GetArrayOrDefault("foo"), Is.SameAs(foo));
					Assert.That(obj.GetArrayOrDefault("bar"), Is.SameAs(bar));
					Assert.That(obj.GetArrayOrDefault("baz"), Is.Null);
					Assert.That(obj.GetArrayOrDefault("not_found"), Is.Null);
					Assert.That(() => obj.GetArrayOrDefault("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrDefault("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrDefault("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrDefault("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrEmpty()
					Assert.That(obj.GetArrayOrEmpty("foo"), Is.SameAs(foo));
					Assert.That(obj.GetArrayOrEmpty("bar"), Is.SameAs(bar));
					Assert.That(obj.GetArrayOrEmpty("baz"), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(obj.GetArrayOrEmpty("not_found"), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(() => obj.GetArrayOrEmpty("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrEmpty("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrEmpty("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrEmpty("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
			}
			{
				var arr = new JsonArray()
				{
					/*0*/ foo,
					/*1*/ bar,
					/*2*/ JsonNull.Null,
					/*3*/ JsonObject.Create("hello", "world"),
					/*4*/ "hello, there!",
					/*5*/ 123,
					/*6*/ true,
				};

				{ // GetArray()
					Assert.That(arr.GetArray(0), Is.SameAs(foo));
					Assert.That(arr.GetArray(1), Is.SameAs(bar));
					Assert.That(() => arr.GetArray(2), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(42), Throws.InstanceOf<IndexOutOfRangeException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(arr.GetArrayOrDefault(0), Is.SameAs(foo));
					Assert.That(arr.GetArrayOrDefault(1), Is.SameAs(bar));
					Assert.That(arr.GetArrayOrDefault(2), Is.Null);
					Assert.That(() => arr.GetArrayOrDefault(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(42), Throws.InstanceOf<IndexOutOfRangeException>());
				}
				{ // GetArrayOrEmpty()
					Assert.That(arr.GetArrayOrEmpty(0), Is.SameAs(foo));
					Assert.That(arr.GetArrayOrEmpty(1), Is.SameAs(bar));
					Assert.That(arr.GetArrayOrEmpty(2), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(() => arr.GetArrayOrEmpty(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(42), Throws.InstanceOf<IndexOutOfRangeException>());
				}
			}
			{
				JsonValue missing = JsonNull.Missing;

				{ // GetArray()
					Assert.That(() => missing.GetArray(0), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(1), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(2), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(42), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(missing.GetArrayOrDefault(0), Is.Null);
					Assert.That(missing.GetArrayOrDefault(1), Is.Null);
					Assert.That(missing.GetArrayOrDefault(2), Is.Null);
					Assert.That(missing.GetArrayOrDefault(3), Is.Null);
					Assert.That(missing.GetArrayOrDefault(4), Is.Null);
					Assert.That(missing.GetArrayOrDefault(5), Is.Null);
					Assert.That(missing.GetArrayOrDefault(6), Is.Null);
					Assert.That(missing.GetArrayOrDefault(42), Is.Null);
				}
				{ // GetArrayOrEmpty()
					Assert.That(missing.GetArrayOrEmpty(0), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(1), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(2), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(3), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(4), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(5), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(6), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(42), Is.SameAs(JsonArray.ReadOnly.Empty));
				}
			}
		}

		[Test]
		public void Test_JsonFromValue_NodaTime_Types()
		{
			// Instant
			{
				var instant = NodaTime.Instant.FromDateTimeOffset(new DateTimeOffset(new DateTime(2015, 7, 17), new TimeSpan(2, 0, 0)));
				// 17 Juillet 2015 0h00, GMT+2 => 16 Juillet 2015 22h00 UTC
				var json = JsonValue.FromValue<NodaTime.Instant>(instant);
				Assert.That(json.Type, Is.EqualTo(JsonType.String));
				Assert.That(((JsonString) json).Value, Is.EqualTo("2015-07-16T22:00:00Z"));
				Assert.That(json.ToInstant(), Is.EqualTo(instant));
				Assert.That(json.As<NodaTime.Instant>(), Is.EqualTo(instant));
			}

			// Duration
			{
				var duration = NodaTime.Duration.FromHours(1);
				var json = JsonValue.FromValue<NodaTime.Duration>(duration);
				Assert.That(json.Type, Is.EqualTo(JsonType.Number));
				Assert.That(((JsonNumber)json).ToDouble(), Is.EqualTo(3600.0));
				Assert.That(json.ToDuration(), Is.EqualTo(duration));
				Assert.That(json.Required<NodaTime.Duration>(), Is.EqualTo(duration));
				Assert.That(json.As<NodaTime.Duration>(), Is.EqualTo(duration));
			}
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Types_ToMicrosoftFormat()
		{
			var settings = CrystalJsonSettings.Json.WithMicrosoftDates();

			// trust, but verify...
			Assume.That(typeof(DateTime).IsPrimitive, Is.False);
			Assume.That(typeof(DateTime).IsValueType, Is.True);
			long unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
			Assume.That(unixEpoch, Is.EqualTo(621355968000000000));

			TimeSpan utcOffset = DateTimeOffset.Now.Offset;

			// corner cases
			CheckSerialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"\\/Date(0)\\/\"");
			CheckSerialize(new DateTime(0, DateTimeKind.Utc), settings, "\"\\/Date(-62135596800000)\\/\"");
			CheckSerialize(DateTime.MinValue, settings, "\"\\/Date(-62135596800000)\\/\"");
			CheckSerialize(new DateTime(3155378975999999999, DateTimeKind.Utc), settings, "\"\\/Date(253402300799999)\\/\"");
			CheckSerialize(DateTime.MaxValue, settings, "\"\\/Date(253402300799999)\\/\"");

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			long ticks = (utcNow.Ticks - unixEpoch) / 10000;
			CheckSerialize(utcNow, settings, "\"\\/Date(" + ticks.ToString() + ")\\/\"");

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			ticks = (localNow.Ticks - unixEpoch - utcOffset.Ticks) / 10000;
			CheckSerialize(localNow, settings, "\"\\/Date(" + ticks.ToString() + GetTimeZoneSuffix(localNow) + ")\\/\"");

			// Local vs Unspecified vs UTC
			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(
				new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				settings,
				"\"\\/Date(946684800000)\\/\"",
				"2000-01-01 UTC"
			);
			CheckSerialize(
				new DateTime(2000, 1, 1, 0, 0, 0),
				settings,
				"\"\\/Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + "+0100)\\/\"",
				"2000-01-01 GMT+1 (Paris)"
			);
			CheckSerialize(
				new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local),
				settings,
				"\"\\/Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + "+0100)\\/\"",
				"2000-01-01 GMT+1 (Paris)"
			);
			// * 1er Août 2000 = GMT + 2, car heure d'été
			CheckSerialize(
				new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc),
				settings,
				"\"\\/Date(967766400000)\\/\"",
				"2000-09-01 UTC"
			);
			CheckSerialize(
				new DateTime(2000, 9, 1, 0, 0, 0),
				settings,
				"\"\\/Date(" + (967766400000 - 2 * 3600 * 1000).ToString() + "+0200)\\/\"",
				"2000-08-01 GMT+2 (Paris, DST)"
			);
			CheckSerialize(
				new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local),
				settings,
				"\"\\/Date(967759200000" + "+0200)\\/\"",
				"2000-08-01 GMT+2 (Paris, DST)"
			);
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Iso8601()
		{
			var settings = CrystalJsonSettings.Json.WithIso8601Dates();
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// MinValue: must be serialized as an empty string
			// will handle the case where we have serialized DateTime.MinValue, but we are deserializing as Nullable<DateTime>
			CheckSerialize(DateTime.MinValue, settings, "\"\"");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), settings, "\"\"");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local), settings, "\"\"");

			// MaxValue: must NOT specify a timezone
			CheckSerialize(DateTime.MaxValue, settings, "\"9999-12-31T23:59:59.9999999\"");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), settings, "\"9999-12-31T23:59:59.9999999\"", "DateTime.MaxValue should not specify UTC 'Z'");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Local), settings, "\"9999-12-31T23:59:59.9999999\"", "DateTime.MaxValue should not specify local TimeZone");

			// Unix Epoch
			CheckSerialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"1970-01-01T00:00:00Z\"");

			// Unspecified
			CheckSerialize(
				new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified),
				settings,
				"\"2013-03-11T12:34:56.7680000\"",
				"Dates with Unspecified timezone must NOT end with 'Z', NOR include a timezone"
			);

			// UTC
			CheckSerialize(
				new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc),
				settings,
				"\"2013-03-11T12:34:56.7680000Z\"",
				"UTC dates must end with 'Z'"
			);

			// Local
			var dt = new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local);
			CheckSerialize(
				dt,
				settings,
				"\"2013-03-11T12:34:56.7680000" + ToUtcOffset(new DateTimeOffset(dt).Offset) + "\"",
				"Local dates must specify a timezone"
			);

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			CheckSerialize(utcNow, settings, "\"" + utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "Z\"", "DateTime.UtcNow must end with 'Z'");

			// Now (local)
			DateTime localNow = DateTime.Now;
			CheckSerialize(localNow, settings, "\"" + localNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + ToUtcOffset(DateTimeOffset.Now.Offset) + "\"", "DateTime.Now doit inclure la TimeZone");

			// Local vs Unspecified vs UTC
			// IMPORTANT: this test only works if you are in the "Romance Standard Time" (Paris, Bruxelles, ...), sorry! (or use the pretext to visit Paris, all expenses paid by the QA dept. !)
			// Paris: GMT+1 l'hivers, GMT+2 l'état

			// * 1er Janvier 2000 = GMT + 1, car heure d'hiver
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"2000-01-01T00:00:00Z\"", "2000-01-01 UTC");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0), settings, "\"2000-01-01\"", "2000-01-01 (unspecified)");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), settings, "\"2000-01-01T00:00:00+01:00\"", "2000-01-01 GMT+1 (Paris)");

			// * 1er Septembre 2000 = GMT + 2, car heure d'été
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"2000-09-01T00:00:00Z\"", "2000-09-01 UTC");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0), settings, "\"2000-09-01\"", "2000-09-01 (unspecified)");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), settings, "\"2000-09-01T00:00:00+02:00\"", "2000-09-01 GMT+2 (Paris, DST)");
		}

		[Test]
		public void Test_JsonSerialize_DateTimeOffset_Iso8601()
		{
			var settings = CrystalJsonSettings.Json.WithIso8601Dates();
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// MinValue: must be serialized as the empty string
			CheckSerialize(DateTimeOffset.MinValue, settings, "\"\"");

			// MaxValue: should NOT specify a timezone
			CheckSerialize(DateTimeOffset.MaxValue, settings, "\"9999-12-31T23:59:59.9999999\"");

			// Unix Epoch
			CheckSerialize(new DateTimeOffset(new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), settings, "\"1970-01-01T00:00:00+00:00\"");

			// Now (Utc, Local)
			CheckSerialize(new DateTimeOffset(new(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)), settings, "\"2013-03-11T12:34:56.7680000+00:00\"");
			CheckSerialize(new DateTimeOffset(new(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), settings, "\"2013-03-11T12:34:56.7680000" + ToUtcOffset(TimeZoneInfo.Local.BaseUtcOffset) + "\"");

			// TimeZones
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.Zero), settings, "\"2013-03-11T12:34:56.7680000+00:00\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(1)), settings, "\"2013-03-11T12:34:56.7680000+01:00\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-1)), settings, "\"2013-03-11T12:34:56.7680000-01:00\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromMinutes(11 * 60 + 30)), settings, "\"2013-03-11T12:34:56.7680000+11:30\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromMinutes(-11 * 60 - 30)), settings, "\"2013-03-11T12:34:56.7680000-11:30\"");

			// Now (UTC)
			var utcNow = DateTimeOffset.UtcNow;
			CheckSerialize(utcNow, settings, "\"" + utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "+00:00\"");

			// Now (local)
			var localNow = DateTimeOffset.Now;
			CheckSerialize(localNow, settings, "\"" + localNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + ToUtcOffset(localNow.Offset) + "\"", "DateTime.Now doit inclure la TimeZone");
			//note: this test will not work if the server is running int the UTC/GMT+0 timezone !

			// Local vs Unspecified vs UTC
			// Paris: GMT+1 l'hivers, GMT+2 l'état

			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), settings, "\"2000-01-01T00:00:00+00:00\"", "2000-01-01 UTC");
			CheckSerialize(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)), settings, "\"2000-01-01T00:00:00+01:00\"", "2000-01-01 GMT+1 (Paris)");

			// * 1er Septembre 2000 = GMT + 2, car heure d'été
			CheckSerialize(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.Zero), settings, "\"2000-09-01T00:00:00+00:00\"", "2000-09-01 UTC");
			CheckSerialize(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.FromHours(2)), settings, "\"2000-09-01T00:00:00+02:00\"", "2000-09-01 GMT+2 (Paris, DST)");
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Types_ToJavaScriptFormat()
		{
			// trust, but verify...
			Assume.That(typeof(DateTime).IsPrimitive, Is.False);
			Assume.That(typeof(DateTime).IsValueType, Is.True);
			long unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
			Assume.That(unixEpoch, Is.EqualTo(621355968000000000));

			// corner cases
			CheckSerialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(0)");
			CheckSerialize(new DateTime(0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(-62135596800000)");
			CheckSerialize(DateTime.MinValue, CrystalJsonSettings.JavaScript, "new Date(-62135596800000)");
			CheckSerialize(new DateTime(3155378975999999999, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(253402300799999)");
			CheckSerialize(DateTime.MaxValue, CrystalJsonSettings.JavaScript, "new Date(253402300799999)");

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			long ticks = (utcNow.Ticks - unixEpoch) / 10000;
			CheckSerialize(utcNow, CrystalJsonSettings.JavaScript, $"new Date({ticks})");

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			TimeSpan utcOffset = new DateTimeOffset(localNow).Offset;
			ticks = (localNow.Ticks - unixEpoch - utcOffset.Ticks) / 10000;
			CheckSerialize(localNow, CrystalJsonSettings.JavaScript, $"new Date({ticks})");

			// Local vs Unspecified vs UTC
			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(946684800000)", "2000-01-01 UTC");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0), CrystalJsonSettings.JavaScript, "new Date(946681200000)", "2000-01-01 GMT+1 (Paris)");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), CrystalJsonSettings.JavaScript, "new Date(946681200000)", "2000-01-01 GMT+1 (Paris)");
			// * 1er Août 2000 = GMT + 2 car heure d'été
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(967766400000)", "2000-09-01 UTC");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0), CrystalJsonSettings.JavaScript, "new Date(967759200000)", "2000-08-01 GMT+2 (Paris, DST)");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), CrystalJsonSettings.JavaScript, "new Date(967759200000)", "2000-08-01 GMT+2 (Paris, DST)");

			//TODO: DateTimeOffset ?
		}

		[Test]
		public void Test_JsonSerialize_TimeSpan()
		{
			// TimeSpan
			CheckSerialize(TimeSpan.Zero, default, "0");
			CheckSerialize(TimeSpan.FromSeconds(1), default, "1");
			CheckSerialize(TimeSpan.FromSeconds(1.5), default, "1.5");
			CheckSerialize(TimeSpan.FromMinutes(1), default, "60");
			CheckSerialize(TimeSpan.FromMilliseconds(1), default, "0.001");
			CheckSerialize(TimeSpan.FromTicks(1), default, "1E-07");
			CheckSerialize(TimeSpan.MinValue, default, TimeSpan.MinValue.TotalSeconds.ToString("R"));
			CheckSerialize(TimeSpan.MaxValue, default, TimeSpan.MaxValue.TotalSeconds.ToString("R"));
		}

		[Test]
		public void Test_JsonSerializes_EnumTypes()
		{
			// trust, but verify...
			Assume.That(typeof(DummyJsonEnum).IsPrimitive, Is.False);
			Assume.That(typeof(DummyJsonEnum).IsEnum, Is.True);

			// As Integers

			// enum
			CheckSerialize(MidpointRounding.AwayFromZero, default, "1");
			CheckSerialize(DayOfWeek.Friday, default, "5");
			// custom enum
			CheckSerialize(DummyJsonEnum.None, default, "0");
			CheckSerialize(DummyJsonEnum.Foo, default, "1");
			CheckSerialize(DummyJsonEnum.Bar, default, "42");
			CheckSerialize((DummyJsonEnum)123, default, "123");
			// custom [Flags] enum
			CheckSerialize(DummyJsonEnumFlags.None, default, "0");
			CheckSerialize(DummyJsonEnumFlags.Foo, default, "1");
			CheckSerialize(DummyJsonEnumFlags.Bar, default, "2");
			CheckSerialize(DummyJsonEnumFlags.Narf, default, "4");
			CheckSerialize(DummyJsonEnumFlags.Foo | DummyJsonEnumFlags.Bar, default, "3");
			CheckSerialize(DummyJsonEnumFlags.Bar | DummyJsonEnumFlags.Narf, default, "6");
			CheckSerialize((DummyJsonEnumFlags)255, default, "255");

			// As Strings

			var settings = CrystalJsonSettings.Json.WithEnumAsStrings();

			// enum
			CheckSerialize(MidpointRounding.AwayFromZero, settings, "\"AwayFromZero\"");
			CheckSerialize(DayOfWeek.Friday, settings, "\"Friday\"");
			// custom enum
			CheckSerialize(DummyJsonEnum.None, settings, "\"None\"");
			CheckSerialize(DummyJsonEnum.Foo, settings, "\"Foo\"");
			CheckSerialize(DummyJsonEnum.Bar, settings, "\"Bar\"");
			CheckSerialize((DummyJsonEnum)123, settings, "\"123\"");
			// custom [Flags] enum
			CheckSerialize(DummyJsonEnumFlags.None, settings, "\"None\"");
			CheckSerialize(DummyJsonEnumFlags.Foo, settings, "\"Foo\"");
			CheckSerialize(DummyJsonEnumFlags.Bar, settings, "\"Bar\"");
			CheckSerialize(DummyJsonEnumFlags.Narf, settings, "\"Narf\"");
			CheckSerialize(DummyJsonEnumFlags.Foo | DummyJsonEnumFlags.Bar, settings, "\"Foo, Bar\"");
			CheckSerialize(DummyJsonEnumFlags.Bar | DummyJsonEnumFlags.Narf, settings, "\"Bar, Narf\"");
			CheckSerialize((DummyJsonEnumFlags)255, settings, "\"255\"");

			// Duplicate Values

			settings = CrystalJsonSettings.Json.WithEnumAsStrings();
			CheckSerialize(DummyJsonEnumTypo.Bar, settings, "\"Bar\"");
			CheckSerialize(DummyJsonEnumTypo.Barrh, settings, "\"Bar\"");
			CheckSerialize((DummyJsonEnumTypo)2, settings, "\"Bar\"");
		}

		[Test]
		public void Test_JsonSerialize_Structs()
		{
			// trust, but verify...
			Assume.That(typeof(DummyJsonStruct).IsValueType, Is.True);
			Assume.That(typeof(DummyJsonStruct).IsClass, Is.False);

			// empty struct
			var x = new DummyJsonStruct();
			CheckSerialize(
				x,
				default,
				"""{ "Valid": false, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 }",
				"Serialize(EMPTY, JS)"
			);

			// with explicit nulls
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Valid": false, "Name": null, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "Modified": null, "DateOfBirth": null, "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON+ShowNull)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithNullMembers(),
				"{ Valid: false, Name: null, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), Modified: null, DateOfBirth: null, State: 0, RatioOfStuff: 0 }",
				"Serialize(EMPTY, JS+ShowNull)"
			);

			// hide default values
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY, JSON+HideDefaults)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY, JS+HideDefaults)"
			);

			// compact mode
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonCompact,
				"""{"Valid":false,"Index":0,"Size":0,"Height":0,"Amount":0,"Created":"","State":0,"RatioOfStuff":0}""",
				"Serialize(EMPTY, JSON+Compact)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.Compacted(),
				"{Valid:false,Index:0,Size:0,Height:0,Amount:0,Created:new Date(-62135596800000),State:0,RatioOfStuff:0}",
				"Serialize(EMPTY, JS+Compact)"
			);

			// indented
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonIndented,
				"""
				{
					"Valid": false,
					"Index": 0,
					"Size": 0,
					"Height": 0,
					"Amount": 0,
					"Created": "",
					"State": 0,
					"RatioOfStuff": 0
				}
				""",
				"Serialize(X, JSON+Indented)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScriptIndented,
				"""
				{
					Valid: false,
					Index: 0,
					Size: 0,
					Height: 0,
					Amount: 0,
					Created: new Date(-62135596800000),
					State: 0,
					RatioOfStuff: 0
				}
				""",
				"Serialize(X, JS+Indented)"
			);

			// filled with values
			x.Valid = true;
			x.Name = "James Bond";
			x.Index = 7;
			x.Size = 123456789;
			x.Height = 1.8f;
			x.Amount = 0.07d;
			x.Created = new DateTime(1968, 5, 8);
			x.State = DummyJsonEnum.Foo;

			CheckSerialize(
				x,
				default,
				"""{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08", "State": 1, "RatioOfStuff": 8641975.23 }""",
				"Serialize(BOND, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ Valid: true, Name: 'James Bond', Index: 7, Size: 123456789, Height: 1.8, Amount: 0.07, Created: new Date(-52106400000), State: 1, RatioOfStuff: 8641975.23 }",
				"Serialize(BOND, JS)"
			);

			// compact mode
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.Compacted(),
				"""{"Valid":true,"Name":"James Bond","Index":7,"Size":123456789,"Height":1.8,"Amount":0.07,"Created":"1968-05-08","State":1,"RatioOfStuff":8641975.23}""",
				"Serialize(BOND, JSON+Compact)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.Compacted(),
				"{Valid:true,Name:'James Bond',Index:7,Size:123456789,Height:1.8,Amount:0.07,Created:new Date(-52106400000),State:1,RatioOfStuff:8641975.23}",
				"Serialize(BOND, JS+Compact)"
			);
		}

		[Test]
		public void Test_JsonSerialize_NullableTypes()
		{
			var x = new DummyNullableStruct();
			// since all members are null, the object should be empty
			CheckSerialize(
				x,
				default,
				"{ }",
				"Serialize(EMPTY,JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ }",
				"Serialize(EMPTY,JS)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY,JSON+HideDefaults)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY,JS+HideDefaults)"
			);

			// by default, all should be null
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Bool": null, "Int32": null, "Int64": null, "Single": null, "Double": null, "DateTime": null, "TimeSpan": null, "Guid": null, "Enum": null, "Struct": null }""",
				"Serialize(EMPTY,JSON+ShowNull)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithNullMembers(),
				"{ Bool: null, Int32: null, Int64: null, Single: null, Double: null, DateTime: null, TimeSpan: null, Guid: null, Enum: null, Struct: null }",
				"Serialize(EMPTY,JS+ShowNull)"
			);

			// fill the object with non-null values
			x = new DummyNullableStruct()
			{
				Bool = true,
				Int32 = 123,
				Int64 = 123,
				Single = 1.23f,
				Double = 1.23d,
				Guid = new Guid("98bd4ed7-7337-4018-9551-ee0825ada7ba"),
				DateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				TimeSpan = TimeSpan.FromMinutes(1),
				Enum = DummyJsonEnum.Bar,
				Struct = new DummyJsonStruct(), // empty
			};
			CheckSerialize(
				x,
				default,
				"""{ "Bool": true, "Int32": 123, "Int64": 123, "Single": 1.23, "Double": 1.23, "DateTime": "2000-01-01T00:00:00Z", "TimeSpan": 60, "Guid": "98bd4ed7-7337-4018-9551-ee0825ada7ba", "Enum": 42, "Struct": { "Valid": false, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "State": 0, "RatioOfStuff": 0 } }""",
				"Serialize(FILLED, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ Bool: true, Int32: 123, Int64: 123, Single: 1.23, Double: 1.23, DateTime: new Date(946684800000), TimeSpan: 60, Guid: '98bd4ed7-7337-4018-9551-ee0825ada7ba', Enum: 42, Struct: { Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 } }",
				"Serialize(FILLED, JS)"
			);
		}

		[Test]
		public void Test_JsonSerialize_Class()
		{
			// trust, but verify...
			Assume.That(typeof(DummyJsonClass).IsValueType, Is.False);
			Assume.That(typeof(DummyJsonClass).IsClass, Is.True);

			var x = new DummyJsonClass();
			CheckSerialize(
				x,
				default,
				"""{ "Valid": false, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"""{ Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 }""",
				"Serialize(EMPTY, JS)"
			);

			// hide default values
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithoutDefaultValues(),
				"{ }",
				"SerializeObject(EMPTY, JSON+HideDefaults)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithoutDefaultValues(),
				"{ }",
				"SerializeObject(EMPTY, JS+HideDefaults)"
			);

			// with explicit nulls
			CheckSerialize(
				x, CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Valid": false, "Name": null, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "Modified": null, "DateOfBirth": null, "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON+ShowNullMembers)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithNullMembers(),
				"""{ Valid: false, Name: null, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), Modified: null, DateOfBirth: null, State: 0, RatioOfStuff: 0 }""",
				"Serialize(EMPTY, JS+ShowNullMembers)"
			);

			// filled with values
			x.Name = "James Bond";
			x.Index = 7;
			x.Size = 123456789;
			x.Height = 1.8f;
			x.Amount = 0.07d;
			x.Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc);
			x.Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc);
			x.State = DummyJsonEnum.Bar;

			// formatted
			CheckSerialize(
				x,
				default,
				"""{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(class, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"""{ Valid: true, Name: 'James Bond', Index: 7, Size: 123456789, Height: 1.8, Amount: 0.07, Created: new Date(-52099200000), Modified: new Date(1288280340000), State: 42, RatioOfStuff: 8641975.23 }""",
				"Serialize(class, JS)"
			);

			// compact
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonCompact,
				"""{"Valid":true,"Name":"James Bond","Index":7,"Size":123456789,"Height":1.8,"Amount":0.07,"Created":"1968-05-08T00:00:00Z","Modified":"2010-10-28T15:39:00Z","State":42,"RatioOfStuff":8641975.23}""",
				"Serialize(class, JSON+Compact)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.Compacted(),
				"""{Valid:true,Name:'James Bond',Index:7,Size:123456789,Height:1.8,Amount:0.07,Created:new Date(-52099200000),Modified:new Date(1288280340000),State:42,RatioOfStuff:8641975.23}""",
				"Serialize(class, JS+Compact)"
			);

			// indented
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonIndented,
				"""
				{
					"Valid": true,
					"Name": "James Bond",
					"Index": 7,
					"Size": 123456789,
					"Height": 1.8,
					"Amount": 0.07,
					"Created": "1968-05-08T00:00:00Z",
					"Modified": "2010-10-28T15:39:00Z",
					"State": 42,
					"RatioOfStuff": 8641975.23
				}
				""",
				"Serialize(class, JSON+Indented)"
			);

			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScriptIndented,
				"""
				{
					Valid: true,
					Name: 'James Bond',
					Index: 7,
					Size: 123456789,
					Height: 1.8,
					Amount: 0.07,
					Created: new Date(-52099200000),
					Modified: new Date(1288280340000),
					State: 42,
					RatioOfStuff: 8641975.23
				}
				""",
				"Serialize(class, JS+Indented)"
			);

			// Camel Casing
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.CamelCased(),
				"""{ "valid": true, "name": "James Bond", "index": 7, "size": 123456789, "height": 1.8, "amount": 0.07, "created": "1968-05-08T00:00:00Z", "modified": "2010-10-28T15:39:00Z", "state": 42, "ratioOfStuff": 8641975.23 }""",
				"Serialize(class, JSON+CamelCasing)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.CamelCased(),
				"{ valid: true, name: 'James Bond', index: 7, size: 123456789, height: 1.8, amount: 0.07, created: new Date(-52099200000), modified: new Date(1288280340000), state: 42, ratioOfStuff: 8641975.23 }",
				"Serialize(class, JS+CamelCasing)"
			);
		}

		[Test]
		public void Test_JsonSerialize_InterfaceMember()
		{
			// Test: a class that contains a member with an interface type
			// => we should not serialize only the members defined on that interface, but instead serialize the runtime type of the instance, which will not be known in advance

			CheckSerialize(
				new DummyOuterClass(),
				default,
				"""{ "Id": 0 }""",
				"Serialize(EMPTY, JSON)"
			);

			// filled with values
			var agent = new DummyJsonBaseClass()
			{
				Name = "James Bond",
				Index = 7,
				Size = 123456789,
				Height = 1.8f,
				Amount = 0.07d,
				Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
				Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
				State = DummyJsonEnum.Bar,
			};

			// Serialize the instance directly (known type)
			CheckSerialize(
				agent,
				default,
				"""{ "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(INNER, JSON)"
			);

			// Serialize the container type that references this instance via the interface
			var x = new DummyOuterClass()
			{
				Id = 7,
				Agent = agent,
			};

			CheckSerialize(
				x,
				default,
				"""{ "Id": 7, "Agent": { "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 } }""",
				"Serialize(OUTER, JSON)"
			);

			// deserialize the container instance
			var y = CrystalJson.Deserialize<DummyOuterClass>(CrystalJson.Serialize(x));
			Assert.That(y, Is.Not.Null);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(y.Id, Is.EqualTo(7));
				Assert.That(y.Agent, Is.Not.Null);
				Assert.That(y.Agent, Is.InstanceOf<DummyJsonBaseClass>(), "Should have used the _class property to find the original type!");
				Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));
				Assert.That(y.Agent.Index, Is.EqualTo(7));
				Assert.That(y.Agent.Size, Is.EqualTo(123456789));
				Assert.That(y.Agent.Height, Is.EqualTo(1.8f));
				Assert.That(y.Agent.Amount, Is.EqualTo(0.07d));
				Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar));
			}
		}

		[Test]
		public void Test_JsonSerialize_UnsealedClassMember()
		{
			// We have a container type that points to a non-sealed class, but with an instance of the expected type (i.e.: not of a derived type)
			// => there should not be any $type property present, since there are no polymorphic annotations on this type
			var x = new DummyOuterDerivedClass()
			{
				Id = 7,
				Agent = new DummyJsonBaseClass()
				{
					Name = "James Bond",
					Index = 7,
					Size = 123456789,
					Height = 1.8f,
					Amount = 0.07d,
					Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
					Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
					State = DummyJsonEnum.Bar,
				}
			};

			CheckSerialize(
				x.Agent,
				default,
				"""{ "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(INNER, JSON)"
			);

			CheckSerialize(
				x,
				default,
				"""{ "Id": 7, "Agent": { "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 } }""",
				"Serialize(OUTER, JSON)"
			);

			// indented
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.Indented(),
				"""
				{
					"Id": 7,
					"Agent": {
						"$type": "agent",
						"Valid": true,
						"Name": "James Bond",
						"Index": 7,
						"Size": 123456789,
						"Height": 1.8,
						"Amount": 0.07,
						"Created": "1968-05-08T00:00:00Z",
						"Modified": "2010-10-28T15:39:00Z",
						"State": 42,
						"RatioOfStuff": 8641975.23
					}
				}
				""",
				"Serialize(OUTER, JSON)"
			);

			// Deserialize
			var y = CrystalJson.Deserialize<DummyOuterDerivedClass>(CrystalJson.Serialize(x, CrystalJsonSettings.Json.Indented()));
			Assert.That(y, Is.Not.Null);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(y.Id, Is.EqualTo(7));
				Assert.That(y.Agent, Is.Not.Null);
				Assert.That(y.Agent, Is.InstanceOf<DummyJsonBaseClass>());
				Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));
				Assert.That(y.Agent.Index, Is.EqualTo(7));
				Assert.That(y.Agent.Size, Is.EqualTo(123456789));
				Assert.That(y.Agent.Height, Is.EqualTo(1.8f));
				Assert.That(y.Agent.Amount, Is.EqualTo(0.07d));
				Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar));
			}
		}

		[Test]
		public void Test_JsonSerialize_DerivedClassMember()
		{
			// We have a container type with a member of type "FooBase", but at runtime it contains a "FooDerived" instance (class that derives from "FooBase")
			// => $type should always be present, since there are polymorphic annotations on the types
			// => this "$type" property will also be used to instantiate the correct derived type

			CheckSerialize(
				new DummyOuterDerivedClass(),
				default,
				"""{ "Id": 0 }""",
				"Serialize(EMPTY, JSON)"
			);

			// filled with values
			var agent = new DummyDerivedJsonClass("Janov Bondovicz")
			{
				Name = "James Bond",
				Index = 7,
				Size = 123456789,
				Height = 1.8f,
				Amount = 0.07d,
				Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
				Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
				State = DummyJsonEnum.Bar,
			};
			var x = new DummyOuterDerivedClass
			{
				Id = 7,
				Agent = agent,
			};

			// serialize the derived type explicitly (known type)
			// => $type should always be present
			CheckSerialize(
				agent,
				default,
				"""{ "$type": "spy", "DoubleAgentName": "Janov Bondovicz", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(INNER, JSON)"
			);

			// serialize the container, which references this instance via the base type
			// => $type should always be present
			CheckSerialize(
				x,
				default,
				"""{ "Id": 7, "Agent": { "$type": "spy", "DoubleAgentName": "Janov Bondovicz", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 } }""",
				"Serialize(OUTER, JSON)"
			);

			// deserialize the container
			var y = CrystalJson.Deserialize<DummyOuterDerivedClass>(CrystalJson.Serialize(x));
			Assert.That(y, Is.Not.Null);
			Assert.That(y.Agent, Is.Not.Null);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(y.Id, Is.EqualTo(7));
				Assert.That(y.Agent, Is.InstanceOf<DummyDerivedJsonClass>(), "Should have instantianted the derived inner class, not the base class!");
				Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));
				Assert.That(y.Agent.Index, Is.EqualTo(7));
				Assert.That(y.Agent.Size, Is.EqualTo(123456789));
				Assert.That(y.Agent.Height, Is.EqualTo(1.8f));
				Assert.That(y.Agent.Amount, Is.EqualTo(0.07d));
				Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar));
			}

			var z = (DummyDerivedJsonClass) y.Agent;
			Assert.That(z.DoubleAgentName, Is.EqualTo("Janov Bondovicz"), "Should have deserialized the members specific to the derived class");
		}

		[Test]
		public void Test_Json_Custom_Serializable_Interface()
		{
			// serialize
			var x = new DummyJsonCustomClass("foo");
			CheckSerialize(x, default, """{ "custom":"foo" }""");

			// deserialize
			var y = CrystalJson.Deserialize<DummyJsonCustomClass>("""{ "custom":"bar" }""");
			Assert.That(y, Is.Not.Null);
			Assert.That(y, Is.InstanceOf<DummyJsonCustomClass>());
			Assert.That(y.GetSecret(), Is.EqualTo("bar"));

			// pack
			var value = JsonValue.FromValue(x);
			Assert.That(value, Is.Not.Null);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));
			var obj = (JsonObject)value;
			Assert.That(obj.Get<string>("custom"), Is.EqualTo("foo"));
			Assert.That(obj, Has.Count.EqualTo(1));

			// unpack
			var z = value.Bind(typeof(DummyJsonCustomClass))!;
			Assert.That(z, Is.Not.Null);
			Assert.That(z, Is.InstanceOf<DummyJsonCustomClass>());
			Assert.That(((DummyJsonCustomClass) z).GetSecret(), Is.EqualTo("foo"));
		}

		[Test]
		public void Test_Json_Custom_Serializable_Static_Legacy()
		{
			// LEGACY: for back compatibility with old "duck typing" static JsonSerialize method
			// -> new code should use the IJsonDeserializable<T> interface that defines the static method

			// serialize
			var x = new DummyStaticLegacyJson("foo");
			CheckSerialize(x, default, """{ "custom":"foo" }""");

			// deserialize
			var y = CrystalJson.Deserialize<DummyStaticLegacyJson>("""{ "custom":"bar" }""");
			Assert.That(y, Is.Not.Null);
			Assert.That(y, Is.InstanceOf<DummyStaticLegacyJson>());
			Assert.That(y.GetSecret(), Is.EqualTo("bar"));
		}

		[Test]
		public void Test_Json_Custom_Serializable_Static()
		{
			// ensure we can deserialize a type using the static method "JsonDesrialize(...)"
			// - compatible with readonly types and/or types that don't have a parameterless ctor!

			// serialize
			var foo = new DummyStaticCustomJson("foo");
			CheckSerialize(foo, default, """{ "custom":"foo" }""");

			// deserialize
			var foo2 = CrystalJson.Deserialize<DummyStaticCustomJson>("""{ "custom":"bar" }""");
			Assert.That(foo2, Is.Not.Null);
			Assert.That(foo2, Is.InstanceOf<DummyStaticCustomJson>());
			Assert.That(foo2.GetSecret(), Is.EqualTo("bar"));

			// arrays

			var arr = new [] { new DummyStaticCustomJson("foo"), new DummyStaticCustomJson("bar"), };
			CheckSerialize(arr, default, """[ { "custom":"foo" }, { "custom":"bar" } ]""");

			var arr2 = CrystalJson.Deserialize<DummyStaticCustomJson[]>("""[ { "custom":"foo" }, { "custom":"bar" } ]""");
			Assert.That(arr2, Is.Not.Null);
			Assert.That(arr2, Has.Length.EqualTo(2));
			Assert.That(arr2[0].GetSecret(), Is.EqualTo("foo"));
			Assert.That(arr2[1].GetSecret(), Is.EqualTo("bar"));
		}

		[Test]
		public void Test_JsonSerialize_Arrays()
		{
			// int[]
			CheckSerialize(Array.Empty<int>(), default, "[ ]");
			CheckSerialize(new int[1], default, "[ 0 ]");
			CheckSerialize(new int[] { 1, 2, 3 }, default, "[ 1, 2, 3 ]");

			CheckSerialize(Array.Empty<int>(), CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new int[1], CrystalJsonSettings.JsonCompact, "[0]");
			CheckSerialize(new int[] { 1, 2, 3 }, CrystalJsonSettings.JsonCompact, "[1,2,3]");

			// string[]
			CheckSerialize(Array.Empty<string>(), default, "[ ]");
			CheckSerialize(new string[1], default, "[ null ]");
			CheckSerialize(new string[] { "foo" }, default, """[ "foo" ]""");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(new string[] { "foo" }, CrystalJsonSettings.JavaScript, "[ 'foo' ]");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			// compact
			CheckSerialize(Array.Empty<string>(), CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JsonCompact, """["foo","bar","baz"]""");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScriptCompact, "['foo','bar','baz']");

			// bool[]
			CheckSerialize(Array.Empty<bool>(), default, "[ ]");
			CheckSerialize(new bool[1], default, "[ false ]");
			CheckSerialize(new bool[] { true, false, true }, default, "[ true, false, true ]");
		}

		[Test]
		public void Test_JsonSerialize_Jagged_Arrays()
		{
			CheckSerialize((int[][]) [ [ ], [ ] ], default, "[ [ ], [ ] ]");
			CheckSerialize((int[][]) [ [ ], [ ] ], CrystalJsonSettings.JsonCompact, "[[],[]]");

			CheckSerialize((int[][]) [ [ 1, 2, 3 ], [ 4, 5, 6 ] ], default, "[ [ 1, 2, 3 ], [ 4, 5, 6 ] ]");
			CheckSerialize((int[][]) [ [ 1, 2, 3 ], [ 4, 5, 6 ] ], CrystalJsonSettings.JsonCompact, "[[1,2,3],[4,5,6]]");

			// INCEPTION !
			CheckSerialize((string[][][][]) [ [ [ [ "INCEPTION" ] ] ] ], default, """[ [ [ [ "INCEPTION" ] ] ] ]""");
		}

		[Test]
		public void Test_JsonSerialize_ImmutableArrays()
		{
			static ImmutableArray<T> Make<T>(params ReadOnlySpan<T> items) => [ ..items ];

			// int[]
			CheckSerialize(ImmutableArray<int>.Empty, default, "[ ]");
			CheckSerialize(new int[1].ToImmutableArray(), default, "[ 0 ]");
			CheckSerialize(Make(1, 2, 3), default, "[ 1, 2, 3 ]");

			CheckSerialize(ImmutableArray<int>.Empty, CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new int[1].ToImmutableArray(), CrystalJsonSettings.JsonCompact, "[0]");
			CheckSerialize(Make(1, 2, 3), CrystalJsonSettings.JsonCompact, "[1,2,3]");

			// string[]
			CheckSerialize(ImmutableArray<string>.Empty, default, "[ ]");
			CheckSerialize(new string[1].ToImmutableArray(), default, "[ null ]");
			CheckSerialize(Make("foo"), default, """[ "foo" ]""");
			CheckSerialize(Make("foo", "bar", "baz"), default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(Make("foo"), CrystalJsonSettings.JavaScript, "[ 'foo' ]");
			CheckSerialize(Make("foo", "bar", "baz"), CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			// compact
			CheckSerialize(ImmutableArray<string>.Empty, CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JsonCompact, """["foo","bar","baz"]""");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScriptCompact, "['foo','bar','baz']");

			// bool[]
			CheckSerialize(ImmutableArray<bool>.Empty, default, "[ ]");
			CheckSerialize(new bool[1].ToImmutableArray(), default, "[ false ]");
			CheckSerialize(Make(true, false, true), default, "[ true, false, true ]");
		}

		[Test]
		public void Test_JsonSerialize_Lists()
		{
			// Collections
			var listOfStrings = new List<string>();
			CheckSerialize(listOfStrings, default, "[ ]");
			listOfStrings.Add("foo");
			CheckSerialize(listOfStrings, default, """[ "foo" ]""");
			listOfStrings.Add("bar");
			listOfStrings.Add("baz");
			CheckSerialize(listOfStrings, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(listOfStrings, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			var listOfObjects = new List<object?>() { 123, "Narf", true, DummyJsonEnum.Bar };
			CheckSerialize(listOfObjects, default, """[ 123, "Narf", true, 42 ]""");
			CheckSerialize(listOfObjects, CrystalJsonSettings.JavaScript, "[ 123, 'Narf', true, 42 ]");


			// List<int>
			CheckSerialize(new List<int>(), default, "[ ]");
			CheckSerialize(new List<int> { 0 }, default, "[ 0 ]");
			CheckSerialize(new List<int> { 1, 2, 3 }, default, "[ 1, 2, 3 ]");
			CheckSerialize(new List<int>(), CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new List<int> { 0 }, CrystalJsonSettings.JsonCompact, "[0]");
			CheckSerialize(new List<int> { 1, 2, 3 }, CrystalJsonSettings.JsonCompact, "[1,2,3]");

			// List<string>
			CheckSerialize(new List<string>(), default, "[ ]");
			CheckSerialize(new List<string?> { null }, default, "[ null ]");
			CheckSerialize(new List<string> { "foo" }, default, """[ "foo" ]""");
			CheckSerialize(new List<string> { "foo", "bar", "baz" }, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(new List<string> { "foo" }, CrystalJsonSettings.JavaScript, "[ 'foo' ]");
			CheckSerialize(new List<string> { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			// List<bool>
			CheckSerialize(new List<bool>(), default, "[ ]");
			CheckSerialize(new List<bool> { false }, default, "[ false ]");
			CheckSerialize(new List<bool> { true, false, true }, default, "[ true, false, true ]");
		}

		[Test]
		public void Test_JsonSerialize_ImmutableLists()
		{
			static ImmutableList<T> Make<T>(params ReadOnlySpan<T> items) => [ ..items ];

			// Collections
			var listOfStrings = ImmutableList<string>.Empty;
			CheckSerialize(listOfStrings, default, "[ ]");
			listOfStrings = listOfStrings.Add("foo");
			CheckSerialize(listOfStrings, default, """[ "foo" ]""");
			listOfStrings = listOfStrings.Add("bar");
			listOfStrings = listOfStrings.Add("baz");
			CheckSerialize(listOfStrings, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(listOfStrings, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			var listOfObjects = ImmutableList.CreateRange<object?>([ 123, "Narf", true, DummyJsonEnum.Bar ]);
			CheckSerialize(listOfObjects, default, """[ 123, "Narf", true, 42 ]""");
			CheckSerialize(listOfObjects, CrystalJsonSettings.JavaScript, "[ 123, 'Narf', true, 42 ]");

			// ImmutableList<int>
			CheckSerialize(ImmutableList<int>.Empty, default, "[ ]");
			CheckSerialize(Make(0), default, "[ 0 ]");
			CheckSerialize(Make(1, 2, 3), default, "[ 1, 2, 3 ]");
			CheckSerialize(ImmutableList<int>.Empty, CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(Make(0), CrystalJsonSettings.JsonCompact, "[0]");
			CheckSerialize(Make(1, 2, 3), CrystalJsonSettings.JsonCompact, "[1,2,3]");

			// ImmutableList<string>
			CheckSerialize(ImmutableList<string>.Empty, default, "[ ]");
			CheckSerialize(Make(default(string)), default, "[ null ]");
			CheckSerialize(Make("foo"), default, """[ "foo" ]""");
			CheckSerialize(Make("foo", "bar", "baz"), default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(Make("foo"), CrystalJsonSettings.JavaScript, "[ 'foo' ]");
			CheckSerialize(Make("foo", "bar", "baz"), CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			// ImmutableList<bool>
			CheckSerialize(ImmutableList<bool>.Empty, default, "[ ]");
			CheckSerialize(Make(false), default, "[ false ]");
			CheckSerialize(Make(true, false, true), default, "[ true, false, true ]");
		}

		[Test]
		public void Test_JsonSerialize_Enumerable()
		{
			CheckSerialize(Enumerable.Empty<int>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<string>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<bool>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<System.Net.IPAddress>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<DateTimeKind>(), default, "[ ]");

			CheckSerialize(Enumerable.Range(1, 1), default, "[ 1 ]");
			CheckSerialize(Enumerable.Range(1, 3), default, "[ 1, 2, 3 ]");
			CheckSerialize(Enumerable.Range(0, 3).Select(i => ((ReadOnlySpan<string>) ["Foo", "Bar", "Baz"])[i]), default, """[ "Foo", "Bar", "Baz" ]""");
			CheckSerialize(Enumerable.Range(1, 3).Select(i => i % 2 == 0), default, "[ false, true, false ]");
			CheckSerialize(Enumerable.Range(1, 3).Select(i => (i, i * i, i * i * i)), default, """[ [ 1, 1, 1 ], [ 2, 4, 8 ], [ 3, 9, 27 ] ]""");

			static IEnumerable<int> RangeUncountable(int count)
			{
				for (int i = 0; i < count; i++)
				{
					yield return 1 + i;
				}
			}

			CheckSerialize(RangeUncountable(0), default, "[ ]");
			CheckSerialize(RangeUncountable(1), default, "[ 1 ]");
			CheckSerialize(RangeUncountable(3), default, "[ 1, 2, 3 ]");
		}

		[Test]
		public void Test_JsonSerialize_QueryableCollection()
		{
			// ReSharper disable PossibleMultipleEnumeration

			// list of objects
			var queryableOfAnonymous = new int[] { 1, 2, 3 }.Select((x) => new { Value = x, Square = x * x, Ascii = (char)(64 + x) });

			// queryable
			CheckSerialize(queryableOfAnonymous, default, """[ { "Value": 1, "Square": 1, "Ascii": "A" }, { "Value": 2, "Square": 4, "Ascii": "B" }, { "Value": 3, "Square": 9, "Ascii": "C" } ]""");

			// convert to list
			CheckSerialize(queryableOfAnonymous.ToList(), default, """[ { "Value": 1, "Square": 1, "Ascii": "A" }, { "Value": 2, "Square": 4, "Ascii": "B" }, { "Value": 3, "Square": 9, "Ascii": "C" } ]""");

			// ReSharper restore PossibleMultipleEnumeration
		}

		[Test]
		public void Test_JsonSerialize_STuples()
		{
			// STuple<...>
			CheckSerialize(new STuple(), default, "[ ]");
			CheckSerialize(STuple.Create(123), default, "[ 123 ]");
			CheckSerialize(STuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize(STuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");

			// (ITuple) STuple<...>
			CheckSerialize(STuple.Empty, default, "[ ]");
			CheckSerialize((IVarTuple) STuple.Create(123), default, "[ 123 ]");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");

			// custom tuple types
			CheckSerialize(new ListTuple<int>([ 1, 2, 3 ]), default, "[ 1, 2, 3 ]");
			CheckSerialize(new ListTuple<string>([ "foo", "bar", "baz" ]), default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(new ListTuple<object>([ "hello world", 123, false ]), default, """[ "hello world", 123, false ]""");
			CheckSerialize(new LinkedTuple<int>(STuple.Create(1, 2), 3), default, "[ 1, 2, 3 ]");
			CheckSerialize(new JoinedTuple<STuple<int, int>, STuple<int>>(STuple.Create(1, 2), STuple.Create(3)), default, "[ 1, 2, 3 ]");
		}

		[Test]
		public void Test_JsonSerialize_ValueTuples()
		{
			// STuple<...>
			Log("ValueTuple...");
			CheckSerialize(ValueTuple.Create(), default, "[ ]");
			CheckSerialize(ValueTuple.Create(123), default, "[ 123 ]");
			CheckSerialize(ValueTuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");

			// (ITuple) STuple<...>
			Log("ITuple...");
			CheckSerialize((ITuple) ValueTuple.Create(), default, "[ ]");
			CheckSerialize((ITuple) ValueTuple.Create(123), default, "[ 123 ]");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");
		}

		[Test]
		public void Test_JsonSerialize_NodaTime_Types()
		{
			#region Duration

			// seconds (integer)
			var duration = NodaTime.Duration.FromSeconds(3600);
			CheckSerialize(duration, default, "3600");

			// seconds + miliseconds
			duration = NodaTime.Duration.FromMilliseconds(3600272);
			CheckSerialize(duration, default, "3600.272");

			// epsilon
			duration = NodaTime.Duration.Epsilon;
			CheckSerialize(duration, default, "1E-09");

			#endregion

			#region Instant

			var instant = default(NodaTime.Instant);
			CheckSerialize(instant, default, "\"1970-01-01T00:00:00Z\"");

			instant = NodaTime.Instant.FromUtc(2013, 6, 7, 11, 06, 58);
			CheckSerialize(instant, default, "\"2013-06-07T11:06:58Z\"");

			instant = NodaTime.Instant.FromUtc(-52, 8, 27, 12, 12);
			CheckSerialize(instant, default, "\"-0052-08-27T12:12:00Z\"");

			#endregion

			#region LocalDateTime

			var time = default(NodaTime.LocalDateTime);
			CheckSerialize(time, default, "\"0001-01-01T00:00:00\"");

			time = new NodaTime.LocalDateTime(1988, 04, 19, 00, 35, 56);
			CheckSerialize(time, default, "\"1988-04-19T00:35:56\"");

			time = new NodaTime.LocalDateTime(0, 1, 1, 0, 0);
			CheckSerialize(time, default, "\"0000-01-01T00:00:00\"");

			time = new NodaTime.LocalDateTime(-250, 02, 27, 18, 42);
			CheckSerialize(time, default, "\"-0250-02-27T18:42:00\"");

			#endregion

			#region ZonedDateTime

			CheckSerialize(
				default(ZonedDateTime),
				default,
				"\"0001-01-01T00:00:00Z UTC\""
			);

			CheckSerialize(
				new ZonedDateTime(Instant.FromUtc(1988, 04, 19, 00, 35, 56), DateTimeZoneProviders.Tzdb["Europe/Paris"]),
				default,
				"\"1988-04-19T02:35:56+02 Europe/Paris\"" 
				// note: GMT+2
			);

			CheckSerialize(
				new ZonedDateTime(Instant.FromUtc(0, 1, 1, 0, 0), DateTimeZone.Utc),
				default,
				"\"0000-01-01T00:00:00Z UTC\""
			);

			CheckSerialize(
				new ZonedDateTime(Instant.FromUtc(-250, 02, 27, 18, 42), DateTimeZoneProviders.Tzdb["Africa/Cairo"]),
				default,
				"\"-0250-02-27T20:47:09+02:05:09 Africa/Cairo\"" 
				// note: gregorian calendars
			);

			// Intentionaly give it an ambiguous local time, in both ways.
			var zone = NodaTime.DateTimeZoneProviders.Tzdb["Europe/London"];
			CheckSerialize(
				new ZonedDateTime(new LocalDateTime(2012, 10, 28, 1, 30), zone, Offset.FromHours(1)),
				default,
				"\"2012-10-28T01:30:00+01 Europe/London\""
			);
			CheckSerialize(
				new ZonedDateTime(new LocalDateTime(2012, 10, 28, 1, 30), zone, Offset.FromHours(0)),
				default,
				"\"2012-10-28T01:30:00Z Europe/London\""
			);

			#endregion

			#region DateTimeZone

			CheckSerialize(DateTimeZone.Utc, default, "\"UTC\"");
			// with tzdb, the format is "Region/City"
			CheckSerialize(DateTimeZoneProviders.Tzdb["Europe/Paris"], default, "\"Europe/Paris\"");
			CheckSerialize(DateTimeZoneProviders.Tzdb["America/New_York"], default, "\"America/New_York\""); // spaces converted to '_'
			CheckSerialize(DateTimeZoneProviders.Tzdb["Asia/Tokyo"], default, "\"Asia/Tokyo\"");

			#endregion

			#region OffsetDateTime

			CheckSerialize(
				default(OffsetDateTime),
				default,
				"\"0001-01-01T00:00:00Z\"",
				""
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.Zero),
				default,
				"\"2012-01-02T03:04:05.0060007Z\"",
				"Offset of 0 means UTC"
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.FromHours(2)),
				default,
				"\"2012-01-02T03:04:05.0060007+02:00\"",
				"Only HH:MM for the timezone offset"
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.FromHoursAndMinutes(-1, -30)),
				default,
				"\"2012-01-02T03:04:05.0060007-01:30\"",
				"Allow negative offsets"
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.FromHoursAndMinutes(-1, -30) + Offset.FromMilliseconds(-1234)),
				default,
				"\"2012-01-02T03:04:05.0060007-01:30\"",
				"Seconds and milliseconds in timezone offset should be dropped"
			);

			#endregion

			#region Offset...

			CheckSerialize(Offset.Zero, default, "\"+00\"");
			CheckSerialize(Offset.FromHours(2), default, "\"+02\"");
			CheckSerialize(Offset.FromHoursAndMinutes(1, 30), default, "\"+01:30\"");
			CheckSerialize(Offset.FromHoursAndMinutes(-1, -30), default, "\"-01:30\"");

			#endregion
		}

		[Test]
		public void Test_JsonSerialize_DateTimeZone()
		{
			var rnd = new Random();
			int index = rnd.Next(NodaTime.DateTimeZoneProviders.Tzdb.Ids.Count);
			var id = NodaTime.DateTimeZoneProviders.Tzdb.Ids[index];
			var zone = NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);
			CheckSerialize(zone, default, "\"" + id + "\"");
		}

		[Test]
		public void Test_JsonSerialize_Bytes()
		{
			CheckSerialize(default(byte[]), default, "null");
			CheckSerialize(Array.Empty<byte>(), default, @"""""");

			// note: binaries are encoded as Base64 text
			byte[] arrayOfBytes = [ 65, 0, 42, 255, 32 ];
			CheckSerialize(arrayOfBytes, default, @"""QQAq/yA=""");

			// random
			arrayOfBytes = new byte[16];
			new Random().NextBytes(arrayOfBytes);
			CheckSerialize(arrayOfBytes, default, "\"" + Convert.ToBase64String(arrayOfBytes) + "\"", "Random Data!");
		}

		[Test]
		public void Test_JsonSerialize_Slices()
		{
			CheckSerialize(Slice.Nil, default, "null");
			CheckSerialize(Slice.Empty, default, @"""""");

			var slice = new byte[] { 123, 123, 123, 65, 0, 42, 255, 32, 123 }.AsSlice(3, 5);
			CheckSerialize(slice, default, @"""QQAq/yA=""");

			// random
			var bytes = new byte[32];
			new Random().NextBytes(bytes);
			slice = bytes.AsSlice(8, 16);
			CheckSerialize(slice, default, "\"" + Convert.ToBase64String(bytes, 8, 16) + "\"", "Random Data!");
		}

		[Test]
		public void Test_JsonDeserialize_Bytes()
		{
			Assert.That(CrystalJson.Deserialize<byte[]?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<byte[]>("\"\""), Is.EqualTo(Array.Empty<byte>()));

			// note: binaries are encoded as Base64 text
			Assert.That(CrystalJson.Deserialize<byte[]>("\"QQAq/yA=\""), Is.EqualTo(new byte[] { 65, 0, 42, 255, 32 }));

			// random
			var bytes = new byte[16];
			new Random().NextBytes(bytes);
			Assert.That(CrystalJson.Deserialize<byte[]>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(bytes), "Random Data!");
		}

		[Test]
		public void Test_JsonDeserialize_Slices()
		{
			Assert.That(CrystalJson.Deserialize<Slice>("null", default), Is.EqualTo(Slice.Nil));
			Assert.That(CrystalJson.Deserialize<Slice>(@""""""), Is.EqualTo(Slice.Empty));

			Assert.That(CrystalJson.Deserialize<Slice>(@"""QQAq/yA="""), Is.EqualTo(new byte[] { 65, 0, 42, 255, 32 }.AsSlice()));

			// random
			var bytes = new byte[32];
			new Random().NextBytes(bytes);
			Assert.That(CrystalJson.Deserialize<Slice>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(bytes.AsSlice()), "Random Data!");
		}

		[Test]
		public void Test_JsonSerialize_Dictionary()
		{
			// The keys are converted to string
			// - JSON target: keys must always be escaped with double quotes (")
			// - JavaScript target: keys are identifiers and usually do no require escaping, unless required, and in this case will use single quotes (')

			CheckSerialize(new Dictionary<string, string>(), default, "{ }");
			CheckSerialize(new Dictionary<string, string> { ["foo"] = "bar" }, default, """{ "foo": "bar" }""");

			CheckSerialize(new Dictionary<string, string>(), CrystalJsonSettings.JavaScript, "{ }");
			CheckSerialize(new Dictionary<string, string> { ["foo"] = "bar" }, CrystalJsonSettings.JavaScript, "{ foo: 'bar' }");

			var dicOfStrings = new Dictionary<string, string>
			{
				["foo"] = "bar",
				["narf"] = "zort",
				["123"] = "456",
				["all your bases"] = "are belong to us"
			};
			// JSON
			CheckSerialize(
				dicOfStrings,
				default,
				"""{ "foo": "bar", "narf": "zort", "123": "456", "all your bases": "are belong to us" }"""
			);
			CheckSerialize(
				dicOfStrings,
				CrystalJsonSettings.Json.Compacted(),
				"""{"foo":"bar","narf":"zort","123":"456","all your bases":"are belong to us"}"""
			);
			CheckSerialize(
				dicOfStrings,
				CrystalJsonSettings.Json.Indented(),
				"""
				{
					"foo": "bar",
					"narf": "zort",
					"123": "456",
					"all your bases": "are belong to us"
				}
				"""
			);
			// JS
			CheckSerialize(
				dicOfStrings,
				CrystalJsonSettings.JavaScript,
				"{ foo: 'bar', narf: 'zort', '123': '456', 'all your bases': 'are belong to us' }",
				"JavaScript"
			);

			var dicOfInts = new Dictionary<string, int>
			{
				["foo"] = 123,
				["bar"] = 456
			};
			CheckSerialize(dicOfInts, default, """{ "foo": 123, "bar": 456 }""");
			CheckSerialize(dicOfInts, CrystalJsonSettings.JavaScript, "{ foo: 123, bar: 456 }");

			var dicOfObjects = new Dictionary<string, Tuple<int, string>>
			{
				["foo"] = new(123, "bar"),
				["narf"] = new(456, "zort")
			};
			CheckSerialize(
				dicOfObjects,
				default,
				"""{ "foo": { "Item1": 123, "Item2": "bar" }, "narf": { "Item1": 456, "Item2": "zort" } }"""
			);
			CheckSerialize(
				dicOfObjects,
				CrystalJsonSettings.JavaScript,
				"{ foo: { Item1: 123, Item2: 'bar' }, narf: { Item1: 456, Item2: 'zort' } }"
			);

			var dicOfTuples = new Dictionary<string, (int, string)>
			{
				["foo"] = new(123, "bar"),
				["narf"] = new(456, "zort")
			};
			CheckSerialize(
				dicOfTuples,
				default,
				"""{ "foo": [ 123, "bar" ], "narf": [ 456, "zort" ] }"""
			);
			CheckSerialize(
				dicOfTuples,
				CrystalJsonSettings.JavaScript,
				"{ foo: [ 123, 'bar' ], narf: [ 456, 'zort' ] }"
			);

		}

		[Test]
		public void Test_JsonDeserialize_Dictionary()
		{
			// key => string
			var obj = JsonObject.Parse("""{ "hello": "World", "foo": 123, "bar": true }""");
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());

			var dic = obj.Required<Dictionary<string, string>>();
			Assert.That(dic, Is.Not.Null);

			Assert.That(dic.ContainsKey("hello"), Is.True, "dic[hello]");
			Assert.That(dic.ContainsKey("foo"), Is.True, "dic[foo]");
			Assert.That(dic.ContainsKey("bar"), Is.True, "dic[bar]");

			Assert.That(dic["hello"], Is.EqualTo("World"));
			Assert.That(dic["foo"], Is.EqualTo("123"));
			Assert.That(dic["bar"], Is.EqualTo("true"));

			Assert.That(dic, Has.Count.EqualTo(3));

			// key => int
			obj = JsonObject.Parse("""{ "1": "Hello World", "42": "Narf!", "007": "James Bond" }""");
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());

			var dicInt = obj.Required<Dictionary<int, string>>();
			Assert.That(dicInt, Is.Not.Null);

			Assert.That(dicInt.ContainsKey(1), Is.True, "dicInt[1]");
			Assert.That(dicInt.ContainsKey(7), Is.True, "dicInt[7]");
			Assert.That(dicInt.ContainsKey(42), Is.True, "dicInt[42]");

			Assert.That(dicInt[1], Is.EqualTo("Hello World"));
			Assert.That(dicInt[7], Is.EqualTo("James Bond"));
			Assert.That(dicInt[42], Is.EqualTo("Narf!"));

			Assert.That(dicInt, Has.Count.EqualTo(3));
		}

		[Test]
		public void Test_JsonSerialize_Composite()
		{
			var composite = new
			{
				Id = 1,
				Title = "The Big Bang Theory",
				Cancelled = false,
				Cast = new[] {
					new { Character="Sheldon Cooper", Actor="Jim Parsons", Female=false },
					new { Character="Leonard Hofstadter", Actor="Johny Galecki", Female=false },
					new { Character="Penny", Actor="Kaley Cuoco", Female=true },
					new { Character="Howard Wolowitz", Actor="Simon Helberg", Female=false },
					new { Character="Raj Koothrappali", Actor="Kunal Nayyar", Female=false },
				},
				Seasons = 4,
				ScoreIMDB = 8.4, // (26/10/2010)
				Producer = "Chuck Lorre Productions",
				PilotAirDate = new DateTime(2007, 9, 24, 0, 0, 0, DateTimeKind.Utc), // easier with UTC dates
			};

			// JSON
			CheckSerialize(
				composite,
				default,
				"""{ "Id": 1, "Title": "The Big Bang Theory", "Cancelled": false, "Cast": [ { "Character": "Sheldon Cooper", "Actor": "Jim Parsons", "Female": false }, { "Character": "Leonard Hofstadter", "Actor": "Johny Galecki", "Female": false }, { "Character": "Penny", "Actor": "Kaley Cuoco", "Female": true }, { "Character": "Howard Wolowitz", "Actor": "Simon Helberg", "Female": false }, { "Character": "Raj Koothrappali", "Actor": "Kunal Nayyar", "Female": false } ], "Seasons": 4, "ScoreIMDB": 8.4, "Producer": "Chuck Lorre Productions", "PilotAirDate": "2007-09-24T00:00:00Z" }"""
			);

			// JS
			CheckSerialize(
				composite,
				CrystalJsonSettings.JavaScript,
				"{ Id: 1, Title: 'The Big Bang Theory', Cancelled: false, Cast: [ { Character: 'Sheldon Cooper', Actor: 'Jim Parsons', Female: false }, { Character: 'Leonard Hofstadter', Actor: 'Johny Galecki', Female: false }, { Character: 'Penny', Actor: 'Kaley Cuoco', Female: true }, { Character: 'Howard Wolowitz', Actor: 'Simon Helberg', Female: false }, { Character: 'Raj Koothrappali', Actor: 'Kunal Nayyar', Female: false } ], Seasons: 4, ScoreIMDB: 8.4, Producer: 'Chuck Lorre Productions', PilotAirDate: new Date(1190592000000) }"
			);
		}

		[Test]
		public void Test_JsonSerialize_DataContract()
		{
			CheckSerialize(new DummyDataContractClass(), default, """{ "Id": 0, "Age": 0, "IsFemale": false, "VisibleProperty": "CanBeSeen" }""");
			// with explicit nulls
			CheckSerialize(new DummyDataContractClass(), CrystalJsonSettings.Json.WithNullMembers(), """{ "Id": 0, "Name": null, "Age": 0, "IsFemale": false, "CurrentLoveInterest": null, "VisibleProperty": "CanBeSeen" }""");

			CheckSerialize(
				new DummyDataContractClass
				{
					AgentId = 7,
					Name = "James Bond",
					Age = 69,
					Female = false,
					CurrentLoveInterest = "Miss Moneypenny",
					InvisibleField = "007",
				},
				default,
				"""{ "Id": 7, "Name": "James Bond", "Age": 69, "IsFemale": false, "CurrentLoveInterest": "Miss Moneypenny", "VisibleProperty": "CanBeSeen" }"""
			);
		}

		[Test]
		public void Test_JsonSerialize_XmlIgnore()
		{

			CheckSerialize(
				new DummyXmlSerializableContractClass(),
				default,
				"""{ "Id": 0, "Age": 0, "IsFemale": false, "VisibleProperty": "CanBeSeen" }"""
			);
			CheckSerialize(
				new DummyXmlSerializableContractClass(),
				CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Id": 0, "Name": null, "Age": 0, "IsFemale": false, "CurrentLoveInterest": null, "VisibleProperty": "CanBeSeen" }"""
			);

			CheckSerialize(
				new DummyXmlSerializableContractClass()
				{
					AgentId = 7,
					Name = "James Bond",
					Age = 69,
					CurrentLoveInterest = "Miss Moneypenny",
					InvisibleField = "007",
				},
				default,
				"""{ "Id": 7, "Name": "James Bond", "Age": 69, "IsFemale": false, "CurrentLoveInterest": "Miss Moneypenny", "VisibleProperty": "CanBeSeen" }"""
			);
		}

		[Test]
		public void Test_JsonSerialize_Large_List_To_Disk()
		{
			const int N = 100 * 1000;

			var rnd = new Random(1234567890);

			var list = new List<string>();
			for (int i = 0; i < N; i++)
			{
				var rounds = rnd.Next(8) + 1;
				var str = string.Empty;
				for (int k = 0; k < rounds; k++)
				{
					str += new string((char)(rnd.Next(64) + 33), 4);
				}
				list.Add(str);
			}

			{ // WARMUP
				var x = CrystalJson.ToSlice(list);
				_ = x.ZstdCompress(0);
				_ = x.DeflateCompress(CompressionLevel.Optimal);
				_ = x.GzipCompress(CompressionLevel.Optimal);
			}

			// Clear Text

			string path =  GetTemporaryPath("foo.json");
			File.Delete(path);

			Log($"Writing to {path}");
			var sw = Stopwatch.StartNew();
			CrystalJson.SaveTo(path, list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
			sw.Stop();
			Assert.That(File.Exists(path), Is.True, "Should have created a file at " + path);
			long rawSize = new FileInfo(path).Length;
			Log($"RAW    : Saved {rawSize,9:N0} bytes in {sw.Elapsed.TotalMilliseconds:N1} ms");

			// read the file back
			string text = File.ReadAllText(path);
			Assert.That(text, Is.Not.Null.Or.Empty, "File should contain stuff");
			// deserialize
			var reloaded = CrystalJson.Deserialize<string[]>(text);
			Assert.That(reloaded, Is.Not.Null);
			Assert.That(reloaded, Has.Length.EqualTo(list.Count));
			for (int i = 0; i < list.Count; i++)
			{
				Assert.That(reloaded[i], Is.EqualTo(list[i]), $"Mismatch at index {i}");
			}

			{ // Compress, Deflate
				path = GetTemporaryPath("foo.json.deflate");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				using (var fs = File.Create(path))
				{
					fs.Write(data.DeflateCompress(CompressionLevel.Optimal).Span);
				}
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"Deflate: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

			{ // Compress, GZip
				path = GetTemporaryPath("foo.json.gz");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				using (var fs = File.Create(path))
				{
					fs.Write(data.GzipCompress(CompressionLevel.Optimal).Span);
				}
				sw.Stop();

				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"GZip -5: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

			{ // Compress, ZSTD -1
				path = GetTemporaryPath("foo.json.1.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(1).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -1: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -3
				path = GetTemporaryPath("foo.json.3.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(3).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -3: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -5
				path = GetTemporaryPath("foo.json.5.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(5).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -5: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -9
				path = GetTemporaryPath("foo.json.9.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(9).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -9: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -20
				path = GetTemporaryPath("foo.json.20.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(20).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -20: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
		}

		[Test]
		public void Test_JsonSerialize_Version()
		{
			CheckSerialize(new Version(1, 0), default, "\"1.0\"");
			CheckSerialize(new Version(1, 2, 3), default, "\"1.2.3\"");
			CheckSerialize(new Version(1, 2, 3, 4), default, "\"1.2.3.4\"");
			CheckSerialize(new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), default, "\"2147483647.2147483647.2147483647.2147483647\"");
		}

		[Test]
		public void Test_JsonSerialize_KeyValuePair()
		{
			// KeyValuePair<K, V> instances, outside a dictionary, will be serialized as the array '[KEY, VALUE]', instead of '{ "Key": KEY, "Value": VALUE }', because it is more compact.
			// The only exception is for a collection of KV pairs (all with the same type), which are serialized as an object

			CheckSerialize(new KeyValuePair<string, int>("hello", 42), CrystalJsonSettings.Json, """[ "hello", 42 ]""");
			CheckSerialize(new KeyValuePair<int, bool>(123, true), CrystalJsonSettings.Json, "[ 123, true ]");

			CheckSerialize(default(KeyValuePair<string, int>), CrystalJsonSettings.Json, "[ null, 0 ]");
			CheckSerialize(default(KeyValuePair<int, bool>), CrystalJsonSettings.Json, "[ 0, false ]");

			CheckSerialize(default(KeyValuePair<string, int>), CrystalJsonSettings.Json.WithoutDefaultValues(), "[ null, 0 ]");
			CheckSerialize(default(KeyValuePair<int, bool>), CrystalJsonSettings.Json.WithoutDefaultValues(), "[ 0, false ]");

			var nested = KeyValuePair.Create(KeyValuePair.Create("hello", KeyValuePair.Create("narf", 42)), KeyValuePair.Create(123, KeyValuePair.Create("zort", TimeSpan.Zero)));
			CheckSerialize(nested, CrystalJsonSettings.Json, """[ [ "hello", [ "narf", 42 ] ], [ 123, [ "zort", 0 ] ] ]""");
		}

		[Test]
		public void Test_JsonValue_FromValue_KeyValuePair()
		{
			// KeyValuePair<K, V> instances, outside a dictionary, will be serialized as the array '[KEY, VALUE]', instead of '{ "Key": KEY, "Value": VALUE }', because it is more compact.
			// The only exception is for a collection of KV pairs (all with the same type), which are serialized as an object

			Assert.That(JsonValue.FromValue(new KeyValuePair<string, int>("hello", 42)).ToJsonText(), Is.EqualTo("""[ "hello", 42 ]"""));
			Assert.That(JsonValue.FromValue(new KeyValuePair<int, bool>(123, true)).ToJsonText(), Is.EqualTo("[ 123, true ]"));

			Assert.That(JsonValue.FromValue(default(KeyValuePair<string, int>)).ToJsonText(), Is.EqualTo("[ null, 0 ]"));
			Assert.That(JsonValue.FromValue(default(KeyValuePair<int, bool>)).ToJsonText(), Is.EqualTo("[ 0, false ]"));

			var nested = KeyValuePair.Create(KeyValuePair.Create("hello", KeyValuePair.Create("narf", 42)), KeyValuePair.Create(123, KeyValuePair.Create("zort", TimeSpan.Zero)));
			Assert.That(JsonValue.FromValue(nested).ToJsonText(), Is.EqualTo("""[ [ "hello", [ "narf", 42 ] ], [ 123, [ "zort", 0 ] ] ]"""));
		}

		[Test]
		public void Test_JsonDeserialize_KeyValuePair()
		{
			// array variant: [Key, Value]
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("""["hello",42]"""), Is.EqualTo(KeyValuePair.Create("hello", 42)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[123,true]"), Is.EqualTo(KeyValuePair.Create(123, true)));

			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[null,0]"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[]"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[0,false]"), Is.EqualTo(default(KeyValuePair<int, bool>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[]"), Is.EqualTo(default(KeyValuePair<int, bool>)));

			Assert.That(() => CrystalJson.Deserialize<KeyValuePair<string, int>>("""["hello",123,true]"""), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => CrystalJson.Deserialize<KeyValuePair<string, int>>("""["hello"]"""), Throws.InstanceOf<InvalidOperationException>());

			// object-variant: {Key:.., Value:..}
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("""{ "Key": "hello", "Value": 42 }"""), Is.EqualTo(KeyValuePair.Create("hello", 42)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("""{ "Key": 123, "Value": true }]"""), Is.EqualTo(KeyValuePair.Create(123, true)));

			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("""{ "Key": null, "Value": 0 }"""), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("{}"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("""{ "Key": 0, "Value": false }"""), Is.EqualTo(default(KeyValuePair<int, bool>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("{}"), Is.EqualTo(default(KeyValuePair<int, bool>)));
		}

		[Test]
		public void Test_JsonSerialize_Uri()
		{
			CheckSerialize(default(Uri), default, "null");
			CheckSerialize(
				new Uri("http://google.com"),
				default,
				@"""http://google.com"""
			);
			CheckSerialize(
				new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
				default,
				@"""https://www.youtube.com/watch?v=dQw4w9WgXcQ"""
			);
			CheckSerialize(
				new Uri("ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"),
				default,
				@"""ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"""
			);
		}

		[Test]
		public void Test_JsonDeserialize_Uri()
		{
			Assert.That(CrystalJson.Deserialize<Uri>(@"""http://google.com"""), Is.EqualTo(new Uri("http://google.com")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""http://www.doxense.com/"""), Is.EqualTo(new Uri("http://www.doxense.com/")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""https://www.youtube.com/watch?v=dQw4w9WgXcQ"""), Is.EqualTo(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"""), Is.EqualTo(new Uri("ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv")));
			Assert.That(() => CrystalJson.Deserialize<Uri>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(CrystalJson.Deserialize<Uri?>("null", null), Is.EqualTo(default(Uri)));
		}

		private static void Verify_TryFormat_Json<T>(T literal)
		{
			Verify_TryFormat_Json(JsonValue.FromValue(literal));
		}

		private static void Verify_TryFormat_Json(JsonValue literal)
		{
			foreach (var (format, settings) in new [] { ("C", CrystalJsonSettings.JsonCompact), ("N", CrystalJsonSettings.Json), ("P", CrystalJsonSettings.JsonIndented), ("J", CrystalJsonSettings.JavaScript) })
			{
				string expected = CrystalJson.ToJsonSlice(literal, settings).ToStringUtf8()!;
				Verify_TryFormat(literal, format, expected);
			}
		}

		private static void Verify_TryFormat<T>(T literal, string format, string expected)
		{
			Verify_TryFormat(JsonValue.FromValue(literal), format, expected);
		}

		private static void Verify_TryFormat(JsonValue literal, string format, string expected)
		{
			Log($"# (<{literal.GetType().Name}> {literal.ToJsonTextCompact()}).ToString(\"{format}\") => `{expected}`");
			// DEFAULT

			{
				// with more space than required
				Span<char> buf = new char[expected.Length + 100];
				buf.Fill('無');
				Assert.That(literal.TryFormat(buf, out int charsWritten, format, null), Is.True, $"{literal}.TryFormat([{buf.Length}], \"{format}\")");
				Assert.That(buf[..charsWritten].ToString(), Is.EqualTo(expected), $"{literal}.TryFormat([{buf.Length}], \"{format}\")");
				Assert.That(charsWritten, Is.EqualTo(expected.Length), $"{literal}.TryFormat([{buf.Length}], \"{format}\")");
				Assert.That(buf[charsWritten..].ToString(), Is.EqualTo(new string('無', buf.Length - charsWritten)), $"{literal}.TryFormat([{buf.Length}], \"{format}\")");
			}

			{
				// with exactly enough space
				Span<char> buf = new char[expected.Length + 10];
				buf.Fill('無');
				var exactSize = buf[..expected.Length];
				Assert.That(literal.TryFormat(exactSize, out int charsWritten, format, null), Is.True, $"{literal}.TryFormat([{exactSize.Length}], \"{format}\")");
				Assert.That(charsWritten, Is.EqualTo(expected.Length), $"{literal}.TryFormat([{exactSize.Length}], \"{format}\")");
				Assert.That(exactSize.ToString(), Is.EqualTo(expected), $"{literal}.TryFormat([{exactSize.Length}], \"{format}\")");
				Assert.That(buf[exactSize.Length..].ToString(), Is.EqualTo(new string('無', buf.Length - exactSize.Length)), $"{literal}.TryFormat([{exactSize.Length}], \"{format}\")");
			}

			{
				// with empty buffer
				Span<char> buf = new char[expected.Length];
				buf.Fill('無');
				var empty = Span<char>.Empty;
				if (expected.Length != 0)
				{
					Assert.That(literal.TryFormat(empty, out int charsWritten, format, null), Is.False, $"{literal}.TryFormat([0], \"{format}\")");
					Assert.That(charsWritten, Is.Zero, $"{literal}.TryFormat([0], \"{format}\")");
					Assert.That(buf.ToString(), Is.EqualTo(new string('無', buf.Length)), $"{literal}.TryFormat([0], \"{format}\")");
				}
				else
				{
					Assert.That(literal.TryFormat(empty, out int charsWritten, format, null), Is.True, $"{literal}.TryFormat([0], \"{format}\")");
					Assert.That(charsWritten, Is.Zero, $"{literal}.TryFormat([0], \"{format}\")");
					Assert.That(buf.ToString(), Is.EqualTo(new string('無', buf.Length)), $"{literal}.TryFormat([0], \"{format}\")");
				}
			}
		}

		[Test]
		public void Test_JsonString_TryFormat()
		{
			Verify_TryFormat_Json(JsonString.Empty);

			Verify_TryFormat("a", "", "a");
			Verify_TryFormat("hello", "", "hello");
			Verify_TryFormat_Json("a");
			Verify_TryFormat_Json("hello");

			Verify_TryFormat("\"", "", "\"");
			Verify_TryFormat("hello\"world", "", "hello\"world");
			Verify_TryFormat("Héllö, 世界!", "", "Héllö, 世界!");
			Verify_TryFormat_Json("\"");
			Verify_TryFormat_Json("hello\"world");
			Verify_TryFormat_Json("Héllö, 世界!");

			Verify_TryFormat("\0", "", "\0");
			Verify_TryFormat("\x12", "", "\x12");
			Verify_TryFormat("\x7F", "", "\x7F");
			Verify_TryFormat("\xFF", "", "\xFF");
			Verify_TryFormat("\uDF34", "", "\uDF34");
			Verify_TryFormat("\uFFFE", "", "\uFFFE");
			Verify_TryFormat("\uFFFF", "", "\uFFFF");
			Verify_TryFormat_Json("\0");
			Verify_TryFormat_Json("\x12");
			Verify_TryFormat_Json("\x7F");
			Verify_TryFormat_Json("\xFF");
			Verify_TryFormat_Json("\uDF34");
			Verify_TryFormat_Json("\uFFFE");
			Verify_TryFormat_Json("\uFFFF");

#if NET6_0_OR_GREATER
			Verify_TryFormat_Json(Slice.Random(Random.Shared, 1024).ToBase64());
#else
			// Random.Shared is not available: use a transient instance
			Verify_TryFormat_Json(Slice.Random(new Random(), 1024).ToBase64());
#endif
		}

		[Test]
		public void Test_JsonNumber_TryFormat()
		{
			Verify_TryFormat(JsonNumber.Zero, "", "0");
			Verify_TryFormat(JsonNumber.DecimalZero, "", "0");
			Verify_TryFormat(JsonNumber.One, "", "1");
			Verify_TryFormat(JsonNumber.DecimalOne, "", "1");
			Verify_TryFormat(JsonNumber.MinusOne, "", "-1");
			Verify_TryFormat_Json(JsonNumber.Zero);
			Verify_TryFormat_Json(JsonNumber.DecimalZero);
			Verify_TryFormat_Json(JsonNumber.One);
			Verify_TryFormat_Json(JsonNumber.DecimalOne);
			Verify_TryFormat_Json(JsonNumber.MinusOne);

			Verify_TryFormat(123, "", "123");
			Verify_TryFormat(-123, "", "-123");
			Verify_TryFormat(123d, "", "123");
			Verify_TryFormat(12.3, "", "12.3");
			Verify_TryFormat(0.123, "", "0.123");
			Verify_TryFormat_Json(123);
			Verify_TryFormat_Json(-123);
			Verify_TryFormat_Json(123d);
			Verify_TryFormat_Json(12.3);
			Verify_TryFormat_Json(0.123);

			Verify_TryFormat(Math.PI, "", PiDoubleText);
			Verify_TryFormat(double.NaN, "", "NaN");
			Verify_TryFormat(double.PositiveInfinity, "", "Infinity");
			Verify_TryFormat(double.NegativeInfinity, "", "-Infinity");
			Verify_TryFormat(double.Epsilon, "", double.Epsilon.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
			Verify_TryFormat_Json(Math.PI);
			Verify_TryFormat_Json(double.NaN);
			Verify_TryFormat_Json(double.PositiveInfinity);
			Verify_TryFormat_Json(double.NegativeInfinity);
			Verify_TryFormat_Json(double.Epsilon);

			Verify_TryFormat_Json(JsonValue.Parse("123"));
			Verify_TryFormat_Json(JsonValue.Parse("123.0"));
			Verify_TryFormat_Json(JsonValue.Parse("123.000"));
		}

		[Test]
		public void Test_JsonBoolean_TryFormat()
		{
			Verify_TryFormat(JsonBoolean.False, "", "false");
			Verify_TryFormat(JsonBoolean.True, "", "true");

			Verify_TryFormat_Json(JsonBoolean.False);
			Verify_TryFormat_Json(JsonBoolean.True);

			Verify_TryFormat(JsonBoolean.False, "P", "false");
			Verify_TryFormat(JsonBoolean.True, "P", "true");

			Verify_TryFormat(JsonBoolean.False, "Q", "false");
			Verify_TryFormat(JsonBoolean.True, "Q", "true");
		}

		[Test]
		public void Test_JsonDateTime_DateTime_TryFormat()
		{
			Verify_TryFormat(DateTime.MinValue, "", "");
			Verify_TryFormat(DateTime.MinValue, "D", "");
			Verify_TryFormat_Json(DateTime.MinValue);
			Verify_TryFormat(DateTime.MinValue, "P", "\"\"");
			Verify_TryFormat(DateTime.MinValue, "Q", "\"\"");

			Verify_TryFormat(DateTime.MaxValue, "", "9999-12-31T23:59:59.9999999");
			Verify_TryFormat(DateTime.MaxValue, "D", "9999-12-31T23:59:59.9999999");
			Verify_TryFormat_Json(DateTime.MaxValue);
			Verify_TryFormat(DateTime.MaxValue, "P", "\"9999-12-31T23:59:59.9999999\"");
			Verify_TryFormat(DateTime.MaxValue, "Q", "\"9999-12-31T23:59:59.9999999\"");

			var now = DateTime.Now;
			Log($"## {now}");
			Verify_TryFormat(now, "", now.ToString("O"));
			Verify_TryFormat(now, "D", now.ToString("O"));
			Verify_TryFormat_Json(now);
			Verify_TryFormat(now, "P", $"\"{now:O}\"");
			Verify_TryFormat(now, "Q", $"\"{now:O}\"");
			Log($"### {now.Date} ({now.Date.Kind})");
			Verify_TryFormat(now.Date, "", now.Date.ToString("yyyy-MM-ddTHH:mm:ssK"));
			Verify_TryFormat(now.Date, "D", now.Date.ToString("yyyy-MM-ddTHH:mm:ssK"));
			Verify_TryFormat_Json(now.Date);
			Verify_TryFormat(now.Date, "P", $"\"{now.Date:yyyy-MM-ddTHH:mm:ssK}\"");
			Verify_TryFormat(now.Date, "Q", $"\"{now.Date:yyyy-MM-ddTHH:mm:ssK}\"");

			var utcNow = DateTime.UtcNow;
			Log($"## {utcNow}");
			Verify_TryFormat(utcNow, "", utcNow.ToString("O"));
			Verify_TryFormat(utcNow, "D", utcNow.ToString("O"));
			Verify_TryFormat_Json(utcNow);
			Log($"### {utcNow.Date} ({utcNow.Date.Kind})");
			Verify_TryFormat(utcNow.Date, "", utcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssK"));
			Verify_TryFormat(utcNow.Date, "D", utcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssK"));
			Verify_TryFormat_Json(utcNow.Date);
			Verify_TryFormat(utcNow.Date, "P", $"\"{utcNow.Date:yyyy-MM-ddTHH:mm:ssK}\"");
			Verify_TryFormat(utcNow.Date, "Q", $"\"{utcNow.Date:yyyy-MM-ddTHH:mm:ssK}\"");
		}

		[Test]
		public void Test_JsonDateTime_DateTimeOffset_TryFormat()
		{
			Verify_TryFormat(DateTimeOffset.MinValue, "", "");
			Verify_TryFormat(DateTimeOffset.MaxValue, "", "9999-12-31T23:59:59.9999999");
			Verify_TryFormat_Json(DateTimeOffset.MinValue);
			Verify_TryFormat_Json(DateTimeOffset.MaxValue);

			var now = DateTimeOffset.Now;
			Log($"## {now}");
			Verify_TryFormat(now, "", now.ToString("O", CultureInfo.InvariantCulture));
			Verify_TryFormat_Json(now);
			Log($"### {now.Date} ({now.Date.Kind})");
			Verify_TryFormat(now.Date, "", now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			Verify_TryFormat_Json(now.Date);

			var utcNow = DateTimeOffset.UtcNow;
			Log($"## {utcNow}");
			Verify_TryFormat(utcNow, "", utcNow.ToString("O", CultureInfo.InvariantCulture));
			Verify_TryFormat_Json(utcNow);
			Log($"### {utcNow.Date} ({utcNow.Date.Kind})");
			Verify_TryFormat(utcNow.Date, "", utcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			Verify_TryFormat_Json(utcNow.Date);
		}

		[Test]
		public void Test_JsonDateTime_DateOnly_TryFormat()
		{
			var today = DateOnly.FromDateTime(DateTime.Now);

			Verify_TryFormat(DateOnly.MinValue, "", "");
			Verify_TryFormat(DateOnly.MaxValue, "", "9999-12-31");
			Verify_TryFormat(today, "", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

			Verify_TryFormat_Json(DateOnly.MinValue);
			Verify_TryFormat_Json(DateOnly.MaxValue);
			Verify_TryFormat_Json(today);
		}

		[Test]
		public void Test_JsonArray_TryFormat()
		{
			Verify_TryFormat_Json(JsonArray.ReadOnly.Empty);
			Verify_TryFormat_Json(JsonArray.Create("Hello"));
			Verify_TryFormat_Json(JsonArray.Create("Hello", "World"));
			Verify_TryFormat_Json(JsonArray.Create(123));
			Verify_TryFormat_Json(JsonArray.Create(true));
			Verify_TryFormat_Json(JsonArray.Create("Hello", 123, true, "World"));
		}

		[Test]
		public void Test_JsonObject_TryFormat()
		{
			Verify_TryFormat_Json(JsonObject.ReadOnly.Empty);
			Verify_TryFormat_Json(JsonObject.Create(("Hello", "World")));
			Verify_TryFormat_Json(JsonObject.Create(("Hello", 123)));
			Verify_TryFormat_Json(JsonObject.Create(("Hello", true)));
			Verify_TryFormat_Json(JsonObject.Create([ ("Hello", "World"), ("Foo", 123), ("Bar", true) ]));
		}

		[Test]
		public void Test_PropertyNames_CrystalJson()
		{
			var instance = new DummyCrystalJsonTextPropertyNames()
			{
				HelloWorld = "hello", // => "helloWorld": "hello"
				Foo = "world",        // => "bar": "world"
			};

			var json = CrystalJson.Serialize(instance, CrystalJsonSettings.Json);
			Log(json);
			Assert.That(json, Is.EqualTo("{ \"helloWorld\": \"hello\", \"bar\": \"world\" }"));

			var obj = JsonObject.FromObject(instance);
			Dump(obj);
			Assert.That(obj["helloWorld"], IsJson.EqualTo("hello"));
			Assert.That(obj["bar"], IsJson.EqualTo("world"));

			var decoded = obj.As<DummyCrystalJsonTextPropertyNames>()!;
			Assert.That(decoded.HelloWorld, Is.EqualTo("hello"));
			Assert.That(decoded.Foo, Is.EqualTo("world"));
		}

		[Test]
		public void Test_PropertyNames_SystemTextJson()
		{
			var instance = new DummySystemJsonTextPropertyNames()
			{
				HelloWorld = "hello", // => "helloWorld": "hello"
				Foo = "world",        // => "bar": "world"
			};

			var json = CrystalJson.Serialize(instance, CrystalJsonSettings.Json);
			Log(json);
			Assert.That(json, Is.EqualTo("{ \"helloWorld\": \"hello\", \"bar\": \"world\" }"));

			var obj = JsonObject.FromObject(instance);
			Dump(obj);
			Assert.That(obj["helloWorld"], IsJson.EqualTo("hello"));
			Assert.That(obj["bar"], IsJson.EqualTo("world"));

			var decoded = obj.As<DummySystemJsonTextPropertyNames>()!;
			Assert.That(decoded.HelloWorld, Is.EqualTo("hello"));
			Assert.That(decoded.Foo, Is.EqualTo("world"));
		}

		[Test]
		public void Test_PropertyNames_NewtonsoftJson()
		{
			var instance = new DummyNewtonsoftJsonPropertyNames()
			{
				HelloWorld = "hello", // => "helloWorld": "hello"
				Foo = "world",        // => "bar": "world"
			};

			var json = CrystalJson.Serialize(instance, CrystalJsonSettings.Json);
			Log(json);
			Assert.That(json, Is.EqualTo("{ \"helloWorld\": \"hello\", \"bar\": \"world\" }"));

			var obj = JsonObject.FromObject(instance);
			Dump(obj);
			Assert.That(obj["helloWorld"], IsJson.EqualTo("hello"));
			Assert.That(obj["bar"], IsJson.EqualTo("world"));

			var decoded = obj.As<DummyNewtonsoftJsonPropertyNames>()!;
			Assert.That(decoded.HelloWorld, Is.EqualTo("hello"));
			Assert.That(decoded.Foo, Is.EqualTo("world"));
		}

		[Test]
		public void Test_Cannot_Serialize_Tasks()
		{
			// on common mistake is forgetting the "await" keyword,
			// and trying to serialize a Task<Foo> instead of a Foo

			var foo = new { Hello = "There" };
			var taskFoo = Task.FromResult(foo);

			// Serialize<T>
			Assert.That(() => CrystalJson.Serialize(foo), Throws.Nothing);
			Assert.That(() => CrystalJson.Serialize(taskFoo), Throws.InstanceOf<JsonSerializationException>().With.Message.Contains("'await'"));

			// FromValue<T>
			Assert.That(() => JsonValue.FromValue(foo), Throws.Nothing);
			Assert.That(() => JsonValue.FromValue(taskFoo), Throws.InstanceOf<JsonSerializationException>().With.Message.Contains("'await'"));
		}

	}

}
