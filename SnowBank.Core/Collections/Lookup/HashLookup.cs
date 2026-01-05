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

namespace SnowBank.Collections.Generic
{
	/// <summary>Lookup table that groups elements by a common key</summary>
	/// <typeparam name="TKey">Type of the key used to group elements</typeparam>
	/// <typeparam name="TElement">Type of the elements</typeparam>
	[PublicAPI]
	[DebuggerDisplay("Count={m_items.Count}")]
	public class HashLookup<TKey, TElement> : IEnumerable<Grouping<TKey, TElement>>, ILookup<TKey, TElement> where TKey : notnull
	{

		private readonly Dictionary<TKey, Grouping<TKey, TElement>> m_items;

		public HashLookup()
			: this(EqualityComparer<TKey>.Default)
		{ }

		public HashLookup(IEqualityComparer<TKey> comparer)
			: this(919, comparer) // 919 (0x397) is the prime number closest to 1000
		{ }

		public HashLookup(int capacity, IEqualityComparer<TKey> comparer)
		{
			Contract.NotNull(comparer);
			m_items = new(capacity, comparer);
		}

		public HashLookup(HashLookup<TKey, TElement> elements, IEqualityComparer<TKey> comparer)
		{
			Contract.NotNull(elements);
			Contract.NotNull(comparer);
			m_items = new(elements.m_items, comparer);
		}

		public HashLookup(ILookup<TKey, TElement> elements, IEqualityComparer<TKey> comparer)
		{
			Contract.NotNull(elements);
			Contract.NotNull(comparer);

			Dictionary<TKey,Grouping<TKey,TElement>> items;

			if (elements is HashLookup<TKey, TElement> hl)
			{
				//TODO: check if 'comparer' and 'hl.m_comparer' are the same?
				items = new(hl.m_items, comparer);
			}
			else
			{
				items = new(elements.Count, comparer);
				foreach(var grp in elements)
				{
					items.Add(grp.Key, Grouping.Create(grp));
				}
			}
			m_items = items;
		}

		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items.Count;
		}

		public ICollection<TKey> Keys => m_items.Keys;

		public ICollection<Grouping<TKey, TElement>> Values => m_items.Values;

		public IEqualityComparer<TKey> Comparer => m_items.Comparer;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(TKey key) => m_items.ContainsKey(key);

