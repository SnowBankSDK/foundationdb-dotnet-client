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

	using System.Numerics;

	/// <summary>Wraps a <see cref="Slice"/> that contains a pre-encoded key in the database</summary>
	/// <remarks>
	/// <para>This can help in situations where the same key will be frequently used in the same transaction, and where the cost of allocating on the heap will be less than re-encoding the key over and over.</para>
	/// <para>Please refrain from capturing a key in once transaction and reusing it in another. It is possible that the original subspace would be moved or re-created between both transactions, with a different prefix, which could create silent data corruption!</para>
	/// </remarks>
	public readonly struct FdbRawKey : IFdbKey
#if NET7_0_OR_GREATER
		// IComparisonOperators<,,> has static abstract members, which need runtime support that netstandard2.0/NetFX lacks (the comparison operators themselves are still defined)
		, IComparisonOperators<FdbRawKey, FdbRawKey, bool>
		, IComparisonOperators<FdbRawKey, Slice, bool>
#endif
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbRawKey(Slice data)
		{
			this.Data = data;
		}

		/// <summary>Pre-encoded bytes for this key</summary>
		public readonly Slice Data;

		[Pure]
		public ReadOnlySpan<byte> Span => this.Data.Span;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Data.Equals(bytes),
			FdbRawKey key => this.Data.Equals(key.Data),
			FdbTupleKey key => key.Equals(this.Data),
			FdbSuffixKey key => key.Equals(this.Data),
			FdbSubspaceKey key => key.Equals(this.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Data.GetHashCode();

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbRawKey key => this.Data.Equals(key.Data),
			FdbTupleKey key => key.Equals(this.Data),
			FdbSuffixKey key => key.Equals(this.Data),
			FdbSubspaceKey key => key.Equals(this.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbRawKey))      { return (     (FdbRawKey) (object) other).Data.Equals(this.Data); }
			if (typeof(TOtherKey) == typeof(FdbTupleKey))    { return (   (FdbTupleKey) (object) other).Equals(this.Data); }
			if (typeof(TOtherKey) == typeof(FdbSuffixKey))   { return (  (FdbSuffixKey) (object) other).Equals(this.Data); }
			if (typeof(TOtherKey) == typeof(FdbSubspaceKey)) { return ((FdbSubspaceKey) (object) other).Equals(this.Data); }
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => this.Data.Equals(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => this.Data.Equals(other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => this.Data.Equals(other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbTupleKey key => -key.CompareTo(this.Span),
			FdbSuffixKey key => -key.CompareTo(this.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other) => this.Data.CompareTo(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other) => this.Data.CompareTo(other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other) => this.Data.Span.SequenceCompareTo(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return this.Data.CompareTo(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbRawKey left, FdbRawKey right) => left.Data.Equals(right.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbRawKey left, Slice right) => left.Data.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbRawKey left, FdbRawKey right) => !left.Data.Equals(right.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbRawKey left, Slice right) => !left.Data.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbRawKey left, Slice right) => left.Data.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbRawKey left, Slice right) => left.Data.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbRawKey left, Slice right) => left.Data.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbRawKey left, Slice right) => left.Data.CompareTo(right) >= 0;

		#endregion

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "K" or "k" or "P" or "p" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Single),
			"B" or "b" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Begin),
			"E" or "e" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.End),
			"X" or "x" => this.Data.ToString(format),
			"G" or "g" => $"{nameof(FdbRawKey)}({Data:K})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" or "K" or "k" or "P" or "p" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Single).TryCopyTo(destination, out charsWritten),
			"B" or "b" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Begin).TryCopyTo(destination, out charsWritten),
			"E" or "e" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.End).TryCopyTo(destination, out charsWritten),
			"X" or "x" => this.Data.TryFormat(destination, out charsWritten, format, provider),
			"G" or "g" => destination.TryWrite($"{nameof(FdbRawKey)}({this.Data:K})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = this.Data.Span;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Data.Count;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.TryCopyTo(destination, out bytesWritten);
		}

		#endregion

		/// <summary>Returns the pre-encoded binary representation of this key</summary>
		/// <remarks>This method is "free" and will not cause any memory allocations</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice() => this.Data;

		/// <summary>Returns a pooled copy of the pre-encoded binary representation of this key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => pool is null ? SliceOwner.Wrap(this.Data) : SliceOwner.Copy(this.Data, pool);

		/// <inheritdoc cref="IFdbKey.Contains" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Slice key) => key.StartsWith(this.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key) => key.StartsWith(this.Data.Span);

		/// <inheritdoc />
		[Pure]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
		{
			return FdbKeyHelpers.IsChildOf(this.Span, key);
		}

		/// <summary>Extracts the suffix from a key that is a child of the current key</summary>
		/// <param name="key">Give that starts with the current key</param>
		/// <returns>Suffix part of the key</returns>
		/// <exception cref="ArgumentException">If <paramref name="key"/> is not a child of this key</exception>
		/// <remarks>Example:<code lang="c#">
		/// FdbKey.FromBytes("foo").GetSuffix(Slice.FromBytes("foobar")) // => `bar`
		/// FdbKey.FromBytes("foo").GetSuffix(Slice.FromBytes("foo")) // => ``
		/// FdbKey.FromBytes("foo").GetSuffix(Slice.FromBytes("bar")) // => throws
		/// </code></remarks>
		[Pure]
		public Slice GetSuffix(Slice key)
		{
			if (!TryGetSuffix(key, out var suffix))
			{
				throw new ArgumentException("The key does not have the expected prefix.", nameof(key));
			}
			return suffix;
		}

		/// <summary>Extracts the suffix from a key that is a child of the current key</summary>
		/// <param name="key">Give that starts with the current key</param>
		/// <returns>Suffix part of the key</returns>
		/// <exception cref="ArgumentException">If <paramref name="key"/> is not a child of this key</exception>
		/// <remarks>Example:<code lang="c#">
		/// FdbKey.FromBytes("foo").GetSuffix("foobar"u8) // => `bar`
		/// FdbKey.FromBytes("foo").GetSuffix("foo"u8) // => ``
		/// FdbKey.FromBytes("foo").GetSuffix("bar"u8) // => throws
		/// </code></remarks>
		[Pure]
		public ReadOnlySpan<byte> GetSuffix(ReadOnlySpan<byte> key)
		{
			if (!TryGetSuffix(key, out var suffix))
			{
				throw new ArgumentException("The key does not have the expected prefix.", nameof(key));
			}
			return suffix;
		}

		/// <summary>Extracts the suffix from a key that is a child of the current key</summary>
		/// <param name="key">Give that starts with the current key</param>
		/// <param name="suffix">Receives the suffix part of the key</param>
		/// <returns><c>true</c> if <paramref name="key"/> starts with this key's prefix; otherwise, <c>false</c></returns>
		/// <exception cref="ArgumentException">If <paramref name="key"/> is not a child of this key</exception>
		/// <remarks>Example:<code lang="c#">
		/// FdbKey.FromBytes("foo").TryGetSuffix(Slice.FromBytes("foobar"), out var suffix) // => true, suffix = `bar`
		/// FdbKey.FromBytes("foo").TryGetSuffix(Slice.FromBytes("foo"), out var suffix) // => true, suffix = ``
		/// FdbKey.FromBytes("foo").TryGetSuffix(Slice.FromBytes("bar"), out var suffix) // => false
		/// </code></remarks>
		[Pure]
		public bool TryGetSuffix(Slice key, out Slice suffix)
		{
			if (key.IsNull || key.Count < this.Data.Count || !key.StartsWith(this.Data))
			{
				suffix = default;
				return false;
			}

			suffix = key.Substring(this.Data.Count);
			return true;
		}

		/// <summary>Extracts the suffix from a key that is a child of the current key</summary>
		/// <param name="key">Give that starts with the current key</param>
		/// <param name="suffix">Receives the suffix part of the key</param>
		/// <returns><c>true</c> if <paramref name="key"/> starts with this key's prefix; otherwise, <c>false</c></returns>
		/// <exception cref="ArgumentException">If <paramref name="key"/> is not a child of this key</exception>
		/// <remarks>Example:<code lang="c#">
		/// FdbKey.FromBytes("foo").TryGetSuffix("foobar"u8, out var suffix) // => true, suffix = `bar`
		/// FdbKey.FromBytes("foo").TryGetSuffix("foo"u8, out var suffix) // => true, suffix = ``
		/// FdbKey.FromBytes("foo").TryGetSuffix("bar"u8, out var suffix) // => false
		/// </code></remarks>
		[Pure]
		public bool TryGetSuffix(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> suffix)
		{
			if (key.Length < this.Data.Count || !key.StartsWith(this.Span))
			{
				suffix = default;
				return false;
			}

			suffix = key[this.Data.Count..];
			return true;
		}

		#region IFdbKey.Key(...)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1>> Key<T1>(T1 item1)
			=> new(this.Data, new(item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2>> Key<T1, T2>(T1 item1, T2 item2)
			=> new(this.Data, new(item1, item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3>> Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
			=> new(this.Data, new(item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4>> Key<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
			=> new(this.Data, new(item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5>> Key<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
			=> new(this.Data, new(item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6>> Key<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
			=> new(this.Data, new(item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6, T7>> Key<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
			=> new(this.Data, new(item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Key<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
			=> new(this.Data, new(item1, item2, item3, item4, item5, item6, item7, item8));

		#endregion

		#region IFdbKey.Tuple(STuple<...>)

		/// <summary>Appends the packed elements of a tuple after the current key</summary>
		/// <typeparam name="TTuple">Type of the tuple</typeparam>
		/// <param name="items">Tuples with the items to append</param>
		/// <returns>New key that will append the <paramref name="items"/> at the end of the current key</returns>
		public FdbTupleSuffixKey<TTuple> Tuple<TTuple>(TTuple items)
			where TTuple : IVarTuple
		{
			return new(this.Data, items);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1>> Tuple<T1>(in STuple<T1> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2>> Tuple<T1, T2>(in STuple<T1, T2> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3>> Tuple<T1, T2, T3>(in STuple<T1, T2, T3> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4>> Tuple<T1, T2, T3, T4>(in STuple<T1, T2, T3, T4> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5>> Tuple<T1, T2, T3, T4, T5>(in STuple<T1, T2, T3, T4, T5> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6>> Tuple<T1, T2, T3, T4, T5, T6>(in STuple<T1, T2, T3, T4, T5, T6> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6, T7>> Tuple<T1, T2, T3, T4, T5, T6, T7>(in STuple<T1, T2, T3, T4, T5, T6, T7> items)
			=> new(this.Data, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Tuple<T1, T2, T3, T4, T5, T6, T7, T8>(in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
			=> new(this.Data, in items);

		#endregion

		#region IFdbKey.Tuple(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1>> Tuple<T1>(in ValueTuple<T1> items)
			=> new(this.Data, new(items.Item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2>> Tuple<T1, T2>(in ValueTuple<T1, T2> items)
			=> new(this.Data, new(items.Item1, items.Item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3>> Tuple<T1, T2, T3>(in ValueTuple<T1, T2, T3> items)
			=> new(this.Data, new(items.Item1, items.Item2, items.Item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4>> Tuple<T1, T2, T3, T4>(in ValueTuple<T1, T2, T3, T4> items)
			=> new(this.Data, new(items.Item1, items.Item2, items.Item3, items.Item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5>> Tuple<T1, T2, T3, T4, T5>(in ValueTuple<T1, T2, T3, T4, T5> items)
			=> new(this.Data, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6>> Tuple<T1, T2, T3, T4, T5, T6>(in ValueTuple<T1, T2, T3, T4, T5, T6> items)
			=> new(this.Data, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6, T7>> Tuple<T1, T2, T3, T4, T5, T6, T7>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> items)
			=> new(this.Data, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Tuple<T1, T2, T3, T4, T5, T6, T7, T8>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> items)
			=> new(this.Data, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7, items.Item8));

		#endregion


	}

}
