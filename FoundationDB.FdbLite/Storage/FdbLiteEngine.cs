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

namespace FoundationDB.Storage.FdbLite
{

	/// <summary>Policy for merging under-full dirty leaves at commit, before the flush (the pre-commit consolidation arm).</summary>
	/// <remarks>
	/// <para><see cref="Off"/> is the deterministic default and the EMULATOR posture: wall-clock heuristics are nondeterministic, and a determinism-sensitive configuration must never inherit one silently. <see cref="FixedBudget"/> merges up to K runs per commit, deterministically, which is also what lets a test exercise the merge machinery byte-for-byte reproducibly. <see cref="Adaptive"/> spends a fraction of the recent commit cost (an EMA the engine maintains) and is the file store's shipped default; whatever it skips falls to the background vacuum by construction.</para>
	/// </remarks>
	public readonly struct FdbLitePreCommitConsolidation
	{

		private FdbLitePreCommitConsolidation(int maxRuns, bool adaptive)
		{
			this.MaxRuns = maxRuns;
			this.IsAdaptive = adaptive;
		}

		/// <summary>No consolidation: every commit flushes exactly what the transaction dirtied.</summary>
		public static FdbLitePreCommitConsolidation Off => default;

		/// <summary>Merge up to <paramref name="maxRuns"/> candidate runs per commit, best run first, deterministically.</summary>
		public static FdbLitePreCommitConsolidation FixedBudget(int maxRuns)
		{
			Contract.Positive(maxRuns);
			return new(maxRuns, adaptive: false);
		}

		/// <summary>Spend up to a fraction of the recent commit wall time per commit (see <see cref="FdbLiteEngine.ConsolidationBudgetFraction"/>), under a hard page cap; candidate order stays deterministic, the clock decides only where the loop stops.</summary>
		public static FdbLitePreCommitConsolidation Adaptive => new(0, adaptive: true);

		internal int MaxRuns { get; }

		internal bool IsAdaptive { get; }

		internal bool IsOff => !this.IsAdaptive && this.MaxRuns == 0;

		/// <inheritdoc />
		public override string ToString() => this.IsAdaptive ? "Adaptive" : this.MaxRuns > 0 ? $"FixedBudget({this.MaxRuns})" : "Off";

	}

	/// <summary>The engine facade: opens or creates a store, runs the two-fsync commit protocol, and manages read-snapshot pins.</summary>
	/// <remarks>
	/// <para>Commit protocol: COW data blocks and the fresh free-list chain are written and flushed FIRST; only then is the alternate snapshot-header slot written and flushed. A crash before the second flush leaves the previous header valid, and because the free-list chain is rewritten fresh each commit, reopening at the previous generation automatically forgets every allocation the torn generation made: crash recovery needs no sweep at all.</para>
	/// <para>One writer at a time (the emulator's commit path is serialized); read pins may be taken and released from any thread. A pin holds the reclamation horizon: pages of the pinned generation can never be reused, relocated, or truncated while it lives.</para>
	/// </remarks>
	public sealed class FdbLiteEngine : IDisposable
	{

		private FdbLiteEngine(IFdbLitePager pager, FdbLiteFreeSpaceMap freeSpace, FdbLiteSnapshotHeader durable, uint durableSlot)
		{
			this.Pager = pager;
			this.FreeSpace = freeSpace;
			this.Allocator = new FdbLiteBlockAllocator(pager, freeSpace, durable.AllocationFrontier);
			this.Durable = durable;
			this.DurableSlot = durableSlot;
		}

		/// <summary>Pager under this store (owned: disposed with the engine)</summary>
		public IFdbLitePager Pager { get; }

		private FdbLiteFreeSpaceMap FreeSpace { get; }

		private FdbLiteBlockAllocator Allocator { get; }

		/// <summary>The current durable snapshot header</summary>
		public FdbLiteSnapshotHeader Durable { get; private set; }

		/// <summary>The previous durable header, when still retained (the backup slot): its tree is intact by the crash-safety rule, so it stays readable through <see cref="TryBeginReadAtVersion"/></summary>
		public FdbLiteSnapshotHeader? PreviousDurable { get; private set; }

		private uint DurableSlot { get; set; }

