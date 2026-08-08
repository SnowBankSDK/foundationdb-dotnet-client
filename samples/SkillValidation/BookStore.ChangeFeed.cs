// Change-feed + retention for BookStore (partial). Every mutation in BookStore.cs calls LogChange,
// which appends a VersionStamp-ordered event and bumps a signal key. Other nodes subscribe to the feed
// to maintain an observable view. PurgeAsync trims the log so it can't grow without bound.

using System.Runtime.CompilerServices;   // [EnumeratorCancellation]
using System.Threading.Channels;
using FoundationDB.Client;
using SnowBank.Data.Json;                 // CrystalJson
using SnowBank.Data.Tuples;               // STuple
using SnowBank.Data.Tuples.Binary;        // TuPack
using SnowBank.Linq;                      // ToListAsync, FirstOrDefaultAsync

namespace SkillValidation;

public enum BookChangeKind { Put, Delete }

public sealed record BookChange
{
	public BookChangeKind Kind { get; init; }
	public string Id { get; init; } = "";
	public Book? Book { get; init; }   // the new value for Put; null for Delete
}

/// <summary>A change observed from the feed, tagged with its commit-ordered version (the resume cursor).</summary>
public readonly record struct BookChangeEntry(VersionStamp Version, BookChange Change);

/// <summary>
/// Thrown when a subscriber resumes from a cursor that the retention GC has already reclaimed past:
/// changes between the cursor and the trim horizon were deleted, so the feed is INCOMPLETE for this
/// subscriber (it was likely frozen long enough to be evicted). The subscriber must reload current
/// state and re-subscribe from "now", it cannot trust its incremental view any more.
/// </summary>
public sealed class ChangeFeedOutOfSyncException(VersionStamp cursor, VersionStamp trimHorizon)
	: Exception($"Change feed is out of sync: cursor {cursor} is older than the trim horizon {trimHorizon}; changes were missed.")
{
	public VersionStamp Cursor { get; } = cursor;
	public VersionStamp TrimHorizon { get; } = trimHorizon;
}

public sealed partial class BookStore
{
	private const int BatchSize = 256;

	public sealed partial class State
	{
		// Called by every mutation (Insert/Update/Patch/Delete) in BookStore.cs, in the SAME transaction
		// as the document write, so the feed can never disagree with the data.
		internal void LogChange(IFdbTransaction tr, BookChangeKind kind, string id, Book? book)
		{
			var stamp = tr.CreateUniqueVersionStamp();   // distinct per change, even several in one tx
			var change = new BookChange { Kind = kind, Id = id, Book = book };
			tr.SetVersionStampedKey(this.Subspace.Key(SUBSPACE_FEED, stamp), FdbValue.ToJson(change));
			tr.AtomicIncrement64(this.Subspace.Key(SUBSPACE_SIGNAL));   // wake every watcher; never conflicts
		}
	}

	#region Subscribe ...

