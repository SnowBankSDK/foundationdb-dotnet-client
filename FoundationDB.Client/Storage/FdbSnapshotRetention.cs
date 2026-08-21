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

	/// <summary>Decides, after each published snapshot, which older retained snapshots the store drops.</summary>
	/// <remarks>Invoked under the store's global write lock, right after the new snapshot is published. The policy inspects the retained set through the context (oldest first, the just-published head last) and marks drops; the head cannot be dropped. A dropped version stops being readable: starting a read at it fails with transaction_too_old, while a transaction already holding a snapshot of it keeps reading (its pin protects it). The backend's own capability stays a ceiling: a backend that reclaims storage cannot serve versions its pages no longer hold, whatever the policy keeps.</remarks>
	public delegate void FdbSnapshotRetentionPolicy(FdbSnapshotRetentionContext context);

	/// <summary>A retained published version, as a retention policy sees it.</summary>
	/// <param name="Version">Commit version of the snapshot.</param>
	/// <param name="PublishedAt">Instant of its publication on the store's clock. A fake provider makes this virtual time.</param>
	public readonly record struct FdbRetainedSnapshot(long Version, DateTimeOffset PublishedAt);

	/// <summary>Retained-set view a <see cref="FdbSnapshotRetentionPolicy"/> inspects, and the drop collector it reclaims through.</summary>
	/// <remarks>One instance per store, reused between publishes; only ever touched under the store's write lock.</remarks>
	public sealed class FdbSnapshotRetentionContext
	{

		internal List<FdbRetainedSnapshot> Entries { get; } = [ ];

		internal List<long>? Dropped { get; private set; }

		internal TimeProvider Clock { get; set; } = TimeProvider.System;

		/// <summary>Number of retained snapshots, the head included.</summary>
		public int Count => this.Entries.Count;

		/// <summary>Retained snapshot by position, oldest first; index <c>Count - 1</c> is the head.</summary>
		public FdbRetainedSnapshot this[int index] => this.Entries[index];

		/// <summary>The just-published head.</summary>
		public FdbRetainedSnapshot Head => this.Entries[^1];

		/// <summary>The store's clock, which a fake provider virtualizes.</summary>
		public TimeProvider Time => this.Clock;

		/// <summary>Marks a retained version for dropping. The head is refused.</summary>
		public void Drop(long version)
		{
			if (version == this.Head.Version) throw new ArgumentException("The just-published head snapshot cannot be dropped.", nameof(version));
			(this.Dropped ??= [ ]).Add(version);
		}

		internal void Begin(TimeProvider clock)
		{
			this.Clock = clock;
			this.Dropped?.Clear();
		}

	}

	/// <summary>Built-in snapshot retention policies.</summary>
	public static class FdbSnapshotRetention
	{

		/// <summary>The window a real fdb cluster serves: a read older than about 5 seconds fails with transaction_too_old.</summary>
		public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

		/// <summary>Keeps every published version: the whole run stays inspectable, at unbounded growth. The forensic mode.</summary>
		public static readonly FdbSnapshotRetentionPolicy KeepEverything = static _ => { };

		/// <summary>Keeps the <paramref name="count"/> most recent versions, the head included.</summary>
		public static FdbSnapshotRetentionPolicy KeepLast(int count)
		{
			Contract.Positive(count);
			return ctx =>
			{
				for (int i = 0; i < ctx.Count - count; i++)
				{
					ctx.Drop(ctx[i].Version);
				}
			};
		}

		/// <summary>Keeps the versions published within <paramref name="window"/> of the head, on the store's clock.</summary>
		/// <remarks>With a fake provider the window is virtual time: a test that never advances its clock retains everything, and one that advances past the window ages versions out exactly as a real cluster would.</remarks>
		public static FdbSnapshotRetentionPolicy KeepWindow(TimeSpan window)
		{
			Contract.Requires(window > TimeSpan.Zero);
			return ctx =>
			{
				var horizon = ctx.Head.PublishedAt - window;
				for (int i = 0; i < ctx.Count - 1; i++)
				{
					if (ctx[i].PublishedAt >= horizon) break; // entries are publish-ordered: the first one inside the window ends the scan
					ctx.Drop(ctx[i].Version);
				}
			};
		}

	}

}
