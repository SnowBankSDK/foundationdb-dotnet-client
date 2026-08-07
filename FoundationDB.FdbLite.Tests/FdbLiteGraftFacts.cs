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

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteGraftFacts : SimpleTest
	{

		[Test]
		public void Test_Import_Options_Fill_Ceiling_Follows_Volatility_Class()
		{
			const int PAGE = 65536;

			// the ceilings are the ones MergedFillCeiling already uses for merged runs, so that a grafted page
			// and a vacuum-merged page of the same declared class come out the same density
			Assert.That(FdbLiteImportOptions.Default.Volatility, Is.EqualTo(FdbLiteVolatilityClass.Stable));
			Assert.That(FdbLiteImportOptions.Default.FillCeiling(PAGE), Is.EqualTo(PAGE), "Stable packs full");

			var occasional = FdbLiteImportOptions.Default with { Volatility = FdbLiteVolatilityClass.Occasional };
			Assert.That(occasional.FillCeiling(PAGE), Is.EqualTo((PAGE * 9) / 10));

			var volatile_ = FdbLiteImportOptions.Default with { Volatility = FdbLiteVolatilityClass.Volatile };
			Assert.That(volatile_.FillCeiling(PAGE), Is.EqualTo((PAGE * 85) / 100));
		}

		/// <summary>Cells of a known size, in key order, so page counts are arithmetic rather than approximate.</summary>
		private static byte[] GraftKey(int i)
		{
			var key = new byte[16];
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(8), i);
			return key;
		}

		/// <summary>Pins the append-path packing guarantee the graft renderer must later match: sequential
		/// <see cref="FdbLiteTreeWriter.Insert"/> packs every leaf but the last to at least 95% of the page.</summary>
		/// <remarks>Drives cells through <c>Insert</c>/<c>Commit</c>, not <see cref="FdbLiteTreeWriter.RenderRun"/>.
		/// This is a regression gate on the append path's own packing behaviour.</remarks>
		[Test]
		public void Test_Sequential_Insert_Packs_Leaves_To_The_Ceiling()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			int pageSize = engine.Pager.Geometry.PageSize;

			// 400 cells of 16-byte key + 256-byte value. Enough to need several pages at any sane page size.
			const int COUNT = 400;
			const int VALUE = 256;
			var value = new byte[VALUE];

			var writer = engine.BeginWrite();
			for (int i = 0; i < COUNT; i++)
			{
				writer.Insert(GraftKey(i), value);
			}
			engine.Commit(writer, 1);

			var stats = engine.MeasureTreeStatistics();

			// every leaf but the last must be packed at or above 95% of the page: a renderer that stops early
			// leaves a trail of half-full pages, which is the whole defect this design exists to avoid
			int atLeast95 = 0;
			FdbLiteTreeStatistics.VisitLeaves(engine.Pager, engine.Durable.RootPageId, live =>
			{
				if (live * 100 >= pageSize * 95L) { atLeast95++; }
			});

			Log($"leaves={stats.LeafPages} at>=95%={atLeast95} liveBytes={stats.LeafLiveBytes}");
			Assert.That(stats.LeafPages, Is.GreaterThan(1), "the run must span several pages or this proves nothing");
			Assert.That(atLeast95, Is.EqualTo(stats.LeafPages - 1), "every page but the last is packed to the ceiling");
		}

		[Test]
		public void Test_SplitCellsAt_Partitions_A_Page_At_The_Insertion_Point()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[16];

			var writer = engine.BeginWrite();
			for (int i = 0; i < 6; i++)
			{
				writer.Insert(GraftKey(i * 10), value);   // keys 0,10,20,30,40,50 in ONE leaf
			}

			// partition at 25: keys 0,10,20 below, keys 30,40,50 at-or-above
			var (below, above) = writer.SplitCellsAt(writer.Root, GraftKey(25));
			Assert.That(below.Length, Is.EqualTo(3), "keys 0,10,20 sort below 25");
			Assert.That(above.Length, Is.EqualTo(3), "keys 30,40,50 sort at or above 25");

			// a key EQUAL to an existing key belongs on the at-or-above side (below is strictly less): with
			// 30 present in the page, splitting at 30 must give the same 3/3 split as splitting at 25.
			var (belowEqual, aboveEqual) = writer.SplitCellsAt(writer.Root, GraftKey(30));
			Assert.That(belowEqual.Length, Is.EqualTo(3), "keys 0,10,20 sort strictly below 30");
			Assert.That(aboveEqual.Length, Is.EqualTo(3), "keys 30,40,50 sort at or above 30, including 30 itself");

			// a key that precedes everything leaves the whole page on the right. GraftKey(-1) does NOT work for
			// this: WriteInt64BigEndian encodes -1 as 0xFF...FF, which sorts ABOVE every non-negative GraftKey, not
			// below it. The empty key is a proper prefix of every non-empty key, so it is the one value guaranteed
			// to precede all of them under the tree's byte-lexicographic ordering.
			var (none, all) = writer.SplitCellsAt(writer.Root, []);
			Assert.That(none.Length, Is.Zero);
			Assert.That(all.Length, Is.EqualTo(6));

			// a key that follows everything leaves the whole page on the left
			var (everything, empty) = writer.SplitCellsAt(writer.Root, GraftKey(999));
			Assert.That(everything.Length, Is.EqualTo(6));
			Assert.That(empty.Length, Is.Zero);
		}

		private const int GRAFT_VALUE = 256;

		/// <summary>Seeds a tree with a deliberate GAP: keys 0..<paramref name="side"/>-1 and 100000..100000+<paramref name="side"/>-1, nothing between.</summary>
		private static FdbLiteEngine SeedGappedTree(int side)
		{
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[GRAFT_VALUE];
			var seed = engine.BeginWrite();
			for (int i = 0; i < side; i++) { seed.Insert(GraftKey(i), value); }
			for (int i = 0; i < side; i++) { seed.Insert(GraftKey(100_000 + i), value); }
			engine.Commit(seed, 1);
			return engine;
		}

		/// <summary>The run a graft consumes: cells that carry their own buffer, since nothing gathered them from a page.</summary>
		private static FdbLiteTreeWriter.CellRef[] BuildRun(int first, int count)
		{
			var run = new FdbLiteTreeWriter.CellRef[count];
			for (int i = 0; i < count; i++)
			{
				var cell = new byte[16 + GRAFT_VALUE];
				GraftKey(first + i).CopyTo(cell.AsSpan(0, 16));
				run[i] = FdbLiteTreeWriter.CellRef.OfLeafBuffer(cell, 16, GRAFT_VALUE, 0);
			}
			return run;
		}

		/// <summary>A run over an arbitrary (non-contiguous) key list, for tests whose run itself has gaps.</summary>
		private static FdbLiteTreeWriter.CellRef[] BuildRun(IReadOnlyList<byte[]> keys)
		{
			var run = new FdbLiteTreeWriter.CellRef[keys.Count];
			for (int i = 0; i < keys.Count; i++)
			{
				var cell = new byte[keys[i].Length + GRAFT_VALUE];
				keys[i].CopyTo(cell.AsSpan(0, keys[i].Length));
				run[i] = FdbLiteTreeWriter.CellRef.OfLeafBuffer(cell, keys[i].Length, GRAFT_VALUE, 0);
			}
			return run;
		}

		/// <summary>Wraps a pager to count <see cref="ReadBlocks"/> calls, so an early-exit claim can be checked against
		/// how many pages a walk actually touched instead of trusting the returned count alone.</summary>
		private sealed class CountingPager(IFdbLitePager inner) : IFdbLitePager
		{
			public int ReadCalls;

			public FdbLiteGeometry Geometry => inner.Geometry;
			public uint BlockCount => inner.BlockCount;
			public uint RegionSizeInBlocks => inner.RegionSizeInBlocks;

			public FdbLitePageRef ReadBlocksRef(uint firstBlock, int count)
			{
				this.ReadCalls++;
				return inner.ReadBlocksRef(firstBlock, count);
			}

			public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count)
			{
				this.ReadCalls++;
				return inner.ReadBlocks(firstBlock, count);
			}

			public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data) => inner.WriteBlocks(firstBlock, data);
			public void Flush() => inner.Flush();
			public void Grow(uint minimumBlockCount) => inner.Grow(minimumBlockCount);
			public void Truncate(uint newBlockCount) => inner.Truncate(newBlockCount);
			public bool TrackFirstTouch { get => inner.TrackFirstTouch; set => inner.TrackFirstTouch = value; }
			public bool MarkTouched(uint firstBlock) => inner.MarkTouched(firstBlock);

			public void ResetFirstTouch() => inner.ResetFirstTouch();
			public void PunchHole(uint firstBlock, uint count) => inner.PunchHole(firstBlock, count);
			public void Prefetch(uint firstBlock, uint count) => inner.Prefetch(firstBlock, count);
			public void Dispose() => inner.Dispose();
		}

		/// <summary>Grafts <paramref name="run"/> and returns the number of pages it emitted.</summary>
		private static int Graft(FdbLiteTreeWriter writer, int pageSize, ReadOnlySpan<byte> begin, FdbLiteTreeWriter.CellRef[] run, FdbLiteVolatilityClass volatility = FdbLiteVolatilityClass.Stable)
		{
			// the caller names the leaf the run falls in, which is what Task 6's driver will do too
			Span<uint> pathPages = stackalloc uint[20];
			Span<int> pathChildren = stackalloc int[20];
			uint leafId = writer.DescendToLeaf(begin, pathPages, pathChildren, out _);

			// one entry per emitted page, and the merged list is the run plus the boundary leaf's cells; a cell
			// costs at least one byte, so the page size is a safe (if generous) bound on that second term
			var output = new FdbLiteGraftedPage[run.Length + pageSize];
			var options = FdbLiteImportOptions.Default with { Volatility = volatility };
			return writer.GraftIntoGap(leafId, begin, run, options.FillCeiling(pageSize), volatility, output);
		}

		/// <summary>Volatility episode count of every leaf of the committed tree, in key order.</summary>
		private static List<byte> LeafEpisodes(FdbLiteEngine engine)
		{
			var episodes = new List<byte>();
			Walk(engine.Durable.RootPageId);
			return episodes;

			void Walk(uint pageId)
			{
				var page = engine.Pager.ReadBlocks(pageId, engine.Pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					episodes.Add(FdbLitePageHeader.GetVolatilityEpisodes(page));
					return;
				}
				int children = FdbLiteTreePage.GetChildCount(page);
				var ids = new uint[children];
				for (int i = 0; i < children; i++) { ids[i] = FdbLiteTreePage.GetChild(page, i); }
				foreach (var id in ids) { Walk(id); }
			}
		}

		/// <summary>The cross-level structural oracle: separators, ordering and aggregates must all agree after a splice.</summary>
		private static void AssertStructurallySound(FdbLiteEngine engine)
		{
			var problems = FdbLiteTreeAudit.Check(engine.Pager, engine.Durable.RootPageId, maxProblems: 8);
			foreach (var p in problems) { Log($"# STRUCT {p}"); }
			Assert.That(problems, Is.Empty, "the grafted pages must splice into a structurally sound tree");
		}

		/// <summary>Reads every key of the committed tree back in order, as its GraftKey index.</summary>
		private static List<long> ReadAllKeys(FdbLiteEngine engine)
		{
			var keys = new List<long>();
			var cursor = new FdbLiteTreeCursor(engine.Pager, engine.Durable.RootPageId);
			if (cursor.SeekCeiling([]))
			{
				do
				{
					keys.Add(System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(cursor.CurrentKey[8..]));
				}
				while (cursor.MoveNext());
			}
			return keys;
		}

		/// <summary>Records what per-key insertion into a gap achieves, which is the bar <see cref="Test_GraftIntoGap_Fills_The_Middle_Pages_Whole"/> exists to beat.</summary>
		/// <remarks>Not a graft test: it drives <see cref="FdbLiteTreeWriter.Insert"/> only, and pins the defect rather than the fix.</remarks>
		[Test]
		public void Test_Per_Key_Insertion_Into_A_Gap_Leaves_Leaves_Half_Empty()
		{
			using var engine = SeedGappedTree(100);
			int pageSize = engine.Pager.Geometry.PageSize;
			var value = new byte[GRAFT_VALUE];

			var writer = engine.BeginWrite();
			for (int i = 0; i < 2000; i++) { writer.Insert(GraftKey(1000 + i), value); }
			engine.Commit(writer, 2);

			var stats = engine.MeasureTreeStatistics();
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / ((long) stats.LeafPages * pageSize));
			Log($"per-key: leaves={stats.LeafPages} fill={fillPct}%");
			Assert.That(fillPct, Is.LessThan(90), "if per-key insertion already packed a gap full, the graft would have nothing to fix");
		}

		[Test]
		public void Test_GraftIntoGap_Fills_The_Middle_Pages_Whole()
		{
			using var engine = SeedGappedTree(100);
			int pageSize = engine.Pager.Geometry.PageSize;

			int leavesBefore = engine.MeasureTreeStatistics().LeafPages;
			long pendingReclaimBefore = engine.GetStats().PendingReclaimBlocks;

			// graft 2,000 keys into the gap, which is several pages worth
			var writer = engine.BeginWrite();
			var run = BuildRun(1000, 2000);
			int emitted = Graft(writer, pageSize, GraftKey(1000), run);
			engine.Commit(writer, 2);

			var stats = engine.MeasureTreeStatistics();
			long capacity = (long) stats.LeafPages * pageSize;
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / capacity);
			Log($"leavesBefore={leavesBefore} emitted={emitted} leavesAfter={stats.LeafPages} fill={fillPct}% pendingReclaimBefore={pendingReclaimBefore} pendingReclaimAfter={engine.GetStats().PendingReclaimBlocks}");

			// the graft must not lose or duplicate a key, and the tree must still be in key order
			AssertStructurallySound(engine);
			var keys = ReadAllKeys(engine);
			Assert.That(keys.Count, Is.EqualTo(2200), "100 below + 2000 grafted + 100 above");
			Assert.That(keys, Is.Ordered, "the spliced separators must keep the tree sorted");
			Assert.That(keys[100], Is.EqualTo(1000L));
			Assert.That(keys[2099], Is.EqualTo(2999L));

			// the boundary leaf's own cells were carried over, not re-added, so the count comes ONLY from the run:
			// a wrong KeyCountDelta (e.g. double-counting the carried-over cells, or missing the run) passes every
			// other oracle here (cursor readback, structural audit) but not this one
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(2200UL), "KeyCountDelta must book exactly the run's new keys, not the boundary leaf's carried-over ones");

			// two pages are retired here: the rebuilt root is an ordinary copy-on-write (WritePage frees the old
			// root as it copies it) plus the boundary leaf itself, which ONLY the FreePage(leafId) at the tail of
			// GraftIntoGap retires. Without it this figure is short by exactly one BlocksPerPage, and every
			// other oracle here (structural audit, cursor readback) stays silent about the leak
			Assert.That(engine.GetStats().PendingReclaimBlocks, Is.EqualTo(pendingReclaimBefore + (2 * engine.Pager.Geometry.BlocksPerPage)), "the boundary leaf's page must be retired, not leaked");

			// the acceptance bar from the design: a run that owns its range packs like a sequential build
			Assert.That(fillPct, Is.GreaterThanOrEqualTo(90), "a run owning its range must pack near full");
		}

		/// <summary>A grafted page takes the DECLARED volatility class, and does not inherit the boundary leaf's own history.</summary>
		[Test]
		public void Test_GraftIntoGap_Stamps_The_Declared_Volatility_Class()
		{
			using var engine = SeedGappedTree(20);
			int pageSize = engine.Pager.Geometry.PageSize;
			var value = new byte[GRAFT_VALUE];

			// Brand the boundary leaf: an INTERIOR insert books one episode, at most one per generation, so two
			// generations give it a count of 2. Without this seeding the defect is invisible - a boundary leaf
			// at 0 is indistinguishable from a correctly stamped Stable page.
			var bump1 = engine.BeginWrite();
			bump1.Insert(GraftKey(500), value);
			engine.Commit(bump1, 2);
			var bump2 = engine.BeginWrite();
			bump2.Insert(GraftKey(600), value);
			engine.Commit(bump2, 3);

			Assert.That(LeafEpisodes(engine), Is.EqualTo(new List<byte> { 2 }), "the boundary leaf must carry a non-zero count, or this test cannot tell inheritance from a correct stamp");

			// declared Occasional == 1, deliberately different from the boundary leaf's 2 AND from 0
			var writer = engine.BeginWrite();
			var run = BuildRun(1000, 2000);
			int emitted = Graft(writer, pageSize, GraftKey(1000), run, FdbLiteVolatilityClass.Occasional);
			engine.Commit(writer, 4);

			var after = LeafEpisodes(engine);
			Log($"declared=Occasional emitted={emitted} leafEpisodes=[{string.Join(",", after)}]");

			AssertStructurallySound(engine);
			Assert.That(after.Count, Is.GreaterThan(1), "the graft must have emitted several pages or this proves little");
			// reading 2 here is the defect: every grafted page inheriting the boundary leaf's history, which would
			// make a one-time bulk load brand its leaves volatile forever
			Assert.That(after, Is.All.EqualTo((byte) FdbLiteVolatilityClass.Occasional), "every grafted page must carry the declared class, not the boundary leaf's episode count");
		}

		/// <summary>The boundary leaf IS the root: the graft's pages have no parent to splice into and must grow one.</summary>
		[Test]
		public void Test_GraftIntoGap_Grows_A_Root_When_The_Leaf_Was_The_Tree()
		{
			using var engine = SeedGappedTree(20);
			int pageSize = engine.Pager.Geometry.PageSize;
			Assert.That(engine.MeasureTreeStatistics().LeafPages, Is.EqualTo(1), "the seed must fit ONE leaf or this proves nothing");

			var writer = engine.BeginWrite();
			var run = BuildRun(1000, 2000);
			int emitted = Graft(writer, pageSize, GraftKey(1000), run);
			engine.Commit(writer, 2);

			var stats = engine.MeasureTreeStatistics();
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / ((long) stats.LeafPages * pageSize));
			Log($"root grew: emitted={emitted} leaves={stats.LeafPages} internal={stats.InternalPages} fill={fillPct}%");

			AssertStructurallySound(engine);
			var keys = ReadAllKeys(engine);
			Assert.That(keys.Count, Is.EqualTo(2040), "20 below + 2000 grafted + 20 above");
			Assert.That(keys, Is.Ordered);
			Assert.That(stats.InternalPages, Is.GreaterThan(0), "the tree must have grown a level");
			Assert.That(fillPct, Is.GreaterThanOrEqualTo(90));
		}

		/// <summary>The run a public <see cref="FdbLiteEngine.Import"/> consumes, over a contiguous key range.</summary>
		private static List<KeyValuePair<Slice, Slice>> BuildImportRun(int first, int count)
		{
			var value = new byte[GRAFT_VALUE];
			var run = new List<KeyValuePair<Slice, Slice>>(count);
			for (int i = 0; i < count; i++) { run.Add(new(GraftKey(first + i).AsSlice(), value.AsSlice())); }
			return run;
		}

		[Test]
		public void Test_Import_Beats_Per_Key_Insertion_On_A_Range_It_Owns()
		{
			int Build(bool useImport)
			{
				using var engine = SeedGappedTree(100);

				if (useImport)
				{
					int applied = engine.Import(BuildImportRun(1000, 2000), GraftKey(1000).AsSlice(), GraftKey(3001).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2);
					Assert.That(applied, Is.EqualTo(2000), "every pair of the run must be applied");
				}
				else
				{
					var value = new byte[GRAFT_VALUE];
					var w = engine.BeginWrite();
					for (int i = 0; i < 2000; i++) { w.Insert(GraftKey(1000 + i), value); }
					engine.Commit(w, 2);
				}

				// same 2,200 keys either way, so the leaf counts below compare like for like
				AssertStructurallySound(engine);
				var keys = ReadAllKeys(engine);
				Assert.That(keys.Count, Is.EqualTo(2200), "100 below + 2000 imported + 100 above");
				Assert.That(keys, Is.Ordered);

				return engine.MeasureTreeStatistics().LeafPages;
			}

			int perKey = Build(useImport: false);
			int imported = Build(useImport: true);
			Log($"perKey={perKey} leaves, import={imported} leaves");

			Assert.That(imported, Is.LessThanOrEqualTo(perKey), "the graft must never produce MORE pages than per-key insertion");
		}

		/// <summary>The volatility class the CALLER declares must reach the emitted pages, not stop at the options record.</summary>
		[Test]
		public void Test_Import_Stamps_The_Declared_Volatility_Class()
		{
			using var engine = SeedGappedTree(20);
			var value = new byte[GRAFT_VALUE];

			// Brand the boundary leaf: an INTERIOR insert books one episode, at most one per generation, so two
			// generations give it a count of 2. Without this seeding the defect is invisible - a boundary leaf at 0
			// is indistinguishable from a correctly stamped Stable page. Both keys sit OUTSIDE the imported range,
			// so they are not cleared and the count they brand the leaf with survives to the graft.
			var bump1 = engine.BeginWrite();
			bump1.Insert(GraftKey(500), value);
			engine.Commit(bump1, 2);
			var bump2 = engine.BeginWrite();
			bump2.Insert(GraftKey(600), value);
			engine.Commit(bump2, 3);
			Assert.That(LeafEpisodes(engine), Is.EqualTo(new List<byte> { 2 }), "the boundary leaf must carry a non-zero count, or this test cannot tell inheritance from a correct stamp");

			// declared Occasional == 1, deliberately different from the boundary leaf's 2 AND from 0
			int applied = engine.Import(BuildImportRun(1000, 2000), GraftKey(1000).AsSlice(), GraftKey(3001).AsSlice(), FdbLiteImportOptions.Default with { Volatility = FdbLiteVolatilityClass.Occasional }, databaseVersion: 4);

			var after = LeafEpisodes(engine);
			Log($"declared=Occasional applied={applied} leafEpisodes=[{string.Join(",", after)}]");

			AssertStructurallySound(engine);
			Assert.That(applied, Is.EqualTo(2000));
			Assert.That(after.Count, Is.GreaterThan(1), "the import must have emitted several pages or this proves little");
			// a knob that silently did nothing reads 0 (Stable, the default the options record never left) or 2
			// (the boundary leaf's own history); only the declared class proves the value travelled end to end
			Assert.That(after, Is.All.EqualTo((byte) FdbLiteVolatilityClass.Occasional), "every page the import emitted must carry the class the caller declared");
		}

		/// <summary>A run that does not own its range takes the per-key fallback, and the keys it does not supply survive.</summary>
		/// <remarks>The gate is "no survivor at all" rather than "few enough survivors" precisely because of this case:
		/// the graft path CLEARS the range before rendering, so grafting a run with even one survivor in its range would
		/// delete that key. A single survivor is the smallest input that tells the two gates apart.</remarks>
		[Test]
		public void Test_Import_Falls_Back_To_Per_Key_And_Keeps_The_Survivors()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[GRAFT_VALUE];

			var seed = engine.BeginWrite();
			for (int i = 0; i < 200; i++) { seed.Insert(GraftKey(i), value); }
			engine.Commit(seed, 1);

			// the run supplies 0..198 and the range covers 0..199, so key 199 is the lone survivor
			int applied = engine.Import(BuildImportRun(0, 199), GraftKey(0).AsSlice(), GraftKey(200).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2);
			Assert.That(applied, Is.EqualTo(199));

			AssertStructurallySound(engine);
			var keys = ReadAllKeys(engine);
			Log($"applied={applied} keysAfter={keys.Count} keyCount={engine.Durable.KeyCount}");
			Assert.That(keys.Count, Is.EqualTo(200), "the key the run does not supply must survive the import");
			Assert.That(keys[199], Is.EqualTo(199L), "the survivor is the last key of the range");
			Assert.That(keys, Is.Ordered);
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(200UL), "an import that only replaces existing keys must not move the key count");
		}

		/// <summary>An import into an EMPTY store: there is no boundary leaf to graft into, and it is the case a restore actually hits.</summary>
		[Test]
		public void Test_Import_Into_An_Empty_Store_Grafts_A_Whole_Tree()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			int pageSize = engine.Pager.Geometry.PageSize;

			int applied = engine.Import(BuildImportRun(0, 2000), GraftKey(0).AsSlice(), GraftKey(2000).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 1);

			var stats = engine.MeasureTreeStatistics();
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / ((long) stats.LeafPages * pageSize));
			Log($"empty store: applied={applied} leaves={stats.LeafPages} fill={fillPct}%");

			AssertStructurallySound(engine);
			var keys = ReadAllKeys(engine);
			Assert.That(applied, Is.EqualTo(2000));
			Assert.That(keys.Count, Is.EqualTo(2000));
			Assert.That(keys, Is.Ordered);
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(2000UL));
			Assert.That(stats.LeafPages, Is.GreaterThan(1), "the run must span several pages or the packing claim proves nothing");
			Assert.That(fillPct, Is.GreaterThanOrEqualTo(90), "a run owning a whole empty store must pack near full");
		}

		/// <summary>The same run, with every value byte set to <paramref name="fill"/>, so a replaced value is distinguishable from the one it replaced.</summary>
		private static List<KeyValuePair<Slice, Slice>> BuildImportRun(int first, int count, byte fill, int valueSize = GRAFT_VALUE)
		{
			var value = new byte[valueSize];
			value.AsSpan().Fill(fill);
			var run = new List<KeyValuePair<Slice, Slice>>(count);
			for (int i = 0; i < count; i++) { run.Add(new(GraftKey(first + i).AsSlice(), value.AsSlice())); }
			return run;
		}

		/// <summary>Reads every key back with the first byte of its value, which is the marker the import runs stamp.</summary>
		private static List<(long Key, byte Fill)> ReadAllPairs(FdbLiteEngine engine, int valueSize = GRAFT_VALUE)
		{
			var pairs = new List<(long, byte)>();
			var cursor = new FdbLiteTreeCursor(engine.Pager, engine.Durable.RootPageId);
			if (cursor.SeekCeiling([]))
			{
				do
				{
					var value = cursor.CurrentValue;
					pairs.Add((System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(cursor.CurrentKey[8..]), value.Length == valueSize ? value[0] : (byte) 0xFF));
				}
				while (cursor.MoveNext());
			}
			return pairs;
		}

		/// <summary>The re-restore case: the imported range is ALREADY populated, so the graft's clear removes exactly what the run puts back.</summary>
		/// <remarks>The untested intersection of the two riskiest paths - <c>RemoveRange</c> deleting N keys and the renderer writing n back
		/// into the hole - and the shape a second restore of the same range actually has.</remarks>
		[Test]
		public void Test_Import_Over_A_Populated_Range_Replaces_Every_Key()
		{
			using var engine = SeedGappedTree(100);
			int pageSize = engine.Pager.Geometry.PageSize;

			// fill the gap first: 1000..2999 exist, with the 0xA5 marker, before the import touches them
			var seeded = new byte[GRAFT_VALUE];
			seeded.AsSpan().Fill(0xA5);
			var fill = engine.BeginWrite();
			for (int i = 0; i < 2000; i++) { fill.Insert(GraftKey(1000 + i), seeded); }
			engine.Commit(fill, 2);
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(2200UL), "100 below + 2000 in the range + 100 above");

			// every key of [1000, 3000) is supplied, so the gate stays on the graft path and the clear drops all 2000
			int applied = engine.Import(BuildImportRun(1000, 2000, 0x5C), GraftKey(1000).AsSlice(), GraftKey(3000).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 3);

			var stats = engine.MeasureTreeStatistics();
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / ((long) stats.LeafPages * pageSize));
			Log($"re-import: applied={applied} keys={engine.Durable.KeyCount} leaves={stats.LeafPages} fill={fillPct}%");

			AssertStructurallySound(engine);
			var pairs = ReadAllPairs(engine);
			Assert.That(applied, Is.EqualTo(2000));
			Assert.That(pairs.Count, Is.EqualTo(2200), "replacing 2000 keys with 2000 keys must not change the population");
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(2200UL), "the clear and the render must balance in the key count too");
			Assert.That(pairs.Select(p => p.Key), Is.Ordered);
			// a graft that cleared but re-rendered stale cells would read back 0xA5 here
			Assert.That(pairs.Where(p => p.Key is >= 1000 and < 3000).Select(p => p.Fill), Is.All.EqualTo((byte) 0x5C), "every key of the imported range must carry the NEW value");
			Assert.That(pairs.Where(p => p.Key is < 1000 or >= 3000).Select(p => p.Fill), Is.All.EqualTo((byte) 0x00), "a key outside the range must keep the value the seed gave it");
			Assert.That(stats.LeafPages, Is.GreaterThan(1), "the run must span several pages or the packing claim proves nothing");
			Assert.That(fillPct, Is.GreaterThanOrEqualTo(90), "re-importing over a populated range must pack as well as importing into a gap");
		}

		/// <summary>Levels of the committed tree, counting the leaf: 1 is a lone root leaf, 3 is a root over internal pages over leaves.</summary>
		private static int TreeDepth(FdbLiteEngine engine)
		{
			int depth = 1;
			uint pageId = engine.Durable.RootPageId;
			while (true)
			{
				var page = engine.Pager.ReadBlocks(pageId, engine.Pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					return depth;
				}
				depth++;
				pageId = FdbLiteTreePage.GetChild(page, 0);
			}
		}

		/// <summary>The same import over a populated range, in a THREE-level tree: the ascent's per-level raise loop
		/// iterates more than once, and the leaves the clear empties can sit under DIFFERENT parents, which puts the
		/// loose separator at the grandparent.</summary>
		/// <remarks>Every other graft test runs on <see cref="FdbLiteGeometry.Default"/> (32 KiB pages) with a couple of
		/// thousand SMALL cells, which all fit under ONE internal page: the raise loop cannot climb twice and the root
		/// cannot grow twice, so those paths are structurally unreachable there rather than merely untested. Depth comes
		/// from the leaf count, and the page floor is 16 KiB (<see cref="FdbLiteGeometry.MinPageSizeLog2"/>), so this test
		/// buys its leaves with FAT values instead: at the smallest legal page, a value at the inline ceiling puts three
		/// cells in a leaf, and 4,200 keys then need more leaves than one internal page can address. The depth is asserted
		/// BEFORE anything else, because a two-level tree passes every assert below while proving nothing new.</remarks>
		[Test]
		public void Test_Import_Over_A_Populated_Range_In_A_Three_Level_Tree()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Uniform(FdbLiteGeometry.MinPageSizeLog2)));
			int deepValue = engine.Pager.Geometry.MaxInlineValueLength;

			// 100 below the range, 4000 inside it with the 0xA5 marker, 100 above
			var seeded = new byte[deepValue];
			seeded.AsSpan().Fill(0xA5);
			var untouched = new byte[deepValue];
			var seed = engine.BeginWrite();
			for (int i = 0; i < 100; i++) { seed.Insert(GraftKey(i), untouched); }
			for (int i = 0; i < 4000; i++) { seed.Insert(GraftKey(1000 + i), seeded); }
			for (int i = 0; i < 100; i++) { seed.Insert(GraftKey(100_000 + i), untouched); }
			engine.Commit(seed, 1);

			int depth = TreeDepth(engine);
			Log($"seeded: depth={depth} leaves={engine.MeasureTreeStatistics().LeafPages} internal={engine.MeasureTreeStatistics().InternalPages} keys={engine.Durable.KeyCount}");
			Assert.That(depth, Is.GreaterThanOrEqualTo(3), "the seed must build a tree of three levels or more, or this test proves nothing the two-level ones do not");
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(4200UL));

			// every key of [1000, 5000) is supplied, so the gate stays on the graft path and the clear drops all 4000
			int applied = engine.Import(BuildImportRun(1000, 4000, 0x5C, deepValue), GraftKey(1000).AsSlice(), GraftKey(5000).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2);

			int depthAfter = TreeDepth(engine);
			Log($"deep import: applied={applied} depth={depthAfter} keys={engine.Durable.KeyCount}");

			AssertStructurallySound(engine);
			var pairs = ReadAllPairs(engine, deepValue);
			Assert.That(applied, Is.EqualTo(4000));
			Assert.That(pairs.Count, Is.EqualTo(4200), "replacing 4000 keys with 4000 keys must not change the population");
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(4200UL));
			Assert.That(pairs.Select(p => p.Key), Is.Ordered);
			Assert.That(pairs.Where(p => p.Key is >= 1000 and < 5000).Select(p => p.Fill), Is.All.EqualTo((byte) 0x5C), "every key of the imported range must carry the NEW value");
			Assert.That(pairs.Where(p => p.Key is < 1000 or >= 5000).Select(p => p.Fill), Is.All.EqualTo((byte) 0x00), "a key outside the range must keep the value the seed gave it");
			Assert.That(depthAfter, Is.GreaterThanOrEqualTo(3), "the graft must splice into the deep tree, not flatten it");
		}

		/// <summary>The imported range covers the WHOLE tree: the clear drops the root, and the graft re-enters the empty-store branch from inside <c>ImportRun</c>.</summary>
		[Test]
		public void Test_Import_Over_The_Whole_Tree_Rebuilds_It_From_Empty()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			int pageSize = engine.Pager.Geometry.PageSize;

			var seeded = new byte[GRAFT_VALUE];
			seeded.AsSpan().Fill(0xA5);
			var seed = engine.BeginWrite();
			for (int i = 0; i < 2000; i++) { seed.Insert(GraftKey(i), seeded); }
			engine.Commit(seed, 1);
			Assert.That(engine.MeasureTreeStatistics().InternalPages, Is.GreaterThan(0), "the seed must be a multi-level tree or the root-drop proves nothing");

			// [0, 2000) is the entire tree: nothing survives the clear, so the renderer starts from Root == 0
			int applied = engine.Import(BuildImportRun(0, 2000, 0x5C), GraftKey(0).AsSlice(), GraftKey(2000).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2);

			var stats = engine.MeasureTreeStatistics();
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / ((long) stats.LeafPages * pageSize));
			Log($"whole tree: applied={applied} keys={engine.Durable.KeyCount} leaves={stats.LeafPages} fill={fillPct}%");

			AssertStructurallySound(engine);
			var pairs = ReadAllPairs(engine);
			Assert.That(applied, Is.EqualTo(2000));
			Assert.That(pairs.Count, Is.EqualTo(2000));
			Assert.That(engine.Durable.KeyCount, Is.EqualTo(2000UL));
			Assert.That(pairs.Select(p => p.Key), Is.Ordered);
			Assert.That(pairs.Select(p => p.Fill), Is.All.EqualTo((byte) 0x5C), "every key must carry the NEW value");
			Assert.That(stats.LeafPages, Is.GreaterThan(1), "the run must span several pages or the packing claim proves nothing");
			Assert.That(fillPct, Is.GreaterThanOrEqualTo(90), "rebuilding the whole tree must pack near full");
		}

		/// <summary>What <see cref="FdbLiteEngine.Import"/> rejects, and how. These are all things a CALLER gets wrong, so each one
		/// is an explicit <see cref="ArgumentException"/> naming the offending parameter, not a contract failure: an important check
		/// on a public entry point belongs in the code, and stays in a release build.</summary>
		/// <remarks>Every check runs before the first write, so a rejected import must leave the store untouched - which is what the
		/// last case asserts.</remarks>
		[Test]
		public void Test_Import_Rejects_What_The_Caller_Got_Wrong()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

			var seed = engine.BeginWrite();
			for (int i = 100; i < 110; i++) { seed.Insert(GraftKey(i), new byte[16]); }
			engine.Commit(seed, 1);

			var value = new byte[16].AsSlice();
			List<KeyValuePair<Slice, Slice>> One(byte[] key, Slice v) => [ new(key.AsSlice(), v) ];

			// an EMPTY range: nothing can be imported into it
			Assert.That(
				() => engine.Import(One(GraftKey(1), value), GraftKey(1).AsSlice(), GraftKey(1).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("end").And.Message.Contains("strictly above the begin key")
			);

			// an OVERSIZE upper bound: the graft writes `end` as a separator, so it is held to a key's ceiling
			var longEnd = new byte[FdbLiteTreePage.MaxKeyLength + 1];
			longEnd.AsSpan().Fill(0xFF);
			Assert.That(
				() => engine.Import(One(GraftKey(1), value), GraftKey(0).AsSlice(), longEnd.AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("end").And.Message.Contains("maximum key length")
			);

			// an OVERSIZE key: without this the renderer accepts a cell no page can hold, and its part-sizing loop stops making progress
			var longKey = new byte[FdbLiteTreePage.MaxKeyLength + 1];
			GraftKey(1).CopyTo(longKey.AsSpan(0, 16));
			Assert.That(
				() => engine.Import(One(longKey, value), GraftKey(0).AsSlice(), GraftKey(2).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("run").And.Message.Contains("maximum key length")
			);

			// an OVERSIZE value: the out-of-line extent path is not wired into the graft renderer
			int maxInline = FdbLiteGeometry.Default.MaxInlineValueLength;
			Assert.That(
				() => engine.Import(One(GraftKey(1), new byte[maxInline + 1].AsSlice()), GraftKey(0).AsSlice(), GraftKey(2).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("run").And.Message.Contains("maximum inline value length")
			);

			// a key OUTSIDE the range: the graft clears [begin, end), so a key beyond it would be applied by one path and not the other
			Assert.That(
				() => engine.Import(One(GraftKey(9), value), GraftKey(0).AsSlice(), GraftKey(2).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("run").And.Message.Contains("outside the imported range")
			);

			// an UNSORTED run (here a duplicate, which is the strictness half of the rule): an unsorted merge renders a silently mis-ordered tree
			List<KeyValuePair<Slice, Slice>> descending = [ new(GraftKey(1).AsSlice(), value), new(GraftKey(0).AsSlice(), value) ];
			Assert.That(
				() => engine.Import(descending, GraftKey(0).AsSlice(), GraftKey(2).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("run").And.Message.Contains("strictly ascending key order")
			);
			List<KeyValuePair<Slice, Slice>> duplicate = [ new(GraftKey(1).AsSlice(), value), new(GraftKey(1).AsSlice(), value) ];
			Assert.That(
				() => engine.Import(duplicate, GraftKey(0).AsSlice(), GraftKey(2).AsSlice(), FdbLiteImportOptions.Default, databaseVersion: 2),
				Throws.ArgumentException.With.Property("ParamName").EqualTo("run").And.Message.Contains("strictly ascending key order")
			);

			// nothing above reached a write: the seed is still the durable generation, intact
			Assert.That(engine.Durable.DatabaseVersion, Is.EqualTo(1UL), "a rejected import must not commit a generation");
			Assert.That(ReadAllKeys(engine).Count, Is.EqualTo(10), "a rejected import must leave the store untouched");
		}

		[Test]
		public void Test_CountSurvivors_Early_Exits_Once_The_Budget_Is_Reached()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[16];

			var seed = engine.BeginWrite();
			for (int i = 0; i < 20; i++) { seed.Insert(GraftKey(i), value); }   // keys 0..19
			engine.Commit(seed, 1);

			var writer = engine.BeginWrite();

			// a run supplying every even key in 0..19 leaves the 10 odd ones surviving
			var evenKeys = new List<byte[]>();
			for (int i = 0; i < 20; i += 2) { evenKeys.Add(GraftKey(i)); }
			var evens = BuildRun(evenKeys);

			int found = writer.CountSurvivors(GraftKey(0), GraftKey(20), evens, stopAfter: 3);
			Assert.That(found, Is.EqualTo(3), "the walk stops as soon as the budget is reached");

			// a run supplying EVERY key in the range leaves none: that is situation C, a clean overwrite
			var allKeys = new List<byte[]>();
			for (int i = 0; i < 20; i++) { allKeys.Add(GraftKey(i)); }
			var all = BuildRun(allKeys);
			Assert.That(writer.CountSurvivors(GraftKey(0), GraftKey(20), all, stopAfter: 3), Is.Zero);
		}

		/// <summary>The required test above only checks the RETURN VALUE, which a "count everything, then clamp
		/// the result" implementation would also pass while defeating the entire point of the budget (it would
		/// still walk every survivor). This test proves the walk itself stops: it counts pager reads through a
		/// budget-of-3 call against a seed large enough to need many leaves, and requires that far fewer leaves
		/// were touched than a full scan would need.</summary>
		[Test]
		public void Test_CountSurvivors_Early_Exit_Stops_The_Walk_Not_Just_The_Count()
		{
			var counting = new CountingPager(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			using var engine = FdbLiteEngine.Create(counting);
			var value = new byte[8];

			const int COUNT = 20_000;
			var seed = engine.BeginWrite();
			for (int i = 0; i < COUNT; i++) { seed.Insert(GraftKey(i), value); }
			engine.Commit(seed, 1);

			var stats = engine.MeasureTreeStatistics();
			Log($"seeded leaves={stats.LeafPages}");
			Assert.That(stats.LeafPages, Is.GreaterThan(5), "the seed must span several leaves or a bounded read count proves nothing");

			var writer = engine.BeginWrite();
			counting.ReadCalls = 0; // only the walk under test counts from here

			// an EMPTY run supplies nothing: every key in [0, COUNT) is a survivor, so a walk that did not stop
			// at the budget would visit all COUNT keys, across every leaf
			int found = writer.CountSurvivors(GraftKey(0), GraftKey(COUNT), [], stopAfter: 3);

			Log($"readCalls={counting.ReadCalls} leaves={stats.LeafPages}");
			Assert.That(found, Is.EqualTo(3));
			Assert.That(counting.ReadCalls, Is.LessThan(stats.LeafPages), "an early exit that actually stopped the walk touches far fewer leaves than a full scan would (a full scan touches every leaf at least once)");
		}

	}

}
