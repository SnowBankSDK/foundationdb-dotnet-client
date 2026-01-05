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
	public static partial class Fdb
	{

		internal static class Errors
		{

			internal static Exception CannotExecuteOnNetworkThread()
			{
				return new InvalidOperationException("Cannot commit transaction from the Network Thread!");
			}

			#region Keys / Values...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static Exception KeyCannotBeNull(string paramName = "key")
			{
				return new ArgumentException("Key cannot be null.", paramName);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static Exception KeyIsTooBig(ReadOnlySpan<byte> key, string paramName = "key")
			{
				return new ArgumentException($"Key is too big ({key.Length} > {Fdb.MaxKeySize}).", paramName);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static Exception ValueCannotBeNull(string paramName = "value")
			{
				return new ArgumentException("Value cannot be null", paramName);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static Exception ValueIsTooBig(ReadOnlySpan<byte> value, string paramName = "value")
			{
				return new ArgumentException($"Value is too big ({value.Length} > {Fdb.MaxValueSize}).", paramName);
			}

			internal static Exception InvalidKeyOutsideDatabaseNamespace(IFdbDatabase db, ReadOnlySpan<byte> key)
			{
				Contract.Debug.Requires(db != null);
				return new FdbException(
					FdbError.KeyOutsideLegalRange,
#if DEBUG
					$"An attempt was made to use a key '{FdbKey.Dump(key)}' that is outside of the root keyspace {db.Root}."
#else
					$"An attempt was made to use a key that is outside of the root Root {db.Root}"
#endif
				);
			}

			#endregion

			internal static Exception FailedToRegisterTransactionOnDatabase(IFdbTransaction transaction, FdbDatabase db)
			{
				Contract.Debug.Requires(transaction != null && db != null);
				return new InvalidOperationException($"Failed to register transaction #{transaction.Id}");
			}

			internal static Exception FailedToRegisterTenantOnDatabase(IFdbTenant tenant, FdbDatabase db)
			{
				Contract.Debug.Requires(tenant!= null && db != null);
				return new InvalidOperationException($"Failed to register tenant @{tenant.Name}");
			}

			internal static Exception FailedToRegisterTransactionOnTenant(IFdbTransaction transaction, FdbTenant tenant)
			{
				Contract.Debug.Requires(transaction != null && tenant != null);
				return new InvalidOperationException($"Failed to register transaction #{transaction.Id} on tenant @{tenant.Name}");
			}

			internal static Exception CannotIncrementKey()
			{
				return new ArgumentException("Key must contain at least one byte not equal to 0xFF");
			}
		}

	}

}
