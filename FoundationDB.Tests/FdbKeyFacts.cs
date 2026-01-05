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

// ReSharper disable VariableLengthStringHexEscapeSequence
// ReSharper disable EqualExpressionComparison
#pragma warning disable CS1718 // Comparison made to same variable

namespace FoundationDB.Client.Tests
{

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbKeyFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbRawKey_Basics()
		{
			FdbRawKey hello = FdbKey.FromBytes("hello"u8);
			FdbRawKey world = FdbKey.FromBytes("world"u8);
			FdbRawKey tuple = FdbKey.FromBytes(TuPack.EncodeKey("hello", 123, "world"));

			{ // Empty
				var k = FdbKey.FromBytes(Slice.Empty);
				Assert.That(k.Data, Is.EqualTo(Slice.Empty));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.Empty));

				Assert.That(k, Is.EqualTo(new FdbRawKey(Slice.Empty)));
				Assert.That(k, Is.EqualTo(Slice.Empty));

				Assert.That(k, Is.Not.EqualTo(Slice.Nil));
				Assert.That(k, Is.Not.EqualTo(hello));

				Assert.That(k, Is.EqualTo((object) new FdbRawKey(Slice.Empty)));
				Assert.That(k, Is.EqualTo((object) Slice.Empty));

				Assert.That(k.CompareTo(Slice.Empty), Is.Zero);

				Assert.That(k.ToString(), Is.EqualTo("<empty>"));
				Assert.That(k.ToString("X"), Is.EqualTo(""));
				Assert.That(k.ToString("K"), Is.EqualTo("<empty>"));
				Assert.That(k.ToString("B"), Is.EqualTo("<empty>"));
				Assert.That(k.ToString("E"), Is.EqualTo("<empty>"));
				Assert.That(k.ToString("P"), Is.EqualTo("<empty>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbRawKey('')"));

