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

namespace FoundationDB.Layers.Collections
{

	/// <summary>Implements a simple order dictionary of keys to values</summary>
	/// <typeparam name="TKey">Type of the keys</typeparam>
	/// <typeparam name="TValue">Type of the values</typeparam>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbMap<TKey, TValue> : FdbMap<TKey, TKey, TValue, FdbRawValue>
	{

		/// <summary>Constructs a map that will encode simple keys, using the specified value codec</summary>
		/// <param name="location">Location in the database where the map will be stored</param>
		/// <param name="valueCodec">Codec used to serialize and deserialize the values</param>
		public FdbMap(ISubspaceLocation location, IFdbValueCodec<TValue, FdbRawValue> valueCodec)
			: base(location, FdbIdentityKeyCodec<TKey>.Instance, valueCodec)
		{ }

		[Obsolete("Please use an IFdbValueCodec instead")]
		public FdbMap(ISubspaceLocation location, IValueEncoder<TValue> valueEncoder)
			: base(location, FdbIdentityKeyCodec<TKey>.Instance, new FdbValueEncoderCodec<TValue>(valueEncoder))
		{ }

	}

	/// <summary>Implements a simple order dictionary of keys to values</summary>
	/// <typeparam name="TKey">Type of the keys</typeparam>
	/// <typeparam name="TValue">Type of the values</typeparam>
	/// <typeparam name="TEncodedValue">Type of the binary representation for the values, that must implement <see cref="ISpanEncodable"/></typeparam>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbMap<TKey, TValue, TEncodedValue> : FdbMap<TKey, TKey, TValue, TEncodedValue>
		where TEncodedValue : struct, ISpanEncodable
	{

		/// <summary>Constructs a map that will encode simple keys, using the specified value codec</summary>
		/// <param name="location">Location in the database where the map will be stored</param>
		/// <param name="valueCodec">Codec used to serialize and deserialize the values</param>
		public FdbMap(ISubspaceLocation location, IFdbValueCodec<TValue, TEncodedValue> valueCodec)
			: base(location, FdbIdentityKeyCodec<TKey>.Instance, valueCodec)
		{ }

	}

