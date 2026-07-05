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

// enables consistency checks after each operation to the set
//#define ENFORCE_INVARIANTS

namespace SnowBank.Collections.CacheOblivious
{
	using System.Buffers;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers;

	/// <summary>Store elements in a list of ordered levels</summary>
	/// <typeparam name="T">Type of elements stored in the set</typeparam>
	[PublicAPI]
	[DebuggerDisplay("Count={m_count}, Depth={Depth}")]
	public sealed class ColaStore<T> : IDisposable
	{

		#region Documentation

		// Based on http://supertech.csail.mit.edu/papers/sbtree.pdf (COLA)

		/*
			The cache-oblivious lookahead array (COLA) is similar to the binomial list structure [9] of Bentley and Saxe. It consists of ⌈log2 N⌉ arrays,
			or levels, each of which is either completely full or completely empty. The kth array is of size 2^k and the arrays are stored contiguously in memory.

			The COLA maintains the following invariants:
			1. The kth array contains items if and only if the kth least-significant bit of the binary representation of N is a 1.
			2. Each array contains its items in ascending order by key
		 */

		// DEFINITIONS
		//
		// "Level" is the index in the list of segments with level 0 being the top (or root)
		// "Segment" is an array whose length is equal to 2^i (where i is the "level" of the segment).
		// "Doubling Array" means that each segment has double the length of its predecessor
		// "Cache Oblivious" means that the algorithm is not tuned for a specific CPU cache size (L1, L2, ou block size on disk), and amortize the cost of insertion over the lifespan of the set.
		//
		// INVARIANTS:
		//
		// * Each segment is twice the size of the previous segment, i.e.: m_levels[i].Length == 1 << i
		//		0 [ ] 1
		//		1 [ , ] 2
		//		2 [ , , , ] 4
		//		3 [ , , , , , , , ] 8
		//		4 [ , , , , , , , , , , , , , , , ] 16
		//		...
		// * A segment is either EMPTY, or completely FULL
		//		legal:		[ , , , ] or [1,2,3,4]
		//		illegal:	[1,2,3, ]
		// * A segment has all its elements sorted
		//		legal:		[3,12,42,66]
		//		illegal:	[12,66,42,3]
		//
		// NOTES:
		//
		// - 50% of all inserts will always be done on the root (level 0), so will be O(1)
		// - 87.5% of all inserts will only touch levels 0, 1 and 2, which should be contiguous in memory
		// - For random insertions, it is difficult to predict in which level a specific value will be found, except that older values are towards the bottom, and younger values are towards the top.
		// - A range of values (ex: "from 10 to 20") can have its elements scattered in multiple segments
		// - If all inserts are ordered, then all items of level N will be sorted after all the items of level N + 1
		// - Most inserts are usually pretty fast, but every times the count goes to the next power of 2, the duration will be more and more noticeable (ie : the (2^N)th INSERT will have to merge (2^N) values)
		//
		// COST
		//
		// The cost for inserting N values is about N.Log2(N) comparisons
		// - This is amortized to Log2(N) per insert, which means that insertion is O(log(N))
		// - This means that N should stay relatively low (ideally under 2^10 items)

		#endregion

		/// <summary>Number of elements in the store</summary>
		private volatile int m_count;

		/// <summary>Number of levels currently allocated</summary>
		private int m_allocatedLevels;

		private T[] m_root;

#if NET8_0_OR_GREATER
		private Scratch m_scratch;
#else
		private T[] m_scratch; // used to perform temp operations with up to 2 items
#endif

		/// <summary>Key comparer</summary>
		private readonly IComparer<T> m_comparer;

		private readonly ArrayPool<T> m_pool;

		/// <summary>Array of all the segments making up the levels</summary>
#if NET8_0_OR_GREATER
		private LevelsStack m_levels;
#else
		private T[]?[] m_levels;
#endif

#if NET8_0_OR_GREATER

		[InlineArray(2)]
		private struct Scratch
		{
			private T Item;
		}

		[InlineArray(ColaStore.MAX_LEVEL + 1)]
		private struct LevelsStack
		{
			private T[]? Item;
		}

#endif

		#region Constructors...

		/// <summary>Allocates a new store</summary>
		/// <param name="capacity">Initial capacity, or 0 for the default capacity</param>
		/// <param name="comparer">Comparer used to order the elements</param>
		/// <param name="pool">Pool used to allocate levels</param>
		public ColaStore(int capacity, IComparer<T> comparer, ArrayPool<T>? pool = null)
		{
			Contract.Positive(capacity);
			Contract.NotNull(comparer);

			pool ??= ArrayPool<T>.Shared;
			m_pool = pool;

			// how many levels required?

			m_root = new T[1];
#if !NET8_0_OR_GREATER
			// allocate on the heap
			m_levels = new T[ColaStore.MAX_LEVEL + 1][];
			m_scratch = new T[2];
#endif
			int levels = Math.Max(ColaStore.GetLevelCount(capacity), ColaStore.INITIAL_LEVELS);
			m_allocatedLevels = levels;

			// pre-allocate the segments and spares at the same time, so that they are always at the same memory location
			Span<T[]?> segments = m_levels;
			segments[0] = m_root;
			for (int i = 1; i < levels; i++)
			{
				(segments[i], _) = RentLevel(i);
			}

			m_comparer = comparer;
			CheckInvariants();
		}

		/// <summary>Constructs a <see cref="ColaStore{T}"/> with a copy of the contents of another store</summary>
		/// <param name="source">Store to use as the source</param>
		/// <remarks>Adding or removing elements to this new instance will not impact the source, and vice versa.</remarks>
		public ColaStore(ColaStore<T> source)
		{
			m_pool = source.m_pool;
			m_count = source.m_count;

			// copy the levels from the original
			var levels = Math.Max(ColaStore.GetLevelCount(m_count), ColaStore.INITIAL_LEVELS);

#if NET8_0_OR_GREATER
			// levels already initialized
#else
			m_levels = new T[ColaStore.MAX_LEVEL + 1][];
			m_scratch = new T[2];
#endif
			m_root = source.m_root.ToArray();
			m_levels[0] = m_root;
			for (int i = 1; i < levels; i++)
			{
				var (tmp, _) = RentLevel(i);
				if (!IsFree(i))
				{
					source.GetLevel(i).CopyTo(tmp);
				}
				m_levels[i] = tmp;
			}

			m_allocatedLevels = levels;
			m_comparer = source.m_comparer;
		}

