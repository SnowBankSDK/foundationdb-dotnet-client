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
	using System.Numerics;
	using SnowBank.Buffers;

	/// <summary>Multimap that tracks the number of times a specific key/value pair has been inserted or removed.</summary>
	/// <typeparam name="TKey">Type of the keys of the map</typeparam>
	/// <typeparam name="TValue">Type of the values of the map</typeparam>
	[DebuggerDisplay("Location={Location}, AllowNegativeValues={AllowNegativeValues}")]
	[PublicAPI]
	public class FdbMultiMap<TKey, TValue> : FdbMultiMap<TKey, TKey, TValue, TValue>
	{

		/// <summary>Constructs a new <see cref="FdbMultiMap{TKey,TValue}"/></summary>
		/// <param name="location">Location where the map will be stored in the database</param>
		/// <param name="allowNegativeValues">If true, allow negative or zero values to stay in the map.</param>
		public FdbMultiMap(ISubspaceLocation location, bool allowNegativeValues)
			: base(location, FdbIdentityKeyCodec<TKey>.Instance, FdbIdentityKeyCodec<TValue>.Instance, allowNegativeValues)
		{ }

	}

	/// <summary>Multimap that tracks the number of times a specific key/value pair has been inserted or removed.</summary>
	/// <typeparam name="TKey">Type of the logical keys of the map</typeparam>
	/// <typeparam name="TEncodedKey">Type of the lowered keys. Can be the same as <typeparamref name="TKey"/> for simple types (numbers, strings, UUIDs, ...).</typeparam>
	/// <typeparam name="TValue">Type of the logical values of the map</typeparam>
	/// <typeparam name="TEncodedValue">Type of the lowered values. Can be the same as <typeparamref name="TValue"/> for simple types (numbers, strings, UUIDs, ...).</typeparam>
	[DebuggerDisplay("Location={Location}, AllowNegativeValues={AllowNegativeValues}")]
	[PublicAPI]
	public class FdbMultiMap<TKey, TEncodedKey, TValue, TEncodedValue> : IFdbLayer<FdbMultiMap<TKey, TEncodedKey, TValue, TEncodedValue>.State>
	{

		// Inspired by https://apple.github.io/foundationdb/multimaps.html
		// It is the logical equivalent of a Map<KeyValuePair<TKey, TValue>, long> where the value would be incremented each time a specific pair of (key, value) is added (and subtracted when removed)

		// The layer stores each key/value using the following format:
		// (..., key, value) = 64-bit counter

		/// <summary>Constructs a new <see cref="FdbMultiMap{TKey,TEncodedKey,TValue,TEncodedValue}"/></summary>
		/// <param name="location">Location where the map will be stored in the database</param>
		/// <param name="keyCodec">Codec used to represent the keys in their lowered form</param>
		/// <param name="valueCodec">Codec used to represent the values in their lowered form</param>
		/// <param name="allowNegativeValues">If true, allow negative or zero values to stay in the map.</param>
		public FdbMultiMap(ISubspaceLocation location, IFdbKeyCodec<TKey, TEncodedKey> keyCodec, IFdbKeyCodec<TValue, TEncodedValue> valueCodec, bool allowNegativeValues)
		{
			Contract.NotNull(location);
			this.Location = location;
			this.AllowNegativeValues = allowNegativeValues;
			this.KeyCodec = keyCodec;
			this.ValueCodec = valueCodec;
		}

		/// <summary>Subspace used to encode the keys for the items</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>If true, allow negative or zero values to stay in the map.</summary>
		public bool AllowNegativeValues { get; }

		public IFdbKeyCodec<TKey, TEncodedKey> KeyCodec { get; }

		public IFdbKeyCodec<TValue, TEncodedValue> ValueCodec { get; }

		[PublicAPI]
		public sealed class State
		{

			public IKeySubspace Subspace { get; }

			public FdbMultiMap<TKey, TEncodedKey, TValue, TEncodedValue> Parent { get; }

			internal State(IKeySubspace subspace, FdbMultiMap<TKey, TEncodedKey, TValue, TEncodedValue> parent)
			{
				Contract.Debug.Requires(subspace is not null && parent is not null);
				this.Subspace = subspace;
				this.Parent = parent;
			}

			#region Add / Subtract / Remove...

			/// <summary>Increments the count of an (<paramref name="key"/>, <paramref name="value"/>) pair in the multimap.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="value">Value for the <paramref name="key"/> to increment</param>
			/// <remarks>If the (index, value) pair does not exist, its value is considered to be 0</remarks>
			/// <exception cref="System.ArgumentNullException">If <paramref name="trans"/> is null.</exception>
			public void Add(IFdbTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				var pv = this.Parent.ValueCodec.EncodeKey(value);

				trans.AtomicIncrement64(this.Subspace.Key(pk, pv));
			}

			/// <summary>Increments the count of an (<paramref name="key"/>, <paramref name="value"/>) pair in the multimap.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="values">Span of values for the <paramref name="key"/> to increment</param>
			/// <remarks>If any (index, value) pair does not exist, its value is considered to be 0</remarks>
			/// <exception cref="System.ArgumentNullException">If <paramref name="trans"/> is null.</exception>
			public void Add(IFdbTransaction trans, TKey key, ReadOnlySpan<TValue> values)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				for (int i = 0; i < values.Length; i++)
				{
					var pv = this.Parent.ValueCodec.EncodeKey(in values[i]);
					trans.AtomicIncrement64(this.Subspace.Key(pk, pv));
				}
			}

			/// <summary>Increments the count of an (<paramref name="key"/>, <paramref name="value"/>) pair in the multimap.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="values">Span of values for the <paramref name="key"/> to increment</param>
			/// <remarks>If any (index, value) pair does not exist, its value is considered to be 0</remarks>
			/// <exception cref="System.ArgumentNullException">If <paramref name="trans"/> is null.</exception>
			public void Add(IFdbTransaction trans, TKey key, IEnumerable<TValue> values)
			{
				Contract.NotNull(trans);

				if (values.TryGetSpan(out var span))
				{
					Add(trans, key, span);
					return;
				}

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				foreach (var value in values)
				{
					var pv = this.Parent.ValueCodec.EncodeKey(in value);
					trans.AtomicIncrement64(this.Subspace.Key(pk, pv));
				}
			}

			/// <summary>Decrements the count of an (<paramref name="key"/>, <paramref name="value"/>) pair in the multimap, and optionally removes it if the count reaches zero.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="value">Value for the <paramref name="key"/> to decrement</param>
			/// <remarks>If the updated count reaches zero or less, and <see cref="FdbMultiMap{TKey,TEncodedKey,TValue,TEncodedValue}.AllowNegativeValues"/> is <c>false</c>, the key will be cleared from the map.</remarks>
			public void Subtract(IFdbTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				var pv = this.Parent.ValueCodec.EncodeKey(value);

				// decrement, and optionally clear the key if it reaches zero
				trans.AtomicDecrement64(this.Subspace.Key(pk, pv), clearIfZero: !this.Parent.AllowNegativeValues);
			}

			/// <summary>Decrements the count of several values for the given <paramref name="key"/> in the multimap, and optionally removes them when their count reaches zero.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="values">Span of values for the <paramref name="key"/> to decrement</param>
			/// <remarks>If any updated count reaches zero or less, and <see cref="FdbMultiMap{TKey,TEncodedKey,TValue,TEncodedValue}.AllowNegativeValues"/> is <c>false</c>, the key will be cleared from the map.</remarks>
			public void Subtract(IFdbTransaction trans, TKey key, ReadOnlySpan<TValue> values)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				for (int i = 0; i < values.Length; i++)
				{
					var pv = this.Parent.ValueCodec.EncodeKey(in values[i]);
					// decrement, and optionally clear the key if it reaches zero
					trans.AtomicDecrement64(this.Subspace.Key(pk, pv), clearIfZero: !this.Parent.AllowNegativeValues);
				}
			}

			/// <summary>Decrements the count of several values for the given <paramref name="key"/> in the multimap, and optionally removes them when their count reaches zero.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="values">Sequence of values for the <paramref name="key"/> to decrement</param>
			/// <remarks>If any updated count reaches zero or less, and <see cref="FdbMultiMap{TKey,TEncodedKey,TValue,TEncodedValue}.AllowNegativeValues"/> is <c>false</c>, the key will be cleared from the map.</remarks>
			public void Subtract(IFdbTransaction trans, TKey key, IEnumerable<TValue> values)
			{
				Contract.NotNull(trans);

				if (values.TryGetSpan(out var span))
				{
					Subtract(trans, key, span);
					return;
				}

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				foreach (var value in values)
				{
					var pv = this.Parent.ValueCodec.EncodeKey(in value);
					// decrement, and optionally clear the key if it reaches zero
					trans.AtomicDecrement64(this.Subspace.Key(pk, pv), clearIfZero: !this.Parent.AllowNegativeValues);
				}
			}

			/// <summary>Checks if a (<paramref name="key"/>, <paramref name="value"/>) pair exists</summary>
			public async Task<bool> ContainsAsync(IFdbReadOnlyTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				var pv = this.Parent.ValueCodec.EncodeKey(value);

				var counterBytes = await trans.GetAsync(this.Subspace.Key(pk, pv)).ConfigureAwait(false);
				return this.Parent.AllowNegativeValues ? counterBytes.IsPresent : counterBytes.ToInt64() > 0;
			}

			/// <summary>Checks if a <paramref name="key"/> contains any of the given values</summary>
			public Task<bool> ContainsAnyAsync(IFdbReadOnlyTransaction trans, TKey key, ReadOnlySpan<TValue> values)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				var task = trans.GetValuesAsync(values, value => this.Subspace.Key(pk, this.Parent.ValueCodec.EncodeKey(value)));

				return ContainsAnyContinuation(this, task);

				static async Task<bool> ContainsAnyContinuation(State self, Task<Slice[]> task)
				{
					var counters = await task.ConfigureAwait(false);
					for (int i = 0; i < counters.Length; i++)
					{
						if (self.Parent.AllowNegativeValues ? counters[i].IsPresent : counters[i].ToInt64() > 0)
						{
							return true;
						}
					}
					return false;
				}
			}

			/// <summary>Returns the count for a <paramref name="value"/> of a given <paramref name="key"/></summary>
			/// <returns>Count for this value, or <c>null</c> if the index does not contain that particular value</returns>
			/// <remarks>The count can be zero or negative if <see cref="FdbMultiMap{TKey,TEncodedKey,TValue,TEncodedValue}.AllowNegativeValues"/> is <c>true</c>.</remarks>
			public async Task<long?> GetCountAsync(IFdbReadOnlyTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				var pv = this.Parent.ValueCodec.EncodeKey(value);

				var counterBytes = await trans.GetAsync(this.Subspace.Key(pk, pv)).ConfigureAwait(false);
				if (counterBytes.IsNullOrEmpty) return null;
				long counterValue = counterBytes.ToInt64();
				return this.Parent.AllowNegativeValues || counterValue > 0 ? counterValue : null;
			}

			/// <summary>Returns a query that will read the values for a given <paramref name="key"/></summary>
			public IAsyncQuery<TValue> Get(IFdbReadOnlyTransaction trans, TKey key)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);

				if (this.Parent.AllowNegativeValues)
				{
					//TODO: use a GetRange that decodes the keys and values directly
					return trans
						.GetRange(this.Subspace.Key(pk).ToRange())
						.Select(kvp => this.Parent.ValueCodec.DecodeKey(this.Subspace.DecodeLast<TEncodedValue>(kvp.Key)!));
				}
				else
				{
					//TODO: use a GetRange that decodes the keys and values directly
					return trans
						.GetRange(this.Subspace.Key(pk).ToRange())
						.Where(kvp => kvp.Value.ToInt64() > 0) // we need to filter out zero or negative values (possible artefacts)
						.Select(kvp => this.Parent.ValueCodec.DecodeKey(this.Subspace.DecodeLast<TEncodedValue>(kvp.Key)!));
				}
			}

			/// <summary>Returns the list of values for a given <paramref name="key"/></summary>
			/// <returns>List of values for this index, or an empty list if the index does not exist</returns>
			public Task<List<TValue>> GetAsync(IFdbReadOnlyTransaction trans, TKey key)
			{
				return Get(trans, key).ToListAsync();
			}

			/// <summary>Returns a query that will read the counts for each value for a given <paramref name="key"/></summary>
			public IAsyncQuery<(TValue Value, long Count)> GetCounts(IFdbReadOnlyTransaction trans, TKey key)
			{
				var pk = this.Parent.KeyCodec.EncodeKey(key);

				var query = trans
					.GetRange(
						this.Subspace.Key(pk).ToRange(),
						this, static (self, k, v) => (Value: self.Parent.ValueCodec.DecodeKey(self.Subspace.DecodeLast<TEncodedValue>(k)!), Count: v.ToInt64())
					);

				return this.Parent.AllowNegativeValues
					? query
					: query.Where(x => x.Count > 0);
			}

			/// <summary>Removes all the values for a given <paramref name="key"/></summary>
			public void Remove(IFdbTransaction trans, TKey key)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				trans.ClearRange(this.Subspace.Key(pk).ToRange(inclusive: true));
			}

			/// <summary>Removes a <paramref name="value"/> for a specific <paramref name="key"/></summary>
			public void Remove(IFdbTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var pk = this.Parent.KeyCodec.EncodeKey(key);
				var pv = this.Parent.ValueCodec.EncodeKey(value);
				trans.Clear(this.Subspace.Key(pk, pv));
			}

			#endregion

		}

		/// <inheritdoc />
		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			return new State(subspace, this);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbMultiMap<,,,>);

	}

}
