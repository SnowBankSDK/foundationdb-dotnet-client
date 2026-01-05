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
	using System.Net.Sockets;

	/// <summary>Set of hooks that can be called during the execution of an HTTP request</summary>
	public interface IBetterHttpHooks
	{

		/// <summary>The stage in the processing pipeline has changed</summary>
		void OnStageChanged(BetterHttpClientContext context, BetterHttpClientStage stage);

		/// <summary>An error occurred during the execution of the query</summary>
		void OnError(BetterHttpClientContext context, Exception error);

		/// <summary>A filter failed during the execution of the query</summary>
		bool OnFilterError(BetterHttpClientContext context, Exception error);

		/// <summary>The query has been configured</summary>
		void OnConfigured(BetterHttpClientContext context);

		/// <summary>The request message has been prepared</summary>
		void OnRequestPrepared(BetterHttpClientContext context);

		/// <summary>The request message has been sent to the server</summary>
		void OnRequestCompleted(BetterHttpClientContext context);

		/// <summary>The response message has been received, but not yet processed</summary>
		void OnPrepareResponse(BetterHttpClientContext context);

		/// <summary>The response message has been processed</summary>
		void OnCompleteResponse(BetterHttpClientContext context);

		/// <summary>The execution of the query has completed</summary>
		void OnFinalizeQuery(BetterHttpClientContext context);

		/// <summary>A socket connection was established with the remote server</summary>
		void OnSocketConnected(BetterHttpClientContext context, Socket socket);

		/// <summary>A socket connection attempt has failed</summary>
		void OnSocketFailed(BetterHttpClientContext context, Socket socket, Exception error);

	}

	/// <summary>Stage in the execution of the query pipeline.</summary>
	public enum BetterHttpClientStage
	{
		/// <summary>The query has completed</summary>
		Completed = -1,
		/// <summary>The query is being prepared</summary>
		Prepare = 0,
		/// <summary>The query is being configured.</summary>
		Configure,
		/// <summary>The query is being sent.</summary>
		Send,
		/// <summary>The client is connecting to the remote server.</summary>
		Connecting,
		/// <summary>The Request message is being prepared for sending</summary>
		PrepareRequest,
		/// <summary>The Request message has been sent</summary>
		CompleteRequest,
		/// <summary>The Response message is being prepared for processing.</summary>
		PrepareResponse,
		/// <summary>The Response message is being processed.</summary>
		HandleResponse,
		/// <summary>The Response message has been processed.</summary>
		CompleteResponse,
		/// <summary>The Query is being finalized.</summary>
		Finalize,
	}

}
