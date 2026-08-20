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

namespace FoundationDB.Client
{
	using System.Collections.Immutable;
	using System.ComponentModel;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}, Limit={Limit}, Mode={Streaming}, Reverse={Reverse}, Snapshot={IsSnapshot}")]
	[PublicAPI]
	internal class FdbRangeQuery<TState, TResult> : IFdbRangeQuery<TResult>, IAsyncEnumerable<TResult>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbRangeQuery(IFdbReadOnlyTransaction transaction, KeySelector begin, KeySelector end, TState? state, Func<TState>? stateFactory, FdbKeyValueDecoder<TState, TResult> decoder, bool snapshot, FdbRangeOptions? options)
		{
			Contract.Debug.Requires(transaction != null && decoder != null);

			this.Transaction = transaction;
			this.Begin = begin;
			this.End = end;
			this.State = state;
			this.StateFactory = stateFactory;
			this.Decoder = decoder;
			this.IsSnapshot = snapshot;
			this.Options = options ?? new FdbRangeOptions();
			this.OriginalRange = KeySelectorPair.Create(begin, end);
		}

		/// <summary>Copy constructor</summary>
		private FdbRangeQuery(FdbRangeQuery<TState, TResult> query, FdbRangeOptions options)
		{
			Contract.Debug.Requires(query != null && options != null);

			this.Transaction = query.Transaction;
			this.Begin = query.Begin;
			this.End = query.End;
			this.State = query.State;
			this.StateFactory = query.StateFactory;
			this.Decoder = query.Decoder;
			this.IsSnapshot = query.IsSnapshot;
			this.Options = options;
			this.OriginalRange = query.OriginalRange;
		}

		#region Public Properties...

		/// <summary>Key selector describing the beginning of the range that will be queried</summary>
		public KeySelector Begin { get; private set; }

		/// <summary>Key selector describing the end of the range that will be queried</summary>
		public KeySelector End { get; private set; }

		/// <summary>Key selector pair describing the beginning and end of the range that will be queried</summary>
		public KeySelectorPair Range => new(this.Begin, this.End);

		/// <summary>Stores all the settings for this range query</summary>
		public FdbRangeOptions Options { get; }

		/// <summary>Original key selector pair describing the bounds of the parent range. All the results returned by the query will be bounded by this original range.</summary>
		/// <remarks>May differ from <see cref="Range"/> when combining certain operators.</remarks>
		internal KeySelectorPair OriginalRange { get; }

		/// <summary>Limit in number of rows to return</summary>
		public int? Limit => this.Options.Limit;

		/// <summary>Limit in number of bytes to return</summary>
		public int? TargetBytes => this.Options.TargetBytes;

		/// <summary>Streaming mode to enable or disable prefetching</summary>
		/// <remarks>
		/// <para>This setting changes how many items will be returned in the first batch but the storage servers</para>
		/// <para>Queries that will return all the data in the range should use <see cref="FdbStreamingMode.WantAll"/>, while queries that will only take a fraction should use <see cref="FdbStreamingMode.Iterator"/></para>
		/// </remarks>
		public FdbStreamingMode Streaming => this.Options.Streaming ?? FdbStreamingMode.Iterator;

		/// <summary>Read mode</summary>
		/// <remarks>
		/// <para>Queries that need to decode both keys and values should use <see cref="FdbFetchMode.KeysAndValues"/>, while queries that need only one or the other should use either <see cref="FdbFetchMode.KeysOnly"/> or <see cref="FdbFetchMode.ValuesOnly"/></para>
		/// </remarks>
		public FdbFetchMode Fetch => this.Options.Fetch ?? FdbFetchMode.KeysAndValues;

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool IsSnapshot { get; }

		/// <summary>Should the results be returned in reverse order (from last key to first key)</summary>
		public bool IsReversed => this.Options.IsReversed;

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		public IFdbReadOnlyTransaction Transaction { get; }

		internal TState? State { get; }

		internal Func<TState>? StateFactory { get; }

		/// <summary>Transformation applied to the result</summary>
		internal FdbKeyValueDecoder<TState, TResult> Decoder { get; }

		#endregion

		#region Fluent API

		public IAsyncEnumerable<TResult> ToAsyncEnumerable(AsyncIterationHint hint = AsyncIterationHint.Default)
		{
			// if the hint is compatible with the streaming mode, we can return the query unchanged; otherwise, we have to change the streaming mode in the options to match the hint.
			return hint switch
			{
				AsyncIterationHint.Default => this,
				AsyncIterationHint.All => this.Streaming == FdbStreamingMode.WantAll ? this : new(this, this.Options with { Streaming = FdbStreamingMode.WantAll }),
				AsyncIterationHint.Iterator => this.Streaming == FdbStreamingMode.Iterator ? this : new(this, this.Options with { Streaming = FdbStreamingMode.WantAll }),
				AsyncIterationHint.Head => this.Streaming == FdbStreamingMode.Exact ? this : new(this, this.Options with { Streaming = FdbStreamingMode.Exact }),
				_ => throw new NotSupportedException("Unsupported async iteration mode")
			};
		}

		IFdbPagedQuery<TResult> IFdbRangeQuery<TResult>.Paged() => this.Paged();

		/// <inheritdoc cref="IFdbRangeQuery{TResult}.Paged"/>
		public IFdbPagedQuery<TResult> Paged() => new FdbPagedQuery<TState, TResult>(
			this.Transaction,
			this.Begin,
			this.End,
			this.State,
			this.StateFactory,
			this.Decoder,
			this.IsSnapshot,
			this.Options
		);

		IFdbRangeQuery<TResult> IFdbRangeQuery<TResult>.Reverse() => Reverse();

		/// <inheritdoc cref="IFdbRangeQuery{TResult}.Reverse"/>
		[Pure]
		public FdbRangeQuery<TState, TResult> Reverse()
		{
			var begin = this.Begin;
			var end = this.End;
			var limit = this.Options.Limit;
			if (limit.HasValue)
			{
				// If Take() of Skip() have been called, we need to update the end bound when reversing (or begin if already reversed)
				if (!this.IsReversed)
				{
					end = this.Begin + limit.Value;
				}
				else
				{
					begin = this.End - limit.Value;
				}
			}

			return new(
				this,
				this.Options with { IsReversed = !this.IsReversed }
			)
			{
				Begin = begin,
				End = end,
			};
		}

		/// <summary>Use a specific target bytes size</summary>
		/// <param name="bytes"></param>
		/// <returns>A new query object that will use the specified target bytes size when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> WithTargetBytes([Positive] int bytes)
		{
			Contract.Positive(bytes);

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { TargetBytes = bytes }
			);
		}

		/// <summary>Use a different Streaming Mode</summary>
		/// <param name="mode">Streaming mode to use when reading the results from the database</param>
		/// <returns>A new query object that will use the specified streaming mode when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> WithMode(FdbStreamingMode mode)
		{
			if (!Enum.IsDefined(typeof(FdbStreamingMode), mode))
			{
				throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported streaming mode");
			}

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { Streaming = mode }
			);
		}

		/// <summary>Force the query to use a specific transaction</summary>
		/// <param name="transaction">Transaction to use when executing this query</param>
		/// <returns>A new query object that will use the specified transaction when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> UseTransaction(IFdbReadOnlyTransaction transaction)
		{
			Contract.NotNull(transaction);

			return new FdbRangeQuery<TState, TResult>(
				transaction,
				this.Begin,
				this.End,
				this.State,
				this.StateFactory,
				this.Decoder,
				this.IsSnapshot,
				this.Options with { }
			);
		}

		#endregion

		#region Pseudo-LINQ

		//note: these methods are more optimized than regular AsyncLINQ methods, in that they can customize the query settings to return the least data possible over the network.
		// ex: FirstOrDefault can set the Limit to 1, LastOrDefault can set Reverse to true, etc...

		public CancellationToken Cancellation => this.Transaction.Cancellation;

		[MustDisposeResource]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct = default)
		{
			// We assume that the most frequent caller of this "alternate" entry point will be "await foreach" which wants to scan everything,
			// and that "regular" LINQ usage will go through the IAsyncLinqQuery<T> interface, which calls the GetAsyncEnumerator(AsyncIterationHint) overload.
			return AsyncQuery.GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);
		}

		[MustDisposeResource]
		public IAsyncEnumerator<TResult> GetAsyncEnumerator(AsyncIterationHint hint)
			=> new FdbResultIterator<TState, TResult>(this, GetState()).GetAsyncEnumerator(hint);

		[MustDisposeResource]
		public IAsyncEnumerator<ReadOnlyMemory<TResult>> GetPagedAsyncIterator(AsyncIterationHint hint)
			=> new FdbPagedIterator<TState, TResult>(this, this.OriginalRange, GetState(), this.Decoder).GetAsyncEnumerator(hint);

		#region To{Collection}Async()...

		/// <summary>Returns a list of all the elements of the range results</summary>
		public async Task<List<TResult>> ToListAsync()
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buffer.AddRange(iterator.Current.Span);
			}

			return buffer.ToListAndClear();
		}

		/// <summary>Returns an array with all the elements of the range results</summary>
		public async Task<TResult[]> ToArrayAsync()
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buffer.AddRange(iterator.Current.Span);
			}

			return buffer.ToArrayAndClear();
		}

		/// <summary>Returns an array with all the elements of the range results</summary>
		public async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buffer.AddRange(iterator.Current.Span);
			}

			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer = null)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var res = new HashSet<TResult>(comparer);
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					res.Add(item);
				}
			}
			return res;
		}

		/// <inheritdoc />
		public async Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var res = new Dictionary<TKey, TResult>(comparer);
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					res.Add(keySelector(item), item);
				}
			}
			return res;
		}

		/// <inheritdoc />
		public async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TResult, TKey> keySelector, Func<TResult, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var res = new Dictionary<TKey, TElement>(comparer);
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					res.Add(keySelector(item), elementSelector(item));
				}
			}
			return res;
		}

		#endregion

		#region CountAsync...

		/// <summary>Returns the number of elements in the range, by reading them</summary>
		/// <remarks>This method has to read all the keys and values, which may exceed the lifetime of a transaction. Please consider using <see cref="Fdb.System.EstimateCountAsync(FoundationDB.Client.IFdbDatabase,FoundationDB.Client.KeyRange,System.Threading.CancellationToken)"/> when reading potentially large ranges.</remarks>
		public async Task<int> CountAsync()
		{
			//TODO: OPTIMIZE: there are ways to count the keys in a range without reading their values!

			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			int count = 0;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				count = checked(count + iterator.Current.Length);
			}
			return count;
		}

		/// <inheritdoc />
		public async Task<int> CountAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			int count = 0;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (predicate(item))
					{
						count = checked(count + 1);
					}
				}
			}
			return count;
		}

		/// <inheritdoc />
		public async Task<int> CountAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			int count = 0;
			var ct = this.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for(int i = 0; i < batch.Length; i++)
				{
					if (await predicate(batch.Span[i], ct).ConfigureAwait(false))
					{
						count = checked(count + 1);
					}
				}
			}
			return count;
		}

		#endregion

		#region AnyAsync...

		/// <summary>Returns <see langword="true"/> if the range query yields at least one element, or <see langword="false"/> if there was no result.</summary>
		public Task<bool> AnyAsync()
		{
			// Optimized code path for Any, where we can be smart and only ask for 1 from the db

			// we can use the EXACT streaming mode with Limit = 1, and it will work if TargetBytes is 0
			if ((this.TargetBytes ?? 0) != 0 || (this.Streaming != FdbStreamingMode.Iterator && this.Streaming != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				return ImplSlow();
			}

			return ImplFast();

			async Task<bool> ImplSlow()
			{
				await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Head);

				return await iterator.MoveNextAsync().ConfigureAwait(false) && iterator.Current.Length > 0;
			}

			async Task<bool> ImplFast()
			{
				var tr = this.IsSnapshot ? this.Transaction.Snapshot : this.Transaction;

				//BUGBUG: do we need special handling if OriginalRange != Range ? (weird combinations of Take/Skip and Reverse)
				var results = await tr.GetRangeAsync(
					this.Begin,
					this.End,
					new() { Limit = 1, IsReversed = this.IsReversed, Streaming = FdbStreamingMode.Exact, },
					iteration: 0
				).ConfigureAwait(false);

				return !results.IsEmpty;
			}
		}

		/// <inheritdoc />
		public async Task<bool> AnyAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (predicate(item))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <inheritdoc />
		public async Task<bool> AnyAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for(int i = 0; i < batch.Length; i++)
				{
					if (await predicate(batch.Span[i], ct).ConfigureAwait(false))
					{
						return true;
					}
				}
			}
			return false;
		}

		#endregion

		#region AllAsync...

		/// <inheritdoc />
		public async Task<bool> AllAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (!predicate(item))
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <inheritdoc />
		public async Task<bool> AllAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			var ct = this.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for(int i = 0; i < batch.Length; i++)
				{
					if (!(await predicate(batch.Span[i], ct).ConfigureAwait(false)))
					{
						return false;
					}
				}
			}
			return true;
		}

		#endregion

		#region FirstOrDefaultAsync...

