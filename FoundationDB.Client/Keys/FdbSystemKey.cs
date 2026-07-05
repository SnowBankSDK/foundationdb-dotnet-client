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

	/// <summary>Wraps a <see cref="Slice"/> that wraps a key in either the System (<c>`\xFF`</c>) or Special Key (<c>`\xFF\xFF`</c>) subspaces</summary>
	public readonly struct FdbSystemKey : IFdbKey
		, IEquatable<FdbSystemKey>, IComparable<FdbSystemKey>
	{

		public static readonly FdbSystemKey System = new(Slice.Empty, special: false);

		public static readonly FdbSystemKey Special = new(Slice.Empty, special: true);

		#region Well Known Keys...

		/// <summary><c>`\xFF/metadataVersion`</c>: contains the global metadata version for the database</summary>
		/// <remarks>See <see cref="IFdbReadOnlyTransaction.GetMetadataVersionKeyAsync"/> and <see cref="IFdbTransaction.TouchMetadataVersionKey"/> for more details</remarks>
		public static readonly FdbSystemKey MetadataVersion = new("/metadataVersion", special: false);

		/// <summary><c>`\xFF\xFF/status/json</c>: returns a JSON document describing the current status of the client and cluster.</summary>
		/// <remarks>See <see cref="Fdb.System.GetStatusAsync(IFdbReadOnlyTransaction)"/> for more details.</remarks>
		public static readonly FdbSystemKey StatusJson = new("/status/json", special: true);

		/// <summary><c>`\xFF\xFF/transaction/conflicting_keys/...`</c></summary>
		public static readonly FdbSystemKey TransactionConflictingKeys = new("/transaction/conflicting_keys/", special: true);

		/// <summary><c>`\xFF/dbLocked`</c></summary>
		public static readonly FdbSystemKey DbLocked = new("/dbLocked", false);

		/// <summary><c>`\xFF/clusterIdKey`</c>: Durable cluster ID key.</summary>
		/// <remarks>Added "Key" to the end to differentiate from the key // "\xff/clusterId" which was stored in the txnStateStore in FDB 7.1, whereas this key is stored in the database in 7.2+.</remarks>
		public static readonly FdbSystemKey ClusterIdKey = new("/clusterIdKey", false);

		/// <summary><c>`\xFF/logs`</c></summary>
		public static readonly FdbSystemKey Logs = new("/logs", false); // see decodeLogsValue() in SystemData.cpp

		/// <summary><c>`\xFF/backupEnabled`</c></summary>
		public static readonly FdbSystemKey BackupEnabled = new("/backupEnabled", false);

		/// <summary><c>`\xFF/backupDataFormat`</c></summary>
		public static readonly FdbSystemKey BackupDataFormat = new("/backupDataFormat", false);

		/// <summary><c>`\xFF/minRequiredCommitVersion`</c></summary>
		public static readonly FdbSystemKey MinRequiredCommitVersion = new("/minRequiredCommitVersion", false);

		/// <summary><c>`\xFF/dataDistributionMode`</c></summary>
		public static readonly FdbSystemKey DataDistributionMode = new("/dataDistributionMode", false);

		/// <summary><c>`\xFF/bulkLoadMode`</c></summary>
		public static readonly FdbSystemKey BulkLoadMode = new("/bulkLoadMode", false);

		/// <summary><c>`\xFF/bulkDumpMode`</c></summary>
		public static readonly FdbSystemKey BulkDumpMode = new("/bulkDumpMode", false);

		/// <summary><c>`\xFF/versionEpoch`</c></summary>
		public static readonly FdbSystemKey VersionEpoch = new("/versionEpoch", false);

		/// <summary><c>`\xFF/globals/lastEpochEnd`</c></summary>
		public static readonly FdbSystemKey LastEpochEnd = new("/globals/lastEpochEnd", false);

		/// <summary><c>`\xFF\xFF/globals/lastEpochEnd`</c></summary>
		public static readonly FdbSystemKey LastEpochEndPrivate = new("/globals/lastEpochEnd", true);

		/// <summary><c>`\xFF/globals/killStorage`</c></summary>
		public static readonly FdbSystemKey KillStorage = new("/globals/killStorage", false);

		/// <summary><c>`\xFF\xFF/globals/killStorage`</c></summary>
		public static readonly FdbSystemKey KillStoragePrivate = new("/globals/killStorage", true);

		/// <summary><c>`\xFF/globals/killStorage`</c></summary>
		public static readonly FdbSystemKey RebootWhenDurable = new("/globals/rebootWhenDurable", false);

		/// <summary><c>`\xFF\xFF/globals/killStorage`</c></summary>
		public static readonly FdbSystemKey RebootWhenDurablePrivate = new("/globals/rebootWhenDurable", true);

		/// <summary><c>`\xFF/globals/primaryLocality`</c></summary>
		public static readonly FdbSystemKey PrimaryLocality = new("/globals/primaryLocality", false);

		/// <summary><c>`\xFF\xFF/globals/primaryLocality`</c></summary>
		public static readonly FdbSystemKey PrimaryLocalityPrivate = new("/globals/primaryLocality", true);

		/// <summary><c>`\xFF/globals/fastLoggingEnabled`</c></summary>
		public static readonly FdbSystemKey FastLoggingEnabled = new("/globals/fastLoggingEnabled", false);

		/// <summary><c>`\xFF\xFF/globals/fastLoggingEnabled`</c></summary>
		public static readonly FdbSystemKey FastLoggingEnabledPrivate = new("/globals/fastLoggingEnabled", true);

		/// <summary><c>`\xFF/globals/constructData`</c></summary>
		public static readonly FdbSystemKey ConstructData = new("/globals/constructData", false); // see decodeConstructKeys() in SystemData.cpp

		#endregion

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbSystemKey(Slice suffix, bool special)
		{
			this.IsSpecial = special;
			this.SuffixBytes = suffix;
			this.SuffixString = null;

			Contract.Debug.Ensures(this.IsSpecial || !this.SuffixBytes.StartsWith(0xFF));
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbSystemKey(string suffix, bool special)
		{
			this.IsSpecial = special;
			this.SuffixBytes = default;
			this.SuffixString = suffix;

			Contract.Debug.Ensures(this.IsSpecial || !this.SuffixString.StartsWith('\xFF'));
		}

		/// <summary>If <c>true</c>, uses the <c>`\xFF\xFF`</c> prefix (for special keys); otherwise, uses the <c>`\xFF`</c> prefix (for regular system keys)</summary>
		public readonly bool IsSpecial;

		/// <summary>Rest of the key (as bytes)</summary>
		/// <remarks>This is only present if <see cref="SuffixString"/> is <c>null</c></remarks>
		public readonly Slice SuffixBytes;

		/// <summary>Rest of the key (as raw string)</summary>
		/// <remarks>If <c>null</c> then <see cref="SuffixBytes"/> contains the rest of the key</remarks>
		public readonly string? SuffixString;

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			if (key.Length == 0 || key[0] != 0xFF)
			{
				return false;
			}

			if (this.IsSpecial && (key.Length < 2 || key[1] != 0xFF))
			{
				return false;
			}

			return this.SuffixString is null
				? key[(this.IsSpecial ? 2 : 1)..].StartsWith(key)
				: FdbKeyHelpers.IsChildOf(in this, key);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		/// <inheritdoc cref="FdbKeyHelpers.ToSlice{TKey}(in TKey)"/>
		public Slice ToSlice()
		{
			if (this.SuffixString is not null) return FdbKeyHelpers.ToSlice(in this);

			var suffix = this.SuffixBytes.Span;
			var res = new byte[checked((this.IsSpecial ? 2 : 1) + suffix.Length)];
			if (this.IsSpecial)
			{
				res[0] = 0xFF;
				res[1] = 0xFF;
				suffix.CopyTo(res.AsSpan(2));
			}
			else
			{
				res[0] = 0xFF;
				suffix.CopyTo(res.AsSpan(1));
			}
			return res.AsSlice();
		}

		/// <inheritdoc cref="FdbKeyHelpers.ToSlice{TKey}(in TKey)"/>
		[MustDisposeResource]
		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			if (pool is null) return SliceOwner.Wrap(ToSlice());
			if (this.SuffixString is not null) return FdbKeyHelpers.ToSlice(in this, pool);

			var suffix = this.SuffixBytes.Span;
			int length = checked((this.IsSpecial ? 2 : 1) + suffix.Length);
			var res = pool.Rent(length);
			if (this.IsSpecial)
			{
				res[0] = 0xFF;
				res[1] = 0xFF;
				suffix.CopyTo(res.AsSpan(2));
			}
			else
			{
				res[0] = 0xFF;
				suffix.CopyTo(res.AsSpan(1));
			}
			return SliceOwner.Create(res.AsSlice(0, length), pool);
		}

		#endregion

		#region Bytes(...)

		/// <summary>Appends the bytes of another key after the current key</summary>
		/// <param name="suffix">Key whose binary representation will be appended to the current key</param>
		public FdbSystemKey Bytes(FdbRawKey suffix) => Bytes(suffix.Data);

		/// <summary>Appends the bytes of another key after the current key</summary>
		/// <param name="suffix">Bytes to append to the current key</param>
		[Pure]
		public FdbSystemKey Bytes(Slice suffix)
		{
			if (suffix.Count == 0)
			{
				return this;
			}

			bool special = this.IsSpecial;
			if (!special && suffix.StartsWith(0xFF))
			{ // promote to SpecialKey
				special = true;
				suffix = suffix.Substring(1);
			}

			return this.SuffixString is null
				? new(this.SuffixBytes + suffix, special)
				: new(Slice.FromStringAscii(this.SuffixString) + suffix, special);
		}

		/// <summary>Appends the bytes of another key after the current key</summary>
		/// <param name="suffix">Bytes to append to the current key</param>
		[Pure]
		public FdbSystemKey Bytes(ReadOnlySpan<byte> suffix)
		{
			if (suffix.Length == 0)
			{
				return this;
			}

			bool special = this.IsSpecial;
			if (!special && suffix[0] == 0xFF)
			{ // promote to SpecialKey
				special = true;
				suffix = suffix[1..];
			}

			return this.SuffixString is null
				? new(this.SuffixBytes.Concat(suffix), special)
				: new(Slice.FromStringAscii(this.SuffixString).Concat(suffix), special);
		}

		/// <summary>Appends an ASCII string after the current key</summary>
		/// <param name="suffix">String that must only contain ASCII bytes (0..127).</param>
		/// <remarks>Suffix must only contain 7-bit ASCII characters. The only exception is the first character can be <c>\xFF</c> when appending to the System key (\xFF)</remarks>
		[Pure]
		public FdbSystemKey Bytes(string suffix)
		{
			if (string.IsNullOrEmpty(suffix))
			{
				return this;
			}

			if (this.SuffixString is null)
			{
				return Bytes(Slice.FromStringAscii(suffix));
			}

			bool special = IsSpecial;
			if (!special && suffix[0] == '\xFF')
			{ // promote to SpecialKey
				special = true;
				suffix = suffix[1..];
			}

			return new(this.SuffixString + suffix, special);
		}

		/// <summary>Appends an ASCII string after the current key</summary>
		/// <param name="suffix">String that must only contain ASCII bytes (0..127).</param>
		/// <remarks>Suffix must only contain 7-bit ASCII characters. The only exception is the first character can be <c>\xFF</c> when appending to the System key (\xFF)</remarks>
		[Pure]
		public FdbSystemKey Bytes(ReadOnlySpan<char> suffix)
		{
			if (suffix.Length == 0)
			{
				return this;
			}

			if (this.SuffixString is null)
			{
				return Bytes(Slice.FromStringAscii(suffix));
			}

			bool special = IsSpecial;
			if (!special && suffix[0] == '\xFF')
			{ // promote to SpecialKey
				special = true;
				suffix = suffix[1..];
			}

#if NETCOREAPP3_0_OR_GREATER
			return new(string.Concat(this.SuffixString, suffix), special);
#else
			// string.Concat(string, ReadOnlySpan<char>) is not available: materialize the span into a string first (which allocates)
			return new(string.Concat(this.SuffixString, suffix.ToString()), special);
#endif
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
			FdbSystemKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbSystemKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSystemKey))
			{
				return Equals((FdbSystemKey) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return Equals(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSystemKey other)
		{
			if (this.IsSpecial != other.IsSpecial)
			{
				return false;
			}

			return (this.SuffixString, other.SuffixString) switch
			{
				(not null, not null) => this.SuffixString == other.SuffixString,
				(null, null) => this.SuffixBytes.Equals(other.SuffixBytes),
				_ => FdbKeyHelpers.AreEqual(in this, in other)
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => Equals(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => !other.IsNull && Equals(other.Span);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			if (this.SuffixString is not null)
			{ // no easy fast path
				return FdbKeyHelpers.AreEqual(in this, other);
			}
			return this.IsSpecial
				? other.Length >= 2 && other[0] == 0xFF && other[1] == 0xFF && other[2..].SequenceEqual(this.SuffixBytes.Span)
				: other.Length >= 1 && other[0] == 0xFF && other[1..].SequenceEqual(this.SuffixBytes.Span);
		}

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbSystemKey key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSystemKey other)
		{
			switch (this.IsSpecial, other.IsSpecial)
			{
				case (false, false) or (true, true):
				{
					switch (this.SuffixString, other.SuffixString)
					{
						case (not null, not null):
						{
							return string.CompareOrdinal(this.SuffixString, other.SuffixString);
						}
						case (null, null):
						{
							return this.SuffixBytes.CompareTo(other.SuffixBytes);
						}
						default:
						{
							// fallback: slow path
							return FdbKeyHelpers.Compare(in this, in other);
						}
					}
				}
				case (false, true):
				{ // SystemKey < SpecialKey
					return -1;
				}
				case (true, false):
				{ // SpecialKey > SystemKey
					return +1;
				}
			}

		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other) => CompareTo(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other) => CompareTo(other.Span);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
		{
			if (other.Length == 0 || other[0] != 0xFF)
			{ // Regular Key < System Key
				return +1;
			}

			if (this.IsSpecial)
			{ // \xFF\xFF...
				if (other.Length > 1 && other[1] != 0xFF)
				{
					return +1;
				}

				if (this.SuffixString is null)
				{
					return this.SuffixBytes.CompareTo(other[2..]);
				}
			}
			else
			{ // \xFF...
				if (this.SuffixString is null)
				{
					return this.SuffixBytes.CompareTo(other[1..]);
				}
			}

			// fallback to slow path
			return FdbKeyHelpers.Compare(in this, other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSystemKey))
			{
				return CompareTo((FdbSystemKey) (object) other);
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
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> this.SuffixString is not null
				? (this.SuffixString.Length == 0
					? (this.IsSpecial ? "<FF><FF>" : "<FF>").TryCopyTo(destination, out charsWritten)
					: destination.TryWrite(provider, $"{(this.IsSpecial ? "<FF><FF>" : "<FF>")}{Slice.FromStringAscii(this.SuffixString):R}", out charsWritten)) //PERF: optimize if string is "clean" (nothing encoded as <XX>)
				: (this.SuffixBytes.Count == 0
					? (this.IsSpecial ? "<FF><FF>" : "<FF>").TryCopyTo(destination, out charsWritten)
					: destination.TryWrite(provider, $"{(this.IsSpecial ? "<FF><FF>" : "<FF>")}{this.SuffixBytes:R}", out charsWritten));

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
			sizeHint = checked((this.IsSpecial ? 2 : 1) + (this.SuffixString?.Length ?? this.SuffixBytes.Count));
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.SuffixString is null)
			{ // we already have the encoded bytes

				if (this.IsSpecial)
				{
					if (destination.Length <= 2 || !this.SuffixBytes.TryCopyTo(destination[2..], out var dataLen))
					{
						goto too_small;
					}

					destination[0] = 0xFF;
					destination[1] = 0xFF;
					bytesWritten = 2 + dataLen;
					return true;
				}
				else
				{
					if (destination.Length <= 1 || !this.SuffixBytes.TryCopyTo(destination[1..], out var dataLen))
					{
						goto too_small;
					}

					destination[0] = 0xFF;
					bytesWritten = 1 + dataLen;
					return true;
				}
			}

			// we need to encode the string "in place"
			var tail = destination;
			if (this.IsSpecial)
			{
				if (tail.Length < 2) goto too_small;
				tail[0] = 0xFF;
				tail[1] = 0xFF;
				tail = tail[2..];
			}
			else
			{
				if (tail.Length < 1) goto too_small;
				tail[0] = 0xFF;
				tail = tail[1..];
			}

			var chars = SuffixString.AsSpan();
			if (chars.Length > tail.Length) goto too_small;
			//PERF: use SIMD? or some hack by casting to RoS<byte> and copying only one every two bytes?
			for(int i = 0; i < chars.Length; i++)
			{
				tail[i] = unchecked((byte) chars[i]); //REVIEW: overflow or truncate?
			}
			tail = tail[chars.Length..];

			bytesWritten = destination.Length - tail.Length;
			return true;

		too_small:
			bytesWritten = 0;
			return false;
		}

		#endregion

	}

}
