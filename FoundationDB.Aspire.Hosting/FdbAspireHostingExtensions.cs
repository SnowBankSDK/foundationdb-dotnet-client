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

namespace Aspire.Hosting
{
	using System.Globalization;
	using System.Net;
	using System.Text;
	using Aspire.Hosting.ApplicationModel;
	using Aspire.Hosting.Publishing;

	/// <summary>Provides extension methods for adding FoundationDB resources to the application model.</summary>
	[PublicAPI]
	public static class FdbAspireHostingExtensions
	{

		/// <summary>Tag for the latest v7.4 docker image</summary>
		public static readonly Version LatestVersion74 = new Version(7, 4, 4);

		/// <summary>Tag for the latest v7.3 docker image</summary>
		public static readonly Version LatestVersion73 = new Version(7, 3, 70);

		/// <summary>Tag for the latest v7.2 docker image</summary>
		public static readonly Version LatestVersion72 = new Version(7, 2, 9);

		/// <summary>Tag for the latest v7.1 docker image</summary>
		public static readonly Version LatestVersion71 = new Version(7, 1, 62);

		#region Fdb Cluster Connections...

		/// <summary>Adds a connection to an external FoundationDB cluster</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Base subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="clusterFile">Path to the cluster file, or <c>null</c> if the default cluster file should be used.</param>
		/// <param name="clusterVersion">If not <c>null</c>, the known version of the remote cluster, which can be used to infer the appropriate version of the local FDB client library that should be used to connect to this cluster.</param>
		public static IResourceBuilder<FdbConnectionResource> AddFoundationDbCluster(this IDistributedApplicationBuilder builder, string name, int apiVersion, string root, string? clusterFile = null, string? clusterVersion = null)
		{
			return AddFoundationDbCluster(builder, name, apiVersion, FdbPath.Parse(root), clusterFile, clusterVersion);
		}

		/// <summary>Adds a connection to an external FoundationDB cluster</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Root subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="clusterFile">Path to the cluster file, or <c>null</c> if the default cluster file should be used.</param>
		/// <param name="clusterVersion">If not <c>null</c>, the known version of the remote cluster, which can be used to infer the appropriate version of the local FDB client library that should be used to connect to this cluster.</param>
		public static IResourceBuilder<FdbConnectionResource> AddFoundationDbCluster(this IDistributedApplicationBuilder builder, string name, int apiVersion, FdbPath root, string? clusterFile = null, string? clusterVersion = null)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrWhiteSpace(name);
			Contract.GreaterThan(apiVersion, 0);

			//REVIEW: should we allow formats like "7.3" or "7.3.*" to mean "I don't know exactly, but it is 7.3.something, figure it out!" ?
			//REVIEW: should we also include a "RollForward" policy here? The version of the cluster is fixed, we can only use this to select a client version with more limited wriggle room (cannot jump minor or major version, for example)
			Version? ver = null;
			if (!string.IsNullOrWhiteSpace(clusterVersion) && !Version.TryParse(clusterVersion, out ver))
			{
				throw new ArgumentException("Malformed cluster version", nameof(clusterVersion));
			}

			var fdbConn = new FdbConnectionResource(name)
			{
				ApiVersion = apiVersion,
				Root = root,
				ClusterFile = clusterFile,
				ClusterVersion = ver,
			};

			return builder
				.AddResource(fdbConn)
				.WithAnnotation(new ManifestPublishingCallbackAnnotation((ctx) => WriteFdbConnectionToManifest(ctx, fdbConn)))
			;
		}

		/// <summary>Configures the FoundationDB client to use the cluster file at the default location</summary>
		public static IResourceBuilder<FdbConnectionResource> WithDefaultClusterFile(this IResourceBuilder<FdbConnectionResource> builder)
		{
			builder.Resource.ClusterFile = null;
			builder.Resource.ClusterContents = null;
			return builder;
		}

		/// <summary>Configures the FoundationDB client to use a cluster file at a specific location</summary>
		public static IResourceBuilder<FdbConnectionResource> WithClusterFile(this IResourceBuilder<FdbConnectionResource> builder, string clusterFile)
		{
			builder.Resource.ClusterFile = clusterFile;
			builder.Resource.ClusterContents = null;
			return builder;
		}

		/// <summary>Configures the FoundationDB client to use a hardcoded cluster file content</summary>
		public static IResourceBuilder<FdbConnectionResource> WithClusterContents(this IResourceBuilder<FdbConnectionResource> builder, string clusterContents)
		{
			builder.Resource.ClusterFile = null;
			builder.Resource.ClusterContents = clusterContents;
			return builder;
		}

