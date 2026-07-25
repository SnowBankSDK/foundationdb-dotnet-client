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

	/// <summary>Page-level prefix stripping: that it HAPPENS, and that nothing observable changes when it does.</summary>
	/// <remarks>A stripping implementation that silently never strips would pass every behavioural test in the suite, because not stripping is exactly the old behaviour. So these assert the mechanism fired, not only that the results are still correct.</remarks>
	[TestFixture]
	public class FdbLitePrefixFacts : SimpleTest
	{

		/// <summary>Keys that all begin with the same long run, which is what a directory-subspace prefix looks like to the engine.</summary>
		private static byte[] SharedPrefixKey(int i)
		{
			var key = new byte[24];
			"\xFE/tenant/42/idx/"u8.CopyTo(key);
			BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(16), i);
			return key;
		}

		[Test]
		public void Test_Leaf_Strips_The_Prefix_Its_Keys_Share()
		{
			// Enough pairs to fill and SPLIT leaves. The prefix is computed when a page is BUILT, and a page that
			// fills purely by splicing is never rebuilt, so a small tree legitimately carries no prefix at all.
			// A 200-key version of this test reports 0 and is right to.
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var writer = engine.BeginWrite();
			for (int i = 0; i < 20_000; i++)
			{
				writer.Insert(SharedPrefixKey(i), "v"u8);
			}
			engine.Commit(writer, 1);

			// walked through the public header accessors, because the page layout itself is internal to the engine
			int leaves = 0, stripped = 0, best = 0;
			var geometry = engine.Pager.Geometry;
			for (uint id = 1; id + (uint) geometry.BlocksPerPage <= engine.Pager.BlockCount; id += (uint) geometry.BlocksPerPage)
			{
				var page = engine.Pager.ReadBlocks(id, geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(page) != FdbLitePageType.Leaf) continue;
				++leaves;
				int p = FdbLitePageHeader.GetPrefixLength(page);
				if (p > 0) { ++stripped; best = Math.Max(best, p); }
			}

			Log($"# leaves={leaves} stripped={stripped} longest prefix={best} bytes");
			Assert.That(leaves, Is.GreaterThan(1), "20k pairs must occupy more than one leaf, or this proves nothing about page building");
			Assert.That(leaves, Is.GreaterThan(1), "20k pairs must occupy more than one leaf, or this proves nothing about page building");
			Assert.That(stripped, Is.GreaterThan(0), "keys sharing a 16-byte run must leave SOME leaf stripping a prefix; zero means the mechanism never fires");
			Assert.That(best, Is.GreaterThanOrEqualTo(16), "the shared run is 16 bytes, so a stripped page should reach at least that");
		}

		[Test]
		public void Test_Stripping_Costs_One_Rebuild_Per_Page_Not_Per_Insert()
		{
			// The fill-time rebuild's obvious hazard is degenerating into a rebuild per insert once a page is
			// near-full and every splice fails. It cannot: a rebuild strips to exactly the longest shared prefix, so
			// the next attempt finds nothing to gain and declines. This asserts that rather than trusting it.
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var writer = engine.BeginWrite();
			const int COUNT = 20_000;
			for (int i = 0; i < COUNT; i++)
			{
				writer.Insert(SharedPrefixKey(i), "v"u8);
			}
			int stripped = writer.PagesStripped;
			int pages = writer.PagesAppended;
			engine.Commit(writer, 1);

			Log($"# keys={COUNT} pagesStripped={stripped} pagesAppended={pages}");
			Assert.That(stripped, Is.GreaterThan(0), "some page must have filled and been stripped, or the mechanism never ran");
			Assert.That(stripped, Is.LessThanOrEqualTo(pages + 2), "stripping must track PAGES that filled, not keys inserted: a count near the key count means it rebuilt per insert");
		}

		[Test]
		public void Test_Stripping_Is_Invisible_To_A_Reader()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var expected = new List<byte[]>();
			var writer = engine.BeginWrite();
			for (int i = 0; i < 200; i++)
			{
				var k = SharedPrefixKey(i);
				expected.Add(k);
				writer.Insert(k, "v"u8);
			}
			engine.Commit(writer, 1);

			var pin = engine.BeginRead();
			try
			{
				var cursor = new FdbLiteTreeCursor(engine.Pager, pin.RootPageId);

				// enumeration hands back WHOLE keys even though the page stores suffixes
				var seen = new List<byte[]>();
				if (cursor.SeekFirst())
				{
					do { seen.Add(cursor.CurrentKey.ToArray()); } while (cursor.MoveNext());
				}
				Assert.That(seen.Count, Is.EqualTo(expected.Count));
				for (int i = 0; i < expected.Count; i++)
				{
					Assert.That(seen[i], Is.EqualTo(expected[i]), $"key {i} must read back whole");
				}

				// and a seek for a whole key still lands on it
				Assert.That(cursor.SeekFloor(expected[137], orEqual: true), Is.True);
				Assert.That(cursor.CurrentKey.SequenceEqual(expected[137]), Is.True, "seek must resolve a whole key against stripped storage");

				// a probe that diverges INSIDE the shared prefix sorts outside the page entirely: the four-case
				// boundary analysis, which is the part of prefix search that is easy to get subtly wrong
				var below = new byte[24];
				"\xFE/tenant/41/idx/"u8.CopyTo(below);
				Assert.That(cursor.SeekFloor(below, orEqual: true), Is.False, "a key below the page prefix has no floor here");

				var above = new byte[24];
				"\xFE/tenant/43/idx/"u8.CopyTo(above);
				Assert.That(cursor.SeekFloor(above, orEqual: true), Is.True, "a key above the page prefix floors on the last key");
				Assert.That(cursor.CurrentKey.SequenceEqual(expected[^1]), Is.True);
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

	}

}
