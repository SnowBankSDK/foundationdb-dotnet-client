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

// ReSharper disable AccessToModifiedClosure
// ReSharper disable StringLiteralTypo
// ReSharper disable InlineOutVariableDeclaration
// ReSharper disable ConvertToUsingDeclaration
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable RedundantArgumentDefaultValue
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace SnowBank.Data.Tuples.Tests
{
	using System.Collections;
	using System.Linq;
	using System.Net;

	using SnowBank.Buffers.Text;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class TupleFacts : SimpleTest
	{
		// the netstandard2.0 build formats floating-point values with "R" (17 significant digits on netfx),
		// while the modern targets emit the shortest round-trippable form: compute the expected text at runtime
		private static readonly string PiDoubleText = Math.PI.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
		private static readonly string PiSingleText = ((float) Math.PI).ToString("R", System.Globalization.CultureInfo.InvariantCulture);


		#region General Use...

		[Test]
		public void Test_Tuple_0()
		{
			var t0 = STuple.Create();
			Log(t0);
			Assert.That(t0.Count, Is.Zero);
			Assert.That(t0.ToArray(), Is.EqualTo(Array.Empty<object>()));
			Assert.That(t0.ToString(), Is.EqualTo("()"));
			Assert.That(t0.ToString(null, null), Is.EqualTo("()"));
			Assert.That($"***{t0}***", Is.EqualTo("***()***"));
			Assert.That(t0, Is.InstanceOf<STuple>());
			Assert.That(((IVarTuple) t0)[0, 0], Is.EqualTo(STuple.Empty));
			Assert.That(((IVarTuple) t0)[..], Is.EqualTo(STuple.Empty));
			Assert.That(() => ((IVarTuple) t0)[0], Throws.Exception);

			using (var it = t0.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Tuple_1()
		{
			var t1 = STuple.Create("hello world");
			Log(t1);
			Assert.That(t1.Count, Is.EqualTo(1));
			Assert.That(t1.Item1, Is.EqualTo("hello world"));
			Assert.That(t1.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(((IVarTuple)t1)[0], Is.EqualTo("hello world"));
			Assert.That(t1.ToArray(), Is.EqualTo(new object[] { "hello world" }));
			Assert.That(t1.ToString(), Is.EqualTo("(\"hello world\",)"));
			Assert.That(t1.ToString(null, null), Is.EqualTo("(\"hello world\",)"));
			Assert.That($"***{t1}***", Is.EqualTo("***(\"hello world\",)***"));
			Assert.That(t1, Is.InstanceOf<STuple<string>>());
			Assert.That(t1[0, 1], Is.EqualTo(t1));
			Assert.That(t1[..], Is.EqualTo(t1));
			Assert.That(t1[..0], Is.EqualTo(STuple.Empty));
			Assert.That(t1[1..], Is.EqualTo(STuple.Empty));

			Assert.That(STuple.Create(123).GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123).Tail.GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123).GetHashCode(), Is.EqualTo(STuple.Create(123L).GetHashCode()), "Hashcode should be stable");

			// ReSharper disable CannotApplyEqualityOperatorToType
			// ReSharper disable EqualExpressionComparison
			Assert.That(STuple.Create(123) == STuple.Create(123), Is.True, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123) != STuple.Create(123), Is.False, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123) == STuple.Create(456), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123) != STuple.Create(456), Is.True, "op_Inequality should work for struct tuples");

			Assert.That(STuple.Create(123) == ValueTuple.Create(123), Is.True, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123) != ValueTuple.Create(123), Is.False, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123) == ValueTuple.Create(456), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123) != ValueTuple.Create(456), Is.True, "op_Inequality should work for struct tuples");

			Assert.That(ValueTuple.Create(123) == STuple.Create(123), Is.True, "op_Equality should work for struct tuples");
			Assert.That(ValueTuple.Create(123) != STuple.Create(123), Is.False, "op_Inequality should work for struct tuples");
			Assert.That(ValueTuple.Create(123) == STuple.Create(456), Is.False, "op_Equality should work for struct tuples");
			Assert.That(ValueTuple.Create(123) != STuple.Create(456), Is.True, "op_Inequality should work for struct tuples");

			// ReSharper restore EqualExpressionComparison
			// ReSharper restore CannotApplyEqualityOperatorToType

			{ // Deconstruct
				t1.Deconstruct(out var item1);
				Assert.That(item1, Is.EqualTo("hello world"));
			}

			Assert.That((ValueTuple<string>) t1, Is.EqualTo(ValueTuple.Create("hello world")));
			Assert.That(t1.ToValueTuple(), Is.EqualTo(ValueTuple.Create("hello world")));
			Assert.That((STuple<string>) Tuple.Create("hello world"), Is.EqualTo(t1));

			Assert.That(t1.Append(123), Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t1.Append(123, false), Is.EqualTo(STuple.Create("hello world", 123, false)));

			Assert.That(t1.Concat(STuple.Create(123)), Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t1.Concat(STuple.Create(123, false)), Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t1.Concat(STuple.Create(123, false, 1234L)), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t1.Concat(STuple.Create(123, false, 1234L, -1234)), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));

			Assert.That(t1.Concat(ValueTuple.Create(123)), Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t1.Concat((123, false)), Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t1.Concat((123, false, 1234L)), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t1.Concat((123, false, 1234L, -1234)), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t1.Concat((123, false, 1234L, -1234, Math.PI)), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, Math.PI)));

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_2()
		{
			var t2 = STuple.Create("hello world", 123);
			Log(t2);
			Assert.That(t2.Count, Is.EqualTo(2));
			Assert.That(t2.Item1, Is.EqualTo("hello world"));
			Assert.That(t2.Item2, Is.EqualTo(123));
			Assert.That(t2.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t2.Get<int>(1), Is.EqualTo(123));
			Assert.That(((IVarTuple) t2)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t2)[1], Is.EqualTo(123));
			Assert.That(t2.ToArray(), Is.EqualTo(new object[] { "hello world", 123 }));
			Assert.That(t2.ToString(), Is.EqualTo("(\"hello world\", 123)"));
			Assert.That(t2.ToString(null, null), Is.EqualTo("(\"hello world\", 123)"));
			Assert.That($"***{t2}***", Is.EqualTo("***(\"hello world\", 123)***"));
			Assert.That(t2, Is.InstanceOf<STuple<string, int>>());
			Assert.That(t2[0, 2], Is.EqualTo(t2));
			Assert.That(t2[0, 1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t2[1, 2], Is.EqualTo(STuple.Create(123)));
			Assert.That(t2[..], Is.EqualTo(t2));
			Assert.That(t2[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t2[1..], Is.EqualTo(STuple.Create(123)));

			Assert.That(t2.Tail.Count, Is.EqualTo(1));
			Assert.That(t2.Tail.Item1, Is.EqualTo(123));

			Assert.That(STuple.Create(123, true).GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true).Tail.GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true).GetHashCode(), Is.EqualTo(STuple.Create(123L, 1).GetHashCode()), "Hashcode should be stable");

			// ReSharper disable CannotApplyEqualityOperatorToType
			// ReSharper disable EqualExpressionComparison
			Assert.That(STuple.Create(123, true) == STuple.Create(123, true), Is.True, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true) != STuple.Create(123, true), Is.False, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true) == STuple.Create(456, true), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true) != STuple.Create(456, true), Is.True, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true) == STuple.Create(123, false), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true) != STuple.Create(123, false), Is.True, "op_Inequality should work for struct tuples");
			// ReSharper restore EqualExpressionComparison
			// ReSharper restore CannotApplyEqualityOperatorToType

			{ // Deconstruct
				t2.Deconstruct(out var item1, out var item2);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
			}
			{ // Deconstruct
				var (item1, item2) = t2;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
			}

			Assert.That(((string, int)) t2, Is.EqualTo(ValueTuple.Create("hello world", 123)));
			Assert.That(t2.ToValueTuple(), Is.EqualTo(ValueTuple.Create("hello world", 123)));
			Assert.That((STuple<string, int>) Tuple.Create("hello world", 123), Is.EqualTo(t2));

			Assert.That(t2.Append(false), Is.InstanceOf<STuple<string, int, bool>>().And.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t2.Append(false, 1234L), Is.InstanceOf<STuple<string, int, bool, long>>().And.EqualTo(STuple.Create("hello world", 123, false, 1234L)));

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_3()
		{
			var t3 = STuple.Create("hello world", 123, false);
			Log(t3);
			Assert.That(t3, Is.InstanceOf<STuple<string, int, bool>>());
			Assert.That(t3.Count, Is.EqualTo(3));
			Assert.That(t3.Item1, Is.EqualTo("hello world"));
			Assert.That(t3.Item2, Is.EqualTo(123));
			Assert.That(t3.Item3, Is.False);
			Assert.That(t3.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t3.Get<int>(1), Is.EqualTo(123));
			Assert.That(t3.Get<bool>(2), Is.False);
			Assert.That(((IVarTuple) t3)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t3)[1], Is.EqualTo(123));
			Assert.That(((IVarTuple) t3)[2], Is.False);
			Assert.That(t3.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false }));
			Assert.That(t3.ToString(), Is.EqualTo(@"(""hello world"", 123, false)"));
			Assert.That(t3.ToString(null, null), Is.EqualTo("(\"hello world\", 123, false)"));
			Assert.That($"***{t3}***", Is.EqualTo("***(\"hello world\", 123, false)***"));

			Assert.That(((IVarTuple) t3)[^1], Is.EqualTo(false));
			Assert.That(((IVarTuple) t3)[^2], Is.EqualTo(123));
			Assert.That(((IVarTuple) t3)[^3], Is.EqualTo("hello world"));
			Assert.That(() => ((IVarTuple) t3)[^4], Throws.Exception);

			Assert.That(t3[0, 3], Is.EqualTo(t3));
			Assert.That(t3[0, 1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t3[0, 2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t3[1, 2], Is.EqualTo(STuple.Create(123)));
			Assert.That(t3[1, 3], Is.EqualTo(STuple.Create(123, false)));
			Assert.That(t3[..], Is.EqualTo(t3));
			Assert.That(t3[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t3[..2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t3[1..2], Is.EqualTo(STuple.Create(123)));
			Assert.That(t3[1..], Is.EqualTo(STuple.Create(123, false)));

			Assert.That(t3.Tail.Count, Is.EqualTo(2));
			Assert.That(t3.Tail.Item1, Is.EqualTo(123));
			Assert.That(t3.Tail.Item2, Is.False);

			Assert.That(STuple.Create(123, true, "foo").GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true, "foo").Tail.GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo").GetHashCode(), Is.EqualTo(STuple.Create(123L, 1, "foo").GetHashCode()), "Hashcode should be stable");

			// ReSharper disable CannotApplyEqualityOperatorToType
			// ReSharper disable EqualExpressionComparison
			Assert.That(STuple.Create(123, true, "foo") == STuple.Create(123, true, "foo"), Is.True, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") != STuple.Create(123, true, "foo"), Is.False, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") == STuple.Create(456, true, "foo"), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") != STuple.Create(456, true, "foo"), Is.True, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") == STuple.Create(123, false, "foo"), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") != STuple.Create(123, false, "foo"), Is.True, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") == STuple.Create(123, true, "bar"), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo") != STuple.Create(123, true, "bar"), Is.True, "op_Inequality should work for struct tuples");
			// ReSharper restore EqualExpressionComparison
			// ReSharper restore CannotApplyEqualityOperatorToType

			{ // Deconstruct
				t3.Deconstruct(out var item1, out var item2, out var item3);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
			}
			{ // Deconstruct
				var (item1, item2, item3) = t3;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
			}

			Assert.That(((string, int, bool)) t3, Is.EqualTo(ValueTuple.Create("hello world", 123, false)));
			Assert.That(t3.ToValueTuple(), Is.EqualTo(ValueTuple.Create("hello world", 123, false)));
			Assert.That((STuple<string, int, bool>) Tuple.Create("hello world", 123, false), Is.EqualTo(t3));

			Assert.That(t3.Append(1234L), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t3.Append(1234L, -1234), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t3.Append(1234L, -1234, Math.PI), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, Math.PI)));

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2, 3);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Relaxed), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2, 3)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3, 3)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 4)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2, 3)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1, 3)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 2)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3, 4).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_4()
		{
			var t4 = STuple.Create("hello world", 123, false, 1234L);
			Log(t4);
			Assert.That(t4, Is.InstanceOf<STuple<string, int, bool, long>>());
			Assert.That(t4.Count, Is.EqualTo(4));
			Assert.That(t4.Item1, Is.EqualTo("hello world"));
			Assert.That(t4.Item2, Is.EqualTo(123));
			Assert.That(t4.Item3, Is.False);
			Assert.That(t4.Item4, Is.EqualTo(1234L));
			Assert.That(t4.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t4.Get<int>(1), Is.EqualTo(123));
			Assert.That(t4.Get<bool>(2), Is.False);
			Assert.That(t4.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t4)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t4)[1], Is.EqualTo(123));
			Assert.That(((IVarTuple) t4)[2], Is.False);
			Assert.That(((IVarTuple) t4)[3], Is.EqualTo(1234L));
			Assert.That(t4.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false, 1234L}));
			Assert.That(t4.ToString(), Is.EqualTo(@"(""hello world"", 123, false, 1234)"));
			Assert.That(t4.ToString(null, null), Is.EqualTo("(\"hello world\", 123, false, 1234)"));
			Assert.That($"***{t4}***", Is.EqualTo("***(\"hello world\", 123, false, 1234)***"));

			Assert.That(((IVarTuple) t4)[^1], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t4)[^2], Is.EqualTo(false));
			Assert.That(((IVarTuple) t4)[^3], Is.EqualTo(123));
			Assert.That(((IVarTuple) t4)[^4], Is.EqualTo("hello world"));
			Assert.That(() => ((IVarTuple) t4)[^5], Throws.Exception);

			Assert.That(t4[..], Is.EqualTo(t4));
			Assert.That(t4[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t4[..2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t4[..3], Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t4[1..], Is.EqualTo(STuple.Create(123, false, 1234L)));
			Assert.That(t4[2..], Is.EqualTo(STuple.Create(false, 1234L)));
			Assert.That(t4[3..], Is.EqualTo(STuple.Create(1234L)));
			Assert.That(t4[1..3], Is.EqualTo(STuple.Create(123, false)));
			Assert.That(t4[2..3], Is.EqualTo(STuple.Create(false)));

			Assert.That(t4.Tail.Count, Is.EqualTo(3));
			Assert.That(t4.Tail.Item1, Is.EqualTo(123));
			Assert.That(t4.Tail.Item2, Is.False);
			Assert.That(t4.Tail.Item3, Is.EqualTo(1234L));

			Assert.That(STuple.Create(123, true, "foo", 666).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo").Append(666).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666).GetHashCode(), Is.EqualTo(STuple.Create(123, true).Append("foo", 666).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666).GetHashCode(), Is.EqualTo(STuple.Create(123).Concat(STuple.Create(true, "foo", 666)).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666).GetHashCode(), Is.EqualTo(STuple.Create(123L, 1, "foo", 666UL).GetHashCode()), "Hashcode should be stable");

			// ReSharper disable CannotApplyEqualityOperatorToType
			// ReSharper disable EqualExpressionComparison
			Assert.That(STuple.Create(123, true, "foo", 666) == STuple.Create(123, true, "foo", 666), Is.True, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) != STuple.Create(123, true, "foo", 666), Is.False, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) == STuple.Create(456, true, "foo", 666), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) != STuple.Create(456, true, "foo", 666), Is.True, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) == STuple.Create(123, false, "foo", 666), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) != STuple.Create(123, false, "foo", 666), Is.True, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) == STuple.Create(123, true, "bar", 666), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) != STuple.Create(123, true, "bar", 666), Is.True, "op_Inequality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) == STuple.Create(123, true, "foo", 667), Is.False, "op_Equality should work for struct tuples");
			Assert.That(STuple.Create(123, true, "foo", 666) != STuple.Create(123, true, "foo", 667), Is.True, "op_Inequality should work for struct tuples");
			// ReSharper restore EqualExpressionComparison
			// ReSharper restore CannotApplyEqualityOperatorToType

			{ // Deconstruct
				t4.Deconstruct(out var item1, out var item2, out var item3, out var item4);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
			}
			{ // Deconstruct
				var (item1, item2, item3, item4) = t4;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
			}

			Assert.That(((string, int, bool, long)) t4, Is.EqualTo(ValueTuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t4.ToValueTuple(), Is.EqualTo(ValueTuple.Create("hello world", 123, false, 1234L)));
			Assert.That((STuple<string, int, bool, long>) Tuple.Create("hello world", 123, false, 1234L), Is.EqualTo(t4));

			Assert.That(((IVarTuple) t4).Append(-1234), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t4.Append(-1234), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t4.Append(-1234, Math.PI), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, Math.PI)));

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2, 3, 4);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Relaxed), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2, 3, 4)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3, 3, 4)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 4, 4)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 5)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2, 3, 4)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1, 3, 4)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 2, 4)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 3)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3, 4, 5).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_5()
		{
			var t5 = STuple.Create("hello world", 123, false, 1234L, -1234);
			Log(t5);
			Assert.That(t5, Is.InstanceOf<STuple<string, int, bool, long, int>>());
			Assert.That(t5.Count, Is.EqualTo(5));
			Assert.That(t5.Item1, Is.EqualTo("hello world"));
			Assert.That(t5.Item2, Is.EqualTo(123));
			Assert.That(t5.Item3, Is.False);
			Assert.That(t5.Item4, Is.EqualTo(1234L));
			Assert.That(t5.Item5, Is.EqualTo(-1234));
			Assert.That(t5.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t5.Get<int>(1), Is.EqualTo(123));
			Assert.That(t5.Get<bool>(2), Is.False);
			Assert.That(t5.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(t5.Get<int>(4), Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t5)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t5)[1], Is.EqualTo(123));
			Assert.That(((IVarTuple) t5)[2], Is.False);
			Assert.That(((IVarTuple) t5)[3], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t5)[4], Is.EqualTo(-1234));
			Assert.That(t5.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false, 1234L, -1234 }));
			Assert.That(t5.ToString(), Is.EqualTo(@"(""hello world"", 123, false, 1234, -1234)"));
			Assert.That(t5.ToString(null, null), Is.EqualTo("(\"hello world\", 123, false, 1234, -1234)"));
			Assert.That($"***{t5}***", Is.EqualTo("***(\"hello world\", 123, false, 1234, -1234)***"));

			Assert.That(((IVarTuple) t5)[^1], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t5)[^2], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t5)[^3], Is.EqualTo(false));
			Assert.That(((IVarTuple) t5)[^4], Is.EqualTo(123));
			Assert.That(((IVarTuple) t5)[^5], Is.EqualTo("hello world"));
			Assert.That(() => ((IVarTuple) t5)[^6], Throws.Exception);

			Assert.That(t5[..], Is.EqualTo(t5));
			Assert.That(t5[1..], Is.EqualTo(STuple.Create(123, false, 1234L, -1234)));
			Assert.That(t5[2..], Is.EqualTo(STuple.Create(false, 1234L, -1234)));
			Assert.That(t5[3..], Is.EqualTo(STuple.Create(1234L, -1234)));
			Assert.That(t5[4..], Is.EqualTo(STuple.Create(-1234)));
			Assert.That(t5[..4], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t5[..3], Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t5[..2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t5[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t5[1..4], Is.EqualTo(STuple.Create(123, false, 1234L)));
			Assert.That(t5[2..4], Is.EqualTo(STuple.Create(false, 1234L)));
			Assert.That(t5[2..3], Is.EqualTo(STuple.Create(false)));

			Assert.That(t5.Tail.Count, Is.EqualTo(4));
			Assert.That(t5.Tail.Item1, Is.EqualTo(123));
			Assert.That(t5.Tail.Item2, Is.False);
			Assert.That(t5.Tail.Item3, Is.EqualTo(1234L));
			Assert.That(t5.Tail.Item4, Is.EqualTo(-1234L));

			Assert.That(STuple.Create(123, true, "foo", 666, false).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666).Append(false).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo").Append(666, false).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false).GetHashCode(), Is.EqualTo(STuple.Create(123, true).Concat(STuple.Create("foo", 666, false)).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false).GetHashCode(), Is.EqualTo(STuple.Create(123).Concat(STuple.Create(true, "foo", 666, false)).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false).GetHashCode(), Is.EqualTo(STuple.Create(123L, 1, "foo", 666UL, 0).GetHashCode()), "Hashcode should be stable");

			{ // Deconstruct
				t5.Deconstruct(out string item1, out int item2, out bool item3, out long item4, out int item5);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234));
			}
			{ // Deconstruct
				(string item1, int item2, bool item3, long item4, int item5) = t5;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234));
			}

			Assert.That(((string, int, bool, long, int)) t5, Is.EqualTo(ValueTuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t5.ToValueTuple(), Is.EqualTo(ValueTuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That((STuple<string, int, bool, long, int>) Tuple.Create("hello world", 123, false, 1234L, -1234), Is.EqualTo(t5));

			Assert.That(((IVarTuple) t5).Append("six"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six")));
			Assert.That(t5.Append("six"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six")));
			Assert.That(t5.Append("six", "seven"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t5.Append("six", "seven", "eight"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight")));

			Assert.That(t5.Concat(STuple.Create("six")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six")));
			Assert.That(t5.Concat(STuple.Create("six", "seven")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t5.Concat(STuple.Create("six", "seven", "eight")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight")));

			Assert.That(t5.Concat(ValueTuple.Create("six")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six")));
			Assert.That(t5.Concat(ValueTuple.Create("six", "seven")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t5.Concat(ValueTuple.Create("six", "seven", "eight")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight")));

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2, 3, 4, 5);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Relaxed), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2, 3, 4, 5)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3, 3, 4, 5)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 4, 4, 5)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 5, 5)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 6)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2, 3, 4, 5)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1, 3, 4, 5)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 2, 4, 5)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 3, 5)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 4)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3, 4, 5, 6).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_6()
		{
			var t6 = STuple.Create("hello world", 123, false, 1234L, -1234, "six");
			Log(t6);
			Assert.That(t6, Is.InstanceOf<STuple<string, int, bool, long, int, string>>());
			Assert.That(t6.Count, Is.EqualTo(6));
			Assert.That(t6.Item1, Is.EqualTo("hello world"));
			Assert.That(t6.Item2, Is.EqualTo(123));
			Assert.That(t6.Item3, Is.False);
			Assert.That(t6.Item4, Is.EqualTo(1234L));
			Assert.That(t6.Item5, Is.EqualTo(-1234));
			Assert.That(t6.Item6, Is.EqualTo("six"));
			Assert.That(t6.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t6.Get<int>(1), Is.EqualTo(123));
			Assert.That(t6.Get<bool>(2), Is.False);
			Assert.That(t6.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(t6.Get<int>(4), Is.EqualTo(-1234));
			Assert.That(t6.Get<string>(5), Is.EqualTo("six"));
			Assert.That(((IVarTuple) t6)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t6)[1], Is.EqualTo(123));
			Assert.That(((IVarTuple) t6)[2], Is.False);
			Assert.That(((IVarTuple) t6)[3], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t6)[4], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t6)[5], Is.EqualTo("six"));
			Assert.That(t6.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false, 1234L, -1234, "six" }));
			Assert.That(t6.ToString(), Is.EqualTo(@"(""hello world"", 123, false, 1234, -1234, ""six"")"));
			Assert.That(t6.ToString(null, null), Is.EqualTo("(\"hello world\", 123, false, 1234, -1234, \"six\")"));
			Assert.That($"***{t6}***", Is.EqualTo("***(\"hello world\", 123, false, 1234, -1234, \"six\")***"));

			Assert.That(((IVarTuple) t6)[^1], Is.EqualTo("six"));
			Assert.That(((IVarTuple) t6)[^2], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t6)[^3], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t6)[^4], Is.EqualTo(false));
			Assert.That(((IVarTuple) t6)[^5], Is.EqualTo(123));
			Assert.That(((IVarTuple) t6)[^6], Is.EqualTo("hello world"));
			Assert.That(() => ((IVarTuple) t6)[^7], Throws.Exception);

			Assert.That(t6[..], Is.EqualTo(t6));
			Assert.That(t6[1..], Is.EqualTo(STuple.Create(123, false, 1234L, -1234, "six")));
			Assert.That(t6[2..], Is.EqualTo(STuple.Create(false, 1234L, -1234, "six")));
			Assert.That(t6[3..], Is.EqualTo(STuple.Create(1234L, -1234, "six")));
			Assert.That(t6[4..], Is.EqualTo(STuple.Create(-1234, "six")));
			Assert.That(t6[5..], Is.EqualTo(STuple.Create("six")));
			Assert.That(t6[..5], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t6[..4], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t6[..3], Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t6[..2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t6[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t6[1..5], Is.EqualTo(STuple.Create(123, false, 1234L, -1234)));
			Assert.That(t6[1..4], Is.EqualTo(STuple.Create(123, false, 1234L)));
			Assert.That(t6[2..4], Is.EqualTo(STuple.Create(false, 1234L)));
			Assert.That(t6[2..3], Is.EqualTo(STuple.Create(false)));

			Assert.That(t6.Tail.Count, Is.EqualTo(5));
			Assert.That(t6.Tail.Item1, Is.EqualTo(123));
			Assert.That(t6.Tail.Item2, Is.False);
			Assert.That(t6.Tail.Item3, Is.EqualTo(1234L));
			Assert.That(t6.Tail.Item4, Is.EqualTo(-1234L));
			Assert.That(t6.Tail.Item5, Is.EqualTo("six"));

			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666, false).Append("bar").GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666).Append(false, "bar").GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo").Concat(STuple.Create(666, false, "bar")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar").GetHashCode(), Is.EqualTo(STuple.Create(123, true).Concat(STuple.Create("foo", 666, false, "bar")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar").GetHashCode(), Is.EqualTo(STuple.Create(123).Concat(STuple.Create(true, "foo", 666, false, "bar")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar").GetHashCode(), Is.EqualTo(STuple.Create(123L, 1, "foo", 666UL, 0, "bar").GetHashCode()), "Hashcode should be stable");

			Assert.That(((IVarTuple) t6).Append("seven"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t6.Append("seven"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t6.Append("seven", "eight"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight")));
			Assert.That(t6.Append("seven", "eight","nine"), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight", "nine")));

			Assert.That(t6.Concat(STuple.Create("seven")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t6.Concat(STuple.Create("seven", "eight")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight")));
			Assert.That(t6.Concat(STuple.Create("seven", "eight", "nine")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight", "nine")));

			Assert.That(t6.Concat(ValueTuple.Create("seven")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven")));
			Assert.That(t6.Concat(ValueTuple.Create("seven", "eight")), Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", "seven", "eight")));

			{ // Deconstruct
				t6.Deconstruct(out string item1, out int item2, out bool item3, out long item4, out long item5, out string item6);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234L));
				Assert.That(item6, Is.EqualTo("six"));
			}
			{ // Deconstruct
				(string item1, int item2, bool item3, long item4, int item5, string item6) = t6;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234L));
				Assert.That(item6, Is.EqualTo("six"));
			}

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2, 3, 4, 5, 6);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Relaxed), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2, 3, 4, 5, 6)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3, 3, 4, 5, 6)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 4, 4, 5, 6)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 5, 5, 6)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 6, 6)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2, 3, 4, 5, 6)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1, 3, 4, 5, 6)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 2, 4, 5, 6)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 3, 5, 6)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 4, 6)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 5)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 7)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3, 4, 5, 6, 7).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_7()
		{
			var t7 = STuple.Create("hello world", 123, false, 1234L, -1234, "six", 777);
			Log(t7);
			Assert.That(t7, Is.InstanceOf<STuple<string, int, bool, long, int, string, int>>());
			Assert.That(t7.Count, Is.EqualTo(7));
			Assert.That(t7.Item1, Is.EqualTo("hello world"));
			Assert.That(t7.Item2, Is.EqualTo(123));
			Assert.That(t7.Item3, Is.False);
			Assert.That(t7.Item4, Is.EqualTo(1234L));
			Assert.That(t7.Item5, Is.EqualTo(-1234));
			Assert.That(t7.Item6, Is.EqualTo("six"));
			Assert.That(t7.Item7, Is.EqualTo(777));
			Assert.That(t7.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t7.Get<int>(1), Is.EqualTo(123));
			Assert.That(t7.Get<bool>(2), Is.False);
			Assert.That(t7.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(t7.Get<int>(4), Is.EqualTo(-1234));
			Assert.That(t7.Get<string>(5), Is.EqualTo("six"));
			Assert.That(t7.Get<int>(6), Is.EqualTo(777));
			Assert.That(((IVarTuple) t7)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t7)[1], Is.EqualTo(123));
			Assert.That(((IVarTuple) t7)[2], Is.False);
			Assert.That(((IVarTuple) t7)[3], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t7)[4], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t7)[5], Is.EqualTo("six"));
			Assert.That(((IVarTuple) t7)[6], Is.EqualTo(777));
			Assert.That(t7.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false, 1234L, -1234, "six", 777 }));
			Assert.That(t7.ToString(), Is.EqualTo(@"(""hello world"", 123, false, 1234, -1234, ""six"", 777)"));
			Assert.That(t7.ToString(null, null), Is.EqualTo("(\"hello world\", 123, false, 1234, -1234, \"six\", 777)"));
			Assert.That($"***{t7}***", Is.EqualTo("***(\"hello world\", 123, false, 1234, -1234, \"six\", 777)***"));

			Assert.That(((IVarTuple) t7)[^1], Is.EqualTo(777));
			Assert.That(((IVarTuple) t7)[^2], Is.EqualTo("six"));
			Assert.That(((IVarTuple) t7)[^3], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t7)[^4], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t7)[^5], Is.EqualTo(false));
			Assert.That(((IVarTuple) t7)[^6], Is.EqualTo(123));
			Assert.That(((IVarTuple) t7)[^7], Is.EqualTo("hello world"));
			Assert.That(() => ((IVarTuple) t7)[^8], Throws.Exception);

			Assert.That(t7[..], Is.EqualTo(t7));
			Assert.That(t7[1..], Is.EqualTo(STuple.Create(123, false, 1234L, -1234, "six", 777)));
			Assert.That(t7[2..], Is.EqualTo(STuple.Create(false, 1234L, -1234, "six", 777)));
			Assert.That(t7[3..], Is.EqualTo(STuple.Create(1234L, -1234, "six", 777)));
			Assert.That(t7[4..], Is.EqualTo(STuple.Create(-1234, "six", 777)));
			Assert.That(t7[5..], Is.EqualTo(STuple.Create("six", 777)));
			Assert.That(t7[6..], Is.EqualTo(STuple.Create(777)));
			Assert.That(t7[..6], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six")));
			Assert.That(t7[..5], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t7[..4], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t7[..3], Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t7[..2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t7[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t7[1..6], Is.EqualTo(STuple.Create(123, false, 1234L, -1234, "six")));
			Assert.That(t7[1..5], Is.EqualTo(STuple.Create(123, false, 1234L, -1234)));
			Assert.That(t7[1..4], Is.EqualTo(STuple.Create(123, false, 1234L)));
			Assert.That(t7[2..4], Is.EqualTo(STuple.Create(false, 1234L)));
			Assert.That(t7[2..3], Is.EqualTo(STuple.Create(false)));

			Assert.That(t7.Tail.Count, Is.EqualTo(6));
			Assert.That(t7.Tail.Item1, Is.EqualTo(123));
			Assert.That(t7.Tail.Item2, Is.False);
			Assert.That(t7.Tail.Item3, Is.EqualTo(1234L));
			Assert.That(t7.Tail.Item4, Is.EqualTo(-1234L));
			Assert.That(t7.Tail.Item5, Is.EqualTo("six"));
			Assert.That(t7.Tail.Item6, Is.EqualTo(777));

			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666, false, "bar").Append(777).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666, false).Append("bar", 777).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666).Concat(STuple.Create(false, "bar", 777)).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777).GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo").Concat(STuple.Create(666, false, "bar", 777)).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777).GetHashCode(), Is.EqualTo(STuple.Create(123).Concat(STuple.Create(true, "foo", 666, false, "bar", 777)).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777).GetHashCode(), Is.EqualTo(STuple.Create(123L, 1, "foo", 666UL, 0, "bar", 777U).GetHashCode()), "Hashcode should be stable");

			{ // Deconstruct
				t7.Deconstruct(out string item1, out int item2, out bool item3, out long item4, out long item5, out string item6, out int item7);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234L));
				Assert.That(item6, Is.EqualTo("six"));
				Assert.That(item7, Is.EqualTo(777));
			}
			{ // Deconstruct
				(string item1, int item2, bool item3, long item4, int item5, string item6, int item7) = t7;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234L));
				Assert.That(item6, Is.EqualTo("six"));
				Assert.That(item7, Is.EqualTo(777));
			}

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2, 3, 4, 5, 6, 7);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Relaxed), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2, 3, 4, 5, 6, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3, 3, 4, 5, 6, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 4, 4, 5, 6, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 5, 5, 6, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 6, 6, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 7, 7)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2, 3, 4, 5, 6, 7)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1, 3, 4, 5, 6, 7)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 2, 4, 5, 6, 7)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 3, 5, 6, 7)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 4, 6, 7)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 5, 7)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 6)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 7, 8)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3, 4, 5, 6, 7, 8).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_8()
		{
			var t8 = STuple.Create("hello world", 123, false, 1234L, -1234, "six", 777, "eight");
			Log(t8);
			Assert.That(t8, Is.InstanceOf<STuple<string, int, bool, long, int, string, int, string>>());
			Assert.That(t8.Count, Is.EqualTo(8));
			Assert.That(t8.Item1, Is.EqualTo("hello world"));
			Assert.That(t8.Item2, Is.EqualTo(123));
			Assert.That(t8.Item3, Is.False);
			Assert.That(t8.Item4, Is.EqualTo(1234L));
			Assert.That(t8.Item5, Is.EqualTo(-1234));
			Assert.That(t8.Item6, Is.EqualTo("six"));
			Assert.That(t8.Item7, Is.EqualTo(777));
			Assert.That(t8.Item8, Is.EqualTo("eight"));
			Assert.That(t8.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t8.Get<int>(1), Is.EqualTo(123));
			Assert.That(t8.Get<bool>(2), Is.False);
			Assert.That(t8.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(t8.Get<int>(4), Is.EqualTo(-1234));
			Assert.That(t8.Get<string>(5), Is.EqualTo("six"));
			Assert.That(t8.Get<int>(6), Is.EqualTo(777));
			Assert.That(t8.Get<string>(7), Is.EqualTo("eight"));
			Assert.That(((IVarTuple) t8)[0], Is.EqualTo("hello world"));
			Assert.That(((IVarTuple) t8)[1], Is.EqualTo(123));
			Assert.That(((IVarTuple) t8)[2], Is.False);
			Assert.That(((IVarTuple) t8)[3], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t8)[4], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t8)[5], Is.EqualTo("six"));
			Assert.That(((IVarTuple) t8)[6], Is.EqualTo(777));
			Assert.That(((IVarTuple) t8)[7], Is.EqualTo("eight"));
			Assert.That(t8.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false, 1234L, -1234, "six", 777, "eight" }));
			Assert.That(t8.ToString(), Is.EqualTo(@"(""hello world"", 123, false, 1234, -1234, ""six"", 777, ""eight"")"));
			Assert.That(t8.ToString(null, null), Is.EqualTo("(\"hello world\", 123, false, 1234, -1234, \"six\", 777, \"eight\")"));
			Assert.That($"***{t8}***", Is.EqualTo("***(\"hello world\", 123, false, 1234, -1234, \"six\", 777, \"eight\")***"));

			Assert.That(((IVarTuple) t8)[^1], Is.EqualTo("eight"));
			Assert.That(((IVarTuple) t8)[^2], Is.EqualTo(777));
			Assert.That(((IVarTuple) t8)[^3], Is.EqualTo("six"));
			Assert.That(((IVarTuple) t8)[^4], Is.EqualTo(-1234));
			Assert.That(((IVarTuple) t8)[^5], Is.EqualTo(1234L));
			Assert.That(((IVarTuple) t8)[^6], Is.EqualTo(false));
			Assert.That(((IVarTuple) t8)[^7], Is.EqualTo(123));
			Assert.That(((IVarTuple) t8)[^8], Is.EqualTo("hello world"));
			Assert.That(() => ((IVarTuple) t8)[^9], Throws.Exception);

			Assert.That(t8[..], Is.EqualTo(t8));
			Assert.That(t8[1..], Is.EqualTo(STuple.Create(123, false, 1234L, -1234, "six", 777, "eight")));
			Assert.That(t8[2..], Is.EqualTo(STuple.Create(false, 1234L, -1234, "six", 777, "eight")));
			Assert.That(t8[3..], Is.EqualTo(STuple.Create(1234L, -1234, "six", 777, "eight")));
			Assert.That(t8[4..], Is.EqualTo(STuple.Create(-1234, "six", 777, "eight")));
			Assert.That(t8[5..], Is.EqualTo(STuple.Create("six", 777, "eight")));
			Assert.That(t8[6..], Is.EqualTo(STuple.Create(777, "eight")));
			Assert.That(t8[7..], Is.EqualTo(STuple.Create("eight")));
			Assert.That(t8[..7], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six", 777)));
			Assert.That(t8[..6], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234, "six")));
			Assert.That(t8[..5], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L, -1234)));
			Assert.That(t8[..4], Is.EqualTo(STuple.Create("hello world", 123, false, 1234L)));
			Assert.That(t8[..3], Is.EqualTo(STuple.Create("hello world", 123, false)));
			Assert.That(t8[..2], Is.EqualTo(STuple.Create("hello world", 123)));
			Assert.That(t8[..1], Is.EqualTo(STuple.Create("hello world")));
			Assert.That(t8[1..7], Is.EqualTo(STuple.Create(123, false, 1234L, -1234, "six", 777)));
			Assert.That(t8[1..6], Is.EqualTo(STuple.Create(123, false, 1234L, -1234, "six")));
			Assert.That(t8[1..5], Is.EqualTo(STuple.Create(123, false, 1234L, -1234)));
			Assert.That(t8[1..4], Is.EqualTo(STuple.Create(123, false, 1234L)));
			Assert.That(t8[1..3], Is.EqualTo(STuple.Create(123, false)));
			Assert.That(t8[1..2], Is.EqualTo(STuple.Create(123)));
			Assert.That(t8[2..7], Is.EqualTo(STuple.Create(false, 1234L, -1234, "six", 777)));
			Assert.That(t8[2..6], Is.EqualTo(STuple.Create(false, 1234L, -1234, "six")));
			Assert.That(t8[2..5], Is.EqualTo(STuple.Create(false, 1234L, -1234)));
			Assert.That(t8[2..4], Is.EqualTo(STuple.Create(false, 1234L)));
			Assert.That(t8[2..3], Is.EqualTo(STuple.Create(false)));
			Assert.That(t8[3..7], Is.EqualTo(STuple.Create(1234L, -1234, "six", 777)));
			Assert.That(t8[3..6], Is.EqualTo(STuple.Create(1234L, -1234, "six")));
			Assert.That(t8[3..5], Is.EqualTo(STuple.Create(1234L, -1234)));
			Assert.That(t8[3..4], Is.EqualTo(STuple.Create(1234L)));
			Assert.That(t8[4..7], Is.EqualTo(STuple.Create(-1234, "six", 777)));
			Assert.That(t8[4..6], Is.EqualTo(STuple.Create(-1234, "six")));
			Assert.That(t8[4..5], Is.EqualTo(STuple.Create(-1234)));
			Assert.That(t8[5..7], Is.EqualTo(STuple.Create("six", 777)));
			Assert.That(t8[5..6], Is.EqualTo(STuple.Create("six")));
			Assert.That(t8[6..7], Is.EqualTo(STuple.Create(777)));

			Assert.That(t8.Tail.Count, Is.EqualTo(7));
			Assert.That(t8.Tail.Item1, Is.EqualTo(123));
			Assert.That(t8.Tail.Item2, Is.False);
			Assert.That(t8.Tail.Item3, Is.EqualTo(1234L));
			Assert.That(t8.Tail.Item4, Is.EqualTo(-1234L));
			Assert.That(t8.Tail.Item5, Is.EqualTo("six"));
			Assert.That(t8.Tail.Item6, Is.EqualTo(777));
			Assert.That(t8.Tail.Item7, Is.EqualTo("eight"));

			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666, false, "bar", 777).Append("eight").GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666, false, "bar").Append(777, "eight").GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666, false).Concat(STuple.Create("bar", 777, "eight")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123, true, "foo", 666).Concat(STuple.Create(false, "bar", 777, "eight")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123, true).Concat(STuple.Create("foo", 666, false, "bar", 777, "eight")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123).Concat(STuple.Create(true, "foo", 666, false, "bar", 777, "eight")).GetHashCode()), "Hashcode should be stable");
			Assert.That(STuple.Create(123, true, "foo", 666, false, "bar", 777, "eight").GetHashCode(), Is.EqualTo(STuple.Create(123L, 1, "foo", 666UL, 0, "bar", 777U, "eight").GetHashCode()), "Hashcode should be stable");

			{ // Deconstruct
				t8.Deconstruct(out string item1, out int item2, out bool item3, out long item4, out long item5, out string item6, out int item7, out string item8);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234L));
				Assert.That(item6, Is.EqualTo("six"));
				Assert.That(item7, Is.EqualTo(777));
				Assert.That(item8, Is.EqualTo("eight"));
			}
			{ // Deconstruct
				(string item1, int item2, bool item3, long item4, int item5, string item6, int item7, string item8) = t8;
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234L));
				Assert.That(item5, Is.EqualTo(-1234L));
				Assert.That(item6, Is.EqualTo("six"));
				Assert.That(item7, Is.EqualTo(777));
				Assert.That(item8, Is.EqualTo("eight"));
			}

			Assert.Multiple(() =>
			{
				var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8);

				Assert.That(t.Equals(t), Is.True);
				Assert.That(t.Equals((IVarTuple) t), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Default), Is.True);
				Assert.That(((IStructuralEquatable) t).Equals(t, SimilarValueComparer.Relaxed), Is.True);
				Assert.That(t.Equals(t.ToValueTuple()), Is.True);

				Assert.That(t.CompareTo(t), Is.Zero);
				Assert.That(t.CompareTo((IVarTuple) t), Is.Zero);
				Assert.That(((IComparable) t).CompareTo(t), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) t).CompareTo(t, SimilarValueComparer.Relaxed), Is.Zero);
				Assert.That(t.CompareTo(t.ToValueTuple()), Is.Zero);

				Assert.That(t.CompareTo(STuple.Create(2, 2, 3, 4, 5, 6, 7, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 3, 3, 4, 5, 6, 7, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 4, 4, 5, 6, 7, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 5, 5, 6, 7, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 6, 6, 7, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 7, 7, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 8, 8)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 7, 9)), Is.LessThan(0));
				Assert.That(t.CompareTo(STuple.Create(0, 2, 3, 4, 5, 6, 7, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 1, 3, 4, 5, 6, 7, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 2, 4, 5, 6, 7, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 3, 5, 6, 7, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 4, 6, 7, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 5, 7, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 6, 8)), Is.GreaterThan(0));
				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 7, 7)), Is.GreaterThan(0));

				Assert.That(t.CompareTo(STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9)), Is.LessThan(0));
				Assert.That(STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9).CompareTo(t), Is.GreaterThan(0));
			});
		}

		[Test]
		public void Test_Tuple_Many()
		{
			// ReSharper disable once RedundantExplicitParamsArrayCreation
			var tn = STuple.Create((object[]) [ "hello world", 123, false, 1234L, -1234, "six", true, Math.PI ]);
			Log(tn);
			Assert.That(tn.Count, Is.EqualTo(8));
			Assert.That(tn.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tn.Get<int>(1), Is.EqualTo(123));
			Assert.That(tn.Get<bool>(2), Is.False);
			Assert.That(tn.Get<int>(3), Is.EqualTo(1234));
			Assert.That(tn.Get<long>(4), Is.EqualTo(-1234));
			Assert.That(tn.Get<string>(5), Is.EqualTo("six"));
			Assert.That(tn.Get<bool>(6), Is.True);
			Assert.That(tn.Get<double>(7), Is.EqualTo(Math.PI));
			Assert.That(tn.ToArray(), Is.EqualTo(new object[] { "hello world", 123, false, 1234L, -1234, "six", true, Math.PI }));
			Assert.That(tn.ToString(), Is.EqualTo("(\"hello world\", 123, false, 1234, -1234, \"six\", true, " + Math.PI.ToString("R")+ ")"));
			Assert.That(tn, Is.InstanceOf<ListTuple<object?>>());

			Assert.That(tn[^1], Is.EqualTo(Math.PI));
			Assert.That(tn[^2], Is.EqualTo(true));
			Assert.That(tn[^8], Is.EqualTo("hello world"));
			Assert.That(tn[..], Is.EqualTo(tn));
			Assert.That(tn[2..6], Is.EqualTo(STuple.Create(false, 1234L, -1234, "six")));

			{ // Deconstruct
				string item1;
				int item2;
				bool item3;
				long item4;
				long item5;
				string item6;
				bool item7;
				double item8;

				Assert.That(() => tn.Deconstruct(out item1), Throws.InvalidOperationException);
				Assert.That(() => tn.Deconstruct(out item1, out item2), Throws.InvalidOperationException);
				Assert.That(() => tn.Deconstruct(out item1, out item2, out item3), Throws.InvalidOperationException);
				Assert.That(() => tn.Deconstruct(out item1, out item2, out item3, out item4), Throws.InvalidOperationException);
				Assert.That(() => tn.Deconstruct(out item1, out item2, out item3, out item4, out item5), Throws.InvalidOperationException);
				Assert.That(() => tn.Deconstruct(out item1, out item2, out item3, out item4, out item5, out item6), Throws.InvalidOperationException);
				Assert.That(() => tn.Deconstruct(out item1, out item2, out item3, out item4, out item5, out item6, out item7), Throws.InvalidOperationException);

				tn.Deconstruct(out item1, out item2, out item3, out item4, out item5, out item6, out item7, out item8);
				Assert.That(item1, Is.EqualTo("hello world"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(1234));
				Assert.That(item5, Is.EqualTo(-1234));
				Assert.That(item6, Is.EqualTo("six"));
				Assert.That(item7, Is.True);
				Assert.That(item8, Is.EqualTo(Math.PI));
			}

			Assert.That(tn.Concat(STuple.Create("foo", "bar")), Is.EqualTo(STuple.Create(new object?[] { "hello world", 123, false, 1234L, -1234, "six", true, Math.PI, "foo", "bar" })));
		}

		[Test]
		public void Test_Tuple_Wrap()
		{
			// STuple.Wrap(...) does not copy the items of the array

			var arr = new object[] { "Hello", 123, false, TimeSpan.FromSeconds(5) };

			var t = STuple.Wrap(arr);
			Log(t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(4));
			Assert.That(t[0], Is.EqualTo("Hello"));
			Assert.That(t[1], Is.EqualTo(123));
			Assert.That(t[2], Is.False);
			Assert.That(t[3], Is.EqualTo(TimeSpan.FromSeconds(5)));

			{ // Deconstruct
				t.Deconstruct(out string item1, out int item2, out bool item3, out TimeSpan item4);
				Assert.That(item1, Is.EqualTo("Hello"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			}

			t = STuple.Wrap(arr, 1, 2);
			Log(t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo(123));
			Assert.That(t[1], Is.False);

			// changing the underyling array should change the tuple
			// DON'T DO THIS IN ACTUAL CODE!!!

			arr[1] = 456;
			arr[2] = true;
			Log(t);

			Assert.That(t[0], Is.EqualTo(456));
			Assert.That(t[1], Is.True);

			{ // Deconstruct
				t.Deconstruct(out int item1, out bool item2);
				Assert.That(item1, Is.EqualTo(456));
				Assert.That(item2, Is.True);
			}

		}

		[Test]
		public void Test_Tuple_Linked()
		{
			Assert.Multiple(() =>
			{
				var t = new LinkedTuple<string>(STuple.Create("Hello", 123, true), "World");
				Assert.That(t.Count, Is.EqualTo(4));

				Assert.That(t[0], Is.EqualTo("Hello"));
				Assert.That(t[1], Is.EqualTo(123));
				Assert.That(t[2], Is.EqualTo(true));
				Assert.That(t[3], Is.EqualTo("World"));

				Assert.That(t.Get<string>(0), Is.EqualTo("Hello"));
				Assert.That(t.Get<int>(1), Is.EqualTo(123));
				Assert.That(t.Get<bool>(2), Is.EqualTo(true));
				Assert.That(t.Get<string>(3), Is.EqualTo("World"));

				Assert.That(t.Last, Is.EqualTo("World"));
				Assert.That(t.GetFirst<string>(), Is.EqualTo("Hello"));
				Assert.That(t.GetLast<string>(), Is.EqualTo("World"));

				Assert.That(t, Is.EqualTo(STuple.Create("Hello", 123, true, "World")));
				Assert.That(t, Is.Not.EqualTo(STuple.Create("Hello", 123, true, "There")));
				Assert.That(t, Is.Not.EqualTo(STuple.Create("Hello", 123, "World")));

				Assert.That(t.ToString(), Is.EqualTo("(\"Hello\", 123, true, \"World\")"));

				Assert.That(t.GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true, "World").GetHashCode()));

				Assert.That(t.ToArray(), Is.EqualTo((object?[])[ "Hello", 123, true, "World" ]));
			});
		}

		[Test]
		public void Test_Tuple_Joined()
		{
			Assert.Multiple(() =>
			{
				var t = new JoinedTuple<STuple<string, int>, STuple<bool, string>>(STuple.Create("Hello", 123), STuple.Create(true, "World"));
				Assert.That(t.Count, Is.EqualTo(4));

				Assert.That(t[0], Is.EqualTo("Hello"));
				Assert.That(t[1], Is.EqualTo(123));
				Assert.That(t[2], Is.EqualTo(true));
				Assert.That(t[3], Is.EqualTo("World"));

				Assert.That(t.Get<string>(0), Is.EqualTo("Hello"));
				Assert.That(t.Get<int>(1), Is.EqualTo(123));
				Assert.That(t.Get<bool>(2), Is.EqualTo(true));
				Assert.That(t.Get<string>(3), Is.EqualTo("World"));

				Assert.That(t.GetFirst<string>(), Is.EqualTo("Hello"));
				Assert.That(t.GetLast<string>(), Is.EqualTo("World"));

				Assert.That(t, Is.EqualTo(STuple.Create("Hello", 123, true, "World")));
				Assert.That(t, Is.Not.EqualTo(STuple.Create("Hello", 123, true, "There")));
				Assert.That(t, Is.Not.EqualTo(STuple.Create("Hello", 123, true)));
				Assert.That(t, Is.Not.EqualTo(STuple.Create("Hello", 124, true, "World")));
				Assert.That(t, Is.Not.EqualTo(STuple.Create("Hello", true, "World")));

				Assert.That(t.ToString(), Is.EqualTo("(\"Hello\", 123, true, \"World\")"));

				Assert.That(t.GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true, "World").GetHashCode()));

				Assert.That(t.ToArray(), Is.EqualTo((object?[])[ "Hello", 123, true, "World" ]));
			});
		}

		[Test]
		public void Test_Tuple_SpanTuple()
		{
			Assert.Multiple(() =>
			{
				var packed = TuPack.Pack(STuple.Create("Hello", 123, true, "World"));
				
				var t = SpanTuple.Unpack(packed);
				Assert.That(t.Count, Is.EqualTo(4));

				Assert.That(t[0], Is.EqualTo("Hello"));
				Assert.That(t[1], Is.EqualTo(123));
				Assert.That(t[2], Is.EqualTo(true));
				Assert.That(t[3], Is.EqualTo("World"));

				Assert.That(t.Get<string>(0), Is.EqualTo("Hello"));
				Assert.That(t.Get<int>(1), Is.EqualTo(123));
				Assert.That(t.Get<bool>(2), Is.EqualTo(true));
				Assert.That(t.Get<string>(3), Is.EqualTo("World"));

				Assert.That(t.GetFirst<string>(), Is.EqualTo("Hello"));
				Assert.That(t.GetLast<string>(), Is.EqualTo("World"));

				Assert.That(t.ToString(), Is.EqualTo("(\"Hello\", 123, true, \"World\")"));

				Assert.That(t.Equals(STuple.Create("Hello", 123, true, "World")), Is.True);
				Assert.That(t.Equals(STuple.Create("Hello", 123, true, "There")), Is.False);
				Assert.That(t.Equals(STuple.Create("Hello", 123, true)), Is.False);
				Assert.That(t.Equals(STuple.Create("Hello", 124, true, "World")), Is.False);
				Assert.That(t.Equals(STuple.Create("Hello", true, "World")), Is.False);

				Assert.That(t[..3].Equals(STuple.Create("Hello", 123, true)), Is.True);
				Assert.That(t[..2].Equals(STuple.Create("Hello", 123)), Is.True);
				Assert.That(t[..1].Equals(STuple.Create("Hello")), Is.True);
				Assert.That(t[..0].Equals(STuple.Create()), Is.True);
				Assert.That(t[1..].Equals(STuple.Create(123, true, "World")), Is.True);
				Assert.That(t[2..].Equals(STuple.Create(true, "World")), Is.True);
				Assert.That(t[3..].Equals(STuple.Create("World")), Is.True);
				Assert.That(t[4..].Equals(STuple.Create()), Is.True);

				Assert.That(t.GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true, "World").GetHashCode()));
				Assert.That(t[..3].GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true).GetHashCode()));
				Assert.That(t[..2].GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123).GetHashCode()));
				Assert.That(t[..1].GetHashCode(), Is.EqualTo(STuple.Create("Hello").GetHashCode()));
				Assert.That(t[..0].GetHashCode(), Is.EqualTo(STuple.Create().GetHashCode()));

				Assert.That(t.ToArray(), Is.EqualTo((object?[]) [ "Hello", 123, true, "World" ]));
			});
		}

		[Test]
		public void Test_Tuple_SlicedTuple()
		{
			Assert.Multiple(() =>
			{
				var packed = TuPack.Pack(STuple.Create("Hello", 123, true, "World"));
				
				var t = SlicedTuple.Unpack(packed);
				Assert.That(t.Count, Is.EqualTo(4));

				Assert.That(t[0], Is.EqualTo("Hello"));
				Assert.That(t[1], Is.EqualTo(123));
				Assert.That(t[2], Is.EqualTo(true));
				Assert.That(t[3], Is.EqualTo("World"));

				Assert.That(t.Get<string>(0), Is.EqualTo("Hello"));
				Assert.That(t.Get<int>(1), Is.EqualTo(123));
				Assert.That(t.Get<bool>(2), Is.EqualTo(true));
				Assert.That(t.Get<string>(3), Is.EqualTo("World"));

				Assert.That(t.GetFirst<string>(), Is.EqualTo("Hello"));
				Assert.That(t.GetLast<string>(), Is.EqualTo("World"));

				Assert.That(t.ToString(), Is.EqualTo("(\"Hello\", 123, true, \"World\")"));

				Assert.That(t.Equals(STuple.Create("Hello", 123, true, "World")), Is.True);
				Assert.That(t.Equals(STuple.Create("Hello", 123, true, "There")), Is.False);
				Assert.That(t.Equals(STuple.Create("Hello", 123, true)), Is.False);
				Assert.That(t.Equals(STuple.Create("Hello", 124, true, "World")), Is.False);
				Assert.That(t.Equals(STuple.Create("Hello", true, "World")), Is.False);

				Assert.That(t[..3].Equals(STuple.Create("Hello", 123, true)), Is.True);
				Assert.That(t[..2].Equals(STuple.Create("Hello", 123)), Is.True);
				Assert.That(t[..1].Equals(STuple.Create("Hello")), Is.True);
				Assert.That(t[..0].Equals(STuple.Create()), Is.True);
				Assert.That(t[1..].Equals(STuple.Create(123, true, "World")), Is.True);
				Assert.That(t[2..].Equals(STuple.Create(true, "World")), Is.True);
				Assert.That(t[3..].Equals(STuple.Create("World")), Is.True);
				Assert.That(t[4..].Equals(STuple.Create()), Is.True);

				Assert.That(t.GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true, "World").GetHashCode()));
				Assert.That(t[..3].GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123, true).GetHashCode()));
				Assert.That(t[..2].GetHashCode(), Is.EqualTo(STuple.Create("Hello", 123).GetHashCode()));
				Assert.That(t[..1].GetHashCode(), Is.EqualTo(STuple.Create("Hello").GetHashCode()));
				Assert.That(t[..0].GetHashCode(), Is.EqualTo(STuple.Create().GetHashCode()));

				Assert.That(t.ToArray(), Is.EqualTo((object?[]) [ "Hello", 123, true, "World" ]));
			});
		}

		[Test]
		public void Test_Tuple_FromObjects()
		{
			// STuple.FromObjects(...) does a copy of the items of the array

			var arr = new object[] { "Hello", 123, false, TimeSpan.FromSeconds(5) };

			var t = STuple.FromObjects(arr);
			Log(t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(4));
			Assert.That(t[0], Is.EqualTo("Hello"));
			Assert.That(t[1], Is.EqualTo(123));
			Assert.That(t[2], Is.False);
			Assert.That(t[3], Is.EqualTo(TimeSpan.FromSeconds(5)));

			{ // Deconstruct
				t.Deconstruct(out string item1, out int item2, out bool item3, out TimeSpan item4);
				Assert.That(item1, Is.EqualTo("Hello"));
				Assert.That(item2, Is.EqualTo(123));
				Assert.That(item3, Is.False);
				Assert.That(item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			}

			t = STuple.FromObjects(arr, 1, 2);
			Log(t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo(123));
			Assert.That(t[1], Is.False);

			{ // Deconstruct
				t.Deconstruct(out int item1, out bool item2);
				Assert.That(item1, Is.EqualTo(123));
				Assert.That(item2, Is.False);
			}

			// changing the underyling array should NOT change the tuple

			arr[1] = 456;
			arr[2] = true;
			Log(t);

			Assert.That(t[0], Is.EqualTo(123));
			Assert.That(t[1], Is.False);

		}

		[Test]
		public void Test_Tuple_FromArray()
		{
			var items = new[] { "Bonjour", "le", "Monde" };

			var t = STuple.FromArray(items);
			Log(t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(3));
			Assert.That(t[0], Is.EqualTo("Bonjour"));
			Assert.That(t[1], Is.EqualTo("le"));
			Assert.That(t[2], Is.EqualTo("Monde"));

			{ // Deconstruct
				t.Deconstruct(out string item1, out string item2, out string item3);
				Assert.That(item1, Is.EqualTo("Bonjour"));
				Assert.That(item2, Is.EqualTo("le"));
				Assert.That(item3, Is.EqualTo("Monde"));
			}

			t = STuple.FromArray(items, 1, 2);
			Log(t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo("le"));
			Assert.That(t[1], Is.EqualTo("Monde"));
			{ // Deconstruct
				t.Deconstruct(out string item1, out string item2);
				Assert.That(item1, Is.EqualTo("le"));
				Assert.That(item2, Is.EqualTo("Monde"));
			}

			// changing the underlying array should NOT change the tuple
			items[1] = "ze";
			Log(t);

			Assert.That(t[0], Is.EqualTo("le"));
		}

		[Test]
		public void Test_Tuple_Negative_Indexing()
		{
			//note: we would like to phase out negative indexing in the future, but for now we have to still support and test it!

			Assert.Multiple(() =>
			{
				var t1 = STuple.Create("hello world");
				Log(t1);
				Assert.That(t1.Get<string>(-1), Is.EqualTo("hello world"));
				Assert.That(((IVarTuple) t1)[^1], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var t2 = STuple.Create("hello world", 123);
				Log(t2);
				Assert.That(t2.Get<int>(-1), Is.EqualTo(123));
				Assert.That(t2.Get<string>(-2), Is.EqualTo("hello world"));
				Assert.That(t2.Get<int>(^1), Is.EqualTo(123));
				Assert.That(t2.Get<string>(^2), Is.EqualTo("hello world"));
				Assert.That(((IVarTuple) t2)[^1], Is.EqualTo(123));
				Assert.That(((IVarTuple) t2)[^2], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var t3 = STuple.Create("hello world", 123, false);
				Log(t3);
				Assert.That(t3.Get<bool>(-1), Is.False);
				Assert.That(t3.Get<int>(-2), Is.EqualTo(123));
				Assert.That(t3.Get<string>(-3), Is.EqualTo("hello world"));
				Assert.That(t3.Get<bool>(^1), Is.False);
				Assert.That(t3.Get<int>(^2), Is.EqualTo(123));
				Assert.That(t3.Get<string>(^3), Is.EqualTo("hello world"));
				Assert.That(((IVarTuple) t3)[^1], Is.False);
				Assert.That(((IVarTuple) t3)[^2], Is.EqualTo(123));
				Assert.That(((IVarTuple) t3)[^3], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var t4 = STuple.Create("hello world", 123, false, 1234L);
				Log(t4);
				Assert.That(t4.Get<long>(-1), Is.EqualTo(1234L));
				Assert.That(t4.Get<bool>(-2), Is.False);
				Assert.That(t4.Get<int>(-3), Is.EqualTo(123));
				Assert.That(t4.Get<string>(-4), Is.EqualTo("hello world"));
				Assert.That(t4.Get<long>(^1), Is.EqualTo(1234L));
				Assert.That(t4.Get<bool>(^2), Is.False);
				Assert.That(t4.Get<int>(^3), Is.EqualTo(123));
				Assert.That(t4.Get<string>(^4), Is.EqualTo("hello world"));
				Assert.That(((IVarTuple) t4)[^1], Is.EqualTo(1234L));
				Assert.That(((IVarTuple) t4)[^2], Is.False);
				Assert.That(((IVarTuple) t4)[^3], Is.EqualTo(123));
				Assert.That(((IVarTuple) t4)[^4], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var t5 = STuple.Create("hello world", 123, false, 1234L, -1234);
				Log(t5);
				Assert.That(t5.Get<long>(-1), Is.EqualTo(-1234));
				Assert.That(t5.Get<long>(-2), Is.EqualTo(1234L));
				Assert.That(t5.Get<bool>(-3), Is.False);
				Assert.That(t5.Get<int>(-4), Is.EqualTo(123));
				Assert.That(t5.Get<string>(-5), Is.EqualTo("hello world"));
				Assert.That(t5.Get<long>(^1), Is.EqualTo(-1234));
				Assert.That(t5.Get<long>(^2), Is.EqualTo(1234L));
				Assert.That(t5.Get<bool>(^3), Is.False);
				Assert.That(t5.Get<int>(^4), Is.EqualTo(123));
				Assert.That(t5.Get<string>(^5), Is.EqualTo("hello world"));
				Assert.That(((IVarTuple) t5)[^1], Is.EqualTo(-1234));
				Assert.That(((IVarTuple) t5)[^2], Is.EqualTo(1234L));
				Assert.That(((IVarTuple) t5)[^3], Is.False);
				Assert.That(((IVarTuple) t5)[^4], Is.EqualTo(123));
				Assert.That(((IVarTuple) t5)[^5], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var t6 = STuple.Create("hello world", 123, false, 1234L, -1234, "six");
				Log(t6);
				Assert.That(t6.Get<string>(-1), Is.EqualTo("six"));
				Assert.That(t6.Get<long>(-2), Is.EqualTo(-1234));
				Assert.That(t6.Get<long>(-3), Is.EqualTo(1234L));
				Assert.That(t6.Get<bool>(-4), Is.False);
				Assert.That(t6.Get<int>(-5), Is.EqualTo(123));
				Assert.That(t6.Get<string>(-6), Is.EqualTo("hello world"));
				Assert.That(t6.Get<string>(^1), Is.EqualTo("six"));
				Assert.That(t6.Get<long>(^2), Is.EqualTo(-1234));
				Assert.That(t6.Get<long>(^3), Is.EqualTo(1234L));
				Assert.That(t6.Get<bool>(^4), Is.False);
				Assert.That(t6.Get<int>(^5), Is.EqualTo(123));
				Assert.That(t6.Get<string>(^6), Is.EqualTo("hello world"));
				Assert.That(((IVarTuple) t6)[^1], Is.EqualTo("six"));
				Assert.That(((IVarTuple) t6)[^2], Is.EqualTo(-1234));
				Assert.That(((IVarTuple) t6)[^3], Is.EqualTo(1234L));
				Assert.That(((IVarTuple) t6)[^4], Is.False);
				Assert.That(((IVarTuple) t6)[^5], Is.EqualTo(123));
				Assert.That(((IVarTuple) t6)[^6], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var tn = STuple.Create((object[]) [ "hello world", 123, false, 1234, -1234, "six" ]);
				Log(tn);
				Assert.That(tn.Get<string>(^1), Is.EqualTo("six"));
				Assert.That(tn.Get<int>(^2), Is.EqualTo(-1234));
				Assert.That(tn.Get<long>(^3), Is.EqualTo(1234));
				Assert.That(tn.Get<bool>(^4), Is.False);
				Assert.That(tn.Get<int>(^5), Is.EqualTo(123));
				Assert.That(tn.Get<string>(^6), Is.EqualTo("hello world"));
				Assert.That(tn[^1], Is.EqualTo("six"));
				Assert.That(tn[^2], Is.EqualTo(-1234));
				Assert.That(tn[^3], Is.EqualTo(1234));
				Assert.That(tn[^4], Is.False);
				Assert.That(tn[^5], Is.EqualTo(123));
				Assert.That(tn[^6], Is.EqualTo("hello world"));
			});

			Assert.Multiple(() =>
			{
				var tn = STuple.Create([ "hello world", 123, false, 1234, -1234, "six" ]);
				Log(tn);
				Assert.That(tn.Get<string>(^1), Is.EqualTo("six"));
				Assert.That(tn.Get<int>(^2), Is.EqualTo(-1234));
				Assert.That(tn.Get<long>(^3), Is.EqualTo(1234));
				Assert.That(tn.Get<bool>(^4), Is.False);
				Assert.That(tn.Get<int>(^5), Is.EqualTo(123));
				Assert.That(tn.Get<string>(^6), Is.EqualTo("hello world"));
				Assert.That(tn[^1], Is.EqualTo("six"));
				Assert.That(tn[^2], Is.EqualTo(-1234));
				Assert.That(tn[^3], Is.EqualTo(1234));
				Assert.That(tn[^4], Is.False);
				Assert.That(tn[^5], Is.EqualTo(123));
				Assert.That(tn[^6], Is.EqualTo("hello world"));
			});
		}

		[Test]
		public void Test_Tuple_First_And_Last()
		{
			// tuple.First<T>() should be equivalent to tuple.Get<T>(0)
			// tuple.Last<T>() should be equivalent to tuple.Get<T>(^1)

			var t1 = STuple.Create(1);
			Log(t1);
			Assert.That(((IVarTuple) t1).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t1).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t1).GetLast<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t1).GetLast<string>(), Is.EqualTo("1"));

			var t2 = STuple.Create(1, 2);
			Log(t2);
			Assert.That(t2.Last, Is.EqualTo(2));
			Assert.That(((IVarTuple) t2).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t2).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t2).GetLast<int>(), Is.EqualTo(2));
			Assert.That(((IVarTuple) t2).GetLast<string>(), Is.EqualTo("2"));

			var t3 = STuple.Create(1, 2, 3);
			Log(t3);
			Assert.That(t3.Last, Is.EqualTo(3));
			Assert.That(((IVarTuple) t3).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t3).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t3).GetLast<int>(), Is.EqualTo(3));
			Assert.That(((IVarTuple) t3).GetLast<string>(), Is.EqualTo("3"));

			var t4 = STuple.Create(1, 2, 3, 4);
			Log(t4);
			Assert.That(t4.Last, Is.EqualTo(4));
			Assert.That(((IVarTuple) t4).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t4).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t4).GetLast<int>(), Is.EqualTo(4));
			Assert.That(((IVarTuple) t4).GetLast<string>(), Is.EqualTo("4"));

			var t5 = STuple.Create(1, 2, 3, 4, 5);
			Log(t5);
			Assert.That(t5.Last, Is.EqualTo(5));
			Assert.That(((IVarTuple) t5).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t5).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t5).GetLast<int>(), Is.EqualTo(5));
			Assert.That(((IVarTuple) t5).GetLast<string>(), Is.EqualTo("5"));

			var t6 = STuple.Create(1, 2, 3, 4, 5, 6);
			Log(t6);
			Assert.That(t6.Last, Is.EqualTo(6));
			Assert.That(((IVarTuple) t6).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t6).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t6).GetLast<int>(), Is.EqualTo(6));
			Assert.That(((IVarTuple) t6).GetLast<string>(), Is.EqualTo("6"));

			var t7 = STuple.Create(1, 2, 3, 4, 5, 6, 7);
			Log(t7);
			Assert.That(t7.Last, Is.EqualTo(7));
			Assert.That(((IVarTuple) t7).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t7).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t7).GetLast<int>(), Is.EqualTo(7));
			Assert.That(((IVarTuple) t7).GetLast<string>(), Is.EqualTo("7"));

			var t8 = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8);
			Log(t8);
			Assert.That(((IVarTuple) t8).GetFirst<int>(), Is.EqualTo(1));
			Assert.That(((IVarTuple) t8).GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(((IVarTuple) t8).GetLast<int>(), Is.EqualTo(8));
			Assert.That(((IVarTuple) t8).GetLast<string>(), Is.EqualTo("8"));

			var tn = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9);
			Log(tn);
			Assert.That(tn.GetFirst<int>(), Is.EqualTo(1));
			Assert.That(tn.GetFirst<string>(), Is.EqualTo("1"));
			Assert.That(tn.GetLast<int>(), Is.EqualTo(9));
			Assert.That(tn.GetLast<string>(), Is.EqualTo("9"));

			Assert.That(() => STuple.Empty.GetFirst<string>(), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => STuple.Empty.GetLast<string>(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_Tuple_CreateBoxed()
		{
			IVarTuple tuple;

			tuple = STuple.CreateBoxed(null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.Null);

			tuple = STuple.CreateBoxed(1);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(1));

			tuple = STuple.CreateBoxed(1L);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(1L));

			tuple = STuple.CreateBoxed(false);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.False);

			tuple = STuple.CreateBoxed("hello");
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo("hello"));

			tuple = STuple.CreateBoxed(new byte[] { 1, 2, 3 });
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(new byte[] { 1, 2, 3 }.AsSlice()));
		}

		[Test]
		public void Test_Tuple_Embedded_Tuples()
		{
			// (A,B).Append((C,D)) should return (A,B,(C,D)) (length 3) and not (A,B,C,D) (length 4)

			var x = STuple.Create("A", "B");
			var y = STuple.Create("C", "D");

			// using the instance method that returns a STuple<T1, T2, T3>
			IVarTuple z = x.Append(y);
			Log(z);
			Assert.That(z, Is.Not.Null);
			Assert.That(z.Count, Is.EqualTo(3));
			Assert.That(z[0], Is.EqualTo("A"));
			Assert.That(z[1], Is.EqualTo("B"));
			Assert.That(z[2], Is.EqualTo(y));
			var t = z.Get<IVarTuple>(2);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo("C"));
			Assert.That(t[1], Is.EqualTo("D"));

			// cast down to the interface ITuple
			z = ((IVarTuple)x).Append((IVarTuple)y);
			Log(z);
			Assert.That(z, Is.Not.Null);
			Assert.That(z.Count, Is.EqualTo(3));
			Assert.That(z[0], Is.EqualTo("A"));
			Assert.That(z[1], Is.EqualTo("B"));
			Assert.That(z[2], Is.EqualTo(y));
			t = z.Get<IVarTuple>(2);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo("C"));
			Assert.That(t[1], Is.EqualTo("D"));

			// composite index key "(prefix, value, id)"
			IVarTuple subspace = STuple.Create(123, 42);
			IVarTuple value = STuple.Create(2014, 11, 6); // Indexing a date value (Y, M, D)
			const string ID = "Doc123";
			z = subspace.Append(value, ID);
			Log(z);
			Assert.That(z.Count, Is.EqualTo(4));

		}

		[Test]
		public void Test_Tuples_CompareTo()
		{
			Assert.Multiple(() =>
			{
				var x = STuple.Create("A", "B");
				var y = STuple.Create("C", "D");

				// CompareTo(STuple)
				Assert.That(x.CompareTo(x), Is.Zero);
				Assert.That(x.CompareTo(y), Is.LessThan(0));
				Assert.That(y.CompareTo(x), Is.GreaterThan(0));

				// CompareTo(IVarTuple)
				Assert.That(x.CompareTo((IVarTuple) x), Is.Zero);
				Assert.That(x.CompareTo((IVarTuple) y), Is.LessThan(0));
				Assert.That(y.CompareTo((IVarTuple) x), Is.GreaterThan(0));
				Assert.That(x.CompareTo(STuple.Create("A", "B", "C")), Is.LessThan(0));
				Assert.That(x.CompareTo(STuple.Create("A")), Is.GreaterThan(0));

				// CompareTo(ValueTuple)
				Assert.That(x.CompareTo(("A", "B")), Is.Zero);
				Assert.That(x.CompareTo(("C", "D")), Is.LessThan(0));
				Assert.That(y.CompareTo(("A", "B")), Is.GreaterThan(0));

				// CompareTo(object)
				Assert.That(((IComparable) x).CompareTo(x), Is.Zero);
				Assert.That(((IComparable) x).CompareTo(y), Is.LessThan(0));
				Assert.That(((IComparable) y).CompareTo(x), Is.GreaterThan(0));
				Assert.That(((IComparable) x).CompareTo(STuple.Create("A", "B", "C")), Is.LessThan(0));
				Assert.That(((IComparable) x).CompareTo(STuple.Create("A")), Is.GreaterThan(0));

				// CompareTo(object, IComparer)
				Assert.That(((IStructuralComparable) x).CompareTo(x, SimilarValueComparer.Default), Is.Zero);
				Assert.That(((IStructuralComparable) x).CompareTo(y, SimilarValueComparer.Default), Is.LessThan(0));
				Assert.That(((IStructuralComparable) y).CompareTo(x, SimilarValueComparer.Default), Is.GreaterThan(0));
				Assert.That(((IStructuralComparable) x).CompareTo(STuple.Create("A", "B", "C"), SimilarValueComparer.Default), Is.LessThan(0));
				Assert.That(((IStructuralComparable) x).CompareTo(STuple.Create("A"), SimilarValueComparer.Default), Is.GreaterThan(0));
			});

			Assert.Multiple(() =>
			{
				var t123 = STuple.Create(1, 2, 3);
				Assert.That(t123.CompareTo(STuple.Create(1.0d, 2.0f, 3.0m)), Is.Zero);
				Assert.That(t123.CompareTo(STuple.Create(1.1d, 2.0d, 3.0d)), Is.LessThan(0));
				Assert.That(t123.CompareTo(STuple.Create(0.9d, 2.0d, 3.0d)), Is.GreaterThan(0));
				Assert.That(t123.CompareTo(STuple.Create(1.0d, 2.1d, 3.0d)), Is.LessThan(0));
				Assert.That(t123.CompareTo(STuple.Create(1.0d, 1.9d, 3.0d)), Is.GreaterThan(0));
				Assert.That(t123.CompareTo(STuple.Create(1.0d, 2.0d, 3.1d)), Is.LessThan(0));
				Assert.That(t123.CompareTo(STuple.Create(1.0d, 2.0d, 2.9d)), Is.GreaterThan(0));

				var tAbc = STuple.Create("a", "b", "c");
				Assert.That(tAbc.CompareTo(tAbc), Is.Zero);
				Assert.That(tAbc.CompareTo(STuple.Create("a", "b", "d")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "b", "b")), Is.GreaterThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "b", "cc")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "bb", "c")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("aa", "bb", "c")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("aa")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("`")), Is.GreaterThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "ba")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "a")), Is.GreaterThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "b", "ca")), Is.LessThan(0));
				Assert.That(tAbc.CompareTo(STuple.Create("a", "b", "b")), Is.GreaterThan(0));

				// ints after null
				Assert.That(t123.CompareTo(STuple.Create(null, 2, 3)), Is.GreaterThan(0));
				// ints after bytes
				Assert.That(t123.CompareTo(STuple.Create(Slice.FromInt32(1), 2, 3)), Is.GreaterThan(0));
				// ints after strings
				Assert.That(t123.CompareTo(STuple.Create("1", 2, 3)), Is.GreaterThan(0));
				// ints same as floats
				Assert.That(t123.CompareTo(STuple.Create(1.0, 2, 3)), Is.Zero);
				// ints before bools
				Assert.That(t123.CompareTo(STuple.Create(true, 2, 3)), Is.LessThan(0));
				// ints before Guids
				Assert.That(t123.CompareTo(STuple.Create(Guid.Empty, 2, 3)), Is.LessThan(0));
				// ints before VersionStamps
				Assert.That(t123.CompareTo(STuple.Create(VersionStamp.None, 2, 3)), Is.LessThan(0));
			});
		}

		[Test]
		public void Test_Tuple_With()
		{
			//note: important to always cast to (ITuple) to be sure that we don't call specialized instance methods (tested elsewhere)
			IVarTuple t;
			bool called;

			// Size 1

			t = STuple.Create(123);
			Log(t);
			called = false;
			t.With((int a) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a) =>
			{
				Assert.That(a, Is.EqualTo(123));
				return 42;
			}), Is.EqualTo(42));
			Assert.That(() => t.With((int _) => throw new InvalidOperationException("BOOM")), Throws.InvalidOperationException.With.Message.EqualTo("BOOM"));

			// Size 2

			t = t.Append("abc");
			Log(t);
			called = false;
			t.With((int a, string b) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				return 42;
			}), Is.EqualTo(42));

			// Size 3

			t = t.Append(3.14f);
			Log(t);
			called = false;
			t.With((int a, string b, float c) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b, float c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				return 42;
			}), Is.EqualTo(42));

			// Size 4

			t = t.Append(true);
			Log(t);
			called = false;
			t.With((int a, string b, float c, bool d) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b, float c, bool d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				return 42;
			}), Is.EqualTo(42));

			// Size 5

			t = t.Append('z');
			Log(t);
			called = false;
			t.With((int a, string b, float c, bool d, char e) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b, float c, bool d, char e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				return 42;
			}), Is.EqualTo(42));

			// Size 6

			t = t.Append(Math.PI);
			Log(t);
			called = false;
			t.With((int a, string b, float c, bool d, char e, double f) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b, float c, bool d, char e, double f) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				return 42;
			}), Is.EqualTo(42));

			// Size 7

			t = t.Append(IPAddress.Loopback);
			Log(t);
			called = false;
			t.With((int a, string b, float c, bool d, char e, double f, IPAddress g) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b, float c, bool d, char e, double f, IPAddress g) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
				return 42;
			}), Is.EqualTo(42));

			// Size 8

			t = t.Append(DateTime.MaxValue);
			Log(t);
			called = false;
			t.With((int a, string b, float c, bool d, char e, double f, IPAddress g, DateTime h) =>
			{
				called = true;
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
				Assert.That(h, Is.EqualTo(DateTime.MaxValue));
			});
			Assert.That(called, Is.True);
			Assert.That(t.With((int a, string b, float c, bool d, char e, double f, IPAddress g, DateTime h) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
				Assert.That(h, Is.EqualTo(DateTime.MaxValue));
				return 42;
			}), Is.EqualTo(42));
		}

		[Test]
		public void Test_Tuple_With_Struct()
		{
			// calling With() on the structs is faster

			var t1 = STuple.Create(123);
			Log(t1);
			t1.With((a) =>
			{
				Assert.That(a, Is.EqualTo(123));
			});
			Assert.That(t1.With((a) =>
			{
				Assert.That(a, Is.EqualTo(123));
				return 42;
			}), Is.EqualTo(42));

			var t2 = STuple.Create(123, "abc");
			Log(t2);
			t2.With((a, b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
			});
			Assert.That(t2.With((a, b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				return 42;
			}), Is.EqualTo(42));

			var t3 = STuple.Create(123, "abc", 3.14f);
			Log(t3);
			t3.With((a, b, c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
			});
			Assert.That(t3.With((a, b, c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				return 42;
			}), Is.EqualTo(42));

			var t4 = STuple.Create(123, "abc", 3.14f, true);
			Log(t4);
			t4.With((a, b, c, d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
			});
			Assert.That(t4.With((a, b, c, d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				return 42;
			}), Is.EqualTo(42));

			var t5 = STuple.Create(123, "abc", 3.14f, true, 'z');
			Log(t5);
			t5.With((a, b, c, d, e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
			});
			Assert.That(t5.With((a, b, c, d, e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				return 42;
			}), Is.EqualTo(42));

			var t6 = STuple.Create(123, "abc", 3.14f, true, 'z', DateTime.MaxValue);
			Log(t6);
			t6.With((a, b, c, d, e, f) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(DateTime.MaxValue));
			});
			Assert.That(t6.With((a, b, c, d, e, f) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(DateTime.MaxValue));
				return 42;
			}), Is.EqualTo(42));

			var t7 = STuple.Create(123, "abc", 3.14f, true, 'z', DateTime.MaxValue, false);
			Log(t7);
			t7.With((a, b, c, d, e, f, g) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(DateTime.MaxValue));
				Assert.That(g, Is.False);
			});
			Assert.That(t7.With((a, b, c, d, e, f, g) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(DateTime.MaxValue));
				Assert.That(g, Is.False);
				return 42;
			}), Is.EqualTo(42));
		}

		[Test]
		public void Test_Tuple_Of_Size()
		{
			// OfSize(n) check the size and return the tuple if it passed
			// VerifySize(n) only check the size
			// Both should throw if tuple is null, or not the expected size

			void Verify(IVarTuple t)
			{
				Log(t);
				for (int i = 0; i <= 10; i++)
				{
					if (t.Count > i)
					{
						Assert.That(() => t.OfSize(i), Throws.InstanceOf<InvalidOperationException>());
						Assert.That(t.OfSizeAtLeast(i), Is.SameAs(t));
						Assert.That(() => t.OfSizeAtMost(i), Throws.InstanceOf<InvalidOperationException>());
					}
					else if (t.Count < i)
					{
						Assert.That(() => t.OfSize(i), Throws.InstanceOf<InvalidOperationException>());
						Assert.That(() => t.OfSizeAtLeast(i), Throws.InstanceOf<InvalidOperationException>());
						Assert.That(t.OfSizeAtMost(i), Is.SameAs(t));
					}
					else
					{
						Assert.That(t.OfSize(i), Is.SameAs(t));
						Assert.That(t.OfSizeAtLeast(i), Is.SameAs(t));
						Assert.That(t.OfSizeAtMost(i), Is.SameAs(t));
					}
				}
			}

			Verify(STuple.Empty);
			Verify(STuple.Create(123));
			Verify(STuple.Create(123, "abc"));
			Verify(STuple.Create(123, "abc", 3.14f));
			Verify(STuple.Create(123, "abc", 3.14f, true));
			Verify(STuple.Create(123, "abc", 3.14f, true, 'z'));
			Verify(STuple.FromArray(new[] { "hello", "world", "!" }));
			Verify(STuple.FromEnumerable(Enumerable.Range(0, 10)));

			Verify(STuple.Create(123, "abc", 3.14f, true, 'z')[0, 2]);
			Verify(STuple.Create(123, "abc", 3.14f, true, 'z')[1, 4]);
			Verify(STuple.FromEnumerable(Enumerable.Range(0, 50)).Substring(15, 6));

			// ReSharper disable ExpressionIsAlwaysNull
			IVarTuple? none = null;
			Assert.That(() => none.OfSize(0), Throws.ArgumentNullException);
			Assert.That(() => none.OfSizeAtLeast(0), Throws.ArgumentNullException);
			Assert.That(() => none.OfSizeAtMost(0), Throws.ArgumentNullException);
			// ReSharper restore ExpressionIsAlwaysNull
		}

		[Test]
		public void Test_Tuple_Truncate()
		{
			IVarTuple t = STuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5), "World");
			Log(t);

			var head = t.Truncate(1);
			Log(head);
			Assert.That(head, Is.Not.Null);
			Assert.That(head.Count, Is.EqualTo(1));
			Assert.That(head[0], Is.EqualTo("Hello"));

			head = t.Truncate(2);
			Log(head);
			Assert.That(head, Is.Not.Null);
			Assert.That(head.Count, Is.EqualTo(2));
			Assert.That(head[0], Is.EqualTo("Hello"));
			Assert.That(head[1], Is.EqualTo(123));

			head = t.Truncate(5);
			Log(head);
			Assert.That(head, Is.EqualTo(t));

			var tail = t.Truncate(-1);
			Log(tail);
			Assert.That(tail, Is.Not.Null);
			Assert.That(tail.Count, Is.EqualTo(1));
			Assert.That(tail[0], Is.EqualTo("World"));

			tail = t.Truncate(-2);
			Log(tail);
			Assert.That(tail, Is.Not.Null);
			Assert.That(tail.Count, Is.EqualTo(2));
			Assert.That(tail[0], Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(tail[1], Is.EqualTo("World"));

			tail = t.Truncate(-5);
			Log(tail);
			Assert.That(tail, Is.EqualTo(t));

			Assert.That(t.Truncate(0), Is.EqualTo(STuple.Empty));
			Assert.That(() => t.Truncate(6), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => t.Truncate(-6), Throws.InstanceOf<InvalidOperationException>());

			Assert.That(() => STuple.Empty.Truncate(1), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => STuple.Create("Hello", "World").Truncate(3), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => STuple.Create("Hello", "World").Truncate(-3), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_Tuple_As()
		{
			// ITuple.As<...>() adds types to an untyped ITuple
			IVarTuple t;

			t = STuple.Create("Hello");
			var t1 = t.As<string>();
			Assert.That(t1.Item1, Is.EqualTo("Hello"));

			t = STuple.Create("Hello", 123);
			Assert.That(t.As<string, int>(), Is.EqualTo(("Hello", 123)));

			t = STuple.Create("Hello", 123, false);
			Assert.That(t.As<string, int, bool>(), Is.EqualTo(("Hello", 123, false)));

			t = STuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5));
			Assert.That(t.As<string, int, bool, TimeSpan>(), Is.EqualTo(("Hello", 123, false, TimeSpan.FromSeconds(5))));

			t = STuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5), "World");
			Assert.That(t.As<string, int, bool, TimeSpan, string>(), Is.EqualTo(("Hello", 123, false, TimeSpan.FromSeconds(5), "World")));
		}

		[Test]
		public void Test_Cast_To_BCL_Tuples()
		{
			// implicit: Tuple => ITuple
			// explicit: ITuple => Tuple

			var t1 = STuple.Create("Hello");
			var b1 = (Tuple<string>) t1; // explicit
			Assert.That(b1, Is.Not.Null);
			Assert.That(b1.Item1, Is.EqualTo("Hello"));
			ValueTuple<string> r1 = t1; // implicit
			Assert.That(r1.Item1, Is.EqualTo("Hello"));

			var t2 = STuple.Create("Hello", 123);
			var b2 = (Tuple<string, int>)t2;	// explicit
			Assert.That(b2, Is.Not.Null);
			Assert.That(b2.Item1, Is.EqualTo("Hello"));
			Assert.That(b2.Item2, Is.EqualTo(123));
			ValueTuple<string, int> r2 = t2; // implicit
			Assert.That(r2.Item1, Is.EqualTo("Hello"));
			Assert.That(r2.Item2, Is.EqualTo(123));

			var t3 = STuple.Create("Hello", 123, false);
			var b3 = (Tuple<string, int, bool>)t3;	// explicit
			Assert.That(b3, Is.Not.Null);
			Assert.That(b3.Item1, Is.EqualTo("Hello"));
			Assert.That(b3.Item2, Is.EqualTo(123));
			Assert.That(b3.Item3, Is.False);
			ValueTuple<string, int, bool> r3 = t3; // implicit
			Assert.That(r3.Item1, Is.EqualTo("Hello"));
			Assert.That(r3.Item2, Is.EqualTo(123));
			Assert.That(r3.Item3, Is.False);

			var t4 = STuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5));
			var b4 = (Tuple<string, int, bool, TimeSpan>)t4;	// explicit
			Assert.That(b4, Is.Not.Null);
			Assert.That(b4.Item1, Is.EqualTo("Hello"));
			Assert.That(b4.Item2, Is.EqualTo(123));
			Assert.That(b4.Item3, Is.False);
			Assert.That(b4.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			ValueTuple<string, int, bool, TimeSpan> r4 = t4; // implicit
			Assert.That(r4.Item1, Is.EqualTo("Hello"));
			Assert.That(r4.Item2, Is.EqualTo(123));
			Assert.That(r4.Item3, Is.False);
			Assert.That(r4.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));

			var t5 = STuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5), "World");
			var b5 = (Tuple<string?, int, bool, TimeSpan, string?>) t5;	// explicit
			Assert.That(b5, Is.Not.Null);
			Assert.That(b5.Item1, Is.EqualTo("Hello"));
			Assert.That(b5.Item2, Is.EqualTo(123));
			Assert.That(b5.Item3, Is.False);
			Assert.That(b5.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(b5.Item5, Is.EqualTo("World"));
			ValueTuple<string, int, bool, TimeSpan, string> r5 = t5; // implicit
			Assert.That(r5.Item1, Is.EqualTo("Hello"));
			Assert.That(r5.Item2, Is.EqualTo(123));
			Assert.That(r5.Item3, Is.False);
			Assert.That(r5.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(r5.Item5, Is.EqualTo("World"));

		}

		[Test]
		public void Test_Tuple_Stringify()
		{
			// typed tuples
			Assert.That(STuple.Empty.ToString(), Is.EqualTo("()"));
			Assert.That(STuple.Create<string>("hello world").ToString(), Is.EqualTo(@"(""hello world"",)"));
			Assert.That(STuple.Create<bool>(true).ToString(), Is.EqualTo("(true,)"));
			Assert.That(STuple.Create<int>(123).ToString(), Is.EqualTo("(123,)"));
			Assert.That(STuple.Create<uint>(123U).ToString(), Is.EqualTo("(123,)"));
			Assert.That(STuple.Create<long>(123L).ToString(), Is.EqualTo("(123,)"));
			Assert.That(STuple.Create<ulong>(123UL).ToString(), Is.EqualTo("(123,)"));
			Assert.That(STuple.Create<double>(123.4d).ToString(), Is.EqualTo("(123.4,)"));
			Assert.That(STuple.Create<float>(123.4f).ToString(), Is.EqualTo("(123.4,)"));
			Assert.That(STuple.Create<Guid>(Guid.Parse("102cb0aa-2151-4c72-9e9d-61cf2980cbd0")).ToString(), Is.EqualTo("({102cb0aa-2151-4c72-9e9d-61cf2980cbd0},)"));
			Assert.That(STuple.Create<Uuid128>(Uuid128.Parse("102cb0aa-2151-4c72-9e9d-61cf2980cbd0")).ToString(), Is.EqualTo("({102cb0aa-2151-4c72-9e9d-61cf2980cbd0},)"));
			Assert.That(STuple.Create<Uuid64>(Uuid64.Parse("102cb0aa-21514c72")).ToString(), Is.EqualTo("({102CB0AA-21514C72},)"));
			Assert.That(STuple.Create<byte[]>([ 0x02, 0x41, 0x42, 0x43, 0x00 ]).ToString(), Is.EqualTo("(`<02>ABC<00>`,)"));
			Assert.That(STuple.Create<Slice>(Slice.FromBytes([ 0x02, 0x41, 0x42, 0x43, 0x00 ])).ToString(), Is.EqualTo("(`<02>ABC<00>`,)"));

			Assert.That(STuple.Create("Hello", 123, "World", '!', false).ToString(), Is.EqualTo("""("Hello", 123, "World", '!', false)"""));
		}

		[Test]
		public void Test_Tuple_Formatter_ToString()
		{

			static void Verify<TTuple>(TTuple t, string expected, string? expectedParsed = null)
				where TTuple : IVarTuple
			{
				if (expectedParsed != null)
				{
					Log($"# ({typeof(TTuple).GetFriendlyName()}) `{expected}` => `{expectedParsed}`");
				}
				else
				{
					Log($"# ({typeof(TTuple).GetFriendlyName()}) `{expected}`");
					expectedParsed = expected;
				}

				Assert.That(t.ToString(), Is.EqualTo(expected));

				var sb = new FastStringBuilder(new char[100]);

				STuple.Formatter.ToString(ref sb, t);
				Assert.That(sb.ToString(), Is.EqualTo(expected));

				sb.Append("*prefix*");
				STuple.Formatter.ToString(ref sb, t);
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				STuple.Formatter.ToString(ref sb, new ListTuple<object?>(t.ToArray()));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				STuple.Formatter.ToString(ref sb, t.ToArray().AsSpan());
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				STuple.Formatter.ToString(ref sb, (IEnumerable<object?>) t.ToArray());
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				STuple.Formatter.ToString(ref sb, t.ToList());
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				STuple.Formatter.ToString(ref sb, t.Select(x => x));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				// for SlicedTuple and SpanTuple, we have to serialize and parse back

				var packed = TuPack.Pack(t);

				SpanTuple st = SpanTuple.Unpack(packed);
				sb.Append("*prefix*");
				st.ToString(ref sb);
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expectedParsed));

				SlicedTuple sliced = SlicedTuple.Unpack(packed);
				sb.Append("*prefix*");
				sliced.ToString(ref sb);
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expectedParsed));
			}

			Verify(STuple.Empty, "()");
			Verify(new STuple(), "()");
			Verify(STuple.Create("hello"), "(\"hello\",)");
			Verify(STuple.Create(123), "(123,)");
			Verify(STuple.Create(true), "(true,)");
			Verify(STuple.Create(false), "(false,)");
			Verify(STuple.Create("hello", 123), "(\"hello\", 123)");
			Verify(STuple.Create("hello", 123, true), "(\"hello\", 123, true)");
			Verify(STuple.Create("hello", 123, true, "world"), "(\"hello\", 123, true, \"world\")");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ")");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ", false)");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false, 1.23d), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ", false, 1.23)");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false, 1.23d, 4.56f), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ", false, 1.23, 4.56)");

