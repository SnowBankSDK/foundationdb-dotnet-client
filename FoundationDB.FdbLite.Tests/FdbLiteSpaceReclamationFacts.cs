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

	/// <summary>Tests of the space-reclamation train: the aggregate block, the volatility counter, pre-commit consolidation, and the background vacuum.</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteSpaceReclamationFacts : SimpleTest
	{

		private static FdbLiteEngine CreateHeapEngine() => FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

		private static byte[] Key(int i) => System.Text.Encoding.ASCII.GetBytes($"key-{i:D6}");

		private static byte[] Value(int i, int length)
		{
			var v = new byte[length];
			new Random(i).NextBytes(v);
			return v;
		}

		/// <summary>Reads a committed page image through the engine's pager.</summary>
		private static ReadOnlySpan<byte> ReadPage(FdbLiteEngine engine, uint pageId)
			=> engine.Pager.ReadBlocks(pageId, engine.Pager.Geometry.BlocksPerPage);

		private static byte[] Key64(long i)
		{
			var key = new byte[8];
			System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		/// <summary>Leaves of the current durable generation in key order: page id, numeric key range, and the volatility episode count.</summary>
		private static List<(uint Id, long FirstKey, long LastKey, byte Episodes)> ScanLeafEpisodes(FdbLiteEngine engine)
		{
			var result = new List<(uint, long, long, byte)>();
			foreach (var group in FdbLiteLeafAnalysis.Snapshot(engine.Pager, engine.Durable.RootPageId).Groups)
			{
				foreach (var leaf in group)
				{
					result.Add((leaf.PageId, leaf.FirstKey, leaf.LastKey, FdbLitePageHeader.GetVolatilityEpisodes(ReadPage(engine, leaf.PageId))));
				}
			}
			return result;
		}

		private static long TotalEpisodes(FdbLiteEngine engine) => ScanLeafEpisodes(engine).Sum(l => (long) l.Episodes);

		private static byte EpisodesOfLeafCovering(FdbLiteEngine engine, long key)
			=> ScanLeafEpisodes(engine).Single(l => l.FirstKey <= key && key <= l.LastKey).Episodes;

		[Test]
		public void Test_Volatility_Episodes_Follow_The_Ratified_Event_Set()
		{
			using var engine = CreateHeapEngine();
			ulong version = 1;

			// sequential fill: a log page being built is FILLING, not mutating, and must reach its packed
			// state at count zero - this is the write-once shape the whole posture exists to protect
			const long Stride = 16;
			const int N = 3_000;
			var writer = engine.BeginWrite();
			for (long i = 0; i < N; i++)
			{
				writer.Insert(Key64(i * Stride), Value((int) i, 16));
			}
			engine.Commit(writer, version++);

			var leaves = ScanLeafEpisodes(engine);
			Assert.That(leaves.Count, Is.GreaterThanOrEqualTo(3), "the seed must span several leaves or the local/global distinction below is untestable");
			Assert.That(leaves.All(l => l.Episodes == 0), Is.True, "an append-built tree reads zero episodes everywhere");

			// deletes count, once per generation however many land on the page
			writer = engine.BeginWrite();
			Assert.That(writer.Remove(Key64(10 * Stride)), Is.True);
			Assert.That(writer.Remove(Key64(11 * Stride)), Is.True);
			Assert.That(writer.Remove(Key64(12 * Stride)), Is.True);
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(1), "three deletes in one generation are ONE episode");

			// a second mutating generation is a second episode
			writer = engine.BeginWrite();
			Assert.That(writer.Remove(Key64(13 * Stride)), Is.True);
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(2));

			// an in-place value mutation counts (same-length replace, the copy-verbatim first-touch path)
			writer = engine.BeginWrite();
			writer.Insert(Key64(20 * Stride), Value(9999, 16));
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(3), "a replace is a mutation episode");

			// an interior insert counts: the key lands strictly below the receiving leaf's maximum
			writer = engine.BeginWrite();
			writer.Insert(Key64((100 * Stride) + 1), Value(7, 16));
			engine.Commit(writer, version++);
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(4), "an interior insert is a mutation episode");

			// append-edge growth at the TREE's right edge does not count
			long before = TotalEpisodes(engine);
			writer = engine.BeginWrite();
			writer.Insert(Key64(N * Stride), Value(8, 16));
			engine.Commit(writer, version++);
			Assert.That(TotalEpisodes(engine), Is.EqualTo(before), "growing the rightmost leaf is filling, not mutating");

			// THE LOAD-BEARING LOCAL TEST: a key at the right edge of an INTERIOR leaf is that leaf's own
			// append edge, even though it sits far below the tree's global maximum. A store with many
			// append-shaped subspaces has many such edges at once, and a global running-max test would
			// brand every one of them volatile.
			var leftmost = ScanLeafEpisodes(engine)[0];
			long edgeKey = leftmost.LastKey + 1; // above everything in the leaf, below the next leaf's first key
			before = TotalEpisodes(engine);
			writer = engine.BeginWrite();
			writer.Insert(Key64(edgeKey), Value(9, 16));
			engine.Commit(writer, version++);
			Assert.That(TotalEpisodes(engine), Is.EqualTo(before), "a leaf-local append edge does not count, wherever the leaf sits in the tree");

			// the counter saturates instead of wrapping
			for (int g = 0; g < 260; g++)
			{
				writer = engine.BeginWrite();
				if ((g & 1) == 0)
				{
					Assert.That(writer.Remove(Key64(30 * Stride)), Is.True);
				}
				else
				{
					writer.Insert(Key64(30 * Stride), Value(g, 16)); // re-inserting interior: an episode either way
				}
				engine.Commit(writer, version++);
			}
			Assert.That(EpisodesOfLeafCovering(engine, 0), Is.EqualTo(255), "the u8 saturates, never wraps");

			// split parts inherit the source page's history: a rebuild of ONE page is that page's own
			// continued life, not a repack into a new one
			writer = engine.BeginWrite();
			for (long j = 40; j < 140; j++)
			{
				for (long s = 1; s < 16; s += 2)
				{
					writer.Insert(Key64((j * Stride) + s), Value((int) (j * 100 + s), 16));
				}
			}
			engine.Commit(writer, version++);
			var saturatedRegion = ScanLeafEpisodes(engine).Where(l => l.FirstKey < 140 * Stride).ToList();
			Assert.That(saturatedRegion.Count, Is.GreaterThanOrEqualTo(2), "the pumped region must have split");
			Assert.That(saturatedRegion.All(l => l.Episodes == 255), Is.True, "every part of a split volatile page carries its history");
		}

		[Test]
		public void Test_Root_Aggregates_Are_Exact_Against_The_Model()
		{
			// The root page's aggregate block claims the tree-wide totals in O(1); the MODEL is the oracle
			// (nothing derived from the pages under test), and the walk-based statistics cross-check the leaf
			// counts. Checked after EVERY commit: exactness across generations is the dirty-chain invariant's
			// whole claim, and a clean subtree whose stored sums drifted is precisely what one final check
			// at the end would miss attributing.
			using var engine = CreateHeapEngine();
			var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
			var rnd = new Random(20260730);

			void CheckAggregates(string phase)
			{
				var agg = engine.GetTreeAggregates();
				long keyBytes = model.Keys.Sum(k => (long) k.Length);
				long valueBytes = model.Values.Sum(v => v.LongLength);
				Assert.That(agg.EntryCount, Is.EqualTo((ulong) model.Count), $"{phase}: entry count");
				Assert.That(agg.LogicalKeyBytes, Is.EqualTo((ulong) keyBytes), $"{phase}: logical key bytes");
				Assert.That(agg.LogicalValueBytes, Is.EqualTo((ulong) valueBytes), $"{phase}: logical value bytes");

				var stats = engine.MeasureTreeStatistics();
				Assert.That(agg.LeafCount, Is.EqualTo((uint) stats.LeafPages), $"{phase}: leaf count");
				Assert.That(agg.LeafLiveBytes, Is.EqualTo((ulong) stats.LeafLiveBytes), $"{phase}: leaf live bytes");
			}

			void Insert(FdbLiteTreeWriter w, string key, byte[] value)
			{
				w.Insert(System.Text.Encoding.ASCII.GetBytes(key), value);
				model[key] = value;
			}

			// generation 1: enough sorted+random keys to build a multi-level tree, plus one extent value
			ulong version = 1;
			var writer = engine.BeginWrite();
			for (int i = 0; i < 6_000; i++)
			{
				Insert(writer, $"key-{i:D6}", Value(i, rnd.Next(0, 64)));
			}
			Insert(writer, "big-blob", Value(777, 100_000)); // out-of-line extent: logical bytes count its CONTENT
			engine.Commit(writer, version++);
			CheckAggregates("after bulk load");

			// generation 2: shrinks, grows, same-length replaces (the in-place paths)
			writer = engine.BeginWrite();
			for (int i = 0; i < 6_000; i += 3)
			{
				Insert(writer, $"key-{i:D6}", Value(i + 9000, rnd.Next(3) switch { 0 => 4, 1 => 32, _ => 150 }));
			}
			engine.Commit(writer, version++);
			CheckAggregates("after replace churn");

			// generation 3: deletes (point + range), including the extent
			writer = engine.BeginWrite();
			for (int i = 1; i < 6_000; i += 5)
			{
				string k = $"key-{i:D6}";
				Assert.That(writer.Remove(System.Text.Encoding.ASCII.GetBytes(k)), Is.True);
				model.Remove(k);
			}
			int removed = writer.RemoveRange("key-002000"u8, "key-002500"u8);
			var dead = model.Keys.Where(k => string.CompareOrdinal(k, "key-002000") >= 0 && string.CompareOrdinal(k, "key-002500") < 0).ToList();
			Assert.That(removed, Is.EqualTo(dead.Count));
			foreach (var k in dead) { model.Remove(k); }
			Assert.That(writer.Remove("big-blob"u8), Is.True);
			model.Remove("big-blob");
			engine.Commit(writer, version++);
			CheckAggregates("after deletes");

			// generation 4: a small touch, so clean subtrees from earlier generations carry their sums across
			writer = engine.BeginWrite();
			Insert(writer, "zz-last", Value(1, 10));
			engine.Commit(writer, version++);
			CheckAggregates("after a one-key generation");

			// and the structural audit stays silent, which now includes per-page aggregate validation
			var pin = engine.BeginRead();
			try
			{
				Assert.That(FdbLiteTreeAudit.Check(engine.Pager, pin.RootPageId), Is.Empty, "audit (incl. aggregate recount) must be silent");
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		[Test]
		public void Test_Seal_Restamps_The_Generation_Of_Verbatim_Copies()
		{
			// The copy-verbatim replace path duplicates a committed page image and mutates one value in the
			// copy, so the copy carries the SOURCE generation's stamp. The stamp is diagnostic (an inspector
			// uses it to detect a page reused under its feet), and a page published by generation N stamped
			// N-1 sends any such diagnosis to the wrong generation. Seal is the one point every dirty image
			// passes through exactly once, so the stamp is corrected there.
			using var engine = CreateHeapEngine();

			var writer = engine.BeginWrite();
			for (int i = 0; i < 3; i++)
			{
				writer.Insert(Key(i), Value(i, 32));
			}
			engine.Commit(writer, databaseVersion: 1);

			// same-length replace of a committed value: the first touch of the page takes the copy-verbatim path
			writer = engine.BeginWrite();
			ulong writeGeneration = writer.Generation;
			writer.Insert(Key(1), Value(1000, 32));
			Assert.That(writer.CellsOverwritten, Is.EqualTo(1), "the replace must take the in-place overwrite path (copy-verbatim on first touch), or this test no longer exercises the stamp");
			engine.Commit(writer, databaseVersion: 2);

			var leaf = ReadPage(engine, engine.Durable.RootPageId);
			Assert.That(FdbLitePageHeader.GetPageType(leaf), Is.EqualTo(FdbLitePageType.Leaf), "a 3-key store is a single-leaf tree");
			Assert.That(FdbLitePageHeader.GetGeneration(leaf), Is.EqualTo(writeGeneration), "a page published by a generation must carry that generation's stamp");
		}

	}

}