#if !NET5_0_OR_GREATER
		// on this target, IAsyncLinqQuery<T> cannot supply these overloads as default interface members (they need runtime support that netstandard2.0/NetFX lacks), so they are implemented here instead

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync() => FirstOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync(Func<TResult, bool> predicate) => FirstOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => FirstOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync() => LastOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync(Func<TResult, bool> predicate) => LastOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => LastOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync() => SingleOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync(Func<TResult, bool> predicate) => SingleOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => SingleOrDefaultAsync(predicate, default(TResult)!)!;

#endif

		/// <inheritdoc />
		public Task<TResult> FirstOrDefaultAsync(TResult defaultValue)
		{
			return HeadAsync(single: false, orDefault: true, defaultValue);
		}

		/// <inheritdoc />
		public async Task<TResult> FirstOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (!predicate(item))
					{
						return item;
					}
				}
			}
			return defaultValue;
		}

		/// <inheritdoc />
		public async Task<TResult> FirstOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for (int i = 0; i < batch.Length; i++)
				{
					if (!(await predicate(batch.Span[i], ct).ConfigureAwait(false)))
					{
						return batch.Span[i];
					}
				}
			}
			return defaultValue;

		}

		#endregion

		#region FirstAsync...

		/// <summary>Returns the first result of the query, or an exception if the query yields no result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields no result</exception>
		public Task<TResult> FirstAsync()
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: false, default!);
		}

		/// <inheritdoc />
		public async Task<TResult> FirstAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (!predicate(item))
					{
						return item;
					}
				}
			}

			throw ErrorRangeHasNotMatch();
		}

		/// <inheritdoc />
		public async Task<TResult> FirstAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for (int i = 0; i < batch.Length; i++)
				{
					if (!(await predicate(batch.Span[i], ct).ConfigureAwait(false)))
					{
						return batch.Span[i];
					}
				}
			}

			throw ErrorRangeHasNotMatch();
		}

		#endregion

		#region LastOrDefaultAsync...

		/// <summary>Returns the last result of the query, or the default for this type if the query yields no results.</summary>
		public Task<TResult> LastOrDefaultAsync(TResult defaultValue)
		{
			//BUGBUG: if there is a Take(N) on the query, Last() will mean "The Nth key" and not the "last key in the original range".

			// we can optimize by reversing the current query and calling FirstOrDefault !
			return this.Reverse().HeadAsync(single: false, orDefault: true, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult> LastOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			return Reverse().FirstOrDefaultAsync(predicate, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult> LastOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			return Reverse().FirstOrDefaultAsync(predicate, defaultValue);
		}

		#endregion

		#region LastAsync...

		/// <summary>Returns the last result of the query, or an exception if the query yields no result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields no result</exception>
		public Task<TResult> LastAsync()
		{
			//BUGBUG: if there is a Take(N) on the query, Last() will mean "The Nth key" and not the "last key in the original range".

			// we can optimize this by reversing the current query and calling First !
			return this.Reverse().HeadAsync(single: false, orDefault:false, default!);
		}

		/// <inheritdoc />
		public Task<TResult> LastAsync(Func<TResult, bool> predicate)
		{
			return Reverse().FirstAsync(predicate);
		}

		/// <inheritdoc />
		public Task<TResult> LastAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			return Reverse().FirstAsync(predicate);
		}

		#endregion

		#region SingleOrDefaultAsync...

		/// <summary>Returns the only result of the query, the default for this type if the query yields no results, or an exception if it yields two or more results.</summary>
		/// <exception cref="InvalidOperationException">If the query yields two or more results</exception>
		public Task<TResult> SingleOrDefaultAsync(TResult defaultValue)
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: true, defaultValue);
		}

		/// <inheritdoc />
		public async Task<TResult> SingleOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			TResult result = defaultValue;
			bool found = false;

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (!predicate(item))
					{
						if (found) throw ErrorRangeHasMoreThanOneMatch();
						result = item;
						found = true;
					}
				}
			}

			return result;
		}

		/// <inheritdoc />
		public async Task<TResult> SingleOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			TResult result = defaultValue;
			bool found = false;
			var ct = this.Cancellation;

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for (int i = 0; i < batch.Length; i++)
				{
					if (!(await predicate(batch.Span[i], ct).ConfigureAwait(false)))
					{
						if (found) throw ErrorRangeHasMoreThanOneMatch();
						result = batch.Span[i];
						found = true;
					}
				}
			}

			return result;
		}

		#endregion

		#region SingleAsync...

		/// <summary>Returns the only result of the query, or an exception if it yields either zero, or more than one result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields two or more results</exception>
		public Task<TResult> SingleAsync()
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: false, default!);
		}

		/// <inheritdoc />
		public async Task<TResult> SingleAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			TResult result = default!;
			bool found = false;

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					if (!predicate(item))
					{
						if (found) throw ErrorRangeHasMoreThanOneMatch();
						result = item;
						found = true;
					}
				}
			}

			return found ? result : throw ErrorRangeHasNotMatch();
		}

		/// <inheritdoc />
		public async Task<TResult> SingleAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.Iterator);

			TResult result = default!;
			bool found = false;
			var ct = this.Cancellation;

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = iterator.Current;
				for (int i = 0; i < batch.Length; i++)
				{
					if (!(await predicate(batch.Span[i], ct).ConfigureAwait(false)))
					{
						if (found) throw ErrorRangeHasMoreThanOneMatch();
						result = batch.Span[i];
						found = true;
					}
				}
			}

			return found ? result : throw ErrorRangeHasNotMatch();
		}

		#endregion

		[Pure]
		internal FdbRangeQuery<TState, TOther> Map<TOther>(Func<TResult, TOther> lambda)
		{
			Contract.Debug.Requires(lambda != null);

			return new(
				this.Transaction,
				this.Begin,
				this.End,
				this.State,
				this.StateFactory,
				Combine(this.Decoder, lambda),
				this.IsSnapshot,
				this.Options with { }
			);

			static FdbKeyValueDecoder<TState, TOther> Combine(FdbKeyValueDecoder<TState, TResult> decoder, Func<TResult, TOther> transform)
			{
				return (s, k, v) => transform(decoder(s, k, v));
			}
		}
		
		#region MinAsync/MaxAsync...

		/// <inheritdoc />
		public Task<TResult?> MinAsync(IComparer<TResult>? comparer)
		{
			return AsyncIterators.MaxAsync(this, comparer);
		}

		/// <inheritdoc />
		public Task<TResult?> MaxAsync(IComparer<TResult>? comparer)
		{
			return AsyncIterators.MaxAsync(this, comparer);
		}

		#endregion

		#region CountAsync...

		/// <inheritdoc />
		public Task<TResult> SumAsync()
		{
			// checks for the most common mistakes when called on a range with binary keys and/or values.
			if (typeof(TResult) == typeof(KeyValuePair<Slice, Slice>)) throw new InvalidOperationException("Cannot compute the sum of key/value pairs. Please use Select(...) on the query to extract values that can be summed.");
			if (typeof(TResult) == typeof(Slice)) throw new InvalidOperationException("Cannot compute the sum of raw keys or values. Please use Select(...) on the query to extract values that can be summed.");

			return AsyncIterators.SumUnconstrainedAsync(this);
		}

		#endregion

		#region Take...

		/// <inheritdoc />
		IAsyncLinqQuery<TResult> IAsyncLinqQuery<TResult>.Take(int count) => Take(count);

		/// <inheritdoc />
		[Pure]
		public IFdbRangeQuery<TResult> Take([Positive] int count)
		{
			Contract.Positive(count);

			if (this.Options.Limit == count)
			{
				return this;
			}

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { Limit = count }
			);
		}

		/// <inheritdoc />
		IAsyncLinqQuery<TResult> IAsyncLinqQuery<TResult>.Take(Range range) => throw new NotImplementedException();

		/// <inheritdoc />
		IFdbRangeQuery<TResult> IFdbRangeQuery<TResult>.Take(Range range) => throw new NotImplementedException();

		#endregion

		#region TakeWhile...

		/// <inheritdoc />
		IAsyncLinqQuery<TResult> IAsyncLinqQuery<TResult>.TakeWhile(Func<TResult, bool> condition) => TakeWhile(condition);

		/// <inheritdoc />
		public IFdbRangeQuery<TResult> TakeWhile(Func<TResult, bool> condition) => throw new NotImplementedException();

		#endregion

		#region Skip...

		/// <inheritdoc />
		IAsyncLinqQuery<TResult> IAsyncLinqQuery<TResult>.Skip(int count) => Skip(count);

		/// <summary>Bypasses a specified number of elements in a sequence and then returns the remaining elements.</summary>
		/// <param name="count"></param>
		/// <returns>A new query object that will skip the first <paramref name="count"/> results when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> Skip([Positive] int count)
		{
			Contract.Positive(count);

			var limit = this.Options.Limit;
			var begin = this.Begin;
			var end = this.End;

			// Take(N).Skip(k) ?
			if (limit.HasValue)
			{
				// If k >= N, then the result will be empty
				// If k < N, then we need to update the Begin key, and limit accordingly
				if (count >= limit.Value)
				{
					limit = 0; // hopefully this would be optimized at runtime?
				}
				else
				{
					limit -= count;
				}
			}

			if (this.IsReversed)
			{
				end -= count;
			}
			else
			{
				begin += count;
			}

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { Limit = limit }
			)
			{
				Begin = begin,
				End = end,
			};
		}

		#endregion

		#region Select...

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.Select<TNew>(Func<TResult, TNew> selector) => Select(selector);
		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.Select<TNew>(Func<TResult, int, TNew> selector) => Select(selector);
		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector) => Select(selector);
		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.Select<TNew>(Func<TResult, int, CancellationToken, Task<TNew>> selector) => Select(selector);

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="lambda">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[Pure]
		public IFdbRangeQuery<TOther> Select<TOther>(Func<TResult, TOther> lambda)
		{
			Contract.NotNull(lambda);
			return Map<TOther>(lambda);
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="lambda">Function that is invoked with both the element value and its index in the sequence (0-based), and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv, i) => $"#{i}: {kv.Key:K} = {kv.Value:V}")</c></example>
		[Pure]
		public IFdbRangeQuery<TOther> Select<TOther>(Func<TResult, int, TOther> lambda)
		{
			Contract.Debug.Requires(lambda != null);

			return new FdbRangeQuery<TState, TOther>(
				this.Transaction,
				this.Begin,
				this.End,
				this.State,
				this.StateFactory,
				Combine(this.Decoder, lambda),
				this.IsSnapshot,
				this.Options with { }
			);

			static FdbKeyValueDecoder<TState, TOther> Combine(FdbKeyValueDecoder<TState, TResult> transform, Func<TResult, int, TOther> lambda)
			{
				Contract.Debug.Assert(transform != null);
				int counter = 0;
				return (s, k, v) => lambda(transform(s, k, v), counter++);
			}
		}

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector) => throw new NotImplementedException();

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> Select<TNew>(Func<TResult, int, CancellationToken, Task<TNew>> selector) => throw new NotImplementedException();

		#endregion

		#region SelectMany...

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector) => SelectMany(selector);

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> selector) => SelectMany(selector);

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.SelectMany<TNew>(Func<TResult, IAsyncEnumerable<TNew>> selector) => SelectMany(selector);

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.SelectMany<TNew>(Func<TResult, IAsyncQuery<TNew>> selector) => SelectMany(selector);

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector) => SelectMany(collectionSelector, resultSelector);

		IAsyncLinqQuery<TNew> IAsyncLinqQuery<TResult>.SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector) => SelectMany(collectionSelector, resultSelector);
		/// <inheritdoc />
		public IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector) => throw new NotImplementedException();

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> selector) => throw new NotImplementedException();

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncEnumerable<TNew>> selector) => throw new NotImplementedException();

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncQuery<TNew>> selector) => throw new NotImplementedException();

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector) => throw new NotImplementedException();

		/// <inheritdoc />
		public IFdbRangeQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector) => throw new NotImplementedException();

		#endregion

		#region Where...

		/// <inheritdoc />
		[Pure, LinqTunnel]
		public IAsyncLinqQuery<TResult> Where(Func<TResult, bool> predicate)
		{
			// never tunnel through the AsyncQuery.Where extension here: it dispatches back to this method for any
			// IAsyncLinqQuery source
			return AsyncIterators.Where(this, predicate);
		}

		/// <inheritdoc />
		[Pure, LinqTunnel]
		public IAsyncLinqQuery<TResult> Where(Func<TResult, int, bool> predicate) => throw new NotImplementedException();

		/// <inheritdoc />
		[Pure, LinqTunnel]
		public IAsyncLinqQuery<TResult> Where(Func<TResult, CancellationToken, Task<bool>> predicate) => throw new NotImplementedException();

		/// <inheritdoc />
		[Pure, LinqTunnel]
		public IAsyncLinqQuery<TResult> Where(Func<TResult, int, CancellationToken, Task<bool>> predicate) => throw new NotImplementedException();

		#endregion

		/// <summary>Executes an action on each of the range results</summary>
		public async Task ForEachAsync(Action<TResult> action)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					action(item);
				}
			}
		}

		/// <summary>Executes an action on each of the range results</summary>
		public async Task<TAggregate> ForEachAsync<TAggregate>(TAggregate aggregate, Action<TAggregate, TResult> action)
		{
			await using var iterator = GetPagedAsyncIterator(AsyncIterationHint.All);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				foreach (var item in iterator.Current.Span)
				{
					action(aggregate, item);
				}
			}

			return aggregate;
		}

		protected TState GetState() => this.StateFactory != null ? this.StateFactory() : this.State!;

		internal async Task<TResult> HeadAsync(bool single, bool orDefault, TResult defaultValue)
		{
			// Optimized code path for First/Last/Single variants where we can be smart and only ask for 1 or 2 results from the db

			// we can use the EXACT streaming mode with Limit = 1|2, and it will work if TargetBytes is 0
			if ((this.TargetBytes ?? 0) != 0 || (this.Streaming != FdbStreamingMode.Iterator && this.Streaming != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				return await AsyncQuery.Head(this, single, orDefault, defaultValue).ConfigureAwait(false);
			}

			//BUGBUG: do we need special handling if OriginalRange != Range ? (weird combinations of Take/Skip and Reverse)

			var tr = this.IsSnapshot ? this.Transaction.Snapshot : this.Transaction;

			var results = await tr.GetRangeAsync<TState, TResult>(
				this.Begin,
				this.End,
				GetState(),
				this.Decoder,
				new FdbRangeOptions()
				{
					Limit = Math.Min(single ? 2 : 1, this.Options.Limit ?? int.MaxValue),
					IsReversed = this.IsReversed,
					Streaming = FdbStreamingMode.Exact,
				},
				iteration: 0
			).ConfigureAwait(false);

			if (results.IsEmpty)
			{
				// no result
				return orDefault
					? defaultValue
					: throw ErrorRangeIsEmpty();
			}

			if (single && results.Count > 1)
			{ // there was more than one result
				throw ErrorRangeHasMoreThanOneElement();
			}

			// we have a result
			return results[0];
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ErrorRangeIsEmpty() => new("The range was empty.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ErrorRangeHasMoreThanOneElement() => new("The range contained more than one element.");

		private static InvalidOperationException ErrorRangeHasNotMatch() => new("The range has not matching elements.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ErrorRangeHasMoreThanOneMatch() => new("The range contained more than one matching element.");

		#endregion

		/// <summary>Returns a human-readable representation of this query</summary>
		public override string ToString()
		{
			return $"Range({this.Range}, {this.Limit}, {(this.IsReversed ? "reverse" : "forward")})";
		}

	}
}
