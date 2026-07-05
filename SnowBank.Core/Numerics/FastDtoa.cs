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

// This file is not part of the netstandard2.0 build: shortest-roundtrip formatter: string.Create(SpanAction) + bit-twiddling throughout.
#if !NETSTANDARD2_0

namespace SnowBank.Numerics
{
	using System.Runtime.InteropServices;

	/// <summary>
	/// This "Do It Yourself Floating Point" class implements a floating-point number
	/// with a uint64 significand and an int exponent. Normalized DiyFp numbers will
	/// have the most significant bit of the significand set.
	/// Multiplication and Subtraction do not normalize their results.
	/// DiyFp are not designed to contain special doubles (NaN and Infinity).
	/// </summary>
	[DebuggerDisplay("F={F}, E={E}")]
	public struct DiyFp
	{
		public const int SignificandSize = 64;

		public static readonly DiyFp Zero = default;

		/// <summary>Significand (64-bit)</summary>
		public ulong F;

		/// <summary>Exponent</summary>
		public int E;

		public DiyFp(ulong significant, int exponent)
		{
			this.F = significant;
			this.E = exponent;
		}

		public void Subtract(DiyFp other)
		{
			Paranoid.Requires(this.E == other.E && this.F >= other.F, "The exponents of both numbers must be the same and the significand of this must be bigger than the significand of other.");
			this.F -= other.F;
		}

		public void Multiply(DiyFp other)
		{
			const ulong M32 = 0xFFFFFFFFul;

			ulong f;

			f = this.F;
			ulong a = f >> 32;
			ulong b = f & M32;
			f = other.F;
			ulong c = f >> 32;
			ulong d = f & M32;

			ulong ac = a * c;
			ulong bc = b * c;
			ulong ad = a * d;
			ulong bd = b * d;
			ulong tmp = (bd >> 32) + (ad & M32) + (bc & M32);
			// By adding 1U << 31 to tmp we round the final result.
			// Halfway cases will be round up.
			tmp += 1ul << 31;

			this.F = ac + (ad >> 32) + (bc >> 32) + (tmp >> 32);
			this.E += other.E + 64;
		}

		public void Normalize()
		{
			Normalize(this.F, this.E, out this.F, out this.E);
		}

		private static void Normalize(ulong significand, int exponent, out ulong outSignificand, out int outExponent)
		{
			Paranoid.Requires(significand != 0);

			// This method is mainly called for normalizing boundaries. In general
			// boundaries need to be shifted by 10 bits. We thus optimize for this
			// case.
			const ulong TEN_MS_BITS = 0xFFC0000000000000ul;
			const ulong UINT64_MSB = 0x8000000000000000ul;
			while ((significand & TEN_MS_BITS) == 0)
			{
				significand <<= 10;
				exponent -= 10;
			}
			while ((significand & UINT64_MSB) == 0)
			{
				significand <<= 1;
				exponent--;
			}
			outSignificand = significand;
			outExponent = exponent;
		}

		public static DiyFp Normalized(ulong significand, int exponent)
		{
			Normalize(significand, exponent, out significand, out exponent);
			return new DiyFp(significand, exponent);
		}

		public static DiyFp Normalized(ref DiyFp a)
		{
			var result = a;
			result.Normalize();
			return result;
		}

		public static DiyFp Minus(ref DiyFp a, ref DiyFp b)
		{
			var result = a;
			result.Subtract(b);
			return result;
		}

		/// <summary>Same as <see cref="Minus(ref DiyFp, ref DiyFp)"/> but only returns the significand</summary>
		public static ulong MinusSignificand(ref DiyFp a, ref DiyFp b)
		{
			Paranoid.Requires(a.E == b.E && a.F >= b.F, "The exponents of both numbers must be the same and the significand of a must be bigger than the significand of b.");
			return a.F - b.F;
		}

		public static DiyFp Times(ref DiyFp a, ref DiyFp b)
		{
			var result = a;
			result.Multiply(b);
			return result;
		}
	}

