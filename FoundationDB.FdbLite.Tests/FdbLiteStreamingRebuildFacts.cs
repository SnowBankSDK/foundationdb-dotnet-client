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

// the streaming rebuild is a net10+ prototype (`allows ref struct`); the materialized path is the only one on net8
#if NET10_0_OR_GREATER

namespace FoundationDB.Storage.FdbLite.Tests
{
	using FoundationDB.Storage.FdbLite;

	/// <summary>Differential proof for the streaming leaf rebuild: the streamed writer and the materialized <c>CellRef[]</c> writer must produce BYTE-IDENTICAL stores on the same workload.</summary>
	/// <remarks>Two separate green suites would never prove the paths agree; this fixture runs both on the same deterministic workload and compares every block of the resulting stores. The counter assert is the execution proof: a toggle that silently routes back to the materialized path would pass the comparison while testing nothing.</remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteStreamingRebuildFacts : SimpleTest
	{

		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) CreateStore(FdbLiteGeometry geometry, bool streaming)
		{
			var pager = new FdbLiteHeapPager(geometry);
			var allocator = new FdbLiteBlockAllocator(pager, new FdbLiteFreeSpaceMap(), frontier: 3);
			var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0)
			{
				UseStreamingRebuild = streaming,
			};
			return (pager, writer);
		}

		/// <summary>Split-heavy mixed workload: bucketed random keys (shared prefixes, interior inserts), replaces of both growing and shrinking sizes, and a sprinkle of extent values.</summary>
		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) RunMixedWorkload(bool streaming)
		{
			var geometry = FdbLiteGeometry.Uniform(14); // 16 KiB pages: the floor, so splits come fast
			var (pager, writer) = CreateStore(geometry, streaming);

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

		/// <summary>Giant-cell workload: maximum-size keys and near-inline-ceiling values, so a page holds very few cells and the K-way (no legal 2-way cut) split branch is exercised.</summary>
		private static (FdbLiteHeapPager Pager, FdbLiteTreeWriter Writer) RunGiantCellWorkload(bool streaming)
		{
			var geometry = FdbLiteGeometry.Uniform(14);
			var (pager, writer) = CreateStore(geometry, streaming);

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
			// execution proof FIRST: a knob nobody consults would make every assert below pass vacuously
			Assert.That(streamed.Writer.StreamedLeafRebuilds, Is.GreaterThan(0), "the streaming path never executed: the toggle is dead and the byte comparison proves nothing");
			Assert.That(baseline.Writer.StreamedLeafRebuilds, Is.Zero, "the baseline store must not stream, or there is no differential");

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
			var baseline = RunMixedWorkload(streaming: false);
			var streamed = RunMixedWorkload(streaming: true);
			AssertStoresIdentical(baseline, streamed);

			// the single-pass fast path must carry the non-splitting majority AND the split fallback must still
			// run: byte-identical output makes either regression invisible without the counters
			Assert.That(streamed.Writer.StreamedSinglePassRebuilds, Is.GreaterThan(0), "no rebuild took the single-pass fast path: it is dead or its fit accounting always aborts");
			Assert.That(streamed.Writer.StreamedSinglePassRebuilds, Is.LessThan(streamed.Writer.StreamedLeafRebuilds), "every rebuild took the fast path, so the split fallback never ran and this workload no longer covers it");
		}

		[Test]
		public void Streaming_Rebuild_Matches_Materialized_On_Giant_Cells()
		{
			var baseline = RunGiantCellWorkload(streaming: false);
			var streamed = RunGiantCellWorkload(streaming: true);
			AssertStoresIdentical(baseline, streamed);
		}

	}

}

#endif
