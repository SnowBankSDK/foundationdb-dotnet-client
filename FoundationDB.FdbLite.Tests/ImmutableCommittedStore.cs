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

namespace FoundationDB.FdbLite.Tests
{
	using FoundationDB.Storage;
	using System;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Linq;
	using FoundationDB.Client;

	/// <summary>Deliberately-dumb always-run reference backend for the committed-store seam: a persistent sorted map.</summary>
	/// <remarks>
	/// <para>The point of this backend is that there is almost nothing to it - a <see cref="ImmutableSortedDictionary{TKey,TValue}"/> with a
	/// binary-search cursor, no allocator, no free-list, no COW - so it has the smallest surface for a bug to hide in, which is what makes
	/// it worth running as the third, independent binding. "Simple" is not "assumed correct": it earns its trust by being cross-checked
	/// against BOTH the ColaStore reference and the FdbLite engine in the storage differential every run. Its structural sharing gives free
	/// snapshots: <see cref="Copy"/> just shares the current immutable root, which also makes it the "retain every generation in memory"
	/// backend for inspection.</para>
	/// <para>It binds the span-first seam the same way the ColaStore reference does: the stored <see cref="Key"/>/<see cref="Value"/>
	/// are stable for the snapshot's lifetime, so <c>CopyKey</c>/<c>CopyValue</c> return the view (no bytes moved) and <c>CurrentKey</c>/
	/// <c>CurrentValue</c> expose its span. That a third representation drops into the contract unchanged is the evidence that the
	/// contract stayed representation-agnostic.</para>
	/// </remarks>
	public sealed class ImmutableCommittedStore : IFdbCommittedStore<ImmutableCommittedCursor>
	{

		private ImmutableSortedDictionary<Key, Value> Inner { get; set; }

		public ImmutableCommittedStore()
			: this(ImmutableSortedDictionary.Create<Key, Value>(Key.Comparer.Default))
		{ }

		private ImmutableCommittedStore(ImmutableSortedDictionary<Key, Value> inner) => this.Inner = inner;

		/// <inheritdoc />
		public int Count => this.Inner.Count;

		/// <inheritdoc />
		public bool TryGetValue(Key key, out Value value) => this.Inner.TryGetValue(key, out value);

		/// <inheritdoc />
		public bool ContainsKey(Key key) => this.Inner.ContainsKey(key);

		/// <inheritdoc />
		public TResult Read<TState, TResult>(Key key, TState state, FdbValueDecoder<TState, TResult> decoder)
			=> this.Inner.TryGetValue(key, out var value)
				? decoder(state, value.Span, true)
				: decoder(state, default, false);

		/// <inheritdoc />
		public ImmutableCommittedCursor GetCursor() => new(this.Inner);

		/// <inheritdoc />
		IFdbCommittedCursor IFdbCommittedStore.GetCursor() => GetCursor();

		/// <inheritdoc />
		public IEnumerable<KeyValuePair<Key, Value>> Scan(Key begin, Key end, bool reversed)
		{
			var range = this.Inner.Where(kv => kv.Key.CompareTo(begin) >= 0 && kv.Key.CompareTo(end) < 0);
			return reversed ? range.Reverse() : range;
		}

		/// <inheritdoc />
		public IEnumerable<KeyValuePair<Key, Value>> IterateOrdered() => this.Inner;

		/// <inheritdoc />
		public void VisitRange<TState>(Key begin, Key end, bool reversed, TState state, FdbCommittedRangeVisitor<TState> visitor)
		{
			foreach (var kv in Scan(begin, end, reversed))
			{
				if (!visitor(state, kv.Key.Span, kv.Value.Span)) break;
			}
		}

		/// <inheritdoc />
		public IFdbCommittedStore Copy() => new ImmutableCommittedStore(this.Inner); // structural sharing = a free snapshot