	/// <summary>
	/// Streams changes in commit order, starting after <paramref name="after"/> (null = from the beginning).
	/// While streaming it keeps a per-subscriber lease (<c>(SUBSCRIBERS, subscriberId) -&gt; (leaseReadVersion, cursor)</c>)
	/// fresh, so the purge job knows this subscriber is alive and how far it has consumed. When caught up it
	/// watches the signal key, but never waits longer than <paramref name="heartbeat"/> without renewing the lease.
	/// </summary>
	public async IAsyncEnumerable<BookChangeEntry> ReadChangesAsync(
		IFdbDatabase db,
		Guid subscriberId,
		VersionStamp? after = null,
		TimeSpan? heartbeat = null,
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		// NOTE: `heartbeat` is only this consumer pacing ITS OWN renewals with a local timer, that needs no
		// cross-node agreement. The lease value and its expiry are measured in the DATABASE read version
		// (a single global clock all nodes share), never in local wall-clock time, see PurgeAsync.
		var hb = heartbeat ?? TimeSpan.FromSeconds(10);
		VersionStamp? cursor = after;

		while (!ct.IsCancellationRequested)
		{
			(BookChangeEntry[] Batch, FdbWatch? Watch) page = await db.ReadWriteAsync(async tr =>
			{
				var state = await this.Resolve(tr);
				var feed = state.Subspace.Key(SUBSPACE_FEED);

				// ONE round-trip: read the next page of changes after our cursor. Eviction is detected IN this
				// same GetRange, when the GC reclaims part of the log it leaves a TOMBSTONE (an empty value)
				// at the trim horizon. If we were reclaimed past, everything below the horizon is gone, so the
				// first entry we see is that tombstone (a null change) instead of a real delta. No extra read,
				// no serial dependency on a separate "trim" key.
				KeyRange range = cursor.HasValue
					? feed.ToTailRangeExclusive(cursor.Value).ToKeyRange()
					: feed.ToRange().ToKeyRange();
				var chunk = await tr.Snapshot.GetRangeAsync(range, FdbRangeOptions.WantAll.WithLimit(BatchSize));

				// renew this subscriber's liveness TOKEN with a database-sourced monotonic value (the read
				// version; a version-stamped value works too). It is never compared to wall-clock time,
				// the observer (see LivenessObserver) only checks whether it CHANGES between polls, so it is
				// immune to both clock skew across nodes and the non-constant version tick-rate.
				// NOTE: if a tombstone is found below we throw, which rolls back this renewal, so an evicted
				// subscriber never re-registers a stale cursor that could mislead the GC's horizon.
				long token = await tr.GetReadVersionAsync();
				tr.Set(
					state.Subspace.Key(SUBSPACE_SUBSCRIBERS, subscriberId),
					FdbValue.FromTuple((token, cursor ?? default(VersionStamp))));

				if (chunk.Count == 0)
				{ // caught up: watch the signal key (outer token, the watch outlives this tx)
					BookChangeEntry[] none = [];
					return (none, (FdbWatch?) tr.Watch(state.Subspace.Key(SUBSPACE_SIGNAL), ct));
				}

				var batch = new BookChangeEntry[chunk.Count];
				int i = 0;
				foreach (var kv in chunk)
				{
					var version = state.Subspace.DecodeLast<VersionStamp>(kv.Key);
					// A real change always serializes to a non-null JSON document; only a GC tombstone is empty.
					var change = CrystalJson.Deserialize<BookChange>(kv.Value);
					if (change is null)
					{ // tombstone => the GC reclaimed past our cursor while we were frozen: we missed changes
						throw new ChangeFeedOutOfSyncException(cursor ?? default, version);
					}
					batch[i++] = new BookChangeEntry(version, change);
				}
				return (batch, (FdbWatch?) null);
			}, ct);

			foreach (var entry in page.Batch)
			{
				cursor = entry.Version;
				yield return entry;
			}

			if (page.Watch is not null)
			{
				// Wake on the next change OR after the heartbeat interval, whichever comes first, so an
				// idle-but-alive subscriber keeps renewing its lease instead of looking dead.
				var done = await Task.WhenAny(page.Watch.Task, Task.Delay(hb, ct)).ConfigureAwait(false);
				if (done != page.Watch.Task) page.Watch.Cancel();   // heartbeat tick won -> drop the watch, loop
			}
		}
	}

	/// <summary>Subscribe via a <see cref="Channel{T}"/>: a background pump drains the feed into the channel.</summary>
	public ChannelReader<BookChangeEntry> Subscribe(
		IFdbDatabase db, Guid subscriberId, VersionStamp? after = null, CancellationToken ct = default)
	{
		var channel = Channel.CreateUnbounded<BookChangeEntry>(new UnboundedChannelOptions { SingleWriter = true });

		_ = PumpAsync();
		return channel.Reader;

		async Task PumpAsync()
		{
			try
			{
				await foreach (var entry in ReadChangesAsync(db, subscriberId, after, null, ct).ConfigureAwait(false))
				{
					await channel.Writer.WriteAsync(entry, ct).ConfigureAwait(false);
				}
				channel.Writer.TryComplete();
			}
			catch (OperationCanceledException) { channel.Writer.TryComplete(); }
			catch (Exception ex) { channel.Writer.TryComplete(ex); }
		}
	}

