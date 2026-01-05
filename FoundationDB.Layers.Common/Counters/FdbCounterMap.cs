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

namespace FoundationDB.Layers.Counters
{

	/// <summary>Providers a dictionary of 64-bit counters that can be updated atomically</summary>
	/// <typeparam name="TKey">Type of the keys in the counter map</typeparam>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbCounterMap<TKey> : FdbCounterMap<TKey, TKey>
	{

		/// <summary>Create a new counter map.</summary>
		public FdbCounterMap(ISubspaceLocation location)
			: base(location, FdbIdentityKeyCodec<TKey>.Instance)
		{ }

	}

	/// <summary>Providers a dictionary of 64-bit counters that can be updated atomically</summary>
	/// <typeparam name="TKey">Type of the logical keys in the counter map</typeparam>
	/// <typeparam name="TEncodedKey">Type of the lowered keys in the counter map</typeparam>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbCounterMap<TKey, TEncodedKey> : IFdbLayer<FdbCounterMap<TKey, TEncodedKey>.State>
	{

		/// <summary>Create a new counter map.</summary>
		public FdbCounterMap(ISubspaceLocation location, IFdbKeyCodec<TKey, TEncodedKey> codec)
		{
			Contract.NotNull(location);

			this.Location = location;
			this.Codec = codec;
		}

		/// <summary>Subspace used as a prefix for all items in this counter list</summary>
		public ISubspaceLocation Location { get; }

		public IFdbKeyCodec<TKey, TEncodedKey> Codec { get; }

		/// <summary>Resolves this layer for use inside a transaction</summary>
		/// <param name="tr">Transaction used for this operation</param>
		/// <returns><see cref="State"/> that can be used to interact with this map inside this transaction</returns>
		/// <remarks>This state is only valid within the scope of this transaction, and <b>MUST NOT</b> be used outside the retry handler, or reused with a different transaction!</remarks>
		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			return new State(subspace, this);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbCounterMap<,>);

		[PublicAPI]
		public sealed class State
		{

			public IKeySubspace Subspace { get; }

			public FdbCounterMap<TKey, TEncodedKey> Parent { get; }

			internal State(IKeySubspace subspace, FdbCounterMap<TKey, TEncodedKey> parent)
			{
				this.Subspace = subspace;
				this.Parent = parent;
			}

			/// <summary>Add a value to a counter in one atomic operation</summary>
			/// <param name="transaction"></param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <param name="value">Value that will be added</param>
			/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
			public void Add(IFdbTransaction transaction, TKey counterKey, long value)
			{
				Contract.NotNull(transaction);
				Contract.NotNull(counterKey);

				//REVIEW: we could no-op if value == 0 but this may change conflict behaviour for other transactions...
				var pk = this.Parent.Codec.EncodeKey(counterKey);
				transaction.AtomicAdd64(this.Subspace.Key(pk), value);
			}

			/// <summary>Subtract a value from a counter in one atomic operation</summary>
			/// <param name="transaction">Transaction to use for the operation</param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <param name="value">Value that will be subtracted. If the value is zero</param>
			/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
			public void Subtract(IFdbTransaction transaction, TKey counterKey, long value)
				=> Add(transaction, counterKey, -value);

			/// <summary>Increment the value of a counter in one atomic operation</summary>
			/// <param name="transaction">Transaction to use for the operation</param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
			public void Increment(IFdbTransaction transaction, TKey counterKey)
				=> Add(transaction, counterKey, 1);

			/// <summary>Decrement the value of a counter in one atomic operation</summary>
			/// <param name="transaction">Transaction to use for the operation</param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
			public void Decrement(IFdbTransaction transaction, TKey counterKey)
				=> Add(transaction, counterKey, -1);

			/// <summary>Read the value of a counter</summary>
			/// <param name="transaction">Transaction to use for the operation</param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <returns></returns>
			public async Task<long?> ReadAsync(IFdbReadOnlyTransaction transaction, TKey counterKey)
			{
				Contract.NotNull(transaction);
				Contract.NotNull(counterKey);

				var pk = this.Parent.Codec.EncodeKey(counterKey);
				var data = await transaction.GetAsync(this.Subspace.Key(pk)).ConfigureAwait(false);
				return data.IsNullOrEmpty ? null : data.ToInt64();
			}

			/// <summary>Adds a value to a counter, and return its new value.</summary>
			/// <param name="transaction">Transaction to use for the operation</param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <param name="value">Value that will be added to the counter</param>
			/// <returns>New value of the counter. Returns <paramref name="value"/> if the counter did not exist previously.</returns>
			/// <remarks>This method WILL conflict with other transactions!</remarks>
			public async Task<long> AddThenReadAsync(IFdbTransaction transaction, TKey counterKey, long value)
			{
				Contract.NotNull(transaction);
				Contract.NotNull(counterKey);

				var pk = this.Parent.Codec.EncodeKey(counterKey);
				var res = await transaction.GetAsync(this.Subspace.Key(pk)).ConfigureAwait(false);

				if (!res.IsNullOrEmpty) value += res.ToInt64();
				transaction.Set(this.Subspace.Key(pk), Slice.FromFixed64(value));

				return value;
			}

			public Task<long> SubtractThenReadAsync(IFdbTransaction transaction, TKey counterKey, long value)
				=> AddThenReadAsync(transaction, counterKey, -value);

			public Task<long> IncrementThenReadAsync(IFdbTransaction transaction, TKey counterKey)
				=> AddThenReadAsync(transaction, counterKey, 1);

			public Task<long> DecrementThenReadAsync(IFdbTransaction transaction, TKey counterKey)
				=> AddThenReadAsync(transaction, counterKey, -1);

			/// <summary>Adds a value to a counter, but return its previous value.</summary>
			/// <param name="transaction">Transaction to use for the operation</param>
			/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
			/// <param name="value">Value that will be added to the counter</param>
			/// <returns>Previous value of the counter. Returns 0 if the counter did not exist previously.</returns>
			/// <remarks>This method WILL conflict with other transactions!</remarks>
			public async Task<long> ReadThenAddAsync(IFdbTransaction transaction, TKey counterKey, long value)
			{
				Contract.NotNull(transaction);
				Contract.NotNull(counterKey);

				var pk = this.Parent.Codec.EncodeKey(counterKey);
				var res = await transaction.GetAsync(this.Subspace.Key(pk)).ConfigureAwait(false);

				long previous = res.IsNullOrEmpty ? 0 : res.ToInt64();
				transaction.Set(this.Subspace.Key(pk), Slice.FromFixed64(value + previous));

				return previous;
			}

			public Task<long> ReadThenSubtractAsync(IFdbTransaction transaction, TKey counterKey, long value)
				=> ReadThenAddAsync(transaction, counterKey, -value);

			public Task<long> ReadThenIncrementAsync(IFdbTransaction transaction, TKey counterKey)
				=> ReadThenAddAsync(transaction, counterKey, 1);

			public Task<long> ReadThenDecrementAsync(IFdbTransaction transaction, TKey counterKey)
				=> ReadThenAddAsync(transaction, counterKey, -1);

		}

	}

}
