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

		/// <summary>Grafts <paramref name="run"/> and returns the number of pages it emitted.</summary>
		private static int Graft(FdbLiteTreeWriter writer, int pageSize, ReadOnlySpan<byte> begin, FdbLiteTreeWriter.CellRef[] run)
		{
			// the caller names the leaf the run falls in, which is what Task 6's driver will do too
			Span<uint> pathPages = stackalloc uint[20];
			Span<int> pathChildren = stackalloc int[20];
			uint leafId = writer.DescendToLeaf(begin, pathPages, pathChildren, out _);

			// one entry per emitted page, and the merged list is the run plus the boundary leaf's cells; a cell
			// costs at least one byte, so the page size is a safe (if generous) bound on that second term
			var output = new FdbLiteGraftedPage[run.Length + pageSize];
			return writer.GraftIntoGap(leafId, begin, run, FdbLiteImportOptions.Default.FillCeiling(pageSize), output);
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

			// graft 2,000 keys into the gap, which is several pages worth
			var writer = engine.BeginWrite();
			var run = BuildRun(1000, 2000);
			int emitted = Graft(writer, pageSize, GraftKey(1000), run);
			engine.Commit(writer, 2);

			var stats = engine.MeasureTreeStatistics();
			long capacity = (long) stats.LeafPages * pageSize;
			int fillPct = (int) (100.0 * stats.LeafLiveBytes / capacity);
			Log($"leavesBefore={leavesBefore} emitted={emitted} leavesAfter={stats.LeafPages} fill={fillPct}%");

			// the graft must not lose or duplicate a key, and the tree must still be in key order
			AssertStructurallySound(engine);
			var keys = ReadAllKeys(engine);
			Assert.That(keys.Count, Is.EqualTo(2200), "100 below + 2000 grafted + 100 above");
			Assert.That(keys, Is.Ordered, "the spliced separators must keep the tree sorted");
			Assert.That(keys[100], Is.EqualTo(1000L));
			Assert.That(keys[2099], Is.EqualTo(2999L));

			// the acceptance bar from the design: a run that owns its range packs like a sequential build
			Assert.That(fillPct, Is.GreaterThanOrEqualTo(90), "a run owning its range must pack near full");
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

	}

}
