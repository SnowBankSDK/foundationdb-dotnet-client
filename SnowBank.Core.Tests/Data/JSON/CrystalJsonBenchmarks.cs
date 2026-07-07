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

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
// ReSharper disable StringLiteralTypo

#define ENABLE_NEWTONSOFT

namespace SnowBank.Data.Json.Tests
{
	using System.Buffers;
	using System.Collections;
	using System.IO.Compression;
	using System.Text.Json.Serialization;
	using SnowBank.Numerics;
	using SnowBank.Runtime;
	using SnowBank.Text;
#if ENABLE_NEWTONSOFT
	using NJ = Newtonsoft.Json;
#endif

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Category("Benchmark")]
	[Parallelizable(ParallelScope.None)]
	public class CrystalJsonBenchmarks : SimpleTest
	{

		static CrystalJsonBenchmarks()
		{
#if DEBUG
			Log("WARNING: benchmark running in DEBUG mode! Do NOT use these results to judge the actual performance at runtime.");
#endif
			
			// do some warmup work
			for (int i = 0; i < 100; i++)
			{
				_ = CrystalJson.Serialize(CrystalJson.Parse(JsonValue.FromValue(MediaContent.GetMedia1()).ToJsonText()).As<MediaContent>());
			}
		}

		#region Simple Benchmarks...

		[Test]
		[Category("LongRunning")]
		[Order(10)]
		public void Bench_Compare()
		{
			var priority = Thread.CurrentThread.Priority;
			try
			{
				Thread.CurrentThread.Priority = ThreadPriority.Highest;

				// basic types
				ComparativeBenchmark(0);
				ComparativeBenchmark(123);
				ComparativeBenchmark(long.MaxValue);
				ComparativeBenchmark(Math.PI);
				ComparativeBenchmark("short string");
				ComparativeBenchmark("really long string that does not contains a double quote, and is trying to consume aaaall your memory even if you have hundreds of zetabytes!");
				ComparativeBenchmark("really long string that does indeed contain the \" charac, and is trying to consume aaaall your memory even if you have hundreds of zetabytes!");
				ComparativeBenchmark(DateTime.Now);
				ComparativeBenchmark(DateTime.UtcNow);
				ComparativeBenchmark(DateTimeOffset.Now);
				ComparativeBenchmark(DateTimeOffset.UtcNow);
				ComparativeBenchmark(Guid.NewGuid());

				// simple class / structs
				ComparativeBenchmark(new DummyJsonStruct(), 10_000);
				ComparativeBenchmark(new DummyJsonClass(), 10_000);
				ComparativeBenchmark(
					new DummyJsonClass()
					{
						Name = "James Bond",
						Index = 7,
						Size = 123456789,
						Height = 1.8f,
						Amount = 0.07d,
						Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
						Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
						State = DummyJsonEnum.Bar,
					}, 10_000
				);

				ComparativeBenchmark(new[] { "Sheldon Cooper by Jim Parsons", "Leonard Hofstadter by Johny Galecki", "Penny by Kaley Cuoco", "Howard Wolowitz by Simon Helberg", "Raj Koothrappali by Kunal Nayyar", }, 10_000);

				ComparativeBenchmark(new[] { 1, 2, 3, 4, 42, 666, 2403, 999999999 }, 1000);
				ComparativeBenchmark(new[] { new { Character = "Sheldon Cooper", Actor = "Jim Parsons", Female = false }, new { Character = "Leonard Hofstadter", Actor = "Johny Galecki", Female = false }, new { Character = "Penny", Actor = "Kaley Cuoco", Female = true }, new { Character = "Howard Wolowitz", Actor = "Simon Helberg", Female = false }, new { Character = "Raj Koothrappali", Actor = "Kunal Nayyar", Female = false }, }, 10_000);

				// Objet composite (worst case ?)
				ComparativeBenchmark(
					new
					{
						Id = 1,
						Title = "The Big Bang Theory",
						Cancelled = false, // (I hope that's always the case ^^; )
						Cast = new[] { new { Character = "Sheldon Cooper", Actor = "Jim Parsons", Female = false }, new { Character = "Leonard Hofstadter", Actor = "Johny Galecki", Female = false }, new { Character = "Penny", Actor = "Kaley Cuoco", Female = true }, new { Character = "Howard Wolowitz", Actor = "Simon Helberg", Female = false }, new { Character = "Raj Koothrappali", Actor = "Kunal Nayyar", Female = false }, },
						Seasons = 4,
						ScoreIMDB = 8.4, // (26/10/2010)
						Producer = "Chuck Lorre Productions",
						PilotAirDate = new DateTime(2007, 9, 24, 0, 0, 0, DateTimeKind.Utc), // plus simple si UTC
					}, 10_000
				);
			}
			finally
			{
				Thread.CurrentThread.Priority = priority;
			}
		}

		private static void ComparativeBenchmark<T>(T? data, int iterations = 100_000, int runs = 50)
		{
			Log();
			Log("### Comparative Benchmark: " + (data == null ? "<null>" : (data.GetType().GetFriendlyName())));
			// warmup!
			var sb = new StringBuilder(1024);
			var settings = CrystalJsonSettings.JsonCompact;
			_ = CrystalJson.Serialize(data, sb, settings);
			string jsonText = sb.ToString();
			Log($"CJS # [{jsonText.Length}] {jsonText}");
			var jsonValue = CrystalJson.Deserialize<T?>(jsonText, default(T));
			Log($"CJS > {jsonValue}");

#if ENABLE_NEWTONSOFT
			var njs = new NJ.JsonSerializer();
			bool newtonOk = true;
			string? newtonJsonText = null;
			try
			{
				sb.Clear();
				njs.Serialize(new StringWriter(sb), data);
				newtonJsonText = sb.ToString();
				Log($"NSJ $ [{sb.Length}] {sb}");
				jsonValue = njs.Deserialize<T>(new NJ.JsonTextReader(new StringReader(sb.ToString())));
				Log($"NSJ > {jsonValue}");
			}
			catch (Exception e)
			{
				newtonOk = false;
				LogError("! NSJ FAILED => " + e.Message);
			}
#endif

			// Benchmarking!

			var report = RobustBenchmark.Run(
				(data, settings),
				static (g) => new StringBuilder(1024),
				static (g, buffer, _) =>
				{
					buffer.Clear();
					return CrystalJson.Serialize(g.data, buffer, g.settings);
				},
				static (g, buffer) => buffer.Length,
				runs,
				iterations
			);
			double crystalOps = report.BestIterationsPerSecond;
			Log($"* CRYSTAL   SERIALIZATION: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.Results[0]}");

			report = RobustBenchmark.Run(
				jsonText,
				(text) => CrystalJson.Deserialize(text, default(T)),
				(text) => text.Length,
				runs,
				iterations
			);
			double crystalOps2 = report.BestIterationsPerSecond;
			Log($"* CRYSTAL DESERIALIZATION: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos)");

#if ENABLE_NEWTONSOFT
			// comparison with NewtonSoft.JSON
			if (newtonOk)
			{
				report = RobustBenchmark.Run(
					new StringBuilder(1024),
					(buffer) =>
					{
						buffer.Clear();
						njs.Serialize(new StringWriter(buffer), data);
					},
					(buffer) => buffer.Length,
					runs,
					iterations
				);
				double newtonOps = report.BestIterationsPerSecond;
				Log($"* JSON.NET  SERIALIZATION: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos)");

				double ratio = (newtonOps - crystalOps) / newtonOps;
				if (ratio == 0)
					Log("-> Result: EX AEQUO !");
				else if (ratio > 0)
					Log($"-> Result: We are {Math.Round(100 * ratio, 1, MidpointRounding.AwayFromZero)} % slower :( => 1 : {(newtonOps / crystalOps):N2}");
				else
					Log($"-> Result: We are {Math.Round(-100 * ratio, 1, MidpointRounding.AwayFromZero)} % faster :) => {(crystalOps / newtonOps):N2} : 1");
			}

			if (newtonOk)
			{
				report = RobustBenchmark.Run(
					newtonJsonText ?? "",
					(text) => njs.Deserialize<T>(new NJ.JsonTextReader(new StringReader(text))),
					(text) => text.Length,
					runs,
					iterations
				);
				double newtonOps = report.BestIterationsPerSecond;
				Log($"* JSON.NET DESERIALIZATION:{report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos)");

				double ratio = (newtonOps - crystalOps2) / newtonOps;
				if (ratio == 0)
					Log("-> Result: EX AEQUO !");
				else if (ratio > 0)
					Log($"-> Result: We are {Math.Round(100 * ratio, 1, MidpointRounding.AwayFromZero)} % slower :( => 1 : {(newtonOps / crystalOps2):N2}");
				else
					Log($"-> Result: We are {Math.Round(-100 * ratio, 1, MidpointRounding.AwayFromZero)} % faster :) => {(crystalOps2 / newtonOps):N2} : 1");
			}
#endif

			Log();
		}

		#endregion

		#region TPC Based Benchmarks...

		// This is based on the "Thrift vs ProtocolBuffers" benchmark:
		// * Article: https://github.com/eishay/jvm-serializers/wiki/
		// * GitHub: https://github.com/eishay/jvm-serializers

#if !DEBUG
		public const int BENCH_RUNS = 25; // (max 60 a cause du Pierce Criterion)
		public const int BENCH_ITERATIONS = 1_000;
#else
		public const int BENCH_RUNS = 50; // (max 60 a cause du Pierce Criterion)
		public const int BENCH_ITERATIONS = 10_000;
#endif

		public void Bench_Everything()
		{
			Bench_TotalTime();
			Bench_SerializationTime();
			Bench_ParseTime();
			Bench_DeserializationTime();
		}

		[Test]
		public void Bench_TotalTime()
		{
			// Create an object, serialize it to a byte array, then deserialize it back to an object.
			var media = MediaContent.GetMedia1();

			#region Warmup
			{ // first run to check that everything is ok
				
				Log("Warming up...");
				
				{
					Log("CrystalJson:");
					var b = CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact);
					var c = CrystalJson.Deserialize<MediaContent>(b);
					Log($"json/snowbank-runtime: size   = {b.Length:N0} chars");
					Log(b);
					Assert.That(media, Is.EqualTo(c), "clone != media ??");

					using var w = new CrystalJsonWriter(0, CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver);
					media.Manual(w);
					b = w.GetString();
					Log($"json/snowbank-manual : size   = {b.Length:N0} chars");
					Log(b);

					for (int i = 0; i < 100; i++)
					{
						var so = CrystalJson.ToSlice(media, ArrayPool<byte>.Shared, CrystalJsonSettings.JsonCompact);
						so.Dispose();
					}
				}

#if ENABLE_NEWTONSOFT
				{
					Log("JSON.Net:");
					var b = NJ.JsonConvert.SerializeObject(media);
					_ = NJ.JsonConvert.DeserializeObject<MediaContent>(b);
					Log($"json/json.net-runtime: size  = {b.Length:N0} chars");
					Log(b);
				}
#endif
				{
					Log("System.Text.Json:");
					var b = System.Text.Json.JsonSerializer.Serialize(media);
					_ = System.Text.Json.JsonSerializer.Deserialize<MediaContent>(b);
					Log($"json/s.t.json-runtime: size  = {b.Length:N0} chars");
					Log(b);
				}
			}

			#endregion

			RunBenchOnMethod("json/snowbank-runtime: total", media, static (m) =>
			{
				_ = CrystalJson.Deserialize<MediaContent>(CrystalJson.Serialize(m, CrystalJsonSettings.JsonCompact));
			});

			RunBenchOnMethod("json/snowbank-pooled : total", media, static (m) =>
			{
				var bytes = CrystalJson.ToSlice(m, ArrayPool<byte>.Shared, CrystalJsonSettings.JsonCompact);
				_ = CrystalJson.Deserialize<MediaContent>(bytes.Data);
				bytes.Dispose();
			});

			RunBenchOnMethod("json/s.t.json-runtime: total", media, static (m) => System.Text.Json.JsonSerializer.Deserialize<MediaContent>(System.Text.Json.JsonSerializer.Serialize(m)));
			
#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-runtime: total", media, static (m) => NJ.JsonConvert.DeserializeObject<MediaContent>(NJ.JsonConvert.SerializeObject(m)));
#endif
		}

		[Test]
		public void Bench_SerializationTime()
		{
			// Create an object, serialize it to a byte array
			var media = MediaContent.GetMedia1();

			#region Warmup
			// first run to check that everything is ok
			for (int i = 0; i < 10; i++)
			{
				_ = CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact);
				_ = CrystalJson.ToSlice(media, CrystalJsonSettings.JsonCompact);
			}

			#endregion

			RunBenchOnMethod("json/snowbank-text  : ser  ", media, static (m) => CrystalJson.Serialize(m, CrystalJsonSettings.JsonCompact));

			RunBenchOnMethod("json/snowbank-buffer: ser  ", media, static (m) => CrystalJson.ToSlice(m, CrystalJsonSettings.JsonCompact));

			RunBenchOnMethod("json/snowbank-manual: ser  ", media, static (m) => CrystalJson.Convert(m, static (writer, media) => media.Manual(writer), CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver));

			RunBenchOnMethod("json/s.t.json-text  : ser  ", media, static (m) => System.Text.Json.JsonSerializer.Serialize(m));
			
