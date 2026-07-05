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

#pragma warning disable IL2091

namespace SnowBank.Data.Tuples
{
	using System.Buffers;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Buffers;

	/// <summary>Tuple Binary Encoding</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class TuPack
	{

		#region Packing...

		// Without prefix

		/// <summary>Pack a tuple into a slice</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<TTuple>(Span<byte> destination, out int bytesWritten, in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<TTuple>(ref TupleSpanWriter writer, in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.TryWriteTo(ref writer, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack(IVarTuple? tuple)
		{
			return TupleEncoder.Pack(tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(in tuple);
		}

		/// <summary>Packs a tuple into a <see cref="SliceOwner"/></summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <param name="pool">Pool used to allocate the buffers</param>
		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner Pack<TTuple>(in TTuple? tuple, ArrayPool<byte>? pool)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(in tuple, pool ?? ArrayPool<byte>.Shared);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(in ValueTuple<T1> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1>(Span<byte> destination, out int bytesWritten, in ValueTuple<T1> tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(in (T1, T2) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2>(Span<byte> destination, out int bytesWritten, in (T1, T2) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(in (T1, T2, T3) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(in (T1, T2, T3, T4) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3, T4>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3, T4) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(in (T1, T2, T3, T4, T5) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3, T4, T5>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3, T4, T5) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(in (T1, T2, T3, T4, T5, T6) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3, T4, T5, T6) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(in (T1, T2, T3, T4, T5, T6, T7) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3, T4, T5, T6, T7) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(in (T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in (T1, T2, T3, T4, T5, T6, T7, T8, T9) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Packs a tuple into a destination buffer</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <remarks><c>true</c> if the operation was successful and the buffer large enough, or <c>false</c> if it was too small.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Span<byte> destination, out int bytesWritten, in (T1, T2, T3, T4, T5, T6, T7, T8, T9) tuple)
		{
			return TupleEncoder.TryPackTo(destination, out bytesWritten, default, in tuple);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(params IVarTuple[] tuples)
		{
			return TupleEncoder.Pack(default, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples(params ReadOnlySpan<IVarTuple> tuples)
#else
		public static Slice[] PackTuples(ReadOnlySpan<IVarTuple> tuples)
#endif
		{
			return TupleEncoder.Pack(default, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TTuple>(params TTuple[] tuples)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack<TTuple>(default, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples<TTuple>(params ReadOnlySpan<TTuple> tuples)
#else
		public static Slice[] PackTuples<TTuple>(ReadOnlySpan<TTuple> tuples)
#endif
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack<TTuple>(default, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples([InstantHandle] this IEnumerable<IVarTuple> tuples)
		{
			return TupleEncoder.Pack(default, tuples);
		}

		/// <summary>Efficiently write the packed representation of a tuple</summary>
		/// <param name="writer">Output buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PackTo<TTuple>(ref SliceWriter writer, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			var tw = new TupleWriter(ref writer);
			TupleEncoder.WriteTo(tw, tuple);
#if !NET7_0_OR_GREATER
			writer = tw.Output; // the TupleWriter wraps a boxed COPY on this target: propagate the writes back to the caller's writer
#endif
		}

		/// <summary>Efficiently write the packed representation of a tuple</summary>
		/// <param name="writer">Output buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PackTo<TTuple>(TupleWriter writer, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			TupleEncoder.WriteTo(writer, tuple);
		}

		// With prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a tuple</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(Slice prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix.Span, tuple);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a tuple</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(ReadOnlySpan<byte> prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix, tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(Slice prefix, STuple<T1> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, STuple<T1> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(Slice prefix, STuple<T1, T2> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, STuple<T1, T2> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(Slice prefix, STuple<T1, T2, T3> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(Slice prefix, STuple<T1, T2, T3, T4> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(Slice prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6, T7, T8> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6, T7, T8> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(Slice prefix, params IVarTuple[] tuples)
		{
			return TupleEncoder.Pack(prefix.Span, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, params IVarTuple[] tuples)
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples(Slice prefix, params ReadOnlySpan<IVarTuple> tuples)
#else
		public static Slice[] PackTuples(Slice prefix, ReadOnlySpan<IVarTuple> tuples)
#endif
		{
			return TupleEncoder.Pack(prefix.Span, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, params ReadOnlySpan<IVarTuple> tuples)
#else
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, ReadOnlySpan<IVarTuple> tuples)
#endif
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(Slice prefix, IEnumerable<IVarTuple> tuples)
		{
			return TupleEncoder.Pack(prefix.Span, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, IEnumerable<IVarTuple> tuples)
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(Slice prefix, TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix.Span, elements, transform);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(ReadOnlySpan<byte> prefix, TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix, elements, transform);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(Slice prefix, IEnumerable<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix.Span, elements, transform);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(ReadOnlySpan<byte> prefix, IEnumerable<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix, elements, transform);
		}

		#endregion

		#region Encode

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1>(T1 item1)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1);
		}

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1>(Span<byte> destination, out int bytesWritten, T1 item1)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1);
		}

		/// <summary>Pack a 2-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2>(T1 item1, T2 item2)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2);
		}

		/// <summary>Pack a 2-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2);
		}

		/// <summary>Pack a 3-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3);
		}

		/// <summary>Pack a 3-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3);
		}

		/// <summary>Pack a 4-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4);
		}

		/// <summary>Pack a 4-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3, item4);
		}

		/// <summary>Pack a 5-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5);
		}

		/// <summary>Pack a 5-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3, item4, item5);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Pack a 7-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6, item7);
		}

		/// <summary>Pack a 7-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6, T7>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3, item4, item5, item6, item7);
		}

		/// <summary>Pack an 8-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6, item7, item8);
		}

