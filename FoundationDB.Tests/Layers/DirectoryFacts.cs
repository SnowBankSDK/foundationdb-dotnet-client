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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable RedundantCast
// ReSharper disable ReplaceAsyncWithTaskReturn

#define ENABLE_LOGGING

namespace FoundationDB.Client.Tests
{
	using System.Diagnostics.CodeAnalysis;
#if ENABLE_LOGGING
	using FoundationDB.Filters.Logging;
#endif
	using FoundationDB.Layers.Allocators;

	[TestFixture]
	public class DirectoryLayerFacts : FdbTest
	{

		[Test]
		public async Task Test_Allocator()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("hca"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => list.Add(log));
#endif

			var hpa = new FdbHighContentionAllocator(location);

			var ids = new HashSet<long>();

			// allocate a single new id
			long id = await hpa.ReadWriteAsync(db, (tr, state) => state.AllocateAsync(tr), this.Cancellation);
			ids.Add(id);

			await DumpSubspace(db, location);

			// allocate a batch of new ids
			for (int i = 0; i < 100; i++)
			{
				id = await hpa.ReadWriteAsync(db, (tr, state) => state.AllocateAsync(tr), this.Cancellation);
				if (ids.Contains(id))
				{
					await DumpSubspace(db, location);
					Assert.Fail($"Duplicate key allocated: {id} (#{i})");
				}

				ids.Add(id);
			}

			await DumpSubspace(db, location);

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		[Test]
		public async Task Test_CreateOrOpen_Simple()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
			var dl = FdbDirectoryLayer.Create(location);

			Assert.That(dl.Content, Is.Not.Null);
			Assert.That(dl.Content, Is.EqualTo(location));

			// first call should create a new subspace (with a random prefix)
			FdbDirectorySubspace foo;

			Log("Before creating 'Foo':");
			await DumpSubspace(db, location);

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				foo = await dl.CreateOrOpenAsync(tr, FdbPath.Parse("/Foo"));
				await tr.CommitAsync();
			}

#if DEBUG
			Log("After creating 'Foo':");
			await DumpSubspace(db, location);
#endif

			Assert.That(foo, Is.Not.Null);
			Assert.That(foo.FullName, Is.EqualTo("/Foo"), "foo.FullName");
			Assert.That(foo.Path, Is.EqualTo(FdbPath.Parse("/Foo")), "foo.Path");
			Assert.That(foo.Layer, Is.EqualTo(string.Empty), "foo.Layer");
			Assert.That(foo.DirectoryLayer, Is.SameAs(dl), "foo.DirectoryLayer");
			Assert.That(foo.Context, Is.Not.Null, ".Context");
			Assert.That(foo.Descriptor, Is.Not.Null, ".Descriptor");
			Assert.That(foo.Descriptor.Prefix, Is.EqualTo(foo.GetPrefixUnsafe()));
			Assert.That(foo.Descriptor.Path, Is.EqualTo(FdbPath.Absolute("Foo")));
			Assert.That(foo.Descriptor.Layer, Is.EqualTo(string.Empty));
			Assert.That(foo.Descriptor.ValidationChain, Is.Not.Null);
			DumpValidationChange(foo);
			Assert.That(foo.Descriptor.ValidationChain[0].Key, Is.EqualTo(foo.Descriptor.Partition.GetStampKey()));
			Assert.That(foo.Descriptor.ValidationChain, Has.Count.EqualTo(1));

			// second call should return the same subspace

			var foo2 = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo")), this.Cancellation);
			Assert.That(foo2, Is.Not.Null);
			Assert.That(foo2.FullName, Is.EqualTo("/Foo"), "foo2.FullName");
			Assert.That(foo2.Path, Is.EqualTo(FdbPath.Parse("/Foo")), "foo2.Path");
			Assert.That(foo2.Layer, Is.EqualTo(string.Empty), "foo2.Layer");
			Assert.That(foo2.DirectoryLayer, Is.SameAs(dl), "foo2.DirectoryLayer");
			Assert.That(foo2.Context, Is.Not.Null, "foo2.Context");
			Assert.That(foo.Descriptor, Is.Not.Null, "foo2.Descriptor");
			DumpValidationChange(foo2);

			Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()), "Second call to CreateOrOpen should return the same subspace");
		}

		[Test]
		public async Task Test_CreateOrOpen_With_Layer()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => list.Add(log));
#endif

			// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
			var dl = FdbDirectoryLayer.Create(location);

			Assert.That(dl.Content, Is.Not.Null);
			Assert.That(dl.Content, Is.EqualTo(location));

			// first call should create a new subspace (with a random prefix)
			var foo = await db.ReadWriteAsync(tr => dl.CreateOrOpenAsync(tr, FdbPath.Parse("/Foo[AcmeLayer]")), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif

			var segFoo = FdbPathSegment.Create("Foo", "AcmeLayer");

			Assert.That(foo, Is.Not.Null);
			Assert.That(foo.FullName, Is.EqualTo("/Foo"));
			Assert.That(foo.Path, Is.EqualTo(FdbPath.Absolute(segFoo)));
			Assert.That(foo.Name, Is.EqualTo("Foo"));
			Assert.That(foo.Layer, Is.EqualTo("AcmeLayer"));
			Assert.That(foo.DirectoryLayer, Is.SameAs(dl));

			// second call should return the same subspace
			var foo2 = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo[AcmeLayer]")), this.Cancellation);
			Assert.That(foo2, Is.Not.Null);
			Assert.That(foo2.FullName, Is.EqualTo("/Foo"));
			Assert.That(foo2.Path, Is.EqualTo(FdbPath.Absolute(segFoo)));
			Assert.That(foo2.Name, Is.EqualTo("Foo"));
			Assert.That(foo2.Layer, Is.EqualTo("AcmeLayer"));
			Assert.That(foo2.DirectoryLayer, Is.SameAs(dl));
			Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()), "Second call to CreateOrOpen should return the same subspace");

			// opening it with wrong layer id should fail
			Assert.That(async () => await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo[OtherLayer]")), this.Cancellation), Throws.InstanceOf<InvalidOperationException>(), "Opening with invalid layer id should fail");

			// opening without specifying a layer should disable the layer check
			var foo3 = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo")), this.Cancellation);
			Assert.That(foo3, Is.Not.Null);
			Assert.That(foo3.Layer, Is.EqualTo("AcmeLayer"));

			// CheckLayer with the correct value should pass
			Assert.DoesNotThrow(() => foo3.CheckLayer("AcmeLayer"), "CheckLayer should not throw if the layer id is correct");

			// CheckLayer with the incorrect value should fail
			Assert.That(() => foo3.CheckLayer("OtherLayer"), Throws.InstanceOf<InvalidOperationException>(), "CheckLayer should throw if the layer id is not correct");

			// CheckLayer with empty string should do nothing
			foo3.CheckLayer("");

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		private void DumpValidationChange(FdbDirectorySubspace subspace)
		{
			Log($"Validation chain for {subspace.Location}:");
			foreach (var kv in subspace.Descriptor.ValidationChain)
			{
				Log($"- {kv.Key:K} = {kv.Value:V}");
			}
		}

		[Test]
		public async Task Test_CreateOrOpen_SubFolder()
		{
			// Create a folder ("foo", "bar", "baz") and ensure that all the parent folder are also creating and linked properly

			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => list.Add(log));
#endif

			// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
			var dl = FdbDirectoryLayer.Create(location);

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var folder = await dl.CreateOrOpenAsync(tr, FdbPath.Parse("/Foo/Bar/Baz"));
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.FullName, Is.EqualTo("/Foo/Bar/Baz"));
				Assert.That(folder.Path, Is.EqualTo(FdbPath.Absolute("Foo", "Bar", "Baz")));
				await tr.CommitAsync();
			}
