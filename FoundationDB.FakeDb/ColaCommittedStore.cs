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

namespace FoundationDB.Storage
{
	using FoundationDB.Client;
	using SnowBank.Collections.CacheOblivious;


	/// <summary><see cref="IFdbCommittedStore"/> backed by a <see cref="ColaOrderedDictionary{TKey,TValue}"/></summary>
	public sealed class ColaCommittedStore : IFdbCommittedStore<ColaCommittedCursor>
	{

		/// <summary>Underlying ColaStore-backed committed keyspace</summary>
		internal ColaOrderedDictionary<Key, Value> Inner { get; }

		public ColaCommittedStore(ColaOrderedDictionary<Key, Value> inner)
		{
			Contract.Debug.Requires(inner != null);
			this.Inner = inner;
		}

		/// <inheritdoc />
		public int Count => this.Inner.Count;

		/// <inheritdoc />
		public bool TryGetValue(Key key, out Value value) => this.Inner.TryGetValue(key, out value);

		/// <inheritdoc />
		public bool ContainsKey(Key key) => this.Inner.ContainsKey(key);

		/// <inheritdoc />
		public TResult Read<TState, TResult>(Key key, TState state, FdbValueDecoder<TState, TResult> decoder)
		{
			return this.Inner.TryGetValue(key, out var value)
				? decoder(state, value.Span, true)
				: decoder(state, default, false);
		}

		/// <inheritdoc />
		public ColaCommittedCursor GetCursor() => new(this.Inner.GetIterator());

		/// <inheritdoc />
		IFdbCommittedCursor IFdbCommittedStore.GetCursor() => GetCursor();

		/// <inheritdoc />
		public IEnumerable<KeyValuePair<Key, Value>> Scan(Key begin, Key end, bool reversed)
			=> reversed
				? this.Inner.ScanReverse(begin, true, end, false)
				: this.Inner.Scan(begin, true, end, false);

		/// <inheritdoc />
		public IEnumerable<KeyValuePair<Key, Value>> IterateOrdered() => this.Inner.IterateOrdered();

		/// <inheritdoc />
		public void VisitRange<TState>(Key begin, Key end, bool reversed, TState state, FdbCommittedRangeVisitor<TState> visitor)
		{
			// arena-backed: kv.Key/kv.Value are views, so the spans cost nothing; the enumerator is the only allocation (O(1))
			foreach (var kv in reversed ? this.Inner.ScanReverse(begin, true, end, false) : this.Inner.Scan(begin, true, end, false))
			{
				if (!visitor(state, kv.Key.Span, kv.Value.Span)) break;
			}
		}

		/// <inheritdoc />
		public IFdbCommittedStore Copy() => new ColaCommittedStore(this.Inner.Copy());

		/// <inheritdoc />
		public bool Remove(Key key) => this.Inner.Remove(key);

		/// <inheritdoc />
		public bool TryGetKeyValue(Key key, out KeyValuePair<Key, Value> entry) => this.Inner.TryGetKeyValue(key, out entry);

		/// <inheritdoc />
		public int RemoveRange(Key begin, Key end) => this.Inner.RemoveRange(begin, true, end, false);

		/// <inheritdoc />
		public Value this[Key key] { set => this.Inner[key] = value; }

	}

	/// <summary><see cref="IFdbCommittedCursor"/> backed by a <see cref="ColaOrderedDictionary{TKey,TValue}"/>'s iterator</summary>
	/// <remarks>A thin struct over the class iterator: the struct satisfies the read hot core's <c>TCursor : struct</c> constraint (devirtualizing the seam), and every member forwards to the wrapped iterator, so copies of the struct share position by construction.</remarks>
	public readonly struct ColaCommittedCursor : IFdbCommittedCursor
	{

		private readonly ColaStore<KeyValuePair<Key, Value>>.Iterator Iter;

		public ColaCommittedCursor(ColaStore<KeyValuePair<Key, Value>>.Iterator iter)
		{
			Contract.Debug.Requires(iter != null);
			this.Iter = iter;
		}

		/// <inheritdoc />
		public ReadOnlySpan<byte> CurrentKey => this.Iter.Current.Key.Span;

		/// <inheritdoc />
		public ReadOnlySpan<byte> CurrentValue => this.Iter.Current.Value.Span;

		/// <inheritdoc />
		public Key CopyKey() => this.Iter.Current.Key; // the arena view is stable for the snapshot's lifetime: no bytes moved

		/// <inheritdoc />
		public Value CopyValue() => this.Iter.Current.Value;

		/// <inheritdoc />
		public KeyValuePair<Key, Value> CopyCurrent() => this.Iter.Current;

		/// <inheritdoc />
		public bool Seek(Key key, bool orEqual) => this.Iter.Seek(new(key, default), orEqual);

		/// <inheritdoc />
		public bool SeekFirst() => this.Iter.SeekFirst();

		/// <inheritdoc />
		public void SeekBeforeFirst() => this.Iter.SeekBeforeFirst();

		/// <inheritdoc />
		public bool Next() => this.Iter.Next();

		/// <inheritdoc />
		public bool Previous() => this.Iter.Previous();

	}

}
