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

// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable StringLiteralTypo

namespace FoundationDB.Client.Tests
{

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class KeyFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbKey_Constants()
		{
			Assert.Multiple(() =>
				{
					Assert.That(FdbKey.MinValue.ToArray(), Is.EqualTo(new byte[] { 0 }));
					Assert.That(FdbKey.MaxValue.ToArray(), Is.EqualTo(new byte[] { 255 }));
					Assert.That(FdbKey.DirectoryPrefix.ToArray(), Is.EqualTo(new byte[] { 254 }));
					Assert.That(FdbKey.SystemPrefix.ToArray(), Is.EqualTo(new byte[] { 255 }));

					Assert.That(FdbKey.MinValueSpan.ToArray(), Is.EqualTo(new byte[] { 0 }));
					Assert.That(FdbKey.MaxValueSpan.ToArray(), Is.EqualTo(new byte[] { 255 }));
					Assert.That(FdbKey.DirectoryPrefixSpan.ToArray(), Is.EqualTo(new byte[] { 254 }));
					Assert.That(FdbKey.SystemPrefixSpan.ToArray(), Is.EqualTo(new byte[] { 255 }));
					Assert.That(FdbKey.SystemEndSpan.ToArray(), Is.EqualTo(new byte[] { 255, 255 }));
				}
			);

			Assert.Multiple(() =>
				{
					Assert.That(Fdb.System.Coordinators.ToString(), Is.EqualTo("<FF>/coordinators"));
					Assert.That(Fdb.System.KeyServers.ToString(), Is.EqualTo("<FF>/keyServers/"));
					Assert.That(Fdb.System.MinValue.ToString(), Is.EqualTo("<FF><00>"));
					Assert.That(Fdb.System.MaxValue.ToString(), Is.EqualTo("<FF><FF>"));
					Assert.That(Fdb.System.ServerKeys.ToString(), Is.EqualTo("<FF>/serverKeys/"));
					Assert.That(Fdb.System.ServerList.ToString(), Is.EqualTo("<FF>/serverList/"));
					Assert.That(Fdb.System.BackupDataFormat.ToString(), Is.EqualTo("<FF>/backupDataFormat"));
					Assert.That(Fdb.System.InitId.ToString(), Is.EqualTo("<FF>/init_id"));
					Assert.That(Fdb.System.ConfigKey("hello").ToString(), Is.EqualTo("<FF>/conf/hello"));
					Assert.That(Fdb.System.GlobalsKey("world").ToString(), Is.EqualTo("<FF>/globals/world"));
					Assert.That(Fdb.System.WorkersKey("foo", "bar").ToString(), Is.EqualTo("<FF>/workers/foo/bar"));
				}
			);
		}

		[Test]
		public void Test_FdbKey_Increment()
		{
			Assert.That(FdbKey.Increment(Literal("Hello")).ToString(), Is.EqualTo("Hellp"));
			Assert.That(FdbKey.Increment(Literal("Hello\x00")).ToString(), Is.EqualTo("Hello<01>"));
			Assert.That(FdbKey.Increment(Literal("Hello\xFE")).ToString(), Is.EqualTo("Hello<FF>"));
			Assert.That(FdbKey.Increment(Literal("Hello\xFF")).ToString(), Is.EqualTo("Hellp"), "Should remove trailing \\xFF");
			Assert.That(FdbKey.Increment(Literal("A\xFF\xFF\xFF")).ToString(), Is.EqualTo("B"), "Should truncate all trailing \\xFFs");

			// corner cases
			Assert.That(() => FdbKey.Increment(Slice.Nil), Throws.InstanceOf<ArgumentException>().With.Property("ParamName").EqualTo("key"));
			Assert.That(() => FdbKey.Increment(Slice.Empty), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => FdbKey.Increment(Literal("\xFF")), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKey_Merge()
		{
			// get a bunch of random slices
			var rnd = new Random();
			var slices = Enumerable.Range(0, 16).Select(_ => Slice.Random(rnd, 4 + rnd.Next(32))).ToArray();

			var merged = FdbKey.Merge(Slice.FromByte(42), slices);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(slices.Length));

			for (int i = 0; i < slices.Length; i++)
			{
				var expected = Slice.FromByte(42) + slices[i];
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}

			// corner cases
			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(() => FdbKey.Merge(Slice.Empty, null!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("keys"));
			Assert.That(() => FdbKey.Merge(Slice.Empty, default(IEnumerable<Slice>)!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("keys"));
			// ReSharper restore AssignNullToNotNullAttribute
		}

		[Test]
		public void Test_FdbKey_BatchedRange()
		{
			// we want numbers from 0 to 99 in 5 batches of 20 contiguous items each

			IEnumerable<IEnumerable<int>> query = FdbKey.BatchedRange(0, 100, 20);
			Assert.That(query, Is.Not.Null);

			var batches = query.ToArray();
			Assert.That(batches, Is.Not.Null);
			Assert.That(batches.Length, Is.EqualTo(5));
			Assert.That(batches, Is.All.Not.Null);

			// each batch should be an enumerable that will return 20 items each
			for (int i = 0; i < batches.Length; i++)
			{
				var items = batches[i].ToArray();
				Assert.That(items, Is.Not.Null.And.Length.EqualTo(20));
				for (int j = 0; j < items.Length; j++)
				{
					Assert.That(items[j], Is.EqualTo(j + i * 20));
				}
			}
		}

		[Test]
		public async Task Test_FdbKey_Batched()
		{
			// we want numbers from 0 to 999 split between 5 workers that will consume batches of 20 items at a time
			// > we get 5 enumerables that all take ranges from the same pool and all complete where there is no more values

			const int N = 1000;
			const int B = 20;
			const int W = 5;

			IEnumerable<IEnumerable<KeyValuePair<int, int>>> query = FdbKey.Batched(0, N, W, B);
			Assert.That(query, Is.Not.Null);

			var batches = query.ToArray();
			Assert.That(batches, Is.Not.Null);
			Assert.That(batches.Length, Is.EqualTo(W));
			Assert.That(batches, Is.All.Not.Null);

			var used = new bool[N];

			var signal = new TaskCompletionSource<object?>();

			// each batch should return new numbers
			var tasks = batches.Select(async (iterator, id) =>
				{
					// force async
					await signal.Task.ConfigureAwait(false);

					foreach (var chunk in iterator)
					{
						// kvp = (offset, count)
						// > count should always be 20
						// > offset should always be a multiple of 20
						// > there should never be any overlap between workers
						Assert.That(chunk.Value, Is.EqualTo(B), $"{chunk.Key}:{chunk.Value}");
						Assert.That(chunk.Key % B, Is.EqualTo(0), $"{chunk.Key}:{chunk.Value}");

						lock (used)
						{
							for (int i = chunk.Key; i < chunk.Key + chunk.Value; i++)
							{

								if (used[i])
									Assert.Fail($"Duplicate index {i} chunk {chunk.Key}:{chunk.Value} for worker {id}");
								else
									used[i] = true;
							}
						}

						await Task.Delay(1).ConfigureAwait(false);
					}
				}
			).ToArray();

			ThreadPool.UnsafeQueueUserWorkItem((_) => signal.TrySetResult(null), null);

			await Task.WhenAll(tasks);

			Assert.That(used, Is.All.True);
		}

	}

}
