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

namespace FoundationDB.Storage.FdbLite.Tests
{
	using System.Buffers.Binary;
	using FoundationDB.Storage.FdbLite;

	/// <summary>Diagnostic: what fraction of leaf splits could an already-dirty sibling have absorbed (the spill-on-split opportunity, OQ-5)?</summary>
	/// <remarks>
	/// <para>Reads the <see cref="FdbLiteTreeWriter.LeafSplits"/> counter family, which the writer books at its
	/// single leaf-split site without changing behavior. Reports rather than asserts thresholds, because the
	/// point is to find out; marked Explicit so the finding is gathered on demand instead of taxing every suite
	/// run. The absorbable count is an UPPER BOUND by design (minimal move, compact sizing, recipient allowed
	/// to fill to 100%) - a small number here KILLS the spill arm, a large one only keeps it alive.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	[Explicit("diagnostic: spill-on-split opportunity measurement, run on demand")]
	public class FdbLiteSplitSpillOpportunityFacts : SimpleTest
	{

		private static byte[] Key(long i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		private sealed class SplitTally
		{
			public int Splits { get; set; }
			public int WithDirtySibling { get; set; }
			public int Absorbable { get; set; }
			public int Appended { get; set; }

			public void Add(FdbLiteTreeWriter w)
			{
				this.Splits += w.LeafSplits;
				this.WithDirtySibling += w.LeafSplitsWithDirtySibling;
				this.Absorbable += w.LeafSplitsAbsorbableByDirtySibling;
				this.Appended += w.PagesAppended;
			}

			public string Fractions => this.Splits == 0
				? "no leaf splits"
				: $"dirty-sibling {100.0 * this.WithDirtySibling / this.Splits:N1}%, absorbable {100.0 * this.Absorbable / this.Splits:N1}% of {this.Splits:N0} splits";
		}

		/// <summary>Random insert-heavy growth: leaves fill and split mid-generation while their neighbors are dirty from the same burst - the friendliest realistic shape for a spill arm.</summary>
		[Test]
		public void Diagnose_Random_Growth_Workload()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var rnd = new Random(20260729);
			var model = new HashSet<long>();

			const int GENS = 6;
			const int PER_GEN = 10_000;
			var tally = new SplitTally();
			for (int g = 0; g < GENS; g++)
			{
				var w = engine.BeginWrite();
				for (int op = 0; op < PER_GEN; op++)
				{
					long k = rnd.NextInt64(0, 1L << 40);
					w.Insert(Key(k), new byte[rnd.Next(0, 100)]);
					model.Add(k);
				}
				engine.Commit(w, (ulong) (g + 1));
				Assert.That(w.LeafSplits, Is.LessThanOrEqualTo(w.PageSplits), "leaf splits are a subset of all splits");
				Log($"# [random-growth] gen {g + 1}: splits={w.LeafSplits} dirtySibling={w.LeafSplitsWithDirtySibling} absorbable={w.LeafSplitsAbsorbableByDirtySibling} appended={w.PagesAppended}");
				tally.Add(w);
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count), "sanity: the instrument must not be measuring a broken tree");
			Assert.That(tally.Splits, Is.GreaterThan(0), "this shape must split, or the diagnostic is measuring nothing");
			Assert.That(tally.WithDirtySibling, Is.GreaterThan(0), "mechanism: the probe must have engaged (random growth dirties neighbors)");
			Log($"# [random-growth] TOTAL: {tally.Fractions}");
		}

		/// <summary>Random churn over a bounded keyspace (same shape and seed as the consolidation-opportunity diagnostic): the OQ-5-relevant steady state, where splits are rare and neighbors mostly clean.</summary>
		[Test]
		public void Diagnose_Random_Churn_Workload()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var rnd = new Random(20260729);
			var model = new HashSet<long>();

