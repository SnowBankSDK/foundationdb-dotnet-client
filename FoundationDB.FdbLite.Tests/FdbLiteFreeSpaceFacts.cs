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

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteFreeSpaceFacts : SimpleTest
	{

		[Test]
		public void Test_Delayed_Free_Promotes_By_Generation()
		{
			var map = new FdbLiteFreeSpaceMap();
			map.Free(10, 4, generation: 5);
			map.Free(50, 2, generation: 6);
			Assert.That(map.PendingRangeCount, Is.EqualTo(2));
			Assert.That(map.ReusableRangeCount, Is.Zero);

			// nothing reusable yet: allocation must fail
			Assert.That(map.TryAllocate(1, 1, 1024, out _), Is.False);

			map.Promote(reusableUpToInclusive: 5);
			Assert.That(map.PendingRangeCount, Is.EqualTo(1), "generation 6 stays pending");
			Assert.That(map.ReusableRangeCount, Is.EqualTo(1));
			Assert.That(map.TryAllocate(4, 1, 1024, out uint start), Is.True);
			Assert.That(start, Is.EqualTo(10));

			map.Promote(6);
			Assert.That(map.PendingRangeCount, Is.Zero);
			Assert.That(map.TryAllocate(2, 1, 1024, out start), Is.True);
			Assert.That(start, Is.EqualTo(50));
		}

		[Test]
		public void Test_TryAllocate_FromHighEnd_Takes_The_Highest_Reusable_Run()
		{
			// Placement bias: internal pages reuse from the LOW end, leaf pages from the HIGH end, so that over
			// the churn of copy-on-write the internal pages cluster near the start of the file. Same free set,
			// opposite ends.

			// low end (the existing default) takes the lowest run
			var low = new FdbLiteFreeSpaceMap();
			low.FreeImmediately(10, 1);
			low.FreeImmediately(100, 1);
			Assert.That(low.TryAllocate(1, 1, 1024, out uint lowStart, fromHighEnd: false), Is.True);
			Assert.That(lowStart, Is.EqualTo(10), "low-end reuse must take the lowest reusable run");

			// high end takes the highest run, leaving the low runs free for internal pages
			var high = new FdbLiteFreeSpaceMap();
			high.FreeImmediately(10, 1);
			high.FreeImmediately(100, 1);
			Assert.That(high.TryAllocate(1, 1, 1024, out uint highStart, fromHighEnd: true), Is.True);
			Assert.That(highStart, Is.EqualTo(100), "high-end reuse must take the highest reusable run");
		}

		[Test]
		public void Test_AllocatePage_Places_Leaf_High_And_Internal_Low()
		{
			// 1 block per page, so a page allocation is a single-block run and the test addresses are page ids
			var pager = new FdbLiteHeapPager(new FdbLiteGeometry(14, 0));

			// internal pages reuse from the LOW end
			var lowFree = new FdbLiteFreeSpaceMap();
			lowFree.FreeImmediately(10, 1);
			lowFree.FreeImmediately(100, 1);
			var internalAlloc = new FdbLiteBlockAllocator(pager, lowFree, frontier: 1000);
			Assert.That(internalAlloc.AllocatePage(fromHighEnd: false), Is.EqualTo(10u), "internal pages reuse the lowest free page");

			// leaf pages reuse from the HIGH end, leaving the low pages for internal pages
			var highFree = new FdbLiteFreeSpaceMap();
			highFree.FreeImmediately(10, 1);
			highFree.FreeImmediately(100, 1);
			var leafAlloc = new FdbLiteBlockAllocator(pager, highFree, frontier: 1000);
			Assert.That(leafAlloc.AllocatePage(fromHighEnd: true), Is.EqualTo(100u), "leaf pages reuse the highest free page");
		}

		[Test]
		public void Test_Churn_Clusters_Internal_Pages_Below_Leaf_Pages()
		{
			// Build a multi-level tree, then churn it: overwrite every key each generation so both leaves and their
			// ancestor internal pages are copied-on-write and reallocated from the free list. That reuse is where the
			// placement bias acts - leaves take high free pages, internal pages take low ones - so after enough churn
			// the internal tier should sit below the leaves. This is emergent, not absolute, so the assertion is on
			// the medians, not on every page.
			var pager = new FdbLiteHeapPager(FdbLiteGeometry.Hypothesis);
			var engine = FdbLiteEngine.Create(pager);
			using var cleanup = engine;
			engine.RetainFloor = ulong.MaxValue; // reclaim/reuse freed pages, so the bias has free pages to place into

			const int keys = 30000;
			for (int gen = 0; gen < 8; gen++)
			{
				var writer = engine.BeginWrite();
				for (int i = 0; i < keys; i++)
				{
					var key = new byte[8];
					System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key, i);
					var value = new byte[64];
					value[0] = (byte) gen;
					writer.Insert(key, value);
				}
				engine.Commit(writer, (ulong) (gen + 1));
			}

			var internalIds = new List<uint>();
			var leafIds = new List<uint>();
			var pin = engine.BeginRead();
			try
			{
				CollectPageIds(pager, pin.RootPageId, internalIds, leafIds);
			}
			finally
			{
				engine.EndRead(in pin);
			}

			Assert.That(internalIds, Is.Not.Empty, "the tree must have internal pages for this to mean anything");
			Assert.That(leafIds.Count, Is.GreaterThan(internalIds.Count), "leaves must dominate the page population");

			internalIds.Sort();
			leafIds.Sort();
			uint maxInternal = internalIds[^1];
			uint minLeaf = leafIds[0];
			// The bias reallocates internal pages from the low end and leaves from the high end, so after churn the
			// internal tier sits below the leaves. Without the bias the reallocated internal page lands amid the
			// leaves (it takes the same lowest-free page they compete for).
			Assert.That(maxInternal, Is.LessThan(minLeaf),
				$"placement bias must put the internal pages below the leaves (max internal id {maxInternal}, min leaf id {minLeaf}; internal n={internalIds.Count}, leaf n={leafIds.Count})");
		}

		private static void CollectPageIds(IFdbLitePager pager, uint pageId, List<uint> internalIds, List<uint> leafIds)
		{
			if (pageId == 0) { return; }
			var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage);
			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				leafIds.Add(pageId);
				return;
			}
			internalIds.Add(pageId);
			int children = FdbLiteTreePage.GetChildCount(page);
			for (int i = 0; i < children; i++)
			{
				CollectPageIds(pager, FdbLiteTreePage.GetChild(page, i), internalIds, leafIds);
			}
		}

		[Test]
		public void Test_Every_Allocated_Block_Is_Accounted_For()
		{
			// THE conservation invariant: every block below the allocation frontier is in exactly one place -
			// the three header blocks, the durable tree (pages and extents), the free-list chain, or the free
			// map (reusable or pending). A leaked free or a double-count is invisible to every structural
			// check, because the tree itself stays perfectly sound; only this equation sees it.
			var geometry = FdbLiteGeometry.Hypothesis;
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-tests");
			Directory.CreateDirectory(dir);
			var path = Path.Combine(dir, $"conservation-{Guid.NewGuid():N}.sbkv");
			try
			{
				using (var engine = FdbLiteEngine.Create(FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20)))
				{
					engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.FixedBudget(8);

					var rnd = new Random(9021);
					for (int gen = 0; gen < 10; gen++)
					{
						var writer = engine.BeginWrite();
						for (int i = 0; i < 400; i++)
						{ // inserts and overwrites, sizes scattered so pages splice, relocate, split and rebuild
							writer.Insert(Key(i), Value(gen * 1000 + i, rnd.Next(1, 400)));
						}
						for (int i = 0; i < 8; i++)
						{ // extent values, churned every generation (each overwrite frees the previous extent)
							writer.Insert(Key(10_000 + i), Value(gen * 100 + i, 60_000 + (i * 5_000)));
						}
						for (int i = gen * 30; i < (gen * 30) + 30; i++)
						{ // a moving delete band, so leaves underflow and the consolidation arm has work
							Assert.That(writer.Remove(Key(i)), Is.True);
						}
						engine.Commit(writer, (ulong) (gen + 1));
						AssertConservation(engine, $"after commit {gen + 1}");
					}

					for (int step = 0; step < 8; step++)
					{ // vacuum conserves at every step, and stops when it stops paying
						var outcome = engine.VacuumStep(16);
						AssertConservation(engine, $"after vacuum step {step}");
						if (outcome.PagesFreed <= 0) { break; }
					}
				}

				using (var reopened = FdbLiteEngine.Open(FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20)))
				{ // the free-list chain must round-trip the exact same accounting
					AssertConservation(reopened, "after reopen");
				}
			}
			finally
			{
				try { File.Delete(path); } catch { }
			}
		}

		[Test]
		public void Test_Small_Free_List_Rides_The_Slot_And_Round_Trips()
		{
			// format 4: a free list that fits the commit slot is serialized inline (FreeListRoot = 0, no
			// chain block written) and a reopen reloads the exact same map from the slot. The counter
			// asserts the inline path actually ran: a chain silently taken on every commit passes every
			// behavioural check and just writes a block per commit it should not.
			var geometry = FdbLiteGeometry.Default;
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-tests");
			Directory.CreateDirectory(dir);
			var path = Path.Combine(dir, $"inline-freelist-{Guid.NewGuid():N}.sbkv");
			try
			{
				(long Reusable, long Pending) before;
				long inlineCommits;
				using (var engine = FdbLiteEngine.Create(FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20)))
				{
					engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Off;
					var writer = engine.BeginWrite();
					for (int i = 0; i < 5_000; i++)
					{
						writer.Insert(Key(i), Value(i, 64));
					}
					engine.Commit(writer, 1);

					writer = engine.BeginWrite();
					Assert.That(writer.RemoveRange(Key(1_000), Key(2_000)), Is.EqualTo(1_000));
					engine.Commit(writer, 2);

					before = engine.MeasureFreeSpace();
					Assert.That(before.Reusable + before.Pending, Is.GreaterThan(0), "the deletes must have freed something, or the round-trip proves nothing");
					Assert.That(engine.Durable.FreeListRoot, Is.Zero, "a list this small must ride the slot");
					inlineCommits = engine.InlineFreeListCommitCount;
					Assert.That(inlineCommits, Is.EqualTo(2), "both commits must have taken the inline path");
					AssertConservation(engine, "before reopen");
				}

				using (var reopened = FdbLiteEngine.Open(FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20)))
				{
					Assert.That(reopened.MeasureFreeSpace(), Is.EqualTo(before), "the inline list must reload the exact same map");
					AssertConservation(reopened, "after reopen");
				}
			}
			finally
			{
				try { File.Delete(path); } catch { }
			}
		}

		[Test]
		public void Test_A_Large_Free_List_Falls_Back_To_The_Chain()
		{
			// 16 KiB uniform geometry (the smallest legal) holds 1,014 inline ranges; 2,600 one-block
			// extents with every other one freed leave ~1,300 non-coalescing ranges, which must take the
			// chain representation and keep round-tripping unchanged.
			var geometry = FdbLiteGeometry.Uniform(14);
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-tests");
			Directory.CreateDirectory(dir);
			var path = Path.Combine(dir, $"chain-freelist-{Guid.NewGuid():N}.sbkv");
			try
			{
				(long Reusable, long Pending) before;
				using (var engine = FdbLiteEngine.Create(FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20)))
				{
					engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Off;
					var writer = engine.BeginWrite();
					for (int i = 0; i < 2_600; i++)
					{ // above MaxInlineValueLength (4 KiB here): each value is its own one-block extent
						writer.Insert(Key(i), Value(i, 5_000));
					}
					engine.Commit(writer, 1);

					writer = engine.BeginWrite();
					for (int i = 1; i < 2_600; i += 2)
					{ // every other extent frees: alternating blocks, so the ranges cannot coalesce
						Assert.That(writer.Remove(Key(i)), Is.True);
					}
					engine.Commit(writer, 2);

					Assert.That(engine.Durable.FreeListRoot, Is.Not.Zero, "a list this fragmented cannot fit the slot: the chain must have been taken");
					before = engine.MeasureFreeSpace();
					AssertConservation(engine, "before reopen");
				}

				using (var reopened = FdbLiteEngine.Open(FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20)))
				{
					Assert.That(reopened.MeasureFreeSpace(), Is.EqualTo(before), "the chain must reload the exact same map");
					AssertConservation(reopened, "after reopen");
				}
			}
			finally
			{
				try { File.Delete(path); } catch { }
			}
		}

		private static byte[] Key(int i)
		{
			var key = new byte[8];
			System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key, i);
			return key;
		}

		private static byte[] Value(int seed, int length)
		{
			var value = new byte[length];
			new Random(seed).NextBytes(value);
			return value;
		}

		/// <summary>Asserts the conservation equation of one committed generation, in blocks: frontier = 3 headers + tree pages + extent blocks + chain blocks + reusable + pending.</summary>
		internal static void AssertConservation(FdbLiteEngine engine, string site)
		{
			var durable = engine.Durable;
			var geometry = engine.Pager.Geometry;
			var (treePages, extentBlocks) = MeasureFootprint(engine.Pager, durable.RootPageId);
			long chainBlocks = FdbLiteFreeListChain.CollectChainBlocks(engine.Pager, durable.FreeListRoot).Count;
			var (reusableBytes, pendingBytes) = engine.MeasureFreeSpace();
			long reusable = reusableBytes >> geometry.BlockSizeLog2;
			long pending = pendingBytes >> geometry.BlockSizeLog2;
			long accounted = 3 + (treePages * geometry.BlocksPerPage) + extentBlocks + chainBlocks + reusable + pending;
			Assert.That(accounted, Is.EqualTo((long) durable.AllocationFrontier),
				$"{site}: conservation broke - frontier {durable.AllocationFrontier} vs 3 header + {treePages} tree pages x {geometry.BlocksPerPage} + {extentBlocks} extent + {chainBlocks} chain + {reusable} reusable + {pending} pending blocks");
		}

		/// <summary>Pages and extent blocks reachable from <paramref name="pageId"/> (the durable tree's whole footprint).</summary>
		private static (long Pages, long ExtentBlocks) MeasureFootprint(IFdbLitePager pager, uint pageId)
		{
			if (pageId == 0) { return (0, 0); }
			var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage);
			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				long extentBlocks = 0;
				int count = FdbLitePageHeader.GetCellCount(page);
				for (int i = 0; i < count; i++)
				{
					if ((FdbLiteTreePage.GetLeafFlags(page, i) & FdbLiteTreePage.FlagValueIsExtent) != 0)
					{
						extentBlocks += FdbLiteTreePage.GetLeafExtentDescriptor(page, i).BlockCount;
					}
				}
				return (1, extentBlocks);
			}
			long pages = 1;
			long extents = 0;
			int children = FdbLiteTreePage.GetChildCount(page);
			for (int i = 0; i < children; i++)
			{
				var (p, e) = MeasureFootprint(pager, FdbLiteTreePage.GetChild(page, i));
				pages += p;
				extents += e;
			}
			return (pages, extents);
		}

		/// <summary>Pager decorator recording hole punches, so the promotion-time punch is assertable without a filesystem.</summary>
		private sealed class PunchRecordingPager(IFdbLitePager inner) : IFdbLitePager
		{
			public List<(uint Start, uint Count)> Punched { get; } = [ ];

			public FdbLiteGeometry Geometry => inner.Geometry;
			public uint BlockCount => inner.BlockCount;
			public uint RegionSizeInBlocks => inner.RegionSizeInBlocks;
			public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count) => inner.ReadBlocks(firstBlock, count);
			public FdbLitePageRef ReadBlocksRef(uint firstBlock, int count) => inner.ReadBlocksRef(firstBlock, count);
			public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data) => inner.WriteBlocks(firstBlock, data);
			public void Flush() => inner.Flush();
			public void Grow(uint minimumBlockCount) => inner.Grow(minimumBlockCount);
			public void Truncate(uint newBlockCount) => inner.Truncate(newBlockCount);
			public void PunchHole(uint firstBlock, uint count) { this.Punched.Add((firstBlock, count)); inner.PunchHole(firstBlock, count); }
			public void Prefetch(uint firstBlock, uint count) => inner.Prefetch(firstBlock, count);
			public bool TrackFirstTouch { get => inner.TrackFirstTouch; set => inner.TrackFirstTouch = value; }
			public bool MarkTouched(uint firstBlock) => inner.MarkTouched(firstBlock);

			public void ResetFirstTouch() => inner.ResetFirstTouch();
			public void Dispose() => inner.Dispose();
		}

		[Test]
		public void Test_Promotion_Punches_Freed_Page_Runs()
		{
			// the punch rides promotion because that is the first moment neither retained header nor any pin
			// can reference the blocks: the same fence that makes REUSE safe makes releasing the space safe
			var recording = new PunchRecordingPager(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			using var engine = FdbLiteEngine.Create(recording);
			uint blocksPerPage = (uint) engine.Pager.Geometry.BlocksPerPage;

			var writer = engine.BeginWrite();
			for (int i = 0; i < 5_000; i++)
			{
				var key = new byte[8];
				System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, i);
				writer.Insert(key, new byte[200]);
			}
			engine.Commit(writer, 1);

			writer = engine.BeginWrite();
			var begin = new byte[8];
			var end = new byte[8];
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(begin, 500);
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(end, 4_500);
			Assert.That(writer.RemoveRange(begin, end), Is.EqualTo(4_000));
			engine.Commit(writer, 2);
			Assert.That(recording.Punched, Is.Empty, "freed blocks are still pending: the backup header can reference them");

			// two more (empty) generations move the promote limit past the clear's frees
			engine.Commit(engine.BeginWrite(), 3);
			engine.Commit(engine.BeginWrite(), 4);

			Assert.That(recording.Punched, Is.Not.Empty, "promotion must punch the freed page runs");
			long punchedBlocks = 0;
			foreach (var (start, count) in recording.Punched)
			{
				Assert.That(count, Is.GreaterThanOrEqualTo(blocksPerPage), "sub-page fragments are kept, not punched");
				Assert.That(start, Is.GreaterThanOrEqualTo(3u), "the header blocks are never punched");
				Assert.That((long) start + count, Is.LessThanOrEqualTo(engine.Durable.AllocationFrontier), "punches stay below the frontier");
				punchedBlocks += count;
			}
			Log($"# punched {recording.Punched.Count} run(s), {punchedBlocks} blocks");
			Assert.That(punchedBlocks * engine.Pager.Geometry.BlockSize, Is.GreaterThan(700L << 10), "clearing 4,000 x 200 B (~830 KB live) must release most of it, coalesced into runs");
			FdbLiteFreeSpaceFacts.AssertConservation(engine, "after promotion punched");

			// the knob: a store that opts out must never see a punch
			var silent = new PunchRecordingPager(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			using var optOut = FdbLiteEngine.Create(silent);
			optOut.PunchFreedSpace = false;
			writer = optOut.BeginWrite();
			for (int i = 0; i < 2_000; i++)
			{
				var key = new byte[8];
				System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, i);
				writer.Insert(key, new byte[200]);
			}
			optOut.Commit(writer, 1);
			writer = optOut.BeginWrite();
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(begin, 0);
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(end, 2_000);
			writer.RemoveRange(begin, end);
			optOut.Commit(writer, 2);
			optOut.Commit(optOut.BeginWrite(), 3);
			optOut.Commit(optOut.BeginWrite(), 4);
			Assert.That(silent.Punched, Is.Empty, "PunchFreedSpace=false must suppress every punch");
		}

		[Test]
		public void Test_RetainFloor_Cannot_Be_Raised_Over_Retained_Generations()
		{
			// raising the floor re-enables promotion of generations the old floor retained - and readers of a
			// retain-all store hold no engine pins, so their pages would be reused under them, silently
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			using var cleanup = engine;
			engine.RetainFloor = 0; // fresh store, nothing retained yet: the change is legal

			for (int gen = 0; gen < 2; gen++)
			{ // overwrite the same keys so the second commit frees (and retains) the first generation's pages
				var writer = engine.BeginWrite();
				for (int i = 0; i < 40; i++)
				{
					var key = new byte[8];
					System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key, i);
					writer.Insert(key, new byte[300]);
				}
				engine.Commit(writer, (ulong) (gen + 1));
			}
			Assume.That(engine.GetStats().PendingReclaimBlocks, Is.GreaterThan(0), "the churn must have left retained blocks");

			Assert.That(() => engine.RetainFloor = ulong.MaxValue, Throws.Exception, "raising over retained generations must fail");
			Assert.That(() => engine.RetainFloor = 0, Throws.Nothing, "restating (or lowering) stays legal");
		}

		[Test]
		public void Test_Dispose_Refuses_While_A_Reader_Is_Pinned()
		{
			// disposing unmaps the store: a pinned reader's spans would dereference unmapped memory, a native
			// fault rather than a managed exception, so the pinned dispose must be the thing that fails
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var writer = engine.BeginWrite();
			writer.Insert("hello"u8, "world"u8);
			engine.Commit(writer, 1);

			var pin = engine.BeginRead();
			Assert.That(() => engine.Dispose(), Throws.Exception, "dispose must refuse while a reader is pinned");
			engine.EndRead(in pin);
			Assert.That(() => engine.Dispose(), Throws.Nothing, "the last unpin makes dispose legal");
		}

		[Test]
		public void Test_RetainFloor_Is_The_Retention_Policy_Knob()
		{
			// identical churn on two engines: one retains every generation (RetainFloor = 0), one reclaims (the
			// production default). Same mechanism, opposite policies - the boundary this test keeps load-bearing.
			long retainAll = ChurnAndMeasurePending(retainFloor: 0);
			long reclaiming = ChurnAndMeasurePending(retainFloor: ulong.MaxValue);

			// retain-all never promotes a freed block, so every generation's frees pile up unreclaimed; the default
			// promotes each generation's frees two generations later (and reuses them), so almost nothing is pending
			Assert.That(retainAll, Is.GreaterThan(reclaiming),
				$"RetainFloor=0 must retain every generation's freed blocks (pending {retainAll}); the default must reclaim them (pending {reclaiming})");
		}

		private static long ChurnAndMeasurePending(ulong retainFloor)
		{
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Hypothesis));
			using var cleanup = engine;
			engine.RetainFloor = retainFloor;

			for (int gen = 0; gen < 10; gen++)
			{ // overwrite the same 40 keys each generation, so every commit frees the previous generation's pages/extents
				var writer = engine.BeginWrite();
				for (int i = 0; i < 40; i++)
				{
					var key = new byte[8];
					System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key, i);
					var value = new byte[300];
					value[0] = (byte) gen;
					writer.Insert(key, value);
				}
				engine.Commit(writer, (ulong) (gen + 1));
			}
			return engine.GetStats().PendingReclaimBlocks;
		}

		[Test]
		public void Test_Adjacent_Ranges_Coalesce()
		{
			var map = new FdbLiteFreeSpaceMap();
			map.FreeImmediately(10, 2);
			map.FreeImmediately(14, 2);
			Assert.That(map.ReusableRangeCount, Is.EqualTo(2));

			// the middle piece bridges both neighbors into one range
			map.FreeImmediately(12, 2);
			Assert.That(map.ReusableRangeCount, Is.EqualTo(1));
			Assert.That(map.ReusableBlockCount, Is.EqualTo(6));

			// which can now serve a single 6-block run
			Assert.That(map.TryAllocate(6, 1, 1024, out uint start), Is.True);
			Assert.That(start, Is.EqualTo(10));
			Assert.That(map.ReusableRangeCount, Is.Zero);
		}

		[Test]
		public void Test_Allocation_Honors_Alignment_And_Splits()
		{
			var map = new FdbLiteFreeSpaceMap();
			map.FreeImmediately(5, 20); // [5, 25)

			// aligned-4 run of 4: first candidate inside the range is 8
			Assert.That(map.TryAllocate(4, 4, 1024, out uint start), Is.True);
			Assert.That(start, Is.EqualTo(8));

			// the take split the range into [5,8) and [12,25)
			Assert.That(map.ReusableRangeCount, Is.EqualTo(2));
			Assert.That(map.ReusableBlockCount, Is.EqualTo(16));

			// an aligned-8 run of 8 fits at 16
			Assert.That(map.TryAllocate(8, 8, 1024, out start), Is.True);
			Assert.That(start, Is.EqualTo(16));
		}

		[Test]
		public void Test_Allocation_Never_Straddles_A_Region_Boundary()
		{
			var map = new FdbLiteFreeSpaceMap();
			// a free range crossing the boundary at 64: [60, 70)
			map.FreeImmediately(60, 10);

			// 6 unaligned blocks fit only at 64 (any earlier start straddles)
			Assert.That(map.TryAllocate(6, 1, regionSizeInBlocks: 64, out uint start), Is.True);
			Assert.That(start, Is.EqualTo(64));

			// 5 remaining: [60,64) + [70,70)... the tail [70) is exhausted, so only [60,64) is left
			Assert.That(map.ReusableBlockCount, Is.EqualTo(4));

			// a 5-block run cannot be served anymore (4 below the boundary, straddle forbidden)
			Assert.That(map.TryAllocate(5, 1, 64, out _), Is.False);
		}

		[Test]
		public void Test_Block_Allocator_Pages_Are_Aligned_And_Extents_Reuse_Freed_Space()
		{
			foreach (var geometry in TestGeometries.All)
			{
				using var pager = new FdbLiteHeapPager(geometry);
				var allocator = new FdbLiteBlockAllocator(pager, new FdbLiteFreeSpaceMap(), frontier: 3);

				uint page1 = allocator.AllocatePage();
				uint page2 = allocator.AllocatePage();
				uint blocksPerPage = (uint) geometry.BlocksPerPage;
				Assert.That(page1 % blocksPerPage, Is.Zero, $"[{geometry}] pages are page-aligned");
				Assert.That(page2 % blocksPerPage, Is.Zero);
				Assert.That(page2, Is.GreaterThanOrEqualTo(page1 + blocksPerPage), "pages do not overlap");
				Assert.That(pager.BlockCount, Is.GreaterThanOrEqualTo(allocator.Frontier), "the pager grew to cover the frontier");

				// free page1 at generation 5; before promotion the allocator must NOT hand it back
				allocator.Free(page1, blocksPerPage, freedAtGeneration: 5);
				uint page3 = allocator.AllocatePage();
				Assert.That(page3, Is.Not.EqualTo(page1), $"[{geometry}] unpromoted space must not be reused");

				// after promotion, the freed page is the first candidate again
				allocator.FreeSpace.Promote(5);
				uint page4 = allocator.AllocatePage();
				Assert.That(page4, Is.EqualTo(page1), $"[{geometry}] promoted space is reused first");
			}
		}

		[Test]
		public void Test_Block_Allocator_Skips_Region_Boundary_And_Recycles_The_Gap()
		{
			var geometry = FdbLiteGeometry.Uniform(14);
			using var pager = new FdbLiteHeapPager(geometry); // 1 MiB regions = 64 blocks of 16 KiB
			var allocator = new FdbLiteBlockAllocator(pager, new FdbLiteFreeSpaceMap(), frontier: 0);
			uint regionBlocks = pager.RegionSizeInBlocks;

			// fill most of the first region, leaving 3 blocks below the boundary
			uint a = allocator.AllocateExtent(regionBlocks - 3);
			Assert.That(a, Is.Zero);

			// a 5-block extent cannot straddle: it must start at the next region, and the 3-block gap is recycled
			uint b = allocator.AllocateExtent(5);
			Assert.That(b, Is.EqualTo(regionBlocks));

			uint gap = allocator.AllocateExtent(3);
			Assert.That(gap, Is.EqualTo(regionBlocks - 3), "the skipped gap is immediately reusable");
		}

		[Test]
		public void Test_Free_List_Chain_Roundtrip()
		{
			// small blocks (4 KiB) so a few hundred entries force a multi-block chain
			var geometry = new FdbLiteGeometry(12, 2);
			using var pager = new FdbLiteHeapPager(geometry);
			var map = new FdbLiteFreeSpaceMap();
			var allocator = new FdbLiteBlockAllocator(pager, map, frontier: 3);
			pager.Grow(4096);

			map.FreeImmediately(50, 7);
			map.FreeImmediately(80, 1);
			for (int i = 0; i < 300; i++)
			{
				map.Free(1_000 + (uint) (i * 3), 2, generation: (ulong) (100 + i));
			}

			uint root = FdbLiteFreeListChain.Persist(map, allocator, pager, generation: 500);
			Assert.That(root, Is.Not.Zero);
			Assert.That(FdbLiteFreeListChain.CollectChainBlocks(pager, root), Has.Count.EqualTo(2), "~302 entries at 254 per 4 KiB block = 2 blocks");

			// the chain's own allocation mutates the map, so the comparison target is the POST-persist state
			var loaded = FdbLiteFreeListChain.Load(pager, root, expectedGeneration: 500);
			Assert.That(loaded.TotalRangeCount, Is.EqualTo(map.TotalRangeCount));
			Assert.That(loaded.Enumerate(), Is.EqualTo(map.Enumerate()), "loaded state must be identical, in order");

			// pending entries must still promote correctly after a reload: generations 100..150 promote
			int pendingBefore = loaded.PendingRangeCount;
			loaded.Promote(150);
			Assert.That(loaded.PendingRangeCount, Is.EqualTo(pendingBefore - 51));
			Assert.That(loaded.TryAllocate(2, 1, pager.RegionSizeInBlocks, out _), Is.True);
		}

		[Test]
		public void Test_Free_List_Chain_Rejects_Corruption()
		{
			var geometry = FdbLiteGeometry.Uniform(14);
			using var pager = new FdbLiteHeapPager(geometry);
			var map = new FdbLiteFreeSpaceMap();
			var allocator = new FdbLiteBlockAllocator(pager, map, frontier: 3);
			pager.Grow(128);
			map.FreeImmediately(100, 5);

			uint root = FdbLiteFreeListChain.Persist(map, allocator, pager, generation: 9);

			// flip one payload byte in the chain block
			var corrupted = pager.ReadBlocks(root, 1).ToArray();
			corrupted[^1] ^= 0xFF;
			pager.WriteBlocks(root, corrupted);

			Assert.That(() => FdbLiteFreeListChain.Load(pager, root, 9), Throws.InstanceOf<InvalidDataException>());
		}

		[Test]
		public void Test_Allocator_Fuzz_Against_Reference_Model()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var rnd = new Random(4242);
				using var pager = new FdbLiteHeapPager(geometry);
				var allocator = new FdbLiteBlockAllocator(pager, new FdbLiteFreeSpaceMap(), frontier: 3);

				// model: every live allocation, as (start, count)
				var live = new List<(uint Start, uint Count)>();
				var freedAt = new List<(ulong Gen, uint Start, uint Count)>();
				ulong generation = 1;

				for (int step = 0; step < 2_000; step++)
				{
					switch (rnd.Next(10))
					{
						case < 4:
						{ // allocate a page
							uint start = allocator.AllocatePage();
							AssertFresh(start, (uint) geometry.BlocksPerPage);
							Assert.That(start % (uint) geometry.BlocksPerPage, Is.Zero);
							live.Add((start, (uint) geometry.BlocksPerPage));
							break;
						}
						case < 7:
						{ // allocate an extent
							uint count = (uint) rnd.Next(1, 12);
							uint start = allocator.AllocateExtent(count);
							AssertFresh(start, count);
							uint region = start / pager.RegionSizeInBlocks;
							Assert.That((start + count - 1) / pager.RegionSizeInBlocks, Is.EqualTo(region), "no extent straddles a region");
							live.Add((start, count));
							break;
						}
						case < 9 when live.Count > 0:
						{ // free a random live allocation at the current generation
							int victim = rnd.Next(live.Count);
							var (start, count) = live[victim];
							live.RemoveAt(victim);
							allocator.Free(start, count, generation);
							freedAt.Add((generation, start, count));
							break;
						}
						default:
						{ // close the generation and promote what a 2-root retention would allow
							generation++;
							if (generation >= 2)
							{
								allocator.FreeSpace.Promote(generation - 2);
							}
							break;
						}
					}
				}

				// invariant helper: a fresh allocation may not overlap any LIVE allocation, nor any
				// UNPROMOTED freed range (those may still be referenced by the retained roots)
				void AssertFresh(uint start, uint count)
				{
					foreach (var (s, c) in live)
					{
						Assert.That(start + count <= s || s + c <= start, Is.True, $"[{geometry}] overlap with live [{s},{s + c}) by [{start},{start + count})");
					}
					foreach (var (g, s, c) in freedAt)
					{
						if (g > generation - 2 || generation < 2)
						{ // not yet promotable at the time of this allocation
							Assert.That(start + count <= s || s + c <= start, Is.True, $"[{geometry}] overlap with unpromoted freed [{s},{s + c}) (gen {g}) by [{start},{start + count})");
						}
					}
					Assert.That(start + count, Is.LessThanOrEqualTo(pager.BlockCount), "allocation within the grown file");
				}
			}
		}

	}

}
