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

namespace SnowBank.Numerics.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class PeirceCriterionFacts : SimpleTest
	{

		[Test]
		public void Test_Basics_Double()
		{

			static void Verify(double[] values, double[] expected, int[] eliminated)
			{
				Log("input:");
				DumpCompact(values);

				var res = PeirceCriterion.FilterOutliers(values, out var outliers);
				Assert.That(res, Is.Not.Null);
				Log($"filtered: {res.Count}");
				DumpCompact(res);
				Log($"outliers: {outliers.Count}");
				DumpCompact(outliers);

				Assert.That(res, Is.EqualTo(expected));
				Assert.That(outliers, Is.EqualTo(eliminated));

				Log();
			}

			// reference, from "Peirce's criterion for the elimination of suspect experimental data" by Stephen M. Ross, Ph.D
			Verify(
				[ 101.2, 90.0, 99.0, 102.0, 103.0, 100.2, 89.0, 98.1, 101.5, 102.0 ],
				[ 101.2, 99.0, 102.0, 103.0, 100.2, 98.1, 101.5, 102.0 ],
				[ 1, 6 ]
			);

			// with no outliers
			Verify([ 1, 2, 3 ], [ 1, 2, 3 ], [ ]);
			Verify([ 1, 2, 3, 4, 5, 6 ], [ 1, 2, 3, 4, 5, 6 ], [ ]);

			// with 1 outlier
			Verify([ 1, 2, 3, 42 ], [ 1, 2, 3 ], [ 3 ]);
			Verify([ 1, 2, 3, 4, 42, 5, 6 ], [ 1, 2, 3, 4, 5, 6 ], [ 4 ]);

			// with 2 outliers
			Verify([ 1, 2, -37, 3, 4, 42, 5, 6 ], [ 1, 2, 3, 4, 5, 6 ], [ 2, 5 ]);

			// with 1 extreme outlier
			Verify([ 1, 2, -137, 3, 4, 42, 5, 6 ], [ 1, 2, 3, 4, 42, 5, 6 ], [ 2 ]);

		}

		[Test]
		public void Test_Convert_To_Double()
		{
			static void Verify<T>(T[] values, Func<T, double> selector, T[] expected, int[] eliminated)
			{
				Log("input:");
				DumpCompact(values);

				var res = PeirceCriterion.FilterOutliers(values, selector, out var outliers);
				Assert.That(res, Is.Not.Null);
				Log($"filtered: {res.Count}");
				DumpCompact(res);
				Log($"outliers: {outliers.Count}");
				DumpCompact(outliers);

				Assert.That(res, Is.EqualTo(expected));
				Assert.That(outliers, Is.EqualTo(eliminated));

				Log();
			}

			double[] source = [ 101.2, 90.0, 99.0, 102.0, 103.0, 100.2, 89.0, 98.1, 101.5, 102.0 ];
			double[] filtered = [ 101.2, 99.0, 102.0, 103.0, 100.2, 98.1, 101.5, 102.0 ];

			// double => double
			Verify(
				source,
				(x) => x,
				filtered,
				[ 1, 6 ]
			);

			// int => double
			Verify(
				source.Select(x => (int) (x * 10)).ToArray(),
				(x) => x,
				filtered.Select(x => (int) (x * 10)).ToArray(),
				[1, 6]
			);

			// TimeSpan => double
			Verify(
				source.Select(TimeSpan.FromSeconds).ToArray(),
				(x) => x.TotalSeconds,
				filtered.Select(TimeSpan.FromSeconds).ToArray(),
				[ 1, 6 ]
			);

			// String => length
			Verify(
				[ "hello", "world", "how", "are", "supercalifragilisticexpialidocious", "you", "today" ],
				(x) => x.Length,
				[ "hello", "world", "how", "are", "you", "today" ],
				[ 4 ]
			);
		}


	}

}
