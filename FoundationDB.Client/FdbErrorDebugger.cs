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
	using System.ComponentModel;
	using FoundationDB.Client.Native;

	/// <summary>Exposes the client's error-translation internals (message lookup, exception mapping, error predicates), for test harnesses and diagnostics.</summary>
	/// <remarks>
	/// <para>This is the error-side companion of <see cref="FdbTransactionDebugger"/>: a small proxy that lets test code observe internal behavior without requiring an <c>InternalsVisibleTo</c> grant. Conformance suites use it to pin the native client's answers as the oracle that emulator error behavior is held against.</para>
	/// <para>Every member calls into the native client library (<c>fdb_c</c>); no cluster connection is involved, but the library must be deployed, or the call fails with a <see cref="DllNotFoundException"/>.</para>
	/// <para>Application logic should never take decisions based on these observations: error handling belongs to the retry loop (<c>db.ReadAsync</c> / <c>WriteAsync</c>), never to per-error application code.</para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class FdbErrorDebugger
	{

		/// <summary>Returns the native client's message for this error code (<c>fdb_get_error</c>), or <see langword="null"/> if the code is unknown.</summary>
		public static string? GetErrorMessage(FdbError code) => FdbNative.GetErrorMessage(code);

		/// <summary>Maps an error code to the exception instance the client would throw for it, or <see langword="null"/> for <see cref="FdbError.Success"/>.</summary>
		public static Exception? MapToException(FdbError code) => FdbNative.MapToException(code);

		/// <summary>Evaluates one of the native client's error predicates (<c>fdb_error_predicate</c>) against an error code.</summary>
		public static bool TestErrorPredicate(FdbErrorPredicate predicate, FdbError code) => FdbNative.TestErrorPredicate(predicate, code);

	}

}
