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

// ReSharper disable RedundantArgumentDefaultValue

namespace SnowBank.Data.Json.Tests
{
	using System.IO.Compression;

	public partial class CrystalJsonTest
	{

		[Test]
		public async Task Test_Can_Stream_Arrays_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();

			// empty
			var path = GetTemporaryPath("null.json");
#if NET5_0_OR_GREATER
			await using (var fs = File.Create(path))
#else
			// FileStream does not implement IAsyncDisposable on .NET Framework
			using (var fs = File.Create(path))
#endif
			{
				await using (new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, ownStream: false))
				{
					//nothing
				}
				Assert.That(fs.CanWrite, Is.True, "Stream should be kept open (ownStream: false)");
			}
			var arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Null);

			// empty batch
			path = GetTemporaryPath("empty.json");
#if NET5_0_OR_GREATER
			await using (var fs = File.Create(path))
#else
			// FileStream does not implement IAsyncDisposable on .NET Framework
			using (var fs = File.Create(path))
#endif
			{
				await using (var stream = new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, ownStream: true))
				{
					using (stream.BeginArrayFragment())
					{
						//no-op
					}

					await stream.FlushAsync(cancel);
				}
				Assert.That(fs.CanWrite, Is.False, "Stream should have been closed (ownStream: true)");
			}
			arr = CrystalJson.LoadFrom<List<int>>(path)!;
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.Empty);

			// single element
			path = GetTemporaryPath("forty_two.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				using (var array = stream.BeginArrayFragment(cancel))
				{
					await array.WriteItemAsync(42);
				}
				await stream.FlushAsync(cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo([ 42 ]));

			// single batch
			path = GetTemporaryPath("one_batch.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(Enumerable.Range(0, 1000), cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// multiple batches
			path = GetTemporaryPath("multiple_batches.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 10; i++)
					{
						await array.WriteBatchAsync(Enumerable.Range(i * 100, 100));
					}
				}, cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// changes types during the sequence (first 500 are ints, next 500 are longs)
			path = GetTemporaryPath("int_long.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					await array.WriteBatchAsync<int>(Enumerable.Range(0, 500));
					await array.WriteBatchAsync<long>(Enumerable.Range(500, 500).Select(x => (long)x));
				}, cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// guids
			path = GetTemporaryPath("guids.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 100; i++)
					{
						var batch = Enumerable.Range(0, rnd.Next(100)).Select(_ => Guid.NewGuid());
						await array.WriteBatchAsync(batch);
					}
				}, cancel);
			}
			var guids = CrystalJson.LoadFrom<List<Guid>>(path);
			Assert.That(guids, Is.Not.Null);

			// anonymous types
			path = GetTemporaryPath("objects.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 100; i++)
					{
						var batch = Enumerable.Range(i * 100, rnd.Next(100));
						await array.WriteBatchAsync(batch, (x) => new { Id = Guid.NewGuid(), Index = x, Score = Math.Round(rnd.NextDouble() * 100, 3), Rnd = Stopwatch.GetTimestamp() });
					}
				}, cancel);
			}
			//TODO: verify!

			// compress
			path = GetTemporaryPath("objects.json.gz");
#if NET5_0_OR_GREATER
			await using (var fs = File.Create(path + ".gz"))
			await using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
#else
			// FileStream/GZipStream do not implement IAsyncDisposable on .NET Framework
			using (var fs = File.Create(path + ".gz"))
			using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
#endif
			await using (var stream = new CrystalJsonStreamWriter(gz, CrystalJsonSettings.Json))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 100; i++)
					{
						var batch = Enumerable.Range(i * 100, rnd.Next(100));
						await array.WriteBatchAsync(batch, (x) => new { Id = Guid.NewGuid(), Index = x, Score = Math.Round(rnd.NextDouble() * 100, 3), Rnd = Stopwatch.GetTimestamp() });
					}
				}, cancel);
			}
			//TODO: verify!
		}

		[Test]
		public async Task Test_Can_Stream_Multiple_Fragments_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();

			// empty batch
			Log("Saving empty batches...");
			var path = GetTemporaryPath("three_empty_objects.json");
			await using (var writer = CrystalJsonStreamWriter.Create(path, CrystalJsonSettings.Json))
			{
				using (writer.BeginObjectFragment(cancel))
				{
					//no-op
				}
				using (writer.BeginObjectFragment(cancel))
				{
					//no-op
				}
				using (writer.BeginObjectFragment(cancel))
				{
					//no-op
				}
				await writer.FlushAsync(cancel);
			}
			Log("> done");

			// object meta + data series
			path = GetTemporaryPath("device.json");
			Log("Saving multi-fragments 'export'...");
			await using (var writer = CrystalJsonStreamWriter.Create(path, CrystalJsonSettings.Json))
			{
				var metric = new {
					Id = "123ABC",
					Vendor = "ACME",
					Model = "HAL 9001",
					Metrics = new[] { "Foo", "Bar", "Baz" },
				};

				// first obj = meta containing an array of ids
				await writer.WriteFragmentAsync(metric, cancel);

				// next, une array for each id in the first array
				foreach(var _ in metric.Metrics)
				{
					using var arr = writer.BeginArrayFragment(cancel);

					await arr.WriteBatchAsync(Enumerable.Range(0, 10).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), rnd.Next())));
				}
			}
			Log($"> saved {new FileInfo(path).Length:N0} bytes");

			// read back
			Log("> reloading...");
			using (var reader = CrystalJsonStreamReader.Open(path))
			{
				// metrics meta
				Log("> Reading metadata object...");
				var frag = reader.ReadNextFragment()!;
				DumpCompact(frag);
				Assert.That(frag, Is.Not.Null);
				Assert.That(frag.Type, Is.EqualTo(JsonType.Object));
				var m = (JsonObject)frag;
				Assert.That(m.Get<string>("Id"), Is.EqualTo("123ABC"));
				Assert.That(m.Get<string>("Vendor"), Is.EqualTo("ACME"));
				Assert.That(m.Get<string>("Model"), Is.EqualTo("HAL 9001"));
				Assert.That(m.Get<string[]>("Metrics"), Has.Length.EqualTo(3));

				// metrics value
				foreach (var id in m.GetArray("Metrics").AsArrayOf<string>())
				{
					Log($"> Reading batch for {id}...");
					frag = reader.ReadNextFragment()!;
					DumpCompact(frag);
					Assert.That(frag, Is.Not.Null);
					Assert.That(frag.Type, Is.EqualTo(JsonType.Array));
					var a = (JsonArray)frag;
					Log($"> {a.Count}");
					Assert.That(a, Has.Count.EqualTo(10));
				}

				// end of file
				frag = reader.ReadNextFragment();
				Assert.That(frag, Is.Null);
			}

		}

	}

}
