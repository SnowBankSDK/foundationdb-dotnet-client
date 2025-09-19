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
	using System.Globalization;

	/// <summary>Represents a FoundationDB cluster resource in a distributed application.</summary>
	/// <remarks>During local development, a local docker image is used to run a single-node cluster.</remarks>
	public class FdbClusterResource : ContainerResource, IFdbResource
	{

		/// <summary>A resource that represents a FoundationDB cluster</summary>
		/// <param name="name">The name of the resource.</param>
		/// <param name="entrypoint">An optional container entrypoint.</param>
		public FdbClusterResource(string name, string? entrypoint = null) : base(name, entrypoint) { }

		/// <summary>The minimum API version that must be supported by the cluster</summary>
		/// <remarks>See <see cref="Fdb.Start(int)"/> for more information.</remarks>
		public required int ApiVersion { get; set; }

		/// <summary>If specified, the minimum version of the cluster to be deployed.</summary>
		/// <remarks>If <see langword="null"/>, the best version compatible with <see cref="ApiVersion"/> will be selected</remarks>
		public required Version ClusterVersion { get; set; }

		/// <summary>Strategy used to select the actual runtime version of the deployed cluster.</summary>
		/// <remarks>The strategy works similarly to the <c>rollForward</c> property of the <c>global.json</c> file, see https://learn.microsoft.com/en-us/dotnet/core/tools/global-json.</remarks>
		public required FdbVersionPolicy RollForward { get; set; }

		/// <summary>Tag of the docker image that will be used to run the cluster locally. (ex: <c>"latest"</c>, <c>"7.3.68"</c>, ...)</summary>
		public required string DockerTag { get; set; }

		/// <summary>Path to the local native client library (<c>fdb_c.dll</c> on Windows, <c>libfdb_c.so</c> on Linux, <c>libfdb_c.dylib</c> on macOS, ...)</summary>
		/// <remarks>
		/// <para>This value is ignored if <see cref="DisableNativePreloading"/> is set to <see langword="true"/>.</para>
		/// <para>See <see cref="Fdb.Options.SetNativeLibPath"/> for more information.</para>
		/// </remarks>
		public string? NativeLibraryPath { get; set; }

		/// <summary>Specifies if native preloading should be enabled or disabled</summary>
		/// <remarks>
		/// <para>If <see langword="true"/>, the value of <see cref="NativeLibraryPath"/> will be ignored.</para>
		/// <para>See <see cref="Fdb.Options.DisableNativeLibraryPreloading"/> for more information.</para>
		/// </remarks>
		public bool DisableNativePreloading { get; set; }

		/// <summary>Default transaction timeout</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.Timeout"/> for more information.</remarks>
		public TimeSpan? DefaultTimeout { get; set; }

		/// <summary>Default transaction retry limit</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.RetryLimit"/> for more information.</remarks>
		public int? DefaultRetryLimit { get; set; }

		/// <summary>Default tracing options</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.Tracing"/> for more information.</remarks>
		public FdbTracingOptions? DefaultTracing { get; set; }

		/// <summary>Specifies if the FoundationDB cluster is mounted in read-only mode by default.</summary>
		/// <remarks>See <see cref="FdbConnectionOptions.ReadOnly"/> for more information.</remarks>
		public bool? ReadOnly { get; set; }

		/// <summary>Specifies the default root path of the partition used by all processes.</summary>
		/// <remarks>See <see cref="FdbConnectionOptions.Root"/> for more information.</remarks>
		public FdbPath Root { get; set; } = FdbPath.Root;

		/// <summary>Specifies the description field in the cluster connection string.</summary>
		/// <remarks>
		/// <para>This corresponds to the 'description' part of the equivalent 'fdb.cluster' file.</para>
		/// <para>This value is for humans only, and is not significant for the connection itself.</para>
		/// <para>Defaults to <c>"docker"</c> when omitted.</para>
		/// </remarks>
		public string? ClusterDescription { get; set; } = "docker";

		/// <summary>Specifies the 'id' part of the locally generated 'fdb.cluster' file.</summary>
		/// <remarks>
		/// <para>This corresponds to the 'id' part of the equivalent 'fdb.cluster' file.</para>
		/// <para>This value <b>must</b> match the id of the cluster, and if incorrect or changed, may prevent the process from connecting successfully.</para>
		/// <para>Defaults to <c>"docker"</c> when omitted.</para>
		/// </remarks>
		public string? ClusterId { get; set; } = "docker";

		/// <summary>Specifies the common Session ID used by all resources when logging transactions, during local development.</summary>
		/// <remarks>
		/// <para>All transactions logged by any node using this cluster resource will use the same session identifier, allowing for easier merging and scanning for conflicting transactions or correlated errors.</para>
		/// <para>This should only be used during local development.</para>
		/// </remarks>
		public string? LogSessionId { get; set; }

		/// <inheritdoc />
		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{GetConnectionString()}");

		/// <summary>Generates the Connection String that can be injected into other processes to connect to this cluster</summary>
		public string GetConnectionString()
		{
			string clusterDesc = this.ClusterDescription ?? this.Name;
			string clusterId = this.ClusterId ?? this.Name;

			var ep = this.GetEndpoint("tcp");

			var coordinatorHost = ep.Host;
			if (coordinatorHost == "localhost") coordinatorHost = "127.0.0.1";
			var coordinatorPort = ep.Port;

			// Cluster File format: "<DESC>:<ID>@<HOST1>:<PORT1>[,<HOST2>:<PORT2>,...]"
			// By default, the docker image uses "docker:docker@127.0.0.1:4550"

			string contents = $"{clusterDesc}:{clusterId}@{coordinatorHost}:{coordinatorPort.ToString(CultureInfo.InvariantCulture)}";

			//TODO: replace this with a proper use of ReferenceExpression?

			var builder = new DbConnectionStringBuilder
			{
				["ApiVersion"] = this.ApiVersion,
				["Root"] = this.Root.ToString(),
				["ClusterFileContents"] = contents,
				["ClusterVersion"] = this.ClusterVersion.ToString(),
				//TODO: more options? Debug? TraceId? Timeout? ...
			};
			if (this.DisableNativePreloading)
			{
				builder["DisableNativePreloading"] = true;
			}
			else if (!string.IsNullOrEmpty(this.NativeLibraryPath))
			{
				builder["NativeLibrary"] = this.NativeLibraryPath;
			}

			if (this.DefaultTimeout.HasValue)
			{
				if (this.DefaultTimeout.Value < TimeSpan.Zero) throw new InvalidOperationException("Default timeout must be a positive value");
				builder["DefaultTimeout"] = this.DefaultTimeout.Value.TotalSeconds.ToString("R", CultureInfo.InvariantCulture);
			}

			if (this.DefaultRetryLimit.HasValue)
			{
				if (this.DefaultRetryLimit.Value < 0) throw new InvalidOperationException("Default retry limit must be a positive value");
				builder["DefaultRetryLimit"] = this.DefaultRetryLimit.Value.ToString(CultureInfo.InvariantCulture);
			}

			if (this.DefaultTracing.HasValue)
			{
				builder["DefaultTracing"] = ((int) this.DefaultTracing.Value).ToString(CultureInfo.InvariantCulture);
			}

			if (this.ReadOnly == true)
			{
				builder["ReadOnly"] = true;
			}

			if (this.LogSessionId is not null)
			{
				builder["LogSessionId"] = this.LogSessionId;
			}

			return builder.ConnectionString;
		}

	}

}
