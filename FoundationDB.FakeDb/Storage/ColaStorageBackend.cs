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
	using SnowBank.Collections.CacheOblivious;

	/// <summary>In-memory storage: a <see cref="ColaOrderedDictionary{TKey,TValue}"/> committed keyspace, arena-backed, retaining every version ever published.</summary>
	/// <remarks>Nothing here is durable and nothing is ever reclaimed, so a published snapshot stays readable forever and its arena views stay valid: pinning is free and publishing is the identity. That retain-everything behaviour is what makes a test able to walk the store's whole history.</remarks>
	public sealed class ColaStorageBackend : IFdbStorageBackend
	{

		private static ArrayPool<byte> GlobalPool { get; } = ArrayPool<byte>.Create();

		/// <inheritdoc />
		public Snapshot CreateInitialSnapshot(long initialVersion)
		{
			var stamp = VersionStamp.Complete((ulong) initialVersion, 0);
			var arena = new Arena(128 * 1024, 512 * 1024, GlobalPool);

			var data = new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default);
			data[SpecialKeys.SystemRoot] = arena.InternValue(SpecialKeys.SystemRootSentinelValue);
			data[SpecialKeys.SystemMetadataVersion] = arena.InternValue(stamp.ToSlice());
			data[SpecialKeys.SystemEnd] = Value.Empty;

			return new Snapshot(
				initialVersion,
				new ColaCommittedStore(data),
				new ColaRangeDictionary<Key, long>(Key.Comparer.Default),
				stamp,
				arena
			);
		}

		/// <inheritdoc />
		public IFdbTransactionHandler CreateTransaction(FakeDbStore store, FdbOperationContext context)
			=> new FakeDbStore.TransactionHandler<ColaCommittedCursor>(store, context);

		/// <inheritdoc />
		public IFdbCommittedStore Publish(IFdbCommittedStore committed, long commitVersion) => committed;

		/// <inheritdoc />
		public int RetainedVersions => int.MaxValue;

		/// <inheritdoc />
		public void Pin(long version) { }

		/// <inheritdoc />
		public void Release(long version) { }

		/// <inheritdoc />
		public void Dispose() { }

	}

}
