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

	/// <summary>Defines a set of options for the network thread</summary>
	[PublicAPI]
	public enum FdbNetworkOption
	{
		/// <summary>None</summary>
		None = 0,

		/// <summary>Deprecated</summary>
		[Obsolete]
		LocalAddress = 10,

		/// <summary>Enable the object serializer for network communication</summary>
		/// <remarks>0 is false, every other value is true</remarks>
		UseObjectSerializer = 11,

		/// <summary>Deprecated</summary>
		[Obsolete]
		ClusterFile = 20, //TODO: should we remove this? We don't support older API versions ....

		/// <summary>Enables trace output to a file in a directory of the clients choosing.</summary>
		/// <remarks>Parameter: (String) path to output directory (or NULL for current working directory)</remarks>
		TraceEnable = 30,

		/// <summary>Sets the maximum size in bytes of a single trace output file.
		/// This value should be in the range ``[0, long.MaxValue]``.
		/// If the value is set to 0, there is no limit on individual file size.
		/// The default is a maximum size of 10,485,760 bytes.
		/// </summary>
		/// <remarks>Parameter: (Int64) max size of a single trace output file</remarks>
		TraceRollSize = 31,

		/// <summary>Sets the maximum size of all the trace output files put together.</summary>
		/// <remarks>
		/// <para>Parameter: (Int64) max total size of trace files. This value should be in the range ``[0, long.MaxValue]``.</para>
		/// <para>If the value is set to 0, there is no limit on the total size of the files.</para>
		/// <para>The default is a maximum size of 104,857,600 bytes.</para>
		/// <para>If the default roll size is used, this means that a maximum of 10 trace files will be written at a time.</para>
		/// </remarks>
		TraceMaxLogsSize = 32,

		/// <summary>Sets the <c>LogGroup</c> attribute with the specified value for all events in the trace output files.</summary>
		/// <remarks>Parameter: (String) value of the LogGroup attribute. The default log group is <c>default</c>.</remarks>
		TraceLogGroup = 33,

		/// <summary>Select the format of the log files.</summary>
		/// <remarks>
		/// <para>Parameter: (String) format of trace files: <c>xml</c> (the default) and <c>json</c> are supported.</para>
		/// </remarks>
		TraceFormat = 34,

		/// <summary>Select clock source for trace files.</summary>
		/// <remarks>Parameter: (String) Trace clock source: <c>now</c> (the default) or <c>realtime</c> are supported.</remarks>
		TraceClockSource = 35,

		/// <summary>Once provided, this string will be used to replace the port/PID in the log file names.</summary>
		/// <remarks>
		/// <para>Parameter: (String) The identifier that will be part of all trace file names</para>
		/// </remarks>
		TraceFileIdentifier = 36,

		/// <summary>Use the same base trace file name for all client threads as it did before version 7.2. The current default behavior is to use distinct trace file names for client threads by including their version and thread index.</summary>
		TraceShareAmongClientThreads = 37,

		/// <summary>Initialize trace files on network setup, determine the local IP later. Otherwise, tracing is initialized when opening the first database.</summary>
		TraceInitializeOnSetup = 38,

		/// <summary>Set file suffix for partially written log files.</summary>
		/// <remarks>
		/// <para>Parameter: (String) Append this suffix to partially written log files. When a log file is complete, it is renamed to remove the suffix. No separator is added between the file and the suffix. If you want to add a file extension, you should include the separator - e.g. '.tmp' instead of 'tmp' to add the 'tmp' extension.</para>
		/// </remarks>
		TracePartialFileSuffix = 39,

		/// <summary>Set internal tuning or debugging knobs </summary>
		/// <remarks>Parameter: (String) knob_name=knob_value</remarks>
		Knob = 40,

		/// <summary>Set the TLS plugin to load.
		/// This option, if used, must be set before any other TLS options
		/// Parameter: (String) file path or linker-resolved name
		/// </summary>
		[Obsolete("This option is deprecated since v6.0")]
		TlsPlugin = 41,

		/// <summary>Set the certificate chain</summary>
		/// <remarks>Parameter: (Bytes) certificates</remarks>
		TlsCertBytes = 42,

		/// <summary>Set the file from which to load the certificate chain</summary>
		/// <remarks>Parameter: (String) File path</remarks>
		TlsCertPath = 43,

		//note: there is no entry for 44

		/// <summary>Set the private key corresponding to your own certificate</summary>
		/// <remarks>Parameter: (Bytes) Key</remarks>
		TlsKeyBytes = 45,

		/// <summary>Set the file from which to load the private key corresponding to your own certificate</summary>
		/// <remarks>Parameter: (String) File path</remarks>
		TlsKeyPath = 46,

		/// <summary>Set the peer certificate field verification criteria</summary>
		/// <remarks>Parameter: (Bytes) Verification pattern</remarks>
		TlsVerifyPeers = 47,

		BuggifyEnable = 48,

		BuggifyDisable = 49,

		/// <summary>Set the probability of a BUGGIFY section being active for the current execution.
		/// Only applies to code paths first traversed AFTER this option is changed.</summary>
		BuggifySectionActivatedProbability = 50,

		/// <summary>Set the probability of an active BUGGIFY section being fired</summary>
		BuggifySectionFiredProbability = 51,

		/// <summary>Set the ca bundle.</summary>
		TlsCaBytes = 52,

		/// <summary>Set the file from which to load the certificate authority bundle.</summary>
		TlsCaPath = 53,

		/// <summary>Set the passphrase for encrypted private key. Password should be set before setting the key for the password to be used.</summary>
		/// <remarks>Parameter: (String) key passphrase</remarks>
		TlsPassword = 54,

		/// <summary>Prevent client from connecting to a non-TLS endpoint by throwing network connection failed error.</summary>
		TlsDisablePlaintextConnection = 55,

		/// <summary>Disables the multi-version client API and instead uses the local client directly. Must be set before setting up the network.</summary>
		DisableMultiVersionClientApi = 60,

		/// <summary>If set, callbacks from external client libraries can be called from threads created by the FoundationDB client library.
		/// Otherwise, callbacks will be called from either the thread used to add the callback or the network thread.
		/// Setting this option can improve performance when connected using an external client, but may not be safe to use in all environments.
		/// Must be set before setting up the network.</summary>
		/// <remarks>WARNING: This feature is considered experimental at this time.</remarks>
		CallbacksOnExternalThreads = 61,

		/// <summary>Adds an external client library for use by the multi-version client API. Must be set before setting up the network.</summary>
		ExternalClientLibrary = 62,

		/// <summary>Searches the specified path for dynamic libraries and adds them to the list of client libraries for use by the multi-version client API. Must be set before setting up the network.</summary>
		ExternalClientDirectory = 63,

		/// <summary>Prevents connections through the local client, allowing only connections through externally loaded client libraries. Intended primarily for testing.</summary>
		DisableLocalClient = 64,

		/// <summary>Spawns multiple worker threads for each version of the client that is loaded.</summary>
		/// <remarks>
		/// <para>Parameter: (Int64) Number of client threads to be spawned. Each cluster will be serviced by a single client thread.</para>
		/// <para>Setting this to a number greater than one implies <see cref="DisableLocalClient"/>.</para>
		/// </remarks>
		ClientThreadsPerVersion = 65,

		/// <summary>Adds an external client library to be used with a future version protocol.</summary>
		/// <remarks>
		/// <para>Parameter: (String) path to client library</para>
		/// <para>This option can be used testing purposes only!</para>
		/// </remarks>
		FutureVersionClientLibrary = 66,

		/// <summary>Retain temporary external client library copies that are created for enabling multi-threading.</summary>
		RetainClientLibraryCopies = 67,

		/// <summary>Ignore the failure to initialize some of the external clients</summary>
		IgnoreExternalClientFailures = 68,

		/// <summary>Fail with an error if there is no client matching the server version the client is connecting to.</summary>
		FailIncompatibleClient = 69,

		/// <summary>Disables logging of client statistics, such as sampled transaction activity.</summary>
		DisableClientStatisticsLogging = 70,

		/// <summary>Enables debugging feature to perform run loop profiling.</summary>
		/// <remarks>
		/// <para>Requires trace logging to be enabled.</para>
		/// <para>WARNING: this feature is not recommended for use in production.</para>
		/// </remarks>
		EnableRunLoopProfiling = 71,

		/// <summary>Enables debugging feature to perform slow task profiling.</summary>
		/// <remarks>
		/// <para>Requires trace logging to be enabled.</para>
		/// <para>WARNING: this feature is not recommended for use in production.</para>
		/// </remarks>
		[Obsolete("This option has been renamed to EnableRunLoopProfiling")]
		EnableSlowTaskProfiling = 71,

		/// <summary>Prevents the multi-version client API from being disabled, even if no external clients are configured.</summary>
		/// <remarks>This option is required to use GRV caching.</remarks>
		DisableClientBypass = 72,
		
		/// <summary>Enable client buggify - will make requests randomly fail (intended for client testing)</summary>
		ClientBuggifyEnable = 80,

		/// <summary>Disable client buggify</summary>
		ClientBuggifyDisable = 81,

		/// <summary>Set the probability of a CLIENT_BUGGIFY section being active for the current execution.</summary>
		ClientBuggifySectionActivatedProbability = 82,

		/// <summary>Set the probability of an active CLIENT_BUGGIFY section being fired. A section will only fire if it was activated</summary>
		ClientBuggifySectionFiredProbability = 83,

		/// <summary>Set a tracer to run on the client.</summary>
		/// <remarks>
		/// <para>Parameter: (String) Distributed tracer type. Choose from <c>none</c>, <c>log_file</c>, or <c>network_lossy</c></para>
		/// <para>Should be set to the same value as the tracer set on the server.</para>
		/// </remarks>
		DistributedClientTracer = 90,

		/// <summary>Sets the directory for storing temporary files created by FDB client, such as temporary copies of client libraries.</summary>
		/// <remarks>
		/// <para>Parameter: (String) Client directory for temporary files.</para>
		/// <para>Defaults to <c>/tmp</c> (on Linux)</para>
		/// </remarks>
		ClientTmpDir = 91,

		/// <summary>This option is set automatically to communicate the list of supported clients to the active client.</summary>
		SupportedClientVersions = 1000, // hidden

		/// <summary>This option is set automatically on all clients loaded externally using the multi-version API.</summary>
		ExternalClient = 1001, // hidden

		/// <summary>This option tells a child on a multi-version client what transport ID to use.</summary>
		/// <remarks>
		/// <para>Parameter: (Int64) Transport ID for the child connection</para>
		/// </remarks>
		ExternalClientTransportId = 1002, // hidden

	}

}
