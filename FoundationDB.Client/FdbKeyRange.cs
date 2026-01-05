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
	using System;
	using System.ComponentModel;

	/// <summary>Helpers for creating <see cref="IFdbKeyRange"/> instances</summary>
	public static class FdbKeyRange
	{

		/// <summary>Returns a range that matches only this key</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that will be matched</param>
		/// <returns>Range that match only this key</returns>
		/// <remarks>
		/// <para>Ex: <c>FdbKeyRange.Single(subspace.GetKey(123))</c> will match only <c>(..., 123)</c>, and will exclude any of its children <c>(..., 123, ...)</c>.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyRange<TKey, TKey> Single<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> new(key, KeyRangeMode.Inclusive, key, KeyRangeMode.Inclusive);

		/// <summary>Returns a range that will match all keys between two bounds</summary>
		/// <param name="lowerInclusive">Inclusive lower bound</param>
		/// <param name="upperExclusive">Exclusive upper bound</param>
		public static FdbRawKeyRange Between(Slice lowerInclusive, Slice upperExclusive) => new(lowerInclusive, upperExclusive);

		/// <summary>Returns a range that will match all keys between this lower bound key and the given upper bound key</summary>
		/// <typeparam name="TFromKey">Type of the lower bound key</typeparam>
		/// <typeparam name="TToKey">Type of the upper bound key</typeparam>
		/// <param name="lowerInclusive">Inclusive lower bound</param>
		/// <param name="upperExclusive">Exclusive upper bound</param>
		public static FdbKeyRange<TFromKey, TToKey> Between<TFromKey, TToKey>(TFromKey lowerInclusive, in TToKey upperExclusive)
			where TFromKey : struct, IFdbKey
			where TToKey : struct, IFdbKey
		{
			return new(lowerInclusive, lowerMode: KeyRangeMode.Inclusive, upperExclusive, upperMode: KeyRangeMode.Exclusive);
		}

		/// <summary>Returns a range that will match all keys between this lower bound key and the given upper bound key</summary>
		/// <typeparam name="TLowerKey">Type of the lower bound key</typeparam>
		/// <typeparam name="TUpperKey">Type of the upper bound key</typeparam>
		/// <param name="lowerKey">Key used to compute the lower bound of the range</param>
		/// <param name="lowerMode">Transformation applied to <paramref name="lowerKey"/></param>
		/// <param name="upperKey">Key used to compute the upper bound of the range</param>
		/// <param name="upperMode">Transformation applied to <paramref name="upperKey"/></param>
		public static FdbKeyRange<TLowerKey, TUpperKey> Between<TLowerKey, TUpperKey>(in TLowerKey lowerKey, KeyRangeMode lowerMode, TUpperKey upperKey, KeyRangeMode upperMode)
			where TLowerKey : struct, IFdbKey
			where TUpperKey : struct, IFdbKey
		{
			return new(lowerKey, lowerMode, upperKey, upperMode);
		}

		/// <summary>Returns a range that matches all the children of this key</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that will be used as a prefix</param>
		/// <param name="inclusive">If <c>true</c> the key itself will be included in the range; otherwise, the range will start immediately after this key.</param>
		/// <returns>Range that matches all keys that start with <paramref name="key"/> (included)</returns>
		/// <remarks>
		/// <para>Ex: <c>FdbKeyRange.StartsWith(subspace.GetKey(123))</c> will match all the keys of the form <c>(..., 123, ...)</c>, but not <c>(..., 123)</c> itself.</para>
		/// <para>Ex: <c>FdbKeyRange.StartsWith(subspace.GetKey(123), inclusive: true)</c> will match <c>(..., 123)</c> as well as all the keys of the form <c>(..., 123, ...)</c>.</para>
		/// </remarks>
		public static FdbKeyRange<TKey, TKey> StartsWith<TKey>(in TKey key, bool inclusive = false)
			where TKey : struct, IFdbKey
			=> new(key, KeyRangeMode.Inclusive, key, KeyRangeMode.NextSibling);

	}

	/// <summary>Range that matches all keys between a lower and upper bound</summary>
	/// <typeparam name="TLowerKey">Type of the lower bound key</typeparam>
	/// <typeparam name="TUpperKey">Type of the upper bound key</typeparam>
	/// <seealso cref="IFdbKeyRange"/>
	/// <seealso cref="KeyRangeMode"/>
	public readonly struct FdbKeyRange<TLowerKey, TUpperKey> : IFdbKeyRange, IEquatable<FdbKeyRange<TLowerKey, TUpperKey>>
		where TLowerKey : struct, IFdbKey
		where TUpperKey : struct, IFdbKey
	{

		/// <summary>Constructs a new <see cref="FdbKeyRange{TLowerKey,TUpperKey}"/></summary>
		/// <param name="lowerKey">Key that will be used to compute the lower bound</param>
		/// <param name="lowerMode">Transformation that will be applied to <paramref name="lowerKey"/></param>
		/// <param name="upperKey">Key that will be used to compute the upper bound</param>
		/// <param name="upperMode">Transformation that will be applied to <paramref name="upperKey"/></param>
		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyRange(in TLowerKey lowerKey, KeyRangeMode lowerMode, in TUpperKey upperKey, KeyRangeMode upperMode)
		{
			this.LowerKey = lowerKey;
			this.UpperKey = upperKey;
			this.LowerMode = lowerMode;
			this.UpperMode = upperMode;
		}

		/// <summary>Key that is used to compute the lower bound of this range</summary>
		public readonly TLowerKey LowerKey;

		/// <summary>Transformation applied to the lower key to produce the effective lower bound (Inclusive by default)</summary>
		public readonly KeyRangeMode LowerMode;

		/// <summary>Key that is used to compute the upper bound of this range</summary>
		public readonly TUpperKey UpperKey;

		/// <summary>Transformation applied to the upper key to produce the effective upper bound (Exclusive by default)</summary>
		public readonly KeyRangeMode UpperMode;

		/// <summary>Returns the inclusive "Begin" key of this range</summary>
		public BeginKey Begin() => new(in this);

		/// <summary>Returns the exclusive "End" key of this range</summary>
		public EndKey End() => new(in this);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => KeyRange.Create(ToBeginKey(), ToEndKey());

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public Slice ToBeginKey() => this.LowerMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Inclusive => FdbKeyHelpers.ToSlice(in this.LowerKey),
			KeyRangeMode.Exclusive => FdbKeyHelpers.ToSlice(this.LowerKey.Successor()),
			KeyRangeMode.NextSibling => FdbKeyHelpers.ToSlice(this.LowerKey.NextSibling()),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public Slice ToEndKey() => this.UpperMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Exclusive => FdbKeyHelpers.ToSlice(this.UpperKey),
			KeyRangeMode.Inclusive => FdbKeyHelpers.ToSlice(this.UpperKey.Successor()),
			KeyRangeMode.NextSibling => FdbKeyHelpers.ToSlice(this.UpperKey.NextSibling()),
			KeyRangeMode.Last => FdbKeyHelpers.ToSlice(this.UpperKey.Last()),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public KeySelector ToBeginSelector() => this.LowerMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Inclusive => this.LowerKey.FirstGreaterOrEqual().ToSelector(),
			KeyRangeMode.Exclusive => this.LowerKey.FirstGreaterThan().ToSelector(),
			KeyRangeMode.NextSibling => this.LowerKey.NextSibling().FirstGreaterOrEqual().ToSelector(),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public KeySelector ToEndSelector() => this.UpperMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Exclusive => this.UpperKey.FirstGreaterOrEqual().ToSelector(),
			KeyRangeMode.Inclusive => this.UpperKey.FirstGreaterThan().ToSelector(),
			KeyRangeMode.NextSibling => this.UpperKey.NextSibling().FirstGreaterOrEqual().ToSelector(),
			KeyRangeMode.Last => this.UpperKey.Last().FirstGreaterOrEqual().ToSelector(),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Tests if the key is before (<c>-1</c>), after (<c>+1</c>) or contained in the range (<c>0</c>)</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>-1</c> if the key is before the range, <c>0</c> if the key is in the range, and <c>+1</c> if the key is after the range.</returns>
		[Pure]
		public int Test<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.Begin().FastCompareTo(in key) > 0 ? -1
			 : this.End().FastCompareTo(in key) <= 0 ? +1
			 : 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains(Slice key)
			=> this.Begin().CompareTo(key.Span) <= 0 && this.End().CompareTo(key.Span) > 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key)
			=> this.Begin().CompareTo(key) <= 0 && this.End().CompareTo(key) > 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.Begin().FastCompareTo(in key) <= 0 && this.End().FastCompareTo(in key) > 0;

		/// <summary>Tests if the given key is strictly before this range</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is less than the effective lower bound of the range.</returns>
		[Pure]
		public bool IsBefore<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.Begin().FastCompareTo(in key) > 0;

		/// <summary>Tests if the given key is strictly before this range</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is less than the effective lower bound of the range.</returns>
		[Pure]
		public bool IsBefore(Slice key)
			=> this.Begin().CompareTo(key) > 0;

		/// <summary>Tests if the given key is strictly after this range</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is greater than or equal to the effective upper bound of the range.</returns>
		[Pure]
		public bool IsAfter<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.End().FastCompareTo(in key) <= 0;

		/// <summary>Tests if the given key is strictly after this range</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is greater than or equal to the effective upper bound of the range.</returns>
		[Pure]
		public bool IsAfter(Slice key)
			=> this.End().CompareTo(key) <= 0;

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		public string ToString(string? format, IFormatProvider? provider = null) => format switch
		{
			null or "" or "D" or "d" => string.Create(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin()} <= k < {End()} }}"),
			"K" or "k" => string.Create(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():B} <= k < {End():E} }}"),
			"P" or "p" => string.Create(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():P} <= k < {End():P} }}"),
			_ => throw new FormatException("Unsupported format."),
		};

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin()} <= k < {End()} }}", out charsWritten),
			"K" or "k" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():B} <= k < {End():E} }}", out charsWritten),
			"P" or "p" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():P} <= k < {End():P} }}", out charsWritten),
			_ => throw new FormatException("Unsupported format."),
		};

		public bool Equals(FdbKeyRange<TLowerKey, TUpperKey> other)
		{
			return this.LowerMode == other.LowerMode && this.UpperMode == other.UpperMode && this.LowerKey.FastEqualTo(in other.LowerKey) && this.UpperKey.FastEqualTo(in other.UpperKey);
		}

		/// <summary>Represents the "Begin" key of a key range</summary>
		public readonly struct BeginKey : IFdbKey
			, IEquatable<BeginKey>, IComparable<BeginKey>
			, IEquatable<FdbSuccessorKey<TLowerKey>>
		{

			[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public BeginKey(in FdbKeyRange<TLowerKey, TUpperKey> range)
			{
				this.Range = range;
			}

			/// <summary>Parent range</summary>
			public readonly FdbKeyRange<TLowerKey, TUpperKey> Range;

			#region IFdbKey...

			/// <inheritdoc />
			public IKeySubspace? GetSubspace() => this.Range.LowerKey.GetSubspace();

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Contains(ReadOnlySpan<byte> key)
				=> FdbKeyHelpers.IsChildOf(in this, key);

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Contains<TOtherKey>(in TOtherKey key)
				where TOtherKey : struct, IFdbKey
				=> FdbKeyHelpers.IsChildOf(in this, in key);

			#endregion

			#region Equals()...

			/// <inheritdoc />
			public override int GetHashCode() => this.Range.GetHashCode();

			/// <inheritdoc />
			public override bool Equals([NotNullWhen(true)] object? other) => other switch
			{
				BeginKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				Slice bytes => this.Equals(bytes.Span),
				IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
				_ => false,
			};

			/// <inheritdoc />
			[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
			public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
			{
				null => false,
				BeginKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				_ => FdbKeyHelpers.AreEqual(in this, other),
			};

			/// <inheritdoc />
			public bool Equals(BeginKey other) => this.Range.Equals(other.Range);

			/// <inheritdoc />
			public bool Equals(FdbRawKey other) => Equals(other.Span);

			/// <inheritdoc />
			public bool Equals(FdbSuccessorKey<TLowerKey> other)
				=> this.Range.LowerMode == KeyRangeMode.Exclusive
					? this.Range.LowerKey.Equals(other.Parent)
					: FdbKeyHelpers.AreEqual(in this, other);

			/// <inheritdoc />
			public bool Equals(Slice other) => Equals(other.Span);

			/// <inheritdoc cref="Equals(Slice)" />
			public bool Equals(ReadOnlySpan<byte> other) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.Equals(other),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().Equals(other),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().Equals(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool FastEqualTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(BeginKey))
				{
					return Equals((BeginKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return Equals(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.AreEqual(in this, in other);
			}

			/// <inheritdoc />
			public int CompareTo(object? obj) => obj switch
			{
				Slice key => CompareTo(key.Span),
				FdbRawKey key => CompareTo(key.Span),
				BeginKey key => CompareTo(key),
				IFdbKey other => FdbKeyHelpers.Compare(in this, other),
				_ => throw new NotSupportedException()
			};

			/// <inheritdoc />
			public int CompareTo(BeginKey other) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.FastCompareTo(in other),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().FastCompareTo(in other),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().FastCompareTo(in other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int CompareTo(FdbRawKey other) => CompareTo(other.Span);

			/// <inheritdoc />
			public int CompareTo(Slice other) => CompareTo(other.Span);

			/// <inheritdoc cref="CompareTo(Slice)" />
			public int CompareTo(ReadOnlySpan<byte> other) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.CompareTo(other),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().CompareTo(other),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().CompareTo(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int FastCompareTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(BeginKey))
				{
					return CompareTo((BeginKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return CompareTo(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.Compare(in this, in other);
			}

			#endregion

			#region Formatting...

			/// <inheritdoc />
			public override string ToString() => ToString(null);

			/// <inheritdoc />
			public string ToString(string? format, IFormatProvider? formatProvider = null) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.ToString(format, formatProvider),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().ToString(format, formatProvider),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().ToString(format, formatProvider),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().TryFormat(destination, out charsWritten, format, provider),
				_ => throw new NotSupportedException(),
			};

			#endregion

			#region ISpanEncodable...

			/// <inheritdoc />
			public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

			/// <inheritdoc />
			public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

			/// <inheritdoc />
			public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.TryEncode(destination, out bytesWritten),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().TryEncode(destination, out bytesWritten),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().TryEncode(destination, out bytesWritten),
				_ => throw new NotSupportedException(),
			};

			#endregion

		}

		/// <summary>Represents the "End" key of a key range</summary>
		public readonly struct EndKey : IFdbKey
			, IEquatable<EndKey>, IComparable<EndKey>
			, IEquatable<FdbNextSiblingKey<TUpperKey>>
			, IEquatable<FdbLastKey<TUpperKey>>
		{

			public EndKey(in FdbKeyRange<TLowerKey, TUpperKey> range)
			{
				this.Range = range;
			}

			public readonly FdbKeyRange<TLowerKey, TUpperKey> Range;

			#region IFdbKey...

			/// <inheritdoc />
			public IKeySubspace? GetSubspace() => this.Range.UpperKey.GetSubspace();

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Contains(ReadOnlySpan<byte> key)
				=> FdbKeyHelpers.IsChildOf(in this, key);

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Contains<TOtherKey>(in TOtherKey key)
				where TOtherKey : struct, IFdbKey
				=> FdbKeyHelpers.IsChildOf(in this, in key);

			#endregion

			#region Equals()...

			/// <inheritdoc />
			[EditorBrowsable(EditorBrowsableState.Never)]
			public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

			/// <inheritdoc />
			public override bool Equals([NotNullWhen(true)] object? other) => other switch
			{
				EndKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				Slice bytes => this.Equals(bytes.Span),
				IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
				_ => false,
			};

			/// <inheritdoc />
			[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
			public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
			{
				null => false,
				EndKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				_ => FdbKeyHelpers.AreEqual(in this, other),
			};

			/// <inheritdoc />
			public bool Equals(EndKey other) => this.Range.Equals(other.Range);

			/// <inheritdoc />
			public bool Equals(FdbRawKey other) => Equals(other.Span);

			/// <inheritdoc />
			public bool Equals(FdbNextSiblingKey<TUpperKey> other)
				=> this.Range.UpperMode == KeyRangeMode.NextSibling
					? this.Range.UpperKey.Equals(other.Parent)
					: FdbKeyHelpers.AreEqual(in this, other);

			/// <inheritdoc />
			public bool Equals(FdbLastKey<TUpperKey> other)
				=> this.Range.UpperMode == KeyRangeMode.Last
					? this.Range.UpperKey.Equals(other.Parent)
					: FdbKeyHelpers.AreEqual(in this, other);

			/// <inheritdoc />
			public bool Equals(Slice other) => Equals(other.Span);

			/// <inheritdoc cref="Equals(Slice)" />
			public bool Equals(ReadOnlySpan<byte> other) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.Equals(other),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().Equals(other),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().Equals(other),
				KeyRangeMode.Last => this.Range.UpperKey.Last().Equals(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool FastEqualTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(EndKey))
				{
					return Equals((EndKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return Equals(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.AreEqual(in this, in other);
			}

			/// <inheritdoc />
			public int CompareTo(object? obj) => obj switch
			{
				Slice key => CompareTo(key.Span),
				FdbRawKey key => CompareTo(key.Span),
				EndKey key => CompareTo(key),
				IFdbKey other => FdbKeyHelpers.Compare(in this, other),
				_ => throw new NotSupportedException()
			};

			/// <inheritdoc />
			public int CompareTo(EndKey other) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.FastCompareTo(in other),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().FastCompareTo(in other),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().FastCompareTo(in other),
				KeyRangeMode.Last => this.Range.UpperKey.Last().FastCompareTo(in other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int CompareTo(FdbRawKey other) => CompareTo(other.Span);

			/// <inheritdoc />
			public int CompareTo(Slice other) => CompareTo(other.Span);

			/// <inheritdoc cref="CompareTo(Slice)" />
			public int CompareTo(ReadOnlySpan<byte> other) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.CompareTo(other),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().CompareTo(other),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().CompareTo(other),
				KeyRangeMode.Last => this.Range.UpperKey.Last().CompareTo(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int FastCompareTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(EndKey))
				{
					return CompareTo((EndKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return CompareTo(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.Compare(in this, in other);
			}

			#endregion

			#region Formatting...

			/// <inheritdoc />
			public override string ToString() => ToString(null);

			/// <inheritdoc />
			public string ToString(string? format, IFormatProvider? formatProvider = null) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.ToString(format, formatProvider),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().ToString(format, formatProvider),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().ToString(format, formatProvider),
				KeyRangeMode.Last => this.Range.UpperKey.Last().ToString(format, formatProvider),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.Last => this.Range.UpperKey.Last().TryFormat(destination, out charsWritten, format, provider),
				_ => throw new NotSupportedException(),
			};

			#endregion

			#region ISpanEncodable...

			/// <inheritdoc />
			public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

			/// <inheritdoc />
			public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

			/// <inheritdoc />
			public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.TryEncode(destination, out bytesWritten),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().TryEncode(destination, out bytesWritten),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().TryEncode(destination, out bytesWritten),
				KeyRangeMode.Last => this.Range.UpperKey.Last().TryEncode(destination, out bytesWritten),
				_ => throw new NotSupportedException(),
			};

			#endregion

		}

	}

}
