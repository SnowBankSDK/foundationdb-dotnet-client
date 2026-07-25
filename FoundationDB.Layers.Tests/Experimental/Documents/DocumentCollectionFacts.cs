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

namespace FoundationDB.Layers.Documents.Tests
{
	using FoundationDB.Types.ProtocolBuffers;
	using SnowBank.Data.Json.Binary;

	public abstract class DocumentCollectionFacts : FdbTest
	{

		private sealed record Book
		{
			public required int Id { get; init; }

			public required string Title { get; init; }

			public required string Author { get; init; }

			public DateOnly Published { get; init; }

			public int Pages { get; init; }
		}

		private static Book[] GetBooks()
		{
			return
			[
				new()
				{
					Id = 42,
					Title = "On the Origin of Species",
					Author = "Charles Darwin",
					Published = new DateOnly(1859, 11, 24),
					Pages = 502,
				},
				new()
				{
					Id = 43,
					Title = "Foundation and Empire",
					Author = "Isaac Asimov",
					Published = new DateOnly(1952, 1, 1),
					Pages = 247
				}
			];
		}

		[Test]
		public async Task Test_Can_Insert_And_Retrieve_Json_Documents()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			var docs = new FdbDocumentCollection<Book, int>(
				location,
				(book) => book.Id,
				CrystalJsonCodec.GetEncoder<Book>()
			);

			var books = GetBooks();

			// store a document
			var book1 = books[0];
			await db.WriteAsync((tr) => docs.InsertAsync(tr, book1), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif

			// retrieve the document
			var copy = await db.ReadAsync((tr) =>docs.LoadAsync(tr, book1.Id), this.Cancellation);

			Assert.That(copy, Is.Not.Null);
			Assert.That(copy.Id, Is.EqualTo(book1.Id));
			Assert.That(copy.Title, Is.EqualTo(book1.Title));
			Assert.That(copy.Author, Is.EqualTo(book1.Author));
			Assert.That(copy.Published, Is.EqualTo(book1.Published));
			Assert.That(copy.Pages, Is.EqualTo(book1.Pages));

			// store another document
			var book2 = books[1];
			await db.WriteAsync((tr) => docs.InsertAsync(tr, book2), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif
		}

		[Test]
		public async Task Test_Can_Insert_And_Retrieve_ProtoBuf_Documents()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			// quickly define the metatype for Books, because I'm too lazy to write a .proto for this, or add [ProtoMember] attributes everywhere
			var metaType = ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Book), false);
			metaType.Add("Id", "Title", "Author", "Published", "Pages");
			metaType.CompileInPlace();

			var docs = new FdbDocumentCollection<Book, int>(
				location,
				(book) => book.Id,
				new ProtobufCodec<Book>()
			);

			var books = GetBooks();

			// store a document
			var book1 = books[0];
			await db.WriteAsync((tr) => docs.InsertAsync(tr, book1), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif

			// retrieve the document
			var copy = await db.ReadAsync((tr) => docs.LoadAsync(tr, 42), this.Cancellation);

			Assert.That(copy, Is.Not.Null);
			Assert.That(copy.Id, Is.EqualTo(book1.Id));
			Assert.That(copy.Title, Is.EqualTo(book1.Title));
			Assert.That(copy.Author, Is.EqualTo(book1.Author));
			Assert.That(copy.Published, Is.EqualTo(book1.Published));
			Assert.That(copy.Pages, Is.EqualTo(book1.Pages));

			// store another document
			var book2 = books[1];
			await db.WriteAsync((tr) => docs.InsertAsync(tr, book2), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif
		}

	}


	/// <summary>Runs the suite against the in-memory FakeDb emulator (no Docker, no native client) for fast iteration.</summary>
	[TestFixture]
	public sealed class DocumentCollectionFactsFakeDbFacts : DocumentCollectionFacts
	{

		private FakeDbTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against a real FoundationDB cluster (Testcontainers). Run explicitly from the Unit Test Sessions UI or with the <c>RealCluster</c> category; requires a local Docker daemon and the native client.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public sealed class DocumentCollectionFactsRealClusterFacts : DocumentCollectionFacts
	{
		// inherits the full FdbTest behavior: container startup, native client probing, real connection
	}

}

