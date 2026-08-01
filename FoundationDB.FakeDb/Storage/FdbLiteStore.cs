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

namespace FoundationDB.Storage.FdbLite
{
	using FoundationDB.Testing;

	/// <summary>A store over the memory-mapped engine: the shared emulator (transaction machinery, watches, conflict checking) with <see cref="FdbLiteBackend"/> as its storage instead of the in-memory one.</summary>
	/// <remarks>This type is only the opening surface: it chooses the backend and nothing else. Every behaviour difference against an in-memory store is described on <see cref="FdbLiteBackend"/>.</remarks>
	public class FdbLiteStore : FakeDbStore
	{

		public FdbLiteStore(FdbLiteEngine engine, int apiVersion = DEFAULT_API_VERSION, int protocolVersion = MAX_API_VERSION, long initialVersion = 0, TimeProvider? time = null, bool disposeEngine = true, bool retainEveryVersion = false)
			: base(new FdbLiteBackend(engine, disposeEngine, retainEveryVersion), apiVersion, protocolVersion, initialVersion, time)
		{
			this.Engine = engine;
		}

		/// <summary>The storage engine under this store</summary>
		public FdbLiteEngine Engine { get; }

		/// <summary>Opens (or creates) a file-backed store.</summary>
		public static FdbLiteStore OpenOrCreateFile(string path, FdbLiteGeometry geometry, int apiVersion = DEFAULT_API_VERSION, int protocolVersion = MAX_API_VERSION, TimeProvider? time = null)
			=> new(FdbLiteEngine.OpenOrCreateFile(path, geometry), apiVersion, protocolVersion, time: time);

		/// <summary>Creates a non-persistent store over the heap pager (the engine-under-test and future ephemeral mode).</summary>
		/// <param name="geometry">The geometry for the in-memory store.</param>
		/// <param name="apiVersion">The API version to use.</param>
		/// <param name="protocolVersion">The protocol version to use.</param>
		/// <param name="time">The time provider to use.</param>
		/// <param name="retainEveryVersion">Keeps every published version readable instead of a cluster-like recent-version window, at the cost of unbounded growth. This is the emulator configuration: it is what makes a store's whole history inspectable from a test.</param>
		public static FdbLiteStore CreateInMemory(FdbLiteGeometry geometry, int apiVersion = DEFAULT_API_VERSION, int protocolVersion = MAX_API_VERSION, TimeProvider? time = null, bool retainEveryVersion = false)
			=> new(FdbLiteEngine.Create(new FdbLiteHeapPager(geometry)), apiVersion, protocolVersion, time: time, retainEveryVersion: retainEveryVersion);

	}

}
