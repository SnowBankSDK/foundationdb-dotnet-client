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

namespace FoundationDB.Tools
{
	using System.Collections;
	using System.Data.Common;
	using System.Globalization;

	/// <summary>Connection settings of a FoundationDB cluster, read from the connection string that .NET Aspire injects into a process</summary>
	/// <remarks>
	/// <para>An Aspire AppHost injects <c>ConnectionStrings__{name}</c> into every process that references a FoundationDB resource (<c>WithReference(fdb)</c>).</para>
	/// <para>The value is a list of <c>key=value</c> pairs: <c>ApiVersion</c>, <c>Root</c>, <c>ClusterFile</c> or <c>ClusterFileContents</c>, and a few optional settings.</para>
	/// <para>The command-line tools use this type to implement their <c>--aspire</c> option.</para>
	/// </remarks>
	internal sealed record FdbAspireConnection
	{

		/// <summary>Prefix of the environment variables that hold the connection strings injected by Aspire</summary>
		public const string EnvironmentPrefix = "ConnectionStrings__";

		/// <summary>Name of the Aspire resource (the part after <see cref="EnvironmentPrefix"/>)</summary>
		public required string Name { get; init; }

		/// <summary>API version requested by the resource, if specified</summary>
		public int? ApiVersion { get; init; }

		/// <summary>Root path of the applications that reference the resource, if specified</summary>
		public FdbPath? Root { get; init; }

		/// <summary>Path of a cluster file, if specified</summary>
		public string? ClusterFile { get; init; }

		/// <summary>Contents of the cluster file (<c>description:id@ip:port,...</c>), if specified</summary>
		public string? ClusterFileContents { get; init; }

		/// <summary>Version of the cluster (ex: <c>7.3.54</c>), if specified</summary>
		/// <remarks>A native client talks only to clusters of its own major.minor version.</remarks>
		public Version? ClusterVersion { get; init; }

		/// <summary>Path of the native client library to load, if specified</summary>
		/// <remarks>The AppHost names the native client of the branch of the cluster, for the tools that it starts.</remarks>
		public string? NativeLibrary { get; init; }

		/// <summary>Finds the FoundationDB connection string injected by Aspire in the current environment</summary>
		/// <param name="name">Name of the Aspire resource, or <c>null</c> to select the only FoundationDB connection string found</param>
		/// <exception cref="InvalidOperationException">The environment holds no matching FoundationDB connection string, or more than one when <paramref name="name"/> is <c>null</c>. The message tells the user how to fix it.</exception>
		public static FdbAspireConnection Resolve(string? name) => Resolve(name, Environment.GetEnvironmentVariables());

