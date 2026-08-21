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

namespace FoundationDB.Storage
{

	/// <summary>The transaction's local write buffer: every mutation marked so far, plus its write-conflict extent.</summary>
	/// <remarks>
	/// <para>The analogue of the C++ client's <c>WriteMap</c>, shaped for its actual traffic: point mutations vastly outnumber range clears, and writes vastly outnumber mid-transaction reads of the write set. Point marks therefore APPEND (an ordered structure is not paid per op); the ordered, carved, cola-equivalent view every iterating consumer wants is built lazily and cached until the next mark.</para>
	/// <para>The carved view reproduces the interval-overwrite semantics the previous <c>ColaRangeDictionary.Mark</c> maintained eagerly: at any key, the surviving mutation is the one marked LAST; a later range truncates or swallows earlier entries, a later point carves a hole in an earlier range (the split pieces sharing the range's mutation instance, as cola's split did).</para>
	/// <para>Write conflicts ride the entries (every mutation is conflict-material today) plus a small list for the explicit conflict-only ranges of <c>AddConflictRange</c>, replacing the second per-op structure the old <c>ColaRangeSet</c> cost.</para>
	/// </remarks>
	internal sealed class FdbWriteMap
	{

		/// <summary>One mark: a point mutation ([k, successor(k)), the dominant case) or a range clear.</summary>
		/// <remarks>A class rather than a struct so the carved view and the storage share instances, and so the <see cref="Cursor"/> can hand out <see langword="null"/> past either end, which is what the merge iterator's state machine expects.</remarks>
		internal sealed class Entry
		{
			public Entry(Key begin, Key end, Mutation? value, int seq)
			{
				this.Begin = begin;
				this.End = end;
				this.Value = value;
				this.Seq = seq;
			}

			public readonly Key Begin;

			public readonly Key End;

			public readonly Mutation? Value;

			/// <summary>Global mark order, the tiebreaker of the carve: at any key the highest sequence wins.</summary>
			public readonly int Seq;

			public override string ToString() => $"[{this.Begin}, {this.End}) = {this.Value} (#{this.Seq})";
		}

		/// <summary>Point marks with a settled order: sorted by key, at most one entry per key (the latest).</summary>
		private List<Entry> Sorted { get; } = new();

		/// <summary>Point marks since the last settle, in arrival order (a point lookup scans this newest-first before searching <see cref="Sorted"/>).</summary>
		private List<Entry> Tail { get; } = new();

		/// <summary>Range marks, in arrival order (rare; the view builder segments them).</summary>
		private List<Entry> Ranges { get; } = new();

		/// <summary>Explicit conflict-only ranges (<c>AddConflictRange</c>), which carry no mutation.</summary>
		private List<(Key Begin, Key End)> ExtraConflicts { get; } = new();

		/// <summary>Tail size beyond which point lookups would degrade, forcing a settle.</summary>
		private const int MaxTail = 128;

		private int SeqCounter;

		/// <summary>The carved disjoint view, or null when a mark has invalidated it.</summary>
		private Entry[]? View;

		/// <summary>Marks (mutations and explicit conflict ranges alike) accepted so far.</summary>
		/// <remarks>The "does this transaction write anything" test commit uses; the read-merge paths use <see cref="HasMutations"/>, which ignores conflict-only ranges.</remarks>
		public int Count => this.Sorted.Count + this.Tail.Count + this.Ranges.Count + this.ExtraConflicts.Count;

		/// <summary>Whether any MUTATION has been marked (explicit conflict-only ranges do not affect what a read sees).</summary>
		public bool HasMutations => this.Sorted.Count + this.Tail.Count + this.Ranges.Count > 0;

		/// <summary>Marks a point mutation over <c>[begin, successor)</c>, superseding whatever was marked at that key before.</summary>
		/// <remarks>Pure append: a mark never pays for order. The tail is settled by the operations that need order (a point lookup over a large tail, or a view build), so a write-only transaction sorts exactly once, at commit.</remarks>
		public void MarkPoint(Key begin, Key end, Mutation value)
		{
			this.Tail.Add(new(begin, end, value, ++this.SeqCounter));
			this.View = null;
		}

		/// <summary>Marks a range mutation (a clear-range), superseding every earlier mark inside <c>[begin, end)</c>.</summary>
		public void MarkRange(Key begin, Key end, Mutation value)
		{
			this.Ranges.Add(new(begin, end, value, ++this.SeqCounter));
			this.View = null;
		}

		/// <summary>Records an explicit write-conflict range that carries no mutation.</summary>
		public void MarkConflictRange(Key begin, Key end)
		{
			this.ExtraConflicts.Add((begin, end));
		}

