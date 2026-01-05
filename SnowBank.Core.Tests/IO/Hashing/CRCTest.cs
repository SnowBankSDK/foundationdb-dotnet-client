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

// ReSharper disable HeapView.BoxingAllocation
// ReSharper disable HeapView.ObjectAllocation
#pragma warning disable CS0618 // Type or member is obsolete
namespace SnowBank.IO.Hashing.Tests
{
	using System.Diagnostics.CodeAnalysis;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ChecksumFacts : SimpleTest
	{

		#region FNV1

		[Test]
		public void TestFnv1Hash32()
		{
			Assert.That(Fnv1Hash32.FromString("", false), Is.EqualTo(0x00000000));
			Assert.That(Fnv1Hash32.FromString("foobar", false), Is.EqualTo(0x31f0b262));
			Assert.That(Fnv1Hash32.FromString("Hello World", false), Is.EqualTo(0x1282A4EF));
			Assert.That(Fnv1Hash32.FromString("hello world", false), Is.EqualTo(0x548DA96F), "Case sensitive!");
			Assert.That(Fnv1Hash32.FromString("Hello World", true), Is.EqualTo(0x548DA96F), "Case INsensitive!");
			Assert.That(Fnv1Hash32.FromString("Hello World ", false), Is.EqualTo(0x12A9A41D));
			Assert.That(Fnv1Hash32.FromString("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", false), Is.EqualTo(0x30CD4C75));

			Assert.That(Fnv1Hash32.FromBytes(new byte[0]), Is.EqualTo(0x811C9DC5));
			Assert.That(Fnv1Hash32.FromBytes(new byte[] { 65, 66, 67 }), Is.EqualTo(0x634CAFEB));
			Assert.That(Fnv1Hash32.FromBytes(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }), Is.EqualTo(0x1282A4EF));

			// TODO: utiliser des test vectors à partir de http://www.isthe.com/chongo/src/fnv/test_fnv.c
		}

		[Test]
		public void TestFnv1Hash64()
		{
			Assert.That(Fnv1Hash64.FromString("", false), Is.EqualTo(0x0000000000000000));
			Assert.That(Fnv1Hash64.FromString("foobar", false), Is.EqualTo(0x340D8765A4DDA9C2));
			Assert.That(Fnv1Hash64.FromString("Hello World", false), Is.EqualTo(0x91F4E6CCCE8B35AF));
			Assert.That(Fnv1Hash64.FromString("hello world", false), Is.EqualTo(0x7DCF62CDB1910E6F), "Case sensitive!");
			Assert.That(Fnv1Hash64.FromString("Hello World", true), Is.EqualTo(0x7DCF62CDB1910E6F), "Case INsensitive!");
			Assert.That(Fnv1Hash64.FromString("Hello World ", false), Is.EqualTo(0x8E59DD02F68C387D));
			Assert.That(Fnv1Hash64.FromString("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", false), Is.EqualTo(0x01517B4ECD46EB75));

			Assert.That(Fnv1Hash64.FromBytes(new byte[0]), Is.EqualTo(0xCBF29CE484222325));
			Assert.That(Fnv1Hash64.FromBytes(new byte[] { 65, 66, 67 }), Is.EqualTo(0xD86FEA186B53126B));
			Assert.That(Fnv1Hash64.FromBytes(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }), Is.EqualTo(0x91F4E6CCCE8B35AF));

			Assert.That(Fnv1Hash64.FromInt64(0x0123456789abcdefL), Is.EqualTo(0x4C66A756F98346A5));
			Assert.That(Fnv1Hash64.FromUInt64(0xfedcba9876543210UL), Is.EqualTo(0xEC364618ECCC1DF5));
			Assert.That(Fnv1Hash64.FromInt32(0x12345678), Is.EqualTo(0x6768D67E8A7E58F5));
			Assert.That(Fnv1Hash64.FromUInt32(0x98765432U), Is.EqualTo(0xC6FFCE7FE2FD3125));
			Assert.That(Fnv1Hash64.FromInt16(0x1234), Is.EqualTo(0x08329407B4EB8443));
			Assert.That(Fnv1Hash64.FromUInt16(0xFEDC), Is.EqualTo(0x0831AC07B4E9FAE7));
			Assert.That(Fnv1Hash64.FromByte(42), Is.EqualTo(0xAF63BD4C8601B7F5));

