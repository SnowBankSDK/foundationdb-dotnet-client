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
	using System.ComponentModel;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}, Limit={Limit}, Mode={Streaming}, Reverse={Reverse}, Snapshot={IsSnapshot}")]
	[PublicAPI]
	internal class FdbPagedQuery<TState, TResult> : IFdbPagedQuery<TResult>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbPagedQuery(IFdbReadOnlyTransaction transaction, KeySelector begin, KeySelector end, TState? state, Func<TState>? stateFactory, FdbKeyValueDecoder<TState, TResult> decoder, bool snapshot, FdbRangeOptions? options)
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
		private FdbPagedQuery(FdbPagedQuery<TState, TResult> query, FdbRangeOptions options)
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

		//REVIEW: TODO: there is a lot of duplication with FdbRangeQuery<...>, maybe find a way to refactor this?

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

		#region Fluent API...

		IFdbPagedQuery<TResult> IFdbPagedQuery<TResult>.Reverse() => Reverse();

		/// <inheritdoc cref="IFdbPagedQuery{TResult}.Reverse"/>
		[Pure]
		public FdbPagedQuery<TState, TResult> Reverse()
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
		public IFdbPagedQuery<TResult> WithTargetBytes([Positive] int bytes)
		{
			Contract.Positive(bytes);

			return new FdbPagedQuery<TState, TResult>(
				this,
				this.Options with { TargetBytes = bytes }
			);
		}

		/// <summary>Use a different Streaming Mode</summary>
		/// <param name="mode">Streaming mode to use when reading the results from the database</param>
		/// <returns>A new query object that will use the specified streaming mode when executed</returns>
		[Pure]
		public IFdbPagedQuery<TResult> WithMode(FdbStreamingMode mode)
		{
			if (!Enum.IsDefined(typeof(FdbStreamingMode), mode))
			{
				throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported streaming mode");
			}

			return new FdbPagedQuery<TState, TResult>(
				this,
				this.Options with { Streaming = mode }
			);
		}

		/// <summary>Force the query to use a specific transaction</summary>
		/// <param name="transaction">Transaction to use when executing this query</param>
		/// <returns>A new query object that will use the specified transaction when executed</returns>
		[Pure]
		public IFdbPagedQuery<TResult> UseTransaction(IFdbReadOnlyTransaction transaction)
		{
			Contract.NotNull(transaction);

			return new FdbPagedQuery<TState, TResult>(
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
		
		/// <inheritdoc />
		public IFdbPagedQuery<TOther> Select<TOther>(PageFunc<TResult, TOther> lambda) => throw new NotImplementedException();

		/// <inheritdoc />
		public Task ForEachAsync(PageAction<TResult> handler) => throw new NotImplementedException();

		/// <inheritdoc />
		public async Task<TAggregate> ForEachAsync<TAggregate>(TAggregate aggregate, PageAggregator<TAggregate, TResult> action)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.All);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				action(aggregate, iterator.Current.Span);
			}

			return aggregate;
		}

		/// <inheritdoc />
		public async Task<List<TResult>> ToListAsync()
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.All);

			var buf = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buf.AddRange(iterator.Current.Span);
			}

			return buf.ToListAndClear();
		}

		/// <inheritdoc />
		public async Task<TResult[]> ToArrayAsync()
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.All);

			var buf = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buf.AddRange(iterator.Current.Span);
			}

			return buf.ToArrayAndClear();
		}

		#region Pseudo-LINQ

		public CancellationToken Cancellation => this.Transaction.Cancellation;

		//note: these methods are more optimized than regular AsyncLINQ methods, in that they can customize the query settings to return the least data possible over the network.
		// ex: FirstOrDefault can set the Limit to 1, LastOrDefault can set Reverse to true, etc...

		[MustDisposeResource]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public IAsyncEnumerator<ReadOnlyMemory<TResult>> GetAsyncEnumerator(CancellationToken ct)
			=> AsyncQuery.GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);

		[MustDisposeResource]
		public IAsyncEnumerator<ReadOnlyMemory<TResult>> GetAsyncEnumerator(AsyncIterationHint hint)
		{
			return new FdbPagedIterator<TState, TResult>(this, this.OriginalRange, GetState(), this.Decoder).GetAsyncEnumerator(hint);
		}

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

		protected TState GetState() => this.StateFactory != null ? this.StateFactory() : this.State!;

		#endregion

		/// <summary>Returns a human-readable representation of this query</summary>
		public override string ToString()
		{
			return $"Range({this.Range}, {this.Limit}, {(this.IsReversed ? "reverse" : "forward")}, paged)";
		}

	}

}
