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
	using System.Globalization;
	using System.Numerics;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Binary;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>Factory class for Tuples</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public readonly struct STuple : IVarTuple
		, IEquatable<STuple>, IComparable<STuple>, IComparable
		, ITupleSpanPackable
		, ITupleFormattable
		, ISpanFormattable
		, ISpanEncodable
	{
		//note: We cannot use 'Tuple' because it's already used by the BCL in the System namespace, and we cannot use 'Tuples' either because it is part of the namespace...

		/// <summary>Empty tuple</summary>
		/// <remarks>Not to be mistaken with a 1-tuple containing 'null' !</remarks>
		public static readonly IVarTuple Empty = new STuple();

		#region Empty Tuple

		/// <inheritdoc />
		public int Count => 0;

		/// <inheritdoc />
		object IReadOnlyList<object?>.this[int index] => throw TupleHelpers.FailTupleIsEmpty();

#if !NETSTANDARD2_0
		// System.Runtime.CompilerServices.ITuple is not visible to netstandard2.0
		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => 0;

		/// <inheritdoc />
		object System.Runtime.CompilerServices.ITuple.this[int index] => throw TupleHelpers.FailTupleIsEmpty();
#endif

		//REVIEW: should we throw if from/to are not null, 0 or -1 ?
		IVarTuple IVarTuple.this[int? from, int? to] => this;

		/// <inheritdoc />
		object IVarTuple.this[int index] => throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		object IVarTuple.this[Index index] => throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		IVarTuple IVarTuple.this[Range range]
		{
			get
			{
				_ = range.GetOffsetAndLength(0);
				return this;
			}
		}

		/// <inheritdoc />
		TItem IVarTuple.Get<TItem>(int index) => throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		TItem IVarTuple.Get<TItem>(Index index) => throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		TItem IVarTuple.GetFirst<TItem>() => throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		TItem IVarTuple.GetLast<TItem>() => throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Append<T1>(T1 value) => new STuple<T1>(value);

		/// <inheritdoc />
		public IVarTuple Concat(IVarTuple tuple)
		{
			Contract.NotNull(tuple);
			return tuple.Count == 0 ? this : tuple;
		}

		/// <inheritdoc />
		public void CopyTo(object?[] array, int offset)
		{
			//NO-OP
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			yield break;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <inheritdoc />
		public override string ToString() => "()";

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null) => "()";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => "()".TryCopyTo(destination, out charsWritten);

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return 0;
		}

		/// <inheritdoc />
		bool IEquatable<STuple>.Equals(STuple value) => true;

		/// <inheritdoc />
		public bool Equals(IVarTuple? value)
			=> value is STuple || (value is not null && value.Count == 0);

		/// <inheritdoc />
		public override bool Equals(object? obj)
			=> obj is STuple || (obj is IVarTuple t && t.Count == 0);

		/// <inheritdoc />
		bool System.Collections.IStructuralEquatable.Equals(object? other, System.Collections.IEqualityComparer comparer)
			=> other is STuple || (other is IVarTuple t && t.Count == 0);

		int IComparable<STuple>.CompareTo(STuple other) => 0;

		public int CompareTo(IVarTuple? other) => other switch
		{
			null => -1,
			STuple => 0,
			_ => (other.Count == 0 ? 0 : -1),
		};

		public int CompareTo(object? other) => other switch
		{
			null => -1,
			STuple => 0,
			IVarTuple t => (t.Count == 0 ? 0 : -1),
			_ => throw new ArgumentException($"Cannot compare STuple to instance of {other.GetType().GetFriendlyName()}", nameof(other)),
		};

		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => other switch
		{
			null => -1,
			STuple => 0,
			IVarTuple t => (t.Count == 0 ? 0 : -1),
			_ => throw new ArgumentException($"Cannot compare STuple to instance of {other.GetType().GetFriendlyName()}", nameof(other)),
		};

		/// <inheritdoc />
		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return 0;
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			return index == 0 ? 0 : throw new IndexOutOfRangeException();
		}

		/// <inheritdoc />
		void ITuplePackable.PackTo(TupleWriter writer)
		{
			//NOP
		}

		/// <inheritdoc />
		bool ITupleSpanPackable.TryPackTo(ref TupleSpanWriter writer)
		{
			//NOP
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ITupleSpanPackable.TryGetSizeHint(bool embedded, out int sizeHint) { sizeHint = embedded ? 2 : 0; return true; }

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			return 0;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return true; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint) { sizeHint = 0; return true; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten) { bytesWritten = 0; return true; }

		#endregion

		#region Creation

		/// <summary>Create a new empty tuple with 0 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple Create()
		{
			//note: redundant with STuple.Empty, but is here to fit nicely with the other Create<T...> overloads
			return new STuple();
		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1> Create<T1>(T1 item1)
		{
			return new STuple<T1>(item1);
		}

		/// <summary>Create a new 2-tuple, holding two items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
		{
			return new STuple<T1, T2>(item1, item2);
		}

		/// <summary>Create a new 3-tuple, holding three items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new STuple<T1, T2, T3>(item1, item2, item3);
		}

		/// <summary>Create a new 4-tuple, holding four items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new STuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		/// <summary>Create a new 5-tuple, holding five items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new STuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		/// <summary>Create a new 6-tuple, holding six items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Create a new 7-tuple, holding seven items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return new STuple<T1, T2, T3, T4, T5, T6, T7>(item1, item2, item3, item4, item5, item6, item7);
		}

		/// <summary>Create a new 8-tuple, holding eight items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return new STuple<T1, T2, T3, T4, T5, T6, T7, T8>(item1, item2, item3, item4, item5, item6, item7, item8);
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Items to wrap in a tuple</param>
		/// <remarks>If you already have an array of items, you should call <see cref="FromArray{T}(T[])"/> instead. Mutating the array, would also mutate the tuple!</remarks>
		[Pure]
#if NET9_0_OR_GREATER
		public static IVarTuple Create(object?[] items)
#else
		public static IVarTuple Create(params object?[] items)
