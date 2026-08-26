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
	using System.Collections;
	using System.ComponentModel;
	using SnowBank.Runtime.Converters;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Runtime;

	/// <summary>Lazily-evaluated tuple that was unpacked from a key</summary>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public readonly ref struct SpanTuple
#if NET9_0_OR_GREATER
		: IVarTuple
		, IEquatable<SpanTuple>, IComparable<SpanTuple>, IComparable
		, ITupleSpanPackable, ISliceSerializable, ITupleFormattable
#endif
	{

		/// <summary>Buffer containing the original content.</summary>
		private readonly ReadOnlySpan<byte> m_buffer;

		/// <summary>Buffer containing the location of each slice.</summary>
		private readonly ReadOnlySpan<Range> m_slices;

		public static SpanTuple Empty => default;

		public SpanTuple(ReadOnlySpan<byte> buffer, ReadOnlySpan<Range> slices)
		{
			m_buffer = buffer;
			m_slices = slices;
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <remarks>
		/// <para>This is the same as <see cref="TuPack.Unpack(System.Slice)"/>, except that it will expose the concrete type <see cref="SpanTuple"/> instead of the <see cref="IVarTuple"/> interface.</para>
		/// </remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static SpanTuple Unpack(Slice packedKey)
		{
			if (packedKey.IsNull) throw new ArgumentNullException(nameof(packedKey), "Cannot unpack tuple from Nil");
			return Unpack(packedKey.Span);
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <remarks>
		/// <para>This is the same as <see cref="TuPack.Unpack(System.Slice)"/>, except that it will expose the concrete type <see cref="SpanTuple"/> instead of the <see cref="IVarTuple"/> interface.</para>
		/// </remarks>
		[Pure]
		public static SpanTuple Unpack(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			return TuplePackers.Unpack(ref reader);
		}

		/// <summary>Transcode a tuple into the equivalent tuple, but backed by a <see cref="SpanTuple"/></summary>
		/// <remarks>This methods can be useful to examine what the result of packing a tuple would be, after a round-trip to the database.</remarks>
		public static SpanTuple Repack<TTuple>(in TTuple? tuple) where TTuple : IVarTuple?
		{
			return tuple is SlicedTuple st ? Unpack(st.ToSlice()) : Unpack(TuPack.Pack(in tuple));
		}

		/// <summary>Return the original serialized key blob that is equivalent to this tuple</summary>
		/// <returns>Packed tuple</returns>
		/// <remarks><c>SpanTuple.Unpack(bytes).ToSlice() == bytes</c></remarks>
		public Slice ToSlice()
		{
			// pre-compute the capacity required
			var bufferLen = m_buffer.Length;
			int size = 0;
			foreach (var slice in m_slices)
			{
				size += slice.GetOffsetAndLength(bufferLen).Length;
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

		/// <inheritdoc />
		public void WriteTo(ref SliceWriter writer)
		{
			var buffer = m_buffer;
			foreach(var slice in m_slices)
			{
				writer.WriteBytes(buffer[slice]);
			}
		}

		/// <inheritdoc />
		public void PackTo(TupleWriter writer)
		{
			var buffer = m_buffer;
			foreach(var slice in m_slices)
			{
				writer.Output.WriteBytes(buffer[slice]);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ErrorCanOnlyBeTopLevel() => new($"Tuples of type {nameof(SlicedTuple)} can only be packed as top-level.");

		/// <inheritdoc />
		public bool TryPackTo(ref TupleSpanWriter writer)
		{
			if (writer.Depth != 0) throw ErrorCanOnlyBeTopLevel();

			var buffer = m_buffer;
			foreach(var slice in m_slices)
			{
				if (!writer.TryWriteLiteral(buffer[slice]))
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

			var bufferLen = m_buffer.Length;
			long total = 0;
			foreach (var slice in m_slices)
			{
				total += slice.GetOffsetAndLength(bufferLen).Length;
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

			TuplePackers.StringifyBoxedTo(ref sb, this.GetSpan(0));

			for (int i = 1; i < length; i++)
			{
				sb.Append(", ");
				TuplePackers.StringifyBoxedTo(ref sb, this.GetSpan(i));
			}

			return length;
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Always)]
		public int Count => m_slices.Length;

#if NET9_0_OR_GREATER

		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;

#endif

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public object? this[int index] => TuplePackers.DeserializeBoxed(GetSpan(index));

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public SpanTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int count = m_slices.Length;
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, count) : count;

				int len = end - begin;
				if (len <= 0) return default;
				if (begin == 0 && len == count) return this;
				return new(m_buffer, m_slices.Slice(begin, len));
			}
		}

#if NET9_0_OR_GREATER

		/// <inheritdoc />
		IVarTuple IVarTuple.this[int? fromIncluded, int? toExcluded] => throw new NotSupportedException();

#endif

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public object? this[Index index] => TuplePackers.DeserializeBoxed(m_buffer[m_slices[index.GetOffset(m_slices.Length)]]);

		[EditorBrowsable(EditorBrowsableState.Always)]
		public SpanTuple this[Range range]
		{
			get
			{
				int len = this.Count;
				(int offset, int count) = range.GetOffsetAndLength(len);
				if (count == 0) return default;
				if (offset == 0 && count == len) return this;
				return new(m_buffer, m_slices.Slice(offset, count));
			}
		}

#if NET9_0_OR_GREATER

		/// <inheritdoc />
		IVarTuple IVarTuple.this[Range range] => throw new NotSupportedException();

#endif

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
			return TuplePacker<T>.Deserialize(m_buffer[m_slices[index]]);
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
			var slices = m_slices;
			return slices.Length != 0 ? TuplePacker<T>.Deserialize(m_buffer[slices[0]]) : throw TupleHelpers.FailTupleIsEmpty();
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
			var slices = m_slices;
			return slices.Length != 0 ? TuplePacker<T>.Deserialize(m_buffer[slices[^1]]) : throw TupleHelpers.FailTupleIsEmpty();
		}

		/// <summary>Returns the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<byte> GetSpan(int index)
		{
			var slices = m_slices;
			return m_buffer[slices[TupleHelpers.MapIndex(index, slices.Length)]];
		}

		/// <summary>Returns the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetSlice(int index)
		{
			var slices = m_slices;
			return Slice.FromBytes(m_buffer[slices[TupleHelpers.MapIndex(index, slices.Length)]]);
		}

		/// <summary>Test if the element at the specified index is <see cref="TupleSegmentType.Nil"/></summary>
		/// <returns><see langword="true"/> if this the value is either a byte string, or null</returns>
		public bool IsNil(int index) => GetElementType(index) is TupleSegmentType.Nil;

		/// <summary>Test if the element at the specified index is a <see cref="TupleSegmentType.ByteString"/></summary>
		/// <returns><see langword="true"/> if this the value is a byte string</returns>
		/// <remarks>This could either be an array of bytes, OR a non-unicode Python string</remarks>
		public bool IsBytes(int index) => GetElementType(index) is (TupleSegmentType.ByteString);

		/// <summary>Test if the element at the specified index is either a <see cref="TupleSegmentType.ByteString"/> or a <see cref="TupleSegmentType.Nil"/></summary>
		/// <returns><see langword="true"/> if this the value is either a byte string, or null</returns>
		/// <remarks>This could either be an array of bytes, OR a non-unicode Python string</remarks>
		public bool IsBytesOrDefault(int index) => GetElementType(index) is (TupleSegmentType.ByteString or TupleSegmentType.Nil);

		/// <summary>Test if the element at the specified index is a <see cref="TupleSegmentType.UnicodeString"/></summary>
		/// <returns><see langword="true"/> if this the value is either a unicode string</returns>
		public bool IsUnicodeString(int index) => GetElementType(index) is (TupleSegmentType.UnicodeString);

		/// <summary>Test if the element at the specified index is either a <see cref="TupleSegmentType.UnicodeString"/> or a <see cref="TupleSegmentType.Nil"/></summary>
		/// <returns><see langword="true"/> if this the value is either a unicode string, or null</returns>
		public bool IsUnicodeStringOrDefault(int index) => GetElementType(index) is (TupleSegmentType.UnicodeString or TupleSegmentType.Nil);

		/// <summary>Test if the element at the specified index is an <see cref="TupleSegmentType.Integer"/></summary>
		public bool IsInteger(int index) => GetElementType(index) is TupleSegmentType.Integer;

		/// <summary>Test if the element at the specified index is a <see cref="TupleSegmentType.Double"/></summary>
		public bool IsDouble(int index) => GetElementType(index) is TupleSegmentType.Double;

		/// <summary>Test if the element at the specified index is a <see cref="TupleSegmentType.Single"/></summary>
		public bool IsSingle(int index) => GetElementType(index) is TupleSegmentType.Single;

		/// <summary>Test if the element at the specified index is any of the floating points types (<see cref="TupleSegmentType.Single"/>, <see cref="TupleSegmentType.Double"/>, <see cref="TupleSegmentType.Triple"/> or <see cref="TupleSegmentType.Decimal"/>)</summary>
		public bool IsFloatingPoint(int index) => GetElementType(index) is (TupleSegmentType.Double or TupleSegmentType.Single or TupleSegmentType.Decimal or TupleSegmentType.Triple);

		/// <summary>Test if the element at the specified index is a number of any type (integer or floating point)</summary>
		public bool IsNumber(int index) => GetElementType(index) is (TupleSegmentType.Integer or TupleSegmentType.Double or TupleSegmentType.Single or TupleSegmentType.Decimal or TupleSegmentType.Triple);

		/// <summary>Return the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<byte> GetSpan(Index index) => m_buffer[m_slices[index]];

		/// <summary>Return the encoded binary representation of the element at the specified index</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetSlice(Index index) => Slice.FromBytes(m_buffer[m_slices[index]]);

		/// <summary>Returns the <see cref="TupleSegmentType">encoded type</see> of the element at the specified index</summary>
		/// <returns>This helps test if a parser element is a string, a number, a boolean, etc...</returns>
		[Pure]
		public TupleSegmentType GetElementType(int index) => TupleTypes.DecodeSegmentType(GetSlice(index));

		/// <summary>Returns the <see cref="TupleSegmentType">encoded type</see> of the element at the specified index</summary>
		[Pure]
		public TupleSegmentType GetElementType(Index index) => (TupleSegmentType) GetSlice(index)[0];

#if NET9_0_OR_GREATER

		IVarTuple IVarTuple.Append<T>(T value) => throw new NotSupportedException();

		IVarTuple IVarTuple.Concat(IVarTuple tuple) => throw new NotSupportedException();

#endif

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
			var slices = m_slices;
			if (destination.Length < slices.Length)
			{
				return false;
			}

			var buffer = m_buffer;
			for (int i = 0; i < slices.Length;i++)
			{
				destination[i] = TuplePackers.DeserializeBoxed(buffer[slices[i]]);
			}
			return true;
		}

		public void CopyTo(Span<Slice> destination)
		{
			var slices = m_slices;
			if (slices.Length > destination.Length) throw ThrowHelper.ArgumentException(nameof(destination), "Destination buffer is too short");
			var buffer = m_buffer;
			for (int i = 0; i < slices.Length; i++)
			{
				destination[i] = Slice.FromBytes(buffer[slices[i]]);
			}
		}

		public bool TryCopyTo(Span<Slice> destination)
		{
			var slices = m_slices;
			if (slices.Length > destination.Length)
			{
				return false;
			}
			var buffer = m_buffer;
			for (int i = 0; i < slices.Length; i++)
			{
				destination[i] = Slice.FromBytes(buffer[slices[i]]);
			}
			return true;
		}

		public object?[] ToArray()
		{
			var slices = m_slices;
			var buffer = m_buffer;
			var items = new object?[slices.Length];
			//note: I'm not sure if we're allowed to use a local variable of type Span<...> in here?
			for (int i = 0; i < slices.Length; i++)
			{
				items[i] = TuplePackers.DeserializeBoxed(buffer[slices[i]]);
			}

			return items;
		}

		public IVarTuple ToTuple() => new ListTuple<object?>(ToArray());

		internal SlicedTuple ToTuple(Slice original)
		{
			// the caller MUST pass the original Slice that contains our buffer (or with the same content)
			var slices = m_slices;
			if (m_slices.Length == 0) return SlicedTuple.Empty;

			var tmp = new Slice[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				tmp[i] = original[slices[i]];
			}
			return new SlicedTuple(tmp);
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			//TODO: PERF: we cannot use 'yield' since we are a ref struct, so we will have to allocate :/
			return ((IEnumerable<object?>) ToArray()).GetEnumerator();
		}

#if NET9_0_OR_GREATER

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

#endif

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, SkipLocalsInit]
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			if (this.Count == 0) return "()";

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


		public override bool Equals(object? obj) => obj is not null && Equals(obj, SimilarValueComparer.Default);

		public bool Equals(SpanTuple other)
		{
			var thisSlices = m_slices;
			var otherSlices = other.m_slices;
			if (thisSlices.Length != otherSlices.Length) return false;

			var thisBuffer = m_buffer;
			var otherBuffer = other.m_buffer;

			for (int i = 0; i < thisSlices.Length; i++)
			{
				// we use the fact that if items are equal, then their byte representation MUST be equal as well!
				if (!thisBuffer[thisSlices[i]].SequenceEqual(otherBuffer[otherSlices[i]]))
				{
					return false;
				}
			}

			return true;
		}

		public bool Equals(IVarTuple? other) => other is not null && Equals(other, SimilarValueComparer.Default);

		private bool Equals(object? other, IEqualityComparer comparer)
		{
			if (other is null) return false;
			if (other is not IVarTuple vt) return false;

			var slices = m_slices;
			if (slices.Length != vt.Count) return false;
			for (int i = 0; i < slices.Length; i++)
			{
				if (!comparer.Equals(this[i], vt[i])) return false;
			}

			return true;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => GetHashCode(SimilarValueComparer.Default);

		[Pure]
		public int CompareTo(SpanTuple other)
		{
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

#if NET9_0_OR_GREATER

		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) => Equals(other, comparer);

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) => GetHashCode(comparer);

		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => CompareTo(other, comparer);

#endif

		private int GetHashCode(IEqualityComparer comparer)
		{
			var slices = m_slices;
			var buffer = m_buffer;

			// only use up to 3 items: the first, and the last two
			switch (slices.Length)
			{
				case 0: return 0;
				case 1: return TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[0]]), comparer);
				case 2: return TupleHelpers.CombineHashCodes(
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[0]]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[1]]), comparer)
				);
				case 3: return TupleHelpers.CombineHashCodes(
					slices.Length,
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[0]]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[1]]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[2]]), comparer)
				);
				default: return TupleHelpers.CombineHashCodes(
					slices.Length,
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[0]]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[^2]]), comparer),
					TupleHelpers.ComputeHashCode(TuplePackers.DeserializeBoxed(buffer[slices[^1]]), comparer)
				);
			}
		}

#if NET9_0_OR_GREATER

		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer) => comparer.GetHashCode(m_slices[index]);

#endif

	}

}
