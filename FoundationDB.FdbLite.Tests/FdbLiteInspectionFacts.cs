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
	using FoundationDB.FakeDb;
	using FoundationDB.Storage;
	using FoundationDB.FdbLite;
	using FoundationDB.Testing;
	using FoundationDB.Testing.Tests;

	/// <summary>Snapshot capture, enumeration, dump and diff, over each storage backend.</summary>
	/// <remarks>These are the inspection facilities a test uses to see what a store actually holds. They read a <see cref="Snapshot"/>, which is storage-agnostic by contract, so the same assertions must hold whichever backend produced it - a helper that only works over one storage is a hole in that contract.</remarks>
	[TestFixture]
	[Category("Fdb-Storage")]
	public class FdbLiteInspectionFacts : FakeDbTest
	{

		private static Slice K(string literal) => Slice.FromStringAscii(literal);

		private static Slice V(string literal) => Slice.FromStringUtf8(literal);

		/// <summary>Runs the whole inspection surface over a store, whatever its storage.</summary>
		/// <remarks>The database is opened here and stays open for the whole verification: a persistent store's snapshots read through its pager, which the database owns.</remarks>
		private async Task VerifyInspectionSurfaceAsync(FdbEmulatedDatabase store)
		{
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			// seed three keys, then add / change / remove one each
			await db.WriteAsync(tr =>
			{
				tr.Set(K("k1"), V("v1"));
				tr.Set(K("k2"), V("v2"));
				tr.Set(K("k3"), V("v3"));
			}, this.Cancellation);

			var before = store.CurrentSnapshotUnsafe;

			await db.WriteAsync(tr =>
			{
				tr.Set(K("k2"), V("v2-changed"));
				tr.Clear(K("k3"));
				tr.Set(K("k4"), V("v4"));
			}, this.Cancellation);

			var after = store.CurrentSnapshotUnsafe;

			// capture: two distinct generations, the later one at the higher version
			Assert.That(after.Version, Is.GreaterThan(before.Version), "each commit must publish its own snapshot version");

			// enumeration: the user keyspace of the later generation, in key order
			var keys = after
				.ReadData()
				.Where(kv => !kv.Key.IsSystemKey())
				.Select(kv => kv.Key.Slice)
				.ToList();
			Assert.That(keys, Is.EqualTo(new[] { K("k1"), K("k2"), K("k4") }), "enumeration must yield the committed user keys in order");
			Assert.That(after.Count, Is.EqualTo(after.ReadData().Count()), "the reported key count must match what enumeration yields");

			// diff: exactly one added, one changed, one removed
			var diff = after.Diff(before).Where(x => !x.Key.IsSystemKey()).ToList();
			Assert.That(diff.Where(x => x.Before.IsNull).Select(x => x.Key.Slice), Is.EqualTo(new[] { K("k4") }), "added keys");
			Assert.That(diff.Where(x => x.After.IsNull).Select(x => x.Key.Slice), Is.EqualTo(new[] { K("k3") }), "removed keys");
			Assert.That(diff.Where(x => !x.Before.IsNull && !x.After.IsNull).Select(x => x.Key.Slice), Is.EqualTo(new[] { K("k2") }), "changed keys");

			// dump: the rendering helper the suites use to show a store's contents
			DumpStore(after, "after");
			DumpStore(store, "current");
		}

		[Test]
		public async Task Test_Can_Inspect_An_In_Memory_Store()
		{
			using var store = new FakeDbStore();
			await VerifyInspectionSurfaceAsync(store);
		}

		[Test]
		public async Task Test_Can_Inspect_A_Persistent_Store()
		{
			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis);
			await VerifyInspectionSurfaceAsync(store);
		}

	}

}
