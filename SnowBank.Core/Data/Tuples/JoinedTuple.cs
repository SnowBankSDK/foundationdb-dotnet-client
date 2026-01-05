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

	/// <summary>Tuple that represents the concatenation of two tuples</summary>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class JoinedTuple<THead, TTail> : IVarTuple, IComparable, ITupleFormattable
		where THead : IVarTuple
		where TTail : IVarTuple
	{
		// Uses cases: joining a 'subspace' tuple (customerId, 'Users', ) with a 'key' tuple (userId, 'Contacts', 123, )

		/// <summary>First tuple (first N items)</summary>
		public readonly THead Head;

		/// <summary>Second tuple (last M items)</summary>
		public readonly TTail Tail;

		/// <summary>Offset at which the Tail tuple starts. Items are in Head tuple if index &lt; split. Items are in Tail tuple if index &gt;= split.</summary>
		private readonly int HeadCount;

		public JoinedTuple(THead head, TTail tail)
		{
			Contract.NotNull(head);
			Contract.NotNull(tail);

			this.Head = head;
			this.Tail = tail;
			this.HeadCount = head.Count;
			this.Count = this.HeadCount + tail.Count;
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			// cannot be empty

			int n = 0;

			if (this.Head is ITupleFormattable h)
			{
				n = h.AppendItemsTo(ref sb);
			}
			else
			{
				foreach (var item in this.Head)
				{
					if (n > 0)
					{
						sb.Append(", ");
					}
					STuple.Formatter.StringifyBoxedTo(ref sb, item);
					++n;
				}
			}

			if (n > 0 && this.Tail.Count > 0)
			{
				sb.Append(", ");
			}

			if (this.Tail is ITupleFormattable t)
			{
				n += t.AppendItemsTo(ref sb);
			}
			else
			{
				foreach (var item in this.Tail)
				{
					if (n > 0)
					{
						sb.Append(", ");
					}
					STuple.Formatter.StringifyBoxedTo(ref sb, item);
					++n;
				}
			}

			return n;
		}

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure]
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			var sb = new FastStringBuilder(stackalloc char[128]);
			sb.Append('(');
			if (((ITupleFormattable)this).AppendItemsTo(ref sb) == 1)
			{
				sb.Append(",)");
			}
			else
			{
				sb.Append(')');
			}
			return sb.ToString();
		}

		/// <inheritdoc />
		public int Count { get; }

		/// <inheritdoc />
		int ITuple.Length => this.Count;

		/// <inheritdoc cref="IVarTuple.this[int]"/>
		public object? this[int index]
		{
			get
			{
				int p = TupleHelpers.MapIndex(index, this.Count);
				return p < this.HeadCount ? this.Head[p] : this.Tail[p - this.HeadCount];
			}
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, this.Count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, this.Count) : this.Count;

				if (end <= begin) return STuple.Empty;

				int p = this.Head.Count;
				if (begin >= p)
				{ // all selected items are in the tail
					return this.Tail[begin - p, end - p];
				}
				if (end <= p)
				{ // all selected items are in the head
					return this.Head[begin, end];
				}
				// selected items are both in head and tail
				var head = this.Head[begin, null];
				var tail = this.Tail[null, end - p];
				return new JoinedTuple<IVarTuple, IVarTuple>(head, tail);
			}
		}

		public object? this[Index index]
		{
			get
			{
				int p = TupleHelpers.MapIndex(index, this.Count);
				return p < this.HeadCount ? this.Head[p] : this.Tail[p - this.HeadCount];
			}
		}

		public IVarTuple this[Range range]
		{
			get
			{
				int lenHead = this.Head.Count;
				int lenTail = this.Tail.Count;
				(int offset, int count) = range.GetOffsetAndLength(lenHead + lenTail);
				if (count == 0) return STuple.Empty;
				if (offset == 0)
				{
					if (count == lenHead + lenTail) return this;
					if (count == lenHead) return this.Head;
				}
				if (offset == lenHead && count == lenTail)
				{
					return this.Tail;
				}
				return TupleHelpers.Splice(this, range);
			}
		}

		/// <inheritdoc />
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
		{
			index = TupleHelpers.MapIndex(index, this.Count);
			return index < this.HeadCount ? this.Head.Get<TItem>(index) : this.Tail.Get<TItem>(index - this.HeadCount);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(Index index)
			=> Get<TItem>(index.GetOffset(this.Count));

		/// <inheritdoc />
		public T? GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
			=> this.HeadCount > 0 ? this.Head.GetFirst<T>() : this.Tail.GetFirst<T>();

		/// <inheritdoc />
		public T? GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
			=> this.HeadCount >= this.Count ? this.Head.GetLast<T>() : this.Tail.GetLast<T>();

		/// <inheritdoc />
		IVarTuple IVarTuple.Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T value)
			=> new LinkedTuple<T>(this, value);

		public LinkedTuple<T> Append<T>(T value)
			=> new(this, value);

		/// <inheritdoc />
		public IVarTuple Concat(IVarTuple tuple)
		{
			Contract.NotNull(tuple);

			int n1 = tuple.Count;
			if (n1 == 0) return this;

			int n2 = this.Count;

			if (n1 + n2 >= 10)
			{ // it's getting big, merge to a new List tuple
				return STuple.Concat(this.Head, this.Tail, tuple);
			}
			// REVIEW: should we always concat with the tail?
			return STuple.Concat(this.Head, this.Tail.Concat(tuple));
		}

		/// <inheritdoc />
		public void CopyTo(object?[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			this.Tail.CopyTo(array, offset + this.HeadCount);
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			foreach (var item in this.Head)
			{
				yield return item;
			}
			foreach (var item in this.Tail)
			{
				yield return item;
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
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
			if (other is null) return false;

			if (other is IVarTuple tuple)
			{
				if (tuple.Count != this.Count) return false;

				using (var iter = tuple.GetEnumerator())
				{
					foreach (var item in this.Head)
					{
						if (!iter.MoveNext() || !comparer.Equals(item, iter.Current)) return false;
					}
					foreach (var item in this.Tail)
					{
						if (!iter.MoveNext() || !comparer.Equals(item, iter.Current)) return false;
					}
					return !iter.MoveNext();
				}
			}

			return false;
		}

		/// <inheritdoc />
		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			int tc = this.Tail.Count;
			return tc switch
			{
				0 => this.Head.GetHashCode(comparer),
				1 => this.HeadCount switch
				{
					0 => this.Tail.GetHashCode(comparer),
					1 => TupleHelpers.CombineHashCodes(this.Head.GetItemHashCode(0, comparer), this.Tail.GetItemHashCode(0, comparer)),
					_ => TupleHelpers.CombineHashCodes(this.Count, this.Head.GetItemHashCode(0, comparer), this.Tail.GetItemHashCode(tc - 2, comparer), this.Tail.GetItemHashCode(tc - 1, comparer))
				},
				_ => this.HeadCount switch
				{
					0 => this.Tail.GetHashCode(comparer),
					_ => TupleHelpers.CombineHashCodes(this.Count, this.Head.GetItemHashCode(0, comparer), this.Tail.GetItemHashCode(tc - 2, comparer), this.Tail.GetItemHashCode(tc - 1, comparer))
				}
			};
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			if (index < this.HeadCount) return this.Head.GetItemHashCode(index, comparer);
			if (index < this.Count) return this.Tail.GetItemHashCode(index - this.HeadCount, comparer);
			throw new IndexOutOfRangeException();
		}

	}

}
