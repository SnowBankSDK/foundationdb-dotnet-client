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
	using System.ComponentModel;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime.Converters;

	/// <summary>Tuple that can hold any number of untyped items</summary>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class ListTuple<T> : IVarTuple, IComparable, ITupleFormattable
	{
		// We could use a ListTuple<T> for tuples where all items are of type T, and ListTuple could derive from ListTuple<object>.
		// => this could speed up a bit the use case of STuple.FromArray<T> or STuple.FromSequence<T>

		/// <summary>List of the items in the tuple.</summary>
		/// <remarks>It is supposed to be immutable!</remarks>
		private readonly ReadOnlyMemory<T> m_items;

		private int? m_hashCode;

		/// <summary>Create a new tuple from a sequence of items (copied)</summary>
		public ListTuple([InstantHandle] IEnumerable<T> items)
			: this(items.ToArray().AsMemory())
		{ }

		public ListTuple(T[] items, int offset, int count)
			: this(items.AsMemory(offset, count))
		{ }

		public ListTuple(T[] items)
			: this(items.AsMemory())
		{ }

		/// <summary>Wrap a List of items</summary>
		/// <remarks>The list should not mutate and should not be exposed to anyone else!</remarks>
		public ListTuple(ReadOnlyMemory<T> items)
		{
			m_items = items.Length != 0 ? items : default;
		}

		/// <summary>Create a new list tuple by merging the items of two tuples together</summary>
		public ListTuple(ListTuple<T> a, ListTuple<T> b)
		{
			Contract.NotNull(a);
			Contract.NotNull(b);

			int nA = a.Count;
			int nB = b.Count;

			var items = new T[checked(nA + nB)];

			if (nA > 0) a.CopyTo(items, 0);
			if (nB > 0) b.CopyTo(items, nA);
			m_items = items.AsMemory();
		}

		/// <summary>Create a new list tuple by merging the items of three tuples together</summary>
		public ListTuple(ListTuple<T> a, ListTuple<T> b, ListTuple<T> c)
		{
			Contract.NotNull(a);
			Contract.NotNull(b);
			Contract.NotNull(c);

			int nA = a.Count;
			int nB = b.Count;
			int nC = c.Count;

			var items = new T[checked(nA + nB + nC)];

			if (nA > 0) a.CopyTo(items, 0);
			if (nB > 0) b.CopyTo(items, nA);
			if (nC > 0) c.CopyTo(items, nA + nB);

			m_items = items;
		}

		public int Count => m_items.Length;

		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;

		/// <inheritdoc />
		object? IReadOnlyList<object?>.this[int index] => this[index];

		/// <inheritdoc />
		object? System.Runtime.CompilerServices.ITuple.this[int index] => this[index];

		/// <inheritdoc />
		object? IVarTuple.this[int index] => this[index];

		public T this[int index] => m_items.Span[TupleHelpers.MapIndex(index, m_items.Length)];

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int count = m_items.Length;
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, count) : count;

				int len = end - begin;
				if (len <= 0) return STuple.Empty;
				if (begin == 0 && len == count) return this;

				Contract.Debug.Assert(begin >= 0);
				Contract.Debug.Assert((uint) len <= count);

				return new ListTuple<T>(m_items.Slice(begin, len));
			}
		}

		/// <inheritdoc />
		object? IVarTuple.this[Index index] => this[index];

		public T this[Index index] => m_items.Span[TupleHelpers.MapIndex(index, m_items.Length)];

		/// <inheritdoc />
		public IVarTuple this[Range range]
		{
			get
			{
				(int offset, int count) = range.GetOffsetAndLength(m_items.Length);
				if (count == 0) return STuple.Empty;
				if (offset == 0 && count == this.Count) return this;
				return new ListTuple<T>(m_items.Slice(offset, count));
			}
		}

		/// <inheritdoc />
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
			=> TypeConverters.ConvertBoxed<TItem>(this[index]);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(Index index)
			=> Get<TItem>(index.GetOffset(this.Count));

		/// <inheritdoc />
		public TItem? GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
		{
			if (m_items.Length == 0) TupleHelpers.ThrowTupleIsEmpty();
			return TypeConverters.ConvertBoxed<TItem>(m_items.Span[0]);
		}

		/// <inheritdoc />
		public TItem? GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
		{
			if (m_items.Length == 0) TupleHelpers.ThrowTupleIsEmpty();
			return TypeConverters.ConvertBoxed<TItem>(m_items.Span[^1]);
		}

		/// <inheritdoc />
		public IVarTuple Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(TItem value)
		{
			return new LinkedTuple<TItem>(this, value);
		}

		/// <inheritdoc />
		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		/// <inheritdoc />
		void IVarTuple.CopyTo(object?[] array, int offset)
		{
			int p = offset;
			foreach(var item in m_items.Span)
			{
				array[p++] = item;
			}
		}

		public void CopyTo(T[] array, int offset)
		{
			m_items.Span.CopyTo(array.AsSpan(offset));
		}

		/// <inheritdoc />
		IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
		{
			for(int i = 0; i < m_items.Length; i++)
			{
				yield return m_items.Span[i];
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < m_items.Length; i++)
			{
				yield return m_items.Span[i];
			}
		}

		/// <inheritdoc />
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			return STuple.Formatter.AppendItemsTo(ref sb, m_items.Span);
		}

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure]
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			return STuple.Formatter.ToString(m_items.Span);
		}

		private bool CompareItems(ReadOnlySpan<T> theirs, IEqualityComparer comparer)
		{
			int p = 0;
			var items = m_items.Span;
			int count = items.Length;
			foreach (var item in theirs)
			{
				if (p >= count) return false;

				if (item == null)
				{
					if (items[p] != null) return false;
				}
				else
				{
					if (!comparer.Equals(item, items[p])) return false;
				}
				p++;
			}
			return p >= count;
		}

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		/// <inheritdoc />
		public bool Equals(IVarTuple? other)
		{
			return !ReferenceEquals(other, null) && ((IStructuralEquatable) this).Equals(other, SimilarValueComparer.Default);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		/// <inheritdoc />
		public int CompareTo(IVarTuple? other) => TupleHelpers.Compare(this, other, SimilarValueComparer.Default);

		/// <inheritdoc />
		public int CompareTo(object? other) => TupleHelpers.Compare(this, other, SimilarValueComparer.Default);

		/// <inheritdoc />
		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => TupleHelpers.Compare(this, other, comparer);

		/// <inheritdoc />
		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			if (ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			if (other is ListTuple<T> list)
			{
				return list.Count == this.Count && CompareItems(list.m_items.Span, comparer);
			}

			return TupleHelpers.Equals(this, other, comparer);
		}

		/// <inheritdoc />
		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			// the cached hashcode is only valid for the default comparer!
			bool canUseCache = ReferenceEquals(comparer, SimilarValueComparer.Default);
			if (m_hashCode.HasValue && canUseCache)
			{
				return m_hashCode.Value;
			}

			var items = m_items.Span;
			var h = items.Length switch
			{
				0 => 0,
				1 => TupleHelpers.ComputeHashCode(items[0], comparer),
				2 => TupleHelpers.CombineHashCodes(TupleHelpers.ComputeHashCode(items[0], comparer), TupleHelpers.ComputeHashCode(items[1], comparer)),
				_ => TupleHelpers.CombineHashCodes(items.Length, TupleHelpers.ComputeHashCode(items[0], comparer), TupleHelpers.ComputeHashCode(items[^2], comparer), TupleHelpers.ComputeHashCode(items[^1], comparer))
			};
			if (canUseCache) m_hashCode = h;
			return h;
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			return TupleHelpers.ComputeHashCode(m_items.Span[index], comparer);
		}

	}

}
