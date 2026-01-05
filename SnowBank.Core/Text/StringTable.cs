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

//#define DEBUG_STRINGTABLE_PERFS

namespace SnowBank.Text
{
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Collections.Caching;
	using SnowBank.Buffers.Text;

	// Adaptation de Roslyn.Utilities.StringTable, cf http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis/InternalUtilities/StringTable.cs

	/// <summary>
	/// This is basically a lossy cache of strings that is searchable by
	/// strings, string sub ranges, character array ranges or string-builder.
	/// </summary>
	[PublicAPI]
	public class StringTable : IDisposable
	{
#if DEBUG_STRINGTABLE_PERFS
		public static long addCalls;
		public static long localHits;
		public static long sharedHits;
#endif

		/// <summary>Helpers for Hash computation</summary>
		[PublicAPI]
		public static class Hash
		{
			/// <summary>
			/// The offset bias value used in the FNV-1a algorithm
			/// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
			/// </summary>
			public const int FNV_OFFSET_BIAS = unchecked((int) 2166136261);

			/// <summary>
			/// The generative factor used in the FNV-1a algorithm
			/// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
			/// </summary>
			private const int FNV_PRIME = 16777619;

			[Pure]
			public static unsafe int GetFnvHashCode(ReadOnlySpan<byte> text, out bool isAscii)
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(text))
				{
					int hashCode = FNV_OFFSET_BIAS;
					byte asciiMask = 0;
					byte* inp = ptr;
					byte* end = ptr + text.Length;
					while(inp < end)
					{
						byte b = *inp++;
						asciiMask |= b;
						hashCode = unchecked((hashCode ^ b) * FNV_PRIME);
					}

#if NET8_0_OR_GREATER
					isAscii = Ascii.IsValid(asciiMask);
#else
					isAscii = (asciiMask & 0x80) == 0;
#endif
					return hashCode;
				}
			}

			[Pure]
			public static unsafe int GetFnvHashCode(ReadOnlySpan<char> text)
			{
				fixed (char* ptr = &MemoryMarshal.GetReference(text))
				{
					char* inp = ptr;
					char* end = ptr + text.Length;
					int hashCode = FNV_OFFSET_BIAS;
					while (inp < end)
					{
						hashCode = unchecked((hashCode ^ (*inp++)) * FNV_PRIME);
					}

					return hashCode;
				}
			}