#if DEBUG
			await DumpSubspace(db, location);
#endif

			// all the parent folders should also now exist
			var foo = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo")), this.Cancellation);
			Assert.That(foo, Is.Not.Null);
			Assert.That(foo.FullName, Is.EqualTo("/Foo"));
			Assert.That(foo.Descriptor, Is.Not.Null);
			Assert.That(foo.Descriptor.ValidationChain, Is.Not.Null);
			DumpValidationChange(foo);
			Assert.That(foo.Descriptor.ValidationChain, Has.Count.EqualTo(1));
			Assert.That(foo.Descriptor.ValidationChain[0].Key, Is.EqualTo(foo.Descriptor.Partition.GetStampKey()));

			var bar = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo/Bar")), this.Cancellation);
			Assert.That(bar, Is.Not.Null);
			Assert.That(bar.FullName, Is.EqualTo("/Foo/Bar"));
			Assert.That(bar.Descriptor, Is.Not.Null);
			Assert.That(bar.Descriptor.ValidationChain, Is.Not.Null);
			DumpValidationChange(bar);
			Assert.That(bar.Descriptor.ValidationChain, Has.Count.EqualTo(1));
			Assert.That(bar.Descriptor.ValidationChain[0], Is.EqualTo(foo.Descriptor.ValidationChain[0]));

			var baz = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo/Bar/Baz")), this.Cancellation);
			Assert.That(baz, Is.Not.Null);
			Assert.That(baz.FullName, Is.EqualTo("/Foo/Bar/Baz"));
			Assert.That(baz.Descriptor, Is.Not.Null);
			Assert.That(baz.Descriptor.ValidationChain, Is.Not.Null);
			DumpValidationChange(baz);
			Assert.That(baz.Descriptor.ValidationChain[0], Is.EqualTo(foo.Descriptor.ValidationChain[0]));

			// We can also access /Foo/Bar via 'Foo'
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				foo = await dl.OpenAsync(tr, FdbPath.Parse("/Foo"));
				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.FullName, Is.EqualTo("/Foo"));

				// via relative path
				bar = await foo.OpenAsync(tr, FdbPath.Relative("Bar"));
				Assert.That(bar, Is.Not.Null);
				Assert.That(bar.FullName, Is.EqualTo("/Foo/Bar"));

				// via absolute path
				bar = await foo.OpenAsync(tr, FdbPath.Parse("/Foo/Bar"));
				Assert.That(bar, Is.Not.Null);
				Assert.That(bar.FullName, Is.EqualTo("/Foo/Bar"));

				// opening a non existing folder should fail
				Assert.That(async () => await foo.OpenAsync(tr, FdbPath.Relative("Baz")), Throws.Exception, "Open on a missing folder should fail");
				Assert.That(await foo.TryOpenAsync(tr, FdbPath.Relative("Baz")), Is.Null, "TryOpen on a missing folder should return null");

				// attempting to open a "foreign" folder via "foo" should fail
				Assert.That(async () => await foo.OpenAsync(tr, FdbPath.Parse("/Other/Bar")), Throws.InvalidOperationException, "Should not be able to open a sub-folder with a path outside its parent");
				Assert.That(async () => await foo.TryOpenAsync(tr, FdbPath.Parse("/Other/Bar")), Throws.InvalidOperationException, "Should not be able to open a sub-folder with a path outside its parent");
			}

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		[Test]
		public async Task Test_CreateOrOpen_AutoCreate_Nested_SubFolders()
		{
			// Create a deeply nested subfolder and verify that all parents are also created with the proper layer ids

			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => { list.Add(log); });
#endif

			var dl = FdbDirectoryLayer.Create(location);

			var path = FdbPath.Parse("/Outer[partition]/Foo/Inner[partition]/Bar[BarLayer]/Baz[BazLayer]");
			// pre-flight checks...
			Assert.That(path.Count, Is.EqualTo(5));
			Assert.That(path[0], Is.EqualTo(FdbPathSegment.Partition("Outer")));
			Assert.That(path[1], Is.EqualTo(FdbPathSegment.Create("Foo")));
			Assert.That(path[2], Is.EqualTo(FdbPathSegment.Partition("Inner")));
			Assert.That(path[3], Is.EqualTo(FdbPathSegment.Create("Bar", "BarLayer")));
			Assert.That(path[4], Is.EqualTo(FdbPathSegment.Create("Baz", "BazLayer")));

			var folder = await db.ReadWriteAsync(tr => dl.CreateOrOpenAsync(tr, path), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif

			Assert.That(folder, Is.Not.Null);
			Assert.That(folder.FullName, Is.EqualTo("/Outer/Foo/Inner/Bar/Baz"));
			Assert.That(folder.Path, Is.EqualTo(path));

			// all the parent folders should also now exist
			Log("Checking parents from the root:");
			var outer = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Outer")), this.Cancellation);
			Assert.That(outer, Is.Not.Null);
			Log($"- {outer} @ {outer.GetPrefixUnsafe()}");
			Assert.That(outer.FullName, Is.EqualTo("/Outer"));
			Assert.That(outer.Layer, Is.EqualTo("partition"));
			Assert.That(outer.GetPrefixUnsafe().StartsWith(location.Prefix), Is.True, $"Outer prefix {outer.GetPrefixUnsafe()} MUST starts with DL content location prefix {dl.Content.Prefix} because it is contained in that partition");

			var foo = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Outer/Foo")), this.Cancellation);
			Assert.That(foo, Is.Not.Null);
			Log($"- {foo} @ {foo.GetPrefixUnsafe()}");
			Assert.That(foo.FullName, Is.EqualTo("/Outer/Foo"));
			Assert.That(foo.Layer, Is.EqualTo(string.Empty));
			Assert.That(foo.GetPrefixUnsafe().StartsWith(outer.GetPrefixUnsafe()), Is.True, $"Foo prefix {foo.GetPrefixUnsafe()} MUST starts with outer prefix {outer.GetPrefixUnsafe()} because it is contained in that partition");

			var inner = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Outer/Foo/Inner")), this.Cancellation);
			Assert.That(inner, Is.Not.Null);
			Log($"- {inner} @ {inner.GetPrefixUnsafe()}");
			Assert.That(inner.FullName, Is.EqualTo("/Outer/Foo/Inner"));
			Assert.That(inner.Layer, Is.EqualTo("partition"));
			Assert.That(inner.GetPrefixUnsafe().StartsWith(outer.GetPrefixUnsafe()), Is.True, "Inner prefix {inner.GetPrefixUnsafe()} MUST starts with outer prefix {outer.GetPrefixUnsafe()} because it is contained in that partition");
			Assert.That(inner.GetPrefixUnsafe().StartsWith(foo.GetPrefixUnsafe()), Is.False, "Inner prefix {inner.GetPrefixUnsafe()} MUST NOT starts with foo prefix {foo.GetPrefixUnsafe()} because they are both in the same partition");

			var bar = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Outer/Foo/Inner/Bar")), this.Cancellation);
			Assert.That(bar, Is.Not.Null);
			Log($"- {bar} @ {bar.GetPrefixUnsafe()}");
			Assert.That(bar.FullName, Is.EqualTo("/Outer/Foo/Inner/Bar"));
			Assert.That(bar.Layer, Is.EqualTo("BarLayer"));
			Assert.That(bar.GetPrefixUnsafe().StartsWith(inner.GetPrefixUnsafe()), Is.True, $"Bar prefix {bar.GetPrefixUnsafe()} MUST starts with inner prefix {inner.GetPrefixUnsafe()} because it is contained in that partition");

			var baz = await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Outer/Foo/Inner/Bar/Baz")), this.Cancellation);
			Assert.That(baz, Is.Not.Null);
			Log($"- {baz} @ {baz.GetPrefixUnsafe()}");
			Assert.That(baz.FullName, Is.EqualTo("/Outer/Foo/Inner/Bar/Baz"));
			Assert.That(baz.Layer, Is.EqualTo("BazLayer"));
			Assert.That(baz.GetPrefixUnsafe().StartsWith(inner.GetPrefixUnsafe()), Is.True, $"Baz prefix {baz.GetPrefixUnsafe()} MUST starts with inner prefix {inner.GetPrefixUnsafe()} because it is contained in that partition");
			Assert.That(baz.GetPrefixUnsafe().StartsWith(bar.GetPrefixUnsafe()), Is.False, $"Baz prefix {baz.GetPrefixUnsafe()} MUST NOT starts with Bar prefix {bar.GetPrefixUnsafe()} because they are both in the same partition");

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		[Test]
		public async Task Test_List_SubFolders()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

			var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => { list.Add(log); });
