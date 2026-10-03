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
	using FoundationDB.Testing;

	/// <summary>Minimal conformance fixture used to validate the dual-backend plumbing itself.</summary>
	public abstract class SmokeConformanceFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Set_And_Get_Value()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("smoke", "hello"), Text("world"));
			}, this.Cancellation);

			var actual = await db.ReadAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				return await tr.GetAsync(subspace.Key("smoke", "hello"));
			}, this.Cancellation);
			Assert.That(actual, Is.EqualTo(Text("world")));
		}

		[Test]
		public async Task Test_Can_Use_Root_Database()
		{
			// this is the only smoke test that exercises OpenTestDatabaseAsync (root database, no partition)
			// note: we cannot call CleanLocation(db) here, because the root of the database can never be cleaned!
			using var db = await OpenTestDatabaseAsync();

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("smoke", "root"), Text("world"));
			}, this.Cancellation);

			var actual = await db.ReadAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				return await tr.GetAsync(subspace.Key("smoke", "root"));
			}, this.Cancellation);
			Assert.That(actual, Is.EqualTo(Text("world")));
		}

		[Test]
		public async Task Test_Can_Clear_Value()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("smoke", "gone"), Text("soon"));
			}, this.Cancellation);
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Clear(subspace.Key("smoke", "gone"));
			}, this.Cancellation);

			var actual = await db.ReadAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				return await tr.GetAsync(subspace.Key("smoke", "gone"));
			}, this.Cancellation);
			Assert.That(actual, Is.EqualTo(Slice.Nil));
		}

	}

	/// <summary>Runs the smoke conformance tests against the FakeDb emulator (no Docker, no native client).</summary>
	[TestFixture]
	public class SmokeFakeDbFacts : SmokeConformanceFacts
	{

		/// <summary>Store shared by all databases opened during a single test, reset between tests.</summary>
		private FakeDbStore? Store { get; set; }

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetFakeDbStore() => this.Store = null;

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false)
		{
			var db = (this.Store ??= TestBuggify.ChaosStore()).OpenDatabase(FdbPath.Root, readOnly);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null)
		{
			var db = (this.Store ??= TestBuggify.ChaosStore()).OpenDatabase(GetTestPartitionPath(testMethod), readOnly: false);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

	}

	/// <summary>Runs the smoke conformance tests against a real FoundationDB cluster (Testcontainers). Run explicitly from the Unit Test Sessions UI; requires a local Docker daemon.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class SmokeRealClusterFacts : SmokeConformanceFacts
	{
		// inherits the full FdbTest behavior: container startup, native client probing, real connection
	}

}