#endif
		{
			Contract.NotNull(items);

			//note: this is a convenience method for people that wants to pass more than 3 args arguments, and not have to call CreateRange(object[]) method

			if (items.Length == 0) return STuple.Empty;

			// We don't copy the array, and rely on the fact that the array was created by the compiler and that nobody will get a reference on it.
			return new ListTuple<object?>(items.AsMemory());
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Items to wrap in a tuple</param>
		/// <remarks>If you already have an array of items, you should call <see cref="FromArray{T}(T[])"/> instead. Mutating the array, would also mutate the tuple!</remarks>
		[Pure]
#if NET9_0_OR_GREATER
		public static IVarTuple Create(params ReadOnlySpan<object?> items)
#else
		public static IVarTuple Create(ReadOnlySpan<object?> items)
#endif
		{
			//note: this is a convenience method for people that wants to pass more than 3 args arguments, and not have to call CreateRange(object[]) method

			if (items.Length == 0) return STuple.Empty;

			// We don't copy the array, and rely on the fact that the array was created by the compiler and that nobody will get a reference on it.
			return new ListTuple<object?>(items.ToArray());
		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		/// <remarks>This is the non-generic equivalent of <c>STuple.Create&lt;object&gt;(item)</c></remarks>
		[Pure]
		public static IVarTuple CreateBoxed(object? item)
		{
			return new STuple<object?>(item);
		}

		/// <summary>Create a new N-tuple that wraps a sequence of untyped items</summary>
		/// <remarks>If the original sequence is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple Wrap(ReadOnlyMemory<object?> items)
		{
			//note: this method only exists to differentiate between Create(ROM<object>) and Create<ROM<object>>()
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(items);
		}

		/// <summary>Create a new N-tuple that wraps an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple Wrap(object?[] items)
		{
			//note: this method only exists to differentiate between Create(object[]) and Create<object[]>()
			Contract.NotNull(items);
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(items.AsMemory());
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple Wrap(object?[] items, int offset, int count)
		{
			if (count == 0) return STuple.Empty;
			return new ListTuple<object?>(items.AsMemory(offset, count));
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[Pure]
		public static IVarTuple FromObjects(ReadOnlyMemory<object?> items)
		{
			return new ListTuple<object?>(items.ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[Pure]
		public static IVarTuple FromObjects(ReadOnlySpan<object?> items)
		{
			return new ListTuple<object?>(items.ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[Pure]
		public static IVarTuple FromObjects(object?[] items)
		{
			//note: this method only exists to differentiate between Create(object[]) and Create<object[]>()
			Contract.NotNull(items);
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(items.ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple by copying a section of an array of untyped items</summary>
		public static IVarTuple FromObjects(object?[] items, int offset, int count)
		{
			if (count == 0) return STuple.Empty;
			return new ListTuple<object?>(items.AsMemory(offset, count).ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If <paramref name="copy"/> is true, and the original array is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple FromObjects(ReadOnlyMemory<object?> items, bool copy)
		{
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(copy ? items.ToArray().AsMemory() : items);
		}

		/// <summary>Wraps an array into a tuple with the same items</summary>
		/// <remarks>Changing the array will impact the tuple, which may break the expectations of some consumers, who assume that a tuple is immutable.</remarks>
		public static IVarTuple WrapArray<T>(T[] items)
		{
			Contract.NotNull(items);
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<T>(items.AsMemory());
		}

		/// <summary>Wraps an array into a tuple with the same items</summary>
		/// <remarks>Changing the array will impact the tuple, which may break the expectations of some consumers, who assume that a tuple is immutable.</remarks>
		public static IVarTuple WrapArray<T>(ReadOnlyMemory<T> items)
		{
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<T>(items);
		}

		/// <summary>Creates a new tuple, from an array of typed items</summary>
		/// <param name="items">Array of items</param>
		/// <returns>Tuple with the same size as <paramref name="items"/> and where all the items are of type <typeparamref name="T"/></returns>
		[Pure]
		public static IVarTuple FromArray<T>(T[] items)
		{
			Contract.NotNull(items);

			return FromArray<T>(items.AsSpan());
		}

		/// <summary>Creates a new tuple, from a section of an array of typed items</summary>
		[Pure]
		public static IVarTuple FromArray<T>(T[] items, int offset, int count)
		{
			return FromArray<T>(items.AsSpan(offset, count));
		}

		/// <summary>Creates a new tuple, from a section of an array of typed items</summary>
		[Pure]
		public static IVarTuple FromArray<T>(ReadOnlySpan<T> items)
		{
			switch (items.Length)
			{
				case 0: return STuple.Empty;
				case 1: return Create<T>(items[0]);
				case 2: return Create<T, T>(items[0], items[1]);
				case 3: return Create<T, T, T>(items[0], items[1], items[2]);
				default:
				{ // copy the items in a temp array
					return new ListTuple<T>(items.ToArray().AsMemory());
				}
			}
		}

		/// <summary>Creates a new tuple from a sequence of typed items</summary>
		[Pure]
		public static IVarTuple FromEnumerable<T>(IEnumerable<T> items)
		{
			Contract.NotNull(items);

			if (items is T[] arr)
			{
				return FromArray<T>(arr.AsSpan());
			}

			if (items is ListTuple<T> lt)
			{
				return lt;
			}

			// may already be a tuple (because it implements IE<obj>)
			if (typeof(T) == typeof(object) && items is IVarTuple vt)
			{
				return vt;
			}

			if (items is IList<T> list)
			{
				switch (list.Count)
				{
					case 0: return STuple.Empty;
					case 1: return Create<T>(list[0]);
					case 2: return Create<T, T>(list[0], list[1]);
					case 3: return Create<T, T, T>(list[0], list[1], list[2]);
					default:
					{ // copy the items in a temp array
						return new ListTuple<T>(items.ToArray().AsMemory());
					}
				}
			}

			return new ListTuple<T>(items.ToArray().AsMemory());
		}

		/// <summary>Concatenates two tuples together</summary>
		[Pure]
		public static IVarTuple Concat(IVarTuple head, IVarTuple tail)
		{
			Contract.NotNull(head);
			Contract.NotNull(tail);

			return tail.Count == 0 ? head
			     : head.Count == 0 ? tail
			     : new JoinedTuple<IVarTuple, IVarTuple>(head, tail);
		}

		/// <summary>Concatenates two tuples together</summary>
		[Pure]
		public static IVarTuple Concat<THead, TTail>(in THead head, in TTail tail)
			where THead : IVarTuple
			where TTail : IVarTuple
		{
			Contract.NotNull(head);
			Contract.NotNull(tail);

			return tail.Count == 0 ? head
				: head.Count == 0 ? tail
				: new JoinedTuple<THead, TTail>(head, tail);
		}

		/// <summary>Concatenates two tuples together</summary>
		[Pure]
		public static IVarTuple Concat(IVarTuple head, IVarTuple middle, IVarTuple tail)
		{
			Contract.NotNull(head);
			Contract.NotNull(middle);
			Contract.NotNull(tail);

			int numA = head.Count;
			int numB = middle.Count;
			int numC = tail.Count;

			if (numC == 0) return Concat(head, middle);
			if (numB == 0) return Concat(head, tail);
			if (numA == 0) return Concat(middle, tail);

			var tmp = new object?[checked(numA + numB + numC)];
			head.CopyTo(tmp, 0);
			middle.CopyTo(tmp, numA);
			tail.CopyTo(tmp, numA + numB);

			return new ListTuple<object?>(tmp.AsMemory());
		}

		/// <summary>Converts a <see cref="ValueTuple{T1}"/> into the equivalent <see cref="STuple{T1}"/> of the same size</summary>
		[Pure]
		public static STuple<T1> Create<T1>(ValueTuple<T1> tuple)
			=> new(tuple.Item1);

		/// <summary>Converts a <see cref="ValueTuple{T1}"/> into the equivalent <see cref="STuple{T1}"/> of the same size</summary>
		[Pure]
		public static STuple<T1> Create<T1>(ref ValueTuple<T1> tuple)
			=> new(tuple.Item1);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2}"/> into the equivalent <see cref="STuple{T1,T2}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2> Create<T1, T2>((T1, T2) tuple)
			=> new(tuple.Item1, tuple.Item2);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2}"/> into the equivalent <see cref="STuple{T1,T2}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2> Create<T1, T2>(ref (T1, T2) tuple)
			=> new(tuple.Item1, tuple.Item2);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3}"/> into the equivalent <see cref="STuple{T1,T2,T3}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>((T1, T2, T3) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3}"/> into the equivalent <see cref="STuple{T1,T2,T3}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(ref (T1, T2, T3) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>((T1, T2, T3, T4) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(ref (T1, T2, T3, T4) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>((T1, T2, T3, T4, T5) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(ref (T1, T2, T3, T4, T5) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>((T1, T2, T3, T4, T5, T6) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(ref (T1, T2, T3, T4, T5, T6) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>((T1, T2, T3, T4, T5, T6, T7) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(ref (T1, T2, T3, T4, T5, T6, T7) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7,T8}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7,T8}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>((T1, T2, T3, T4, T5, T6, T7, T8) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7,T8}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7,T8}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>(ref (T1, T2, T3, T4, T5, T6, T7, T8) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8);

		#endregion

		#region Internal Helpers...

		/// <summary>Determines whether the specified tuple instances are considered equal</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered equal; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		/// <remarks>This method is equivalent of calling left.Equals(right), </remarks>
		public static bool Equals(IVarTuple left, IVarTuple right)
		{
			if (ReferenceEquals(left, null)) return ReferenceEquals(right, null);
			return left.Equals(right);
		}

		/// <summary>Determines whether the specified tuple instances are considered similar</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered similar; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		public static bool Equivalent(IVarTuple left, IVarTuple right)
		{
			if (ReferenceEquals(left, null)) return ReferenceEquals(right, null);
			return !ReferenceEquals(right, null) && TupleHelpers.Equals(left, right, TupleComparisons.Default);
		}

		/// <summary>Methods for formatting tuples into strings</summary>
		[PublicAPI]
		public static class Formatter
		{

			private const string TokenNull = "null";
			private const string TokenFalse = "false";
			private const string TokenTrue = "true";
			private const string TokenTupleEmpty = "()";

			/// <summary>Converts any object into a displayable string, for logging/debugging purpose</summary>
			/// <param name="item">Object to stringify</param>
			/// <returns>String representation of the object</returns>
			/// <example>
			/// Stringify&lt;{REF_TYPE}&gt;(null) => "nil"
			/// Stringify&lt;string&gt;{string}("hello") => "\"hello\""
			/// Stringify&lt;int&gt;(123) => "123"
			/// Stringify&lt;double&gt;(123.4d) => "123.4"
			/// Stringify&lt;bool&gt;(true) => "true"
			/// Stringify&lt;char&gt;('Z') => "'Z'"
			/// Stringify&lt;Slice&gt;((...) => hexadecimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify<T>(T? item)
			{
				if (item is null) return TokenNull;

				if (default(T) is not null)
				{
					// <JIT_HACK>!
					if (typeof(T) == typeof(int)) return Stringify((int) (object) item);
					if (typeof(T) == typeof(uint)) return Stringify((uint) (object) item);
					if (typeof(T) == typeof(long)) return Stringify((long) (object) item);
					if (typeof(T) == typeof(ulong)) return Stringify((ulong) (object) item);
					if (typeof(T) == typeof(bool)) return Stringify((bool) (object) item);
					if (typeof(T) == typeof(char)) return Stringify((char) (object) item);
					if (typeof(T) == typeof(Slice)) return Stringify((Slice) (object) item);
					if (typeof(T) == typeof(double)) return Stringify((double) (object) item);
					if (typeof(T) == typeof(float)) return Stringify((float) (object) item);
					if (typeof(T) == typeof(decimal)) return Stringify((decimal) (object) item);
					if (typeof(T) == typeof(Guid)) return Stringify((Guid) (object) item);
					if (typeof(T) == typeof(Uuid128)) return Stringify((Uuid128) (object) item);
					if (typeof(T) == typeof(Uuid96)) return Stringify((Uuid96) (object) item);
					if (typeof(T) == typeof(Uuid80)) return Stringify((Uuid80) (object) item);
					if (typeof(T) == typeof(Uuid64)) return Stringify((Uuid64) (object) item);
					if (typeof(T) == typeof(Uuid48)) return Stringify((Uuid48) (object) item);
					if (typeof(T) == typeof(DateTime)) return Stringify((DateTime) (object) item);
					if (typeof(T) == typeof(DateTimeOffset)) return Stringify((DateTimeOffset) (object) item);
					if (typeof(T) == typeof(NodaTime.Instant)) return Stringify((NodaTime.Instant) (object) item);
					if (typeof(T) == typeof(VersionStamp)) return Stringify((VersionStamp) (object) item);
					if (typeof(T) == typeof(ArraySegment<byte>)) return Stringify(((ArraySegment<byte>) (object) item).AsSlice());
					if (typeof(T) == typeof(ReadOnlyMemory<byte>)) return Stringify(((ReadOnlyMemory<byte>) (object) item).Span);
					// </JIT_HACK>
				}
				else
				{
					if (item is string s) return Stringify(s);

					// <JIT_HACK>!
					if (typeof(T) == typeof(int?)) return Stringify((int) (object) item);
					if (typeof(T) == typeof(uint?)) return Stringify((uint) (object) item);
					if (typeof(T) == typeof(long?)) return Stringify((long) (object) item);
					if (typeof(T) == typeof(ulong?)) return Stringify((ulong) (object) item);
					if (typeof(T) == typeof(bool?)) return Stringify((bool) (object) item);
					if (typeof(T) == typeof(char?)) return Stringify((char) (object) item);
					if (typeof(T) == typeof(Slice?)) return Stringify((Slice) (object) item);
					if (typeof(T) == typeof(double?)) return Stringify((double) (object) item);
					if (typeof(T) == typeof(float?)) return Stringify((float) (object) item);
					if (typeof(T) == typeof(decimal?)) return Stringify((decimal) (object) item);
					if (typeof(T) == typeof(Guid?)) return Stringify((Guid) (object) item);
					if (typeof(T) == typeof(Uuid128?)) return Stringify((Uuid128) (object) item);
					if (typeof(T) == typeof(Uuid96?)) return Stringify((Uuid96) (object) item);
					if (typeof(T) == typeof(Uuid80?)) return Stringify((Uuid80) (object) item);
					if (typeof(T) == typeof(Uuid64?)) return Stringify((Uuid64) (object) item);
					if (typeof(T) == typeof(Uuid48?)) return Stringify((Uuid48) (object) item);
					if (typeof(T) == typeof(DateTime?)) return Stringify((DateTime) (object) item);
					if (typeof(T) == typeof(DateTimeOffset?)) return Stringify((DateTimeOffset) (object) item);
					if (typeof(T) == typeof(NodaTime.Instant?)) return Stringify((NodaTime.Instant) (object) item);
					if (typeof(T) == typeof(VersionStamp?)) return Stringify((VersionStamp) (object) item);
					// </JIT_HACK>
				}

				return typeof(T) == typeof(object)
					? StringifyBoxed(item) // probably a List<object?> or ReadOnlySpan<object?> that was misrouted here instead of StringifyBoxed!
					: StringifyInternal(item);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo<T>(ref FastStringBuilder sb, T? item)
			{
				if (item is null)
				{
					sb.Append(TokenNull);
					return;
				}

				if (default(T) is not null)
				{
					// <JIT_HACK>!
					if (typeof(T) == typeof(int)) { StringifyTo(ref sb, (int) (object) item); return; }
					if (typeof(T) == typeof(uint)) { StringifyTo(ref sb, (uint) (object) item); return; }
					if (typeof(T) == typeof(long)) { StringifyTo(ref sb, (long) (object) item); return; }
					if (typeof(T) == typeof(ulong)) { StringifyTo(ref sb, (ulong) (object) item); return; }
					if (typeof(T) == typeof(bool)) { StringifyTo(ref sb, (bool) (object) item); return; }
					if (typeof(T) == typeof(char)) { StringifyTo(ref sb, (char) (object) item); return; }
					if (typeof(T) == typeof(Slice)) { StringifyTo(ref sb, (Slice) (object) item); return; }
					if (typeof(T) == typeof(double)) { StringifyTo(ref sb, (double) (object) item); return; }
					if (typeof(T) == typeof(float)) { StringifyTo(ref sb, (float) (object) item); return; }
					if (typeof(T) == typeof(decimal)) { StringifyTo(ref sb, (decimal) (object) item); return; }
					if (typeof(T) == typeof(Guid)) { StringifyTo(ref sb, (Guid) (object) item); return; }
					if (typeof(T) == typeof(Uuid128)) { StringifyTo(ref sb, (Uuid128) (object) item); return; }
					if (typeof(T) == typeof(Uuid96)) { StringifyTo(ref sb, (Uuid96) (object) item); return; }
					if (typeof(T) == typeof(Uuid80)) { StringifyTo(ref sb, (Uuid80) (object) item); return; }
					if (typeof(T) == typeof(Uuid64)) { StringifyTo(ref sb, (Uuid64) (object) item); return; }
					if (typeof(T) == typeof(Uuid48)) { StringifyTo(ref sb, (Uuid48) (object) item); return; }
					if (typeof(T) == typeof(DateTime)) { StringifyTo(ref sb, (DateTime) (object) item); return; }
					if (typeof(T) == typeof(DateTimeOffset)) { StringifyTo(ref sb, (DateTimeOffset) (object) item); return; }
					if (typeof(T) == typeof(NodaTime.Instant)) { StringifyTo(ref sb, (NodaTime.Instant) (object) item); return; }
					if (typeof(T) == typeof(VersionStamp)) { StringifyTo(ref sb, (VersionStamp) (object) item); return; }
					if (typeof(T) == typeof(ArraySegment<byte>)) { StringifyTo(ref sb, ((ArraySegment<byte>) (object) item).AsSlice()); return; }
					if (typeof(T) == typeof(ReadOnlyMemory<byte>)) { StringifyTo(ref sb, ((ReadOnlyMemory<byte>) (object) item).Span); return; }
					// </JIT_HACK>
				}
				else
				{
					// <JIT_HACK>!
					if (typeof(T) == typeof(int?)) { StringifyTo(ref sb, (int) (object) item); return; }
					if (typeof(T) == typeof(uint?)) { StringifyTo(ref sb, (uint) (object) item); return; }
					if (typeof(T) == typeof(long?)) { StringifyTo(ref sb, (long) (object) item); return; }
					if (typeof(T) == typeof(ulong?)) { StringifyTo(ref sb, (ulong) (object) item); return; }
					if (typeof(T) == typeof(bool?)) { StringifyTo(ref sb, (bool) (object) item); return; }
					if (typeof(T) == typeof(char?)) { StringifyTo(ref sb, (char) (object) item); return; }
					if (typeof(T) == typeof(Slice?)) { StringifyTo(ref sb, (Slice) (object) item); return; }
					if (typeof(T) == typeof(double?)) { StringifyTo(ref sb, (double) (object) item); return; }
					if (typeof(T) == typeof(float?)) { StringifyTo(ref sb, (float) (object) item); return; }
					if (typeof(T) == typeof(decimal?)) { StringifyTo(ref sb, (decimal) (object) item); return; }
					if (typeof(T) == typeof(Guid?)) { StringifyTo(ref sb, (Guid) (object) item); return; }
					if (typeof(T) == typeof(Uuid128?)) { StringifyTo(ref sb, (Uuid128) (object) item); return; }
					if (typeof(T) == typeof(Uuid96?)) { StringifyTo(ref sb, (Uuid96) (object) item); return; }
					if (typeof(T) == typeof(Uuid80?)) { StringifyTo(ref sb, (Uuid80) (object) item); return; }
					if (typeof(T) == typeof(Uuid64?)) { StringifyTo(ref sb, (Uuid64) (object) item); return; }
					if (typeof(T) == typeof(Uuid48?)) { StringifyTo(ref sb, (Uuid48) (object) item); return; }
					if (typeof(T) == typeof(DateTime?)) { StringifyTo(ref sb, (DateTime) (object) item); return; }
					if (typeof(T) == typeof(DateTimeOffset?)) { StringifyTo(ref sb, (DateTimeOffset) (object) item); return; }
					if (typeof(T) == typeof(NodaTime.Instant?)) { StringifyTo(ref sb, (NodaTime.Instant) (object) item); return; }
					if (typeof(T) == typeof(VersionStamp?)) { StringifyTo(ref sb, (VersionStamp) (object) item); return; }
					// </JIT_HACK>

					if (item is string s) { StringifyTo(ref sb, s);  return; }
				}

				if (typeof(T) == typeof(object))
				{
					StringifyBoxedTo(ref sb, item); // probably a List<object?> or ReadOnlySpan<object?> that was misrouted here instead of StringifyBoxed!
				}
				else
				{
					StringifyInternalTo(ref sb, item);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo<T>(Span<char> destination, out int charsWritten, T? item)
			{
				if (item is null)
				{
					return TokenNull.TryCopyTo(destination, out charsWritten);
				}

				if (default(T) is not null)
				{
					// <JIT_HACK>!
					if (typeof(T) == typeof(int))                  return TryStringifyTo(destination, out charsWritten, (int) (object) item);
					if (typeof(T) == typeof(uint))                 return TryStringifyTo(destination, out charsWritten, (uint) (object) item);
					if (typeof(T) == typeof(long))                 return TryStringifyTo(destination, out charsWritten, (long) (object) item);
					if (typeof(T) == typeof(ulong))                return TryStringifyTo(destination, out charsWritten, (ulong) (object) item);
					if (typeof(T) == typeof(bool))                 return TryStringifyTo(destination, out charsWritten, (bool) (object) item);
					if (typeof(T) == typeof(char))                 return TryStringifyTo(destination, out charsWritten, (char) (object) item);
					if (typeof(T) == typeof(Slice))                return TryStringifyTo(destination, out charsWritten, (Slice) (object) item);
					if (typeof(T) == typeof(double))               return TryStringifyTo(destination, out charsWritten, (double) (object) item);
					if (typeof(T) == typeof(float))                return TryStringifyTo(destination, out charsWritten, (float) (object) item);
					if (typeof(T) == typeof(decimal))              return TryStringifyTo(destination, out charsWritten, (decimal) (object) item);
					if (typeof(T) == typeof(Guid))                 return TryStringifyTo(destination, out charsWritten, (Guid) (object) item);
					if (typeof(T) == typeof(Uuid128))              return TryStringifyTo(destination, out charsWritten, (Uuid128) (object) item);
					if (typeof(T) == typeof(Uuid96))               return TryStringifyTo(destination, out charsWritten, (Uuid96) (object) item);
					if (typeof(T) == typeof(Uuid80))               return TryStringifyTo(destination, out charsWritten, (Uuid80) (object) item);
					if (typeof(T) == typeof(Uuid64))               return TryStringifyTo(destination, out charsWritten, (Uuid64) (object) item);
					if (typeof(T) == typeof(Uuid48))               return TryStringifyTo(destination, out charsWritten, (Uuid48) (object) item);
					if (typeof(T) == typeof(DateTime))             return TryStringifyTo(destination, out charsWritten, (DateTime) (object) item);
					if (typeof(T) == typeof(DateTimeOffset))       return TryStringifyTo(destination, out charsWritten, (DateTimeOffset) (object) item);
					if (typeof(T) == typeof(NodaTime.Instant))     return TryStringifyTo(destination, out charsWritten, (NodaTime.Instant) (object) item);
					if (typeof(T) == typeof(VersionStamp))         return TryStringifyTo(destination, out charsWritten, (VersionStamp) (object) item);
					if (typeof(T) == typeof(ArraySegment<byte>))   return TryStringifyTo(destination, out charsWritten, ((ArraySegment<byte>) (object) item).AsSlice());
					if (typeof(T) == typeof(ReadOnlyMemory<byte>)) return TryStringifyTo(destination, out charsWritten, ((ReadOnlyMemory<byte>) (object) item).Span);
					if (typeof(T) == typeof(BigInteger))           return TryStringifyTo(destination, out charsWritten, (BigInteger) (object) item);
					// </JIT_HACK>
				}
				else
				{
					// <JIT_HACK>!
					if (typeof(T) == typeof(int?))              return TryStringifyTo(destination, out charsWritten, (int) (object) item);
					if (typeof(T) == typeof(uint?))             return TryStringifyTo(destination, out charsWritten, (uint) (object) item);
					if (typeof(T) == typeof(long?))             return TryStringifyTo(destination, out charsWritten, (long) (object) item);
					if (typeof(T) == typeof(ulong?))            return TryStringifyTo(destination, out charsWritten, (ulong) (object) item);
					if (typeof(T) == typeof(bool?))             return TryStringifyTo(destination, out charsWritten, (bool) (object) item);
					if (typeof(T) == typeof(char?))             return TryStringifyTo(destination, out charsWritten, (char) (object) item);
					if (typeof(T) == typeof(Slice?))            return TryStringifyTo(destination, out charsWritten, (Slice) (object) item);
					if (typeof(T) == typeof(double?))           return TryStringifyTo(destination, out charsWritten, (double) (object) item);
					if (typeof(T) == typeof(float?))            return TryStringifyTo(destination, out charsWritten, (float) (object) item);
					if (typeof(T) == typeof(decimal?))          return TryStringifyTo(destination, out charsWritten, (decimal) (object) item);
					if (typeof(T) == typeof(Guid?))             return TryStringifyTo(destination, out charsWritten, (Guid) (object) item);
					if (typeof(T) == typeof(Uuid128?))          return TryStringifyTo(destination, out charsWritten, (Uuid128) (object) item);
					if (typeof(T) == typeof(Uuid96?))           return TryStringifyTo(destination, out charsWritten, (Uuid96) (object) item);
					if (typeof(T) == typeof(Uuid80?))           return TryStringifyTo(destination, out charsWritten, (Uuid80) (object) item);
					if (typeof(T) == typeof(Uuid64?))           return TryStringifyTo(destination, out charsWritten, (Uuid64) (object) item);
					if (typeof(T) == typeof(Uuid48?))           return TryStringifyTo(destination, out charsWritten, (Uuid48) (object) item);
					if (typeof(T) == typeof(DateTime?))         return TryStringifyTo(destination, out charsWritten, (DateTime) (object) item);
					if (typeof(T) == typeof(DateTimeOffset?))   return TryStringifyTo(destination, out charsWritten, (DateTimeOffset) (object) item);
					if (typeof(T) == typeof(NodaTime.Instant?)) return TryStringifyTo(destination, out charsWritten, (NodaTime.Instant) (object) item);
					if (typeof(T) == typeof(VersionStamp?))     return TryStringifyTo(destination, out charsWritten, (VersionStamp) (object) item);
					if (typeof(T) == typeof(BigInteger?))       return TryStringifyTo(destination, out charsWritten, (BigInteger) (object) item);
					// </JIT_HACK>

					if (item is string s)
					{
						return TryStringifyTo(destination, out charsWritten, s); 
					}
				}

				if (typeof(T) == typeof(object))
				{
					return TryStringifyBoxedTo(destination, out charsWritten, item); // probably a List<object?> or ReadOnlySpan<object?> that was misrouted here instead of StringifyBoxed!
				}
				else
				{
					return TryStringifyInternalTo(destination, out charsWritten, item);
				}
			}

			/// <summary>Converts any object into a displayable string, for logging/debugging purpose</summary>
			/// <param name="item">Object to stringify</param>
			/// <returns>String representation of the object</returns>
			/// <example>
			/// Stringify(null) => "nil"
			/// Stringify("hello") => "\"hello\""
			/// Stringify(123) => "123"
			/// Stringify(123.4d) => "123.4"
			/// Stringify(true) => "true"
			/// Stringify('Z') => "'Z'"
			/// Stringify((Slice)...) => hexadecimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			internal static string StringifyBoxed(object? item)
			{
				switch (item)
				{
					case null:               return TokenNull;
					case string s:           return Stringify(s);
					case int i:              return Stringify(i);
					case long l:             return Stringify(l);
					case uint u:             return Stringify(u);
					case ulong ul:           return Stringify(ul);
					case bool b:             return Stringify(b);
					case char c:             return Stringify(c);
					case Slice sl:           return Stringify(sl);
					case double d:           return Stringify(d);
					case float f:            return Stringify(f);
					case Guid guid:          return Stringify(guid);
					case Uuid128 u128:       return Stringify(u128);
					case Uuid96 u96:         return Stringify(u96);
					case Uuid80 u80:         return Stringify(u80);
					case Uuid64 u64:         return Stringify(u64);
					case Uuid48 u48:         return Stringify(u48);
					case DateTime dt:        return Stringify(dt);
					case DateTimeOffset dto: return Stringify(dto);
					case NodaTime.Instant t: return Stringify(t);
					case VersionStamp vs:    return Stringify(vs);
				}

				// some other type
				return StringifyInternal(item);
			}

			/// <summary>Converts any object into a displayable string, for logging/debugging purpose</summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="item">Object to stringify</param>
			/// <returns>String representation of the object</returns>
			/// <example>
			/// Stringify(null) => "nil"
			/// Stringify("hello") => "\"hello\""
			/// Stringify(123) => "123"
			/// Stringify(123.4d) => "123.4"
			/// Stringify(true) => "true"
			/// Stringify('Z') => "'Z'"
			/// Stringify((Slice)...) => hexadecimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			internal static void StringifyBoxedTo(ref FastStringBuilder sb, object? item)
			{
				switch (item)
				{
					case null:               { sb.Append(TokenNull); return; }
					case string s:           { StringifyTo(ref sb, s); return; }
					case int i:              { StringifyTo(ref sb, i); return; }
					case long l:             { StringifyTo(ref sb, l); return; }
					case uint u:             { StringifyTo(ref sb, u); return; }
					case ulong ul:           { StringifyTo(ref sb, ul); return; }
					case bool b:             { StringifyTo(ref sb, b); return; }
					case char c:             { StringifyTo(ref sb, c); return; }
					case Slice sl:           { StringifyTo(ref sb, sl); return; }
					case double d:           { StringifyTo(ref sb, d); return; }
					case float f:            { StringifyTo(ref sb, f); return; }
					case Guid guid:          { StringifyTo(ref sb, guid); return; }
					case Uuid128 u128:       { StringifyTo(ref sb, u128); return; }
					case Uuid96 u96:         { StringifyTo(ref sb, u96); return; }
					case Uuid80 u80:         { StringifyTo(ref sb, u80); return; }
					case Uuid64 u64:         { StringifyTo(ref sb, u64); return; }
					case Uuid48 u48:         { StringifyTo(ref sb, u48); return; }
					case DateTime dt:        { StringifyTo(ref sb, dt); return; }
					case DateTimeOffset dto: { StringifyTo(ref sb, dto); return; }
					case NodaTime.Instant t: { StringifyTo(ref sb, t); return; }
					case VersionStamp vs:    { StringifyTo(ref sb, vs); return; }
				}

				// some other type
				StringifyInternalTo(ref sb, item);
			}

			/// <summary>Converts any object into a displayable string, for logging/debugging purpose</summary>
			/// <param name="destination">Output buffer</param>
			/// <param name="charsWritten">Number of characters written to <paramref name="destination"/></param>
			/// <param name="item">Object to stringify</param>
			/// <returns>String representation of the object</returns>
			/// <example>
			/// Stringify(null) => "nil"
			/// Stringify("hello") => "\"hello\""
			/// Stringify(123) => "123"
			/// Stringify(123.4d) => "123.4"
			/// Stringify(true) => "true"
			/// Stringify('Z') => "'Z'"
			/// Stringify((Slice)...) => hexadecimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			internal static bool TryStringifyBoxedTo(Span<char> destination, out int charsWritten, object? item)
			{
				switch (item)
				{
					case null:               return TokenNull.TryCopyTo(destination, out charsWritten);
					case string s:           return TryStringifyTo(destination, out charsWritten, s);
					case int i:              return TryStringifyTo(destination, out charsWritten, i);
					case long l:             return TryStringifyTo(destination, out charsWritten, l);
					case uint u:             return TryStringifyTo(destination, out charsWritten, u);
					case ulong ul:           return TryStringifyTo(destination, out charsWritten, ul);
					case bool b:             return TryStringifyTo(destination, out charsWritten, b);
					case char c:             return TryStringifyTo(destination, out charsWritten, c);
					case Slice sl:           return TryStringifyTo(destination, out charsWritten, sl);
					case double d:           return TryStringifyTo(destination, out charsWritten, d);
					case float f:            return TryStringifyTo(destination, out charsWritten, f);
					case Guid guid:          return TryStringifyTo(destination, out charsWritten, guid);
					case Uuid128 u128:       return TryStringifyTo(destination, out charsWritten, u128);
					case Uuid96 u96:         return TryStringifyTo(destination, out charsWritten, u96);
					case Uuid80 u80:         return TryStringifyTo(destination, out charsWritten, u80);
					case Uuid64 u64:         return TryStringifyTo(destination, out charsWritten, u64);
					case Uuid48 u48:         return TryStringifyTo(destination, out charsWritten, u48);
					case DateTime dt:        return TryStringifyTo(destination, out charsWritten, dt);
					case DateTimeOffset dto: return TryStringifyTo(destination, out charsWritten, dto);
					case NodaTime.Instant t: return TryStringifyTo(destination, out charsWritten, t);
					case VersionStamp vs:    return TryStringifyTo(destination, out charsWritten, vs);
				}

				// some other type
				return TryStringifyInternalTo(destination, out charsWritten, item);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static string StringifyInternal(object? item) => item switch
			{
				null => TokenNull,
				string s => Stringify(s),
				byte[] bytes => Stringify(bytes.AsSlice()),
				Slice slice => Stringify(slice),
				ArraySegment<byte> buffer => Stringify(buffer.AsSlice()),
				System.Net.IPAddress ip => Stringify(ip),
				//TODO: Memory<T>, ReadOnlyMemory<T>, ...
				IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
				_ => item.ToString() ?? TokenNull
			};

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void StringifyInternalTo(ref FastStringBuilder sb, object? item)
			{
				switch (item)
				{
					case null:                      { sb.Append(TokenNull); break; }
					case string s:                  { StringifyTo(ref sb, s); break; }
					case byte[] bytes:              { StringifyTo(ref sb, bytes.AsSlice()); break; }
					case Slice slice:               { StringifyTo(ref sb, slice); break; }
					case ArraySegment<byte> buffer: { StringifyTo(ref sb, buffer.AsSlice()); break; }
					case System.Net.IPAddress ip:   { StringifyTo(ref sb, ip); break; }
					case ISpanFormattable sf:       { sb.Append(sf); break; }
					case IFormattable f:            { sb.Append(f.ToString(null, CultureInfo.InvariantCulture)); break; }
					default:                        { sb.Append(item.ToString() ?? TokenNull); break; }
				}
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static bool TryStringifyInternalTo(Span<char> destination, out int charsWritten, object? item)
			{
				switch (item)
				{
					case null:                      return TokenNull.TryCopyTo(destination, out charsWritten);
					case string s:                  return TryStringifyTo(destination, out charsWritten, s);
					case byte[] bytes:              return TryStringifyTo(destination, out charsWritten, bytes.AsSlice());
					case Slice slice:               return TryStringifyTo(destination, out charsWritten, slice);
					case ArraySegment<byte> buffer: return TryStringifyTo(destination, out charsWritten, buffer.AsSlice());
					case System.Net.IPAddress ip:   return TryStringifyTo(destination, out charsWritten, ip);
					case ISpanFormattable sf:       return sf.TryFormat(destination, out charsWritten, default, null);
					case IFormattable f:            return f.ToString(null, CultureInfo.InvariantCulture).TryCopyTo(destination, out charsWritten);
					default:                        return (item.ToString() ?? TokenNull).TryCopyTo(destination, out charsWritten);
				}
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			//TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
			public static string Stringify(string? item) => string.IsNullOrEmpty(item) ? "\"\"" : string.Concat("\"", item, "\""); /* "hello" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			//TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
			public static void StringifyTo(ref FastStringBuilder sb, string? item)
			{
				if (item is null)
				{
					sb.Append("null");
				}
				else if (item.Length == 0)
				{
					sb.Append("\"\""); //BUGBUG: maybe output 'null' ?
				}
				else
				{
					sb.Append('"');
					//BUGBUG: TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
					sb.Append(item); /* "hello" */
					sb.Append('"');
				}
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, string? item)
			{
				if (item is null)
				{
					return "null".TryCopyTo(destination, out charsWritten);
				}
				if (item.Length == 0)
				{
					return "\"\"".TryCopyTo(destination, out charsWritten);
				}

				//BUGBUG: TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
#if NET6_0_OR_GREATER
				return destination.TryWrite(CultureInfo.InvariantCulture, $"\"{item}\"", out charsWritten);
#else
				// no MemoryExtensions.TryWrite
				return ("\"" + item + "\"").TryCopyTo(destination, out charsWritten);
#endif
			}


			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(bool item) => item ? TokenTrue : TokenFalse;

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, bool item) => sb.Append(item ? TokenTrue : TokenFalse);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, bool item) => (item ? "true" : "false").TryCopyTo(destination, out charsWritten);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(int item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, int item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, int item) => item.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(uint item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, uint item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, uint item) => item.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(long item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, long item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, long item) => item.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ulong item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, ulong item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, ulong item) => item.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(double item) => item.ToString("R", CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, double item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, double item) => item.TryFormat(destination, out charsWritten, "R", CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(float item) => item.ToString("R", CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, float item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, float item) => item.TryFormat(destination, out charsWritten, "R", CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(decimal item)
#if NET8_0_OR_GREATER
				=> item.ToString("R", NumberFormatInfo.InvariantInfo);
#else
				=> item.ToString("G", NumberFormatInfo.InvariantInfo);
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, decimal item) => sb.Append(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, decimal item)
#if NET8_0_OR_GREATER
				=> item.TryFormat(destination, out charsWritten, "R", CultureInfo.InvariantCulture);
#else
				=> item.TryFormat(destination, out charsWritten, "G", CultureInfo.InvariantCulture);
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(BigInteger item) => item.ToString("D", NumberFormatInfo.InvariantInfo);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static void StringifyTo(ref FastStringBuilder sb, BigInteger item) => sb.Append(item, "D");
#else
			// BigInteger is not ISpanFormattable
			public static void StringifyTo(ref FastStringBuilder sb, BigInteger item) => sb.Append(item.ToString("D", CultureInfo.InvariantCulture));
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, BigInteger item) => item.TryFormat(destination, out charsWritten, "D", CultureInfo.InvariantCulture);
#else
			// no BigInteger.TryFormat
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, BigInteger item) => item.ToString("D", CultureInfo.InvariantCulture).TryCopyTo(destination, out charsWritten);
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(char item) => new([ '\'', item, '\'']); /* 'X' */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, char item)
			{
				sb.Append('\'');
				sb.Append(item); //BUGBUG: TODO: escape !
				sb.Append('\'');
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, char item) => destination.TryWrite($"'{item}'", out charsWritten);
#else
			// no MemoryExtensions.TryWrite
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, char item) => ("'" + item + "'").TryCopyTo(destination, out charsWritten);
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ReadOnlySpan<byte> item) => item.Length == 0 ? "``" : ('`' + Slice.Dump(item, item.Length) + '`');

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, ReadOnlySpan<byte> item)
			{
				if (item.Length == 0)
				{
					sb.Append("``");
				}
				else
				{
					sb.Append('`');
					sb.Append(Slice.Dump(item, item.Length)); //TODO: version that does not allocate?
					sb.Append('`');
				}
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, ReadOnlySpan<byte> item)
			{
				if (item.Length == 0)
				{
					return "``".TryCopyTo(destination, out charsWritten);
				}

				//TODO: BUGBUG: PERF: implement a TryDump() ??
#if NET6_0_OR_GREATER
				return destination.TryWrite($"`{Slice.Dump(item, item.Length)}`", out charsWritten);
#else
				// no MemoryExtensions.TryWrite
				return ("`" + Slice.Dump(item, item.Length) + "`").TryCopyTo(destination, out charsWritten);
#endif
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Slice item) => item.IsNull ? "null" : item.Count == 0 ? "``" : ('`' + Slice.Dump(item, item.Count) + '`');

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, Slice item)
			{
				if (item.IsNull)
				{
					sb.Append("null");
				}
				else if (item.Count == 0)
				{
					sb.Append("``");
				}
				else
				{
					sb.Append('`');
					sb.Append(Slice.Dump(item, item.Count)); //TODO: version that does not allocate?
					sb.Append('`');
				}
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Slice item)
			{
				if (item.Count == 0)
				{
					return (item.IsNull ? "null" : "``").TryCopyTo(destination, out charsWritten);
				}
#if NET6_0_OR_GREATER
				return destination.TryWrite($"`{item}`", out charsWritten);
#else
				// no MemoryExtensions.TryWrite
				return ("`" + item.ToString() + "`").TryCopyTo(destination, out charsWritten);
#endif
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(byte[]? item) => item == null ? "null" : item.Length == 0 ? "``" : ('`' + Slice.Dump(item, item.Length) + '`');

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, byte[]? item)
			{
				if (item is null)
				{
					sb.Append("null");
				}
				else if (item.Length == 0)
				{
					sb.Append("``");
				}
				else
				{
					sb.Append('`');
					sb.Append(Slice.Dump(item, item.Length)); //TODO: version that does not allocate?
					sb.Append('`');
				}
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, byte[]? item)
			{
#if NET6_0_OR_GREATER
				return item is null ? "null".TryCopyTo(destination, out charsWritten)
					: item.Length == 0 ? "``".TryCopyTo(destination, out charsWritten)
					: destination.TryWrite($"`{new Slice(item)}`", out charsWritten);
#else
				// no MemoryExtensions.TryWrite
				return item is null ? "null".TryCopyTo(destination, out charsWritten)
					: item.Length == 0 ? "``".TryCopyTo(destination, out charsWritten)
					: ("`" + new Slice(item).ToString() + "`").TryCopyTo(destination, out charsWritten);
#endif
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ArraySegment<byte> item) => Stringify(item.AsSlice());

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, ArraySegment<byte> item) => StringifyTo(ref sb, item.AsSlice());

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, ArraySegment<byte> item)
			{
				if (item.Count == 0)
				{
					return (item.Array is null ? "null" : "``").TryCopyTo(destination, out charsWritten);
				}
#if NET6_0_OR_GREATER
				return destination.TryWrite($"`{item.AsSlice()}`", out charsWritten);
#else
				// no MemoryExtensions.TryWrite
				return ("`" + item.AsSlice().ToString() + "`").TryCopyTo(destination, out charsWritten);
#endif
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Guid item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static void StringifyTo(ref FastStringBuilder sb, Guid item) => sb.Append(item, "B"); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */
#else
			// Guid is not ISpanFormattable
			public static void StringifyTo(ref FastStringBuilder sb, Guid item) => sb.Append(item.ToString("B", CultureInfo.InvariantCulture)); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Guid item) => item.TryFormat(destination, out charsWritten, "B");

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid128 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static void StringifyTo(ref FastStringBuilder sb, Uuid128 item) => sb.Append(item, "B"); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */
#else
			// Uuid128 is not ISpanFormattable on ns2.0
			public static void StringifyTo(ref FastStringBuilder sb, Uuid128 item) => sb.Append(item.ToString("B", CultureInfo.InvariantCulture)); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Uuid128 item) => item.TryFormat(destination, out charsWritten, "B");

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid96 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXXXXXX-XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static void StringifyTo(ref FastStringBuilder sb, Uuid96 item) => sb.Append(item, "B"); /* {XXXXXXXX-XXXXXXXX-XXXXXXXX} */
#else
			// Uuid96 is not ISpanFormattable on ns2.0
			public static void StringifyTo(ref FastStringBuilder sb, Uuid96 item) => sb.Append(item.ToString("B", CultureInfo.InvariantCulture)); /* {XXXXXXXX-XXXXXXXX-XXXXXXXX} */
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Uuid96 item) => item.TryFormat(destination, out charsWritten, "B");

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid80 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXX-XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static void StringifyTo(ref FastStringBuilder sb, Uuid80 item) => sb.Append(item, "B"); /* {XXXX-XXXXXXXX-XXXXXXXX} */
#else
			// Uuid80 is not ISpanFormattable on ns2.0
			public static void StringifyTo(ref FastStringBuilder sb, Uuid80 item) => sb.Append(item.ToString("B", CultureInfo.InvariantCulture)); /* {XXXX-XXXXXXXX-XXXXXXXX} */
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Uuid80 item) => item.TryFormat(destination, out charsWritten, "B");

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid64 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, Uuid64 item) => sb.Append(item, "B"); /* {XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Uuid64 item) => item.TryFormat(destination, out charsWritten, "B");

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid48 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, Uuid48 item) => sb.Append(item, "B"); /* {XXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, Uuid48 item) => item.TryFormat(destination, out charsWritten, "B");

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(VersionStamp item) => item.ToString(); /* @xxxxx-xx#xx */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static void StringifyTo(ref FastStringBuilder sb, VersionStamp item) => sb.Append(item); /* @xxxxx-xx#xx */
#else
			// VersionStamp is not ISpanFormattable on ns2.0
			public static void StringifyTo(ref FastStringBuilder sb, VersionStamp item) => sb.Append(item.ToString()); /* @xxxxx-xx#xx */
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, VersionStamp item) => item.TryFormat(destination, out charsWritten);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(DateTime item) => "\"" + item.ToString("O", CultureInfo.InvariantCulture) + "\""; /* "yyyy-mm-ddThh:mm:ss.ffffff" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, DateTime item)
			{
				sb.Append('"');
#if NET6_0_OR_GREATER
				sb.Append(item, "O"); /* "yyyy-mm-ddThh:mm:ss.ffffff" */
#else
				// DateTime is not ISpanFormattable
				sb.Append(item.ToString("O", CultureInfo.InvariantCulture)); /* "yyyy-mm-ddThh:mm:ss.ffffff" */
#endif
				sb.Append('"');
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, DateTime item) => destination.TryWrite(CultureInfo.InvariantCulture, $"\"{item:O}\"", out charsWritten);
#else
			// no MemoryExtensions.TryWrite
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, DateTime item) => ("\"" + item.ToString("O", CultureInfo.InvariantCulture) + "\"").TryCopyTo(destination, out charsWritten);
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(DateTimeOffset item) => "\"" + item.ToString("O", CultureInfo.InvariantCulture) + "\""; /* "yyyy-mm-ddThh:mm:ss.ffffff+hh:mm" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, DateTimeOffset item)
			{
				sb.Append('"');
#if NET6_0_OR_GREATER
				sb.Append(item, "O"); /* "yyyy-mm-ddThh:mm:ss.ffffff+hh:mm" */
#else
				// DateTimeOffset is not ISpanFormattable
				sb.Append(item.ToString("O", CultureInfo.InvariantCulture)); /* "yyyy-mm-ddThh:mm:ss.ffffff+hh:mm" */
#endif
				sb.Append('"');
			}

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, DateTimeOffset item) => destination.TryWrite(CultureInfo.InvariantCulture, $"\"{item:O}\"", out charsWritten);
#else
			// no MemoryExtensions.TryWrite
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, DateTimeOffset item) => ("\"" + item.ToString("O", CultureInfo.InvariantCulture) + "\"").TryCopyTo(destination, out charsWritten);
#endif

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(NodaTime.Instant item) => Stringify(item.ToDateTimeUtc()); /* "yyyy-mm-ddThh:mm:ss.ffffff" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void StringifyTo(ref FastStringBuilder sb, NodaTime.Instant item) => StringifyTo(ref sb, item.ToDateTimeUtc()); /* "yyyy-mm-ddThh:mm:ss.ffffff" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, NodaTime.Instant item) => destination.TryWrite(CultureInfo.InvariantCulture, $"\"{item.ToDateTimeUtc():O}\"", out charsWritten);
#else
			// no MemoryExtensions.TryWrite
			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, NodaTime.Instant item) => ("\"" + item.ToDateTimeUtc().ToString("O", CultureInfo.InvariantCulture) + "\"").TryCopyTo(destination, out charsWritten);
#endif

			public static string Stringify(System.Net.IPAddress? value) => value is null ? "null" : $"'{value}'";

			public static void StringifyTo(ref FastStringBuilder sb, System.Net.IPAddress? value) => sb.Append(value is null ? "null" : $"'{value}'");

			public static bool TryStringifyTo(Span<char> destination, out int charsWritten, System.Net.IPAddress? item)
#if NET6_0_OR_GREATER
				=> item is null
					? "null".TryCopyTo(destination, out charsWritten)
					: destination.TryWrite(CultureInfo.InvariantCulture, $"'{item}'", out charsWritten);
#else
				// no MemoryExtensions.TryWrite
				=> item is null
					? "null".TryCopyTo(destination, out charsWritten)
					: ("'" + item + "'").TryCopyTo(destination, out charsWritten);
#endif

			/// <summary>Converts a list of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="items">Span of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString([ 1, 2, 3 ])</c> => <c>"(1, 2, 3)"</c></example>
			public static void ToString<T>(ref FastStringBuilder sb, ReadOnlySpan<T> items)
			{
				sb.Append('(');
				if (AppendItemsTo<T>(ref sb, items) == 1)
				{
					sb.Append(",)");
				}
				else
				{
					sb.Append(')');
				}
			}

			/// <summary>Converts a sequence of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="items">Span of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString([ 1, 2, 3 ])</c> => <c>"(1, 2, 3)"</c></example>
			[SkipLocalsInit]
			public static string ToString<T>(ReadOnlySpan<T> items)
			{
				if (items.Length == 0) return TokenTupleEmpty;

				var sb = new FastStringBuilder(stackalloc char[128]);
				ToString(ref sb, items);
				return sb.ToString();
			}

			public static int AppendItemsTo<T>(ref FastStringBuilder sb, ReadOnlySpan<T> items)
			{
				if (items.Length == 0)
				{ // empty tuple: "()"
					return 0;
				}

				if (typeof(T) == typeof(object))
				{
					sb.Append(StringifyBoxed(items[0]));
					for(int i = 1; i < items.Length; i++)
					{
						sb.Append(", ");
						sb.Append(StringifyBoxed(items[i]));
					}
				}
				else
				{
					sb.Append(Stringify<T>(items[0]));
					for(int i = 1; i < items.Length; i++)
					{
						sb.Append(", ");
						sb.Append(Stringify<T>(items[i]));
					}
				}

				return items.Length;
			}

			/// <summary>Converts a sequence of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="items">Span of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString([ "hello", 123, true, "world" ])</c> => <c>"(\"hello\", 123, true, \"world\")"</c></example>
			public static void ToString(ref FastStringBuilder sb, ReadOnlySpan<object?> items)
			{
				sb.Append('(');
				if (AppendItemsTo(ref sb, items) == 1)
				{
					sb.Append(",)");
				}
				else
				{
					sb.Append(')');
				}
			}

			/// <summary>Converts a sequence of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="items">Span of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString([ "hello", 123, true, "world" ])</c> => <c>"(\"hello\", 123, true, \"world\")"</c></example>
			[SkipLocalsInit]
			public static string ToString(ReadOnlySpan<object?> items)
			{
				if (items.Length == 0) return TokenTupleEmpty;

				var sb = new FastStringBuilder(stackalloc char[128]);
				ToString(ref sb, items);
				return sb.ToString();
			}

			public static int AppendItemsTo(ref FastStringBuilder sb, ReadOnlySpan<object?> items)
			{
				if (items.Length == 0)
				{ // empty tuple: "()"
					return 0;
				}

				sb.Append(StringifyBoxed(items[0]));
				for(int i = 1; i < items.Length; i++)
				{
					sb.Append(", ");
					sb.Append(StringifyBoxed(items[i]));
				}

				return items.Length;
			}

			/// <summary>Converts a sequence of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="items">Sequence of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString([ "hello", 123, true, "world" ])</c> => <c>"(\"hello\", 123, true, \"world\")"</c></example>
			public static void ToString(ref FastStringBuilder sb, IEnumerable<object?>? items)
			{
				if (items == null)
				{
					return;
				}

				sb.Append('(');
				if (AppendItemsTo(ref sb, items) == 1)
				{
					sb.Append(",)");
				}
				else
				{
					sb.Append(')');
				}
			}

			/// <summary>Converts a sequence of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="items">Sequence of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString([ "hello", 123, true, "world" ])</c> => <c>"(\"hello\", 123, true, \"world\")"</c></example>
			[SkipLocalsInit]
			public static string ToString(IEnumerable<object?>? items)
			{
				var sb = new FastStringBuilder(stackalloc char[128]);
				ToString(ref sb, items);
				return sb.ToString();
			}

			/// <summary>Appends the string representation of the items in a <see cref="ListTuple{T}"/></summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="items">Items to append</param>
			/// <returns>Number of items added</returns>
			public static int AppendItemsTo(ref FastStringBuilder sb, IEnumerable<object?>? items)
			{
				if (items == null) return 0;

				if (items.TryGetSpan(out var span))
				{
					return AppendItemsTo(ref sb, span);
				}

				using (var enumerator = items.GetEnumerator())
				{
					if (!enumerator.MoveNext())
					{ // empty tuple : "()"
						return 0;
					}

					sb.Append(StringifyBoxed(enumerator.Current));
					int n = 1;
					while (enumerator.MoveNext())
					{
						sb.Append(", ");
						sb.Append(StringifyBoxed(enumerator.Current));
						++n;
					}

					return n;
				}
			}

			/// <summary>Converts a sequence of object into a displaying string, for logging/debugging purpose</summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="items">Tuple to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString(TuPack.Unpack(/* ... "hello", 123, true, "world" ... */))</c> => <c>"(\"hello\", 123, true, \"world\")"</c></example>
			public static void ToString<TTuple>(ref FastStringBuilder sb, TTuple items)
