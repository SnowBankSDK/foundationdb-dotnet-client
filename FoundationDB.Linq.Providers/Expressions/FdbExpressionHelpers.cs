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

	/// <summary>Helper class for working with extension expressions</summary>
	public static class FdbExpressionHelpers
	{

		#region DebugView...

#pragma warning disable IL2026
		private static readonly Func<Expression, string> DebugViewGetter = CreateDebugViewGetter();
#pragma warning restore IL2026

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		private static Func<Expression, string> CreateDebugViewGetter()
		{
			try
			{
				var prm = Expression.Parameter(typeof(Expression), "expr");
				var func = Expression.Lambda<Func<Expression, string>>(
					Expression.PropertyOrField(prm, "DebugView"),
					prm
				).Compile();

				// test if it really works
				func(Expression.Constant(123));

				return func;
			}
			catch
			{
				return (expr) => expr.ToString();
			}
		}

		/// <summary>Return the internal DebugView property of an expression</summary>
		/// <param name="expression">Expression</param>
		/// <returns>Debug string</returns>
		public static string GetDebugView(this Expression? expression)
		{
			return expression != null ? DebugViewGetter(expression) : "<null>";
		}

		#endregion

		/// <summary>Extract a constant value from an expression, if possible</summary>
		public static T EvaluateConstantExpression<T>(Expression expression)
		{
			var expr = expression.CanReduce ? expression.Reduce() : expression;

			if (expr.Type != typeof(T))
			{
				throw new InvalidOperationException("Expression type mismatch");
			}
			if (expr is ConstantExpression constant)
			{
				return (T) constant.Value!;
			}

			throw new NotSupportedException($"Unsupported expression {expr}: '{expression.GetType().Name}' should return a constant value");
		}

		private static Task<T> Inline<T>(Func<IFdbReadOnlyTransaction, T> func, IFdbReadOnlyTransaction trans)
		{
			try
			{
				if (trans.Cancellation.IsCancellationRequested) return Task.FromCanceled<T>(trans.Cancellation);
				return Task.FromResult(func(trans));
			}
			catch(Exception e)
			{
				return Task.FromException<T>(e);
			}
		}

		/// <summary>Wraps an expression into another expression that returns a <see cref="Task{T}"/> with the result of the expression</summary>
		public static Expression<Func<IFdbReadOnlyTransaction, Task<T>>> ToTask<T>(Expression<Func<IFdbReadOnlyTransaction, T>> lambda)
		{
			// rewrite a Func<..., T> into a Func<..., Task<T>>

			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			var body = RewriteCall<Func<IFdbReadOnlyTransaction, Func<IFdbReadOnlyTransaction, T>, Task<T>>>(
				(trans, func) => Inline<T>(func, trans),
				prmTrans,
				lambda
			);

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, Task<T>>>(
				body,
				prmTrans
			);
		}

		internal static Task<TResult> ExecuteEnumerable<TSource, TResult>(Func<IFdbReadOnlyTransaction, IAsyncQuery<TSource>> generator, Func<IAsyncQuery<TSource>, Task<TResult>> lambda, IFdbReadOnlyTransaction trans)
		{
			Contract.Debug.Requires(generator != null && lambda != null && trans != null);
			if (trans.Cancellation.IsCancellationRequested) return Task.FromCanceled<TResult>(trans.Cancellation);
			try
			{
				var enumerable = generator(trans);
				if (enumerable == null!)
				{
					return Task.FromException<TResult>(new InvalidOperationException("Source query returned null"));
				}
				return lambda(enumerable);
			}
			catch (Exception e)
			{
				return Task.FromException<TResult>(e);
			}
		}

		internal static string GetOperatorAlias(ExpressionType op)
		{
			switch (op)
			{
				case ExpressionType.Equal: return "==";
				case ExpressionType.NotEqual: return "!=";
				case ExpressionType.GreaterThan: return ">";
				case ExpressionType.GreaterThanOrEqual: return ">=";
				case ExpressionType.LessThan: return "<";
				case ExpressionType.LessThanOrEqual: return "<=";

				case ExpressionType.Negate: return "-";
				case ExpressionType.Not: return "!";

				case ExpressionType.And: return "&";
				case ExpressionType.Or: return "|";

				case ExpressionType.AndAlso: return "&&";
				case ExpressionType.OrElse: return "||";

				case ExpressionType.Add: return "+";
				case ExpressionType.Subtract: return "-";
				case ExpressionType.Multiply: return "*";
				case ExpressionType.Divide: return "/";
				case ExpressionType.Modulo: return "%";

				default:
					return op.ToString();
			}
		}

		#region Method Call Rewritting...

		/// <summary>Rewrite a method call capture by a lambda, into the equivalent call where the parameters have been replaced by the actual values</summary>
		/// <typeparam name="TDelegate"></typeparam>
		/// <param name="lambda"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		/// <remarks>Typical use case is when we have a dummy Expression&lt;Func&lt;...&gt;&gt; used to get a method call signature, and we want to remplace the parameters of the lambda with the actual values that will be used at runtime</remarks>
		public static Expression RewriteCall<TDelegate>(Expression<TDelegate> lambda, params Expression[] arguments)
		{
			Contract.NotNull(lambda);
			if (arguments.Length != lambda.Parameters.Count)
			{
				throw new InvalidOperationException("Argument count mismatch");
			}

			var rewritten = new Dictionary<ParameterExpression, Expression>(arguments.Length);
			for (int i = 0; i < arguments.Length;i++)
			{
				var arg = lambda.Parameters[i];
				if (!arg.Type.IsAssignableFrom(arguments[i].Type))
				{
					throw ThrowHelper.InvalidOperationException($"Argument {arg.Name} of type {arg.Type.Name} does not match type {arguments[i].Type.Name}");
				}
				rewritten[arg] = arguments[i];
			}

			return new ParameterRewritingVisitor(rewritten).Visit(lambda.Body);
		}

		/// <summary>Visitor that replace paramters of a lambda with other expressions</summary>
		private sealed class ParameterRewritingVisitor : ExpressionVisitor
		{

			public Dictionary<ParameterExpression, Expression> Parameters { get; }

			public ParameterRewritingVisitor(Dictionary<ParameterExpression, Expression> rewrittenParameters)
			{
				Contract.Debug.Requires(rewrittenParameters != null && rewrittenParameters.Count > 0);
				this.Parameters = rewrittenParameters;
			}

			protected override Expression VisitParameter(ParameterExpression node)
			{
				return this.Parameters.GetValueOrDefault(node, node);
			}

		}

		#endregion

	}

}
