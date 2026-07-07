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

#pragma warning disable CS0618 // Type or member is obsolete
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace SnowBank.Data.Json.Binary.Tests
{
	using System.IO.Compression;
	using SnowBank.Numerics;
	using SnowBank.Runtime;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class JsonbTest : SimpleTest
	{

		static JsonbTest()
		{
			Jsonb.Warmup();
		}

		private void VerifyRoundtrip(JsonValue value)
		{
			Log("-----------------");

			DumpCompact("ORIGINAL", value);
			var bytes = Jsonb.Encode(value);
			//Log("RAW: ({0}) {1}", bytes.Length, bytes.AsSlice().ToString("X"));
			DumpHexa(bytes);

			var decoded = Jsonb.Decode(bytes);
			DumpCompact("DECODED", decoded);
			Assert.That(decoded, Is.EqualTo(value), "Decoded JSON value does not match original");
			Assert.That(decoded.IsReadOnly, Is.True, "JSON values created from jsonb are immutable by default");

			var compressed = bytes.ZstdCompress(0);
			if (compressed.Count < bytes.Count)
			{
				Log($"Zstd: {compressed.Count:N0} bytes (1 : {bytes.Count * 1.0 / compressed.Count:N2})");
#if FULL_DEBUG
				DumpHexa(compressed);
#endif
			}
			else
			{
				Log("Zstd: no gain");
			}
			Log("done");
			// mini-bench (uniquement en mode RELEASE)
#if !DEBUG
			// WARMUP
			var jsonBytes = value.ToJsonBytes(CrystalJsonSettings.JsonCompact).AsSlice();
			_ = CrystalJson.Parse(jsonBytes);
#if FULL_DEBUG
			DumpVersus(bytes, jsonBytes);
#endif

			const int N = 1000;
			long start = GetTimestamp();
			for (int i = N; i > 0; i--)
			{
				_ = Jsonb.Encode(value);
			}
			var elapsed = GetElapsedTime(start);
			Log($"Bench: jsonb encode {elapsed.TotalMilliseconds * 1000000 / N:N1} nanos");
			start = GetTimestamp();
			for (int i = N; i > 0; i--)
			{
				_ = Jsonb.Decode(bytes);
			}
			elapsed = GetElapsedTime(start); 
			Log($"Bench: jsonb decode {elapsed.TotalMilliseconds * 1000000 / N:N1} nanos");

			start = GetTimestamp();
			for (int i = N; i > 0; i--)
			{
				_ = value.ToJsonBytes(CrystalJsonSettings.JsonCompact);
			}
			elapsed = GetElapsedTime(start);
			Log($"Bench: JSON  encode {elapsed.TotalMilliseconds * 1000000 / N:N1} nanos");

			start = GetTimestamp();
			for (int i = N; i > 0; i--)
			{
				_ = CrystalJson.Parse(jsonBytes);
			}
			elapsed = GetElapsedTime(start);
			Log($"Bench: JSON  decode {elapsed.TotalMilliseconds * 1000000 / N:N1} nanos");
#endif
		}

		private void VerifyReferenceEncoding(string packed, JsonValue expected)
		{
			Log(packed);
			var bytes = Slice.FromHexString(packed, ' ');
#if FULL_DEBUG
			DumpHexa(bytes);
#endif
			var decoded = Jsonb.Decode(bytes);
			Log($"=> {decoded.ToJsonText()}");
			Assert.That(decoded, Is.EqualTo(expected));
		}

		[Test]
		public void Test_Encode_Scalars()
		{
			VerifyRoundtrip("hello");
			VerifyRoundtrip(string.Empty);
			VerifyRoundtrip("しのぶ一番");
			VerifyRoundtrip(true);
			VerifyRoundtrip(false);
			VerifyRoundtrip(JsonNull.Null);
			VerifyRoundtrip(0);
			VerifyRoundtrip(1);
			VerifyRoundtrip(123);
			VerifyRoundtrip(255);
			VerifyRoundtrip(256);
			VerifyRoundtrip(1234);
			VerifyRoundtrip(32767);
			VerifyRoundtrip(32768);
			VerifyRoundtrip(int.MaxValue);
			VerifyRoundtrip(uint.MaxValue);
			VerifyRoundtrip(long.MaxValue);
			VerifyRoundtrip(ulong.MaxValue);
			VerifyRoundtrip(-1);
			VerifyRoundtrip(-123);
			VerifyRoundtrip(-256);
			VerifyRoundtrip(-257);
			VerifyRoundtrip(-1234);
			VerifyRoundtrip(-32768);
			VerifyRoundtrip(-32769);
			VerifyRoundtrip(int.MinValue);
			VerifyRoundtrip(long.MinValue);
			VerifyRoundtrip(Math.PI);
			VerifyRoundtrip(Math.E);
			VerifyRoundtrip(double.NaN);
		}

		[Test]
		public void Test_Decode_Reference_Scalars()
		{
			// sanity test: we check that we can decode reference binary versions
			// note: if the binary format changes, don't forget to update the reference encodings!

			#region strings

			VerifyReferenceEncoding(
				"11 00 00 00 01 00 00 50 05 00 00 80 68 65 6C 6C 6F",
				"hello"
			);

			// ""
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 80",
				""
			);

			VerifyReferenceEncoding(
				"1B 00 00 00 01 00 00 50 0F 00 00 80 E3 81 97 E3 81 AE E3 81 B6 E4 B8 80 E7 95 AA",
				"しのぶ一番"
			);

			#endregion

			#region booleans

			// false
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 A0",
				false
			);

			// true
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 B0",
				true
			);

			#endregion

			#region null

			// null
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 C0",
				JsonNull.Null
			);

			#endregion

			#region integers

			// 0 (integer)
			VerifyReferenceEncoding(
				"0D 00 00 00 01 00 00 50 01 00 00 90 00",
				0
			);

			// -1 (integer)
			VerifyReferenceEncoding(
				"0D 00 00 00 01 00 00 50 01 00 00 90 FF",
				-1
			);

			// 1234 (integer)
			VerifyReferenceEncoding(
				"0E 00 00 00 01 00 00 50 02 00 00 90 D2 04",
				1234
			);

			// zero (literal)
			VerifyReferenceEncoding(
				"0D 00 00 00 01 00 00 50 01 00 00 E0 30",
				0
			);

			#endregion

			#region literal numbers (text)

			// -1 (literal)
			VerifyReferenceEncoding(
				"0E 00 00 00 01 00 00 50 02 00 00 E0 2D 31",
				-1
			);

			// 1234 (literal)
			VerifyReferenceEncoding(
				"10 00 00 00 01 00 00 50 04 00 00 E0 31 32 33 34",
				1234
			);

			// 123.4 (literal)
			VerifyReferenceEncoding(
				"11 00 00 00 01 00 00 50 05 00 00 E0 31 32 33 2E 34",
				123.4
			);

			// Math.PI (literal)
			VerifyReferenceEncoding(
				"1E 00 00 00 01 00 00 50 12 00 00 E0 33 2E 31 34 31 35 39 32 36 35 33 35 38 39 37 39 33 31",
				Math.PI
			);

			// double.NaN (literal)
			VerifyReferenceEncoding(
				"0F 00 00 00 01 00 00 50 03 00 00 E0 4E 61 4E",
				double.NaN
			);

			#endregion

		}

		[Test]
		public void Test_Encode_Arrays()
		{
			// empty array
			VerifyRoundtrip(JsonArray.Create());
			VerifyRoundtrip(JsonArray.ReadOnly.Empty);
			VerifyRoundtrip(JsonArray.ReadOnly.Create());

			// simple arrays
			VerifyRoundtrip(JsonArray.FromValues([ "hello", "world" ]));
			VerifyRoundtrip(JsonArray.FromValues([ 1, 2, 3 ]));
			VerifyRoundtrip(JsonArray.FromValues([ 1.1, 2.2, 3.3 ]));

			// mixed types
			VerifyRoundtrip(JsonArray.Create("hello", 123, true, JsonNull.Null, Math.PI, false, Guid.NewGuid()));

			// 0..99
			VerifyRoundtrip(Enumerable.Range(0, 100).ToJsonArray());

			// random numbers
			var rnd = new Random();
			VerifyRoundtrip(Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToJsonArray());

			// random guids
			VerifyRoundtrip(Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToJsonArray());

			// array of arrays
			VerifyRoundtrip(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create(4, 5, 6)));

			// array of objects
			VerifyRoundtrip(JsonArray.Create(JsonObject.Create("Foo", "Bar"), JsonObject.Create("Narf", "Zort")));

		}

		[Test]
		public void Test_Decode_Reference_Arrays()
		{
			//	["hello", "world"]
			VerifyReferenceEncoding(
				"1A 00 00 00 02 00 00 40 05 00 00 00 05 00 00 00 68 65 6C 6C 6F 77 6F 72 6C 64",
				JsonArray.Create([ "hello", "world" ])
			);

			//	[]
			VerifyReferenceEncoding(
				"08 00 00 00 00 00 00 40",
				JsonArray.ReadOnly.Empty
			);

		}

		[Test]
		public void Test_Encode_Objects()
		{
			// { }
			VerifyRoundtrip(JsonObject.Create());
			VerifyRoundtrip(JsonObject.ReadOnly.Empty);
			VerifyRoundtrip(JsonObject.ReadOnly.Create());
			VerifyRoundtrip(new JsonObject());
			VerifyRoundtrip(new JsonObject(0));

			// { "hello": "world" }
			VerifyRoundtrip(JsonObject.Create("hello", "world"));

			// complex object
			VerifyRoundtrip(JsonObject.FromObject(new
			{
				Hello = "World",
				Narf = "Zort",
				Level = 9001,
				Yes = true,
				No = false,
				Void = default(string),
				Ricky = new[] { "Un", "Dos", "Tres" },
				Foo = new { Bar = new { Baz = 123 } }
			}));

			// object with many fields (hashed!)
			VerifyRoundtrip(JsonObject.FromObject(new
			{
				One = 1,
				Two = 2,
				Three = 3,
				Four = 4,
				Five = 5,
				Six = 6,
				Seven = 7,
				Eight = 8,
				Nine = 9,
				Ten = 10,
			}));

		}

		[Test]
		public void Bench_Select_Objects_Hashed()
		{
			// verify that large object (with a lot of fields) are stored with a hashtable, that can be used to speed up future lookups
#if DEBUG
			const int RUNS = 3;
			const int ITERATIONS = 100;
#else
			const int RUNS = 10;
			const int ITERATIONS = 2_000;
#endif

			var rnd = new Random(123456);
			var randomIds = Enumerable.Range(0, 1_000).Select(i => (Key: $"AAAAAAAAAAAAAAAAAA_{Guid.NewGuid()}_{rnd.Next():X}_{i}", Value: i)).ToArray();

			RobustHistogram h = new();
			var min = h.FromNanoseconds(100); // It is very unlikely to take less than 100ns to perform a lookup!

			// perform a warmup run, so that the Tiered JIT does not interfer with us!
			{
				Log("Warming up...");
				var tmp = PackObject(randomIds.AsSpan(0, 50), true);
				for (int i = 0; i < 10; i++)
				{
					RunIteration(randomIds.AsSpan(0, 50), tmp, h);
					System.Threading.Thread.Sleep(25);
				}
				tmp = PackObject(randomIds.AsSpan(0, 50), false);
				for (int i = 0; i < 10; i++)
				{
					RunIteration(randomIds.AsSpan(0, 50), tmp, h);
					System.Threading.Thread.Sleep(25);
				}
				Log("Ready to rumble!");
			}

			foreach(bool disableHashing in (bool[]) [ true, false ])
			foreach (int numFields in (int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 22, 25, 33, 50, 66, 75, 100, 120, 150, 200, 250, 333, 500, 666, 750, 1_000 ])
			{
				rnd = new Random(123456);

				var ids = randomIds.AsSpan(0, numFields).ToArray();
				var packed = PackObject(ids, disableHashing);
				Shuffle(rnd, ids);

				h.Clear();
				for (int k = 0; k < RUNS; k++)
				{
					RunIteration(ids, packed, h);
				}
				Log($"║ N={numFields,5} | SZ={packed.Count,7:N0} | {(disableHashing ? "NO_HASH" : "HASH   ")} ║ P05={h.ToNanoseconds(h.Percentile(5)),10:N1} ns | MED={h.ToNanoseconds(h.Median),10:N1} ns | P95={h.ToNanoseconds(h.Percentile(95)),10:N1} ns ║ {h.GetDistribution(begin: min, end: 2_000)} ║");

			}

			static Slice PackObject(ReadOnlySpan<(string, int)> ids, bool noHash)
			{
				var obj = JsonObject.FromValues(ids);

				return Jsonb.Encode(obj, new JsonbWriterOptions() { HashingThreshold = noHash ? int.MaxValue : 0 });
			}

			static void RunIteration(ReadOnlySpan<(string Key, int Value)> items, Slice packed, RobustHistogram histo)
			{
				var sw = new Stopwatch();
				for (int i = 0; i < ITERATIONS; i++)
				{
					int idx = i % items.Length;
					var k = items[idx].Key;
					sw.Restart();
					// we have an issue where it is "too fast" in Release and the stopwatch may or may not have enough precision to measure the time taken
					int x = Jsonb.Get<int>(packed, k, -1);
					histo.Add(sw.Elapsed);
					if (x != idx) Assert.That(x, Is.EqualTo(items[idx].Value), "Found the wrong field!");
				}
			}
		}

		[Test, Category("Benchmark")]
		public void Test_Compress_Large_DataSet_Jsonb()
		{
			var db = FooDb.CreateTestData();
			var obj = JsonValue.FromValue(db);

			var json = obj.ToJsonSlice(CrystalJsonSettings.JsonCompact);
			Log($"JSON : {json.Count:N0} bytes, uncompressed");

			// jsonb raw
			var jsonb = Jsonb.Encode(obj);
			Log($"JSONB: {jsonb.Count:N0} bytes, uncompressed, {100.0 * jsonb.Count / json.Count:N1}%");

			// jsonb compressed
			Log($"JSONB: {jsonb.ZstdCompress(2).Count:N0} bytes, compressed with Zstd-2");
			Log($"JSONB: {jsonb.ZstdCompress(9).Count:N0} bytes, compressed with Zstd-9");
			Log($"JSONB: {jsonb.ZstdCompress(20).Count:N0} bytes, compressed with Zstd-20");
			Log($"JSONB: {jsonb.DeflateCompress(CompressionLevel.Optimal).Count:N0} bytes, compressed with Deflate (5)");
#if NET6_0_OR_GREATER
			Log($"JSONB: {jsonb.DeflateCompress(CompressionLevel.SmallestSize).Count:N0} bytes, compressed with Deflate (9)");
#endif
		}

		[Test]
		public void Test_Jsonb_Test()
		{
			var original = JsonObject.Create(
				[
					("Hello", "World!"),
					("Foo", 123),
					("Bar", true),
					("Baz", false),
					("Pi", Math.PI),
					("Max", long.MaxValue),
					("Nothing", null),
					("Point", JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ])),
					("Items", JsonArray.Create([ "A", "B", "C" ])),
				]
			);

			var packed = Jsonb.Encode(original);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Jsonb.Test(packed, "Hello", "World!"), Is.True);
				Assert.That(Jsonb.Test(packed, "Hello", JsonString.Return("World!")), Is.True);
				Assert.That(Jsonb.Test(packed, "Hello", "Le Monde!"), Is.False);
				Assert.That(Jsonb.Test(packed, "Hello", default(string)), Is.False);
				Assert.That(Jsonb.Test(packed, "Hello", 123), Is.False);
				Assert.That(Jsonb.Test(packed, "Hello", false), Is.False);
				Assert.That(Jsonb.Test(packed, "Hello", double.NaN), Is.False);

				Assert.That(Jsonb.Test(packed, "Nothing", default(string)), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", JsonNull.Null), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", JsonNull.Missing), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", JsonNull.Error), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", default(int?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", default(bool?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", default(Guid?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", default(Uuid128?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Nothing", "World!"), Is.False);
				Assert.That(Jsonb.Test(packed, "Nothing", ""), Is.False);
				Assert.That(Jsonb.Test(packed, "Nothing", false), Is.False);
				Assert.That(Jsonb.Test(packed, "Nothing", 0), Is.False);

				Assert.That(Jsonb.Test(packed, "Missing", default(string)), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", JsonNull.Null), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", JsonNull.Missing), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", JsonNull.Error), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", default(int?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", default(bool?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", default(Guid?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", default(Uuid128?)), Is.True);
				Assert.That(Jsonb.Test(packed, "Missing", "World!"), Is.False);
				Assert.That(Jsonb.Test(packed, "Missing", ""), Is.False);
				Assert.That(Jsonb.Test(packed, "Missing", false), Is.False);
				Assert.That(Jsonb.Test(packed, "Missing", 0), Is.False);

				Assert.That(Jsonb.Test(packed, "Foo", 123), Is.True);
				Assert.That(Jsonb.Test(packed, "Foo", 123L), Is.True);
				Assert.That(Jsonb.Test(packed, "Foo", (int?) 123), Is.True);
				Assert.That(Jsonb.Test(packed, "Foo", (long?) 123L), Is.True);
				Assert.That(Jsonb.Test(packed, "Foo", JsonNumber.Return(123)), Is.True);
				Assert.That(Jsonb.Test(packed, "Foo", 124), Is.False);
				Assert.That(Jsonb.Test(packed, "Foo", 123.01), Is.False);
				Assert.That(Jsonb.Test(packed, "Foo", double.NaN), Is.False);

				Assert.That(Jsonb.Test(packed, "Bar", true), Is.True);
				Assert.That(Jsonb.Test(packed, "Bar", false), Is.False);
				Assert.That(Jsonb.Test(packed, "Bar", "true"), Is.False);
				Assert.That(Jsonb.Test(packed, "Bar", ""), Is.False);
				Assert.That(Jsonb.Test(packed, "Bar", default(string)), Is.False);
				Assert.That(Jsonb.Test(packed, "Bar", 1), Is.False);

				Assert.That(Jsonb.Test(packed, "Baz", false), Is.True);
				Assert.That(Jsonb.Test(packed, "Baz", true), Is.False);
				Assert.That(Jsonb.Test(packed, "Baz", "false"), Is.False);
				Assert.That(Jsonb.Test(packed, "Baz", ""), Is.False);
				Assert.That(Jsonb.Test(packed, "Baz", default(string)), Is.False);
				Assert.That(Jsonb.Test(packed, "Baz", 0), Is.False);

				Assert.That(Jsonb.Test(packed, "Pi", Math.PI), Is.True);
				Assert.That(Jsonb.Test(packed, "Pi", (float) Math.PI), Is.False);
				Assert.That(Jsonb.Test(packed, "Pi", 3.1415), Is.False);
				Assert.That(Jsonb.Test(packed, "Pi", 3), Is.False);
				Assert.That(Jsonb.Test(packed, "Pi", double.NaN), Is.False);

				Assert.That(Jsonb.Test(packed, "Max", long.MaxValue), Is.True);
				Assert.That(Jsonb.Test(packed, "Max", -1), Is.False);
				Assert.That(Jsonb.Test(packed, "Max", "9223372036854775807"), Is.False);
				Assert.That(Jsonb.Test(packed, "Max", true), Is.False);
				Assert.That(Jsonb.Test(packed, "Max", double.NaN), Is.False);

				Assert.That(Jsonb.Test(packed, "Point.X", 1), Is.True);
				Assert.That(Jsonb.Test(packed, "Point.X", 123), Is.False);

				Assert.That(Jsonb.Test(packed, "Point.Y", 2), Is.True);
				Assert.That(Jsonb.Test(packed, "Point.Y", 1), Is.False);

				Assert.That(Jsonb.Test(packed, "Point.Z", 3), Is.True);
				Assert.That(Jsonb.Test(packed, "Point.Z", -1), Is.False);

				Assert.That(Jsonb.Test(packed, "Items[0]", "A"), Is.True);
				Assert.That(Jsonb.Test(packed, "Items[0]", 123), Is.False);

				Assert.That(Jsonb.Test(packed, "Items[1]", "B"), Is.True);
				Assert.That(Jsonb.Test(packed, "Items[1]", true), Is.False);

				Assert.That(Jsonb.Test(packed, "Items[2]", "C"), Is.True);
				Assert.That(Jsonb.Test(packed, "Items[2]", "c"), Is.False);
			}
		}
		
		[Test]
		public void Test_Jsonb_Select()
		{
			var original = JsonObject.Create(
				[
					("Hello", "World!"),
					("Foo", 123),
					("Bar", true),
					("Baz", Math.PI),
					("Point", JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ])),
					("Items", JsonArray.Create([ "A", "B", "C" ])),
				]
			);

			var packed = Jsonb.Encode(original);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Jsonb.Select(packed, "Hello"), IsJson.EqualTo("World!"));
				Assert.That(Jsonb.Select(packed, "Foo"), IsJson.EqualTo(123));
				Assert.That(Jsonb.Select(packed, "Bar"), IsJson.True);
				Assert.That(Jsonb.Select(packed, "Baz"), IsJson.EqualTo(Math.PI));
				Assert.That(Jsonb.Select(packed, "Point"), IsJson.EqualTo(JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ])));
				Assert.That(Jsonb.Select(packed, "Items"), IsJson.EqualTo(JsonArray.Create([ "A", "B", "C" ])));

				Assert.That(Jsonb.Select(packed, "Point.X"), IsJson.EqualTo(1));
				Assert.That(Jsonb.Select(packed, "Point.Y"), IsJson.EqualTo(2));
				Assert.That(Jsonb.Select(packed, "Point.Z"), IsJson.EqualTo(3));

				Assert.That(Jsonb.Select(packed, "Items[0]"), IsJson.EqualTo("A"));
				Assert.That(Jsonb.Select(packed, "Items[1]"), IsJson.EqualTo("B"));
				Assert.That(Jsonb.Select(packed, "Items[2]"), IsJson.EqualTo("C"));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Jsonb.Select(packed, JsonPath.Create("Hello")), IsJson.EqualTo("World!"));
				Assert.That(Jsonb.Select(packed, JsonPath.Create("Foo")), IsJson.EqualTo(123));
				Assert.That(Jsonb.Select(packed, JsonPath.Create("Bar")), IsJson.True);
				Assert.That(Jsonb.Select(packed, JsonPath.Create("Baz")), IsJson.EqualTo(Math.PI));
				Assert.That(Jsonb.Select(packed, JsonPath.Create("Point")), IsJson.EqualTo(JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ])));
				Assert.That(Jsonb.Select(packed, JsonPath.Create("Items")), IsJson.EqualTo(JsonArray.Create([ "A", "B", "C" ])));

				Assert.That(Jsonb.Select(packed, JsonPath.Empty["Point"]["X"]), IsJson.EqualTo(1));
				Assert.That(Jsonb.Select(packed, JsonPath.Empty["Point"]["Y"]), IsJson.EqualTo(2));
				Assert.That(Jsonb.Select(packed, JsonPath.Empty["Point"]["Z"]), IsJson.EqualTo(3));

				Assert.That(Jsonb.Select(packed, JsonPath.Empty["Items"][0]), IsJson.EqualTo("A"));
				Assert.That(Jsonb.Select(packed, JsonPath.Empty["Items"][1]), IsJson.EqualTo("B"));
				Assert.That(Jsonb.Select(packed, JsonPath.Empty["Items"][2]), IsJson.EqualTo("C"));
			}

		}

		[Test]
		public void Test_Jsonb_Select_Compiled()
		{
			// these would be reused multiple times;
			var selId = Jsonb.CreateSelector("Id");
			var selX = Jsonb.CreateSelector("Points[0].X");
			var selY = Jsonb.CreateSelector("Points[1].Y");
			var selFooBarBaz = Jsonb.CreateSelector("Foo.Bar.Baz");

			Assert.That(selId.Path, Is.EqualTo("Id"));
			Assert.That(selX.Path, Is.EqualTo("Points[0].X"));
			Assert.That(selY.Path, Is.EqualTo("Points[1].Y"));
			Assert.That(selFooBarBaz.Path, Is.EqualTo("Foo.Bar.Baz"));

			// generate a batch of random data
			var data = Enumerable
				.Range(0, 10)
				.Select(i => JsonObject.Create(
				[
					("Id", Guid.NewGuid()),
					("Points", JsonArray.Create(
						null,
						JsonObject.Create([
							("X", 1),
							("Y", -i),
							("Z", 0)
						])
					)),
					("Foo", JsonObject.Create("Bar", JsonObject.Create("Baz", "Hello, there!")))
				]))
				.ToArray();

			var batch = data
				.Select(o => Jsonb.Encode(o))
				.ToArray();

			// use these selectors on multiple different documents
			for(int i = 0; i < batch.Length; i++)
			{
				using (Assert.EnterMultipleScope())
				{
					Assert.That(Jsonb.Select(batch[i], selId), IsJson.EqualTo(data[i].Get<Guid>("Id")));
					Assert.That(Jsonb.Select(batch[i], selX), IsJson.Missing);
					Assert.That(Jsonb.Select(batch[i], selY), IsJson.EqualTo(-i));
					Assert.That(Jsonb.Select(batch[i], selFooBarBaz), IsJson.EqualTo("Hello, there!"));
				}
			}

		}

		public sealed record FooDb
		{
			public required int Version { get; init; }

			public required List<Vendor> Vendors { get; init; }

			public enum DeviceType
			{
				Unknown,
				Printer,
				Mfp,
				Plotter
			}

			public sealed record Vendor
			{
				public required Guid Id { get; init; }
				public required string Label { get; init; }
				public required string Name { get; init; }

				public required Dictionary<Guid, Model> Models { get; init; }

			}

			public sealed record Model
			{
				public required Guid Id { get; init; }

				public required string Name { get; init; }

				public required DeviceType Type { get; init; }

				public required bool ColorCapable { get; init; }

				public required bool DuplexCapable { get; init; }

				public required DateTime Updated { get; init; }

				public required List<Rule> Rules { get; init; }

				public static Model MakeRandom(Random rnd)
				{
					return new Model
					{
						Id = Guid.NewGuid(),
						Name = string.Join(" ", Enumerable.Range(0, rnd.Next(1, 5)).Select(_ => new string('M', rnd.Next(4, 32)))),
						Type = (DeviceType) rnd.Next((int) DeviceType.Printer, 1 + (int) DeviceType.Plotter),
						ColorCapable = rnd.Next(2) == 1,
						DuplexCapable = rnd.Next(2) == 1,
						Updated = DateTime.Now.Subtract(TimeSpan.FromMilliseconds(rnd.Next())),
						Rules = Enumerable.Range(0, rnd.Next(1, 10)).Select(_ => Rule.MakeRandom(rnd)).ToList()
					};
				}
			}

			public sealed record Rule
			{
				public required Guid Id { get; init; }

				public required List<Guid> Parents { get; init; }

				public required int Level { get; init; }

				public required string Expression { get; init; }

				public static Rule MakeRandom(Random rnd)
				{
					return new Rule
					{
						Id = Guid.NewGuid(),
						Parents = Enumerable.Range(0, rnd.Next(3)).Select(_ => Guid.NewGuid()).ToList(),
						Level = rnd.Next(100),
						Expression = string.Join(" ", Enumerable.Range(0, rnd.Next(1, 4)).Select(_ => new string('X', rnd.Next(4, 32))))
					};
				}
			}

			public static FooDb CreateTestData()
			{
				var rnd = new Random(1234);

				var fooDb = new FooDb()
				{
					Version = 1,
					Vendors =
					[
						new()
						{
							Id = Guid.NewGuid(),
							Label = "hp",
							Name = "Hewlett-Packard",
							Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
						new()
						{
							Id = Guid.NewGuid(),
							Label = "xerox",
							Name = "Xerox",
							Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
						new()
						{
							Id = Guid.NewGuid(),
							Label = "lex",
							Name = "Lexmark",
							Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
						new()
						{
							Id = Guid.NewGuid(),
							Label = "km",
							Name = "Konica Minolta",
							Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
						new()
						{
							Id = Guid.NewGuid(),
							Label = "kyo",
							Name = "Kyocera",
							Models = Enumerable.Range(0, 1000).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
						new()
						{
							Id = Guid.NewGuid(),
							Label = "samsung",
							Name = "Samsung",
							Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
						new()
						{
							Id = Guid.NewGuid(),
							Label = "tosh",
							Name = "Toshiba",
							Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
						},
					]
				};

				return fooDb;
			}

		}

	}

}