#if NET9_0_OR_GREATER
				where TTuple : IVarTuple, allows ref struct
#else
				where TTuple : IVarTuple
#endif
			{
				sb.Append('(');
				if (AppendItemsTo<TTuple>(ref sb, items) == 1)
				{
					sb.Append(",)");
				}
				else
				{
					sb.Append(')');
				}
			}

			/// <summary>Converts a sequence of object into a displaying string, for loggin/debugging purpose</summary>
			/// <param name="items">Sequence of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ...)"</returns>
			/// <example><c>STuple.Formatter.ToString(STuple.Create("hello", 123, true, "world"))</c> => <c>"(\"hello\", 123, true, \"world\")"</c></example>
			[SkipLocalsInit]
			public static string ToString<TTuple>(TTuple items)
#if NET9_0_OR_GREATER
				where TTuple : IVarTuple, allows ref struct
#else
				where TTuple : IVarTuple
#endif
			{
				if (items.Count == 0) return TokenTupleEmpty;

				var sb = new FastStringBuilder(stackalloc char[128]);
				ToString(ref sb, items);
				return sb.ToString();
			}

			/// <summary>Appends the string representation of the items in a <see cref="SpanTuple"/></summary>
			/// <param name="sb">Output buffer</param>
			/// <param name="items">Items to append</param>
			/// <returns>Number of items added</returns>
			public static int AppendItemsTo<TTuple>(ref FastStringBuilder sb, TTuple items)
