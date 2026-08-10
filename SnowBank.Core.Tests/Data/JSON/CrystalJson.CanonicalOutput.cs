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

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_Canonical_Settings_Flag()
		{
			var canonical = CrystalJsonSettings.Json.Canonical();
			Assert.That(canonical.IsCanonicalOutput, Is.True);
			Assert.That(CrystalJsonSettings.Json.IsCanonicalOutput, Is.False);
			// settings instances are cached per flag combination: same flags, same instance
			Assert.That(CrystalJsonSettings.Json.Canonical(), Is.SameAs(canonical));
			// composable with other settings
			Assert.That(CrystalJsonSettings.JsonCompact.Canonical().IsCanonicalOutput, Is.True);
			Assert.That(canonical.WithNullMembers().IsCanonicalOutput, Is.True);
		}

#if !NETFRAMEWORK

		[Test]
		public void Test_Canonical_Number_Rendering()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();

			// (input value, expected canonical text); the worked-example table of the design, section 5
			Assert.Multiple(() =>
			{
				// int-shaped
				Assert.That(JsonNumber.Return(1).ToJsonText(canonical), Is.EqualTo("1"));
				Assert.That(JsonNumber.Return(long.MaxValue).ToJsonText(canonical), Is.EqualTo("9223372036854775807"));
				Assert.That(JsonNumber.Return(ulong.MaxValue).ToJsonText(canonical), Is.EqualTo("18446744073709551615"));
				// float-shaped whole values carry the .0 marker
				Assert.That(JsonNumber.Return(1.0d).ToJsonText(canonical), Is.EqualTo("1.0"));
				Assert.That(JsonNumber.Return(100.0d).ToJsonText(canonical), Is.EqualTo("100.0"));
				Assert.That(JsonNumber.Return(-0.0d).ToJsonText(canonical), Is.EqualTo("0.0"));
				Assert.That(JsonNumber.Return(1e20d).ToJsonText(canonical), Is.EqualTo("100000000000000000000.0"));
				// ES6 exponent decoration: lowercase e, sign only when positive, no zero padding
				Assert.That(JsonNumber.Return(1e21d).ToJsonText(canonical), Is.EqualTo("1e+21"));
				Assert.That(JsonNumber.Return(1e-7d).ToJsonText(canonical), Is.EqualTo("1e-7"));
				Assert.That(JsonNumber.Return(0.000001d).ToJsonText(canonical), Is.EqualTo("0.000001"));
				Assert.That(JsonNumber.Return(double.MaxValue).ToJsonText(canonical), Is.EqualTo("1.7976931348623157e+308"));
				Assert.That(JsonNumber.Return(5e-324d).ToJsonText(canonical), Is.EqualTo("5e-324"));
				Assert.That(JsonNumber.Return(0.1d + 0.2d).ToJsonText(canonical), Is.EqualTo("0.30000000000000004"));
				Assert.That(JsonNumber.Return(3.141592653589793d).ToJsonText(canonical), Is.EqualTo("3.141592653589793"));
				// decimal scale is normalized
				Assert.That(JsonNumber.Return(1.10m).ToJsonText(canonical), Is.EqualTo("1.1"));
				Assert.That(JsonNumber.Return(1.1m).ToJsonText(canonical), Is.EqualTo("1.1"));
				Assert.That(JsonNumber.Return(100m).ToJsonText(canonical), Is.EqualTo("100.0"));
			});
		}

		[Test]
		public void Test_Canonical_Number_Rendering_From_Parsed_Literals()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();

			// the literal never drives the digits; it only decides the int/float shape
			Assert.Multiple(() =>
			{
				Assert.That(CrystalJson.Parse("1").ToJsonText(canonical), Is.EqualTo("1"));
				Assert.That(CrystalJson.Parse("1.0").ToJsonText(canonical), Is.EqualTo("1.0"));
				Assert.That(CrystalJson.Parse("1E1").ToJsonText(canonical), Is.EqualTo("10.0"));
				Assert.That(CrystalJson.Parse("1.10").ToJsonText(canonical), Is.EqualTo("1.1"));
				Assert.That(CrystalJson.Parse("-0").ToJsonText(canonical), Is.EqualTo("0"));
				// integer literal too large for ulong: parses into the Decimal kind, stays int-shaped
				Assert.That(CrystalJson.Parse("99999999999999999999999").ToJsonText(canonical), Is.EqualTo("99999999999999999999999"));
			});
		}

		[Test]
		public void Test_Canonical_Rejects_NonFinite()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			Assert.Throws<JsonSerializationException>(() => JsonNumber.Return(double.NaN).ToJsonText(canonical));
			Assert.Throws<JsonSerializationException>(() => JsonNumber.Return(double.PositiveInfinity).ToJsonText(canonical));
		}

		[Test]
		public void Test_Canonical_Member_Ordering()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();

			// ordinal case-sensitive: 'B' (0x42) sorts before 'b' (0x62), per RFC 8785
			var obj = new JsonObject
			{
				["bar"] = JsonNumber.Return(2),
				["Baz"] = JsonNumber.Return(1),
				["alpha"] = JsonNumber.Return(3),
			};
			Assert.That(obj.ToJsonText(canonical), Is.EqualTo("""{"Baz":1,"alpha":3,"bar":2}"""));

			// recursion: nested objects sort too, including inside arrays
			var nested = new JsonObject
			{
				["z"] = new JsonObject { ["b"] = JsonNumber.Return(1), ["a"] = JsonNumber.Return(2) },
				["a"] = JsonArray.Create(new JsonObject { ["y"] = JsonNumber.Return(1), ["x"] = JsonNumber.Return(2) }),
			};
			Assert.That(nested.ToJsonText(canonical), Is.EqualTo("""{"a":[{"x":2,"y":1}],"z":{"a":2,"b":1}}"""));

			// without the flag, insertion order is preserved (unchanged behavior)
			Assert.That(obj.ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"bar":2,"Baz":1,"alpha":3}"""));
		}

		public sealed record CanonicalAcmeOrder(string Id, string Customer, decimal Total, double Weight);

		[Test]
		public void Test_Canonical_Typed_Serialization()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			var order = new CanonicalAcmeOrder("X-42", "acme", Total: 9.90m, Weight: 1.0d);

			// members sorted, decimal scale normalized, whole double keeps its float marker
			string text = CrystalJson.Serialize(order, canonical);
			Assert.That(text, Is.EqualTo("""{"Customer":"acme","Id":"X-42","Total":9.9,"Weight":1.0}"""));

			// the core invariant: parse-then-reserialize equals built-from-scratch
			Assert.That(CrystalJson.SerializeJson(CrystalJson.Parse(text), canonical), Is.EqualTo(text));

			// bytes route agrees with the text route
			Assert.That(CrystalJson.ToSlice(order, canonical).ToStringUtf8(), Is.EqualTo(text));
		}

		[Test]
		public void Test_Canonical_Typed_Scalar_Serialization()
		{
			// top-level scalars go through VisitValue<T>'s Release-mode JIT_HACK fast path;
			// this is the discriminator that catches a fast path left ungated by IsCanonicalOutput
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			Assert.That(CrystalJson.Serialize(1.0d, canonical), Is.EqualTo("1.0"));
			Assert.That(CrystalJson.Serialize(42, canonical), Is.EqualTo("42"));
		}

		[Test]
		public void Test_Canonical_Typed_Serializer_Branches_Match_Visitor_Route()
		{
			// the three IJsonSerializer<T> canonical branches (CrystalJson.Serialize, ToSlice, and
			// ToSlice with a pool) all detour through Pack() + JsonSerialize(); prove each one against
			// the plain DOM-visitor route (no serializer), using a real generated converter already
			// exercised elsewhere in this suite.
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			var dto = new VocabularyDto { Id = 42, Label = "hello", Enabled = true };
			var serializer = ModernSpellingSerializers.VocabularyDto.Default;

			string expected = CrystalJson.Serialize(dto, canonical);

			Assert.That(CrystalJson.Serialize(dto, serializer, canonical), Is.EqualTo(expected));
			Assert.That(CrystalJson.ToSlice(dto, serializer, canonical).ToStringUtf8(), Is.EqualTo(expected));
			using var owner = CrystalJson.ToSlice(dto, serializer, null, canonical);
			Assert.That(owner.Data.ToStringUtf8(), Is.EqualTo(expected));
		}

		/// <summary>Serializer that implements only the writing facet, not <see cref="IJsonPacker{T}"/></summary>
		private sealed class NonPackableSerializer : IJsonSerializer<VocabularyDto>
		{
			public void Serialize(CrystalJsonWriter writer, VocabularyDto? instance) => writer.WriteValue("unused");
		}

		[Test]
		public void Test_Canonical_Serialize_Rejects_NonPackableSerializer()
		{
			// canonical output packs to the DOM first (sorted members, normalized numbers), so a
			// serializer that cannot pack cannot honor the contract: it must fail loudly, not silently
			// fall back to its own (non-canonical) writing order.
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			var dto = new VocabularyDto { Id = 1 };
			Assert.Throws<JsonSerializationException>(() => CrystalJson.Serialize(dto, new NonPackableSerializer(), canonical));
		}

		[Test]
		public void Test_Canonical_Closed_Under_Reparse()
		{
			// serialize(parse(serialize(x)), s) == serialize(x, s) for every supported combination s
			var combos = new[]
			{
				CrystalJsonSettings.Json.Canonical(),
				CrystalJsonSettings.JsonCompact.Canonical(),
				CrystalJsonSettings.JsonCompact.Canonical().WithNullMembers(),
			};
			var subjects = new JsonValue[]
			{
				CrystalJson.Parse("""{"zeta":1.0,"alpha":1.10,"beta":3,"Gamma":{"y":1E1,"x":[1,2.5,true,null,"text"]}}"""),
				JsonObject.Create("x", JsonNumber.Return(1e21d)),
				JsonArray.Create(JsonNumber.Return(0.1d + 0.2d), JsonString.Return("Ünïcode \"quoted\""), JsonNumber.Return(long.MinValue)),
			};
			foreach (var s in combos)
			{
				foreach (var subject in subjects)
				{
					string once = CrystalJson.SerializeJson(subject, s);
					string twice = CrystalJson.SerializeJson(CrystalJson.Parse(once), s);
					Assert.That(twice, Is.EqualTo(once), $"not closed under reparse for settings {s.Flags}");
				}
			}
		}

		[Test]
		public void Test_Canonical_Frozen_Corpus()
		{
			// FROZEN: these exact strings are the canonical output contract (design, section 2).
			// A failure here means a release changed canonical bytes: that is a conscious decision
			// with a release-note entry, never a casual edit of the expected value.
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			var doc = CrystalJson.Parse("""{"zeta":1.0,"alpha":1.10,"Baz":1E1,"bar":{"b":18446744073709551615,"a":[1e-7,0.000001,-0]}}""");
			Assert.That(
				CrystalJson.SerializeJson(doc, canonical),
				Is.EqualTo("""{"Baz":10.0,"alpha":1.1,"bar":{"a":[1e-7,0.000001,0],"b":18446744073709551615},"zeta":1.0}"""));

			// FROZEN: same document, CrystalJsonSettings.Json's default layout (single-line, spaced).
			// Sanity-checked: keys still sort ordinally (Baz, alpha, bar, zeta), numbers are still
			// canonical (10.0, 1.1, 1e-7, 0.000001, 0, the full-width ulong, 1.0); only the spacing differs.
			Assert.That(
				CrystalJson.SerializeJson(doc, CrystalJsonSettings.Json.Canonical()),
				Is.EqualTo("""{ "Baz": 10.0, "alpha": 1.1, "bar": { "a": [ 1e-7, 0.000001, 0 ], "b": 18446744073709551615 }, "zeta": 1.0 }"""));

			// FROZEN: same document, compact layout plus WithNullMembers(). This document has no null
			// member, so the string is byte-identical to the plain compact case above; the point of this
			// case is pinning that the combination itself does not change canonical output for a
			// null-free document (a document with a null member is out of scope: canonical ordering and
			// number rendering are the contract this corpus pins, not null-member visibility).
			Assert.That(
				CrystalJson.SerializeJson(doc, CrystalJsonSettings.JsonCompact.Canonical().WithNullMembers()),
				Is.EqualTo("""{"Baz":10.0,"alpha":1.1,"bar":{"a":[1e-7,0.000001,0],"b":18446744073709551615},"zeta":1.0}"""));
		}

		// --- Task 3 follow-up: decimal-kind canonical text must be value-determined, not kind-determined ---
		// (coordinator finding via Task 6's closure-invariant tests; see task-3-report.md)

		[Test]
		public void Test_Canonical_Decimal_MaxValue_Does_Not_Throw()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			// decimal.MaxValue is a whole number outside the 64-bit signed range, so IsFloatShaped
			// classifies it int-shaped: it never reaches the new double-round-trip guard (that branch
			// only runs for a float-shaped Kind.Decimal). This still exercises the canonical path
			// end-to-end and proves no OverflowException anywhere on the way.
			string once = JsonNumber.Return(decimal.MaxValue).ToJsonText(canonical);
			Assert.That(once, Does.Not.Contain("E"));
			Assert.That(once, Does.Not.Contain("."));
			string twice = CrystalJson.Parse(once).ToJsonText(canonical);
			Assert.That(twice, Is.EqualTo(once));
		}

		[Test]
		public void Test_Canonical_High_Precision_Decimal_Stays_Plain()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			// 18 significant digits: exceeds a double's ~15-17 digit precision, so the round-trip
			// back-conversion cannot be exact and the value must keep full-digit plain notation.
			var value = JsonNumber.Return(3.141592653589793238m);
			string once = value.ToJsonText(canonical);
			Assert.That(once, Is.EqualTo("3.141592653589793238"));

			// NOT asserting reparse-closure here: CrystalJsonParser.ParseNumberFromLiteral tries
			// double.TryParse before decimal.TryParse for any literal with a '.', and double.TryParse
			// succeeds (losing precision) for every magnitude a decimal can hold. A dotted literal
			// can therefore never land back on Kind.Decimal through the parser, regardless of the
			// canonical formatter. That is a pre-existing parser precision limit, not a defect in
			// this fix; the frozen corpus and the closure test use no value that hits it.
		}

#endif

#if NETFRAMEWORK

		[Test]
		public void Test_Canonical_Not_Supported_On_NetFramework()
		{
			var canonical = CrystalJsonSettings.JsonCompact.Canonical();
			Assert.Throws<NotSupportedException>(() => new JsonObject { ["a"] = JsonNumber.Return(1) }.ToJsonText(canonical));
			Assert.Throws<NotSupportedException>(() => JsonNumber.Return(1.5d).ToJsonText(canonical));
		}

#endif

	}

}