		/// <summary>Pack an 8-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3, item4, item5, item6, item7, item8);
		}

		/// <summary>Pack an 9-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6, item7, item8, item9);
		}

		/// <summary>Pack a 9-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Span<byte> destination, out int bytesWritten, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9)
		{
			return TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, item1, item2, item3, item4, item5, item6, item7, item8, item9);
		}

		#endregion

		#region Unpacking...

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static IVarTuple Unpack(Slice packedKey) //REVIEW: consider changing return type to SlicedTuple ?
		{
			return SlicedTuple.Unpack(packedKey);
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <param name="tuple">Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></param>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static bool TryUnpack(Slice packedKey, [MaybeNullWhen(false)] out IVarTuple tuple)
		{
			if (!packedKey.IsNull && TryUnpack(packedKey.Span, out var st))
			{
				tuple = st.ToTuple(packedKey);
				return true;
			}
			tuple = null;
			return false;
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <param name="tuple">Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></param>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static bool TryUnpack(ReadOnlySpan<byte> packedKey, out SpanTuple tuple)
		{
			if (packedKey.Length == 0)
			{
				tuple = SpanTuple.Empty;
				return true;
			}

			var reader = new TupleReader(packedKey);
			return TuplePackers.TryUnpack(ref reader, out tuple, out _);
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static SpanTuple Unpack(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) return SpanTuple.Empty;

			var reader = new TupleReader(packedKey);
			return TuplePackers.Unpack(ref reader);
		}

		/// <summary>Unpack a tuple from a binary representation</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple, or Slice.Nil</param>
		/// <returns>Unpacked tuple, the empty tuple if <paramref name="packedKey"/> is equal to <see cref="Slice.Empty"/>, or null if the key is <see cref="Slice.Nil"/></returns>
		[Pure]
		public static IVarTuple? UnpackOrDefault(Slice packedKey)
		{
			if (packedKey.IsNull) return null;
			if (packedKey.Count == 0) return STuple.Empty;
			return TuplePackers.Unpack(packedKey, embedded: false);
		}

		#region DecodeAt...


		/// <summary>Unpack the value of the element at the given position in a tuple</summary>
		/// <typeparam name="T1">Type of the element to decode</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple</param>
		/// <param name="index">Index of the item to decode, from the start of the tuple</param>
		/// <returns>Decoded value of the item at this position in the tuple. Throws an exception if the tuple is empty, or does not have enough elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeKeyAt<T1>(Slice packedKey, int index) => DecodeKeyAt<T1>(packedKey.Span, index);

		/// <summary>Unpack the value of the element at the given position in a tuple</summary>
		/// <typeparam name="T1">Type of the element to decode</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple</param>
		/// <param name="index">Index of the item to decode, from the start of the tuple</param>
		/// <param name="item1">Receives the decoded value, if successful</param>
		/// <returns><c>true</c> if the packed key was successfully unpacked.</returns>
		public static bool TryDecodeKeyAt<T1>(Slice packedKey, int index, out T1? item1) => TryDecodeKeyAt(packedKey.Span, index, out item1);

		/// <summary>Unpack the value of the element at the given position in a tuple</summary>
		/// <typeparam name="T1">Type of the element to decode</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple</param>
		/// <param name="index">Index of the item to decode, from the start of the tuple</param>
		/// <returns>Decoded value of the item at this position in the tuple. Throws an exception if the tuple is empty, or does not have enough elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeKeyAt<T1>(ReadOnlySpan<byte> packedKey, int index)
		{
			Exception? error;

			var reader = new TupleReader(packedKey);

			// skip previous items
			for (int i = 0; i < index; i++)
			{
				if (!TupleEncoder.TrySkipNext(ref reader, out error))
				{
					throw new FormatException($"Failed to skip item at offset {i}.", error); 
				}
			}

			// now decode the wanted item
			if (!TupleEncoder.TryDecodeNext(ref reader, out T1? item1, out error))
			{
				throw new FormatException($"Failed to decode item at offset {index}", error);
			}

			return item1;
		}

		/// <summary>Unpack the value of the element at the given position in a tuple</summary>
		/// <typeparam name="T1">Type of the element to decode</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple</param>
		/// <param name="index">Index of the item to decode, from the start of the tuple</param>
		/// <param name="item1">Receives the decoded value, if successful</param>
		/// <returns><c>true</c> if the packed key was successfully unpacked.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeKeyAt<T1>(ReadOnlySpan<byte> packedKey, int index, out T1? item1)
		{
			var reader = new TupleReader(packedKey);
			// skip previous items
			for (int i = 0; i < index; i++)
			{
				if (!TupleEncoder.TrySkipNext(ref reader, out _))
				{
					item1 = default;
					return false;
				}
			}

			// now decode the wanted item
			return TupleEncoder.TryDecodeKey(ref reader, out item1, out _);
		}


		#endregion

		#region DecodeFirst...

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 1 element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeFirst<T1>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 1 element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeFirst<T1>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its first 2 elements</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first two elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeFirst<T1, T2>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first 2 elements</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first two elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its first 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first three elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first three elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its first 4 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the fourth  value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first four elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeFirst<T1, T2, T3, T4>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3, T4>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first 4 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the fourth  value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first four elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3, T4>(packedKey, expectedSize);

		#endregion

		#region DecodeLast...

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeLast<T1>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeLast<T1>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the next to last value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last two elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the next to last value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last two elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last three elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last three elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last four elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3, T4>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last four elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3, T4>(packedKey, expectedSize);

		#endregion

		#region TryDecodeLast...

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(Slice packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(Slice packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(Slice packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1, out item2, out item3, out item4);

		#endregion

		#region TryDecodeLast...

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(Slice packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(Slice packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(Slice packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfully, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1, out item2, out item3, out item4);

		#endregion

		#region DecodeKey...

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty, or has more than one element.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeKey<T1>(Slice packedKey) => DecodeKey<T1>(packedKey.Span);

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty, or has more than one element.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeKey<T1>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out T1? item1);
			return item1;
		}

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Decoded value of the only item in the tuple.</param>
		/// <exception cref="FormatException"> if the tuple is empty, or has more than one element</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1>(Slice packedKey, out T1? item1) => DecodeKey(packedKey.Span, out item1);

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Decoded value of the only item in the tuple.</param>
		/// <exception cref="FormatException"> if the tuple is empty, or has more than one element</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1);
		}

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Decoded value of the only item in the tuple. Throws an exception if the tuple is empty, or has more than one element.</param>
		/// <returns><c>true</c> if the packed key was successfully unpacked.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeKey<T1>(Slice packedKey, out T1? item1) => TryDecodeKey<T1>(packedKey.Span, out item1);

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Decoded value of the only item in the tuple. Throws an exception if the tuple is empty, or has more than one element.</param>
		/// <returns><c>true</c> if the packed key was successfully unpacked.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeKey<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
		{
			var reader = new TupleReader(packedKey);
			return TupleEncoder.TryDecodeKey(ref reader, out item1, out _);
		}

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than two elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeKey<T1, T2>(Slice packedKey) => DecodeKey<T1, T2>(packedKey.Span);

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than two elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeKey<T1, T2>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2>(Slice packedKey, out T1? item1, out T2? item2)
			=> DecodeKey(packedKey.Span, out item1, out item2);

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2);
		}

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than three elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeKey<T1, T2, T3>(Slice packedKey) => DecodeKey<T1, T2, T3>(packedKey.Span);

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than three elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeKey<T1, T2, T3>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3);

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3);
		}

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than four elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeKey<T1, T2, T3, T4>(Slice packedKey) => DecodeKey<T1, T2, T3, T4>(packedKey.Span);

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than four elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3, out item4);
		}

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than five elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?) DecodeKey<T1, T2, T3, T4, T5>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5>(packedKey.Span);

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than five elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?) DecodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3, out item4, out item5);

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3, out item4, out item5);
		}

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than six elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6>(packedKey.Span);

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than six elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3, out item4, out item5, out item6);

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3, out item4, out item5, out item6);
		}

		/// <summary>Unpack a key containing seven elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with seven elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than seven elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<T1, T2, T3, T4, T5, T6, T7>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6, T7>(packedKey.Span);

		/// <summary>Unpack a key containing seven elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with seven elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than seven elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing seven elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with seven elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		/// <param name="item7">Decoded value of the 7th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3, out item4, out item5, out item6, out item7);

		/// <summary>Unpack a key containing seven elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with seven elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		/// <param name="item7">Decoded value of the 7th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3, out item4, out item5, out item6, out item7);
		}

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than eight elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(packedKey.Span);

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than eight elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		/// <param name="item7">Decoded value of the 7th element in the tuple.</param>
		/// <param name="item8">Decoded value of the 8th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3, out item4, out item5, out item6, out item7, out item8);

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		/// <param name="item7">Decoded value of the 7th element in the tuple.</param>
		/// <param name="item8">Decoded value of the 8th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3, out item4, out item5, out item6, out item7, out item8);
		}

		/// <summary>Unpack a key containing nine elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than eight elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(packedKey.Span);

		/// <summary>Unpack a key containing nine elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty, or has more than eight elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		/// <param name="item7">Decoded value of the 7th element in the tuple.</param>
		/// <param name="item8">Decoded value of the 8th element in the tuple.</param>
		/// <param name="item9">Decoded value of the 9th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8, out T9? item9)
			=> DecodeKey(packedKey.Span, out item1, out item2, out item3, out item4, out item5, out item6, out item7, out item8, out item9);

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <param name="item1">Decoded value of the 1st element in the tuple.</param>
		/// <param name="item2">Decoded value of the 2nd element in the tuple.</param>
		/// <param name="item3">Decoded value of the 3rd element in the tuple.</param>
		/// <param name="item4">Decoded value of the 4th element in the tuple.</param>
		/// <param name="item5">Decoded value of the 5th element in the tuple.</param>
		/// <param name="item6">Decoded value of the 6th element in the tuple.</param>
		/// <param name="item7">Decoded value of the 7th element in the tuple.</param>
		/// <param name="item8">Decoded value of the 8th element in the tuple.</param>
		/// <param name="item9">Decoded value of the 9th element in the tuple.</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8, out T9? item9)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out item1, out item2, out item3, out item4, out item5, out item6, out item7, out item8, out item9);
		}

		#endregion

		#endregion

		#region EncodePrefixedKey...

		//note: they are equivalent to the Pack<...>() methods, they only take a binary prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1>(Slice prefix, T1? value) => TupleEncoder.EncodeKey(prefix.Span, value);

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1>(ReadOnlySpan<byte> prefix, T1? value) => TupleEncoder.EncodeKey(prefix, value);

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 2-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2>(Slice prefix, T1? value1, T2? value2)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 2-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 3-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3>(Slice prefix, T1? value1, T2? value2, T3? value3)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 3-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 4-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 4-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 5-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 5-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 6-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5, value6);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 6-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5, value6);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 7-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5, value6, value7);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 7-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5, value6, value7);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of an 8-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of an 8-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		#endregion

	}

}
