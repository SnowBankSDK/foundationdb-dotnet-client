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

// ReSharper disable AccessToModifiedClosure
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable AccessToDisposedClosure
// ReSharper disable MethodSupportsCancellation
// ReSharper disable MethodHasAsyncOverload

namespace SnowBank.Linq.Async.Tests
{
	using System.Collections.Immutable;
	using System.Linq;
	using SnowBank.Data.Json;
	using SnowBank.Linq;
	using SnowBank.Linq.Async.Iterators;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class AsyncEnumerableFacts : SimpleTest
	{

		[Test]
		public async Task Test_Can_Convert_Enumerable_To_AsyncEnumerable()
		{
			// we need to make sure this works, because we will use this a lot for other tests

			var source = AsyncQuery.Range(0, 10, this.Cancellation);
			Assert.That(source, Is.Not.Null);

			var results = new List<int>();
			await using (var iterator = source.GetAsyncEnumerator())
			{
				while (await iterator.MoveNextAsync())
				{
					Assert.That(results.Count, Is.LessThan(10));
					results.Add(iterator.Current);
				}
			}
			Assert.That(results.Count, Is.EqualTo(10));
			Assert.That(results, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}

		[Test]
		public async Task Test_Can_Convert_Enumerable_To_AsyncEnumerable_With_Async_Transform()
		{
			// we need to make sure this works, because we will use this a lot for other tests

			var source = AsyncQuery.Range(0, 10, this.Cancellation).Select(async (x, ct) =>
			{
				await Task.Delay(10, ct);
				return x + 1;
			});
			Assert.That(source, Is.Not.Null);

			var results = new List<int>();
			await using (var iterator = source.GetAsyncEnumerator())
			{
				while (await iterator.MoveNextAsync())
				{
					Assert.That(results.Count, Is.LessThan(10));
					results.Add(iterator.Current);
				}
			}
			Assert.That(results.Count, Is.EqualTo(10));
			Assert.That(results, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_ToListAsync()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(await source.ToListAsync(), Is.Empty);
			}
			{
				var source = AsyncQuery.Range(0, 10, this.Cancellation);
				Assert.That(await source.ToListAsync(), Is.EqualTo(new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);
				Assert.That(await source.ToListAsync(), Is.EqualTo(new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var source = FakeAsyncQuery([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);
				Assert.That(await source.ToListAsync(), Is.EqualTo(new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var source = AsyncQuery.Range(0, 10000, this.Cancellation);
				Assert.That(await source.ToListAsync(), Is.EqualTo(Enumerable.Range(0, 10000).ToList()));
			}
		}

		[Test]
		public async Task Test_Can_ToArrayAsync()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(await source.ToArrayAsync(), Is.Empty);
			}
			{
				var source = AsyncQuery.Range(0, 10, this.Cancellation);
				Assert.That(await source.ToArrayAsync(), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);
				Assert.That(await source.ToArrayAsync(), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var source = FakeAsyncQuery([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);
				Assert.That(await source.ToArrayAsync(), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var source = AsyncQuery.Range(0, 10000, this.Cancellation);
				Assert.That(await source.ToArrayAsync(), Is.EqualTo(Enumerable.Range(0, 10000).ToArray()));
			}
		}

		[Test]
		public async Task Test_Empty()
		{
#if NET10_0_OR_GREATER
			//note: as of .NET 10 preview 2, "Current" does not throw on AsyncEnumerable.Empty, but it DOES throw for Enumerable.Empty
			// => we will align with this implementation, unless we have a good reason to!
			{
				var x = AsyncEnumerable.Empty<int>().GetAsyncEnumerator();
				Assert.That(await x.MoveNextAsync(), Is.False);
				Assert.That(() => x.Current, Throws.Nothing, "AsyncEnumerable.Empty currently does not throw calling Current");
			}
#endif

			Assert.That(AsyncQuery.Empty<int>(), Is.Not.Null);

			await using (var it = AsyncQuery.Empty<int>().GetAsyncEnumerator())
			{
				//Note: AsyncEnumerable in .NET 10 returns default instead of throwing
				Assert.That(it.Current, Is.Zero); 

				// MoveNext should return an already completed 'false' result
				var next = it.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);

				//Note: AsyncEnumerable in .NET 10 returns default instead of throwing
				Assert.That(it.Current, Is.Zero);
			}

			Assert.That(await AsyncQuery.Empty<int>().ToArrayAsync(), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().ToListAsync(), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().ToImmutableArrayAsync(), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().ToDictionaryAsync(x => x), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().ToDictionaryAsync(x => x, x => x), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().AnyAsync(), Is.False);
			Assert.That(await AsyncQuery.Empty<int>().AllAsync(x => x == 42), Is.True);
			Assert.That(await AsyncQuery.Empty<int>().CountAsync(), Is.Zero);
			Assert.That(await AsyncQuery.Empty<int>().SumAsync(), Is.Zero);
			Assert.That(await AsyncQuery.Empty<int>().SingleOrDefaultAsync(), Is.Zero);
			Assert.That(await AsyncQuery.Empty<int>().SingleOrDefaultAsync(-1), Is.EqualTo(-1));
			Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync(), Is.Zero);
			Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync(-1), Is.EqualTo(-1));
			Assert.That(await AsyncQuery.Empty<int>().LastOrDefaultAsync(), Is.Zero);
			Assert.That(await AsyncQuery.Empty<int>().LastOrDefaultAsync(-1), Is.EqualTo(-1));
			Assert.That(async () => await AsyncQuery.Empty<int>().SingleAsync(), Throws.InvalidOperationException);
			Assert.That(async () => await AsyncQuery.Empty<int>().FirstAsync(), Throws.InvalidOperationException);
			Assert.That(async () => await AsyncQuery.Empty<int>().LastAsync(), Throws.InvalidOperationException);
			Assert.That(async () => await AsyncQuery.Empty<int>().MinAsync(), Throws.InvalidOperationException);
			Assert.That(async () => await AsyncQuery.Empty<int>().MaxAsync(), Throws.InvalidOperationException);
			Assert.That(await AsyncQuery.Empty<string>().MinAsync(), Is.Null);
			Assert.That(await AsyncQuery.Empty<string>().MaxAsync(), Is.Null);

			Assert.That(await AsyncQuery.Empty<int>().Where(x => throw new InvalidOperationException()).ToListAsync(), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().Select<int, string>(x => throw new InvalidOperationException()).ToListAsync(), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().Skip(123).ToListAsync(), Is.Empty);
			Assert.That(await AsyncQuery.Empty<int>().Take(123).ToListAsync(), Is.Empty);
		}

		[Test]
		public async Task Test_Singleton()
		{
			var singleton = AsyncQuery.Singleton(42);
			Assert.That(singleton, Is.Not.Null);

			await using (var iterator = singleton.GetAsyncEnumerator())
			{
				// calling Current before MoveNext should throw
				Assert.That(() => iterator.Current, Throws.InvalidOperationException);
				
				// first call to MoveNext should return an already completed 'true' result
				var next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.True);
				Assert.That(iterator.Current, Is.EqualTo(42));
				// second call to MoveNext should return an already completed 'false' result
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var results = await singleton.ToListAsync();
			Assert.That(results, Is.EqualTo(new [] { 42 }));

			Assert.That(await singleton.AnyAsync(), Is.True);
			Assert.That(await singleton.AnyAsync(x => x == 42), Is.True);
			Assert.That(await singleton.AnyAsync(x => x != 42), Is.False);

			Assert.That(await singleton.AllAsync(x => x == 42), Is.True);
			Assert.That(await singleton.AllAsync(x => x != 42), Is.False);

			Assert.That(await singleton.CountAsync(), Is.EqualTo(1));
			Assert.That(await singleton.CountAsync(x => x == 42), Is.EqualTo(1));
			Assert.That(await singleton.CountAsync(x => x != 42), Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Range()
		{
			{
				Assert.That(await AsyncQuery.Range(0, 0).CountAsync(), Is.Zero);
				Assert.That(await AsyncQuery.Range(0, 0).AnyAsync(), Is.False);
				Assert.That(await AsyncQuery.Range(0, 0).ToListAsync(), Is.Empty);
			}
			{
				Assert.That(await AsyncQuery.Range(42, 3).CountAsync(), Is.EqualTo(3));
				Assert.That(await AsyncQuery.Range(42, 3).CountAsync(x => x % 2 == 1), Is.EqualTo(1));
				Assert.That(await AsyncQuery.Range(42, 3).CountAsync(x => x % 2 == 42), Is.Zero);
				Assert.That(await AsyncQuery.Range(42, 3).AnyAsync(), Is.True);
				Assert.That(await AsyncQuery.Range(42, 3).AnyAsync(x => x % 2 == 1), Is.True);
				Assert.That(await AsyncQuery.Range(42, 3).AnyAsync(x => x % 2 == 42), Is.False);
				Assert.That(await AsyncQuery.Range(42, 3).ToArrayAsync(), Is.EqualTo((int[]) [ 42, 43, 44 ]));
				Assert.That(await AsyncQuery.Range(42, 3).ToListAsync(), Is.EqualTo((List<int>) [ 42, 43, 44 ]));
				Assert.That(await AsyncQuery.Range(42, 3).ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 42, 43, 44 ]));
				Assert.That(await AsyncQuery.Range(42, 3).SumAsync(), Is.EqualTo(129));
				Assert.That(await AsyncQuery.Range(42, 3).MinAsync(), Is.EqualTo(42));
				Assert.That(await AsyncQuery.Range(42, 3).MaxAsync(), Is.EqualTo(44));
				Assert.That(await AsyncQuery.Range(42, 3).FirstAsync(), Is.EqualTo(42));
				Assert.That(await AsyncQuery.Range(42, 3).FirstOrDefaultAsync(-1), Is.EqualTo(42));
				Assert.That(await AsyncQuery.Range(42, 3).LastAsync(), Is.EqualTo(44));
				Assert.That(await AsyncQuery.Range(42, 3).LastOrDefaultAsync(-1), Is.EqualTo(44));
				Assert.That(async () => await AsyncQuery.Range(42, 3).SingleAsync(), Throws.InvalidOperationException);
				Assert.That(async () => await AsyncQuery.Range(42, 3).SingleOrDefaultAsync(-1), Throws.InvalidOperationException);
			}
			{
				Assert.That(await AsyncQuery.Range(0, 10).Take(5).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2, 3, 4 ]));
				Assert.That(await AsyncQuery.Range(0, 10).Skip(5).ToArrayAsync(), Is.EqualTo((int[]) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Range(0, 10).Skip(10).ToArrayAsync(), Is.Empty);
				Assert.That(await AsyncQuery.Range(0, 10).Skip(2).Take(5).ToArrayAsync(), Is.EqualTo((int[]) [ 2, 3, 4, 5, 6 ]));
				Assert.That(await AsyncQuery.Range(0, 10).Skip(5).Take(2).ToArrayAsync(), Is.EqualTo((int[]) [ 5, 6 ]));
				Assert.That(await AsyncQuery.Range(0, 10).Take(5).Take(2).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1 ]));
				Assert.That(await AsyncQuery.Range(0, 10).Take(3).Skip(5).ToArrayAsync(), Is.Empty);
			}
		}

		[Test]
		public async Task Test_Between_Int32()
		{
			{
				Assert.That(await AsyncQuery.Between(0, 0).CountAsync(), Is.Zero);
				Assert.That(await AsyncQuery.Between(0, 0).AnyAsync(), Is.False);
				Assert.That(await AsyncQuery.Between(0, 0).ToListAsync(), Is.Empty);
			}
			{
				Assert.That(await AsyncQuery.Between(5, 10).CountAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5, 10).CountAsync(x => x % 2 == 0), Is.EqualTo(2));
				Assert.That(await AsyncQuery.Between(5, 10).CountAsync(x => x % 2 == 1), Is.EqualTo(3));
				Assert.That(await AsyncQuery.Between(5, 10).CountAsync(x => x % 2 == 42), Is.Zero);
				Assert.That(await AsyncQuery.Between(5, 10).AnyAsync(), Is.True);
				Assert.That(await AsyncQuery.Between(5, 10).AnyAsync(x => x % 2 == 1), Is.True);
				Assert.That(await AsyncQuery.Between(5, 10).AnyAsync(x => x % 2 == 42), Is.False);
				Assert.That(await AsyncQuery.Between(5, 10).ToArrayAsync(), Is.EqualTo((int[]) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(5, 10).ToListAsync(), Is.EqualTo((List<int>) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(5, 10).ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(5, 10).SumAsync(), Is.EqualTo(5 + 6 + 7 + 8 + 9));
				Assert.That(await AsyncQuery.Between(5, 10).MinAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5, 10).MaxAsync(), Is.EqualTo(9));
				Assert.That(await AsyncQuery.Between(5, 10).FirstAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5, 10).FirstOrDefaultAsync(-1), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5, 10).LastAsync(), Is.EqualTo(9));
				Assert.That(await AsyncQuery.Between(5, 10).LastOrDefaultAsync(-1), Is.EqualTo(9));
				Assert.That(async () => await AsyncQuery.Between(5, 10).SingleAsync(), Throws.InvalidOperationException);
				Assert.That(async () => await AsyncQuery.Between(5, 10).SingleOrDefaultAsync(-1), Throws.InvalidOperationException);
			}
		}

		[Test]
		public async Task Test_Between_Int64()
		{
			{
				Assert.That(await AsyncQuery.Between(0L, 0L).CountAsync(), Is.Zero);
				Assert.That(await AsyncQuery.Between(0L, 0L).AnyAsync(), Is.False);
				Assert.That(await AsyncQuery.Between(0L, 0L).ToListAsync(), Is.Empty);
			}
			{
				Assert.That(await AsyncQuery.Between(5L, 10L).CountAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5L, 10L).CountAsync(x => x % 2 == 0), Is.EqualTo(2));
				Assert.That(await AsyncQuery.Between(5L, 10L).CountAsync(x => x % 2 == 1), Is.EqualTo(3));
				Assert.That(await AsyncQuery.Between(5L, 10L).CountAsync(x => x % 2 == 42), Is.Zero);
				Assert.That(await AsyncQuery.Between(5L, 10L).AnyAsync(), Is.True);
				Assert.That(await AsyncQuery.Between(5L, 10L).AnyAsync(x => x % 2 == 1), Is.True);
				Assert.That(await AsyncQuery.Between(5L, 10L).AnyAsync(x => x % 2 == 42), Is.False);
				Assert.That(await AsyncQuery.Between(5L, 10L).ToArrayAsync(), Is.EqualTo((long[]) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(5L, 10L).ToListAsync(), Is.EqualTo((List<long>) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(5L, 10L).ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<long>) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(5L, 10L).SumAsync(), Is.EqualTo(5 + 6 + 7 + 8 + 9));
				Assert.That(await AsyncQuery.Between(5L, 10L).MinAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5L, 10L).MaxAsync(), Is.EqualTo(9));
				Assert.That(await AsyncQuery.Between(5L, 10L).FirstAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5L, 10L).FirstOrDefaultAsync(-1), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(5L, 10L).LastAsync(), Is.EqualTo(9));
				Assert.That(await AsyncQuery.Between(5L, 10L).LastOrDefaultAsync(-1), Is.EqualTo(9));
				Assert.That(async () => await AsyncQuery.Between(5L, 10L).SingleAsync(), Throws.InvalidOperationException);
				Assert.That(async () => await AsyncQuery.Between(5L, 10L).SingleOrDefaultAsync(-1L), Throws.InvalidOperationException);
			}
		}

		[Test]
		public async Task Test_Between_Generic_Number()
		{
			{
				Assert.That(await AsyncQuery.Between(JsonNumber.Zero, JsonNumber.Zero).CountAsync(), Is.Zero);
				Assert.That(await AsyncQuery.Between(JsonNumber.Zero, JsonNumber.Zero).AnyAsync(), Is.False);
				Assert.That(await AsyncQuery.Between(JsonNumber.Zero, JsonNumber.Zero).ToListAsync(), Is.Empty);
			}
			{
				var five = JsonNumber.Return(5);
				var ten = JsonNumber.Return(10);

				Assert.That(await AsyncQuery.Between(five, ten).CountAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(five, ten).CountAsync(x => x.ToInt32() % 2 == 0), Is.EqualTo(2));
				Assert.That(await AsyncQuery.Between(five, ten).CountAsync(x => x.ToInt32() % 2 == 1), Is.EqualTo(3));
				Assert.That(await AsyncQuery.Between(five, ten).CountAsync(x => x.ToInt32() % 2 == 42), Is.Zero);
				Assert.That(await AsyncQuery.Between(five, ten).AnyAsync(), Is.True);
				Assert.That(await AsyncQuery.Between(five, ten).AnyAsync(x => x.ToInt32() % 2 == 1), Is.True);
				Assert.That(await AsyncQuery.Between(five, ten).AnyAsync(x => x.ToInt32() % 2 == 42), Is.False);
				Assert.That(await AsyncQuery.Between(five, ten).ToArrayAsync(), Is.EqualTo((long[]) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(five, ten).ToListAsync(), Is.EqualTo((List<long>) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(five, ten).ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<long>) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await AsyncQuery.Between(five, ten).SumAsync(), Is.EqualTo(5 + 6 + 7 + 8 + 9));
				Assert.That(await AsyncQuery.Between(five, ten).MinAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(five, ten).MaxAsync(), Is.EqualTo(9));
				Assert.That(await AsyncQuery.Between(five, ten).FirstAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(five, ten).FirstOrDefaultAsync(-1), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between(five, ten).LastAsync(), Is.EqualTo(9));
				Assert.That(await AsyncQuery.Between(five, ten).LastOrDefaultAsync(-1), Is.EqualTo(9));
				Assert.That(async () => await AsyncQuery.Between(five, ten).SingleAsync(), Throws.InvalidOperationException);
				Assert.That(async () => await AsyncQuery.Between(five, ten).SingleOrDefaultAsync(-1L), Throws.InvalidOperationException);
			}
		}

		[Test]
		public async Task Test_Between_Generic_Non_Number()
		{
			{
				Assert.That(await AsyncQuery.Between("a", "a", (_) => "b").CountAsync(), Is.Zero);
				Assert.That(await AsyncQuery.Between("a", "a", (_) => "b").AnyAsync(), Is.False);
				Assert.That(await AsyncQuery.Between("a", "a", (_) => "b").ToListAsync(), Is.Empty);
			}
			{
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").CountAsync(), Is.EqualTo(5));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").CountAsync(x => x.Length % 2 == 0), Is.EqualTo(2));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").CountAsync(x => x.Length % 2 == 1), Is.EqualTo(3));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").CountAsync(x => x.Length % 2 == 42), Is.Zero);
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").AnyAsync(), Is.True);
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").AnyAsync(x => x.Length % 2 == 1), Is.True);
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").AnyAsync(x => x.Length % 2 == 42), Is.False);
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").ToArrayAsync(), Is.EqualTo((string[]) [ "a", "aa", "aaa", "aaaa", "aaaaa" ]));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").ToListAsync(), Is.EqualTo((List<string>) [ "a", "aa", "aaa", "aaaa", "aaaaa" ]));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<string>) [ "a", "aa", "aaa", "aaaa", "aaaaa" ]));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").MinAsync(), Is.EqualTo("a"));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").MaxAsync(), Is.EqualTo("aaaaa"));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").FirstAsync(), Is.EqualTo("a"));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").FirstOrDefaultAsync("?"), Is.EqualTo("a"));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").LastAsync(), Is.EqualTo("aaaaa"));
				Assert.That(await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").LastOrDefaultAsync("?"), Is.EqualTo("aaaaa"));
				Assert.That(async () => await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").SingleAsync(), Throws.InvalidOperationException);
				Assert.That(async () => await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").SingleOrDefaultAsync("?"), Throws.InvalidOperationException);
				Assert.That(async () => await AsyncQuery.Between("a", "aaaaaa", (s) => s + "a").SumAsync(), Throws.InstanceOf<NotSupportedException>());
			}
		}

		[Test]
		public async Task Test_Producer_Single()
		{
			{
				// Func<T>

				var singleton = AsyncQuery.Singleton(() => 42);
				Assert.That(singleton, Is.Not.Null);

				await using (var iterator = singleton.GetAsyncEnumerator())
				{
					var next = await iterator.MoveNextAsync();
					Assert.That(next, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(42));
					next = await iterator.MoveNextAsync();
					Assert.That(next, Is.False);
				}

				var list = await singleton.ToListAsync();
				Assert.That(list, Is.EqualTo(new[] { 42 }));

				var array = await singleton.ToArrayAsync();
				Assert.That(array, Is.EqualTo(new[] { 42 }));

				Assert.That(await singleton.AnyAsync(), Is.True);

				Assert.That(await singleton.AllAsync(x => x == 42), Is.True);
				Assert.That(await singleton.AllAsync(x => x != 42), Is.False);

				Assert.That(await singleton.CountAsync(), Is.EqualTo(1));
			}

			{
				// Func<Task<T>>

				var singleton = AsyncQuery.Singleton(() => Task.Delay(50).ContinueWith(_ => 42));
				Assert.That(singleton, Is.Not.Null);

				await using (var iterator = singleton.GetAsyncEnumerator())
				{
					var next = await iterator.MoveNextAsync();
					Assert.That(next, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(42));
					next = await iterator.MoveNextAsync();
					Assert.That(next, Is.False);
				}

				var list = await singleton.ToListAsync();
				Assert.That(list, Is.EqualTo(new[] { 42 }));

				var array = await singleton.ToArrayAsync();
				Assert.That(array, Is.EqualTo(new[] { 42 }));

				Assert.That(await singleton.AnyAsync(), Is.True);

				Assert.That(await singleton.AllAsync(x => x == 42), Is.True);
				Assert.That(await singleton.AllAsync(x => x != 42), Is.False);

				Assert.That(await singleton.CountAsync(), Is.EqualTo(1));
			}

			{
				// Func<CancellationToken, Task<T>>

				var singleton = AsyncQuery.Singleton((ct) => Task.Delay(50, ct).ContinueWith(_ => 42, ct));
				Assert.That(singleton, Is.Not.Null);

				await using (var iterator = singleton.GetAsyncEnumerator())
				{
					var next = await iterator.MoveNextAsync();
					Assert.That(next, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(42));
					next = await iterator.MoveNextAsync();
					Assert.That(next, Is.False);
				}

				var list = await singleton.ToListAsync();
				Assert.That(list, Is.EqualTo(new[] { 42 }));

				var array = await singleton.ToArrayAsync();
				Assert.That(array, Is.EqualTo(new[] { 42 }));

				Assert.That(await singleton.AnyAsync(), Is.True);

				Assert.That(await singleton.AllAsync(x => x == 42), Is.True);
				Assert.That(await singleton.AllAsync(x => x != 42), Is.False);

				Assert.That(await singleton.CountAsync(), Is.EqualTo(1));
			}
		}

		[Test]
		public async Task Test_Can_Select_Sync()
		{
			{
				var selected = AsyncQuery.Empty<int>().Select(x => x + 1);
				Assert.That(await selected.CountAsync(), Is.EqualTo(0));
				Assert.That(await selected.AnyAsync(), Is.False);
				Assert.That(await selected.ToArrayAsync(), Is.Empty);
				Assert.That(await selected.ToListAsync(), Is.Empty);
			}
			{
				var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

				var selected = source.Select(x => x + 1);
				Assert.That(selected, Is.Not.Null);

				await using (var iterator = selected.GetAsyncEnumerator())
				{
					// first 10 calls should return 'true', and current value should match
					for (int i = 0; i < 10; i++)
					{
						Assert.That(await iterator.MoveNextAsync(), Is.True);
						Assert.That(iterator.Current, Is.EqualTo(i + 1));
					}

					// last call should return 'false'
					Assert.That(await iterator.MoveNextAsync(), Is.False);
				}

				var list = await selected.ToListAsync();
				Assert.That(list, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));

				var array = await selected.ToArrayAsync();
				Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				var selected = source.Select(x => x + 1);
				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.CountAsync(), Is.EqualTo(3));
				Assert.That(await selected.AnyAsync(), Is.True);
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				var selected = source.Select(x => x + 1);
				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.CountAsync(), Is.EqualTo(3));
				Assert.That(await selected.AnyAsync(), Is.True);
			}
		}

		[Test]
		public async Task Test_Can_Select_Async()
		{
			{
				var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

				var selected = source.Select(
					async (x, ct) =>
					{
						await Task.Delay(10, ct);
						return x + 1;
					}
				);
				Assert.That(selected, Is.Not.Null);

				var list = await selected.ToListAsync();
				Assert.That(list, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));

				var array = await selected.ToArrayAsync();
				Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				var selected = source.Select((int x, CancellationToken _) => Task.FromResult(x + 1));
				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.CountAsync(), Is.EqualTo(3));
				Assert.That(await selected.AnyAsync(), Is.True);
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				var selected = source.Select((int x, CancellationToken _) => Task.FromResult(x + 1));
				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 43, 44, 45 ]));
				Assert.That(await selected.CountAsync(), Is.EqualTo(3));
				Assert.That(await selected.AnyAsync(), Is.True);
			}
		}

		[Test]
		public async Task Test_Can_Select_Multiple_Times()
		{
			var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

			var squares = source.Select(x => (long)x * x);
			Assert.That(squares, Is.Not.Null);

			var roots = squares.Select(x => Math.Sqrt(x));
			Assert.That(roots, Is.Not.Null);

			var list = await roots.ToListAsync();
			Assert.That(list, Is.EqualTo(new [] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));

			var array = await roots.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
		}

		[Test]
		public async Task Test_Can_Select_Async_Multiple_Times()
		{
			var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

			var squares = source.Select((int x, CancellationToken _) => Task.FromResult((long) x * x));
			Assert.That(squares, Is.Not.Null);

			var roots = squares.Select((long x, CancellationToken _) => Task.FromResult(Math.Sqrt(x)));
			Assert.That(roots, Is.Not.Null);

			var list = await roots.ToListAsync();
			Assert.That(list, Is.EqualTo(new [] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));

			var array = await roots.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
		}

		[Test]
		public async Task Test_Can_Where()
		{
			var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

			var query = source.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);

			await using (var iterator = query.GetAsyncEnumerator())
			{
				// only half the items match, so only 5 are expected to go out of the enumeration...
				for (int i = 0; i < 5; i++)
				{
					Assert.That(await iterator.MoveNextAsync(), Is.True);
					Assert.That(iterator.Current, Is.EqualTo(i * 2 + 1));
				}
				// last call should return false
				Assert.That(await iterator.MoveNextAsync(), Is.False);
			}

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Skip()
		{
			{
				Assert.That(await AsyncQuery.Empty<int>().Skip(0).ToListAsync(), Is.Empty);
				Assert.That(await AsyncQuery.Empty<int>().Skip(1).ToListAsync(), Is.Empty);
				Assert.That(() => AsyncQuery.Empty<int>().Skip(-1), Throws.InstanceOf<ArgumentException>());
			}
			{
				var query = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);
				Assert.That(() => query.Skip(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
				Assert.That(await query.Skip(0).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));
				Assert.That(await query.Skip(1).ToArrayAsync(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));
				Assert.That(await query.Skip(9).ToArrayAsync(), Is.EqualTo((int[]) [ 9 ]));
				Assert.That(await query.Skip(10).ToArrayAsync(), Is.Empty);
				Assert.That(await query.Skip(11).ToArrayAsync(), Is.Empty);

				Assert.That(await query.Skip(2).Skip(3).ToArrayAsync(), Is.EqualTo((int[]) [ 5, 6, 7, 8, 9 ]));
				Assert.That(await query.Skip(7).Skip(5).ToArrayAsync(), Is.Empty);

				Assert.That(await query.Take(5).Skip(0).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2, 3, 4 ]));
				Assert.That(await query.Take(5).Skip(2).ToArrayAsync(), Is.EqualTo((int[]) [ 2, 3, 4 ]));
				Assert.That(await query.Take(5).Skip(5).ToArrayAsync(), Is.Empty);

				Assert.That(await query.Skip(2).Take(5).Skip(1).ToArrayAsync(), Is.EqualTo((int[]) [ 3, 4, 5, 6 ]));
				Assert.That(await query.Skip(2).Take(5).Skip(5).ToArrayAsync(), Is.Empty);
				Assert.That(await query.Skip(2).Take(5).Skip(6).ToArrayAsync(), Is.Empty);
			}
		}

		[Test]
		public async Task Test_Can_Take()
		{
			{
				Assert.That(await AsyncQuery.Empty<int>().Take(0).ToListAsync(), Is.Empty);
				Assert.That(await AsyncQuery.Empty<int>().Take(1).ToListAsync(), Is.Empty);
				Assert.That(() => AsyncQuery.Empty<int>().Take(-1), Throws.InstanceOf<ArgumentException>());
			}
			{
				var source = AsyncQuery.Range(0, 42, this.Cancellation);

				var query = source.Take(10);
				Assert.That(query, Is.Not.Null);

				await using (var iterator = query.GetAsyncEnumerator())
				{
					ValueTask<bool> next;
					// first 10 calls should return an already completed 'true' task, and current value should match
					for (int i = 0; i < 10; i++)
					{
						next = iterator.MoveNextAsync();
						Assert.That(next.IsCompleted, Is.True);
						Assert.That(next.Result, Is.True);
						Assert.That(iterator.Current, Is.EqualTo(i));
					}

					// last call should return an already completed 'false' task
					next = iterator.MoveNextAsync();
					Assert.That(next.IsCompleted, Is.True);
					Assert.That(next.Result, Is.False);
				}

				var list = await query.ToListAsync();
				Assert.That(list, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));

				var array = await query.ToArrayAsync();
				Assert.That(array, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
			}
			{
				var query = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);
				Assert.That(() => query.Take(-1), Throws.InstanceOf<ArgumentException>());
				Assert.That(await query.Take(0).ToArrayAsync(), Is.Empty);
				Assert.That(await query.Take(1).ToArrayAsync(), Is.EqualTo((int[]) [ 0 ]));
				Assert.That(await query.Take(10).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));
				Assert.That(await query.Take(11).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));

				Assert.That(await query.Take(5).Take(3).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2 ]));
				Assert.That(await query.Take(3).Take(5).ToArrayAsync(), Is.EqualTo((int[]) [ 0, 1, 2 ]));
				Assert.That(await query.Skip(2).Take(3).ToArrayAsync(), Is.EqualTo((int[]) [ 2, 3, 4 ]));
				Assert.That(await query.Skip(10).Take(3).ToArrayAsync(), Is.Empty);

				Assert.That(await query.Take(2..5).ToArrayAsync(), Is.EqualTo((int[]) [ 2, 3, 4 ]));