		/// <summary>Finds the mutation covering <paramref name="key"/> (its begin at or before the key, its END strictly after), or null.</summary>
		/// <remarks>Point-op fast path: does NOT build the carved view. The winner is the LATEST mark covering the key, which is exactly what the eager carve would have left there.</remarks>
		public Mutation? FindCovering(Key key)
		{
			Entry? best = null;

			if (this.Tail.Count > MaxTail)
			{ // a long unsorted tail would make every lookup a long scan: settle it once and search properly
				SettleTail();
			}

			// newest first, so the first exact hit is the freshest and nothing older can beat it
			var tail = this.Tail;
			for (int i = tail.Count - 1; i >= 0; i--)
			{
				var e = tail[i];
				if (e.Begin.Equals(key)) { best = e; break; }
			}

			if (best is null)
			{
				int at = SearchSorted(key);
				if (at >= 0)
				{
					best = this.Sorted[at];
				}
			}

			// ranges are rare: a linear scan beats maintaining a second ordered structure per op
			var ranges = this.Ranges;
			for (int i = ranges.Count - 1; i >= 0; i--)
			{
				var r = ranges[i];
				if ((best is null || r.Seq > best.Seq) && r.Begin <= key && r.End > key)
				{
					best = r;
				}
			}

			return best?.Value;
		}

		/// <summary>Point-read form: the effective mutation at <paramref name="key"/>, if any.</summary>
		public bool TryFindCovering(Key key, out Mutation mutation)
		{
			var m = FindCovering(key);
			mutation = m!;
			return m is not null;
		}

		/// <summary>Binary search of <see cref="Sorted"/> for an exact key; negative when absent.</summary>
		private int SearchSorted(Key key)
		{
			var sorted = this.Sorted;
			int lo = 0, hi = sorted.Count - 1;
			while (lo <= hi)
			{
				int mid = (lo + hi) >> 1;
				int cmp = sorted[mid].Begin.CompareTo(key);
				if (cmp == 0) return mid;
				if (cmp < 0) lo = mid + 1; else hi = mid - 1;
			}
			return -1;
		}

		/// <summary>Folds <see cref="Tail"/> into <see cref="Sorted"/>: sorts the TAIL alone, then merges the two sorted lists, keeping the latest entry per key.</summary>
		/// <remarks>Never re-sorts the settled part: settling is O(n + t log t), so repeated settles over a long transaction stay linear overall instead of quadratic.</remarks>
		private void SettleTail()
		{
			var tail = this.Tail;
			if (tail.Count == 0) return;
			// sequential ingest arrives already ordered (equal keys arrive in seq order, which is the
			// comparator's own tiebreak): detect it in one pass instead of paying the full sort for nothing
			bool ordered = true;
			for (int k = 1; k < tail.Count; k++)
			{
				if (tail[k - 1].Begin.CompareTo(tail[k].Begin) > 0) { ordered = false; break; }
			}
			if (!ordered)
			{
				tail.Sort(static (a, b) =>
				{
					int cmp = a.Begin.CompareTo(b.Begin);
					return cmp != 0 ? cmp : a.Seq.CompareTo(b.Seq);
				});
			}

			var sorted = this.Sorted;
			var merged = new List<Entry>(sorted.Count + tail.Count);
			int i = 0, j = 0;
			while (i < sorted.Count || j < tail.Count)
			{
				// within the tail, skip an entry a newer one for the same key follows (they are adjacent after the sort)
				if (j + 1 < tail.Count && tail[j + 1].Begin.Equals(tail[j].Begin))
				{
					j++;
					continue;
				}
				if (j >= tail.Count)
				{
					merged.Add(sorted[i++]);
				}
				else if (i >= sorted.Count)
				{
					merged.Add(tail[j++]);
				}
				else
				{
					int cmp = sorted[i].Begin.CompareTo(tail[j].Begin);
					if (cmp < 0) { merged.Add(sorted[i++]); }
					else if (cmp > 0) { merged.Add(tail[j++]); }
					else { merged.Add(tail[j++]); i++; } // same key: the tail entry is always the newer mark
				}
			}
			sorted.Clear();
			sorted.AddRange(merged);
			tail.Clear();
		}

		/// <summary>The carved, disjoint, begin-ordered view of every mutation: what the eager cola Mark used to maintain per op.</summary>
		public Entry[] GetView()
		{
			return this.View ??= BuildView();
		}

