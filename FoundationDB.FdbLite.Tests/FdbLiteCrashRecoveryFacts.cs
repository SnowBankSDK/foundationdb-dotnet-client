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

	/// <summary>Crash-injection harness for the commit protocol (step 1 of the single-fsync commit design note): a simulated crash persists an ARBITRARY SUBSET of the unflushed writes, optionally tearing one, because that is what an unbarriered device may legally do. The two-barrier protocol must recover to a fully valid generation (the crashed one or its predecessor) from EVERY crash point; the unsafe single-barrier bench knob must be OBSERVABLY corruptible by the same harness, which is the hazard the future commit manifest exists to close.</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteCrashRecoveryFacts : SimpleTest
	{

		private sealed class SimulatedCrashException : Exception
		{
		}

		/// <summary>Models durability: writes land in the LIVE image immediately but reach the DURABLE image only at <see cref="Flush"/>. A scheduled crash aborts the counted operation; <see cref="CrashNow"/> then persists a seeded-random subset of the pending writes (in order, optionally tearing one at a sector boundary) and hands back the durable image for reopening.</summary>
		/// <remarks>A write is recorded as pending BEFORE the crash check (an in-flight write may still land on the device); a flush checks FIRST (an fsync that never completed must not drain). Hole punches are advisory and only ever target unreferenced space, so they are not replayed into the durable image, which is the conservative simulation.</remarks>
		private sealed class CrashInjectionPager : IFdbLitePager
		{

			private FdbLiteHeapPager Live { get; }

			private FdbLiteHeapPager DurableImage { get; }

			private List<(uint FirstBlock, byte[] Data)> Pending { get; } = [ ];

			public int OperationCount { get; private set; }

			/// <summary>Counted operation (a write or a flush) at which the simulated crash fires; int.MaxValue = never.</summary>
			public int CrashAtOperation { get; set; } = int.MaxValue;

			public bool Crashed { get; private set; }

			public CrashInjectionPager(FdbLiteGeometry geometry)
			{
				this.Live = new FdbLiteHeapPager(geometry);
				this.DurableImage = new FdbLiteHeapPager(geometry);
			}

			/// <summary>Diagnostic: first block whose durable content differs from the live image (flushed state only), or -1.</summary>
			public long FirstDivergentBlock()
			{
				for (uint b = 0; b < Math.Min(this.Live.BlockCount, this.DurableImage.BlockCount); b++)
				{
					if (!this.Live.ReadBlocks(b, 1).SequenceEqual(this.DurableImage.ReadBlocks(b, 1)))
					{
						return b;
					}
				}
				return this.Live.BlockCount == this.DurableImage.BlockCount ? -1 : Math.Min(this.Live.BlockCount, this.DurableImage.BlockCount);
			}

			private void CountOperation()
			{
				if (this.Crashed || ++this.OperationCount >= this.CrashAtOperation)
				{
					this.Crashed = true;
					throw new SimulatedCrashException();
				}
			}

			/// <summary>The crash itself: a random subset of the pending writes reaches the durable image, in order; at most one persisted write is torn at a 512-byte boundary.</summary>
			public FdbLiteHeapPager CrashNow(Random rnd, bool tearOne)
			{
				int tearIndex = tearOne && this.Pending.Count > 0 ? rnd.Next(this.Pending.Count) : -1;
				for (int i = 0; i < this.Pending.Count; i++)
				{
					var (firstBlock, data) = this.Pending[i];
					if (rnd.Next(2) == 0 && i != tearIndex)
					{
						continue; // this write never reached the platter
					}
					ApplyToDurable(firstBlock, data, torn: i == tearIndex ? rnd : null);
				}
				this.Pending.Clear();
				return this.DurableImage;
			}

			private void ApplyToDurable(uint firstBlock, byte[] data, Random? torn)
			{
				int blockSize = this.Live.Geometry.BlockSize;
				uint needed = firstBlock + (uint) (data.Length / blockSize);
				if (this.DurableImage.BlockCount < needed)
				{
					this.DurableImage.Grow(needed);
				}
				if (torn is null)
				{
					this.DurableImage.WriteBlocks(firstBlock, data);
					return;
				}
				// torn: the head of the write is new, the tail keeps whatever the durable image already held
				int keep = torn.Next(data.Length / 512) * 512;
				var image = this.DurableImage.ReadBlocks(firstBlock, data.Length / blockSize).ToArray();
				data.AsSpan(0, keep).CopyTo(image);
				this.DurableImage.WriteBlocks(firstBlock, image);
			}

			public FdbLiteGeometry Geometry => this.Live.Geometry;

			public uint BlockCount => this.Live.BlockCount;

			public uint RegionSizeInBlocks => this.Live.RegionSizeInBlocks;

			public bool TrackFirstTouch
			{
				get => this.Live.TrackFirstTouch;
				set => this.Live.TrackFirstTouch = value;
			}

			public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count) => this.Live.ReadBlocks(firstBlock, count);

			public FdbLitePageRef ReadBlocksRef(uint firstBlock, int count) => this.Live.ReadBlocksRef(firstBlock, count);

			public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data)
			{
				this.Live.WriteBlocks(firstBlock, data);
				this.Pending.Add((firstBlock, data.ToArray()));
				CountOperation();
			}

			public void Flush()
			{
				CountOperation();
				foreach (var (firstBlock, data) in this.Pending)
				{
					ApplyToDurable(firstBlock, data, torn: null);
				}
				this.Pending.Clear();
			}

			public void Grow(uint minimumBlockCount) => this.Live.Grow(minimumBlockCount);

			public void Truncate(uint newBlockCount) => this.Live.Truncate(newBlockCount);

			public void PunchHole(uint firstBlock, uint count) => this.Live.PunchHole(firstBlock, count);

			public void Prefetch(uint firstBlock, uint count) => this.Live.Prefetch(firstBlock, count);

			public bool MarkTouched(uint firstBlock) => this.Live.MarkTouched(firstBlock);

			public void ResetFirstTouch() => this.Live.ResetFirstTouch();

			public void Dispose()
			{
				// deliberate no-op: the harness reopens both images after engine disposal (a clean close
				// followed by a reopen is itself a scenario under test), so the harness owns their lifetime
			}

		}

		/// <summary>Applies one seeded generation of mixed mutations to both the writer and the model.</summary>
		private static void ApplyGeneration(FdbLiteTreeWriter writer, SortedDictionary<string, byte[]> model, Random rnd, int ops)
		{
			var key = new byte[16];
			var value = new byte[200];
			var known = new List<string>(model.Keys);
			for (int i = 0; i < ops; i++)
			{
				if (known.Count > 8 && rnd.Next(10) < 3)
				{ // remove an existing key
					string victim = known[rnd.Next(known.Count)];
					if (model.Remove(victim))
					{
						Assert.That(writer.Remove(Convert.FromHexString(victim)), Is.True);
					}
					continue;
				}
				rnd.NextBytes(key);
				key[0] = 0x77;
				int len = rnd.Next(1, 200);
				rnd.NextBytes(value.AsSpan(0, len));
				string hex = Convert.ToHexString(key);
				writer.Insert(key, value.AsSpan(0, len));
				model[hex] = value.AsSpan(0, len).ToArray();
				known.Add(hex);
			}
		}

		/// <summary>Scans the mounted store and compares it to the model; false (with a reason) instead of throwing, so the discriminator can COUNT corruption. Any exception during the walk (torn page checksums, garbage offsets) is corruption by definition.</summary>
		private static bool TryValidate(FdbLiteHeapPager durable, ulong generationCurr, SortedDictionary<string, byte[]> modelCurr, ulong generationPrev, SortedDictionary<string, byte[]> modelPrev, out string reason, out ulong mountedGeneration)
		{
			mountedGeneration = 0;
			try
			{
				// the engine owns and DISPOSES its pager, so the durable image supports exactly ONE mount:
				// everything the caller wants to know must come out of this open
				using var reopened = FdbLiteEngine.Open(durable);
				mountedGeneration = reopened.Durable.Generation;
				SortedDictionary<string, byte[]>? expected =
					reopened.Durable.Generation == generationCurr ? modelCurr
					: reopened.Durable.Generation == generationPrev ? modelPrev
					: null;
				if (expected is null)
				{
					reason = $"recovered generation {reopened.Durable.Generation} is neither {generationCurr} nor {generationPrev}";
					return false;
				}

				int seen = 0;
				var cursor = new FdbLiteTreeCursor(reopened.Pager, reopened.Durable.RootPageId);
				if (reopened.Durable.RootPageId != 0 && cursor.SeekFirst())
				{
					do
					{
						string hex = Convert.ToHexString(cursor.CurrentKey);
						if (!expected.TryGetValue(hex, out var want))
						{
							reason = $"generation {reopened.Durable.Generation}: key {hex} is not in the model";
							return false;
						}
						if (!cursor.CurrentValue.SequenceEqual(want))
						{
							reason = $"generation {reopened.Durable.Generation}: value mismatch for {hex}";
							return false;
						}
						seen++;
					}
					while (cursor.MoveNext());
				}
				if (seen != expected.Count)
				{
					reason = $"generation {reopened.Durable.Generation}: scanned {seen} keys, model has {expected.Count}";
					return false;
				}
				reason = "";
				return true;
			}
			catch (Exception e)
			{
				reason = $"walk failed: {e.GetType().Name}: {e.Message}";
				return false;
			}
		}

		[Test]
		public void Pages_Written_By_This_Open_Count_As_Touched()
		{
			// first-touch checksum verification exists for blocks whose content this process has NOT seen;
			// a page this open just computed and wrote needs no re-verification, and before this contract a
			// build-then-scan in one open re-hashed its whole store (measured +2.2 us/op on value scans, the
			// range-scan regression of 2026-08-06)
			var pager = new FdbLiteHeapPager(FdbLiteGeometry.Uniform(14));
			var engine = FdbLiteEngine.Create(pager);
			var rnd = new Random(1618);
			var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
			var w = engine.BeginWrite();
			ApplyGeneration(w, model, rnd, ops: 400);
			engine.Commit(w, 1);
			uint root = engine.Durable.RootPageId;
			Assert.That(root, Is.Not.Zero);
			Assert.That(pager.MarkTouched(root), Is.False, "the root page was WRITTEN by this open: its first read must not count as a first touch (no pointless re-verification of bytes this process computed)");
		}

		[Test]
		public void Diag_Completed_Commit_Images_Are_Identical()
		{
			var rnd = new Random(9999);
			var pager = new CrashInjectionPager(FdbLiteGeometry.Uniform(14));
			var engine = FdbLiteEngine.Create(pager);
			var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
			ulong generation = 0;
			for (int g = 0; g < 4; g++)
			{
				var w = engine.BeginWrite();
				ApplyGeneration(w, model, rnd, ops: 400);
				engine.Commit(w, ++generation);
			}
			long divergent = pager.FirstDivergentBlock();
			Log($"live blocks={pager.BlockCount}, first divergent={divergent}");
			Assert.That(divergent, Is.EqualTo(-1), "after completed commits every flushed block must match the live image");
		}

		/// <summary>Runs warm generations, then arms a crash at operation offset <paramref name="crashOffset"/> inside one more commit; returns everything the validation needs.</summary>
		private static (FdbLiteHeapPager Durable, ulong GenCurr, SortedDictionary<string, byte[]> ModelCurr, ulong GenPrev, SortedDictionary<string, byte[]> ModelPrev, long SingleFsyncCommits) RunCrashScenario(int seed, int crashOffset, bool tearOne, bool unsafeSingleBarrier, bool singleFsync = false)
		{
			var rnd = new Random(seed);
			var pager = new CrashInjectionPager(FdbLiteGeometry.Uniform(14));
			var engine = FdbLiteEngine.Create(pager);
			engine.UnsafeSingleCommitBarrier = unsafeSingleBarrier;
			engine.SingleFsyncCommits = singleFsync;

			var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
			ulong generation = 0;
			for (int g = 0; g < 3; g++)
			{ // warm, uncrashed generations: their commits complete and drain to the durable image
				var w = engine.BeginWrite();
				ApplyGeneration(w, model, rnd, ops: 400);
				engine.Commit(w, ++generation);
			}
			var modelPrev = new SortedDictionary<string, byte[]>(model, StringComparer.Ordinal);
			ulong genPrev = engine.Durable.Generation;

			// the crash generation
			var writer = engine.BeginWrite();
			ApplyGeneration(writer, model, rnd, ops: 400);
			pager.CrashAtOperation = pager.OperationCount + crashOffset;
			try
			{
				engine.Commit(writer, ++generation);
			}
			catch (SimulatedCrashException)
			{
			}
			var durable = pager.CrashNow(rnd, tearOne);
			return (durable, genPrev + 1, model, genPrev, modelPrev, engine.SingleFsyncCommitCount);
		}

		[Test]
		public void Manifest_Commit_Recovers_From_Every_Crash_Point()
		{
			// THE FEATURE'S ACCEPTANCE GATE: with single-fsync commits ON (manifest + in-use flag), every
			// crash point that corrupts the unsafe knob must instead recover to a valid N or N-1, because the
			// recovery validates the manifest and falls back. Same sweep as the two-barrier pin.
			for (int seed = 1; seed <= 8; seed++)
			{
				for (int crashOffset = 1; crashOffset <= 24; crashOffset += 1)
				{
					var s = RunCrashScenario(seed * 5555, crashOffset, tearOne: (seed + crashOffset) % 3 == 0, unsafeSingleBarrier: false, singleFsync: true);
					Assert.That(s.SingleFsyncCommits, Is.GreaterThan(0), $"seed {seed}: no commit took the single-fsync path, so this sweep gates nothing");
					bool clean = TryValidate(s.Durable, s.GenCurr, s.ModelCurr, s.GenPrev, s.ModelPrev, out var reason, out _);
					Assert.That(clean, Is.True, $"seed {seed} crashOffset {crashOffset}: {reason}");
				}
			}
		}

		[Test]
		public void Clean_Shutdown_Skips_Recovery_Validation_And_A_Crash_Pays_It()
		{
			// the in-use flag's whole point: an orderly close leaves nothing to validate at the next open,
			// and only a crashed session pays a (bounded) validation pass
			var rnd = new Random(31337);
			var pager = new CrashInjectionPager(FdbLiteGeometry.Uniform(14));
			var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
			ulong generation = 0;
			var engine = FdbLiteEngine.Create(pager);
			engine.SingleFsyncCommits = true;
			for (int g = 0; g < 2; g++)
			{
				var w = engine.BeginWrite();
				ApplyGeneration(w, model, rnd, ops: 300);
				engine.Commit(w, ++generation);
			}
			Assert.That(engine.SingleFsyncCommitCount, Is.GreaterThan(0), "the option is dead: no commit took the single-fsync path");
			ulong closedGeneration = engine.Durable.Generation;
			engine.Dispose(); // orderly close: clears the in-use flag (and flushes it)

			// clean reopen: no validation work
			var reopened = FdbLiteEngine.Open(pager);
			Assert.That(reopened.RecoveryValidatedPages, Is.Zero, "a cleanly closed store must not pay recovery validation");
			Assert.That(reopened.Durable.Generation, Is.EqualTo(closedGeneration));

			// now crash a commit and reopen: validation must RUN (the counter is the execution proof)
			reopened.SingleFsyncCommits = true;
			var writer = reopened.BeginWrite();
			ApplyGeneration(writer, model, rnd, ops: 300);
			pager.CrashAtOperation = pager.OperationCount + 10;
			try
			{
				reopened.Commit(writer, ++generation);
			}
			catch (SimulatedCrashException)
			{
			}
			var durable = pager.CrashNow(rnd, tearOne: false);
			using var recovered = FdbLiteEngine.Open(durable);
			Assert.That(recovered.RecoveryValidatedPages, Is.GreaterThan(0), "a crashed session must pay the validation pass: zero means the in-use flag never reached the file");
		}

		[Test]
		public void Two_Barrier_Commit_Recovers_From_Every_Crash_Point()
		{
			// the pin: today's protocol survives ANY persisted subset at ANY crash point, because the data
			// barrier orders pages before the header - a durable header implies durable pages
			for (int seed = 1; seed <= 8; seed++)
			{
				for (int crashOffset = 1; crashOffset <= 24; crashOffset += 1)
				{
					var s = RunCrashScenario(seed * 1000, crashOffset, tearOne: (seed + crashOffset) % 3 == 0, unsafeSingleBarrier: false);
					bool clean = TryValidate(s.Durable, s.GenCurr, s.ModelCurr, s.GenPrev, s.ModelPrev, out var reason, out _);
					Assert.That(clean, Is.True, $"seed {seed} crashOffset {crashOffset}: {reason}");
				}
			}
		}

		[Test]
		public void Crash_Beyond_The_Commit_Recovers_The_Committed_Generation()
		{
			// a crash offset past the commit's operation count means the commit completed: recovery must then
			// mount the NEW generation, or the harness would pass vacuously by always rolling back
			var s = RunCrashScenario(seed: 42, crashOffset: 100_000, tearOne: false, unsafeSingleBarrier: false);
			bool clean = TryValidate(s.Durable, s.GenCurr, s.ModelCurr, s.GenPrev, s.ModelPrev, out var reason, out ulong mounted);
			Assert.That(clean, Is.True, reason);
			Assert.That(mounted, Is.EqualTo(s.GenCurr), "the completed commit must be the mounted generation");
		}

		[Test]
		public void The_Harness_Observes_The_Corruption_The_Single_Barrier_Mode_Permits()
		{
			// the instrument's own proof: with the data barrier skipped (the bench-only unsafe knob), SOME
			// crash point must produce a store the validation rejects - the header persisting without its
			// pages. If this finds nothing, the harness is too weak to gate the manifest design.
			// When the commit-manifest recovery lands, this expectation FLIPS: every such crash must then
			// roll back cleanly, and this test becomes the feature's acceptance gate.
			int corrupted = 0;
			var reasons = new List<string>();
			for (int seed = 1; seed <= 8; seed++)
			{
				for (int crashOffset = 1; crashOffset <= 24; crashOffset += 1)
				{
					var s = RunCrashScenario(seed * 7777, crashOffset, tearOne: false, unsafeSingleBarrier: true);
					if (!TryValidate(s.Durable, s.GenCurr, s.ModelCurr, s.GenPrev, s.ModelPrev, out var reason, out _))
					{
						corrupted++;
						if (reasons.Count < 5) { reasons.Add($"seed {seed} offset {crashOffset}: {reason}"); }
					}
				}
			}
			Log($"single-barrier crashes observed corrupt: {corrupted} of 192");
			foreach (var r in reasons) { Log($"  e.g. {r}"); }
			Assert.That(corrupted, Is.GreaterThan(0), "no crash point corrupted the single-barrier store: the harness cannot see the hazard it exists to gate");
		}

	}

}
