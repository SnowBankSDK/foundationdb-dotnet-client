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
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>Dictionary that can look up tokens either via their <typeparamref name="TLiteral"/> representation (ex: <c>string</c>), or via their representation as a <see cref="ReadOnlySpan{TRune}"/> (ex: <c>ReadOnlySpan&lt;char&gt;</c>)</summary>
	/// <typeparam name="TValue">Type of the stored tokens</typeparam>
	/// <typeparam name="TLiteral">Type of the "managed" representation (ex: string)</typeparam>
	/// <typeparam name="TRune">Type of the elements that make up the literal (ex: char)</typeparam>
	[DebuggerDisplay("Count={Size}")]
#if NET9_0_OR_GREATER
	[Obsolete("Use Dictionary<TKey, TValue>.AlternateLookup<TSpan> (.NET 9+) instead.", error: true)]
#endif
	public abstract class TokenDictionary<TValue, TLiteral, TRune>
		where TLiteral : IEquatable<TLiteral>
		where TRune : struct, IEquatable<TRune>
	{
		//notes: this is a clone of Dictionary<K,V> adapted for the case where K is a ReadOnlyMemory<byte>, and with far fewer features
		// the intended use-case is a static readonly dictionary initialized with a list of tokens that never changes

		private struct Entry
		{
			// 0-based index of next entry in chain: -1 means end of chain
			// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
			// so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
			public int Next;

			public uint HashCode;

			// Encoded key
			public ReadOnlyMemory<TRune> Key;

			// Value of entry
			public TValue Value;
		}

		private int[]? Buckets;
		private Entry[]? Entries;

		private int Size;

		public int Count => this.Size;


		protected TokenDictionary()
		{ }

		protected TokenDictionary(TokenDictionary<TValue, TLiteral, TRune> parent)
		{
			Initialize(parent.Count);
			var buckets = parent.Buckets!;
			var entries = parent.Entries!;
			int size = parent.Size;
			for (int i = 0; i < size; i++)
			{
				int idx = buckets[i] - 1;
				if (idx < 0) continue;
				Insert(entries[idx].Key, entries[idx].Value);
			}
		}
		
		#region Insert...

		public void Add(in ReadOnlyMemory<TRune> literal, TValue value)
		{
			Insert(literal, value);
		}

		private void Insert(in ReadOnlyMemory<TRune> token, TValue value)
		{
			var buckets = this.Buckets!;
			var entries = this.Entries!;

			var tokenSpan = token.Span;
			uint hashCode = ComputeHashCode(tokenSpan);

			int collisionCount = 0;

			ref int bucket = ref buckets[hashCode % (uint) buckets.Length];
			// Value in _buckets is 1-based
			int i = bucket - 1;

			// ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
			do
			{
				// Should be a while loop https://github.com/dotnet/coreclr/issues/15476
				// Test uint in if rather than loop condition to drop range check for following array access
				if ((uint) i >= (uint) entries.Length)
				{
					break;
				}

				if (entries[i].HashCode == hashCode && tokenSpan.SequenceEqual(entries[i].Key.Span))
				{
					throw new InvalidOperationException("Duplicate entry in token dictionary");
				}

				i = entries[i].Next;
				if (collisionCount >= entries.Length)
				{
					// The chain of entries forms a loop; which means a concurrent update has happened.
					// Break out of the loop and throw, rather than looping forever.
					throw new InvalidOperationException("ConcurrentOperationsNotSupported");
				}
				collisionCount++;
			} while (true);

			int count = this.Size;
			if (count == entries.Length)
			{
				Resize();
				buckets = this.Buckets!;
				entries = this.Entries!;
				bucket = ref buckets[hashCode % (uint) buckets.Length];
			}

			int index = count;
			Size = count + 1;

			ref Entry entry = ref entries[index];

			entry.HashCode = hashCode;
			// Value in _buckets is 1-based
			entry.Next = bucket - 1;
			entry.Key = token;
			entry.Value = value;
			// Value in _buckets is 1-based
			bucket = index + 1;
		}

		protected void Initialize(int capacity)
		{
			if (capacity != 0)
			{
				int size = PrimeHelpers.GetPrime(capacity);
				this.Buckets = new int[size];
				this.Entries = new Entry[size];
			}
			else
			{
				this.Buckets = [ ];
				this.Entries = [ ];
			}
		}

		private void Resize() => Resize(this.Size == 0 ? 7 : PrimeHelpers.ExpandPrime(this.Size));

		private void Resize(int newSize)
		{
			// Value types never rehash
			Contract.Debug.Requires(this.Entries != null, "_entries should be non-null");
			Contract.Debug.Requires(newSize >= this.Entries?.Length);

			var buckets = new int[newSize];
			var entries = new Entry[newSize];

			int count = this.Size;
			Array.Copy(this.Entries, 0, entries, 0, count);

			for (int i = 0; i < count; i++)
			{
				if (entries[i].Next >= -1)
				{
					uint bucket = entries[i].HashCode % (uint)newSize;
					// Value in _buckets is 1-based
					entries[i].Next = buckets[bucket] - 1;
					// Value in _buckets is 1-based
					buckets[bucket] = i + 1;
				}
			}

			this.Buckets = buckets;
			this.Entries = entries;
		}

		protected abstract uint ComputeHashCode(in ReadOnlySpan<TRune> token);

		/// <summary>Computes a fast hash over raw bytes ("pseudo xxHash32" tuned for the short literals typically stored in these dictionaries)</summary>
		/// <remarks>Only stable within the current process execution (like <see cref="string.GetHashCode()"/>), and only used for the internal buckets.</remarks>
		protected static uint ComputeHashCodeBytes(ReadOnlySpan<byte> value)
		{
			// Implementation of a "pseudo XXHASH32" optimized for small values
			// => most literals are fairly short (often less than 16) so we can settle for reading 4 bytes at a time

			if (value.Length <= 0) return 0x02CC5D05;

			ref byte ptr = ref MemoryMarshal.GetReference(value);
			ref byte end = ref Unsafe.AddByteOffset(ref ptr, new IntPtr(value.Length));
			uint h = 0;

			// combine 32 bits chunks
			while (!Unsafe.IsAddressGreaterThan(ref Unsafe.AddByteOffset(ref ptr, new IntPtr(4)), ref end))
			{
				h ^= Unsafe.As<byte, uint>(ref ptr);
				h *= 3266489917U;
				ptr = ref Unsafe.AddByteOffset(ref ptr, new IntPtr(4));
			}

			// combine remaining bytes
			while (Unsafe.IsAddressLessThan(ref ptr, ref end))
			{
				h ^= ptr;
				h *= 374761393U;
				ptr = ref Unsafe.AddByteOffset(ref ptr, new IntPtr(1));
			}

			// combine length
			h ^= (uint) value.Length;

			return h;
		}

		private static class PrimeHelpers
		{

			// This is the maximum prime smaller than Array.MaxArrayLength
			private const int MaxPrimeArrayLength = 0x7FEFFFFD;

			private const int HashPrime = 101;

			private static bool IsPrime(int candidate)
			{
				if ((candidate & 1) != 0)
				{
					int limit = (int) Math.Sqrt(candidate);
					for (int divisor = 3; divisor <= limit; divisor += 2)
					{
						if ((candidate % divisor) == 0)
						{
							return false;
						}
					}

					return true;
				}

				return (candidate == 2);
			}

			public static int GetPrime(int min)
			{
				if (min < 0) throw new ArgumentException();

				// Table of prime numbers to use as hash table sizes. 
				// A typical resize algorithm would pick the smallest prime number in this array
				// that is larger than twice the previous capacity. 
				// Suppose our Hashtable currently has capacity x and enough elements are added 
				// such that a resize needs to occur. Resizing first computes 2x then finds the 
				// first prime in the table greater than 2x, i.e. if primes are ordered 
				// p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n. 
				// Doubling is important for preserving the asymptotic complexity of the 
				// hashtable operations such as add.  Having a prime guarantees that double 
				// hashing does not lead to infinite loops.  IE, your hash function will be 
				// h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
				// We prefer the low computation costs of higher prime numbers over the increased
				// memory allocation of a fixed prime number i.e. when right sizing a HashSet.

				//note: this does NOT allocate the array every time! the compiler creates a cached version of this
				ReadOnlySpan<int> primes = [
					3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
					1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
					17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
					187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
					1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
				];

				for (int i = 0; i < primes.Length; i++)
				{
					int prime = primes[i];
					if (prime >= min)
					{
						return prime;
					}
				}

				//outside of our predefined table. 
				//compute the hard way. 
				for (int i = (min | 1); i < int.MaxValue; i += 2)
				{
					if (IsPrime(i) && ((i - 1) % HashPrime != 0))
					{
						return i;
					}
				}

				return min;
			}

			public static int ExpandPrime(int oldSize)
			{
				int newSize = 2 * oldSize;

				// Allow the hashtables to grow to maximum possible size (~2G elements) before encountering capacity overflow.
				// Note that this check works even when _items.Length overflowed thanks to the (uint) cast
				if ((uint) newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
				{
					Contract.Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
					return MaxPrimeArrayLength;
				}

				return GetPrime(newSize);
			}
		}

		#endregion

		#region Lookup...

		public bool TryGetValue(in ReadOnlySpan<TRune> token, [MaybeNullWhen(false)] out TValue value)
		{
			int idx = FindEntry(token);
			if (idx < 0)
			{
				value = default!;
				return false;
			}
			value = this.Entries![idx].Value;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetValue(in ReadOnlyMemory<TRune> token, [MaybeNullWhen(false)] out TValue value)
		{
			return TryGetValue(token.Span, out value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsKey(in ReadOnlySpan<TRune> token)
		{
			return FindEntry(token) >= 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsKey(in ReadOnlyMemory<TRune> token)
		{
			return FindEntry(token.Span) >= 0;
		}

		private int FindEntry(in ReadOnlySpan<TRune> token)
		{
			int i = -1;
			var buckets = this.Buckets!;
			var entries = this.Entries!;
			int collisionCount = 0;
			if (buckets.Length != 0)
			{
				uint hashCode = ComputeHashCode(token);

				// Value in _buckets is 1-based
				i = buckets[hashCode % (uint) buckets.Length] - 1;
				do
				{
					// Test in if to drop range check for following array access
					if ((uint) i >= (uint) entries.Length || (entries[i].HashCode == hashCode && token.SequenceEqual(entries[i].Key.Span)))
					{
						break;
					}

					i = entries[i].Next;
					if (collisionCount >= entries.Length)
					{
						// The chain of entries forms a loop; which means a concurrent update has happened.
						// Break out of the loop and throw, rather than looping forever.
						throw new InvalidOperationException("ConcurrentOperationsNotSupported");
					}
					collisionCount++;
				} while (true);
			}
			return i;
		}

		#endregion

	}

	/// <summary>Dictionary of tokens with string literals addressable via <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;char&gt;</see> keys</summary>
	/// <typeparam name="TValue">Type of tokens stored in the dictionary</typeparam>
#if NET9_0_OR_GREATER
	[Obsolete("Use Dictionary<TKey, TValue>.AlternateLookup<TSpan> (.NET 9+) instead.", error: true)]
#endif
	public sealed class CharStringTokenDictionary<TValue> : TokenDictionary<TValue, string, char>
	{

		public CharStringTokenDictionary() : this(0)
		{ }

		public CharStringTokenDictionary(int capacity)
		{
			Initialize(capacity);
		}

		public CharStringTokenDictionary(CharStringTokenDictionary<TValue> parent)
			: base(parent)
		{ }

		public CharStringTokenDictionary(IEnumerable<TValue> tokens, Func<TValue, string> literal)
		{
			Contract.NotNull(tokens);
			Contract.NotNull(literal);

			if (tokens is ICollection<TValue> coll)
			{
				Initialize(coll.Count);
			}
			foreach (var item in tokens)
			{
				if (item == null!) throw new ArgumentException("Tokens cannot be null.", nameof(tokens));
				string l = literal(item);
				if (l == null!) throw new ArgumentException("Tokens cannot have a null literal.", nameof(tokens));
				Add(l.AsMemory(), item);
			}
		}

		protected override uint ComputeHashCode(in ReadOnlySpan<char> token)
		{
#if NET5_0_OR_GREATER
			return (uint) string.GetHashCode(token);
#else
			// string.GetHashCode(ReadOnlySpan<char>) is not available there, and the netfx string hash cannot be
			// replicated over a span (it may be the process-randomized Marvin). Every bucket probe goes through this
			// single method, so the hash only needs to be self-consistent within the process: reuse the byte-based
			// routine over the raw UTF-16 bytes. (Unlike the modern branch this is NOT randomized per process, which
			// is acceptable for dictionaries of known literals, not attacker-controlled input.)
			return ComputeHashCodeBytes(MemoryMarshal.AsBytes(token));
#endif
		}

	}

	/// <summary>Dictionary of tokens with string literals addressable via their encoded byte representation (ex: UTF-8)</summary>
	/// <typeparam name="TValue">Type of tokens stored in the dictionary</typeparam>
#if NET9_0_OR_GREATER
	[Obsolete("Use Dictionary<TKey, TValue>.AlternateLookup<TSpan> (.NET 9+) instead.", error: true)]
#endif
	public sealed class ByteStringTokenDictionary<TValue> : TokenDictionary<TValue, string, byte>
	{

		public Encoding Encoding { get;}
		
		public ByteStringTokenDictionary()
			: this(0, Encoding.UTF8)
		{ }

		public ByteStringTokenDictionary(int capacity, Encoding? encoding = null)
		{
			this.Encoding = encoding ?? Encoding.UTF8;
			Initialize(capacity);
		}

		public ByteStringTokenDictionary(ByteStringTokenDictionary<TValue> parent, Encoding? encoding = null)
			: base(parent)
		{
			this.Encoding = encoding ?? parent.Encoding;
		}
		
		public ByteStringTokenDictionary(IEnumerable<TValue> tokens, Func<TValue, string> literal, Encoding? encoding = null)
		{
			Contract.NotNull(tokens);
			Contract.NotNull(literal);

			encoding ??= Encoding.UTF8;
			this.Encoding = encoding;
			if (tokens is ICollection<TValue> coll)
			{
				Initialize(coll.Count);
			}
			foreach (var item in tokens)
			{
				if (item == null!) throw new ArgumentException("Tokens cannot be null.", nameof(tokens));
				string l = literal(item);
				if (l == null!) throw new ArgumentException("Tokens cannot have a null literal.", nameof(tokens));
				Add(encoding.GetBytes(l), item);
			}
		}

		protected override uint ComputeHashCode(in ReadOnlySpan<byte> value)
			=> ComputeHashCodeBytes(value);

	}

}