		/// <summary>Retention policy: the reclaimer never promotes a freed-at generation above this floor.</summary>
		/// <remarks>The default (<see cref="ulong.MaxValue"/>) is the PRODUCTION policy - reclamation is bounded only by the live read pins and the backup generation (self-limiting: freed blocks come back as soon as no reader can see them). Drop it to <c>0</c> to RETAIN every generation - nothing freed is ever reclaimed - which is the retain-all-in-memory / inspection policy. This is deliberately one explicit knob, defaulted open: the production reclamation path can never silently inherit the retain-all assumption, it has to be asked for.</remarks>
		public ulong RetainFloor { get; set; } = ulong.MaxValue;

		/// <summary>Read pins per generation (guarded by <see cref="PinLock"/>)</summary>
		private SortedDictionary<ulong, int> Pins { get; } = new();

#if NET9_0_OR_GREATER
		private readonly System.Threading.Lock PinLock = new();
#else
		private readonly object PinLock = new();
#endif

		/// <summary>Creates a fresh store on a pager holding no data yet.</summary>
		public static FdbLiteEngine Create(IFdbLitePager pager)
		{
			Contract.NotNull(pager);
			Contract.Requires(pager.BlockCount == 0 || IsBlank(pager), "the pager already holds a store");

			pager.Grow(3);
			int blockSize = pager.Geometry.BlockSize;
			var buffer = ArrayPool<byte>.Shared.Rent(blockSize);
			try
			{
				var block = buffer.AsSpan(0, blockSize);

				FdbLiteFileHeader.Write(block, pager.Geometry, Guid.NewGuid(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
				pager.WriteBlocks(0, block);

				var initial = new FdbLiteSnapshotHeader(
					Generation: 1,
					DatabaseVersion: 0,
					RootPageId: 0,
					AllocatedBlockCount: pager.BlockCount,
					FreeListRoot: 0,
					AllocationFrontier: 3,
					KeyCount: 0,
					Horizon: 0);
				initial.Write(block, blockId: 1);
				pager.WriteBlocks(1, block);
				pager.Flush();

				return new FdbLiteEngine(pager, new FdbLiteFreeSpaceMap(), initial, durableSlot: 1);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		private static bool IsBlank(IFdbLitePager pager)
		{
			if (pager.BlockCount < 3)
			{
				return true;
			}
			return !FdbLiteSnapshotHeader.TryRead(pager.ReadBlocks(1, 1), 1, out _) && !FdbLiteSnapshotHeader.TryRead(pager.ReadBlocks(2, 1), 2, out _);
		}

		/// <summary>Opens an existing store from a pager: picks the newest VALID snapshot header (a torn commit's header fails its checksum and loses) and reloads the free-space state it references.</summary>
		public static FdbLiteEngine Open(IFdbLitePager pager)
		{
			Contract.NotNull(pager);
			if (pager.BlockCount < 3)
			{
				throw new InvalidDataException("Store file too short");
			}
			FdbLiteFileHeader.Read(pager.ReadBlocks(0, 1));

			bool aValid = FdbLiteSnapshotHeader.TryRead(pager.ReadBlocks(1, 1), 1, out var a);
			bool bValid = FdbLiteSnapshotHeader.TryRead(pager.ReadBlocks(2, 1), 2, out var b);
			if (!aValid && !bValid)
			{
				throw new InvalidDataException("Both snapshot headers are unreadable");
			}
			var (durable, slot) = !bValid || (aValid && a.Generation > b.Generation) ? (a, 1u) : (b, 2u);

			var freeSpace = FdbLiteFreeListChain.Load(pager, durable.FreeListRoot, durable.Generation);
			var engine = new FdbLiteEngine(pager, freeSpace, durable, slot);
			var other = slot == 1 ? (bValid, b) : (aValid, a);
			if (other.Item1 && other.Item2.Generation == durable.Generation - 1)
			{ // the backup slot still describes the immediately-previous generation: it remains readable
				engine.PreviousDurable = other.Item2;
			}
			return engine;
		}

		/// <summary>Opens (or creates) a file-backed store.</summary>
		/// <param name="initialSizeInBytes">Reserve this much file up front rather than growing into it a region at a time. See <see cref="FdbLiteMemoryMappedPager.Open"/>; it is a hint, not a cap.</param>
		/// <remarks><b>The <paramref name="geometry"/> argument is only honoured when the file does not exist yet.</b>
		/// For an existing store it is read back from the file's own header and this argument is IGNORED, so
		/// re-opening a stale file with a different geometry silently keeps the old one.</remarks>
		public static FdbLiteEngine OpenOrCreateFile(string path, FdbLiteGeometry geometry, int regionSizeInBytes = FdbLiteMemoryMappedPager.DefaultRegionSizeInBytes, long initialSizeInBytes = 0)
		{
			FdbLiteEngine engine;
			if (File.Exists(path) && new FileInfo(path).Length > 0)
			{
				var existing = FdbLiteMemoryMappedPager.ReadGeometry(path);
				var pager = FdbLiteMemoryMappedPager.Open(path, existing, regionSizeInBytes, initialSizeInBytes);
				engine = Open(pager);
			}
			else
			{
				var fresh = FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes, initialSizeInBytes);
				engine = Create(fresh);
			}
			// the file store's shipped default (a caller that needs determinism sets Off explicitly); engines
			// built over a raw pager - the emulator among them - keep the deterministic Off default
			engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Adaptive;
			return engine;
		}

		#region Writing...

		/// <summary>Append policy handed to every writer this engine starts (see <see cref="FdbLiteTreeWriter.AvoidSequentialAppendSplits"/>).</summary>
		public bool AvoidSequentialAppendSplits { get; set; } = true;

		/// <summary>Pre-commit consolidation policy (see <see cref="FdbLitePreCommitConsolidation"/>): <see cref="FdbLitePreCommitConsolidation.Off"/> by default, so the emulator and every determinism-sensitive configuration stays deterministic unless explicitly asked otherwise. <see cref="OpenOrCreateFile"/> ships file-backed stores with <see cref="FdbLitePreCommitConsolidation.Adaptive"/>.</summary>
		public FdbLitePreCommitConsolidation PreCommitConsolidation { get; set; } = FdbLitePreCommitConsolidation.Off;

		/// <summary>Hard cap on input pages one commit's consolidation may consume, whatever the budget says (safety against a pathological budget estimate).</summary>
		public const int ConsolidationHardPageCap = 48;

		/// <summary>Fraction of the recent commit cost the adaptive policy may spend per commit: the measured consolidation work runs mean 1-10% (max 13%) of a commit's own page-write bytes, so a tenth of the EMA already covers the median generation on every measured shape.</summary>
		public const double ConsolidationBudgetFraction = 0.10;

		/// <summary>Commits observed before the adaptive policy engages (the EMA seeds on commits that ran without consolidation).</summary>
		private const int AdaptiveSeedCommits = 4;

		/// <summary>EMA of total commit wall time (fsyncs included), in <see cref="Stopwatch"/> ticks</summary>
		private double CommitEmaStopwatchTicks { get; set; }

		private int CommitSamples { get; set; }

		/// <summary>Runs merged by pre-commit consolidation over this engine's lifetime</summary>
		public long ConsolidationRunsMerged { get; private set; }

		/// <summary>Viable runs left unmerged when a commit's consolidation stopped on its budget or caps: the number that distinguishes a heuristic that silently never fires from one that fires and does nothing</summary>
		public long ConsolidationRunsSkipped { get; private set; }

		/// <summary>Net pages freed by pre-commit consolidation over this engine's lifetime</summary>
		public long ConsolidationPagesFreed { get; private set; }

		/// <summary>The current commit-cost EMA the adaptive budget is a fraction of (zero until the first commit)</summary>
		public TimeSpan CommitDurationEma => Stopwatch.GetElapsedTime(0, (long) this.CommitEmaStopwatchTicks);

		/// <summary>Starts the writable generation (exactly one at a time; commit or abandon it before starting another).</summary>
		public FdbLiteTreeWriter BeginWrite() => new(this.Pager, this.Allocator, this.Durable.Generation + 1, this.Durable.RootPageId) { AvoidSequentialAppendSplits = this.AvoidSequentialAppendSplits };

		/// <summary>Publishes a written generation: flush data, then flip the alternate header, then flush again.</summary>
		public void Commit(FdbLiteTreeWriter writer, ulong databaseVersion)
		{
			Contract.NotNull(writer);
			Contract.Requires(writer.Generation == this.Durable.Generation + 1, "commit out of order");

			// the EMA measures the TOTAL commit wall time, fsyncs and consolidation included: it is the cost
			// baseline the adaptive budget takes its fraction of
			long commitStart = Stopwatch.GetTimestamp();

			// consolidation runs BEFORE the flush, while under-full dirty pages are still in-process buffers:
			// merging them here removes page writes from the flush instead of adding any
			RunPreCommitConsolidation(writer);

			// the writer holds its modified page images until now, so they must reach the pager before anything
			// else this commit writes, and well before the first flush barrier below
			writer.FlushDirtyPages();

			// the previous generation's free-list chain dies with this commit
			if (this.Durable.FreeListRoot != 0)
			{
				foreach (var block in FdbLiteFreeListChain.CollectChainBlocks(this.Pager, this.Durable.FreeListRoot))
				{
					this.Allocator.Free(block, 1, writer.Generation);
				}
			}
			uint freeRoot = FdbLiteFreeListChain.Persist(this.FreeSpace, this.Allocator, this.Pager, writer.Generation);

			// barrier 1: every data and free-list block of this generation is durable before the header moves
			this.Pager.Flush();

			var header = new FdbLiteSnapshotHeader(
				Generation: writer.Generation,
				DatabaseVersion: databaseVersion,
				RootPageId: writer.Root,
				AllocatedBlockCount: this.Pager.BlockCount,
				FreeListRoot: freeRoot,
				AllocationFrontier: this.Allocator.Frontier,
				KeyCount: (ulong) ((long) this.Durable.KeyCount + writer.KeyCountDelta),
				Horizon: ComputePromoteLimit(writer.Generation));

			uint slot = this.DurableSlot == 1 ? 2u : 1u;
			int blockSize = this.Pager.Geometry.BlockSize;
			var buffer = ArrayPool<byte>.Shared.Rent(blockSize);
			try
			{
				var block = buffer.AsSpan(0, blockSize);
				block.Clear();
				header.Write(block, slot);
				this.Pager.WriteBlocks(slot, block);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}

			// barrier 2: the header (and with it the generation) becomes the durable truth
			this.Pager.Flush();

			lock (this.PinLock)
			{ // BeginRead/TryBeginReadAtVersion read these under the same lock; the snapshot header is a
			  // multi-field struct, so an unlocked assignment can tear against a concurrent pin and pin the
			  // WRONG generation - which un-fences the reclaimer from blocks that root still references
				this.PreviousDurable = this.Durable;
				this.Durable = header;
				this.DurableSlot = slot;
			}

			// blocks freed by generations nothing can see anymore become reusable
			this.FreeSpace.Promote(ComputePromoteLimit(this.Durable.Generation + 1));

			UpdateCommitEma(commitStart);
		}

		/// <summary>Runs the configured pre-commit consolidation on the writer, before its dirty set flushes.</summary>
		private void RunPreCommitConsolidation(FdbLiteTreeWriter writer)
		{
			var policy = this.PreCommitConsolidation;
			if (policy.IsOff)
			{
				return;
			}

			(int Merged, int Freed) result;
			if (!policy.IsAdaptive)
			{
				result = writer.ConsolidateUnderflow(policy.MaxRuns, ConsolidationHardPageCap);
			}
			else if (this.CommitSamples < AdaptiveSeedCommits)
			{ // the EMA seeds on the first commits running WITHOUT consolidation, so the very first budget is
			  // derived from this store's real commit cost rather than from a guess
				return;
			}
			else
			{
				// the stopwatch decides only WHERE THE LOOP STOPS, never the order: two runs of the same
				// commit differ at most in a suffix of the merge list, and that suffix falls to the
				// background vacuum by construction - which is what makes a wall-clock heuristic tolerable
				double budgetTicks = this.CommitEmaStopwatchTicks * ConsolidationBudgetFraction;
				long start = Stopwatch.GetTimestamp();
				result = writer.ConsolidateUnderflow(int.MaxValue, ConsolidationHardPageCap, () => Stopwatch.GetTimestamp() - start > budgetTicks);
			}

			this.ConsolidationRunsMerged += result.Merged;
			this.ConsolidationPagesFreed += result.Freed;
			this.ConsolidationRunsSkipped += writer.ConsolidationRunsSkipped;
		}

		private void UpdateCommitEma(long startTimestamp)
		{
			long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
			// alpha 0.2: a few commits of history, quick to follow a workload change, immune to one outlier
			this.CommitEmaStopwatchTicks = this.CommitSamples == 0 ? elapsed : (this.CommitEmaStopwatchTicks * 0.8) + (elapsed * 0.2);
			this.CommitSamples++;
		}

		/// <summary>Highest freed-at generation that is reusable while building <paramref name="buildingGeneration"/>: below the previous durable root, and at or below the oldest pin.</summary>
		private ulong ComputePromoteLimit(ulong buildingGeneration)
		{
			ulong limit = buildingGeneration - 2; // the backup header's generation stays intact
			lock (this.PinLock)
			{
				if (this.Pins.Count > 0)
				{
					ulong oldest = this.Pins.Keys.First();
					if (oldest < limit) { limit = oldest; }
				}
			}
			// retention policy: never reclaim above the retain floor (production leaves it wide open, so pins and the
			// backup generation govern; a test drops it to 0 to retain every generation - see RetainFloor)
			if (this.RetainFloor < limit) { limit = this.RetainFloor; }
			return limit;
		}

		#endregion

		#region Read pins...

		/// <summary>A pinned, immutable view of one committed generation.</summary>
		public readonly record struct ReadSnapshot(ulong Generation, ulong DatabaseVersion, uint RootPageId, ulong KeyCount);

		/// <summary>Pins the current durable generation for reading; every span read out of it stays valid until the pin is released.</summary>
		public ReadSnapshot BeginRead()
		{
			lock (this.PinLock)
			{
				var d = this.Durable;
				this.Pins.TryGetValue(d.Generation, out int count);
				this.Pins[d.Generation] = count + 1;
				return new(d.Generation, d.DatabaseVersion, d.RootPageId, d.KeyCount);
			}
		}

		/// <summary>Pins the generation that published a database version, when it is still retained: the current generation always, the previous one thanks to the crash-safety rule (its pages are freed AT the current generation, which never promotes while current).</summary>
		public bool TryBeginReadAtVersion(ulong databaseVersion, out ReadSnapshot snapshot)
		{
			lock (this.PinLock)
			{
				FdbLiteSnapshotHeader header;
				if (this.Durable.DatabaseVersion == databaseVersion)
				{
					header = this.Durable;
				}
				else if (this.PreviousDurable is { } previous && previous.DatabaseVersion == databaseVersion)
				{
					header = previous;
				}
				else
				{
					snapshot = default;
					return false;
				}

				this.Pins.TryGetValue(header.Generation, out int count);
				this.Pins[header.Generation] = count + 1;
				snapshot = new(header.Generation, header.DatabaseVersion, header.RootPageId, header.KeyCount);
				return true;
			}
		}

		/// <summary>Releases a pin (reclamation of its generation resumes at the next commit).</summary>
		public void EndRead(in ReadSnapshot snapshot)
		{
			lock (this.PinLock)
			{
				if (this.Pins.TryGetValue(snapshot.Generation, out int count))
				{
					if (count <= 1) { this.Pins.Remove(snapshot.Generation); } else { this.Pins[snapshot.Generation] = count - 1; }
				}
			}
		}

		/// <summary>Walks the current durable generation (under its own read pin) and returns its aggregate tree statistics, wasted bytes included.</summary>
		/// <remarks>O(pages): an inspection call for probes, tests and space diagnostics, never a hot-path one.</remarks>
		public FdbLiteTreeStatistics MeasureTreeStatistics()
		{
			var pin = BeginRead();
			try
			{
				return FdbLiteTreeStatistics.Measure(this.Pager, pin.RootPageId);
			}
			finally
			{
				EndRead(in pin);
			}
		}

		/// <summary>Tree-wide totals of the current durable generation, from its root page's aggregate block: O(1), exact, and safe on any thread.</summary>
		public FdbLiteTreeAggregates GetTreeAggregates()
		{
			var pin = BeginRead();
			try
			{
				return FdbLiteTreeAggregates.Read(this.Pager, pin.RootPageId);
			}
			finally
			{
				EndRead(in pin);
			}
		}

		/// <summary>Slow-reader observability: pin count, the oldest pinned generation, and the blocks retained only by unpromoted frees.</summary>
		public (int PinCount, ulong? OldestPinnedGeneration, long PendingReclaimBlocks) GetStats()
		{
			lock (this.PinLock)
			{
				int pins = 0;
				foreach (var kv in this.Pins) { pins += kv.Value; }
				return (pins, this.Pins.Count > 0 ? this.Pins.Keys.First() : null, this.FreeSpace.PendingBlockCount);
			}
		}

		#endregion

		/// <inheritdoc />
		public void Dispose() => this.Pager.Dispose();

	}

}
