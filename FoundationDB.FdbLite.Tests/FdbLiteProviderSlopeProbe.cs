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

namespace FoundationDB.FdbLite.Tests
{
	using System.Buffers.Binary;
	using System.Diagnostics;
	using FoundationDB.Client;
	using FoundationDB.Storage;

	/// <summary>Slope probes for the provider path (the emulated database over the fdblite engine): per-commit and per-read cost as the store grows.</summary>
	/// <remarks>Probes, not gates: they print slopes for the charter log, and assert only that the measured mechanism actually ran.</remarks>
	[TestFixture]
	[Explicit("perf probe: prints per-round commit and per-op selector slopes, run by hand")]
	[Category("Benchmark")]
	public class FdbLiteProviderSlopeProbe : SimpleTest
	{

		private static byte[] CounterKey(int i)
		{
			var key = new byte[20];
			"\xFE/counters/"u8.CopyTo(key);
			BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(16), i);
			return key;
		}

		/// <summary>Wall time and allocation per full-keyset replacement round through the provider path, with and without version retention.</summary>
		[Test]
		public async Task Probe_Commit_Slope_Through_The_Provider()
		{
			const int KEYS = 20_000;
			const int ROUNDS = 10;
			const int BATCH = 2_000;

			foreach (bool retain in new[] { false, true })
			{
				using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Default, retainEveryVersion: retain);
				using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

				for (int start = 0; start < KEYS; start += BATCH)
				{
					using var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
					for (int i = start; i < start + BATCH; i++)
					{
						tr.Set(Slice.FromBytes(CounterKey(i)), Slice.FromInt64(0));
					}
					await tr.CommitAsync();
				}

				for (int round = 1; round <= ROUNDS; round++)
				{
					long alloc0 = GC.GetTotalAllocatedBytes(precise: true);
					var sw = Stopwatch.StartNew();
					for (int start = 0; start < KEYS; start += BATCH)
					{
						using var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
						for (int i = start; i < start + BATCH; i++)
						{
							tr.Set(Slice.FromBytes(CounterKey(i)), Slice.FromInt64(round));
						}
						await tr.CommitAsync();
					}
					sw.Stop();
					long allocMb = (GC.GetTotalAllocatedBytes(precise: true) - alloc0) / (1024 * 1024);
					Log($"# retain={retain} round={round} ms={sw.ElapsedMilliseconds} allocMB={allocMb}");
				}

				using var read = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
				var all = await read.GetRangeAsync(Slice.FromBytes(CounterKey(0)), Slice.FromBytes(CounterKey(KEYS)), new() { Limit = KEYS + 10 });
				Assert.That(all.Count, Is.EqualTo(KEYS), $"retain={retain}: every counter must read back");
			}
		}

		/// <summary>Per-op selector resolution in a clean transaction (committed fast path) against one holding a pending write (merged path), as the store grows.</summary>
		[Test]
		public async Task Probe_Selector_Resolution_Slope()
		{
			const int OPS = 500;

			foreach (int keys in new[] { 1_000, 10_000, 50_000 })
			{
				using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Default, retainEveryVersion: false);
				using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

				for (int start = 0; start < keys; start += 1_000)
				{
					using var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
					for (int i = start; i < start + 1_000; i++)
					{
						tr.Set(Slice.FromBytes(CounterKey(i)), Slice.FromInt64(0));
					}
					await tr.CommitAsync();
				}

				var rnd = new Random(4632);
				double clean = await MeasureGetKeys(db, keys, OPS, pendingWrite: false, rnd);
				double merged = await MeasureGetKeys(db, keys, OPS, pendingWrite: true, rnd);
				Log($"# keys={keys:N0} getKey clean={clean:F1} us/op merged={merged:F1} us/op ratio={merged / clean:F1}x");
			}
		}

		private async Task<double> MeasureGetKeys(FdbDatabase db, int keys, int ops, bool pendingWrite, Random rnd)
		{
			using var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
			if (pendingWrite)
			{ // a single pending mutation flips every later selector resolution onto the merged path
				tr.Set(Slice.FromBytes(CounterKey(0)), Slice.FromInt64(-1));
			}
			// warmup resolution, outside the timed window
			_ = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(Slice.FromBytes(CounterKey(rnd.Next(keys)))));
			var sw = Stopwatch.StartNew();
			for (int n = 0; n < ops; n++)
			{
				_ = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(Slice.FromBytes(CounterKey(rnd.Next(keys)))));
			}
			sw.Stop();
			return sw.Elapsed.TotalMicroseconds / ops;
		}

	}

}