		/// <inheritdoc />
		public bool Remove(Key key)
		{
			var next = this.Inner.Remove(key);
			if (next == this.Inner) return false;
			this.Inner = next;
			return true;
		}

		/// <inheritdoc />
		public bool TryGetKeyValue(Key key, out KeyValuePair<Key, Value> entry)
		{
			// the stored key instance can differ from the lookup instance (the versionstamp path relies on the stored one)
			foreach (var kv in this.Inner)
			{
				if (kv.Key.Equals(key)) { entry = kv; return true; }
			}
			entry = default;
			return false;
		}

		/// <inheritdoc />
		public int RemoveRange(Key begin, Key end)
		{
			var victims = this.Inner.Keys.Where(k => k.CompareTo(begin) >= 0 && k.CompareTo(end) < 0).ToArray();
			this.Inner = this.Inner.RemoveRange(victims);
			return victims.Length;
		}

		/// <inheritdoc />
		public Value this[Key key] { set => this.Inner = this.Inner.SetItem(key, value); }

	}

	/// <summary>Bidirectional cursor over the immutable reference backend: an index into the ordered snapshot.</summary>
	/// <remarks>The ordered entries are materialized once from the immutable map (stable for the snapshot); position is a plain index, so seek is a binary search and next/previous are index steps - the dumbest thing that is obviously correct.</remarks>
	public struct ImmutableCommittedCursor : IFdbCommittedCursor
	{

		private readonly ImmutableArray<KeyValuePair<Key, Value>> Items;

		/// <summary>-1 = before the first key (or exhausted below); Items.Length = never (moves clamp)</summary>
		private int Index;

		public ImmutableCommittedCursor(ImmutableSortedDictionary<Key, Value> inner)
		{
			this.Items = inner.ToImmutableArray(); // already in key order
			this.Index = -1;
		}

		private bool Positioned => (uint) this.Index < (uint) this.Items.Length;

		/// <inheritdoc />
		public ReadOnlySpan<byte> CurrentKey => this.Positioned ? this.Items[this.Index].Key.Span : default;

		/// <inheritdoc />
		public ReadOnlySpan<byte> CurrentValue => this.Positioned ? this.Items[this.Index].Value.Span : default;

		/// <inheritdoc />
		public Key CopyKey() => this.Positioned ? this.Items[this.Index].Key : Key.Nil;

		/// <inheritdoc />
		public Value CopyValue() => this.Positioned ? this.Items[this.Index].Value : Value.Nil;

		/// <inheritdoc />
		public KeyValuePair<Key, Value> CopyCurrent() => this.Positioned ? this.Items[this.Index] : default;

		/// <inheritdoc />
		public bool Seek(Key key, bool orEqual)
		{
			// floor: the largest index whose key is < pivot (or <= pivot when orEqual)
			int idx = -1, lo = 0, hi = this.Items.Length - 1;
			while (lo <= hi)
			{
				int mid = (int) (((uint) lo + (uint) hi) >> 1);
				int cmp = this.Items[mid].Key.CompareTo(key);
				if (cmp < 0 || (orEqual && cmp == 0)) { idx = mid; lo = mid + 1; } else { hi = mid - 1; }
			}
			this.Index = idx;
			return idx >= 0;
		}

		/// <inheritdoc />
		public bool SeekFirst()
		{
			this.Index = this.Items.Length > 0 ? 0 : -1;
			return this.Index == 0;
		}

		/// <inheritdoc />
		public void SeekBeforeFirst() => this.Index = -1;

		/// <inheritdoc />
		public bool Next()
		{
			if (this.Index < 0)
			{
				return SeekFirst();
			}
			if (this.Index + 1 < this.Items.Length) { this.Index++; return true; }
			return false; // a failed step keeps the cursor on the last key
		}

		/// <inheritdoc />
		public bool Previous()
		{
			if (this.Index <= 0) { return false; }
			this.Index--;
			return true;
		}

	}

}
