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

#pragma warning disable IDE0004

namespace System
{
	using System.Buffers;
	using System.ComponentModel;
	using System.IO;
	using System.IO.Pipelines;
	using System.Numerics;
	using System.Runtime.InteropServices;
	using System.Security.Cryptography;
	using System.Text;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;
	using SnowBank.Data.Binary;

	/// <summary>Delimits a read-only section of a byte array</summary>
	/// <remarks>
	/// A <c>Slice</c> is the logical equivalent to a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;byte&gt;</see>. It represents a segment of bytes backed by an array, at a certain offset.
	/// It is considered "read-only", in a sense that <i>consumers</i> of this type SHOULD NOT attempt to modify the content of the slice. Though, it is <b>NOT</b> guaranteed the content of a slice will not change, if the backing array is mutated directly.
	/// This type as several advantages over <see cref="ReadOnlyMemory{T}"/> or <see cref="Span{T}"/> when working with legacy APIs that don't support spans directly, and can also be stored one the heap.
	/// </remarks>
	[PublicAPI, ImmutableObject(true), DebuggerDisplay("{ToDebuggerDisplay(),nq}"), DebuggerTypeProxy(typeof(DebugView))]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
#if NET8_0_OR_GREATER
	[CollectionBuilder(typeof(Slice), nameof(Slice.FromBytes))]
#endif
	public readonly partial struct Slice : IEquatable<Slice>, IEquatable<ArraySegment<byte>>, IEquatable<byte[]>, IComparable<Slice>, ISliceSerializable, ISpanFormattable, ISpanEncodable
#if NET8_0_OR_GREATER
		, IComparisonOperators<Slice, Slice, bool>
#endif
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>, IEquatable<Span<byte>>, IEquatable<ReadOnlyMemory<byte>>
		, IComparable<ReadOnlySpan<byte>>, IComparable<Span<byte>>, IComparable<ReadOnlyMemory<byte>>
		, IEquatable<ReadOnlySpan<char>>, IEquatable<ReadOnlyMemory<char>>
#endif
	{
		#region Static Members...

		/// <summary>Null slice ("no segment")</summary>
		public static readonly Slice Nil;

		/// <summary>Empty slice ("segment of 0 bytes")</summary>
		//note: we allocate a 1-byte array so that we can get a pointer to &slice.Array[slice.Offset] even for the empty slice
		public static readonly Slice Empty = new(new byte[1], 0, 0);

		/// <summary>Cached array of bytes from 0 to 255</summary>
		internal static readonly byte[] ByteSprite = CreateByteSprite();

		private static byte[] CreateByteSprite()
		{
			var tmp = new byte[256];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = (byte) i;
			return tmp;
		}

		#endregion

		//REVIEW: Layout: should we maybe swap things around? .Count seems to be the most often touched field before the rest
		// => Should it be Array/Offset/Count (current), or Count/Offset/Array ?

		/// <summary>Pointer to the buffer (or null for <see cref="Nil"/>)</summary>
		public readonly byte[] Array;

		/// <summary>Offset of the first byte of the slice in the parent buffer</summary>
		public readonly int Offset;

		/// <summary>Number of bytes in the slice</summary>
		public readonly int Count;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Slice(byte[] array, int offset, int count)
		{
			//Paranoid.Requires(array is not null && offset >= 0 && offset <= array.Length && count >= 0 && offset + count <= array.Length);
			this.Array = array;
			this.Offset = offset;
			this.Count = count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Slice(byte[] array)
		{
			//Paranoid.Requires(array is not null);
			this.Array = array;
			this.Offset = 0;
			this.Count = array.Length;
		}

		/// <summary>Create a slice filled with zeroes</summary>
		/// <param name="count">Number of zeroes</param>
		[Pure]
		public static Slice Zero(int count)
		{
			Contract.Positive(count);
			return count != 0 ? new Slice(new byte[count]) : Empty;
		}

		/// <summary>Creates a new Slice with a specific length and initializes it after creation by using the specified callback.</summary>
		/// <param name="length">The length of the slice to create.</param>
		/// <param name="state">The element to pass to <paramref name="action" />.</param>
		/// <param name="action">A callback to initialize the slice.</param>
		/// <typeparam name="TState">The type of the element to pass to <paramref name="action" />.</typeparam>
		/// <returns>The created slice.</returns>
		public static Slice Create<TState>(int length, TState state, SpanAction<byte, TState> action)
		{
			Contract.NotNull(action);
			Contract.Positive(length);

			if (length == 0)
			{
				return Slice.Empty;
			}

			var tmp = new byte[length];
			action(tmp, state);
			return new Slice(tmp, 0, length);
		}

		/// <summary>Creates a new slice with a copy of the array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice Copy(byte[]? source) => FromBytes(source);

		/// <summary>Creates a new slice with a copy of the array segment</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice Copy(byte[]? source, int offset, int count)
		{
			return count == 0  && source is null && offset == 0 ? Slice.Nil : FromBytes(source.AsSpan(offset, count));
		}

		/// <summary>Creates a new slice with a copy of the span</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice Copy(ReadOnlySpan<byte> source) => FromBytes(source);

		/// <summary>Creates a new slice with a copy of the span, using a scratch buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice Copy(ReadOnlySpan<byte> source, ref byte[]? buffer)
		{
			if (source.Length == 0) return Empty;
			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, BitHelpers.NextPowerOfTwo(source.Length));
			source.CopyTo(tmp);
			return new Slice(tmp, 0, source.Length);
		}

		/// <summary>Creates a new slice with a copy of the span</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice Copy(Span<byte> source)
		{
			return source.Length switch
			{
				0 => Slice.Empty,
				1 => FromByte(source[0]),
				_ => new Slice(source.ToArray())
			};
		}

		/// <summary>Creates a new slice by copying the contents of an array of bytes</summary>
		/// <param name="source">Array of bytes to copy</param>
		/// <returns><see cref="Slice"/> that points to a copy of the bytes in <paramref name="source"/>, or <see cref="Slice.Nil"/> if <paramref name="source"/> is <c>null</c></returns>
		/// <remarks>Returns the <see cref="Slice.Empty"/> singleton if <paramref name="source"/> is empty</remarks>
		/// <example><code lang="c#">
		/// Slice.FromBytes((byte[]) null)             // => Slice.Nil
		/// Slice.FromBytes(new byte[0])               // => Slice.Empty
		/// Slice.FromBytes(new byte[] { 0x12, 0x34 }) // => [ 0x12, 0x34 ]
		/// Slice.FromBytes("Hello"u8.ToArray())       // => [ 0x48, 0x65, 0x6c, 0x6c, 0x6f ]
		/// </code></example>
		/// <seealso cref="FromBytes(ReadOnlySpan{byte})"/>
		[Pure]
		public static Slice FromBytes(byte[]? source)
		{
			if (source is null) return Slice.Nil;
			return source.Length switch
			{
				0 => Slice.Empty,
				1 => FromByte(source[0]),
				_ => new Slice(source.ToArray())
			};
		}

		/// <summary>Creates a new slice by copying the contents of a span of bytes</summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <returns><see cref="Slice"/> that points to a copy of the bytes in <paramref name="source"/></returns>
		/// <remarks>
		/// <para>Returns the <see cref="Slice.Empty"/> singleton if <paramref name="source"/> is empty.</para>
		/// <para>Please be careful when using UTF-8 string literals: the value <c>"\xff..."u8</c> will be encoded as UTF-8, meaning that it will start with bytes <c>[ 0xC3, 0xBF, ...]</c> and <b>NOT</b> <c>[ 0xFF, .... ]</c> as you could expect! Only <c>'\x00'</c> is safe to use in this way.</para>
		/// </remarks>
		/// <example><code lang="c#">
		/// Slice.FromBytes([])             // => Slice.Empty
		/// Slice.FromBytes([ 0x12, 0x34 ]) // => [ 0x12, 0x34 ]
		/// Slice.FromBytes("Hello"u8)      // => [ 0x48, 0x65, 0x6c, 0x6c, 0x6f ]
		/// Slice.FromBytes("\x00"u8)       // => [ 0x00 ]
		/// Slice.FromBytes("\xff"u8)       // => [ 0xC3, 0xBF ] !!!
		/// </code></example>
		/// <seealso cref="FromBytes(byte[])"/>
		[Pure]
		public static Slice FromBytes(ReadOnlySpan<byte> source)
		{
			return source.Length switch
			{
				0 => Slice.Empty,
				1 => FromByte(source[0]),
				_ => new Slice(source.ToArray())
			};
		}

		/// <summary>Creates a new slice by copying the contents of a span of bytes</summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <returns><see cref="Slice"/> that points to a copy of the bytes in <paramref name="source"/></returns>
		/// <remarks>
		/// <para>Returns the <see cref="Slice.Empty"/> singleton if <paramref name="source"/> is empty.</para>
		/// <para>Please be careful when using UTF-8 string literals: the value <c>"\xff..."u8</c> will be encoded as UTF-8, meaning that it will start with bytes <c>[ 0xC3, 0xBF, ...]</c> and <b>NOT</b> <c>[ 0xFF, .... ]</c> as you could expect! Only <c>'\x00'</c> is safe to use in this way.</para>
		/// </remarks>
		/// <example><code lang="c#">
		/// Slice.FromBytes([])             // => Slice.Empty
		/// Slice.FromBytes([ 0x12, 0x34 ]) // => [ 0x12, 0x34 ]
		/// Slice.FromBytes("Hello"u8)      // => [ 0x48, 0x65, 0x6c, 0x6c, 0x6f ]
		/// Slice.FromBytes("\x00"u8)       // => [ 0x00 ]
		/// Slice.FromBytes("\xff"u8)       // => [ 0xC3, 0xBF ] !!!
		/// </code></example>
		[Pure]
		public static Slice FromBytes(Span<byte> source)
		{
			return source.Length switch
			{
				0 => Slice.Empty,
				1 => FromByte(source[0]),
				_ => new Slice(source.ToArray())
			};
		}

		/// <summary>Creates a new <see cref="Slice"/> by copying the contents of a span of bytes, using a provided scratch buffer</summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <param name="buffer">Buffer that should be used to store the bytes. If <c>null</c> or too small, it will be replaced by a newly allocated buffer (with length rounded to the next power of two)</param>
		/// <returns><see cref="Slice"/> that points to a copy of the bytes in <paramref name="source"/>, and uses the <paramref name="buffer"/> as its backing store</returns>
		/// <remarks>
		/// <para>Returns the <see cref="Slice.Empty"/> singleton if <paramref name="source"/> is empty.</para>
		/// <para>Please be careful when using UTF-8 string literals: the value <c>"\xff..."u8</c> will be encoded as UTF-8, meaning that it will start with bytes <c>[ 0xC3, 0xBF, ...]</c> and <b>NOT</b> <c>[ 0xFF, .... ]</c> as you could expect! Only <c>'\x00'</c> is safe to use in this way.</para>
		/// </remarks>
		/// <example><code>
		/// // no initial buffer
		/// byte[]? buffer = null;
		/// Slice.FromBytes([ 0x12, 0x34 ], ref buffer) // => allocates a buffer of length 2
		/// Slice.FromBytes([ 0x56, 0x78 ], ref buffer) // => reuses the same buffer
		/// Slice.FromBytes("Hello World"u8, ref buffer) // => allocates a new larger buffer of length 16
		/// // with initial buffer
		/// byte[] buffer = new byte[8];
		/// Slice.FromBytes([ 0x12, 0x34 ], ref buffer) // => uses the initial buffer
		/// Slice.FromBytes([ 0x56, 0x78 ], ref buffer) // => uses the initial buffer
		/// Slice.FromBytes("Hello World"u8, ref buffer) // => allocates a new larger buffer of length 16
		/// </code></example>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static Slice FromBytes(ReadOnlySpan<byte> source, ref byte[]? buffer)
		{
			if (source.Length == 0) return Empty;
			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, BitHelpers.NextPowerOfTwo(source.Length));
			source.CopyTo(tmp);
			return new Slice(tmp, 0, source.Length);
		}

		/// <summary>Creates a new <see cref="SliceOwner"/> that wraps a copy the contents of a span of bytes, using a provided <see cref="ArrayPool{T}"/></summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <param name="pool">Pool used to allocate the backing array</param>
		/// <returns><see cref="SliceOwner"/> that points to a copy of the bytes in <paramref name="source"/>, using an array rented from the <paramref name="pool"/> as its backing store.</returns>
		/// <remarks>
		/// <para>The return value must be <see cref="SliceOwner.Dispose">disposed</see> for the buffer to return to the pool.</para>
		/// <para>Please be careful when using UTF-8 string literals: the value <c>"\xff..."u8</c> will be encoded as UTF-8, meaning that it will start with bytes <c>[ 0xC3, 0xBF, ...]</c> and <b>NOT</b> <c>[ 0xFF, .... ]</c> as you could expect! Only <c>'\x00'</c> is safe to use in this way.</para>
		/// </remarks>
		/// <example><code>
		/// Span&lt;byte> data = /* .... */;
		/// using(var buffer = Slice.FromBytes(data, ArrayPool&lt;byte>.Shared))
		/// {
		///    // use buffer.Data or buffer.Memory or buffer.Span
		/// }
		/// </code></example>
		[Pure]
		public static SliceOwner FromBytes(ReadOnlySpan<byte> source, ArrayPool<byte> pool)
		{
			if (source.Length == 0) return SliceOwner.Empty;
			var tmp = pool.Rent(source.Length);
			source.CopyTo(tmp);
			return SliceOwner.Create(new Slice(tmp, 0, source.Length), pool);
		}

		/// <summary>Implicitly converts a <see cref="Slice"/> into an <see cref="ArraySegment{T}">ArraySegment&lt;byte&gt;</see></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ArraySegment<byte>(Slice value)
		{
			return value.HasValue ? new ArraySegment<byte>(value.Array, value.Offset, value.Count) : default;
		}

		/// <summary>Implicitly converts an <see cref="ArraySegment{T}">ArraySegment&lt;byte&gt;</see> into a <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Slice(ArraySegment<byte> value)
		{
			if (value.Count == 0) return value.Array is null ? default : Empty;
			return new Slice(value.Array!, value.Offset, value.Count);
		}

		/// <summary>Returns a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see> that wraps the content of this slice</summary>
		public ReadOnlySpan<byte> Span
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(this.Array, this.Offset, this.Count);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal ReadOnlySpan<byte> ValidateSpan()
		{
			EnsureSliceIsValid();
			return new ReadOnlySpan<byte>(this.Array, this.Offset, this.Count);
		}

