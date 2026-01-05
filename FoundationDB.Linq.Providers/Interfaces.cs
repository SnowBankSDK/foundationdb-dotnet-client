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

namespace FoundationDB.Linq
{
	using FoundationDB.Layers.Indexing;

	/// <summary>Base interface of all queryable objects</summary>
	public interface IFdbAsyncQueryable
	{
		/// <summary>Type of the results of the query</summary>
		Type Type { get; }

		/// <summary>Expression describing the intent of the query</summary>
		FdbQueryExpression? Expression { get; }

		/// <summary>Provider that created this query</summary>
		IFdbAsyncQueryProvider Provider { get; }
	}

	/// <summary>Queryable that returns a single result of type T</summary>
	/// <typeparam name="T"></typeparam>
	public interface IFdbAsyncQueryable<out T> : IFdbAsyncQueryable
	{
	}

	/// <summary>Queryable that returns a sequence of elements of type T</summary>
	/// <typeparam name="T"></typeparam>
	[PublicAPI]
	public interface IFdbAsyncSequenceQueryable<out T> : IFdbAsyncQueryable
	{
		/// <summary>Type of elements of the sequence</summary>
		Type ElementType { get; }
	}

	/// <summary>Query provider</summary>
	[PublicAPI]
	public interface IFdbAsyncQueryProvider 
	{
		/// <summary>Wraps a query expression into a new queryable</summary>
		IFdbAsyncQueryable CreateQuery(FdbQueryExpression expression);

		/// <summary>Wraps a typed query expression into a new queryable</summary>
		IFdbAsyncQueryable<TResult> CreateQuery<TResult>(FdbQueryExpression<TResult> expression);

		/// <summary>Wraps a type sequence query expression into a new queryable</summary>
		IFdbAsyncSequenceQueryable<TResult> CreateSequenceQuery<TResult>(FdbQuerySequenceExpression<TResult> expression);

		/// <summary>Execute a query expression into a typed result</summary>
		Task<TResult> ExecuteAsync<TResult>(FdbQueryExpression expression, CancellationToken ct = default);
	}

	/// <summary>Queryable transaction</summary>
	[PublicAPI]
	public interface IFdbTransactionQueryable : IFdbAsyncQueryable
	{
		// Note: this interface is only used to hook extension methods specific to transaction queries

		/// <summary>Transaction used by this query</summary>
		IFdbReadOnlyTransaction Transaction { get; }

	}

	/// <summary>Queryable index</summary>
	[PublicAPI]
	public interface IFdbIndexQueryable<TId, TValue> : IFdbAsyncQueryable
	{
		// Note: this interface is only used to hook extension methods specific to index queries

		/// <summary>Index used by this query</summary>
		FdbIndex<TId, TValue> Index { get; }

	}

}
