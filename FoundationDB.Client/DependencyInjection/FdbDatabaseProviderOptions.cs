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
	using FoundationDB.Client;
	using FoundationDB.Filters.Logging;

	[PublicAPI]
	public record FdbDatabaseProviderOptions
	{

		/// <summary>Selected API Version</summary>
		public int ApiVersion { get; set; }

		public FdbConnectionOptions ConnectionOptions { get; set;} = new();

		/// <summary>Specifices whether we should automatically connect to the cluster, or if <see cref="IFdbDatabaseProvider.Start"/> must be called explicitly</summary>
		/// <remarks>
		/// <para>If <see langword="true"/> (by default), the first attempt to get the database instance will start the connection, if it is not already connected.</para>
		/// <para>If <see langword="false"/>, the application must call <see cref="IFdbDatabaseProvider.Start"/> explicitely sometimes during startup</para>
		/// <para>Note that if autostart is enabled, the availability of the cluster will not be observed until the first transaction is started which could hide connectivity issues until later during the process lifetime.</para>
		/// </remarks>
		public bool AutoStart { get; set; } = true;

		/// <summary>Specifies whether the network loop should be automatically stopped when the database provider is disposed.</summary>
		/// <remarks>
		/// <para>If <see langword="true"/>, disposing the database provider singleton will also stop the Network thread of the FoundationDB client and ensure any pending work is aborted safely.</para>
		/// <para>If <see langword="false"/> (default), disposing the database provider singleton will let the Network thread running (if it has been started).</para>
		/// <para>Please note that, once stopped, it is impossible to restart the Network thread. This is not a concern for traditionnal web host or API process, but prevents any other use cases where the provider must be restarted multiple times, like during unit testing.</para>
		/// </remarks>
		public bool AutoStop { get; set; }
		//note: for now it is opt-in, because it would break all unit tests.

		/// <summary>Overrides the path to the native library (aka "fdb_c.dll", "fdb_c.so", ...) that must be used.</summary>
		/// <remarks>
		/// <para>If <see langword="null"/> (the default), the library is loaded from the locations where the <c>FoundationDB.Client.Native</c> package deploys it, then from the location of the official client installer. See <see cref="Fdb.Options.NativeLibPath"/> for the list. The startup fails with the list of these paths when none exists.</para>
		/// <para>If <see cref="string.Empty">empty</see>, the runtime and the operating system search for the library. <see cref="UseSystemNativeClient"/> sets this value and explains the risk.</para>
		/// <para>If not empty, the native C API library will be pre-loaded using the specified path. If the file does not exist, is not readable, or is corrupted, the startup will fail.</para>
		/// </remarks>
		public string? NativeLibraryPath { get; set; }

		/// <summary>Lets the runtime and the operating system search for the native library, instead of the fixed locations of the default</summary>
		/// <returns>The same options</returns>
		/// <remarks>
		/// <para>Use it for a host where the library is installed in a folder that the default does not check, and only found through <c>PATH</c> (Windows) or <c>LD_LIBRARY_PATH</c> (Linux).</para>
		/// <para>The search visits folders that other users or programs may be able to write to, and loads the first file named like the native library. A library planted in one of these folders runs inside the process. Prefer the <c>FoundationDB.Client.Native</c> package, or set <see cref="NativeLibraryPath"/> to the full path of the library.</para>
		/// </remarks>
		public FdbDatabaseProviderOptions UseSystemNativeClient()
		{
			this.NativeLibraryPath = string.Empty;
			return this;
		}

		/// <summary>Network options that the provider applies before the network thread starts, in the order they were added</summary>
		internal List<(FdbNetworkOption Option, object? Value)> NetworkOptions { get; } = [];

		/// <summary>Sets a network option that takes no parameter</summary>
		/// <param name="option">Option to set</param>
		/// <returns>The same options</returns>
		/// <remarks>
		/// <para>The provider applies the option when it starts the network thread, after the trace and TLS options. Options are applied in the order of the calls, and an option set twice is applied twice: <see cref="FdbNetworkOption.ExternalClientLibrary"/> loads one library per call.</para>
		/// <para>The network thread starts once per process, so the options of the first provider that starts apply to the whole process.</para>
		/// </remarks>
		public FdbDatabaseProviderOptions SetNetworkOption(FdbNetworkOption option)
		{
			this.NetworkOptions.Add((option, null));
			return this;
		}

		/// <summary>Sets a network option that takes a string parameter, for example a path</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the option</param>
		/// <returns>The same options</returns>
		/// <remarks>
		/// <para>The multi-version client uses these options: <see cref="FdbNetworkOption.ExternalClientLibrary"/> and <see cref="FdbNetworkOption.ExternalClientDirectory"/> load other versions of the native library next to the primary one. All loaded libraries share one API version, so a 7.3 library next to a 7.4 one requires an <see cref="ApiVersion"/> of 730 or lower.</para>
		/// <para>See <see cref="SetNetworkOption(FdbNetworkOption)"/> for when and in which order the options are applied.</para>
		/// </remarks>
		public FdbDatabaseProviderOptions SetNetworkOption(FdbNetworkOption option, string value)
		{
			Contract.NotNull(value);
			this.NetworkOptions.Add((option, value));
			return this;
		}

		/// <summary>Sets a network option that takes an integer parameter</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the option</param>
		/// <returns>The same options</returns>
		/// <remarks>See <see cref="SetNetworkOption(FdbNetworkOption)"/> for when and in which order the options are applied.</remarks>
		public FdbDatabaseProviderOptions SetNetworkOption(FdbNetworkOption option, long value)
		{
			this.NetworkOptions.Add((option, value));
			return this;
		}

		/// <summary>If not null, log handler that will be applied to all transactions</summary>
		public Action<FdbTransactionLog>? DefaultLogHandler { get; set; }

		/// <summary>Default logging options</summary>
		public FdbLoggingOptions DefaultLogOptions { get; set; } = new();

	}

}
