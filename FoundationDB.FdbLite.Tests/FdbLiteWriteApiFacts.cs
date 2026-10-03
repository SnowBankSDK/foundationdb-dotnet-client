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

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteWriteApiFacts : SimpleTest
	{

		private static byte[] Key(int i)
		{
			var key = new byte[8];
			System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(key, i);
			return key;
		}

		private static byte[] Value(int seed, int length)
		{
			var value = new byte[length];
			new Random(seed).NextBytes(value);
			return value;
		}

		private static FdbLiteEngine Seeded(out FdbLiteHeapPager pager)
		{
			pager = new FdbLiteHeapPager(FdbLiteGeometry.Default);
			var engine = FdbLiteEngine.Create(pager);
			engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Off;
			engine.Write(1, w =>
			{
				for (int i = 0; i < 5_000; i++) { w.Insert(Key(i), Value(i, 64)); }
			});
			return engine;
		}

		private static bool Contains(FdbLiteEngine engine, int i)
			=> FdbLiteTreeReader.TryGetValue(engine.Pager, engine.Durable.RootPageId, Key(i), out _);

		[Test]
		public void Test_Abandon_Rolls_Back_Allocations_And_Recorded_Frees()
		{
			using var engine = Seeded(out _);
			ulong generationBefore = engine.Durable.Generation;
			var freeBefore = engine.MeasureFreeSpace();

			// the abandoned generation ALLOCATES (fresh inserts, an extent-sized value) and FREES durable
			// pages (a range clear), so the rollback has every category of side effect to undo
			void RunAbandonedCycle()
			{
				var writer = engine.BeginWrite();
				for (int i = 10_000; i < 11_000; i++) { writer.Insert(Key(i), Value(i, 64)); }
				writer.Insert(Key(20_000), Value(7, 60_000)); // out-of-line: allocates an extent
				Assert.That(writer.RemoveRange(Key(1_000), Key(2_000)), Is.EqualTo(1_000));
				engine.Abandon(writer);
			}
			RunAbandonedCycle();

			Assert.That(engine.Durable.Generation, Is.EqualTo(generationBefore), "nothing durable moved");
			var freeAfter = engine.MeasureFreeSpace();
			Assert.That(freeAfter.PendingBytes, Is.EqualTo(freeBefore.PendingBytes), "the frees recorded against the durable tree were erased");
			Assert.That(Contains(engine, 1_500), Is.True, "the durable tree still holds what the abandoned generation deleted");
			Assert.That(Contains(engine, 10_500), Is.False, "the abandoned inserts do not exist");
			// conservation against the DURABLE header is deliberately not asserted here: between an abandon
			// and the next commit, the rolled-back space sits above the durable frontier by design, and only
			// the commit below records it; the conservation check at the end is the one that must hold

			// rollback cannot un-advance the allocation frontier, so the FIRST cycle converts frontier space
			// into reusable space; the no-leak proof is STEADY STATE: a second identical abandoned cycle
			// (served from that reusable space) must leave the numbers exactly where the first left them
			RunAbandonedCycle();
			Assert.That(engine.MeasureFreeSpace(), Is.EqualTo(freeAfter), "an abandon cycle at steady state leaks nothing");

			// the engine accepts and commits the NEXT generation cleanly
			engine.Write(2, w => w.Insert(Key(30_000), Value(1, 8)));
			Assert.That(Contains(engine, 30_000), Is.True);
			FdbLiteFreeSpaceFacts.AssertConservation(engine, "after the commit that follows an abandon");
		}

		[Test]
		public void Test_Disposed_Transaction_Abandons_And_Committed_One_Does_Not()
		{
			using var engine = Seeded(out _);
			var freeBefore = engine.MeasureFreeSpace();

			using (var tx = engine.Write())
			{
				tx.Writer.Insert(Key(10_000), Value(2, 8));
				// no Commit: disposal must roll back
			}
			Assert.That(Contains(engine, 10_000), Is.False, "a disposed-without-commit transaction leaves no trace");
			Assert.That(engine.MeasureFreeSpace().PendingBytes, Is.EqualTo(freeBefore.PendingBytes));
			var freeAfterFirst = engine.MeasureFreeSpace();
			using (var tx = engine.Write())
			{ // steady state: the second abandoned cycle reuses what the first released
				tx.Writer.Insert(Key(10_000), Value(2, 8));
			}
			Assert.That(engine.MeasureFreeSpace(), Is.EqualTo(freeAfterFirst), "a dispose cycle at steady state leaks nothing");

			using (var tx = engine.Write())
			{
				tx.Writer.Insert(Key(10_001), Value(3, 8));
				tx.Commit(2);
			}
			Assert.That(Contains(engine, 10_001), Is.True, "a committed transaction publishes; its disposal is a no-op");
			FdbLiteFreeSpaceFacts.AssertConservation(engine, "after wrapper commit");
		}

		[Test]
		public void Test_Handler_Form_Commits_On_Success_And_Rolls_Back_On_Throw()
		{
			using var engine = Seeded(out _);
			var freeBefore = engine.MeasureFreeSpace();

			Assert.That(
				() => engine.Write(2, w =>
				{
					w.Insert(Key(10_000), Value(4, 8));
					throw new InvalidOperationException("boom");
				}),
				Throws.InvalidOperationException.With.Message.EqualTo("boom"));
			Assert.That(Contains(engine, 10_000), Is.False, "the throwing handler's generation rolled back");
			Assert.That(engine.MeasureFreeSpace().PendingBytes, Is.EqualTo(freeBefore.PendingBytes));

			int result = engine.Write(2, w =>
			{
				w.Insert(Key(10_001), Value(5, 8));
				return 42;
			});
			Assert.That(result, Is.EqualTo(42));
			Assert.That(Contains(engine, 10_001), Is.True);
			FdbLiteFreeSpaceFacts.AssertConservation(engine, "after handler commit");
		}

		[Test]
		public void Test_Second_BeginWrite_Throws_While_One_Is_In_Flight()
		{
			using var engine = Seeded(out _);
			var writer = engine.BeginWrite();
			Assert.That(() => engine.BeginWrite(), Throws.InvalidOperationException, "the engine is single-writer and a silent second writer would corrupt shared state");
			engine.Abandon(writer);
			var next = engine.BeginWrite(); // the slot was released
			engine.Abandon(next);
			Assert.That(engine.TryAbandon(next), Is.False, "an already-abandoned writer is no longer in flight");
		}

	}

}
