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

// ReSharper disable AccessToDisposedClosure
namespace FoundationDB.Testing.Tests
{
	using FoundationDB.Client;
	using Microsoft.Extensions.Time.Testing;

	/// <summary>FakeDb-specific transaction tests that have no real-cluster equivalent (virtual clock, store internals).</summary>
	/// <remarks>The dual-backend transaction conformance tests live in <c>Conformance/TransactionConformanceFacts.cs</c>; this fixture is only for behaviors that exist solely on the emulator.</remarks>
	[TestFixture]
	public class FakeDbTransactionFacts : FakeDbTest
	{

		[Test]
		public async Task Test_Retry_Backoff_Runs_On_Virtual_Time()
		{
			// A retryable error backs off on the store's TimeProvider: with a fake clock and a realistic
			// RetryDelayMaximum, the retry loop STALLS while virtual time is frozen (however long we really wait) and
			// proceeds only when virtual time advances. The default policy (RetryDelayMaximum == 0) retries instantly,
			// keeping normal tests fast.

			var fake = new FakeTimeProvider();
			var store = new FakeDbStore(730, time: fake)
			{
				RetryDelayInitial = TimeSpan.FromMilliseconds(50),
				RetryDelayMaximum = TimeSpan.FromSeconds(1),
			};
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			int attempts = 0;
			var op = db.WriteAsync(
				tr =>
				{
					attempts++;
					if (attempts <= 2) throw new FdbException(FdbError.NotCommitted, "simulated conflict");
					// succeed on the 3rd attempt (an empty write commits fine)
				},
				this.Cancellation
			);

			// the first backoff is on VIRTUAL time: a generous real settle must not let the loop past the first delay
			await Wait(200);
			Assert.That(op.IsCompleted, Is.False, "the retry backoff must be gated by virtual time, not the wall clock");
			Assert.That(attempts, Is.EqualTo(1), "the retry loop is parked in its first backoff");

			// advancing virtual time past the backoffs lets the loop retry and finally commit on the 3rd attempt
			await AdvanceAndPump(fake, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(250));
			for (int i = 0; i < 100 && !op.IsCompleted; i++)
			{
				await Wait(10);
			}
			Assert.That(op.IsCompleted, Is.True, "advancing virtual time must release the backoff and complete the retry loop");
			await op;
			Assert.That(attempts, Is.EqualTo(3), "two retryable failures, then success");
		}

	}

}
