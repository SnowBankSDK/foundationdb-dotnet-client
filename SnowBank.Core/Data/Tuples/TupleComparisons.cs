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

// ReSharper disable MemberHidesStaticFromOuterClass

#pragma warning disable IL2091

namespace SnowBank.Data.Tuples
{
	using System.Collections;
	using SnowBank.Runtime.Converters;

	/// <summary>Helper class for tuple comparisons</summary>
	[PublicAPI]
	public static class TupleComparisons
	{

		/// <summary>Tuple comparer that treats similar values as equal ("123" = 123 = 123L = 123.0d)</summary>
		public static readonly EqualityComparer Default = new (SimilarValueComparer.Default);

		/// <summary>Tuple comparer that uses the default BCL object comparison ("123" != 123 != 123L != 123.0d)</summary>
		public static readonly EqualityComparer Bcl = new (EqualityComparer<object>.Default);

		public sealed class EqualityComparer : IEqualityComparer<IVarTuple>, IEqualityComparer
		{
			private readonly IEqualityComparer m_comparer;

			internal EqualityComparer(IEqualityComparer comparer)
			{
				m_comparer = comparer;
			}

			public bool Equals(IVarTuple? x, IVarTuple? y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (x ==  null || y == null) return false;

				return x.Equals(y, m_comparer);
			}

			public int GetHashCode(IVarTuple? obj)
			{
				return obj?.GetHashCode(m_comparer) ?? 0;
			}

			public new bool Equals(object? x, object? y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (x == null || y == null) return false;

				if (x is IVarTuple tx) return tx.Equals(y, m_comparer);
				if (y is IVarTuple ty) return ty.Equals(x, m_comparer);

				return false;
			}

			public int GetHashCode(object? obj)
			{
				if (obj == null) return 0;

				if (obj is IVarTuple t) return t.GetHashCode(m_comparer);

				// returns a hash base on the pointers
				return RuntimeHelpers.GetHashCode(obj);
			}
		}

		/// <summary>Create a new instance that compares a single item position in two tuples</summary>
		/// <typeparam name="T1">Type of the item to compare</typeparam>
		/// <param name="offset">Offset of the item to compare (can be negative)</param>
		/// <param name="comparer">Comparer for the item's type</param>
		/// <returns>New comparer instance</returns>
		public static CompositeComparer<T1> Composite<T1>(int offset = 0, IComparer<T1>? comparer = null)
		{
			return new CompositeComparer<T1>(offset, comparer);
		}

		/// <summary>Create a new instance that compares two consecutive items in two tuples</summary>
		/// <typeparam name="T1">Type of the first item to compare</typeparam>
		/// <typeparam name="T2">Type of the second item to compare</typeparam>
		/// <param name="offset">Offset of the first item to compare (can be negative)</param>
		/// <param name="comparer1">Comparer for the first item's type</param>
		/// <param name="comparer2">Comparer for the second item's type</param>
		/// <returns>New comparer instance</returns>
		public static CompositeComparer<T1, T2> Composite<T1, T2>(int offset = 0, IComparer<T1>? comparer1 = null, IComparer<T2>? comparer2 = null)
		{
			return new CompositeComparer<T1, T2>(offset, comparer1, comparer2);
		}

		/// <summary>Create a new instance that compares three consecutive items in two tuples</summary>
		/// <typeparam name="T1">Type of the first item to compare</typeparam>
		/// <typeparam name="T2">Type of the second item to compare</typeparam>
		/// <typeparam name="T3">Type of the third item to compare</typeparam>
		/// <param name="offset">Offset of the first item to compare (can be negative)</param>
		/// <param name="comparer1">Comparer for the first item's type</param>
		/// <param name="comparer2">Comparer for the second item's type</param>
		/// <param name="comparer3">Comparer for the third item's type</param>
		/// <returns>New comparer instance</returns>
		public static CompositeComparer<T1, T2, T3> Composite<T1, T2, T3>(int offset = 0, IComparer<T1>? comparer1 = null, IComparer<T2>? comparer2 = null, IComparer<T3>? comparer3 = null)
		{
			return new CompositeComparer<T1, T2, T3>(offset, comparer1, comparer2, comparer3);
		}

