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

	/// <summary>Pins <c>[JsonProperty(NumberFormat = ...)]</c>: the per-member string form for numeric members, with the always-tolerant read</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonNumberFormatFacts : SimpleTest
	{

		public sealed class AccountDto
		{

			[JsonProperty(NumberFormat = JsonNumberFormat.String)]
			public long AccountId { get; set; }

			[JsonProperty(NumberFormat = JsonNumberFormat.String)]
			public decimal Balance { get; set; }

			public long Plain { get; set; }

		}

		[Test]
		public void Test_NumberFormat_String_Forces_The_String_Form_On_Serialize()
		{
			var dto = new AccountDto { AccountId = 12345678901234567, Balance = 12.50m, Plain = 42 };

			var obj = JsonObject.Parse(CrystalJson.Serialize(dto));
			Log(obj.ToJsonText());

			Assert.That(obj["AccountId"], Is.InstanceOf<JsonString>(), "[JsonProperty(NumberFormat=String)] forces the string form for this member");
			Assert.That(obj.Get<string>("AccountId"), Is.EqualTo("12345678901234567"));
			Assert.That(obj["Balance"], Is.InstanceOf<JsonString>(), "decimal members honor the attribute too");
			Assert.That(obj.Get<string>("Balance"), Is.EqualTo("12.50"), "the string is the numeric literal, decimal scale included");
			Assert.That(obj["Plain"], Is.InstanceOf<JsonNumber>(), "a member without the attribute keeps the numeric form");
		}

		[Test]
		public void Test_NumberFormat_String_Forces_The_String_Form_On_The_Dom_Route()
		{
			var dto = new AccountDto { AccountId = 12345678901234567, Plain = 42 };

			var obj = JsonValue.FromValue(dto).AsObject();
			Log(obj.ToJsonText());

			Assert.That(obj["AccountId"], Is.InstanceOf<JsonString>(), "the DOM route honors the per-member NumberFormat, same as the text route");
			Assert.That(obj["Plain"], Is.InstanceOf<JsonNumber>());
		}

		[Test]
		public void Test_Reads_Accept_Both_Forms_Regardless_Of_The_Attribute()
		{
			var fromString = CrystalJson.Deserialize<AccountDto>("""{ "AccountId": "12345678901234567", "Balance": "12.5", "Plain": 42 }""");
			Assert.That(fromString.AccountId, Is.EqualTo(12345678901234567));
			Assert.That(fromString.Balance, Is.EqualTo(12.5m));

			var fromNumber = CrystalJson.Deserialize<AccountDto>("""{ "AccountId": 12345678901234567, "Balance": 12.5, "Plain": 42 }""");
			Assert.That(fromNumber.AccountId, Is.EqualTo(12345678901234567), "the numeric form keeps binding: producers move independently");
			Assert.That(fromNumber.Balance, Is.EqualTo(12.5m));
		}

		[Test]
		public void Test_Round_Trip_Preserves_The_Value()
		{
			var dto = new AccountDto { AccountId = long.MaxValue, Balance = 0.001m, Plain = -1 };

			var back = CrystalJson.Deserialize<AccountDto>(CrystalJson.Serialize(dto));

			Assert.That(back.AccountId, Is.EqualTo(long.MaxValue));
			Assert.That(back.Balance, Is.EqualTo(0.001m));
			Assert.That(back.Plain, Is.EqualTo(-1));
		}

	}

}
