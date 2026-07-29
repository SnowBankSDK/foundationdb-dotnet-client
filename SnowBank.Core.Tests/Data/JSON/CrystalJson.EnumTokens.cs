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

namespace SnowBank.Data.Json.Tests
{
	using System.Runtime.Serialization;

	/// <summary>Pins the enum wire behavior: the text-writer and DOM routes must agree, settings are honored on both,
	/// and custom tokens (<c>[JsonStringEnumMemberName]</c>, <c>[EnumMember(Value=...)]</c>) drive both directions.</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonEnumTokenFacts : SimpleTest
	{

		/// <summary>Control: no attributes anywhere</summary>
		public enum PlainKind
		{
			Unknown = 0,
			Paper = 1,
			Electronic = 2,
		}

		/// <summary>Domain codes via the DataContract/Newtonsoft spelling</summary>
		public enum CourierKind
		{
			[EnumMember(Value = "C")]
			Paper = 0,

			[EnumMember(Value = "E")]
			Electronic = 1,

			// deliberately bare: [EnumMember] without Value must keep the .NET name
			[EnumMember]
			Hybrid = 2,
		}

		/// <summary>One token member and one plain member, to pin the camelCasing exclusion</summary>
		public enum MixedKind
		{
			[EnumMember(Value = "XL")]
			BigFormat = 0,

			SmallFormat = 1,
		}

		[Flags]
		public enum Access
		{
			None = 0,
			Read = 1,
			Write = 2,
			ReadWrite = 3,
			Delete = 4,
		}

		[Flags]
		public enum TokenAccess
		{
			[EnumMember(Value = "r")]
			Read = 1,

			[EnumMember(Value = "w")]
			Write = 2,

			[EnumMember(Value = "rw")]
			ReadWrite = 3,
		}

#if NET9_0_OR_GREATER
		/// <summary>Domain codes via the System.Text.Json 9+ spelling</summary>
		public enum StjKind
		{
			[System.Text.Json.Serialization.JsonStringEnumMemberName("C")]
			Paper = 0,

			[System.Text.Json.Serialization.JsonStringEnumMemberName("E")]
			Electronic = 1,

			// both spellings on one field: the STJ one must win
			[System.Text.Json.Serialization.JsonStringEnumMemberName("H")]
			[EnumMember(Value = "x")]
			Hybrid = 2,
		}
#endif

		public sealed class Parcel
		{
			public CourierKind Kind { get; set; }
		}

		[Test]
		public void Test_Enums_Serialize_As_Strings_By_Default()
		{
			// enums default to their string form on ALL routes (a deliberate divergence from STJ, for javascript-client compat);
			// WithEnumAsNumbers() is the opt-in for the numeric wire
			Assert.That(CrystalJson.Serialize(DayOfWeek.Friday), Is.EqualTo("\"Friday\""), "text route defaults to the string form");
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday), Is.InstanceOf<JsonString>(), "DOM route defaults to the string form");
			Assert.That(CrystalJson.Serialize(CourierKind.Paper), Is.EqualTo("\"C\""), "custom tokens shape the default form");
			Assert.That(CrystalJson.Serialize(Access.ReadWrite | Access.Delete), Is.EqualTo("\"ReadWrite, Delete\""), "flags compose in the default form");

