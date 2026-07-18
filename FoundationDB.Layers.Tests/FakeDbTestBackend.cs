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

	/// <summary>Reusable FakeDb backend for a dual-backend layer fixture. A concrete <c>Xxx</c><c>FakeDbFacts</c> subclass holds
	/// one of these, resets it in <c>[TearDown]</c>, and delegates its <see cref="FdbTest.OpenTestDatabaseAsync"/> /
	/// <see cref="FdbTest.OpenTestPartitionAsync"/> overrides to it, so a layer suite written on <see cref="FdbTest"/> runs
	/// against the in-memory FakeDb emulator (no Docker, no native client) for fast iteration.</summary>
	internal sealed class FakeDbTestBackend
	{

		/// <summary>Store shared by all databases opened during a single test, reset between tests.</summary>
		private FakeDbStore? Store { get; set; }

		/// <summary>Discards the store so the next test starts from an empty emulator (call from <c>[TearDown]</c>).</summary>
		public void Reset() => this.Store = null;

		/// <summary>Opens a database rooted at <paramref name="path"/> from the current (lazily created) store.</summary>
		public Task<IFdbDatabase> OpenAsync(FdbPath path, bool readOnly = false)
		{
			var db = (this.Store ??= new FakeDbStore()).OpenDatabase(path, readOnly);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

	}

}
