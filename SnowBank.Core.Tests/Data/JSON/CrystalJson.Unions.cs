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

// C# 15 union types need the runtime markers (System.Runtime.CompilerServices.UnionAttribute / IUnion),
// which ship in the BCL only on net11.0 and later. Guard the whole fixture to that target.
#if NET11_0_OR_GREATER

namespace SnowBank.Data.Json.Tests
{

	/// <summary>A .NET 11 keyword union of an <see cref="int"/> case and a <see cref="string"/> case.</summary>
	/// <remarks>The compiler lowers this to a sealed struct marked <c>[Union]</c> that implements <c>IUnion</c>; the case types are the constructor parameter types.</remarks>
	public union IntOrString(int, string);

	public sealed record UnionCat(string Name);

	public sealed record UnionDog(int Legs);

	/// <summary>A union whose cases are both object types.</summary>
	public union CatOrDog(UnionCat, UnionDog);

	/// <summary>An object that carries a union as one of its members.</summary>
	public sealed record ChoiceHolder(IntOrString Choice);

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_Serialize_Union_Writes_Bare_Active_Case()
		{
			// A union serializes the active case value directly, untagged: no envelope, no "$type" (this matches System.Text.Json).
			Assert.That(CrystalJson.Serialize(new IntOrString(42)), Is.EqualTo("42"));
			Assert.That(CrystalJson.Serialize(new IntOrString("hello")), Is.EqualTo("\"hello\""));

			// The none state (Value == null) writes JSON null.
			Assert.That(CrystalJson.Serialize(default(IntOrString)), Is.EqualTo("null"));
		}

		[Test]
		public void Test_Pack_Union_To_JsonValue_Writes_Bare_Active_Case()
		{
			// Packing to the JsonValue DOM produces the same untagged shape as text serialization.
			Assert.That(JsonValue.FromValue(new IntOrString(42)).ToJsonText(), Is.EqualTo("42"));
			Assert.That(JsonValue.FromValue(new IntOrString("hello")).ToJsonText(), Is.EqualTo("\"hello\""));

			// The none state (Value == null) packs to JSON null.
			Assert.That(JsonValue.FromValue(default(IntOrString)).ToJsonText(), Is.EqualTo("null"));
		}

		[Test]
		public void Test_Serialize_Union_With_Object_Case_Adds_No_Envelope()
		{
			var cat = new UnionCat("Felix");

			// the union writes exactly what the bare case value writes: no "$type", no wrapping object
			Assert.That(CrystalJson.Serialize(new CatOrDog(cat)), Is.EqualTo(CrystalJson.Serialize(cat)));
			Assert.That(CrystalJson.Serialize(new CatOrDog(cat)), Does.Not.Contain("$type"));
		}

		[Test]
		public void Test_Serialize_Union_As_Member_Writes_Bare_Active_Case()
		{
			// a union member serializes as its active case, so the "Choice" field holds 42, not a nested { "Value": 42 }
			Assert.That(CrystalJson.Serialize(new ChoiceHolder(new IntOrString(42)), CrystalJsonSettings.JsonCompact), Is.EqualTo("{\"Choice\":42}"));
		}

	}

}

#endif
