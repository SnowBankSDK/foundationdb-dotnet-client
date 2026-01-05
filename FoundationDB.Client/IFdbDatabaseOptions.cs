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

	[PublicAPI]
	public interface IFdbDatabaseOptions
	{

		/// <summary>API version selected for this database</summary>
		int ApiVersion { get; }

		/// <summary>Full path to the '.cluster' file that contains the connection string to the cluster</summary>
		/// <remarks>
		/// <para>This should be a valid path, accessible with read and write permissions by the process.</para>
		/// <para>This property and <see cref="ConnectionString"/> are mutually exclusive.</para>
		/// </remarks>
		string? ClusterFile { get; }

		/// <summary>Connection string to the cluster</summary>
		/// <remarks>
		/// <para>The format of this string is the same as the content of a <c>.cluster</c> file.</para>
		/// <para>This property and <see cref="ClusterFile"/> are mutually exclusive.</para>
		/// </remarks>
		string? ConnectionString { get; }

		/// <summary>Default Timeout value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>If changed, will only be effective for future transactions</remarks>
		int DefaultTimeout { get; set; }

		/// <summary>Default Retry Limit value for all transactions created from this database instance.</summary>
		/// <remarks>If changed, will only be effective for future transactions</remarks>
		int DefaultRetryLimit { get; set; }

		/// <summary>Default Maximum Retry Delay value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>If changed, will only be effective for future transactions</remarks>
		int DefaultMaxRetryDelay { get; set; }

		/// <summary>Default tracing options for all transactions created from this database instance.</summary>
		/// <remarks>If changed, will only be effective for future transactions</remarks>
		FdbTracingOptions DefaultTracing { get; set; }

		/// <summary>Set an option on this database that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		IFdbDatabaseOptions SetOption(FdbDatabaseOption option);

		/// <summary>Set an option on this database that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		IFdbDatabaseOptions SetOption(FdbDatabaseOption option, long value);

		/// <summary>Set an option on this database that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be empty)</param>
		IFdbDatabaseOptions SetOption(FdbDatabaseOption option, ReadOnlySpan<char> value);

		/// <summary>Set an option on this database that takes a byte array value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be empty)</param>
		IFdbDatabaseOptions SetOption(FdbDatabaseOption option, ReadOnlySpan<byte> value);

	}

}
