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

	/// <summary>Value that wraps a 32-bit unsigned value, encoded in little-endian, using as few bytes as possible</summary>
	public readonly struct FdbCompactLittleEndianUInt32Value : IFdbValue
		, IEquatable<FdbCompactLittleEndianUInt32Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbCompactLittleEndianUInt32Value(uint value) => this.Value = value;

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
			"G" or "g" => string.CreateInvariant($"{nameof(FdbCompactLittleEndianUInt32Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbCompactLittleEndianUInt32Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.CompactLittleEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.CompactLittleEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.CompactLittleEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbCompactLittleEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbCompactLittleEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbCompactLittleEndianUInt32Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count <= 4 && other.Data.ToUInt32() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count <= 4 && other.ToUInt32() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length <= 4 && other.ToUInt32() == this.Value;

		#endregion

	}

	/// <summary>Value that wraps a 64-bit unsigned value, encoded in little-endian, using as few bytes as possible</summary>
	public readonly struct FdbCompactLittleEndianUInt64Value : IFdbValue
		, IEquatable<FdbCompactLittleEndianUInt64Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbCompactLittleEndianUInt64Value(ulong value) => this.Value = value;

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
			"G" or "g" => string.CreateInvariant($"{nameof(FdbCompactLittleEndianUInt64Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbCompactLittleEndianUInt64Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.CompactLittleEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.CompactLittleEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.CompactLittleEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbCompactLittleEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbCompactLittleEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbCompactLittleEndianUInt64Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count <= 8 && other.Data.ToUInt64() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count <= 8 && other.ToUInt64() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length <= 8 && other.ToUInt64() == this.Value;

		#endregion

	}

	/// <summary>Value that wraps a 32-bit unsigned value, encoded in big-endian, using as few bytes as possible</summary>
	public readonly struct FdbCompactBigEndianUInt32Value : IFdbValue
		, IEquatable<FdbCompactBigEndianUInt32Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbCompactBigEndianUInt32Value(uint value) => this.Value = value;

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
			"G" or "g" => string.CreateInvariant($"{nameof(FdbCompactBigEndianUInt32Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbCompactBigEndianUInt32Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.CompactBigEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.CompactBigEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.CompactBigEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbCompactBigEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbCompactBigEndianUInt32Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbCompactBigEndianUInt32Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count <= 4 && other.Data.ToUInt32BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count <= 4 && other.ToUInt32BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length <= 4 && other.ToUInt32BE() == this.Value;

		#endregion

	}

	/// <summary>Value that wraps a 64-bit unsigned value, encoded in big-endian, using as few bytes as possible</summary>
	public readonly struct FdbCompactBigEndianUInt64Value : IFdbValue
		, IEquatable<FdbCompactBigEndianUInt64Value>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbCompactBigEndianUInt64Value(ulong value) => this.Value = value;

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
			"G" or "g" => string.CreateInvariant($"{nameof(FdbCompactBigEndianUInt64Value)}({this.Value})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Value.TryFormat(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbCompactBigEndianUInt64Value)}({this.Value})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.CompactBigEndianEncoder.TryGetSpan(in this.Value, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.CompactBigEndianEncoder.TryGetSizeHint(in this.Value, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.CompactBigEndianEncoder.TryEncode(destination, out bytesWritten, in this.Value);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbCompactBigEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbCompactBigEndianUInt64Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbCompactBigEndianUInt64Value other) => this.Value == other.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => other.Data.Count <= 8 && other.Data.ToUInt64BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => other.Count <= 8 && other.ToUInt64BE() == this.Value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => other.Length <= 8 && other.ToUInt64BE() == this.Value;

		#endregion

	}

}