		/// <summary>Returns a copy of this store</summary>
		/// <returns>New store with a copy of the contents of this instance</returns>
		/// <remarks>Adding or removing elements to this new instance will not impact the source, and vice versa.</remarks>
		public ColaStore<T> Copy() => new(this);

		/// <inheritdoc />
		public void Dispose()
		{
			Span<T[]?> levels = m_levels;
			m_root = null!;
			for (int i = 1; i < levels.Length; i++)
			{
				var level = levels[i];
				if (level == null) continue;

				levels[i] = null;
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					level.AsSpan(0, ColaStore.GetLevelSize(i)).Clear();
				}
				if (i > 0)
				{
					m_pool.Return(level);
				}
			}
			m_allocatedLevels = 0;
			m_count = 0;
		}

		[UsedImplicitly]
		private Span<T[]?> LevelsAllocated => GetLevels();

		[UsedImplicitly]
		private Span<T[]?> LevelsAll => m_levels;

		private (T[] Array, int Size) RentLevel(int level)
		{
			Contract.Debug.Requires(level >= 1 && level <= ColaStore.MAX_LEVEL);

			// note: the pool may return arrays that are LARGER, but the rest of the code will only use Spans that have the correct size
			int size = ColaStore.GetLevelSize(level);
			var tmp = m_pool.Rent(size);
			tmp.AsSpan(0, size).Clear();
			return (tmp, size);
		}

		private void ReturnLevel(int level)
		{
			Contract.Debug.Requires((uint) level <= ColaStore.MAX_LEVEL);

			if (level == 0)
			{ // we NEVER return the level 0 !
				return;
			}

			var array = m_levels[level];
			m_levels[level] = null;
			if (array != null)
			{
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					array.AsSpan(0, ColaStore.GetLevelSize(level)).Clear();
				}
				m_pool.Return(array);
			}

			if (level + 1 == m_allocatedLevels)
			{
				m_allocatedLevels--;
			}
		}

		private (T[] Array, int Size) RentSpare(int level) => RentLevel(level);

		private void ReturnSpare(int level, T[]? spare)
		{
			Contract.Debug.Requires((uint) level <= ColaStore.MAX_LEVEL);

			if (spare != null)
			{
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					spare.AsSpan(0, ColaStore.GetLevelSize(level)).Clear();
				}

				m_pool.Return(spare);
			}
		}

		[Conditional("ENFORCE_INVARIANTS")]
		private void CheckInvariants()
		{
#if ENFORCE_INVARIANTS
			Contract.Debug.Invariant(m_count >= 0, "Count cannot be less than zero");
			Contract.Debug.Invariant(m_allocatedLevels >= ColaStore.GetLevelCount(m_count));
			for (int i = 0; i <= ColaStore.MAX_LEVEL; i++)
			{
				var segment = m_levels[i];
				var expectedSize = ColaStore.GetLevelSize(i);

				if (i == 0)
				{
					Contract.Debug.Requires(ReferenceEquals(segment, m_root), "Level 0 should be the same as m_root");
				}
				else if (i >= m_allocatedLevels)
				{
					Contract.Debug.Invariant(segment == null, "Non allocated levels should be null");
					continue;
				}
				else
				{
					Contract.Debug.Invariant(segment != null, "All segments should be allocated in memory");
					Contract.Debug.Invariant(segment.Length >= expectedSize, "The size of a segment should be able to store 2^LEVEL items"); // maybe larger since this is rented from a pool!
				}

				if (IsFree(i))
				{ // All unused segments SHOULD be filled with default(T)
					for (int j = 0; j < expectedSize; j++)
					{
						if (!EqualityComparer<T>.Default.Equals(segment[j], default(T)))
						{
							if (Debugger.IsAttached)
							{
								Debugger.Break(); // STEP ONCE TO READ THE DUMP!
								var sw = new System.IO.StringWriter();
								Debug_Dump(sw);
								string dump = sw.ToString();
								Debug.WriteLine(dump);
								Debugger.Break();
							}
							Contract.Debug.Invariant(false, $"Non-zero value at offset {j} of unused level {i} : {string.Join(", ", segment)}");
						}
					}
				}
				else
				{ // All used segments SHOULD be sorted
					T previous = segment[0];
					for (int j = 1; j < expectedSize; j++)
					{
						T x = segment[j];
						if (m_comparer.Compare(previous, x) >= 0)
						{
							if (Debugger.IsAttached)
							{
								Debugger.Break(); // STEP ONCE TO READ THE DUMP!
								var sw = new System.IO.StringWriter();
								Debug_Dump(sw);
								string dump = sw.ToString();
								Debug.WriteLine(dump);
								Debugger.Break();
							}
							Contract.Debug.Invariant(false, $"Unsorted value {segment[j]} at offset {j} of allocated level {i} : {string.Join(", ", segment)}");
						}
						previous = segment[j];
					}
				}
			}
#endif
		}

