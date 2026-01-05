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
		/// <para>If <see langword="null"/>, pre-loading of the native C API library will be disabled, and the CLR will handle the binding of the library.</para>
		/// <para>If <see cref="string.Empty">empty</see>, pre-loading of the native C API library will be enabled, and the operating system will handle the binding of the library.</para>
		/// <para>If not empty, the native C API library will be pre-loaded using the specified path. If the file does not exist, is not readable, or is corrupted, the startup will fail.</para>
		/// </remarks>
		public string? NativeLibraryPath { get; set; } = string.Empty;

		/// <summary>If not null, log handler that will be applied to all transactions</summary>
		public Action<FdbTransactionLog>? DefaultLogHandler { get; set; }

		/// <summary>Default logging options</summary>
		public FdbLoggingOptions DefaultLogOptions { get; set; } = new();

	}

}
