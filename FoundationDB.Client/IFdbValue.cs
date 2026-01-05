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

	/// <summary>Value that can be encoded into bytes</summary>
	public interface IFdbValue : ISpanEncodable, ISpanFormattable
		, IEquatable<FdbRawValue>
		, IEquatable<IFdbValue>
		, IEquatable<Slice>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		/// <summary>Returns a hint on how to interpret this value</summary>
		[Pure]
		FdbValueTypeHint GetTypeHint();

#if !NET9_0_OR_GREATER

		/// <inheritdoc cref="IEquatable{T}.Equals(T?)"/>
		[Pure]
		bool Equals(ReadOnlySpan<byte> other);

#endif

	}

	/// <summary>Represents the possible type for a value in the database</summary>
	public enum FdbValueTypeHint
	{
		/// <summary>The type is unknown.</summary>
		None = 0,

		/// <summary>The value is raw binary that should be displayed as a raw hexadecimal dump</summary>
		/// <remarks>This type can be used for files, binary blobs, or encoded/encrypted data.</remarks>
		Binary = 1,

		/// <summary>The value is encoded using the Tuple format</summary>
		/// <remarks>See <see cref="TuPack.Unpack(System.Slice)"/></remarks>
		Tuple,

		/// <summary>The value is a 7-bit ASCII encoded string.</summary>
		Ascii,

		/// <summary>The value is a UTF-8 encoded string.</summary>
		/// <remarks>The value <i>may</i> include a UTF-8 BOM.</remarks>
		Utf8,

		/// <summary>The value is a UTF-16 encoded string.</summary>
		/// <remarks>The value <i>may</i> include a UTF-16 BOM.</remarks>
		Utf16,

		/// <summary>The value is a little-endian integer value, that is best displayed in base-10.</summary>
		IntegerLittleEndian,

		/// <summary>The value is a big-endian integer value, that is best displayed in base-10.</summary>
		IntegerBigEndian,

		/// <summary>The value is a little-endian integer value, that is best displayed in base-16.</summary>
		IntegerHexLittleEndian,

		/// <summary>The value is a big-endian integer value, that is best displayed in base-16.</summary>
		IntegerHexBigEndian,

		/// <summary>The value is an integer that represents the number of elapsed milliseconds since the Unix epoch.</summary>
		UnixTimeMillis,

		/// <summary>The value is a 32-bit IEEE floating point number.</summary>
		Single,

		/// <summary>The value is a 64-bit IEEE floating point number.</summary>
		Double,

		/// <summary>The value is a 128-bit <see cref="Uuid128"/></summary>
		Uuid128,

		/// <summary>The value is a 96-bit <see cref="Uuid96"/></summary>
		Uuid96,
		
		/// <summary>The value is an 80-bit <see cref="Uuid80"/></summary>
		Uuid80,
		
		/// <summary>The value is a 64-bit <see cref="Uuid64"/></summary>
		Uuid64,
		
		/// <summary>The value is a 48-bit <see cref="Uuid48"/></summary>
		Uuid48,

		/// <summary>The value is <see cref="VersionStamp"/></summary>
		VersionStamp,

		/// <summary>The value is a JSON Document (UTF-8)</summary>
		Json,

	}

}