	/// <summary>Implements a simple order dictionary of keys to values</summary>
	/// <typeparam name="TKey">Type of the keys</typeparam>
	/// <typeparam name="TEncodedKey">Type of the intermediate representation of keys, which can be different for composite keys.</typeparam>
	/// <typeparam name="TValue">Type of the values</typeparam>
	/// <typeparam name="TEncodedValue">Type of the binary representation for the values, that must implement <see cref="ISpanEncodable"/></typeparam>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbMap<TKey, TEncodedKey, TValue, TEncodedValue> : IFdbLayer<FdbMap<TKey, TEncodedKey, TValue, TEncodedValue>.State>
		where TEncodedValue : struct, ISpanEncodable
	{

		/// <summary>Constructs a map that will encode simple keys, using the specified value codec</summary>
		/// <param name="location">Location in the database where the map will be stored</param>
		/// <param name="keyCodec">Codec used to serialize and deserialize the keys</param>
		/// <param name="valueCodec">Codec used to serialize and deserialize the values</param>
		public FdbMap(ISubspaceLocation location, IFdbKeyCodec<TKey, TEncodedKey> keyCodec, IFdbValueCodec<TValue, TEncodedValue> valueCodec)
		{
			Contract.NotNull(location);
			Contract.NotNull(keyCodec);
			Contract.NotNull(valueCodec);

			this.Location = location;
			this.KeyCodec = keyCodec;
			this.ValueCodec = valueCodec;
		}

		#region Public Properties...

		/// <summary>Subspace used to encode the keys for the items</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>Codec used to serialize/deserializes the keys</summary>
		public IFdbKeyCodec<TKey, TEncodedKey> KeyCodec { get; }

		/// <summary>Codec used to serialize/deserialize the values</summary>
		public IFdbValueCodec<TValue, TEncodedValue> ValueCodec { get; }

		#endregion

		/// <summary>Internal state that can be used inside a transaction context</summary>
		[PublicAPI]
		public sealed class State
		{

			/// <summary>Resolved subspace of the map</summary>
			public IKeySubspace Subspace { get; }

			/// <summary>Map that resolved this state</summary>
			public FdbMap<TKey, TEncodedKey, TValue, TEncodedValue> Parent { get; }

			internal State(IKeySubspace subspace, FdbMap<TKey, TEncodedKey, TValue, TEncodedValue> parent)
			{
				Contract.Debug.Requires(subspace is not null && parent is not null);
				this.Subspace = subspace;
				this.Parent = parent;
			}

			#region Get / Set / Remove...

			/// <summary>Reads the value of an existing entry in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to read from the map</param>
			/// <returns>Value of the entry if it exists; otherwise, throws an exception</returns>
			/// <exception cref="System.ArgumentNullException">If either <paramref name="trans"/> or <paramref name="id"/> is null.</exception>
			/// <exception cref="System.Collections.Generic.KeyNotFoundException">If the map does not contain an entry with this key.</exception>
			public async Task<TValue> GetAsync(IFdbReadOnlyTransaction trans, TKey id)
			{
				Contract.NotNull(trans);
				Contract.NotNull(id);

				var pk = this.Parent.KeyCodec.EncodeKey(id);

				var (data, found) = await trans.GetAsync(
					this.Subspace.Key(pk),
					(value, found) => found ? (this.Parent.ValueCodec.DecodeValue(value), true) : (default, false)
				).ConfigureAwait(false);

				return found ? data! : throw new KeyNotFoundException("The given id was not present in the map.");
			}

			/// <summary>Reads the value of an entry in the map if it exists.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to read from the map</param>
			/// <returns>Optional with the value of the entry if it exists, or an empty result if it is not present in the map.</returns>
			public async Task<(TValue? Value, bool HasValue)> TryGetAsync(IFdbReadOnlyTransaction trans, TKey id)
			{
				Contract.NotNull(trans);
				Contract.NotNull(id);

				var pk = this.Parent.KeyCodec.EncodeKey(id);

				return await trans.GetAsync(
					this.Subspace.Key(pk),
					(value, found) => found ? (this.Parent.ValueCodec.DecodeValue(value), true) : (default, false)
				).ConfigureAwait(false);
			}

			/// <summary>Adds or updates an entry in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to add or update</param>
			/// <param name="value">New value of the entry</param>
			/// <remarks>If the entry did not exist, it will be created. If not, its value will be replaced with <paramref name="value"/>.</remarks>
			public void Set(IFdbTransaction trans, TKey id, TValue value)
			{
				Contract.NotNull(trans);
				Contract.NotNull(id);

				var pk = this.Parent.KeyCodec.EncodeKey(id);

				trans.Set(this.Subspace.Key(pk), this.Parent.ValueCodec.EncodeValue(value));
			}

			/// <summary>Removes a single entry from the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to remove</param>
			/// <remarks>If the entry did not exist, the operation will not do anything.</remarks>
			public void Remove(IFdbTransaction trans, TKey id)
			{
				Contract.NotNull(trans);
				Contract.NotNull(id);

				var pk = this.Parent.KeyCodec.EncodeKey(id);

				trans.Clear(this.Subspace.Key(pk));
			}

			/// <summary>Creates a query that will attempt to read all the entries in the map within a single transaction.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="options"></param>
			/// <returns>Async sequence of pairs of keys and values, ordered by keys ascending.</returns>
			/// <remarks>CAUTION: This can be dangerous if the map contains a lot of entries! You should always use .Take() to limit the number of results returned.</remarks>
			public IAsyncQuery<KeyValuePair<TKey, TValue?>> All(IFdbReadOnlyTransaction trans, FdbRangeOptions? options = null)
			{
				Contract.NotNull(trans);

				return trans
					.GetRange(this.Subspace.ToRange(), options)
					.Select(kv => this.Parent.DecodeItem(this.Subspace, kv));
			}

			/// <summary>Reads the values of multiple entries in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="ids">List of the keys to read</param>
			/// <returns>Array of results, in the same order as specified in <paramref name="ids"/>.</returns>
			public async Task<TValue?[]> GetValuesAsync(IFdbReadOnlyTransaction trans, IEnumerable<TKey> ids)
			{
				Contract.NotNull(trans);
				Contract.NotNull(ids);

				return await trans.GetValuesAsync(
					ids, id => this.Subspace.Key(this.Parent.KeyCodec.EncodeKey(id)),
					(value, found) => found ? this.Parent.ValueCodec.DecodeValue(value) : default
				).ConfigureAwait(false);
			}

			#endregion

			#region Bulk Operations...

			/// <summary>Clears all the entries in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <remarks>This will delete <b>EVERYTHING</b> in the map!</remarks>
			public void Clear(IFdbTransaction trans)
			{
				Contract.NotNull(trans);

				trans.ClearRange(this.Subspace);
			}

			#region Import...

			/// <summary>Imports a potentially large sequence of items into the map.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="items">Sequence of items to import. If the item already exists in the map, its value will be overwritten.</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <remarks>
			/// <p>Any previously existing items in the map will remain. If you want to get from the previous content, you need to clear the map beforehand.</p>
			/// <p>Other transactions may see a partial view of the map while the sequence is being imported. If this is a problem, you may need to import the map into a temporary subspace, and then 'publish' the final result using an indirection layer (like the Directory Layer)</p>
			/// <p>If the import operation fails midway, all items that have already been successfully imported will be kept in the database.</p>
			/// </remarks>
			public Task ImportAsync(IFdbDatabase db, IEnumerable<KeyValuePair<TKey, TValue>> items, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(items);

				return Fdb.Bulk.InsertAsync(
					db,
					items,
					(item, tr) => Set(tr, item.Key, item.Value),
					ct
				);
			}

			/// <summary>Imports a potentially large sequence of items into the map, using a specific key selector.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="items">Sequence of elements to import. If an item with the same key already exists in the map, its value will be overwritten.</param>
			/// <param name="keySelector">Lambda that will extract the key of an element</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <remarks>
			/// <p>Any previously existing items in the map will remain. If you want to get from the previous content, you need to clear the map beforehand.</p>
			/// <p>Other transactions may see a partial view of the map while the sequence is being imported. If this is a problem, you may need to import the map into a temporary subspace, and then 'publish' the final result using an indirection layer (like the Directory Layer)</p>
			/// <p>If the import operation fails midway, all items that have already been successfully imported will be kept in the database.</p>
			/// </remarks>
			public Task ImportAsync(IFdbDatabase db, IEnumerable<TValue> items, Func<TValue, TKey> keySelector, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(items);
				Contract.NotNull(keySelector);

				return Fdb.Bulk.InsertAsync(
					db,
					items,
					(item, tr) => Set(tr, keySelector(item), item),
					ct
				);
			}

			/// <summary>Imports a potentially large sequence of elements into the map, using specific key and value selectors.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="items">Sequence of elements to import. If an item with the same key already exists in the map, its value will be overwritten.</param>
			/// <param name="keySelector">Lambda that will return the key of an element</param>
			/// <param name="valueSelector">Lambda that will return the value of an element</param>
			/// <param name="ct">Token used to cancel the operation</param>
			/// <remarks>
			/// <p>Any previously existing items in the map will remain. If you want to get from the previous content, you need to clear the map beforehand.</p>
			/// <p>Other transactions may see a partial view of the map while the sequence is being imported. If this is a problem, you may need to import the map into a temporary subspace, and then 'publish' the final result using an indirection layer (like the Directory Layer)</p>
			/// <p>If the import operation fails midway, all items that have already been successfully imported will be kept in the database.</p>
			/// </remarks>
			public Task ImportAsync<TElement>(IFdbDatabase db, IEnumerable<TElement> items, Func<TElement, TKey> keySelector, Func<TElement, TValue> valueSelector, CancellationToken ct)
			{
				Contract.NotNull(db);
				Contract.NotNull(items);
				Contract.NotNull(keySelector);
				Contract.NotNull(valueSelector);

				return Fdb.Bulk.InsertAsync(
					db,
					items,
					(item, tr) => Set(tr, keySelector(item), valueSelector(item)),
					ct
				);
			}

			#endregion

			#endregion

		}

		/// <summary>Resolves this layer for use inside a transaction</summary>
		/// <param name="tr">Transaction used for this operation</param>
		/// <returns><see cref="State"/> that can be used to interact with this map inside this transaction</returns>
		/// <remarks>This state is only valid within the scope of this transaction, and <b>MUST NOT</b> be used outside the retry handler, or reused with a different transaction!</remarks>
		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);