#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-poco  : ser  ", media, static (m) => NJ.JsonConvert.SerializeObject(m));
#endif
		}

		[Test]
		// ReSharper disable once IdentifierTypo
		public void Bench_DomificationTime()
		{
			// Measure the time to convert a CLR type into the equivalent JsonObject
			var media = MediaContent.GetMedia1();

			#region Warmup
			{
				_ = JsonObject.FromObject(media);
			}
			#endregion

			RunBenchOnMethod("json/snowbank-text  : dom  ", media, static (m) => JsonObject.FromObject(m));
		}

		[Test]
		// ReSharper disable once IdentifierTypo
		public void Bench_BindTime()
		{
			// Measure the time to bind a JsonObject into the equivalent CLR type
			var json = JsonObject.FromObject(MediaContent.GetMedia1());

			#region Warmup
			{
				_ = json.Bind<MediaContent>();
			}
			#endregion

			RunBenchOnMethod("json/snowbank-text  : dom  ", json, static (j) => j.Bind<MediaContent>());
		}

		[Test]
		public void Bench_DeserializationTime()
		{
			// Measure the time to deserialize JSON text into the equivalent CLR type
			var media = MediaContent.GetMedia1();

			#region Warmup
			string jsonText = CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact);
			{ // first run to check that everything is ok
				Assert.That(CrystalJson.Deserialize<MediaContent>(jsonText), Is.EqualTo(media), "clone != media ??");
#if ENABLE_NEWTONSOFT
				Assert.That(new NJ.JsonSerializer().Deserialize<MediaContent>(new NJ.JsonTextReader(new StringReader(jsonText))), Is.EqualTo(media), "newtonsoft check");
#endif
			}

			#endregion

			// JSON => STATIC
			
			RunBenchOnMethod("json/snowbank-poco  : deser", jsonText, static (txt) => CrystalJson.Deserialize<MediaContent>(txt, CrystalJsonSettings.Json));

			RunBenchOnMethod("json/s.t.json-poco  : deser", jsonText, static (txt) => System.Text.Json.JsonSerializer.Deserialize<MediaContent>(txt));

