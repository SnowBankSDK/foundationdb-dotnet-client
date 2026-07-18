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

	}

}

#endif
