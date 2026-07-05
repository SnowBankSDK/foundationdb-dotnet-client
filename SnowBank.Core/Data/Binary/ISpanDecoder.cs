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

	/// <summary>Defines methods to decode the binary representation of instances of type <typeparamref name="TValue"/> from a span</summary>
	/// <typeparam name="TValue">Type of the decoded values</typeparam>
	/// <remarks>
	/// <para>This type is expected to be implemented on structs <b>ONLY</b>, in order to achieve the best performance by using JIT inlining and other optimizations as much as possible.</para>
	/// </remarks>
	public interface ISpanDecoder<TValue>
	{

#if NET7_0_OR_GREATER
		// static abstract interface members need runtime support that netstandard2.0/netfx lacks:
		// there the interface is a marker only, and the decoder structs expose the same method as a plain static

		/// <summary>Decodes the value from the input buffer, if it contains enough data</summary>
		/// <param name="source">Source buffer with bytes to decode</param>
		/// <param name="value">Receives the decoded value, if the operation was successful</param>
		/// <returns><c>true</c> if the value was successfully decoded, <c>false</c> if the buffer does not contain a full representation of the value, or an exception if the decoding failed for other reasons.</returns>
		/// <remarks>
		/// <para>The caller should first call this method with the first chunk of data available. As long as the method returns <c>false</c>, the caller should wait for more data to arrive before calling it again, until it returns <c>true</c>.</para>
		/// <para>Please note that the method MUST NOT return <c>false</c> for a reason other than the buffer not containing enough data, otherwise the caller may end up in an infinite retry loop, passing a larger and larger buffer.</para>
		/// <para>If the data is incomplete, but the available data is already malformed, the method <b>SHOULD</b> throw an exception, in order to avoid waiting for more data before throwing.</para>
		/// </remarks>
		static abstract bool TryDecode(ReadOnlySpan<byte> source, out TValue? value);

#endif

	}

}
