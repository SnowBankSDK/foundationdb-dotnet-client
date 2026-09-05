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

namespace SnowBank.Data.Tuples.Binary
{
	using System;
	using System.Collections;
	using System.ComponentModel;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Tuples;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;

	/// <summary>Lazily-evaluated tuple that was unpacked from a key</summary>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class SlicedTuple : IVarTuple
		, IEquatable<SlicedTuple>, IComparable<SlicedTuple>, IComparable
		, ITupleSpanPackable, ITupleFormattable, ISliceSerializable
	{
		//REVIEW: consider renaming the type to DecodedTuple or UnpackedTuple or RawTuple ?

		/// <summary>Buffer containing the original slices.</summary>
		private readonly ReadOnlyMemory<Slice> m_slices;

		private int? m_hashCode;

		public static readonly SlicedTuple Empty = new(default);

		public SlicedTuple(ReadOnlyMemory<Slice> slices)
		{
			m_slices = slices;
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <remarks>
		/// <para>This is the same as <see cref="TuPack.Unpack(System.Slice)"/>, except that it will expose the concrete type <see cref="SlicedTuple"/> instead of the <see cref="IVarTuple"/> interface.</para>
		/// </remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static SlicedTuple Unpack(Slice packedKey)
		{
			if (packedKey.IsNull) throw new ArgumentNullException(nameof(packedKey), "Cannot unpack tuple from Nil");
			if (packedKey.Count == 0) return SlicedTuple.Empty;

			scoped var reader = new TupleReader(packedKey.Span);
			Span<Range> buffer = stackalloc Range[TuplePackers.StackTokenCount];
			// try the stack buffer first, and only allocate a heap array, sized exactly, when the tuple does not fit in it.
			if (!TuplePackers.TryUnpack(ref reader, buffer, out var st, out var error))
			{
				if (error != null) throw error;
				st = TuplePackers.UnpackWithHeapBuffer(packedKey.Span, 0);
			}
			return st.ToTuple(packedKey);
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <param name="tuple">Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></param>
		/// <remarks>
		/// <para>This is the same as <see cref="TuPack.Unpack(System.Slice)"/>, except that it will expose the concrete type <see cref="SlicedTuple"/> instead of the <see cref="IVarTuple"/> interface.</para>
		/// </remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static bool TryUnpack(Slice packedKey, [MaybeNullWhen(false)] out SlicedTuple tuple)
		{
			if (packedKey.IsNull)
			{
				tuple = null;
				return false;
			}

			if (packedKey.Count == 0)
			{
				tuple = SlicedTuple.Empty;
				return true;
			}

			scoped var reader = new TupleReader(packedKey.Span);
			Span<Range> buffer = stackalloc Range[TuplePackers.StackTokenCount];
			if (!TuplePackers.TryUnpack(ref reader, buffer, out var st, out var error))
			{
				if (error != null)
				{
					tuple = null;
					return false;
				}
				st = TuplePackers.UnpackWithHeapBuffer(packedKey.Span, 0);
			}

			tuple = st.ToTuple(packedKey);
			return true;
		}

		/// <summary>Transcode a tuple into the equivalent tuple, but backed by a <see cref="SlicedTuple"/></summary>
		/// <remarks>This methods can be useful to examine what the result of packing a tuple would be, after a round-trip to the database.</remarks>
		public static SlicedTuple Repack<TTuple>(in TTuple? tuple) where TTuple : IVarTuple?
		{
			return tuple is SlicedTuple st ? Unpack(st.ToSlice()) : Unpack(TuPack.Pack(in tuple));
		}

		/// <summary>Return the original serialized key blob that is equivalent to this tuple</summary>
		/// <returns>Packed tuple</returns>
		/// <remarks><c>SlicedTuple.Unpack(bytes).ToSlice() == bytes</c></remarks>
		public Slice ToSlice()
		{
			// pre-compute the capacity required
			int size = 0;
			foreach (var slice in m_slices.Span)
			{
				size += slice.Count;
			}

			if (size == 0)
			{
				return Slice.Empty;
			}

			// concat all slices
			var writer = new SliceWriter(size);
			WriteTo(ref writer);

			return writer.ToSlice();
		}

		public void WriteTo(ref SliceWriter writer)
		{
			foreach(var slice in m_slices.Span)
			{
				writer.WriteBytes(slice);
			}
		}

		void ITuplePackable.PackTo(TupleWriter writer)
		{
			foreach(var slice in m_slices.Span)
			{
				writer.Output.WriteBytes(slice);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ErrorCanOnlyBeTopLevel() => new($"Tuples of type {nameof(SlicedTuple)} can only be packed as top-level.");

		/// <inheritdoc />
		public bool TryPackTo(ref TupleSpanWriter writer)
		{
			if (writer.Depth != 0) throw ErrorCanOnlyBeTopLevel();

			foreach(var slice in m_slices.Span)
			{
				if (!writer.TryWriteLiteral(slice.Span))
				{
					return false;
				}
			}
			return true;
		}

		/// <inheritdoc />
		public bool TryGetSizeHint(bool embedded, out int sizeHint)
		{
			if (embedded) throw ErrorCanOnlyBeTopLevel();

			// we simply have to count the size of the already encoded chunks

			long total = 0;
			foreach (var slice in m_slices.Span)
			{
				total += slice.Count;
			}

			if (total > int.MaxValue)
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = unchecked((int) total);
			return true;
		}

		/// <inheritdoc />
		public int AppendItemsTo(ref FastStringBuilder sb)
		{
			var length = this.Count;
			if (length == 0)
			{
				return 0;
			}

			TuplePackers.StringifyBoxedTo(ref sb, GetSpan(0));

			for (int i = 1; i < length; i++)
			{
				sb.Append(", ");
				TuplePackers.StringifyBoxedTo(ref sb, GetSpan(i));
			}

			return length;
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Always)]
		public int Count => m_slices.Length;
		
#if !NETSTANDARD2_0
		// ITuple is not visible to netstandard2.0
		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;
#endif

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public object? this[int index] => TuplePackers.DeserializeBoxed(GetSpan(index));

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public SlicedTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int count = m_slices.Length;
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, count) : count;

				int len = end - begin;
				if (len <= 0) return SlicedTuple.Empty;
				if (begin == 0 && len == count) return this;
				return new SlicedTuple(m_slices.Slice(begin, len));
			}
		}

		/// <inheritdoc />
		IVarTuple IVarTuple.this[int? fromIncluded, int? toExcluded] => this[fromIncluded, toExcluded];

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public object? this[Index index] => TuplePackers.DeserializeBoxed(GetSpan(index));

		[EditorBrowsable(EditorBrowsableState.Always)]
		public SlicedTuple this[Range range]
		{
			get
			{
				int len = this.Count;
				(int offset, int count) = range.GetOffsetAndLength(len);
				if (count == 0) return SlicedTuple.Empty;
				if (offset == 0 && count == len) return this;
				return new SlicedTuple(m_slices.Slice(offset, count));
			}
		}

		/// <inheritdoc />
		IVarTuple IVarTuple.this[Range range] => this[range];

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Always)]
		public T? Get<T>(int index)
		{
			//REVIEW: consider dropping the negative indexing? We have Index now for this use-case!
			return TuplePacker<T>.Deserialize(GetSlice(index));
		}

		/// <summary>Returns the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="T">Expected type of the item</typeparam>
		/// <param name="index">Position of the item, with <c>0</c> for the first element, and <c>^1</c> for the last element</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="T"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If <paramref name="index"/> is outside the bounds of the tuple</exception>
		/// <example>
		/// <para><c>("Hello", "World", 123,).Get&lt;string&gt;(^3) => "Hello"</c></para>
		/// <para><c>("Hello", "World", 123,).Get&lt;string&gt;(^2) => "World"</c></para>
		/// <para><c>("Hello", "World", 123,).Get&lt;int&gt;(^1) => 123</c></para>
		/// <para><c>("Hello", "World", 123,).Get&lt;string&gt;(^1) => "123"</c></para>
		/// </example>
		public T? Get<T>(Index index)
		{
			return TuplePacker<T>.Deserialize(m_slices.Span[index]);
		}

		/// <summary>Returns the typed value of the first item in this tuple</summary>
		/// <returns>Value of the item at the first position, adapted into type <typeparamref name="T"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the tuple is empty</exception>
		/// <example>
		/// <para><c>("Hello", "World").First&lt;string&gt;() => "Hello"</c></para>
		/// <para><c>(123, 456).First&lt;int&gt;() => 123</c></para>
		/// <para><c>(123, 456).First&lt;string&gt;() => "123"</c></para>
		/// </example>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public T? GetFirst<T>()
		{
			var slices = m_slices.Span;
			return slices.Length != 0 ? TuplePacker<T>.Deserialize(slices[0]) : throw TupleHelpers.FailTupleIsEmpty();
		}

		/// <summary>Returns the typed value of the last item of the tuple</summary>
		/// <returns>Value of the item at the last position, adapted into type <typeparamref name="T"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the tuple is empty</exception>
		/// <example>
		/// <para><c>("Hello",).Last&lt;string&gt;() => "Hello"</c></para>
		/// <para><c>(123, 456).Last&lt;int&gt;() => 456</c></para>
		/// <para><c>(123, 456).Last&lt;string&gt;() => "456"</c></para>
		/// </example>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public T? GetLast<T>()
		{
			var slices = m_slices.Span;
			return slices.Length != 0 ? TuplePacker<T>.Deserialize(slices[^1]) : throw TupleHelpers.FailTupleIsEmpty();
		}

		/// <summary>Return the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetSlice(int index)
		{
			var slices = m_slices.Span;
			return slices[TupleHelpers.MapIndex(index, slices.Length)];
		}

		/// <summary>Return the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetSlice(Index index) => m_slices.Span[index];

		/// <summary>Return the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<byte> GetSpan(int index)
		{
			var slices = m_slices.Span;
			return slices[TupleHelpers.MapIndex(index, slices.Length)].Span;
		}

		/// <summary>Return the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<byte> GetSpan(Index index) => m_slices.Span[index].Span;

		/// <summary>Tests if the element at the specified index is <see cref="TupleSegmentType.Nil"/></summary>
		/// <returns><see langword="true"/> if this the value is either a byte string, or null</returns>
		public bool IsNil(int index) => GetElementType(index) is TupleSegmentType.Nil;

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.ByteString"/></summary>
		/// <returns><see langword="true"/> if this the value is a byte string</returns>
		/// <remarks>This could either be an array of bytes, OR a non-unicode Python string</remarks>
		public bool IsBytes(int index) => GetElementType(index) is (TupleSegmentType.ByteString);

		/// <summary>Tests if the element at the specified index is either a <see cref="TupleSegmentType.ByteString"/> or a <see cref="TupleSegmentType.Nil"/></summary>
		/// <returns><see langword="true"/> if this the value is either a byte string, or null</returns>
		/// <remarks>This could either be an array of bytes, OR a non-unicode Python string</remarks>
		public bool IsBytesOrDefault(int index) => GetElementType(index) is (TupleSegmentType.ByteString or TupleSegmentType.Nil);

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.UnicodeString"/></summary>
		/// <returns><see langword="true"/> if this the value is either a unicode string</returns>
		public bool IsUnicodeString(int index) => GetElementType(index) is (TupleSegmentType.UnicodeString);

		/// <summary>Tests if the element at the specified index is either a <see cref="TupleSegmentType.UnicodeString"/> or a <see cref="TupleSegmentType.Nil"/></summary>
		/// <returns><see langword="true"/> if this the value is either a unicode string, or null</returns>
		public bool IsUnicodeStringOrDefault(int index) => GetElementType(index) is (TupleSegmentType.UnicodeString or TupleSegmentType.Nil);

		/// <summary>Tests if the element at the specified index is an <see cref="TupleSegmentType.Integer"/></summary>
		/// <remarks>Floating points numbers (<see cref="TupleSegmentType.Single"/>, <see cref="TupleSegmentType.Double"/>, ...) or Big Integers (<see cref="TupleSegmentType.BigInteger"/>, ...) are not in this category</remarks>
		public bool IsInteger(int index) => GetElementType(index) is TupleSegmentType.Integer;

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.Double"/></summary>
		public bool IsDouble(int index) => GetElementType(index) is TupleSegmentType.Double;

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.Single"/></summary>
		public bool IsSingle(int index) => GetElementType(index) is TupleSegmentType.Single;

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.Decimal"/></summary>
		public bool IsDecimal(int index) => GetElementType(index) is TupleSegmentType.Decimal;

		/// <summary>Tests if the element at the specified index is any of the floating points types (<see cref="TupleSegmentType.Single"/>, <see cref="TupleSegmentType.Double"/>, <see cref="TupleSegmentType.Triple"/> or <see cref="TupleSegmentType.Decimal"/>)</summary>
		public bool IsFloatingPoint(int index) => GetElementType(index) is (TupleSegmentType.Double or TupleSegmentType.Single or TupleSegmentType.Decimal or TupleSegmentType.Triple);

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.BigInteger"/></summary>
		public bool IsBigInteger(int index) => GetElementType(index) is TupleSegmentType.BigInteger;

		/// <summary>Tests if the element at the specified index is a number of any type (integer, floating point or big integer)</summary>
		public bool IsNumber(int index) => GetElementType(index) is (TupleSegmentType.Integer or TupleSegmentType.Double or TupleSegmentType.Single or TupleSegmentType.Decimal or TupleSegmentType.Triple or TupleSegmentType.BigInteger);

		/// <summary>Tests if the element at the specified index is a <see cref="TupleSegmentType.Boolean"/></summary>
		public bool IsBoolean(int index) => GetElementType(index) is TupleSegmentType.Boolean;

		/// <summary>Tests if the element at the specified index is a reserved custom type code (application specific)</summary>
		public bool IsUserType(int index) => GetElementType(index) is TupleSegmentType.UserType;

		/// <summary>Returns the <see cref="TupleSegmentType">encoded type</see> of the element at the specified index</summary>
		/// <returns>This helps test if a parser element is a string, a number, a boolean, etc...</returns>
		[Pure]
		public TupleSegmentType GetElementType(int index) => TupleTypes.DecodeSegmentType(GetSlice(index));

		/// <summary>Returns the <see cref="TupleSegmentType">encoded type</see> of the element at the specified index</summary>
		[Pure]
		public TupleSegmentType GetElementType(Index index) => (TupleSegmentType) GetSlice(index)[0];

		IVarTuple IVarTuple.Append<T>(T value) => throw new NotSupportedException();

		IVarTuple IVarTuple.Concat(IVarTuple tuple) => throw new NotSupportedException();

		/// <inheritdoc />
		public void CopyTo(object?[] array, int offset)
		{
			Contract.NotNull(array);
			CopyTo(array.AsSpan(offset));
		}

		public void CopyTo(Span<object?> destination)
		{
			if (!TryCopyTo(destination))
			{
				throw new InvalidOperationException("Target buffer is too small");
			}
		}

		public bool TryCopyTo(Span<object?> destination)
		{
			var slices = m_slices.Span;
			if (destination.Length < slices.Length)
			{
				return false;
			}

			for (int i = 0; i < slices.Length;i++)
			{
				destination[i] = TuplePackers.DeserializeBoxed(slices[i]);
			}
			return true;
		}

		public void CopyTo(Span<Slice> destination) => m_slices.Span.CopyTo(destination);

		public bool TryCopyTo(Span<Slice> destination) => m_slices.Span.TryCopyTo(destination);

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			//note: I'm not sure if we're allowed to use a local variable of type Span<...> in here?
			for (int i = 0; i < m_slices.Length; i++)
			{
				yield return TuplePackers.DeserializeBoxed(m_slices.Span[i]);
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, SkipLocalsInit]
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			var sb = new FastStringBuilder(stackalloc char[128]);
			ToString(ref sb);
			return sb.ToString();
		}

		public void ToString(ref FastStringBuilder sb)
		{
			if (this.Count == 0)
			{
				sb.Append("()");
				return;
			}

			sb.Append('(');
			if (AppendItemsTo(ref sb) == 1)
			{
				sb.Append(",)");
			}
			else
			{
				sb.Append(')');
			}
		}

		public override bool Equals(object? obj) => obj is not null && ((IStructuralEquatable) this).Equals(obj, SimilarValueComparer.Default);

		public bool Equals(IVarTuple? other) => other is not null && ((IStructuralEquatable) this).Equals(other, SimilarValueComparer.Default);

		public bool Equals(SlicedTuple? other)
		{
			if (ReferenceEquals(this, other)) return true;
			if (other is null) return false;

			var thisSlices = m_slices.Span;
			var otherSlices = other.m_slices.Span;
			if (thisSlices.Length != otherSlices.Length) return false;

			for (int i = 0; i < thisSlices.Length; i++)
			{
				// we use the fact that if items are equal, then their byte representation MUST be equal as well!
				if (!thisSlices[i].Equals(otherSlices[i]))
				{
					return false;
				}
			}

			return true;
		}

		public override int GetHashCode() => ((IStructuralEquatable) this).GetHashCode(SimilarValueComparer.Default);

		[Pure]
		public int CompareTo(SlicedTuple? other)
		{
			if (ReferenceEquals(this, other)) return 0;
			if (other is null) return +1;

			var thisLen = m_slices.Length;
			var otherLen = other.m_slices.Length;

			if (thisLen == 0) return otherLen == 0 ? 0 : -1;
			if (otherLen == 0) return -1;

			var len = Math.Min(thisLen, otherLen);
			var comparer = SimilarValueComparer.Default;
			for (int i = 0; i < len; i++)
			{
				int cmp = comparer.Compare(this[i], other[i]);
				if (cmp != 0)
				{
					return cmp;
				}
			}

			return thisLen.CompareTo(otherLen);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(IVarTuple? other) => CompareTo(other, SimilarValueComparer.Default);

		private int CompareTo(IVarTuple? other, IComparer comparer)
		{
			if (other is null) return +1;

			var thisLen = m_slices.Length;
			var otherLen = other.Count;

			if (thisLen == 0) return otherLen == 0 ? 0 : -1;
			if (otherLen == 0) return -1;

			var len = Math.Min(thisLen, otherLen);
			for (int i = 0; i < len; i++)
			{
				int cmp = comparer.Compare(this[i], other[i]);
				if (cmp != 0)
				{
					return cmp;
				}
			}

			return thisLen.CompareTo(otherLen);
		}

#if !NETSTANDARD2_0
		// ITuple is not visible to netstandard2.0
		private int CompareTo(ITuple? other, IComparer comparer)
		{
			if (other is null) return +1;

			var thisLen = m_slices.Length;
			var otherLen = other.Length;

			if (thisLen == 0) return otherLen == 0 ? 0 : -1;
			if (otherLen == 0) return -1;

			var len = Math.Min(thisLen, otherLen);
			for (int i = 0; i < len; i++)
			{
				int cmp = comparer.Compare(this[i], other[i]);
				if (cmp != 0)
				{
					return cmp;
				}
			}

			return thisLen.CompareTo(otherLen);
		}
#endif

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(object? other) => CompareTo(other, SimilarValueComparer.Default);

		private int CompareTo(object? other, IComparer comparer) => other switch
		{
			null => +1,
			IVarTuple t => CompareTo(t, comparer),
#if !NETSTANDARD2_0
			// ITuple is not visible to netstandard2.0
			ITuple t => CompareTo(t, comparer),
#endif
			_ => throw new ArgumentException($"Cannot compare a SpanTuple with an instance of {other.GetType().GetFriendlyName()}")
		};


		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			if (ReferenceEquals(this, other)) return true;
			if (other is null) return false;

			if (other is SlicedTuple sliced)
			{
				return Equals(sliced);
			}

			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			bool canUseCache = ReferenceEquals(comparer, SimilarValueComparer.Default);

			if (m_hashCode.HasValue && canUseCache)
			{
				return m_hashCode.Value;
			}

			var slices = m_slices.Span;

			// only use up to 3 items: the first, and the last two
			int h = slices.Length switch
			{
				0 => 0,
				1 => TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[0]), comparer),
				2 => TupleHelpers.CombineHashCodes(
						TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[0]), comparer),
						TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[1]), comparer)
					),
				3 => TupleHelpers.CombineHashCodes(
						slices.Length,
						TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[0]), comparer), TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[1]), comparer),
						TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[2]), comparer)
					),
				_ => TupleHelpers.CombineHashCodes(
					slices.Length,
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[0]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[^2]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(slices[^1]), comparer)
					),
			};

			if (canUseCache)
			{
				m_hashCode = h;
			}
			return h;
		}

		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer) => comparer.GetHashCode(m_slices.Span[index]);

		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => CompareTo(other, comparer);

	}

}
