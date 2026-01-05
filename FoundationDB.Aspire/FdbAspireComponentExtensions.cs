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

namespace Microsoft.Extensions.Hosting
{
	using System;
	using System.Data.Common;
	using System.Globalization;
	using System.IO;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using Aspire.FoundationDb.Client;
	using FoundationDB.Client;
	using FoundationDB.DependencyInjection;
	using JetBrains.Annotations;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Diagnostics.HealthChecks;
	using OpenTelemetry.Trace;

	/// <summary>Provides extension methods for registering FoundationDB-related services in an <see cref="T:Microsoft.Extensions.Hosting.IHostApplicationBuilder" />.</summary>
	[PublicAPI]
	public static class FdbAspireComponentExtensions
	{

		private const string DefaultConfigSectionName = "Aspire:FoundationDb:Client";

		/// <summary>Add support for connecting to a FoundationDB cluster</summary>
		/// <param name="builder">The <see cref="T:Microsoft.Extensions.Hosting.IHostApplicationBuilder" /> to read config from and add services to.</param>
		/// <param name="connectionName">Name of the FoundationDB cluster or connection resource, as defined in the Aspire AppHost.</param>
		/// <param name="configureSettings">An optional method that can be used for customizing the <see cref="FdbClientSettings" />. It's invoked after the settings are read from the configuration.</param>
		/// <param name="configureProvider">An optional method that can be used for customizing the <see cref="FdbDatabaseProviderOptions" />. It's invoked after the options are read from the configuration.</param>
		/// <remarks>Reads the configuration from "Aspire:FoundationDb:Client" section.</remarks>
		public static IHostApplicationBuilder AddFoundationDb(this IHostApplicationBuilder builder, string connectionName, Action<FdbClientSettings>? configureSettings = null, Action<FdbDatabaseProviderOptions>? configureProvider = null)
		{
			return AddFoundationDb(builder, connectionName, DefaultConfigSectionName, configureSettings, configureProvider);
		}

