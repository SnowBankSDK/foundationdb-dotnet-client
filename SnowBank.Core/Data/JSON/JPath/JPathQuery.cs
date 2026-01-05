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

namespace SnowBank.Data.Json.JPath
{

	/// <summary>Represents a query in a JSON Document</summary>
	[DebuggerDisplay("{Text,nq}")]
	public sealed class JPathQuery
	{

		/// <summary>Parse expression that represents this query</summary>
		public JPathExpression Expression { get; }

		/// <summary>Original text of this query</summary>
		public string Text { get; }

		internal JPathQuery(JPathExpression expression, string text)
		{
			Contract.NotNull(expression);
			Contract.NotNullOrEmpty(text);

			this.Expression = expression;
			this.Text = text;
		}

		/// <inheritdoc />
		public override string ToString() => this.Text;

		/// <inheritdoc />
		public override int GetHashCode() => this.Text.GetHashCode();

		/// <summary>Return the first value that match this query, or <see cref="JsonNull.Missing"/> if no match was found</summary>
		[Pure]
		public JsonValue FirstOrDefault(JsonValue? root) => !root.IsNullOrMissing() ? FirstOrDefault(root, this.Expression) : JsonNull.Missing;

		/// <summary>Return all the nodes that match this query, or an empty sequence if no match was found</summary>
		[Pure]
		public IEnumerable<JsonValue> Select(JsonValue? root) => !root.IsNullOrMissing() ? Select(root, this.Expression) : [];

		/// <summary>Return the first value that match the given JPath query, or <see cref="JsonNull.Missing"/> if no match was found</summary>
		/// <exception cref="FormatException">If the query is invalid or malformed</exception>
		[Pure]
		public static JsonValue FirstOrDefault(JsonValue? root, string queryText)
		{
			Contract.NotNull(queryText);
			return root.IsNullOrMissing() ? JsonNull.Missing : ParseExpression(queryText).Iterate(root, root).FirstOrDefault() ?? JsonNull.Missing;
		}

		/// <summary>Return the first value that match the given JPath expression, or <see cref="JsonNull.Missing"/> if no match was found</summary>
		[Pure]
		public static JsonValue FirstOrDefault(JsonValue? root, JPathExpression expression)
		{
			Contract.NotNull(expression);
			return (!root.IsNullOrMissing() ? expression.Iterate(root, root).FirstOrDefault() : null) ?? JsonNull.Missing;
		}

		/// <summary>Returns all the nodes that match the given JPath query, or an empty sequence if no match was found</summary>
		/// <exception cref="FormatException">If the query is invalid or malformed</exception>
		[Pure]
		public static IEnumerable<JsonValue> Select(JsonValue? root, string queryText)
		{
			Contract.NotNullOrEmpty(queryText);
			return !root.IsNullOrMissing() ? ParseExpression(queryText).Iterate(root, root) : [];
		}

		/// <summary>Return all the nodes that match the given JPath expression</summary>
		[Pure]
		public static IEnumerable<JsonValue> Select(JsonValue? root, JPathExpression expression)
		{
			Contract.NotNull(expression);
			return !root.IsNullOrMissing() ? expression.Iterate(root, root) : [];
		}

		/// <summary>Parse the given JPath expression</summary>
		/// <exception cref="FormatException">If the query is invalid or malformed</exception>
		[Pure]
		public static JPathExpression ParseExpression(string queryText)
		{
			Contract.NotNullOrEmpty(queryText);
			var context = new JPathParser(queryText.AsSpan());
			return context.ParseExpression(JPathExpression.Root);
		}

		/// <summary>Parse the given JPath query</summary>
		/// <exception cref="FormatException">If the query is invalid or malformed</exception>
		[Pure]
		public static JPathQuery Parse(string queryText)
		{
			Contract.NotNullOrEmpty(queryText);
			return new JPathQuery(ParseExpression(queryText), queryText);
		}

	}

}
