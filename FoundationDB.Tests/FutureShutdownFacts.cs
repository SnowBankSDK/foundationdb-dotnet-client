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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client.Utils;

	/// <summary>Tests of the deterministic shutdown drain: <see cref="Fdb.Stop"/> must settle every outstanding
	/// future (including watches, which pend indefinitely) and release every callback cookie BEFORE stopping the
	/// network thread, instead of killing the thread under armed callbacks.</summary>
	/// <remarks>Explicit: <see cref="Fdb.Stop"/> is process-wide and would kill the network for every subsequent
	/// fixture; run this standalone (a filtered run is its own process):
	/// <c>dotnet exec ...FoundationDB.Tests.dll --filter "FullyQualifiedName~FutureShutdownFacts"</c></remarks>
	[TestFixture]
	[Explicit("Calls Fdb.Stop, which kills the fdb network thread for the whole process; run standalone.")]
	[NonParallelizable]
	[Category("Fdb-Client-Live")]
	public class FutureShutdownFacts : FdbTest
	{

		[Test]
		public async Task Test_Fdb_Stop_Drains_Outstanding_Futures_Deterministically()
		{
			// arm futures that will still be pending when Stop runs:
			// - watches pend until their key changes (i.e. forever, here)
			// - the transactions are deliberately NOT disposed (the mass-extinction scenario of issue #48)
			var db = await OpenTestPartitionAsync();
			var location = db.Root;

			var watches = new List<FdbWatch>();
			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				for (int i = 0; i < 8; i++)
				{
					tr.Set(subspace.Key("watched", i), Slice.FromInt32(i));
				}
			}, this.Cancellation);

			await db.ReadWriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				for (int i = 0; i < 8; i++)
				{
					watches.Add(tr.Watch(subspace.Key("watched", i), this.Cancellation));
				}
				return default(object?);
			}, this.Cancellation);

			Assert.That(watches, Has.Count.EqualTo(8));
			Assert.That(watches.TrueForAll(w => w.IsAlive), Is.True, "watches should be pending before the shutdown");

			long armedBefore = Volatile.Read(ref DebugCounters.CallbackHandles);
			Assert.That(armedBefore, Is.GreaterThanOrEqualTo(8), "the watch callbacks should be armed");

			// the drain under test
			var sw = System.Diagnostics.Stopwatch.StartNew();
			Fdb.Stop();
			sw.Stop();

			Log($"Fdb.Stop() returned in {sw.ElapsedMilliseconds:N0} ms");

			// every watch must have settled (canceled), not be left hanging on a dead network thread
			foreach (var watch in watches)
			{
				Assert.That(watch.Task.IsCompleted, Is.True, "a watch was left pending after Fdb.Stop");
				Assert.That(watch.Task.IsCanceled, Is.True, "a drained watch should surface as canceled");
			}

			// and every callback cookie must have been released (no leaked GCHandles / native futures)
			Assert.That(Volatile.Read(ref DebugCounters.CallbackHandles), Is.Zero, "all callback cookies should have been released by the drain");

			// bounded: the drain must not have burned the whole fallback timeout
			Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the drain should complete promptly, not by timeout");
		}

	}

}
