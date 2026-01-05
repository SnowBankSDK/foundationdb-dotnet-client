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

namespace FoundationDB.Linq.Expressions
{

	/// <summary>Base class for FoundationDB LINQ Expression Tree visitors</summary>
	public abstract class FdbQueryExpressionVisitor:  ExpressionVisitor
	{

		/// <summary>Visit an extension node</summary>
		protected override Expression VisitExtension(Expression node)
		{
			if (node is FdbQueryExpression expr)
			{
				return Visit(expr);
			}
			return base.VisitExtension(node);
		}

		/// <summary>Visit this expression</summary>
		public virtual Expression Visit(FdbQueryExpression node)
		{
			return node.Accept(this);
		}

		/// <summary>Visit an Async Sequence query expression</summary>
		protected internal virtual Expression VisitAsyncEnumerable<T>(FdbQueryAsyncEnumerableExpression<T> node)
		{
			throw new NotImplementedException();
		}

		/// <summary>Visit a Range query expression</summary>
		protected internal virtual Expression VisitQueryRange(FdbQueryRangeExpression node)
		{
			throw new NotImplementedException();
		}

		/// <summary>Visit a Transform query expression</summary>
		protected internal virtual Expression VisitQueryTransform<T, R>(FdbQueryTransformExpression<T, R> node)
		{
			throw new NotImplementedException();
		}

		/// <summary>Visit a Filter query expression</summary>
		protected internal virtual Expression VisitQueryFilter<T>(FdbQueryFilterExpression<T> node)
		{
			throw new NotImplementedException();
		}

		/// <summary>Visit a Single query expression</summary>
		protected internal virtual Expression VisitQuerySingle<T, R>(FdbQuerySingleExpression<T, R> node)
		{
			throw new NotImplementedException();
		}

		/// <summary>Visit a Lookup query expression</summary>
		protected internal virtual Expression VisitQueryLookup<K, T>(FdbQueryLookupExpression<K, T> node)
		{
			throw new NotImplementedException();
		}

		/// <summary>Visit a Merge query expression</summary>
		protected internal virtual Expression VisitQueryMerge<K>(FdbQueryMergeExpression<K> node)
		{
			throw new NotImplementedException();
		}

	}

}
