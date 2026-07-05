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

// This file is not part of the netstandard2.0 build: COLA store: CollectionsMarshal.AsSpan (in-place), RuntimeHelpers.IsReferenceOrContainsReferences, Span.Sort.
#if !NETSTANDARD2_0

// enables consitency checks after each operation to the set
//#define ENFORCE_INVARIANTS

namespace SnowBank.Collections.CacheOblivious
{
	using System;
	using System.Buffers;
	using System.Globalization;

	/// <summary>Represent an ordered list of ranges, each associated with a specific value, stored in a Cache Oblivious Lookup Array</summary>
	/// <typeparam name="TKey">Type of the keys stored in the set</typeparam>
	/// <typeparam name="TValue">Type of the values associated with each range</typeparam>
	/// <seealso cref="ColaRangeSet{TKey}"/>
	[PublicAPI]
	[DebuggerDisplay("Count={m_items.Count}, Bounds={m_bounds.Begin}..{m_bounds.End}")]
	public sealed class ColaRangeDictionary<TKey, TValue> : IEnumerable<ColaRangeDictionary<TKey, TValue>.Entry>, IDisposable
	{
		// This class is equivalent to ColaRangeSet<TKey>, except that we have an extra value stored in each range.
		// That means that we only merge ranges with the same value, and split/truncate/overwrite ranges with different values

		// INVARIANTS
		// * If there is at least on range, the set is not empty (ie: Begin <= End)
		// * The 'Begin' key is INCLUDED in range, but the 'End' key is EXCLUDED from the range (ie: Begin <= K < End)
		// * The 'End' key of a range MUST be GREATER THAN or EQUAL TO the 'Begin' key of a range (ie: ranges are not backwards)
		// * The 'End' key of a range CANNOT be GREATER THAN the 'Begin' key of the next range (ie: ranges do not overlap)
		// * If the 'End' key of a range is EQUAL TO the 'Begin' key of the next range, then they MUST have a DIFFERENT value

		/// <summary>Mutable range</summary>
		[DebuggerDisplay("{ToString(),nq}")]
		public sealed record Entry : ISpanFormattable
		{
			//REVIEW: consider making this a struct, if we refactor the rest of the code to use "ref Entry" ?

			/// <summary>Begin key of this range (inclusive)</summary>
			public TKey? Begin { get; internal set; }

			/// <summary>End key of this range (exclusive)</summary>
			public TKey? End { get; internal set; }

			/// <summary>Value for this range</summary>
			public TValue? Value { get; set; }

			/// <summary>Constructs a new <see cref="Entry"/></summary>
			public Entry(TKey? begin, TKey? end, TValue? value)
			{
				this.Begin = begin;
				this.End = end;
				this.Value = value;
			}

			/// <summary>Overwrite this range with another one</summary>
			/// <param name="other">New range that will overwrite the current instance</param>
			internal void Set(Entry other)
			{
				this.Begin = other.Begin;
				this.End = other.End;
				this.Value = other.Value;
			}

			/// <inheritdoc />
			public override string ToString() => ToString(null);

			/// <inheritdoc />
			public string ToString(string? format, IFormatProvider? formatProvider = null)
				=> string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"({this.Begin} ~ {this.End}, {this.Value})");

			/// <inheritdoc />
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
				=> destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"({this.Begin} ~ {this.End}, {this.Value})", out charsWritten);