#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-poco  : deser", jsonText, static (txt) => NJ.JsonSerializer.CreateDefault().Deserialize<MediaContent>(new NJ.JsonTextReader(new StringReader(txt))));
#endif

		}

		[Test]
		public void Bench_ParseTime()
		{
			// Measure the time to parse JSON text into the equivalent JsonObject

			string jsonText = CrystalJson.Serialize(MediaContent.GetMedia1(), CrystalJsonSettings.JsonCompact);

			#region Warmup
			{
				CrystalJson.Parse(jsonText);
#if ENABLE_NEWTONSOFT
				_ = NJ.Linq.JObject.Parse(jsonText);
#endif
			}
			#endregion

			// JSON => DOM
			RunBenchOnMethod("json/snowbank-token: parse", jsonText, static (txt) => CrystalJson.Parse(txt));

#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-token: parse", jsonText, static (txt) => NJ.Linq.JObject.Parse(txt));
#endif

#if DEBUG_STRINGTABLE_PERFS
			Log("ST.addCalls   = " + CrystalJsonParser.StringTable.addCalls);
			Log("ST.localHits  = " + CrystalJsonParser.StringTable.localHits);
			Log("ST.sharedHits = " + CrystalJsonParser.StringTable.sharedHits);
#endif
		}

		[Test]
		public void Bench_String()
		{
			// Measure the time to escape various strings into JSON

			string[] phrases =
			[
				"\"",
				"a",
				"ab",
				"abc",
				"abcd",
				"abcde",
				"abcdef",
				"abcdefg",
				"abcdefgh",
				"Hello World!",
				"0123456789ABCDEF",
				"abcdefghijklmnopqrstuvwxyz",
				"The quick brown fox jumps over the lazy dog",
				"This is a test of the emergency broadcast system",
				"This is a test of the emergency broadcast system!",
				"This is a test of the emergency broadcast system!!",
				"This is a test of the emergency broadcast system!!!",
				"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Praesent ac urna nisl, ut placerat nibh. Quisque molestie feugiat tellus et feugiat.",
			];

			#region Warmup...

			foreach (var s in phrases)
			{
				JsonEncoding.NeedsEscaping(s);
				_ = JsonEncoding.Encode(s);

			}
			#endregion

			double[][] nanos = new double[phrases.Length][];

			var buffer = new char[1024];
			
			for (int i = 0; i < phrases.Length; i++)
			{
				buffer.AsSpan().Clear();
				string s = phrases[i];
				var ts = new double[6];
				int p = 0;
				Log($"\"{s}\" [{s.Length}]");
				ts[p++] = RunBenchOnMethod("   check", () => JsonEncoding.NeedsEscaping(s), measure: false);
				ts[p++] = RunBenchOnMethod("  unsafe", () => { buffer[0] = '"'; s.CopyTo(buffer.AsSpan(1)); buffer[s.Length + 1] = '"'; }, measure: false);
				ts[p  ] = RunBenchOnMethod("  append", () => JsonEncoding.TryEncodeTo(s, buffer, out _), measure: false);
				nanos[i] = ts;
			}

#if true || DUMP_TO_CSV
			Log("Value;Length;Check;Unsafe;Append;Check_NPC;Unsafe_NPC;Append_NPC");
			for (int i = 0; i < phrases.Length; i++)
			{
				string s = phrases[i];
				var line = $"\"{s.Replace("\"", "\"\"")}\";{s.Length}";
				foreach (var n in nanos[i])
				{
					line += $";{n}";
				}

				foreach (var n in nanos[i])
				{
					line += $";{(n / s.Length)}";
				}
				Log(line);
			}