#endif

			Log("Creating directory tree...");
			// linear subtree "/Foo/Bar/Baz"
			await db.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, FdbPath.Parse("/Foo/Bar/Baz")), this.Cancellation);
			// flat subtree "/numbers/0" to "/numbers/9"
			await db.WriteAsync(async tr =>
			{
				for (int i = 0; i < 10; i++)
				{
					await directory.CreateOrOpenAsync(tr, FdbPath.Absolute("numbers", i.ToString()));
				}
			}, this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif

			Log("List '/Foo':");
			var subdirs = await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Parse("/Foo")), this.Cancellation);
			Assert.That(subdirs, Is.Not.Null);
			foreach (var subdir in subdirs) Log("- " + subdir);
			Assert.That(subdirs.Count, Is.EqualTo(1));
			Assert.That(subdirs[0], Is.EqualTo(FdbPath.Parse("/Foo/Bar")));

			Log("List '/Foo/Bar':");
			subdirs = await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Parse("/Foo/Bar")), this.Cancellation);
			Assert.That(subdirs, Is.Not.Null);
			foreach (var subdir in subdirs) Log("- " + subdir);
			Assert.That(subdirs.Count, Is.EqualTo(1));
			Assert.That(subdirs[0], Is.EqualTo(FdbPath.Parse("/Foo/Bar/Baz")));

			Log("List '/Foo/Bar/Baz':");
			subdirs = await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Parse("/Foo/Bar/Baz")), this.Cancellation);
			Assert.That(subdirs, Is.Not.Null);
			foreach (var subdir in subdirs) Log("- " + subdir);
			Assert.That(subdirs.Count, Is.Zero);

			Log("List '/numbers':");
			subdirs = await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Parse("/numbers")), this.Cancellation);
			Assert.That(subdirs, Is.Not.Null);
			foreach (var subdir in subdirs) Log("- " + subdir);
			Assert.That(subdirs.Count, Is.EqualTo(10));
			Assert.That(subdirs, Is.EquivalentTo(Enumerable.Range(0, 10).Select(x => FdbPath.Absolute("numbers", x.ToString())).ToList()));

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		[Test]
		public async Task Test_List_Folders_Should_Be_Sorted_By_Name()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

			var dl = FdbDirectoryLayer.Create(location);

			// insert letters in random order
			Log("Inserting sub-folders in random order...");
			await db.WriteAsync(async tr =>
			{
				var rnd = new Random();
				var letters = Enumerable.Range(65, 10).Select(x => new string((char) x, 1)).ToList();
				while (letters.Count > 0)
				{
					var i = rnd.Next(letters.Count);
					var s = letters[i];
					letters.RemoveAt(i);
					await dl.CreateOrOpenAsync(tr, FdbPath.Absolute("letters", s));
				}
			}, this.Cancellation);

#if DEBUG
			await DumpSubspace(db, location);
#endif

			// they should be sorted when listed
			Log("Listing '/letters':");
			var subdirs = await db.ReadAsync(tr => dl.ListAsync(tr, FdbPath.Absolute("letters")), this.Cancellation);
			Assert.That(subdirs, Is.Not.Null);
			foreach (var subdir in subdirs) Log("- " + subdir);
			Assert.That(subdirs.Count, Is.EqualTo(10));
			for (int i = 0; i < subdirs.Count; i++)
			{
				Assert.That(subdirs[i], Is.EqualTo(FdbPath.Absolute("letters", new string((char) (65 + i), 1))));
			}
		}

		[Test]
		public async Task Test_Move_Folder()
		{
			// Create a folder ("foo", "bar", "baz") and ensure that all the parent folder are also creating and linked properly

			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => list.Add(log));
