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

namespace SnowBank.Data.Binary
{
	using SnowBank.Buffers;

	/// <summary>Class that know how to encode and decode values of a fixed type into a lower format</summary>
	/// <typeparam name="TValue">Type of the values</typeparam>
	/// <typeparam name="TStorage">Type of the encoded form of the values (Slice, string, ...)</typeparam>
	[PublicAPI]
	public interface IValueEncoder<TValue, TStorage>
	{

		/// <summary>Encodes a single value into a compact binary representation</summary>
		/// <param name="value">Value to encode</param>
		TStorage EncodeValue(TValue? value);

		/// <summary>Decodes a single value from a compact binary representation</summary>
		/// <param name="encoded">Packed value</param>
		TValue? DecodeValue(TStorage encoded);

	}

	/// <summary>Class that know how to encode and decode values of a fixed type</summary>
	/// <typeparam name="TValue">Type of the values</typeparam>
	[PublicAPI]
	public interface IValueEncoder<TValue> : IValueEncoder<TValue, Slice>
	{
		//TODO: add custom "EncodeValueTo(ref SliceWriter)" and "DecodeValueFrom(ref SliceReader)" ?

		/// <summary>Writes a value to the specified buffer</summary>
		void WriteValueTo(ref SliceWriter writer, TValue? value)
		//REVIEW: TODO: rename to "EncodeValueTo" ??
		{
			writer.WriteBytes(EncodeValue(value));
		}

	}

}