#endif

		}

		private static double RunBenchOnMethod(string name, Action method, bool measure = true)
		{
			return RunBenchOnMethod(name, method, (m) => m(), measure);
		}
		
		private static double RunBenchOnMethod<TState>(string name, TState state, Action<TState> method, bool measure = true)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			// small pause, so that runs can be visually separated in a profiler (in Timeline view)
			Thread.Sleep(50);

			var h = measure ? new RobustHistogram() : null;

			var report = RobustBenchmark.Run(
				state,
				method,
				(_) => 0,
				BENCH_RUNS,
				BENCH_ITERATIONS,
				h
			);

			Log($"{name} = {report.BestIterationsNanos,5:N0} nanos [{report.MedianIterationsNanos,5:N0}] (~ {report.BestIterationsPerSecond,11:N0} ips [{report.MedianIterationsPerSecond,11:N0}], allocated {report.GcAllocatedOnThread:N0} bytes, GC={report.GC0:N0}/{report.GC0:N0}/{report.GC0:N0})");
			if (h != null)
			{
				Log(h.GetReport(true));
			}
			
			// small pause, so that runs can be visually separated in a profiler (in Timeline view)
			Thread.Sleep(50);
			
			return report.BestIterationsNanos;
		}

		#endregion

		#region NextGen Bench

		[Flags]
		public enum TestMode
		{
			None = 0,
			Serialize = 1,
			Parse = 2,
			Deserialize = 4,
			All = Serialize | Parse | Deserialize
		}

		public void RunTestMethod<T>(T model, TestMode mode = TestMode.All, bool instrument = false)
			where T : notnull
		{
#if DEBUG
			// Build TeamCity
			const int RUNS = 20;
			const int ITERATIONS_FAST = 500;
			const int ITERATIONS_NORMAL = 250;
			const int ITERATIONS_MEDIUM = 100;
			const int ITERATIONS_SLOW = 50;
#else
			// Benchmarks
			const int RUNS = 60;
			const int ITERATIONS_FAST = 1000;
			const int ITERATIONS_NORMAL = 500;
			const int ITERATIONS_MEDIUM = 250;
			const int ITERATIONS_SLOW = 100;
#endif

			var settings = CrystalJsonSettings.JsonCompact;
			var resolver = CrystalJson.DefaultResolver;

			static void DoFullGc()
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
			}

			// setup / warmup
			var jsonText = CrystalJson.Serialize<T>(model, settings);
			{
				_ = CrystalJson.Parse(jsonText, settings);
				_ = JsonValue.FromValue<T>(model, settings, resolver);
				// can sometime fail if T is an abstract type
				try { _ = CrystalJson.Deserialize<T>(jsonText, settings, resolver); } catch(JsonBindingException) { }
			}

			int iterationsPerRun = jsonText.Length <= 100 ? ITERATIONS_FAST : jsonText.Length <= 1000 ? ITERATIONS_NORMAL : jsonText.Length <= 10000 ? ITERATIONS_MEDIUM : ITERATIONS_SLOW;
			//Log("{0} => {1}", sw.Elapsed.Ticks, ITER_PER_RUN);

			//DoFullGc();
			var serReport = RobustBenchmark.Run(
				model,
				(value) => CrystalJson.Serialize<T>(value, settings),
				(value) => value,
				RUNS,
				iterationsPerRun,
				instrument ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			DoFullGc();
			var parseReport = RobustBenchmark.Run(
				jsonText,
				(text) => CrystalJson.Parse(text, settings),
				(text) => text,
				RUNS,
				iterationsPerRun,
				instrument ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			DoFullGc();
			var deserializeReport = RobustBenchmark.Run(
				jsonText,
				(text) => CrystalJson.Deserialize<T>(text, settings, resolver),
				(text) => text,
				RUNS,
				iterationsPerRun,
				instrument ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			DoFullGc();
			var domReport = RobustBenchmark.Run(
				model,
				(value) => JsonValue.FromValue<T>(value),
				(value) => value,
				RUNS,
				iterationsPerRun,
				instrument ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			string name = typeof(T).GetFriendlyName();
			if (name.EndsWith("[]", StringComparison.Ordinal))
			{
				if (model is ICollection x) name = name.Substring(0, name.Length - 1) + x.Count + "]";
			}

			Log(String.Format(CultureInfo.InvariantCulture,
				" {0,-22} {19,5} {1,7:N0} | {2,8:N0} ({3,7:N0} \u00B1{11,7:N1}) {15,10} | {4,8:N0} ({5,7:N0} \u00B1{12,7:N1}) {16,10} | {6,8:N0} ({7,7:N0} \u00B1{13,7:N1}) {17,10} | {8,8:N0} ({9,7:N0} \u00B1{14,7:N1}) {18,10} | {10}",
				name,
				jsonText.Length,
				serReport.BestIterationsNanos,
				serReport.MedianIterationsNanos,
				parseReport.BestIterationsNanos,
				parseReport.MedianIterationsNanos,
				deserializeReport.BestIterationsNanos,
				deserializeReport.MedianIterationsNanos,
				domReport.BestIterationsNanos,
				domReport.MedianIterationsNanos,
				Truncate(jsonText),
				serReport.StdDevIterationNanos,
				parseReport.StdDevIterationNanos,
				deserializeReport.StdDevIterationNanos,
				domReport.StdDevIterationNanos,
				serReport.GcAllocatedOnThread / iterationsPerRun, //string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", serReport.GC0, serReport.GC1, serReport.GC2),
				parseReport.GcAllocatedOnThread / iterationsPerRun, //string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", parseReport.GC0, parseReport.GC1, parseReport.GC2),
				deserializeReport.GcAllocatedOnThread / iterationsPerRun, //string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", deserReport.GC0, deserReport.GC1, deserReport.GC2),
				domReport.GcAllocatedOnThread / iterationsPerRun, //string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", domReport.GC0, domReport.GC1, domReport.GC2),
				iterationsPerRun
			));
			//Log(serReport.Histogram.GetReport(true));
		}

		private static string Truncate(string json)
		{
			if (json.Length < 100) return json;
			return $"{json.AsSpan(0, 70)}\u2026{json.AsSpan(json.Length - 30)}";
		}

		[Test]
		[Category("LongRunning")]
		[Order(11)]
		public void Test_NG_Bench()
		{
			Log("Model Type                       Size | Ser<T>   (nanos)           (allocated) | Parse    (nanos)           (allocated) | Deser<T> (nanos)           (allocated) | Domify   (nanos)           (allocated) | JSON Text");

			var rnd = new Random(24031974);

			// primitive types
			RunTestMethod(true);
			RunTestMethod(0);
			RunTestMethod(123);
			RunTestMethod(-666);
			RunTestMethod(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
			RunTestMethod(DateTime.Now.Ticks);
			RunTestMethod(0.0d);
			RunTestMethod(1.234d);
			RunTestMethod(Math.PI);
			RunTestMethod(1.234f);
			RunTestMethod(1.234m);
			RunTestMethod(DummyJsonEnum.Foo);
			RunTestMethod("hello world");
			RunTestMethod("Ph'nglui mglw'nafh Cthulhu R'lyeh wgah'nagl fhtan");
			RunTestMethod("This is the super long string that looks innocent until you look closer and realize that it ends with a \"");
			RunTestMethod(Guid.NewGuid().ToString());
			RunTestMethod(Guid.NewGuid());
			RunTestMethod(DateTime.Now);
			RunTestMethod(DateTimeOffset.Now);
			RunTestMethod(DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant());
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant().InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()));
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant().InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()).ToOffsetDateTime());
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant().InUtc().LocalDateTime);

			// small arrays
			RunTestMethod(Enumerable.Range(0, 7).Select(i => i).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => rnd.Next(int.MaxValue)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => (uint)rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => (double)rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Math.Round(rnd.NextDouble() * 10, 2, MidpointRounding.AwayFromZero)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => rnd.NextDouble() * 10).ToArray());
			RunTestMethod(new[] { "hello", "world", "foo", "bar", "baz", "narfzort", "Leeroy Jenkins" });
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Guid.NewGuid().ToString()).ToList());
			// medium arrays
			RunTestMethod(Enumerable.Range(0, 60).Select(i => i).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => rnd.Next(int.MaxValue)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => (double)rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => Math.Round(rnd.NextDouble() * 10, 2, MidpointRounding.AwayFromZero)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => rnd.NextDouble() * 10).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => Guid.NewGuid().ToString()).ToList());
			// jagged
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 24).Select(j => rnd.Next(10000)).ToArray()).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 24).Select(j => (double)rnd.Next(10000)).ToArray()).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 24).Select(j => Math.Round(rnd.NextDouble() * 1000, 2, MidpointRounding.AwayFromZero)).ToArray()).ToArray());
			// big ugly array
			RunTestMethod(Enumerable.Range(0, 365).Select(i => rnd.Next(1001) - 500).ToArray()); // -500..+500
			RunTestMethod(Enumerable.Range(0, 365).Select(i => rnd.Next(1001) - 500.0).ToArray()); // -500..+500
			RunTestMethod(Enumerable.Range(0, 365).Select(i => rnd.Next()).ToArray()); // big numbers
			RunTestMethod(Enumerable.Range(0, 365).Select(i => ((long) rnd.Next()) * rnd.Next()).ToArray());
			RunTestMethod(Enumerable.Range(0, 365).Select(i => (ulong) rnd.Next() * (ulong) rnd.Next()).ToArray());
			RunTestMethod(Enumerable.Range(0, 365).Select(i => Math.Round((rnd.NextDouble() - 0.5) * 500, 2, MidpointRounding.AwayFromZero)).ToArray()); // -500.00..+500.00

			// objects
			RunTestMethod(MediaContent.GetMedia1());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => MediaContent.GetMedia1()).ToArray());
			RunTestMethod(Enumerable.Range(0, 32).Select(i => MediaContent.GetMedia1()).ToArray());

			// sealed
			{
				var users = CrystalJson.LoadFrom<UserSealed[]>(MapPathRelativeToCallerSourcePath("Samples/Users.json"))!;
				RunTestMethod(users[0]);
				RunTestMethod(users.Take(7).ToArray());
				RunTestMethod(users.Take(100).ToArray());
			}

			// unsealed
			{
				var users = CrystalJson.LoadFrom<UserUnsealed[]>(MapPathRelativeToCallerSourcePath("Samples/Users.json"))!;
				RunTestMethod(users[0]);
				RunTestMethod(users.Take(7).ToArray());
				RunTestMethod(users.Take(100).ToArray());
			}

			// abstract
			{
				var users = CrystalJson.LoadFrom<UserDerived[]>(MapPathRelativeToCallerSourcePath("Samples/Users.json"))!.Cast<UserAbstract>().ToList();
				RunTestMethod(users[0]);
				RunTestMethod(users.Take(7).ToArray());
				RunTestMethod(users.Take(100).ToArray());
			}

		}

		#endregion

		#region Readers...

		[Test]
		public void Bench_Compare_Utf8_Readers()
		{
			// This test compares the different ways of parsing JSON: via strings, via Slices, or via a Stream, with ASCII vs UTF-8

			Log("# Warming up...");

			Slice bytesUtf8 = Slice.FromStringUtf8(@"{ ""Id"": 1234, ""Created"": ""2016-12-20T17:10:59.0662814+01:00"", ""Foo"": ""Hello"", ""Bar"": ""世界!"", ""ಠ_ಠ"": ""(╯°□°）╯︵ ┻━┻"", ""Baz"": false }");
			Log($"UTF8 : {bytesUtf8.ToString("P")}");
			Slice bytesAscii = Slice.FromStringAscii(@"{ ""Id"": 1234, ""Created"": ""2016-12-20T17:10:59.0662814+01:00"", ""Foo"": ""Hello"", ""Bar"": ""WD!"", ""O_O"": ""(/*v*)/ ^ +-+"", ""Baz"": false }");
			Log($"ASCII: {bytesAscii.ToString("P")}");

			//WARMUP:
			{
				Log(CrystalJson.Parse(bytesUtf8.ToStringUtf8()));
				Log(CrystalJson.Parse(bytesAscii.ToStringUtf8()));
				Log(CrystalJson.Parse(bytesUtf8));
				Log(CrystalJson.Parse(bytesAscii));
			}
			using (var sr = new StreamReader(new MemoryStream(bytesUtf8.Array, bytesUtf8.Offset, bytesUtf8.Count), Encoding.UTF8))
			{
				Log(CrystalJson.ParseFrom(sr));
			}
			using (var sr = new StreamReader(new MemoryStream(bytesAscii.Array, bytesAscii.Offset, bytesAscii.Count), Encoding.UTF8))
			{
				Log(CrystalJson.ParseFrom(sr));
			}

			FullGc();

			int iterations = 1_000_000;
#if DEBUG
			iterations /= 10;
#endif

			Log($"# Running test with {iterations:N0} iterations...");

			void Trace(string label, TimeSpan duration)
			{
				Log($"> {label,18} = {duration} ({iterations / duration.TotalSeconds:N0} ips)");
			}

			//ASCII

			string strAscii = bytesAscii.ToStringUtf8() ?? "";
			Trace("String:ASCII", Run_Bench_Utf8_ViaString(strAscii, iterations));
			FullGc();

			Trace("StreamReader:ASCII", Run_Bench_Utf8_ViaStreamReader(bytesAscii, iterations));
			FullGc();

			Trace("Slice:ASCII", Run_Bench_Utf8_ViaUtf8StringReader(bytesAscii, iterations));
			FullGc();

			// UTF8

			string strUtf8 = bytesUtf8.ToStringUtf8() ?? "";
			Trace("String:UTF8", Run_Bench_Utf8_ViaString(strUtf8, iterations));
			FullGc();

			Trace("StreamReader:UTF8", Run_Bench_Utf8_ViaStreamReader(bytesUtf8, iterations));
			FullGc();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static TimeSpan Run_Bench_Utf8_ViaString(string data, int iterations)
		{
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				_ = CrystalJson.Parse(data);
			}
			sw.Stop();
			return sw.Elapsed;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static TimeSpan Run_Bench_Utf8_ViaStreamReader(Slice data, int iterations)
		{
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				using var sr = new StreamReader(new MemoryStream(data.Array, data.Offset, data.Count), Encoding.UTF8);
				_ = CrystalJson.ParseFrom(sr);
			}
			sw.Stop();
			return sw.Elapsed;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static TimeSpan Run_Bench_Utf8_ViaUtf8StringReader(Slice data, int iterations)
		{
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				_ = CrystalJson.Parse(data);
			}
			sw.Stop();
			return sw.Elapsed;
		}

		#endregion

		[Test]
		public async Task Bench_Stream_Objects_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();
			var clock = new Stopwatch();

			// empty object
			Log("Saving simple flat object...");
			var path = GetTemporaryPath("empty.json");
			File.Delete(path);
