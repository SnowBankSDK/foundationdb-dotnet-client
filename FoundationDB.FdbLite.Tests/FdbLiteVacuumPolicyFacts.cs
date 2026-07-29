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

	/// <summary>Diagnostic: which fill target and underflow threshold should consolidation/vacuum use (OQ-3), and what would a pre-commit budget capture (OQ-4)?</summary>
	/// <remarks>
	/// <para>A WHAT-IF harness: no consolidation arm exists, so at chosen generation boundaries it re-derives
	/// every leaf from the raw pages, simulates the merge pass each (underflow U, fill target T) policy would
	/// run, and then replays the workload's own FUTURE operations against the simulated merged runs to see
	/// which of them the workload would have torn open again (the split/merge oscillation OQ-3's hysteresis
	/// exists to avoid). Everything reported is a deterministic count or byte total.</para>
	/// <para>Approximations, all conservative and shared by every policy cell so the comparison stays fair:
	/// merged-run density is sized at the RUN's shared prefix (each real output page would strip a longer one);
	/// runs group under one parent only (cross-parent consolidation, in scope for the background vacuum, adds
	/// wins on top); a merged run's headroom is tracked run-wide rather than per output page (a hot spot inside
	/// a run would re-split earlier than the run-wide number says); re-split tracking assumes no later
	/// consolidation pass runs.</para>
	/// <para>A generation's dirty leaves are identified as the leaf ids ABSENT from the previous snapshot: any
	/// cross-generation touch relocates the page (copy-on-write), so the id delta is exact. The header's
	/// generation stamp is deliberately NOT used: a copy-and-overwrite carries the source page's stamp
	/// verbatim, so stamps under-report the dirty set.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	[Explicit("diagnostic: consolidation-policy what-if measurement, run on demand")]
	public class FdbLiteVacuumPolicyFacts : SimpleTest
	{

		private static byte[] Key(long i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		#region Policy what-if...

		/// <summary>One simulated merge of a run of adjacent under-full sibling leaves.</summary>
		private sealed record MergedRun(long FirstKey, long LastKey, int RunLength, int PagesOut, long LiveBytes, long Headroom)
		{
			/// <summary>Net future growth booked against this run so far, and whether it already overflowed</summary>
			public long NetGrowth { get; set; }

			public bool Resplit { get; set; }
		}

		private sealed record PolicyOutcome(int Candidates, int RunPages, int PagesFreed, long MovedBytes, List<MergedRun> Merges);

		/// <summary>Simulates the merge pass policy (U, T) would run on this snapshot: adjacent same-parent leaves under U% full, re-emitted at T% fill.</summary>
		/// <param name="scope">Restricts candidacy (the pre-commit pass sees only the generation's dirty leaves); null analyzes every leaf (the vacuum's view).</param>
		private static PolicyOutcome Simulate(VacuumTreeSnapshot snap, HashSet<uint>? scope, double underflow, double target, int pageSize)
		{
			int candidates = 0, runPages = 0, pagesFreed = 0;
			long movedBytes = 0;
			var merges = new List<MergedRun>();
			foreach (var group in snap.Groups)
			{
				int i = 0;
				while (i < group.Count)
				{
					if (!IsCandidate(group[i]))
					{
						i++;
						continue;
					}
					candidates++;
					int j = i;
					while (j + 1 < group.Count && IsCandidate(group[j + 1]))
					{
						candidates++;
						j++;
					}
					if (j > i)
					{ // a run of 2+ adjacent candidates: gather and re-emit at the target fill
						int count = 0;
						long sumWhole = 0, sumValue = 0;
						for (int r = i; r <= j; r++)
						{
							count += group[r].CellCount;
							sumWhole += group[r].SumWholeKeyBytes;
							sumValue += group[r].SumValueBytes;
						}
						long live = FdbLiteLeafAnalysis.RunBytes(count, sumWhole, sumValue, FdbLiteLeafAnalysis.Lcp(group[i].FirstKey, group[j].LastKey));
						int runLen = j - i + 1;
						int pagesOut = (int) ((live + (long) (target * pageSize) - 1) / (long) (target * pageSize));
						if (pagesOut < runLen)
						{
							runPages += runLen;
							pagesFreed += runLen - pagesOut;
							movedBytes += live;
							merges.Add(new(group[i].FirstKey, group[j].LastKey, runLen, pagesOut, live, ((long) pagesOut * pageSize) - live));
						}
					}
					i = j + 1;
				}
			}
			return new(candidates, runPages, pagesFreed, movedBytes, merges);

			bool IsCandidate(VacuumLeafInfo leaf)
				=> leaf.LiveBytes < (long) (underflow * pageSize) && (scope == null || scope.Contains(leaf.PageId));
		}

		#endregion

		#region Oscillation replay...

		private readonly record struct Op(long Key, int CellBytes, bool Insert);

		/// <summary>Books every future operation against the merged run covering its key; a run whose net growth exceeds its headroom is torn open again (a re-split).</summary>
		private static (int Resplit, int Total) TrackResplits(List<MergedRun> merges, IEnumerable<Op> futureOps)
		{
			if (merges.Count == 0)
			{
				return (0, 0);
			}
			var byFirst = merges.OrderBy(m => m.FirstKey).ToArray();
			var firsts = byFirst.Select(m => m.FirstKey).ToArray();
			foreach (var op in futureOps)
			{
				int at = Array.BinarySearch(firsts, op.Key);
				if (at < 0) { at = ~at - 1; }
				if (at < 0) { continue; }
				var run = byFirst[at];
				if (run.Resplit || op.Key > run.LastKey) { continue; }
				run.NetGrowth += op.Insert ? op.CellBytes : -op.CellBytes;
				if (run.NetGrowth > run.Headroom)
				{
					run.Resplit = true;
				}
			}
			return (byFirst.Count(m => m.Resplit), byFirst.Length);
		}

		#endregion

		#region Analysis driver...

		private static readonly double[] Underflows = [ 0.40, 0.50, 0.60, 0.70 ];

		private static readonly double[] Targets = [ 0.85, 0.90, 1.00 ];

		private const double HeadlineUnderflow = 0.50;

		private const double HeadlineTarget = 0.90;

		private static readonly int[] BudgetCandidates = [ 1, 2, 4, 8, int.MaxValue ];

		private sealed class Analysis
		{
			public required string Workload { get; init; }

			public required int PageSize { get; init; }

			/// <summary>(anchor generation, snapshot, dirty ids at that generation)</summary>
			public List<(int Gen, VacuumTreeSnapshot Snap, HashSet<uint> Dirty)> Anchors { get; } = [ ];

			/// <summary>Per-generation op lists, index = generation ordinal in the workload loop</summary>
			public required List<Op>[] OpsByGen { get; init; }

			/// <summary>Per-generation headline-cell numbers on the DIRTY scope (the pre-commit pass), for the OQ-4 legs</summary>
			public List<(int PagesFreed, int RunPages, long MovedBytes, long CommitBytes, long FreedAtBudget1, long FreedAtBudget2, long FreedAtBudget4, long FreedAtBudget8)> PreCommit { get; } = [ ];
		}

		/// <summary>Runs the headline pre-commit cell on this generation's dirty scope, with the fixed-K coverage ladder (best-run-first, K counts candidate pages gathered).</summary>
		private static void BookPreCommitGeneration(Analysis a, VacuumTreeSnapshot snap, HashSet<uint> dirty, long commitBytes)
		{
			var outcome = Simulate(snap, dirty, HeadlineUnderflow, HeadlineTarget, a.PageSize);
			var best = outcome.Merges.OrderByDescending(m => m.RunLength - m.PagesOut).ToList();
			Span<long> freedAt = stackalloc long[4];
			for (int b = 0; b < 4; b++)
			{
				int budget = BudgetCandidates[b];
				int consumed = 0;
				long freed = 0;
				foreach (var m in best)
				{
					if (consumed >= budget) { break; }
					consumed += m.RunLength;
					freed += m.RunLength - m.PagesOut;
				}
				freedAt[b] = freed;
			}
			a.PreCommit.Add((outcome.PagesFreed, outcome.RunPages, outcome.MovedBytes, commitBytes, freedAt[0], freedAt[1], freedAt[2], freedAt[3]));
		}

		private void ReportPolicyGrid(Analysis a)
		{
			foreach (bool dirtyScope in (ReadOnlySpan<bool>) [ true, false ])
			{
				string scopeName = dirtyScope ? "pre-commit (dirty leaves)" : "vacuum (all leaves)";
				Log($"# [{a.Workload}] {scopeName}, mean over {a.Anchors.Count} anchor generation(s); cells are pagesFreed/anchor (resplit% of merged runs, moved KiB per freed page)");
				foreach (double u in Underflows)
				{
					var cells = new List<string>();
					foreach (double t in Targets)
					{
						long freed = 0, moved = 0;
						int resplit = 0, runs = 0;
						foreach (var (gen, snap, dirty) in a.Anchors)
						{
							var outcome = Simulate(snap, dirtyScope ? dirty : null, u, t, a.PageSize);
							freed += outcome.PagesFreed;
							moved += outcome.MovedBytes;
							var (r, total) = TrackResplits(outcome.Merges, a.OpsByGen.Skip(gen + 1).SelectMany(ops => ops));
							resplit += r;
							runs += total;
						}
						double freedPerAnchor = (double) freed / a.Anchors.Count;
						string resplitPct = runs > 0 ? $"{100.0 * resplit / runs:N0}%" : "-";
						string cost = freed > 0 ? $"{moved / 1024.0 / freed:N0} KiB" : "-";
						cells.Add($"T={t:0.00}: {freedPerAnchor,6:N1} ({resplitPct}, {cost})");
					}
					Log($"# [{a.Workload}]   U={u:0.00}  {string.Join("   ", cells)}");
				}
			}

			// the OQ-4 legs: what a bounded pre-commit pass captures, and what it costs relative to the commit itself
			long totalFreed = a.PreCommit.Sum(p => (long) p.PagesFreed);
			if (totalFreed > 0)
			{
				long f1 = a.PreCommit.Sum(p => p.FreedAtBudget1);
				long f2 = a.PreCommit.Sum(p => p.FreedAtBudget2);
				long f4 = a.PreCommit.Sum(p => p.FreedAtBudget4);
				long f8 = a.PreCommit.Sum(p => p.FreedAtBudget8);
				Log($"# [{a.Workload}] pre-commit fixed-K coverage (U={HeadlineUnderflow:0.00} T={HeadlineTarget:0.00}, best-run-first, all generations): K=1 {100.0 * f1 / totalFreed:N0}%  K=2 {100.0 * f2 / totalFreed:N0}%  K=4 {100.0 * f4 / totalFreed:N0}%  K=8 {100.0 * f8 / totalFreed:N0}%  of {totalFreed:N0} freeable pages");
			}
			else
			{
				Log($"# [{a.Workload}] pre-commit fixed-K coverage: no freeable pages in the dirty scope at the headline cell");
			}
			var withWork = a.PreCommit.Where(p => p.MovedBytes > 0 && p.CommitBytes > 0).ToList();
			if (withWork.Count > 0)
			{
				double meanPct = withWork.Average(p => 100.0 * p.MovedBytes / p.CommitBytes);
				double maxPct = withWork.Max(p => 100.0 * p.MovedBytes / p.CommitBytes);
				Log($"# [{a.Workload}] pre-commit consolidation work vs the commit's own page writes: mean {meanPct:N0}%, max {maxPct:N0}% across {withWork.Count} generation(s) with work to do (time-budget calibration input)");
			}
		}

		#endregion

		/// <summary>The task-trail shape (the 65.6%-reclaimable finding): where does the policy grid put the reclaim, and does anything re-split?</summary>
		[Test]
		public void Diagnose_Task_Trail_Policy_Grid()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[32];
			int pageSize = engine.Pager.Geometry.PageSize;

			const int BATCH = 2_000;
			const int GENS = 12;
			int[] anchors = [ 4, 6, 8 ];
			var a = new Analysis { Workload = "task-trail", PageSize = pageSize, OpsByGen = new List<Op>[GENS] };

			long expected = 0;
			var previous = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
			for (int g = 0; g < GENS; g++)
			{
				var ops = a.OpsByGen[g] = [ ];
				var w = engine.BeginWrite();
				long edge = (long) g * BATCH;
				for (long i = edge; i < edge + BATCH; i++)
				{
					w.Insert(Key(i), value);
					ops.Add(new(i, 9 + 8 + value.Length, Insert: true));
					expected++;
				}
				if (g > 0)
				{
					for (long i = edge - BATCH; i < edge; i++)
					{
						if (i % 5 != 0)
						{
							Assert.That(w.Remove(Key(i)), Is.True, $"gen {g}: task {i} must exist");
							ops.Add(new(i, 9 + 8 + value.Length, Insert: false));
							expected--;
						}
					}
				}
				engine.Commit(w, (ulong) (g + 1));

				var snap = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
				var dirty = new HashSet<uint>(snap.LeafIds);
				dirty.ExceptWith(previous.LeafIds);
				BookPreCommitGeneration(a, snap, dirty, (long) w.PagesWritten * pageSize);
				if (anchors.Contains(g + 1))
				{
					a.Anchors.Add((g, snap, dirty));
				}
				previous = snap;
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) expected), "sanity: the instrument must not be measuring a broken tree");
			Assert.That(a.Anchors.SelectMany(x => x.Snap.Groups).SelectMany(gr => gr).Any(l => l.LiveBytes < pageSize / 2), Is.True, "mechanism: the trail shape must produce under-full leaves for the grid to measure");
			ReportPolicyGrid(a);
		}

		/// <summary>Random churn over a bounded keyspace (same shape and seed as the opportunity diagnostic): the grid where hysteresis actually matters.</summary>
		[Test]
		public void Diagnose_Random_Churn_Policy_Grid()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var rnd = new Random(20260729);
			var model = new Dictionary<long, int>();
			int pageSize = engine.Pager.Geometry.PageSize;

			const int KEYSPACE = 30_000;
			const int GENS = 24;
			int[] anchors = [ 8, 12, 16 ];
			var a = new Analysis { Workload = "random-churn", PageSize = pageSize, OpsByGen = new List<Op>[GENS] };

			var previous = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
			for (int g = 0; g < GENS; g++)
			{
				var ops = a.OpsByGen[g] = [ ];
				var w = engine.BeginWrite();
				for (int op = 0; op < 6_000; op++)
				{
					long k = rnd.Next(KEYSPACE);
					if (rnd.Next(10) < 6 || !model.ContainsKey(k))
					{
						int len = rnd.Next(0, 100);
						w.Insert(Key(k), new byte[len]);
						if (model.TryGetValue(k, out int old))
						{ // a replace: the old cell's bytes leave, the new ones arrive
							ops.Add(new(k, 9 + 8 + old, Insert: false));
						}
						ops.Add(new(k, 9 + 8 + len, Insert: true));
						model[k] = len;
					}
					else
					{
						Assert.That(w.Remove(Key(k)), Is.True);
						ops.Add(new(k, 9 + 8 + model[k], Insert: false));
						model.Remove(k);
					}
				}
				engine.Commit(w, (ulong) (g + 1));

				var snap = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
				var dirty = new HashSet<uint>(snap.LeafIds);
				dirty.ExceptWith(previous.LeafIds);
				BookPreCommitGeneration(a, snap, dirty, (long) w.PagesWritten * pageSize);
				if (anchors.Contains(g + 1))
				{
					a.Anchors.Add((g, snap, dirty));
				}
				previous = snap;
			}

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count), "sanity: the instrument must not be measuring a broken tree");
			Assert.That(a.Anchors.SelectMany(x => x.Snap.Groups).SelectMany(gr => gr).Any(l => l.LiveBytes < pageSize / 2), Is.True, "mechanism: churn must produce under-full leaves for the grid to measure");
			ReportPolicyGrid(a);
		}

		/// <summary>Control: a pure sequential load leaves packed pages, so every policy cell must find (near) nothing.</summary>
		[Test]
		public void Diagnose_Sequential_Control()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[64];
			int pageSize = engine.Pager.Geometry.PageSize;

			const int GENS = 6;
			var a = new Analysis { Workload = "sequential", PageSize = pageSize, OpsByGen = new List<Op>[GENS] };

			long next = 0;
			var previous = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
			for (int g = 0; g < GENS; g++)
			{
				a.OpsByGen[g] = [ ];
				var w = engine.BeginWrite();
				for (int i = 0; i < 10_000; i++)
				{
					w.Insert(Key(next++), value);
				}
				engine.Commit(w, (ulong) (g + 1));

				var snap = FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId);
				var dirty = new HashSet<uint>(snap.LeafIds);
				dirty.ExceptWith(previous.LeafIds);
				BookPreCommitGeneration(a, snap, dirty, (long) w.PagesWritten * pageSize);
				previous = snap;
			}
			a.Anchors.Add((GENS - 1, previous, [ ]));

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) next), "sanity: the instrument must not be measuring a broken tree");
			var headline = Simulate(previous, scope: null, HeadlineUnderflow, HeadlineTarget, pageSize);
			Assert.That(headline.PagesFreed, Is.Zero, "control: a sequential load is packed; a policy that finds pages to free here is measuring noise");
			ReportPolicyGrid(a);
		}

	}

}
