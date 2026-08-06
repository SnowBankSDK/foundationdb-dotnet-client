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

	/// <summary>Point reads over a committed tree generation.</summary>
	/// <remarks>
	/// <para>Returned spans point into pager memory: valid while the generation stays pinned, engine-internal per the memory-safety contract (what leaves the engine is a copy or a delegate-scoped span).</para>
	/// <para>Read-path verification is FIRST TOUCH PER OPEN: the first time any read enters a page (or resolves an extent) since the pager opened, its checksum is verified and corruption throws with the block named - gated by <see cref="IFdbLitePager.MarkTouched"/>, so the warm path pays one bitmap test. Rot that develops after a block's first touch is the offline audit's to find.</para>
	/// </remarks>
	public static class FdbLiteTreeReader
	{

		/// <summary>Reads the value of a key at the given root (0 = empty tree).</summary>
		public static bool TryGetValue(IFdbLitePager pager, uint root, ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
		{
			Contract.NotNull(pager);
			value = default;
			if (root == 0)
			{
				return false;
			}
			uint pageId = root;
			while (true)
			{
				var page = ReadPageVerified(pager, pageId);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					int slot = FdbLiteTreePage.FindLeafSlot(page, key, out bool exact);
					if (!exact)
					{
						return false;
					}
					value = ResolveLeafValue(pager, page, slot);
					return true;
				}
				pageId = FdbLiteTreePage.GetChild(page, FdbLiteTreePage.FindChildIndex(page, key));
			}
		}

		/// <summary>Reads a tree page, verifying its checksum on the first touch since the pager opened.</summary>
		internal static ReadOnlySpan<byte> ReadPageVerified(IFdbLitePager pager, uint pageId)
		{
			var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage);
			if (pager.MarkTouched(pageId) && !FdbLitePageHeader.Verify(page, pageId))
			{
				throw new InvalidDataException($"Tree page {pageId} fails its checksum (on-disk corruption)");
			}
			return page;
		}

		/// <summary>Resolves a leaf cell's value: the inline bytes, or the single contiguous span of its extent.</summary>
		internal static ReadOnlySpan<byte> ResolveLeafValue(IFdbLitePager pager, ReadOnlySpan<byte> leaf, int slot)
		{
			// ONE pass: asking for the flag and then the value walked the cell's whole entry chain twice, per row
			var (offset, length, flags) = FdbLiteTreePage.LeafValueAndFlags(leaf, slot);
			if ((flags & FdbLiteTreePage.FlagValueIsExtent) == 0)
			{
				return leaf.Slice(offset, length);
			}
			var (start, blockCount, totalLength, checksum) = FdbLiteTreePage.GetLeafExtentDescriptor(leaf, slot);
			var payload = pager.ReadBlocks(start, blockCount)[..(int) totalLength];
			if (pager.MarkTouched(start) && System.IO.Hashing.XxHash3.HashToUInt64(payload, unchecked((long) start)) != checksum)
			{
				throw new InvalidDataException($"Extent at block {start} ({totalLength} bytes) fails its checksum (on-disk corruption)");
			}
			return payload;
		}

	}

	/// <summary>Ordered bidirectional cursor over a committed tree generation.</summary>
	/// <remarks>
	/// <para>The tree has no leaf sibling links (COW would cascade through them), so the cursor carries the root-to-leaf path and steps through parents. Exposed key/value spans point into pager memory and are valid while the generation stays pinned.</para>
	/// <para>Seek semantics match the committed-store seam: <see cref="SeekFloor"/> positions on the largest key below (or at) the pivot, <see cref="SeekCeiling"/> on the smallest at/above it.</para>
	/// </remarks>
	public struct FdbLiteTreeCursor
	{

		private const int MaxDepth = 20;

		public FdbLiteTreeCursor(IFdbLitePager pager, uint root)
		{
			Contract.NotNull(pager);
			this.Pager = pager;
			this.Root = root;
		}

		private IFdbLitePager Pager { get; }

		private uint Root { get; }

		[InlineArray(MaxDepth)]
		private struct PagePathBuffer
		{
			private uint Element0;
		}

		[InlineArray(MaxDepth)]
		private struct ChildPathBuffer
		{
			private int Element0;
		}

		/// <summary>Internal pages of the current path (0..Depth-1). Inline: a cursor per scan operation is the ordinary usage pattern, and the two path arrays were its whole allocation cost (measured as the dominant allocator of the range/read benchmark legs once page buffers pooled).</summary>
		private PagePathBuffer PagePath;

		/// <summary>Child index taken in each internal page of the path</summary>
		private ChildPathBuffer ChildPath;

		private int Depth;

		private uint LeafPage;

		private int LeafSlot;

		/// <summary>The cursor points at a key/value pair</summary>
		public bool IsValid { get; private set; }

		/// <summary>Scratch that whole keys are assembled into when the page they live on strips a prefix; grown on demand and reused, so this is O(1) allocations for the cursor's lifetime rather than one per key.</summary>
		private byte[]? KeyScratch;

		/// <summary>Key at the current position.</summary>
		/// <remarks>
		/// <para>Points straight at pager memory (valid while the generation is pinned) for a page that strips no prefix, which is the common case and costs nothing.</para>
		/// <para>When the page DOES strip a prefix there is no contiguous whole key anywhere in it, so one is assembled into cursor-owned scratch. That is a copy per key, and it is the reason searching should compare against the page prefix and the suffix separately rather than asking for this: a probe never needs the assembled key, only a caller that genuinely wants the whole bytes does.</para>
		/// </remarks>
		public ReadOnlySpan<byte> CurrentKey
		{
			get
			{
				var leaf = ReadLeaf();
				var suffix = FdbLiteTreePage.GetLeafKey(leaf, this.LeafSlot);
				int prefixLen = FdbLitePageHeader.GetPrefixLength(leaf);
				if (prefixLen == 0)
				{
					return suffix;
				}

				int total = prefixLen + suffix.Length;
				if (this.KeyScratch is null || this.KeyScratch.Length < total)
				{
					this.KeyScratch = new byte[Math.Max(total, 128)];
				}
				var scratch = this.KeyScratch.AsSpan(0, total);
				FdbLiteTreePage.GetPagePrefix(leaf, isInternal: false).CopyTo(scratch);
				suffix.CopyTo(scratch[prefixLen..]);
				return scratch;
			}
		}

		/// <summary>Value at the current position (pager memory, valid while the generation is pinned; extent values are one contiguous span)</summary>
		public ReadOnlySpan<byte> CurrentValue => FdbLiteTreeReader.ResolveLeafValue(this.Pager, ReadLeaf(), this.LeafSlot);

		private ReadOnlySpan<byte> ReadLeaf()
		{
			Contract.Debug.Requires(this.IsValid);
			return this.Pager.ReadBlocks(this.LeafPage, this.Pager.Geometry.BlocksPerPage);
		}

		/// <summary>Page whose cell count <see cref="CachedCellCount"/> holds (0 = nothing cached).</summary>
		/// <remarks>Fields, not auto-properties: this pair is read on every single row of a scan, which is where the repo's property convention stops paying for itself.</remarks>
		private uint CachedCountPage;

		private int CachedCellCount;

		/// <summary>Cell count of the current leaf, resolved once per LEAF rather than once per row.</summary>
		/// <remarks>
		/// <para>The advance step used to re-resolve the whole page through the pager just to read this number, and a pager read is not free: a disposed check, three always-on preconditions (two of them integer divisions) and a region lookup, paid per row. The legacy prototype keeps a page POINTER on its cursor and reads the count off it, which is why its per-row advance is so much cheaper.</para>
		/// <para>Self-invalidating on the page id, so no seek path has to remember to clear it. Sound because a cursor runs over a COMMITTED generation: the pages under it are immutable for as long as the pin is held.</para>
		/// </remarks>
		private int LeafCellCount()
		{
			if (this.CachedCountPage != this.LeafPage)
			{
				this.CachedCellCount = FdbLitePageHeader.GetCellCount(ReadLeaf());
				this.CachedCountPage = this.LeafPage;
			}
			return this.CachedCellCount;
		}

		/// <summary>Reads a page the cursor is ENTERING (seek and sibling steps): first-touch verified. <see cref="ReadLeaf"/> re-reads the already-entered leaf and stays raw, so the per-row paths pay nothing.</summary>
		private ReadOnlySpan<byte> ReadPage(uint pageId) => FdbLiteTreeReader.ReadPageVerified(this.Pager, pageId);

		/// <summary>Positions on the smallest key of the tree.</summary>
		public bool SeekFirst() => SeekEdge(first: true);

		/// <summary>Positions on the largest key of the tree.</summary>
		public bool SeekLast() => SeekEdge(first: false);

		private bool SeekEdge(bool first)
		{
			this.IsValid = false;
			if (this.Root == 0)
			{
				return false;
			}
			uint pageId = this.Root;
			this.Depth = 0;
			while (true)
			{
				var page = ReadPage(pageId);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					int count = FdbLitePageHeader.GetCellCount(page);
					if (count == 0)
					{ // only the never-written empty root can be empty, and root == 0 covered that
						return false;
					}
					this.LeafPage = pageId;
					this.LeafSlot = first ? 0 : count - 1;
					this.IsValid = true;
					return true;
				}
				int childIndex = first ? 0 : FdbLiteTreePage.GetChildCount(page) - 1;
				Push(pageId, childIndex);
				pageId = FdbLiteTreePage.GetChild(page, childIndex);
			}
		}

		/// <summary>Positions on the largest key strictly below <paramref name="key"/> (or equal to it when <paramref name="orEqual"/>).</summary>
		public bool SeekFloor(ReadOnlySpan<byte> key, bool orEqual)
		{
			if (!DescendTo(key, out var leaf))
			{
				return false;
			}
			int slot = FdbLiteTreePage.FindLeafSlot(leaf, key, out bool exact);
			if (exact && orEqual)
			{
				this.LeafSlot = slot;
				this.IsValid = true;
				return true;
			}
			if (slot > 0)
			{
				this.LeafSlot = slot - 1;
				this.IsValid = true;
				return true;
			}
			// every key of this leaf is at/above the pivot: the floor lives in the predecessor leaf
			return StepToSiblingLeaf(forward: false);
		}

		/// <summary>Positions on the smallest key at or above <paramref name="key"/>.</summary>
		public bool SeekCeiling(ReadOnlySpan<byte> key)
		{
			if (!DescendTo(key, out var leaf))
			{
				return false;
			}
			int slot = FdbLiteTreePage.FindLeafSlot(leaf, key, out _);
			if (slot < FdbLitePageHeader.GetCellCount(leaf))
			{
				this.LeafSlot = slot;
				this.IsValid = true;
				return true;
			}
			// every key of this leaf is below the pivot: the ceiling lives in the successor leaf
			return StepToSiblingLeaf(forward: true);
		}

		/// <summary>Moves to the next key in order.</summary>
		public bool MoveNext()
		{
			Contract.Debug.Requires(this.IsValid);
			if (this.LeafSlot + 1 < LeafCellCount())
			{
				this.LeafSlot++;
				return true;
			}
			bool moved = StepToSiblingLeaf(forward: true);
			this.IsValid = moved || this.IsValid; // a failed step keeps the cursor on the last key
			return moved;
		}

		/// <summary>Moves to the previous key in order.</summary>
		public bool MovePrevious()
		{
			Contract.Debug.Requires(this.IsValid);
			if (this.LeafSlot > 0)
			{
				this.LeafSlot--;
				return true;
			}
			bool moved = StepToSiblingLeaf(forward: false);
			this.IsValid = moved || this.IsValid;
			return moved;
		}

		/// <summary>Descends to the leaf covering <paramref name="key"/>, rebuilding the path.</summary>
		private bool DescendTo(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> leaf)
		{
			this.IsValid = false;
			leaf = default;
			if (this.Root == 0)
			{
				return false;
			}
			uint pageId = this.Root;
			this.Depth = 0;
			while (true)
			{
				var page = ReadPage(pageId);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					this.LeafPage = pageId;
					leaf = page;
					return true;
				}
				int childIndex = FdbLiteTreePage.FindChildIndex(page, key);
				Push(pageId, childIndex);
				pageId = FdbLiteTreePage.GetChild(page, childIndex);
			}
		}

		/// <summary>Walks to the first key of the next leaf (or the last key of the previous leaf), through the path stack.</summary>
		private bool StepToSiblingLeaf(bool forward)
		{
			// find the deepest ancestor that still has a sibling child in the walk direction
			int level = this.Depth - 1;
			while (level >= 0)
			{
				var page = ReadPage(this.PagePath[level]);
				int childIndex = this.ChildPath[level];
				int childCount = FdbLiteTreePage.GetChildCount(page);
				if (forward ? childIndex + 1 < childCount : childIndex > 0)
				{
					break;
				}
				level--;
			}
			if (level < 0)
			{
				return false;
			}

			// step sideways, then descend along the edge; empty (leftmost-only) internal pages descend through
			this.ChildPath[level] += forward ? +1 : -1;
			this.Depth = level + 1;
			uint pageId = FdbLiteTreePage.GetChild(ReadPage(this.PagePath[level]), this.ChildPath[level]);
			while (true)
			{
				var page = ReadPage(pageId);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					int count = FdbLitePageHeader.GetCellCount(page);
					Contract.Debug.Assert(count > 0, "non-root leaves always hold at least one cell");
					this.LeafPage = pageId;
					this.LeafSlot = forward ? 0 : count - 1;
					this.IsValid = true;
					return true;
				}
				int childIndex = forward ? 0 : FdbLiteTreePage.GetChildCount(page) - 1;
				Push(pageId, childIndex);
				pageId = FdbLiteTreePage.GetChild(page, childIndex);
			}
		}

		private void Push(uint pageId, int childIndex)
		{
			Contract.Debug.Assert(this.Depth < MaxDepth);
			this.PagePath[this.Depth] = pageId;
			this.ChildPath[this.Depth] = childIndex;
			this.Depth++;
		}

	}

}