#if NET5_0_OR_GREATER
			await using (var fs = File.Create(path))
#else
			// FileStream does not implement IAsyncDisposable on .NET Framework
			using (var fs = File.Create(path))
#endif
			{
				await using (var writer = new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, true))
				{
					using (writer.BeginObjectFragment(cancel))
					{
						//no-op
					}

					await writer.FlushAsync(cancel);
				}
				Assert.That(fs.CanWrite, Is.False, "Stream should have been closed");
			}

			// verify
			Log("> reloading...");
			var verify = CrystalJson.ParseFrom(path).AsObject();
			Dump(verify);
			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Count, Is.EqualTo(0), "Object should be empty!");

			// simple object (flat)
			Log("Saving simple flat object...");
			path = GetTemporaryPath("hello_world.json");
			File.Delete(path);
			var now = DateTimeOffset.Now;
			await using (var writer = CrystalJsonStreamWriter.Create(path))
			{
				using (var obj = writer.BeginObjectFragment(cancel))
				{
					obj.WriteField("Hello", "World");
					obj.WriteField("PowerLevel", 8001); // Over 8000 !!!!
					obj.WriteField("Date", now);
				}

				await writer.FlushAsync(cancel);
			}
			Log($"> {new FileInfo(path).Length:N0} bytes");

			// verify
			Log("> reloading...");
			verify = CrystalJson.ParseFrom(path).AsObject();
			Dump(verify);
			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Get<string>("Hello"), Is.EqualTo("World"), ".Hello");
			Assert.That(verify.Get<int>("PowerLevel"), Is.GreaterThan(8000).And.EqualTo(8001), ".PowerLevel");
			Assert.That(verify.Get<DateTimeOffset>("Date"), Is.EqualTo(now), ".Date");

			// object that contains a very large streamed array
			path = GetTemporaryPath("data.json");
			Log("Saving object with large streamed array...");
			File.Delete(path);
			clock.Restart();
			await using (var writer = CrystalJsonStreamWriter.Create(path))
			{
				using (var obj = writer.BeginObjectFragment(cancel))
				{
					obj.WriteField("Id", "FOOBAR9000");
					obj.WriteField("Date", now);

					using (var arr = obj.BeginArrayStream("Values"))
					{
						// we simulate a dump of 365 days woth of de data, with a precision of 1 minute, with a batch per day (1440 values)
						for (int i = 0; i < 365; i++)
						{
							var batch = Enumerable.Range(0, 1440).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), Math.Round(rnd.NextDouble() * 100000.0, 1)));
							await arr.WriteBatchAsync(batch);
						}
					}
				}

				await writer.FlushAsync(cancel);
			}
			clock.Stop();
			var sizeRaw = new FileInfo(path).Length;
			Log($"> {sizeRaw:N0} bytes in {clock.Elapsed.TotalMilliseconds:N1} ms");

			// verify
			Log("> reloading...");
			verify = CrystalJson.ParseFrom(path).AsObject();
			// too large to be dumped to the log!
			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Get<string>("Id"), Is.EqualTo("FOOBAR9000"), ".Id");
			Assert.That(verify.Get<DateTimeOffset>("Date"), Is.EqualTo(now), ".Date");
			var values = verify.GetArray("Values");
			Assert.That(values, Is.Not.Null, ".Values[]");
			Assert.That(values.Count, Is.EqualTo(365 * 1440), ".Values[] should have 365 fragments of 1440 values combined into a single array");
			Assert.That(values.GetElementsTypeOrDefault(), Is.EqualTo(JsonType.Array), ".Values[] should only contain arrays");
			Assert.That(values.AsArrays(), Is.All.Count.EqualTo(2), ".Values[] should only have arrays of size 2");

			// same deal, but with gzip compression
			Log("Saving object with large streamed array to compressed file...");
			File.Delete(path + ".gz");
			clock.Restart();
