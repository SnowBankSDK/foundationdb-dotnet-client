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

namespace SnowBank.Collections.CacheOblivious
{
	using System;
	using System.Buffers;
	using System.Globalization;

	/// <summary>Represent an ordered list of ranges, stored in a Cache Oblivious Lookup Array</summary>
	/// <typeparam name="TKey">Type of keys stored in the set</typeparam>
	/// <seealso cref="ColaRangeDictionary{TKey,TValue}"/>
	[PublicAPI]
	[DebuggerDisplay("Count={m_items.Count}, Bounds={m_bounds.Begin}..{m_bounds.End}")]
	public sealed class ColaRangeSet<TKey> : IEnumerable<ColaRangeSet<TKey>.Entry>, IDisposable
	{
		// We store the ranges in a COLA array that is sorted by the Begin keys
		// The range are mutable, which allows for efficient merging

		// INVARIANTS
		// * If there is at least on range, the set is not empty
		// * The Begin key is INCLUDED in range, but the End key is EXCLUDED from the range (ie: Begin <= K < End)
		// * The End key of a range is always GREATER than or EQUAL to the Begin key of a range (ie: ranges are not backwards)
		// * The End key of a range is always strictly LESS than the Begin key of the next range (ie: there are gaps between ranges)

		// This should give us a sorted set of disjoint ranges

		/// <summary>Mutable range</summary>
		public sealed class Entry : ISpanFormattable
		{
			/// <summary>Begin key of this range (inclusive)</summary>
			public TKey? Begin { get; internal set; }

			/// <summary>End key of this range (exclusive)</summary>
			public TKey? End { get; internal set; }

			/// <summary>Constructs a new <see cref="Entry"/></summary>
			public Entry(TKey? begin, TKey? end)
			{
				this.Begin = begin;
				this.End = end;
			}

			internal void ReplaceWith(Entry other)
			{
				this.Begin = other.Begin;
				this.End = other.End;
			}

			internal void Update(TKey? begin, TKey? end)
			{
				this.Begin = begin;
				this.End = end;
			}

			internal bool Contains(TKey key, IComparer<TKey> comparer)
			{
				return comparer.Compare(key, this.Begin) >= 0 && comparer.Compare(key, this.End) < 0;
			}

			/// <inheritdoc />
			public override string ToString() => ToString(null);

			/// <inheritdoc />
			public string ToString(string? format, IFormatProvider? formatProvider = null)
				=> string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"[{this.Begin}, {this.End})");