		/// <summary>Configures the FoundationDB client to use a hardcoded cluster file content</summary>
		public static IResourceBuilder<FdbConnectionResource> WithClusterContents(this IResourceBuilder<FdbConnectionResource> builder, string description, string id, params EndPoint[] coordinators)
		{
			// "<DESC>:<ID>@<HOST1>:<PORT1>[,<HOST2>:<PORT2>,....]"

			var sb = new StringBuilder();
			sb.Append(description).Append(':').Append(id);
			for (int i = 0; i < coordinators.Length; i++)
			{
				sb.Append(i == 0 ? '@' : ',');
				switch (coordinators[i])
				{
					case IPEndPoint ip:
					{
						sb.Append(CultureInfo.InvariantCulture, $"{ip.Address}:{ip.Port}");
						break;
					}
					case DnsEndPoint dns:
					{
						sb.Append(CultureInfo.InvariantCulture, $"{dns.Host}:{dns.Port}");
						break;
					}
					default:
					{
						throw new ArgumentException("Unsupported coordinator endpoint type", nameof(coordinators));
					}
				}
			}

			builder.Resource.ClusterFile = null;
			builder.Resource.ClusterContents = sb.ToString();
			return builder;
		}

		/// <summary>Configures the FoundationDB client to use a specific library version</summary>
		public static IResourceBuilder<FdbConnectionResource> WithClusterVersion(this IResourceBuilder<FdbConnectionResource> builder, string version)
		{
			builder.Resource.ClusterVersion = Version.Parse(version);
			return builder;
		}

		#endregion

		#region Locally hosted FDB Cluster using Docker containers...

		/// <summary>Adds a FoundationDB container to application model.</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Root subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="port">Custom port for the docker container</param>
		/// <param name="clusterVersion">If not <c>null</c>, specifies the targeted version for the cluster nodes (ex: "7.4.4", "7.3.27", "7.4.*", "7.*", ...)</param>
		/// <param name="rollForward">Specifies the policy used to optionally select a more recent version</param>
		public static IResourceBuilder<FdbClusterResource> AddFoundationDb(this IDistributedApplicationBuilder builder, string name, int apiVersion, string root, int? port = null, string? clusterVersion = null, FdbVersionPolicy? rollForward = null)
		{
			return AddFoundationDb(builder, name, apiVersion, FdbPath.Parse(root), port, clusterVersion, rollForward);
		}

		/// <summary>Adds a FoundationDB container to application model.</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Root subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="port">The host port to bind the underlying container to (defaults to <c>4550</c>)</param>
		/// <param name="clusterVersion">If not <c>null</c>, specifies the targeted version for the cluster nodes (ex: "7.4.4", "7.3.27", "7.4.*", "7.*", ...)</param>
		/// <param name="rollForward">Specifies the policy used to optionally select a more recent version</param>
		/// <param name="imageRegistry">Specifies a custom image registry for the container (defaults to <c>"docker.io"</c>)</param>
		public static IResourceBuilder<FdbClusterResource> AddFoundationDb(
			this IDistributedApplicationBuilder builder,
			string name,
			int apiVersion,
			FdbPath root,
			int? port = null,
			string? clusterVersion = null,
			FdbVersionPolicy? rollForward = null,
			string? imageRegistry = null
		)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrWhiteSpace(name);
			Contract.GreaterThan(apiVersion, 0);

			Version? ver;
			if (string.IsNullOrWhiteSpace(clusterVersion) || clusterVersion == "*")
			{ // version is not specified

				// Use the request ApiVersion to select the correct version.
				// For example, the version corresponding to level 740 is 7.4 (last digit is usually always 0)
				int major = (apiVersion / 100);
				int minor = (apiVersion / 10) % 10;
				ver = new Version(major, minor);
				rollForward ??= FdbVersionPolicy.LatestMajor;
			}
			else if (clusterVersion.Length > 2 && clusterVersion.EndsWith(".*", StringComparison.Ordinal))
			{ // version includes a variable part
				ver = Version.Parse(clusterVersion[..^2]);
				if (ver.Minor < 0)
				{ // ex: "7.*"
					rollForward ??= FdbVersionPolicy.LatestMinor;
				}
				else
				{ // ex: "7.4.x"
					rollForward ??= FdbVersionPolicy.LatestPatch;
				}
			}
			else
			{ // exact version
				ver = Version.Parse(clusterVersion);
				rollForward ??= FdbVersionPolicy.Exact;
			}

			if (string.IsNullOrWhiteSpace(imageRegistry))
			{
				imageRegistry = "docker.io";
			}