	/// <summary>Subscribe via a callback invoked once per change. Returns when <paramref name="ct"/> is cancelled.</summary>
	public async Task SubscribeAsync(
		IFdbDatabase db, Guid subscriberId,
		Func<BookChangeEntry, CancellationToken, ValueTask> onChange,
		VersionStamp? after = null, CancellationToken ct = default)
	{
		await foreach (var entry in ReadChangesAsync(db, subscriberId, after, null, ct).ConfigureAwait(false))
		{
			await onChange(entry, ct).ConfigureAwait(false);
		}
	}

	/// <summary>Latest committed feed version, so a subscriber can start "from now" instead of replaying history.</summary>
	public Task<VersionStamp?> GetLatestVersionAsync(IFdbDatabase db, CancellationToken ct = default)
	{
		return db.ReadAsync(async tr =>
		{
			var state = await this.Resolve(tr);
			var last = await tr
				.GetRange(state.Subspace.Key(SUBSPACE_FEED).ToRange(), FdbRangeOptions.Last)
				.FirstOrDefaultAsync();
			return last.Key.IsNull ? (VersionStamp?) null : state.Subspace.DecodeLast<VersionStamp>(last.Key);
		}, ct);
	}

	#endregion

	#region Retention (purge old feed entries) ...

	/// <summary>
	/// Decides which subscribers are frozen and trims the feed accordingly, WITHOUT ever comparing
	/// timestamps across nodes. Each subscriber stores a database-sourced monotonic token that changes
	/// only when it renews. This observer reads those tokens on a fixed LOCAL interval and watches for
	/// tokens that have NOT CHANGED across several consecutive polls. "Unchanged for N polls" == "N ×
	/// the observer's own local delay between reads", the local clock measures only the gap between this
	/// observer's own reads, never a value produced by another node. Testing the token for equality (not
	/// converting it to a duration) also makes it immune to the non-constant version tick-rate.
	/// Call <see cref="PollAndPurgeAsync"/> in a loop with a local <c>Task.Delay</c> between calls.
	/// </summary>
	public sealed class LivenessObserver(BookStore store)
	{
		// What this observer saw last poll: token + how many consecutive polls it has stayed unchanged.
		private readonly Dictionary<Guid, (long Token, int Unchanged)> seen = new();