		private static IHostApplicationBuilder AddFoundationDb(this IHostApplicationBuilder builder, string connectionName, string configurationSectionName, Action<FdbClientSettings>? configureSettings, Action<FdbDatabaseProviderOptions>? configureProvider)
		{
			ArgumentNullException.ThrowIfNull(builder);

			var configSection = builder.Configuration.GetSection(configurationSectionName);

			var settings = new FdbClientSettings();
			configSection.Bind(settings);

			if (builder.Configuration.GetConnectionString(connectionName) is { } connectionString)
			{
				settings.ConnectionString = connectionString;
			}

			configureSettings?.Invoke(settings);

			// parse the connection string:
			var cnx = !string.IsNullOrWhiteSpace(settings.ConnectionString) ? new DbConnectionStringBuilder()
			{
				ConnectionString = settings.ConnectionString,
			} : null;
			//note: in general, the value in the connection string takes precedence other the values in the configuration section, which act like a default value (or when running outside of the Aspire AppHost)

			builder.Services.AddFoundationDb(options =>
			{
				options.AutoStart = true;

				// ApiVersion=123
				int apiVersion;
				if (cnx != null && cnx.ContainsKey("ApiVersion"))
				{
					var apiVersionLiteral = (string) cnx["ApiVersion"];
					if (!int.TryParse(apiVersionLiteral, NumberStyles.Integer, CultureInfo.InvariantCulture, out apiVersion) || apiVersion <= 0)
					{
						throw new InvalidOperationException("Invalid ApiVersion parameter in connection string.");
					}
				}
				else if (settings.ApiVersion is > 0)
				{ // use the value specified in the settings
					apiVersion = settings.ApiVersion.Value;
				}
				else
				{
					throw new InvalidOperationException($"Missing required ApiVersion parameter. It must be specified either be injected via the Aspire ConnectionString, or via the {DefaultConfigSectionName}:{nameof(settings.ApiVersion)} configuration option.");
				}
				options.ApiVersion = apiVersion;

				// Native Library Path
				if (cnx != null && cnx.ContainsKey("NativeLibrary"))
				{
					var nativeLibraryPath = (cnx["NativeLibrary"] as string)?.Trim();
					if (!string.IsNullOrEmpty(nativeLibraryPath))
					{ // preload the library at the specified path
						options.NativeLibraryPath = nativeLibraryPath;
					}
				}
				else if (cnx != null && cnx.ContainsKey("PreloadNativeLibrary"))
				{
					if (string.Equals((string) cnx["PreloadNativeLibrary"], "true", StringComparison.OrdinalIgnoreCase))
					{ // automatic pre-loading, OS will resolve
						options.NativeLibraryPath = "";
					}
					else
					{ // disable pre-loading, CLR will resolve
						options.NativeLibraryPath = null;
					}
				}
				else if (settings.NativeLibraryPath != null)
				{
					options.NativeLibraryPath = settings.NativeLibraryPath;
				}

				// Cluster File Path

				// either specified directly with ClusterFile=/path/to/file.cluster, or indirectly via ClusterFileContents=desc:id@ip:port
				string? clusterFilePath, clusterFileContents;
				if (cnx != null && (cnx.ContainsKey("ClusterFile") || cnx.ContainsKey("ClusterFileContents")))
				{
					clusterFilePath = cnx.ContainsKey("ClusterFile") ? ((string?) cnx["ClusterFile"])?.Trim() : null;
					clusterFileContents = cnx.ContainsKey("ClusterFileContents") ? ((string?) cnx["ClusterFileContents"])?.Trim() : null;
				}
				else
				{
					clusterFilePath = !string.IsNullOrWhiteSpace(settings.ClusterFile) ? settings.ClusterFile.Trim() : null;
					clusterFileContents = !string.IsNullOrWhiteSpace(settings.ClusterContents) ? settings.ClusterContents.Trim() : null;
				}

				//REVIEW: what to do if BOTH are specified? write the content to the specified path instead of a temp file ??
				if (!string.IsNullOrWhiteSpace(clusterFilePath))
				{
					options.ConnectionOptions.ClusterFile = clusterFilePath.Trim();
				}
				else if (!string.IsNullOrWhiteSpace(clusterFileContents))
				{
					if (options.ApiVersion >= 720)
					{ // we can use fdb_create_database_from_connection_string which does not require a file on disk
						options.ConnectionOptions.ConnectionString = clusterFileContents;
					}
					else
					{ // unfortunately, we need to store the content of the connection string into a temporary cluster file
						//HACKHACK: BUGBUG: TODO: we need to find a proper location for this file, and which will not conflict with other processes!
						clusterFilePath = Path.GetFullPath("local-" + connectionName + ".cluster");
						File.WriteAllText(clusterFilePath, clusterFileContents);
						options.ConnectionOptions.ClusterFile = clusterFilePath;
					}
				}
				else
				{
					// use system default cluster file
					options.ConnectionOptions.ClusterFile = null;
				}

				// Root=/path/to/root
				var rootPathLiteral = cnx != null && cnx.ContainsKey("Root") ? ((string?) cnx["Root"])?.Trim() : !string.IsNullOrWhiteSpace(settings.Root) ? settings.Root.Trim() : null;
				if (!string.IsNullOrWhiteSpace(rootPathLiteral))
				{
					if (!FdbPath.TryParse(rootPathLiteral, out var rootPath))
					{
						throw new InvalidOperationException("Invalid Root Path parameter");
					}
					options.ConnectionOptions.Root = rootPath;
				}

				// ReadOnly=(true|false) (default: false)
				if (cnx != null && cnx.ContainsKey("ReadOly"))
				{
					options.ConnectionOptions.ReadOnly = !string.Equals((string) cnx["ReadOnly"], "true", StringComparison.OrdinalIgnoreCase);
				}
				else
				{
					options.ConnectionOptions.ReadOnly = settings.ReadOnly ?? false;
				}

				// ClusterVersion=7.2.5
				var clusterVersionLiteral = cnx != null && cnx.ContainsKey("ClusterVersion") ? ((string?) cnx["ClusterVersion"])?.Trim() : !string.IsNullOrWhiteSpace(settings.ClusterVersion) ? settings.ClusterVersion.Trim() : null;
				if (clusterVersionLiteral != null)
				{
					if (!Version.TryParse(clusterVersionLiteral, out var clusterVersion))
					{
						throw new InvalidOperationException("Invalid Cluster Version parameter");
					}

					//TODO: how can we use the clusterVersion?
					// => ideally we would like to be able to select the correct FDB C client dll that is compatible,
					//    but we would need to add support for multiversion clients to the binding!
				}

				// DefaultTimeout=(seconds)
				if (cnx != null && cnx.ContainsKey("DefaultTimeout"))
				{
					if (!double.TryParse((string) cnx["DefaultTimeout"], out var seconds))
					{
						throw new InvalidOperationException("Malformed default timeout");
					}
					if (seconds < 0)
					{
						throw new InvalidOperationException("Default timeout must be a positive value");
					}
					options.ConnectionOptions.DefaultTimeout = TimeSpan.FromSeconds(seconds);
				}
				else if (settings.DefaultTimeout != null)
				{
					options.ConnectionOptions.DefaultTimeout = settings.DefaultTimeout.Value;
				}

				// DefaultRetryLimit=(number)
				if (cnx != null && cnx.ContainsKey("DefaultRetryLimit"))
				{
					if (!int.TryParse((string) cnx["DefaultRetryLimit"], out var count))
					{
						throw new InvalidOperationException("Malformed default retry limit");
					}
					if (count < 0)
					{
						throw new InvalidOperationException("Default retry limit must be a positive value");
					}
					options.ConnectionOptions.DefaultRetryLimit = count;
				}
				else if (settings.DefaultRetryLimit != null)
				{
					options.ConnectionOptions.DefaultRetryLimit = settings.DefaultRetryLimit.Value;
				}

				// DefaultTracing=(flags)
				if (cnx != null && cnx.ContainsKey("DefaultTracing"))
				{
					if (!int.TryParse((string) cnx["DefaultTracing"], out var count))
					{
						throw new InvalidOperationException("Malformed default tracing options");
					}
					if (count < 0)
					{
						throw new InvalidOperationException("Default tracing options must be a positive value");
					}
					options.ConnectionOptions.DefaultTracing = (FdbTracingOptions) count;
				}
				else if (settings.DefaultTracing != null)
				{
					options.ConnectionOptions.DefaultTracing = (FdbTracingOptions) settings.DefaultTracing.Value;
				}

				// LogSessionId=(string)
				if (cnx != null && cnx.ContainsKey("LogSessionId"))
				{
					var logSessionId = ((string?) cnx["LogSessionId"])?.Trim() ?? "";
					if (logSessionId.Length > 1024)
					{
						throw new InvalidOperationException("Log Session ID is too large");
					}
					options.DefaultLogOptions.SessionId = logSessionId;
					options.DefaultLogOptions.Origin = $"{Assembly.GetEntryAssembly()?.GetName().Name}_{Environment.ProcessId}";
				}

				// run additional custom configuration
				configureProvider?.Invoke(options);
			});

			if (!settings.DisableTracing)
			{
				builder.Services.AddOpenTelemetry()
					.WithTracing(traceBuilder => traceBuilder.AddFoundationDbInstrumentation());
			}

			if (!settings.DisableHealthChecks)
			{
				var check = new HealthCheckRegistration(
					FdbClientInstrumentation.HealthCheckName,
					sp =>
					{
						try
						{
							// if the IConnection can't be resolved, make a health check that will fail
							var options = new FdbHealthCheckOptions()
							{
								Provider = sp.GetRequiredService<IFdbDatabaseProvider>(),
							};
							return new FdbHealthCheck(options);
						}
						catch (Exception ex)
						{
							return new FailedHealthCheck(ex);
						}
					},
					failureStatus: default,
					tags: default);

				var healthCheckKey = $"Aspire.HealthChecks.{check.Name}";
				if (!builder.Properties.ContainsKey(healthCheckKey))
				{
					builder.Properties[healthCheckKey] = true;
					builder.Services.AddHealthChecks().Add(check);
				}
			}

			if (!settings.DisableMetrics)
			{
				builder.Services.AddOpenTelemetry()
					.WithMetrics((meterBuilder) => meterBuilder.AddFoundationDbInstrumentation());
			}

			return builder;
		}

		private sealed class FailedHealthCheck : IHealthCheck
		{
			private readonly Exception Error;

			public FailedHealthCheck(Exception error)
			{
				Error = error;
			}

			public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
				!cancellationToken.IsCancellationRequested
					? Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, exception: Error))
					: Task.FromCanceled<HealthCheckResult>(cancellationToken);
		}

	}

}
