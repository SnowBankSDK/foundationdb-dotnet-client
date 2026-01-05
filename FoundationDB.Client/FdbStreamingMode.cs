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

	/// <summary>Defines how the client would like the data in a range a returned</summary>
	[PublicAPI]
	public enum FdbStreamingMode
	{

		/// <summary>
		/// Client intends to consume the entire range and would like it all transferred as early as possible. 
		/// </summary>
		WantAll = -2,

		/// <summary>
		/// The default. The client doesn't know how much of the range it is likely to used and wants different performance concerns to be balanced. Only a small portion of data is transferred to the client initially (in order to minimize costs if the client doesn't read the entire range), and as the caller iterates over more items in the range larger batches will be transferred in order to minimize latency.
		/// </summary>
		Iterator = -1,

		/// <summary>
		/// Infrequently used. The client has passed a specific row limit and wants that many rows delivered in a single batch. Because of iterator operation in client drivers make request batches transparent to the user, consider WANT_ALL StreamingMode instead. A row limit must be specified if this mode is used.
		/// </summary>
		Exact = 0,

		/// <summary>
		/// Infrequently used. Transfer data in batches small enough to not be much more expensive than reading individual rows, to minimize cost if iteration stops early.
		/// </summary>
		Small = 1,

		/// <summary>
		/// Infrequently used. Transfer data in batches sized in between small and large. Usually the default
		/// </summary>
		Medium = 2,

		/// <summary>
		/// Infrequently used. Transfer data in batches large enough to be, in a high-concurrency environment, nearly as efficient as possible. If the client stops iteration early, some disk and network bandwidth may be wasted. The batch size may still be too small to allow a single client to get high throughput from the database, so if that is what you need consider the SERIAL StreamingMode.
		/// </summary>
		Large = 3,

		/// <summary>
		/// Transfer data in batches large enough that an individual client can get reasonable read bandwidth from the database. If the client stops iteration early, considerable disk and network bandwidth may be wasted.
		/// </summary>
		Serial = 4,

	}

	/// <summary>Defines if the range read will only return the keys, values or both.</summary>
	public enum FdbFetchMode
	{

		/// <summary>Read both keys and values (default)</summary>
		/// <remarks>When exposed as a <see cref="KeyValuePair{T,T}"/>, both <see cref="KeyValuePair{T,T}.Key"/> and <see cref="KeyValuePair{T,T}.Value"/> will be filled with the results</remarks>
		KeysAndValues = 0,
		
		/// <summary>Read only the keys. The values will be set to <see cref="Slice.Nil"/></summary>
		/// <remarks>When exposed as a <see cref="KeyValuePair{T,T}"/>, only <see cref="KeyValuePair{T,T}.Key"/> will be filled, and <see cref="KeyValuePair{T,T}.Value"/> will be empty</remarks>
		KeysOnly = 1,
		
		/// <summary>Read only the values. The keys will be set to <see cref="Slice.Nil"/></summary>
		/// <remarks>When exposed as a <see cref="KeyValuePair{T,T}"/>, only <see cref="KeyValuePair{T,T}.Value"/> will be filled, and <see cref="KeyValuePair{T,T}.Key"/> will be empty</remarks>
		ValuesOnly = 2,

	}

}
