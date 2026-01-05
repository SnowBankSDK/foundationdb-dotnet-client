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

namespace SnowBank.Buffers.Binary
{
	using System.Globalization;

	/// <summary>Units for size of buffers or arrays of bytes (<see cref="SizeUnit.KB"/> = 1_000 bytes, <see cref="SizeUnit.KiB"/> = 1_024 bytes, ...)</summary>
	[PublicAPI]
	public enum SizeUnit
	{
		// first range is base 10

		/// <summary>Bytes</summary>
		Byte = 0,

		/// <summary>Kilobyte, <c>kB</c>, 1E3 bytes: 1 kB = 1_000 bytes</summary>
		KB = 1,
		/// <summary>Megabyte, <c>MB</c>, 1E6 bytes: 1 MB = 10^6 = 1_000_000 bytes</summary>
		MB = 2,
		/// <summary>Gigabyte, <c>GB</c>, 1E9 bytes: 1 GB = 10^9 = 1_000_000_000 bytes</summary>
		GB = 3,
		/// <summary>Terabyte, <c>TB</c>, 1E12 bytes: 1 TB = 10^12 = 1_000_000_000_000 bytes</summary>
		TB = 4,
		/// <summary>Petabyte, <c>PB</c>, 1E15 bytes: 1 PB = 10^15 = 1_000_000_000_000_000 bytes</summary>
		PB = 5,
		/// <summary>Exabyte, <c>EB</c>, 1E18 bytes: 1 EB = 10^18 = 1_000_000_000_000_000_000 bytes</summary>
		EB = 6,
		//...

		// second range is base 2

		/// <summary>Kibibyte, <c>KiB</c>, 2^10 bytes: 1 KiB = 1_024 bytes</summary>
		KiB = 129,
		/// <summary>Mebibyte, <c>MiB</c>, 2^20 bytes: 1 MiB = 1_048_576 bytes</summary>
		MiB = 130,
		/// <summary>Gibibyte, <c>GiB</c>, 2^30 bytes: 1 GiB = 1_073_741_824 bytes</summary>
		GiB = 131,
		/// <summary>Tebibyte, <c>TiB</c>, 2^40 bytes: 1 TiB = 1_099_511_627_776 bytes</summary>
		TiB = 132,
		/// <summary>Pebibyte, <c>PiB</c>, 2^50 bytes: 1 PiB =  1_125_899_906_842_624 bytes</summary>
		PiB = 133,
		/// <summary>Exibyte, <c>EiB</c>, 2^60 bytes: 1 EiB =  1_152_921_504_606_846_976 bytes</summary>
		EiB = 134,
		//...

	}

