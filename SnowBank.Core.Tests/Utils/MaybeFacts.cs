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

// ReSharper disable CompareOfFloatsByEqualityOperator
#pragma warning disable NUnit2009
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace SnowBank.Core.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class MaybeFacts
	{

		[Test]
		public void Test_Maybe_Holds_To_Value()
		{
			var m = Maybe.Return("Hello World");

			Assert.That(m.HasValue, Is.True);
			Assert.That(m.Value, Is.EqualTo("Hello World"));
			Assert.That(m.Error, Is.Null);
		}

		[Test]
		public void Test_Nothing_Does_Not_Have_Value()
		{
			var m = Maybe<string>.Nothing;

			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.Null);
			Assert.That(() => { _ = m.Value; }, Throws.InvalidOperationException);
		}

		[Test]
		public void Test_Maybe_Propagates_Errors()
		{
			var boom = new Exception("EPIC FAIL");

			var m = Maybe<string>.Failure(boom);

			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.SameAs(boom));

			var aggEx = Assert.Throws<AggregateException>(() => { _ = m.Value; });
			Assert.That(aggEx.InnerException, Is.SameAs(boom));
		}

		[Test]
		public void Test_Maybe_Equality()
		{
			{ // M<T>.Equals(M<T>), M<T> == M<T>, M<T> != M<T>

				var ms = new[]
				{
					Maybe.Nothing<string>(),
					Maybe.Return("Hello"),
					Maybe.Return("World"),
					Maybe.Return(default(string)!),
					Maybe.Error<string>(new InvalidOperationException("KABOOM!")),
				};

				for (int i = 0; i < ms.Length; i++)
				{
					for (int j = 0; j < ms.Length; j++)
					{
						if (i == j)
						{
							Assert.That(ms[i].Equals(ms[j]), Is.True, $"{ms[i]}.Equals({ms[j]})");
							Assert.That(ms[i] == ms[j], Is.True, $"{ms[i]} ==  {ms[j]}");
							Assert.That(ms[i] != ms[j], Is.False, $"{ms[i]} !=  {ms[j]}");
							Assert.That(ms[i].Equals((object) ms[j]), Is.True, $"{ms[i]}.Equals((object) {ms[j]})");
						}
						else
						{
							Assert.That(ms[i].Equals(ms[j]), Is.False, $"{ms[i]}.Equals({1})");
							Assert.That(ms[i] == ms[j], Is.False, $"{ms[i]} ==  {ms[j]}");
							Assert.That(ms[i] != ms[j], Is.True, $"{ms[i]} !=  {ms[j]}");
							Assert.That(ms[i].Equals((object) ms[j]), Is.False, $"{ms[i]}.Equals((object) {ms[j]})");
						}
					}
				}
			}

			{ // M<T>.Equals(T)

				Assert.That(Maybe.Nothing<string>().Equals("Hello"), Is.False, "<nothing> eq 'Hello'");
				Assert.That(Maybe.Return(default(string)).Equals("Hello"), Is.False, "<null> eq 'World'");
				Assert.That(Maybe.Return("Hello").Equals("Hello"), Is.True, "'Hello' eq 'Hello'");
				Assert.That(Maybe.Return("World").Equals("Hello"), Is.False, "'World' eq 'World'");
				Assert.That(Maybe.Return("Hello").Equals(null), Is.False, "'Hello' eq <null>");
				Assert.That(Maybe.Error<string>(new InvalidOperationException("KABOOM!")).Equals("Hello"), Is.False, "<error> eq 'World'");
			}

			// corner cases
			Assert.That(Maybe.Return(double.NaN).Equals(Maybe.Return(double.NaN)), Is.True, "M(NaN) == M(NaN)"); // even though NaN != NaN
		}

		[Test]
		public void Test_Maybe_Comparison()
		{
			{ // M<T>.CompareTo(T)

				// order by sort order
				var ms = new[]
				{
					Maybe.Nothing<string>(),
					Maybe.Return(default(string)!),
					Maybe.Return("Hello"),
					Maybe.Return("World"),
					Maybe.Error<string>(new InvalidOperationException("KABOOM!")),
				};

				for (int i = 0; i < ms.Length; i++)
				{
					for (int j = 0; j < ms.Length; j++)
					{
						int cmp = ms[i].CompareTo(ms[j]);
						if (i == j)
						{
							Assert.That(cmp, Is.Zero, $"{ms[i]} cmp {ms[j]}");
						}
						else if (i < j)
						{
							Assert.That(cmp, Is.Negative, $"{ms[i]} cmp {ms[j]}");
						}
						else
						{
							Assert.That(cmp, Is.GreaterThan(0), $"{ms[i]} cmp {ms[j]}");
						}
					}
				}
			}

			{ // M<T>.CompareTo(T)

				Assert.That(Maybe.Nothing<string>().CompareTo("Hello"), Is.Negative, "<nothing> cmp 'Hello'");
				Assert.That(Maybe.Return(default(string)).CompareTo("Hello"), Is.Negative, "<null> cmp 'World'");
				Assert.That(Maybe.Return("Foo").CompareTo("Hello"), Is.Negative, "'Foo' cmp 'Hello'");
				Assert.That(Maybe.Return("Hello").CompareTo("Hello"), Is.Zero, "'Hello' cmp 'Hello'");
				Assert.That(Maybe.Return("World").CompareTo("Hello"), Is.GreaterThan(0), "'World' cmp 'World'");
				Assert.That(Maybe.Error<string>(new InvalidOperationException("KABOOM!")).CompareTo("Hello"), Is.GreaterThan(0), "<error> cmp 'World'");
			}


			{ // Exceptions are sorted using their hashcodes
				var ex1 = Maybe.Error<string>(new InvalidOperationException("KABOOM!"));
				var ex2 = Maybe.Error<string>(new InvalidOperationException("KAPOW!"));
				Assert.That(ex1.CompareTo(ex1), Is.Zero, "Comparison should return 0 for the same exceptions");
				Assert.That(ex1.CompareTo(ex2), Is.Not.Zero, "Comparison should not return 0 for different exceptions");
				Assert.That(ex1.CompareTo(ex2), Is.EqualTo(ex1.CompareTo(ex2)), "Comparison of exceptions should be stable in time");
				Assert.That(Math.Sign(ex2.CompareTo(ex1)), Is.EqualTo(-Math.Sign(ex1.CompareTo(ex2))), "(ERROR1 cmp ERROR2) should have opposite sign to (ERROR2 cmp ERROR1)");
			}
		}

		[Test]
		public void Test_Maybe_Lift_Computation()
		{
			var f = Maybe<int>.Return((x) => x * 2);

			var m = f(2);
			Assert.That(m.Value, Is.EqualTo(4));

			m = f(Maybe<int>.Nothing);
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.Null);

			m = f(new InvalidOperationException("KABOOM"));
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<InvalidOperationException>());
			Assert.That(m.Error.Message, Is.EqualTo("KABOOM"));
		}

		[Test]
		public void Test_Maybe_FromTask()
		{
			{ // Task<T>

				// TASK: OK
				var m = Maybe.FromTask(Task.FromResult("Hello"));
				Assert.That(m.HasValue, Is.True);
				Assert.That(m.Value, Is.EqualTo("Hello"));

				// TASK: ERROR
				m = Maybe.FromTask(Task.FromException<string>(new InvalidOperationException("KABOOM")));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KABOOM"));

				// TASK: CANCELLED
				var cts = new CancellationTokenSource();
				cts.Cancel();
				m = Maybe.FromTask(Task.FromCanceled<string>(cts.Token));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<OperationCanceledException>());

				// TASK: PENDING
				var pending = new TaskCompletionSource<string>();
				Assert.That(() => Maybe.FromTask(pending.Task), Throws.InvalidOperationException);
			}

			{ // Task<Maybe<T>

				// TASK: OK, MAYBE: OK
				var m = Maybe.FromTask(Task.FromResult(Maybe.Return("Hello")));
				Assert.That(m.HasValue, Is.True);
				Assert.That(m.Value, Is.EqualTo("Hello"));

				// TASK: OK, MAYBE: ERROR
				m = Maybe.FromTask(Task.FromResult(Maybe.Error<string>(new InvalidOperationException("KABOOM"))));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KABOOM"));

				// TASK: OK, MAYBE: NOTHING
				m = Maybe.FromTask(Task.FromResult(Maybe.Nothing<string>()));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Null);

				// TASK: ERROR
				m = Maybe.FromTask(Task.FromException<Maybe<string>>(new InvalidOperationException("KABOOM")));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KABOOM"));

				// TASK: CANCELLED
				var cts = new CancellationTokenSource();
				cts.Cancel();
				m = Maybe.FromTask(Task.FromCanceled<Maybe<string>>(cts.Token));
				Assert.That(m.HasValue, Is.False);
				Assert.That(m.Error, Is.Not.Null.And.InstanceOf<OperationCanceledException>());

				// TASK: PENDING
				var pending = new TaskCompletionSource<Maybe<string>>();
				Assert.That(() => Maybe.FromTask(pending.Task), Throws.InvalidOperationException);
			}
		}

		[Test]
		public void Test_Bind_With_One_Value()
		{

			var safeSqrt = Maybe<double>.Bind((x) =>
			{
				//Console.WriteLine("safeSqrt(" + x + ")");
				if (double.IsNaN(x) || x < 0) return Maybe<double>.Nothing;
				return Math.Sqrt(x);
			});

			var m = safeSqrt(4);
			Assert.That(m.Value, Is.EqualTo(2));

			m = safeSqrt(-1);
			Assert.That(m.HasValue, Is.False);

			m = safeSqrt(double.NaN);
			Assert.That(m.HasValue, Is.False);

			m = safeSqrt(Maybe<double>.Nothing);
			Assert.That(m.HasValue, Is.False);

			m = safeSqrt(Maybe<double>.Failure(new InvalidOperationException("PAF")));
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public void Test_Bind_With_Two_Values()
		{
			var safeDivide = Maybe<int>.Bind((x, y) =>
			{
				//Console.WriteLine("safeDivide(" + x + ", " + y + ")");
				if (y == 0) return Maybe<int>.Nothing;
				return x / y;
			});

			var m = safeDivide(6, 2);
			Assert.That(m.HasValue, Is.True);
			Assert.That(m.Value, Is.EqualTo(3));

			m = safeDivide(6, 0);
			Assert.That(m.HasValue, Is.False);

			m = safeDivide(Maybe<int>.Nothing, 2);
			Assert.That(m.HasValue, Is.False);

			m = safeDivide(3, Maybe<int>.Nothing);
			Assert.That(m.HasValue, Is.False);

			m = safeDivide(3, new InvalidOperationException("BOOM"));
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<InvalidOperationException>());

			m = safeDivide(new InvalidOperationException("POW"), 2);
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_Bind_Combine_Other_Binds()
		{

			var combined = Maybe.Bind<double, double, double>(
				f: x =>
				{
					Console.WriteLine("F(" + x + ")");
					if (double.IsNaN(x) || x < 0) return Maybe<double>.Nothing;
					if (x == 666.0) throw new InvalidOperationException("F");
					if (x == 42.0) return Maybe<double>.Nothing;
					return Maybe.Return(Math.Sqrt(x));
				},
				g: x =>
				{
					Console.WriteLine("G(" + x + ")");
					if (x == 666.0) throw new InvalidOperationException("G");
					if (x == 42.0) return Maybe<double>.Nothing;
					return Maybe.Return(x * 123);
				}
			);

			var m = combined(100);
			Assert.That(m.Value, Is.EqualTo(1230));

			m = combined(Maybe<double>.Nothing);
			Assert.That(m.HasValue, Is.False);

			m = combined(new Exception("KABOOM"));
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<Exception>());
			Assert.That(m.Error.Message, Is.EqualTo("KABOOM"));

			// first function intercepts...

			m = combined(42);
			Assert.That(m.HasValue, Is.False);

			m = combined(666);
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<InvalidOperationException>());
			Assert.That(m.Error.Message, Is.EqualTo("F"));

			// second function intercepts

			m = combined(42 * 42);
			Assert.That(m.HasValue, Is.False);

			m = combined(666 * 666);
			Assert.That(m.HasValue, Is.False);
			Assert.That(m.Error, Is.InstanceOf<Exception>());
			Assert.That(m.Error.Message, Is.EqualTo("G"));

		}

	}

}
