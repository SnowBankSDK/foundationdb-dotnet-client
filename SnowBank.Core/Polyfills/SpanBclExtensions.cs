#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

// Extension-method polyfills for span-based BCL APIs that are missing from the netstandard2.0 surface.
// Common drawback vs the modern BCL: several of these allocate a transient array/string to bridge to a
// netstandard2.0 method that only accepts arrays (the modern versions write straight into the caller's span).
// Per-method notes below call out anything beyond a transient allocation (e.g. different random values).
// Compat build only.

#if NETSTANDARD2_0

namespace SnowBank.Compat
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Polyfills for span-based BCL methods absent from netstandard2.0.</summary>
	public static class SpanBclExtensions
	{

		#region Random.NextInt64...

		// NOTE: these do not reproduce the BCL's values — Random.NextInt64 draws from the internal PRNG state directly,
		// whereas this pulls 8 bytes via NextBytes and reinterprets them. The distribution is uniform and unbiased (bounded
		// overloads use rejection sampling), but a given seed will NOT yield the same sequence as a modern runtime. Fine for
		// non-reproducible id/salt generation; do not rely on cross-target-framework reproducibility from a fixed seed.

		/// <summary>Returns a non-negative random 64-bit integer.</summary>
		public static long NextInt64(this Random random)
			=> (long) (NextUInt64(random) & long.MaxValue);

		/// <summary>Returns a non-negative random 64-bit integer that is less than <paramref name="maxValue"/>.</summary>
		public static long NextInt64(this Random random, long maxValue)
			=> NextInt64(random, 0, maxValue);

		/// <summary>Returns a random 64-bit integer in <c>[minValue, maxValue)</c>.</summary>
		public static long NextInt64(this Random random, long minValue, long maxValue)
		{
			if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
			ulong range = unchecked((ulong) (maxValue - minValue));
			if (range == 0) return minValue; // empty or single-value range

			// rejection sampling to avoid modulo bias
			ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
			ulong value;
			do { value = NextUInt64(random); } while (value >= limit);
			return unchecked(minValue + (long) (value % range));
		}

		/// <summary>Mirrors <c>Random.NextBytes(Span&lt;byte&gt;)</c> (.NET Core 2.1+). COST: bounces through a transient array.</summary>
		public static void NextBytes(this Random random, Span<byte> buffer)
		{
			var tmp = new byte[buffer.Length];
			random.NextBytes(tmp);
			tmp.CopyTo(buffer);
		}

		private static ulong NextUInt64(Random random)
		{
			var buffer = new byte[8];
			random.NextBytes(buffer);
			return BitConverter.ToUInt64(buffer, 0);
		}

		#endregion

		// NOTE: the 2-argument string.TryCopyTo(Span<char>, out int) is provided by SystemStringExtensions
		// (in InvariantInterpolatedStringHandler.cs), so it is intentionally not defined here.

		/// <summary>Fills the destination span with cryptographically strong random bytes.</summary>
		public static void GetBytes(this RandomNumberGenerator rng, Span<byte> destination)
		{
			var buffer = new byte[destination.Length];
			rng.GetBytes(buffer);
			buffer.CopyTo(destination);
		}

		#region Encoding span overloads...

		/// <summary>Decodes the bytes into the destination char span and returns the number of characters written.</summary>
		public static int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
		{
			char[] decoded = encoding.GetChars(bytes.ToArray());
			decoded.CopyTo(chars);
			return decoded.Length;
		}

		/// <summary>Decodes the bytes into a string.</summary>
		public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
			=> encoding.GetString(bytes.ToArray());

		/// <summary>Counts the characters the bytes would decode to (delegates through an intermediate array).</summary>
		public static int GetCharCount(this Encoding encoding, ReadOnlySpan<byte> bytes)
			=> encoding.GetCharCount(bytes.ToArray());

		/// <summary>Encodes the characters into the destination byte span and returns the number of bytes written.</summary>
		public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
		{
			byte[] encoded = encoding.GetBytes(chars.ToArray());
			encoded.CopyTo(bytes);
			return encoded.Length;
		}

		/// <summary>Tries to encode the characters into the destination byte span (mirrors the .NET 8+ instance method).</summary>
		public static bool TryGetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes, out int bytesWritten)
		{
			byte[] encoded = encoding.GetBytes(chars.ToArray());
			if (encoded.Length > bytes.Length)
			{
				bytesWritten = 0;
				return false;
			}
			encoded.CopyTo(bytes);
			bytesWritten = encoded.Length;
			return true;
		}

		#endregion

		/// <summary>Formats a <see cref="Guid"/> into the destination span (delegates to ToString then copies).</summary>
		public static bool TryFormat(this Guid value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default)
		{
			string text = value.ToString(format.Length == 0 ? null : format.ToString());
			if (text.Length > destination.Length)
			{
				charsWritten = 0;
				return false;
			}
			text.AsSpan().CopyTo(destination);
			charsWritten = text.Length;
			return true;
		}

		#region TryFormat for primitives...

		// Mirrors the instance TryFormat(Span<char>, out int, ReadOnlySpan<char>, IFormatProvider?) that all primitives
		// gained with ISpanFormattable (.NET 6). Fallback formats to a transient string then copies — one string allocation
		// per call (the modern versions write digits straight into the destination). Same results, including format strings.
		// On the modern targets the real instance methods take over, so call sites are source-compatible across both.

		private static bool TryFormatViaToString<T>(T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			where T : IFormattable
		{
			// constrained call: no boxing for the ToString invocation itself
			string text = value.ToString(format.Length == 0 ? null : format.ToString(), provider);
			if (text.Length > destination.Length)
			{
				charsWritten = 0;
				return false;
			}
			text.AsSpan().CopyTo(destination);
			charsWritten = text.Length;
			return true;
		}

		/// <summary>Tries to format the value into the destination span (netstandard2.0 fallback for the modern instance method).</summary>
		public static bool TryFormat(this sbyte value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this byte value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this short value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this ushort value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this int value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this uint value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this long value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this ulong value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this float value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this double value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this DateTime value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this DateTimeOffset value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <inheritdoc cref="TryFormat(sbyte,Span{char},out int,ReadOnlySpan{char},IFormatProvider?)"/>
		public static bool TryFormat(this TimeSpan value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => TryFormatViaToString(value, destination, out charsWritten, format, provider);

		/// <summary>Tries to format the boolean into the destination span (the modern instance method has no format/provider parameters).</summary>
		public static bool TryFormat(this bool value, Span<char> destination, out int charsWritten)
		{
			string text = value ? "True" : "False";
			if (text.Length > destination.Length)
			{
				charsWritten = 0;
				return false;
			}
			text.AsSpan().CopyTo(destination);
			charsWritten = text.Length;
			return true;
		}

		#endregion

		/// <summary>Removes all leading and trailing occurrences of the bytes in <paramref name="trimElements"/>.</summary>
		public static ReadOnlySpan<byte> Trim(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> trimElements)
		{
			int start = 0;
			while (start < span.Length && trimElements.IndexOf(span[start]) >= 0) start++;
			int end = span.Length - 1;
			while (end >= start && trimElements.IndexOf(span[end]) >= 0) end--;
			return span.Slice(start, end - start + 1);
		}

		/// <summary>Returns <see langword="true"/> if the span contains any byte other than <paramref name="value"/>.</summary>
		public static bool ContainsAnyExcept(this ReadOnlySpan<byte> span, byte value)
		{
			foreach (var b in span)
			{
				if (b != value) return true;
			}
			return false;
		}

		/// <summary>Returns <see langword="true"/> if the span contains the specified character.</summary>
		public static bool Contains(this ReadOnlySpan<char> span, char value) => span.IndexOf(value) >= 0;

		/// <summary>Counts the bytes needed to encode the characters (delegates through an intermediate array).</summary>
		public static int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars) => encoding.GetByteCount(chars.ToArray());

		/// <summary>Copies this string into the destination span. Throws if it does not fit (matches the modern <c>string.CopyTo(Span&lt;char&gt;)</c>).</summary>
		public static void CopyTo(this string source, Span<char> destination) => source.AsSpan().CopyTo(destination);

		/// <summary>Tries to copy this string into the destination span (matches the modern one-argument <c>string.TryCopyTo</c>).</summary>
		public static bool TryCopyTo(this string source, Span<char> destination) => source.AsSpan().TryCopyTo(destination);

		/// <summary>Determines whether the current type can be assigned to a variable of the specified <paramref name="targetType"/> (mirrors the .NET 5+ instance method; same null semantics: a null target returns false).</summary>
		public static bool IsAssignableTo(this Type type, Type? targetType) => targetType?.IsAssignableFrom(type) ?? false;

		#region IEnumerable fast-paths (net6+)...

		// NOTE: this mirrors the BCL fast-path but only recognizes materialized collections; for any other enumerable it
		// returns false, which makes the CALLER fall back to normal enumeration. So the behavior is correct everywhere,
		// just without the fast-path for lazy sequences.
		// (there is deliberately NO TryGetSpan polyfill here: SnowBank.Buffers.BufferExtensions already ships a public
		// TryGetSpan on every target, and a second one would make every call site ambiguous.)

		public static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T>? items, out int count)
		{
			switch (items)
			{
				case null: count = 0; return true;
				case ICollection<T> collection: count = collection.Count; return true;
				case IReadOnlyCollection<T> readOnly: count = readOnly.Count; return true;
				default: count = 0; return false;
			}
		}

		#endregion

		#region Stream span/async overloads...

		/// <summary>Reads from the stream into <paramref name="buffer"/> (bridges to the array-based <c>ReadAsync</c>).</summary>
		public static Task<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken ct)
		{
			if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>) buffer, out ArraySegment<byte> segment) && segment.Array is not null)
			{
				return stream.ReadAsync(segment.Array, segment.Offset, segment.Count, ct);
			}
			return ReadAsyncViaRentedArray(stream, buffer, ct);

			static async Task<int> ReadAsyncViaRentedArray(Stream stream, Memory<byte> buffer, CancellationToken ct)
			{
				var tmp = new byte[buffer.Length];
				int read = await stream.ReadAsync(tmp, 0, tmp.Length, ct).ConfigureAwait(false);
				new ReadOnlySpan<byte>(tmp, 0, read).CopyTo(buffer.Span);
				return read;
			}
		}

		/// <summary>Copies this stream to <paramref name="destination"/> with cancellation (bridges to the buffer-size overload).</summary>
		public static Task CopyToAsync(this Stream source, Stream destination, CancellationToken ct) => source.CopyToAsync(destination, 81920, ct);

		/// <summary>Writes a span of characters to the writer (bridges to the array-based <c>Write</c>; allocates a transient array).</summary>
		public static void Write(this TextWriter writer, ReadOnlySpan<char> buffer) => writer.Write(buffer.ToArray());

		/// <summary>Writes a span of bytes to the stream (bridges to the array-based <c>Write</c>; allocates a transient array).</summary>
		/// <summary>Mirrors <c>Stream.Read(Span&lt;byte&gt;)</c> (.NET Core 2.1+). COST: reads into a transient array then copies.</summary>
		public static int Read(this Stream stream, Span<byte> buffer)
		{
			var tmp = new byte[buffer.Length];
			int n = stream.Read(tmp, 0, tmp.Length);
			if (n > 0)
			{
				tmp.AsSpan(0, n).CopyTo(buffer);
			}
			return n;
		}

		public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
		{
			var tmp = buffer.ToArray();
			stream.Write(tmp, 0, tmp.Length);
		}

		/// <summary>Mirrors <c>Stream.WriteAsync(ReadOnlyMemory&lt;byte&gt;, CancellationToken)</c> (.NET Core 2.1+). COST: copies to a pooled array when the memory is not array-backed.</summary>
		public static async ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out var segment))
			{
				await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
				return;
			}
			var tmp = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
			try
			{
				buffer.Span.CopyTo(tmp);
				await stream.WriteAsync(tmp, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				System.Buffers.ArrayPool<byte>.Shared.Return(tmp);
			}
		}

		/// <summary>Disposes the stream (the netstandard2.0 <see cref="Stream"/> has no async dispose; this completes synchronously).</summary>
		public static ValueTask DisposeAsync(this Stream stream)
		{
			stream.Dispose();
			return default;
		}

		#endregion

		#region Collection capacity/mutation helpers (net6+)...

		/// <summary>Mirrors <c>Dictionary&lt;TKey,TValue&gt;.EnsureCapacity</c> (.NET Core 2.1+). The netstandard2.0 Dictionary cannot be re-sized in place, so this is a no-op that returns the requested capacity — correct, just without the re-hash-avoidance benefit.</summary>
		public static int EnsureCapacity<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, int capacity)
			where TKey : notnull
			=> capacity;

		/// <summary>Mirrors <c>Dictionary&lt;TKey,TValue&gt;.TryAdd</c> (.NET Core 2.0+): adds the entry only if the key is not already present (two lookups instead of the modern single-probe, same behavior).</summary>
		public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
			where TKey : notnull
		{
			if (dictionary.ContainsKey(key)) return false;
			dictionary.Add(key, value);
			return true;
		}

		/// <summary>Mirrors <c>MemoryExtensions.Sort</c> (.NET 5+): sorts in place by copying through a transient array (one allocation + two copies).</summary>
		public static void Sort<T>(this Span<T> span)
		{
			if (span.Length <= 1) return;
			var tmp = span.ToArray();
			Array.Sort(tmp);
			tmp.CopyTo(span);
		}

		/// <summary>Mirrors the <c>KeyValuePair&lt;TKey,TValue&gt;.Deconstruct</c> instance method (.NET Core 2.0+), enabling <c>foreach (var (k, v) in dictionary)</c>.</summary>
		public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
		{
			key = pair.Key;
			value = pair.Value;
		}

		/// <summary>Mirrors <c>Enumerable.FirstOrDefault(source, defaultValue)</c> (.NET 6+).</summary>
		public static T FirstOrDefault<T>(this IEnumerable<T> source, T defaultValue)
		{
			foreach (var item in source) { return item; }
			return defaultValue;
		}

		/// <summary>Mirrors <c>Enumerable.FirstOrDefault(source, predicate, defaultValue)</c> (.NET 6+).</summary>
		public static T FirstOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate, T defaultValue)
		{
			foreach (var item in source) { if (predicate(item)) return item; }
			return defaultValue;
		}

		/// <summary>Mirrors <c>Enumerable.LastOrDefault(source, defaultValue)</c> (.NET 6+).</summary>
		public static T LastOrDefault<T>(this IEnumerable<T> source, T defaultValue)
		{
			var result = defaultValue;
			foreach (var item in source) { result = item; }
			return result;
		}

		/// <summary>Mirrors <c>Enumerable.LastOrDefault(source, predicate, defaultValue)</c> (.NET 6+).</summary>
		public static T LastOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate, T defaultValue)
		{
			var result = defaultValue;
			foreach (var item in source) { if (predicate(item)) result = item; }
			return result;
		}

		/// <summary>Mirrors <c>Enumerable.ToHashSet</c> (.NET Framework 4.7.2+/.NET Core 2.0+, but absent from the netstandard2.0 surface).</summary>
		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T>? comparer = null)
			=> new(source, comparer);

		/// <summary>Mirrors <c>Queue&lt;T&gt;.TryDequeue</c> (.NET Core 2.0+): two operations instead of the modern single probe, same behavior.</summary>
		public static bool TryDequeue<T>(this Queue<T> queue, out T result)
		{
			if (queue.Count == 0)
			{
				result = default!;
				return false;
			}
			result = queue.Dequeue();
			return true;
		}

		/// <summary>Mirrors <c>Queue&lt;T&gt;.TryPeek</c> (.NET Core 2.0+).</summary>
		public static bool TryPeek<T>(this Queue<T> queue, out T result)
		{
			if (queue.Count == 0)
			{
				result = default!;
				return false;
			}
			result = queue.Peek();
			return true;
		}

		/// <summary>Mirrors <c>Dictionary&lt;TKey,TValue&gt;.Remove(key, out value)</c> (.NET Core 2.0+): two lookups (TryGetValue + Remove) instead of the modern single probe, same behavior.</summary>
		public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, [MaybeNullWhen(false)] out TValue value)
			where TKey : notnull
		{
			if (!dictionary.TryGetValue(key, out value))
			{
				return false;
			}
			dictionary.Remove(key);
			return true;
		}

		#endregion

		#region Span search helpers (net8+)...

		/// <summary>Mirrors <c>MemoryExtensions.ContainsAnyExcept(value)</c> (.NET 8+): linear scan instead of the vectorized BCL implementation.</summary>
		public static bool ContainsAnyExcept<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
		{
			foreach (var item in span)
			{
				if (!item.Equals(value)) return true;
			}
			return false;
		}

		/// <summary>Mirrors <c>MemoryExtensions.ContainsAnyExcept(value)</c> (.NET 8+): linear scan instead of the vectorized BCL implementation.</summary>
		public static bool ContainsAnyExcept<T>(this Span<T> span, T value) where T : IEquatable<T>
			=> ContainsAnyExcept((ReadOnlySpan<T>) span, value);

		#endregion

		#region Dictionary GetValueOrDefault (netcore2.0+)...

		/// <summary>Mirrors <c>CollectionExtensions.GetValueOrDefault</c> (.NET Core 2.0+).</summary>
		[return: MaybeNull]
		public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
			=> dictionary.TryGetValue(key, out var value) ? value : default;

		/// <summary>Mirrors <c>CollectionExtensions.GetValueOrDefault</c> (.NET Core 2.0+).</summary>
		public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
			=> dictionary.TryGetValue(key, out var value) ? value : defaultValue;

		#endregion

		#region String single-character helpers (netcore2.0+)...

		/// <summary>Mirrors <c>string.StartsWith(char)</c> (.NET Core 2.0+): ordinal comparison, no culture lookup.</summary>
		public static bool StartsWith(this string self, char value) => self.Length != 0 && self[0] == value;

		/// <summary>Mirrors <c>string.EndsWith(char)</c> (.NET Core 2.0+): ordinal comparison, no culture lookup.</summary>
		public static bool EndsWith(this string self, char value) => self.Length != 0 && self[self.Length - 1] == value;

		/// <summary>Mirrors <c>string.Split(char, StringSplitOptions)</c> (.NET Core 2.0+): allocates a one-element separator array per call.</summary>
		public static string[] Split(this string self, char separator, StringSplitOptions options) => self.Split([ separator ], options);

		/// <summary>Mirrors <c>string.Split(string?, StringSplitOptions)</c> (.NET Core 2.0+): allocates a one-element separator array per call.</summary>
		public static string[] Split(this string self, string? separator, StringSplitOptions options = StringSplitOptions.None) => self.Split([ separator! ], options);

		#endregion

		#region Random.GetItems (net8+)...

		/// <summary>Mirrors <c>Random.GetItems(choices, destination)</c> (.NET 8+): fills the destination with items chosen at random from the provided set of choices.</summary>
		public static void GetItems<T>(this Random random, ReadOnlySpan<T> choices, Span<T> destination)
		{
			if (choices.IsEmpty) throw new ArgumentException("Span may not be empty.", nameof(choices));
			for (int i = 0; i < destination.Length; i++)
			{
				destination[i] = choices[random.Next(choices.Length)];
			}
		}

		#endregion

		#region StringBuilder interpolated-handler overloads (net6+)...

		// Mirrors the .NET 6+ StringBuilder.Append/AppendLine(IFormatProvider, ref handler) overloads: the handler
		// (also polyfilled) formats to a transient string which is then appended (one extra allocation per call).

		/// <summary>Mirrors <c>StringBuilder.Append(IFormatProvider, ref AppendInterpolatedStringHandler)</c> (.NET 6+).</summary>
		public static StringBuilder Append(this StringBuilder builder, IFormatProvider? provider, [System.Runtime.CompilerServices.InterpolatedStringHandlerArgument(nameof(provider))] ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler handler)
			=> builder.Append(handler.ToStringAndClear());

		/// <summary>Mirrors <c>StringBuilder.AppendLine(IFormatProvider, ref AppendInterpolatedStringHandler)</c> (.NET 6+).</summary>
		public static StringBuilder AppendLine(this StringBuilder builder, IFormatProvider? provider, [System.Runtime.CompilerServices.InterpolatedStringHandlerArgument(nameof(provider))] ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler handler)
			=> builder.AppendLine(handler.ToStringAndClear());

		#endregion

		#region Task.WaitAsync (net6+)...

		// COST: each call allocates a linked CancellationTokenSource and a Task.Delay timer, where the modern BCL uses a
		// dedicated zero-allocation awaiter. Same semantics: TimeoutException on timeout, OperationCanceledException on
		// cancellation, and the underlying task keeps running in both cases.

		/// <summary>Mirrors <c>Task.WaitAsync(TimeSpan, CancellationToken)</c> (.NET 6+).</summary>
		public static async Task WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
		{
			if (task.IsCompleted)
			{
				await task.ConfigureAwait(false);
				return;
			}
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var delay = Task.Delay(timeout, cts.Token);
			var winner = await Task.WhenAny(task, delay).ConfigureAwait(false);
			if (winner != task)
			{
				if (cancellationToken.IsCancellationRequested)
				{ // the real WaitAsync throws TaskCanceledException (not the base OperationCanceledException)
					throw new TaskCanceledException(Task.FromCanceled(cancellationToken));
				}
				throw new TimeoutException();
			}
			cts.Cancel(); // release the timer
			await task.ConfigureAwait(false);
		}

		/// <summary>Mirrors <c>Task&lt;T&gt;.WaitAsync(TimeSpan, CancellationToken)</c> (.NET 6+).</summary>
		public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, TimeSpan timeout, CancellationToken cancellationToken = default)
		{
			if (task.IsCompleted)
			{
				return await task.ConfigureAwait(false);
			}
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var delay = Task.Delay(timeout, cts.Token);
			var winner = await Task.WhenAny(task, delay).ConfigureAwait(false);
			if (winner != task)
			{
				if (cancellationToken.IsCancellationRequested)
				{ // the real WaitAsync throws TaskCanceledException (not the base OperationCanceledException)
					throw new TaskCanceledException(Task.FromCanceled(cancellationToken));
				}
				throw new TimeoutException();
			}
			cts.Cancel(); // release the timer
			return await task.ConfigureAwait(false);
		}

		/// <summary>Mirrors <c>Task.WaitAsync(CancellationToken)</c> (.NET 6+).</summary>
		public static Task WaitAsync(this Task task, CancellationToken cancellationToken) => WaitAsync(task, Timeout.InfiniteTimeSpan, cancellationToken);

		/// <summary>Mirrors <c>Task&lt;T&gt;.WaitAsync(CancellationToken)</c> (.NET 6+).</summary>
		public static Task<TResult> WaitAsync<TResult>(this Task<TResult> task, CancellationToken cancellationToken) => WaitAsync(task, Timeout.InfiniteTimeSpan, cancellationToken);

		#endregion

		#region Task...

		extension(Task task)
		{

			/// <summary>Mirrors <c>Task.IsCompletedSuccessfully</c> (.NET Core 2.0+).</summary>
			public bool IsCompletedSuccessfully => task.Status == TaskStatus.RanToCompletion;

		}

		/// <summary>Mirrors <c>CancellationTokenSource.CancelAsync()</c> (.NET 8+); cancels synchronously on this target (the callbacks run inline before the returned task completes).</summary>
		public static Task CancelAsync(this CancellationTokenSource cts)
		{
			cts.Cancel();
			return Task.CompletedTask;
		}

		#endregion

		#region ValueTask statics...

		extension(ValueTask)
		{

			/// <summary>Mirrors <c>ValueTask.CompletedTask</c> (.NET 5+).</summary>
			public static ValueTask CompletedTask => default;

			/// <summary>Mirrors <c>ValueTask.FromResult&lt;T&gt;</c> (.NET 5+).</summary>
			public static ValueTask<T> FromResult<T>(T result) => new(result);

			/// <summary>Mirrors <c>ValueTask.FromCanceled</c> (.NET 5+).</summary>
			public static ValueTask FromCanceled(CancellationToken cancellationToken) => new(Task.FromCanceled(cancellationToken));

			/// <summary>Mirrors <c>ValueTask.FromCanceled&lt;T&gt;</c> (.NET 5+).</summary>
			public static ValueTask<T> FromCanceled<T>(CancellationToken cancellationToken) => new(Task.FromCanceled<T>(cancellationToken));

			/// <summary>Mirrors <c>ValueTask.FromException</c> (.NET 5+).</summary>
			public static ValueTask FromException(Exception exception) => new(Task.FromException(exception));

			/// <summary>Mirrors <c>ValueTask.FromException&lt;T&gt;</c> (.NET 5+).</summary>
			public static ValueTask<T> FromException<T>(Exception exception) => new(Task.FromException<T>(exception));

		}

		#endregion

		#region Random statics...

		extension(Random)
		{

			/// <summary>Mirrors <c>Random.Shared</c> (.NET 6+); backed by a per-thread instance on this target (the modern implementation is thread-safe).</summary>
			public static Random Shared => SharedRandom.Instance ??= new Random(unchecked((Environment.TickCount * 397) ^ Environment.CurrentManagedThreadId));

		}

		private static class SharedRandom
		{
			[ThreadStatic]
			public static Random? Instance;
		}

		#endregion

		#region Span parsing statics...

		extension(int)
		{

			/// <summary>Mirrors <c>int.TryParse(ReadOnlySpan&lt;char&gt;, out int)</c> (.NET Core 2.1+); allocates a temporary string on this target.</summary>
			public static bool TryParse(ReadOnlySpan<char> s, out int result) => int.TryParse(s.ToString(), out result);

		}

		extension(ulong)
		{

			/// <summary>Mirrors <c>ulong.Parse(ReadOnlySpan&lt;char&gt;, NumberStyles, IFormatProvider)</c> (.NET Core 2.1+); allocates a temporary string on this target.</summary>
			public static ulong Parse(ReadOnlySpan<char> s, System.Globalization.NumberStyles style, IFormatProvider? provider) => ulong.Parse(s.ToString(), style, provider);

			/// <summary>Mirrors <c>ulong.TryParse(ReadOnlySpan&lt;char&gt;, NumberStyles, IFormatProvider, out ulong)</c> (.NET Core 2.1+); allocates a temporary string on this target.</summary>
			public static bool TryParse(ReadOnlySpan<char> s, System.Globalization.NumberStyles style, IFormatProvider? provider, out ulong result) => ulong.TryParse(s.ToString(), style, provider, out result);

		}

		extension(System.Net.IPAddress)
		{

			/// <summary>Mirrors <c>IPAddress.TryParse(ReadOnlySpan&lt;char&gt;, out IPAddress)</c> (.NET Core 3.0+); allocates a temporary string on this target.</summary>
			public static bool TryParse(ReadOnlySpan<char> ipSpan, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Net.IPAddress? address) => System.Net.IPAddress.TryParse(ipSpan.ToString(), out address);

		}

		#endregion

		#region Collections...

		/// <summary>Mirrors <c>ConcurrentDictionary&lt;K, V&gt;.TryRemove(KeyValuePair&lt;K, V&gt;)</c> (.NET Core 3.0+): removes the entry only if it maps this exact key to this exact value.</summary>
		public static bool TryRemove<TKey, TValue>(this System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> self, KeyValuePair<TKey, TValue> item) where TKey : notnull
			=> ((ICollection<KeyValuePair<TKey, TValue>>) self).Remove(item); // the explicit ICollection implementation has the same "remove this exact pair" semantics

		/// <summary>Mirrors <c>MemoryExtensions.AsSpan(T[], Range)</c> (.NET Core 3.0+).</summary>
		public static Span<T> AsSpan<T>(this T[]? array, Range range)
		{
			var (offset, length) = range.GetOffsetAndLength(array?.Length ?? 0);
			return array.AsSpan(offset, length);
		}

		/// <summary>Mirrors <c>MemoryExtensions.Sort(Span&lt;T&gt;, TComparer)</c> (.NET 5+); sorts through a rented temporary array on this target.</summary>
		public static void Sort<T, TComparer>(this Span<T> span, TComparer comparer) where TComparer : IComparer<T>
		{
			if (span.Length <= 1) return;
			var tmp = System.Buffers.ArrayPool<T>.Shared.Rent(span.Length);
			span.CopyTo(tmp);
			Array.Sort(tmp, 0, span.Length, comparer);
			tmp.AsSpan(0, span.Length).CopyTo(span);
			System.Buffers.ArrayPool<T>.Shared.Return(tmp, clearArray: RuntimeHelpersCompat.IsReferenceOrContainsReferences<T>());
		}

		/// <summary>Mirrors <c>MemoryExtensions.LastIndexOfAnyExcept(Span&lt;T&gt;, T)</c> (.NET 8+).</summary>
		public static int LastIndexOfAnyExcept<T>(this Span<T> span, T value) where T : IEquatable<T>
			=> LastIndexOfAnyExcept((ReadOnlySpan<T>) span, value);

		/// <summary>Mirrors <c>MemoryExtensions.LastIndexOfAnyExcept(ReadOnlySpan&lt;T&gt;, T)</c> (.NET 8+).</summary>
		public static int LastIndexOfAnyExcept<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
		{
			for (int i = span.Length - 1; i >= 0; i--)
			{
				if (!span[i].Equals(value)) return i;
			}
			return -1;
		}

		#endregion

	}

}

#endif
