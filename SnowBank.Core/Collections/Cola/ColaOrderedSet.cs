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
	using System.Buffers;
	using System.Runtime.InteropServices;

	/// <summary>Represent an ordered set of elements, stored in a Cache Oblivious Lookup Array</summary>
	/// <typeparam name="T">Type of elements stored in the set</typeparam>
	/// <remarks>Inserts are in O(LogN) amortized. Lookups are in O(Log(N))</remarks>
	[PublicAPI]
	[DebuggerDisplay("Count={m_items.Count}"), DebuggerTypeProxy(typeof(ColaOrderedSet<>.DebugView))]
	public class ColaOrderedSet<T> : IEnumerable<T>, IDisposable
	{
		private const int NOT_FOUND = -1;

		/// <summary>COLA array used to store the elements in the set</summary>
		private readonly ColaStore<T> m_items;

		private volatile int m_version;

		#region Constructors...

		/// <summary>Constructs a <see cref="ColaOrderedSet{T}"/></summary>
		/// <param name="pool">Pool used to allocate the internal levels (optional)</param>
		public ColaOrderedSet(ArrayPool<T>? pool = null)
			: this(0, Comparer<T>.Default, pool)
		{ }

		/// <summary>Constructs a <see cref="ColaOrderedSet{T}"/> with the given initial capacity</summary>
		/// <param name="capacity">Initial capacity</param>
		/// <param name="pool">Pool used to allocate the internal levels (optional)</param>
		public ColaOrderedSet(int capacity, ArrayPool<T>? pool = null)
			: this(capacity, Comparer<T>.Default, pool)
		{ }

		/// <summary>Constructs a <see cref="ColaOrderedSet{T}"/> with the given comparer</summary>
		/// <param name="comparer">Value comparer</param>
		/// <param name="pool">Pool used to allocate the internal levels (optional)</param>
		public ColaOrderedSet(IComparer<T>? comparer, ArrayPool<T>? pool = null)
			: this(0, comparer, pool)
		{ }

		/// <summary>Constructs a <see cref="ColaOrderedSet{T}"/> with the given initial capacity and comparer</summary>
		/// <param name="capacity">Initial capacity</param>
		/// <param name="comparer">Value comparer</param>
		/// <param name="pool">Pool used to allocate the internal levels (optional)</param>
		public ColaOrderedSet(int capacity, IComparer<T>? comparer, ArrayPool<T>? pool = null)
		{
			Contract.Positive(capacity);
			m_items = new(capacity, comparer ?? Comparer<T>.Default, pool);
		}

		#endregion

		#region Public Properties...

		/// <summary>Gets the number of elements in the immutable sorted set.</summary>
		public int Count => m_items.Count;

		/// <summary>Current capacity of the set</summary>
		public int Capacity => m_items.Capacity;

		/// <summary>Instance used to compare values in the set</summary>
		public IComparer<T> Comparer => m_items.Comparer;

		/// <summary>Returns the value at the given index</summary>
		/// <param name="index">Index (0-based) of the value, using the natural ordering of the set</param>
		/// <returns>Corresponding value</returns>
		/// <exception cref="IndexOutOfRangeException"> <paramref name="index"/> is out of bounds</exception>
		public T this[int index]
		{
			get
			{
				if (index < 0 || index >= m_items.Count) ThrowIndexOutOfRangeException();
				var (level, offset) = ColaStore.MapOffsetToLocation(m_items.Count, index);
				if (level < 0) throw new IndexOutOfRangeException();
				return m_items.GetReference(level, offset);
			}
		}

		[DoesNotReturn, StackTraceHidden]
		private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException("Index is out of range");

		#endregion

		#region Public Methods...

		/// <inheritdoc />
		public void Dispose()
		{
			m_version = int.MinValue;
			m_items.Dispose();
		}

		/// <summary>Removes all the elements in the set</summary>
		public void Clear()
		{
			Interlocked.Increment(ref m_version);
			m_items.Clear();
		}

		/// <summary>Adds the specified value to this ordered set.</summary>
		/// <param name="value">The value to add.</param>
		/// <remarks>If the value already exists in the set, it will not be overwritten</remarks>
		public bool Add(T value)
		{
			Interlocked.Increment(ref m_version);
			if (!m_items.SetOrAdd(value, overwriteExistingValue: false))
			{
				Interlocked.Decrement(ref m_version);
				return false;
			}
			return true;
		}

		/// <summary>Adds or overwrite the specified value to this ordered set.</summary>
		/// <param name="value">The value to add.</param>
		/// <remarks>If the value already exists in the set, it will be overwritten by <paramref name="value"/></remarks>
		public bool Set(T value)
		{
			Interlocked.Increment(ref m_version);
			return m_items.SetOrAdd(value, overwriteExistingValue: true);
		}

		/// <summary>Removes the specified value from the set, if it exists.</summary>
		/// <param name="value">The value to remove.</param>
		/// <param name="actualValue">Receives the actual value that was removed.</param>
		/// <returns><c>true</c> if the value was found and removed; otherwise, <c>false</c></returns>
		public bool TryRemove(T value, [MaybeNullWhen(false)] out T actualValue)
		{
			ref var slot = ref m_items.Find(value, out var level, out var offset);
			if (!Unsafe.IsNullRef(ref slot))
			{
				actualValue = slot;
				Interlocked.Increment(ref m_version);
				m_items.RemoveAt(level, offset);
				return true;
			}

			actualValue = default!;
			return false;
		}

		/// <summary>Removes the specified value from the set.</summary>
		/// <param name="value">The value to remove.</param>
		/// <returns><c>true</c> if the value was found and removed; otherwise, <c>false</c></returns>
		public bool Remove(T value)
		{
			return TryRemove(value, out _);
		}

		/// <summary>Removes the value at the specified location</summary>
		/// <param name="arrayIndex">Index (0-based) of the value to remove, using the natural order of the set</param>
		/// <returns>Value that was removed</returns>
		/// <exception cref="ArgumentOutOfRangeException"> <paramref name="arrayIndex"/> if outside the bounds of the set</exception>
		public T RemoveAt(int arrayIndex)
		{
			if (arrayIndex < 0 || arrayIndex >= m_items.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Index is outside the array");
			}

			var (level, offset) = ColaStore.MapOffsetToLocation(m_items.Count, arrayIndex);
			Contract.Debug.Assert(level >= 0 && offset >= 0 && offset < 1 << level);

			Interlocked.Increment(ref m_version);
			return m_items.RemoveAt(level, offset);
		}

		/// <summary>Determines whether this immutable sorted set contains the specified value.</summary>
		/// <param name="value">The value to check for.</param>
		/// <returns><c>true</c> if the set contains the specified value; otherwise, <c>false</c>.</returns>
		public bool Contains(T value)
		{
			return !Unsafe.IsNullRef(ref m_items.Find(value, out _,out _));
		}

		/// <summary>Find an element </summary>
		/// <param name="value"></param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="value"/> within the entire list, if found; otherwise, –1.</returns>
		public int IndexOf(T value)
		{
			if (!Unsafe.IsNullRef(ref m_items.Find(value, out var level, out var offset)))
			{
				return ColaStore.MapLocationToOffset(m_items.Count, level, offset);
			}
			return NOT_FOUND;
		}

		/// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
		/// <param name="value">The value to search for.</param>
		/// <param name="actualValue">The value from the set that the search found, or the original value if the search yielded no match.</param>
		/// <returns>A value indicating whether the search was successful.</returns>
		public bool TryGetValue(T value, [MaybeNullWhen(false)] out T actualValue)
		{
			ref var slot = ref m_items.Find(value, out _, out _);

			if (Unsafe.IsNullRef(ref slot))
			{
				actualValue = default;
				return false;
			}

			actualValue = slot;
			return true;
		}

		/// <summary>Copy the ordered elements of the set to an array</summary>
		/// <param name="destination">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
		/// <exception cref="ArgumentException">Thrown when the destination Span is too small to receive all the items.</exception>
		public void CopyTo(Span<T> destination)
		{
			m_items.CopyTo(destination);
		}

		/// <summary>Copy the ordered elements of the set to an array</summary>
		/// <param name="destination">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		public bool TryCopyTo(Span<T> destination)
		{
			return m_items.TryCopyTo(destination);
		}

		/// <summary>Copy the ordered elements of the set to an array</summary>
		/// <param name="destination">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
		/// <exception cref="ArgumentException">Thrown when the destination Span is too small to receive all the items.</exception>
		public void CopyTo(T[] destination)
		{
			Contract.Debug.Requires(destination != null);
			m_items.CopyTo(destination);
		}

		/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
		public ColaStore.Enumerator<T> GetEnumerator() => new(m_items, reverse: false);

		/// <inheritdoc />
		IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

		#endregion

		/// <summary>Writes the contents of this set into a log, for debugging purpose [DEBUG ONLY]</summary>
		[Conditional("DEBUG")]
		public void Debug_Dump(TextWriter output)
		{
#if DEBUG
			output.WriteLine($"Dumping ColaOrderedSet<{typeof(T).Name}> filled at {(100.0d * this.Count / this.Capacity):N2}%");
			m_items.Debug_Dump(output);
#endif
		}

		/// <summary>Debug view helper</summary>
		private sealed class DebugView
		{

			private readonly ColaOrderedSet<T> m_set;

			public DebugView(ColaOrderedSet<T> set)
			{
				m_set = set;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public T[] Items
			{
				get
				{
					var tmp = new T[m_set.Count];
					m_set.CopyTo(tmp);
					return tmp;
				}
			}
		}

		/// <summary>Enumerates the elements stored in a <see cref="ColaOrderedSet{T}"/></summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<T>
		{

			private readonly int m_version;

			private readonly ColaOrderedSet<T> m_parent;

			private ColaStore.Enumerator<T> m_iterator;

			internal Enumerator(ColaOrderedSet<T> parent, bool reverse)
			{
				m_version = parent.m_version;
				m_parent = parent;
				m_iterator = new(parent.m_items, reverse);
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				if (m_version != m_parent.m_version)
				{
					throw ColaStore.ErrorStoreVersionChanged();
				}

				return m_iterator.MoveNext();
			}

			/// <inheritdoc />
			public readonly T Current => m_iterator.Current;

			/// <inheritdoc />
			public void Dispose()
			{
				// we are a struct that can be copied by value, so there is no guarantee that Dispose() will accomplish anything anyway...
			}

			object? System.Collections.IEnumerator.Current => m_iterator.Current;

			void System.Collections.IEnumerator.Reset()
			{
				if (m_version != m_parent.m_version)
				{
					throw ColaStore.ErrorStoreVersionChanged();
				}
				m_iterator = new(m_parent.m_items, m_iterator.Reverse);
			}

		}

	}

}
