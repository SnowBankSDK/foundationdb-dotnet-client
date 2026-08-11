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

	/// <summary>Characterization experiment for native FoundationDB idempotency (the transaction options
	/// <see cref="FdbTransactionOption.IdempotencyId"/> = 504 and <see cref="FdbTransactionOption.AutomaticIdempotency"/> = 505).</summary>
	/// <remarks>
	/// <para>These are not regression tests; they establish, against a real 7.4 cluster, the two facts the idempotency
	/// toolkit is built on:</para>
	/// <para>1. A committed transaction that carries an idempotency id persists that id under the <c>\xff\x02/idmp/</c>
	/// system keyspace (so a later attempt could look it up).</para>
	/// <para>2. An id set INSIDE the handler is honored on the committing attempt even after a forced retry, because the
	/// option does not survive the <c>on_error</c> reset and the handler re-applies it each attempt. This is the
	/// observation that decides whether the retry loop needs to re-apply the option itself.</para>
	/// </remarks>
	[TestFixture]
	public class IdempotencyExperimentFacts : FdbTest
	{

		public IdempotencyExperimentFacts()
		{
			// idempotency options require api level 720 or greater; pin it so the experiment runs at a supported level.
			this.OverrideApiVersion = 740;
		}

		// the idmp system keyspace: \xff\x02/idmp/<commit_version_big_endian><...> -> { protocol, timestamp, (len,id,batch_lo)* }
		private static readonly Slice IdmpBegin = Slice.FromByteString("\xff\x02/idmp/");
		private static readonly Slice IdmpEnd = Slice.FromByteString("\xff\x02/idmp0");

		[Test]
		public async Task Test_Native_IdempotencyId_Is_Stored_On_Commit()
		{
			Assume.That(Fdb.ApiVersion, Is.GreaterThanOrEqualTo(720), "idempotency options require api level 720+");

			using var db = await OpenTestDatabaseAsync();

			const string opId = "idem-exp-baseline-0123456789"; // 28 bytes, >= 16
			var idBytes = Slice.FromStringUtf8(opId);

			await db.WriteAsync(async tr =>
			{
				tr.Options.WithIdempotencyId(opId);
				tr.Set(Key("Tests", "Idempotency", "baseline"), Slice.FromStringUtf8("v"));
				await Task.CompletedTask;
			}, this.Cancellation);

			bool found = await IdmpContainsAsync(db, idBytes);
			Log($"# idmp contains baseline id = {found}");
			Assert.That(found, Is.True, "a committed transaction carrying a manual idempotency id must persist it under \\xff\\x02/idmp/");
		}

		[Test]
		public async Task Test_Native_IdempotencyId_Set_In_Handler_Survives_Forced_Retry()
		{
			Assume.That(Fdb.ApiVersion, Is.GreaterThanOrEqualTo(720), "idempotency options require api level 720+");

			using var db = await OpenTestDatabaseAsync();

			const string opId = "idem-exp-retry-0123456789abcd"; // >= 16 bytes
			var idBytes = Slice.FromStringUtf8(opId);

			int attempts = 0;
			await db.WriteAsync(async tr =>
			{
				int n = ++attempts;
				// set INSIDE the handler, so it is re-applied on every attempt (non-persistent options are dropped by the on_error reset)
				tr.Options.WithIdempotencyId(opId);
				tr.Set(Key("Tests", "Idempotency", "retry"), Slice.FromStringUtf8("v"));
				if (n == 1) throw new FdbException(FdbError.NotCommitted); // force exactly one reset+retry
				await Task.CompletedTask;
			}, this.Cancellation);

			Log($"# handler attempts = {attempts}");
			Assert.That(attempts, Is.EqualTo(2), "the first attempt was forced to retry, so the handler must run twice");

			bool found = await IdmpContainsAsync(db, idBytes);
			Log($"# idmp contains retry id = {found}");
			Assert.That(found, Is.True, "the id set inside the handler must be honored on the committing (2nd) attempt: proof the per-attempt set survives the reset");
		}

		private async Task<bool> IdmpContainsAsync(IFdbDatabase db, Slice idBytes)
		{
			return await db.ReadAsync(async tr =>
			{
				tr.Options.WithReadAccessToSystemKeys();
				var kvs = await tr.GetRange(IdmpBegin, IdmpEnd).ToListAsync();
				Log($"# idmp range entries = {kvs.Count}");
				foreach (var kv in kvs)
				{
					if (Contains(kv.Value.Span, idBytes.Span)) return true;
				}
				return false;
			}, this.Cancellation);
		}

		private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
		{
			if (needle.Length == 0 || needle.Length > haystack.Length) return false;
			for (int i = 0; i + needle.Length <= haystack.Length; i++)
			{
				if (haystack.Slice(i, needle.Length).SequenceEqual(needle)) return true;
			}
			return false;
		}

	}

}
