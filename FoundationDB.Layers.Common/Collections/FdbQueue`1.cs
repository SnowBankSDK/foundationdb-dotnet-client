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
	public class FdbQueue : FdbQueue<Slice, FdbRawValue>
	{
		public FdbQueue(ISubspaceLocation location)
			: base(location, FdbValueCodec.Raw)
		{ }

	}

	/// <summary>Provides a high-contention Queue class</summary>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbQueue<TValue> : FdbQueue<TValue, STuple<TValue>>
		where TValue : notnull
	{
		public FdbQueue(ISubspaceLocation location)
			: base(location, FdbValueCodec.Tuples.ForKey<TValue>())
		{ }

	}

	/// <summary>Provides a high-contention Queue class</summary>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbQueue<TValue, TEncoded> : IFdbLayer<FdbQueue<TValue, TEncoded>.State>
		where TValue : notnull
		where TEncoded : struct, ISpanEncodable
	{
		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="location">Subspace where the queue will be stored</param>
		/// <param name="codec">Encoder for the values stored in this queue</param>
		/// <remarks>Uses the default Tuple serializer</remarks>
		public FdbQueue(ISubspaceLocation location, IFdbValueCodec<TValue, TEncoded> codec)
		{
			Contract.NotNull(location);
			Contract.NotNull(codec);

			this.Location = location;
			this.Codec = codec;
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>Serializer for the elements of the queue</summary>
		public IFdbValueCodec<TValue, TEncoded> Codec { get; }

		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			return new State(subspace, this);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbQueue<,>);

		[PublicAPI]
		public sealed class State
		{

			public IKeySubspace Subspace { get; }

			public FdbQueue<TValue, TEncoded> Parent { get; }

			internal State(IKeySubspace subspace, FdbQueue<TValue, TEncoded> parent)
			{
				Contract.Debug.Requires(subspace is not null && parent is not null);
				this.Subspace = subspace;
				this.Parent = parent;
			}

			/// <summary>Remove all items from the queue.</summary>
			public void Clear(IFdbTransaction tr)
			{
				Contract.NotNull(tr);
				tr.ClearRange(this.Subspace);
			}

			/// <summary>Push a single item onto the queue.</summary>
			public void Push(IFdbTransaction tr, TValue value)
			{
				Contract.NotNull(tr);

#if DEBUG
				tr.Annotate($"Push({value})");
#endif

				//BUGBUG: can be called multiple times per transaction, so need a unique stamp _per_ transaction!!
				tr.SetVersionStampedKey(this.Subspace.Key(tr.CreateUniqueVersionStamp()), this.Parent.Codec.EncodeValue(value));
			}

			/// <summary>Pop the next item from the queue. Cannot be composed with other functions in a single transaction.</summary>
			public async Task<(TValue? Value, bool HasValue)> PopAsync(IFdbTransaction tr)
			{
				Contract.NotNull(tr);
#if DEBUG
				tr.Annotate("Pop()");
#endif

				var first = await tr.GetRangeAsync(this.Subspace.ToRange(), FdbRangeOptions.First);
				if (first.IsEmpty)
				{
#if DEBUG
					tr.Annotate("Got nothing");
#endif
					return default;
				}

				tr.Clear(first[0].Key);
				var value = this.Parent.Codec.DecodeValue(first[0].Value.Span);
#if DEBUG
				if (tr.IsLogged()) tr.Annotate($"Got key {first[0].Key:P} = {value}");
#endif
				return (value, true);
			}

			/// <summary>Test whether the queue is empty.</summary>
			public async Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
			{
				var first = await tr.GetRangeAsync(this.Subspace.ToRange(), FdbRangeOptions.First);
				return first.IsEmpty;
			}

			/// <summary>Get the value of the next item in the queue without popping it.</summary>
			public async Task<(TValue? Value, bool HasValue)> PeekAsync(IFdbReadOnlyTransaction tr)
			{
				Contract.NotNull(tr);

				var first = await tr.GetRangeAsync(this.Subspace.ToRange(), FdbRangeOptions.First);
				if (first.IsEmpty) return default;

				var value = this.Parent.Codec.DecodeValue(first[0].Value.Span);
#if DEBUG
				if (tr.IsLogged()) tr.Annotate($"Peeked key {first[0].Key:P} = {value}");
#endif
				return (value, true);
			}

		}

		#region Bulk Operations

		public Task ExportAsync(IFdbDatabase db, Action<TValue, long> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(kvs, _, offset, _) =>
				{
					foreach(var kv in kvs)
					{
						if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

						handler(this.Codec.DecodeValue(kv.Value.Span)!, offset);
						++offset;
					}
					return Task.CompletedTask;
				},
				ct
			);
		}

		public Task ExportAsync(IFdbDatabase db, Func<TValue, long, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				async (kvs, _, offset, _) =>
				{
					foreach (var kv in kvs)
					{
						if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

						await handler(this.Codec.DecodeValue(kv.Value.Span)!, offset);
						++offset;
					}
				},
				ct
			);
		}

		public Task ExportAsync(IFdbDatabase db, Action<TValue[], long> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(kvs, _, offset, _) =>
				{
					var values = new TValue[kvs.Length];
					for (int i = 0; i < kvs.Length; i++)
					{
						values[i] = this.Codec.DecodeValue(kvs[i].Value.Span)!;
					}
					handler(values, offset);
					return Task.CompletedTask;
				},
				ct
			);
		}

		public Task ExportAsync(IFdbDatabase db, Func<TValue[], long, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(kvs, _, offset, _) =>
				{
					var values = new TValue[kvs.Length];
					for (int i = 0; i < kvs.Length; i++)
					{
						values[i] = this.Codec.DecodeValue(kvs[i].Value.Span)!;
					}
					return handler(values, offset);
				},
				ct
			);
		}

		#endregion

	}

}
