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
	using FoundationDB.Client;

	/// <summary>Replace-heavy churn against a small key set, which is the shape a concurrent counter benchmark produces.</summary>
	/// <remarks>Reduction of a layer-suite benchmark that took over four minutes before failing inside the extent decoder. The engine's own suites did not cover repeated REPLACE of the same keys across many generations, which is where the page is rebuilt again and again rather than grown.</remarks>
	[TestFixture]
	public class FdbLiteChurnRepro : SimpleTest
	{

		private static byte[] CounterKey(int i)
		{
			var key = new byte[20];
			"\xFE/counters/"u8.CopyTo(key);
			BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(16), i);
			return key;
		}

		[Test]
		public void Test_Repeated_Replace_Keeps_The_Store_Readable()
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

			// enough keys to FILL and STRIP pages first: a key set that fits one page is never stripped, so a
			// smaller version of this test exercises churn without exercising the feature under test
			const int KEYS = 20_000;
			const int ROUNDS = 5;

			var value = new byte[8];
			ulong version = 1;

			// seed
			var seed = engine.BeginWrite();
			for (int i = 0; i < KEYS; i++) { seed.Insert(CounterKey(i), value); }
			engine.Commit(seed, version++);

			// churn: every round REPLACES every key, in its own generation, which is what an increment does
			for (int round = 1; round <= ROUNDS; round++)
			{
				var w = engine.BeginWrite();
				for (int i = 0; i < KEYS; i++)
				{
					BinaryPrimitives.WriteInt64LittleEndian(value, round);
					w.Insert(CounterKey(i), value);
				}
				// what does a round of PURE same-length replacement actually cost? A replace changes no key, no
				// length and no offset, so in principle it is a memcpy over the value bytes.
				Log($"# round {round}: spliced={w.CellsSpliced} pagesWritten={w.PagesWritten} copies={w.PageCopies} splits={w.PageSplits} stripped={w.PagesStripped} descents={w.LeafDescents}");
				engine.Commit(w, version++);

				// read everything back every round, so a corrupted page is caught at the round that made it
				var pin = engine.BeginRead();
				try
				{
					var cursor = new FdbLiteTreeCursor(engine.Pager, pin.RootPageId);
					int seen = 0;
					if (cursor.SeekFirst())
					{
						do
						{
							// touching the VALUE is what trips the extent decoder if a cell is misread
							Assert.That(cursor.CurrentValue.Length, Is.EqualTo(8), $"round {round}, cell {seen}: value must still be 8 bytes");
							++seen;
						}
						while (cursor.MoveNext());
					}
					Assert.That(seen, Is.EqualTo(KEYS), $"round {round}: every key must still be present");
					// the count identity betrays ORPHANED pages (unreachable, so no scan or audit can visit them),
					// and the cross-level audit betrays mis-routed or truncated keys the scan reads back happily
					Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) KEYS), $"round {round}: committed KeyCount vs actual tree");
					Assert.That(FdbLiteTreeAudit.Check(engine.Pager, pin.RootPageId), Is.Empty, $"round {round}: structural audit");
				}
				finally
				{
					engine.EndRead(in pin);
				}
			}
		}

		[Test]
		public async Task Test_Repeated_Replace_Through_The_Emulator_Keeps_The_Store_Readable()
		{
			// The engine-level churn test above drives the raw engine with reclamation ON. The EMULATOR runs the
			// engine with every version retained, which is the configuration the layer suites actually use and the
			// one the failing benchmark ran under. Retention changes which pages stay live, so it is the largest
			// untested difference between that failure and the reproduction that does not reproduce it.
			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Default, retainEveryVersion: true);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			const int KEYS = 20_000;
			const int ROUNDS = 4;
			const int BATCH = 2_000;

			for (int start = 0; start < KEYS; start += BATCH)
			{
				using var tr = await db.BeginTransactionAsync(FdbTransactionMode.Default, this.Cancellation);
				for (int i = start; i < start + BATCH; i++)
				{
					tr.Set(Slice.FromBytes(CounterKey(i)), Slice.FromInt64(0));
				}
				await tr.CommitAsync();
			}

			for (int round = 1; round <= ROUNDS; round++)
			{
				for (int start = 0; start < KEYS; start += BATCH)
				{
					using var tr = await db.BeginTransactionAsync(FdbTransactionMode.Default, this.Cancellation);
					for (int i = start; i < start + BATCH; i++)
					{
						// same-length replacement, which is what an increment settles into
						tr.Set(Slice.FromBytes(CounterKey(i)), Slice.FromInt64(round));
					}
					await tr.CommitAsync();
				}

				// full range read, which is the path that tripped the extent decoder
				using var read = await db.BeginTransactionAsync(FdbTransactionMode.Default, this.Cancellation);
				var all = await read.GetRangeAsync(Slice.FromBytes(CounterKey(0)), Slice.FromBytes(CounterKey(KEYS)), new() { Limit = KEYS + 10 });
				Assert.That(all.Count, Is.EqualTo(KEYS), $"round {round}: every counter must read back");
			}
		}

	}

}
