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
	public class FdbLiteTreeFacts : SimpleTest
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

		/// <summary>Walks the whole tree forward and returns every key.</summary>
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

		private static List<byte[]> ScanBackward(IFdbLitePager pager, uint root)
		{
			var keys = new List<byte[]>();
			var cursor = new FdbLiteTreeCursor(pager, root);
			if (cursor.SeekLast())
			{
				do { keys.Add(cursor.CurrentKey.ToArray()); } while (cursor.MovePrevious());
			}
			return keys;
		}

		[Test]
		public void Test_Insert_Get_Update_Small()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var (pager, allocator) = CreateStore(geometry);
				using var cleanup = pager;
				var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);

				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "hello"u8, out _), Is.False, $"[{geometry}] empty tree");

				writer.Insert("hello"u8, "world"u8);
				writer.Insert("alpha"u8, "1"u8);
				writer.Insert("omega"u8, "2"u8);

				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "hello"u8, out var v), Is.True);
				Assert.That(v.SequenceEqual("world"u8), Is.True);
				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "alpha"u8, out v), Is.True);
				Assert.That(v.SequenceEqual("1"u8), Is.True);
				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "nope"u8, out _), Is.False);

				// update in place (same generation)
				writer.Insert("hello"u8, "again, with a longer value this time"u8);
				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), "hello"u8, out v), Is.True);
				Assert.That(v.SequenceEqual("again, with a longer value this time"u8), Is.True);

				Assert.That(ScanForward(pager, Published(writer)), Has.Count.EqualTo(3));
			}
		}

		[Test]
		public void Test_Leaf_Holding_Only_Empty_Values_Accepts_More_Of_Them()
		{
			// the secondary-index shape: every value is empty, so a leaf's value heap never leaves the page
			// end - an offset that does not fit the u16 frontier field on 64 KiB pages, where it must round
			// through the 0 "empty heap" sentinel instead of overflowing on the second splice
			foreach (var geometry in TestGeometries.All)
			{
				var (pager, allocator) = CreateStore(geometry);
				using var cleanup = pager;
				var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);

				const int COUNT = 10_000;
				var rnd = new Random(4021);
				var order = Enumerable.Range(0, COUNT).OrderBy(_ => rnd.Next()).ToList();

				Span<byte> key = stackalloc byte[8];
				foreach (var i in order)
				{ // scattered arrival, so growth goes through the splice path and not the append path
					BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) i);
					writer.Insert(key, ReadOnlySpan<byte>.Empty);
				}

				var root = Published(writer);
				for (int i = 0; i < COUNT; i++)
				{
					BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) i);
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, root, key, out var v), Is.True, $"[{geometry}] missing key {i}");
					Assert.That(v.Length, Is.Zero, $"[{geometry}] key {i} should have an empty value");
				}
				Assert.That(ScanForward(pager, root), Has.Count.EqualTo(COUNT), $"[{geometry}] forward scan");
				Assert.That(FdbLiteTreeAudit.Check(pager, root), Is.Empty, $"[{geometry}] structural audit");
			}
		}

		[Test]
		public void Test_Reference_Model_Random_Inserts_And_Scans()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var (pager, allocator) = CreateStore(geometry);
				using var cleanup = pager;
				var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
				var model = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Instance);

				var rnd = new Random(1234);
				for (int i = 0; i < 3_000; i++)
				{
					var key = new byte[rnd.Next(1, 60)];
					rnd.NextBytes(key);
					var value = new byte[rnd.Next(0, 120)];
					rnd.NextBytes(value);
					writer.Insert(key, value);
					model[key] = value;

					if (i % 5 == 0 && model.Count > 0)
					{ // sprinkle updates of existing keys
						var existing = model.Keys.ElementAt(rnd.Next(model.Count));
						var updated = new byte[rnd.Next(0, 200)];
						rnd.NextBytes(updated);
						writer.Insert(existing, updated);
						model[existing] = updated;
					}
				}

				// every model entry reads back exactly
				foreach (var kv in model)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), kv.Key, out var v), Is.True, $"[{geometry}] missing key");
					if (!v.SequenceEqual(kv.Value))
					{
						Assert.Fail($"[{geometry}] value mismatch for a key of {kv.Key.Length} bytes");
					}
				}

				// full scans in both directions match the model order
				var expected = model.Keys.ToList();
				Assert.That(ScanForward(pager, Published(writer)), Is.EqualTo(expected), $"[{geometry}] forward scan");
				expected.Reverse();
				Assert.That(ScanBackward(pager, Published(writer)), Is.EqualTo(expected), $"[{geometry}] backward scan");
			}
		}

		[Test]
		public void Test_Seek_Floor_And_Ceiling_Match_The_Model()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			var (pager, allocator) = CreateStore(geometry);
			using var cleanup = pager;
			var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
			var model = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Instance);

			var rnd = new Random(777);
			for (int i = 0; i < 2_000; i++)
			{ // even-valued keys only, so every odd probe falls between keys
				var key = new byte[8];
				BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) rnd.Next(1_000_000) * 2);
				var value = new byte[] { (byte) i };
				writer.Insert(key, value);
				model[key] = value;
			}

			var keys = model.Keys.ToList();
			var cursor = new FdbLiteTreeCursor(pager, Published(writer));
			for (int i = 0; i < 500; i++)
			{
				var probe = new byte[8];
				BinaryPrimitives.WriteUInt64BigEndian(probe, (ulong) rnd.Next(2_000_001));

				// floor (strictly below, and at-or-below)
				int idx = keys.BinarySearch(probe, ByteArrayComparer.Instance);
				int floorStrict = idx >= 0 ? idx - 1 : ~idx - 1;
				int floorOrEqual = idx >= 0 ? idx : ~idx - 1;

				bool found = cursor.SeekFloor(probe, orEqual: false);
				Assert.That(found, Is.EqualTo(floorStrict >= 0));
				if (found)
				{
					Assert.That(cursor.CurrentKey.SequenceEqual(keys[floorStrict]), Is.True, "floor(strict)");
				}

				found = cursor.SeekFloor(probe, orEqual: true);
				Assert.That(found, Is.EqualTo(floorOrEqual >= 0));
				if (found)
				{
					Assert.That(cursor.CurrentKey.SequenceEqual(keys[floorOrEqual]), Is.True, "floor(orEqual)");
				}

				// ceiling
				int ceiling = idx >= 0 ? idx : ~idx;
				found = cursor.SeekCeiling(probe);
				Assert.That(found, Is.EqualTo(ceiling < keys.Count));
				if (found)
				{
					Assert.That(cursor.CurrentKey.SequenceEqual(keys[ceiling]), Is.True, "ceiling");
				}
			}
		}

		[Test]
		public void Test_Adversarial_Key_Sizes_Split_Correctly()
		{
			// maximum-size keys at the smallest page: forces the K-way splits and degenerate
			// (leftmost-only) internal pages that a plain two-way split cannot produce
			var geometry = FdbLiteGeometry.Uniform(14);
			var (pager, allocator) = CreateStore(geometry);
			using var cleanup = pager;
			var writer = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
			var model = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Instance);

			var rnd = new Random(999);
			for (int i = 0; i < 60; i++)
			{
				var key = new byte[rnd.Next(9_000, 10_001)];
				rnd.NextBytes(key);
				var value = new byte[rnd.Next(0, geometry.MaxInlineValueLength + 1)];
				rnd.NextBytes(value);
				writer.Insert(key, value);
				model[key] = value;
			}

			foreach (var kv in model)
			{
				Assert.That(FdbLiteTreeReader.TryGetValue(pager, Published(writer), kv.Key, out var v), Is.True);
				Assert.That(v.SequenceEqual(kv.Value), Is.True);
			}
			Assert.That(ScanForward(pager, Published(writer)), Is.EqualTo(model.Keys.ToList()));
		}

		[Test]
		public void Test_Copy_On_Write_Preserves_Previous_Generations()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var (pager, allocator) = CreateStore(geometry);
				using var cleanup = pager;

				// generation 1: a few hundred keys
				var writer1 = new FdbLiteTreeWriter(pager, allocator, generation: 1, root: 0);
				var model1 = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Instance);
				var rnd = new Random(55);
				for (int i = 0; i < 400; i++)
				{
					var key = new byte[rnd.Next(4, 40)];
					rnd.NextBytes(key);
					var value = new byte[] { 1, (byte) i };
					writer1.Insert(key, value);
					model1[key] = value;
				}
				uint root1 = Published(writer1);

				// generation 2: overwrite half the keys, add new ones (freed pages stay pending: nothing promoted)
				var writer2 = new FdbLiteTreeWriter(pager, allocator, generation: 2, root: root1);
				var model2 = new SortedDictionary<byte[], byte[]>(model1, ByteArrayComparer.Instance);
				int n = 0;
				foreach (var key in model1.Keys.ToList())
				{
					if ((n++ & 1) == 0)
					{
						var value = new byte[] { 2, 2 };
						writer2.Insert(key, value);
						model2[key] = value;
					}
				}
				for (int i = 0; i < 200; i++)
				{
					var key = new byte[rnd.Next(4, 40)];
					rnd.NextBytes(key);
					var value = new byte[] { 2, (byte) i };
					writer2.Insert(key, value);
					model2[key] = value;
				}
				uint root2 = Published(writer2);

				// generation 1 must read EXACTLY as before (MVCC: no page of a retained generation was touched)
				foreach (var kv in model1)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, root1, kv.Key, out var v), Is.True, $"[{geometry}] gen1 key lost");
					Assert.That(v.SequenceEqual(kv.Value), Is.True, $"[{geometry}] gen1 value changed");
				}
				Assert.That(ScanForward(pager, root1), Is.EqualTo(model1.Keys.ToList()), $"[{geometry}] gen1 scan");

				// generation 2 reads its own state
				foreach (var kv in model2)
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(pager, root2, kv.Key, out var v), Is.True, $"[{geometry}] gen2 key lost");
					Assert.That(v.SequenceEqual(kv.Value), Is.True, $"[{geometry}] gen2 value changed");
				}
				Assert.That(ScanForward(pager, root2), Is.EqualTo(model2.Keys.ToList()), $"[{geometry}] gen2 scan");
			}
		}

	}

}
