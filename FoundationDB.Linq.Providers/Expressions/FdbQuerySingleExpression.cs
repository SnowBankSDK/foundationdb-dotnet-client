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
	using System.Reflection;

	/// <summary>Base class of all queries that return a single element</summary>
	/// <typeparam name="TSource">Type of the elements of the source sequence</typeparam>
	/// <typeparam name="TResult">Type of the result of the query</typeparam>
	[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
	public class FdbQuerySingleExpression<TSource, TResult> : FdbQueryExpression<TResult>
	{
		/// <summary>Create a new expression that returns a single result from a source sequence</summary>
		public FdbQuerySingleExpression(FdbQuerySequenceExpression<TSource> sequence, string name, Expression<Func<IAsyncQuery<TSource>, Task<TResult>>> lambda)
		{
			Contract.Debug.Requires(sequence != null && lambda != null);
			this.Sequence = sequence;
			this.Name = name;
			this.Handler = lambda;
		}

		/// <summary>Always returns <see cref="FdbQueryShape.Single"/></summary>
		public override FdbQueryShape Shape => FdbQueryShape.Single;

		/// <summary>Source sequence</summary>
		public FdbQuerySequenceExpression<TSource> Sequence { get; }

		/// <summary>Name of this query</summary>
		public string Name { get; }

		public Expression<Func<IAsyncQuery<TSource>, Task<TResult>>> Handler { get; }

		/// <summary>Apply a custom visitor to this expression</summary>
		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQuerySingle(this);
		}

		/// <summary>Write a human-readable explanation of this expression</summary>
		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Writer.WriteLine("{0}(", this.Name).Enter();
			builder.Visit(this.Sequence);
			builder.Writer.Leave().Write(")");
		}

		/// <summary>Returns a new expression that will execute this query on a transaction and return a single result</summary>
		public override Expression<Func<IFdbReadOnlyTransaction, Task<TResult>>> CompileSingle()
		{
			// We want to generate: (trans, ct) => ExecuteEnumerable(source, lambda, trans, ct);

			var sourceExpr = this.Sequence.CompileSequence();

			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			var method = typeof(FdbExpressionHelpers).GetMethod(nameof(FdbExpressionHelpers.ExecuteEnumerable), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(TSource), typeof(TResult));
			Contract.Debug.Assert(method != null, "ExecuteEnumerable helper method is missing!");

			var body = Expression.Call(
				method,
				sourceExpr,
				this.Handler,
				prmTrans);

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, Task<TResult>>>(
				body,
				prmTrans
			);
		}

		/// <summary>Returns a textual representation of expression</summary>
		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "{0}({1})", this.Name, this.Sequence.ToString());
		}
	}

}
