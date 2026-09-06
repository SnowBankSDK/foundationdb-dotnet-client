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
	using System.Text.Json.Serialization;
	using SnowBank.Data;

	/// <summary>A positional record: its primary constructor is the only way to build it.</summary>
	public sealed record CtorToy(string Name, int Size = 3);

	/// <summary>A class with one parameterized constructor, a get-only member bound through it, and a settable member that is not.</summary>
	public sealed class CtorBowl
	{
		public CtorBowl(string material) { this.Material = material; }
		public string Material { get; }
		public int Capacity { get; set; }
	}

	/// <summary>Two public constructors; the marked one is used.</summary>
	public sealed class CtorLeash
	{
		public CtorLeash(int length) { this.Length = length; }

		[JsonConstructor]
		public CtorLeash(int length, string color) { this.Length = length; this.Color = color; }

		public int Length { get; }
		public string? Color { get; }
	}

	public sealed record CtorOwner(string Name, CtorToy Favorite);

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(CtorToy))]
	[CrystalSerializable(typeof(CtorBowl))]
	[CrystalSerializable(typeof(CtorLeash))]
	[CrystalSerializable(typeof(CtorOwner))]
	public static partial class CtorConverters { }

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_Deserialize_Positional_Record_Through_Its_Constructor()
		{
			var toy = CrystalJson.Deserialize<CtorToy>("""{ "Name": "ball", "Size": 5 }""");
			Assert.That(toy, Is.EqualTo(new CtorToy("ball", 5)));

			// an absent optional parameter takes the parameter's default value, not default(int)
			toy = CrystalJson.Deserialize<CtorToy>("""{ "Name": "ball" }""");
			Assert.That(toy, Is.EqualTo(new CtorToy("ball", 3)));
		}

		[Test]
		public void Test_Deserialize_Class_Through_Its_Single_Constructor()
		{
			var bowl = CrystalJson.Deserialize<CtorBowl>("""{ "Material": "steel", "Capacity": 2 }""");
			Assert.That(bowl.Material, Is.EqualTo("steel"), "bound through the constructor");
			Assert.That(bowl.Capacity, Is.EqualTo(2), "assigned after construction");
		}

		[Test]
		public void Test_Deserialize_Through_The_Marked_Constructor()
		{
			var leash = CrystalJson.Deserialize<CtorLeash>("""{ "Length": 120, "Color": "red" }""");
			Assert.That(leash.Length, Is.EqualTo(120));
			Assert.That(leash.Color, Is.EqualTo("red"), "the [JsonConstructor] one takes the color; the other would have dropped it");
		}

		[Test]
		public void Test_Positional_Record_Round_Trips_On_Both_Paths()
		{
			var owner = new CtorOwner("Alice", new CtorToy("ball", 5));
			var text = CrystalJson.Serialize(owner, CrystalJsonSettings.JsonCompact);
			Assert.That(text, Is.EqualTo("""{"Name":"Alice","Favorite":{"Name":"ball","Size":5}}"""));

			Assert.That(CrystalJson.Deserialize<CtorOwner>(text), Is.EqualTo(owner), "reflection path");
			Assert.That(CtorConverters.CtorOwner.Unpack(JsonObject.Parse(text)), Is.EqualTo(owner), "generated converter");
			Assert.That(CtorConverters.CtorOwner.ToJsonText(owner, CrystalJsonSettings.JsonCompact), Is.EqualTo(text));
		}

		[Test]
		public void Test_Generated_Converter_Applies_The_Parameter_Default()
		{
			Assert.That(CtorConverters.CtorToy.Unpack(JsonObject.Parse("""{ "Name": "ball" }""")), Is.EqualTo(new CtorToy("ball", 3)));
			var bowl = CtorConverters.CtorBowl.Unpack(JsonObject.Parse("""{ "Material": "steel", "Capacity": 2 }"""));
			Assert.That((bowl.Material, bowl.Capacity), Is.EqualTo(("steel", 2)));
			var leash = CtorConverters.CtorLeash.Unpack(JsonObject.Parse("""{ "Length": 120, "Color": "red" }"""));
			Assert.That((leash.Length, leash.Color), Is.EqualTo((120, "red")));
		}

	}

}
