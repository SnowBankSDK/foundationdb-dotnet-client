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

namespace FoundationDB.Linq.Providers
{

	/// <summary>Async LINQ query that returns an async sequence of items</summary>
	/// <typeparam name="T">Type of the items in the sequence</typeparam>
	public class FdbAsyncSequenceQuery<T> : FdbAsyncQuery<T>, IFdbAsyncSequenceQueryable<T>
	{

		/// <summary>Async LINQ query that will execute under a retry loop on a specific Database instance</summary>
		public FdbAsyncSequenceQuery(IFdbDatabase db, FdbQueryExpression expression)
			: base(db, expression)
		{ }

		/// <summary>Async LINQ query that will execute on a specific Transaction instance</summary>
		public FdbAsyncSequenceQuery(IFdbReadOnlyTransaction trans, FdbQueryExpression expression)
			: base(trans, expression)
		{ }

		/// <summary>Type of the elements of the sequence</summary>
		public Type ElementType => typeof(T);

		/// <summary>Return an async sequence that will return the results of this query</summary>
		public IAsyncQuery<T> ToEnumerable()
		{
			return AsyncQuery.Create((_, mode, _) => GetEnumerator(this, mode), null, this.Transaction?.Cancellation ?? CancellationToken.None); //BUGBUG: token when db only??
		}

	}

}
