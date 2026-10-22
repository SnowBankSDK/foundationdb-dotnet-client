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

	public sealed record ReqNullableHolder
	{
		public required string Id { get; init; }

		public required string? Note { get; init; }

		public required int? Count { get; init; }
	}

	[CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)]
	[CrystalSerializable(typeof(ReqNullableHolder))]
	public static partial class ReqNullableConverters { }

	/// <summary>A missing field and an explicit null mean the same thing: no value</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonRequiredNullableFacts : SimpleTest
	{

		[Test]
		public void Test_Generated_Round_Trip_Of_Required_Nullable_Members_Holding_Null()
		{
			var value = new ReqNullableHolder { Id = "a", Note = null, Count = null };

			var packed = ReqNullableConverters.ReqNullableHolder.Pack(value);
			Log($"packed: {packed.ToJsonText(CrystalJsonSettings.JsonCompact)}");
			var decoded = ReqNullableConverters.ReqNullableHolder.Unpack(packed);

			Assert.That(decoded, Is.EqualTo(value));
		}

		[Test]
		public void Test_Generated_Reads_Missing_And_Null_Alike([Values("{ \"id\": \"a\" }", "{ \"id\": \"a\", \"note\": null, \"count\": null }")] string json)
		{
			var decoded = ReqNullableConverters.ReqNullableHolder.Unpack(JsonValue.Parse(json));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(decoded.Note, Is.Null);
				Assert.That(decoded.Count, Is.Null);
			}
		}

		[Test]
		public void Test_Generated_Missing_Non_Nullable_Required_Member_Throws([Values("{ }", "{ \"id\": null }")] string json)
		{
			Assert.That(() => ReqNullableConverters.ReqNullableHolder.Unpack(JsonValue.Parse(json)), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_Reflection_Reads_Missing_And_Null_Alike([Values("{ \"Id\": \"a\" }", "{ \"Id\": \"a\", \"Note\": null, \"Count\": null }")] string json)
		{
			var decoded = CrystalJson.Deserialize<ReqNullableHolder>(json);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(decoded.Note, Is.Null);
				Assert.That(decoded.Count, Is.Null);
			}
		}

		[Test]
		public void Test_Reflection_Missing_Non_Nullable_Required_Member_Throws([Values("{ }", "{ \"Id\": null }")] string json)
		{
			Assert.That(() => CrystalJson.Deserialize<ReqNullableHolder>(json), Throws.InstanceOf<JsonBindingException>());
		}

	}

}
