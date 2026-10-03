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

	/// <summary>Result of <see cref="Fdb.ProbeAsync(System.Net.EndPoint, CancellationToken)"/>: the FoundationDB process that answered, and what it told about itself</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class FdbProbeResult
	{

		internal FdbProbeResult(FdbEndPoint endPoint, FdbProtocolVersion protocolVersion)
		{
			this.EndPoint = endPoint;
			this.ProtocolVersion = protocolVersion;
		}

		/// <summary>Address of the process that answered</summary>
		/// <remarks>For a host name, this is the resolved IP address that answered. For a cluster, this is the first coordinator that answered.</remarks>
		public FdbEndPoint EndPoint { get; }

		/// <summary>Protocol version of the process (ex: <c>0x1FDB00B074000000</c> for 7.4)</summary>
		/// <remarks>Use <see cref="FdbProtocolVersion.ToVersion"/> to get the major and minor version.</remarks>
		public FdbProtocolVersion ProtocolVersion { get; }

		/// <inheritdoc />
		public override string ToString() => $"{this.EndPoint}: {this.ProtocolVersion} ({this.ProtocolVersion.ToVersion()})";

	}

}