			// select the docker image tag that corresponds to the version and specified roll forward policy
			var dockerTag = ComputeDockerTagFromVersion(ver, rollForward.Value);

			var fdbCluster = new FdbClusterResource(name)
			{
				ApiVersion = apiVersion,
				Root = root,
				ClusterVersion = ver,
				RollForward = rollForward.Value,
				DockerTag = dockerTag,
			};

			//note: Aspire wants to allocate random ports to ensure that there is not conflict with any local versions of the resources,
			// but we have an issue where the fdbserver that runs in the docker image sees the "targetPort" of the container, which is 4550
			// and will return this as part of the address sent to any client. So if, inside the container, the port is 4550, it HAS to be 4550 also on the host!
			// If not, if for ex Aspire allocated port 12345 externally, we will set the "connection string" to "127.0.0.1:12345",
			// which will initially be proxied to the port 4550 inside the container (so far so good), but the fdb node will return addresses to other "agents" using "127.0.0.1:4550" because it does not know of the port 12345
			// The application will think that we are pointed to a different node, and attempt to connect to 127.0.0.1:4550 which would not exist on the host!

			// => To work around this problem, we must FORCE both the "aspire port" and the "container port" to be the same, as well as disable the Aspire proxy.

			// In order to prevent any conflict with any natively installed FoundationDB server, we will use the port 4550,
			// which is outside the typical range of 4500+ for default fdb installations (unless there are more than 50 processes on the same box??)

			int nodePort = port ?? 4550;

			var cluster = builder
				.AddResource(fdbCluster)
				.WithAnnotation(new ManifestPublishingCallbackAnnotation((ctx) => WriteFdbClusterToManifest(ctx, fdbCluster)))
				.WithEndpoint(port: nodePort, targetPort: nodePort, name: "tcp", isProxied: false) // note: both ports MUST be the same (see above)
				.WithImage(image: "foundationdb/foundationdb", tag: fdbCluster.DockerTag)
				.WithImageRegistry(imageRegistry)
				.WithVolume("fdb_data", "/var/fdb/data", isReadOnly: false) //HACKHACK: TODO: make this configurable!
				.WithEnvironment((context) =>
				{
					// get the allocated endpoint
					var ep = fdbCluster.GetEndpoint("tcp");
					//note: it SHOULD be equal to 'nodePort' here!
					Contract.Debug.Assert(ep.Port == nodePort);

					// we use the "host" mode so that we can talk to the node from the host
					context.EnvironmentVariables["FDB_NETWORKING_MODE"] = "host";
					//REVIEW: if the "*.docker.internal" names get supported by Aspire, maybe we can switch to those?
					// => if we have more than one fdb container, we need them to be able to talk to each other, AND to the host!

					// the port that the fdbserver will bind
					context.EnvironmentVariables["FDB_PORT"] =  ep.Port.ToString(CultureInfo.InvariantCulture);
					// we are the coordinator so point it back to itself
					context.EnvironmentVariables["FDB_COORDINATOR_PORT"] = ep.Port.ToString(CultureInfo.InvariantCulture);
				});