			/// <inheritdoc />
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
				=> destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"[{this.Begin}, {this.End})", out charsWritten);

		}

		/// <summary>Range comparer that only test the Begin key</summary>
		private sealed class BeginKeyComparer : IComparer<Entry>
		{
			private readonly IComparer<TKey> m_comparer;
			
			public BeginKeyComparer(IComparer<TKey> comparer)
			{
				m_comparer = comparer;
			}

			public int Compare(Entry? x, Entry? y)
			{
				Contract.Debug.Requires(x != null && y != null);
				return m_comparer.Compare(x.Begin, y.Begin);
			}

		}

		private readonly ColaStore<Entry> m_items;
		private readonly IComparer<TKey> m_comparer;
		private readonly Entry m_bounds;

		/// <summary>Constructs a new <see cref="ColaRangeSet{TKey}"/></summary>
		public ColaRangeSet(ArrayPool<Entry>? pool = null)
			: this(0, null, pool)
		{ }

		/// <summary>Constructs a new <see cref="ColaRangeSet{TKey}"/> with the given initial capacity</summary>
		public ColaRangeSet(int capacity, ArrayPool<Entry>? pool = null)
			: this(capacity, null, pool)
		{ }

		/// <summary>Constructs a new <see cref="ColaRangeSet{TKey}"/> with the given key comparer</summary>
		public ColaRangeSet(IComparer<TKey>? keyComparer, ArrayPool<Entry>? pool = null)
			: this(0, keyComparer, pool)
		{ }

		/// <summary>Constructs a new <see cref="ColaRangeSet{TKey}"/> with the given initial capacity and key comparer</summary>
		public ColaRangeSet(int capacity, IComparer<TKey>? keyComparer, ArrayPool<Entry>? pool = null)
		{
			m_comparer = keyComparer ?? Comparer<TKey>.Default;
			if (capacity == 0) capacity = 15;
			m_items = new(capacity, new BeginKeyComparer(m_comparer), pool);
			m_bounds = new(default(TKey), default(TKey));
		}

		/// <inheritdoc />
		public void Dispose()
		{
			m_items.Dispose();
	
			m_bounds.Begin = default;
			m_bounds.End = default;
		}

		/// <summary>Number of distinct ranges in this instance</summary>
		public int Count => m_items.Count;

		/// <summary>Allocated capacity of this instance</summary>
		public int Capacity => m_items.Capacity;

		/// <summary>Helper used to compare and sort keys of this instance</summary>
		public IComparer<TKey> Comparer => m_comparer;

		/// <summary>Current bounds of this instance (minimum and maximum value)</summary>
		public Entry Bounds => m_bounds;

		private bool Resolve(Entry previous, Entry candidate)
		{

			int c = m_comparer.Compare(previous.Begin, candidate.Begin);
			if (c == 0)
			{ // they share the same begin key !

				if (m_comparer.Compare(previous.End, candidate.End) < 0)
				{ // candidate replaces the previous ony
					previous.ReplaceWith(candidate);
				}
				return true;
			}

			if (c < 0)
			{ // b is to the right
				if (m_comparer.Compare(previous.End, candidate.Begin) < 0)
				{ // there is a gap in between
					return false;
				}
				// they touch or overlap
				previous.Update(previous.Begin, Max(previous.End, candidate.End));
				return true;
			}
			else
			{ // b is to the left
				if (m_comparer.Compare(candidate.End, previous.Begin) < 0)
				{ // there is a gap in between
					return false;
				}
				// they touch or overlap
				previous.Update(candidate.Begin, Max(previous.End, candidate.End));
				return true;
			}
		}

		private TKey Min(TKey? a, TKey? b)
		{
			Contract.Debug.Requires(a != null && b != null);
			return m_comparer.Compare(a, b) <= 0 ? a : b;
		}

		private TKey Max(TKey? a, TKey? b)
		{
			Contract.Debug.Requires(a != null && b != null);
			return m_comparer.Compare(a, b) >= 0 ? a : b;
		}

		/// <summary>Removes all ranges from this instance</summary>
		public void Clear()
		{
			m_items.Clear();
			m_bounds.Update(default(TKey), default(TKey));
		}

		/// <summary>Adds a range to this set</summary>
		/// <param name="begin">Begin key of the range (included)</param>
		/// <param name="end">End key of the range (excluded)</param>
		/// <exception cref="InvalidOperationException">If <paramref name="end"/> is less than or equal to <paramref name="begin"/></exception>
		/// <remarks>If the range overlaps existing ranges, they will be merged as required.</remarks>
		public void Mark(TKey begin, TKey end)
		{
			if (m_comparer.Compare(begin, end) >= 0) throw new InvalidOperationException($"End key `{begin}` must be greater than the Begin key `{end}`.");

			var entry = new Entry(begin, end);

			Entry? cursor; //TODO: REVIEW: ref Entry ?

			switch (m_items.Count)
			{
				case 0:
				{ // the list empty

					// no checks required
					m_items.Insert(entry);
					m_bounds.ReplaceWith(entry);
					break;
				}

				case 1:
				{ // there is only one value

					cursor = m_items.GetReference(0);
					if (!Resolve(cursor, entry))
					{ // no conflict
						m_items.Insert(entry);
						m_bounds.Update(
							Min(entry.Begin, cursor.Begin),
							Max(entry.End, cursor.End)
						);
					}
					else
					{ // merged with the previous range
						m_bounds.ReplaceWith(cursor);
					}
					break;
				}
				default:
				{
					// check with the bounds first

					if (m_comparer.Compare(begin, m_bounds.End) > 0)
					{ // completely to the right
						m_items.Insert(entry);
						m_bounds.Update(m_bounds.Begin, end);
						break;
					}
					if (m_comparer.Compare(end, m_bounds.Begin) < 0)
					{ // completely to the left
						m_items.Insert(entry);
						m_bounds.Update(begin, m_bounds.End);
						break;
					}
					if (m_comparer.Compare(begin, m_bounds.Begin) <= 0 && m_comparer.Compare(end, m_bounds.End) >= 0)
					{ // overlaps with all the ranges
						// note :if we are here, there was at least 2 items, so just clear everything
						m_items.Clear();
						m_items.Insert(entry);
						m_bounds.ReplaceWith(entry);
						break;
					}


					// overlaps with existing ranges, we may need to resolve conflicts
					int level;
					bool inserted = false;

					// once inserted, will it conflict with the previous entry ?
					if ((level = m_items.FindPrevious(entry, true, out int offset, out cursor)) >= 0)
					{
						if (Resolve(cursor!, entry))
						{
							entry = cursor!;
							inserted = true;
						}
					}

					// also check for potential conflicts with the next entries
					while (true)
					{
						level = m_items.FindNext(entry, false, out offset, out cursor);
						if (level < 0) break;

						if (inserted)
						{ // we already have inserted the key so conflicts will remove the next segment
							if (Resolve(entry, cursor))
							{ // next segment has been removed
								m_items.RemoveAt(level, offset);
							}
							else
							{
								break;
							}
						}
						else
						{ // we haven't inserted the key yet, so in case of conflict, we will use the next segment's slot
							if (Resolve(cursor, entry))
							{
								inserted = true;
							}
							else
							{
								break;
							}
						}
					}

					if (!inserted)
					{ // no conflict, we have to insert the new range
						m_items.Insert(entry);
					}

					m_bounds.Update(
						Min(m_bounds.Begin, entry.Begin),
						Max(m_bounds.End, entry.End)
					);

					break;
				}
			}

			//TODO: check constraints !
		}

		/// <summary>Copies all the ranges of this set onto another set</summary>
		/// <param name="destination">Destination set that will be modified</param>
		public void CopyTo(ColaRangeSet<TKey> destination)
		{
			using var iter = new ColaStore.Enumerator<Entry>(m_items, reverse: false);
			while (iter.MoveNext())
			{
				var entry = iter.Current;
				destination.Mark(entry.Begin!, entry.End!);
			}
		}

		/// <summary>Checks if there is at least one range that contains the specified key</summary>
		/// <param name="key">Key to test</param>
		/// <returns><c>true</c> if the key is contained by one range; otherwise, <c>false</c>.</returns>
		public bool ContainsKey(TKey key)
		{
			if (m_bounds.Contains(key, m_comparer))
			{
				int level = m_items.FindPrevious(new(key, key), orEqual: true, out _, out var entry);
				return level >= 0 && entry.Contains(key, m_comparer);
			}
			return false;
		}

		/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
		public ColaStore.Enumerator<Entry> GetEnumerator() => new(m_items, reverse: false);

		/// <summary>Returns a sequence of all the ranges in this set, ordered by their keys.</summary>
		public IEnumerable<Entry> IterateOrdered() => m_items.IterateOrdered();

		IEnumerator<Entry> IEnumerable<Entry>.GetEnumerator() => this.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <summary>Writes the contents of this set into a log, for debugging purpose [DEBUG ONLY]</summary>
		[Conditional("DEBUG")]
		public void Debug_Dump(TextWriter output)
		{
#if DEBUG
			output.WriteLine($"Dumping ColaRangeSet<{typeof(TKey).Name}> filled at {(100.0d * this.Count / this.Capacity):N2}%");
			m_items.Debug_Dump(output);
#endif
		}

		/// <inheritdoc />
		public override string ToString()
		{
			if (m_items.Count == 0) return "{ }";
			return "{ " + string.Join(", ", this) + " }";
		}

	}

}
