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
	using System.Runtime.ExceptionServices;

	/// <summary>Defines a target that receive items and can throttle the producer</summary>
	/// <typeparam name="T">Type of values being accepted by the target</typeparam>
	[PublicAPI]
	public interface IAsyncTarget<in T>
	{
		//note: should OnCompleted and OnError be async or not ?
		//note2: should we just use a single method that pass a Maybe<T> ?

		/// <summary>Push a new item onto the target, if it can accept one</summary>
		/// <param name="value">New value that is being published</param>
		/// <param name="ct">Cancellation token that is used to abort the call if the target is blocked</param>
		/// <returns>Task that completes once the target has accepted the new value (or fails if the cancellation token fires)</returns>
		Task OnNextAsync(T value, CancellationToken ct);

		/// <summary>Notifies the target that the producer is done and that no more values will be published</summary>
		void OnCompleted();

		/// <summary>Notifies the target that tere was an exception, and that no more values will be published</summary>
		/// <param name="error">The error that occurred</param>
		void OnError(ExceptionDispatchInfo error);
	}

}
