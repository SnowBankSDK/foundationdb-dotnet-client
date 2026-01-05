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

#pragma warning disable CS0618 // Type or member is obsolete
namespace SnowBank.IO.Hashing.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class HighestRandomWeightHashFacts : SimpleTest
	{

		[Test]
		public void Test_Can_Choose_Node_From_Key()
		{
			// Best_Girl_Selector

			string[] girls = [ "ANGELICA", "LAETICIA", "PATRICIA", "MATHILDA", "ROBERTA", "SABRINA", "VERONICA" ];

			var loveMachine = new HighestRandomWeightHash<Slice, string>(
				// list of "nodes"
				buckets: girls, 
				// main hash function (node_seed, key) => score
				kf: (seed, key) => XxHash64.Continue(seed, key), 
				// node seed generator (node) => node_seed
				nf: (node) => XxHash64.FromText(node),
				// used to perturb the node seeds (ie: "generation number")
				seed: 0
			);

			Log("Scores('dupond') -> " + string.Join(", ", loveMachine.Score(Slice.FromString("dupond")).OrderByDescending(x => x.Score).Select(x => $"({x.Bucket}, {x.Score:F05})")));
			Log("Scores('dupont') -> " + string.Join(", ", loveMachine.Score(Slice.FromString("dupont")).OrderByDescending(x => x.Score).Select(x => $"({x.Bucket}, {x.Score:F05})")));
			Log("Scores('durand') -> " + string.Join(", ", loveMachine.Score(Slice.FromString("durand")).OrderByDescending(x => x.Score).Select(x => $"({x.Bucket}, {x.Score:F05})")));

			// Choose()
			Assert.That(loveMachine.Choose(Slice.FromString("dupond")), Is.EqualTo("ROBERTA"), "BestGirl('dupond')");
			Assert.That(loveMachine.Choose(Slice.FromString("dupont")), Is.EqualTo("PATRICIA"), "BestGirl('dupont')");
			Assert.That(loveMachine.Choose(Slice.FromString("durand")), Is.EqualTo("VERONICA"), "BestGirl('durand')");

			// ChooseMultiple(2)
			Assert.That(loveMachine.ChooseMultiple(Slice.FromString("dupond"), 2), Is.EqualTo(new [] { "ROBERTA", "MATHILDA" }), "BestGirls('dupond', 2)");
			Assert.That(loveMachine.ChooseMultiple(Slice.FromString("dupont"), 2), Is.EqualTo(new [] { "PATRICIA", "SABRINA" }), "BestGirls('dupont', 2)");
			Assert.That(loveMachine.ChooseMultiple(Slice.FromString("durand"), 2), Is.EqualTo(new [] { "VERONICA", "ANGELICA" }), "BestGirls('durand', 2)");

			// RankBy()
			Assert.That(loveMachine.RankBy(Slice.FromString("dupond")), Is.EqualTo(new [] { "ROBERTA", "MATHILDA", "LAETICIA", "ANGELICA", "SABRINA", "PATRICIA", "VERONICA" }), "BestGirls('dupond', *)");
			Assert.That(loveMachine.RankBy(Slice.FromString("dupont")), Is.EqualTo(new [] { "PATRICIA", "SABRINA", "ROBERTA", "LAETICIA", "ANGELICA", "VERONICA", "MATHILDA" }), "BestGirls('dupont', *)");
			Assert.That(loveMachine.RankBy(Slice.FromString("durand")), Is.EqualTo(new [] { "VERONICA", "ANGELICA", "SABRINA", "ROBERTA", "MATHILDA", "LAETICIA", "PATRICIA" }), "BestGirls('durant', *)");

			// Score()
			Assert.That(loveMachine.Score(Slice.FromString("dupond")).Select(x => x.Bucket), Is.EquivalentTo(girls));
			Assert.That(loveMachine.Score(Slice.FromString("dupont")).Select(x => x.Bucket), Is.EquivalentTo(girls));
			Assert.That(loveMachine.Score(Slice.FromString("durand")).Select(x => x.Bucket), Is.EquivalentTo(girls));
		}

		[Test]
		public void Test_Random_Distribution()
		{
			// Count the number of times each bucket is assigned into each position, for a bunch of sequential keys (KEY#######) and bucket names with very low variance
			// => verify that each combination is as close as possible to the expected normal distribution (1/B)

			const int BUCKETS = 10;
			const int KEYS = 100_000;

			Log($"Initializing {BUCKETS} buckets...");
			var buckets = Enumerable.Range(0, BUCKETS).Select(i => $"SRV{i:D03}").ToArray();

			var hrw = new HighestRandomWeightHash<Slice, string>(
				buckets,
				(n, k) => XxHash64.Continue(n, k),
				(n) => XxHash64.FromText(n)
			);

			// Map each bucket to the number of times it was selected as the Nth candidate (ie: int[0] is number of time selected as highest, int[1] second highest, int[BUCKETS-1] last)
			var counters = new Dictionary<string, int[]>(StringComparer.Ordinal);
			foreach(var b in buckets) counters[b] = new int[BUCKETS];

			var rng = new Random();
			int seed = rng.Next();
			Log("# using random seed " + seed);
			rng = new Random(seed);

			Log($"Ranking {KEYS} keys...");
			for (int i = 0; i < KEYS; i++)
			{
				var key = Slice.FromString($"KEY_{i:X08}");
				var ranks = hrw.RankBy(key).ToArray();
				if (ranks.Length != BUCKETS) Assert.That(ranks, Is.Not.Null.And.Length.EqualTo(BUCKETS));

				// increment all the selected buckets
				for (int j = 0; j < ranks.Length; j++)
				{
					counters[ranks[j]][j]++;
				}
			}

			Log("Results:");
			const double EXPECTED_SHARE = 100.0 / BUCKETS;
			foreach (var kv in counters)
			{
				Log($"- '{kv.Key}': {string.Join(", ", kv.Value.Select((x, i) => $"#{i+1} = {x * 100.0 / KEYS,6:F3}% ({100.0 * ((x * 100.0 / KEYS) - EXPECTED_SHARE) / EXPECTED_SHARE,5:##0.0}%)"))}");
			}

			const double TOLERANCE = 0.05; // 5% deviation, for N=10 means 9.5..10.5%
			const double MIN_SHARE = EXPECTED_SHARE * (1 - TOLERANCE);
			const double MAX_SHARE = EXPECTED_SHARE * (1 + TOLERANCE);
			foreach (var kv in counters)
			{
				for (int i = 0; i < kv.Value.Length; i++)
				{
					double p = 100.0 * kv.Value[i] / KEYS;
					if (p < MIN_SHARE || p > MAX_SHARE)
					{
						Assert.Warn($"Bucket '{kv.Key}' was assigned into slot #{(i + 1)} about {p,6:F2}% of the time, which is outside the {MIN_SHARE:F2} .. {MAX_SHARE:F2}% allowed range!");
					}
				}
			}
		}

		private static List<(string Key, string Bucket)> Compute(int count, string[] buckets, ulong seed = 0)
		{

			var hrw = new HighestRandomWeightHash<Slice, string>(
				buckets,
				(n, k) => XxHash64.Continue(n, k),
				(n) => XxHash64.FromText(n),
				seed
			);

			var keys = new List<(string Key, string Bucket)>(count);
			for (int i = 0; i < count; i++)
			{
				string k = $"KEY_{i:X04}";
				string b = hrw.Choose(Slice.FromString(k));
				keys.Add((k, b));
			}

			return keys;
		}

		[Test]
		public void Test_Removing_One_Bucket_Should_Only_Impact_Keys_Assigned_To_It()
		{
			// TEST:
			// - Assign K keys to N buckets
			// - Remove 1 elements from the buckets
			// - Assign the same K keys again, to the N-1 remaining buckets
			// ASSERT:
			// - All keys assigned to the remove bucket should be reassigned to one of the N-1 remaning buckets
			// - All keys assigned to the other N-1 buckets should not change their assigned bucket

			const int KEYS = 10_000;

			Log("Before: 7 candidates");
			var keys1 = Compute(KEYS, [ "ANGELICA", "LAETICIA", "PATRICIA", "MATHILDA", "MARCELUS", "SABRINA", "VERONICA" ]); // one is not like the others... :/
			// remove "MARCELUS" from the pool!!!!
			Log("After : 6 candidates");
			var keys2 = Compute(KEYS, [ "ANGELICA", "LAETICIA", "PATRICIA", "MATHILDA", /*-------*/ "SABRINA", "VERONICA" ]); // much better!

			// Check all keys that only the keys assign previously to MARCELUS have been "reassigned"
			int changed = 0;
			var gained = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < KEYS; i ++)
			{
				Assume.That(keys1[i].Key == keys2[i].Key);

				var b1 = keys1[i].Bucket;
				var b2 = keys2[i].Bucket;

				if (b1 != b2) changed++;

				if (b1 != "MARCELUS")
				{ // the key should not change bucket!
					if (b2 != b1) Assert.That(b2, Is.EqualTo(b1), $"The bucket for key {keys1[i].Key} was changed from {b1} to {b2} even though it should not have been affected!");
				}
				else
				{ // the key should be remaped to someone else!
					if (b2 == "MARCELUS") Assert.That(b2, Is.Not.EqualTo("MARCELUS"), $"The bucket for key {keys1[i].Key} should have been reassigned, but is still equal to {b2} !");
					gained[b2] = (gained.GetValueOrDefault(b2, 0)) + 1;
				}
			}
			Log($"Found {changed:N0} keys out of {KEYS:N0} that have been reassigned (1 in {1.0*KEYS/changed:F3})");
			Assert.That(changed, Is.GreaterThan(0.95 * KEYS / 7), "Too few keys were remapped!");
			Assert.That(changed, Is.LessThan(1.05 * KEYS / 7), "Too many keys were remapped!");
			foreach (var kv in gained)
			{
				Log($"- {kv.Key,-8} gained {kv.Value:N0} votes ({100.0*kv.Value/KEYS:F2}%)");
			}

		}

		[Test]
		public void Test_Adding_One_Bucket_Should_Only_Impact_A_Fraction_Of_Keys()
		{
			// TEST:
			// - Assign K keys to N buckets
			// - Add 1 element to the buckets
			// - Assign the same K keys again, to the now N+1 buckets
			// ASSERT:
			// - All keys that are not assigned to the new bucket should still use the same assigned bucket
			// - All keys assigned to the new bucket should only represent about 1/N-th of the keys

			const int KEYS = 10_000;

			Log("Before: 6 candidates");
			var keys1 = Compute(KEYS, [ "ANGELICA", "LAETICIA", "PATRICIA", "MATHILDA", /*------*/ "SABRINA", "VERONICA" ]); // something is missing....
			// remove "MARCELUS" from the pool!!!!
			Log("After : 7 candidates");
			var keys2 = Compute(KEYS, [ "ANGELICA", "LAETICIA", "PATRICIA", "MATHILDA", "ROBERTA", "SABRINA", "VERONICA" ]); // much better!

			var stolen = new Dictionary<string, int>(StringComparer.Ordinal);

			// Check all keys
			int changed = 0;
			for (int i = 0; i < KEYS; i ++)
			{
				Assume.That(keys1[i].Key == keys2[i].Key);

				var b1 = keys1[i].Bucket;
				var b2 = keys2[i].Bucket;

				if (b1 != b2) changed++;

				if (b2 == "ROBERTA")
				{
					stolen[b1] = (stolen.GetValueOrDefault(b1, 0)) + 1;
				}
				else
				{ // the key should not change assignment
					if (b1 != b2) Assert.That(b2, Is.EqualTo(b1), $"The bucket for key {keys1[i].Key} should not have been reassigned, but has changed from {b1} to {b2} !");
				}
			}
			Log($"Found {changed:N0} keys out of {KEYS:N0} that have been reassigned (1 in {1.0*KEYS/changed:F3})");
			Assert.That(changed, Is.GreaterThan(0.9 * KEYS / 7), "Too few keys were remapped!");
			Assert.That(changed, Is.LessThan(1.1 * KEYS / 7), "Too many keys were remapped!");
			foreach (var kv in stolen)
			{
				Log($"- {kv.Key,-8} lost {kv.Value:N0} votes ({100.0*kv.Value/KEYS:F2}%)");
			}
		}

		[Test]
		public void Test_Changing_Buckets_Seed_Should_Change_The_Key_Distribution()
		{
			// We have the same list of buckets and keys, but only change the global seed
			// - The distribution of both cases should be random
			// - The transition for each key should also be random

			const int KEYS = 10_000;

			var girls = new[] {"ANGELICA", "LAETICIA", "PATRICIA", "MATHILDA", "SABRINA", "VERONICA"};

			Log("Before: seed=12345678");
			var keys1 = Compute(KEYS, girls, seed: 12345678);
			Log("After : seed=87654321");
			var keys2 = Compute(KEYS, girls, seed: 87654321);

			var counters = new Dictionary<string, (int Before, int After)>(StringComparer.Ordinal);
			foreach (var b in girls) counters[b] = (0, 0);

			int unchanged = 0;
			for (int i = 0; i < KEYS; i++)
			{
				var b1 = keys1[i].Bucket;
				var before = counters[keys1[i].Bucket];
				before.Before++;
				counters[keys1[i].Bucket] = before;

				var b2 = keys2[i].Bucket;
				var after = counters[b2];
				after.After++;
				counters[b2] = after;

				if (b1 == b2) unchanged++;

			}
			Log($"Found {unchanged:N0} keys out of {KEYS:N0} that have NOT been reassigned (1 in {1.0*KEYS/unchanged:F3})");
			Assert.That(unchanged, Is.GreaterThan(0.9 * KEYS / 6), "Too few keys were unchanged!");
			Assert.That(unchanged, Is.LessThan(1.1 * KEYS / 6), "Too many keys were unchanged!");
			foreach (var kv in counters)
			{
				Log($"- {kv.Key,-8} changed from {kv.Value.Before:N0} to {kv.Value.After:N0} votes ({100.0 * (kv.Value.After - kv.Value.Before) / KEYS:F4}%)");
			}
		}

	}

}

