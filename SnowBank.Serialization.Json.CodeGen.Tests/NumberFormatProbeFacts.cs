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

namespace SnowBank.Serialization.Json.CodeGen.Tests
{

	#region Probe types...

	public sealed record ProbeNumberFormatDto
	{

		[JsonProperty(NumberFormat = JsonNumberFormat.String)]
		public long AccountId { get; set; }

		[JsonProperty(NumberFormat = JsonNumberFormat.String)]
		public decimal Balance { get; set; }

		public long Plain { get; set; }

	}

	#endregion

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProbeNumberFormatDto))]
	public static partial class ProbeNumberFormatHost
	{
	}

	/// <summary>Pins <c>[JsonProperty(NumberFormat = String)]</c> in generated converters, and their byte parity with the reflection path</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public class NumberFormatProbeFacts : SimpleTest
	{

		[Test]
		public void Test_Generated_Converter_Forces_The_String_Form()
		{
			var dto = new ProbeNumberFormatDto { AccountId = 12345678901234567, Balance = 12.50m, Plain = 42 };

			var obj = JsonObject.Parse(ProbeNumberFormatHost.ProbeNumberFormatDto.ToJsonText(dto));
			Log(obj.ToJsonText());

			Assert.That(obj["AccountId"], Is.InstanceOf<JsonString>(), "the generated converter honors [JsonProperty(NumberFormat=String)]");
			Assert.That(obj.Get<string>("AccountId"), Is.EqualTo("12345678901234567"));
			Assert.That(obj["Balance"], Is.InstanceOf<JsonString>());
			Assert.That(obj.Get<string>("Balance"), Is.EqualTo("12.50"), "the string is the numeric literal, decimal scale included");
			Assert.That(obj["Plain"], Is.InstanceOf<JsonNumber>(), "a member without the attribute keeps the numeric form");
		}

		[Test]
		public void Test_Generated_Pack_Forces_The_String_Form()
		{
			var dto = new ProbeNumberFormatDto { AccountId = 12345678901234567, Plain = 42 };

			var obj = ProbeNumberFormatHost.ProbeNumberFormatDto.Pack(dto).AsObject();

			Assert.That(obj["AccountId"], Is.InstanceOf<JsonString>(), "the generated Pack honors the attribute, same as the reflection DOM route");
			Assert.That(obj["Plain"], Is.InstanceOf<JsonNumber>());
		}

		[Test]
		public void Test_Both_Paths_Produce_The_Same_Bytes()
		{
			// one dto through both paths, compared: separate green suites never prove agreement
			var dto = new ProbeNumberFormatDto { AccountId = long.MaxValue, Balance = 0.001m, Plain = -1 };

			var generated  = ProbeNumberFormatHost.ProbeNumberFormatDto.ToJsonText(dto);
			var reflection = CrystalJson.Serialize(dto);

			Assert.That(generated, Is.EqualTo(reflection));
		}

		[Test]
		public void Test_Writable_Proxy_Setter_Writes_The_String_Form()
		{
			var dto = new ProbeNumberFormatDto { AccountId = 1, Plain = 1 };

			var proxy = ProbeNumberFormatHost.ProbeNumberFormatDto.ToReadOnly(dto).ToMutable();
			proxy.AccountId = 12345678901234567;
			proxy.Plain = 42;

			var obj = proxy.ToJsonValue().AsObject();
			Assert.That(obj["AccountId"], Is.InstanceOf<JsonString>(), "the proxy setter honors the attribute, like the other write routes");
			Assert.That(obj["Plain"], Is.InstanceOf<JsonNumber>());
		}

		[Test]
		public void Test_Generated_Reads_Accept_Both_Forms()
		{
			var fromString = ProbeNumberFormatHost.ProbeNumberFormatDto.Deserialize("""{ "AccountId": "12345678901234567", "Balance": "12.50", "Plain": 42 }""");
			Assert.That(fromString.AccountId, Is.EqualTo(12345678901234567));
			Assert.That(fromString.Balance, Is.EqualTo(12.50m));

			var fromNumber = ProbeNumberFormatHost.ProbeNumberFormatDto.Deserialize("""{ "AccountId": 12345678901234567, "Balance": 12.50, "Plain": 42 }""");
			Assert.That(fromNumber.AccountId, Is.EqualTo(12345678901234567), "the numeric form keeps binding: producers move independently");
			Assert.That(fromNumber.Balance, Is.EqualTo(12.50m));
		}

	}

}
