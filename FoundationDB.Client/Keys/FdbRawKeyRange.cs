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

	/// <summary>Range where the "Begin" and "End" key are already encoded</summary>
	public readonly struct FdbRawKeyRange : IFdbKeyRange
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbRawKeyRange(Slice begin, Slice end)
		{
			this.Begin = begin;
			this.End = end;
		}

		/// <summary>Begin key (inclusive)</summary>
		public readonly Slice Begin;

		/// <summary>End key (exclusive)</summary>
		public readonly Slice End;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => new(this.Begin, this.End);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToBeginKey() => this.Begin;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToEndKey() => this.End;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToBeginSelector() => KeySelector.FirstGreaterOrEqual(this.Begin);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToEndSelector() => KeySelector.FirstGreaterOrEqual(this.End);

		/// <inheritdoc />
		[Pure]
		public bool Contains(Slice key) => key.CompareTo(this.Begin) >= 0 && key.CompareTo(this.End) < 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key) => key.SequenceCompareTo(this.Begin.Span) >= 0 && key.SequenceCompareTo(this.End.Span) < 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			return key.CompareTo(this.Begin) >= 0 && key.CompareTo(this.End) < 0;
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> ToKeyRange().ToString(format, formatProvider);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
			=> ToKeyRange().TryFormat(destination, out charsWritten, format, provider);

	}

}
