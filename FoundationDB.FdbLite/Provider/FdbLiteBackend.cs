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

namespace FoundationDB.FdbLite
{
	using FoundationDB.Storage;
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using SnowBank.Collections.CacheOblivious;

	/// <summary>Persistent storage over the memory-mapped engine: committed state lives in the engine's pages and survives the process for a file-backed store.</summary>
	/// <remarks>
	/// <para>Mode differences against the retain-everything in-memory backend, by design: there is NO full version history, because pages are reclaimed. A transaction reads the version it started at through a read PIN taken here, the readable window is the current version plus the immediately-previous one (a real cluster's recent-version window), and a read outside it fails with <see cref="FdbError.TransactionTooOld"/>.</para>
	/// <para>The conflict history stays in memory whichever backend is used: a restarted process has no in-flight transactions to conflict with.</para>
	/// </remarks>
	public sealed class FdbLiteBackend : IFdbStorageBackend
	{

		public FdbLiteBackend(FdbLiteEngine engine, bool disposeEngine = true, bool retainEveryVersion = false)
		{
			Contract.NotNull(engine);
			this.Engine = engine;
			this.DisposeEngine = disposeEngine;
			this.RetainEveryVersion = retainEveryVersion;
			if (retainEveryVersion)
			{ // drop the reclamation floor so no generation is ever promoted to reusable
				engine.RetainFloor = 0;
			}
		}

		/// <summary>The storage engine holding the committed state</summary>
		public FdbLiteEngine Engine { get; }

		/// <summary>Whether every published version stays readable forever, instead of only the recent-version window.</summary>
		/// <remarks>Retaining everything means nothing is ever reclaimed, so the store grows without bound and its whole history stays inspectable. That is the emulator configuration; a store that must bound its footprint leaves this off and gets a cluster-like window instead.</remarks>
		private bool RetainEveryVersion { get; }

		/// <summary>Whether disposing the store disposes the engine (false when the engine outlives the store, e.g. across benchmark iterations)</summary>
		private bool DisposeEngine { get; }

		/// <summary>Read pins per database version, shared by every transaction reading that version</summary>
		private Dictionary<long, (FdbLiteEngine.ReadSnapshot Pin, int RefCount)> PinsByVersion { get; } = new();

#if NET9_0_OR_GREATER
		private readonly System.Threading.Lock PinLock = new();
#else
		private readonly object PinLock = new();
#endif

		/// <inheritdoc />
		/// <remarks>An engine that already holds committed state is adopted at ITS version, and <paramref name="initialVersion"/> is ignored: the durable state is the truth for a store reopened over an existing file.</remarks>
		public Snapshot CreateInitialSnapshot(long initialVersion)
		{
			var engine = this.Engine;
			if (engine.Durable.DatabaseVersion == 0 && engine.Durable.KeyCount == 0)
			{ // fresh store: seed the same system keys a fresh in-memory store gets
				var stamp = VersionStamp.Complete((ulong) initialVersion, 0);
				engine.Write((ulong) initialVersion, writer =>
				{
					writer.Insert(SpecialKeys.SystemRoot.Span, SpecialKeys.SystemRootSentinelValue.Span);
					writer.Insert(SpecialKeys.SystemMetadataVersion.Span, stamp.ToSlice().Span);
					writer.Insert(SpecialKeys.SystemEnd.Span, default);
				});
			}

			var durable = engine.Durable;
			return new Snapshot(
				(long) durable.DatabaseVersion,
				new FdbLiteCommittedStore(engine, durable.RootPageId, durable.KeyCount),
				new ColaRangeDictionary<Key, long>(Key.Comparer.Default),
				VersionStamp.Complete(durable.DatabaseVersion, 0),
				new Arena(128 * 1024, 512 * 1024, ArrayPool<byte>.Shared)
			);
		}

		/// <inheritdoc />
		public IFdbTransactionHandler CreateTransaction(FdbEmulatedDatabase store, FdbOperationContext context)
			=> new FdbEmulatedDatabase.TransactionHandler<FdbLiteCommittedCursor>(store, context);

		/// <inheritdoc />
		public IFdbCommittedStore Publish(IFdbCommittedStore committed, long commitVersion)
		{
			// durability first: the engine runs the two-flush commit protocol for this generation
			var writable = (FdbLiteCommittedStore) committed;
			this.Engine.Commit(writable.Writer!, (ulong) commitVersion);

			// then freeze: the published snapshot reads through a plain readable store at the new durable root
			var durable = this.Engine.Durable;
			return new FdbLiteCommittedStore(this.Engine, durable.RootPageId, durable.KeyCount);
		}

		/// <inheritdoc />
		/// <remarks>Retaining everything, every version ever published stays readable. Otherwise one version behind the current one, matching a real cluster's recent-version window; older generations have had their pages reclaimed.</remarks>
		public int RetainedVersions => this.RetainEveryVersion ? int.MaxValue : 1;

		/// <inheritdoc />
		public void Pin(long version)
		{
			if (this.RetainEveryVersion)
			{ // nothing is ever reclaimed, so every published root stays readable and there is no horizon to hold
				return;
			}

			lock (this.PinLock)
			{
				if (this.PinsByVersion.TryGetValue(version, out var entry))
				{
					this.PinsByVersion[version] = (entry.Pin, entry.RefCount + 1);
				}
				else
				{ // taken under the store's read lock, so the version is the current or the retained previous one
					if (!this.Engine.TryBeginReadAtVersion((ulong) version, out var pin))
					{
						throw new FdbException(FdbError.TransactionTooOld, $"Version {version} is no longer retained by the persistent store");
					}
					this.PinsByVersion[version] = (pin, 1);
				}
			}
		}

		/// <inheritdoc />
		public void Release(long version)
		{
			if (this.RetainEveryVersion)
			{
				return;
			}

			lock (this.PinLock)
			{
				if (!this.PinsByVersion.TryGetValue(version, out var entry))
				{
					return;
				}
				if (entry.RefCount > 1)
				{
					this.PinsByVersion[version] = (entry.Pin, entry.RefCount - 1);
				}
				else
				{
					this.PinsByVersion.Remove(version);
					this.Engine.EndRead(in entry.Pin);
				}
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (this.DisposeEngine)
			{
				this.Engine.Dispose();
			}
		}

	}

}
