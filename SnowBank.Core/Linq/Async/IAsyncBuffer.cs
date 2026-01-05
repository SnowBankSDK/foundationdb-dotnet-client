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

namespace SnowBank.Linq.Async
{

	/// <summary>Defines a producer/consumer buffer queue that can hold several items before blocking the producer</summary>
	/// <typeparam name="TInput">Type of elements entering the buffer</typeparam>
	/// <typeparam name="TOutput">Type of elements exiting the buffer. Can be different from <typeparamref name="TInput"/> if the buffer also transforms the elements.</typeparam>
	[PublicAPI]
	public interface IAsyncBuffer<in TInput, TOutput> : IAsyncTarget<TInput>, IAsyncSource<TOutput>
	{
		/// <summary>Returns the current number of items in the buffer</summary>
		int Count { get; }

		/// <summary>Returns the maximum capacity of the buffer</summary>
		int Capacity { get; }

		/// <summary>Returns true if the producer is blocked (queue is full)</summary>
		bool IsProducerBlocked { get; }

		/// <summary>Returns true if the consumer is blocked (queue is empty)</summary>
		bool IsConsumerBlocked { get; }

		/// <summary>Wait for all the consumers to drain the queue</summary>
		/// <returns>Task that completes when all consumers have drained the queue</returns>
		Task DrainAsync();
	}

}
