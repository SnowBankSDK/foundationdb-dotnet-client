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


namespace SnowBank.Testing
{
	using System.Diagnostics.CodeAnalysis;

	public class UlpsToleranceComparer : IEqualityComparer<double>, IEqualityComparer<double?>, IEqualityComparer<float>, IEqualityComparer<float?>
	{

		public static readonly UlpsToleranceComparer OneUlps = new(1);

		public int Ulps { get; }

		public UlpsToleranceComparer(int ulps)
		{
			this.Ulps = ulps;
		}

		/// <inheritdoc />
		public bool Equals(double x, double y) => AreAlmostEqualUlps(x, y, this.Ulps);

		/// <inheritdoc />
		public bool Equals(double? x, double? y) => x is null ? y is null : y is not null && AreAlmostEqualUlps(x.Value, y.Value, this.Ulps);

		/// <inheritdoc />
		public bool Equals(float x, float y) => AreAlmostEqualUlps(x, y, this.Ulps);

		/// <inheritdoc />
		public bool Equals(float? x, float? y) => x is null ? y is null : y is not null && AreAlmostEqualUlps(x.Value, y.Value, this.Ulps);

		int IEqualityComparer<double>.GetHashCode(double obj) => throw new NotSupportedException("This type is only expected to test for equality");

		int IEqualityComparer<double?>.GetHashCode([DisallowNull] double? obj) => throw new NotSupportedException("This type is only expected to test for equality");

		int IEqualityComparer<float>.GetHashCode(float obj) => throw new NotSupportedException("This type is only expected to test for equality");

		int IEqualityComparer<float?>.GetHashCode([DisallowNull] float? obj) => throw new NotSupportedException("This type is only expected to test for equality");

		public static bool AreAlmostEqualUlps(double left, double right, long maxUlps)
		{
#if NET6_0_OR_GREATER
			ulong leftBits = BitConverter.DoubleToUInt64Bits(left);
			ulong rightBits = BitConverter.DoubleToUInt64Bits(right);
#else
			// BitConverter.DoubleToUInt64Bits is not available: same bits via the signed variant
			ulong leftBits = unchecked((ulong) BitConverter.DoubleToInt64Bits(left));
			ulong rightBits = unchecked((ulong) BitConverter.DoubleToInt64Bits(right));
#endif

			ulong leftSignMask = (leftBits >> 63);
			ulong rightSignMask = (rightBits >> 63);

			ulong leftTemp = ((0x8000000000000000 - leftBits) & leftSignMask);
			leftBits = leftTemp | (leftBits & ~leftSignMask);

			ulong rightTemp = ((0x8000000000000000 - rightBits) & rightSignMask);
			rightBits = rightTemp | (rightBits & ~rightSignMask);

			if (leftSignMask != rightSignMask) // Overflow possible, check each against zero
			{
				// This check is specifically used to trap the case of 0 == -0
				// In IEEE floating point maths, -0 is converted to Double.MinValue, which cannot be used with
				// Math.Abs(...) below due to overflow issues. This should only match the 0 == -0 condition.

				// ReSharper disable once CompareOfFloatsByEqualityOperator
				if (left == right)
				{
					return true;
				}
				if (Math.Abs(unchecked((long) leftBits)) > maxUlps || Math.Abs(unchecked((long) rightBits)) > maxUlps)
				{
					return false;
				}
			}

			// Either they have the same sign or both are very close to zero
			return Math.Abs(unchecked((long) leftBits - (long) rightBits)) <= maxUlps;
		}

		public static bool AreAlmostEqualUlps(float left, float right, int maxUlps)
		{
#if NET6_0_OR_GREATER
			uint leftBits = BitConverter.SingleToUInt32Bits(left);
			uint rightBits = BitConverter.SingleToUInt32Bits(right);
#else
			// BitConverter.SingleToUInt32Bits is not available: reinterpret the bits via Unsafe
			uint leftBits = System.Runtime.CompilerServices.Unsafe.As<float, uint>(ref left);
			uint rightBits = System.Runtime.CompilerServices.Unsafe.As<float, uint>(ref right);
#endif

			uint leftSignMask = (leftBits >> 31);
			uint rightSignMask = (rightBits >> 31);

			uint leftTemp = ((0x80000000 - leftBits) & leftSignMask);
			leftBits = leftTemp | (leftBits & ~leftSignMask);

			uint rightTemp = ((0x80000000 - rightBits) & rightSignMask);
			rightBits = rightTemp | (rightBits & ~rightSignMask);

			if (leftSignMask != rightSignMask) // Overflow possible, check each against zero
			{
				// This check is specifically used to trap the case of 0 == -0
				// In IEEE floating point maths, -0 is converted to Float.MinValue, which cannot be used with
				// Math.Abs(...) below due to overflow issues. This should only match the 0 == -0 condition.

				// ReSharper disable once CompareOfFloatsByEqualityOperator
				if (left == right)
				{
					return true;
				}
				if (Math.Abs(unchecked((int) leftBits)) > maxUlps || Math.Abs(unchecked((int) rightBits)) > maxUlps)
					return false;
			}

			// Either they have the same sign or both are very close to zero
			return Math.Abs(unchecked((int) leftBits) - unchecked((int) rightBits)) <= maxUlps;
		}

	}

}
