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

namespace SnowBank.Data.Tuples.Binary
{
	using SnowBank.Buffers.Text;

	/// <summary>Represents an object that can serialize itself using the Tuple Binary Encoding format</summary>
	[PublicAPI]
	public interface ITuplePackable
	{
		/// <summary>Appends the packed bytes of this instance to the end of a buffer</summary>
		/// <param name="writer">Buffer that will receive the packed bytes of this instance</param>
		void PackTo(TupleWriter writer);

	}

	/// <summary>Represents an object that can serialize itself using the Tuple Binary Encoding format</summary>
	[PublicAPI]
	public interface ITupleSpanPackable : ITuplePackable
	{
		/// <summary>Appends the packed bytes of this instance to the end of a buffer, if it is large enough</summary>
		/// <param name="writer">Buffer that will receive the packed bytes of this instance</param>
		/// <remarks><c>true</c> if the operation was successful, or <c>false</c> if it was too small</remarks>
		/// <remarks>
		/// <para>If this method returns <c>false</c>, the caller should retry with a larger buffer.</para>
		/// <para>Implementors of this method should ONLY return <c>false</c> when the buffer is too small, and <b>MUST</b> throw exceptions if the data is invalid, or cannot be formatted even with a larger buffer. Failure to do so may cause an infinite loop!</para>
		/// </remarks>
		bool TryPackTo(ref TupleSpanWriter writer);

		/// <summary>Attempts to quickly estimate the minimum buffer capacity required to pack this tuple</summary>
		/// <param name="embedded"><c>true</c> if this is an embedded tuple, <c>false</c> if this is the top-level</param>
		/// <param name="sizeHint">Receives the estimated size, or <c>0</c></param>
		/// <returns><c>true</c> if the capacity could be quickly estimated, or <c>false</c> if it would require almost as much work as encoding the tuple.</returns>
		/// <remarks>The result MUST include the start/stop bytes for embedded tuples (which will be accounted by the parent)</remarks>
		bool TryGetSizeHint(bool embedded, out int sizeHint);

	}

	/// <summary>Represents a tuple that can formats its item into a string representation</summary>
	[PublicAPI]
	public interface ITupleFormattable
	{

		/// <summary>Writes the string representation of the items of this tuple</summary>
		/// <param name="sb">Output buffer</param>
		/// <returns>Number of items written</returns>
		/// <remarks>This method should not emit any <c>"("</c> or <c>")"</c> delimiters, but should insert <c>", "</c> separators between items</remarks>
		int AppendItemsTo(ref FastStringBuilder sb);

	}

}
