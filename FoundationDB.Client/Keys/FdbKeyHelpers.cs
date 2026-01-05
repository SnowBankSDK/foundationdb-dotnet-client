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

	/// <summary>Helper methods used by <see cref="IFdbKey"/> implementations</summary>
	internal static class FdbKeyHelpers
	{

		internal const string PreferFastEqualToForKeysMessage = "When comparing two different types of keys, use left." + nameof(IFdbKey.FastEqualTo) + "(right) instead, to prevent unnecessary boxing!";

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static NotSupportedException ErrorCannotComputeHashCodeMessage() => new("It is not possible to compute a stable hash code on keys without doing more work than calling Equals(...), which defeats the purpose. Consider converting the key into a Slice instead.");

		/// <summary>Returns a pre-encoded version of a key</summary>
		/// <typeparam name="TKey">Type of the key to pre-encode</typeparam>
		/// <param name="key">Key to pre-encoded</param>
		/// <returns>Key with a cached version of the encoded original</returns>
		/// <remarks>This key can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey Memoize<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{ // already cached!
				return (FdbRawKey) (object) key;
			}

			return new(key.ToSlice());
		}

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		public static bool IsSystem<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbSystemKey))
			{
				return true;
			}

			// if the key is complete, 
			if (key.TryGetSpan(out var span))
			{
				return span.Length != 0 && span[0] == 0xFF;
			}

			// check the subspace prefix (rare, but could happen)
			var subspace = key.GetSubspace();
			if (subspace is not null && subspace.TryGetSpan(out span) && span.Length > 0)
			{
				return span[0] == 0xFF;
			}

			// we have to render the key, unfortunately
			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Data.StartsWith((byte) 0xFF);
		}

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		public static bool IsSpecial<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbSystemKey))
			{
				return ((FdbSystemKey) (object) key).IsSpecial;
			}

			// if the key is complete, 
			if (key.TryGetSpan(out var span))
			{
				return span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFF;
			}

			// check the subspace prefix (rare, but could happen)
			var subspace = key.GetSubspace();
			if (subspace is not null && subspace.TryGetSpan(out span) && span.Length >= 2)
			{
				return span[0] == 0xFF && span[1] == 0xFF;
			}

			// we have to render the key, unfortunately
			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Data.StartsWith([ 0xFF, 0xFF ]);
		}

		/// <summary>Compares the prefix of two subspaces for equality</summary>
		public static bool AreEqual(IKeySubspace? subspace, IKeySubspace? other)
		{
			return (subspace ?? KeySubspace.Empty).Equals(other ?? KeySubspace.Empty);
		}

		/// <summary>Compares the prefix of two subspaces</summary>
		public static int Compare(IKeySubspace? subspace, IKeySubspace? other)
		{
			return (subspace ?? KeySubspace.Empty).CompareTo(other ?? KeySubspace.Empty);
		}

		/// <inheritdoc cref="Compare{TKey}(in TKey,System.ReadOnlySpan{byte})"/>
		public static int Compare<TKey>(in TKey key, Slice expectedBytes)
			where TKey : struct, IFdbKey
			=> !expectedBytes.IsNull ? Compare(in key, expectedBytes.Span) : +1;

		/// <summary>Compares a key with a specific encoded binary representation</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key being compared</param>
		/// <param name="expectedBytes">Encoded bytes to compare with</param>
		/// <returns><c>0</c> if the key encodes to the exact same bytes, a negative number if it would be sorted before, or a positive number if it would be sorted after</returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static int Compare<TKey>(in TKey key, ReadOnlySpan<byte> expectedBytes)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var span))
			{
				return span.SequenceCompareTo(expectedBytes);
			}

			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Span.SequenceCompareTo(expectedBytes);
		}

		/// <summary>Compares two keys by their encoded binary representation</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <typeparam name="TOtherKey">Type of the second key</typeparam>
		/// <param name="key">First key to compare</param>
		/// <param name="other">Second key to compare</param>
		/// <returns><c>0</c> if both keys are equal, a negative number if <paramref name="key"/> would be sorted before <paramref name="other"/>, or a positive number if <paramref name="key"/> would be sorted after <paramref name="other"/></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static int Compare<TKey, TOtherKey>(in TKey key, in TOtherKey other)
			where TKey : struct, IFdbKey
			where TOtherKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return Compare(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return Compare(in other, keySpan);
			}
			return CompareToIncompatible(in key, in other);

			static int CompareToIncompatible(in TKey key, in TOtherKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(in other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceCompareTo(otherBytes.Span);
			}
		}

		/// <summary>Compares two keys by their encoded binary representation</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <param name="key">First key to compare</param>
		/// <param name="other">Second key to compare</param>
		/// <returns><c>0</c> if both keys are equal, a negative number if <paramref name="key"/> would be sorted before <paramref name="other"/>, or a positive number if <paramref name="key"/> would be sorted after <paramref name="other"/></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static int Compare<TKey>(in TKey key, IFdbKey other)
			where TKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return Compare(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return other.CompareTo(keySpan);
			}
			return CompareToIncompatible(in key, other);

			static int CompareToIncompatible(in TKey key, IFdbKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceCompareTo(otherBytes.Span);
			}
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <typeparam name="TOtherKey">Type of the second key</typeparam>
		/// <param name="key">First key to test</param>
		/// <param name="other">Second key to test</param>
		/// <returns><c>true</c> if the both key encodes to the exact same bytes; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool AreEqual<TKey, TOtherKey>(in TKey key, in TOtherKey other)
			where TKey : struct, IFdbKey
			where TOtherKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return AreEqual(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return AreEqual(in other, keySpan);
			}
			return EqualsIncompatible(in key, in other);

			static bool EqualsIncompatible(in TKey key, in TOtherKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(in other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceEqual(otherBytes.Span);
			}
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <param name="key">First key to test</param>
		/// <param name="other">Second key to test</param>
		/// <returns><c>true</c> if the both key encodes to the exact same bytes; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool AreEqual<TKey>(in TKey key, IFdbKey other)
			where TKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return AreEqual(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return other.Equals(keySpan);
			}
			return EqualsIncompatible(in key, other);

			static bool EqualsIncompatible(in TKey key, IFdbKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceEqual(otherBytes.Span);
			}
		}

		/// <inheritdoc cref="AreEqual{TKey}(in TKey,System.ReadOnlySpan{byte})"/>
		public static bool AreEqual<TKey>(in TKey key, Slice expectedBytes)
			where TKey : struct, IFdbKey
			=> AreEqual(in key, expectedBytes.Span);

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key to test</param>
		/// <param name="expectedBytes">Expected encoded bytes</param>
		/// <returns><c>true</c> if the key encodes to the exact same bytes; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method may need to encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool AreEqual<TKey>(in TKey key, ReadOnlySpan<byte> expectedBytes)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var span))
			{
				return span.SequenceEqual(expectedBytes);
			}

			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Span.SequenceEqual(expectedBytes);
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TParentKey">Type of the parent key</typeparam>
		/// <param name="parent">Parent key</param>
		/// <param name="childBytes">Bytes of the encoded child key to test</param>
		/// <param name="excluded">If <c>true</c>, exclude the parent key itself.</param>
		/// <returns><c>true</c> if the encoded child key starts with the same bytes as the encoded parent key; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method may need to encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool IsChildOf<TParentKey>(in TParentKey parent, ReadOnlySpan<byte> childBytes, bool excluded = false)
			where TParentKey : struct, IFdbKey
		{
			if (parent.TryGetSpan(out var parentSpan))
			{
				return childBytes.StartsWith(parentSpan) && (!excluded || childBytes.Length > parentSpan.Length);
			}

			using var bytes = Encode(in parent, ArrayPool<byte>.Shared);
			return childBytes.StartsWith(bytes.Span) && (!excluded || childBytes.Length > bytes.Span.Length);
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TChildKey">Type of the potential child key</typeparam>
		/// <param name="parentBytes">Bytes of the encoded parent key</param>
		/// <param name="child">Child key to test</param>
		/// <param name="excluded">If <c>true</c>, exclude the parent key itself.</param>
		/// <returns><c>true</c> if the encoded child key starts with the same bytes as the encoded parent key; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method may need to encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool IsChildOf<TChildKey>(ReadOnlySpan<byte> parentBytes, in TChildKey child, bool excluded = false)
			where TChildKey : struct, IFdbKey
		{
			if (child.TryGetSpan(out var childSpan))
			{
				return childSpan.StartsWith(parentBytes) && (!excluded || childSpan.Length > parentBytes.Length);
			}

			using var childBytes = Encode(in child, ArrayPool<byte>.Shared);
			return childBytes.Span.StartsWith(parentBytes) && (!excluded || childBytes.Count > parentBytes.Length);
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TParentKey">Type of the parent key</typeparam>
		/// <typeparam name="TChildKey">Type of the potential child key</typeparam>
		/// <param name="parent">Parent key</param>
		/// <param name="child">Child key to test</param>
		/// <param name="excluded">If <c>true</c>, exclude the parent key itself.</param>
		/// <returns><c>true</c> if the encoded child key starts with the same bytes as the encoded parent key; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method may need to encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool IsChildOf<TParentKey, TChildKey>(in TParentKey parent, in TChildKey child, bool excluded = false)
			where TParentKey : struct, IFdbKey
			where TChildKey : struct, IFdbKey
		{
			if (child.TryGetSpan(out var childSpan))
			{
				return parent.Contains(childSpan);
			}

			if (parent.TryGetSpan(out var parentSpan))
			{
				using var childBytes = Encode(in child, ArrayPool<byte>.Shared);
				return childBytes.Span.StartsWith(parentSpan) && (!excluded || childBytes.Count > parentSpan.Length);
			}
			else
			{
				using var parentBytes = Encode(in parent, ArrayPool<byte>.Shared);
				using var childBytes = Encode(in child, ArrayPool<byte>.Shared);
				return childBytes.Span.StartsWith(parentBytes.Span) && (!excluded || childBytes.Count > parentBytes.Count);
			}
		}

		/// <summary>Encodes this key into <see cref="Slice"/></summary>
		/// <param name="key">Key to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this key</returns>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return ((FdbRawKey) (object) key).Data;
			}

			if (key.TryGetSpan(out var span))
			{
				return Slice.FromBytes(span);
			}

			return ToSliceSlow(in key);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Slice ToSliceSlow(in TKey key)
			{
				byte[]? tmp = null;
				if (key.TryGetSizeHint(out var capacity))
				{
					if (capacity <= 0)
					{
#if DEBUG
						// probably a bug in TryGetSizeHint which returned "true" instead of "false"!
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
						capacity = 16;
					}

					// we will hope for the best, and pre-allocate the slice
					tmp = new byte[capacity];
					if (key.TryEncode(tmp, out var bytesWritten))
					{
						return tmp.AsSlice(0, bytesWritten);
					}

					if (capacity >= FdbKey.MaxSize)
					{
						goto key_too_long;
					}

					capacity *= 2;
				}
				else
				{
					capacity = 128;
				}

				var pool = ArrayPool<byte>.Shared;
				try
				{
					while (true)
					{
						tmp = pool.Rent(capacity);
						if (key.TryEncode(tmp, out int bytesWritten))
						{
							return tmp.AsSlice(0, bytesWritten).Copy();
						}

						pool.Return(tmp);
						tmp = null;

						if (capacity >= FdbKey.MaxSize)
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
				throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
			}
		}

		/// <summary>Encodes this key into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="key">Key to encode</param>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToSlice<TKey>(in TKey key, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return SliceOwner.Wrap(((FdbRawKey) (object) key).Data);
			}

			pool ??= ArrayPool<byte>.Shared;
			return key.TryGetSpan(out var span)
				? SliceOwner.Copy(span, pool)
				: Encode(in key, pool);
		}

		/// <summary>Encodes a boxed key, using a pooled buffer</summary>
		/// <remarks>This method is less efficient than the generic implementations, and should only be used when there is no other solution.</remarks>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[MustDisposeResource, MethodImpl(MethodImplOptions.NoInlining)]
		public static SliceOwner Encode(IFdbKey key, ArrayPool<byte>? pool, int? sizeHint = null)
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (key.TryEncode(tmp, out int bytesWritten))
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

					if (capacity >= FdbKey.MaxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
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

		/// <summary>Encodes a key, using a pooled buffer</summary>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[MustDisposeResource, MethodImpl(MethodImplOptions.NoInlining)]
		public static SliceOwner Encode<TKey>(in TKey key, ArrayPool<byte>? pool, int? sizeHint = null)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (key.TryEncode(tmp, out int bytesWritten))
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

					if (capacity >= FdbKey.MaxSize)
					{
						// it would be too large anyway!
						throw new InvalidOperationException("Cannot encode key because it would exceed the maximum allowed length.");
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

		/// <summary>Encodes a key, using a pooled buffer</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		public static ReadOnlySpan<byte> Encode<TKey>(scoped in TKey key, scoped ref byte[]? buffer, ArrayPool<byte>? pool, int? sizeHint = null)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
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

				if (key.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSpan(0, bytesWritten) : default;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}

		/// <summary>Encodes a key, using a pooled buffer</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		public static Slice ToSlice<TKey>(scoped in TKey key, scoped ref byte[]? buffer, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			if (!key.TryGetSizeHint(out int capacity) || capacity <= 0)
			{
				capacity = 128;
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

				if (key.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSlice(0, bytesWritten) : Slice.Empty;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}

	}

}
