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

namespace FoundationDB.Layers.Interning.Tests
{

	[TestFixture]
	[Obsolete]
	public class StringInternFacts : FdbTest
	{

		[Test]
		public async Task Test_StringIntern_Example()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

				var stringSpace = db.Root.WithPrefix(TuPack.EncodeKey("Strings"));
				var dataSpace = db.Root.WithPrefix(TuPack.EncodeKey("Data"));

				var stringTable = new FdbStringIntern(stringSpace);

				// insert a bunch of strings
				await stringTable.WriteAsync(db, async (tr, table) =>
				{
					Assert.That(table, Is.Not.Null);

					var va = await table.InternAsync(tr, "testing 123456789");
					var vb = await table.InternAsync(tr, "dog");
					var vc = await table.InternAsync(tr, "testing 123456789");
					var vd = await table.InternAsync(tr, "cat");
					var ve = await table.InternAsync(tr, "cat");

					var subspace = await dataSpace.Resolve(tr);
					tr.Set(subspace.Key("a"), va);
					tr.Set(subspace.Key("b"), vb);
					tr.Set(subspace.Key("c"), vc);
					tr.Set(subspace.Key("d"), vd);
					tr.Set(subspace.Key("e"), ve);
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, stringSpace);
				await DumpSubspace(db, dataSpace);
#endif

				// check the contents of the data
				await stringTable.ReadAsync(db, async (tr, table) =>
				{
					var subspace = await dataSpace.Resolve(tr);
					var uidA = await tr.GetAsync(subspace.Key("a"));
					var uidB = await tr.GetAsync(subspace.Key("b"));
					var uidC = await tr.GetAsync(subspace.Key("c"));
					var uidD = await tr.GetAsync(subspace.Key("d"));
					var uidE = await tr.GetAsync(subspace.Key("e"));

					// a, b, d should be different
					Assert.That(uidB, Is.Not.EqualTo(uidA));
					Assert.That(uidD, Is.Not.EqualTo(uidA));

					// a should equal c
					Assert.That(uidC, Is.EqualTo(uidA));

					// d should equal e
					Assert.That(uidE, Is.EqualTo(uidD));

					// perform a lookup
					var strA = await table.LookupAsync(tr, uidA);
					var strB = await table.LookupAsync(tr, uidB);
					var strC = await table.LookupAsync(tr, uidC);
					var strD = await table.LookupAsync(tr, uidD);
					var strE = await table.LookupAsync(tr, uidE);

					Assert.That(strA, Is.EqualTo("testing 123456789"));
					Assert.That(strB, Is.EqualTo("dog"));
					Assert.That(strC, Is.EqualTo(strA));
					Assert.That(strD, Is.EqualTo("cat"));
					Assert.That(strE, Is.EqualTo(strD));
				}, this.Cancellation);

				stringTable.Dispose();
			}
		}

	}
}
