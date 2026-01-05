#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

#nullable disable

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace SnowBank.Data.Json.Tests
{
#if NET8_0_OR_GREATER
	using System.Numerics;
#endif

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public class JsonNumericsFacts : SimpleTest
	{

		[Test]
		public void Test_JsonNumber_INumber_Additions()
		{
			// Validate the INumber<T> arithmetic for addition

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Zero + JsonNumber.Zero, Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.Zero + JsonNumber.One, Is.EqualTo(JsonNumber.One));
				Assert.That(JsonNumber.One + JsonNumber.Zero, Is.EqualTo(JsonNumber.One));
				Assert.That(JsonNumber.One + JsonNumber.One, Is.EqualTo(JsonNumber.Return(2)));

				Assert.That(JsonNumber.Create(123) + JsonNumber.Create(456), Is.EqualTo(579));
				Assert.That(JsonNumber.Create(-123) + JsonNumber.Create(123), Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.Create(+123) + JsonNumber.Create(-123), Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.Create(0) + JsonNumber.Create(long.MaxValue), Is.EqualTo(JsonNumber.Create(long.MaxValue)));
				Assert.That(JsonNumber.Create(0) + JsonNumber.Create(ulong.MaxValue), Is.EqualTo(JsonNumber.Create(ulong.MaxValue)));
				Assert.That(JsonNumber.Create(1) + JsonNumber.Create(long.MaxValue), Is.EqualTo(JsonNumber.Create(9223372036854775808UL)));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(0), Is.EqualTo(JsonNumber.Create(long.MaxValue)));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(1), Is.EqualTo(JsonNumber.Create(9223372036854775808UL)));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(long.MaxValue), Is.EqualTo(JsonNumber.Create(2UL * long.MaxValue)));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(-long.MaxValue), Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.Create(ulong.MaxValue) + JsonNumber.Create(0), Is.EqualTo(JsonNumber.Create(ulong.MaxValue)));
				Assert.That(JsonNumber.Create(ulong.MaxValue) + JsonNumber.Create(-1), Is.EqualTo(JsonNumber.Create(ulong.MaxValue - 1)));
				Assert.That(JsonNumber.Create(ulong.MaxValue) + JsonNumber.Create(-1), Is.EqualTo(JsonNumber.Create(ulong.MaxValue - 1)));

				Assert.That(JsonNumber.Zero + JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.One + JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.Create(2) + JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.Zero, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.One, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.Create(2), Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Zero + JsonNumber.Zero, IsJson.EqualTo(0));
				Assert.That(JsonNumber.Zero + JsonNumber.One, IsJson.EqualTo(1));
				Assert.That(JsonNumber.One + JsonNumber.Zero, IsJson.EqualTo(1));
				Assert.That(JsonNumber.One + JsonNumber.One, IsJson.EqualTo(2));

				Assert.That(JsonNumber.Create(123) + JsonNumber.Create(456), IsJson.EqualTo(579));
				Assert.That(JsonNumber.Create(-123) + JsonNumber.Create(123), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(+123) + JsonNumber.Create(-123), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(0) + JsonNumber.Create(long.MaxValue), IsJson.EqualTo(long.MaxValue));
				Assert.That(JsonNumber.Create(0) + JsonNumber.Create(ulong.MaxValue), IsJson.EqualTo(ulong.MaxValue));
				Assert.That(JsonNumber.Create(1) + JsonNumber.Create(long.MaxValue), IsJson.EqualTo(9223372036854775808UL));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(0), IsJson.EqualTo(long.MaxValue));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(1), IsJson.EqualTo(9223372036854775808UL));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(long.MaxValue), IsJson.EqualTo(2UL * long.MaxValue));
				Assert.That(JsonNumber.Create(long.MaxValue) + JsonNumber.Create(-long.MaxValue), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(ulong.MaxValue) + JsonNumber.Create(0), IsJson.EqualTo(ulong.MaxValue));
				Assert.That(JsonNumber.Create(ulong.MaxValue) + JsonNumber.Create(-1), IsJson.EqualTo(ulong.MaxValue - 1));
				Assert.That(JsonNumber.Create(ulong.MaxValue) + JsonNumber.Create(-1), IsJson.EqualTo(ulong.MaxValue - 1));

				Assert.That(JsonNumber.Zero + JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.One + JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.Create(2) + JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.Zero, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.One, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.Create(2), IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN + JsonNumber.NaN, IsJson.EqualTo(double.NaN));

			}
		}

		[Test]
		public void Test_JsonNumber_INumber_Subtractions()
		{
			// Validate the INumber<T> arithmetic for subtraction

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Zero - JsonNumber.Zero, IsJson.EqualTo(0));
				Assert.That(JsonNumber.Zero - JsonNumber.One, IsJson.EqualTo(-1));
				Assert.That(JsonNumber.One - JsonNumber.Zero, IsJson.EqualTo(1));
				Assert.That(JsonNumber.One - JsonNumber.One, IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(456) - JsonNumber.Create(123), IsJson.EqualTo(333));
				Assert.That(JsonNumber.Create(123) - JsonNumber.Create(456), IsJson.EqualTo(-333));
				Assert.That(JsonNumber.Create(+123) - JsonNumber.Create(+123), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(+123) - JsonNumber.Create(-123), IsJson.EqualTo(246));
				Assert.That(JsonNumber.Create(-123) - JsonNumber.Create(+123), IsJson.EqualTo(-246));
				Assert.That(JsonNumber.Create(-123) - JsonNumber.Create(-123), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(0) - JsonNumber.Create(long.MaxValue), IsJson.EqualTo(-9223372036854775807));
				Assert.That(JsonNumber.Create(-1) - JsonNumber.Create(long.MaxValue), IsJson.EqualTo(long.MinValue));
				Assert.That(JsonNumber.Create(long.MaxValue) - JsonNumber.Create(0), IsJson.EqualTo(9223372036854775807));
				Assert.That(JsonNumber.Create(long.MaxValue) - JsonNumber.Create(1), IsJson.EqualTo(9223372036854775806));
				Assert.That(JsonNumber.Create(long.MaxValue) - JsonNumber.Create(long.MaxValue), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(ulong.MaxValue) - JsonNumber.Create(0), IsJson.EqualTo(ulong.MaxValue));
				Assert.That(JsonNumber.Create(ulong.MaxValue) - JsonNumber.Create(1), IsJson.EqualTo(ulong.MaxValue - 1));
				Assert.That(JsonNumber.Create(ulong.MaxValue) - JsonNumber.Create(ulong.MaxValue), IsJson.EqualTo(0));
				Assert.That(JsonNumber.Create(ulong.MaxValue) - JsonNumber.Create(ulong.MaxValue - 1), IsJson.EqualTo(1));
				Assert.That(JsonNumber.Create(ulong.MaxValue - 1) - JsonNumber.Create(ulong.MaxValue), IsJson.EqualTo(-1));
				Assert.That(JsonNumber.Zero - JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.One - JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.Create(2) - JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN - JsonNumber.Zero, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN - JsonNumber.One, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN - JsonNumber.Create(2), IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN - JsonNumber.NaN, IsJson.EqualTo(double.NaN));
			}
		}

		[Test]
		public void Test_JsonNumber_INumber_Multiplications()
		{
			// Validate the INumber<T> arithmetic for multiplication

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Zero * JsonNumber.Zero, Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.Zero * JsonNumber.One, Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.One * JsonNumber.Zero, Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.One * JsonNumber.One, Is.EqualTo(JsonNumber.One));

				Assert.That(JsonNumber.Create(123) * JsonNumber.Create(456), Is.EqualTo(JsonNumber.Create(56088)));
				Assert.That(JsonNumber.Create(456) * JsonNumber.Create(123), Is.EqualTo(JsonNumber.Create(56088)));
				Assert.That(JsonNumber.One * JsonNumber.Create(long.MaxValue), Is.EqualTo(JsonNumber.Create(long.MaxValue)));
				Assert.That(JsonNumber.Create(2) * JsonNumber.Create(long.MaxValue), Is.EqualTo(JsonNumber.Create(2UL * long.MaxValue)));

				Assert.That(JsonNumber.Zero * JsonNumber.PI, Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.One * JsonNumber.PI, Is.EqualTo(JsonNumber.PI));
				Assert.That(JsonNumber.Create(2) * JsonNumber.PI, Is.EqualTo(JsonNumber.Tau));
				Assert.That(JsonNumber.PI * JsonNumber.Zero, Is.EqualTo(JsonNumber.Zero));
				Assert.That(JsonNumber.PI * JsonNumber.One, Is.EqualTo(JsonNumber.PI));
				Assert.That(JsonNumber.PI * JsonNumber.Create(2), Is.EqualTo(JsonNumber.Tau));

				Assert.That(JsonNumber.DecimalZero * JsonNumber.PI, Is.EqualTo(0.0));
				Assert.That(JsonNumber.DecimalOne * JsonNumber.PI, Is.EqualTo(JsonNumber.PI));
				Assert.That(JsonNumber.Create(2.0) * JsonNumber.PI, Is.EqualTo(JsonNumber.Tau));
				Assert.That(JsonNumber.PI * JsonNumber.DecimalZero, Is.EqualTo(0.0));
				Assert.That(JsonNumber.PI * JsonNumber.DecimalOne, Is.EqualTo(JsonNumber.PI));
				Assert.That(JsonNumber.PI * JsonNumber.Create(2.0), Is.EqualTo(JsonNumber.Tau));
				Assert.That(JsonNumber.PI * JsonNumber.PI, Is.EqualTo(JsonNumber.Create(Math.PI * Math.PI)));

				Assert.That(JsonNumber.Zero * JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.One * JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.Create(2) * JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.Zero, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.One, Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.Create(2), Is.EqualTo(JsonNumber.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.NaN, Is.EqualTo(JsonNumber.NaN));
			}

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonNumber.Zero * JsonNumber.Zero, IsJson.EqualTo(0));
				Assert.That(JsonNumber.Zero * JsonNumber.One, IsJson.EqualTo(0));
				Assert.That(JsonNumber.One * JsonNumber.Zero, IsJson.EqualTo(0));
				Assert.That(JsonNumber.One * JsonNumber.One, IsJson.EqualTo(1));

				Assert.That(JsonNumber.Create(123) * JsonNumber.Create(456), IsJson.EqualTo(56088));
				Assert.That(JsonNumber.Create(456) * JsonNumber.Create(123), IsJson.EqualTo(56088));
				Assert.That(JsonNumber.One * JsonNumber.Create(long.MaxValue), IsJson.EqualTo(long.MaxValue));
				Assert.That(JsonNumber.Create(2) * JsonNumber.Create(long.MaxValue), IsJson.EqualTo(2UL * long.MaxValue));

				Assert.That(JsonNumber.Zero * JsonNumber.PI, IsJson.EqualTo(0.0));
				Assert.That(JsonNumber.One * JsonNumber.PI, IsJson.EqualTo(Math.PI));
				Assert.That(JsonNumber.Create(2) * JsonNumber.PI, IsJson.EqualTo(2.0 * Math.PI));
				Assert.That(JsonNumber.PI * JsonNumber.Zero, IsJson.EqualTo(0.0));
				Assert.That(JsonNumber.PI * JsonNumber.One, IsJson.EqualTo(Math.PI));
				Assert.That(JsonNumber.PI * JsonNumber.Create(2), IsJson.EqualTo(Math.PI * 2.0));

				Assert.That(JsonNumber.DecimalZero * JsonNumber.PI, IsJson.EqualTo(0.0));
				Assert.That(JsonNumber.DecimalOne * JsonNumber.PI, IsJson.EqualTo(Math.PI));
				Assert.That(JsonNumber.Create(2.0) * JsonNumber.PI, IsJson.EqualTo(2.0 * Math.PI));
				Assert.That(JsonNumber.PI * JsonNumber.DecimalZero, IsJson.EqualTo(0.0));
				Assert.That(JsonNumber.PI * JsonNumber.DecimalOne, IsJson.EqualTo(Math.PI));
				Assert.That(JsonNumber.PI * JsonNumber.Create(2.0), IsJson.EqualTo(Math.PI * 2.0));
				Assert.That(JsonNumber.PI * JsonNumber.PI, IsJson.EqualTo(Math.PI * Math.PI));

				Assert.That(JsonNumber.Zero * JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.One * JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.Create(2) * JsonNumber.NaN, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.Zero, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.One, IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.Create(2), IsJson.EqualTo(double.NaN));
				Assert.That(JsonNumber.NaN * JsonNumber.NaN, IsJson.EqualTo(double.NaN));
			}
			
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_JsonValue_INumber_Addition()
		{
			static TNumber AddNumbers<TNumber>(TNumber a, TNumber b) where TNumber : INumberBase<TNumber> => a + b;

			static JsonValue Add(JsonValue a, JsonValue b) => AddNumbers(a, b);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Add(123, 456), IsJson.EqualTo(579));
				Assert.That(Add(-123, 456), IsJson.EqualTo(333));
				Assert.That(Add(-123, -456), IsJson.EqualTo(-579));
				Assert.That(Add(123, -456), IsJson.EqualTo(-333));

				Assert.That(Add(1.23d, 4.56d), IsJson.EqualTo(5.79d));
				Assert.That(Add(1.23f, 4.56f), IsJson.EqualTo(5.79f));
				Assert.That(Add(Math.PI, -Math.PI), IsJson.EqualTo(0));

				Assert.That(Add(int.MaxValue, int.MaxValue), IsJson.EqualTo(2L * int.MaxValue));
				Assert.That(Add(int.MinValue, int.MinValue), IsJson.EqualTo(2L * int.MinValue));
			}

		}

		[Test]
		public void Test_JsonNumber_Can_By_Used_With_Generic_Arithmetic()
		{
			JsonNumber a = JsonNumber.One;
			JsonNumber b = JsonNumber.PI;
			GenericINumber(a, b);
		}

		[Test]
		public void Test_JsonValue_Can_By_Used_With_Generic_Arithmetic()
		{
			JsonValue a = JsonNumber.One;
			JsonValue b = JsonNumber.PI;
			GenericINumber(a, b);
		}

		private static void GenericINumber<T>(T x, T y) where T : INumberBase<T>
		{
			_ = x + T.Zero;
			_ = x + y;
			_ = x - y;
			_ = x * T.One;
			_ = x * y;
			_ = x / y;
			_ = x++;
			_ = ++x;
			_ = y--;
			_ = --y;
		}

		[Test]
		public void Test_JsonValue_IComparisonOperators()
		{

			static bool Equal<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x == y;

			static bool NotEqual<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x != y;

			static bool GreaterThan<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x > y;

			static bool GreaterOrEqual<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x >= y;

			static bool LessThan<T>(T x, T y) where T: IComparisonOperators<T, T, bool> => x < y;

			static bool LessOrEqual<T>(T x, T y) where T : IComparisonOperators<T, T, bool> => x <= y;

			// use random numbers, so that we don't end up just testing reference equality between cached small numbers
			int[] numbers = [NextInt32(), NextInt32(), NextInt32()];
			Assume.That(numbers, Is.Unique);
			Array.Sort(numbers);

			JsonValue x0 = JsonNumber.Return(numbers[0]);
			JsonValue x1 = JsonNumber.Return(numbers[1]);
			JsonValue x2 = JsonNumber.Return(numbers[2]);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(Equal(x0, x0), Is.True);
				Assert.That(Equal(x0, x1), Is.False);
				Assert.That(Equal(x0, x2), Is.False);

				Assert.That(NotEqual(x0, x0), Is.False);
				Assert.That(NotEqual(x0, x1), Is.True);
				Assert.That(NotEqual(x0, x2), Is.True);

				Assert.That(LessThan(x0, x0), Is.False);
				Assert.That(LessThan(x0, x1), Is.True);
				Assert.That(LessThan(x0, x2), Is.True);

 				Assert.That(LessOrEqual(x0, x0), Is.True);
				Assert.That(LessOrEqual(x0, x1), Is.True);
				Assert.That(LessOrEqual(x0, x2), Is.True);

				Assert.That(GreaterThan(x0, x0), Is.False);
				Assert.That(GreaterThan(x0, x1), Is.False);
				Assert.That(GreaterThan(x0, x2), Is.False);

				Assert.That(GreaterOrEqual(x0, x0), Is.True);
				Assert.That(GreaterOrEqual(x0, x1), Is.False);
				Assert.That(GreaterOrEqual(x0, x2), Is.False);
			}
		}

#endif

	}

}
