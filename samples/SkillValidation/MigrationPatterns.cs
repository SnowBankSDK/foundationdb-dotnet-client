// Validates the current-API patterns surfaced by comparing the old vs new DocStore layer:
//  - migrating dynamic subspace.Encode(...) / .Pack(...) to typed subspace.Key(...) / .Tuple(...)
//  - typed prefix + RUNTIME tuple suffix (for keys whose tail types aren't known at compile time)
//  - IFdbLayer<TState, TOptions> for parameterized (e.g. multi-tenant) layers
//  - FQL schema rules with a value-hinter FUNCTION (hint depends on the decoded key)

using FoundationDB.Client;
using SnowBank.Data.Tuples;          // STuple, IVarTuple
using SnowBank.Data.Tuples.Binary;   // SpanTuple, TuPackUserType

namespace SkillValidation;

public static class MigrationPatterns
{
	private const int INDEXES = 1;
	private const int CHUNKS = 0;

	// OLD (pre-revamp, dynamic API — shown in comments, no longer compiles):
	//   tenant.Encode(CHUNKS, rid, chunkId, chunkCount)
	//   tenant.Pack(STuple.Create(INDEXES, idx).Concat(value))
	//   tenant.EncodeRange(CHUNKS, rid)
	//   global.Partition.ByKey(tenant.Prefix)
	// NEW (typed API):
	public static void KeyApiMigration(IKeySubspace tenant, long rid, int chunkId, int chunkCount, int idx)
	{
		// .Encode(a, b, c)  ->  .Key(a, b, c)
		var chunkKey = tenant.Key(CHUNKS, rid, chunkId, chunkCount);

		// .Pack(STuple.Create(prefix...).Concat(runtimeValue))  ->  .Key(prefix...).Tuple(runtimeValue)
		IVarTuple value = STuple.Create("Asimov", 1951);          // value types/arity only known at runtime
		var uniqueIndexKey = tenant.Key(INDEXES, idx).Tuple(value);

		// .EncodeRange(...)  ->  FdbKeyRange.Between(...).ToKeyRange(), using a system sentinel or .Last()
		var allChunksOfDoc = tenant.Key(CHUNKS, rid).ToRange();
		var indexValueRange = FdbKeyRange.Between(
			tenant.Key(INDEXES, idx),
			tenant.Key(INDEXES, idx, TuPackUserType.System)).ToKeyRange();
		var fromValueToEnd = FdbKeyRange.Between(
			tenant.Key(INDEXES, idx).Tuple(value),
			tenant.Key(INDEXES, idx).Last()).ToKeyRange();

		// .Partition.ByKey(prefix)  ->  .Key(prefix).ToSubspace()
		IKeySubspace child = tenant.Key(42).ToSubspace();

		_ = (chunkKey, uniqueIndexKey, allChunksOfDoc, indexValueRange, fromValueToEnd, child);
	}

	// FQL schema rule whose value hint is computed from the decoded key (not a fixed hint).
	public static FqlTemplateExpression HinterRule()
	{
		return new FqlTemplateExpression(
			"GlobalAttributes",
			FqlTupleExpression.Create().VarString("attr").MaybeMore(),
			(SpanTuple t) => t.Get<string>(0) switch
			{
				"Count" or "SchemaVersion" => FdbValueTypeHint.IntegerLittleEndian,
				"Name" or "Type"           => FdbValueTypeHint.Utf8,
				_                          => FdbValueTypeHint.None,
			});
	}

	// FQL rule using a named constant element and a wildcard capture.
	public static FqlTemplateExpression NonUniqueIndexRule()
	{
		return new FqlTemplateExpression(
			"NonUniqueIndexedValues",
			FqlTupleExpression.Create()
				.VarInteger("tenant")
				.Integer(INDEXES, "INDEXES")
				.VarInteger("idx")
				.VarAny("value")
				.VarInteger("rowId"),
			FdbValueTypeHint.None);
	}

	// Exercise the IFdbLayer<TState, TOptions> retry-loop helpers (TOptions = tenant).
	public static async Task UseTenantLayer(IFdbDatabase db, ISubspaceLocation location, CancellationToken ct)
	{
		var counter = new TenantCounter(location);
		await counter.WriteAsync(db, new TenantCounter.Tenant(1), (tr, st) => st.Bump(tr, "hits"), ct);
		long n = await counter.ReadWriteAsync(db, new TenantCounter.Tenant(1),
			(tr, st) => st.ReadAsync(tr, "hits"), ct);
		_ = n;
	}
}

/// <summary>A counter layer parameterized by a tenant, using the two-type-param IFdbLayer&lt;TState, TOptions&gt;.</summary>
public sealed class TenantCounter : IFdbLayer<TenantCounter.State, TenantCounter.Tenant>
{
	public readonly record struct Tenant(int Id);

	public TenantCounter(ISubspaceLocation location) => this.Location = location;

	public ISubspaceLocation Location { get; }

	public string Name => nameof(TenantCounter);

	// Resolve takes the option (tenant) and partitions the global subspace by it.
	public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr, Tenant tenant)
	{
		var global = await this.Location.Resolve(tr);
		var subspace = global.Key(tenant.Id).ToSubspace();
		return new State(subspace);
	}

	public sealed class State
	{
		public IKeySubspace Subspace { get; }
		internal State(IKeySubspace subspace) => this.Subspace = subspace;

		public void Bump(IFdbTransaction tr, string name)
			=> tr.AtomicIncrement64(this.Subspace.Key(name));

		public async Task<long> ReadAsync(IFdbReadOnlyTransaction tr, string name)
		{
			var v = await tr.GetAsync(this.Subspace.Key(name));
			return v.IsNullOrEmpty ? 0 : v.ToInt64();
		}
	}
}