#if NET10_0_OR_GREATER
				//note: support of ranges with '^' require .NET 10 for now!
				Assert.That(await query.Take(^5..^2).ToArrayAsync(), Is.EqualTo((int[]) [ 5, 6, 7 ]));
#endif
			}
		}

		[Test]
		public async Task Test_Can_Where_And_Take()
		{
			var source = AsyncQuery.Range(0, 42, this.Cancellation);

			var query = source
				.Where(x => x % 2 == 1)
				.Take(10);
			Assert.That(query, Is.Not.Null);

			await using (var iterator = query.GetAsyncEnumerator())
			{
				ValueTask<bool> next;
				// first 10 calls should return an already completed 'true' task, and current value should match
				for (int i = 0; i < 10; i++)
				{
					next = iterator.MoveNextAsync();
					Assert.That(next.IsCompleted, Is.True);
					Assert.That(next.Result, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(i * 2 + 1));
				}
				// last call should return an already completed 'false' task
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 }));
		}

		[Test]
		public async Task Test_Can_Take_And_Where()
		{
			var source = AsyncQuery.Range(0, 42, this.Cancellation);

			var query = source
				.Take(10)
				.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Combine_Where_Clauses()
		{
			var source = AsyncQuery.Range(0, 42, this.Cancellation);

			var query = source
				.Where(x => x % 2 == 1)
				.Where(x => x % 3 == 0);
			Assert.That(query, Is.Not.Null);

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 3, 9, 15, 21, 27, 33, 39 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 3, 9, 15, 21, 27, 33, 39 }));
		}

		[Test]
		public async Task Test_Can_Skip_And_Where()
		{
			var source = AsyncQuery.Range(0, 42, this.Cancellation);

			var query = source
				.Skip(21)
				.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41 }));
		}

		[Test]
		public async Task Test_Can_Where_And_Skip()
		{
			var source = AsyncQuery.Range(0, 42, this.Cancellation);

			var query = source
				.Where(x => x % 2 == 1)
				.Skip(15);
			Assert.That(query, Is.Not.Null);

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 31, 33, 35, 37, 39, 41 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 31, 33, 35, 37, 39, 41 }));
		}

		[Test]
		public async Task Test_Can_SelectMany()
		{
			{
				var selected = AsyncQuery.Empty<int>().SelectMany(x => (int[]) [ 1, 2, 3 ]);
				Assert.That(await selected.CountAsync(), Is.EqualTo(0));
				Assert.That(await selected.AnyAsync(), Is.False);
				Assert.That(await selected.ToArrayAsync(), Is.Empty);
				Assert.That(await selected.ToListAsync(), Is.Empty);
			}
			{
				var source = AsyncQuery.Range(0, 5, this.Cancellation);
				var selected = source.SelectMany((x) => Enumerable.Range(0, x).Select(y => x * 10 + y));
				Assert.That(selected, Is.Not.Null);

				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));

				Assert.That(await selected.AnyAsync(), Is.True);
				Assert.That(await selected.AnyAsync(x => x % 10 == 2), Is.True);
				Assert.That(await selected.AnyAsync(x => x % 10 == 7), Is.False);

				Assert.That(await selected.CountAsync(), Is.EqualTo(10));
				Assert.That(await selected.CountAsync(x => x % 10 == 2), Is.EqualTo(2));
				Assert.That(await selected.CountAsync(x => x % 10 == 7), Is.EqualTo(0));

				Assert.That(await selected.FirstAsync(), Is.EqualTo(10));
				Assert.That(await selected.FirstAsync(x => x % 10 == 2), Is.EqualTo(32));
				Assert.That(async () => await selected.FirstAsync(x => x % 10 == 7), Throws.InvalidOperationException);

				Assert.That(await selected.LastAsync(), Is.EqualTo(43));
				Assert.That(await selected.LastAsync(x => x % 10 == 2), Is.EqualTo(42));
				Assert.That(async () => await selected.LastAsync(x => x % 10 == 7), Throws.InvalidOperationException);

				Assert.That(await selected.FirstOrDefaultAsync(), Is.EqualTo(10));
				Assert.That(await selected.FirstOrDefaultAsync(x => x % 10 == 2, -1), Is.EqualTo(32));
				Assert.That(await selected.FirstOrDefaultAsync(x => x % 10 == 7, -1), Is.EqualTo(-1));

				Assert.That(await selected.LastOrDefaultAsync(), Is.EqualTo(43));
				Assert.That(await selected.LastOrDefaultAsync(x => x % 10 == 2, -1), Is.EqualTo(42));
				Assert.That(await selected.LastOrDefaultAsync(x => x % 10 == 7, -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4 ]);
				var selected = source.SelectMany((x) => Enumerable.Range(0, x).Select(y => x * 10 + y));
				Assert.That(selected, Is.Not.Null);

				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));

				Assert.That(await selected.AnyAsync(), Is.True);
				Assert.That(await selected.AnyAsync(x => x % 10 == 2), Is.True);
				Assert.That(await selected.AnyAsync(x => x % 10 == 7), Is.False);

				Assert.That(await selected.CountAsync(), Is.EqualTo(10));
				Assert.That(await selected.CountAsync(x => x % 10 == 2), Is.EqualTo(2));
				Assert.That(await selected.CountAsync(x => x % 10 == 7), Is.EqualTo(0));

				Assert.That(await selected.FirstAsync(), Is.EqualTo(10));
				Assert.That(await selected.FirstAsync(x => x % 10 == 2), Is.EqualTo(32));
				Assert.That(async () => await selected.FirstAsync(x => x % 10 == 7), Throws.InvalidOperationException);

				Assert.That(await selected.LastAsync(), Is.EqualTo(43));
				Assert.That(await selected.LastAsync(x => x % 10 == 2), Is.EqualTo(42));
				Assert.That(async () => await selected.LastAsync(x => x % 10 == 7), Throws.InvalidOperationException);

				Assert.That(await selected.FirstOrDefaultAsync(), Is.EqualTo(10));
				Assert.That(await selected.FirstOrDefaultAsync(x => x % 10 == 2, -1), Is.EqualTo(32));
				Assert.That(await selected.FirstOrDefaultAsync(x => x % 10 == 7, -1), Is.EqualTo(-1));

				Assert.That(await selected.LastOrDefaultAsync(), Is.EqualTo(43));
				Assert.That(await selected.LastOrDefaultAsync(x => x % 10 == 2, -1), Is.EqualTo(42));
				Assert.That(await selected.LastOrDefaultAsync(x => x % 10 == 7, -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncQuery([ 0, 1, 2, 3, 4 ]);
				var selected = source.SelectMany((x) => Enumerable.Range(0, x).Select(y => x * 10 + y));
				Assert.That(selected, Is.Not.Null);

				Assert.That(await selected.ToArrayAsync(), Is.EqualTo((int[]) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));
				Assert.That(await selected.ToListAsync(), Is.EqualTo((List<int>) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));
				Assert.That(await selected.ToImmutableArrayAsync(), Is.EqualTo((ImmutableArray<int>) [ 10, 20, 21, 30, 31, 32, 40, 41, 42, 43 ]));

				Assert.That(await selected.AnyAsync(), Is.True);
				Assert.That(await selected.AnyAsync(x => x % 10 == 2), Is.True);
				Assert.That(await selected.AnyAsync(x => x % 10 == 7), Is.False);

				Assert.That(await selected.CountAsync(), Is.EqualTo(10));
				Assert.That(await selected.CountAsync(x => x % 10 == 2), Is.EqualTo(2));
				Assert.That(await selected.CountAsync(x => x % 10 == 7), Is.EqualTo(0));

				Assert.That(await selected.FirstAsync(), Is.EqualTo(10));
				Assert.That(await selected.FirstAsync(x => x % 10 == 2), Is.EqualTo(32));
				Assert.That(async () => await selected.FirstAsync(x => x % 10 == 7), Throws.InvalidOperationException);

				Assert.That(await selected.LastAsync(), Is.EqualTo(43));
				Assert.That(await selected.LastAsync(x => x % 10 == 2), Is.EqualTo(42));
				Assert.That(async () => await selected.LastAsync(x => x % 10 == 7), Throws.InvalidOperationException);

				Assert.That(await selected.FirstOrDefaultAsync(), Is.EqualTo(10));
				Assert.That(await selected.FirstOrDefaultAsync(x => x % 10 == 2, -1), Is.EqualTo(32));
				Assert.That(await selected.FirstOrDefaultAsync(x => x % 10 == 7, -1), Is.EqualTo(-1));

				Assert.That(await selected.LastOrDefaultAsync(), Is.EqualTo(43));
				Assert.That(await selected.LastOrDefaultAsync(x => x % 10 == 2, -1), Is.EqualTo(42));
				Assert.That(await selected.LastOrDefaultAsync(x => x % 10 == 7, -1), Is.EqualTo(-1));
			}
		}

		[Test]
		public async Task Test_Can_Get_First()
		{
			{
				Assert.That(async () => await AsyncQuery.Empty<int>().FirstAsync(), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await AsyncQuery.Empty<int>().FirstAsync(x => x == 42), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await AsyncQuery.Empty<int>().FirstAsync((x, _) => Task.FromResult(x == 42)), Throws.InstanceOf<InvalidOperationException>());
			}
			{
				var source = AsyncQuery.Range(42, 3, this.Cancellation);
				Assert.That(await source.FirstAsync(), Is.EqualTo(42));
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(await source.FirstAsync(), Is.EqualTo(42));
				Assert.That(await source.FirstAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(async () => await source.FirstAsync(x => x % 2 == 42), Throws.InvalidOperationException);
				Assert.That(await source.FirstAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(async () => await source.FirstAsync((x, _) => Task.FromResult(x % 2 == 42)), Throws.InvalidOperationException);
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				Assert.That(await source.FirstAsync(), Is.EqualTo(42));
				Assert.That(await source.FirstAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(async () => await source.FirstAsync(x => x % 2 == 42), Throws.InvalidOperationException);
				Assert.That(await source.FirstAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(async () => await source.FirstAsync((x, _) => Task.FromResult(x % 2 == 42)), Throws.InvalidOperationException);
			}
		}

		[Test]
		public async Task Test_Can_Get_FirstOrDefault()
		{
			{
				Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync(), Is.EqualTo(0));
				Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync(-1), Is.EqualTo(-1));
				Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync(x => x == 42), Is.EqualTo(0));
				Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync(x => x == 42, -1), Is.EqualTo(-1));
				Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync((x, _) => Task.FromResult(x == 42)), Is.EqualTo(0));
				Assert.That(await AsyncQuery.Empty<int>().FirstOrDefaultAsync((x, _) => Task.FromResult(x == 42), -1), Is.EqualTo(-1));
			}
			{
				var source = AsyncQuery.Range(42, 3, this.Cancellation);
				Assert.That(await source.FirstOrDefaultAsync(), Is.EqualTo(42));
				Assert.That(await source.FirstOrDefaultAsync(-1), Is.EqualTo(42));
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(await source.FirstOrDefaultAsync(), Is.EqualTo(42));
				Assert.That(await source.FirstOrDefaultAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(await source.FirstOrDefaultAsync(x => x % 2 == 42), Is.EqualTo(0));
				Assert.That(await source.FirstOrDefaultAsync(x => x % 2 == 42, -1), Is.EqualTo(-1));
				Assert.That(await source.FirstOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(await source.FirstOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 42)), Is.EqualTo(0));
				Assert.That(await source.FirstOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 42), -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				Assert.That(await source.FirstOrDefaultAsync(), Is.EqualTo(42));
				Assert.That(await source.FirstOrDefaultAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(await source.FirstOrDefaultAsync(x => x % 2 == 42), Is.EqualTo(0));
				Assert.That(await source.FirstOrDefaultAsync(x => x % 2 == 42, -1), Is.EqualTo(-1));
				Assert.That(await source.FirstOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(await source.FirstOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 42)), Is.EqualTo(0));
				Assert.That(await source.FirstOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 42), -1), Is.EqualTo(-1));
			}
		}

		[Test]
		public async Task Test_Can_Get_Single()
		{
			{
				var source = FakeAsyncLinqIterator<int>([ ]);
				Assert.That(async () => await source.SingleAsync(), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await source.SingleAsync(x => x == 42), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await source.SingleAsync((x, _) => Task.FromResult(x == 42)), Throws.InstanceOf<InvalidOperationException>());
			}
			{
				var source = FakeAsyncLinqIterator([ 42 ]);
				Assert.That(await source.SingleAsync(), Is.EqualTo(42));
				Assert.That(await source.SingleAsync(x => x == 42), Is.EqualTo(42));
				Assert.That(async () => await source.SingleAsync(x => x != 42), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(await source.SingleAsync((x, ct) => Task.FromResult(x == 42)), Is.EqualTo(42));
				Assert.That(async () => await source.SingleAsync((x, _) => Task.FromResult(x != 42)), Throws.InstanceOf<InvalidOperationException>());
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(async () => await source.SingleAsync(), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(await source.SingleAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(async () => await source.SingleAsync(x => x % 2 == 0), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(await source.SingleAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(async () => await source.SingleAsync((x, _) => Task.FromResult(x % 2 == 0)), Throws.InstanceOf<InvalidOperationException>());
			}
		}

		[Test]
		public async Task Test_Can_Get_SingleOrDefault()
		{
			{
				var source = FakeAsyncLinqIterator<int>([ ]);
				Assert.That(async () => await source.SingleOrDefaultAsync(), Is.EqualTo(0));
				Assert.That(async () => await source.SingleOrDefaultAsync(-1), Is.EqualTo(-1));
				Assert.That(async () => await source.SingleOrDefaultAsync(x => x == 42), Is.EqualTo(0));
				Assert.That(async () => await source.SingleOrDefaultAsync(x => x == 42, -1), Is.EqualTo(-1));
				Assert.That(async () => await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x == 42)), Is.EqualTo(0));
				Assert.That(async () => await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x == 42), -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncLinqIterator([ 42 ]);
				Assert.That(await source.SingleOrDefaultAsync(), Is.EqualTo(42));
				Assert.That(await source.SingleOrDefaultAsync(x => x == 42), Is.EqualTo(42));
				Assert.That(await source.SingleOrDefaultAsync(x => x != 42), Is.EqualTo(0));
				Assert.That(await source.SingleOrDefaultAsync(x => x != 42, -1), Is.EqualTo(-1));
				Assert.That(await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x == 42)), Is.EqualTo(42));
				Assert.That(await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x != 42)), Is.EqualTo(0));
				Assert.That(await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x != 42), -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(async () => await source.SingleOrDefaultAsync(), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(await source.SingleOrDefaultAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(async () => await source.SingleOrDefaultAsync(x => x % 2 == 0), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(async () => await source.SingleOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 0)), Throws.InstanceOf<InvalidOperationException>());
			}
		}

		[Test]
		public async Task Test_Can_Get_Last()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(async () => await source.LastAsync(), Throws.InstanceOf<InvalidOperationException>());
			}
			{
				var source = AsyncQuery.Range(42, 3, this.Cancellation);
				Assert.That(await source.LastAsync(), Is.EqualTo(44));
				Assert.That(await source.LastAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(await source.LastAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
			}
			{
				var source = FakeAsyncLinqIterator([ "foo", "bar", "baz" ]);
				Assert.That(await source.LastAsync(), Is.EqualTo("baz"));
				Assert.That(await source.LastAsync(x => x.StartsWith('f')), Is.EqualTo("foo"));
				Assert.That(await source.LastAsync((x, _) => Task.FromResult(x.StartsWith('f'))), Is.EqualTo("foo"));
			}
			{
				var source = FakeAsyncQuery([ "foo", "bar", "baz" ]);
				Assert.That(await source.LastAsync(), Is.EqualTo("baz"));
				Assert.That(await source.LastAsync(x => x.StartsWith('f')), Is.EqualTo("foo"));
				Assert.That(await source.LastAsync((x, _) => Task.FromResult(x.StartsWith('f'))), Is.EqualTo("foo"));
			}
		}

		[Test]
		public async Task Test_Can_Get_LastOrDefault()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(await source.LastOrDefaultAsync(), Is.EqualTo(0));
				Assert.That(await source.LastOrDefaultAsync(-1), Is.EqualTo(-1));
				Assert.That(await source.LastOrDefaultAsync(x => x == 42), Is.EqualTo(0));
				Assert.That(await source.LastOrDefaultAsync(x => x == 42, -1), Is.EqualTo(-1));
				Assert.That(await source.LastOrDefaultAsync((x, _) => Task.FromResult(x == 42)), Is.EqualTo(0));
				Assert.That(await source.LastOrDefaultAsync((x, _) => Task.FromResult(x == 42), -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(await source.LastOrDefaultAsync(), Is.EqualTo(44));
				Assert.That(await source.LastOrDefaultAsync(-1), Is.EqualTo(44));
				Assert.That(await source.LastOrDefaultAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(await source.LastOrDefaultAsync(x => x % 2 == 42, -1), Is.EqualTo(-1));
				Assert.That(await source.LastOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(await source.LastOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 42), -1), Is.EqualTo(-1));
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				Assert.That(await source.LastOrDefaultAsync(), Is.EqualTo(44));
				Assert.That(await source.LastOrDefaultAsync(-1), Is.EqualTo(44));
				Assert.That(await source.LastOrDefaultAsync(x => x % 2 == 1), Is.EqualTo(43));
				Assert.That(await source.LastOrDefaultAsync(x => x % 2 == 42, -1), Is.EqualTo(-1));
				Assert.That(await source.LastOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(43));
				Assert.That(await source.LastOrDefaultAsync((x, _) => Task.FromResult(x % 2 == 42), -1), Is.EqualTo(-1));
			}
		}

		[Test]
		public async Task Test_Can_Get_ElementAt()
		{
			var source = AsyncQuery.Range(42, 10, this.Cancellation);

			Assert.That(async () => await source.ElementAtAsync(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			int item = await source.ElementAtAsync(0);
			Assert.That(item, Is.EqualTo(42));

			item = await source.ElementAtAsync(5);
			Assert.That(item, Is.EqualTo(47));

			item = await source.ElementAtAsync(9);
			Assert.That(item, Is.EqualTo(51));

			Assert.That(async () => await source.ElementAtAsync(10), Throws.InstanceOf<InvalidOperationException>());

			source = AsyncQuery.Empty<int>();
			Assert.That(async () => await source.ElementAtAsync(0), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Get_ElementAtOrDefault()
		{
			var source = AsyncQuery.Range(42, 10, this.Cancellation);

			Assert.That(() => source.ElementAtOrDefaultAsync(-1).GetAwaiter().GetResult(), Throws.InstanceOf<ArgumentOutOfRangeException>());

			int item = await source.ElementAtOrDefaultAsync(0);
			Assert.That(item, Is.EqualTo(42));

			item = await source.ElementAtOrDefaultAsync(5);
			Assert.That(item, Is.EqualTo(47));

			item = await source.ElementAtOrDefaultAsync(9);
			Assert.That(item, Is.EqualTo(51));

			item = await source.ElementAtOrDefaultAsync(10);
			Assert.That(item, Is.EqualTo(0));

			source = AsyncQuery.Empty<int>();
			item = await source.ElementAtOrDefaultAsync(0);
			Assert.That(item, Is.EqualTo(0));
			item = await source.ElementAtOrDefaultAsync(42);
			Assert.That(item, Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Can_Distinct()
		{
			int[] items = [ 1, 42, 7, 42, 9, 13, 7, 66 ];

			var source = FakeAsyncLinqIterator(items);

			var results = await source.Distinct().ToListAsync();
			Assert.That(results, Is.Not.Null.And.EqualTo(items.Distinct().ToList()));

			var sequence = Enumerable.Range(0, 100).Select(x => (x * 1049) % 43);
			source = sequence.ToAsyncQuery(this.Cancellation);
			results = await source.Distinct().ToListAsync();
			Assert.That(results, Is.Not.Null.And.EqualTo(sequence.Distinct().ToList()));
		}

		[Test]
		public async Task Test_Can_Distinct_With_Comparer()
		{
			string[] items = [ "World", "hello", "Hello", "world", "World!", "FileNotFound" ];

			var source = FakeAsyncLinqIterator(items);

			var results = await source.Distinct(StringComparer.Ordinal).ToListAsync();
			Assert.That(results, Is.Not.Null.And.EqualTo(items.Distinct(StringComparer.Ordinal).ToList()));

			results = await source.Distinct(StringComparer.OrdinalIgnoreCase).ToListAsync();
			Assert.That(results, Is.Not.Null.And.EqualTo(items.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
		}

		
		[Test]
		public async Task Test_Can_Await_ForEach()
		{
			var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

			var items = new List<int>();
			await foreach (var x in source)
			{
				Assert.That(items.Count, Is.LessThan(10));
				items.Add(x);
			};

			Assert.That(items, Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));
		}

		[Test]
		public async Task Test_Can_ForEach()
		{
			var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

			var items = new List<int>();

			await source.ForEachAsync((x) =>
			{
				Assert.That(items.Count, Is.LessThan(10));
				items.Add(x);
			});

			Assert.That(items, Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));
		}

		[Test]
		public async Task Test_Can_ForEach_Async()
		{
			var source = FakeAsyncLinqIterator([ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]);

			var items = new List<int>();

			await source.ForEachAsync(async (x) =>
			{
				Assert.That(items.Count, Is.LessThan(10));
				await Task.Delay(10);
				items.Add(x);
			});

			Assert.That(items, Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]));
		}

		internal sealed class CustomAsyncLinqIterator<T> : AsyncLinqIterator<T>
		{

			public CustomAsyncLinqIterator(T[] items, CancellationToken ct)
			{
				this.Items = items;
				this.Cancellation = ct;
			}

			public T[] Items { get; }

			private int Index;

			/// <inheritdoc />
			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override CustomAsyncLinqIterator<T> Clone() => new(this.Items, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Index = 0;
				return new(true);
			}

			/// <inheritdoc />
			protected override async ValueTask<bool> OnNextAsync()
			{
				var index = this.Index;
				if (index >= this.Items.Length)
				{
					return await Completed();
				}

				await Task.Yield();

				this.Index = index + 1;
				return Publish(this.Items[index]);
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				this.Index = this.Items.Length;
				return default;
			}
		}

		public sealed class CustomAsyncQuery<T> : IAsyncQuery<T>
		{

			public CustomAsyncQuery(T[] items, CancellationToken ct)
			{
				this.Items = items;
				this.Cancellation = ct;
			}

			private T[] Items { get; }

			/// <inheritdoc />
			public CancellationToken Cancellation { get; }

			/// <inheritdoc />
			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct) => GetAsyncEnumerator(AsyncIterationHint.All);

			/// <inheritdoc />
			public async IAsyncEnumerator<T> GetAsyncEnumerator(AsyncIterationHint hint)
			{
				this.Cancellation.ThrowIfCancellationRequested();
				foreach (var item in this.Items)
				{
					await Task.Yield();

					yield return item;
				}
			}
		}

		/// <summary>Returns a custom query that implements <see cref="AsyncLinqIterator{TResult}"/></summary>
		private IAsyncQuery<T> FakeAsyncLinqIterator<T>(IEnumerable<T> items) => new CustomAsyncLinqIterator<T>((items as T[]) ?? items.ToArray(), this.Cancellation);

		/// <summary>Returns a custom query that does NOT implement <see cref="AsyncLinqIterator{TResult}"/></summary>
		private IAsyncQuery<T> FakeAsyncQuery<T>(IEnumerable<T> items) => new CustomAsyncQuery<T>((items as T[]) ?? items.ToArray(), this.Cancellation);

		[Test]
		public async Task Test_Can_Any()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(await source.AnyAsync(), Is.False);
				Assert.That(await source.AnyAsync(x => x == 42), Is.False);
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(await source.AnyAsync(), Is.True);
				Assert.That(await source.AnyAsync(x => x == 42), Is.True);
				Assert.That(await source.AnyAsync(x => x % 2 == 1), Is.True);
				Assert.That(await source.AnyAsync(x => x % 2 == 0), Is.True);
				Assert.That(await source.AnyAsync(x => x == 45), Is.False);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == 42)), Is.True);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == 45)), Is.False);
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				Assert.That(await source.AnyAsync(), Is.True);
				Assert.That(await source.AnyAsync(x => x == 42), Is.True);
				Assert.That(await source.AnyAsync(x => x % 2 == 1), Is.True);
				Assert.That(await source.AnyAsync(x => x % 2 == 0), Is.True);
				Assert.That(await source.AnyAsync(x => x == 45), Is.False);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == 42)), Is.True);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == 45)), Is.False);
			}
			{
				var source = FakeAsyncLinqIterator([ "hello", "world" ]);
				Assert.That(await source.AnyAsync(), Is.True);
				Assert.That(await source.AnyAsync(x => x == "hello"), Is.True);
				Assert.That(await source.AnyAsync(x => x == "world"), Is.True);
				Assert.That(await source.AnyAsync(x => x == "other"), Is.False);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == "hello")), Is.True);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == "other")), Is.False);
			}
			{
				var source = AsyncQuery.Range(0, 2, this.Cancellation).Select(x => x == 0 ? "hello" : "world");
				Assert.That(await source.AnyAsync(), Is.True);
				Assert.That(await source.AnyAsync(x => x == "hello"), Is.True);
				Assert.That(await source.AnyAsync(x => x == "world"), Is.True);
				Assert.That(await source.AnyAsync(x => x == "other"), Is.False);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == "hello")), Is.True);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == "other")), Is.False);
			}
			{
				var source = Enumerable.Range(0, 2).Select(x => x == 0 ? "hello" : "world").ToAsyncQuery(this.Cancellation);
				Assert.That(await source.AnyAsync(), Is.True);
				Assert.That(await source.AnyAsync(x => x == "hello"), Is.True);
				Assert.That(await source.AnyAsync(x => x == "world"), Is.True);
				Assert.That(await source.AnyAsync(x => x == "other"), Is.False);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == "hello")), Is.True);
				Assert.That(await source.AnyAsync((x, _) => Task.FromResult(x == "other")), Is.False);
			}
		}

		[Test]
		public async Task Test_Can_All()
		{
			Assert.That(Enumerable.Empty<int>().All(x => x == 42), Is.True);
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(await source.AllAsync(x => x == 42), Is.True);
				Assert.That(await source.AllAsync(x => x != 42), Is.True);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x == 42)), Is.True);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x != 42)), Is.True);
			}
			{
				var source = AsyncQuery.Range(0, 1, this.Cancellation);
				Assert.That(await source.AllAsync(x => x == 0), Is.True);
				Assert.That(await source.AllAsync(x => x == 42), Is.False);
				Assert.That(await source.AllAsync(x => x != 42), Is.True);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x == 42)), Is.False);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x != 42)), Is.True);
			}
			{
				var source = AsyncQuery.Range(0, 10, this.Cancellation);
				Assert.That(await source.AllAsync(x => x == 0), Is.False);
				Assert.That(await source.AllAsync(x => x == 42), Is.False);
				Assert.That(await source.AllAsync(x => x != 42), Is.True);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x == 42)), Is.False);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x != 42)), Is.True);
			}
			{
				var source = FakeAsyncLinqIterator([ "hello", "world" ]);
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x == "hello")), Is.False);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x != "other")), Is.True);
			}
			{
				var source = FakeAsyncQuery([ "hello", "world" ]);
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x == "hello")), Is.False);
				Assert.That(await source.AllAsync((x, _) => Task.FromResult(x != "other")), Is.True);
			}
			{
				var source = Enumerable.Range(0, 2).Select(x => x == 0 ? "hello" : "world").ToAsyncQuery(this.Cancellation);
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
			}
			{
				var source = FakeAsyncLinqIterator(["hello", "world"]);
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
			}
			{
				var source = FakeAsyncQuery(["hello", "world"]);
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
			}
			{
				var source = FakeAsyncLinqIterator([ 0, 1 ]).Select(x => x == 0 ? "hello" : "world");
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
			}
			{
				var source = FakeAsyncQuery([ 0, 1 ]).Select(x => x == 0 ? "hello" : "world");
				Assert.That(await source.AllAsync(x => x == "hello"), Is.False);
				Assert.That(await source.AllAsync(x => x == "other"), Is.False);
				Assert.That(await source.AllAsync(x => x != "other"), Is.True);
			}
		}

		[Test]
		public async Task Test_Can_Count()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(await source.CountAsync(), Is.EqualTo(0));
				Assert.That(await source.CountAsync(x => x % 2 == 1), Is.EqualTo(0));
				Assert.That(await source.CountAsync(x => x == 42), Is.EqualTo(0));
				Assert.That(await source.CountAsync((x, _) => Task.FromResult(x % 2 == 1)), Is.EqualTo(0));
				Assert.That(await source.CountAsync((x, _) => Task.FromResult(x == 42)), Is.EqualTo(0));
			}
			{
				var source = AsyncQuery.Range(0, 10, this.Cancellation);
				Assert.That(await source.CountAsync(), Is.EqualTo(10));
				Assert.That(await source.CountAsync(x => x % 2 == 1), Is.EqualTo(5));
				Assert.That(await source.CountAsync(x => x == 42), Is.EqualTo(0));
			}
			{
				var source = ((string[]) [ "hello", "world" ]).ToAsyncQuery(this.Cancellation);
				Assert.That(await source.CountAsync(), Is.EqualTo(2));
				Assert.That(await source.CountAsync(x => x == "hello"), Is.EqualTo(1));
				Assert.That(await source.CountAsync(x => x == "world"), Is.EqualTo(1));
				Assert.That(await source.CountAsync(x => x != "other"), Is.EqualTo(2));
				Assert.That(await source.CountAsync(x => x == "other"), Is.EqualTo(0));
			}
			{
				var source = FakeAsyncLinqIterator([ "hello", "world" ]);
				Assert.That(await source.CountAsync(), Is.EqualTo(2));
				Assert.That(await source.CountAsync(x => x == "hello"), Is.EqualTo(1));
				Assert.That(await source.CountAsync(x => x == "world"), Is.EqualTo(1));
				Assert.That(await source.CountAsync(x => x != "other"), Is.EqualTo(2));
				Assert.That(await source.CountAsync(x => x == "other"), Is.EqualTo(0));
				Assert.That(await source.CountAsync((x, _) => Task.FromResult(x == "hello")), Is.EqualTo(1));
				Assert.That(await source.CountAsync((x, _) => Task.FromResult(x != "other")), Is.EqualTo(2));
			}
			{
				var source = FakeAsyncQuery([ "hello", "world" ]);
				Assert.That(await source.CountAsync(), Is.EqualTo(2));
				Assert.That(await source.CountAsync(x => x == "hello"), Is.EqualTo(1));
				Assert.That(await source.CountAsync(x => x == "world"), Is.EqualTo(1));
				Assert.That(await source.CountAsync(x => x != "other"), Is.EqualTo(2));
				Assert.That(await source.CountAsync(x => x == "other"), Is.EqualTo(0));
				Assert.That(await source.CountAsync((x, _) => Task.FromResult(x == "hello")), Is.EqualTo(1));
				Assert.That(await source.CountAsync((x, _) => Task.FromResult(x != "other")), Is.EqualTo(2));
			}
		}

		[Test]
		public async Task Test_Can_Min()
		{
			{
				// empty should fail for value types
				var source = FakeAsyncLinqIterator<int>([]);
				Assert.That(async () => await source.MinAsync(), Throws.InstanceOf<InvalidOperationException>());
			}
			{
				// empty should return null for ref types
				var source = FakeAsyncLinqIterator<string>([]);
				Assert.That(async () => await source.MinAsync(), Is.Null);
			}
			{
				// empty should return null for nullable types
				var source = FakeAsyncLinqIterator<int?>([]);
				Assert.That(async () => await source.MinAsync(), Is.Null);
			}
			{
				var rnd = new Random(1234);
				var items = Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToList();

				var source = items.ToAsyncQuery(this.Cancellation);
				int min = await source.MinAsync();
				Assert.That(min, Is.EqualTo(items.Min()));

				// if min is the first
				items[0] = min - 1;
				source = items.ToAsyncQuery(this.Cancellation);
				min = await source.MinAsync();
				Assert.That(min, Is.EqualTo(items.Min()));

				// if min is the last
				items[^1] = min - 1;
				source = items.ToAsyncQuery(this.Cancellation);
				min = await source.MinAsync();
				Assert.That(min, Is.EqualTo(items.Min()));
			}
			{
				var source = FakeAsyncLinqIterator([ 3, 4, 1, 2 ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(1));
			}
			{
				var source = FakeAsyncLinqIterator([ 3L, 4L, 1L, 2L ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(1L));
			}
			{
				var source = FakeAsyncLinqIterator([ 3.0, 4.0, 1.0, 2.0 ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(1.0));
			}
			{
				var source = FakeAsyncLinqIterator([ 3.0f, 4.0f, 1.0f, 2.0f ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(1.0f));
			}
			{
				var source = FakeAsyncLinqIterator([ 3.0m, 4.0m, 1.0m, 2.0m ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(1.0m));
			}
			{
				var source = FakeAsyncLinqIterator([ 3UL, 4UL, 1UL, 2UL ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(1UL));
			}
			{
				var source = FakeAsyncLinqIterator([ "once", "upon", "a", "time" ]);
				Assert.That(await source.MinAsync(), Is.EqualTo("a"));
			}
			{
				var source = FakeAsyncLinqIterator([ JsonNumber.Return(3), JsonNumber.Return(4), JsonNumber.Return(1), JsonNumber.Return(2) ]);
				Assert.That(await source.MinAsync(), Is.EqualTo(JsonNumber.Return(1)));
			}
		}

		[Test]
		public async Task Test_Can_Max()
		{
			{
				// empty should fail for value types
				var source = FakeAsyncLinqIterator<int>([]);
				Assert.That(async () => await source.MaxAsync(), Throws.InstanceOf<InvalidOperationException>());
			}
			{
				// empty should return null for ref types
				var source = FakeAsyncLinqIterator<string>([]);
				Assert.That(async () => await source.MaxAsync(), Is.Null);
			}
			{
				// empty should return null for nullable types
				var source = FakeAsyncLinqIterator<int?>([]);
				Assert.That(async () => await source.MaxAsync(), Is.Null);
			}
			{
				var rnd = new Random(1234);
				var items = Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToList();

				var source = items.ToAsyncQuery(this.Cancellation);
				int max = await source.MaxAsync();
				Assert.That(max, Is.EqualTo(items.Max()));

				// if max is the first
				items[0] = max + 1;
				source = items.ToAsyncQuery(this.Cancellation);
				max = await source.MaxAsync();
				Assert.That(max, Is.EqualTo(items.Max()));

				// if max is the last
				items[^1] = max + 1;
				source = items.ToAsyncQuery(this.Cancellation);
				max = await source.MaxAsync();
				Assert.That(max, Is.EqualTo(items.Max()));
			}
			{
				var source = FakeAsyncLinqIterator([ 3, 4, 1, 2 ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(4));
			}
			{
				var source = FakeAsyncLinqIterator([ 3L, 4L, 1L, 2L ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(4L));
			}
			{
				var source = FakeAsyncLinqIterator([ 3.0, 4.0, 1.0, 2.0 ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(4.0));
			}
			{
				var source = FakeAsyncLinqIterator([ 3.0f, 4.0f, 1.0f, 2.0f ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(4.0f));
			}
			{
				var source = FakeAsyncLinqIterator([ 3.0m, 4.0m, 1.0m, 2.0m ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(4.0m));
			}
			{
				var source = FakeAsyncLinqIterator([ 3UL, 4UL, 1UL, 2UL ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(4UL));
			}
			{
				var source = FakeAsyncLinqIterator([ "once", "upon", "a", "time" ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo("upon"));
			}
			{
				var source = FakeAsyncLinqIterator([ JsonNumber.Return(3), JsonNumber.Return(4), JsonNumber.Return(1), JsonNumber.Return(2) ]);
				Assert.That(await source.MaxAsync(), Is.EqualTo(JsonNumber.Return(4)));
			}
		}

		[Test]
		public async Task Test_Can_Sum()
		{
			var rnd = new Random(1234);

			{ // int
				Assert.That(await AsyncQuery.Empty<int>().SumAsync(), Is.EqualTo(0));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.Next(0, 1000)).ToArray();
				long expected = items.Sum();

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<int>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<int>(), Is.EqualTo(expected));
			}

			{ // int?
				Assert.That(await AsyncQuery.Empty<int?>().SumAsync(), Is.EqualTo(0));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() < 0.5 ? rnd.Next(0, 1000) : default(int?)).ToArray();
				int? expected = items.Sum().GetValueOrDefault();

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<int>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<int>(), Is.EqualTo(expected));
			}

			{ // long
				Assert.That(await AsyncQuery.Empty<long>().SumAsync(), Is.EqualTo(0L));

				var items = Enumerable.Range(0, 100).Select(_ => (long) rnd.Next(0, 1000)).ToArray();
				long expected = items.Sum();

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<long>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<long>(), Is.EqualTo(expected));
			}
			{ // long?
				Assert.That(await AsyncQuery.Empty<long?>().SumAsync(), Is.EqualTo(0L));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() < 0.5 ? (long?) rnd.Next(0, 1000) : null).ToArray();
				long expected = items.Sum(x => x ?? 0);

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<long>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<long>(), Is.EqualTo(expected));
			}

			{ // float
				Assert.That(await AsyncQuery.Empty<float>().SumAsync(), Is.EqualTo(0f));

				var items = Enumerable.Range(0, 100).Select(_ => (float) rnd.NextDouble() * 1000).ToArray();
				float expected = items.Sum();
				//note: Sum(IEnumerable<float>) uses a double accumulator internally, and the sum will be slightly different from using a float as accumulator!

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<float>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<float>(), Is.EqualTo(expected));
			}

			{ // float?
				Assert.That(await AsyncQuery.Empty<float?>().SumAsync(), Is.EqualTo(0f));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() < 0.5 ? (float?) (rnd.NextDouble() * 1000) : null).ToArray();
				float expected = items.Sum(x => x ?? 0f);
				//note: Sum(IEnumerable<float>) uses a double accumulator internally, and the sum will be slightly different from using a float as accumulator!

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<float>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<float>(), Is.EqualTo(expected));
			}

			{ // double
				Assert.That(await AsyncQuery.Empty<double>().SumAsync(), Is.EqualTo(0d));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() * 1000).ToArray();
				double expected = items.Sum();

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<double>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<double>(), Is.EqualTo(expected));
			}
			{ // double?
				Assert.That(await AsyncQuery.Empty<double?>().SumAsync(), Is.EqualTo(0d));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() < 0.5 ? (double?) (rnd.NextDouble() * 1000) : null).ToArray();
				double expected = items.Sum(x => x ?? 0d);

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<double>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<double>(), Is.EqualTo(expected));
			}

			{ // decimal
				Assert.That(await AsyncQuery.Empty<decimal>().SumAsync(), Is.EqualTo(0d));

				var items = Enumerable.Range(0, 100).Select(_ => (decimal) (rnd.NextDouble() * 1000)).ToArray();
				decimal expected = items.Sum();

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<decimal>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<decimal>(), Is.EqualTo(expected));
			}
			{ // decimal?
				Assert.That(await AsyncQuery.Empty<decimal?>().SumAsync(), Is.EqualTo(0d));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() < 0.5 ? (decimal?) (rnd.NextDouble() * 1000) : null).ToArray();
				decimal expected = items.Sum(x => x ?? 0m);

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncLinqIterator(items).SumAsync<decimal>(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync<decimal>(), Is.EqualTo(expected));
			}

			{ // ulong (generic math)
				Assert.That(await AsyncQuery.Empty<ulong>().SumAsync(), Is.EqualTo(0UL));

				var items = Enumerable.Range(0, 100).Select(_ => (ulong) rnd.Next(0, 1000)).ToArray();
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + x);

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
			}
			{ // ulong? (generic math)
				Assert.That(await AsyncQuery.Empty<ulong?>().SumAsync(), Is.EqualTo(0UL));

				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() < 0.5 ? (ulong?) rnd.Next(0, 1000) : null).ToArray();
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + (x ?? 0));

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
			}

			{ // JsonNumber (generic math)
				Assert.That(await AsyncQuery.Empty<JsonNumber>().SumAsync(), Is.SameAs(JsonNumber.Zero));

				var items = Enumerable.Range(0, 100).Select(_ => JsonNumber.Return(rnd.Next(0, 1000))).ToArray();
				long expected = 0;
				foreach (var x in items) expected += x.ToInt64();

				Assert.That(await FakeAsyncLinqIterator(items).SumAsync(), Is.EqualTo(expected));
				Assert.That(await FakeAsyncQuery(items).SumAsync(), Is.EqualTo(expected));
			}

			// overflow detection

			Assert.That(async () => await FakeAsyncQuery([ int.MaxValue, int.MaxValue ]).SumAsync(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await FakeAsyncQuery([ long.MaxValue, long.MaxValue ]).SumAsync(), Throws.InstanceOf<OverflowException>());

			Assert.That(async () => await FakeAsyncQuery([ int.MaxValue, int.MaxValue ]).SumAsync<int>(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await FakeAsyncQuery([ uint.MaxValue, uint.MaxValue ]).SumAsync<uint>(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await FakeAsyncQuery([ long.MaxValue, long.MaxValue ]).SumAsync<long>(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await FakeAsyncQuery([ ulong.MaxValue, ulong.MaxValue ]).SumAsync<ulong>(), Throws.InstanceOf<OverflowException>());
		}

		[Test]
		public async Task Test_Can_Sum_Selector()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(idx => new
			{
				Index = idx,
				Integer = rnd.Next(0, 1000),
				Decimal = rnd.NextDouble(),
				Json = JsonNumber.Create(rnd.Next(0, 1000)),
			}).ToList();

			// Summing on integers is special cased to return a 'long', instead of overflowing
			// (a common case is to sum other the size (in bytes) of documents, files, etc... where 2GB would be too small)

			{ // int
				var source = items.ToAsyncQuery(this.Cancellation);
				long sum = await source.Select(x => x.Integer).SumAsync();
				long expected = 0;
				foreach (var x in items) expected = checked(expected + x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			// The others are generic over INumberBase

			{ // uint
				var source = items.ToAsyncQuery(this.Cancellation);
				uint sum = await source.Select(x => (uint) x.Integer).SumAsync();
				uint expected = 0;
				foreach (var x in items) expected = checked(expected + (uint) x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // long
				var source = items.ToAsyncQuery(this.Cancellation);
				long sum = await source.Select(x => (long) x.Integer).SumAsync();
				long expected = 0;
				foreach (var x in items) expected = checked(expected + x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // ulong
				var source = items.ToAsyncQuery(this.Cancellation);
				ulong sum = await source.Select(x => (ulong) x.Integer).SumAsync();
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + (ulong) x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // float
				var source = items.ToAsyncQuery(this.Cancellation);
				float sum = await source.Select(x => (float) x.Decimal).SumAsync();
				float expected = items.Select(x => (float) x.Decimal).Sum();
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // double
				var source = items.ToAsyncQuery(this.Cancellation);
				double sum = await source.Select(x => x.Decimal).SumAsync();
				double expected = 0f;
				foreach (var x in items) expected += x.Decimal;
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // custom INumber<T> implementation
				var source = items.ToAsyncQuery(this.Cancellation);
				JsonNumber sum = await source.Select(x => x.Json).SumAsync();
				long expected = 0;
				foreach (var x in items) expected += x.Json.ToInt64();
				Assert.That(sum, Is.EqualTo(expected));
			}

			// overflow detection
			Assert.That(async () => await FakeAsyncQuery([ "FOO", "BAR" ]).Select(_ => long.MaxValue).SumAsync(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await FakeAsyncQuery([ "FOO", "BAR" ]).Select(_ => uint.MaxValue).SumAsync(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await FakeAsyncQuery([ "FOO", "BAR" ]).Select(_ => ulong.MaxValue).SumAsync(), Throws.InstanceOf<OverflowException>());
		}

		[Test]
		public async Task Test_Can_Aggregate()
		{
			var rnd = new Random(1234);

			{ // Func<TAgg, TSource, TAgg>

				// empty
				Assert.That(await AsyncQuery.Empty<int>().AggregateAsync(0L, (agg, x) => -1), Is.EqualTo(0));

				// single
				Assert.That(await FakeAsyncLinqIterator(Enumerable.Range(42, 1)).AggregateAsync(0L, (agg, x) => agg + x), Is.EqualTo(42));
				Assert.That(await FakeAsyncQuery(Enumerable.Range(42, 1)).AggregateAsync(0L, (agg, x) => agg + x), Is.EqualTo(42));

				// many
				Assert.That(await FakeAsyncLinqIterator(Enumerable.Range(0, 10)).AggregateAsync(0L, (agg, x) => agg + x), Is.EqualTo(45));
				Assert.That(await FakeAsyncQuery(Enumerable.Range(0, 10)).AggregateAsync(0L, (agg, x) => agg + x), Is.EqualTo(45));
			}

			{ // Action<TAcc, TSource>
				var buffer = new List<int>();
				await AsyncQuery.Empty<int>().AggregateAsync(buffer, (xs, x) => xs.Add(x));
				Assert.That(buffer, Is.Empty);

				var items = Enumerable.Range(0, 10).ToArray();

				buffer.Clear();
				await FakeAsyncLinqIterator(items).AggregateAsync(buffer, (xs, x) => xs.Add(x));
				Assert.That(buffer, Is.EqualTo(items));

				buffer.Clear();
				await FakeAsyncQuery(items).AggregateAsync(buffer, (xs, x) => xs.Add(x));
				Assert.That(buffer, Is.EqualTo(items));
			}

			{ // Func<TAgg, TSource, TAgg> + Func<TAgg, TResult>

				// empty
				Assert.That(
					await AsyncQuery.Empty<int>().AggregateAsync(0L, (acc, x) => acc + x, (acc) => acc + 100),
					Is.EqualTo(100)
				);

				// single
				Assert.That(
					await FakeAsyncLinqIterator(Enumerable.Range(42, 1)).AggregateAsync((Sum: 0L, Count: 0), (agg, x) => (agg.Sum + x, agg.Count + 1), (agg) => (double) agg.Sum / agg.Count),
					Is.EqualTo(42.0)
				);
				Assert.That(
					await FakeAsyncQuery(Enumerable.Range(42, 1)).AggregateAsync((Sum: 0L, Count: 0), (agg, x) => (agg.Sum + x, agg.Count + 1), (agg) => (double) agg.Sum / agg.Count),
					Is.EqualTo(42.0)
				);

				// many
				Assert.That(
					await FakeAsyncLinqIterator(Enumerable.Range(0, 10)).AggregateAsync((Sum: 0L, Count: 0), (agg, x) => (agg.Sum + x, agg.Count + 1), (agg) => (double) agg.Sum / agg.Count),
					Is.EqualTo(4.5)
				);
				Assert.That(
					await FakeAsyncQuery(Enumerable.Range(0, 10)).AggregateAsync((Sum: 0L, Count: 0), (agg, x) => (agg.Sum + x, agg.Count + 1), (agg) => (double) agg.Sum / agg.Count),
					Is.EqualTo(4.5)
				);
			}

			{ // Action<TAcc, TSource> + Func<TAcc, TResult>

				// empty
				Assert.That(
					await AsyncQuery.Empty<int>().AggregateAsync(new List<int>(), (xs, x) => xs.Add(x), (xs) => xs.Count),
					Is.EqualTo(0)
				);

				// single
				Assert.That(
					await FakeAsyncLinqIterator(Enumerable.Range(42, 1)).AggregateAsync(new List<int>(), (xs, x) => xs.Add(x), (xs) => xs.Sum()),
					Is.EqualTo(42)
				);
				Assert.That(
					await FakeAsyncQuery(Enumerable.Range(42, 1)).AggregateAsync(new List<int>(), (xs, x) => xs.Add(x), (xs) => xs.Sum()),
					Is.EqualTo(42)
				);

				// many
				Assert.That(
					await FakeAsyncLinqIterator(Enumerable.Range(0, 10)).AggregateAsync(new List<int>(), (xs, x) => xs.Add(x), (xs) => xs.Sum()),
					Is.EqualTo(45)
				);
				Assert.That(
					await FakeAsyncQuery(Enumerable.Range(0, 10)).AggregateAsync(new List<int>(), (xs, x) => xs.Add(x), (xs) => xs.Sum()),
					Is.EqualTo(45)
				);
			}

		}

		[Test]
		public async Task Test_Can_OrderBy()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToList();

			var source = items.ToAsyncQuery(this.Cancellation);

			var query = source.OrderBy((x) => x);
			Assert.That(query, Is.Not.Null);
			var res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(items.OrderBy((x) => x).ToList()));

			query = source.OrderByDescending((x) => x);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(items.OrderByDescending((x) => x).ToList()));
		}

		[Test]
		public async Task Test_Can_OrderBy_With_Custom_Comparer()
		{
			var items = new[] { "c", "B", "a", "D" };

			var source = items.ToAsyncQuery(this.Cancellation);

			// ordinal should put upper before lower
			var query = source.OrderBy((x) => x, StringComparer.Ordinal);
			Assert.That(query, Is.Not.Null);
			var res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(new [] { "B", "D", "a", "c" }));

			// ordinal ingore case should mixe upper and lower
			query = source.OrderBy((x) => x, StringComparer.OrdinalIgnoreCase);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(new[] { "a", "B", "c", "D" }));
		}

		[Test]
		public async Task Test_Can_ThenBy()
		{
			var rnd = new Random(1234);
			var pairs = Enumerable.Range(0, 100).Select(_ => new KeyValuePair<int, int>(rnd.Next(10), rnd.Next())).ToList();
			var source = pairs.ToAsyncQuery(this.Cancellation);

			var query = source.OrderBy(kvp => kvp.Key).ThenBy(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			var res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderBy(kvp => kvp.Key).ThenBy(kvp => kvp.Value).ToList()));

			query = source.OrderBy(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderBy(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value).ToList()));

			query = source.OrderByDescending(kvp => kvp.Key).ThenBy(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderByDescending(kvp => kvp.Key).ThenBy(kvp => kvp.Value).ToList()));

			query = source.OrderByDescending(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderByDescending(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value).ToList()));
		}

		[Test]
		public async Task Test_Can_Batch()
		{
			var items = Enumerable.Range(0, 100).ToList();

			var source = items.ToAsyncQuery(this.Cancellation);

			// evenly divided

			var query = source.Batch(20);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(5));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 20).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(20, 20).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(40, 20).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(60, 20).ToArray()));
			Assert.That(results[4], Is.EqualTo(Enumerable.Range(80, 20).ToArray()));

			// unevenly divided

			query = source.Batch(32);
			Assert.That(query, Is.Not.Null);

			results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(4));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 32).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(32, 32).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(64, 32).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(96, 4).ToArray()));

			// empty

			query = AsyncQuery.Empty<int>().Batch(20);
			Assert.That(query, Is.Not.Null);

			results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Empty);
		}

		[Test]
		public async Task Test_Can_Window()
		{

			// generate a source that stalls every 13 items, from 0 to 49

			var source = new AnonymousAsyncGenerator<int>((index, ct) =>
			{
				if (index >= 50) return Task.FromResult(Maybe.Nothing<int>());
				if (index % 13 == 0) return Task.Delay(100, ct).ContinueWith((_) => Maybe.Return((int)index));
				return Task.FromResult(Maybe.Return((int)index));
			}, this.Cancellation);

			// window size larger than sequence period

			var query = source.Window(20);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(4));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 13).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(13, 13).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(26, 13).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(39, 11).ToArray()));

			// window size smaller than sequence period

			query = source.Window(10);
			Assert.That(query, Is.Not.Null);

			results = await query.ToListAsync();
			//REVIEW: right now the Window operator will produce small windows at the end of a period which may not be the most efficient...
			//TODO: optimize the implementation to try to squeeze out small buffers with only a couple items, and update this unit test!
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(8));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(10, 3).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(13, 10).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(23, 3).ToArray()));
			Assert.That(results[4], Is.EqualTo(Enumerable.Range(26, 10).ToArray()));
			Assert.That(results[5], Is.EqualTo(Enumerable.Range(36, 3).ToArray()));
			Assert.That(results[6], Is.EqualTo(Enumerable.Range(39, 10).ToArray()));
			Assert.That(results[7], Is.EqualTo(Enumerable.Range(49, 1).ToArray()));
		}

		[Test]
		public async Task Test_Can_Prefetch_On_Constant_Latency_Source()
		{
			int called = 0;
			var sw = new Stopwatch();

			Log("CONSTANT LATENCY GENERATOR:");

			// this iterator waits on each item produced
			var source = new AnonymousAsyncGenerator<int>((index, ct) =>
			{
				Interlocked.Increment(ref called);
				if (index >= 10) return Task.FromResult(Maybe.Nothing<int>());
				return Task.Delay(15, ct).ContinueWith((_) => Maybe.Return((int)index));
			}, this.Cancellation);

			var results = await source.ToListAsync();
			Assert.That(results, Is.Not.Null);
			Assert.That(results, Is.EqualTo(Enumerable.Range(0, 10).ToList()));

			// record the timing and call history to ensure that inner is called at least twice before the first item gets out

			Func<int, (int Value, int Called)> record = (x) => (x, Volatile.Read(ref called));

			// without pre-fetching, the number of calls should match for the producer and the consumer
			called = 0;
			sw.Restart();
			var withoutPrefetching = await source.Select(record).ToListAsync();
			Log($"P0: {string.Join(", ", withoutPrefetching)}");
			Assert.That(withoutPrefetching.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withoutPrefetching.Select(x => x.Called), Is.EqualTo(Enumerable.Range(1, 10)));

			// with pre-fetching, the consumer should always have one item in advance
			called = 0;
			sw.Restart();
			var withPrefetching1 = await source.Prefetch().Select(record).ToListAsync();
			Log($"P1: {string.Join(", ", withPrefetching1)}");
			Assert.That(withPrefetching1.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withPrefetching1.Select(x => x.Called), Is.EqualTo(Enumerable.Range(2, 10)));

			// pre-fetching more than 1 item on a consumer that is not buffered should not change the picture (since we can only read one ahead anyway)
			//REVIEW: maybe we should change the implementation of the operator so that it still prefetch items in the background if the rest of the query is lagging a bit?
			called = 0;
			sw.Restart();
			var withPrefetching2 = await source.Prefetch(2).Select(record).ToListAsync();
			Log($"P2: {string.Join(", ", withPrefetching2)}");
			Assert.That(withPrefetching2.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withPrefetching2.Select(x => x.Called), Is.EqualTo(Enumerable.Range(2, 10)));
		}

		[Test]
		public async Task Test_Can_Prefetch_On_Bursty_Source()
		{
			int called = 0;
			var sw = new Stopwatch();

			Log("BURSTY GENERATOR:");

			// this iterator produce burst of items
			var source = new AnonymousAsyncGenerator<int>((index, ct) =>
			{
				Interlocked.Increment(ref called);
				if (index >= 10) return Task.FromResult(Maybe.Nothing<int>());
				if (index % 4 == 0) return Task.Delay(50, ct).ContinueWith((_) => Maybe.Return((int)index));
				return Task.FromResult(Maybe.Return((int)index));
			}, this.Cancellation);

			(int Value, int Called, TimeSpan Elapsed) Record(int x)
			{
				var res = (x, Volatile.Read(ref called), sw.Elapsed);
				sw.Restart();
				return res;
			}

			// without pre-fetching, the number of calls should match for the producer and the consumer
			called = 0;
			sw.Restart();
			var withoutPrefetching = await source.Select(Record).ToListAsync();
			Log($"P0: {string.Join(", ", withoutPrefetching)}");
			Assert.That(withoutPrefetching.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));

			// with pre-fetching K, the consumer should always have K items in advance
			//REVIEW: maybe we should change the implementation of the operator so that it still prefetch items in the background if the rest of the query is lagging a bit?
			for (int K = 1; K <= 4; K++)
			{
				called = 0;
				sw.Restart();
				var withPrefetchingK = await source.Prefetch(K).Select(Record).ToListAsync();
				Log($"P{K}: {string.Join(", ", withPrefetchingK)}");
				Assert.That(withPrefetchingK.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
				Assert.That(withPrefetchingK[0].Called, Is.EqualTo(K + 1), $"Generator must have {K} call(s) in advance!");
				Assert.That(withPrefetchingK.Select(x => x.Called), Is.All.LessThanOrEqualTo(11));
			}

			// if pre-fetching more than the period of the producer, we should not have any perf gain
			called = 0;
			sw.Restart();
			var withPrefetching5 = await source.Prefetch(5).Select(Record).ToListAsync();
			Log($"P5: {string.Join(", ", withPrefetching5)}");
			Assert.That(withPrefetching5.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withPrefetching5[0].Called, Is.EqualTo(5), "Generator must have only 4 calls in advance because it only produces 4 items at a time!");
			Assert.That(withPrefetching5.Select(x => x.Called), Is.All.LessThanOrEqualTo(11));
		}

		[Test]
		public async Task Test_Can_Select_Anonymous_Types()
		{
			var source = AsyncQuery.Range(0, 10, this.Cancellation);

			var results = await source
				.Select((x) => new { Value = x, Square = x * x, Root = Math.Sqrt(x), Odd = x % 2 == 1 })
				.Where((t) => t.Odd)
				.ToListAsync();

			Assert.That(results, Is.Not.Null);
			Assert.That(results.Count, Is.EqualTo(5));

			Assert.That(results[0].Value, Is.EqualTo(1));
			Assert.That(results[0].Square, Is.EqualTo(1));
			Assert.That(results[0].Root, Is.EqualTo(1.0));
			Assert.That(results[0].Odd, Is.True);
		}

		[Test]
		public async Task Test_Can_Select_With_LINQ_Syntax()
		{
			// ensure that we can also use the "from ... select ... where" syntax

			var results = await
				(from x in AsyncQuery.Range(0, 10, this.Cancellation)
				let t = new { Value = x, Square = x * x, Root = Math.Sqrt(x), Odd = x % 2 == 1 }
				where t.Odd
				select t)
				.ToListAsync();

			Assert.That(results.Count, Is.EqualTo(5));
			Assert.That(results.Select(t => t.Value).ToArray(), Is.EqualTo(new [] { 1, 3, 5, 7, 9 }));
			Assert.That(results.Select(t => t.Square).ToArray(), Is.EqualTo(new [] { 1, 9, 25, 49, 81 }));
			Assert.That(results.Select(t => t.Value).ToArray(), Is.EqualTo(new [] { 1.0, 3.0, 5.0, 7.0, 9.0 }));
			Assert.That(results.All(t => t.Odd), Is.True);
		}

		[Test]
		public async Task Test_Can_SelectMany_With_LINQ_Syntax()
		{

			var results = await
			(from x in AsyncQuery.Range(0, 10, this.Cancellation)
			 from y in Enumerable.Repeat((char)(65 + x), x)
			 let t = new { X = x, Y = y, Odd = x % 2 == 1 }
			 where t.Odd
			 select t)
			 .ToListAsync();

			Assert.That(results.Count, Is.EqualTo(25));
			Assert.That(results.Select(t => t.X).ToArray(), Is.EqualTo(new [] { 1, 3, 3, 3, 5, 5, 5, 5, 5, 7, 7, 7, 7, 7, 7, 7, 9, 9, 9, 9, 9, 9, 9, 9, 9 }));
			Assert.That(results.Select(t => t.Y).ToArray(), Is.EqualTo(new [] { 'B', 'D', 'D', 'D', 'F', 'F', 'F', 'F', 'F', 'H', 'H', 'H', 'H', 'H', 'H', 'H', 'J', 'J', 'J', 'J', 'J', 'J', 'J', 'J', 'J' }));
			Assert.That(results.All(t => t.Odd), Is.True);
		}

		[Test]
		public async Task Test_Exceptions_Are_Propagated_To_Caller()
		{
			var query = AsyncQuery
				.Range(0, 10, this.Cancellation)
				.Select(x =>
				{
					if (x == 1) throw new FormatException("KABOOM");
					Assert.That(x, Is.LessThan(1));
					return x;
				});

			await using var iterator = query.GetAsyncEnumerator();
			// first move next should succeed
			bool res = await iterator.MoveNextAsync();
			Assert.That(res, Is.True);

			// second move next should fail
			Assert.That(async () => await iterator.MoveNextAsync(), Throws.InstanceOf<FormatException>().With.Message.EqualTo("KABOOM"), "Should have failed");

			// accessing current should rethrow the exception
			Assert.That(() => iterator.Current, Throws.InstanceOf<InvalidOperationException>());

			// another attempt at MoveNext should fail immediately but with a different error
			Assert.That(async () => await iterator.MoveNextAsync(), Throws.InstanceOf<ObjectDisposedException>());
		}

		[Test]
		public async Task Test_Parallel_Select_Async()
		{
			const int MAX_CONCURRENCY = 5;
			const int N = 20;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				int concurrent = 0;
				var rnd = new Random();

				var sw = Stopwatch.StartNew();

				var query = Enumerable.Range(1, N)
					.ToAsyncQuery(token)
					.Select(async (x, ct) =>
					{
						int ms;
						lock (rnd) {  ms = 10 + rnd.Next(25); }
						await Task.Delay(ms, ct);
						return x;
					})
					.SelectParallel(async (x, ct) =>
					{
						try
						{
							var n = Interlocked.Increment(ref concurrent);
							try
							{
								Assert.That(n, Is.LessThanOrEqualTo(MAX_CONCURRENCY));
								Log($"** {sw.Elapsed} start {x} ({n})");
#if DEBUG_STACK_TRACES
								Log("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
								int ms;
								lock (rnd) { ms = rnd.Next(25) + 50; }
								await Task.Delay(ms, ct);
								Log($"** {sw.Elapsed} stop {x} ({Volatile.Read(ref concurrent)})");

								return x * x;
							}
							finally
							{
								n = Interlocked.Decrement(ref concurrent);
								Assert.That(n, Is.GreaterThanOrEqualTo(0));
							}
						}
						catch(Exception e)
						{
							Console.Error.WriteLine($"Thread #{x} failed: {e}");
							throw;
						}
					},
					new() { MaxConcurrency = MAX_CONCURRENCY }
				);

				var results = await query.ToListAsync();

				Assert.That(Volatile.Read(ref concurrent), Is.EqualTo(0));
				Log("Results: " + string.Join(", ", results));
				Assert.That(results, Is.EqualTo(Enumerable.Range(1, N).Select(x => x * x).ToArray()));
			}

		}

		[Test]
		public async Task Test_AsyncBuffer()
		{
			const int MAX_CAPACITY = 5;

			var buffer = new AsyncTransformQueue<int, int>((x, _) => Task.FromResult(x * x), MAX_CAPACITY, TaskScheduler.Default);

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var rnd = new Random(1234);

				// setup a pump loop
				var pump = Task.Run(async () =>
				{
					while (!token.IsCancellationRequested)
					{
						Log("[consumer] start receiving next...");
						var msg = await buffer.ReceiveAsync(token);
#if DEBUG_STACK_TRACES
						Log("[consumer] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[consumer] > "));
#endif
						if (msg.HasValue)
						{
							Log($"[consumer] Got value {msg.Value}");
						}
						else if (msg.HasValue)
						{
							Log($"[consumer] Got error: {msg.Error}");
							msg.ThrowForNonSuccess();
							break;
						}
						else
						{
							Log("[consumer] Done!");
							break;
						}

					}

				}, token);

				int i = 0;

				// first 5 calls to enqueue should already be completed
				while (!token.IsCancellationRequested && i < MAX_CAPACITY * 10)
				{
					Log("[PRODUCER] Publishing " + i);
#if DEBUG_STACK_TRACES
					Log("[PRODUCER] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[PRODUCER] > "));
