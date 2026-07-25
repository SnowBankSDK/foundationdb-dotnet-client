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

namespace FoundationDB.Storage.FdbLite
{

	/// <summary>Block-addressed storage under the engine.</summary>
	/// <remarks>
	/// <para>Blocks (<see cref="FdbLiteGeometry.BlockSize"/>) are the allocation unit. The file is presented as fixed-size mapping regions; a read never crosses a region boundary, which is why the allocator never places a page or extent across one (<see cref="RegionSizeInBlocks"/>).</para>
	/// <para><see cref="ReadBlocks"/> hands out read-only spans over the pager's own memory (mapped regions, or heap arrays for the in-memory pager); the span stays valid until the blocks are reused or truncated - the engine's pin/horizon machinery provides that guarantee, and the read-only mapping makes stray writes fault at the culprit.</para>
	/// <para>Writes are positional and only durable after <see cref="Flush"/>; the two-fsync commit protocol is: write data blocks, Flush, write the snapshot header block, Flush.</para>
	/// </remarks>
	public interface IFdbLitePager : IDisposable
	{

		/// <summary>Size parameters of this store</summary>
		FdbLiteGeometry Geometry { get; }

		/// <summary>Number of addressable blocks (the current file length)</summary>
		uint BlockCount { get; }

		/// <summary>Number of blocks per mapping region (a power of two): reads, pages, and extents never straddle a region boundary</summary>
		uint RegionSizeInBlocks { get; }

		/// <summary>Returns a read-only view over a contiguous run of blocks (which must not straddle a region boundary).</summary>
		ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count);

		/// <summary>Writes a contiguous run of blocks (which must not straddle a region boundary); durable only after <see cref="Flush"/>.</summary>
		void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data);

		/// <summary>Forces everything written so far to durable storage.</summary>
		void Flush();

		/// <summary>Grows the store to at least the requested number of blocks.</summary>
		void Grow(uint minimumBlockCount);

		/// <summary>Shrinks the store to the requested number of blocks (the caller guarantees nothing above is referenced by any retained generation or pin).</summary>
		void Truncate(uint newBlockCount);

	}

}
