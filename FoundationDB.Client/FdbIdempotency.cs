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

	/// <summary>Extension exposing native FoundationDB idempotency helpers on a transaction's options.</summary>
	[PublicAPI]
	public static class FdbIdempotencyExtensions
	{

		/// <summary>Minimum api level at which native FoundationDB idempotency is available (fdb 7.2).</summary>
		public const int MinimumApiVersion = 720;

		extension(IFdbTransactionOptions options)
		{

			/// <summary>Returns <see langword="true"/> when the connected cluster supports native automatic idempotency (api level 720 or greater, fdb 7.2+).</summary>
			/// <remarks>
			/// <para>Native FoundationDB idempotency (the <see cref="FdbTransactionOption.AutomaticIdempotency"/> option) makes a commit that
			/// returns an ambiguous result resolve to a definitive outcome, so the handler is never re-run on top of its own landed writes.</para>
			/// <para>When this is <see langword="false"/>, a handler either proceeds without idempotency (accepting the maybe-committed hazard) or applies its own fallback.</para>
			/// </remarks>
			public bool IsAutomaticIdempotencySupported => options.ApiVersion >= MinimumApiVersion;

		}

	}

}
