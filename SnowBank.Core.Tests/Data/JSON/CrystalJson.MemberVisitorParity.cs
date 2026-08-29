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
	using SnowBank.Data;
	using STJ = System.Text.Json.Serialization;

	/// <summary>A member converter that renders a bool as the "1"/"0" string form (legacy format)</summary>
	public sealed class ParityBitStringConverter : IJsonMemberConverter<bool>
	{

		public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
			=> JsonString.Return(instance ? "1" : "0");

		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value switch
			{
				JsonBoolean b => b.ToBoolean(),
				JsonString s => s.Value is "1" or "true" or "True",
				_ => throw new JsonBindingException($"Cannot convert {value.Type} into a bit-string boolean")
			};

	}

	/// <summary>A nested, locally generated type (case c): a source-generated member reached inside another generated type</summary>
	public sealed record ParityGadget
	{
		public int N { get; set; }
	}

	/// <summary>The source-generated type under test, carrying the three member kinds the oracle covers</summary>
	public sealed record ParityWidget
	{

		// (a) a plain member
		public string? Code { get; set; }

		// (b) a member carrying a value-transforming member converter (bool -> "1"/"0"), which must be honored on the reflection path too
		[STJ.JsonConverter(typeof(ParityBitStringConverter))]
		public bool Flag { get; set; }

		// (c) a nested locally generated type member
		public ParityGadget? Inner { get; set; }

	}

	/// <summary>Container registering <see cref="ParityWidget"/> (and its nested <see cref="ParityGadget"/>)</summary>
	[CrystalConverter]
	[CrystalJsonOutput]
	[CrystalSerializable(typeof(ParityWidget))]
	[CrystalSerializable(typeof(ParityGadget))]
	public static partial class ParityWidgetHost { }

	/// <summary>A plain (NON-generated) envelope, whose <c>object</c>-typed member reaches a source-generated value through the reflection member-walk</summary>
	public sealed class ParityEnvelope
	{
		public object? Payload { get; set; }
	}

	/// <summary>Pins that the reflection member-walk of a source-generated type (reached through an <c>object</c>-typed member slot with a chained resolver) produces BYTE-IDENTICAL output to the typed entry point, honoring member converters and nested generated types</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonMemberVisitorParityFacts : SimpleTest
	{

		private static ICrystalJsonTypeResolver Chain()
			=> CrystalJsonTypeResolverChain.Create([ CrystalJson.DefaultResolver, ParityWidgetHost.GetResolver() ]);

		private static ParityWidget MakeSample()
			=> new() { Code = "abc", Flag = true, Inner = new() { N = 42 } };

		[Test]
		public void Test_Reflection_Walk_Of_Generated_Member_Matches_Typed_Path()
		{
			var widget = MakeSample();
			var settings = CrystalJsonSettings.JsonCompact;
			var chain = Chain();

			// the typed entry point is the oracle
			var typed = ParityWidgetHost.ParityWidget.ToJsonText(widget, settings);
			Log($"typed:      {typed}");

			// the reflection member-walk: the generated widget is reached through an object-typed member slot
			var env = new ParityEnvelope { Payload = widget };
			var envJson = CrystalJson.Serialize(env, settings, chain);
			Log($"envelope:   {envJson}");

			var reflected = CrystalJson.Parse(envJson).AsObject()["Payload"].ToJsonText(settings);
			Log($"reflected:  {reflected}");

			Assert.That(reflected, Is.EqualTo(typed), "the reflection member-walk must byte-match the typed entry point");
		}

		[Test]
		public void Test_Reflection_Walk_Honors_Each_Member_Kind()
		{
			var widget = MakeSample();
			var settings = CrystalJsonSettings.JsonCompact;
			var chain = Chain();

			var env = new ParityEnvelope { Payload = widget };
			var payload = CrystalJson.Parse(CrystalJson.Serialize(env, settings, chain)).AsObject()["Payload"].AsObject();

			using (Assert.EnterMultipleScope())
			{
				// (a) plain member
				Assert.That(payload.Get<string>("Code"), Is.EqualTo("abc"), "plain member");
				// (b) member converter must still be honored on the reflection path (bool -> "1")
				Assert.That(payload["Flag"], Is.InstanceOf<JsonString>(), "member converter must shape the output on the reflection path");
				Assert.That(payload.Get<string>("Flag"), Is.EqualTo("1"), "member converter output");
				// (c) nested generated type
				Assert.That(payload["Inner"].AsObject().Get<int>("N"), Is.EqualTo(42), "nested generated member");
			}
		}

	}

}
