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

namespace FoundationDB.Testing.Tests
{
	using FoundationDB.Client;
	using Microsoft.Extensions.Time.Testing;

	/// <summary>Tests that a watch idle-timeout is measured on the database <see cref="System.TimeProvider"/>, so a FakeDb
	/// built on a fake clock can drive the timeout with virtual time instead of the wall clock.</summary>
	[TestFixture]
	[Category("FakeDb-Client")]
	public class FakeDbWatchClockFacts : FakeDbTest
	{

		[Test]
		public async Task Test_Watch_WaitAsync_Timeout_Fires_On_The_Database_Clock()
		{
			// FdbWatch.WaitAsync(timeout, ct) must measure its idle timeout on the database's time provider. A FakeDb built
			// on a fake clock then drives the timeout with virtual time: advancing past the timeout completes the wait with
			// false, without ever touching the real wall clock.

			var fake = new FakeTimeProvider();
			var store = new FakeDbStore(time: fake);
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("v1")), this.Cancellation);

			FdbWatch watch;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				watch = tr.Watch(Key("k1"), this.Cancellation);
				await tr.CommitAsync();
			}
			Assert.That(watch.Task.IsCompleted, Is.False, "the freshly-armed watch is pending: nothing has changed yet");

			// wait on the watch with a 30 s timeout; the whole test runs in well under a real second, so the real wall
			// clock never reaches it and only virtual time can complete the wait
			var timeout = TimeSpan.FromSeconds(30);
			var waitTask = watch.WaitAsync(timeout, this.Cancellation);

			// before the timeout elapses on the virtual clock, the wait stays pending
			await AdvanceAndPump(fake, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
			Assert.That(waitTask.IsCompleted, Is.False, "the virtual clock has not reached the timeout yet");

			// crossing the timeout on the database clock completes the wait, reporting the watch did not fire
			await AdvanceAndPump(fake, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(5));
			Assert.That(waitTask.IsCompleted, Is.True, "the timeout elapsed on the database clock, so the wait completed");
			Assert.That(await waitTask, Is.False, "the watch did not fire; WaitAsync reports the timeout");
		}

	}

}
