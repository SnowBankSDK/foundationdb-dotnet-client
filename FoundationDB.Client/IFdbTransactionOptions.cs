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

	/// <summary>Helper that can set various options on transactions</summary>
	[PublicAPI]
	public interface IFdbTransactionOptions
	{

		/// <summary>API version of this transaction</summary>
		int ApiVersion { get; }

		/// <summary>Timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are ``[0, int.MaxValue]``.
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// </summary>
		int Timeout { get; set; }

		/// <summary>Maximum number of retries after which additional calls to onError will throw the most recently seen error code.
		/// Valid parameter values are ``[-1, int.MaxValue]``.
		/// If set to -1, will disable the retry limit.
		/// </summary>
		int RetryLimit { get; set; }


		/// <summary>Maximum amount of back-off delay incurred in the call to onError if the error is retry-able.
		/// Defaults to 1000 ms. Valid parameter values are [0, int.MaxValue].
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// </summary>
		int MaxRetryDelay { get; set; }

		/// <summary>Tracing options for this transaction.</summary>
		FdbTracingOptions Tracing { get; set; }

		/// <summary>Sets an option on this transaction that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		IFdbTransactionOptions SetOption(FdbTransactionOption option);

		/// <summary>Sets an option on this transaction that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		IFdbTransactionOptions SetOption(FdbTransactionOption option, long value);

		/// <summary>Sets an option on this transaction that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		IFdbTransactionOptions SetOption(FdbTransactionOption option, ReadOnlySpan<char> value);

		/// <summary>Sets an option on this transaction that takes a byte array value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		IFdbTransactionOptions SetOption(FdbTransactionOption option, ReadOnlySpan<byte> value);

	}

}
