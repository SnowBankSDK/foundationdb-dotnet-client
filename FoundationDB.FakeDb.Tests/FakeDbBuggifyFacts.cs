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
	using System.Text;
	using FoundationDB.Client;
	using Microsoft.Extensions.Time.Testing;

	/// <summary>Tests for the watch fault-injection facet (<see cref="FakeDbStore.Buggify"/>): the deterministic injection API that
	/// reproduces the two catalogued watch glitch classes a real cluster is allowed to produce (a spurious fire, and a missed fire on
	/// a net-reverted change) so a test can exercise consumer code against the weak watch contract.</summary>
	[TestFixture]
	[Category("FakeDb-Client")]
	public class FakeDbBuggifyFacts : FakeDbTest
	{

		[Test]
		public async Task Test_FireWatches_Injects_A_Spurious_Fire_And_The_Value_Did_Not_Change()
		{
			// a watch may fire even when the watched key did not change; a correct consumer re-reads after the fire and
			// observes no change. FireWatches injects exactly that permitted spurious fire.

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("v1")), this.Cancellation);

			FdbWatch watch;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				watch = tr.Watch(Key("k1"), this.Cancellation);
				await tr.CommitAsync();
			}
			Assert.That(watch.Task.IsCompleted, Is.False, "the freshly-armed watch must be pending: nothing has changed yet");

			// inject the spurious fire (the key is NOT modified)
			int fired = store.Buggify.FireWatches(Key("k1"));
			Assert.That(fired, Is.EqualTo(1), "the injection must land on the one armed watch");

			await watch.Task.WaitAsync(TimeSpan.FromSeconds(5), this.Cancellation);
			Assert.That(watch.Task.IsCompletedSuccessfully, Is.True, "the injected spurious fire must complete the watch");

			// the correct-consumer discipline: re-read after the fire and observe that nothing actually changed
			var current = await db.ReadAsync(tr => tr.GetAsync(Key("k1")), this.Cancellation);
			Assert.That(current, Is.EqualTo(Value("v1")), "the fire was spurious: a consumer that assumed 'fired implies changed' would be wrong");
		}

		[Test]
		public async Task Test_FireWatches_On_A_Key_With_No_Watch_Reports_Zero()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			int fired = store.Buggify.FireWatches(Key("never-watched"));
			Assert.That(fired, Is.Zero, "no watch is armed on the key: the injection lands on nothing");
		}

		[Test]
		public async Task Test_FireWatches_Fans_Out_To_Every_Watch_On_The_Same_Key()
		{
			// the FDBV-026 shape: one stale-armed sibling drags every co-registered watch on the key. FireWatches models
			// that per-key fan-out - all watches on the key fire together.

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			FdbWatch w1;
			using (var trA = db.BeginTransaction(this.Cancellation))
			{
				w1 = trA.Watch(Key("shared"), this.Cancellation);
				await trA.CommitAsync();
			}
			FdbWatch w2;
			using (var trB = db.BeginTransaction(this.Cancellation))
			{
				w2 = trB.Watch(Key("shared"), this.Cancellation);
				await trB.CommitAsync();
			}

			int fired = store.Buggify.FireWatches(Key("shared"));
			Assert.That(fired, Is.EqualTo(2), "both watches on the key fire together (the entanglement shape)");

			await Task.WhenAll(w1.Task, w2.Task).WaitAsync(TimeSpan.FromSeconds(5), this.Cancellation);
			Assert.That(w1.Task.IsCompletedSuccessfully && w2.Task.IsCompletedSuccessfully, Is.True);
		}

		[Test]
		public async Task Test_SuppressNextWatchCheck_Deferred_Check_Self_Heals_On_A_Later_Real_Change()
		{
			// the FDBV-027 mechanism: a skipped commit-time check leaves the watch registered with its original baseline.
			// The stack is level-triggered, so the miss self-heals: a later commit that still leaves the value differing
			// from the baseline fires the watch (late, which the contract permits).

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("a")), this.Cancellation);

			FdbWatch watch;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				watch = tr.Watch(Key("k1"), this.Cancellation);
				await tr.CommitAsync();
			}

			// arm the deferred check, then make a real change: the (skipped) check must NOT fire the watch
			store.Buggify.SuppressNextWatchCheck(Key("k1"));
			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("b")), this.Cancellation);
			Assert.That(watch.Task.IsCompleted, Is.False, "the deferred check skipped the fire for a→b");

			// a later commit still leaves the value (c) differing from the baseline (a): the missed fire self-heals
			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("c")), this.Cancellation);
			await watch.Task.WaitAsync(TimeSpan.FromSeconds(5), this.Cancellation);
			Assert.That(watch.Task.IsCompletedSuccessfully, Is.True, "a later real change self-heals the missed fire");
		}

		[Test]
		public async Task Test_SuppressNextWatchCheck_A_Net_Reverted_Change_Stays_Pending()
		{
			// the FDBV-027 false negative proper: with the change's check deferred, a value that is changed and then
			// changed back to the baseline never fires - exactly the reverted transient a real cluster is allowed to miss.

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("a")), this.Cancellation);

			FdbWatch watch;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				watch = tr.Watch(Key("k1"), this.Cancellation);
				await tr.CommitAsync();
			}

			store.Buggify.SuppressNextWatchCheck(Key("k1"));
			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("b")), this.Cancellation);      // suppressed: does not fire
			Assert.That(watch.Task.IsCompleted, Is.False, "the deferred check skipped the fire for a→b");

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("a")), this.Cancellation);      // net revert: value == baseline again
			Assert.That(watch.Task.IsCompleted, Is.False, "a net-reverted (ABA) change never fires: the value equals the baseline again");

			watch.Cancel();
		}

		[Test]
		public async Task Test_SuppressNextWatchCheck_Only_Skips_One_Check()
		{
			// suppression is one-shot: only the NEXT check is deferred. A watch armed clean after the suppression is spent
			// fires normally on the very first real change.

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("a")), this.Cancellation);

			FdbWatch watch;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				watch = tr.Watch(Key("k1"), this.Cancellation);
				await tr.CommitAsync();
			}

			store.Buggify.SuppressNextWatchCheck(Key("k1"));
			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("b")), this.Cancellation);      // consumes the one suppression
			Assert.That(watch.Task.IsCompleted, Is.False, "the single suppression skipped this check");

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("c")), this.Cancellation);      // no suppression left: fires
			await watch.Task.WaitAsync(TimeSpan.FromSeconds(5), this.Cancellation);
			Assert.That(watch.Task.IsCompletedSuccessfully, Is.True, "the suppression was one-shot; the next check fires normally");
		}

		[Test]
		public async Task Test_FireWatchesAfter_Fires_When_Virtual_Time_Advances_Past_The_Delay()
		{
			// the timed variant schedules on the store clock: deterministic under a fake clock. Nothing fires until the
			// test advances virtual time past the delay, then the watch fires.

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

			store.Buggify.FireWatchesAfter(Key("k1"), TimeSpan.FromSeconds(1));

			// before the delay elapses, the scheduled fire has not landed
			await AdvanceAndPump(fake, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(100));
			Assert.That(watch.Task.IsCompleted, Is.False, "the scheduled fire is gated on virtual time and has not reached its due time");

			// advancing past the delay lands the fire
			await AdvanceAndPump(fake, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(100));
			await watch.Task.WaitAsync(TimeSpan.FromSeconds(5), this.Cancellation);
			Assert.That(watch.Task.IsCompletedSuccessfully, Is.True, "crossing the delay on the virtual clock fires the watch");
		}

		// ---- Approach B: seeded chaos --------------------------------------------------------------------------------

		[Test]
		public async Task Test_Chaos_Deferred_Check_Reproduces_Missed_Fires_Deterministically()
		{
			// without buggify every ABA change fires its watch at the intermediate value
			Assert.That(await RunAbaSchedule(null), Is.EqualTo("FFFFF"), "clean: every real change fires its watch");

			// deferring EVERY check turns each net-reverted (ABA) change into a missed fire - a false negative the contract permits
			Assert.That(
				await RunAbaSchedule(new FakeDbStore.FakeDbBuggifyChaos(1) { SpuriousFireRate = 0.0, DeferredCheckRate = 1.0 }),
				Is.EqualTo("PPPPP"),
				"buggify defers every check, so every ABA change is missed (the FDBV-027 class)"
			);

			// a partial-rate chaos run is a pure function of (seed, schedule): it replays byte-for-byte
			var r1 = await RunAbaSchedule(new FakeDbStore.FakeDbBuggifyChaos(42) { SpuriousFireRate = 0.0, DeferredCheckRate = 0.5 });
			var r2 = await RunAbaSchedule(new FakeDbStore.FakeDbBuggifyChaos(42) { SpuriousFireRate = 0.0, DeferredCheckRate = 0.5 });
			Assert.That(r1, Is.EqualTo(r2), "same seed + same schedule => identical watch outcomes");
		}

		[Test]
		public async Task Test_Chaos_Spurious_Fire_Reproduces_Phantom_Fires_Deterministically()
		{
			// the watched keys never change, so without buggify nothing fires
			Assert.That(await RunSpuriousSchedule(null), Is.EqualTo("PPPPP"), "clean: no watched key changes, so nothing fires");

			// every commit injects a spurious fire on one armed key: some watches fire although their keys never changed
			var s1 = await RunSpuriousSchedule(new FakeDbStore.FakeDbBuggifyChaos(7) { SpuriousFireRate = 1.0, DeferredCheckRate = 0.0 });
			var s2 = await RunSpuriousSchedule(new FakeDbStore.FakeDbBuggifyChaos(7) { SpuriousFireRate = 1.0, DeferredCheckRate = 0.0 });
			Assert.That(s1, Is.EqualTo(s2), "same seed + same schedule => identical watch outcomes");
			Assert.That(s1, Does.Contain("F"), "spurious fires land on armed watches although their keys never changed (the FDBV-026 class)");
			Assert.That(s1, Does.Contain("P"), "fewer commits than watches: some watches stay pending");
		}

		/// <summary>Arms one watch per key, then plays an A→B→A (change then revert) per key. Clean: every watch fires at B ("FFFFF").
		/// Under a deferred-check chaos, a deferred B change becomes a net-reverted miss and the watch stays pending.</summary>
		private async Task<string> RunAbaSchedule(FakeDbStore.FakeDbBuggifyChaos? chaos)
		{
			var store = new FakeDbStore();
			if (chaos is not null) store.Buggify.Chaos = chaos;
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => { for (int i = 0; i < 5; i++) tr.Set(Key($"k{i}"), Value("v0")); }, this.Cancellation);

			var watches = new FdbWatch[5];
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				for (int i = 0; i < 5; i++) watches[i] = tr.Watch(Key($"k{i}"), this.Cancellation);
				await tr.CommitAsync();
			}

			for (int i = 0; i < 5; i++)
			{
				var key = Key($"k{i}");
				await db.WriteAsync(tr => tr.Set(key, Value("v1")), this.Cancellation);   // change...
				await db.WriteAsync(tr => tr.Set(key, Value("v0")), this.Cancellation);   // ...then revert (ABA)
			}

			return Outcome(watches);
		}

		/// <summary>Arms one watch per key, then commits only to UNWATCHED keys. Clean: nothing fires ("PPPPP"). Under a
		/// spurious-fire chaos, each commit fans out a phantom fire onto one armed key although the watched keys never changed.</summary>
		private async Task<string> RunSpuriousSchedule(FakeDbStore.FakeDbBuggifyChaos? chaos)
		{
			var store = new FakeDbStore();
			if (chaos is not null) store.Buggify.Chaos = chaos;
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => { for (int i = 0; i < 5; i++) tr.Set(Key($"w{i}"), Value("v0")); }, this.Cancellation);

			var watches = new FdbWatch[5];
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				for (int i = 0; i < 5; i++) watches[i] = tr.Watch(Key($"w{i}"), this.Cancellation);
				await tr.CommitAsync();
			}

			for (int i = 0; i < 3; i++)
			{
				var key = Key($"u{i}");
				await db.WriteAsync(tr => tr.Set(key, Value("x")), this.Cancellation);   // unwatched keys: no legitimate fire
			}

			return Outcome(watches);
		}

		private static string Outcome(FdbWatch[] watches)
		{
			var sb = new StringBuilder(watches.Length);
			foreach (var w in watches)
			{
				sb.Append(w.Task.IsCompleted ? 'F' : 'P');
				if (!w.Task.IsCompleted) w.Cancel();
			}
			return sb.ToString();
		}

	}

}
