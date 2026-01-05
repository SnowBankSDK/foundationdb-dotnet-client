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
	using System.Linq.Expressions;

	/// <summary>Base class of a node in a JPath query AST</summary>
	public abstract class JPathExpression : IEquatable<JPathExpression>
	{

		internal abstract IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current);

		/// <summary>Returns the item at the specified index in a JSON Array, or <c>null</c></summary>
		protected static JsonValue? GetAtIndex(JsonValue item, int index)
		{
			if (item is JsonArray array)
			{
				if (index < 0) index += array.Count;
				return (uint) index < (uint) array.Count ? array[index] : null;
			}
			return null;
		}

		/// <summary>Tests if a JSON value is "truthy" or "falsy"</summary>
		protected static bool IsTruthy(JsonValue x) => x switch
		{
			null => false,
			JsonNull => false,
			JsonBoolean b => b.Value,
			JsonString s => s.Length > 0,
			JsonNumber n => !n.IsDefault,
			JsonDateTime d => !d.IsDefault,
			JsonArray a => a.Count > 0,
			_ => true
		};

		/// <summary>Evaluates a "pseudo property" of a JSON Array (<c>"$length"</c>, <c>"$first"</c>, ...)</summary>
		protected static JsonValue? ArrayPseudoProperty(JsonArray array, string name)
		{
			switch (name)
			{
				case "$length": return array.Count;
				case "$first": return array.Count > 0 ? array[0] : null;
				case "$last": return array.Count > 0 ? array[^1] : null;
				default: return null; //TODO: or throw "invalid array pseudo-property?"
			}
		}

		/// <summary>Returns the root node of the JSON document</summary>
		public static JPathExpression Root { get; } = new JPathSpecialToken('$');

		/// <summary>Returns the current node (relative to the current scope)</summary>
		public static JPathExpression Current { get; } = new JPathSpecialToken('@');

		/// <summary>Returns a <see cref="JPathExpression"/> that inverses the result of another expression</summary>
		[Pure]
		public static JPathExpression Not(JPathExpression node)
		{
			Contract.NotNull(node);
			return new JPathUnaryOperator(ExpressionType.Not, node);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that inverses the result of this expression</summary>
		[Pure]
		public JPathExpression Not() => new JPathUnaryOperator(ExpressionType.Not, this);

		/// <summary>Returns a <see cref="JPathExpression"/> that quotes another expression</summary>
		[Pure]
		public static JPathExpression Quote(JPathExpression node)
		{
			Contract.NotNull(node);
			return new JPathQuoteExpression(node);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that quotes this expression</summary>
		[Pure]
		public JPathExpression Quote() => new JPathQuoteExpression(this);

		/// <summary>Returns a <see cref="JPathExpression"/> that applies a filter on another expression</summary>
		[Pure]
		public static JPathExpression Matching(JPathExpression node, JPathExpression filter)
		{
			Contract.NotNull(node);
			Contract.NotNull(filter);
			return new JPathFilterExpression(node, filter);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that applies a filter on this expression</summary>
		[Pure]
		public JPathExpression Matching(JPathExpression filter) => Matching(this, filter);

		/// <summary>Returns a <see cref="JPathExpression"/> that extracts the field of an object with the specified name</summary>
		[Pure]
		public static JPathExpression Property(JPathExpression node, string name)
		{
			Contract.NotNull(node);
			Contract.NotNull(name);
			return new JPathObjectIndexer(node, name);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that extracts the field this object with the specified name</summary>
		[Pure]
		public JPathExpression Property(string name) => Property(this, name);

		/// <summary>Unwrap elle the items of an array</summary>
		public static JPathExpression All(JPathExpression node)
		{
			Contract.NotNull(node);
			return new JPathArrayRange(node, null, null);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that will return all the elements of this array</summary>
		public JPathExpression All() => new JPathArrayRange(this, null, null);

		/// <summary>Returns a <see cref="JPathExpression"/> that will return the element at the specified index in an array</summary>
		[Pure]
		public static JPathExpression At(JPathExpression node, int index)
		{
			Contract.NotNull(node);
			return new JPathArrayIndexer(node, index);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that will return the element at the specified index in this array</summary>
		[Pure]
		public JPathExpression At(int index) => At(this, index);

		/// <summary>Returns a <see cref="JPathExpression"/> that will apply a binary operator between an expression and a string constant</summary>
		[Pure]
		public static JPathExpression BinaryOperator(ExpressionType op, JPathExpression node, string literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(op, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that will apply a binary operator between an expression and a constant</summary>
		[Pure]
		public static JPathExpression BinaryOperator(ExpressionType op, JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(op, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that will apply a binary operator between two expressions</summary>
		[Pure]
		public static JPathExpression BinaryOperator(ExpressionType op, JPathExpression node, JPathExpression literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(op, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that will apply match if both expressions are true</summary>
		[Pure]
		public static JPathExpression AndAlso(JPathExpression left, JPathExpression right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);
			//note: to simplify parsing of parenthesis inside logical expressions, we unwrap the Quote(..) that contain them here!
			if (left is JPathQuoteExpression ql)
			{
				left = ql.Node;
			}
			if (right is JPathQuoteExpression qr)
			{
				right = qr.Node;
			}
			//REVIEW: this waste some memory, maybe we could simplify this! by only quoting if followed by [] ?
			return new JPathBinaryOperator(ExpressionType.AndAlso, left, right);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that will apply match if at least one expression is true</summary>
		[Pure]
		public static JPathExpression OrElse(JPathExpression left, JPathExpression right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);
			//note: to simplify parsing of parenthesis inside logical expressions, we unwrap the Quote(..) that contain them here!
			if (left is JPathQuoteExpression ql)
			{
				left = ql.Node;
			}
			if (right is JPathQuoteExpression qr)
			{
				right = qr.Node;
			}
			//REVIEW: this waste some memory, maybe we could simplify this! by only quoting if followed by [] ?
			return new JPathBinaryOperator(ExpressionType.OrElse, left, right);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is equal to a string constant</summary>
		[Pure]
		public static JPathExpression EqualTo(JPathExpression node, string literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.Equal, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is equal to a constant</summary>
		[Pure]
		public static JPathExpression EqualTo(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.Equal, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is equal to a string constant</summary>
		[Pure]
		public JPathExpression EqualTo(string literal) => EqualTo(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is equal to a constant</summary>
		[Pure]
		public JPathExpression EqualTo(JsonValue literal) => EqualTo(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is not equal to a string constant</summary>
		[Pure]
		public static JPathExpression NotEqualTo(JPathExpression node, string literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.NotEqual, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is not equal to a constant</summary>
		[Pure]
		public static JPathExpression NotEqualTo(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.NotEqual, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is not equal to a string constant</summary>
		[Pure]
		public JPathExpression NotEqualTo(string literal) => NotEqualTo(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is not equal to a constant</summary>
		[Pure]
		public JPathExpression NotEqualTo(JsonValue literal) => NotEqualTo(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is greater than a constant</summary>
		[Pure]
		public static JPathExpression GreaterThan(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.GreaterThan, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is greater than a constant</summary>
		[Pure]
		public JPathExpression GreaterThan(JsonValue literal) => GreaterThan(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is greater than or equal to a constant</summary>
		[Pure]
		public static JPathExpression GreaterThanOrEqual(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.GreaterThanOrEqual, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is greater than or equal to a constant</summary>
		[Pure]
		public JPathExpression GreaterThanOrEqualTo(JsonValue literal) => GreaterThanOrEqual(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is less than a constant</summary>
		[Pure]
		public static JPathExpression LessThan(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.LessThan, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is less than a constant</summary>
		[Pure]
		public JPathExpression LessThan(JsonValue literal) => LessThan(this, literal);

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if an expression is less than or equal to a constant</summary>
		[Pure]
		public static JPathExpression LessThanOrEqual(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.LessThanOrEqual, node, literal);
		}

		/// <summary>Returns a <see cref="JPathExpression"/> that tests if this expression is less than or equal to a constant</summary>
		[Pure]
		public JPathExpression LessThanOrEqualTo(JsonValue literal) => LessThanOrEqual(this, literal);

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj == this || (obj is JPathExpression expr && Equals(expr));

		/// <inheritdoc />
		public abstract override int GetHashCode();

		/// <inheritdoc />
		public abstract bool Equals(JPathExpression? other);
		
	}

	/// <summary>Expression that represents a special JPath Token (either <c>$</c> or <c>@</c>)</summary>
	public sealed class JPathSpecialToken : JPathExpression
	{

		/// <summary>Value of the token</summary>
		public char Token { get; }

		internal JPathSpecialToken(char token)
		{
			this.Token = token;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathSpecialToken tok && tok.Token == this.Token;

		/// <inheritdoc />
		public override int GetHashCode() => this.Token.GetHashCode();

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			yield return this.Token switch
			{
				'$' => root,
				'@' => current,
				_ => throw new InvalidOperationException()
			};
		}

		/// <inheritdoc />
		public override string ToString() => this.Token == '$' ? "$" : "@";

	}

	/// <summary>Expression that represents a field access in a JSON Object (ex: <c>foo['bar']</c>)</summary>
	public sealed class JPathObjectIndexer : JPathExpression
	{

		/// <summary>Expression for the parent object</summary>
		public JPathExpression Node { get; }

		/// <summary>Name of the field that is accessed</summary>
		public string Name { get; }

		internal JPathObjectIndexer(JPathExpression node, string name)
		{
			Contract.Debug.Requires(node != null && name != null);
			this.Node = node;
			this.Name = name;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathObjectIndexer idx && idx.Name == this.Name && idx.Node.Equals(this.Node);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Node.GetHashCode(), this.Node.GetHashCode());
		//TODO: cache!

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			// optimize fdor the most frequent cases
			if (this.Node is JPathSpecialToken tok)
			{
				return IterateSpecialNode(tok, this.Name, root, current);
			}
			else
			{
				return IterateNodes(this.Node, this.Name, root, current);
			}
		}

		private static IEnumerable<JsonValue> IterateNodes(JPathExpression node, string name, JsonValue root, JsonValue current)
		{ 

			foreach (var x in node.Iterate(root, current))
			{
				if (x is JsonArray map)
				{
					var y = ArrayPseudoProperty(map, name);
					if (y != null)
					{
						yield return y;
					}
				}
				else if (x is JsonObject obj)
				{
					if (obj.TryGetValue(name, out var y))
					{
						yield return y;
					}
				}
			}
		}

		private static JsonValue[] IterateSpecialNode(JPathSpecialToken node, string name, JsonValue root, JsonValue current)
		{
			var x = node.Token == '$' ? root : current;
			switch (x)
			{
				case JsonArray map:
				{
					var y = ArrayPseudoProperty(map, name);
					if (y != null)
					{
						return [ y ];
					}

					break;
				}
				case JsonObject obj when obj.TryGetValue(name, out var y):
				{
					return [ y ];
				}
			}

			return [ ];
		}

		/// <inheritdoc />
		public override string ToString() => $"{this.Node}['{this.Name}']";

	}

	/// <summary>Expression that represents the access to a part of a JSON Array</summary>
	[DebuggerDisplay("[{StartInclusive}:{EndExclusive}")]
	public sealed class JPathArrayRange : JPathExpression
	{

		/// <summary>Expression for the JSON array.</summary>
		public JPathExpression Node { get; }

		/// <summary>Index where to start reading from the array, or <c>null</c> to read from the first element.</summary>
		public int? StartInclusive { get; }

		/// <summary>Index where to stop reading, or <c>null</c> to read until the end.</summary>
		public int? EndExclusive { get; }

		internal JPathArrayRange(JPathExpression node, int? start, int? end)
		{
			Contract.Debug.Requires(node != null);
			this.Node = node;
			this.StartInclusive = start;
			this.EndExclusive = end;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathArrayRange range && range.StartInclusive == this.StartInclusive && range.EndExclusive == this.EndExclusive && range.Node.Equals(this.Node);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.StartInclusive ?? 0, this.EndExclusive ?? 0, this.Node.GetHashCode());

		/// <inheritdoc />
		public override string ToString()
		{
			if (this.StartInclusive == null && this.EndExclusive == null)
			{
				return $"{this.Node}.All()";
			}
			return $"{this.Node}.Range({this.StartInclusive}:{this.EndExclusive})";
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			if ((this.StartInclusive ?? 0) == 0 && this.EndExclusive == null)
			{ // FULL SCAN
				foreach (var x in this.Node.Iterate(root, current))
				{
					if (x is JsonArray arr)
					{
						foreach (var y in arr)
						{
							yield return y;
						}
					}
				}
			}
			else
			{
				throw new NotImplementedException();
			}
		}
	}

	/// <summary>Expression that represents a single element of a JSON Array</summary>
	public sealed class JPathArrayIndexer : JPathExpression
	{

		/// <summary>Expression for the array</summary>
		public JPathExpression Node { get; }

		/// <summary>Index of the element to read</summary>
		public int Index { get; }

		internal JPathArrayIndexer(JPathExpression node, int index)
		{
			Contract.Debug.Requires(node != null);
			this.Node = node;
			this.Index = index;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathArrayIndexer idx && idx.Index == this.Index && idx.Node.Equals(this.Node);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Index, this.Node.GetHashCode());

		/// <inheritdoc />
		public override string ToString() => $"{this.Node}[{this.Index}]";

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			foreach (var x in this.Node.Iterate(root, current))
			{
				var y = GetAtIndex(x, this.Index);
				if (y != null)
				{
					yield return y;
				}
			}
		}

	}

	/// <summary>Expression that outputs only the elements or a source expression that pass a filter</summary>
	public sealed class JPathFilterExpression : JPathExpression
	{

		/// <summary>Expression for the elements to filter</summary>
		public JPathExpression Node { get; }

		/// <summary>Expression for the filter to applies</summary>
		public JPathExpression Filter { get; }

		internal JPathFilterExpression(JPathExpression node, JPathExpression filter)
		{
			Contract.Debug.Requires(node != null && filter != null);
			this.Node = node;
			this.Filter = filter;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathFilterExpression filter && filter.Filter.Equals(this.Filter) && filter.Node.Equals(this.Node);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Node.GetHashCode(), this.Filter.GetHashCode());

		/// <inheritdoc />
		public override string ToString() => $"{this.Node}.Where(@ => {this.Filter})";

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			// for each element that passes here:
			// - if apply the filter on this element returns at least one result that is ~true, then the element is passed along
			// - if the filter does not return anything or only items that are ~false, then the element is dropped.
			//note: special case here: if item is an array, then we filter the ITEMS of the array!
			// => if caller wants to filter the array itself, it must be quoted first

			bool unrollArrays = true;
			var node = this.Node;
			if (node is JPathQuoteExpression quote)
			{
				unrollArrays = false;
				node = quote.Node;
			}

			foreach (var x in node.Iterate(root, current))
			{
				if (x.IsNullOrMissing()) continue; //REVIEW: is there any expression that would select 'null' ?

				if (unrollArrays && x is JsonArray array)
				{ // for arrays, we must apply the filter on the _elements_ and not the array itself!
					foreach (var item in array)
					{
						foreach (var y in this.Filter.Iterate(root, item))
						{
							if (IsTruthy(y))
							{ // found at least one match, we can emit this item
								yield return item;
								break;
							}
						}
					}
				}
				else
				{
					foreach (var y in this.Filter.Iterate(root, x))
					{
						if (IsTruthy(y))
						{ // found at least one match, we can emit this item
							yield return x;
							break;
						}
					}
				}
			}
		}

	}
	
	/// <summary>Expression that applies a binary operator on two expressions, or an expression and a constant</summary>
	public sealed class JPathBinaryOperator : JPathExpression
	{

		/// <summary>Type of operator applied</summary>
		public ExpressionType Operator { get; }

		/// <summary>Expression for the left side of the operator</summary>
		public JPathExpression Left { get; }

		/// <summary>Expression or constant value for the right side of the operator</summary>
		/// <remarks>Can be either a <see cref="string"/>, <see cref="JsonValue"/>, or <see cref="JPathExpression"/></remarks>
		public object? Right { get; }

		internal JPathBinaryOperator(ExpressionType op, JPathExpression left, object right)
		{
			Contract.Debug.Requires(left != null && right != null);
			Contract.Debug.Requires(right is string or JsonValue or JPathExpression);
			this.Operator = op;
			this.Left = left;
			this.Right = right;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathBinaryOperator op && op.Operator == this.Operator && Equals(op.Right, this.Right) && op.Left.Equals(this.Left);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(
			(int) this.Operator,
			this.Left.GetHashCode(),
			this.Right switch
			{
				string str => str.GetHashCode(),
				JsonValue val => val.GetHashCode(),
				JPathExpression expr => expr.GetHashCode(),
				_ => -1
			}
		);

		private IEnumerable<JsonValue> IterateStringLiteral(string literal, JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.Equal:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && s.Value == literal)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.NotEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && s.Value != literal)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.CompareOrdinal(s.Value, literal) < 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.CompareOrdinal(s.Value, literal) <= 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.CompareOrdinal(s.Value, literal) > 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.CompareOrdinal(s.Value, literal) >= 0)
						{
							return True();
						}
					}
					return None();
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
		}

		private IEnumerable<JsonValue> IterateJsonLiteral(JsonValue literal, JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.Equal:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (literal.Equals(x))
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.NotEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (!literal.Equals(x))
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) < 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) <= 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) > 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) >= 0)
						{
							return True();
						}
					}
					return None();
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
		}

		private static readonly JsonValue[] TrueArray = [ JsonBoolean.True];

		private static readonly JsonValue[] NoneArray = [];

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JsonValue[] True() => TrueArray;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JsonValue[] None() => NoneArray;

		private IEnumerable<JsonValue> IterateExpression(JPathExpression right, JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.AndAlso:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (IsTruthy(x))
						{
							foreach (var y in right.Iterate(root, current))
							{
								if (IsTruthy(y))
								{
									return True();
								}
							}
						}
					}
					return None();
				}
				case ExpressionType.OrElse:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (IsTruthy(x))
						{
							return True();
						}
					}
					foreach (var x in right.Iterate(root, current))
					{
						if (IsTruthy(x))
						{
							return True();
						}
					}
					return None();
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current) => this.Right switch
		{
			string str => IterateStringLiteral(str, root, current),
			JsonValue j => IterateJsonLiteral(j, root, current),
			JPathExpression expr => IterateExpression(expr, root, current),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		public override string ToString() => this.Right switch
		{
			string s => $"{this.Operator}({this.Left}, '{s}')",
			JsonValue j => $"{this.Operator}({this.Left}, {j:Q})",
			_ => $"{this.Operator}({this.Left}, {this.Right})"
		};

	}

	/// <summary>Expression that applies a unary operator onto another expression</summary>
	public sealed class JPathUnaryOperator : JPathExpression
	{

		/// <summary>Operator to apply to the expression</summary>
		public ExpressionType Operator { get; }

		/// <summary>Source expression</summary>
		public JPathExpression Node { get; }

		internal JPathUnaryOperator(ExpressionType op, JPathExpression node)
		{
			Contract.Debug.Requires(node != null);
			Contract.Debug.Requires(op == ExpressionType.Not); //TODO: add others here!
			this.Node = node;
			this.Operator = op;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? obj) => obj is JPathUnaryOperator op && op.Operator == this.Operator && op.Node.Equals(this.Node);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine((int) this.Operator, this.Node.GetHashCode());

		/// <inheritdoc />
		public override string ToString() => $"{this.Operator}({this.Node})";

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.Not:
				{
					// "not(...)" returns:
					// - false if there is AT LEAST one item that is ~true
					// - true if there are no items, or they were all ~false

					foreach (var x in this.Node.Iterate(root, current))
					{
						if (IsTruthy(x))
						{ // at least one true, so the whole expression is true, and not(true) => false
							yield break;
						}
					}
					// we did not see any "true" (either nothing, or all false) so the whole expression is false, and not(false) => true
					yield return JsonBoolean.True;
					break;
				}
				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

	}

	/// <summary>Expression that buffers the elements returned by another expression, into a single JSON Array</summary>
	public sealed class JPathQuoteExpression : JPathExpression
	{

		/// <summary>Source expression that returns one or more elements</summary>
		public JPathExpression Node { get; }

		internal JPathQuoteExpression(JPathExpression node)
		{
			this.Node = node;
		}

		/// <inheritdoc />
		public override bool Equals(JPathExpression? other) => other is JPathQuoteExpression quote && quote.Node.Equals(this.Node);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(0xC0FFEEE, this.Node.GetHashCode());

		/// <inheritdoc />
		public override string ToString() => $"Quote({this.Node})";

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			// we will simply buffer all incoming elements into a single new array, that we will emit as a single element
			JsonArray? arr = null;
			foreach (var x in this.Node.Iterate(root, current))
			{
				if (x != null!)
				{
					(arr ??= [ ]).Add(x);
				}
			}
			return arr == null ? [ ] : [ arr ];
		}

	}

}
