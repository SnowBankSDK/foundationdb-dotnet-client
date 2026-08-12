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

// enable this to help debug native calls to fdbc.dll
//#define DEBUG_NATIVE_CALLS

namespace FoundationDB.Client.Native
{
	using System.IO;
	using System.Runtime.ExceptionServices;

	internal static unsafe partial class FdbNative
	{
		public const int FDB_API_MIN_VERSION = 610;
		public const int FDB_API_MAX_VERSION = 740;

		/// <summary>Name of the C API dll used for P/Invoking</summary>
		internal const string FDB_C_DLL = "fdb_c";

		/// <summary>Handle on the native FDB C API library</summary>
		private static readonly UnmanagedLibrary? FdbCLib;

		/// <summary>Exception that was thrown when we last tried to load the native FDB C library (or null if nothing wrong happened)</summary>
		private static readonly ExceptionDispatchInfo? LibraryLoadError;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void FdbNetworkThreadCompletionCallback(IntPtr parameter);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void FdbFutureCallback(IntPtr future, IntPtr parameter);

		/// <summary>Contain all the stubs to the methods exposed by the C API library</summary>
		[System.Security.SuppressUnmanagedCodeSecurity]
		internal static partial class NativeMethods
		{

			#region Core

#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_select_api_version_impl(int runtimeVersion, int headerVersion);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_select_api_version_impl(int runtimeVersion, int headerVersion);
#endif

			/// <summary>Returns <c>FDB_API_VERSION</c>, the current version of the FoundationDB C API.</summary>
			/// <returns>This is the maximum version that may be passed to <see cref="fdb_select_api_version_impl"/>.</returns>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial int fdb_get_max_api_version();
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern int fdb_get_max_api_version();
#endif

			/// <summary>Returns the build version, git commit hash and protocol version of the loaded native library</summary>
			/// <returns><c>"version,commit_hash,protocol"</c>. Ex.: <c>"7.1.29,1b2517abce552441e3d0ed8836d1cc3f40e61a2a,fdb00b071010000"</c></returns>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial byte* fdb_get_client_version();
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern byte* fdb_get_client_version();
#endif

			/// <summary>Returns a (somewhat) human-readable English message from an error code.</summary>
			/// <remarks>The return value is a statically allocated null-terminated string that must not be freed by the caller.</remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial byte* fdb_get_error(FdbError code);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern byte* fdb_get_error(FdbError code);
#endif

			/// <summary>Evaluates a predicate against an error code.</summary>
			/// <returns>True if the code matches the specified <paramref name="predicateTest">predicate</paramref></returns>
			/// <remarks>The predicate to run should be one of the codes listed by the <see cref="FdbErrorPredicate"/> enum. Sample predicates include <see cref="FdbErrorPredicate.Retryable"/>, which can be used to determine whether the error with the given code is a retryable error or not.</remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static partial bool fdb_error_predicate(FdbErrorPredicate predicateTest, FdbError code);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_error_predicate(FdbErrorPredicate predicateTest, FdbError code);
#endif

			#endregion

			#region Network

			/// <summary>Called to set network options.</summary>
			/// <remarks>
			/// If the given option is documented as taking a parameter, you must also pass a pointer to the parameter <paramref name="value"/> and the parameter values <paramref name="length"/>.
			/// If the option is documented as taking an <c>Int</c> parameter, value must point to a signed 64-bit integer (little-endian), and <paramref name="length"/> must be <c>8</c>.
			/// This memory only needs to be valid until <see cref="fdb_network_set_option"/> returns.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_network_set_option(FdbNetworkOption option, byte* value, int length);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_network_set_option(FdbNetworkOption option, byte* value, int length);
#endif

			/// <summary>Set up the network thread.</summary>
			/// <remarks>
			/// Must be called after <see cref="fdb_select_api_version_impl"/> (and zero or more calls to <see cref="fdb_network_set_option"/>) and before any other function in this API.
			/// <see cref="fdb_setup_network"/> can only be called once.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_setup_network();
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_setup_network();
#endif

			/// <summary>Register the given callback to run at the completion of the network thread</summary>
			/// <remarks>
			/// Must be called after <see cref="fdb_setup_network"/> and prior to <see cref="fdb_run_network"/> if called at all.
			/// If there are multiple network threads running (which might occur if one is running multiple versions of the client, for example), then the callback is invoked once on each thread.
			/// When the supplied function is called, the supplied <paramref name="parameter"/> is passed to it.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_add_network_thread_completion_hook(FdbNetworkThreadCompletionCallback hook, IntPtr parameter);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_add_network_thread_completion_hook(FdbNetworkThreadCompletionCallback hook, IntPtr parameter);
#endif

			/// <summary>Run the network loop on the current thread</summary>
			/// <remarks>
			/// Must be called after <see cref="fdb_setup_network"/> before any asynchronous functions in this API can be expected to complete.
			/// Unless your program is entirely event-driven based on results of asynchronous functions in this API and has no event loop of its own, you will want to invoke this function on an auxiliary thread (which it is your responsibility to create).
			/// This function will not return until <see cref="fdb_stop_network"/> is called by you or a serious error occurs.
			/// It is not possible to run more than one network thread, and the network thread cannot be restarted once it has been stopped.
			/// This means that once <see cref="fdb_run_network"/> has been called, it is not legal to call it again for the lifetime of the running program.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_run_network();
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_run_network();
#endif

			/// <summary>Signals the event loop invoked by <see cref="fdb_run_network"/> to terminate.</summary>
			/// <remarks>
			/// You must call this function and wait for <see cref="fdb_run_network"/> to return before allowing your program to exit, or else the behavior is undefined.
			/// This function may be called from any thread. Once the network is stopped it cannot be restarted during the lifetime of the running program.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_stop_network();
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_stop_network();
#endif

			#endregion

			#region Database

			/// <summary>Creates a new database connected the specified cluster.</summary>
			/// <remarks>
			/// The caller assumes ownership of the FDBDatabase object and must destroy it with <see cref="fdb_database_destroy"/>.
			/// A single client can use this function multiple times to connect to different clusters simultaneously, with each invocation requiring its own cluster file.
			/// To connect to multiple clusters running at different, incompatible versions, the multi-version client API must be used.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_create_database([MarshalAs(UnmanagedType.LPStr)] string? clusterFilePath, out DatabaseHandle database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			public static extern FdbError fdb_create_database([MarshalAs(UnmanagedType.LPStr)] string? clusterFilePath, out DatabaseHandle database);
#endif

			/// <summary>Creates a new database connected the specified cluster, using the specified connection string.</summary>
			/// <remarks>
			/// <para>The caller assumes ownership of the FDBDatabase object and must destroy it with <see cref="fdb_database_destroy"/>.</para>
			/// <para>A single client can use this function multiple times to connect to different clusters simultaneously, with each invocation requiring its own cluster file.</para>
			/// <para>To connect to multiple clusters running at different, incompatible versions, the multi-version client API must be used.</para>
			/// <para>Available since 720.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_create_database_from_connection_string([MarshalAs(UnmanagedType.LPStr)] string? connectionString, out DatabaseHandle database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			public static extern FdbError fdb_create_database_from_connection_string([MarshalAs(UnmanagedType.LPStr)] string? connectionString, out DatabaseHandle database);
#endif

