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
	public class FdbLiteDurabilityFacts : SimpleTest
	{

		private static string NewStorePath()
		{
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-tests");
			Directory.CreateDirectory(dir);
			return Path.Combine(dir, $"store-{Guid.NewGuid():N}.sbkv");
		}

		private static void DeleteQuietly(string path)
		{
			try { File.Delete(path); } catch { }
		}

		[Test]
		public void Test_Snapshot_Header_Codec_And_Torn_Detection()
		{
			var header = new FdbLiteSnapshotHeader(42, 0xfdb1337000123, 100, 2048, 77, 501, 12345, 40);
			var block = new byte[16 * 1024];

			header.Write(block, blockId: 1);
			Assert.That(FdbLiteSnapshotHeader.TryRead(block, 1, out var decoded), Is.True);
			Assert.That(decoded, Is.EqualTo(header));

			// the same bytes at the OTHER slot fail (checksum is slot-seeded)
			Assert.That(FdbLiteSnapshotHeader.TryRead(block, 2, out _), Is.False);

			// a torn write fails
			block[50] ^= 0x01;
			Assert.That(FdbLiteSnapshotHeader.TryRead(block, 1, out _), Is.False);

			// a blank slot fails
			Assert.That(FdbLiteSnapshotHeader.TryRead(new byte[16 * 1024], 2, out _), Is.False);
		}

		[Test]
		public void Test_Older_Format_Store_Is_Rejected_Loudly()
		{
			// the format is Experimental: an older file has no migration path, and it must fail the
			// file-header version check with a clear message, never reach the page-level accessors
			var path = NewStorePath();
			try
			{
				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, FdbLiteGeometry.Default, regionSizeInBytes: 1 << 20))
				{
					var writer = engine.BeginWrite();
					writer.Insert("hello"u8, "world"u8);
					engine.Commit(writer, databaseVersion: 1);
				}

				// stamp the file as written at format 1 (readable by format-1 readers): offsets 8 and 10 of block 0
				using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
				{
					fs.Position = 8;
					fs.Write([ 1, 0, 1, 0 ]);
				}

				Assert.That(
					() => FdbLiteEngine.OpenOrCreateFile(path, FdbLiteGeometry.Default, regionSizeInBytes: 1 << 20),
					Throws.InstanceOf<InvalidDataException>().With.Message.Contains("format"));
			}
			finally
			{
				DeleteQuietly(path);
			}
		}

		[Test]
		public void Test_Memory_Mapped_Pager_Roundtrip_And_Reopen()
		{
			var path = NewStorePath();
			try
			{
				var geometry = FdbLiteGeometry.Hypothesis;
				var data = new byte[geometry.BlockSize];
				new Random(1).NextBytes(data);

				using (var pager = FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20))
				{
					pager.Grow(8);
					pager.WriteBlocks(5, data);

					// coherence: the mapped read view sees the positional write immediately
					Assert.That(pager.ReadBlocks(5, 1).SequenceEqual(data), Is.True, "map sees the write");

					pager.Flush();
				}

				using (var pager = FdbLiteMemoryMappedPager.Open(path, geometry, regionSizeInBytes: 1 << 20))
				{
					Assert.That(pager.BlockCount, Is.GreaterThanOrEqualTo(8));
					Assert.That(pager.ReadBlocks(5, 1).SequenceEqual(data), Is.True, "data survives reopen");

					// growth into a second region, then truncation back below it
					pager.Grow(pager.RegionSizeInBlocks + 1);
					Assert.That(pager.BlockCount, Is.EqualTo(2 * pager.RegionSizeInBlocks));
					pager.WriteBlocks(pager.RegionSizeInBlocks + 2, data);
					Assert.That(pager.ReadBlocks(pager.RegionSizeInBlocks + 2, 1).SequenceEqual(data), Is.True);

					pager.Truncate(pager.RegionSizeInBlocks);
					Assert.That(pager.BlockCount, Is.EqualTo(pager.RegionSizeInBlocks));
					Assert.That(pager.ReadBlocks(5, 1).SequenceEqual(data), Is.True, "data below the cut survives");
				}
				Assert.That(new FileInfo(path).Length, Is.EqualTo(1 << 20), "the file really shrank");
			}
			finally
			{
				DeleteQuietly(path);
			}
		}

		[Test]
		public void Test_Store_Engine_Commits_Survive_Reopen()
		{
			var path = NewStorePath();
			try
			{
				var geometry = FdbLiteGeometry.Hypothesis;
				var model = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
				var rnd = new Random(9);

				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, geometry, regionSizeInBytes: 1 << 20))
				{
					for (ulong gen = 0; gen < 3; gen++)
					{ // three committed generations: inserts, updates, deletes, one extent value
						var writer = engine.BeginWrite();
						for (int i = 0; i < 300; i++)
						{
							string k = $"key-{rnd.Next(500):D4}";
							var v = new byte[rnd.Next(0, 80)];
							rnd.NextBytes(v);
							writer.Insert(System.Text.Encoding.ASCII.GetBytes(k), v);
							model[k] = v;
						}
						var big = new byte[40_000 + (int) gen];
						rnd.NextBytes(big);
						writer.Insert(System.Text.Encoding.ASCII.GetBytes($"big-{gen}"), big);
						model[$"big-{gen}"] = big;

						var dead = model.Keys.Where(k => k.StartsWith("key-") && rnd.Next(4) == 0).Take(30).ToList();
						foreach (var k in dead)
						{
							Assert.That(writer.Remove(System.Text.Encoding.ASCII.GetBytes(k)), Is.True);
							model.Remove(k);
						}

						engine.Commit(writer, databaseVersion: 1000 + gen);
					}

					Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count), "exact key count maintained");
				}

				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, geometry, regionSizeInBytes: 1 << 20))
				{
					Assert.That(engine.Durable.DatabaseVersion, Is.EqualTo(1002));
					Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count));

					foreach (var kv in model)
					{
						Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, engine.Durable.RootPageId, System.Text.Encoding.ASCII.GetBytes(kv.Key), out var v), Is.True, $"missing {kv.Key} after reopen");
						if (!v.SequenceEqual(kv.Value))
						{
							Assert.Fail($"value mismatch for {kv.Key} after reopen");
						}
					}

					// the reopened store keeps working: another generation commits and reopens clean
					var writer = engine.BeginWrite();
					writer.Insert("after-reopen"u8, "ok"u8);
					engine.Commit(writer, 1003);
				}

				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, geometry, regionSizeInBytes: 1 << 20))
				{
					Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, engine.Durable.RootPageId, "after-reopen"u8, out var v), Is.True);
					Assert.That(v.SequenceEqual("ok"u8), Is.True);
				}
			}
			finally
			{
				DeleteQuietly(path);
			}
		}

		[Test]
		public void Test_Torn_Header_Falls_Back_To_Previous_Generation()
		{
			var path = NewStorePath();
			try
			{
				var geometry = FdbLiteGeometry.Hypothesis;
				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, geometry, regionSizeInBytes: 1 << 20))
				{
					var w2 = engine.BeginWrite();
					w2.Insert("stable"u8, "gen2"u8);
					engine.Commit(w2, 100);

					var w3 = engine.BeginWrite();
					w3.Insert("stable"u8, "gen3"u8);
					w3.Insert("only-in-3"u8, "x"u8);
					engine.Commit(w3, 101);
				}

				// simulate a torn commit of generation 3: its header slot gets corrupted on disk
				// (slots alternate from creation: gen 1 slot 1, gen 2 slot 2, gen 3 slot 1)
				using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
				{
					fs.Position = (long) 1 * geometry.BlockSize + 20;
					int b = fs.ReadByte();
					fs.Position -= 1;
					fs.WriteByte((byte) (b ^ 0xFF));
				}

				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, geometry, regionSizeInBytes: 1 << 20))
				{
					Assert.That(engine.Durable.Generation, Is.EqualTo(2), "the torn generation is discarded");
					Assert.That(engine.Durable.DatabaseVersion, Is.EqualTo(100));
					Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, engine.Durable.RootPageId, "stable"u8, out var v), Is.True);
					Assert.That(v.SequenceEqual("gen2"u8), Is.True, "generation 2 content intact");
					Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, engine.Durable.RootPageId, "only-in-3"u8, out _), Is.False, "the torn generation's writes are invisible");

					// and the store keeps working from generation 2 (the torn generation's allocations
					// were never part of generation 2's persisted free state: no sweep, no leak)
					var writer = engine.BeginWrite();
					Assert.That(writer.Generation, Is.EqualTo(3));
					writer.Insert("recovered"u8, "yes"u8);
					engine.Commit(writer, 102);
					Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, engine.Durable.RootPageId, "recovered"u8, out v), Is.True);
					Assert.That(v.SequenceEqual("yes"u8), Is.True);
				}
			}
			finally
			{
				DeleteQuietly(path);
			}
		}

		[Test]
		public void Test_Pins_Hold_The_Horizon_And_Release_It()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);

			var w2 = engine.BeginWrite();
			for (int i = 0; i < 500; i++)
			{
				var key = new byte[8];
				BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) i);
				w2.Insert(key, "gen2-value"u8);
			}
			engine.Commit(w2, 10);

			// pin generation 2, then let generation 3 rewrite everything
			var pin = engine.BeginRead();
			Assert.That(pin.Generation, Is.EqualTo(2));

			var w3 = engine.BeginWrite();
			for (int i = 0; i < 500; i++)
			{
				var key = new byte[8];
				BinaryPrimitives.WriteUInt64BigEndian(key, (ulong) i);
				w3.Insert(key, "gen3-value"u8);
			}
			engine.Commit(w3, 11);

			// one more commit: without the pin this would promote generation-2 frees for reuse
			var w4 = engine.BeginWrite();
			w4.Insert("one-more"u8, "x"u8);
			engine.Commit(w4, 12);

			var stats = engine.GetStats();
			Assert.That(stats.PinCount, Is.EqualTo(1));
			Assert.That(stats.OldestPinnedGeneration, Is.EqualTo(2));
			Assert.That(stats.PendingReclaimBlocks, Is.GreaterThan(0), "the pin retains freed blocks");

			// the pinned generation still reads its own truth
			var key0 = new byte[8];
			Assert.That(FdbLiteTreeReader.TryGetValue(engine.Pager, pin.RootPageId, key0, out var v), Is.True);
			Assert.That(v.SequenceEqual("gen2-value"u8), Is.True, "pinned generation intact under later rewrites");

			// release: the next commit promotes, and the retained blocks drain
			long retainedBefore = stats.PendingReclaimBlocks;
			engine.EndRead(in pin);
			var w5 = engine.BeginWrite();
			w5.Insert("drain"u8, "x"u8);
			engine.Commit(w5, 13);
			Assert.That(engine.GetStats().PendingReclaimBlocks, Is.LessThan(retainedBefore), "released pin lets reclamation drain");
		}

	}

}