#if NET5_0_OR_GREATER
			var dt = DateTime.Now;
			Verify(STuple.Create(dt), $"(\"{dt:O}\",)", $"({(dt.ToUniversalTime() - new DateTime(1970,1,1, 0, 0, 0, DateTimeKind.Utc)).TotalDays:R},)");

			var dto = DateTimeOffset.Now;
			Verify(STuple.Create(dto), $"(\"{dto:O}\",)", $"({(dto - new DateTime(1970,1,1, 0, 0, 0, DateTimeKind.Utc)).TotalDays:R},)");
#else
			// the netfx double parser is imprecise in the last ulp, so the value reparsed from the formatted date
			// cannot reliably be compared against the recomputed one
#endif

			var g = Guid.NewGuid();
			Verify(STuple.Create(g), $"({g:B},)");

			var u128 = Uuid128.NewUuid();
			Verify(STuple.Create(u128), $"({u128:B},)");

			var u96 = Uuid96.NewUuid();
			Verify(STuple.Create(u96), $"({u96:B},)", $"({VersionStamp.FromUuid96(u96)},)");

			var u80 = Uuid80.NewUuid();
			Verify(STuple.Create(u80), $"({u80:B},)", $"({VersionStamp.FromUuid80(u80)},)");

			var u64 = Uuid64.NewUuid();
			Verify(STuple.Create(u64), $"({u64:B},)");

			var u48 = Uuid48.NewUuid();
			Verify(STuple.Create(u48), $"({u48:B},)", $"({u48.ToUInt64()},)");

			var vs = VersionStamp.FromUuid96(u96);
			Verify(STuple.Create(vs), $"({vs},)");

			var ip = IPAddress.Parse("192.168.1.23");
			Verify(STuple.Create(ip), "('192.168.1.23',)", "(`<C0><A8><01><17>`,)");
		}

		[Test]
		public void Test_Tuple_Formatter_AppendTokens()
		{

			static void Verify<TTuple>(TTuple t, string expected)
				where TTuple : IVarTuple
			{
				var sb = new FastStringBuilder(new char[100]);

				sb.Append("*prefix*");
				Assert.That(STuple.Formatter.AppendItemsTo(ref sb, t), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				Assert.That(STuple.Formatter.AppendItemsTo(ref sb, new ListTuple<object?>(t.ToArray())), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				Assert.That(STuple.Formatter.AppendItemsTo(ref sb, t.ToArray().AsSpan()), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				Assert.That(STuple.Formatter.AppendItemsTo(ref sb, (IEnumerable<object?>) t.ToArray()), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				Assert.That(STuple.Formatter.AppendItemsTo(ref sb, t.ToList()), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				sb.Append("*prefix*");
				Assert.That(STuple.Formatter.AppendItemsTo(ref sb, t.Select(x => x)), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));

				// for SpanTuple, we have to serialize and parse back
				SpanTuple st = TuPack.Unpack(TuPack.Pack(t).Span);
				sb.Append("*prefix*");
				Assert.That(st.AppendItemsTo(ref sb), Is.EqualTo(t.Count));
				Assert.That(sb.ToString(), Is.EqualTo("*prefix*" + expected));
			}

			Verify(STuple.Empty, "");
			Verify(new STuple(), "");
			Verify(STuple.Create("hello"), "\"hello\"");
			Verify(STuple.Create(123), "123");
			Verify(STuple.Create(true), "true");
			Verify(STuple.Create("hello", 123), "\"hello\", 123");
			Verify(STuple.Create("hello", 123, true), "\"hello\", 123, true");
			Verify(STuple.Create("hello", 123, true, "world"), "\"hello\", 123, true, \"world\"");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI), "\"hello\", 123, true, \"world\", " + PiDoubleText + "");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false), "\"hello\", 123, true, \"world\", " + PiDoubleText + ", false");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false, 1.23d), "\"hello\", 123, true, \"world\", " + PiDoubleText + ", false, 1.23");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false, 1.23d, 4.56f), "\"hello\", 123, true, \"world\", " + PiDoubleText + ", false, 1.23, 4.56");
		}

		[Test]
		public void Test_Tuple_ISpanFormattable_TryFormat()
		{

			static void Verify<TTuple>(TTuple t, string expected)
				where TTuple : IVarTuple, ISpanFormattable
			{
				Log($"# ({typeof(TTuple).GetFriendlyName()}) `{expected}` [{expected.Length}]");

				// string interpolation
				Assert.That($"***{t}***", Is.EqualTo("***" + expected + "***"));

				var buffer = new char[expected.Length + 32];

				// buffer with more space
				{
					var span = buffer.AsSpan();
					span.Fill('_');
					Assert.That(t.TryFormat(span, out var charsWritten, default, null), Is.True.WithOutput(charsWritten).EqualTo(expected.Length));
					Assert.That(span[..charsWritten].ToString(), Is.EqualTo(expected));
					Log($"> {span.Length:D02} `{span}`");
					Assert.That(buffer.AsSpan(span.Length).ContainsAnyExcept('∅'), Is.False);
				}

				// buffer with exact space
				{
					buffer.AsSpan().Fill('∅');
					var span = buffer.AsSpan(0, expected.Length);
					span.Fill('_');
					Assert.That(t.TryFormat(span, out var charsWritten, default, null), Is.True.WithOutput(charsWritten).EqualTo(expected.Length));
					Assert.That(span[..charsWritten].ToString(), Is.EqualTo(expected));
					Log($"> {span.Length:D02} `{span}`");
					Assert.That(buffer.AsSpan(span.Length).ContainsAnyExcept('∅'), Is.False);
				}

				// all buffer sizes that are not enough
				for (int i = expected.Length - 1; i >= 0; i--)
				{
					buffer.AsSpan().Fill('∅');
					var span = buffer.AsSpan(0, i);
					span.Fill('_');
					Assert.That(t.TryFormat(span, out var charsWritten, default, null), Is.False.WithOutput(charsWritten).Zero, $"{expected}.TryFormat([{i}]) with buffer too small should fail");
					Log($"> {span.Length:D02} `{span}`");
					Assert.That(buffer.AsSpan(span.Length).ContainsAnyExcept('∅'), Is.False);
				}
			}

			Verify(new STuple(), "()");
			Verify(STuple.Create("hello"), "(\"hello\",)");
			Verify(STuple.Create(123), "(123,)");
			Verify(STuple.Create(true), "(true,)");
			Verify(STuple.Create(false), "(false,)");
			Verify(STuple.Create("hello", 123), "(\"hello\", 123)");
			Verify(STuple.Create("hello", 123, true), "(\"hello\", 123, true)");
			Verify(STuple.Create("hello", 123, true, "world"), "(\"hello\", 123, true, \"world\")");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ")");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ", false)");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false, 1.23d), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ", false, 1.23)");
			Verify(STuple.Create("hello", 123, true, "world", Math.PI, false, 1.23d, 4.56f), "(\"hello\", 123, true, \"world\", " + PiDoubleText + ", false, 1.23, 4.56)");

			var dt = DateTime.Now;
			Verify(STuple.Create(dt), $"(\"{dt:O}\",)");

			var dto = DateTimeOffset.Now;
			Verify(STuple.Create(dto), $"(\"{dto:O}\",)");

			var g = Guid.NewGuid();
			Verify(STuple.Create(g), $"({g:B},)");

			var u128 = Uuid128.NewUuid();
			Verify(STuple.Create(u128), $"({u128:B},)");

			var u96 = Uuid96.NewUuid();
			Verify(STuple.Create(u96), $"({u96:B},)");

			var u80 = Uuid80.NewUuid();
			Verify(STuple.Create(u80), $"({u80:B},)");

			var u64 = Uuid64.NewUuid();
			Verify(STuple.Create(u64), $"({u64:B},)");

			var u48 = Uuid48.NewUuid();
			Verify(STuple.Create(u48), $"({u48:B},)");

			var vs = VersionStamp.FromUuid96(u96);
			Verify(STuple.Create(vs), $"({vs},)");

			Verify(STuple.Create(IPAddress.Loopback), "('127.0.0.1',)");
			Verify(STuple.Create(IPAddress.Parse("192.168.1.23")), "('192.168.1.23',)");
			Verify(STuple.Create(IPAddress.IPv6Loopback), "('::1',)");
		}

		#endregion

		#region Splicing...

		private static void VerifyTuple(string message, IVarTuple t, object?[] expected)
		{
			// count
			if (t.Count != expected.Length)
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				Assert.Fail($"{message}: Count mismatch between observed {t} and expected {STuple.Formatter.ToString(expected.AsSpan())} for tuple of type {t.GetType().Name}");
			}

			// direct access
			for (int i = 0; i < expected.Length; i++)
			{
				Assert.That(ComparisonHelper.AreSimilarStrict(t[i], expected[i]), Is.True, $"{message}: t[{i}] != expected[{i}]");
			}

			// iterator
			int p = 0;
			foreach (var obj in t)
			{
				if (p >= expected.Length) Assert.Fail($"Spliced iterator overshoot at t[{p}] = {obj}");
				Assert.That(ComparisonHelper.AreSimilarStrict(obj, expected[p]), Is.True, $"{message}: Iterator[{p}], {obj} ~= {expected[p]}");
				++p;
			}
			Assert.That(p, Is.EqualTo(expected.Length), $"{message}: t.GetEnumerator() returned only {p} elements out of {expected.Length} expected");

			// CopyTo
			var tmp = new object?[expected.Length];
			t.CopyTo(tmp, 0);
			for (int i = 0; i < tmp.Length; i++)
			{
				Assert.That(ComparisonHelper.AreSimilarStrict(tmp[i], expected[i]), Is.True, $"{message}: CopyTo[{i}], {tmp[i]} ~= {expected[i]}");
			}

			// Memoize
			//tmp = t.Memoize().ToArray();
			//for (int i = 0; i < tmp.Length; i++)
			//{
			//	Assert.That(ComparisonHelper.AreSimilarStrict(tmp[i], expected[i]), Is.True, "{0}: Memoize.Items[{1}], {2} ~= {3}", message, i, tmp[i], expected[i]);
			//}

			// Append
			//if (!(t is SlicedTuple))
			{
				var u = t.Append("last");
				Assert.That(u.Get<string>(^1), Is.EqualTo("last"));
				tmp = u.ToArray();
				for (int i = 0; i < tmp.Length - 1; i++)
				{
					Assert.That(ComparisonHelper.AreSimilarStrict(tmp[i], expected[i]), Is.True, $"{message}: Appended[{i}], {tmp[i]} ~= {expected[i]}");
				}
			}
		}

		[Test]
		public void Test_Can_Splice_ListTuple()
		{
			var items = new object[] { "hello", "world", 123, "foo", 456, "bar" };
			//                            0        1      2     3     4     5
			//                           -6       -5     -4    -3    -2    -1

			var tuple = new ListTuple<object?>(items);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(6));

			// get all
			VerifyTuple("[:]", tuple[..], items);
			VerifyTuple("[:]", tuple[..6], items);
			VerifyTuple("[:]", tuple[^6..], items);
			VerifyTuple("[:]", tuple[^6..6], items);

			// tail
			VerifyTuple("[n:]", tuple[4..], [ 456, "bar" ]);
			VerifyTuple("[n:+]", tuple[4..6], [ 456, "bar" ]);
			VerifyTuple("[-n:+]", tuple[^2..6], [ 456, "bar" ]);
			VerifyTuple("[-n:-]", tuple[^2..], [ 456, "bar" ]);

			// head
			VerifyTuple("[:n]", tuple[..3], [ "hello", "world", 123 ]);
			VerifyTuple("[0:-n]", tuple[..^3], [ "hello", "world", 123 ]);
			VerifyTuple("[-:n]", tuple[^6..3], [ "hello", "world", 123 ]);
			VerifyTuple("[-:-n]", tuple[^6..^3], [ "hello", "world", 123 ]);

			// single
			VerifyTuple("[0:1]", tuple[0..1], [ "hello" ]);
			VerifyTuple("[-6:-5]", tuple[^6..^5], [ "hello" ]);
			VerifyTuple("[1:2]", tuple[1..2], [ "world" ]);
			VerifyTuple("[-5:-4]", tuple[^5..^4], [ "world" ]);
			VerifyTuple("[5:6]", tuple[5, 6], [ "bar" ]);
			VerifyTuple("[-1:]", tuple[^1..], [ "bar" ]);

			// chunk
			VerifyTuple("[2:4]", tuple[2..4], [ 123, "foo" ]);
			VerifyTuple("[2:-2]", tuple[2..^2], [ 123, "foo" ]);
			VerifyTuple("[-4:4]", tuple[^4..4], [ 123, "foo" ]);
			VerifyTuple("[-4:-2]", tuple[^4..^2], [ 123, "foo" ]);

			// remove first
			VerifyTuple("[1:]", tuple[1..], [ "world", 123, "foo", 456, "bar" ]);
			VerifyTuple("[1:+]", tuple[1..6], [ "world", 123, "foo", 456, "bar" ]);
			VerifyTuple("[-5:]", tuple[^5..], [ "world", 123, "foo", 456, "bar" ]);
			VerifyTuple("[-5:+]", tuple[^5..6], [ "world", 123, "foo", 456, "bar" ]);

			// remove last
			VerifyTuple("[:5]", tuple[..5], [ "hello", "world", 123, "foo", 456 ]);
			VerifyTuple("[:-1]", tuple[..^1], [ "hello", "world", 123, "foo", 456 ]);

			// out of range
			Assert.That(() => tuple[2..7], Throws.Exception);
			Assert.That(() => tuple[2..42], Throws.Exception);
			Assert.That(() => tuple[2..123456], Throws.Exception);
			Assert.That(() => tuple[^7..2], Throws.Exception);
			Assert.That(() => tuple[^42..2], Throws.Exception);
		}

		private static object[] GetRange(int fromIncluded, int toExcluded, int count)
		{
			if (count == 0) return [ ];

			if (fromIncluded < 0) fromIncluded += count;
			if (toExcluded < 0) toExcluded += count;

			if (toExcluded > count) toExcluded = count;
			var tmp = new object[toExcluded - fromIncluded];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = new string((char) (65 + fromIncluded + i), 1);
			return tmp;
		}

		[Test]
		public void Test_Randomized_Splices()
		{
			// Test a random mix of sizes, and indexes...

			const int N = 100 * 1000;

			var tuples = new IVarTuple[14];
			tuples[0] = STuple.Empty;
			tuples[1] = STuple.Create("A");
			tuples[2] = STuple.Create("A", "B");
			tuples[3] = STuple.Create("A", "B", "C");
			tuples[4] = STuple.Create("A", "B", "C", "D");
			tuples[5] = STuple.Create("A", "B", "C", "D", "E");
			tuples[6] = STuple.Create("A", "B", "C", "D", "E", "F");
			tuples[7] = STuple.Create("A", "B", "C", "D", "E", "F", "G");
			tuples[8] = STuple.Create("A", "B", "C", "D", "E", "F", "G", "H");
			tuples[9] = STuple.Create("A", "B", "C", "D", "E", "F", "G", "H", "I");
			tuples[10]= STuple.Create("A", "B", "C", "D", "E", "F", "G", "H", "I", "J");
			tuples[11] = new JoinedTuple<IVarTuple, STuple<string, string, string, string, string>>(tuples[6], STuple.Create("G", "H", "I", "J", "K"));
			tuples[12] = new LinkedTuple<string>(tuples[11], "L");
			tuples[13] = new LinkedTuple<string>(STuple.Create("A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L"), "M");

#if false
			LogPartial("Checking tuples");

			foreach (var tuple in tuples)
			{
				var t = STuple.Unpack(tuple.ToSlice());
				Assert.That(t.Equals(tuple), Is.True, t.ToString() + " != unpack(" + tuple.ToString() + ")");
			}
#endif

			var rnd = new Random(123456);

			Log($"Generating {N:N0} random tuples:");
			for (int i = 0; i < N; i++)
			{
				if (i % 1000 == 0) Log($"- {100.0 * i / N:N1} %");
				var len = rnd.Next(1, tuples.Length);
				var tuple = tuples[len];
				if (tuple.Count != len)
				{
					Assert.That(tuple.Count, Is.EqualTo(len), $"Invalid length for tuple {tuple}");
				}

				var prefix = tuple.ToString();

				switch (rnd.Next(6))
				{
					case 0:
					{ // [:+rnd]
						int x = rnd.Next(len);
						VerifyTuple($"{prefix}[:{x}]", tuple[..x], GetRange(0, x, len));
						break;
					}
					case 1:
					{ // [+rnd:]
						int x = rnd.Next(len);
						VerifyTuple($"{prefix}[{x}:]", tuple[x..], GetRange(x, int.MaxValue, len));
						break;
					}
					case 2:
					{ // [:^rnd]
						int x = 1 + rnd.Next(len);
						var p = new Index(x, fromEnd: true);
						VerifyTuple($"{prefix}[:{x}]", tuple[..p], GetRange(0, len - x, len));
						break;
					}
					case 3:
					{ // [^rnd:]
						int x = 1 + rnd.Next(len);
						var p = new Index(x, fromEnd: true);
						VerifyTuple($"{prefix}[{x}:]", tuple[p..], GetRange(len - x, int.MaxValue, len));
						break;
					}
					case 4:
					{ // [rnd:rnd]
						int x = rnd.Next(len);
						int y;
						do { y = rnd.Next(len); } while (y < x);
						VerifyTuple($"{prefix} [{x}:{y}]", tuple[x..y], GetRange(x, y, len));
						break;
					}
					case 5:
					{ // [^rnd:^rnd]
						int x = 1 + rnd.Next(len);
						var px = new Index(x, fromEnd: true);

						int y;
						do { y = 1 + rnd.Next(len); } while (y > x);
						var py = new Index(y, fromEnd: true);
						VerifyTuple($"{prefix} [{x}:{y}]", tuple[px..py], GetRange(len - x, len - y, len));
						break;
					}
				}

			}
			Log("> success");

		}

		#endregion

		#region Equality / Comparison

		private static void AssertEquality(IVarTuple x, IVarTuple y)
		{
			Assert.That(x.Equals(y), Is.True, "x.Equals(y)");
			Assert.That(x.Equals((object) y), Is.True, "x.Equals((object)y)");
			Assert.That(y.Equals(x), Is.True, "y.Equals(x)");
			Assert.That(y.Equals((object) x), Is.True, "y.Equals((object)y");
		}

		private static void AssertInequality(IVarTuple x, IVarTuple y)
		{
			Assert.That(x.Equals(y), Is.False, "!x.Equals(y)");
			Assert.That(x.Equals((object)y), Is.False, "!x.Equals((object)y)");
			Assert.That(y.Equals(x), Is.False, "!y.Equals(x)");
			Assert.That(y.Equals((object)x), Is.False, "!y.Equals((object)y");
		}

		private static void AssertEqualityRelaxed(IVarTuple x, IVarTuple y)
		{
			Assert.That(x.Equals(y, SimilarValueComparer.Relaxed), Is.True, "x.Equals(y)");
			Assert.That(y.Equals(x, SimilarValueComparer.Relaxed), Is.True, "y.Equals(x)");
		}

		private static void AssertInequalityRelaxed(IVarTuple x, IVarTuple y)
		{
			Assert.That(x.Equals(y, SimilarValueComparer.Relaxed), Is.False, "!x.Equals(y)");
			Assert.That(y.Equals(x, SimilarValueComparer.Relaxed), Is.False, "!y.Equals(x)");
		}

		[Test]
		public void Test_Tuple_Equals()
		{
			var t1 = STuple.Create(1, 2);
			Log(t1);
			// self equality
			AssertEquality(t1, t1);

			var t2 = STuple.Create(1, 2);
			Log(t2);
			// same type equality
			AssertEquality(t1, t2);

			var t3 = STuple.Create((object[]) [ 1, 2 ]);
			Log(t3);
			// boxed array
			AssertEquality(t1, t3);

			var t4 = STuple.Create([ 1, 2 ]);
			Log(t4);
			// collection expression
			AssertEquality(t1, t4);

			var t5 = STuple.Create(1).Append(2);
			Log(t5);
			// multistep
			AssertEquality(t1, t5);
		}

		[Test]
		public void Test_Tuple_Similar()
		{
			var t1 = STuple.Create(1, 2);
			var t2 = STuple.Create((long) 1, (short) 2);
			var t3 = STuple.Create([ 1, 2L ]);

			AssertEquality(t1, t1);
			AssertEquality(t1, t2);
			AssertEquality(t1, t3);
			AssertEquality(t2, t2);
			AssertEquality(t2, t3);
			AssertEquality(t3, t3);

			AssertEqualityRelaxed(t1, t1);
			AssertEqualityRelaxed(t1, t2);
			AssertEqualityRelaxed(t1, t3);
			AssertEqualityRelaxed(t2, t2);
			AssertEqualityRelaxed(t2, t3);
			AssertEqualityRelaxed(t3, t3);

			var t4 = STuple.Create("1", "2");

			AssertEquality(t4, t4);
			AssertInequality(t1, t4);
			AssertInequality(t2, t4);
			AssertInequality(t4, t3);

			AssertEqualityRelaxed(t4, t4);
			AssertEqualityRelaxed(t1, t4);
			AssertEqualityRelaxed(t2, t4);
			AssertEqualityRelaxed(t4, t3);
		}

		[Test]
		public void Test_Tuple_Not_Equal()
		{
			var t1 = STuple.Create(1, 2);

			var x1 = STuple.Create(2, 1);
			var x2 = STuple.Create("11", "22");
			var x3 = STuple.Create(1, 2, 3);
			//var x4 = STuple.Unpack(Slice.Unescape("<15><01>"));

			AssertInequality(t1, x1);
			AssertInequality(t1, x2);
			AssertInequality(t1, x3);
			//AssertInequality(t1, x4);

			AssertInequality(x1, x2);
			AssertInequality(x1, x3);
			//AssertInequality(x1, x4);
			AssertInequality(x2, x3);
			//AssertInequality(x2, x4);
			//AssertInequality(x3, x4);
		}

		[Test]
		public void Test_Tuple_Substring_Equality()
		{
			IVarTuple x = STuple.FromArray<string>(new[] {"A", "C"});
			IVarTuple y = STuple.FromArray<string>(new[] {"A", "B", "C"});

			Assert.That(x.Substring(0, 1), Is.EqualTo(y.Substring(0, 1)));
			Assert.That(x.Substring(1, 1), Is.EqualTo(y.Substring(2, 1)));

			IVarTuple a = x.Substring(0, 1);
			IVarTuple b = y.Substring(0, 1);
			Assert.That(a.Equals(b), Is.True);
			Assert.That(a.Equals((object)b), Is.True);
			Assert.That(object.Equals(a, b), Is.True);
			Assert.That(STuple.Equals(a, b), Is.True);
			Assert.That(STuple.Equivalent(a, b), Is.True);

			// this is very unfortunate, but 'a == b' does NOT work because ITuple is an interface, and there is no known way to make it work :(
			// ReSharper disable PossibleUnintendedReferenceComparison
			// ReSharper disable CannotApplyEqualityOperatorToType
			Assert.That(a == b, Is.False, "Tuples A and B, even if they contain the same values, are pointers to two different instances on the heap, and should not ReferenceEquals !");
			// ReSharper restore CannotApplyEqualityOperatorToType
			// ReSharper restore PossibleUnintendedReferenceComparison

			// It should work on STuple<...> though (but with a compiler warning)
			var aa = STuple.Create<string>("A");
			var bb = STuple.Create<string>("A");
			// ReSharper disable CannotApplyEqualityOperatorToType
			Assert.That(aa == bb, Is.True, "Operator '==' should work on struct tuples.");
			// ReSharper restore CannotApplyEqualityOperatorToType
			Assert.That(aa.Equals(bb), Is.True, "Equals(..) should work on struct tuples.");
			var cc = STuple.Create<string>(new string('A', 1)); // make sure we have an "A" string that is not the same pointers as the others
			Assert.That(aa.Item1, Is.Not.SameAs(cc.Item1), "Did your compiler optimize the new string('A', 1). If so, need to find another way");
			Assert.That(aa.Equals(cc), Is.True, "Equals(..) should compare the values, not the pointers.");


		}

		[Test]
		public void Test_Tuple_String_AutoCast()
		{
			// 'a' ~= "A"
			AssertEqualityRelaxed(STuple.Create("A"), STuple.Create('A'));
			AssertInequalityRelaxed(STuple.Create("A"), STuple.Create('B'));
			AssertInequalityRelaxed(STuple.Create("A"), STuple.Create('a'));

			// ASCII ~= Unicode
			AssertEqualityRelaxed(STuple.Create("ABC"), STuple.Create(Slice.FromStringAscii("ABC")));
			AssertInequalityRelaxed(STuple.Create("ABC"), STuple.Create(Slice.FromStringAscii("DEF")));
			AssertInequalityRelaxed(STuple.Create("ABC"), STuple.Create(Slice.FromStringAscii("abc")));

			// 'a' ~= ASCII 'a'
			AssertEqualityRelaxed(STuple.Create(Slice.FromStringAscii("A")), STuple.Create('A'));
			AssertInequalityRelaxed(STuple.Create(Slice.FromStringAscii("A")), STuple.Create('B'));
			AssertInequalityRelaxed(STuple.Create(Slice.FromStringAscii("A")), STuple.Create('a'));
		}

		[Test]
		public void Test_Tuple_Comparers()
		{
			{
				var cmp = STuple<int>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create(123), STuple.Create(123)), Is.True, "(123,) == (123,)");
				Assert.That(cmp.Equals(STuple.Create(123), STuple.Create(456)), Is.False, "(123,) != (456,)");
				Assert.That(cmp.GetHashCode(STuple.Create(123)), Is.EqualTo(STuple.Create(123).GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create(123)), Is.Not.EqualTo(STuple.Create(456).GetHashCode()));
			}
			{
				var cmp = STuple<string>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create("foo"), STuple.Create("foo")), Is.True, "('foo',) == ('foo',)");
				Assert.That(cmp.Equals(STuple.Create("foo"), STuple.Create("bar")), Is.False, "('foo',) != ('bar',)");
				Assert.That(cmp.GetHashCode(STuple.Create("foo")), Is.EqualTo(STuple.Create("foo").GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create("foo")), Is.Not.EqualTo(STuple.Create("bar").GetHashCode()));
			}

			{
				var cmp = STuple<string, int>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create("foo", 123), STuple.Create("foo", 123)), Is.True, "('foo',123) == ('foo',123)");
				Assert.That(cmp.Equals(STuple.Create("foo", 123), STuple.Create("bar", 123)), Is.False, "('foo',123) != ('bar',123)");
				Assert.That(cmp.Equals(STuple.Create("foo", 123), STuple.Create("foo", 456)), Is.False, "('foo',123) != ('foo',456)");
				Assert.That(cmp.GetHashCode(STuple.Create("foo", 123)), Is.EqualTo(STuple.Create("foo", 123).GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create("foo", 123)), Is.Not.EqualTo(STuple.Create("foo", 456).GetHashCode()));
			}

			{
				var cmp = STuple<string, bool, int>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123), STuple.Create("foo", true, 123)), Is.True, "('foo',true,123) == ('foo',true,123)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123), STuple.Create("bar", true, 123)), Is.False, "('foo',true,123) != ('bar',true,123)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123), STuple.Create("foo", false, 123)), Is.False, "('foo',true,123) != ('foo',false,123)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123), STuple.Create("foo", true, 456)), Is.False, "('foo',true,123) != ('foo',true,456)");
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123)), Is.EqualTo(STuple.Create("foo", true, 123).GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123)), Is.Not.EqualTo(STuple.Create("foo", true, 456).GetHashCode()));
			}

			{
				var cmp = STuple<string, bool, int, long>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L), STuple.Create("foo", true, 123, -1L)), Is.True, "('foo',true,123,-1) == ('foo',true,123,-1)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L), STuple.Create("bar", true, 123, -1L)), Is.False, "('foo',true,123,-1) != ('bar',true,123,-1)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L), STuple.Create("foo", false, 123, -1L)), Is.False, "('foo',true,123,-1) != ('foo',false,123,-1)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L), STuple.Create("foo", true, 456, -1L)), Is.False, "('foo',true,123,-1) != ('foo',true,456,-1)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L), STuple.Create("foo", true, 123, -2L)), Is.False, "('foo',true,123,-1) != ('foo',true,123,-2)");
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123, -1L)), Is.EqualTo(STuple.Create("foo", true, 123, -1L).GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123, -1L)), Is.Not.EqualTo(STuple.Create("foo", true, 456, 123L).GetHashCode()));
			}

			{
				var cmp = STuple<string, bool, int, long, string>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf"), STuple.Create("foo", true, 123, -1L, "narf")), Is.True, "('foo',true,123,-1) == ('foo',true,123,-1,'narf')");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf"), STuple.Create("bar", true, 123, -1L, "narf")), Is.False, "('foo',true,123,-1) != ('bar',true,123,-1,'narf')");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf"), STuple.Create("foo", false, 123, -1L, "narf")), Is.False, "('foo',true,123,-1) != ('foo',false,123,-1,'narf')");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf"), STuple.Create("foo", true, 456, -1L, "narf")), Is.False, "('foo',true,123,-1) != ('foo',true,456,-1,'narf')");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf"), STuple.Create("foo", true, 123, -2L, "narf")), Is.False, "('foo',true,123,-1) != ('foo',true,123,-2,'narf')");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf"), STuple.Create("foo", true, 123, -1L, "zort")), Is.False, "('foo',true,123,-1) != ('foo',true,123,-1,'zort')");
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123, -1L, "narf")), Is.EqualTo(STuple.Create("foo", true, 123, -1L, "narf").GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123, -1L, "narf")), Is.Not.EqualTo(STuple.Create("foo", true, 123, -1L, "zort").GetHashCode()));
			}

			{
				var cmp = STuple<string, bool, int, long, string, double>.EqualityComparer.Default;
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("foo", true, 123, -1L, "narf", Math.PI)), Is.True, "('foo',true,123,-1) == ('foo',true,123,-1,'narf',PI)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("bar", true, 123, -1L, "narf", Math.PI)), Is.False, "('foo',true,123,-1) != ('bar',true,123,-1,'narf',PI)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("foo", false, 123, -1L, "narf", Math.PI)), Is.False, "('foo',true,123,-1) != ('foo',false,123,-1,'narf',PI)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("foo", true, 456, -1L, "narf", Math.PI)), Is.False, "('foo',true,123,-1) != ('foo',true,456,-1,'narf',PI)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("foo", true, 123, -2L, "narf", Math.PI)), Is.False, "('foo',true,123,-1) != ('foo',true,123,-2,'narf',PI)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("foo", true, 123, -1L, "zort", Math.PI)), Is.False, "('foo',true,123,-1) != ('foo',true,123,-1,'zort',PI)");
				Assert.That(cmp.Equals(STuple.Create("foo", true, 123, -1L, "narf", Math.PI), STuple.Create("foo", true, 123, -1L, "narf", Math.E)), Is.False, "('foo',true,123,-1) != ('foo',true,123,-1,'narf',E)");
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123, -1L, "narf", Math.PI)), Is.EqualTo(STuple.Create("foo", true, 123, -1L, "narf", Math.PI).GetHashCode()));
				Assert.That(cmp.GetHashCode(STuple.Create("foo", true, 123, -1L, "narf", Math.PI)), Is.Not.EqualTo(STuple.Create("foo", true, 123, -1L, "narf", Math.E).GetHashCode()));
			}

		}

		#endregion

		#region Deformatters

		[Test]
		public void Test_Can_Parse_Simple_Tuples()
		{

			static void Check<TTuple>(string expr, TTuple expected) where TTuple : IVarTuple
			{
				Log($"> {expr}");
				var actual = STuple.Deformatter.Parse(expr);
				if (!expected.Equals(actual))
				{
					Log($"- EXPECTED: {expected}");
					Log($"- ACTUAL  : {actual}");
					Log($"- {TuPack.Pack(actual)}");
					Log($"- {TuPack.Pack(expected)}");
					Assert.That(actual, Is.EqualTo(expected), expr);
				}
			}

			Check("()", STuple.Empty);
			Check("(true)", STuple.Create(true));
			Check("(false)", STuple.Create(false));
			Check("(123)", STuple.Create(123));
			Check("(-42)", STuple.Create(-42));
			Check("(123.4)", STuple.Create(123.4d));
			Check("(1E10)", STuple.Create(1E10));
			Check("('x')", STuple.Create('x'));
			Check("(\"Hello World\")", STuple.Create("Hello World"));
			Check("(\"Foo\\\"Bar\\tBaz\")", STuple.Create("Foo\"Bar\tBaz"));
			Check("({4626466c-fdac-4230-af3a-4029fab668ab})", STuple.Create(Guid.Parse("4626466c-fdac-4230-af3a-4029fab668ab")));

			Check("(\"Hello\",123,false)", STuple.Create("Hello", 123, false));
			Check("('M',123456789,{4626466c-fdac-4230-af3a-4029fab668ab})", STuple.Create('M', 123456789, Guid.Parse("4626466c-fdac-4230-af3a-4029fab668ab")));
			Check("(123, true , \"Hello\")", STuple.Create(123, true, "Hello"));

			Check("(\"Hello\",(123,true),\"World!\")", STuple.Create("Hello", STuple.Create(123, true), "World!"));
			Check("(9223372036854775807,)", STuple.Create(long.MaxValue));
			Check("(-9223372036854775808,)", STuple.Create(long.MinValue));
			Check("(18446744073709551615,)", STuple.Create(ulong.MaxValue));
			Check("(3.1415926535897931, 2.7182818284590451)", STuple.Create(Math.PI, Math.E));
			Check("(123E45,-123E-45)", STuple.Create(123E45, -123E-45));

			Check("(|System|)", STuple.Create(TuPackUserType.System));
			Check("(|Directory|)", STuple.Create(TuPackUserType.Directory));
			Check("(|System|,\"Hello\")", STuple.Create(TuPackUserType.System, "Hello"));
			Check("(|Directory|,42,\"Hello\")", STuple.Create(TuPackUserType.Directory, 42, "Hello"));
		}

		#endregion

		#region Bench....

