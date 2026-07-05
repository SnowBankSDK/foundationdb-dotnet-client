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

namespace SnowBank.Data.Binary
{
	/// <summary>Defines methods to encode the binary representation of instances of type <typeparamref name="TValue"/> into a span</summary>
	/// <typeparam name="TValue">Type of the encoded values</typeparam>
	/// <remarks>
	/// <para>This type is expected to be implemented on structs <b>ONLY</b>, in order to achieve the best performance by using JIT inlining and other optimizations as much as possible.</para>
	/// </remarks>
	public interface ISpanEncoder<TValue>
#if NET9_0_OR_GREATER
		where TValue : allows ref struct
#endif
	{

#if NET7_0_OR_GREATER
		// static abstract interface members need runtime support that netstandard2.0/netfx lacks:
		// there the interface is a marker only, and the encoder structs expose the same methods as plain statics

		/// <summary>Returns a span of the encoded representation of the value, if it can be done without any memory allocations</summary>
		/// <param name="value">Value to encode</param>
		/// <param name="span">Receives a span with the encoded value</param>
		/// <returns><c>true</c> if the span is available; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This should only return true when the in-memory layout of the value is the same as its encoded representation.</para>
		/// <para>If this method returns <c>false</c>, use <see cref="TryEncode"/> to encode the value into a temporary buffer.</para>
		/// </remarks>
		static abstract bool TryGetSpan(scoped in TValue? value, out ReadOnlySpan<byte> span);

		/// <summary>Returns a hint for the minimum capacity required to format the value.</summary>
		/// <param name="value">Value that needs to be encoded</param>
		/// <param name="sizeHint">Receives the minimum buffer size that should be passed to <see cref="TryEncode"/></param>
		/// <returns><c>true</c> if a minimum size is known, or <c>false</c> if computing this size would be too costly</returns>
		/// <remarks>
		/// <para>The returned capacity <b>MAY</b> be smaller than the actual size required: some encoders may return a good estimate for 99%+ of the cases, in which case the capacity is a good starting point.</para>
		/// <para>For example, a UTF-8 encoder may assume that each character will take 2 bytes <i>on average</i>, which is smaller than the maximum of 3 bytes.</para>
		/// </remarks>
		static abstract bool TryGetSizeHint(scoped in TValue? value, out int sizeHint);

		/// <summary>Encodes the value to the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer that will receive the encoded representation of the value</param>
		/// <param name="bytesWritten">Number of bytes that where written to the buffer, if the operation is successful</param>
		/// <param name="value">Value to encode</param>
		/// <returns><c>false</c> if the buffer is not large enough, <c>true</c> if the operation was successful, or an exception if the encoding failed for other reasons</returns>
		/// <remarks>
		/// <para>This method behaves similarly to <see cref="ISpanFormattable.TryFormat"/>: the caller allocates a buffer with a safe initial capacity. If the buffer is too small, then the caller should retry with a larger buffer, until the method returns <c>true</c> or fails.</para>
		/// <para>Please note that the method MUST NOT return <c>false</c> for a reason other than a buffer being too small, otherwise the caller may end up in an infinite retry loop, passing a larger and larger buffer.</para>
		/// </remarks>
		static abstract bool TryEncode(Span<byte> destination, out int bytesWritten, scoped in TValue? value);

#endif

	}

}
