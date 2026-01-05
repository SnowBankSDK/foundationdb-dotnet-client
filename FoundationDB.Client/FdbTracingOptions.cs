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

	/// <summary>Options that can configure the tracing for transactions</summary>
	[Flags]
	[PublicAPI]
	public enum FdbTracingOptions
	{

		/// <summary>Disable all tracing.</summary>
		None = 0,

		/// <summary>Record spans for each transaction</summary>
		RecordTransactions = 1,

		/// <summary>Record spans for each retry loop iteration</summary>
		RecordOperations = 2,

		/// <summary>Record spans for each sub-step of a retry loop iteration (handler, value-checks, commit, ...)</summary>
		RecordSteps = 4,

		/// <summary>Maintain a counter number of calls to each base API operators (Get, GetRange, Set, Clear, ClearRange, ...)</summary>
		RecordApiCalls = 8,

		// ----

		/// <summary>The default tracing options.</summary>
		Default = RecordTransactions | RecordOperations,

		/// <summary>Enable all traces</summary>
		All = RecordTransactions | RecordOperations | RecordSteps | RecordApiCalls,

	}

}
