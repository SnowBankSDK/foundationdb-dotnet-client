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

namespace SnowBank.Buffers
{
	using System.Buffers;

	/// <summary>Represents an output sink into which <typeparam name="T"/> data can be written.</summary>
	/// <remarks>This is a simpler version of <see cref="IBufferWriter{T}"/> for containers that don't use managed memory, and thus cannot implement <see cref="IBufferWriter{T}.GetMemory"/>.</remarks>
	public interface ISpanBufferWriter<T>
	{
		/// <summary>
		/// Notifies <see cref="ISpanBufferWriter{T}"/> that <paramref name="count"/> amount of data was written to the output <see cref="Span{T}"/>/<see cref="Memory{T}"/>
		/// </summary>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		void Advance(int count);

		/// <summary>
		/// Returns a <see cref="Span{T}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
		/// </summary>
		/// <remarks>
		/// This must never return an empty <see cref="Span{T}"/> but it can throw
		/// if the requested buffer size is not available.
		/// </remarks>
		/// <remarks>
		/// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
		/// </remarks>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		Span<T> GetSpan(int sizeHint = 0);
	}

}