#endif
					await buffer.OnNextAsync(i, token);
					++i;
					Log("[PRODUCER] Published");
#if DEBUG_STACK_TRACES
					Log("[PRODUCER] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[PRODUCER] > "));
#endif

					if (rnd.Next(10) < 2)
					{
						Log("[PRODUCER] Thinking " + i);
						await Task.Delay(10, this.Cancellation);
					}
				}

				Log("[PRODUCER] COMPLETED!");
				buffer.OnCompleted();

				var t = await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(10), token));
				Assert.That(t, Is.SameAs(pump));

			}
		}

		private static async Task VerifyResult<T>(Func<Task<T>> asyncQuery, Func<T> referenceQuery, IQueryable<T> witness, string label)
		{
			Exception? asyncError = null;
			Exception? referenceError = null;

			T? asyncResult = default;
			T? referenceResult = default;

			try { asyncResult = await asyncQuery().ConfigureAwait(false); }
			catch (Exception e) { asyncError = e; }

			try { referenceResult = referenceQuery(); }
			catch (Exception e) { referenceError = e; }

			if (asyncError != null)
			{
				if (referenceError == null) Assert.Fail($"{label}(): The async query failed but not the reference query {witness.Expression} : {asyncError}");
				//TODO: compare exception types ?
			}
			else if (referenceError != null)
			{
				Assert.Fail($"{label}(): The reference query {witness.Expression} failed ({referenceError.Message}) but the async query returned: {asyncResult}");
			}
			else
			{
				try
				{
					Assert.That(asyncResult, Is.EqualTo(referenceResult), $"{label}(): {witness.Expression}");
				}
				catch(AssertionException x)
				{
					Log($"FAIL: {witness.Expression}\r\n >  {x.Message}");
				}
			}

		}

		private static async Task VerifySequence<T, TSeq>(Func<Task<TSeq>> asyncQuery, Func<TSeq> referenceQuery, IQueryable<T> witness, string label)
			where TSeq : class, IEnumerable<T>
		{
			Exception? asyncError = null;
			Exception? referenceError = null;

			TSeq? asyncResult = null;
			TSeq? referenceResult = null;

			try { asyncResult = await asyncQuery().ConfigureAwait(false); }
			catch (Exception e) { asyncError = e; }

			try { referenceResult = referenceQuery(); }
			catch (Exception e) { referenceError = e; }

			if (asyncError != null)
			{
				if (referenceError == null) Assert.Fail($"{label}(): The async query failed but not there reference query {witness.Expression} : {asyncError}");
				//TODO: compare exception types ?
			}
			else if (referenceError != null)
			{
				Assert.Fail($"{label}(): The reference query {witness.Expression} failed ({referenceError.Message}) but the async query returned: {asyncResult}");
			}
			else
			{
				try
				{
					Assert.That(asyncResult, Is.EqualTo(referenceResult), $"{label}(): {witness.Expression}");
				}
				catch (AssertionException x)
				{
					Log("FAIL: " + witness.Expression + "\r\n >  " + x.Message);
				}
			}

		}

		[Test]
		public async Task Test_AsyncLinq_vs_LinqToObject()
		{
			// Construct a random async query in parallel with the equivalent LINQ-to-Object and ensure that they produce the same result

			// Then we call each of these methods: Count(), ToList(), ToArray(), First(), FirstOrDefault(), Single(), SingleOrDefault(), All(), None(), Any()
			// * if they produce a result, it must match
			// * if one fail, the other must also fail with an equivalent error (ex: Single() if the collection as more than one).

			// note: we will also create a third LINQ query using lambda expressions, just to be able to have a nicer ToString() in case of errors

			int[] sourceOfInts = [ 1, 7, 42, -456, 123, int.MaxValue, -1, 1023, 0, short.MinValue, 5, 13, -273, 2013, 4534, -999 ];

			const int N = 1000;

			var rnd = new Random(); // new Random(1234)

			for(int i=0;i<N;i++)
			{

				var query = sourceOfInts.ToAsyncQuery(this.Cancellation);
				IEnumerable<int> reference = sourceOfInts;
				IQueryable<int> witness = sourceOfInts.AsQueryable();

				// optional where
				switch (rnd.Next(6))
				{
					case 0:
					{ // keep about 50% of the items
						query = query.Where(x => x > 0);
						reference = reference.Where(x => x > 0);
						witness = witness.Where(x => x > 0);
						break;
					}
					case 1:
					{ // keep no items at all
						query = query.Where(_ => false);
						reference = reference.Where(_ => false);
						witness = witness.Where(x => false);
						break;
					}
					case 2:
					{ // keep only one item
						query = query.Where(x => x == 42);
						reference = reference.Where(x => x == 42);
						witness = witness.Where(x => x == 42);
						break;
					}
				}

				// optional transform (keep the type)
				if (rnd.Next(5) == 0)
				{
					query = query.Select(x => x >> 1);
					reference = reference.Select(x => x >> 1);
					witness = witness.Select(x => x >> 1);
				}

				// optional transform that change the type (and back)
				if (rnd.Next(3) == 0)
				{
					var sq = query.Select(x => x.ToString());
					var sr = reference.Select(x => x.ToString());
					var sw = witness.Select(x => x.ToString());

					// optional where
					if (rnd.Next(2) == 0)
					{
						sq = sq.Where(s => s.Length <= 2);
						sr = sr.Where(s => s.Length <= 2);
						sw = sw.Where(s => s.Length <= 2);
					}

					// convert back
					query = sq.Select(x => int.Parse(x));
					reference = sr.Select(x => int.Parse(x));
					witness = sw.Select(x => int.Parse(x));
				}

				// optional Skip
#if false // TODO !
				switch (rnd.Next(10))
				{
					case 0:
					{ // skip a few
						query = query.Skip(3);
						reference = reference.Skip(3);
						witness = witness.Skip(3);
						break;
					}
					case 1:
					{ // only take 1 
						query = query.Skip(1);
						reference = reference.Skip(1);
						witness = witness.Skip(1);
						break;
					}
				}
#endif

				// optional Take
				switch(rnd.Next(10))
				{
					case 0:
					{ // only take a few
						query = query.Take(3);
						reference = reference.Take(3);
						witness = witness.Take(3);
						break;
					}
					case 1:
					{ // only take 1
						query = query.Take(1);
						reference = reference.Take(1);
						witness = witness.Take(1);
						break;
					}
				}

				// => ensure that results are coherent

				await VerifyResult(
					() => query.CountAsync(),
					() => reference.Count(),
					witness,
					"Count"
				);

				await VerifySequence(
					() => query.ToListAsync(),
					() => reference.ToList(),
					witness,
					"ToList"
				);

				await VerifySequence(
					() => query.ToArrayAsync(),
					() => reference.ToArray(),
					witness,
					"ToArray"
				);

				await VerifyResult(
					() => query.FirstAsync(),
					() => reference.First(),
					witness,
					"First"
				);

				await VerifyResult(
					() => query.FirstOrDefaultAsync(),
					() => reference.FirstOrDefault(),
					witness,
					"FirstOrDefault"
				);

				await VerifyResult(
					() => query.SingleAsync(),
					() => reference.Single(),
					witness,
					"Single"
				);

				await VerifyResult(
					() => query.SingleOrDefaultAsync(),
					() => reference.SingleOrDefault(),
					witness,
					"SingleOrDefault"
				);

				await VerifyResult(
					() => query.LastAsync(),
					() => reference.Last(),
					witness,
					"Last"
				);

				await VerifyResult(
					() => query.LastOrDefaultAsync(),
					() => reference.LastOrDefault(),
					witness,
					"LastOrDefault"
				);

			}

		}

		[Test]
		public async Task Test_Record_Items()
		{
			var items = Enumerable.Range(0, 10);
			var source = items.ToAsyncQuery(this.Cancellation);

			var before = new List<int>();
			var after = new List<int>();

			var query = source
				.Observe((x) => before.Add(x))
				.Where((x) => x % 2 == 1)
				.Observe((x) => after.Add(x))
				.Select((x) => x + 1);

			Log("query: " + query);

			var results = await query.ToListAsync();

			Log($"input : {string.Join(", ", items)}");
			Log($"before: {string.Join(", ", before)}");
			Log($"after : {string.Join(", ", after)}");
			Log($"output: {string.Join(", ", results)}");

			Assert.That(before, Is.EqualTo(Enumerable.Range(0, 10).ToList()));
			Assert.That(after, Is.EqualTo(Enumerable.Range(0, 10).Where(x => x % 2 == 1).ToList()));
			Assert.That(results, Is.EqualTo(Enumerable.Range(1, 5).Select(x => x * 2).ToList()));
		}

		#region .NET 10+ AsyncEnumerable integration...

