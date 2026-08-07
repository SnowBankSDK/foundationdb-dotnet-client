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

namespace FoundationDB.FakeDb
{
	using FoundationDB.Storage;

	/// <summary>The in-memory emulated database: the shared <see cref="FdbEmulatedDatabase"/> base over the default heap-engine backend, keeping every published version.</summary>
	/// <remarks>The base carries everything behavioral (read-your-writes, conflicts, watches, versionstamps, retry and buggify); this sibling only chooses the storage a test gets when nothing else is asked for.</remarks>
	public class FakeDbStore : FdbEmulatedDatabase
	{

		public FakeDbStore(int apiVersion = DEFAULT_API_VERSION, int protocolVersion = MAX_API_VERSION, long initialVersion = 0, TimeProvider? time = null)
			: base(CreateInMemoryBackend(), apiVersion, protocolVersion, initialVersion, time)
		{ }

		/// <summary>Builds the storage an emulator gets when nothing else is asked for: the COLA in-memory store, keeping every version.</summary>
		/// <remarks>The in-memory emulator is a CONFIGURATION of the storage engine rather than a separate implementation of it, so the semantics a test relies on - read-your-writes, conflict detection, watches, versionstamps - are exercised over the same storage that a persistent store uses. Retaining every version is what keeps the whole published history inspectable, and costs unbounded growth, which is the right trade for a store that lives as long as a test.</remarks>
		private static IFdbStorageBackend CreateInMemoryBackend() => new ColaBackend();

		/// <summary>Opens a store over an explicit storage backend.</summary>
		protected FakeDbStore(IFdbStorageBackend backend, int apiVersion, int protocolVersion, long initialVersion, TimeProvider? time)
			: base(backend, apiVersion, protocolVersion, initialVersion, time)
		{ }

	}

}