			var numbers = CrystalJsonSettings.Json.WithEnumAsNumbers();
			Assert.That(CrystalJson.Serialize(DayOfWeek.Friday, numbers), Is.EqualTo("5"), "WithEnumAsNumbers() restores the numeric wire");
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday, numbers), Is.InstanceOf<JsonNumber>(), "on the DOM route as well");

			// reads stay tolerant regardless of the write default: names and tokens any-case, numbers, numeric strings
			Assert.That(CrystalJson.Deserialize<DayOfWeek>("\"friday\""), Is.EqualTo(DayOfWeek.Friday));
			Assert.That(CrystalJson.Deserialize<DayOfWeek>("\"FRIDAY\""), Is.EqualTo(DayOfWeek.Friday));
			Assert.That(CrystalJson.Deserialize<DayOfWeek>("5"), Is.EqualTo(DayOfWeek.Friday));
			Assert.That(CrystalJson.Deserialize<DayOfWeek>("\"5\""), Is.EqualTo(DayOfWeek.Friday));
		}

		[Test]
		public void Test_Dom_Route_Honors_Enum_Settings()
		{
			// with WithEnumAsNumbers(), the DOM route must emit numbers, agreeing with the text route
			var numbers = CrystalJsonSettings.Json.WithEnumAsNumbers();
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday, numbers), Is.InstanceOf<JsonNumber>(), "DOM route must honor WithEnumAsNumbers()");
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday, numbers).ToInt32(), Is.EqualTo(5));

			var strings = CrystalJsonSettings.Json.WithEnumAsStrings();
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday, strings), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday, strings).ToStringOrDefault(), Is.EqualTo("Friday"));

			var camel = CrystalJsonSettings.Json.WithEnumAsStrings(camelCased: true);
			Assert.That(JsonValue.FromValue(DayOfWeek.Friday, camel).ToStringOrDefault(), Is.EqualTo("friday"), "DOM route must honor UseCamelCasingForEnums");
		}

		[Test]
		public void Test_Text_And_Dom_Routes_Agree()
		{
			// the same object, serialized through the text writer and through the DOM, must produce the same bytes
			var composite = Access.ReadWrite | Access.Delete; // 7: not a declared member, must compose

			foreach (var settings in new[] { CrystalJsonSettings.Json, CrystalJsonSettings.Json.WithEnumAsStrings(), CrystalJsonSettings.Json.WithEnumAsStrings(camelCased: true) })
			{
				Assert.That(
					JsonValue.FromValue(composite, settings).ToJsonText(settings),
					Is.EqualTo(CrystalJson.Serialize(composite, settings)),
					$"DOM and text routes disagree for flags value {composite} with settings {settings.Flags}");

				Assert.That(
					JsonValue.FromValue(DayOfWeek.Friday, settings).ToJsonText(settings),
					Is.EqualTo(CrystalJson.Serialize(DayOfWeek.Friday, settings)),
					$"DOM and text routes disagree for DayOfWeek.Friday with settings {settings.Flags}");
			}
		}

		[Test]
		public void Test_Flags_Composition_Prefers_Composites()
		{
			var strings = CrystalJsonSettings.Json.WithEnumAsStrings();

			// Enum.ToString("G") composes with "more bits first", so ReadWrite is preferred over Read + Write
			Assert.That(CrystalJson.Serialize(Access.Read | Access.Write, strings), Is.EqualTo("\"ReadWrite\""));
			Assert.That(CrystalJson.Serialize(Access.ReadWrite | Access.Delete, strings), Is.EqualTo("\"ReadWrite, Delete\""));

			// the DOM route must produce the same composition
			Assert.That(JsonValue.FromValue(Access.ReadWrite | Access.Delete, strings).ToStringOrDefault(), Is.EqualTo("ReadWrite, Delete"));
		}

		[Test]
		public void Test_NoToken_Enum_String_Form_Matches_ToString_G()
		{
			// for enums without custom tokens, the string form must be exactly ToString("G"), on both routes
			var strings = CrystalJsonSettings.Json.WithEnumAsStrings();
			Access[] values = [ Access.None, Access.Read, (Access) 3, (Access) 7, (Access) 64, (Access) 6 ];
			foreach (var value in values)
			{
				var expected = "\"" + value.ToString("G") + "\"";
				Assert.That(CrystalJson.Serialize(value, strings), Is.EqualTo(expected), $"text route for {value:D}");
				Assert.That(JsonValue.FromValue(value, strings).ToJsonText(), Is.EqualTo(expected), $"DOM route for {value:D}");
			}

			// non-flags undefined value renders as its number, in a string
			Assert.That(CrystalJson.Serialize((PlainKind) 123, strings), Is.EqualTo("\"123\""));
			Assert.That(JsonValue.FromValue((PlainKind) 123, strings).ToJsonText(), Is.EqualTo("\"123\""));
		}

		[Test]
		public void Test_EnumMember_Tokens_On_Write()
		{
			var strings = CrystalJsonSettings.Json.WithEnumAsStrings();

			// text route
			Assert.That(CrystalJson.Serialize(CourierKind.Paper, strings), Is.EqualTo("\"C\""));
			Assert.That(CrystalJson.Serialize(CourierKind.Electronic, strings), Is.EqualTo("\"E\""));
			Assert.That(CrystalJson.Serialize(CourierKind.Hybrid, strings), Is.EqualTo("\"Hybrid\""), "a bare [EnumMember] keeps the .NET name");

			// DOM route
			Assert.That(JsonValue.FromValue(CourierKind.Paper, strings).ToStringOrDefault(), Is.EqualTo("C"));

			// the string form is the default, so tokens shape the default wire; the numeric opt-out still works
			Assert.That(CrystalJson.Serialize(CourierKind.Electronic), Is.EqualTo("\"E\""));
			Assert.That(CrystalJson.Serialize(CourierKind.Electronic, CrystalJsonSettings.Json.WithEnumAsNumbers()), Is.EqualTo("1"));

			// end to end through a DTO member
			Assert.That(CrystalJson.Serialize(new Parcel { Kind = CourierKind.Electronic }, strings), Does.Contain("\"E\""));
		}

		[Test]
		public void Test_EnumMember_Tokens_On_Read()
		{
			// the token reads back
			Assert.That(CrystalJson.Deserialize<CourierKind>("\"C\""), Is.EqualTo(CourierKind.Paper));
			// case-insensitively
			Assert.That(CrystalJson.Deserialize<CourierKind>("\"c\""), Is.EqualTo(CourierKind.Paper));
			// the .NET name is still accepted (lenient parse)
			Assert.That(CrystalJson.Deserialize<CourierKind>("\"Paper\""), Is.EqualTo(CourierKind.Paper));
			// numbers and numeric strings are still accepted
			Assert.That(CrystalJson.Deserialize<CourierKind>("1"), Is.EqualTo(CourierKind.Electronic));
			Assert.That(CrystalJson.Deserialize<CourierKind>("\"1\""), Is.EqualTo(CourierKind.Electronic));

			// through a DTO member
			Assert.That(CrystalJson.Deserialize<Parcel>("""{ "Kind": "E" }""").Kind, Is.EqualTo(CourierKind.Electronic));

			// and through the DOM accessors
			Assert.That(JsonString.Return("E").As<CourierKind>(), Is.EqualTo(CourierKind.Electronic));
			Assert.That(JsonObject.Parse("""{ "k": "C" }""").Get<CourierKind>("k"), Is.EqualTo(CourierKind.Paper));
		}

		[Test]
		public void Test_Token_Flags_Compose_And_Parse()
		{
			var strings = CrystalJsonSettings.Json.WithEnumAsStrings();

			// composite-preferring write, with tokens
			Assert.That(CrystalJson.Serialize(TokenAccess.Read | TokenAccess.Write, strings), Is.EqualTo("\"rw\""));
			Assert.That(CrystalJson.Serialize(TokenAccess.Read, strings), Is.EqualTo("\"r\""));

			// tolerant read: single token, token list, names, numbers
			Assert.That(CrystalJson.Deserialize<TokenAccess>("\"rw\""), Is.EqualTo(TokenAccess.ReadWrite));
			Assert.That(CrystalJson.Deserialize<TokenAccess>("\"r, w\""), Is.EqualTo(TokenAccess.ReadWrite));
			Assert.That(CrystalJson.Deserialize<TokenAccess>("\"Read, Write\""), Is.EqualTo(TokenAccess.ReadWrite));
			Assert.That(CrystalJson.Deserialize<TokenAccess>("3"), Is.EqualTo(TokenAccess.ReadWrite));
		}

		[Test]
		public void Test_CamelCasing_Not_Applied_To_Tokens()
		{
			// D-2: naming policies do not apply to names explicitly set via attributes (same as STJ)
			var camel = CrystalJsonSettings.Json.WithEnumAsStrings(camelCased: true);
			Assert.That(CrystalJson.Serialize(MixedKind.BigFormat, camel), Is.EqualTo("\"XL\""), "an attribute-set token must not be camelCased");
			Assert.That(CrystalJson.Serialize(MixedKind.SmallFormat, camel), Is.EqualTo("\"smallFormat\""), "a plain member name is camelCased");
		}

#if NET9_0_OR_GREATER
		[Test]
		public void Test_JsonStringEnumMemberName_Is_Recognized()
		{
			var strings = CrystalJsonSettings.Json.WithEnumAsStrings();

			Assert.That(CrystalJson.Serialize(StjKind.Paper, strings), Is.EqualTo("\"C\""));
			Assert.That(CrystalJson.Deserialize<StjKind>("\"E\""), Is.EqualTo(StjKind.Electronic));

			// when both spellings are present, the STJ attribute wins
			Assert.That(CrystalJson.Serialize(StjKind.Hybrid, strings), Is.EqualTo("\"H\""));
			Assert.That(CrystalJson.Deserialize<StjKind>("\"H\""), Is.EqualTo(StjKind.Hybrid));
			// but the loser is still accepted on read (lenient parse)
			Assert.That(CrystalJson.Deserialize<StjKind>("\"x\""), Is.EqualTo(StjKind.Hybrid));
		}
#endif

	}

}
