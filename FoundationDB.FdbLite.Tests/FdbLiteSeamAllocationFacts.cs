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
	using System;
	using System.Collections.Generic;
	using FoundationDB.FdbLite;
	using SnowBank.Collections.CacheOblivious;

	/// <summary>Guards the per-cursor-move heap traffic of the span-first committed-store seam, FdbLite vs the ColaStore reference.</summary>
	/// <remarks>
	/// <para>The seam exposes span views (<c>CurrentKey</c>/<c>CurrentValue</c>) for compare/walk and <c>Copy*</c> for retain. A selector walk compares keys over spans and materializes only the resolved key, so its allocation is O(1) in the number of steps - not O(steps), as it was when the seam forced a <c>Slice.FromBytes</c> copy of key AND value on every move (that pre-fix path allocated ~23 KB for this 200-step walk; it now allocates a few hundred bytes).</para>
	/// <para>The walk assertion is the always-run structural regression guard (it fails hard if per-move copying returns); the printed table is the before/after measurement for the redesign.</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteSeamAllocationFacts : SimpleTest
	{

		private const int KeyCount = 2000;
		private const int KeySize = 16;
		private const int ValueSize = 48;
		private const int WalkSteps = 200;

		private static Key MakeKey(int i)
		{
			var bytes = new byte[KeySize];
			System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, i);
			return new Key(bytes.AsSlice());
		}

		private static Value MakeValue(int i)
		{
			var bytes = new byte[ValueSize];
			bytes[0] = (byte) i;
			bytes[^1] = (byte) (i >> 8);
			return new Value(bytes.AsSlice());
		}

		private static void Fill(IFdbCommittedStore store)
		{
			for (int i = 0; i < KeyCount; i++)
			{
				store[MakeKey(i * 2)] = MakeValue(i); // even keys, so odd pivots fall between
			}
		}

		private static IFdbCommittedStore Reference()
		{
			var store = (IFdbCommittedStore) new ColaCommittedStore(new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default)).Copy();
			Fill(store);
			return store;
		}

		private static (FdbLiteEngine Engine, IFdbCommittedStore Store) Persistent()
		{
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Hypothesis));
			var seed = new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);
			var writable = ((IFdbCommittedStore) seed).Copy();
			Fill(writable);
			engine.Commit(((FdbLiteCommittedStore) writable).Writer!, 1);
			return (engine, new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount));
		}

		/// <summary>Bytes allocated on this thread while running <paramref name="body"/> <paramref name="iterations"/> times (warmed once).</summary>
		private static long MeasureBytes(Action body, int iterations)
		{
			body(); // JIT + warm
			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < iterations; i++) { body(); }
			long after = GC.GetAllocatedBytesForCurrentThread();
			return (after - before) / iterations;
		}

		[Test]
		public void Measure_Selector_Walk_And_Range_Scan()
		{
			var reference = Reference();
			var (engine, persistent) = Persistent();
			using var cleanup = engine;

			// --- selector walk: seek pivot, WalkSteps forward, read the final key only ---
			long walkRef = MeasureBytes(() => WalkVia(reference), 50);
			long walkLite = MeasureBytes(() => WalkVia(persistent), 50);

			// --- range scan: collect key+value of a mid-store range into a list, like GetRange ---
			Key begin = MakeKey(400 * 2);
			Key end = MakeKey(700 * 2);
			long scanRef = MeasureBytes(() => ScanCollect(reference, begin, end), 50);
			long scanLite = MeasureBytes(() => ScanCollect(persistent, begin, end), 50);

			Log($"seam allocation (bytes/op), KeyCount={KeyCount} KeySize={KeySize} ValueSize={ValueSize} WalkSteps={WalkSteps}");
			Log($"  selector walk  : reference(Cola) = {walkRef,10:N0}   FdbLite = {walkLite,10:N0}   ratio = {(walkRef == 0 ? double.PositiveInfinity : (double) walkLite / walkRef):F1}x");
			Log($"  range scan(300): reference(Cola) = {scanRef,10:N0}   FdbLite = {scanLite,10:N0}   ratio = {(scanRef == 0 ? double.PositiveInfinity : (double) scanLite / scanRef):F1}x");
			Log("  (selector walk now materializes only the resolved key; range scan is the retain-all path, addressed by the VisitRange stage)");

			// structural guard: the walk's allocation must NOT scale with WalkSteps. Per-move key+value copying
			// (the pre-fix seam) allocated ~23 KB here; the span-first walk materializes one key, a few hundred bytes.
			// The bound is far above the necessary set (cursor path arrays + one key) and far below the O(steps) regression.
			Assert.That(walkLite, Is.LessThan(4000), "selector walk allocation regressed - per-move materialization is back");
		}

		private static void WalkVia(IFdbCommittedStore store)
		{
			var it = store.GetCursor();
			if (!it.Seek(MakeKey(401), orEqual: false)) { it.SeekBeforeFirst(); }
			for (int s = 0; s < WalkSteps; s++)
			{
				if (!it.Next()) { break; }
			}
			// the consumer reads ONLY the resolved key at the end; the value and every intermediate key are never used
			var resolved = it.CopyKey();
			GC.KeepAlive(resolved.Count);
		}

		private static void ScanCollect(IFdbCommittedStore store, Key begin, Key end)
		{
			var sink = new List<KeyValuePair<Key, Value>>();
			foreach (var kv in store.Scan(begin, end, reversed: false))
			{
				sink.Add(kv);
			}
			GC.KeepAlive(sink.Count);
		}

		[Test]
		public void VisitRange_Fold_Allocates_Flat_In_Range_Size()
		{
			// the seam-level proof behind the transaction aggregate-scan goal: a value-folding range read over the
			// span-first VisitRange must allocate O(1) in the number of pairs (only the cursor's path arrays), so a
			// 200x-larger range costs essentially the same as a small one - no per-pair copy off the page.
			const int N = 20_000;
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Hypothesis));
			using var cleanup = engine;
			var writable = ((IFdbCommittedStore) new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount)).Copy();
			for (int i = 0; i < N; i++)
			{
				writable[Int32Key(i)] = Int32Value(i);
			}
			engine.Commit(((FdbLiteCommittedStore) writable).Writer!, 1);
			var store = (IFdbCommittedStore) new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);

			Key begin = Int32Key(0), smallEnd = Int32Key(100), fullEnd = Int32Key(N);
			long smallSum = 0, fullSum = 0;
			long allocSmall = MeasureBytes(() => smallSum = SumInt32Range(store, begin, smallEnd), 50);
			long allocFull = MeasureBytes(() => fullSum = SumInt32Range(store, begin, fullEnd), 50);

			Assert.That(smallSum, Is.EqualTo(100L * 99 / 2), "small fold read the wrong values");
			Assert.That(fullSum, Is.EqualTo((long) N * (N - 1) / 2), "full fold read the wrong values");

			Log($"VisitRange int32 fold (bytes/op): 100 pairs = {allocSmall:N0}, {N:N0} pairs = {allocFull:N0}");
			Assert.That(allocFull, Is.LessThan(allocSmall + 512), "VisitRange allocation scales with range size - per-pair copying is back");
		}

		private static Key Int32Key(int i)
		{
			var b = new byte[4];
			System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(b, i);
			return new Key(b.AsSlice());
		}

		private static Value Int32Value(int i)
		{
			var b = new byte[4];
			System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(b, i);
			return new Value(b.AsSlice());
		}

		[Test]
		[Explicit("write-amplification diagnostic")]
		public void Measure_Bulk_Load_Per_Insert_Allocation()
		{
			// one BeginWrite / N Insert / one Commit (exactly the CompareBench durable-write shape). If per-insert
			// allocation scales with the leaf CAPACITY (page size), the cost is a full-leaf rebuild per insert, not
			// a per-transaction page copy (the Shadow set already makes COW once-per-page-per-transaction).
			foreach (var (label, geo) in new[] { ("Default 32KiB", FdbLiteGeometry.Default), ("16KiB", FdbLiteGeometry.Uniform(14)) })
			{
				const int N = 100_000;
				var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(geo));
				using var cleanup = engine;
				var writer = engine.BeginWrite();

				long before = GC.GetTotalAllocatedBytes(precise: true);
				for (int i = 0; i < N; i++)
				{
					var key = new byte[8];
					System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(key, i);
					var value = new byte[4];
					System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(value, i);
					writer.Insert(key, value);
				}
				long after = GC.GetTotalAllocatedBytes(precise: true);
				engine.Commit(writer, 1);

				long perInsert = (after - before) / N;
				Log($"{label}: {perInsert:N0} bytes/insert over {N:N0} sequential inserts in ONE transaction (leaf capacity ~{geo.PageSize / 19} int32 cells)");
			}
		}

		private static long SumInt32Range(IFdbCommittedStore store, Key begin, Key end)
		{
			var acc = new long[1];
			store.VisitRange(begin, end, reversed: false, acc, static (long[] s, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) =>
			{
				s[0] += System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(value); // span -> int32, no materialization
				return true;
			});
			return acc[0];
		}

	}

}
