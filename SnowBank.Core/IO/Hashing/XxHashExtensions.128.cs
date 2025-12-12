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

namespace System.IO.Hashing
{
	using System.Buffers;
	using System.Text;

	/// <summary>Extension methods for computing XxHash values</summary>
	[PublicAPI]
	public static partial class XxHashExtensions
	{

		extension(XxHash128)
		{

			/// <summary>Computes the XxHash128 hash of the provided data</summary>
			/// <param name="bytes">The data to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static UInt128 HashToUInt128(Slice bytes, int seed = 0)
			{
				return XxHash128.HashToUInt128(bytes.Span, seed);
			}

			/// <summary>Computes the XxHash128 hash of the provided text</summary>
			/// <param name="text">The text to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <param name="encoding">Encoding used to convert the text to bytes (UTF-8 by default)</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static UInt128 HashToUInt128(string text, int seed = 0, Encoding? encoding = null)
			{
				return HashToUInt128(text.AsSpan(), seed, encoding);
			}

			/// <summary>Computes the XxHash128 hash of the provided text</summary>
			/// <param name="text">The text to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <param name="encoding">Encoding used to convert the text to bytes (UTF-8 by default)</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static UInt128 HashToUInt128(ReadOnlySpan<char> text, int seed = 0, Encoding? encoding = null)
			{
				encoding ??= Encoding.UTF8;
				int byteCount = encoding.GetByteCount(text);

				switch (byteCount)
				{
					case 0:
					{
						return XxHash128.HashToUInt128(default, seed);
					}
					case <= 256:
					{
						Span<byte> tmp = stackalloc byte[byteCount];
						byteCount = encoding.GetBytes(text, tmp);
						return XxHash128.HashToUInt128(tmp[..byteCount], seed);
					}
					default:
					{
						var pool = ArrayPool<byte>.Shared;
						byte[] buffer = pool.Rent(byteCount);
						try
						{
							byteCount = encoding.GetBytes(text, buffer);
							return XxHash128.HashToUInt128(buffer.AsSpan(0, byteCount), seed);
						}
						finally
						{
							pool.Return(buffer, clearArray: true);
						}
					}
				}
			}

			/// <summary>Computes the XxHash128 hash of the provided text</summary>
			/// <param name="text">The text to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <param name="encoding">Encoding used to convert the text to bytes (UTF-8 by default)</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static UInt128 HashToUInt128(ref DefaultInterpolatedStringHandler text, int seed = 0, Encoding? encoding = null)
			{
#if NET10_0_OR_GREATER
				var res = HashToUInt128(text.Text, seed, encoding);
				text.Clear();
				return res;
#else
				return HashToUInt128(text.ToStringAndClear(), seed, encoding);
#endif
			}

			/// <summary>Computes the XxHash128 hash of the provided text</summary>
			/// <param name="text">The text to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <param name="encoding">Encoding used to convert the text to bytes (UTF-8 by default)</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static Uuid128 HashToUuId128(string text, int seed = 0, Encoding? encoding = null)
				=> new Uuid128(HashToUInt128(text.AsSpan(), seed, encoding));

			/// <summary>Computes the XxHash128 hash of the provided text</summary>
			/// <param name="text">The text to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <param name="encoding">Encoding used to convert the text to bytes (UTF-8 by default)</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static Uuid128 HashToUid128(ReadOnlySpan<char> text, int seed = 0, Encoding? encoding = null)
				=> new(HashToUInt128(text, seed, encoding));

			/// <summary>Computes the XxHash128 hash of the provided text</summary>
			/// <param name="text">The text to hash</param>
			/// <param name="seed">The seed value for this hash computation. The default is zero.</param>
			/// <param name="encoding">Encoding used to convert the text to bytes (UTF-8 by default)</param>
			/// <returns>The computed XxHash128 hash.</returns>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static Uuid128 HashToUuId128(ref DefaultInterpolatedStringHandler text, int seed = 0, Encoding? encoding = null)
			{
#if NET10_0_OR_GREATER
				var res = new Uuid128(HashToUInt128(text.Text, seed, encoding));
				text.Clear();
				return res;
#else
				return new(HashToUInt128(text.ToStringAndClear(), seed, encoding));
#endif
			}

		}

	}

}
