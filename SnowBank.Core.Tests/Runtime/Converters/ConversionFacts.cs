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

namespace SnowBank.Runtime.Converters.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ConversionFacts : SimpleTest
	{

		[Test]
		public void Test_Values_Of_Same_Types_Are_Always_Similar()
		{
			static void Test(object? x, object? y)
			{
				bool expected = x == null ? y == null : y != null && x.Equals(y);
				Assert.That(ComparisonHelper.AreSimilarStrict(x, y), Is.EqualTo(expected), expected ? $"{x} == {y}" : $"{x} != {y}");
				Assert.That(ComparisonHelper.AreSimilarRelaxed(x, y), Is.EqualTo(expected), expected ? $"{x} == {y}" : $"{x} != {y}");
			}

			Test(null, null);

			Test("hello", "world");
			Test("hello", "hello");
			Test("hello", "Hello");
			Test("hello", null);
			Test(null, "world");

			Test(123, 123);
			Test(123, 456);
			Test(123, null);
			Test(null, 456);

			Test(123L, 123L);
			Test(123L, 456L);
			Test(123L, null);
			Test(null, 456L);
		}

		[Test]
		public void Test_Values_Of_Similar_Types_Are_Similar()
		{
			static void SimilarRelaxed(object? x, object? y)
			{
				if (!ComparisonHelper.AreSimilarRelaxed(x, y))
				{
					Assert.Fail($"({(x == null ? "object" : x.GetType().Name)}) {x} ~= ({(y == null ? "object" : y.GetType().Name)}) {y}");
				}
			}
			static void SimilarStrict(object? x, object? y)
			{
				if (!ComparisonHelper.AreSimilarStrict(x, y))
				{
					Assert.Fail($"({(x == null ? "object" : x.GetType().Name)}) {x} ~== ({(y == null ? "object" : y.GetType().Name)}) {y}");
				}
			}

			static void DifferentRelaxed(object? x, object? y)
			{
				if (ComparisonHelper.AreSimilarRelaxed(x, y))
				{
					Assert.Fail($"({(x == null ? "object" : x.GetType().Name)}) {x} !~= ({(y == null ? "object" : y.GetType().Name)}) {y}");
				}
			}

			static void DifferentStrict(object? x, object? y)
			{
				if (ComparisonHelper.AreSimilarStrict(x, y))
				{
					Assert.Fail($"({(x == null ? "object" : x.GetType().Name)}) {x} !~== ({(y == null ? "object" : y.GetType().Name)}) {y}");
				}
			}

			Assert.Multiple(() =>
			{
				SimilarRelaxed(false, 0);
				SimilarRelaxed(true, 1);
				SimilarRelaxed(0, false);
				SimilarRelaxed(1, true);
				//
				DifferentStrict(false, 0);
				DifferentStrict(true, 1);
				DifferentStrict(0, false);
				DifferentStrict(1, true);

				SimilarRelaxed(123, 123L);
				SimilarRelaxed(123, 123U);
				SimilarRelaxed(123, 123UL);
				SimilarRelaxed(123, 123d);
				SimilarRelaxed(123, 123f);
				//
				SimilarStrict(123, 123L);
				SimilarStrict(123, 123U);
				SimilarStrict(123, 123UL);
				SimilarStrict(123, 123d);
				SimilarStrict(123, 123f);

				DifferentRelaxed("hello", 123);
				DifferentRelaxed(123, "hello");
				//
				DifferentStrict("hello", 123);
				DifferentStrict(123, "hello");

				SimilarRelaxed("A", 'A');
				SimilarRelaxed('A', "A");
				DifferentRelaxed("AA", 'A');
				DifferentRelaxed('A', "AA");
				DifferentRelaxed("A", 'B');
				DifferentRelaxed('A', "B");
				//
				SimilarStrict("A", 'A');
				SimilarStrict('A', "A");
				DifferentStrict("AA", 'A');
				DifferentStrict('A', "AA");
				DifferentStrict("A", 'B');
				DifferentStrict('A', "B");

				SimilarRelaxed("123", 123);
				SimilarRelaxed("123", 123L);
				SimilarRelaxed("123.4", 123.4f);
				SimilarRelaxed("123.4", 123.4d);
				//
				DifferentStrict("123", 123);
				DifferentStrict("123", 123L);
				DifferentStrict("123.4", 123.4f);
				DifferentStrict("123.4", 123.4d);

				SimilarRelaxed(123, "123");
				SimilarRelaxed(123L, "123");
				SimilarRelaxed(123.4f, "123.4");
				SimilarRelaxed(123.4d, "123.4");
				//
				DifferentStrict(123, "123");
				DifferentStrict(123L, "123");
				DifferentStrict(123.4f, "123.4");
				DifferentStrict(123.4d, "123.4");

				var g = Guid.NewGuid();

				SimilarRelaxed(g, g.ToString());
				SimilarRelaxed(g.ToString(), g);
				//
				DifferentStrict(g, g.ToString());
				DifferentStrict(g.ToString(), g);

				DifferentRelaxed(g.ToString(), Guid.Empty);
				DifferentRelaxed(Guid.Empty, g.ToString());
				//
				DifferentStrict(g.ToString(), Guid.Empty);
				DifferentStrict(Guid.Empty, g.ToString());
			});
		}

	}

}