			const int KEYSPACE = 30_000;
			const int GENS = 24;
			var tally = new SplitTally();
			for (int g = 0; g < GENS; g++)
			{
				var w = engine.BeginWrite();
				for (int op = 0; op < 6_000; op++)
				{
					long k = rnd.Next(KEYSPACE);
					if (rnd.Next(10) < 6 || !model.Contains(k))
					{
						w.Insert(Key(k), new byte[rnd.Next(0, 100)]);
						model.Add(k);
					}
					else
					{
						Assert.That(w.Remove(Key(k)), Is.True);
						model.Remove(k);
					}
				}
				engine.Commit(w, (ulong) (g + 1));
				Log($"# [random-churn] gen {g + 1}: splits={w.LeafSplits} dirtySibling={w.LeafSplitsWithDirtySibling} absorbable={w.LeafSplitsAbsorbableByDirtySibling}");
				tally.Add(w);
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count), "sanity: the instrument must not be measuring a broken tree");
			Log($"# [random-churn] TOTAL: {tally.Fractions}");
		}

		/// <summary>Discriminator: a split between siblings that are dirty but PACKED must count the dirty sibling and refuse the absorb - proving the capacity check can say no, so the high absorbable fractions elsewhere are findings and not a stuck-true probe.</summary>
		/// <remarks>Cells are sized so a page holds ~10 of them with a free gap well under one cell (3,026 B needed against a ~2,500 B gap even after a prefix strip), which makes the refusal deterministic rather than tuned.</remarks>
		[Test]
		public void Diagnose_Packed_Dirty_Siblings_Cannot_Absorb()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[3_000];

			var w = engine.BeginWrite();
			const int N = 200;
			for (long i = 0; i < N; i++)
			{ // sequential fill in ONE generation: the append path packs each finished page, and every page stays dirty
				w.Insert(Key(i), value);
			}
			Assert.That(w.LeafSplits, Is.Zero, "the sequential fill must ride the append path, not split");
			Assert.That(w.PagesAppended, Is.GreaterThan(2), "the fill must have produced a row of packed sibling pages");

			// a 9-byte key sorting between Key(mid) and Key(mid+1): interior, so it lands in a packed page with
			// packed dirty siblings on both sides
			var interior = new byte[9];
			Key(N / 2).CopyTo(interior, 0);
			interior[8] = 0x01;
			w.Insert(interior, value);

			Assert.That(w.LeafSplits, Is.EqualTo(1), "the interior insert must split its packed page");
			Assert.That(w.LeafSplitsWithDirtySibling, Is.EqualTo(1), "both neighbors were written by this same generation");
			Assert.That(w.LeafSplitsAbsorbableByDirtySibling, Is.Zero, "a packed sibling has no room for the spill: the capacity check must refuse");

			engine.Commit(w, 1);
			Log($"# [packed-siblings] splits={w.LeafSplits} dirtySibling={w.LeafSplitsWithDirtySibling} absorbable={w.LeafSplitsAbsorbableByDirtySibling} (the refusal branch fired)");
		}

		/// <summary>The task-trail shape: F-B says its inserts ride the append edge and never split, so the counter family must read zero here - asserted, because a control that drifts is a finding.</summary>
		[Test]
		public void Diagnose_Task_Trail_Control()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[32];

			const int BATCH = 2_000;
			const int GENS = 12;
			var tally = new SplitTally();
			for (int g = 0; g < GENS; g++)
			{
				var w = engine.BeginWrite();
				long edge = (long) g * BATCH;
				for (long i = edge; i < edge + BATCH; i++)
				{
					w.Insert(Key(i), value);
				}
				if (g > 0)
				{
					for (long i = edge - BATCH; i < edge; i++)
					{
						if (i % 5 != 0)
						{
							Assert.That(w.Remove(Key(i)), Is.True);
						}
					}
				}
				engine.Commit(w, (ulong) (g + 1));
				tally.Add(w);
			}

			Assert.That(tally.Splits, Is.Zero, "F-B: the trail shape appends, it does not split - a split here says the shape (or the append path) changed");
			Assert.That(tally.Appended, Is.GreaterThan(0), "the append fast path must have carried the inserts");
			Log($"# [task-trail] control: splits={tally.Splits} appended={tally.Appended}");
		}

	}

}
