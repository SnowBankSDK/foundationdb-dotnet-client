// This file is the verbatim worked example from
//   .claude/skills/foundationdb-keys-and-layers/SKILL.md  (section 7 "Writing a custom Layer")
// Its purpose is to fail the build if that example ever drifts from the real API.

using FoundationDB.Client;
using SnowBank.Data.Tuples;   // STuple, TuPack
using SnowBank.Data.Json;     // CrystalJson
using SnowBank.Linq;          // IAsyncQuery

namespace SkillValidation;

/// <summary>Stores Book documents (as JSON) keyed by their Id, plus a secondary index by author,
/// and an append-only change feed that other nodes can subscribe to. (Feed/retention live in
/// BookStore.ChangeFeed.cs.)</summary>
public sealed partial class BookStore : IFdbLayer<BookStore.State>
{
	// Discriminate the sub-parts of the subspace with small INTEGER constants, not strings.
	// Tuple-packed, 0 takes 1 byte (0x14) and 1 takes 2 bytes (0x15 0x01), whereas the string "D"
	// takes 3 bytes (0x02 'D' 0x00) on EVERY key. Name them so call sites stay readable.
	private const int SUBSPACE_DOCUMENTS = 0;      // (0, <id>)            -> json document
	private const int SUBSPACE_INDEX_AUTHOR = 1;   // (1, <author>, <id>) -> empty (index entry)
	private const int SUBSPACE_FEED = 2;           // (2, <versionstamp>) -> json change event (the feed)
	private const int SUBSPACE_SIGNAL = 3;         // (3,)                -> counter watched by subscribers
	private const int SUBSPACE_SUBSCRIBERS = 4;    // (4, <subId>)        -> (leaseReadVersion, cursor) lease
	// (eviction is signalled by a TOMBSTONE left inside the feed itself, see BookStore.ChangeFeed.cs, so
	//  no separate "trim horizon" key is needed; a resuming subscriber detects it in its normal GetRange.)

	/// <summary>LayerId advertised to the Directory layer, linking subspaces to <see cref="SchemaMapper"/>.</summary>
	public const string LayerId = "docstore.Books";

	public BookStore(ISubspaceLocation location)
	{
		this.Location = location;
	}

	public ISubspaceLocation Location { get; }

	public string Name => nameof(BookStore);

	private const string LocalDataKey = nameof(BookStore);

	// Resolve once per transaction; memoize in the transaction's local data so repeated
	// Resolve(tr) calls in the same tx reuse the same State. Never cache it OUTSIDE the tx.
	public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
	{
		if (tr.Context.TryGetLocalData(LocalDataKey, out State? state))
		{
			return new ValueTask<State>(state);
		}
		return ResolveSlow(this, tr);

		static async ValueTask<State> ResolveSlow(BookStore self, IFdbReadOnlyTransaction tr)
		{
			var subspace = await self.Location.Resolve(tr);
			return tr.Context.GetOrCreateLocalData(LocalDataKey, new State(self, subspace));
		}
	}

	public sealed partial class State
	{
		private readonly BookStore Layer;
		public IKeySubspace Subspace { get; }

		internal State(BookStore layer, IKeySubspace subspace)
		{
			this.Layer = layer;
			this.Subspace = subspace;
		}

