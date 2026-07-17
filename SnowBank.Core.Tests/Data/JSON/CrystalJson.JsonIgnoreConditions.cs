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
	using STJ = System.Text.Json.Serialization;

	/// <summary>Pins the System.Text.Json <c>[JsonIgnore(Condition = ...)]</c> semantics on the reflection path (writer AND DOM routes)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonIgnoreConditionFacts : SimpleTest
	{

		public sealed class IgnoreConditionDto
		{

			[STJ.JsonIgnore]
			public string? AlwaysHidden { get; set; }

			[STJ.JsonIgnore(Condition = STJ.JsonIgnoreCondition.Never)]
			public int Pinned { get; set; }

			[STJ.JsonIgnore(Condition = STJ.JsonIgnoreCondition.WhenWritingNull)]
			public string? MaybeNull { get; set; }

			[STJ.JsonIgnore(Condition = STJ.JsonIgnoreCondition.WhenWritingDefault)]
			public int Count { get; set; }

			public int Plain { get; set; }

			public string? PlainRef { get; set; }

		}

		[Test]
		public void Test_Conditions_With_Default_Settings()
		{
			var dto = new IgnoreConditionDto { AlwaysHidden = "boo", Pinned = 0, MaybeNull = null, Count = 0, Plain = 0, PlainRef = null };

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("AlwaysHidden"), Is.False, "[JsonIgnore] (Always) must exclude the member");
				Assert.That(obj.Get<int>("Pinned", -1), Is.Zero, "Never must emit even a default value");
				Assert.That(obj.ContainsKey("MaybeNull"), Is.False, "WhenWritingNull must omit a null value");
				Assert.That(obj.ContainsKey("Count"), Is.False, "WhenWritingDefault must omit the default value");
				Assert.That(obj.Get<int>("Plain", -1), Is.Zero, "control: plain value-type default is emitted by default settings");
			}

			// non-default values are emitted normally
			var obj2 = CrystalJson.Parse(CrystalJson.Serialize(new IgnoreConditionDto { MaybeNull = "x", Count = 5 })).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj2.Get<string>("MaybeNull"), Is.EqualTo("x"));
				Assert.That(obj2.Get<int>("Count"), Is.EqualTo(5));
			}
		}

		[Test]
		public void Test_Never_Wins_Over_Global_Discards()
		{
			var dto = new IgnoreConditionDto { Pinned = 0, Plain = 0 };

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto, CrystalJsonSettings.Json.WithoutDefaultValues())).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<int>("Pinned", -1), Is.Zero, "Never must override WithoutDefaultValues()");
				Assert.That(obj.ContainsKey("Plain"), Is.False, "control: WithoutDefaultValues() omits a plain default member");
			}
		}

		[Test]
		public void Test_WhenWritingNull_Wins_Over_WithNullMembers()
		{
			var dto = new IgnoreConditionDto { MaybeNull = null, PlainRef = null };

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto, CrystalJsonSettings.Json.WithNullMembers())).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("MaybeNull"), Is.False, "WhenWritingNull must override WithNullMembers()");
				Assert.That(obj.ContainsKey("PlainRef"), Is.True, "control: WithNullMembers() emits a plain null member");
			}
		}

		[Test]
		public void Test_Conditions_Apply_To_The_Dom_Route()
		{
			// JsonValue.FromValue goes through CrystalJsonDomWriter, not the text writer: same semantics expected
			var dto = new IgnoreConditionDto { AlwaysHidden = "boo", Pinned = 0, MaybeNull = null, Count = 0, Plain = 0 };

			var obj = JsonValue.FromValue(dto).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("AlwaysHidden"), Is.False, "[JsonIgnore] (Always) must exclude the member (DOM route)");
				Assert.That(obj.Get<int>("Pinned", -1), Is.Zero, "Never must emit even a default value (DOM route)");
				Assert.That(obj.ContainsKey("MaybeNull"), Is.False, "WhenWritingNull must omit a null value (DOM route)");
				Assert.That(obj.ContainsKey("Count"), Is.False, "WhenWritingDefault must omit the default value (DOM route)");
			}
		}

		[Test]
		public void Test_Conditions_Do_Not_Affect_Reading()
		{
			// Always excludes from binding too; the conditional members bind normally
			var dto = CrystalJson.Deserialize<IgnoreConditionDto>("""{ "AlwaysHidden": "boo", "Pinned": 7, "MaybeNull": "x", "Count": 5, "Plain": 1 }""");
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