#if NET5_0_OR_GREATER
			await using (var fs = File.Create(path + ".gz"))
			await using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
#else
			// FileStream/GZipStream do not implement IAsyncDisposable on .NET Framework
			using (var fs = File.Create(path + ".gz"))
			using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
#endif
			await using (var writer = new CrystalJsonStreamWriter(gz, CrystalJsonSettings.Json))
			{
				using (var obj = writer.BeginObjectFragment(cancel))
				{
					obj.WriteField("Id", "FOOBAR9000");
					obj.WriteField("Date", now);
					using (var arr = obj.BeginArrayStream("Values"))
					{
						// we simulate a dump of 365 days worth of de data, with a precision of 1 minute, with a batch per day (1440 values)
						for (int i = 0; i < 365; i++)
						{
							var batch = Enumerable.Range(0, 1440).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), Math.Round(rnd.NextDouble() * 100000.0, 1)));
							await arr.WriteBatchAsync(batch);
						}
					}
				}
				await writer.FlushAsync(cancel);
			}
			clock.Stop();
			var sizeCompressed = new FileInfo(path + ".gz").Length;
			Log($"> {sizeCompressed:N0} bytes in {clock.Elapsed.TotalMilliseconds:N1} ms (1 : {(double) sizeRaw / sizeCompressed:N2})");
			Assert.That(sizeCompressed, Is.LessThan(sizeRaw / 2), "Compressed file should be AT MINMUM 50% smaller than original");

			// verify
			Log("> reloading...");
