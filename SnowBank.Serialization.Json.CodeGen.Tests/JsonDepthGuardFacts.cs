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
	using System.Globalization;
	using SnowBank.Data;
	using SnowBank.Data.Xml;
	using SnowBank.Serialization.Json.CodeGen.Tests.Acme;
	using SnowBank.Serialization.Json.CodeGen.Tests.AcmeLegacy;

	/// <summary>Runs the code the generator emitted for the two JSON formats, over object graphs that loop or run very deep</summary>
	/// <remarks>
	/// <para>Neither generated JSON path used to count anything: a two-node cycle recursed until the native stack gave out,
	/// raising a <see cref="StackOverflowException"/> that .NET cannot catch and that takes the whole process down (measured
	/// before this guard existed: ~8000 nested <c>Pack</c> frames, ~6000 nested <c>Serialize</c> frames). Every fact below
	/// pins the typed, catchable answer that replaced it.</para>
	/// <para>The boundary facts deliberately restate the cap by name rather than by value, so that moving
	/// <see cref="CrystalJsonWriter.MaxDepth"/> moves the tests with it; what they pin is WHERE the line sits relative to
	/// the cap, not what the cap happens to be.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class JsonDepthGuardFacts : SimpleTest
	{

		/// <summary>Builds an ACYCLIC chain of <paramref name="length"/> nodes, so the deepest one sits at depth <c>length - 1</c></summary>
		private static Chain MakeChain(int length)
		{
			var head = new Chain();
			var tail = head;
			for (int i = 1; i < length; i++)
			{
				var next = new Chain();
				tail.Next = next;
				tail = next;
			}
			return head;
		}

		/// <summary>Same shape, on the type enrolled in the <c>DataContractCompat</c> container</summary>
		private static CycleNode MakeCompatChain(int length)
		{
			var head = new CycleNode();
			var tail = head;
			for (int i = 1; i < length; i++)
			{
				var next = new CycleNode();
				tail.Next = next;
				tail = next;
			}
			return head;
		}

		#region The shared cap...

		[Test]
		public void Test_Every_Wire_Shares_One_Nesting_Cap()
		{
			// the whole point of the constant: a document the XML emission writes must not be refused by the JSON one (or the
			// other way round) purely because the two disagreed on where "too deep" starts
			Assert.That(CrystalXml.MaxDepth, Is.EqualTo(CrystalJsonWriter.MaxDepth), "CrystalXml.MaxDepth is an alias, not a second opinion");
		}

		#endregion

		#region Pack (the DOM path, no writer in hand)...

		[Test]
		public void Test_A_Reference_Cycle_Through_Generated_Pack_Throws_Instead_Of_Overflowing_The_Stack()
		{
			// a cycle has no JSON representation here (there is no $id/$ref form), so the packer must stop with a typed,
			// CATCHABLE error rather than recursing into a StackOverflowException
			var a = new Chain { Label = "a" };
			var b = new Chain { Label = "b" };
			a.Next = b;
			b.Next = a;

			Assert.That(
				() => AcmeSerializers.Chain.Pack(a),
				Throws.InstanceOf<JsonSerializationException>().With.Message.Contains(CrystalJsonWriter.MaxDepth.ToString(CultureInfo.InvariantCulture)),
				"the same exception the reflection path raises for the same condition, naming the cap it hit");
		}

		[Test]
		public void Test_A_Reference_Cycle_Through_Generated_Pack_Throws_On_The_Compat_Profile_Too()
		{
			// the DataContractCompat container emits its own Pack body: the guard has to be on BOTH, not just the modern one
			var a = new CycleNode { Label = "a" };
			var b = new CycleNode { Label = "b" };
			a.Next = b;
			b.Next = a;

			Assert.That(
				() => LegacySerializers.CycleNode.Pack(a),
				Throws.InstanceOf<JsonSerializationException>().With.Message.Contains(CrystalJsonWriter.MaxDepth.ToString(CultureInfo.InvariantCulture)));
		}

		[Test]
		public void Test_A_Deep_Acyclic_Compat_Chain_Up_To_The_Cap_Is_Packed_In_Full()
		{
			// same boundary pair as the modern Chain, restated on the DataContractCompat container: the compat Pack body has
			// its own emitted guard, and this pins WHERE its line sits, not just that a cycle is refused somewhere
			var obj = (JsonObject) LegacySerializers.CycleNode.Pack(MakeCompatChain(CrystalJsonWriter.MaxDepth));

			int levels = 0;
			for (JsonValue? cursor = obj; cursor is JsonObject o; cursor = o[LegacySerializers.CycleNode.PropertyNames.Next])
			{
				++levels;
			}
			Assert.That(levels, Is.EqualTo(CrystalJsonWriter.MaxDepth), "one packed object per node of the chain");
		}

		[Test]
		public void Test_A_Deep_Acyclic_Compat_Chain_Past_The_Cap_Throws_The_Same_Typed_Exception()
		{
			// one node deeper than the cap, on the compat container: still the same typed exception, not a crash
			Assert.That(
				() => LegacySerializers.CycleNode.Pack(MakeCompatChain(CrystalJsonWriter.MaxDepth + 1)),
				Throws.InstanceOf<JsonSerializationException>());
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Up_To_The_Cap_Is_Packed_In_Full()
		{
			// the guard must not eat a legitimate (if absurd) deep document: a chain exactly as deep as the cap allows still
			// packs, which is what pins WHERE the line sits
			var obj = (JsonObject) AcmeSerializers.Chain.Pack(MakeChain(CrystalJsonWriter.MaxDepth));

			int levels = 0;
			for (JsonValue? cursor = obj; cursor is JsonObject o; cursor = o[AcmeSerializers.Chain.PropertyNames.Next])
			{
				++levels;
			}
			Assert.That(levels, Is.EqualTo(CrystalJsonWriter.MaxDepth), "one packed object per node of the chain");
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Past_The_Cap_Throws_The_Same_Typed_Exception()
		{
			// one node deeper than the cap: the guard cannot tell a cycle from a graph that is simply too deep, and both
			// answers are the same typed exception rather than a crash
			Assert.That(
				() => AcmeSerializers.Chain.Pack(MakeChain(CrystalJsonWriter.MaxDepth + 1)),
				Throws.InstanceOf<JsonSerializationException>());
		}

		[Test]
		public void Test_The_Pack_Depth_Counter_Starts_Over_On_Each_Root_Call()
		{
			// the counter is a parameter, not writer state: packing the same value twice must not accumulate, or the second
			// call of a long-lived converter would start closer to the cap than the first
			var chain = MakeChain(CrystalJsonWriter.MaxDepth);

			Assert.That(() => AcmeSerializers.Chain.Pack(chain), Throws.Nothing);
			Assert.That(() => AcmeSerializers.Chain.Pack(chain), Throws.Nothing, "a root call always starts the count at zero");
		}

		#endregion

		#region Serialize (the text path, writer in hand)...

		[Test]
		public void Test_A_Reference_Cycle_Through_Generated_Serialize_Throws_Instead_Of_Overflowing_The_Stack()
		{
			// the writer already owned the depth machinery (MarkVisited/Leave) but the generated Serialize path never called
			// into it, so it was every bit as unguarded as Pack: this pins that it now brackets its body with the writer's
			// counter
			var a = new Chain { Label = "a" };
			var b = new Chain { Label = "b" };
			a.Next = b;
			b.Next = a;

			Assert.That(
				() => AcmeSerializers.Chain.ToJsonText(a),
				Throws.InstanceOf<JsonSerializationException>().With.Message.Contains(CrystalJsonWriter.MaxDepth.ToString(CultureInfo.InvariantCulture)));
		}

		[Test]
		public void Test_A_Reference_Cycle_Through_Generated_Serialize_Throws_On_The_Compat_Profile_Too()
		{
			// the DataContractCompat container emits its own Serialize body too: same requirement as the Pack side, restated
			// on the text path
			var a = new CycleNode { Label = "a" };
			var b = new CycleNode { Label = "b" };
			a.Next = b;
			b.Next = a;

			Assert.That(
				() => LegacySerializers.CycleNode.ToJsonText(a),
				Throws.InstanceOf<JsonSerializationException>().With.Message.Contains(CrystalJsonWriter.MaxDepth.ToString(CultureInfo.InvariantCulture)));
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Up_To_The_Cap_Is_Serialized_In_Full()
		{
			string json = AcmeSerializers.Chain.ToJsonText(MakeChain(CrystalJsonWriter.MaxDepth));

			int levels = 0;
			for (int i = json.IndexOf("\"next\"", StringComparison.Ordinal); i >= 0; i = json.IndexOf("\"next\"", i + 6, StringComparison.Ordinal))
			{
				++levels;
			}
			Assert.That(levels, Is.EqualTo(CrystalJsonWriter.MaxDepth - 1), "one nested field per node past the root");
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Past_The_Cap_Is_Refused_By_The_Writer()
		{
			Assert.That(
				() => AcmeSerializers.Chain.ToJsonText(MakeChain(CrystalJsonWriter.MaxDepth + 1)),
				Throws.InstanceOf<JsonSerializationException>());
		}

		[Test]
		public void Test_The_Serialize_Depth_Counter_Unwinds_Between_Siblings()
		{
			// EnterDepth/LeaveDepth must be balanced: two siblings written through the SAME writer each open a level, and the
			// second must not start where the first one ended. Each one alone stops just short of the cap, so an unbalanced
			// bracket makes the second one trip.
			var deep = MakeChain(CrystalJsonWriter.MaxDepth - 1);

			using var writer = new CrystalJsonWriter(0, CrystalJsonSettings.Json, CrystalJson.DefaultResolver);
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			AcmeSerializers.Chain.Default.Serialize(writer, deep);
			writer.WriteTailSeparator();
			AcmeSerializers.Chain.Default.Serialize(writer, deep);
			writer.EndArray(state);

			string json = writer.GetString();
			Assert.That(json, Does.StartWith("[").And.EndsWith("]"), "both siblings were written at the same level");
		}

		#endregion

	}

}