			/// <summary>Deconstructs this entry into the <paramref name="begin"/>, <paramref name="end"/> and <paramref name="value"/> parts</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Deconstruct(out TKey? begin, out TKey? end, out TValue? value)
			{
				begin = this.Begin;
				end = this.End;
				value = this.Value;
			}

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
#if DEBUG
				if ((x == null || y == null) && System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				Contract.Debug.Assert(x != null && y != null);
				return m_comparer.Compare(x.Begin, y.Begin);
			}
		}

		private sealed class EndKeyComparer : IComparer<Entry>
		{
			private readonly IComparer<TKey> m_comparer;

			public EndKeyComparer(IComparer<TKey> comparer)
			{
				m_comparer = comparer;
			}

			public int Compare(Entry? x, Entry? y)
			{
				Contract.Debug.Requires(x != null && y != null);
				return m_comparer.Compare(x.End, y.End);
			}
		}

		private readonly ColaStore<Entry> m_items;
		private readonly IComparer<TKey> m_keyComparer;
		private readonly IEqualityComparer<TValue> m_valueComparer;
		private readonly Entry m_bounds;

		/// <summary>Constructs a new <see cref="ColaRangeDictionary{TKey,TValue}"/></summary>
		public ColaRangeDictionary(ArrayPool<Entry>? pool = null)
			: this(0, null, null, pool)
		{ }

		/// <summary>Constructs a new <see cref="ColaRangeDictionary{TKey,TValue}"/> with the given initial capacity</summary>
		public ColaRangeDictionary(int capacity, ArrayPool<Entry>? pool = null)
			: this(capacity, null, null, pool)
		{ }

		/// <summary>Constructs a new <see cref="ColaRangeDictionary{TKey,TValue}"/> with the given key and value comparer</summary>
		public ColaRangeDictionary(IComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer = null, ArrayPool<Entry>? pool = null)
			: this(0, keyComparer, valueComparer, pool)
		{ }

		/// <summary>Constructs a new <see cref="ColaRangeDictionary{TKey,TValue}"/> with the given key and value comparer, and initial capacity</summary>
		public ColaRangeDictionary(int capacity, IComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer = null, ArrayPool<Entry>? pool = null)
		{
			m_keyComparer = keyComparer ?? Comparer<TKey>.Default;
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			if (capacity == 0) capacity = 15;
			m_items = new(capacity, new BeginKeyComparer(m_keyComparer), pool);
			m_bounds = new(default(TKey), default(TKey), default(TValue));
		}

		/// <summary>Constructs a new <see cref="ColaRangeDictionary{TKey,TValue}"/> by copying the contents of another dictionary</summary>
		public ColaRangeDictionary(ColaRangeDictionary<TKey, TValue> source)
		{
			m_keyComparer = source.m_keyComparer;
			m_valueComparer = source.m_valueComparer;
			m_items = source.m_items.Copy();
			m_bounds = new(source.m_bounds.Begin, source.m_bounds.End, source.m_bounds.Value);
		}

		/// <summary>Creates a copy of this dictionary</summary>
		public ColaRangeDictionary<TKey, TValue> Copy() => new(this);

		/// <inheritdoc />
		public void Dispose()
		{
			m_items.Dispose();

			m_bounds.Value = default;
			m_bounds.Begin = default;
			m_bounds.End = default;
		}

		[Conditional("ENFORCE_INVARIANTS")]
		private void CheckInvariants()
		{
#if ENFORCE_INVARIANTS
			Contract.Debug.Invariant(m_bounds != null);
			Debug.WriteLine("INVARIANTS:" + this.ToString() + " <> " + m_bounds.ToString());

			if (m_items.Count == 0)
			{
				Contract.Debug.Invariant(EqualityComparer<TKey>.Default.Equals(m_bounds.Begin, default(TKey)));
				Contract.Debug.Invariant(EqualityComparer<TKey>.Default.Equals(m_bounds.End, default(TKey)));
			}
			else if (m_items.Count == 1)
			{
				Contract.Debug.Invariant(EqualityComparer<TKey>.Default.Equals(m_bounds.Begin, m_items[0].Begin));
				Contract.Debug.Invariant(EqualityComparer<TKey>.Default.Equals(m_bounds.End, m_items[0].End));
			}
			else
			{
				Entry previous = null;
				Entry first = null;
				foreach (var item in this)
				{
					Contract.Debug.Invariant(m_keyComparer.Compare(item.Begin, item.End) < 0, "End key should be after begin");

					if (previous == null)
					{
						first = item;
					}
					else
					{
						int c = m_keyComparer.Compare(previous.End, item.Begin);
						if (c > 0) Contract.Debug.Invariant(false, String.Format("Range overlapping: {0} and {1}", previous, item));
						if (c == 0 && m_valueComparer.Compare(previous.Value, item.Value) == 0) Contract.Debug.Invariant(false, String.Format("Unmerged ranges: {0} and {1}", previous, item));
					}
					previous = item;
				}
				Contract.Debug.Invariant(EqualityComparer<TKey>.Default.Equals(m_bounds.Begin, first.Begin), String.Format("Min bound {0} does not match with {1}", m_bounds.Begin, first.Begin));
				Contract.Debug.Invariant(EqualityComparer<TKey>.Default.Equals(m_bounds.End, previous.End), String.Format("Max bound {0} does not match with {1}", m_bounds.End, previous.End));
			}
#endif
		}

		/// <summary>Number of distinct ranges in this instance</summary>
		public int Count => m_items.Count;

		/// <summary>Allocated capacity of this instance</summary>
		public int Capacity => m_items.Capacity;

		/// <summary>Helper used to compare and sort keys of this instance</summary>
		public IComparer<TKey> KeyComparer => m_keyComparer;

		/// <summary>Helper used to check values of this instance for equality</summary>
		public IEqualityComparer<TValue> ValueComparer => m_valueComparer;

		/// <summary>Current bounds of this instance (minimum and maximum value)</summary>
		public Entry Bounds => m_bounds;

		/// <summary>Looks for the first existing range that is intersected by the start of the new range</summary>
		private Entry? GetBeginRangeIntersecting(Entry range)
		{
			int level = m_items.FindPrevious(range, true, out _, out var cursor);
			if (level < 0)
			{
				return null;
			}
			return cursor;
		}

		/// <summary>Looks for the last existing range that is intersected by the end of the new range</summary>
		private Entry? GetEndRangeIntersecting(Entry range)
		{
			int level = m_items.FindPrevious(range, true, out _, out var cursor);
			if (level < 0)
			{
				return null;
			}
			return cursor;
		}

		/// <summary>Returns the smaller key of the two</summary>
		private TKey Min(TKey? a, TKey? b)
		{
			Contract.Debug.Requires(a != null && b != null);
			return m_keyComparer.Compare(a, b) <= 0 ? a : b;
		}

		/// <summary>Returns the greater key of the two</summary>
		private TKey Max(TKey? a, TKey? b)
		{
			Contract.Debug.Requires(a != null && b != null);
			return m_keyComparer.Compare(a, b) >= 0 ? a : b;
		}

		/// <summary>Removes all ranges from this instance</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Clear()
		{
			m_items.Clear();
			m_bounds.Begin = default;
			m_bounds.End = default;

			CheckInvariants();
		}

		/// <summary>Removes everything between <paramref name="beginInclusive"/> and <paramref name="endExclusive"/> then translates everything</summary>
		/// <param name="beginInclusive">Begin key (inclusive)</param>
		/// <param name="endExclusive">End key (exclusive)</param>
		/// <param name="offset">Offset to apply</param>
		/// <param name="applyOffset">Func to apply offset to a key</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Remove(TKey beginInclusive, TKey endExclusive, TKey offset, Func<TKey?, TKey, TKey> applyOffset)
		{
			if (m_keyComparer.Compare(beginInclusive, endExclusive) >= 0)
			{
				throw new InvalidOperationException("End key must be greater than the Begin key.");
			}

			try
			{
				var entry = new Entry(beginInclusive, endExclusive, default(TValue));
				var iterator = m_items.GetIterator();
				var comparer = m_keyComparer;
				if (!iterator.Seek(entry, true))
				{
					// could not find an exact match, use the first one instead.
					iterator.SeekFirst();
				}
				var cursor = iterator.Current!;
				var c1 = comparer.Compare(beginInclusive, cursor.Begin);
				var c2 = comparer.Compare(endExclusive, cursor.End);
				List<Entry>? toRemove = null; //PERF: use a stackalloc'ed buffer?

				//begin < cursor.Begin
				if (c1 < 0)
				{
					var c3 = comparer.Compare(endExclusive, cursor.Begin);
					//end <= cursor.Begin
					//         [++++++++++
					// ------[
					//ou
					//       [++++++
					// ------[
					if (c3 <= 0)
					{
						TranslateAfter(null, offset, applyOffset);
						return;
					}
					//end < cursor.End
					//     [+++++++++++[
					//-----------[
					if (c2 < 0)
					{
						cursor.Begin = applyOffset(endExclusive, offset);
						cursor.End = applyOffset(cursor.End, offset);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end == cursor.End
					//     [+++++++++[
					//---------------[
					if (c2 == 0)
					{
						m_items.RemoveItem(cursor);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end > cursor.End
					//      [+++++++++[
					//-------------------...
					toRemove = [ cursor ];
					while (iterator.Next(out cursor))
					{
						c2 = comparer.Compare(endExclusive, cursor.End);
						c3 = comparer.Compare(endExclusive, cursor.Begin);
						//end <= cursor.Begin
						//       [+++++
						// ----[
						//ou
						//       [+++++
						// ------[
						if (c3 <= 0)
						{
							// set cursor so that the translation can be done properly
							cursor = entry;
							break;
						}
						//end < cursor.End
						//     [+++++++++++
						// ----------[
						if (c2 < 0)
						{
							cursor.Begin = beginInclusive;
							cursor.End = applyOffset(cursor.End, offset);
							break;
						}
						// end >= cursor.End
						//      [+++++++++[
						// ---------------[
						//ou
						//      [+++++++[
						// ----------------...
						toRemove.Add(cursor);
						if (c2 == 0) break;
					}
					m_items.RemoveItems(toRemove);
					TranslateAfter(cursor, offset, applyOffset);
				}
				//begin == cursor.Begin
				else if (c1 == 0)
				{
					//end < cursor.End
					// [+++++++++[
					// [-----[
					if (c2 < 0)
					{
						cursor.Begin = beginInclusive;
						cursor.End = applyOffset(cursor.End, offset);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end == cursor.End
					// [++++++++[
					// [--------[
					else if (c2 == 0)
					{
						toRemove = [ cursor ];
					}
					// end > cursor.End
					// [+++++++[
					// [-----------....
					else
					{
						toRemove = [ cursor ];
						while (iterator.Next(out cursor))
						{
							var c3 = comparer.Compare(endExclusive, cursor.Begin);
							c2 = comparer.Compare(endExclusive, cursor.End);
							//end < cursor.Begin
							//                [++++++++[
							//---------[
							//ou
							//         [+++++++[
							//---------[
							if (c3 <= 0)
							{
								break;
							}
							else
							{
								//end < cursor.End
								// [++++++++++++[
								//-----[
								if (c2 < 0)
								{
									cursor.Begin = beginInclusive;
									cursor.End = applyOffset(cursor.End, offset);
									break;
								}
								//end >= cursor.End
								// [+++++++++[
								//---------------...
								//or
								// [+++++++++[
								//-----------[
								toRemove.Add(cursor);
								if (c2 == 0) break;
							}
						}
					}
					m_items.RemoveItems(toRemove);
					TranslateAfter(cursor, offset, applyOffset);
				}
				//begin > cursor.Begin
				else
				{
					//end < cursor.End
					//   [++++++++++++[
					//      [----[
					// = [++[[++++[
					if (c2 < 0)
					{
						var oldEnd = cursor.End;
						cursor.End = beginInclusive;
						TranslateAfter(cursor, offset, applyOffset);
						m_items.Insert(new Entry(beginInclusive, applyOffset(oldEnd, offset), cursor.Value));
						return;
					}
					//end == cursor.End
					// [+++++++++++++[
					//       [-------[
					if (c2 == 0)
					{
						cursor.End = beginInclusive;
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end > cursor.End
					// [+++++++++++++[
					//       [-------------
					else
					{
						cursor.End = beginInclusive;
						while (iterator.Next(out cursor))
						{
							var c3 = comparer.Compare(endExclusive, cursor.Begin);
							c2 = comparer.Compare(endExclusive, cursor.End);
							//end <= cursor.Begin
							//      [++++++++++++[
							// --[
							//ou
							//      [++++++++++++[
							// -----[
							if (c3 <= 0)
							{
								break;
							}
							else
							{
								//end < cursor.End
								//     [+++++++++++++[
								// ------------[
								if (c2 < 0)
								{
									cursor.Begin = beginInclusive;
									cursor.End = applyOffset(cursor.End, offset);
									break;
								}
								//end >= cursor.End
								//   [+++++++++++[
								//--------------------...
								//ou
								//   [+++++++++++[
								//---------------[
								else
								{
									toRemove ??= [ ];
									toRemove.Add(cursor);
									if (c2 == 0) break;
								}
							}
						}

						if (toRemove != null)
						{
							m_items.RemoveItems(toRemove);
						}

						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
				}
			}
			finally
			{
				CheckInvariants();
			}
		}

		private void TranslateAfter(Entry? lastOk, TKey offset, Func<TKey?, TKey, TKey> applyKeyOffset)
		{
			var iterator = m_items.GetIterator();
			
			if (lastOk == null)
			{ // null => we need to shift everything
				if (!iterator.SeekFirst()) return;
			}
			else
			{
				if (!iterator.Seek(lastOk, true))
				{
					// the item passed has parameter has been deleted
					// - search for the next item
					// - if everything has been deleted, we exit early.
					if (!iterator.SeekFirst()) return;
					var c = m_keyComparer.Compare(lastOk.End, iterator.Current!.Begin);
					while (c > 0 && iterator.Next())
					{
						c = m_keyComparer.Compare(lastOk.End, iterator.Current!.Begin);
					}
				}
				else
				{
					// we want to shift the item that follow the one passed as parameter
					iterator.Next();
				}
			}

			var cursor = iterator.Current;
			do
			{
				// in the case where everything has been deleted after lastOK, the iterator is already passed the end when we reach here
				if (cursor == null) break;

				cursor.Begin = applyKeyOffset(cursor.Begin, offset);
				cursor.End = applyKeyOffset(cursor.End, offset);
			}
			while (iterator.Next(out cursor));

			// shift the bounds if required
			if (iterator.SeekFirst()) m_bounds.Begin = iterator.Current!.Begin;
			if (iterator.SeekLast()) m_bounds.End = iterator.Current!.End;
		}

		/// <summary>Marks a range with a new value</summary>
		/// <param name="range">Begin key (included), End key (excluded) and new Value for this range</param>
		/// <exception cref="InvalidOperationException">If <paramref name="range"/> is empty (End key less than or equal to Begin key)</exception>
		public void Mark((TKey BeginInclusive, TKey EndExclusive, TValue value) range) => Mark(range.BeginInclusive, range.EndExclusive, range.value);

		/// <summary>Marks a range with a new value</summary>
		/// <param name="range">Begin key (included) and End key (excluded) of the range</param>
		/// <param name="value">New value for this range</param>
		/// <exception cref="InvalidOperationException">If <paramref name="range"/> is empty (End key less than or equal to Begin key)</exception>
		public void Mark((TKey BeginInclusive, TKey EndExclusive) range, TValue value) => Mark(range.BeginInclusive, range.EndExclusive, value);

		/// <summary>Marks a range with a new value</summary>
		/// <param name="beginInclusive">Begin key of the range (included)</param>
		/// <param name="endExclusive">End key of the range (excluded)</param>
		/// <param name="value">New value for this range</param>
		/// <exception cref="InvalidOperationException">If <paramref name="endExclusive"/> is less than or equal to <paramref name="beginInclusive"/></exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Mark(TKey beginInclusive, TKey endExclusive, TValue value)
		{
			if (m_keyComparer.Compare(beginInclusive, endExclusive) >= 0) throw new InvalidOperationException("End key must be greater than the Begin key.");

			// adds a new interval to the dictionary by overwriting or splitting any previous interval
			// * if there are no interval, or the interval is disjoint from all other intervals, it is inserted as-is
			// * if the new interval completely overwrites one or more intervals, they will be replaced by the new interval
			// * if the new interval partially overlaps with one or more intervals, they will be split into chunks, and the new interval will be inserted between them

			// Examples:
			// { } + [0..1,A] => { [0..1,A] }
			// { [0..1,A] } + [2..3,B] => { [0..1,A], [2..3,B] }
			// { [4..5,A] } + [0..10,B] => { [0..10,B] }
			// { [0..10,A] } + [4..5,B] => { [0..4,A], [4..5,B], [5..10,A] }
			// { [2..4,A], [6..8,B] } + [3..7,C] => { [2..3,A], [3..7,C], [7..8,B] }
			// { [1..2,A], [2..3,B], ..., [9..10,Y] } + [0..10,Z] => { [0..10,Z] }

			var entry = new Entry(beginInclusive, endExclusive, value);
			var cmp = m_keyComparer;

			try
			{
				Entry cursor; //TODO: REVIEW: => ref Entry ?
				int c1, c2;
				switch (m_items.Count)
				{
					case 0:
					{ // the list empty

						// no checks required
						m_items.Insert(entry);
						m_bounds.Begin = entry.Begin;
						m_bounds.End = entry.End;
						break;
					}

					case 1:
					{ // there is only one value

						cursor = m_items.GetReference(0);

						c1 = cmp.Compare(beginInclusive, cursor.End);
						if (c1 >= 0)
						{
							// [--------)  [========)
							// or [--------|========)
							if (c1 == 0 && m_valueComparer.Equals(cursor.Value, value))
							{
								cursor.End = endExclusive;
							}
							else
							{
								m_items.Insert(entry);
							}
							m_bounds.End = endExclusive;
							return;
						}
						c1 = cmp.Compare(endExclusive, cursor.Begin);
						if (c1 <= 0)
						{
							// [========)  [--------)
							// or [========|--------)
							if (c1 == 0 && m_valueComparer.Equals(cursor.Value, value))
							{
								cursor.Begin = beginInclusive;
							}
							else
							{
								m_items.Insert(entry);
							}
							m_bounds.Begin = beginInclusive;
							return;
						}

						c1 = cmp.Compare(beginInclusive, cursor.Begin);
						c2 = cmp.Compare(endExclusive, cursor.End);
						if (c1 == 0)
						{ // same start
							if (c2 == 0)
							{ // same end
								//   [--------)
								// + [========)
								// = [========)
								cursor.Value = value;
							}
							else if (c2 < 0)
							{
								if (!m_valueComparer.Equals(cursor.Value, value))
								{
									//   [----------)
									// + [======)
									// = [======|---)

									cursor.Begin = endExclusive;
									m_items.Insert(entry);
								}
								else
								{
									//   [==========)
									// + [======)
									// = [==========)

									// no-op !
								}
							}
							else
							{
								//   [------)
								// + [==========)
								// = [==========)
								cursor.Set(entry);
								m_bounds.End = endExclusive;
							}
						}
						else if (c1 > 0)
						{ // entry is to the right
							if (c2 >= 0)
							{
								if (!m_valueComparer.Equals(value, cursor.Value))
								{
									//   [------)
									// +     [=======)
									// = [---|=======)
									cursor.End = beginInclusive;
									m_items.Insert(entry);
									if (c2 > 0) m_bounds.End = endExclusive;
								}
								else
								{
									//   [======)
									// +     [=======)
									// = [===========)
									cursor.End = endExclusive;
									m_bounds.End = endExclusive;
								}
							}
							else
							{
								if (!m_valueComparer.Equals(value, cursor.Value))
								{
									//   [-----------)
									// +     [====)
									// = [---|====|--)

									var tmp = new Entry(endExclusive, cursor.End, cursor.Value);
									cursor.End = beginInclusive;
									m_items.InsertItems(entry, tmp);
								}
								else
								{
									//   [===========)
									// +     [====)
									// = [===========)

									// no-op!
								}
							}
						}
						else
						{ // entry is to the left
							if (c2 >= 0)
							{
								cursor.Set(entry);
								m_bounds.End = endExclusive;
							}
							else
							{
								cursor.Begin = endExclusive;
								m_items.Insert(entry);
							}
							m_bounds.Begin = beginInclusive;
						}
						break;
					}

					default:
					{
						// check with the bounds first

						if (cmp.Compare(beginInclusive, m_bounds.End) > 0)
						{ // completely to the right
							m_items.Insert(entry);
							m_bounds.End = endExclusive;
							break;
						}
						if (cmp.Compare(endExclusive, m_bounds.Begin) < 0)
						{ // completely to the left
							m_items.Insert(entry);
							m_bounds.Begin = beginInclusive;
							break;
						}
						if (cmp.Compare(beginInclusive, m_bounds.Begin) <= 0 && cmp.Compare(endExclusive, m_bounds.End) >= 0)
						{ // overlaps with all the ranges
							// note :if we are here, there was at least 2 items, so just clear everything
							m_items.Clear();
							m_items.Insert(entry);
							m_bounds.Begin = entry.Begin;
							m_bounds.End = entry.End;
							break;
						}

						// note: we have already bound checked, so we know that there is at least one overlap !

						bool inserted = false;

						// => we will try to find the first range and last range in the dictionary that would be impacted, mutate them and delete all ranges in between

						var iterator = m_items.GetIterator();
						// seek to the range that starts before (or at) the new range's begin point
						if (!iterator.Seek(entry, true))
						{ // the new range will go into first position
							// => still need to check if we are overlapping with the next ranges
							iterator.SeekFirst();
							m_bounds.Begin = beginInclusive;
						}

						m_bounds.End = Max(m_bounds.End, endExclusive);

						cursor = iterator.Current!;

						c1 = cmp.Compare(cursor.Begin, beginInclusive);
						c2 = cmp.Compare(cursor.End, endExclusive);
						if (c1 >= 0)
						{
							if (c2 == 0)
							{ // same end
								//   [-------)..           [-------)..
								// + [=======)        + [==========)
								// = [=======)..      = [==========)..

								cursor.Set(entry);

								// It is possible that we just "repainted" a gap with the same color has the preceding and/or following entry.
								// We will attempt to merge these entry back into a single entry (to prevent fragmentation)

								// capture current position (allow faster deletion later on)
								var pos = iterator.Position;

								if (iterator.Previous())
								{
									var prev = iterator.Current;
									Entry? next = null;

									// we want to check also the one after us, which require jump back 2 slots
									//TODO: having the ability to "snapshot" the level cursors inside the iterator would be helpful here!
									if (iterator.Next() && iterator.Next())
									{
										next = iterator.Current;
									}

									if (prev != null && cmp.Compare(prev.End, beginInclusive) == 0 && m_valueComparer.Equals(prev.Value, value))
									{ // the previous is contiguous and with the same value, it can be merged!
										if (next != null && cmp.Compare(next.Begin, endExclusive) == 0 && m_valueComparer.Equals(next.Value, value))
										{ // merge all three into a single contiguous

											//   [=======)       [=====)..
											// +         [=======)
											// = [=====================)..

											prev.End = next.End;
											m_items.RemoveAt(pos.Level, pos.Offset);
											m_items.RemoveItem(next);
										}
										else
										{ // only merge with the previous

											//   [=======)          [=====)..       [=======)       [-------)..
											// +         [=======)                          [=======)
											// = [===============)  [=====)..       [===============|-------)..

											prev.End = endExclusive;
											m_items.RemoveAt(pos.Level, pos.Offset);
										}
									}
									else if (next != null && cmp.Compare(next.Begin, endExclusive) == 0 && m_valueComparer.Equals(next.Value, value))
									{ // the next one is contigious and with the same value, it can be merged!

										//   [=======)          [=====)..       [-------)       [=====)..
										// +            [=======)                       [=======)
										// = [=======)  [=============)..       [-------|=============)

										next.Begin = beginInclusive;
										m_items.RemoveAt(pos.Level, pos.Offset);
									}
								}
								else if (iterator.SeekFirst() && iterator.Next())
								{ // there was no previous entry, but still check the next one
									var next = iterator.Current;
									if (next != null && cmp.Compare(next.Begin, endExclusive) == 0 && m_valueComparer.Equals(next.Value, value))
									{ // the next one is contiguous and with the same value, it can be merged!

										//   x        [=====)..
										// + x[=======)
										// = x[=============)..

										next.Begin = beginInclusive;
										m_items.RemoveAt(pos.Level, pos.Offset);
									}
								}
								return;
							}

							if (c2 > 0)
							{ // truncate begin
								//   [----------)..        [----------)..
								// + [=======)	      + [=======)
								// = [=======|--)..   = [=======|-----)..
								cursor.Begin = endExclusive;
								m_items.Insert(entry);
								return;
							}

							// replace + propagate
							//   [-------)???..            [-----)????..
							// + [==========)         + [============)
							// = [==========)..       = [============)..

							cursor.Set(entry);
							//we keep the reference to cursor to be able to modify it later
							entry = cursor;
							inserted = true;
							//TODO: need to propagate !
						}
						else
						{
							if (c2 == 0)
							{ // same end
								//   [------------)
								//       [========)
								// = [---|========)

								if (!m_valueComparer.Equals(cursor.Value, value))
								{
									cursor.End = beginInclusive;
									m_items.Insert(entry);
								}
								return;
							}

							if (c2 > 0)
							{
								//   [------------)
								//       [=====)
								// = [---|=====|--)

								if (!m_valueComparer.Equals(cursor.Value, value))
								{
									var tmp = new Entry(endExclusive, cursor.End, cursor.Value);
									cursor.End = beginInclusive;
									m_items.InsertItems(entry, tmp);
								}
								return;
							}

							int c3 = cmp.Compare(beginInclusive, cursor.End);
							if (c3 >= 0)
							{
								if (c3 == 0 && m_valueComparer.Equals(value, cursor.Value))
								{ // touching same value => merge
									cursor.End = endExclusive;
									entry = cursor;
									inserted = true;
								}
								else
								{
									//   [---)
									//         [=====????
									// = [---) [=====????
								}
							}
							else
							{
								//   [--------????
								//       [====????
								// = [---|====????
								cursor.End = beginInclusive;
							}
						}

						// if we end up here, it means that we may be overlapping with following items
						// => we need to delete them until we reach the last one, which we need to either delete or mutate
						// => also, if we haven't inserted the entry yet, we will reuse the first deleted range to insert the entry, and only insert at the end if we haven't found a spot

						List<Entry>? deleted = null;

						while (true)
						{
							if (!iterator.Next())
							{ // we reached past the end of the db
								break;
							}

							// cursor: existing range that we need to either delete or mutate
							cursor = iterator.Current!;

							c1 = cmp.Compare(cursor.Begin, endExclusive);
							if (c1 == 0)
							{ // touching the next range
								if (m_valueComparer.Equals(value, cursor.Value))
								{ // contiguous block with same value => merge
									//         [===========)
									//   [=====)
									// = [=================)
									if (inserted)
									{
										if (cmp.Compare(cursor.End, entry.End) > 0)
										{
											entry.End = cursor.End;
										}
										//note: we can't really delete while iterating with a cursor, so just mark it for deletion
										deleted ??= [ ];
										deleted.Add(cursor);
									}
									else
									{
										cursor.Begin = beginInclusive;
										entry = cursor;
										inserted = true;
									}
									break;
								}
								else
								{
									//         [-----------)
									//   [=====)
									// = [=====|-----------)
								}
								break;
							}
							else if (c1 > 0)
							{ // we are past the inserted range, nothing to do any more
								//            [------------)
								//   [=====)
								// = [=====)  [------------)
								//Console.WriteLine("  . no overlap => break");
								break;
							}

							c1 = cmp.Compare(cursor.End, endExclusive);
							if (c1 <= 0)
							{ // we are completely covered => delete

								//      [-------)           [-------)
								// + [...=======)      + [...=======...)
								// = [...=======)      = [...=======...)
								if (!inserted)
								{ // use that slot to insert ourselves
									cursor.Set(entry);
									// get the reference to be able to eventually merge it afterward
									entry = cursor;
									inserted = true;
								}
								else
								{
									//note: we can't really delete while iterating with a cursor, so just mark it for deletion
									deleted ??= new List<Entry>();
									deleted.Add(cursor);
								}
							}
							else
							{ // we are only partially overlapped

								//       [------------)
								//   [....========)
								// = [....========|---)

								cursor.Begin = endExclusive;
								break;
							}
						}

						if (deleted is { Count: > 0 })
						{
							m_items.RemoveItems(deleted);
						}

						if (!inserted)
						{ // we did not find an existing spot to re-use, so we need to insert the new range
							m_items.Insert(entry);
						}
						break;
					}
				}
			}
			finally
			{
				CheckInvariants();
			}
		}

		/// <summary>Copies all the ranges of this dictionary onto another dictionary</summary>
		/// <param name="destination">Destination dictionary that will be modified</param>
		public void CopyTo(ColaRangeDictionary<TKey, TValue> destination)
		{
			using var iter = new ColaStore.Enumerator<Entry>(m_items, reverse: false);
			while (iter.MoveNext())
			{
				var entry = iter.Current;
				destination.Mark(entry.Begin!, entry.End!, entry.Value!);
			}
		}

		/// <summary>Marks a range with a new value by updating, merging or overriding any previous values in this range</summary>
		public void Merge<TData>(TKey beginInclusive, TKey endExclusive, TData value, Func<TValue?, TData, TValue> combinator)
		{
			var keyComparer = m_keyComparer;
			if (keyComparer.Compare(beginInclusive, endExclusive) >= 0) throw new InvalidOperationException("End key must be greater than the Begin key.");

			var cursor = beginInclusive;

			try
			{
				while (keyComparer.Compare(cursor, endExclusive) < 0)
				{
					if (!Intersect(cursor, endExclusive, out var match))
					{
						// empty, simply add the range
						Mark(cursor, endExclusive, combinator(default, value));
						return;
					}

					Contract.Debug.Assert(match.Begin is not null && match.End is not null);

					if (keyComparer.Compare(cursor, match.Begin) < 0)
					{
						// we have a tiny slice before that we have to paint
						Mark(cursor, match.Begin!, combinator(default, value));
					}

					if (keyComparer.Compare(endExclusive, match.End) <= 0)
					{
						// we either cover exactly, or end before, either cases this is the last chunk to update
						// we are covering more, fully update the rest of the match
						Mark(match.Begin!, endExclusive, combinator(match.Value, value));
						break;
					}

					// update the chunk, and continue
					Mark(match.Begin!, match.End!, combinator(match.Value, value));
					cursor = match.End!;
				}
			}
			finally
			{
				CheckInvariants();
			}
		}

		/// <summary>Returns the value of the range that contains the specified key, if there is one.</summary>
		/// <param name="key">Key that is being looked up</param>
		/// <param name="value">If the key intersects a range, receives the value of this range.</param>
		/// <returns><see langword="true"/> if there is a range that contains <paramref name="key"/>; otherwise, <see langword="false"/> (outside the bounds, or between two ranges)</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			value = default;
			if (m_items.Count == 0) return false;

			var cmp = m_keyComparer;
			if (cmp.Compare(m_bounds.Begin, key) > 0) return false;
			if (cmp.Compare(m_bounds.End, key) <= 0) return false;

			var entry = new Entry(key, key, default(TValue));

			var iterator = m_items.GetIterator();
			if (!iterator.Seek(entry, orEqual: true))
			{ // starts before
				iterator.SeekFirst();
			}

			var cursor = iterator.Current!;
			do
			{
				// key is in internal if CMP(key, A.begin) >= 0 .AND. CMP(key, B.end) <= 0

				if (cmp.Compare(key, cursor.Begin) < 0)
				{ 
					return false;
				}

				if (cmp.Compare(cursor.End, key) >= 0)
				{
					value = cursor.Value!;
					return true;
				}
			}
			while(iterator.Next(out cursor));

			return false;
		}

		/// <summary>Returns the value of the range that contains the specified key, or a default value if there is none.</summary>
		/// <param name="key">Key that is being looked up</param>
		/// <param name="defaultValue">Value that will be returned if the key is outside the bounds, or falls between two ranges.</param>
		/// <returns>Value of the range that contains <paramref name="key"/>; otherwise, <paramref name="defaultValue"/></returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public TValue GetValueOrDefault(TKey key, TValue defaultValue) => TryGetValue(key, out var value) ? value : defaultValue;

		/// <summary>Checks if there is at least one range in the dictionary that intersects with the specified range, and matches the predicate</summary>
		/// <param name="begin">Lower bound of the intersection</param>
		/// <param name="end">Higher bound (excluded) of the intersection</param>
		/// <param name="value">Receives the value of the first matched segment in the range</param>
		/// <returns><see langword="true"/> if there was at least one intersecting range, and <paramref name="value"/> received the corresponding value.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool FindFirst(TKey begin, TKey end, [MaybeNullWhen(false)] out TValue value)
		{
			value = default;
			if (m_items.Count == 0) return false;

			var cmp = m_keyComparer;
			if (cmp.Compare(m_bounds.Begin, end) >= 0) return false;
			if (cmp.Compare(m_bounds.End, begin) <= 0) return false;

			var entry = new Entry(begin, end, default(TValue));

			var iterator = m_items.GetIterator();
			if (!iterator.Seek(entry, orEqual: true))
			{ // starts before
				iterator.SeekFirst();
			}

			var cursor = iterator.Current!;
			do
			{
				// A and B intersects if: CMP(B.end, A.begin) <= 0 .OR. CMP(A.end, B.begin) <= 0

				if (cmp.Compare(end, cursor.Begin) <= 0)
				{ 
					return false;
				}

				if (cmp.Compare(cursor.End, begin) > 0)
				{
					value = cursor.Value!;
					return true;
				}
			}
			while(iterator.Next(out cursor));

			return false;
		}

		/// <summary>Checks if there is at least one range in the dictionary that intersects with the specified range, and matches the predicate</summary>
		/// <param name="begin">Lower bound of the intersection</param>
		/// <param name="end">Higher bound (excluded) of the intersection</param>
		/// <param name="match"></param>
		/// <returns><see langword="true"/> if there was at least one intersecting range.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool Intersect(TKey begin, TKey end, [MaybeNullWhen(false)] out Entry match)
		{
			match = null;
			if (m_items.Count == 0) return false;

			var cmp = m_keyComparer;
			if (cmp.Compare(m_bounds.Begin, end) >= 0) return false;
			if (cmp.Compare(m_bounds.End, begin) <= 0) return false;

			var entry = new Entry(begin, end, default(TValue));

			var iterator = m_items.GetIterator();
			if (!iterator.Seek(entry, orEqual: true))
			{ // starts before
				iterator.SeekFirst();
			}

			var cursor = iterator.Current!;
			do
			{
				// A and B intersects if: CMP(B.end, A.begin) <= 0 .OR. CMP(A.end, B.begin) <= 0

				if (cmp.Compare(end, cursor.Begin) <= 0)
				{
					return false;
				}

				if (cmp.Compare(cursor.End, begin) > 0)
				{
					match = cursor;
					return true;
				}
			}
			while(iterator.Next(out cursor));

			return false;
		}

		/// <summary>Checks if there is at least one range in the dictionary that intersects with the specified range, and matches the predicate</summary>
		/// <param name="begin">Lower bound of the intersection</param>
		/// <param name="end">Higher bound (excluded) of the intersection</param>
		/// <param name="predicate">Predicate called for each intersected range.</param>
		/// <param name="match">Receives the first matching entry, if there is one.</param>
		/// <returns><see langword="true"/> if there was at least one intersecting range, and <paramref name="predicate"/> returned true for that range.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool Intersect(TKey begin, TKey end, Func<TValue?, bool> predicate, [MaybeNullWhen(false)] out Entry match)
		{
			match = null;
			if (m_items.Count == 0) return false;

			var cmp = m_keyComparer;
			if (cmp.Compare(m_bounds.Begin, end) >= 0) return false;
			if (cmp.Compare(m_bounds.End, begin) <= 0) return false;

			var entry = new Entry(begin, end, default(TValue));

			var iterator = m_items.GetIterator();
			if (!iterator.Seek(entry, orEqual: true))
			{ // starts before
				iterator.SeekFirst();
			}

			var cursor = iterator.Current!;
			do
			{
				// A and B intersects if: CMP(B.end, A.begin) <= 0 .OR. CMP(A.end, B.begin) <= 0

				if (cmp.Compare(end, cursor.Begin) <= 0)
				{ 
					return false;
				}

				if (cmp.Compare(cursor.End, begin) > 0 && predicate(cursor.Value))
				{
					match = cursor;
					return true;
				}
			}
			while(iterator.Next(out cursor));

			return false;
		}

		/// <summary>Checks if there is at least one range in the dictionary that intersects with the specified range, and matches the predicate</summary>
		/// <param name="begin">Lower bound of the intersection</param>
		/// <param name="end">Higher bound (excluded) of the intersection</param>
		/// <param name="arg">Value that is passed as the second argument to <paramref name="predicate"/></param>
		/// <param name="predicate">Predicate called for each intersected range.</param>
		/// <param name="match">Receives the first matching entry, if there is one.</param>
		/// <returns><see langword="true"/> if there was at least one intersecting range, and <paramref name="predicate"/> returned true for that range.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool Intersect<TArg>(TKey begin, TKey end, TArg arg, Func<TValue?, TArg, bool> predicate, [MaybeNullWhen(false)] out Entry match)
		{
			match = null;
			if (m_items.Count == 0) return false;

			var cmp = m_keyComparer;
			if (cmp.Compare(m_bounds.Begin, end) >= 0) return false;
			if (cmp.Compare(m_bounds.End, begin) <= 0) return false;

			var entry = new Entry(begin, end, default(TValue));

			var iterator = m_items.GetIterator();
			if (!iterator.Seek(entry, orEqual: true))
			{ // starts before
				iterator.SeekFirst();
			}

			var cursor = iterator.Current!;
			do
			{
				
				// A and B intersects if: CMP(B.end, A.begin) <= 0 .OR. CMP(A.end, B.begin) <= 0

				if (cmp.Compare(end, cursor.Begin) <= 0)
				{ 
					return false;
				}

				if (cmp.Compare(cursor.End, begin) > 0 && predicate(cursor.Value, arg))
				{
					match = cursor;
					return true;
				}
			}
			while(iterator.Next(out cursor));

			return false;
		}

		/// <summary>Enumerate all the keys in the dictionary that are in the specified range</summary>
		/// <param name="begin">Start of the range</param>
		/// <param name="end">End of the range</param>
		/// <returns>Sequence of the all the ranges in the dictionary that intersect the specified range.</returns>
		/// <remarks>The dictionary should not be modified while iterating over the sequence.</remarks>
		[CollectionAccess(CollectionAccessType.Read)]
		public IEnumerable<(TKey Begin, TKey End, TValue Value)> Scan(TKey begin, TKey end)
		{
			// return the unordered list of all the keys that are between the begin/end pair.
			// each bound is included in the list if its corresponding 'orEqual' is set to true

			if (m_items.Count > 0)
			{
				var iter = m_items.GetIterator();
				if (!iter.Seek(new Entry(begin, default, default), orEqual: true))
				{ // starts before
					iter.SeekFirst();
				}

				var cursor = iter.Current!;
				do
				{
					if (m_keyComparer.Compare(cursor.End, begin) <= 0)
					{ // ends before the end of the range
						yield break;
					}
					if (m_keyComparer.Compare(cursor.Begin, end) >= 0)
					{ // starts after the end of the range
						yield break;
					}

					yield return (cursor.Begin!, cursor.End!, cursor.Value!);
				}
				while (iter.Next(out cursor));
			}
		}

		/// <summary>Returns an iterator that can read the contents of this dictionary</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[MustDisposeResource]
		public ColaStore<Entry>.Iterator GetIterator()
		{
			return m_items.GetIterator();
		}

		/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[MustDisposeResource]
		public ColaStore.Enumerator<Entry> GetEnumerator()
		{
			return new(m_items, reverse: false);
		}

		/// <summary>Returns a sequence of all the ranges in this dictionary, ordered by their keys.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[CollectionAccess(CollectionAccessType.Read)]
		public IEnumerable<Entry> IterateOrdered() => m_items.IterateOrdered();

		[MustDisposeResource]
		IEnumerator<Entry> IEnumerable<Entry>.GetEnumerator() => this.GetEnumerator();

		[MustDisposeResource]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <summary>Writes the contents of this dictionary into a log, for debugging purpose [DEBUG ONLY]</summary>
		[Conditional("DEBUG")]
		[CollectionAccess(CollectionAccessType.Read)]
		public void Debug_Dump(TextWriter output)
		{
#if DEBUG
			output.WriteLine($"Dumping ColaRangeDictionary<{typeof(TKey).Name}> filled at {(100.0d * this.Count / m_items.Capacity):N2}%");
			m_items.Debug_Dump(output);
#endif
		}

		/// <inheritdoc />
		public override string ToString()
		{
			if (m_items.Count == 0) return "{ }";

			var sb = new System.Text.StringBuilder();
			Entry? previous = null;
			foreach(var item in this)
			{
				if (previous == null)
				{
					sb.Append('[');
				}
				else if (m_keyComparer.Compare(previous.End, item.Begin) < 0)
				{
					sb.Append(previous.End).Append(") [");
				}
				else
				{
					sb.Append(previous.End).Append('|');
				}

				sb.Append(item.Begin).Append("..(").Append(item.Value).Append(")..");
				previous = item;
			}
			if (previous != null)
			{
				sb.Append(previous.End).Append(')');
			}

			return sb.ToString();
		}

	}

}

#endif
