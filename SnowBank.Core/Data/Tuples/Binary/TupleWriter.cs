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
	using System.ComponentModel;
	using SnowBank.Buffers;

	/// <summary>Writes bytes to a contiguous region of arbitrary memory</summary>
	[DebuggerDisplay("{Output.Position}/{Output.Buffer.Length} @ {Depth}")]
	[DebuggerNonUserCode]
	public readonly ref struct TupleWriter
	{

		/// <summary>Buffer where the tuple will be written</summary>
		public readonly ref SliceWriter Output;

		/// <summary>Current depth (0 for top-level, >= 1 for embedded tuples</summary>
		public readonly int Depth;

		[Obsolete("You must pass an external SliceWriter by reference!", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public TupleWriter() { throw new NotSupportedException(); }

		/// <summary>Constructs a <see cref="TupleWriter"/> that wraps an externally supplied <see cref="SliceWriter"/></summary>
		/// <param name="buffer">Buffer where the tuple will be written</param>
		/// <param name="depth">Initial depth of this writer</param>
		[SkipLocalsInit]
		public TupleWriter(ref SliceWriter buffer, int depth = 0)
		{
			this.Output = ref buffer;
			this.Depth = depth;
		}

	}

}
