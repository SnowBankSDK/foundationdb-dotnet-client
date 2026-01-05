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
	using System.Numerics;
	using System.Runtime;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers;

	/// <summary>Container for a set of elements that share the same key</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TElement">Type of elements</typeparam>
	[PublicAPI]
	[DebuggerDisplay("Key={m_key} Count={m_count}")]
	public sealed class Grouping<TKey, TElement> : IGrouping<TKey, TElement>, IList<TElement>
	{
		// IMPORTANT: contrary to the LINQ implementation (readonly), the items can be MODIFIED!
		// => it is possible to add/remove/filter values after the grouping has been created.
		// => it is not thread-safe!

		#region Private Members...

		// note: these values are modified by the caller

		/// <summary>Key of this grouping</summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		internal TKey m_key;

		/// <summary>Buffer that contains the elements in this grouping.</summary>
		/// <remarks>Number of actual elements is given by <see cref="m_count"/>. The tail of the buffer is unused.</remarks>
		internal TElement[] m_elements;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary>Number of entries in the array</summary>
		internal int m_count;

		//REVIEW: use ReadOnlyMemory<TElement> instead?

		#endregion

		#region Public Properties...

		/// <summary>Get the key of the <see cref="Grouping{TKey,TElement}"/></summary>
		public TKey Key
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_key;
		}

		/// <summary>Get the number of elements contained in the <see cref="Grouping{TKey,TElement}"/></summary>
		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count;
		}

		/// <summary>Get the first element of the grouping (or default(T) if empty)</summary>
		public TElement? First
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count > 0 ? m_elements[0] : default;
		}

		/// <summary>Get the last element of the grouping (or default(T) if empty)</summary>
		public TElement? Last
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count > 0 ? m_elements[m_count - 1] : default;
		}

		public TElement this[int index]
		{
			get
			{
				if (index < 0 || index >= m_count) ThrowHelper.ThrowArgumentOutOfRangeIndex(index);
				return m_elements[index];
			}
			[DoesNotReturn]
			set => throw new NotSupportedException();
		}

		#endregion

		#region Public Methods...

		/// <summary>Add an item to this grouping</summary>
		/// <param name="element"></param>
		public void Add(TElement element)
		{
			int count = m_count;
			if (count == m_elements.Length)
			{
				// note: may throw if count is > 2^29 due to the max size of .NET objects in memory!
				Array.Resize(ref m_elements, Math.Max(1, checked(count * 2)));
			}
			m_elements[count] = element;
			m_count = count + 1;
		}

		private void EnsureCapacity(int capacity)
		{
			Contract.Positive(capacity);
			if (m_elements.Length < capacity)
			{ // too small, must resize
				capacity = unchecked((uint) capacity) <= (1U << 30) ? unchecked((int) BitOperations.RoundUpToPowerOf2((uint) capacity)) : capacity;
				Array.Resize(ref m_elements, capacity);
			}
		}

		/// <summary>Adds a batch of elements to this grouping</summary>
		/// <param name="elements">Span of elements (can be empty)</param>
		public void AddRange(ReadOnlySpan<TElement> elements)
		{
			int n = elements.Length;
			if (n == 0) return;

			// Ensure that we have enough capacity for the new elements
			EnsureCapacity(m_count + n);
			elements.CopyTo(m_elements.AsSpan(m_count));
			m_count += n;
		}

		/// <summary>Adds a batch of elements to this grouping</summary>
		/// <param name="elements">Array of elements (can be null or empty)</param>
		public void AddRange(TElement[]? elements)
		{
			if (elements != null)
			{
				AddRange(new ReadOnlySpan<TElement>(elements));
			}
		}

		/// <summary>Adds a batch of elements to this grouping</summary>
		/// <param name="elements">Sequence of elements (can be empty)</param>
		public void AddRange(IEnumerable<TElement> elements)
		{
			if (elements.TryGetSpan(out var span))
			{ // we know the length, we can resize the buffer to be large enough in one step.
				AddRange(span);
			}
			else if (elements is Grouping<TKey, TElement> grouping)
			{
				AddRange(grouping);
			}
			else
			{ // we don't know the number, we have to add them one by one, and maybe need multiple buffer resize
#if NET6_0_OR_GREATER
				if (elements.TryGetNonEnumeratedCount(out var count))
				{
					EnsureCapacity(m_count + count);
				}
#endif
				foreach (var element in elements)
				{
					Add(element);
				}
			}
		}

		/// <summary>Adds all the elements of another grouping to this grouping</summary>
		/// <param name="grouping">Grouping that must be merged with this one</param>
		/// <remarks>No attempt will be made to de-dup the items. If the both grouping contain the same item, it will be duplicated!</remarks>
		public void AddRange(Grouping<TKey, TElement> grouping)
		{
			Contract.NotNull(grouping);

			int n = grouping.m_count;
			if (n == 0) return;

			EnsureCapacity(m_count + n);
			grouping.Span.CopyTo(m_elements.AsSpan(m_count));
			m_count += n;
		}

		/// <summary>Removes an element from this grouping</summary>
		/// <param name="element">Element to remove</param>
		/// <returns><c>true</c> if the element was present and has been removed; otherwise, <c>false</c></returns>
		/// <remarks>Uses <c>EqualityComparer&lt;T&gt;.Default</c> to compare the elements.
		/// Warning: will only remove the first occurrence found, in case of duplicates!</remarks>
		public bool Remove(TElement element)
		{
			if (m_count >= 0)
			{
				int index = Array.IndexOf<TElement>(m_elements, element, 0, m_count);
				if (index >= 0)
				{
					RemoveAtInternal(index);
					return true;
				}
			}
			return false;
		}

		/// <summary>Removes an element from this grouping</summary>
		/// <param name="element">Element to remove</param>
		/// <param name="comparer">Comparer used to find the element</param>
		/// <returns><c>true</c> if the element was present and has been removed; otherwise, <c>false</c></returns>
		/// <remarks>Warning: will only remove the first occurrence found, in case of duplicates!</remarks>
		public bool Remove(TElement element, IEqualityComparer<TElement> comparer)
		{
			Contract.NotNull(comparer);

			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (comparer.Equals(element, elements[i]))
				{
					RemoveAtInternal(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>Removes the element at the specified position</summary>
		public void RemoveAt(int index)
		{
			if ((uint) index >= m_count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(index));
			RemoveAtInternal(index);
		}

		/// <summary>Removes and return the last element of the grouping</summary>
		/// <returns>Last element</returns>
		/// <exception cref="System.InvalidOperationException">If the grouping was already empty</exception>
		public TElement Pop()
		{
			if (m_count == 0) throw new InvalidOperationException("Cannot remove last item from an empty grouping");
			int p = m_count - 1;
			var result = m_elements[p];
			RemoveAtInternal(p);
			return result;
		}

		/// <summary>Updates the last element, or add it if the grouping was empty</summary>
		/// <param name="newValue">New value for the last element</param>
		public void UpdateLast(TElement newValue)
		{
			if (m_count == 0)
			{
				Add(newValue);
			}
			else
			{
				m_elements[m_count - 1] = newValue;
			}
		}

		/// <summary>Clears all elements from this grouping</summary>
		public void Clear()
		{
			if (m_count > 0)
			{
				if (m_elements.Length <= 8)
				{ // reuse the buffer
					m_elements.AsSpan(0, m_count).Clear();
				}
				else
				{
					m_elements = [ ];
				}
				m_count = 0;
			}
		}

		/// <summary>Remove all elements that match the specified predicate</summary>
		/// <param name="match">Predicate that returns <c>true</c> for each element to remove, and <c>false</c> for elements to keep.</param>
		/// <remarks>Number of removed elements (0 if grouping was empty, or no match found)</remarks>
		public int RemoveAll(Func<TElement, bool> match)
		{
			Contract.NotNull(match);

			// look for the first match
			// => any elements before will be kept
			int count = m_count;
			var elements = m_elements;
			int index = 0;
			while ((index < count) && !match(elements[index]))
			{ // keep this element
				index++;
			}

			if (index >= count)
			{ // nothing to remove
				return 0;
			}

			// advance through the buffer, and shift all chunks of elements that are kept towards the start of the buffer.
			int cursor = index + 1;
			while (cursor < count)
			{
				while ((cursor < count) && match(elements[cursor]))
				{
					cursor++;
				}
				if (cursor < count)
				{
					elements[index++] = elements[cursor++];
				}
			}

			// clear the tail of the buffer
			Array.Clear(elements, index, count - index);
			int deleted = count - index;
			m_count = index;
			ShrinkIfNeeded();
			return deleted;
		}

		/// <summary>Remove the element at the given position</summary>
		private void RemoveAtInternal(int index)
		{
			var elements = m_elements.AsSpan(0, m_count);
			int after = elements.Length - index - 1;
			if (after > 0)
			{ // shift all items after that one to the left
				elements[(index + 1)..].CopyTo(elements[index..]);
			}

			// empty the last slot to help the GC
			elements[^1] = default!;

			--m_count;
			ShrinkIfNeeded();
		}

		/// <summary>Maybe reduce the size of the buffer, if possible</summary>
		/// <remarks>Shrink the length by 2 if less than 1/8th is used</remarks>
		private void ShrinkIfNeeded()
		{
			// if less than 1/8th is allocated, reduce the buffer size by 50%
			int n = m_elements.Length;
			if (m_count < (n >> 3) && n > 1)
			{
				Array.Resize(ref m_elements, n >> 1);
			}
		}

		public int IndexOf(Func<TElement, bool> match)
		{
			var elements = this.Span;
			for (int i = 0; i < elements.Length; i++)
			{
				if (match(elements[i])) return i;
			}
			return -1;
		}

		public ReadOnlySpan<TElement> Span
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_elements.AsSpan(0, m_count);
		}

		public ReadOnlyMemory<TElement> Memory
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_elements.AsMemory(0, m_count);
		}

		public TElement[] ToArray() => this.Span.ToArray();

		public List<TElement> ToList()
		{
			int count = m_count;
		
			var list = new List<TElement>(count);
#if NET8_0_OR_GREATER
			CollectionsMarshal.SetCount(list, count);
			this.Span.CopyTo(CollectionsMarshal.AsSpan(list));
			return list;
#else
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				list.Add(elements[i]);
			}
			return list;
#endif
		}

		/// <summary>Runs an action on each element in this grouping</summary>
		/// <param name="action">Lambda that will be called with each element, one by one</param>
		public void ForEach(Action<TElement> action)
		{
			foreach(var element in this.Span)
			{
				action(element);
			}
		}

		/// <summary>Runs an action on each element in this grouping</summary>
		/// <param name="action">Lambda that will be called with each element, one by one</param>
		public void ForEach(Action<TKey, TElement> action)
		{
			var key = m_key;
			foreach(var element in this.Span)
			{
				action(key, element);
			}
		}

		/// <summary>Converts the grouping into a sequence of key/value pairs</summary>
		/// <param name="selector">Optional filter (return <c>true</c> for elements to keep, and <c>false</c> for elements to discard)</param>
		/// <returns>Sequence of KeyValuePair where Key is the grouping's key, and Value is an element of this grouping</returns>
		public IEnumerable<KeyValuePair<TKey, TElement>> Map(Func<TElement, bool>? selector = null)
		{
			int count = m_count;
			var elements = m_elements;
			var key = m_key;
			for (int i = 0; i < count; i++)
			{
				if (selector == null || selector(elements[i]))
				{
					yield return new(key, elements[i]);
				}
			}
		}

		#endregion

		#region IEnumerable<TElement> ...

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<TElement>
		{
			private readonly Grouping<TKey, TElement> m_grouping;
			private int m_index;
			private TElement m_current;

			internal Enumerator(Grouping<TKey, TElement> grouping)
			{
				m_grouping = grouping;
				m_index = 0;
				m_current = default!;
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				var grouping = m_grouping;
				if (m_index < grouping.m_count)
				{
					m_current = grouping.m_elements[m_index];
					++m_index;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				m_index = m_grouping.m_count + 1;
				m_current = default!;
				return false;
			}

			/// <inheritdoc />
			public readonly TElement Current
			{
				[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
				[return: MaybeNull]
				get => m_current;
			}

			object? System.Collections.IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index == (m_grouping.Count + 1))
					{
						ThrowEnumOpCantHappen();
					}
					return m_current;
				}
			}

			[DoesNotReturn, StackTraceHidden]
			private static void ThrowEnumOpCantHappen()
			{
				throw new InvalidOperationException("Enumeration has either not started or has already finished.");
			}

			void System.Collections.IEnumerator.Reset()
			{
				m_index = 0;
				m_current = default!;
			}

			/// <inheritdoc />
			public void Dispose()
			{
				//NOP
			}
		}

		/// <summary>Returns an enumerator that iterates through the grouping.</summary>
		public Enumerator GetEnumerator()
			=> new(this);

		IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
			=> new Enumerator(this);

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> new Enumerator(this);

		#endregion

		#region ICollection<TElement> ...

		bool ICollection<TElement>.Contains(TElement item)
		{
			for (int i = 0; i < m_count; i++)
			{
				if (object.Equals(item, m_elements[i])) return true;
			}
			return false;
		}

		/// <summary>Copy the elements of this grouping into an array</summary>
		/// <remarks>The destinatino array must be large enough to receive all elements.</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="array"/> is <c>null</c></exception>
		/// <exception cref="System.InvalidOperationException">If the target array is not large enough, or <paramref name="arrayIndex"/> is negative</exception>
		public void CopyTo(TElement[] array, int arrayIndex)
		{
			Array.Copy(m_elements, 0, array, arrayIndex, m_count);
		}

		bool ICollection<TElement>.IsReadOnly => false;

		#endregion

		#region IList<TElement> ...

		int IList<TElement>.IndexOf(TElement item)
		{
			return Array.IndexOf(m_elements, item, 0, m_count);
		}

		void IList<TElement>.Insert(int index, TElement item)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region Pseudo-LINQ

		public bool Any(Func<TElement, bool> predicate)
		{
			int count = m_count;
			if (count == 0) return false;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (predicate(elements[i])) return true;
			}
			return false;
		}

		public bool All(Func<TElement, bool> predicate)
		{
			int count = m_count;
			if (count == 0) return false;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (!predicate(elements[i])) return false;
			}
			return true;
		}

		public IEnumerable<TElement> Where(Func<TElement, bool> predicate)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				var element = elements[i];
				if (predicate(element)) yield return element;
			}
		}

		#endregion

		[PublicAPI]
		public sealed class EqualityComparer : IEqualityComparer<Grouping<TKey, TElement>>, IEqualityComparer<TKey>
		{
			private readonly IEqualityComparer<TKey> m_comparer;

			public EqualityComparer()
			{
				m_comparer = EqualityComparer<TKey>.Default;
			}

			public EqualityComparer(IEqualityComparer<TKey> comparer)
			{
				Contract.NotNull(comparer);
				m_comparer = comparer;
			}

			public bool Equals(TKey? x, TKey? y) => m_comparer.Equals(x, y);

			public int GetHashCode(TKey obj) => m_comparer.GetHashCode(obj!);

			public bool Equals(Grouping<TKey, TElement>? x, Grouping<TKey, TElement>? y) => x == null ? y == null : y != null && m_comparer.Equals(x.Key, y.Key);

			public int GetHashCode(Grouping<TKey, TElement>? obj) => obj == null ? -1 : m_comparer.GetHashCode(obj.Key!);
		}

		[PublicAPI]
		public sealed class Comparer : IComparer<Grouping<TKey, TElement>>, IComparer<TKey>
		{
			public static readonly IComparer<Grouping<TKey, TElement>> Default = new Comparer();

			private readonly IComparer<TKey> m_comparer;

			public Comparer()
			{
				m_comparer = Comparer<TKey>.Default;
			}

			public Comparer(IComparer<TKey> comparer)
			{
				Contract.NotNull(comparer);
				m_comparer = comparer;
			}

			public int Compare(TKey? x, TKey? y) => m_comparer.Compare(x, y);

			public int Compare(Grouping<TKey, TElement>? x, Grouping<TKey, TElement>? y)
				=> x == null ? (y == null ? 0 : -1)
				 : y == null ? +1
				 : m_comparer.Compare(x.Key, y.Key);

		}

	}

	[PublicAPI]
	public static class Grouping
	{

		/// <summary>Create a new grouping that contains a single element</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="element">Single element that will be stored in the grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, TElement element) => new()
		{
			m_key = key,
			m_elements = [ element ],
			m_count = 1
		};

		/// <summary>Creates a new grouping from an already allocated array of elements</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="elements">Array of elements that will be stored in the grouping</param>
		/// <remarks>The grouping will use the specified array as the backing store. This array should not be modified or returned into a pool, as long as the grouping is used!</remarks>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, params TElement[] elements) => new()
		{
			m_key = key,
			m_elements = elements,
			m_count = elements.Length
		};

		/// <summary>Creates a new grouping from a collection of elements</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="elements">Collection of elements that will be stored in the grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, ICollection<TElement> elements) => new()
		{
			m_key = key,
			m_elements = elements.ToArray(),
			m_count = elements.Count
		};

		/// <summary>Creates a new grouping from a sequence of elements</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="elements">Sequence of elements that will be stored in the grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
		{
			var t = elements.ToArray();
			return new()
			{
				m_key = key,
				m_elements = t,
				m_count = t.Length
			};
		}

		/// <summary>Creates a new grouping from an existing <see cref="IGrouping{TKey,TElement}"/></summary>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(IGrouping<TKey, TElement> grouping)
		{
			var t = grouping.ToArray();
			return new()
			{
				m_key = grouping.Key,
				m_elements = t,
				m_count = t.Length
			};
		}

		/// <summary>Creates a new grouping from a key/value pair singleton</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, TElement> pair)
		{
			return new()
			{
				m_key = pair.Key,
				m_elements = [ pair.Value ],
				m_count = 1
			};
		}

		/// <summary>Creates a new grouping from a key/values pair</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, TElement[]> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Creates a new grouping from a key/values pair</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, ICollection<TElement>> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Creates a new grouping from a key/values pair</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, IEnumerable<TElement>> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Converts from a LINQ grouping</summary>
		[ContractAnnotation("null => null; notnull => notnull")]
		public static Grouping<TKey, TElement>? FromLinq<TKey, TElement>(IGrouping<TKey, TElement>? grouping)
		{
			return grouping != null ? (grouping as Grouping<TKey, TElement>) ?? Create(grouping) : null;
		}

	}

}
