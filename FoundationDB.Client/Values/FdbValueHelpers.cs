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

namespace FoundationDB.Client
{

	internal static class FdbValueHelpers
	{

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static NotSupportedException ErrorCannotComputeHashCodeMessage() => new("It is not possible to compute a stable hash code on values without doing more work than calling Equals(...), which defeats the purpose. Consider converting the value into a Slice instead.");

		/// <summary>Creates a pre-encoded version of a value that can be reused multiple times</summary>
		/// <typeparam name="TValue">Type of the value to pre-encode</typeparam>
		/// <param name="value">value to pre-encoded</param>
		/// <returns>Value with a cached version of the encoded original</returns>
		/// <remarks>This value can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue Memoize<TValue>(in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (typeof(TValue) == typeof(FdbRawValue))
			{ // already cached!
				return Unsafe.As<TValue, FdbRawValue>(ref Unsafe.AsRef(in value));
			}

			if (typeof(TValue) == typeof(Slice))
			{ // we still need to copy the slice
				return new(Unsafe.As<TValue, Slice>(ref Unsafe.AsRef(in value)).Copy());
			}

			if (typeof(TValue).IsAssignableTo(typeof(IFdbValue)))
			{ // we need to also copy the type hint
				return new(ToSlice(in value), Unsafe.As<TValue, IFdbValue>(ref Unsafe.AsRef(in value)).GetTypeHint());
			}

			return new(ToSlice(in value));
		}

		/// <summary>Encodes this value into <see cref="Slice"/></summary>
		/// <param name="value">Value to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TValue>(in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (typeof(TValue) == typeof(FdbRawValue))
			{
				return Unsafe.As<TValue, FdbRawValue>(ref Unsafe.AsRef(in value)).Data;
			}

			if (typeof(TValue) == typeof(Slice))
			{
				return Unsafe.As<TValue, Slice>(ref Unsafe.AsRef(in value));
			}

			if (value.TryGetSpan(out var span))
			{
				return Slice.FromBytes(span);
			}

			return ToSliceSlow(in value);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Slice ToSliceSlow(in TValue value)
			{
				byte[]? tmp = null;
				if (value.TryGetSizeHint(out var capacity))
				{
					// we will hope for the best, and pre-allocate the slice

					tmp = new byte[capacity];
					if (value.TryEncode(tmp, out var bytesWritten))
					{
						return tmp.AsSlice(0, bytesWritten);
					}

					if (capacity >= FdbValue.MaxSize)
					{
						goto key_too_long;
					}

					capacity *= 2;
				}
				else
				{
					capacity = 256;
				}

				var pool = ArrayPool<byte>.Shared;
				try
				{
					while (true)
					{
						tmp = pool.Rent(capacity);
						if (value.TryEncode(tmp, out int bytesWritten))
						{
							return tmp.AsSlice(0, bytesWritten).Copy();
						}

						pool.Return(tmp);
						tmp = null;

						if (capacity >= FdbValue.MaxSize)
						{
							goto key_too_long;
						}

						capacity *= 2;
					}
				}
				catch (Exception)
				{
					if (tmp is not null)
					{
						pool.Return(tmp);
					}

					throw;
				}

			key_too_long:
				// it would be too large anyway!
				throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
			}
		}

		/// <summary>Encodes this value into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="value">Value to encode</param>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this value</returns>
		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToSlice<TValue>(in TValue value, ArrayPool<byte>? pool)
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
#endif
		{
			pool ??= ArrayPool<byte>.Shared;

			if (typeof(TValue) == typeof(FdbRawValue))
			{
				return SliceOwner.Wrap(Unsafe.As<TValue, FdbRawValue>(ref Unsafe.AsRef(in value)).Data);
			}

			return value.TryGetSpan(out var span)
				? SliceOwner.Copy(span, pool)
				: Encode(in value, pool);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static SliceOwner Encode<TValue>(in TValue value, ArrayPool<byte> pool, int? sizeHint = null)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			Contract.Debug.Requires(pool is not null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!value.TryGetSizeHint(out capacity))
			{
				capacity = 128;
			}
			if (capacity <= 0)
			{
				capacity = 256;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (value.TryEncode(tmp, out int bytesWritten))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							return SliceOwner.Empty;
						}

						return SliceOwner.Create(tmp.AsSlice(0, bytesWritten), pool);
					}

					pool.Return(tmp);
					tmp = null;

					if (capacity >= FdbValue.MaxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
					}
					capacity *= 2;
				}
			}
			catch(Exception)
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				throw;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static SliceOwner Encode(IFdbValue value, ArrayPool<byte> pool, int? sizeHint = null)
		{
			Contract.Debug.Requires(pool is not null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!value.TryGetSizeHint(out capacity))
			{
				capacity = 128;
			}
			if (capacity <= 0)
			{
				capacity = 256;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (value.TryEncode(tmp, out int bytesWritten))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							return SliceOwner.Empty;
						}

						return SliceOwner.Create(tmp.AsSlice(0, bytesWritten), pool);
					}

					pool.Return(tmp);
					tmp = null;

					if (capacity >= FdbValue.MaxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
					}
					capacity *= 2;
				}
			}
			catch(Exception)
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				throw;
			}
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		internal static ReadOnlySpan<byte> Encode<TValue>(scoped in TValue value, scoped ref byte[]? buffer, ArrayPool<byte> pool)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			Contract.Debug.Requires(pool is not null);

			if (!value.TryGetSizeHint(out int capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 256;
			}

			while (true)
			{
				if (buffer is null)
				{
					buffer = pool.Rent(capacity);
				}
				else if (buffer.Length < capacity)
				{
					pool.Return(buffer);
					buffer = pool.Rent(capacity);
				}

				if (value.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSpan(0, bytesWritten) : default;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}

		public static bool AreEqual<TValue>(in TValue value, ReadOnlySpan<byte> other)
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
#endif
		{
			if (value.TryGetSpan(out var span))
			{
				return other.SequenceEqual(span);
			}

			using var valueBytes = Encode(in value, ArrayPool<byte>.Shared);
			return valueBytes.Span.SequenceEqual(other);
		}

		public static bool AreEqual<TValue, TOtherValue>(in TValue value, in TOtherValue other)
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
			where TOtherValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
			where TOtherValue : struct, IFdbValue
#endif
		{
			if (value.TryGetSpan(out var span))
			{
				return other.Equals(span);
			}

			if (other.TryGetSpan(out span))
			{
				return value.Equals(span);
			}

			using var valueBytes = Encode(in value, ArrayPool<byte>.Shared);
			using var otherBytes = Encode(in other, ArrayPool<byte>.Shared);
			return valueBytes.Span.SequenceEqual(otherBytes.Span);
		}

		public static bool AreEqual<TValue>(in TValue value, IFdbValue other)
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
#endif
		{
			if (value.TryGetSpan(out var span))
			{
				return other.Equals(span);
			}

			if (other.TryGetSpan(out span))
			{
				return value.Equals(span);
			}

			using var valueBytes = Encode(in value, ArrayPool<byte>.Shared);
			using var otherBytes = Encode(other, ArrayPool<byte>.Shared);
			return valueBytes.Span.SequenceEqual(otherBytes.Span);
		}

	}

}
