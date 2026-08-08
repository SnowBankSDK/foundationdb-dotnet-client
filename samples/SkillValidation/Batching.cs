// Validates the read-batching idioms from the foundationdb-advanced-layers skill (section 2): reading many
// independent keys in one round-trip instead of awaiting them serially in a loop.

using FoundationDB.Client;

namespace SkillValidation;

public static class Batching
{
	public static async Task BatchReads(IFdbDatabase db, IKeySubspace subspace, IReadOnlyList<string> ids, CancellationToken ct)
	{
		await db.ReadAsync(async tr =>
		{
			// ✅ one batched multi-read of N independent keys
			Slice[] values = await tr.GetValuesAsync(ids.Select(id => subspace.Key("D", id)));

			// ✅ independent reads issued concurrently -> pipelined into ~one round-trip
			Slice[] pair = await Task.WhenAll(
				tr.GetAsync(subspace.Key("meta", "a")),
				tr.GetAsync(subspace.Key("meta", "b")));

			return values.Length + pair.Length;
		}, ct);
	}
}
