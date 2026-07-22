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

#if !NETFRAMEWORK

// ReSharper disable AccessToModifiedClosure
// ReSharper disable ConvertToLocalFunction
// ReSharper disable StringLiteralTypo

namespace FoundationDB.Layers.Experimental.Indexing.Tests
{
	using System.Numerics;
	using MathNet.Numerics.Distributions;

	[TestFixture]
	[Category("LongRunning")]
	public class CompressedBitmapsFacts : FdbTest
	{

		[Test]
		public void Test_EmptyBitmap()
		{
			// the empty bitmap shouldn't have any words
			var empty = CompressedBitmap.Empty;
			Assert.That(empty, Is.Not.Null.And.Count.EqualTo(0));

			//REVIEW: what should the bounds be for an empty bitmap?
			Assert.That(empty.Bounds.Lowest, Is.EqualTo(0), "empty.Bounds.Lowest");
			Assert.That(empty.Bounds.Highest, Is.EqualTo(-1), "empty.Bounds.Highest");
			Assert.That(empty.Bounds.IsEmpty, Is.True, "empty.Bounds.IsEmpty");

			// all bits should be unset
			Assert.That(empty.Test(0), Is.False);
			Assert.That(empty.Test(31), Is.False);
			Assert.That(empty.Test(32), Is.False);
			Assert.That(empty.Test(1234), Is.False);

			// binary representation should be 0 bytes
			var packed = empty.ToSlice();
			Assert.That(packed, Is.EqualTo(Slice.Empty));
		}

		private static void Verify(ref CompressedBitmapBuilder builder, SuperSlowUncompressedBitmap witness, string label)
		{
			var bmpBuilder = builder.ToBitmap();
			var bmpWitness = witness.ToBitmap();
			Log($"> B: {bmpBuilder.Bounds,12} ({bmpBuilder.CountBits(),3}) {bmpBuilder.ToSlice().ToHexString()}");
			Log($"> W: {bmpWitness.Bounds,12} ({bmpWitness.CountBits(),3}) {bmpWitness.ToSlice().ToHexString()}");
			var rawBuilder = builder.ToBooleanArray();
			var rawWitness = witness.ToBooleanArray();
			Log("> B: " + bmpBuilder.Dump());
			Log("> W: " + bmpWitness.Dump());

			var a = SuperSlowUncompressedBitmap.Dump(rawBuilder).ToString().Split('\n');
			var b = SuperSlowUncompressedBitmap.Dump(rawWitness).ToString().Split('\n');

			Log(string.Join("\n", a.Zip(b, (x, y) => (x == y ? "= " : "##") + x + "\n  " + y)));

			if (!rawBuilder.SequenceEqual(rawWitness))
			{
				Assert.That(rawBuilder, Is.EqualTo(rawWitness), $"Uncompressed bitmap does not match {label}");
			}

			if (bmpBuilder.Bounds.Lowest != bmpWitness.Bounds.Lowest)
			{
				Assert.That(bmpBuilder.Bounds.Lowest, Is.EqualTo(bmpWitness.Bounds.Lowest), $"Lowest bound does not match: {label}");
			}

			if (bmpBuilder.Bounds.Highest != bmpWitness.Bounds.Highest)
			{
				Assert.That(bmpBuilder.Bounds.Highest, Is.EqualTo(bmpWitness.Bounds.Highest), $"Lowest bound does not match: {label}");
			}
		}

		private static bool SetBitAndVerify(ref CompressedBitmapBuilder builder, SuperSlowUncompressedBitmap witness, int offset)
		{
			Log();
			Log($"Set({offset}):");
			bool actual = builder.Set(offset);
			bool expected = witness.Set(offset);
			if (actual != expected)
			{
				Assert.That(actual, Is.EqualTo(expected), $"Set({offset})");
			}

			Verify(ref builder, witness, $"SetBitAndVerify({offset})");
			return actual;
		}

