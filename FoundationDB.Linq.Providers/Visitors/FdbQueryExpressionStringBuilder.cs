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
	using System.Collections.ObjectModel;

	/// <summary>Helper class for building a string representation of a FoundationDB LINQ Expression Tree</summary>
	public class FdbQueryExpressionStringBuilder : FdbQueryExpressionVisitor
	{
		private readonly FdbDebugStatementWriter m_writer;

		/// <summary>Creates a new expression string builder</summary>
		public FdbQueryExpressionStringBuilder()
			: this(null)
		{ }

		/// <summary>Creates a new expression string builder with a specific writer</summary>
		public FdbQueryExpressionStringBuilder(FdbDebugStatementWriter? writer)
		{
			m_writer = writer ?? new FdbDebugStatementWriter();
		}

		/// <summary>Writer used by this builder</summary>
		public FdbDebugStatementWriter Writer => m_writer;

		/// <summary>Returns the text expression that has been written so far</summary>
		public override string ToString()
		{
			return m_writer.Buffer.ToString();
		}

		/// <summary>Visit a node and appends it to the string builder</summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public override Expression Visit(FdbQueryExpression node)
		{
			node.WriteTo(this);
			return node;
		}

		private void VisitExpressions<TExpr>(ReadOnlyCollection<TExpr>? expressions, string open, string close, string sep)
			where TExpr : Expression
		{
			if (expressions == null || expressions.Count == 0)
			{
				m_writer.Write(open).Write(close);
				return;
			}

			m_writer.Write(open);
			bool first = true;
			foreach(var expr in expressions)
			{
				if (first) first = false; else m_writer.Write(sep);
				Visit(expr);
			}
			m_writer.Write(close);
		}

		private void VisitValues<T>(ICollection<T>? values, Action<T> handler, string open, string close, string sep)
		{
			if (values == null || values.Count == 0)
			{
				m_writer.Write(open).Write(close);
				return;
			}

			m_writer.Write(open);
			bool first = true;
			foreach (var expr in values)
			{
				if (first)
				{
					first = false;
				}
				else
				{
					m_writer.Write(sep);
				}

				handler(expr);
			}
			m_writer.Write(close);
		}

		/// <summary>Visit a Lambda expression</summary>
		protected override Expression VisitLambda<T>(Expression<T> node)
		{
			VisitExpressions(node.Parameters, "(", ")", ", ");
			m_writer.Write(" => ");
			Visit(node.Body);

			return node;
		}

		/// <summary>Visit a Parameter expression</summary>
		protected override Expression VisitParameter(ParameterExpression node)
		{
			m_writer.Write(node.Name ?? "");

			return node;
		}

		/// <summary>Visit a Member expression</summary>
		protected override Expression VisitMember(MemberExpression node)
		{
			Visit(node.Expression);
			m_writer.Write(".").Write(node.Member.Name);

			return node;
		}

		/// <summary>Visit a Binary expression</summary>
		protected override Expression VisitBinary(BinaryExpression node)
		{
			Visit(node.Left);
			m_writer.Write(" {0} ", FdbExpressionHelpers.GetOperatorAlias(node.NodeType));
			Visit(node.Right);

			return node;
		}

		/// <summary>Visit a Method Call expression</summary>
		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			if (node.Object == null)
			{
				m_writer.Write(node.Method.DeclaringType!.Name);
			}
			else
			{
				Visit(node.Object);
			}

			m_writer.Write(".{0}", node.Method.Name);

			if (node.Method.IsGenericMethod)
			{
				VisitValues(node.Method.GetGenericArguments(), (type) => m_writer.Write(type.Name), "<", ">", ", ");
			}

			if (node.Arguments != null!)
			{
				VisitExpressions(node.Arguments, "(", ")", ", ");
			}

			return node;
		}

		/// <summary>Visit a Constant expression</summary>
		protected override Expression VisitConstant(ConstantExpression node)
		{
			m_writer.Write(node.GetDebugView());
			return node;
		}

#if DEBUG
		public static void Test()
		{

			Expression<Func<int, int, string>> func = (x, y) => string.Format("{0} x {1} == {2}", x, y, (x * y));

			var w = new FdbQueryExpressionStringBuilder();
			w.Visit(func);

			Console.WriteLine(w.ToString());

		}
#endif

	}

}
