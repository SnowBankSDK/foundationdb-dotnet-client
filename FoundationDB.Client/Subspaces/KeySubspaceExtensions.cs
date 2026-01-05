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

	/// <summary>Extensions methods and helpers to work with Key Subspaces</summary>
	[PublicAPI]
	public static class KeySubspaceExtensions
	{

		#region Copy...

		/// <summary>Create a new copy of a subspace prefix</summary>
		[Pure]
		internal static Slice StealPrefix(IKeySubspace subspace)
		{
			//note: we can work around the 'security' in top directory partition by accessing their key prefix without triggering an exception!
			return subspace is KeySubspace ks
				? ks.GetPrefixUnsafe().Copy()
				: subspace.GetPrefix().Copy();
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure]
		public static KeySubspace Copy(this IKeySubspace subspace)
		{
			Contract.NotNull(subspace);

			return new(StealPrefix(subspace), subspace.Context);
		}

		#endregion

		/// <summary>Test if a key is inside the range of keys logically contained by this subspace</summary>
		/// <param name="subspace">Subspace used for the test</param>
		/// <param name="absoluteKey">Key to test</param>
		/// <returns>True if the key can exist inside the current subspace.</returns>
		/// <remarks>Please note that this method does not test if the key *actually* exists in the database, only if the key is not outside the range of keys defined by the subspace.</remarks>
		public static bool Contains(this IKeySubspace subspace, Slice absoluteKey)
		{
			return !absoluteKey.IsNull && subspace.Contains(absoluteKey.Span);
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static void ClearRange(this IFdbTransaction trans, IKeySubspace subspace)
		{
			Contract.Debug.Requires(trans != null && subspace != null);

			//BUGBUG: should we call subspace.ToRange() ?
			trans.ClearRange(subspace.ToRange(inclusive: true));
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static Task ClearRangeAsync(this IFdbRetryable db, IKeySubspace subspace, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(subspace);

			return db.WriteAsync((tr) => ClearRange(tr, subspace), ct);
		}

	}

}
