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

	/// <summary>Expression that represents a GetRange query using a pair of key selectors</summary>
	[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
	public class FdbQueryRangeExpression : FdbQuerySequenceExpression<KeyValuePair<Slice, Slice>>
	{

		internal FdbQueryRangeExpression(KeySelectorPair range, FdbRangeOptions? options)
		{
			this.Range = range;
			this.Options = options;
		}

		/// <summary>Returns the pair of key selectors for this range query</summary>
		public KeySelectorPair Range { get; }

		/// <summary>Returns the options for this range query</summary>
		public FdbRangeOptions? Options { get; }

		/// <summary>Visit this expression</summary>
		public override Expression Accept(FdbQueryExpressionVisitor visitor)
		{
			return visitor.VisitQueryRange(this);
		}

		/// <summary>Explains this range query</summary>
		public override void WriteTo(FdbQueryExpressionStringBuilder builder)
		{
			builder.Writer.WriteLine("Range(").Enter()
				.WriteLine("Start({0}),", this.Range.Begin.ToString())
				.WriteLine("Stop({0})", this.Range.End.ToString())
			.Leave().Write(")");
		}

		/// <summary>Returns a new expression that creates an async sequence that will execute this query on a transaction</summary>
		public override Expression<Func<IFdbReadOnlyTransaction, IAsyncQuery<KeyValuePair<Slice, Slice>>>> CompileSequence()
		{
			var prmTrans = Expression.Parameter(typeof(IFdbReadOnlyTransaction), "trans");

			var body = FdbExpressionHelpers.RewriteCall<Func<IFdbReadOnlyTransaction, KeySelectorPair, FdbRangeOptions, IFdbRangeQuery<KeyValuePair<Slice, Slice>>>>(
				(trans, range, options) => trans.GetRange(range, options),
				prmTrans,
				Expression.Constant(this.Range, typeof(KeySelectorPair)),
				Expression.Constant(this.Options, typeof(FdbRangeOptions))
			);

			return Expression.Lambda<Func<IFdbReadOnlyTransaction, IAsyncQuery<KeyValuePair<Slice, Slice>>>>(
				body,
				prmTrans
			);
		}

		/// <summary>Returns a short description of this range query</summary>
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "GetRange({0}, {1})", this.Range.Begin.ToString(), this.Range.End.ToString());
		}


	}

}
