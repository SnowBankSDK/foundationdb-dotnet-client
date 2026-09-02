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

// Union types need the net11 runtime markers; the source generator handling is exercised by building this
// with the .NET 11 SDK compiler (the only Roslyn that parses the `union` keyword).
#if NET11_0_OR_GREATER

namespace SnowBank.Data.Json.Tests
{

	/// <summary>Keyword union with scalar cases, enrolled in a generated converter container below.</summary>
	public union GenScalarUnion(int, string);

	public sealed record GenCat { public string Name { get; init; } = ""; }

	public sealed record GenDog { public int Legs { get; init; } }

	/// <summary>Keyword union whose cases are object types (they need their own generated converters).</summary>
	public union GenCatOrDog(GenCat, GenDog);

	/// <summary>Hand-written union with the non-boxing access pattern (per-case <c>TryGetValue</c>), detected by the <c>[Union]</c> attribute.</summary>
	[System.Runtime.CompilerServices.Union]
	public readonly struct GenMoney : System.Runtime.CompilerServices.IUnion
	{
		private readonly int Tag { get; }
		private readonly long Cents { get; }
		private readonly string? Note { get; }

		public GenMoney(long cents) { this.Tag = 1; this.Cents = cents; this.Note = null; }
		public GenMoney(string note) { this.Tag = 2; this.Cents = 0; this.Note = note; }

		public object? Value => this.Tag switch { 1 => this.Cents, 2 => this.Note, _ => null };
		public bool HasValue => this.Tag != 0;
		public bool TryGetValue(out long value) { value = this.Cents; return this.Tag == 1; }
		public bool TryGetValue(out string value) { value = this.Note!; return this.Tag == 2; }
	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(GenScalarUnion))]
	[CrystalSerializable(typeof(GenCatOrDog))]
	[CrystalSerializable(typeof(GenMoney))]
	public static partial class UnionGenConverters { }

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_Generated_Union_Serializer_Writes_Bare_Active_Case()
		{
			// the generated converter must produce the same untagged output as the runtime path
			Assert.That(UnionGenConverters.GenScalarUnion.ToJsonText(new GenScalarUnion(42)), Is.EqualTo("42"));
			Assert.That(UnionGenConverters.GenScalarUnion.ToJsonText(new GenScalarUnion("hello")), Is.EqualTo("\"hello\""));
			Assert.That(UnionGenConverters.GenScalarUnion.ToJsonText(default(GenScalarUnion)), Is.EqualTo("null"));
		}

		[Test]
		public void Test_Generated_Union_Packer_Writes_Bare_Active_Case()
		{
			Assert.That(UnionGenConverters.GenScalarUnion.Pack(new GenScalarUnion(42)).ToJsonText(), Is.EqualTo("42"));
			Assert.That(UnionGenConverters.GenScalarUnion.Pack(new GenScalarUnion("hello")).ToJsonText(), Is.EqualTo("\"hello\""));
			Assert.That(UnionGenConverters.GenScalarUnion.Pack(default(GenScalarUnion)).ToJsonText(), Is.EqualTo("null"));
		}

		[Test]
		public void Test_Generated_Union_With_Object_Case_Dispatches_To_Generated_Case_Converter()
		{
			var felix = new GenCat { Name = "Felix" };

			// the object case writes exactly what the case type's own generated converter writes: no envelope, no "$type"
			Assert.That(UnionGenConverters.GenCatOrDog.ToJsonText(new GenCatOrDog(felix)), Is.EqualTo(UnionGenConverters.GenCat.ToJsonText(felix)));
			Assert.That(UnionGenConverters.GenCatOrDog.ToJsonText(new GenCatOrDog(felix)), Does.Not.Contain("$type"));
		}

		[Test]
		public void Test_Generated_Union_Uses_NonBoxing_TryGetValue_Path()
		{
			// GenMoney provides bool TryGetValue(out TCase) for every case, so the generated dispatch avoids boxing
			Assert.That(UnionGenConverters.GenMoney.ToJsonText(new GenMoney(4200L)), Is.EqualTo("4200"));
			Assert.That(UnionGenConverters.GenMoney.ToJsonText(new GenMoney("gift")), Is.EqualTo("\"gift\""));
			Assert.That(UnionGenConverters.GenMoney.ToJsonText(default(GenMoney)), Is.EqualTo("null"));
		}

	}

}

#endif
