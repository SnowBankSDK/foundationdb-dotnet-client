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

	/// <summary>Factory class for keys</summary>
	[PublicAPI]
	public static class FdbKey
	{

		/// <summary>Maximum allowed size for a key in the database (10,000 bytes)</summary>
		public const int MaxSize = Fdb.MaxKeySize;

		#region Well Known Keys...

		/// <summary>Smallest possible key (<c>0x00</c>)</summary>
		public static readonly Slice MinValue = Slice.FromByte(0);

		/// <summary>Smallest possible key (<c>0x00 ]</c>)</summary>
		public static ReadOnlySpan<byte> MinValueSpan => [ 0 ];

		/// <summary>Biggest possible key (<c>0xFF</c>), excluding the system keys</summary>
		public static readonly Slice MaxValue = Slice.FromByte(255);

		/// <summary>Biggest possible key (<c>0xFF</c>), excluding the system keys</summary>
		public static ReadOnlySpan<byte> MaxValueSpan => [ 0xFF ];

		/// <summary>Default Directory Layer prefix (<c>0xFE...</c>)</summary>
		[Obsolete("Use FdbKey.DirectoryPrefix instead", error: true)] //TODO: remove me soon!
		public static readonly Slice Directory = Slice.FromByte(254);

		/// <summary>Default Directory Layer prefix (<c>0xFE...</c>)</summary>
		public static readonly Slice DirectoryPrefix = Slice.FromByte(254);

		/// <summary>Default Directory Layer prefix (<c>0xFE...</c>)</summary>
		public static ReadOnlySpan<byte> DirectoryPrefixSpan => [ 0xFE ];

		/// <summary>Default System prefix (<c>0xFF...</c>)</summary>
		[Obsolete("Use FdbKey.SystemPrefix instead", error: true)] //TODO: remove me soon!
		public static readonly Slice System = Slice.FromByte(255);
		//note: this is obsolete because it causes too much ambiguity issues with the System namespace

		/// <summary>Default System prefix (<c>0xFF...</c>)</summary>
		public static readonly Slice SystemPrefix = Slice.FromByte(255);

		/// <summary>Prefix of the System keyspace (<c>`\xFF...`</c>)</summary>
		public static ReadOnlySpan<byte> SystemPrefixSpan => [ 0xFF ];

		/// <summary>Last possible key of System keyspace (<c>`\xFF\xFF`</c>)</summary>
		public static ReadOnlySpan<byte> SystemEndSpan => [ 0xFF, 0xFF ];

		/// <summary>Prefix of the Special Keys keyspace (<c>`\xFF\xFF...`</c>)</summary>
		public static ReadOnlySpan<byte> SpecialKeyPrefix => [ 0xFF, 0xFF ];

		#endregion

		#region Key Factories...

		#region FromBytes...

		#region No Subspace...

		/// <summary>Returns a key that wraps a pre-encoded <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey FromBytes(Slice key) => !key.IsNull ? new(key) : throw Fdb.Errors.KeyCannotBeNull();

		/// <summary>Returns a key that wraps a pre-encoded byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey FromBytes(byte[] key) => key is not null ? new(key.AsSlice()) : throw Fdb.Errors.KeyCannotBeNull();

		/// <summary>Returns a key that wraps a pre-encoded byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey FromBytes(byte[] key, int start, int length) => key is not null ? new(key.AsSlice(start, length)) : throw Fdb.Errors.KeyCannotBeNull();

		/// <summary>Returns a key that wraps a copy of a span of bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey FromBytes(ReadOnlySpan<byte> key) => new(Slice.FromBytes(key));

		/// <summary>Returns a key that wraps a pre-encoded successor of a <see cref="Slice"/> (<c>key.`\x00`</c>)</summary>
		public static FdbSuccessorKey<FdbRawKey> Successor(Slice key) => new(new(key));

		/// <summary>Returns a key that wraps a pre-encoded end of valid children of a <see cref="Slice"/> (<c>key.`\xFF`</c>)</summary>
		public static FdbLastKey<FdbRawKey> Last(Slice key) => new(new(key));

		#endregion

		#region With Subspace...

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey FromBytes(IKeySubspace subspace, Slice relativeKey)
		{
			Contract.NotNull(subspace);
			return !relativeKey.IsNull ? new(subspace, relativeKey) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));
		}

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey FromBytes(IKeySubspace subspace, byte[] relativeKey)
		{
			Contract.NotNull(subspace);
			return relativeKey is not null ? new(subspace, relativeKey.AsSlice()) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));
		}

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey FromBytes(IKeySubspace subspace, byte[] relativeKey, int start, int length)
		{
			Contract.NotNull(subspace);
			return relativeKey is not null ? new(subspace, relativeKey.AsSlice(start, length)) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));
		}

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey FromBytes(IKeySubspace subspace, ReadOnlySpan<byte> relativeKey)
		{
			Contract.NotNull(subspace);
			return new(subspace, Slice.FromBytes(relativeKey));
		}

		#endregion

		#endregion

		#region Tuples...

		#region No Subspace...

		#region FromTuple(ValueTuple<...>)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> FromTuple<T1>(ValueTuple<T1> key) => new(null, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> FromTuple<T1, T2>(in ValueTuple<T1, T2> key) => new(null, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> FromTuple<T1, T2, T3>(in ValueTuple<T1, T2, T3> key) => new(null, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> FromTuple<T1, T2, T3, T4>(in ValueTuple<T1, T2, T3, T4> key) => new(null, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> FromTuple<T1, T2, T3, T4, T5>(in ValueTuple<T1, T2, T3, T4, T5> key) => new(null, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> FromTuple<T1, T2, T3, T4, T5, T6>(in ValueTuple<T1, T2, T3, T4, T5, T6> key) => new(null, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> FromTuple<T1, T2, T3, T4, T5, T6, T7>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> key) => new(null, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> FromTuple<T1, T2, T3, T4, T5, T6, T7, T8>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> key) => new(null, key.ToSTuple());

		#endregion

		#region FromTuple(STuple<...>)...

		/// <summary>Returns a key that packs the given items inside the root subspace</summary>
		/// <param name="items">Elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey FromTuple(IVarTuple items) => new(null, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> FromTuple<T1>(STuple<T1> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> FromTuple<T1, T2>(in STuple<T1, T2> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> FromTuple<T1, T2, T3>(in STuple<T1, T2, T3> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> FromTuple<T1, T2, T3, T4>(in STuple<T1, T2, T3, T4> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> FromTuple<T1, T2, T3, T4, T5>(in STuple<T1, T2, T3, T4, T5> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> FromTuple<T1, T2, T3, T4, T5, T6>(in STuple<T1, T2, T3, T4, T5, T6> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> FromTuple<T1, T2, T3, T4, T5, T6, T7>(in STuple<T1, T2, T3, T4, T5, T6, T7> key) => new(null, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> FromTuple<T1, T2, T3, T4, T5, T6, T7, T8>(in STuple<T1, T2, T3, T4, T5, T6, T7, T8> key) => new(null, in key);

		#endregion

		#endregion

		#region With Subspace...

		#region FromTuple(ValueTuple<...>)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> FromTuple<T1>(IKeySubspace subspace, ValueTuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> FromTuple<T1, T2>(IKeySubspace subspace, in ValueTuple<T1, T2> key) => new(subspace, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> FromTuple<T1, T2, T3>(IKeySubspace subspace, in ValueTuple<T1, T2, T3> key) => new(subspace, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> FromTuple<T1, T2, T3, T4>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> key) => new(subspace, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> FromTuple<T1, T2, T3, T4, T5>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> key) => new(subspace, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> FromTuple<T1, T2, T3, T4, T5, T6>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> FromTuple<T1, T2, T3, T4, T5, T6, T7>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, key.ToSTuple());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> FromTuple<T1, T2, T3, T4, T5, T6, T7, T8>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> key) => new(subspace, key.ToSTuple());

		#endregion

		#region FromTuple(STuple<...>)...

		/// <summary>Returns a key that packs the given items inside the root subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">Elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey FromTuple(IKeySubspace subspace, IVarTuple items) => new(subspace, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> FromTuple<T1>(IKeySubspace subspace, STuple<T1> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> FromTuple<T1, T2>(IKeySubspace subspace, in STuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> FromTuple<T1, T2, T3>(IKeySubspace subspace, in STuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> FromTuple<T1, T2, T3, T4>(IKeySubspace subspace, in STuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> FromTuple<T1, T2, T3, T4, T5>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> FromTuple<T1, T2, T3, T4, T5, T6>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> FromTuple<T1, T2, T3, T4, T5, T6, T7>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> FromTuple<T1, T2, T3, T4, T5, T6, T7, T8>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> key) => new(subspace, in key);

		#endregion

		#endregion

		#endregion

		#region Special Keys...

		/// <summary>Returns a key in the System subspace (<c>`\xFF....`</c>)</summary>
		[Pure]
		public static FdbSystemKey ToSystemKey(ReadOnlySpan<byte> relativeKey)
			=> relativeKey.Length == 0 ? FdbSystemKey.System
			 : relativeKey[0] != 0xFF ? new(Slice.FromBytes(relativeKey), special: false)
			 : new(Slice.FromBytes(relativeKey[1..]), special: true);

		/// <summary>Returns a key in the System subspace (<c>`\xFF....`</c>)</summary>
		[Pure]
		public static FdbSystemKey ToSystemKey(Slice relativeKey)
			=> relativeKey.IsNull ? throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey))
			 : relativeKey.Count == 0 ? FdbSystemKey.System
			 : relativeKey[0] != 0xFF ? new(relativeKey, special: false)
			 : new(relativeKey.Substring(1), special: true);

		/// <summary>Returns a key in the System subspace (<c>`\xFF....`</c>)</summary>
		[Pure]
		public static FdbSystemKey ToSystemKey(byte[] relativeKey)
			=> relativeKey is not null ? ToSystemKey(relativeKey.AsSlice()) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));

		/// <summary>Returns a key in the System subspace (<c>`\xFF....`</c>)</summary>
		/// <remarks>
		/// <para>For example, <c>FdbKey.ToSystemKey("/metadataVersion")</c> will generate the key <c>`\xFF/metadataVersion`</c></para>
		/// <para>The <paramref name="relativeKey"/> MUST NOT contain any characters greater than 0xFF. See <see cref="Slice.FromStringAscii(string)"/> for more details.</para>
		/// </remarks>
		[Pure]
		public static FdbSystemKey ToSystemKey(string relativeKey)
			=> relativeKey is null ? throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey))
			 : relativeKey.Length == 0 ? FdbSystemKey.System
			 : relativeKey[0] != '\xFF' ? new(relativeKey, special: false)
			 : new(relativeKey.Substring(1), special: true);

		/// <summary>Returns a key in the System subspace (<c>`\xFF....`</c>)</summary>
		/// <remarks>
		/// <para>For example, <c>FdbKey.ToSystemKey("/metadataVersion".AsSpan())</c> will generate the key <c>`\xFF/metadataVersion`</c></para>
		/// <para>The <paramref name="relativeKey"/> MUST NOT contain any characters greater than 0xFF. See <see cref="Slice.FromStringAscii(string)"/> for more details.</para>
		/// </remarks>
		[Pure]
		public static FdbSystemKey ToSystemKey(ReadOnlySpan<char> relativeKey)
			=> relativeKey.Length == 0 ? FdbSystemKey.System
			 : relativeKey[0] != '\xFF' ? new(relativeKey.ToString(), special: false) 
			 : new(relativeKey[1..].ToString(), special: true);

		/// <summary>Returns a key in the Special Key subspace (<c>`\xFF\xFF....`</c>)</summary>
		/// <remarks>
		/// <para>The <paramref name="relativeKey"/> MUST NOT contain any characters greater than 0xFF. See <see cref="Slice.FromStringAscii(string)"/> for more details.</para>
		/// <para>Please be careful when using UTF-8 string literals: the value <c>"\xff/hello/world"u8</c> will be encoded as UTF-8, meaning that it will start with bytes <c>[ 0xC3, 0xBF, ...]</c> and NOT <c>[ 0xFF, .... ]</c> !</para>
		/// </remarks>
		[Pure]
		public static FdbSystemKey ToSpecialKey(ReadOnlySpan<byte> relativeKey)
			=> new(Slice.FromBytes(relativeKey), special: true);

		/// <summary>Returns a key in the Special Key subspace (<c>`\xFF\xFF....`</c>)</summary>
		[Pure]
		public static FdbSystemKey ToSpecialKey(Slice relativeKey)
			=> !relativeKey.IsNull ? new(relativeKey, special: true) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));

		/// <summary>Returns a key in the Special Key subspace (<c>`\xFF\xFF....`</c>)</summary>
		[Pure]
		public static FdbSystemKey ToSpecialKey(byte[] relativeKey)
			=> relativeKey is not null ? new(relativeKey.AsSlice(), special: true) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));

		/// <summary>Returns a key in the Special Key subspace (<c>`\xFF\xFF....`</c>)</summary>
		/// <remarks>The <paramref name="relativeKey"/> MUST NOT contain any characters greater than 0xFF. See <see cref="Slice.FromStringAscii(string)"/> for more details.</remarks>
		[Pure]
		public static FdbSystemKey ToSpecialKey(string relativeKey)
			=> relativeKey is not null ? new(relativeKey, special: true) : throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));

		/// <summary>Returns a key in the Special Key subspace (<c>`\xFF\xFF....`</c>)</summary>
		/// <remarks>The <paramref name="relativeKey"/> MUST NOT contain any characters greater than 0xFF. See <see cref="Slice.FromStringAscii(string)"/> for more details.</remarks>
		[Pure]
		public static FdbSystemKey ToSpecialKey(ReadOnlySpan<char> relativeKey)
			=> new(relativeKey.ToString(), special: true);

		#endregion

		#endregion

		#region Helpers...

		/// <summary>Returns the first key (in lexicographically order) that does not have the passed in <paramref name="key"/> as a prefix</summary>
		/// <param name="key">Slice to increment</param>
		/// <returns>New slice that is guaranteed to be the first key lexicographically higher than <paramref name="key"/> which does not have <paramref name="key"/> as a prefix</returns>
		/// <remarks>If the last byte is already equal to 0xFF, it will roll over to 0x00 and the previous byte will be incremented.</remarks>
		/// <exception cref="ArgumentException">If <paramref name="key"/> is <see cref="Slice.Nil"/></exception>
		/// <exception cref="OverflowException">If <paramref name="key"/> is <see cref="Slice.Empty"/> or consists only of 0xFF bytes</exception>
		/// <example>
		/// FdbKey.Increment(Slice.FromString("ABC")) => "ABD"
		/// FdbKey.Increment(Slice.FromHexa("01 FF")) => { 02 }
		/// </example>
		public static Slice Increment(Slice key)
		{
			if (key.IsNull) throw new ArgumentException("Cannot increment null buffer", nameof(key));

			var tmp = key.ToArray();
			Increment(tmp.AsSpan(), out var length);
			return tmp.AsSlice(0, length);
		}

		/// <summary>Increments the key in the buffer into the first key (in lexicographically order) that does not have the passed in <paramref name="key"/> as a prefix.</summary>
		/// <param name="key"></param>
		/// <param name="length"></param>
		/// <remarks>
		/// <para>If the last byte is already equal to 0xFF, the previous byte will be incremented instead.</para>
		/// <para>If all bytes are equal to 0xFF, and exception is thrown</para>
		/// <para>Examples:<code>
		/// FdbKey.Increment([ 0x42, 0x12, 0x34, 0x00 ], out length) => [ 0x42, 0x12, 0x34, 0x01 ], length = 4
		/// FdbKey.Increment([ 0x42, 0x12, 0x34, 0xFE ], out length) => [ 0x42, 0x12, 0x34, 0xFF ], length = 4
		/// FdbKey.Increment([ 0x42, 0x12, 0x34, 0xFF ], out length) => [ 0x42, 0x12, 0x35 ], length = 3
		/// FdbKey.Increment([ 0x42, 0x12, 0xFF, 0xFF ], out length) => [ 0x42, 0x13 ], length = 2
		/// FdbKey.Increment([ 0x42, 0xFF, 0xFF, 0xFF ], out length) => [ 0x43 ], length = 1
		/// FdbKey.Increment([ 0xFF, 0xFF, 0xFF, 0xFF ], out length) => throws!
		/// </code></para>
		/// </remarks>
		public static void Increment(Span<byte> key, out int length)
		{
			// ReSharper disable once InconsistentNaming
			int lastNonFFByte = key.LastIndexOfAnyExcept((byte) 0xFF);
			if (lastNonFFByte < 0)
			{
				throw Fdb.Errors.CannotIncrementKey();
			}
			++key[lastNonFFByte];
			length = lastNonFFByte + 1;
		}

		/// <summary>Merges an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, Slice[] keys)
		{
			Contract.NotNull(keys);
			return Merge(prefix, keys.AsSpan());
		}

		/// <summary>Merges an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, ReadOnlySpan<Slice> keys)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));

			//REVIEW: merge this code with Slice.ConcatRange!

			// we can pre-allocate exactly the buffer by computing the total size of all keys
			long size = keys.Length * prefix.Count;
			for (int i = 0; i < keys.Length; i++)
			{
				size += keys[i].Count;
			}

			var writer = new SliceWriter(checked((int) size));
			var next = new List<int>(keys.Length);

			//TODO: use multiple buffers if item count is huge ?

			var prefixSpan = prefix.Span;
			foreach (var key in keys)
			{
				if (prefixSpan.Length != 0) writer.WriteBytes(prefixSpan);
				writer.WriteBytes(key.Span);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Merges a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] Merge(Slice prefix, IEnumerable<Slice> keys)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));
			Contract.NotNull(keys);

			//REVIEW: merge this code with Slice.ConcatRange!

			// use optimized version for arrays
			if (keys is Slice[] array) return Merge(prefix, array);

			// pre-allocate with a count if we can get one...
			var next = keys is ICollection<Slice> coll ? new List<int>(coll.Count) : [ ];
			var writer = default(SliceWriter);

			//TODO: use multiple buffers if item count is huge ?

			var prefixSpan = prefix.Span;
			foreach (var key in keys)
			{
				if (prefixSpan.Length != 0) writer.WriteBytes(prefixSpan);
				writer.WriteBytes(key);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Splits a buffer containing multiple contiguous segments into an array of segments</summary>
		/// <param name="buffer">Buffer containing all the segments</param>
		/// <param name="start">Offset of the start of the first segment</param>
		/// <param name="endOffsets">Array containing, for each segment, the offset of the following segment</param>
		/// <returns>Array of segments</returns>
		/// <example>SplitIntoSegments("HelloWorld", 0, [5, 10]) => [{"Hello"}, {"World"}]</example>
		internal static Slice[] SplitIntoSegments(byte[]? buffer, int start, List<int> endOffsets)
		{
			var result = new Slice[endOffsets.Count];
			int i = 0;
			int p = start;
			foreach (var end in endOffsets)
			{
				result[i++] = buffer.AsSlice(p, end - p);
				p = end;
			}

			return result;
		}

		/// <summary>Splits a range of indexes into several batches</summary>
		/// <param name="offset">Offset from which to start counting</param>
		/// <param name="count">Total number of values that will be returned</param>
		/// <param name="batchSize">Maximum size of each batch</param>
		/// <returns>Collection of B batches each containing at most <paramref name="batchSize"/> contiguous indices, counting from <paramref name="offset"/> to (<paramref name="offset"/> + <paramref name="count"/> - 1)</returns>
		/// <example>Batched(0, 100, 20) => [ {0..19}, {20..39}, {40..59}, {60..79}, {80..99} ]</example>
		public static IEnumerable<IEnumerable<int>> BatchedRange(int offset, int count, int batchSize)
		{
			while (count > 0)
			{
				int chunk = Math.Min(count, batchSize);
				yield return Enumerable.Range(offset, chunk);
				offset += chunk;
				count -= chunk;
			}
		}

		#endregion

		private sealed class BatchIterator : IEnumerable<IEnumerable<KeyValuePair<int, int>>>
		{
#if NET9_0_OR_GREATER
			private readonly System.Threading.Lock m_lock = new ();
#else
			private readonly object m_lock = new ();
#endif
			private int m_cursor;
			private int m_remaining;

			private readonly int m_workers;
			private readonly int m_batchSize;

			public BatchIterator(int offset, int count, int workers, int batchSize)
			{
				Contract.Debug.Requires(offset >= 0 && count >= 0 && workers >= 0 && batchSize >= 0);
				m_cursor = offset;
				m_remaining = count;
				m_workers = workers;
				m_batchSize = batchSize;
			}

			private KeyValuePair<int, int> GetChunk()
			{
				if (m_remaining == 0) return default;

				lock (m_lock)
				{
					int cursor = m_cursor;
					int size = Math.Min(m_remaining, m_batchSize);

					m_cursor += size;
					m_remaining -= size;

					return new(cursor, size);
				}
			}


			public IEnumerator<IEnumerable<KeyValuePair<int, int>>> GetEnumerator()
			{
				for (int k = 0; k < m_workers; k++)
				{
					if (m_remaining == 0) yield break;
					yield return WorkerIterator();
				}
			}

			private IEnumerable<KeyValuePair<int, int>> WorkerIterator()
			{
				while (true)
				{
					var chunk = GetChunk();
					if (chunk.Value == 0) break;
					yield return chunk;
				}
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}

		/// <summary>Splits a range of indexes into a fixed number of 'worker' sequence, that will consume batches in parallel</summary>
		/// <param name="offset">Offset from which to start counting</param>
		/// <param name="count">Total number of values that will be returned</param>
		/// <param name="workers">Number of concurrent workers that will take batches from the pool</param>
		/// <param name="batchSize">Maximum size of each batch</param>
		/// <returns>List of '<paramref name="workers"/>' enumerables that all fetch batches of values from the same common pool. All enumerables will stop when the last batch as been consumed by the last worker.</returns>
		public static IEnumerable<IEnumerable<KeyValuePair<int, int>>> Batched(int offset, int count, int workers, int batchSize)
		{
			return new BatchIterator(offset, count, workers, batchSize);
		}

		/// <summary>Produces a user-friendly version of the slice</summary>
		/// <param name="key">Any binary key</param>
		/// <returns>User-friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as a hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		public static string Dump(Slice key)
		{
			return PrettyPrint(key, PrettyPrintMode.Single);
		}

#if NET8_0_OR_GREATER
		private static readonly global::System.Buffers.SearchValues<byte> PossibleTupleFirstBytes = global::System.Buffers.SearchValues.Create([
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
			21, 22, 23, 24, 25, 26, 27, 28, 29,
			32, 33, 38, 39, 48, 49, 50, 51,
			254, 255
		]);
#endif

		/// <summary>Produces a user-friendly version of the slice</summary>
		/// <param name="key">Any binary key</param>
		/// <param name="mode">Defines if the key is standalone, or is the beginning or end part or a key range. This will enable or disable some heuristics that try to properly format key ranges.</param>
		/// <returns>User-friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as a hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		[DebuggerNonUserCode]
		public static string PrettyPrint(Slice key, PrettyPrintMode mode)
		{
			var span = key.Span;
			if (span.Length == 0) return key.IsNull ? "<null>" : "<empty>";
			if (span.Length> 1)
			{
				byte c = span[0];
				//OPTIMIZE: maybe we need a lookup table
#if NET8_0_OR_GREATER
				if (PossibleTupleFirstBytes.Contains(c))
#else
				if (c <= 28 || c == 32 || c == 33 || c == 48 || c == 49 || c >= 254)
#endif
				{ // it could be a tuple...
					try
					{
						SpanTuple tuple = default;
						string? suffix = null;
						bool skip = false;

						try
						{
							switch (mode)
							{
								case PrettyPrintMode.End:
								{ // the last byte will either be FF, or incremented
									// for tuples, the worst cases are for byte[]/strings (which normally end with 00)
									// => pack(("string",))+\xFF => <02>string<00><FF>
									// => string(("string",)) => <02>string<01>
									switch (span[^1])
									{
										case 0xFF:
										{
											//***README*** if you break under here, see README in the last catch() block
											if (TuPack.TryUnpack(span[..^1], out tuple))
											{
												suffix = ".<FF>";
											}
											break;
										}
										case 0x01:
										{
											var tmp = span.ToArray();
											tmp[^1] = 0;
											//***README*** if you break under here, see README in the last catch() block
											if (TuPack.TryUnpack(tmp, out tuple))
											{
												suffix = " + 1";
											}
											break;
										}
									}
									break;
								}
								case PrettyPrintMode.Begin:
								{ // the last byte will usually be 00

									// We can't really know if the tuple ended with NULL (serialized to <00>) or if a <00> was added,
									// but since the ToRange() on tuples add a <00> we can bet on the fact that it is not part of the tuple itself.
									// except maybe if we have "00 FF 00" which would be the expected form of a string that ends with a <00>

									if (span.Length > 2 && span[^1] == 0 && span[^2] != 0xFF)
									{
										//***README*** if you break under here, see README in the last catch() block
										if (TuPack.TryUnpack(span[..^1], out tuple))
										{
											suffix = ".<00>";
										}
									}
									break;
								}
							}
						}
						catch (Exception e)
						{
							suffix = null;
							skip = e is not (FormatException or ArgumentOutOfRangeException);
						}

						if (tuple.Count != 0)
						{
							return tuple.ToString() + suffix;
						}

						if (!skip)
						{ // attempt a regular decoding
							if (TuPack.TryUnpack(span, out tuple))
							{
								return tuple.ToString() + suffix;
							}
						}
					}
					catch (Exception)
					{
						//README: If Visual Studio is breaking inside some Tuple parsing method somewhere inside this try/catch,
						// this is because your debugger is configured to automatically break on thrown exceptions of type FormatException, ArgumentException, or InvalidOperation.
						// Unfortunately, there isn't much you can do except unchecking "break when this exception type is thrown". If you know a way to disable locally this behaviour, please fix this!
						// => only other option would be to redesign the parsing of tuples as a TryParseXXX() that does not throw, OR to have a VerifyTuple() methods that only checks for validity....
					}
				}
			}

			return Slice.Dump(key);
		}

		/// <summary>Produces a user-friendly version of the slice</summary>
		/// <param name="key">Any binary key</param>
		/// <returns>User-friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as a hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		public static string Dump(ReadOnlySpan<byte> key)
		{
			return PrettyPrint(key, PrettyPrintMode.Single);
		}

		/// <summary>Produces a user-friendly version of the slice</summary>
		/// <param name="key">Any binary key</param>
		/// <param name="mode">Defines if the key is standalone, or is the beginning or end part or a key range. This will enable or disable some heuristics that try to properly format key ranges.</param>
		/// <returns>User-friendly version of the key. Attempts to decode the key as a tuple first. Then as an ASCII string. Then as a hex dump of the key.</returns>
		/// <remarks>This can be slow, and should only be used for logging or troubleshooting.</remarks>
		[DebuggerNonUserCode]
		public static string PrettyPrint(ReadOnlySpan<byte> key, PrettyPrintMode mode)
		{
			if (key.Length > 1)
			{
				byte c = key[0];

				if (c == 0xFF)
				{ // this is most probably a SystemKey
					//return Slice.Dump(key);
				}

				//OPTIMIZE: maybe we need a lookup table
#if NET8_0_OR_GREATER
				if (PossibleTupleFirstBytes.Contains(c))
#else
				if (c <= 28 || c == 32 || c == 33 || c == 48 || c == 49 || c >= 254)
#endif
				{ // it could be a tuple...

					// handle some corner cases when dealing with begin/end keys...
					switch (mode)
					{
						case PrettyPrintMode.End:
						{ // the last byte will either be FF, or incremented
							// for tuples, the worst cases are for byte[]/strings (which normally end with 00)
							// => pack(("string",))+\xFF => <02>string<00><FF>
							// => string(("string",)) => <02>string<01>
							switch (key[^1])
							{
								case 0xFF:
								{
									if (TuPack.TryUnpack(key[..^1], out var tuple))
									{
										return tuple.ToString() + ".<FF>";
									}
									break;
								}
								case 0x01:
								{
									//TODO: HACKHACK: until we find another solution, we have to make a copy :(
									var tmp = key.ToArray();
									tmp[^1] = 0;
									if (TuPack.TryUnpack(tmp, out var tuple))
									{
										return tuple.ToString() + "+1";
									}
									break;
								}
							}
							break;
						}
						case PrettyPrintMode.Begin:
						{ // the last byte will usually be 00

							// We can't really know if the tuple ended with NULL (serialized to <00>) or if a <00> was added,
							// but since the ToRange() on tuples add a <00> we can bet on the fact that it is not part of the tuple itself.
							// except maybe if we have "00 FF 00" which would be the expected form of a string that ends with a <00>

							if (key.Length > 2 && key[-1] == 0 && key[-2] != 0xFF)
							{
								//***README*** if you break under here, see README in the last catch() block
								if (TuPack.TryUnpack(key[..^1], out var tuple))
								{
									return tuple.ToString() + ".<00>";
								}
							}
							break;
						}
					}
					// attempt a regular decoding
					{ 
						if (TuPack.TryUnpack(key, out var tuple))
						{
							return tuple.ToString();
						}
					}
				}
			}

			return Slice.Dump(key);
		}

		/// <summary>How a key should be formatted</summary>
		public enum PrettyPrintMode
		{
			/// <summary>The key points to an actual entry in the database, and in not part of a <see cref="KeySelector"/> or <see cref="KeyRange"/></summary>
			Single = 0,

			/// <summary>The key represents the "Begin" key of a <see cref="KeySelector"/> or <see cref="KeyRange"/>, and may be incomplete.</summary>
			Begin = 1,

			/// <summary>The key represents the "End" key of a <see cref="KeySelector"/> or <see cref="KeyRange"/>, and may be incomplete.</summary>
			End = 2,
		}

		#region Key Validation...

		/// <summary>Checks that a key is valid, and is inside the global key space of this database</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <param name="ignoreError">If true, don't return an exception in <paramref name="error"/>, even if the key is invalid.</param>
		/// <param name="error">Receive an exception object if the key is not valid and <paramref name="ignoreError"/> is false</param>
		/// <returns>Return <c>false</c> if the key is outside the allowed key space of this database</returns>
		internal static bool ValidateKey(Slice key, bool endExclusive, bool ignoreError, out Exception? error)
		{
			// null keys are not allowed
			if (key.IsNull)
			{
				error = ignoreError ? null : Fdb.Errors.KeyCannotBeNull();
				return false;
			}
			return ValidateKey(key.Span, endExclusive, ignoreError, out error);
		}

		/// <summary>Checks that a key is valid, and is inside the global key space of this database</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <param name="ignoreError"></param>
		/// <param name="error"></param>
		/// <returns>An exception if the key is outside the allowed key space of this database</returns>
		internal static bool ValidateKey(ReadOnlySpan<byte> key, bool endExclusive, bool ignoreError, out Exception? error)
		{
			error = null;

			// key cannot be larger than maximum allowed key size
			if (key.Length > Fdb.MaxKeySize)
			{
				if (!ignoreError) error = Fdb.Errors.KeyIsTooBig(key);
				return false;
			}

			// special case for system keys
			if (IsSystemKey(key))
			{
				// note: it will fail later if the transaction does not have access to the system keys!
				return true;
			}

			return true;
		}

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSystemKey(Slice key)
			=> key.StartsWith(0xFF);

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSystemKey(ReadOnlySpan<byte> key)
#if NET9_0_OR_GREATER
			=> key.StartsWith((byte) 0xFF);
#else
			=> key.Length != 0 && key[0] == 0xFF;
#endif

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSystemKey<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> FdbKeyHelpers.IsSystem(in key);

		/// <summary>Checks if the key is in the SpecialKey keyspace (starts with <c>`\xFF\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSpecialKey(Slice key) => IsSpecialKey(key.Span);

		/// <summary>Checks if the key is in the SpecialKey keyspace (starts with <c>`\xFF\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSpecialKey(ReadOnlySpan<byte> key)
			=> key.Length >= 2 && key[0] == 0xFF && key[1] == 0xFF;

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSpecialKey<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> FdbKeyHelpers.IsSpecial(in key);

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		public static void EnsureKeyIsValid(Slice key, bool endExclusive = false)
		{
			if (!ValidateKey(key, endExclusive, false, out var ex))
			{
				throw ex!;
			}
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		public static void EnsureKeyIsValid(ReadOnlySpan<byte> key, bool endExclusive = false)
		{
			if (!ValidateKey(key, endExclusive, false, out var ex)) throw ex!;
		}

		/// <summary>Checks that one or more keys are inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="keys">Array of keys to verify</param>
		/// <param name="endExclusive">If true, the keys are allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If at least on key is outside the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		public static void EnsureKeysAreValid(ReadOnlySpan<Slice> keys, bool endExclusive = false)
		{
			for (int i = 0; i < keys.Length; i++)
			{
				if (!ValidateKey(keys[i], endExclusive, false, out var ex))
				{
					throw ex!;
				}
			}
		}

		/// <summary>Tests if a key is allowed to be used with this database instance</summary>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, and is contained in the global key space of this database instance. Otherwise, returns false.</returns>
		[Pure]
		public static bool IsKeyValid(Slice key)
		{
			return ValidateKey(key, false, true, out _);
		}

		/// <summary>Tests if a key is allowed to be used with this database instance</summary>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, and is contained in the global key space of this database instance. Otherwise, returns false.</returns>
		[Pure]
		public static bool IsKeyValid(ReadOnlySpan<byte> key)
		{
			return ValidateKey(key, false, true, out _);
		}

		#endregion

		#region Value Validation

		/// <summary>Ensures that a serialized value is valid</summary>
		/// <remarks>Throws an exception if the value is null, or exceeds the maximum allowed size (Fdb.MaxValueSize)</remarks>
		public static void EnsureValueIsValid(Slice value)
		{
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();
			EnsureValueIsValid(value.Span);
		}

		/// <summary>Ensures that a serialized value is valid</summary>
		/// <remarks>Throws an exception if the value is null, or exceeds the maximum allowed size (Fdb.MaxValueSize)</remarks>
		public static void EnsureValueIsValid(ReadOnlySpan<byte> value)
		{
			var ex = ValidateValue(value);
			if (ex != null) throw ex;
		}

		internal static Exception? ValidateValue(ReadOnlySpan<byte> value)
		{
			if (value.Length > Fdb.MaxValueSize)
			{
				return Fdb.Errors.ValueIsTooBig(value);
			}
			return null;
		}

		#endregion

	}

}