	/// <summary>Represents the size (in bytes) of a buffer or range of data, with its associated "natural" unit (KB, KiB, GiB, ...)</summary>
	/// <example>A size of 8 GiB can be expressed as (8589934592, GiB) so that the value can be nicely formated as "8 GiB".</example>
	[PublicAPI]
	public readonly struct ByteSize : IEquatable<ByteSize>, IEquatable<long>, IEquatable<int>, IEquatable<ulong>, IEquatable<uint>, IFormattable
	{

		/// <summary>Zero size (0 bytes)</summary>
		public static readonly ByteSize Zero = default;

		private ByteSize(ulong value, SizeUnit unit)
		{
			this.Value = value;
			this.Unit = unit;
		}

		/// <summary>The size (in bytes)</summary>
		public readonly ulong Value;

		/// <summary>The natural unit that was used to construct this size (GiB, KB, ...)</summary>
		public readonly SizeUnit Unit;

		/// <summary>Test if the size is an exact multiple of its base unit</summary>
		/// <returns>true if <c>value mod sizeof(unit) == 0</c></returns>
		/// <example>
		///	<para><c>new ByteSize(16384, SizeUnit.KiB).IsMultipleOfUnit() == true</c> (because 16384 mod 1024 = 0)</para>
		///	<para><c>new ByteSize(16384, SizeUnit.KB).IsMultipleOfUnit() == false</c> (because 16384 mod 1000 = 384 != 0)</para>
		/// </example>
		public bool IsMultipleOfUnit()
		{
			var s = GetUnitSize(this.Unit);
			return s == 1 || (this.Value % s) == 0;
		}

		/// <summary>Returns the size in its unit</summary>
		/// <returns>Corresponding value, expressed in the size unit. For example, if the size unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Create(1536, SizeUnit.KB).ScaleToUnitDouble() == 1.5d</code></example>
		public double ScaleToUnitDouble() => this.Value * GetUnitRatio(this.Unit);

		/// <summary>Returns the size, mapped to a specific unit</summary>
		/// <param name="unit">Target unit</param>
		/// <returns>Corresponding value, expressed in the new unit. For example, if the unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitDouble(SizeUnit.KB) == 1.5d</code></example>
		public double ScaleToUnitDouble(SizeUnit unit) => this.Value * GetUnitRatio(unit);

		/// <summary>Returns the size in its unit, rounded to an integer</summary>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the size unit. For example, if the size unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitInt32(SizeUnit.KB) == 2</code></example>
		public int ScaleToUnitInt32(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((int) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		/// <summary>Returns the size, mapped to a specific unit, rounded to an integer</summary>
		/// <param name="unit">Target unit</param>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the new unit. For example, if the unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitInt32(SizeUnit.KB) == 2</code></example>
		public int ScaleToUnitInt32(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((int) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		/// <summary>Returns the size in its unit, rounded to an integer</summary>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the size unit. For example, if the size unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitUInt32(SizeUnit.KB) == 2</code></example>
		public uint ScaleToUnitUInt32(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((uint) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		/// <summary>Returns the size, mapped to a specific unit, rounded to an integer</summary>
		/// <param name="unit">Target unit</param>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the new unit. For example, if the unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitUInt32(SizeUnit.KB) == 2</code></example>
		public uint ScaleToUnitUInt32(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((uint) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		/// <summary>Returns the size in its unit, rounded to an integer</summary>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the size unit. For example, if the size unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitInt64(SizeUnit.KB) == 2</code></example>
		public long ScaleToUnitInt64(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((long) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		/// <summary>Returns the size, mapped to a specific unit, rounded to an integer</summary>
		/// <param name="unit">Target unit</param>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the new unit. For example, if the unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitInt64(SizeUnit.KB) == 2</code></example>
		public long ScaleToUnitInt64(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((long) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		/// <summary>Returns the size in its unit, rounded to an integer</summary>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the size unit. For example, if the size unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitUInt64(SizeUnit.KB) == 2</code></example>
		public ulong ScaleToUnitUInt64(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((ulong) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		/// <summary>Returns the size, mapped to a specific unit, rounded to an integer</summary>
		/// <param name="unit">Target unit</param>
		/// <param name="rounding">Rounding mode (defaults to <see cref="MidpointRounding.AwayFromZero"/>)</param>
		/// <returns>Corresponding value, expressed in the new unit. For example, if the unit is <see cref="SizeUnit.KB"/> then the result will be the size (in bytes) divided by 1024.</returns>
		/// <example><code>ByteSize.Bytes(1536).ScaleToUnitUInt64(SizeUnit.KB) == 2</code></example>
		public ulong ScaleToUnitUInt64(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((ulong) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		/// <summary>Test if the size is an exact multiple of the specified unit</summary>
		/// <returns>true if <c>value mod sizeof(unit) == 0</c></returns>
		/// <example>
		///	<para><c>new ByteSize(16777216, SizeUnit.MiB).IsMultipleOfUnit(SizeOfUnit.MiB) == true</c> (because 16777216 mod 1048576 = 0)</para>
		///	<para><c>new ByteSize(16777216, SizeUnit.MiB).IsMultipleOfUnit(SizeOfUnit.KiB) == true</c> (because 16777216 mod 1024 = 0)</para>
		///	<para><c>new ByteSize(16777216, SizeUnit.MiB).IsMultipleOfUnit(SizeOfUnit.MB) == false</c> (because 16777216 mod 1000000 = 777216 != 0)</para>
		///	<para><c>new ByteSize(16000000, SizeUnit.KB).IsMultipleOfUnit(SizeOfUnit.KB) == true</c> (because 16000000 mod 1000 = 0)</para>
		/// </example>
		public bool IsMultipleOfUnit(SizeUnit unit)
		{
			var s = GetUnitSize(unit);
			return s == 1 || (this.Value % s) == 0;
		}

		#region Base 10

		/// <summary>Kilobyte (10^3 = 1,000 bytes)</summary>
		public const long KB = 1_000L;

		/// <summary>Megabyte (10^6 = 1,000,000 bytes)</summary>
		public const long MB = 1_000_000L;

		/// <summary>Gigabyte (10^9 = 1,000,000,000 bytes)</summary>
		public const long GB = 1_000_000_000L;

		/// <summary>Terabyte (10^12 = 1,000,000,000,000 bytes)</summary>
		public const long TB = 1_000_000_000_000L;

		/// <summary>Petabyte (10^15 = 1,000,000,000,000,000 bytes)</summary>
		public const long PB = 1_000_000_000_000_000L;

		/// <summary>Exabyte (10^19 = 1,000,000,000,000,000,000 bytes)</summary>
		public const long EB = 1_000_000_000_000_000_000L;

		//note: 1 ZettaByte is larger than 2^64 so could not be represented by this type anyway!

		#endregion

		#region Base 2

		/// <summary>Kibibyte (2^10 = 1,024 bytes)</summary>
		public const long KiB = 1L << 10;

		/// <summary>Mebibyte (2^20 = 1,048,576 bytes)</summary>
		public const long MiB = 1L << 20;

		/// <summary>Gibibyte (2^30 = 1,073,741,824 bytes)</summary>
		public const long GiB = 1L << 30;

		/// <summary>Tebibyte (2^40 = 1,099,511,627,776 bytes)</summary>
		public const long TiB = 1L << 40;

		/// <summary>Pebibyte (2^50 = 1,125,899,906,842,624 bytes)</summary>
		public const long PiB = 1L << 50;

		/// <summary>Exibyte (2^60 = 1,152,921,504,606,846,976 bytes)</summary>
		public const long EiB = 1L << 60;

		//note: 1 ZebiByte is larger than 2^64 so could not be represented by this type anyway!

		#endregion

		/// <inheritdoc />
		public override string ToString() => this.ToString(null, null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider)
		{
			if (this.Unit == SizeUnit.Byte)
			{
				//TODO: try to "auto-detect" the natural unit if it is an exact multiple?)
				return this.Value.ToString(provider ?? CultureInfo.InvariantCulture);
			}
			else
			{
				double x = this.Value * GetUnitRatio(this.Unit);
				var literal = GetUnitLiteral(this.Unit);
				return string.Create(provider ?? CultureInfo.InvariantCulture, $"{x:R} {literal}");
			}
		}

		public static ulong GetUnitSize(SizeUnit unit) => unit switch
		{
			SizeUnit.Byte => 1,
			SizeUnit.KB => KB,
			SizeUnit.MB => MB,
			SizeUnit.GB => GB,
			SizeUnit.TB => TB,
			SizeUnit.PB => PB,
			SizeUnit.EB => EB,
			SizeUnit.KiB => KiB,
			SizeUnit.MiB => MiB,
			SizeUnit.GiB => GiB,
			SizeUnit.TiB => TiB,
			SizeUnit.PiB => PiB,
			SizeUnit.EiB => EiB,
			_ => throw new ArgumentOutOfRangeException(nameof(unit), "Invalid size unit")
		};

		public static double GetUnitRatio(SizeUnit unit) => unit switch
		{
			SizeUnit.Byte => 1.0,
			SizeUnit.KB => 1.0 / KB,
			SizeUnit.MB => 1.0 / MB,
			SizeUnit.GB => 1.0 / GB,
			SizeUnit.TB => 1.0 / TB,
			SizeUnit.PB => 1.0 / PB,
			SizeUnit.EB => 1.0 / EB,
			SizeUnit.KiB => 1.0 / KiB,
			SizeUnit.MiB => 1.0 / MiB,
			SizeUnit.GiB => 1.0 / GiB,
			SizeUnit.TiB => 1.0 / TiB,
			SizeUnit.PiB => 1.0 / PiB,
			SizeUnit.EiB => 1.0 / EiB,
			_ => throw new ArgumentOutOfRangeException(nameof(unit), "Invalid size unit")
		};

		public static SizeUnit GetNaturalUnit(ulong value)
		{
			if (value < KB) return SizeUnit.Byte;

			if ((value & (KiB - 1)) == 0)
			{ // could be base 2
				if ((value & (MiB - 1)) != 0) return SizeUnit.KiB;
				if ((value & (GiB - 1)) != 0) return SizeUnit.MiB;
				if ((value & (TiB - 1)) != 0) return SizeUnit.GiB;
				if ((value & (PiB - 1)) != 0) return SizeUnit.TiB;
				if ((value & (EiB - 1)) != 0) return SizeUnit.PiB;
				return SizeUnit.EiB;
			}

			if (value % KB == 0)
			{ // could be base 10

				if ((value % MB) != 0) return SizeUnit.KB;
				if ((value % GB) != 0) return SizeUnit.MB;
				if ((value % TB) != 0) return SizeUnit.GB;
				if ((value % PB) != 0) return SizeUnit.TB;
				if ((value % EB) != 0) return SizeUnit.PB;
				return SizeUnit.EB;
			}

			return SizeUnit.Byte;
		}

		#region Equality, Comparison...

		public override int GetHashCode() => this.Value.GetHashCode();

		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			if (obj is ByteSize bs) return Equals(bs);
			if (obj is long l) return l >= 0 && (ulong) l == this.Value;
			if (obj is int i) return i >= 0 && (ulong) i == this.Value;
			if (obj is ulong ul) return ul == this.Value;
			if (obj is uint ui) return ui == this.Value;
			return false;
		}

		public bool Equals(ByteSize other) => other.Value == this.Value;

		public bool Equals(int other) => other >= 0 && (ulong) other == this.Value;

		public bool Equals(uint other) => other == this.Value;

		public bool Equals(long other) => other >= 0 && (ulong) other == this.Value;

		public bool Equals(ulong other) => other == this.Value;

		public static bool operator ==(ByteSize left, ByteSize right) => left.Value == right.Value;

		public static bool operator ==(ByteSize left, ulong right) => left.Value == right;

		public static bool operator ==(ByteSize left, long right) => left.Value == (ulong) right && right >= 0;

		public static bool operator !=(ByteSize left, ByteSize right) => left.Value != right.Value;

		public static bool operator !=(ByteSize left, ulong right) => left.Value != right;

		public static bool operator !=(ByteSize left, long right) => left.Value != (ulong) right || right < 0;

		public static bool operator >(ByteSize left, ByteSize right) => left.Value > right.Value;

		public static bool operator >(ByteSize left, ulong right) => left.Value > right;

		public static bool operator >(ByteSize left, long right) => right < 0 || left.Value > (ulong) right;

		public static bool operator >=(ByteSize left, ByteSize right) => left.Value >= right.Value;

		public static bool operator >=(ByteSize left, ulong right) => left.Value >= right;

		public static bool operator >=(ByteSize left, long right) => right < 0 || left.Value >= (ulong) right;

		public static bool operator <(ByteSize left, ByteSize right) => left.Value < right.Value;

		public static bool operator <(ByteSize left, ulong right) => left.Value < right;

		public static bool operator <(ByteSize left, long right) => right >= 0 && left.Value < (ulong) right;

		public static bool operator <=(ByteSize left, ByteSize right) => left.Value <= right.Value;

		public static bool operator <=(ByteSize left, ulong right) => left.Value <= right;

		public static bool operator <=(ByteSize left, long right) => right >= 0 && left.Value <= (ulong) right;

		#endregion

		#region Conversion...

		/// <summary>Size with a specific unit</summary>
		[Pure]
		public static ByteSize Create(int bytes, SizeUnit unit) => new(checked((ulong) bytes), unit);

		/// <summary>Size with a specific unit</summary>
		[Pure]
		public static ByteSize Create(long bytes, SizeUnit unit) => new(checked((ulong) bytes), unit);

		/// <summary>Size with a specific unit</summary>
		[Pure]
		public static ByteSize Create(uint bytes, SizeUnit unit) => new(bytes, unit);

		/// <summary>Size with a specific unit</summary>
		[Pure]
		public static ByteSize Create(ulong bytes, SizeUnit unit) => new(bytes, unit);

		public static implicit operator ByteSize(int bytes) => new(checked((ulong) bytes), SizeUnit.Byte);

		public static implicit operator ByteSize(uint bytes) => new(bytes, SizeUnit.Byte);

		public static implicit operator ByteSize(long bytes) => new(checked((ulong) bytes), SizeUnit.Byte);

		public static implicit operator ByteSize(ulong bytes) => new(bytes, SizeUnit.Byte);

		/// <summary>Size expressed in bytes</summary>
		[Pure]
		public static ByteSize Bytes(int bytes) => new(checked((ulong) bytes), SizeUnit.Byte);

		/// <summary>Size expressed in bytes</summary>
		[Pure]
		public static ByteSize Bytes(long bytes) => new(checked((ulong) bytes), SizeUnit.Byte);

		/// <summary>Size expressed in bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteSize Bytes(uint bytes) => new(bytes, SizeUnit.Byte);

		/// <summary>Size expressed in bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ByteSize Bytes(ulong bytes) => new(bytes, SizeUnit.Byte);

		#region Base 10...

		#region Kilo...

		/// <summary>Return the size, downscaled to Kilobytes (<c>KB</c>, 10^3 bytes)</summary>
		public double ToKiloBytes() => this.Value / (double) KB;

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		[Pure]
		public static ByteSize KiloBytes(int kb) => new(checked((ulong) kb * KB), SizeUnit.KB);

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		[Pure]
		public static ByteSize KiloBytes(uint kb) => new(checked((ulong) kb * KB), SizeUnit.KB);

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		[Pure]
		public static ByteSize KiloBytes(long kb) => new(checked((ulong) kb * KB), SizeUnit.KB);

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		[Pure]
		public static ByteSize KiloBytes(ulong kb) => new(checked(kb * KB), SizeUnit.KB);

		#endregion

		#region Mega...

		/// <summary>Return the size, downscaled to Megabytes (<c>MB</c>, 10^6 bytes)</summary>
		public double ToMegaBytes() => this.Value / (double) MB;

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		[Pure]
		public static ByteSize MegaBytes(int mb) => new(checked((ulong) mb * MB), SizeUnit.MB);

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		[Pure]
		public static ByteSize MegaBytes(uint mb) => new(checked((ulong) mb * MB), SizeUnit.MB);

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		[Pure]
		public static ByteSize MegaBytes(long mb) => new(checked((ulong) mb * MB), SizeUnit.MB);

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		[Pure]
		public static ByteSize MegaBytes(ulong mb) => new(checked(mb * MB), SizeUnit.MB);

		#endregion

		#region Giga...

		/// <summary>Return the size, downscaled to Gigabytes (<c>GB</c>, 10^9 bytes)</summary>
		public double ToGigaBytes() => this.Value / (double) GB;

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		[Pure]
		public static ByteSize GigaBytes(int gb) => new(checked((ulong) gb * GB), SizeUnit.GB);

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		[Pure]
		public static ByteSize GigaBytes(uint gb) => new(checked((ulong) gb * GB), SizeUnit.GB);

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		[Pure]
		public static ByteSize GigaBytes(long gb) => new(checked((ulong) gb * GB), SizeUnit.GB);

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		[Pure]
		public static ByteSize GigaBytes(ulong gb) => new(checked(gb * GB), SizeUnit.GB);

		#endregion

		#region Tera...

		/// <summary>Return the size, downscaled to Terabytes (<c>TB</c>, 10^12 bytes)</summary>
		public double ToTeraBytes() => this.Value / (double) TB;

		/// <summary>Size expressed in Terabytes (10^12 bytes)</summary>
		[Pure]
		public static ByteSize TeraBytes(int tb) => new(checked((ulong) tb * TB), SizeUnit.TB);

		/// <summary>Size expressed in Terabytes (10^12 bytes)</summary>
		[Pure]
		public static ByteSize TeraBytes(uint tb) => new(checked((ulong) tb * TB), SizeUnit.TB);

		#endregion

		#region Peta...

		/// <summary>Return the size, downscaled to Petabytes (<c>PB</c>, 10^15 bytes)</summary>
		public double ToPetaBytes() => this.Value / (double) PB;

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		public static ByteSize PetaBytes(int pb) => new(checked((ulong) pb * PB), SizeUnit.PB);

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		public static ByteSize PetaBytes(uint pb) => new(checked((ulong) pb * PB), SizeUnit.PB);

		#endregion

		#region Exa...

		/// <summary>Return the size, downscaled to Exabytes (<c>EB</c>, 10^19 bytes)</summary>
		public double ToExaBytes() => this.Value / (double) EB;

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		[Pure]
		public static ByteSize ExaBytes(int eb) => new(checked((ulong) eb * EB), SizeUnit.EB);

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		[Pure]
		public static ByteSize ExaBytes(uint eb) => new(checked((ulong) eb * EB), SizeUnit.EB);

		#endregion

		#endregion

		#region Base 2...

		#region Kibi...

		/// <summary>Return the size, downscaled to Kibibytes (<c>KiB</c>, 2^10 bytes)</summary>
		public double ToKibiBytes() => this.Value / (double) KiB;

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(int kib) => new(checked((ulong) kib * KiB), SizeUnit.KiB);

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(uint kib) => new(checked((ulong) kib * KiB), SizeUnit.KiB);

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(long kib) => new(checked((ulong) kib * KiB), SizeUnit.KiB);

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(ulong kib) => new(checked(kib * KiB), SizeUnit.KiB);

		#endregion

		#region Mebi...

		/// <summary>Return the size, downscaled to Mebibytes (<c>MiB</c>, 2^20 bytes)</summary>
		public double ToMebiBytes() => this.Value / (double) MiB;

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(int mib) => new(checked((ulong) mib * MiB), SizeUnit.MiB);

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(uint mib) => new(checked((ulong) mib * MiB), SizeUnit.MiB);

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(long mib) => new(checked((ulong) mib * MiB), SizeUnit.MiB);

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(ulong mib) => new(checked(mib * MiB), SizeUnit.MiB);

		#endregion

		#region Gibi...

		/// <summary>Return the size, downscaled to Gibibytes (<c>GiB</c>, 2^30 bytes)</summary>
		public double ToGibiBytes() => this.Value / (double) GiB;

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes)</summary>
		public static ByteSize GibiBytes(int gib) => new(checked((ulong) gib * GiB), SizeUnit.GiB);

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes)</summary>
		public static ByteSize GibiBytes(uint gib) => new(checked((ulong) gib * GiB), SizeUnit.GiB);

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes)</summary>
		public static ByteSize GibiBytes(long gib) => new(checked((ulong) gib * GiB), SizeUnit.GiB);

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes)</summary>
		public static ByteSize GibiBytes(ulong gib) => new(checked(gib * GiB), SizeUnit.GiB);

		#endregion

		#region Tebi...

		/// <summary>Return the size, downscaled to Tebibytes (<c>TiB</c>, 2^40 bytes)</summary>
		public double ToTebiBytes() => this.Value / (double) TiB;

		/// <summary>Size expressed in <b>Tebibytes</b> (2^40 or 1,099,511,627,776 bytes)</summary>
		public static ByteSize TebiBytes(int tib) => new(checked((ulong) tib * TiB), SizeUnit.TiB);

		/// <summary>Size expressed in <b>Tebibytes</b> (2^40 or 1,099,511,627,776 bytes)</summary>
		public static ByteSize TebiBytes(uint tib) => new(checked((ulong) tib * TiB), SizeUnit.TiB);

		#endregion

		#region Pebi...

		/// <summary>Return the size, downscaled to Pebibytes (<c>PiB</c>, 2^50 bytes)</summary>
		public double ToPebiBytes() => this.Value / (double) PiB;

		/// <summary>Size expressed in <b>Pebibytes</b> (2^50 or 1,125,899,906,842,624 bytes)</summary>
		public static ByteSize PebiBytes(int pib) => new(checked((ulong) pib * PiB), SizeUnit.PiB);

		/// <summary>Size expressed in <b>Pebibytes</b> (2^50 or 1,125,899,906,842,624 bytes)</summary>
		public static ByteSize PebiBytes(uint pib) => new(checked((ulong) pib * PiB), SizeUnit.PiB);

		#endregion

		#region Exi...

		/// <summary>Return the size, downscaled to Exibytes (<c>EiB</c>, 2^60 bytes)</summary>
		public double ToExiBytes() => this.Value / (double) EiB;

		/// <summary>Size expressed in <b>Exibytes</b> (2^60 or 1,152,921,504,606,846,976 bytes)</summary>
		public static ByteSize ExiBytes(int eib) => new(checked((ulong) eib * EiB), SizeUnit.EiB);

		/// <summary>Size expressed in <b>Exibytes</b> (2^60 or 1,152,921,504,606,846,976 bytes)</summary>
		public static ByteSize ExiBytes(uint eib) => new(checked((ulong) eib * EiB), SizeUnit.EiB);

		#endregion

		#endregion

		/// <summary>Returns the value as a signed 32-bit integer</summary>
		/// <exception cref="OverflowException">If the value is larger than <c>int.MaxValue</c> (~2 GiB)</exception>
		public int ToInt32() => checked((int) this.Value);

		/// <summary>Returns the value as an unsigned 32-bit integer</summary>
		/// <exception cref="OverflowException">If the value is larger than <c>uint.MaxValue</c> (~4 GiB)</exception>
		public uint ToUInt32() => checked((uint) this.Value);

		/// <summary>Returns the value as a signed 64-bit integer</summary>
		/// <exception cref="OverflowException">If the value is larger than <c>long.MaxValue</c> (~4 GiB)</exception>
		public long ToInt64() => checked((long) this.Value);

		/// <summary>Returns the value as an unsigned 64-bit integer</summary>
		public ulong ToUInt64() => this.Value;

		public static implicit operator int(ByteSize size) => checked((int) size.Value);

		public static implicit operator uint(ByteSize size) => checked((uint) size.Value);

		public static implicit operator long(ByteSize size) => checked((long) size.Value);

		public static implicit operator ulong(ByteSize size) => size.Value;

		/// <summary>Returns a string representation of this instance, using the best unit (without rounding errors).</summary>
		public string ToNaturalString(IFormatProvider? provider = null)
		{
			if (this.Unit == SizeUnit.Byte)
			{ // respect the original unit

				var unit = GetNaturalUnit(this.Value);
				if (unit != SizeUnit.Byte)
				{
					return new ByteSize(this.Value, unit).ToString(null, provider);
				}
			}

			return this.ToString(null, provider);
		}

		/// <summary>Returns a string representation of this instance, using its natural unit (with rounding if necessary).</summary>
		public string ToApproximateString(bool base2 = false, IFormatProvider? provider = null)
		{
			provider ??= CultureInfo.InvariantCulture;

			SizeUnit unit;
			if (base2)
			{
				unit = this.Value switch
				{
					< ByteSize.KiB => SizeUnit.Byte,
					< ByteSize.MiB => SizeUnit.KiB,
					< ByteSize.GiB => SizeUnit.MiB,
					< ByteSize.TiB => SizeUnit.GiB,
					_ => SizeUnit.TiB,
				};
			}
			else
			{
				unit = this.Value switch
				{
					< ByteSize.KB => SizeUnit.Byte,
					< ByteSize.MB => SizeUnit.KB,
					< ByteSize.GB => SizeUnit.MB,
					< ByteSize.TB => SizeUnit.GB,
					_ => SizeUnit.TB,
				};
			}

			if (unit == SizeUnit.Byte)
			{
				return this.Value == 1 ? "1 byte" : (this.Value.ToString("N0") + " bytes");
			}

			var ratio = GetUnitRatio(unit);

			string fmt = unit is SizeUnit.KiB or SizeUnit.KB ? "N2" : "N1";

			return (this.Value * ratio).ToString(fmt, provider) + " " + GetUnitLiteral(unit);

		}

		#endregion

		#region Parsing...

		/// <summary>Tries parsing a string representation of a size ("1 KB", "2 MiB", ...)</summary>
		public static bool TryParse(ReadOnlySpan<char> literal, out ByteSize value)
		{
			if (literal.Length == 0) goto invalid;

			int offset;
			SizeUnit unit;

			// unit detection

			char last = literal[^1];

			if (char.IsDigit(last))
			{ // probably bytes
				offset = 0;
				unit = SizeUnit.Byte;
			}
			else if (last == 'B')
			{
				if (literal.Length > 3 && literal[^2] == 'i')
				{ // ends with "iB", could be KiB, MiB, ...

					char u = literal[^3];

					if (u == 'K')
					{ // "KiB" means 2^10 bytes
						unit = SizeUnit.KiB;
					}
					else if (u == 'M')
					{ // "MiB" means 2^20 bytes
						unit = SizeUnit.MiB;
					}
					else if (u == 'G')
					{ // "GiB" means 2^30 bytes
						unit = SizeUnit.GiB;
					}
					else if (u == 'T')
					{ // "TiB" means 2^40 bytes
						unit = SizeUnit.TiB;
					}
					else if (u == 'P')
					{ // "PiB" means 2^50 bytes
						unit = SizeUnit.PiB;
					}
					else if (u == 'E')
					{ // "EiB" means 2^60 bytes
						unit = SizeUnit.EiB;
					}
					else
					{
						goto invalid;
					}
					offset = 3;
				}
				else if (literal.Length > 2)
				{ // ends with 'B' (but not 'iB'), could be "KB", "MB", "GB", ...
					var u = literal[^2];
					if (u == 'K')
					{ // "KB" means 10^3 bytes
						offset = 2;
						unit = SizeUnit.KB;
					}
					else if (u == 'M')
					{ // "MB" means 10^6 bytes
						offset = 2;
						unit = SizeUnit.MB;
					}
					else if (u == 'G')
					{ // "GB" means 10^9 bytes
						offset = 2;
						unit = SizeUnit.GB;
					}
					else if (u == 'T')
					{ // "TB" means 10^12 bytes
						offset = 2;
						unit = SizeUnit.TB;
					}
					else if (u == 'P')
					{ // "PB" means 10^15 bytes
						offset = 2;
						unit = SizeUnit.PB;
					}
					else if (u == 'E')
					{ // "EB" means 10^19 bytes
						offset = 2;
						unit = SizeUnit.EB;
					}
					else
					{ // probably bytes
						offset = 1;
						unit = SizeUnit.Byte;
					}
				}
				else
				{
					goto invalid;
				}
			}
			else if (char.IsUpper(last))
			{
				if (last == 'K')
				{ // "K" means "KiB" or 2^10 bytes
					offset = 1;
					unit = SizeUnit.KiB;
				}
				else if (last == 'M')
				{ // "M" means "MiB" or 2^20 bytes
					offset = 1;
					unit = SizeUnit.MiB;
				}
				else if (last == 'G')
				{ // "G" means "GiB" or 2^30 bytes
					offset = 1;
					unit = SizeUnit.GiB;
				}
				else if (last == 'T')
				{ // "T" means "TiB" or 2^40 bytes
					offset = 1;
					unit = SizeUnit.TiB;
				}
				else if (last == 'P')
				{ // "P" means "PiB" or 2^50 bytes
					offset = 1;
					unit = SizeUnit.TiB;
				}
				else if (last == 'E')
				{ // "P" means "EiB" or 2^60 bytes
					offset = 1;
					unit = SizeUnit.EiB;
				}
				else
				{
					goto invalid;
				}
			}
			else
			{
				goto invalid;
			}

			literal = (offset > 0 ? literal[..^offset] : literal).Trim();
			if (!long.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out var num))
			{
				goto invalid;
			}

			try
			{
				value = new(checked((ulong) num * GetUnitSize(unit)), unit);
				return true;
			}
			catch (OverflowException)
			{
				goto invalid;
			}

		invalid:
			value = default;
			return false;
		}

		/// <summary>Gets the string literal for a specific <see cref="SizeUnit"/> (ex: <c>"MB"</c> for <see cref="SizeUnit.MB"/>, <c>"GiB"</c> for <see cref="SizeUnit.GB"/></summary>
		public static string GetUnitLiteral(SizeUnit unit) => unit switch
		{
			SizeUnit.Byte => "b",
			SizeUnit.KB => "KB",
			SizeUnit.MB => "MB",
			SizeUnit.GB => "GB",
			SizeUnit.TB => "TB",
			SizeUnit.PB => "PB",
			SizeUnit.EB => "EB",
			SizeUnit.KiB => "KiB",
			SizeUnit.MiB => "MiB",
			SizeUnit.GiB => "GiB",
			SizeUnit.TiB => "TiB",
			SizeUnit.PiB => "PiB",
			SizeUnit.EiB => "EiB",
			_ => throw new ArgumentOutOfRangeException(nameof(unit), "Unknown size unit")
		};

		/// <summary>Gets the size unit that corresponds to a string literal (ex: <see cref="SizeUnit.MB"/> for <c>"MB"</c>, <see cref="SizeUnit.GB"/> for <c>"GiB"</c></summary>
		public static SizeUnit FromUnitLiteral(string literal) => literal switch
		{
			"b" or "B" => SizeUnit.Byte,
			"kb" or "KB" => SizeUnit.KB,
			"mb" or "MB" => SizeUnit.MB,
			"gb" or "GB" => SizeUnit.GB,
			"tb" or "TB" => SizeUnit.TB,
			"pb" or "PB" => SizeUnit.PB,
			"eb" or "EB" => SizeUnit.EB,
			"kib" or "KiB" => SizeUnit.KiB,
			"mib" or "MiB" => SizeUnit.MiB,
			"gib" or "GiB" => SizeUnit.GiB,
			"tib" or "TiB" => SizeUnit.TiB,
			"pib" or "PiB" => SizeUnit.PiB,
			"eib" or "EiB" => SizeUnit.EiB,
			_ => throw new ArgumentOutOfRangeException(nameof(literal), "Unknown size unit")
		};

		/// <summary>Gets the size unit that corresponds to a string literal (ex: <see cref="SizeUnit.MB"/> for <c>"MB"</c>, <see cref="SizeUnit.GB"/> for <c>"GiB"</c></summary>
		public static SizeUnit FromUnitLiteral(ReadOnlySpan<char> literal) => literal switch
		{
			"b" or "B" => SizeUnit.Byte,
			"kb" or "KB" => SizeUnit.KB,
			"mb" or "MB" => SizeUnit.MB,
			"gb" or "GB" => SizeUnit.GB,
			"tb" or "TB" => SizeUnit.TB,
			"pb" or "PB" => SizeUnit.PB,
			"eb" or "EB" => SizeUnit.EB,
			"kib" or "KiB" => SizeUnit.KiB,
			"mib" or "MiB" => SizeUnit.MiB,
			"gib" or "GiB" => SizeUnit.GiB,
			"tib" or "TiB" => SizeUnit.TiB,
			"pib" or "PiB" => SizeUnit.PiB,
			"eib" or "EiB" => SizeUnit.EiB,
			_ => throw new ArgumentOutOfRangeException(nameof(literal), "Unknown size unit")
		};

		#endregion

	}

}
