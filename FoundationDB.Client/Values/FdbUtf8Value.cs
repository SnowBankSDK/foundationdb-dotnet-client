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

	/// <summary>Value that wraps text that is encoded as UTF-8 bytes</summary>
	public readonly struct FdbUtf8Value : IFdbValue
		, IEquatable<FdbUtf8Value>
#if NET9_0_OR_GREATER
		, IEquatable<FdbUtf8SpanValue>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf8Value(string text)
		{
			this.Text = text.AsMemory();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf8Value(ReadOnlyMemory<char> text)
		{
			this.Text = text;
		}

		/// <summary>Wrapped text fragment</summary>
		public readonly ReadOnlyMemory<char> Text;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.Utf8;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => $"\"{this.Text.Span}\"", //TODO: escape?
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => $"{nameof(FdbUtf8Value)}(\"{this.Text.Span}\")", //TODO: escape?
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => destination.TryWrite($"\"{this.Text.Span}\"", out charsWritten), //TODO: escape?
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite($"{nameof(FdbUtf8Value)}(\"{this.Text.Span}\")", out charsWritten), //TODO: escape?
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.Utf8Encoder.TryGetSpan(in this.Text, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.Utf8Encoder.TryGetSizeHint(in this.Text, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.Utf8Encoder.TryEncode(destination, out bytesWritten, in this.Text);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbUtf8Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbUtf8Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

#if NET9_0_OR_GREATER

		/// <inheritdoc cref="Equals(FdbUtf8Value)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbUtf8SpanValue other) => this.Text.Span.SequenceEqual(other.Text);

#endif

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbUtf8Value other) => this.Text.Span.SequenceEqual(other.Text.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbValueHelpers.AreEqual(in this, other);
		//PERF: is there a fast way to compare UTF-8 bytes with a RoS<char> ?

		#endregion

	}

#if NET9_0_OR_GREATER

	/// <summary>Value that wraps text that is encoded as UTF-8 bytes</summary>
	public readonly ref struct FdbUtf8SpanValue : IFdbValue
		, IEquatable<FdbUtf8Value>
		, IEquatable<FdbUtf8SpanValue>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf8SpanValue(ReadOnlySpan<char> text)
		{
			this.Text = text;
		}

		/// <summary>Wrapped text fragment</summary>
		public readonly ReadOnlySpan<char> Text;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.Utf8;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => $"\"{this.Text}\"", //TODO: escape?
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => $"{nameof(FdbUtf8SpanValue)}(\"{this.Text}\")", //TODO: escape?
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => destination.TryWrite($"\"{this.Text}\"", out charsWritten), //TODO: escape?
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite($"{nameof(FdbUtf8SpanValue)}(\"{this.Text}\")", out charsWritten), //TODO: escape?
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.Utf8Encoder.TryGetSpan(in this.Text, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.Utf8Encoder.TryGetSizeHint(in this.Text, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.Utf8Encoder.TryEncode(destination, out bytesWritten, in this.Text);

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbUtf8Value value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbUtf8Value value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbUtf8SpanValue other) => this.Text.SequenceEqual(other.Text);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbUtf8Value other) => this.Text.SequenceEqual(other.Text.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbValueHelpers.AreEqual(in this, other);
		//PERF: is there a fast way to compare UTF-8 bytes with a RoS<char> ?

		#endregion

	}

#endif
}
