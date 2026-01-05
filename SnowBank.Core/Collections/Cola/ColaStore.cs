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
	using System.Numerics;
	using System.Runtime.InteropServices;

	/// <summary>Helper methods used by <see cref="ColaStore"/> and derived implementations</summary>
	public static class ColaStore
	{

		/// <summary>With 5 initial levels, we will pre-allocate space for 31 items</summary>
		internal const int INITIAL_LEVELS = 5;

		/// <summary>We can have a maximum of 30 levels</summary>
		/// <remarks>since the 31st level would need allocate 2^31 (2,147,483,648) items which is bigger than Array.MaxLength or 0x7FFFFFC7 (2,147,483,591)</remarks>
		internal const int MAX_LEVEL = 30;

		/// <summary>The maximum number of items that can be stored, with all levels filled</summary>
		internal const int MAX_CAPACITY = 0x7FFFFFFF; // (2^(MAX_LEVELS + 1)) - 1

		/// <summary>Value returned when lookup operation does not find a match</summary>
		internal const int NOT_FOUND = -1;

#if !NET6_0_OR_GREATER
		private static readonly int[] MultiplyDeBruijnLowestBitPosition =
		[
			0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8, 
			31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
		];

		private static readonly int[] MultiplyDeBruijnHighestBitPosition =
		[
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
			8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		];
#endif

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsFree(int level, int count)
		{
			return (count & (1 << level)) == 0;
		}

		/// <summary>Returns the number of levels needed to store the given number of items</summary>
		/// <param name="capacity">Total capacity required</param>
		/// <returns>Number of levels that can store up to this number of items</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int GetLevelCount(int capacity)
		{
			// returns up to MAX_LEVELS + 1 (or 31)
			return HighestBit(capacity) + 1;
		}

		internal static int GetLevelSize(int level)
		{
			Contract.Debug.Requires((uint) level <= MAX_LEVEL);
			return 1 << level;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsAllocated(int level, int count)
		{
			Contract.Debug.Requires(level >= 0 && count >= 0);
			return (count & (1 << level)) != 0;
		}

		/// <summary>Finds the level that holds an absolute index</summary>
		/// <param name="index">Absolute index in a COLA array where 0 is the root, 1 is the first item of level 1, and so on</param>
		/// <returns>Level and Offset that contains the specified location.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (int Level, int Offset) FromIndex(int index)
		{
			Contract.Debug.Requires(index >= 0);

			// frequently called with index == 0
			if (index == 0)
			{
				return (0, 0);
			}

			// Sample Lookup Table:
			// idx    lvl off
			//  0  =>  0   0
			//  1  =>  1   0
			//  2  =>  1   1
			//  3  =>  2   0
			//  4  =>  2   1
			//  5  =>  2   2
			//  6  =>  2   3
			//  7  =>  3   0

			// level is the Log2 of the index
			int level = BitOperations.Log2((uint) (index + 1));

			// offset is equal to index minus the number of items in all previous levels
			// this does not work for index == 0, but we have handled this case already
			int offset = index - (int) ((1U << level) - 1);

			Contract.Debug.Ensures((uint) level <= MAX_LEVEL && (uint) offset < (1 << level));
			return (level, offset);
		}

		/// <summary>Convert a (level, offset) pair into the corresponding absolute index</summary>
		/// <param name="level">Level of the location (0 for the root)</param>
		/// <param name="offset">Offset within the level of the location</param>
		/// <returns>Absolute index where 0 is the root, 1 is the first item of level 1, and so on</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ToIndex(int level, int offset)
		{
			Contract.Debug.Requires(level >= 0 && level <= MAX_LEVEL && offset >= 0 && offset < (1 << level));

			int index = (1 << level) - 1 + offset;
			Contract.Debug.Ensures(index >= 0 && index < ((1U << (level + 1)) - 1));
			return index;
		}

		/// <summary>Returns the position of the lowest bit set, or <c>0</c> if all bits are cleared</summary>
		/// <remarks>This method returns <see langword="0"/> if value is <see langword="0"/></remarks>
		[Pure]
		public static int LowestBit(int value)
		{
			Contract.Debug.Requires(value >= 0);
#if NET6_0_OR_GREATER
			// note: TrailingZeroCount returns 32 (which is logical), but we want to return 0 for this case, hence why we AND with 31 at the end
			return BitOperations.TrailingZeroCount(unchecked((uint) value)) & 31;
#else
			uint v = (uint)value;
			v = (uint)((v & -v) * 0x077CB531U);

			return MultiplyDeBruijnLowestBitPosition[v >> 27];
#endif
		}

		/// <summary>Returns the position of the highest bit set</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int HighestBit(int value)
		{
			Contract.Debug.Requires(value >= 0);
#if NET6_0_OR_GREATER
			return BitOperations.Log2(unchecked((uint) value));
#else
			// first round down to one less than a power of 2 
			uint v = (uint)value;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;

			return MultiplyDeBruijnHighestBitPosition[(int)((v * 0x07C4ACDDU) >> 27)];
#endif
		}

		/// <summary>Computes the absolute index from a value offset (in the allocated levels)</summary>
		/// <param name="count">Number of items in the COLA array</param>
		/// <param name="arrayIndex">Offset of the value in the allocated levels of the COLA array, with 0 being the oldest (first item of the last allocated level)</param>
		/// <returns>Absolute index of the location where that value would be stored in the COLA array (from the top)</returns>
		public static int MapOffsetToIndex(int count, int arrayIndex)
		{
			Contract.Debug.Requires(count >= 0 && arrayIndex >= 0);

			var (level, offset) = MapOffsetToLocation(count, arrayIndex);
			if (level < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			return (1 << level) - 1 + offset;
		}

		/// <summary>Computes the (level, offset) pair from a value offset (in the allocated levels)</summary>
		/// <param name="count">Number of items in the COLA array</param>
		/// <param name="arrayIndex">Offset of the value in the allocated levels of the COLA array, with 0 being the oldest (first item of the last allocated level)</param>
		/// <returns>Absolute index of the location where that value would be stored in the COLA array (from the top)</returns>
		public static (int Level, int Offset) MapOffsetToLocation(int count, int arrayIndex)
		{
			Contract.Debug.Requires(count >= 0 && arrayIndex >= 0);

			if (count == 0)
			{ // special case for the empty array
				return (0, 0);
			}

			// find the highest allocated level (note: 50% of values will be in this segment!)
			int level = HighestBit(count);
			int k = 1 << level;
			//int p = k - 1;
			do
			{
				if ((count & k) != 0)
				{ // this level is allocated
					if (arrayIndex < k)
					{
						return (level, arrayIndex);
					}
					arrayIndex -= k;
				}
				k >>= 1;
				--level;
				//p -= k;
			}
			while (k > 0);

			return (NOT_FOUND, 0);
		}

		/// <summary>Computes the absolute location from a (level, offset) pair (in the allocated levels)</summary>
		public static int MapLocationToOffset(int count, int level, int offset)
		{
			Contract.Debug.Assert(count >= 0 && level >= 0 && offset >= 0 && offset < 1 << level);

			if (count == 0)
			{ // special case for the empty array
				return 0;
			}

			// compute the base location of the selected level
			int p = 0;
			int k = 1;
			for (int i = 0; i < level; i++)
			{
				if ((count & k) != 0)
				{
					p += k;
				}
				k <<= 1;
			}

			return p + offset;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorDuplicateKey<T>(T value)
		{
			return new($"Cannot insert '{value}' because the key already exists in the set");
		}

		internal static int BinarySearch<T>(ReadOnlySpan<T> array, T value, IComparer<T> comparer)
		{
			Contract.Debug.Assert(comparer != null);

			// Instead of starting from the middle we will exploit the fact that, since items are usually inserted in order, the value is probably either to the left or the right of the segment.
			// Also, since most activity happens in the top levels, the search array is probably very small (size 1, 2 or 4)

			if (array.Length == 0)
			{
				// note: there should be no array of size 0, this is probably a bug !
				return ~(0);
			}

			int end = array.Length - 1;

			// compare with the last item
			int c = comparer.Compare(array[end], value);
			if (c == 0) return end;
			if (array.Length == 1)
			{
				return c < 0 ? ~(1) : ~(0);
			}
			if (c < 0) return ~(end + 1);
			--end;

			// compare with the first
			c = comparer.Compare(array[0], value);
			if (c == 0) return 0;
			if (c > 0) return ~0;

			int cursor = 1;
			while (cursor <= end)
			{
				int center = cursor + ((end - cursor) >> 1);
				c = comparer.Compare(array[center], value);
				if (c == 0)
				{ // the value is the center point
					return center;
				}
				if (c < 0)
				{ // the value is after the center point
					cursor = center + 1;
				}
				else
				{ // the value is before the center point
					end = center - 1;
				}
			}
			return ~cursor;
		}

		/// <summary>Merge two values into level 1</summary>
		/// <param name="segment">Segment for level 1 (should be of size 2)</param>
		/// <param name="left">Left value</param>
		/// <param name="right">Right value</param>
		/// <param name="comparer">Comparer to use</param>
		internal static void MergeSimple<T>(Span<T> segment, T left, T right, IComparer<T> comparer)
		{
			Contract.Debug.Requires(segment.Length == 2);

			int c = comparer.Compare(left, right);
			if (c == 0) throw ErrorDuplicateKey(right);
			else if (c < 0)
			{
				segment[0] = left;
				segment[1] = right;
			}
			else
			{
				segment[0] = right;
				segment[1] = left;
			}
		}

		/// <summary>Replace a value in a segment with another value, while keeping it sorted</summary>
		/// <param name="segment">Segment that will receive the new value</param>
		/// <param name="offset">Offset of replaced value in the segment</param>
		/// <param name="value">New value to insert into the segment</param>
		/// <param name="comparer">Comparer to use</param>
		internal static void MergeInPlace<T>(Span<T> segment, int offset, T value, IComparer<T> comparer)
		{
			Contract.Debug.Requires(offset >= 0 && comparer != null);

			// Find the spot where the new value should be inserted
			int p = segment.BinarySearch(value, comparer);
			int index = p < 0 ? (~p) : p;
			Contract.Debug.Assert(index <= segment.Length);

			if (index == offset)
			{ // merge in place

				//                _______ offset == index
				//               V
				// before: [...] X [...] 
				// after:  [...] O [...]

				segment[index] = value;
				return;
			}

			if (index < offset)
			{ // shift right

				//                 ____________ index
				//                /     _______ offset
				//               V     V
				// before: [...] # # # X [...] 
				// after:  [...] O # # # [...]

				segment.Slice(index, offset - index).CopyTo(segment.Slice(index + 1));
				segment[index] = value;
			}
			else
			{ // shift left

				--index;

				//                 ____________ offset
				//                /     _______ index
				//               V     V
				// before: [...] X # # # [...] 
				// after:  [...] # # # O [...]

				segment.Slice(offset + 1, index - offset).CopyTo(segment.Slice(offset));
				segment[index] = value;
			}
		}

		/// <summary>Spread the content of a level to all the previous levels into pieces, except the first item that is returned</summary>
		/// <param name="store">Item store</param>
		/// <param name="level">Level that should be broken into chunks</param>
		/// <returns>The last element of the broken level</returns>
		/// <remarks>The broken segment will be cleared</remarks>
		internal static T SpreadLevel<T>(ColaStore<T> store, int level)
		{
			Contract.Debug.Requires(store != null && level <= store.MaxLevel);

			// Spread all items in the target level - except the first - to the previous level (which should ALL be EMPTY)

			var source = store.GetLevel(level);

			int p = 1;
			for (int i = level - 1; i >= 0; i--)
			{
				var segment = store.GetLevel(i);
				source.Slice(p, segment.Length).CopyTo(segment);
				p += segment.Length;
			}
			Contract.Debug.Assert(p == source.Length);
			T res = source[0];
			source.Clear();
			return res;
		}

		/// <summary>Merge two ordered segments of level N into an ordered segment of level N + 1</summary>
		/// <param name="output">Destination, level N + 1 (size 2^(N+1))</param>
		/// <param name="left">First level N segment (size 2^N)</param>
		/// <param name="right">Second level N segment (size 2^N)</param>
		/// <param name="comparer">Comparer used for the merge</param>
		internal static void MergeSort<T>(Span<T> output, Span<T> left, Span<T> right, IComparer<T> comparer)
		{
			Contract.Debug.Requires(comparer != null);
			Contract.Debug.Requires(left.Length > 0 && output.Length == left.Length * 2 && right.Length == left.Length);

			int c, n = left.Length;
			// note: The probability to merge an array of size N is roughly 1/N (with N being a power of 2),
			// which means that we will spend roughly half the time merging arrays of size 1 into an array of size 2...

			if (n == 1)
			{ // Most frequent case (p=0.5)
				var l = left[0];
				var r = right[0];
				if ((c = comparer.Compare(l, r)) < 0)
				{
					output[0] = l;
					output[1] = r;
				}
				else
				{
					Contract.Debug.Assert(c != 0);
					output[0] = r;
					output[1] = l;
				}
				return;
			}

			if (n == 2)
			{ // second most frequent case (p=0.25)

				// We are merging 2 pairs of ordered values into an array of size 4
				if (comparer.Compare(left[1], right[0]) < 0)
				{ // left << right
					output[0] = left[0];
					output[1] = left[1];
					output[2] = right[0];
					output[3] = right[1];
					return;
				}

				if (comparer.Compare(right[1], left[0]) < 0)
				{ // right << left
					output[0] = right[0];
					output[1] = right[1];
					output[2] = left[0];
					output[3] = left[1];
					return;
				}

				// left and right intersects
				// => just use the regular merge sort below
			}

			int pLeft = 0;
			int pRight = 0;
			int pOutput = 0;

			while (true)
			{
				if ((c = comparer.Compare(left[pLeft], right[pRight])) < 0)
				{ // left is smaller than right => advance

					output[pOutput++] = left[pLeft++];

					if (pLeft >= n)
					{ // the left array is done, copy the remainder of the right array
						if (pRight < n)
						{
							right.Slice(pRight, n - pRight).CopyTo(output.Slice(pOutput));
						}

						return;
					}
				}
				else
				{ // right is smaller or equal => advance
					Contract.Debug.Assert(c != 0);

					output[pOutput++] = right[pRight++];

					if (pRight >= n)
					{ // the right array is done, copy the remainder of the left array
						if (pLeft < n)
						{
							left.Slice(pLeft, n - pLeft).CopyTo(output.Slice(pOutput));
						}

						return;
					}
				}
			}

		}

		internal static int[] CreateCursors(int count, out int min)
		{
			min = LowestBit(count);
			var cursors = new int[HighestBit(count) + 1];
			int k = 1;
			for (int i = 0; i < cursors.Length; i++)
			{
				if (i < min || (count & k) == 0) cursors[i] = NOT_FOUND;
				k <<= 1;
			}
			return cursors;
		}

		/// <summary>Search for the smallest element that is larger than a reference element</summary>
		/// <param name="store">Item store</param>
		/// <param name="count"></param>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="comparer"></param>
		/// <param name="offset">Receive the offset within the level of the next element, or 0 if not found</param>
		/// <param name="result">Receive the value of the next element, or default(T) if not found</param>
		/// <returns>Level of the next element, or -1 if <paramref name="result"/> was already the largest</returns>
		public static int FindNext<T>(ColaStore<T> store, int count, T value, bool orEqual, IComparer<T> comparer, out int offset, out T result)
		{
			int level = NOT_FOUND;
			var min = default(T);
			int minOffset = 0;


			// scan each segment for a value that would be larger, keep track of the smallest found
			var numLevels = store.MaxLevel + 1;
			for (int i = 0; i < numLevels; i++)
			{
				if (IsFree(i, count)) continue;

				var segment = store.GetLevel(i);
				int pos = segment.BinarySearch(value, comparer);
				if (pos >= 0)
				{ // we found an exact match in this segment
					if (orEqual)
					{
						offset = pos;
						result = segment[pos];
						return i;
					}

					// the next item in this segment should be larger
					++pos;
				}
				else
				{ // we found where it would be stored in this segment
					pos = ~pos;
				}

				if (pos < segment.Length)
				{
					if (level == NOT_FOUND || comparer.Compare(segment[pos], min) < 0)
					{ // we found a better candidate
						min = segment[pos];
						level = i;
						minOffset = pos;
					}
				}
			}

			offset = minOffset;
			result = min!;
			return level;
		}

		/// <summary>Search for the largest element that is smaller than a reference element</summary>
		/// <param name="store">Item store</param>
		/// <param name="count"></param>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="comparer"></param>
		/// <param name="offset">Receive the offset within the level of the previous element, or 0 if not found</param>
		/// <param name="result">Receive the value of the previous element, or default(T) if not found</param>
		/// <returns>Level of the previous element, or -1 if <paramref name="result"/> was already the smallest</returns>
		public static int FindPrevious<T>(ColaStore<T> store, int count, T value, bool orEqual, IComparer<T> comparer, out int offset, out T result)
		{
			int level = NOT_FOUND;
			var max = default(T);
			int maxOffset = 0;

			// scan each segment for a value that would be smaller, keep track of the smallest found
			var numLevels = store.MaxLevel + 1;
			for (int i = 0; i < numLevels; i++)
			{
				if (IsFree(i, count)) continue;

				var segment = store.GetLevel(i);
				int pos = segment.BinarySearch(value, comparer);
				// the previous item in this segment should be smaller
				if (pos < 0)
				{ // it is not 
					pos = ~pos;
				}
				else if (orEqual)
				{ // we found an exact match in this segment
					offset = pos;
					result = segment[pos];
					return i;
				}

				--pos;

				if (pos >= 0)
				{
					if (level == NOT_FOUND || comparer.Compare(segment[pos], max) > 0)
					{ // we found a better candidate
						max = segment[pos];
						level = i;
						maxOffset = pos;
					}
				}
			}

			offset = maxOffset;
			result = max!;
			return level;
		}

		/// <summary>Enumerates all the elements between two bounds of a store</summary>
		/// <typeparam name="T">Type of the elements</typeparam>
		/// <param name="store">Store to scan</param>
		/// <param name="count">Number of elements in the store</param>
		/// <param name="begin">Lower bound</param>
		/// <param name="beginOrEqual">If <c>true</c> the lower bound is included in the scan</param>
		/// <param name="end">Upper bound</param>
		/// <param name="endOrEqual">If <c>true</c> the upper bound is included in the scan</param>
		/// <param name="limit">Maximum number of elements to return</param>
		/// <param name="comparer">Element comparer</param>
		public static IEnumerable<T> FindBetween<T>(ColaStore<T> store, int count, T begin, bool beginOrEqual, T end, bool endOrEqual, int limit, IComparer<T> comparer)
		{
			if (limit > 0)
			{
				var numLevels = store.MaxLevel + 1;
				for (int i = 0; i < numLevels; i++)
				{
					if (IsFree(i, count)) continue;

					var segment = store.GetLevelMemory(i);

					int to = segment.Span.BinarySearch(end, comparer);
					if (to >= 0)
					{
						if (!endOrEqual)
						{
							to--;
						}
					}
					else
					{
						to = ~to;
					}
					if (to < 0 || to >= segment.Length) continue;

					int from = segment.Span.BinarySearch(begin, comparer);
					if (from >= 0)
					{
						if (!beginOrEqual)
						{
							++from;
						}
					}
					else
					{
						from = ~from;
					}

					if (from >= segment.Length) continue;

					if (from > to) continue;

					for (int j = from; j <= to && limit > 0; j++)
					{
						yield return segment.Span[j];
						--limit;
					}
					if (limit <= 0) break;
				}
			}
		}

		/// <summary>Find the next smallest key pointed by a list of cursors</summary>
		/// <param name="store">Item store</param>
		/// <param name="cursors">Lit of cursors in source arrays</param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <param name="comparer">Key comparer</param>
		/// <param name="result">Received the next smallest element if the method returns true; otherwise set to default(T)</param>
		/// <returns>The index of the level that returned the value, or -1 if all levels are done</returns>
		internal static int IterateFindNext<T>(ColaStore<T> store, int[] cursors, int min, int max, IComparer<T> comparer, out T result)
		{
			Contract.Debug.Requires(store != null && cursors != null && min >= 0 && max >= min && comparer != null);

			int index = NOT_FOUND;
			int pos = NOT_FOUND;
			var next = default(T);

			// look for the smallest element
			// note: we scan from the bottom up, because older items are usually in the lower levels
			for (int i = max; i >= min; i--)
			{
				int cursor = cursors[i];
				if (cursor < 0) continue;

				var segment = store.GetLevel(i);
				if (cursor >= segment.Length) continue;

				var x = segment[cursor];
				if (index == NOT_FOUND || comparer.Compare(x, next) < 0)
				{ // found a candidate
					index = i;
					pos = cursor;
					next = x;
				}
			}

			if (index != NOT_FOUND)
			{
				++pos;
				if (pos >= (1 << index))
				{ // this array is done
					pos = NOT_FOUND;
				}
				cursors[index] = pos;
				result = next!;
				return index;
			}

			result = default!;
			return NOT_FOUND;
		}

		/// <summary>Find the next largest key pointed by a list of cursors</summary>
		/// <param name="store">Item store</param>
		/// <param name="cursors">Lit of cursors in source arrays</param>
		/// <param name="max"></param>
		/// <param name="comparer">Key comparer</param>
		/// <param name="result">Received the next largest element if the method returns true; otherwise set to default(T)</param>
		/// <param name="min"></param>
		/// <returns>The index of the level that returned the value, or -1 if all levels are done</returns>
		internal static int IterateFindPrevious<T>(ColaStore<T> store, int[] cursors, int min, int max, IComparer<T> comparer, out T result)
		{
			Contract.Debug.Requires(store != null && cursors != null && min >= 0 && max >= min && comparer != null);
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!

			//Trace.WriteLine("IterateFindPrevious(" + min + ".." + max + ")");

			int index = NOT_FOUND;
			int pos = NOT_FOUND;
			var next = default(T);

			// look for the largest element
			// note: we scan from the top down, because more recent items are usually in the upper levels
			for (int i = min; i >= max; i--)
			{
				int cursor = cursors[i];
				if (cursor < 0) continue;

				var segment = store.GetLevel(i);
				if (cursor >= segment.Length) continue;

				var x = segment[cursor];
				if (index == NOT_FOUND || comparer.Compare(x, next) < 0)
				{ // found a candidate
					index = i;
					pos = cursor;
					next = x;
				}
			}

			if (index != NOT_FOUND)
			{
				--pos;
				if (pos < 0)
				{ // this array is done
					pos = NOT_FOUND;
				}
				cursors[index] = pos;
				result = next!;
				return index;
			}

			result = default!;
			return NOT_FOUND;
		}

		/// <summary>Iterate over all the values in the set, using their natural order</summary>
		internal static IEnumerable<T> IterateOrdered<T>(ColaStore<T> store, bool reverse)
		{
			Contract.Debug.Requires(store != null);
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!

			// We will use a list of N cursors, set to the start of their respective levels.
			// At each turn, look for the smallest key referenced by the cursors, return that one, and advance its cursor.
			// Once a cursor is past the end of its level, it is set to -1 and is ignored for the rest of the operation

			var count = store.Count;
			var comparer = store.Comparer;

			if (count > 0)
			{
				// set up the cursors, with the empty levels already marked as completed
				var cursors = new int[store.MaxLevel + 1];
				for (int i = 0; i < cursors.Length; i++)
				{
					if (IsFree(i, count))
					{
						cursors[i] = NOT_FOUND;
					}
				}

				// pre-compute the first/last active level
				int min = LowestBit(count);
				int max = HighestBit(count);

				while (count-- > 0)
				{
					T? item;
					int pos;
					if (reverse)
					{
						pos = IterateFindPrevious(store, cursors, min, max, comparer, out item);
					}
					else
					{
						pos = IterateFindNext(store, cursors, min, max, comparer, out item);
					}

					if (pos == NOT_FOUND)
					{ // we unexpectedly ran out of stuff before the end ?
						//TODO: should we fail or stop here ?
						throw new InvalidOperationException("Not enough data in the source arrays to fill the output array");
					}
					yield return item!;

					// update the bounds if needed
					if (pos == max)
					{
						if (cursors[max] == NOT_FOUND) --max;
					}
					else if (pos == min)
					{
						if (cursors[min] == NOT_FOUND) ++min;
					}
				}
			}
		}

		/// <summary>Iterate over all the values in the set, without any order guarantee</summary>
		internal static IEnumerable<T> IterateUnordered<T>(ColaStore<T> store)
		{
			Contract.Debug.Requires(store != null);

			int numLevels = store.MaxLevel + 1;
			for (int i = 0; i < numLevels; i++)
			{
				if (store.IsFree(i)) continue;
				var segment = store.GetLevelMemory(i);
				for (int j = 0; j < segment.Length; j++)
				{
					yield return segment.Span[j];
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorStoreVersionChanged()
			=> new("The version of the store has changed. This usually means that the collection has been modified while it was being enumerated");

		/// <summary>Enumerates the elements stored in a <see cref="ColaOrderedSet{T}"/></summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator<T> : IEnumerator<T>
		{
			private readonly ColaStore<T> m_items;
			private readonly bool m_reverse;
			private int[] m_cursors;
			private T? m_current;
			private int m_min;
			private int m_max;

			internal Enumerator(ColaStore<T> items, bool reverse)
			{
				m_items = items;
				m_reverse = reverse;
				m_cursors = CreateCursors(m_items.Count, out m_min);
				m_max = m_cursors.Length - 1;
				m_current = default;
				Contract.Ensures(m_max >= m_min);
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				if (m_max < m_min)
				{ // no more items!
					return false;
				}

				int pos;
				if (m_reverse)
				{
					pos = IterateFindPrevious(m_items, m_cursors, m_min, m_max, m_items.Comparer, out m_current);
				}
				else
				{
					pos = IterateFindNext(m_items, m_cursors, m_min, m_max, m_items.Comparer, out m_current);
				}

				if (pos == NOT_FOUND)
				{ // that was the last item!
					return false;
				}

				// update the bounds if necessary
				if (pos == m_max)
				{
					if (m_cursors[m_max] == NOT_FOUND)
					{
						--m_max;
					}
				}
				else if (pos == m_min)
				{
					if (m_cursors[m_min] == NOT_FOUND)
					{
						++m_min;
					}
				}

				return true;
			}

			/// <inheritdoc />
			public readonly T Current => m_current!;

			/// <summary>Set to <see langword="true"/> if the store is enumerated in reverse order</summary>
			public readonly bool Reverse => m_reverse;

			/// <inheritdoc />
			public void Dispose()
			{
				// we are a struct that can be copied by value, so there is no guarantee that Dispose() will accomplish anything anyway...
			}

			object System.Collections.IEnumerator.Current => m_current!;

			void System.Collections.IEnumerator.Reset()
			{
				m_cursors = CreateCursors(m_items.Count, out m_min);
				m_max = m_cursors.Length - 1;
				m_current = default(T);
				Contract.Ensures(m_max >= m_min);
			}

		}

	}

}
