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
	using System.ComponentModel;

	/// <summary>Wraps a pre-encoded binary suffix and a Tuple suffix</summary>
	/// <typeparam name="TTuple">Type of the tuple suffix</typeparam>
	public readonly struct FdbTupleSuffixKey<TTuple> : IFdbKey
		, IEquatable<FdbTupleSuffixKey<TTuple>>, IComparable<FdbTupleSuffixKey<TTuple>>
		where TTuple : IVarTuple
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbTupleSuffixKey(Slice prefix, in TTuple suffix)
		{
			this.Prefix = prefix;
			this.Suffix = suffix;
		}

		public readonly Slice Prefix;

		public readonly TTuple Suffix;

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			return key.StartsWith(this.Prefix.Span) && FdbKeyHelpers.IsChildOf(in this, key);
		}

		/// <inheritdoc />
		[Pure]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var childSpan))
			{
				if (!childSpan.StartsWith(this.Prefix.Span))
				{
					return false;
				}
			}
			return FdbKeyHelpers.IsChildOf(in this, in key);
		}

		#endregion

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleSuffixKey<TTuple> key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleSuffixKey<TTuple> key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleSuffixKey<TTuple>))
			{
				return this.Prefix.Equals(((FdbTupleSuffixKey<TTuple>) (object) other).Prefix) && this.Suffix.Equals(((FdbTupleSuffixKey<TTuple>) (object) other).Suffix);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return Equals(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleSuffixKey<TTuple> other) => this.Prefix.Equals(other.Prefix) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => Equals(other.Span);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbTupleSuffixKey<TTuple> key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbTupleSuffixKey<TTuple> other)
		{
			var cmp = this.Prefix.CompareTo(other.Prefix);
			if (cmp == 0) cmp = this.Suffix.CompareTo(other.Suffix);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.Compare(in this, in other);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"{FdbKey.PrettyPrint(this.Prefix, FdbKey.PrettyPrintMode.Single)}.{this.Suffix}", out charsWritten);

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			//PERF: we don't have an easy way to estimate the size of a TTuple yet!
			sizeHint = 0;
			return false;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Prefix.TryCopyTo(destination, out var prefixLen)
			 && TupleEncoder.TryPackTo(destination[prefixLen..], out var suffixLen, default, this.Suffix))
			{
				bytesWritten = prefixLen + suffixLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

		#endregion

	}

	/// <summary>Wraps a parent key, followed by a Tuple suffix</summary>
	/// <typeparam name="TKey">Type of the parent key</typeparam>
	/// <typeparam name="TTuple">Type of the tuple suffix</typeparam>
	public readonly struct FdbTupleSuffixKey<TKey, TTuple> : IFdbKey
		, IEquatable<FdbTupleSuffixKey<TKey, TTuple>>, IComparable<FdbTupleSuffixKey<TKey, TTuple>>
		where TKey : struct, IFdbKey
		where TTuple : IVarTuple
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbTupleSuffixKey(TKey parent, in TTuple suffix)
		{
			this.Parent = parent;
			this.Suffix = suffix;
		}

		public readonly TKey Parent;

		public readonly TTuple Suffix;

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key) => FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleSuffixKey<TKey, TTuple> key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleSuffixKey<TKey, TTuple> key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleSuffixKey<TKey, TTuple>))
			{
				return FdbKeyHelpers.AreEqual(in this.Parent, in Unsafe.As<TOtherKey, FdbTupleSuffixKey<TKey, TTuple>>(ref Unsafe.AsRef(in other)).Parent) && this.Suffix.Equals(((FdbTupleSuffixKey<TKey, TTuple>) (object) other).Suffix);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.AreEqual(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleSuffixKey<TKey, TTuple> other) => FdbKeyHelpers.AreEqual(in this.Parent, in other.Parent) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbTupleSuffixKey<TKey, TTuple> key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbTupleSuffixKey<TKey, TTuple> other)
		{
			var cmp = FdbKeyHelpers.Compare(in this.Parent, in other.Parent);
			if (cmp == 0) cmp = this.Suffix.CompareTo(other.Suffix);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.Compare(in this, in other);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"{this.Parent}.{this.Suffix}", out charsWritten);

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			//PERF: we don't have an easy way to estimate the size of a TTuple yet!
			sizeHint = 0;
			return false;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Parent.TryEncode(destination, out var parentLen)
			 && TupleEncoder.TryPackTo(destination[parentLen..], out var suffixLen, default, this.Suffix))
			{
				bytesWritten = parentLen + suffixLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

		#endregion

	}
	
	/// <summary>Wraps a <see cref="Slice"/> that wraps a pre-encoded binary suffix, relative to a subspace</summary>
	public readonly struct FdbSuffixKey : IFdbKey
		, IEquatable<FdbSuffixKey>, IComparable<FdbSuffixKey>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbSuffixKey(IKeySubspace subspace, Slice suffix)
		{
			this.Subspace = subspace;
			this.Suffix = suffix;
		}

		/// <summary>Parent subspace</summary>
		public readonly IKeySubspace Subspace;

		/// <summary>Suffix added after the parent's prefix</summary>
		public readonly Slice Suffix;

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			var prefix = this.Subspace.GetPrefix().Span;
			var suffix = this.Suffix.Span;

			int length = checked(prefix.Length + suffix.Length);
			if (key.Length < length) return false;

			return key.StartsWith(prefix) && key[prefix.Length..].StartsWith(suffix);
		}

		/// <inheritdoc />
		[Pure]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		/// <inheritdoc cref="FdbKeyHelpers.ToSlice{TKey}(in TKey)"/>
		[Pure]
		public Slice ToSlice() => this.Subspace.GetPrefix() + this.Suffix;

		/// <inheritdoc cref="FdbKeyHelpers.ToSlice{TKey}(in TKey,System.Buffers.ArrayPool{byte}?)"/>
		[Pure]
		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			if (pool is null) return SliceOwner.Wrap(ToSlice());

			var prefix = this.Subspace.GetPrefix().Span;
			var suffix = this.Suffix.Span;
			int length = checked(prefix.Length + suffix.Length);

			var buffer = pool.Rent(length);
			prefix.CopyTo(buffer);
			suffix.CopyTo(buffer.AsSpan(0, prefix.Length));
			return SliceOwner.Create(buffer.AsSlice(0, length), pool);
		}

		#endregion

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbRawKey key => this.Equals(key),
			FdbSuffixKey key => this.Equals(key),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbRawKey key => this.Equals(key),
			FdbSuffixKey key => this.Equals(key),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSuffixKey))
			{
				return Equals((FdbSuffixKey) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return Equals((FdbRawKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSuffixKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other)
		{
			var otherSpan = other.Span;
			var suffixSpan = this.Subspace.GetPrefix().Span;
			return otherSpan.StartsWith(suffixSpan) && otherSpan[suffixSpan.Length..].SequenceEqual(suffixSpan);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other)
		{
			var otherSpan = other.Span;
			var suffixSpan = this.Subspace.GetPrefix().Span;
			return otherSpan.StartsWith(suffixSpan) && otherSpan[suffixSpan.Length..].SequenceEqual(suffixSpan);
		}

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			var suffixSpan = this.Subspace.GetPrefix().Span;
			return other.StartsWith(suffixSpan) && other[suffixSpan.Length..].SequenceEqual(suffixSpan);
		}

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbSuffixKey key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSuffixKey other)
		{
			var cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Suffix.CompareTo(other.Suffix);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.Compare(in this, in other);

		#endregion

		#region Key...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1>> Key<T1>(T1 item1) => new(this, new(item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2>> Key<T1, T2>(T1 item1, T2 item2) => new(this, new(item1, item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3>> Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(this, new(item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4>> Key<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(this, new(item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5>> Key<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(this, new(item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5, T6>> Key<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this, new(item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5, T6, T7>> Key<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this, new(item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Key<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this, new(item1, item2, item3, item4, item5, item6, item7, item8));

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"[{this.Subspace}] `{this.Suffix}`", out charsWritten);

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Subspace.GetPrefix().Count + this.Suffix.Count;
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Subspace.GetPrefix().TryCopyTo(destination, out var prefixLen)
			    && this.Suffix.TryCopyTo(destination[prefixLen..], out var dataLen))
			{
				bytesWritten = prefixLen + dataLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

		#endregion

	}

	/// <summary>Wraps another <see cref="IFdbKey"/>, with an added binary suffix</summary>
	/// <typeparam name="TKey">Type of the parent key</typeparam>
	public readonly struct FdbSuffixKey<TKey> : IFdbKey
		, IEquatable<FdbSuffixKey<TKey>>, IComparable<FdbSuffixKey<TKey>>
		where TKey : struct, IFdbKey
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbSuffixKey(in TKey parent, Slice suffix)
		{
			this.Parent = parent;
			this.Suffix = suffix;
		}

		/// <summary>Parent key</summary>
		public readonly TKey Parent;

		/// <summary>Suffix added after the key</summary>
		public readonly Slice Suffix;

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				Slice bytes => this.Equals(bytes),
				FdbSuffixKey<TKey> key => this.Equals(key),
				FdbRawKey key => this.Equals(key),
				IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
				_ => false,
			};
		}

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbKey? other)
		{
			return other switch
			{
				null => false,
				FdbSuffixKey<TKey> key => this.Equals(key),
				FdbRawKey key => this.Equals(key),
				_ => FdbKeyHelpers.AreEqual(in this, other),
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSuffixKey<TKey>))
			{
				return this.Suffix.Equals(((FdbSuffixKey<TKey>) (object) other).Suffix) && this.Parent.FastEqualTo(in Unsafe.As<TOtherKey, FdbSuffixKey<TKey>>(ref Unsafe.AsRef(in other)));
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.AreEqual(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSuffixKey<TKey> other) => this.Parent.FastEqualTo(in other.Parent) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbSuffixKey<TKey> key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSuffixKey<TKey> other)
		{
			int cmp = FdbKeyHelpers.Compare(in this.Parent, in other.Parent);
			if (cmp == 0) cmp = this.Suffix.Span.SequenceCompareTo(other.Suffix.Span);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.Compare(in this, in other);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"{this.Parent} + `{this.Suffix}`", out charsWritten);

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Parent.TryGetSizeHint(out var size))
			{
				sizeHint = 0;
				return false;
			}
			sizeHint = checked(size  + this.Suffix.Count);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Parent.TryEncode(destination, out var parentLen)
			 && this.Suffix.TryCopyTo(destination[parentLen..], out var dataLen))
			{
				bytesWritten = parentLen + dataLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

		#endregion

	}

}