#endif

			// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
			var dl = FdbDirectoryLayer.Create(location);

			// create a folder at ('Foo',)
			Slice originalPrefix = await db.ReadWriteAsync(async tr =>
			{
				var original = await dl.CreateOrOpenAsync(tr, FdbPath.Parse("/Foo"));
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(original, Is.Not.Null);
				Assert.That(original.FullName, Is.EqualTo("/Foo"));
				Assert.That(original.Path, Is.EqualTo(FdbPath.Absolute("Foo")));

				// rename/move it as ('Bar',)
				var renamed = await original.MoveToAsync(tr, FdbPath.Parse("/Bar"));
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(renamed, Is.Not.Null);
				Assert.That(renamed.FullName, Is.EqualTo("/Bar"));
				Assert.That(renamed.Path, Is.EqualTo(FdbPath.Absolute("Bar")));
				Assert.That(renamed.GetPrefix(), Is.EqualTo(original.GetPrefix()));

				return original.GetPrefix();
			}, this.Cancellation);

			// opening the old path should fail
			Assert.That(async () => await db.ReadAsync(tr => dl.OpenAsync(tr, FdbPath.Parse("/Foo")), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

			// opening the new path should succeed
			await db.WriteAsync(async tr =>
			{
				var folder = await dl.OpenAsync(tr, FdbPath.Parse("/Bar"));
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.FullName, Is.EqualTo("/Bar"));
				Assert.That(folder.Path, Is.EqualTo(FdbPath.Absolute("Bar")));
				Assert.That(folder.GetPrefix(), Is.EqualTo(originalPrefix));

				// moving the folder under itself should fail
				Assert.That(async () => await folder.MoveToAsync(tr, FdbPath.Parse("/Foo/Bar")), Throws.InstanceOf<InvalidOperationException>());
			}, this.Cancellation);

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		[Test]
		public async Task Test_Remove_Folder()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

			var dl = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
			var list = new List<FdbTransactionLog>();
			db.SetDefaultLogHandler((log) => list.Add(log));
#endif

			// RemoveAsync

			var path = FdbPath.Parse("/CrashTestDummy");
			await db.ReadWriteAsync(tr => dl.CreateAsync(tr, path), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif

			// removing an existing folder should succeed
			await db.WriteAsync(tr => dl.RemoveAsync(tr, path), this.Cancellation);
#if DEBUG
			await DumpSubspace(db, location);
#endif
			//TODO: call ExistsAsync(...) once it is implemented!

			// Removing it a second time should fail
			Assert.That(
				async () => await db.WriteAsync(tr => dl.RemoveAsync(tr, path), this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>(),
				"Removing a non-existent directory should fail"
			);

			// TryRemoveAsync

			await db.ReadWriteAsync(tr => dl.CreateAsync(tr, path), this.Cancellation);

			// attempting to remove a folder should return true
			bool res = await db.ReadWriteAsync(tr => dl.TryRemoveAsync(tr, path), this.Cancellation);
			Assert.That(res, Is.True);

			// further attempts should return false
			res = await db.ReadWriteAsync(tr => dl.TryRemoveAsync(tr, path), this.Cancellation);
			Assert.That(res, Is.False);

			// Corner Cases

			// removing the root folder is not allowed (too dangerous)
			Assert.That(async () => await db.WriteAsync(tr => dl.RemoveAsync(tr, FdbPath.Empty), this.Cancellation), Throws.InstanceOf<InvalidOperationException>(), "Attempting to remove the root directory should fail");

#if ENABLE_LOGGING
			foreach (var log in list)
			{
				Log(log.GetTimingsReport(true));
			}
#endif
		}

		[Test]
		public async Task Test_Can_Change_Layer_Of_Existing_Directory()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

			var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			await db.WriteAsync(async tr =>
			{
				var folder = await directory.CreateAsync(tr, FdbPath.Root["Test", "foo"]);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Layer, Is.EqualTo("foo"));

				var folder2 = await folder.ChangeLayerAsync(tr, "bar");
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(folder2, Is.Not.Null);
				Assert.That(folder2.Layer, Is.EqualTo("bar"));
				Assert.That(folder2.FullName, Is.EqualTo("/Test"));
				Assert.That(folder2.Path, Is.EqualTo(FdbPath.Absolute(FdbPathSegment.Create("Test", "foo"))));
				Assert.That(folder2.GetPrefix(), Is.EqualTo(folder.GetPrefix()));
			}, this.Cancellation);

			// opening the directory with the new layer should succeed
			await db.ReadAsync(async tr =>
			{
				var folder3 = await directory.OpenAsync(tr, FdbPath.Parse("/Test[bar]"));
				Assert.That(folder3, Is.Not.Null);
				return default(object);
			}, this.Cancellation);

			// opening the directory with the old layer should fail
			Assert.That(
				async () => await db.ReadAsync(tr => directory.OpenAsync(tr, FdbPath.Parse("/Test[foo]")), this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>()
			);
		}

		[Test]
		public async Task Test_Directory_Partitions()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);
			Log(dl);

			await db.WriteAsync(async tr =>
			{
				var segFoo = FdbPathSegment.Partition("Foo$");
				var segBar = FdbPathSegment.Create("Bar");

				Log("Creating partition /Foo$ ...");
				var partition = await dl.CreateAsync(tr, FdbPath.Absolute(segFoo));
				Log(partition);
				await DumpSubspace(tr, location);
				// we can't get the partition key directory (because it's a root directory) so we need to cheat a little bit
				var partitionKey = partition.Copy().GetPrefix();
				Log($"> Created with prefix: {partitionKey:K}");

				Assert.That(partition, Is.InstanceOf<FdbDirectoryPartition>());
				Assert.That(partition.Layer, Is.EqualTo("partition"));
				Assert.That(partition.FullName, Is.EqualTo("/Foo$"));
				Assert.That(partition.Path, Is.EqualTo(FdbPath.Absolute(segFoo)), "Partition's path should be absolute");
				Assert.That(partition.DirectoryLayer, Is.SameAs(dl), "Partitions share the same DL");

				Log("Creating sub-directory Bar under partition Foo$ ...");
				var bar = await partition.CreateAsync(tr, FdbPath.Relative(segBar));
				Log(bar);
				await DumpSubspace(tr, location);
				Assert.That(bar, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(bar.Path, Is.EqualTo(FdbPath.Absolute(segFoo, FdbPathSegment.Create("Bar"))), "Path of directories under a partition should be absolute");
				Assert.That(bar.GetPrefix(), Is.Not.EqualTo(partitionKey), $"{bar} should be located under {partition}");
				Assert.That(bar.GetPrefix().StartsWith(partitionKey), Is.True, $"{bar} should be located under {partition}");

				Log("Creating sub-directory /Foo$/Baz starting from the root...");
				var baz = await dl.CreateAsync(tr, FdbPath.Absolute(segFoo, FdbPathSegment.Create("Baz")));
				Log(baz);
				await DumpSubspace(tr, location);
				Assert.That(baz, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(baz.FullName, Is.EqualTo("/Foo$/Baz"));
				Assert.That(baz.Path, Is.EqualTo(FdbPath.Absolute(segFoo, FdbPathSegment.Create("Baz"))), "Path of directories under a partition should be absolute");
				Assert.That(baz.GetPrefix(), Is.Not.EqualTo(partitionKey), $"{baz} should be located under {partition}");
				Assert.That(baz.GetPrefix().StartsWith(partitionKey), Is.True, $"{baz} should be located under {partition}");

				// Rename 'Bar' to 'BarBar'
				Log("Renaming /Foo$/Bar to /Foo$/BarBar...");
				var bar2 = await bar.MoveToAsync(tr, FdbPath.Absolute(segFoo, FdbPathSegment.Create("BarBar")));
				Log(bar2);
				await DumpSubspace(tr, location);
				Assert.That(bar2, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(bar2, Is.Not.SameAs(bar));
				Assert.That(bar2.GetPrefix(), Is.EqualTo(bar.GetPrefix()));
				Assert.That(bar2.FullName, Is.EqualTo("/Foo$/BarBar"));
				Assert.That(bar2.Path, Is.EqualTo(FdbPath.Absolute(segFoo, FdbPathSegment.Create("BarBar"))));
				Assert.That(bar2.DirectoryLayer, Is.SameAs(bar.DirectoryLayer));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Directory_Cannot_Move_To_Another_Partition()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);
			Log(dl);

			await db.WriteAsync(async tr =>
			{
				Log("Creating /Foo$ ...");
				var foo = await dl.CreateAsync(tr, FdbPath.Absolute("Foo$[partition]"));
				Log(foo);
				await DumpSubspace(tr, location);

				Log("Creating /Foo$/Bar ...");
				// create a 'Bar' under the 'Foo' partition
				var bar = await foo.CreateAsync(tr, FdbPath.Relative("Bar"));
				Log(bar);
				await DumpSubspace(tr, location);

				Assert.That(bar.FullName, Is.EqualTo("/Foo$/Bar"));
				Assert.That(bar.Path, Is.EqualTo(FdbPath.Absolute("Foo$[partition]", "Bar")));
				Assert.That(bar.DirectoryLayer, Is.SameAs(dl));
				Assert.That(bar.DirectoryLayer, Is.SameAs(foo.DirectoryLayer));

				// Attempting to move 'Bar' outside the Foo partition should fail
				Log("Attempting to move /Foo$/Bar to /Bar ...");
				Assert.That(
					async () => await bar.MoveToAsync(tr, FdbPath.Absolute("Bar")),
					Throws.InstanceOf<InvalidOperationException>()
				);

			}, this.Cancellation);

			Log("Attempting to move /Foo$/Bar to /Bar ...");
			Assert.That(
				async () => await db.ReadWriteAsync(tr => dl.MoveAsync(tr, FdbPath.Absolute("Foo$", "Bar"), FdbPath.Absolute("Bar")), this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>()
			);
		}

		[Test]
		public async Task Test_Directory_Cannot_Move_To_A_Sub_Partition()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);
			Log(dl);

			await db.WriteAsync(async tr =>
			{
				var segOuter = FdbPathSegment.Partition("Outer");
				var segInner = FdbPathSegment.Partition("Inner");
				var segFoo = FdbPathSegment.Create("Foo");
				var segBar = FdbPathSegment.Create("Bar");
				var segSubFolder = FdbPathSegment.Create("SubFolder");

				Log("Create [Outer$]");
				var outer = await dl.CreateAsync(tr, FdbPath.Absolute(segOuter));
				Log(outer);
				await DumpSubspace(tr, location);

				// create a 'Inner' subpartition under the 'Outer' partition
				Log("Create [Outer$][Inner$]");
				var inner = await outer.CreateAsync(tr, FdbPath.Relative(segInner));
				Log(inner);
				await DumpSubspace(tr, location);

				Assert.That(inner.FullName, Is.EqualTo("/Outer/Inner"));
				Assert.That(inner.Path, Is.EqualTo(FdbPath.Absolute(segOuter, segInner)));
				Assert.That(inner.DirectoryLayer, Is.SameAs(dl));
				Assert.That(inner.DirectoryLayer, Is.SameAs(outer.DirectoryLayer));

				// create folder /Outer/Foo
				Log("Create [Outer$][Foo]...");
				var foo = await outer.CreateAsync(tr, FdbPath.Relative(segFoo));
				await DumpSubspace(tr, location);
				Assert.That(foo.FullName, Is.EqualTo("/Outer/Foo"));
				Assert.That(foo.Path, Is.EqualTo(FdbPath.Absolute(segOuter, segFoo)));

				// create folder /Outer/Inner/Bar
				Log("Create [Outer$/Inner$][Bar]...");
				var bar = await inner.CreateAsync(tr, FdbPath.Relative(segBar));
				await DumpSubspace(tr, location);
				Assert.That(bar.FullName, Is.EqualTo("/Outer/Inner/Bar"));
				Assert.That(bar.Path, Is.EqualTo(FdbPath.Absolute(segOuter, segInner, segBar)));

				// Attempting to move 'Foo' inside the Inner partition should fail
				Assert.That(async () => await foo.MoveToAsync(tr, FdbPath.Absolute(segOuter, segInner, segFoo)), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await dl.MoveAsync(tr, FdbPath.Absolute(segOuter, segFoo), FdbPath.Absolute(segOuter, segInner, segFoo)), Throws.InstanceOf<InvalidOperationException>());

				// Attempting to move 'Bar' outside the Inner partition should fail
				Assert.That(async () => await bar.MoveToAsync(tr, FdbPath.Absolute(segOuter, FdbPathSegment.Create("Bar"))), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await dl.MoveAsync(tr, FdbPath.Absolute(segOuter, segInner, FdbPathSegment.Create("Bar")), FdbPath.Absolute(segOuter, segBar)), Throws.InstanceOf<InvalidOperationException>());

				// Moving 'Foo' inside the Outer partition itself should work
				Log("Create [Outer$/SubFolder]...");
				await dl.CreateAsync(tr, FdbPath.Absolute(segOuter, segSubFolder)); // parent of destination folder must already exist when moving...
				await DumpSubspace(tr, location);

				Log("Move [Outer$/Foo] to [Outer$/SubFolder/Foo]");
				var foo2 = await dl.MoveAsync(tr, FdbPath.Absolute(segOuter, segFoo), FdbPath.Absolute(segOuter, segSubFolder, segFoo));
				await DumpSubspace(tr, location);
				Assert.That(foo2.Path, Is.EqualTo(FdbPath.Absolute(segOuter, segSubFolder, segFoo)));
				Assert.That(foo2.FullName, Is.EqualTo("/Outer/SubFolder/Foo"));
				Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()));

				// Moving 'Bar' inside the Inner partition itself should work
				Log("Create 'Outer/Inner/SubFolder'...");
				await dl.CreateAsync(tr, FdbPath.Absolute(segOuter, segInner, segSubFolder)); // parent of destination folder must already exist when moving...
				await DumpSubspace(tr, location);

				Log("Move 'Outer/Inner/Bar' to 'Outer/Inner/SubFolder/Bar'");
				var bar2 = await dl.MoveAsync(tr, FdbPath.Absolute(segOuter, segInner, segBar), FdbPath.Absolute(segOuter, segInner, segSubFolder, segBar));
				await DumpSubspace(tr, location);
				Assert.That(bar2.Path, Is.EqualTo(FdbPath.Absolute(segOuter, segInner, segSubFolder, segBar)));
				Assert.That(bar2.FullName, Is.EqualTo("/Outer/Inner/SubFolder/Bar"));
				Assert.That(bar2.GetPrefix(), Is.EqualTo(bar.GetPrefix()));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Renaming_Partition_Uses_Parent_DirectoryLayer()
		{
			// - Create a partition "foo"
			// - Verify that DL.List() returns only "foo"
			// - Rename partition "foo" to "bar"
			// - Verify that DL.List() now returns only "bar"

			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);

			using var tr = db.BeginTransaction(this.Cancellation);

			var segFoo = FdbPathSegment.Partition("Foo$");
			var segBar = FdbPathSegment.Partition("Bar$");

			// create foo
			Log("Creating /Foo$ ...");
			var foo = await dl.CreateOrOpenAsync(tr, FdbPath.Absolute(segFoo));
			Log(foo);
			await DumpSubspace(tr, location);
			Assert.That(foo, Is.Not.Null);
			Assert.That(foo.FullName, Is.EqualTo("/Foo$"));
			Assert.That(foo.Path, Is.EqualTo(FdbPath.Absolute(segFoo)));
			Assert.That(foo.Name, Is.EqualTo("Foo$"));
			Assert.That(foo.Layer, Is.EqualTo("partition"));
			Assert.That(foo, Is.InstanceOf<FdbDirectoryPartition>());

			// verify list
			Log("Checking top...");
			var folders = await dl.ListAsync(tr);
			foreach (var f in folders) Log($"- {f}");
			Assert.That(folders, Is.EqualTo(new[] { FdbPath.Absolute(segFoo) }));

			// rename to bar
			Log("Renaming [Foo$] to [Bar$]");
			var bar = await foo.MoveToAsync(tr, FdbPath.Absolute(segBar));
			await DumpSubspace(tr, location);
			Assert.That(bar, Is.Not.Null);
			Assert.That(bar.FullName, Is.EqualTo("/Bar$"));
			Assert.That(bar.Path, Is.EqualTo(FdbPath.Absolute(segBar)));
			Assert.That(bar.Name, Is.EqualTo("Bar$"));
			Assert.That(bar.Layer, Is.EqualTo("partition"));
			Assert.That(bar, Is.InstanceOf<FdbDirectoryPartition>());

			// should have kept the same prefix
			//note: we need to cheat to get the key of the partition
			Assert.That(bar.Copy().GetPrefix(), Is.EqualTo(foo.Copy().GetPrefix()), "Prefix for partition should not changed after being moved");

			// verify list again
			folders = await dl.ListAsync(tr);
			foreach (var f in folders) Log($"- {f}");
			Assert.That(folders, Is.EqualTo(new [] { FdbPath.Absolute(segBar) }));

			//no need to commit
		}

		[Test]
		public async Task Test_Removing_Partition_Uses_Parent_DirectoryLayer()
		{
			// - Create a partition "foo" in ROOT
			// - Verify that ROOT.List() returns "foo"
			// - Remove "foo" via dir.RemoveAsync()
			// - Verify that ROOT.List() now returns an empty list
			// - Verify that dir.ExistsAsync() returns false

			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);

			using var tr = db.BeginTransaction(this.Cancellation);
			var segFoo = FdbPathSegment.Partition("foo");

			// create foo
			var foo = await dl.CreateOrOpenAsync(tr, FdbPath.Absolute(segFoo));
			Assert.That(foo, Is.Not.Null);
			Assert.That(foo.FullName, Is.EqualTo("/foo"));
			Assert.That(foo.Path, Is.EqualTo(FdbPath.Absolute(segFoo)));
			Assert.That(foo.Layer, Is.EqualTo("partition"));
			Assert.That(foo, Is.InstanceOf<FdbDirectoryPartition>());

			// verify list
			var folders = await dl.ListAsync(tr);
			Assert.That(folders, Is.EqualTo(new[] { FdbPath.Absolute(segFoo) }));

			// delete foo
			await foo.RemoveAsync(tr);

			// verify list again
			folders = await dl.ListAsync(tr);
			Assert.That(folders, Is.Empty);

			// verify that it does not exist anymore
			var res = await foo.ExistsAsync(tr);
			Assert.That(res, Is.False);

			res = await dl.ExistsAsync(tr, FdbPath.Absolute(segFoo));
			Assert.That(res, Is.False);

			//no need to commit
		}

		[Test]
		public async Task Test_Directory_Methods_Should_Fail_With_Empty_Paths()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var directory = FdbDirectoryLayer.Create(location);

			// CreateOrOpen
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, FdbPath.Root), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, FdbPath.Empty), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

			// Create
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.CreateAsync(tr, FdbPath.Root), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.CreateAsync(tr, FdbPath.Empty), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

			// Open
			Assert.That(async () => await db.ReadAsync(tr => directory.OpenAsync(tr, FdbPath.Root), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.ReadAsync(tr => directory.OpenAsync(tr, FdbPath.Empty), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

			// Move
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.MoveAsync(tr, FdbPath.Root, FdbPath.Parse("/foo")), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.MoveAsync(tr, FdbPath.Parse("/foo"), FdbPath.Root), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.MoveAsync(tr, FdbPath.Empty, FdbPath.Parse("/foo")), this.Cancellation), Throws.InstanceOf<ArgumentException>());
			Assert.That(async () => await db.ReadWriteAsync(tr => directory.MoveAsync(tr, FdbPath.Parse("/foo"), FdbPath.Empty), this.Cancellation), Throws.InstanceOf<ArgumentException>());

			// Remove
			Assert.That(async () => await db.WriteAsync(tr => directory.RemoveAsync(tr, FdbPath.Root), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.WriteAsync(tr => directory.RemoveAsync(tr, FdbPath.Empty), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(async () => await db.WriteAsync(tr => directory.RemoveAsync(tr, FdbPath.Absolute("Foo", " ", "Bar")), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

			// List
			Assert.That(async () => await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Root), this.Cancellation), Throws.Nothing);
			Assert.That(async () => await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Empty), this.Cancellation), Throws.Nothing);
			Assert.That(async () => await db.ReadAsync(tr => directory.ListAsync(tr, FdbPath.Absolute("Foo", " ", "Bar")), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Directory_Partitions_Should_Disallow_Creation_Of_Direct_Keys()
		{
			// an instance of FdbDirectoryPartition should throw when attempting to create keys directly under it, and should only be used to create/open subdirectories
			// => this is because keys created directly will most certainly conflict with directory subspaces

			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);
			Log(dl);

			// the constraint will always be the same for all the checks
			void ShouldFail<T>(Func<T> del)
			{
				Assert.That(del, Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("root of directory partition"));
			}

			void ShouldPass<T>(Func<T> del)
			{
				Assert.That(del, Throws.Nothing);
			}

			await db.WriteAsync(async tr =>
			{
				var partition = await dl.CreateAsync(tr, FdbPath.Absolute("Foo[partition]"));
				Log($"Partition: {partition.Descriptor.Prefix:K}");
				//note: if we want a testable key INSIDE the partition, we have to get it from a subdirectory
				var subdir = await partition.CreateOrOpenAsync(tr, FdbPath.Relative("Bar"));
				Log($"SubDir: {subdir.Descriptor.Prefix:K}");
				var barKey = subdir.GetPrefix();

				// === PASS ===
				// these methods are allowed to succeed on directory partitions, because we need them for the rest to work

				ShouldPass(() => partition.Copy().GetPrefix()); // EXCEPTION: we need this to work, because that's the only way that the unit tests above can see the partition key!
				ShouldPass(() => partition.ToString()); // EXCEPTION: this should never fail!
				ShouldPass(() => partition.DumpKey(barKey)); // EXCEPTION: this should always work, because this can be used for debugging and logging...
				ShouldPass(() => partition.BoundCheck(barKey, true)); // EXCEPTION: needs to work because it is used by GetRange() and GetKey()

				// === FAIL ====

				// Key
				ShouldFail(() => partition.GetPrefix());

				// Contains
				ShouldFail(() => partition.Contains(barKey));

				// Extract / ExtractAndCheck / BoundCheck
				ShouldFail(() => partition.ExtractKey(barKey, boundCheck: false));
				ShouldFail(() => partition.ExtractKey(barKey, boundCheck: true));

				// With Prefix

				ShouldFail(() => partition.Key(123).ToSubspace());
				ShouldFail(() => partition.Key(123, "hello").ToSubspace());
				ShouldFail(() => partition.Key(123, "hello", false).ToSubspace());
				ShouldFail(() => partition.Key(123, "hello", false, "world").ToSubspace());

				// Keys

				ShouldFail(() => partition.Bytes("hello"u8).ToSlice());
				var subspace = await location.Resolve(tr, dl);

				ShouldFail(() => partition.Bytes(subspace.GetPrefix()).ToSlice());

				ShouldFail(() => partition.ToRange().ToKeyRange());
				ShouldFail(() => partition.Key("hello").ToRange().ToKeyRange());
				ShouldFail(() => FdbKeyRange.Between(partition.Key("hello", 123), partition.Key("hello", 456)).ToKeyRange());

				// Tuples

				ShouldFail(() => partition.Unpack(barKey));
				ShouldFail(() => partition.Decode<int>(barKey));
				ShouldFail(() => partition.DecodeLast<int>(barKey));
				ShouldFail(() => partition.DecodeFirst<int>(barKey));

				// GetKey

				// GetKey() itself should not fail, but attempting to render the bytes should!
				ShouldPass(() => partition.Key(123));
				ShouldPass(() => partition.Key(123, "hello"));
				ShouldPass(() => partition.Key(123, "hello", false));
				ShouldPass(() => partition.Key(123, "hello", false, "world"));
				ShouldPass(() => partition.Key<object>(123));

				ShouldFail(() => partition.Key(123).ToSlice());
				ShouldFail(() => partition.Key(123, "hello").ToSlice());
				ShouldFail(() => partition.Key(123, "hello", false).ToSlice());
				ShouldFail(() => partition.Key(123, "hello", false, "world").ToSlice());
				ShouldFail(() => partition.Key<object>(123).ToSlice());

			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Concurrent_Directory_Creation()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);
			Log(dl);

			//to prevent any side effect from first time initialization of the directory layer, already create one dummy folder
			await db.ReadWriteAsync(tr => dl.CreateAsync(tr, FdbPath.Root["Zero"]), this.Cancellation);

			var f = FdbDirectoryLayer.AnnotateTransactions;
			try
			{
				FdbDirectoryLayer.AnnotateTransactions = true;

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{

					await Task.WhenAll(
						tr1.GetReadVersionAsync(),
						tr2.GetReadVersionAsync()
					);

					// T1 creates first directory
					var first = await dl.CreateAsync(tr1, FdbPath.Absolute("First"));
					tr1.Set(first.GetPrefix(), Text("This belongs to the first directory"));

					// T2 creates second directory
					var second = await dl.CreateAsync(tr2, FdbPath.Absolute("Second"));
					tr2.Set(second.GetPrefix(), Text("This belongs to the second directory"));

					// T1 commits first
					Log("Committing T1...");
					await tr1.CommitAsync();
					Log("T1 committed");

					// ReSharper disable once DisposeOnUsingVariable
					tr1.Dispose(); // force T1 to be dumped immediately

					// T2 commits second
					Log("Committing T2...");
					await tr2.CommitAsync();
					Log("T2 committed");
				}
			}
			finally
			{
				FdbDirectoryLayer.AnnotateTransactions = f;
			}
		}

		[Test]
		public async Task Test_Concurrent_Directory_Creation_With_Custom_Prefix()
		{
			using var db = await OpenTestDatabaseAsync();
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			// to keep the test db in a good shape, we will still use tuples for our custom prefix,
			// but using strings so that they do not collide with the integers used by the normal allocator
			// ie: regular prefix would be ("DL", 123) and our custom prefixes will be ("DL", "abc")

			var dl = FdbDirectoryLayer.Create(location);
			Log(dl);

			//to prevent any side effect from first time initialization of the directory layer, already create one dummy folder
			await db.ReadWriteAsync(tr => dl.CreateAsync(tr, FdbPath.Absolute("Zero")), this.Cancellation);

			var f = FdbDirectoryLayer.AnnotateTransactions;
			try
			{
				FdbDirectoryLayer.AnnotateTransactions = true;

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{

					await Task.WhenAll(
						tr1.GetReadVersionAsync(),
						tr2.GetReadVersionAsync()
					);

					var subspace1 = await location.Resolve(tr1, dl);
					Assert.That(subspace1, Is.Not.Null);

					var subspace2 = await location.Resolve(tr2, dl);
					Assert.That(subspace2, Is.Not.Null);

					var first = await dl.RegisterAsync(tr1, FdbPath.Absolute("First"), subspace1.Key("abc").ToSlice());
					tr1.Set(first.GetPrefix(), Text("This belongs to the first directory"));

					var second = await dl.RegisterAsync(tr2, FdbPath.Absolute("Second"), subspace2.Key("def").ToSlice());
					tr2.Set(second.GetPrefix(), Text("This belongs to the second directory"));

					Log("Committing T1...");
					await tr1.CommitAsync();
					Log("T1 committed");

					// ReSharper disable once DisposeOnUsingVariable
					tr1.Dispose(); // force T1 to be dumped immediately

					Log("Committing T2...");
					try
					{
						await tr2.CommitAsync();
					}
					catch (FdbException x)
					{
						if (x.Code == FdbError.NotCommitted)
						{
							// Current implementation of the DirectoryLayer conflicts with other transactions when checking if the custom key prefix has already been used in the DL
							// cf: http://community.foundationdb.com/questions/4493/im-seeing-conflicts-when-creating-directory-subspa.html#answer-4497
							Log("INCONCLUSIVE!");
							Assert.Inconclusive("FIXME: Current implementation of DirectoryLayer creates read conflict when registering directories with custom key prefix");
						}

						throw;
					}

					Log("T2 committed");
				}
			}
			finally
			{
				FdbDirectoryLayer.AnnotateTransactions = f;
			}
		}

		[Test]
		public async Task Test_TryOpenCached()
		{
			// Verify that we can open a previously cached subspace without paying the cost of resolving the nodes each time,
			// while still observing changes from other transactions

			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
			var dl = FdbDirectoryLayer.Create(location);

			Assert.That(dl.Content, Is.Not.Null);
			Assert.That(dl.Content, Is.EqualTo(location));

			void Validate([NotNull] FdbDirectorySubspace? actual, FdbDirectorySubspace expected)
			{
				Assert.That(actual, Is.Not.Null);
				Assert.That(actual!.FullName, Is.EqualTo(expected.FullName));
				Assert.That(actual.Path, Is.EqualTo(expected.Path));
				Assert.That(actual.Layer, Is.EqualTo(expected.Layer));
				Assert.That(actual.DirectoryLayer, Is.SameAs(expected.DirectoryLayer));
				Assert.That(actual.GetPrefix(), Is.EqualTo(expected.GetPrefix()));
			}

			var pathFoo = FdbPath.Absolute(FdbPathSegment.Create("Foo"));
			var pathBar = pathFoo[FdbPathSegment.Create("Bar")];

			// first, initialize the subspace
			Log($"Creating '{pathFoo}' ...");
			var foo = await db.ReadWriteAsync(tr => dl.CreateAsync(tr, pathFoo), this.Cancellation);
			Assert.That(foo, Is.Not.Null);
			Assert.That(foo.FullName, Is.EqualTo("/Foo"));
			Assert.That(foo.Path, Is.EqualTo(pathFoo));
			Assert.That(foo.Layer, Is.EqualTo(string.Empty));
			Assert.That(foo.DirectoryLayer, Is.SameAs(dl));
			Assert.That(foo.Context, Is.InstanceOf<SubspaceContext>());
#if DEBUG
			Log($"After creating '{pathFoo}':");
			await DumpSubspace(db, location);
#endif
			// first transaction wants to open the folder, which is not in cache.
			// => we expect the subspace to be a new instance
			Log($"OpenCached({pathFoo}) #1...");
			var foo1 = await db.ReadAsync(async tr =>
			{
				var folder = await dl.TryOpenCachedAsync(tr, pathFoo);
				Validate(folder, foo);
				return folder.Descriptor;
			}, this.Cancellation);

			// This instance should not be the same as the one returned by CreateAsync(..) because the layer does not know then if the transaction will commit or not
			// => we only cache when _reading_ from the db.
			Assert.That(foo1, Is.Not.SameAs(foo), "Subspace should NOT be cached when created, only when first accessed in another transaction!");

			// second transaction wants to open the same folder, which should be in cache
			// => we expect the subspace to be the SAME instance
			Log($"OpenCached({pathFoo}) #2...");
			var foo2 = await db.ReadAsync(async tr =>
			{
				var folder = await dl.TryOpenCachedAsync(tr, pathFoo);
				Validate(folder, foo);
				return folder.Descriptor;
			}, this.Cancellation);
			// We expect to get the same instance as the previous call, since no change was made to the directory layer
			Assert.That(foo2, Is.SameAs(foo1), "Subspace descriptor should be the same instance as previous transaction!");

			// creating *another* subspace should not change the cache
			Log($"Creating '{pathBar}'...");
			_ = await db.ReadWriteAsync(tr => dl.CreateAsync(tr, pathBar), this.Cancellation);
#if DEBUG
			Log($"After creating '{pathBar}':");
			await DumpSubspace(db, location);
#endif

			// This should NOT bust the DL cache because we did not change Foo
			Log($"OpenCached({pathFoo}) #3...");
			var foo3 = await db.ReadAsync(async tr =>
			{
				var folder = await dl.TryOpenCachedAsync(tr, pathFoo);
				Validate(folder, foo);
				return folder.Descriptor;
			}, this.Cancellation);

			// We expect the instance to change since the cache SHOULD have been reset after the change to the metadataVersion
			Assert.That(foo3, Is.SameAs(foo1), "Subspace should be the same instance as previous transaction after creating _another_ directory.");

			// If we *delete* Foo, we should observe that it changed
			await db.WriteAsync(tr => dl.RemoveAsync(tr, pathFoo), this.Cancellation);

			// note: we expect the transaction to retry at least once!
			Log($"OpenCached({pathFoo}) #4...");
			var foo4 = await db.ReadAsync(async tr =>
			{
				var folder = await dl.TryOpenCachedAsync(tr, pathFoo);

				// on first attempt, we expect "folder" to NOT be null. On second attempt we expect it to be null.
				Log($"- Attempt #{tr.Context.Retries}: {pathFoo} => {folder?.GetPrefixUnsafe().ToString("P") ?? "<not_found>"}");
				
				return folder?.Descriptor;
			}, this.Cancellation);
			Assert.That(foo4, Is.Null, "Should not have reused previously cached instance of deleted folder!");
		}

		[Test]
		public async Task Test_TryOpenCached_Sequential()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);

			// create multiple batches of folders, and attempt to read them all back sequentially
			// - without cache, we expect the latency to be latency for GRV plus N times the latency per folder
			// - with a cache, we expect the latency to be identical the first time, and then only the GRV latency until the next mutation

			const int K = 5; // number of batches
			const int N = 5; // number of folders per batch
			const int R = 10; // number of executions per batch

			// create a bunch of subspaces and read them back multiple times

			for (int k = 1; k <= K; k++)
			{
				Log($"Creating Batch #{k}/{K}...");
				await db.WriteAsync(async tr =>
				{
					for (int i = 0; i < N; i++) await dl.CreateAsync(tr, FdbPath.Absolute("Students", k.ToString(), i.ToString()));
				}, this.Cancellation);

				Log("> Reading...");
				for (int r = 0; r < R; r++)
				{
					var sw = Stopwatch.StartNew();
					var folders = await db.ReadAsync(async tr =>
					{
						// reading sequentially should be slow without caching for the first request, but then should be very fast
						var res = new List<FdbDirectorySubspace?>();
						for (int i = 0; i < N; i++)
						{
							res.Add(await dl.TryOpenCachedAsync(tr, FdbPath.Absolute("Students", k.ToString(), i.ToString())));
						}

						return res;
					}, this.Cancellation);
					var dur = sw.Elapsed;

					Log($"  - {dur.TotalMilliseconds:N3} ms");

					Assert.That(folders, Is.Not.Null.And.Count.EqualTo(N));
				}
			}
		}

		[Test]
		public async Task Test_TryOpenCached_Poisoned_After_Mutation()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var dl = FdbDirectoryLayer.Create(location);

			await db.ReadWriteAsync(tr => dl.CreateAsync(tr, FdbPath.Absolute("Foo")), this.Cancellation);

			var fooCached = await db.ReadAsync(async tr =>
			{
				var folder = await dl.TryOpenCachedAsync(tr, FdbPath.Absolute("Foo"));
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder!.Context, Is.InstanceOf<FdbDirectoryLayer.State>(), $"Unexpected context for cached directory {folder.Path}");
				return folder;
			}, this.Cancellation);

			// the fooCached instance should be dead outside the transaction!
			Assert.That(() => fooCached.GetPrefix(), Throws.InstanceOf<InvalidOperationException>(), "Accessing a cached subspace outside the transaction should throw");

			var fooUncached = await db.ReadAsync(tr => dl.TryOpenAsync(tr, FdbPath.Absolute("Foo")), this.Cancellation);
			Assert.That(fooUncached, Is.Not.Null);
			Assert.That(() => fooUncached!.Context.EnsureIsValid(), Throws.Nothing, "Accessing a non-cached subspace outside the transaction should not throw");
		}

		[Test]
		public async Task Test_SubspacePath_Resolve()
		{
			using var db = await OpenTestDatabaseAsync();
			// we will put everything under a custom namespace
			var location = db.Root.WithPrefix(TuPack.EncodeKey("DL"));
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var directoryLayer = FdbDirectoryLayer.Create(location);

			var dir = new FdbDirectorySubspaceLocation(FdbPath.Root["Hello"]["World"]);

			// create the corresponding location
			Log("Creating " + dir);
			var prefix = await db.ReadWriteAsync(async tr => (await directoryLayer.CreateAsync(tr, dir.Path)).GetPrefix(), this.Cancellation);
			Log("> Created under " + prefix);

			await DumpSubspace(db, location);

			// resolve the location (optional)
			await db.ReadAsync(async tr =>
			{
				Log("Resolving " + dir);
				var subspace = await dir.TryResolve(tr, directoryLayer);
				Assert.That(subspace, Is.Not.Null);
				Assert.That(subspace!.Path, Is.EqualTo(dir.Path), ".Path");
				Log("> Found under " + subspace.GetPrefix());
				Assert.That(subspace.GetPrefix(), Is.EqualTo(prefix), ".Prefix");
			}, this.Cancellation);

			// resolving again should return a cached version
			await db.ReadAsync(async tr =>
			{
				var subspace = await dir.TryResolve(tr, directoryLayer);
				Assert.That(subspace, Is.Not.Null);
				Assert.That(subspace!.GetPrefix(), Is.EqualTo(prefix), ".Prefix");
			}, this.Cancellation);

			// resolve the location (required)
			await db.ReadAsync(async tr =>
			{
				Log("Resolving " + dir);
				var subspace = await dir.Resolve(tr, directoryLayer);
				Assert.That(subspace, Is.Not.Null);
				Assert.That(subspace.Path, Is.EqualTo(dir.Path), ".Path");
				Log("> Found under " + subspace.GetPrefix());
				Assert.That(subspace.GetPrefix(), Is.EqualTo(prefix), ".Prefix");
			}, this.Cancellation);

		}

	}

}