		/// <summary>Inserts a brand-new book (assumes the Id does not already exist).</summary>
		public void Insert(IFdbTransaction tr, Book book)
		{
			tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id), FdbValue.ToJson(book));
			tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, book.Author, book.Id), FdbValue.Empty);
			LogChange(tr, BookChangeKind.Put, book.Id, book);
		}

		/// <summary>Reads a book by Id, or <c>null</c> if it does not exist.</summary>
		public async Task<Book?> GetAsync(IFdbReadOnlyTransaction tr, string id)
		{
			var bytes = await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id));
			// Deserialize already maps a Nil/empty slice (missing key) to null, no IsNull check needed.
			return CrystalJson.Deserialize<Book>(bytes);
		}

		/// <summary>
		/// Updates an existing book by re-reading the stored document to learn the old indexed value.
		/// Use this when the caller does not already hold the original (e.g. it built the new Book
		/// from scratch). The re-read is cheap if the same key was already read in this transaction
		/// (no extra network round-trip), but still re-deserializes the JSON.
		/// </summary>
		public async Task UpdateAsync(IFdbTransaction tr, Book book)
		{
			Book? old = CrystalJson.Deserialize<Book>(
				await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id)));

			tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id), FdbValue.ToJson(book));
			ReindexAuthor(tr, book.Id, old?.Author, book.Author);
			LogChange(tr, BookChangeKind.Put, book.Id, book);
		}

		/// <summary>
		/// Updates a book when the caller ALREADY holds the original document.
		/// CONTRACT: <paramref name="original"/> MUST be the exact value returned by a GetAsync(...)
		/// on THIS SAME transaction. That read is what registers the conflict that keeps the index
		/// consistent; passing a stale or foreign "original" WILL corrupt the index. No read is done here.
		/// </summary>
		public Task UpdateAsync(IFdbTransaction tr, Book updated, Book original)
		{
			tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, updated.Id), FdbValue.ToJson(updated));
			ReindexAuthor(tr, updated.Id, original.Author, updated.Author);
			LogChange(tr, BookChangeKind.Put, updated.Id, updated);
			return Task.CompletedTask;
		}

		/// <summary>
		/// Reads, mutates and saves a book in one call. <paramref name="patch"/> receives the current
		/// document and returns the modified copy (with records: a <c>with</c> expression). Nothing is
		/// written if the patch is a no-op, and the index is only touched if the author changed.
		/// Ideal for cheap field bumps (LastAccessed, UseCount, ...).
		/// </summary>
		public async Task<Book?> PatchAsync(IFdbTransaction tr, string id, Func<Book, Book> patch)
		{
			Book? current = CrystalJson.Deserialize<Book>(
				await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id)));
			if (current is null) return null; // nothing to patch

			Book updated = patch(current);

			if (updated == current) return current; // no-op: records compare by value -> skip all writes

			if (!string.Equals(updated.Id, id, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("A patch must not change the document Id.");
			}

			tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, id), FdbValue.ToJson(updated));
			ReindexAuthor(tr, id, current.Author, updated.Author);
			LogChange(tr, BookChangeKind.Put, id, updated);
			return updated;
		}

		/// <summary>
		/// Deletes a book by Id (and its index entry). Takes only the Id: a Book passed by the caller
		/// cannot be trusted (it may be stale or mutated), and using its Author to locate the index key
		/// could leave an orphaned index entry. We read the stored document to get the real author.
		/// </summary>
		public async Task<bool> DeleteAsync(IFdbTransaction tr, string id)
		{
			Book? existing = CrystalJson.Deserialize<Book>(
				await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id)));
			if (existing is null) return false; // nothing to delete

			tr.Clear(this.Subspace.Key(SUBSPACE_DOCUMENTS, id));
			tr.Clear(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, existing.Author, id));
			LogChange(tr, BookChangeKind.Delete, id, null);
			return true;
		}

		/// <summary>Returns the Ids of all books by the given author, in order.</summary>
		public IAsyncQuery<string> FindIdsByAuthor(IFdbReadOnlyTransaction tr, string author)
		{
			return tr
				.GetRange(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, author).ToRange())
				.Select(kv => this.Subspace.DecodeLast<string>(kv.Key)!);
		}

		/// <summary>Moves the author index entry for <paramref name="id"/> from <paramref name="oldAuthor"/> to <paramref name="newAuthor"/>, only when it changed.</summary>
		private void ReindexAuthor(IFdbTransaction tr, string id, string? oldAuthor, string newAuthor)
		{
			if (oldAuthor is null)
			{ // freshly created entry
				tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, newAuthor, id), FdbValue.Empty);
			}
			else if (!string.Equals(oldAuthor, newAuthor, StringComparison.Ordinal))
			{ // indexed value changed -> move the entry
				tr.Clear(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, oldAuthor, id));
				tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, newAuthor, id), FdbValue.Empty);
			}
			// else: unchanged -> leave the index alone (no wasted write, no extra conflict)
		}
	}

	/// <summary>
	/// Describes the key layout of this layer so tools (db dumps, the FQL shell, loggers) can render
	/// raw keys as friendly tuples. Any directory subspace tagged with <see cref="LayerId"/> uses these rules.
	/// </summary>
	public sealed class SchemaMapper : IFdbLayerSchemaMapper
	{
		public string LayerId => BookStore.LayerId;

		public IEnumerable<FqlTemplateExpression> GetRules()
		{
			// (0, <id:string>) -> a JSON document
			yield return new FqlTemplateExpression(
				"document",
				FqlTupleExpression.Create().Integer(SUBSPACE_DOCUMENTS).VarString("id"),
				FdbValueTypeHint.Json);

			// (1, <author:string>, <id:string>) -> empty index entry
			yield return new FqlTemplateExpression(
				"index.author",
				FqlTupleExpression.Create().Integer(SUBSPACE_INDEX_AUTHOR).VarString("author").VarString("id"),
				FdbValueTypeHint.None);
		}
	}
}

public sealed record Book
{
	public required string Id { get; init; }
	public required string Author { get; init; }
	public required string Title { get; init; }
}
