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

namespace FoundationDB.Layers.Indexing
{

	/// <summary>Simple index that maps values of type <typeparamref name="TValue"/> into lists of ids of type <typeparamref name="TId"/></summary>
	/// <typeparam name="TId">Type of the unique id of each document or entity</typeparam>
	/// <typeparam name="TValue">Type of the value being indexed</typeparam>
	[DebuggerDisplay("Location={Location}, IndexNullValues={IndexNullValues})")]
	[PublicAPI]
	public class FdbIndex<TId, TValue> : IFdbLayer<FdbIndex<TId, TValue>.State>
	{

		public FdbIndex(ISubspaceLocation location, IEqualityComparer<TValue>? valueComparer = null, bool indexNullValues = false)
		{
			Contract.NotNull(location);
			this.Location = location;
			this.ValueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			this.IndexNullValues = indexNullValues;
		}

		public ISubspaceLocation Location { get; }

		public IEqualityComparer<TValue> ValueComparer { get; }

		/// <summary>If true, null values are inserted in the index. If false (default), they are ignored</summary>
		/// <remarks>This has no effect if <typeparamref name="TValue" /> is not a reference type</remarks>
		public bool IndexNullValues { get; }

		/// <summary>Represent the state of this index relative to the context of a single transaction</summary>
		public class State
		{
			/// <summary>Schema of the index</summary>
			public FdbIndex<TId, TValue> Schema { get; }

			/// <summary>Resolved subspace containing the index</summary>
			public IKeySubspace Subspace { get; }

			public State(FdbIndex<TId, TValue> schema, IKeySubspace subspace)
			{
				this.Schema = schema;
				this.Subspace = subspace;
			}

			public bool Add(IFdbTransaction trans, TId id, TValue? value)
			{
				if (this.Schema.IndexNullValues || value is not null)
				{
					trans.Set(this.Subspace.Key(value, id), Slice.Empty);
					return true;
				}
				return false;
			}

			/// <summary>Update the indexed values of an entity</summary>
			public bool Update(IFdbTransaction trans, TId id, TValue? newValue, TValue? previousValue)
			{
				if (!this.Schema.ValueComparer.Equals(newValue, previousValue))
				{
					// remove previous value
					if (this.Schema.IndexNullValues || previousValue is not null)
					{
						trans.Clear(this.Subspace.Key(previousValue, id));
					}

					// add new value
					if (this.Schema.IndexNullValues || newValue is not null)
					{
						trans.Set(this.Subspace.Key(newValue, id), Slice.Empty);
					}

					// cannot be both null, so we did at least something
					return true;
				}
				return false;
			}

			/// <summary>Remove an entity from the index</summary>
			public void Remove(IFdbTransaction trans, TId id, TValue? value)
			{
				trans.Clear(this.Subspace.Key(value, id));
			}

			/// <summary>Returns a query that will return all id of the entities that have the specified <paramref name="value"/></summary>
			public IFdbRangeQuery<TId> Lookup(IFdbReadOnlyTransaction trans, TValue? value, bool reverse = false)
			{
				return trans.GetRangeKeys(
					this.Subspace.Key(value).ToRange(inclusive: true),
					this.Subspace, (s, k) => s.DecodeLast<TId>(k)!,
					reverse ? FdbRangeOptions.Reversed : FdbRangeOptions.Default
				);
			}

			/// <summary>Returns a query that will return all id of the entities that have a value greater than (or equal) a specified <paramref name="value"/></summary>
			public IFdbRangeQuery<TId> LookupGreaterThan(IFdbReadOnlyTransaction trans, TValue value, bool orEqual, bool reverse = false)
			{
				if (orEqual)
				{ // (>= "abc", *) => (..., "abc") < x < (..., <FF>)
					return trans.GetRangeKeys(
						this.Subspace.Key(value),
						this.Subspace.Last(),
						this.Subspace, static (s, k) => s.DecodeLast<TId>(k)!,
						reverse ? FdbRangeOptions.Reversed : FdbRangeOptions.Default
					);
				}
				else
				{ // (> "abc", *) => (..., "abd") < x < (..., <FF>)
					return trans.GetRangeKeys(
						this.Subspace.Key(value).NextSibling(),
						this.Subspace.Last(),
						this.Subspace, static (s, k) => s.DecodeLast<TId>(k)!,
						reverse ? FdbRangeOptions.Reversed : FdbRangeOptions.Default
					);
				}
			}

