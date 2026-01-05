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

	[TestFixture]
	public class TenantFacts : FdbTest
	{
		//note: this test only works if the cluster supports Tenants ("tenant_mode" cannot be disabled)
		//      and will create several tenants, that all are prefixed with ("Tests", ...)
		//      PLEASE TRY NOT TO RUN THIS ON A PRODUCTION SERVER ^^;

		#region Setup...

		private IFdbDatabase? m_db;

		protected IFdbDatabase Db => m_db ?? throw new InvalidOperationException();

		protected override async Task OnBeforeAllTests()
		{
			m_db = await OpenTestDatabaseAsync();

			var mode = await m_db.ReadAsync(tr => Fdb.Tenants.GetTenantMode(tr), this.Cancellation);
			Log("Tenant Mode: " + mode);
			Assume.That(mode, Is.Not.EqualTo(FdbTenantMode.Disabled), "This test requires that the cluster is configure with 'tenant_mode' = optional !");
		}

		protected override Task OnAfterAllTests()
		{
			m_db?.Dispose();
			m_db = null;
			return Task.CompletedTask;
		}

		#endregion

		[Test, Order(1)]
		public void Test_Tenant_Name()
		{
			{ // Empty
				var name = FdbTenantName.None;
				Log($"> {name} ({name.Value:X})");
				Assert.That(name.IsValid, Is.False, "Empty tenant name is invalid");
				Assert.That(name.ToSlice(), Is.EqualTo(Slice.Nil), "Empty tenant name has no value");
				Assert.That(name.ToString(), Is.EqualTo("global"), "Empty tenant name has no string representation");
				Assert.That(name, Is.EqualTo(default(FdbTenantName)), "Empty tenant name is equal to itself");

				Assert.That(name.TryGetTuple(out _), Is.False, "Empty tenant name is not a valid tuple!");
			}
			{ // Create
				var name = FdbTenantName.Create(Literal("Hello, there!"));
				Log($"> {name} ({name.Value:X})");
				Assert.That(name.IsValid, Is.True, $"{name}.IsValid");
				Assert.That(name.ToSlice(), Is.EqualTo(Literal("Hello, there!")), $"{name}.ToSlice()");
				Assert.That(name.ToString(), Is.EqualTo("'Hello, there!'"), $"{name}.ToString()");

				Assert.That(name.TryGetTuple(out _), Is.False, "Name is not a valid tuple!");

				Assert.That(() => FdbTenantName.Create(Slice.Nil), Throws.InstanceOf<ArgumentException>(), "Cannot use Slice.Nil");
				Assert.That(() => FdbTenantName.Create(Slice.Empty), Throws.InstanceOf<ArgumentException>(), "Cannot use Slice.Empty");
				Assert.That(() => FdbTenantName.Create(Slice.FromByte(255)), Throws.InstanceOf<ArgumentException>(), "Cannot start with \xFF");
				Assert.That(() => FdbTenantName.Create(Slice.FromByteString("\xFFHello")), Throws.InstanceOf<ArgumentException>(), "Cannot start with \xFF");
			}
			{ // From Parts
				var name = FdbTenantName.FromParts("Hello", "There");
				Log($"> {name} ({name.Value:X})");
				Assert.That(name.IsValid, Is.True, $"{name}.IsValid");
				Assert.That(name.ToSlice(), Is.EqualTo(TuPack.EncodeKey("Hello", "There")), $"{name}.ToSlice()");
				Assert.That(name.ToString(), Is.EqualTo("(\"Hello\", \"There\")"), $"{name}.ToString()");
				Assert.That(name.TryGetTuple(out var tuple), Is.True, "Name is a valid tuple!");
				Assert.That(tuple, Is.Not.Null.And.EqualTo(("Hello", "There")));
			}
			{ // From Tuple
				var name = FdbTenantName.FromTuple(("Hello", "There"));
				Log($"> {name} ({name.Value:X})");
				Assert.That(name.IsValid, Is.True, $"{name}.IsValid");
				Assert.That(name.ToSlice(), Is.EqualTo(TuPack.EncodeKey("Hello", "There")), $"{name}.ToSlice()");
				Assert.That(name.ToString(), Is.EqualTo("(\"Hello\", \"There\")"), $"{name}.ToString()");
				Assert.That(name.TryGetTuple(out var tuple), Is.True, "Name is a valid tuple!");
				Assert.That(tuple, Is.Not.Null.And.EqualTo(("Hello", "There")));

				Assert.That(() => FdbTenantName.FromTuple(STuple.Empty), Throws.InstanceOf<ArgumentException>(), "Cannot use empty tuple");
			}
			{ // Equals
				Assert.That(FdbTenantName.FromParts("Hello"), Is.EqualTo(FdbTenantName.FromParts("Hello")));
				Assert.That(FdbTenantName.FromParts("Hello"), Is.EqualTo(FdbTenantName.FromTuple(STuple.Create("Hello"))));
				Assert.That(FdbTenantName.FromParts("Hello"), Is.EqualTo(FdbTenantName.Create(Slice.FromByteString("\x02Hello\x00"))));
				Assert.That(FdbTenantName.FromParts("Hello"), Is.Not.EqualTo(FdbTenantName.Create(Slice.FromByteString("\x02Hello"))));
				Assert.That(FdbTenantName.FromParts("Hello"), Is.Not.EqualTo(FdbTenantName.FromParts("World")));
				Assert.That(FdbTenantName.FromParts("Hello"), Is.Not.EqualTo(FdbTenantName.None));
			}
			{ // GetHashCode
				Assert.That(FdbTenantName.FromParts("Hello").GetHashCode(), Is.EqualTo(FdbTenantName.FromParts("Hello").GetHashCode()));
				Assert.That(FdbTenantName.FromParts("Hello", "There").GetHashCode(), Is.EqualTo(FdbTenantName.FromTuple(("Hello", "There")).GetHashCode()));
				Assert.That(FdbTenantName.FromParts("Hello").GetHashCode(), Is.EqualTo(FdbTenantName.Create(Slice.FromByteString("\x02Hello\x00")).GetHashCode()));
				Assert.That(FdbTenantName.FromParts("Hello").GetHashCode(), Is.Not.EqualTo(FdbTenantName.FromParts("World").GetHashCode()));
				Assert.That(FdbTenantName.None.GetHashCode(), Is.EqualTo(default(FdbTenantName).GetHashCode()));
			}
		}

		[Test, Order(2)]
		public void Test_Can_Get_Tenant_Instance()
		{

			Log("Opening tenant Tests/acme ...");
			var tenantAcme = this.Db.GetTenant(("Tests", "acme"));
			Assert.That(tenantAcme, Is.Not.Null);
			Assert.That(tenantAcme.Name.ToSlice(), Is.EqualTo(TuPack.EncodeKey("Tests", "acme")));
			Assert.That(tenantAcme.Name.ToString(), Is.EqualTo("(\"Tests\", \"acme\")"));

			// getting the same tenant should return the same cached instance
			Log("Checking that the tenant instance is cached...");
			var tenantAcme2 = this.Db.GetTenant(FdbTenantName.FromParts("Tests", "acme"));
			Assert.That(tenantAcme2, Is.SameAs(tenantAcme));
			Assert.That(tenantAcme2.Name.ToString(), Is.EqualTo("(\"Tests\", \"acme\")"));

			Log("Opening tenant Tests/contoso ...");
			var tenantContoso = this.Db.GetTenant(("Tests", "contoso", 123));
			Assert.That(tenantContoso, Is.Not.Null.And.Not.SameAs(tenantAcme));
			Assert.That(tenantContoso.Name.ToSlice(), Is.EqualTo(TuPack.EncodeKey("Tests", "contoso", 123)));
			Assert.That(tenantContoso.Name.ToString(), Is.EqualTo("(\"Tests\", \"contoso\", 123)"));
			
		}

		[Test]
		public async Task Test_List_Tenants()
		{
			// ensure that tenants exists
			Log("Initialize tenants");
			var acme = await PrepareTestTenant(FdbTenantName.FromParts("Tests", "acme"));
			var contoso = await PrepareTestTenant(FdbTenantName.FromParts("Tests", "contoso", 123));

			// read existing tenants
			Log("Listing existing 'Tests' tenants...");
			var tenants = await this.Db.ReadAsync(tr => Fdb.Tenants.QueryTenants(tr, TuPack.EncodeKey("Tests")).ToDictionaryAsync(k => k.Name), this.Cancellation);
			foreach (var (name, tenant) in tenants)
			{
				Log($"- `{name}`: Id={tenant.Id}, Prefix={tenant.Prefix:V} ({tenant.Prefix:X})");
			}

			// it is possible that there are other tenants from other tests, we will ensure we can find both in the list

			Assert.That(tenants, Does.ContainKey(acme.Name), $"Should have found tenant {acme.Name} in the list");
			Assert.That(tenants, Does.ContainKey(contoso.Name), $"Should have found tenant {contoso.Name} in the list");

			Assert.That(tenants[acme.Name], Is.EqualTo(acme));
			Assert.That(tenants[contoso.Name], Is.EqualTo(contoso));
		}

		[Test]
		public async Task Test_Tenant_Keyspace_Isolation()
		{
			// ensure that tenants exists
			Log("Initialize tenants");
			var acme = await PrepareTestTenant(FdbTenantName.FromParts("Tests", "acme"));
			var contoso = await PrepareTestTenant(FdbTenantName.FromParts("Tests", "contoso", 123));

			// prepare a key in the global keyspace (that should not be visible by tenants)
			await this.Db.WriteAsync(tr => tr.Set(Pack(("tests", "hello")), Text("global")), this.Cancellation);

			Log("Start a transaction with tenant Tests/acme");
			var random = await this.Db.GetTenant(acme.Name).ReadWriteAsync(async tr =>
			{
				Assert.That(tr, Is.Not.Null);
				Assert.That(tr.Tenant, Is.Not.Null, "tr.Tenant should not be null");
				Assert.That(tr.Tenant!.Name, Is.EqualTo(acme.Name), "tr.Tenant.Name should be valid");
				Assert.That(tr.Database, Is.Not.Null.And.SameAs(this.Db), "tr.Database should be the same db objet that was used to create the tenant");
				Assert.That(tr.Context.Mode.HasFlag(FdbTransactionMode.UseTenant), Is.True, $"tr.Mode flag UseTenant should be set, but was {tr.Context.Mode}");

				Log("Read a key from this transaction...");
				var value = await tr.GetAsync(Pack(("tests", "hello")));
				Log($"> {value:V}");

				tr.Set(Pack(("tests", "hello")), Text("inside tenant acme"));

				var token = Slice.FromGuid(Guid.NewGuid());

				Log($"Write a random token '{token}' to tenant {tr.Tenant?.Name}");
				tr.Set(Pack(("tests", "random")), token);

				return token;
			}, this.Cancellation);

			Log("Dump content of tenant range...");
			var res = await GlobalQuery(tr => tr.GetRange(acme.Range));
			foreach (var kv in res)
			{
				Log($"{kv.Key} = {kv.Value}");
			}

			//NOTE: may not work with option tenant_mode = required | required_experimental !
			Log("Read from global database should not see the changes...");
			Assert.That(await DbRead(this.Db, tr => tr.GetAsync(Key("tests", "hello"))), Is.EqualTo(Slice.FromString("global")));
			Assert.That(await DbRead(this.Db, tr => tr.GetAsync(Key("tests", "random"))), Is.EqualTo(Slice.Nil));

			// we should be able to read back the token with the same tenant
			Log($"Read from tenant {acme.Name} should see the changes...");
			var readBack = await TenantRead(acme.Name, tr => tr.GetAsync(Pack(("tests", "random"))));
			Log("> " + readBack);
			Assert.That(readBack, Is.EqualTo(random));

			Log($"Read from different tenant {contoso} should not see anything at all");
			readBack = await TenantRead(contoso.Name, tr => tr.GetAsync(Pack(("tests", "random"))));
			Log("> " + readBack);
			Assert.That(readBack, Is.Not.EqualTo(random), "Should not be able to read the key from a different tenant!");

			//TODO: if we know the tenant prefix, we can read from the global db !
		}

		[Test]
		public async Task Test_Can_Use_Tenant_Subspace()
		{
			// ensure that tenants exists
			var acme = await PrepareTestTenant(FdbTenantName.FromParts("Tests", "acme"));
			var contoso = await PrepareTestTenant(FdbTenantName.FromParts("Tests", "contoso", 123));

			static void TestEncode(FdbTenantSubspace subspace, Slice key)
			{
				var encoded = subspace.Bytes(key).ToSlice();
				Log($"> Append({key:K}) => {encoded}");

				var expected = subspace.Metadata.Prefix + key;
				Assert.That(encoded, Is.EqualTo(expected), $"Key '{key}' does not match expected encoding in tenant subspace {subspace.Name}");

				var decoded = subspace.ExtractKey(encoded);
				Assert.That(decoded, Is.EqualTo(key), "Key does not extract back to the original in tenant subspace");
			}

			static void TestPack(FdbTenantSubspace subspace, IVarTuple key)
			{
				var raw = TuPack.Pack(key);

				var encoded = subspace.Tuple(key).ToSlice();

				Log($"> Pack({key}) => {encoded}");

				var expected = subspace.Metadata.Prefix + raw;
				Assert.That(encoded, Is.EqualTo(expected), $"Key '{key}' does not match expected encoding in tenant subspace {subspace.Name}");

				var decoded = subspace.Unpack(encoded);
				Assert.That(decoded, Is.EqualTo(key), "Key does not unpack to the original in tenant subspace");
			}

			foreach (var tenant in new[] { acme, contoso })
			{
				Log($"Tenant {tenant.Name}");

				await GlobalVerify(async tr =>
				{
					var metadata = await Fdb.Tenants.GetTenantMetadata(tr, tenant.Name);
					Assert.That(metadata, Is.Not.Null, $"Tenant {tenant.Name} should exist!");
					Assert.That(metadata!.Prefix.Count, Is.GreaterThan(0), $"Tenant {tenant.Name} should have a non-empty prefix!");

					var subspace = metadata.GetSubspace();
					Assert.That(subspace, Is.Not.Null);
					Assert.That(subspace.Name, Is.EqualTo(tenant.Name), ".Name");
					Assert.That(subspace.Metadata, Is.SameAs(metadata), ".Metadata");
					Assert.That(subspace.GetPrefix(), Is.EqualTo(metadata.Prefix), ".GetPrefix()");
					Assert.That(subspace.GetPrefixUnsafe(), Is.EqualTo(metadata.Prefix), ".GetPrefixUnsafe()");

					TestEncode(subspace, Slice.Empty);
					TestEncode(subspace, Literal("hello"));
					TestEncode(subspace, FdbKey.MaxValue);

					TestPack(subspace, STuple.Empty);
					TestPack(subspace, STuple.Create("hello"));
					TestPack(subspace, STuple.Create("hello", "world"));
					TestPack(subspace, STuple.Create("hello", "world", 123));
				});
			}
		}

		#region Helpers...

		protected async Task<FdbTenantMetadata> PrepareTestTenant(FdbTenantName name)
		{
			// make sure we do not destroy a tenant that would be used in production by running these tests!
			Assume.That(TuPack.DecodeFirst<string>(name.ToSlice()), Is.EqualTo("Tests"), "FAILSAFE: only tenant names starting with (\"Tests\", ...) are allowed!");

			var tenant = await this.Db.ReadAsync(tr => Fdb.Tenants.GetTenantMetadata(tr, name), this.Cancellation);
			if (tenant != null)
			{ // clear this tenant

				Assume.That(tenant.Name, Is.EqualTo(name));
				Assume.That(tenant.Prefix.Count, Is.EqualTo(8), $"Prefix of tenants should be exactly 8 bytes: {tenant.Prefix}"); //note: as of version 7.x !
				//Log($"Found test tenant {name} with id #{tenant.Id} and prefix {tenant.Prefix}");
				await this.Db.WriteAsync(async tr =>
				{
					var hasKeys = await tr.GetRange(tenant.Range).AnyAsync();
					if (hasKeys)
					{
						//Log("> Purging keys from previous run!");
						tr.ClearRange(tenant.Range);
					}
				}, this.Cancellation);
			}
			else
			{ // create this tenant
				Log($"### Creating test tenant {name} !");
				await this.Db.WriteAsync(tr => Fdb.Tenants.CreateTenant(tr, name), this.Cancellation);

				tenant = await this.Db.ReadAsync(tr => Fdb.Tenants.GetTenantMetadata(tr, name), this.Cancellation);
				Assume.That(tenant, Is.Not.Null, $"Could not create test tenant {name}");
			}

			return tenant!;
		}

		protected Task<T> TenantRead<T>(FdbTenantName tenant, Func<IFdbReadOnlyTransaction, Task<T>> handler)
		{
			return DbRead(this.Db.GetTenant(tenant), handler);
		}

		protected Task<T> GlobalRead<T>(Func<IFdbReadOnlyTransaction, Task<T>> handler)
		{
			return DbRead(this.Db, handler);
		}

		protected Task<List<T>> TenantQuery<T>(FdbTenantName tenant, Func<IFdbReadOnlyTransaction, IAsyncQuery<T>> handler)
		{
			return DbQuery(this.Db.GetTenant(tenant), handler);
		}

		protected Task<List<T>> GlobalQuery<T>(Func<IFdbReadOnlyTransaction, IAsyncQuery<T>> handler)
		{
			return DbQuery(this.Db, handler);
		}

		protected Task TenantWrite(FdbTenantName tenant, Action<IFdbTransaction> handler)
		{
			return DbWrite(this.Db.GetTenant(tenant), handler);
		}

		protected Task GlobalWrite(Action<IFdbTransaction> handler)
		{
			return DbWrite(this.Db, handler);
		}

		protected Task TenantWrite(FdbTenantName tenant, Func<IFdbTransaction, Task> handler)
		{
			return DbWrite(this.Db.GetTenant(tenant), handler);
		}

		protected Task GlobalWrite(Func<IFdbTransaction, Task> handler)
		{
			return DbWrite(this.Db, handler);
		}

		protected Task TenantVerify(FdbTenantName tenant, Func<IFdbReadOnlyTransaction, Task> handler)
		{
			return DbVerify(this.Db.GetTenant(tenant), handler);
		}

		protected Task GlobalVerify(Func<IFdbReadOnlyTransaction, Task> handler)
		{
			return DbVerify(this.Db, handler);
		}

		#endregion

	}

}
