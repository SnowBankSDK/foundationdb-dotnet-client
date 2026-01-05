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

	[PublicAPI]
	public static class FdbTenantExtensions
	{

		#region Transactions...

		/// <summary>Start a new read-only transaction on this tenant</summary>
		/// <param name="tenant">Tenant instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from the tenant.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// <code>
		/// using(var tr = db.BeginReadOnlyTransaction(CancellationToken.None))
		/// {
		///		var result = await tr.Get(Slice.FromString("Hello"));
		///		var items = await tr.GetRange(KeyRange.StartsWith(Slice.FromString("ABC"))).ToListAsync();
		/// }
		/// </code>
		/// </example>
		[Pure]
		public static IFdbReadOnlyTransaction BeginReadOnlyTransaction(this IFdbTenant tenant, CancellationToken ct)
		{
			Contract.NotNull(tenant);
			return tenant.BeginTransaction(FdbTransactionMode.ReadOnly | FdbTransactionMode.UseTenant, ct);
		}

		/// <summary>Start a new transaction on this tenant</summary>
		/// <param name="tenant">Tenant instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from or write to the tenant.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }</example>
		[Pure]
		public static IFdbTransaction BeginTransaction(this IFdbTenant tenant, CancellationToken ct)
		{
			Contract.NotNull(tenant);
			return tenant.BeginTransaction(FdbTransactionMode.Default | FdbTransactionMode.UseTenant, ct);
		}

		#endregion

	}

}