		/// <summary>Returns a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;byte&gt;</see> that wraps the content of this slice</summary>
		public ReadOnlyMemory<byte> Memory
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(this.Array, this.Offset, this.Count);
		}

		/// <summary>Returns <see langword="true"/> is the slice is not null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool HasValue
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array is not null;
		}

		/// <summary>Returns <see langword="true"/> if the slice is null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool IsNull
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array is null;
		}

		/// <summary>Return <see langword="true"/> if the slice is not null but contains 0 bytes</summary>
		/// <remarks>A null slice is NOT empty</remarks>
		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count == 0 && this.Array is not null;
		}

		/// <summary>Returns <see langword="true"/> if the slice is null or empty, or <see langword="false"/> if it contains at least one byte</summary>
		public bool IsNullOrEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count == 0;
		}

		/// <summary>Returns <see langword="true"/> if the slice contains at least one byte, or <see langword="false"/> if it is null or empty</summary>
		public bool IsPresent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count != 0;
		}

		/// <summary>Throws an exception if the slice is equal to <see cref="Slice.Nil"/></summary>
		public static void ThrowIfNull(Slice argument, string? message = null, [CallerArgumentExpression("argument")] string? paramName = null)
		{
			if (argument.IsNull) throw ThrowHelper.ArgumentException(paramName!, message ?? "Slice cannot be Nil");
		}

		/// <summary>Throws an exception if the slice is equal to <see cref="Slice.Nil"/> or <see cref="Slice.Empty"/></summary>
		public static void ThrowIfNullOrEmpty(Slice argument, string? message = null, [CallerArgumentExpression("argument")] string? paramName = null)
		{
			if (argument.IsNullOrEmpty) throw ThrowHelper.ArgumentException(paramName!, message ?? "Slice cannot be Nil or Empty");
		}

		/// <summary>Replaces <see cref="Nil"/> with <see cref="Empty"/></summary>
		/// <returns>The same slice if it is not <see cref="Nil"/>; otherwise, <see cref="Empty"/></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice OrEmpty()
		{
			return this.Count != 0? this : Empty;
		}

		/// <summary>Copies the contents of this slice into a new array.</summary>
		/// <returns>An array containing the data in the current slice.</returns>
		/// <remarks>
		/// <para>This will return an empty array for both <see cref="Slice.Nil"/> and <see cref="Slice.Empty"/>.</para>
		/// <para>If you need to distinguish between both, you can use <see cref="GetBytes()"/> which will return <see langword="null"/> for <see cref="Slice.Nil"/>.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] ToArray()
		{
			return this.Count != 0 ? this.Span.ToArray() : [ ];
		}

		/// <summary>Copies the contents of this slice into a new array.</summary>
		/// <returns>An array containing the data in the current slice.</returns>
		/// <remarks>
		/// <para>This will return an empty array for both <see cref="Slice.Nil"/> and <see cref="Slice.Empty"/>.</para>
		/// <para>If you need to distinguish between both, you can use <see cref="GetBytes()"/> which will return <see langword="null"/> for <see cref="Slice.Nil"/>.</para>
		/// </remarks>
		[Pure]
		public byte[]? GetBytes()
		{
			return this.Count != 0 ? this.Span.ToArray() : this.Array is not null ? [ ] : null;
		}

		/// <summary>Return a byte array containing a subset of the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of a subset of the slice, or null</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] GetBytes(int offset, int count)
		{
			//TODO: throw if this.Array is null ? (what does "Slice.Nil.GetBytes(..., 0)" mean ?)
			return this.Span.Slice(offset, count).ToArray();
		}

		/// <summary>Return a SliceReader that can decode this slice into smaller fields</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceReader ToSliceReader()
		{
			return new SliceReader(this);
		}

		/// <summary>Returns a new slice that contains an isolated copy of the buffer</summary>
		/// <returns>Slice that is equivalent, but is isolated from any changes to the buffer</returns>
		[Pure]
		public Slice Copy() =>
			  this.Count != 0 ? new(this.Span.ToArray())
			: this.IsNull ? default
			: Empty;

		/// <summary>Map an offset in the slice into the absolute offset in the buffer, without any bound checking</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int UnsafeMapToOffset(int index)
		{
			return this.Offset + NormalizeIndex(index);
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		/// <exception cref="IndexOutOfRangeException">If the index is outside the slice</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int MapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			if ((uint) p >= (uint) this.Count) throw UnsafeHelpers.Errors.IndexOutOfBound();
			return checked(this.Offset + p);
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		/// <exception cref="IndexOutOfRangeException">If the index is outside the slice</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int MapToOffset(Index index)
		{
			int p = index.GetOffset(this.Count);
			if ((uint) p >= (uint) this.Count) throw UnsafeHelpers.Errors.IndexOutOfBound();
			return checked(this.Offset + p);
		}

		/// <summary>Normalize negative index values into offset from the start</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Relative offset from the start of the slice</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int NormalizeIndex(int index)
		{
			return index < 0 ? checked(index + this.Count) : index;
		}

		/// <summary>Returns the value of one byte in the slice</summary>
		/// <param name="index">Offset of the byte (negative values means start from the end)</param>
		public byte this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array[MapToOffset(index)];
		}

		/// <summary>Returns a reference to a specific position in the slice</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref readonly byte ItemRef(int index)
		{
			return ref this.Array[MapToOffset(index)];
		}

		/// <summary>Returns a substring of the current slice that fits within the specified index range</summary>
		/// <param name="start">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="end">The end position (excluded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Slice that only contain the specified range, but shares the same underlying buffer.</returns>
		public Slice this[int start, int end]
		{
			get
			{
				start = NormalizeIndex(start);
				end = NormalizeIndex(end);

				// bound check
				if (start < 0) start = 0;
				if (end > this.Count) end = this.Count;

				if (start >= end) return Empty;
				if (start == 0 && end == this.Count) return this;

				checked { return new(this.Array, this.Offset + start, end - start); }
			}
		}

		/// <summary>Returns the value of the byte at the specified index in the slice</summary>
		/// <param name="index">Offset of the byte</param>
		public byte this[Index index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array[MapToOffset(index)];
		}

		/// <summary>Returns a substring of the current slice that fits within the specified index range</summary>
		/// <param name="range">The <c>begin</c> and <c>end</c> position of the substring.</param>
		/// <returns>Slice that only contain the specified range, but shares the same underlying buffer.</returns>
		public Slice this[Range range]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Substring(range);
		}

		/// <summary>
		/// Returns a reference to the first byte in the slice.
		/// If the slice is empty, returns a reference to the location where the first character would have been stored.
		/// Such a reference can be used for pinning but must never be de-referenced.
		/// </summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref byte DangerousGetPinnableReference()
		{
			//note: this is the equivalent of MemoryMarshal.GetReference(..) and does not check for the 0-length case!
			return ref this.Array[this.Offset];
		}

		/// <summary>
		/// Returns a reference to the 0th element of the Span. If the Span is empty, returns null reference.
		/// It can be used for pinning and is required to support the use of span within a fixed statement.
		/// </summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref byte GetPinnableReference()
		{
			unsafe
			{
				return ref (this.Count != 0) ? ref this.Array[this.Offset] : ref Unsafe.AsRef<byte>(null);
			}
		}

		/// <summary>Copy this slice into another buffer, and move the cursor</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="cursor">Offset into the destination buffer</param>
		public void WriteTo(byte[] buffer, ref int cursor)
		{
			this.Span.CopyTo(buffer.AsSpan(cursor));
			cursor += this.Count;
		}

		/// <summary>Copy this slice into another buffer, and move the cursor</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		public Span<byte> WriteTo(Span<byte> buffer)
		{
			if (buffer.Length == 0) return buffer;
			this.Span.CopyTo(buffer);
			return buffer[this.Count..];
		}

		/// <summary>Copy this slice into another buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyTo(Span<byte> destination)
		{
			this.Span.CopyTo(destination);
		}

		/// <summary>Copy this slice into another buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyTo(Span<byte> destination, out int bytesWritten)
		{
			bytesWritten = 0;
			var span = this.Span;
			span.CopyTo(destination);
			bytesWritten = span.Length;
		}

		/// <summary>Copy this slice into another buffer, if it is large enough.</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCopyTo(Span<byte> destination)
			=> this.Span.TryCopyTo(destination);

		/// <summary>Copy this slice into another buffer, if it is large enough.</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCopyTo(Span<byte> destination, out int bytesWritten)
			=> this.Span.TryCopyTo(destination, out bytesWritten);

		/// <summary>Copy this slice into another buffer</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="offset">Offset into the destination buffer</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyTo(byte[] buffer, int offset)
		{
			this.Span.CopyTo(buffer.AsSpan(offset));
		}

		/// <summary>Copy this slice into another buffer</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="offset">Offset into the destination buffer</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryCopyTo(byte[] buffer, int offset)
		{
			return this.Span.TryCopyTo(buffer.AsSpan(offset));
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position.</summary>
		/// <param name="offset">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>A slice that is equivalent to the substring that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or <see cref="Slice.Empty"/> if <paramref name="offset"/> is equal to the length of the slice.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0) => {"ABC"}
		/// {"ABCDE"}.Substring(1) => {"BCDE"}
		/// {"ABCDE"}.Substring(-2) => {"DE"}
		/// {"ABCDE"}.Substring(5) => Slice.Empty
		/// Slice.Empty.Substring(0) => Slice.Empty
		/// Slice.Nil.Substring(0) => Slice.Empty
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> indicates a position not within this instance, or <paramref name="offset"/> is less than zero</exception>
		[Pure]
		public Slice Substring(int offset)
		{
			int len = this.Count;

			// negative values mean from the end
			if (offset < 0) offset += this.Count;
			//TODO: get rid of negative indexing, and create a different "substring from the end" method?

			// bound check
			if ((uint) offset > (uint) len) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			int r = len - offset;
			return r != 0 ? new Slice(this.Array, this.Offset + offset, r) : Empty;
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length.</summary>
		/// <param name="offset">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="count">Number of bytes in the substring</param>
		/// <returns>A slice that is equivalent to the substring of length <paramref name="count"/> that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if count is zero.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0, 3) => {"ABC"}
		/// {"ABCDE"}.Substring(1, 3) => {"BCD"}
		/// {"ABCDE"}.Substring(-2, 2) => {"DE"}
		/// Slice.Empty.Substring(0, 0) => Slice.Empty
		/// Slice.Nil.Substring(0, 0) => Slice.Empty
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> plus <paramref name="count"/> indicates a position not within this instance, or <paramref name="offset"/> or <paramref name="count"/> is less than zero</exception>
		[Pure]
		public Slice Substring(int offset, int count)
		{
			if (count == 0) return Empty;
			int len = this.Count;

			// bound check
			if ((uint) offset >= (uint) len || (uint) count > (uint)(len - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new Slice(this.Array, this.Offset + offset, count);
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length.</summary>
		/// <param name="range">The range to return</param>
		/// <returns>A slice that is equivalent to the substring that starts from <paramref name="range"/>.Start and ends before <paramref name="range"/>.End in this instance, or Slice.Empty if range is empty.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0, 3) => {"ABC"}
		/// {"ABCDE"}.Substring(1..4) => {"BCD"}
		/// {"ABCDE"}.Substring(^2..) => {"DE"}
		/// Slice.Empty.Substring(0..0) => Slice.Empty
		/// Slice.Nil.Substring(0..0) => Slice.Empty
		/// </example>
		[Pure]
		public Slice Substring(Range range)
		{
			(int offset, int count) = range.GetOffsetAndLength(this.Count);
			if (count == 0) return Empty;
			return new Slice(this.Array, this.Offset + offset, count);
		}

		/// <summary>Truncate the slice if its size exceeds the specified length.</summary>
		/// <param name="maxSize">Maximum size.</param>
		/// <returns>Slice of at most the specified size, or smaller if the original slice does not exceed the size.</returns>
		/// <example><list type="table">
		///   <item><term>Smaller than maxSize is unmodified</term><description><code>{"Hello, World!"}.Truncate(20) => {"Hello, World!"}</code></description></item>
		///   <item><term>Larger than maxSize is truncated</term><description><code>{"Hello, World!"}.Truncate(5) => {"Hello"}</code></description></item>
		///   <item><term>Truncating to 0 returns Empty (or Nil)</term><description><code>{"Hello, World!"}.Truncate(0) == Slice.Empty</code></description></item>
		/// </list></example>
		[Pure]
		public Slice Truncate([Positive] int maxSize)
		{
			//note: the only difference with Substring(0, maxSize) is that we don't throw if the slice is smaller than !
			Contract.Positive(maxSize);

			if (maxSize == 0) return this.IsNull ? Nil : Empty;
			return this.Count <= maxSize ? this : new Slice(this.Array, this.Offset, maxSize);
		}

		/// <summary>Returns a slice array that contains the sub-slices in this instance that are delimited by the specified separator</summary>
		/// <param name="separator">The slice that delimits the sub-slices in this instance.</param>
		/// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array elements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
		/// <returns>An array whose elements contains the sub-slices in this instance that are delimited by the value of <paramref name="separator"/>.</returns>
		[Pure]
		public Slice[] Split(Slice separator, StringSplitOptions options = StringSplitOptions.None)
		{
			return Split(this, separator, options);
		}

		/// <summary>Returns a slice array that contains the sub-slices in instance by cutting fixed-length chunks or size <paramref name="stride"/>.</summary>
		/// <param name="stride">Size of each chunk that will be cut from this instance. Must be greater or equal to 1.</param>
		/// <returns>
		/// An array whose elements contain the sub-slices, each of size <paramref name="stride"/>, except the last slice that may be smaller if the length of this instance is not a multiple of <paramref name="stride"/>.
		/// If this instance is <see cref="Slice.Nil"/> then the array will be empty.
		/// If it is <see cref="Slice.Empty"/> then the array will we of length 1 and contain the empty slice.
		/// </returns>
		/// <remarks>To reduce memory usage, the sub-slices returned in the array will all share the same underlying buffer of the input slice.</remarks>
		[Pure]
		public Slice[] Split(int stride)
		{
			return Split(this, stride);
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this instance.</summary>
		/// <param name="value">The slice to seek</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(Slice value)
		{
			return this.Span.IndexOf(value.Span);
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this instance.</summary>
		/// <param name="value">The slice to seek</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(ReadOnlySpan<byte> value)
		{
			return this.Span.IndexOf(value);
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this instance. The search starts at a specified position.</summary>
		/// <param name="value">The slice to seek</param>
		/// <param name="startIndex">The search starting position</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is startIndex</returns>
		[Pure]
		public int IndexOf(Slice value, int startIndex)
		{
			int idx = this.Span.Slice(startIndex).IndexOf(value.Span);
			return idx >= 0 ? checked(startIndex + idx) : - 1;
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this instance. The search starts at a specified position.</summary>
		/// <param name="value">The slice to seek</param>
		/// <param name="startIndex">The search starting position</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is startIndex</returns>
		[Pure]
		public int IndexOf(ReadOnlySpan<byte> value, int startIndex)
		{
			int idx = this.Span.Slice(startIndex).IndexOf(value);
			return idx >= 0 ? checked(startIndex + idx) : -1;
		}

		/// <summary>Searches for the specified value and returns the index of its first occurrence.</summary>
		/// <param name="value">The byte to search for</param>
		/// <returns>The index of the occurrence of the value in the span. If not found, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(byte value)
		{
			return this.Span.IndexOf(value);
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified byte in this instance. The search starts at a specified position.</summary>
		/// <param name="value">The byte to seek</param>
		/// <param name="startIndex">The search starting position</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that byte is found, or -1 if it is not.</returns>
		[Pure]
		public int IndexOf(byte value, int startIndex)
		{
			int idx = this.Span.Slice(startIndex).IndexOf(value);
			return idx >= 0 ? checked(startIndex + idx) : -1;
		}

		/// <summary>Searches for the first index of any one of the specified values similar to calling IndexOf several times with the logical OR operator.</summary>
		/// <returns>The first index of the occurrence of any one of the values in the span. If not found, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOfAny(byte value0, byte value1)
		{
			return this.Span.IndexOfAny(value0, value1);
		}

		/// <summary>Searches for the first index of any one of the specified values similar to calling IndexOf several times with the logical OR operator.</summary>
		/// <returns>The first index of the occurrence of any one of the values in the span. If not found, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOfAny(byte value0, byte value1, byte value2)
		{
			return this.Span.IndexOfAny(value0, value1, value2);
		}

		/// <summary>Searches for the first index of any one of the specified values similar to calling IndexOf several times with the logical OR operator.</summary>
		/// <returns>The first index of the occurrence of any one of the values in the span. If not found, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOfAny(ReadOnlySpan<byte> values)
		{
			return this.Span.IndexOfAny(values);
		}

#if NET8_0_OR_GREATER

		/// <summary>Searches for the first index of any byte other than the specified <paramref name="values" />.</summary>
		/// <param name="values">The values to avoid.</param>
		/// <returns>The index in the slice of the first occurrence of any byte other than those in <paramref name="values" />. If all the bytes are in <paramref name="values" />, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOfAnyExcept(ReadOnlySpan<byte> values)
		{
			return this.Span.IndexOfAnyExcept(values);
		}

		/// <summary>Searches for the first index of any one of the specified values similar to calling IndexOf several times with the logical OR operator.</summary>
		/// <returns>The first index of the occurrence of any one of the values in the span. If not found, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOfAny(SearchValues<byte> values)
		{
			return this.Span.IndexOfAny(values);
		}

		/// <summary>Searches for the first index of any byte other than the specified <paramref name="values" />.</summary>
		/// <param name="values">The values to avoid.</param>
		/// <returns>The index in the slice of the first occurrence of any byte other than those in <paramref name="values" />. If all the bytes are in <paramref name="values" />, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOfAnyExcept(SearchValues<byte> values)
		{
			return this.Span.IndexOfAnyExcept(values);
		}

		/// <summary>Searches for the first index of any one of the specified values similar to calling IndexOf several times with the logical OR operator.</summary>
		/// <returns>The first index of the occurrence of one any of the values in the span. If not found, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsAny(SearchValues<byte> values)
		{
			return this.Span.ContainsAny(values);
		}

		/// <summary>Searches for the first index of any byte other than the specified <paramref name="values" />.</summary>
		/// <param name="values">The values to avoid.</param>
		/// <returns>The index in the slice of the first occurrence of any byte other than those in <paramref name="values" />. If all the bytes are in <paramref name="values" />, returns -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsAnyExcept(SearchValues<byte> values)
		{
			return this.Span.ContainsAnyExcept(values);
		}

#endif

		/// <summary>Determines whether this slice instance starts with the specified byte.</summary>
		/// <param name="value">The byte to compare.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		public bool StartsWith(byte value)
		{
			var span = this.Span;
			return span.Length > 0 && span[0] == value;
		}

		/// <summary>Determines whether this slice instance starts with the specified byte.</summary>
		/// <param name="value">The value to compare, interpreted as a byte (between 0 and 255).</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		public bool StartsWith(int value)
		{
			var span = this.Span;
			return span.Length > 0 && span[0] == (byte) value;
		}

		/// <summary>Determines whether this slice instance starts with the specified byte.</summary>
		/// <param name="value">The value to compare, interpreted as a byte (between 0 and 255).</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		/// <remarks>
		/// <para>This is a convenience method, where <paramref name="value"/> is expected to be an ASCII character, allowing for easy checks like.</para>
		/// </remarks>
		/// <example><code>
		/// if (data.StartsWith('{') &amp;&amp; data.EndsWith('}')) { /* probably JSON */ }
		/// </code>
		/// </example>
		public bool StartsWith(char value)
		{
			var span = this.Span;
			return span.Length > 0 && span[0] == (byte) value;
		}

		/// <summary>Determines whether the beginning of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare. <see cref="Slice.Nil"/> is not allowed.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		[Pure]
		public bool StartsWith(Slice value)
		{
			if (value.Count == 0)
			{
				return !value.IsNull ? true : throw ThrowHelper.ArgumentNullException(nameof(value));
			}

			//REVIEW: what does Slice.Nil.StartsWith(Empty) means?
			return this.Span.StartsWith(value.Span);
		}

		/// <summary>Determines whether the beginning of this slice instance matches a specified slice.</summary>
		/// <param name="value">The span to compare.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool StartsWith(ReadOnlySpan<byte> value)
		{
			return value.Length == 0 || this.Span.StartsWith(value);
		}

		/// <summary>Determines whether this slice instance ends with the specified byte.</summary>
		/// <param name="value">The byte to compare.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		public bool EndsWith(byte value)
		{
			var span = this.Span;
			return span.Length > 0 && span[^1] == value;
		}

		/// <summary>Determines whether this slice instance ends with the specified byte.</summary>
		/// <param name="value">The value to compare, interpreted as a byte (between 0 and 255).</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		public bool EndsWith(int value)
		{
			var span = this.Span;
			return span.Length > 0 && span[^1] == (byte) value;
		}

		/// <summary>Determines whether this slice instance ends with the specified byte.</summary>
		/// <param name="value">The value to compare, interpreted as a byte (between 0 and 255).</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		/// <remarks>
		/// <para>This is a convenience method, where <paramref name="value"/> is expected to be an ASCII character, allowing for easy checks like.</para>
		/// </remarks>
		/// <example><code>
		/// if (data.StartsWith('{') &amp;&amp; data.EndsWith('}')) { /* probably JSON */ }
		/// </code>
		/// </example>
		public bool EndsWith(char value)
		{
			var span = this.Span;
			return span.Length > 0 && span[^1] == (byte) value;
		}

		/// <summary>Determines whether the end of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare to the substring at the end of this instance.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		[Pure]
		public bool EndsWith(Slice value)
		{
			if (value.Count == 0)
			{
				return !value.IsNull ? true : throw ThrowHelper.ArgumentNullException(nameof(value));
			}

			//REVIEW: what does Slice.Nil.StartsWith(Empty) means?
			return this.Span.EndsWith(value.Span);
		}

		/// <summary>Determines whether the end of this slice instance matches a specified slice.</summary>
		/// <param name="value">The span to compare.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool EndsWith(ReadOnlySpan<byte> value)
		{
			return value.Length == 0 || this.Span.EndsWith(value);
		}

		/// <summary>Equivalent of StartsWith, but the returns false if both slices are identical</summary>
		[Pure]
		public bool PrefixedBy(Slice parent)
		{
			int count = parent.Count;

			// empty is a parent of everyone
			if (count == 0) return true;

			// we must have at least one more byte then the parent
			if (this.Count <= count) return false;

			// must start with the same bytes
			return this.Span.StartsWith(parent.Span);
		}

		/// <summary>Equivalent of EndsWith, but will return false if both slices are identical</summary>
		[Pure]
		public bool SuffixedBy(Slice parent)
		{
			// empty is a parent of everyone
			int count = parent.Count;
			if (count == 0) return true;

			// empty is not a child of anything
			int len = this.Count;
			if (len == 0) return false;

			// we must have at least one more byte than the parent
			if (len <= count) return false;

			// must start with the same bytes
			return this.Span.EndsWith(parent.Span);
		}

		/// <summary>Determines whether the slice is equal to the specified ASCII keyword</summary>
		/// <param name="asciiString">String of ASCII chars. Any character with a code pointer greater or equal to 128 will not work as intended</param>
		/// <returns><b>true</b> if <paramref name="asciiString"/>, when interpreted as bytes, represents the same bytes as this slice; otherwise, <b>false</b></returns>
		/// <remarks>This method is only intended to test the presence of specific keywords or header signatures when parsing protocols, NOT for matching natural text!</remarks>
		public bool Equals(ReadOnlySpan<char> asciiString)
		{
#if NET8_0_OR_GREATER
			return Ascii.Equals(this.Span, asciiString);
#else
			var span = this.Span;
			if (span.Length < asciiString.Length) return false;
			for (int i = 0; i < asciiString.Length; i++)
			{
				Contract.Debug.Assert(asciiString[i] < 128);
				if (span[i] != asciiString[i]) return false;
			}
			return true;
#endif
		}

		/// <summary>Determines whether the slice is equal to the specified ASCII keyword</summary>
		/// <param name="asciiString">String of ASCII chars. Any character with a code pointer greater or equal to 128 will not work as intended</param>
		/// <returns><b>true</b> if <paramref name="asciiString"/>, when interpreted as bytes, represents the same bytes as this slice; otherwise, <b>false</b></returns>
		/// <remarks>This method is only intended to test the presence of specific keywords or header signatures when parsing protocols, NOT for matching natural text!</remarks>
		public bool Equals(ReadOnlyMemory<char> asciiString) => Equals(asciiString.Span);

		/// <summary>Determines whether the beginning of this slice instance matches a specified ASCII keyword</summary>
		/// <param name="asciiString">String of ASCII chars. Any character with a code pointer greater or equal to 128 will not work as intended</param>
		/// <returns><b>true</b> if <paramref name="asciiString"/>, when interpreted as bytes, matches the beginning of this slice; otherwise, <b>false</b></returns>
		/// <remarks>This method is only intended to test the presence of specific keywords or header signatures when parsing protocols, NOT for matching natural text!</remarks>
		public bool StartsWith(ReadOnlySpan<char> asciiString)
		{
			if (asciiString.Length == 0) return true;
			if (this.Count < asciiString.Length) return false;
			return Substring(0, asciiString.Length).Equals(asciiString);
		}

		/// <summary>Determines whether the end of this slice instance matches a specified ASCII keyword</summary>
		/// <param name="asciiString">String of ASCII chars. Any character with a code pointer greater or equal to 128 will not work as intended</param>
		/// <returns><b>true</b> if <paramref name="asciiString"/>, when interpreted as bytes, matches the end of this slice; otherwise, <b>false</b></returns>
		/// <remarks>This method is only intended to test the presence of specific keywords or header signatures when parsing protocols, NOT for matching natural text!</remarks>
		public bool EndsWith(ReadOnlySpan<char> asciiString)
		{
			if (asciiString.Length == 0) return true;
			if (this.Count < asciiString.Length) return false;
			return Substring(this.Count - asciiString.Length).Equals(asciiString);
		}

		/// <summary>Append/Merge a slice at the end of the current slice</summary>
		/// <param name="tail">Slice that must be appended</param>
		/// <returns>Merged slice if both slices are contiguous, or a new slice containing the content of the current slice, followed by the tail slice. Or <see cref="Slice.Empty"/> if both parts are nil or empty</returns>
		[Pure]
		public Slice Concat(Slice tail)
		{
			int count = this.Count;
			if (tail.Count == 0) return count > 0 ? this: Empty;
			if (count == 0) return tail;

			tail.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			// special case: adjacent segments ?
			if (ReferenceEquals(this.Array, tail.Array) && this.Offset + count == tail.Offset)
			{
				return new Slice(this.Array, this.Offset, count + tail.Count);
			}

			byte[] tmp = new byte[count + tail.Count];
			this.Span.CopyTo(tmp);
			tail.Span.CopyTo(tmp.AsSpan(count));
			return new Slice(tmp);
		}

		/// <summary>Append/Merge a slice at the end of the current slice</summary>
		/// <param name="tail">Slice that must be appended</param>
		/// <returns>Merged slice if both slices are contiguous, or a new slice containing the content of the current slice, followed by the tail slice. Or <see cref="Slice.Empty"/> if both parts are nil or empty</returns>
		[Pure]
		public Slice Concat(ReadOnlySpan<byte> tail)
		{
			int count = this.Count;
			if (tail.Length == 0) return count > 0 ? this : Empty;
			if (count == 0) return FromBytes(tail);

			this.EnsureSliceIsValid();

			var tmp = new byte[count + tail.Length];
			this.Span.CopyTo(tmp);
			tail.CopyTo(tmp.AsSpan(count));
			return new Slice(tmp);
		}

		/// <summary>Append an array of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure]
		public Slice[] ConcatRange(Slice[] slices)
		{
			Contract.NotNull(slices);
			EnsureSliceIsValid();

			// pre-allocate by computing final buffer capacity
			var prefixSize = this.Count;
			var capacity = slices.Sum((slice) => prefixSize + slice.Count);
			var writer = new SliceWriter(capacity);
			var next = new List<int>(slices.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(this);
				writer.WriteBytes(slice);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Append an array of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure]
		public Slice[] ConcatRange(ReadOnlySpan<Slice> slices)
		{
			EnsureSliceIsValid();

			// pre-allocate by computing final buffer capacity
			var prefixSize = this.Count;
			long capacity = (long) prefixSize * slices.Length;
			foreach (var slice in slices) capacity += slice.Count;
			var writer = new SliceWriter(checked((int) capacity));

			var next = new List<int>(slices.Length);
			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(this);
				writer.WriteBytes(slice);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Append a sequence of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure]
		public Slice[] ConcatRange(IEnumerable<Slice> slices)
		{
			Contract.NotNull(slices);

			// use optimized version for arrays
			if (slices is Slice[] array) return ConcatRange(array);

			var next = new List<int>();
			var writer = default(SliceWriter);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(this);
				writer.WriteBytes(slice);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Split a buffer containing multiple contiguous segments into an array of segments</summary>
		/// <param name="buffer">Buffer containing all the segments</param>
		/// <param name="start">Offset of the start of the first segment</param>
		/// <param name="endOffsets">Array containing, for each segment, the offset of the following segment</param>
		/// <returns>Array of segments</returns>
		/// <example>SplitIntoSegments("HelloWorld", 0, [5, 10]) => [{"Hello"}, {"World"}]</example>
		public static Slice[] SplitIntoSegments(byte[] buffer, int start, List<int> endOffsets)
		{
			Contract.Debug.Requires(buffer is not null && endOffsets is not null);
			if (endOffsets.Count == 0) return [ ];
			var result = new Slice[endOffsets.Count];
			int i = 0;
			int p = start;
			foreach (var end in endOffsets)
			{
				result[i++] = new(buffer, p, end - p);
				p = end;
			}

			return result;
		}

		/// <summary>Concatenate two slices together</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Concat(Slice a, Slice b)
		{
			return a.Concat(b);
		}

		/// <summary>Concatenate two spans together</summary>
		public static Slice Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
		{
			int count = checked(a.Length + b.Length);
			if (count == 0) return Empty;

			var tmp = new byte[count];

			Span<byte> buf = tmp;
			if (a.Length != 0)
			{
				a.CopyTo(buf);
			}
			if (b.Length != 0)
			{
				b.CopyTo(buf.Slice(a.Length));
			}

			return new(tmp, 0, count);
		}

		/// <summary>Concatenate three slices together</summary>
		public static Slice Concat(Slice a, Slice b, Slice c)
		{
			int count = checked(a.Count + b.Count + c.Count);
			if (count == 0) return Empty;

			var tmp = new byte[count];

			Span<byte> buf = tmp;
			if (a.Count > 0)
			{
				a.Span.CopyTo(buf);
				buf = buf.Slice(a.Count);
			}
			if (b.Count > 0)
			{
				b.Span.CopyTo(buf);
				buf = buf.Slice(b.Count);
			}
			if (c.Count > 0)
			{
				c.Span.CopyTo(buf);
			}
			return new(tmp, 0, count);
		}

		/// <summary>Concatenate three spans together</summary>
		public static Slice Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c)
		{
			int count = checked(a.Length + b.Length + c.Length);
			if (count == 0) return Empty;

			var tmp = new byte[count];

			Span<byte> buf = tmp;
			if (a.Length != 0)
			{
				a.CopyTo(buf);
				buf = buf.Slice(a.Length);
			}
			if (b.Length != 0)
			{
				b.CopyTo(buf);
				buf = buf.Slice(b.Length);
			}
			if (c.Length != 0)
			{
				c.CopyTo(buf);
			}
			return new(tmp, 0, count);
		}

		/// <summary>Concatenate four slices together</summary>
		public static Slice Concat(Slice a, Slice b, Slice c, Slice d)
		{
			int count = checked(a.Count + b.Count + c.Count + d.Count);
			if (count == 0) return Empty;

			var tmp = new byte[count];

			Span<byte> buf = tmp;
			if (a.Count > 0)
			{
				a.Span.CopyTo(buf);
				buf = buf.Slice(a.Count);
			}
			if (b.Count > 0)
			{
				b.Span.CopyTo(buf);
				buf = buf.Slice(b.Count);
			}
			if (c.Count > 0)
			{
				c.Span.CopyTo(buf);
				buf = buf.Slice(c.Count);
			}
			if (d.Count > 0)
			{
				d.Span.CopyTo(buf);
			}
			return new(tmp, 0, count);
		}

		/// <summary>Concatenate four spans together</summary>
		public static Slice Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c, ReadOnlySpan<byte> d)
		{
			int count = checked(a.Length + b.Length + c.Length + d.Length);
			if (count == 0) return Empty;

			var tmp = new byte[count];

			Span<byte> buf = tmp;
			if (a.Length != 0)
			{
				a.CopyTo(buf);
				buf = buf.Slice(a.Length);
			}
			if (b.Length != 0)
			{
				b.CopyTo(buf);
				buf = buf.Slice(b.Length);
			}
			if (c.Length != 0)
			{
				c.CopyTo(buf);
				buf = buf.Slice(c.Length);
			}
			if (d.Length != 0)
			{
				d.CopyTo(buf);
			}
			return new Slice(tmp, 0, count);
		}

		/// <summary>Concatenate an array of slices into a single slice</summary>
		public static Slice Concat(params Slice[] args)
		{
			if (args.Length == 0) return Empty;
			if (args.Length == 1) return args[0];

			long count = 0;
			for (int i = 0; i < args.Length; i++)
			{
				count += args[i].Count;
			}
			if (count == 0) return Empty;

			var tmp = new byte[checked((int) count)];

			Span<byte> buf = tmp;
			foreach(var arg in args)
			{
				if (arg.Count > 0)
				{
					arg.Span.CopyTo(buf);
					buf = buf.Slice(arg.Count);
				}
			}
			Contract.Debug.Assert(buf.Length == 0);
			return new Slice(tmp);
		}

		/// <summary>Concatenate a sequence of slices into a single slice</summary>
#if NET9_0_OR_GREATER
		public static Slice Concat(params ReadOnlySpan<Slice> args)
#else
		public static Slice Concat(ReadOnlySpan<Slice> args)
#endif
		{
			if (args.Length == 0) return Empty;
			if (args.Length == 1) return args[0];
			
			long capacity = 0;
			for (int i = 0; i < args.Length; i++)
			{
				capacity += args[i].Count;
			}
			if (capacity == 0) return Empty;

			var tmp = new byte[checked((int) capacity)];
			Span<byte> buf = tmp;
			foreach (var arg in args)
			{
				if (arg.Count > 0)
				{
					arg.Span.CopyTo(buf);
					buf = buf.Slice(arg.Count);
				}
			}
			Contract.Debug.Assert(buf.Length == 0);
			return new Slice(tmp);
		}

		/// <summary>Concatenate a sequence of slices into a single slice, allocated using a pool</summary>
		/// <param name="pool">Pool used to allocate the buffer for the result</param>
		/// <param name="args">List of spans to concatenate</param>
		/// <returns><see cref="SliceOwner"/> containing all the slices added one after the other</returns>
		/// <remarks>The caller <b>MUST</b> dispose the result; otherwise, the buffer will not be returned to the pool</remarks>
#if NET9_0_OR_GREATER
		public static SliceOwner Concat(ArrayPool<byte> pool, params ReadOnlySpan<Slice> args)
#else
		public static SliceOwner Concat(ArrayPool<byte> pool, ReadOnlySpan<Slice> args)
#endif
		{
			if (args.Length == 0) return SliceOwner.Empty;

			long capacity = ComputeSize(args);
			if (capacity == 0) return SliceOwner.Empty;

			var tmp = pool.Rent(checked((int) capacity));
			Span<byte> buf = tmp;
			foreach (var arg in args)
			{
				if (arg.Count > 0)
				{
					arg.Span.CopyTo(buf);
					buf = buf.Slice(arg.Count);
				}
			}
			Contract.Debug.Assert(buf.Length == 0);
			return SliceOwner.Create(new Slice(tmp), pool);
		}

		/// <summary>Concatenate a sequence of slices into a single slice</summary>
		public static Slice Concat(IEnumerable<Slice> args)
		{
			if (args.TryGetSpan(out var span))
			{
				return Concat(span);
			}

			if (args.TryGetNonEnumeratedCount(out var count) && count == 0)
			{
				return Slice.Empty;
			}

			return ConcatEnumerable(args);

			static Slice ConcatEnumerable(IEnumerable<Slice> args)
			{
				var sw = new SliceWriter();

				// if this is a collection, pre-compute the capacity, to prevent unnecessary resizes
				if (args is ICollection<Slice> coll)
				{
					long capacity = ComputeSize(coll);

					if (capacity == 0) return Empty;
					sw.EnsureBytes(checked((int) capacity));
				}

				foreach (var arg in args)
				{
					if (arg.Count > 0)
					{
						sw.WriteBytes(arg);
					}
				}

				return sw.ToSlice();
			}
		}

		/// <summary>Concatenate a sequence of slices into a single slice, allocated using a pool</summary>
		public static SliceOwner Concat(ArrayPool<byte> pool, IEnumerable<Slice>? args)
		{
			if (args == null) return SliceOwner.Nil;

			if (args.TryGetSpan(out var span))
			{
				return Concat(pool, span);
			}

			var sw = new SliceWriter(pool);
			foreach (var arg in args)
			{
				sw.WriteBytes(arg);
			}
			return sw.ToSliceOwner();
		}

		/// <summary>Adds a prefix to a list of slices</summary>
		/// <param name="prefix">Prefix to add to all the slices</param>
		/// <param name="slices">List of slices to process</param>
		/// <returns>Array of slice that all start with <paramref name="prefix"/> and followed by the corresponding entry in <paramref name="slices"/></returns>
		/// <remarks>This method is optimized to reduce the amount of memory allocated</remarks>
		[Pure]
		public static Slice[] ConcatRange(Slice prefix, IEnumerable<Slice> slices)
		{
			Contract.NotNull(slices);

			if (prefix.IsNullOrEmpty)
			{ // nothing to do, but we still need to copy the array
				return slices.ToArray();
			}

			Slice[] res;

			if (slices is Slice[] arr)
			{	// fast-path for arrays (most frequent with range reads)

				// we wil use a SliceBuffer to store all the keys produced in as few byte[] arrays as needed

				// precompute the exact size needed
				int totalSize = prefix.Count * arr.Length;
				for (int i = 0; i < arr.Length; i++) totalSize += arr[i].Count;
				var buf = new SliceBuffer(Math.Min(totalSize, 64 * 1024));

				res = new Slice[arr.Length];
				for (int i = 0; i < arr.Length; i++)
				{
					res[i] = buf.Intern(prefix, arr[i], aligned: false);
				}
			}
			else if (slices is ICollection<Slice> coll)
			{  // collection (size known)

				//TODO: also use a SliceBuffer since we could precompute the total size...

				res = new Slice[coll.Count];
				int p = 0;
				foreach (var suffix in coll)
				{
					res[p++] = prefix.Concat(suffix);
				}
			}
			else
			{ // streaming sequence (size unknown)

				//note: we can only scan the list once, so would be no way to get a sensible value for the buffer's page size
				var list = new List<Slice>();
				foreach (var suffix in slices)
				{
					list.Add(prefix.Concat(suffix));
				}
				res = list.ToArray();
			}

			return res;
		}

		/// <summary>Computes the sum of the length of a list of slices</summary>
		/// <param name="slices">List of slices to process</param>
		/// <returns>Total size of all the slices</returns>
		/// <remarks>This method can be used to pre-allocate a buffer large enough to fit all the slices</remarks>
		public static long ComputeSize(ReadOnlySpan<Slice> slices)
		{
			long total = 0;
			for (int i = 0; i < slices.Length; i++)
			{
				total += slices[i].Count;
			}
			return total;
		}

		/// <summary>Computes the sum of the length of a list of slices</summary>
		/// <param name="slices">List of slices to process</param>
		/// <returns>Total size of all the slices</returns>
		/// <remarks>This method can be used to pre-allocate a buffer large enough to fit all the slices</remarks>
		public static long ComputeSize(Slice[]? slices)
		{
			if (slices == null) return 0;
			return ComputeSize(new ReadOnlySpan<Slice>(slices));
		}

		/// <summary>Computes the sum of the length of a list of slices</summary>
		/// <param name="slices">List of slices to process</param>
		/// <returns>Total size of all the slices</returns>
		/// <remarks>This method can be used to pre-allocate a buffer large enough to fit all the slices</remarks>
		public static long ComputeSize(IEnumerable<Slice>? slices)
		{
			if (slices == null) return 0;

			if (slices.TryGetSpan(out var span))
			{
				return ComputeSize(span);
			}

			return ComputeSizeEnumerable(slices);

			static long ComputeSizeEnumerable(IEnumerable<Slice> slices)
			{
				long total = 0;
				foreach (var slice in slices)
				{
					total += slice.Count;
				}
				return total;
			}
		}

		/// <summary>Computes the sum of the length of the keys and values</summary>
		/// <param name="slices">List of pairs of slices to process</param>
		/// <returns>Total size of all the keys and values</returns>
		/// <remarks>This method can be used to pre-allocate a buffer large enough to fit all the slices</remarks>
		public static long ComputeSize(ReadOnlySpan<KeyValuePair<Slice, Slice>> slices)
		{
			long total = 0;
			for (int i = 0; i < slices.Length; i++)
			{
				total += slices[i].Key.Count;
				total += slices[i].Value.Count;
			}
			return total;
		}

		/// <summary>Computes the sum of the length of the keys and values</summary>
		/// <param name="slices">List of pairs of slices to process</param>
		/// <returns>Total size of all the keys and values</returns>
		/// <remarks>This method can be used to pre-allocate a buffer large enough to fit all the slices</remarks>
		public static long ComputeSize(KeyValuePair<Slice, Slice>[]? slices)
		{
			if (slices == null) return 0;
			return ComputeSize(new ReadOnlySpan<KeyValuePair<Slice, Slice>>(slices));
		}

		/// <summary>Computes the sum of the length of the keys and values</summary>
		/// <param name="slices">List of pairs of slices to process</param>
		/// <returns>Total size of all the keys and values</returns>
		/// <remarks>This method can be used to pre-allocate a buffer large enough to fit all the slices</remarks>
		public static long ComputeSize(IEnumerable<KeyValuePair<Slice, Slice>>? slices)
		{
			if (slices == null) return 0;

			if (slices.TryGetSpan(out var span))
			{
				return ComputeSize(span);
			}

			return ComputeSizeEnumerable(slices);

			static long ComputeSizeEnumerable(IEnumerable<KeyValuePair<Slice, Slice>> slices)
			{
				long total = 0;
				foreach (var kv in slices)
				{
					total += kv.Key.Count;
					total += kv.Value.Count;
				}
				return total;
			}
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this source.</summary>
		/// <param name="source">The slice Input slice</param>
		/// <param name="value">The slice to seek</param>
		/// <returns>Offset of the match if positive, or no occurrence was found if negative</returns>
		[Pure]
		public static int Find(Slice source, Slice value)
		{
			return source.Span.IndexOf(value.Span);
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified byte in this source.</summary>
		/// <param name="source">The slice Input slice</param>
		/// <param name="value">The byte to find</param>
		/// <returns>Offset of the match if positive, or the byte was not found if negative</returns>
		[Pure]
		public static int Find(Slice source, byte value)
		{
			return source.Span.IndexOf(value);
		}

		/// <summary>Concatenates all the elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <returns>A slice that consists of the elements in a value delimited by the <paramref name="separator"/> slice. If <paramref name="values"/> is an empty array, the method returns <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, Slice[] values)
		{
			Contract.NotNull(values);

			int count = values.Length;
			return count switch
			{
				0 => Empty,
				1 => values[0],
				_ => Join(separator, values, 0, count)
			};
		}

		/// <summary>Concatenates all the elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <returns>A slice that consists of the elements in a value delimited by the <paramref name="separator"/> slice. If <paramref name="values"/> is an empty array, the method returns <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, ReadOnlySpan<Slice> values)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Nil and Empty are equivalent (either for separator, or for the elements of the array)

			if (values.Length == 0) return Empty;
			if (values.Length == 1) return values[0];

			long capacity = 0;
			for (int i = 0; i < values.Length; i++) capacity += values[i].Count;
			capacity += (long) (values.Length - 1) * separator.Count;

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (capacity > int.MaxValue) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[(int) capacity];
			Span<byte> buf = tmp;
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0) buf = separator.WriteTo(buf);
				buf = values[i].WriteTo(buf);
			}
			Contract.Debug.Ensures(buf.Length == 0);
			return new Slice(tmp);
		}

		/// <summary>Concatenates the specified elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <param name="startIndex">The first element in <paramref name="values"/> to use.</param>
		/// <param name="count">The number of elements of <paramref name="values"/> to use.</param>
		/// <returns>A slice that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- <see cref="Empty"/> if <paramref name="count"/> is zero, <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="startIndex"/> or <paramref name="count"/> is less than zero. -or- <paramref name="startIndex"/> plus <paramref name="count"/> is greater than the number of elements in <paramref name="values"/>.</exception>
		public static Slice Join(Slice separator, Slice[] values, int startIndex, int count)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Nil and Empty are equivalent (either for separator, or for the elements of the array)

			Contract.NotNull(values);

			if (startIndex < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must be a positive integer");
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count must be a positive integer");
			if (startIndex > values.Length - count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must fit within the array");

			if (count == 0) return Empty;
			if (count == 1) return values[startIndex];

			int size = 0;
			for (int i = 0; i < values.Length; i++) size += values[i].Count;
			size += (values.Length - 1) * separator.Count;

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (size < 0) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[size];
			var writer = new SliceWriter(tmp);
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0) writer.WriteBytes(separator);
				writer.WriteBytes(values[i]);
			}
			Contract.Debug.Ensures(writer.Buffer?.Length == size);
			return writer.ToSlice();
		}

		/// <summary>Concatenates the specified elements of a slice sequence, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">A sequence will return the elements to concatenate.</param>
		/// <returns>A slice that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- <see cref="Slice.Empty"/> if <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, IEnumerable<Slice> values)
		{
			Contract.NotNull(values);
			var array = (values as Slice[]) ?? values.ToArray();
			return Join(separator, array, 0, array.Length);
		}

		/// <summary>Concatenates the specified elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <param name="startIndex">The first element in <paramref name="values"/> to use.</param>
		/// <param name="count">The number of elements of <paramref name="values"/> to use.</param>
		/// <returns>A byte array that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- an empty array if <paramref name="count"/> is zero, <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="startIndex"/> or <paramref name="count"/> is less than zero. -or- <paramref name="startIndex"/> plus <paramref name="count"/> is greater than the number of elements in <paramref name="values"/>.</exception>
		public static byte[] JoinBytes(Slice separator, Slice[] values, int startIndex, int count)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Nil and Empty are equivalent (either for separator, or for the elements of the array)

			Contract.NotNull(values);
			if (startIndex < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must be a positive integer");
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count must be a positive integer");
			if (startIndex > values.Length - count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must fit within the array");

			if (count == 0) return [ ];
			if (count == 1) return values[startIndex].GetBytes() ?? [ ];

			int size = 0;
			for (int i = 0; i < count; i++)
			{
				size = checked(size + values[startIndex + i].Count);
			}
			size = checked(size + (count - 1) * separator.Count);

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (size < 0) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[size];
			Span<byte> buf = tmp;
			for (int i = 0; i < count; i++)
			{
				if (i > 0) buf = separator.WriteTo(buf);
				buf = values[startIndex + i].WriteTo(buf);
			}
			Contract.Debug.Ensures(buf.Length == 0);
			return tmp;
		}

		/// <summary>Concatenates the specified elements of a slice sequence, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">A sequence will return the elements to concatenate.</param>
		/// <returns>A byte array that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- an empty array if <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static byte[] JoinBytes(Slice separator, IEnumerable<Slice> values)
		{
			Contract.NotNull(values);
			var array = (values as Slice[]) ?? values.ToArray();
			return JoinBytes(separator, array, 0, array.Length);
		}

		/// <summary>Returns a slice array that contains the sub-slices in <paramref name="input"/> that are delimited by <paramref name="separator"/>. A parameter specifies whether to return empty array elements.</summary>
		/// <param name="input">Input slice that must be split into sub-slices</param>
		/// <param name="separator">Separator that delimits the sub-slices in <paramref name="input"/>. Cannot be empty or nil</param>
		/// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array elements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
		/// <returns>An array whose elements contain the sub-slices that are delimited by <paramref name="separator"/>.</returns>
		/// <exception cref="System.ArgumentException">If <paramref name="separator"/> is empty, or if <paramref name="options"/> is not one of the <see cref="StringSplitOptions"/> values.</exception>
		/// <remarks>If <paramref name="input"/> does not contain the delimiter, the returned array consists of a single element that repeats the input, or an empty array if input is itself empty.
		/// To reduce memory usage, the sub-slices returned in the array will all share the same underlying buffer of the input slice.</remarks>
		public static Slice[] Split(Slice input, Slice separator, StringSplitOptions options = StringSplitOptions.None)
		{
			// this method is made to behave the same way as String.Split(), especially the following edge cases
			// - Empty.Split(..., StringSplitOptions.None) => { Empty }
			// - Empty.Split(..., StringSplitOptions.RemoveEmptyEntries) => { }
			// differences:
			// - If input is Nil, it is considered equivalent to Empty
			// - If separator is Nil or Empty, the method throws

			var list = new List<Slice>();

			if (separator.Count <= 0) throw ThrowHelper.ArgumentException(nameof(separator), "Separator must have at least one byte");
			if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries) throw ThrowHelper.ArgumentException(nameof(options));

			bool skipEmpty = options.HasFlag(StringSplitOptions.RemoveEmptyEntries);
			if (input.Count == 0)
			{
				return skipEmpty ? [ ] : [ Empty ];
			}

			while (input.Count > 0)
			{
				int p = Find(input, separator);
				if (p < 0)
				{ // last chunk
					break;
				}
				if (p == 0)
				{ // empty chunk
					if (!skipEmpty) list.Add(Empty);
				}
				else
				{
					list.Add(input.Substring(0, p));
				}
				// note: we checked earlier that separator.Count > 0, so we are guaranteed to advance the cursor
				input = input.Substring(p + separator.Count);
			}

			if (input.Count > 0 || !skipEmpty)
			{
				list.Add(input);
			}

			return list.ToArray();
		}

		/// <summary>Returns a slice array that contains the sub-slices in <paramref name="input"/> by cutting fixed-length chunks or size <paramref name="stride"/>.</summary>
		/// <param name="input">Input slice that must be split into sub-slices</param>
		/// <param name="stride">Size of each chunk that will be cut from <paramref name="input"/>. Must be greater or equal to 1.</param>
		/// <returns>
		/// An array whose elements contain the sub-slices, each of size <paramref name="stride"/>, except the last slice that may be smaller if the length of <paramref name="input"/> is not a multiple of <paramref name="stride"/>.
		/// If <paramref name="input"/> is <see cref="Slice.Nil"/> then the array will be empty.
		/// If it is <see cref="Slice.Empty"/> then the array will we of length 1 and contain the empty slice.
		/// </returns>
		/// <remarks>To reduce memory usage, the sub-slices returned in the array will all share the same underlying buffer of the input slice.</remarks>
		public static Slice[] Split(Slice input, int stride)
		{
			Contract.GreaterOrEqual(stride, 1);

			if (input.IsNull) return [ ];

			if (input.Count <= stride)
			{ // single element
				return [ input ];
			}

			// how many slices? (last one may be incomplete)
			int count = (input.Count + (stride - 1)) / stride;
			var result = new Slice[count];

			int p = 0;
			int r = input.Count;
			for(int i = 0; i < result.Length; i++)
			{
				Contract.Debug.Assert(r >= 0);
				result[i] = new Slice(input.Array, input.Offset + p, Math.Min(r, stride));
				p += stride;
				r -= stride;
			}

			return result;
		}

		/// <summary>Returns the first key lexicographically that does not have the passed in <paramref name="slice"/> as a prefix</summary>
		/// <param name="slice">Slice to increment</param>
		/// <returns>New slice that is guaranteed to be the first key lexicographically higher than <paramref name="slice"/> which does not have <paramref name="slice"/> as a prefix</returns>
		/// <remarks>If the last byte is already equal to 0xFF, it will rollover to 0x00 and the next byte will be incremented.</remarks>
		/// <exception cref="ArgumentException">If the Slice is equal to Slice.Nil</exception>
		/// <exception cref="OverflowException">If the Slice is the empty string or consists only of 0xFF bytes</exception>
		/// <example>
		/// Slice.Increment(Slice.FromString("ABC")) => "ABD"
		/// Slice.Increment(Slice.FromHexa("01 FF")) => { 02 }
		/// </example>
		public static Slice Increment(Slice slice)
		{
			if (slice.IsNull) throw ThrowHelper.ArgumentException(nameof(slice), "Cannot increment null buffer");

			int lastNonFfByte;
			var tmp = slice.ToArray();
			for (lastNonFfByte = tmp.Length - 1; lastNonFfByte >= 0; --lastNonFfByte)
			{
				if (tmp[lastNonFfByte] != 0xFF)
				{
					++tmp[lastNonFfByte];
					break;
				}
			}

			if (lastNonFfByte < 0)
			{
				throw ThrowHelper.ArgumentException(nameof(slice), "Cannot increment key");
			}

			return new Slice(tmp, 0, lastNonFfByte + 1);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, Slice[] keys)
		{
			Contract.NotNull(keys);

			//REVIEW: merge this code with Slice.ConcatRange!

			if (keys.Length == 0) return [ ];

			// we can pre-allocate exactly the buffer by computing the total size of all keys
			int size = keys.Length * prefix.Count;
			for (int i = 0; i < keys.Length; i++) size += keys[i].Count;

			var writer = new SliceWriter(size);
			var next = new List<int>(keys.Length);

			//TODO: use multiple buffers if item count is huge ?
			bool hasPrefix = prefix.Count != 0;
			foreach (var key in keys)
			{
				if (hasPrefix) writer.WriteBytes(prefix);
				writer.WriteBytes(key);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, ReadOnlySpan<Slice> keys)
		{
			//REVIEW: merge this code with Slice.ConcatRange!

			if (keys.Length == 0) return [ ];

			// we can pre-allocate exactly the buffer by computing the total size of all keys
			int pc = prefix.Count;
			long capacity = (long) keys.Length * pc;
			for (int i = 0; i < keys.Length; i++) capacity += keys[i].Count;

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (capacity > int.MaxValue) throw new OutOfMemoryException();

			var tmp = new byte[(int) capacity];
			var segs = new Slice[keys.Length];

			//TODO: use multiple buffers if item count is huge ?
			Span<byte> buf = tmp;
			int pos = 0;
			for(int i = 0; i < keys.Length; i++)
			{
				if (pc != 0) buf = prefix.WriteTo(buf);
				buf = keys[i].WriteTo(buf);
				int sz = keys[i].Count + pc;
				segs[i++] = new Slice(tmp, pos, sz);
				pos += sz;
			}
			return segs;
		}

		/// <summary>Merge a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, IEnumerable<Slice> keys)
		{
			Contract.NotNull(keys);

			//REVIEW: merge this code with Slice.ConcatRange!

			// use optimized version for arrays
			if (keys is Slice[] array) return Merge(prefix, array);

			// pre-allocate with a count if we can get one...
			var next = keys is ICollection<Slice> coll ? new List<int>(coll.Count) : new List<int>();
			var writer = default(SliceWriter);

			//TODO: use multiple buffers if item count is huge ?

			bool hasPrefix = prefix.Count != 0;
			foreach (var key in keys)
			{
				if (hasPrefix) writer.WriteBytes(prefix);
				writer.WriteBytes(key);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Creates a new slice that contains the same byte repeated</summary>
		/// <param name="value">Byte that will fill the slice</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>New slice that contains <paramref name="count"/> times the byte <paramref name="value"/>.</returns>
		public static Slice Repeat(byte value, int count)
		{
			Contract.Positive(count);
			if (count == 0) return Empty;

			var res = new byte[count];
			res.AsSpan().Fill(value);
			return new Slice(res);
		}

		/// <summary>Creates a new slice that contains the same byte repeated</summary>
		/// <param name="value">ASCII character (between 0 and 255) that will fill the slice. If <paramref name="value"/> is greater than 0xFF, only the 8 lowest bits will be used</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>New slice that contains <paramref name="count"/> times the byte <paramref name="value"/>.</returns>
		public static Slice Repeat(char value, int count)
		{
			Contract.Positive(count);
			if (count == 0) return Empty;

			var res = new byte[count];
			res.AsSpan().Fill((byte) value);
			return new Slice(res);
		}

		/// <summary>Create a new slice filled with random bytes taken from a random number generator</summary>
		/// <param name="prng">Pseudo random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="prng"/></returns>
		/// <remarks>Warning: <see cref="System.Random"/> is not thread-safe ! If the <paramref name="prng"/> instance is shared between threads, then it needs to be locked before calling this method.</remarks>
		public static Slice Random(Random prng, int count)
		{
			Contract.NotNull(prng);
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative");
			if (count == 0) return Empty;

			var bytes = new byte[count];
			prng.NextBytes(bytes);
			return new Slice(bytes, 0, count);
		}

		/// <summary>Create a new slice filled with random bytes taken from a cryptographic random number generator</summary>
		/// <param name="rng">Random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <param name="nonZeroBytes">If true, produce a sequence of non-zero bytes.</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="rng"/></returns>
		/// <remarks>Warning: All RNG implementations may not be thread-safe ! If the <paramref name="rng"/> instance is shared between threads, then it may need to be locked before calling this method.</remarks>
		public static Slice Random(RandomNumberGenerator rng, int count, bool nonZeroBytes = false)
		{
			Contract.NotNull(rng);
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative");
			if (count == 0) return Empty;

			var bytes = new byte[count];

			if (nonZeroBytes)
				rng.GetNonZeroBytes(bytes);
			else
				rng.GetBytes(bytes);

			return new Slice(bytes, 0, count);
		}

		/// <summary>Returns the lowest of two keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <returns>The key that is BEFORE the other, using lexicographical order</returns>
		/// <remarks>If both keys are equal, then <paramref name="a"/> is returned</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Min(Slice a, Slice b)
		{
			return a.CompareTo(b) <= 0 ? a : b;
		}

		/// <summary>Returns the lowest of three keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <param name="c">Second key</param>
		/// <returns>The key that is BEFORE the other two, using lexicographical order</returns>
		public static Slice Min(Slice a, Slice b, Slice c)
		{
			return a.CompareTo(b) <= 0
				? (a.CompareTo(c) <= 0 ? a : c)
				: (b.CompareTo(c) <= 0 ? b : c);
		}

		/// <summary>Returns the lowest element in an array of keys</summary>
		public static Slice Min(params Slice[] values)
		{
			switch (values.Length)
			{
				case 0: return default;
				case 1: return values[0];
				case 2: return Min(values[0], values[1]);
				case 3: return Min(values[0], values[1], values[3]);
				default:
				{
					var min = values[0];
					for (int i = 1; i < values.Length; i++)
					{
						if (values[i].CompareTo(min) < 0) min = values[i];
					}
					return min;
				}
			}
		}

		/// <summary>Returns the lowest element in a span of keys</summary>
#if NET9_0_OR_GREATER
		public static Slice Min(params ReadOnlySpan<Slice> values)
#else
		public static Slice Min(ReadOnlySpan<Slice> values)
#endif
		{
			switch (values.Length)
			{
				case 0: return default;
				case 1: return values[0];
				case 2: return Min(values[0], values[1]);
				case 3: return Min(values[0], values[1], values[3]);
				default:
				{
					var min = values[0];
					for (int i = 1; i < values.Length; i++)
					{
						if (values[i].CompareTo(min) < 0) min = values[i];
					}
					return min;
				}
			}
		}

		/// <summary>Returns the highest of two keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <returns>The key that is AFTER the other, using lexicographical order</returns>
		/// <remarks>If both keys are equal, then <paramref name="a"/> is returned</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Max(Slice a, Slice b)
		{
			return a.CompareTo(b) >= 0 ? a : b;
		}

		/// <summary>Returns the highest of three keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <param name="c">Second key</param>
		/// <returns>The key that is AFTER the other two, using lexicographical order</returns>
		public static Slice Max(Slice a, Slice b, Slice c)
		{
			return a.CompareTo(b) >= 0
				? (a.CompareTo(c) >= 0 ? a : c)
				: (b.CompareTo(c) >= 0 ? b : c);
		}

		/// <summary>Returns the highest element in an array of keys</summary>
		public static Slice Max(params Slice[] values)
		{
			switch (values.Length)
			{
				case 0: return default;
				case 1: return values[0];
				case 2: return Max(values[0], values[1]);
				case 3: return Max(values[0], values[1], values[3]);
				default:
				{
					var max = values[0];
					for (int i = 1; i < values.Length; i++)
					{
						if (values[i].CompareTo(max) > 0) max = values[i];
					}
					return max;
				}
			}
		}

		/// <summary>Returns the highest element in a span of keys</summary>
#if NET9_0_OR_GREATER
		public static Slice Max(params ReadOnlySpan<Slice> values)
#else
		public static Slice Max(ReadOnlySpan<Slice> values)
#endif
		{
			switch (values.Length)
			{
				case 0: return default;
				case 1: return values[0];
				case 2: return Max(values[0], values[1]);
				case 3: return Max(values[0], values[1], values[3]);
				default:
				{
					var max = values[0];
					for (int i = 1; i < values.Length; i++)
					{
						if (values[i].CompareTo(max) > 0) max = values[i];
					}
					return max;
				}
			}
		}

		#region Slice arithmetics...

		/// <summary>Compare two slices for equality</summary>
		/// <returns>True if the slices contains the same bytes</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Slice a, Slice b) => a.Equals(b);

		/// <summary>Compare two slices for inequality</summary>
		/// <returns>True if the slices do not contain the same bytes</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Slice a, Slice b) => !a.Equals(b);

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(Slice a, Slice b) => a.CompareTo(b) < 0;

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than or equal to <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(Slice a, Slice b) => a.CompareTo(b) <= 0;

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(Slice a, Slice b) => a.CompareTo(b) > 0;

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than or equal to <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(Slice a, Slice b) => a.CompareTo(b) >= 0;

		/// <summary>Append/Merge two slices together</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Second slice</param>
		/// <returns>Merged slices if both slices are contiguous, or a new slice containing the content of the first slice, followed by the second</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice operator +(Slice a, Slice b) => a.Concat(b);

		/// <summary>Appends a byte at the end of the slice</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Byte to append at the end</param>
		/// <returns>New slice with the byte appended</returns>
		public static Slice operator +(Slice a, byte b)
		{
			if (a.Count == 0) return FromByte(b);
			var tmp = new byte[a.Count + 1];
			a.Span.CopyTo(tmp);
			tmp[a.Count] = b;
			return new Slice(tmp);
		}

		/// <summary>Remove <paramref name="n"/> bytes at the end of slice <paramref name="s"/></summary>
		/// <returns>Smaller slice</returns>
		public static Slice operator -(Slice s, int n)
		{
			if (n < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(n), n, "Cannot subtract a negative number from a slice");
			if (n > s.Count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(n), n, "Cannot subtract more bytes than the slice contains");

			if (n == 0) return s;
			if (n == s.Count) return Empty;

			return new Slice(s.Array, s.Offset, s.Count - n);
		}

		// note: We also need overloads with Nullable<Slice>'s to be able to do things like "if (slice == null)", "if (slice != null)" or "if (null != slice)".
		// For structs that have "==" / "!=" operators, the compiler will think that when you write "slice == null", you really mean "(Slice?)slice == default(Slice?)", and that would ALWAYS false if you don't have specialized overloads to intercept.

		/// <summary>Determines whether two specified instances of <see cref="Slice"/> are equal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `value.IsNil` or `value == Slice.Nil` instead.")]
		public static bool operator ==(Slice? a, Slice? b) => a.GetValueOrDefault().Equals(b.GetValueOrDefault());

		/// <summary>Determines whether two specified instances of <see cref="Slice"/> are not equal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `!value.IsNil` or `value != Slice.Nil` instead.")]
		public static bool operator !=(Slice? a, Slice? b) => !a.GetValueOrDefault().Equals(b.GetValueOrDefault());

		/// <summary>Determines whether one specified <see cref="Slice"/> is less than another specified <see cref="Slice"/>.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `value < Slice.Nil` instead.")]
		public static bool operator <(Slice? a, Slice? b) => a.GetValueOrDefault() < b.GetValueOrDefault();

		/// <summary>Determines whether one specified <see cref="Slice"/> is less than or equal to another specified <see cref="Slice"/>.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `value <= Slice.Nil` instead.")]
		public static bool operator <=(Slice? a, Slice? b) => a.GetValueOrDefault() <= b.GetValueOrDefault();

		/// <summary>Determines whether one specified <see cref="Slice"/> is greater than another specified <see cref="Slice"/>.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `value > Slice.Nil` instead.")]
		public static bool operator >(Slice? a, Slice? b) => a.GetValueOrDefault() > b.GetValueOrDefault();

		/// <summary>Determines whether one specified <see cref="Slice"/> is greater than or equal to another specified <see cref="Slice"/>.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `value >= Slice.Nil` instead.")]
		public static bool operator >=(Slice? a, Slice? b) => a.GetValueOrDefault() >= b.GetValueOrDefault();

		/// <summary>Concatenates two <see cref="Slice"/> together.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("This is dangerous! Please use `value + Slice.Nil` instead.")]
		public static Slice operator +(Slice? a, Slice? b)
		{
			// note: makes "slice + null" work!
			return a.GetValueOrDefault().Concat(b.GetValueOrDefault());
		}

		/// <summary>Compare two slices for equality</summary>
		/// <returns>True if the slices contains the same bytes</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(-1)]
		public static bool operator ==(Slice a, ReadOnlySpan<byte> b) => a.Equals(b);
		
		/// <summary>Compare two slices for inequality</summary>
		/// <returns>True if the slices do not contain the same bytes</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(-1)]
		public static bool operator !=(Slice a, ReadOnlySpan<byte> b) => !a.Equals(b);

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(-1)]
		public static bool operator <(Slice a, ReadOnlySpan<byte> b) => a.CompareTo(b) < 0;

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than or equal to <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(-1)]
		public static bool operator <=(Slice a, ReadOnlySpan<byte> b) => a.CompareTo(b) <= 0;

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(-1)]
		public static bool operator >(Slice a, ReadOnlySpan<byte> b) => a.CompareTo(b) > 0;

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than or equal to <paramref name="a"/>; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(-1)]
		public static bool operator >=(Slice a, ReadOnlySpan<byte> b) => a.CompareTo(b) >= 0;

		#endregion

		#region ISliceSerializable...

		/// <inheritdoc />
		public void WriteTo(ref SliceWriter writer)
		{
			writer.WriteBytes(this.Span);
		}

		#endregion

		#region ISpanEncodable...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = new(this.Array, this.Offset, this.Count);
			return true;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Count;
			return true;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (!new ReadOnlySpan<byte>(this.Array, this.Offset, this.Count).TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = this.Count;
			return true;
		}

		#endregion

		#region ISpanFormattable

		/// <summary>Tries to format the value of the current instance into the provided span of characters.</summary>
		/// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
		/// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="destination" />.</param>
		/// <param name="provider">An optional object that supplies culture-specific formatting information for <paramref name="destination" />.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			//PERF: make this method really optimized and without any allocations!
			charsWritten = 0;

			switch (format)
			{
				case "" or "D" or "d": return Dump(destination, out charsWritten, this.Span);
				case "R" or "r": return Dump(destination, out charsWritten, this.Span, int.MaxValue);
			}

			string s = format switch
			{
				"N" => ToHexString(),
				"n" => ToHexStringLower(),
				"X" => ToHexString(' '),
				"x" => ToHexStringLower(' '),
				"P" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: null, lower: false),
				"p" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: null, lower: true),
				"K" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: true, lower: false),
				"k" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: true, lower: true),
				"V" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: false, lower: false),
				"v" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: false, lower: true),
				_ => throw new FormatException("Format is invalid or not supported")
			};

			if (s.Length > destination.Length)
			{
				return false;
			}

			s.CopyTo(destination);
			charsWritten = s.Length;
			return true;
		}

		#endregion

		/// <summary>Returns a printable representation of the key</summary>
		/// <remarks>You can roundtrip the result of calling slice.ToString() by passing it to <see cref="Unescape(string?)"/>(string) and get back the original slice.</remarks>
		public override string ToString()
		{
			return Dump(this);
		}

		/// <inheritdoc cref="Slice.ToString()" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Formats the slice using the specified encoding</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Slice. The <paramref name="format"/> parameter can be "N", "D", "X", or "P". If format is null or an empty string (""), "D" is used. A lower case character will usually produce lowercased hexadecimal letters.</param>
		/// <param name="provider">This parameter is not used</param>
		/// <returns></returns>
		/// <remarks>
		/// The format <b>D</b> is the default, and produce a roundtrip-able version of the slice, using &lt;XX&gt; tokens for non-printable bytes.
		/// The format <b>N</b> (or <b>n</b>) produces a compact hexadecimal string (without separators).
		/// The format <b>X</b> (or <b>x</b>) produces a hexadecimal string with spaces between each byte.
		/// The format <b>P</b> is the equivalent of calling <see cref="PrettyPrint()"/>.
		/// </remarks>
		public string ToString(string? format, IFormatProvider? provider)
		{
			return (format ?? "D") switch
			{
				"D" or "d" => Dump(this),
				"N" => ToHexString(),
				"n" => ToHexStringLower(),
				"X" => ToHexString(' '),
				"x" => ToHexStringLower(' '),
				"P" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: null, lower: false),
				"p" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: null, lower: true),
				"K" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: true, lower: false),
				"k" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: true, lower: true),
				"V" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: false, lower: false),
				"v" => PrettyPrint(this.Span, Slice.DefaultPrettyPrintSize, biasKey: false, lower: true),
				"R" or "r" => Dump(this, int.MaxValue),
				_ => throw new FormatException("Format is invalid or not supported")
			};
		}

		/// <summary>Returns a printable representation of a key</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		public static string Dump(Slice value, int maxSize = DefaultPrettyPrintSize) //REVIEW: rename this to Encode(..) or Escape(..)
		{
			if (value.Count == 0) return value.IsNull ? "<null>" : "<empty>";

			value.EnsureSliceIsValid();

			var buffer = value.Array;
			int count = Math.Min(value.Count, maxSize);
			int pos = value.Offset;

			var sb = new StringBuilder(count + 16);
			while (count-- > 0)
			{
				int c = buffer[pos++];
				if (c < 32 || c >= 127 || c == 60)
				{
					sb.Append('<');
					int x = c >> 4;
					sb.Append((char)(x + (x < 10 ? 48 : 55)));
					x = c & 0xF;
					sb.Append((char)(x + (x < 10 ? 48 : 55)));
					sb.Append('>');
				}
				else
				{
					sb.Append((char)c);
				}
			}
			if (value.Count > maxSize) sb.Append("[\u2026]"); // Unicode for '...'
			return sb.ToString();
		}

		/// <summary>Returns a printable representation of a key</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		public static string Dump(ReadOnlySpan<byte> value, int maxSize = DefaultPrettyPrintSize) //REVIEW: rename this to Encode(..) or Escape(..)
		{
			if (value.Length == 0) return "<empty>";

			bool truncated = value.Length > maxSize;
			if (truncated)
			{
				value = value[..maxSize];
			}

			var sb = new StringBuilder(value.Length + 16);
			foreach(var c in value)
			{
				if (c < 32 || c >= 127 || c == 60)
				{
					sb.Append('<');
					int x = c >> 4;
					sb.Append((char) (x + (x < 10 ? 48 : 55)));
					x = c & 0xF;
					sb.Append((char) (x + (x < 10 ? 48 : 55)));
					sb.Append('>');
				}
				else
				{
					sb.Append((char) c);
				}
			}
			if (truncated) sb.Append("[\u2026]"); // Unicode for '...'
			return sb.ToString();
		}

		/// <summary>Returns a printable representation of a key</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		private static string DumpLower(ReadOnlySpan<byte> value, int maxSize = DefaultPrettyPrintSize)
		{
			//note: same as Dump() but with lowercase hexadecimal!
			if (value.Length == 0) return "<empty>";

			bool truncated = value.Length > maxSize;
			if (truncated)
			{
				value = value[..maxSize];
			}

			var sb = new StringBuilder(value.Length + 16);
			foreach(var c in value)
			{
				if (c < 32 || c >= 127 || c == 60)
				{
					sb.Append('<');
					int x = c >> 4;
					sb.Append((char) (x + (x < 10 ? 48 : 67)));
					x = c & 0xF;
					sb.Append((char) (x + (x < 10 ? 48 : 67)));
					sb.Append('>');
				}
				else
				{
					sb.Append((char) c);
				}
			}
			if (truncated) sb.Append("[\u2026]"); // Unicode for '...'
			return sb.ToString();
		}

		public static bool Dump(Span<char> destination, out int charsWritten, ReadOnlySpan<byte> value, int maxSize = Slice.DefaultPrettyPrintSize)
		{
			if (value.Length == 0) return "<empty>".TryCopyTo(destination, out charsWritten);

			bool truncated = value.Length > maxSize;
			if (truncated)
			{
				value = value[..maxSize];
			}

			var tail = destination;
			// in best case, all bytes encode to a single char
			if (tail.Length < value.Length) goto too_small;

			foreach(var c in value)
			{
				if (c is < 32 or >= 127 or 60)
				{
					if (tail.Length < 4) goto too_small;
					int x = c >> 4;
					int y = c & 0xF;
					tail[0] = '<';
					tail[1] = (char) (x + (x < 10 ? 48 : 55));
					tail[2] = (char) (y + (y < 10 ? 48 : 55));
					tail[3] = '>';
					tail = tail[4..];
				}
				else
				{
					if (tail.Length == 0) goto too_small;
					tail[0] = (char) c;
					tail = tail[1..];
				}
			}
			if (truncated)
			{
				if (!"[\u2026]".TryCopyTo(tail)) goto too_small; // Unicode for '...'
				tail = tail[3..];
			}

			charsWritten = destination.Length - tail.Length;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		#region Streams...

		/// <summary>Read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <returns>Slice containing the stream content (or <see cref="Slice.Nil"/> if the stream is <see cref="Stream.Null"/>)</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="data"/> is null.</exception>
		/// <exception cref="InvalidOperationException">If the size of the <paramref name="data"/> stream exceeds <see cref="int.MaxValue"/> or if it does not support reading.</exception>
		public static Slice FromStream(Stream data)
		{
			Contract.NotNull(data);

			// special case for empty values
			if (data == Stream.Null) return default;
			if (!data.CanRead) throw ThrowHelper.InvalidOperationException("Cannot read from provided stream");

			if (data.Length == 0) return Empty;
			if (data.Length > int.MaxValue) throw ThrowHelper.InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return LoadFromNonBlockingStream(data, length);
			}

			// read asynchronously
			return LoadFromBlockingStream(data, length);
		}

		/// <summary>Asynchronously read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <param name="ct">Optional cancellation token for this operation</param>
		/// <returns>Slice containing the stream content (or <see cref="Slice.Nil"/> if the stream is <see cref="Stream.Null"/>)</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="data"/> is null.</exception>
		/// <exception cref="InvalidOperationException">If the size of the <paramref name="data"/> stream exceeds <see cref="int.MaxValue"/> or if it does not support reading.</exception>
		public static Task<Slice> FromStreamAsync(Stream data, CancellationToken ct)
		{
			Contract.NotNull(data);

			// special case for empty values
			if (data == Stream.Null) return Task.FromResult(Nil);
			if (!data.CanRead) throw ThrowHelper.InvalidOperationException("Cannot read from provided stream");

			if (data.Length == 0) return Task.FromResult(Empty);
			if (data.Length > int.MaxValue) throw ThrowHelper.InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice>(ct);

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream or UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return Task.FromResult(LoadFromNonBlockingStream(data, length));
			}

			// read asynchronously
			return LoadFromBlockingStreamAsync(data, length, 0, ct);
		}

		/// <summary>Read from a non-blocking stream that already contains all the data in memory (MemoryStream, UnmanagedStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromNonBlockingStream(Stream source, int length)
		{
			Contract.Debug.Requires(source is not null && source.CanRead && source.Length <= int.MaxValue);

			if (source is MemoryStream ms)
			{ // Already holds onto a byte[]

				//note: should be use GetBuffer() ? It can throws and is dangerous (could mutate)
				return new Slice(ms.ToArray());
			}

			// read it in bulk, without buffering

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int n = source.Read(buffer, p, r);
				if (n <= 0) throw ThrowHelper.InvalidOperationException($"Unexpected end of stream at {p:N0} / {length:N0} bytes");
				p += n;
				r -= n;
			}
			Contract.Debug.Ensures(r == 0 && p == length);

			return new Slice(buffer);
		}

		/// <summary>Synchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromBlockingStream(Stream source, int length, int chunkSize = 0)
		{
			Contract.Debug.Requires(source is not null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Min(r, chunkSize);
				int n = source.Read(buffer, p, c);
				if (n <= 0) throw ThrowHelper.InvalidOperationException($"Unexpected end of stream at {p:N0} / {length:N0} bytes");
				p += n;
				r -= n;
			}
			Contract.Debug.Ensures(r == 0 && p == length);

			return new Slice(buffer);
		}

		/// <summary>Asynchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non-zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <param name="ct">Optional cancellation token for this operation</param>
		/// <returns>Slice containing the loaded data</returns>
		private static async Task<Slice> LoadFromBlockingStreamAsync(Stream source, int length, int chunkSize, CancellationToken ct)
		{
			Contract.Debug.Requires(source is not null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Min(r, chunkSize);
				int n = await source.ReadAsync(buffer.AsMemory(p, c), ct).ConfigureAwait(false);
				if (n <= 0)
				{
					throw ThrowHelper.InvalidOperationException($"Unexpected end of stream at {p:N0} / {length:N0} bytes");
				}

				p += n;
				r -= n;
			}
			Contract.Debug.Assert(r == 0 && p == length);

			return new Slice(buffer);
		}

		#endregion

		#region Equality, Comparison...

		/// <summary>Checks if an object is equal to the current slice</summary>
		/// <param name="obj">Object that can be either another slice, a byte array, or a byte array segment.</param>
		/// <returns>true if the object represents a sequence of bytes that has the same size and same content as the current slice.</returns>
		public override bool Equals(object? obj) => obj switch
		{
			null => this.IsNull,
			Slice slice => Equals(slice),
			ArraySegment<byte> segment => Equals(segment),
			byte[] bytes => Equals(bytes),
			_ => false
		};

		/// <summary>Gets the hash code for this slice</summary>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the slice.</returns>
		public override int GetHashCode()
		{
			EnsureSliceIsValid();
			return this.IsNull ? 0 : UnsafeHelpers.ComputeHashCode(this.Span);
		}

		/// <summary>Checks if another slice is equal to the current slice.</summary>
		/// <param name="other">Slice compared with the current instance</param>
		/// <returns>true if both slices have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		[Pure]
		public bool Equals(Slice other)
		{
			other.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			// note: Slice.Nil != Slice.Empty
			if (this.IsNull)
			{
				return other.IsNull;
			}

			return !other.IsNull && this.Count == other.Count && this.Span.SequenceEqual(other.Span);
		}

		/// <summary>Checks if the content of a span is equal to the current slice.</summary>
		/// <param name="other">Span of memory compared with the current instance</param>
		/// <returns>true if both locations have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		[Pure]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			this.EnsureSliceIsValid();

			// note: Nil and Empty are both equal to empty span
			return this.Count == other.Length && this.Span.SequenceEqual(other);
		}

		/// <summary>Checks if the content of a span is equal to the current slice.</summary>
		/// <param name="other">Span of memory compared with the current instance</param>
		/// <returns>true if both locations have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		[Pure]
		public bool Equals(ReadOnlyMemory<byte> other) => Equals(other.Span);

		/// <summary>Checks if the content of a span is equal to the current slice.</summary>
		/// <param name="other">Span of memory compared with the current instance</param>
		/// <returns>true if both locations have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		[Pure]
		public bool Equals(Span<byte> other)
		{
			this.EnsureSliceIsValid();

			// note: Nil and Empty are both equal to empty span
			return this.Count == other.Length && this.Span.SequenceEqual(other);
		}

		/// <summary>Lexicographically compare this slice with another one, and return an indication of their relative sort order</summary>
		/// <param name="other">Slice to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		/// <remarks>If both this instance and <paramref name="other"/> are Nil or Empty, the comparison will return ZERO. If only <paramref name="other"/> is Nil or Empty, it will return a NEGATIVE value. If only this instance is Nil or Empty, it will return a POSITIVE value.</remarks>
		public int CompareTo(Slice other)
		{
			other.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			if (this.Count == 0) return other.Count == 0 ? 0 : -1;
			if (other.Count == 0) return +1;
			return this.Span.SequenceCompareTo(other.Span);
		}

		/// <summary>Lexicographically compare this slice with another span, and return an indication of their relative sort order</summary>
		/// <param name="other">Span of memory to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		public int CompareTo(ReadOnlySpan<byte> other)
		{
			this.EnsureSliceIsValid();

			if (this.Count == 0) return other.Length == 0 ? 0 : -1;
			if (other.Length == 0) return +1;
			return this.Span.SequenceCompareTo(other);
		}

		/// <summary>Lexicographically compare this slice with another span, and return an indication of their relative sort order</summary>
		/// <param name="other">Span of memory to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		public int CompareTo(Span<byte> other)
		{
			this.EnsureSliceIsValid();

			if (this.Count == 0) return other.Length == 0 ? 0 : -1;
			if (other.Length == 0) return +1;
			return this.Span.SequenceCompareTo(other);
		}

		/// <summary>Lexicographically compare this slice with another span, and return an indication of their relative sort order</summary>
		/// <param name="other">Span of memory to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		public int CompareTo(ReadOnlyMemory<byte> other)
		{
			this.EnsureSliceIsValid();

			if (this.Count == 0) return other.Length == 0 ? 0 : -1;
			if (other.Length == 0) return +1;
			return this.Span.SequenceCompareTo(other.Span);
		}

		/// <summary>Checks if the content of a byte array segment matches the current slice.</summary>
		/// <param name="other">Byte array segment compared with the current instance</param>
		/// <returns>true if both segment and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(ArraySegment<byte> other)
		{
			return this.Count == other.Count && this.Span.SequenceEqual(other.AsSpan());
		}

		/// <summary>Checks if the content of a byte array matches the current slice.</summary>
		/// <param name="other">Byte array compared with the current instance</param>
		/// <returns>true if the both array and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(byte[]? other)
		{
			if (other is null) return this.IsNull;
			return this.Count == other.Length && this.Span.SequenceEqual(other);
		}

		#endregion

		#region Sanity Checking...

		/// <summary>Verifies that the <see cref="Offset"/> and <see cref="Count"/> fields represent a valid location in <see cref="Array"/></summary>
		/// <remarks>This method is inlined for best performance</remarks>
		/// <exception cref="FormatException">If the slice is not a valid section of a buffer</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureSliceIsValid()
		{
			// Conditions for a slice to be valid:
			// - Count equal to 0 (other fields are ignored)
			// - Count greater than 0 and Array not null and all the bytes of the slice are contained in the underlying buffer

			int count = this.Count;
			if (count != 0)
			{
				byte[]? array = this.Array;
				if (array is null || (ulong) (uint) this.Offset + (ulong) (uint) count > (ulong) (uint) array.Length)
				{
					throw MalformedSlice(this);
				}
			}
		}

		/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
		/// <param name="slice">Slice that is being naughty</param>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception MalformedSlice(Slice slice)
		{
#if DEBUG
			// If you break here, that means that a slice is invalid (negative count, offset, ...), which may be a sign of memory corruption!
			// You should walk up the stack to see what is going on !
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

			if (slice.Offset < 0) return UnsafeHelpers.Errors.SliceOffsetNotNeg();
			if (slice.Count < 0) return UnsafeHelpers.Errors.SliceCountNotNeg();
			if (slice.Count > 0)
			{
				if (slice.IsNull) return UnsafeHelpers.Errors.SliceBufferNotNull();
				if ((ulong) (uint) slice.Offset + (ulong) (uint) slice.Count > (ulong) (uint) slice.Array.Length) return UnsafeHelpers.Errors.SliceBufferTooSmall();
			}
			// maybe it's Lupus ?
			return UnsafeHelpers.Errors.SliceInvalid();
		}

		#endregion

		/// <summary>Return the sum of the size of all the slices with an additional prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, Slice[] slices)
		{
			long size = prefix * slices.Length;
			for (int i = 0; i < slices.Length; i++)
			{
				size += slices[i].Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additional prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, Slice?[] slices)
		{
			long size = prefix * slices.Length;
			for (int i = 0; i < slices.Length; i++)
			{
				size += slices[i].GetValueOrDefault().Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additional prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, List<Slice> slices)
		{
			long size = prefix * slices.Count;
			foreach (var val in slices)
			{
				size += val.Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additional prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, List<Slice?> slices)
		{
			long size = prefix * slices.Count;
			foreach (var val in slices)
			{
				size += val.GetValueOrDefault().Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additional prefix, and test if they all share the same buffer</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <param name="commonStore">Receives null if at least two slices are stored in a different buffer. If not null, return the common buffer for all the keys</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSizeAndCommonStore(int prefix, Slice[] slices, out byte[]? commonStore)
		{
			if (slices.Length == 0)
			{
				commonStore = null;
				return 0;
			}
			byte[] store = slices[0].Array;
			if (slices.Length == 1)
			{
				commonStore = store;
				return prefix + slices[0].Count;
			}

			bool sameStore = true;
			long size = slices[0].Count + slices.Length * prefix;
			for (int i = 1; i < slices.Length; i++)
			{
				size += slices[i].Count;
				sameStore &= (slices[i].Array == store);
			}
			commonStore = sameStore ? store : null;
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additional prefix, and test if they all share the same buffer</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <param name="commonStore">Receives null if at least two slices are stored in a different buffer. If not null, return the common buffer for all the keys</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSizeAndCommonStore(int prefix, List<Slice> slices, out byte[]? commonStore)
		{
			Contract.Debug.Requires(slices is not null);
			if (slices.Count == 0)
			{
				commonStore = null;
				return 0;
			}
			byte[] store = slices[0].Array;
			if (slices.Count == 1)
			{
				commonStore = store;
				return prefix + slices[0].Count;
			}

			bool sameStore = true;
			long size = slices[0].Count + slices.Count * prefix;
			foreach (var val in slices)
			{
				size += val.Count;
				sameStore &= (val.Array == store);
			}
			commonStore = sameStore ? store : null;
			return checked((int)size);
		}

		/// <summary>Structure that keeps buffers from moving in memory during GC collections</summary>
		/// <remarks>
		/// Caller must ensure that this structure is properly Disposed in all executions paths once the buffers are not needed anymore!
		/// It is safe to call Dispose() multiple times (though the buffers will be unpinned on the first call)
		/// </remarks>
		public struct Pinned : IDisposable
		{

			/// <summary>GC Handle on the main buffer</summary>
			public GCHandle Handle;

			/// <summary>Additional GC Handles (optional)</summary>
			internal readonly GCHandle[]? Handles;

			internal object? Owner;

			/// <summary>Creates a pinned handle for a slice</summary>
			public Pinned(object owner, byte[] buffer, List<Slice>? extra)
			{
				Contract.Debug.Requires(owner is not null && buffer is not null);

				this.Owner = buffer;
				this.Handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
				if (extra is null || extra.Count == 0)
				{
					this.Handles = null;
				}
				else
				{
					var handles = new GCHandle[extra.Count];
					this.Handles = handles;
					int p = 0;
					foreach (var chunk in extra)
					{
						handles[p++] = GCHandle.Alloc(chunk.Array, GCHandleType.Pinned);
					}
					handles[p] = GCHandle.Alloc(buffer);
				}
			}

			/// <inheritdoc cref="GCHandle.IsAllocated"/>
			public readonly bool IsAllocated => this.Handle.IsAllocated;

			/// <inheritdoc />
			public void Dispose()
			{
				if (this.Owner is not null)
				{
					if (this.Handle.IsAllocated) this.Handle.Free();
					var handles = this.Handles;
					if (handles is not null)
					{
						for (int i = 0; i < handles.Length; i++)
						{
							if (handles[i].IsAllocated) handles[i].Free();
						}
					}
					this.Owner = null;
				}
			}
		}

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private readonly struct DebugView
		{

			private readonly Slice m_slice;

			public DebugView(Slice slice)
			{
				m_slice = slice;
			}

			// ReSharper disable InconsistentNaming

			/// <summary>Size of the slice</summary>
			public int _Count => m_slice.Count;

			/// <summary>Offset of the start of slice in the buffer</summary>
			public int _Offset => m_slice.Offset;

			/// <summary>Buffer</summary>
			public byte[] _Array => m_slice.Array;

			// ReSharper restore InconsistentNaming

			public ReadOnlySpan<byte> Data => m_slice.Span;

			public string Content => Slice.Dump(m_slice, maxSize: DefaultPrettyPrintSize);

			public string? TextUtf8
				=> m_slice.Count == 0
					? (m_slice.IsNull ? null : string.Empty)
					: Utf8NoBomEncodingNoThrow.GetString(m_slice.Span);

			public string? TextLatin1
				=> m_slice.Count == 0
					? (m_slice.IsNull ? null : string.Empty)
					: Encoding.Latin1.GetString(m_slice.Span);

			public string? Hex =>
				m_slice.Count == 0
					? (m_slice.IsNull ? null : string.Empty)
					: m_slice.Count <= Slice.DefaultPrettyPrintSize
						? m_slice.ToHexString(' ')
						: m_slice.Substring(0, Slice.DefaultPrettyPrintSize).ToHexString(' ') + "[\u2026]";

			/// <summary>Encoding using only for display purpose: we don't want to throw in the 'Text' property if the input is not text!</summary>
			internal static readonly UTF8Encoding Utf8NoBomEncodingNoThrow = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

		}

	}

	/// <summary>Helper methods for Slice</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class SliceExtensions
	{

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Slice EmptyOrNil(byte[]? array)
		{
			//note: we consider the "empty" or "nil" case less frequent, so we handle it in a non-inlined method
			return array is null ? default : Slice.Empty;
		}

		/// <summary>Handle the Nil/Empty memoization</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Slice EmptyOrNil(byte[]? array, int count)
		{
			//note: we consider the "empty" or "nil" case less frequent, so we handle it in a non-inlined method
			if (array is null)
			{
				return count == 0 ? default : throw UnsafeHelpers.Errors.BufferArrayNotNull();
			}

			return Slice.Empty;
		}

		/// <summary>Returns a slice that wraps the whole array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this byte[]? bytes)
		{
			return bytes is not null && bytes.Length > 0 ? new Slice(bytes) : EmptyOrNil(bytes);
		}

		/// <summary>Returns the tail of the array, starting from the specified <b>offset</b></summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first byte of the slice</param>
		/// <remarks><b>REMINDER:</b> the parameter is the <b>offset</b>, and not the length !</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this byte[]? bytes, [Positive] int offset)
		{
			//note: this method is DANGEROUS! Caller may think it is passing a count instead of an offset.

			if (bytes is null)
			{
				return offset == 0 ? Slice.Nil : throw UnsafeHelpers.Errors.BufferArrayNotNull();
			}

			// bound check
			if ((uint) offset > (uint) bytes.Length)
			{
				throw UnsafeHelpers.Errors.BufferArrayToSmall();
			}

			return bytes.Length != 0 ? new Slice(bytes, offset, bytes.Length - offset) : Slice.Empty;
		}

		/// <summary>Returns a slice from the subsection of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first element of the slice (if not empty)</param>
		/// <param name="count">Number of bytes to take</param>
		/// <returns>
		/// Slice that maps the corresponding subsection of the array.
		/// If <paramref name="count"/> is 0 then either <see cref="Slice.Empty"/> or <see cref="Slice.Nil"/> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this byte[]? bytes, [Positive] int offset, [Positive] int count)
		{
			//note: this method will frequently be called with offset==0, so we should optimize for this case!
			if (bytes is null || count == 0)
			{
				return EmptyOrNil(bytes, count);
			}

			// bound check
			// ReSharper disable once RedundantCast
			if ((ulong) (uint) offset + (ulong) (uint) count > (ulong) (uint) bytes.Length)
			{
				UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();
			}

			return new Slice(bytes, offset, count);
		}

		/// <summary>Returns a slice from the subsection of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first element of the slice (if not empty)</param>
		/// <param name="count">Number of bytes to take</param>
		/// <returns>
		/// Slice that maps the corresponding subsection of the array.
		/// If <paramref name="count"/> is 0, then either <see cref="Slice.Empty"/> or <see cref="Slice.Nil"/> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this byte[]? bytes, uint offset, uint count)
		{
			//note: this method will frequently be called with offset==0, so we should optimize for this case!
			if (bytes is null || count == 0) return EmptyOrNil(bytes, (int) count);

			// bound check
			if (offset >= (uint) bytes.Length || count > ((uint) bytes.Length - offset)) throw UnsafeHelpers.Errors.OffsetOutsideSlice();

			return new Slice(bytes, (int) offset, (int) count);
		}

		/// <summary>Returns a slice from the subsection of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="range">Range of the array to return</param>
		/// <returns>
		/// Slice that maps the corresponding subsection of the array.
		/// If <paramref name="range"/> is empty, then either <see cref="Slice.Empty"/> or <see cref="Slice.Nil"/> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		public static Slice AsSlice(this byte[]? bytes, Range range)
		{
			if (bytes is null)
			{
				return AsSliceNil(range);
			}

			(int offset, int count) = range.GetOffsetAndLength(bytes.Length);
			return count != 0 ? new Slice(bytes, offset, count) : Slice.Empty;

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Slice AsSliceNil(Range range)
			{
				var startIndex = range.Start;
				var endIndex = range.End;

				if (!startIndex.Equals(Index.Start) || !endIndex.Equals(Index.Start))
				{
					throw UnsafeHelpers.Errors.BufferArrayNotNull();
				}

				return Slice.Nil;
			}
		}

		/// <summary>Returns a slice that is the wraps the same memory region as this <see cref="ArraySegment{T}"/></summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this ArraySegment<byte> self)
		{
			// We trust the ArraySegment<byte> ctor to validate the arguments beforehand.
			// If somehow the arguments were corrupted (intentionally or not), then the same problem could have happened with the slice anyway!

			// ReSharper disable once AssignNullToNotNullAttribute
			return self.Count != 0 ? new Slice(self.Array!, self.Offset, self.Count) : EmptyOrNil(self.Array, self.Count);
		}

		/// <summary>Returns a slice that is the wraps the same memory region as this <see cref="ArraySegment{T}"/></summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this ArraySegment<byte> self, int offset) => AsSlice(self).Substring(offset);

		/// <summary>Returns a slice that is the wraps the same memory region as this <see cref="ArraySegment{T}"/></summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this ArraySegment<byte> self, int offset, int count) => AsSlice(self).Substring(offset, count);

		/// <summary>Creates a new slice by copying the contents of this span of bytes</summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <returns><see cref="Slice"/> that points to a copy of the bytes in <paramref name="source"/></returns>
		/// <remarks>Returns the <see cref="Slice.Empty"/> singleton if <paramref name="source"/> is empty</remarks>
		/// <remarks>Any future change to either span or resulting slice will not impact the other.</remarks>
		/// <example><code>
		/// Span&lt;byte> source = [ 0x12, 0x34 ];
		/// // create a copy in memory
		/// var slice = source.ToSlice();
		/// Debug.Assert(slice[0] == source[0]);
		/// // changing the source does not affect the copy
		/// source[0] = 0x56;
		/// Debug.Assert(slice[0] != source[0]);
		/// </code></example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice(this ReadOnlySpan<byte> source)
		{
			return Slice.FromBytes(source);
		}

		/// <summary>Creates a new slice by copying the contents of this span of bytes</summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <returns><see cref="Slice"/> that points to a copy of the bytes in <paramref name="source"/></returns>
		/// <remarks>Returns the <see cref="Slice.Empty"/> singleton if <paramref name="source"/> is empty</remarks>
		/// <remarks>Any future change to either span or resulting slice will not impact the other.</remarks>
		/// <example><code>
		/// Span&lt;byte> source = [ 0x12, 0x34 ];
		/// // create a copy in memory
		/// var slice = source.ToSlice();
		/// Debug.Assert(slice[0] == source[0]);
		/// // changing the source does not affect the copy
		/// source[0] = 0x56;
		/// Debug.Assert(slice[0] != source[0]);
		/// </code></example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice(this Span<byte> source)
		{
			return Slice.FromBytes(source);
		}

		/// <summary>Creates a new <see cref="SliceOwner"/> that wraps a copy the contents of this span of bytes, using a provided <see cref="ArrayPool{T}"/></summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <param name="pool">Pool used to allocate the backing array</param>
		/// <returns><see cref="SliceOwner"/> that points to a copy of the bytes in <paramref name="source"/>, using an array rented from the <paramref name="pool"/> as its backing store.</returns>
		/// <remarks>The return value must be <see cref="SliceOwner.Dispose">disposed</see> for the buffer to return to the pool.</remarks>
		/// <example><code>
		/// // write some arbitrary data to a buffer
		/// ReadOnlySpan&lt;byte> data = /*....*/;
		/// // copy the formatted data to a rented buffer
		/// using(var buffer = data.ToSlice(ArrayPool&lt;byte>.Shared))
		/// {
		///    // use buffer.Data or buffer.Memory or buffer.Span
		/// }
		/// </code></example>
		public static SliceOwner ToSliceOwner(this ReadOnlySpan<byte> source, ArrayPool<byte> pool)
		{
			return Slice.FromBytes(source, pool);
		}

		/// <summary>Creates a new <see cref="SliceOwner"/> that wraps a copy the contents of this span of bytes, using a provided <see cref="ArrayPool{T}"/></summary>
		/// <param name="source">Span of bytes to copy</param>
		/// <param name="pool">Pool used to allocate the backing array</param>
		/// <returns><see cref="SliceOwner"/> that points to a copy of the bytes in <paramref name="source"/>, using an array rented from the <paramref name="pool"/> as its backing store.</returns>
		/// <remarks>The return value must be <see cref="SliceOwner.Dispose">disposed</see> for the buffer to return to the pool.</remarks>
		/// <example><code>
		/// // write some arbitrary data to a buffer
		/// Span&lt;byte> data = stackalloc byte[128];
		/// Random.Shared.NextInt64().TryFormatTo(data, out int written);
		/// // copy the formatted data to a rented buffer
		/// using(var buffer = data[..written].ToSlice(ArrayPool&lt;byte>.Shared))
		/// {
		///    // use buffer.Data or buffer.Memory or buffer.Span
		/// }
		/// </code></example>
		public static SliceOwner ToSliceOwner(this Span<byte> source, ArrayPool<byte> pool)
		{
			return Slice.FromBytes(source, pool);
		}

		/// <summary>Return a <see cref="SliceReader"/> that will expose the content of a buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self)
		{
			return new SliceReader(self);
		}

		/// <summary>Return a <see cref="SliceReader"/> that will expose the start of a buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self, int count)
		{
			return new SliceReader(self, 0, count);
		}

		/// <summary>Return a <see cref="SliceReader"/> that will expose a subsection of a buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self, int offset, int count)
		{
			return new SliceReader(self, offset, count);
		}

		/// <summary>Return a <see cref="SliceReader"/> that will expose a subsection of a buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self, Range range)
		{
			return AsSlice(self, range).ToSliceReader();
		}

		/// <summary>Return a stream that can read from the current slice.</summary>
		public static MemoryStream ToStream(this Slice slice)
		{
			if (slice.IsNull) throw ThrowHelper.InvalidOperationException("Slice cannot be null");
			return new MemoryStream(slice.Array, slice.Offset, slice.Count, writable: false, publiclyVisible: true);
		}

		/// <summary>Exposes the content of a <see cref="MemoryStream"/> as a <see cref="Slice"/> if possible, or returns a copy</summary>
		/// <param name="stream">Stream with some content</param>
		/// <returns>Slice that uses the stream internal buffer, if it is publicly visible; otherwise, a copy of the stream's content.</returns>
		public static Slice ToSlice(this MemoryStream stream)
		{
			Contract.Debug.Requires(stream is not null);
			if (stream.TryGetBuffer(out ArraySegment<byte> buf))
			{
				return buf.AsSlice();
			}
			return stream.ToArray().AsSlice();
		}

		/// <summary>Reads the entire content of a <see cref="Stream"/> into a <see cref="Slice"/> in memory.</summary>
		public static Slice ReadAllSlice(this Stream input)
		{
			Contract.NotNull(input);

			if (input is MemoryStream ms)
			{
				ms.ToArray().AsSlice();
			}

			var capacity = input.CanSeek ? input.Length : 0;
			using (var output = new MemoryStream(capacity >= 0 && capacity < int.MaxValue ? (int) capacity : 0))
			{
				input.CopyTo(output);
				return output.GetBuffer().AsSlice(0, checked((int) output.Length));
			}
		}

		/// <summary>Reads the entire content of a <see cref="Stream"/> into a <see cref="Slice"/> in memory.</summary>
		public static async Task<Slice> ReadAllSliceAsync(this Stream input, CancellationToken ct)
		{
			Contract.NotNull(input);
			ct.ThrowIfCancellationRequested();

			if (input is MemoryStream ms)
			{
				ms.ToArray().AsSlice();
			}

			var capacity = input.CanSeek ? input.Length : 0;
			using (var output = new MemoryStream(capacity >= 0 && capacity < int.MaxValue ? (int) capacity : 0))
			{
				await input.CopyToAsync(output, ct).ConfigureAwait(false);
				return output.GetBuffer().AsSlice(0, checked((int) output.Length));
			}
		}

		/// <summary>Reads exactly <paramref name="count"/> bytes from the specified stream.</summary>
		/// <param name="input">Input stream</param>
		/// <param name="count">Number of bytes to read from the current position in the <paramref name="input">stream</paramref></param>
		/// <returns>Slice that contains exactly <paramref name="count"/> bytes.</returns>
		/// <remarks>This method implements the classical read loop, to handle for situations where a read for N bytes from the stream returns less than requested (ex: sockets, pipes, ...)</remarks>
		public static Slice ReadSliceExactly(this Stream input, int count)
		{
			Contract.NotNull(input);
			Contract.Positive(count);

			if (!input.CanRead) throw new InvalidOperationException("The specified stream does not support read operations.");

			//NOTE: reading 0 bytes from a stream is not exactly well-defined, because returning 0 means "end of file"!
			// => some stream use this as a way to await for new data ("WaitForNextByte") while other stream do not support this.
			//REVIEW: should we return empty? or should we throw? for now, we simply defer to the underlying stream implementation
			if (count == 0) return Slice.Empty;

			// if we can get the stream's buffer, AND we know it is not writeable, then we can safely expose the underlying buffer
			if (input is MemoryStream ms && !ms.CanWrite && ms.TryGetBuffer(out var buffer))
			{ // we can simply grab the data from the buffer

				// check if there's enough data in the stream
				int position = checked((int) ms.Position);
				long expected = checked(position + count);
				if (expected > ms.Length)
				{ // stream is too small!
					ms.Seek(0, SeekOrigin.End);
					goto stream_too_small;
				}

				// advance the cursor
				ms.Seek(count, SeekOrigin.Current);

				// return the request chunk of the underlying buffer
				return new Slice(buffer.Array!, buffer.Offset + position, count);
			}

			byte[] tmp = new byte[count];
			int p = 0;
			while (p < count)
			{
				int n = input.Read(tmp, p, count - p);
				if (n <= 0) goto stream_too_small;
				p += n;
			}
			return new Slice(tmp);

		stream_too_small:
			throw new IOException("The stream does not contain enough data to satisfy the read operation exactly.");
		}

		/// <summary>Reads exactly <paramref name="count"/> bytes from the specified stream.</summary>
		/// <param name="input">Input stream</param>
		/// <param name="count">Number of bytes to read from the current position in the <paramref name="input">stream</paramref></param>
		/// <param name="ct">Token used to cancel the read operation</param>
		/// <returns>Slice that contains exactly <paramref name="count"/> bytes.</returns>
		/// <remarks>This method implements the classical read loop, to handle for situations where a read for N bytes from the stream returns less than requested (ex: sockets, pipes, ...)</remarks>
		public static async Task<Slice> ReadSliceExactlyAsync(this Stream input, int count, CancellationToken ct)
		{
			Contract.NotNull(input);
			Contract.Positive(count);
			ct.ThrowIfCancellationRequested();

			if (input is MemoryStream ms)
			{ // fast path for memory streams!
				return ReadSliceExactly(ms, count);
			}

			if (!input.CanRead)
			{
				throw new InvalidOperationException("The specified stream does not support read operations.");
			}

			if (count == 0)
			{
				return Slice.Empty;
			}

			byte[] tmp = new byte[count];
			int p = 0;
			while (p < count)
			{
				int n = await input.ReadAsync(tmp.AsMemory(p, count - p), ct).ConfigureAwait(false);
				if (n <= 0)
				{
					throw new IOException("The file does not contain enough data to satisfy the read operation exactly.");
				}

				p += n;
			}
			return new Slice(tmp);
		}

		/// <summary>Reads the entire content of a <see cref="PipeReader"/> into a <see cref="Slice"/>> in memory.</summary>
		public static async Task<Slice> ReadAllSliceAsync(this PipeReader input, CancellationToken ct)
		{
			using (var ms = new MemoryStream())
			{
				await input.CopyToAsync(ms, ct).ConfigureAwait(false);
				return ms.GetBuffer().AsSlice(0, checked((int) ms.Length));
			}
		}

		/// <summary>Returns a <see cref="PipeReader"/> that will consume the content of this slice</summary>
		public static PipeReader AsPipeReader(this Slice slice)
		{
			return new SlicePipeReader(slice);
		}

		private sealed class SlicePipeReader : PipeReader
		{

			public SlicePipeReader(Slice slice)
			{
				this.Buffer = slice;
			}

			private readonly Slice Buffer;

			private int Offset;

			public override bool TryRead(out ReadResult result)
			{
				if (this.Offset < this.Buffer.Count)
				{
					result = new(new(this.Buffer.Memory[this.Offset..]), false, true);
					return true;
				}

				result = default;
				return false;
			}

			public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
			{
				TryRead(out var result);
				return new(result);
			}

			public override void AdvanceTo(SequencePosition consumed) => AdvanceTo(consumed, consumed);

			public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
			{
				int offset = consumed.GetInteger();
				int cursor = checked(this.Offset + offset);
				if (cursor > this.Buffer.Count) throw new ArgumentException("Cannot advance past the end of the buffer.", nameof(consumed));

				this.Offset = cursor;
			}

			public override void CancelPendingRead()
			{
				//NOP
			}

			public override void Complete(Exception? exception = null)
			{
				//NOP
			}
		}

		/// <summary>Copies the content of a <see cref="Slice"/> into a <see cref="PipeWriter"/>.</summary>
		public static async Task CopyToAsync(this Slice slice, PipeWriter output, CancellationToken ct)
		{
			if (slice.IsNull) throw new ArgumentException("Source slice cannot be nil.", nameof(slice));
			Contract.NotNull(output);
			ct.ThrowIfCancellationRequested();

			var result = await output.WriteAsync(slice.Memory, ct).ConfigureAwait(false);
			if (result.IsCanceled)
			{
				ct.ThrowIfCancellationRequested();
				throw new OperationCanceledException("The underlying pipe was canceled.");
			}
		}

	}

	/// <summary>Advanced or unsafe operations on <see cref="Slice"/></summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class SliceMarshal
	{

		/// <summary>Exposes the internal buffer if it has the exact same size as the slice</summary>
		/// <param name="buffer">Slice with some content</param>
		/// <param name="bytes">Receives the internal buffer, or <see langword="null"/> if the buffer is larger than the slice</param>
		/// <returns><see langword="true"/>> if the buffer is complete and is exposed in <paramref name="bytes"/>, or <see langword="false"/> if the slice only cover a part of the buffer.</returns>
		/// <remarks>
		/// <para>Used to optimize the case when the caller needs to pass the content of the slice to a legacy API that requires a <c>byte[]</c> without support for spans or specifying an offset or length,
		/// and would like to avoid an extra copy, especially if the buffer is know to have the correct size.</para>
		/// <para>The expected pattern is:
		/// <code>if (SliceMarshal.TryGetBytes(slice, out var bytes))
		/// { // no copy required
		///     LegacyAPI.DoSomething(bytes);
		/// }
		/// else
		/// { // need to allocate and copy!!!
		///     LegacyAPI.DoSomething(slice.ToArray());
		/// }
		/// </code></para>
		/// <para>CAUTION: Slice are expected to be read-only, but exposing the internal buffer may lead to unexpected mutations! Use with caution, and make sure that any consumer of the buffer only read and never write to it!</para>
		/// </remarks>
		public static bool TryGetBytes(Slice buffer, [MaybeNullWhen(false)] out byte[] bytes)
		{
			var arr = buffer.Array;

			// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (arr is null)
			{
				bytes = [ ];
				return true;
			}

			if (buffer.Offset != 0 || buffer.Count != arr.Length)
			{
				bytes = null;
				return false;
			}

			bytes = arr;
			return true;
		}

		/// <summary>Exposes the internal buffer if it has the exact same size as the slice; otherwise, returns a copy of the content</summary>
		/// <param name="buffer">Slice with some content</param>
		/// <returns>A byte array which is either the original buffer, or a copy.</returns>
		/// <remarks>
		/// <para>Used to optimize situations where the caller needs to pass the content of the slice to a legacy API that requires a <c>byte[]</c> without support for spans or specifying an offset or length,
		/// and would like to avoid an extra copy, especially if the buffer is know to have the correct size.</para>
		/// <para>The expected pattern is:
		/// <code>LegacyAPI.DoSomething(SliceMarshal.GetBytesOrCopy()); // has a chance to skip an extra copy if the buffer has the correct size already</code>
		/// </para>
		/// <para><b>CAUTION</b>: Slice are expected to be read-only, but exposing the internal buffer may lead to unexpected mutations! Use with caution, and make sure that any consumer of the buffer only read and never write to it!</para>
		/// </remarks>
		public static byte[] GetBytesOrCopy(Slice buffer)
		{
			return TryGetBytes(buffer, out var bytes) ? bytes : buffer.ToArray();
		}

		/// <summary>Try to convert a <see cref="ReadOnlyMemory{T}"/> into a Slice if it is backed by a managed byte array.</summary>
		/// <param name="buffer">Buffer that maps a region of memory</param>
		/// <param name="slice">If the method returns <c>true</c>, a slice that maps the same region of managed memory.</param>
		/// <returns>True if the memory was backed by a managed array; otherwise, false.</returns>
		[Pure]
		public static bool TryGetSlice(ReadOnlyMemory<byte> buffer, out Slice slice)
		{
			if (!MemoryMarshal.TryGetArray(buffer, out var segment))
			{
				slice = default;
				return false;
			}

			slice = segment.Count == 0
				? Slice.Empty
				: new Slice(segment.Array!, segment.Offset, segment.Count);

			return true;
		}

		/// <summary>Returns a copy of the memory content of an array of item</summary>
		[Pure]
		public static Slice CopyAsBytes<T>(ReadOnlySpan<T> items)
			where T : struct
		{
			return Slice.FromBytes(MemoryMarshal.AsBytes(items));
		}

		/// <summary>Returns a copy of the memory content of an array of item</summary>
		[Pure]
		public static Slice CopyAsBytes<T>(Span<T> items)
			where T : struct
		{
			return Slice.FromBytes(MemoryMarshal.AsBytes(items));
		}

		/// <summary>Returns a copy of the memory content of an array of item</summary>
		[Pure]
		public static Slice CopyAsBytes<T>(ReadOnlySpan<T> items, ref byte[]? buffer)
			where T : struct
		{
			return Slice.FromBytes(MemoryMarshal.AsBytes(items), ref buffer);
		}

		/// <summary>Returns a copy of the memory content of an array of item</summary>
		[Pure]
		public static Slice CopyAsBytes<T>(Span<T> items, ref byte[]? buffer)
			where T : struct
		{
			return Slice.FromBytes(MemoryMarshal.AsBytes(items), ref buffer);
		}

		/// <summary>Returns a valid reference to the first byte in the slice</summary>
		/// <param name="buffer">Slice</param>
		/// <returns>Reference to the first byte, or where it would be if the slice is empty</returns>
		/// <exception cref="ArgumentException">If the slice is empty.</exception>
		public static ref readonly byte GetReference(Slice buffer)
		{
			if (buffer.Count <= 0)
			{
				throw new ArgumentException(null, nameof(buffer));
			}
			return ref MemoryMarshal.GetReference(buffer.Span);
		}

		/// <summary>Returns a valid reference to a byte at the given location in the slice</summary>
		/// <param name="buffer">Slice</param>
		/// <param name="index">Index in the slice.</param>
		/// <returns>Reference to the corresponding byte</returns>
		/// <exception cref="IndexOutOfRangeException">If the slice is empty, of <paramref name="index"/> is outside of the slice.</exception>
		public static ref readonly byte GetReferenceAt(Slice buffer, int index)
		{
			if (buffer.Count <= 0)
			{
				throw new ArgumentException(null, nameof(buffer));
			}
			if ((uint) index >= buffer.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}
			return ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer.Span), index);
		}

		/// <summary>Returns a valid reference to a byte at the given location in the slice</summary>
		/// <param name="buffer">Slice</param>
		/// <param name="index">Index in the slice.</param>
		/// <returns>Reference to the corresponding byte</returns>
		/// <exception cref="IndexOutOfRangeException">If the slice is empty, of <paramref name="index"/> is outside of the slice.</exception>
		public static ref readonly byte GetReferenceAt(Slice buffer, Index index)
			=> ref GetReferenceAt(buffer, index.GetOffset(buffer.Count));

		/// <summary>Returns a valid reference to the last byte in the slice</summary>
		/// <param name="buffer">Slice</param>
		/// <returns>Reference to the corresponding byte</returns>
		/// <exception cref="ArgumentException">If the slice is empty.</exception>
		public static ref readonly byte GetReferenceToLast(Slice buffer)
		{
			if (buffer.Count <= 0)
			{
				throw new ArgumentException(null, nameof(buffer));
			}
			return ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer.Span), buffer.Count - 1);
		}

		/// <summary>Returns a reference to a byte at the given location in the slice, or <see langword="null"/> if it is outside</summary>
		/// <param name="buffer">Slice</param>
		/// <param name="index">Index in the slice.</param>
		/// <param name="valid">Receives <see langword="true"/> if <paramref name="index"/> is inside the slice; otherwise, <see langword="false"/>.</param>
		/// <returns>Reference to the corresponding byte, or <see langword="null"/> if <paramref name="index"/> is outside the bounds of the slice.</returns>
		/// <exception cref="IndexOutOfRangeException">If the slice is empty, of <paramref name="index"/> is outside of the slice.</exception>
		public static ref readonly byte TryGetReferenceAt(Slice buffer, int index, out bool valid)
		{
			if ((uint) index >= buffer.Count)
			{
				valid = false;
				return ref Unsafe.NullRef<byte>();
			}

			valid = true;
			return ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer.Span), index);
		}

		/// <summary>Returns a reference to a byte at the given location in the slice, or <see langword="null"/> if it is outside</summary>
		/// <param name="buffer">Slice</param>
		/// <param name="index">Index in the slice.</param>
		/// <param name="valid">Receives <see langword="true"/> if <paramref name="index"/> is inside the slice; otherwise, <see langword="false"/>.</param>
		/// <returns>Reference to the corresponding byte, or <see langword="null"/> if <paramref name="index"/> is outside the bounds of the slice.</returns>
		/// <exception cref="IndexOutOfRangeException">If the slice is empty, of <paramref name="index"/> is outside of the slice.</exception>
		public static ref readonly byte TryGetReferenceAt(Slice buffer, Index index, out bool valid)
			=> ref TryGetReferenceAt(buffer, index.GetOffset(buffer.Count), out valid);

		/// <summary>Tests if a reference points inside the corresponding slice</summary>
		/// <param name="buffer">Buffer that is being tested</param>
		/// <param name="ptr">Pointer that may or may not point inside <paramref name="buffer"/></param>
		/// <returns><see langword="true"/> if <paramref name="ptr"/> points to a byte inside the slice, or <see langword="false"/> if it is outside the slice</returns>
		[Pure]
		public static bool IsAddressInside(Slice buffer, ref readonly byte ptr)
		{
			var span = buffer.Span;
			if (span.Length == 0)
			{
				return false;
			}

			ref readonly byte start = ref buffer.Array[buffer.Offset];
			ref readonly byte end = ref Unsafe.Add(ref Unsafe.AsRef(in start), span.Length);
#if NET8_0_OR_GREATER
			return !Unsafe.IsAddressLessThan(in ptr, in start) && Unsafe.IsAddressLessThan(in ptr, in end);
#else
			return !Unsafe.IsAddressLessThan(ref Unsafe.AsRef(in ptr), ref Unsafe.AsRef(in start)) && Unsafe.IsAddressLessThan(ref Unsafe.AsRef(in ptr), ref Unsafe.AsRef(in end));
#endif
		}

		/// <summary>Returns the offset of an unmanaged pointer inside the slice</summary>
		/// <param name="ptr">Unamanged pointer</param>
		/// <param name="buffer">Slice to compare</param>
		/// <param name="offset">If the pointer is inside the slice, receives the offset from the start of the slice</param>
		/// <returns><see langword="true"/> if the pointer is contained inside the slice (or after the end).</returns>
		/// <returns>
		/// <para>If <paramref name="ptr"/> points to where the next byte after the last byte of the slice, then the method will still return true, and offset will be equal to the length of the slice.</para>
		/// <para>This simplifies the logic of cursor management when writing inside a pre-allocated buffer.</para>
		/// </returns>
		[Pure]
		public static bool TryGetOffset(ref byte ptr, Slice buffer, out int offset)
		{
			var span = buffer.Span;
			ref byte start = ref MemoryMarshal.GetReference(span);
			if (Unsafe.IsAddressLessThan(ref ptr, ref start))
			{ // before the end
				goto invalid;
			}

			ref byte end = ref Unsafe.Add(ref start, span.Length);
			if (Unsafe.IsAddressGreaterThan(ref ptr, ref end))
			{ // after the end
				goto invalid;
			}

			// we are inside (or right at the end of) the slice
			offset = Unsafe.ByteOffset(ref start, ref ptr).ToInt32();
			return true;

		invalid:
			offset = 0;
			return false;

		}

		/// <summary>Reinterprets a Slice of bytes as a read-only reference to the structure of type <typeparamref name="T" />.</summary>
		/// <param name="buffer">The Slice to reinterpret.</param>
		/// <typeparam name="T">The type of the returned reference.</typeparam>
		/// <exception cref="T:System.ArgumentException"> <typeparamref name="T" /> contains managed object references.</exception>
		/// <returns>The read-only reference to the structure of type <typeparamref name="T" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref readonly T AsRef<T>(Slice buffer) where T : struct
		{
			return ref MemoryMarshal.AsRef<T>(buffer.Span);
		}

		/// <summary>Casts a Slice to a read-only span of another primitive type.</summary>
		/// <param name="buffer">The source slice to convert.</param>
		/// <typeparam name="TTo">The type of the target span.</typeparam>
		/// <exception cref="T:System.ArgumentException"> <typeparamref name="TTo" /> contains managed object references.</exception>
		/// <returns>The converted read-only span.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<TTo> Cast<TTo>(Slice buffer)
			where TTo : struct
		{
			return MemoryMarshal.Cast<byte, TTo>(buffer.Span);
		}

		/// <summary>Reads a structure of type <typeparamref name="T" /> out of a <see cref="Slice"/>.</summary>
		/// <param name="source">A slice.</param>
		/// <typeparam name="T">The type of the item to retrieve from the slice.</typeparam>
		/// <exception cref="T:System.ArgumentException"> <typeparamref name="T" /> contains managed object references.</exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="source" /> is smaller than <typeparamref name="T" />'s length in bytes.</exception>
		/// <returns>The structure retrieved from the read-only span.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Read<T>(Slice source) where T : struct
		{
			return MemoryMarshal.Read<T>(source.Span);
		}

		/// <summary>Reads a structure of type <typeparamref name="T" /> out of a <see cref="Slice"/>.</summary>
		/// <param name="source">A slice.</param>
		/// <param name="index">Offset (in bytes) from the start of the slice</param>
		/// <typeparam name="T">The type of the item to retrieve from the slice.</typeparam>
		/// <exception cref="T:System.ArgumentException"> <typeparamref name="T" /> contains managed object references.</exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="source" /> is smaller than <typeparamref name="T" />'s length in bytes.</exception>
		/// <returns>The structure retrieved from the read-only span.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadAt<T>(Slice source, int index) where T : struct
		{
			return MemoryMarshal.Read<T>(source.Span[index..]);
		}

		/// <summary>Reads a structure of type <typeparamref name="T" /> out of a <see cref="Slice"/>.</summary>
		/// <param name="source">A slice.</param>
		/// <param name="index">Offset (in bytes) in the slice</param>
		/// <typeparam name="T">The type of the item to retrieve from the slice.</typeparam>
		/// <exception cref="T:System.ArgumentException"> <typeparamref name="T" /> contains managed object references.</exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="source" /> is smaller than <typeparamref name="T" />'s length in bytes.</exception>
		/// <returns>The structure retrieved from the read-only span.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadAt<T>(Slice source, Index index) where T : struct
		{
			return MemoryMarshal.Read<T>(source.Span[index.GetOffset(source.Count)..]);
		}

		/// <summary>Tries to read a structure of type <typeparamref name="T" /> from a <see cref="Slice"/>.</summary>
		/// <param name="source">A slice.</param>
		/// <param name="value">When the method returns, an instance of <typeparamref name="T" />.</param>
		/// <typeparam name="T">The type of the structure to retrieve.</typeparam>
		/// <exception cref="T:System.ArgumentException"> <typeparamref name="T" /> contains managed object references.</exception>
		/// <returns> <see langword="true" /> if the method succeeds in retrieving an instance of the structure; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryRead<T>(Slice source, out T value) where T : struct
		{
			return MemoryMarshal.TryRead<T>(source.Span, out value);
		}


		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="source">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		[Pure]
		public static unsafe Slice Copy(IntPtr source, int count)
		{
			return Slice.FromBytes(new ReadOnlySpan<byte>(source.ToPointer(), count));
		}

		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="source">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		[Pure]
		public static unsafe Slice Copy(void* source, int count)
		{
			return Slice.FromBytes(new ReadOnlySpan<byte>(source, count));
		}

		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="source">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		[Pure]
		public static unsafe Slice Copy(byte* source, int count)
		{
			return Slice.FromBytes(new ReadOnlySpan<byte>(source, count));
		}

		/// <summary>Copy this slice into memory and return the advanced cursor</summary>
		/// <param name="buffer">Slice to copy</param>
		/// <param name="ptr">Pointer where to copy this slice</param>
		/// <param name="end">Pointer to the next byte after the last available position in the output buffer</param>
		/// <remarks>Copy will fail if there is not enough space in the output buffer (ie: if it would write at or after <paramref name="end"/>)</remarks>
		public static unsafe byte* CopyTo(Slice buffer, byte* ptr, byte* end)
		{
			if (ptr is null | end is null)
			{
				throw new ArgumentNullException(ptr is null ? nameof(ptr) : nameof(end));
			}

			if (!buffer.Span.TryCopyTo(new Span<byte>(ptr, (int) Math.Min(end - ptr, int.MaxValue))))
			{
				throw UnsafeHelpers.Errors.SliceBufferTooSmall();
			}

			return ptr + buffer.Count;
		}

		/// <summary>Try to copy this slice into memory and return the advanced cursor, if the destination is large enough</summary>
		/// <param name="buffer">Slice to copy</param>
		/// <param name="ptr">Pointer where to copy this slice</param>
		/// <param name="end">Pointer to the next byte after the last available position in the output buffer</param>
		/// <returns>Pointer to the advanced memory position, or null if the destination buffer was too small</returns>
		[return:MaybeNull]
		public static unsafe byte* TryCopyTo(Slice buffer, byte* ptr, byte* end)
		{
			if (ptr is null | end is null)
			{
				throw new ArgumentNullException(ptr is null ? nameof(ptr) : nameof(end));
			}

			return buffer.Span.TryCopyTo(new Span<byte>(ptr, (int) Math.Min(end - ptr, int.MaxValue)))
				? ptr + buffer.Count
				: null;
		}

		/// <summary>Copy this slice into memory and return the advanced cursor</summary>
		/// <param name="buffer">Slice to copy</param>
		/// <param name="destination">Pointer where to copy this slice</param>
		/// <param name="capacity">Capacity of the output buffer</param>
		/// <remarks>Copy will fail if there is not enough space in the output buffer</remarks>
		public static IntPtr CopyTo(Slice buffer, IntPtr destination, nuint capacity)
		{
			unsafe
			{
				if (!buffer.Span.TryCopyTo(new Span<byte>(destination.ToPointer(), checked((int) capacity))))
				{
					throw UnsafeHelpers.Errors.SliceBufferTooSmall();
				}
			}
			return IntPtr.Add(destination, buffer.Count);
		}

		/// <summary>Copy this slice into memory and return the advanced cursor</summary>
		/// <param name="buffer">Slice to copy</param>
		/// <param name="destination">Pointer where to copy this slice</param>
		/// <param name="capacity">Capacity of the output buffer</param>
		/// <return>Updated pointer after the copy, of <see cref="IntPtr.Zero"/> if the destination buffer was too small</return>
		public static bool TryCopyTo(Slice buffer, IntPtr destination, nuint capacity)
		{
			unsafe
			{
				return buffer.Span.TryCopyTo(new Span<byte>(destination.ToPointer(), checked((int) capacity)));
			}
		}

	}

}
