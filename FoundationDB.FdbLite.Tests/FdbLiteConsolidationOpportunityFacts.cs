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

	/// <summary>Diagnostic: how much space would same-parent leaf consolidation reclaim, per workload shape?</summary>
	/// <remarks>
	/// <para>Measures the OPPORTUNITY for the vacuum / pre-commit consolidation / spill-on-split family before
	/// any of those arms is built, per the house discipline (measure, record, then decide). Reports rather than
	/// asserts thresholds, because the point is to find out; marked Explicit so the finding is gathered on
	/// demand instead of taxing every suite run.</para>
	/// <para>The analysis is deliberately OUTSIDE the engine: it re-derives leaf layouts from the raw pages
	/// (headers are public, the rest is offset arithmetic, same approach as the page-accounting oracle) and
	/// replicates the LeafRunBytes sizing formula, prefix-sensitively: a merged run's cost is computed against
	/// the LCP the DESTINATION page would strip, which shortens when foreign keys arrive - ignoring that
	/// re-expansion is exactly the shape of the historical prefix-boundary defect.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	[Explicit("diagnostic: consolidation-opportunity measurement, run on demand")]
	public class FdbLiteConsolidationOpportunityFacts : SimpleTest
	{

		private static byte[] Key(long i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		#region Raw-layout analysis (independent of the page accessors)...

		private sealed record LeafShape(uint PageId, int CellCount, long SumWholeKeyBytes, long SumValueBytes, byte[] FirstWholeKey, byte[] LastWholeKey);

		private static LeafShape ParseLeaf(ReadOnlySpan<byte> page, uint pageId)
		{
			int count = FdbLitePageHeader.GetCellCount(page);
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			var prefix = page.Slice(32, prefixLen);
			int slotsAt = 32 + ((prefixLen + 1) & ~1);
			int keyBase = slotsAt + (count * 2);

			long sumWhole = 0, sumValue = 0;
			byte[] first = [ ], last = [ ];
			for (int i = 0; i < count; i++)
			{
				int entry = keyBase + BinaryPrimitives.ReadUInt16LittleEndian(page[(slotsAt + (i * 2))..]);
				int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]);
				int f = entry + 2 + keyLen;
				sumWhole += prefixLen + keyLen;
				sumValue += BinaryPrimitives.ReadUInt16LittleEndian(page[(f + 2)..]);
				if (i == 0 || i == count - 1)
				{
					var whole = new byte[prefixLen + keyLen];
					prefix.CopyTo(whole);
					page.Slice(entry + 2, keyLen).CopyTo(whole.AsSpan(prefixLen));
					if (i == 0) { first = whole; }
					if (i == count - 1) { last = whole; }
				}
			}
			return new(pageId, count, sumWhole, sumValue, first, last.Length > 0 ? last : first);
		}

		/// <summary>Replicates LeafRunBytes: full page footprint of a run stored against <paramref name="lcp"/>.</summary>
		private static long RunBytes(int count, long sumWhole, long sumValue, int lcp)
		{
			int effective = count > 1 ? lcp : 0;
			return 32 + ((effective + 1) & ~1) + (sumWhole - ((long) count * effective)) + ((long) count * 9) + sumValue;
		}

		private static int CommonPrefixLength(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
		{
			int n = Math.Min(a.Length, b.Length);
			int i = 0;
			while (i < n && a[i] == b[i]) { ++i; }
			return i;
		}

		/// <summary>Walks the committed tree, returning leaves in key order grouped by parent (only leaf-parented groups).</summary>
		private static List<List<LeafShape>> CollectSiblingGroups(IFdbLitePager pager, uint root)
		{
			var groups = new List<List<LeafShape>>();
			if (root != 0)
			{
				Walk(pager, root, groups);
			}
			return groups;

			static void Walk(IFdbLitePager pager, uint pageId, List<List<LeafShape>> groups)
			{
				var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage).ToArray();
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{ // a root that is itself a leaf: a degenerate single-leaf group
					groups.Add([ ParseLeaf(page, pageId) ]);
					return;
				}

				int count = FdbLitePageHeader.GetCellCount(page);
				var children = new uint[count + 1];
				children[0] = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(32));
				for (int i = 0; i < count; i++)
				{ // internal slots start at 36 (leftmost child u32 after the header; internal pages strip no prefix)
					int off = BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(36 + (i * 2)));
					children[i + 1] = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(off));
				}

				var firstChild = pager.ReadBlocks(children[0], pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(firstChild) == FdbLitePageType.Leaf)
				{ // all siblings of a leaf are leaves: this internal page parents one group
					var group = new List<LeafShape>(children.Length);
					foreach (var child in children)
					{
						group.Add(ParseLeaf(pager.ReadBlocks(child, pager.Geometry.BlocksPerPage), child));
					}
					groups.Add(group);
					return;
				}
				foreach (var child in children)
				{
					Walk(pager, child, groups);
				}
			}
		}

		/// <summary>Greedy same-parent run packing: how many pages would consolidation reclaim, at a fill target?</summary>
		private static int GreedyPagesSaved(List<List<LeafShape>> groups, int pageSize, double fillTarget)
		{
			long budget = (long) (pageSize * fillTarget);
			int saved = 0;
			foreach (var group in groups)
			{
				int i = 0;
				while (i < group.Count)
				{
					// extend the run [i..j] while the combined cells still fit ONE page at the target
					int count = group[i].CellCount;
					long sumWhole = group[i].SumWholeKeyBytes, sumValue = group[i].SumValueBytes;
					int j = i;
					while (j + 1 < group.Count)
					{
						var next = group[j + 1];
						int lcp = CommonPrefixLength(group[i].FirstWholeKey, next.LastWholeKey);
						if (RunBytes(count + next.CellCount, sumWhole + next.SumWholeKeyBytes, sumValue + next.SumValueBytes, lcp) > budget)
						{
							break;
						}
						count += next.CellCount;
						sumWhole += next.SumWholeKeyBytes;
						sumValue += next.SumValueBytes;
						j++;
					}
					saved += j - i;
					i = j + 1;
				}
			}
			return saved;
		}

		private void Report(string workload, FdbLiteEngine engine, FdbLiteTreeWriter lastWriter)
		{
			var stats = engine.MeasureTreeStatistics();
			var groups = CollectSiblingGroups(engine.Pager, engine.Durable.RootPageId);
			int pageSize = engine.Pager.Geometry.PageSize;
			int savedFull = GreedyPagesSaved(groups, pageSize, fillTarget: 1.0);
			int savedSafe = GreedyPagesSaved(groups, pageSize, fillTarget: 0.90);

			Log($"# [{workload}] leaves={stats.LeafPages:N0} internals={stats.InternalPages:N0} cells={stats.CellCount:N0}");
			Log($"# [{workload}] wasted={stats.WastedBytes:N0} B (max/page {stats.MaxWastedBytesPerPage:N0}) freeGap={stats.FreeGapBytes:N0} B of {(long) stats.LeafPages * pageSize:N0} B in leaves");
			Log($"# [{workload}] consolidation could reclaim {savedFull:N0} pages packed full, {savedSafe:N0} pages at a 90% fill target ({100.0 * savedSafe / Math.Max(1, stats.LeafPages):N1}% of leaves)");
			Log($"# [{workload}] last generation: splits={lastWriter.PageSplits} appended={lastWriter.PagesAppended} stripped={lastWriter.PagesStripped} removedInPlace={lastWriter.CellsRemovedInPlace:N0} overwritten={lastWriter.CellsOverwritten:N0}");
		}

		#endregion

		/// <summary>The task-trail ("cheese holes") shape: entries appended at the edge, most of an older window deleted, some survivors kept forever.</summary>
		[Test]
		public void Diagnose_Task_Trail_Workload()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[32];

			const int BATCH = 2_000;
			const int GENS = 12;
			long expected = 0;
			FdbLiteTreeWriter w = null!;
			for (int g = 0; g < GENS; g++)
			{
				w = engine.BeginWrite();
				long edge = (long) g * BATCH;
				for (long i = edge; i < edge + BATCH; i++)
				{ // new tasks arrive at the advancing edge
					w.Insert(Key(i), value);
					expected++;
				}
				if (g > 0)
				{ // the PREVIOUS batch gets processed: 80% of its entries are deleted, every 5th survives
					for (long i = edge - BATCH; i < edge; i++)
					{
						if (i % 5 != 0)
						{
							Assert.That(w.Remove(Key(i)), Is.True, $"gen {g}: task {i} must exist");
							expected--;
						}
					}
				}
				engine.Commit(w, (ulong) (g + 1));
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) expected), "sanity: the instrument must not be measuring a broken tree");
			Report("task-trail", engine, w);
		}

		/// <summary>Random churn over a bounded keyspace: mixed inserts and deletes, holes everywhere.</summary>
		[Test]
		public void Diagnose_Random_Churn_Workload()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var rnd = new Random(20260729);
			var model = new HashSet<long>();

			const int KEYSPACE = 30_000;
			const int GENS = 8;
			FdbLiteTreeWriter w = null!;
			for (int g = 0; g < GENS; g++)
			{
				w = engine.BeginWrite();
				for (int op = 0; op < 6_000; op++)
				{
					long k = rnd.Next(KEYSPACE);
					if (rnd.Next(10) < 6 || !model.Contains(k))
					{
						var value = new byte[rnd.Next(0, 100)];
						w.Insert(Key(k), value);
						model.Add(k);
					}
					else
					{
						Assert.That(w.Remove(Key(k)), Is.True);
						model.Remove(k);
					}
				}
				engine.Commit(w, (ulong) (g + 1));
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count), "sanity: the instrument must not be measuring a broken tree");
			Report("random-churn", engine, w);
		}

		/// <summary>Control: pure same-length replace churn creates no holes, so the instrument must read near zero here.</summary>
		[Test]
		public void Diagnose_Replace_Only_Control()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

			const int N = 20_000;
			var value = new byte[16];
			var seed = engine.BeginWrite();
			for (int i = 0; i < N; i++)
			{
				seed.Insert(Key(i), value);
			}
			engine.Commit(seed, 1);

			FdbLiteTreeWriter w = null!;
			for (int g = 0; g < 4; g++)
			{
				w = engine.BeginWrite();
				for (int i = 0; i < N; i++)
				{
					BinaryPrimitives.WriteInt32LittleEndian(value, g);
					w.Insert(Key(i), value);
				}
				engine.Commit(w, (ulong) (g + 2));
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) N));
			Report("replace-control", engine, w);
		}

	}

}