#if NET5_0_OR_GREATER
			await using(var fs = File.OpenRead(path + ".gz"))
			await using (var gs = new GZipStream(fs, CompressionMode.Decompress, false))
#else
			// FileStream/GZipStream do not implement IAsyncDisposable on .NET Framework
			using (var fs = File.OpenRead(path + ".gz"))
			using (var gs = new GZipStream(fs, CompressionMode.Decompress, false))
#endif
			{
				verify = CrystalJson.ParseFrom(gs).AsObject();
			}

			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Get<string>("Id"), Is.EqualTo("FOOBAR9000"), ".Id");
			Assert.That(verify.Get<DateTimeOffset>("Date"), Is.EqualTo(now), ".Date");
			values = verify.GetArray("Values");
			Assert.That(values, Is.Not.Null, ".Values[]");
			Assert.That(values.Count, Is.EqualTo(365 * 1440), ".Values[] should have 365 fragments of 1440 values combined into a single array");
			Assert.That(values.GetElementsTypeOrDefault(), Is.EqualTo(JsonType.Array), ".Values[] should only contain arrays");
			Assert.That(values.AsArrays(), Is.All.Count.EqualTo(2), ".Values[] should only have arrays of size 2");
		}

	}

	#region Models...

	public sealed record MediaContent
	{

		public required Media Media { get; init; }

		public List<Image>? Images { get; init; }

		public bool Equals(MediaContent? other)
		{
			if (ReferenceEquals(other, this)) return true;
			if (other == null) return false;
			if (!this.Media.Equals(other.Media)) return false;
			if (this.Images == null || other.Images == null) return this.Images == other.Images;
			if (this.Images.Count != other.Images.Count) return false;
			return this.Images.Zip(other.Images, (left, right) => left.Equals(right)).All(b => b);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return this.Media.GetHashCode();
		}

		public void Manual(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();

			writer.WriteName("Media");
			this.Media.Manual(writer);

			if (this.Images != null && this.Images.Count > 0)
			{
				writer.WriteName("Images");
				var state2 = writer.BeginArray();
				foreach (var image in this.Images)
				{
					writer.WriteFieldSeparator();
					image.Manual(writer);
				}
				writer.EndArray(state2);
			}

			writer.EndObject(state);
		}

		/// <summary>The standard test value.</summary>
		private static readonly MediaContent Media1 = new MediaContent
		{
			Media = new()
			{
				Uri = "http://javaone.com/keynote.mpg",
				Title = "Javaone Keynote",
				Width = 640,
				Height = 480,
				Format = "video/mpg4",
				Duration = 18000000, // half hour in milliseconds
				Size = 58982400, // bitrate * duration in seconds / 8 bits per byte
				Bitrate = 262144, // 256k
				HasBitrate = true,
				Persons = [ "Bill Gates", "Steve Jobs" ],
				Player = Media.PlayerType.Java,
			},
			Images =
			[
				new() { Uri = "http://javaone.com/keynote_large.jpg", Title = "Javaone Keynote", Width = 1024, Height = 768, Size = Image.ImageSize.Large },
				new() { Uri = "http://javaone.com/keynote_small.jpg", Title = "Javaone Keynote", Width = 320, Height = 240, Size = Image.ImageSize.Small },
			]
		};

		public static MediaContent GetMedia1(bool copy = false) => copy ? MediaContent.Media1 with { } : MediaContent.Media1;
		
	}

	public sealed record Media
	{
		public enum PlayerType
		{
			Java, Flash
		}

		public required string Uri { get; init; }
		public string? Title { get; init; }
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required string Format { get; init; }
		public required long Duration { get; init; }
		public required long Size { get; init; }
		// ReSharper disable IdentifierTypo
		public int Bitrate { get; init; }
		public bool HasBitrate { get; init; }
		// ReSharper restore IdentifierTypo
		public List<string>? Persons { get; init; }
		public PlayerType Player { get; init; }
		public string? Copyright { get; init; }

		public bool Equals(Media? other)
		{
			if (ReferenceEquals(other, this)) return true;
			return other != null
				&& this.Uri == other.Uri
				&& this.Title == other.Title
				&& this.Width == other.Width
				&& this.Height == other.Height
				&& this.Format == other.Format
				&& this.Duration == other.Duration
				&& this.Size == other.Size
				&& this.Bitrate == other.Bitrate
				&& this.HasBitrate == other.HasBitrate
				&& (this.Persons ?? []).Zip(other.Persons ?? [], (left, right) => left == right).All(b => b)
				&& this.Player == other.Player
				&& this.Copyright == other.Copyright;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return this.Uri.GetHashCode();
		}

		public void Manual(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			writer.WriteField("Uri", this.Uri);
			if (this.Title != null) writer.WriteField("Title", this.Title);
			writer.WriteField("Width", this.Width);
			writer.WriteField("Height", this.Height);
			writer.WriteField("Format", this.Format);
			writer.WriteField("Duration", this.Duration);
			writer.WriteField("Size", this.Size);
			if (this.HasBitrate)
			{
				writer.WriteField("HasBitrate", true);
				writer.WriteField("Bitrate", this.Bitrate);
			}
			if (this.Persons != null) writer.WriteFieldArray("Persons", this.Persons);
			writer.WriteField("Player", this.Player);
			if (this.Copyright != null) writer.WriteField("Copyright", this.Copyright);
			writer.EndObject(state);
		}

	}

	public sealed record Image
	{
		public enum ImageSize
		{
			Small, Large
		}

		public required string Uri { get; init; }
		public string? Title { get; init; } // Can be null
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required ImageSize Size { get; init; }

		public bool Equals(Image? other)
		{
			return other != null
				&& this.Uri == other.Uri
				&& this.Title == other.Title
				&& this.Width == other.Width
				&& this.Height == other.Height
				&& this.Size == other.Size;
		}

		public override int GetHashCode()
		{
			return this.Uri.GetHashCode();
		}

		public void Manual(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			writer.WriteField("Uri", this.Uri);
			if (this.Title != null) writer.WriteField("Title", this.Title);
			writer.WriteField("Width", this.Width);
			writer.WriteField("Height", this.Height);
			writer.WriteField("Size", this.Size);
			writer.EndObject(state);
		}
	}

	public sealed record UserSealed
	{
		public required Guid Id { get; init; }
		public required string FirstName { get; init; }
		public required string LastName { get; init; }
		public required string Email { get; init; }
		public required string Country { get; init; }
		public required string IpAddress { get; init; }
		public required bool IsActive { get; init; }
		public required DateTime Joined { get; init; }
	}

	public record UserUnsealed
	{
		public required Guid Id { get; init; }
		public required string FirstName { get; init; }
		public required string LastName { get; init; }
		public required string Email { get; init; }
		public required string Country { get; init; }
		public required string IpAddress { get; init; }
		public required bool IsActive { get; init; }
		public required DateTime Joined { get; init; }
	}

	[JsonPolymorphic]
	[JsonDerivedType(typeof(UserDerived), "user")]
	public abstract record UserAbstract
	{
		public required Guid Id { get; init; }
		public required string FirstName { get; init; }
		public required string LastName { get; init; }
		public required string Email { get; init; }
		public required string Country { get; init; }
		public required string IpAddress { get; init; }
		public required bool IsActive { get; init; }
		public required DateTime Joined { get; init; }
	}

	public record UserDerived : UserAbstract;

	#endregion

}
