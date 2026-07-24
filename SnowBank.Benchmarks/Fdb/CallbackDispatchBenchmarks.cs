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

namespace SnowBank.Benchmarks
{
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using BenchmarkDotNet.Attributes;
	using FoundationDB.Client.Native;

	/// <summary>Isolates the native-to-managed transition cost of the future completion callback: the current marshaled
	/// <c>FdbNative.FdbFutureCallback</c> delegate (reverse-P/Invoke thunk built by
	/// <see cref="Marshal.GetFunctionPointerForDelegate{TDelegate}(TDelegate)"/>) against an
	/// <see cref="UnmanagedCallersOnlyAttribute"/> static entry point. Both are invoked through the same
	/// <c>delegate* unmanaged[Cdecl]</c> shape the fdb_c network thread uses, so each measurement pays one
	/// managed-to-unmanaged call plus the unmanaged-to-managed re-entry under test; the delta between the two
	/// variants is the marshaling thunk itself.</summary>
	[Config(typeof(FdbBenchConfig))]
	[MemoryDiagnoser]
	public unsafe class CallbackDispatchBenchmarks
	{

		private static int CallCount;

		/// <summary>Kept alive for the process lifetime, like the cached per-generic-type CallbackHandler delegates</summary>
		private static readonly FdbNative.FdbFutureCallback MarshaledHandler = static (_, _) => CallCount++;

		private static readonly IntPtr MarshaledEntryPoint = Marshal.GetFunctionPointerForDelegate(MarshaledHandler);

		[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
		private static void UnmanagedHandler(IntPtr future, IntPtr parameter) => CallCount++;

		[Benchmark(Baseline = true)]
		public void MarshaledDelegate()
		{
			((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>) MarshaledEntryPoint)(IntPtr.Zero, IntPtr.Zero);
		}

		[Benchmark]
		public void UnmanagedCallersOnlyPointer()
		{
			((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>) &UnmanagedHandler)(IntPtr.Zero, IntPtr.Zero);
		}

	}

}