			// les FromXXX doivent être équivalent a FromBytes(XXX.ToBytes())
			Assert.That(Fnv1Hash64.FromInt64(0x0123456789abcdefL), Is.EqualTo(Fnv1Hash64.FromBytes(BitConverter.GetBytes(0x0123456789abcdefL))));
			Assert.That(Fnv1Hash64.FromUInt64(0xfedcba9876543210UL), Is.EqualTo(Fnv1Hash64.FromBytes(BitConverter.GetBytes(0xfedcba9876543210UL))));
			Assert.That(Fnv1Hash64.FromInt32(0x12345678), Is.EqualTo(Fnv1Hash64.FromBytes(BitConverter.GetBytes(0x12345678))));
			Assert.That(Fnv1Hash64.FromUInt32(0x98765432U), Is.EqualTo(Fnv1Hash64.FromBytes(BitConverter.GetBytes(0x98765432U))));
			Assert.That(Fnv1Hash64.FromInt16(0x1234), Is.EqualTo(Fnv1Hash64.FromBytes(BitConverter.GetBytes((short)0x1234))));
			Assert.That(Fnv1Hash64.FromUInt16(0xFEDC), Is.EqualTo(Fnv1Hash64.FromBytes(BitConverter.GetBytes((ushort)0xFEDC))));
			Assert.That(Fnv1Hash64.FromByte(42), Is.EqualTo(Fnv1Hash64.FromBytes(new byte[] { 42 })));

			Assert.That(Fnv1Hash64.FromBytes(Guid.Parse("c71679bc-14fd-4373-a10a-28e3789102de").ToByteArray()), Is.EqualTo(0x5BCB33EF746B1C43));
			Assert.That(Fnv1Hash64.FromGuid(Guid.Parse("c71679bc-14fd-4373-a10a-28e3789102de")), Is.EqualTo(0x5BCB33EF746B1C43));

