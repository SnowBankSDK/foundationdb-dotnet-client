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

	[PublicAPI]
	[DebuggerDisplay("Version={Version}, Data={Data.Count}, Conflicts={Conflicts.Count}")]
	public sealed record Snapshot
	{

		/// <summary>Version of this snapshot</summary>
		public long Version { get; }

		/// <summary>Key/Value pairs in this snapshot</summary>
		internal IFdbCommittedStore Data { get; }

		/// <summary>Conflicts ranges in this snapshot</summary>
		internal ColaRangeDictionary<Key, long> Conflicts { get; }

		/// <summary>Number of key/value pairs in this snapshot</summary>
		public int Count => this.Data.Count;

		public VersionStamp Stamp { get; }

		public Arena Arena { get; }

		public Snapshot(long version, IFdbCommittedStore data, ColaRangeDictionary<Key, long> conflicts, VersionStamp stamp, Arena arena)
		{
			Contract.Debug.Requires(version >= 0 && data != null && conflicts != null && !stamp.IsIncomplete && arena != null);
			this.Version = version;
			this.Data = data;
			this.Conflicts = conflicts;
			this.Stamp = stamp;
			this.Arena = arena;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Value Read(Key key) => this.Data.TryGetValue(key, out var value) ? value : default;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsKey(Key key) => this.Data.ContainsKey(key);

		[Pure]
		public Key Resolve<TCursor>(Selector selector, bool accessSystemKeys)
			where TCursor : struct, IFdbCommittedCursor
		{
			//HACKHACK:

			if (!accessSystemKeys && selector.Key > SpecialKeys.SystemPrefix)
			{
				throw new FdbException(FdbError.KeyOutsideLegalRange, $"Key selector {selector} requires access to system keys");
			}

			var iter = ((IFdbCommittedStore<TCursor>) this.Data).GetCursor();
			if (!iter.Seek(selector.Key, selector.OrEqual))
			{
				iter.SeekBeforeFirst();
			}

			if (selector.Offset > 0)
			{
				for (int i = 0; i < selector.Offset; i++)
				{
					if (!iter.Next())
					{
						// the walk exhausted the store: clamp like the real cluster does (to the end of the user
						// keyspace without system access, to the end of the system keyspace with it) - offsets
						// that merely LAND in the system range are clamped the same way below
						return accessSystemKeys ? SpecialKeys.SystemEnd : SpecialKeys.SystemPrefix;
					}
				}
			}
			else if (selector.Offset < 0)
			{
				for (int i = selector.Offset; i < 0; i++)
				{
					if (!iter.Previous())
					{
						return Key.Empty;
					}
				}
			}

			// the walk compared keys over spans (no per-step copy); retain only the resolved key
			var key = iter.CopyKey();
			if (key.IsNull || (selector.Offset < 0 && key > selector.Key))
			{
				return Key.Empty;
			}

			if (!accessSystemKeys && key.IsSystemKey())
			{
				return SpecialKeys.SystemPrefix;
			}
			return key;
		}

		public IEnumerable<KeyValuePair<Key, Value>> ScanRange(Key beginInclusive, Key endExclusive, bool reversed = false)
		{
			return this.Data.Scan(beginInclusive, endExclusive, reversed);
		}

		[Pure]
		public FdbRangeChunk GetRange<TCursor>(Key beginInclusive, Key endExclusive, FdbRangeOptions options, int iteration)
			where TCursor : struct, IFdbCommittedCursor
		{
			// each backend's Scan() is its own optimal range iteration: the ColaStore level-merge enumerable
			// measures ~2x faster per key than stepping the seam cursor (see the committed-store scan
			// benchmarks); a backend whose cursor is span-cheap reclaims the loop inside its Scan()
			var source = this.Data.Scan(beginInclusive, endExclusive, options.IsReversed);

			var res = new List<KeyValuePair<Slice, Slice>>();
			bool hasMore = false;
			int limit = options.Limit ?? 0;
			long targetBytes = options.TargetBytes ?? 0;
			long bytes = 0;
			foreach (var kv in source)
			{
				if (limit != 0 && res.Count == limit)
				{
					hasMore = true;
					break;
				}

				if (targetBytes != 0 && bytes >= targetBytes)
				{
					hasMore = true;
					break;
				}

				// very unlikely, but make should that we would not read more than 2GB !?
				int delta = checked(kv.Key.Count + kv.Value.Count);
				if (bytes + delta >= int.MaxValue)
				{
					hasMore = true;
					break;
				}

				res.Add(KeyValuePair.Create(kv.Key.Slice, kv.Value.Slice));
				bytes += delta;
			}

			int count = res.Count;
			Slice first = count > 0 ? res[0].Key : default;
			Slice last = count > 0 ? res[^1].Key : default;

			ApplyFetchMode(res, options);

			return new FdbRangeChunk(count > 0 ? res.ToArray() : [ ], hasMore, iteration, options, first, last, (int) bytes, SliceOwner.Nil);
		}

		/// <summary>Applies the fetch mode to the materialized items of a range read: the omitted component reads as <see cref="Slice.Nil"/>, like the real client; the chunk's First/Last keys and byte accounting keep the raw keys</summary>
		internal static void ApplyFetchMode(List<KeyValuePair<Slice, Slice>> items, FdbRangeOptions options)
		{
			switch (options.Fetch)
			{
				case FdbFetchMode.KeysOnly:
				{
					for (int i = 0; i < items.Count; i++) items[i] = KeyValuePair.Create(items[i].Key, Slice.Nil);
					break;
				}
				case FdbFetchMode.ValuesOnly:
				{
					for (int i = 0; i < items.Count; i++) items[i] = KeyValuePair.Create(Slice.Nil, items[i].Value);
					break;
				}
			}
		}

		/// <summary>Streams a resolved range to <paramref name="visitor"/> over spans, computing the SAME count / First / Last / byte total / hasMore / fetch-mode masking as <see cref="GetRange{TCursor}"/> but without materializing an items array - the span-first range read behind the zero-copy aggregate scan.</summary>
		/// <remarks>Byte-identical to the fast-path <see cref="GetRange{TCursor}"/> (limit truncates at the high end, target-bytes stops after the pair that crosses it, First/Last/bytes use the raw keys, the omitted fetch-mode side reads as an empty span). Retains only First and Last (two keys, O(1)); everything else streams.</remarks>
		public FdbRangeResult VisitRange<TState>(Key beginInclusive, Key endExclusive, FdbRangeOptions options, int iteration, TState state, FdbCommittedRangeVisitor<TState> visitor)
		{
			var acc = new VisitAccumulator<TState>(state, visitor, options.Limit ?? 0, options.TargetBytes ?? 0, options.Fetch.GetValueOrDefault());
			this.Data.VisitRange(beginInclusive, endExclusive, options.IsReversed, acc, static (VisitAccumulator<TState> a, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) => a.Accept(key, value));
			return new FdbRangeResult(acc.Count, acc.HasMore, iteration, options, acc.First, acc.Last, checked((int) acc.Bytes));
		}

		/// <summary>Accumulates a streaming range read: the same limit / target-bytes / First / Last / byte accounting the chunk builder does, minus the items array. One instance per range read (O(1)).</summary>
		private sealed class VisitAccumulator<TState>
		{
			public VisitAccumulator(TState state, FdbCommittedRangeVisitor<TState> visitor, int limit, long targetBytes, FdbFetchMode fetch)
			{
				this.State = state;
				this.Visitor = visitor;
				this.Limit = limit;
				this.TargetBytes = targetBytes;
				this.Fetch = fetch;
			}

			private TState State { get; }
			private FdbCommittedRangeVisitor<TState> Visitor { get; }
			private int Limit { get; }
			private long TargetBytes { get; }
			private FdbFetchMode Fetch { get; }

			public int Count { get; private set; }
			public long Bytes { get; private set; }
			public bool HasMore { get; private set; }
			public Slice First { get; private set; }

			/// <summary>Last returned key, tracked in a grown-only reused buffer so the copy is O(1) allocation over the scan</summary>
			private byte[] LastBuffer = [];
			private int LastLength;
			public Slice Last => this.Count > 0 ? Slice.FromBytes(this.LastBuffer.AsSpan(0, this.LastLength)) : default;

			/// <summary>Returns <c>false</c> to stop the walk (limit / target-bytes reached, or the outer visitor asked to stop)</summary>
			public bool Accept(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			{
				// stop checks BEFORE including this pair (identical order to the chunk builder)
				if (this.Limit != 0 && this.Count == this.Limit) { this.HasMore = true; return false; }
				if (this.TargetBytes != 0 && this.Bytes >= this.TargetBytes) { this.HasMore = true; return false; }
				int delta = checked(key.Length + value.Length);
				if (this.Bytes + delta >= int.MaxValue) { this.HasMore = true; return false; }

				if (this.Count == 0) { this.First = Slice.FromBytes(key); }
				if (this.LastBuffer.Length < key.Length) { this.LastBuffer = new byte[key.Length]; }
				key.CopyTo(this.LastBuffer);
				this.LastLength = key.Length;
				this.Bytes += delta;
				this.Count++;

				// the fetch mode hides one side from the visitor (as an empty span), like ApplyFetchMode's Slice.Nil
				var vk = this.Fetch == FdbFetchMode.ValuesOnly ? default : key;
				var vv = this.Fetch == FdbFetchMode.KeysOnly ? default : value;
				return this.Visitor(this.State, vk, vv);
			}
		}

		/// <summary>Exposes all the key/value pairs in this snapshot</summary>
		public IEnumerable<KeyValuePair<Key, Value>> ReadData() => this.Data.IterateOrdered();

		/// <summary>Exposes all the conflict ranges in this snapshot</summary>
		public IEnumerable<(Key Begin, Key End, long Version)> ReadConflicts()
		{
			foreach (var entry in this.Conflicts)
			{
				yield return (entry.Begin, entry.End, entry.Value);
			}
		}

		public IEnumerable<(Key Key, Value Before, Value After)> Diff(Snapshot previous)
		{
			var itBefore = previous.Data.GetCursor();
			var itAfter = this.Data.GetCursor();

			KeyValuePair<Key, Value> curBefore = default;
			KeyValuePair<Key, Value> curAfter = default;

			if (!itAfter.SeekFirst())
			{ // empty after
				if (!itBefore.SeekFirst())
				{ // both empty
					goto all_done;
				}
				else
				{ // all keys removed
					goto after_is_done;
				}
			}
			if (!itBefore.SeekFirst())
			{ // empty before, all keys added
				goto before_is_done;
			}

			curBefore = itBefore.CopyCurrent();
			curAfter = itAfter.CopyCurrent();

			while (true)
			{
				switch (curBefore.Key.CompareTo(curAfter.Key))
				{
					case < 0:
					{ // key removed
						yield return (curBefore.Key, curBefore.Value, Value.Nil);
						if (!AdvanceCursor(itBefore, out curBefore))
						{
							goto before_is_done;
						}
						break;
					}
					case > 0:
					{ // key added
						yield return (curAfter.Key, Value.Nil, curAfter.Value);
						if (!AdvanceCursor(itAfter, out curAfter))
						{
							goto after_is_done;
						}
						break;
					}
					default:
					{
						if (!curBefore.Value.Equals(curAfter.Value))
						{ // key changed
							yield return (curBefore.Key, curBefore.Value, curAfter.Value);
						}

						// advance both
						switch((AdvanceCursor(itBefore, out curBefore), AdvanceCursor(itAfter, out curAfter)))
						{
							case (false, false): goto all_done;
							case (false, true):  goto after_is_done;
							case (true, false):  goto before_is_done;
						}
						break;
					}
				}
			}

		before_is_done:
			do
			{
				yield return (curAfter.Key, Value.Nil, curAfter.Value);
			}
			while (AdvanceCursor(itAfter, out curAfter));

			goto all_done;

		after_is_done:
			do
			{
				yield return (curBefore.Key, curBefore.Value, Value.Nil);
			}
			while (AdvanceCursor(itBefore, out curBefore));

			goto all_done;

		all_done:
			yield break;
		}

		/// <summary>Steps the cursor and materializes the new position (a diff retains both sides, so the copy is necessary).</summary>
		private static bool AdvanceCursor(IFdbCommittedCursor it, out KeyValuePair<Key, Value> cur)
		{
			if (it.Next())
			{
				cur = it.CopyCurrent();
				return true;
			}
			cur = default;
			return false;
		}

	}
}
