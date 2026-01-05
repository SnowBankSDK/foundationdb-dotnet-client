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

namespace FoundationDB.Layers.Tables.Tests
{
	using FoundationDB.Layers.Indexing;

	[TestFixture]
	public class IndexingFacts : FdbTest
	{

		[Test]
		public async Task Task_Can_Add_Update_Remove_From_Index()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				var index = new FdbIndex<int, string>(db.Root);

				// add items to the index
				await db.WriteAsync(async (tr) =>
				{
					await index.AddAsync(tr, 1, "red");
					await index.AddAsync(tr, 2, "green");
					await index.AddAsync(tr, 3, "blue");
					await index.AddAsync(tr, 4, "green");
					await index.AddAsync(tr, 5, "yellow");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db);
#endif

				// lookup values

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var reds = await index.Lookup(tr, "red").ToListAsync();
					Assert.That(reds, Is.EqualTo([ 1 ]));

					var greens = await index.Lookup(tr, "green").ToListAsync();
					Assert.That(greens, Is.EqualTo([ 2, 4 ]));

					var blues = await index.Lookup(tr, "blue").ToListAsync();
					Assert.That(blues, Is.EqualTo([ 3 ]));

					var yellows = await index.Lookup(tr, "yellow").ToListAsync();
					Assert.That(yellows, Is.EqualTo([ 5 ]));
				}

				// update

				await db.WriteAsync(async (tr) =>
				{
					await index.UpdateAsync(tr, 3, "indigo", "blue");
					await index.RemoveAsync(tr, 5, "yellow");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db);
#endif

				// check values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var reds = await index.Lookup(tr, "red").ToListAsync();
					Assert.That(reds, Is.EqualTo([ 1 ]));

					var greens = await index.Lookup(tr, "green").ToListAsync();
					Assert.That(greens, Is.EqualTo([ 2, 4 ]));

					var blues = await index.Lookup(tr, "blue").ToListAsync();
					Assert.That(blues.Count, Is.Zero);

					var yellows = await index.Lookup(tr, "yellow").ToListAsync();
					Assert.That(yellows.Count, Is.Zero);

					var indigos = await index.Lookup(tr, "indigo").ToListAsync();
					Assert.That(indigos, Is.EqualTo([ 3 ]));
				}

			}

		}

		[Test]
		public async Task Test_Can_Combine_Indexes()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				// summon our main cast
				List<Character> characters =
				[
					new() { Id = 1, Name = "Super Man", Brand = "DC", HasSuperPowers = true, IsVillain = false },
					new() { Id = 2, Name = "Batman", Brand = "DC", IsVillain = false },
					new() { Id = 3, Name = "Joker", Brand = "DC", IsVillain = true },
					new() { Id = 4, Name = "Iron Man", Brand = "Marvel", IsVillain = false },
					new() { Id = 5, Name = "Magneto", Brand = "Marvel", HasSuperPowers = true, IsVillain = true },
					new() { Id = 6, Name = "Cat Woman", Brand = "DC", IsVillain = null }
				];

				var indexBrand = new FdbIndex<long, string>(db.Root.WithPrefix(TuPack.EncodeKey("CharactersByBrand")));
				var indexSuperHero = new FdbIndex<long, bool>(db.Root.WithPrefix(TuPack.EncodeKey("SuperHeroes")));
				var indexAlignment = new FdbIndex<long, bool?>(db.Root.WithPrefix(TuPack.EncodeKey("FriendsOrFoe")));

				// index everything
				await db.WriteAsync(async (tr) =>
				{
					var indexBrandState = await indexBrand.Resolve(tr);
					var indexSuperHeroState = await indexSuperHero.Resolve(tr);
					var indexAlignmentState = await indexAlignment.Resolve(tr);

					foreach (var character in characters)
					{
						indexBrandState.Add(tr, character.Id, character.Brand);
						indexSuperHeroState.Add(tr, character.Id, character.HasSuperPowers);
						indexAlignmentState.Add(tr, character.Id, character.IsVillain);
					}

				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db);
#endif

				// super hereos only (sorry Batman!)
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var superHeroes = await indexSuperHero.Lookup(tr, value: true).ToListAsync();
					Log($"SuperHeroes: {string.Join(", ", superHeroes)}");
					Assert.That(superHeroes, Is.EqualTo(characters.Where(c => c.HasSuperPowers).Select(c => c.Id).ToList()));
				}

				// Versus !
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var dc = await indexBrand.Lookup(tr, value: "DC").ToListAsync();
					Log($"DC: {string.Join(", ", dc)}");
					Assert.That(dc, Is.EqualTo(characters.Where(c => c.Brand == "DC").Select(c => c.Id).ToList()));

					var marvel = await indexBrand.Lookup(tr, value: "Marvel").ToListAsync();
					Log($"Marvel: {string.Join(", ", dc)}");
					Assert.That(marvel, Is.EqualTo(characters.Where(c => c.Brand == "Marvel").Select(c => c.Id).ToList()));
				}

				// Vilains with superpowers are the worst
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var first = indexAlignment.Lookup(tr, value: true);
					var second = indexSuperHero.Lookup(tr, value: true);

					var merged = await first
						.Intersect(second)
						.ToListAsync();

					Assert.That(merged.Count, Is.EqualTo(1));
					Assert.That(merged[0] == characters.Single(c => c.Name == "Magneto").Id);
				}
			}

		}

		private sealed class Character
		{
			public long Id { get; init; }

			public required string Name { get; init; }

			public required string Brand { get; init; }

			public bool HasSuperPowers { get; init; }

			public bool? IsVillain { get; init; }
		}

	}

}