#endregion

		#region Public Properties...

		/// <summary>Gets the number of elements in the store.</summary>
		public int Count => m_count;

		/// <summary>Gets the current capacity of the store.</summary>
		public int Capacity => (int) ((1U << m_allocatedLevels) - 1);
		// note: the capacity is always 2^L - 1 where L is the number of levels

		/// <summary>Gets the comparer used to sort the elements in the store</summary>
		public IComparer<T> Comparer => m_comparer;

		/// <summary>Gets the current number of levels</summary>
		/// <remarks>Note that the last level may not be currently used!</remarks>
		public int Depth => ColaStore.GetLevelCount(m_count);

		/// <summary>Gets the index of the first currently allocated level</summary>
		public int MinLevel => ColaStore.LowestBit(m_count);

		/// <summary>Gets the index of the last currently allocated level</summary>
		public int MaxLevel => ColaStore.HighestBit(m_count);

		/// <summary>Returns the content of a level</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns>Segment that contains all the elements of that level</returns>
		internal Span<T> GetLevel(int level)
		{
			Contract.Debug.Requires((uint) level <= ColaStore.MAX_LEVEL);
			return m_levels[level].AsSpan(0, ColaStore.GetLevelSize(level));
		}

		/// <summary>Returns the content of a level</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns>Segment that contains all the elements of that level</returns>
		internal Memory<T> GetLevelMemory(int level)
		{
			Contract.Debug.Requires((uint) level <= ColaStore.MAX_LEVEL);
			return m_levels[level].AsMemory(0, ColaStore.GetLevelSize(level));
		}

		internal Span<T[]?> GetLevels()
		{
			Span<T[]?> levels = m_levels;
			return levels.Slice(0, m_allocatedLevels);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Span<T> GetLevel(T[]? span, int level)
		{
			return span.AsSpan(0, ColaStore.GetLevelSize(level));
		}

		/// <summary>Gets a reference to the value stored at the specified index.</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Reference to the value stored at that location, or <see cref="Unsafe.NullRef{T}"/> if the location is in an unallocated level</returns>
		public ref T this[int arrayIndex]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ref GetReference(arrayIndex);
		}

		#endregion

		#region Public Methods...

		/// <summary>Finds the location of an element in the array</summary>
		/// <param name="value">Value of the element to search for.</param>
		/// <param name="level">Receives the level that contains the element if found; otherwise, -1.</param>
		/// <param name="offset">Receives the offset of the element inside the level if found; otherwise, 0.</param>
		/// <returns>Reference to the entry, or <c>null</c> if not found.</returns>
		public ref T Find(T value, out int level, out int offset)
		{
			if ((m_count & 1) != 0)
			{
				// If someone gets the last inserted key, there is a 50% change that it is in the root
				// (if not, it will the last one of the first non-empty level)
				ref T last = ref m_root[0];
				if (m_comparer.Compare(value, last) == 0)
				{
					level = 0;
					offset = 0;
					return ref last;
				}
			}

			for (int i = 1; i < m_allocatedLevels; i++)
			{
				if (IsFree(i))
				{ // this segment is not allocated
					continue;
				}

				var span = GetLevel(i);
				int p = span.BinarySearch(value, m_comparer);
				if (p >= 0)
				{
					level = i;
					offset = p;
					return ref span[p];
				}
			}

			level = ColaStore.NOT_FOUND;
			offset = 0;
			return ref Unsafe.NullRef<T>();
		}

		/// <summary>Searches for the smallest element that is larger than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="offset">Receive the offset within the level of the next element, or 0 if not found</param>
		/// <param name="result">Receive the value of the next element, or default(T) if not found</param>
		/// <returns>Level of the next element, or -1 if <paramref name="result"/> was already the largest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindNext(T value, bool orEqual, out int offset, out T result)
		{
			return ColaStore.FindNext(this, m_count, value, orEqual, m_comparer, out offset, out result);
		}
		
		/// <summary>Searches for the smallest element that is larger than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="comparer"></param>
		/// <param name="offset">Receive the offset within the level of the next element, or 0 if not found</param>
		/// <param name="result">Receive the value of the next element, or default(T) if not found</param>
		/// <returns>Level of the next element, or -1 if <paramref name="result"/> was already the largest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindNext(T value, bool orEqual, IComparer<T>? comparer, out int offset, out T result)
		{
			return ColaStore.FindNext(this, m_count, value, orEqual, comparer ?? m_comparer, out offset, out result);
		}

		/// <summary>Searches for the largest element that is smaller than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="offset">Receive the offset within the level of the previous element, or 0 if not found</param>
		/// <param name="result">Receive the value of the previous element, or default(T) if not found</param>
		/// <returns>Level of the previous element, or -1 if <paramref name="result"/> was already the smallest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindPrevious(T value, bool orEqual, out int offset, out T result)
		{
			return ColaStore.FindPrevious(this, m_count, value, orEqual, m_comparer, out offset, out result);
		}

		/// <summary>Searches for the largest element that is smaller than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="comparer"></param>
		/// <param name="offset">Receive the offset within the level of the previous element, or 0 if not found</param>
		/// <param name="result">Receive the value of the previous element, or default(T) if not found</param>
		/// <returns>Level of the previous element, or -1 if <paramref name="result"/> was already the smallest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindPrevious(T value, bool orEqual, IComparer<T>? comparer, out int offset, out T result)
		{
			return ColaStore.FindPrevious<T>(this, m_count, value, orEqual, comparer ?? m_comparer, out offset, out result);
		}

		/// <summary>Enumerates all the items in the store that are between two bounds</summary>
		/// <param name="beginInclusive">Inclusive lower bound</param>
		/// <param name="endExclusive">Exclusive upper bound</param>
		/// <param name="limit">Maximum number of items to return</param>
		/// <param name="comparer">Optional comparer (if <c>null</c> use the comparer used by the store)</param>
		/// <returns>Sequence that will return all elements in this store that are greater than or equal to <paramref name="beginInclusive"/> and strictly less than <paramref name="endExclusive"/>, up to a maximum of <paramref name="limit"/> results.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerable<T> FindBetween(T beginInclusive, T endExclusive, int limit, IComparer<T>? comparer = null)
		{
			return ColaStore.FindBetween<T>(this, m_count, beginInclusive, true, endExclusive, false, limit, comparer ?? m_comparer);
		}

		/// <summary>Enumerates all the items in the store that are between two bounds</summary>
		/// <param name="begin">Lower bound</param>
		/// <param name="beginOrEqual">Specifies if the lower bound is included (<c>true</c>) or not (<c>false</c>)</param>
		/// <param name="end">Upper bound</param>
		/// <param name="endOrEqual">Specifies if the upper bound is included (<c>true</c>) or not (<c>false</c>)</param>
		/// <param name="limit">Maximum number of items to return</param>
		/// <param name="comparer">Optional comparer (if <c>null</c> use the comparer used by the store)</param>
		/// <returns>Sequence that will return all elements in this store that are greater than (or equal to) <paramref name="begin"/> and less than (or equal to) <paramref name="end"/>, up to a maximum of <paramref name="limit"/> results.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerable<T> FindBetween(T begin, bool beginOrEqual, T end, bool endOrEqual, int limit, IComparer<T>? comparer = null)
		{
			return ColaStore.FindBetween<T>(this, m_count, begin, beginOrEqual, end, endOrEqual, limit, comparer ?? m_comparer);
		}

		/// <summary>Returns the value stored at a specific location in the array</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value stored at this location, or default(T) if the level is not allocated</returns>
		public ref T GetReference(int arrayIndex)
		{
			Contract.Debug.Requires(arrayIndex >= 0 && arrayIndex <= this.Capacity);

			var (level, offset) = ColaStore.FromIndex(arrayIndex);

			return ref GetReference(level, offset);
		}

		/// <summary>Returns the value at a specific location in the array</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <returns>Returns a reference to the value at this location, or <c>null</c> if not found</returns>
		public ref T GetReference(int level, int offset)
		{
			Contract.Debug.Requires((uint) level <= ColaStore.MAX_LEVEL && (uint) offset < (1U << level));

			return ref (IsFree(level) ? ref Unsafe.NullRef<T>() : ref GetLevel(level)[offset]);
		}

		/// <summary>Clears the array</summary>
		public void Clear()
		{
			for (int i = 0; i < m_allocatedLevels; i++)
			{
				var level = m_levels[i];
				if (level is null) continue;

				if (i < ColaStore.INITIAL_LEVELS)
				{ // we will keep this level
					if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
					{
						GetLevel(level, i).Clear();
					}
				}
				else
				{ // we will return this level to the pool

					// only need to clear if T is a ref type, or has ref type members
					if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
					{
						GetLevel(level, i).Clear();
					}
					m_pool.Return(level);
					m_levels[i] = null;
				}
			}
			m_count = 0;
			m_allocatedLevels = ColaStore.INITIAL_LEVELS;
			CheckInvariants();
		}

		/// <summary>Adds a value to the array</summary>
		/// <param name="value">Value to add to the array</param>
		/// <param name="overwriteExistingValue">If <paramref name="value"/> already exists in the array and <paramref name="overwriteExistingValue"/> is true, it will be overwritten with <paramref name="value"/>.</param>
		/// <returns><c>true</c>if the value was added to the array, or <c>false</c> if it was already there.</returns>
		/// <remarks>
		/// <para>Setting <paramref name="overwriteExistingValue"/> to <c>true</c> only makes sense for ValueTypes where the caller wants to update the existing entry with an update value, that has the same logical key value!</para>
		/// </remarks>
		public bool SetOrAdd(T value, bool overwriteExistingValue = false)
		{
			ref var entry = ref Find(value, out _, out _);
			if (!Unsafe.IsNullRef(ref entry))
			{
				if (overwriteExistingValue)
				{
					entry = value;
				}
				return false;
			}

			Insert(value);
			return true;
		}

		/// <summary>Inserts a new element in the set, and returns its index.</summary>
		/// <param name="value">Value to insert. Warning: if the value already exists, the store will be corrupted !</param>
		/// <remarks>The index is the absolute index, as if all the levels where a single, contiguous, array (0 = root, 7 = first element of level 3)</remarks>
		public void Insert(T value)
		{
			if (IsFree(0))
			{ // half the inserts (when the count is even) can be done in the root
				m_root[0] = value;
			}
			else if (IsFree(1))
			{ // a quarter of the inserts only need to move the root and the value to level 1
				ColaStore.MergeSimple(GetLevel(1), m_root[0], value, m_comparer);
				m_root[0] = default!;
			}
			else
			{ // we need to merge one or more levels

				MergeCascade(1, m_root, MemoryMarshal.CreateSpan(ref value, 1));
				m_root[0] = default!;
			}

			Interlocked.Increment(ref m_count);

			CheckInvariants();
		}

		/// <summary>Inserts two elements in the set.</summary>
		public void InsertItems(T first, T second)
		{
			Contract.Debug.Requires(m_comparer.Compare(first, second) != 0, "Cannot insert the same value twice");

			if (IsFree(1))
			{
				ColaStore.MergeSimple(GetLevel(1), first, second, m_comparer);
			}
			else
			{
				//Console.WriteLine("InsertItems([2]) Cascade");
				Span<T> spare = m_scratch;
				spare[0] = first;
				spare[1] = second;
				var segment = GetLevel(1);
				MergeCascade(2, segment, spare);
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					segment.Clear();
					spare.Clear();
				}
			}

			Interlocked.Add(ref m_count, 2);

			CheckInvariants();
		}

		/// <summary>Inserts one or more new elements in the set.</summary>
		/// <param name="values">Array of elements to insert. Warning: if a value already exist, the store will be corrupted !</param>
		/// <param name="ordered">If true, the entries in <paramref name="values"/> are guaranteed to already be sorted (using the store default comparer).</param>
		/// <remarks>The best performances are achieved when inserting a number of items that is a power of 2. The worst performances are when doubling the size of a store that is full.
		/// Warning: if <paramref name="ordered"/> is true but <paramref name="values"/> is not sorted, or is sorted using a different comparer, then the store will become corrupted !
		/// </remarks>
		public void InsertItems(List<T> values, bool ordered = false)
		{
			Contract.NotNull(values);

			int count = values.Count;

			if (count < 2)
			{
				if (count == 1)
				{
					Insert(values[0]);
				}
				return;
			}

			if (count == 2)
			{
				if (IsFree(1))
				{
					var segment = GetLevel(1);
					if (ordered)
					{
						segment[0] = values[0];
						segment[1] = values[1];
					}
					else
					{
						ColaStore.MergeSimple<T>(segment, values[0], values[1], m_comparer);
					}
				}
				else
				{
					Span<T> spare = m_scratch;
					spare[0] = values[0];
					spare[1] = values[1];
					var segment = GetLevel(1);
					MergeCascade(2, segment, spare);
					if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
					{
						segment.Clear();
						spare.Clear();
					}
				}
			}
			else
			{
				// Inserting a size that is a power of 2 is very simple:
				// * either the corresponding level is empty, in that case we just copy the items and do a quicksort
				// * or it is full, then we just need to do a cascade merge
				// For non-power of 2s, we can split decompose them into a suite of power of 2s and insert them one by one

				int min = ColaStore.LowestBit(count);
				int max = ColaStore.HighestBit(count);

				if (max >= m_allocatedLevels)
				{ // we need to allocate new levels
					Grow(max);
				}

				int p = 0;
				for (int i = min; i <= max; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					var segment = GetLevel(i);
					if (IsFree(i))
					{ // the target level is free, we can copy and sort in place

						CollectionsMarshal.AsSpan(values).Slice(p).CopyTo(segment);

						if (!ordered)
						{
							segment.Sort(m_comparer);
						}

						p += segment.Length;
						Interlocked.Add(ref m_count, segment.Length);
					}
					else
					{ // the target level is used, we will have to do a cascade merge, using a spare

						var (array, size) = RentLevel(i);
						var spare = array.AsSpan(0, size);
						CollectionsMarshal.AsSpan(values).Slice(p).CopyTo(spare);

						if (!ordered)
						{
							spare.Sort(m_comparer);
						}

						p += segment.Length;
						MergeCascade(i + 1, segment, spare);
						if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
						{
							segment.Clear();
						}

						ReturnSpare(i, array);
						Interlocked.Add(ref m_count, segment.Length);
					}
				}
				Contract.Debug.Assert(p == count);
			}

			CheckInvariants();
		}

		/// <summary>Removes the value at the specified location</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value that was removed</returns>
		public T RemoveAt(int arrayIndex)
		{
			Contract.Debug.Requires(arrayIndex >= 0 && arrayIndex <= this.Capacity);
			var (level, offset) = ColaStore.FromIndex(arrayIndex);
			return RemoveAt(level, offset);
		}

		/// <summary>Removes the value at the specified location</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <returns>Value that was removed</returns>
		public T RemoveAt(int level, int offset)
		{
			Contract.Debug.Requires(level >= 0 && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			var segment = GetLevel(level);
			T removed = segment[offset];

			if (level == 0)
			{ // removing the last inserted value
				segment[0] = default!;
			}
			else if (level == 1)
			{ // split the first level in two
				if (IsFree(0))
				{ // move up to root

					// ex: remove 'b' at (1,1) and move the 'a' back to the root
					// 0 [_]	=> [a]
					// 1 [a,b]	=> [_,_]

					m_root[0] = segment[1 - offset];
					segment[0] = default!;
					segment[1] = default!;
				}
				else
				{ // merge the root in missing spot

					// ex: remove 'b' at (1,1) and move the 'c' down a level
					//		  N = 3		N = 2
					//		0 [c]	=>	0 [_]
					//		1 [a,b]	=>	1 [a,c]

					ColaStore.MergeSimple<T>(segment, m_root[0], segment[1 - offset], m_comparer);
					m_root[0] = default!;
				}
			}
			else if ((m_count & 1) == 1)
			{ // Remove an item from an odd-numbered set

				// Since the new count will be even, we only need to merge the root in place with the level that is missing a spot

				// ex: replace the 'b' at (2,1) with the 'e' in the root
				//		  N = 5			  N = 4
				//		0 [e]		=>	0 [_]
				//		1 [_,_]			1 [_,_]
				//		2 [a,b,c,d]	=>	2 [a,c,d,e]

				ColaStore.MergeInPlace<T>(segment, offset, m_root[0], m_comparer);
				m_root[0] = default!;
			}
			else
			{
				// we are missing a spot in out modified segment, that need to fill
				// > we will take the first non-empty segment, and break it in pieces
				//  > its last item will be used to fill the empty spot
				//  > the rest of its items will be spread to all the previous empty segments

				// find the first non-empty segment that can be broken
				int firstNonEmptyLevel = ColaStore.LowestBit(m_count);

				if (firstNonEmptyLevel == level)
				{ // we are the first level, this is easy !

					// move the empty spot at the start
					if (offset > 0)
					{
						segment[..offset].CopyTo(segment[1..]);
						//Array.Copy(segment, 0, segment, 1, offset);
					}

					// and spread the rest to all the previous levels
					ColaStore.SpreadLevel(this, level);
					//TODO: modify SpreadLevel(..) to take the offset of the value to skip ?
				}
				else
				{ // break that level, and merge its last item with the level that is missing one spot

					// break down this level
					T tmp = ColaStore.SpreadLevel(this, firstNonEmptyLevel);

					// merge its last item with the empty spot in the modified level
					ColaStore.MergeInPlace(GetLevel(level), offset, tmp, m_comparer);
				}
			}

			Interlocked.Decrement(ref m_count);

			if (m_allocatedLevels > ColaStore.INITIAL_LEVELS)
			{ // maybe release the last level if it is empty
				ShrinkIfRequired();
			}

			CheckInvariants();

			return removed;
		}

		/// <summary>Removes the specified item</summary>
		public bool RemoveItem(T item)
		{
			if (Unsafe.IsNullRef(ref Find(item, out int level, out int offset)))
			{ // not found
				return false;
			}

			_ = RemoveAt(level, offset);
			CheckInvariants();
			return true;
		}

		/// <summary>Removes one or more items</summary>
		public int RemoveItems(ReadOnlySpan<T> items)
		{
			int count = 0;

			foreach (var item in items)
			{
				if (!Unsafe.IsNullRef(ref Find(item, out int level, out int offset)))
				{
					RemoveAt(level, offset);
					++count;
				}
			}
			CheckInvariants();
			return count;
		}

		/// <summary>Removes one or more items</summary>
		public int RemoveItems(IEnumerable<T> items)
		{
			Contract.NotNull(items);

			if (items.TryGetSpan(out var span))
			{
				return RemoveItems(span);
			}

			return RemoveItemsEnumerable(items);

			int RemoveItemsEnumerable(IEnumerable<T> xs)
			{
				int count = 0;

				foreach (var item in xs)
				{
					if (!Unsafe.IsNullRef(ref Find(item, out int level, out int offset)))
					{
						RemoveAt(level, offset);
						++count;
					}
				}
				CheckInvariants();
				return count;
			}
		}

		/// <summary>Copies the contents of this store into destination span.</summary>
		/// <param name="destination">The span to copy items into.</param>
		/// <exception cref="ArgumentException">Thrown when the destination Span is too small to receive all the items.</exception>
		public void CopyTo(Span<T> destination)
		{
			if (!TryCopyTo(destination))
			{
				throw new ArgumentException("Destination buffer is too short", nameof(destination));
			}
		}

		/// <summary>Copies the contents of this store into destination span.</summary>
		/// <param name="destination">The span to copy items into.</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		public bool TryCopyTo(Span<T> destination)
		{
			if (destination.Length < m_count)
			{
				return false;
			}

			int p = 0;
			foreach (var item in ColaStore.IterateOrdered(this, reverse: false))
			{
				destination[p++] = item;
			}
			Contract.Debug.Ensures(p == m_count);
			return true;
		}

		/// <summary>Checks if a level is currently not allocated</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns><see langword="true"/> is the level is unallocated and does not store any elements; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsFree(int level)
		{
			Contract.Debug.Requires(level >= 0);
			return (m_count & (1 << level)) == 0;
		}

		/// <summary>Checks if a level is currently not allocated</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="count">Current number of items in the store</param>
		/// <returns><see langword="true"/> is the level is unallocated and does not store any elements; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsFree(int level, int count)
		{
			Contract.Debug.Requires(level >= 0);
			return (count & (1 << level)) == 0;
		}

		/// <summary>Find the smallest element in the store</summary>
		/// <returns>Smallest element found, or default(T) if the store is empty</returns>
		public T Min()
		{
			switch (m_count)
			{
				case 0: return default!;
				case 1: return m_root[0];
				case 2: return GetLevel(1)[0];
				default:
				{

					int level = ColaStore.LowestBit(m_count);
					int end = ColaStore.HighestBit(m_count);
					T min = GetLevel(level)[0];
					while (level <= end)
					{
						if (!IsFree(level))
						{
							var candidate = GetLevel(level)[0];
							if (m_comparer.Compare(min, candidate) > 0)
							{
								min = candidate;
							}
						}
						++level;
					}
					return min;
				}
			}
		}

		/// <summary>Find the largest element in the store</summary>
		/// <returns>Largest element found, or default(T) if the store is empty</returns>
		public T Max()
		{
			switch (m_count)
			{
				case 0: return default!;
				case 1: return m_root[0];
				case 2: return GetLevel(1)[1];
				default:
				{
					int level = ColaStore.LowestBit(m_count);
					int end = ColaStore.HighestBit(m_count);
					T max = GetLevel(level)[^1];
					while (level <= end)
					{
						if (!IsFree(level))
						{
							var candidate = GetLevel(level)[^1];
							if (m_comparer.Compare(max, candidate) < 0)
							{
								max = candidate;
							}
						}
						++level;
					}
					return max;
				}
			}

		}

		/// <summary>Returns the smallest and largest element in the store</summary>
		/// <param name="min">Receives the value of the smallest element (or default(T) is the store is Empty)</param>
		/// <param name="max">Receives the value of the largest element (or default(T) is the store is Empty)</param>
		/// <remarks>If the store contains only one element, then min and max will be equal</remarks>
		public bool TryGetBounds([MaybeNullWhen(false)] out T min, [MaybeNullWhen(false)] out T max)
		{
			switch (m_count)
			{
				case 0:
				{
					min = default!;
					max = default!;
					return false;
				}
				case 1:
				{
					min = m_root[0];
					max = min;
					return true;
				}
				case 2:
				{
					var segment = GetLevel(1);
					min = segment[0];
					max = segment[1];
					return true;
				}
				default:
				{

					int level = ColaStore.LowestBit(m_count);
					int end = ColaStore.HighestBit(m_count);
					var segment = GetLevel(level);
					min = segment[0];
					max = segment[^1];
					while (level <= end)
					{
						if (IsFree(level)) continue;
						segment = GetLevel(level);
						if (m_comparer.Compare(min, segment[0]) > 0) min = segment[0];
						if (m_comparer.Compare(max, segment[^1]) < 0) min = segment[^1];
						++level;
					}
					return true;
				}
			}
		}

		/// <summary>Returns a new iterator that can access the contents of this store</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Iterator GetIterator() => new(this);

		/// <summary>Pre-allocate memory in the store so that it can store a specified amount of items</summary>
		/// <param name="minimumRequired">Number of items that will be inserted in the store</param>
		public void EnsureCapacity(int minimumRequired)
		{
			int level = ColaStore.GetLevelCount(minimumRequired);
			if ((1 << level) < minimumRequired)
			{
				++level;
			}

			if (level >= m_allocatedLevels)
			{
				Grow(level);
			}
		}

		#endregion

		private void MergeCascade(int level, Span<T> left, Span<T> right)
		{
			Contract.Debug.Requires(level > 0, "level");
			Contract.Debug.Requires(left.Length == (1 << (level - 1)), "left");
			Contract.Debug.Requires(right.Length == (1 << (level - 1)), "right");

			if (IsFree(level))
			{ // target level is empty

				if (level >= m_allocatedLevels)
				{
					Grow(level);
				}
				Contract.Debug.Assert(level < m_allocatedLevels);

				ColaStore.MergeSort(GetLevel(level), left, right, m_comparer);
			}
			else if (IsFree(level + 1))
			{ // the next level is empty

				if (level + 1 >= m_allocatedLevels) Grow(level + 1);
				Contract.Debug.Assert(level + 1 < m_allocatedLevels);

				var (array, size) = RentSpare(level);
				var spare = array.AsSpan(0, size);
				ColaStore.MergeSort(spare, left, right, m_comparer);
				var next = GetLevel(level);
				ColaStore.MergeSort(GetLevel(level + 1), next, spare, m_comparer);
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					next.Clear();
				}
				ReturnSpare(level, array);
			}
			else
			{ // both are full, need to do a cascade merge

				Contract.Debug.Assert(level < m_allocatedLevels);

				// merge N and N +1
				var (array, size) = RentSpare(level);
				var spare = array.AsSpan(0, size);
				ColaStore.MergeSort(spare, left, right, m_comparer);

				// and cascade to N + 2 ...
				var next = GetLevel(level);
				MergeCascade(level + 1, next, spare);
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					next.Clear();
				}
				ReturnSpare(level, array);
			}
		}

		/// <summary>Grow the capacity of the level array</summary>
		/// <param name="level">Minimum level required</param>
		private void Grow(int level)
		{
			Contract.Debug.Requires((uint) level <= ColaStore.MAX_LEVEL);

			// note: we want m_segments[level] to not be empty, which means there must be at least (level + 1) entries in the level array
			int current = m_allocatedLevels;
			int required = checked(level + 1);
			Contract.Debug.Assert(current < required);

			for (int i = current; i < required; i++)
			{
				(m_levels[i], _) = RentLevel(i);
			}

			m_allocatedLevels = required;
		}

		private void ShrinkIfRequired()
		{
			int n = m_allocatedLevels - 1;
			if (n <= ColaStore.INITIAL_LEVELS) return;
			if (IsFree(n))
			{ // less than 50% full

				// to avoid the degenerate case of constantly Adding/Removing when at the threshold of a new level,
				// we will only remove the last level if the previous level is also empty

				if (IsFree(n - 1))
				{ // less than 25% full

					ReturnLevel(n);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal IEnumerable<T> IterateOrdered(bool reverse = false)
		{
			return ColaStore.IterateOrdered(this, reverse);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal IEnumerable<T> IterateUnordered()
		{
			return ColaStore.IterateUnordered(this);
		}

		/// <summary>Writes the contents of this store into a log, for debugging purpose [DEBUG ONLY]</summary>
		[Conditional("DEBUG")]
		public void Debug_Dump(TextWriter output, Func<T, string>? dump = null)
		{
#if DEBUG
			var numLevels = this.MaxLevel + 1;
			output.WriteLine($"> Levels: {numLevels} used, {m_allocatedLevels} allocated");
			for(int i = 0; i < numLevels; i++)
			{
				output.Write($"  - {i,2}|{(IsFree(i) ? "_" : "#")}: ");
				if (!IsFree(i))
				{
					bool first = true;
					foreach (var item in GetLevel(i))
					{
						if (first) first = false;
						else output.Write(", ");
						output.Write(dump != null ? dump(item) : item?.ToString());
					}
				}
				output.WriteLine();
			}
			output.WriteLine($"> {m_count:N0} items");
#endif
		}

		/// <summary>Iterator for scanning the contents of a <see cref="ColaStore{T}"/></summary>
		[DebuggerDisplay("Current={m_current}, Level={m_currentLevel} ({m_min})")]
		[PublicAPI]
		public sealed class Iterator
		{
			private const int DIRECTION_PREVIOUS = -1;
			private const int DIRECTION_SEEK = 0;
			private const int DIRECTION_NEXT = +1;

			private readonly ColaStore<T> m_store;
			private readonly int m_count;
			private readonly IComparer<T> m_comparer;
			private readonly int[] m_cursors;
			private readonly int m_min;
			private T? m_current;
			private int m_currentLevel;
			private int m_direction;
#if FULL_DEBUG
			private ColaStore<T> m_parent; // useful when troubleshooting to have the pointer to the parent!
#endif

			internal Iterator(ColaStore<T> store)
			{
				Contract.Debug.Requires(store != null);
				m_store = store;
				m_count = store.m_count;
				m_comparer = store.m_comparer;

				m_cursors = ColaStore.CreateCursors(m_count, out m_min);
#if FULL_DEBUG
				m_parent = store;
#endif
			}

			/// <summary>Writes the internal state of this iterator into a log, for debugging purpose [DEBUG ONLY]</summary>
			[Conditional("FULL_DEBUG")]
			public void Debug_Dump(string? label = null)
			{
#if FULL_DEBUG
				System.Diagnostics.Trace.WriteLine("* Cursor State: " + label);
				for (int i = m_min; i < m_cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, m_count))
					{
						System.Diagnostics.Trace.WriteLine("  - L" + i + ": unallocated");
						continue;
					}

					int p = m_cursors[i];
					System.Diagnostics.Trace.WriteLine("  - L" + i + ": " + p + " [" + (1 << i) + "] = " + (p < 0 ? "<BEFORE>" : (p >= (1 << i)) ? "<AFTER>" : ("" + m_levels[i][p])));
				}
				System.Diagnostics.Trace.WriteLine(" > Current at " + m_currentLevel + " : " + m_current);
#endif
			}

			/// <summary>Set the cursor just before the first key in the store</summary>
			public void SeekBeforeFirst()
			{
				var cursors = m_cursors;
				cursors[m_min] = -1;
				for (int i = m_min + 1; i < cursors.Length; i++)
				{
					cursors[i] = 0;
				}
				m_currentLevel = m_min;
				m_current = default(T);
				m_direction = DIRECTION_SEEK;
			}

			/// <summary>Set the cursor just before the first key in the store</summary>
			public void SeekAfterLast()
			{
				var cursors = m_cursors;
				for (int i = m_min; i < cursors.Length; i++)
				{
					cursors[i] = 1 << i;
				}
				m_currentLevel = ColaStore.NOT_FOUND;
				m_current = default(T);
				m_direction = DIRECTION_SEEK;
			}

			/// <summary>Seek the cursor to the smallest key in the store</summary>
			public bool SeekFirst()
			{
				var min = default(T);
				int minLevel = ColaStore.NOT_FOUND;

				var cursors = m_cursors;
				var cmp = m_comparer;
				var count = m_count;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					cursors[i] = 0;
					var segment = m_store.GetLevel(i);
					if (minLevel < 0 || cmp.Compare(segment[0], min) < 0)
					{
						min = segment[0];
						minLevel = i;
					}
				}

				m_current = min;
				m_currentLevel = minLevel;
				m_direction = DIRECTION_SEEK;

				Debug_Dump("SeekFirst");

				return minLevel >= 0;
			}

			/// <summary>Seek the cursor to the largest key in the store</summary>
			public bool SeekLast()
			{
				var max = default(T);
				int maxLevel = ColaStore.NOT_FOUND;

				var cursors = m_cursors;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, m_count)) continue;
					var segment = m_store.GetLevel(i);
					int pos = segment.Length - 1;
					cursors[i] = pos;
					if (maxLevel < 0 || m_comparer.Compare(segment[pos], max) > 0)
					{
						max = segment[^1];
						maxLevel = i;
					}
				}

				m_current = max;
				m_currentLevel = maxLevel;
				m_direction = DIRECTION_SEEK;

				Debug_Dump("SeekLast");

				return maxLevel >= 0;
			}

			/// <summary>Seek the iterator at the smallest value that is less (or equal) to the desired item</summary>
			/// <param name="item">Item to seek to</param>
			/// <param name="orEqual">When <paramref name="item"/> exists: if <c>true</c>, then seek to this item. If <c>false</c>, seek to the previous entry.</param>
			public bool Seek(T item, bool orEqual)
			{
				// Goal: we want to find the item key itself (if it exists and orEqual==true), or the max key that is strictly less than item
				// We can use BinarySearch to look in each segment for where that key would be, but we have to compensate for the fact that BinarySearch looks for the smallest key that is greater than or equal to the search key.

				// Also, the iterator can be used to move:
				// - forward: from the current location, find the smallest key that is greater than the current cursor position
				// - backward: from the current location, find the largest key that is smaller than the current cursor position

				var max = default(T);
				int maxLevel = ColaStore.NOT_FOUND;
				bool exact = false;

				var cursors = m_cursors;
				var count = m_count;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					var segment = m_store.GetLevel(i);

					int pos = segment.BinarySearch(item, m_comparer);

					if (pos >= 0)
					{ // we found a match in this segment

						if (orEqual)
						{ // the item exist and is allowed
							max = segment[pos];
							maxLevel = i;
							exact = true; // stop checking for the max in other levels
						}
						else
						{ // the previous value is by definition less than 'item'
							--pos;
						}
					}
					else
					{ // not in this segment

						pos = ~pos; // <- position of where item would be place in this segment == position of the first item that is larger than item
						// since segment[pos] > item, and item is not in segment, then segment[pos - 1] < item
						--pos;
					}

					// bound check

					if (pos < 0)
					{ // the value would be before this segment
						cursors[i] = 0;
					}
					else if (pos >= segment.Length)
					{ // the value would be after this segment
						cursors[i] = segment.Length;
					}
					else
					{
						cursors[i] = pos;
						if (!exact && (maxLevel < 0 || m_comparer.Compare(segment[pos], max) > 0))
						{
							max = segment[pos];
							maxLevel = i;
						}
					}
				}

				m_currentLevel = maxLevel;
				m_current = max;
				m_direction = DIRECTION_SEEK;
				Debug_Dump("Seek");
				return maxLevel >= 0;
			}

			/// <summary>Move the cursor the smallest value that is greater than the current value</summary>
			public bool Next() => Next(out _);

			/// <summary>Move the cursor the smallest value that is greater than the current value</summary>
			/// <param name="value">Receives the next value</param>
			public bool Next([MaybeNullWhen(false)] out T value)
			{
				// invalid position, or no more values
				if (m_currentLevel < 0)
				{
					value = default;
					return false;
				}

				var cursors = m_cursors;
				var count = m_count;

				var prev = m_current;
				var min = default(T);
				int minLevel = ColaStore.NOT_FOUND;
				int pos;

				if (m_direction >= DIRECTION_SEEK)
				{ // we know that the current position CANNOT be the next value, so increment that cursor
					cursors[m_currentLevel]++;
					Debug_Dump("Next:continue");
				}
				else
				{ // previous call was a Previous()
					// we know that the current is the largest value of all the current cursors. Since we want even larger than that, we have to increment ALL the cursors
					for (int i = m_min; i < cursors.Length; i++)
					{
						if (!ColaStore.IsFree(i, count) && ((pos = cursors[i]) < ColaStore.GetLevelSize(i)))
						{
							cursors[i] = pos + 1;
						}
					}
					Debug_Dump("Next:reverse");
				}

				var cmp = m_comparer;
				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					pos = cursors[i];
					if (pos < 0) continue; //??

					var segment = m_store.GetLevel(i);

					var x = default(T);
					while(pos < segment.Length && cmp.Compare((x = segment[pos]), prev) < 0)
					{ // cannot be less than the previous value
						cursors[i] = ++pos;
					}
					if (pos >= segment.Length) continue;

					if (minLevel < 0 || cmp.Compare(x, min) < 0)
					{ // new minimum
						min = x;
						minLevel = i;
					}
				}

				m_current = min;
				m_currentLevel = minLevel;
				m_direction = DIRECTION_NEXT;
				if (minLevel >= 0)
				{
					value = min!;
					return true;
				}
				else
				{
					value = default;
					return false;
				}
			}

			/// <summary>Move the cursor the largest value that is smaller than the current value</summary>
			public bool Previous()
			{
				// invalid position, or no more values
				if (m_currentLevel < 0) return false;

				var cursors = m_cursors;
				var count = m_count;

				var prev = m_current;
				var max = default(T);
				int pos;
				int maxLevel = ColaStore.NOT_FOUND;

				if (m_direction <= DIRECTION_SEEK)
				{ // we know that the current position CANNOT be the next value, so decrement that cursor
					cursors[m_currentLevel]--;
					Debug_Dump("Previous:continue");
				}
				else
				{ // previous call was a call to Seek(), or Next()
					// we know that the current is the smallest value of all the current cursors. Since we want even smaller than that, we have to decrement ALL the cursors
					for (int i = m_min; i < cursors.Length; i++)
					{
						if (!ColaStore.IsFree(i, count) && ((pos = cursors[i]) >= 0))
						{
							cursors[i] = pos - 1;
						}
					}
					Debug_Dump("Previous:reverse");
				}

				var cmp = m_comparer;
				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					pos = cursors[i];
					var segment = m_store.GetLevel(i);
					if (pos >= segment.Length) continue; //??

					var x = default(T);
					while (pos >= 0 && cmp.Compare((x = segment[pos]), prev) > 0)
					{ // cannot be more than the previous value
						cursors[i] = --pos;
					}
					if (pos < 0) continue;

					if (maxLevel < 0 || cmp.Compare(x, max) > 0)
					{ // new maximum
						max = x;
						maxLevel = i;
					}
				}

				m_current = max;
				m_currentLevel = maxLevel;
				m_direction = DIRECTION_PREVIOUS;
				return maxLevel >= 0;
			}

			/// <summary>Value of the current entry</summary>
			public T? Current => m_current;

			/// <summary>Checks if the current position of the iterator is valid</summary>
			public bool Valid => m_currentLevel >= 0;

			/// <summary>Direction of the last operation</summary>
			public int Direction => m_direction;

			/// <summary>Current position of the cursor</summary>
			/// <remarks>This can be used to efficiently "remove" an item if we already know its location</remarks>
			internal (int Level, int Offset) Position => (m_currentLevel, m_cursors[m_currentLevel]);

		}

	}

}

#endif