		/// <summary>Finds the FoundationDB connection string injected by Aspire in the given set of environment variables</summary>
		/// <param name="name">Name of the Aspire resource, or <c>null</c> to select the only FoundationDB connection string found</param>
		/// <param name="environment">Environment variables to search</param>
		/// <exception cref="InvalidOperationException">The environment holds no matching FoundationDB connection string, or more than one when <paramref name="name"/> is <c>null</c>. The message tells the user how to fix it.</exception>
		public static FdbAspireConnection Resolve(string? name, IDictionary environment)
		{
			var candidates = new List<FdbAspireConnection>();
			foreach (DictionaryEntry entry in environment)
			{
				if (entry.Key is not string key || !key.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase) || entry.Value is not string value)
				{
					continue;
				}

				var resourceName = key.Substring(EnvironmentPrefix.Length);
				// Aspire also injects a portable alias with '_' in place of '-' (ex: "my-db" => "my_db"), both must match the name
				if (name != null && !string.Equals(resourceName, name, StringComparison.OrdinalIgnoreCase) && !string.Equals(resourceName, name.Replace('-', '_'), StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (TryParse(resourceName, value) is { } connection)
				{
					candidates.Add(connection);
				}
			}

			// the portable alias of a name has the same value as the original, keep only one of each
			var distinct = candidates.DistinctBy(c => (c.ApiVersion, c.Root, c.ClusterFile, c.ClusterFileContents, c.ClusterVersion, c.NativeLibrary)).ToList();

			if (distinct.Count == 1)
			{
				return distinct[0];
			}

			if (distinct.Count == 0)
			{
				throw new InvalidOperationException(
					name != null
						? $"No FoundationDB connection string named '{name}' was found in the environment (expected variable '{EnvironmentPrefix}{name}').{Environment.NewLine}{Hint}"
						: $"No FoundationDB connection string was found in the environment (expected a variable named '{EnvironmentPrefix}<resource>').{Environment.NewLine}{Hint}"
				);
			}

			throw new InvalidOperationException(
				$"Found {distinct.Count} FoundationDB connection strings in the environment ({string.Join(", ", distinct.Select(c => c.Name))}).{Environment.NewLine}"
				+ "Specify the name of the Aspire resource to use, for example: --aspire " + distinct[0].Name
			);
		}

		private const string Hint =
			"The --aspire option reads the connection string that an Aspire AppHost injects into the processes that reference a FoundationDB resource. "
			+ "Start this tool from the AppHost (for example with the FdbShell command of the fdb resource in the dashboard), "
			+ "or connect directly with --docker <port> or --connfile <path>.";

		/// <summary>Parses the value of a connection string injected by Aspire</summary>
		/// <returns>The connection settings, or <c>null</c> if the value is not a FoundationDB connection string</returns>
		/// <exception cref="InvalidOperationException">The value is a FoundationDB connection string with an invalid <c>ApiVersion</c> or <c>Root</c>.</exception>
		internal static FdbAspireConnection? TryParse(string name, string value)
		{
			DbConnectionStringBuilder builder;
			try
			{
				builder = new DbConnectionStringBuilder() { ConnectionString = value };
			}
			catch (ArgumentException)
			{ // not a list of key=value pairs, so not one of ours
				return null;
			}

			string? clusterFile = GetString(builder, "ClusterFile");
			string? clusterFileContents = GetString(builder, "ClusterFileContents");
			if (clusterFile == null && clusterFileContents == null)
			{ // another kind of resource (database, cache, ...)
				return null;
			}

			int? apiVersion = null;
			if (GetString(builder, "ApiVersion") is { } apiVersionLiteral)
			{
				if (!int.TryParse(apiVersionLiteral, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) || version <= 0)
				{
					throw new InvalidOperationException($"The connection string '{EnvironmentPrefix}{name}' has an invalid ApiVersion: '{apiVersionLiteral}'.");
				}
				apiVersion = version;
			}

			FdbPath? root = null;
			if (GetString(builder, "Root") is { } rootLiteral)
			{
				if (!FdbPath.TryParse(rootLiteral, out var path))
				{
					throw new InvalidOperationException($"The connection string '{EnvironmentPrefix}{name}' has an invalid Root: '{rootLiteral}'.");
				}
				root = path;
			}

			Version? clusterVersion = null;
			if (GetString(builder, "ClusterVersion") is { } clusterVersionLiteral)
			{
				if (!Version.TryParse(clusterVersionLiteral, out clusterVersion))
				{
					throw new InvalidOperationException($"The connection string '{EnvironmentPrefix}{name}' has an invalid ClusterVersion: '{clusterVersionLiteral}'.");
				}
			}

			return new()
			{
				Name = name,
				ApiVersion = apiVersion,
				Root = root,
				ClusterFile = clusterFile,
				ClusterFileContents = clusterFileContents,
				ClusterVersion = clusterVersion,
				NativeLibrary = GetString(builder, "NativeLibrary"),
			};
		}

		private static string? GetString(DbConnectionStringBuilder builder, string key)
			=> builder.TryGetValue(key, out var value) && value is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

	}

}