		private static bool ClearBitAndVerify(ref CompressedBitmapBuilder builder, SuperSlowUncompressedBitmap witness, int offset)
		{
			Log();
			Log($"Clear({offset}):");
			bool actual = builder.Clear(offset);
			bool expected = witness.Clear(offset);
			if (actual != expected)
			{
				Assert.That(actual, Is.EqualTo(expected), $"Clear({offset})");
			}

			Verify(ref builder, witness, $"ClearBitAndVerify({offset})");
			return actual;
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Set_Bits()
		{
			// start with an empty bitmap
			var builder = CompressedBitmap.Empty.ToBuilder();
			Assert.That(builder, Is.Not.Null.And.Count.EqualTo(0));
			var witness = new SuperSlowUncompressedBitmap();

			Assert.That(SetBitAndVerify(ref builder, witness, 0), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 17), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 17), Is.False);
			Assert.That(SetBitAndVerify(ref builder, witness, 31), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 1234), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 777), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 62), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 774), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 124), Is.True);
			Assert.That(SetBitAndVerify(ref builder, witness, 93), Is.True);
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Clear_Bits()
		{
			var builder = CompressedBitmap.Empty.ToBuilder();
			Assert.That(builder, Is.Not.Null.And.Count.EqualTo(0));
			var witness = new SuperSlowUncompressedBitmap();

			// clearing anything in the empty bitmap is a no-op
			Assert.That(ClearBitAndVerify(ref builder, witness, 0), Is.False);
			Assert.That(ClearBitAndVerify(ref builder, witness, 42), Is.False);
			Assert.That(ClearBitAndVerify(ref builder, witness, int.MaxValue), Is.False);

			Assert.That(SetBitAndVerify(ref builder, witness, 42), Is.True);
			Assert.That(ClearBitAndVerify(ref builder, witness, 42), Is.True, "Clear just after set");
			Assert.That(ClearBitAndVerify(ref builder, witness, 42), Is.False, "Clear just after clear");

		}

		[Test]
		public void Test_CompressedBitmapBuilder_Linear_Sets()
		{
			// for each bit, from 0 to N-1, set it with proability P
			// => this test linear insertion, that always need to patch or append at the end of the bitmap

			var builder = CompressedBitmap.Empty.ToBuilder();
			var witness = new SuperSlowUncompressedBitmap();

			const int N = 5 * 1000;
			const int P = 100;

			var rnd = new Random(12345678);
			for (int i = 0; i < N; i++)
			{
				if (rnd.Next(P) == 42)
				{
					SetBitAndVerify(ref builder, witness, rnd.Next(N));
				}
			}
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Random_Sets()
		{
			// randomly set K bits in a set of N possible bits (with possible overlap)
			// => this test random insertions that need to modify the inside of a bitmap

			var builder = CompressedBitmap.Empty.ToBuilder();
			var witness = new SuperSlowUncompressedBitmap();

			const int N = 5 * 1000;
			const int K = 100;

			var rnd = new Random(12345678);
			for (int i = 0; i < K; i++)
			{
				SetBitAndVerify(ref builder, witness, rnd.Next(N));
			}
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Random_Sets_And_Clears()
		{
			// randomly alternate between setting and clearing random bits

			const int K = 20;
			const int S = 100;
			const int C = 100;
			const int N = 5_000;

			var bmp = CompressedBitmap.Empty;

			var witness = new SuperSlowUncompressedBitmap();

			var rnd = new Random(12345678);
			for (int k = 0; k < K; k++)
			{
				Log($"### Generation {k}");

				// convert to builder
				var builder = bmp.ToBuilder();
				Verify(ref builder, witness, "bmp.ToBuilder()");

				// set S bits
				for (int i = 0; i < S; i++)
				{
					int p = rnd.Next(N);
					//SetBitAndVerify(builder, witness, p);
					builder.Set(p);
					witness.Set(p);
				}

				// clear C bits
				for (int i = 0; i < C; i++)
				{
					int p = rnd.Next(N);
					if (k == 2 && i == 0) p = 39;

					//ClearBitAndVerify(builder, witness, p);
					builder.Clear(p);
					witness.Clear(p);
				}

				// pack back to bitmap
				bmp = builder.ToBitmap();
				Log();
				Log($"> Result of gen #{k}: {bmp.Dump()}");
				Log($"> {bmp.ToSlice().ToHexString()}");
				Log();

				builder.Dispose();
			}
		}

		[Test]
		public void Test_WordAlignHybridEncoder_CompressTo()
		{
			var rnd = new Random();

			void Compress(Slice input)
			{
				Log($"IN  [{input.Count}]");
				DumpHexa(input);

				var writer = new CompressedBitmapWriter();
				int r = WordAlignHybridEncoder.CompressTo(input, writer);

				using var compressed = writer.GetBuffer();
				Log($"OUT [{compressed.Count}] => [r={r}]");
				DumpHexa(compressed.Data);
				Log(WordAlignHybridEncoder.DumpCompressed(compressed.Data).ToString());
				Log();
			}

			Compress(Slice.FromString("This is a test of the emergency broadcast system"));

			// all zeroes (multiple of 31 bits)
			Compress(Slice.Repeat(0, 62));
			// all zeroes (with padding)
			Compress(Slice.Repeat(0, 42));

			// all ones (multiple of 31 bits)
			Compress(Slice.Repeat(255, 62));
			// all ones (with padding)
			Compress(Slice.Repeat(255, 42));

			// random stuff (multiple of 31 bits)
			Compress(Slice.Random(rnd, 42));
			// random stuff (with padding)
			Compress(Slice.Random(rnd, 42));

			// mostly zeroes
			void SetBit(byte[] b, int p)
			{
				b[p >> 3] |= (byte) (1 << (p & 7));
			}

			byte[] MostlyZeroes(int count)
			{
				var buf = new byte[1024];
				for (int i = 0; i < count; i++)
				{
					SetBit(buf, rnd.Next(buf.Length * 8));
				}

				Log($"Mostly zeroes: {count}");
				return buf;
			}

			Compress(MostlyZeroes(1).AsSlice());
			Compress(MostlyZeroes(10).AsSlice());
			Compress(MostlyZeroes(42).AsSlice());
			Compress(MostlyZeroes(100).AsSlice());


			// mostly ones
			void ClearBit(byte[] b, int p)
			{
				b[p >> 3] &= (byte) ~(1 << (p & 7));
			}

			byte[] MostlyOnes(int count)
			{
				var buf = new byte[1024];
				for (int i = 0; i < buf.Length; i++) buf[i] = 0xFF;
				for (int i = 0; i < 10; i++)
				{
					ClearBit(buf, rnd.Next(buf.Length * 8));
				}

				Log($"Mostly ones: {count}");
				return buf;
			}

			Compress(MostlyOnes(1).AsSlice());
			Compress(MostlyOnes(10).AsSlice());
			Compress(MostlyOnes(42).AsSlice());
			Compress(MostlyOnes(100).AsSlice());

			// progressive
			bool TestBit(byte[] b, int p) => (b[p >> 3] & (1 << (p & 7))) != 0;

			const int VALUES = 8192;
			var buffer = new byte[VALUES / 8];
			var output = new CompressedBitmapWriter();
			WordAlignHybridEncoder.CompressTo(buffer.AsSlice(), output);
			Log($"{0}\t{output.Length}\t1024");
			for (int i = 0; i < VALUES / 8; i++)
			{
				int p;
				do
				{
					p = rnd.Next(VALUES);
				}
				while (TestBit(buffer, p));

				SetBit(buffer, p);

				output.Reset();
				WordAlignHybridEncoder.CompressTo(buffer.AsSlice(), output);
				Log($"{1.0d * (i + 1) / VALUES}\t{output.Length}\t1024");
			}

		}

		private sealed record Character
		{
			public required int Id { get; init; }
			public required string Name { get; init; } // will mostly be used for sorting
			public required string Gender { get; init; } // poor man's enum with 49%/49%/1% distribution
			public required string Job { get; init; } // regular enum with random distribution
			public DateTime? Born { get; init; } // accurate to the day, usually used in range queries
			public bool Dead { get; init; } // zomg, spoilers ahead! probably used as an exclusion flag (like IsDeleted)

		}

		public class MemoryIndex<TKey> where TKey : notnull
		{
			public readonly Dictionary<TKey, CompressedBitmap> Values;
			public readonly Dictionary<TKey, int> Statistics;

			public MemoryIndex(IEqualityComparer<TKey>? comparer = null)
			{
				comparer ??= EqualityComparer<TKey>.Default;
				this.Values = new(comparer);
				this.Statistics = new(comparer);
			}

			public CompressedBitmap? Lookup(TKey value)
			{
				return this.Values.GetValueOrDefault(value);
			}

			public int Count(TKey value)
			{
				return this.Statistics.GetValueOrDefault(value, 0);
			}

			public double Frequency(TKey value)
			{
				return (double) Count(value) / this.Statistics.Values.Sum();
			}
		}

		private static Action<TDoc> MakeInserter<TDoc, TKey>(MemoryIndex<TKey> index, Func<TDoc, int> idFunc, Func<TDoc, TKey> keyFunc) where TKey : notnull
		{
			return (doc) =>
			{
				int docId = idFunc(doc);
				TKey indexedValue = keyFunc(doc);
				int count;
				if (!index.Values.TryGetValue(indexedValue, out var bmp))
				{
					bmp = CompressedBitmap.Empty;
					count = 0;
				}
				else
				{
					count = index.Statistics[indexedValue];
				}

				var builder = bmp.ToBuilder();
				builder.Set(docId);
				index.Values[indexedValue] = builder.ToBitmap();
				index.Statistics[indexedValue] = count + 1;
			};
		}

		private static string MakeHeatMap(int[] map)
		{
			int max = map.Max();
			const string SCALE = "`.:;+=xX$&#";
			double r = (double)(SCALE.Length - 1) / max;
			var chars = new char[map.Length];
			for (int i = 0; i < map.Length; i++)
			{
				if (map[i] == 0)
					chars[i] = '\xA0';
				else
					chars[i] = SCALE[(int)Math.Round(r * map[i], MidpointRounding.AwayFromZero)];
			}
			return new(chars);
		}

		private static void DumpIndex<TKey, TVal>(string label, MemoryIndex<TKey> index, Func<TKey, int, TVal> orderBy, IComparer<TVal>? comparer = null, bool heatMaps = false) where TKey : notnull
		{
			comparer ??= Comparer<TVal>.Default;

			int total = index.Statistics.Values.Sum();
			long totalLegacy = 0;
			int[] map = new int[100];
			double r = (double)(map.Length - 1) / total;
			Log($"__{label}__");
			Log("| Indexed Value           |  Count | Total % | Words |  Lit%  | 1-Bits |  Word% |   Bitmap | ratio % |   Legacy  | ratio % |" + (heatMaps ? " HeatMap |" : ""));
			Log("|:------------------------|-------:|--------:|------:|-------:|-------:|-------:|---------:|--------:|----------:|--------:|" + (heatMaps ? ":-----------------------------------------------------------------------|" : ""));
			foreach (var kv in index.Values.OrderBy((kv) => orderBy(kv.Key, index.Count(kv.Key)), comparer))
			{
				var t = STuple.Create(kv.Key);
				var tk = TuPack.Pack(in t);

				kv.Value.GetStatistics(out int bits, out int words, out int literals, out int _, out double ratio);

				long legacyIndexSize = 0; // size estimate of a regular FDB index (..., "Value", GUID) = ""
				Array.Clear(map, 0, map.Length);
				foreach(var p in kv.Value.GetView())
				{
					map[(int)(r * p)]++;
					legacyIndexSize += 3 + tk.Count + 17;
				}
				totalLegacy += legacyIndexSize;

				int bytes = kv.Value.ToSlice().Count;

				Log(string.Format(
					CultureInfo.InvariantCulture,
					"| {0,-24}| {1,6:N0} | {2,6:N2}% | {3,5:N0} | {4,5:N1}% | {5,6:N0} | {6,6:N2} | {7,8:N0} | {8,6:N2}% | {9,9:N0} | {10,6:N2}% |" + (heatMaps ? " `{11}` |" : ""),
					/*0*/ t,
					/*1*/ index.Count(kv.Key),
					/*2*/ 100.0 * index.Frequency(kv.Key),
					/*3*/ words,
					/*4*/ (100.0 * literals) / words,
					/*5*/ bits,
					/*6*/ 1.0 * bits / words,
					/*7*/ bytes,
					/*8*/ 100.0 * ratio,
					/*9*/ legacyIndexSize,
					/*A*/ (100.0 * bytes) / legacyIndexSize,
					/*B*/ heatMaps ? MakeHeatMap(map) : ""
				));
			}

			Log(string.Format(
				CultureInfo.InvariantCulture,
				"> {0:N0} distinct value(s), {1:N0} document(s), {2:N0} bitmap bytes, {3:N0} legacy bytes",
				index.Values.Count,
				total,
				index.Values.Values.Sum(x => x.ByteCount),
				totalLegacy
			));
		}

		private static void DumpIndexQueryResult(Dictionary<long, Character> characters, CompressedBitmap bitmap)
		{
			foreach (var docId in bitmap.GetView())
			{
				Assert.That(characters.TryGetValue(docId, out var character), Is.True);

				Log($"- {docId}: {character!.Name} {(character.Gender == "Male" ? "\u2642" : character.Gender == "Female" ? "\u2640" : character.Gender)}{(character.Dead ? " (\u271D)" : "")}");
			}
		}

		[Test]
		public void Test_Merging_Multiple_Bitmaps()
		{
			List<Character> dataSet =
			[
				new() { Id = 1, Name = "Spike Spiegel", Gender = "Male", Job = "Bounty_Hunter", Born = new DateTime(2044, 6, 26), Dead = true /* bang! */ },
				new() { Id = 2, Name = "Jet Black", Gender = "Male", Job = "Bounty_Hunter", Born = new DateTime(2035, 12, 13) },
				new() { Id = 3, Name = "Faye Valentine", Gender = "Female", Job = "Bounty_Hunter", Born = new DateTime(1994, 8, 14) },
				new() { Id = 4, Name = "Edward Wong Hau Pepelu Tivruski IV", Gender = "Female", Job = "Hacker", Born = new DateTime(2058, 1, 1) },
				new() { Id = 5, Name = "Ein", Gender = "Male", Job = "Dog" },
				new() { Id = 6, Name = "Vicious", Gender = "Male", Job = "Vilain", Dead = true },
				new() { Id = 7, Name = "Julia", Gender = "Female", Job = "Damsel_In_Distress", Dead = true /* It's all a dream */ },
				new() { Id = 8, Name = "Victoria Tepsichore", Gender = "Female", Job = "Space_Trucker" },
				new() { Id = 9, Name = "Punch", Gender = "Male", Job = "TV_Host" },
				new() { Id = 10, Name = "Judy", Gender = "Female", Job = "TV_Host" }
			];

			// poor man's in memory database
			var database = new Dictionary<long, Character>();
			var indexByGender = new MemoryIndex<string>(StringComparer.OrdinalIgnoreCase);
			var indexByJob = new MemoryIndex<string>(StringComparer.OrdinalIgnoreCase);
			var indexOfTheDead = new MemoryIndex<bool>();

			// simulate building the indexes one document at a time
			var indexers = new[]
			{
				MakeInserter<Character, string>(indexByGender, (doc) => doc.Id, (doc) => doc.Gender),
				MakeInserter<Character, string>(indexByJob, (doc) => doc.Id, (doc) => doc.Job),
				MakeInserter<Character, bool>(indexOfTheDead, (doc) => doc.Id, (doc) => doc.Dead),
			};

			Log("Inserting into database...");
			foreach (var character in dataSet)
			{
				database[character.Id] = character;
				foreach (var indexer in indexers)
				{
					indexer(character);
				}
			}

			// dump the indexes
			Log();
			DumpIndex("Genders", indexByGender, (s, _) => s);

			Log();
			DumpIndex("Jobs", indexByJob, (s, _) => s);

			Log();
			DumpIndex("DeadOrAlive", indexOfTheDead, (s, _) => s);

			// "Où sont les femmes ?"
			Log();
			Log("indexByGender.Lookup('Female')");
			var females = indexByGender.Lookup("Female")!;
			Assert.That(females, Is.Not.Null);
			Log($"=> {females.Dump()}");
			DumpIndexQueryResult(database, females);

			// R.I.P
			Log();
			Log("indexOfTheDead.Lookup(dead: true)");
			var deadPeople = indexOfTheDead.Lookup(true)!;
			Assert.That(deadPeople, Is.Not.Null);
			Log($"=> {deadPeople.Dump()}");
			DumpIndexQueryResult(database, deadPeople);

			// combination of both
			Log();
			Log("indexByGender.Lookup('Female') AND indexOfTheDead.Lookup(dead: true)");
			var julia = females.And(deadPeople);
			Log($"=> {julia.Dump()}");
			DumpIndexQueryResult(database, julia);

			// the crew
			Log();
			Log("indexByJob.Lookup('Bounty_Hunter' OR 'Hacker' OR 'Dog')");

			var bitmaps = ((string[]) ["Bounty_Hunter", "Hacker", "Dog"]).Select(job => indexByJob.Lookup(job)!).ToList();

			CompressedBitmap? crew = null;
			foreach (var bmp in bitmaps)
			{
				crew = crew == null ? bmp : crew.Or(bmp);
			}
			crew ??= CompressedBitmap.Empty;
			Log($"=> {crew.Dump()}");
			DumpIndexQueryResult(database, crew);

		}

		[Test]
		public void Test_Logical_Binary_Operations()
		{
			var a = SuperSlowUncompressedBitmap.FromBitString("0101").ToBitmap();
			var b = SuperSlowUncompressedBitmap.FromBitString("0011").ToBitmap();

			var and = a.And(b);
			Assert.That(and, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(and).ToBitString(), Is.EqualTo("0001000000000000000000000000000"), "a AND b");

			var or  = a.Or(b);
			Assert.That(or, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(or).ToBitString(), Is.EqualTo("0111000000000000000000000000000"), "a OR b");

			var xor = a.Xor(b);
			Assert.That(xor, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(xor).ToBitString(), Is.EqualTo("0110000000000000000000000000000"), "a XOR b");

			var andNot = a.AndNot(b);
			Assert.That(andNot, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(andNot).ToBitString(), Is.EqualTo("0100000000000000000000000000000"), "a AND NOT b");

			var orNot = a.OrNot(b);
			Assert.That(orNot, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(orNot).ToBitString(), Is.EqualTo("1101111111111111111111111111111"), "a OR NOT b");

			var xorNot = a.XorNot(b);
			Assert.That(xorNot, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(xorNot).ToBitString(), Is.EqualTo("1001111111111111111111111111111"));

		}

		#region Coin Toss...

		public sealed class CoinToss
		{
			public const int HEAD = 1; // 49.5%
			public const int TAIL = 2; // 49.5%
			public const int EDGE = 0; //  1.0%

			/// <summary>Toss unique id (random guid)</summary>
			public Guid Id { get; set; }

			/// <summary>False if the toss was discarded as invalid</summary>
			public bool Valid { get; set; } // 99.9% true, 0.1% false

			/// <summary>True for head, False for tails, null for edge</summary>
			public int Result { get; set; }

			/// <summary>Number of completed 360° flips</summary>
			public int Flips { get; set; }

			/// <summary>Coin elevation (in cm)</summary>
			public double Elevation { get; set; }

			/// <summary>true for daytime, false for nighttime</summary>
			public bool Daytime { get; set; }

			/// <summary>Name of location where the toss was performed</summary>
			public string? Location { get; set; }
		}

		[Test]
		public void Test_Randomized_Data()
		{
			#region Data Generators...

			Random rnd = Random.Shared; // initialized later with the actual value!

			var dfFlips = new Cauchy(10, 4, rnd);
			Func<int> makeFlips = () =>
			{
				int x = 0;
				while (x <= 0 || x >= 30) { x = (int)Math.Floor(dfFlips.Sample()); }
				return x;
			};

			var dfElev = new Cauchy(10, 1, rnd);
			var makeElevation = () =>
			{
				double x = 0;
				while (x <= 0.0 || x >= 30) { x = dfElev.Sample(); }
				return x;
			};

			bool flipFlop = false;
			var makeFlipFlop = () =>
			{
				if (rnd.NextDouble() < 0.01)
				{
					flipFlop = !flipFlop;
				}
				return flipFlop;
			};

			var cities = new[]
			{ 
				"Paris", "Marseilles", "Lyon", "Toulouse", "Nice",
				"Nantes", "Strasbourg", "Montpellier", "Bordeaux", "Lille",
				"Rennes", "Reims", "Le Havre", "Saint-Étienne", "Toulon",
				"Grenoble", "Dijon", "Angers", "Saint-Denis", "Villeurbanne",
				"Nîmes", "Le Mans", "Clermont-Ferrand", "Aix-en-Provence", "Brest"
			};
			var dfLoc = new Cauchy(0, 1.25, rnd);
			var makeLocation = () =>
			{
				int x = cities.Length;
				while (x >= cities.Length) { x = (int)Math.Floor(Math.Abs(dfLoc.Sample())); }
				return cities[x];
			};
			var makeHeadsOrTails = () => rnd.NextDouble() < 0.01 ? CoinToss.EDGE : rnd.NextDouble() <= 0.5 ? CoinToss.HEAD : CoinToss.TAIL; // biased!
			var makeValid = () => rnd.Next(1000) != 666;

			#endregion

			//foreach (var N in new[] { 1000, 2000, 5000, 10 * 1000, 20 * 1000, 50 * 1000, 100 * 1000 })
			const int N = 10 * 1000;
			{
				Log("=================================================================================================================================================================================================================================");
				Log($"N = {N:N0}");
				Log("=================================================================================================================================================================================================================================");

				rnd = new(123456);

				var dataSet = Enumerable
					.Range(0, N)
					.Select(i => new KeyValuePair<int, CoinToss>(i, new()
					{
						Id = Guid.NewGuid(),
						Valid = makeValid(),
						Result = makeHeadsOrTails(),
						Flips = makeFlips(),
						Elevation = makeElevation(),
						Location = makeLocation(),
						Daytime = makeFlipFlop(),
					}))
					.ToList();

				var indexLoc = new MemoryIndex<string>(StringComparer.Ordinal);
				var indexValid = new MemoryIndex<bool>();
				var indexResult = new MemoryIndex<int>();
				var indexFlips = new MemoryIndex<int>();
				var indexElevation = new MemoryIndex<double>(); // quantized!
				var indexFlipFlop = new MemoryIndex<bool>();

				var inserters = new []
				{
					MakeInserter<KeyValuePair<int, CoinToss>, int>(indexResult, (kv) => kv.Key, (kv) => kv.Value.Result),
					MakeInserter<KeyValuePair<int, CoinToss>, bool>(indexValid, (kv) => kv.Key, (kv) => kv.Value.Valid),
					MakeInserter<KeyValuePair<int, CoinToss>, bool>(indexFlipFlop, (kv) => kv.Key, (kv) => kv.Value.Daytime),
					MakeInserter<KeyValuePair<int, CoinToss>, int>(indexFlips, (kv) => kv.Key, (kv) => kv.Value.Flips),
					MakeInserter<KeyValuePair<int, CoinToss>, double>(indexElevation, (kv) => kv.Key, (kv) => Math.Round(kv.Value.Elevation, 1, MidpointRounding.AwayFromZero)),
					MakeInserter<KeyValuePair<int, CoinToss>, string>(indexLoc, (kv) => kv.Key, (kv) => kv.Value.Location ?? string.Empty),
				};

				//var database = new Dictionary<int, CoinToss>();
				//Log("Inserting data: ...");
				foreach (var data in dataSet)
				{
					//if (database.Count % 1000 == 0) Log("\rInserting data: {0} / {1}", database.Count, N);
					//database[data.Key] = data.Value;
					foreach (var inserter in inserters)
					{
						inserter(data);
					}
				}
				//Log("\rInserting data: {0} / {1}", database.Count, N);

				Log();
				DumpIndex("Result", indexResult, (s, _) => s, heatMaps: true);

				Log();
				DumpIndex("Valid", indexValid, (s, _) => s, heatMaps: true);

				Log();
				DumpIndex("FlipFlops", indexFlipFlop, (s, _) => s, heatMaps: true);

				Log();
				DumpIndex("Flips", indexFlips, (s, _) => s, heatMaps: true);

				Log();
				DumpIndex("Location", indexLoc, (_, n) => -n, heatMaps: true);

				Log();
				DumpIndex("Elevation", indexElevation, (s, _) => s, heatMaps: true);

				//Log(indexValid.Values[true].Dump());
				//Log(indexValid.Values[true].ToSlice().ToHexString());
				Log();
				Log();
			}

		}

		#endregion

		[Test]
		[Category("LongRunning")]
		public void Test_BigBadIndexOfTheDead()
		{
			// simulate a dataset where 50,000 users create a stream of 10,000,000  events, with a non-uniform distribution, ie: few users making the bulk, and a long tail of mostly inactive users
#if DEBUG
			const int R = 3;
			const int N = 100_000;
			const int K = 500;
#else
			const int R = 5;
			const int N = 10_000_000;
			const int K = 50_000;
#endif

			var rnd = new Random(123456);
			#region create a non uniform random distribution for the users

			// step1: create a semi random distribution for the values
			Log($"Creating Probability Distribution Function for {K:N0} users...");
			var pk = new double[K];
			// step1: each gets a random score
			for (int i = 0; i < pk.Length; i++)
			{
				pk[i] = Math.Pow(rnd.NextDouble(), 10);
			}
			// then sort + reverse
			Array.Sort(pk); Array.Reverse(pk);
			// step2: spread
			double sum = 0;
			for(int i = 0; i < pk.Length;i++)
			{
				var s = pk[i];
				pk[i] = sum;
				sum += s;
			}
			// step3: normalize
			double r = (N - 1) / sum;
			for (int i = 0; i < pk.Length; i++)
			{
				pk[i] = Math.Floor(pk[i] * r);
			}
			sum = N;

			// step4: fudge the tail
			r = pk[^1];
			double delta = 1;
			for (int i = pk.Length - 2; i >= 0; i--)
			{
				if (pk[i] < r) break;
				r -= delta;
				delta *= 1.0001;
				pk[i] = r;
			}

			//for (int i = 0; i < pk.Length; i += 500)
			//{
			//	Log(pk[i].ToString("R", CultureInfo.InvariantCulture));
			//}

			int p25 = Array.BinarySearch(pk, 0.25 * sum); p25 = p25 < 0 ? ~p25 : p25;
			int p50 = Array.BinarySearch(pk, 0.50 * sum); p50 = p50 < 0 ? ~p50 : p50;
			int p75 = Array.BinarySearch(pk, 0.75 * sum); p75 = p75 < 0 ? ~p75 : p75;
			int p95 = Array.BinarySearch(pk, 0.95 * sum); p95 = p95 < 0 ? ~p95 : p95;
			Log($"> PDF: P25={100.0 * p25 / K:G2} %, P50={100.0 * p50 / K:G2} %, P75={100.0 * p75 / K:G2} %, P95={100.0 * p95 / K:G2} %");

			#endregion

			#region Create the random event dataset...

			// a user will be selected randomly, and will be able to produce a random number of consecutive events, until we reach the desired amount of events

			Log($"Creating dataset for {N:N0} documents...");
			var dataSet = new int[N];
			int user = 0;
			int j = 0;
			while (j < N)
			{
				if (rnd.NextDouble() * sum * 1.005 >= pk[user])
				{
					int n = 1 + (int) (Math.Pow(rnd.NextDouble(), 2) * 10);
					while (n-- > 0 && j < N)
					{
						dataSet[j++] = user;
					}
				}
				++user;
				if (user == K) user = 0;
			}

			Log("Computing control statistics...");
			// compute the control value for the counts per value
			var controlStats = dataSet
				.GroupBy(x => x).Select(g => new { Value = g.Key, Count = g.Count() })
				.OrderByDescending(x => x.Count)
				.ToList();
			Log($"> Found {controlStats.Count:N0} unique values");

			#endregion

			// create pseudo-index

			var megaPool = ArrayPool<CompressedWord>.Create((int) BitOperations.RoundUpToPowerOf2(N / 31), K);

			Log($"Indexing {N:N0} documents...");
			var index = new CompressedBitmapIndexBuilder<int>(capacity: K, pool: megaPool);
			for (int run = 0; run < R; run++)
			{
				if (run != 0)
				{
					index.Dispose();
					index = new(capacity: K, pool: megaPool);
				}

				// this is so that we can easily separate runs in the profiler TimeLine view
				Thread.Sleep(50);

				long sz = GC.GetAllocatedBytesForCurrentThread();

				var sw = Stopwatch.StartNew();
				for (int id = 0; id < dataSet.Length; id++)
				{
					int value = dataSet[id];
					index.Add(value, id);
				}

				sw.Stop();

				sz = GC.GetAllocatedBytesForCurrentThread() - sz;

				Log($"> Inserted {N:N0} documents in {sw.Elapsed.TotalSeconds:N1} sec ({N / sw.Elapsed.TotalSeconds:N0} ips, {sz:N0} bytes)");
			}

			// this is so that we can easily separate runs in the profiler TimeLine view
			Thread.Sleep(50);

			Log($"> Found {index.Count:N0} unique values");

			// verify the counts
			Log("Verifying index results...");
			var log = new StringWriter(CultureInfo.InvariantCulture);
			long totalBitmapSize = 0;
			j = 0;
			foreach (var kv in controlStats)
			{
				Assert.That(index.TryGetBitmap(kv.Value, out var bmp), Is.True, $"{kv.Value} is missing from index");
				Assert.That(bmp, Is.Not.Null);
				bmp!.GetStatistics(out int bits, out int words, out int a, out int b, out _);
				Assert.That(bits, Is.EqualTo(kv.Count), $"{kv.Value} has invalid count");
				int sz = bmp.ByteCount;
				log.WriteLine($"{kv.Value,8} : {bits,5} bits, {words} words ({a} lit. / {b} fil.), {sz:N0} bytes, {1.0 * sz / bits:N3} bytes/doc, {100.0 * (4 + 17 + sz) / (17 + (4 + 17) * bits):N2}% compression");
				totalBitmapSize += sz;
				j++;
			}
			Assert.That(index.Count, Is.EqualTo(controlStats.Count), "Some values have not been indexed properly");
			Log("> success!");
			Log($"Total index size for {N:N0} documents and {K:N0} values is {totalBitmapSize:N0} bytes");

			Log();
			Log("Dumping results:");
			Log(log.ToString());
		}

	}

}

#endif
