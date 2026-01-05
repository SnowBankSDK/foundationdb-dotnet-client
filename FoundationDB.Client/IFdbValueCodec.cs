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
	using System.Buffers;

	using SnowBank.Runtime;

	public interface IFdbValueCodec<TValue, out TEncoded>
		where TEncoded : struct, ISpanEncodable
	{

		TEncoded EncodeValue(in TValue value);

		TValue? DecodeValue(ReadOnlySpan<byte> encoded);

	}

	public sealed class FdbVarTupleValueCodec : IFdbValueCodec<IVarTuple, FdbVarTupleValue>
	{

		public static readonly FdbVarTupleValueCodec Instance = new();

		private FdbVarTupleValueCodec() { }

		/// <inheritdoc />
		public FdbVarTupleValue EncodeValue(in IVarTuple value) => new(value);

		/// <inheritdoc />
		public IVarTuple DecodeValue(ReadOnlySpan<byte> encoded) => TuPack.Unpack(encoded).ToTuple();

	}

	/// <summary>Encodes a <typeparamref name="T"/> value using the Tuple Encoding</summary>
	public sealed class FdbTupleValueCodec<T> : IFdbValueCodec<T, STuple<T>>
	{

		public static readonly FdbTupleValueCodec<T> Instance = new();

		private FdbTupleValueCodec() { }

		/// <inheritdoc />
		public STuple<T> EncodeValue(in T value) => STuple.Create<T>(value);

		/// <inheritdoc />
		public T? DecodeValue(ReadOnlySpan<byte> encoded) => TuPack.DecodeKey<T>(encoded);

	}

	public sealed class FdbUtf8ValueCodec : IFdbValueCodec<string, FdbUtf8Value>
	{

		public static readonly FdbUtf8ValueCodec Instance = new();

		private FdbUtf8ValueCodec() { }

		/// <inheritdoc />
		public FdbUtf8Value EncodeValue(in string value) => new(value);

		/// <inheritdoc />
		public string DecodeValue(ReadOnlySpan<byte> encoded)
			=> SpanEncoders.Utf8Encoder.TryDecode(encoded, out string? value)
				? value!
				: throw new FormatException("Failed to decoded string literal");

	}

	public sealed class FdbUtf16ValueCodec : IFdbValueCodec<string, FdbUtf16Value>
	{

		public static readonly FdbUtf16ValueCodec Instance = new();

		private FdbUtf16ValueCodec() { }

		/// <inheritdoc />
		public FdbUtf16Value EncodeValue(in string value) => new(value);

		/// <inheritdoc />
		public string DecodeValue(ReadOnlySpan<byte> encoded)
			=> SpanEncoders.Utf16Encoder.TryDecode(encoded, out string? value)
				? value!
				: throw new FormatException("Failed to decoded string literal");

	}

	public sealed class FdbValueSpanEncoderCodec<TValue, TCodec> : IFdbValueCodec<TValue, FdbValue<TValue, TCodec>>
		where TCodec : struct, ISpanEncoder<TValue>, ISpanDecoder<TValue>
	{

		public static readonly FdbValueSpanEncoderCodec<TValue, TCodec> Instance = new();

		private FdbValueSpanEncoderCodec() { }

		/// <inheritdoc />
		public FdbValue<TValue, TCodec> EncodeValue(in TValue value) => new(value);

		/// <inheritdoc />
		public TValue DecodeValue(ReadOnlySpan<byte> encoded)
			=> TCodec.TryDecode(encoded, out var value)
				? value!
				: throw new FormatException($"Failed to decode value of type '{typeof(TValue).GetFriendlyName()}'");
	}

	public static class FdbValueCodec
	{

		public static FdbRawValueCodec Raw => FdbRawValueCodec.Instance;

		public static class Tuples
		{

			public static FdbVarTupleValueCodec Dynamic => FdbVarTupleValueCodec.Instance;

			public static FdbTupleValueCodec<T> ForKey<T>() => FdbTupleValueCodec<T>.Instance;

		}

		public static FdbUtf8ValueCodec Utf8 => FdbUtf8ValueCodec.Instance;

		public static FdbUtf16ValueCodec Utf16 => FdbUtf16ValueCodec.Instance;

		public static class FixedSize
		{

			#region Little Endian...

			public static FdbValueSpanEncoderCodec<int, SpanEncoders.FixedSizeLittleEndianEncoder> Int32LittleEndian => FdbValueSpanEncoderCodec<int, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<long, SpanEncoders.FixedSizeLittleEndianEncoder> Int64LittleEndian => FdbValueSpanEncoderCodec<long, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<Int128, SpanEncoders.FixedSizeLittleEndianEncoder> Int128LittleEndian => FdbValueSpanEncoderCodec<Int128, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<uint, SpanEncoders.FixedSizeLittleEndianEncoder> UInt32LittleEndian => FdbValueSpanEncoderCodec<uint, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> UInt64LittleEndian => FdbValueSpanEncoderCodec<ulong, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<UInt128, SpanEncoders.FixedSizeLittleEndianEncoder> UInt128LittleEndian => FdbValueSpanEncoderCodec<UInt128, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<float, SpanEncoders.FixedSizeLittleEndianEncoder> SingleLittleEndian => FdbValueSpanEncoderCodec<float, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<double, SpanEncoders.FixedSizeLittleEndianEncoder> DoubleLittleEndian => FdbValueSpanEncoderCodec<double, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<Half, SpanEncoders.FixedSizeLittleEndianEncoder> HalfLittleEndian => FdbValueSpanEncoderCodec<Half, SpanEncoders.FixedSizeLittleEndianEncoder>.Instance;

			#endregion

			#region Big Endian...

			public static FdbValueSpanEncoderCodec<int, SpanEncoders.FixedSizeBigEndianEncoder> Int32BigEndian => FdbValueSpanEncoderCodec<int, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<long, SpanEncoders.FixedSizeBigEndianEncoder> Int64BigEndian => FdbValueSpanEncoderCodec<long, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<Int128, SpanEncoders.FixedSizeBigEndianEncoder> Int128BigEndian => FdbValueSpanEncoderCodec<Int128, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<uint, SpanEncoders.FixedSizeBigEndianEncoder> UInt32BigEndian => FdbValueSpanEncoderCodec<uint, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<ulong, SpanEncoders.FixedSizeBigEndianEncoder> UInt64BigEndian => FdbValueSpanEncoderCodec<ulong, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<UInt128, SpanEncoders.FixedSizeBigEndianEncoder> UInt128BigEndian => FdbValueSpanEncoderCodec<UInt128, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<float, SpanEncoders.FixedSizeBigEndianEncoder> SingleBigEndian => FdbValueSpanEncoderCodec<float, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<double, SpanEncoders.FixedSizeBigEndianEncoder> DoubleBigEndian => FdbValueSpanEncoderCodec<double, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			public static FdbValueSpanEncoderCodec<Half, SpanEncoders.FixedSizeBigEndianEncoder> HalfBigEndian => FdbValueSpanEncoderCodec<Half, SpanEncoders.FixedSizeBigEndianEncoder>.Instance;

			#endregion

		}

		//TODO: Compact!

	}

	public sealed class FdbRawValueCodec : IFdbValueCodec<Slice, FdbRawValue>, IFdbValueCodec<ReadOnlyMemory<byte>, FdbRawMemoryValue>
	{

		public static readonly FdbRawValueCodec Instance = new();

		private FdbRawValueCodec() { }

		/// <inheritdoc />
		public FdbRawValue EncodeValue(in Slice value) => new(value);

		/// <inheritdoc />
		public Slice DecodeValue(ReadOnlySpan<byte> encoded) => Slice.FromBytes(encoded);

		/// <inheritdoc />
		public FdbRawMemoryValue EncodeValue(in ReadOnlyMemory<byte> value) => new(value);

		/// <inheritdoc />
		ReadOnlyMemory<byte> IFdbValueCodec<ReadOnlyMemory<byte>, FdbRawMemoryValue>.DecodeValue(ReadOnlySpan<byte> encoded) => Slice.FromBytes(encoded).Memory;

	}

	public sealed class FdbValueCodec<TValue, TEncoded> : IFdbValueCodec<TValue, TEncoded>
		where TEncoded : struct, ISpanEncodable
	{

		public Func<TValue, TEncoded> Encode { get; }

#if NET9_0_OR_GREATER

		public Func<ReadOnlySpan<byte>, TValue> Decode { get; }

		public FdbValueCodec(Func<TValue, TEncoded> encode, Func<ReadOnlySpan<byte>, TValue> decode)
		{
			this.Encode = encode;
			this.Decode = decode;
		}

#else

		public SpanDecoder Decode { get; }

		public delegate TValue SpanDecoder(ReadOnlySpan<byte> encoded);

		public FdbValueCodec(Func<TValue, TEncoded> encode, SpanDecoder decode)
		{
			this.Encode = encode;
			this.Decode = decode;
		}

#endif

		/// <inheritdoc />
		public TEncoded EncodeValue(in TValue value) => this.Encode(value);

		/// <inheritdoc />
		public TValue DecodeValue(ReadOnlySpan<byte> encoded) => this.Decode(encoded);

	}

	public sealed class FdbValueEncoderCodec<TValue> : IFdbValueCodec<TValue, FdbRawValue>
	{

		public FdbValueEncoderCodec(IValueEncoder<TValue> encoder, FdbValueTypeHint typeHint = FdbValueTypeHint.None)
		{
			this.Encoder = encoder;
			this.TypeHint = typeHint;
		}

		public IValueEncoder<TValue> Encoder { get; }

		public FdbValueTypeHint TypeHint { get; }

		/// <inheritdoc />
		public FdbRawValue EncodeValue(in TValue value)
		{
			return new(this.Encoder.EncodeValue(value), this.TypeHint);
		}

		/// <inheritdoc />
		public TValue DecodeValue(ReadOnlySpan<byte> encoded)
		{
			using var owner = Slice.FromBytes(encoded, ArrayPool<byte>.Shared);
			return this.Encoder.DecodeValue(owner.Data)!;
		}

	}

}