		/// <param name="deadPolls">How many consecutive polls a token may stay unchanged before its
		/// subscriber is declared frozen. With a 5s loop delay, deadPolls=3 ≈ 15s of local elapsed time.</param>
		public async Task PollAndPurgeAsync(IFdbDatabase db, int deadPolls, CancellationToken ct = default)
		{
			var previous = this.seen;   // read-only inside the retryable handler below -> safe to capture

			Dictionary<Guid, long> fresh = await db.ReadWriteAsync(async tr =>
			{
				var state = await store.Resolve(tr);

				// NON-snapshot read: if a subscriber renews concurrently this tx conflicts and retries,
				// so we never evict someone who just came back to life.
				var regs = await tr.GetRange(state.Subspace.Key(SUBSPACE_SUBSCRIBERS).ToRange()).ToListAsync();

				var current = new Dictionary<Guid, long>();
				VersionStamp? horizon = null;   // slowest live subscriber's cursor
				foreach (var kv in regs)
				{
					var subId = state.Subspace.DecodeLast<Guid>(kv.Key);
					var (token, cursor) = TuPack.DecodeKey<long, VersionStamp>(kv.Value.Span);
					current[subId] = token;

					bool frozen = previous.TryGetValue(subId, out var p) && p.Token == token && p.Unchanged + 1 >= deadPolls;
					if (frozen)
					{ // its token hasn't moved across enough local intervals -> evict; it can't pin the log
						tr.Clear(kv.Key);
						continue;
					}
					if (horizon is null || cursor.CompareTo(horizon.Value) < 0) horizon = cursor;
				}

				var feed = state.Subspace.Key(SUBSPACE_FEED);

				// What we will reclaim up to: the slowest live cursor, or the current head if nobody is live.
				VersionStamp trimTo;
				if (horizon is not null)
				{
					trimTo = horizon.Value;
				}
				else
				{ // nobody live -> drop the whole backlog (snapshot-read the head so we don't conflict with writers)
					var lastKv = await tr.Snapshot.GetRange(feed.ToRange(), FdbRangeOptions.Last).FirstOrDefaultAsync();
					trimTo = lastKv.Key.IsNull ? default : state.Subspace.DecodeLast<VersionStamp>(lastKv.Key);
				}

				if (trimTo.CompareTo(default(VersionStamp)) > 0)
				{
					// Reclaim everything up to and including the horizon, then leave a single TOMBSTONE (empty
					// value, reusing the horizon's versionstamp) as a fence. A resuming subscriber whose cursor
					// is older than the horizon will read this tombstone first and know it missed changes, all
					// detected by its normal GetRange, with no extra read. (Set-after-clear keeps the tombstone.)
					tr.ClearRange(feed.ToHeadRangeInclusive(trimTo));
					tr.Set(feed.Key(trimTo), FdbValue.Empty);
				}

				return current;
			}, ct);

			// Update observation state AFTER commit (never mutate it inside the retryable handler).
			this.seen.Clear();
			foreach (var (subId, token) in fresh)
			{
				int unchanged = previous.TryGetValue(subId, out var p) && p.Token == token ? p.Unchanged + 1 : 0;
				if (unchanged >= deadPolls) continue;   // evicted this round -> forget it
				this.seen[subId] = (token, unchanged);
			}
		}
	}

	#endregion
}

// Example consumer maintaining an in-memory observable view (compile-only).
public static class BookStoreChangeFeedUsage
{
	public static async Task ObserveViaChannel(BookStore store, IFdbDatabase db, CancellationToken ct)
	{
		var me = Guid.NewGuid();
		var view = new Dictionary<string, Book>();

		while (!ct.IsCancellationRequested)
		{
			// Re-sync point: load current state, then tail the feed FROM NOW so cursor >= trim horizon.
			view.Clear();
			// ... reload the current documents into `view` here (full scan) ...
			var from = await store.GetLatestVersionAsync(db, ct);

			try
			{
				await foreach (var e in store.Subscribe(db, me, from, ct).ReadAllAsync(ct))
				{
					if (e.Change.Kind == BookChangeKind.Put) view[e.Change.Id] = e.Change.Book!;
					else view.Remove(e.Change.Id);
				}
			}
			catch (ChangeFeedOutOfSyncException)
			{
				// We were frozen/evicted and missed changes, our incremental view is untrustworthy.
				// Loop: reload current state and re-subscribe from now.
				continue;
			}
		}
	}

	// Background GC: poll every 5s (LOCAL clock, only the gap between our own reads), evicting any
	// subscriber whose DB token hasn't changed for 3 polls (~15s frozen) and trimming the feed.
	public static async Task RunGcLoop(BookStore store, IFdbDatabase db, CancellationToken ct)
	{
		var observer = new BookStore.LivenessObserver(store);
		while (!ct.IsCancellationRequested)
		{
			await observer.PollAndPurgeAsync(db, deadPolls: 3, ct);
			await Task.Delay(TimeSpan.FromSeconds(5), ct);   // local elapsed time between our own polls
		}
	}
}
