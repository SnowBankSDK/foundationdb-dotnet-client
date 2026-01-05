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

namespace FoundationDB.Client
{

	/// <summary>Tenant configuration mode of a FoundationDB cluster</summary>
	[PublicAPI]
	public enum FdbTenantMode
	{

		/// <summary>Invalid or unknown</summary>
		Invalid = -1,

		/// <summary>Tenant support is disabled</summary>
		Disabled = 0,

		/// <summary>Tenant support is enabled, but optional. Transactions that do not specify a tenant are allowed to execute.</summary>
		Optional = 1,

		/// <summary>Tenant support is enabled and required. Transactions that do not specify a tenant will fail to execute.</summary>
		Required = 2,

	}

	public static partial class Fdb
	{

		/// <summary>Helper methods for managing Tenants in a FoundationDB cluster</summary>
		[PublicAPI]
		public static class Tenants
		{

			/// <summary><c>\xFF\xFF/management/tenant/map/</c></summary>
			private static readonly Slice TenantMapPrefix = Fdb.System.SpecialKey("/management/tenant/map/");

			/// <summary><c>\xFF\xFF/management/tenant/map/...</c></summary>
			private static readonly KeyRange TenantMapRange = KeyRange.Create(TenantMapPrefix, Fdb.System.SpecialKey("/management/tenant/map0"));

			/// <summary><c>\xFF/conf/tenant_mode</c></summary>
			private static readonly Slice TenantModeKey = Fdb.System.ConfigKey("tenant_mode");

			/// <summary>Queries the current <see cref="FdbTenantMode"/> of the cluster</summary>
			public static async Task<FdbTenantMode> GetTenantMode(IFdbReadOnlyTransaction tr)
			{
				tr.Options.WithReadAccessToSystemKeys();
				var val = await tr.GetAsync(TenantModeKey).ConfigureAwait(false);
				if (val.IsNullOrEmpty) return FdbTenantMode.Disabled;
				// this is a number represented as a decimal string
				// so "0" is Disabled, "1" is Optional, etc...
				if (!int.TryParse(val.ToString(), out var value))
				{
					return FdbTenantMode.Invalid;
				}

				return (FdbTenantMode) value;
			}

			/// <summary>Creates a new tenant in the cluster</summary>
			public static void CreateTenant(IFdbTransaction tr, FdbTenantName name)
			{
				// just setting the key in the tenant module will trigger the creation of the tenant, once the transaction commits
				// the tenant module will do the prefix allocation and generate the new tenant entry in the cluster.
				tr.Options.WithSpecialKeySpaceEnableWrites();
				tr.Set(TenantMapPrefix + name.Value, Slice.Empty);
				//note: if the tenant already exists, this is a "no-op"
			}

			/// <summary>Tests if a tenant already exists in the cluster</summary>
			public static async Task<bool> HasTenant(IFdbReadOnlyTransaction tr, FdbTenantName name)
			{
				var value = await tr.GetAsync(TenantMapPrefix + name.Value).ConfigureAwait(false);
				return value.HasValue;
			}

			private static FdbTenantMetadata ParseTenantMetadata(FdbTenantName name, Slice data)
			{
				Contract.Debug.Requires(data.Count > 0);

				// as of version 7.3, this is a JSON document that looks like { "id":xxx, "prefix": { "base64": "AAA...E=", "printable": "\u0000\u0000\u000...\u0042" } }
				// The current implementation assign each tenant a new unique sequential integer id, and it's prefix will be this id encoded as 64-bit big-endian.

				var obj = CrystalJson.Parse(data).AsObjectOrDefault();
				if (obj == null) throw new FormatException("Invalid Tenant Metadata format: required JSON document is missing.");

				var id = obj.Get<int?>("id", null);
				if (id == null) throw new FormatException("Invalid Tenant Metadata format: required 'id' field is missing.");

				if (!obj.ContainsKey("prefix")) throw new FormatException("Invalid Tenant Metadata format: required 'prefix' field is missing.");

				var prefixObj = obj.GetObject("prefix");
				var prefixBase64 = prefixObj.Get<string?>("base64", null);
				if (prefixBase64 == null) throw new FormatException("Invalid Tenant Metadata format: required 'prefix.base64' field is missing.");

				var prefix = Slice.FromBase64(prefixBase64);

				return new FdbTenantMetadata()
				{
					Name = name,
					Id = id.Value,
					Prefix = prefix,
					Range = KeyRange.StartsWith(prefix),
					Raw = data,
				};
			}

			/// <summary>Fetches the metadata for a tenant in the cluster</summary>
			public static async Task<FdbTenantMetadata?> GetTenantMetadata(IFdbReadOnlyTransaction tr, FdbTenantName name)
			{
				var data = await tr.GetAsync(TenantMapPrefix + name.Value).ConfigureAwait(false);
				return data.HasValue ? ParseTenantMetadata(name, data) : null;
			}

			/// <summary>Deletes an existing tenant in the cluster</summary>
			public static void DeleteTenant(IFdbTransaction tr, FdbTenantName name)
			{
				Contract.NotNull(tr);

				// just deleting the key in the tenant module will trigger the deletion of the tenant, once the transaction commits
				tr.Options.WithSpecialKeySpaceEnableWrites();
				tr.Clear(TenantMapPrefix + name.Value);
			}

			public static IAsyncQuery<FdbTenantMetadata> QueryTenants(IFdbReadOnlyTransaction tr, Slice prefix = default, Func<Slice, bool>? filter = null)
			{
				Contract.NotNull(tr);
				return tr
					// get all keys in the "/management/tenant/map/...." table
					.GetRange(prefix.IsNullOrEmpty ? TenantMapRange : KeyRange.StartsWith(TenantMapPrefix + prefix))
					// remove the table prefix to get only the name
					.Select(kv => (Name: kv.Key.Substring(TenantMapPrefix.Count), Data: kv.Value))
					// optional filtering on the names
					.Where(kv => filter == null || filter(kv.Name))
					// parse the name and the metadata
					.Select(kv => ParseTenantMetadata(FdbTenantName.Create(kv.Name), kv.Data));
			}

		}

	}

}
