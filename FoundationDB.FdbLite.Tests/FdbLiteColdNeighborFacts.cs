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
	using System.Diagnostics;
	using FoundationDB.Storage.FdbLite;

	/// <summary>Diagnostic: what does the cold-neighbor variant of pre-commit consolidation buy, and what does the cold read cost on the FILE pager (OQ-6)?</summary>
	/// <remarks>
	/// <para>Two halves, deterministic first. The DELTA half replays workloads on a file-backed store and, at
	/// each generation boundary, compares the dirty-only merge pass against one allowed to pull at most ONE
	/// adjacent cold sparse leaf per run edge (the variant as designed: write count stays flat, the extension
	/// only adds a read). Headlines are counts and bytes. The COST half times the actual cold-pull read path
	/// (<c>ReadBlocks</c> + checksum verify, exactly what the writer pays on first touch of a cold page)
	/// against a fresh mapping and again warm, in shuffled order so OS readahead does not flatter it.</para>
	/// <para>Honesty note on "cold": the store file was written moments earlier, so a first touch after reopen
	/// pays MAPPING faults served from the system file cache - a lower bound on a genuinely disk-cold read. A
	/// disk-cold number needs working-set eviction (elevated RAMMap) and tens of GB; deliberately out of this
	/// diagnostic's scope, and the report says so wherever the numbers travel.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	[Explicit("diagnostic: cold-neighbor consolidation cost/win measurement on the file pager, run on demand")]
	public class FdbLiteColdNeighborFacts : SimpleTest
	{

		private static byte[] Key(long i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		private static string NewStorePath() => Path.Combine(Path.GetTempPath(), $"fdblite-vacuum-{Guid.NewGuid():N}.dat");

		#region Cold-neighbor merge what-if...

		private const double Underflow = 0.50;

		private const double Target = 0.90;

		private sealed record ColdDelta(int DirtyRuns, int DirtyFreed, int DirtyPagesOut, int ExtFreed, int ExtPagesOut, int ExtDirtyPages, int ColdReads);

		/// <summary>Simulates the pre-commit pass twice on one snapshot: dirty candidates only, then with each run allowed to pull at most one adjacent cold sparse leaf per edge.</summary>
		private static ColdDelta SimulateColdExtension(VacuumTreeSnapshot snap, HashSet<uint> dirty, int pageSize)
		{
			long budget = (long) (Target * pageSize);
			int dirtyRuns = 0, dirtyFreed = 0, dirtyPagesOut = 0, extFreed = 0, extPagesOut = 0, extDirtyPages = 0, coldReads = 0;

			foreach (var group in snap.Groups)
			{
				int i = 0;
				while (i < group.Count)
				{
					if (!IsSparse(group[i]) || !dirty.Contains(group[i].PageId))
					{
						i++;
						continue;
					}
					int j = i;
					while (j + 1 < group.Count && IsSparse(group[j + 1]) && dirty.Contains(group[j + 1].PageId))
					{
						j++;
					}

					// the dirty-only pass needs 2+ pages to merge at all; a lone sparse dirty leaf is a seed
					// that only the cold extension can do anything with
					if (j > i)
					{
						dirtyRuns++;
						var (freed, pagesOut) = Merge(group, i, j);
						dirtyFreed += freed;
						dirtyPagesOut += pagesOut;
					}

					// extend by at most one cold sparse leaf per edge (the bounded variant; anything wider is
					// the background vacuum's business)
					int lo = i, hi = j;
					if (lo > 0 && IsSparse(group[lo - 1]) && !dirty.Contains(group[lo - 1].PageId)) { lo--; }
					if (hi + 1 < group.Count && IsSparse(group[hi + 1]) && !dirty.Contains(group[hi + 1].PageId)) { hi++; }
					if (hi > lo)
					{
						var (freed, pagesOut) = Merge(group, lo, hi);
						if (freed > 0)
						{
							extFreed += freed;
							extPagesOut += pagesOut;
							extDirtyPages += j - i + 1;
							coldReads += (i - lo) + (hi - j);
						}
					}
					i = j + 1;
				}
			}
			return new(dirtyRuns, dirtyFreed, dirtyPagesOut, extFreed, extPagesOut, extDirtyPages, coldReads);

			bool IsSparse(VacuumLeafInfo leaf) => leaf.LiveBytes < (long) (Underflow * pageSize);

			(int Freed, int PagesOut) Merge(List<VacuumLeafInfo> group, int lo, int hi)
			{
				int count = 0;
				long sumWhole = 0, sumValue = 0;
				for (int r = lo; r <= hi; r++)
				{
					count += group[r].CellCount;
					sumWhole += group[r].SumWholeKeyBytes;
					sumValue += group[r].SumValueBytes;
				}
				long live = FdbLiteLeafAnalysis.RunBytes(count, sumWhole, sumValue, FdbLiteLeafAnalysis.Lcp(group[lo].FirstKey, group[hi].LastKey));
				int runLen = hi - lo + 1;
				int pagesOut = (int) ((live + budget - 1) / budget);
				return pagesOut < runLen ? (runLen - pagesOut, pagesOut) : (0, 0);
			}
		}

		private void RunColdDeltaWorkload(string workload, Action<FdbLiteTreeWriter, int> generation, int gens, Func<ulong> expectedKeys)
		{
			var path = NewStorePath();
			try
			{
				using var engine = FdbLiteEngine.OpenOrCreateFile(path, FdbLiteGeometry.Default, regionSizeInBytes: 1 << 24);
				int pageSize = engine.Pager.Geometry.PageSize;

				int totalDirtyFreed = 0, totalExtFreed = 0, totalColdReads = 0, totalDirtyOut = 0, totalExtOut = 0, totalExtDirty = 0;
				var previous = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
				for (int g = 0; g < gens; g++)
				{
					var w = engine.BeginWrite();
					generation(w, g);
					engine.Commit(w, (ulong) (g + 1));

					var snap = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
					var dirty = new HashSet<uint>(snap.LeafIds);
					dirty.ExceptWith(previous.LeafIds);
					var delta = SimulateColdExtension(snap, dirty, pageSize);
					if (delta.DirtyFreed > 0 || delta.ExtFreed > 0)
					{
						Log($"# [{workload}] gen {g + 1}: dirty-only freed {delta.DirtyFreed} (writes {delta.DirtyPagesOut}), +cold freed {delta.ExtFreed} (writes {delta.ExtPagesOut}, cold reads {delta.ColdReads})");
					}
					totalDirtyFreed += delta.DirtyFreed;
					totalExtFreed += delta.ExtFreed;
					totalColdReads += delta.ColdReads;
					totalDirtyOut += delta.DirtyPagesOut;
					totalExtOut += delta.ExtPagesOut;
					totalExtDirty += delta.ExtDirtyPages;
					previous = snap;
				}

				Assert.That(engine.Durable.KeyCount, Is.EqualTo(expectedKeys()), "sanity: the instrument must not be measuring a broken tree");
				int extra = totalExtFreed - totalDirtyFreed;
				Log($"# [{workload}] TOTAL: dirty-only freed {totalDirtyFreed} ({totalDirtyOut} pages out), +cold freed {totalExtFreed} ({totalExtOut} pages out), extra {extra} for {totalColdReads} cold reads");
				Log($"# [{workload}] write flatness: the extended merges emit {totalExtOut} pages against {totalExtDirty} dirty pages the commit was writing anyway ({(totalExtOut <= totalExtDirty ? "flat or fewer - the variant adds reads, not writes" : "MORE writes: the flatness claim does not hold here")})");
				Log($"# [{workload}] cost/win: {(extra > 0 ? $"{(double) totalColdReads / extra:N2} cold page reads per extra page freed" : "the cold extension freed nothing extra")}");
			}
			finally
			{
				File.Delete(path);
			}
		}

		/// <summary>The task-trail shape on the file pager: the survivors' holes are COLD, so this is where the cold extension must show its win.</summary>
		[Test]
		public void Diagnose_Task_Trail_Cold_Delta()
		{
			var value = new byte[32];
			const int BATCH = 2_000;
			const int GENS = 12;
			long expected = 0;

			RunColdDeltaWorkload("task-trail", (w, g) =>
			{
				long edge = (long) g * BATCH;
				for (long i = edge; i < edge + BATCH; i++)
				{
					w.Insert(Key(i), value);
					expected++;
				}
				if (g > 0)
				{
					for (long i = edge - BATCH; i < edge; i++)
					{
						if (i % 5 != 0)
						{
							Assert.That(w.Remove(Key(i)), Is.True);
							expected--;
						}
					}
				}
			}, GENS, () => (ulong) expected);
		}

		/// <summary>Random churn on the file pager: dirty runs exist on their own here, so the cold extension is measured as a marginal gain, not the whole win.</summary>
		[Test]
		public void Diagnose_Random_Churn_Cold_Delta()
		{
			var rnd = new Random(20260729);
			var model = new HashSet<long>();
			const int KEYSPACE = 30_000;
			const int GENS = 16;

			RunColdDeltaWorkload("random-churn", (w, g) =>
			{
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
			}, GENS, () => (ulong) model.Count);
		}

		#endregion

		#region Cold read cost on the file pager...

		/// <summary>Times the cold-pull read path (whole-page read + checksum verify, what the writer pays on first touch) over a fresh mapping and again warm, in shuffled order, across reopen cycles.</summary>
		/// <remarks>Deterministic headline: pages and bytes touched. The nanoseconds are supporting evidence, bracketed first-touch/warm per cycle with the spread visible, and they are a LOWER bound of disk-cold (see the fixture remarks).</remarks>
		[Test]
		public void Measure_Cold_Page_Read_Cost_On_File_Pager()
		{
			var path = NewStorePath();
			try
			{
				const int KEYS = 1_500_000;
				const int PER_GEN = 300_000;
				var value = new byte[1_000];
				uint[] leafIds;

				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, FdbLiteGeometry.Default, initialSizeInBytes: 2L << 30))
				{
					long next = 0;
					for (int g = 0; g < KEYS / PER_GEN; g++)
					{
						var w = engine.BeginWrite();
						for (int i = 0; i < PER_GEN; i++)
						{
							w.Insert(Key(next++), value);
						}
						engine.Commit(w, (ulong) (g + 1));
					}
					var snap = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
					leafIds = [ .. snap.LeafIds ];
				}

				// shuffled probe order, fixed seed: the cold pull reads lone pages next to dirty runs, and a
				// sequential sweep would let OS readahead flatter it
				var order = (uint[]) leafIds.Clone();
				var rnd = new Random(20260729);
				for (int i = order.Length - 1; i > 0; i--)
				{
					int j = rnd.Next(i + 1);
					(order[i], order[j]) = (order[j], order[i]);
				}

				var existing = FdbLiteMemoryMappedPager.ReadGeometry(path);
				long bytesPerSweep = (long) order.Length * existing.PageSize;
				Log($"# [cold-read] store: {order.Length:N0} leaves, {bytesPerSweep / (1 << 20):N0} MiB touched per sweep, page {existing.PageSize / 1024} KiB");

				const int CYCLES = 4;
				for (int c = 1; c <= CYCLES; c++)
				{
					using var pager = FdbLiteMemoryMappedPager.Open(path, existing, regionSizeInBytes: FdbLiteMemoryMappedPager.DefaultRegionSizeInBytes);
					long first = TimeSweep(pager, order);
					long warm = TimeSweep(pager, order);
					Log($"# [cold-read] cycle {c}: first-touch {first / order.Length:N0} ns/page, warm {warm / order.Length:N0} ns/page (sweeps {first / 1_000_000:N0} ms / {warm / 1_000_000:N0} ms)");
				}
			}
			finally
			{
				File.Delete(path);
			}

			static long TimeSweep(IFdbLitePager pager, uint[] order)
			{
				int blocks = pager.Geometry.BlocksPerPage;
				int failures = 0;
				var sw = Stopwatch.StartNew();
				foreach (var id in order)
				{
					// the checksum verify is PART of the measured operation on purpose: it is what the writer
					// pays on first touch of a cold page, and it forces the whole page to actually be read
					if (!FdbLitePageHeader.Verify(pager.ReadBlocks(id, blocks), id))
					{
						failures++;
					}
				}
				sw.Stop();
				Assert.That(failures, Is.Zero, "a page failing its checksum means the sweep read garbage, not pages");
				return sw.Elapsed.Ticks * 100;
			}
		}

		#endregion

	}

}
