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

#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. The generic parameter of the source method or type does not have matching annotations.

namespace SnowBank.Data.Tuples
{
	using System.Collections;
	using System.ComponentModel;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Binary;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime.Converters;

	/// <summary>Tuple that holds only one item</summary>
	/// <typeparam name="T1">Type of the item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public readonly struct STuple<T1> : IVarTuple
		, IEquatable<STuple<T1>>, IComparable<STuple<T1>>
		, IEquatable<ValueTuple<T1>>, IComparable<ValueTuple<T1>>
		, IComparable
		, ITupleFormattable
		, ITupleSpanPackable
		, ISpanFormattable
		, ISpanEncodable
	{
		// This is mostly used by code that create a lot of temporary singleton, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First and only item in the tuple</summary>
		public readonly T1 Item1;

		/// <summary>Constructs a <see cref="STuple{T1}"/></summary>
		[SkipLocalsInit, DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple(T1 item1)
		{
			this.Item1 = item1;
		}

		/// <inheritdoc />
		public int Count => 1;

		/// <summary>Returns the element at the specified index</summary>
		[Pure, EditorBrowsable(EditorBrowsableState.Never)]
		public object? this[int index]
			=> index is 0 or -1 ? this.Item1 : TupleHelpers.FailIndexOutOfRange<object>(index, 1);

		/// <inheritdoc />
		object? IReadOnlyList<object?>.this[int index] => this[index];

		/// <inheritdoc />
		int ITuple.Length => 1;

		/// <inheritdoc />
		object? ITuple.this[int index] => this[index];

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public IVarTuple this[int? fromIncluded, int? toExcluded] => TupleHelpers.Splice(this, fromIncluded, toExcluded);

		/// <inheritdoc />
		object? IVarTuple.this[Index index] => index.GetOffset(1) switch
		{
			0 => this.Item1,
			_ => TupleHelpers.FailIndexOutOfRange<object>(index.Value, 1)
		};

		/// <inheritdoc />
		public IVarTuple this[Range range]
		{
			get
			{
				(_, int count) = range.GetOffsetAndLength(1);
				return count == 0 ? STuple.Empty : this;
			}
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
			=> index is 0 or -1 ? TypeConverters.Convert<T1, TItem?>(this.Item1) : TupleHelpers.FailIndexOutOfRange<TItem>(index, 1);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(Index index)
			=> Get<TItem>(index.GetOffset(1));

		/// <inheritdoc />
		TItem? IVarTuple.GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			where TItem : default
			=> TypeConverters.Convert<T1, TItem?>(this.Item1);

		/// <inheritdoc />
		TItem? IVarTuple.GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			where TItem : default
			=> TypeConverters.Convert<T1, TItem?>(this.Item1);

		/// <inheritdoc />
		IVarTuple IVarTuple.Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(T2 value)
			where T2 : default
			=> new STuple<T1, T2>(this.Item1, value);

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2> Append<T2>(T2 value)
		{
			return new(this.Item1, value);
		}

		/// <summary>Appends two new items at the end of the current tuple.</summary>
		/// <param name="value1">First item that will be added as an embedded item</param>
		/// <param name="value2">Second item that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If any of <paramref name="value1"/> or <paramref name="value2"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Append<T2, T3>(T2 value1, T3 value2)
		{
			return new STuple<T1, T2, T3>(this.Item1, value1, value2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2> Concat<T2>(STuple<T2> tuple)
		{
			return new STuple<T1, T2>(this.Item1, tuple.Item1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Concat<T2, T3>(STuple<T2, T3> tuple)
		{
			return new STuple<T1, T2, T3>(this.Item1, tuple.Item1, tuple.Item2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4> Concat<T2, T3, T4>(STuple<T2, T3, T4> tuple)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3);
		}

		/// <summary>Copy the item of this singleton into an array at the specified offset</summary>
		public void CopyTo(object?[] array, int offset)
		{
			array[offset] = this.Item1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out T1 item1)
		{
			item1 = this.Item1;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With(Action<T1> lambda)
		{
			lambda(this.Item1);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>(Func<T1, TItem> lambda)
		{
			return lambda(this.Item1);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void ITuplePackable.PackTo(TupleWriter writer)
		{
			TuplePackers.SerializeTo<T1>(writer, this.Item1);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ITupleSpanPackable.TryPackTo(ref TupleSpanWriter writer)
		{
			return TuplePackers.TrySerializeTo<T1>(ref writer, this.Item1);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ITupleSpanPackable.TryGetSizeHint(bool embedded, out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(in this.Item1, embedded, out var size))
			{
				sizeHint = 0;
				return false;
			}
			sizeHint = embedded ? (size + 2) : size;
			return true;
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			STuple.Formatter.StringifyTo(ref sb, this.Item1);
			return 1;
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			yield return this.Item1;
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			// singleton tuples end with a trailing ','
			var sb = new FastStringBuilder(stackalloc char[128]);
			sb.Append('(');
			STuple.Formatter.StringifyTo(ref sb, this.Item1);
			sb.Append(",)");
			return sb.ToString();
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			var buffer = destination;
			if (!buffer.TryAppendAndAdvance('(')) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item1)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(",)")) goto too_small;
			charsWritten = destination.Length - buffer.Length;
			return true;
		too_small:
			charsWritten = 0;
			return false;
		}

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			STuple<T1> t => EqualityComparer.Equals(in this, in t),
			ValueTuple<T1> t => EqualityComparer.Equals(in this, in t),
			IVarTuple t => TupleHelpers.Equals(in this, t, SimilarValueComparer.Default),
			_ => false,
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => other switch
		{
			null => false,
			STuple<T1> t => EqualityComparer.Equals(in this, in t),
			_ => TupleHelpers.Equals(this, other, SimilarValueComparer.Default),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1> other)
		{
			return EqualityComparer.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ValueTuple<T1> other)
		{
			return EqualityComparer.Equals(in this, in other);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return EqualityComparer.GetHashCode(in this);
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer) => index switch
		{
			0 => TupleHelpers.ComputeHashCode(this.Item1, comparer),
			_ => throw new IndexOutOfRangeException()
		};

		/// <inheritdoc />
		public int CompareTo(STuple<T1> other) => Comparer.Compare(in this, in other);

		/// <inheritdoc />
		public int CompareTo(ValueTuple<T1> other) => Comparer.Compare(in this, in other);

		/// <inheritdoc />
		public int CompareTo(IVarTuple? other) => other switch
		{
			null => +1,
			STuple<T1> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(in this, other, SimilarValueComparer.Default),
		};

		int IComparable.CompareTo(object? other) => other switch
		{
			null => +1,
			STuple<T1> t => Comparer.Compare(in this, in t),
			ValueTuple<T1> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(in this, other, SimilarValueComparer.Default),
		};

		public static bool operator ==(STuple<T1> left, STuple<T1> right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator !=(STuple<T1> left, STuple<T1> right)
			=> !EqualityComparer.Equals(in left, in right);

		public static bool operator ==(STuple<T1> left, ValueTuple<T1> right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator !=(STuple<T1> left, ValueTuple<T1> right)
			=> !EqualityComparer.Equals(in left, in right);

		public static bool operator ==(ValueTuple<T1> left, STuple<T1> right)
			=> EqualityComparer.Equals(in right, in left);

		public static bool operator !=(ValueTuple<T1> left, STuple<T1> right)
			=> !EqualityComparer.Equals(in right, in left);

		/// <inheritdoc />
		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) => other switch
		{
			null => false,
			STuple<T1> t => comparer.Equals(this.Item1, t.Item1),
			ValueTuple<T1> t => comparer.Equals(this.Item1, t.Item1),
			_ => TupleHelpers.Equals(this, other, comparer)
		};

		/// <inheritdoc />
		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return TupleHelpers.ComputeHashCode(this.Item1, comparer);
		}

		/// <inheritdoc />
		int IStructuralComparable.CompareTo(object? other, IComparer comparer)
		{
			return TupleHelpers.Compare(this, other, SimilarValueComparer.Default);
		}

		[Pure]
		public static implicit operator STuple<T1>(Tuple<T1> t)
		{
			Contract.NotNull(t);
			return new STuple<T1>(t.Item1);
		}

		[Pure]
		public static explicit operator Tuple<T1>(STuple<T1> t)
		{
			return new Tuple<T1>(t.Item1);
		}

		public void Fill(ref ValueTuple<T1> t)
		{
			t.Item1 = this.Item1;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2> Concat<T2>(ValueTuple<T2> tuple)
		{
			return new STuple<T1, T2>(this.Item1, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3> Concat<T2, T3>((T2, T3) tuple)
		{
			return new STuple<T1, T2, T3>(this.Item1, tuple.Item1, tuple.Item2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4> Concat<T2, T3, T4>((T2, T3, T4) tuple)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5> Concat<T2, T3, T4, T5>((T2, T3, T4, T5) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6> Concat<T2, T3, T4, T5, T6>((T2, T3, T4, T5, T6) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7> Concat<T2, T3, T4, T5, T6, T7>((T2, T3, T4, T5, T6, T7) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6, T7>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7, T8> Concat<T2, T3, T4, T5, T6, T7, T8>((T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6, T7, T8>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ValueTuple<T1> ToValueTuple()
		{
			return new ValueTuple<T1>(this.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1>(ValueTuple<T1> t)
		{
			return new STuple<T1>(t.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ValueTuple<T1>(STuple<T1> t)
		{
			return new ValueTuple<T1>(t.Item1);
		}

		public sealed class Comparer : IComparer<STuple<T1>>
		{
			public static Comparer Default { get; } = new();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;

			private Comparer() { }

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(STuple<T1> x, STuple<T1> y) => Comparer1.Compare(x.Item1, y.Item1);

			/// <inheritdoc cref="Compare(STuple{T1},STuple{T1})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int Compare(in STuple<T1> x, in STuple<T1> y) => Comparer1.Compare(x.Item1, y.Item1);

			/// <inheritdoc cref="Compare(STuple{T1},STuple{T1})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int Compare(in STuple<T1> x, in ValueTuple<T1> y) => Comparer1.Compare(x.Item1, y.Item1);

		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1>>
		{
			public static EqualityComparer Default { get; } = new();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;

			private EqualityComparer() { }

			#region Equals...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(STuple<T1> x, STuple<T1> y) => Comparer1.Equals(x.Item1, y.Item1);

			/// <inheritdoc cref="Equals(STuple{T1},STuple{T1})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Equals(in STuple<T1> x, in STuple<T1> y) => Comparer1.Equals(x.Item1, y.Item1);

			/// <inheritdoc cref="Equals(STuple{T1},STuple{T1})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Equals(in STuple<T1> x, in ValueTuple<T1> y) => Comparer1.Equals(x.Item1, y.Item1);

			#endregion

			#region GetHashCode...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetHashCode(STuple<T1> obj) => obj.Item1 is not null ? Comparer1.GetHashCode(obj.Item1) : -1;

			/// <inheritdoc cref="GetHashCode(STuple{T1})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int GetHashCode(in STuple<T1> obj) => obj.Item1 is not null ? Comparer1.GetHashCode(obj.Item1) : -1;

			#endregion

		}

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint) => TupleEncoder.TryGetSizeHint<T1>(this.Item1, embedded: false, out sizeHint);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, this.Item1);

		#endregion

	}

}
