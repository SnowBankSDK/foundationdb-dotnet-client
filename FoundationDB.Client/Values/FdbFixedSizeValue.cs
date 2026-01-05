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

namespace FoundationDB.Client
{

	/// <summary>Value that wraps a 32-bit unsigned value, encoded as 4 bytes in little-endian</summary>
	public readonly struct FdbLittleEndianUInt32Value : IFdbValue
		, IEquatable<FdbLittleEndianUInt32Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbLittleEndianUInt32Value(uint value) => this.Value = value;

		/// <summary>Integer value</summary>
		public readonly uint Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.IntegerLittleEndian;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.ToString(null, null),
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => string.Create(CultureInfo.InvariantCulture, $"{nameof(FdbLittleEndianUInt32Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbLittleEndianUInt32Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.FixedSizeLittleEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.FixedSizeLittleEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.FixedSizeLittleEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			FdbLittleEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbLittleEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbLittleEndianUInt32Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count == 4 && other.Data.ToUInt32() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count == 4 && other.ToUInt32() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length == 4 && other.ToUInt32() == this.Value;

		#endregion

	}

	/// <summary>Value that wraps a 64-bit unsigned value, encoded as 8 bytes in little-endian</summary>
	public readonly struct FdbLittleEndianUInt64Value : IFdbValue
		, IEquatable<FdbLittleEndianUInt64Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbLittleEndianUInt64Value(ulong value) => this.Value = value;

		/// <summary>Integer value</summary>
		public readonly ulong Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.IntegerLittleEndian;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.ToString(null, null),
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => string.Create(CultureInfo.InvariantCulture, $"{nameof(FdbLittleEndianUInt64Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbLittleEndianUInt64Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.FixedSizeLittleEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.FixedSizeLittleEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.FixedSizeLittleEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			FdbLittleEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbLittleEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbLittleEndianUInt64Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count == 8 && other.Data.ToUInt64() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count == 8 && other.ToUInt64() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length == 8 && other.ToUInt64() == this.Value;

		#endregion

	}

	/// <summary>Value that wraps a 32-bit unsigned value, encoded as 4 bytes in big-endian</summary>
	public readonly struct FdbBigEndianUInt32Value : IFdbValue
		, IEquatable<FdbBigEndianUInt32Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbBigEndianUInt32Value(uint value) => this.Value = value;

		/// <summary>Integer value</summary>
		public readonly uint Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.IntegerBigEndian;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.ToString(null, null),
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => string.Create(CultureInfo.InvariantCulture, $"{nameof(FdbBigEndianUInt32Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbBigEndianUInt32Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.FixedSizeBigEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.FixedSizeBigEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.FixedSizeBigEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			FdbBigEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbBigEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbBigEndianUInt32Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count == 4 && other.Data.ToUInt32BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count == 4 && other.ToUInt32BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length == 4 && other.ToUInt32BE() == this.Value;

		#endregion

	}

	/// <summary>Value that wraps a 64-bit unsigned value, encoded as 8 bytes in big-endian</summary>
	public readonly struct FdbBigEndianUInt64Value : IFdbValue
		, IEquatable<FdbBigEndianUInt64Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbBigEndianUInt64Value(ulong value) => this.Value = value;

		/// <summary>Integer value</summary>
		public readonly ulong Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.IntegerBigEndian;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.ToString(null, null),
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => string.Create(CultureInfo.InvariantCulture, $"{nameof(FdbBigEndianUInt64Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbBigEndianUInt64Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.FixedSizeBigEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.FixedSizeBigEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.FixedSizeBigEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			FdbBigEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbBigEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbBigEndianUInt64Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count == 8 && other.Data.ToUInt64BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count == 8 && other.ToUInt64BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length == 8 && other.ToUInt64BE() == this.Value;

		#endregion

	}

}
