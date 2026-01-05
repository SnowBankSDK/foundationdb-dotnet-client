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

	/// <summary>Value that wraps raw bytes</summary>
	[PublicAPI]
	public readonly struct FdbRawValue : IFdbValue
	{

		/// <summary>Value that is equivalent to the <see cref="Slice.Nil"/> slice.</summary>
		/// <remarks>This can be used to represent a missing value, or a "Not Found" result.</remarks>
		public static readonly FdbRawValue Nil;

		/// <summary>Value that is equivalent to the <see cref="Slice.Empty"/> slice.</summary>
		public static readonly FdbRawValue Empty = new(Slice.Empty);

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbRawValue(Slice data, FdbValueTypeHint typeHint = FdbValueTypeHint.None)
		{
			this.Data = data;
			this.TypeHint = typeHint;
		}

		/// <summary>Wrapped bytes</summary>
		public readonly Slice Data;

		/// <summary>Hint about the type of the wrapped data</summary>
		public readonly FdbValueTypeHint TypeHint;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => this.TypeHint;

		public ReadOnlySpan<byte> Span
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Data.Span;
		}

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => this.Data.ToString();

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider)
			=> this.Data.ToString(format, formatProvider);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> this.Data.TryFormat(destination, out charsWritten, format, provider);

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = this.Data.Span;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Data.Count;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.TryCopyTo(destination, out bytesWritten);
		}

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => this.Span.SequenceEqual(bytes.Span),
			FdbRawValue value => this.Span.SequenceEqual(value.Span),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => this.Span.SequenceEqual(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => this.Span.SequenceEqual(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => this.Span.SequenceEqual(other);

		#endregion

	}

}
