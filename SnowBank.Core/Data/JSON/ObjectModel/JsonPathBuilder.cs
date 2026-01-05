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

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.ComponentModel;
	using SnowBank.Runtime.Converters;

	[PublicAPI]
	public ref struct JsonPathBuilder
#if NET9_0_OR_GREATER
		: IDisposable
#endif
	{
		public const int MaxLength = 1_000_000;

		private Span<char> Chars;

		private int Cursor;

		private char[]? Buffer;

		public int Length
		{
			readonly get
			{
				return this.Cursor;
			}
			set
			{
				Contract.Positive(value);
				if (value > this.Cursor)
				{
					throw new InvalidOperationException("Cannot set length greater than the current cursor");
				}
				this.Chars[value..this.Cursor].Clear();
				this.Cursor = value;
			}
		}

		public readonly int Capacity => this.Chars.Length;

		public readonly Span<char> Span => this.Chars[..this.Cursor];

		[MustDisposeResource]
		public JsonPathBuilder(Span<char> scratch)
		{
			this.Chars = scratch;
			this.Cursor = 0;
			this.Buffer = null;
		}

		[MustDisposeResource]
		public JsonPathBuilder(int capacity)
		{
			Contract.Positive(capacity);
			var tmp = ArrayPool<char>.Shared.Rent(capacity);
			this.Chars = tmp;
			this.Cursor = 0;
			this.Buffer = tmp;
		}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary><c>YOU MUST PROVIDE AN INITIAL CAPACITY OR SCRATCH SPACE!</c></summary>
		[Obsolete("You must specify an initial capacity or scratch buffer", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public JsonPathBuilder() { }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		public void Dispose()
		{
			this.Chars = default;
			var buffer = this.Buffer;
			if (buffer != null)
			{
				this.Buffer = null;
				buffer.AsSpan(0, this.Cursor).Clear();
				ArrayPool<char>.Shared.Return(buffer);
			}
			this.Cursor = 0;
		}

		private Span<char> GetSpan(int minSize)
		{
			if ((uint) this.Cursor + (uint) minSize > (uint) this.Chars.Length)
			{
				Grow(minSize);
			}
			Contract.Debug.Assert(this.Cursor + minSize <= this.Chars.Length);

			return this.Chars[this.Cursor..];
		}

		private Span<char> AllocateBefore(int size)
		{
			if ((uint) this.Cursor + (uint) size > (uint) this.Chars.Length)
			{
				Grow(size);
			}
			Contract.Debug.Assert(this.Cursor + size <= this.Chars.Length);

			this.Chars[..this.Cursor].CopyTo(this.Chars[size..]);
			this.Cursor += size;
			return this.Chars[..size];
		}

		private void Advance(int length)
		{
			int newPos = this.Cursor + length;
			Contract.Debug.Assert(newPos <= this.Chars.Length);
			this.Cursor = newPos;
		}

		private void InsertBefore(scoped ReadOnlySpan<char> prefix)
		{
			if ((uint) this.Cursor + (uint) prefix.Length > (uint) this.Chars.Length)
			{
				Grow(prefix.Length);
			}

			this.Chars[..this.Cursor].CopyTo(this.Chars[prefix.Length..]);
			prefix.CopyTo(this.Chars);
			this.Cursor += prefix.Length;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Grow(int size)
		{
			// compute new capacity
			long minCapacity = (long) this.Cursor + size;
			if (minCapacity > JsonPathBuilder.MaxLength)
			{
				throw new OutOfMemoryException("Maximum size reached");
			}

			// powers of 2, until we reach the max allowed size
			long newCapacity = Math.Max(this.Chars.Length, 32);
			while (newCapacity < minCapacity)
			{
				newCapacity <<= 1;
			}
			newCapacity = Math.Min(JsonPathBuilder.MaxLength, newCapacity);

			// rent new larger buffer
			var tmp = ArrayPool<char>.Shared.Rent((int) newCapacity);

			// copy over the current content
			var prev = this.Chars[..this.Cursor];
			prev.CopyTo(tmp);

			// return previous buffer to the pool
			if (this.Buffer != null)
			{
				prev.Clear();
				ArrayPool<char>.Shared.Return(this.Buffer);
			}

			// use new buffer
			this.Buffer = tmp;
			this.Chars = tmp;
		}

		public void Append(in JsonPathSegment segment)
		{
			if (segment.TryGetName(out var name))
			{
				Append(name.Span);
			}
			else if (segment.TryGetIndex(out var index))
			{
				Append(index);
			}
			// else: empty!
		}

		public void Append(string name)
		{
			Contract.NotNull(name);
			Append(name.AsSpan());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Append(ReadOnlyMemory<char> name)
		{
			Append(name.Span);
		}

		public void Append(scoped ReadOnlySpan<char> name)
		{
			if (JsonPath.RequiresEscaping(name))
			{
				AppendEscaped(name);
			}
			else if (this.Cursor != 0)
			{ // ".Foo"
				var span = GetSpan(name.Length + 1);
				span[0] = '.';
				name.CopyTo(span[1..]);
				Advance(name.Length + 1);
			}
			else
			{ // "Foo"
				var span = GetSpan(name.Length);
				name.CopyTo(span);
				Advance(name.Length);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AppendEscaped(scoped ReadOnlySpan<char> name)
		{
			// the encoding may take 2x the string length
			int dot = this.Cursor == 0 ? 0 : 1;
			var span = GetSpan(checked(dot + (name.Length * 2)));
			if (dot != 0)
			{
				span[0] = '.';
				span = span[1..];
			}
			JsonPath.TryEncodeKeyNameTo(span, out int charsWritten, name);
			Advance(dot + charsWritten);
		}

		public void Append(int index)
		{
			Contract.Positive(index);

			// '[' + int + ']'
			var span = GetSpan(2 + StringConverters.Base10MaxCapacityInt32);
			span[0] = '[';
			index.TryFormat(span[1..], out var charsWritten);
			span[charsWritten + 1] = ']';
			Advance(charsWritten + 2);
		}

		public void Append(Index index)
		{
			if (index.IsFromEnd)
			{
				AppendFromEnd(index.Value);
			}
			else
			{
				Append(index.Value);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AppendFromEnd(int index)
		{
			// '[^' + int + ']'
			var span = GetSpan(3 + StringConverters.Base10MaxCapacityInt32);
			span[0] = '[';
			span[1] = '^';
			index.TryFormat(span[2..], out var charsWritten);
			span[charsWritten + 2] = ']';
			Advance(charsWritten + 3);
		}

		public void Prepend(scoped ReadOnlySpan<char> name)
		{
			if (this.Cursor == 0)
			{
				Append(name);
				return;
			}

			if (JsonPath.RequiresEscaping(name))
			{
				PrependEscaped(name);
			}
			else if (this.Chars[0] != '[')
			{ // ".Foo"
				var span = AllocateBefore(name.Length + 1);
				name.CopyTo(span);
				span[name.Length] = '.';
			}
			else
			{ // "Foo"
				var span = AllocateBefore(name.Length);
				name.CopyTo(span);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PrependEscaped(scoped ReadOnlySpan<char> name)
		{
			// the encoding may take 2x the string length
			var tmp = ArrayPool<char>.Shared.Rent(checked(1 + name.Length * 2));
			JsonPath.TryEncodeKeyNameTo(tmp, out int charsWritten, name);
			if (this.Chars[0] != '[')
			{
				tmp[charsWritten++] = '.';
			}

			InsertBefore(tmp.AsSpan(0, charsWritten));
			ArrayPool<char>.Shared.Return(tmp);
		}

		public void Prepend(int index)
		{
			if (this.Cursor == 0)
			{
				Append(index);
			}
			else
			{
				PrependFromStart(index);
			}
		}

		public void Prepend(Index index)
		{
			if (this.Cursor == 0)
			{
				Append(index);
			}
			else if (index.IsFromEnd)
			{
				PrependFromEnd(index.Value);
			}
			else
			{
				PrependFromStart(index.Value);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PrependFromStart(int index)
		{
			Contract.Positive(index);

			char first = this.Chars[0];

			Span<char> chars = stackalloc char[3 + StringConverters.Base10MaxCapacityInt32];
			chars[0] = '[';
			index.TryFormat(chars[1..], out var charsWritten);
			++charsWritten;
			chars[charsWritten++] = ']';
			if (first != '[')
			{
				chars[charsWritten++] = '.';
			}

			InsertBefore(chars[..charsWritten]);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PrependFromEnd(int index)
		{
			Contract.Positive(index);

			// '[^' + int + ']'
			Span<char> span = stackalloc char[4 + StringConverters.Base10MaxCapacityInt32];
			span[0] = '[';
			span[1] = '^';
			index.TryFormat(span[2..], out var charsWritten);
			charsWritten += 2;
			span[charsWritten++] = ']';
			if (this.Chars[0] != '[')
			{
				span[charsWritten++] = '.';
			}

			InsertBefore(span[..charsWritten]);
		}

		[Pure]
		public readonly override string ToString() => this.Chars[..this.Cursor].ToString();

		[Pure]
		public readonly JsonPath ToPath() => JsonPath.Create(this.Chars[..this.Cursor]);
	}

}
