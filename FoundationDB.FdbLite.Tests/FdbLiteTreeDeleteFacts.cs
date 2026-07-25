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

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteTreeDeleteFacts : SimpleTest
	{

		private sealed class ByteArrayComparer : IComparer<byte[]>
		{
			public static readonly ByteArrayComparer Instance = new();
			public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
		}

		private static (FdbLiteHeapPager Pager, FdbLiteBlockAllocator Allocator) CreateStore(FdbLiteGeometry geometry)
		{
			var pager = new FdbLiteHeapPager(geometry);
			var allocator = new FdbLiteBlockAllocator(pager, new FdbLiteFreeSpaceMap(), frontier: 3);
			return (pager, allocator);
		}

		/// <summary>Root of the tree as it is visible THROUGH THE PAGER: the writer buffers its modified pages, so they must be flushed before any reader that goes straight to the pager (the engine does this in Commit).</summary>
		private static uint Published(FdbLiteTreeWriter writer)
		{
			writer.FlushDirtyPages();
			return writer.Root;
		}

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

		[Test]
		public void Test_Remove_Small()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			var (pager, allocator) = CreateStore(geometry);
			using var cleanup = pager;
			var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);

			writer.Insert("a"u8, "1"u8);
			writer.Insert("b"u8, "2"u8);
			writer.Insert("c"u8, "3"u8);

			Assert.That(writer.Remove("b"u8), Is.True);
			Assert.That(writer.Remove("b"u8), Is.False, "second removal misses");
			Assert.That(writer.Remove("zz"u8), Is.False, "unknown key misses");

			Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "b"u8, out _), Is.False);
			Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "a"u8, out var v), Is.True);
			Assert.That(v.SequenceEqual("1"u8), Is.True);
			Assert.That(ScanForward(pager, Published(writer)), Has.Count.EqualTo(2));

			// removing everything empties the tree completely
			Assert.That(writer.Remove("a"u8), Is.True);
			Assert.That(writer.Remove("c"u8), Is.True);
			Assert.That(Published(writer), Is.Zero, "empty tree collapses to root 0");

			// and it comes back to life on the next insert
			writer.Insert("phoenix"u8, "rises"u8);
			Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "phoenix"u8, out v), Is.True);
			Assert.That(v.SequenceEqual("rises"u8), Is.True);
		}

		[Test]
		public void Test_Reference_Model_Random_Insert_Delete_Fuzz()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var (pager, allocator) = CreateStore(geometry);
				using var cleanup = pager;
				var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
				var model = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Instance);

				var rnd = new Random(31337);
				for (int step = 0; step < 6_000; step++)
				{
					int op = rnd.Next(10);
					if (op < 6 || model.Count == 0)
					{ // insert or update
						var key = new byte[rnd.Next(1, 40)];
						rnd.NextBytes(key);
						var value = new byte[rnd.Next(0, 100)];
						rnd.NextBytes(value);
						writer.Insert(key, value);
						model[key] = value;
					}
					else if (op < 9)
					{ // remove an existing key (and occasionally a missing one)
						var victim = model.Keys.ElementAt(rnd.Next(model.Count));
						Assert.That(writer.Remove(victim), Is.True);
						model.Remove(victim);

						var missing = new byte[] { 0xFF, 0xFF, 0xFF, (byte) rnd.Next(256) };
						Assert.That(writer.Remove(missing), Is.EqualTo(model.ContainsKey(missing)));
					}
					else
					{ // remove a small random range
						var begin = new byte[rnd.Next(1, 6)];
						rnd.NextBytes(begin);
						var end = new byte[rnd.Next(1, 6)];
						rnd.NextBytes(end);
						if (ByteArrayComparer.Instance.Compare(begin, end) > 0)
						{
							(begin, end) = (end, begin);
						}
						int expected = model.Keys.Count(k => ByteArrayComparer.Instance.Compare(k, begin) >= 0 && ByteArrayComparer.Instance.Compare(k, end) < 0);
						int removed = writer.RemoveRange(begin, end);
						Assert.That(removed, Is.EqualTo(expected), $"[{geometry}] range delete count");
						foreach (var k in model.Keys.Where(k => ByteArrayComparer.Instance.Compare(k, begin) >= 0 && ByteArrayComparer.Instance.Compare(k, end) < 0).ToList())
						{
							model.Remove(k);
						}
					}
				}

				// final state matches the model exactly
				Assert.That(ScanForward(pager, Published(writer)), Is.EqualTo(model.Keys.ToList()), $"[{geometry}] final scan");
				foreach (var kv in model)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), kv.Key, out var v), Is.True);
					if (!v.SequenceEqual(kv.Value))
					{
						Assert.Fail($"[{geometry}] value mismatch after fuzz");
					}
				}
			}
		}

		[Test]
		public void Test_RemoveRange_Whole_Keyspace()
		{
			var geometry = FdbLiteGeometry.Uniform(14);
			var (pager, allocator) = CreateStore(geometry);
			using var cleanup = pager;
			var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);

			for (int i = 0; i < 2_000; i++)
			{
				var key = new byte[8];
				BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) i);
				writer.Insert(key, "v"u8);
			}

			int removed = writer.RemoveRange([ 0x00 ], [ 0xFF ]);
			Assert.That(removed, Is.EqualTo(2_000));
			Assert.That(Published(writer), Is.Zero, "clearing everything collapses the tree");
			Assert.That(allocator.FreeSpace.PendingBlockCount + allocator.FreeSpace.ReusableBlockCount, Is.GreaterThan(0), "the dropped pages went back to the free machinery");
		}

		[Test]
		public void Test_Extent_Values_Roundtrip_And_Lifecycle()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var (pager, allocator) = CreateStore(geometry);
				using var cleanup = pager;
				var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
				var rnd = new Random(2024);

				// values from just-inline to the fdb maximum (100,000 B), interleaved with small ones
				var model = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Instance);
				for (int i = 0; i < 40; i++)
				{
					var key = new byte[8];
					BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) i);
					int size = (i % 4) switch
					{
						0 => rnd.Next(0, 64),
						1 => geometry.MaxInlineValueLength,
						2 => geometry.MaxInlineValueLength + 1,
						_ => rnd.Next(geometry.MaxInlineValueLength + 1, 100_001),
					};
					var value = new byte[size];
					rnd.NextBytes(value);
					writer.Insert(key, value);
					model[key] = value;
				}

				foreach (var kv in model)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), kv.Key, out var v), Is.True, $"[{geometry}] extent key missing");
					if (!v.SequenceEqual(kv.Value))
					{
						Assert.Fail($"[{geometry}] value mismatch for size {kv.Value.Length}");
					}
				}

				// the cursor resolves extents as single spans too
				var cursor = new FdbLiteTreeCursor(pager, Published(writer));
				Assert.That(cursor.SeekFirst(), Is.True);
				int seen = 0;
				do
				{
					var key = cursor.CurrentKey.ToArray();
					Assert.That(cursor.CurrentValue.SequenceEqual(model[key]), Is.True, $"[{geometry}] cursor extent value");
					seen++;
				}
				while (cursor.MoveNext());
				Assert.That(seen, Is.EqualTo(model.Count));

				// replacing an extent value with a small inline one releases the extent's blocks
				long pendingBefore = allocator.FreeSpace.PendingBlockCount + allocator.FreeSpace.ReusableBlockCount;
				var bigKey = model.Keys.First(k => model[k].Length > geometry.MaxInlineValueLength);
				writer.Insert(bigKey, "tiny"u8);
				long pendingAfter = allocator.FreeSpace.PendingBlockCount + allocator.FreeSpace.ReusableBlockCount;
				Assert.That(pendingAfter, Is.GreaterThan(pendingBefore), $"[{geometry}] replaced extent must be released");
				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), bigKey, out var tiny), Is.True);
				Assert.That(tiny.SequenceEqual("tiny"u8), Is.True);

				// deleting an extent key releases its blocks as well
				var bigKey2 = model.Keys.Last(k => model[k].Length > geometry.MaxInlineValueLength);
				pendingBefore = allocator.FreeSpace.PendingBlockCount + allocator.FreeSpace.ReusableBlockCount;
				Assert.That(writer.Remove(bigKey2), Is.True);
				pendingAfter = allocator.FreeSpace.PendingBlockCount + allocator.FreeSpace.ReusableBlockCount;
				Assert.That(pendingAfter, Is.GreaterThan(pendingBefore), $"[{geometry}] deleted extent must be released");
			}
		}

		[Test]
		public void Test_Extents_Survive_Leaf_Copy_On_Write()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			var (pager, allocator) = CreateStore(geometry);
			using var cleanup = pager;

			// generation 1 writes one extent value
			var writer1 = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
			var big = new byte[70_000];
			new Random(5).NextBytes(big);
			writer1.Insert("big"u8, big);
			writer1.Insert("side"u8, "x"u8);
			uint root1 = Published(writer1);

			// generation 2 rewrites the OTHER key: the leaf is COWed, the extent is untouched and shared
			var writer2 = new FdbLiteTreeWriter(pager, allocator, generation: 2, root: root1);
			writer2.Insert("side"u8, "y"u8);

			Assert.That(FdbLiteTreeReader.TryGetValue(pager, root1, "big"u8, out var v1), Is.True);
			Assert.That(v1.SequenceEqual(big), Is.True, "gen1 still reads the extent");
			Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer2), "big"u8, out var v2), Is.True);
			Assert.That(v2.SequenceEqual(big), Is.True, "gen2 shares the same extent");
		}

	}

}