		/// <summary>Comparer that compares tuples with at least 1 item</summary>
		/// <typeparam name="T1">Type of the item</typeparam>
		[PublicAPI]
		public sealed class CompositeComparer<T1> : IComparer<IVarTuple>, IComparer<STuple<T1>>, IComparer<ValueTuple<T1>>
		{

			public static readonly IComparer<IVarTuple> Default = new CompositeComparer<T1>();

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer()
				: this(0, null)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer(IComparer<T1>? comparer)
				: this(0, comparer)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			/// <param name="offset">Offset in the tuples of the element to compare (can be negative)</param>
			/// <param name="comparer">Comparer for the element type</param>
			public CompositeComparer(int offset, IComparer<T1>? comparer)
			{
				this.Offset = offset;
				this.Comparer = comparer ?? Comparer<T1>.Default;
			}

			/// <summary>Offset in the tuples where the comparison starts</summary>
			/// <remarks>If negative, comparison starts from the end.</remarks>
			public int Offset { get; }

			/// <summary>Comparer for the first element (at position <see cref="Offset"/>)</summary>
			public IComparer<T1> Comparer { get; }

			/// <summary>Compare a single item in both tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(IVarTuple? x, IVarTuple? y)
			{
				if (y == null) return x == null ? 0 : +1;
				if (x == null) return -1;

				int nx = x.Count;
				int ny = y.Count;
				if (ny == 0 || nx == 0) return nx - ny;

				int p = this.Offset;
				return this.Comparer.Compare(x.Get<T1>(p)!, y.Get<T1>(p)!);
			}

			/// <summary>Compare two tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(STuple<T1> x, STuple<T1> y)
			{
				if (this.Offset != 0) throw new InvalidOperationException("Cannot compare fixed tuples with non-zero offset.");
				return this.Comparer.Compare(x.Item1, y.Item1);
			}

			/// <summary>Compare two tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(ValueTuple<T1> x, ValueTuple<T1> y)
			{
				if (this.Offset != 0) throw new InvalidOperationException("Cannot compare fixed tuples with non-zero offset.");
				return this.Comparer.Compare(x.Item1, y.Item1);
			}

		}

		/// <summary>Comparer that compares tuples with at least 2 items</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		[PublicAPI]
		public sealed class CompositeComparer<T1, T2> : IComparer<IVarTuple>, IComparer<STuple<T1, T2>>, IComparer<(T1, T2)>
		{

			public static readonly IComparer<IVarTuple> Default = new CompositeComparer<T1, T2>();

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer()
				: this(0, null, null)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer(IComparer<T1>? comparer1, IComparer<T2>? comparer2)
				: this(0, comparer1, comparer2)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			/// <param name="offset">Offset in the tuples of the first element to compare (can be negative)</param>
			/// <param name="comparer1">Comparer for the first element type</param>
			/// <param name="comparer2">Comparer for the second element type</param>
			public CompositeComparer(int offset, IComparer<T1>? comparer1, IComparer<T2>? comparer2)
			{
				this.Offset = offset;
				this.Comparer1 = comparer1 ?? Comparer<T1>.Default;
				this.Comparer2 = comparer2 ?? Comparer<T2>.Default;
			}

			/// <summary>Offset in the tuples where the comparison starts</summary>
			/// <remarks>If negative, comparison starts from the end.</remarks>
			public int Offset { get; }

			/// <summary>Comparer for the first element (at position <see cref="Offset"/>)</summary>
			public IComparer<T1> Comparer1 { get; }

			/// <summary>Comparer for the second element (at position <see cref="Offset"/> + 1)</summary>
			public IComparer<T2> Comparer2 { get; }

			/// <summary>Compare up to two items in both tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(IVarTuple? x, IVarTuple? y)
			{
				if (y == null) return x == null ? 0 : +1;
				if (x == null) return -1;

				int nx = x.Count;
				int ny = y.Count;
				if (ny == 0 || nx == 0) return nx - ny;

				int p = this.Offset;

				int cmp = this.Comparer1.Compare(x.Get<T1>(p)!, y.Get<T1>(p)!);
				if (cmp != 0) return cmp;

				if (ny == 1 || nx == 1) return nx - ny;
				cmp = this.Comparer2.Compare(x.Get<T2>(p + 1)!, y.Get<T2>(p + 1)!);

				return cmp;
			}

