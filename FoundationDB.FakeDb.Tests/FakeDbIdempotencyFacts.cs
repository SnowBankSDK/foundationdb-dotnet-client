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

	/// <summary>Tests for the native-idempotency emulation (<see cref="FdbTransactionOption.AutomaticIdempotency"/> / <see cref="FdbTransactionOption.IdempotencyId"/>)
	/// and its fault-injection driver <see cref="FakeDbStore.FakeDbBuggify.LoseNextCommitAck"/>.</summary>
	/// <remarks>
	/// <para>The emulator reproduces the maybe-committed hazard deterministically: <c>LoseNextCommitAck</c> makes the next commit apply its
	/// writes but report an ambiguous outcome. Without an idempotency id the retry loop re-runs the handler on top of its own landed writes
	/// (the probe-then-throw bug); with one the client resolves the outcome to success and the handler runs exactly once. The observable that
	/// underpins this - a committed id landing in the idmp store - is cross-checked against a real 7.4 cluster in
	/// <c>IdempotencyExperimentFacts</c> (the emulation is validated against the cluster, never trusted as its own oracle).</para>
	/// </remarks>
	[TestFixture]
	[Category("FakeDb-Client")]
	public class FakeDbIdempotencyFacts : FakeDbTest
	{

		/// <summary>A create-if-not-exists operation: the probe-then-throw shape that misbehaves on a maybe-committed retry (the CreateTenantAsync case).</summary>
		private async Task<Slice> CreateIfAbsent(IFdbDatabase db, Slice key, Slice value, bool idempotent, Action onAttempt)
		{
			return await db.ReadWriteAsync(async tr =>
			{
				onAttempt();
				if (idempotent) tr.Options.WithAutomaticIdempotency();
				var existing = await tr.GetAsync(key);
				if (!existing.IsNull) throw new InvalidOperationException("The specified key already exists.");
				tr.Set(key, value);
				return value;
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_MaybeCommitted_Retry_Without_Idempotency_Reproduces_The_Bug()
		{
			var store = new FakeDbStore(FdbIdempotencyExtensions.MinimumApiVersion);
			using var db = store.OpenDatabase(null, readOnly: false);

			var key = Key("thing");
			int attempts = 0;

			// arm the maybe-committed injection: the first commit applies its writes but loses its acknowledgement
			store.Buggify.LoseNextCommitAck();

			// without idempotency, the loop re-runs the handler on top of its own landed write, so the probe throws a false "already exists"
			Assert.That(
				async () => await CreateIfAbsent(db, key, Slice.FromStringUtf8("v"), idempotent: false, () => attempts++),
				Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("already exists"),
				"the maybe-committed re-run must hit the probe-then-throw bug"
			);
			Assert.That(attempts, Is.EqualTo(2), "the handler must have re-run after the lost ack");
		}

		[Test]
		public async Task Test_MaybeCommitted_Retry_With_Idempotency_Short_Circuits()
		{
			var store = new FakeDbStore(FdbIdempotencyExtensions.MinimumApiVersion);
			using var db = store.OpenDatabase(null, readOnly: false);

			var key = Key("thing");
			int attempts = 0;

			store.Buggify.LoseNextCommitAck();

			// with automatic idempotency the client resolves the maybe-committed outcome to success: the handler runs exactly once
			var result = await CreateIfAbsent(db, key, Slice.FromStringUtf8("v"), idempotent: true, () => attempts++);

			Assert.That(store.MaybeCommittedResolutions, Is.EqualTo(1), "the maybe-committed resolution path must have fired (proves the injection was not silently skipped)");
			Assert.That(attempts, Is.EqualTo(1), "the handler must NOT be re-run: the ambiguous commit resolved to a definitive success");
			Assert.That(result, Is.EqualTo(Slice.FromStringUtf8("v")), "must return the first run's result");

			// the write landed exactly once and is readable
			var stored = await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation);
			Assert.That(stored, Is.EqualTo(Slice.FromStringUtf8("v")), "the single landed write must be present");
		}

		[Test]
		public async Task Test_Genuine_Collision_Still_Throws()
		{
			var store = new FakeDbStore(FdbIdempotencyExtensions.MinimumApiVersion);
			using var db = store.OpenDatabase(null, readOnly: false);

			var key = Key("thing");

			// first creator commits cleanly (with its own idempotency id)
			await CreateIfAbsent(db, key, Slice.FromStringUtf8("v1"), idempotent: true, () => { });

			// a genuine second creator (a DIFFERENT operation, hence a different idempotency id) must still fail:
			// its id is absent from the idmp store, so nothing masks the real collision
			Assert.That(
				async () => await CreateIfAbsent(db, key, Slice.FromStringUtf8("v2"), idempotent: true, () => { }),
				Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("already exists"),
				"idempotency must not mask a genuine third-party collision"
			);
		}

		[Test]
		public async Task Test_IsSupported_Tracks_Api_Version()
		{
			// below api 720 the cluster has no native idempotency: the check reports false
			using (var db = new FakeDbStore(710).OpenDatabase(null, readOnly: false))
			{
				var supported = await db.ReadAsync(tr => Task.FromResult(tr.Options.IsAutomaticIdempotencySupported), this.Cancellation);
				Assert.That(supported, Is.False, "api 710 (< 720) does not support native idempotency");
			}

			// at api 720 or greater the check reports true
			using (var db = new FakeDbStore(FdbIdempotencyExtensions.MinimumApiVersion).OpenDatabase(null, readOnly: false))
			{
				var supported = await db.ReadAsync(tr => Task.FromResult(tr.Options.IsAutomaticIdempotencySupported), this.Cancellation);
				Assert.That(supported, Is.True, "api 720+ supports native idempotency");
			}
		}

	}

}
