// Validates the Watch + VersionStamp-ordered-queue patterns from the PubSub/FireHose layer:
//  - append messages with commit-ordered VersionStamp keys (tr.CreateVersionStamp + SetVersionStampedKey)
//  - wake a subscriber by AtomicIncrement-ing a single "watch" key it is watching
//  - consume loop: read a batch; if empty, Watch the signal key, await OUTSIDE the tx, then re-read

using FoundationDB.Client;
using SnowBank.Data.Tuples;

namespace SkillValidation;

public static class WatchAndQueue
{
	private const string INBOX = "INBOX";
	private const string WATCH = "WATCH";

	/// <summary>Append messages to an inbox under commit-ordered VersionStamp keys, then signal the subscriber.</summary>
	public static void Publish(IFdbTransaction tr, IKeySubspace topic, IKeySubspace subscriber, IReadOnlyList<Slice> messages)
	{
		var inbox = topic.Key(INBOX);

		int userVersion = 0;
		foreach (var msg in messages)
		{
			// One distinct *incomplete* stamp per item in this tx (userVersion disambiguates them);
			// SetVersionStampedKey fills in the real, globally-ordered stamp at commit time.
			var stamp = tr.CreateVersionStamp(userVersion++);
			tr.SetVersionStampedKey(inbox.Key(stamp), msg);
		}

		// Signal: bump the single key the subscriber is watching. AtomicIncrement guarantees a change
		// (so the watch always fires) and never conflicts with other publishers.
		tr.AtomicIncrement32(subscriber.Key(WATCH));
	}

	/// <summary>Stamp the VALUE (not the key) with the commit version, e.g. "last updated at" markers.</summary>
	public static void StampValue(IFdbTransaction tr, IKeySubspace subspace)
	{
		// The value carries the incomplete stamp; FDB fills in the real commit version on commit.
		tr.SetVersionStampedValue(subspace.Key("lastWrite"), tr.CreateVersionStamp());
	}

	/// <summary>Consume loop: drain a batch, or watch the signal key and wait for the next publish.</summary>
	public static async Task ConsumeLoop(IFdbDatabase db, ISubspaceLocation subscriberLocation, CancellationToken outerToken)
	{
		while (!outerToken.IsCancellationRequested)
		{
			(List<Slice>? Batch, FdbWatch? Watch) step = await db.ReadWriteAsync(async tr =>
			{
				var sub = await subscriberLocation.Resolve(tr);
				var inbox = sub.Key(INBOX);

				// snapshot read: scanning the queue must not create read-conflicts with publishers
				var msgs = await tr.Snapshot.GetRangeAsync(inbox.ToRange(), FdbRangeOptions.WantAll.WithLimit(100));

				if (msgs.Count == 0)
				{ // nothing yet -> watch the signal key. NOTE: outer token, never tr.Cancellation!
					return ((List<Slice>?) null, (FdbWatch?) tr.Watch(sub.Key(WATCH), outerToken));
				}

				// consume the batch by clearing exactly the keys we read
				tr.ClearRange(msgs.First, FdbKey.Successor(msgs.Last));
				var list = new List<Slice>(msgs.Count);
				foreach (var kv in msgs) list.Add(kv.Value);
				return ((List<Slice>?) list, (FdbWatch?) null);
			}, outerToken);

			if (step.Watch != null)
			{
				await step.Watch;   // a watch only NOTIFIES that the key changed...
				continue;           // ...so loop back and re-read the inbox
			}

			// dispatch step.Batch to handlers here
			_ = step.Batch;
		}
	}
}
