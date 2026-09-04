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

namespace FoundationDB.FdbLite.Tests
{
	using FoundationDB.Storage;
	using System.Buffers.Binary;
	using FoundationDB.FdbLite;

	/// <summary>The fast non-regression GATE: fixed-seed workloads whose engine COUNTERS are asserted against pinned budgets.</summary>
	/// <remarks>
	/// <para>Wall clock appears nowhere: every number here is a deterministic count (pages written, splits, merges, blocks consumed), so the gate is valid on a loaded machine and cheap enough to run on every engine change. It exists because a performance regression is invisible to every correctness assertion - the results stay right, they just cost more to produce - and a bench window is too expensive to spend on noticing one.</para>
	/// <para>Budgets are CEILINGS with ~10-15% headroom over the pinned baseline (floors assert a mechanism FIRED). A legitimate engine change that moves a number past its band should retighten the band in the same commit, with the new baseline in the assertion message's place - that edit is the record that the cost change was seen and accepted.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteNonRegressionGateFacts : SimpleTest
	{

		private static FdbLiteEngine CreateEngine() => FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

		private static byte[] Key(byte region, long seq)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteUInt64BigEndian(key, ((ulong) region << 56) | (ulong) seq);
			return key;
		}

		private static byte[] Value(int seed, int length)
		{
			var value = new byte[length];
			new Random(seed).NextBytes(value);
			return value;
		}

		[Test]
		public void Gate_Sequential_Bulk_Load()
		{
			// the write-amp headline shape: one generation, 50k sorted inserts, append-packed
			using var engine = CreateEngine();
			var writer = engine.BeginWrite();
			for (long i = 0; i < 50_000; i++)
			{
				writer.Insert(Key(1, i), Value((int) i, 24));
			}
			engine.Commit(writer, 1);

			var stats = engine.MeasureTreeStatistics();
			Log($"# pagesWritten={writer.PagesWritten} appended={writer.PagesAppended} splits={writer.LeafSplits} leaves={stats.LeafPages} spliced={writer.CellsSpliced} descents={writer.LeafDescents}");

			Assert.That(writer.LeafSplits, Is.Zero, "sorted load must take the append path, never split");
			Assert.That(writer.PagesAppended, Is.GreaterThan(0), "the append fast path must actually fire");
			Assert.That(writer.CellsSpliced, Is.GreaterThanOrEqualTo(49_900), "virtually every insert must splice (page-fill boundaries rebuild; baseline 49,946)");
			Assert.That(writer.LeafDescents, Is.LessThanOrEqualTo(200), "a sorted run costs a few descents per LEAF, never one per key (baseline 159 for 54 leaves)");
			Assert.That(writer.PagesWritten, Is.LessThanOrEqualTo(65), "pages written budget (baseline 55)");
			Assert.That(stats.LeafPages, Is.LessThanOrEqualTo(60), "leaf population budget (baseline 54)");
			Assert.That(stats.LeafLiveBytes * 10, Is.GreaterThanOrEqualTo((long) stats.LeafPages * FdbLiteGeometry.Default.PageSize * 9), "append-built leaves stay >= 90% packed");
		}

