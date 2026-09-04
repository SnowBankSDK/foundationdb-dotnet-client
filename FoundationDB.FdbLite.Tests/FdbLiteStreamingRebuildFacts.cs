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
	using FoundationDB.FdbLite;

	/// <summary>Regression net for the streaming rebuild (the engine's only rebuild path since the materialized <c>CellRef[]</c> twin was deleted): every workload runs TWICE and the two stores must be BYTE-IDENTICAL.</summary>
	/// <remarks>The twin-run comparison pins determinism and is the detector for the uninitialized-buffer contract (a page image read before being written whole shows up as run-to-run divergence). The counters are the execution proof per site, and the workloads themselves carry the writer's contract tripwires (heap-crossing, split-of-a-shrink) through every rebuild shape: splits, strips, K-way giants, internal splits, vacuum merges and the cross-parent join.</remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteStreamingRebuildFacts : SimpleTest
	{

		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) CreateStore(FdbLiteGeometry geometry)
		{
			var pager = new FdbLiteHeapPager(geometry);
			var allocator = new FdbLiteBlockAllocator(pager, new FdbLiteFreeSpaceMap(), frontier: 3);
			var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
			return (pager, writer);
		}

		/// <summary>Split-heavy mixed workload: bucketed random keys (shared prefixes, interior inserts), replaces of both growing and shrinking sizes, and a sprinkle of extent values.</summary>
		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) RunMixedWorkload()
		{
			var geometry = FdbLiteGeometry.Uniform(14); // 16 KiB pages: the floor, so splits come fast
			var (pager, writer) = CreateStore(geometry);

			// all randomness flows from one seeded generator, in one call order: both configurations replay
			// the exact same byte sequences, so any store divergence is the writer's doing
			var rnd = new Random(4271);

			var keys = new List<byte[]>(5_000);
			var key = new byte[24];
			var value = new byte[512];
			for (int i = 0; i < 5_000; i++)
			{
				rnd.NextBytes(key);
				// bucketed: a 3-byte bucket prefix shared by ~hundreds of keys, so leaves get a real shared
				// prefix to strip and the strip/re-strip paths run, not just the plain split
				key[0] = 0x42;
				key[1] = (byte) (i % 16);
				key[2] = (byte) rnd.Next(4);
				keys.Add(key.ToArray());

				int valueLength = rnd.Next(1, 200);
				rnd.NextBytes(value.AsSpan(0, valueLength));
				writer.Insert(keys[i], value.AsSpan(0, valueLength));
			}

			// replaces: interior, cannot always be done in place, so they route through the rebuild;
			// alternate growing and shrinking so both in-place and rebuild variants run
			for (int i = 0; i < keys.Count; i += 7)
			{
				int valueLength = (i % 14 == 0) ? rnd.Next(200, 500) : rnd.Next(1, 20);
				rnd.NextBytes(value.AsSpan(0, valueLength));
				writer.Insert(keys[i], value.AsSpan(0, valueLength));
			}

			// extent values (above MaxInlineValueLength = PageSize/4 = 4 KiB): the injected cell is a
			// descriptor carrying FlagValueIsExtent, which the rebuild must preserve
			var big = new byte[6_000];
			for (int i = 3; i < keys.Count; i += 501)
			{
				rnd.NextBytes(big);
				writer.Insert(keys[i], big);
			}

			writer.FlushDirtyPages();
			return (pager, writer);
		}

		/// <summary>Sequential ASCII keys packed to 100% by the append-avoid path, then interior replaces of varying size rebuilding those packed pages. The shape that caught the fused fast path's fit accounting when the default flipped on (found by the aggregates suite at the default 32 KiB geometry).</summary>
		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) RunSequentialPackedWorkload()
		{
			var (pager, writer) = CreateStore(FdbLiteGeometry.Default);

			var rnd = new Random(20260730);
			var value = new byte[128];
			for (int i = 0; i < 6_000; i++)
			{
				int len = rnd.Next(0, 64);
				rnd.NextBytes(value.AsSpan(0, len));
				writer.Insert(System.Text.Encoding.ASCII.GetBytes($"key-{i:D6}"), value.AsSpan(0, len));
			}
			// an interior key sharing NO prefix with the packed run: the destination prefix collapses to zero,
			// every stored suffix re-expands, and the rebuild aborts DEEP into the page with many cells still
			// to come - the shape that exposed the up-front slot-directory charge in the fused fit accounting
			writer.Insert(System.Text.Encoding.ASCII.GetBytes("big-blob"), value.AsSpan(0, 64));

			for (int i = 0; i < 6_000; i += 3)
			{
				int len = rnd.Next(0, 100);
				rnd.NextBytes(value.AsSpan(0, len));
				writer.Insert(System.Text.Encoding.ASCII.GetBytes($"key-{i:D6}"), value.AsSpan(0, len));
			}

			writer.FlushDirtyPages();
			return (pager, writer);
		}

		[Test]
		public void Streaming_Rebuild_Matches_Materialized_On_Sequential_Packed_Pages()
		{
			var baseline = RunSequentialPackedWorkload();
			var streamed = RunSequentialPackedWorkload();
			AssertStoresIdentical(baseline, streamed);
		}

		/// <summary>Giant-cell workload: maximum-size keys and near-inline-ceiling values, so a page holds very few cells and the K-way (no legal 2-way cut) split branch is exercised.</summary>
		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) RunGiantCellWorkload()
		{
			var geometry = FdbLiteGeometry.Uniform(14);
			var (pager, writer) = CreateStore(geometry);

			var rnd = new Random(90125);
			var key = new byte[FdbLiteTreePage.MaxKeyLength];
			var value = new byte[geometry.MaxInlineValueLength];
			for (int i = 0; i < 64; i++)
			{
				rnd.NextBytes(key);
				key[0] = 0x33; // one bucket: every key shares a byte, so the prefix machinery is not idle
				rnd.NextBytes(value);
				writer.Insert(key, value);
			}

			writer.FlushDirtyPages();
			return (pager, writer);
		}

		private static void AssertStoresIdentical(
			(FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) baseline,
			(FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) streamed)
		{
			// execution proof FIRST: a workload that stopped rebuilding would make the comparison vacuous
			Assert.That(streamed.Writer.StreamedLeafRebuilds, Is.GreaterThan(0), "the workload no longer reaches the rebuild path, so the comparison proves nothing");

			Assert.That(streamed.Writer.Root, Is.EqualTo(baseline.Writer.Root), "the two writers placed their roots differently");
			Assert.That(streamed.Pager.BlockCount, Is.EqualTo(baseline.Pager.BlockCount), "the two stores allocated different amounts");

			for (uint block = 0; block < baseline.Pager.BlockCount; block++)
			{
				var expected = baseline.Pager.ReadBlocks(block, 1);
				var actual = streamed.Pager.ReadBlocks(block, 1);
				if (!expected.SequenceEqual(actual))
				{
					int offset = expected.CommonPrefixLength(actual);
					Assert.Fail($"stores diverge at block {block}, first differing byte at offset {offset} (baseline={expected[offset]:X2}, streamed={actual[offset]:X2})");
				}
			}
		}

		[Test]
		public void Streaming_Rebuild_Matches_Materialized_On_Mixed_Workload()
		{
			var baseline = RunMixedWorkload();
			var streamed = RunMixedWorkload();
			AssertStoresIdentical(baseline, streamed);

			// the single-pass fast path must carry the non-splitting majority AND the split fallback must still
			// run: byte-identical output makes either regression invisible without the counters
			Assert.That(streamed.Writer.StreamedSinglePassRebuilds, Is.GreaterThan(0), "no rebuild took the single-pass fast path: it is dead or its fit accounting always aborts");
			Assert.That(streamed.Writer.StreamedSinglePassRebuilds, Is.LessThan(streamed.Writer.StreamedLeafRebuilds), "every rebuild took the fast path, so the split fallback never ran and this workload no longer covers it");

			// the strip site must stream too: strips happen on this workload (bucketed keys share prefixes),
			// and a strip that silently kept materializing would be invisible in the byte comparison
			Assert.That(streamed.Writer.PagesStripped, Is.GreaterThan(0), "the workload no longer strips any page, so the streamed strip is untested");
			Assert.That(streamed.Writer.StreamedStrips, Is.GreaterThan(0), "no strip took the streaming path: the strip site is dead code under the toggle");

			// internal rebuilds must stream too (every leaf split rebuilds its parent through RebuildInternal)
			Assert.That(streamed.Writer.StreamedInternalRebuilds, Is.GreaterThan(0), "no internal rebuild took the streaming path: the RebuildInternal site is dead code under the toggle");
			Assert.That(streamed.Writer.StreamedInternalSinglePass, Is.GreaterThan(0), "no internal rebuild completed in the fused single pass: the fast path is dead or always aborts");

			// growing the tree's depth builds a root level; the workload splits the root leaf early on
			Assert.That(streamed.Writer.StreamedRootBuilds, Is.GreaterThan(0), "no root-level build took the streaming path: the BuildRootLevel site is dead code under the toggle");
		}

		/// <summary>Delete-heavy build followed by vacuum-until-dry: the only deterministic driver of the replace-run / drop-leading / join family (pre-commit consolidation budgets on wall-clock EMA, so it cannot be byte-compared). Fat keys shrink the internal fan-out, so the tree has several leaf-parents and the cross-parent merge (drop-leading + join) is reachable.</summary>
		private static (FdbLiteEngine Engine, FdbLiteTreeWriter LastWriter) RunVacuumWorkload(int maxRounds = 200)
		{
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Uniform(14)));

			var rnd = new Random(618034);
			var key = new byte[400];
			var value = new byte[100];
			var keys = new List<byte[]>(2_500);
			ulong generation = 0;

			{ // build: ~2,500 fat-keyed rows over several leaf-parents
				var w = engine.BeginWrite();
				for (int i = 0; i < 2_500; i++)
				{
					rnd.NextBytes(key);
					key[0] = 0x55;
					keys.Add(key.ToArray());
					int valueLength = rnd.Next(1, 100);
					rnd.NextBytes(value.AsSpan(0, valueLength));
					w.Insert(keys[i], value.AsSpan(0, valueLength));
				}
				engine.Commit(w, ++generation);
			}

			{ // hollow out two NARROW bands of the sorted key space, aimed at the expected leaf-parent
			  // boundaries (~1/3 and ~2/3 of the leaf order). A cross-parent merge is only taken when it
			  // strictly BEATS the best same-parent run, so the sparse stretch must straddle a boundary and
			  // stay short enough that neither same-parent half can match the joined run - blanket sparseness
			  // makes every same-parent run tie the cross one and the join never fires.
				var rank = new int[keys.Count];
				{
					var sorted = Enumerable.Range(0, keys.Count).OrderBy(i => keys[i], Comparer<byte[]>.Create(static (a, b) => a.AsSpan().SequenceCompareTo(b))).ToArray();
					for (int r = 0; r < sorted.Length; r++) { rank[sorted[r]] = r; }
				}
				int n = keys.Count;
				// band centers measured from the deterministic build (seed 618034): leaf-parent boundaries sit
				// at ~26.5% and ~49% of the sorted key space (see Diag_Vacuum_Workload_Structure)
				bool InBand(int r) => (r >= (int) (0.245 * n) && r < (int) (0.285 * n)) || (r >= (int) (0.47 * n) && r < (int) (0.51 * n));

				var w = engine.BeginWrite();
				for (int i = 0; i < keys.Count; i++)
				{
					if (InBand(rank[i]) && i % 25 != 0)
					{
						w.Remove(keys[i]);
					}
				}
				engine.Commit(w, ++generation);
			}

			// vacuum until dry; the writer of the LAST productive step carries the streaming counters the
			// asserts read (counters are per writer, and the caller aggregates them)
			FdbLiteTreeWriter last = null!;
			for (int round = 0; round < maxRounds; round++)
			{
				var w = engine.BeginWrite();
				var outcome = w.VacuumWorstRegion(maxInputPages: 6);
				engine.Commit(w, ++generation);
				if (last is null || outcome.InputPages > 0) { last = w; }
				if (outcome.InputPages == 0)
				{
					break;
				}
			}
			return (engine, last);
		}

		[Test]
		[Explicit("diagnostic: dumps the pre-vacuum leaf-parent structure, for calibrating the sparse bands")]
		public void Diag_Vacuum_Workload_Structure()
		{
			var (engine, _) = RunVacuumWorkload(maxRounds: 0);
			using var _e = engine;
			int pageSize = engine.Pager.Geometry.PageSize;

			var groups = new List<List<long>>();
			Walk(engine.Pager, engine.Durable.RootPageId, groups);
			Log($"groups={groups.Count} leaves={groups.Sum(g => g.Count)} joins={engine.LifetimeStreamedJoins} dropLeading={engine.LifetimeStreamedDropLeading} replaceRuns={engine.LifetimeStreamedReplaceRuns}");
			for (int g = 0; g < groups.Count; g++)
			{
				var flags = string.Concat(groups[g].Select(live => live * 10 < (long) pageSize * 6 ? 'S' : 'D'));
				Log($"group {g}: {groups[g].Count} leaves [{flags}]");
			}

			static void Walk(IFdbLitePager pager, uint pageId, List<List<long>> groups)
			{
				var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage).ToArray();
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					groups.Add([ FdbLiteTreePage.LeafLiveBytes(page) ]);
					return;
				}
				int children = FdbLiteTreePage.GetChildCount(page);
				var firstChild = pager.ReadBlocks(FdbLiteTreePage.GetChild(page, 0), pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(firstChild) == FdbLitePageType.Leaf)
				{ // a leaf-parent: one group
					var group = new List<long>(children);
					for (int i = 0; i < children; i++)
					{
						group.Add(FdbLiteTreePage.LeafLiveBytes(pager.ReadBlocks(FdbLiteTreePage.GetChild(page, i), pager.Geometry.BlocksPerPage)));
					}
					groups.Add(group);
					return;
				}
				for (int i = 0; i < children; i++)
				{
					Walk(pager, FdbLiteTreePage.GetChild(page, i), groups);
				}
			}
		}

		[Test]
		public void Streaming_Rebuild_Matches_Materialized_On_Vacuum_Merges()
		{
			using var baseline = RunVacuumWorkload().Engine;
			var (streamedEngine, _) = RunVacuumWorkload();
			using var streamed = streamedEngine;

			Assert.That(streamed.Durable.RootPageId, Is.EqualTo(baseline.Durable.RootPageId), "the two vacuums placed their roots differently");
			Assert.That(streamed.Durable.KeyCount, Is.EqualTo(baseline.Durable.KeyCount), "the two stores hold different key counts");
			Assert.That(streamed.Pager.BlockCount, Is.EqualTo(baseline.Pager.BlockCount), "the two stores allocated different amounts");
			// blocks 0-2 are the store/commit headers and carry a random file id plus wall-clock stamps, so
			// they differ between ANY two stores; the tree and data blocks (frontier starts at 3) are the
			// comparison that means something
			for (uint block = 3; block < baseline.Pager.BlockCount; block++)
			{
				var expected = baseline.Pager.ReadBlocks(block, 1);
				var actual = streamed.Pager.ReadBlocks(block, 1);
				if (!expected.SequenceEqual(actual))
				{
					int offset = expected.CommonPrefixLength(actual);
					Assert.Fail($"stores diverge at block {block}, first differing byte at offset {offset} (baseline={expected[offset]:X2}, streamed={actual[offset]:X2})");
				}
			}
		}

		/// <summary>Execution proof for the merge family, per site: aggregated across every vacuum generation of the streamed store.</summary>
		[Test]
		public void Vacuum_Merges_Execute_The_Streamed_Family()
		{
			using var engine = RunVacuumWorkload().Engine;

			Assert.That(engine.LifetimeStreamedMerges, Is.GreaterThan(0), "no leaf run merged through the streamed K-to-1 merge: the site is dead code under the toggle");
			Assert.That(engine.LifetimeStreamedReplaceRuns, Is.GreaterThan(0), "no vacuum merge rebuilt its parent through the streamed replace-run: the site is dead code under the toggle");
			Assert.That(engine.LifetimeStreamedJoins, Is.GreaterThan(0), "no cross-parent merge ran: the workload no longer covers the join/drop-leading sites");
			Assert.That(engine.LifetimeStreamedDropLeading, Is.GreaterThan(0), "the cross-parent merge never dropped leading children: the site is dead code under the toggle");
		}

		/// <summary>Delete-heavy workload: scattered removals thin leaves (dropped cell ranges), and range removals empty whole leaves and subtrees (child and child-run removals in the ancestors).</summary>
		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) RunDeleteWorkload()
		{
			var (pager, writer) = CreateStore(FdbLiteGeometry.Uniform(14));

			var rnd = new Random(28657);
			var keys = new List<byte[]>(6_000);
			var key = new byte[24];
			var value = new byte[128];
			for (int i = 0; i < 6_000; i++)
			{
				rnd.NextBytes(key);
				key[0] = 0x51;
				key[1] = (byte) (i % 8);
				keys.Add(key.ToArray());
				int len = rnd.Next(1, 120);
				rnd.NextBytes(value.AsSpan(0, len));
				writer.Insert(keys[i], value.AsSpan(0, len));
			}

			// scattered removals: leaves survive with dropped ranges
			for (int i = 0; i < keys.Count; i += 3)
			{
				writer.Remove(keys[i]);
			}

			// range removals: whole buckets die, emptying leaves and removing children (and child runs) from
			// their ancestors
			Span<byte> begin = [ 0x51, 0x02 ];
			Span<byte> end = [ 0x51, 0x05 ];
			writer.RemoveRange(begin, end);

			writer.FlushDirtyPages();
			return (pager, writer);
		}

		[Test]
		public void Delete_Workload_Runs_Are_Byte_Identical_And_Stream_The_Drop_Sites()
		{
			var first = RunDeleteWorkload();
			var second = RunDeleteWorkload();
			AssertStoresIdentical(first, second);

			Assert.That(second.Writer.StreamedLeafDrops, Is.GreaterThan(0), "no delete rebuilt a leaf through the streamed drop path: the site is dead code");
			Assert.That(second.Writer.StreamedChildRemovals, Is.GreaterThan(0), "no delete removed a child through the streamed path: the site is dead code");
		}

		[Test]
		public void Streaming_Rebuild_Matches_Materialized_On_Giant_Cells()
		{
			var baseline = RunGiantCellWorkload();
			var streamed = RunGiantCellWorkload();
			AssertStoresIdentical(baseline, streamed);

			// giant separators overflow internal pages, so this workload is what covers the internal SPLIT
			// fallback; if every internal rebuild fit one page here, that coverage is gone
			Assert.That(streamed.Writer.StreamedInternalRebuilds, Is.GreaterThan(streamed.Writer.StreamedInternalSinglePass), "every internal rebuild took the fast path, so the internal split fallback never ran and this workload no longer covers it");
		}

	}

}

