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

	/// <summary>Visits one committed key/value pair, over spans into the backend's memory. Returns <c>false</c> to stop the range walk early (limit / target-bytes reached).</summary>
	/// <remarks>The spans are valid only for the duration of the call (inside the caller's snapshot pin): copy anything that must be retained. This is the range analogue of the point-read <see cref="FdbValueDecoder{TState,TResult}"/> leg.</remarks>
	public delegate bool FdbCommittedRangeVisitor<in TState>(TState state, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

	/// <summary>Committed keyspace of a single <see cref="Snapshot"/>, seen at one version.</summary>
	/// <remarks>The keys and values returned by this store (and its cursors) are only valid for the lifetime of the pinned snapshot: they are backed by the snapshot's arena today, and by memory-mapped pages under a future B-tree backend. Copy anything that must outlive the snapshot.</remarks>
	public interface IFdbCommittedStore
	{

		/// <summary>Number of key/value pairs in the committed keyspace</summary>
		int Count { get; }

		/// <summary>Reads the value for a key, if it is present in the committed keyspace</summary>
		bool TryGetValue(Key key, out Value value);

		/// <summary>Tests whether a key is present in the committed keyspace</summary>
		bool ContainsKey(Key key);

		/// <summary>Reads a value and decodes it IN PLACE: the decoder receives the backend's raw bytes (or an empty span with <c>found == false</c>) and must not let them escape the call.</summary>
		/// <remarks>This is the zero-copy leg of the two-tier read contract: a page-backed backend passes a span straight over its own memory, valid only for the delegate's scope (inside the caller's snapshot pin); the value-returning members hand out caller-owned copies instead.</remarks>
		TResult Read<TState, TResult>(Key key, TState state, FdbValueDecoder<TState, TResult> decoder);

		/// <summary>Returns a fresh ordered bidirectional cursor over the committed keyspace</summary>
		IFdbCommittedCursor GetCursor();

		/// <summary>Enumerates the key/value pairs of a range, in key order (or reverse key order).</summary>
		/// <param name="begin">Inclusive lower bound</param>
		/// <param name="end">Exclusive upper bound</param>
		/// <param name="reversed">When <c>true</c>, the range is enumerated from its high end down to its low end</param>
		IEnumerable<KeyValuePair<Key, Value>> Scan(Key begin, Key end, bool reversed);

		/// <summary>Enumerates every key/value pair, in key order</summary>
		IEnumerable<KeyValuePair<Key, Value>> IterateOrdered();

		/// <summary>Streams a range in key order (or reverse), handing the visitor spans over the backend's memory - no per-pair materialization. Stops when the visitor returns <c>false</c> or the range is exhausted.</summary>
		/// <remarks>The span-first range read: the <see cref="Scan"/> enumerable yields owned <see cref="Key"/>/<see cref="Value"/> pairs (a copy per pair on a page-backed backend); this hands the same pairs as spans so the caller decodes/aggregates in place and copies only what it retains. Same bounds and ordering as <see cref="Scan"/> (<paramref name="begin"/> inclusive, <paramref name="end"/> exclusive).</remarks>
		void VisitRange<TState>(Key begin, Key end, bool reversed, TState state, FdbCommittedRangeVisitor<TState> visitor);

		#region Mutation / publish surface (used to build the next snapshot in ApplyMutations)...

		/// <summary>Creates a mutable copy that can be published as the next snapshot</summary>
		IFdbCommittedStore Copy();

		/// <summary>Releases an UNCOMMITTED copy produced by <see cref="Copy"/> without publishing it.</summary>
		/// <remarks>A persistent backend rolls its writable generation back (allocations, buffered pages, recorded frees) and releases the single-writer slot; the in-memory store has nothing to release, which is this default. EVERY path that drops a copy without publishing it must call this: a conflicted commit, a failed mutation replay, chaos injection.</remarks>
		void Discard()
		{
		}

		/// <summary>Removes a key</summary>
		bool Remove(Key key);

		/// <summary>Reads the stored key/value pair for a key (the stored key may be a different instance than the lookup key)</summary>
		bool TryGetKeyValue(Key key, out KeyValuePair<Key, Value> entry);

		/// <summary>Removes every key in the <c>[begin, end)</c> range</summary>
		int RemoveRange(Key begin, Key end);

		/// <summary>Sets the value for a key</summary>
		Value this[Key key] { set; }

		#endregion

	}

	/// <summary>Committed store whose cursor is exposed as its concrete struct type, so the read hot core can monomorphize per backend.</summary>
	/// <remarks>The read machinery is generic over <typeparamref name="TCursor"/> (<c>where TCursor : struct, IFdbCommittedCursor</c>): the JIT stamps a dedicated code copy per backend and devirtualizes + inlines the per-key cursor calls in the scan loops. The non-generic <see cref="IFdbCommittedStore"/> surface remains for tooling and per-operation call sites, where a single interface dispatch is irrelevant.</remarks>
	public interface IFdbCommittedStore<TCursor> : IFdbCommittedStore
		where TCursor : struct, IFdbCommittedCursor
	{

		/// <summary>Returns a fresh ordered bidirectional cursor over the committed keyspace, typed as the backend's concrete struct</summary>
		new TCursor GetCursor();

	}

	/// <summary>Ordered bidirectional cursor over the committed keyspace of a single <see cref="Snapshot"/>, seen at one version.</summary>
	/// <remarks>
	/// <para>The current entry is read the same span-vs-copy way as the store's <see cref="IFdbCommittedStore.Read{TState,TResult}"/> leg: <see cref="CurrentKey"/>/<see cref="CurrentValue"/> hand out spans straight over the backend's memory for the machinery to compare and walk (the necessary-copy set is much smaller than the touched set - a selector walk compares many keys and returns one), while <see cref="CopyKey"/>/<see cref="CopyValue"/>/<see cref="CopyCurrent"/> materialize an owned copy for the entries the machinery actually retains past the pin. There is deliberately no eager owned-pair accessor: retaining is always an explicit <c>Copy*</c> at the call site.</para>
	/// <para>Every span exposed here is only valid until the next move (or the pinned snapshot's release): backed by the snapshot's arena today, by memory-mapped pages under the persistent backend. Copy (retain) anything that must outlive the current position.</para>
	/// </remarks>
	public interface IFdbCommittedCursor
	{

		/// <summary>Key at the current position, as a span over the backend's memory (valid until the next move or the snapshot's release). For compare/walk; call <see cref="CopyKey"/> to retain it.</summary>
		ReadOnlySpan<byte> CurrentKey { get; }

		/// <summary>Value at the current position, as a span over the backend's memory (valid until the next move or the snapshot's release). Call <see cref="CopyValue"/> to retain it.</summary>
		ReadOnlySpan<byte> CurrentValue { get; }

		/// <summary>Materializes an owned copy of the current key: a page-backed backend copies off the page, a view-backed one returns its stable arena view.</summary>
		Key CopyKey();

		/// <summary>Materializes an owned copy of the current value.</summary>
		Value CopyValue();

		/// <summary>Materializes an owned copy of the current key/value pair (both retained).</summary>
		KeyValuePair<Key, Value> CopyCurrent();

		/// <summary>Seeks the largest key that is less than <paramref name="key"/> (or equal to it when <paramref name="orEqual"/> is <c>true</c>).</summary>
		bool Seek(Key key, bool orEqual);

		/// <summary>Seeks the smallest key in the store</summary>
		bool SeekFirst();

		/// <summary>Sets the cursor just before the first key in the store</summary>
		void SeekBeforeFirst();

		/// <summary>Moves the cursor to the smallest key that is greater than the current one</summary>
		bool Next();

		/// <summary>Moves the cursor to the largest key that is smaller than the current one</summary>
		bool Previous();

	}
}
