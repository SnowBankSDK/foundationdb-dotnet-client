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
	using FoundationDB.Storage.FdbLite;
	using FoundationDB.Testing;

	/// <summary>The read-version retention window: which past versions a store still serves, and what a read past the window reports.</summary>
	/// <remarks>The window is the backend's call - a store that reclaims its storage can only serve the versions whose pages still exist, while one that never reclaims serves every version it ever published. Both are exercised here, because the shared read path resolves a requested version the same way for either and only the retention count differs.</remarks>
	[TestFixture]
	[Category("Fdb-Storage")]
	public class FdbLiteRetentionFacts : FdbSimpleTest
	{

		/// <summary>Commits three times and returns the versions, newest last.</summary>
		private async Task<List<long>> CommitThreeAsync(IFdbDatabase db)
		{
			var versions = new List<long>();
			for (int i = 0; i < 3; i++)
			{
				using var tr = db.BeginTransaction(this.Cancellation);
				tr.Set(Slice.FromString($"k{i}"), Slice.FromString($"v{i}"));
				await tr.CommitAsync();
				versions.Add(tr.GetCommittedVersion());
			}
			return versions;
		}

		/// <summary>Reads at <paramref name="version"/> and returns the error code, or <c>null</c> when the read succeeds.</summary>
		private async Task<FdbError?> TryReadAtVersionAsync(IFdbDatabase db, long version)
		{
			try
			{
				using var tr = db.BeginTransaction(this.Cancellation);
				tr.SetReadVersion(version);
				await tr.GetAsync(Slice.FromString("k0"));
				return null;
			}
			catch (FdbException e)
			{
				return e.Code;
			}
		}

		[Test]
		public async Task Test_Persistent_Store_Serves_The_Previous_Version_But_Not_Older()
		{
			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			var versions = await CommitThreeAsync(db);

			// the current version and the one behind it are still served: that is the window a real cluster keeps
			Assert.That(await TryReadAtVersionAsync(db, versions[2]), Is.Null, "the current version must be readable");
			Assert.That(await TryReadAtVersionAsync(db, versions[1]), Is.Null, "the immediately-previous version must be readable");

			// anything older has had its pages reclaimed, and must say so the way the cluster does
			Assert.That(await TryReadAtVersionAsync(db, versions[0]), Is.EqualTo(FdbError.TransactionTooOld), "a version past the window must fail as too old");

			// the store must also stop RETAINING what it can no longer serve: the engine refuses the read pin either
			// way, so a store that kept every snapshot would still report "too old" while leaking one snapshot per
			// commit forever. The retained set is the assertion that separates the two.
			Assert.That(store.Snapshots.Count, Is.EqualTo(2), "a reclaiming store must retain only the versions it can still serve");
		}

		[Test]
		public async Task Test_In_Memory_Store_Serves_Every_Version_It_Published()
		{
			using var store = new FakeDbStore();
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			var versions = await CommitThreeAsync(db);

			// nothing is ever reclaimed here, so the whole history stays addressable
			foreach (var version in versions)
			{
				Assert.That(await TryReadAtVersionAsync(db, version), Is.Null, $"version {version} must still be readable");
			}

			// the initial snapshot plus one per commit: the movie is kept in full
			Assert.That(store.Snapshots.Count, Is.EqualTo(versions.Count + 1), "a store that never reclaims must retain every version it published");
		}

		[Test]
		public async Task Test_Retaining_Store_Over_The_Engine_Serves_Every_Version_It_Published()
		{
			// same body as the in-memory store above, over the paged engine instead: retaining every version is a
			// CONFIGURATION of the engine (reclamation floor dropped), not a property of a different storage
			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis, retainEveryVersion: true);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			var versions = await CommitThreeAsync(db);

			foreach (var version in versions)
			{
				Assert.That(await TryReadAtVersionAsync(db, version), Is.Null, $"version {version} must still be readable");
			}

			Assert.That(store.Snapshots.Count, Is.EqualTo(versions.Count + 1), "a store that never reclaims must retain every version it published");

			// the same store still reports a version it never published as too old, so retaining everything must not
			// have turned the retention check into "always yes"
			Assert.That(await TryReadAtVersionAsync(db, versions[^1] + 1_000_000), Is.EqualTo(FdbError.TransactionTooOld), "a version this store never published must still fail as too old");
		}

		[Test]
		public async Task Test_Reading_At_A_Version_That_Was_Never_Published_Fails_As_Too_Old()
		{
			using var store = new FakeDbStore();
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			var versions = await CommitThreeAsync(db);

			Assert.That(await TryReadAtVersionAsync(db, versions[^1] + 1_000_000), Is.EqualTo(FdbError.TransactionTooOld), "a version this store never published must fail as too old, not throw a lookup error");
		}

	}

}
