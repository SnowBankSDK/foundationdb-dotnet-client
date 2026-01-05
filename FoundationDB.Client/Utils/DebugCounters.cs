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

#pragma warning disable CA2211

namespace FoundationDB.Client.Utils
{
	/// <summary>Simple debug counters container that is used to troubleshoot alloc/free problems...</summary>
	public static class DebugCounters
	{

		/// <summary>Total number of <see cref="FoundationDB.Client.Native.DatabaseHandle"/> instance created, since the start of the application</summary>
		public static long DatabaseHandlesTotal = 0;

		/// <summary>Number of currently active <see cref="FoundationDB.Client.Native.DatabaseHandle"/> instances</summary>
		public static long DatabaseHandles = 0;

		/// <summary>Total number of <see cref="FoundationDB.Client.Native.TenantHandle"/> instance created, since the start of the application</summary>
		public static long TenantHandlesTotal = 0;

		/// <summary>Number of currently active <see cref="FoundationDB.Client.Native.TenantHandle"/> instances</summary>
		public static long TenantHandles = 0;

		/// <summary>Total number of <see cref="FoundationDB.Client.Native.TransactionHandle"/> instance created, since the start of the application</summary>
		public static long TransactionHandlesTotal = 0;

		/// <summary>Number of currently active <see cref="FoundationDB.Client.Native.TransactionHandle"/> instances</summary>
		public static long TransactionHandles = 0;

		/// <summary>Total number of <see cref="FoundationDB.Client.Native.FutureHandle"/> instance created, since the start of the application</summary>
		public static long FutureHandlesTotal = 0;

		/// <summary>Number of currently active <see cref="FoundationDB.Client.Native.FutureHandle"/> instances</summary>
		public static long FutureHandles = 0;

		/// <summary>Total number of callbacks registered on futures, since the start of the application</summary>
		public static long CallbackHandlesTotal = 0;

		/// <summary>Number of callbacks currently registered on active futures</summary>
		public static long CallbackHandles = 0;

	}

}
