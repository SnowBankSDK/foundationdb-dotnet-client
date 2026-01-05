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

	/// <summary>Provides a high-contention Queue class</summary>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbVector : FdbVector<Slice, FdbRawValue>
	{
		public FdbVector(ISubspaceLocation location)
			: base(location, FdbValueCodec.Raw)
		{ }

	}

	/// <summary>Provides a high-contention Queue class</summary>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbVector<TValue> : FdbVector<TValue, STuple<TValue>>
		where TValue : notnull
	{
		public FdbVector(ISubspaceLocation location)
			: base(location, FdbValueCodec.Tuples.ForKey<TValue>())
		{ }

	}

	/// <summary>Represents a potentially sparse array in FoundationDB.</summary>
	[DebuggerDisplay("Location={Location}, Default={DefaultValue}")]
	[PublicAPI]
	public class FdbVector<TValue, TEncoded> : IFdbLayer<FdbVector<TValue, TEncoded>.State>
		where TValue : notnull
		where TEncoded : struct, ISpanEncodable
	{

		// from https://apple.github.io/foundationdb/vector.html

		// Vector stores each of its values using its index as the key.
		// The size of a vector is equal to the index of its last key + 1.
		//
		// For indexes smaller than the vector's size that have no associated key
		// in the database, the value will be the specified defaultValue.
		//
		// If the last value in the vector has the default value, its key will
		// always be set so that size can be determined.
		//
		// By creating Vector with a Subspace, all kv pairs modified by the
		// layer will have keys that start within that Subspace.

		// Implementation note:
		// - vector.py uses Thread Local Storage that does not work well with async and Tasks in .NET
		//   so we won't be able to 'store' the current transaction in the vector object itself

		/// <summary>Create a new sparse Vector</summary>
		/// <param name="location">Subspace where the vector will be stored</param>
		/// <param name="defaultValue">Default value for sparse entries</param>
		/// <param name="codec">Codec used for the values of this vector</param>
		public FdbVector(ISubspaceLocation location, IFdbValueCodec<TValue, TEncoded> codec, TValue? defaultValue = default)
		{
			Contract.NotNull(location);
			Contract.NotNull(codec);

			this.Location = location;
			this.DefaultValue = defaultValue;
			this.Codec = codec;
		}

		/// <summary>Subspace used as a prefix for all items in this vector</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>Default value for sparse entries</summary>
		public TValue? DefaultValue { get; }

		public IFdbValueCodec<TValue, TEncoded> Codec { get; }

		[PublicAPI]
		public sealed class State
		{

			public IKeySubspace Subspace { get; }

			public FdbVector<TValue, TEncoded> Parent { get; }

			internal State(IKeySubspace subspace, FdbVector<TValue, TEncoded> parent)
			{
				this.Subspace = subspace;
				this.Parent = parent;
			}

			/// <summary>Get the number of items in the Vector. This number includes the sparsely represented items.</summary>
			public Task<long> SizeAsync(IFdbReadOnlyTransaction tr)
			{
				Contract.NotNull(tr);

				return ComputeSizeAsync(tr);
			}

			/// <summary>Push a single item onto the end of the Vector.</summary>
			public async Task PushAsync(IFdbTransaction tr, TValue value)
			{
				Contract.NotNull(tr);

				var size = await ComputeSizeAsync(tr).ConfigureAwait(false);

				tr.Set(this.Subspace.Key(size), this.Parent.Codec.EncodeValue(value));
			}

			/// <summary>Get the value of the last item in the Vector.</summary>
			public Task<TValue?> BackAsync(IFdbReadOnlyTransaction tr)
			{
				//REVIEW: rename this to "PeekLast" ?
				Contract.NotNull(tr);

				//PERF: use GetRange with value decoder!
				return tr
					.GetRangeValues(
						this.Subspace.ToRange(),
						this.Parent.Codec, static (codec, bytes) => codec.DecodeValue(bytes)
					)
					.LastOrDefaultAsync();
			}

			/// <summary>Get the value of the first item in the Vector.</summary>
			public Task<TValue?> FrontAsync(IFdbReadOnlyTransaction tr)
			{
				//REVIEW: rename this to "Peek" ?
				return GetAsync(tr, 0);
			}

			/// <summary>Get and pops the last item off the Vector.</summary>
			public async Task<(TValue? Value, bool HasValue)> PopAsync(IFdbTransaction tr)
			{
				Contract.NotNull(tr);

				// Read the last two entries so we can check if the second to last item
				// is being represented sparsely. If so, we will be required to set it
				// to the default value
				var lastTwo = await tr
					.GetRange(this.Subspace.ToRange(), FdbRangeOptions.Reversed.WithLimit(2))
					.ToListAsync()
					.ConfigureAwait(false);

				// Vector was empty
				if (lastTwo.Count == 0) return default;

				//note: keys are reversed so indices[0] = last, indices[1] = second to last
				var indices = lastTwo.Select(kvp => this.Subspace.DecodeFirst<long>(kvp.Key)).ToList();

				if (indices[0] == 0)
				{ // Vector has size one
				  //pass
				}
				else if (lastTwo.Count == 1 || indices[0] > indices[1] + 1)
				{ // Second to last item is being represented sparsely
					tr.Set(this.Subspace.Key(indices[0] - 1), this.Parent.Codec.EncodeValue(this.Parent.DefaultValue!));
				}

				tr.Clear(lastTwo[0].Key);

				return (this.Parent.Codec.DecodeValue(lastTwo[0].Value.Span), true);
			}

			/// <summary>Swap the items at positions i1 and i2.</summary>
			public async Task SwapAsync(IFdbTransaction tr, long index1, long index2)
			{
				Contract.NotNull(tr);

				if (index1 < 0 || index2 < 0) throw new IndexOutOfRangeException($"Indices ({index1}, {index2}) must be positive");

				var k1 = this.Subspace.Key(index1);
				var k2 = this.Subspace.Key(index2);

				long currentSize = await ComputeSizeAsync(tr).ConfigureAwait(false);

				if (index1 >= currentSize || index2 >= currentSize) throw new IndexOutOfRangeException($"Indices ({index1}, {index2}) are out of range");

				var vs = await tr.GetValuesAsync([ k1, k2 ]).ConfigureAwait(false);
				var v1 = vs[0];
				var v2 = vs[1];

				if (!v2.IsNullOrEmpty)
				{
					tr.Set(k1, v2);
				}
				else if (v1.IsPresent && index1 < currentSize - 1)
				{
					tr.Clear(k1);
				}

				if (!v1.IsNullOrEmpty)
				{
					tr.Set(k2, v1);
				}
				else if (v2.IsPresent && index2 < currentSize - 1)
				{
					tr.Clear(k2);
				}
			}

			/// <summary>Get the item at the specified index.</summary>
			public async Task<TValue?> GetAsync(IFdbReadOnlyTransaction tr, long index)
			{
				Contract.NotNull(tr);
				Contract.Positive(index);

				var output = await tr
					.GetRange(this.Subspace.Key(index), this.Subspace.Last())
					.FirstOrDefaultAsync()
					.ConfigureAwait(false);

				if (output.Key.HasValue)
				{
					if (this.Subspace.Decode<long>(output.Key) == index)
					{ // The requested index had an associated key
						return this.Parent.Codec.DecodeValue(output.Value.Span)!;
					}

					// The requested index is sparsely represented
					return this.Parent.DefaultValue;
				}

				// We requested a value past the end of the vector
				throw new IndexOutOfRangeException($"Index {index} out of range");
			}

			/// <summary>Set the value at a particular index in the Vector.</summary>
			public void Set(IFdbTransaction tr, long index, TValue value)
			{
				Contract.NotNull(tr);

				tr.Set(this.Subspace.Key(index), this.Parent.Codec.EncodeValue(value));
			}

			/// <summary>Test whether the Vector is empty.</summary>
			public async Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
			{
				Contract.NotNull(tr);

				return (await ComputeSizeAsync(tr).ConfigureAwait(false)) == 0;
			}

			/// <summary>Grow or shrink the size of the Vector.</summary>
			public async Task ResizeAsync(IFdbTransaction tr, long length)
			{
				Contract.NotNull(tr);

				long currentSize = await ComputeSizeAsync(tr).ConfigureAwait(false);

				if (length < currentSize)
				{
					tr.ClearRange(this.Subspace.Key(length), this.Subspace.Last());

					// Check if the new end of the vector was being sparsely represented
					if (await ComputeSizeAsync(tr).ConfigureAwait(false) < length)
					{
						tr.Set(this.Subspace.Key(length - 1), this.Parent.Codec.EncodeValue(this.Parent.DefaultValue!));
					}
				}
				else if (length > currentSize)
				{
					tr.Set(this.Subspace.Key(length - 1), this.Parent.Codec.EncodeValue(this.Parent.DefaultValue!));
				}
			}

			/// <summary>Remove all items from the Vector.</summary>
			public void Clear(IFdbTransaction tr)
			{
				Contract.NotNull(tr);

				tr.ClearRange(this.Subspace.ToRange());
			}

			#region Private Helpers...

			private async Task<long> ComputeSizeAsync(IFdbReadOnlyTransaction tr)
			{
				Contract.Debug.Requires(tr != null);

				// get the last key in the range (if there is any)
				var lastKey = await tr.GetRangeKeys(this.Subspace.ToRange()).LastOrDefaultAsync().ConfigureAwait(false);

				if (lastKey.IsNull)
				{ // the range is empty
					return 0;
				}

				return this.Subspace.DecodeFirst<long>(lastKey) + 1;
			}

			#endregion

		}

		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			return new State(subspace, this);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbVector<,>);

	}

}
