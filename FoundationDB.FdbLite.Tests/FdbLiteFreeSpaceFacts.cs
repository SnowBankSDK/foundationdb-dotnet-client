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