			/// <summary>Destroys an FDBDatabase object.</summary>
			/// <remarks>
			/// It must be called exactly once for each successful call to <see cref="fdb_create_database"/>.
			/// This function only destroys a handle to the database  your database will be fine!
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_database_destroy(IntPtr database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_database_destroy(IntPtr database);
#endif

			/// <summary>Called to set an option on an <see cref="DatabaseHandle">FDBDatabase</see>.</summary>
			/// <remarks>
			/// If the given option is documented as taking a parameter, you must also pass a pointer to the parameter <paramref name="value"/> and the parameter values <paramref name="length"/>.
			/// If the option is documented as taking an Int parameter, <paramref name="value"/> must point to a signed 64-bit integer (little-endian), and <paramref name="length"/> must be <c>8</c>.
			/// This memory only needs to be valid until <see cref="fdb_database_set_option"/> returns.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_database_set_option(DatabaseHandle handle, FdbDatabaseOption option, byte* value, int length);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_set_option(DatabaseHandle handle, FdbDatabaseOption option, byte* value, int length);
#endif

			/// <summary>Creates a new transaction on the given database without using a tenant, meaning that it will operate on the entire database key-space.</summary>
			/// <remarks>The caller assumes ownership of the <see cref="TransactionHandle">FDBTransaction</see> object and must destroy it with <see cref="fdb_transaction_destroy"/>.</remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_database_create_transaction(DatabaseHandle database, out TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_create_transaction(DatabaseHandle database, out TransactionHandle transaction);
#endif

			//TODO: documentation! (added 7.0)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_database_reboot_worker(DatabaseHandle database, byte* address, int addressLength, [MarshalAs(UnmanagedType.Bool)] bool check, int duration);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_database_reboot_worker(DatabaseHandle database, byte* address, int addressLength, bool check, int duration);
#endif

			//TODO: documentation! (added 7.0)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_database_force_recovery_with_data_loss(DatabaseHandle database, byte* dcId, int dcIdLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_database_force_recovery_with_data_loss(DatabaseHandle database, byte* dcId, int dcIdLength);
#endif

			//TODO: documentation! (added 7.0)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_database_create_snapshot(DatabaseHandle database, byte* uid, int uidLength, byte* snapCommand, int snapCommandLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_database_create_snapshot(DatabaseHandle database, byte* uid, int uidLength, byte* snapCommand, int snapCommandLength);
#endif

			/// <summary>Returns a value where <see langword="0"/> indicates that the client is idle and <see langword="1"/> (or larger) indicates that the client is saturated.</summary>
			/// <remarks>
			/// <para>By default, this value is updated every second.</para>
			/// <para>Added in 700</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial double fdb_database_get_main_thread_busyness(DatabaseHandle database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern double fdb_database_get_main_thread_busyness(DatabaseHandle database);
#endif

			/// <summary>Returns the protocol version reported by the coordinator this client is connected to.</summary>
			/// <remarks>
			/// <para>If an expected version is non-zero, the future won't return until the protocol version is different from the expected version</para>
			/// <para>Note: this will never return if the server is running a protocol from FDB 5.0 or older</para>
			/// <para>Added in 700</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_database_get_server_protocol(DatabaseHandle database, ulong expectedVersion);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_database_get_server_protocol(DatabaseHandle database, ulong expectedVersion);
#endif

			//TODO: documentation! (added 7.1)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_database_open_tenant(DatabaseHandle database, byte* tenantName, int tenantNameLength, out TenantHandle handle);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_open_tenant(DatabaseHandle database, byte* tenantName, int tenantNameLength, out TenantHandle handle);
#endif

			//TODO: documentation! (added 7.3)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_database_get_client_status(DatabaseHandle database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_database_get_client_status(DatabaseHandle database);
#endif

			#endregion

			#region Tenant

			/// <summary>Destroys an FDBTenant object.</summary>
			/// <remarks>
			/// <para>It must be called exactly once for each successful call to fdb_database_create_tenant().</para>
			/// <para>This function only destroys a handle to the tenant -- the tenant and its data will be fine!</para>
			/// <para>Available since 710</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_tenant_destroy(IntPtr tenant);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_tenant_destroy(IntPtr tenant);
#endif

			/// <summary>Creates a new transaction on the given tenant.</summary>
			/// <param name="database"></param>
			/// <param name="transaction"></param>
			/// <returns></returns>
			/// <remarks>
			/// <para>This transaction will operate within the tenant's key-space and cannot access data outside the tenant.</para>
			/// <para>The caller assumes ownership of the <see cref="TransactionHandle">FDBTransaction</see> object and must destroy it with <see cref="fdb_transaction_destroy"/>.</para>
			/// <para>Available since 710</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_tenant_create_transaction(TenantHandle database, out TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_tenant_create_transaction(TenantHandle database, out TransactionHandle transaction);
#endif

			// added 7.3
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_tenant_get_id(TenantHandle tenant);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_tenant_get_id(TenantHandle tenant);
#endif

			#endregion

			#region Transaction

			/// <summary>Destroys an <see cref="TransactionHandle">FDBTransaction</see> object.</summary>
			/// <remarks>
			/// It must be called exactly once for each successful call to <see cref="fdb_database_create_transaction"/>.
			/// Destroying a transaction which has not had <see cref="fdb_transaction_commit"/> called implicitly rolls back the transaction (sets and clears do not take effect on the database).
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_destroy(IntPtr database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_destroy(IntPtr database);
#endif

			/// <summary>Called to set an option on an FDBTransaction.</summary>
			/// <remarks>
			/// If the given option is documented as taking a parameter, you must also pass a pointer to the parameter value and the parameter values length.
			/// If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8.
			/// This memory only needs to be valid until fdb_transaction_set_option() returns.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_transaction_set_option(TransactionHandle handle, FdbTransactionOption option, byte* value, int valueLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_set_option(TransactionHandle handle, FdbTransactionOption option, byte* value, int valueLength);
#endif

			/// <summary>Sets the snapshot read version used by a transaction.</summary>
			/// <remarks>
			/// This is not needed in simple cases.
			/// If the given version is too old, subsequent reads will fail with error_code_transaction_too_old;
			/// if it is too new, subsequent reads may be delayed indefinitely and/or fail with <see cref="FdbError"><c>error_code_future_version</c></see>.
			/// If any of <c>fdb_transaction_get_*()</c> have been called on this transaction already, the result is undefined.</remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_set_read_version(TransactionHandle handle, long version);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_set_read_version(TransactionHandle handle, long version);
#endif

			/// <summary>Gets the read version of the <paramref name="transaction"/> snapshot</summary>
			/// <returns>Returns an <see cref="FutureHandle"><c>FDBFuture</c></see> which will be set to the transaction snapshot read version.</returns>
			/// <remarks>
			/// <para>You must first wait for the <see cref="FutureHandle"><c>FDBFuture</c></see> to be ready, check for errors, call <see cref="fdb_future_get_int64"/> to extract the version into an <c>int64_t</c> that you provide, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// The transaction obtains a snapshot read version automatically at the time of the first call to <c>fdb_transaction_get_*()</c> (including this one) and (unless causal consistency has been deliberately compromised by transaction options) is guaranteed to represent all transactions which were reported committed before that call.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_read_version(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_read_version(TransactionHandle transaction);
#endif

			/// <summary>Reads a value from the database snapshot represented by <paramref name="transaction"/></summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the value of <paramref name="keyName"/> in the database.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_value"/> to extract the value, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>See <see cref="fdb_future_get_value"/> to see exactly how results are unpacked.</para>
			/// <para>If <paramref name="keyName"/> is not present in the database, the result is not an error, but a zero for <c>present</c> returned from that function.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get(TransactionHandle transaction, byte* keyName, int keyNameLength, [MarshalAs(UnmanagedType.Bool)] bool snapshot);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get(TransactionHandle transaction, byte* keyName, int keyNameLength, bool snapshot);
#endif

			/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="keyName"/> and its associated value.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to an array of strings.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_string_array"/> to extract the string array, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_addresses_for_key(TransactionHandle transaction, byte* keyName, int keyNameLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_addresses_for_key(TransactionHandle transaction, byte* keyName, int keyNameLength);
#endif

			/// <summary>Returns a list of keys that can split the given range into (roughly) equally sized chunks based on <paramref name="chunkSize"/>.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the list of split points.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_key_array"/> to extract the array, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>Added in 700</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_range_split_points(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, long chunkSize);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_range_split_points(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, long chunkSize);
#endif

			/// <summary>Returns an estimated byte size of the key range.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the estimated size of the key range given.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_int64"/> to extract the size, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_estimated_range_size_bytes(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_estimated_range_size_bytes(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength);
#endif

			/// <summary>Resolves a key selector against the keys in the database snapshot represented by <paramref name="transaction"/>.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the key in the database matching the key selector.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_key"/> to extract the key, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_key(TransactionHandle transaction, byte* keyName, int keyNameLength, [MarshalAs(UnmanagedType.Bool)] bool orEqual, int offset, [MarshalAs(UnmanagedType.Bool)] bool snapshot);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_key(TransactionHandle transaction, byte* keyName, int keyNameLength, bool orEqual, int offset, bool snapshot);
#endif

			/// <summary>Reads all key-value pairs in the database snapshot represented by <paramref name="transaction"/> (potentially limited by <paramref name="limit"/>, <paramref name="targetBytes"/>, or <paramref name="mode"/>) which have a key lexicographically greater than or equal to the key resolved by the <c>begin</c> key selector and lexicographically less than the key resolved by the <c>end</c> key selector.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to an <c>FDBKeyValue</c> array.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_keyvalue_array"/> to extract the key-value array, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_range(
				TransactionHandle transaction,
				/* begin */ byte* beginKeyName, int beginKeyNameLength, [MarshalAs(UnmanagedType.Bool)] bool beginOrEqual, int beginOffset,
				/* end */ byte* endKeyName, int endKeyNameLength, [MarshalAs(UnmanagedType.Bool)] bool endOrEqual, int endOffset,
				int limit, int targetBytes, FdbStreamingMode mode, int iteration, [MarshalAs(UnmanagedType.Bool)] bool snapshot, [MarshalAs(UnmanagedType.Bool)] bool reverse
			);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_range(
				TransactionHandle transaction,
				/* begin */ byte* beginKeyName, int beginKeyNameLength, bool beginOrEqual, int beginOffset,
				/* end */ byte* endKeyName, int endKeyNameLength, bool endOrEqual, int endOffset,
				int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse
			);
#endif

			//TODO: documentation! (added 7.1)
			//TODO: 'fdb_transaction_get_range_and_flat_map' was added in 7.0 but renamed to 'fdb_transaction_get_mapped_range' in 7.1 ... should we support the old name?
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_mapped_range(
				TransactionHandle transaction,
				/* begin */ byte* beginKeyName, int beginKeyNameLength, [MarshalAs(UnmanagedType.Bool)] bool beginOrEqual, int beginOffset,
				/* end */ byte* endKeyName, int endKeyNameLength, [MarshalAs(UnmanagedType.Bool)] bool endOrEqual, int endOffset,
				/* mapper */ byte* mapperName, int mapperNameLength,
				int limit, int targetBytes, FdbStreamingMode mode, int iteration, [MarshalAs(UnmanagedType.Bool)] bool snapshot, [MarshalAs(UnmanagedType.Bool)] bool reverse
			);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_mapped_range(
				TransactionHandle transaction,
				/* begin */ byte* beginKeyName, int beginKeyNameLength, bool beginOrEqual, int beginOffset,
				/* end */ byte* endKeyName, int endKeyNameLength, bool endOrEqual, int endOffset,
				/* mapper */ byte* mapperName, int mapperNameLength,
				int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse
			);
#endif

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to change the given key to have the given value. If the given key was not previously present in the database it is inserted.</summary>
			/// <remarks>
			/// The modification affects the actual database only if <paramref name="transaction"/> is later committed with <see cref="fdb_transaction_commit"/>.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_set(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* value, int valueLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_set(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* value, int valueLength);
#endif

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to remove the given key from the database. If the key was not previously present in the database, there is no effect.</summary>
			/// <remarks>
			/// The modification affects the actual database only if <paramref name="transaction"/> is later committed with <see cref="fdb_transaction_commit"/>.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_clear(TransactionHandle transaction, byte* keyName, int keyNameLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_clear(TransactionHandle transaction, byte* keyName, int keyNameLength);
#endif

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.</summary>
			/// <remarks>
			/// The modification affects the actual database only if <paramref name="transaction"/> is later committed with <see cref="fdb_transaction_commit"/>.
			/// Range clears are efficient with FoundationDB  clearing large amounts of data will be fast.
			/// However, this will not immediately free up disk - data for the deleted range is cleaned up in the background.
			/// For purposes of computing the transaction size, only the <c>Begin</c> and <c>End</c> keys of a clear range are counted.
			/// The size of the data stored in the range does not count against the transaction size limit.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_clear_range(
				TransactionHandle transaction,
				byte* beginKeyName, int beginKeyNameLength,
				byte* endKeyName, int endKeyNameLength
			);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_clear_range(
				TransactionHandle transaction,
				byte* beginKeyName, int beginKeyNameLength,
				byte* endKeyName, int endKeyNameLength
			);
#endif

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to perform the operation indicated by operationType with operand param to the value stored by the given key.</summary>
			/// <remarks>
			/// An atomic operation is a single database command that carries out several logical steps: reading the value of a key, performing a transformation on that value, and writing the result.
			/// Different atomic operations perform different transformations.
			/// Like other database operations, an atomic operation is used within a transaction; however, its use within a transaction will not cause the transaction to conflict.
			/// Atomic operations do not expose the current value of the key to the client but simply send the database the transformation to apply.
			/// In regard to conflict checking, an atomic operation is equivalent to a 'write' without a 'read'. It can only cause other transactions performing reads of the key to conflict.
			/// By combining these logical steps into a single, read-free operation, FoundationDB can guarantee that the transaction will not conflict due to the operation.
			/// This makes atomic operations ideal for operating on keys that are frequently modified. A common example is the use of a key-value pair as a counter.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_atomic_op(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* param, int paramLength, FdbMutationType operationType);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_atomic_op(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* param, int paramLength, FdbMutationType operationType);
#endif

