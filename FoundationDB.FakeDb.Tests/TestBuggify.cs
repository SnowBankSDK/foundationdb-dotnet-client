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
	using FoundationDB.Testing;

	/// <summary>Repo test-harness policy for FakeDb watch buggify: the repo's own harness backends are watch-realistic BY DEFAULT.</summary>
	/// <remarks>
	/// <para>The library default is buggify-off (that decision belongs to each downstream consumer, since the emulator is public), but
	/// one level up - where we own the tests - the harness backends enable seeded chaos by default so every watch-arming suite exercises
	/// the weak watch contract (a watch may fire spuriously, and a net-reverted change may never fire). Each test gets a stable,
	/// per-test-name seed, so a run is reproducible and a failure replays.</para>
	/// <para>A test that asserts exact, clean watch semantics disables it with one line: <c>store.Buggify.Disable()</c> (or, on a
	/// conformance head, the fixture's <c>RequireCleanWatches()</c> hook), which documents "this test needs clean watches" at the site.</para>
	/// <para>Chaos is a no-op for a suite that arms no watches (nothing to fire, no checks to defer), so enabling it on a non-watch
	/// conformance head is harmless.</para>
	/// </remarks>
	internal static class TestBuggify
	{

		/// <summary>Creates a fresh FakeDb store with watch chaos enabled, seeded from the currently-running test's name.</summary>
		public static FakeDbStore ChaosStore()
		{
			var store = new FakeDbStore();
			store.Buggify.EnableChaos(NUnit.Framework.TestContext.CurrentContext.Test.FullName);
			return store;
		}

	}

}
