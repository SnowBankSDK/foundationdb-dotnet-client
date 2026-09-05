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

namespace FoundationDB.Layers.FullText
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using SnowBank.Buffers;
	using SnowBank.Data.Json;
	using SnowBank.Diagnostics.Contracts;
	using SnowBank.Linq;

	/// <summary>A field to index, identified by a <see cref="JsonPath"/> into the document, with a relative importance weight.</summary>
	/// <param name="Path">Path of the value to index (e.g. <c>title</c>, <c>author</c>, <c>address.city</c>).</param>
	/// <param name="Weight">Relative importance of a match in this field when ranking (higher wins). Defaults to <c>1.0</c>.</param>
	public sealed record FdbTextField(JsonPath Path, double Weight = 1.0)
	{
		/// <summary>Creates a field from a textual path.</summary>
		public FdbTextField(string path, double weight = 1.0) : this(JsonPath.Create(path), weight) { }
	}

	/// <summary>A single result returned by the full-text index: the document <paramref name="Id"/> and its computed relevance <paramref name="Score"/>.</summary>
	public readonly record struct FtsHit<TId>(TId Id, double Score);

	/// <summary>Base type of a full-text query expression.</summary>
	public abstract record FtsQuery;

	/// <summary>Matches documents containing <paramref name="Term"/>. When <paramref name="Field"/> is <see langword="null"/>,
	/// the term is searched across ALL configured fields, each scaled by its weight (the "search everything" case).</summary>
	public sealed record FtsTerm(string Term, JsonPath? Field = null) : FtsQuery;

	/// <summary>Matches documents that satisfy BOTH sides (their scores are summed).</summary>
	public sealed record FtsAnd(FtsQuery Left, FtsQuery Right) : FtsQuery;

	/// <summary>Matches documents that satisfy EITHER side (their scores are summed).</summary>
	public sealed record FtsOr(FtsQuery Left, FtsQuery Right) : FtsQuery;

	/// <summary>Matches documents that satisfy <paramref name="Include"/> but NOT <paramref name="Exclude"/>.</summary>
	public sealed record FtsAndNot(FtsQuery Include, FtsQuery Exclude) : FtsQuery;

	/// <summary>Matches documents where <paramref name="Terms"/> occur IN ORDER within <paramref name="Slop"/> extra positions of each other.</summary>
	/// <param name="Terms">The already-analyzed terms of the phrase, in order (lowercase, stop-words removed the same way the index analyzes them).</param>
	/// <param name="Field">Field to match the phrase in. When <see langword="null"/>, the phrase is matched in EVERY configured field, each scaled by its weight.</param>
	/// <param name="Slop">Allowed number of extra positions between the first and last term beyond a tight run: <c>0</c> is an exact, consecutive
	/// phrase (<c>"left hand"</c> matches only adjacent <c>left</c>, <c>hand</c>); <c>1</c> lets one other position sit between them (proximity).</param>
	/// <remarks>Because positions are recorded in the analyzed (stop-word-filtered) term stream, an intervening stop-word does not count as a
	/// gap; and a phrase never matches across two elements of an array-valued field (a large position gap separates the elements).</remarks>
	public sealed record FtsPhrase(IReadOnlyList<string> Terms, JsonPath? Field = null, int Slop = 0) : FtsQuery;

	/// <summary>Experimental, tutorial-grade inverted-index (full-text search) layer over FoundationDB.</summary>
	/// <typeparam name="TId">Type of the document identifier (must be tuple-encodable, e.g. <see cref="string"/>, <see cref="Guid"/>, or <see cref="long"/>).</typeparam>
	/// <remarks>
	/// <para>An inverted index answers "which documents contain this term?" by storing, for every term, the list of documents
	/// that use it (its "postings"). We keep one key per (field, term, document) so that a single term lookup is one ordered
	/// range read, and so that each field can be scored and weighted independently (which is what lets one query search across
	/// several fields without a concatenated "catch-all" field).</para>
	/// <para>Key layout, all under the subspace resolved from <see cref="Location"/> (small integer prefixes keep keys short):</para>
	/// <code>
	/// (0, id)                = the original JSON document      // kept inline so Update/Remove can recompute the OLD terms
	/// (1, field, term, id)   = term positions (varint list)   // one posting per (term, document); its length is the term frequency
	/// (2, id, field)         = field length (term count)       // used by BM25 to normalise for long vs short fields
	/// (3, field, term)       = document frequency              // how many documents use the term; an atomic counter
	/// (4, "docs")            = total document count            // atomic counter, the N in BM25's idf
	/// (4, "sumlen", field)   = sum of field lengths            // divided by N gives the average field length
	/// </code>
	/// <para>This is deliberately simple and unoptimized (one key per (field, term, doc), the document stored inline, the field
	/// path used verbatim in the key). It exists to show that a full-text layer works on top of FoundationDB, not to be fast.</para>
	/// </remarks>
	public sealed class FdbTextIndex<TId> : IFdbLayer<FdbTextIndex<TId>.State>
		where TId : notnull
	{

		// Sub-parts of the subspace. Tuple-packed, a small integer discriminator is 1-2 bytes; a string like "postings"
		// would cost that many bytes on EVERY key, so we name integer constants instead (see foundationdb-keys-and-layers).
		private const int Documents = 0;   // (0, id)                  -> the original JSON document
		private const int Postings = 1;    // (1, field, term, id)     -> the term's positions in that field (varint list; its length is the term frequency)
		private const int DocLengths = 2;  // (2, id, field)           -> number of terms in that field
		private const int TermStats = 3;   // (3, field, term)         -> document frequency (atomic counter)
		private const int Metadata = 4;    // (4, "docs") / (4, "sumlen", field)

		private const string MetaDocCount = "docs";     // total number of indexed documents
		private const string MetaSumLength = "sumlen";  // sum of field lengths, per field (for the average length)

		// A phrase must not match across two elements of an array-valued field, so element boundaries insert a position jump this large.
		// Any realistic slop stays well below it (Lucene uses the same default), so "last word of element i" is never adjacent to "first word of element i+1".
		private const int ArrayPositionGap = 100;

		// BM25 tuning constants (the usual defaults). k1 controls how quickly extra occurrences of a term stop helping
		// (term-frequency saturation); b controls how strongly a long field is penalised relative to the average length.
		private const double K1 = 1.2;
		private const double B = 0.75;

		/// <summary>Creates a new full-text index over <paramref name="location"/> for the given <paramref name="fields"/>.</summary>
		public FdbTextIndex(ISubspaceLocation location, IEnumerable<FdbTextField> fields, TextAnalyzer? analyzer = null)
		{
			Contract.NotNull(location);
			Contract.NotNull(fields);

			this.Location = location;
			this.Fields = new List<FdbTextField>(fields);
			this.Analyzer = analyzer ?? TextAnalyzer.Default;

			if (this.Fields.Count == 0)
			{
				throw new ArgumentException("A full-text index must declare at least one field.", nameof(fields));
			}
		}

		/// <summary>Location of the subspace that stores this index.</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>Fields indexed by this layer, with their weights.</summary>
		public IReadOnlyList<FdbTextField> Fields { get; }

		/// <summary>Analyzer used to turn field text into terms.</summary>
		public TextAnalyzer Analyzer { get; }

		/// <inheritdoc />
		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction trans)
		{
			Contract.NotNull(trans);

			// Resolving the location asks the Directory layer for this subspace's (short, shared) key prefix. It must be
			// done inside the transaction and never cached across transactions, so State stays confined to this transaction.
			var subspace = await this.Location.Resolve(trans);
			return new State(this, subspace);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbTextIndex<>);

		/// <inheritdoc />
		public override string ToString() => $"TextIndex[{this.Location}]";

		/// <summary>State of the index bound to a single transaction. All keys are built from <see cref="Subspace"/>.</summary>
		public sealed class State
		{

			internal State(FdbTextIndex<TId> schema, IKeySubspace subspace)
			{
				this.Schema = schema;
				this.Subspace = subspace;
			}

			/// <summary>Schema (field list, analyzer) of the index.</summary>
			public FdbTextIndex<TId> Schema { get; }

			/// <summary>Resolved subspace holding the index keys.</summary>
			public IKeySubspace Subspace { get; }

			/// <summary>Inserts or replaces the document with the given <paramref name="id"/>. Calling it again on the same id is a
			/// full update: the previously indexed terms are removed before the new ones are written.</summary>
			public async Task IndexAsync(IFdbTransaction trans, TId id, JsonObject document)
			{
				Contract.NotNull(trans);
				Contract.NotNull(id);
				Contract.NotNull(document);

				var documentKey = this.Subspace.Key(Documents, id);

				// Read the stored copy first. If the id already exists this is an update, so we must remove its OLD terms;
				// the only trustworthy source for those is the stored document, never a value the caller passes in (it may
				// be stale, which would leave orphaned postings). If it does not exist, this is a brand-new document.
				var existing = await trans.GetAsync(documentKey);
				if (existing.IsNull)
				{
					trans.AtomicIncrement64(this.Subspace.Key(Metadata, MetaDocCount));
				}
				else
				{
					this.Unindex(trans, id, JsonObject.ParseObject(existing));
				}

				// Store the new document (so a later Update/Remove can recompute these terms) and index it.
				trans.Set(documentKey, FdbValue.ToTextUtf8(document.ToJsonText()));
				this.Index(trans, id, document);
			}

			/// <summary>Removes the document with the given <paramref name="id"/>. Returns <see langword="false"/> if it did not exist.</summary>
			public async Task<bool> RemoveAsync(IFdbTransaction trans, TId id)
			{
				Contract.NotNull(trans);
				Contract.NotNull(id);

				var documentKey = this.Subspace.Key(Documents, id);
				var existing = await trans.GetAsync(documentKey);
				if (existing.IsNull)
				{
					return false;
				}

				// Recompute the old terms from the stored document and clear every posting they produced.
				this.Unindex(trans, id, JsonObject.ParseObject(existing));
				trans.Clear(documentKey);
				trans.AtomicDecrement64(this.Subspace.Key(Metadata, MetaDocCount), clearIfZero: false);
				return true;
			}

			/// <summary>Runs a query and returns the best <paramref name="limit"/> documents, most relevant first.</summary>
			public async Task<List<FtsHit<TId>>> SearchAsync(IFdbReadOnlyTransaction trans, FtsQuery query, int limit)
			{
				Contract.NotNull(trans);
				Contract.NotNull(query);
				Contract.Positive(limit);

				// N (the document count) feeds BM25's inverse-document-frequency, so read it once up front.
				long documentCount = await ReadCounterAsync(trans, this.Subspace.Key(Metadata, MetaDocCount));
				var scores = await this.EvaluateAsync(trans, query, documentCount);

				// Rank by score and keep the top-K. A sort is fine for a tutorial; a real engine would use a bounded heap.
				var hits = new List<FtsHit<TId>>(scores.Count);
				foreach (var (id, score) in scores)
				{
					hits.Add(new FtsHit<TId>(id, score));
				}
				hits.Sort(static (x, y) => y.Score.CompareTo(x.Score));
				if (hits.Count > limit)
				{
					hits.RemoveRange(limit, hits.Count - limit);
				}
				return hits;
			}

			/// <summary>Convenience "search bar": tokenizes <paramref name="queryText"/> and searches every token across all
			/// fields (weighted), combined with OR so a document matching more tokens ranks higher.</summary>
			public Task<List<FtsHit<TId>>> SearchAsync(IFdbReadOnlyTransaction trans, string queryText, int limit)
			{
				Contract.NotNull(trans);
				Contract.NotNull(queryText);

				// Split the typed text into terms, then OR them together. Each FtsTerm has no field, so EvaluateAsync scores
				// it across every configured field weighted: this is the multi-field "type anything" search, no catch-all field.
				var tokens = this.Schema.Analyzer.Analyze(queryText);
				if (tokens.Count == 0)
				{
					return Task.FromResult(new List<FtsHit<TId>>());
				}

				FtsQuery query = new FtsTerm(tokens[0]);
				for (int i = 1; i < tokens.Count; i++)
				{
					query = new FtsOr(query, new FtsTerm(tokens[i]));
				}
				return this.SearchAsync(trans, query, limit);
			}

			/// <summary>Convenience phrase search: analyzes <paramref name="phrase"/> with the index analyzer (so stop-words and casing are
			/// handled the same way as at index time), then runs it as an <see cref="FtsPhrase"/> with the given <paramref name="slop"/>.</summary>
			/// <param name="trans">The read-only transaction to use for the search.</param>
			/// <param name="phrase">The phrase to search for.</param>
			/// <param name="field">Field to match the phrase in, or <see langword="null"/> to match it across every configured field (weighted).</param>
			/// <param name="slop">The maximum number of positions to consider for phrase matching.</param>
			/// <param name="limit">The maximum number of results to return.</param>
			public Task<List<FtsHit<TId>>> SearchPhraseAsync(IFdbReadOnlyTransaction trans, string phrase, JsonPath? field, int slop, int limit)
			{
				Contract.NotNull(trans);
				Contract.NotNull(phrase);

				var terms = this.Schema.Analyzer.Analyze(phrase);
				if (terms.Count == 0)
				{
					return Task.FromResult(new List<FtsHit<TId>>());
				}
				return this.SearchAsync(trans, new FtsPhrase(terms, field, slop), limit);
			}

			// --- write path -------------------------------------------------------------------------------------------

			/// <summary>Writes the postings for <paramref name="document"/>: for each field, tokenize its value and record one
			/// posting per distinct term, bump that term's document frequency, and track the field length.</summary>
			private void Index(IFdbTransaction trans, TId id, JsonObject document)
			{
				foreach (var field in this.Schema.Fields)
				{
					string fieldKey = field.Path.ToString();
					var tokens = this.ExtractTerms(document, field.Path);
					if (tokens.Count == 0) continue;

					// Group each distinct term's positions (kept ascending, in field order) so a phrase/proximity query can test adjacency later.
					var positions = new Dictionary<string, List<int>>(StringComparer.Ordinal);
					foreach (var (term, position) in tokens)
					{
						if (!positions.TryGetValue(term, out var list))
						{
							positions[term] = list = [ ];
						}
						list.Add(position);
					}

					foreach (var (term, list) in positions)
					{
						// One posting for this (field, term, document); the value is the term's ascending positions (and its length is the term frequency).
						trans.Set(this.Subspace.Key(Postings, fieldKey, term, id), EncodePositions(list));
						// This document now contributes to the term's document frequency. AtomicIncrement avoids a
						// read-modify-write, so concurrent indexers never conflict on the shared counter.
						trans.AtomicIncrement64(this.Subspace.Key(TermStats, fieldKey, term));
					}

					// Field length and its running sum feed BM25's length normalisation (average field length = sum / N).
					trans.Set(this.Subspace.Key(DocLengths, id, fieldKey), FdbValue.ToFixed64LittleEndian(tokens.Count));
					trans.AtomicAdd64(this.Subspace.Key(Metadata, MetaSumLength, fieldKey), tokens.Count);
				}
			}

			/// <summary>Reverses <see cref="Index"/> for an old version of a document: clears its postings and decrements the
			/// per-term document frequency and the length sums.</summary>
			private void Unindex(IFdbTransaction trans, TId id, JsonObject document)
			{
				foreach (var field in this.Schema.Fields)
				{
					string fieldKey = field.Path.ToString();
					var tokens = this.ExtractTerms(document, field.Path);
					if (tokens.Count == 0) continue;

					// We only wrote one posting per DISTINCT term, so we only clear once per distinct term (and decrement df once).
					var distinct = new HashSet<string>(StringComparer.Ordinal);
					foreach (var (term, _) in tokens)
					{
						distinct.Add(term);
					}
					foreach (var term in distinct)
					{
						trans.Clear(this.Subspace.Key(Postings, fieldKey, term, id));
						// clearIfZero deletes the counter key once it reaches 0, so vanished terms leave nothing behind.
						trans.AtomicDecrement64(this.Subspace.Key(TermStats, fieldKey, term), clearIfZero: true);
					}

					trans.Clear(this.Subspace.Key(DocLengths, id, fieldKey));
					trans.AtomicAdd64(this.Subspace.Key(Metadata, MetaSumLength, fieldKey), -tokens.Count);
				}
			}

			/// <summary>Pulls the value at <paramref name="path"/> out of the document and runs it through the analyzer, tagging each
			/// term with its position (its index in the analyzed stream). A path that points at an array (e.g. multiple authors) is
			/// analyzed element by element, with a large position gap between elements so a phrase cannot span two elements.</summary>
			private List<(string Term, int Position)> ExtractTerms(JsonObject document, JsonPath path)
			{
				var tokens = new List<(string, int)>();
				if (!document.TryGetPathValue(path, out var value))
				{
					return tokens;
				}

				if (value is JsonArray array)
				{
					int next = 0;
					foreach (var item in array)
					{
						next = this.AppendTerms(tokens, item.ToStringOrDefault(), next);
					}
					return tokens;
				}

				this.AppendTerms(tokens, value.ToStringOrDefault(), 0);
				return tokens;
			}

			/// <summary>Analyzes <paramref name="text"/> and appends its terms starting at <paramref name="startPosition"/>; returns the
			/// position at which the NEXT array element should start (past a gap, so phrases never straddle an element boundary).</summary>
			private int AppendTerms(List<(string Term, int Position)> tokens, string? text, int startPosition)
			{
				int position = startPosition;
				foreach (var term in this.Schema.Analyzer.Analyze(text))
				{
					tokens.Add((term, position));
					position++;
				}
				return position + ArrayPositionGap;
			}

			// --- query path -------------------------------------------------------------------------------------------

			/// <summary>Evaluates a query node into a map of <c>document id -&gt; score</c>.</summary>
			private async Task<Dictionary<TId, double>> EvaluateAsync(IFdbReadOnlyTransaction trans, FtsQuery query, long documentCount)
			{
				switch (query)
				{
					case FtsTerm term:
					{
						// A term aimed at one field: score it there only.
						if (term.Field is JsonPath field)
						{
							return await this.ScoreFieldTermAsync(trans, field, 1.0, term.Term, documentCount);
						}

						// A term with no field: score it in EVERY configured field, scaled by that field's weight, and sum.
						// This is the multi-field search that makes a hit in a heavy field (a title) outrank a hit in a light
						// field (a blurb), with no duplicated "catch-all" field on disk.
						var accumulated = new Dictionary<TId, double>();
						foreach (var configured in this.Schema.Fields)
						{
							MergeSum(accumulated, await this.ScoreFieldTermAsync(trans, configured.Path, configured.Weight, term.Term, documentCount));
						}
						return accumulated;
					}
					case FtsPhrase phrase:
					{
						// A phrase aimed at one field: match it there only.
						if (phrase.Field is JsonPath field)
						{
							return await this.ScoreFieldPhraseAsync(trans, field, 1.0, phrase.Terms, phrase.Slop, documentCount);
						}

						// A phrase with no field: match it in EVERY configured field, scaled by that field's weight, and sum.
						var accumulated = new Dictionary<TId, double>();
						foreach (var configured in this.Schema.Fields)
						{
							MergeSum(accumulated, await this.ScoreFieldPhraseAsync(trans, configured.Path, configured.Weight, phrase.Terms, phrase.Slop, documentCount));
						}
						return accumulated;
					}
					case FtsOr or:
					{
						// Union: a document present on either side is a match; overlapping scores add up.
						var left = await this.EvaluateAsync(trans, or.Left, documentCount);
						MergeSum(left, await this.EvaluateAsync(trans, or.Right, documentCount));
						return left;
					}
					case FtsAnd and:
					{
						// Intersection: keep only documents present on both sides, and add their scores.
						var left = await this.EvaluateAsync(trans, and.Left, documentCount);
						var right = await this.EvaluateAsync(trans, and.Right, documentCount);
						var intersection = new Dictionary<TId, double>();
						foreach (var (id, score) in left)
						{
							if (right.TryGetValue(id, out double other))
							{
								intersection[id] = score + other;
							}
						}
						return intersection;
					}
					case FtsAndNot andNot:
					{
						// Difference: keep the included documents, drop any that also match the excluded side.
						var included = await this.EvaluateAsync(trans, andNot.Include, documentCount);
						var excluded = await this.EvaluateAsync(trans, andNot.Exclude, documentCount);
						foreach (var id in excluded.Keys)
						{
							included.Remove(id);
						}
						return included;
					}
					default:
					{
						throw new NotSupportedException($"Unsupported query node '{query.GetType().Name}'.");
					}
				}
			}

			/// <summary>Scores one term in one field using BM25, then multiplies every score by the field's <paramref name="weight"/>.</summary>
			private async Task<Dictionary<TId, double>> ScoreFieldTermAsync(IFdbReadOnlyTransaction trans, JsonPath path, double weight, string term, long documentCount)
			{
				var result = new Dictionary<TId, double>();
				string fieldKey = path.ToString();

				// Document frequency: how many documents contain this term in this field. If none (or the index is empty),
				// there is nothing to score.
				long documentFrequency = await ReadCounterAsync(trans, this.Subspace.Key(TermStats, fieldKey, term));
				if (documentFrequency <= 0 || documentCount <= 0)
				{
					return result;
				}

				double idf = Idf(documentCount, documentFrequency);
				double averageLength = await this.AverageLengthAsync(trans, fieldKey, documentCount);

				// One range read over (field, term, *) returns every document that uses the term in this field, with its positions.
				var postings = await this.ReadPostingsAsync(trans, fieldKey, term);
				foreach (var (id, positions) in postings)
				{
					// The term frequency is simply how many positions the term has in this field of this document.
					long docLength = await ReadCounterAsync(trans, this.Subspace.Key(DocLengths, id, fieldKey));
					result[id] = weight * Bm25(idf, positions.Length, docLength, averageLength);
				}

				return result;
			}

			/// <summary>Scores an ordered phrase/proximity match of <paramref name="terms"/> in one field, weighted by the field's <paramref name="weight"/>. Only documents
			/// where the terms occur in order within <paramref name="slop"/> are returned, each scored by the summed BM25 of its (co-occurring) terms.</summary>
			private async Task<Dictionary<TId, double>> ScoreFieldPhraseAsync(IFdbReadOnlyTransaction trans, JsonPath path, double weight, IReadOnlyList<string> terms, int slop, long documentCount)
			{
				var result = new Dictionary<TId, double>();
				if (terms.Count == 0 || documentCount <= 0)
				{
					return result;
				}

				string fieldKey = path.ToString();
				double averageLength = await this.AverageLengthAsync(trans, fieldKey, documentCount);

				// Read each term's postings (document -> positions) and its idf once. A term absent from this field means the phrase
				// can never match here, so abandon the whole field.
				var perTerm = new Dictionary<TId, int[]>[terms.Count];
				var idfs = new double[terms.Count];
				for (int i = 0; i < terms.Count; i++)
				{
					long documentFrequency = await ReadCounterAsync(trans, this.Subspace.Key(TermStats, fieldKey, terms[i]));
					if (documentFrequency <= 0)
					{
						return result;
					}
					idfs[i] = Idf(documentCount, documentFrequency);
					perTerm[i] = await this.ReadPostingsAsync(trans, fieldKey, terms[i]);
				}

				// Candidate documents are those containing EVERY term; iterate the rarest term's postings to keep the candidate set small.
				int pivot = 0;
				for (int i = 1; i < perTerm.Length; i++)
				{
					if (perTerm[i].Count < perTerm[pivot].Count) pivot = i;
				}

				var lists = new int[terms.Count][];
				foreach (var id in perTerm[pivot].Keys)
				{
					bool present = true;
					for (int i = 0; i < terms.Count; i++)
					{
						if (!perTerm[i].TryGetValue(id, out var positions))
						{
							present = false;
							break;
						}
						lists[i] = positions;
					}
					if (!present || !PhraseMatches(lists, slop))
					{
						continue;
					}

					long docLength = await ReadCounterAsync(trans, this.Subspace.Key(DocLengths, id, fieldKey));
					double score = 0.0;
					for (int i = 0; i < terms.Count; i++)
					{
						score += Bm25(idfs[i], lists[i].Length, docLength, averageLength);
					}
					result[id] = weight * score;
				}

				return result;
			}

			/// <summary>Reads all postings for (field, term): one range read returning every document that uses the term in the field, decoded to its positions.</summary>
			private async Task<Dictionary<TId, int[]>> ReadPostingsAsync(IFdbReadOnlyTransaction trans, string fieldKey, string term)
			{
				var result = new Dictionary<TId, int[]>();
				var postings = await trans.GetRange(this.Subspace.Key(Postings, fieldKey, term).ToRange()).ToListAsync();
				foreach (var kv in postings)
				{
					// DecodeLast pulls the document id back out of the tuple-encoded posting key.
					var id = this.Subspace.DecodeLast<TId>(kv.Key)!;
					result[id] = DecodePositions(kv.Value);
				}
				return result;
			}

			/// <summary>Returns true if the per-term position lists admit an in-order occurrence <c>q0 &lt; q1 &lt; ...</c> with
			/// <c>(last - first) - (n - 1) &lt;= slop</c> (slop 0 = strictly consecutive, an exact phrase). Each list is ascending.</summary>
			private static bool PhraseMatches(int[][] positionsPerTerm, int slop)
			{
				int termCount = positionsPerTerm.Length;
				int[] first = positionsPerTerm[0];
				if (termCount == 1)
				{
					return first.Length > 0;
				}

				// For each starting position of the first term, greedily take the smallest next position after the previous pick: that
				// minimises the last position for this start, so if even the tightest run exceeds the slop this start cannot match.
				foreach (int start in first)
				{
					int previous = start;
					bool chained = true;
					for (int i = 1; i < termCount; i++)
					{
						int next = NextGreaterThan(positionsPerTerm[i], previous);
						if (next < 0)
						{
							chained = false;
							break;
						}
						previous = next;
					}
					if (chained && (previous - start) - (termCount - 1) <= slop)
					{
						return true;
					}
				}
				return false;
			}

			/// <summary>Smallest value in the ascending <paramref name="sortedPositions"/> strictly greater than <paramref name="threshold"/>, or <c>-1</c> if none.</summary>
			private static int NextGreaterThan(int[] sortedPositions, int threshold)
			{
				int lo = 0, hi = sortedPositions.Length;
				while (lo < hi)
				{
					int mid = (int) (((uint) lo + (uint) hi) >> 1);
					if (sortedPositions[mid] <= threshold)
					{
						lo = mid + 1;
					}
					else
					{
						hi = mid;
					}
				}
				return lo < sortedPositions.Length ? sortedPositions[lo] : -1;
			}

			/// <summary>Encodes a term's ascending positions as a compact varint list (the posting value). Its length is the term frequency, so that is not stored separately.</summary>
			private static FdbRawValue EncodePositions(List<int> positions)
			{
				var writer = new SliceWriter(positions.Count * 2);
				foreach (int position in positions)
				{
					writer.WriteVarInt32((uint) position);
				}
				return FdbValue.ToBytes(writer.ToSlice());
			}

			/// <summary>Decodes the varint position list written by <see cref="EncodePositions"/>.</summary>
			private static int[] DecodePositions(Slice value)
			{
				if (value.Count == 0)
				{
					return [ ];
				}
				var reader = new SliceReader(value);
				var positions = new List<int>();
				while (reader.HasMore)
				{
					positions.Add((int) reader.ReadVarInt32());
				}
				return positions.ToArray();
			}

			/// <summary>The BM25 term score: more occurrences help with diminishing returns (<see cref="K1"/>), and a hit in a shorter-than-average field counts for more (<see cref="B"/>).</summary>
			private static double Bm25(double idf, long termFrequency, long docLength, double averageLength)
			{
				double denominator = termFrequency + K1 * (1.0 - B + (averageLength > 0 ? B * docLength / averageLength : 0.0));
				return idf * (termFrequency * (K1 + 1.0)) / (denominator <= 0 ? 1.0 : denominator);
			}

			/// <summary>BM25 inverse document frequency: rarer terms are more informative and score higher.</summary>
			private static double Idf(long documentCount, long documentFrequency)
				=> Math.Log(1.0 + (documentCount - documentFrequency + 0.5) / (documentFrequency + 0.5));

			/// <summary>Average length of <paramref name="fieldKey"/> across the corpus (sum of field lengths / N), used by BM25's length normalisation.</summary>
			private async Task<double> AverageLengthAsync(IFdbReadOnlyTransaction trans, string fieldKey, long documentCount)
			{
				long sumLength = await ReadCounterAsync(trans, this.Subspace.Key(Metadata, MetaSumLength, fieldKey));
				return (double) sumLength / documentCount;
			}

			/// <summary>Reads a little-endian 64-bit counter, treating a missing key as zero.</summary>
			private static async Task<long> ReadCounterAsync<TKey>(IFdbReadOnlyTransaction trans, TKey key)
				where TKey : struct, IFdbKey
			{
				var slice = await trans.GetAsync(key);
				return slice.IsNull ? 0L : slice.ToInt64();
			}

			/// <summary>Adds every score in <paramref name="source"/> into <paramref name="destination"/> (used to combine fields and OR branches).</summary>
			private static void MergeSum(Dictionary<TId, double> destination, Dictionary<TId, double> source)
			{
				foreach (var (id, score) in source)
				{
					destination[id] = destination.GetValueOrDefault(id) + score;
				}
			}

		}

	}

}
