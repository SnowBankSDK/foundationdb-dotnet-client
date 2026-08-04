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

	/// <summary>Tracks freed block ranges and when they become safe to reuse (delayed free).</summary>
	/// <remarks>
	/// <para>A range freed while building generation F was last referenced by the tree of generation F-1: it becomes reusable only when no retained root and no pin can still see that tree. The engine computes the resulting promotion limit (<c>min(durableGeneration - 1, oldest pin)</c>) and calls <see cref="Promote"/>; this class only orders ranges by generation.</para>
	/// <para>Single-writer machinery (used under the engine's commit lock): no synchronization inside.</para>
	/// </remarks>
	public sealed class FdbLiteFreeSpaceMap
	{

		// ponytail: sorted List with O(n) inserts for the reusable set; an interval tree replaces it if allocation ever measures hot

		/// <summary>Reusable ranges, sorted by start block, adjacent ranges coalesced</summary>
		private List<(uint Start, uint Count)> Reusable { get; } = [ ];

		/// <summary>Ranges awaiting their promotion generation, in freed order (generations are monotonic)</summary>
		private Queue<(ulong Generation, uint Start, uint Count)> Pending { get; } = new();

		/// <summary>Number of reusable ranges (not blocks)</summary>
		public int ReusableRangeCount => this.Reusable.Count;

		/// <summary>Number of ranges still waiting for promotion</summary>
		public int PendingRangeCount => this.Pending.Count;

		/// <summary>Total number of ranges tracked (the free-list serialization size driver)</summary>
		public int TotalRangeCount => this.Reusable.Count + this.Pending.Count;

		/// <summary>Total reusable blocks (allocation capacity before tail growth)</summary>
		public long ReusableBlockCount
		{
			get
			{
				long sum = 0;
				foreach (var r in this.Reusable) { sum += r.Count; }
				return sum;
			}
		}

		/// <summary>Total blocks retained only because their generation has not been promoted yet (the FL-21 slow-reader observability number)</summary>
		public long PendingBlockCount
		{
			get
			{
				long sum = 0;
				foreach (var p in this.Pending) { sum += p.Count; }
				return sum;
			}
		}

		/// <summary>Records a range freed while building <paramref name="generation"/> (not reusable yet).</summary>
		public void Free(uint start, uint count, ulong generation)
		{
			Contract.Requires(count > 0);
			Contract.Debug.Requires(this.Pending.Count == 0 || this.Pending.Last().Generation <= generation, "generations are monotonic");
			this.Pending.Enqueue((generation, start, count));
		}

		/// <summary>Records a range that is immediately reusable (never referenced by any retained tree: allocation waste, startup-sweep finds).</summary>
		public void FreeImmediately(uint start, uint count)
		{
			Contract.Requires(count > 0);
			InsertReusable(start, count);
		}

		/// <summary>Moves every pending range freed at or before <paramref name="reusableUpToInclusive"/> into the reusable set.</summary>
		public void Promote(ulong reusableUpToInclusive)
		{
			while (this.Pending.TryPeek(out var head) && head.Generation <= reusableUpToInclusive)
			{
				this.Pending.Dequeue();
				InsertReusable(head.Start, head.Count);
			}
		}

		/// <summary>Takes the first reusable run of <paramref name="count"/> blocks whose start is a multiple of <paramref name="alignment"/> and which does not straddle a <paramref name="regionSizeInBlocks"/> boundary.</summary>
		public bool TryAllocate(uint count, uint alignment, uint regionSizeInBlocks, out uint start, bool fromHighEnd = false)
		{
			Contract.Requires(count > 0 && BitOperations.IsPow2(alignment) && BitOperations.IsPow2(regionSizeInBlocks) && count <= regionSizeInBlocks);

			if (fromHighEnd)
			{
				// Leaf pages allocate from the HIGH end so that, over the churn of copy-on-write, they cluster near
				// the end of the file and leave the low reusable runs for internal pages (which allocate low). Scan
				// ranges high to low, and within a range take the highest aligned run that fits without straddling a
				// region boundary.
				for (int i = this.Reusable.Count - 1; i >= 0; i--)
				{
					var (rangeStart, rangeCount) = this.Reusable[i];
					if (rangeCount < count) { continue; }

					// highest aligned start whose run ends at or before the range end
					uint candidate = (rangeStart + rangeCount - count) & ~(alignment - 1);
					while (candidate >= rangeStart)
					{
						uint region = candidate / regionSizeInBlocks;
						if ((candidate + count - 1) / regionSizeInBlocks != region)
						{ // would straddle: the highest position that still fits ends at this region's last block
							candidate = ((region + 1) * regionSizeInBlocks - count) & ~(alignment - 1);
							continue;
						}

						RemoveFromRange(i, rangeStart, rangeCount, candidate, count);
						start = candidate;
						return true;
					}
				}

				start = 0;
				return false;
			}

			for (int i = 0; i < this.Reusable.Count; i++)
			{
				var (rangeStart, rangeCount) = this.Reusable[i];

				// candidate start: aligned up within the range
				uint candidate = (rangeStart + alignment - 1) & ~(alignment - 1);
				while (candidate + count <= rangeStart + rangeCount)
				{
					uint region = candidate / regionSizeInBlocks;
					if ((candidate + count - 1) / regionSizeInBlocks != region)
					{ // would straddle: the only aligned position that can still work in this range starts at the next region boundary
						candidate = (region + 1) * regionSizeInBlocks;
						continue;
					}

					// take [candidate, candidate + count) out of the range
					RemoveFromRange(i, rangeStart, rangeCount, candidate, count);
					start = candidate;
					return true;
				}
			}

			start = 0;
			return false;
		}

		/// <summary>Enumerates every tracked range, reusable first (generation 0), then pending in freed order.</summary>
		public IEnumerable<(ulong Generation, uint Start, uint Count)> Enumerate()
		{
			foreach (var r in this.Reusable)
			{
				yield return (0, r.Start, r.Count);
			}
			foreach (var p in this.Pending)
			{
				yield return p;
			}
		}

		private void RemoveFromRange(int index, uint rangeStart, uint rangeCount, uint takenStart, uint takenCount)
		{
			uint before = takenStart - rangeStart;
			uint after = (rangeStart + rangeCount) - (takenStart + takenCount);
			if (before == 0 && after == 0)
			{
				this.Reusable.RemoveAt(index);
			}
			else if (before == 0)
			{
				this.Reusable[index] = (takenStart + takenCount, after);
			}
			else if (after == 0)
			{
				this.Reusable[index] = (rangeStart, before);
			}
			else
			{ // the take splits the range in two
				this.Reusable[index] = (rangeStart, before);
				this.Reusable.Insert(index + 1, (takenStart + takenCount, after));
			}
		}

		private void InsertReusable(uint start, uint count)
		{
			// binary search for the insertion point by start block
			int lo = 0, hi = this.Reusable.Count;
			while (lo < hi)
			{
				int mid = (lo + hi) >> 1;
				if (this.Reusable[mid].Start < start) { lo = mid + 1; } else { hi = mid; }
			}

			// always-on, deliberately: a double free is the free-map signature of the worst corruption class
			// this engine can have (one block handed to two owners), and Release-only silence here converts
			// "loud failure at the offending commit" into "cross-page corruption discovered days later"
			Contract.Requires(lo == this.Reusable.Count || start + count <= this.Reusable[lo].Start, "freed range overlaps a free range (double free)");
			Contract.Requires(lo == 0 || this.Reusable[lo - 1].Start + this.Reusable[lo - 1].Count <= start, "freed range overlaps a free range (double free)");

			// coalesce with the previous and/or next range when adjacent
			bool mergePrev = lo > 0 && this.Reusable[lo - 1].Start + this.Reusable[lo - 1].Count == start;
			bool mergeNext = lo < this.Reusable.Count && start + count == this.Reusable[lo].Start;

			if (mergePrev && mergeNext)
			{
				this.Reusable[lo - 1] = (this.Reusable[lo - 1].Start, this.Reusable[lo - 1].Count + count + this.Reusable[lo].Count);
				this.Reusable.RemoveAt(lo);
			}
			else if (mergePrev)
			{
				this.Reusable[lo - 1] = (this.Reusable[lo - 1].Start, this.Reusable[lo - 1].Count + count);
			}
			else if (mergeNext)
			{
				this.Reusable[lo] = (start, count + this.Reusable[lo].Count);
			}
			else
			{
				this.Reusable.Insert(lo, (start, count));
			}
		}

	}

}
