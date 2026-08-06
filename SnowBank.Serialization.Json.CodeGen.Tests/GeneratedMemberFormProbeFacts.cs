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

	/// <summary>Generic container with a member on an UNCONSTRAINED type parameter, declared as <c>T?</c>.</summary>
	/// <remarks>
	/// On an unconstrained <typeparamref name="T"/>, <c>T?</c> is "defaultable T" (the nullable annotation), NOT <c>Nullable&lt;T&gt;</c>.
	/// When the container is instantiated with a value type (e.g. <see cref="int"/>), the substituted member type is plain <c>int</c>,
	/// and the generated converter must treat it as a plain value-type member, not as <c>Nullable&lt;int&gt;</c>.
	/// </remarks>
	public sealed class GenericBox<T>
	{

		public T? Payload { get; init; }

	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(GenericBox<int>))]
	[CrystalSerializable(typeof(GenericBox<string>))]
	public static partial class GenericNullableProbeConverters
	{
		// generated code goes here!
	}

	/// <summary>POCO with a get-only (read-only) computed property, as found in a DataContract-era DTO</summary>
	public sealed record ProbeReadOnlyDto
	{

		public string? Name { get; set; }

		public int Count { get; set; }

		// get-only computed property: DataContract POCO semantics are "serialization-only" (written out, never assigned back).
		// The generated deserializer must NOT emit an object-initializer assignment to this member (that would be CS0200).
		public string ReadOnlyIgnored => $"{this.Name}#{this.Count}";

	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProbeReadOnlyDto))]
	public static partial class ReadOnlyProbeConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Pins that the generated converters handle awkward member declarations: a nullable member on an unconstrained generic instantiated with a value type, and a get-only (read-only) member.</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class GeneratedMemberFormProbeFacts : SimpleTest
	{

		[Test]
		public void Test_Generic_Nullable_Member_On_ValueType_Instantiation()
		{
			// T? on an unconstrained T instantiated with `int` is a plain `int` member (not Nullable<int>):
			// the generated converter must compile and round-trip the value.

			var box = new GenericBox<int> { Payload = 42 };

			var json = GenericNullableProbeConverters.GenericBox_Int32.ToJsonText(box);
			Log(json);

			var obj = JsonObject.Parse(json).AsObject();
			Assert.That(obj.Get<int>("Payload", -1), Is.EqualTo(42));

			var decoded = GenericNullableProbeConverters.GenericBox_Int32.Deserialize(json);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Payload, Is.EqualTo(42));
		}

		[Test]
		public void Test_Generic_Nullable_Member_On_ReferenceType_Instantiation()
		{
			// the reference-type instantiation (T = string) is the case that already worked

			var box = new GenericBox<string> { Payload = "hello" };

			var json = GenericNullableProbeConverters.GenericBox_String.ToJsonText(box);
			Log(json);

			var obj = JsonObject.Parse(json).AsObject();
			Assert.That(obj.Get<string>("Payload"), Is.EqualTo("hello"));

			var decoded = GenericNullableProbeConverters.GenericBox_String.Deserialize(json);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Payload, Is.EqualTo("hello"));
		}

		[Test]
		public void Test_ReadOnly_Member_Is_Serialized_But_Not_Deserialized()
		{
			var dto = new ProbeReadOnlyDto { Name = "abc", Count = 3 };

			// the read-only computed member IS written out (serialization-only)
			var obj = JsonObject.Parse(ReadOnlyProbeConverters.ProbeReadOnlyDto.ToJsonText(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Name"), Is.EqualTo("abc"));
				Assert.That(obj.Get<int>("Count", -1), Is.EqualTo(3));
				Assert.That(obj.Get<string>("ReadOnlyIgnored"), Is.EqualTo("abc#3"), "read-only members are serialized");
			}

			// deserializing does NOT assign the get-only member (it is recomputed from the other members)
			var back = ReadOnlyProbeConverters.ProbeReadOnlyDto.Deserialize("""{ "Name": "xyz", "Count": 7, "ReadOnlyIgnored": "ignored-on-read" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Name, Is.EqualTo("xyz"));
				Assert.That(back.Count, Is.EqualTo(7));
				Assert.That(back.ReadOnlyIgnored, Is.EqualTo("xyz#7"), "the get-only member is computed, never bound from the JSON");
			}
		}

	}

}
