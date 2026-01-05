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

namespace FoundationDB.Client.Tests
{
	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbPathFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbPath_Empty()
		{
			var empty = FdbPath.Empty;
			Assert.That(empty.IsAbsolute, Is.False);
			Assert.That(empty.IsEmpty, Is.True);
			Assert.That(empty.IsRoot, Is.False);
			Assert.That(empty.Count, Is.EqualTo(0));
			Assert.That(empty.ToString(), Is.EqualTo(string.Empty));
			Assert.That(empty.Name, Is.EqualTo(string.Empty));
			Assert.That(empty.Segments.Length, Is.EqualTo(0));
			Assert.That(empty.ToArray(), Is.EqualTo(Array.Empty<string>()));

			Assert.That(empty, Is.EqualTo(FdbPath.Empty));
			Assert.That(empty == FdbPath.Empty, Is.True);
			Assert.That(empty != FdbPath.Empty, Is.False);
			Assert.That(empty, Is.Not.EqualTo(FdbPath.Root));
			Assert.That(empty == FdbPath.Root, Is.False);
			Assert.That(empty != FdbPath.Root, Is.True);

			Assert.That(FdbPath.Parse("Hello").StartsWith(empty), Is.True);
			Assert.That(FdbPath.Parse("/Hello").StartsWith(empty), Is.False);

			Assert.That($"{empty}", Is.Empty);
			Assert.That($"{empty:D}", Is.Empty);
			Assert.That($"{empty:N}", Is.Empty);
		}

		[Test]
		public void Test_FdbPath_Root()
		{
			var root = FdbPath.Root;
			Assert.That(root.IsAbsolute, Is.True);
			Assert.That(root.IsEmpty, Is.False);
			Assert.That(root.IsRoot, Is.True);
			Assert.That(root.Count, Is.EqualTo(0));
			Assert.That(root.ToString(), Is.EqualTo("/"));
			Assert.That(root.Name, Is.EqualTo(string.Empty));
			Assert.That(root.Segments.Length, Is.EqualTo(0));
			Assert.That(root.ToArray(), Is.EqualTo(Array.Empty<string>()));

			Assert.That(root, Is.EqualTo(FdbPath.Root));
			Assert.That(root == FdbPath.Root, Is.True);
			Assert.That(root != FdbPath.Root, Is.False);
			Assert.That(root, Is.Not.EqualTo(FdbPath.Empty));
			Assert.That(root == FdbPath.Empty, Is.False);
			Assert.That(root != FdbPath.Empty, Is.True);

			Assert.That(FdbPath.Parse("Hello").StartsWith(root), Is.False);
			Assert.That(FdbPath.Parse("/Hello").StartsWith(root), Is.True);

			Assert.That($"{root}", Is.EqualTo("/"));
			Assert.That($"{root:D}", Is.EqualTo("/"));
			Assert.That($"{root:N}", Is.EqualTo("/"));
		}

		[Test]
		public void Test_FdbPath_Basics()
		{
			{
				var path = FdbPath.Relative("Foo");
				Assert.That(path.IsEmpty, Is.False, "[Foo].IsEmpty");
				Assert.That(path.Count, Is.EqualTo(1), "[Foo].Count");
				Assert.That(path.Name, Is.EqualTo("Foo"), "[Foo].Name");
				Assert.That(path.ToString(), Is.EqualTo("Foo"), "[Foo].ToString()");
				Assert.That(path[0].Name, Is.EqualTo("Foo"), "[Foo][0]");
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty), "[Foo][0]");
				Assert.That(path.GetParent(), Is.EqualTo(FdbPath.Empty), "[Foo].Name");

				Assert.That(path, Is.EqualTo(path), "[Foo].Equals([Foo])");
#pragma warning disable CS1718 // Comparison made to same variable
				// ReSharper disable EqualExpressionComparison
				Assert.That(path == path, Is.True, "[Foo] == [Foo]");
				Assert.That(path != path, Is.False, "[Foo] != [Foo]");
				// ReSharper restore EqualExpressionComparison
#pragma warning restore CS1718 // Comparison made to same variable

				Assert.That(path, Is.EqualTo(FdbPath.Relative("Foo")), "[Foo].Equals([Foo]')");
				Assert.That(path, Is.EqualTo(FdbPath.Relative("Foo", "Bar").GetParent()), "[Foo].Equals([Foo/Bar].GetParent())");

				Assert.That(path, Is.Not.EqualTo(FdbPath.Empty), "[Foo].Equals(Empty)");
				Assert.That(path == FdbPath.Empty, Is.False, "[Foo] == Empty");
				Assert.That(path != FdbPath.Empty, Is.True, "[Foo] != Empty");