			//TODO: store in transaction context?
			return new State(subspace, this);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbMap<,>);

		#region Export...

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each entry in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync(IFdbDatabase db, Action<KeyValuePair<TKey, TValue?>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, _) =>
				{
					foreach (var item in batch)
					{
						handler(this.DecodeItem(loc, item));
					}
					return Task.CompletedTask;
				},
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each entry in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync(IFdbDatabase db, Func<KeyValuePair<TKey, TValue?>, CancellationToken, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				async (batch, loc, _, _) =>
				{
					foreach (var item in batch)
					{
						await handler(this.DecodeItem(loc, item), ct);
					}
				},
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each batch of items in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync(IFdbDatabase db, Action<KeyValuePair<TKey, TValue?>[]> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, _) =>
				{
					if (batch.Length > 0)
					{
						handler(this.DecodeItems(loc, batch));
					}
					return Task.CompletedTask;
				},
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each batch of items in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync(IFdbDatabase db, Func<KeyValuePair<TKey, TValue?>[], CancellationToken, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, tok) => handler(DecodeItems(loc, batch), tok),
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="init">Handler that is called once before the first batch, to produce the initial state.</param>
		/// <param name="handler">Handler called for each batch of items in the map. It is given the previous state, and should return the updated state. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed and return the result of the last call to <paramref name="handler"/> if there was at least one batch, or the result of <paramref name="init"/> if the map was empty.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public async Task<TResult> AggregateAsync<TResult>(IFdbDatabase db, Func<TResult>? init, Func<TResult, KeyValuePair<TKey, TValue?>[], TResult> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			var state = default(TResult);
			if (init is not null)
			{
				state = init();
			}

			await Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, _) =>
				{
					state = handler(state!, DecodeItems(loc, batch));
					return Task.CompletedTask;
				},
				ct
			);

			return state!;
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="init">Handler that is called once before the first batch, to produce the initial state.</param>
		/// <param name="handler">Handler called for each batch of items in the map. It is given the previous state, and should return the updated state. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="finish">Handler that is called one after the last batch, to produce the final result out of the last state.</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed and return the result of calling <paramref name="finish"/> with the state return by the last call to <paramref name="handler"/> if there was at least one batch, or the result of <paramref name="init"/> if the map was empty.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public async Task<TResult> AggregateAsync<TState, TResult>(IFdbDatabase db, Func<TState>? init, Func<TState, KeyValuePair<TKey, TValue?>[], TState> handler, Func<TState, TResult>? finish, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			var state = default(TState);
			if (init is not null)
			{
				state = init();
			}

			await Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, _) =>
				{
					state = handler(state!, DecodeItems(loc, batch));
					return Task.CompletedTask;
				},
				ct
			);

			ct.ThrowIfCancellationRequested();

			var result = default(TResult);
			if (finish is not null)
			{
				result = finish(state!);
			}

			return result!;
		}

		private KeyValuePair<TKey, TValue?> DecodeItem(IKeySubspace subspace, KeyValuePair<Slice, Slice> item)
		{
			return new(
				this.KeyCodec.DecodeKey(subspace.Decode<TEncodedKey>(item.Key)!),
				this.ValueCodec.DecodeValue(item.Value.Span)
			);
		}

		private KeyValuePair<TKey, TValue?>[] DecodeItems(IKeySubspace subspace, KeyValuePair<Slice, Slice>[] batch)
		{
			Contract.Debug.Requires(batch is not null);

			var items = new KeyValuePair<TKey, TValue?>[batch.Length];
			for (int i = 0; i < batch.Length; i++)
			{
				items[i] = new(
					this.KeyCodec.DecodeKey(subspace.Decode<TEncodedKey>(batch[i].Key)!),
					this.ValueCodec.DecodeValue(batch[i].Value.Span)
				);
			}
			return items;
		}

		#endregion

	}

}
