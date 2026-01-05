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
	using FoundationDB.Layers.Indexing;

	/// <summary>Expression that represents a lookup on an FdbIndex</summary>
	/// <typeparam name="TKey">Type of the Id of the entities being indexed</typeparam>
	/// <typeparam name="TValue">Type of the value that will be looked up</typeparam>
	[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
	public abstract class FdbQueryLookupExpression<TKey, TValue> : FdbQuerySequenceExpression<TKey>
	{

		/// <summary>Create a new expression that looks up a value in a source index</summary>
		protected FdbQueryLookupExpression(Expression source, ExpressionType op, Expression value)
		{
			Contract.Debug.Requires(source != null && value != null);
			this.Source = source;
			this.Operator = op;
			this.Value = value;
		}

		/// <summary>Source of the lookup (index, range read, ...)</summary>
		public Expression Source { get; }

		/// <summary>Operation applied to <see cref="Value"/> on the source</summary>
		public ExpressionType Operator { get; }

		/// <summary>Value looked up in the source</summary>
		public Expression Value { get; }

		/// <summary>Apply a custom visitor to this expression</summary>
		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryLookup(this);
		}

		/// <summary>Write a human-readable explanation of this expression</summary>
		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Visit(this.Source);
			builder.Writer.Write(".Lookup<{0}>(value {1} ", this.ElementType.Name, FdbExpressionHelpers.GetOperatorAlias(this.Operator));
			builder.Visit(this.Value);
			builder.Writer.Write(")");
		}

		/// <summary>Returns a textual representation of expression</summary>
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}.Lookup({1}, {2})", this.Source.ToString(), this.Operator, this.Value);
		}

	}

	/// <summary>Expression that represents a lookup on an FdbIndex</summary>
	/// <typeparam name="TKey">Type of the Id of the entities being indexed</typeparam>
	/// <typeparam name="TValue">Type of the value that will be looked up</typeparam>
	[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
	public class FdbQueryIndexLookupExpression<TKey, TValue> : FdbQueryLookupExpression<TKey, TValue>
	{

		internal FdbQueryIndexLookupExpression(FdbIndex<TKey, TValue> index, ExpressionType op, Expression value)
			: base(Expression.Constant(index), op, value)
		{
			Contract.Debug.Requires(index != null);
			this.Index = index;
		}

		/// <summary>Index queried by this expression</summary>
		public FdbIndex<TKey, TValue> Index { get; }

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		public override Expression<Func<IFdbReadOnlyTransaction, IAsyncQuery<TKey>>> CompileSequence()
		{
			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");
			Expression body;

			switch (this.Operator)
			{
				case ExpressionType.Equal:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<FdbIndex<TKey, TValue>, IFdbReadOnlyTransaction, TValue, bool, IAsyncQuery<TKey>>>(
						(index, trans, value, reverse) => index.Lookup(trans, value, reverse),
						Expression.Constant(this.Index, typeof(FdbIndex<TKey, TValue>)),
						prmTrans,
						this.Value,
						Expression.Constant(false, typeof(bool)) // reverse
					);
					break;
				}

				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<FdbIndex<TKey, TValue>, IFdbReadOnlyTransaction, TValue, bool, IAsyncQuery<TKey>>>(
						(index, trans, value, reverse) => index.LookupGreaterThan(trans, value, this.Operator == ExpressionType.GreaterThanOrEqual, reverse),
						Expression.Constant(this.Index, typeof(FdbIndex<TKey, TValue>)),
						prmTrans,
						this.Value,
						Expression.Constant(false, typeof(bool)) // reverse
					);
					break;
				}

				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
				{
					body = FdbExpressionHelpers.RewriteCall<Func<FdbIndex<TKey, TValue>, IFdbReadOnlyTransaction, TValue, bool, IAsyncQuery<TKey>>>(
						(index, trans, value, reverse) => index.LookupLessThan(trans, value, this.Operator == ExpressionType.LessThanOrEqual, reverse),
						Expression.Constant(this.Index, typeof(FdbIndex<TKey, TValue>)),
						prmTrans,
						this.Value,
						Expression.Constant(false, typeof(bool)) // reverse
					);
					break;
				}

				default:
				{
					throw new NotImplementedException();
				}
			}

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IAsyncQuery<TKey>>>(body, prmTrans);
		}

		/// <summary>Returns a textual representation of expression</summary>
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Index[{0}].Lookup({1}, {2})", this.Index.Location, this.Operator, this.Value);
		}

		/// <summary>Create a lookup expression on an index</summary>
		public static FdbQueryIndexLookupExpression<TKey, TValue> Lookup(FdbIndex<TKey, TValue> index, ExpressionType op, Expression value)
		{
			Contract.NotNull(index);
			Contract.NotNull(value);

			switch (op)
			{
				case ExpressionType.Equal:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
					break;
				default:
					throw new ArgumentException("Index lookups only support the following operators: '==', '!=', '>', '>=', '<' and '<='", nameof(op));
			}

			//TODO: IsAssignableFrom?
			if (value.Type != typeof(TValue)) throw new ArgumentException("Value must have a type compatible with the index", nameof(value));

			return new FdbQueryIndexLookupExpression<TKey, TValue>(index, op, value);
		}

		/// <summary>Create a lookup expression on an index</summary>
		/// <param name="index"></param>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static FdbQueryIndexLookupExpression<TKey, TValue> Lookup(FdbIndex<TKey, TValue> index, Expression<Func<TValue, bool>> expression)
		{
			Contract.NotNull(index);

			if (expression.Body is not BinaryExpression binary)
			{
				throw ThrowHelper.ArgumentException(nameof(expression), "Only binary expressions are allowed");
			}
			if (binary.Right is not ConstantExpression constant)
			{
				throw ThrowHelper.ArgumentException($"Left side of expression '{binary.Right}' must be a constant of type {typeof(TValue).Name}");
			}

			return Lookup(index, binary.NodeType, constant);
		}
	}

}
