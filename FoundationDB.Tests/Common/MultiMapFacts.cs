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

//#define ENABLE_LOGGING
//#define FULL_DEBUG

namespace FoundationDB.Layers.Collections.Tests
{

	[TestFixture]
	public class MultiMapFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbMultiMap_Read_Write_Delete()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var mapFoos = new FdbMultiMap<string, string>(db.Root, allowNegativeValues: false);

				// read non existing value
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					bool res = await foos.ContainsAsync(tr, "hello", "world");
					Assert.That(res, Is.False, "ContainsAsync('hello','world')");

					long? count = await foos.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.Null, "GetCountAsync('hello', 'world')");
				}, this.Cancellation);

				// add some values
				await mapFoos.WriteAsync(db, (tr, foos) =>
				{
					foos.Add(tr, "hello", "world");
					foos.Add(tr, "foo", "bar");
					foos.Add(tr, "foo", "baz");
				}, this.Cancellation);

#if FULL_DEBUG
				await DumpSubspace(db, location);
#endif

				// read values back
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					long? count = await foos.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.EqualTo(1), "hello:world");

					Assert.That(await foos.ContainsAsync(tr, "hello", "world"), Is.True);
					Assert.That(await foos.ContainsAsync(tr, "hello", "WORLD"), Is.False);

					count = await foos.GetCountAsync(tr, "foo", "bar");
					Assert.That(count, Is.EqualTo(1), "foo:bar");

					count = await foos.GetCountAsync(tr, "foo", "baz");
					Assert.That(count, Is.EqualTo(1), "foo:baz");

					Assert.That(await foos.ContainsAsync(tr, "foo", "bar"), Is.True);
					Assert.That(await foos.ContainsAsync(tr, "foo", "baz"), Is.True);
					Assert.That(await foos.ContainsAsync(tr, "foo", "jazz"), Is.False);
					Assert.That(await foos.ContainsAnyAsync(tr, "foo", ["one", "two", "three"]), Is.False);
					Assert.That(await foos.ContainsAnyAsync(tr, "foo", ["one", "bar", "three"]), Is.True);

					Assert.That(await foos.ContainsAsync(tr, "world", "hello"), Is.False);
					Assert.That(await foos.ContainsAnyAsync(tr, "world", ["bar", "baz"]), Is.False);
				}, this.Cancellation);

				// directly read the value, behind the table's back
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					var loc = foos.Subspace;
					var value = await tr.GetAsync(loc.Key("hello", "world"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToInt64(), Is.EqualTo(1));
				}, this.Cancellation);

				// delete the value
				await mapFoos.WriteAsync(db, (tr, foos) => foos.Remove(tr, "hello", "world"), this.Cancellation);
#if FULL_DEBUG
				await DumpSubspace(db, location);
#endif

				// verify that it is gone
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					long? count = await foos.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.Null);

					// also check directly
					var loc = foos.Subspace;
					var data = await tr.GetAsync(loc.Key("hello", "world"));
					Assert.That(data, Is.EqualTo(Slice.Nil));
				}, this.Cancellation);

			}

		}

	}

}
