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

	/// <summary>Runtime size parameters of a store: the allocation-block size and the tree-page size (as a block multiple).</summary>
	/// <remarks>
	/// <para>Both sizes are per-store data, carried in the file header and chosen at store creation: the engine must run correctly at ANY legal geometry, and no code may assume a fixed size at compile time (the default geometry is a tuning decision made on benchmarks, and the sizes double as per-store tuning knobs).</para>
	/// <para>The 64 KiB tree-page ceiling is load-bearing: the page format's 2-byte slot/offset fields span exactly that much, and a larger page would force wider offsets in every slot of every page.</para>
	/// </remarks>
	[DebuggerDisplay("Block={BlockSize}, Page={PageSize}")]
	public readonly struct FdbLiteGeometry : IEquatable<FdbLiteGeometry>
	{

		/// <summary>Smallest supported allocation block: 4 KiB</summary>
		public const int MinBlockSizeLog2 = 12;

		/// <summary>Largest supported tree page: 64 KiB (the u16 slot/offset ceiling)</summary>
		public const int MaxPageSizeLog2 = 16;

		/// <summary>Smallest supported tree page: 16 KiB, the smallest power of two that holds one maximum-size key cell inline (keys never chain-walk)</summary>
		public const int MinPageSizeLog2 = 14;

		/// <summary>Longest inline value the leaf format can address: the key-heap entry holds the value length in a <see cref="ushort"/>.</summary>
		public const int MaxAddressableInlineValueLength = ushort.MaxValue;

		static FdbLiteGeometry()
		{
			// The leaf key-heap entry stores the inline value length in a u16, which is only wide enough because the
			// largest legal page is 64 KiB and an inline value is a quarter of a page. Raising MaxPageSizeLog2 without
			// widening that field would silently truncate a value, which reads back as corruption rather than as an
			// error, so the two are pinned together here and the mismatch fails on first touch of the type.
			int largestInlineValue = (1 << MaxPageSizeLog2) >> 2;
			Contract.Requires(largestInlineValue <= MaxAddressableInlineValueLength, "The largest legal page yields an inline value longer than the leaf format can address. Widen the value-length field in the key-heap entry before raising the maximum page size.");
		}

		/// <summary>Creates a geometry from the two file-header fields.</summary>
		/// <param name="blockSizeLog2">Allocation-block size, as log2 (12..16)</param>
		/// <param name="pageSizeInBlocksLog2">Tree-page size in blocks, as log2 (0 = one block per page)</param>
		public FdbLiteGeometry(int blockSizeLog2, int pageSizeInBlocksLog2)
		{
			Contract.Between(blockSizeLog2, MinBlockSizeLog2, MaxPageSizeLog2);
			Contract.Between(pageSizeInBlocksLog2, 0, MaxPageSizeLog2 - MinBlockSizeLog2);
			int pageSizeLog2 = blockSizeLog2 + pageSizeInBlocksLog2;
			Contract.Between(pageSizeLog2, MinPageSizeLog2, MaxPageSizeLog2);

			this.BlockSizeLog2 = (byte) blockSizeLog2;
			this.PageSizeInBlocksLog2 = (byte) pageSizeInBlocksLog2;
		}

		/// <summary>Allocation-block size, as log2 (the file-header field)</summary>
		public byte BlockSizeLog2 { get; }

		/// <summary>Tree-page size in blocks, as log2 (the file-header field)</summary>
		public byte PageSizeInBlocksLog2 { get; }

		/// <summary>Allocation-block size, in bytes</summary>
		public int BlockSize => 1 << this.BlockSizeLog2;

		/// <summary>Tree-page size, in bytes</summary>
		public int PageSize => 1 << (this.BlockSizeLog2 + this.PageSizeInBlocksLog2);

		/// <summary>Number of allocation blocks per tree page</summary>
		public int BlocksPerPage => 1 << this.PageSizeInBlocksLog2;

		/// <summary>Values longer than this are stored in a contiguous extent instead of inline in the leaf (one quarter of a tree page)</summary>
		public int MaxInlineValueLength => this.PageSize >> 2;

		/// <summary>The default geometry (32 KiB uniform): the minimax choice of the two-platform FL-17 benchmark matrix - never worse than second on any file-backed thermal tier on either measured platform</summary>
		public static FdbLiteGeometry Default => new(15, 0);

		/// <summary>Split geometry (16 KiB blocks, 64 KiB tree pages): the scan-tuned option - class-leading engine scans and depth at scale, at a higher tiny-commit CPU and cold-read cost than <see cref="Default"/></summary>
		public static FdbLiteGeometry Hypothesis => new(14, 2);

		/// <summary>Uniform geometry: block and tree page are the same size</summary>
		public static FdbLiteGeometry Uniform(int sizeLog2) => new(sizeLog2, 0);

		/// <inheritdoc />
		public bool Equals(FdbLiteGeometry other) => this.BlockSizeLog2 == other.BlockSizeLog2 && this.PageSizeInBlocksLog2 == other.PageSizeInBlocksLog2;

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FdbLiteGeometry other && Equals(other);

		/// <inheritdoc />
		public override int GetHashCode() => (this.BlockSizeLog2 << 8) | this.PageSizeInBlocksLog2;

		public static bool operator ==(FdbLiteGeometry left, FdbLiteGeometry right) => left.Equals(right);

		public static bool operator !=(FdbLiteGeometry left, FdbLiteGeometry right) => !left.Equals(right);

		/// <inheritdoc />
		public override string ToString() => $"block={this.BlockSize}, page={this.PageSize}";

	}

}