				Assert.That($"{path}", Is.EqualTo("Foo"));
				Assert.That($"{path:D}", Is.EqualTo("Foo"));
				Assert.That($"{path:N}", Is.EqualTo("Foo"));
			}

			{
				var path1 = FdbPath.Relative("Foo", "Bar");
				var path2 = FdbPath.Parse("Foo/Bar");
				var path3 = new FdbPath(new[] { FdbPathSegment.Create("Foo"), FdbPathSegment.Create("Bar") }, false);

				Assert.That(path2, Is.EqualTo(path1), "path1 eq path2");
				Assert.That(path3, Is.EqualTo(path1), "path1 eq path3");
				Assert.That(path3, Is.EqualTo(path2), "path2 eq path3");

				Assert.That(path2.GetHashCode(), Is.EqualTo(path1.GetHashCode()), "h(path1) == h(path2)");
				Assert.That(path3.GetHashCode(), Is.EqualTo(path1.GetHashCode()), "h(path1) == h(path3)");

				Assert.That($"{path1}", Is.EqualTo("Foo/Bar"));
				Assert.That($"{path1:D}", Is.EqualTo("Foo/Bar"));
				Assert.That($"{path1:N}", Is.EqualTo("Foo/Bar"));
			}

		}

		[Test]
		public void Test_FdbPath_Simple_Relative()
		{
			var foo = FdbPath.Relative("Foo");
			Assert.Multiple(() =>
			{
				Assert.That(foo.ToString(), Is.EqualTo("Foo"));
				Assert.That(foo.IsAbsolute, Is.False);
				Assert.That(foo.IsEmpty, Is.False);
				Assert.That(foo.IsRoot, Is.False);
				Assert.That(foo.Count, Is.EqualTo(1));
				Assert.That(foo[0].Name, Is.EqualTo("Foo"));
				Assert.That(foo[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(foo.Name, Is.EqualTo("Foo"));
				Assert.That(foo.ToArray(), Is.EqualTo(new[] { FdbPathSegment.Create("Foo") }));
				Assert.That(foo.StartsWith(FdbPath.Empty), Is.True);
				Assert.That(foo.IsChildOf(FdbPath.Empty), Is.True);
				Assert.That(foo.EndsWith(FdbPath.Empty), Is.True);
				Assert.That(foo.IsParentOf(FdbPath.Empty), Is.False);
				Assert.That($"{foo}", Is.EqualTo("Foo"));
				Assert.That($"{foo:D}", Is.EqualTo("Foo"));
				Assert.That($"{foo:N}", Is.EqualTo("Foo"));
			});

			var fooBar = foo["Bar"];
			Assert.Multiple(() =>
			{
				Assert.That(fooBar.ToString(), Is.EqualTo("Foo/Bar"));
				Assert.That(fooBar.IsAbsolute, Is.False);
				Assert.That(fooBar.IsEmpty, Is.False);
				Assert.That(fooBar.IsRoot, Is.False);
				Assert.That(fooBar.Count, Is.EqualTo(2));
				Assert.That(fooBar[0].Name, Is.EqualTo("Foo"));
				Assert.That(fooBar[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBar[1].Name, Is.EqualTo("Bar"));
				Assert.That(fooBar[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBar.Name, Is.EqualTo("Bar"));
				Assert.That(fooBar.ToArray(), Is.EqualTo(new[] { FdbPathSegment.Create("Foo"), FdbPathSegment.Create("Bar") }));
				Assert.That(fooBar.StartsWith(FdbPath.Empty), Is.True);
				Assert.That(fooBar.IsChildOf(FdbPath.Empty), Is.True);
				Assert.That(fooBar.IsParentOf(FdbPath.Empty), Is.False);
				Assert.That(fooBar.EndsWith(FdbPath.Empty), Is.True);
				Assert.That(fooBar.StartsWith(foo), Is.True);
				Assert.That(fooBar.IsChildOf(foo), Is.True);
				Assert.That(fooBar.EndsWith(foo), Is.False);
				Assert.That(fooBar.IsParentOf(foo), Is.False);
				Assert.That($"{fooBar}", Is.EqualTo("Foo/Bar"));
				Assert.That($"{fooBar:D}", Is.EqualTo("Foo/Bar"));
				Assert.That($"{fooBar:N}", Is.EqualTo("Foo/Bar"));
			});

			var fooBarBaz = fooBar["Baz"];
			Assert.Multiple(() =>
			{
				Assert.That(fooBarBaz.ToString(), Is.EqualTo("Foo/Bar/Baz"));
				Assert.That(fooBarBaz.IsAbsolute, Is.False);
				Assert.That(fooBarBaz.IsEmpty, Is.False);
				Assert.That(fooBarBaz.IsRoot, Is.False);
				Assert.That(fooBarBaz.Count, Is.EqualTo(3));
				Assert.That(fooBarBaz[0].Name, Is.EqualTo("Foo"));
				Assert.That(fooBarBaz[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBarBaz[1].Name, Is.EqualTo("Bar"));
				Assert.That(fooBarBaz[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBarBaz[2].Name, Is.EqualTo("Baz"));
				Assert.That(fooBarBaz[2].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBarBaz.Name, Is.EqualTo("Baz"));
				Assert.That(fooBarBaz.ToArray(), Is.EqualTo(new[] { FdbPathSegment.Create("Foo"), FdbPathSegment.Create("Bar"), FdbPathSegment.Create("Baz") }));
				Assert.That(fooBarBaz.StartsWith(FdbPath.Empty), Is.True);
				Assert.That(fooBarBaz.IsChildOf(FdbPath.Empty), Is.True);
				Assert.That(fooBarBaz.IsParentOf(FdbPath.Empty), Is.False);
				Assert.That(fooBarBaz.EndsWith(FdbPath.Empty), Is.True);
				Assert.That(fooBarBaz.StartsWith(foo), Is.True);
				Assert.That(fooBarBaz.IsChildOf(foo), Is.True);
				Assert.That(fooBarBaz.EndsWith(foo), Is.False);
				Assert.That(fooBarBaz.IsParentOf(foo), Is.False);
				Assert.That(fooBarBaz.StartsWith(fooBar), Is.True);
				Assert.That(fooBarBaz.IsChildOf(fooBar), Is.True);
				Assert.That(fooBarBaz.EndsWith(fooBar), Is.False);
				Assert.That(fooBarBaz.IsParentOf(fooBar), Is.False);
				Assert.That($"{fooBarBaz}", Is.EqualTo("Foo/Bar/Baz"));
				Assert.That($"{fooBarBaz:D}", Is.EqualTo("Foo/Bar/Baz"));
				Assert.That($"{fooBarBaz:N}", Is.EqualTo("Foo/Bar/Baz"));
			});
		}

		[Test]
		public void Test_FdbPath_Simple_Absolute()
		{
			var foo = FdbPath.Absolute("Foo");
			Assert.Multiple(() =>
			{
				Assert.That(foo.ToString(), Is.EqualTo("/Foo"));
				Assert.That(foo.IsAbsolute, Is.True);
				Assert.That(foo.IsEmpty, Is.False);
				Assert.That(foo.IsRoot, Is.False);
				Assert.That(foo.Count, Is.EqualTo(1));
				Assert.That(foo[0].Name, Is.EqualTo("Foo"));
				Assert.That(foo[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(foo.Name, Is.EqualTo("Foo"));
				Assert.That(foo.ToArray(), Is.EqualTo(new[] { FdbPathSegment.Create("Foo") }));
				Assert.That(foo.StartsWith(FdbPath.Root), Is.True);
				Assert.That(foo.IsChildOf(FdbPath.Root), Is.True);
				Assert.That(foo.EndsWith(FdbPath.Root), Is.True);
				Assert.That(foo.IsParentOf(FdbPath.Root), Is.False);
				Assert.That($"{foo}", Is.EqualTo("/Foo"));
				Assert.That($"{foo:D}", Is.EqualTo("/Foo"));
				Assert.That($"{foo:N}", Is.EqualTo("/Foo"));
			});

			var fooBar = foo["Bar"];
			Assert.Multiple(() =>
			{
				Assert.That(fooBar.ToString(), Is.EqualTo("/Foo/Bar"));
				Assert.That(fooBar.IsAbsolute, Is.True);
				Assert.That(fooBar.IsEmpty, Is.False);
				Assert.That(fooBar.IsRoot, Is.False);
				Assert.That(fooBar.Count, Is.EqualTo(2));
				Assert.That(fooBar[0].Name, Is.EqualTo("Foo"));
				Assert.That(fooBar[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBar[1].Name, Is.EqualTo("Bar"));
				Assert.That(fooBar[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBar.Name, Is.EqualTo("Bar"));
				Assert.That(fooBar.ToArray(), Is.EqualTo(new[] { FdbPathSegment.Create("Foo"), FdbPathSegment.Create("Bar") }));
				Assert.That(fooBar.StartsWith(FdbPath.Root), Is.True);
				Assert.That(fooBar.IsChildOf(FdbPath.Root), Is.True);
				Assert.That(fooBar.IsParentOf(FdbPath.Root), Is.False);
				Assert.That(fooBar.EndsWith(FdbPath.Root), Is.True);
				Assert.That(fooBar.StartsWith(foo), Is.True);
				Assert.That(fooBar.IsChildOf(foo), Is.True);
				Assert.That(fooBar.IsParentOf(foo), Is.False);
				Assert.That(fooBar.EndsWith(foo), Is.False);
				Assert.That($"{fooBar}", Is.EqualTo("/Foo/Bar"));
				Assert.That($"{fooBar:D}", Is.EqualTo("/Foo/Bar"));
				Assert.That($"{fooBar:N}", Is.EqualTo("/Foo/Bar"));
			});

			var fooBarBaz = fooBar["Baz"];
			Assert.Multiple(() =>
			{
				Assert.That(fooBarBaz.ToString(), Is.EqualTo("/Foo/Bar/Baz"));
				Assert.That(fooBarBaz.IsAbsolute, Is.True);
				Assert.That(fooBarBaz.IsEmpty, Is.False);
				Assert.That(fooBarBaz.IsRoot, Is.False);
				Assert.That(fooBarBaz.Count, Is.EqualTo(3));
				Assert.That(fooBarBaz[0].Name, Is.EqualTo("Foo"));
				Assert.That(fooBarBaz[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBarBaz[1].Name, Is.EqualTo("Bar"));
				Assert.That(fooBarBaz[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBarBaz[2].Name, Is.EqualTo("Baz"));
				Assert.That(fooBarBaz[2].LayerId, Is.EqualTo(string.Empty));
				Assert.That(fooBarBaz.Name, Is.EqualTo("Baz"));
				Assert.That(fooBarBaz.ToArray(), Is.EqualTo(new [] { FdbPathSegment.Create("Foo"), FdbPathSegment.Create("Bar"), FdbPathSegment.Create("Baz") }));
				Assert.That(fooBarBaz.StartsWith(FdbPath.Root), Is.True);
				Assert.That(fooBarBaz.IsChildOf(FdbPath.Root), Is.True);
				Assert.That(fooBarBaz.IsParentOf(FdbPath.Root), Is.False);
				Assert.That(fooBarBaz.EndsWith(FdbPath.Root), Is.True);
				Assert.That(fooBarBaz.StartsWith(foo), Is.True);
				Assert.That(fooBarBaz.IsChildOf(foo), Is.True);
				Assert.That(fooBarBaz.IsParentOf(foo), Is.False);
				Assert.That(fooBarBaz.EndsWith(foo), Is.False);
				Assert.That(fooBarBaz.StartsWith(fooBar), Is.True);
				Assert.That(fooBarBaz.IsChildOf(fooBar), Is.True);
				Assert.That(fooBarBaz.IsParentOf(fooBar), Is.False);
				Assert.That(fooBarBaz.EndsWith(fooBar), Is.False);
				Assert.That($"{fooBarBaz}", Is.EqualTo("/Foo/Bar/Baz"));
				Assert.That($"{fooBarBaz:D}", Is.EqualTo("/Foo/Bar/Baz"));
				Assert.That($"{fooBarBaz:N}", Is.EqualTo("/Foo/Bar/Baz"));
			});
		}

		[Test]
		public void Test_FdbPath_Substring_Absolute()
		{
			var path = FdbPath.Absolute("Foo", "Bar", "Baz");
			Assert.That(path.ToString(), Is.EqualTo("/Foo/Bar/Baz"));

			var slice = path.Substring(0, 2);
			Assert.Multiple(() =>
			{
				Assert.That(slice.IsAbsolute, Is.True);
				Assert.That(slice.Count, Is.EqualTo(2));
				Assert.That(slice[0].Name, Is.EqualTo("Foo"));
				Assert.That(slice[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(slice[1].Name, Is.EqualTo("Bar"));
				Assert.That(slice[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(slice.ToString(), Is.EqualTo("/Foo/Bar"));
				Assert.That($"{slice}", Is.EqualTo("/Foo/Bar"));
				Assert.That(slice.IsParentOf(path), Is.True);
				Assert.That(slice.IsChildOf(path), Is.False);
				Assert.That(path.IsChildOf(slice), Is.True);
				Assert.That(path.IsParentOf(slice), Is.False);
			});
		}

		[Test]
		public void Test_FdbPath_StartsWith()
		{
			// no layer
			Assert.Multiple(() =>
			{
				Assert.That(FdbPath.Root.StartsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A"].StartsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].StartsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].StartsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].StartsWith(FdbPath.Root["A"]["B"]), Is.True);
				Assert.That(FdbPath.Root.StartsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A"].StartsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].StartsWith(FdbPath.Root["A"]["B"]), Is.True);

				Assert.That(FdbPath.Root.StartsWith(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["B"].StartsWith(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["B"].StartsWith(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Root["C"]["A"]["B"].StartsWith(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Root["ABC"].StartsWith(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["ABC"].StartsWith(FdbPath.Root["A"]["B"]), Is.False);
			});

			// with layer
			Assert.Multiple(() =>
			{
				Assert.That(FdbPath.Root["A", "Foo"].StartsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A", "Foo"]["B"].StartsWith(FdbPath.Root["A", "Foo"]), Is.True);
				Assert.That(FdbPath.Root["A", "Foo"]["B"].StartsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].StartsWith(FdbPath.Root["A", "Foo"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].StartsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].StartsWith(FdbPath.Root["A"]["B"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B", "Foo"]["C"].StartsWith(FdbPath.Root["A"]["B"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].StartsWith(FdbPath.Root["A"]["B", "Foo"]), Is.True);
				Assert.That(FdbPath.Root["A", "Foo"]["B", "Bar"].StartsWith(FdbPath.Root["A"]["B"]), Is.True);

				Assert.That(FdbPath.Root["A", "Foo"].StartsWith(FdbPath.Root["A", "Bar"]), Is.False);
				Assert.That(FdbPath.Root["A", "Foo"]["B"].StartsWith(FdbPath.Root["A", "Bar"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B", "Foo"]["C"].StartsWith(FdbPath.Root["A", "Foo"]["B", "Bar"]), Is.False);
			});

		}

		[Test]
		public void Test_FdbPath_EndsWith()
		{
			// no layer
			Assert.Multiple(() =>
			{
				Assert.That(FdbPath.Root.EndsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A"].EndsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].EndsWith(FdbPath.Root["B"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].EndsWith(FdbPath.Root["C"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].EndsWith(FdbPath.Root["B"]["C"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].EndsWith(FdbPath.Root["A"]["B"]["C"]), Is.True);
				Assert.That(FdbPath.Root["A"].EndsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].EndsWith(FdbPath.Root["A"]["B"]), Is.True);

				Assert.That(FdbPath.Root.EndsWith(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["B"].EndsWith(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["B"]["A"].EndsWith(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B"]["C"].EndsWith(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Root["ABC"].EndsWith(FdbPath.Root["C"]), Is.False);
				Assert.That(FdbPath.Root["ABC"].EndsWith(FdbPath.Root["BC"]), Is.False);
			});

			// with layer
			Assert.Multiple(() =>
			{
				Assert.That(FdbPath.Root["A", "Foo"].EndsWith(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A", "Foo"].EndsWith(FdbPath.Root["A", "Foo"]), Is.True);
				Assert.That(FdbPath.Root["A", "Foo"].EndsWith(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C", "Foo"].EndsWith(FdbPath.Root["C", "Foo"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].EndsWith(FdbPath.Root["C", "Foo"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B", "Foo"]["C"].EndsWith(FdbPath.Root["B"]["C"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].EndsWith(FdbPath.Root["B", "Foo"]["C"]), Is.True);
				Assert.That(FdbPath.Root["A", "Foo"]["B", "Bar"].EndsWith(FdbPath.Root["A"]["B"]), Is.True);

				Assert.That(FdbPath.Root["A", "Foo"].EndsWith(FdbPath.Root["A", "Bar"]), Is.False);
				Assert.That(FdbPath.Root["A", "Foo"]["B"].EndsWith(FdbPath.Root["A", "Bar"]["B"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B", "Foo"]["C", "Bar"].EndsWith(FdbPath.Root["B", "FooZ"]["C", "Bar"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B", "Foo"]["C", "Bar"].EndsWith(FdbPath.Root["B", "Foo"]["C", "BarZ"]), Is.False);
			});

		}

		[Test]
		public void Test_FdbPath_IsParentOf()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbPath.Root.IsParentOf(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"].IsParentOf(FdbPath.Root["A"]["B"]), Is.True);
				Assert.That(FdbPath.Root["A"].IsParentOf(FdbPath.Root["A"]["B"]["C"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].IsParentOf(FdbPath.Root["A"]["B"]["C"]), Is.True);

				Assert.That(FdbPath.Root.IsParentOf(FdbPath.Root), Is.False);
				Assert.That(FdbPath.Root["A"].IsParentOf(FdbPath.Root), Is.False);
				Assert.That(FdbPath.Root["A"].IsParentOf(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["A"].IsParentOf(FdbPath.Root["B"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B"].IsParentOf(FdbPath.Root["B"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B"].IsParentOf(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B"].IsParentOf(FdbPath.Root["C"]["A"]["B"]), Is.False);
			});
		}

		[Test]
		public void Test_FdbPath_IsChildOf()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbPath.Root["A"].IsChildOf(FdbPath.Root), Is.True);
				Assert.That(FdbPath.Root["A"]["B"].IsChildOf(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].IsChildOf(FdbPath.Root["A"]), Is.True);
				Assert.That(FdbPath.Root["A"]["B"]["C"].IsChildOf(FdbPath.Root["A"]["B"]), Is.True);

				Assert.That(FdbPath.Root.IsChildOf(FdbPath.Root), Is.False);
				Assert.That(FdbPath.Root.IsChildOf(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["A"].IsChildOf(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["B"].IsChildOf(FdbPath.Root["A"]), Is.False);
				Assert.That(FdbPath.Root["B"].IsChildOf(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Root["A"]["B"].IsChildOf(FdbPath.Root["A"]["B"]), Is.False);
				Assert.That(FdbPath.Absolute("C", "A", "B").IsChildOf(FdbPath.Root["A"]["B"]), Is.False);
			});
		}

		[Test]
		public void Test_FdbPathSegment_Parse()
		{
			Assert.That(FdbPathSegment.Parse("Hello"), Is.EqualTo(FdbPathSegment.Create("Hello")));
			Assert.That(FdbPathSegment.Parse("Hello[World]"), Is.EqualTo(FdbPathSegment.Create("Hello", "World")));
			Assert.That(FdbPathSegment.Parse("Hello[]"), Is.EqualTo(FdbPathSegment.Create("Hello", "")));
			Assert.That(FdbPathSegment.Parse(@"Hello\[World\]"), Is.EqualTo(FdbPathSegment.Create("Hello[World]")));
			Assert.That(FdbPathSegment.Parse(@"Hello\[World\][Layer]"), Is.EqualTo(FdbPathSegment.Create("Hello[World]", "Layer")));
			Assert.That(FdbPathSegment.Parse(@"Hello\/World[Foo\[Bar]"), Is.EqualTo(FdbPathSegment.Create("Hello/World", "Foo[Bar")));
			Assert.That(FdbPathSegment.Parse(@"Hello\/World[Foo\[Bar\]]"), Is.EqualTo(FdbPathSegment.Create("Hello/World", "Foo[Bar]")));
		}

		[Test]
		public void Test_FdbPathSegment_Create()
		{
			{ // ("Hello",) => "Hello"
				var seg = FdbPathSegment.Create("Hello");
				Assert.That(seg.Name, Is.EqualTo("Hello"));
				Assert.That(seg.LayerId, Is.Empty);
				Assert.That(seg.ToString(), Is.EqualTo("Hello"));
				Assert.That(seg, Is.EqualTo(new FdbPathSegment("Hello")));
				Assert.That(seg == new FdbPathSegment("Hello"), Is.True);
				Assert.That(seg != new FdbPathSegment("Hello"), Is.False);
				Assert.That(seg == new FdbPathSegment("World"), Is.False);
				Assert.That(seg != new FdbPathSegment("World"), Is.True);
				Assert.That(seg == new FdbPathSegment("Hello", "World"), Is.False);
				Assert.That(seg != new FdbPathSegment("Hello", "World"), Is.True);
				Assert.That(seg == new FdbPathSegment("Hello", "Hello"), Is.False);
				Assert.That(seg != new FdbPathSegment("Hello", "Hello"), Is.True);
				Assert.That($"{seg}", Is.EqualTo("Hello"));
				Assert.That($"{seg:D}", Is.EqualTo("Hello"));
				Assert.That($"{seg:N}", Is.EqualTo("Hello"));
			}

			{ // ("Hello", null) => "Hello"
				var seg = FdbPathSegment.Create("Hello", null!);
				Assert.That(seg.Name, Is.EqualTo("Hello"));
				Assert.That(seg.LayerId, Is.Empty);
				Assert.That(seg.ToString(), Is.EqualTo("Hello"));
				Assert.That(seg, Is.EqualTo(new FdbPathSegment("Hello")));
				Assert.That(seg == new FdbPathSegment("Hello"), Is.True);
				Assert.That(seg != new FdbPathSegment("Hello"), Is.False);
				Assert.That(seg == new FdbPathSegment("World"), Is.False);
				Assert.That(seg != new FdbPathSegment("World"), Is.True);
				Assert.That(seg == new FdbPathSegment("Hello", "World"), Is.False);
				Assert.That(seg != new FdbPathSegment("Hello", "World"), Is.True);
				Assert.That(seg == new FdbPathSegment("Hello", "Hello"), Is.False);
				Assert.That(seg != new FdbPathSegment("Hello", "Hello"), Is.True);
				Assert.That($"{seg}", Is.EqualTo("Hello"));
				Assert.That($"{seg:D}", Is.EqualTo("Hello"));
				Assert.That($"{seg:N}", Is.EqualTo("Hello"));
			}

			{ // ("Hello", "World") => "Hello[World]"
				var seg = FdbPathSegment.Create("Hello", "World");
				Assert.That(seg.Name, Is.EqualTo("Hello"));
				Assert.That(seg.LayerId, Is.EqualTo("World"));
				Assert.That(seg.ToString(), Is.EqualTo("Hello[World]"));
				Assert.That(seg, Is.EqualTo(new FdbPathSegment("Hello", "World")));
				Assert.That(seg == new FdbPathSegment("Hello", "World"), Is.True);
				Assert.That(seg != new FdbPathSegment("Hello", "World"), Is.False);
				Assert.That(seg == new FdbPathSegment("Hello"), Is.False);
				Assert.That(seg != new FdbPathSegment("Hello"), Is.True);
				Assert.That(seg == new FdbPathSegment("World", "Hello"), Is.False);
				Assert.That(seg != new FdbPathSegment("World", "Hello"), Is.True);
				Assert.That($"{seg}", Is.EqualTo("Hello[World]"));
				Assert.That($"{seg:D}", Is.EqualTo("Hello[World]"));
				Assert.That($"{seg:N}", Is.EqualTo("Hello"));
			}
		}

		[Test]
		public void Test_FdbPathSegment_Encode()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbPathSegment.Create("Hello").ToString(), Is.EqualTo("Hello"));
				Assert.That(FdbPathSegment.Create("A[B]C").ToString(), Is.EqualTo(@"A\[B\]C"));
				Assert.That(FdbPathSegment.Create("Hello", "World").ToString(), Is.EqualTo("Hello[World]"));
				Assert.That(FdbPathSegment.Create("A[B]C", "World").ToString(), Is.EqualTo(@"A\[B\]C[World]"));
				Assert.That(FdbPathSegment.Create("Hello", "D[E]F").ToString(), Is.EqualTo(@"Hello[D\[E\]F]"));
				Assert.That(FdbPathSegment.Create("A[B]C", "D[E]F").ToString(), Is.EqualTo(@"A\[B\]C[D\[E\]F]"));
				Assert.That(FdbPathSegment.Create("A/B\\C", "D/E\\F").ToString(), Is.EqualTo(@"A\/B\\C[D\/E\\F]"));
				Assert.That(FdbPathSegment.Create("/\\/\\", "][][").ToString(), Is.EqualTo(@"\/\\\/\\[\]\[\]\[]"));

				Assert.That($"{FdbPathSegment.Create("Hello")}", Is.EqualTo("Hello"));
				Assert.That($"{FdbPathSegment.Create("A[B]C")}", Is.EqualTo(@"A\[B\]C"));
				Assert.That($"{FdbPathSegment.Create("A[B]C", "D[E]F")}", Is.EqualTo(@"A\[B\]C[D\[E\]F]"));
				Assert.That($"{FdbPathSegment.Create("A/B\\C", "D/E\\F")}", Is.EqualTo(@"A\/B\\C[D\/E\\F]"));
				Assert.That($"{FdbPathSegment.Create("/\\/\\", "][][")}", Is.EqualTo(@"\/\\\/\\[\]\[\]\[]"));

				Assert.That($"{FdbPathSegment.Create("A[B]C", "D[E]F"):N}", Is.EqualTo(@"A\[B\]C"));
				Assert.That($"{FdbPathSegment.Create("A/B\\C", "D/E\\F"):N}", Is.EqualTo(@"A\/B\\C"));
				Assert.That($"{FdbPathSegment.Create("/\\/\\", "][]["):N}", Is.EqualTo(@"\/\\\/\\"));
			});
		}

		[Test]
		public void Test_FdbPath_Parse()
		{
			// Relative paths

			FdbPath Parse(string value)
			{
				Log($"\"{value}\":");
				var path = FdbPath.Parse(value);
				if (path.IsEmpty)
					Log("> <empty>");
				else if (path.IsRoot)
					Log("> <root>");
				else 
					Log($"> Path='{path.ToString()}', Count={path.Count}, Name='{path.Name}', Absolute={path.IsAbsolute}");
				return path;
			}

			{ // Empty
				var path = Parse("");
				Assert.That(path.IsAbsolute, Is.False, ".Absolute");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.IsEmpty, Is.True, ".IsEmpty");
				Assert.That(path.Count, Is.EqualTo(0), ".Count");
				Assert.That(path.ToString(), Is.EqualTo(""));
				Assert.That(path.Name, Is.EqualTo(string.Empty));
			}
			{ // Foo
				var path = Parse("Foo");
				Assert.That(path.IsAbsolute, Is.False, ".Absolute");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.Count, Is.EqualTo(1), ".Count");
				Assert.That(path[0].Name, Is.EqualTo("Foo"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo("Foo"));
				Assert.That(path.Name, Is.EqualTo("Foo"));
			}
			{ // Foo/Bar/Baz
				var path = Parse("Foo/Bar/Baz");
				Assert.That(path.IsAbsolute, Is.False, ".Absolute");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.Count, Is.EqualTo(3), ".Count");
				Assert.That(path[0].Name, Is.EqualTo("Foo"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[1].Name, Is.EqualTo("Bar"));
				Assert.That(path[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[2].Name, Is.EqualTo("Baz"));
				Assert.That(path[2].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo("Foo/Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}

			// Absolute path

			{ // Root ("/")
				var path = Parse("/");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.True, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(0));
				Assert.That(path.ToString(), Is.EqualTo("/"));
				Assert.That(path.Name, Is.EqualTo(string.Empty));
			}
			{ // /Foo
				var path = Parse("/Foo");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0].Name, Is.EqualTo("Foo"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo("/Foo"));
				Assert.That(path.Name, Is.EqualTo("Foo"));
			}

			{ // /Foo/Bar/Baz
				var path = Parse("/Foo/Bar/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(3));
				Assert.That(path[0].Name, Is.EqualTo("Foo"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[1].Name, Is.EqualTo("Bar"));
				Assert.That(path[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[2].Name, Is.EqualTo("Baz"));
				Assert.That(path[2].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo("/Foo/Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}
			{ // /Foo\/Bar/Baz => { "Foo/Bar", "Baz" }
				var path = Parse(@"/Foo\/Bar/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0].Name, Is.EqualTo("Foo/Bar"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[1].Name, Is.EqualTo("Baz"));
				Assert.That(path[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo(@"/Foo\/Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}
			{ // /Foo\\Bar/Baz => { @"Foo\Bar", "Baz" }
				var path = Parse(@"/Foo\\Bar/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0].Name, Is.EqualTo("Foo\\Bar"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[1].Name, Is.EqualTo("Baz"));
				Assert.That(path[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo(@"/Foo\\Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}
			{ // /Foo[Bar]/Baz => { "Foo[Bar]", "Baz" }
				var path = Parse("/Foo\\[Bar]/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0].Name, Is.EqualTo("Foo[Bar]"));
				Assert.That(path[0].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path[1].Name, Is.EqualTo("Baz"));
				Assert.That(path[1].LayerId, Is.EqualTo(string.Empty));
				Assert.That(path.ToString(), Is.EqualTo(@"/Foo\[Bar\]/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}

			// Layers

			{ // "/Foo[Test]"
				var path = Parse("/Foo[test]");
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.ToString(), Is.EqualTo("/Foo[test]"));
				Assert.That(path.IsEmpty, Is.False);
				Assert.That(path.IsRoot, Is.False);
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0].Name, Is.EqualTo("Foo"));
				Assert.That(path[0].LayerId, Is.EqualTo("test"));
				Assert.That(path.Name, Is.EqualTo("Foo"));
				Assert.That(path.LayerId, Is.EqualTo("test"));
				Assert.That(path.ToArray(), Is.EqualTo(new [] { FdbPathSegment.Create("Foo", "test") }));
				Assert.That(path.StartsWith(FdbPath.Root), Is.True);
				Assert.That(path.IsChildOf(FdbPath.Root), Is.True);
				Assert.That(path.EndsWith(FdbPath.Root), Is.True);
				Assert.That(path.IsParentOf(FdbPath.Root), Is.False);
			}

			// invalid paths
			{ 
				// "/Foo//Baz" => empty segment
				Assert.That(() => FdbPath.Parse("/Foo//Baz"), Throws.InstanceOf<FormatException>());

				// "/Foo/Bar/" => last is empty
				Assert.That(() => FdbPath.Parse("/Foo/Bar/"), Throws.InstanceOf<FormatException>());
			}

		}

		[Test]
		public void Test_FdbPath_Concat()
		{
			{
				var path = FdbPath.Root["Hello"];
				Assert.That(path.ToString(), Is.EqualTo("/Hello"));
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0], Is.EqualTo(new FdbPathSegment("Hello")));
			}
			{
				var path = FdbPath.Root["Hello", "World"];
				Assert.That(path.ToString(), Is.EqualTo("/Hello[World]"));
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0], Is.EqualTo(new FdbPathSegment("Hello", "World")));
			}
			{
				var path = FdbPath.Root["Hello"]["World"];
				Assert.That(path.ToString(), Is.EqualTo("/Hello/World"));
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0], Is.EqualTo(new FdbPathSegment("Hello")));
				Assert.That(path[1], Is.EqualTo(new FdbPathSegment("World")));
			}
			{
				var path = FdbPath.Root[FdbPathSegment.Create("Hello")];
				Assert.That(path.ToString(), Is.EqualTo("/Hello"));
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0], Is.EqualTo(new FdbPathSegment("Hello")));
			}
			{
				var path = FdbPath.Root[FdbPathSegment.Create("Hello", "World")];
				Assert.That(path.ToString(), Is.EqualTo("/Hello[World]"));
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0], Is.EqualTo(new FdbPathSegment("Hello", "World")));
			}
			{
				var relative = FdbPath.Empty["Hello"]["World"];
				var path = FdbPath.Root[relative];
				Assert.That(path.ToString(), Is.EqualTo("/Hello/World"));
				Assert.That(path.IsAbsolute, Is.True);
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0], Is.EqualTo(new FdbPathSegment("Hello")));
				Assert.That(path[1], Is.EqualTo(new FdbPathSegment("World")));
			}
			{
				var absolute = FdbPath.Root["Hello"]["World"];
				Assert.That(() => FdbPath.Root["Foo"]["Bar"][absolute], Throws.InstanceOf<InvalidOperationException>());
			}
		}

		[Test]
		public void Test_FdbPath_Long_Paths()
		{
			{
				var path = FdbPath.Root["This"]["Is"]["A"]["Very"]["Long"]["Path"]["Name"]["That"]["Does"]["Not"]["Fit"]["In"]["The"]["Default"]["Initial"]["Buffer"]["Size"];
				Assert.That(path.ToString(), Is.EqualTo("/This/Is/A/Very/Long/Path/Name/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size"));
				Assert.That(path.ToString("N"), Is.EqualTo("/This/Is/A/Very/Long/Path/Name/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size"));

				Assert.That($"{path}", Is.EqualTo("/This/Is/A/Very/Long/Path/Name/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size"));
				Assert.That($"{path:N}", Is.EqualTo("/This/Is/A/Very/Long/Path/Name/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size"));
			}
			{
				var path = FdbPath.Root["This", "Is"]["An"]["Even"]["Longer"]["Path", "Name"]["That"]["Does"]["Not"]["Fit"]["In"]["The"]["Default"]["Initial"]["Buffer"]["Size"]["With"]["Custom", "LayerId"];

				Assert.That(path.ToString(), Is.EqualTo("/This[Is]/An/Even/Longer/Path[Name]/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size/With/Custom[LayerId]"));
				Assert.That(path.ToString("N"), Is.EqualTo("/This/An/Even/Longer/Path/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size/With/Custom"));

				Assert.That($"{path}", Is.EqualTo("/This[Is]/An/Even/Longer/Path[Name]/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size/With/Custom[LayerId]"));
				Assert.That($"{path:N}", Is.EqualTo("/This/An/Even/Longer/Path/That/Does/Not/Fit/In/The/Default/Initial/Buffer/Size/With/Custom"));
			}
		}

	}

}
