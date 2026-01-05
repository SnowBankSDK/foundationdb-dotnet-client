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

namespace FoundationDB.Types.ProtocolBuffers
{

	public class ProtobufCodec<TDocument> : IValueEncoder<TDocument>
	{

		public ProtobufCodec()
		{
			ProtoBuf.Serializer.PrepareSerializer<TDocument>();
		}

		public Slice EncodeValue(TDocument? document)
		{
			using (var ms = new MemoryStream())
			{
				ProtoBuf.Serializer.Serialize<TDocument?>(ms, document);

				// Overflow protection (should never happen since a MemoryStream won't let us write more than 2G, but just to be sure ...)
				if (ms.Length > int.MaxValue) throw new OutOfMemoryException("The serialized JSON document exceeds the maximum allowed size");

				// Reuse the stream's internal buffer to reduce the need for allocations!
				var tmp = ms.GetBuffer();
				int size = checked((int)ms.Length);
				Debug.Assert(tmp != null && size >= 0 && size <= tmp.Length);

				return tmp.AsSlice(0, size);
			}
		}

		public TDocument? DecodeValue(Slice encoded)
		{
			if (encoded.IsNullOrEmpty) return default(TDocument);

			using (var sr = encoded.ToStream())
			{
				return ProtoBuf.Serializer.Deserialize<TDocument>(sr);
			}
		}

	}

}