			/// <summary>Returns a query that will return all id of the entities that have a value lesser than (or equal to) a specified <paramref name="value"/></summary>
			public IFdbRangeQuery<TId> LookupLessThan(IFdbReadOnlyTransaction trans, TValue value, bool orEqual, bool reverse = false)
			{
				IFdbKeyValueRangeQuery query;
				if (orEqual)
				{
					query = trans.GetRange(
						this.Subspace.First(),
						this.Subspace.Key(value).Last(),
						reverse ? FdbRangeOptions.Reversed : FdbRangeOptions.Default
					);
				}
				else
				{
					query = trans.GetRange(
						this.Subspace.First(),
						this.Subspace.Key(value),
						reverse ? FdbRangeOptions.Reversed : FdbRangeOptions.Default
					);
				}

				return query.Select((kvp) => this.Subspace.DecodeLast<TId>(kvp.Key)!);
			}

		}

		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction trans)
		{
			Contract.NotNull(trans);

			//TODO: cache the instance on the transaction!

			var subspace = await this.Location.Resolve(trans);

			return new State(this, subspace);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbIndex<,>);

		/// <summary>Insert a newly created entity to the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the new entity (that was never indexed before)</param>
		/// <param name="value">Value of this entity in the index</param>
		/// <returns>True if a value was inserted into the index; otherwise false (if value is null and <see cref="IndexNullValues"/> is false)</returns>
		public async Task<bool> AddAsync(IFdbTransaction trans, TId id, TValue? value)
		{
			var state = await Resolve(trans);
			return state.Add(trans, id, value);
		}

		/// <summary>Update the indexed values of an entity</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has changed</param>
		/// <param name="newValue">Previous value of this entity in the index</param>
		/// <param name="previousValue">New value of this entity in the index</param>
		/// <returns>True if a change was performed in the index; otherwise false (if <paramref name="previousValue"/> and <paramref name="newValue"/>)</returns>
		/// <remarks>If <paramref name="newValue"/> and <paramref name="previousValue"/> are identical, then nothing will be done. Otherwise, the old index value will be deleted and the new value will be added</remarks>
		public async Task<bool> UpdateAsync(IFdbTransaction trans, TId id, TValue? newValue, TValue? previousValue)
		{
			var state = await Resolve(trans);
			return state.Update(trans, id, newValue, previousValue);
		}

		/// <summary>Remove an entity from the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has been deleted</param>
		/// <param name="value">Previous value of the entity in the index</param>
		public async Task RemoveAsync(IFdbTransaction trans, TId id, TValue? value)
		{
			var state = await Resolve(trans);
			state.Remove(trans, id, value);
		}

		/// <summary>Returns a query that will return all id of the entities that have the specified value in this index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="value">Value to lookup</param>
		/// <param name="reverse">If true, returns the results in reverse identifier order</param>
		/// <returns>List of the ids of entities that match the value</returns>
		public IAsyncQuery<TId> Lookup(IFdbReadOnlyTransaction trans, TValue? value, bool reverse = false)
		{
			Contract.NotNull(trans);
			return AsyncQuery.Defer((Index: this, Trans: trans, Value: value, Reverse: reverse), (args, _) => args.Index.CreateLookupQuery(args.Trans, args.Value, args.Reverse), trans.Cancellation);
		}

		/// <summary>Returns a query that will return all id of the entities that have the specified value in this index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="value">Value to lookup</param>
		/// <param name="reverse">If true, returns the results in reverse identifier order</param>
		/// <returns>Range query that returns all the ids of entities that match the value</returns>
		public async Task<IAsyncLinqQuery<TId>> CreateLookupQuery(IFdbReadOnlyTransaction trans, TValue? value, bool reverse = false)
		{
			var state = await Resolve(trans);
			return state.Lookup(trans, value, reverse);
		}

		public IAsyncLinqQuery<TId> LookupGreaterThan(IFdbReadOnlyTransaction trans, TValue value, bool orEqual, bool reverse = false)
		{
			Contract.NotNull(trans);
			return AsyncQuery.Defer((_) => CreateLookupGreaterThanQuery(trans, value, orEqual, reverse), trans.Cancellation);
		}

		public async Task<IAsyncLinqQuery<TId>> CreateLookupGreaterThanQuery(IFdbReadOnlyTransaction trans, TValue value, bool orEqual, bool reverse = false)
		{
			var state = await Resolve(trans);
			return state.LookupGreaterThan(trans, value, orEqual, reverse);
		}

		public IAsyncLinqQuery<TId> LookupLessThan(IFdbReadOnlyTransaction trans, TValue value, bool orEqual, bool reverse = false)
		{
			Contract.NotNull(trans);
			return AsyncQuery.Defer((_) => CreateLookupLessThanQuery(trans, value, orEqual, reverse), trans.Cancellation);
		}

		public async Task<IAsyncLinqQuery<TId>> CreateLookupLessThanQuery(IFdbReadOnlyTransaction trans, TValue value, bool orEqual, bool reverse = false)
		{
			var state = await Resolve(trans);
			return state.LookupLessThan(trans, value, orEqual, reverse);
		}

		public override string ToString()
		{
			return "Index[" + this.Location + "]";
		}

	}

}
