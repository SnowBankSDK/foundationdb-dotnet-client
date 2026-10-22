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

	public sealed record GenDictInner
	{
		public required string Id { get; init; }

		public required string Name { get; init; }
	}

	public sealed record GenDictOuter
	{
		public required string Id { get; init; }

		public required Dictionary<string, GenDictInner> Items { get; init; }

		public Dictionary<string, GenDictInner>? Extra { get; init; }

		public Dictionary<int, GenDictInner>? ByRank { get; init; }
	}

	[CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)]
	[CrystalSerializable(typeof(GenDictOuter))]
	[CrystalSerializable(typeof(GenDictInner))]
	public static partial class GenDictConverters { }

	/// <summary>A generated container reads back the dictionaries of generated types that it wrote, in both dictionary shapes</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonGeneratedDictionaryFacts : SimpleTest
	{

		private static GenDictOuter MakeOuter() => new()
		{
			Id = "o",
			Items = new() { ["a"] = new() { Id = "a", Name = "x" }, ["b"] = new() { Id = "b", Name = "y" } },
			Extra = new() { ["c"] = new() { Id = "c", Name = "z" } },
			ByRank = new() { [1] = new() { Id = "r1", Name = "first" }, [-2] = new() { Id = "r2", Name = "second" } },
		};

		private static void AssertSameDictionaries(GenDictOuter decoded, GenDictOuter expected)
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(decoded.Items, Is.EquivalentTo(expected.Items));
				Assert.That(decoded.Extra, Is.EquivalentTo(expected.Extra!));
				Assert.That(decoded.ByRank, Is.EquivalentTo(expected.ByRank!));
			}
		}

		[Test]
		public void Test_Pack_Then_Unpack_Restores_Dictionaries_Of_Generated_Types()
		{
			var outer = MakeOuter();

			var packed = GenDictConverters.GenDictOuter.Pack(outer);
			Log($"packed: {packed.ToJsonText(CrystalJsonSettings.JsonCompact)}");

			AssertSameDictionaries(GenDictConverters.GenDictOuter.Unpack(packed), outer);
		}

		[Test]
		public void Test_Serialize_Then_Unpack_Restores_Dictionaries_Of_Generated_Types()
		{
			var outer = MakeOuter();

			var json = GenDictConverters.GenDictOuter.ToJsonText(outer);
			Log($"json: {json}");

			AssertSameDictionaries(GenDictConverters.GenDictOuter.Unpack(JsonValue.Parse(json)), outer);
		}

		[Test]
		public void Test_Legacy_Pair_Arrays_Written_By_The_Container_Unpack()
		{
			var outer = MakeOuter();

			var json = GenDictConverters.GenDictOuter.ToJsonText(outer, CrystalJsonSettings.Json.WithDictionariesAsPairArrays());
			Log($"json: {json}");
			Assume.That(json, Does.Contain("\"key\":").Or.Contain("\"Key\":"), "the dictionaries must be written as key/value pairs for this test");

			AssertSameDictionaries(GenDictConverters.GenDictOuter.Unpack(JsonValue.Parse(json)), outer);
		}

		[Test]
		public void Test_Legacy_Pair_Arrays_Unpack_With_Either_Casing()
		{
			var outer = MakeOuter();
			var json = """
				{
					"id": "o",
					"items": [ { "Key": "a", "Value": { "id": "a", "name": "x" } }, { "key": "b", "value": { "id": "b", "name": "y" } } ],
					"extra": [ { "Key": "c", "Value": { "id": "c", "name": "z" } } ],
					"byRank": [ { "Key": 1, "Value": { "id": "r1", "name": "first" } }, { "key": -2, "value": { "id": "r2", "name": "second" } } ]
				}
				""";

			AssertSameDictionaries(GenDictConverters.GenDictOuter.Unpack(JsonValue.Parse(json)), outer);
		}

		[Test]
		public void Test_Missing_Optional_Dictionaries_Unpack_As_Null()
		{
			var outer = MakeOuter() with { Extra = null, ByRank = null };

			var decoded = GenDictConverters.GenDictOuter.Unpack(GenDictConverters.GenDictOuter.Pack(outer));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(decoded.Extra, Is.Null);
				Assert.That(decoded.ByRank, Is.Null);
				Assert.That(decoded.Items, Is.EquivalentTo(outer.Items));
			}
		}

	}

}
