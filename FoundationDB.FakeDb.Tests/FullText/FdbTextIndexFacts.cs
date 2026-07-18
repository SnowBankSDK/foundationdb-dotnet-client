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

#if !NETFRAMEWORK

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FoundationDB.Layers.FullText;
	using FoundationDB.Testing;
	using FoundationDB.Testing.Tests;
	using SnowBank.Data.Json;

	/// <summary>Behavioral tests for the experimental <see cref="FdbTextIndex{TId}"/> layer, run against the in-memory FakeDb
	/// (no cluster and no Docker: this fixture derives from <see cref="FakeDbTest"/>). The scenarios are adapted from the
	/// Apache Lucene / LeanCorpus style of "index a corpus, search, assert the result set and the ranking".</summary>
	[TestFixture]
	public class FdbTextIndexFacts : FakeDbTest
	{

		// The fields we index, with their relative importance. A hit in the title or author outranks a hit in the blurb,
		// which is exactly the "one search box, ranked by which attribute matched" behavior we want.
		private static readonly FdbTextField[] BookFields =
		[
			new("title", 5.0),
			new("author", 4.0),
			new("isbn", 4.0),
			new("genre", 2.0),
			new("blurb", 1.0),
		];

		// A small palette of well-known books across five genres, with a couple of shared authors and shared words planted
		// on purpose (Le Guin wrote one SF and one fantasy book; "dragon" is a title for one book and a blurb word for others).
		private static readonly (string Id, string Json)[] Books =
		[
			("dune",         """{ "title": "Dune", "author": "Frank Herbert", "genre": "science fiction", "isbn": "978-0441013593", "blurb": "On the desert planet Arrakis a young heir seizes the galaxy's most valuable resource." }"""),
			("neuromancer",  """{ "title": "Neuromancer", "author": "William Gibson", "genre": "science fiction", "isbn": "978-0441569595", "blurb": "A burned out hacker is hired for one last run against a powerful artificial intelligence." }"""),
			("foundation",   """{ "title": "Foundation", "author": "Isaac Asimov", "genre": "science fiction", "isbn": "978-0553293357", "blurb": "A mathematician predicts the fall of a galactic empire and works to shorten the coming dark age." }"""),
			("lefthand",     """{ "title": "The Left Hand of Darkness", "author": "Ursula K. Le Guin", "genre": "science fiction", "isbn": "978-0441478125", "blurb": "An envoy on a frozen world must navigate a society without fixed gender." }"""),
			("hyperion",     """{ "title": "Hyperion", "author": "Dan Simmons", "genre": "science fiction", "isbn": "978-0553283686", "blurb": "Seven pilgrims journey to a distant world and the deadly creature that waits there." }"""),
			("hobbit",       """{ "title": "The Hobbit", "author": "J.R.R. Tolkien", "genre": "fantasy", "isbn": "978-0547928227", "blurb": "A comfortable hobbit is swept into a quest to reclaim a treasure guarded by a dragon." }"""),
			("thrones",      """{ "title": "A Game of Thrones", "author": "George R.R. Martin", "genre": "fantasy", "isbn": "978-0553103540", "blurb": "Noble houses wage war for a throne while an ancient winter threatens them all." }"""),
			("earthsea",     """{ "title": "A Wizard of Earthsea", "author": "Ursula K. Le Guin", "genre": "fantasy", "isbn": "978-0553383041", "blurb": "A gifted young wizard unleashes a shadow and chases it across a world of islands and dragon lore." }"""),
			("namewind",     """{ "title": "The Name of the Wind", "author": "Patrick Rothfuss", "genre": "fantasy", "isbn": "978-0756404741", "blurb": "A legendary magician recounts how he became the most notorious wizard of his age." }"""),
			("mistborn",     """{ "title": "Mistborn", "author": "Brandon Sanderson", "genre": "fantasy", "isbn": "978-0765311788", "blurb": "In a world of ash and mist a street thief discovers a rare magic and joins a plot against a god emperor." }"""),
			("dragontattoo", """{ "title": "The Girl with the Dragon Tattoo", "author": "Stieg Larsson", "genre": "mystery", "isbn": "978-0307454546", "blurb": "A journalist and a brilliant hacker investigate a wealthy family's decades old disappearance." }"""),
			("gonegirl",     """{ "title": "Gone Girl", "author": "Gillian Flynn", "genre": "mystery", "isbn": "978-0307588371", "blurb": "When a wife vanishes on her anniversary her husband becomes the prime suspect." }"""),
			("bigsleep",     """{ "title": "The Big Sleep", "author": "Raymond Chandler", "genre": "mystery", "isbn": "978-0394758282", "blurb": "A private detective is hired by a dying millionaire and is pulled into blackmail and murder." }"""),
			("noneleft",     """{ "title": "And Then There Were None", "author": "Agatha Christie", "genre": "mystery", "isbn": "978-0062073488", "blurb": "Ten strangers are lured to an island and killed one by one." }"""),
			("shining",      """{ "title": "The Shining", "author": "Stephen King", "genre": "horror", "isbn": "978-0307743657", "blurb": "A haunted hotel preys on a troubled writer and his family through a long dark winter." }"""),
			("dracula",      """{ "title": "Dracula", "author": "Bram Stoker", "genre": "horror", "isbn": "978-0486411095", "blurb": "An ancient count travels to England to spread his undead curse." }"""),
			("nineteen",     """{ "title": "1984", "author": "George Orwell", "genre": "dystopian", "isbn": "978-0451524935", "blurb": "A clerk rebels against a total surveillance state that rewrites the past." }"""),
			("bravenew",     """{ "title": "Brave New World", "author": "Aldous Huxley", "genre": "dystopian", "isbn": "978-0060850524", "blurb": "An engineered society trades freedom for comfort and chemical happiness." }"""),
		];

		private static IFdbDatabase OpenDb()
		{
			var db = new FakeDbStore().OpenDatabase(null, readOnly: false);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return db;
		}

		private static FdbTextIndex<string> CreateIndex(IFdbDatabase db) => new(db.Root["fts"], BookFields);

		// FakeDbTest has no CleanLocation helper, so create the index directory ourselves (fresh FakeDb = nothing to clean).
		private Task EnsureLocationAsync(IFdbDatabase db, ISubspaceLocation location)
			=> db.WriteAsync(async tr => { _ = await db.DirectoryLayer.CreateOrOpenAsync(tr, location.Path); }, this.Cancellation);

		private async Task SeedAsync(IFdbDatabase db, FdbTextIndex<string> index)
		{
			foreach (var (id, json) in Books)
			{
				await db.WriteAsync(async tr =>
				{
					var state = await index.Resolve(tr);
					await state.IndexAsync(tr, id, JsonObject.ParseObject(json));
				}, this.Cancellation);
			}
		}

		private async Task<List<string>> SearchIdsAsync(IFdbDatabase db, FdbTextIndex<string> index, FtsQuery query, int limit = 20)
		{
			var hits = await db.ReadAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				return await state.SearchAsync(tr, query, limit);
			}, this.Cancellation);

			Log($"# {query} -> [{string.Join(", ", hits.ConvertAll(h => $"{h.Id}:{h.Score:F2}"))}]");
			return hits.ConvertAll(h => h.Id);
		}

		private async Task<List<string>> SearchIdsAsync(IFdbDatabase db, FdbTextIndex<string> index, string queryText, int limit = 20)
		{
			var hits = await db.ReadAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				return await state.SearchAsync(tr, queryText, limit);
			}, this.Cancellation);

			Log($"# '{queryText}' -> [{string.Join(", ", hits.ConvertAll(h => $"{h.Id}:{h.Score:F2}"))}]");
			return hits.ConvertAll(h => h.Id);
		}

		private async Task<Dictionary<string, double>> ScoresAsync(IFdbDatabase db, FdbTextIndex<string> index, FtsQuery query)
		{
			var hits = await db.ReadAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				return await state.SearchAsync(tr, query, 100);
			}, this.Cancellation);

			Log($"# {query} -> [{string.Join(", ", hits.ConvertAll(h => $"{h.Id}:{h.Score:F4}"))}]");
			var scores = new Dictionary<string, double>();
			foreach (var hit in hits)
			{
				scores[hit.Id] = hit.Score;
			}
			return scores;
		}

		[Test]
		public async Task Single_Field_Search_By_Author_And_By_Genre()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// "author" is its own field: "guin" belongs to Ursula K. Le Guin, who wrote one SF and one fantasy book here.
			var byAuthor = await this.SearchIdsAsync(db, index, new FtsTerm("guin", JsonPath.Create("author")));
			Assert.That(byAuthor, Is.EquivalentTo(new[] { "lefthand", "earthsea" }));

			// "genre" is a field too: five of the books are tagged fantasy.
			var fantasy = await this.SearchIdsAsync(db, index, new FtsTerm("fantasy", JsonPath.Create("genre")));
			Assert.That(fantasy, Is.EquivalentTo(new[] { "hobbit", "thrones", "earthsea", "namewind", "mistborn" }));
		}

		[Test]
		public async Task A_Title_Hit_Outranks_A_Blurb_Hit()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// "dragon" is the TITLE of The Girl with the Dragon Tattoo (weight 5) and a BLURB word for The Hobbit and Earthsea
			// (weight 1). Same term, three books, but the title match must come first.
			var ids = await this.SearchIdsAsync(db, index, "dragon");

			Assert.That(ids, Is.EquivalentTo(new[] { "dragontattoo", "hobbit", "earthsea" }));
			Assert.That(ids[0], Is.EqualTo("dragontattoo"));
		}

		[Test]
		public async Task Search_Bar_Finds_Across_Fields_Ranked_By_Coverage()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// The "one prompt to find them all": "ursula science fiction" matches The Left Hand of Darkness on BOTH the
			// author (ursula) and the genre (science fiction); Earthsea matches only the author; the other SF books match
			// only the genre. The best-covered book must rank first, with no concatenated catch-all field.
			var ids = await this.SearchIdsAsync(db, index, "ursula science fiction");

			Assert.That(ids[0], Is.EqualTo("lefthand"));
			Assert.That(ids, Contains.Item("earthsea"));
			Assert.That(ids, Contains.Item("dune"));
		}

		[Test]
		public async Task Updating_A_Field_Changes_The_Results()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// Before: two books are tagged dystopian.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("dystopian", JsonPath.Create("genre"))), Is.EquivalentTo(new[] { "nineteen", "bravenew" }));

			// Reclassify 1984 from dystopian to science fiction (re-indexing the same id is a full update).
			await db.WriteAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				await state.IndexAsync(tr, "nineteen", JsonObject.ParseObject("""{ "title": "1984", "author": "George Orwell", "genre": "science fiction", "isbn": "978-0451524935", "blurb": "A clerk rebels against a total surveillance state that rewrites the past." }"""));
			}, this.Cancellation);

			// After: only Brave New World is still dystopian.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("dystopian", JsonPath.Create("genre"))), Is.EqualTo(new[] { "bravenew" }));
		}

		[Test]
		public async Task Removing_A_Book_Removes_Its_Terms()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// "haunted" appears only in The Shining's blurb.
			Assert.That(await this.SearchIdsAsync(db, index, "haunted"), Is.EqualTo(new[] { "shining" }));

			bool removed = await db.ReadWriteAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				return await state.RemoveAsync(tr, "shining");
			}, this.Cancellation);

			Assert.That(removed, Is.True);
			Assert.That(await this.SearchIdsAsync(db, index, "haunted"), Is.Empty);
		}

		[Test]
		public async Task Exact_Phrase_Matches_Only_Consecutive_Terms()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// "The Girl with the Dragon Tattoo" analyzes (stop-words dropped) to [girl, dragon, tattoo] at positions 0,1,2.
			var title = JsonPath.Create("title");

			// An exact phrase finds the consecutive pair "dragon tattoo"...
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["dragon", "tattoo"], title)), Is.EqualTo(new[] { "dragontattoo" }));

			// ...but "girl tattoo" co-occur in the same title WITHOUT being adjacent (girl@0, tattoo@2), so at slop 0 there is no match.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["girl", "tattoo"], title)), Is.Empty);
		}

		[Test]
		public async Task Phrase_Is_Ordered()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			var title = JsonPath.Create("title");

			// Forward order matches (dragon@1, tattoo@2)...
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["dragon", "tattoo"], title)), Is.EqualTo(new[] { "dragontattoo" }));

			// ...the reverse "tattoo dragon" does not: a phrase matches its terms in order only.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["tattoo", "dragon"], title)), Is.Empty);
		}

		[Test]
		public async Task Proximity_Slop_Bridges_A_Gap()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			var title = JsonPath.Create("title");

			// girl@0 and tattoo@2 sit one position beyond adjacency: slop 0 rejects them, slop 1 accepts them (proximity).
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["girl", "tattoo"], title, Slop: 0)), Is.Empty);
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["girl", "tattoo"], title, Slop: 1)), Is.EqualTo(new[] { "dragontattoo" }));
		}

		[Test]
		public async Task Phrase_Search_Drops_Stop_Words_On_Both_Sides()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			// "The Left Hand of Darkness" indexes as [left, hand, darkness]; typing the phrase WITH its stop-words still matches,
			// because SearchPhraseAsync runs the same analyzer over the query (dropping "the"/"of") before matching positions.
			var hits = await db.ReadAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				return await state.SearchPhraseAsync(tr, "the left hand", JsonPath.Create("title"), slop: 0, limit: 20);
			}, this.Cancellation);

			Assert.That(hits.ConvertAll(h => h.Id), Is.EqualTo(new[] { "lefthand" }));
		}

		[Test]
		public async Task Phrase_Does_Not_Span_Array_Elements()
		{
			using var db = OpenDb();

			// A field whose value is an ARRAY: each element is analyzed separately, and a large position gap is inserted between
			// elements so a phrase cannot match the last word of one element followed by the first word of the next.
			var index = new FdbTextIndex<string>(db.Root["fts-array"], [new FdbTextField("tags", 1.0)]);
			await this.EnsureLocationAsync(db, index.Location);
			await db.WriteAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				await state.IndexAsync(tr, "d1", JsonObject.ParseObject("""{ "tags": [ "red car", "blue bike" ] }"""));
			}, this.Cancellation);

			var tags = JsonPath.Create("tags");

			// "red car" is a phrase WITHIN one element -> matches.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["red", "car"], tags)), Is.EqualTo(new[] { "d1" }));

			// "car blue" straddles the element boundary (car ends element 0, blue starts element 1) -> no match.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["car", "blue"], tags)), Is.Empty);
		}

		[Test]
		public async Task Bm25_Scores_Match_The_Hand_Computed_Formula()
		{
			// There is no standard corpus of "correct" BM25 scores to assert against: BM25 is a family of variants (see Kamphuis et al.,
			// "Which BM25 Do You Mean?", ECIR 2020) that differ in the idf form and the (k1+1) numerator, so engines legitimately disagree
			// on absolute scores. The definitive check for THIS layer is therefore a worked example computed by hand from the exact formula
			// it implements, on a corpus small enough to derive every input:
			//
			//   score = idf * (tf * (k1+1)) / (tf + k1*(1 - b + b*dl/avgdl)),   k1 = 1.2, b = 0.75,
			//   idf   = ln(1 + (N - df + 0.5) / (df + 0.5))                     (the Lucene idf; our numerator keeps the textbook (k1+1))
			//
			// Single field "body", weight 1.0 (so the returned score IS the raw BM25). Analyzer drops "the". Three documents:
			//   A "quick brown fox" -> [quick, brown, fox]  dl = 3
			//   B "quick quick"     -> [quick, quick]       dl = 2   (tf(quick) = 2)
			//   C "brown"           -> [brown]              dl = 1
			//   N = 3,  sumlen = 6,  avgdl = 2.0.  df(quick) = 2, df(brown) = 2, df(fox) = 1.
			//
			//   idf(df=2) = ln(1 + 1.5/2.5)   = ln(1.60000) = 0.47000363
			//   idf(df=1) = ln(1 + 2.5/1.5)   = ln(2.66667) = 0.98082925
			//   quick@B: tf=2 dl=2 -> 0.47000363 * 4.4/3.20 = 0.47000363 * 1.375000 = 0.64625499
			//   quick@A: tf=1 dl=3 -> 0.47000363 * 2.2/2.65 = 0.47000363 * 0.830189 = 0.39019170
			//   brown@C: tf=1 dl=1 -> 0.47000363 * 2.2/1.75 = 0.47000363 * 1.257143 = 0.59086171
			//   brown@A: tf=1 dl=3 -> (same denominator as quick@A)             = 0.39019170
			//   fox@A:   tf=1 dl=3 -> 0.98082925 * 2.2/2.65 = 0.98082925 * 0.830189 = 0.81427335

			using var db = OpenDb();
			var index = new FdbTextIndex<string>(db.Root["fts-bm25"], [new FdbTextField("body", 1.0)]);
			await this.EnsureLocationAsync(db, index.Location);

			foreach (var (id, json) in new[]
			{
				("A", """{ "body": "quick brown fox" }"""),
				("B", """{ "body": "quick quick" }"""),
				("C", """{ "body": "brown" }"""),
			})
			{
				await db.WriteAsync(async tr =>
				{
					var state = await index.Resolve(tr);
					await state.IndexAsync(tr, id, JsonObject.ParseObject(json));
				}, this.Cancellation);
			}

			var body = JsonPath.Create("body");

			// tf saturation + length normalization: B (tf=2, short) outscores A (tf=1, long), both matching the exact hand-computed values.
			var quick = await this.ScoresAsync(db, index, new FtsTerm("quick", body));
			Assert.That(quick["B"], Is.EqualTo(0.64625499).Within(1e-6));
			Assert.That(quick["A"], Is.EqualTo(0.39019170).Within(1e-6));

			// Length normalization alone: same tf, but C (dl=1) outscores A (dl=3).
			var brown = await this.ScoresAsync(db, index, new FtsTerm("brown", body));
			Assert.That(brown["C"], Is.EqualTo(0.59086171).Within(1e-6));
			Assert.That(brown["A"], Is.EqualTo(0.39019170).Within(1e-6));

			// idf: a rarer term (df=1) scores higher than a common one (df=2) at equal tf/length.
			var fox = await this.ScoresAsync(db, index, new FtsTerm("fox", body));
			Assert.That(fox["A"], Is.EqualTo(0.81427335).Within(1e-6));
			Assert.That(fox["A"], Is.GreaterThan(quick["A"]));
		}

		[Test]
		public async Task Bm25_Weighted_Sum_Across_Fields_Matches_The_Hand_Computed_Formula()
		{
			// The layer's headline is per-field weighted ranking: a term with no field is scored in EVERY field with that field's
			// own idf / average-length / document-length, multiplied by the field weight, and the per-field contributions are summed.
			// Two fields: title (weight 3.0) and body (weight 1.0). Three documents (the analyzer changes nothing here):
			//   D1  title "alpha"   body "alpha beta"
			//   D2  title "beta"    body "alpha"
			//   D3  title "gamma"   body "gamma"        N = 3.
			//   title lengths 1,1,1 -> avgdl_title = 1.0  ; df_title(alpha) = 1  (D1)
			//   body  lengths 2,1,1 -> avgdl_body  = 4/3  ; df_body(alpha)  = 2  (D1, D2)
			//   idf_title(df=1) = ln(1 + 2.5/1.5) = 0.98082925 ; idf_body(df=2) = ln(1 + 1.5/2.5) = 0.47000363
			//
			//   D1 (alpha in BOTH fields):
			//     title tf=1 dl=1 -> 0.98082925 * 2.2/2.200 = 0.98082925 ; * weight 3.0 = 2.94248776
			//     body  tf=1 dl=2 -> 0.47000363 * 2.2/2.650 = 0.39019169 ; * weight 1.0 = 0.39019169   => 3.33267945
			//   D2 (alpha in body only):
			//     body  tf=1 dl=1 -> 0.47000363 * 2.2/1.975 = 0.52354830 ; * weight 1.0            => 0.52354830
			//   D3: no alpha -> not a hit.

			using var db = OpenDb();
			var index = new FdbTextIndex<string>(db.Root["fts-weighted"], [new FdbTextField("title", 3.0), new FdbTextField("body", 1.0)]);
			await this.EnsureLocationAsync(db, index.Location);

			foreach (var (id, json) in new[]
			{
				("D1", """{ "title": "alpha", "body": "alpha beta" }"""),
				("D2", """{ "title": "beta",  "body": "alpha" }"""),
				("D3", """{ "title": "gamma", "body": "gamma" }"""),
			})
			{
				await db.WriteAsync(async tr =>
				{
					var state = await index.Resolve(tr);
					await state.IndexAsync(tr, id, JsonObject.ParseObject(json));
				}, this.Cancellation);
			}

			// A term with no field is scored across all fields, weighted and summed.
			var alpha = await this.ScoresAsync(db, index, new FtsTerm("alpha"));

			Assert.That(alpha.Keys, Is.EquivalentTo(new[] { "D1", "D2" }));
			Assert.That(alpha["D1"], Is.EqualTo(3.33267945).Within(1e-5)); // the heavy title field dominates
			Assert.That(alpha["D2"], Is.EqualTo(0.52354830).Within(1e-5));
			Assert.That(alpha["D1"], Is.GreaterThan(alpha["D2"]));
		}

		[Test]
		public async Task Empty_Index_Returns_No_Hits()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			// Deliberately not seeded: every query surface must cope with an empty corpus (document count 0) without throwing.

			Assert.That(await this.SearchIdsAsync(db, index, "dragon"), Is.Empty);
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("dragon", JsonPath.Create("title"))), Is.Empty);
			Assert.That(await this.SearchIdsAsync(db, index, new FtsPhrase(["dragon", "tattoo"], JsonPath.Create("title"))), Is.Empty);
		}

		[Test]
		public async Task Unknown_Term_Returns_No_Hits()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			Assert.That(await this.SearchIdsAsync(db, index, "zzzznonexistent"), Is.Empty);
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("zzzznonexistent", JsonPath.Create("title"))), Is.Empty);
		}

		[Test]
		public async Task Analyzer_Is_Case_Insensitive_And_Keeps_Accented_Letters()
		{
			using var db = OpenDb();
			var index = new FdbTextIndex<string>(db.Root["fts-unicode"], [new FdbTextField("name", 1.0)]);
			await this.EnsureLocationAsync(db, index.Location);
			await db.WriteAsync(async tr =>
			{
				var state = await index.Resolve(tr);
				// "Grande CAFE" where the last letter is an accented, upper-case E (U+00C9); the source file is UTF-8, which the C# compiler reads by default.
				await state.IndexAsync(tr, "d1", JsonObject.ParseObject("{ \"name\": \"Grande CAFÉ\" }"));
			}, this.Cancellation);

			var name = JsonPath.Create("name");

			// Lowercased at index time, and the accented letter is a term character (not a separator): "café" (é = 'é') matches.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("café", name)), Is.EqualTo(new[] { "d1" }));
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("grande", name)), Is.EqualTo(new[] { "d1" }));

			// No diacritic folding (documented simplification): plain "cafe" is a different term and does not match.
			Assert.That(await this.SearchIdsAsync(db, index, new FtsTerm("cafe", name)), Is.Empty);
		}

		[Test]
		public async Task And_Or_Compose_Scores_By_Summation()
		{
			using var db = OpenDb();
			var index = CreateIndex(db);
			await this.EnsureLocationAsync(db, index.Location);
			await this.SeedAsync(db, index);

			var author = JsonPath.Create("author");
			var genre = JsonPath.Create("genre");

			var guin = await this.ScoresAsync(db, index, new FtsTerm("guin", author));
			var fantasy = await this.ScoresAsync(db, index, new FtsTerm("fantasy", genre));
			var and = await this.ScoresAsync(db, index, new FtsAnd(new FtsTerm("guin", author), new FtsTerm("fantasy", genre)));
			var or = await this.ScoresAsync(db, index, new FtsOr(new FtsTerm("guin", author), new FtsTerm("fantasy", genre)));

			// AND keeps only documents matching both sides: earthsea is the one Le Guin book that is also tagged fantasy.
			Assert.That(and.Keys, Is.EquivalentTo(new[] { "earthsea" }));
			// AND and OR both add the two sides' scores for a document present on both.
			Assert.That(and["earthsea"], Is.EqualTo(guin["earthsea"] + fantasy["earthsea"]).Within(1e-9));
			Assert.That(or["earthsea"], Is.EqualTo(guin["earthsea"] + fantasy["earthsea"]).Within(1e-9));
			// OR also keeps documents from either side alone: lefthand is the Le Guin science-fiction book (matches guin, not fantasy).
			Assert.That(or.Keys, Contains.Item("lefthand"));
		}

	}

}

#endif
