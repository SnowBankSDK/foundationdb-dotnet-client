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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client.Tests
{
	using Bogus;

	public sealed record Album
	{
		public required Guid Id { get; init; }
		
		public required string Title { get; init; }
		
		public required string Artist { get; init; }
		
		public required string Genre { get; init; }
		
		public required DateTime ReleaseDate { get; init; }
		
		public required int Tracks { get; init; }

	}
	
	[TestFixture]
	[Parallelizable(ParallelScope.All)]
	public class FqlQueriesFacts : FdbTest
	{

		protected static void Explain(IFqlQuery? query)
		{
			if (query == null)
			{
				Log("# Query: <null>");
			}
			else
			{
				Log(query.Explain(prefix: "# "));
			}
		}

		public static List<Album> CreateAlbums(string user, int count)
		{
			var faker = new Faker<Album>()
				.UseSeed(HashCode.Combine(user, count))
				.RuleFor(a => a.Id, _ => Guid.NewGuid())
				.RuleFor(a => a.Title, f => f.Lorem.Sentence())
				.RuleFor(a => a.Artist, f => f.Name.FullName())
				.RuleFor(a => a.Genre, f => f.Music.Genre())
				.RuleFor(a => a.ReleaseDate, f => f.Date.Between(new(1970, 1, 1), DateTime.Now).Date)
				.RuleFor(a => a.Tracks, f => f.Random.Number(4, 12))
				;
			
			faker.AssertConfigurationIsValid();

			return faker.Generate(count);
		}
		
		[Test]
		public async Task Test_Can_Filter_Directories()
		{
			var db = await OpenTestPartitionAsync();

			var location = db.Root;
			await CleanLocation(db, location);

			// create the structure of directories
			await db.WriteAsync(async tr =>
			{
				var parent = await location.ResolveOrCreate(tr);

				foreach (var path in (string[])
				[
					"foo/bar/baz/jazz",
					"users/alice[tenant]/documents/music[table]",
					"users/alice/documents/photos[table]",
					"users/bob[tenant]/documents/music[table]",
					"users/bob/documents/photos[table]",
					"users/charlie[tenant]/documents/music[table]",
					"users/charlie/documents/photos[table]",
				])
				{
					await parent.CreateAsync(tr, FdbPath.Relative(FdbPath.Parse(path)));
				}
			}, this.Cancellation);

			// populate with some fake data

			var users = (string[]) [ "alice", "bob", "charlie" ];

			var rnd = new Random(12345678);

			var dataset = new Dictionary<string, List<(int RowId, Album Album)>>();

			int idCounter = 0;
			
			foreach (var user in users)
			{
				Log($"Generating data for user {user}...");

				var count = rnd.Next(1_000, 2_000);

				var albums = CreateAlbums(user, count);

				var set = new List<(int, Album)>(count);
				
				dataset[user] = set;
				
				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root[FdbPath.Parse($"users/{user}/documents/music")].Resolve(tr).ConfigureAwait(false);
					
					tr.Set(subspace.Key("name"), FdbValue.ToTextUtf8("Albums"));
					tr.Set(subspace.Key("count"), FdbValue.ToFixed32LittleEndian(count));
					
					for (int i = 0; i < albums.Count; i++)
					{
						var album = albums[i];
						var rid = ++idCounter;
						set.Add((rid, album));
						var json = CrystalJson.ToSlice(album, ArrayPool<byte>.Shared);
						tr.Set(subspace.Key(0, rid), json.Data);
						tr.Set(subspace.Key(1, album.Id), FdbValue.ToFixed32LittleEndian(rid));
						tr.Set(subspace.Key(2, album.Genre, rid ), FdbValue.Empty);
						tr.Set(subspace.Key(3, album.Artist, rid), FdbValue.Empty);
						tr.Set(subspace.Key(4, album.ReleaseDate.Year, rid), FdbValue.Empty);
						json.Dispose();
					}
				}, this.Cancellation);
			}

			await DumpTree(db, location);

			var genre = Choose(dataset[Choose(users)]).Album.Genre;
			var artist = Choose(dataset[Choose(users)]).Album.Artist;
			var year = Choose(dataset[Choose(users)]).Album.ReleaseDate.Year;

			var albumsByGenre = dataset.Values.SelectMany(v => v.Where(x => x.Album.Genre == genre)).ToList();
			var albumsByArtist = dataset.Values.SelectMany(v => v.Where(x => x.Album.Artist == artist)).ToList();
			var albumsByYear = dataset.Values.SelectMany(v => v.Where(x => x.Album.ReleaseDate.Year == year)).ToList();

			Log($"Querying Genre '{genre}', expect {albumsByGenre.Count:N0} results");
			await db.ReadAsync(async tr =>
			{
				// find all the "techno" albums from all the users
				var q = FqlQueryParser.Parse($"./users/<>/documents/music(2,\"{genre}\",<int>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);

				var results = new List<int>();
				await foreach (var match in q.Scan(tr, db.Root))
				{
					//Log($"- {match.Path}{match.Tuple} = {match.Value:V}");
					results.Add(match.Tuple.Get<int>(^1));
				}
				Log($"> Found {results.Count} results");
				Assert.That(results, Is.EquivalentTo(albumsByGenre.Select(x => x.RowId)));

			}, this.Cancellation);
			Log();

			Log($"Querying Artist '{artist}', expect {albumsByArtist.Count:N0} results");
			await db.ReadAsync(async tr =>
			{
				// find all the "techno" albums from all the users
				var q = FqlQueryParser.Parse($"./users/<>/documents/music(3,\"{artist}\",<int>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);

				var results = new List<int>();
				await foreach (var match in q.Scan(tr, db.Root))
				{
					//Log($"- {match.Path}{match.Tuple} = {match.Value:V}");
					results.Add(match.Tuple.Get<int>(^1));
				}
				Log($"> Found {results.Count} results");
				Assert.That(results, Is.EquivalentTo(albumsByArtist.Select(x => x.RowId)));

			}, this.Cancellation);
			Log();

			Log($"Querying Year {year}, expect {albumsByYear.Count:N0} results");
			await db.ReadAsync(async tr =>
			{
				// find all the "techno" albums from all the users
				var q = FqlQueryParser.Parse($"./users/<>/documents/music(4,{year},<int>)");
				Assert.That(q, Is.Not.Null);
				Explain(q);

				var results = new List<int>();
				await foreach (var match in q.Scan(tr, db.Root))
				{
					//Log($"- {match.Path}{match.Tuple} = {match.Value:V}");
					results.Add(match.Tuple.Get<int>(^1));
				}
				Log($"> Found {results.Count} results");
				Assert.That(results, Is.EquivalentTo(albumsByYear.Select(x => x.RowId)));

			}, this.Cancellation);
			Log();

		}

	}

}

#endif
