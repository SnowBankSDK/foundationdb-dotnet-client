#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Aspire.Hosting.ApplicationModel
{
	using System.Data.Common;

	/// <summary>Represents an externally hosted FoundationDB Cluster in a distributed application.</summary>
	public class FdbConnectionResource : Resource, IFdbResource
	{

		/// <summary>Creates a resource for an externally hosted FoundationDB Cluster</summary>
		/// <param name="name">The name of the resource.</param>
		public FdbConnectionResource(string name) : base(name) { }

		/// <summary>The minimum API version that must be supported by the cluster</summary>
		/// <remarks>See <see cref="Fdb.Start(int)"/> for more information.</remarks>
		public int ApiVersion { get; set; }

		/// <summary>Default root location used by the database</summary>
		/// <remarks>See <see cref="FdbConnectionOptions.Root"/> for more information.</remarks>
		public FdbPath Root { get; set; }

		/// <summary>Full path to a specific 'fdb.cluster' file</summary>
		/// <remarks>
		/// <para>See <see cref="FdbConnectionOptions.ClusterFile"/> for more information.</para>
		/// <para>This property and <see cref="ClusterContents"/> are mutually exclusive.</para>
		/// </remarks>
		public string? ClusterFile { get; set; }

		/// <summary>Connection string to the cluster</summary>
		/// <remarks>
		/// <para>See <see cref="FdbConnectionOptions.ConnectionString"/> for more information.</para>
		/// <para>This property and <see cref="ClusterFile"/> are mutually exclusive.</para>
		/// </remarks>
		public string? ClusterContents { get; set; }

		/// <summary>If specified, the minimum version of the cluster to be deployed.</summary>
		/// <remarks>The actual version of the cluster may be different, depending on the <see cref="FdbVersionPolicy">versioning policy</see> used during deployment</remarks>
		public Version? ClusterVersion { get; set; }

		/// <summary>Path to the local native client library ('fdb_c.dll' or 'libfdb_c.so')</summary>
		/// <remarks>
		/// <para>This value if ignored when <see cref="DisableNativePreloading"/> is set to <see langword="true"/>.</para>
		/// <para>See <see cref="Fdb.Options.SetNativeLibPath"/> for more information.</para>
		/// </remarks>
		public string? NativeLibraryPath { get; set; }

		/// <summary>Specifies if native preloading should be enabled or disabled</summary>
		/// <remarks>
		/// <para>If <see langword="true"/>, the value of <see cref="NativeLibraryPath"/> will be ignored.</para>
		/// <para>See <see cref="Fdb.Options.DisableNativeLibraryPreloading"/> for more information.</para>
		/// </remarks>
		public bool DisableNativePreloading { get; set; }

		//TODO: more options? Debug? TraceId? Timeout? ....

		/// <inheritdoc />
		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{GetConnectionString()}");

		/// <summary>Returns the corresponding connection string for this resource</summary>
		public string? GetConnectionString()
		{
			var builder = new DbConnectionStringBuilder();

			builder["ApiVersion"] = this.ApiVersion;

			if (!this.Root.IsEmpty)
			{
				builder["Root"] = this.Root.ToString();
			}

			if (!string.IsNullOrWhiteSpace(this.ClusterFile))
			{
				builder["ClusterFile"] = this.ClusterFile;
			}

			if (!string.IsNullOrWhiteSpace(this.ClusterContents))
			{
				builder["ClusterFileContents"] = this.ClusterContents;
			}

			if (this.ClusterVersion != null)
			{
				builder["ClusterVersion"] = this.ClusterVersion.ToString();
			}

			return builder.ConnectionString;
		}

	}

}
