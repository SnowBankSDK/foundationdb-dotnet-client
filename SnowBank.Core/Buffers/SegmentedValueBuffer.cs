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

#if NET8_0_OR_GREATER

namespace SnowBank.Buffers
{
	using System.Buffers;
	using System.ComponentModel;
	using System.Runtime.InteropServices;

	/// <summary>Buffer that will accumulate data in a contiguous span, starting from a stack allocated buffer, and switching to pooled buffers if required</summary>
	/// <typeparam name="T">Type of elements stored in the buffer</typeparam>
	/// <remarks>
	/// <para>The final list of items will be available as a single contiguous <see cref="Span{T}"/></para>
	/// <para>If the caller does not need to consume the items as a single span, <see cref="SegmentedValueBuffer{T}"/> may be faster</para>
	/// </remarks>
	[DebuggerDisplay("Count={Count}, Capacity{Capacity}, Depth={Depth}")]
	[DebuggerTypeProxy(typeof(SegmentedValueBufferDebugView<>))]
	[PublicAPI]
	public ref struct SegmentedValueBuffer<T>
#if NET9_0_OR_GREATER
		: IDisposable
#endif
	{

		#region Constants...

		/// <summary>Size of the initial scratch</summary>
		private const int DefaultScratchSize = 8;

		/// <summary>Number of doublings, starting from 8, to reach the maximum array length</summary>
		private const int MaxSegments = 27;

		#endregion

		/// <summary>Initializes an empty buffer, using the supplied scratch as initial space</summary>
		/// <param name="scratch">Scratch used, until more space is required</param>
		/// <param name="pool">Pool used to rent additional segments (or <see cref="ArrayPool{T}.Shared"/>)</param>
		/// <remarks>This instance <b>MUST</b> be disposed, otherwise any rented buffers will <b>NOT</b> be returned to the pool!</remarks>
		public SegmentedValueBuffer(Span<T> scratch, ArrayPool<T>? pool = null)
		{
			this.Initial = scratch;
			this.Current = scratch;
			this.Pool = pool ?? ArrayPool<T>.Shared;
		}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary><c>YOU MUST PROVIDE AN INITIAL SCRATCH SPACE!</c></summary>
		[Obsolete("You must specify an initial scratch buffer", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public SegmentedValueBuffer() { }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		#region Fields...

		/// <summary>Initial buffer provided by the caller</summary>
		/// <remarks>Usually a buffer of size 8, but could be larger.</remarks>
		private readonly Span<T> Initial;

		/// <summary>Current buffer</summary>
		/// <remarks>When <see cref="Depth"/> is 0, this is the same as <see cref="Initial"/>. Otherwise, this is the last rented buffer which corresponds to the tail of the list</remarks>
		private Span<T> Current;

		/// <summary>Number of items in the <see cref="Current"/> buffer</summary>
		private int CountInCurrent;

		/// <summary>Number of items added to previous segments, excluding the ones in the current buffer</summary>
		private int CountInOthers;
		//note: this could be derived from Depth, but it seems a bit faster to cache this value

		/// <summary>Number of segments allocated in <see cref="Segments"/></summary>
		/// <remarks>
		/// <para>If equal to <see langword="0"/>, then the <see cref="Initial"/> buffer is still in used, and nothing has been rented from the pool</para>
		/// <para>If greater than <see langword="0"/>, then <see cref="Current"/> points to the last rented buffer from the pool</para>
		/// <para>If equal to <see cref="MaxSegments"/>, then the buffer has reached max capacity and cannot be expanded anymore.</para>
		/// </remarks>
		private int Depth;

		/// <summary>Pool used to rent additional segments</summary>
		private ArrayPool<T> Pool;

		/// <summary>Additional segments, rented from <see cref="Pool"/></summary>
		private SegmentStack Segments;

		#endregion

		#region Nested Types...

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0044 // Add readonly modifier

		/// <summary>Helper type for quickly allocating space on the heap</summary>
		//TODO: replace with InlineArray8<T> ?
		[InlineArray(DefaultScratchSize)]
		public struct Scratch
		{
			private T Item;
		}

		[InlineArray(MaxSegments)]
		private struct SegmentStack
		{

			// ReSharper disable once CollectionNeverUpdated.Local
			private T[]? Item;

		}

#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore IDE0051 // Remove unused private members

		#endregion

		/// <summary>Returns the number of items that have been added to the buffer</summary>
		public readonly int Count => checked(this.CountInCurrent + this.CountInOthers);

		/// <summary>Returns the current capacity of the buffer</summary>
		public readonly int Capacity => checked(this.CountInOthers + this.Current.Length);

		/// <summary>Appends an item to the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(T item)
		{
			var current = this.Current;
			var countInCurrent = this.CountInCurrent;
			if (countInCurrent < current.Length)
			{
				current[countInCurrent] = item;
				this.CountInCurrent = countInCurrent + 1;
				return;
			}

			AddWithResize(item);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AddWithResize(T item)
		{
			Expand();

			this.Current[0] = item;
			this.CountInCurrent = 1;
		}

		/// <summary>Appends a range of items to the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddRange(scoped ReadOnlySpan<T> items)
		{
			while (items.Length > 0)
			{
				var countInCurrent = this.CountInCurrent;
				var remaining = this.Current.Length - countInCurrent;

				// is the current buffer large enough?
				if (remaining >= items.Length)
				{ // yes, fill it, and return

					items.CopyTo(this.Current[countInCurrent..]);
					this.CountInCurrent = countInCurrent + items.Length;
					return;
				}

				// no, fill what we can and expand to a new segment
				items[..remaining].CopyTo(this.Current[countInCurrent..]);
				this.CountInCurrent += remaining;

				items = items[remaining..];

				Expand();
			}
		}

		/// <summary>Appends an array of items to the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddRange(T[]? items)
		{
			if (items != null)
			{
				AddRange(new ReadOnlySpan<T>(items));
			}
		}

		/// <summary>Appends a sequence of items to the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddRange(IEnumerable<T>? items)
		{
			if (items is null)
			{
				return;
			}

			if (items.TryGetSpan(out var span))
			{
				AddRange(span);
				return;
			}

			foreach (var item in items)
			{
				Add(item);
			}
		}

		/// <summary>Clears the content of the buffer, so that it can be reused immediately</summary>
		public void Clear()
		{
			if (this.Depth > 0)
			{
				ReleaseSegments(this.Depth);
			}
			this.CountInCurrent = 0;
			this.CountInOthers = 0;
			this.Depth = 0;
			this.Current = this.Initial;
		}

		private void Expand()
		{
			// how the new segment size is computed:
			// - the rented size will always be a power of two, which is twice the previous size
			// - we assume that the initial capacity is 8
			// - if Depth == 0, this is the first resize, we should have used a scratch of length 8, so we should rent a segment of length 16, giving a total capacity of 8 + 16 = 24
			// - if Depth == 1, this is the second resize, we should have 8 in the initial scratch, 16 in the first segment (24 total), so we should rent a segment of length 32, giving a total capacity of 24 + 32 = 56
			// - if Depth == 2, .... rent a segment of length 64 to give a total capacity of 56 + 64 = 120

			// This gives us the following formulas:
			// - size of rented buffer when at depth D: rentedSize(x) = 2 ^ (x + 4).
			// - total capacity when at depth D: totalCapacity(x) = (2 ^ (x + 5) - 16) + Initial.Length

			var depth = Depth;
			if ((uint) depth >= SegmentedValueBuffer<T>.MaxSegments)
			{
				throw new InvalidOperationException("Cannot resize the buffer, because it would exceed the maximum length allowed for an array");
			}

			int newSize = 1 << (depth + 4);
			var next = this.Pool.Rent(newSize);

			this.Segments[depth] = next;
			this.Depth = depth + 1;
			this.Current = next;
			this.CountInOthers += CountInCurrent;
			this.CountInCurrent = 0;
		}

		private void ReleaseSegments(int depth)
		{
			Contract.Debug.Requires(depth > 0);
			Span<T[]> segments = this.Segments!;

			// if T contains at least one ref type, we have to clear it before returning to the pool
			// if not, we don't need to clear at all

			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				// clear and release full segments
				if (depth > 1)
				{
					foreach (var segment in segments[..(depth - 1)])
					{
						Array.Clear(segment);
						this.Pool.Return(segment);
					}
				}

				// clear and release last segment (may not be full)
				this.Current[..CountInCurrent].Clear();
				this.Pool.Return(segments[depth - 1]);
			}
			else
			{
				// return all segments, without clearing
				foreach (var segment in segments[..depth])
				{
					this.Pool.Return(segment);
				}
			}
		}

		/// <summary>Indicates if the buffer content does fit in a single consecutive span</summary>
		public readonly bool IsSingleSegment => this.Depth == 0;

		/// <summary>Returns the content of the buffer, if it fits in a single consecutive span</summary>
		public readonly bool TryGetSpan(out Span<T> span)
		{
			if (this.Depth == 0)
			{
				span = this.Current[..this.CountInCurrent];
				return true;
			}

			span = default;
			return false;
		}

		/// <summary>Returns the content of the buffer as an array</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public readonly T[] ToArray()
		{
			if (this.Depth != 0)
			{
				return ToArrayMultipleSegments();
			}

			var res = new T[this.CountInCurrent];
			this.Current[..this.CountInCurrent].CopyTo(res);
			return res;
		}

		private readonly T[] ToArrayMultipleSegments()
		{
			Contract.Debug.Requires(this.Depth > 0);

			// copy the initial segment
			var res = new T[this.Count];
			this.Initial.CopyTo(res);

			var depth = this.Depth;
			Span<T> span = res.AsSpan(this.Initial.Length);
			for (int i = 0; i < depth - 1; i++)
			{
				var segment = this.Segments[i]!;
				segment.CopyTo(span);
				span = span[segment.Length..];
			}
			if (span.Length != this.CountInCurrent)
			{
				throw new InvalidOperationException();
			}
			this.Current[..this.CountInCurrent].CopyTo(span);

			return res;
		}

		/// <summary>Returns the content of the buffer as an array, and clears it for immediate re-use</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>
		/// <para>After this call, the buffer can be used again for the next batch of item</para>
		/// <para>If the buffer is only used once, it is faster to call <see cref="ToArray()"/> and the <see cref="Dispose"/></para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public T[] ToArrayAndClear()
		{
			var items = ToArray();
			Clear();
			return items;
		}

		/// <summary>Returns the content of the buffer as a list</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public readonly List<T> ToList()
		{
			if (this.Depth != 0)
			{
				return ToListMultipleSegments();
			}

			var res = new List<T>();
			CollectionsMarshal.SetCount(res, this.CountInCurrent);
			this.Current[..this.CountInCurrent].CopyTo(CollectionsMarshal.AsSpan(res));
			return res;
		}

		private readonly List<T> ToListMultipleSegments()
		{
			Contract.Debug.Requires(this.Depth > 0);

			// copy the initial segment
			var res = new List<T>(this.Count);
			res.AddRange(this.Initial);

			var depth = this.Depth;

			for (int i = 0; i < depth - 1; i++)
			{
				res.AddRange(this.Segments[i]!);
			}

			res.AddRange(this.Current[..this.CountInCurrent]);

			return res;
		}

		/// <summary>Returns the content of the buffer as a list</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>
		/// <para>After this call, the buffer can be used again for the next batch of item</para>
		/// <para>If the buffer is only used once, it is faster to call <see cref="ToList()"/> and the <see cref="Dispose"/></para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public List<T> ToListAndClear()
		{
			var items = ToList();
			Clear();
			return items;
		}

		/// <summary>Copies the content of the buffer into a destination span</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public readonly int CopyTo(Span<T> destination)
		{
			if (!TryCopyTo(destination, out int written))
			{
				throw new ArgumentException("Destination buffer is too small", nameof(destination));
			}
			return written;
		}

		/// <summary>Copies the content of the buffer into a destination span, if it is large enough</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public readonly bool TryCopyTo(Span<T> destination, out int written)
		{
			if (this.Depth != 0)
			{
				return TryCopyToMultipleSegments(destination, out written);
			}

			if (!this.Current[..this.CountInCurrent].TryCopyTo(destination))
			{
				written = 0;
				return false;
			}

			written = this.CountInCurrent;
			return true;
		}

		private readonly bool TryCopyToMultipleSegments(Span<T> destination, out int written)
		{
			var span = destination;

			if (!this.Initial.TryCopyTo(span))
			{
				goto too_small;
			}
			span = span[this.Initial.Length..];

			var depth = this.Depth;
			for (int i = 0; i < depth - 1; i++)
			{
				var segment = this.Segments[i]!;
				if (!segment.AsSpan().TryCopyTo(span))
				{
					goto too_small;
				}
				span = span[segment.Length..];
			}

			if (!this.Current[..this.CountInCurrent].TryCopyTo(span))
			{
				goto too_small;
			}
			span = span[this.CountInCurrent..];

			written = destination.Length - span.Length;
			return true;

		too_small:
			written = 0;
			return false;
		}

		/// <summary>Copies the content of the buffer into a destination <see cref="IBufferWriter{T}"/></summary>
		public readonly void CopyTo(IBufferWriter<T> writer)
		{
			if (this.Depth != 0)
			{
				CopyToMultipleSegments(writer);
				return;
			}

			var count = this.CountInCurrent;
			var span = writer.GetSpan(count);
			this.Current[..count].CopyTo(span);
			writer.Advance(count);
		}

		private readonly void CopyToMultipleSegments(IBufferWriter<T> writer)
		{
			Contract.Debug.Requires(this.Depth > 0);

			// initial segment (full)
			var span = writer.GetSpan(this.Initial.Length);
			this.Initial.CopyTo(span);
			writer.Advance(this.Initial.Length);

			// completed segments
			int depth = this.Depth - 1;
			for (int i = 0; i < depth; i++)
			{
				var segment = this.Segments[i]!;
				span = writer.GetSpan(segment.Length);
				segment.CopyTo(span);
				writer.Advance(segment.Length);
			}

			// last segment (may not be full)
			var count = this.CountInCurrent;
			span = writer.GetSpan(count);
			this.Current[..count].CopyTo(span);
			writer.Advance(count);
		}

		#region IReadOnlyList<T>...

		/// <summary>Returns a reference to the item at the given index</summary>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public ref T this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get
			{
				if (this.Depth == 0)
				{ // we are in the last segment

					if ((uint) index >= (uint) this.CountInCurrent)
					{
						ThrowHelper.ThrowIndexOutOfRangeException();
					}
					return ref this.Current[index];
				}

				if (index < this.Initial.Length)
				{
					return ref this.Initial[index];
				}
				index -= this.Initial.Length;
				int depth = this.Depth - 1;
				for (int i = 0; i < depth; i++)
				{
					var segment = this.Segments[i]!;
					if (index < segment.Length)
					{
						return ref segment[index];
					}
					index -= segment.Length;
				}

				return ref Current[..this.CountInCurrent][index];
			}
		}

		/// <summary>Enumerates the values inserted into to a <see cref="SegmentedValueBuffer{T}"/></summary>
		[UnscopedRef]
		public Enumerator GetEnumerator()
		{
			switch (this.Depth)
			{
				case 0:
				{
					return new(this.Initial[..this.CountInCurrent], default, default);
				}
				case 1:
				{
					return new(this.Initial, default, this.Current[..this.CountInCurrent]);
				}
				default:
				{
					Span<T[]?> segments = this.Segments;
					return new(this.Initial, segments[..(this.Depth - 1)]!, this.Current[..this.CountInCurrent]);
				}
			}
		}

		/// <summary>Enumerates the contents of a <see cref="SegmentedValueBuffer{T}"/></summary>
		public ref struct Enumerator
#if NET9_0_OR_GREATER
			: IEnumerator<T>
#endif
		{
			private ReadOnlySpan<T> Segment;
			private int Offset;
			private int Index;
			private readonly ReadOnlySpan<T> First;
			private readonly ReadOnlySpan<T[]> Segments;
			private readonly ReadOnlySpan<T> Last;

			internal Enumerator(ReadOnlySpan<T> first, ReadOnlySpan<T[]> segments, ReadOnlySpan<T> last)
			{
				this.Segment = first;
				this.Offset = -1;
				this.Index = -1;
				this.First = first;
				this.Segments = segments;
				this.Last = last;
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				var offset = this.Offset + 1;
				if (offset < this.Segment.Length)
				{
					this.Offset = offset;
					return true;
				}

				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				var next = this.Index + 1;
				if (next < this.Segments.Length)
				{
					this.Segment = this.Segments[next];
					this.Offset = 0;
					this.Index = next;
					return true;
				}

				if (next == this.Segments.Length && this.Last.Length != 0)
				{ // last
					this.Segment = this.Last;
					this.Offset = 0;
					this.Index = next;
					return true;
				}

				// no more segments
				this.Index = this.Segments.Length + 1;
				this.Offset = -1;
				return false;
			}

			/// <inheritdoc />
			public void Reset()
			{
				this.Index = -1;
				this.Offset = -1;
				this.Segment = this.First;
			}

			/// <inheritdoc />
			public T Current => this.Segment[this.Offset];

#if NET9_0_OR_GREATER

			/// <inheritdoc />
			object? System.Collections.IEnumerator.Current => this.Segment[this.Offset];

#endif

			/// <inheritdoc />
			public void Dispose()
			{
				this = default;
			}

		}

		#endregion

		/// <inheritdoc />
		public override string ToString()
		{
			return $"{nameof(SegmentedValueBuffer<>)}<{typeof(T).Name}>[{this.Count}]";
		}

		/// <summary>Release any rented buffer used by this instance</summary>
		/// <remarks>It is <b>NOT</b> same to continue using this buffer after Dispose has been called at once!</remarks>
		public void Dispose()
		{
			var depth = this.Depth;
			if (depth > 0)
			{
				ReleaseSegments(depth);
			}
		}
	}

	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	internal class SegmentedValueBufferDebugView<T>
	{
		public SegmentedValueBufferDebugView(ValueBuffer<T> buffer)
		{
			this.Items = buffer.ToArray();
		}

		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public T[] Items { get; set; }

	}

}

#endif
