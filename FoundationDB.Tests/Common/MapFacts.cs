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

// ReSharper disable ReplaceAsyncWithTaskReturn

//#define ENABLE_LOGGING

//#define FULL_DEBUG

namespace FoundationDB.Layers.Collections.Tests
{
	using System.Net;

	[TestFixture]
	public class MapFacts : FdbTest
	{

		[Test, Order(1)]
		public async Task Test_FdbMap_Read_Write_Delete()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var mapFoos = new FdbMap<string, string, FdbUtf8Value>(db.Root, FdbUtf8ValueCodec.Instance);

				string secret = $"world:{Guid.NewGuid()}";

				// read non existing value
				await mapFoos.WriteAsync(db, async (tr, foos) =>
				{
					Assert.That(async () => await foos.GetAsync(tr, "hello"), Throws.InstanceOf<KeyNotFoundException>());

					var value = await foos.TryGetAsync(tr, "hello");
					Assert.That(value.HasValue, Is.False);
					Assert.That(value.Value, Is.Null);
				}, this.Cancellation);

				// write value
				await mapFoos.WriteAsync(db, (tr, foos) => foos.Set(tr, "hello", secret), this.Cancellation);
#if FULL_DEBUG
				await DumpSubspace(db);
#endif

				// read value back
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					var value = await foos.GetAsync(tr, "hello");
					Assert.That(value, Is.EqualTo(secret));

					var opt = await foos.TryGetAsync(tr, "hello");
					Assert.That(opt.HasValue, Is.True);
					Assert.That(opt.Value, Is.EqualTo(secret));
				}, this.Cancellation);

				// directly read the value, behind the table's back
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					var value = await tr.GetAsync(foos.Subspace.Key("hello"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo(secret));
				}, this.Cancellation);

				// delete the value
				await mapFoos.WriteAsync(db, (tr, foos) => foos.Remove(tr, "hello"), this.Cancellation);
#if FULL_DEBUG
				await DumpSubspace(db);
#endif

				// verifiy that it is gone
				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					Assert.That(async () => await foos.GetAsync(tr, "hello"), Throws.InstanceOf<KeyNotFoundException>());

					var value = await foos.TryGetAsync(tr, "hello");
					Assert.That(value.HasValue, Is.False);

					// also check directly
					var folder = await db.Root.Resolve(tr);
					var data = await tr.GetAsync(folder.Key("hello"));
					Assert.That(data, Is.EqualTo(Slice.Nil));
				}, this.Cancellation);

			}

		}

		[Test, Order(2)]
		public async Task Test_FdbMap_List()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var mapFoos = new FdbMap<string, string, FdbUtf8Value>(db.Root, FdbUtf8ValueCodec.Instance);

				// write a bunch of keys
				await mapFoos.WriteAsync(db, (tr, foos) =>
				{
					foos.Set(tr, "foo", "foo_value");
					foos.Set(tr, "bar", "bar_value");
				}, this.Cancellation);
#if FULL_DEBUG
				await DumpSubspace(db);
#endif

				// read them back

				await mapFoos.ReadAsync(db, async (tr, foos) =>
				{
					var value = await foos.GetAsync(tr, "foo");
					Assert.That(value, Is.EqualTo("foo_value"));

					value = await foos.GetAsync(tr, "bar");
					Assert.That(value, Is.EqualTo("bar_value"));

					Assert.That(async () => await foos.GetAsync(tr, "baz"), Throws.InstanceOf<KeyNotFoundException>());

					var opt = await foos.TryGetAsync(tr, "baz");
					Assert.That(opt.HasValue, Is.False);
				}, this.Cancellation);

			}
		}

		[Test]
		public async Task Test_FdbMap_With_Custom_Key_Encoder()
		{
			// Use a table as a backing store for the rules of a Poor Man's firewall, where each key are the IPEndPoint (tcp only!), and the values are "pass" or "block"

			// Encode IPEndPoint as the (IP, Port) encoded with the Tuple codec
			// note: there is a much simpler way or creating composite keys, this is just a quick and dirty test!
			var keyCodec = FdbKeyCodec.Create<IPEndPoint, STuple<IPAddress, int>>(
				key => STuple.Create(key.Address, key.Port),
				encoded => new(encoded.Item1, encoded.Item2)
			);

			var rules = new Dictionary<IPEndPoint, string>()
			{
				{ new(IPAddress.Parse("172.16.12.34"), 6667), "block" },
				{ new(IPAddress.Parse("192.168.34.56"), 80), "pass" },
				{ new(IPAddress.Parse("192.168.34.56"), 443), "pass" }
			};

			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var mapHosts = new FdbMap<IPEndPoint, STuple<IPAddress, int>, string, FdbUtf8Value>(
					db.Root,
					keyCodec,
					FdbValueCodec.Utf8
				);

				// import all the rules
				await mapHosts.WriteAsync(db, (tr, hosts) =>
				{
					foreach(var rule in rules)
					{
						hosts.Set(tr, rule.Key, rule.Value);
					}
				}, this.Cancellation);
#if FULL_DEBUG
				await DumpSubspace(db);
#endif

				// test the rules

				await mapHosts.ReadAsync(db, async (tr, hosts) =>
				{
					var value = await hosts.GetAsync(tr, new IPEndPoint(IPAddress.Parse("172.16.12.34"), 6667));
					Assert.That(value, Is.EqualTo("block"));

					value = await hosts.GetAsync(tr, new IPEndPoint(IPAddress.Parse("192.168.34.56"), 443));
					Assert.That(value, Is.EqualTo("pass"));

					var baz = new IPEndPoint(IPAddress.Parse("172.16.12.34"), 80);
					Assert.That(async () => await hosts.GetAsync(tr, baz), Throws.InstanceOf<KeyNotFoundException>());

					var opt = await hosts.TryGetAsync(tr, baz);
					Assert.That(opt.HasValue, Is.False);
				}, this.Cancellation);

			}
		}

	}

}
