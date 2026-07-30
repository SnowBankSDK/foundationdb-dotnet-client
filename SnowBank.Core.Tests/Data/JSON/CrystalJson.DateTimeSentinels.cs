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

	/// <summary>Pins the sentinel convention for dates: <see cref="DateTime.MinValue"/> (the default of an unset member)
	/// serializes as the empty string and round-trips on EVERY route, independently of the machine's timezone. Applying a
	/// local offset to either extreme would throw east (MinValue) or west (MaxValue) of Greenwich, the same landmine that
	/// makes the legacy DataContractJsonSerializer fail on unset dates.</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonDateTimeSentinelFacts : SimpleTest
	{

		public sealed class DateDto
		{
			public string? Name { get; set; }

			public DateTime When { get; set; }

			public DateTime? Maybe { get; set; }

			public DateTimeOffset Offset { get; set; }
		}

		[Test]
		public void Test_Default_Dates_Roundtrip_On_Text_Route()
		{
			var dto = new DateDto { Name = "x" }; // all dates left at their default (== MinValue)

			string json = CrystalJson.Serialize(dto);
			Assert.That(json, Is.EqualTo("""{ "Name": "x", "When": "", "Offset": "" }"""), "unset dates serialize as the empty string");

			var back = CrystalJson.Deserialize<DateDto>(json);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.When, Is.EqualTo(DateTime.MinValue));
				Assert.That(back.Maybe, Is.Null);
				Assert.That(back.Offset, Is.EqualTo(DateTimeOffset.MinValue));
			}
		}

		[Test]
		public void Test_Default_Dates_Roundtrip_On_Dom_Route()
		{
			var dto = new DateDto { Name = "x" };

			var packed = JsonValue.FromValue(dto);
			var back = packed.As<DateDto>()!;
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.When, Is.EqualTo(DateTime.MinValue));
				Assert.That(back.Maybe, Is.Null);
				Assert.That(back.Offset, Is.EqualTo(DateTimeOffset.MinValue));
			}
		}

		[Test]
		public void Test_JsonDateTime_Extremes_Convert_Without_Offset_Arithmetic()
		{
			// the sentinel values must never have the machine's local offset applied to them
			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonDateTime.Return(DateTime.MinValue).ToDateTimeOffset(), Is.EqualTo(DateTimeOffset.MinValue), "MinValue must not shift (throws east of Greenwich otherwise)");
				Assert.That(JsonDateTime.Return(DateTime.MaxValue).ToDateTimeOffset(), Is.EqualTo(DateTimeOffset.MaxValue), "MaxValue must not shift (throws west of Greenwich otherwise)");
				Assert.That(JsonDateTime.Return(DateTime.MinValue).ToDateTime(), Is.EqualTo(DateTime.MinValue));
				Assert.That(JsonDateTime.Return(DateTime.MaxValue).ToDateTime(), Is.EqualTo(DateTime.MaxValue));
			}
		}

	}

}