			[Pure]
			public static int GetFnvHashCode(StringBuilder text)
			{
				int hashCode = FNV_OFFSET_BIAS;
				int end = text.Length;

				for (int i = 0; i < end; i++)
				{
					hashCode = unchecked((hashCode ^ text[i]) * FNV_PRIME);
				}

				return hashCode;
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int GetFnvHashCode(char ch)
			{
				return unchecked((FNV_OFFSET_BIAS ^ ch) * FNV_PRIME);
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int CombineFnvHash(int hashCode, char ch)
			{
				return unchecked((hashCode ^ ch) * FNV_PRIME);
			}

		}

		// Size of local cache.
		private const int LocalSizeBits = 11;
		private const int LocalSize = 1 << LocalSizeBits;
		private const int LocalSizeMask = LocalSize - 1;

		// max size of shared cache.
		private const int SharedSizeBits = 16;
		private const int SharedSize = 1 << SharedSizeBits;
		private const int SharedSizeMask = SharedSize - 1;

		// size of bucket in shared cache. (local cache has bucket size 1).
		private const int SharedBucketBits = 4;
		private const int SharedBucketSize = (1 << SharedBucketBits);
		private const int SharedBucketSizeMask = SharedBucketSize - 1;

		// local (L1) cache
		// simple fast and not threadsafe cache
		// with limited size and "last add wins" expiration policy
		//
		// The main purpose of the local cache is to use in long-lived
		// single threaded operations with lots of locality (like parsing).
		// Local cache is smaller (and thus faster) and is not affected
		// by cache misses on other threads.
		private readonly (int HashCode, string? Text)[] m_localTable = new (int, string?)[LocalSize];

		// shared (L2) threadsafe cache
		// slightly slower than local cache
		// we read this cache when having a miss in local cache
		// writes to local cache will update shared cache as well.
		private static readonly (int HashCode, string? Text)[] s_sharedTable = new (int, string?)[SharedSize];

		// essentially a random number
		// the usage pattern will randomly use and increment this
		// the counter is not static to avoid interlocked operations and cross-thread traffic
		private int m_localRandom = Environment.TickCount;

		#region Poolable...

		private StringTable(ObjectPool<StringTable>? pool)
		{
			m_pool = pool;
		}

		private readonly ObjectPool<StringTable>? m_pool;
		private static readonly ObjectPool<StringTable> s_staticPool = CreatePool();

		private static ObjectPool<StringTable> CreatePool()
		{
			ObjectPool<StringTable>? pool = null;
			// ReSharper disable once AccessToModifiedClosure
			pool = new ObjectPool<StringTable>(() => new StringTable(pool), Environment.ProcessorCount * 2);
			return pool;
		}

		public static StringTable GetInstance()
		{
			return s_staticPool.Allocate();
		}

		public void Dispose()
		{
			// leave cache content in the cache, just return it to the pool
			// Array.Clear(this.localTable, 0, this.localTable.Length);
			// Array.Clear(sharedTable, 0, sharedTable.Length);
			m_pool?.Free(this);
		}

		#endregion

		/// <summary>Ajoute une chaîne de texte dans la table, a partir d'octets encodés en UTF8 (ou ASCII)</summary>
		/// <returns>Chaîne de texte correspondante.</returns>
		public string Add(ReadOnlySpan<byte> text)
		{
			if (text.Length == 0) return string.Empty;
			var hashCode = Hash.GetFnvHashCode(text, out bool isAscii);
			return isAscii
				? AddAscii(hashCode, text)
				: AddUtf8(hashCode, text);
		}

		/// <summary>Ajoute une chaîne de texte dans la table, a partir d'octets encodés en UTF8 (ou ASCII) et un HashCode déjà calculé</summary>
		/// <returns>Chaîne de texte correspondante.</returns>
		public string Add(int hashCode, ReadOnlySpan<byte> text)
		{
			if (text.Length == 0) return string.Empty;
			return AddUtf8(hashCode, text);
		}

		[Pure]
		public string AddAscii(int hashCode, ReadOnlySpan<byte> ascii)
		{
#if DEBUG_STRINGTABLE_PERFS
			Interlocked.Increment(ref addCalls);
#endif
			var arr = m_localTable;
			var idx = LocalIdxFromHash(hashCode);

			var text = arr[idx].Text;
			if (text != null && arr[idx].HashCode == hashCode)
			{
				var result = arr[idx].Text!;
				if (TextEqualsAscii(result, ascii))
				{
#if DEBUG_STRINGTABLE_PERFS
					Interlocked.Increment(ref localHits);
#endif
					return result;
				}
			}

			string? shared = FindSharedEntryAscii(ascii, hashCode);
			if (shared != null)
			{
				// PERF: the following code does elementwise assignment of a struct
				//       because current JIT produces better code compared to
				//       arr[idx] = new Entry(...)
				arr[idx].HashCode = hashCode;
				arr[idx].Text = shared;

#if DEBUG_STRINGTABLE_PERFS
				Interlocked.Increment(ref sharedHits);
#endif
				return shared;
			}

			return AddItemAscii(ascii, hashCode);
		}

		[Pure]
		public string AddUtf8(int hashCode, ReadOnlySpan<byte> utf8)
		{
#if DEBUG_STRINGTABLE_PERFS
			Interlocked.Increment(ref addCalls);
#endif
			var arr = m_localTable;
			var idx = LocalIdxFromHash(hashCode);

			var text = arr[idx].Text;
			if (text != null && arr[idx].HashCode == hashCode)
			{
				var result = arr[idx].Text!;
				if (TextEqualsUtf8(text, utf8))
				{
#if DEBUG_STRINGTABLE_PERFS
					Interlocked.Increment(ref localHits);
#endif
					return result;
				}
			}

			string? shared = FindSharedEntryUtf8(utf8, hashCode);
			if (shared != null)
			{
				// PERF: the following code does elementwise assignment of a struct
				//       because current JIT produces better code compared to
				//       arr[idx] = new Entry(...)
				arr[idx].HashCode = hashCode;
				arr[idx].Text = shared;

#if DEBUG_STRINGTABLE_PERFS
				Interlocked.Increment(ref sharedHits);
#endif
				return shared;
			}

			return AddItemUtf8(utf8, hashCode);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string Add(ReadOnlySpan<char> chars, string? original = null)
		{
			return Add(Hash.GetFnvHashCode(chars), chars, original);
		}

		[Pure]
		public string Add(int hashCode, ReadOnlySpan<char> chars, string? original = null)
		{
#if DEBUG_STRINGTABLE_PERFS
			Interlocked.Increment(ref addCalls);
#endif

			var arr = m_localTable;
			var idx = LocalIdxFromHash(hashCode);

			var text = arr[idx].Text;
			if (text != null && arr[idx].HashCode == hashCode)
			{
				var result = arr[idx].Text!;
				if (chars.SequenceEqual(result.AsSpan()))
				{
#if DEBUG_STRINGTABLE_PERFS
					Interlocked.Increment(ref localHits);
#endif
					return result;
				}
			}

			string? shared = FindSharedEntry(chars, hashCode);
			if (shared != null)
			{
				// PERF: the following code does elementwise assignment of a struct
				//       because current JIT produces better code compared to
				//       arr[idx] = new Entry(...)
				arr[idx].HashCode = hashCode;
				arr[idx].Text = shared;

#if DEBUG_STRINGTABLE_PERFS
				Interlocked.Increment(ref sharedHits);
#endif
				return shared;
			}

			return AddItem(chars, hashCode, original);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string Add(char[] chars, int start, int len)
		{
			return Add(new ReadOnlySpan<char>(chars, start, len));
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string Add(string chars)
		{
			return Add(chars.AsSpan());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string Add(int hashCode, string chars)
		{
			return Add(hashCode, chars.AsSpan(), chars);
		}

		public string Add(StringBuilder chars)
		{
			var hashCode = Hash.GetFnvHashCode(chars);
			return Add(hashCode, chars);
		}

		public string Add(int hashCode, StringBuilder chars)
		{
#if DEBUG_STRINGTABLE_PERFS
			Interlocked.Increment(ref addCalls);
#endif

			// capture array to avoid extra range checks
			var arr = m_localTable;
			var idx = LocalIdxFromHash(hashCode);

			var text = arr[idx].Text;

			if (text != null && arr[idx].HashCode == hashCode)
			{
				var result = arr[idx].Text!;
				if (TextEquals(result, chars))
				{
#if DEBUG_STRINGTABLE_PERFS
					Interlocked.Increment(ref localHits);
#endif
					return result;
				}
			}

			string? shared = FindSharedEntry(chars, hashCode);
			if (shared != null)
			{
				// PERF: the following code does elementwise assignment of a struct
				//       because current JIT produces better code compared to
				//       arr[idx] = new Entry(...)
				arr[idx].HashCode = hashCode;
				arr[idx].Text = shared;

#if DEBUG_STRINGTABLE_PERFS
				Interlocked.Increment(ref sharedHits);
#endif
				return shared;
			}

			return AddItem(chars, hashCode);
		}

		private static string? FindSharedEntryAscii(ReadOnlySpan<byte> ascii, int hashCode)
		{
			var arr = s_sharedTable;
			int idx = SharedIdxFromHash(hashCode);

			string? e = null;
			// we use quadratic probing here
			// bucket positions are (n^2 + n)/2 relative to the masked hashcode
			for (int i = 1; i < SharedBucketSize + 1; i++)
			{
				e = arr[idx].Text;
				int hash = arr[idx].HashCode;

				if (e != null)
				{
					if (hash == hashCode && TextEqualsAscii(e, ascii))
					{
						break;
					}

					// this is not e we are looking for
					e = null;
				}
				else
				{
					// once we see unfilled entry, the rest of the bucket will be empty
					break;
				}

				idx = (idx + i) & SharedSizeMask;
			}

			return e;
		}

		private static string? FindSharedEntryUtf8(ReadOnlySpan<byte> utf8, int hashCode)
		{
			var arr = s_sharedTable;
			int idx = SharedIdxFromHash(hashCode);

			string? e = null;
			// we use quadratic probing here
			// bucket positions are (n^2 + n)/2 relative to the masked hashcode
			for (int i = 1; i < SharedBucketSize + 1; i++)
			{
				e = arr[idx].Text;
				int hash = arr[idx].HashCode;

				if (e != null)
				{
					if (hash == hashCode && TextEqualsUtf8(e, utf8))
					{
						break;
					}

					// this is not e we are looking for
					e = null;
				}
				else
				{
					// once we see unfilled entry, the rest of the bucket will be empty
					break;
				}

				idx = (idx + i) & SharedSizeMask;
			}

			return e;
		}

		private static string? FindSharedEntry(ReadOnlySpan<char> chars, int hashCode)
		{
			var arr = s_sharedTable;
			int idx = SharedIdxFromHash(hashCode);

			string? e = null;
			// we use quadratic probing here
			// bucket positions are (n^2 + n)/2 relative to the masked hashcode
			for (int i = 1; i < SharedBucketSize + 1; i++)
			{
				e = arr[idx].Text;
				int hash = arr[idx].HashCode;

				if (e != null)
				{
					if (hash == hashCode && TextEquals(e, chars))
					{
						break;
					}

					// this is not e we are looking for
					e = null;
				}
				else
				{
					// once we see unfilled entry, the rest of the bucket will be empty
					break;
				}

				idx = (idx + i) & SharedSizeMask;
			}

			return e;
		}

		private static string? FindSharedEntry(StringBuilder chars, int hashCode)
		{
			var arr = s_sharedTable;
			int idx = SharedIdxFromHash(hashCode);

			string? e = null;
			// we use quadratic probing here
			// bucket positions are (n^2 + n)/2 relative to the masked hashcode
			for (int i = 1; i < SharedBucketSize + 1; i++)
			{
				e = arr[idx].Text;
				int hash = arr[idx].HashCode;

				if (e != null)
				{
					if (hash == hashCode && TextEquals(e, chars))
					{
						break;
					}

					// this is not e we are looking for
					e = null;
				}
				else
				{
					// once we see unfilled entry, the rest of the bucket will be empty
					break;
				}

				idx = (idx + i) & SharedSizeMask;
			}

			return e;
		}

		private unsafe string AddItemAscii(ReadOnlySpan<byte> ascii, int hashCode)
		{
			fixed (byte* ptr = &MemoryMarshal.GetReference(ascii))
			{
				var text = new string((sbyte*) ptr, 0, ascii.Length);
				AddCore(text, hashCode);
				return text;
			}
		}

		private unsafe string AddItemUtf8(ReadOnlySpan<byte> utf8, int hashCode)
		{
			fixed (byte* ptr = &MemoryMarshal.GetReference(utf8))
			{
				var text = new string((sbyte*) ptr, 0, utf8.Length, Encoding.UTF8);
				AddCore(text, hashCode);
				return text;
			}
		}

		private string AddItem(ReadOnlySpan<char> chars, int hashCode, string? original)
		{
			var text = original ?? chars.ToString();
			AddCore(text, hashCode);
			return text;
		}

		private string AddItem(StringBuilder chars, int hashCode)
		{
			var text = chars.ToString();
			AddCore(text, hashCode);
			return text;
		}

		private void AddCore(string chars, int hashCode)
		{
			// add to the shared table first (in case someone looks for same item)
			AddSharedEntry(hashCode, chars);

			// add to the local table too
			var arr = m_localTable;
			var idx = LocalIdxFromHash(hashCode);
			arr[idx].HashCode = hashCode;
			arr[idx].Text = chars;
		}

		private void AddSharedEntry(int hashCode, string text)
		{
			var arr = s_sharedTable;
			int idx = SharedIdxFromHash(hashCode);

			// try finding an empty spot in the bucket
			// we use quadratic probing here
			// bucket positions are (n^2 + n)/2 relative to the masked hashcode
			int curIdx = idx;
			for (int i = 1; i < SharedBucketSize + 1; i++)
			{
				if (arr[curIdx].Text is null)
				{
					idx = curIdx;
					goto foundIdx;
				}

				curIdx = (curIdx + i) & SharedSizeMask;
			}

			// or pick a random victim within the bucket range
			// and replace with new entry
			var i1 = LocalNextRandom() & SharedBucketSizeMask;
			idx = (idx + ((i1 * i1 + i1) / 2)) & SharedSizeMask;

			foundIdx:
			arr[idx].HashCode = hashCode;
			Volatile.Write(ref arr[idx].Text, text);
		}

		private static int LocalIdxFromHash(int hash)
		{
			return hash & LocalSizeMask;
		}

		private static int SharedIdxFromHash(int hash)
		{
			// we can afford to mix some more hash bits here
			return (hash ^ (hash >> LocalSizeBits)) & SharedSizeMask;
		}

		private int LocalNextRandom()
		{
			return m_localRandom++;
		}

		private static bool TextEquals(string array, ReadOnlySpan<char> text)
		{
			return array.Length == text.Length && text.SequenceEqual(array.AsSpan());
		}

		private static bool TextEquals(string array, StringBuilder text)
		{
			if (array.Length != text.Length)
			{
				return false;
			}

			// interestingly, string builder holds the list of chunks by the tail
			// so accessing positions at the beginning may cost more than those at the end.
			for (var i = array.Length - 1; i >= 0; i--)
			{
				if (array[i] != text[i])
				{
					return false;
				}
			}

			return true;
		}

		internal static unsafe bool TextEquals(string array, char* text, int length)
		{
			return array.Length == length && TextEqualsCore(array, text);
		}

		private static unsafe bool TextEqualsCore(string array, char* text)
		{
			// use array.Length to eliminate the range check
			foreach (char c in array)
			{
				if (c != *text++)
				{
					return false;
				}
			}
			return true;
		}

		private static unsafe bool TextEqualsAscii(string text, ReadOnlySpan<byte> ascii)
		{
			if (text.Length != ascii.Length) return false;

			fixed (byte* pChars = &MemoryMarshal.GetReference(ascii))
			{
				for (var i = 0; i < text.Length; i++)
				{
					if (text[i] != pChars[i])
					{
						return false;
					}
				}

				return true;
			}
		}

		private static bool TextEqualsUtf8(string text, ReadOnlySpan<byte> utf8)
		{
			return Utf8String.Equals(utf8, text);
		}

	}
}
