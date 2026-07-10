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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client;

	/// <summary>Tests for the scenario model of the differential harness: the step vocabulary, the builder, and the JSON round-trip.</summary>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class ScenarioModelFacts : SimpleTest
	{

		[Test]
		public void Test_ScenarioText_Roundtrips()
		{
			// printable ascii is encoded as-is
			Assert.That(ScenarioText.Encode(Slice.FromStringAscii("hello world!")), Is.EqualTo("hello world!"));
			Assert.That(ScenarioText.Decode("hello world!"), Is.EqualTo(Slice.FromStringAscii("hello world!")));

			// nil and empty are distinct
			Assert.That(ScenarioText.Encode(Slice.Nil), Is.Null);
			Assert.That(ScenarioText.Encode(Slice.Empty), Is.EqualTo(""));
			Assert.That(ScenarioText.Decode(null), Is.EqualTo(Slice.Nil));
			Assert.That(ScenarioText.Decode(""), Is.EqualTo(Slice.Empty));

			// backslash is escaped
			Assert.That(ScenarioText.Encode(Slice.FromStringAscii(@"a\b")), Is.EqualTo(@"a\\b"));
			Assert.That(ScenarioText.Decode(@"a\\b"), Is.EqualTo(Slice.FromStringAscii(@"a\b")));

			// non-printable bytes are hex-escaped
			var binary = Slice.FromBytes([ 0x00, 0x1F, (byte) 'A', 0x7F, 0xFF ]);
			Assert.That(ScenarioText.Encode(binary), Is.EqualTo(@"\x00\x1fA\x7f\xff"));
			Assert.That(ScenarioText.Decode(@"\x00\x1fA\x7f\xff"), Is.EqualTo(binary));

			// any byte sequence round-trips
			var all = Slice.FromBytes(Enumerable.Range(0, 256).Select(i => (byte) i).ToArray());
			Assert.That(ScenarioText.Decode(ScenarioText.Encode(all)), Is.EqualTo(all));

			// malformed escapes are rejected
			Assert.That(() => ScenarioText.Decode(@"\x2"), Throws.InstanceOf<FormatException>());
			Assert.That(() => ScenarioText.Decode(@"\q"), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Builder_Emits_Steps_In_Order()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "hello");
			builder.Get("A", "k1");
			int watchId = builder.Watch("A", "k1");
			int vsId = builder.GetVersionstamp("A");
			builder.Commit("A");
			builder.ExpectFired(watchId);
			builder.ExpectVersionstamp(vsId);
			builder.Dispose("A");
			var scenario = builder.Build("test_builder", "builder smoke");

			Assert.That(scenario.Name, Is.EqualTo("test_builder"));
			Assert.That(scenario.Description, Is.EqualTo("builder smoke"));
			Assert.That(scenario.Steps, Has.Count.EqualTo(9));

			Assert.That(scenario.Steps[0].Op, Is.EqualTo(ScenarioOp.Begin));
			Assert.That(scenario.Steps[0].Actor, Is.EqualTo("A"));

			Assert.That(scenario.Steps[1].Op, Is.EqualTo(ScenarioOp.Set));
			Assert.That(scenario.Steps[1].Key, Is.EqualTo(Slice.FromStringAscii("k1")));
			Assert.That(scenario.Steps[1].Value, Is.EqualTo(Slice.FromStringAscii("hello")));

			Assert.That(scenario.Steps[2].Op, Is.EqualTo(ScenarioOp.Get));
			Assert.That(scenario.Steps[2].Snapshot, Is.False);

			Assert.That(scenario.Steps[3].Op, Is.EqualTo(ScenarioOp.Watch));
			Assert.That(scenario.Steps[3].HandleId, Is.EqualTo(watchId));

			Assert.That(scenario.Steps[4].Op, Is.EqualTo(ScenarioOp.GetVersionstamp));
			Assert.That(scenario.Steps[4].HandleId, Is.EqualTo(vsId));
			Assert.That(vsId, Is.Not.EqualTo(watchId), "handle ids must be unique across kinds");

			Assert.That(scenario.Steps[6].Op, Is.EqualTo(ScenarioOp.ExpectFired));
			Assert.That(scenario.Steps[6].HandleId, Is.EqualTo(watchId));

			Assert.That(scenario.Steps[8].Op, Is.EqualTo(ScenarioOp.Dispose));
		}

		[Test]
		public void Test_Scenario_Json_Roundtrip()
		{
			// exercise the whole step vocabulary at least once
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "v1");
			builder.Clear("A", "k2");
			builder.ClearRange("A", "k3", "k9");
			builder.Atomic("A", "counter", Slice.FromFixed64(1), FdbMutationType.Add);
			builder.SetVersionstampedKey("A", Slice.FromStringAscii("log-") + Slice.Zero(10), 4, "payload");
			builder.SetVersionstampedValue("A", "marker", Slice.Zero(10), 0);
			builder.Get("A", "k1", snapshot: true);
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k1"), OrEqual: true, Offset: 1));
			builder.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), false, 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), false, 1),
				limit: 10, reverse: true);
			builder.GetReadVersion("A");
			int vsId = builder.GetVersionstamp("A");
			int watchId = builder.Watch("A", "k1");
			builder.Commit("A");
			builder.GetCommittedVersion("A");
			builder.ExpectVersionstamp(vsId);
			builder.ExpectPending(watchId, ScenarioTolerance.AllowSpuriousWatchFire);
			builder.Reset("A");
			builder.Dispose("A");
			var scenario = builder.Build("test_json_roundtrip");

			var json = scenario.ToJson();
			Log(json.ToJsonText(CrystalJsonSettings.JsonIndented));
			var decoded = Scenario.FromJson(json);

			// records with list properties have no structural equality: compare via canonical JSON
			Assert.That(decoded.ToJson(), Is.EqualTo(json));
			Assert.That(decoded.Steps, Has.Count.EqualTo(scenario.Steps.Count));
			Assert.That(decoded.Steps[9].Limit, Is.EqualTo(10));
			Assert.That(decoded.Steps[9].Reverse, Is.True);
			Assert.That(decoded.Steps[16].Tolerance, Is.EqualTo(ScenarioTolerance.AllowSpuriousWatchFire));
		}

		[Test]
		public void Test_Campaign_Vocabulary_Roundtrips()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.SetOption("A", ScenarioTransactionOption.ReadYourWritesDisable);
			builder.SetOption("A", ScenarioTransactionOption.SnapshotReadYourWritesDisable);
			builder.TouchMetadataVersion("A");
			builder.GetMetadataVersion("A");
			int w = builder.WatchMetadataVersion("A");
			builder.Commit("A");
			builder.ExpectFired(w);
			var scenario = builder.Build("campaign_vocab");

			Assert.That(scenario.Steps[1].Op, Is.EqualTo(ScenarioOp.SetOption));
			Assert.That(scenario.Steps[1].Option, Is.EqualTo(ScenarioTransactionOption.ReadYourWritesDisable));
			Assert.That(scenario.Steps[2].Option, Is.EqualTo(ScenarioTransactionOption.SnapshotReadYourWritesDisable));
			Assert.That(scenario.Steps[3].Op, Is.EqualTo(ScenarioOp.TouchMetadataVersion));
			Assert.That(scenario.Steps[4].Op, Is.EqualTo(ScenarioOp.GetMetadataVersion));
			Assert.That(scenario.Steps[5].Op, Is.EqualTo(ScenarioOp.WatchMetadataVersion));
			Assert.That(scenario.Steps[5].HandleId, Is.EqualTo(w));

			var json = scenario.ToJson();
			Assert.That(Scenario.FromJson(json).ToJson(), Is.EqualTo(json));
		}

		[Test]
		public void Test_Scenario_Json_Rejects_Unknown_Op()
		{
			var json = JsonObject.Create(
			[
				("name", "bogus"),
				("steps", JsonArray.Create(JsonObject.Create([ ("op", "Frobnicate"), ("actor", "A") ])))
			]);
			Assert.That(() => Scenario.FromJson(json), Throws.InstanceOf<FormatException>());
		}

	}

}
