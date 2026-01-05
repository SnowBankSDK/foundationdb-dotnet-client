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

namespace SnowBank.Data.Json.Tests
{
	using System.Linq.Expressions;
	using SnowBank.Data.Json.JPath;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class JPathFacts : SimpleTest
	{

		#region Test Helpers...

		private static JsonObject GetSample()
		{
			return JsonObject.Parse(
				"""
				{
					"store":
					{
						"book":
						[
							{
								"category": "reference",
								"author": "Nigel Rees",
								"title": "Sayings of the Century",
								"price": 8.95
		
							},
							{
								"category": "fiction",
								"author": "Evelyn Waugh",
								"title": "Sword of Honour",
								"price": 12.99
		
							},
							{
								"category": "fiction",
								"author": "Herman Melville",
								"title": "Moby Dick",
								"isbn": "0-553-21311-3",
								"price": 8.99
		
							},
							{
								"category": "fiction",
								"author": "J. R. R. Tolkien",
								"title": "The Lord of the Rings",
								"isbn": "0-395-19395-8",
								"price": 22.99
		
							}
						],
						"bicycle":
						{
							"color": "red",
							"price": 19.95
						}
					}
				}
				""");
		}

		private static void CheckSingle(JsonValue node, string query, JsonValue expected, string? label = null)
		{
			Log($"? {query}");
			Log($"* {JPathQuery.ParseExpression(query)}");
			var res = node.Find(query);
			Assert.That(res, Is.Not.Null);
			if (res.Equals(JsonNull.Missing))
			{
				Log("> (no result)");
			}
			else
			{
				Log($"> {GetType(res)} {res:Q}");
			}
			Assert.That(res, Is.Not.Null, label);
			Assert.That(res, Is.EqualTo(expected), label);
		}

		private static string GetType(JsonValue? value)
		{
			if (value == null) return "---";
			return value.Type switch
			{
				JsonType.Null     => "nil",
				JsonType.Array    => "arr",
				JsonType.Object   => "obj",
				JsonType.Boolean  => "bol",
				JsonType.Number   => "num",
				JsonType.String   => "str",
				JsonType.DateTime => "dat",
				_                 => "???"
			};
		}

		private static void CheckMultiple(JsonValue node, string query, params JsonValue[] results)
		{
			Log($"? {query}");
			Log($"* {JPathQuery.ParseExpression(query)}");
			Assert.That(node, Is.Not.Null);
			var res = node.FindAll(query);
			Assert.That(res, Is.Not.Null);
			if (res.Count == 0)
			{
				Log("> (no results)");
			}
			else if (res.Count == 1)
			{
				Log("> 1 result");
			}
			else
			{
				Log($"> {res.Count} result(s)");
			}

			for (int i = 0; i < res.Count; i++)
			{
				Log($"- [{i}] {GetType(res[i])} {res[i]:Q}");
			}

			Assert.That(res, Is.EqualTo(results));
		}

		private static void CheckEqual(JPathExpression actual, JPathExpression expected, string? label = null)
		{
			if (!actual.Equals(expected))
			{
				Log($"> Actual  : {actual}");
				Log($"> Expected: {expected}");
				Assert.That(actual, Is.EqualTo(expected), label);
			}
		}

		private static void CheckEqual(JPathQuery query, JPathExpression expected, string? label = null)
		{
			if (!query.Expression.Equals(expected))
			{
				Log($"FAILED to parse query \"{query.Text}\"");
				Log($"> Actual  : {query.Expression}");
				Log($"> Expected: {expected}");
				Assert.That(query.Expression, Is.EqualTo(expected), label);
			}
		}

		private static void CheckNotEqual(JPathExpression actual, JPathExpression expected, string? label = null)
		{
			if (actual.Equals(expected))
			{
				Log($"Actual  : {actual}");
				Assert.That(actual, Is.Not.EqualTo(expected), label);
			}
		}

		#endregion

		[Test]
		public void Test_Expression_Factories()
		{
			using (Assert.EnterMultipleScope())
			{
				var expr = JPathExpression.Root;
				Assert.That(expr, Is.InstanceOf<JPathSpecialToken>());
				Assert.That(((JPathSpecialToken) expr).Token, Is.EqualTo('$'));
				Assert.That(expr.ToString(), Is.EqualTo("$"));
			}
			using (Assert.EnterMultipleScope())
			{
				var expr = JPathExpression.Current;
				Assert.That(expr, Is.InstanceOf<JPathSpecialToken>());
				Assert.That(((JPathSpecialToken) expr).Token, Is.EqualTo('@'));
				Assert.That(expr.ToString(), Is.EqualTo("@"));
			}
			using (Assert.EnterMultipleScope())
			{
				var expr = JPathExpression.Property(JPathExpression.Current, "hello");
				Assert.That(expr, Is.InstanceOf<JPathObjectIndexer>());
				Assert.That(((JPathObjectIndexer) expr).Node, Is.SameAs(JPathExpression.Current));
				Assert.That(((JPathObjectIndexer) expr).Name, Is.EqualTo("hello"));
				Assert.That(expr.ToString(), Is.EqualTo("@['hello']"));
			}
			using (Assert.EnterMultipleScope())
			{
				var expr = JPathExpression.At(JPathExpression.Current, 42);
				Assert.That(expr, Is.InstanceOf<JPathArrayIndexer>());
				Assert.That(((JPathArrayIndexer) expr).Node, Is.SameAs(JPathExpression.Current));
				Assert.That(((JPathArrayIndexer) expr).Index, Is.EqualTo(42));
				Assert.That(expr.ToString(), Is.EqualTo("@[42]"));
			}
			using (Assert.EnterMultipleScope())
			{
				var expr = JPathExpression.EqualTo(JPathExpression.Current, "Hello World");
				Assert.That(expr, Is.InstanceOf<JPathBinaryOperator>());
				Assert.That(((JPathBinaryOperator) expr).Operator, Is.EqualTo(ExpressionType.Equal));
				Assert.That(((JPathBinaryOperator) expr).Left, Is.SameAs(JPathExpression.Current));
				Assert.That(((JPathBinaryOperator) expr).Right, Is.EqualTo("Hello World"));
				Assert.That(expr.ToString(), Is.EqualTo("Equal(@, 'Hello World')"));
			}
			using (Assert.EnterMultipleScope())
			{
				var expr = JPathExpression.EqualTo(JPathExpression.Current, 42);
				Assert.That(expr, Is.InstanceOf<JPathBinaryOperator>());
				Assert.That(((JPathBinaryOperator) expr).Operator, Is.EqualTo(ExpressionType.Equal));
				Assert.That(((JPathBinaryOperator) expr).Left, Is.SameAs(JPathExpression.Current));
				Assert.That(((JPathBinaryOperator) expr).Right, Is.EqualTo(JsonNumber.Return(42)));
				Assert.That(expr.ToString(), Is.EqualTo("Equal(@, 42)"));
			}
		}

		[Test]
		public void Test_Expression_Equality()
		{
			CheckEqual(JPathExpression.Current, JPathExpression.Current);
			CheckNotEqual(JPathExpression.Current, JPathExpression.Root);
			CheckEqual(JPathExpression.Root, JPathExpression.Root);
			CheckNotEqual(JPathExpression.Root, JPathExpression.Current);

			CheckEqual(JPathExpression.Property(JPathExpression.Root, "foo"), JPathExpression.Root.Property("foo"));
			CheckNotEqual(JPathExpression.Property(JPathExpression.Root, "foo"), JPathExpression.Root.Property("bar"));
			CheckNotEqual(JPathExpression.Property(JPathExpression.Root, "foo"), JPathExpression.Current.Property("foo"));

			CheckEqual(JPathExpression.At(JPathExpression.Root, 42), JPathExpression.Root.At(42));
			CheckNotEqual(JPathExpression.At(JPathExpression.Root, 42), JPathExpression.Root.At(1));
			CheckNotEqual(JPathExpression.At(JPathExpression.Root, 42), JPathExpression.Current.At(42));

			CheckEqual(JPathExpression.EqualTo(JPathExpression.Root, 42), JPathExpression.Root.EqualTo(42));
			CheckEqual(JPathExpression.EqualTo(JPathExpression.Root, "hello"), JPathExpression.Root.EqualTo("hello"));
			CheckEqual(JPathExpression.NotEqualTo(JPathExpression.Root, "hello"), JPathExpression.Root.NotEqualTo("hello"));
			CheckNotEqual(JPathExpression.EqualTo(JPathExpression.Root, 42), JPathExpression.Root.EqualTo(123));
			CheckNotEqual(JPathExpression.EqualTo(JPathExpression.Root, 42), JPathExpression.Current.EqualTo(42));
			CheckNotEqual(JPathExpression.NotEqualTo(JPathExpression.Root, 42), JPathExpression.Current.NotEqualTo(42));
			CheckNotEqual(JPathExpression.LessThan(JPathExpression.Root, 42), JPathExpression.Current.LessThanOrEqualTo(42));

			CheckEqual(JPathExpression.Matching(JPathExpression.Root, JPathExpression.EqualTo(JPathExpression.Current, 42)), JPathExpression.Root.Matching(JPathExpression.Current.EqualTo(42)));
			CheckNotEqual(JPathExpression.Matching(JPathExpression.Root, JPathExpression.EqualTo(JPathExpression.Current, 42)), JPathExpression.Root.Matching(JPathExpression.Current.LessThan(42)));
		}

		[Test]
		public void Test_ParseExpression()
		{
			CheckEqual(
				JPathQuery.Parse("$"),
				JPathExpression.Root
			);
			CheckEqual(
				JPathQuery.Parse("@"),
				JPathExpression.Current
			);
			CheckEqual(
				JPathQuery.Parse("$.foo"),
				JPathExpression.Root.Property("foo")
			);
			CheckEqual(
				JPathQuery.Parse("@.foo"),
				JPathExpression.Current.Property("foo")
			);
			CheckEqual(
				JPathQuery.Parse("foo"),
				JPathExpression.Root.Property("foo")
			);
			CheckEqual(
				JPathQuery.Parse("foo.bar"),
				JPathExpression.Root.Property("foo").Property("bar")
			);
			CheckEqual(
				JPathQuery.Parse("$foo"),
				JPathExpression.Root.Property("$foo")
			);
			CheckEqual(
				JPathQuery.Parse("@foo"),
				JPathExpression.Root.Property("@foo")
			);
			CheckEqual(
				JPathQuery.Parse("$foo.@bar"),
				JPathExpression.Root.Property("$foo").Property("@bar")
			);

			// Indexing...

			CheckEqual(
				JPathQuery.Parse("foo[42]"),
				JPathExpression.Root.Property("foo").At(42)
			);
			CheckEqual(
				JPathQuery.Parse("foo[42][0]"),
				JPathExpression.Root.Property("foo").At(42).At(0)
			);
			CheckEqual(
				JPathQuery.Parse("foo[42].bar"),
				JPathExpression.Root.Property("foo").At(42).Property("bar")
			);

			// Filtering...

			CheckEqual(
				JPathQuery.Parse("foo[bar]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar"))
			);
			CheckEqual(
				JPathQuery.Parse("foo[not(bar)]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").Not())
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar=='hello']"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").EqualTo("hello"))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar == 'hello']"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").EqualTo("hello"))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar>123]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").GreaterThan(123))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar>123.4]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").GreaterThan(123.4))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar>=123]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").GreaterThanOrEqualTo(123))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar<123]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").LessThan(123))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar<=123]"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar").LessThanOrEqualTo(123))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar=='hello' && baz>123]"), // && has precedence other comparisons, so (a == b) && (c > d)
				JPathExpression.Root.Property("foo").Matching(JPathExpression.AndAlso(JPathExpression.Current.Property("bar").EqualTo("hello"), JPathExpression.Current.Property("baz").GreaterThan(123)))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar=='hello' && baz>123.4]"), // && has precedence other comparisons, so (a == b) && (c > d)
				JPathExpression.Root.Property("foo").Matching(JPathExpression.AndAlso(JPathExpression.Current.Property("bar").EqualTo("hello"), JPathExpression.Current.Property("baz").GreaterThan(123.4)))
			);
			CheckEqual(
				JPathQuery.Parse("foo[bar=='hello' || baz>123]"), // || has precedence other comparisons, so (a == b) || (c > d)
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.Current.Property("bar").EqualTo("hello"),
						JPathExpression.Current.Property("baz").GreaterThan(123)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a && b && c]"), // associativity from the left to right, so (a && b) && c
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.AndAlso(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a"),
							JPathExpression.Current.Property("b")
						),
						JPathExpression.Current.Property("c")
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a==1 && b==2 && c==3]"), // associativity from the left to right, so (a && b) && c
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.AndAlso(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a").EqualTo(1),
							JPathExpression.Current.Property("b").EqualTo(2)
						),
						JPathExpression.Current.Property("c").EqualTo(3)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a || b || c]"), // associativity from the left to right, so (a || b) || c
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.OrElse(
							JPathExpression.Current.Property("a"),
							JPathExpression.Current.Property("b")
						),
						JPathExpression.Current.Property("c")
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a==1 || b==2 || c==3]"), // associativity from the left to right, so (a || b) || c
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.OrElse(
							JPathExpression.Current.Property("a").EqualTo(1),
							JPathExpression.Current.Property("b").EqualTo(2)
						),
						JPathExpression.Current.Property("c").EqualTo(3)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a && b || c]"),
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a"),
							JPathExpression.Current.Property("b")
						),
						JPathExpression.Current.Property("c")
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a==1 && b==2 || c==3]"),
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a").EqualTo(1),
							JPathExpression.Current.Property("b").EqualTo(2)
						),
						JPathExpression.Current.Property("c").EqualTo(3)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a || b && c]"), // && has precedence over ||, so a || (b && c)
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.Current.Property("a"),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("b"),
							JPathExpression.Current.Property("c")
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a==1 || b==2 && c==3]"), // && has precedence over ||, so a || (b && c)
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.Current.Property("a").EqualTo(1),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("b").EqualTo(2),
							JPathExpression.Current.Property("c").EqualTo(3)
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a && b || c && d]"), // && has precedence over ||, so must be seen as "(a & b) || (c && d)"
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a"),
							JPathExpression.Current.Property("b")
						),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("c"),
							JPathExpression.Current.Property("d")
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a==1 && b==2 || c==3 && d==4]"), // && has precedence over ||, so must be seen as "(a & b) || (c && d)"
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a").EqualTo(1),
							JPathExpression.Current.Property("b").EqualTo(2)
						),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("c").EqualTo(3),
							JPathExpression.Current.Property("d").EqualTo(4)
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[a && b || c && d || e && f]"),
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.OrElse(
							JPathExpression.AndAlso(
								JPathExpression.Current.Property("a"),
								JPathExpression.Current.Property("b")
							),
							JPathExpression.AndAlso(
								JPathExpression.Current.Property("c"),
								JPathExpression.Current.Property("d")
							)
						),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("e"),
							JPathExpression.Current.Property("f")
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[(a && b) || (c && d)]"), // parenthesis here are redundant
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a"),
							JPathExpression.Current.Property("b")
						),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("c"),
							JPathExpression.Current.Property("d")
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[(a==1 && b==2) || (c==3 && d==4)]"), // parenthesis here are redundant
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.OrElse(
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("a").EqualTo(1),
							JPathExpression.Current.Property("b").EqualTo(2)
						),
						JPathExpression.AndAlso(
							JPathExpression.Current.Property("c").EqualTo(3),
							JPathExpression.Current.Property("d").EqualTo(4)
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[(a || b) && (c || d)]"), // parenthesis here are NOT redundant!
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.AndAlso(
						JPathExpression.OrElse(
							JPathExpression.Current.Property("a"),
							JPathExpression.Current.Property("b")
						),
						JPathExpression.OrElse(
							JPathExpression.Current.Property("c"),
							JPathExpression.Current.Property("d")
						)
					)
				)
			);
			CheckEqual(
				JPathQuery.Parse("foo[(a==1 || b==2) && (c==3 || d==4)]"), // parenthesis here are NOT redundant!
				JPathExpression.Root.Property("foo").Matching(
					JPathExpression.AndAlso(
						JPathExpression.OrElse(
							JPathExpression.Current.Property("a").EqualTo(1),
							JPathExpression.Current.Property("b").EqualTo(2)
						),
						JPathExpression.OrElse(
							JPathExpression.Current.Property("c").EqualTo(3),
							JPathExpression.Current.Property("d").EqualTo(4)
						)
					)
				)
			);

			// Quoting (buferring N items into a single array that contains all the previous results)

			CheckEqual(
				JPathQuery.Parse("(foo)[1]"),
				JPathExpression.Root.Property("foo").Quote().At(1)
			);
			CheckEqual(
				JPathQuery.Parse("(foo[bar])"),
				JPathExpression.Root.Property("foo").Matching(JPathExpression.Current.Property("bar")).Quote()
			);

			// Funny cases

			CheckEqual(
				JPathQuery.Parse("$[][@<6]"),
				JPathExpression.Root.All().Matching(JPathExpression.Current.LessThan(6))
			);

			CheckEqual(
				JPathQuery.Parse("$[@[@<6]]"),
				JPathExpression.Root.Matching(JPathExpression.Current.Matching(JPathExpression.Current.LessThan(6)))
			);

		}

		[Test]
		public void Test_ParseExpression_InvalidSyntax()
		{
			CheckFailure(" ");
			CheckFailure("$$");
			CheckFailure("[");
			CheckFailure("[]]");
			CheckFailure("(");
			CheckFailure("()");
			CheckFailure("())");
			CheckFailure("(]");
			CheckFailure("[)");

			CheckFailure("foo.");
			CheckFailure("foo..");
			CheckFailure("foo..bar");
			CheckFailure("foo.$");
			CheckFailure("foo.$.bar");

			CheckFailure("foo[bar");
			CheckFailure("foo[bar[");
			CheckFailure("foo[bar[]");
			CheckFailure("foo[bar][");

			CheckFailure("(foo");
			CheckFailure("(foo))");

			CheckFailure("1a");
			CheckFailure("1+");
			CheckFailure("1-");
			CheckFailure("a+");
			CheckFailure("foo.1a");
			CheckFailure("foo.1+");
			CheckFailure("foo.1-");
			CheckFailure("foo.a+");
			CheckFailure("foo[1a]");
			CheckFailure("foo[1+]");
			CheckFailure("foo[1-]");
			CheckFailure("foo[a+]");



			static void CheckFailure(string literal)
			{
				Log($"Parsing: `{literal}`");

				try
				{
					var expr = JPathQuery.Parse(literal);
					Assert.That(expr, Is.Null, $"Parsing of malformed query `{literal}` should have failed, but it returned expression {expr.GetType().Name} instead.");
					Assert.Fail($"Parsing of malformed query `{literal}` should have failed, but it returned null instead");
				}
				catch (FormatException e)
				{ // PASS
					Log($"> {e.Message}");
				}
				catch (AssertionException)
				{ // FAIL
					throw;
				}
				catch (Exception e)
				{ // FAIL
					Log($"!! {e.GetType().Name}: {e.Message}");
					Assert.That(e, Is.InstanceOf<FormatException>(), $"Parsing of malformed query `{literal}` should have failed");
				}
			}
		}

		[Test]
		public void Test_Simple_Query_Single()
		{
			var obj = GetSample();

			CheckSingle(obj, "$", obj);
			CheckSingle(obj, "$.store", obj["store"]);
			CheckSingle(obj, "store", obj["store"]);
			CheckSingle(obj, "store.bicycle", obj["store"]["bicycle"]);
			CheckSingle(obj, "store.bicycle.color", "red");
			CheckSingle(obj, "store.bicycle.price", 19.95);
			CheckSingle(obj, "store.book", obj["store"]["book"]); // should return the array itself
			CheckSingle(obj, "store.book[0]", obj["store"]["book"][0]); // should return the first item of the array
			CheckSingle(obj, "store.book[0].title", "Sayings of the Century"); // should return the tite of the first item of the array
			CheckSingle(obj, "store.book[1].title", "Sword of Honour");
			CheckSingle(obj, "store.book[-1].title", "The Lord of the Rings");
			CheckSingle(obj, "store.book[^1].title", "The Lord of the Rings");
			CheckSingle(obj, "store.book[2].price", 8.99);
			CheckSingle(obj, "store.book[-1].title", "The Lord of the Rings");
			CheckSingle(obj, "store.book[^1].title", "The Lord of the Rings");

			// "typos"
			CheckSingle(obj, "ztore", JsonNull.Missing);
			CheckSingle(obj, "Store", JsonNull.Missing);
			CheckSingle(obj, "store.not_found", JsonNull.Missing);
			CheckSingle(obj, "store.Book", JsonNull.Missing); // case sensitive!

			// array indexing
			CheckSingle(obj, "store.book[42]", JsonNull.Missing);
			CheckSingle(obj, "store.book[-42]", JsonNull.Missing);
			CheckSingle(obj, "store.book[^42]", JsonNull.Missing);
			CheckSingle(obj, "store.book[0].not_found", JsonNull.Missing);
			CheckSingle(obj, "store.book[0].not_found", JsonNull.Missing);

			CheckSingle(obj, "store.book.title", JsonNull.Missing); // "book" is an array that does not have a ".title". (caller probably meant book[].title)
			CheckSingle(obj, "store.book[].title", "Sayings of the Century"); // value of title of first item in array that has one
			CheckSingle(obj, "store.book[].isbn", "0-553-21311-3");

			CheckSingle(obj, "store.book[isbn].title", "Moby Dick");
			CheckSingle(obj, "store.book[@.isbn].title", "Moby Dick");

			CheckSingle(obj, "store.book[isbn=='0-395-19395-8'].title", "The Lord of the Rings");
			CheckSingle(obj, "store.book[isbn == '0-553-21311-3'].title", "Moby Dick"); // should ignore whitespaces in sub-expression
			CheckSingle(obj, "store.book[price>10].title", "Sword of Honour");
			CheckSingle(obj, "store.book[price < 10].title", "Sayings of the Century");
			CheckSingle(obj, "store.book[price < 10 && category == 'fiction'].title", "Moby Dick");

			CheckSingle(obj, "(store.book)[$length > 3]", obj["store"]["book"]); // should return the array itself (it has length 4)
			CheckSingle(obj, "(store.book)[$length < 3]", JsonNull.Missing); // should return nothing
			CheckSingle(obj, "(store.book[isbn])[1]", obj["store"]["book"][3]); // should return the second book for which it is true that it has an isbn field
		}

		[Test]
		public void Test_Simple_Query_Multiple()
		{
			var obj = GetSample();

			CheckMultiple(obj, "$", obj);

			CheckMultiple(obj, "store", obj["store"]);

			CheckMultiple(obj, "store.bicycle", obj["store"]["bicycle"]);
			CheckMultiple(obj, "store.bicycle.color", "red");

			CheckMultiple(obj, "store.book", obj["store"]["book"]); // should return the array itself!

			CheckMultiple(obj, "store.book[0]", obj["store"]["book"][0]); // should only return the first item
			CheckMultiple(obj, "store.book[1]", obj["store"]["book"][1]); // should only return the second item
			CheckMultiple(obj, "store.book[-1]", obj["store"]["book"][3]); // should only return the last item
			CheckMultiple(obj, "store.book[^1]", obj["store"]["book"][3]); // should only return the last item

			CheckMultiple(obj, "store.book[]", obj["store"]["book"].Required<JsonValue[]>()); // should return the items of the array!
			CheckMultiple(obj, "store.book[].title", "Sayings of the Century", "Sword of Honour", "Moby Dick", "The Lord of the Rings"); // should flatten array of a single JsonArray into an array of all items
			CheckMultiple(obj, "store.book[isbn]", obj["store"]["book"][2], obj["store"]["book"][3]); // should return only items of the array that have an isbn
			CheckMultiple(obj, "store.book[isbn].title", "Moby Dick", "The Lord of the Rings"); // should all the titles of the books that have an isbn

			CheckMultiple(obj, "store.book[price<10].title", "Sayings of the Century", "Moby Dick"); // should all the titles of the books that have an isbn

			CheckMultiple(obj, "store.book[not(isbn)]", obj["store"]["book"][0], obj["store"]["book"][1]); // should return only items of the array that have an isbn

			var arr = JsonArray.Parse("[ [1, 2, 3], [4, 5, 6], [7, 8, 9]]");
			CheckMultiple(arr, "$", arr); // return the top array
			CheckMultiple(arr, "$[]", arr[0], arr[1], arr[2]); // return all arrays
			CheckMultiple(arr, "$[0]", arr[0]); // return the first array
			CheckMultiple(arr, "$[0][]", 1, 2, 3); // unroll only the first array
			CheckMultiple(arr, "$[][0]", 1, 4, 7); // return the first element of each array
			CheckMultiple(arr, "$[][]", 1, 2, 3, 4, 5, 6, 7, 8, 9); // unroll all arrays
			CheckMultiple(arr, "$[][@<6]", 1, 2, 3, 4, 5); // unroll all elements and return only numbers that are < 6
			CheckMultiple(arr, "$[][@>5]", 6, 7, 8, 9); // unroll all elements and return only numbers that are > 5
			CheckMultiple(arr, "$[@[@<6]]", arr[0], arr[1]); // only returns the arrays for which it is true that at least one of their element is < 6
			CheckMultiple(arr, "$[@[@>5]]", arr[1], arr[2]); // only returns the arrays for which it is true that at least one of their element is > 5
		}

	}

}
