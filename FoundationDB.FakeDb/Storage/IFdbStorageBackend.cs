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

namespace FoundationDB.Storage
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Testing;

	/// <summary>The storage under a store: what its committed state is made of, how a committed generation becomes durable, and how long a published version stays readable.</summary>
	/// <remarks>
	/// <para>Everything a backend does NOT decide is the shared engine: read-your-writes resolution, conflict detection, atomic mutation arithmetic, watches, versionstamps and the whole transaction lifetime all live in <see cref="FakeDbStore"/> and run identically over every backend. That split is the point - the subtle semantics are written once, and a storage change cannot silently fork them.</para>
	/// <para>The store calls <see cref="CreateInitialSnapshot"/> once from its constructor, and <see cref="Publish"/> under its global WRITE lock. <see cref="Pin"/> and <see cref="Release"/> are called by concurrent readers and must be thread-safe.</para>
	/// </remarks>
	public interface IFdbStorageBackend : IDisposable
	{

		/// <summary>Builds the store's first committed snapshot, carrying the system keys of <see cref="SpecialKeys"/> that every database is born with.</summary>
		/// <param name="initialVersion">Version a fresh database starts at. A backend that opens EXISTING committed state ignores it and reports that state's own version.</param>
		Snapshot CreateInitialSnapshot(long initialVersion);

		/// <summary>Opens a transaction handler monomorphized over this backend's cursor type.</summary>
		/// <remarks>The handler is generic over the cursor so the JIT stamps a dedicated copy per backend and inlines the per-key seam calls in the scan loops. Only the backend knows which concrete cursor closes that generic, which is why this is its job rather than the store's.</remarks>
		IFdbTransactionHandler CreateTransaction(FakeDbStore store, FdbOperationContext context);

		/// <summary>Makes a committed generation durable, and returns the committed store the published snapshot reads through.</summary>
		/// <remarks>A persistent backend flushes here and hands back a readable re-wrap at the new durable root; a backend whose committed state IS the published state returns <paramref name="committed"/> unchanged.</remarks>
		IFdbCommittedStore Publish(IFdbCommittedStore committed, long commitVersion);

		/// <summary>Number of previously-published versions that stay readable behind the current one.</summary>
		/// <remarks><see cref="int.MaxValue"/> retains every version ever published, which is what makes the whole history inspectable; a backend that reclaims storage retains only the window its pages can still serve, and a read at an evicted version fails with <see cref="FdbError.TransactionTooOld"/> like a real cluster.</remarks>
		int RetainedVersions { get; }

		/// <summary>Holds a published version readable for as long as a transaction is reading it.</summary>
		/// <remarks>Called under the store's read lock, so the version is always one the store still retains. A backend that reclaims storage counts readers here; one that never reclaims does nothing.</remarks>
		void Pin(long version);

		/// <summary>Releases a <see cref="Pin"/>, once per pin, when the transaction is done with its snapshot.</summary>
		void Release(long version);

	}

}
