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

namespace SnowBank.Data.Tuples
{
	using System.Collections;
	using SnowBank.Runtime;

	/// <summary>Helpers for writing custom <see cref="IVarTuple"/> implementations</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class TupleHelpers
	{

		/// <summary>Default (non-optimized) implementation of <c>ITuple.this[long?, long?]</c></summary>
		/// <param name="tuple">Tuple to slice</param>
		/// <param name="fromIncluded">Start offset of the section (included)</param>
		/// <param name="toExcluded">End offset of the section (included)</param>
		/// <returns>New tuple only containing items inside this section</returns>
		[Pure]
		public static IVarTuple Splice(IVarTuple tuple, int? fromIncluded, int? toExcluded)
		{
			Contract.Debug.Requires(tuple != null);
			int count = tuple.Count;
			if (count == 0) return STuple.Empty;

			int start = fromIncluded.HasValue ? MapIndexBounded(fromIncluded.Value, count) : 0;
			int end = toExcluded.HasValue ? MapIndexBounded(toExcluded.Value, count) : count;

			int len = end - start;

			if (len <= 0) return STuple.Empty;
			if (start == 0 && len == count) return tuple;
			switch (len)
			{
				case 1:
					return new ListTuple<object?>([ tuple[start] ]);
				case 2:
					return new ListTuple<object?>([ tuple[start], tuple[start + 1] ]);
				default:
				{
					var items = new object?[len];
					//note: can be slow for tuples using linked-lists, but hopefully they will have their own Slice implementation...
					int q = start;
					for (int p = 0; p < items.Length; p++)
					{
						items[p] = tuple[q++];
					}
					return new ListTuple<object?>(items.AsMemory());
				}
			}
		}

		/// <summary>Default (non-optimized) implementation of <c>ITuple.this[Range]</c></summary>
		/// <param name="tuple">Tuple to slice</param>
		/// <param name="range">Range to select</param>
		/// <returns>New tuple only containing items inside this section</returns>
		[Pure]
		public static IVarTuple Splice(IVarTuple tuple, Range range)
		{
			Contract.Debug.Requires(tuple != null);
			int count = tuple.Count;

			(int start, int len) = range.GetOffsetAndLength(count);
			if (len == 0) return STuple.Empty;
			if (start == 0 && len == count) return tuple;
			switch (len)
			{
				case 1:
				{
					return new ListTuple<object?>([ tuple[start] ]);
				}
				case 2:
				{
					return new ListTuple<object?>([ tuple[start], tuple[start + 1] ]);
				}
				default:
				{
					var items = new object?[len];
					//note: can be slow for tuples using linked-lists, but hopefully they will have their own Slice implementation...
					int q = start;
					for (int p = 0; p < items.Length; p++)
					{
						items[p] = tuple[q++];
					}
					return new ListTuple<object?>(items.AsMemory());
				}
			}
		}

		/// <summary>Default (non-optimized) implementation for <c>ITuple.StartsWith()</c></summary>
		/// <param name="a">Larger tuple</param>
		/// <param name="b">Smaller tuple</param>
		/// <returns>True if <paramref name="a"/> starts with (or is equal to) <paramref name="b"/></returns>
		public static bool StartsWith(IVarTuple a, IVarTuple b)
		{
			Contract.Debug.Requires(a != null && b != null);
			if (object.ReferenceEquals(a, b)) return true;
			int an = a.Count;
			int bn = b.Count;

			if (bn > an) return false;
			if (bn == 0) return true; // note: 'an' can only be 0 because of previous test

			for (int i = 0; i < bn; i++)
			{
				if (!object.Equals(a[i], b[i])) return false;
			}
			return true;
		}

		/// <summary>Default (non-optimized) implementation for <c>ITuple.EndsWith()</c></summary>
		/// <param name="a">Larger tuple</param>
		/// <param name="b">Smaller tuple</param>
		/// <returns>True if <paramref name="a"/> starts with (or is equal to) <paramref name="b"/></returns>
		public static bool EndsWith(IVarTuple a, IVarTuple b)
		{
			Contract.Debug.Requires(a != null && b != null);
			if (object.ReferenceEquals(a, b)) return true;
			int an = a.Count;
			int bn = b.Count;

			if (bn > an) return false;
			if (bn == 0) return true; // note: 'an' can only be 0 because of previous test

			int offset = an - bn;
			for (int i = 0; i < bn; i++)
			{
				if (!object.Equals(a[offset + i], b[i])) return false;
			}
			return true;
		}

		/// <summary>Helper to copy the content of a tuple at a specific position in an array</summary>
		/// <returns>Updated offset just after the last element of the copied tuple</returns>
		public static int CopyTo(IVarTuple tuple, object?[] array, int offset)
		{
			Contract.Debug.Requires(tuple != null && array != null && offset >= 0);

			foreach (var item in tuple)
			{
				array[offset++] = item;
			}
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple, or exception if outside the tuple</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the absolute index is outside the tuple (&lt;0 or &gt;=<paramref name="count"/>)</exception>
		[Pure]
		public static int MapIndex(int index, int count)
		{
			Contract.Debug.Requires(count >= 0);
			int offset = index;
			if (offset < 0)
			{
#if DEBUG
				// we are attempting to phase out negative indexing (legacy)!
				// please use the new Index type and syntax: tuple[-1] => tuple[^1]
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				offset += count;
			}

			return (uint) offset < count ? offset : FailIndexOutOfRange<int>(index, count);
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple, or exception if outside the tuple</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the absolute index is outside the tuple (&lt;0 or &gt;=<paramref name="count"/>)</exception>
		[Pure]
		public static int MapIndex(Index index, int count)
		{
			int offset = index.GetOffset(count);
			if (offset < 0 || offset >= count) return FailIndexOutOfRange<int>(index, count);
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple. Truncated to 0 if index is before the start of the tuple, or to <paramref name="count"/> if the index is after the end of the tuple</returns>
		[Pure]
		public static int MapIndexBounded(int index, int count)
		{
			if (index < 0) index += count;
			return Math.Max(Math.Min(index, count), 0);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException FailTupleIsEmpty() => new("Tuple is empty.");

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowTupleIsEmpty() => throw FailTupleIsEmpty();

		[DoesNotReturn, StackTraceHidden]
		public static T FailIndexOutOfRange<T>(int index, int count)
		{
			if (count == 0)
			{
				throw FailTupleIsEmpty();
			}
			throw new IndexOutOfRangeException($"Index {index} is outside of the tuple range (0..{count - 1})");
		}

		[DoesNotReturn, StackTraceHidden]
		public static T FailIndexOutOfRange<T>(Index index, int count)
		{
			throw new IndexOutOfRangeException($"Index {index} is outside of the tuple range (0..{count - 1})");
		}

		public static bool Equals<TTuple>(TTuple? left, object? other, IEqualityComparer comparer)
			where TTuple : class, IVarTuple
		{
			return left is null ? other is null : other switch
			{
				IVarTuple t => Equals(left, t, comparer),
				ITuple t => Equals(left, t, comparer),
				_ => false
			};
		}

		public static bool Equals<TTuple>(in TTuple left, object? other, IEqualityComparer comparer)
			where TTuple : struct, IVarTuple
		{
			return other switch
			{
				IVarTuple t => Equals(in left, t, comparer),
				ITuple t => Equals(in left, t, comparer),
				_ => false
			};
		}

		public static bool Equals<TTuple>(TTuple? x, IVarTuple? y, IEqualityComparer comparer)
			where TTuple : class, IVarTuple
		{
			if (object.ReferenceEquals(x, y)) return true;
			if (x is null || y is null) return false;

			if (x.Count != y.Count) return false;

			using var xs = x.GetEnumerator();
			using var ys = y.GetEnumerator();

			while (xs.MoveNext())
			{
				if (!ys.MoveNext()) return false;
				if (!comparer.Equals(xs.Current, ys.Current)) return false;
			}

			return !ys.MoveNext();
		}

		public static bool Equals<TTuple>(in TTuple x, IVarTuple? y, IEqualityComparer comparer)
			where TTuple : struct, IVarTuple
		{
			if (y is null) return false;

			if (x.Count != y.Count) return false;

			using var xs = x.GetEnumerator();
			using var ys = y.GetEnumerator();

			while (xs.MoveNext())
			{
				if (!ys.MoveNext()) return false;
				if (!comparer.Equals(xs.Current, ys.Current)) return false;
			}

			return !ys.MoveNext();
		}

		public static bool Equals<TTuple>(TTuple? x, ITuple? y, IEqualityComparer comparer)
			where TTuple : class, IVarTuple
		{
			if (object.ReferenceEquals(x, y)) return true;
			if (x is null || y is null) return false;

			int len = y.Length;
			if (x.Count != len) return false;

			using var xs = x.GetEnumerator();

			int i = 0;
			while (xs.MoveNext())
			{
				if (i >= len) return false;
				if (!comparer.Equals(xs.Current, y[i++])) return false;
			}

			return i == len;
		}

		public static bool Equals<TTuple>(in TTuple x, ITuple? y, IEqualityComparer comparer)
			where TTuple : struct, IVarTuple
		{
			if (y is null) return false;

			int len = y.Length;
			if (x.Count != len) return false;

			using var xs = x.GetEnumerator();

			int i = 0;
			while (xs.MoveNext())
			{
				if (i >= len) return false;
				if (!comparer.Equals(xs.Current, y[i++])) return false;
			}

			return i == len;
		}

		public static int Compare<TTuple>(TTuple? left, object? other, IComparer comparer)
			where TTuple : class, IVarTuple
		{
			return left is null ? (other is null ? 0 : -1) : other switch
			{
				null => +1,
				IVarTuple t => Compare(left, t, comparer),
				ITuple t => Compare(left, t, comparer),
				_ => throw new ArgumentException($"Cannot compare {left.GetType().GetFriendlyName()} with an instance of {other.GetType().GetFriendlyName()}", nameof(other)),
			};
		}

		public static int Compare<TTuple>(in TTuple left, object? other, IComparer comparer)
			where TTuple : struct, IVarTuple
			=> other switch
			{
				null => +1,
				IVarTuple t => Compare(left, t, comparer),
				ITuple t => Compare(left, t, comparer),
				_ => throw new ArgumentException($"Cannot compare {left.GetType().GetFriendlyName()} with an instance of {other.GetType().GetFriendlyName()}", nameof(other)),
			};

		public static int Compare<TTuple>(TTuple? x, IVarTuple? y, IComparer comparer)
			where TTuple : class, IVarTuple
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x is null) return -1;
			if (y is null) return +1;

			using var xs = x.GetEnumerator();
			using var ys = y.GetEnumerator();

			while (xs.MoveNext())
			{
				if (!ys.MoveNext()) return +1;
				int cmp = comparer.Compare(xs.Current, ys.Current);
				if (cmp != 0) return cmp;
			}

			return !ys.MoveNext() ? 0 : -1;
		}

		public static int Compare<TTuple>(in TTuple x, IVarTuple? y, IComparer comparer)
			where TTuple : struct, IVarTuple
		{
			if (y is null) return +1;

			using var xs = x.GetEnumerator();
			using var ys = y.GetEnumerator();

			while (xs.MoveNext())
			{
				if (!ys.MoveNext()) return +1;
				int cmp = comparer.Compare(xs.Current, ys.Current);
				if (cmp != 0) return cmp;
			}

			return !ys.MoveNext() ? 0 : -1;
		}

		public static int Compare<TTuple>(TTuple? x, ITuple? y, IComparer comparer)
			where TTuple : class, IVarTuple
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x is null) return -1;
			if (y is null) return +1;

			using var xs = x.GetEnumerator();

			int i = 0;
			int len = y.Length;

			while (xs.MoveNext())
			{
				if (i >= len) return +1;
				int cmp = comparer.Compare(xs.Current, y[i++]);
				if (cmp != 0) return cmp;
			}

			return i == len ? 0 : -1;
		}

		public static int Compare<TTuple>(in TTuple x, ITuple? y, IComparer comparer)
			where TTuple : struct, IVarTuple
		{
			if (y is null) return +1;

			using var xs = x.GetEnumerator();

			int i = 0;
			int len = y.Length;

			while (xs.MoveNext())
			{
				if (i >= len) return +1;
				int cmp = comparer.Compare(xs.Current, y[i++]);
				if (cmp != 0) return cmp;
			}

			return i == len ? 0 : -1;
		}

		public static int StructuralGetHashCode(IVarTuple? tuple, IEqualityComparer comparer)
		{
			Contract.Debug.Requires(comparer != null);

			if (tuple == null)
			{
				return comparer.GetHashCode(null!);
			}

			var hc = new HashCode();
			foreach (var item in tuple)
			{
				hc.Add(comparer.GetHashCode(item!));
			}
			return hc.ToHashCode();
		}

		public static int StructuralCompare(IVarTuple? x, IVarTuple? y, IComparer comparer)
		{
			Contract.Debug.Requires(comparer != null);

			if (object.ReferenceEquals(x, y)) return 0;
			if (x ==  null) return -1;
			if (y == null) return +1;

			using var xs = x.GetEnumerator();
			using var ys = y.GetEnumerator();

			while (xs.MoveNext())
			{
				if (!ys.MoveNext()) return 1;

				int cmp = comparer.Compare(xs.Current, ys.Current);
				if (cmp != 0) return cmp;

			}

			return ys.MoveNext() ? -1 : 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int ComputeHashCode<T>(T? value, IEqualityComparer comparer)
		{
			return value is not null ? comparer.GetHashCode(value) : -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int CombineHashCodes(int h1, int h2)
		{
			return HashCode.Combine(2, h1, h2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int CombineHashCodes(int count, int h1, int h2, int h3)
		{
			return HashCode.Combine(count, h1, h2, h3);
		}

	}

}
