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

namespace FoundationDB.FdbLite.Bench
{
	using System.Buffers.Binary;

	/// <summary>One replayable store-level operation, below any transaction handling.</summary>
	/// <remarks>No conflict tracking, no versions, no atomic mutations: the log measures the storage engine, not the transaction layer over it.</remarks>
	public enum KvOp : byte
	{

		Set = 0,

		Clear,

		ClearRange,

		Get,

		GetRange,

		/// <summary>Batch boundary. The engine is copy-on-write with generations, so where the commits fall decides how much page copying is amortized.</summary>
		Commit,

	}

	/// <summary>One step of a replay log.</summary>
	/// <remarks>Keys and values are materialized <see cref="Slice"/>s, so a timed loop never pays for a generator.</remarks>
	public readonly record struct KvStep
	{

		public required KvOp Op { get; init; }

		public Slice Key { get; init; }

		public Slice Value { get; init; }

		/// <summary>Exclusive upper bound for <see cref="KvOp.ClearRange"/> and <see cref="KvOp.GetRange"/>.</summary>
		public Slice EndKey { get; init; }

		public int Limit { get; init; }

		/// <summary>Scan backwards from <see cref="EndKey"/> instead of forwards from <see cref="Key"/>.</summary>
		public bool Reverse { get; init; }

		/// <summary>Touch the values during a scan. False measures reaching the keys only.</summary>
		public bool ReadValues { get; init; }

		public static KvStep Set(Slice key, Slice value) => new() { Op = KvOp.Set, Key = key, Value = value };

		public static KvStep Clear(Slice key) => new() { Op = KvOp.Clear, Key = key };

		public static KvStep ClearRange(Slice begin, Slice end) => new() { Op = KvOp.ClearRange, Key = begin, EndKey = end };

		public static KvStep Get(Slice key) => new() { Op = KvOp.Get, Key = key };

		public static KvStep GetRange(Slice begin, Slice end, int limit, bool reverse = false, bool readValues = true)
			=> new() { Op = KvOp.GetRange, Key = begin, EndKey = end, Limit = limit, Reverse = reverse, ReadValues = readValues };

		public static KvStep Commit() => new() { Op = KvOp.Commit };

	}

	/// <summary>A named, fully materialized operation log that every engine replays identically.</summary>
	public sealed class Workload
	{

		public required string Name { get; init; }

		/// <summary>Report grouping: insert, replace, delete, read, range, mixed.</summary>
		public required string Family { get; init; }

		public required string Shape { get; init; }

		/// <summary>Brings the store to the state the measurement runs against. Replayed but never timed.</summary>
		public KvStep[] Prepare { get; init; } = [];

		public required KvStep[] Steps { get; init; }

		/// <summary>Store operations in <see cref="Steps"/>, excluding commits: the denominator of ns/op.</summary>
		public int Ops
		{
			get
			{
				int n = 0;
				foreach (var s in this.Steps)
				{
					if (s.Op != KvOp.Commit)
					{
						++n;
					}
				}
				return n;
			}
		}

		/// <summary>One sentence saying what the timed section does.</summary>
		public string Description { get; init; } = "";

		/// <summary>True when <see cref="Steps"/> writes nothing: the store is identical after every pass, so a measurement may prepare it once and re-run only the timed section.</summary>
		public bool ReadOnlySteps
		{
			get
			{
				foreach (var s in this.Steps)
				{
					if (s.Op is KvOp.Set or KvOp.Clear or KvOp.ClearRange or KvOp.Commit)
					{
						return false;
					}
				}
				return true;
			}
		}

		public override string ToString() => $"{this.Name}  [{this.Shape}]";

	}

	/// <summary>How keys are shaped, which decides whether prefix compression has anything to work on.</summary>
	public enum KeyShape
	{

		/// <summary>36 B: a 32-byte shared directory run then a 4-byte big-endian counter, the shape of an index key.</summary>
		Index = 0,

		/// <summary>16 B: a bare big-endian counter, so the shared prefix of a page stays near zero.</summary>
		Flat,

		/// <summary>36 B: shared run then scrambled bytes. Insert order becomes random and nothing compresses.</summary>
		Scattered,

	}

	/// <summary>Order in which keys are touched.</summary>
	public enum AccessOrder
	{

		/// <summary>Ascending: right-edge append for writes, prefetch-friendly for reads.</summary>
		Sequential = 0,

		/// <summary>Uniform random: the worst case for splits and copy-on-write.</summary>
		Random,

