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
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client.Tests
{
	using FoundationDB.FdbLite;
	using FoundationDB.Testing;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Time.Testing;

	/// <summary>The composition-time door: <c>services.AddFdbLite(...)</c> registers an <see cref="IFdbDatabaseProvider"/> over a file-backed or in-memory <see cref="FdbLiteStore"/>.</summary>
	[TestFixture]
	[Category("Fdb-Storage")]
	public class FdbLiteProviderFacts : FdbSimpleTest
	{

		private static string NewStorePath()
		{
			var dir = Path.Combine(Path.GetTempPath(), "fdblite-tests");
			Directory.CreateDirectory(dir);
			return Path.Combine(dir, $"provider-{Guid.NewGuid():N}.fdblite");
		}

		private static void DeleteQuietly(string path)
		{
			try { File.Delete(path); } catch { }
		}

		private static async Task<Slice> ReadKeyAsync(IFdbDatabase db, string key, CancellationToken ct)
		{
			return await db.ReadAsync(tr => tr.GetAsync(Slice.FromString(key)), ct);
		}

		[Test]
		public async Task Test_File_Store_Round_Trips_And_Survives_The_Container()
		{
			var path = NewStorePath();
			try
			{
				// first container: the provider creates the file and one transaction writes a key
				var services = new ServiceCollection();
				services.AddFdbLite(730, path);
				using (var sp = services.BuildServiceProvider())
				{
					var provider = sp.GetRequiredService<IFdbDatabaseProvider>();
					Assert.That(provider, Is.InstanceOf<FdbLiteProvider>());

					var db = await provider.GetDatabase(this.Cancellation);
					await db.WriteAsync(tr => tr.Set(Slice.FromString("hello"), Slice.FromString("world")), this.Cancellation);
					Assert.That(await ReadKeyAsync(db, "hello", this.Cancellation), Is.EqualTo(Slice.FromString("world")));
					Assert.That(provider.IsAvailable, Is.True, "a started provider with a database is available");

					// the package README sample: a directory created on first write, a tuple key, a text value
					await db.WriteAsync(async tr =>
					{
						var subspace = await db.Root["state"].ResolveOrCreate(tr);
						tr.Set(subspace.Key("hello"), FdbValue.ToTextUtf8("world"));
					}, this.Cancellation);
					var text = await db.ReadAsync(async tr =>
					{
						var subspace = await db.Root["state"].Resolve(tr);
						return (await tr.GetAsync(subspace.Key("hello"))).ToStringUtf8();
					}, this.Cancellation);
					Assert.That(text, Is.EqualTo("world"));
				}

				Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "the store file exists after the provider is disposed");

				// second container over the same path: the value is on disk
				services = new ServiceCollection();
				services.AddFdbLite(730, path);
				using (var sp = services.BuildServiceProvider())
				{
					var db = await sp.GetRequiredService<IFdbDatabaseProvider>().GetDatabase(this.Cancellation);
					Assert.That(await ReadKeyAsync(db, "hello", this.Cancellation), Is.EqualTo(Slice.FromString("world")));
				}
			}
			finally
			{
				DeleteQuietly(path);
			}
		}

		[Test]
		public async Task Test_In_Memory_Store_Round_Trips_And_Dies_With_The_Container()
		{
			var services = new ServiceCollection();
			services.AddFdbLite(730);
			using (var sp = services.BuildServiceProvider())
			{
				var db = await sp.GetRequiredService<IFdbDatabaseProvider>().GetDatabase(this.Cancellation);
				await db.WriteAsync(tr => tr.Set(Slice.FromString("hello"), Slice.FromString("world")), this.Cancellation);
				Assert.That(await ReadKeyAsync(db, "hello", this.Cancellation), Is.EqualTo(Slice.FromString("world")));
			}

			// a second in-memory registration is a fresh store
			services = new ServiceCollection();
			services.AddFdbLite(730);
			using (var sp = services.BuildServiceProvider())
			{
				var db = await sp.GetRequiredService<IFdbDatabaseProvider>().GetDatabase(this.Cancellation);
				Assert.That(await ReadKeyAsync(db, "hello", this.Cancellation), Is.EqualTo(Slice.Nil));
			}
		}

		[Test]
		public async Task Test_Shared_Store_Outlives_Its_Providers()
		{
			// two "processes" over one store: each has its own container, both see the same data, and disposing one leaves the store alive
			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Default, apiVersion: 730);

			var services1 = new ServiceCollection();
			services1.AddFdbLite(store);
			var services2 = new ServiceCollection();
			services2.AddFdbLite(store);

			using (var sp1 = services1.BuildServiceProvider())
			{
				var db1 = await sp1.GetRequiredService<IFdbDatabaseProvider>().GetDatabase(this.Cancellation);
				await db1.WriteAsync(tr => tr.Set(Slice.FromString("hello"), Slice.FromString("world")), this.Cancellation);
			}

			using (var sp2 = services2.BuildServiceProvider())
			{
				var db2 = await sp2.GetRequiredService<IFdbDatabaseProvider>().GetDatabase(this.Cancellation);
				Assert.That(await ReadKeyAsync(db2, "hello", this.Cancellation), Is.EqualTo(Slice.FromString("world")));
			}

			Assert.That(store.IsClosed, Is.False, "the shared store belongs to its creator, not to the providers");
		}

		[Test]
		public async Task Test_Provider_Resolves_The_DI_TimeProvider()
		{
			var fake = new FakeTimeProvider();
			var services = new ServiceCollection();
			services.AddSingleton<TimeProvider>(fake);
			services.AddFdbLite(730);
			using var sp = services.BuildServiceProvider();

			var db = await sp.GetRequiredService<IFdbDatabaseProvider>().GetDatabase(this.Cancellation);
			Assert.That(db.Time, Is.SameAs(fake), "the provider must resolve the DI-registered TimeProvider onto the database");
		}

	}

}
