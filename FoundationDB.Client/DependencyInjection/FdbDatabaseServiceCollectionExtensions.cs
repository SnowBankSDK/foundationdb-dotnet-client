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

namespace FoundationDB.DependencyInjection
{
	using System.IO;
	using FoundationDB.Client;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Options;

	/// <summary>Extension methods for registering a <see cref="IFdbDatabaseProvider"/> service</summary>
	[PublicAPI]
	public static class FdbDatabaseServiceCollectionExtensions
	{

		/// <summary>Add a <see cref="IFdbDatabaseProvider">FoundationDB database provider</see> to the list of services</summary>
		/// <param name="services">Service collection</param>
		/// <param name="apiVersion">Selected API version level</param>
		/// <param name="configure">Optional callback used to configure the database provider options</param>
		/// <returns>FoundationDB database provider builder</returns>
		public static IFdbDatabaseProviderBuilder AddFoundationDb(this IServiceCollection services, int apiVersion, Action<FdbDatabaseProviderOptions>? configure = null)
		{
			Contract.NotNull(services);
			Contract.Positive(apiVersion);

			if (apiVersion == 0)
			{ // most probably the API level was not properly configured (Fdb.UseApiVersion not called)
				throw new InvalidOperationException("You must specify a valid API version that will be used by this provider.");
			}

			return AddFoundationDb(services, (options) =>
			{
				options.ApiVersion = apiVersion;
				configure?.Invoke(options);
			});
		}

		/// <summary>Add a <see cref="IFdbDatabaseProvider">FoundationDB database provider</see> to the list of services</summary>
		/// <param name="services">Service collection</param>
		/// <param name="apiVersion">Selected API version level</param>
		/// <param name="clusterFile">Path to the cluster file, or <c>null</c> to use the system default location</param>
		/// <param name="configure">Optional callback used to configure the database provider options</param>
		/// <returns>FoundationDB database provider builder</returns>
		public static IFdbDatabaseProviderBuilder AddFoundationDb(this IServiceCollection services, int apiVersion, string? clusterFile, Action<FdbDatabaseProviderOptions>? configure = null)
		{
			Contract.NotNull(services);
			Contract.GreaterThan(apiVersion, 0, nameof(apiVersion));

			return AddFoundationDb(services, (options) =>
			{
				options.ApiVersion = apiVersion;
				options.ConnectionOptions.ClusterFile = clusterFile;
				configure?.Invoke(options);
			});
		}

		/// <summary>Add a <see cref="IFdbDatabaseProvider">FoundationDB database provider</see> to the list of services</summary>
		/// <param name="services">Service collection</param>
		/// <param name="configure">Callback used to configure the database provider options</param>
		/// <returns>FoundationDB database provider builder</returns>
		public static IFdbDatabaseProviderBuilder AddFoundationDb(this IServiceCollection services, Action<FdbDatabaseProviderOptions> configure)
		{
			Contract.NotNull(services);
			Contract.NotNull(configure);

			services.AddSingleton<IFdbDatabaseProvider, FdbDatabaseProvider>();
			services.AddOptionsWithValidateOnStart<FdbDatabaseProviderOptions, FdbDatabaseProviderOptionsValidator>()
				.Configure(configure);

			return new FdbDefaultDatabaseProviderBuilder(services);
		}

	}

	/// <summary>Validates an instance of <see cref="FdbDatabaseProviderOptions"/></summary>
	internal sealed class FdbDatabaseProviderOptionsValidator : IValidateOptions<FdbDatabaseProviderOptions>
	{

		public ValidateOptionsResult Validate(string? name, FdbDatabaseProviderOptions options)
		{
			var res = new ValidateOptionsResultBuilder();

			// ApiVersion
			if (options.ApiVersion == 0)
			{ // must be specified
				res.AddError("API version must be specified", nameof(options.ApiVersion));
			}
			else if (options.ApiVersion < 0)
			{ // cannot be negative
				res.AddError("API version must be a positive value", nameof(options.ApiVersion));
			}
			else if (options.ApiVersion < Fdb.MinSafeApiVersion)
			{ // cannot be less than minimum supported
				res.AddError($"API version can be less than the minimum supported API version of {Fdb.MinSafeApiVersion}", nameof(options.ApiVersion));
			}

			if (!string.IsNullOrEmpty(options.ConnectionOptions.ClusterFile))
			{ // must be a valid path!

				//note: at the moment I don't know of any method in the BCL that checks a path for validity (!= existence), but calling the ctor of FileInfo will throw if there is something is wrong with it!
				FileInfo? fi;
				try { fi = new FileInfo(options.ConnectionOptions.ClusterFile); } catch { fi = null; }

				if (fi == null)
				{
					res.AddError("Invalid or malformed cluster file path", nameof(options.ConnectionOptions.ClusterFile));
				}
				//TODO: check if the file exists?
			}

			if (!string.IsNullOrEmpty(options.ConnectionOptions.ConnectionString))
			{
				//TODO: check that it is a valid connection string!

				if (!string.IsNullOrEmpty(options.ConnectionOptions.ClusterFile))
				{
					res.AddError($"Both {nameof(FdbConnectionOptions.ConnectionString)} and {nameof(FdbConnectionOptions.ClusterFile)} cannot be set at the same time", nameof(options.ConnectionOptions.ConnectionString));
				}
			}

			if (!string.IsNullOrEmpty(options.NativeLibraryPath))
			{
				//note: at the moment I don't know of any method in the BCL that checks a path for validity (!= existence), but calling the ctor of FileInfo will throw if there is something is wrong with it!
				FileInfo? fi;
				try { fi = new FileInfo(options.NativeLibraryPath); } catch { fi = null; }

				if (fi == null)
				{
					res.AddError("Invalid or malformed native library path", nameof(options.NativeLibraryPath));
				}
				//TODO: check if the file exists?
			}


			if (options.ConnectionOptions.DefaultMaxRetryDelay < 0)
			{ // cannot be negative
				res.AddError("Default maximum retry delay must be a positive value", nameof(options.ConnectionOptions.DefaultMaxRetryDelay));
			}
			if (options.ConnectionOptions.DefaultRetryLimit < 0)
			{ // cannot be negative
				res.AddError("Default maximum retry limit must be a positive value", nameof(options.ConnectionOptions.DefaultRetryLimit));
			}
			if (options.ConnectionOptions.DefaultTimeout < TimeSpan.Zero)
			{ // cannot be negative
				res.AddError("Default transaction timeout must be a positive value", nameof(options.ConnectionOptions.DefaultTimeout));
			}

			return res.Build();
		}

	}

}