			return cluster;
		}

		/// <summary>Specifies the path to the native FoundationDB C library that should be used by the application</summary>
		/// <param name="builder">FDB cluster builder</param>
		/// <param name="nativeLibraryPath">Path to the library on the host. The path may be rewritten if required.</param>
		/// <remarks>
		/// <para>This should only be used for local development, or very specific deployments where the application must use a very specific build of the native library.</para>
		/// <para>The file must be present on the host. If a project that references this resource runs inside a Docker image, the path may be rewritten to where the library was copied inside the container image.</para>
		/// </remarks>
		public static IResourceBuilder<FdbClusterResource> WithNativeLibrary(this IResourceBuilder<FdbClusterResource> builder, string? nativeLibraryPath)
		{
			Contract.NotNull(builder);
			var fdbCluster = builder.Resource;
			fdbCluster.NativeLibraryPath = !string.IsNullOrWhiteSpace(nativeLibraryPath) ? nativeLibraryPath.Trim() : null;
			return builder;
		}

		/// <summary>Sets various default configuration options (timeouts, max retry limits, ...) that will be applied to all processes using this resource.</summary>
		/// <param name="builder">FDB cluster builder</param>
		/// <param name="timeout">Default transaction timeout (or TimeSpan.Zero for infinite timeout)</param>
		/// <param name="retryLimit">Default transaction max retry limit (or 0 for infinite retries)</param>
		/// <param name="readOnly">If true, the process will only have read-only access to the FoundationDB cluster.</param>
		/// <param name="tracing">Default tracing options</param>
		/// <param name="logSessionId">Default common Log Session Id</param>
		/// <remarks>These settings will be applied to the default <see cref="FdbConnectionOptions">connection options</see>, and can be overriden per transaction, or during the program startup.</remarks>
		public static IResourceBuilder<FdbClusterResource> WithDefaults(this IResourceBuilder<FdbClusterResource> builder, TimeSpan? timeout = null, int? retryLimit = null, bool? readOnly = null, FdbTracingOptions? tracing = null, string? logSessionId = null)
		{
			Contract.NotNull(builder);
			var fdbCluster = builder.Resource;
			if (timeout != null) fdbCluster.DefaultTimeout = timeout;
			if (retryLimit != null) fdbCluster.DefaultRetryLimit = retryLimit;
			if (readOnly != null) fdbCluster.ReadOnly = readOnly;
			if (tracing != null) fdbCluster.DefaultTracing = tracing;
			if (logSessionId != null) fdbCluster.LogSessionId = logSessionId;
			return builder;
		}

		/// <summary>Sets the Session ID used when logging transactions to this local cluster.</summary>
		/// <param name="builder">FDB cluster builder</param>
		/// <param name="sessionId">Common Session Identifier used by all transactions logged.</param>
		/// <remarks>
		/// <para>This identifier will be injected, via the connection string, to all resources that reference this cluster, and will be automatically be assigned to all logged transactions.</para>
		/// <para>Once a session has been completed, this identifier could then be used to match conflicting transactions, or to recreate a sensible history of what happened during a specific time window.</para>
		/// </remarks>
		public static IResourceBuilder<FdbClusterResource> WithLogSessionId(this IResourceBuilder<FdbClusterResource> builder, string sessionId)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrEmpty(sessionId);
			if (sessionId.Length > 1024) throw new ArgumentException("Log Session ID is too large.");

			builder.Resource.LogSessionId = sessionId;
			return builder;
		}

		/// <summary>Sets the Session ID used when logging transactions to this local cluster to a pseudo-random value.</summary>
		/// <param name="builder">FDB cluster builder</param>
		/// <remarks>
		/// <para>The identifier will be generated using the current time, the hostname and current PID. This will ensure that any log file that will use this identifier should be mostly sorted chronologically.</para>
		/// <para>This identifier will be injected, via the connection string, to all resources that reference this cluster, and will be automatically be assigned to all logged transactions.</para>
		/// <para>Once a session has been completed, this identifier could then be used to match conflicting transactions, or to recreate a sensible history of what happened during a specific time window.</para>
		/// </remarks>
		public static IResourceBuilder<FdbClusterResource> WithRandomizedLogSessionId(this IResourceBuilder<FdbClusterResource> builder)
		{
			// we use a mix of the starting time, the PID of the AppHost itself, and some randomness
			// => this should ensure that logs are somewhat sorted chronologically
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var host = Environment.MachineName;
			var pid = Environment.ProcessId;
			var prefix = string.Create(CultureInfo.InvariantCulture, $"{now}_{host}_{pid:D05}");

			return builder.WithLogSessionId(prefix);
		}

		#endregion

		private static void WriteFdbClusterToManifest(ManifestPublishingContext context, FdbClusterResource cluster)
		{
			var jsonWriter = context.Writer;
			jsonWriter.WriteString("type", "fdb.cluster.v0");
			jsonWriter.WriteNumber("apiVersion", cluster.ApiVersion);
			jsonWriter.WriteString("root", cluster.Root.ToString());
			jsonWriter.WriteString("clusterId", cluster.ClusterId);
			jsonWriter.WriteString("clusterDesc", cluster.ClusterDescription);
			jsonWriter.WriteString("version", cluster.ClusterVersion.ToString());
			jsonWriter.WriteString("rollForward", cluster.RollForward.ToString());
			if (!string.IsNullOrWhiteSpace(cluster.DockerTag))
			{
				jsonWriter.WriteString("tag", cluster.DockerTag);
			}
		}

		private static void WriteFdbConnectionToManifest(ManifestPublishingContext context, FdbConnectionResource connection)
		{
			var jsonWriter = context.Writer;
			jsonWriter.WriteString("type", "fdb.connection.v0");
			jsonWriter.WriteNumber("apiVersion", connection.ApiVersion);
			jsonWriter.WriteString("root", connection.Root.ToString());
			if (connection.ClusterVersion != null)
			{
				jsonWriter.WriteString("version", connection.ClusterVersion.ToString());
			}
			if (!string.IsNullOrWhiteSpace(connection.ClusterFile))
			{
				jsonWriter.WriteString("clusterFile", connection.ClusterFile);
			}
			if (!string.IsNullOrWhiteSpace(connection.ClusterContents))
			{
				jsonWriter.WriteString("clusterFileContents", connection.ClusterContents);
			}

			if (connection.DisableNativePreloading)
			{
				jsonWriter.WriteBoolean("disableNativePreloading", true);
			}
			else if (!string.IsNullOrWhiteSpace(connection.NativeLibraryPath))
			{
				jsonWriter.WriteString("nativeLibrary", connection.ClusterContents);
			}

		}

		private static string ComputeDockerTagFromVersion(Version version, FdbVersionPolicy rollForward)
		{
			// Important Note:
			// - As of now, they are always released in pairs: one with AVX instructions enabled (odd number, ex: 7.3.71) and one without AVX enabled (even number, ex: 7.3.70)
			// - The AVX enabled versions are faster, but are NOT compatible with ARM64 and will not run on Apple M-chip enabled laptops, where you need a non-AVX version.
			// - As this method is mostly used for the local dev loop, and to make it easier to develop on MBP or Mac Mini, we will always roll forward to even version (non-AVX)
			// => In production deployment, you should use the most appropriate version depending on the target platform.

			//TODO: maybe query the docker hub API, but use a local cache?
			// => https://registry.hub.docker.com/v2/repositories/foundationdb/foundationdb/tags?name=X.Y&ordering=last_updated

			switch (rollForward)
			{
				case FdbVersionPolicy.Exact:
				{
					return string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Build}");
				}
				case FdbVersionPolicy.Latest:
				{ // I like to live dangerously!
					return "latest";
				}
				case FdbVersionPolicy.LatestPatch:
				{ // Keep major.minor but use the latest patch (ie: X.Y.*)
					switch (version.Major, version.Minor)
					{
						case (7, 4):
						{
							return LatestVersion74.ToString();
						}
						case (7, 3):
						{
							return LatestVersion73.ToString();
						}
						case (7, 2):
						{
							return LatestVersion72.ToString();
						}
						case (7, 1):
						{
							return LatestVersion71.ToString();
						}
						default:
						{
							throw version.Major switch
							{
								< 7 => ErrorVersionIsTooOldMajor(version),
								7 => ErrorVersionIsTooOldMinor(version),
								_ => ErrorVersionIsGreaterThanSupportedByThisPackage(version)
							};
						}
					}
				}
				case FdbVersionPolicy.LatestMinor:
				{ // Keep major but use latest patch of latest minor (ie: X.*.*)
					switch (version.Major)
					{
						case 7:
						{
							if (version.Minor > 4)
							{
								throw ErrorVersionIsGreaterThanSupportedByThisPackage(version);
							}

							return version.Minor == 4 && version.Build > LatestVersion74.Build
								? string.Create(CultureInfo.InvariantCulture, $"7.4.{version.Build}")
								: LatestVersion74.ToString();
						}
						default:
						{
							throw version.Major < 7 ? ErrorVersionIsTooOldMajor(version) : ErrorVersionIsGreaterThanSupportedByThisPackage(version);
						}
					}
				}
				case FdbVersionPolicy.LatestMajor:
				{ // Use the latest (stable) version available

					if (version.Major > 7 || (version.Major == 7 && version.Minor > 4))
					{
						throw ErrorVersionIsGreaterThanSupportedByThisPackage(version);
					}

					if (version.Minor == 4 && version.Build > LatestVersion74.Build)
					{ // this 7.4 version was probably released after this code was packaged, we'll allow it
						return string.Create(CultureInfo.InvariantCulture, $"7.4.{version.Build}");
					}
					return LatestVersion74.ToString();
				}
				default:
				{
					throw new InvalidOperationException($"The roll forward policy '{rollForward}' is not supported.");
				}
			}
		}

		[MustUseReturnValue]
		private static InvalidOperationException ErrorVersionIsTooOldMajor(Version version)
			=> new($"There are no docker images available for version {version}. The first docker images available start from 7.1.0. Please use the 'latestMajor' policy, or select a version of 7.1 or greater.");

		[MustUseReturnValue]
		private static InvalidOperationException ErrorVersionIsTooOldMinor(Version version)
			=> new($"There are no docker images available for version {version}. The first docker images available start from 7.1.0. Please use the 'latestMinor' policy, or select a version of 7.1 or greater.");

		[MustUseReturnValue]
		private static InvalidOperationException ErrorVersionIsGreaterThanSupportedByThisPackage(Version version)
			=> new($"The selected version {version} is too recent and is not supported by this NuGet package. Please update the NuGet package or select an older version.");

	}

}
