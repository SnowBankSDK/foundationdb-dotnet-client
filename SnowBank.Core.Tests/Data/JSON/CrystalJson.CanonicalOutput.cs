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

	}

}