	[DebuggerDisplay("M={Significand}, E={Exponent}, S={Sign}")]
	[StructLayout(LayoutKind.Explicit)]
	public readonly struct DiyDouble : IEquatable<DiyDouble>, IFormattable
	{
		public const ulong SignMask = 0x8000000000000000ul;
		public const ulong ExponentMask = 0x7FF0000000000000ul;
		public const ulong SignificandMask = 0x000FFFFFFFFFFFFFul;
		public const ulong HiddenBit = 0x0010000000000000ul;
		public const int PhysicalSignificandSize = 52; // Excludes the hidden bit.
		public const int SignificandSize = 53;

		public const int ExponentOffset = 0x3FF; // = 1023
		public const int ExponentBias = ExponentOffset + PhysicalSignificandSize; // = 1075
		public const int DenormalExponent = -ExponentBias + 1; // = -1074
		public const int MaxExponent = 0x7FF - ExponentBias; // = 972
		public const ulong Infinity = 0x7FF0000000000000ul;
		public const ulong NaN = 0x7FF8000000000000ul;

		//note: this only works on LE hosts!

		[FieldOffset(0)]
		public readonly ulong Raw;
		[FieldOffset(0)]
		public readonly double Value;

		public DiyDouble(double d)
			: this()
		{
			this.Value = d;
		}

		public DiyDouble(ulong r)
			: this()
		{
			this.Raw = r;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator DiyDouble(double v) => new(v);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator DiyDouble(ulong r) => new(r);

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is DiySingle other && other.Raw == this.Raw;

		/// <inheritdoc />
		public override int GetHashCode() => this.Raw.GetHashCode();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(DiyDouble other) => other.Raw == this.Raw;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ToRaw(double f) => new DiyDouble(f).Raw;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double FromRaw(ulong raw) => new DiyDouble(raw).Value;

		/// <summary>Returns the next greater double.</summary>
		/// <remarks>Returns +infinity on input +infinity.</remarks>
		[Pure]
		public double NextDouble()
		{
			if (this.Raw == Infinity) return double.PositiveInfinity;
			if (this.Sign >= 0) return new DiyDouble(this.Raw + 1).Value;
			if (this.Significand == 0) return 0.0;
			return new DiyDouble(this.Raw - 1).Value;
		}

		/// <summary>Returns the next greater double.</summary>
		/// <remarks>Returns +infinity on input +infinity.</remarks>
		[Pure]
		public static double NextDouble(double value)
		{
			return new DiyDouble(value).NextDouble();
		}

		/// <summary>Returns the previous smaller double.</summary>
		/// <remarks>Returns -infinity on input -infinity.</remarks>
		[Pure]
		public double PreviousDouble()
		{
			if (this.Raw == (Infinity | SignMask)) return double.NegativeInfinity;
			if (this.Sign < 0) return new DiyDouble(this.Raw + 1).Value;
			if (this.Significand == 0) return -0.0;
			return new DiyDouble(this.Raw - 1).Value;
		}

		/// <summary>Returns the previous smaller double.</summary>
		/// <remarks>Returns -infinity on input -infinity.</remarks>
		[Pure]
		public static double PreviousDouble(double value) => new DiyDouble(value).PreviousDouble();

		[Pure]
		public DiyFp ToNormalized()
		{
			Paranoid.Requires(this.Value > 0d, "The value encoded by this Double must be strictly greater than 0.");

			ulong f = this.Significand;
			int e = this.Exponent;

			// The current double could be a denormal.
			while ((f & HiddenBit) == 0)
			{
				f <<= 1;
				e--;
			}

			// Do the final shifts in one go.
			f <<= DiyFp.SignificandSize - SignificandSize;
			e -= DiyFp.SignificandSize - SignificandSize;
			return new DiyFp(f, e);
		}

		public int Exponent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.IsDenormal
				? DenormalExponent
				: (int)((this.Raw & ExponentMask) >> PhysicalSignificandSize) - ExponentBias;
		}

		public int Sign
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & SignMask) == 0 ? 1 : -1;
		}

		public ulong Significand
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & SignificandMask) | ((r & ExponentMask) == 0 ? 0 : HiddenBit);
			}
		}

		public bool IsDenormal
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & ExponentMask) == 0;
		}

		public bool IsSpecial
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & ExponentMask) == ExponentMask;
			// We consider denormals not to be special.
			// Hence only Infinity and NaN are special.
		}

		public bool IsNaN
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) != 0;
			}
		}

		public bool IsInfinite
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) == 0;
			}
		}

		public bool IsInteger
		{
			[Pure]
			get
			{
				// see http://stackoverflow.com/a/1944214

				ulong r = this.Raw;
				if (r == 0) return true; // 0 is a denormal, but is an integer

				int e = (int)((r & ExponentMask) >> PhysicalSignificandSize) - ExponentOffset;
				// - E == 1024: special number (nan, posinf, neginf, ...)
				// - E >= 52 : too large to hold a decimal value
				// - E < 0 : too small to hold a magnitude greater than 1.0
				return e >= 0 && e < (ExponentOffset + 1) && (e >= PhysicalSignificandSize || ((r << e) & SignificandMask) == 0);
			}
		}

		public bool IsNegative
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & SignMask) != 0;
		}

		[Pure]
		public DiyFp UpperBoundary()
		{
			return new DiyFp(this.Significand * 2 + 1, this.Exponent - 1);
		}

		[Pure]
		public void NormalizedBoundaries(out DiyFp minus, out DiyFp plus)
		{
			Paranoid.Requires(this.Value > 0.0, "The value encoded by this Double must be greater than 0.");

			DiyFp v = ToDiyFp();

			DiyFp mPlus = DiyFp.Normalized((v.F << 1) + 1, v.E - 1);
			DiyFp mMinus;
			if (LowerBoundaryIsCloser())
			{
				mMinus = new DiyFp((v.F << 2) - 1, v.E - 2);
			}
			else
			{
				mMinus = new DiyFp((v.F << 1) - 1, v.E - 1);
			}
			mMinus.F <<= mMinus.E - mPlus.E;
			mMinus.E = mPlus.E;

			minus = mMinus;
			plus = mPlus;
		}

		[Pure]
		private bool LowerBoundaryIsCloser()
		{
			return (this.Raw & SignificandMask) == 0 && this.Exponent != DenormalExponent;
		}

		public DiyFp ToDiyFp()
		{
			Paranoid.Requires(this.Sign > 0, "The value encoded by this Double must be greater or equal to +0.0.");
			Paranoid.Requires(!this.IsSpecial, "It must not be special (infinity, or NaN).");

			return new DiyFp(this.Significand, this.Exponent);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ToUInt64()
		{
			return this.Raw;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double ToDouble()
		{
			return this.Value;
		}

		public override string ToString() => ToString(null, null);

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			formatProvider ??= formatProvider;

			if (this.IsSpecial)
			{
				return string.Create(formatProvider, $"DiyDouble[0x{this.Raw:X16}] = {(this.IsNaN ? "NaN" : this.IsInfinite ? (this.Sign > 0 ? "+Infinity" : "-Infinity") : "Special!")} [SPECIAL]");
			}

			if (this.IsDenormal)
			{
				return string.Create(formatProvider, $"DiyDouble[0x{this.Raw:X16}] = {(this.Sign > 0 ? "+" : "-")}{Convert.ToString((long) this.Significand, 2)} [DENORMAL] = {this.Value:R} {(this.IsInteger ? "(int)" : "(dec)")}");
			}

			return string.Create(formatProvider, $"DiyDouble[0x{this.Raw:X16}] = {(this.Sign > 0 ? "+" : "-")}{Convert.ToString((long) this.Significand, 2)} x 2^{this.Exponent} = {this.Value:R} {(this.IsInteger ? "(int)" : "(dec)")}");
		}

	}

	[DebuggerDisplay("M={Significand}, E={Exponent}, S={Sign}")]
	[StructLayout(LayoutKind.Explicit)]
	public readonly struct DiySingle : IEquatable<DiySingle>, IFormattable
	{

		public const uint SignMask = 0x80000000u;
		public const uint ExponentMask = 0x7F800000u;
		public const uint SignificandMask = 0x007FFFFFu;
		public const uint HiddenBit = 0x00800000u;
		public const int PhysicalSignificandSize = 23; // Excludes the hidden bit.
		public const int SignificandSize = 24;

		public const int ExponentOffset = 0x7F;
		public const int ExponentBias = ExponentOffset + PhysicalSignificandSize;
		public const int DenormalExponent = -ExponentBias + 1;
		public const int MaxExponent = 0xFF - ExponentBias;
		public const uint Infinity = 0x7F800000u;
		public const uint NaN = 0x7FC00000u;

		//note: this only works on LE hosts!

		[FieldOffset(0)]
		public readonly uint Raw;

		[FieldOffset(0)]
		public readonly float Value;

		public DiySingle(float d)
			: this()
		{
			this.Value = d;
		}

		public DiySingle(uint r)
			: this()
		{
			this.Raw = r;
		}

		public static implicit operator DiySingle(float v) => new(v);

		public static implicit operator DiySingle(uint r) => new(r);

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is DiySingle other && other.Raw == this.Raw;

		/// <inheritdoc />
		public override int GetHashCode() => this.Raw.GetHashCode();

		public bool Equals(DiySingle other) => other.Raw == this.Raw;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ToRaw(float f) => new DiySingle(f).Raw;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FromRaw(uint raw) => new DiySingle(raw).Value;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetRawExponent(uint raw) => (raw & ExponentMask) >> PhysicalSignificandSize;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetRawSignificand(uint raw) => raw & SignificandMask;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetRawSign(uint raw) => (raw & SignMask) != 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Recombine(uint rawSignificand, uint rawExponent, bool sign)
			=> new DiySingle((rawSignificand & SignificandMask) | (rawExponent << PhysicalSignificandSize) | (sign ? SignMask : 0)).Value;

		public int Exponent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.IsDenormal
				? DenormalExponent
				: (int) ((this.Raw & ExponentMask) >> PhysicalSignificandSize) - ExponentBias;
		}

		public int Sign
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & SignMask) == 0 ? 1 : -1;
		}

		public uint Significand
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				uint r = this.Raw;
				return (r & SignificandMask) | ((r & ExponentMask) == 0 ? 0 : HiddenBit);
			}
		}

		public bool IsDenormal
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & ExponentMask) == 0;
		}

		public bool IsSpecial
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & ExponentMask) == ExponentMask;
			// We consider denormals not to be special.
			// Hence only Infinity and NaN are special.
		}

		public bool IsNaN
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) != 0;
			}
		}

		public bool IsInfinite
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) == 0;
			}
		}

		public bool IsInteger
		{
			[Pure]
			get
			{
				// see http://stackoverflow.com/a/1944214

				ulong r = this.Raw;
				if (r == 0) return true; // 0 is a denormal, but is an integer

				int e = (int)((r & ExponentMask) >> PhysicalSignificandSize) - ExponentOffset;
				// - E == 128: special number (nan, posinf, neginf, ...)
				// - E >= 23 : too large to hold a decimal value
				// - E < 0 : too small to hold a magnitude greater than 1.0
				return e >= 0 && e < (ExponentOffset + 1) && (e >= PhysicalSignificandSize || ((r << e) & SignificandMask) == 0);
			}
		}

		public bool IsNegative
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Raw & SignMask) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ToUInt32()
		{
			return this.Raw;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double ToSingle()
		{
			return this.Value;
		}

		public override string ToString() => ToString(null, null);

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			formatProvider ??= formatProvider;

			if (this.IsSpecial)
			{
				return string.Create(formatProvider, $"DiySingle[0x{this.Raw:X8}] = {(this.IsNaN ? "NaN" : this.IsInfinite ? (this.Sign > 0 ? "+Infinity" : "-Infinity") : "Special!")} [SPECIAL]");
			}

			if (this.IsDenormal)
			{
				return string.Create(formatProvider, $"DiySingle[0x{this.Raw:X8}] = {(this.Sign > 0 ? "+" : "-")}{Convert.ToString((int) this.Significand, 2)} [DENORMAL] = {this.Value:R} {(this.IsInteger ? "(int)" : "(dec)")}");
			}

			return string.Create(formatProvider, $"DiySingle[0x{this.Raw:X8}] = {(this.Sign > 0 ? "+" : "-")}{Convert.ToString((int) this.Significand, 2)} x 2^{this.Exponent} = {this.Value:R} {(this.IsInteger ? "(int)" : "(dec)")}");
		}

	}

}

#endif
