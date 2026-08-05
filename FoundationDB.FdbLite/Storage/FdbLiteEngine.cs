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
		public ulong RetainFloor
		{
			get => field;
			set
			{
				// lowering (retaining more) is always safe; RAISING re-enables promotion of generations the old
				// floor retained - and readers of a retain-all store hold no engine pins at all, so their pages
				// would be reused under them. A raise is therefore only legal while nothing is retained yet.
				Contract.Requires(value <= field || this.FreeSpace.PendingBlockCount == 0, "raising RetainFloor over retained generations would reclaim blocks readers can still see");
				field = value;
			}
		} = ulong.MaxValue;

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
			// blank means genuinely UNWRITTEN: a fresh file or a preallocation reads all-zero. Anything else -
			// a valid store, a store with torn headers, or a FOREIGN file - must never be overwritten by Create
			// (the old header-checksum test called a foreign file "blank", and Create clobbered its head)
			uint probe = Math.Min(pager.BlockCount, 3u);
			for (uint i = 0; i < probe; i++)
			{
				if (pager.ReadBlocks(i, 1).ContainsAnyExcept((byte) 0))
				{
					return false;
				}
			}
			return true;
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
		/// <param name="path">The path to the store file.</param>
		/// <param name="geometry">The geometry of the store.</param>
		/// <param name="regionSizeInBytes">The size of each region in bytes.</param>
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
		public FdbLiteTreeWriter BeginWrite() => new(this.Pager, this.Allocator, this.Durable.Generation + 1, this.Durable.RootPageId, this.PageBufferPool) { AvoidSequentialAppendSplits = this.AvoidSequentialAppendSplits };

		/// <summary>Slots the leaf directory reserves each time a splice exhausts its headroom.</summary>
		/// <remarks>Exposed here because the page layout itself is internal, and a benchmark has to be able to measure this both ways in one window. <b>1 reproduces the pre-headroom behaviour</b>, where the key area slides on every insert; the default is 32. Process-wide, and meant for measurement rather than for tuning a live store.</remarks>
		public static int LeafSlotGrowth
		{
			get => FdbLiteTreePage.SlotGrowth;
			set => FdbLiteTreePage.SlotGrowth = value;
		}

		/// <summary>Page-image buffers recycled across generations, so a write workload stops allocating one page per page it touches.</summary>
		/// <remarks>Owned here rather than by the writer because a writer lives for exactly one generation, which is the interval the buffers have to OUTLIVE to be worth pooling. Single-writer by construction (one writable generation at a time), so no synchronization.</remarks>
		private Stack<byte[]> PageBufferPool { get; } = new();

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

		/// <summary>Sums the free-space map: bytes already reusable, and bytes awaiting their promotion generation.</summary>
		/// <remarks>An inspection call for space diagnostics: together with <see cref="FdbLiteSnapshotHeader.AllocationFrontier"/> and the tree aggregates it decomposes the file into live tree, circulating free space, and allocation headroom. Serialized under the single-writer model: call it between write generations.</remarks>
		public (long ReusableBytes, long PendingBytes) MeasureFreeSpace()
		{
			int blockSizeLog2 = this.Pager.Geometry.BlockSizeLog2;
			long reusable = 0;
			long pending = 0;
			foreach (var (generation, _, count) in this.FreeSpace.Enumerate())
			{
				if (generation == 0) { reusable += (long) count << blockSizeLog2; }
				else { pending += (long) count << blockSizeLog2; }
			}
			return (reusable, pending);
		}

		/// <summary>Vacuum steps that merged something over this engine's lifetime</summary>
		public long VacuumStepsExecuted { get; private set; }

		/// <summary>Net pages freed by the background vacuum over this engine's lifetime</summary>
		public long VacuumPagesFreed { get; private set; }

		/// <summary>Runs one background-vacuum step: a maintenance generation with NO logical changes that merges the worst region's adjacent sparse leaves at the volatility-adaptive fill target.</summary>
		/// <param name="maxInputPages">Bound on leaves one step may consume (the wall-clock bound of a step)</param>
		/// <returns>What the step did; <c>PagesFreed == 0</c> means no viable run exists and nothing was written - the caller's signal to stop its trigger loop</returns>
		/// <remarks>
		/// <para>Serialized under the single-writer model like any write generation: never call it while another writer is open. The step commits at the CURRENT database version (readers observe no logical change), the trigger belongs to the caller: <see cref="GetTreeAggregates"/> is the O(1) signal (<c>LeafCount</c> against <c>ceil(LeafLiveBytes / (0.90 x pageSize))</c> is the tree's reclaim opportunity).</para>
		/// <para>Whatever the pre-commit arm skips on budget falls to this arm by construction: its candidacy is OCCUPANCY, not the commit-time notes, so any leaf that sits sparse - noted or never noted - is found here. Cross-parent runs (out of the pre-commit arm's scope) are in scope here and only here.</para>
		/// </remarks>
		public FdbLiteTreeWriter.VacuumOutcome VacuumStep(int maxInputPages = 16)
		{
			var writer = BeginWrite();
			var outcome = writer.VacuumWorstRegion(maxInputPages);
			if (outcome.InputPages == 0)
			{ // nothing viable: the writer allocated and wrote nothing, so abandoning it leaves no trace
				return outcome;
			}
			Commit(writer, this.Durable.DatabaseVersion);
			this.VacuumStepsExecuted++;
			this.VacuumPagesFreed += outcome.PagesFreed;
			return outcome;
		}

		/// <summary>Applies an ordered run of key/value pairs to <c>[begin, end)</c>, grafting whole pages when the run owns the range outright.</summary>
		/// <param name="run">Pairs in strictly ascending key order, every key inside <c>[begin, end)</c>.</param>
		/// <param name="begin">Inclusive lower bound of the range the run targets.</param>
		/// <param name="end">Exclusive upper bound of the range the run targets.</param>
		/// <param name="options">Declared volatility, which selects the fill target; see <see cref="FdbLiteImportOptions"/>.</param>
		/// <param name="databaseVersion">Version the import's generation is published at. The CALLER owns the version counter, exactly as for <see cref="Commit"/>: the engine never invents one.</param>
		/// <returns>Number of keys applied.</returns>
		/// <remarks>
		/// <para>The gate is EXACT rather than a share of the range: the graft path clears <c>[begin, end)</c> before it
		/// renders, so it may only run when the run supplies every key the range already holds, and a key the run does
		/// not supply is never dropped. A restore owns 100% of its range by construction and always takes the graft; an
		/// ordinary commit owns a fraction of a percent and always takes the fallback. Grafting AROUND a handful of
		/// survivors (one gap-graft per survivor, the design's situation D) is the follow-up that would move the gate off
		/// zero, and until it exists a partial run is served correctly by the fallback rather than incorrectly by the
		/// graft.</para>
		/// <para>That a fallback exists at all is what the density curve says: the graft is worth its cost only when the
		/// run owns a large share of the range it targets. Measured on 500,000 keys, fill against how much of the range
		/// one ordered run covers is a VALLEY: 74% at 0.2% coverage, bottoming at 67% around a tenth, then 79% at half
		/// and 99% at full, while scattered arrival sits flat at 76%. Ordered arrival therefore only beats scattered from
		/// roughly half the range upward, and below that it packs no better while costing up to 20x the CPU.</para>
		/// <para>Keys and values are stored INLINE. A key longer than <see cref="FdbLiteTreePage.MaxKeyLength"/> or a
		/// value longer than <see cref="FdbLiteGeometry.MaxInlineValueLength"/> is rejected instead of being silently
		/// truncated: the out-of-line extent path that <c>Insert</c> uses is not wired into the graft renderer yet.</para>
		/// <para>Everything the caller can get wrong is checked BEFORE any write begins, so a rejected import leaves the
		/// store exactly as it was.</para>
		/// <para>Commits its own generation, at <paramref name="databaseVersion"/>. An import changes what readers see,
		/// so - unlike <see cref="VacuumStep"/>, which republishes the current version because it changes nothing
		/// logically - reusing the previous version would make that generation unreachable through
		/// <see cref="TryBeginReadAtVersion"/>.</para>
		/// <para>Serialized under the single-writer model like any write generation: never call it while another
		/// writer is open - it opens (and commits) its own.</para>
		/// </remarks>
		/// <exception cref="ArgumentException">The range is empty or its upper bound is too long, or the run is not in strictly ascending key order, or it holds a key outside <c>[begin, end)</c>, an oversized key, or an oversized value.</exception>
		public int Import(IEnumerable<KeyValuePair<Slice, Slice>> run, Slice begin, Slice end, FdbLiteImportOptions options, ulong databaseVersion)
		{
			Contract.NotNull(run);
			if (begin.Span.SequenceCompareTo(end.Span) >= 0)
			{
				throw new ArgumentException("The end key must sort strictly above the begin key: the imported range cannot be empty.", nameof(end));
			}
			// the graft writes `end` as a separator when it repairs the bound the clear left behind, so the range's
			// upper bound is held to the same ceiling as a key
			if (end.Count > FdbLiteTreePage.MaxKeyLength)
			{
				throw new ArgumentException($"The imported range's upper bound is {end.Count} bytes long, which exceeds the maximum key length of {FdbLiteTreePage.MaxKeyLength}: the bound is written as a separator, so it is held to the same ceiling as a key.", nameof(end));
			}

			int maxInline = this.Pager.Geometry.MaxInlineValueLength;
			var cells = new List<FdbLiteTreeWriter.CellRef>();
			foreach (var kv in run)
			{
				var key = kv.Key.Span;
				var value = kv.Value.Span;
				if (key.Length > FdbLiteTreePage.MaxKeyLength)
				{
					throw new ArgumentException($"The key at index {cells.Count} of the run is {key.Length} bytes long, which exceeds the maximum key length of {FdbLiteTreePage.MaxKeyLength}.", nameof(run));
				}
				if (value.Length > maxInline)
				{
					throw new ArgumentException($"The value at index {cells.Count} of the run is {value.Length} bytes long, which exceeds the maximum inline value length of {maxInline}: a longer value needs the out-of-line extent path, which Import does not implement.", nameof(run));
				}
				if (key.SequenceCompareTo(begin.Span) < 0 || key.SequenceCompareTo(end.Span) >= 0)
				{
					throw new ArgumentException($"The key at index {cells.Count} of the run falls outside the imported range [begin, end).", nameof(run));
				}
				if (cells.Count > 0 && cells[^1].ResolveKey(default).SequenceCompareTo(key) >= 0)
				{
					throw new ArgumentException($"The key at index {cells.Count} of the run does not sort strictly above the one before it: the run must be in strictly ascending key order.", nameof(run));
				}

				// ONE buffer per cell, key then value, which is the shape both paths below read: the graft renders
				// these cells as they are, and the fallback resolves the two parts back out of them, so the run is
				// materialised once whichever path is taken
				var buffer = new byte[key.Length + value.Length];
				key.CopyTo(buffer);
				value.CopyTo(buffer.AsSpan(key.Length));
				cells.Add(FdbLiteTreeWriter.CellRef.OfLeafBuffer(buffer, key.Length, value.Length, 0));
			}
			if (cells.Count == 0)
			{
				return 0;
			}

			// the list IS the backing array: a span over it skips a full copy of the run (~24 MB on a 500k restore)
			ReadOnlySpan<FdbLiteTreeWriter.CellRef> all = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(cells);
			var writer = BeginWrite();

			// stopAfter is 1 because the gate is "no survivor at all": once one exists the answer is settled, and
			// the walk has nothing left to learn. The count IS the density test - a run with survivors in its range
			// is by definition not the whole of it.
			if (writer.CountSurvivors(begin.Span, end.Span, all, stopAfter: 1) == 0)
			{
				writer.ImportRun(begin.Span, end.Span, all, options.FillCeiling(this.Pager.Geometry.PageSize), options.Volatility);
			}
			else
			{ // sparse: per-key insertion, which is what the measured curve says is right below about half coverage
				foreach (ref readonly var cell in all)
				{
					writer.Insert(cell.ResolveKey(default), cell.ResolveValue(default));
				}
			}

			Commit(writer, databaseVersion);
			return all.Length;
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
		/// <remarks>Disposing unmaps the store: a reader still holding a pinned snapshot would then dereference
		/// unmapped memory - a native fault, not a managed exception - so live pins fail this loudly instead.</remarks>
		public void Dispose()
		{
			lock (this.PinLock)
			{
				Contract.Requires(this.Pins.Count == 0, "the engine still has pinned readers: disposing would unmap memory a reader can still touch");
			}
			this.Pager.Dispose();
		}

	}

}
