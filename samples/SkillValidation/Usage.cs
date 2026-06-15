// Compiles the concrete code snippets from BOTH skill docs against the live API.
// Each method mirrors a section of the skills. Nothing here connects to a database;
// the goal is purely to prove the examples type-check.

using FoundationDB.Client;
using SnowBank.Data.Tuples;   // STuple, IVarTuple
using SnowBank.Data.Json;     // CrystalJson
using SnowBank.Linq;          // IAsyncQuery, ToListAsync

namespace SkillValidation;

public static class Usage
{
	// keys-and-layers §3 — Building keys
	public static void BuildingKeys(IKeySubspace subspace)
	{
		FdbTupleKey<string> k1 = subspace.Key("hello");
		FdbTupleKey<string, int> k2 = subspace.Key("hello", 123);
		var userId = Guid.NewGuid();
		FdbTupleKey<string, int, Guid> k3 = subspace.Key("user", 123, userId);
		FdbSubspaceKey kp = subspace.Key();

		var t1 = subspace.Tuple(STuple.Create("hello", 123));
		var t2 = subspace.Tuple(("hello", 123));

		FdbSuffixKey raw = subspace.Bytes("abc"u8);

		FdbRawKey r = FdbKey.FromBytes(Slice.FromString("hello"));
		FdbTupleKey<string, int> t = FdbKey.FromTuple(("hello", 123));
		FdbSystemKey sys = FdbKey.ToSystemKey("/metadataVersion");

		_ = (k1, k2, k3, kp, t1, t2, raw, r, t, sys);
	}

	// keys-and-layers §4 — Ranges & key derivation
	public static void RangesAndDerivation(IKeySubspace subspace)
	{
		var r1 = subspace.ToRange();
		var r2 = subspace.ToRange(inclusive: true);
		var r3 = subspace.Key("user", 123).ToRange();
		var r4 = FdbKeyRange.Single(subspace.Key(123));
		var r5 = FdbKeyRange.Between(subspace.Key(100), subspace.Key(200));
		var r6 = subspace.ToHeadRange(123);
		var r7 = subspace.ToTailRange(123);

		var succ = subspace.Key(123).Successor();
		var sib = subspace.Key(123).NextSibling();
		var first = subspace.First();
		var last = subspace.Last();
		var sel1 = subspace.Key(123).FirstGreaterOrEqual();
		var sel2 = subspace.Key(123).LastLessOrEqual();

		_ = (r1, r2, r3, r4, r5, r6, r7, succ, sib, first, last, sel1, sel2);
	}

	// keys-and-layers §5 — Decoding keys
	public static async Task DecodingKeys(IFdbDatabase db, ISubspaceLocation location, CancellationToken ct)
	{
		await db.ReadAsync(async tr =>
		{
			var subspace = await location.Resolve(tr);
			var chunk = await tr.GetRange(subspace.Key("I", "Asimov").ToRange()).ToListAsync();
			foreach (var kv in chunk)
			{
				var (name, id) = subspace.Decode<string, int>(kv.Key);   // deconstructs STuple<string?, int?>
				int id2 = subspace.DecodeLast<int>(kv.Key);
				string n = subspace.DecodeFirst<string>(kv.Key)!;
				IVarTuple tup = subspace.Unpack(kv.Key);
				_ = (name, id, id2, n, tup);
			}
			return chunk.Count;
		}, ct);
	}

	// keys-and-layers §6 — Locations & the Directory layer
	public static async Task Locations(IFdbDatabase db, CancellationToken ct)
	{
		ISubspaceLocation location = db.Root["Tenants"]["ACME"]["Documents"]["Books"];
		ISubspaceLocation viaPath = db.Root[FdbPath.Relative("Tenants", "ACME", "Documents", "Books")];

		await db.WriteAsync(async tr =>
		{
			IKeySubspace subspace = await location.Resolve(tr);
			tr.Set(subspace.Key("BOOK_123"), FdbValue.FromTuple(("Title", "ISBN")));
		}, ct);

		_ = viaPath;
	}

	// keys-and-layers §8 — Encoding values
	public static void EncodingValues()
	{
		var book = new Book { Id = "B1", Author = "Asimov", Title = "Foundation" };

		var v1 = FdbValue.ToBytes(Slice.FromString("blob"));
		var v2 = FdbValue.Empty;
		var v3 = FdbValue.FromTuple(("a", 1));
		var v4 = FdbValue.ToTextUtf8("hello");
		var v5 = FdbValue.ToFixed64LittleEndian(42);
		var v6 = FdbValue.ToCompactLittleEndian(42);
		var v7 = FdbValue.ToUuid128(Guid.NewGuid());
		var v8 = FdbValue.ToJson(book);

		_ = (v1, v2, v3, v4, v5, v6, v7, v8);
	}

