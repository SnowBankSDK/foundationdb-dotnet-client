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
	using SnowBank.Collections.CacheOblivious;

	/// <summary>Differential conformance of the persistent committed store against the ColaStore-backed reference, through the seam surface only.</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteCommittedStoreFacts : SimpleTest
	{

		private static Key K(string text) => new(Slice.FromStringAscii(text));

		private static Key K(byte[] bytes) => new(bytes.AsSlice());

		private static Value V(byte[] bytes) => new(bytes.AsSlice());

		private static ColaCommittedStore CreateReference()
			=> new(new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default));

		private static (FdbLiteEngine Engine, FdbLiteCommittedStore Store) CreatePersistent(FdbLiteGeometry geometry)
		{
			var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(geometry));
			return (engine, new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount));
		}

		/// <summary>Compares every read surface of the two stores.</summary>
		private static void AssertSameState(IFdbCommittedStore expected, IFdbCommittedStore actual, string label)
		{
			Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label}: Count");
			Assert.That(actual.IterateOrdered(), Is.EqualTo(expected.IterateOrdered()).Using(PairComparer.Instance), $"{label}: IterateOrdered");

			// spot ranges, forward and reversed
			foreach (var (begin, end) in new[] { ("a", "z"), ("", "\xFF"), ("k3", "k7"), ("nope1", "nope2") })
			{
				var b = K(begin);
				var e = K(end);
				Assert.That(actual.Scan(b, e, false), Is.EqualTo(expected.Scan(b, e, false)).Using(PairComparer.Instance), $"{label}: Scan({begin}..{end})");
				Assert.That(actual.Scan(b, e, true), Is.EqualTo(expected.Scan(b, e, true)).Using(PairComparer.Instance), $"{label}: ScanReverse({begin}..{end})");
			}

			// cursor walk equivalence (interface-shaped, like the merge iterator uses it)
			var ce = expected.GetCursor();
			var ca = actual.GetCursor();
			bool he = ce.SeekFirst(), ha = ca.SeekFirst();
			Assert.That(ha, Is.EqualTo(he), $"{label}: SeekFirst");
			while (he)
			{
				Assert.That(PairComparer.Instance.Equals(ca.CopyCurrent(), ce.CopyCurrent()), Is.True, $"{label}: cursor Current");
				he = ce.Next();
				ha = ca.Next();
				Assert.That(ha, Is.EqualTo(he), $"{label}: cursor Next");
			}
		}

		private sealed class PairComparer : IEqualityComparer<KeyValuePair<Key, Value>>
		{
			public static readonly PairComparer Instance = new();
			public bool Equals(KeyValuePair<Key, Value> x, KeyValuePair<Key, Value> y) => x.Key.Equals(y.Key) && x.Value.Equals(y.Value);
			public int GetHashCode(KeyValuePair<Key, Value> obj) => obj.Key.GetHashCode();
		}

		[Test]
		public void Test_Differential_Random_Mutation_Cycles()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var reference = (IFdbCommittedStore) CreateReference();
				var (engine, persistentSeed) = CreatePersistent(geometry);
				using var cleanup = engine;
				var persistent = (IFdbCommittedStore) persistentSeed;

				var rnd = new Random(4711);
				for (int cycle = 0; cycle < 5; cycle++)
				{
					// one "commit": copy both stores, apply the same mutations through the seam surface
					var refNext = reference.Copy();
					var perNext = persistent.Copy();

					for (int i = 0; i < 400; i++)
					{
						int op = rnd.Next(10);
						if (op < 6)
						{
							var key = K($"k{rnd.Next(200):D3}");
							var value = new byte[rnd.Next(0, 100)];
							rnd.NextBytes(value);
							refNext[key] = V(value);
							perNext[key] = V(value);
						}
						else if (op < 8)
						{
							var key = K($"k{rnd.Next(200):D3}");
							Assert.That(perNext.Remove(key), Is.EqualTo(refNext.Remove(key)), $"[{geometry}] Remove parity");
						}
						else
						{
							var begin = K($"k{rnd.Next(200):D3}");
							var end = K($"k{rnd.Next(200):D3}");
							if (begin.CompareTo(end) > 0) { (begin, end) = (end, begin); }
							int refRemoved;
							try
							{
								refRemoved = refNext.RemoveRange(begin, end);
							}
							catch (Exception e)
							{
								Assert.Fail($"[{geometry}] reference store crashed on RemoveRange({begin}, {end}) with {refNext.Count} keys: {e.GetType().Name} {e.Message}");
								throw;
							}
							Assert.That(perNext.RemoveRange(begin, end), Is.EqualTo(refRemoved), $"[{geometry}] RemoveRange parity");
						}

						if (i % 50 == 0)
						{ // TryGetKeyValue parity on a random probe (the versionstamp path relies on the stored instance)
							var probe = K($"k{rnd.Next(200):D3}");
							bool fe = refNext.TryGetKeyValue(probe, out var ee);
							bool fa = perNext.TryGetKeyValue(probe, out var ea);
							Assert.That(fa, Is.EqualTo(fe), $"[{geometry}] TryGetKeyValue parity");
							if (fe)
							{
								Assert.That(PairComparer.Instance.Equals(ea, ee), Is.True, $"[{geometry}] TryGetKeyValue pair parity");
							}
						}
					}

					AssertSameState(refNext, perNext, $"[{geometry}] cycle {cycle} (writable)");

					// publish: the persistent side commits its generation; both become the next committed state
					engine.Commit(((FdbLiteCommittedStore) perNext).Writer!, databaseVersion: (ulong) (100 + cycle));
					persistent = new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);
					reference = refNext;

					AssertSameState(reference, persistent, $"[{geometry}] cycle {cycle} (committed)");
					Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) reference.Count), $"[{geometry}] header key count");
				}
			}
		}

		[Test]
		public void Test_Immutable_Reference_Is_A_Correct_Seam_Binding()
		{
			// the dumb persistent-sorted-map backend, run in the differential in lockstep with BOTH the ColaStore
			// reference and the FdbLite engine: a third, independent binding of the span-first seam. Its always-on
			// agreement is the evidence the contract stayed representation-agnostic (a page-backed, an arena-backed,
			// and a structural-sharing backend all satisfy it unchanged) - and a storage cross-check that neither
			// optimized structure has drifted from the trivially-correct one.
			var geometry = FdbLiteGeometry.Hypothesis;
			var cola = (IFdbCommittedStore) CreateReference();
			var immutable = (IFdbCommittedStore) new ImmutableCommittedStore();
			var (engine, seed) = CreatePersistent(geometry);
			using var cleanup = engine;
			var persistent = (IFdbCommittedStore) seed;

			var rnd = new Random(1789);
			for (int cycle = 0; cycle < 5; cycle++)
			{
				var colaNext = cola.Copy();
				var immNext = immutable.Copy();
				var perNext = persistent.Copy();

				for (int i = 0; i < 400; i++)
				{
					int op = rnd.Next(10);
					if (op < 6)
					{
						var key = K($"k{rnd.Next(200):D3}");
						var value = new byte[rnd.Next(0, 100)];
						rnd.NextBytes(value);
						colaNext[key] = V(value);
						immNext[key] = V(value);
						perNext[key] = V(value);
					}
					else if (op < 8)
					{
						var key = K($"k{rnd.Next(200):D3}");
						bool removed = colaNext.Remove(key);
						Assert.That(immNext.Remove(key), Is.EqualTo(removed), "Immutable Remove parity");
						Assert.That(perNext.Remove(key), Is.EqualTo(removed), "FdbLite Remove parity");
					}
					else
					{
						var begin = K($"k{rnd.Next(200):D3}");
						var end = K($"k{rnd.Next(200):D3}");
						if (begin.CompareTo(end) > 0) { (begin, end) = (end, begin); }
						int removed = colaNext.RemoveRange(begin, end);
						Assert.That(immNext.RemoveRange(begin, end), Is.EqualTo(removed), "Immutable RemoveRange parity");
						Assert.That(perNext.RemoveRange(begin, end), Is.EqualTo(removed), "FdbLite RemoveRange parity");
					}
				}

				AssertSameState(colaNext, immNext, $"cycle {cycle} (immutable vs cola, writable)");
				AssertSameState(immNext, perNext, $"cycle {cycle} (fdblite vs immutable, writable)");

				engine.Commit(((FdbLiteCommittedStore) perNext).Writer!, databaseVersion: (ulong) (200 + cycle));
				persistent = new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);
				cola = colaNext;
				immutable = immNext;

				AssertSameState(immutable, persistent, $"cycle {cycle} (fdblite vs immutable, committed)");
			}
		}

		[Test]
		public void Test_Cursor_Seek_Semantics_Match_The_Reference()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			var reference = (IFdbCommittedStore) CreateReference();
			var (engine, persistentSeed) = CreatePersistent(geometry);
			using var cleanup = engine;

			var refW = reference.Copy();
			var perW = ((IFdbCommittedStore) persistentSeed).Copy();
			var rnd = new Random(88);
			for (int i = 0; i < 500; i++)
			{ // even keys so odd probes fall between
				var key = new byte[4];
				BinaryPrimitivesWriteInt(key, rnd.Next(50_000) * 2);
				var value = new byte[] { (byte) i };
				refW[K(key)] = V(value);
				perW[K(key)] = V(value);
			}
			engine.Commit(((FdbLiteCommittedStore) perW).Writer!, 1);
			var per = (IFdbCommittedStore) new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);

			var ce = refW.GetCursor();
			var ca = per.GetCursor();
			for (int i = 0; i < 800; i++)
			{
				var probe = new byte[4];
				BinaryPrimitivesWriteInt(probe, rnd.Next(100_001));
				bool orEqual = rnd.Next(2) == 0;

				bool fe = ce.Seek(K(probe), orEqual);
				bool fa = ca.Seek(K(probe), orEqual);
				Assert.That(fa, Is.EqualTo(fe), "Seek parity");
				if (fe)
				{
					Assert.That(ca.CopyKey(), Is.EqualTo(ce.CopyKey()), "Seek key parity");
				}
				else
				{ // the seam's miss protocol: rewind before the first key, then walk forward
					ce.SeekBeforeFirst();
					ca.SeekBeforeFirst();
				}

				// walk a few steps in a random direction from wherever we are
				int steps = rnd.Next(1, 4);
				bool forward = rnd.Next(2) == 0;
				for (int s = 0; s < steps; s++)
				{
					bool me = forward ? ce.Next() : ce.Previous();
					bool ma = forward ? ca.Next() : ca.Previous();
					Assert.That(ma, Is.EqualTo(me), "step parity");
					if (!me)
					{
						break;
					}
					Assert.That(ca.CopyKey(), Is.EqualTo(ce.CopyKey()), "step key parity");
				}
			}

			static void BinaryPrimitivesWriteInt(byte[] buffer, int value)
				=> System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(buffer, value);
		}

		[Test]
		public void Test_Delegate_Read_Leg_Is_Zero_Copy_Capable()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			var (engine, seed) = CreatePersistent(geometry);
			using var cleanup = engine;

			var writable = ((IFdbCommittedStore) seed).Copy();
			var big = new byte[70_000];
			new Random(3).NextBytes(big);
			writable[K("big")] = V(big);
			writable[K("small")] = V([ 1, 2, 3 ]);
			engine.Commit(((FdbLiteCommittedStore) writable).Writer!, 1);
			var store = (IFdbCommittedStore) new FdbLiteCommittedStore(engine, engine.Durable.RootPageId, engine.Durable.KeyCount);

			// the delegate leg sees the raw bytes (for the big value: the extent, as one span) and
			// decodes in place without any intermediate copy
			int len = store.Read(K("big"), 0, (int _, ReadOnlySpan<byte> span, bool found) =>
			{
				Assert.That(found, Is.True);
				Assert.That(span.SequenceEqual(big), Is.True, "the delegate sees the whole value as one span");
				return span.Length;
			});
			Assert.That(len, Is.EqualTo(70_000));

			bool missing = store.Read(K("nope"), 0, (int _, ReadOnlySpan<byte> span, bool found) => found);
			Assert.That(missing, Is.False);
		}

	}

}
