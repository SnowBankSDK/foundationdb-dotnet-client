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

namespace SnowBank.Numerics.Test
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class DiyFloatingPointFacts : SimpleTest
	{

		[Test]
		public void Test_DiyDouble_Constants()
		{
			Assert.That(DiyDouble.SignMask,		Is.EqualTo(0x8000000000000000ul), "Sign bit is at bit 63 (MSB)");
			Assert.That(DiyDouble.ExponentMask,    Is.EqualTo(0x7FF0000000000000ul), "Exponent takes 11 bits (52 to 62)");
			Assert.That(DiyDouble.SignificandMask, Is.EqualTo(0x000FFFFFFFFFFFFFul), "Significant takes 52 bits (0 to 51)");
			Assert.That(DiyDouble.HiddenBit,       Is.EqualTo(0x0010000000000000ul), "Hidden bit is at bit 53");
			Assert.That(DiyDouble.SignificandSize, Is.EqualTo(53), "Includes the hidden bit");
			Assert.That(DiyDouble.PhysicalSignificandSize, Is.EqualTo(52), "Excludes the hidden bit");

			Assert.That(DiyDouble.ExponentBias, Is.EqualTo(1075), "Exponent Bias");
			Assert.That(DiyDouble.DenormalExponent, Is.EqualTo(-1074), "Denormal Exponent");
			Assert.That(DiyDouble.MaxExponent, Is.EqualTo(972), "Max Exponent");
			Assert.That(DiyDouble.Infinity, Is.EqualTo(0x7FF0000000000000ul), "Infinity");
			Assert.That(DiyDouble.NaN, Is.EqualTo(0x7FF8000000000000ul), "NaN");
		}

		[Test]
		public void Test_DiySingle_Constants()
		{
			Assert.That(DiySingle.SignMask,        Is.EqualTo(0x80000000u), "Sign bit is at bit 31 (MSB)");
			Assert.That(DiySingle.ExponentMask,    Is.EqualTo(0x7F800000u), "Exponent takes 8 bits (23 to 30)");
			Assert.That(DiySingle.SignificandMask, Is.EqualTo(0x007FFFFFu), "Significant takes 23 bits (0 to 22)");
			Assert.That(DiySingle.HiddenBit,       Is.EqualTo(0x00800000u), "Hidden bit is at bit 23");
			Assert.That(DiySingle.SignificandSize, Is.EqualTo(24), "Includes the hidden bit");
			Assert.That(DiySingle.PhysicalSignificandSize, Is.EqualTo(23), "Excludes the hidden bit");

			Assert.That(DiySingle.ExponentBias, Is.EqualTo(150), "Exponent Bias");
			Assert.That(DiySingle.DenormalExponent, Is.EqualTo(-149), "Denormal Exponent");
			Assert.That(DiySingle.MaxExponent, Is.EqualTo(105), "Max Exponent");
			Assert.That(DiySingle.Infinity, Is.EqualTo(0x7F800000u), "Infinity");
			Assert.That(DiySingle.NaN, Is.EqualTo(0x7FC00000u), "NaN");
		}

		[Test]
		public void Test_DiyDouble_Properties()
		{
			static void Check(double value)
			{
				DiyDouble d = value;
				Log(d);

				Assert.That(d.Value, Is.EqualTo(value), $"Value {d}");
				ulong r;
				unsafe
				{
					r = *((ulong*) (&d));
				}

				Assert.That(d.Raw, Is.EqualTo(r), $"Raw {d}");

				Assert.That(d.IsNaN, Is.EqualTo(double.IsNaN(value)), $"NaN? {d}");
				Assert.That(d.IsInfinite, Is.EqualTo(double.IsInfinity(value)), $"Infinity? {d}");
				Assert.That(d.Sign, Is.EqualTo(value >= 0 ? +1 : -1), $"Sign? {d}");

				Assert.That(d.IsInteger, Is.EqualTo(!double.IsInfinity(value) && !double.IsNaN(value) && (value - Math.Truncate(value)) == 0.0), $"Integer? {d}");
			}

			Check(0.0);
			Check(double.Epsilon);
			Check(1.0);
			Check(-1.0);
			Check(double.NaN);
			Check(double.PositiveInfinity);
			Check(double.NegativeInfinity);
			Check(int.MaxValue);
			Check(long.MaxValue);
			Check(Math.PI);
			Check(Math.E);
			Check(1.0 / 3.0);
			Check(11.0 / 10.0);
			Check(1025.0 / 1024.0);

			var rnd = new Random(7654321);
			for (int i = 0; i < 10; i++)
			{
				Check(rnd.NextDouble());
			}
			for (int i = 0; i < 10; i++)
			{
				Check(rnd.Next());
			}
			for (int i = 0; i < 10; i++)
			{
				Check(0.5d + rnd.Next());
			}
			for (int i = 0; i < 63; i++)
			{
				Check(rnd.NextDouble() * (1UL << i));
			}

		}

		[Test]
		public void Test_DiySingle_Properties()
		{
			static void Check(float value)
			{
				DiySingle d = value;
				Log(d);

				Assert.That(d.Value, Is.EqualTo(value), $"Value {d}");
				uint r;
				unsafe
				{
					r = *((uint*) (&d));
				}

				Assert.That(d.Raw, Is.EqualTo(r), $"Raw {d}");

				Assert.That(d.IsNaN, Is.EqualTo(float.IsNaN(value)), $"NaN? {d}");
				Assert.That(d.IsInfinite, Is.EqualTo(float.IsInfinity(value)), $"Infinity? {d}");
				Assert.That(d.Sign, Is.EqualTo(value >= 0 ? +1 : -1), $"Sign? {d}");

				Assert.That(d.IsInteger, Is.EqualTo(!float.IsInfinity(value) && !float.IsNaN(value) && (value - (int) value) == 0), $"Integer? {d}");
			}

			Check(0.0f);
			Check(float.Epsilon);
			Check(1.0f);
			Check(-1.0f);
			Check(float.NaN);
			Check(float.PositiveInfinity);
			Check(float.NegativeInfinity);
			Check((float)Math.PI);
			Check((float)Math.E);
			Check(1.0f / 3.0f);
			Check(11.0f / 10.0f);
			Check(1025.0f / 1024.0f);

			var rnd = new Random(7654321);
			for (int i = 0; i < 10; i++)
			{
				Check((float)rnd.NextDouble());
			}
			for (int i = 0; i < 10; i++)
			{
				Check(rnd.Next());
			}
			for (int i = 0; i < 10; i++)
			{
				Check(0.5f + rnd.Next());
			}
			for (int i = 0; i < 31; i++)
			{
				Check((float)(rnd.NextDouble() * (1U << i)));
			}

		}

	}

}