			/// <summary>Attempts to commit the sets and clears previously applied to the database snapshot represented by transaction to the actual database. The commit may or may not succeed  in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> representing an empty value.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>It is not necessary to commit a read-only transaction  you can simply call <see cref="fdb_transaction_destroy"/>.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_commit(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_commit(TransactionHandle transaction);
#endif

			/// <summary>Retrieves the database version number at which a given transaction was committed.</summary>
			/// <remarks>
			/// <see cref="fdb_transaction_commit"/> must have been called on <paramref name="transaction"/> and the resulting future must be ready and not an error before this function is called, or the behavior is undefined.
			/// Read-only transactions do not modify the database when committed and will have a committed version of -1.
			/// Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
			/// Note that database versions are not necessarily unique to a given transaction and so cannot be used to determine in what order two transactions completed.
			/// The only use for this function is to manually enforce causal consistency when calling <see cref="fdb_transaction_set_read_version"/> on another subsequent transaction.
			/// Most applications will not call this function.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_transaction_get_committed_version(TransactionHandle transaction, out long version);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_get_committed_version(TransactionHandle transaction, out long version);
#endif

			/// <summary>Retrieves the <see cref="VersionStamp"/> which was used by any versionstamp operation in this transaction.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the versionstamp which was used by any versionstamp operations in this transaction.</returns>
			/// <remarks>
			/// You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_key"/> to extract the key, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.
			/// The future will be ready only after the successful completion of a call to <see cref="fdb_transaction_commit"/> on this Transaction. Read-only transactions do not modify the database when committed and will result in the future completing with an error. Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
			/// Most applications will not call this function.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_versionstamp(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_versionstamp(TransactionHandle transaction);
#endif

			/// <summary></summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> representing an empty value that will be set once the watch has detected a change to the value at the specified key.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>
			/// A watchs behavior is relative to the transaction that created it.
			/// A watch will report a change in relation to the keys value as readable by that transaction.
			/// The initial value used for comparison is either that of the transactions read version or the value as modified by the transaction itself prior to the creation of the watch.
			/// If the value changes and then changes back to its initial value, the watch might not report the change.
			/// </para>
			/// <para>
			/// Until the transaction that created it has been committed, a watch will not report changes made by other transactions.
			/// In contrast, a watch will immediately report changes made by the transaction itself.
			/// Watches cannot be created if the transaction has set the <c>READ_YOUR_WRITES_DISABLE</c> transaction option, and an attempt to do so will return a <c>watches_disabled</c> error.
			/// </para>
			/// <para>
			/// If the transaction used to create a watch encounters an error during commit, then the watch will be set with that error.
			/// A transaction whose commit result is unknown will set all of its watches with the commit_unknown_result error.
			/// If an uncommitted transaction is reset or destroyed, then any watches it created will be set with the <see cref="FdbError"><c>transaction_cancelled</c></see> error.
			/// </para>
			/// <para>
			/// By default, each database connection can have no more than <c>10,000</c> watches that have not yet reported a change.
			/// When this number is exceeded, an attempt to create a watch will return a <see cref="FdbError"><c>too_many_watches</c></see> error.
			/// This limit can be changed using the <c>MAX_WATCHES</c> database option.
			/// Because a watch outlives the transaction that creates it, any watch that is no longer needed should be cancelled by calling <see cref="fdb_future_cancel"/> on its returned future.
			/// </para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_watch(TransactionHandle transaction, byte* keyName, int keyNameLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_watch(TransactionHandle transaction, byte* keyName, int keyNameLength);
#endif

			/// <summary>Implements the recommended retry and backoff behavior for a transaction.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> representing an empty value.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>
			/// This function knows which of the error codes generated by other <c>fdb_transaction_*()</c> functions represent temporary error conditions and which represent application errors that should be handled by the application.
			/// It also implements an exponential backoff strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
			/// </para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_on_error(TransactionHandle transaction, FdbError error);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_on_error(TransactionHandle transaction, FdbError error);
#endif

			/// <summary>Reset <paramref name="transaction"/> to its initial state.</summary>
			/// <remarks>
			/// This is similar to calling <see cref="fdb_transaction_destroy"/> followed by <see cref="fdb_database_create_transaction"/>.
			/// It is not necessary to call <see cref="fdb_transaction_reset"/> when handling an error with <see cref="fdb_transaction_on_error"/> since the transaction has already been reset.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_reset(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_reset(TransactionHandle transaction);
#endif

			/// <summary>Cancels the transaction.</summary>
			/// <remarks>
			/// All pending or future uses of the transaction will return a <see cref="FdbError"><c>transaction_cancelled</c></see> error.
			/// The transaction can be used again after it is reset.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_transaction_cancel(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_cancel(TransactionHandle transaction);
#endif

			/// <summary>Adds a conflict range to a transaction without performing the associated read or write.</summary>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_transaction_add_conflict_range(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, FdbConflictRangeType type);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_add_conflict_range(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, FdbConflictRangeType type);
#endif

			/// <summary>Returns the approximate transaction size so far.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the approximate transaction size so far in the returned future, which is the summation of the estimated size of mutations, read conflict ranges, and write conflict ranges.</returns>
			/// <remarks>
			/// You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_int64"/> to extract the size, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.
			/// This can be called multiple times before the transaction is committed.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_approximate_size(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_approximate_size(TransactionHandle transaction);
#endif


			//TODO: documentation! (added 7.3)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_tag_throttled_duration(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_tag_throttled_duration(TransactionHandle transaction);
#endif

			//TODO: documentation! (added 7.3)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_total_cost(TransactionHandle transaction);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_total_cost(TransactionHandle transaction);
#endif

			//TODO: documentation! (added 7.3)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FutureHandle fdb_transaction_get_blob_granule_ranges(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_blob_granule_ranges(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength);
#endif

			#endregion

			#region Future

			/// <summary>Destroys an <see cref="FutureHandle">FDBFuture</see> object.</summary>
			/// <remarks>
			/// It must be called exactly once for each FDBFuture* returned by an API function.
			/// It may be called before or after the future is ready.
			/// It will also cancel the future (and its associated operation if the latter is still outstanding).
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_future_destroy(IntPtr future);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_destroy(IntPtr future);
#endif

			/// <summary>Cancels an <see cref="FutureHandle">FDBFuture</see> object and its associated asynchronous operation.</summary>
			/// <remarks>
			/// If called before the future is ready, attempts to access its value will return an operation_cancelled error.
			/// Cancelling a future which is already ready has no effect.
			/// Note that even if a future is not ready, its associated asynchronous operation may have successfully completed and be unable to be cancelled.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_future_cancel(FutureHandle future);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_cancel(FutureHandle future);
#endif

