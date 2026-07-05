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

namespace SnowBank.Text.Tests
{
	using System.Buffers.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Base64EncodingTest : SimpleTest
	{

		[Test]
		public void Test_Basics_Base64()
		{
			Assume.That(Convert.ToBase64String("a"u8.ToArray()), Is.EqualTo("YQ=="), "Base64 encoding padds by default");

			Assert.Multiple(() =>
			{
				Assert.That(Base64Encoding.ToBase64String(""u8), Is.EqualTo(""));
				Assert.That(Base64Encoding.ToBase64String("A"u8), Is.EqualTo("QQ=="));
				Assert.That(Base64Encoding.ToBase64String("Ab"u8), Is.EqualTo("QWI="));
				Assert.That(Base64Encoding.ToBase64String("Abc"u8), Is.EqualTo("QWJj"));
				Assert.That(Base64Encoding.ToBase64String("Abcd"u8), Is.EqualTo("QWJjZA=="));
				Assert.That(Base64Encoding.ToBase64String("Abcde"u8), Is.EqualTo("QWJjZGU="));
				Assert.That(Base64Encoding.ToBase64String("Abcdef"u8), Is.EqualTo("QWJjZGVm"));
				Assert.That(Base64Encoding.ToBase64String("Abcdefg"u8), Is.EqualTo("QWJjZGVmZw=="));
				Assert.That(Base64Encoding.ToBase64String("Hello, World!"u8), Is.EqualTo("SGVsbG8sIFdvcmxkIQ=="));
				Assert.That(Base64Encoding.ToBase64String(Slice.FromFixedU64(0xFFFEFCFBFAF9F8UL)), Is.EqualTo("+Pn6+/z+/wA="));

				Assert.That(Base64Encoding.FromBase64String("QWJjZGVmZw=="), Is.EqualTo("Abcdefg"u8.ToArray()));
				Assert.That(Base64Encoding.FromBase64String("SGVsbG8sIFdvcmxkIQ=="), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(Base64Encoding.FromBase64String("+Pn6+/z+/wA=").AsSlice(), Is.EqualTo(Slice.FromFixedU64(0xFFFEFCFBFAF9F8UL)));
			});

			var rnd = CreateRandomizer();

			// generate some random data, but with first 256 bytes in ascending order
			byte[] data = GetRandomData(rnd, 1024);
			for (int i = 0; i < 256; i++) data[i] = (byte) i;

			DumpHexa(data);

			string expected = Convert.ToBase64String(data);
			Log(expected);

			// encode
			Assert.That(Base64Encoding.ToBase64String(data), Is.EqualTo(expected));
			Assert.That(Base64Encoding.ToBase64String(data.AsSlice()), Is.EqualTo(expected));
			Assert.That(Base64Encoding.ToBase64String(data.AsSpan()), Is.EqualTo(expected));
			// decode
			Assert.That(Base64Encoding.FromBase64String(expected), Is.EqualTo(data));
			Assert.That(Base64Encoding.FromBase64String(expected.AsSpan()), Is.EqualTo(data));

			// graments
			for (int i = 1; i < data.Length; i++)
			{
				for (int j = 0; j < i; j++)
				{
					var chunk = data.AsSlice(j, i - j);

					expected = Convert.ToBase64String(chunk.Span.ToArray());
					string actual = Base64Encoding.ToBase64String(chunk);
					if (expected != actual)
					{
						Assert.That(expected, Is.EqualTo(actual), $"FAIL! j={j}, i={i}");
					}

					var decoded = Base64Encoding.FromBase64String(actual);
					if (!chunk.Span.SequenceEqual(decoded))
					{
						DumpVersus(chunk, decoded.AsSlice());
						Assert.That(decoded, Is.EqualTo(chunk.GetBytes()), $"FAIL! j={j}, i={i}");
					}
				}
			}
		}

		[Test]
		public void Test_Basics_Base64Url()
		{
#if NET9_0_OR_GREATER
			Assume.That(Base64Url.EncodeToString("a"u8), Is.EqualTo("YQ"), "Base64Url does not pad by default");
#endif

			Assert.Multiple(() =>
			{
				Assert.That(Base64Encoding.ToBase64UrlString(""u8), Is.EqualTo(""));
				Assert.That(Base64Encoding.ToBase64UrlString("A"u8), Is.EqualTo("QQ"));
				Assert.That(Base64Encoding.ToBase64UrlString("Ab"u8), Is.EqualTo("QWI"));
				Assert.That(Base64Encoding.ToBase64UrlString("Abc"u8), Is.EqualTo("QWJj"));
				Assert.That(Base64Encoding.ToBase64UrlString("Abcd"u8), Is.EqualTo("QWJjZA"));
				Assert.That(Base64Encoding.ToBase64UrlString("Abcde"u8), Is.EqualTo("QWJjZGU"));
				Assert.That(Base64Encoding.ToBase64UrlString("Abcdef"u8), Is.EqualTo("QWJjZGVm"));
				Assert.That(Base64Encoding.ToBase64UrlString("Abcdefg"u8), Is.EqualTo("QWJjZGVmZw"));
				Assert.That(Base64Encoding.ToBase64UrlString("Hello, World!"u8), Is.EqualTo("SGVsbG8sIFdvcmxkIQ"));
				Assert.That(Base64Encoding.ToBase64UrlString(Slice.FromFixedU64(0xFFFEFCFBFAF9F8UL)), Is.EqualTo("-Pn6-_z-_wA"));

				Assert.That(Base64Encoding.FromBase64UrlString("QWJjZGVmZw"), Is.EqualTo("Abcdefg"u8.ToArray()));
				Assert.That(Base64Encoding.FromBase64UrlString("SGVsbG8sIFdvcmxkIQ"), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(Base64Encoding.FromBase64UrlString("-Pn6-_z-_wA").AsSlice(), Is.EqualTo(Slice.FromFixedU64(0xFFFEFCFBFAF9F8UL)));
			});

			var rnd = CreateRandomizer();

			// generate some random data, but with first 256 bytes in ascending order
			byte[] data = GetRandomData(rnd, 1024);
			for (int i = 0; i < 256; i++) data[i] = (byte) i;

			DumpHexa(data);

#if NET9_0_OR_GREATER
			string expected = Base64Url.EncodeToString(data);
#else
			string expected = Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
#endif
			Log(expected);

			// encode
			Assert.That(Base64Encoding.ToBase64UrlString(data), Is.EqualTo(expected));
			Assert.That(Base64Encoding.ToBase64UrlString(data.AsSlice()), Is.EqualTo(expected));
			Assert.That(Base64Encoding.ToBase64UrlString(data.AsSpan()), Is.EqualTo(expected));
			// decode
			Assert.That(Base64Encoding.FromBase64UrlString(expected), Is.EqualTo(data));
			Assert.That(Base64Encoding.FromBase64UrlString(expected.AsSpan()), Is.EqualTo(data));

			// graments
			for (int i = 1; i < data.Length; i++)
			{
				for (int j = 0; j < i; j++)
				{
					var chunk = data.AsSlice(j, i - j);

#if NET9_0_OR_GREATER
					expected = Base64Url.EncodeToString(chunk.Span);
#else
					expected = Convert.ToBase64String(chunk.Span.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
#endif
					string actual = Base64Encoding.ToBase64UrlString(chunk);
					if (expected != actual)
					{
						Assert.That(expected, Is.EqualTo(actual), $"FAIL! j={j}, i={i}");
					}

					var decoded = Base64Encoding.FromBase64UrlString(actual);
					if (!chunk.Span.SequenceEqual(decoded))
					{
						DumpVersus(chunk, decoded.AsSlice());
						Assert.That(decoded, Is.EqualTo(chunk.GetBytes()), $"FAIL! j={j}, i={i}");
					}
				}
			}
		}

		[Test, Category("Benchmark")]
		[Parallelizable(ParallelScope.None)]
		public void Bench_Compare_ToString_With_BCL()
		{
#if DEBUG
			Assert.Warn("The results of this test are invalid when run in DEBUG build!");
			Log("###########################################################################");
			Log("### DEBUG WARNING ### THIS BENCHMARK IS INVALID WHEN RUN IN DEBUG MODE! ###");
			Log("###########################################################################");
			Log();
#endif
			byte[] data = new byte[1024];
			new Random().NextBytes(data);
			for (int i = 0; i < 256; i++) data[i] = (byte)i;

			//WARMUP + JIT
			for (int i = 0; i < 5; i++)
			{
				Assume.That(Base64Encoding.ToBase64String(data), Is.EqualTo(Convert.ToBase64String(data)));
				Assume.That(Base64Encoding.ToBase64String(data.AsSpan(31, 777)), Is.EqualTo(Convert.ToBase64String(data, 31, 777)));
			}

			string? s1 = null, s2 = null;
			var sw = new Stopwatch();

			TimeSpan durA = TimeSpan.Zero;
			TimeSpan durB = TimeSpan.Zero;
#if DEBUG
			const int R = 5;
			const int N = 25 * 1000;
#else
			const int R = 5;
			const int N = 250 * 1000;
#endif

			for (int r = -1; r < R; r++)
			{
				var gc = GC.CollectionCount(0);
				sw.Restart();
				for (int i = 0; i < N; i++)
				{
					s1 = Convert.ToBase64String(data, 0, (i % data.Length) + 1);
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				if (r >= 0)
				{
					Log($"#{r,-2} => BCL: {sw.Elapsed} for {N:N0} with {gc:N0} GC0 => {data.Length * N / (sw.Elapsed.TotalSeconds * 1048576),8:N1} MB/sec | {new string('A', (int)Math.Round(data.Length * N / (sw.Elapsed.TotalSeconds * 1048576 * 50), MidpointRounding.AwayFromZero))}");
					durA += sw.Elapsed;
				}
				GC.Collect();

				gc = GC.CollectionCount(0);
				sw.Restart();
				for (int i = 0; i < N; i++)
				{
					s2 = Base64Encoding.ToBase64String(data.AsSpan(0, (i % data.Length) + 1));
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				if (r >= 0)
				{
					Log($"#{r,-2} => DOX: {sw.Elapsed} for {N:N0} with {gc:N0} GC0 => {data.Length * N / (sw.Elapsed.TotalSeconds * 1048576),8:N1} MB/sec | {new string('B', (int) Math.Round(data.Length * N / (sw.Elapsed.TotalSeconds * 1048576 * 50), MidpointRounding.AwayFromZero))}");
					durB += sw.Elapsed;
				}
				GC.Collect();
			}

			Log($"### BCL {durA.TotalMilliseconds / R:N1} ms <- VS -> DOX {durB.TotalMilliseconds / R:N1} ms => x {durA.TotalSeconds / durB.TotalSeconds:N2}");

			Log($"EXPECTED: {s1}");
			Log($"ACTUAL  : {s2}");
			Assert.That(s2, Is.EqualTo(s1));
		}

		[Test, Category("Benchmark")]
		[Parallelizable(ParallelScope.None)]
		public void Bench_Compare_TextWriter_Append_With_BCL()
		{
#if DEBUG
			Assert.Warn("The results of this test are invalid when run in DEBUG build!");
			Log("###########################################################################");
			Log("### DEBUG WARNING ### THIS BENCHMARK IS INVALID WHEN RUN IN DEBUG MODE! ###");
			Log("###########################################################################");
			Log();
#endif

			byte[] data = new byte[256 * 1024];
			new Random().NextBytes(data);
			for (int i = 0; i < 256; i++) data[i] = (byte) i;

			//WARMUP + JIT
			{
				var wr = new StringWriter(new StringBuilder(1024 * 1024));
				Assume.That(Base64Encoding.ToBase64String(data), Is.EqualTo(Convert.ToBase64String(data)));
				Base64Encoding.EncodeTo(wr, data.AsSpan(31, 777));
			}

#if DEBUG
			const int N = 1_000;
#else
			const int N = 10_000;
#endif

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			{
				long gc = GC.CollectionCount(0);
				StringWriter? buf = null;
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < N; i++)
				{
					if (buf == null || (i & 0xFF) == 0) buf = new StringWriter(new StringBuilder(1024 * 1024));
					buf.Write(Convert.ToBase64String(data));
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				Log($"BCL: {sw.Elapsed} for {N:N0} with {gc:N0} GC0");
			}

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			{
				long gc = GC.CollectionCount(0);
				StringWriter? buf = null;
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < N; i++)
				{
					if (buf == null || (i & 0xFF) == 0) buf = new StringWriter(new StringBuilder(1024 * 1024));
					Base64Encoding.EncodeTo(buf, data);
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				Log($"DOX: {sw.Elapsed} for {N:N0} with {gc:N0} GC0");
			}
		}

		[Test]
		public void Test_Versus()
		{
			byte[] original = "Ecchi na no wa, ikenai to omoimasu!"u8.ToArray();

			for (int i = 0; i < original.Length; i++)
			{
				byte[] source = new byte[i];
				Array.Copy(original, source, i);

				string b64 = Convert.ToBase64String(source);
				byte[] decoded = Base64Encoding.FromBase64String(b64);
				if (!decoded.AsSlice().Equals(source))
				{
					Log($"|{b64}|");
					DumpVersus(source, decoded);
					Assert.That(decoded, Is.EqualTo(source), "Decode Base64 buffer does not match original");
				}

				decoded = Base64Encoding.FromBase64String(b64.AsSpan());
				if (!decoded.AsSlice().Equals(source))
				{
					Log($"|{b64}|");
					DumpVersus(source, decoded);
					Assert.That(decoded, Is.EqualTo(source), "Decode Base64 buffer does not match original");
				}
			}
		}

		[Test]
		public void Test_Subset()
		{
			byte[] original = "Ecchi na no wa, ikenai to omoimasu!"u8.ToArray();

			for (int i = 0; i < original.Length; i++)
			{
				byte[] source = new byte[i];
				Array.Copy(original, source, i);

				string b64 = Base64Encoding.ToBase64UrlString(source);
				byte[] decoded = Base64Encoding.FromBase64UrlString(b64);
				if (!decoded.AsSlice().Equals(source))
				{
					Log($"|{b64}|");
					DumpVersus(source, decoded);
					Assert.That(decoded, Is.EqualTo(source));
				}
			}

			const int N = 10 * 1000;
			var rnd = new Random();
			for (int i = 0; i < N; i++)
			{
				int sz = rnd.Next(0, 255);
				var data = new byte[sz];

				rnd.NextBytes(data);

				string encoded = Base64Encoding.ToBase64UrlString(data);
				byte[] decoded = Base64Encoding.FromBase64UrlString(encoded);
				if (!decoded.AsSlice().Equals(data))
				{
					Log($"|{encoded}|");
					DumpVersus(data, decoded);
					Assert.That(decoded, Is.EqualTo(data));
				}
			}

		}

	}

}
