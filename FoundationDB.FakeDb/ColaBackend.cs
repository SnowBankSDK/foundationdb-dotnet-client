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
	using System.Buffers;
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Storage;
	using SnowBank.Collections.CacheOblivious;

	/// <summary>The purely in-memory storage backend: COLA ordered dictionaries, no engine, no durability.</summary>
	/// <remarks>
	/// <para>The FakeDb sibling's own storage. A published version's store IS the copy that was mutated (no freeze step, nothing durable to order), every published version is retained (the whole history stays inspectable, at unbounded growth, the right trade for a store that lives as long as a test), and pins are no-ops because nothing is ever reclaimed.</para>
	/// </remarks>
	public sealed class ColaBackend : IFdbStorageBackend
	{

		/// <inheritdoc />
		public Snapshot CreateInitialSnapshot(long initialVersion)
		{
			var stamp = VersionStamp.Complete((ulong) initialVersion, 0);
			var data = new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default);
			// the same system keys every fresh database gets, whichever backend created it
			data[SpecialKeys.SystemRoot] = new Value(SpecialKeys.SystemRootSentinelValue);
			data[SpecialKeys.SystemMetadataVersion] = new Value(stamp.ToSlice());
			data[SpecialKeys.SystemEnd] = default;
			return new Snapshot(
				initialVersion,
				new ColaCommittedStore(data),
				new ColaRangeDictionary<Key, long>(Key.Comparer.Default),
				stamp,
				new Arena(128 * 1024, 512 * 1024, ArrayPool<byte>.Shared)
			);
		}

		/// <inheritdoc />
		public IFdbTransactionHandler CreateTransaction(FdbEmulatedDatabase store, FdbOperationContext context)
			=> new FdbEmulatedDatabase.TransactionHandler<ColaCommittedCursor>(store, context);

		/// <inheritdoc />
		/// <remarks>The mutated copy is already the next committed state: nothing to flush, nothing to freeze.</remarks>
		public IFdbCommittedStore Publish(IFdbCommittedStore committed, long commitVersion) => committed;

		/// <inheritdoc />
		/// <remarks>Everything is retained: every published version stays readable forever.</remarks>
		public int RetainedVersions => int.MaxValue;

		/// <inheritdoc />
		public void Pin(long version)
		{
		}

		/// <inheritdoc />
		public void Release(long version)
		{
		}

		/// <inheritdoc />
		public void Dispose()
		{
		}

	}

}
