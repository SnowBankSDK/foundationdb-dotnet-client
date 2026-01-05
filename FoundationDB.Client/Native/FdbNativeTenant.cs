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

// enable this to capture the stacktrace of the ctor, when troubleshooting leaked database handles
//#define CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using FoundationDB.Client.Core;

	/// <summary>Wraps a native FDBTenant* handle</summary>
	internal sealed class FdbNativeTenant : IFdbTenantHandler
	{
#if CAPTURE_STACKTRACES
		private readonly StackTrace m_stackTrace;
#endif

		public FdbNativeTenant(FdbNativeDatabase db, TenantHandle handle)
		{
			Contract.NotNull(db);
			Contract.NotNull(handle);

			this.Handle = handle;
			this.Database = db;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

#if DEBUG
		// We add a destructor in DEBUG builds to help track leaks of databases...
		~FdbNativeTenant()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A tenant handle (" + m_handle + ") was leaked by " + m_stackTrace);
#endif
			// If you break here, that means that a native tenant handler was leaked by a FdbTenant instance (or that the tenant instance was leaked)
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			Dispose(false);
		}
#endif

		private TenantHandle Handle { get; }

		public FdbNativeDatabase Database { get; }
		IFdbDatabaseHandler IFdbTenantHandler.Database => this.Database;

		/// <inheritdoc />
		public bool IsClosed { get; }

		/// <inheritdoc />
		public IFdbTransactionHandler CreateTransaction(FdbOperationContext context)
		{
			TransactionHandle? handle = null;
			try
			{
				var err = FdbNative.TenantCreateTransaction(this.Handle, out handle);
				FdbNative.DieOnError(err);

				return new FdbNativeTransaction(this.Database, this, handle);
			}
			catch(Exception)
			{
				handle?.Dispose();
				throw;
			}
		}

		public Task<long> GetIdAsync(CancellationToken ct)
		{
			Contract.Debug.Assert(Fdb.BindingVersion >= 730);

			var future = FdbNative.TenantGetId(this.Handle);
			return FdbFuture.CreateTaskFromHandle(
				future,
				this,
				static (h, _) =>
				{
					var err = FdbNative.FutureGetInt64(h, out var value);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTenant[" + m_name + "].GetIdAsync() => err=" + err + ", id=" + value);
#endif
					FdbNative.DieOnError(err);
					return value;
				},
				ct
			);

		}

		#region IDisposable...

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Dispose of the handle
				if (!this.Handle.IsClosed) this.Handle.Dispose();
			}
		}

		#endregion

	}

}