			/// <summary>Release memory associated to the given <see cref="FutureHandle">FDBFuture</see> object.</summary>
			/// <remarks>
			/// This function may only be called after a successful (zero return value) call to <see cref="fdb_future_get_key"/>, <see cref="fdb_future_get_value"/>, or <see cref="fdb_future_get_keyvalue_array"/>.
			/// It indicates that the memory returned by the prior get call is no longer needed by the application.
			/// After this function has been called the same number of times as fdb_future_get_*(), further calls to fdb_future_get_*() will return a future_released error.
			/// It is still necessary to later destroy the future with fdb_future_destroy().
			/// Calling this function is optional, since <see cref="fdb_future_destroy"/> will also release the memory returned by get functions.
			/// However, <see cref="fdb_future_release_memory"/> leaves the future object itself intact and provides a specific error code which can be used for coordination by multiple threads racing to do something with the results of a specific future.
			/// This has proven helpful in writing binding code.
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial void fdb_future_release_memory(FutureHandle future);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_release_memory(FutureHandle future);
#endif

			/// <summary>Blocks the calling thread until the given <c>Future</c> is ready.</summary>
			/// <remarks>
			/// It will return success even if the <c>Future</c> is set to an error  you must call <see cref="fdb_future_get_error"/> to determine that.
			/// <see cref="fdb_future_block_until_ready"/> will return an error only in exceptional conditions (e.g. deadlock detected, out of memory or other operating system resources).
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_block_until_ready(FutureHandle future);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_block_until_ready(FutureHandle future);
#endif

			/// <summary>Returns non-zero if the <paramref name="future"/> is ready.</summary>
			/// <remarks>A <c>Future</c> is ready if it has been set to a value or an error.</remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			[return: MarshalAs(UnmanagedType.Bool)] 
			public static partial bool fdb_future_is_ready(FutureHandle future);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_future_is_ready(FutureHandle future);
#endif

			/// <summary>Returns zero if <paramref name="future"/> is ready and not in an error state, and a non-zero error code otherwise.</summary>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_error(FutureHandle future);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_error(FutureHandle future);
#endif

			/// <summary>Causes the FDBCallback function to be invoked as <c><paramref name="callback"/>(<paramref name="future"/>, <paramref name="parameter"/>)</c> when the given <paramref name="future"/> is ready.</summary>
			/// <returns>
			/// If the <c>Future</c> is already ready, the call may occur in the current thread before this function returns (but this behavior is not guaranteed).
			/// Alternatively, the call may be delayed indefinitely and take place on the thread on which <see cref="fdb_run_network"/> was invoked,
			/// and the callback is responsible for any necessary thread synchronization (and/or for posting work back to your application
			/// event loop, thread pool, etc. if your applications architecture calls for that).
			/// </returns>
#if NET8_0_OR_GREATER
			// the callback is a raw unmanaged function pointer (a single [UnmanagedCallersOnly] entry point shared by
			// all futures), not a marshaled delegate: no per-callback thunk, nothing to keep alive on the managed side
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_set_callback(FutureHandle future, IntPtr callback, IntPtr parameter);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_set_callback(FutureHandle future, FdbFutureCallback callback, IntPtr parameter);
#endif

			/// <summary>Extracts an 64-bit version number from a pointer to <see cref="FutureHandle">FDBFuture</see>.</summary>
			/// <remarks>
			/// <para><paramref name="future"/> must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>Added in 700</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_version(FutureHandle future, out long version);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_version(FutureHandle future, out long version);
#endif

			/// <summary>Extracts a boolean from a pointer to <see cref="FutureHandle">FDBFuture</see>.</summary>
			/// <remarks>
			/// <para><paramref name="future"/> must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>Added in 720</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_bool(FutureHandle future, [MarshalAs(UnmanagedType.Bool)] out bool version);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_bool(FutureHandle future, out bool version);
#endif

			/// <summary>Extracts a signed 64-bit integer from a pointer to <see cref="FutureHandle">FDBFuture</see>.</summary>
			/// <remarks>
			/// <para><paramref name="future"/> must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>Added in 630</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_int64(FutureHandle future, out long value);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_int64(FutureHandle future, out long value);
#endif

			/// <summary>Extracts an unsigned 64-bit integer from a pointer to <see cref="FutureHandle">FDBFuture</see>.</summary>
			/// <remarks>
			/// <para><paramref name="future"/> must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>Added in 700</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_uint64(FutureHandle future, out ulong value);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_uint64(FutureHandle future, out ulong value);
#endif

			/// <summary>Extracts a double-precision floating-point number from a pointer to <see cref="FutureHandle">FDBFuture</see>.</summary>
			/// <remarks>
			/// <para><paramref name="future"/> must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>Added in 730</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_double(FutureHandle future, out double value);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_double(FutureHandle future, out double value);
#endif

			/// <summary>Extracts a key from an <see cref="FutureHandle">FDBFuture</see> into caller-provided variables of type <c>uint8_t*</c> (a pointer to the beginning of the key) and int (the length of the key). </summary>
			/// <remarks>
			/// <para>future must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>The memory referenced by the result is owned by the <c>FDBFuture</c> object and will be valid until either <see cref="fdb_future_destroy"/> or <see cref="fdb_future_release_memory"/> is called.</para>
			/// </remarks>
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_key(FutureHandle future, out byte* key, out int keyLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_key(FutureHandle future, out byte* key, out int keyLength);
#endif

#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_database(FutureHandle future, out DatabaseHandle database);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_database(FutureHandle future, out DatabaseHandle database);
#endif

#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_value(FutureHandle future, [MarshalAs(UnmanagedType.Bool)] out bool present, out byte* value, out int valueLength);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_value(FutureHandle future, out bool present, out byte* value, out int valueLength);
#endif

#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_keyvalue_array(FutureHandle future, out FdbKeyValue* kv, out int count, [MarshalAs(UnmanagedType.Bool)] out bool more);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_keyvalue_array(FutureHandle future, out FdbKeyValue* kv, out int count, out bool more);
#endif

			//TODO: documentation! (added 7.1)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_mappedkeyvalue_array(FutureHandle future, out FdbMappedKeyValueNative* kvm, out int count, [MarshalAs(UnmanagedType.Bool)] out bool more);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_mappedkeyvalue_array(FutureHandle future, out FdbMappedKeyValueNative* kvm, out int count, out bool more);
#endif

			//TODO: documentation! (added 7.0)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_key_array(FutureHandle future, out FdbKeyNative* keyArray, out int count);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_key_array(FutureHandle future, out FdbKeyNative* keyArray, out int count);
#endif

#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_string_array(FutureHandle future, out byte** strings, out int count);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_string_array(FutureHandle future, out byte** strings, out int count);
#endif

			//TODO: documentation! (added 7.1)
#if NET8_0_OR_GREATER
			[LibraryImport(FDB_C_DLL, StringMarshalling = StringMarshalling.Utf8)]
			[UnmanagedCallConv(CallConvs = [ typeof(CallConvCdecl) ])]
			public static partial FdbError fdb_future_get_keyrange_array(FutureHandle future, out FdbKeyRangeNative* ranges, out int count);
#else
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_keyrange_array(FutureHandle future, out FdbKeyRangeNative* ranges, out int count);
#endif

			#endregion

		}

		static FdbNative()
		{
			var libraryPath = GetPreloadPath();

			if (libraryPath == null)
			{ // PInvoke will load
				return;
			}

			try
			{
				FdbCLib = UnmanagedLibrary.Load(libraryPath);
#if NETCOREAPP3_0_OR_GREATER
				// Pin every [LibraryImport/DllImport(FDB_C_DLL)] call to the library we just preloaded, so the
				// P/Invoke name resolution can never bind a different copy: a stale libfdb_c in the application
				// base directory, a system copy (on macOS, /usr/local/lib/libfdb_c.dylib), or a dyld leaf-name
				// match. On Windows the OS module table already pins the name once the library is loaded; on Linux
				// and macOS NativeLibrary.Load loads by path and does NOT bind the FDB_C_DLL name, so without this
				// the first call re-resolves by convention and can pick the wrong file. Done on all platforms so
				// the guarantee is explicit and uniform. The handle is never released (see
				// SafeLibraryHandle.ReleaseHandle), so it stays valid for the lifetime of the process.
				NativeLibrary.SetDllImportResolver(
					typeof(FdbNative).Assembly,
					static (name, assembly, searchPath) => string.Equals(name, FDB_C_DLL, StringComparison.Ordinal) && FdbCLib is not null
						? FdbCLib.Handle.DangerousGetHandle()
						: IntPtr.Zero
				);
#endif
			}
			catch (Exception e)
			{
				FdbCLib?.Dispose();
				FdbCLib = null;
				if (e is BadImageFormatException && IntPtr.Size == 4)
				{
					e = new InvalidOperationException("The native FDB client is 64-bit only, and cannot be loaded in a 32-bit process.", e);
				}
				else
				{
					e = new InvalidOperationException($"An error occurred while loading the native FoundationDB library: '{libraryPath}'.", e);
				}
				LibraryLoadError = ExceptionDispatchInfo.Capture(e);
			}
		}

		[Conditional("DEBUG_NATIVE_CALLS")]
		private static void LogNative(string message)
		{
#if DEBUG_NATIVE_CALLS
			System.Diagnostics.Debug.WriteLine(message);
			Console.WriteLine(message);
#endif
		}

		private static string? GetPreloadPath()
		{
			// we need to provide sensible defaults for loading the native library
			// if this method returns null we'll let PInvoke deal
			// otherwise - use explicit platform-specific dll loading
			var libraryPath = Fdb.Options.NativeLibPath;

			// on non-windows, library loading by convention just works.
			// unless override is provided, just let PInvoke do the work
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (string.IsNullOrEmpty(libraryPath))
				{
					return null;
				}
				// otherwise just use the provided path
				return libraryPath;
			}

