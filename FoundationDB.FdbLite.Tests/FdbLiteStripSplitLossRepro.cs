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

	/// <summary>RED repro: the split predictor double-counts the prefix region, and <c>TryStripAndRetry</c> treats an unexpected split of its rebuild as a no-op, orphaning half the leaf.</summary>
	/// <remarks>
	/// <para>Path to the defect: a leaf already stripped at prefix P loses its extreme cells to in-place deletes, so its remaining keys now share MORE than P. The next insert that overflows the free gap triggers the strip-and-retry rebuild with the longer prefix. The predictor in <c>WriteCells</c> compares a total that already contains the prefix region against a capacity that subtracts it AGAIN, so a run within ~region bytes of full is planned as a 2-way split even though it fits one page. The strip rebuild then splits in place: part 0 overwrites the leaf with half the cells, the right sibling is written but returned to a caller that DISCARDS it, and every key on it silently leaves the tree.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteStripSplitLossRepro : SimpleTest
	{

		private const int PrefixLength = 2_000;

		/// <summary>Key = 2000 shared bytes, then [head, i:24 big-endian]: 2004 bytes total</summary>
		private static byte[] Key(byte head, int i)
		{
			var key = new byte[PrefixLength + 4];
			key.AsSpan(0, PrefixLength).Fill(0x50);
			key[PrefixLength] = head;
			key[PrefixLength + 1] = (byte) (i >> 16);
			key[PrefixLength + 2] = (byte) (i >> 8);
			key[PrefixLength + 3] = (byte) i;
			return key;
		}

		[Test]
		public void Strip_Rebuild_Split_Must_Not_Lose_Keys()
		{
			var geometry = FdbLiteGeometry.Uniform(14); // 16 KiB pages: the arithmetic below is tuned to it
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);

			var writer = engine.BeginWrite();
			var expected = new SortedSet<byte[]>(Comparer<byte[]>.Create(static (a, b) => a.AsSpan().SequenceCompareTo(b)));
			void Insert(byte[] key, byte[] value)
			{
				writer.Insert(key, value);
				expected.Add(key);
			}

			var small = "0123456789ABCDEF"u8.ToArray(); // 16 B

			// Phase 1: two extreme cells pin the page's shared prefix at exactly 2000, middles fill until the
			// first splice failure strips the (single, root) leaf to that prefix.
			Insert(Key(0x00, 0), small);
			Insert(Key(0x20, 0), small);
			for (int i = 0; i < 7; i++)
			{
				Insert(Key(0x10, i), small);
			}
			Assert.That(writer.PagesStripped, Is.EqualTo(1), "setup: the root leaf must have stripped its 2000-byte prefix");
			Assert.That(writer.PageSplits, Is.Zero, "setup: still a single root leaf");

			// Phase 2: fill the stripped leaf with middle keys (27 B per cell) until the free gap is under the
			// killer cell's size but still above one middle cell, all through in-place splices.
			for (int i = 7; i < 477; i++)
			{
				Insert(Key(0x10, i), small);
			}
			Assert.That(writer.PageSplits, Is.Zero, "setup: the fill must stay inside the single leaf");
			Assert.That(writer.PagesStripped, Is.EqualTo(1), "setup: no second strip during the fill");

			// Phase 3: delete the extremes in place; the remaining keys now share 2002 bytes while the page
			// prefix is still 2000, which re-arms the strip.
			Assert.That(writer.Remove(Key(0x00, 0)), Is.True);
			Assert.That(writer.Remove(Key(0x20, 0)), Is.True);
			expected.RemoveWhere(k => k[PrefixLength] != 0x10);
			Assert.That(writer.CellsRemovedInPlace, Is.EqualTo(2), "setup: both extremes must go through the in-place delete");

			// Phase 4: a key sharing the new 2002-byte prefix, too big for the gap: splice fails, the strip
			// rebuild runs with the longer prefix, and the predictor plans it as a split even though it fits.
			Insert(Key(0x10, 0xFFFF), new byte[500]);

			engine.Commit(writer, 1);

			// every inserted key must still be readable
			var seen = new List<byte[]>();
			var cursor = new FdbLiteTreeCursor(pager, engine.Durable.RootPageId);
			if (cursor.SeekFirst())
			{
				do { seen.Add(cursor.CurrentKey.ToArray()); } while (cursor.MoveNext());
			}

			Log($"inserted {expected.Count:N0} keys, scan sees {seen.Count:N0}; splits={writer.PageSplits}, stripped={writer.PagesStripped}, keyCount={engine.Durable.KeyCount:N0}");
			Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) expected.Count), "the committed key count must match what was inserted");
			Assert.That(seen.Count, Is.EqualTo(expected.Count), "no key may silently leave the tree");
		}

	}

}
