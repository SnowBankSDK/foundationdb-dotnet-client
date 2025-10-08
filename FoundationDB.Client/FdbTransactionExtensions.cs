#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Buffers.Binary;
	using System.ComponentModel;
	using System.Numerics;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB transaction implementations.</summary>
	[PublicAPI]
	public static class FdbTransactionExtensions
	{

		#region Fluent Options...

		/// <summary>Configure the options of this transaction.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TTransaction WithOptions<TTransaction>(this TTransaction trans, Action<IFdbTransactionOptions> configure)
			where TTransaction : IFdbReadOnlyTransaction
		{
			configure(trans.Options);
			return trans;
		}

		#endregion

		#region Key/Value Validation...

		[Pure, MethodImpl(MethodImplOptions.NoInlining), StackTraceHidden]
		internal static ReadOnlySpan<byte> ToSpanKey(Slice key) => !key.IsNull ? key.Span : throw Fdb.Errors.KeyCannotBeNull();

		[Pure, MethodImpl(MethodImplOptions.NoInlining), StackTraceHidden]
		internal static ReadOnlySpan<byte> ToSpanValue(Slice value) => !value.IsNull ? value.Span : throw Fdb.Errors.ValueCannotBeNull();

		#endregion

		#region Get...

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetAsync(ToSpanKey(key));

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetAsync(ReadOnlySpan{byte})"/>
		public static Task<Slice> GetAsync<TKey>(this IFdbReadOnlyTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetAsync(keySpan);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetAsync(keyBytes.Span);
			}
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetAsync{TResult}"/>
		public static Task<TResult> GetAsync<TKey, TResult>(this IFdbReadOnlyTransaction trans, in TKey key, FdbValueDecoder<TResult> valueDecoder)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetAsync(keySpan, valueDecoder);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetAsync(keyBytes.Span, valueDecoder);
			}
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetAsync{TState,TResult}"/>
		public static Task<TResult> GetAsync<TKey, TValueState, TResult>(this IFdbReadOnlyTransaction trans, in TKey key, TValueState valueState, FdbValueDecoder<TValueState, TResult> valueDecoder)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetAsync(keySpan, valueState, valueDecoder);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetAsync(keyBytes.Span, valueState, valueDecoder);
			}
		}

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="state">State that will be forwarded to the <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<TResult> GetAsync<TState, TResult>(this IFdbReadOnlyTransaction trans, Slice key, TState state, FdbValueDecoder<TState, TResult> decoder)
			=> trans.GetAsync(ToSpanKey(key), state, decoder);

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<TResult> GetAsync<TResult>(this IFdbReadOnlyTransaction trans, Slice key, FdbValueDecoder<TResult> decoder)
			=> trans.GetAsync(ToSpanKey(key), decoder);

		/// <summary>Reads and decodes a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Task<TValue?> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice key, IValueEncoder<TValue> encoder)
			=> trans.GetAsync(ToSpanKey(key), encoder);

		/// <summary>Reads and decodes a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Task<TValue?> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(encoder);

			return trans.GetAsync(key, encoder, static (state, buffer, found) =>
			{
				//HACKHACK: TODO: OPTIMIZE: PERF: IValueEncoder should also accept ReadOnlySpan !
				return state.DecodeValue(found ? Slice.FromBytes(buffer) : Slice.Nil);
			});
		}

		/// <summary>Tries to read a value from database snapshot represented by the current transaction, and writes it to <paramref name="valueWriter"/> if found.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="valueWriter">Buffer writer where the value will be written, if it is found</param>
		/// <returns>Task with <see langword="true"/> if the key was found; otherwise, <see langword="false"/></returns>
		public static Task<bool> TryGetAsync(this IFdbReadOnlyTransaction trans, Slice key, IBufferWriter<byte> valueWriter)
		{
			return trans.TryGetAsync(ToSpanKey(key), valueWriter);
		}

		/// <summary>Tries to read a value from database snapshot represented by the current transaction, and writes it to <paramref name="valueWriter"/> if found.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="valueWriter">Buffer writer where the value will be written, if it is found</param>
		/// <returns>Task with <see langword="true"/> if the key was found; otherwise, <see langword="false"/></returns>
		public static Task<bool> TryGetAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, IBufferWriter<byte> valueWriter)
		{
			Contract.NotNull(valueWriter);
			return trans.GetAsync(key, valueWriter, static (vw, value, found) =>
			{
				if (!found) return false;
				vw.Write(value);
				return true;
			});
		}

		/// <summary>Add a read conflict range on the <c>\xff/metadataVersion</c> key</summary>
		/// <remarks>
		/// This is only required if a previous read to that key was done with snapshot isolation, to guarantee that the transaction will conflict!
		/// Please note that any external change to the key, unrelated to the current application code, could also trigger a conflict!
		/// </remarks>
		public static void ProtectAgainstMetadataVersionChange(this IFdbTransaction trans)
		{
			trans.AddReadConflictRange(Fdb.System.MetadataVersionKey, Fdb.System.MetadataVersionKeyEnd);
		}

		#region GetValueStringAsync

		/// <inheritdoc cref="GetValueStringAsync(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<string?> GetValueStringAsync(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetAsync(ToSpanKey(key), static (value, found) => found ? value.ToStringUtf8() : null);

		/// <summary>Reads the value of a key from the database, into a UTF-8 encoded <see cref="string"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<string?> GetValueStringAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToStringUtf8() : null);

		/// <inheritdoc cref="GetValueStringAsync(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},string)"/>
		public static Task<string> GetValueStringAsync(this IFdbReadOnlyTransaction trans, Slice key, string missingValue)
			=> trans.GetAsync(ToSpanKey(key), missingValue, static (missing, value, found) => found ? value.ToStringUtf8() : missing);

		/// <summary>Reads the value of a key from the database, into a UTF-8 encoded <see cref="string"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<string> GetValueStringAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, string missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToStringUtf8() : missing);

		#endregion

		#region GetValueInt32Async

		/// <inheritdoc cref="GetValueInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<int?> GetValueInt32Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueInt32Async(ToSpanKey(key));

		/// <inheritdoc cref="GetValueInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<int?> GetValueInt32Async<TKey>(this IFdbReadOnlyTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetValueInt32Async(keySpan);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetValueInt32Async(keyBytes.Span);
			}
		}

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<int?> GetValueInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToInt32() : default(int?));

		/// <inheritdoc cref="GetValueInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},int)"/>
		public static Task<int> GetValueInt32Async(this IFdbReadOnlyTransaction trans, Slice key, int missingValue)
			=> trans.GetValueInt32Async(ToSpanKey(key), missingValue);

		/// <inheritdoc cref="GetValueInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},int)"/>
		public static Task<int> GetValueInt32Async<TKey>(this IFdbReadOnlyTransaction trans, in TKey key, int missingValue)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetValueInt32Async(keySpan, missingValue);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetValueInt32Async(keyBytes.Span, missingValue);
			}
		}

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<int> GetValueInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, int missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToInt32() : missing);

		#endregion

		#region GetValueUInt32Async

		/// <inheritdoc cref="GetValueUInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<uint?> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUInt32Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<uint?> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToUInt32() : default(uint?));

		/// <inheritdoc cref="GetValueUInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},uint)"/>
		public static Task<uint> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, Slice key, uint missingValue)
			=> trans.GetValueUInt32Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<uint> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, uint missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToUInt32() : missing);

		#endregion

		#region GetValueInt64Async

		/// <inheritdoc cref="GetValueInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<long?> GetValueInt64Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueInt64Async(ToSpanKey(key));

		/// <inheritdoc cref="GetValueInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<long?> GetValueInt64Async<TKey>(this IFdbReadOnlyTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetValueInt64Async(keySpan);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetValueInt64Async(keyBytes.Span);
			}
		}

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit signed integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<long?> GetValueInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToInt64() : default(long?));

		/// <inheritdoc cref="GetValueInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},long)"/>
		public static Task<long> GetValueInt64Async(this IFdbReadOnlyTransaction trans, Slice key, long missingValue)
			=> trans.GetValueInt64Async(ToSpanKey(key), missingValue);

		/// <inheritdoc cref="GetValueInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},long)"/>
		public static Task<long> GetValueInt64Async<TKey>(this IFdbReadOnlyTransaction trans, in TKey key, long missingValue)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetValueInt64Async(keySpan, missingValue);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.GetValueInt64Async(keyBytes.Span, missingValue);
			}
		}

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit signed integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<long> GetValueInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, long missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToInt64() : missing);

		#endregion

		#region GetValueUInt64Async

		/// <inheritdoc cref="GetValueUInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<ulong?> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUInt64Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<ulong?> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToUInt64() : default(ulong?));

		/// <inheritdoc cref="GetValueUInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},ulong)"/>
		public static Task<ulong> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, Slice key, ulong missingValue)
			=> trans.GetValueUInt64Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<ulong> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, ulong missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToUInt64() : missing);

		#endregion

		#region GetValueUuid128Async

		/// <inheritdoc cref="GetValueUuid128Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<Uuid128?> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUuid128Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a 128-bit <see cref="Uuid128"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<Uuid128?> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? Uuid128.Read(value) : default(Uuid128?));

		/// <inheritdoc cref="GetValueUuid128Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},Uuid128)"/>
		public static Task<Uuid128> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, Slice key, Uuid128 missingValue)
			=> trans.GetValueUuid128Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a 128-bit <see cref="Uuid128"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<Uuid128> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, Uuid128 missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? Uuid128.Read(value) : missing);

		#endregion

		#region GetValueUuid96Async

		/// <inheritdoc cref="GetValueUuid96Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<Uuid96?> GetValueUuid96Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUuid96Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a 96-bit <see cref="Uuid96"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<Uuid96?> GetValueUuid96Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? Uuid96.Read(value) : default(Uuid96?));

		/// <inheritdoc cref="GetValueUuid96Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},Uuid96)"/>
		public static Task<Uuid96> GetValueUuid96Async(this IFdbReadOnlyTransaction trans, Slice key, Uuid96 missingValue)
			=> trans.GetValueUuid96Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a 96-bit <see cref="Uuid96"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<Uuid96> GetValueUuid96Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, Uuid96 missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? Uuid96.Read(value)  : missing);

		#endregion

		#region GetValueUuid80Async

		/// <inheritdoc cref="GetValueUuid80Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<Uuid80?> GetValueUuid80Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUuid80Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as an 80-bit <see cref="Uuid80"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<Uuid80?> GetValueUuid80Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? Uuid80.Read(value) : default(Uuid80?));

		/// <inheritdoc cref="GetValueUuid80Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},Uuid80)"/>
		public static Task<Uuid80> GetValueUuid80Async(this IFdbReadOnlyTransaction trans, Slice key, Uuid80 missingValue)
			=> trans.GetValueUuid80Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as an 80-bit <see cref="Uuid80"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<Uuid80> GetValueUuid80Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, Uuid80 missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? Uuid80.Read(value)  : missing);

		#endregion

		#region GetValueUuid64Async

		/// <inheritdoc cref="GetValueUuid64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<Uuid64?> GetValueUuid64Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUuid64Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a 64-bit <see cref="Uuid64"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<Uuid64?> GetValueUuid64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? Uuid64.Read(value)  : default(Uuid64?));

		/// <inheritdoc cref="GetValueUuid64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},Uuid64)"/>
		public static Task<Uuid64> GetValueUuid64Async(this IFdbReadOnlyTransaction trans, Slice key, Uuid64 missingValue)
			=> trans.GetValueUuid64Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a 64-bit <see cref="Uuid64"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<Uuid64> GetValueUuid64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, Uuid64 missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? Uuid64.Read(value) : missing);

		#endregion

		#region GetValueUuid48Async

		/// <inheritdoc cref="GetValueUuid48Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<Uuid48?> GetValueUuid48Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueUuid48Async(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a 48-bit <see cref="Uuid48"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<Uuid48?> GetValueUuid48Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? Uuid48.Read(value)  : default(Uuid48?));

		/// <inheritdoc cref="GetValueUuid48Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},Uuid48)"/>
		public static Task<Uuid48> GetValueUuid48Async(this IFdbReadOnlyTransaction trans, Slice key, Uuid48 missingValue)
			=> trans.GetValueUuid48Async(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a 48-bit <see cref="Uuid48"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<Uuid48> GetValueUuid48Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, Uuid48 missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? Uuid48.Read(value) : missing);

		#endregion

		#region GetValueVersionStampAsync

		/// <inheritdoc cref="GetValueVersionStampAsync(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<VersionStamp?> GetValueVersionStampAsync(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetValueVersionStampAsync(ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a <see cref="VersionStamp"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<VersionStamp?> GetValueVersionStampAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? VersionStamp.ReadFrom(value) : default(VersionStamp?));

		/// <inheritdoc cref="GetValueVersionStampAsync(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},VersionStamp)"/>
		public static Task<VersionStamp> GetValueVersionStampAsync(this IFdbReadOnlyTransaction trans, Slice key, VersionStamp missingValue)
			=> trans.GetValueVersionStampAsync(ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a <see cref="VersionStamp"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<VersionStamp> GetValueVersionStampAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, VersionStamp missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? VersionStamp.ReadFrom(value) : missing);

		#endregion

		#endregion

		#region Set...

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set(this IFdbTransaction trans, Slice key, ReadOnlySpan<byte> value)
			=> trans.Set(ToSpanKey(key), value);

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set(this IFdbTransaction trans, ReadOnlySpan<byte> key, Slice value)
			=> trans.Set(key, ToSpanValue(value));

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set(this IFdbTransaction trans, Slice key, Slice value)
			=> trans.Set(ToSpanKey(key), ToSpanValue(value));

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Set(ToSpanKey(key), in value);

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set<TKey>(this IFdbTransaction trans, in TKey key, Slice value)
			where TKey : struct, IFdbKey
			=> trans.Set(in key, ToSpanValue(value));

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set<TKey>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.Set(keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.Set(keyBytes.Span, value);
			}
		}

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (value.TryGetSpan(out var valueSpan))
			{
				trans.Set(key, valueSpan);
				return;
			}

			if (value.TryGetSizeHint(out int sizeHint) && (uint) sizeHint <= 128)
			{
				Span<byte> buffer = stackalloc byte[sizeHint];
				if (value.TryEncode(buffer, out int bytesWritten))
				{
					trans.Set(key, buffer[..bytesWritten]);
					return;
				}
			}

			using var valueBytes = FdbValueHelpers.Encode(in value, ArrayPool<byte>.Shared);
			trans.Set(key, valueBytes.Span);
		}

		/// <inheritdoc cref="IFdbTransaction.Set"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.Set(keySpan, in value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.Set(keyBytes.Span, in value);
			}
		}

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="value">Value of the key</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary literal.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)] //TODO: phase out IKeyEncoder/IValueEncoder
		public static void Set<TValue>(this IFdbTransaction trans, Slice key, IValueEncoder<TValue> encoder, TValue value)
			=> trans.Set(ToSpanKey(key), encoder, value);

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary literal.</param>
		/// <param name="value">Value of the key</param>
		[EditorBrowsable(EditorBrowsableState.Never)] //TODO: phase out IKeyEncoder/IValueEncoder
		public static void Set<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, IValueEncoder<TValue> encoder, TValue value)
		{
			Contract.NotNull(trans);
			Contract.NotNull(encoder);

			var writer = new SliceWriter(ArrayPool<byte>.Shared);

			encoder.WriteValueTo(ref writer, value);

			trans.Set(key, writer.ToSpan());

			writer.Dispose();
		}

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToTextUtf8(...)) instead")]
		public static void SetValueString(this IFdbTransaction trans, Slice key, string value) => SetValueString(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToTextUtf8(...)) instead")]
		public static void SetValueString(this IFdbTransaction trans, ReadOnlySpan<byte> key, string value)
		{
			Contract.NotNull(value);
			SetValueString(trans, key, value.AsSpan());
		}

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToTextUtf8(value)) instead")]
		public static void SetValueString(this IFdbTransaction trans, Slice key, ReadOnlySpan<char> value) => SetValueString(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToTextUtf8(value)) instead")]
		public static void SetValueString(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{
				trans.Set(key, default(ReadOnlySpan<byte>));
				return;
			}

			int byteCount = Encoding.UTF8.GetByteCount(value);
			if (byteCount <= 128)
			{
				SetValueStringSmall(trans, key, value, byteCount);
			}
			else
			{
				SetValueStringLarge(trans, key, value, byteCount);
			}

			static void SetValueStringSmall(IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<char> value, int byteCount)
			{
				Span<byte> scratch = stackalloc byte[byteCount];
				int len = Encoding.UTF8.GetBytes(value, scratch);
				trans.Set(key, scratch[..len]);
			}

			static void SetValueStringLarge(IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<char> value, int byteCount)
			{
				var array = ArrayPool<byte>.Shared.Rent(byteCount);
				int len = Encoding.UTF8.GetBytes(value, array);
				trans.Set(key, array.AsSpan(0, len));
				ArrayPool<byte>.Shared.Return(array);
			}

		}

		#region Int32

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed32LittleEndian(value)) instead")]
		public static void SetValueInt32(this IFdbTransaction trans, Slice key, int value) => SetValueInt32(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed32LittleEndian(value)) instead")]
		public static void SetValueInt32<TKey>(this IFdbTransaction trans, in TKey key, int value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueInt32(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueInt32(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed32LittleEndian(value)) instead")]
		public static void SetValueInt32(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<int, byte>(ref value), 4));
		}

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact32LittleEndian(value)) instead")]
		public static void SetValueInt32Compact(this IFdbTransaction trans, Slice key, int value) => SetValueInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact32LittleEndian(value)) instead")]
		public static void SetValueInt32Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value) => SetValueInt64Compact(trans, key, value);

		#endregion

		#region UInt32

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed32LittleEndian(value)) instead")]
		public static void SetValueUInt32(this IFdbTransaction trans, Slice key, uint value) => SetValueUInt32(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed32LittleEndian(value)) instead")]
		public static void SetValueUInt32<TKey>(this IFdbTransaction trans, in TKey key, uint value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUInt32(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUInt32(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed32LittleEndian(value)) instead")]
		public static void SetValueUInt32(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref value), 4));
		}

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact32LittleEndian(value)) instead")]
		public static void SetValueUInt32Compact(this IFdbTransaction trans, Slice key, uint value) => SetValueUInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact32LittleEndian(value)) instead")]
		public static void SetValueUInt32Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value) => SetValueUInt64Compact(trans, key, value);

		#endregion

		#region Int64

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed64LittleEndian(value)) instead")]
		public static void SetValueInt64(this IFdbTransaction trans, Slice key, long value) => SetValueInt64(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed64LittleEndian(value)) instead")]
		public static void SetValueInt64<TKey>(this IFdbTransaction trans, in TKey key, long value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueInt64(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueInt64(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed64LittleEndian(value)) instead")]
		public static void SetValueInt64(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<long, byte>(ref value), 8));
		}

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact64LittleEndian(value)) instead")]
		public static void SetValueInt64Compact(this IFdbTransaction trans, Slice key, long value) => SetValueInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact64LittleEndian(value)) instead")]
		public static void SetValueInt64Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			unchecked
			{
				ulong v = Math.Min(Math.Max(1, (ulong) value), ulong.MaxValue - 1);
				int n = ((7 + BitOperations.Log2(BitOperations.RoundUpToPowerOf2(v + 1))) / 8);
				v = BitConverter.IsLittleEndian ? (ulong) value : BinaryPrimitives.ReverseEndianness((ulong) value);
				trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref v), n));
			}
		}

		#endregion

		#region UInt64

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed64LittleEndian(value)) instead")]
		public static void SetValueUInt64(this IFdbTransaction trans, Slice key, ulong value) => SetValueUInt64(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed64LittleEndian(value)) instead")]
		public static void SetValueUInt64<TKey>(this IFdbTransaction trans, in TKey key, ulong value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUInt64(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUInt64(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToFixed64LittleEndian(value)) instead")]
		public static void SetValueUInt64(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref value), 8));
		}

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact64LittleEndian(value)) instead")]
		public static void SetValueUInt64Compact(this IFdbTransaction trans, Slice key, ulong value) => SetValueUInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToCompact64LittleEndian(value)) instead")]
		public static void SetValueUInt64Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			ulong v = Math.Min(Math.Max(1, value), ulong.MaxValue - 1);
			int n = ((7 + BitOperations.Log2(BitOperations.RoundUpToPowerOf2(v + 1))) / 8);
			v = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref v), n));
		}

		#endregion

		#region UUIDs

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToUuid128(value)) instead")]
		public static void SetValueGuid(this IFdbTransaction trans, Slice key, Guid value) => SetValueGuid(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToUuid128(value)) instead")]
		public static void SetValueGuid<TKey>(this IFdbTransaction trans, in TKey key, Guid value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueGuid(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueGuid(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, FdbValue.ToUuid128(value)) instead")]
		public static void SetValueGuid(this IFdbTransaction trans, ReadOnlySpan<byte> key, Guid value)
		{
			Span<byte> scratch = stackalloc byte[16];
			new Uuid128(value).WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid128(this IFdbTransaction trans, Slice key, Uuid128 value) => SetValueUuid128(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid128<TKey>(this IFdbTransaction trans, in TKey key, Uuid128 value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUuid128(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUuid128(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid128(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid128 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid128.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid96(this IFdbTransaction trans, Slice key, Uuid96 value) => SetValueUuid96(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid96<TKey>(this IFdbTransaction trans, in TKey key, Uuid96 value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUuid96(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUuid96(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid96(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid96 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid96.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid80(this IFdbTransaction trans, Slice key, Uuid80 value) => SetValueUuid80(trans, ToSpanKey(key), value);


		/// <summary>Sets the value of a key in the database as an 80-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid80<TKey>(this IFdbTransaction trans, in TKey key, Uuid80 value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUuid80(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUuid80(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid80(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid80 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid96.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 64-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid64(this IFdbTransaction trans, Slice key, Uuid64 value) => SetValueUuid64(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid64<TKey>(this IFdbTransaction trans, in TKey key, Uuid64 value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUuid64(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUuid64(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 64-bits UUID</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid64(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid64 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid64.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 48-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid48(this IFdbTransaction trans, Slice key, Uuid48 value) => SetValueUuid48(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 48-bits UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid48<TKey>(this IFdbTransaction trans, in TKey key, Uuid48 value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				SetValueUuid48(trans, keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				SetValueUuid48(trans, keyBytes.Span, value);
			}
		}

		/// <summary>Sets the value of a key in the database as a 48-bits UUID</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use Set(key, value) instead")]
		public static void SetValueUuid48(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid48 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid48.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		#endregion

		#endregion

		#region SetValues

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, KeyValuePair<Slice, Slice>[] keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			foreach (var kv in keyValuePairs)
			{
				trans.Set(kv.Key, kv.Value);
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, (Slice Key, Slice Value)[] keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			foreach (var kv in keyValuePairs)
			{
				trans.Set(kv.Key, kv.Value);
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		[OverloadResolutionPriority(1)]
		public static void SetValues(this IFdbTransaction trans, ReadOnlySpan<KeyValuePair<Slice, Slice>> keyValuePairs)
		{
			Contract.NotNull(trans);

			foreach (var kv in keyValuePairs)
			{
				trans.Set(ToSpanKey(kv.Key), ToSpanValue(kv.Value));
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		[OverloadResolutionPriority(1)]
		public static void SetValues(this IFdbTransaction trans, params ReadOnlySpan<(Slice Key, Slice Value)> keyValuePairs)
		{
			Contract.NotNull(trans);

			for(int i = 0; i < keyValuePairs.Length; i++)
			{
				trans.Set(ToSpanKey(keyValuePairs[i].Key), ToSpanValue(keyValuePairs[i].Value));
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to set</param>
		/// <param name="values">Array of values for each key. Must be in the same order as <paramref name="keys"/> and have the same length.</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/>, <paramref name="keys"/> or <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentException">If the <paramref name="values"/> does not have the same length as <paramref name="keys"/>.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, Slice[] keys, Slice[] values)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);
			Contract.NotNull(values);

			trans.SetValues(keys.AsSpan(), values.AsSpan());
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to set</param>
		/// <param name="values">Array of values for each key. Must be in the same order as <paramref name="keys"/> and have the same length.</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/>, <paramref name="keys"/> or <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentException">If the <paramref name="values"/> does not have the same length as <paramref name="keys"/>.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, ReadOnlySpan<Slice> keys, ReadOnlySpan<Slice> values)
		{
			Contract.NotNull(trans);
			if (values.Length != keys.Length) throw new ArgumentException("Both key and value arrays must have the same size.", nameof(values));

			for (int i = 0; i < keys.Length; i++)
			{
				trans.Set(ToSpanKey(keys[i]), ToSpanValue(values[i]));
			}
		}

		/// <summary>Set the values of a sequence of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Sequence of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, IEnumerable<KeyValuePair<Slice, Slice>> keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			if (keyValuePairs.TryGetSpan(out var span))
			{
				trans.SetValues(span);
			}
			else if (keyValuePairs is Dictionary<Slice, Slice> dict)
			{
				foreach (var kv in dict)
				{
					trans.Set(ToSpanKey(kv.Key), ToSpanValue(kv.Value));
				}
			}
			else
			{
				foreach (var kv in keyValuePairs)
				{
					trans.Set(ToSpanKey(kv.Key), ToSpanValue(kv.Value));
				}
			}
		}

		/// <summary>Set the values of a sequence of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Sequence of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, IEnumerable<(Slice Key, Slice Value)> keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			if (keyValuePairs.TryGetSpan(out var span))
			{
				trans.SetValues(span);
			}
			else
			{
				foreach (var kv in keyValuePairs)
				{
					trans.Set(ToSpanKey(kv.Key), ToSpanValue(kv.Value));
				}
			}
		}

		/// <summary>Set the values of a sequence of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to set</param>
		/// <param name="values">Sequence of values for each key. Must be in the same order as <paramref name="keys"/> and have the same number of elements.</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/>, <paramref name="keys"/> or <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentException">If the <paramref name="values"/> does not have the same number of elements as <paramref name="keys"/>.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, IEnumerable<Slice> keys, IEnumerable<Slice> values)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);
			Contract.NotNull(values);

			// attempt to extract a Span from both keys & values
			// if we fail, then fallback to a slow enumeration

			if (keys.TryGetSpan(out var keySpan) && values.TryGetSpan(out var valueSpan))
			{
				trans.SetValues(keySpan, valueSpan);
			}
			else
			{
				SetValuesSlow(trans, keys, values);
			}


			static void SetValuesSlow(IFdbTransaction trans, IEnumerable<Slice> keys, IEnumerable<Slice> values)
			{
				using var keyIterator = keys.GetEnumerator();
				using var valueIterator = values.GetEnumerator();

				while(keyIterator.MoveNext())
				{
					if (!valueIterator.MoveNext())
					{
						throw new ArgumentException("Both key and value sequences must have the same size.", nameof(values));
					}

					trans.Set(ToSpanKey(keyIterator.Current), ToSpanValue(valueIterator.Current));
				}
				if (valueIterator.MoveNext())
				{
					throw new ArgumentException("Both key and values sequences must have the same size.", nameof(values));
				}
			}

		}

#if NET9_0_OR_GREATER

		[OverloadResolutionPriority(1)]
		public static void SetValues<TElement>(this IFdbTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, ReadOnlySpan<byte>> keySelector, Func<TElement, ReadOnlySpan<byte>> valueSelector)
		{
			for (int i = 0; i < items.Length; i++)
			{
				trans.Set(keySelector(items[i]), valueSelector(items[i]));
			}
		}

		public static void SetValues<TElement>(this IFdbTransaction trans, IEnumerable<TElement> items, Func<TElement, ReadOnlySpan<byte>> keySelector, Func<TElement, ReadOnlySpan<byte>> valueSelector)
		{
			if (items.TryGetSpan(out var span))
			{
				trans.SetValues(span, keySelector, valueSelector);
				return;
			}

			foreach(var item in items)
			{
				trans.Set(keySelector(item), valueSelector(item));
			}
		}

#endif

		[OverloadResolutionPriority(1)]
		public static void SetValues<TElement>(this IFdbTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, Slice> keySelector, Func<TElement, Slice> valueSelector)
		{
			foreach (var item in items)
			{
				trans.Set(ToSpanKey(keySelector(item)), ToSpanValue(valueSelector(item)));
			}
		}

		public static void SetValues<TElement>(this IFdbTransaction trans, IEnumerable<TElement> items, Func<TElement, Slice> keySelector, Func<TElement, Slice> valueSelector)
		{
			if (items.TryGetSpan(out var span))
			{
				trans.SetValues(span, keySelector, valueSelector);
				return;
			}

			foreach(var item in items)
			{
				trans.Set(ToSpanKey(keySelector(item)), ToSpanValue(valueSelector(item)));
			}
		}

		[OverloadResolutionPriority(1)]
		public static void SetValues<TElement, TKey, TValue>(this IFdbTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, TKey> keySelector, Func<TElement, TValue> valueSelector)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			// we will reuse the same pooled buffer for encoding keys, and another one for encoding values
			byte[]? keyBuffer = null;
			byte[]? valueBuffer = null;
			var pool = ArrayPool<byte>.Shared;

			try
			{
				for (int i = 0; i < items.Length; i++)
				{
					var key = keySelector(items[i]);
					var value = valueSelector(items[i]);
					if (!key.TryGetSpan(out var keySpan))
					{
						keySpan = FdbKeyHelpers.Encode(in key, ref keyBuffer, pool);
					}
					if (!value.TryGetSpan(out var valueSpan))
					{
						valueSpan = FdbValueHelpers.Encode(in value, ref valueBuffer, pool);
					}
					trans.Set(keySpan, valueSpan);
				}
			}
			finally
			{
				if (valueBuffer is not null)
				{
					pool.Return(valueBuffer);
				}
				if (keyBuffer is not null)
				{
					pool.Return(keyBuffer);
				}
			}
		}

		public static void SetValues<TElement, TKey, TValue>(this IFdbTransaction trans, IEnumerable<TElement> items, Func<TElement, TKey> keySelector, Func<TElement, TValue> valueSelector)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (items.TryGetSpan(out var span))
			{
				trans.SetValues<TElement, TKey, TValue>(span, keySelector, valueSelector);
				return;
			}

			// we will reuse the same pooled buffer for encoding keys, and another one for encoding values
			byte[]? keyBuffer = null;
			byte[]? valueBuffer = null;
			var pool = ArrayPool<byte>.Shared;

			try
			{
				foreach(var item in items)
				{
					var key = keySelector(item);
					var value = valueSelector(item);
					if (!key.TryGetSpan(out var keySpan))
					{
						keySpan = FdbKeyHelpers.Encode(in key, ref keyBuffer, pool);
					}
					if (!value.TryGetSpan(out var valueSpan))
					{
						valueSpan = FdbValueHelpers.Encode(in value, ref valueBuffer, pool);
					}
					trans.Set(keySpan, valueSpan);
				}
			}
			finally
			{
				if (valueBuffer is not null)
				{
					pool.Return(valueBuffer);
				}
				if (keyBuffer is not null)
				{
					pool.Return(keyBuffer);
				}
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Span of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If <paramref name="trans"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		[OverloadResolutionPriority(1)]
		public static void SetValues<TKey, TValue>(this IFdbTransaction trans, ReadOnlySpan<(TKey Key, TValue Value)> items)
			where TKey : struct, IFdbKey
			where TValue : struct, ISpanEncodable
		{
			// we will reuse the same pooled buffer for encoding keys, and another one for encoding values
			byte[]? keyBuffer = null;
			byte[]? valueBuffer = null;
			var pool = ArrayPool<byte>.Shared;

			try
			{
				for (int i = 0; i < items.Length; i++)
				{
					if (!items[i].Key.TryGetSpan(out var keySpan))
					{
						keySpan = FdbKeyHelpers.Encode(in items[i].Key, ref keyBuffer, pool);
					}
					if (!items[i].Value.TryGetSpan(out var valueSpan))
					{
						valueSpan = FdbValueHelpers.Encode(in items[i].Value, ref valueBuffer, pool);
					}
					trans.Set(keySpan, valueSpan);
				}
			}
			finally
			{
				if (valueBuffer is not null)
				{
					pool.Return(valueBuffer);
				}
				if (keyBuffer is not null)
				{
					pool.Return(keyBuffer);
				}
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="items"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues<TKey, TValue>(this IFdbTransaction trans, IEnumerable<(TKey Key, TValue Value)> items)
			where TKey : struct, IFdbKey
			where TValue : struct, ISpanEncodable
		{
			if (items.TryGetSpan(out var span))
			{
				trans.SetValues<TKey, TValue>(span);
				return;
			}

			// we will reuse the same pooled buffer for encoding keys, and another one for encoding values
			byte[]? keyBuffer = null;
			byte[]? valueBuffer = null;
			var pool = ArrayPool<byte>.Shared;

			try
			{
				foreach(var kv in items)
				{
					if (!kv.Key.TryGetSpan(out var keySpan))
					{
						keySpan = FdbKeyHelpers.Encode(in kv.Key, ref keyBuffer, pool);
					}
					if (!kv.Value.TryGetSpan(out var valueSpan))
					{
						valueSpan = FdbValueHelpers.Encode(in kv.Value, ref valueBuffer, pool);
					}
					trans.Set(keySpan, valueSpan);
				}
			}
			finally
			{
				if (valueBuffer is not null)
				{
					pool.Return(valueBuffer);
				}
				if (keyBuffer is not null)
				{
					pool.Return(keyBuffer);
				}
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Span of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If <paramref name="trans"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		[OverloadResolutionPriority(1)]
		public static void SetValues<TKey, TValue>(this IFdbTransaction trans, ReadOnlySpan<KeyValuePair<TKey, TValue>> items)
			where TKey : struct, IFdbKey
			where TValue : struct, ISpanEncodable
		{
			// we will reuse the same pooled buffer for encoding keys, and another one for encoding values
			byte[]? keyBuffer = null;
			byte[]? valueBuffer = null;
			var pool = ArrayPool<byte>.Shared;

			try
			{
				for (int i = 0; i < items.Length; i++)
				{
					if (!items[i].Key.TryGetSpan(out var keySpan))
					{
						keySpan = FdbKeyHelpers.Encode(items[i].Key, ref keyBuffer, pool);
					}
					if (!items[i].Value.TryGetSpan(out var valueSpan))
					{
						valueSpan = FdbValueHelpers.Encode(items[i].Value, ref valueBuffer, pool);
					}
					trans.Set(keySpan, valueSpan);
				}
			}
			finally
			{
				if (valueBuffer is not null)
				{
					pool.Return(valueBuffer);
				}
				if (keyBuffer is not null)
				{
					pool.Return(keyBuffer);
				}
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="items"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues<TKey, TValue>(this IFdbTransaction trans, IEnumerable<KeyValuePair<TKey, TValue>> items)
			where TKey : struct, IFdbKey
			where TValue : struct, ISpanEncodable
		{
			if (items.TryGetSpan(out var span))
			{
				trans.SetValues<TKey, TValue>(span);
				return;
			}

			// we will reuse the same pooled buffer for encoding keys, and another one for encoding values
			byte[]? keyBuffer = null;
			byte[]? valueBuffer = null;
			var pool = ArrayPool<byte>.Shared;

			try
			{
				foreach(var kv in items)
				{
					if (!kv.Key.TryGetSpan(out var keySpan))
					{
						keySpan = FdbKeyHelpers.Encode(kv.Key, ref keyBuffer, pool);
					}
					if (!kv.Value.TryGetSpan(out var valueSpan))
					{
						valueSpan = FdbValueHelpers.Encode(kv.Value, ref valueBuffer, pool);
					}
					trans.Set(keySpan, valueSpan);
				}
			}
			finally
			{
				if (valueBuffer is not null)
				{
					pool.Return(valueBuffer);
				}
				if (keyBuffer is not null)
				{
					pool.Return(keyBuffer);
				}
			}
		}

		#endregion

		#region Atomic Ops...

		/// <inheritdoc cref="IFdbTransaction.Atomic"/>
		public static void Atomic(this IFdbTransaction trans, Slice key, Slice param, FdbMutationType mutation)
		{
			trans.Atomic(ToSpanKey(key), ToSpanValue(param), mutation);
		}

		/// <inheritdoc cref="IFdbTransaction.Atomic"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Atomic<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue param, FdbMutationType mutation)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (param.TryGetSpan(out var paramSpan))
			{
				trans.Atomic(in key, paramSpan, mutation);
			}
			else
			{
				using var paramBytes = FdbValueHelpers.Encode(in param, ArrayPool<byte>.Shared);
				trans.Atomic(in key, paramBytes.Span, mutation);
			}
		}

		/// <inheritdoc cref="IFdbTransaction.Atomic"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Atomic<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, in TValue param, FdbMutationType mutation)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (param.TryGetSpan(out var paramSpan))
			{
				trans.Atomic(key, paramSpan, mutation);
			}
			else
			{
				using var paramBytes = FdbValueHelpers.Encode(in param, ArrayPool<byte>.Shared);
				trans.Atomic(key, paramBytes.Span, mutation);
			}
		}

		/// <inheritdoc cref="IFdbTransaction.Atomic"/>
		public static void Atomic<TKey>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> param, FdbMutationType mutation)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.Atomic(keySpan, param, mutation);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.Atomic(keyBytes.Span, param, mutation);
			}
		}

		/// <inheritdoc cref="AtomicAdd"/>
		public static void AtomicAdd<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			trans.Atomic(ToSpanKey(key), in value, FdbMutationType.Add);
		}

		/// <inheritdoc cref="AtomicAdd"/>
		public static void AtomicAdd<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			trans.Atomic(in key, in value, FdbMutationType.Add);
		}

		/// <inheritdoc cref="AtomicAdd"/>
		public static void AtomicAdd<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
		{
			trans.Atomic(in key, value, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add the value of <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			trans.Atomic(key, value, FdbMutationType.Add);
		}

		/// <inheritdoc cref="AtomicCompareAndClear(IFdbTransaction,ReadOnlySpan{byte},ReadOnlySpan{byte})"/>
		public static void AtomicCompareAndClear<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue comparand)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in comparand, FdbMutationType.CompareAndClear);

		/// <inheritdoc cref="AtomicCompareAndClear(IFdbTransaction,ReadOnlySpan{byte},ReadOnlySpan{byte})"/>
		public static void AtomicCompareAndClear<TKey>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> comparand)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, comparand, FdbMutationType.CompareAndClear);

		/// <summary>Modify the database snapshot represented by this transaction to clear the value of <paramref name="key"/> only if it is equal to <paramref name="comparand"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be cleared.</param>
		/// <param name="comparand">Value that the key must have, in order to be cleared.</param>
		/// <remarks>
		/// If the <paramref name="key"/> does not exist, or has a different value than <paramref name="comparand"/> then no changes will be performed.
		/// This method requires API version 610 or greater.
		/// </remarks>
		public static void AtomicCompareAndClear(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> comparand)
			=> trans.Atomic(key, comparand, FdbMutationType.CompareAndClear);

		/// <inheritdoc cref="AtomicClearIfZero32(IFdbTransaction,ReadOnlySpan{byte})"/>
		public static void AtomicClearIfZero32(this IFdbTransaction trans, Slice key)
			=> trans.AtomicClearIfZero32(ToSpanKey(key));

		/// <inheritdoc cref="AtomicClearIfZero32(IFdbTransaction,ReadOnlySpan{byte})"/>
		public static void AtomicClearIfZero32<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			Span<byte> zero = stackalloc byte[4];
			zero.Clear();
			trans.Atomic(in key, zero, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 4 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			Span<byte> zero = stackalloc byte[4];
			zero.Clear();
			trans.Atomic(key, zero, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64(this IFdbTransaction trans, Slice key)
			=> trans.AtomicClearIfZero64(ToSpanKey(key));

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			Span<byte> zero = stackalloc byte[8];
			zero.Clear();
			trans.Atomic(in key, zero, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			Span<byte> zero = stackalloc byte[8];
			zero.Clear();
			trans.Atomic(key, zero, FdbMutationType.CompareAndClear);
		}

		/// <summary>Modify the database snapshot represented by this transaction to increment by <see langword="1"/> the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32(this IFdbTransaction trans, Slice key)
			=> trans.AtomicAdd32(ToSpanKey(key), 1);

		/// <summary>Modify the database snapshot represented by this transaction to increment by <see langword="1"/> the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
			=> trans.AtomicAdd32(in key, 1);

		/// <summary>Modify the database snapshot represented by this transaction to increment by <see langword="1"/> the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> trans.AtomicAdd32(key, 1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32(this IFdbTransaction trans, Slice key)
			=> trans.AtomicAdd32(ToSpanKey(key), -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
			=> trans.AtomicAdd32(in key, -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> trans.AtomicAdd32(key, -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement32(this IFdbTransaction trans, Slice key, bool clearIfZero)
		{
			var keySpan = ToSpanKey(key);
			trans.AtomicAdd32(keySpan, -1);
			if (clearIfZero)
			{
				trans.AtomicClearIfZero32(keySpan);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key, bool clearIfZero)
		{
			trans.AtomicAdd32(key, -1);
			if (clearIfZero)
			{
				trans.AtomicClearIfZero32(key);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add <see langword="1"/> to the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
			=> trans.AtomicAdd64(in key, 1);

		/// <summary>Modify the database snapshot represented by this transaction to add <see langword="1"/> to the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64(this IFdbTransaction trans, Slice key)
			=> trans.AtomicAdd64(ToSpanKey(key), 1);

		/// <summary>Modify the database snapshot represented by this transaction to add <see langword="1"/> to the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> trans.AtomicAdd64(key, 1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64(this IFdbTransaction trans, Slice key)
			=> trans.AtomicAdd64(ToSpanKey(key), -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
			=> trans.AtomicAdd64(in key, -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> trans.AtomicAdd64(key, -1);

		/// <inheritdoc cref="AtomicDecrement64(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},bool)"/>
		public static void AtomicDecrement64(this IFdbTransaction trans, Slice key, bool clearIfZero)
			=> trans.AtomicDecrement64(ToSpanKey(key), clearIfZero);

		/// <inheritdoc cref="AtomicDecrement64(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},bool)"/>
		public static void AtomicDecrement64<TKey>(this IFdbTransaction trans, in TKey key, bool clearIfZero)
			where TKey : struct, IFdbKey
		{
			trans.AtomicAdd64(in key, -1);
			if (clearIfZero)
			{
				trans.AtomicClearIfZero64(in key);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key, bool clearIfZero)
		{
			trans.AtomicAdd64(key, -1);
			if (clearIfZero)
			{
				trans.AtomicClearIfZero64(key);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, Slice key, int value)
			=> trans.AtomicAdd32(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32<TKey>(this IFdbTransaction trans, in TKey key, int value)
			where TKey : struct, IFdbKey
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
			trans.Atomic(in key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, Slice key, uint value)
			=> trans.AtomicAdd32(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32<TKey>(this IFdbTransaction trans, in TKey key, uint value)
			where TKey : struct, IFdbKey
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, Slice key, long value)
			=> trans.AtomicAdd64(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64<TKey>(this IFdbTransaction trans, in TKey key, long value)
			where TKey : struct, IFdbKey
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteInt64LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit little-endian value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteInt64LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, Slice key, ulong value)
			=> trans.AtomicAdd64(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64<TKey>(this IFdbTransaction trans, in TKey key, ulong value)
			where TKey : struct, IFdbKey
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteUInt64LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteUInt64LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		#region AtomicAnd...

		/// <inheritdoc cref="AtomicAnd"/>
		public static void AtomicAnd<TValue>(this IFdbTransaction trans, Slice key, in TValue mask)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(ToSpanKey(key), in mask, FdbMutationType.BitAnd);

		/// <inheritdoc cref="AtomicAnd"/>
		public static void AtomicAnd<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue mask)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in mask, FdbMutationType.BitAnd);

		/// <inheritdoc cref="AtomicAnd"/>
		public static void AtomicAnd<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> mask)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, mask, FdbMutationType.BitAnd);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise AND between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		/// <seealso cref="FdbMutationType.BitAnd"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AtomicAnd(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
			=> trans.Atomic(key, mask, FdbMutationType.BitAnd);

		#endregion

		#region AtomicOr...

		/// <inheritdoc cref="AtomicOr"/>
		public static void AtomicOr<TValue>(this IFdbTransaction trans, Slice key, in TValue mask)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(ToSpanKey(key), in mask, FdbMutationType.BitOr);

		/// <inheritdoc cref="AtomicOr"/>
		public static void AtomicOr<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue mask)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in mask, FdbMutationType.BitOr);

		/// <inheritdoc cref="AtomicOr"/>
		public static void AtomicOr<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> mask)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, mask, FdbMutationType.BitOr);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		/// <seealso cref="FdbMutationType.BitOr"/>
		public static void AtomicOr(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
			=> trans.Atomic(key, mask, FdbMutationType.BitOr);

		#endregion

		#region AtomicXor...

		/// <inheritdoc cref="AtomicXor"/>
		public static void AtomicXor<TValue>(this IFdbTransaction trans, Slice key, in TValue mask)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(ToSpanKey(key), in mask, FdbMutationType.BitXor);

		/// <inheritdoc cref="AtomicXor"/>
		public static void AtomicXor<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue mask)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in mask, FdbMutationType.BitXor);

		/// <inheritdoc cref="AtomicXor"/>
		public static void AtomicXor<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> mask)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, mask, FdbMutationType.BitXor);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		/// <seealso cref="FdbMutationType.BitXor"/>
		public static void AtomicXor(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
			=> trans.Atomic(key, mask, FdbMutationType.BitXor);

		#endregion

		#region AtomicAppendIfFits...

		/// <inheritdoc cref="AtomicAppendIfFits"/>
		public static void AtomicAppendIfFits<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(ToSpanKey(key), in value, FdbMutationType.AppendIfFits);

		/// <inheritdoc cref="AtomicAppendIfFits"/>
		public static void AtomicAppendIfFits<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in value, FdbMutationType.AppendIfFits);

		/// <inheritdoc cref="AtomicAppendIfFits"/>
		public static void AtomicAppendIfFits<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, value, FdbMutationType.AppendIfFits);

		/// <summary>Modify the database snapshot represented by this transaction to append to a value, unless it would become larger that the maximum value size supported by the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to append.</param>
		/// <seealso cref="FdbMutationType.AppendIfFits"/>
		public static void AtomicAppendIfFits(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			=> trans.Atomic(key, value, FdbMutationType.AppendIfFits);

		#endregion

		#region AtomicMax...

		/// <inheritdoc cref="AtomicMax"/>
		public static void AtomicMax<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(ToSpanKey(key), in value, FdbMutationType.Max);

		/// <inheritdoc cref="AtomicMax"/>
		public static void AtomicMax<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in value, FdbMutationType.Max);

		/// <inheritdoc cref="AtomicMax"/>
		public static void AtomicMax<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, value, FdbMutationType.Max);

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		/// <seealso cref="FdbMutationType.Max"/>
		public static void AtomicMax(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			=> trans.Atomic(key, value, FdbMutationType.Max);

		#endregion

		#region AtomicMin...

		/// <inheritdoc cref="AtomicMin"/>
		public static void AtomicMin<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(ToSpanKey(key), in value, FdbMutationType.Min);

		/// <inheritdoc cref="AtomicMin"/>
		public static void AtomicMin<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.Atomic(in key, in value, FdbMutationType.Min);

		/// <inheritdoc cref="AtomicMin"/>
		public static void AtomicMin<TKey, TValue>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
			=> trans.Atomic(in key, value, FdbMutationType.Min);

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			=> trans.Atomic(key, value, FdbMutationType.Min);

		#endregion

		/// <summary>Find the location of the VersionStamp in a key or value</summary>
		/// <param name="buffer">Buffer that must contains <paramref name="token"/> once and only once</param>
		/// <param name="token">Token that represents the VersionStamp</param>
		/// <param name="argName"></param>
		/// <returns>Offset in <paramref name="buffer"/> where the stamp was found</returns>
		private static int GetVersionStampOffset(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> token, string argName)
		{
			// the buffer MUST contain one incomplete stamp, either the random token of the current transaction or the default token (all-FF)

			if (buffer.Length < token.Length)
			{
				throw new ArgumentException("The key is too small to contain a VersionStamp.", argName);
			}

			int p = token.Length != 0 ? buffer.IndexOf(token) : -1;
			if (p >= 0)
			{ // found a candidate spot, we have to make sure that it is only present once in the key!

				if (buffer[(p + token.Length)..].IndexOf(token) >= 0)
				{
					throw argName == "key"
						? new ArgumentException("The key should only contain one occurrence of a VersionStamp.", argName)
						: new ArgumentException("The value should only contain one occurrence of a VersionStamp.", argName);
				}
			}
			else
			{ // not found, maybe it is using the default incomplete stamp (all FF) ?
				Span<byte> incomplete = stackalloc byte[10];
				incomplete.Fill(0xFF);
				p = buffer.IndexOf(incomplete);
				if (p < 0)
				{
					throw argName == "key"
						? new ArgumentException("The key should contain at least one VersionStamp.", argName)
						: new ArgumentException("The value should contain at least one VersionStamp.", argName);
				}
			}
			Contract.Debug.Ensures(p + token.Length <= buffer.Length);

			return p;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static NotSupportedException FailVersionStampNotSupported(int apiVersion) => new($"VersionStamps are not supported at API version {apiVersion}. You need to select at least API Version 400 or above.");

		/// <inheritdoc cref="SetVersionStampedKey(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},System.ReadOnlySpan{byte})"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetVersionStampedKey(this IFdbTransaction trans, Slice key, Slice value)
			=> trans.SetVersionStampedKey(ToSpanKey(key), ToSpanValue(value));

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);

			Span<byte> token = stackalloc byte[10];
			trans.CreateVersionStamp().WriteTo(token);
			var offset = GetVersionStampOffset(key, token, nameof(key));

			int apiVer = trans.Context.Database.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				using var writer = new SliceWriter(key.Length + 2, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt16(checked((ushort) offset)); // 16-bits little endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}
			else
			{ // starting from 520, the offset is 32 bits
				using var writer = new SliceWriter(key.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt32(checked((uint) offset)); // 32-bits little endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}

		}

		/// <inheritdoc cref="SetVersionStampedKey(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},int,System.ReadOnlySpan{byte})"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetVersionStampedKey(this IFdbTransaction trans, Slice key, int stampOffset, Slice value)
			=> trans.SetVersionStampedKey(ToSpanKey(key), stampOffset, ToSpanValue(value));

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose start is defined by <paramref name="stampOffset"/>.</param>
		/// <param name="stampOffset">Offset in <paramref name="key"/> where the 80-bit VersionStamp is located.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, ReadOnlySpan<byte> key, int stampOffset, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);
			Contract.Positive(stampOffset);
			if (stampOffset > key.Length - 10)
			{
				throw new ArgumentException("The VersionStamp overflows past the end of the key.", nameof(stampOffset));
			}

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				if (stampOffset > 0xFFFF) throw new ArgumentException("The offset is too large to fit within 16-bits.");
				using var writer = new SliceWriter(key.Length + 2, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt16(checked((ushort) stampOffset)); //stored as 32-bits in Little Endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}
			else
			{ // starting from 520, the offset is 32 bits
				using var writer = new SliceWriter(key.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt32(checked((uint) stampOffset)); //stored as 32-bits in Little Endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey<TKey>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.SetVersionStampedKey(keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.SetVersionStampedKey(keyBytes.Span, value);
			}
		}

		/// <inheritdoc cref="SetVersionStampedKey{TValue}(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},in TValue)"/>
		public static void SetVersionStampedKey<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.SetVersionStampedKey(ToSpanKey(key), in value);

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (value.TryGetSpan(out var valueSpan))
			{
				trans.SetVersionStampedKey(key, valueSpan);
				return;
			}

			if (value.TryGetSizeHint(out int sizeHint) && (uint) sizeHint <= 128)
			{
				Span<byte> buffer = stackalloc byte[sizeHint];
				if (value.TryEncode(buffer, out int bytesWritten))
				{
					trans.SetVersionStampedKey(key, buffer[..bytesWritten]);
					return;
				}
			}

			using var valueBytes = FdbValueHelpers.Encode(in value, ArrayPool<byte>.Shared);
			trans.SetVersionStampedKey(key, valueBytes.Span);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.SetVersionStampedKey<TValue>(keySpan, in value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.SetVersionStampedKey<TValue>(keyBytes.Span, in value);
			}
		}

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> in the value with the resolved value at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value that contains an <see cref="VersionStamp.Incomplete()"/> stamp. This part of the value will be overwritten, by the database, with the resolved <see cref="VersionStamp"/> at commit time.</param>
		/// <remarks>
		/// <para>Prior to API level 520, the version stamp can only be in the first 10 bytes of the value. From 520 and greater, the version stamp can be anywhere inside the value.</para>
		/// <para>There must be only one incomplete version stamp per value, which must be equal to the value returned by <see cref="IFdbTransaction.CreateVersionStamp()"/> (or similar methods).</para>
		/// </remarks>
		public static void SetVersionStampedValue(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);
			if (value.Length < 10)
			{
				throw new ArgumentException("The value must be at least 10 bytes long.", nameof(value));
			}

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the stamp is always at offset 0
				trans.Atomic(key, value, FdbMutationType.VersionStampedValue);
			}
			else
			{ // starting from 520, the offset is stored in the last 32 bits

				int offset;
				{
					Span<byte> token = stackalloc byte[10];
					trans.CreateVersionStamp().WriteTo(token);
					offset = GetVersionStampOffset(value, token, nameof(value));
				}
				Contract.Debug.Requires(offset >=0 && offset <= value.Length - 10);

				using var writer = new SliceWriter(value.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(value);
				writer.WriteUInt32(checked((uint) offset));
				trans.Atomic(key, writer.ToSpan(), FdbMutationType.VersionStampedValue);
			}
		}

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> in the value with the resolved value at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value that contains an <see cref="VersionStamp.Incomplete()"/> stamp. This part of the value will be overwritten, by the database, with the resolved <see cref="VersionStamp"/> at commit time.</param>
		/// <remarks>
		/// <para>Prior to API level 520, the version stamp can only be in the first 10 bytes of the value. From 520 and greater, the version stamp can be anywhere inside the value.</para>
		/// <para>There must be only one incomplete version stamp per value, which must be equal to the value returned by <see cref="IFdbTransaction.CreateVersionStamp()"/> (or similar methods).</para>
		/// </remarks>
		public static void SetVersionStampedValue<TKey>(this IFdbTransaction trans, in TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.SetVersionStampedValue(keySpan, value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.SetVersionStampedValue(keyBytes.Span, value);
			}
		}

		/// <inheritdoc cref="SetVersionStampedValue{TValue}(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},in TValue)"/>
		public static void SetVersionStampedValue<TValue>(this IFdbTransaction trans, Slice key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
			=> trans.SetVersionStampedValue(ToSpanKey(key), in value);

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> in the value with the resolved value at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value that contains an <see cref="VersionStamp.Incomplete()"/> stamp. This part of the value will be overwritten, by the database, with the resolved <see cref="VersionStamp"/> at commit time.</param>
		/// <remarks>
		/// <para>Prior to API level 520, the version stamp can only be in the first 10 bytes of the value. From 520 and greater, the version stamp can be anywhere inside the value.</para>
		/// <para>There must be only one incomplete version stamp per value, which must be equal to the value returned by <see cref="IFdbTransaction.CreateVersionStamp()"/> (or similar methods).</para>
		/// </remarks>
		public static void SetVersionStampedValue<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (value.TryGetSpan(out var valueSpan))
			{
				trans.SetVersionStampedValue(key, valueSpan);
				return;
			}

			if (value.TryGetSizeHint(out int sizeHint) && (uint) sizeHint <= 128)
			{
				Span<byte> buffer = stackalloc byte[sizeHint];
				if (value.TryEncode(buffer, out int bytesWritten))
				{
					trans.SetVersionStampedValue(key, buffer[..bytesWritten]);
					return;
				}
			}

			using var valueBytes = FdbValueHelpers.Encode(in value, ArrayPool<byte>.Shared);
			trans.SetVersionStampedValue(key, valueBytes.Span);
		}

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> in the value with the resolved value at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value that contains an <see cref="VersionStamp.Incomplete()"/> stamp. This part of the value will be overwritten, by the database, with the resolved <see cref="VersionStamp"/> at commit time.</param>
		/// <remarks>
		/// <para>Prior to API level 520, the version stamp can only be in the first 10 bytes of the value. From 520 and greater, the version stamp can be anywhere inside the value.</para>
		/// <para>There must be only one incomplete version stamp per value, which must be equal to the value returned by <see cref="IFdbTransaction.CreateVersionStamp()"/> (or similar methods).</para>
		/// </remarks>
		public static void SetVersionStampedValue<TKey, TValue>(this IFdbTransaction trans, in TKey key, in TValue value)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.SetVersionStampedValue<TValue>(keySpan, in value);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.SetVersionStampedValue<TValue>(keyBytes.Span, in value);
			}
		}

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> at the given offset in the value with the resolved VersionStamp at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, Slice key, Slice value, int stampOffset)
			=> trans.SetVersionStampedValue(ToSpanKey(key), ToSpanValue(value), stampOffset);

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> at the given offset in the value with the resolved VersionStamp at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue<TKey>(this IFdbTransaction trans, in TKey key, Slice value, int stampOffset)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.SetVersionStampedValue(keySpan, ToSpanValue(value), stampOffset);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.SetVersionStampedValue(keyBytes.Span, ToSpanValue(value), stampOffset);
			}
		}

		/// <summary>Sets the <paramref name="value"/> of the <paramref name="key"/> in the database, filling the incomplete <see cref="VersionStamp"/> at the given offset in the value with the resolved VersionStamp at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, int stampOffset)
		{
			Contract.NotNull(trans);
			Contract.Positive(stampOffset);
			if (stampOffset > value.Length - 10) throw new ArgumentException("The VersionStamp overflows past the end of the value.", nameof(stampOffset));

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the stamp is always at offset 0
				if (stampOffset != 0) throw new InvalidOperationException("Prior to API version 520, the VersionStamp can only be located at offset 0. Please update to API Version 520 or above!");
				// let it slide!
				trans.Atomic(key, value, FdbMutationType.VersionStampedValue);
			}
			else
			{ // starting from 520, the offset is stored in the last 32 bits
				using var writer = new SliceWriter(value.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(value);
				writer.WriteUInt32(checked((uint) stampOffset));
				trans.Atomic(key, writer.ToSpan(), FdbMutationType.VersionStampedValue);
			}

		}

		#endregion

		#region GetRange...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbKeyValueRangeQuery GetRange(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			var sp = KeySelectorPair.Create(range);
			return trans.GetRange(sp.Begin, sp.End, options);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange"/>
		[Pure, LinqTunnel]
		public static IFdbKeyValueRangeQuery GetRange<TBeginKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginKeyInclusive, KeySelector endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			//PERF: optimize!
			var beginSelector = beginKeyInclusive.ToSelector();

			return trans.GetRange(
				beginSelector,
				endKeyExclusive,
				options
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange"/>
		[Pure, LinqTunnel]
		public static IFdbKeyValueRangeQuery GetRange<TEndKey>(this IFdbReadOnlyTransaction trans, KeySelector beginKeyInclusive, in FdbKeySelector<TEndKey> endKeyExclusive, FdbRangeOptions? options = null)
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			//PERF: optimize!
			var endSelector = endKeyExclusive.ToSelector();

			return trans.GetRange(
				beginKeyInclusive,
				endSelector,
				options
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange"/>
		[Pure, LinqTunnel]
		public static IFdbKeyValueRangeQuery GetRange<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginKeyInclusive, in FdbKeySelector<TEndKey> endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			//PERF: optimize!
			var beginSelector = beginKeyInclusive.ToSelector();
			var endSelector = endKeyExclusive.ToSelector();

			return trans.GetRange(
				beginSelector,
				endSelector,
				options
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRange<TBeginKey, TEndKey, TState, TResult>(
			this IFdbReadOnlyTransaction trans,
			in FdbKeySelector<TBeginKey> beginKeyInclusive,
			in FdbKeySelector<TEndKey> endKeyExclusive,
			TState state,
			FdbKeyValueDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null
		)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			//TODO: optimize this!
			return trans.GetRange(
				beginKeyInclusive.ToSelector(),
				endKeyExclusive.ToSelector(),
				state,
				decoder,
				options
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRange<TKeyRange, TState, TResult>(
			this IFdbReadOnlyTransaction trans,
			in TKeyRange range,
			TState state,
			FdbKeyValueDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null
		)
			where TKeyRange : struct, IFdbKeyRange
		{
			return trans.GetRange(range.ToKeyRange(), state, decoder, options);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRange<TBeginKey, TEndKey, TState, TResult>(
			this IFdbReadOnlyTransaction trans,
			in TBeginKey beginKeyInclusive,
			in TEndKey endKeyExclusive,
			TState state,
			FdbKeyValueDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null
		)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			return trans.GetRange(
				beginKeyInclusive.FirstGreaterOrEqual(),
				endKeyExclusive.FirstGreaterOrEqual(),
				state,
				decoder,
				options
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbKeyValueRangeQuery GetRange<TBeginKey>(this IFdbReadOnlyTransaction trans, TBeginKey beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			//PERF: optimize!
			var beginKeyBytes = FdbKeyHelpers.ToSlice(in beginKeyInclusive);

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyBytes),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
				options
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbKeyValueRangeQuery GetRange<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, TBeginKey beginKeyInclusive, TEndKey endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			//PERF: optimize!
			var beginKeyBytes = FdbKeyHelpers.ToSlice(in beginKeyInclusive);
			var endKeyBytes = FdbKeyHelpers.ToSlice(in endKeyExclusive);

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyBytes),
				KeySelector.FirstGreaterOrEqual(endKeyBytes),
				options
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbKeyValueRangeQuery GetRange<TKeyRange>(this IFdbReadOnlyTransaction trans, TKeyRange range, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
		{
			Contract.NotNull(trans);

			//PERF: optimize!
			var rangeBytes = range.ToKeyRange();

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(rangeBytes.Begin),
				KeySelector.FirstGreaterOrEqual(rangeBytes.End),
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbKeyValueRangeQuery GetRange(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbKeyValueRangeQuery GetRange(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			return trans.GetRange(range.Begin, range.End, options);
		}

		#endregion

		#region GetRange<T>...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			return trans.GetRange(KeySelectorPair.Create(range), transform, options);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
				transform,
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TState, TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End),
				state,
				decoder,
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TState, TResult>(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
				state,
				decoder,
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, KeySelectorPair range, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			return trans.GetRange(range.Begin, range.End, transform, options);
		}

		#endregion

		#region GetRangeKeys...

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			var selectors = KeySelectorPair.Create(range);
			return trans.GetRange(
				selectors.Begin,
				selectors.End,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeDecoder<TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End),
				decoder,
				static (fn, k, _) => fn(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TState, TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, TState state, FdbRangeDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End),
				(State: state, Decoder: decoder),
				static (s, k, _) => s.Decoder(s.State, k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TKeyRange, TResult>(this IFdbReadOnlyTransaction trans, in TKeyRange range, FdbRangeDecoder<TResult> decoder, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
			=> trans.GetRangeKeys(range.ToKeyRange(), decoder, options);

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TKeyRange, TState, TResult>(this IFdbReadOnlyTransaction trans, in TKeyRange range, TState state, FdbRangeDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
			=> trans.GetRangeKeys(range.ToKeyRange(), state, decoder, options);

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys<TKeyRange>(this IFdbReadOnlyTransaction trans, TKeyRange range, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
		{
			Contract.NotNull(trans);
			var selectors = KeySelectorPair.Create(range.ToKeyRange()); //PERF: Optimize this!
			return trans.GetRange(
				selectors.Begin,
				selectors.End,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TBeginKey, TEndKey, TState, TResult>(
			this IFdbReadOnlyTransaction trans,
			in TBeginKey beginKeyInclusive,
			in TEndKey endKeyExclusive,
			TState state,
			FdbRangeDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null
		)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			//TODO: optimize this!
			return trans.GetRangeKeys(
				beginKeyInclusive.FirstGreaterOrEqual().ToSelector(),
				endKeyExclusive.FirstGreaterOrEqual().ToSelector(),
				state,
				decoder,
				options
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TBeginKey, TEndKey, TState, TResult>(
			this IFdbReadOnlyTransaction trans,
			in FdbKeySelector<TBeginKey> beginKeyInclusive,
			in FdbKeySelector<TEndKey> endKeyExclusive,
			TState state,
			FdbRangeDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null
		)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			//TODO: optimize this!
			return trans.GetRangeKeys(
				beginKeyInclusive.ToSelector(),
				endKeyExclusive.ToSelector(),
				state,
				decoder,
				options
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeKeys<TState, TResult>(
			this IFdbReadOnlyTransaction trans,
			KeySelector beginInclusive,
			KeySelector endExclusive,
			TState state,
			FdbRangeDecoder<TState, TResult> decoder,
			FdbRangeOptions? options = null
		)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				(State: state, Decoder: decoder),
				(s, k, _) => s.Decoder(s.State, k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive.IsNullOrEmpty ? FdbKey.MinValue : beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive.IsNullOrEmpty ? FdbKey.MaxValue : endKeyExclusive),
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in TBeginKey beginKeyInclusive, in TEndKey endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginKeyInclusive.FirstGreaterOrEqual(),
				endKeyExclusive.FirstGreaterOrEqual(),
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginKeyInclusive, in FdbKeySelector<TEndKey> endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				in beginKeyInclusive,
				in endKeyExclusive,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				range.Begin,
				range.End,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		#endregion

		#region GetRangeValues...

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End),
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeValues<TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeDecoder<TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End),
				decoder,
				static (fn, _, v) => fn(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeValues<TState, TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, TState state, FdbRangeDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End),
				(State: state, Decoder: decoder),
				static (s, _, v) => s.Decoder(s.State, v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues<TKeyRange>(this IFdbReadOnlyTransaction trans, in TKeyRange range, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
			=> trans.GetRangeValues(range.ToKeyRange(), options); //PERF: optimize!

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeValues<TKeyRange, TState, TResult>(this IFdbReadOnlyTransaction trans, in TKeyRange range, TState state, FdbRangeDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
		{
			//TODO: optimize this!
			return trans.GetRangeValues(range.ToKeyRange(), state, decoder, options);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeValues<TKeyRange, TResult>(this IFdbReadOnlyTransaction trans, in TKeyRange range, FdbRangeDecoder<TResult> decoder, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
		{
			//TODO: optimize this!
			return trans.GetRangeValues(range.ToKeyRange(), decoder, options);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRange{TState,TResult}"/>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeValues<TBeginKey, TEndKey, TState, TResult>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginKeyInclusive, in FdbKeySelector<TEndKey> endKeyExclusive, TState state, FdbRangeDecoder<TState, TResult> decoder, FdbRangeOptions? options = null
		)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			//TODO: optimize this!
			return trans.GetRangeValues(
				beginKeyInclusive.ToSelector(),
				endKeyExclusive.ToSelector(),
				state,
				decoder,
				options
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<TResult> GetRangeValues<TState, TResult>(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, TState state, FdbRangeDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				(State: state, Decoder: decoder),
				(s, _, v) => s.Decoder(s.State, v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive.IsNullOrEmpty ? FdbKey.MinValue : beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive.IsNullOrEmpty ? FdbKey.MaxValue : endKeyExclusive),
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in TBeginKey beginKeyInclusive, in TEndKey endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginKeyInclusive.FirstGreaterOrEqual(),
				endKeyExclusive.FirstGreaterOrEqual(),
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginKeyInclusive, in FdbKeySelector<TEndKey> endKeyExclusive, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				in beginKeyInclusive,
				in endKeyExclusive,
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				range.Begin,
				range.End,
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		#endregion

		#region GetRangeAsync...

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/>
		public static Task<FdbRangeChunk> GetRangeAsync<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginInclusive, in FdbKeySelector<TEndKey> endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			// we will first encode the Begin key selector, and defer encoding the End key selector in another method
			// => this should allow for a fast path when the JIT inlines a TryGetSpan that always return true

			if (FdbKeySelectorHelpers.TryGetSpan(in beginInclusive, out var beginSelector))
			{
				return trans.GetRangeAsync(beginSelector, in endExclusive, options, iteration);
			}

			byte[]? buffer = null;
			try
			{
				var selectorBytes = FdbKeySelectorHelpers.Encode(in beginInclusive, ref buffer, ArrayPool<byte>.Shared);
				return trans.GetRangeAsync(selectorBytes.ToSpan(), in endExclusive, options, iteration);
			}
			finally
			{
				if (buffer is not null)
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
		}

		public static Task<FdbRangeChunk> GetRangeAsync<TBeginKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			where TBeginKey : struct, IFdbKey
		{
			return trans.GetRangeAsync(in beginInclusive, endExclusive.ToSpan(), options, iteration);
		}

		public static Task<FdbRangeChunk> GetRangeAsync<TBeginKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginInclusive, KeySpanSelector endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			where TBeginKey : struct, IFdbKey
		{
			if (FdbKeySelectorHelpers.TryGetSpan(in beginInclusive, out var beginSelector))
			{
				return trans.GetRangeAsync(beginSelector, endExclusive, options, iteration);
			}

			byte[]? buffer = null;
			try
			{
				var selectorBytes = FdbKeySelectorHelpers.Encode(in beginInclusive, ref buffer, ArrayPool<byte>.Shared);
				return trans.GetRangeAsync(selectorBytes.ToSpan(), endExclusive, options, iteration);
			}
			finally
			{
				if (buffer is not null)
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
		}

		public static Task<FdbRangeChunk> GetRangeAsync<TEndKey>(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, in FdbKeySelector<TEndKey> endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			where TEndKey : struct, IFdbKey
		{
			return trans.GetRangeAsync(beginInclusive.ToSpan(), in endExclusive, options, iteration);
		}

		public static Task<FdbRangeChunk> GetRangeAsync<TEndKey>(this IFdbReadOnlyTransaction trans, KeySpanSelector beginInclusive, in FdbKeySelector<TEndKey> endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			where TEndKey : struct, IFdbKey
		{
			if (FdbKeySelectorHelpers.TryGetSpan(in endExclusive, out var endSelector))
			{
				return trans.GetRangeAsync(beginInclusive, endSelector, options, iteration);
			}

			byte[]? buffer = null;
			try
			{
				var selectorBytes = FdbKeySelectorHelpers.Encode(in endExclusive, ref buffer, ArrayPool<byte>.Shared);
				return trans.GetRangeAsync(beginInclusive, selectorBytes.ToSpan(), options, iteration);
			}
			finally
			{
				if (buffer is not null)
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the Begin key selector
		/// and lexicographically less than the key resolved by the End key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">key selector pair defining the beginning and the end of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null, int iteration = 0)
		{
			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the Begin key selector
		/// and lexicographically less than the key resolved by the End key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys defining the beginning (inclusive) and the end (exclusive) of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null, int iteration = 0)
		{
			var sp = KeySelectorPair.Create(range);
			return trans.GetRangeAsync(sp.Begin, sp.End, options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the Begin key selector
		/// and lexicographically less than the key resolved by the End key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, FdbRangeOptions? options = null, int iteration = 0)
		{
			var range = KeySelectorPair.Create(beginInclusive, endExclusive);
			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		/// <inheritdoc cref="GetRangeAsync(IFdbReadOnlyTransaction,Slice,Slice,FdbRangeOptions?,int)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<FdbRangeChunk> GetRangeAsync<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in TBeginKey beginInclusive, in TEndKey endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			return trans.GetRangeAsync(beginInclusive.FirstGreaterOrEqual(), endExclusive.FirstGreaterOrEqual(), options, iteration);
		}

		/// <inheritdoc cref="GetRangeAsync(IFdbReadOnlyTransaction,Slice,Slice,FdbRangeOptions?,int)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<FdbRangeChunk> GetRangeAsync<TKeyRange>(this IFdbReadOnlyTransaction trans, in TKeyRange range, FdbRangeOptions? options = null, int iteration = 0)
			where TKeyRange : struct, IFdbKeyRange
		{
			//PERF: optimize!
			return trans.GetRangeAsync(range.ToKeyRange(), options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the Begin key selector
		/// and lexicographically less than the key resolved by the End key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null, int iteration = 0)
		{
			return trans.GetRangeAsync<TState, TResult>(
				KeySelector.FirstGreaterOrEqual(beginInclusive),
				KeySelector.FirstGreaterOrEqual(endExclusive),
				state,
				decoder,
				options,
				iteration
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/>
		public static Task<FdbRangeChunk<TResult>> GetRangeAsync<TBeginKey, TEndKey, TState, TResult>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginInclusive, in FdbKeySelector<TEndKey> endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null, int iteration = 0)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			return trans.GetRangeAsync(
				beginInclusive.ToSelector(), //PERF: optimize!
				endExclusive.ToSelector(),   //PERF: optimize!
				state,
				decoder,
				options,
				iteration
			);
		}

		/// <inheritdoc cref="GetRangeAsync(FoundationDB.Client.IFdbReadOnlyTransaction,Slice,Slice,FdbRangeOptions?,int)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<FdbRangeChunk<TResult>> GetRangeAsync<TBeginKey, TEndKey, TState, TResult>(this IFdbReadOnlyTransaction trans, in TBeginKey beginInclusive, in TEndKey endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null, int iteration = 0)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			return trans.GetRangeAsync(
				beginInclusive.FirstGreaterOrEqual(),
				endExclusive.FirstGreaterOrEqual(),
				state,
				decoder,
				options,
				iteration
			);
		}

		#endregion

		#region VisitRangeAsync...

		/// <summary>Visits all key-value pairs in the database snapshot represent by the transaction</summary>
		/// <typeparam name="TState">Type of the state that is passed to the handler</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys defining the beginning (inclusive) and the end (exclusive) of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="visitor"/></param>
		/// <param name="visitor">Lambda called for each key-value pair, in order.</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns></returns>
		public static Task<long> VisitRangeAsync<TState>(this IFdbReadOnlyTransaction trans, KeyRange range, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
		{
			return trans.VisitRangeAsync(range.Begin, range.End, state, visitor, options);
		}

		/// <summary>Visits all key-value pairs in the database snapshot represent by the transaction</summary>
		/// <typeparam name="TState">Type of the state that is passed to the handler</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="visitor"/></param>
		/// <param name="visitor">Lambda called for each key-value pair, in order.</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns></returns>
		public static Task<long> VisitRangeAsync<TState>(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			if (beginInclusive.IsNullOrEmpty) beginInclusive = FdbKey.MinValue;
			if (endExclusive.IsNullOrEmpty) endExclusive = FdbKey.MaxValue;

			return trans.VisitRangeAsync(
				KeySelector.FirstGreaterOrEqual(beginInclusive),
				KeySelector.FirstGreaterOrEqual(endExclusive),
				state,
				visitor,
				options
			);
		}

		/// <inheritdoc cref="VisitRangeAsync{TState}(IFdbReadOnlyTransaction,Slice,Slice,TState,FdbKeyValueAction{TState},FdbRangeOptions?)"/>
		public static Task<long> VisitRangeAsync<TKeyRange, TState>(this IFdbReadOnlyTransaction trans, in TKeyRange range, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
			where TKeyRange : struct, IFdbKeyRange
		{
			Contract.NotNull(trans);

			return trans.VisitRangeAsync(
				range.ToBeginSelector(),
				range.ToEndSelector(),
				state,
				visitor,
				options
			);
		}

		/// <inheritdoc cref="VisitRangeAsync{TState}(IFdbReadOnlyTransaction,Slice,Slice,TState,FdbKeyValueAction{TState},FdbRangeOptions?)"/>
		public static Task<long> VisitRangeAsync<TBeginKey, TEndKey, TState>(this IFdbReadOnlyTransaction trans, in TBeginKey beginInclusive, in TEndKey endExclusive, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			return trans.VisitRangeAsync(
				beginInclusive.FirstGreaterOrEqual(),
				endExclusive.FirstGreaterOrEqual(),
				state,
				visitor,
				options
			);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.VisitRangeAsync{TState}"/>
		public static Task<long> VisitRangeAsync<TBeginKey, TEndKey, TState>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TBeginKey> beginInclusive, in FdbKeySelector<TEndKey> endExclusive, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			return trans.VisitRangeAsync(
				beginInclusive.ToSelector(), //PERF: optimize!
				endExclusive.ToSelector(),   //PERF: optimize!
				state,
				visitor,
				options
			);
		}

		#endregion

		#region GetAddressesForKeyAsync...

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetAddressesForKeyAsync"/>
		public static Task<string[]> GetAddressesForKeyAsync(this IFdbReadOnlyTransaction trans, Slice key)
		{
			return trans.GetAddressesForKeyAsync(ToSpanKey(key));
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetAddressesForKeyAsync"/>
		public static Task<string[]> GetAddressesForKeyAsync<TKey>(this IFdbReadOnlyTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.GetAddressesForKeyAsync(keySpan);
			}

			using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
			return trans.GetAddressesForKeyAsync(keyBytes.Span);
		}

		#endregion

		#region GetRangeSplitPointsAsync...

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRangeSplitPointsAsync"/>
		public static Task<Slice[]> GetRangeSplitPointsAsync(this IFdbReadOnlyTransaction trans, Slice beginKey, Slice endKey, long chunkSize)
		{
			return trans.GetRangeSplitPointsAsync(ToSpanKey(beginKey), ToSpanKey(endKey), chunkSize);
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRangeSplitPointsAsync"/>
		public static Task<Slice[]> GetRangeSplitPointsAsync<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in TBeginKey beginKey, in TEndKey endKey, long chunkSize)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			if (beginKey.TryGetSpan(out var beginSpan))
			{
				return trans.GetRangeSplitPointsAsync(beginSpan, in endKey, chunkSize);
			}
			else
			{
				using var beginBytes = FdbKeyHelpers.Encode(in beginKey, ArrayPool<byte>.Shared);
				return trans.GetRangeSplitPointsAsync(beginBytes.Span, in endKey, chunkSize);
			}
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetRangeSplitPointsAsync"/>
		public static Task<Slice[]> GetRangeSplitPointsAsync<TEndKey>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> beginKey, in TEndKey endKey, long chunkSize)
			where TEndKey : struct, IFdbKey
		{
			if (endKey.TryGetSpan(out var endSpan))
			{
				return trans.GetRangeSplitPointsAsync(beginKey, endSpan, chunkSize);
			}
			else
			{
				using var endBytes = FdbKeyHelpers.Encode(in endKey, ArrayPool<byte>.Shared);
				return trans.GetRangeSplitPointsAsync(beginKey, endBytes.Span, chunkSize);
			}
		}

		#endregion

		#region GetEstimatedRangeSizeBytesAsync...

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync(this IFdbReadOnlyTransaction trans, Slice beginKey, Slice endKey)
		{
			return trans.GetEstimatedRangeSizeBytesAsync(ToSpanKey(beginKey), ToSpanKey(endKey));
		}

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync<TBeginKey, TEndKey>(this IFdbReadOnlyTransaction trans, in TBeginKey beginKey, in TEndKey endKey)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			if (beginKey.TryGetSpan(out var beginSpan))
			{
				return trans.GetEstimatedRangeSizeBytesAsync(beginSpan, in endKey);
			}
			else
			{
				using var beginBytes = FdbKeyHelpers.Encode(in beginKey, ArrayPool<byte>.Shared);
				return trans.GetEstimatedRangeSizeBytesAsync(beginBytes.Span, in endKey);
			}
		}

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync<TEndKey>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> beginKey, in TEndKey endKey)
			where TEndKey : struct, IFdbKey
		{
			if (endKey.TryGetSpan(out var endSpan))
			{
				return trans.GetEstimatedRangeSizeBytesAsync(beginKey, endSpan);
			}
			else
			{
				using var endBytes = FdbKeyHelpers.Encode(in endKey, ArrayPool<byte>.Shared);
				return trans.GetEstimatedRangeSizeBytesAsync(beginKey, endBytes.Span);
			}
		}

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync(this IFdbReadOnlyTransaction trans, KeyRange range)
		{
			return trans.GetEstimatedRangeSizeBytesAsync(ToSpanKey(range.Begin), ToSpanKey(range.End));
		}

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync<TKeyRange>(this IFdbReadOnlyTransaction trans, in TKeyRange range)
			where TKeyRange : struct, IFdbKeyRange
		{
			var r = range.ToKeyRange(); //PERF: optimize this!
			return trans.GetEstimatedRangeSizeBytesAsync(ToSpanKey(r.Begin), ToSpanKey(r.End));
		}

		#endregion

		#region MetadataVersion...

		/// <inheritdoc cref="IFdbTransaction.TouchMetadataVersionKey"/>
		public static void TouchMetadataVersionKey<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			// the metadata key will be stored in a cache, so we have to allocate!
			trans.TouchMetadataVersionKey(FdbKeyHelpers.ToSlice(in key));
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetMetadataVersionKeyAsync"/>
		public static Task<VersionStamp?> GetMetadataVersionKeyAsync<TKey>(this IFdbReadOnlyTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			// the metadata key will be stored in a cache, so we have to allocate!
			return trans.GetMetadataVersionKeyAsync(FdbKeyHelpers.ToSlice(in key));
		}

		#endregion

		#region CheckValueAsync...

		/// <summary>Checks if the value from the database snapshot represented by the current transaction is equal to some <paramref name="expected"/> value.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="expected">Expected value for this key</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		/// <returns>Return the result of the check, plus the actual value of the key.</returns>
		public static Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(this IFdbReadOnlyTransaction trans, Slice key, Slice expected)
		{
			return trans.CheckValueAsync(ToSpanKey(key), expected);
		}

		public static Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync<TKey>(this IFdbReadOnlyTransaction trans, in TKey key, Slice expected)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				return trans.CheckValueAsync(keySpan, expected);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				return trans.CheckValueAsync(keyBytes.Span, expected);
			}
		}

		public static Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, in TKey key, in TValue expected)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			return trans.CheckValueAsync(in key, FdbValueHelpers.ToSlice(in expected));
		}

		#endregion

		#region Clear...

		/// <inheritdoc cref="IFdbTransaction.Clear"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Clear(this IFdbTransaction trans, Slice key)
		{
			trans.Clear(ToSpanKey(key));
		}

		/// <inheritdoc cref="IFdbTransaction.Clear"/>
		public static void Clear<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var keySpan))
			{
				trans.Clear(keySpan);
			}
			else
			{
				using var keyBytes = FdbKeyHelpers.Encode(in key, ArrayPool<byte>.Shared);
				trans.Clear(keyBytes.Span);
			}
		}

		#endregion

		#region ClearRange...

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Pair of keys defining the range to clear.</param>
		public static void ClearRange(this IFdbTransaction trans, KeyRange range)
		{
			Contract.NotNull(trans);

			trans.ClearRange(ToSpanKey(range.Begin), range.End.HasValue ? ToSpanKey(range.End) : FdbKey.MaxValueSpan);
		}

		/// <inheritdoc cref="IFdbTransaction.ClearRange"/>
		public static void ClearRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			trans.ClearRange(ToSpanKey(beginKeyInclusive), ToSpanKey(endKeyExclusive));
		}

		public static void ClearRange<TKeyRange>(this IFdbTransaction trans, in TKeyRange range)
			where TKeyRange : struct, IFdbKeyRange
		{
			//PERF: optimize!
			trans.ClearRange(range.ToKeyRange());
		}

		/// <inheritdoc cref="IFdbTransaction.ClearRange"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearRange<TBeginKey>(this IFdbTransaction trans, in TBeginKey beginKeyInclusive, Slice endKeyExclusive)
			where TBeginKey : struct, IFdbKey
		{
			if (beginKeyInclusive.TryGetSpan(out var beginKeySpan))
			{
				trans.ClearRange(beginKeySpan, ToSpanKey(endKeyExclusive));
			}
			else
			{
				using var beginKeyBytes = FdbKeyHelpers.Encode(in beginKeyInclusive, ArrayPool<byte>.Shared);
				trans.ClearRange(beginKeyBytes.Span, ToSpanKey(endKeyExclusive));
			}
		}

		/// <inheritdoc cref="IFdbTransaction.ClearRange"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearRange<TEndKey>(this IFdbTransaction trans, Slice beginKeyInclusive, in TEndKey endKeyExclusive)
			where TEndKey : struct, IFdbKey
		{
			if (endKeyExclusive.TryGetSpan(out var endKeySpan))
			{
				trans.ClearRange(ToSpanKey(beginKeyInclusive), endKeySpan);
			}
			else
			{
				using var endKeyBytes = FdbKeyHelpers.Encode(in endKeyExclusive, ArrayPool<byte>.Shared);
				trans.ClearRange(ToSpanKey(beginKeyInclusive), endKeyBytes.Span);
			}
		}

		/// <inheritdoc cref="IFdbTransaction.ClearRange"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearRange<TBeginKey, TEndKey>(this IFdbTransaction trans, in TBeginKey beginKeyInclusive, in TEndKey endKeyExclusive)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			if (beginKeyInclusive.TryGetSpan(out var beginKeySpan))
			{
				ClearRangeContinuation(trans, beginKeySpan, in endKeyExclusive);
			}
			else
			{
				using var beginKeyBytes = FdbKeyHelpers.Encode(in beginKeyInclusive, ArrayPool<byte>.Shared);
				ClearRangeContinuation(trans, beginKeyBytes.Span, in endKeyExclusive);
			}

			static void ClearRangeContinuation(IFdbTransaction trans, ReadOnlySpan<byte> beginKeyInclusive, in TEndKey endKeyExclusive)
			{
				if (endKeyExclusive.TryGetSpan(out var endKeySpan))
				{
					trans.ClearRange(beginKeyInclusive, endKeySpan);
				}
				else
				{
					using var endKeyBytes = FdbKeyHelpers.Encode(in endKeyExclusive, ArrayPool<byte>.Shared);
					trans.ClearRange(beginKeyInclusive, endKeyBytes.Span);
				}
			}
		}

		#endregion

		#region Conflict Ranges...

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKeyInclusive">Key specifying the beginning of the conflict range. The key is included</param>
		/// <param name="endKeyExclusive">Key specifying the end of the conflict range. The key is excluded</param>
		/// <param name="type">One of the <see cref="FdbConflictRangeType"/> values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			trans.AddConflictRange(ToSpanKey(beginKeyInclusive), ToSpanKey(endKeyExclusive), type);
		}

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange(this IFdbTransaction trans, KeyRange range, FdbConflictRangeType type)
		{
			trans.AddConflictRange(ToSpanKey(range.Begin), range.End.HasValue ? ToSpanKey(range.End) : FdbKey.MaxValueSpan, type);
		}

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange<TKeyRange>(this IFdbTransaction trans, in TKeyRange range, FdbConflictRangeType type)
			where TKeyRange : struct, IFdbKeyRange
		{
			trans.AddConflictRange(range.ToKeyRange(), type);
		}

		/// <summary>
		/// Adds a key to the transaction's read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddConflictRange<TBeginKey>(this IFdbTransaction trans, in TBeginKey beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
			where TBeginKey : struct, IFdbKey
		{
			if (beginKeyInclusive.TryGetSpan(out var beginSpan))
			{
				trans.AddConflictRange(beginSpan, endKeyExclusive, type);
			}
			else
			{
				using var beginBytes = FdbKeyHelpers.Encode(in beginKeyInclusive, ArrayPool<byte>.Shared);
				trans.AddConflictRange(beginBytes.Span, endKeyExclusive, type);
			}
		}

		/// <summary>
		/// Adds a key to the transaction's read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddConflictRange<TEndKey>(this IFdbTransaction trans, ReadOnlySpan<byte> beginKeyInclusive, in TEndKey endKeyExclusive, FdbConflictRangeType type)
			where TEndKey : struct, IFdbKey
		{
			if (endKeyExclusive.TryGetSpan(out var endSpan))
			{
				trans.AddConflictRange(beginKeyInclusive, endSpan, type);
			}
			else
			{
				using var endBytes = FdbKeyHelpers.Encode(in endKeyExclusive, ArrayPool<byte>.Shared);
				trans.AddConflictRange(beginKeyInclusive, endBytes.Span, type);
			}
		}

		/// <summary>
		/// Adds a key to the transaction's read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddConflictRange<TBeginKey, TEndKey>(this IFdbTransaction trans, in TBeginKey beginKeyInclusive, in TEndKey endKeyExclusive, FdbConflictRangeType type)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			if (beginKeyInclusive.TryGetSpan(out var beginSpan))
			{
				trans.AddConflictRange(beginSpan, in endKeyExclusive, type);
			}
			else
			{
				using var beginBytes = FdbKeyHelpers.Encode(in beginKeyInclusive, ArrayPool<byte>.Shared);
				trans.AddConflictRange(beginBytes.Span, in endKeyExclusive, type);
			}
		}

		/// <inheritdoc cref="AddReadConflictRange(IFdbTransaction,KeyRange)"/>
		public static void AddReadConflictRange<TKeyRange>(this IFdbTransaction trans, in TKeyRange range)
			where TKeyRange : struct, IFdbKeyRange
		{
			trans.AddConflictRange(in range, FdbConflictRangeType.Read);
		}

		/// <summary>Adds a range of keys to the transaction's read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.</summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, KeyRange range)
		{
			trans.AddConflictRange(range, FdbConflictRangeType.Read);
		}

		/// <inheritdoc cref="AddReadConflictRange(IFdbTransaction,ReadOnlySpan{byte},ReadOnlySpan{byte})"/>
		public static void AddReadConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Read);
		}

		/// <inheritdoc cref="AddReadConflictRange(IFdbTransaction,ReadOnlySpan{byte},ReadOnlySpan{byte})"/>
		public static void AddReadConflictRange<TBeginKey, TEndKey>(this IFdbTransaction trans, in TBeginKey beginKeyInclusive, in TEndKey endKeyExclusive)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			trans.AddConflictRange(in beginKeyInclusive, in endKeyExclusive, FdbConflictRangeType.Read);
		}

		/// <summary>Adds a range of keys to the transaction's read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.</summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Read);
		}

		/// <summary>Adds a key to the transaction's read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.</summary>
		public static void AddReadConflictKey(this IFdbTransaction trans, Slice key)
		{
			trans.AddConflictRange(KeyRange.FromKey(key), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a key to the transaction's read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			trans.AddConflictRange(FdbKeyRange.Single(in key), FdbConflictRangeType.Read);
		}

		/// <inheritdoc cref="AddWriteConflictRange(IFdbTransaction,KeyRange)"/>
		public static void AddWriteConflictRange<TKeyRange>(this IFdbTransaction trans, in TKeyRange range)
			where TKeyRange : struct, IFdbKeyRange
		{
			trans.AddConflictRange(in range, FdbConflictRangeType.Write);
		}

		/// <summary>Adds a range of keys to the transaction's write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.</summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, KeyRange range)
		{
			trans.AddConflictRange(range, FdbConflictRangeType.Write);
		}

		/// <inheritdoc cref="AddWriteConflictRange(IFdbTransaction,ReadOnlySpan{byte},ReadOnlySpan{byte})"/>
		public static void AddWriteConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <inheritdoc cref="AddWriteConflictRange(IFdbTransaction,ReadOnlySpan{byte},ReadOnlySpan{byte})"/>
		public static void AddWriteConflictRange<TBeginKey, TEndKey>(this IFdbTransaction trans, in TBeginKey beginKeyInclusive, in TEndKey endKeyExclusive)
			where TBeginKey : struct, IFdbKey
			where TEndKey : struct, IFdbKey
		{
			trans.AddConflictRange(in beginKeyInclusive, in endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <summary>Adds a range of keys to the transaction's write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.</summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction's write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey(this IFdbTransaction trans, Slice key)
		{
			trans.AddConflictRange(KeyRange.FromKey(key), FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction's write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey<TKey>(this IFdbTransaction trans, in TKey key)
			where TKey : struct, IFdbKey
		{
			trans.AddConflictRange(FdbKeyRange.Single(in key), FdbConflictRangeType.Write);
		}

		#endregion

		#region Watch...

		/// <inheritdoc cref="IFdbTransaction.Watch"/>
		[Pure]
		public static FdbWatch Watch(this IFdbTransaction trans, Slice key, CancellationToken ct)
		{
			return trans.Watch(ToSpanKey(key), ct);
		}

		/// <inheritdoc cref="IFdbTransaction.Watch"/>
		[Pure]
		public static FdbWatch Watch<TKey>(this IFdbTransaction trans, in TKey key, CancellationToken ct)
			where TKey : struct, IFdbKey
		{
			// unfortunately, we have to allocate the encoded key since this is an async operation
			return trans.Watch(FdbKeyHelpers.ToSlice(in key), ct);
		}

		#endregion

		#region Batching...

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, Slice[] keys)
		{
			Contract.NotNull(keys);
			return trans.GetValuesAsync(keys.AsSpan());
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value will be Slice.Nil.</returns>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			return keys switch
			{
				Slice[] arr      => trans.GetValuesAsync(arr.AsSpan()),
				List<Slice> list => trans.GetValuesAsync(CollectionsMarshal.AsSpan(list)),
				_                => trans.GetValuesAsync(keys.ToArray()),
			};
		}

		public static Task<Slice[]> GetValuesAsync<TKey>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TKey> keys)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			// we cannot (as of .NET 10) use pass either a ReadOnlySpan<byte>[] nor a ReadOnlySpan<ReadOnlySpan<byte>> to the native handler
			// => we have to allocate the encoded keys into the heap, but at least we can use a SliceBuffer for this

			var pool = ArrayPool<byte>.Shared;
			var buffer = new SliceBuffer(0, pool: pool);
			Slice[]? encodedKeysBuffer = null;
			byte[]? tmp = null;
			int count = 0;
			try
			{
				encodedKeysBuffer = ArrayPool<Slice>.Shared.Rent(keys.Length);
				var encodedKeys = encodedKeysBuffer.AsSpan(0, keys.Length);
				for (int i = 0; i < keys.Length; i++)
				{
					if (!keys[i].TryGetSpan(out var span))
					{
						span = FdbKeyHelpers.Encode(in keys[i], ref tmp, pool);
					}
					encodedKeys[i] = buffer.Intern(span);
					++count;
				}

				return trans.GetValuesAsync(encodedKeys);
			}
			finally
			{
				if (encodedKeysBuffer is not null)
				{
					if (count > 0)
					{ // clear the keys we encoded, otherwise the GC could keep the Slice buffers alive!
						encodedKeysBuffer.AsSpan(0, count).Clear();
					}
					ArrayPool<Slice>.Shared.Return(encodedKeysBuffer);
				}
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				buffer.ReleaseMemoryUnsafe();
			}
		}

		public static Task<Slice[]> GetValuesAsync<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			if (keys.TryGetSpan(out var keysSpan))
			{
				return trans.GetValuesAsync(keysSpan);
			}

			// we cannot (as of .NET 10) use pass either a ReadOnlySpan<byte>[] nor a ReadOnlySpan<ReadOnlySpan<byte>> to the native handler
			// => we have to allocate the encoded keys into the heap, but at least we can use a SliceBuffer for this

			var pool = ArrayPool<byte>.Shared;
			var buffer = new SliceBuffer(0, pool: pool);
			PooledBuffer<Slice> encodedKeys = new(ArrayPool<Slice>.Shared, keys.TryGetNonEnumeratedCount(out int count) ? count : 0);
			byte[]? tmp = null;
			try
			{
				foreach(var key in keys)
				{
					if (!key.TryGetSpan(out var span))
					{
						span = FdbKeyHelpers.Encode(in key, ref tmp, pool);
					}
					encodedKeys.Add(buffer.Intern(span));
				}
				return trans.GetValuesAsync(encodedKeys.AsSpan());
			}
			finally
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				buffer.ReleaseMemoryUnsafe();
				encodedKeys.Dispose();
			}
		}

		public static Task<Slice[]> GetValuesAsync<TElement, TKey>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, TKey> keySelector)
			where TKey : struct, IFdbKey
			=> trans.GetValuesAsync(items, keySelector, static (fn, item) => fn(item));

		public static Task<Slice[]> GetValuesAsync<TElement, TKeyState, TKey>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, TKeyState state, Func<TKeyState, TElement, TKey> keySelector)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(keySelector);

			if (items.Length == 0)
			{
				return Task.FromResult(Array.Empty<Slice>());
			}

			//TODO: fast-path for one ? is this frequent enough to justify?

			// we cannot (as of .NET 10) use pass either a ReadOnlySpan<byte>[] nor a ReadOnlySpan<ReadOnlySpan<byte>> to the native handler
			// => we have to allocate the encoded keys into the heap, but at least we can use a SliceBuffer for this

			// Pool used by the SliceBuffer (to allocate pages of 4K bytes for the key), and for the temp encoding buffer used to encode each key
			var pool = ArrayPool<byte>.Shared;

			var buffer = new SliceBuffer(4096, pool: pool);
			Slice[]? encodedKeysBuffer = null;
			byte[]? tmp = null;
			int tmpMaxSize = 0;
			int count = 0;
			try
			{
				encodedKeysBuffer = ArrayPool<Slice>.Shared.Rent(items.Length);
				var encodedKeys = encodedKeysBuffer.AsSpan(0, items.Length);
				for (int i = 0; i < items.Length; i++)
				{
					var key = keySelector(state, items[i]);

					// if we know the size, we can directly encode in the SliceBuffer
					if (key.TryGetSizeHint(out var sizeHint))
					{
						var chunk = buffer.GetSpan(sizeHint);
						if (key.TryEncode(chunk, out var bytesWritten))
						{
							encodedKeys[i] = buffer.Advance(bytesWritten);
							++count;
							continue;
						}
						// there is small change that the size estimated was wrong (too small)
						// => fallback to slow encoding
					}

					// encode in a temp location, and copy over into the slice buffer
					var keyBytes = FdbKeyHelpers.Encode(in key, ref tmp, pool, sizeHint);
					tmpMaxSize = Math.Max(tmpMaxSize, keyBytes.Length);
					encodedKeys[i] = buffer.Intern(keyBytes);
					++count;
				}

				return trans.GetValuesAsync(encodedKeys);
			}
			finally
			{
				if (encodedKeysBuffer is not null)
				{
					if (count > 0)
					{ // clear the keys we encoded, otherwise the GC could keep the Slice buffers alive!
						encodedKeysBuffer.AsSpan(0, count).Clear();
					}
					ArrayPool<Slice>.Shared.Return(encodedKeysBuffer);
				}
				if (tmp is not null)
				{
					tmp.AsSpan(0, tmpMaxSize).Clear();
					pool.Return(tmp);
				}
				buffer.ReleaseMemoryUnsafe(clear: true);
			}
		}

		public static Task<Slice[]> GetValuesAsync<TElement, TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, Func<TElement, TKey> keySelector)
			where TKey : struct, IFdbKey
			=> trans.GetValuesAsync(items, keySelector, static (fn, item) => fn(item));

		public static Task<Slice[]> GetValuesAsync<TElement, TKeyState, TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, TKeyState state, Func<TKeyState, TElement, TKey> keySelector)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(items);
			Contract.NotNull(keySelector);

			if (items.TryGetSpan(out var itemsSpan))
			{
				return trans.GetValuesAsync(itemsSpan, state, keySelector);
			}

			// we cannot (as of .NET 10) use pass either a ReadOnlySpan<byte>[] nor a ReadOnlySpan<ReadOnlySpan<byte>> to the native handler
			// => we have to allocate the encoded keys into the heap, but at least we can use a SliceBuffer for this

			var pool = ArrayPool<byte>.Shared;
			var buffer = new SliceBuffer(0, pool: pool);
			PooledBuffer<Slice> encodedKeys = new(ArrayPool<Slice>.Shared, items.TryGetNonEnumeratedCount(out int count) ? count : 0);
			byte[]? tmp = null;
			try
			{
				foreach(var item in items)
				{
					var key = keySelector(state, item);
					if (!key.TryGetSpan(out var span))
					{
						span = FdbKeyHelpers.Encode(in key, ref tmp, pool);
					}
					encodedKeys.Add(buffer.Intern(span));
				}
				return trans.GetValuesAsync(encodedKeys.AsSpan());
			}
			finally
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				buffer.ReleaseMemoryUnsafe();
				encodedKeys.Dispose();
			}
		}

		public static Task<TResult[]> GetValuesAsync<TElement, TKey, TResult>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, TKey> keySelector, FdbValueDecoder<TResult> valueDecoder)
			where TKey : struct, IFdbKey
			=> trans.GetValuesAsync(items, keySelector, static (fn, item) => fn(item), valueDecoder);

		public static Task<TResult[]> GetValuesAsync<TElement, TKeyState, TKey, TResult>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, TKeyState state, Func<TKeyState, TElement, TKey> keySelector, FdbValueDecoder<TResult> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(keySelector);

			if (items.Length == 0)
			{
				return Task.FromResult(Array.Empty<TResult>());
			}

			//TODO: fast-path for one ? is this frequent enough to justify?

			// we cannot (as of .NET 10) use pass either a ReadOnlySpan<byte>[] nor a ReadOnlySpan<ReadOnlySpan<byte>> to the native handler
			// => we have to allocate the encoded keys into the heap, but at least we can use a SliceBuffer for this

			// Pool used by the SliceBuffer (to allocate pages of 4K bytes for the key), and for the temp encoding buffer used to encode each key
			var pool = ArrayPool<byte>.Shared;

			var buffer = new SliceBuffer(4096, pool: pool);
			Slice[]? encodedKeys = null;
			byte[]? tmp = null;
			int tmpMaxSize = 0;
			int count = 0;
			var results = new TResult[items.Length];
			try
			{
				encodedKeys = ArrayPool<Slice>.Shared.Rent(items.Length);
				for (int i = 0; i < items.Length; i++)
				{
					var key = keySelector(state, items[i]);

					// if we know the size, we can directly encode in the SliceBuffer
					if (key.TryGetSizeHint(out var sizeHint))
					{
						var chunk = buffer.GetSpan(sizeHint);
						if (key.TryEncode(chunk, out var bytesWritten))
						{
							encodedKeys[i] = buffer.Advance(bytesWritten);
							++count;
							continue;
						}
						// there is small change that the size estimated was wrong (too small)
						// => fallback to slow encoding
					}

					// encode in a temp location, and copy over into the slice buffer
					var keyBytes = FdbKeyHelpers.Encode(in key, ref tmp, pool, sizeHint);
					tmpMaxSize = Math.Max(tmpMaxSize, keyBytes.Length);
					encodedKeys[i] = buffer.Intern(keyBytes);
					++count;
				}
				Contract.Debug.Assert(count == items.Length);

				return Execute(trans, encodedKeys.AsMemory(0, count), results, valueDecoder);

				static async Task<TResult[]> Execute(IFdbReadOnlyTransaction trans, ReadOnlyMemory<Slice> keys, TResult[] results, FdbValueDecoder<TResult> valueDecoder)
				{
					await trans.GetValuesAsync(keys.Span, results.AsMemory(), valueDecoder).ConfigureAwait(false);
					return results;
				}
			}
			finally
			{
				if (encodedKeys is not null)
				{
					if (count > 0)
					{ // clear the keys we encoded, otherwise the GC could keep the Slice buffers alive!
						encodedKeys.AsSpan(0, count).Clear();
					}
					ArrayPool<Slice>.Shared.Return(encodedKeys);
				}
				if (tmp is not null)
				{
					tmp.AsSpan(0, tmpMaxSize).Clear();
					pool.Return(tmp);
				}
				buffer.ReleaseMemoryUnsafe(clear: true);
			}
		}

		public static Task<TResult[]> GetValuesAsync<TElement, TKey, TResult>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, Func<TElement, TKey> keySelector, FdbValueDecoder<TResult> valueDecoder)
			where TKey : struct, IFdbKey
			=> trans.GetValuesAsync(items, keySelector, static (fn, item) => fn(item), valueDecoder);

		public static Task<TResult[]> GetValuesAsync<TElement, TKeyState, TKey, TResult>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, TKeyState state, Func<TKeyState, TElement, TKey> keySelector, FdbValueDecoder<TResult> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(items);
			Contract.NotNull(keySelector);

			if (items.TryGetSpan(out var itemsSpan))
			{
				return trans.GetValuesAsync(itemsSpan, state, keySelector, valueDecoder);
			}

			// we cannot (as of .NET 10) pass either a ReadOnlySpan<byte>[] or ReadOnlySpan<ReadOnlySpan<byte>> to the native handler
			// => we have to allocate the encoded keys into the heap, but at least we can use a SliceBuffer for this

			var pool = ArrayPool<byte>.Shared;
			var buffer = new SliceBuffer(0, pool: pool);
			PooledBuffer<Slice> encodedKeys = new(ArrayPool<Slice>.Shared, items.TryGetNonEnumeratedCount(out int count) ? count : 0);
			byte[]? tmp = null;
			try
			{
				foreach(var item in items)
				{
					var key = keySelector(state, item);
					if (!key.TryGetSpan(out var span))
					{
						span = FdbKeyHelpers.Encode(in key, ref tmp, pool);
					}
					encodedKeys.Add(buffer.Intern(span));
				}

				return Execute(trans, encodedKeys.AsMemory(), valueDecoder);

				static async Task<TResult[]> Execute(IFdbReadOnlyTransaction trans, ReadOnlyMemory<Slice> keys, FdbValueDecoder<TResult> valueDecoder)
				{
					var results = new TResult[keys.Length];
					await trans.GetValuesAsync(keys.Span, results.AsMemory(), valueDecoder).ConfigureAwait(false);
					return results;
				}
			}
			finally
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				buffer.ReleaseMemoryUnsafe();
				encodedKeys.Dispose();
			}
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task GetValuesAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, Memory<TValue> results, FdbValueDecoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			if (!keys.TryGetSpan(out var span))
			{
				span = keys.ToArray();
			}
			return trans.GetValuesAsync<TValue>(span, results, decoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task GetValuesAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TKey> keys, Memory<TValue> results, FdbValueDecoder<TValue> decoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			var encodedKeys = new Slice[keys.Length]; //TODO: pooled?
			for (int i = 0; i < keys.Length; i++)
			{
				encodedKeys[i] = FdbKeyHelpers.ToSlice(in keys[i]); //TODO: pooled
			}
			return trans.GetValuesAsync<TValue>(encodedKeys.AsSpan(), results, decoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task GetValuesAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys, Memory<TValue> results, FdbValueDecoder<TValue> decoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			if (!keys.TryGetSpan(out var span))
			{
				span = keys.ToArray();
			}
			return trans.GetValuesAsync<TKey, TValue>(span, results, decoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="state">State forwarded to the decoder</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task GetValuesAsync<TKey, TValueState, TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TKey> keys, Memory<TValue> results, TValueState state, FdbValueDecoder<TValueState, TValue> decoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			var encodedKeys = new Slice[keys.Length]; //TODO: pooled?
			for (int i = 0; i < keys.Length; i++)
			{
				encodedKeys[i] = FdbKeyHelpers.ToSlice(in keys[i]); //TODO: pooled
			}
			return trans.GetValuesAsync(encodedKeys.AsSpan(), results, state, decoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="keys"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="keys"/>.</param>
		/// <param name="state">State forwarded to the decoder</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TKey, TValueState, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys, Memory<TValue> results, TValueState state, FdbValueDecoder<TValueState, TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			if (!keys.TryGetSpan(out var span))
			{
				span = keys.ToArray();
			}
			return trans.GetValuesAsync(span, results, state, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of elements to be looked up in the database</param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKey, TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, TKey> keySelector, Memory<TValue> results, FdbValueDecoder<TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			//TODO: pooled!
			var keys = new Slice[items.Length]; 
			for (int i = 0; i < items.Length; i++)
			{
				keys[i] = FdbKeyHelpers.ToSlice(keySelector(items[i])); //TODO: pooled
			}
			return trans.GetValuesAsync(keys.AsSpan(), results, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of keys to be looked up in the database</param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKey, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, Func<TElement, TKey> keySelector, Memory<TValue> results, FdbValueDecoder<TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			if (items.TryGetSpan(out var span))
			{
				return trans.GetValuesAsync(span, keySelector, results, valueDecoder);
			}

			//TODO: pooled!
			var keys = new List<Slice>(items.TryGetNonEnumeratedCount(out var count) ? count : 0);
			foreach (var item in items)
			{
				keys.Add(FdbKeyHelpers.ToSlice(keySelector(item))); //TODO: pooled
			}
			return trans.GetValuesAsync(CollectionsMarshal.AsSpan(keys), results, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of elements to be looked up in the database</param>
		/// <param name="keyState">Opaque state forwarded to <paramref name="keySelector"/></param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKeyState, TKey, TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, TKeyState keyState, Func<TKeyState, TElement, TKey> keySelector, Memory<TValue> results, FdbValueDecoder<TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			//TODO: pooled!
			var keys = new Slice[items.Length]; 
			for (int i = 0; i < items.Length; i++)
			{
				keys[i] = FdbKeyHelpers.ToSlice(keySelector(keyState, items[i])); //TODO: pooled
			}
			return trans.GetValuesAsync(keys.AsSpan(), results, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of keys to be looked up in the database</param>
		/// <param name="keyState">Opaque state forwarded to <paramref name="keySelector"/></param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKeyState, TKey, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, TKeyState keyState, Func<TKeyState, TElement, TKey> keySelector, Memory<TValue> results, FdbValueDecoder<TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			if (items.TryGetSpan(out var span))
			{
				return trans.GetValuesAsync(span, keyState, keySelector, results, valueDecoder);
			}

			//TODO: pooled!
			var keys = new List<Slice>(items.TryGetNonEnumeratedCount(out var count) ? count : 0);
			foreach (var item in items)
			{
				keys.Add(FdbKeyHelpers.ToSlice(keySelector(keyState, item))); //TODO: pooled
			}
			return trans.GetValuesAsync(CollectionsMarshal.AsSpan(keys), results, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of elements to be looked up in the database</param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueState">State forwarded to the decoder</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKey, TValueState, TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, Func<TElement, TKey> keySelector, Memory<TValue> results, TValueState valueState, FdbValueDecoder<TValueState, TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			//TODO: pooled!
			var keys = new Slice[items.Length]; 
			for (int i = 0; i < items.Length; i++)
			{
				keys[i] = FdbKeyHelpers.ToSlice(keySelector(items[i])); //TODO: pooled
			}
			return trans.GetValuesAsync(keys.AsSpan(), results, valueState, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of keys to be looked up in the database</param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueState">State forwarded to the decoder</param>
		/// <param name="valueDecoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="valueDecoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKey, TValueState, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, Func<TElement, TKey> keySelector, Memory<TValue> results, TValueState valueState, FdbValueDecoder<TValueState, TValue> valueDecoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(valueDecoder);

			if (items.TryGetSpan(out var span))
			{
				return trans.GetValuesAsync(span, keySelector, results, valueState, valueDecoder);
			}

			//TODO: pooled!
			var keys = new List<Slice>(items.TryGetNonEnumeratedCount(out var count) ? count : 0);
			foreach (var item in items)
			{
				keys.Add(FdbKeyHelpers.ToSlice(keySelector(item))); //TODO: pooled
			}
			return trans.GetValuesAsync(CollectionsMarshal.AsSpan(keys), results, valueState, valueDecoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of elements to be looked up in the database</param>
		/// <param name="keyState">Opaque state forwarded to <paramref name="keySelector"/></param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueState">Opaque state forwarded to <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKeyState, TKey, TValueState, TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<TElement> items, TKeyState keyState, Func<TKeyState, TElement, TKey> keySelector, Memory<TValue> results, TValueState valueState, FdbValueDecoder<TValueState, TValue> decoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			//TODO: pooled!
			var keys = new Slice[items.Length]; 
			for (int i = 0; i < items.Length; i++)
			{
				keys[i] = FdbKeyHelpers.ToSlice(keySelector(keyState, items[i])); //TODO: pooled
			}
			return trans.GetValuesAsync(keys.AsSpan(), results, valueState, decoder);
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="items">Sequence of keys to be looked up in the database</param>
		/// <param name="keyState">Opaque state forwarded to <paramref name="keySelector"/></param>
		/// <param name="keySelector">Function that generate the key for each element</param>
		/// <param name="results">Buffer where the results will be written to (must be at least as large as <paramref name="items"/>). Each entry will contain the decoded value of the key at the same index in <paramref name="items"/>.</param>
		/// <param name="valueState">State forwarded to <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="items"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task GetValuesAsync<TElement, TKeyState, TKey, TValueState, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TElement> items, TKeyState keyState, Func<TKeyState, TElement, TKey> keySelector, Memory<TValue> results, TValueState valueState, FdbValueDecoder<TValueState, TValue> decoder)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			if (items.TryGetSpan(out var span))
			{
				return trans.GetValuesAsync(span, keyState, keySelector, results, valueState, decoder);
			}

			//TODO: pooled!
			var keys = new List<Slice>(items.TryGetNonEnumeratedCount(out var count) ? count : 0);
			foreach (var item in items)
			{
				keys.Add(FdbKeyHelpers.ToSlice(keySelector(keyState, item))); //TODO: pooled
			}
			return trans.GetValuesAsync(CollectionsMarshal.AsSpan(keys), results, valueState, decoder);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static async Task<TValue[]> GetValuesAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			return decoder.DecodeValues(await trans.GetValuesAsync(keys).ConfigureAwait(false));
		}

		/// <inheritdoc cref="IFdbReadOnlyTransaction.GetKeyAsync(FoundationDB.Client.KeySelector)"/>
		public static Task<Slice> GetKeyAsync<TKey>(this IFdbReadOnlyTransaction trans, in FdbKeySelector<TKey> selector)
			where TKey : struct, IFdbKey
		{
			//TODO: PERF: optimize this!
			return trans.GetKeyAsync(selector.ToSelector());
		}

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransaction trans, KeySelector[] selectors)
		{
			Contract.NotNull(trans);
			Contract.NotNull(selectors);
			return trans.GetKeysAsync(selectors.AsSpan());
		}

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Sequence of key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelector> selectors)
		{
			Contract.NotNull(trans);
			Contract.NotNull(selectors);
			if (selectors.TryGetSpan(out var span))
			{
				return trans.GetKeysAsync(span);
			}
			else
			{
				return trans.GetKeysAsync(CollectionsMarshal.AsSpan(selectors.ToList()));
			}
		}

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Sequence of key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync<TKey>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<FdbKeySelector<TKey>> selectors)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);

			//TODO: pooled array? stackalloc?
			var buffer = new KeySelector[selectors.Length];
			for(int i = 0; i < selectors.Length; i++)
			{
				buffer[i] = selectors[i].ToSelector(); //PERF: Optimize!
			}
			return trans.GetKeysAsync(buffer);
		}

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Sequence of key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<FdbKeySelector<TKey>> selectors)
			where TKey : struct, IFdbKey
		{
			Contract.NotNull(trans);
			Contract.NotNull(selectors);
			if (selectors.TryGetSpan(out var span))
			{
				return trans.GetKeysAsync(span);
			}

			var buffer = new List<KeySelector>(selectors.TryGetNonEnumeratedCount(out var count) ? count : 0);
			foreach(var selector in selectors)
			{
				buffer.Add(selector.ToSelector()); //PERF: Optimize!
			}
			return trans.GetKeysAsync(CollectionsMarshal.AsSpan(buffer));
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of key/value pairs, or an exception. Each pair in the array will contain the key at the same index in <paramref name="keys"/>, and its corresponding value in the database or Slice.Nil if that key does not exist.</returns>
		/// <remarks>This method is equivalent to calling <see cref="IFdbReadOnlyTransaction.GetValuesAsync"/>, except that it will return the keys in addition to the values.</remarks>
		public static Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			var array = keys as Slice[] ?? keys.ToArray();

			return trans.GetBatchAsync(array);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of key/value pairs, or an exception. Each pair in the array will contain the key at the same index in <paramref name="keys"/>, and its corresponding value in the database or Slice.Nil if that key does not exist.</returns>
		/// <remarks>This method is equivalent to calling <see cref="IFdbReadOnlyTransaction.GetValuesAsync"/>, except that it will return the keys in addition to the values.</remarks>
		public static async Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync(this IFdbReadOnlyTransaction trans, Slice[] keys)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Debug.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, Slice>[results.Length];
			for (int i = 0; i < array.Length;i++)
			{
				array[i] = new(keys[i], results[i]);
			}
			return array;
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			var array = keys as Slice[] ?? keys.ToArray();

			return trans.GetBatchAsync(array, decoder);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static async Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice[] keys, IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);
			Contract.NotNull(decoder);

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Debug.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, TValue>[results.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new KeyValuePair<Slice, TValue>(keys[i], decoder.DecodeValue(results[i])!);
			}
			return array;
		}

		#endregion

		#region Queries...

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncQuery<T>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync((tr) =>
			{
				var query = handler(tr) ?? throw new InvalidOperationException("The query handler returned a null sequence");
				// ReSharper disable once MethodSupportsCancellation
				return query.ToListAsync();
			}, ct);
		}

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncQuery<T>>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync(async (tr) =>
			{
				var query = (await handler(tr).ConfigureAwait(false)) ?? throw new InvalidOperationException("The query handler returned a null sequence");
				// ReSharper disable once MethodSupportsCancellation
				return await query.ToListAsync().ConfigureAwait(false);
			}, ct);
		}

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncLinqQuery<T>>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync(async (tr) =>
			{
				var query = (await handler(tr).ConfigureAwait(false)) ?? throw new InvalidOperationException("The query handler returned a null sequence");
				// ReSharper disable once MethodSupportsCancellation
				return await query.ToListAsync().ConfigureAwait(false);
			}, ct);
		}

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync(async (tr) =>
			{
				var query = handler(tr) ?? throw new InvalidOperationException("The query handler returned a null sequence");
#if NET10_0_OR_GREATER
				return await query.ToListAsync(ct).ConfigureAwait(false);
#else
				return await query.ToAsyncQuery(ct).ToListAsync().ConfigureAwait(false);
#endif
			}, ct);
		}

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncEnumerable<T>>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync(async (tr) =>
			{
				var query = (await handler(tr).ConfigureAwait(false)) ?? throw new InvalidOperationException("The query handler returned a null sequence");
#if NET10_0_OR_GREATER
				return await query.ToListAsync(ct).ConfigureAwait(false);
#else
				return await query.ToAsyncQuery(ct).ToListAsync().ConfigureAwait(false);
#endif
			}, ct);
		}

		#endregion

	}
}