#if NET9_0_OR_GREATER
				where TTuple : IVarTuple, allows ref struct
#else
				where TTuple : IVarTuple
#endif
			{
				if (items.Count == 0)
				{ // empty tuple : "()"
					return 0;
				}

				using (var enumerator = items.GetEnumerator())
				{
					if (!enumerator.MoveNext())
					{ // mismatch between Count and the enumerator implementation???
						return 0;
					}

					sb.Append(StringifyBoxed(enumerator.Current));
					int n = 1;
					while (enumerator.MoveNext())
					{
						sb.Append(", ");
						sb.Append(StringifyBoxed(enumerator.Current));
						++n;
					}
					return n;
				}
			}

		}

		/// <summary>Methods for parsing tuples from strings</summary>
		public static class Deformatter
		{

			/// <summary>Parses a tuple expression from a string</summary>
			[Pure]
			public static IVarTuple Parse(string expression)
			{
				Contract.NotNullOrWhiteSpace(expression);
				var parser = new Parser(expression.Trim());
				var tuple = parser.ParseExpression();
				if (parser.HasMore) throw new FormatException("Unexpected token after final ')' in Tuple expression.");
				return tuple;
			}

			/// <summary>Parses a tuple expression at the start of a string</summary>
			/// <param name="expression">String that starts with a valid Tuple expression, with optional extra characters</param>
			/// <param name="tuple"></param>
			/// <param name="tail"></param>
			/// <returns>First item is the parsed tuple, and the second item is the rest of the string (or null if we consumed the whole expression)</returns>
			public static void ParseNext(string expression, out IVarTuple? tuple, out string? tail)
			{
				Contract.NotNullOrWhiteSpace(expression);
				if (string.IsNullOrWhiteSpace(expression))
				{
					tuple = null;
					tail = null;
					return;
				}

				var parser = new Parser(expression.Trim());
				tuple = parser.ParseExpression();
				string? s = parser.GetTail();
				tail = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
			}

			private struct Parser
			{

				private const char EOF = '\xFFFF';

				// to reduce boxing, we pre-allocate some well known singletons

				private static readonly object FalseSingleton = false;
				private static readonly object TrueSingleton = true;
				private static readonly object ZeroSingleton = 0;
				private static readonly object OneSingleton = 1;
				private static readonly object TwoSingleton = 2;
				private static readonly object ThreeSingleton = 3;
				private static readonly object FourSingleton = 4;

				public Parser(string expression)
				{
					this.Expression = expression;
					this.Cursor = 0;
				}

				private readonly string Expression;
				
				private int Cursor;

				public readonly bool HasMore => this.Cursor < this.Expression.Length;

				public readonly string? GetTail() => this.Cursor < this.Expression.Length ? this.Expression[this.Cursor..] : null;

				private char ReadNext()
				{
					int p = this.Cursor;
					string s = this.Expression;
					if ((uint) p >= (uint) s.Length) return EOF;
					char c = s[p];
					this.Cursor = p + 1;
					return c;
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				private char PeekNext()
				{
					int p = this.Cursor;
					string s = this.Expression;
					return (uint) p < (uint) s.Length ? s[p] : EOF;
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				private void Advance()
				{
					++this.Cursor;
				}

				private bool TryReadKeyword(string keyword)
				{
					//IMPORTANT: 'keyword' must be lowercased !
					int p = this.Cursor;
					string s = this.Expression;
					int r = keyword.Length;
					if ((uint) (p + r) > (uint) s.Length) return false; // not enough
					for (int i = 0; i < r; i++)
					{
						if (char.ToLowerInvariant(s[p + i]) != keyword[i]) return false;
					}
					this.Cursor = p + r;
					return true;
				}

				/// <summary>Parses a tuple</summary>
				[Pure]
				public IVarTuple ParseExpression()
				{

					char c = ReadNext();
					if (c != '(')
					{
						throw new FormatException("Invalid tuple expression. Valid tuple must start with '(' and end with ')'.");
					}

					bool expectItem = true;

					var items = new List<object>();
					while (true)
					{
						c = PeekNext();
						switch (c)
						{
							case ')':
							{
								//note: we accept a terminal ',' without the last item, to allow "(123,)" as a valid tuple.
								if (expectItem && items.Count > 1) throw new FormatException("Missing item before last ',' in Tuple expression");
								Advance();
								return FromEnumerable(items);
							}
							case EOF:
							{
								throw new FormatException("Missing ')' at the end of tuple expression.");
							}

							case ',':
							{
								if (expectItem) throw new FormatException("Missing ',' before next item in Tuple expression.");
								Advance();
								expectItem = true;
								break;
							}

							case '"':
							{ // string literal
								string s = ReadStringLiteral();
								items.Add(s);
								expectItem = false;
								break;
							}
							case '\'':
							{ // single char literal
								Advance();
								char x = ReadNext();
								c = ReadNext();
								if (c != '\'') throw new FormatException("Missing quote after character. Single quotes are for single characters. For strings, use double quotes!");
								items.Add(x);
								expectItem = false;
								break;
							}
							case '{':
							{ // Guid
								Guid g = ReadGuidLiteral();
								items.Add(g);
								expectItem = false;
								break;
							}
							case '(':
							{ // embedded tuple!
								var sub = ParseExpression();
								items.Add(sub);
								expectItem = false;
								break;
							}
							case '|':
							{ // Custom type?
								var x = ReadCustomTypeLiteral();
								items.Add(x);
								expectItem = false;
								break;
							}

							default:
							{
								if (char.IsWhiteSpace(c))
								{ // ignore whitespaces
									Advance();
									break;
								}

								if (char.IsDigit(c) || c == '-')
								{ // number!
									items.Add(ReadNumberLiteral());
									expectItem = false;
									break;
								}

								if (c is 't' or 'T')
								{ // true?
									if (!TryReadKeyword("true")) throw new FormatException("Unrecognized keyword in Tuple expression. Did you meant to write 'true' instead?");
									items.Add(TrueSingleton);
									expectItem = false;
									break;
								}

								if (c is 'f' or 'F')
								{ // false?
									if (!TryReadKeyword("false")) throw new FormatException("Unrecognized keyword in Tuple expression. Did you meant to write 'false' instead?");
									items.Add(FalseSingleton);
									expectItem = false;
									break;
								}

								throw new FormatException($"Invalid token '{c}' in Tuple expression.");
							}
						}
					}
				}

				private object ReadNumberLiteral()
				{
					bool dec = false;
					bool neg = false;
					bool exp = false;

					string s = this.Expression;
					int start = this.Cursor;
					int end = s.Length;
					int p = start;
					ulong x = 0;

					char c = s[p];
					if (c == '-')
					{
						neg = true;
					}
					else if (c != '+')
					{
						x = (ulong) (c - '0');
					}
					++p;

					while (p < end)
					{
						c = s[p];
						if (char.IsDigit(c))
						{
							x = checked(x * 10 + (ulong) (c - '0'));
							++p;
							continue;
						}

						if (c == '.')
						{
							if (dec) throw FailRedundantDotInNumber();
							if (exp) throw FailUnexpectedDotInNumberExponent();
							dec = true;
							++p;
							continue;
						}

						if (c == ',' || c == ')' || char.IsWhiteSpace(c))
						{
							break;
						}

						if (c == 'E')
						{
							if (dec) throw FailRedundantExponentInNumber();
							exp = true;
							++p;
							continue;
						}

						if (c is '-' or '+')
						{
							if (!exp) throw FailUnexpectedSignInNumber();
							++p;
							continue;
						}

						throw FailInvalidTokenInNumber(c);
					}

					this.Cursor = p;

					if (!dec && !exp)
					{
						if (neg)
						{
							if (x < int.MaxValue) return -((int) x);
							if (x < long.MaxValue) return -((long) x);
							if (x == 1UL + long.MaxValue) return long.MinValue;
							throw new OverflowException("Parsed number is too large");
						}

						if (x <= int.MaxValue)
						{
							// to reduce boxing, we have a few pre-allocated singletons for the most common values (0, 1, 2, ...)
							return x switch
							{
								0 => ZeroSingleton,
								1 => OneSingleton,
								2 => TwoSingleton,
								3 => ThreeSingleton,
								4 => FourSingleton,
								_ => (int) x
							};
						}

						if (x <= long.MaxValue) return (long) x;
						return x;
					}

#if NET5_0_OR_GREATER
					return double.Parse(s.AsSpan(start, p - start), CultureInfo.InvariantCulture);
#else
					// no span-based double.Parse
					return double.Parse(s.Substring(start, p - start), CultureInfo.InvariantCulture);
#endif
				}

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailRedundantDotInNumber() => new("Redundant '.' in number that already has a decimal point.");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailUnexpectedDotInNumberExponent() => new("Unexpected '.' in exponent part of number.");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailRedundantExponentInNumber() => new("Redundant 'E' in number that already has an exponent.");

				private static FormatException FailUnexpectedSignInNumber() => new("Unexpected sign in number.");

				private static FormatException FailInvalidTokenInNumber(char c) => new($"Unexpected token '{c}' while parsing number in Tuple expression.");

				private string ReadStringLiteral()
				{
					string s = this.Expression;
					int p = this.Cursor;
					int end = p + s.Length;

					// main loop is optimistic and assumes that the string will not be escaped.
					// If we find the first instance of '\', then we switch to a secondary loop that uses a StringBuilder to decode each character

					char c = s[p++];
					if (c != '"') throw FailMissingExpectedBackslashToken();
					int start = p;

					while (p < end)
					{
						c = s[p];
						if (c == '"')
						{
							this.Cursor = p + 1;
							return s.Substring(start, p - start);
						}

						if (c == '\\')
						{ // string is escaped, will need to decode the content
							++p;
							goto parse_escaped_string;
						}
						++p;
					}
					goto truncated_string;

				parse_escaped_string:
					bool escape = true;
					var sb = StringBuilderCache.Acquire(128);
#if NET5_0_OR_GREATER
					if (p > start + 1) sb.Append(s.AsSpan(start, p - start - 1)); // copy what we have parsed so far
#else
					// no StringBuilder.Append(ReadOnlySpan<char>)
					if (p > start + 1) sb.Append(s, start, p - start - 1); // copy what we have parsed so far
#endif
					while (p < end)
					{
						c = s[p];
						if (c == '"')
						{
							if (escape)
							{
								escape = false;
							}
							else
							{
								this.Cursor = p + 1;
								return StringBuilderCache.GetStringAndRelease(sb);
							}
						}
						else if (c == '\\')
						{
							if (!escape)
							{ // start of escape sequence
								escape = true;
								++p;
								continue;
							}
							escape = false;
						}
						else if (escape)
						{
							c = c switch
							{
								't' => '\t',
								'r' => '\r',
								'n' => '\n',
								//TODO: \x## and \u#### syntax!
								_ => throw FailInvalidTokenInString(c)
							};
							escape = false;
						}
						++p;
						sb.Append(c);
					}
				truncated_string:
					throw FailMissingDoubleAtEndOfString();
				}

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailMissingExpectedBackslashToken() => new("Expected '\"' token is missing in Tuple expression");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailInvalidTokenInString(char c) => new($"Unexpected token '\\{c}' while parsing string in Tuple expression");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailMissingDoubleAtEndOfString() => new("Missing double quote at end of string in Tuple expression");

				private Guid ReadGuidLiteral()
				{
					var s = this.Expression.AsSpan();
					int p = this.Cursor;
					int end = s.Length;
					char c = s[p];
					if (s[p] != '{') throw new FormatException($"Unexpected token '{c}' at start of GUID in Tuple expression");
					++p;
					int start = p;
					while (p < end)
					{
						c = s[p];
						if (c == '}')
						{
							var lit = s.Slice(start, p - start);
							// Shortcut: "{} or {0} means "00000000-0000-0000-0000-000000000000"
#if NET5_0_OR_GREATER
							Guid g = lit is "" or "0" ? Guid.Empty : Guid.Parse(lit);
#else
							// no span-based Guid.Parse
							Guid g = lit is "" or "0" ? Guid.Empty : Guid.Parse(lit.ToString());
#endif
							this.Cursor = p + 1;
							return g;
						}
						++p;
					}

					throw new FormatException("Invalid GUID in Tuple expression.");
				}

				private TuPackUserType ReadCustomTypeLiteral()
				{
					var s = this.Expression.AsSpan();
					int p = this.Cursor;
					int end = s.Length;
					char c = s[p];
					if (s[p] != '|') throw new FormatException($"Unexpected token '{c}' at start of User Type in Tuple expression");
					++p;
					int start = p;
					while (p < end)
					{
						c = s[p];
						if (c == '|')
						{
							var lit = s.Slice(start, p - start);
							TuPackUserType ut;
							if (lit is "System")
							{
								ut = TuPackUserType.System;
							}
							else if (lit is "Directory")
							{
								ut = TuPackUserType.Directory;
							}
							else if (lit.StartsWith("User-", StringComparison.Ordinal))
							{
								throw new NotImplementedException("Implementation parsing of custom user types in Tuple expressions");
							}
							else
							{
								break;
							}
							this.Cursor = p + 1;
							return ut;
						}
						++p;
					}

					throw new FormatException("Invalid custom User Type in Tuple expression.");
				}
			}
		}

		#endregion

	}
}
