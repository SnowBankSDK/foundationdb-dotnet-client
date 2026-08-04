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

	/// <summary>Allocates block runs for tree pages and value extents: free ranges first, then file-tail growth.</summary>
	/// <remarks>Single-writer machinery (used under the engine's commit lock). Tree pages are allocated page-aligned; extents are block-granular; neither ever straddles a mapping-region boundary (that constraint is what keeps every page and extent a single contiguous span).</remarks>
	public sealed class FdbLiteBlockAllocator
	{

		public FdbLiteBlockAllocator(IFdbLitePager pager, FdbLiteFreeSpaceMap freeSpace, uint frontier)
		{
			Contract.NotNull(pager);
			Contract.NotNull(freeSpace);
			this.Pager = pager;
			this.FreeSpace = freeSpace;
			this.Frontier = frontier;
		}

		private IFdbLitePager Pager { get; }

		/// <summary>Free ranges and the delayed-free machinery</summary>
		public FdbLiteFreeSpaceMap FreeSpace { get; }

		/// <summary>First never-allocated block (the allocation high-water mark; at most <see cref="IFdbLitePager.BlockCount"/>)</summary>
		public uint Frontier { get; private set; }

		/// <summary>Allocates one tree page (page-aligned, page-sized run).</summary>
		public uint AllocatePage(bool fromHighEnd = false)
		{
			var geometry = this.Pager.Geometry;
			return AllocateRun((uint) geometry.BlocksPerPage, (uint) geometry.BlocksPerPage, fromHighEnd);
		}

		/// <summary>Allocates a contiguous extent of <paramref name="blockCount"/> blocks (block-granular).</summary>
		public uint AllocateExtent(uint blockCount) => AllocateRun(blockCount, 1);

		/// <summary>Returns a run to the free machinery, reusable once no retained root or pin can reference it.</summary>
		public void Free(uint start, uint count, ulong freedAtGeneration) => this.FreeSpace.Free(start, count, freedAtGeneration);

		private uint AllocateRun(uint count, uint alignment, bool fromHighEnd = false)
		{
			uint regionBlocks = this.Pager.RegionSizeInBlocks;
			Contract.Requires(count > 0 && count <= regionBlocks);

			if (this.FreeSpace.TryAllocate(count, alignment, regionBlocks, out uint start, fromHighEnd))
			{
				return start;
			}

			// tail allocation: align the frontier, then make sure the run does not straddle a region boundary
			start = checked(this.Frontier + alignment - 1) & ~(alignment - 1);
			if ((start + count - 1) / regionBlocks != start / regionBlocks)
			{ // would straddle: skip to the next region boundary (which satisfies any legal alignment)
				start = (start / regionBlocks + 1) * regionBlocks;
			}
			if (start > this.Frontier)
			{ // the skipped gap never held data and is immediately reusable
				this.FreeSpace.FreeImmediately(this.Frontier, start - this.Frontier);
			}

			uint end = start + count;
			if (end > this.Pager.BlockCount)
			{
				this.Pager.Grow(ComputeGrowthTarget(end));
			}
			this.Frontier = end;
			return start;
		}

		/// <summary>Legacy-prototype growth policy: x4 below 64 MiB, x2 below 1 GiB, +1 GiB below 10 GiB, else +4 GiB (always at least the requested minimum).</summary>
		private uint ComputeGrowthTarget(uint minimumBlocks)
		{
			int blockSizeLog2 = this.Pager.Geometry.BlockSizeLog2;
			long currentBytes = (long) this.Pager.BlockCount << blockSizeLog2;
			long targetBytes = currentBytes switch
			{
				< 64L << 20 => currentBytes << 2,
				< 1L << 30 => currentBytes << 1,
				< 10L << 30 => currentBytes + (1L << 30),
				_ => currentBytes + (4L << 30),
			};
			long minimumBytes = (long) minimumBlocks << blockSizeLog2;
			if (targetBytes < minimumBytes)
			{
				targetBytes = minimumBytes;
			}
			return (uint) Math.Min(uint.MaxValue, targetBytes >> blockSizeLog2);
		}

	}

}