			// TODO: utiliser des test vectors à partir de http://www.isthe.com/chongo/src/fnv/test_fnv.c
		}

		#endregion

		#region XxHash

		[Test]
		[SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
		public void TestXxHash32()
		{
			//TODO: je n'ai pas vraiment trouvé de test suite avec des valeurs de ref
			// tout ce que j'ai trouvé c'est que Hash(123, "test") => 2758658570

			Assert.That(XxHash32.Compute(Array.Empty<byte>()), Is.EqualTo(0x02CC5D05));
			Assert.That(XxHash32.Compute(new byte[] { 65, 66, 67 }), Is.EqualTo(0x80712ED5));
			Assert.That(XxHash32.Compute(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }), Is.EqualTo(0xB1FD16EE));
			Assert.That(XxHash32.Compute(new byte[] { 1, 2, 3, 4, 65, 66, 67, 5, 6 }, 4, 3), Is.EqualTo(0x80712ED5));
			Assert.That(XxHash32.Compute(new byte[] { 65, 66, 67 }, 1, 0), Is.EqualTo(0x02CC5D05));
			Assert.That(() => XxHash32.Compute(default(byte[])!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => XxHash32.Compute(default(byte[])!, 0, 0), Throws.InstanceOf<ArgumentNullException>());

			Assert.That(XxHash32.Compute(Slice.Empty), Is.EqualTo(0x02CC5D05));
			Assert.That(XxHash32.Compute(new byte[] { 65, 66, 67 }.AsSlice()), Is.EqualTo(0x80712ED5));
			Assert.That(XxHash32.Compute(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }.AsSlice()), Is.EqualTo(0xB1FD16EE));
			Assert.That(XxHash32.Compute(new byte[] { 1, 2, 3, 4, 65, 66, 67, 5, 6 }.AsSlice(4, 3)), Is.EqualTo(0x80712ED5));
			Assert.That(XxHash32.Compute(new byte[] { 65, 66, 67 }.AsSlice(1, 0)), Is.EqualTo(0x02CC5D05));
			Assert.That(() => XxHash32.Compute(default(Slice)), Throws.InstanceOf<ArgumentNullException>());

			Assert.That(XxHash32.Continue(123, Encoding.UTF8.GetBytes("test"), 0, 4), Is.EqualTo(0xA46DCA0A));

			Assert.That(XxHash32.Compute(""), Is.EqualTo(0x02CC5D05));
			Assert.That(XxHash32.Compute("foobar"), Is.EqualTo(319326668));
			Assert.That(XxHash32.Compute("Hello World"), Is.EqualTo(690424818));
			Assert.That(XxHash32.Compute("hello world"), Is.EqualTo(3418293499), "Case sensitive!");
			Assert.That(XxHash32.Compute("Hello World "), Is.EqualTo(1029714533));
			Assert.That(XxHash32.Compute("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"), Is.EqualTo(2453304613));
			Assert.That(XxHash32.Compute("foobar", 1, 0), Is.EqualTo(0x02CC5D05));
			Assert.That(() => XxHash32.Compute(default(string)!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => XxHash32.Compute(default(string)!, 0, 0), Throws.InstanceOf<ArgumentNullException>());

			Assert.That(XxHash32.Compute(Array.Empty<char>(), 0, 0), Is.EqualTo(0x02CC5D05));
			Assert.That(XxHash32.Compute("foobar".ToCharArray(), 0, 0), Is.EqualTo(0x02CC5D05));
			Assert.That(XxHash32.Compute("foobar".ToCharArray(), 0, 6), Is.EqualTo(319326668));
			Assert.That(() => XxHash32.Compute(default(char[])!, 0, 0), Throws.InstanceOf<ArgumentNullException>());

		}

		[Test]
		public void TestXxHash32_SanityCheck()
		{
			const uint PRIME = 2654435761U;
			const int SANITY_BUFFER_SIZE = 101;

			var sanityBuffer = new byte[SANITY_BUFFER_SIZE];
			uint prime = PRIME;
			for (int i = 0; i < sanityBuffer.Length; i++)
			{
				sanityBuffer[i] = (byte)(prime >> 24);
				prime *= prime;
			}

			static void TestSequence(byte[] sentence, int len, uint seed, uint nresult)
			{
				uint h = XxHash32.Continue(seed, sentence, 0, len);
				Assert.That(h, Is.EqualTo(nresult), $"[{len}, {seed}] => 0x{h:X8} != 0x{nresult:X8}");
			}

			TestSequence(sanityBuffer, 1, 0, 0xB85CBEE5);
		 	TestSequence(sanityBuffer, 1, PRIME, 0xD5845D64);
		 	TestSequence(sanityBuffer, 14, 0, 0xE5AA0AB4);
		 	TestSequence(sanityBuffer, 14, PRIME, 0x4481951D);
		 	TestSequence(sanityBuffer, SANITY_BUFFER_SIZE, 0, 0x1F1AA412);
		 	TestSequence(sanityBuffer, SANITY_BUFFER_SIZE, PRIME, 0x498EC8E2);
		}

		[Test]
		[SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
		public void TestXxHash64()
		{
			Assert.That(XxHash64.FromBytes(Array.Empty<byte>()), Is.EqualTo(17241709254077376921), "Empty should still return something");
			Assert.That(XxHash64.FromBytes(Slice.Empty), Is.EqualTo(17241709254077376921), "Empty should still return something");
			Assert.That(XxHash64.FromBytes(new byte[] { 65, 66, 67 }), Is.EqualTo(16603337192413064856));
			Assert.That(XxHash64.FromBytes(new byte[] { 65, 66, 67 }.AsSlice()), Is.EqualTo(16603337192413064856));
			Assert.That(XxHash64.FromBytes(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }), Is.EqualTo(7148569436472236994));
			Assert.That(XxHash64.FromBytes(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }.AsSlice()), Is.EqualTo(7148569436472236994));
			Assert.That(XxHash64.FromBytes(new byte[] { 1, 2, 3, 4, 65, 66, 67, 5, 6 }.AsSlice(4, 3)), Is.EqualTo(16603337192413064856));
			Assert.That(XxHash64.FromBytes(new byte[] { 65, 66, 67 }.AsSlice(1, 0)), Is.EqualTo(17241709254077376921));
			Assert.That(() => XxHash64.FromBytes(default(byte[])!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => XxHash64.FromBytes(Slice.Nil), Throws.InstanceOf<ArgumentNullException>());

			Assert.That(XxHash64.FromText(""), Is.EqualTo(17241709254077376921UL), "Empty should still return something");
			Assert.That(XxHash64.FromText("".AsSpan(0 ,0)), Is.EqualTo(17241709254077376921UL), "Empty should still return something");
			Assert.That(XxHash64.FromText("foobar"), Is.EqualTo(5814087441338904397UL));
			Assert.That(XxHash64.FromText("Hello World"), Is.EqualTo(10764455564493180894UL));
			Assert.That(XxHash64.FromText("hello world"), Is.EqualTo(12647288196429669931UL), "Case sensitive!");
			Assert.That(XxHash64.FromText("Hello World "), Is.EqualTo(17307987487561287563UL));
			Assert.That(XxHash64.FromText("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"), Is.EqualTo(12826112420584064250));
			Assert.That(XxHash64.FromText("foobar".AsSpan(1, 0)), Is.EqualTo(17241709254077376921UL));
			//note: taille == 11 et non pas 10 car il y a un diacrytic!
			Assert.That(XxHash64.FromText("Spın̈al Tap"), Is.EqualTo(14255674348978296242UL));
			Assert.That(XxHash64.FromText("This is Spın̈al Tap!!!".AsSpan("This is ".Length, "Spın̈al Tap".Length)), Is.EqualTo(14255674348978296242UL));
			Assert.That(XxHash64.FromText("foobar".ToCharArray().AsSpan().Slice(0, 0)), Is.EqualTo(17241709254077376921));
			Assert.That(XxHash64.FromText("foobar".ToCharArray().AsSpan()), Is.EqualTo(5814087441338904397UL));
			Assert.That(() => XxHash64.FromText(default(string)!), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void TestXxHash64_SanityCheck()
		{
			const uint PRIME = 2654435761U;
			const int SANITY_BUFFER_SIZE = 101;

			var sanityBuffer = new byte[SANITY_BUFFER_SIZE];
			uint prime = PRIME;
			for (int i = 0; i < sanityBuffer.Length; i++)
			{
				sanityBuffer[i] = (byte)(prime >> 24);
				prime *= prime;
			}

			void TestSequence64(byte[] sentence, int len, ulong seed, ulong nresult)
			{
				ulong h = XxHash64.Continue(seed, sentence.AsSpan(0, len));
				Assert.That(h, Is.EqualTo(nresult), $"[{len}, {seed}] => 0x{h:X16} != 0x{nresult:X16}");
			}

			TestSequence64(sanityBuffer, 1, 0, 0x4FCE394CC88952D8UL);
			TestSequence64(sanityBuffer, 1, PRIME, 0x739840CB819FA723UL);
			TestSequence64(sanityBuffer, 14, 0, 0xCFFA8DB881BC3A3DUL);
			TestSequence64(sanityBuffer, 14, PRIME, 0x5B9611585EFCC9CBUL);
			TestSequence64(sanityBuffer, SANITY_BUFFER_SIZE, 0, 0x0EAB543384F878ADUL);
			TestSequence64(sanityBuffer, SANITY_BUFFER_SIZE, PRIME, 0xCAA65939306F1E21UL);
		}


		#endregion

		#region Benchmarks...

		const double NANOS_PER_SEC = 1E9;
		const double NANOS_PER_TICK = 1E9 / TimeSpan.TicksPerSecond;
		const double CPU_GHZ = 3.4;

		private static void Hash32Bench(string? label, Func<int, byte[], uint> f, byte[] sample, int iter)
		{
			f(1, sample); // Warmup
			var t = Stopwatch.StartNew();
			var h = f(iter, sample);
			t.Stop();
			if (label != null)
			{
				double nanos = (t.Elapsed.Ticks * NANOS_PER_TICK) / iter;
				Log($"{label,6} = {h:x8} > {nanos,8:F1} ns => {(nanos / sample.Length) * ChecksumFacts.CPU_GHZ,5:F1} cycles/byte  @ {ChecksumFacts.CPU_GHZ} GHz, {(1.0 * sample.Length * iter) / (1024 * 1024 * t.Elapsed.TotalSeconds),7:F1} MB/sec");
			}
		}

		private static void Hash64Bench(string? label, Func<int, byte[], ulong> f, byte[] sample, int iter)
		{
			f(1, sample); // Warmup
			var t = Stopwatch.StartNew();
			var h = f(iter, sample);
			t.Stop();
			if (label != null)
			{
				double nanos = (t.Elapsed.TotalSeconds / iter) * NANOS_PER_SEC;
				Log($"{label,6} = {h:x16} > {nanos,8:F1} ns => {(nanos / sample.Length) * ChecksumFacts.CPU_GHZ,5:F1} cycles/byte @ {ChecksumFacts.CPU_GHZ} GHz, {(1.0 * sample.Length * iter) / (1024 * 1024 * t.Elapsed.TotalSeconds),7:F1} MB/sec");
			}
		}

		private static void Hash128Bench(string? label, Func<int, byte[], Guid> f, byte[] sample, int iter)
		{
			f(1, sample); // Warmup
			var t = Stopwatch.StartNew();
			var h = f(iter, sample);
			t.Stop();

			if (label != null)
			{
				double nanos = (t.Elapsed.TotalSeconds / iter) * NANOS_PER_SEC;
				Log($"{label,6} = {h:n} > {nanos,8:F1} ns => {(nanos / sample.Length) * ChecksumFacts.CPU_GHZ,5:F1} cycles/byte @ {ChecksumFacts.CPU_GHZ} GHz, {(1.0 * sample.Length * iter) / (1024 * 1024 * t.Elapsed.TotalSeconds),7:F1} MB/sec");
			}
		}

		[Test]
		public void Bench_HashFunctions()
		{
			byte[] TINY = [ 42 ];
			byte[] SMALL = [ 1, 2, 3, 4, 5 ];
			byte[] GUID = Guid.NewGuid().ToByteArray();
			byte[] MEDIUM = "連邦政府軍のご協力により、君達の基地は、全てCATSがいただいた。"u8.ToArray();
			byte[] LARGE = new byte[4096];
			new Random().NextBytes(LARGE);
			byte[] HUGE = new byte[256 * 1024];
			new Random().NextBytes(HUGE);

			// note: 1 nanos => 1 Ghz

			var vectors = new []  { TINY, SMALL, GUID, MEDIUM, LARGE, HUGE };

			Func<int, byte[], uint> hfFnv1_32 = (n, bytes) => { uint h = 0; while (n-- > 0) { h = Fnv1Hash32.FromBytes(bytes); } return h; };
			Func<int, byte[], uint> hfFnv1a_32 = (n, bytes) => { uint h = 0; while (n-- > 0) { h = Fnv1aHash32.FromBytes(bytes); } return h; };
			Func<int, byte[], uint> hfMurmur3_32 = (n, bytes) => { uint h = 0; while (n-- > 0) { h = Murmur3Hash32.FromBytes(bytes); } return h; };
			Func<int, byte[], uint> hfMurmur3_32_inl = (n, bytes) => { uint h = 0; while (n-- > 0) { h = Murmur3Hash32.FromBytes(bytes); } return h; };
			Func<int, byte[], uint> hfXxHash_32 = (n, bytes) => { uint h = 0; while (n-- > 0) { h = XxHash32.Compute(bytes); } return h; };

			Func<int, byte[], ulong> hfFnv1_64 = (n, bytes) => { ulong h = 0; while (n-- > 0) { h = Fnv1Hash64.FromBytes(bytes); } return h; };
			Func<int, byte[], ulong> hfFnv1a_64 = (n, bytes) => { ulong h = 0; while (n-- > 0) { h = Fnv1aHash64.FromBytes(bytes); } return h; };
			Func<int, byte[], ulong> hfXxHash_64 = (n, bytes) => { ulong h = 0; while (n-- > 0) { h = XxHash64.FromBytes(bytes); } return h; };

			Func<int, byte[], Guid> hfMurmur3_128 = (n, bytes) => { Guid h = Guid.Empty; while (n-- > 0) { h = Murmur3Hash128.FromBytes(bytes); } return h; };

			var hashFunctions32 = new Dictionary<string, Func<int, byte[], uint>>
			{
				{ "FNV1 32", hfFnv1_32 },
				{ "FNV1a 32", hfFnv1a_32 },
				{ "Murmur3 32", hfMurmur3_32 },
				{ "Murmur3 32 (inline)", hfMurmur3_32_inl },
				{ "xxHash 32", hfXxHash_32 },
			};

			var hashFunctions64 = new Dictionary<string, Func<int, byte[], ulong>>
			{
				{ "FNV1 64", hfFnv1_64 },
				{ "FNV1a 64", hfFnv1a_64 },
				{ "xxHash 64", hfXxHash_64 },
			};

			var hashFunctions128 = new Dictionary<string, Func<int, byte[], Guid>>
			{
				{ "Murmur3 128", hfMurmur3_128 },
			};

			// warmup

			foreach (var test in hashFunctions32)
			{
				Log();
				Log("==== " + test.Key + " ==================");

				// warmup
				Hash32Bench(null, test.Value, new byte[15], 1);

				foreach (var data in vectors)
				{
					Hash32Bench(data.Length.ToString().PadLeft(5), test.Value, data, 100);
				}
			}

			foreach (var test in hashFunctions64)
			{
				Log();
				Log("==== " + test.Key + " ==================");

				// warmup
				Hash64Bench(null, test.Value, new byte[31], 1);

				foreach (var data in vectors)
				{
					Hash64Bench(data.Length.ToString().PadLeft(5), test.Value, data, 100);
				}
			}

			foreach (var test in hashFunctions128)
			{
				Log();
				Log("==== " + test.Key + " ==================");

				// warmup
				Hash128Bench(null, test.Value, new byte[63], 1);

				foreach (var data in vectors)
				{
					Hash128Bench(data.Length.ToString().PadLeft(5), test.Value, data, 100);
				}
			}

		}

		#endregion

		#region Verification Tests....

		/// <summary>Helper fonction utilisée pour vérifier une Hash Function</summary>
		public static uint VerificationTest(int hashBits, Func<uint, byte[], byte[]> hashFunction)
		{
			Assert.That(hashFunction, Is.Not.Null, "hashFunction");

			int hashBytes = hashBits / 8;

			// Calcul les 256 hashs des vecteurs {0}, {0, 1}, {0, 1, 2}, ...., {0, ..., 255}
			// Concatènes chaque hash dans un tableau de 4 * 256 bytes (ie : bytes 0-3 = hash de {0}, bytes 4-7 = hash de {0, 1}, bytes 1023-1023 = hash de {0, ..., 255 }
			// Calcul le hash final sur le vecteur total

			var key = new byte[256];
			var hashes = new byte[hashBytes * 256];
			for (int i = 0; i < key.Length; i++)
			{
				key[i] = (byte)i;
				uint seed = (uint)(256 - i);

				var tmp = new byte[i];
				Array.Copy(key, 0, tmp, 0, i);
				byte[] h = hashFunction(seed, tmp);
				Assert.That(h, Is.Not.Null, $"h[{i}]");
				Assert.That(h.Length, Is.EqualTo(hashBytes), $"h[{i}].Length");

				Array.Copy(h, 0, hashes, i * hashBytes, hashBytes);
			}

			var result = hashFunction(0U, hashes);
			return result.AsSpan(0, 4).ToUInt32();
		}

		private static byte[] Md5RefTest(uint seed, byte[] bytes)
		{
			// note: la seed est ignorée!
			// on ne retourne que les 4 premiers octets

			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				var h = md5.ComputeHash(bytes);
				var t = new byte[4];
				Array.Copy(h, 0, t, 0, 4);
				return t;
			}
		}

		[Test]
		public void Test_VerificitationTest_Md5_Reference()
		{
			// ce test est la plutôt pour vérifier que notre vérificateur fonctionne :)
			// (on utilise l'implémentation de référence de MD5, donc si le hash calculé n'est pas celui attendu, c'est que le framework de test est buggué...)

			var found = VerificationTest(32, (seed, bytes) => Md5RefTest(seed, bytes));
			// cf http://code.google.com/p/smhasher/source/browse/trunk/main.cpp
			Assert.That(found, Is.EqualTo(0xC10C356B), "MD5, first 32 bits of result. *REFERENCE TEST* (if this fails, check the test framework instead of the hash functions)");
		}

		[Test]
		public void Test_VerificitationTest_Fnv1a_32()
		{
			var found = VerificationTest(32, (seed, bytes) =>
			{
				var h = Fnv1aHash32.FromBytes(seed, bytes);
				var t  = new byte[4];
				BinaryPrimitives.WriteUInt32LittleEndian(t, h);
				return t;
			});
			// cf http://code.google.com/p/smhasher/source/browse/trunk/main.cpp
			Assert.That(found, Is.EqualTo(0xE3CBBE91), "Fowler-Noll-Vo hash, 32-bit, Alternative");
		}

		[Test]
		public void Test_VerificitationTest_Murmur3_32()
		{
			var found = VerificationTest(32, (seed, bytes) =>
			{
				var h = Murmur3Hash32.Continue(seed, bytes);
				var t = new byte[4];
				BinaryPrimitives.WriteUInt32LittleEndian(t, h);
				return t;
			});
			// cf http://code.google.com/p/smhasher/source/browse/trunk/main.cpp
			Assert.That(found, Is.EqualTo(0xB0F57EE3), "MurmurHash3 for x86, 32-bit");
		}

		[Test]
		public void Test_VerificitationTest_Murmur3_128()
		{
			var found = VerificationTest(128, (seed, bytes) =>
			{
				var h = Murmur3Hash128.Continue(seed, bytes);
				return h.ToByteArray();
			});
			// cf http://code.google.com/p/smhasher/source/browse/trunk/main.cpp
			Assert.That(found, Is.EqualTo(0x6384BA69), "MurmurHash3 for x64, 128-bit");
		}

		#endregion

	}

}