			// Impact of NativeLibPath on windows:
			// - If null, don't preload the library, and let the CLR find the file using the default P/Invoke behavior
			// - If String.Empty, call win32 LoadLibrary(FDB_C_DLL + ".dll") and let the os find the file (using the standard OS behavior)
			// - If path is folder, append the FDB_C_DLL
			var winDllWithExtension = FDB_C_DLL + ".dll";
			if (libraryPath == null)
			{
				return null;
			}
			if (libraryPath.Length == 0)
			{
				return winDllWithExtension;
			}
			var fileName = Path.GetFileName(libraryPath);
			if (string.IsNullOrEmpty(fileName))
			{
				libraryPath = Path.Combine(libraryPath, winDllWithExtension);
			}
			return libraryPath;
		}

		private static void EnsureLibraryIsLoaded()
		{
			// should be inlined
			FdbNative.LibraryLoadError?.Throw();
		}

		private static string? ToManagedString(byte* nativeString)
		{
			if (nativeString == null) return null;
			return Marshal.PtrToStringAnsi(new IntPtr(nativeString));
		}

		/// <summary>Converts a string into an ANSI byte array</summary>
		/// <param name="value">String to convert (or null)</param>
		/// <param name="nullTerminated">If true, adds a terminating \0 at the end (C-style strings)</param>
		/// <returns>Byte array with the ANSI-encoded string with an optional NUL terminator, or null if <paramref name="value"/> was null</returns>
		public static Slice ToNativeString(ReadOnlySpan<char> value, bool nullTerminated)
		{
			if (value.Length == 0) return Slice.Empty;

			int len = Encoding.Default.GetByteCount(value);

			if (nullTerminated)
			{ // NULL terminated ANSI string
				len = checked(len + 1);
			}

			var buffer = new byte[len];
			Encoding.Default.GetBytes(value, buffer);

			if (nullTerminated)
			{
				//note: last byte should already be zero, but we want to be sure, in case the default encoding would somehow mess up!
				buffer[^1] = 0;
			}

			return buffer.AsSlice();
		}

		#region Core..

		/// <summary>Throws an exception if the code represents a failure.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DieOnError(FdbError code)
		{
			if (code != FdbError.Success)
			{
				throw CreateExceptionFromError(code);
			}
		}

		/// <summary>fdb_get_error</summary>
		public static string? GetErrorMessage(FdbError code)
		{
			return ToManagedString(NativeMethods.fdb_get_error(code));
		}

		/// <summary>Maps an error code into an Exception (to be thrown)</summary>
		/// <param name="code">Error code returned by a native fdb operation</param>
		/// <returns>Exception object corresponding to the error code, or null if the code is not an error</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Exception? MapToException(FdbError code)
		{
			return code == FdbError.Success ? null : CreateExceptionFromError(code);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception CreateExceptionFromError(FdbError code)
		{
			return code switch
			{
				FdbError.TimedOut => new TimeoutException("Operation timed out"),
				FdbError.LargeAllocFailed => new OutOfMemoryException("Large block allocation failed"),
				FdbError.InvalidOption => new ArgumentException("Option not valid in this context"),
				FdbError.ApiVersionUnset => new InvalidOperationException("Api version must be set"),
				//TODO: add more custom mappings?
				// the managed table answers for every code it knows; only a code newer than this build costs the interop call
				_ => new FdbException(code, FdbErrorMessages.TryGetMessage(code) ?? GetErrorMessage(code) ?? $"{code} ({(int) code})")
			};
		}

		/// <summary>fdb_error_predicate</summary>
		public static bool TestErrorPredicate(FdbErrorPredicate predicate, FdbError code)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_error_predicate(predicate, code);
		}

		/// <summary>fdb_select_api_impl</summary>
		public static FdbError SelectApiVersionImpl(int runtimeVersion, int headerVersion)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_select_api_version_impl(runtimeVersion, headerVersion);
		}

		/// <summary>fdb_select_api_impl</summary>
		public static FdbError SelectApiVersion(int version)
		{
			return SelectApiVersionImpl(version, GetMaxApiVersion());
		}

		/// <summary>fdb_get_max_api_version</summary>
		public static int GetMaxApiVersion()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_get_max_api_version();
		}

		/// <summary>fdb_get_client_version</summary>
		public static string GetClientVersion()
		{
			byte* ptr = NativeMethods.fdb_get_client_version();
			return ToManagedString(ptr) ?? string.Empty;
		}

		#endregion

		#region Futures...

		/// <summary>fdb_future_is_ready</summary>
		public static bool FutureIsReady(FutureHandle futureHandle)
		{
			return NativeMethods.fdb_future_is_ready(futureHandle);
		}

		/// <summary>fdb_future_destroy</summary>
		public static void FutureDestroy(IntPtr futureHandle)
		{
			if (futureHandle != IntPtr.Zero)
			{
				NativeMethods.fdb_future_destroy(futureHandle);
			}
		}

		/// <summary>fdb_future_cancel</summary>
		public static void FutureCancel(FutureHandle futureHandle)
		{
			NativeMethods.fdb_future_cancel(futureHandle);
		}

		/// <summary>fdb_future_release_memory</summary>
		public static void FutureReleaseMemory(FutureHandle futureHandle)
		{
			NativeMethods.fdb_future_release_memory(futureHandle);
		}

		/// <summary>fdb_future_get_error</summary>
		public static FdbError FutureGetError(FutureHandle future)
		{
			return NativeMethods.fdb_future_get_error(future);
		}

		/// <summary>fdb_future_block_until_ready</summary>
		public static FdbError FutureBlockUntilReady(FutureHandle future)
		{
#if DEBUG_NATIVE_CALLS
			LogNative($"calling fdb_future_block_until_ready(0x{future.Handle:x})...");
#endif
			var err = NativeMethods.fdb_future_block_until_ready(future);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_block_until_ready(0x{future.Handle:x}) => err={err}");
#endif
			return err;
		}

		/// <summary>fdb_future_set_callback</summary>
#if NET8_0_OR_GREATER
		public static FdbError FutureSetCallback(FutureHandle future, IntPtr callback, IntPtr callbackParameter)
#else
		public static FdbError FutureSetCallback(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter)
#endif
		{
			var err = NativeMethods.fdb_future_set_callback(future, callback, callbackParameter);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_set_callback(0x{future.Handle:x}, 0x{callbackParameter:x}) => err={err}");
#endif
			return err;
		}

		#endregion

		#region Network...

		/// <summary>fdb_network_set_option</summary>
		public static FdbError NetworkSetOption(FdbNetworkOption option, byte* value, int valueLength)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_network_set_option(option, value, valueLength);
		}

		/// <summary>fdb_setup_network</summary>
		public static FdbError SetupNetwork()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_setup_network();
		}

		/// <summary>fdb_run_network</summary>
		public static FdbError RunNetwork()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_run_network();
		}

		/// <summary>fdb_stop_network</summary>
		public static FdbError StopNetwork()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_stop_network();
		}

		/// <summary>fdb_add_network_thread_completion_hook</summary>
		public static FdbError AddNetworkThreadCompletionHook(FdbNetworkThreadCompletionCallback hook, IntPtr parameter)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_add_network_thread_completion_hook(hook, parameter);
		}

		#endregion

		#region Databases...

		/// <summary>fdb_create_database</summary>
		public static FdbError CreateDatabase(string? path, out DatabaseHandle database)
		{
			var err = NativeMethods.fdb_create_database(path, out database);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_create_database({path}) => err={err}");
#endif

			//TODO: check if err == Success ?
			return err;
		}

		/// <summary>fdb_create_database_from_connection_string, >= 720</summary>
		public static FdbError CreateDatabaseFromConnectionString(string connectionString, out DatabaseHandle database)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 720);
			var err = NativeMethods.fdb_create_database_from_connection_string(connectionString, out database);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_create_database_from_connection_string({connectionString}) => err={err}");
