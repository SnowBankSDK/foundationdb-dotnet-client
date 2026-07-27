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
	using System.Text;
	using FoundationDB.Storage.FdbLite;
	using SnowBank.Data.Tuples;

	/// <summary>Exact write-amplification and occupancy numbers per candidate geometry: the machine-independent axis of the FL-17 decision matrix (bytes written are deterministic; only timings need BenchmarkDotNet).</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteWriteAmplificationFacts : SimpleTest
	{

		/// <summary>Pager wrapper counting bytes written through it: total (the CPU/copy cost) and unique dirty blocks (what actually reaches the disk per flush window, since the OS coalesces rewrites)</summary>
		private sealed class CountingPager : IFdbLitePager
		{
			public required IFdbLitePager Inner { get; init; }
			public long BytesWritten { get; private set; }
			public int FlushCount { get; private set; }
			private HashSet<uint> DirtyBlocks { get; } = [ ];
			private long FlushedUniqueBytes;
			/// <summary>Bytes that reached (or will reach) the disk: unique dirty blocks per flush window, summed across windows (each flush forces its window out)</summary>
			public long UniqueBytesWritten => this.FlushedUniqueBytes + ((long) this.DirtyBlocks.Count * this.Geometry.BlockSize);

			public FdbLiteGeometry Geometry => this.Inner.Geometry;
			public uint BlockCount => this.Inner.BlockCount;
			public uint RegionSizeInBlocks => this.Inner.RegionSizeInBlocks;
			public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count) => this.Inner.ReadBlocks(firstBlock, count);
			public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data)
			{
				this.BytesWritten += data.Length;
				int blocks = data.Length >> this.Geometry.BlockSizeLog2;
				for (int i = 0; i < blocks; i++) { this.DirtyBlocks.Add(firstBlock + (uint) i); }
				this.Inner.WriteBlocks(firstBlock, data);
			}
			public void Flush()
			{
				this.FlushCount++;
				this.FlushedUniqueBytes += (long) this.DirtyBlocks.Count * this.Geometry.BlockSize;
				this.DirtyBlocks.Clear();
				this.Inner.Flush();
			}
			public void Grow(uint minimumBlockCount) => this.Inner.Grow(minimumBlockCount);
			public void Truncate(uint newBlockCount) => this.Inner.Truncate(newBlockCount);
			public void Dispose() => this.Inner.Dispose();
			public void ResetCounters() { this.BytesWritten = 0; this.FlushCount = 0; this.DirtyBlocks.Clear(); this.FlushedUniqueBytes = 0; }
		}

		/// <summary>A page touched N times in one transaction must reach the pager ONCE, at commit: dirty pages live in the writer until then.</summary>
		/// <remarks>
		/// <para>The structural guard against per-mutation page writes. Values stay inline (no extents), so every byte the pager sees during the transaction would be a tree page written before it was finished.</para>
		/// <para>Sequential keys concentrate the inserts on one leaf, so the whole run touches a handful of pages: without buffering the pager sees one full page image PER INSERT.</para>
		/// </remarks>
		[Test]
		public void Writer_Buffers_Dirty_Pages_Until_Commit()
		{
			var geometry = FdbLiteGeometry.Default;
			var counting = new CountingPager { Inner = new FdbLiteHeapPager(geometry) };
			using var cleanup = counting;
			var engine = FdbLiteEngine.Create(counting);

			var writer = engine.BeginWrite();
			counting.ResetCounters();

			const int N = 2_000;
			for (int i = 0; i < N; i++)
			{
				var key = new byte[8];
				BinaryPrimitives.WriteInt64BigEndian(key, i);
				writer.Insert(key, "0123456789ABCDEF"u8);
			}

			Assert.That(counting.BytesWritten, Is.Zero, "no tree page may reach the pager before commit: dirty pages belong to the writer until then");

			engine.Commit(writer, 1);

			// after commit every dirty page has been written exactly once, so the cost is bounded by the pages
			// actually touched (a few leaves + the spine + the free list), NOT by the number of inserts
			long pagesWritten = counting.BytesWritten / geometry.PageSize;
			Log($"{N:N0} sequential inserts -> {counting.BytesWritten:N0} B written ({pagesWritten:N0} page-equivalents), {(double) counting.BytesWritten / N:N1} B/insert");
			Assert.That(counting.BytesWritten, Is.GreaterThan(0), "the generation must be durable after commit");
			Assert.That(pagesWritten, Is.LessThan(N / 10), "page writes must scale with pages touched, not with inserts");

			// and the tree is intact
			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) N));
			var snapshot = engine.BeginRead();
			try
			{
				var cursor = new FdbLiteTreeCursor(counting, snapshot.RootPageId);
				int seen = 0;
				if (cursor.SeekFirst())
				{
					do { seen++; } while (cursor.MoveNext());
				}
				Assert.That(seen, Is.EqualTo(N), "every key must be readable after commit");
			}
			finally
			{
				engine.EndRead(snapshot);
			}
		}

		private static byte[] SequentialKey(int i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		/// <summary>Walks the whole committed tree and returns every key in order.</summary>
		private static List<byte[]> ScanForward(IFdbLitePager pager, uint root)
		{
			var keys = new List<byte[]>();
			var cursor = new FdbLiteTreeCursor(pager, root);
			if (cursor.SeekFirst())
			{
				do { keys.Add(cursor.CurrentKey.ToArray()); } while (cursor.MoveNext());
			}
			return keys;
		}

		/// <summary>A sorted run of inserts must descend from the root once per LEAF, not once per key.</summary>
		/// <remarks>The structural guard for cursor reuse: the descent is O(height) page searches, so paying it per key makes a bulk load cost tree height times more page searches than it needs.</remarks>
		[Test]
		public void Writer_Descends_Once_Per_Leaf_On_A_Sorted_Run()
		{
			var geometry = FdbLiteGeometry.Default;
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);

			var writer = engine.BeginWrite();
			const int N = 5_000;
			for (int i = 0; i < N; i++)
			{
				writer.Insert(SequentialKey(i), "0123456789ABCDEF"u8);
			}

			Log($"{N:N0} sequential inserts -> {writer.LeafDescents:N0} descents, {writer.CellsSpliced:N0} splices, {writer.PageSplits:N0} splits, {writer.PagesAppended:N0} appended pages");
			Assert.That(writer.LeafDescents, Is.LessThan(N / 10), "a sorted run must descend once per leaf (and once per structural change), not once per key");

			engine.Commit(writer, 1);
			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) N));
			Assert.That(ScanForward(pager, engine.Durable.RootPageId), Has.Count.EqualTo(N), "every key must be readable after commit");
		}

		/// <summary>A replace that changes no key and no length must overwrite the value where it lies, not rebuild the page.</summary>
		/// <remarks>
		/// <para>This is the structural guard for the REPLACE path, and the counterpart to the insert guard
		/// above. A replacement that occupies exactly the same room needs no new space, moves no other cell and
		/// shifts no offset: it is a memcpy over bytes this generation already owns. Rebuilding the page for it
		/// costs O(cells) per mutation instead.</para>
		/// <para>Measured against the prototype this engine succeeds, that difference is 4x to 86x on
		/// replace-heavy workloads. It survived for weeks because it is invisible to every correctness
		/// assertion in the suite: the answers stay right, they just cost far more to produce. Hence a test
		/// that asserts the MECHANISM fired rather than that the results are still correct.</para>
		/// </remarks>
		[Test]
		public void Writer_Overwrites_A_Same_Length_Value_In_Place()
		{
			var geometry = FdbLiteGeometry.Default;
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);

			const int N = 5_000;
			var seed = engine.BeginWrite();
			for (int i = 0; i < N; i++)
			{
				seed.Insert(SequentialKey(i), "0123456789ABCDEF"u8);
			}
			engine.Commit(seed, 1);

			// same keys, same value LENGTH, different bytes
			var writer = engine.BeginWrite();
			for (int i = 0; i < N; i++)
			{
				writer.Insert(SequentialKey(i), "FEDCBA9876543210"u8);
			}

			Log($"{N:N0} same-length replaces -> {writer.CellsOverwritten:N0} overwritten, {writer.ReplacesRebuilt:N0} rebuilt, {writer.LeafDescents:N0} descents, {writer.PageSplits:N0} splits");

			// Every replace is accounted for as exactly one of the two outcomes.
			Assert.That(writer.CellsOverwritten + writer.ReplacesRebuilt, Is.EqualTo(N), "every replace must be either overwritten in place or rebuilt, and counted once");

			// A handful of rebuilds is CORRECT and unavoidable: the first mutation of a page in a generation
			// has to copy it, because until then it is still shared with the committed generation. So the floor
			// is one rebuild per leaf TOUCHED, and what must not happen is one per KEY. Bounding this against
			// the descent count rather than a constant states that directly, and keeps the test honest if the
			// page size or the value size changes the number of leaves.
			Assert.That(writer.ReplacesRebuilt, Is.LessThanOrEqualTo(writer.LeafDescents), "rebuilds must be bounded by the pages first touched, not by the number of keys");
			Assert.That(writer.ReplacesRebuilt, Is.LessThan(N / 100), "a replace-heavy generation must not rebuild per key");
			Assert.That(writer.LeafDescents, Is.LessThan(N / 10), "an in-place overwrite must not pay a fresh root-to-leaf descent per key");
			Assert.That(writer.PageSplits, Is.Zero, "a value of identical length cannot make a page need splitting");

			engine.Commit(writer, 2);
			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) N), "a replace must not change the key count");

			// the point of the exercise is still that it REPLACED: check the new bytes actually landed
			var pin = engine.BeginRead();
			try
			{
				for (int i = 0; i < N; i += 97)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, pin.RootPageId, SequentialKey(i), out var value), Is.True, $"key {i} must still be present");
					Assert.That(value.SequenceEqual("FEDCBA9876543210"u8), Is.True, $"key {i} must hold the REPLACEMENT value");
				}
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		/// <summary>Keys arriving out of order must land in the right leaf: a cached cursor position only applies to the key range the descent proved it covers.</summary>
		[Test]
		public void Writer_Cursor_Survives_Out_Of_Order_Inserts()
		{
			var geometry = FdbLiteGeometry.Default;
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);

			var writer = engine.BeginWrite();
			const int N = 5_000;

			// a sorted prefix (warms the cursor), then keys scattered across the leaves it already left behind,
			// then a second sorted run over the gaps: every one of them must be routed by the tree, not by the cursor
			for (int i = 0; i < N; i += 2)
			{
				writer.Insert(SequentialKey(i), "even"u8);
			}
			var rnd = new Random(1234);
			var odds = Enumerable.Range(0, N / 2).Select(i => (i * 2) + 1).ToArray();
			rnd.Shuffle(odds);
			foreach (var i in odds)
			{
				writer.Insert(SequentialKey(i), "odd"u8);
			}
			engine.Commit(writer, 1);

			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) N));
			var keys = ScanForward(pager, engine.Durable.RootPageId);
			Assert.That(keys, Has.Count.EqualTo(N));
			for (int i = 0; i < N; i++)
			{
				Assert.That(keys[i], Is.EqualTo(SequentialKey(i)).AsCollection, $"key #{i} out of place");
			}
		}

		private static (uint FrontierBlocks, int Splits, int Appended, int Keys) SeedSorted(bool avoidAppendSplits, int n)
		{
			var geometry = FdbLiteGeometry.Default;
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);
			engine.AvoidSequentialAppendSplits = avoidAppendSplits;

			var writer = engine.BeginWrite();
			for (int i = 0; i < n; i++)
			{
				writer.Insert(SequentialKey(i), "0123456789ABCDEF"u8);
			}
			int splits = writer.PageSplits;
			int appended = writer.PagesAppended;
			engine.Commit(writer, 1);

			// the tree must be identical either way: same keys, same order
			var keys = ScanForward(pager, engine.Durable.RootPageId);
			Assert.That(keys, Has.Count.EqualTo(n), $"[avoid={avoidAppendSplits}] every key must be readable");
			Assert.That(keys[^1], Is.EqualTo(SequentialKey(n - 1)).AsCollection, $"[avoid={avoidAppendSplits}] last key out of place");
			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) n), $"[avoid={avoidAppendSplits}]");
			return (engine.Durable.AllocationFrontier, splits, appended, keys.Count);
		}

		/// <summary>Appending past the last key of the rightmost leaf must start a fresh page, so finished pages stay packed instead of settling at the ~50% occupancy of a balanced split.</summary>
		[Test]
		public void Sequential_Append_Packs_Right_Edge_Pages()
		{
			const int N = 20_000;
			var on = SeedSorted(avoidAppendSplits: true, N);
			var off = SeedSorted(avoidAppendSplits: false, N);

			Log($"{N:N0} sorted inserts: avoid=ON  {on.FrontierBlocks:N0} blocks, {on.Splits:N0} splits, {on.Appended:N0} appended pages");
			Log($"{N:N0} sorted inserts: avoid=OFF {off.FrontierBlocks:N0} blocks, {off.Splits:N0} splits, {off.Appended:N0} appended pages");

			Assert.That(off.Appended, Is.Zero, "the knob must be honoured: no fresh right-edge page when it is off");
			Assert.That(on.Appended, Is.GreaterThan(0), "a sorted run appends past the rightmost leaf on every page boundary");
			Assert.That(on.FrontierBlocks, Is.LessThan(off.FrontierBlocks * 3 / 4), "packing the right edge must cost substantially fewer blocks than balanced splits");
		}

		/// <summary>The append bet's own falsification: a right-edge page packed to 100% splits on the first later insert into it, so append-THEN-UPDATE is where packing could lose what sorted load won.</summary>
		/// <remarks>
		/// <para>The comparative benchmark cannot answer this: its durable-write scenario is a pure sorted append, which is the case packing trivially wins. The cost only appears when a second transaction inserts BETWEEN keys of pages the first one packed, so it is measured here, deterministically, instead of in a benchmark window.</para>
		/// <para>Both numbers are exact block counts, not timings, so this is a machine-independent decision record for ENG-4.</para>
		/// </remarks>
		[Test]
		public void Append_Then_Update_Bounds_The_Cost_Of_Packing()
		{
			// phase 1 packs pages with a sorted run of even keys; phase 2 is a SEPARATE generation inserting the
			// odd keys between them, so every page it touches is full, owned by an older generation, and splits
			static (uint AfterSeed, uint AfterUpdate, int Splits) Run(bool avoidAppendSplits)
			{
				const int N = 20_000;
				var geometry = FdbLiteGeometry.Default;
				using var pager = new FdbLiteHeapPager(geometry);
				var engine = FdbLiteEngine.Create(pager);
				engine.AvoidSequentialAppendSplits = avoidAppendSplits;

				var seed = engine.BeginWrite();
				for (int i = 0; i < N; i += 2)
				{
					seed.Insert(SequentialKey(i), "0123456789ABCDEF"u8);
				}
				engine.Commit(seed, 1);
				uint afterSeed = engine.Durable.AllocationFrontier;

				var update = engine.BeginWrite();
				for (int i = 1; i < N; i += 2)
				{
					update.Insert(SequentialKey(i), "0123456789ABCDEF"u8);
				}
				int splits = update.PageSplits;
				engine.Commit(update, 2);

				Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) N), $"[avoid={avoidAppendSplits}]");
				Assert.That(ScanForward(pager, engine.Durable.RootPageId), Has.Count.EqualTo(N), $"[avoid={avoidAppendSplits}]");
				return (afterSeed, engine.Durable.AllocationFrontier, splits);
			}

			var on = Run(avoidAppendSplits: true);
			var off = Run(avoidAppendSplits: false);

			Log($"append-then-update, avoid=ON  {on.AfterSeed:N0} blocks seeded -> {on.AfterUpdate:N0} after update ({on.Splits:N0} splits)");
			Log($"append-then-update, avoid=OFF {off.AfterSeed:N0} blocks seeded -> {off.AfterUpdate:N0} after update ({off.Splits:N0} splits)");

			Assert.That(on.AfterSeed, Is.LessThan(off.AfterSeed), "packing must still win the seed");
			Assert.That(on.AfterUpdate, Is.LessThanOrEqualTo(off.AfterUpdate), "packing must not end up costing MORE blocks than balanced splits once the update pass has re-split the packed pages: this is the assertion that falsifies ENG-4 if it ever fires");
		}

		[Test]
		public void Measure_Write_Amplification_Per_Geometry()
		{
			var report = new StringBuilder();
			report.AppendLine("| geometry | seed disk (B/key) | seed cpu (B/key) | tiny commit disk (B) | extent 100KB disk (B) | allocated (MiB) | logical (MiB) |");
			report.AppendLine("|---|---|---|---|---|---|---|");

			foreach (var (name, geometry) in new[]
			{
				("u16K", FdbLiteGeometry.Uniform(14)),
				("u32K", FdbLiteGeometry.Uniform(15)),
				("u64K", FdbLiteGeometry.Uniform(16)),
				("b16K/p64K", FdbLiteGeometry.Hypothesis),
			})
			{
				var counting = new CountingPager { Inner = new FdbLiteHeapPager(geometry, regionSizeInBytes: 16 << 20) };
				using var cleanup = counting;
				var engine = FdbLiteEngine.Create(counting);
				var rnd = new Random(42);

				// bulk seed: 50k chunk-class entries in one commit
				counting.ResetCounters();
				var writer = engine.BeginWrite();
				var value = new byte[1_000];
				rnd.NextBytes(value);
				long logicalSeed = 0;
				for (int i = 0; i < 50_000; i++)
				{
					var key = TuPack.EncodeKey("D", rnd.Next(), i);
					writer.Insert(key.Span, value);
					logicalSeed += key.Count + value.Length;
				}
				engine.Commit(writer, 10);
				long seedPerKeyCpu = counting.BytesWritten / 50_000;
				long seedPerKeyDisk = counting.UniqueBytesWritten / 50_000;
				Log($"[{name}] after seed: frontier={engine.Durable.AllocationFrontier:N0} blocks ({(double) engine.Durable.AllocationFrontier * geometry.BlockSize / (1 << 20):N1} MiB), dirty={counting.UniqueBytesWritten / (1 << 20):N0} MiB, pendingReclaim={engine.GetStats().PendingReclaimBlocks:N0} blocks");

				// tiny commits: single small key/value per commit, averaged
				counting.ResetCounters();
				for (int i = 0; i < 200; i++)
				{
					writer = engine.BeginWrite();
					writer.Insert(TuPack.EncodeKey("T", i).Span, "0123456789ABCDEF"u8);
					engine.Commit(writer, (ulong) (100 + i));
				}
				long tinyPerCommit = counting.UniqueBytesWritten / 200;

				// extent commits: one 100,000 B value per commit, averaged
				counting.ResetCounters();
				var big = new byte[100_000];
				rnd.NextBytes(big);
				for (int i = 0; i < 50; i++)
				{
					writer = engine.BeginWrite();
					writer.Insert(TuPack.EncodeKey("X", i).Span, big);
					engine.Commit(writer, (ulong) (1000 + i));
				}
				long extentPerCommit = counting.UniqueBytesWritten / 50;

				double allocatedMiB = (double) engine.Durable.AllocationFrontier * geometry.BlockSize / (1 << 20);
				double logicalMiB = (double) (logicalSeed + (200 * 24) + (50 * 100_008)) / (1 << 20);
				report.AppendLine($"| {name} | {seedPerKeyDisk:N0} | {seedPerKeyCpu:N0} | {tinyPerCommit:N0} | {extentPerCommit:N0} | {allocatedMiB:N1} | {logicalMiB:N1} |");

				// sanity: the engine stays readable and exact
				Assert.That(engine.Durable.KeyCount, Is.EqualTo(50_000 + 200 + 50), $"[{name}]");
			}

			Log(report.ToString());
		}

	}

}