#if false

		[Test]
		public void Bench_Tuple_Unpack_Random()
		{
			const int N = 100 * 1000;

			var FUNKY_ASCII = Slice.FromAscii("bonjour\x00le\x00\xFFmonde");
			string FUNKY_STRING = "hello\x00world";
			string UNICODE_STRING = "héllø 世界";

			LogPartial("Creating {0:N0} random tuples", N);
			var tuples = new List<ITuple>(N);
			var rnd = new Random(777);
			var guids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
			var uuid128s = Enumerable.Range(0, 10).Select(_ => Uuid128.NewUuid()).ToArray();
			var uuid64s = Enumerable.Range(0, 10).Select(_ => Uuid64.NewUuid()).ToArray();
			var fuzz = new byte[1024 + 1000]; rnd.NextBytes(fuzz);
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				ITuple tuple = STuple.Empty;
				int s = 1 + (int)Math.Sqrt(rnd.Next(128));
				if (i % (N / 100) == 0) LogPartial('.');
				for (int j = 0; j < s; j++)
				{
					switch (rnd.Next(17))
					{
						case 0: tuple = tuple.Append<int>(rnd.Next(255)); break;
						case 1: tuple = tuple.Append<int>(-1 - rnd.Next(255)); break;
						case 2: tuple = tuple.Append<int>(256 + rnd.Next(65536 - 256)); break;
						case 3: tuple = tuple.Append<int>(rnd.Next(int.MaxValue)); break;
						case 4: tuple = tuple.Append<long>((rnd.Next(int.MaxValue) << 32) | rnd.Next(int.MaxValue)); break;
						case 5: tuple = tuple.Append(new string('A', 1 + rnd.Next(16))); break;
						case 6: tuple = tuple.Append(new string('B', 8 + (int)Math.Sqrt(rnd.Next(1024)))); break;
						case 7: tuple = tuple.Append<string>(UNICODE_STRING); break;
						case 8: tuple = tuple.Append<string>(FUNKY_STRING); break;
						case 9: tuple = tuple.Append<Slice>(FUNKY_ASCII); break;
						case 10: tuple = tuple.Append<Guid>(guids[rnd.Next(10)]); break;
						case 11: tuple = tuple.Append<Uuid128>(uuid128s[rnd.Next(10)]); break;
						case 12: tuple = tuple.Append<Uuid64>(uuid64s[rnd.Next(10)]); break;
						case 13: tuple = tuple.Append<Slice>(Slice.Create(fuzz, rnd.Next(1000), 1 + (int)Math.Sqrt(rnd.Next(1024)))); break;
						case 14: tuple = tuple.Append(default(string)); break;
						case 15: tuple = tuple.Append<object>("hello"); break;
						case 16: tuple = tuple.Append<bool>(rnd.Next(2) == 0); break;
					}
				}
				tuples.Add(tuple);
			}
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} items", tuples.Sum(x => x.Count));
			Log(" > {0}", tuples[42]);
			Log();

			LogPartial("Packing tuples...");
			sw.Restart();
			var slices = STuple.Pack(tuples);
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} tps", N / sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} bytes", slices.Sum(x => x.Count));
			Log(" > {0}", slices[42]);
			Log();

			LogPartial("Unpacking tuples...");
			sw.Restart();
			var unpacked = slices.Select(slice => STuple.Unpack(slice)).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} tps", N / sw.Elapsed.TotalSeconds);
			Log(" > {0}", unpacked[42]);
			Log();

			LogPartial("Comparing ...");
			sw.Restart();
			tuples.Zip(unpacked, (x, y) => x.Equals(y)).All(b => b);
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log();

			LogPartial("Tuples.ToString ...");
			sw.Restart();
			var strings = tuples.Select(x => x.ToString()).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} chars", strings.Sum(x => x.Length));
			Log(" > {0}", strings[42]);
			Log();

			LogPartial("Unpacked.ToString ...");
			sw.Restart();
			strings = unpacked.Select(x => x.ToString()).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} chars", strings.Sum(x => x.Length));
			Log(" > {0}", strings[42]);
			Log();

			LogPartial("Memoizing ...");
			sw.Restart();
			var memoized = tuples.Select(x => x.Memoize()).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
		}