#if NET10_0_OR_GREATER

		[Test]
		public async Task Test_ToAsyncEnumerable()
		{
			{
				var source = AsyncQuery.Empty<int>();
				Assert.That(source.ToAsyncEnumerable(), Is.Not.Null);
				Assert.That(await source.ToAsyncEnumerable().CountAsync(), Is.Zero);
				Assert.That(await source.ToAsyncEnumerable().AnyAsync(), Is.False);
				Assert.That(await source.ToAsyncEnumerable().ToArrayAsync(), Is.Empty);
				Assert.That(await source.ToAsyncEnumerable().ToListAsync(), Is.Empty);
			}
			{
				var source = FakeAsyncLinqIterator([ 42, 43, 44 ]);
				Assert.That(source.ToAsyncEnumerable(), Is.Not.Null);

				Assert.That(await source.ToAsyncEnumerable().CountAsync(), Is.EqualTo(3));
				Assert.That(await source.ToAsyncEnumerable().AnyAsync(), Is.True);
				Assert.That(await source.ToAsyncEnumerable().ToArrayAsync(), Is.EqualTo((int[]) [ 42, 43, 44 ]));

				Assert.That(await source.ToAsyncEnumerable().Select(x => x + 1).ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await source.ToAsyncEnumerable().Select((int x, CancellationToken _) => ValueTask.FromResult(x + 1)).ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await source.ToAsyncEnumerable().Where(x => x % 2 == 1).ToArrayAsync(), Is.EqualTo((int[]) [ 43 ]));
				Assert.That(await source.ToAsyncEnumerable().Where((int x, CancellationToken _) => ValueTask.FromResult(x % 2 == 1)).ToArrayAsync(), Is.EqualTo((int[]) [ 43 ]));
			}
			{
				var source = FakeAsyncQuery([ 42, 43, 44 ]);
				Assert.That(source.ToAsyncEnumerable(), Is.Not.Null);

				Assert.That(await source.ToAsyncEnumerable().CountAsync(), Is.EqualTo(3));
				Assert.That(await source.ToAsyncEnumerable().AnyAsync(), Is.True);
				Assert.That(await source.ToAsyncEnumerable().ToArrayAsync(), Is.EqualTo((int[]) [ 42, 43, 44 ]));

				Assert.That(await source.ToAsyncEnumerable().Select(x => x + 1).ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await source.ToAsyncEnumerable().Select((int x, CancellationToken _) => ValueTask.FromResult(x + 1)).ToArrayAsync(), Is.EqualTo((int[]) [ 43, 44, 45 ]));
				Assert.That(await source.ToAsyncEnumerable().Where(x => x % 2 == 1).ToArrayAsync(), Is.EqualTo((int[]) [ 43 ]));
				Assert.That(await source.ToAsyncEnumerable().Where((int x, CancellationToken _) => ValueTask.FromResult(x % 2 == 1)).ToArrayAsync(), Is.EqualTo((int[]) [ 43 ]));
			}
		}

#endif

		#endregion

	}

}
