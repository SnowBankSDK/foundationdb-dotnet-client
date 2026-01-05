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

namespace SnowBank.Networking.Http
{

	/// <summary>Type that can be used to change the behavior of an HTTP request during its execution</summary>
	public interface IBetterHttpFilter
	{

		/// <summary>Name of the filter (for logging/troubleshooting purpose)</summary>
		string Name { get; }

		/// <summary>Called when the request is being configured</summary>
		ValueTask Configure(BetterHttpClientContext context);

		/// <summary>Called when before the request will be sent</summary>
		/// <remarks>This is where the filter can customize the request to add/modify headers or intercept the request stream.</remarks>
		ValueTask PrepareRequest(BetterHttpClientContext context);

		/// <summary>Called when the request has been sent</summary>
		/// <remarks>This is where the filter can release early any resources allocated for preparing the request.</remarks>
		ValueTask CompleteRequest(BetterHttpClientContext context);

		/// <summary>Called when the response has been received, but not yet processed</summary>
		/// <remarks>This is where the filter can customize how the response will be processed, or intercept the response stream.</remarks>
		ValueTask PrepareResponse(BetterHttpClientContext context);

		/// <summary>Called when the response has been processed</summary>
		/// <remarks>This is where the filter can release early any resources allocated for processing the response.</remarks>
		ValueTask CompleteResponse(BetterHttpClientContext context);

		/// <summary>Called when the operation has been completed</summary>
		/// <remarks>This is where the filter can release any resources allocated during the operation</remarks>
		ValueTask Finalize(BetterHttpClientContext context);

	}

}
