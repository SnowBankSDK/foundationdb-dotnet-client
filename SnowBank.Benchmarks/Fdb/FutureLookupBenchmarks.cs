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
	using System.Collections.Concurrent;
	using System.Runtime.InteropServices;
	using BenchmarkDotNet.Attributes;
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;

	/// <summary>Isolates the cookie-to-future lookup cost of the async FDBFuture path. Three variants of the same
	/// register/resolve/unregister round-trip a future pays between construction and callback fire: the per-generic-type
	/// <c>ConcurrentDictionary</c> mechanism (replicated here faithfully - on modern targets it was replaced, but it is
	/// still what the netstandard2.0 lite target ships), the raw <see cref="GCHandle"/> alloc/deref/free floor, and the
	/// SHIPPED production path (<c>RegisterCallback</c>/<c>UnregisterCallback</c>, GCHandle-based since the rework,
	/// including its DebugCounters accounting). The dictionary can be pre-populated, since under load it is never
	/// empty.</summary>
	[Config(typeof(FdbBenchConfig))]
	[MemoryDiagnoser]
	public class FutureLookupBenchmarks
	{

		/// <summary>Number of other in-flight futures registered in the dictionary while measuring.</summary>
		[Params(0, 1_000)]
		public int PendingFutures;

		private FdbFuture<long>? Future;

		private readonly List<LegacyEntry> Pending = [ ];

		#region Replica of the retired dictionary mechanism...

		// faithful copy of the pre-rework FdbFuture<T> statics: monotonic-long cookie, per-type map, m_key recheck
		// on resolve, Exchange + TryRemove(KVP) on unregister, DebugCounters accounting (the real ones)

		private sealed class LegacyEntry
		{
			public IntPtr Key;
		}

		private static readonly ConcurrentDictionary<long, LegacyEntry> Futures = new();

		private static long FutureCounter;

		private static IntPtr LegacyRegister(LegacyEntry entry)
		{
			long id = Interlocked.Increment(ref FutureCounter);
			var prm = new IntPtr(id);
			try { }
			finally
			{
				Volatile.Write(ref entry.Key, prm);
				Futures[prm.ToInt64()] = entry;
				Interlocked.Increment(ref DebugCounters.CallbackHandlesTotal);
				Interlocked.Increment(ref DebugCounters.CallbackHandles);
			}
			return prm;
		}

		private static LegacyEntry? LegacyResolve(IntPtr parameter)
		{
			if (Futures.TryGetValue(parameter.ToInt64(), out var entry))
			{
				if (Volatile.Read(ref entry.Key) == parameter)
				{
					return entry;
				}
			}
			return null;
		}

		private static void LegacyUnregister(LegacyEntry entry)
		{
			try { }
			finally
			{
				var key = Interlocked.Exchange(ref entry.Key, IntPtr.Zero);
				if (key != IntPtr.Zero)
				{
					if (Futures.TryRemove(KeyValuePair.Create(key.ToInt64(), entry)))
					{
						Interlocked.Decrement(ref DebugCounters.CallbackHandles);
					}
				}
			}
		}

		#endregion

		[GlobalSetup]
		public void Setup()
		{
			this.Future = FdbFuture.Create<long>(CancellationToken.None);
			for (int i = 0; i < this.PendingFutures; i++)
			{
				var entry = new LegacyEntry();
				LegacyRegister(entry);
				this.Pending.Add(entry);
			}
		}

		[GlobalCleanup]
		public void Cleanup()
		{
			foreach (var entry in this.Pending)
			{
				LegacyUnregister(entry);
			}
			this.Pending.Clear();
			this.Future?.Dispose();
		}

		private readonly LegacyEntry Entry = new();

		[Benchmark(Baseline = true)]
		public object? DictionaryRoundTrip()
		{
			var entry = this.Entry;
			var prm = LegacyRegister(entry);
			var found = LegacyResolve(prm);
			LegacyUnregister(entry);
			return found;
		}

		[Benchmark]
		public object? GcHandleRoundTrip()
		{
			var handle = GCHandle.Alloc(this.Future);
			var prm = GCHandle.ToIntPtr(handle);
			var recovered = GCHandle.FromIntPtr(prm);
			var found = recovered.Target;
			recovered.Free();
			return found;
		}

		[Benchmark]
		public object? ProductionRoundTrip()
		{
			var future = this.Future!;
			var prm = FdbFuture<long>.RegisterCallback(future);
			var found = GCHandle.FromIntPtr(prm).Target;
			FdbFuture<long>.UnregisterCallback(future);
			return found;
		}

	}

}