		[Test]
		public void Gate_Trail_Consolidation()
		{
			// the space-amp headline shape: packed seed, then delete-heavy generations under FixedBudget -
			// the merges must fire and the leaf population must come back down
			using var engine = CreateEngine();
			engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.FixedBudget(16);

			var writer = engine.BeginWrite();
			for (long i = 0; i < 40_000; i++)
			{
				writer.Insert(Key(1, i), Value((int) i, 24));
			}
			engine.Commit(writer, 1);
			int seededLeaves = engine.MeasureTreeStatistics().LeafPages;

			ulong version = 2;
			for (int gen = 0; gen < 4; gen++)
			{
				writer = engine.BeginWrite();
				for (long i = gen * 10_000; i < (gen + 1) * 10_000; i++)
				{
					if (i % 5 == 0) { continue; }
					Assert.That(writer.Remove(Key(1, i)), Is.True);
				}
				engine.Commit(writer, version++);
			}

			var stats = engine.MeasureTreeStatistics();
			Log($"# seededLeaves={seededLeaves} leaves={stats.LeafPages} runsMerged={engine.ConsolidationRunsMerged} pagesFreed={engine.ConsolidationPagesFreed} skipped={engine.ConsolidationRunsSkipped}");

			Assert.That(engine.ConsolidationRunsMerged, Is.GreaterThan(0), "the pre-commit arm must fire on the trail shape");
			Assert.That(engine.ConsolidationPagesFreed, Is.GreaterThanOrEqualTo(20), "pages freed floor (baseline 24)");
			Assert.That(stats.LeafPages, Is.LessThanOrEqualTo(seededLeaves / 2), "80% deletion must at least halve the leaf population");

			var pin = engine.BeginRead();
			try
			{
				Assert.That(FdbLiteTreeAudit.Check(engine.Pager, pin.RootPageId), Is.Empty, "the gate rides on audited trees");
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		[Test]
		public void Gate_Churn_Steady_State()
		{
			// uniform churn over a fixed keyspace: splits and leaf population must both plateau
			using var engine = CreateEngine();
			engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.FixedBudget(8);
			var rnd = new Random(20260730);

			ulong version = 1;
			int splitsLastGen = 0;
			for (int gen = 0; gen < 6; gen++)
			{
				var writer = engine.BeginWrite();
				for (int op = 0; op < 10_000; op++)
				{
					long k = rnd.Next(60_000);
					if (rnd.Next(10) < 6)
					{
						writer.Insert(Key(2, k), Value((int) k, rnd.Next(0, 80)));
					}
					else
					{
						writer.Remove(Key(2, k));
					}
				}
				engine.Commit(writer, version++);
				splitsLastGen = writer.LeafSplits;
			}

			var stats = engine.MeasureTreeStatistics();
			Log($"# leaves={stats.LeafPages} splitsLastGen={splitsLastGen} runsMerged={engine.ConsolidationRunsMerged} freed={engine.ConsolidationPagesFreed}");

			Assert.That(stats.LeafPages, Is.LessThanOrEqualTo(100), "churn steady-state leaf budget (baseline 86)");
			Assert.That(splitsLastGen, Is.LessThanOrEqualTo(20), "steady-state split rate budget (baseline 14)");
		}

		[Test]
		public void Gate_Vacuum_Convergence()
		{
			// random load then silence: the vacuum must reclaim the balanced-split gap and CONVERGE
			using var engine = CreateEngine();
			var order = new long[60_000];
			for (long i = 0; i < order.Length; i++) { order[i] = i; }
			var rnd = new Random(99);
			for (int i = order.Length - 1; i > 0; i--)
			{
				int j = rnd.Next(i + 1);
				(order[i], order[j]) = (order[j], order[i]);
			}
			var writer = engine.BeginWrite();
			foreach (long i in order)
			{
				writer.Insert(Key(3, i), Value((int) i, 24));
			}
			engine.Commit(writer, 1);
			int loadedLeaves = engine.MeasureTreeStatistics().LeafPages;

			int steps = 0;
			while (steps < 200 && engine.VacuumStep(16).PagesFreed > 0)
			{
				steps++;
			}

			var stats = engine.MeasureTreeStatistics();
			Log($"# loadedLeaves={loadedLeaves} leaves={stats.LeafPages} steps={steps} vacuumFreed={engine.VacuumPagesFreed}");

			Assert.That(steps, Is.LessThan(200), "the vacuum must converge, never loop");
			Assert.That(engine.VacuumPagesFreed, Is.GreaterThan(0), "the vacuum must reclaim the balanced-split gap");
			Assert.That(stats.LeafPages, Is.LessThan(loadedLeaves), "the leaf population must drop");
			// pairwise/short same-parent runs bound how close to the packed ideal a converged vacuum can get:
			// isolated sparse leaves between packed outputs stay (merging them would free nothing), so the
			// budget pins the measured converged population, not a theoretical ideal
			Assert.That(stats.LeafPages, Is.LessThanOrEqualTo(105), "post-vacuum population budget (baseline 98 from 169 loaded)");
		}

	}

}