	// keys-and-layers §7 — Using the layer + composing layers
	public static async Task UsingLayers(IFdbDatabase db, CancellationToken ct)
	{
		var store = new BookStore(db.Root["Documents"]["Books"]);

		await store.WriteAsync(db, (tr, state) =>
			state.Insert(tr, new Book { Id = "B1", Author = "Asimov", Title = "Foundation" }), ct);

		Book? b = await store.ReadAsync(db, (tr, state) => state.GetAsync(tr, "B1"), ct);

		// update (async handler, no result) and delete (async handler returning a result)
		await store.WriteAsync(db, (tr, state) =>
			state.UpdateAsync(tr, new Book { Id = "B1", Author = "Asimov", Title = "Foundation (rev.)" }), ct);

		bool deleted = await store.ReadWriteAsync(db, (tr, state) => state.DeleteAsync(tr, "B1"), ct);

		// patch a single field via a callback (records: use a `with` expression)
		await store.WriteAsync(db, (tr, state) =>
			state.PatchAsync(tr, "B1", current => current with { Title = current.Title + " (patched)" }), ct);

		// update when the caller already holds the original (read in the SAME transaction)
		await db.WriteAsync(async tr =>
		{
			var state = await store.Resolve(tr);
			var original = await state.GetAsync(tr, "B1");
			if (original is not null)
			{
				await state.UpdateAsync(tr, original with { Author = "Clarke" }, original);
			}
		}, ct);

		// the schema mapper exposes the key layout to tools (db dumps, FQL shell, ...)
		IFdbLayerSchemaMapper mapper = new BookStore.SchemaMapper();
		var rules = mapper.GetRules().ToList();

		// compose two layers atomically in one transaction
		var stats = new BookStore(db.Root["Stats"]);
		await db.WriteAsync(async tr =>
		{
			var books = await store.Resolve(tr);
			var other = await stats.Resolve(tr);
			books.Insert(tr, new Book { Id = "B2", Author = "Clarke", Title = "Rama" });
			other.Insert(tr, new Book { Id = "B2", Author = "Clarke", Title = "Rama" });
		}, ct);

		_ = (b, deleted, rules);
	}

	// transactions §1 — the three retry-loop methods
	public static async Task RetryLoops(IFdbDatabase db, ISubspaceLocation location, CancellationToken ct)
	{
		Book? book = await db.ReadAsync(async tr =>
		{
			var subspace = await location.Resolve(tr);
			var bytes = await tr.GetAsync(subspace.Key(0, "B1"));
			return CrystalJson.Deserialize<Book>(bytes);   // Nil/empty slice -> null
		}, ct);

		var fixedSub = KeySubspace.FromKey(Slice.FromByte(42));
		await db.WriteAsync(tr =>
		{
			tr.Set(fixedSub.Key("D", "B1"), FdbValue.ToTextUtf8("x"));
		}, ct);

		var accountKey = KeySubspace.FromKey(Slice.FromByte(7)).Key("acct");
		long newBalance = await db.ReadWriteAsync(async tr =>
		{
			long current = (await tr.GetAsync(accountKey)).ToInt64();
			long updated = current + 100;
			tr.Set(accountKey, FdbValue.ToFixed64LittleEndian(updated));
			return updated;
		}, ct);

		_ = (book, newBalance);
	}

	// transactions §4 — atomic mutations, snapshot reads, conflict ranges
	public static async Task Atomics(IFdbDatabase db, ISubspaceLocation location, CancellationToken ct)
	{
		var counterKey = KeySubspace.FromKey(Slice.FromByte(8)).Key("c");
		await db.WriteAsync(tr =>
		{
			tr.AtomicAdd64(counterKey, +1);
			tr.AtomicIncrement64(counterKey);
			tr.AtomicDecrement64(counterKey, clearIfZero: true);
			tr.AtomicMax(counterKey, Slice.FromFixed64(5));
			tr.AtomicMin(counterKey, Slice.FromFixed64(5));
			tr.AtomicAnd(counterKey, Slice.FromFixed64(0xFF));
			tr.AtomicOr(counterKey, Slice.FromFixed64(0xFF));
			tr.AtomicXor(counterKey, Slice.FromFixed64(0xFF));
		}, ct);

		await db.ReadAsync(async tr =>
		{
			var subspace = await location.Resolve(tr);
			var one = await tr.Snapshot.GetAsync(subspace.Key("D", "B1"));
			var many = await tr.Snapshot.GetRange(subspace.ToRange()).ToListAsync();
			return one.Count + many.Count;
		}, ct);

		await db.WriteAsync(async tr =>
		{
			var subspace = await location.Resolve(tr);
			var prefix = subspace.GetPrefix();
			tr.AddConflictRange(prefix.Span, FdbKey.Increment(prefix).Span, FdbConflictRangeType.Read);
		}, ct);
	}

	// transactions §5 — watches
	public static async Task Watches(IFdbDatabase db, ISubspaceLocation location, CancellationToken ct)
	{
		FdbWatch watch = await db.ReadWriteAsync(
			async tr =>
			{
				var subspace = await location.Resolve(tr);
				return tr.Watch(subspace.Key("signal"), ct);
			},
			ct);

		await watch;
	}
}