#endif

			//TODO: check if err == Success ?
			return err;
		}

		/// <summary>fdb_future_get_database</summary>
		public static FdbError FutureGetDatabase(FutureHandle future, out DatabaseHandle database)
		{
			var err = NativeMethods.fdb_future_get_database(future, out database);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_database(0x{future.Handle:x}) => err={err}, handle=0x{database.Handle:x}");
#endif
			//TODO: check if err == Success ?
			return err;
		}

		/// <summary>fdb_database_set_option</summary>
		public static FdbError DatabaseSetOption(DatabaseHandle database, FdbDatabaseOption option, byte* value, int valueLength)
		{
			return NativeMethods.fdb_database_set_option(database, option, value, valueLength);
		}

		/// <summary>fdb_database_destroy</summary>
		public static void DatabaseDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_database_destroy(handle);
			}
		}

		/// <summary>fdb_database_get_main_thread_busyness, >= 700</summary>
		public static double GetMainThreadBusyness(DatabaseHandle handle)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 700);

			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_database_get_main_thread_busyness(handle);
		}

		/// <summary>fdb_database_open_tenant, >= 710</summary>
		public static FdbError DatabaseOpenTenant(DatabaseHandle database, ReadOnlySpan<byte> name, out TenantHandle tenant)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 710);

			fixed (byte* ptr = name)
			{
				var err = NativeMethods.fdb_database_open_tenant(database, ptr, name.Length, out tenant);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_database_open_tenant(db: 0x{database.Handle:x}, '{Slice.Copy(name):K}') => err={err}, handle=0x{tenant.Handle:x}");
#endif
				return err;
			}
		}

		/// <summary>fdb_database_reboot_worker, >= 700</summary>
		public static FutureHandle DatabaseRebootWorker(DatabaseHandle database, ReadOnlySpan<char> name, bool check, int duration)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 700);

			var bytes = ToNativeString(name, true);
			fixed (byte* ptr = bytes)
			{
				var future = NativeMethods.fdb_database_reboot_worker(database, ptr, name.Length, check, duration);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_database_reboot_worker(db: 0x{database.Handle:x}, '{Slice.Copy(name)}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_database_reboot_worker, >= 700</summary>
		public static FutureHandle DatabaseForceRecoveryWithDataLoss(DatabaseHandle database, ReadOnlySpan<char> dcId)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 700);

			var bytes = ToNativeString(dcId, true);
			fixed (byte* ptr = bytes)
			{
				var future = NativeMethods.fdb_database_force_recovery_with_data_loss(database, ptr, dcId.Length);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_database_force_recovery_with_data_loss(db: 0x{database.Handle:x}, '{Slice.Copy(dcId)}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_database_create_snapshot, >= 700</summary>
		public static FutureHandle DatabaseCreateSnapshot(DatabaseHandle database, ReadOnlySpan<char> uid, ReadOnlySpan<char> snapCommand)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 700);

			var uidBytes = ToNativeString(uid, true);
			var snapCommandBytes = ToNativeString(snapCommand, true);
			fixed (byte* uidPtr = uidBytes)
			fixed (byte* snampCommandPtr = snapCommandBytes)
			{
				var future = NativeMethods.fdb_database_create_snapshot(database, uidPtr, uid.Length, snampCommandPtr, snapCommand.Length);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_database_create_snapshot(db: 0x{database.Handle:x}, '{Slice.Copy(uid)}', '{Slice.Copy(snapCommand)}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_database_get_server_protocol, >= 700</summary>
		public static FutureHandle DatabaseGetServerProtocol(DatabaseHandle database, ulong expectedVersion)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 700);

			var future = NativeMethods.fdb_database_get_server_protocol(database, expectedVersion);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_database_get_server_protocol(db: 0x{database.Handle:x}, expectedVersion: 0x{expectedVersion:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		/// <summary>fdb_database_get_client_status, >= 730</summary>
		public static FutureHandle DatabaseGetClientStatus(DatabaseHandle database)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 730);

			var future = NativeMethods.fdb_database_get_client_status(database);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_database_get_client_status(db: 0x{database.Handle:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		#endregion

		#region Tenants...

		/// <summary>fdb_tenant_destroy, >= 710</summary>
		public static void TenantDestroy(IntPtr handle)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 710);

			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_tenant_destroy(handle);
			}
		}

		/// <summary>fdb_tenant_create_transaction, >= 710</summary>
		public static FdbError TenantCreateTransaction(TenantHandle tenant, out TransactionHandle transaction)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 710);

			var err = NativeMethods.fdb_tenant_create_transaction(tenant, out transaction);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_tenant_create_transaction(0x{tenant.Handle:x}) => err={err}, handle=0x{transaction.Handle:x}");
#endif
			return err;
		}

		/// <summary>fdb_tenant_get_id, >= 730</summary>
		public static FutureHandle TenantGetId(TenantHandle tenant)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 730);

			var future = NativeMethods.fdb_tenant_get_id(tenant);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_tenant_get_id(0x{tenant.Handle:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		#endregion

		#region Transactions...

		/// <summary>fdb_transaction_destroy</summary>
		public static void TransactionDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_transaction_destroy(handle);
			}
		}

		/// <summary>fdb_transaction_set_option</summary>
		public static FdbError TransactionSetOption(TransactionHandle transaction, FdbTransactionOption option, byte* value, int valueLength)
		{
			return NativeMethods.fdb_transaction_set_option(transaction, option, value, valueLength);
		}

		/// <summary>fdb_database_create_transaction</summary>
		public static FdbError DatabaseCreateTransaction(DatabaseHandle database, out TransactionHandle transaction)
		{
			var err = NativeMethods.fdb_database_create_transaction(database, out transaction);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_database_create_transaction(db: 0x{database.Handle:x}) => err={err}, handle=0x{transaction.Handle:x}");
#endif
			return err;
		}

		/// <summary>fdb_transaction_commit</summary>
		public static FutureHandle TransactionCommit(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_commit(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_commit(tr: 0x{transaction.Handle:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		/// <summary>fdb_transaction_get_versionstamp</summary>
		public static FutureHandle TransactionGetVersionStamp(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_versionstamp(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_get_versionstamp(tr: 0x{transaction.Handle:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		/// <summary>fdb_transaction_watch</summary>
		public static FutureHandle TransactionWatch(TransactionHandle transaction, ReadOnlySpan<byte> key)
		{
			if (key.Length == 0) throw new ArgumentException("Key cannot be null or empty", nameof(key));

			fixed (byte* ptrKey = key)
			{
				var future = NativeMethods.fdb_transaction_watch(transaction, ptrKey, key.Length);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_watch(tr: 0x{transaction.Handle:x}, key: '{Slice.Copy(key):K}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_transaction_on_error</summary>
		public static FutureHandle TransactionOnError(TransactionHandle transaction, FdbError errorCode)
		{
			var future = NativeMethods.fdb_transaction_on_error(transaction, errorCode);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_on_error(tr: 0x{transaction.Handle:x}, {errorCode}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		/// <summary>fdb_transaction_reset</summary>
		public static void TransactionReset(TransactionHandle transaction)
		{
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_reset(tr: 0x{transaction.Handle:x})");
#endif
			NativeMethods.fdb_transaction_reset(transaction);
		}

		/// <summary>fdb_transaction_cancel</summary>
		public static void TransactionCancel(TransactionHandle transaction)
		{
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_cancel(tr: 0x{transaction.Handle:x})");
#endif
			NativeMethods.fdb_transaction_cancel(transaction);
		}

		/// <summary>fdb_transaction_set_read_version</summary>
		public static void TransactionSetReadVersion(TransactionHandle transaction, long version)
		{
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_set_read_version(tr: 0x{transaction.Handle:x}, version: {version})");
#endif
			NativeMethods.fdb_transaction_set_read_version(transaction, version);
		}

		/// <summary>fdb_transaction_get_read_version</summary>
		public static FutureHandle TransactionGetReadVersion(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_read_version(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_get_read_version(tr: 0x{transaction.Handle:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		/// <summary>fdb_transaction_get_committed_version</summary>
		public static FdbError TransactionGetCommittedVersion(TransactionHandle transaction, out long version)
		{
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_get_committed_version(tr: 0x{transaction.Handle:x})");
#endif
			return NativeMethods.fdb_transaction_get_committed_version(transaction, out version);
		}

		/// <summary>fdb_future_get_version, &lt;= 610</summary>
		/// <remarks>Was renamed into fdb_future_get_int64 starting from API 620</remarks>
		public static FdbError FutureGetVersion(FutureHandle future, out long version)
		{
			Contract.Debug.Requires(Fdb.BindingVersion <= 610);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_version(fut: 0x{future.Handle:x})");
#endif
			return NativeMethods.fdb_future_get_version(future, out version);
		}

		/// <summary>fdb_future_get_int64, >= 620</summary>
		/// <remarks>Was called <c>fdb_future_get_version</c> and renamed in 620.</remarks>
		public static FdbError FutureGetInt64(FutureHandle future, out long value)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 620);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_int64(fut: 0x{future.Handle:x})");
#endif
			return NativeMethods.fdb_future_get_int64(future, out value);
		}

		/// <summary>fdb_future_get_uint64</summary>
		public static FdbError FutureGetUInt64(FutureHandle future, out ulong value)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 700);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_uint64(fut: 0x{future.Handle:x})");
#endif
			return NativeMethods.fdb_future_get_uint64(future, out value);
		}

		/// <summary>fdb_future_get_bool</summary>
		public static FdbError FutureGetBool(FutureHandle future, out bool value)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 720);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_bool(fut: 0x{future.Handle:x})");
#endif
			return NativeMethods.fdb_future_get_bool(future, out value);
		}

		/// <summary>fdb_future_get_double</summary>
		public static FdbError FutureGetDouble(FutureHandle future, out double value)
		{
			Contract.Debug.Requires(Fdb.BindingVersion >= 730);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_double(fut: 0x{future.Handle:x})");
#endif
			return NativeMethods.fdb_future_get_double(future, out value);
		}

		/// <summary>fdb_transaction_get</summary>
		public static FutureHandle TransactionGet(TransactionHandle transaction, ReadOnlySpan<byte> key, bool snapshot)
		{
			// the empty key is allowed !
			fixed (byte* ptrKey = key)
			{
				var future = NativeMethods.fdb_transaction_get(transaction, ptrKey, key.Length, snapshot);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_get(tr: 0x{transaction.Handle:x}, key: '{FdbKey.Dump(key)}', snapshot: {snapshot}) => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_transaction_get_range</summary>
		public static FutureHandle TransactionGetRange(TransactionHandle transaction, KeySpanSelector begin, KeySpanSelector end, int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse)
		{
			fixed (byte* ptrBegin = begin.Key)
			fixed (byte* ptrEnd = end.Key)
			{
				var future = NativeMethods.fdb_transaction_get_range(
					transaction,
					ptrBegin, begin.Key.Length, begin.OrEqual, begin.Offset,
					ptrEnd, end.Key.Length, end.OrEqual, end.Offset,
					limit, targetBytes, mode, iteration, snapshot, reverse);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_get_range(tr: 0x{transaction.Handle:x}, begin: {begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin)}, end: {end.PrettyPrint(FdbKey.PrettyPrintMode.End)}, {snapshot}) => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_transaction_get_key</summary>
		public static FutureHandle TransactionGetKey(TransactionHandle transaction, KeySpanSelector selector, bool snapshot)
		{
			fixed (byte* ptrKey = selector.Key)
			{
				var future = NativeMethods.fdb_transaction_get_key(transaction, ptrKey, selector.Key.Length, selector.OrEqual, selector.Offset, snapshot);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_get_key(tr: 0x{transaction.Handle:x}, {selector.ToString()}, {snapshot}) => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_transaction_get_addresses_for_key</summary>
		public static FutureHandle TransactionGetAddressesForKey(TransactionHandle transaction, ReadOnlySpan<byte> key)
		{
			if (key.Length == 0) throw new ArgumentException("Key cannot be null or empty", nameof(key));

			fixed (byte* ptrKey = key)
			{
				var future = NativeMethods.fdb_transaction_get_addresses_for_key(transaction, ptrKey, key.Length);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_get_addresses_for_key(tr: 0x{transaction.Handle:x}, key: '{FdbKey.Dump(key)}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary>fdb_transaction_get_range_split_points</summary>
		public static FutureHandle TransactionGetRangeSplitPoints(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize)
		{
			fixed (byte* ptrBeginKey = beginKey)
			fixed (byte* ptrEndKey = endKey)
			{
				var future = NativeMethods.fdb_transaction_get_range_split_points(transaction, ptrBeginKey, beginKey.Length, ptrEndKey, endKey.Length, chunkSize);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_get_range_split_points(tr: 0x{transaction.Handle:x}, begin: '{FdbKey.Dump(beginKey)}', end: '{FdbKey.Dump(endKey)}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary><c>fdb_transaction_get_estimated_range_size_bytes</c></summary>
		public static FutureHandle TransactionGetEstimatedRangeSizeBytes(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
		{
			fixed (byte* ptrBeginKey = beginKey)
			fixed (byte* ptrEndKey = endKey)
			{
				var future = NativeMethods.fdb_transaction_get_estimated_range_size_bytes(transaction, ptrBeginKey, beginKey.Length, ptrEndKey, endKey.Length);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_get_estimated_range_size_bytes(tr: 0x{transaction.Handle:x}, begin: '{FdbKey.Dump(beginKey)}', end: '{FdbKey.Dump(endKey)}') => 0x{future.Handle:x}");
#endif
				return future;
			}
		}

		/// <summary><c>fdb_future_get_value</c></summary>
		public static FdbError FutureGetValue(FutureHandle future, out bool valuePresent, out ReadOnlySpan<byte> value)
		{
			Contract.Debug.Requires(future != null);

			var err = NativeMethods.fdb_future_get_value(future, out valuePresent, out byte* ptr, out int valueLength);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_value(0x{future.Handle:x}) => err={err}, present={valuePresent}, valueLength={valueLength}");
#endif
			if (valueLength > 0 && ptr != null)
			{
				value = new ReadOnlySpan<byte>(ptr, valueLength);
			}
			else
			{
				value = default;
			}
			return err;
		}

		/// <summary><c>fdb_future_get_key</c></summary>
		public static FdbError FutureGetKey(FutureHandle future, out ReadOnlySpan<byte> key)
		{
			var err = NativeMethods.fdb_future_get_key(future, out byte* ptr, out int keyLength);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_key(0x{future.Handle:x}) => err={err}, keyLength={keyLength}");
#endif

			// note: fdb_future_get_key is allowed to return NULL for the empty key (not to be confused with a key that has an empty value)
			Contract.Debug.Assert((uint) keyLength <= Fdb.MaxKeySize);

			if (keyLength > 0 && ptr != null)
			{
				key = new(ptr, keyLength);
			}
			else
			{ // from the spec: "If a key selector would otherwise describe a key off the beginning of the database, it instead resolves to the empty key ''."
				key = default;
			}

			return err;
		}

		/// <summary><c>fdb_future_get_keyvalue_array</c></summary>
		public static FdbError FutureGetKeyValueArray(FutureHandle future, ArrayPool<byte>? pool, out KeyValuePair<Slice, Slice>[]? result, out bool more, out SliceOwner buffer, out int dataBytes)
		{
			result = null;
			buffer = default;
			dataBytes = 0;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* kvp, out int count, out more);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_keyvalue_array(0x{future.Handle:x}) => err={err}, count={count}, more={more}");
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				result = count > 0 ? new KeyValuePair<Slice, Slice>[count] : [ ];

				if (count > 0)
				{ // convert the FdbKeyValueNative result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total buffer size needed
					long sum = 0;
					for (int i = 0; i < count; i++)
					{
						uint kl = kvp[i].KeyLength;
						uint vl = kvp[i].ValueLength;
						if (kl > int.MaxValue) throw ThrowHelper.InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						sum += kl;
						if (vl > int.MaxValue) throw ThrowHelper.InvalidOperationException("A Value has a length that is larger than a signed 32-bit int!");
						sum += vl;
					}
					if (sum > int.MaxValue) throw ThrowHelper.NotSupportedException("Cannot read more than 2GB of key/value data in a single batch!");

					dataBytes = (int) sum;

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at any time...

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = pool?.Rent(dataBytes) ?? new byte[dataBytes];

					int p = 0;
					Span<byte> tail = page.AsSpan();
					for (int i = 0; i < result.Length; i++)
					{
						int kl = (int) kvp[i].KeyLength;
						new ReadOnlySpan<byte>(kvp[i].Key, kl).CopyTo(tail);
						var key = page.AsSlice(p, kl);
						p += kl;
						tail = tail.Slice(kl);

						int vl = (int) kvp[i].ValueLength;
						if (vl == 0)
						{
							result[i] = new(key, Slice.Empty);
							continue;
						}

						new ReadOnlySpan<byte>(kvp[i].Value, vl).CopyTo(tail);
						var value = page.AsSlice(p, vl);
						p += vl;
						result[i] = new(key, value);
						tail = tail.Slice(vl);
					}
					Contract.Debug.Assert(p == dataBytes);

					buffer = SliceOwner.Create(page.AsSlice(0, p), pool);
				}
			}

			return err;
		}

		/// <summary><c>fdb_future_get_keyvalue_array</c></summary>
		public static int VisitKeyValueArray<TState>(FutureHandle future, TState state, FdbKeyValueAction<TState> visitor, out bool more, out Slice first, out Slice last, out int totalBytes)
		{
			first = default;
			last = default;
			totalBytes = 0;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* ptr, out int count, out more);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_keyvalue_array(0x{future.Handle:x}) => err={err}, count={count}, more={more}");
#endif
			DieOnError(err);

			Contract.Debug.Assert(count >= 0, "Return count was negative");
			// note: the entries are read via direct pointer indexing (the portable Span<T> of netstandard2.0 rejects types with pointer fields, like FdbKeyValue)

			if (count == 0)
			{
				return 0;
			}

			// convert the data using the raw native buffer
			long sum = 0;
			for (int i = 0; i < count; i++)
			{
				var k = ptr[i].GetKey();
				var v = ptr[i].GetValue();
				sum += k.Length;
				sum += v.Length;
				visitor(state, k, v);
			}

			// we also need to grab the first and last key (for pagination)
			first = Slice.FromBytes(ptr[0].GetKey());
			last = count > 1 ? Slice.FromBytes(ptr[count - 1].GetKey()) : first;
			totalBytes = checked((int) sum);

			return count;
		}

		/// <summary><c>fdb_future_get_keyvalue_array</c></summary>
		public static TResult[] FutureGetKeyValueArray<TState, TResult>(FutureHandle future, TState state, FdbKeyValueDecoder<TState, TResult> decoder, out bool more, out Slice first, out Slice last, out int totalBytes)
		{
			first = default;
			last = default;
			totalBytes = 0;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* ptr, out int count, out more);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_keyvalue_array(0x{future.Handle:x}) => err={err}, count={count}, more={more}");
#endif
			DieOnError(err);

			Contract.Debug.Assert(count >= 0, "Return count was negative");
			// note: the entries are read via direct pointer indexing (the portable Span<T> of netstandard2.0 rejects types with pointer fields, like FdbKeyValue)

			if (count == 0)
			{
				return [ ];
			}

			// convert the data using the raw native buffer
			var result = new TResult[count];
			long sum = 0;
			for (int i = 0; i < count; i++)
			{
				var k = ptr[i].GetKey();
				var v = ptr[i].GetValue();
				sum += k.Length;
				sum += v.Length;
				result[i] = decoder(state, k, v);
			}

			// we also need to grab the first and last key (for pagination)
			first = Slice.FromBytes(ptr[0].GetKey());
			last = count > 1 ? Slice.FromBytes(ptr[count - 1].GetKey()) : first;
			totalBytes = checked((int) sum);

			return result;
		}

		/// <summary><c>fdb_future_get_keyvalue_array</c></summary>
		public static FdbError FutureGetKeyValueArrayKeysOnly(FutureHandle future, out KeyValuePair<Slice, Slice>[]? result, out bool more, out int dataBytes)
		{
			result = null;
			dataBytes = 0;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* kvp, out int count, out more);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_keyvalue_array(0x{future.Handle:x}) => err={err}, count={count}, more={more}");
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				result = count > 0 ? new KeyValuePair<Slice, Slice>[count] : [ ];

				if (count > 0)
				{ // convert the FdbKeyValueNative result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total buffer size needed
					long sum = 0;
					for (int i = 0; i < count; i++)
					{
						uint kl = kvp[i].KeyLength;
						uint vl = kvp[i].ValueLength;
						if (kl > int.MaxValue) throw new InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						if (vl > int.MaxValue) throw new InvalidOperationException("A Value has a length that is larger than a signed 32-bit int!");
						sum += kl;
					}
					if (sum > int.MaxValue) throw new NotSupportedException("Cannot read more than 2GB of key data in a single batch!");

					dataBytes = (int) sum;

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at any time...

					//TODO: protect against too much memory allocated ?
					// what would be a good max value? we need to at least be able to handle FDB_STREAMING_MODE_WANT_ALL

					//TODO: some keys/values will be small (32 bytes or fewer) while other will be big
					//consider having to copy methods, optimized for each scenario ?

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[sum];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						int kl = checked((int) kvp[i].KeyLength);
						new ReadOnlySpan<byte>(kvp[i].Key, kl).CopyTo(page.AsSpan(p));
						var key = page.AsSlice(p, kl);
						p += kl;

						result[i] = new(key, default);
					}

					Contract.Debug.Assert(p == sum);
				}
			}

			return err;
		}

		/// <summary><c>fdb_future_get_keyvalue_array</c></summary>
		public static FdbError FutureGetKeyValueArrayValuesOnly(FutureHandle future, out KeyValuePair<Slice, Slice>[]? result, out bool more, out Slice first, out Slice last, out int dataBytes)
		{
			result = null;
			first = default;
			last = default;
			dataBytes = 0;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* kvp, out int count, out more);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_keyvalue_array(0x{future.Handle:x}) => err={err}, count={count}, more={more}");
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				result = count > 0 ? new KeyValuePair<Slice, Slice>[count] : [ ];

				if (count > 0)
				{ // convert the FdbKeyValueNative result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					int end = count - 1;

					// first pass to compute the total buffer size needed
					long sum = 0;
					for (int i = 0; i < count; i++)
					{
						//TODO: protect against negative values or values too big ?
						uint kl = kvp[i].KeyLength;
						uint vl = kvp[i].ValueLength;
						if (kl > int.MaxValue) throw new InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						if (vl > int.MaxValue) throw new InvalidOperationException("A Value has a length that is larger than a signed 32-bit int!");
						if (i == 0 || i == end) sum += kl;
						sum += vl;
					}
					if (sum > int.MaxValue) throw new NotSupportedException("Cannot read more than 2GB of value data in a single batch!");

					dataBytes = (int) sum;

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at any time...

					//TODO: protect against too much memory allocated ?
					// what would be a good max value? we need to at least be able to handle FDB_STREAMING_MODE_WANT_ALL

					//TODO: some keys/values will be small (32 bytes or fewer) while other will be big
					//consider having to copy methods, optimized for each scenario ?

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[sum];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						// note: even if we only read the values, we still need to keep the first and last keys,
						// because we will need them for pagination when reading multiple ranges (ex: last key will be used as selector for next chunk when going forward)
						if (i == 0 || i == end)
						{
							int kl = checked((int) kvp[i].KeyLength);
							new ReadOnlySpan<byte>(kvp[i].Key, kl).CopyTo(page.AsSpan(p));
							var key = page.AsSlice(p, kl);
							p += kl;
							if (i == 0) first = key; else last = key;
						}

						int vl = checked((int) kvp[i].ValueLength);
						new ReadOnlySpan<byte>(kvp[i].Value, vl).CopyTo(page.AsSpan(p));
						var value = page.AsSlice(p, vl);
						p += vl;

						result[i] = new KeyValuePair<Slice, Slice>(default, value);
					}
					Contract.Debug.Assert(p == sum);
				}
			}

			return err;
		}

		/// <summary><c>fdb_future_get_key_array</c></summary>
		public static FdbError FutureGetKeyArray(FutureHandle future, out Slice[]? result)
		{
			result = null;

			var err = NativeMethods.fdb_future_get_key_array(future, out FdbKeyNative* kvp, out int count);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_key_array(0x{future.Handle:x}) => err={err}, count={count}");
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				if (count > 0)
				{ // convert the FdbKeyValueNative result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					//TODO: PERF: find a way to use Memory Pooling for this?
					result = new Slice[count];

					// in order to reduce allocations, we want to merge all keys
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total size needed
					long total = 0;
					for (int i = 0; i < count; i++)
					{
						uint kl = kvp[i].Length;
						if (kl > int.MaxValue) throw new InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						total += kl;
					}
					if (total > int.MaxValue) throw new NotSupportedException("Cannot read more than 2GB of key data in a single batch!");

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[total];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						int kl = checked((int) kvp[i].Length);
						new ReadOnlySpan<byte>(kvp[i].Key, kl).CopyTo(page.AsSpan(p));
						var key = page.AsSlice(p, kl);
						p += kl;

						result[i] = key;
					}

					Contract.Debug.Assert(p == total);
				}
				else
				{
					result = [ ];
				}
			}

			return err;
		}

		/// <summary><c>fdb_future_get_string_array</c></summary>
		public static FdbError FutureGetStringArray(FutureHandle future, out string[]? result)
		{
			result = null;

			var err = NativeMethods.fdb_future_get_string_array(future, out byte** strings, out int count);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_string_array(0x{future.Handle:x}) => err={err}, count={count}");
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");


				if (count > 0)
				{ // convert the keyvalue result into an array

					Contract.Debug.Assert(strings != null, "We have results but array pointer was null");

					result = new string[count];

					//TODO: if pointers are corrupted, or memory is garbled, we could very well walk around the heap, randomly copying a bunch of stuff (like passwords or jpegs of cats...)
					// there is no real way to ensure that pointers are valid, except maybe having a maximum valid size for strings, and they should probably only contain legible text ?

					for (int i = 0; i < result.Length; i++)
					{
						result[i] = ToManagedString(strings[i])!;
					}
				}
				else
				{
					result = [ ];
				}
			}

			return err;
		}

		/// <summary><c>fdb_future_get_key</c></summary>
		public static FdbError FutureGetVersionStamp(FutureHandle future, out VersionStamp stamp)
		{
			var err = NativeMethods.fdb_future_get_key(future, out var ptr, out var keyLength);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_future_get_key(0x{future.Handle:x}) => err={err}, keyLength={keyLength}");
#endif

			if (keyLength != 10 || ptr == null)
			{
				//REVIEW: should we fail if len != 10? (would need some MAJOR change in the fdb C API?)
				stamp = default;
				return err;
			}

			VersionStamp.ReadUnsafe(new ReadOnlySpan<byte>(ptr, 10), out stamp);
			return err;
		}

		/// <summary><c>fdb_transaction_set</c></summary>
		public static void TransactionSet(TransactionHandle transaction, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			fixed (byte* pKey = key)
			fixed (byte* pValue = value)
			{
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_set(tr: 0x{transaction.Handle:x}, key: '{FdbKey.Dump(key)}', value: '{FdbKey.Dump(value)}')");
#endif
				NativeMethods.fdb_transaction_set(transaction, pKey, key.Length, pValue, value.Length);
			}
		}

		/// <summary><c>fdb_transaction_atomic_op</c></summary>
		public static void TransactionAtomicOperation(TransactionHandle transaction, ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType operationType)
		{
			fixed (byte* pKey = key)
			fixed (byte* pParam = param)
			{
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_atomic_op(tr: 0x{transaction.Handle:x}, key: '{FdbKey.Dump(key)}', param: '{FdbKey.Dump(param)}', {operationType})");
#endif
				NativeMethods.fdb_transaction_atomic_op(transaction, pKey, key.Length, pParam, param.Length, operationType);
			}
		}

		/// <summary><c>fdb_transaction_clear</c></summary>
		public static void TransactionClear(TransactionHandle transaction, ReadOnlySpan<byte> key)
		{
			fixed (byte* pKey = key)
			{
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_clear(tr: 0x{transaction.Handle:x}, key: '{FdbKey.Dump(key)}')");
#endif
				NativeMethods.fdb_transaction_clear(transaction, pKey, key.Length);
			}
		}

		/// <summary><c>fdb_transaction_clear_range</c></summary>
		public static void TransactionClearRange(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
		{
			fixed (byte* pBeginKey = beginKey)
			fixed (byte* pEndKey = endKey)
			{
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_clear_range(tr: 0x{transaction.Handle:x}, beginKey: '{FdbKey.Dump(beginKey)}, endKey: '{FdbKey.Dump(endKey)}')");
#endif
				NativeMethods.fdb_transaction_clear_range(transaction, pBeginKey, beginKey.Length, pEndKey, endKey.Length);
			}
		}

		/// <summary><c>fdb_transaction_add_conflict_range</c></summary>
		public static FdbError TransactionAddConflictRange(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, FdbConflictRangeType type)
		{
			fixed (byte* pBeginKey = beginKey)
			fixed (byte* pEndKey = endKey)
			{
#if DEBUG_NATIVE_CALLS
				LogNative($"fdb_transaction_add_conflict_range(tr: 0x{transaction.Handle:x}, beginKey: '{FdbKey.Dump(beginKey)}, endKey: '{FdbKey.Dump(endKey)}', {type})");
#endif
				return NativeMethods.fdb_transaction_add_conflict_range(transaction, pBeginKey, beginKey.Length, pEndKey, endKey.Length, type);
			}
		}

		/// <summary><c>fdb_transaction_get_approximate_size</c></summary>
		public static FutureHandle TransactionGetApproximateSize(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_approximate_size(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			LogNative($"fdb_transaction_get_approximate_size(tr: 0x{transaction.Handle:x}) => 0x{future.Handle:x}");
#endif
			return future;
		}

		#endregion

	}

}
