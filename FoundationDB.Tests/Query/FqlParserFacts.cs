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

// ReSharper disable StringLiteralTypo
#if NET8_0_OR_GREATER

namespace FoundationDB.Client.Tests
{
	using System;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FqlParserFacts : FdbSimpleTest
	{

		protected static void Explain(IFqlQuery? query)
		{
			if (query == null)
			{
				Log("# Query: <null>");
			}
			else
			{
				Log(query.Explain(prefix: "# ").Trim());
			}
		}

		[Test]
		public void Test_Fql_Parse_Path()
		{
			// `/`
			{
				var q = FqlQueryParser.Parse("/");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root()));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/user`
			{
				var q = FqlQueryParser.Parse("/user");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("user")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/foo/bar/baz`
			{
				var q = FqlQueryParser.Parse("/foo/bar/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Name("bar").Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/foo/"bar"/baz`
			{
				var q = FqlQueryParser.Parse("/foo/\"bar\"/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Name("bar").Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/"foo\"bar\"baz"`
			{
				var q = FqlQueryParser.Parse("/\"foo\\\"bar\\\"baz\"");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo\"bar\"baz")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `./foo/bar/baz`
			{
				var q = FqlQueryParser.Parse("./foo/bar/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Name("foo").Name("bar").Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}
			// `../bar/baz`
			{
				var q = FqlQueryParser.Parse("../bar/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Parent().Name("bar").Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}
			// `/foo/bar/../baz`
			{
				var q = FqlQueryParser.Parse("/foo/bar/../baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Name("bar").Parent().Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}
		}

		[Test]
		public void Test_Fql_Parse_Path_With_Any()
		{
			// `/<>`
			{
				var q = FqlQueryParser.Parse("/<>");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Any()));
				Assert.That(q.Tuple, Is.Null);
			}
			// `/foo/<>`
			{
				var q = FqlQueryParser.Parse("/foo/<>");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Any()));
				Assert.That(q.Tuple, Is.Null);
			}
			// `/<>/bar`
			{
				var q = FqlQueryParser.Parse("/<>/bar");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Any().Name("bar")));
				Assert.That(q.Tuple, Is.Null);
			}
			// `/foo/<>/baz`
			{
				var q = FqlQueryParser.Parse("/foo/<>/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Any().Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}
			// `/<>/<>/baz`
			{
				var q = FqlQueryParser.Parse("/<>/<>/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Any().Any().Name("baz")));
				Assert.That(q.Tuple, Is.Null);
			}

		}

		[Test]
		public void Test_Fql_Parse_Hybrid()
		{
			// `/user(...)`
			{
				var q = FqlQueryParser.Parse("/user(...)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("user")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().MaybeMore()));
			}

			// `/user(<int>,"Goodwin",...)`
			{
				var q = FqlQueryParser.Parse("/user(<int>,\"Goodwin\",...)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("user")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Var(FqlVariableTypes.Int).String("Goodwin").MaybeMore()));
			}

			// `./records(0,<vstamp>,1,<int>,...)`
			{
				var q = FqlQueryParser.Parse("./records(0,<vstamp>,1,<int>,...)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Name("records")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Integer(0).Var(FqlVariableTypes.VStamp).Integer(1).Var(FqlVariableTypes.Int).MaybeMore()));
			}
		}

		[Test]
		public void Test_Fql_Parse_Tuple()
		{
			// `(...)`
			{
				var q = FqlQueryParser.Parse("(...)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().MaybeMore()));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(...)"));
			}

			// `(0xFF, "thing", ...)`
			{
				var q = FqlQueryParser.Parse("(0xFF, \"thing\", ...)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Bytes(Slice.FromByte(255)).String("thing").MaybeMore()));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(0xff,\"thing\",...)"));
			}

			{
				// `("one", 2, 0x03, ( "subtuple" ), 5825d3f8-de5b-40c6-ac32-47ea8b98f7b4)`
				var q = FqlQueryParser.Parse("(\"one\", 2, 0x03, ( \"subtuple\" ), 5825d3f8-de5b-40c6-ac32-47ea8b98f7b4)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("one").Integer(2).Bytes(Slice.FromByte(3)).Tuple(FqlTupleExpression.Create().String("subtuple")).Uuid(Uuid128.Parse("5825d3f8-de5b-40c6-ac32-47ea8b98f7b4"))));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(\"one\",2,0x03,(\"subtuple\"),5825d3f8-de5b-40c6-ac32-47ea8b98f7b4)"));
			}

			// 

			//{ // `/my/dir("this", 0)=0xabcf03`
			//	var q = FqlQueryParser.Parse("/my/dir(\"this\", 0)=0xabcf03");
			//	Assert.That(q, Is.Not.Null);
			//	Explain(q);
			//}

			// 
			// /my/dir(22.3, -8)=("another", "tuple")
			// /some/where("home", "town", 88.3)=clear

			// /my/dir("hello", "world")=42
			// /my/dir("hello", "world")=clear
			// /my/dir(99.8, 7dfb10d1-2493-4fb5-928e-889fdc6a7136)=<int|string>
			// /people(3392, <str|int>, <>)=(<uint>, ...)
			// /root/<>/items/<>

		}

		[Test]
		public void Test_Fql_Parse_Tuple_With_Variables()
		{
			// `(<>)`
			{
				var q = FqlQueryParser.Parse("(<>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarAny()));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(<>)"));
			}
			// `(<nil>)`
			{
				var q = FqlQueryParser.Parse("(<nil>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarNil()));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(<nil>)"));
			}
			// `(<int>)`
			{
				var q = FqlQueryParser.Parse("(<int>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarInteger()));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(<int>)"));
			}
			// `(<str>)`
			{
				var q = FqlQueryParser.Parse("(<str>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarString()));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(<str>)"));
			}

			// `(<>, <nil>, <bool>, <int>, <num>, <str>, <bytes>, <uuid>, <tup>, <vstamp>)`
			{
				var q = FqlQueryParser.Parse("(<>, <nil>, <bool>, <int>, <num>, <str>, <bytes>, <uuid>, <tup>, <vstamp>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create()
					.VarAny()
					.VarNil()
					.VarBoolean()
					.VarInteger()
					.VarNumber()
					.VarString()
					.VarBytes()
					.VarUuid()
					.VarTuple()
					.VarVStamp()
				));
				Assert.That(q.Tuple!.ToString(), Is.EqualTo("(<>,<nil>,<bool>,<int>,<num>,<str>,<bytes>,<uuid>,<tup>,<vstamp>)"));
			}
		}

		[Test]
		public void Test_Fql_Parse_Named_Variables()
		{
			// `/people(<id:uint>,<firstName:str>,<lastName:str>,<age:int>)`
			{
				var q = FqlQueryParser.Parse("/people(<id:uuid>,<firstName:str>,<lastName:str>,<age:int>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("people")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarUuid("id").VarString("firstName").VarString("lastName").VarInteger("age")));
			}
		}

		[Test]
		public void Test_Fql_ParseNext_With_Extra_Tokens()
		{
			// `/ hello`
			{
				var q = FqlQueryParser.ParseNext("/ hello", FqlParsingOptions.Default, out var rest).Resolve();
				Explain(q);
				Log($"# rest : [{rest.ToString()}]");
				Assert.That(q, Is.Not.Null);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root()));
				Assert.That(q.Tuple, Is.Null);
				Assert.That(rest.ToString(), Is.EqualTo(" hello"));
				Log();
			}

			// `/users hello`
			{
				var q = FqlQueryParser.ParseNext("/users\thello", FqlParsingOptions.Default, out var rest).Resolve();
				Explain(q);
				Log($"# rest : [{rest.ToString()}]");
				Assert.That(q, Is.Not.Null);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("users")));
				Assert.That(q.Tuple, Is.Null);
				Assert.That(rest.ToString(), Is.EqualTo("\thello"));
				Log();
			}

			// `/foo/bar hello`
			{
				var q = FqlQueryParser.ParseNext("/foo/bar hello", FqlParsingOptions.Default, out var rest).Resolve();
				Explain(q);
				Log($"# rest : [{rest.ToString()}]");
				Assert.That(q, Is.Not.Null);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Name("bar")));
				Assert.That(q.Tuple, Is.Null);
				Assert.That(rest.ToString(), Is.EqualTo(" hello"));
				Log();
			}

			// `/"foo bar" hello`
			{
				var q = FqlQueryParser.ParseNext("/\"foo bar\" hello", FqlParsingOptions.Default, out var rest).Resolve();
				Explain(q);
				Log($"# rest : [{rest.ToString()}]");
				Assert.That(q, Is.Not.Null);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo bar")));
				Assert.That(q.Tuple, Is.Null);
				Assert.That(rest.ToString(), Is.EqualTo(" hello"));
				Log();
			}

			// `/users(<int>, 123) hello`
			{
				var q = FqlQueryParser.ParseNext("/users(<int>, 123)\r\nhello", FqlParsingOptions.Default, out var rest).Resolve();
				Explain(q);
				Log($"# rest : [{rest.ToString()}]");
				Assert.That(q, Is.Not.Null);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("users")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarInteger().Integer(123)));
				Assert.That(rest.ToString(), Is.EqualTo("\r\nhello"));
				Log();
			}

			// `/users (<int>, 123) hello`
			{
				var q = FqlQueryParser.ParseNext("/users (<int>, 123)", FqlParsingOptions.Default, out var rest).Resolve();
				Explain(q);
				Log($"# rest : [{rest.ToString()}]");
				Assert.That(q, Is.Not.Null);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("users")));
				Assert.That(q.Tuple, Is.Null);
				Assert.That(rest.ToString(), Is.EqualTo(" (<int>, 123)"));
				Log();
			}

		}

		[Test]
		public void Test_Fql_Parse_With_FqlParsingOptions_PathOnly()
		{
			// the option FqlParsingOptions.PathOnly should reject any query with a tuple
			// - "/foo/bar" is allowed
			// - "/foo/bar(1,...)" or "(1,...)" are not allowed

			// query with only a path should return a valid expression
			{
				var q = FqlQueryParser.Parse("/foo/bar", FqlParsingOptions.PathOnly);
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Name("bar")));
				Assert.That(q.Tuple, Is.Null);
			}

			// queries that include both a path and tuple should throw
			{
				// it should be accepted with the default options
				Assume.That(() => FqlQueryParser.Parse("/foo/bar(1,...)"), Throws.Nothing);
				// but rejected if PathOnly is specified
				Assert.That(() => FqlQueryParser.Parse("/foo/bar(1,...)", FqlParsingOptions.PathOnly), Throws.InstanceOf<FormatException>());
			}

			// queries that include only tuple should throw as well
			{
				// it should be accepted with the default options
				Assume.That(() => FqlQueryParser.Parse("(1,...)"), Throws.Nothing);
				// but rejected if PathOnly is specified
				Assert.That(() => FqlQueryParser.Parse("(1,...)", FqlParsingOptions.PathOnly), Throws.InstanceOf<FormatException>());
			}

		}

		[Test]
		public void Test_Fql_Parse_With_FqlParsingOptions_TupleOnly()
		{
			// the option FqlParsingOptions.TupleOnly should reject any query with a path
			// - "(1,...)" is allowed
			// - "/foo/bar", "/foo/bar(1,...)" or "./(1,...)" are not allowed

			// query with only a tuple should return a valid expression
			{
				var q = FqlQueryParser.Parse("(1,...)", FqlParsingOptions.TupleOnly);
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Integer(1).MaybeMore()));
			}

			// queries that include only a path should throw
			{
				// it should be accepted with the default options
				Assume.That(() => FqlQueryParser.Parse("/foo/bar"), Throws.Nothing);
				// but rejected if PathOnly is specified
				Assert.That(() => FqlQueryParser.Parse("/foo/bar", FqlParsingOptions.TupleOnly), Throws.InstanceOf<FormatException>());
			}

			// queries that include both a path and tuple should throw
			{
				// it should be accepted with the default options
				Assume.That(() => FqlQueryParser.Parse("/foo/bar(1,...)"), Throws.Nothing);
				// but rejected if PathOnly is specified
				Assert.That(() => FqlQueryParser.Parse("/foo/bar(1,...)", FqlParsingOptions.TupleOnly), Throws.InstanceOf<FormatException>());
			}

			// relative queries should throw as well
			{
				// it should be accepted with the default options
				Assume.That(() => FqlQueryParser.Parse(".(1,...)"), Throws.Nothing);
				// but rejected if PathOnly is specified
				Assert.That(() => FqlQueryParser.Parse(".(1,...)", FqlParsingOptions.TupleOnly), Throws.InstanceOf<FormatException>());
			}

		}

		[Test]
		public void Test_Fql_Directories_Matches_Exact()
		{
			// `/`
			{
				var q = FqlQueryParser.Parse("/");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root()));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.False);
				});
			}

			// `/foo`
			{
				var q = FqlQueryParser.Parse("/foo");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo")));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/fo")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foos")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.False);
				});
			}

			// `/foo/bar/baz`
			{
				var q = FqlQueryParser.Parse("/foo/bar/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Name("bar").Name("baz")));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/baz")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/ba")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/bazz")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/baz/jazz")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/not/foo/bar/baz")), Is.False);
				});
			}
		}

		[Test]
		public void Test_Fql_Directories_Matches_Any()
		{
			// `/<>`
			{
				var q = FqlQueryParser.Parse("/<>");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Any()));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.True);
					Assert.That(dir.Match(FdbPath.Parse("/bar")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.False);
				});
			}

			// `/foo/<>`
			{
				var q = FqlQueryParser.Parse("/foo/<>");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Any()));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/baz")), Is.False);
				});
			}

			// `/<>/bar`
			{
				var q = FqlQueryParser.Parse("/<>/bar");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Any().Name("bar")));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/baz")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/baz")), Is.False);
				});
			}

			// `/foo/<>/baz`
			{
				var q = FqlQueryParser.Parse("/foo/<>/baz");
				Assert.That(q, Is.Not.Null);
				Explain(q);
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("foo").Any().Name("baz")));

				Assert.Multiple(() =>
				{
					var dir = q.Directory!;
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/baz")), Is.True);

					Assert.That(dir.Match(FdbPath.Parse("/")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/bar/bar/baz")), Is.False);
					Assert.That(dir.Match(FdbPath.Parse("/foo/bar/baz/jazz")), Is.False);
				});
			}
		}

		[Test]
		public void Test_Fql_Tuples_Matches_Exact()
		{
			{ // ("hello")
				var q = FqlQueryParser.Parse("(\"hello\")");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("hello")));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("hello")), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create("world")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123)), Is.False);
				});
			}
			{ // (123)
				var q = FqlQueryParser.Parse("(123)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Integer(123)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(123)), Is.True);
					Assert.That(tuple.Match(STuple.Create(123L)), Is.True);
					Assert.That(tuple.Match(STuple.Create(123U)), Is.True);
					Assert.That(tuple.Match(STuple.Create(123UL)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(1)), Is.False);
					Assert.That(tuple.Match(STuple.Create(12)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123f)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123, "hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(123, 123)), Is.False);
				});
			}
			{ // (1.23)
				var q = FqlQueryParser.Parse("(1.23)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Number(1.23)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(1.23)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1.23f)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1.23m)), Is.True);
					Assert.That(tuple.Match(STuple.Create((Half) 1.23)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(1)), Is.False);
					Assert.That(tuple.Match(STuple.Create(2)), Is.False);
					Assert.That(tuple.Match(STuple.Create("1.23")), Is.False);
				});
			}
			{ // (@13464654573299691533-4660)
				var vs = VersionStamp.Complete(0xbadc0ffee0ddf00dUL, 0x1234);

				var q = FqlQueryParser.Parse("(@badc0ffee0ddf00d-1234)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VStamp(vs)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(vs)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(VersionStamp.Complete(0xbadc0ffee0ddf00dUL, 0))), Is.False);
					Assert.That(tuple.Match(STuple.Create(VersionStamp.Complete(0xbadc0ffee0ddf00dUL, 0x5678))), Is.False);
					Assert.That(tuple.Match(STuple.Create(VersionStamp.Complete(0x0123456789abcdefUL, 0x1234))), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(vs))), Is.False);
				});
			}
			{ // ("hello", 123)
				var q = FqlQueryParser.Parse("(\"hello\", 123)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("hello").Integer(123)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123L)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123U)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123UL)), Is.True);

					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123f)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123, "world")), Is.False);
					Assert.That(tuple.Match(STuple.Create("world", 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123, "hello")), Is.False);
				});
			}
		}

		[Test]
		public void Test_Fql_Tuples_Matches_Variable()
		{
			// (<str>)
			{
				var q = FqlQueryParser.Parse("(<str>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarString()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("")), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(string))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", "world")), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create("hello"))), Is.False);
				});
			}

			// (<bool>)
			{
				var q = FqlQueryParser.Parse("(<bool>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarBoolean()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(false)), Is.True);
					Assert.That(tuple.Match(STuple.Create(true)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(0)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(true))), Is.False);
				});
			}

			// (<int>)
			{
				var q = FqlQueryParser.Parse("(<int>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarInteger()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(0)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1L)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1U)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1UL)), Is.True);
					Assert.That(tuple.Match(STuple.Create(-1)), Is.True);
					Assert.That(tuple.Match(STuple.Create(-1L)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(123))), Is.False);
				});
			}

			// (<num>)
			{
				var q = FqlQueryParser.Parse("(<num>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarNumber()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(1.0d)), Is.True);
					Assert.That(tuple.Match(STuple.Create(Math.PI)), Is.True);
					Assert.That(tuple.Match(STuple.Create(123f)), Is.True);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.True);
					Assert.That(tuple.Match(STuple.Create(123m)), Is.True);
					Assert.That(tuple.Match(STuple.Create((Half) 123d)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(-1)), Is.False);
					Assert.That(tuple.Match(STuple.Create(0)), Is.False);
					Assert.That(tuple.Match(STuple.Create(1)), Is.False);
					Assert.That(tuple.Match(STuple.Create(1L)), Is.False);
					Assert.That(tuple.Match(STuple.Create(1U)), Is.False);
					Assert.That(tuple.Match(STuple.Create(1UL)), Is.False);
					Assert.That(tuple.Match(STuple.Create(-1L)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(123))), Is.False);
				});
			}

			// (<uuid>)
			{
				var q = FqlQueryParser.Parse("(<uuid>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarUuid()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(Guid.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create(Uuid128.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create(Guid.NewGuid())), Is.True);
					Assert.That(tuple.Match(STuple.Create(Uuid128.NewUuid())), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(0)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(Guid.Empty))), Is.False);
				});
			}

			// (<bytes>)
			{
				var q = FqlQueryParser.Parse("(<bytes>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarBytes()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create(Slice.FromBytes("hello"u8))), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(0)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(Slice.Empty))), Is.False);
				});
			}

			// (<tup>)
			{
				var q = FqlQueryParser.Parse("(<tup>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarTuple()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.True);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(Slice.Empty))), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(0)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.FromBytes("hello"u8))), Is.False);
				});
			}

			// (<vstamp>)
			{
				var q = FqlQueryParser.Parse("(<vstamp>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarVStamp()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var vs = VersionStamp.Complete(0xbadc0ffee0ddf00dUL, 0x1234);

					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(vs)), Is.True);
					Assert.That(tuple.Match(STuple.Create(VersionStamp.Incomplete())), Is.True);
					Assert.That(tuple.Match(STuple.Create(VersionStamp.Incomplete(0x1234))), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(false)), Is.False);
					Assert.That(tuple.Match(STuple.Create(true)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(vs))), Is.False);
				});
			}

		}

		[Test]
		public void Test_Fql_Tuples_Matches_Hybrid()
		{
			// ("hello", <int>)
			{
				var q = FqlQueryParser.Parse("(\"hello\", <int>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("hello").VarInteger()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("hello", 0)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 1)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 12)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123L)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", -1)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", -12)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", -123)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", -123L)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123U)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123UL)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", "123")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123f)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123, "world")), Is.False);
					Assert.That(tuple.Match(STuple.Create("world", 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123, "hello")), Is.False);
				});
			}

			// (<str>, 123)
			{
				var q = FqlQueryParser.Parse("(<str>, 123)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().VarString().Integer(123)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("", 123)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123L)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123U)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123UL)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123, "world")), Is.False);
				});
			}

			// ("hello", <int>, "world")
			{
				var q = FqlQueryParser.Parse("(\"hello\", <int>, \"world\")");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("hello").VarInteger().String("world")));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("hello", 123, "world")), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", default(object), "world")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", "123", "world")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123, "world?")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", 123, "world", "!!!")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", "world", 123)), Is.False);
				});
			}

			// ("hello", <vstamp>, <int>)
			{
				var q = FqlQueryParser.Parse("(\"hello\", <vstamp>, <int>)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("hello").VarVStamp().VarInteger()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var vs = VersionStamp.Complete(0xbadc0ffee0ddf00dUL, 0x1234);

					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("hello", vs, 123)), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", vs)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", default(object), 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", vs, default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", vs, "123")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", vs, 123, "!!!")), Is.False);
					Assert.That(tuple.Match(STuple.Create("hello", "world", 123)), Is.False);
				});
			}

		}

		[Test]
		public void Test_Fql_Tuples_Matches_MaybeMore()
		{
			// ("hello", ...)
			{
				var q = FqlQueryParser.Parse("(\"hello\", ...)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().String("hello").MaybeMore()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create("hello")), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", default(object))), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", 123f)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", "world")), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", Guid.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", Slice.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create("hello", STuple.Create())), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create("world")), Is.False);
					Assert.That(tuple.Match(STuple.Create("world", "hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create("hello"))), Is.False);
				});
			}

			// (1, <int>, ...)
			{
				var q = FqlQueryParser.Parse("(1, <int>, ...)");
				Explain(q);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().Integer(1).VarInteger().MaybeMore()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Match(STuple.Create(1, 123)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, default(object))), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, 456)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, 456f)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, "world")), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, Guid.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, Slice.Empty)), Is.True);
					Assert.That(tuple.Match(STuple.Create(1, 123, STuple.Create())), Is.True);

					Assert.That(tuple.Match(STuple.Create()), Is.False);
					Assert.That(tuple.Match(STuple.Create(1)), Is.False);
					Assert.That(tuple.Match(STuple.Create("world")), Is.False);
					Assert.That(tuple.Match(STuple.Create(2, 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(2, "hello")), Is.False);
					Assert.That(tuple.Match(STuple.Create(1, default(object))), Is.False);
					Assert.That(tuple.Match(STuple.Create(1d)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Guid.Empty, 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(Slice.Empty, 123)), Is.False);
					Assert.That(tuple.Match(STuple.Create(STuple.Create(1), 123)), Is.False);
				});
			}

		}

	}

}

#endif