		public IEnumerable<TElement> this[TKey key]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items[key];
		}

		/// <summary>Returns the grouping for the given key, or the default value</summary>
		/// <param name="key">Key of the grouping</param>
		/// <param name="missing">Value returned if there is no grouping with this key</param>
		/// <returns>Corresponding grouping, or the default value</returns>
		[ContractAnnotation("missing:notnull => notnull")]
		[return: NotNullIfNotNull("missing")]
		public Grouping<TKey, TElement>? GetValueOrDefault(TKey key, Grouping<TKey, TElement>? missing = null)
		{
			var grouping = GetGrouping(key, createIfMissing: false);
			return grouping ?? missing;
		}


		[ContractAnnotation("createIfMissing:true => notnull")]
		public Grouping<TKey, TElement>? GetGrouping(TKey key, bool createIfMissing)
		{
			if (m_items.TryGetValue(key, out var grouping))
			{
				return grouping;
			}

			if (!createIfMissing)
			{
				return null;
			}

			// pre-allocate space for at least one element
			grouping = new()
			{
				m_key = key,
				m_elements = [ ],
				m_count = 0,
			};

			m_items[key] = grouping;
			return grouping;
		}

		public Grouping<TKey, TElement> GetOrCreateGrouping(TKey key, out bool created)
		{
			if (m_items.TryGetValue(key, out var grouping))
			{
				created = false;
				return grouping;
			}

			// pre-allocate space for at least one element
			grouping = new()
			{
				m_key = key,
				m_elements = [ ],
				m_count = 0,
			};

			m_items[key] = grouping;
			created = true;
			return grouping;
		}

		public Grouping<TKey, TElement> AddOrUpdateGrouping(TKey key, TElement element, out bool created)
		{
			var grp = GetOrCreateGrouping(key, out created);
			grp.Add(element);
			return grp;
		}

		public Grouping<TKey, TElement> AddOrUpdateGrouping(TKey key, IEnumerable<TElement> elements, out bool created)
		{
			if (m_items.TryGetValue(key, out var grouping))
			{
				grouping.AddRange(elements);
				created = false;
				return grouping;
			}

			var t = elements.ToArray();
			grouping = new()
			{
				m_key = key,
				m_elements = t,
				m_count = t.Length,
			};

			m_items[key] = grouping;
			created = true;
			return grouping;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(TKey key, TElement element)
		{
			GetOrCreateGrouping(key, out _).Add(element);
		}

		/// <summary>Add multiple elements to the grouping with the specified key</summary>
		/// <param name="key">Key of the grouping</param>
		/// <param name="elements">Sequence of elements that will be added to this grouping</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddRange(TKey key, ReadOnlySpan<TElement> elements)
		{
			GetOrCreateGrouping(key, out _).AddRange(elements);
		}

		/// <summary>Add multiple elements to the grouping with the specified key</summary>
		/// <param name="key">Key of the grouping</param>
		/// <param name="elements">Sequence of elements that will be added to this grouping</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddRange(TKey key, TElement[]? elements)
		{
			GetOrCreateGrouping(key, out _).AddRange(elements);
		}

		/// <summary>Add multiple elements to the grouping with the specified key</summary>
		/// <param name="key">Key of the grouping</param>
		/// <param name="elements">Sequence of elements that will be added to this grouping</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddRange(TKey key, IEnumerable<TElement> elements)
		{
			GetOrCreateGrouping(key, out _).AddRange(elements);
		}

		/// <summary>Remove the grouping with the specified key from the lookup table</summary>
		/// <param name="key">Key of the grouping to remove</param>
		public bool Remove(TKey key) => m_items.Remove(key);

		/// <summary>Remove an element from the lookup table</summary>
		/// <param name="key">Key of the grouping containing the element</param>
		/// <param name="element">Element to remove</param>
		/// <param name="cleanupIfEmpty">If <c>true</c> and this was the last element in the grouping, it will be removed from the table; otherwise, the empty grouping will be kept.</param>
		/// <returns><c>true</c> if a matching element was removed; otherwise, <c>false</c></returns>
		public bool Remove(TKey key, TElement element, bool cleanupIfEmpty = false)
		{
			var grouping = GetGrouping(key, false);

			bool res = false;
			if (grouping != null)
			{
				res = grouping.Remove(element);
				if (cleanupIfEmpty && grouping.Count == 0)
				{ // remove empty grouping?
					m_items.Remove(key);
				}
			}
			return res;
		}

		/// <summary>Clears all groupings</summary>
		public void Clear() => m_items.Clear();

		/// <summary>Returns the grouping for the specified key, if it exists</summary>
		/// <param name="key">Key of the grouping</param>
		/// <param name="elements">Receives the corresponding grouping</param>
		/// <returns><c>true</c> if the grouping was found; otherwise, <c>false</c></returns>
		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out Grouping<TKey, TElement> elements)
		{
			elements = GetGrouping(key, false);
			return elements != null;
		}

		/// <summary>Executes an action on each non-empty grouping in this table.</summary>
		/// <param name="handler">Called with each grouping with at least one element. The 3rd argument is the number of items in the array.</param>
		/// <returns>Number of grouping that were processed</returns>
		public int ForEach(Action<TKey, TElement[], int> handler)
		{
			Contract.NotNull(handler);

			int count = 0;
			foreach (var kvp in m_items.Values)
			{
				if (kvp.m_count == 0) continue;

				handler(kvp.Key, kvp.m_elements, kvp.m_count);
				++count;
			}
			return count;
		}

		/// <summary>Executes an action on each non-empty grouping in this table.</summary>
		/// <param name="handler">Called with each grouping with at least one element.</param>
		/// <returns>Number of grouping that were processed</returns>
		public int ForEach(Action<TKey, ReadOnlyMemory<TElement>> handler)
		{
			Contract.NotNull(handler);

			int count = 0;
			foreach (var kvp in m_items.Values)
			{
				if (kvp.m_count == 0) continue;

				handler(kvp.Key, kvp.m_elements.AsMemory(0, kvp.m_count));
				++count;
			}
			return count;
		}

		#region IEnumerator<...>

		public Dictionary<TKey, Grouping<TKey, TElement>>.ValueCollection.Enumerator GetEnumerator()
		{
			return m_items.Values.GetEnumerator();
		}

		IEnumerator<Grouping<TKey, TElement>> IEnumerable<Grouping<TKey, TElement>>.GetEnumerator()
		{
			return m_items.Values.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return m_items.Values.GetEnumerator();
		}

		IEnumerator<IGrouping<TKey, TElement>> IEnumerable<IGrouping<TKey, TElement>>.GetEnumerator()
		{
			foreach (var grouping in m_items.Values)
			{
				yield return grouping;
			}
		}

		#endregion

	}

}
