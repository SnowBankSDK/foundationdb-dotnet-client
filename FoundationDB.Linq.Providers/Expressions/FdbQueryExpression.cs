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

	/// <summary>Base class of all query expression extensions</summary>
	public abstract class FdbQueryExpression : Expression
	{
		private readonly Type m_type;

		/// <summary>Base ctor</summary>
		/// <param name="type">Type of the results of this expression</param>
		protected FdbQueryExpression(Type type)
		{
			Contract.Debug.Requires(type != null);
			m_type = type;
		}

		/// <summary>Type of the results of the query</summary>
		public override Type Type => m_type;

		/// <summary>Always return <see cref="ExpressionType.Extension"/></summary>
		public override ExpressionType NodeType => ExpressionType.Extension;

		/// <summary>Shape of the query</summary>
		public abstract FdbQueryShape Shape { get; }

		/// <summary>Apply a custom visitor on this expression</summary>
		public abstract Expression Accept(FdbQueryExpressionVisitor visitor);

		public string GetDebugView()
		{
			var builder = new FdbQueryExpressionStringBuilder();
			builder.Visit(this);
			return builder.ToString();
		}

		/// <summary>Write a human-readable explanation of this expression</summary>
		public abstract void WriteTo(FdbQueryExpressionStringBuilder builder);

#if DEBUG
		public override string ToString()
		{
			return this.GetDebugView();
		}
#endif

	}

	/// <summary>Base class of all typed query expression extensions</summary>
	/// <typeparam name="T">Type of the results of this expression</typeparam>
	public abstract class FdbQueryExpression<T> : FdbQueryExpression
	{
		/// <summary>Base ctor</summary>
		protected FdbQueryExpression()
			: base(typeof(T))
		{ }

		/// <summary>Returns a new expression that will execute this query on a transaction and return a single result</summary>
		public abstract Expression<Func<IFdbReadOnlyTransaction, Task<T>>> CompileSingle();

	}

}
