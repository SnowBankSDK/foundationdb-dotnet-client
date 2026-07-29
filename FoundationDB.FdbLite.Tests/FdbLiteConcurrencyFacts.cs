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
	using System.Buffers.Binary;
	using System.Collections.Concurrent;
	using FoundationDB.Storage.FdbLite;

	/// <summary>Bounded smoke for the documented contract: read pins may be taken from ANY thread, concurrently with commits and with cold region mappings.</summary>
	/// <remarks>This is a smoke, not a proof: it exercises the torn-header window (the durable header now flips under the pin lock) and the cold first touch of file regions (the region array is an immutable published snapshot), the two places a racing reader used to be able to observe broken state.</remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteConcurrencyFacts : SimpleTest
	{

		private static string NewStorePath()
		{
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-tests");
			Directory.CreateDirectory(dir);
			return Path.Combine(dir, $"store-{Guid.NewGuid():N}.sbkv");
		}

		private static void DeleteQuietly(string path)
		{
			try { File.Delete(path); } catch { }
		}

		private static byte[] Key(int i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		[Test]
		public void Concurrent_Readers_Survive_Commits_And_Cold_Regions()
		{
			var path = NewStorePath();
			try
			{
				var geometry = FdbLiteGeometry.Uniform(14);
				// 1 MiB regions force many COLD region mappings while the readers run
				using var engine = FdbLiteEngine.OpenOrCreateFile(path, geometry, regionSizeInBytes: 1 << 20);

				const int N = 50_000;
				var value = new byte[64];
				var seed = engine.BeginWrite();
				for (int i = 0; i < N; i++)
				{
					seed.Insert(Key(i), value);
				}
				engine.Commit(seed, 1);

				using var stop = new CancellationTokenSource();
				var failures = new ConcurrentQueue<Exception>();
				var readers = new Thread[4];
				for (int t = 0; t < readers.Length; t++)
				{
					readers[t] = new Thread(seedObj =>
					{
						var rnd = new Random(1000 + (int) seedObj!);
						try
						{
							while (!stop.IsCancellationRequested)
							{
								var pin = engine.BeginRead();
								try
								{
									for (int probe = 0; probe < 64; probe++)
									{
										int i = rnd.Next(N);
										if (!FdbLiteTreeReader.TryGetValue(engine.Pager, pin.RootPageId, Key(i), out var v) || v.Length != 64)
										{
											throw new InvalidOperationException($"key {i} unreadable under a pin of generation {pin.Generation}");
										}
									}
								}
								finally
								{
									engine.EndRead(in pin);
								}
							}
						}
						catch (Exception e)
						{
							failures.Enqueue(e);
						}
					});
					readers[t].Start(t);
				}

				// concurrent commits: same-length replaces churn the tree through copy-on-write, so the file
				// keeps growing into fresh (cold) regions while the readers are probing
				for (ulong gen = 2; gen <= 20; gen++)
				{
					var w = engine.BeginWrite();
					for (int i = 0; i < N; i += 25)
					{
						w.Insert(Key(i), value);
					}
					engine.Commit(w, gen);
				}

				stop.Cancel();
				foreach (var r in readers)
				{
					r.Join();
				}
				Assert.That(failures, Is.Empty, () => $"reader failures:\n{string.Join("\n", failures.Select(f => f.ToString()))}");
			}
			finally
			{
				DeleteQuietly(path);
			}
		}

	}

}