		/// <summary>Large prime stride: covers the space while defeating sequential prefetch.</summary>
		Stride,

		/// <summary>80% of touches on 20% of keys, the shape of real traffic.</summary>
		Skewed,

	}

	/// <summary>Pre-built key set, built once per workload and shared between prepare and replay.</summary>
	public sealed class KeySet
	{

		private static ReadOnlySpan<byte> DirectoryRun => "\xFE/acme/tenant-0042/orders/idx/x"u8;

		public KeySet(KeyShape shape, int count)
		{
			Contract.Positive(count);
			this.Shape = shape;
			this.Keys = new Slice[count];
			int size = shape == KeyShape.Flat ? 16 : 36;
			for (int i = 0; i < count; i++)
			{
				var key = new byte[size];
				if (shape == KeyShape.Flat)
				{
					BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(8), i);
				}
				else
				{
					var run = DirectoryRun;
					for (int j = 0; j < 32; j++) key[j] = run[j % run.Length];
					BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(32), shape == KeyShape.Scattered ? Scramble(i) : i);
				}
				this.Keys[i] = key.AsSlice();
			}
			if (shape == KeyShape.Scattered) Array.Sort(this.Keys, static (a, b) => a.Span.SequenceCompareTo(b.Span));
		}

		public KeyShape Shape { get; }

		private Slice[] Keys { get; }

		public int Count => this.Keys.Length;

		/// <summary>Key at rank <paramref name="i"/> in sorted order, so range bounds are meaningful.</summary>
		public Slice this[int i] => this.Keys[i];

		/// <summary>Reversible 32-bit mix: unique but unordered, so insert order is unrelated to key order.</summary>
		private static int Scramble(int i)
		{
			uint x = (uint) i;
			x ^= x >> 16; x *= 0x7FEB352D;
			x ^= x >> 15; x *= 0x846CA68B;
			x ^= x >> 16;
			return (int) (x & 0x7FFFFFFF);
		}

		/// <summary>Maps operation <paramref name="n"/> to a rank under the chosen access order.</summary>
		public int Pick(AccessOrder order, int n, Random rnd)
			=> order switch
			{
				AccessOrder.Sequential => n % this.Count,
				AccessOrder.Random => rnd.Next(this.Count),
				AccessOrder.Stride => (int) ((long) n * 7919 % this.Count),
				AccessOrder.Skewed => rnd.Next(5) < 4 ? rnd.Next(Math.Max(1, this.Count / 5)) : rnd.Next(this.Count),
				_ => n % this.Count,
			};

	}

	/// <summary>Builds the individual workloads of the matrix.</summary>
	public static class Workloads
	{

		public static Slice Value(int size, int seed)
		{
			if (size == 0) return Slice.Empty;
			var v = new byte[size];
			new Random(seed).NextBytes(v);
			return v.AsSlice();
		}

		/// <summary>Every key inserted once, in <paramref name="order"/>, committing every <paramref name="batch"/>.</summary>
		private static KvStep[] Seed(KeySet keys, int valueSize, AccessOrder order, int batch)
		{
			var steps = new List<KvStep>(keys.Count + keys.Count / batch + 2);
			var value = Value(valueSize, 1);
			var rnd = new Random(1);
			// a permutation, so every key lands exactly once even when the order is random
			var ranks = new int[keys.Count];
			for (int i = 0; i < ranks.Length; i++) ranks[i] = i;
			if (order != AccessOrder.Sequential)
			{
				for (int i = ranks.Length - 1; i > 0; i--) { int j = rnd.Next(i + 1); (ranks[i], ranks[j]) = (ranks[j], ranks[i]); }
			}
			for (int i = 0; i < ranks.Length; i++)
			{
				steps.Add(KvStep.Set(keys[ranks[i]], value));
				if ((i + 1) % batch == 0) steps.Add(KvStep.Commit());
			}
			steps.Add(KvStep.Commit());
			return steps.ToArray();
		}

		public static Workload Insert(string name, KeyShape shape, int count, int valueSize, AccessOrder order, int batch)
		{
			var keys = new KeySet(shape, count);
			return new Workload
			{
				Name = name,
				Family = "insert",
				Shape = $"{count:N0} x {valueSize}B, {shape}, {order}, batch {batch:N0}",
				Description = $"Inserts {count:N0} fresh {valueSize} B values under {order} {shape} keys into an empty store, committing every {batch:N0}.",
				Steps = Seed(keys, valueSize, order, batch),
			};
		}

		/// <summary>Overwrites existing keys. <paramref name="delta"/> 0 is the same-length in-place case.</summary>
		public static Workload Replace(string name, KeyShape shape, int keyCount, int replaces, int valueSize, int delta, AccessOrder order, int batch)
		{
			var keys = new KeySet(shape, keyCount);
			var steps = new List<KvStep>(replaces + replaces / batch + 2);
			var replacement = Value(Math.Max(0, valueSize + delta), 2);
			var rnd = new Random(7);
			for (int i = 0; i < replaces; i++)
			{
				steps.Add(KvStep.Set(keys[keys.Pick(order, i, rnd)], replacement));
				if ((i + 1) % batch == 0) steps.Add(KvStep.Commit());
			}
			steps.Add(KvStep.Commit());
			return new Workload
			{
				Name = name,
				Family = "replace",
				Shape = $"{replaces:N0} over {keyCount:N0}, {valueSize}B{(delta == 0 ? " same-len" : delta > 0 ? $" +{delta}B" : $" {delta}B")}, {order}, batch {batch:N0}",
				Description = $"Overwrites {replaces:N0} existing values ({(delta == 0 ? "same length" : delta > 0 ? $"growing {delta} B" : $"shrinking {-delta} B")}) in {order} order over a seeded {keyCount:N0}-key store, committing every {batch:N0}.",
				Prepare = Seed(keys, valueSize, AccessOrder.Sequential, 100_000),
				Steps = steps.ToArray(),
			};
		}

		public static Workload Delete(string name, KeyShape shape, int keyCount, int deletes, int valueSize, AccessOrder order, int batch)
		{
			var keys = new KeySet(shape, keyCount);
			var steps = new List<KvStep>(deletes + deletes / batch + 2);
			var rnd = new Random(11);
			var seen = new HashSet<int>();
			for (int i = 0; i < deletes; i++)
			{
				int k = keys.Pick(order, i, rnd);
				if (!seen.Add(k)) continue; // removing an absent key measures a different path
				steps.Add(KvStep.Clear(keys[k]));
				if (steps.Count % batch == 0) steps.Add(KvStep.Commit());
			}
			steps.Add(KvStep.Commit());
			return new Workload
			{
				Name = name,
				Family = "delete",
				Shape = $"{deletes:N0} of {keyCount:N0}, {valueSize}B, {order}, batch {batch:N0}",
				Description = $"Deletes single keys in {order} order out of a seeded {keyCount:N0}-key store of {valueSize} B values, committing every {batch:N0}.",
				Prepare = Seed(keys, valueSize, AccessOrder.Sequential, 100_000),
				Steps = steps.ToArray(),
			};
		}

		public static Workload ClearRanges(string name, KeyShape shape, int keyCount, int ranges, int span, int valueSize)
		{
			var keys = new KeySet(shape, keyCount);
			var steps = new List<KvStep>(ranges * 2 + 2);
			var rnd = new Random(13);
			for (int i = 0; i < ranges; i++)
			{
				int start = rnd.Next(Math.Max(1, keyCount - span));
				steps.Add(KvStep.ClearRange(keys[start], keys[start + span]));
				steps.Add(KvStep.Commit());
			}
			return new Workload
			{
				Name = name,
				Family = "delete",
				Shape = $"{ranges:N0} ranges x {span:N0} of {keyCount:N0}, {valueSize}B",
				Description = $"Clears {ranges:N0} random {span:N0}-key ranges from a seeded {keyCount:N0}-key store, one commit per range (one op = one whole range).",
				Prepare = Seed(keys, valueSize, AccessOrder.Sequential, 100_000),
				Steps = steps.ToArray(),
			};
		}

		public static Workload Read(string name, KeyShape shape, int keyCount, int probes, int valueSize, AccessOrder order)
		{
			var keys = new KeySet(shape, keyCount);
			var steps = new KvStep[probes];
			var rnd = new Random(17);
			for (int i = 0; i < probes; i++) steps[i] = KvStep.Get(keys[keys.Pick(order, i, rnd)]);
			return new Workload
			{
				Name = name,
				Family = "read",
				Shape = $"{probes:N0} probes over {keyCount:N0} x {valueSize}B, {order}",
				Description = $"Point-reads {probes:N0} keys in {order} order against a seeded {keyCount:N0}-key store of {valueSize} B values.",
				Prepare = Seed(keys, valueSize, AccessOrder.Sequential, 100_000),
				Steps = steps,
			};
		}

		public static Workload Scan(string name, KeyShape shape, int keyCount, int scans, int span, int valueSize, bool reverse, bool readValues)
		{
			var keys = new KeySet(shape, keyCount);
			var steps = new KvStep[scans];
			var rnd = new Random(19);
			for (int i = 0; i < scans; i++)
			{
				int start = rnd.Next(Math.Max(1, keyCount - span - 1));
				steps[i] = KvStep.GetRange(keys[start], keys[start + span], span, reverse, readValues);
			}
			return new Workload
			{
				Name = name,
				Family = "range",
				Shape = $"{scans:N0} x {span:N0} rows over {keyCount:N0} x {valueSize}B{(reverse ? ", reverse" : "")}{(readValues ? ", +value" : ", keys only")}",
				Description = $"Runs {scans:N0} {(reverse ? "reverse " : "")}range scans of {span:N0} rows each ({(readValues ? "keys and values" : "keys only")}) at random starts over a seeded {keyCount:N0}-key store (one op = one whole scan).",
				Prepare = Seed(keys, valueSize, AccessOrder.Sequential, 100_000),
				Steps = steps,
			};
		}

		/// <summary>Append at the right edge, update a recent window a few times, then leave the record alone: the shape of a job ledger.</summary>
		public static Workload Ledger(string name, KeyShape shape, int records, int updates, int window, int valueSize, int growth, int batch)
		{
			var keys = new KeySet(shape, records);
			var steps = new List<KvStep>(records * (1 + updates) + records / batch + 2);
			var initial = Value(valueSize, 1);
			var grown = Value(valueSize + growth, 2);
			var rnd = new Random(23);
			int op = 0;
			for (int i = 0; i < records; i++)
			{
				steps.Add(KvStep.Set(keys[i], initial));
				++op;
				for (int u = 0; u < updates; u++)
				{
					int lo = Math.Max(0, i - window);
					steps.Add(KvStep.Set(keys[lo + rnd.Next(i - lo + 1)], grown));
					++op;
				}
				if (op % batch == 0) steps.Add(KvStep.Commit());
			}
			steps.Add(KvStep.Commit());
			return new Workload
			{
				Name = name,
				Family = "mixed",
				Shape = $"{records:N0} records, {updates} updates each in a {window:N0} window, {valueSize}B +{growth}B, batch {batch:N0}",
				Description = $"Appends {records:N0} records at the right edge with {updates} updates each inside a trailing {window:N0}-record window, committing every {batch:N0}.",
				Steps = steps.ToArray(),
			};
		}

	}

	/// <summary>The workload matrix. The scale picks the sizes; the axes stay the same.</summary>
	public static class Suite
	{

		public enum Scale { Smoke, Standard, Large }

		/// <summary>Builds the 33 workloads of the matrix at the given scale.</summary>
		public static IReadOnlyList<Workload> Build(Scale scale)
		{
			(int keys, int doc, int ops, int batch) = scale switch
			{
				Scale.Smoke => (20_000, 5_000, 10_000, 5_000),
				Scale.Large => (2_000_000, 200_000, 500_000, 100_000),
				_ => (500_000, 50_000, 200_000, 100_000),
			};

			var w = new List<Workload>();

			// inserts: the value size is the axis the page layout answers to
			w.Add(Workloads.Insert("insert.seq.idx.v0", KeyShape.Index, keys, 0, AccessOrder.Sequential, batch));
			w.Add(Workloads.Insert("insert.seq.idx.v8", KeyShape.Index, keys, 8, AccessOrder.Sequential, batch));
			w.Add(Workloads.Insert("insert.seq.idx.v64", KeyShape.Index, keys, 64, AccessOrder.Sequential, batch));
			w.Add(Workloads.Insert("insert.seq.idx.v1k", KeyShape.Index, doc, 1024, AccessOrder.Sequential, batch));
			// the key shape is the axis prefix compression answers to
			w.Add(Workloads.Insert("insert.seq.flat.v8", KeyShape.Flat, keys, 8, AccessOrder.Sequential, batch));
			w.Add(Workloads.Insert("insert.seq.scatter.v8", KeyShape.Scattered, keys, 8, AccessOrder.Sequential, batch));
			// write order and batch size drive splits and copy-on-write
			w.Add(Workloads.Insert("insert.rand.idx.v8", KeyShape.Index, keys, 8, AccessOrder.Random, batch));
			w.Add(Workloads.Insert("insert.seq.idx.v8.batch1k", KeyShape.Index, keys / 5, 8, AccessOrder.Sequential, 1_000));
			// commit-heavy: about 1,000 commits of 100 random inserts, where the per-commit flush dominates
			w.Add(Workloads.Insert("insert.rand.idx.v8.batch100", KeyShape.Index, keys / 5, 8, AccessOrder.Random, 100));
			// the small-transaction shape: a handful of keys and 2 to 4 dirty pages per commit
			w.Add(Workloads.Insert("insert.rand.idx.v8.batch10", KeyShape.Index, keys / 25, 8, AccessOrder.Random, 10));

			// replaces
			w.Add(Workloads.Replace("replace.same.seq", KeyShape.Index, keys, ops, 8, 0, AccessOrder.Sequential, 10_000));
			w.Add(Workloads.Replace("replace.same.rand", KeyShape.Index, keys, ops, 8, 0, AccessOrder.Random, 10_000));
			w.Add(Workloads.Replace("replace.same.skew", KeyShape.Index, keys, ops, 8, 0, AccessOrder.Skewed, 10_000));
			w.Add(Workloads.Replace("replace.grow.rand", KeyShape.Index, keys, ops, 8, +8, AccessOrder.Random, 10_000));
			w.Add(Workloads.Replace("replace.shrink.rand", KeyShape.Index, keys, ops, 64, -32, AccessOrder.Random, 10_000));
			w.Add(Workloads.Replace("replace.same.v1k", KeyShape.Index, doc, ops / 10, 1024, 0, AccessOrder.Random, 1_000));

			// deletes
			w.Add(Workloads.Delete("delete.rand.idx.v8", KeyShape.Index, keys, ops, 8, AccessOrder.Random, 10_000));
			w.Add(Workloads.Delete("delete.seq.idx.v8", KeyShape.Index, keys, ops, 8, AccessOrder.Sequential, 10_000));
			w.Add(Workloads.ClearRanges("delete.range.idx.v8", KeyShape.Index, keys, Math.Max(10, ops / 2_000), 1_000, 8));

			// point reads: the order is the axis; the value size decides whether the value area is touched
			w.Add(Workloads.Read("read.seq.idx.v8", KeyShape.Index, keys, ops, 8, AccessOrder.Sequential));
			w.Add(Workloads.Read("read.rand.idx.v8", KeyShape.Index, keys, ops, 8, AccessOrder.Random));
			w.Add(Workloads.Read("read.stride.idx.v8", KeyShape.Index, keys, ops, 8, AccessOrder.Stride));
			w.Add(Workloads.Read("read.skew.idx.v8", KeyShape.Index, keys, ops, 8, AccessOrder.Skewed));
			w.Add(Workloads.Read("read.stride.idx.v1k", KeyShape.Index, doc, ops, 1024, AccessOrder.Stride));
			w.Add(Workloads.Read("read.stride.flat.v8", KeyShape.Flat, keys, ops, 8, AccessOrder.Stride));

			// range scans. The scan counts are what a timed section needs to last a few milliseconds.
			int scans = Math.Max(1_000, ops / 4);
			w.Add(Workloads.Scan("range.10.idx.v8", KeyShape.Index, keys, scans, 10, 8, false, true));
			w.Add(Workloads.Scan("range.1k.idx.v8", KeyShape.Index, keys, scans / 20, 1_000, 8, false, true));
			// the same scan, with every timed pass checking page checksums again on first touch (fdblite only):
			// the cost a reopened store pays, next to the base leg's already-checked path
			w.Add(Workloads.Scan("range.1k.idx.v8.verify", KeyShape.Index, keys, scans / 20, 1_000, 8, false, true));
			w.Add(Workloads.Scan("range.1k.idx.v8.keysonly", KeyShape.Index, keys, scans / 20, 1_000, 8, false, false));
			w.Add(Workloads.Scan("range.1k.idx.v8.reverse", KeyShape.Index, keys, scans / 20, 1_000, 8, true, true));
			w.Add(Workloads.Scan("range.1k.idx.v1k", KeyShape.Index, doc, scans / 20, 1_000, 1024, false, true));
			// full-store sweep (span = keys - 2: the generator indexes keys[start + span], so -2 pins start to 0)
			w.Add(Workloads.Scan("range.full.idx.v8", KeyShape.Index, keys, Math.Max(10, scans / 2000), keys - 2, 8, false, true));

			// the real-world shape
			w.Add(Workloads.Ledger("ledger.append.churn", KeyShape.Index, keys / 5, 3, 10_000, 64, +16, batch));

			return w;
		}

	}

}