				Assert.That($"***{k}$$$", Is.EqualTo("***<empty>$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***$$$"));
				Assert.That($"***{k:K}$$$", Is.EqualTo("***<empty>$$$"));
				Assert.That($"***{k:B}$$$", Is.EqualTo("***<empty>$$$"));
				Assert.That($"***{k:E}$$$", Is.EqualTo("***<empty>$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***<empty>$$$"));
			}
			{ // Raw
				var k = FdbKey.FromBytes("hello"u8);
				Assert.That(k.Data, Is.EqualTo(hello));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(hello));
				Assert.That(k.ToSlice().Array, Is.SameAs(k.Data.Array), "Should expose the wrapped slice");

				Assert.That(k, Is.EqualTo(hello));
				Assert.That(k, Is.Not.EqualTo(Slice.Nil));
				Assert.That(k, Is.Not.EqualTo(Slice.Empty));
				Assert.That(k, Is.Not.EqualTo(world));

				Assert.That(k.CompareTo(hello), Is.Zero);
				Assert.That(k.CompareTo(Slice.Empty), Is.GreaterThan(0));
				Assert.That(k.CompareTo(world), Is.LessThan(0));

				Assert.That(k.ToString(), Is.EqualTo("hello"));
				Assert.That(k.ToString("X"), Is.EqualTo("68 65 6C 6C 6F"));
				Assert.That(k.ToString("x"), Is.EqualTo("68 65 6c 6c 6f"));
				Assert.That(k.ToString("K"), Is.EqualTo("hello"));
				Assert.That(k.ToString("B"), Is.EqualTo("hello"));
				Assert.That(k.ToString("E"), Is.EqualTo("hello"));
				Assert.That(k.ToString("P"), Is.EqualTo("hello"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbRawKey('hello')"));

				Assert.That($"***{k}$$$", Is.EqualTo("***hello$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***68 65 6C 6C 6F$$$"));
				Assert.That($"***{k:x}$$$", Is.EqualTo("***68 65 6c 6c 6f$$$"));
				Assert.That($"***{k:K}$$$", Is.EqualTo("***hello$$$"));
				Assert.That($"***{k:B}$$$", Is.EqualTo("***hello$$$"));
				Assert.That($"***{k:E}$$$", Is.EqualTo("***hello$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***hello$$$"));
			}

			{ // Tuple
				var k = FdbKey.FromBytes(TuPack.EncodeKey("hello", 123, "world"));
				Assert.That(k.Data, Is.EqualTo(tuple));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(tuple));
				Assert.That(k.ToSlice().Array, Is.SameAs(k.Data.Array), "Should expose the wrapped slice");

				Assert.That(k, Is.EqualTo(tuple));
				Assert.That(k, Is.Not.EqualTo(Slice.Nil));
				Assert.That(k, Is.Not.EqualTo(Slice.Empty));
				Assert.That(k, Is.Not.EqualTo(hello));
				Assert.That(k, Is.Not.EqualTo(world));

				Assert.That(k.CompareTo(tuple), Is.Zero);
				Assert.That(k.CompareTo(Slice.Empty), Is.GreaterThan(0));
				Assert.That(k.CompareTo(hello), Is.LessThan(0));
				Assert.That(k.CompareTo(world), Is.LessThan(0));

				Assert.That(k.ToString(), Is.EqualTo("(\"hello\", 123, \"world\")"));
				Assert.That(k.ToString("X"), Is.EqualTo("02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00"));
				Assert.That(k.ToString("x"), Is.EqualTo("02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00"));
				Assert.That(k.ToString("K"), Is.EqualTo("(\"hello\", 123, \"world\")"));
				Assert.That(k.ToString("B"), Is.EqualTo("(\"hello\", 123, \"world\")"));
				Assert.That(k.ToString("E"), Is.EqualTo("(\"hello\", 123, \"world\")"));
				Assert.That(k.ToString("P"), Is.EqualTo("(\"hello\", 123, \"world\")"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbRawKey(<02>hello<00><15>{<02>world<00>)"));

				Assert.That($"***{k}$$$", Is.EqualTo("***(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00$$$"));
				Assert.That($"***{k:x}$$$", Is.EqualTo("***02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00$$$"));
				Assert.That($"***{k:K}$$$", Is.EqualTo("***(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k:B}$$$", Is.EqualTo("***(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k:E}$$$", Is.EqualTo("***(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k:G}$$$", Is.EqualTo("***FdbRawKey(<02>hello<00><15>{<02>world<00>)$$$"));
			}
		}

		[Test]
		public void Test_FdbRawKey_Encoding()
		{
			static void Verify(Slice value)
			{
				{ // Slice
					var rawKey = value;

					var key = FdbKey.FromBytes(rawKey);
					Assert.That(key.ToSlice(), Is.EqualTo(rawKey));
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawKey));
					Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Count));

					Assert.That(((IFdbKey) key).GetSubspace(), Is.Null);
				}
				{ // byte[]
					var rawKey = value.ToArray();

					var key = FdbKey.FromBytes(rawKey);
					Assert.That(key.ToSlice().ToArray(), Is.EqualTo(rawKey));
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawKey));
					Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Length));
					Assert.That(((IFdbKey) key).GetSubspace(), Is.Null);
				}
			}

			Verify(Slice.FromBytes("Hello, World!"u8));
		}

		[Test]
		public void Test_FdbTupleKey_Basics()
		{
			var subspace = GetSubspace(FdbPath.Absolute("Foo", "Bar"), STuple.Create(42));
			var other = GetSubspace(STuple.Create(37));
			var child = GetSubspace(STuple.Create(42, "hello"));

			var g = Guid.Parse("df341c73-c2f1-4159-9482-f2263bef9cf8");
			var now = new DateTime(638893026362102287L);
			var vs = VersionStamp.FromUuid80(Uuid80.FromUpper64Lower16(0x6f08a8baa35c47ea, 0x148e));

			var someBytes = Slice.FromBytes("SomeBytes"u8);

			var k1 = subspace.Key("hello");
			var k2 = subspace.Key("hello", 123);
			var k3 = subspace.Key("hello", 123, "world");
			var k4 = subspace.Key("hello", 123, "world", true);
			var k5 = subspace.Key("hello", 123, "world", true, Math.PI);
			var k6 = subspace.Key("hello", 123, "world", true, Math.PI, g);
			var k7 = subspace.Key("hello", 123, "world", true, Math.PI, g, now);
			var k8 = subspace.Key("hello", 123, "world", true, Math.PI, g, now, vs);

			var kv1 = subspace.Tuple((IVarTuple) STuple.Create("hello"));
			var kv2 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123));
			var kv3 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world"));
			var kv4 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true));
			var kv5 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI));
			var kv6 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI, g));
			var kv7 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI, g, now));
			var kv8 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI, g, now, vs));

			Assert.Multiple(() =>
			{ // T1
				Log($"# {k1}");
				Assert.That(k1.Subspace, Is.SameAs(subspace));
				Assert.That(k1.Item1, Is.EqualTo("hello"));
				Assert.That(STuple.Create(k1.Item1), Is.EqualTo(kv1.Items));

				Assert.That(k1.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello")));
				Assert.That(kv1.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello")));

				#region Formatting...

				Assert.That(k1.ToString(), Is.EqualTo("…(\"hello\",)"));
				Assert.That(k1.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00"));
				Assert.That(k1.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00"));
				Assert.That(k1.ToString("K"), Is.EqualTo("(42, \"hello\")"));
				Assert.That(k1.ToString("B"), Is.EqualTo("(42, \"hello\")"));
				Assert.That(k1.ToString("E"), Is.EqualTo("(42, \"hello\")"));
				Assert.That(k1.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\",)"));
				Assert.That(k1.ToString("G"), Is.EqualTo("FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",))"));

				Assert.That($"***{k1}$$$", Is.EqualTo("***…(\"hello\",)$$$"));
				Assert.That($"***{k1:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00$$$"));
				Assert.That($"***{k1:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00$$$"));
				Assert.That($"***{k1:K}$$$", Is.EqualTo("***(42, \"hello\")$$$"));
				Assert.That($"***{k1:B}$$$", Is.EqualTo("***(42, \"hello\")$$$"));
				Assert.That($"***{k1:E}$$$", Is.EqualTo("***(42, \"hello\")$$$"));
				Assert.That($"***{k1:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\",)$$$"));
				Assert.That($"***{k1:G}$$$", Is.EqualTo("***FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k1.Equals(k1), Is.True);
				Assert.That(k1.Equals(kv1), Is.True);
				Assert.That(k1.Equals(subspace.Key("hello")), Is.True);
				Assert.That(k1.FastEqualTo(child.Key()), Is.True);
				Assert.That(k1.Equals(TuPack.EncodeKey(42, "hello")), Is.True);

				Assert.That(k1.FastEqualTo(k2), Is.False);
				Assert.That(k1.Equals(kv2), Is.False);
				Assert.That(k1.Equals(other.Key("hello")), Is.False);
				Assert.That(k1.Equals(subspace.Key("world")), Is.False);

				Assert.That(k1 == k1, Is.True);
				Assert.That(k1 != k1, Is.False);
				Assert.That(k1 == kv1, Is.True);
				Assert.That(k1 != kv1, Is.False);
				Assert.That(k1 == k1.ToSlice(), Is.True);
				Assert.That(k1 != k1.ToSlice(), Is.False);
				Assert.That(k1 == someBytes, Is.False);
				Assert.That(k1 != someBytes, Is.True);

				Assert.That(k1, Is.LessThanOrEqualTo(k1));
				Assert.That(k1, Is.LessThanOrEqualTo(k2));
				Assert.That(k1, Is.LessThanOrEqualTo(k3));
				Assert.That(k1, Is.LessThanOrEqualTo(k4));
				Assert.That(k1, Is.LessThanOrEqualTo(k5));
				Assert.That(k1, Is.LessThanOrEqualTo(k6));
				Assert.That(k1, Is.LessThanOrEqualTo(k7));
				Assert.That(k1, Is.LessThanOrEqualTo(k8));

				Assert.That(k1, Is.Not.LessThan(k1));
				Assert.That(k1, Is.LessThan(k2));
				Assert.That(k1, Is.LessThan(k3));
				Assert.That(k1, Is.LessThan(k4));
				Assert.That(k1, Is.LessThan(k5));
				Assert.That(k1, Is.LessThan(k6));
				Assert.That(k1, Is.LessThan(k7));
				Assert.That(k1, Is.LessThan(k8));

				Assert.That(k1, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k2));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k3));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k4));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k5));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k6));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k7));
				Assert.That(k1, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k1, Is.Not.GreaterThan(k1));
				Assert.That(k1, Is.Not.GreaterThan(k2));
				Assert.That(k1, Is.Not.GreaterThan(k3));
				Assert.That(k1, Is.Not.GreaterThan(k4));
				Assert.That(k1, Is.Not.GreaterThan(k5));
				Assert.That(k1, Is.Not.GreaterThan(k6));
				Assert.That(k1, Is.Not.GreaterThan(k7));
				Assert.That(k1, Is.Not.GreaterThan(k8));

				#endregion
			});
			Assert.Multiple(() =>
			{ // T1, T2
				Log($"# {k2}");
				Assert.That(k2.Subspace, Is.SameAs(subspace));
				Assert.That(k2.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k2.Items.Item2, Is.EqualTo(123));
				Assert.That(k2.Items, Is.EqualTo(kv2.Items));

				Assert.That(k2.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123)));
				Assert.That(kv2.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123)));

				#region Formatting...

				Assert.That(k2.ToString(), Is.EqualTo("…(\"hello\", 123)"));
				Assert.That(k2.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B"));
				Assert.That(k2.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b"));
				Assert.That(k2.ToString("K"), Is.EqualTo("(42, \"hello\", 123)"));
				Assert.That(k2.ToString("B"), Is.EqualTo("(42, \"hello\", 123)"));
				Assert.That(k2.ToString("E"), Is.EqualTo("(42, \"hello\", 123)"));
				Assert.That(k2.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123)"));
				Assert.That(k2.ToString("G"), Is.EqualTo("FdbTupleKey<string, int>(Subspace=/Foo/Bar, Items=(\"hello\", 123))"));

				Assert.That($"***{k2}$$$", Is.EqualTo("***…(\"hello\", 123)$$$"));
				Assert.That($"***{k2:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B$$$"));
				Assert.That($"***{k2:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b$$$"));
				Assert.That($"***{k2:K}$$$", Is.EqualTo("***(42, \"hello\", 123)$$$"));
				Assert.That($"***{k2:B}$$$", Is.EqualTo("***(42, \"hello\", 123)$$$"));
				Assert.That($"***{k2:E}$$$", Is.EqualTo("***(42, \"hello\", 123)$$$"));
				Assert.That($"***{k2:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123)$$$"));
				Assert.That($"***{k2:G}$$$", Is.EqualTo("***FdbTupleKey<string, int>(Subspace=/Foo/Bar, Items=(\"hello\", 123))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k2.Equals(k2), Is.True);
				Assert.That(k2.Equals(kv2), Is.True);
				Assert.That(k2.Equals(subspace.Key("hello", 123)), Is.True);
				Assert.That(k2.Equals(TuPack.EncodeKey(42, "hello", 123)), Is.True);
				Assert.That(k2.FastEqualTo(child.Key(123)), Is.True);
				Assert.That(k2.FastEqualTo(subspace.Bytes(TuPack.EncodeKey("hello", 123))), Is.True);
				Assert.That(k2.FastEqualTo(subspace.Key("hello").Bytes(TuPack.EncodeKey(123))), Is.True);

				Assert.That(k2.Equals(other.Key("hello", 123)), Is.False);
				Assert.That(k2.Equals(subspace.Key("world", 123)), Is.False);
				Assert.That(k2.Equals(subspace.Key("hello", 456)), Is.False);

				Assert.That(k2 == k2, Is.True);
				Assert.That(k2 != k2, Is.False);
				Assert.That(k2 == kv2, Is.True);
				Assert.That(k2 != kv2, Is.False);
				Assert.That(k2 == k2.ToSlice(), Is.True);
				Assert.That(k2 != k2.ToSlice(), Is.False);
				Assert.That(k2 == someBytes, Is.False);
				Assert.That(k2 != someBytes, Is.True);

				Assert.That(k2, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k2, Is.LessThanOrEqualTo(k2));
				Assert.That(k2, Is.LessThanOrEqualTo(k3));
				Assert.That(k2, Is.LessThanOrEqualTo(k4));
				Assert.That(k2, Is.LessThanOrEqualTo(k5));
				Assert.That(k2, Is.LessThanOrEqualTo(k6));
				Assert.That(k2, Is.LessThanOrEqualTo(k7));
				Assert.That(k2, Is.LessThanOrEqualTo(k8));

				Assert.That(k2, Is.Not.LessThan(k1));
				Assert.That(k2, Is.Not.LessThan(k2));
				Assert.That(k2, Is.LessThan(k3));
				Assert.That(k2, Is.LessThan(k4));
				Assert.That(k2, Is.LessThan(k5));
				Assert.That(k2, Is.LessThan(k6));
				Assert.That(k2, Is.LessThan(k7));
				Assert.That(k2, Is.LessThan(k8));

				Assert.That(k2, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k2, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k2, Is.Not.GreaterThanOrEqualTo(k3));
				Assert.That(k2, Is.Not.GreaterThanOrEqualTo(k4));
				Assert.That(k2, Is.Not.GreaterThanOrEqualTo(k5));
				Assert.That(k2, Is.Not.GreaterThanOrEqualTo(k6));
				Assert.That(k2, Is.Not.GreaterThanOrEqualTo(k7));
				Assert.That(k2, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k2, Is.GreaterThan(k1));
				Assert.That(k2, Is.Not.GreaterThan(k2));
				Assert.That(k2, Is.Not.GreaterThan(k3));
				Assert.That(k2, Is.Not.GreaterThan(k4));
				Assert.That(k2, Is.Not.GreaterThan(k5));
				Assert.That(k2, Is.Not.GreaterThan(k6));
				Assert.That(k2, Is.Not.GreaterThan(k7));
				Assert.That(k2, Is.Not.GreaterThan(k8));

				#endregion
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3
				Log($"# {k3}");
				Assert.That(k3.Subspace, Is.SameAs(subspace));
				Assert.That(k3.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k3.Items.Item2, Is.EqualTo(123));
				Assert.That(k3.Items.Item3, Is.EqualTo("world"));
				Assert.That(k3.Items, Is.EqualTo(kv3.Items));

				Assert.That(k3.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world")));
				Assert.That(kv3.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world")));

				#region Formatting...

				Assert.That(k3.ToString(), Is.EqualTo("…(\"hello\", 123, \"world\")"));
				Assert.That(k3.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00"));
				Assert.That(k3.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00"));
				Assert.That(k3.ToString("K"), Is.EqualTo("(42, \"hello\", 123, \"world\")"));
				Assert.That(k3.ToString("B"), Is.EqualTo("(42, \"hello\", 123, \"world\")"));
				Assert.That(k3.ToString("E"), Is.EqualTo("(42, \"hello\", 123, \"world\")"));
				Assert.That(k3.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123, \"world\")"));
				Assert.That(k3.ToString("G"), Is.EqualTo("FdbTupleKey<string, int, string>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\"))"));

				Assert.That($"***{k3}$$$", Is.EqualTo("***…(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k3:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00$$$"));
				Assert.That($"***{k3:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00$$$"));
				Assert.That($"***{k3:K}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k3:B}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k3:E}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k3:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123, \"world\")$$$"));
				Assert.That($"***{k3:G}$$$", Is.EqualTo("***FdbTupleKey<string, int, string>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\"))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k3.Equals(k3), Is.True);
				Assert.That(k3.Equals(kv3), Is.True);
				Assert.That(k3.Equals(subspace.Key("hello", 123, "world")), Is.True);

				Assert.That(k3, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k3, Is.Not.LessThanOrEqualTo(k2));
				Assert.That(k3, Is.LessThanOrEqualTo(k3));
				Assert.That(k3, Is.LessThanOrEqualTo(k4));
				Assert.That(k3, Is.LessThanOrEqualTo(k5));
				Assert.That(k3, Is.LessThanOrEqualTo(k6));
				Assert.That(k3, Is.LessThanOrEqualTo(k7));
				Assert.That(k3, Is.LessThanOrEqualTo(k8));

				Assert.That(k3, Is.Not.LessThan(k1));
				Assert.That(k3, Is.Not.LessThan(k2));
				Assert.That(k3, Is.Not.LessThan(k3));
				Assert.That(k3, Is.LessThan(k4));
				Assert.That(k3, Is.LessThan(k5));
				Assert.That(k3, Is.LessThan(k6));
				Assert.That(k3, Is.LessThan(k7));
				Assert.That(k3, Is.LessThan(k8));

				Assert.That(k3, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k3, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k3, Is.GreaterThanOrEqualTo(k3));
				Assert.That(k3, Is.Not.GreaterThanOrEqualTo(k4));
				Assert.That(k3, Is.Not.GreaterThanOrEqualTo(k5));
				Assert.That(k3, Is.Not.GreaterThanOrEqualTo(k6));
				Assert.That(k3, Is.Not.GreaterThanOrEqualTo(k7));
				Assert.That(k3, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k3, Is.GreaterThan(k1));
				Assert.That(k3, Is.GreaterThan(k2));
				Assert.That(k3, Is.Not.GreaterThan(k3));
				Assert.That(k3, Is.Not.GreaterThan(k4));
				Assert.That(k3, Is.Not.GreaterThan(k5));
				Assert.That(k3, Is.Not.GreaterThan(k6));
				Assert.That(k3, Is.Not.GreaterThan(k7));
				Assert.That(k3, Is.Not.GreaterThan(k8));

				#endregion

			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4
				Log($"# {k4}");
				Assert.That(k4.Subspace, Is.SameAs(subspace));
				Assert.That(k4.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k4.Items.Item2, Is.EqualTo(123));
				Assert.That(k4.Items.Item3, Is.EqualTo("world"));
				Assert.That(k4.Items.Item4, Is.True);
				Assert.That(k4.Items, Is.EqualTo(kv4.Items));

				Assert.That(k4.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true)));
				Assert.That(kv4.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true)));

				#region Formatting...

				Assert.That(k4.ToString(), Is.EqualTo("…(\"hello\", 123, \"world\", true)"));
				Assert.That(k4.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27"));
				Assert.That(k4.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27"));
				Assert.That(k4.ToString("K"), Is.EqualTo("(42, \"hello\", 123, \"world\", true)"));
				Assert.That(k4.ToString("B"), Is.EqualTo("(42, \"hello\", 123, \"world\", true)"));
				Assert.That(k4.ToString("E"), Is.EqualTo("(42, \"hello\", 123, \"world\", true)"));
				Assert.That(k4.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123, \"world\", true)"));
				Assert.That(k4.ToString("G"), Is.EqualTo("FdbTupleKey<string, int, string, bool>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true))"));

				Assert.That($"***{k4}$$$", Is.EqualTo("***…(\"hello\", 123, \"world\", true)$$$"));
				Assert.That($"***{k4:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27$$$"));
				Assert.That($"***{k4:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27$$$"));
				Assert.That($"***{k4:K}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true)$$$"));
				Assert.That($"***{k4:B}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true)$$$"));
				Assert.That($"***{k4:E}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true)$$$"));
				Assert.That($"***{k4:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123, \"world\", true)$$$"));
				Assert.That($"***{k4:G}$$$", Is.EqualTo("***FdbTupleKey<string, int, string, bool>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k4.Equals(k4), Is.True);
				Assert.That(k4.Equals(kv4), Is.True);
				Assert.That(k4.Equals(subspace.Key("hello", 123, "world", true)), Is.True);

				Assert.That(k4, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k4, Is.Not.LessThanOrEqualTo(k2));
				Assert.That(k4, Is.Not.LessThanOrEqualTo(k3));
				Assert.That(k4, Is.LessThanOrEqualTo(k4));
				Assert.That(k4, Is.LessThanOrEqualTo(k5));
				Assert.That(k4, Is.LessThanOrEqualTo(k6));
				Assert.That(k4, Is.LessThanOrEqualTo(k7));
				Assert.That(k4, Is.LessThanOrEqualTo(k8));

				Assert.That(k4, Is.Not.LessThan(k1));
				Assert.That(k4, Is.Not.LessThan(k2));
				Assert.That(k4, Is.Not.LessThan(k3));
				Assert.That(k4, Is.Not.LessThan(k4));
				Assert.That(k4, Is.LessThan(k5));
				Assert.That(k4, Is.LessThan(k6));
				Assert.That(k4, Is.LessThan(k7));
				Assert.That(k4, Is.LessThan(k8));

				Assert.That(k4, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k4, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k4, Is.GreaterThanOrEqualTo(k3));
				Assert.That(k4, Is.GreaterThanOrEqualTo(k4));
				Assert.That(k4, Is.Not.GreaterThanOrEqualTo(k5));
				Assert.That(k4, Is.Not.GreaterThanOrEqualTo(k6));
				Assert.That(k4, Is.Not.GreaterThanOrEqualTo(k7));
				Assert.That(k4, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k4, Is.GreaterThan(k1));
				Assert.That(k4, Is.GreaterThan(k2));
				Assert.That(k4, Is.GreaterThan(k3));
				Assert.That(k4, Is.Not.GreaterThan(k4));
				Assert.That(k4, Is.Not.GreaterThan(k5));
				Assert.That(k4, Is.Not.GreaterThan(k6));
				Assert.That(k4, Is.Not.GreaterThan(k7));
				Assert.That(k4, Is.Not.GreaterThan(k8));

				#endregion
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5
				Log($"# {k5}");
				Assert.That(k5.Subspace, Is.SameAs(subspace));
				Assert.That(k5.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k5.Items.Item2, Is.EqualTo(123));
				Assert.That(k5.Items.Item3, Is.EqualTo("world"));
				Assert.That(k5.Items.Item4, Is.True);
				Assert.That(k5.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k5.Items, Is.EqualTo(kv5.Items));

				Assert.That(k5.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI)));
				Assert.That(kv5.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI)));

				#region Formatting...

				Assert.That(k5.ToString(), Is.EqualTo("…(\"hello\", 123, \"world\", true, 3.141592653589793)"));
				Assert.That(k5.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18"));
				Assert.That(k5.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18"));
				Assert.That(k5.ToString("K"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793)"));
				Assert.That(k5.ToString("B"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793)"));
				Assert.That(k5.ToString("E"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793)"));
				Assert.That(k5.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793)"));
				Assert.That(k5.ToString("G"), Is.EqualTo("FdbTupleKey<string, int, string, bool, double>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793))"));

				Assert.That($"***{k5}$$$", Is.EqualTo("***…(\"hello\", 123, \"world\", true, 3.141592653589793)$$$"));
				Assert.That($"***{k5:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18$$$"));
				Assert.That($"***{k5:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18$$$"));
				Assert.That($"***{k5:K}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793)$$$"));
				Assert.That($"***{k5:B}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793)$$$"));
				Assert.That($"***{k5:E}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793)$$$"));
				Assert.That($"***{k5:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793)$$$"));
				Assert.That($"***{k5:G}$$$", Is.EqualTo("***FdbTupleKey<string, int, string, bool, double>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k5.Equals(k5), Is.True);
				Assert.That(k5.Equals(kv5), Is.True);
				Assert.That(k5.Equals(subspace.Key("hello", 123, "world", true, Math.PI)), Is.True);

				Assert.That(k5, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k5, Is.Not.LessThanOrEqualTo(k2));
				Assert.That(k5, Is.Not.LessThanOrEqualTo(k3));
				Assert.That(k5, Is.Not.LessThanOrEqualTo(k4));
				Assert.That(k5, Is.LessThanOrEqualTo(k5));
				Assert.That(k5, Is.LessThanOrEqualTo(k6));
				Assert.That(k5, Is.LessThanOrEqualTo(k7));
				Assert.That(k5, Is.LessThanOrEqualTo(k8));

				Assert.That(k5, Is.Not.LessThan(k1));
				Assert.That(k5, Is.Not.LessThan(k2));
				Assert.That(k5, Is.Not.LessThan(k3));
				Assert.That(k5, Is.Not.LessThan(k4));
				Assert.That(k5, Is.Not.LessThan(k5));
				Assert.That(k5, Is.LessThan(k6));
				Assert.That(k5, Is.LessThan(k7));
				Assert.That(k5, Is.LessThan(k8));

				Assert.That(k5, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k5, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k5, Is.GreaterThanOrEqualTo(k3));
				Assert.That(k5, Is.GreaterThanOrEqualTo(k4));
				Assert.That(k5, Is.GreaterThanOrEqualTo(k5));
				Assert.That(k5, Is.Not.GreaterThanOrEqualTo(k6));
				Assert.That(k5, Is.Not.GreaterThanOrEqualTo(k7));
				Assert.That(k5, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k5, Is.GreaterThan(k1));
				Assert.That(k5, Is.GreaterThan(k2));
				Assert.That(k5, Is.GreaterThan(k3));
				Assert.That(k5, Is.GreaterThan(k4));
				Assert.That(k5, Is.Not.GreaterThan(k5));
				Assert.That(k5, Is.Not.GreaterThan(k6));
				Assert.That(k5, Is.Not.GreaterThan(k7));
				Assert.That(k5, Is.Not.GreaterThan(k8));

				#endregion
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5, T6
				Log($"# {k6}");
				Assert.That(k6.Subspace, Is.SameAs(subspace));
				Assert.That(k6.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k6.Items.Item2, Is.EqualTo(123));
				Assert.That(k6.Items.Item3, Is.EqualTo("world"));
				Assert.That(k6.Items.Item4, Is.True);
				Assert.That(k6.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k6.Items.Item6, Is.EqualTo(g));
				Assert.That(k6.Items, Is.EqualTo(kv6.Items));

				Assert.That(k6.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g)));
				Assert.That(kv6.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g)));

				#region Formatting...

				Assert.That(k6.ToString(), Is.EqualTo("…(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})"));
				Assert.That(k6.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18 30 DF 34 1C 73 C2 F1 41 59 94 82 F2 26 3B EF 9C F8"));
				Assert.That(k6.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18 30 df 34 1c 73 c2 f1 41 59 94 82 f2 26 3b ef 9c f8"));
				Assert.That(k6.ToString("K"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})"));
				Assert.That(k6.ToString("B"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})"));
				Assert.That(k6.ToString("E"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})"));
				Assert.That(k6.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})"));
				Assert.That(k6.ToString("G"), Is.EqualTo("FdbTupleKey<string, int, string, bool, double, Guid>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}))"));

				Assert.That($"***{k6}$$$", Is.EqualTo("***…(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})$$$"));
				Assert.That($"***{k6:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18 30 DF 34 1C 73 C2 F1 41 59 94 82 F2 26 3B EF 9C F8$$$"));
				Assert.That($"***{k6:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18 30 df 34 1c 73 c2 f1 41 59 94 82 f2 26 3b ef 9c f8$$$"));
				Assert.That($"***{k6:K}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})$$$"));
				Assert.That($"***{k6:B}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})$$$"));
				Assert.That($"***{k6:E}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})$$$"));
				Assert.That($"***{k6:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8})$$$"));
				Assert.That($"***{k6:G}$$$", Is.EqualTo("***FdbTupleKey<string, int, string, bool, double, Guid>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k6.Equals(k6), Is.True);
				Assert.That(k6.Equals(kv6), Is.True);
				Assert.That(k6.Equals(subspace.Key("hello", 123, "world", true, Math.PI, g)), Is.True);

				Assert.That(k6, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k6, Is.Not.LessThanOrEqualTo(k2));
				Assert.That(k6, Is.Not.LessThanOrEqualTo(k3));
				Assert.That(k6, Is.Not.LessThanOrEqualTo(k4));
				Assert.That(k6, Is.Not.LessThanOrEqualTo(k5));
				Assert.That(k6, Is.LessThanOrEqualTo(k6));
				Assert.That(k6, Is.LessThanOrEqualTo(k7));
				Assert.That(k6, Is.LessThanOrEqualTo(k8));

				Assert.That(k6, Is.Not.LessThan(k1));
				Assert.That(k6, Is.Not.LessThan(k2));
				Assert.That(k6, Is.Not.LessThan(k3));
				Assert.That(k6, Is.Not.LessThan(k4));
				Assert.That(k6, Is.Not.LessThan(k5));
				Assert.That(k6, Is.Not.LessThan(k6));
				Assert.That(k6, Is.LessThan(k7));
				Assert.That(k6, Is.LessThan(k8));

				Assert.That(k6, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k6, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k6, Is.GreaterThanOrEqualTo(k3));
				Assert.That(k6, Is.GreaterThanOrEqualTo(k4));
				Assert.That(k6, Is.GreaterThanOrEqualTo(k5));
				Assert.That(k6, Is.GreaterThanOrEqualTo(k6));
				Assert.That(k6, Is.Not.GreaterThanOrEqualTo(k7));
				Assert.That(k6, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k6, Is.GreaterThan(k1));
				Assert.That(k6, Is.GreaterThan(k2));
				Assert.That(k6, Is.GreaterThan(k3));
				Assert.That(k6, Is.GreaterThan(k4));
				Assert.That(k6, Is.GreaterThan(k5));
				Assert.That(k6, Is.Not.GreaterThan(k6));
				Assert.That(k6, Is.Not.GreaterThan(k7));
				Assert.That(k6, Is.Not.GreaterThan(k8));

				#endregion
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5, T6, T7
				Log($"# {k7}");
				Assert.That(k7.Subspace, Is.SameAs(subspace));
				Assert.That(k7.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k7.Items.Item2, Is.EqualTo(123));
				Assert.That(k7.Items.Item3, Is.EqualTo("world"));
				Assert.That(k7.Items.Item4, Is.True);
				Assert.That(k7.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k7.Items.Item6, Is.EqualTo(g));
				Assert.That(k7.Items.Item7, Is.EqualTo(now));
				Assert.That(k7.Items, Is.EqualTo(kv7.Items));

				Assert.That(k7.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now)));
				Assert.That(kv7.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now)));

				#region Formatting...

				Assert.That(k7.ToString(), Is.EqualTo("…(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\")"));
				Assert.That(k7.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18 30 DF 34 1C 73 C2 F1 41 59 94 82 F2 26 3B EF 9C F8 21 C0 D3 D2 5C 06 DD D5 0F"));
				Assert.That(k7.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18 30 df 34 1c 73 c2 f1 41 59 94 82 f2 26 3b ef 9c f8 21 c0 d3 d2 5c 06 dd d5 0f"));
				Assert.That(k7.ToString("K"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987)"));
				Assert.That(k7.ToString("B"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987)"));
				Assert.That(k7.ToString("E"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987)"));
				Assert.That(k7.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\")"));
				Assert.That(k7.ToString("G"), Is.EqualTo("FdbTupleKey<string, int, string, bool, double, Guid, DateTime>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\"))"));

				Assert.That($"***{k7}$$$", Is.EqualTo("***…(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\")$$$"));
				Assert.That($"***{k7:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18 30 DF 34 1C 73 C2 F1 41 59 94 82 F2 26 3B EF 9C F8 21 C0 D3 D2 5C 06 DD D5 0F$$$"));
				Assert.That($"***{k7:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18 30 df 34 1c 73 c2 f1 41 59 94 82 f2 26 3b ef 9c f8 21 c0 d3 d2 5c 06 dd d5 0f$$$"));
				Assert.That($"***{k7:K}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987)$$$"));
				Assert.That($"***{k7:B}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987)$$$"));
				Assert.That($"***{k7:E}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987)$$$"));
				Assert.That($"***{k7:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\")$$$"));
				Assert.That($"***{k7:G}$$$", Is.EqualTo("***FdbTupleKey<string, int, string, bool, double, Guid, DateTime>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\"))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k7.Equals(k7), Is.True);
				Assert.That(k7.Equals(kv7), Is.True);
				Assert.That(k7.Equals(subspace.Key("hello", 123, "world", true, Math.PI, g, now)), Is.True);

				Assert.That(k7, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k7, Is.Not.LessThanOrEqualTo(k2));
				Assert.That(k7, Is.Not.LessThanOrEqualTo(k3));
				Assert.That(k7, Is.Not.LessThanOrEqualTo(k4));
				Assert.That(k7, Is.Not.LessThanOrEqualTo(k5));
				Assert.That(k7, Is.Not.LessThanOrEqualTo(k6));
				Assert.That(k7, Is.LessThanOrEqualTo(k7));
				Assert.That(k7, Is.LessThanOrEqualTo(k8));

				Assert.That(k7, Is.Not.LessThan(k1));
				Assert.That(k7, Is.Not.LessThan(k2));
				Assert.That(k7, Is.Not.LessThan(k3));
				Assert.That(k7, Is.Not.LessThan(k4));
				Assert.That(k7, Is.Not.LessThan(k5));
				Assert.That(k7, Is.Not.LessThan(k6));
				Assert.That(k7, Is.Not.LessThan(k7));
				Assert.That(k7, Is.LessThan(k8));

				Assert.That(k7, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k7, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k7, Is.GreaterThanOrEqualTo(k3));
				Assert.That(k7, Is.GreaterThanOrEqualTo(k4));
				Assert.That(k7, Is.GreaterThanOrEqualTo(k5));
				Assert.That(k7, Is.GreaterThanOrEqualTo(k6));
				Assert.That(k7, Is.GreaterThanOrEqualTo(k7));
				Assert.That(k7, Is.Not.GreaterThanOrEqualTo(k8));

				Assert.That(k7, Is.GreaterThan(k1));
				Assert.That(k7, Is.GreaterThan(k2));
				Assert.That(k7, Is.GreaterThan(k3));
				Assert.That(k7, Is.GreaterThan(k4));
				Assert.That(k7, Is.GreaterThan(k5));
				Assert.That(k7, Is.GreaterThan(k6));
				Assert.That(k7, Is.Not.GreaterThan(k7));
				Assert.That(k7, Is.Not.GreaterThan(k8));

				#endregion
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5, T6, T7, T8
				Log($"# {k8}");
				Assert.That(k8.Subspace, Is.SameAs(subspace));
				Assert.That(k8.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k8.Items.Item2, Is.EqualTo(123));
				Assert.That(k8.Items.Item3, Is.EqualTo("world"));
				Assert.That(k8.Items.Item4, Is.True);
				Assert.That(k8.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k8.Items.Item6, Is.EqualTo(g));
				Assert.That(k8.Items.Item7, Is.EqualTo(now));
				Assert.That(k8.Items.Item8, Is.EqualTo(vs));
				Assert.That(k8.Items, Is.EqualTo(kv8.Items));

				Assert.That(k8.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now, vs)));
				Assert.That(kv8.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now, vs)));

				#region Formatting...

				Assert.That(k8.ToString(), Is.EqualTo("…(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\", @6f08a8baa35c47ea-148e)"));
				Assert.That(k8.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18 30 DF 34 1C 73 C2 F1 41 59 94 82 F2 26 3B EF 9C F8 21 C0 D3 D2 5C 06 DD D5 0F 32 6F 08 A8 BA A3 5C 47 EA 14 8E"));
				Assert.That(k8.ToString("x"), Is.EqualTo("15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18 30 df 34 1c 73 c2 f1 41 59 94 82 f2 26 3b ef 9c f8 21 c0 d3 d2 5c 06 dd d5 0f 32 6f 08 a8 ba a3 5c 47 ea 14 8e"));
				Assert.That(k8.ToString("K"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987, @6f08a8baa35c47ea-148e)"));
				Assert.That(k8.ToString("B"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987, @6f08a8baa35c47ea-148e)"));
				Assert.That(k8.ToString("E"), Is.EqualTo("(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987, @6f08a8baa35c47ea-148e)"));
				Assert.That(k8.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\", @6f08a8baa35c47ea-148e)"));
				Assert.That(k8.ToString("G"), Is.EqualTo("FdbTupleKey<string, int, string, bool, double, Guid, DateTime, VersionStamp>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\", @6f08a8baa35c47ea-148e))"));

				Assert.That($"***{k8}$$$", Is.EqualTo("***…(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\", @6f08a8baa35c47ea-148e)$$$"));
				Assert.That($"***{k8:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 15 7B 02 77 6F 72 6C 64 00 27 21 C0 09 21 FB 54 44 2D 18 30 DF 34 1C 73 C2 F1 41 59 94 82 F2 26 3B EF 9C F8 21 C0 D3 D2 5C 06 DD D5 0F 32 6F 08 A8 BA A3 5C 47 EA 14 8E$$$"));
				Assert.That($"***{k8:x}$$$", Is.EqualTo("***15 2a 02 68 65 6c 6c 6f 00 15 7b 02 77 6f 72 6c 64 00 27 21 c0 09 21 fb 54 44 2d 18 30 df 34 1c 73 c2 f1 41 59 94 82 f2 26 3b ef 9c f8 21 c0 d3 d2 5c 06 dd d5 0f 32 6f 08 a8 ba a3 5c 47 ea 14 8e$$$"));
				Assert.That($"***{k8:K}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987, @6f08a8baa35c47ea-148e)$$$"));
				Assert.That($"***{k8:B}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987, @6f08a8baa35c47ea-148e)$$$"));
				Assert.That($"***{k8:E}$$$", Is.EqualTo("***(42, \"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, 20297.43791909987, @6f08a8baa35c47ea-148e)$$$"));
				Assert.That($"***{k8:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\", @6f08a8baa35c47ea-148e)$$$"));
				Assert.That($"***{k8:G}$$$", Is.EqualTo("***FdbTupleKey<string, int, string, bool, double, Guid, DateTime, VersionStamp>(Subspace=/Foo/Bar, Items=(\"hello\", 123, \"world\", true, 3.141592653589793, {df341c73-c2f1-4159-9482-f2263bef9cf8}, \"2025-07-28T12:30:36.2102287\", @6f08a8baa35c47ea-148e))$$$"));

				#endregion

				#region Comparisons...

				Assert.That(k8.Equals(k8), Is.True);
				Assert.That(k8.Equals(kv8), Is.True);
				Assert.That(k8.Equals(subspace.Key("hello", 123, "world", true, Math.PI, g, now, vs)), Is.True);

				Assert.That(k8, Is.Not.LessThanOrEqualTo(k1));
				Assert.That(k8, Is.Not.LessThanOrEqualTo(k2));
				Assert.That(k8, Is.Not.LessThanOrEqualTo(k3));
				Assert.That(k8, Is.Not.LessThanOrEqualTo(k4));
				Assert.That(k8, Is.Not.LessThanOrEqualTo(k5));
				Assert.That(k8, Is.Not.LessThanOrEqualTo(k6));
				Assert.That(k8, Is.Not.LessThanOrEqualTo(k7));
				Assert.That(k8, Is.LessThanOrEqualTo(k8));

				Assert.That(k8, Is.Not.LessThan(k1));
				Assert.That(k8, Is.Not.LessThan(k2));
				Assert.That(k8, Is.Not.LessThan(k3));
				Assert.That(k8, Is.Not.LessThan(k4));
				Assert.That(k8, Is.Not.LessThan(k5));
				Assert.That(k8, Is.Not.LessThan(k6));
				Assert.That(k8, Is.Not.LessThan(k7));
				Assert.That(k8, Is.Not.LessThan(k8));

				Assert.That(k8, Is.GreaterThanOrEqualTo(k1));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k2));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k3));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k4));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k5));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k6));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k7));
				Assert.That(k8, Is.GreaterThanOrEqualTo(k8));

				Assert.That(k8, Is.GreaterThan(k1));
				Assert.That(k8, Is.GreaterThan(k2));
				Assert.That(k8, Is.GreaterThan(k3));
				Assert.That(k8, Is.GreaterThan(k4));
				Assert.That(k8, Is.GreaterThan(k5));
				Assert.That(k8, Is.GreaterThan(k6));
				Assert.That(k8, Is.GreaterThan(k7));
				Assert.That(k8, Is.Not.GreaterThan(k8));

				#endregion
			});

			// Append Matrix

			Assert.Multiple(() =>
			{
				// T1
				Assert.That(k1.Key(123), Is.EqualTo(k2));
				Assert.That(k1.Key(123, "world"), Is.EqualTo(k3));
				Assert.That(k1.Key(123, "world", true), Is.EqualTo(k4));
				Assert.That(k1.Key(123, "world", true, Math.PI), Is.EqualTo(k5));
				Assert.That(k1.Key(123, "world", true, Math.PI, g), Is.EqualTo(k6));
				Assert.That(k1.Key(123, "world", true, Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k1.Key(123, "world", true, Math.PI, g, now, vs), Is.EqualTo(k8));

				// T2
				Assert.That(k2.Key("world"), Is.EqualTo(k3));
				Assert.That(k2.Key("world", true), Is.EqualTo(k4));
				Assert.That(k2.Key("world", true, Math.PI), Is.EqualTo(k5));
				Assert.That(k2.Key("world", true, Math.PI, g), Is.EqualTo(k6));
				Assert.That(k2.Key("world", true, Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k2.Key("world", true, Math.PI, g, now, vs), Is.EqualTo(k8));

				// T3
				Assert.That(k3.Key(true), Is.EqualTo(k4));
				Assert.That(k3.Key(true, Math.PI), Is.EqualTo(k5));
				Assert.That(k3.Key(true, Math.PI, g), Is.EqualTo(k6));
				Assert.That(k3.Key(true, Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k3.Key(true, Math.PI, g, now, vs), Is.EqualTo(k8));

				// T4
				Assert.That(k4.Key(Math.PI), Is.EqualTo(k5));
				Assert.That(k4.Key(Math.PI, g), Is.EqualTo(k6));
				Assert.That(k4.Key(Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k4.Key(Math.PI, g, now, vs), Is.EqualTo(k8));

				// T5
				Assert.That(k5.Key(g), Is.EqualTo(k6));
				Assert.That(k5.Key(g, now), Is.EqualTo(k7));
				Assert.That(k5.Key(g, now, vs), Is.EqualTo(k8));

				// T6
				Assert.That(k6.Key(now), Is.EqualTo(k7));
				Assert.That(k6.Key(now, vs), Is.EqualTo(k8));

				// T7
				Assert.That(k7.Key(vs), Is.EqualTo(k8));
			});
		}

		[Test]
		public void Test_FdbVarTupleKey_Encoding()
		{
			static void Verify(Slice prefix, IVarTuple items)
			{
				var subspace = KeySubspace.FromKey(prefix);
				var packed = prefix + TuPack.Pack(items);
				Log($"# {prefix:x} + {items}: -> {packed:x}");
				if (!packed.StartsWith(prefix)) throw new InvalidOperationException();

				var key = subspace.Tuple(items);

				Assert.That(key.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero, "");
				Assert.That(key.TryGetSizeHint(out int size), Is.False.WithOutput(size).Zero);

				var buffer = new byte[packed.Count + 32];

				// buffer large enough
				var chunk = buffer.AsSpan();
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(key.TryEncode(chunk, out int bytesWritten), Is.True.WithOutput(bytesWritten).EqualTo(packed.Count));
				Assert.That(chunk[..bytesWritten].ToSlice(), Is.EqualTo(packed));

				// buffer with exact size
				chunk = buffer.AsSpan(0, packed.Count);
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(key.TryEncode(chunk, out bytesWritten), Is.True.WithOutput(bytesWritten).EqualTo(packed.Count));
				Assert.That(chunk[..bytesWritten].ToSlice(), Is.EqualTo(packed));

				// buffer that is too small by 1 byte
				if (packed.Count > 0)
				{
					chunk = buffer.AsSpan(0, packed.Count - 1);
					chunk.Fill(0x55);
					buffer.AsSpan(chunk.Length).Fill(0xAA);
					Assert.That(key.TryEncode(chunk, out bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
				}

				// buffer that is about 50% the required capacity
				if (packed.Count > 2)
				{
					chunk = buffer.AsSpan(0, packed.Count / 2);
					chunk.Fill(0x55);
					buffer.AsSpan(chunk.Length).Fill(0xAA);
					Assert.That(key.TryEncode(chunk, out bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
				}

				// ToSlice()
				Assert.That(key.ToSlice(), Is.EqualTo(packed));

				// ToSliceOwner()
				var so = key.ToSlice(ArrayPool<byte>.Shared);
				Assert.That(so.Data, Is.EqualTo(packed));
				so.Dispose();
			}

			Assert.Multiple(() =>
			{
				Verify(TuPack.EncodeKey(42), STuple.Create());
				Verify(TuPack.EncodeKey(42), STuple.Create("World"));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World"));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123, Math.PI));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123, Math.PI, false));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123, Math.PI, false, DateTime.Now));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, STuple.Create("World", STuple.Create(123, Math.PI), false), DateTime.Now));
				Verify(TuPack.EncodeKey(42, 123), STuple.Create("Hello", "World"));

			});
		}

		[Test]
		public void Test_FdbTupleKey_Encoding()
		{
			static void Verify<TKey, TTuple>(IKeySubspace subspace, TKey key, TTuple items)
				where TKey : struct, IFdbKey
				where TTuple : IVarTuple
			{
				var prefix = subspace.GetPrefix();
				Log($"# {prefix:x} + {items}");

				var packedTuple = TuPack.Pack(items);
				var expected = prefix + packedTuple;
				Log($"  - expected: [{expected.Count:N0}] {expected:x}");

				if (items.Count == 0)
				{
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.Length).EqualTo(prefix.Count));
				}
				else
				{
					Assert.That(key.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				}

				var buffer = new byte[expected.Count + 32];

				// buffer large enough
				var chunk = buffer.AsSpan();
				chunk.Fill(0x55);
				Assert.That(key.TryEncode(chunk, out int bytesWritten), Is.True, $"Failed to encode key: {items}");
				Log($"  - actual  : [{bytesWritten:N0}] {Slice.FromBytes(chunk[..bytesWritten]):x}");
				if (!chunk[..bytesWritten].SequenceEqual(expected.Span))
				{
					DumpVersus(chunk[..bytesWritten], expected.Span);
					Assert.That(bytesWritten, Is.EqualTo(expected.Count), $"Encoded length mismatch: {items}");
					Assert.That(chunk[..prefix.Count].ToArray(), Is.EqualTo(prefix.ToArray()), $"Encoded key does not contains the prefix! {items}");
					Assert.That(chunk[..bytesWritten].ToArray(), Is.EqualTo(expected.ToArray()), $"Encoded key mismatch: {items}");
				}

				// check if the size hint was correct
				if (key.TryGetSizeHint(out int size))
				{
					Log($"  - sizeHint: true, {size}");
					Assert.That(size, Is.GreaterThanOrEqualTo(packedTuple.Count));
				}
				else
				{
					Log("  - sizeHint: false, 0");
					Assert.That(size, Is.Zero);
				}

				// buffer with exact size
				chunk = buffer.AsSpan(0, expected.Count);
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(key.TryEncode(chunk, out bytesWritten), Is.True, $"Failed to encode key: {items}");
				if (!chunk[..bytesWritten].SequenceEqual(expected.Span))
				{
					DumpVersus(chunk[..bytesWritten], expected.Span);
					Assert.That(bytesWritten, Is.EqualTo(expected.Count), $"Encoded length mismatch: {items}");
					Assert.That(chunk[..prefix.Count].ToArray(), Is.EqualTo(prefix.ToArray()), $"Encoded key does not contains the prefix! {items}");
					Assert.That(chunk[..bytesWritten].ToArray(), Is.EqualTo(expected.ToArray()), $"Encoded key mismatch: {items}");
				}
				Assert.That(buffer.AsSpan(chunk.Length).ContainsAnyExcept((byte) 0xAA), Is.False);

				// test all possible sizes that are not large enough
				for(int i = expected.Count - 1; i >= 0; i--)
				{
					chunk = buffer.AsSpan(0, i);
					chunk.Fill(0x55);
					buffer.AsSpan(chunk.Length).Fill(0xAA);
					Assert.That(key.TryEncode(chunk, out bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
					Assert.That(buffer.AsSpan(chunk.Length).ContainsAnyExcept((byte) 0xAA), Is.False);
				}

				// ToSlice()
				Assert.That(key.ToSlice(), Is.EqualTo(expected));

				// ToSliceOwner()
				var so = key.ToSlice(ArrayPool<byte>.Shared);
				Assert.That(so.Data, Is.EqualTo(expected));
				so.Dispose();
			}

			var subspace = KeySubspace.FromKey(TuPack.EncodeKey(42));

			var now = DateTime.Now;
			var vs = VersionStamp.Incomplete(0x1234);
			var uuid128 = Uuid128.NewUuid();
			Verify(subspace, subspace.Key(), STuple.Create());
			Verify(subspace, subspace.Key("Hello"), STuple.Create("Hello"));
			Verify(subspace, subspace.Key("Héllo", "Wörld!"), STuple.Create("Héllo", "Wörld!"));
			Verify(subspace, subspace.Key("Hello", true, "Wörld"), STuple.Create("Hello", true, "Wörld"));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123), STuple.Create("Hello", true, "Wörld", 123));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI), STuple.Create("Hello", true, "Wörld", 123, Math.PI));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI, vs), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI, vs, now), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs, now));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI, vs, now, uuid128), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs, now, uuid128));
			Verify(subspace, subspace.Key("Hello", true, STuple.Create("Wörld", STuple.Create(123, Math.PI), vs), now, uuid128), STuple.Create("Hello", true, STuple.Create("Wörld", STuple.Create(123, Math.PI), vs), now, uuid128));
		}

		[Test]
		public void Test_FdbSystemKey_Basics()
		{
			{ // 0xFF
				var k = FdbSystemKey.System;
				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.False);
				Assert.That(k.SuffixString, Is.Null);
				Assert.That(k.SuffixBytes, Is.EqualTo(Slice.Empty));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF ])));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.Empty, false)));
				Assert.That(k, Is.EqualTo(new FdbSystemKey("", false)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF ]))));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0xFF ])));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.System))));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.System))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.Empty, true)));
				Assert.That(k, Is.Not.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF, 0xFF ]))));
				Assert.That(k, Is.Not.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));
			}
			{ // 0xFF 0xFF
				var k = FdbSystemKey.Special;
				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.SuffixString, Is.Null);
				Assert.That(k.SuffixBytes, Is.EqualTo(Slice.Empty));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.Empty, true)));
				Assert.That(k, Is.EqualTo(new FdbSystemKey("", true)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF, 0xFF ]))));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.Special))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.Empty, false)));
				Assert.That(k, Is.Not.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF ]))));
				Assert.That(k, Is.Not.EqualTo(Slice.FromBytes([ 0xFF ])));
			}
			{ // System: MetadataVersion
				var k = FdbKey.ToSystemKey("/metadataVersion");

				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.False);
				Assert.That(k.SuffixString, Is.EqualTo("/metadataVersion"));
				Assert.That(k.SuffixBytes, Is.EqualTo(Slice.Nil));

				var expectedBytes = Slice.FromByteString("\xFF/metadataVersion");
				Assert.That(k.ToSlice(), Is.EqualTo(expectedBytes));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/metadataVersion"), special: false)));
				Assert.That(k, Is.EqualTo(new FdbSystemKey("/metadataVersion", special: false)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(expectedBytes)));
				Assert.That(k, Is.EqualTo(expectedBytes));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.SystemKey("/metadataVersion")))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/metadataVersion"), special: true)));
				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/metadataversion"), special: false)));
				Assert.That(k, Is.Not.EqualTo(new FdbVarTupleValue(STuple.Create(TuPackUserType.SpecialKey("/metadataVersion")))));

			}
			{ // Special: Status Json
				var k = FdbKey.ToSpecialKey(Slice.FromStringAscii("/status/json"));
				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.SuffixString, Is.Null);
				Assert.That(k.SuffixBytes, Is.EqualTo(Slice.FromStringAscii("/status/json")));

				var expectedBytes = Slice.FromByteString("\xFF\xFF/status/json");
				Assert.That(k.ToSlice(), Is.EqualTo(expectedBytes));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/status/json"), special: true)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(expectedBytes)));
				Assert.That(k, Is.EqualTo(expectedBytes));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.SpecialKey("/status/json")))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/status/json"), special: false)));
				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/status/JSON"), special: true)));
				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromStringAscii("/status/json/"), special: true)));
				Assert.That(k, Is.Not.EqualTo(new FdbVarTupleValue(STuple.Create(TuPackUserType.SystemKey("/status/json")))));

				Assert.That(k.FastEqualTo<FdbSystemKey>(new(Slice.FromStringAscii("/status/json"), special: true)), Is.True);
				Assert.That(k.FastEqualTo<FdbSystemKey>(new("/status/json", special: true)), Is.True);
				Assert.That(k.FastEqualTo<FdbRawKey>(new(expectedBytes)), Is.True);
				Assert.That(k.FastEqualTo<FdbTupleKey>(new(null, STuple.Create(TuPackUserType.SpecialKey("/status/json")))), Is.True);
			}
			{ // Special Key: Transaction Conflicting Keys...
				var ckFirst = FdbSystemKey.TransactionConflictingKeys;
				var ckHello = FdbSystemKey.TransactionConflictingKeys.Bytes(TuPack.EncodeKey("hello", 123));
				var ckWorld = FdbSystemKey.TransactionConflictingKeys.Tuple(("world", 456));
				var ckLast = FdbKey.ToSpecialKey("/transaction/conflicting_keys/\xFF");
				Log($"# {ckFirst}");
				Log($"# {ckHello}");
				Log($"# {ckWorld}");
				Log($"# {ckLast}");

				Assert.That(ckFirst.ToSlice(), Is.EqualTo(Slice.FromStringAscii("\xFF\xFF/transaction/conflicting_keys/")));
				Assert.That(ckHello.ToSlice(), Is.EqualTo(Slice.FromStringAscii("\xFF\xFF/transaction/conflicting_keys/\x02hello\x00\x15{")));
				Assert.That(ckWorld.ToSlice(), Is.EqualTo(Slice.FromStringAscii("\xFF\xFF/transaction/conflicting_keys/\x02world\x00\x16\x01\xC8")));
				Assert.That(ckLast.ToSlice(), Is.EqualTo(Slice.FromStringAscii("\xFF\xFF/transaction/conflicting_keys/\xFF")));

				Assert.That(ckFirst.ToString(), Is.EqualTo("<FF><FF>/transaction/conflicting_keys/"));
				Assert.That(ckHello.ToString(), Is.EqualTo("<FF><FF>/transaction/conflicting_keys/<02>hello<00><15>{"));
				Assert.That(ckWorld.ToString(), Is.EqualTo("<FF><FF>/transaction/conflicting_keys/.(\"world\", 456)")); //REVIEW: this is not pretty
				Assert.That(ckLast.ToString(), Is.EqualTo("<FF><FF>/transaction/conflicting_keys/<FF>"));

				Assert.That(ckFirst, Is.EqualTo(ckFirst).And.LessThan(ckHello).And.LessThan(ckWorld).And.LessThan(ckLast));
				Assert.That(ckHello, Is.GreaterThan(ckFirst).And.EqualTo(ckHello).And.LessThan(ckWorld).And.LessThan(ckLast));
				Assert.That(ckWorld, Is.GreaterThan(ckFirst).And.GreaterThan(ckHello).And.EqualTo(ckWorld).And.LessThan(ckLast));
				Assert.That(ckLast, Is.GreaterThan(ckFirst).And.GreaterThan(ckWorld).And.GreaterThan(ckWorld).And.EqualTo(ckLast));
			}
			{ // System that is actual SpecialKey
				var k = FdbKey.ToSystemKey(Slice.FromStringAscii("\xFF/status/json"));
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.SuffixBytes.ToString(), Is.EqualTo("/status/json"));
				Assert.That(k.SuffixString, Is.Null);
				Assert.That(k.ToSlice().ToString(), Is.EqualTo("<FF><FF>/status/json"));

				k = FdbKey.ToSystemKey(Slice.FromStringAscii("\xFF/status/json").Span);
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.SuffixBytes.ToString(), Is.EqualTo("/status/json"));
				Assert.That(k.SuffixString, Is.Null);
				Assert.That(k.ToSlice().ToString(), Is.EqualTo("<FF><FF>/status/json"));

				k = FdbKey.ToSystemKey("\xFF/status/json");
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.SuffixString, Is.EqualTo("/status/json"));
				Assert.That(k.SuffixBytes, Is.EqualTo(Slice.Nil));
				Assert.That(k.ToSlice().ToString(), Is.EqualTo("<FF><FF>/status/json"));
			}
		}

		[Test]
		public void Test_FdbSystemKey_Comparisons()
		{
			var metadataVersion = FdbKey.ToSystemKey("/metadataVersion");
			var statusJson = FdbKey.ToSpecialKey("/status/json");

			Assert.That(FdbSystemKey.System, Is.EqualTo(FdbKey.FromBytes([ 0xFF ])));
			Assert.That(FdbSystemKey.Special, Is.EqualTo(FdbKey.FromBytes([ 0xFF, 0xFF ])));

			Assert.That(metadataVersion, Is.EqualTo(FdbKey.ToSystemKey("/metadata"u8).Bytes("Version"u8)));

			Assert.That(FdbSystemKey.System, Is.LessThan(FdbSystemKey.Special));
			Assert.That(FdbSystemKey.Special, Is.GreaterThan(FdbSystemKey.System));

			Assert.That(metadataVersion, Is.GreaterThan(FdbSystemKey.System));
			Assert.That(metadataVersion, Is.LessThan(FdbSystemKey.Special));
			Assert.That(metadataVersion, Is.GreaterThan(FdbKey.FromBytes("/metadataVersion"u8)));

			Assert.That(statusJson, Is.GreaterThan(FdbSystemKey.System));
			Assert.That(statusJson, Is.GreaterThan(FdbSystemKey.Special));
			Assert.That(statusJson, Is.GreaterThan(metadataVersion));
			Assert.That(metadataVersion, Is.GreaterThan(FdbKey.FromBytes("/status/json"u8)));

			Assert.That(FdbSystemKey.System.Bytes("/metadataVersion"), Is.EqualTo(metadataVersion));
			Assert.That(FdbSystemKey.System.Bytes("/metadataVersion").ToSlice(), Is.EqualTo(Slice.FromByteString("\xFF/metadataVersion")));

			Assert.That(FdbSystemKey.Special.Bytes("/status/json"), Is.EqualTo(statusJson));
			Assert.That(FdbSystemKey.Special.Bytes("/status/json").ToSlice(), Is.EqualTo(Slice.FromByteString("\xFF\xFF/status/json")));
			Assert.That(FdbSystemKey.Special.Bytes("/status").Bytes("/json"), Is.EqualTo(statusJson));
		}

		[Test]
		public void Test_FdbSuccessorKey_Basics()
		{
			{
				var k = FdbKey.FromBytes(Slice.Empty).Successor();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.Empty));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0 ])));
				
				Assert.That(k.ToString(), Is.EqualTo("<empty>.<00>"));
				Assert.That(k.ToString("X"), Is.EqualTo("00"));
				Assert.That(k.ToString("K"), Is.EqualTo("<empty>.<00>"));
				Assert.That(k.ToString("P"), Is.EqualTo("<empty>.<00>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbSuccessorKey(FdbRawKey(''))"));

				Assert.That($"***{k}$$$", Is.EqualTo("***<empty>.<00>$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***00$$$"));
				Assert.That($"***{k:K}$$$", Is.EqualTo("***<empty>.<00>$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***<empty>.<00>$$$"));
				Assert.That($"***{k:G}$$$", Is.EqualTo("***FdbSuccessorKey(FdbRawKey(''))$$$"));
			}
			{
				var k = FdbKey.FromBytes([ 0x00 ]).Successor();
				Log($"# {k}");

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x00, 0 ])));
				
				Assert.That(k.ToString(), Is.EqualTo("<00>.<00>"));
				Assert.That(k.ToString("X"), Is.EqualTo("00 00"));
				Assert.That(k.ToString("K"), Is.EqualTo("<00>.<00>"));
				Assert.That(k.ToString("P"), Is.EqualTo("<00>.<00>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbSuccessorKey(FdbRawKey('\\0'))"));
			}
			{
				var k = FdbKey.FromBytes([ 0xFF ]).Successor();
				Log($"# {k}");
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF, 0 ])));
			}
			{
				var p = FdbKey.FromBytes(Slice.FromBytes("hello"u8));
				var k = p.Successor();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("hello\0"u8)));
				
				Assert.That(k.ToString(), Is.EqualTo("hello.<00>"));
				Assert.That(k.ToString("X"), Is.EqualTo("68 65 6C 6C 6F 00"));
				Assert.That(k.ToString("K"), Is.EqualTo("hello.<00>"));
				Assert.That(k.ToString("P"), Is.EqualTo("hello.<00>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbSuccessorKey(FdbRawKey('hello'))"));
				
				Assert.That($"***{k}$$$", Is.EqualTo("***hello.<00>$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***68 65 6C 6C 6F 00$$$"));
				Assert.That($"***{k:K}$$$", Is.EqualTo("***hello.<00>$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***hello.<00>$$$"));
				Assert.That($"***{k:G}$$$", Is.EqualTo("***FdbSuccessorKey(FdbRawKey('hello'))$$$"));
			}
			{
				var subspace = GetSubspace(FdbPath.Parse("/Foo/Bar"), STuple.Create(42));
				var p = subspace.Key("hello");
				var k = p.Successor();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("\x15\x2A\x02hello\0\0"u8)));
				
				Assert.That(k.ToString(), Is.EqualTo("…(\"hello\",).<00>"));
				Assert.That(k.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 00"));
				Assert.That(k.ToString("K"), Is.EqualTo("(42, \"hello\").<00>"));
				Assert.That(k.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\",).<00>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbSuccessorKey(FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",)))"));

				Assert.That($"***{k}$$$", Is.EqualTo("***…(\"hello\",).<00>$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 00 00$$$"));
				Assert.That($"***{k:K}$$$", Is.EqualTo("***(42, \"hello\").<00>$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\",).<00>$$$"));
				Assert.That($"***{k:G}$$$", Is.EqualTo("***FdbSuccessorKey(FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",)))$$$"));
			}
		}

		[Test]
		public void Test_FdbLastKey_Basics()
		{
			{
				var k = FdbKey.FromBytes(Slice.Empty).Last();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.Empty));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF ])));
				
				Assert.That(k.ToString(), Is.EqualTo("<empty>.<FF>"));
				Assert.That(k.ToString("X"), Is.EqualTo("FF"));
				Assert.That(k.ToString("K"), Is.EqualTo("<empty>.<FF>"));
				Assert.That(k.ToString("P"), Is.EqualTo("<empty>.<FF>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbLastKey(FdbRawKey(''))"));
			}
			{
				var k = FdbKey.FromBytes([ 0x00 ]).Last();
				Log($"# {k}");

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x00, 0xFF ])));
				
				Assert.That(k.ToString(), Is.EqualTo("<00>.<FF>"));
				Assert.That(k.ToString("X"), Is.EqualTo("00 FF"));
				Assert.That(k.ToString("K"), Is.EqualTo("<00>.<FF>"));
				Assert.That(k.ToString("P"), Is.EqualTo("<00>.<FF>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbLastKey(FdbRawKey('\\0'))"));
			}
			{
				var k = FdbKey.FromBytes([ 0xFF ]).Last();
				Log($"# {k}");
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));
			}
			{
				var p = FdbKey.FromBytes(Slice.FromBytes("hello"u8));
				var k = p.Last();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("hello"u8) + 0xFF));
				
				Assert.That(k.ToString(), Is.EqualTo("hello.<FF>"));
				Assert.That(k.ToString("X"), Is.EqualTo("68 65 6C 6C 6F FF"));
				Assert.That(k.ToString("K"), Is.EqualTo("hello.<FF>"));
				Assert.That(k.ToString("P"), Is.EqualTo("hello.<FF>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbLastKey(FdbRawKey('hello'))"));
			}
			{
				var subspace = GetSubspace(FdbPath.Parse("/Foo/Bar"), STuple.Create(42));
				var p = subspace.Key("hello");
				var k = p.Last();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("\x15\x2A\x02hello\0"u8) + 0xFF));
				
				Assert.That(k.ToString(), Is.EqualTo("…(\"hello\",).<FF>"));
				Assert.That(k.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 00 FF"));
				Assert.That(k.ToString("K"), Is.EqualTo("(42, \"hello\").<FF>"));
				Assert.That(k.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\",).<FF>"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbLastKey(FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",)))"));
			}
		}

		[Test]
		public void Test_FdbNextSiblingKey_Basics()
		{
			{
				var k = FdbKey.FromBytes(Slice.Empty).NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.Empty));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0 ])));

				Assert.That(k.ToString(), Is.EqualTo("<empty>+1"));
				Assert.That(k.ToString("X"), Is.EqualTo("00"));
				Assert.That(k.ToString("K"), Is.EqualTo("<empty>+1"));
				Assert.That(k.ToString("P"), Is.EqualTo("<empty>+1"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbNextSiblingKey(FdbRawKey(''))"));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0x00 ])).NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.FromBytes([ 0x00 ])));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x01 ])));

				Assert.That(k, Is.EqualTo(k));
				Assert.That(k, Is.EqualTo(FdbKey.FromBytes([ 0x01 ])));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0x01 ])));

				Assert.That(k.ToString(), Is.EqualTo("<00>+1"));
				Assert.That(k.ToString("X"), Is.EqualTo("01"));
				Assert.That(k.ToString("K"), Is.EqualTo("<00>+1"));
				Assert.That(k.ToString("P"), Is.EqualTo("<00>+1"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbNextSiblingKey(FdbRawKey('\\0'))"));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0xFF ])).NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.FromBytes([ 0xFF ])));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(() => k.ToSlice(), Throws.ArgumentException);
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0x00, 0xFF ])).NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.FromBytes([ 0x00, 0xFF ])));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x01 ])));

				Assert.That(k, Is.EqualTo(k));
				Assert.That(k, Is.EqualTo(FdbKey.FromBytes([ 0x01 ])));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0x01 ])));

				Assert.That(k.ToString(), Is.EqualTo("(null, |System|)+1"));
				Assert.That(k.ToString("X"), Is.EqualTo("01"));
				Assert.That(k.ToString("K"), Is.EqualTo("(null, |System|)+1"));
				Assert.That(k.ToString("P"), Is.EqualTo("(null, |System|)+1"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbNextSiblingKey(FdbRawKey(<00><FF>))"));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0x00, 0xFF, 0xFF ])).NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent.Data, Is.EqualTo(Slice.FromBytes([ 0x00, 0xFF, 0xFF ])));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x01 ])));

				Assert.That(k, Is.EqualTo(k));
				Assert.That(k, Is.EqualTo(FdbKey.FromBytes([ 0x01 ])));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0x01 ])));

				Assert.That(k.ToString(), Is.EqualTo("(null, |System|, |System|)+1"));
				Assert.That(k.ToString("X"), Is.EqualTo("01"));
				Assert.That(k.ToString("K"), Is.EqualTo("(null, |System|, |System|)+1"));
				Assert.That(k.ToString("P"), Is.EqualTo("(null, |System|, |System|)+1"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbNextSiblingKey(FdbRawKey(<00><FF><FF>))"));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0xFF, 0xFF, 0xFF ])).NextSibling();
				Log($"# {k}");
				Assert.That(() => k.ToSlice(), Throws.ArgumentException);
			}
			{
				var p = FdbKey.FromBytes("hello"u8);
				var k = p.NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("hellp"u8)));

				Assert.That(k.ToString(), Is.EqualTo("hello+1"));
				Assert.That(k.ToString("X"), Is.EqualTo("68 65 6C 6C 70")); // "hellp"
				Assert.That(k.ToString("K"), Is.EqualTo("hello+1"));
				Assert.That(k.ToString("P"), Is.EqualTo("hello+1"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbNextSiblingKey(FdbRawKey('hello'))"));
			}
			{
				var subspace = GetSubspace(FdbPath.Parse("/Foo/Bar"), STuple.Create(42));
				var p = subspace.Key("hello");
				var k = p.NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.SameAs(subspace));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("\x15\x2A\x02hello\x01"u8)));

				Assert.That(k, Is.EqualTo(k));
				Assert.That(k, Is.EqualTo(FdbKey.FromBytes("\x15\x2A\x02hello\x01"u8)));
				Assert.That(k, Is.EqualTo(Slice.FromBytes("\x15\x2A\x02hello\x01"u8)));

				Assert.That(k.ToString(), Is.EqualTo("…(\"hello\",)+1"));
				Assert.That(k.ToString("X"), Is.EqualTo("15 2A 02 68 65 6C 6C 6F 01")); // (42, "hello")+1
				Assert.That(k.ToString("K"), Is.EqualTo("(42, \"hello\")+1"));
				Assert.That(k.ToString("P"), Is.EqualTo("/Foo/Bar(\"hello\",)+1"));
				Assert.That(k.ToString("G"), Is.EqualTo("FdbNextSiblingKey(FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",)))"));

				Assert.That($"***{k}$$$", Is.EqualTo("***…(\"hello\",)+1$$$"));
				Assert.That($"***{k:X}$$$", Is.EqualTo("***15 2A 02 68 65 6C 6C 6F 01$$$")); // (42, "hello")+1
				Assert.That($"***{k:K}$$$", Is.EqualTo("***(42, \"hello\")+1$$$"));
				Assert.That($"***{k:P}$$$", Is.EqualTo("***/Foo/Bar(\"hello\",)+1$$$"));
				Assert.That($"***{k:G}$$$", Is.EqualTo("***FdbNextSiblingKey(FdbTupleKey<string>(Subspace=/Foo/Bar, Items=(\"hello\",)))$$$"));

			}
		}

	}

}
