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

	/// <summary>Tuple that adds a value at the end of an already existing tuple</summary>
	/// <typeparam name="T">Type of the last value of the tuple</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class LinkedTuple<T> : IVarTuple, IComparable, ITupleFormattable
	{
		//TODO: consider changing this to a struct ?

		// Used in scenario where we will append keys to a common base tuple
		// note: linked list are not very efficient, but we do not expect a very long chain, and the head will usually be a subspace or memoized tuple

		/// <summary>Value of the last element of the tuple</summary>
		public readonly T Tail;

		/// <summary>Link to the parent tuple that contains the head.</summary>
		public readonly IVarTuple Head;

		/// <summary>Cached size of the size of the Head tuple. Add 1 to get the size of this tuple.</summary>
		private readonly int HeadCount;

		/// <summary>Append a new value at the end of an existing tuple</summary>
		[SkipLocalsInit, DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public LinkedTuple(IVarTuple head, T tail)
		{
			Contract.NotNull(head);

			this.Head = head;
			this.Tail = tail;
			this.HeadCount = head.Count;
		}

		/// <summary>Returns the number of elements in this tuple</summary>
		public int Count => this.HeadCount + 1;

#if !NETSTANDARD2_0
		// ITuple is not visible to netstandard2.0
		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;
#endif

		/// <inheritdoc cref="IVarTuple.this[int]"/>
		public object? this[int index]
		{
			get
			{
				if (index == this.HeadCount || index == -1) return this.Tail;
				if (index < -1) index++;
				return this.Head[index];
			}
		}

		/// <inheritdoc />
		public IVarTuple this[int? fromIncluded, int? toExcluded] => TupleHelpers.Splice(this, fromIncluded, toExcluded);

		/// <inheritdoc />
		public object? this[Index index]
		{
			get
			{
				int p = TupleHelpers.MapIndex(index, this.HeadCount + 1);
				if (p == this.HeadCount) return this.Tail;
				return this.Head[p];
			}
		}

		/// <inheritdoc />
		public IVarTuple this[Range range]
		{
			get
			{
				int d = this.HeadCount;
				(int offset, int count) = range.GetOffsetAndLength(d + 1);
				if (count == 0) return STuple.Empty;
				if (count == 1 && offset == d) return new STuple<T>(this.Tail);
				if (offset == 0)
				{
					if (count == d + 1) return this;
					if (count == d) return this.Head;
				}
				return TupleHelpers.Splice(this, range);
			}
		}

		/// <inheritdoc />
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
		{
			if (index == this.HeadCount || index == -1) return TypeConverters.Convert<T, TItem?>(this.Tail);
			if (index < -1) index++;
			return this.Head.Get<TItem>(index);
		}
		
		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(Index index)
			=> Get<TItem>(index.GetOffset(this.Count));

		/// <inheritdoc />
		public TItem? GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			=> this.HeadCount > 0 ? this.Head.GetFirst<TItem>() : TypeConverters.Convert<T, TItem?>(this.Tail);

		/// <inheritdoc />
		public TItem? GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			=> TypeConverters.Convert<T, TItem?>(this.Tail);

		public T Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: MaybeNull]
			get => this.Tail;
		}

		/// <inheritdoc />
		public IVarTuple Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(TItem value)
		{
			return STuple.Concat(this.Head, new STuple<T, TItem>(this.Tail, value));
		}

		/// <inheritdoc />
		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		/// <inheritdoc />
		public void CopyTo(object?[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			array[offset + this.HeadCount] = this.Tail;
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			foreach (var item in this.Head)
			{
				yield return item;
			}
			yield return this.Tail;
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			// cannot be empty

			int n = 0;
			foreach (var item in this.Head)
			{
				if (n > 0)
				{
					sb.Append(", ");
				}
				STuple.Formatter.StringifyBoxedTo(ref sb, item);
				++n;
			}

			if (n > 0)
			{
				sb.Append(", ");
			}
			STuple.Formatter.StringifyTo(ref sb, this.Tail);

			return n + 1;
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
		public override bool Equals(object? obj)
		{
			return obj != null && ((IStructuralEquatable) this).Equals(obj, SimilarValueComparer.Default);
		}

		/// <inheritdoc />
		public bool Equals(IVarTuple? other)
		{
			return !ReferenceEquals(other, null) && ((IStructuralEquatable) this).Equals(other, SimilarValueComparer.Default);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return ((IStructuralEquatable) this).GetHashCode(SimilarValueComparer.Default);
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

			if (other is LinkedTuple<T> linked)
			{
				// must have same length
				if (linked.Count != this.Count) return false;
				// compare the tail before
				if (!comparer.Equals(this.Tail, linked.Tail)) return false;
				// compare the rest
				return this.Head.Equals(linked.Tail, comparer);
			}

			return TupleHelpers.Equals(this, other, comparer);
		}

		/// <inheritdoc />
		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			int hc = this.Head.Count;
			return hc switch
			{
				0 => TupleHelpers.ComputeHashCode(this.Tail, comparer),
				1 => TupleHelpers.CombineHashCodes(this.Head.GetItemHashCode(0, comparer), TupleHelpers.ComputeHashCode(this.Tail, comparer)),
				_ => TupleHelpers.CombineHashCodes(this.Count, this.Head.GetItemHashCode(0, comparer), this.Head.GetItemHashCode(hc - 1, comparer), TupleHelpers.ComputeHashCode(this.Tail, comparer))
			};
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			int hc = this.Head.Count;
			if (index < hc) return this.Head.GetItemHashCode(index, comparer);
			if (index == hc) return TupleHelpers.ComputeHashCode(this.Tail, comparer);
			throw new IndexOutOfRangeException();
		}

	}

}
