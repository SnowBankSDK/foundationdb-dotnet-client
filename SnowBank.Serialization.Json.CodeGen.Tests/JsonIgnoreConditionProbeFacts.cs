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
	using System.Text.Json.Serialization;

	#region Probe types...

	public sealed record ProbeIgnoreDto
	{

		[JsonIgnore]
		public string? AlwaysHidden { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public int Pinned { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? MaybeNull { get; set; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public int Count { get; set; }

		public int Plain { get; set; }

		public string? PlainRef { get; set; }

	}

	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(ProbeIgnoreDto))]
	public static partial class IgnoreProbeConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Pins the <c>[JsonIgnore(Condition = ...)]</c> semantics on the SOURCE-GENERATED path (writer AND Pack routes)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class JsonIgnoreConditionProbeFacts : SimpleTest
	{

		[Test]
		public void Test_JsonIgnoreCondition_Numeric_Values_Are_Stable()
		{
			// the generator cannot reference System.Text.Json and matches the enum by its underlying values
			using (Assert.EnterMultipleScope())
			{
				Assert.That((int) JsonIgnoreCondition.Never, Is.EqualTo(0));
				Assert.That((int) JsonIgnoreCondition.Always, Is.EqualTo(1));
				Assert.That((int) JsonIgnoreCondition.WhenWritingDefault, Is.EqualTo(2));
				Assert.That((int) JsonIgnoreCondition.WhenWritingNull, Is.EqualTo(3));
			}
		}

		[Test]
		public void Test_Conditions_On_The_Writer_Route()
		{
			var dto = new ProbeIgnoreDto { AlwaysHidden = "boo", Pinned = 0, MaybeNull = null, Count = 0, Plain = 0, PlainRef = null };

			var obj = JsonObject.Parse(IgnoreProbeConverters.ProbeIgnoreDto.ToJsonText(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("AlwaysHidden"), Is.False, "[JsonIgnore] (Always) must exclude the member");
				Assert.That(obj.Get<int>("Pinned", -1), Is.Zero, "Never must emit even a default value");
				Assert.That(obj.ContainsKey("MaybeNull"), Is.False, "WhenWritingNull must omit a null value");
				Assert.That(obj.ContainsKey("Count"), Is.False, "WhenWritingDefault must omit the default value");
				Assert.That(obj.Get<int>("Plain", -1), Is.Zero, "control: plain value-type default is emitted by default settings");
			}

			// Never wins over the global default-discard; non-default conditional values are emitted
			var json = IgnoreProbeConverters.ProbeIgnoreDto.ToJsonText(new ProbeIgnoreDto { MaybeNull = "x", Count = 5 }, CrystalJsonSettings.Json.WithoutDefaultValues());
			var obj2 = JsonObject.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj2.Get<int>("Pinned", -1), Is.Zero, "Never must override WithoutDefaultValues()");
				Assert.That(obj2.ContainsKey("Plain"), Is.False, "control: WithoutDefaultValues() omits a plain default member");
				Assert.That(obj2.Get<string>("MaybeNull"), Is.EqualTo("x"));
				Assert.That(obj2.Get<int>("Count"), Is.EqualTo(5));
			}
		}

		[Test]
		public void Test_Conditions_On_The_Pack_Route()
		{
			var dto = new ProbeIgnoreDto { AlwaysHidden = "boo", Pinned = 0, MaybeNull = null, Count = 0, Plain = 0 };

			var obj = IgnoreProbeConverters.ProbeIgnoreDto.Pack(dto).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("AlwaysHidden"), Is.False, "[JsonIgnore] (Always) must exclude the member (Pack route)");
				Assert.That(obj.Get<int>("Pinned", -1), Is.Zero, "Never must emit even a default value (Pack route)");
				Assert.That(obj.ContainsKey("MaybeNull"), Is.False, "WhenWritingNull must omit a null value (Pack route)");
				Assert.That(obj.ContainsKey("Count"), Is.False, "WhenWritingDefault must omit the default value (Pack route)");
			}
		}

		[Test]
		public void Test_Conditions_Do_Not_Affect_Reading()
		{
			var dto = IgnoreProbeConverters.ProbeIgnoreDto.Deserialize("""{ "AlwaysHidden": "boo", "Pinned": 7, "MaybeNull": "x", "Count": 5, "Plain": 1 }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.AlwaysHidden, Is.Null, "[JsonIgnore] (Always) must not bind on read");
				Assert.That(dto.Pinned, Is.EqualTo(7));
				Assert.That(dto.MaybeNull, Is.EqualTo("x"));
				Assert.That(dto.Count, Is.EqualTo(5));
				Assert.That(dto.Plain, Is.EqualTo(1));
			}
		}

	}

}
