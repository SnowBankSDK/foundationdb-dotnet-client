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
	using FoundationDB.Storage.FdbLite;

	/// <summary>Tests of the space-reclamation train: the aggregate block, the volatility counter, pre-commit consolidation, and the background vacuum.</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteSpaceReclamationFacts : SimpleTest
	{

		private static FdbLiteEngine CreateHeapEngine() => FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

		private static byte[] Key(int i) => System.Text.Encoding.ASCII.GetBytes($"key-{i:D6}");

		private static byte[] Value(int i, int length)
		{
			var v = new byte[length];
			new Random(i).NextBytes(v);
			return v;
		}

		/// <summary>Reads a committed page image through the engine's pager.</summary>
		private static ReadOnlySpan<byte> ReadPage(FdbLiteEngine engine, uint pageId)
			=> engine.Pager.ReadBlocks(pageId, engine.Pager.Geometry.BlocksPerPage);

		private static byte[] Key64(long i)
		{
			var key = new byte[8];
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		/// <summary>Leaves of the current durable generation in key order: page id, numeric key range, and the volatility episode count.</summary>
		private static List<(uint Id, long FirstKey, long LastKey, byte Episodes)> ScanLeafEpisodes(FdbLiteEngine engine)
		{
			var result = new List<(uint, long, long, byte)>();
			foreach (var group in FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId).Groups)
			{
				foreach (var leaf in group)
				{
					result.Add((leaf.PageId, leaf.FirstKey, leaf.LastKey, FdbLitePageHeader.GetVolatilityEpisodes(ReadPage(engine, leaf.PageId))));
				}
			}
			return result;
		}

		private static long TotalEpisodes(FdbLiteEngine engine) => ScanLeafEpisodes(engine).Sum(l => (long) l.Episodes);

		private static byte EpisodesOfLeafCovering(FdbLiteEngine engine, long key)
			=> ScanLeafEpisodes(engine).Single(l => l.FirstKey <= key && key <= l.LastKey).Episodes;

		[Test]
		public void Test_Volatility_Episodes_Follow_The_Ratified_Event_Set()
		{
			using var engine = CreateHeapEngine();
			ulong version = 1;

			// sequential fill: a log page being built is FILLING, not mutating, and must reach its packed
			// state at count zero - this is the write-once shape the whole posture exists to protect
			const long Stride = 16;
			const int N = 3_000;
			var writer = engine.BeginWrite();
			for (long i = 0; i < N; i++)
			{
				writer.Insert(Key64(i * Stride), Value((int) i, 16));
			}
			engine.Commit(writer, version++);

			var leaves = ScanLeafEpisodes(engine);
			Assert.That(leaves.Count, Is.GreaterThanOrEqualTo(3), "the seed must span several leaves or the local/global distinction below is untestable");
			Assert.That(leaves.All(l => l.Episodes == 0), Is.True, "an append-built tree reads zero episodes everywhere");

			// deletes count, once per generation however many land on the page
			writer = engine.BeginWrite();
			Assert.That(writer.Remove(Key64(10 * Stride)), Is.True);
			Assert.That(writer.Remove(Key64(11 * Stride)), Is.True);
			Assert.That(writer.Remove(Key64(12 * Stride)), Is.True);
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(1), "three deletes in one generation are ONE episode");

			// a second mutating generation is a second episode
			writer = engine.BeginWrite();
			Assert.That(writer.Remove(Key64(13 * Stride)), Is.True);
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(2));

			// an in-place value mutation counts (same-length replace, the copy-verbatim first-touch path)
			writer = engine.BeginWrite();
			writer.Insert(Key64(20 * Stride), Value(9999, 16));
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(3), "a replace is a mutation episode");

			// an interior insert counts: the key lands strictly below the receiving leaf's maximum
			writer = engine.BeginWrite();
			writer.Insert(Key64((100 * Stride) + 1), Value(7, 16));
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(4), "an interior insert is a mutation episode");

			// append-edge growth at the TREE's right edge does not count
			long before = TotalEpisodes(engine);
			writer = engine.BeginWrite();
			writer.Insert(Key64(N * Stride), Value(8, 16));
			engine.Commit(writer, version++);
			Assert.That(TotalEpisodes(engine), Is.EqualTo(before), "growing the rightmost leaf is filling, not mutating");

			// THE LOAD-BEARING LOCAL TEST: a key at the right edge of an INTERIOR leaf is that leaf's own
			// append edge, even though it sits far below the tree's global maximum. A store with many
			// append-shaped subspaces has many such edges at once, and a global running-max test would
			// brand every one of them volatile.
			var leftmost = ScanLeafEpisodes(engine)[0];
			long edgeKey = leftmost.LastKey + 1; // above everything in the leaf, below the next leaf's first key
			before = TotalEpisodes(engine);
			writer = engine.BeginWrite();
			writer.Insert(Key64(edgeKey), Value(9, 16));
			engine.Commit(writer, version++);
			Assert.That(TotalEpisodes(engine), Is.EqualTo(before), "a leaf-local append edge does not count, wherever the leaf sits in the tree");

			// the counter saturates instead of wrapping
			for (int g = 0; g < 260; g++)
			{
				writer = engine.BeginWrite();
				if ((g & 1) == 0)
				{
					Assert.That(writer.Remove(Key64(30 * Stride)), Is.True);
				}
				else
				{
					writer.Insert(Key64(30 * Stride), Value(g, 16)); // re-inserting interior: an episode either way
				}
				engine.Commit(writer, version++);
			}
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(255), "the u8 saturates, never wraps");

			// split parts inherit the source page's history: a rebuild of ONE page is that page's own
			// continued life, not a repack into a new one
			writer = engine.BeginWrite();
			for (long j = 40; j < 140; j++)
			{
				for (long s = 1; s < 16; s += 2)
				{
					writer.Insert(Key64((j * Stride) + s), Value((int) (j * 100 + s), 16));
				}
			}
			engine.Commit(writer, version++);
			var saturatedRegion = ScanLeafEpisodes(engine).Where(l => l.FirstKey < 140 * Stride).ToList();
			Assert.That(saturatedRegion.Count, Is.GreaterThanOrEqualTo(2), "the pumped region must have split");
			Assert.That(saturatedRegion.All(l => l.Episodes == 255), Is.True, "every part of a split volatile page carries its history");
		}

		/// <summary>Audits structure + aggregates and verifies every model key reads back exactly.</summary>
		private static void AssertTreeMatchesModel(FdbLiteEngine engine, SortedDictionary<long, byte[]> model, string phase)
		{
			var pin = engine.BeginRead();
			try
			{
				Assert.That(FdbLiteTreeAudit.Check(engine.Pager, pin.RootPageId), Is.Empty, $"{phase}: audit");
				Assert.That(pin.KeyCount, Is.EqualTo((ulong) model.Count), $"{phase}: key count");
				foreach (var kv in model)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, pin.RootPageId, Key64(kv.Key), out var v), Is.True, $"{phase}: key {kv.Key} missing");
					if (!v.SequenceEqual(kv.Value))
					{
						Assert.Fail($"{phase}: key {kv.Key} value mismatch ({v.Length} vs {kv.Value.Length} B)");
					}
				}
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		[Test]
		public void Test_PreCommit_Consolidation_Merges_Underflow_Runs()
		{
			// the trail shape: a packed store whose region [1000, 5000) loses 80% of its keys, leaving a run
			// of adjacent under-full leaves that the pre-commit arm must merge - correctly, measurably, and
			// with reset-on-repack semantics on the outputs
			const int N = 8_000;
			var deleted = new List<long>();

			(FdbLiteEngine Engine, SortedDictionary<long, byte[]> Model, FdbLiteTreeWriter LastWriter) Run(FdbLitePreCommitConsolidation policy)
			{
				var engine = CreateHeapEngine();
				engine.PreCommitConsolidation = policy;
				var model = new SortedDictionary<long, byte[]>();

				var writer = engine.BeginWrite();
				for (long i = 0; i < N; i++)
				{
					var v = Value((int) i, 24);
					writer.Insert(Key64(i), v);
					model[i] = v;
				}
				var big1 = Value(70001, 60_000); // extent values inside the churned range: they must ride the merge untouched
				var big2 = Value(70002, 60_000);
				writer.Insert(Key64(2_000), big1);
				model[2_000] = big1;
				writer.Insert(Key64(3_000), big2);
				model[3_000] = big2;
				engine.Commit(writer, 1);

				writer = engine.BeginWrite();
				deleted.Clear();
				for (long i = 1_000; i < 5_000; i++)
				{
					if (i % 5 == 0 || i is 2_000 or 3_000) { continue; } // keep every 5th, and the extents
					Assert.That(writer.Remove(Key64(i)), Is.True);
					model.Remove(i);
					deleted.Add(i);
				}
				engine.Commit(writer, 2);
				return (engine, model, writer);
			}

			var (control, controlModel, controlWriter) = Run(FdbLitePreCommitConsolidation.Off);
			using var _c = control;
			Assert.That(controlWriter.ConsolidationRunsMerged, Is.Zero, "Off must not merge");

			var (engine, model, writer) = Run(FdbLitePreCommitConsolidation.FixedBudget(16));
			using var _e = engine;

			// the mechanism fired - a merge that silently never engages is indistinguishable from a correct
			// one by content alone, so the counters are load-bearing assertions, not diagnostics
			Log($"# merged runs={writer.ConsolidationRunsMerged} pagesFreed={writer.ConsolidationPagesFreed} coldPulled={writer.ConsolidationColdPagesPulled}");
			Assert.That(writer.ConsolidationRunsMerged, Is.GreaterThan(0), "the arm must have merged at least one run");
			Assert.That(writer.ConsolidationPagesFreed, Is.GreaterThan(0), "merging must have freed pages");

			// keys and values are intact, structure and aggregates audit clean
			AssertTreeMatchesModel(engine, model, "after consolidation");
			Assert.That(model.Keys.ToList(), Is.EqualTo(controlModel.Keys.ToList()).AsCollection, "both arms saw identical operations");

			// fewer leaves than the identical workload without the arm
			var mergedStats = engine.MeasureTreeStatistics();
			var controlStats = control.MeasureTreeStatistics();
			Log($"# leaves: consolidated={mergedStats.LeafPages} control={controlStats.LeafPages}");
			Assert.That(mergedStats.LeafPages, Is.LessThan(controlStats.LeafPages), "consolidation must reduce the leaf population");

			// reset-on-repack, both ways: a merged output restarts at zero episodes, while the same shape
			// left unmerged keeps the delete episode it was branded with
			var mergedRegion = ScanLeafEpisodes(engine).Where(l => l.LastKey >= 1_000 && l.FirstKey < 5_000).ToList();
			var controlRegion = ScanLeafEpisodes(control).Where(l => l.LastKey >= 1_000 && l.FirstKey < 5_000).ToList();
			Assert.That(mergedRegion.Count(l => l.Episodes == 0), Is.GreaterThan(0), "merged outputs restart at zero (reset-on-repack)");
			Assert.That(controlRegion.All(l => l.Episodes >= 1), Is.True, "without a repack the delete episode stays");

			// the hysteresis MECHANISM: a class-1 run (its inputs carry the delete episode) packs to 0.90,
			// never to capacity, so every merged output must sit at or under the ceiling with real headroom
			int pageSize = engine.Pager.Geometry.PageSize;
			long ceiling = (pageSize * 9L) / 10;
			foreach (var group in FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId).Groups)
			{
				foreach (var leaf in group.Where(l => l.LastKey >= 1_000 && l.FirstKey < 5_000 && l.FirstKey > 0 && FdbLitePageHeader.GetVolatilityEpisodes(ReadPage(engine, l.PageId)) == 0))
				{
					Log($"# merged output {leaf.PageId}: [{leaf.FirstKey}..{leaf.LastKey}] cells={leaf.CellCount} live={leaf.LiveBytes}");
					Assert.That(leaf.LiveBytes, Is.LessThanOrEqualTo(ceiling), $"a merged class-1 output must honor the 0.90 fill target (page {leaf.PageId})");
				}
			}

			// hysteresis: the workload continues with mutations inside the headroom the fill target promised
			// (up to ~10% of each merged page), and the merged run must not immediately re-split - the
			// measured bar is 0%; a refill LARGER than the promised headroom is regrowth, and regrowth
			// splitting is the tree working as designed, not oscillation
			var reinsert = deleted.Where((_, i) => i % 40 == 0).ToList();
			var w2 = engine.BeginWrite();
			foreach (var i in reinsert)
			{
				var v = Value((int) i, 24);
				w2.Insert(Key64(i), v);
				model[i] = v;
			}
			engine.Commit(w2, 3);
			Assert.That(w2.LeafSplits, Is.Zero, $"re-inserting {reinsert.Count} keys into the merged region must not re-split it");
			AssertTreeMatchesModel(engine, model, "after hysteresis reinserts");

			// the extents rode the merge: still readable (checked above), and still deletable exactly once
			var w3 = engine.BeginWrite();
			Assert.That(w3.Remove(Key64(2_000)), Is.True);
			model.Remove(2_000);
			engine.Commit(w3, 4);
			AssertTreeMatchesModel(engine, model, "after removing a merged extent");
		}

		[Test]
		public void Test_Consolidation_Pulls_A_Cold_Sparse_Neighbor()
		{
			// commit 2 leaves a sparse COMMITTED (cold) region behind with consolidation off; commit 3 then
			// shrinks the adjacent region with the arm on, and the merge may pull at most one cold leaf per
			// run edge - as measured, the write count DROPS while an extra page frees per ~one cold read
			using var engine = CreateHeapEngine();
			var model = new SortedDictionary<long, byte[]>();

			const int N = 6_000;
			var writer = engine.BeginWrite();
			for (long i = 0; i < N; i++)
			{
				var v = Value((int) i, 24);
				writer.Insert(Key64(i), v);
				model[i] = v;
			}
			engine.Commit(writer, 1);

			// make [2000, 3000) sparse while the arm is off: these leaves commit cold and sparse
			writer = engine.BeginWrite();
			for (long i = 2_000; i < 3_000; i++)
			{
				if (i % 5 == 0) { continue; }
				Assert.That(writer.Remove(Key64(i)), Is.True);
				model.Remove(i);
			}
			engine.Commit(writer, 2);
			Assert.That(writer.ConsolidationRunsMerged, Is.Zero);

			// now shrink the adjacent region with the arm on: runs ending at the cold boundary can extend
			engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.FixedBudget(16);
			writer = engine.BeginWrite();
			for (long i = 3_000; i < 4_000; i++)
			{
				if (i % 5 == 0) { continue; }
				Assert.That(writer.Remove(Key64(i)), Is.True);
				model.Remove(i);
			}
			engine.Commit(writer, 3);

			Log($"# merged runs={writer.ConsolidationRunsMerged} pagesFreed={writer.ConsolidationPagesFreed} coldPulled={writer.ConsolidationColdPagesPulled}");
			Assert.That(writer.ConsolidationRunsMerged, Is.GreaterThan(0), "the arm must have merged");
			Assert.That(writer.ConsolidationColdPagesPulled, Is.GreaterThan(0), "a cold sparse neighbor must have been pulled into a merge");
			AssertTreeMatchesModel(engine, model, "after cold-neighbor consolidation");
		}

		[Test]
		public void Test_Consolidation_Is_Deterministic_Run_To_Run()
		{
			// the emulator posture: Off produces identical trees trivially, and FixedBudget must too - the
			// candidate order is deterministic and no wall clock is consulted anywhere on that path
			static (FdbLiteEngine Engine, long Merged) Run(FdbLitePreCommitConsolidation policy)
			{
				var engine = CreateHeapEngine();
				engine.PreCommitConsolidation = policy;
				var rnd = new Random(555);
				long merged = 0;
				ulong version = 1;
				for (int round = 0; round < 6; round++)
				{
					var w = engine.BeginWrite();
					for (int op = 0; op < 3_000; op++)
					{
						long i = rnd.Next(10_000);
						if (rnd.Next(10) < 6)
						{
							w.Insert(Key64(i), Value((int) i ^ round, rnd.Next(0, 80)));
						}
						else
						{
							w.Remove(Key64(i));
						}
					}
					engine.Commit(w, version++);
					merged += w.ConsolidationRunsMerged;
				}
				return (engine, merged);
			}

			foreach (var policy in new[] { FdbLitePreCommitConsolidation.Off, FdbLitePreCommitConsolidation.FixedBudget(4) })
			{
				var (a, mergedA) = Run(policy);
				var (b, mergedB) = Run(policy);
				using var _a = a;
				using var _b = b;

				Assert.That(mergedA, Is.EqualTo(mergedB), $"[{policy}] merge counts must match run to run");
				Assert.That(b.Durable.RootPageId, Is.EqualTo(a.Durable.RootPageId), $"[{policy}] root page id");
				Assert.That(b.Durable.KeyCount, Is.EqualTo(a.Durable.KeyCount), $"[{policy}] key count");
				Assert.That(b.MeasureTreeStatistics(), Is.EqualTo(a.MeasureTreeStatistics()), $"[{policy}] statistics");

				// byte-for-byte: every reachable page of the final generation is identical
				var leavesA = FdbLiteLeafAnalysis.Snapshot(a.Pager, a.Durable.RootPageId).LeafIds.OrderBy(x => x).ToList();
				var leavesB = FdbLiteLeafAnalysis.Snapshot(b.Pager, b.Durable.RootPageId).LeafIds.OrderBy(x => x).ToList();
				Assert.That(leavesB, Is.EqualTo(leavesA).AsCollection, $"[{policy}] leaf id sets");
				foreach (var id in leavesA)
				{
					if (!a.Pager.ReadBlocks(id, a.Pager.Geometry.BlocksPerPage).SequenceEqual(b.Pager.ReadBlocks(id, b.Pager.Geometry.BlocksPerPage)))
					{
						Assert.Fail($"[{policy}] leaf {id} differs between two identical runs");
					}
				}
			}
		}

		[Test]
		public void Test_Vacuum_Reclaims_The_Trail_Shape()
		{
			// the whole point of the arm: sparse COMMITTED leaves (the trail's cold holes), never noted by
			// anyone, found by occupancy alone - including everything a pre-commit budget would have skipped
			static (FdbLiteEngine Engine, SortedDictionary<long, byte[]> Model, List<FdbLiteTreeWriter.VacuumOutcome> Steps) Run()
			{
				var engine = CreateHeapEngine(); // Off: nothing consolidates at commit
				var model = new SortedDictionary<long, byte[]>();
				var writer = engine.BeginWrite();
				for (long i = 0; i < 8_000; i++)
				{
					var v = Value((int) i, 24);
					writer.Insert(Key64(i), v);
					model[i] = v;
				}
				engine.Commit(writer, 1);

				writer = engine.BeginWrite();
				for (long i = 1_000; i < 6_000; i++)
				{
					if (i % 5 == 0) { continue; }
					writer.Remove(Key64(i));
					model.Remove(i);
				}
				engine.Commit(writer, 2);

				var steps = new List<FdbLiteTreeWriter.VacuumOutcome>();
				for (int s = 0; s < 50; s++)
				{
					var outcome = engine.VacuumStep(16);
					if (outcome.PagesFreed == 0) { break; }
					steps.Add(outcome);
				}
				return (engine, model, steps);
			}

			var (engine, model, steps) = Run();
			using var _e = engine;

			Assert.That(steps.Count, Is.GreaterThan(0), "the vacuum must have found the sparse region");
			Assert.That(steps.Count, Is.LessThan(50), "the vacuum must CONVERGE: repacked output is not a candidate again");
			Assert.That(engine.VacuumPagesFreed, Is.GreaterThan(0), "the engine counter records the executed steps");
			Log($"# vacuum: {steps.Count} steps, {engine.VacuumPagesFreed} pages freed");

			Assert.That(engine.Durable.DatabaseVersion, Is.EqualTo(2), "a maintenance generation publishes NO new database version");
			AssertTreeMatchesModel(engine, model, "after vacuum");

			// a maintenance generation is deterministic: the identical scenario lands byte-identical
			var (second, _, steps2) = Run();
			using var _s = second;
			Assert.That(steps2.Count, Is.EqualTo(steps.Count), "step count is deterministic");
			Assert.That(second.Durable.RootPageId, Is.EqualTo(engine.Durable.RootPageId), "identical scenarios produce identical trees");
			Assert.That(second.MeasureTreeStatistics(), Is.EqualTo(engine.MeasureTreeStatistics()));
		}

		[Test]
		public void Test_Vacuum_Consolidates_Across_A_Parent_Boundary()
		{
			// long keys shrink the fanout (~16 children per internal page at 16 KiB), so a 400-key store
			// already spans several leaf-parents - the shape the cross-parent scope exists for
			static byte[] LongKey(long i)
			{
				var key = new byte[1_000];
				System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, i);
				new Random((int) i).NextBytes(key.AsSpan(8));
				return key;
			}

			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Uniform(14)));
			var model = new SortedDictionary<long, byte[]>();
			const int N = 400;
			var writer = engine.BeginWrite();
			for (long i = 0; i < N; i++)
			{
				var v = Value((int) i, 8);
				writer.Insert(LongKey(i), v);
				model[i] = v;
			}
			engine.Commit(writer, 1);
			var stats = engine.MeasureTreeStatistics();
			Assert.That(stats.InternalPages, Is.GreaterThanOrEqualTo(3), "the seed must span several leaf-parents (root + at least two), or this test proves nothing");

			// find where the FIRST leaf-parent ends (raw parse, first 8 key bytes carry the ordinal): the
			// band must hollow out exactly its tail plus the next parent's head, so the first parent is the
			// worst region AND its trailing sparse stretch meets a sparse neighbor head across the boundary
			long boundaryOrdinal;
			{
				var root = engine.Pager.ReadBlocks(engine.Durable.RootPageId, engine.Pager.Geometry.BlocksPerPage);
				uint p1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(root[128..]);
				var parent1 = engine.Pager.ReadBlocks(p1, engine.Pager.Geometry.BlocksPerPage);
				int cells = FdbLitePageHeader.GetCellCount(parent1);
				int lastOff = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(parent1[(132 + ((cells - 1) * 2))..]);
				uint lastLeaf = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(parent1[lastOff..]);
				var leaf = engine.Pager.ReadBlocks(lastLeaf, engine.Pager.Geometry.BlocksPerPage);
				int prefixLen = FdbLitePageHeader.GetPrefixLength(leaf);
				int leafCells = FdbLitePageHeader.GetCellCount(leaf);
				int slotsAt = 128 + ((prefixLen + 1) & ~1);
				// the directory reserves slots ahead of the cell count, so the key heap starts after the RESERVED span
				int keyBase = slotsAt + (Math.Max(FdbLitePageHeader.GetSlotCapacity(leaf), leafCells) * 2);
				Span<byte> first8 = stackalloc byte[8];
				leaf.Slice(128, Math.Min(prefixLen, 8)).CopyTo(first8);
				int entry = keyBase + System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(leaf[(slotsAt + ((leafCells - 1) * 2))..]);
				if (prefixLen < 8)
				{
					leaf.Slice(entry + 2, 8 - prefixLen).CopyTo(first8[prefixLen..]);
				}
				boundaryOrdinal = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(first8);
			}
			Log($"# first leaf-parent ends at ordinal {boundaryOrdinal}");
			Assert.That(boundaryOrdinal, Is.GreaterThan(30).And.LessThan(N - 30), "the derived boundary must leave dense flanks on both sides");

			// hollow out the first parent's tail and the neighbor's head, keeping the outermost leaves dense
			writer = engine.BeginWrite();
			for (long i = 20; i < boundaryOrdinal + 25; i++)
			{
				if (i % 5 == 0) { continue; }
				Assert.That(writer.Remove(LongKey(i)), Is.True);
				model.Remove(i);
			}
			engine.Commit(writer, 2);

			bool crossed = false;
			int freed = 0;
			for (int s = 0; s < 50; s++)
			{
				var outcome = engine.VacuumStep(16);
				if (outcome.PagesFreed == 0) { break; }
				crossed |= outcome.CrossedParentBoundary;
				freed += outcome.PagesFreed;
				Log($"# step {s}: in={outcome.InputPages} out={outcome.OutputPages} crossed={outcome.CrossedParentBoundary}");
			}
			Assert.That(freed, Is.GreaterThan(0), "the vacuum must reclaim the hollow band");
			Assert.That(crossed, Is.True, "at least one step must consolidate across a leaf-parent boundary");

			// every key reads back through the moved separators, and the audit (bounds, order, aggregates) is silent
			var pin = engine.BeginRead();
			try
			{
				Assert.That(FdbLiteTreeAudit.Check(engine.Pager, pin.RootPageId), Is.Empty, "audit after cross-parent surgery");
				Assert.That(pin.KeyCount, Is.EqualTo((ulong) model.Count));
				foreach (var kv in model)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, pin.RootPageId, LongKey(kv.Key), out var v), Is.True, $"key {kv.Key} missing after the boundary moved");
					Assert.That(v.SequenceEqual(kv.Value), Is.True, $"key {kv.Key} value");
				}
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		[Test]
		public void Test_Vacuum_Packs_WriteOnce_Data_Full()
		{
			// the population-asymmetry posture's payoff: a single-generation random load builds its pages and
			// mutates them in the SAME generation, so every page reads class 0 (write-once) while balanced
			// splits leave them ~half full - and the vacuum must dare pack that to 1.00, not to 0.90
			using var engine = CreateHeapEngine();
			var model = new SortedDictionary<long, byte[]>();
			var order = Enumerable.Range(0, 8_000).Select(i => (long) i).ToArray();
			var rnd = new Random(99);
			for (int i = order.Length - 1; i > 0; i--)
			{
				int j = rnd.Next(i + 1);
				(order[i], order[j]) = (order[j], order[i]);
			}
			var writer = engine.BeginWrite();
			foreach (long i in order)
			{
				var v = Value((int) i, 24);
				writer.Insert(Key64(i), v);
				model[i] = v;
			}
			engine.Commit(writer, 1);

			var leaves = ScanLeafEpisodes(engine);
			Assert.That(leaves.All(l => l.Episodes == 0), Is.True, "a one-generation load reads class 0 everywhere (its own interior inserts dedup against the creating generation)");
			var before = engine.MeasureTreeStatistics();
			Assert.That(before.FreeGapBytes, Is.GreaterThan(0), "balanced splits must have left gap, or there is nothing to reclaim");

			int freed = 0;
			for (int s = 0; s < 60; s++)
			{
				var outcome = engine.VacuumStep(16);
				if (outcome.PagesFreed == 0) { break; }
				freed += outcome.PagesFreed;
			}
			Assert.That(freed, Is.GreaterThan(0), "the load's half-full pages are reclaimable");

			// the discriminator: at least one repacked leaf sits ABOVE the 0.90 ceiling, which only the
			// class-0 pack-to-1.00 target can produce
			int pageSize = engine.Pager.Geometry.PageSize;
			var after = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId).Groups.SelectMany(g => g).ToList();
			Assert.That(after.Any(l => l.LiveBytes > (pageSize * 95L) / 100), Is.True, "write-once data must pack past the volatile ceiling");

			AssertTreeMatchesModel(engine, model, "after packing the write-once load");
		}

		[Test]
		public void Test_Root_Aggregates_Are_Exact_Against_The_Model()
		{
			// The root page's aggregate block claims the tree-wide totals in O(1); the MODEL is the oracle
			// (nothing derived from the pages under test), and the walk-based statistics cross-check the leaf
			// counts. Checked after EVERY commit: exactness across generations is the dirty-chain invariant's
			// whole claim, and a clean subtree whose stored sums drifted is precisely what one final check
			// at the end would miss attributing.
			using var engine = CreateHeapEngine();
			var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
			var rnd = new Random(20260730);

			void CheckAggregates(string phase)
			{
				var agg = engine.GetTreeAggregates();
				long keyBytes = model.Keys.Sum(k => (long) k.Length);
				long valueBytes = model.Values.Sum(v => v.LongLength);
				Assert.That(agg.EntryCount, Is.EqualTo((ulong) model.Count), $"{phase}: entry count");
				Assert.That(agg.LogicalKeyBytes, Is.EqualTo((ulong) keyBytes), $"{phase}: logical key bytes");
				Assert.That(agg.LogicalValueBytes, Is.EqualTo((ulong) valueBytes), $"{phase}: logical value bytes");

				var stats = engine.MeasureTreeStatistics();
				Assert.That(agg.LeafCount, Is.EqualTo((uint) stats.LeafPages), $"{phase}: leaf count");
				Assert.That(agg.LeafLiveBytes, Is.EqualTo((ulong) stats.LeafLiveBytes), $"{phase}: leaf live bytes");
			}

			void Insert(FdbLiteTreeWriter w, string key, byte[] value)
			{
				w.Insert(System.Text.Encoding.ASCII.GetBytes(key), value);
				model[key] = value;
			}

			// generation 1: enough sorted+random keys to build a multi-level tree, plus one extent value
			ulong version = 1;
			var writer = engine.BeginWrite();
			for (int i = 0; i < 6_000; i++)
			{
				Insert(writer, $"key-{i:D6}", Value(i, rnd.Next(0, 64)));
			}
			Insert(writer, "big-blob", Value(777, 100_000)); // out-of-line extent: logical bytes count its CONTENT
			engine.Commit(writer, version++);
			CheckAggregates("after bulk load");

			// generation 2: shrinks, grows, same-length replaces (the in-place paths)
			writer = engine.BeginWrite();
			for (int i = 0; i < 6_000; i += 3)
			{
				Insert(writer, $"key-{i:D6}", Value(i + 9000, rnd.Next(3) switch { 0 => 4, 1 => 32, _ => 150 }));
			}
			engine.Commit(writer, version++);
			CheckAggregates("after replace churn");

			// generation 3: deletes (point + range), including the extent
			writer = engine.BeginWrite();
			for (int i = 1; i < 6_000; i += 5)
			{
				string k = $"key-{i:D6}";
				Assert.That(writer.Remove(System.Text.Encoding.ASCII.GetBytes(k)), Is.True);
				model.Remove(k);
			}
			int removed = writer.RemoveRange("key-002000"u8, "key-002500"u8);
			var dead = model.Keys.Where(k => string.CompareOrdinal(k, "key-002000") >= 0 && string.CompareOrdinal(k, "key-002500") < 0).ToList();
			Assert.That(removed, Is.EqualTo(dead.Count));
			foreach (var k in dead) { model.Remove(k); }
			Assert.That(writer.Remove("big-blob"u8), Is.True);
			model.Remove("big-blob");
			engine.Commit(writer, version++);
			CheckAggregates("after deletes");

			// generation 4: a small touch, so clean subtrees from earlier generations carry their sums across
			writer = engine.BeginWrite();
			Insert(writer, "zz-last", Value(1, 10));
			engine.Commit(writer, version++);
			CheckAggregates("after a one-key generation");

			// and the structural audit stays silent, which now includes per-page aggregate validation
			var pin = engine.BeginRead();
			try
			{
				Assert.That(FdbLiteTreeAudit.Check(engine.Pager, pin.RootPageId), Is.Empty, "audit (incl. aggregate recount) must be silent");
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		[Test]
		public void Test_Seal_Restamps_The_Generation_Of_Verbatim_Copies()
		{
			// The copy-verbatim replace path duplicates a committed page image and mutates one value in the
			// copy, so the copy carries the SOURCE generation's stamp. The stamp is diagnostic (an inspector
			// uses it to detect a page reused under its feet), and a page published by generation N stamped
			// N-1 sends any such diagnosis to the wrong generation. Seal is the one point every dirty image
			// passes through exactly once, so the stamp is corrected there.
			using var engine = CreateHeapEngine();

			var writer = engine.BeginWrite();
			for (int i = 0; i < 3; i++)
			{
				writer.Insert(Key(i), Value(i, 32));
			}
			engine.Commit(writer, databaseVersion: 1);

			// same-length replace of a committed value: the first touch of the page takes the copy-verbatim path
			writer = engine.BeginWrite();
			ulong writeGeneration = writer.Generation;
			writer.Insert(Key(1), Value(1000, 32));
			Assert.That(writer.CellsOverwritten, Is.EqualTo(1), "the replace must take the in-place overwrite path (copy-verbatim on first touch), or this test no longer exercises the stamp");
			engine.Commit(writer, databaseVersion: 2);

			var leaf = ReadPage(engine, engine.Durable.RootPageId);
			Assert.That(FdbLitePageHeader.GetPageType(leaf), Is.EqualTo(FdbLitePageType.Leaf), "a 3-key store is a single-leaf tree");
			Assert.That(FdbLitePageHeader.GetGeneration(leaf), Is.EqualTo(writeGeneration), "a page published by a generation must carry that generation's stamp");
		}

	}

}
