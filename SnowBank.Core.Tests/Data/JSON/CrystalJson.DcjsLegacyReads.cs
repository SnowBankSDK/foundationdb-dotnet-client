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
	using System.Runtime.Serialization.Json;

	/// <summary>The two shapes that only <see cref="DataContractJsonSerializer"/> writes, and that the reflection path must keep reading:
	/// a <see cref="DateTimeOffset"/> written as an object, and a dictionary written as an array of key/value pairs.</summary>
	/// <remarks>
	/// <para>These are read-side rules. Documents already at rest must stay readable, and nothing here changes what CrystalJson writes.</para>
	/// <para>Each output below comes from the real DCJS, in-process, so the fixture cannot pin a shape the legacy serializer never wrote.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonDcjsLegacyReadsFacts : SimpleTest
	{

		#region DCJS oracle helpers...

		private static string DcjsSerialize<T>(T dto)
		{
			var serializer = new DataContractJsonSerializer(typeof(T));
			using var ms = new MemoryStream();
			serializer.WriteObject(ms, dto);
			return System.Text.Encoding.UTF8.GetString(ms.ToArray());
		}

		private static T DcjsDeserialize<T>(string json)
		{
			var serializer = new DataContractJsonSerializer(typeof(T));
			using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
			return (T) serializer.ReadObject(ms)!;
		}

		#endregion

		#region Test DTOs...

		[DataContract]
		public sealed class DcjsOffsetHolder
		{
			[DataMember] public DateTimeOffset Stamp { get; set; }
		}

		[DataContract]
		public sealed class DcjsOffsetNullableHolder
		{
			[DataMember] public DateTimeOffset? Stamp { get; set; }
		}

		#endregion

		#region The DateTimeOffset object shape...

		[Test]
		public void Test_DateTimeOffset_Reads_The_Dcjs_Object_Shape()
		{
			var stamp = new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.FromHours(2));

			// DCJS writes a DateTimeOffset as an object of two members. The inner date is the instant, normalized to UTC,
			// and the offset travels beside it, in minutes.
			var output = DcjsSerialize(new DcjsOffsetHolder { Stamp = stamp });
			Log($"dcj: {output}");
			Assert.That(output, Is.EqualTo("""{"Stamp":{"DateTime":"\/Date(1234560690000)\/","OffsetMinutes":120}}"""));
			Assert.That(DcjsDeserialize<DcjsOffsetHolder>(output).Stamp, Is.EqualTo(stamp), "the oracle reads its own output");

			// the DTO member path. Without the dedicated conversion the value reaches the member binder, which assigns
			// nothing and returns default(DateTimeOffset) with no error.
			var back = CrystalJson.Deserialize<DcjsOffsetHolder>(output);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Stamp, Is.EqualTo(stamp));
				Assert.That(back.Stamp.Offset, Is.EqualTo(TimeSpan.FromHours(2)), "the offset must survive, not just the instant");
			}

			// the nullable member
			var nullableOutput = DcjsSerialize(new DcjsOffsetNullableHolder { Stamp = stamp });
			var nullableBack = CrystalJson.Deserialize<DcjsOffsetNullableHolder>(nullableOutput);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(nullableBack.Stamp, Is.EqualTo(stamp));
				Assert.That(nullableBack.Stamp!.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
			}
			Assert.That(CrystalJson.Deserialize<DcjsOffsetNullableHolder>("""{"Stamp":null}""").Stamp, Is.Null, "an explicit null stays null");

			// the direct DOM conversion
			const string INNER = """{"DateTime":"\/Date(1234560690000)\/","OffsetMinutes":120}""";
			using (Assert.EnterMultipleScope())
			{
				Assert.That(CrystalJson.Deserialize<DateTimeOffset>(INNER), Is.EqualTo(stamp));
				Assert.That(CrystalJson.Parse(INNER).ToDateTimeOffset(), Is.EqualTo(stamp));
				Assert.That(CrystalJson.Parse(INNER).As<DateTimeOffset>(), Is.EqualTo(stamp));
			}

			// and collections of them
			Assert.That(
				CrystalJson.Deserialize<List<DateTimeOffset>>($"[{INNER}]"),
				Is.EqualTo(new List<DateTimeOffset> { stamp }));
		}

		[Test]
		public void Test_DateTimeOffset_Dcjs_Object_Shape_Corner_Cases()
		{
			using (Assert.EnterMultipleScope())
			{
				// a zero offset is a plain UTC instant
				Assert.That(
					CrystalJson.Deserialize<DateTimeOffset>("""{"DateTime":"\/Date(1234567890000)\/","OffsetMinutes":0}"""),
					Is.EqualTo(new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero)));

				// negative offsets are spelled with a negative OffsetMinutes
				Assert.That(
					CrystalJson.Deserialize<DateTimeOffset>("""{"DateTime":"\/Date(1234567890000)\/","OffsetMinutes":-330}"""),
					Is.EqualTo(new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(-330))));

				// an object of another shape is rejected
				Assert.That(() => CrystalJson.Deserialize<DateTimeOffset>("""{"Hello":"World"}"""), Throws.InstanceOf<JsonBindingException>());

				// so is an object that has the two members plus a third one
				Assert.That(
					() => CrystalJson.Deserialize<DateTimeOffset>("""{"DateTime":"\/Date(1234567890000)\/","OffsetMinutes":0,"Extra":1}"""),
					Throws.InstanceOf<JsonBindingException>());
			}
		}

		#endregion

		#region The legacy key/value-pair array...

		[Test]
		public void Test_Legacy_Pair_Array_Accepts_Both_Spellings_And_Extra_Members()
		{
			using (Assert.EnterMultipleScope())
			{
				// DCJS writes a standalone pair in lowercase, so a dictionary that went through a KeyValuePair contract comes
				// back lowercase. Both spellings are the same output.
				Assert.That(
					CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "key": "a", "value": 1 } ]"""),
					Is.EqualTo(new Dictionary<string, int> { ["a"] = 1 }));

				// mixed spellings within the same array
				Assert.That(
					CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a", "Value": 1 }, { "key": "b", "value": 2 } ]"""),
					Is.EqualTo(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }));

				// extra members are ignored, as they are on any other object. A "__type" member is the one legacy documents carry.
				Assert.That(
					CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "__type": "KeyValuePairOfstringint:#System.Collections.Generic", "Key": "a", "Value": 1 } ]"""),
					Is.EqualTo(new Dictionary<string, int> { ["a"] = 1 }));

				// non-string keys keep working through both spellings
				Assert.That(
					CrystalJson.Deserialize<Dictionary<int, string>>("""[ { "key": 1, "value": "one" } ]"""),
					Is.EqualTo(new Dictionary<int, string> { [1] = "one" }));
			}
		}

		[Test]
		public void Test_Legacy_Pair_Array_Still_Rejects_Unrecognizable_Elements()
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Nope": "a", "Value": 1 } ]"""),
					Throws.InstanceOf<JsonBindingException>(), "an object with no recognizable key member must fail");
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ 1, 2 ]"""),
					Throws.InstanceOf<JsonBindingException>(), "non-object elements must fail");
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a", "Value": 1 }, [ "b", 2 ] ]"""),
					Throws.InstanceOf<JsonBindingException>(), "one non-conforming element must fail the whole bind");
			}
		}

		#endregion

	}

}
