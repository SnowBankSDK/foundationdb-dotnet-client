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

	/// <summary>Keys whose shared run ends INSIDE a multi-byte field, so the page prefix cuts a counter in half.</summary>
	/// <remarks>
	/// <para>Index keys are a long structured run followed by a number, so a page of them shares every byte except the last one or two of that number, and the prefix boundary lands in the middle of the field rather than on a component boundary. Where the number carries into its next byte the shared run SHORTENS, so consecutive pages have different prefix lengths and one page straddles the carry.</para>
	/// <para>That case is invisible to a suite built on short, well-separated keys: every page there strips the same amount and no page straddles anything. It was found by a 20-million-key file-backed measurement, where 57 probes in 200,000 came back as a key three bytes shorter than the one asked for, all of them at a 65,536 carry.</para>
	/// </remarks>
	[TestFixture]
	public class FdbLitePrefixBoundaryFacts : SimpleTest
	{

		private const int KeySize = 36;

		private static byte[] MakeKey(int i)
		{
			var key = new byte[KeySize];
			"/acme/tenant-0042/orders/index"u8.CopyTo(key);
			BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(32), i);
			return key;
		}

		[Test]
		public void Test_Every_Key_Reads_Back_Across_A_Carry_In_The_Key_Suffix()
		{
			// spans two carries of the low 16 bits, so some pages sit inside one block (long shared run, short
			// suffix) and some straddle a carry (shorter shared run, longer suffix)
			// wide enough that the tree is three levels deep: at two levels the root is the only internal page and
			// a separator bug has nowhere to hide
			const int FIRST = 0x0000_F000, LAST = 0x0020_0000;

			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));
			var value = new byte[8];
			ulong version = 1;
			for (int start = FIRST; start < LAST; start += 20_000)
			{
				var w = engine.BeginWrite();
				for (int i = start, end = Math.Min(start + 20_000, LAST); i < end; i++)
				{
					w.Insert(MakeKey(i), value);
				}
				engine.Commit(w, version++);
			}

			var pin = engine.BeginRead();
			try
			{
				// the page prefixes must actually VARY, or this test is not standing where it thinks it is
				var lengths = new HashSet<int>();
				var geometry = engine.Pager.Geometry;
				for (uint id = 1; id + (uint) geometry.BlocksPerPage <= engine.Pager.BlockCount; id += (uint) geometry.BlocksPerPage)
				{
					var page = engine.Pager.ReadBlocks(id, geometry.BlocksPerPage);
					if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
					{
						lengths.Add(FdbLitePageHeader.GetPrefixLength(page));
					}
				}
				Log($"# leaf prefix lengths present: {string.Join(", ", lengths.Order())}");
				Assert.That(lengths.Count, Is.GreaterThan(1), "the keys must produce pages with DIFFERENT prefix lengths, otherwise the straddling case is not covered");

				var cursor = new FdbLiteTreeCursor(engine.Pager, pin.RootPageId);
				var misses = new List<string>();
				for (int i = FIRST; i < LAST; i++)
				{
					var key = MakeKey(i);
					if (!cursor.SeekFloor(key, orEqual: true))
					{
						if (misses.Count < 8) misses.Add($"0x{i:X8}: seek returned false");
						continue;
					}
					var got = cursor.CurrentKey;
					if (!got.SequenceEqual(key) && misses.Count < 8)
					{
						misses.Add($"0x{i:X8}: got {Convert.ToHexString(got)} ({got.Length} B, wanted {key.Length} B)");
					}
				}

				foreach (var m in misses) Log($"# MISS {m}");
				Assert.That(misses, Is.Empty, "every key must read back as itself");
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

	}

}
