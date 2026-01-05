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

namespace SnowBank.Buffers
{
	using System.Buffers;

	/// <summary><see cref="IBufferWriter{T}"/> that can also allocate <see cref="Slice"/> chunks</summary>
	public interface ISliceBufferWriter : IBufferWriter<byte>
	{
		/// <summary>Returns a <see cref="Span{T}" /> to write to that is at least the requested size (specified by <paramref name="sizeHint" />).</summary>
		/// <param name="sizeHint">The minimum length of the returned <see cref="Slice" />. If 0, a non-empty buffer is returned.</param>
		/// <exception cref="T:System.OutOfMemoryException">The requested buffer size is not available.</exception>
		/// <returns>A <see cref="Span{T}" /> of at least the size <paramref name="sizeHint" />. If <paramref name="sizeHint" /> is 0, returns a non-empty buffer.</returns>
		ArraySegment<byte> GetSlice(int sizeHint = 0);

	}

	/// <summary>Helper methods for working with slice allocators and writers</summary>
	public static class SliceBufferWriterExtensions
	{

		/// <summary>Returns a copy of a span of bytes using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <returns>New slice, with identical content.</returns>
		public static Slice Intern(this ISliceAllocator writer, ReadOnlySpan<byte> data)
		{
			Contract.NotNull(writer);
			if (data.Length == 0) return Slice.Empty;
			var tmp = writer.Allocate(data.Length);
			data.CopyTo(tmp.AsSpan());
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a span of bytes using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <param name="suffix">Extra bytes that will be copied after <paramref name="data"/></param>
		/// <returns>New slice, with identical content plus the extra suffix.</returns>
		public static Slice Intern(this ISliceAllocator writer, ReadOnlySpan<byte> data, Slice suffix)
		{
			Contract.NotNull(writer);
			if (data.Length == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : Slice.Empty;
			}
			var tmp = writer.Allocate(checked(data.Length + suffix.Count));
			data.CopyTo(tmp.AsSpan());
			suffix.CopyTo(tmp.AsSpan(data.Length));
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a span of bytes using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <param name="suffix">Extra byte that will be copied after <paramref name="data"/></param>
		/// <returns>New slice, with identical content plus the extra suffix.</returns>
		public static Slice Intern(this ISliceAllocator writer, ReadOnlySpan<byte> data, byte suffix)
		{
			Contract.NotNull(writer);
			if (data.Length == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return Slice.FromByte(suffix);
			}
			var tmp = writer.Allocate(data.Length + 1);
			data.CopyTo(tmp.AsSpan());
			tmp[data.Length] = suffix;
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a span of bytes using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <returns>New slice, with identical content.</returns>
		public static Slice Intern(this ISliceAllocator writer, ReadOnlyMemory<byte> data)
		{
			Contract.NotNull(writer);
			if (data.Length == 0) return Slice.Empty;
			var tmp = writer.Allocate(data.Length);
			data.Span.CopyTo(tmp.AsSpan());
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a span of bytes using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <param name="suffix">Extra bytes that will be copied after <paramref name="data"/></param>
		/// <returns>New slice, with identical content plus the extra suffix.</returns>
		public static Slice Intern(this ISliceAllocator writer, ReadOnlyMemory<byte> data, Slice suffix)
		{
			Contract.NotNull(writer);
			if (data.Length == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : Slice.Empty;
			}
			var tmp = writer.Allocate(checked(data.Length + suffix.Count));
			data.Span.CopyTo(tmp.AsSpan());
			suffix.CopyTo(tmp.AsSpan(data.Length));
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a span of bytes using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <param name="suffix">Extra byte that will be copied after <paramref name="data"/></param>
		/// <returns>New slice, with identical content plus the extra suffix.</returns>
		public static Slice Intern(this ISliceAllocator writer, ReadOnlyMemory<byte> data, byte suffix)
		{
			Contract.NotNull(writer);
			if (data.Length == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return Slice.FromByte(suffix);
			}
			var tmp = writer.Allocate(data.Length + 1);
			data.Span.CopyTo(tmp.AsSpan());
			tmp[data.Length] = suffix;
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a Slice, using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <returns>New slice, with identical content.</returns>
		public static Slice Intern(this ISliceAllocator writer, Slice data)
		{
			Contract.NotNull(writer);
			if (data.Count == 0) return data.IsNull ? Slice.Nil : Slice.Empty;
			var tmp = writer.Allocate(data.Count);
			data.CopyTo(tmp.AsSpan());
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a Slice, using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <param name="suffix">Extra bytes that will be copied after <paramref name="data"/></param>
		/// <returns>New slice, with identical content plus the extra suffix.</returns>
		public static Slice Intern(this ISliceAllocator writer, Slice data, Slice suffix)
		{
			Contract.NotNull(writer);
			if (data.Count == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : data.IsNull ? default : Slice.Empty;
			}
			var tmp = writer.Allocate(checked(data.Count + suffix.Count));
			data.CopyTo(tmp.AsSpan());
			suffix.CopyTo(tmp.AsSpan(data.Count));
			return tmp.AsSlice();
		}

		/// <summary>Returns a copy of a Slice, using this allocator to allocate memory.</summary>
		/// <param name="writer">Allocator that will store the bytes</param>
		/// <param name="data">Bytes to copy</param>
		/// <param name="suffix">Extra byte that will be copied after <paramref name="data"/></param>
		/// <returns>New slice, with identical content plus the extra suffix.</returns>
		public static Slice Intern(this ISliceAllocator writer, Slice data, byte suffix)
		{
			Contract.NotNull(writer);
			if (data.Count == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return Slice.FromByte(suffix);
			}
			var tmp = writer.Allocate(data.Count + 1);
			data.CopyTo(tmp.AsSpan());
			tmp[data.Count] = suffix;
			return tmp.AsSlice();
		}

	}

}