		private Entry[] BuildView()
		{
			SettleTail();
			var points = this.Sorted;

			// 1. segment the ranges by mark order: process oldest to newest, each new range overwriting
			//    the overlapped parts of older segments (the pieces keep their source's mutation and seq,
			//    exactly like cola's split shared the entry's value)
			List<Entry>? segs = null;
			foreach (var r in this.Ranges)
			{
				segs ??= new();
				int i = 0;
				while (i < segs.Count)
				{
					var s = segs[i];
					if (s.End <= r.Begin) { i++; continue; }
					if (s.Begin >= r.End) { break; }
					// s overlaps r: r is newer, so s loses the overlap
					segs.RemoveAt(i);
					if (s.Begin < r.Begin)
					{
						segs.Insert(i, new(s.Begin, r.Begin, s.Value, s.Seq));
						i++;
					}
					if (s.End > r.End)
					{
						segs.Insert(i, new(r.End, s.End, s.Value, s.Seq));
						break;
					}
				}
				// insert r at its ordered position
				int at = 0;
				while (at < segs.Count && segs[at].Begin < r.Begin) at++;
				segs.Insert(at, r);
			}

			if (segs is null || segs.Count == 0)
			{
				return points.ToArray();
			}

			// 2. sweep-merge the points into the segments: a point NEWER than its covering segment carves a
			//    hole and survives; an OLDER one was overwritten by the range and is dropped
			var view = new List<Entry>(points.Count + (segs.Count * 2));
			int p = 0, g = 0;
			Entry? seg = segs.Count > 0 ? segs[0] : null;
			while (p < points.Count || seg is not null)
			{
				if (seg is null || (p < points.Count && points[p].Begin < seg.Begin))
				{
					view.Add(points[p++]);
					continue;
				}
				if (p >= points.Count || points[p].Begin >= seg.End)
				{ // no (more) points inside this segment: emit what is left of it
					if (seg.Begin < seg.End) view.Add(seg);
					seg = ++g < segs.Count ? segs[g] : null;
					continue;
				}
				var pt = points[p];
				// pt.Begin falls inside [seg.Begin, seg.End)
				if (pt.Seq < seg.Seq)
				{ // the range was marked after this point: the point is dead
					p++;
					continue;
				}
				if (seg.Begin < pt.Begin)
				{
					view.Add(new(seg.Begin, pt.Begin, seg.Value, seg.Seq));
				}
				view.Add(pt);
				p++;
				var resume = pt.End > seg.Begin ? pt.End : seg.Begin;
				seg = resume < seg.End
					? new(resume, seg.End, seg.Value, seg.Seq)
					: (++g < segs.Count ? segs[g] : null);
			}
			return view.ToArray();
		}

		/// <summary>Every write-conflict range of this transaction (mutations plus explicit ranges), begin-ordered with touching ranges coalesced.</summary>
		public List<(Key Begin, Key End)> GetConflictRanges()
		{
			var all = new List<(Key Begin, Key End)>();
			foreach (var e in GetView())
			{
				all.Add((e.Begin, e.End));
			}
			all.AddRange(this.ExtraConflicts);
			all.Sort(static (a, b) => a.Begin.CompareTo(b.Begin));
			// coalesce touching/overlapping ranges so the shared conflict map is not fragmented by adjacent point marks
			var res = new List<(Key Begin, Key End)>(all.Count);
			foreach (var r in all)
			{
				if (res.Count > 0 && r.Begin <= res[^1].End)
				{
					if (r.End > res[^1].End) res[^1] = (res[^1].Begin, r.End);
					continue;
				}
				res.Add(r);
			}
			return res;
		}

		/// <summary>Ordered cursor over the carved view, with the floor-seek shape the merge iterator wants.</summary>
		public Cursor GetCursor() => new(GetView());

		/// <summary>Position-sharing cursor over the carved view (a class, like the cola iterator it replaces: the merge iterator copies it around and expects one shared position).</summary>
		internal sealed class Cursor
		{
			private readonly Entry[] Entries;

			private int Index = -1;

			public Cursor(Entry[] entries)
			{
				this.Entries = entries;
			}

			public Entry? Current => (uint) this.Index < (uint) this.Entries.Length ? this.Entries[this.Index] : null;

			/// <summary>Positions at the LAST entry whose begin is at-or-before (<paramref name="orEqual"/>) or strictly before the key; false when no entry qualifies.</summary>
			public bool Seek(Key key, bool orEqual)
			{
				var entries = this.Entries;
				int lo = 0, hi = entries.Length - 1, best = -1;
				while (lo <= hi)
				{
					int mid = (lo + hi) >> 1;
					int cmp = entries[mid].Begin.CompareTo(key);
					if (cmp < 0 || (cmp == 0 && orEqual))
					{
						best = mid;
						lo = mid + 1;
					}
					else
					{
						hi = mid - 1;
					}
				}
				this.Index = best;
				return best >= 0;
			}

			public bool SeekFirst()
			{
				this.Index = 0;
				return this.Entries.Length > 0;
			}

			public bool Next()
			{
				if (this.Index >= this.Entries.Length - 1)
				{
					this.Index = this.Entries.Length;
					return false;
				}
				this.Index++;
				return true;
			}

			public bool Previous()
			{
				if (this.Index <= 0)
				{
					this.Index = -1;
					return false;
				}
				this.Index--;
				return true;
			}
		}

	}

}