			/// <summary>Compare two tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(STuple<T1, T2> x, STuple<T1, T2> y)
			{
				if (this.Offset != 0) throw new InvalidOperationException("Cannot compare fixed tuples with non-zero offset.");
				int cmp = this.Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = this.Comparer2.Compare(x.Item2, y.Item2);
				return cmp;
			}

			/// <summary>Compare two tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare((T1, T2) x, (T1, T2) y)
			{
				if (this.Offset != 0) throw new InvalidOperationException("Cannot compare fixed tuples with non-zero offset.");
				int cmp = this.Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = this.Comparer2.Compare(x.Item2, y.Item2);
				return cmp;
			}

		}

		/// <summary>Comparer that compares tuples with at least 3 items</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		[PublicAPI]
		public sealed class CompositeComparer<T1, T2, T3> : IComparer<IVarTuple>, IComparer<STuple<T1, T2, T3>>, IComparer<(T1, T2, T3)>
		{

			public static readonly IComparer<IVarTuple> Default = new CompositeComparer<T1, T2, T3>();

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer()
				: this(0, null, null, null)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer(IComparer<T1>? comparer1, IComparer<T2>? comparer2, IComparer<T3>? comparer3)
				: this(0, comparer1, comparer2, comparer3)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			/// <param name="offset">Offset in the tuples of the first element to compare (can be negative)</param>
			/// <param name="comparer1">Comparer for the first element type</param>
			/// <param name="comparer2">Comparer for the second element type</param>
			/// <param name="comparer3">Comparer for the third element type</param>
			public CompositeComparer(int offset, IComparer<T1>? comparer1, IComparer<T2>? comparer2, IComparer<T3>? comparer3)
			{
				this.Offset = offset;
				this.Comparer1 = comparer1 ?? Comparer<T1>.Default;
				this.Comparer2 = comparer2 ?? Comparer<T2>.Default;
				this.Comparer3 = comparer3 ?? Comparer<T3>.Default;
			}

			/// <summary>Offset in the tuples where the comparison starts</summary>
			/// <remarks>If negative, comparison starts from the end.</remarks>
			public int Offset { get; }

			/// <summary>Comparer for the first element (at position <see cref="Offset"/>)</summary>
			public IComparer<T1> Comparer1 { get; }

			/// <summary>Comparer for the second element (at position <see cref="Offset"/> + 1)</summary>
			public IComparer<T2> Comparer2 { get; }

			/// <summary>Comparer for the third element (at position <see cref="Offset"/> + 2)</summary>
			public IComparer<T3> Comparer3 { get; }

			/// <summary>Compare up to three items in both tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(IVarTuple? x, IVarTuple? y)
			{
				if (y == null) return x == null ? 0 : +1;
				if (x == null) return -1;

				int nx = x.Count;
				int ny = y.Count;
				if (ny == 0 || nx == 0) return nx - ny;

				int p = this.Offset;

				int c = this.Comparer1.Compare(x.Get<T1>(p)!, y.Get<T1>(p)!);
				if (c != 0) return c;

				if (ny == 1 || nx == 1) return nx - ny;
				c = this.Comparer2.Compare(x.Get<T2>(p + 1)!, y.Get<T2>(p + 1)!);
				if (c != 0) return c;

				if (ny == 2 || nx == 2) return nx - ny;
				c = this.Comparer3.Compare(x.Get<T3>(p + 2)!, y.Get<T3>(p + 2)!);

				return c;
			}

			/// <summary>Compare two tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(STuple<T1, T2, T3> x, STuple<T1, T2, T3> y)
			{
				if (this.Offset != 0) throw new InvalidOperationException("Cannot compare fixed tuples with non-zero offset.");
				int cmp = this.Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = this.Comparer2.Compare(x.Item2, y.Item2);
				if (cmp == 0) cmp = this.Comparer3.Compare(x.Item3, y.Item3);
				return cmp;
			}

			/// <summary>Compare two tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare((T1, T2, T3) x, (T1, T2, T3) y)
			{
				if (this.Offset != 0) throw new InvalidOperationException("Cannot compare fixed tuples with non-zero offset.");
				int cmp = this.Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = this.Comparer2.Compare(x.Item2, y.Item2);
				if (cmp == 0) cmp = this.Comparer3.Compare(x.Item3, y.Item3);
				return cmp;
			}

		}

	}

}