#endif

		#endregion

		#region System.ValueTuple integration...

		[Test]
		public void Test_Implicit_Cast_STuple_To_ValueTuple()
		{
			{
				ValueTuple<int> t = STuple.Create(11);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
			}
			{
				(int, int) t = STuple.Create(11, 22);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
			}
			{
				(int, int, int) t = STuple.Create(11, 22, 33);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
			}
			{
				(int, int, int, int) t = STuple.Create(11, 22, 33, 44);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
			}
			{
				(int, int, int, int, int) t = STuple.Create(11, 22, 33, 44, 55);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
				Assert.That(t.Item5, Is.EqualTo(55));
			}
			{
				(int, int, int, int, int, int) t = STuple.Create(11, 22, 33, 44, 55, 66);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
				Assert.That(t.Item5, Is.EqualTo(55));
				Assert.That(t.Item6, Is.EqualTo(66));
			}
			{
				(int, int, int, int, int, int, int) t = STuple.Create(11, 22, 33, 44, 55, 66, 77);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
				Assert.That(t.Item5, Is.EqualTo(55));
				Assert.That(t.Item6, Is.EqualTo(66));
				Assert.That(t.Item7, Is.EqualTo(77));
			}
		}

		[Test]
		public void Test_Implicit_Cast_ValueTuple_To_STuple()
		{
			{
				STuple<int> t = ValueTuple.Create(11);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
			}
			{
				STuple<int, int> t = (11, 22);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
			}
			{
				STuple<int, int, int> t = (11, 22, 33);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
			}
			{
				STuple<int, int, int, int> t = (11, 22, 33, 44);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
			}
			{
				STuple<int, int, int, int, int> t = (11, 22, 33, 44, 55);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
				Assert.That(t.Item5, Is.EqualTo(55));
			}
			{
				STuple<int, int, int, int, int, int> t = (11, 22, 33, 44, 55, 66);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
				Assert.That(t.Item5, Is.EqualTo(55));
				Assert.That(t.Item6, Is.EqualTo(66));
			}
			{
				STuple<int, int, int, int, int, int, int> t = (11, 22, 33, 44, 55, 66, 77);
				Log(t);
				Assert.That(t.Item1, Is.EqualTo(11));
				Assert.That(t.Item2, Is.EqualTo(22));
				Assert.That(t.Item3, Is.EqualTo(33));
				Assert.That(t.Item4, Is.EqualTo(44));
				Assert.That(t.Item5, Is.EqualTo(55));
				Assert.That(t.Item6, Is.EqualTo(66));
				Assert.That(t.Item7, Is.EqualTo(77));
			}
		}

		private static (int, int) ProduceValueTuple(int item1, int item2) => (item1, item2);

		private static int[] ConsumeValueTuple(STuple<int, int> t) => [ t.Item1, t.Item2 ];

		private static STuple<int, int> ProduceSTuple(int item1, int item2) => STuple.Create(item1, item2);

		private static int[] ConsumeSTuple(STuple<int, int> t) => [ t.Item1, t.Item2 ];

		[Test]
		public void Test_Can_AutoCast_Transparently()
		{

			{ // (int, int) => STuple<int, int>
				var res = ConsumeSTuple(ProduceValueTuple(1234, 5));
				Assert.That(res[0], Is.EqualTo(1234));
				Assert.That(res[1], Is.EqualTo(5));
			}
			{ // literal => STuple<int, int>
				var res = ConsumeSTuple((1234, 5));
				Assert.That(res[0], Is.EqualTo(1234));
				Assert.That(res[1], Is.EqualTo(5));
			}
			{ // STuple<int, int> => (int, int)
				var res = ConsumeValueTuple(ProduceSTuple(1234, 5));
				Assert.That(res[0], Is.EqualTo(1234));
				Assert.That(res[1], Is.EqualTo(5));
			}
		}

		[Test]
		public void Test_Deconstruct_STuple()
		{
			{
				STuple.Create(11, 22).Deconstruct(out int a, out int b);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
			}
			{
				STuple.Create(11, 22, 33).Deconstruct(out int a, out int b, out int c);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
			}
			{
				STuple.Create(11, 22, 33, 44).Deconstruct(out int a, out int b, out int c, out int d);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
				Assert.That(d, Is.EqualTo(44));
			}
			{
				STuple.Create(11, 22, 33, 44, 55).Deconstruct(out int a, out int b, out int c, out int d, out int e);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
				Assert.That(d, Is.EqualTo(44));
				Assert.That(e, Is.EqualTo(55));
			}
			{
				STuple.Create(11, 22, 33, 44, 55, 66).Deconstruct(out int a, out int b, out int c, out int d, out int e, out int f);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
				Assert.That(d, Is.EqualTo(44));
				Assert.That(e, Is.EqualTo(55));
				Assert.That(f, Is.EqualTo(66));
			}
		}

		[Test]
		public void Test_Deconstruct_STuple_TupleSyntax()
		{
			{
				var (a, b) = STuple.Create(11, 22);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
			}
			{
				var (a, b, c) = STuple.Create(11, 22, 33);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
			}
			{
				var (a, b, c, d) = STuple.Create(11, 22, 33, 44);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
				Assert.That(d, Is.EqualTo(44));
			}
			{
				var (a, b, c, d, e) = STuple.Create(11, 22, 33, 44, 55);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
				Assert.That(d, Is.EqualTo(44));
				Assert.That(e, Is.EqualTo(55));
			}
			{
				var (a, b, c, d, e, f) = STuple.Create(11, 22, 33, 44, 55, 66);
				Assert.That(a, Is.EqualTo(11));
				Assert.That(b, Is.EqualTo(22));
				Assert.That(c, Is.EqualTo(33));
				Assert.That(d, Is.EqualTo(44));
				Assert.That(e, Is.EqualTo(55));
				Assert.That(f, Is.EqualTo(66));
			}
		}

		#endregion

	}

}
