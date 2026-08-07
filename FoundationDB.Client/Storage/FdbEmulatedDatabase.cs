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

// Enable this to enable invariant checks
//#define CHECK_INVARIANTS

// Enable this to get tons of logs
//#define FULL_DEBUG

// ReSharper disable MemberHidesStaticFromOuterClass

namespace FoundationDB.Storage
{
	using System.Buffers.Binary;
	using System.Runtime.InteropServices;
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using SnowBank.Collections.CacheOblivious;
	using SnowBank.Threading;
	using static FoundationDB.Storage.FdbEmulatedDatabase;

	/// <summary>Simulates a FoundationDB cluster running in-memory in the local process</summary>
	/// <remarks>This emulator is currently <b>EXPERIMENTAL</b> and may not accurately reproduce the behavior of an actual fdb cluster, most notably due to the absence of network latency!</remarks>
	[PublicAPI]
	[DebuggerDisplay("Version={CurrentSnapshotUnsafe.Version}, Count={CurrentSnapshotUnsafe.Data.Count}")]
	public class FdbEmulatedDatabase : IFdbDatabaseHandler
	{

		[PublicAPI]
		[DebuggerDisplay("Id={Id}, Version={Inner.Version}, Mutations={Mutations.Count}, Reads={ReadConflicts.Count}, Writes={WriteConflicts.Count}")]
		public sealed record ReadYourWritesSnapshot
		{

			private static long IdCounter = 0;

			private Snapshot Inner { get; }

			internal ColaRangeDictionary<Key, Mutation> Mutations { get; } = new(Key.Comparer.Default);

			internal ColaRangeSet<Key> ReadConflicts { get; } = new(Key.Comparer.Default);

			internal ColaRangeSet<Key> WriteConflicts { get; } = new(Key.Comparer.Default);

			private Arena Arena { get; }

			private long Id { get; }

			public ReadYourWritesSnapshot(Snapshot inner, Arena arena)
			{
				this.Inner = inner;
				this.Arena = arena;
				this.Id = Interlocked.Increment(ref IdCounter);
				Kenobi($"!! #{this.Id} started new trans at rv {inner.Version}");
			}

			public long Version => this.Inner.Version;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Value Read(KeyRange key, bool ryw, bool snapshotRead, bool accessSystemKeys)
			{
				if (!accessSystemKeys && IsSystemReadableKey(key.Begin))
				{
					throw ErrorCannotAccessSystemKeys();
				}

				Value value;

				if (ryw && this.Mutations.FindFirst(key.Begin, key.End, out var mutation))
				{
					if (mutation.IsKv())
					{
						return mutation.Parameter;
					}

					if (mutation.IsRange())
					{
						return default;
					}

					if (mutation.IsAtomic())
					{
						// replace the chain of atomics into a single Set(...) by reading the value from the snapshot, and applying each mutation on top

						// a chain anchored on an own Set or Clear is fully determined locally; only an unanchored
						// chain reads through to the committed value
						bool readThrough = mutation.Op is not (Operation.Set or Operation.Clear);
						var prev = readThrough ? this.Inner.Read(key.Begin) : default;

						// flatten the chain
						value = prev;
						do
						{
							value = CoalesceAtomic(this.Arena, value, mutation);
							mutation = mutation.Next;
						}
						while (mutation != null);

						if (!snapshotRead)
						{
							// the documented read-your-writes caveat: a REGULAR read converts the chain into a single
							// Set of the read value (semantically neutral for an anchored chain: it applies to the
							// same result at commit time), and establishes a conflict range on the key IF the value
							// depended on the database; a SNAPSHOT read observes the value transiently and leaves the
							// chain to apply over the committed value at commit time (oracle-pinned)
							this.Mutations.Mark(key.Begin, key.End, Mutation.Set(value));
							if (readThrough)
							{
								this.ReadConflicts.Mark(key.Begin, key.End);
							}
						}

						return value;
					}

					throw new NotSupportedException($"TODO: Read previous mutation of type {mutation.Op}");
				}

				value = this.Inner.Read(key.Begin);
				if (!snapshotRead)
				{
					this.ReadConflicts.Mark(key.Begin, key.End);
				}

				return value;
			}

			/// <summary>Compares two equal-length byte strings as unsigned little-endian integers (most significant byte last).</summary>
			private static int CompareUnsignedLittleEndian(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
			{
				Contract.Debug.Requires(left.Length == right.Length);
				for (int i = left.Length - 1; i >= 0; i--)
				{
					if (left[i] != right[i]) return left[i] < right[i] ? -1 : +1;
				}
				return 0;
			}

			public static Value CoalesceAtomic(Arena scratch, Value previous, Mutation mutation)
			{
				Contract.Debug.Requires(mutation != null && mutation.IsAtomic());

				var operand = mutation.Parameter;

				switch (mutation.Op)
				{
					case Operation.Add:
					{
						if (previous.IsNull)
						{
							return scratch.InternValue(operand);
						}

						var value = scratch.AllocateValue(operand.Count);
						ComputeAtomicAdd(value.UnsafeSpan, previous.Span, operand.Span);
						return value;
					}

					case Operation.VersionStampedKey:
					{
						throw new NotSupportedException("TODO: cannot combine VersionStampedKey atomic operation");
					}
					case Operation.VersionStampedValue:
					{
						throw new NotSupportedException("TODO: cannot combine VersionStampedValue atomic operation");
					}

					case Operation.CompareAndClear:
					{
						// Performs an atomic compare and clear operation. If the existing value in the database is equal to the given value, then given key is cleared.
						if (!previous.IsNull && previous.Equals(operand))
						{
							return Value.Nil;
						}

						return previous;
					}

					case Operation.BitAnd:
					{
						// Performs a bitwise ``and`` operation.
						// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
						// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
						if (previous.IsNull)
						{
							// the parameter is stored when the key is missing (api 300+ semantics, pinned against fdb 7.4;
							// pre-300 clusters stored zeros instead)
							return scratch.InternValue(operand);
						}
						else
						{
							var value = scratch.AllocateValue(operand.Count);
							ComputeAtomicBitAnd(value.UnsafeSpan, previous.Span, operand.Span);
							return value;
						}
					}
					
					case Operation.BitOr:
					{
						// Performs a bitwise ``or`` operation.
						// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
						// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
						if (previous.IsNull)
						{
							// void OR xxx = xxx
							return scratch.InternValue(operand);
						}

						var value = scratch.AllocateValue(operand.Count);
						ComputeAtomicBitOr(value.UnsafeSpan, previous.Span, operand.Span);
						return value;
					}
					
					case Operation.BitXor:
					{
						// Performs a bitwise ``xor`` operation.
						// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
						// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
						if (previous.IsNull)
						{
							// void XOR xxx = xxx
							return scratch.InternValue(operand);
						}

						var value = scratch.AllocateValue(operand.Count);
						ComputeAtomicBitXor(value.UnsafeSpan, previous.Span, operand.Span);
						return value;
					}
					
					case Operation.Max:
					{
						// Performs a little-endian comparison of byte strings.
						// If the existing value in the database is not present, the parameter is stored.
						// The existing value is zero-extended or truncated to the operand length, then both are
						// compared as UNSIGNED LITTLE-ENDIAN integers (pinned against fdb 7.4: the comparison is
						// numeric, NOT lexicographic — ByteMin/ByteMax are the lexicographic ones).
						if (previous.IsNull)
						{
							return scratch.InternValue(operand);
						}
						var adjusted = scratch.AllocateValue(operand.Count, clear: true);
						previous.Span[..Math.Min(previous.Count, operand.Count)].CopyTo(adjusted.UnsafeSpan);
						return CompareUnsignedLittleEndian(adjusted.Span, operand.Span) >= 0 ? adjusted : scratch.InternValue(operand);
					}
					
					case Operation.Min:
					{
						// Performs a little-endian comparison of byte strings.
						// If the existing value in the database is not present, the parameter is stored (zero-extending
						// the missing value instead would make MIN always win with zeros).
						// The existing value is zero-extended or truncated to the operand length, then both are
						// compared as UNSIGNED LITTLE-ENDIAN integers (pinned against fdb 7.4: the comparison is
						// numeric, NOT lexicographic — ByteMin/ByteMax are the lexicographic ones).
						if (previous.IsNull)
						{
							return scratch.InternValue(operand);
						}
						var adjusted = scratch.AllocateValue(operand.Count, clear: true);
						previous.Span[..Math.Min(previous.Count, operand.Count)].CopyTo(adjusted.UnsafeSpan);
						return CompareUnsignedLittleEndian(adjusted.Span, operand.Span) <= 0 ? adjusted : scratch.InternValue(operand);
					}

					case Operation.ByteMax:
					{
						// Performs lexicographic comparison of byte strings. If the existing value in the database is not present, then the parameter is stored. Otherwise, the larger of the two values is then stored in the database.
						if (previous.IsNull || operand > previous)
						{
							return scratch.InternValue(operand);
						}

						return previous;
					}

					case Operation.ByteMin:
					{
						// Performs lexicographic comparison of byte strings. If the existing value in the database is not present, then the parameter is stored. Otherwise, the smaller of the two values is then stored in the database.
						if (previous.IsNull || operand < previous)
						{
							return scratch.InternValue(operand);
						}

						return previous;
					}

					case Operation.AppendIfFits:
					{
						if (previous.IsNull)
						{ // first
							return scratch.InternValue(operand); //BUGBUG: value!
						}

						int n = checked(previous.Count + operand.Count);
						if (n > 100_000) //BUGBUG: constnat!
						{ // does not fit!
							return previous;
						}

						var tmp = scratch.AllocateValue(n);
						previous.Span.CopyTo(tmp.UnsafeSpan);
						operand.Span.CopyTo(tmp.UnsafeSpan[previous.Count..]);
						return tmp;
					}

					case Operation.Set:
					{
						return mutation.Parameter;
					}

					case Operation.Clear:
					{
						return Value.Nil;
					}

					default:
					{
						throw new NotImplementedException($"TODO: Coalesce {mutation.Op} atomic");
					}
				}
			}

			public void Read(ReadOnlySpan<KeyRange> keys, Span<Slice> buffer, bool ryw, bool snapshotRead, bool accessSystemKeys)
			{
				if (buffer.Length < keys.Length) throw new ArgumentException("Buffer is too small", nameof(buffer));

				for (int i = 0; i < keys.Length; i++)
				{
					buffer[i] = Read(keys[i], ryw, snapshotRead, accessSystemKeys).Slice;
				}
			}

			public long Read<TValue>(ReadOnlySpan<KeyRange> keys, Span<TValue> values, FdbValueDecoder<TValue> decoder, bool ryw, bool snapshotRead, bool accessSystemKeys)
			{
				long totalSize = 0;
				for (int i = 0; i < keys.Length; i++)
				{
					var value = Read(keys[i], ryw, snapshotRead, accessSystemKeys);
					values[i] = decoder(value.Span, !value.IsNull);
					totalSize += value.Count;
				}
				return totalSize;
			}

			public long Read<TState, TValue>(ReadOnlySpan<KeyRange> keys, Span<TValue> values, TState state, FdbValueDecoder<TState, TValue> decoder, bool ryw, bool snapshotRead, bool accessSystemKeys)
			{
				long totalSize = 0;
				for (int i = 0; i < keys.Length; i++)
				{
					var value = Read(keys[i], ryw, snapshotRead, accessSystemKeys);
					values[i] = decoder(state, value.Span, !value.IsNull);
					totalSize += value.Count;
				}
				return totalSize;
			}

			public void Set(KeyRange key, Value value, bool accessSystemKeys)
			{
				if (!accessSystemKeys && IsSystemWritableKey(key.Begin))
				{
					throw ErrorCannotAccessSystemKeys();
				}

				this.Mutations.Mark(key.Begin, key.End, Mutation.Set(value));
				this.WriteConflicts.Mark(key.Begin, key.End);
			}

			public void Clear(KeyRange key, bool accessSystemKeys)
			{
				if (!accessSystemKeys && IsSystemWritableKey(key.Begin))
				{
					throw ErrorCannotAccessSystemKeys();
				}

				this.Mutations.Mark(key.Begin, key.End, Mutation.Clear());
				this.WriteConflicts.Mark(key.Begin, key.End);
			}

			public void ClearRange(Key beginInclusive, Key endExclusive, bool accessSystemKeys)
			{
				if (!accessSystemKeys && IsSystemWritableKey(beginInclusive))
				{
					throw ErrorCannotAccessSystemKeys();
				}
				if (!accessSystemKeys && IsSystemWritableKey(endExclusive) && endExclusive.Count > 1)
				{
					throw ErrorCannotAccessSystemKeys();
				}
				this.Mutations.Mark(beginInclusive, endExclusive, Mutation.ClearRange());
				this.WriteConflicts.Mark(beginInclusive, endExclusive);
			}

			public void Atomic(KeyRange key, Value value, FdbMutationType type, bool accessSystemKeys)
			{
				if (!accessSystemKeys && IsSystemWritableKey(key.Begin))
				{
					throw ErrorCannotAccessSystemKeys();
				}

				var stacked = Mutation.Atomic(type, value);
				// note: bounds-aware lookup, because TryGetValue can match a range whose EXCLUSIVE end equals the key
				var mutation = FindCoveringMutation(key.Begin);
				if (mutation != null && !mutation.IsRange())
				{ // a single-key entry at this exact key: stack this operation on top of the previous one
					(mutation.Tail ?? mutation).Next = stacked;
					mutation.Tail = stacked;
				}
				else if (mutation != null)
				{ // the key is covered by an uncommitted cleared RANGE: the atomic applies over the locally-cleared (nil)
				  // value, and must NOT mutate the shared range entry (it covers other keys) — pinned by the RYW fuzzer
					var head = Mutation.Clear();
					head.Next = stacked;
					head.Tail = stacked;
					mutation = head;
				}
				else
				{
					mutation = stacked;
				}

				this.Mutations.Mark(key.Begin, key.End, mutation);
				this.WriteConflicts.Mark(key.Begin, key.End);
			}

			public void ReadConflict(Key beginInclusive, Key endExclusive)
			{
				this.ReadConflicts.Mark(beginInclusive, endExclusive);
			}

			public void WriteConflict(Key beginInclusive, Key endExclusive)
			{
				this.WriteConflicts.Mark(beginInclusive, endExclusive);
			}

			public Key Resolve<TCursor>(Selector selector, bool ryw, bool snapshotRead, bool accessSystemKeys)
				where TCursor : struct, IFdbCommittedCursor
			{
				if (!ryw || this.WriteConflicts.Count == 0)
				{ // fast path for read-only transactions!
					var key = this.Inner.Resolve<TCursor>(selector, accessSystemKeys);
					if (!snapshotRead)
					{
						MarkResolveReadConflict(selector, key);
					}
					return key;
				}

				// slow path: resolve against the merged view (committed snapshot + local uncommitted mutations)
				// getKey RESULT: skip a CompareAndClear'd is_kv anchor to the nearest content key (FDBV-037)
				var resolved = ResolveMerged(selector, accessSystemKeys, contentSkip: true);

				if (!snapshotRead)
				{
					MarkResolveReadConflict(selector, resolved, merged: true);
				}
				return resolved;
			}

			/// <summary>Resolves a key selector against the merged view (committed snapshot + local uncommitted mutations), without recording any conflict range.</summary>
			/// <remarks>This is the ONLY correct selector semantics over pending writes: the onion iterator's per-layer seek is exact just for the 0/+1 offsets the internal pager uses, so range-read bounds resolve here too. <paramref name="contentSkip"/> selects the two fdb semantics: getKey passes <c>true</c> (its RESULT skips a CompareAndClear'd is_kv anchor forward/backward to the nearest content key, FDBV-037); a GetRange begin/end BOUND passes <c>false</c> (it stops at the raw is_kv anchor, FDBV-038 - the merged range scan excludes CAC'd keys on its own, so skipping the bound would over-shrink or over-grow the range).</remarks>
			private Key ResolveMerged(Selector selector, bool accessSystemKeys, bool contentSkip)
			{
				if (!accessSystemKeys && selector.Key > SpecialKeys.SystemPrefix)
				{
					throw new FdbException(FdbError.KeyOutsideLegalRange, $"Key selector {selector} requires access to system keys");
				}

				var merged = GetMergedVisibleKeys(accessSystemKeys);

				// base position: index of the last key <= pivot (orEqual) or < pivot (!orEqual), -1 when none
				int baseIndex = -1;
				for (int i = 0; i < merged.Count; i++)
				{
					int cmp = merged[i].CompareTo(selector.Key);
					if (cmp < 0 || (selector.OrEqual && cmp == 0)) { baseIndex = i; } else { break; }
				}

				long target = (long) baseIndex + selector.Offset;
				if (target < 0)
				{ // before the first visible key
					return Key.Empty;
				}
				if (target >= merged.Count)
				{ // past the last visible key: clamp like the real cluster does (the walk enters the system range)
					return accessSystemKeys ? SpecialKeys.SystemEnd : SpecialKeys.SystemPrefix;
				}

				int index = (int) target;

				// FDBV-037/038: the is_kv walk above positioned the anchor (a pending atomic counts as a present
				// boundary key). fdb getKey is getRange with limit 1 (ReadYourWrites.actor.cpp), so its RESULT is the
				// nearest CONTENT key from the anchor - forward for offset > 0, backward for offset <= 0 - which SKIPS
				// a CompareAndClear'd key (content-absent though is_kv-present). This content-skip is the getKey RESULT
				// step ONLY (contentSkip): a GetRange begin/end BOUND stops at the raw is_kv anchor - the merged range
				// scan (OnionIterator) already excludes a CAC'd key, so skipping the bound too would drop a content key
				// below an end bound, or add one below a begin bound (FDBV-038). merged is the is_kv set and content-
				// present keys are a subset of it, so this is a filtered scan over merged.
				if (contentSkip)
				{
					if (selector.Offset > 0)
					{
						while (index < merged.Count && !IsContentPresentInMergedView(merged[index])) index++;
						if (index >= merged.Count) return accessSystemKeys ? SpecialKeys.SystemEnd : SpecialKeys.SystemPrefix;
					}
					else
					{
						while (index >= 0 && !IsContentPresentInMergedView(merged[index])) index--;
						if (index < 0) return Key.Empty;
					}
				}
				return merged[index];
			}

			/// <summary>Marks the read-conflict range implied by a resolved key selector (the range of keys whose change could alter the resolution).</summary>
			/// <remarks>In the merged path, segments fully determined by the transaction's own writes are subtracted: a key masked by an own set or clear cannot change the merged resolution, so it is not a database dependency (same exemption as the range-read path).</remarks>
			private void MarkResolveReadConflict(Selector selector, Key key, bool merged = false)
			{
				// the range to mark as a conflict depends on the position of the key relative to the selector: whether it is before, equal, or after
				Key from, to;
				int cmp = key.CompareTo(selector.Key);
				if (cmp == 0)
				{
					// only posible with (orEqual==false, offset=+1) or (orEqual==true, offset==0)
					// => the only way for the result to change in both cases is if the pivot key is cleared
					from = key;
					to = key.GetSuccessor(this.Arena); //TODO: scratch!
				}
				else if (cmp > 0)
				{
					// If can only happend with offset >= 1, because:
					// - when offset < 0: the result would be less than the pivot key, and in that case 'cmp' would be < 0
					// - when offset == 0: only possible if orEqual == true and the pivot key exists in the database, but in that case 'cmp' would be == 0

					// the resolved key is always included (oracle-pinned: even a pure value change of it conflicts);
					// the pivot is included only when it can participate in the walk (orEqual == false: the pivot
					// appearing would become the new result), never when the walk starts strictly after it
					from = selector.OrEqual
						? selector.Key.GetSuccessor(this.Arena) // e.g. fGT{pivot}: whether the pivot itself exists can never change the result
						: selector.Key;                         // e.g. fGE{pivot}: the pivot appearing would become the result
					to = key.GetSuccessor(this.Arena);
				}
				else
				{
					// backward walk: the resolved key is always included; the pivot is included only when it can
					// participate in the base (orEqual == true: the pivot appearing would become the result)
					from = key;
					to = selector.OrEqual
						? selector.Key.GetSuccessor(this.Arena) // e.g. lLE{pivot}
						: selector.Key;                         // e.g. lLT{pivot}: whether the pivot itself exists can never change the result
				}

				if (from >= to) return;
				if (merged)
				{
					MarkMergedRangeReadConflict(from, to);
				}
				else
				{
					this.ReadConflicts.Mark(from, to);
				}
			}

			/// <summary>Marks the read conflict of a range read from its selector bounds and scan outcome.</summary>
			/// <remarks>
			/// <para>The extent the result depends on anchors on the pivot span [begin pivot (excluded when orEqual), end pivot (included when orEqual)): a proper span records even when nothing was returned, a degenerate span records nothing, and resolution slack beyond either pivot is not a dependency. Returned keys, and the gap between each pivot and the served keys on its side, are always covered.</para>
			/// <para>A satisfied limit clamps the truncated side: the high side under a forward-resolving end, the low side on a reverse read - unless the begin selector walks forward (offset above +1), in which case the whole span from the begin pivot bound stays a dependency.</para>
			/// </remarks>
			private void MarkRangeReadConflict(Selector beginSelector, Selector endSelector, Key lowestReturned, Key highestReturned, bool limitHit, bool reversed, bool merged)
			{
				// oracle-fitted against the FULL 378-shape x 12-position calibration matrix (the FDBV-029/030/031
				// loops; zero mismatches): the extent anchors on the PIVOT SPAN [begin pivot (excl when orEqual),
				// end pivot (incl when orEqual)) - a proper span records even when it returned nothing (the
				// phantom rule), a degenerate pivot span records nothing, resolution slack beyond either pivot is
				// not a dependency. On top of the span: returned keys are always covered; the GAP between a pivot
				// and the served keys on its side is always covered (a backward begin walk below its pivot, the
				// reversed serve gap between the end pivot and the highest returned key); a satisfied limit clamps
				// the far scan bound (forward, under a forward-resolving end); and on a reverse-truncated read the
				// scan span clamps to the lowest returned key ONLY under a canonical or backward begin (offset
				// <= 1) - a forward begin walk keeps the whole span, down to its pivot bound, a dependency.
				var beginPivotBound = beginSelector.OrEqual ? beginSelector.Key.GetSuccessor(this.Arena) : beginSelector.Key;
				var endPivotBound = endSelector.OrEqual ? endSelector.Key.GetSuccessor(this.Arena) : endSelector.Key;

				var from = beginPivotBound;
				if (!lowestReturned.IsNull && lowestReturned < from) from = lowestReturned;

				Key to;
				if (limitHit && !reversed && endSelector.Offset >= 1)
				{ // truncated at the high end under a forward-resolving end: keys above the highest returned key
					// cannot change the result - the scan never reached them
					to = highestReturned.GetSuccessor(this.Arena);
				}
				else
				{
					to = endPivotBound;
					if (!highestReturned.IsNull)
					{ // returned keys stay covered even above the end pivot (a negative end offset resolves below its pivot)
						var returnedTo = highestReturned.GetSuccessor(this.Arena);
						if (returnedTo > to) to = returnedTo;
					}
				}

				if (!lowestReturned.IsNull)
				{ // pivot-to-served gap zones, both sides (no-ops when the pivot sits inside the served span)
					MarkRange(lowestReturned, beginPivotBound, merged);
					MarkRange(endPivotBound, highestReturned, merged);
				}

				if (limitHit && reversed && beginSelector.Offset <= 1)
				{ // truncated at the LOW end under a canonical or backward begin: the scan span clamps to the
					// lowest returned key. A FORWARD begin walk (offset > 1) voids the clamp instead: the walked
					// keys steer where the serve starts, so the whole span below the served keys stays a dependency.
					from = lowestReturned;
				}

				MarkRange(from, to, merged);
			}

			private void MarkRange(Key fromInclusive, Key toExclusive, bool merged)
			{
				if (fromInclusive >= toExclusive) return;
				if (merged)
				{
					MarkMergedRangeReadConflict(fromInclusive, toExclusive);
				}
				else
				{
					this.ReadConflicts.Mark(fromInclusive, toExclusive);
				}
			}

			/// <summary>Marks the read conflict of a merged-view read (selector or range): the given extent MINUS the segments the transaction's own INDEPENDENT writes fully determine (a Set, a Clear, a versionstamped set, or an atomic over one of those); a DEPENDENT atomic chain reads through to the committed value, so its segment stays a conflict.</summary>
			/// <remarks>fdb's read-conflict tracking is scan-based, not presence-based (7.4.6 fdbclient/WriteMap.cpp OperationStack::isDependent): reading over a dependent atomic reads the database for keys AND values alike, so the exemption is identical for selector and range reads - there is no "atomics are local under selector resolution" rule (that under-conflicted every non-CompareAndClear atomic, FDBV-039).</remarks>
			private void MarkMergedRangeReadConflict(Key fromInclusive, Key toExclusive)
			{
				var cursor = fromInclusive;
				foreach (var entry in this.Mutations.IterateOrdered())
				{
					if (entry.Begin >= toExclusive) break;
					if (entry.End <= cursor) continue;
					var mutation = entry.Value;
					Contract.Debug.Assert(mutation != null); // the write map only stores real mutation stacks
					if (!mutation!.IsKv() && !mutation.IsRange())
					{
						// fdb WriteMap OperationStack::isDependent (7.4.6 fdbclient/WriteMap.cpp:49): an own write is
						// INDEPENDENT - its value is known WITHOUT the committed data, so its segment is subtracted from
						// the read-conflict range - only when a Set, Clear, or versionstamped set gives the coalesced
						// stack a DB-free base. A chain of pure atomics (Add/Max/Min/Bit*/Byte*/AppendIfFits/CompareAndClear)
						// reads THROUGH to the committed value (DEPENDENT) and stays a conflict. fdb's read-conflict
						// tracking is SCAN-based, not presence-based: reading over a dependent atomic reads the DB whether
						// the read returns keys (getKey) or values, so an own atomic never makes a selector read local
						// (FDBV-039 - the removed atomicsAreLocal exemption wrongly did, catching only CompareAndClear).
						// An atomic over an own Clear/Set is independent (the clear/set is the base, WriteMap.cpp:103),
						// found by scanning the chain for a structural op below the atomic head.
						bool independent = false;
						for (var m = mutation; m != null; m = m.Next)
						{
							if (m.Op is Operation.Set or Operation.Clear or Operation.ClearRange or Operation.VersionStampedKey or Operation.VersionStampedValue)
							{
								independent = true;
								break;
							}
						}
						if (!independent)
						{
							continue; // dependent atomic chain: the read reads the committed value, so its segment stays a conflict
						}
					}
					var segBegin = entry.Begin > cursor ? entry.Begin : cursor;
					if (segBegin > cursor)
					{
						this.ReadConflicts.Mark(cursor, segBegin);
					}
					var segEnd = entry.End < toExclusive ? entry.End : toExclusive;
					if (segEnd > cursor)
					{
						cursor = segEnd;
					}
				}
				if (cursor < toExclusive)
				{
					this.ReadConflicts.Mark(cursor, toExclusive);
				}
			}

			/// <summary>Finds the mutation entry covering a key (its begin at or before the key, its END strictly after), or null.</summary>
			/// <remarks>Unlike <c>TryGetValue</c>, this never matches a range whose exclusive end equals the key.</remarks>
			private Mutation? FindCoveringMutation(Key key)
			{
				foreach (var entry in this.Mutations.IterateOrdered())
				{
					if (entry.Begin > key) break;
					if (entry.End > key) return entry.Value;
				}
				return null;
			}

			/// <summary>Computes the ordered list of keys visible in the merged view (committed snapshot + local mutations), without any side effect on the mutation log or the conflict ranges.</summary>
			private List<Key> GetMergedVisibleKeys(bool accessSystemKeys)
			{
				var candidates = new SortedSet<Key>();
				foreach (var kv in this.Inner.Data.IterateOrdered())
				{
					if (!accessSystemKeys && kv.Key.IsSystemKey()) continue;
					candidates.Add(kv.Key);
				}
				foreach (var entry in this.Mutations.IterateOrdered())
				{
					var mutation = entry.Value;
					if (mutation is null || mutation.Op is Operation.Invalid) continue;
					if (mutation.Op is Operation.Clear or Operation.ClearRange && mutation.Next is null) continue; // pure clears never create keys; Clear-HEADED CHAINS (clear then atomic) can
					var key = entry.Begin;
					if (!accessSystemKeys && key.IsSystemKey()) continue;
					candidates.Add(key);
				}

				var visible = new List<Key>(candidates.Count);
				foreach (var key in candidates)
				{
					if (IsVisibleInMergedView(key)) visible.Add(key);
				}
				return visible;
			}

			/// <summary>Checks whether a key is a PRESENT boundary key for selector / range-bound resolution (fdb 7.4.6 <c>RYWIterator::is_kv()</c>): the offset walk counts the KEY, not its coalesced value.</summary>
			/// <remarks>This is the selector-walk present-set (used only by <see cref="GetMergedVisibleKeys"/> -> <see cref="ResolveMerged"/>), NOT read content. A key with a pending write is present unless its net effect is a pure clear (<c>CLEARED_RANGE</c>). Read CONTENT (which excludes a CompareAndClear'd key) is a separate scan.</remarks>
			private bool IsVisibleInMergedView(Key key)
			{
				var mutation = FindCoveringMutation(key);
				if (mutation != null)
				{
					if (mutation.IsKv()) return !mutation.Parameter.IsNull; // a single-key Clear (CLEARED_RANGE) is IsKv() with a Nil parameter; a Set (INDEPENDENT_WRITE) is present
					if (mutation.IsRange()) return false;                   // a ClearRange (CLEARED_RANGE) is EMPTY
					if (mutation.IsAtomic())
					{
						// is_kv semantics: a pending atomic is a DEPENDENT_WRITE (or an INDEPENDENT_WRITE when it
						// coalesces over a cleared span, fdb WriteMap.cpp), both classified KV = a PRESENT boundary key.
						// The walk counts the KEY, not its coalesced value, so a CompareAndClear that would erase the key
						// (its value coalesces to absent) STILL counts for resolution; only read CONTENT (a separate
						// scan via the coalesced value) drops it. Do not coalesce here (FDBV-036).
						return true;
					}
					return true;
				}
				return this.Inner.ContainsKey(key);
			}

			/// <summary>Checks whether a key is present in the merged read CONTENT: the coalesced atomic chain is non-null. A CompareAndClear'd key is absent from content even though it is a present boundary key for the is_kv walk (<see cref="IsVisibleInMergedView"/>) - this is the kv() vs is_kv() split (FDBV-037).</summary>
			private bool IsContentPresentInMergedView(Key key)
			{
				var mutation = FindCoveringMutation(key);
				if (mutation != null)
				{
					if (mutation.IsKv()) return !mutation.Parameter.IsNull; // a single-key Clear is IsKv() with a Nil parameter
					if (mutation.IsRange()) return false;
					if (mutation.IsAtomic())
					{
						// evaluate the chain over the committed value; the result decides content visibility (e.g. CompareAndClear can erase the key)
						var value = (mutation.Op is Operation.Set or Operation.Clear) ? default : this.Inner.Read(key);
						for (var m = mutation; m != null; m = m.Next)
						{
							value = CoalesceAtomic(this.Arena, value, m);
						}
						return !value.IsNull;
					}
					return true;
				}
				return this.Inner.ContainsKey(key);
			}

			public FdbRangeChunk GetRange<TCursor>(
				Selector beginInclusive,
				Selector endExclusive,
				FdbRangeOptions options,
				int iteration,
				bool ryw,
				bool snapshotRead,
				bool accessSystemKeys)
				where TCursor : struct, IFdbCommittedCursor
			{

				// if there are no writes, it's the same thing as a snapshot read
				if (!ryw || this.WriteConflicts.Count == 0)
				{
					var begin = this.Inner.Resolve<TCursor>(beginInclusive, accessSystemKeys);
					var end = this.Inner.Resolve<TCursor>(endExclusive, accessSystemKeys);

					var res = begin.Equals(end)
						? new FdbRangeChunk([], false, iteration, options, default, default, 0, SliceOwner.Nil) // empty range (an empty read records no dependency: the marking below no-ops)
						: this.Inner.GetRange<TCursor>(begin, end, options, iteration);

					if (!snapshotRead)
					{
						// note: a reverse chunk is ordered high-to-low, so res.Last is its LOWEST key
						MarkRangeReadConflict(beginInclusive, endExclusive,
							lowestReturned: res.Count > 0 ? new Key(options.IsReversed ? res.Last : res.First) : default,
							highestReturned: res.Count > 0 ? new Key(options.IsReversed ? res.First : res.Last) : default,
							limitHit: options.Limit is not null && res.Count == options.Limit,
							reversed: options.IsReversed,
							merged: false);
					}

					return res;
				}
				else
				{
					var iter = GetIterator<TCursor>();

#if FULL_DEBUG
					Kenobi($"* #{this.Id} GetRange({beginInclusive}, {endExclusive}, ...)");
					Kenobi($"** #{this.Id} Inner: rv {this.Inner.Version}");
					foreach (var kv in this.Inner.Data.IterateOrdered())
					{
						Kenobi($"** #{this.Id} - {kv.Key:K} = {kv.Value:V}");
					}
					Kenobi($"** #{this.Id} Mutations: rv {this.Inner.Version}");
					foreach (var entry in this.Mutations.IterateOrdered())
					{
						Kenobi($"** #{this.Id} - {entry.Begin:K} ~ {entry.End:K} = {entry.Value}");
					}
#endif

				// resolve BOTH bounds on the merged view first (ResolveMerged is the only correct selector
					// semantics over pending writes; the onion iterator's per-layer Seek is exact just for the
					// 0/+1 offsets used below to position the scan), then scan between the resolved keys
					// a range BOUND resolves to the raw is_kv anchor (contentSkip: false, FDBV-038); the scan below
					// excludes CAC'd keys on its own, so a content-skipped bound would over-shrink/over-grow the range
					var endKey = ResolveMerged(endExclusive, accessSystemKeys, contentSkip: false);
					if (endKey.IsNull)
					{ // the end selector resolves past the last visible key: the merged scan is only bounded by the system space
						endKey = SpecialKeys.SystemPrefix;
					}
					var beginKey = ResolveMerged(beginInclusive, accessSystemKeys, contentSkip: false);
					Kenobi($"*** #{this.Id} BeginKey: {beginKey:K}, EndKey: {endKey:K}");

					var res = new List<KeyValuePair<Slice, Slice>>();
					long sum = 0;
					bool hasMore = false;
					int limit = options.Limit ?? 0;
					bool reversed = options.IsReversed;
					// position at the first merged key at/after the resolved begin (the 0/+1 form the seek handles exactly)
					if (beginKey < endKey && iter.Seek(new Selector(beginKey, orEqual: false, offset: 1)))
					{
						Kenobi($"*** #{this.Id} BeginKey: {iter.Current.Key:K}");

						//REVIEW: should the endKey be included in the range?
						// and start scanning from there!

						do
						{
							var cur = iter.Current;
							if (cur.Key.IsNull || cur.Key >= endKey) goto complete;
							if (!reversed && limit != 0 && res.Count >= limit)
							{ // forward: the limit truncates at the high end; a reverse read must scan the whole range first (see below)
								hasMore = true;
								goto complete;
							}

							Kenobi($"**** #{this.Id} => Take {cur.Key:K} = {cur.Value:V}");
							res.Add(KeyValuePair.Create(cur.Key.Slice, cur.Value.Slice));
							sum += cur.Key.Count;
							sum += cur.Value.Count;
						}
						while (iter.Next());
					}
				complete:

					if (reversed)
					{ // a reverse read returns the range from its high end; the limit truncates at the low end
						res.Reverse();
						if (limit != 0 && res.Count > limit)
						{
							hasMore = true;
							for (int i = limit; i < res.Count; i++)
							{
								sum -= res[i].Key.Count + res[i].Value.Count;
							}
							res.RemoveRange(limit, res.Count - limit);
						}
					}

					var first = res.Count > 0 ? res[0].Key : default;
					var last = res.Count > 0 ? res[^1].Key : default;

					if (!snapshotRead)
					{
						// note: res is already normalized (a reverse read is ordered high-to-low)
						var lowest = res.Count > 0 ? new Key(res[reversed ? ^1 : 0].Key) : default;
						MarkRangeReadConflict(beginInclusive, endExclusive,
							lowestReturned: lowest,
							highestReturned: res.Count > 0 ? new Key(res[reversed ? 0 : ^1].Key) : default,
							limitHit: limit != 0 && res.Count == limit, // NOT hasMore: a limit satisfied exactly at the end of the data clamps the result all the same
							reversed: reversed,
							merged: true);

						// the documented read-your-writes caveat, range flavor: an atomic chain whose key the read
						// actually RETURNED is converted into a set of the read value (keys the scan merely walked
						// past keep their chain, and apply over the committed value at commit time); this runs AFTER
						// the conflict marking above, which must see the chains intact
						foreach (var kv in res)
						{
							if (FindCoveringMutation(new Key(kv.Key))?.IsAtomic() == true)
							{
								// DETACH the key and value (heap copies) before they enter the mutation log: a
								// null-arena wrapper around arena-backed bytes would be waved through the commit-time
								// interning boundary (null means "immutable, safe to keep"), and the committed store
								// would end up aliasing recycled transaction-arena memory
								var key = new Key(kv.Key.Copy());
								this.Mutations.Mark(key, key.GetSuccessor(this.Arena), Mutation.Set(new Value(kv.Value.Copy())));
							}
						}
					}

					Kenobi($"**** #{this.Id} Got {res.Count} results ({sum:N0} bytes)");

					// after the conflict marking and the atomic conversion above, which must see the raw keys
					Snapshot.ApplyFetchMode(res, options);

					return new FdbRangeChunk(res.ToArray(), hasMore, iteration, options, first, last, checked((int) sum), SliceOwner.Nil);
				}
			}

			/// <summary>Streaming twin of <see cref="GetRange{TCursor}"/>: on the read-only fast path it streams the committed range straight to <paramref name="visitor"/> over spans (no items array), identical bounds/limit/First-Last/conflict semantics; on the merged (local-write) path it reuses the chunk pipeline and replays its items. This is what a zero-copy aggregate scan rides.</summary>
			public FdbRangeResult VisitRange<TCursor, TState>(
				Selector beginInclusive,
				Selector endExclusive,
				FdbRangeOptions options,
				int iteration,
				bool ryw,
				bool snapshotRead,
				bool accessSystemKeys,
				TState state,
				FdbCommittedRangeVisitor<TState> visitor)
				where TCursor : struct, IFdbCommittedCursor
			{
				// fast path: no writes => same as a snapshot read, stream directly off the committed store
				if (!ryw || this.WriteConflicts.Count == 0)
				{
					var begin = this.Inner.Resolve<TCursor>(beginInclusive, accessSystemKeys);
					var end = this.Inner.Resolve<TCursor>(endExclusive, accessSystemKeys);

					var res = begin.Equals(end)
						? new FdbRangeResult(0, false, iteration, options, default, default, 0) // empty range records no dependency
						: this.Inner.VisitRange<TState>(begin, end, options, iteration, state, visitor);

					if (!snapshotRead)
					{
						// note: a reverse read reports First as its HIGHEST key, so the lowest returned is Last (same as GetRange)
						MarkRangeReadConflict(beginInclusive, endExclusive,
							lowestReturned: res.Count > 0 ? new Key(options.IsReversed ? res.Last : res.First) : default,
							highestReturned: res.Count > 0 ? new Key(options.IsReversed ? res.First : res.Last) : default,
							limitHit: options.Limit is not null && res.Count == options.Limit,
							reversed: options.IsReversed,
							merged: false);
					}

					return res;
				}

				// merged path (local writes): reuse the exact chunk pipeline - conflict marking and the RYW atomic
				// conversion happen inside it - then replay its already fetch-masked items to the visitor. Not the
				// O(1) path (the merge materializes), but the aggregate-scan goal targets the read-only fast path.
				var chunk = GetRange<TCursor>(beginInclusive, endExclusive, options, iteration, ryw, snapshotRead, accessSystemKeys);
				foreach (var kv in chunk.Items)
				{
					if (!visitor(state, kv.Key.Span, kv.Value.Span)) break;
				}
				return new FdbRangeResult(chunk.Count, chunk.HasMore, chunk.Iteration, chunk.Options, chunk.First, chunk.Last, chunk.TotalBytes);
			}

			public OnionIterator<TCursor> GetIterator<TCursor>()
				where TCursor : struct, IFdbCommittedCursor
			{
				return new OnionIterator<TCursor>((IFdbCommittedStore<TCursor>) this.Inner.Data, this.Mutations, this.Arena, this.Id);
			}

			/// <summary>Sums the exact key+value bytes of the committed snapshot over a range (FakeDb's deterministic stand-in for the real sampling estimator).</summary>
			public long ComputeExactRangeSize(Key begin, Key end)
			{
				long sum = 0;
				foreach (var kv in this.Inner.ScanRange(begin, end))
				{
					sum += kv.Key.Count + kv.Value.Count;
				}
				return sum;
			}

			/// <summary>Walks the committed snapshot emitting a split point every ~<paramref name="chunkSize"/> of exact key+value bytes, both endpoints always included.</summary>
			/// <remarks>The returned slices are detached copies: the caller owns them beyond the transaction, and the inputs live in the transaction's recyclable scratch arena.</remarks>
			public Slice[] ComputeSplitPoints(Key begin, Key end, long chunkSize)
			{
				var splits = new List<Slice> { begin.Slice.Copy() };
				long accumulated = 0;
				foreach (var kv in this.Inner.ScanRange(begin, end))
				{
					accumulated += kv.Key.Count + kv.Value.Count;
					if (accumulated >= chunkSize)
					{
						if (!kv.Key.Slice.Equals(splits[^1]))
						{
							splits.Add(kv.Key.Slice.Copy());
						}
						accumulated = 0;
					}
				}
				splits.Add(end.Slice.Copy());
				return splits.ToArray();
			}

			/// <summary>Conflicting read ranges collected by the failed commit, when the <see cref="FdbTransactionOption.ReportConflictingKeys"/> option is set; served through the <c>\xff\xff/transaction/conflicting_keys/</c> special keyspace.</summary>
			public List<KeyValuePair<Key, Key>>? ConflictingReadRanges { get; private set; }

			public (Snapshot Snapshot, VersionStamp Stamp) ApplyMutations(long commitVersion, Snapshot snapshot, bool reportConflictingKeys = false)
			{
				var conflicts = snapshot.Conflicts;

				if (this.ReadConflicts.Count > 0)
				{
					List<KeyValuePair<Key, Key>>? conflicting = null;
					foreach (var x in this.ReadConflicts)
					{
						if (conflicts.Intersect(x.Begin, x.End, this.Version, (v, cv) => v > cv, out var match))
						{
							Kenobi($"$$$ #{this.Id} Read conflict for {x.Begin:K}->{x.End:K} @ {this.Version} by {match.Begin:K}->{match.End:K} @ {match.Value}!");
							if (!reportConflictingKeys)
							{
								throw new FdbException(FdbError.NotCommitted, $"Read conflict for `{FdbKey.Dump(x.Begin.Span)}` -> `{FdbKey.Dump(x.End.Span)}` @ {this.Version} by `{FdbKey.Dump(match.Begin.Span)}` -> `{FdbKey.Dump(match.End.Span)}` @ {match.Value}!");
							}
							// keep scanning: the report must contain EVERY conflicting read range, not just the first
							(conflicting ??= [ ]).Add(new(x.Begin, x.End));
						}
					}
					if (conflicting != null)
					{
						this.ConflictingReadRanges = conflicting;
						throw new FdbException(FdbError.NotCommitted, $"Read conflicts on {conflicting.Count} range(s) @ {this.Version} (reported through the conflicting-keys special keyspace)");
					}
				}

				var prevData = snapshot.Data;
				var newData = prevData.Copy();
				try
				{

					var arena = snapshot.Arena;

					if (this.WriteConflicts.Count > 0)
					{
						conflicts = conflicts.Copy();
						foreach (var x in this.WriteConflicts)
						{
							conflicts.Mark(arena.InternKey(x.Begin), arena.InternKey(x.End), commitVersion);
						}
					}

					var stamp = MakeVersionStamp(commitVersion, 0);
					Kenobi($"$ #{this.Id} apply trans #{this.Id} rv {snapshot.Version} => cv {commitVersion}");

					// a versionstamped KEY whose placeholder sat inside a clear-range submitted EARLIER in this same
					// transaction: the clear was split around the placeholder position, but completing the stamp moves
					// the key to its final slot, which can land in a remainder of that very clear. Since the clear was
					// submitted first, the later stamped write must survive it - so defer these completions until after
					// every clear-range has been applied, out of reach of the remainders. (versionstamp fuzz FDBV-034)
					List<(Key Key, Value Value)>? deferredStampedKeys = null;

					foreach (var entry in this.Mutations.IterateOrdered())
					{
						var mutation = entry.Value!;

						if (mutation.IsKv())
						{
							if (mutation.Parameter.IsNull)
							{ // clear
								Kenobi($"$$ #{this.Id} clear {entry.Begin:K}");
								newData.Remove(entry.Begin);
							}
							else
							{ // set
								// try to reuse previous key
								Key k;
								if (newData.TryGetKeyValue(entry.Begin, out var kv))
								{
									if (kv.Value.Equals(mutation.Parameter))
									{ // value hasn't changed!
										continue;
									}
									k = kv.Key;
								}
								else
								{
									k = arena.InternKey(entry.Begin);
								}

								var v = arena.InternValue(mutation.Parameter);
								Kenobi($"$$ #{this.Id} set {k:K} = {v:V}");
								newData[k] = v;
							}
						}
						else if (mutation.IsRange())
						{
							var range = arena.InternKeyRange(entry.Begin, entry.End);
							Kenobi($"$$ #{this.Id} clearRange {range}");

							_ = newData.RemoveRange(range.Begin, range.End);
						}
						else if (mutation.IsAtomic())
						{
							if (mutation.Op is (Operation.Set or Operation.Clear)
							 || !newData.TryGetKeyValue(entry.Begin, out var kv))
							{
								kv = new(
									arena.InternKey(entry.Begin),
									default
								);
							}

							Kenobi($"$$ #{this.Id} atomic {mutation}");

							// apply the WHOLE chain in submission order (api 520+ stamp semantics): a stamped KEY
							// materializes immediately against the commit stamp (identical placeholders complete to the
							// same key, so the last submitted wins, like any other mutation), a stamped VALUE completes
							// into the running value, and everything else coalesces on top. The overlay key of a stamped
							// KEY (placeholder + offset suffix) is synthetic and never lands in the committed data; the
							// only non-stamped links such a chain can carry are Clear anchors from a covering range wipe.
							var value = kv.Value;
							bool stampedKey = false;
							// the head op tells us whether this chain was anchored by a clear submitted earlier (a covered
							// wipe): only those completions need deferring past the clear-range remainders (see the preamble)
							bool clearHeaded = mutation.Op is Operation.Clear or Operation.ClearRange;
							Key stampedKeyDst = default;
							Value stampedKeyVal = default;
							do
							{
								switch (mutation.Op)
								{
									case Operation.VersionStampedKey:
									{
										// offset in last 32 bits of the overlay key
										int len = kv.Key.Count - 4;
										if (len < 0) throw new InvalidOperationException("TODO: malformed offset in VersionStampedKey");
										int offset = kv.Key.Slice.Substring(len).ToInt32();

										var tmp = arena.AllocateKey(len);
										kv.Key.Span[..^4].CopyTo(tmp.UnsafeSpan);
										stamp.WriteTo(tmp.UnsafeSpan.Slice(offset));

										// last identical placeholder wins; the actual store happens after the walk (below)
										stampedKeyDst = tmp;
										stampedKeyVal = arena.InternValue(mutation.Parameter);
										stampedKey = true;
										break;
									}

									case Operation.VersionStampedValue:
									{
										// offset in last 32 bits of the parameter
										int len = mutation.Parameter.Count - 4;
										if (len < 0) throw new InvalidOperationException("TODO: malformed offset in VersionStampedValue");
										int offset = mutation.Parameter.Slice.Substring(len).ToInt32();

										var tmp = arena.AllocateValue(len);
										mutation.Parameter.Span[..^4].CopyTo(tmp.UnsafeSpan);
										stamp.WriteTo(tmp.UnsafeSpan.Slice(offset));

										value = tmp;
										break;
									}

									default:
									{
										value = CoalesceAtomic(arena, value, mutation);
										break;
									}
								}
								mutation = mutation.Next;
							}
							while (mutation != null);

							if (stampedKey)
							{
								// the synthetic overlay key never lands; only the completed key does. If this chain was
								// anchored by an earlier clear (a covered wipe), that clear was split around the placeholder
								// and its remainders are still to be applied below - defer the completed key past them so a
								// remainder cannot re-wipe the relocated slot (FDBV-034). Uncovered stamped keys land now.
								if (clearHeaded)
								{
									(deferredStampedKeys ??= new()).Add((stampedKeyDst, stampedKeyVal));
								}
								else
								{
									newData[stampedKeyDst] = stampedKeyVal;
								}
							}
							else if (value.IsNull)
							{
								newData.Remove(kv.Key);
							}
							else
							{
								// the coalesced value can be a passthrough of a chain anchor's Parameter (backed by
								// the transaction's recyclable arena): intern it before it becomes committed state
								newData[kv.Key] = arena.InternValue(value);
							}
						}
						else
						{
							throw new NotSupportedException();
						}
					}

					// completed stamped keys that were covered by an earlier clear land now, after every clear-range
					// remainder has been applied - so a remainder split off the covering wipe cannot re-wipe them (FDBV-034)
					if (deferredStampedKeys != null)
					{
						foreach (var (k, v) in deferredStampedKeys)
						{
							newData[k] = v;
						}
					}

					Kenobi($"$ committed trans #{this.Id} rv {snapshot.Version} => cv {commitVersion}: {prevData.Count:N0} keys => {newData.Count:N0} keys");

	#if CHECK_INVARIANTS
					// invariant checks: all keys & values are using the snapshot's arena (or null)
					foreach (var kv in newData.IterateOrdered())
					{
						if (kv.Key.Arena != arena & kv.Key.Arena != null) throw new InvalidOperationException($"Invariant broken: key '{kv.Key}' uses an unexpected arena!");
						if (kv.Key.IsNull) throw new InvalidOperationException("Invariant broken: illegal 'null' key!");
						if (kv.Value.Arena != arena && kv.Value.Arena != null) throw new InvalidOperationException($"Invariant broken: value '{kv.Value}' (of key '{kv.Key}') uses an unexpected arena!");
						if (kv.Value.IsNull) throw new InvalidOperationException($"Invariant broken: key '{kv.Key}' as illegal null value!");
					}
	#endif

					var updated = new Snapshot(commitVersion, newData, conflicts, stamp, arena);
					return (updated, stamp);
				}
				catch
				{
					// a copy that will never publish must not leak its backend generation (with the fdblite
					// backend, an abandoned engine writer whose allocations roll back); no-op for in-memory data
					newData.Discard();
					throw;
				}
			}

		}

		public Dictionary<long, Snapshot> Snapshots { get; } = new();

		/// <summary>Returns the current snapshot of the database</summary>
		/// <remarks>
		/// <para>Each snapshot is immutable, and a new snapshot is produced whenever a transaction successfully commits.</para>
		/// <para>Please note that, by the time this property returns, the snapshot may already have been replaced by a more recent one!</para>
		/// </remarks>
		public Snapshot CurrentSnapshotUnsafe { get; set; }

		/// <summary>Returns a map of all currently monitored watches in the cluster</summary>
		/// <remarks>
		/// <para>This returns a copy of the list of active watches.</para>
		/// <para>Please note that, by the time this property returns, new watches may have been added, and existing ones may already have triggered</para>
		/// </remarks>
		public Dictionary<Slice, List<WatchNode>> ActiveWatches { get; } = new(Slice.Comparer.Default);

		protected ReaderWriterLockSlim GlobalLock { get; } = new();

		protected long ReadVersion { get; set; }

		/// <summary>Minimum API version supported by this implementation</summary>
		public const int MIN_API_VERSION = 610;

		/// <summary>Maximum API version supported by this implementation</summary>
		public const int MAX_API_VERSION = 730;

		/// <summary>Default API version used by new instances, unless configured otherwise during setup</summary>
		public const int DEFAULT_API_VERSION = 710;

		/// <summary>API version that is used by this instance</summary>
		public int ApiVersion { get; }

		/// <summary>API version of the simulated server</summary>
		public int ProtocolVersion { get; }

		/// <summary>Time source of the simulated cluster, used to schedule the retry backoff (see <see cref="RetryDelayMaximum"/>)</summary>
		/// <remarks>A test that installs a fake provider (e.g. <c>NodaTimeProvider</c> over a <c>FakeTimeProvider</c>) gets a
		/// simulated cluster whose retry timing advances with virtual time, instead of blocking the wall clock.</remarks>
		internal TimeProvider Time { get; }

		/// <summary>Lazily-created watch fault-injection facet, or <see langword="null"/> until <see cref="Buggify"/> is first touched (the buggify-off default costs one null check on the commit path).</summary>
		private FakeDbBuggify? BuggifyState { get; set; }

		/// <summary>Test-only fault-injection facet ("buggify") that reproduces the spurious and missed watch fires a real fdb cluster is allowed to produce; the shipped default is inert (buggify off).</summary>
		/// <remarks>See <see cref="FakeDbBuggify"/>. Deliberately an instance facet holding per-store injection state (suppression counters,
		/// chaos config, timers): probes observe (that is <see cref="FakeDbDebugger"/>, which stays read-only), buggify injects.</remarks>
		[PublicAPI]
		public FakeDbBuggify Buggify => this.BuggifyState ??= new FakeDbBuggify(this);

		/// <summary>Base delay before the first retry after a retryable error (when <see cref="RetryDelayMaximum"/> enables the backoff); defaults to 1 ms</summary>
		public TimeSpan RetryDelayInitial { get; set; } = TimeSpan.FromMilliseconds(1);

		/// <summary>Cap on the exponential retry backoff. <b>Defaults to zero, which disables the wait entirely</b> - a retryable
		/// error retries immediately, so normal tests run at full speed. Raise it (e.g. 1 s) to emulate realistic recovery
		/// timing in a "broken cluster" test; the delay rides <see cref="Time"/>, so under a fake clock it costs zero real time.</summary>
		/// <remarks>The per-transaction <c>MaxRetryDelay</c> option, when set, only tightens this cap (it never enables the backoff).</remarks>
		public TimeSpan RetryDelayMaximum { get; set; } = TimeSpan.Zero;

		/// <summary>Opens a store over a given storage backend.</summary>
		/// <remarks>The backend supplies the committed state, its durability and its retention window; everything else - read-your-writes, conflict detection, watches, versionstamps - is this class and is identical whichever backend is plugged in.</remarks>
		protected FdbEmulatedDatabase(IFdbStorageBackend backend, int apiVersion, int protocolVersion, long initialVersion, TimeProvider? time)
		{
			Contract.NotNull(backend);
			if (protocolVersion < MIN_API_VERSION) throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, "Server protocol version cannot be less than the minimum supported version");
			if (protocolVersion > MAX_API_VERSION) throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, "Server protocol version cannot be greater than the maximum supported version");
			if (apiVersion == 0)
			{
				apiVersion = Math.Min(DEFAULT_API_VERSION, protocolVersion);
			}
			if (apiVersion < MIN_API_VERSION) throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, "API version cannot be less than the minimum supported version");
			if (apiVersion > protocolVersion) throw new ArgumentOutOfRangeException(nameof(apiVersion), apiVersion, "API version cannot be greater than the maximum supported version");

			this.ApiVersion = apiVersion;
			this.ProtocolVersion = protocolVersion;
			this.Time = time ?? TimeProvider.System;
			this.Backend = backend;

			var snapshot = backend.CreateInitialSnapshot(initialVersion > 0 ? initialVersion : 0xfdb1337000000);
			Contract.Debug.Assert(snapshot != null);
			this.Snapshots[snapshot.Version] = snapshot;
			this.CurrentSnapshotUnsafe = snapshot;
			this.ReadVersion = snapshot.Version;
		}

		/// <summary>Storage this store's committed state lives in</summary>
		protected IFdbStorageBackend Backend { get; }

		[Conditional("FULL_DEBUG")]
		[System.Diagnostics.Conditional("FULL_DEBUG")]
		private static void Kenobi(string msg)
		{
			// [Conditional] strips the call (and its argument evaluation) outside a FULL_DEBUG build, so the
			// merge/resolve trace strings - and any Copy* the interpolations touch - cost nothing in normal builds
			System.Diagnostics.Debug.WriteLine(msg);
			Console.WriteLine(msg);
		}

		private static VersionStamp MakeVersionStamp(long version, ushort order)
		{
			Contract.Debug.Requires(version >= 0);
			var stamp = VersionStamp.Complete((ulong) version, order);
			Contract.Debug.Ensures(!stamp.IsIncomplete);
			return stamp;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsSystemKey(Slice key) => key.Count != 0 && key[0] == 0xFF;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsSystemReadableKey(Slice key) => IsSystemKey(key) && !IsMetadataVersionKey(key);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsSystemReadableKey(in Key key) => key.IsSystemKey() && !IsMetadataVersionKey(key);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsSystemWritableKey(Slice key) => IsSystemKey(key) && !IsMetadataVersionKey(key);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsSystemWritableKey(in Key key) => key.IsSystemKey() && !IsMetadataVersionKey(key);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsMetadataVersionKey(Slice key) => key.Equals(SpecialKeys.SystemMetadataVersion.Slice);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsMetadataVersionKey(in Key key) => key.Equals(SpecialKeys.SystemMetadataVersion);

		private static Exception ErrorCannotAccessSystemKeys() => new InvalidOperationException("TODO: cannot access system keys");

		internal static void ComputeAtomicAdd(Span<byte> buffer, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			Contract.Debug.Requires(buffer.Length == right.Length);
			if (left.Length == 0)
			{
				right.CopyTo(buffer);
				return;
			}

			int acc = 0;
			for (int i = 0; i < buffer.Length; i++)
			{
				acc += i < left.Length ? left[i] : 0;
				acc += right[i];
				buffer[i] = (byte) acc;
				acc >>= 8;
			}
		}

		internal static void ComputeAtomicBitAnd(Span<byte> buffer, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			Contract.Debug.Requires(buffer.Length == right.Length);
			if (left.Length == 0)
			{
				right.CopyTo(buffer);
				return;
			}
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = (byte) ((i < left.Length ? left[i] : 0) & right[i]);
			}
		}

		internal static void ComputeAtomicBitOr(Span<byte> buffer, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			Contract.Debug.Requires(buffer.Length == right.Length);
			if (left.Length == 0)
			{
				right.CopyTo(buffer);
				return;
			}
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = (byte) ((i < left.Length ? left[i] : 0) | right[i]);
			}
		}

		internal static void ComputeAtomicBitXor(Span<byte> buffer, ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			Contract.Debug.Requires(buffer.Length == right.Length);
			if (left.Length == 0)
			{
				right.CopyTo(buffer);
				return;
			}
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = (byte) ((i < left.Length ? left[i] : 0) ^ right[i]);
			}
		}

		public void Dispose()
		{
			if (!this.IsClosed)
			{
				this.IsClosed = true;
				try
				{
					this.LifeTime.Cancel();
				}
				finally
				{
					this.CurrentSnapshotUnsafe.Arena?.Dispose();
					this.LifeTime.Dispose();
					this.Backend.Dispose();
				}
			}
		}

		public int GetApiVersion() => this.ApiVersion;

		public int GetMaxApiVersion() => this.ProtocolVersion;

		public double GetMainThreadBusyness() => throw new NotImplementedException();

		public Task<FdbProtocolVersion> GetServerProtocolVersionAsync(FdbProtocolVersion expectedVersion, CancellationToken ct)
		{
			ulong protocol = 0x0fdb00b000000000UL;

			int major = (this.ProtocolVersion / 100) % 10;
			int minor = (this.ProtocolVersion / 10) % 10;
			int build = (this.ProtocolVersion / 1) % 10;
			int dev = 0;

			int xyzd = (major << 12) | (minor << 8) | (build << 4) | dev;
			protocol |= ((ulong) xyzd) << 16;

			//BUGBUG: TODO: add the ObjectSerialize flag or not?

			return Task.FromResult(new FdbProtocolVersion(protocol));
		}

		public Task<Slice> GetClientStatus(CancellationToken ct)
		{
			var obj = new JsonObject();
			obj["Healthy"] = true;
			obj["ClusterID"] = "TODO:ClusterId";
			obj["Coordinators"] = JsonArray.ReadOnly.Empty; //TODO!
			obj["StorageServers"] = JsonArray.ReadOnly.Empty; //TODO!
			obj["CommitProxies"] = JsonArray.ReadOnly.Empty; //TODO!
			obj["GrvProxies"] = JsonArray.ReadOnly.Empty; //TODO!
			obj["Connections"] = JsonArray.ReadOnly.Empty; //TODO!
			obj["NumConnectionsFailed"] = 0;
			//TODO: more!

			var jsonBytes = obj.ToJsonSlice();
			return Task.FromResult(jsonBytes);
		}

		Task IFdbDatabaseHandler.RebootWorkerAsync(ReadOnlySpan<char> name, bool check, int duration, CancellationToken ct) => throw new NotSupportedException();

		Task IFdbDatabaseHandler.ForceRecoveryWithDataLossAsync(ReadOnlySpan<char> dcId, CancellationToken ct) => throw new NotSupportedException();

		Task IFdbDatabaseHandler.CreateSnapshotAsync(ReadOnlySpan<char> uid, ReadOnlySpan<char> snapCommand, CancellationToken ct) => throw new NotImplementedException();

		protected internal Task<ReadYourWritesSnapshot> StartNewSnapshot(Arena arena, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<ReadYourWritesSnapshot>(ct);
			using (this.GlobalLock.GetReadLock())
			{
				if (ct.IsCancellationRequested)
				{
					return Task.FromCanceled<ReadYourWritesSnapshot>(ct);
				}

				var snapshot = this.CurrentSnapshotUnsafe;
				Contract.Debug.Assert(snapshot != null && snapshot.Version == this.ReadVersion);
				this.Backend.Pin(snapshot.Version);
				return Task.FromResult(new ReadYourWritesSnapshot(snapshot, arena));
			}
		}

		protected internal Task<ReadYourWritesSnapshot> StartSnapshotAtVersion(Arena arena, long version, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<ReadYourWritesSnapshot>(ct);
			using (this.GlobalLock.GetReadLock())
			{
				if (ct.IsCancellationRequested)
				{
					return Task.FromCanceled<ReadYourWritesSnapshot>(ct);
				}
				if (!this.Snapshots.TryGetValue(version, out var snapshot))
				{ // outside the backend's retention window (or never a published version at all)
					return Task.FromException<ReadYourWritesSnapshot>(new FdbException(FdbError.TransactionTooOld, $"Version {version} is no longer retained by this store"));
				}
				this.Backend.Pin(snapshot.Version);
				return Task.FromResult(new ReadYourWritesSnapshot(snapshot, arena));
			}
		}

		internal async Task<long> Commit<TCursor>(TransactionHandler<TCursor> handler, CancellationToken ct)
			where TCursor : struct, IFdbCommittedCursor
		{
			ct.ThrowIfCancellationRequested();
			var snapshot = await handler.GetSnapshot(ct);

			List<WatchNode>? watchesToTrigger = null;

			using (this.GlobalLock.GetUpgradableReadLock())
			{
				ct.ThrowIfCancellationRequested();
				var rv = this.ReadVersion;
				var current = this.CurrentSnapshotUnsafe;
				Contract.Debug.Assert(current.Version == rv);
				long commitVersion;
				Snapshot? updated;
				VersionStamp stamp;
				if (snapshot.WriteConflicts.Count != 0)
				{
					commitVersion = rv + 1;
					(updated, stamp) = snapshot.ApplyMutations(commitVersion, current, handler.OptionReportConflictingKeys);
				}
				else
				{
					commitVersion = -1;
					updated = null;
					stamp = default;
				}
				try
				{
					Contract.Debug.Assert(snapshot != null && snapshot.Version <= rv);

					using (this.GlobalLock.GetWriteLock())
					{
						ct.ThrowIfCancellationRequested();

						// keys for watches that have completely triggered in this commit, and should be removed from the active list
						List<Slice>? deadWatchedKeys = null;

						// buggify: keys whose watch check is deferred (skipped) this commit, so one manual suppression is consumed at most once across the arm branch and the post-commit scan
						HashSet<Slice>? buggifyDecided = null;

						if (updated != null)
						{
							updated = PublishSnapshot(updated, commitVersion);
						}

						if (handler.Watches != null)
						{ // the transaction has some watches to add

							var source = updated ?? ((snapshot.Version < current.Version) ? current : null);

							foreach (var w in handler.Watches)
							{
								// set the version of the watch
								w.ReadVersion = snapshot.Version;
								w.CommitVersion = commitVersion;

								// the watch value _may_ already have changed
								if (source != null)
								{
									var updatedValue = source.Read(new(w.Key));
									if (!updatedValue.Slice.Equals(w.Value))
									{ // it was changed in between the creation of the watch and the commit!
										if (this.BuggifyState is null || !this.BuggifyState.ShouldDeferWatchCheck(w.Key, commitVersion, ref buggifyDecided))
										{
											// queue the watch for triggering
											(watchesToTrigger ??= [ ]).Add(w);
											continue;
										}
										// buggify: deferred check - fall through and register the node with its original baseline (a later real change self-heals it)
									}
									// it is still active
								}

	#if NET6_0_OR_GREATER
								ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(this.ActiveWatches, w.Key, out var exists);
								if (!exists)
								{
									slot = [ w ];
								}
								else
								{
									slot!.Add(w);
								}
	#else
								// CollectionsMarshal.GetValueRefOrAddDefault is not available: use a regular lookup + insert (which pays for two hash lookups instead of one)
								if (!this.ActiveWatches.TryGetValue(w.Key, out var slot))
								{
									this.ActiveWatches[w.Key] = [ w ];
								}
								else
								{
									slot.Add(w);
								}
	#endif
								Kenobi($"WWW watching key {w.Key} at rv {w.ReadVersion} and cv {w.CommitVersion}");
							}

							// the store now owns these nodes (registered or queued to trigger): whatever remains on the
							// handler at dispose/reset/failed-commit time is an unarmed watch and must be failed
							handler.Watches = null;
						}

						if (updated != null)
						{
							// look for all active watches and check them against the new write conflict map
							foreach (var kv in this.ActiveWatches)
							{
								var ver = updated.Conflicts.GetValueOrDefault(new(kv.Key), 0);
								Kenobi($"WWW checking watch key {kv.Key} for version {ver} (at rv {snapshot.Version} and cv {commitVersion})");

								for (int i = 0; i < kv.Value.Count; i++)
								{
									var w = kv.Value[i];
									if (ver > w.ReadVersion)
									{
										// we have to check if the value has changed!
										var updatedValue = updated.Read(new(w.Key));
										if (!w.Value.Equals(updatedValue.Slice))
										{
											if (this.BuggifyState is not null && this.BuggifyState.ShouldDeferWatchCheck(w.Key, commitVersion, ref buggifyDecided))
											{ // buggify: the deferred check leaves the node registered with its ORIGINAL baseline and read version, so a later commit still differing from the baseline fires it (self-heal), and only a net-reverted change stays pending
												Kenobi($"WWW watch({w.Key}) check deferred by buggify at commit {commitVersion}");
												continue;
											}

											Kenobi($"WWW watch({w.Key}, rv {w.ReadVersion}, cv {w.CommitVersion}) triggered by commit {commitVersion}, changed to {updatedValue} from {w.Value}");

											// queue the watch for triggering
											(watchesToTrigger ??= [ ]).Add(w);

											// remove it from this key
											kv.Value.RemoveAt(i);
											--i;
										}
										else
										{
											Kenobi($"WWW watch({w.Key}, rv {w.ReadVersion}, cv {w.CommitVersion}) idempotent");
										}
									}
									else
									{
										Kenobi($"WWW watch({w.Key}, rv {w.ReadVersion}, cv {w.CommitVersion}) untouched");
									}
								}

								if (kv.Value.Count == 0)
								{
									(deadWatchedKeys ??= [ ]).Add(kv.Key);
								}
							}

							if (deadWatchedKeys != null)
							{
								foreach (var k in deadWatchedKeys)
								{
									Kenobi($"WWW clearing dead watch key {k:K}");
									this.ActiveWatches.Remove(k);
								}
							}
						}
					}

					if (stamp != default)
					{
						handler.StampSignal?.TrySetResult(stamp);
					}

					// buggify: after arming and the commit-time scan, chaos may inject a spurious fire on one armed key (no-op unless Buggify.Chaos is set)
					this.BuggifyState?.MaybeInjectSpuriousFire(commitVersion, ref watchesToTrigger);

					if (watchesToTrigger is not null)
					{
						foreach (var watch in watchesToTrigger)
						{
							watch.Trigger();
						}
					}

					return commitVersion;
				}
				catch
				{
					// a prepared copy that never published must not leak its backend generation (with the
					// fdblite backend, an engine writer); Discard is commit-aware, so reaching here AFTER a
					// successful publish is a no-op on the frozen store
					updated?.Data.Discard();
					throw;
				}
			}

		}

		/// <summary>Called under the global write lock: makes a committed snapshot durable through the backend, publishes it as the current state, and trims the retained window; returns the instance the rest of the commit works with.</summary>
		/// <remarks>The backend can hand back a different committed store than the one that was written to (a persistent one freezes its writable generation into a readable view at the new durable root), so the published snapshot is a re-wrap rather than <paramref name="updated"/> itself.</remarks>
		private Snapshot PublishSnapshot(Snapshot updated, long commitVersion)
		{
			var published = ReplaceSnapshotStore(updated, this.Backend.Publish(updated.Data, commitVersion));

			this.CurrentSnapshotUnsafe = published;
			this.Snapshots[commitVersion] = published;
			this.ReadVersion = commitVersion;
			TrimRetainedSnapshots();
			return published;
		}

		/// <summary>Drops the published versions that have fallen out of the backend's retention window; a read at one of them then fails with <see cref="FdbError.TransactionTooOld"/>.</summary>
		private void TrimRetainedSnapshots()
		{
			int retained = this.Backend.RetainedVersions;
			if (retained == int.MaxValue)
			{ // every version stays readable: the whole history is inspectable, and nothing is ever reclaimed under it
				return;
			}

			// the window is the current version plus `retained` behind it; it is small by construction, so the scan is cheaper than keeping an ordered index
			while (this.Snapshots.Count > retained + 1)
			{
				long oldest = long.MaxValue;
				foreach (var version in this.Snapshots.Keys)
				{
					if (version < oldest) oldest = version;
				}
				this.Snapshots.Remove(oldest);
			}
		}

		/// <summary>A transaction is done with its resolved snapshot (dispose or reset): releases the pin taken when it started.</summary>
		protected internal void OnTransactionEnd(ReadYourWritesSnapshot snapshot) => this.Backend.Release(snapshot.Version);

		/// <summary>Re-wraps a snapshot around a replacement committed store, keeping its version, conflicts, stamp and arena.</summary>
		protected static Snapshot ReplaceSnapshotStore(Snapshot snapshot, IFdbCommittedStore data) => new(snapshot.Version, data, snapshot.Conflicts, snapshot.Stamp, snapshot.Arena);

		public IFdbTenantHandler OpenTenant(FdbTenantName name) => throw new NotImplementedException();

		public FdbDatabase OpenDatabase(FdbPath? rootPath, bool readOnly)
		{
			if (this.IsClosed) throw new ObjectDisposedException(this.GetType().Name);
			if (this.ApiVersion > this.ProtocolVersion)
			{
				throw new InvalidOperationException($"Emulated API version ({this.ApiVersion}) cannot be larger than the max supported version ({this.ProtocolVersion})");
			}

			var directory = FdbDirectoryLayer.Create(SubspaceLocation.Root);
			var root = new FdbDirectorySubspaceLocation(rootPath ?? FdbPath.Root);
			bool hasPartition = root.Path.Count != 0;

			return FdbDatabase.Create(this, directory, root, !hasPartition && readOnly, this.LifeTime.Token);
		}

		#region IFdbDatabaseHandler...

		public string? ClusterFile { get; }

		public string? ConnectionString { get; }

		public bool IsInvalid { get; private set; }

		public bool IsClosed { get; private set; }

		public Dictionary<FdbDatabaseOption, Slice> Options { get; } = new();

		private CancellationTokenSource LifeTime { get; } = new();

		public void SetOption(FdbDatabaseOption option, ReadOnlySpan<byte> data)
		{
			this.Options[option] = Slice.FromBytes(data);
		}

		public IFdbTransactionHandler CreateTransaction(FdbOperationContext context) => this.Backend.CreateTransaction(this, context);

		#endregion

		/// <summary>Cursor-agnostic face of a transaction handler: what an inspector can reach without knowing which storage is underneath.</summary>
		/// <remarks>The handler is generic over its backend's cursor so the read core monomorphizes per storage, which makes the closed type a storage detail. Anything that only wants the owning store or the in-flight mutation state - a test probe, a dump helper - names this instead, and keeps working when a store changes storage.</remarks>
		public abstract class TransactionHandler
		{

			protected TransactionHandler(FdbEmulatedDatabase store)
			{
				this.Store = store;
			}

			/// <summary>Store this transaction runs against</summary>
			public FdbEmulatedDatabase Store { get; }

			/// <summary>Returns the transaction's read-your-writes snapshot, waiting for it if it has not been started yet.</summary>
			public abstract ReadYourWritesSnapshot GetSnapshotBlocking();

		}

		public class TransactionHandler<TCursor> : TransactionHandler, IFdbTransactionHandler
			where TCursor : struct, IFdbCommittedCursor
		{

			private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create();

#if NET9_0_OR_GREATER
			private readonly System.Threading.Lock Lock = new();
#else
			private readonly object Lock = new();
#endif

			public TransactionHandler(FdbEmulatedDatabase store, FdbOperationContext context)
				: base(store)
			{
				this.Context = context;
				this.Scratch = new Arena(16 * 1024, 128 * 1024, BufferPool);
			}

			public FdbOperationContext Context { get; }

			private Arena Scratch { get; }

			public void Dispose()
			{
				this.IsClosed = true;
				// watches never armed by a successful commit die with the transaction
				FailPendingWatches(FdbError.TransactionCancelled);
				if (TryGetSnapshot(out var snapshot))
				{ // the backend may hold a read pin for the snapshot's generation
					this.Store.OnTransactionEnd(snapshot);
				}
				this.LifeTime.Dispose();
				this.Scratch.Dispose();
			}

			private long CommittedVersion { get; set; } = -1;

			private Task<ReadYourWritesSnapshot>? SnapshotTask { get; set; }

			private CancellationTokenSource LifeTime { get; set; } = new();

			private int m_keyWriteCount;
			private long m_payloadBytes;
			private int m_keyReadCount;
			private long m_keyReadSize;

			public long Size => m_payloadBytes;

			public (int Keys, long Size) GetWriteStatistics() => (Volatile.Read(ref m_keyWriteCount), Volatile.Read(ref m_payloadBytes));

			public (int Keys, long Size) GetReadStatistics() => (Volatile.Read(ref m_keyReadCount), Volatile.Read(ref m_keyReadSize));

			public bool IsClosed { get; private set; }

			private bool OptionReadSystemKeys { get; set; }

			private bool OptionWriteSystemKeys { get; set; }

			private bool OptionReadYourWrites { get; set; } = true;

			internal bool OptionReportConflictingKeys { get; set; }

			private long OptionTimeout { get; set; }

			private long OptionMaxRetryDelay { get; set; }

			private int OptionRetryLimit { get; set; }

			private bool OptionSnapshotReadYourWritesDisable { get; set; }

			public void SetOption(FdbTransactionOption option, ReadOnlySpan<byte> data)
			{
				switch (option)
				{
					case FdbTransactionOption.ReadSystemKeys:
					{
						this.OptionReadSystemKeys = true;
						this.OptionWriteSystemKeys = false;
						break;
					}
					case FdbTransactionOption.AccessSystemKeys:
					{
						this.OptionReadSystemKeys = true;
						this.OptionWriteSystemKeys = true;
						break;
					}
					case FdbTransactionOption.ReadYourWritesDisable:
					{
						if (Volatile.Read(ref m_keyReadCount) > 0)
						{ // observed on the real cluster: disabling read-your-writes after the transaction has already
						  // performed a read leaves it unusable — reads and the commit fail, writes are accepted but doomed
							this.OptionPoisoned = true;
						}
						this.OptionReadYourWrites = false;
						break;
					}
					case FdbTransactionOption.Timeout:
					{
						if (data.Length != 8) throw new FdbException(FdbError.InvalidOptionValue, "Timeout option value must be exactly 8 bytes");
						long v = BinaryPrimitives.ReadInt64LittleEndian(data);
						if (v < 0) throw new FdbException(FdbError.InvalidOptionValue, "Timeout option value must be positive");
						this.OptionTimeout = BinaryPrimitives.ReadInt64LittleEndian(data);
						break;
					}
					case FdbTransactionOption.MaxRetryDelay:
					{
						if (data.Length != 8) throw new FdbException(FdbError.InvalidOptionValue, "MaxRetryDelay option value must be exactly 8 bytes");
						long v = BinaryPrimitives.ReadInt64LittleEndian(data);
						if (v < 0) throw new FdbException(FdbError.InvalidOptionValue, "MaxRetryDelay option value must be positive");
						this.OptionMaxRetryDelay = BinaryPrimitives.ReadInt64LittleEndian(data);
						break;
					}
					case FdbTransactionOption.RetryLimit:
					{
						if (data.Length != 8) throw new FdbException(FdbError.InvalidOptionValue, "RetryLimit option value must be exactly 8 bytes");
						long v = BinaryPrimitives.ReadInt64LittleEndian(data);
						if (v < 0) throw new FdbException(FdbError.InvalidOptionValue, "RetryLimit option value must be positive");
						if (v > int.MaxValue) throw new FdbException(FdbError.InvalidOptionValue, "RetryLimit option value must be bess than int.MaxValue");
						this.OptionRetryLimit = (int) v;
						break;
					}
					case FdbTransactionOption.SnapshotReadYourWritesDisable:
					{
						if (data.Length != 0) throw new FdbException(FdbError.InvalidOptionValue, "SnapshotReadYourWritesDisable option value must be empty");
						this.OptionSnapshotReadYourWritesDisable = true;
						break;
					}
				case FdbTransactionOption.ReportConflictingKeys:
					{
						if (data.Length != 0) throw new FdbException(FdbError.InvalidOptionValue, "ReportConflictingKeys option value must be empty");
						this.OptionReportConflictingKeys = true;
						break;
					}
					default:
					{
						throw new InvalidOperationException("TODO: unsupported transaction option " + option);
					}
				}
			}

			/// <summary>Set when a transaction option was applied too late (e.g. <see cref="FdbTransactionOption.ReadYourWritesDisable"/> after a read): reads and the commit will fail, like on a real cluster.</summary>
			private bool OptionPoisoned;

			private void ThrowIfPoisoned()
			{
				if (this.OptionPoisoned)
				{
					throw new FdbException(FdbError.ClientInvalidOperation, "The transaction options were changed after the transaction already performed reads");
				}
			}

			private void AccountReadOperation(int count, long payload)
			{
				Interlocked.Increment(ref m_keyReadCount);
				Interlocked.Add(ref m_keyReadSize, payload);
			}

			public Task<long> GetReadVersionAsync(CancellationToken ct)
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);
				if (TryGetSnapshot(out var snap))
				{
					return Task.FromResult(snap.Version);
				}
				return GetReadVersionDeferred(this, ct);

				static async Task<long> GetReadVersionDeferred(TransactionHandler<TCursor> self, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					return snap.Version;
				}
			}

			public Task<ReadYourWritesSnapshot> GetSnapshot(CancellationToken ct)
			{
				lock (this.Lock)
				{
					return this.SnapshotTask ??= this.Store.StartNewSnapshot(this.Scratch, ct);
				}
			}

			public bool TryGetSnapshot([MaybeNullWhen(false)] out ReadYourWritesSnapshot snapshot)
			{
				var task = this.SnapshotTask;
				if (task != null && task.IsCompletedSuccessfully)
				{
					snapshot = task.Result;
					return true;
				}

				snapshot = null;
				return false;
			}

			public override ReadYourWritesSnapshot GetSnapshotBlocking()
			{
				if (!TryGetSnapshot(out var snapshot))
				{
					snapshot = GetSnapshot(this.LifeTime.Token).GetAwaiter().GetResult();
				}
				return snapshot;
			}

			public void SetReadVersion(long version)
			{
				lock (this.Lock)
				{
					if (this.SnapshotTask != null) throw new InvalidOperationException("Version already set"); //BUGBUG: same as the real one!
					this.SnapshotTask = this.Store.StartSnapshotAtVersion(this.Scratch, version, this.LifeTime.Token);
				}
			}

			public long GetCommittedVersion()
			{
				return this.CommittedVersion;
			}

			internal TaskCompletionSource<VersionStamp>? StampSignal { get; set; }

			public Task<VersionStamp> GetVersionStampAsync(CancellationToken ct)
			{
				lock (this.Lock)
				{
					this.StampSignal ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
					return this.StampSignal.Task;
				}
			}

			private bool GetEffectiveRyw(bool snapshot)
			{
				return this.OptionReadYourWrites && (!snapshot || !this.OptionSnapshotReadYourWritesDisable);
			}

			public Task<Slice> GetAsync(ReadOnlySpan<byte> key, bool snapshot, CancellationToken ct)
			{
				ThrowIfPoisoned();
				// note: the native client does not account reads the RYW layer serves locally; always accounting is a deliberate overestimate
				AccountApproximateSize(25 + (2 * key.Length));
				var k = this.Scratch.InternKeyRange(key);
				if (TryGetSnapshot(out var snap))
				{
					if (ct.IsCancellationRequested) return Task.FromCanceled<Slice>(ct);
					lock (this.Lock)
					{
						var v = snap.Read(k, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys).Slice;
						AccountReadOperation(1, v.Count);
						return Task.FromResult(v);
					}
				}
				return Deferred(this, k, snapshot, ct);

				static async Task<Slice> Deferred(TransactionHandler<TCursor> self, KeyRange key, bool isSnapshotRead, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						var v = snap.Read(key, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys).Slice;
						self.AccountReadOperation(1, v.Count);
						return v;
					}
				}
			}

			public Task<TResult> GetAsync<TResult>(ReadOnlySpan<byte> key, bool snapshot, FdbValueDecoder<TResult> decoder, CancellationToken ct)
			{
				var k = this.Scratch.InternKeyRange(key);
				if (TryGetSnapshot(out var snap))
				{
					if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
					Value value;
					lock (this.Lock)
					{
						value = snap.Read(k, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys);
						AccountReadOperation(1, value.Count);
					}
					return Task.FromResult(decoder(value.Span, !value.IsNull));
				}
				return Deferred(this, k, snapshot, decoder, ct);

				static async Task<TResult> Deferred(TransactionHandler<TCursor> self, KeyRange key, bool isSnapshotRead, FdbValueDecoder<TResult> decoder, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					Value value;
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						value = snap.Read(key, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys);
						self.AccountReadOperation(1, value.Count);
					}
					return decoder(value.Span, !value.IsNull);
				}
			}

			public Task<TResult> GetAsync<TState, TResult>(ReadOnlySpan<byte> key, bool snapshot, TState state, FdbValueDecoder<TState, TResult> decoder, CancellationToken ct)
			{
				var k = this.Scratch.InternKeyRange(key);
				if (TryGetSnapshot(out var snap))
				{
					if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
					Value value;
					lock (this.Lock)
					{
						value = snap.Read(k, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys);
						AccountReadOperation(1, value.Count);
					}
					return Task.FromResult(decoder(state, value.Span, !value.IsNull));
				}
				return Deferred(this, k, snapshot, state, decoder, ct);

				static async Task<TResult> Deferred(TransactionHandler<TCursor> self, KeyRange key, bool isSnapshotRead, TState state, FdbValueDecoder<TState, TResult> decoder, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					Value value;
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						value = snap.Read(key, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys);
						self.AccountReadOperation(1, value.Count);
					}
					return decoder(state, value.Span, !value.IsNull);
				}
			}

			public Task<Slice[]> GetValuesAsync(ReadOnlySpan<Slice> keys, bool snapshot, CancellationToken ct)
			{
				ThrowIfPoisoned();
				foreach (var key in keys)
				{
					AccountApproximateSize(25 + (2 * key.Count));
				}
				if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

				var ks = this.Scratch.InternKeyRanges(keys);

				// we can't 'await' and have Spans at the same time, but _usually_ the snapshot is already resolved, so we have two paths, with one extra step if we really have to await
				if (TryGetSnapshot(out var snap))
				{
					var values = new Slice[ks.Length];
					lock (this.Lock)
					{
						snap.Read(ks, values, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys);
					}

					long total = 0;
					foreach (var value in values)
					{
						total += value.Count;
					}
					AccountReadOperation(values.Length, total);

					return Task.FromResult(values);
				}

				return GetValuesDeferred(this, ks, snapshot, ct);

				static async Task<Slice[]> GetValuesDeferred(TransactionHandler<TCursor> self, KeyRange[] ks, bool isSnapshotRead, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					var values = new Slice[ks.Length];
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						snap.Read(ks, values, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys);

					}

					long total = 0;
					foreach (var value in values)
					{
						total += value.Count;
					}
					self.AccountReadOperation(values.Length, total);

					return values;
				}
			}

			public Task<long> GetValuesAsync<TValue>(ReadOnlySpan<Slice> keys, Memory<TValue> values, FdbValueDecoder<TValue> decoder, bool snapshot, CancellationToken ct)
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

				var ks = this.Scratch.InternKeyRanges(keys);

				// we can't 'await' and have Spans at the same time, but _usually_ the snapshot is already resolved, so we have two paths, with one extra step if we really have to await
				if (TryGetSnapshot(out var snap))
				{
					lock (this.Lock)
					{
						var total = snap.Read(ks, values.Span, decoder, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys);
						AccountReadOperation(ks.Length, total);
						return Task.FromResult(total);
					}
				}

				return GetValuesDeferred(this, ks, values, decoder, snapshot, ct);

				static async Task<long> GetValuesDeferred(TransactionHandler<TCursor> self, KeyRange[] ks, Memory<TValue> values, FdbValueDecoder<TValue> decoder, bool isSnapshotRead, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						long total = snap.Read(ks, values.Span, decoder, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys);
						self.AccountReadOperation(ks.Length, total);
						return total;
					}
				}
			}

			public Task<long> GetValuesAsync<TState, TValue>(ReadOnlySpan<Slice> keys, Memory<TValue> values, TState state, FdbValueDecoder<TState, TValue> decoder, bool snapshot, CancellationToken ct)
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

				var ks = this.Scratch.InternKeyRanges(keys);

				// we can't 'await' and have Spans at the same time, but _usually_ the snapshot is already resolved, so we have two paths, with one extra step if we really have to await
				if (TryGetSnapshot(out var snap))
				{
					lock (this.Lock)
					{
						long total = snap.Read(ks, values.Span, state, decoder, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys);
						AccountReadOperation(ks.Length, total);
						return Task.FromResult(total);
					}
				}

				return GetValuesDeferred(this, ks, values, state, decoder, snapshot, ct);

				static async Task<long> GetValuesDeferred(TransactionHandler<TCursor> self, KeyRange[] ks, Memory<TValue> values, TState state, FdbValueDecoder<TState, TValue> decoder, bool isSnapshotRead, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						long total = snap.Read(ks, values.Span, state, decoder, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys);
						self.AccountReadOperation(ks.Length, total);
						return total;
					}
				}
			}

			public Task<Slice> GetKeyAsync(KeySelector selector, bool snapshot, CancellationToken ct)
			{
				ThrowIfPoisoned();
				AccountApproximateSize(25 + (2 * selector.Key.Count)); // unprobed formula: same shape as a point read
				var selectorCopy = this.Scratch.InternSelector(selector);
				if (TryGetSnapshot(out var snap))
				{
					lock (this.Lock)
					{
						var result = snap.Resolve<TCursor>(selectorCopy, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys).Slice;
						AccountReadOperation(1, result.Count);
						return Task.FromResult<Slice>(result);
					}
				}

				return GetKeyDeferred(this, selectorCopy, snapshot, ct);

				static async Task<Slice> GetKeyDeferred(TransactionHandler<TCursor> self, Selector selector, bool isSnapshotRead, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct);
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						var result = snap.Resolve<TCursor>(selector, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys).Slice;
						self.AccountReadOperation(1, result.Count);
						return result;
					}
				}
			}

			public Task<Slice> GetKeyAsync(KeySpanSelector selector, bool snapshot, CancellationToken ct)
			{
				ThrowIfPoisoned();
				AccountApproximateSize(25 + (2 * selector.Key.Length)); // unprobed formula: same shape as a point read
				var selectorCopy = this.Scratch.InternSelector(selector);
				if (TryGetSnapshot(out var snap))
				{
					lock (this.Lock)
					{
						var result = snap.Resolve<TCursor>(selectorCopy, GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys).Slice;
						AccountReadOperation(1, result.Count);
						return Task.FromResult<Slice>(result);
					}
				}

				return GetKeyDeferred(this, selectorCopy, snapshot, ct);

				static async Task<Slice> GetKeyDeferred(TransactionHandler<TCursor> self, Selector selector, bool isSnapshotRead, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct);
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						var result = snap.Resolve<TCursor>(selector, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys).Slice;
						self.AccountReadOperation(1, result.Count);
						return result;
					}
				}
			}

			public Task<Slice[]> GetKeysAsync(ReadOnlySpan<KeySelector> selectors, bool snapshot, CancellationToken ct)
			{
				ThrowIfPoisoned();
				if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

				// we can't 'await' and have Spans at the same time, but _usually_ the snapshot is already resolved, so we have two paths, with one extra step if we really have to await
				if (TryGetSnapshot(out var s))
				{
					var res = new Slice[selectors.Length];
					lock (this.Lock)
					{
						long total = 0;
						for (int i = 0; i < selectors.Length; i++)
						{
							var r = s.Resolve<TCursor>(this.Scratch.InternSelector(selectors[i]), GetEffectiveRyw(snapshot), snapshot, this.OptionReadSystemKeys).Slice;
							total += r.Count;
							res[i] = r;
						}
						AccountReadOperation(selectors.Length, total);
					}
					return Task.FromResult(res);
				}

				return GetKeysDeferred(this, this.Scratch.InternSelectors(selectors), snapshot, ct);

				static async Task<Slice[]> GetKeysDeferred(TransactionHandler<TCursor> self, Selector[] selectors, bool isSnapshotRead, CancellationToken ct)
				{
					var s = await self.GetSnapshot(ct).ConfigureAwait(false);

					var res = new Slice[selectors.Length];
					lock (self.Lock)
					{
						ct.ThrowIfCancellationRequested();
						long total = 0;
						for (int i = 0; i < selectors.Length; i++)
						{
							var r = s.Resolve<TCursor>(selectors[i], self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys).Slice;
							total += r.Count;
							res[i] = r;
						}
						self.AccountReadOperation(selectors.Length, total);
					}
					return res;
				}
			}

			public Task<FdbRangeChunk> GetRangeAsync(
				KeySpanSelector beginInclusive,
				KeySpanSelector endExclusive,
				FdbRangeOptions options,
				int iteration,
				bool snapshot,
				CancellationToken ct
			)
			{
				ThrowIfPoisoned();
				if (ct.IsCancellationRequested) return Task.FromCanceled<FdbRangeChunk>(ct);

				// the conflicting-keys special keyspace is transaction-local, populated by a failed commit when the
				// ReportConflictingKeys option is set (like on a real cluster); it never touches the store
				if (beginInclusive.Key.Length > 2 && beginInclusive.Key[0] == 0xFF && beginInclusive.Key[1] == 0xFF
					&& beginInclusive.Key.StartsWith(Fdb.System.TransactionConflictingKeysPrefix.Span[..^1]))
				{
					return Task.FromResult(BuildConflictingKeysChunk(options, iteration));
				}

				AccountApproximateSize(25 + (2 * (beginInclusive.Key.Length + endExclusive.Key.Length))); // unprobed formula: same shape as a point read over both bounds

				//TODO: PERF: OPTIMIZE: implement natively instead of first allocated to KV<Slice, Slice> and then converting!
				var beginSelector = this.Scratch.InternSelector(in beginInclusive);
				var endSelector = this.Scratch.InternSelector(in endExclusive);

				if (!TryGetSnapshot(out var s))
				{
					return GetRangeDeferred(this, beginSelector, endSelector, options, iteration, snapshot, ct);
				}

				try
				{
					var chunk = GetRangeCore(s, beginSelector, endSelector, options, iteration, snapshot, ct);
					AccountReadOperation(chunk.Count, chunk.TotalBytes);
					return Task.FromResult(chunk);
				}
				catch (Exception e)
				{
					return Task.FromException<FdbRangeChunk>(e);
				}

				static async Task<FdbRangeChunk> GetRangeDeferred(TransactionHandler<TCursor> self, Selector beginInclusive, Selector endExclusive, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken ct)
				{
					var s = await self.GetSnapshot(ct).ConfigureAwait(false);
					var chunk = self.GetRangeCore(s, beginInclusive, endExclusive, options, iteration, snapshot, ct);
					self.AccountReadOperation(chunk.Count, chunk.TotalBytes);
					return chunk;
				}
			}

			private FdbRangeChunk GetRangeCore(
				ReadYourWritesSnapshot s,
				Selector beginInclusive,
				Selector endExclusive,
				FdbRangeOptions options,
				int iteration,
				bool isSnapshotRead,
				CancellationToken ct
			)
			{
				lock (this.Lock)
				{
					return s.GetRange<TCursor>(beginInclusive, endExclusive, options, iteration, GetEffectiveRyw(isSnapshotRead), isSnapshotRead, this.OptionReadSystemKeys);
				}
			}

			/// <inheritdoc />
			public Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(
				KeySpanSelector beginInclusive,
				KeySpanSelector endExclusive,
				bool snapshot,
				TState state,
				FdbKeyValueDecoder<TState, TResult> decoder,
				FdbRangeOptions options,
				int iteration,
				CancellationToken ct
			)
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled<FdbRangeChunk<TResult>>(ct);

				// the conflicting-keys special keyspace is transaction-local (populated by a failed commit when the
				// ReportConflictingKeys option is set); it never touches the store
				if (beginInclusive.Key.Length > 2 && beginInclusive.Key[0] == 0xFF && beginInclusive.Key[1] == 0xFF
					&& beginInclusive.Key.StartsWith(Fdb.System.TransactionConflictingKeysPrefix.Span[..^1]))
				{
					var special = BuildConflictingKeysChunk(options, iteration);
					var decoded = new TResult[special.Count];
					for (int i = 0; i < special.Count; i++)
					{
						decoded[i] = decoder(state, special.Items[i].Key.Span, special.Items[i].Value.Span);
					}
					return Task.FromResult(new FdbRangeChunk<TResult>(decoded, special.HasMore, special.Iteration, special.Options, special.First, special.Last, special.TotalBytes));
				}

				//TODO: PERF: OPTIMIZE: implement natively instead of first allocated to KV<Slice, Slice> and then converting!
				var beginSelector = this.Scratch.InternSelector(in beginInclusive);
				var endSelector = this.Scratch.InternSelector(in endExclusive);

				if (!TryGetSnapshot(out var s))
				{
					return GetRangeDeferred(this, beginSelector, endSelector, snapshot, state, decoder, options, iteration, ct);
				}

				try
				{
					var chunk = GetRangeCore(s, beginSelector, endSelector, snapshot, state, decoder, options, iteration, ct);
					AccountReadOperation(chunk.Count, chunk.TotalBytes);
					return Task.FromResult(chunk);
				}
				catch (Exception e)
				{
					return Task.FromException<FdbRangeChunk<TResult>>(e);
				}

				static async Task<FdbRangeChunk<TResult>> GetRangeDeferred(TransactionHandler<TCursor> self, Selector beginInclusive, Selector endExclusive, bool isSnapshotRead, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions options, int iteration, CancellationToken ct)
				{
					var s = await self.GetSnapshot(ct).ConfigureAwait(false);
					var chunk = self.GetRangeCore(s, beginInclusive, endExclusive, isSnapshotRead, state, decoder, options, iteration, ct);
					self.AccountReadOperation(chunk.Count, chunk.TotalBytes);
					return chunk;
				}
			}

			private FdbRangeChunk<TResult> GetRangeCore<TState, TResult>(
				ReadYourWritesSnapshot s,
				Selector beginInclusive,
				Selector endExclusive,
				bool isSnapshotRead,
				TState state,
				FdbKeyValueDecoder<TState, TResult> decoder,
				FdbRangeOptions options,
				int iteration,
				CancellationToken ct
			)
			{
				FdbRangeChunk chunk;
				lock (this.Lock)
				{
					chunk = s.GetRange<TCursor>(beginInclusive, endExclusive, options, iteration, GetEffectiveRyw(isSnapshotRead), isSnapshotRead, this.OptionReadSystemKeys);
				}

				var items = chunk.Items;
				var result = new TResult[items.Length];
				for(int i = 0; i < items.Length; i++)
				{
					result[i] = decoder(state, items[i].Key.Span, items[i].Value.Span);
				}

				//TODO: if chunk is pooled, we need to return the buffer to the pool, but we need to keep "First" and "Last" around?

				return new(result, chunk.HasMore, chunk.Iteration, chunk.Options, chunk.First, chunk.Last, chunk.TotalBytes);
			}

			/// <inheritdoc />
			public Task<FdbRangeResult> VisitRangeAsync<TState>(
				KeySpanSelector beginInclusive,
				KeySpanSelector endExclusive,
				bool snapshot,
				TState state,
				FdbKeyValueAction<TState> visitor,
				FdbRangeOptions options,
				int iteration,
				CancellationToken ct
			)
			{
				if (ct.IsCancellationRequested) return Task.FromCanceled<FdbRangeResult>(ct);

				// same transaction-local special keyspace as in GetRangeAsync: the range-query pipeline reads through this method
				if (beginInclusive.Key.Length > 2 && beginInclusive.Key[0] == 0xFF && beginInclusive.Key[1] == 0xFF
					&& beginInclusive.Key.StartsWith(Fdb.System.TransactionConflictingKeysPrefix.Span[..^1]))
				{
					var chunk = BuildConflictingKeysChunk(options, iteration);
					foreach (var kv in chunk.Items)
					{
						visitor(state, kv.Key.Span, kv.Value.Span);
					}
					return Task.FromResult(new FdbRangeResult(chunk.Count, chunk.HasMore, chunk.Iteration, chunk.Options, chunk.First, chunk.Last, chunk.TotalBytes));
				}

				var beginSelector = this.Scratch.InternSelector(in beginInclusive);
				var endSelector = this.Scratch.InternSelector(in endExclusive);

				if (!TryGetSnapshot(out var s))
				{
					return VisitRangeDeferred(this, beginSelector, endSelector, snapshot, state, visitor, options, iteration, ct);
				}

				try
				{
					var result = VisitRangeCore(s, beginSelector, endSelector, snapshot, state, visitor, options, iteration, ct);
					AccountReadOperation(result.Count, result.TotalBytes);
					return Task.FromResult(result);
				}
				catch (Exception e)
				{
					return Task.FromException<FdbRangeResult>(e);
				}

				static async Task<FdbRangeResult> VisitRangeDeferred(TransactionHandler<TCursor> self, Selector beginInclusive, Selector endExclusive, bool snapshot, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions options, int iteration, CancellationToken ct)
				{
					var s = await self.GetSnapshot(ct).ConfigureAwait(false);
					var result = self.VisitRangeCore<TState>(s, beginInclusive, endExclusive, snapshot, state, visitor, options, iteration, ct);
					self.AccountReadOperation(result.Count, result.TotalBytes);
					return result;
				}
			}

			private FdbRangeResult VisitRangeCore<TState>(
				ReadYourWritesSnapshot s,
				Selector beginInclusive,
				Selector endExclusive,
				bool isSnapshotRead,
				TState state,
				FdbKeyValueAction<TState> visitor,
				FdbRangeOptions options,
				int iteration,
				CancellationToken ct)
			{
				// stream the range straight to the visitor over spans - no KeyValuePair<Slice,Slice>[] chunk, so a
				// read-only aggregate scan is O(1) allocation in the number of pairs. The visitor runs under the
				// transaction lock: it decodes/aggregates a transient span per pair and must not re-enter the transaction.
				var box = new RangeVisitorBox<TState>(state, visitor, ct);
				lock (this.Lock)
				{
					return s.VisitRange<TCursor, RangeVisitorBox<TState>>(
						beginInclusive, endExclusive, options, iteration,
						GetEffectiveRyw(isSnapshotRead), isSnapshotRead, this.OptionReadSystemKeys,
						box, static (RangeVisitorBox<TState> b, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) => b.Visit(key, value));
				}
			}

			/// <summary>Adapts the transaction's void <see cref="FdbKeyValueAction{TState}"/> + cancellation to the seam's bool range visitor: check cancellation, visit, keep going.</summary>
			private sealed class RangeVisitorBox<TState>(TState state, FdbKeyValueAction<TState> visitor, CancellationToken ct)
			{
				public bool Visit(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
				{
					ct.ThrowIfCancellationRequested();
					visitor(state, key, value);
					return true;
				}
			}

			/// <summary>Builds the synthesized chunk for a read of <c>\xff\xff/transaction/conflicting_keys/</c>: one boundary pair per conflicting read range collected by the failed commit (value <c>'1'</c> = begin inclusive, <c>'0'</c> = end exclusive).</summary>
			private FdbRangeChunk BuildConflictingKeysChunk(FdbRangeOptions options, int iteration)
			{
				var items = new List<KeyValuePair<Slice, Slice>>();
				if (TryGetSnapshot(out var snap) && snap.ConflictingReadRanges is { } ranges)
				{
					var prefix = Fdb.System.TransactionConflictingKeysPrefix;
					foreach (var range in ranges)
					{ // the collected ranges are disjoint and sorted (the read-conflict set coalesces overlaps), so the boundary values strictly alternate 1/0
						items.Add(new(prefix + range.Key.Slice, Slice.FromStringAscii("1")));
						items.Add(new(prefix + range.Value.Slice, Slice.FromStringAscii("0")));
					}
					if (options.IsReversed)
					{
						items.Reverse();
					}
					if (options.Limit is > 0 and var limit && items.Count > limit)
					{
						items.RemoveRange(limit, items.Count - limit);
					}
				}
				var first = items.Count > 0 ? items[0].Key : default;
				var last = items.Count > 0 ? items[^1].Key : default;
				long sum = 0;
				foreach (var kv in items) { sum += kv.Key.Count + kv.Value.Count; }
				return new FdbRangeChunk(items.ToArray(), hasMore: false, iteration, options, first, last, checked((int) sum), SliceOwner.Nil);
			}

			public ValueTask<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected, bool snapshot, CancellationToken ct)
			{
				var k = this.Scratch.InternKeyRange(key);
				return new(Deferred(this, GetSnapshot(ct), k, expected, snapshot, ct));

				static async Task<(FdbValueCheckResult Result, Slice Actual)> Deferred(TransactionHandler<TCursor> self, Task<ReadYourWritesSnapshot> st, KeyRange key, Slice expected, bool isSnapshotRead, CancellationToken ct)
				{
					var s = await st.ConfigureAwait(false);
					ct.ThrowIfCancellationRequested();

					var actual = s.Read(key, self.GetEffectiveRyw(isSnapshotRead), isSnapshotRead, self.OptionReadSystemKeys);
					self.AccountReadOperation(1, actual.Count);
					if (actual.Equals(expected))
					{
						return (FdbValueCheckResult.Success, actual.Slice);
					}
					else
					{
						return (FdbValueCheckResult.Failed, actual.Slice);
					}
				}
			}

			public Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key, CancellationToken ct)
			{
				// in memory => fake a single storage process. Starting at API level 630 (IncludePortInAddress
				// becomes the default) the real client returns "IP:PORT" (or "IP:PORT:tls"); before that, just "IP".
				var address = this.Store.ApiVersion >= 630 ? "127.0.0.1:4500" : "127.0.0.1";
				return !ct.IsCancellationRequested ? Task.FromResult<string[]>([ address ]) : Task.FromCanceled<string[]>(ct);
			}

			public Task<Slice[]> GetRangeSplitPointsAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize, CancellationToken ct)
			{
				// deterministic walk over the committed snapshot, emitting a split every ~chunkSize of exact key+value
				// bytes, both endpoints always included: the real API derives splits from storage samples (uneven chunks),
				// so consumers already tolerate jitter, and deterministic splits are the better behavior for a test emulator
				if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be greater than zero");
				if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

				var begin = this.Scratch.InternKeyRange(beginKey).Begin;
				var end = this.Scratch.InternKeyRange(endKey).Begin;

				if (TryGetSnapshot(out var snap))
				{
					return Task.FromResult(snap.ComputeSplitPoints(begin, end, chunkSize));
				}
				return Deferred(this, begin, end, chunkSize, ct);

				static async Task<Slice[]> Deferred(TransactionHandler<TCursor> self, Key begin, Key end, long chunkSize, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					ct.ThrowIfCancellationRequested();
					return snap.ComputeSplitPoints(begin, end, chunkSize);
				}
			}

			public Task<long> GetEstimatedRangeSizeBytesAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, CancellationToken ct)
			{
				// exact, deterministic sum of key+value bytes over the committed snapshot: the real API is a noisy
				// sampling estimator (can lag fresh writes, or return coarse/zero values on small ranges), so tests
				// must not assert tight bounds on this value
				if (ct.IsCancellationRequested) return Task.FromCanceled<long>(ct);

				var begin = this.Scratch.InternKeyRange(beginKey).Begin;
				var end = this.Scratch.InternKeyRange(endKey).Begin;

				if (TryGetSnapshot(out var snap))
				{
					return Task.FromResult(snap.ComputeExactRangeSize(begin, end));
				}
				return Deferred(this, begin, end, ct);

				static async Task<long> Deferred(TransactionHandler<TCursor> self, Key begin, Key end, CancellationToken ct)
				{
					var snap = await self.GetSnapshot(ct).ConfigureAwait(false);
					ct.ThrowIfCancellationRequested();
					return snap.ComputeExactRangeSize(begin, end);
				}
			}

			private void AccountWriteOperation(int payload)
			{
				Interlocked.Increment(ref m_keyWriteCount);
				Interlocked.Add(ref m_payloadBytes, payload);
			}

			public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			{
				var k = this.Scratch.InternKeyRange(key);
				var v = this.Scratch.InternValue(value);
				var snapshot = GetSnapshotBlocking();
				lock (this.Lock)
				{
					snapshot.Set(k, v, this.OptionWriteSystemKeys);
				}
				AccountWriteOperation(key.Length + value.Length + 28);
				AccountApproximateSize(69 + (3 * key.Length) + value.Length);
			}

			public void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType mutation)
			{
#if NET5_0_OR_GREATER
				if (mutation <= FdbMutationType.Invalid || !Enum.IsDefined<FdbMutationType>(mutation))
#else
				// the generic Enum.IsDefined<T>(T) overload is not available, using the legacy (Type, object) overload instead (which boxes)
				if (mutation <= FdbMutationType.Invalid || !Enum.IsDefined(typeof(FdbMutationType), mutation))
#endif
				{
					throw new FdbException(FdbError.InvalidMutationType, $"Invalid mutation type '{mutation}'");
				}

				var k = this.Scratch.InternKeyRange(key);
				var v = this.Scratch.InternValue(param);
				var snapshot = GetSnapshotBlocking();
				lock (this.Lock)
				{
					snapshot.Atomic(k, v, mutation, this.OptionWriteSystemKeys);
				}
				AccountWriteOperation(key.Length + param.Length);
				AccountApproximateSize(69 + (3 * key.Length) + param.Length); // same native accounting as a Set
			}

			public void Clear(ReadOnlySpan<byte> key)
			{
				var k = this.Scratch.InternKeyRange(key);
				var snapshot = GetSnapshotBlocking();
				lock (this.Lock)
				{
					snapshot.Clear(k, this.OptionWriteSystemKeys);
				}
				// The key is converted to range [key, key.'\0'), and there is an overhead of 28-byte per operation
				AccountWriteOperation((key.Length * 2) + 28 + 1);
				AccountApproximateSize(50 + (4 * key.Length));
			}

			public void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
			{
				var beginCopy = this.Scratch.InternKey(beginKeyInclusive);
				var endCopy = this.Scratch.InternKey(endKeyExclusive);
				var snapshot = GetSnapshotBlocking();
				lock (this.Lock)
				{
					snapshot.ClearRange(beginCopy, endCopy, this.OptionWriteSystemKeys);
				}
				// There is an overhead of 28-byte per operation
				AccountWriteOperation(beginKeyInclusive.Length + endKeyExclusive.Length + 28);
				AccountApproximateSize(68 + (2 * (beginKeyInclusive.Length + endKeyExclusive.Length)));
			}

			public void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
			{
				var beginCopy = this.Scratch.InternKey(beginKeyInclusive);
				var endCopy = this.Scratch.InternKey(endKeyExclusive);
				var snapshot = GetSnapshotBlocking();
				lock (this.Lock)
				{
					if (type == FdbConflictRangeType.Write)
					{
						snapshot.WriteConflict(beginCopy, endCopy);
					}
					else
					{
						snapshot.ReadConflict(beginCopy, endCopy);
					}
				}
			}

			/// <summary>Commit-size accounting, calibrated per-operation against the fdb 7.4 native client (see the probe in ScenarioProbeFacts):
			/// GET = 25 + 2k, SET = 69 + 3k + v, CLEAR = 50 + 4k, CLEARRANGE = 68 + 2(b+e), ATOMIC = 69 + 3k + p.</summary>
			/// <remarks>Where FakeDb cannot know what the native client would do (reads served locally by the RYW layer are
			/// not accounted natively; selector/range read formulas are unprobed), it accounts anyway: the invariant that
			/// matters is to never UNDER-estimate — batching loops flush on this value, and an underestimate would produce
			/// an oversized commit that fails deterministically on every retry.</remarks>
			private long m_approximateSize;

			private void AccountApproximateSize(long bytes) => Interlocked.Add(ref m_approximateSize, bytes);

			public Task<long> GetApproximateSizeAsync(CancellationToken ct)
			{
				return !ct.IsCancellationRequested
					? Task.FromResult(Volatile.Read(ref m_approximateSize))
					: Task.FromCanceled<long>(ct);
			}

			/// <summary>Watches created by this transaction</summary>
			internal List<WatchNode>? Watches { get; set; }

			public FdbWatch Watch(Slice key, CancellationToken ct)
			{
				// we have to read the value at the time the watch is created
				// - it could change in a concurrent transaction (that starts after, but commits before)
				// - it could be changed in the same transaction after the watch is created

				// note: it is highly unlikely that this would be the first call,
				// since the caller will probably use the Directory Layer,
				// as well as read some state before deciding to watch the key...
				var snap = GetSnapshotBlocking();

				Value v;
				var kr = this.Scratch.InternKeyRange(key);
				lock (this.Lock)
				{
					v = snap.Read(kr, GetEffectiveRyw(true), snapshotRead: true, this.OptionReadSystemKeys);
					//note: this is a "shadow read", so we will not account for it.
				}

				var node = new WatchNode()
				{
					Key = key.Copy(),
					Value = v.Slice.Copy(),
					//Version: will be set when the transaction commits!
					Future = FdbFuture.Create<Slice>(ct, TaskCreationOptions.RunContinuationsAsynchronously),
				};
				lock (this.Lock)
				{
					(this.Watches ??= [ ]).Add(node);
				}
				return new FdbWatch(node.Future, node.Key);
			}

			/// <summary>Creates an <see cref="FdbException"/> without the native message lookup: the code-only constructor resolves its text through <c>fdb_get_error</c>, and the emulator must work on machines where the <c>fdb_c</c> library is not installed.</summary>
			private static FdbException CreateError(FdbError error) => new(error, error.ToString());

			/// <summary>Fails every watch this transaction created but never armed (its commit did not succeed), like the real client does.</summary>
			private void FailPendingWatches(FdbError error) => FailPendingWatches(CreateError(error));

			/// <summary>Fails every watch this transaction created but never armed, with the given cause; a watch must always settle, or its awaiter deadlocks.</summary>
			private void FailPendingWatches(Exception cause)
			{
				List<WatchNode>? pending;
				lock (this.Lock)
				{
					pending = this.Watches;
					this.Watches = null;
				}
				if (pending is null) return;
				foreach (var node in pending)
				{
					node.Future.TrySetException(cause);
				}
			}

			public async Task CommitAsync(CancellationToken ct)
			{
				ThrowIfPoisoned();
				try
				{
					this.CommittedVersion = await this.Store.Commit(this, ct);
				}
				catch (FdbException e)
				{ // a failed commit (e.g. a conflict) kills the futures it would have settled:
					// the watches fail with the commit error, the versionstamp with TransactionInvalidVersion (there is no commit version) — both pinned against the real cluster
					this.StampSignal?.TrySetException(CreateError(FdbError.TransactionInvalidVersion));
					FailPendingWatches(e.Code);
					throw;
				}
				catch (Exception e) when (e is not OperationCanceledException)
				{ // an unexpected engine failure must still settle the futures with the crash itself: a pending watch would deadlock its awaiter, which hides the failure instead of surfacing it
					// (cancellation is excluded: the watch futures observe their own token, and Dispose/Reset settle survivors with TransactionCancelled)
					this.StampSignal?.TrySetException(e);
					FailPendingWatches(e);
					throw;
				}
				finally
				{
					this.StampSignal?.TrySetCanceled();
				}
			}

			private int RetryCount { get; set; }

			/// <summary>Exponential backoff for this transaction's retries; grows across successive <see cref="OnErrorAsync"/> calls, created lazily only when the store enables a retry delay</summary>
			private ExponentialRandomizedBackoff? RetryBackoff { get; set; }

			public async Task OnErrorAsync(FdbError code, CancellationToken ct)
			{
				ct.ThrowIfCancellationRequested();
				switch (code)
				{
					case FdbError.NotCommitted:
					case FdbError.TransactionTooOld:
					case FdbError.FutureVersion:
					{
						this.RetryCount++;
						if (this.OptionRetryLimit > 0 && this.RetryCount > this.OptionRetryLimit)
						{ // exceeded limit
							break;
						}

						// realistic-but-virtual retry backoff: scheduled on the store's TimeProvider, so a fake clock
						// advances it with everything else (and a real backoff costs ZERO real time under virtual time).
						// The default policy is no wait (RetryDelayMaximum == 0), so normal tests retry instantly - a
						// "broken cluster" test raises FdbEmulatedDatabase.RetryDelayMaximum to emulate recovery timing.
						var maximum = this.Store.RetryDelayMaximum;
						if (maximum > TimeSpan.Zero)
						{
							if (this.OptionMaxRetryDelay > 0)
							{ // the client's MaxRetryDelay option only TIGHTENS the store's cap (it never enables the backoff)
								var cap = TimeSpan.FromMilliseconds(this.OptionMaxRetryDelay);
								if (cap < maximum) maximum = cap;
							}
							var backoff = this.RetryBackoff ??= new ExponentialRandomizedBackoff(this.Store.RetryDelayInitial, maximum) { Time = this.Store.Time };
							await backoff.Wait(ct).ConfigureAwait(false);
						}

						Reset();
						return;
					}
					default:
					{
						this.RetryCount = 0;
						break;
					}
				}
				throw CreateError(code);
			}

			public void Reset()
			{
				// resetting wipes the transaction state: watches created before the reset are cancelled
				FailPendingWatches(FdbError.TransactionCancelled);
				if (TryGetSnapshot(out var snapshot))
				{ // the backend may hold a read pin for the snapshot's generation
					this.Store.OnTransactionEnd(snapshot);
				}
				lock (this.Lock)
				{
					this.SnapshotTask = null;
					this.CommittedVersion = -1;
					this.LifeTime.Cancel();
					this.LifeTime = new();
					this.StampSignal?.TrySetCanceled();
					this.StampSignal = null;
					m_keyWriteCount = 0;
					m_payloadBytes = 0;
					m_keyReadCount = 0;
					m_keyReadSize = 0;
					m_approximateSize = 0;
				}
			}

			public void Cancel()
			{
				this.LifeTime.Cancel();
			}

		}

		public struct OnionIterator<TCursor>
			where TCursor : struct, IFdbCommittedCursor
		{
			/// <summary>We used the current value and the iterator must be advanced</summary>
			private const int STATE_UNKNOWN = 0;
			/// <summary>We haved used the current value</summary>
			private const int STATE_AVAILABLE = 1;
			/// <summary>We have exhausted the iterator, and it does not have any more values</summary>
			private const int STATE_DEAD = 2;

			/// <summary>State of iteration of the outer iterator (0 = need to fetch next value, 1 = we have a value, 2 = iterator has completed)</summary>
			private int OuterState;

			/// <summary>State of iteration of the inner iterator (0 = need to fetch next value, 1 = we have a value, 2 = iterator has completed)</summary>
			private int InnerState;

			public KeyValuePair<Key, Value> Current;

			private readonly ColaStore<ColaRangeDictionary<Key, Mutation>.Entry>.Iterator Outer;

			// non-readonly: the cursor is a mutable struct whose position advances in place (copying it would fork the position for a value-state backend cursor)
			private TCursor Inner;

			private readonly Arena Arena;

			internal long Id;

			public OnionIterator(IFdbCommittedStore<TCursor> inner, ColaRangeDictionary<Key, Mutation> outer, Arena arena, long id)
			{
				this.OuterState = STATE_UNKNOWN;
				this.InnerState = STATE_UNKNOWN;
				this.Outer = outer.GetIterator();
				this.Inner = inner.GetCursor();
				this.Current = default;
				this.Arena = arena;
				this.Id = id;
			}

			public bool Seek(Selector selector)
			{
				// note: the offset is applied to each layer independently, and the merge picks the smallest candidate.
				// This is exact for the offsets range queries use (0 or +1: "first key of this layer at/after the pivot");
				// larger offsets must be resolved on the merged view itself (see ReadYourWritesSnapshot.Resolve).
				Kenobi($"** #{this.Id} Seek at {selector}: {selector.Key}, {selector.OrEqual}, {selector.Offset}...)");
				// setup "inner"
				if (this.Inner.Seek(selector.Key, selector.OrEqual))
				{
					Kenobi($"*** #{this.Id} inner: {this.Inner.CopyCurrent()}");
					this.InnerState = STATE_AVAILABLE;
					for (int i = 0; i < selector.Offset; i++)
					{
						if (!this.Inner.Next())
						{
							this.InnerState = STATE_DEAD;
							break;
						}
						Kenobi($"*** #{this.Id} inner + {i+1}: {this.Inner.CopyCurrent()}");
					}
				}
				else
				{ // no key at/before the pivot: the layer's whole stream lies after it, and its first key is candidate #1
					if (this.Inner.SeekFirst())
					{
						this.InnerState = STATE_AVAILABLE;
						for (int i = 1; i < selector.Offset; i++)
						{
							if (!this.Inner.Next())
							{
								this.InnerState = STATE_DEAD;
								break;
							}
						}
						Kenobi($"*** #{this.Id} inner (from first): {this.Inner.CopyCurrent()}");
					}
					else
					{
						this.InnerState = STATE_DEAD;
					}
				}

				// setup "outer": position at the first entry that can affect keys at/after the pivot.
				// Clear/ClearRange entries are NEVER skipped: the merge consumes them as masking directives over the inner layer.
				if (this.Outer.Seek(new(selector.Key, selector.Key, null), selector.OrEqual))
				{ // positioned at the last entry beginning at/before the pivot
					this.OuterState = STATE_AVAILABLE;
					Kenobi($"*** #{this.Id} outer: {this.Outer.Current}");
					if (selector.Offset > 0)
					{
						// move past the reference entry, UNLESS it is a cleared range covering keys strictly beyond the
						// pivot (it still masks them); a point entry AT the pivot has End == successor(pivot) and must
						// be stepped past, or an end selector like FirstGreaterThan(pivot) would resolve to the pivot itself
						var entry = this.Outer.Current;
						if (entry is null || entry.Value?.Op is not (Operation.ClearRange or Operation.Clear) || !(entry.End > selector.Key.GetSuccessor(this.Arena)))
						{
							this.OuterState = this.Outer.Next() ? STATE_AVAILABLE : STATE_DEAD;
							Kenobi($"*** #{this.Id} outer + 1: {this.Outer.Current}");
						}
					}
				}
				else
				{ // every entry begins after the pivot: they all can affect the forward stream
					this.OuterState = this.Outer.SeekFirst() ? STATE_AVAILABLE : STATE_DEAD;
					Kenobi($"*** #{this.Id} outer (from first): {this.Outer.Current}");
				}

				Kenobi($"*** #{this.Id} seek initial of {selector}: inner={this.InnerState}:{this.Inner.CopyCurrent()}, outer={this.OuterState}:{this.Outer.Current}");
				// "compute" the current
				Next();
				Kenobi($"*** #{this.Id} seek result of {selector}: inner={this.InnerState}:{this.Inner.CopyCurrent()}, outer={this.OuterState}:{this.Outer.Current} : {this.Current}");

				return true;
			}

			private bool BacktrackOuterUntilRealKey()
			{
				var x = this.Outer.Current;
				while (x?.Value?.Op is Operation.ClearRange or Operation.Clear)
				{
					Kenobi($"**** #{this.Id} backtracking outer {x}");
					if (!this.Outer.Previous())
					{
						return false;
					}
					x = this.Outer.Current;
				}

				return true;
			}

			private bool AdvanceOuterUntilRealKey()
			{
				var x = this.Outer.Current;
				while (x?.Value?.Op is Operation.ClearRange or Operation.Clear)
				{
					if (!this.Outer.Next())
					{
						return false;
					}
					x = this.Outer.Current;
				}

				return true;
			}

			public bool Next()
			{
				// the inner cursor stays a field access throughout: a local copy of a struct cursor would fork its position
				var outer = this.Outer;

				while (true)
				{
					Kenobi($"**** #{this.Id} next({this.InnerState}:{this.Inner.CopyKey():K}, {this.OuterState}:{outer.Current})");

					// advance one or the other
					if (this.InnerState == STATE_UNKNOWN)
					{ // get next from inner
						this.InnerState = this.Inner.Next() ? STATE_AVAILABLE : STATE_DEAD;
						Kenobi($"**** #{this.Id} fetched inner => {this.InnerState}:{this.Inner.CopyCurrent()}");
					}

					if (this.OuterState == STATE_UNKNOWN)
					{ // get next from outer
						this.OuterState = !outer.Next() ? STATE_DEAD : STATE_AVAILABLE;
						Kenobi($"**** #{this.Id} fetched outer => {this.OuterState}:{outer.Current}");
					}

					if (this.OuterState == STATE_DEAD)
					{
						if (this.InnerState == STATE_DEAD)
						{ // done iterating on both!
							return false;
						}

						// passthrough the inner as-is
						this.Current = this.Inner.CopyCurrent();
						Kenobi($"** #{this.Id} outer done, pass-through inner {this.Current.Key:K} = {this.Current.Value:V}");
						this.InnerState = STATE_UNKNOWN;
						return true;
					}

					var outerEntry = outer.Current;
					Contract.Debug.Assert(outerEntry != null);
					var mutation = outerEntry.Value;
					Contract.Debug.Assert(mutation != null);

					if (this.InnerState == STATE_DEAD)
					{
						// passthrough the outer as-is

						Kenobi($"** #{this.Id} inner done, pass-through outer {mutation.Op} {outerEntry.Begin:K} ~ {outerEntry.End:K} = {mutation.Parameter:V}");
						if (mutation.Op is Operation.ClearRange or Operation.Clear)
						{
							this.OuterState = STATE_UNKNOWN;
							continue;
						}

						if (mutation.IsAtomic())
						{
							// observe the chain transiently: the mutation log is never patched by a scan (the chain
							// must survive to apply over the committed value at commit time; the read paths convert
							// chains for the keys they actually return)
							var value = Value.Nil;
							do
							{
								value = ReadYourWritesSnapshot.CoalesceAtomic(this.Arena, value, mutation);
								mutation = mutation.Next;
							}
							while (mutation != null);

							this.OuterState = STATE_UNKNOWN;
							if (value.IsNull) continue;
							this.Current = new KeyValuePair<Key, Value>(outerEntry.Begin, value);
							return true;
						}

						this.Current = new KeyValuePair<Key, Value>(outerEntry.Begin, mutation.GetEffectiveValue());
						this.OuterState = STATE_UNKNOWN;
						return true;
					}

					int cmp = this.Inner.CurrentKey.SequenceCompareTo(outerEntry.Begin.Span);
					Kenobi($"** #{this.Id} ({cmp}) [{this.Inner.CopyKey():V} = {this.Inner.CopyValue():V}] vs {mutation.Op} [{outerEntry.Begin:K} ~ {outerEntry.End:K} = {mutation.Parameter:V}]");

					switch (cmp)
					{
						case < 0:
						{ // pass through inner
							this.Current = this.Inner.CopyCurrent();
							this.InnerState = STATE_UNKNOWN;
							Kenobi($"*** #{this.Id} use inner, advance inner: {this.Inner.CopyKey():K} = {this.Inner.CopyValue():V}");
							return true;
						}
						case 0:
						{ // collision
							if (mutation.IsKv())
							{
								if (mutation.Op == Operation.Clear)
								{
									// consume both
									this.InnerState = STATE_UNKNOWN;
									this.OuterState = STATE_UNKNOWN;
									Kenobi($"*** #{this.Id} skip cleared, advance both: {this.Inner.CopyKey():K}");
									continue;
								}

								// consume both
								this.Current = new(this.Inner.CopyKey(), mutation.GetEffectiveValue());
								this.InnerState = STATE_UNKNOWN;
								this.OuterState = STATE_UNKNOWN;
								Kenobi($"*** #{this.Id} combine, advance both: {this.Inner.CopyKey():K} = {this.Current.Value:V}");
								return true;
							}

							if (mutation.IsRange())
							{ // the committed key is the first key of a cleared range: it is masked; keep the range (it may mask more keys)
								this.InnerState = STATE_UNKNOWN;
								Kenobi($"*** #{this.Id} inner masked by clear range, advance inner: {this.Inner.CopyKey():K}");
								continue;
							}

							if (mutation.IsAtomic())
							{ // coalesce the whole chain over the committed value, transiently (see above)
								var value = this.Inner.CopyValue();
								for (var m = mutation; m != null; m = m.Next)
								{
									value = ReadYourWritesSnapshot.CoalesceAtomic(this.Arena, value, m);
								}
								this.InnerState = STATE_UNKNOWN;
								this.OuterState = STATE_UNKNOWN;
								if (value.IsNull) continue;
								this.Current = new KeyValuePair<Key, Value>(outerEntry.Begin, value);
								return true;
							}
							throw new NotSupportedException("Unexpected mutation type while iterating");
						}
						default:
						{ // passthrough outer
							if (mutation.IsKv())
							{
								if (mutation.Parameter.IsNull)
								{
									this.OuterState = STATE_UNKNOWN;
									Kenobi("*** skip, advance outer");
									continue;
								}

								this.OuterState = STATE_UNKNOWN;
								this.Current = KeyValuePair.Create(outerEntry.Begin, mutation.Parameter);
								Kenobi($"*** #{this.Id} use outer, advance outer: {this.Current.Key:K} = {this.Current.Value:V}");
								return true;
							}

							if (mutation.IsRange())
							{
								if (outerEntry.End.Span.SequenceCompareTo(this.Inner.CurrentKey) <= 0)
								{ // clear range in empty spot, skip it!
									this.OuterState = STATE_UNKNOWN;
									Kenobi($"*** #{this.Id} skip, advance outer");
								}
								else
								{ // eat the inner
									this.InnerState = STATE_UNKNOWN;
									Kenobi($"*** #{this.Id} skip, advance inner");
								}
								continue;
							}

							if (mutation.IsAtomic())
							{
								// if we end up here, it means the key does not exist: coalesce the chain from an empty
								// value, transiently (see above)
								var value = Value.Nil;
								do
								{
									value = ReadYourWritesSnapshot.CoalesceAtomic(this.Arena, value, mutation);
									mutation = mutation.Next;
								}
								while (mutation != null);

								this.OuterState = STATE_UNKNOWN;
								if (value.IsNull) continue;
								this.Current = new KeyValuePair<Key, Value>(outerEntry.Begin, value);
								return true;
							}

							throw new NotSupportedException("Unexpected mutation type while iterating");
						}
					}
				}
			}

		}

		[DebuggerDisplay("Key={Key}, RV={ReadVersion}, CV={CommitVersion}, Status={Future.Task.Status}")]
		public sealed record WatchNode
		{
			/// <summary>Watched key</summary>
			public required Slice Key { get; init; }

			/// <summary>Value of the key when the watch was set</summary>
			public required Slice Value { get; init; }

			/// <summary>ReadVersion of the transaction that set the watch</summary>
			public long ReadVersion { get; internal set; }

			/// <summary>CommitVersion of the transaction that set the watch</summary>
			public long? CommitVersion { get; internal set; }

			public required FdbFuture<Slice> Future { get; init; }

			public void Trigger()
			{
				Kenobi($"*** trigger watch for '{this.Key:K}'");
				this.Future.TrySetResult(this.Key);
			}

		}

		/// <summary>Test-only fault-injection facet ("buggify") for the watch machinery, reproducing the false positives and false
		/// negatives that a real fdb cluster is contractually allowed to produce but that the deterministic emulator never emits.</summary>
		/// <remarks>
		/// <para>A FoundationDB watch may fire even when the watched key did not change (a permitted spurious fire), and a change
		/// that is reverted before the watch machinery observes it may never fire at all. Correct consumers re-read the key after a
		/// fire and never assume "fired implies changed". FakeDb keeps watches deterministic and independent, so it never exercises
		/// those weak-contract paths; this facet injects them on demand (<see cref="FireWatches"/> / <see cref="SuppressNextWatchCheck"/> /
		/// <see cref="FireWatchesAfter"/>) or automatically under a seed (<see cref="Chaos"/>).</para>
		/// <para>The two modelled classes are the catalogued real-cluster findings: per-key fan-out spurious fire (a stale-armed
		/// sibling dragging every watch on the key), and a deferred watch check (a skipped check that self-heals on a later real
		/// change but loses a net-reverted transient forever). By construction a deferred check only loses fires the contract already
		/// permits losing, so buggify never makes the emulator contract-illegal.</para>
		/// <para>Every outcome is a pure function of (seed, transaction schedule, virtual clock): <see cref="Chaos"/> draws are
		/// deterministic hashes of the commit version and key, and <see cref="FireWatchesAfter"/> schedules on <see cref="FdbEmulatedDatabase.Time"/>,
		/// so a test driving a fake clock replays exactly.</para>
		/// <para>The facet is created lazily and stays inert until used; the shipped emulator default is buggify-off (see the store's
		/// <see cref="FdbEmulatedDatabase.Buggify"/> property).</para>
		/// </remarks>
		[PublicAPI]
		public sealed class FakeDbBuggify
		{

			internal FakeDbBuggify(FdbEmulatedDatabase store)
			{
				this.Store = store;
			}

			private FdbEmulatedDatabase Store { get; }

			/// <summary>Per-key count of pending deferred watch checks (see <see cref="SuppressNextWatchCheck"/>), consumed by the commit-time trigger check.</summary>
			private Dictionary<Slice, int>? Suppressions { get; set; }

			/// <summary>Live timers scheduled by <see cref="FireWatchesAfter"/>, held so they are not collected before they fire.</summary>
			private List<ITimer>? Timers { get; set; }

			/// <summary>Seeded chaos configuration, or <see langword="null"/> when automatic injection is off (the default).</summary>
			/// <remarks>Set to a <see cref="FakeDbBuggifyChaos"/> to have every commit possibly inject a spurious fire or defer a watch
			/// check, deterministically under the seed. Clearing it (or calling <see cref="Disable"/>) returns to clean watch semantics.</remarks>
			public FakeDbBuggifyChaos? Chaos { get; set; }

			/// <summary>Turns off all automatic injection (clears <see cref="Chaos"/>) for a test that needs clean, deterministic watches.</summary>
			/// <remarks>Deterministic on-demand injection (<see cref="FireWatches"/> / <see cref="SuppressNextWatchCheck"/> / <see cref="FireWatchesAfter"/>)
			/// is unaffected: it only fires when the test explicitly calls it, so there is nothing to disable.</remarks>
			public void Disable() => this.Chaos = null;

			/// <summary>Enables seeded chaos with a stable seed derived from <paramref name="name"/> - the one-line opt-in for a whole suite: buggify every watch-arming test with a profile that is distinct per name yet reproducible across runs.</summary>
			/// <param name="name">A stable, distinct identifier (typically the test or suite name) that fixes the injection profile.</param>
			/// <param name="spuriousFireRate">Per-commit probability of a fan-out spurious fire on one armed key.</param>
			/// <param name="deferredCheckRate">Per-check probability that a watch check is deferred (skipped) this commit.</param>
			/// <returns>The installed chaos profile (for further tuning, e.g. clearing one of the rates).</returns>
			/// <remarks>Recommended for any suite whose code arms watches: it forces that code through the weak watch contract (spurious
			/// and reverted-miss fires) without giving up reproducibility. Chaos never produces a contract-illegal outcome, so it is safe
			/// to leave on for suites that do not assert exact watch timing.</remarks>
			public FakeDbBuggifyChaos EnableChaos(string name, double spuriousFireRate = 0.25, double deferredCheckRate = 0.25)
			{
				Contract.NotNull(name);
				var chaos = new FakeDbBuggifyChaos(StableSeed(name)) { SpuriousFireRate = spuriousFireRate, DeferredCheckRate = deferredCheckRate };
				this.Chaos = chaos;
				return chaos;

				// FNV-1a over the name: a process-stable hash (unlike string.GetHashCode, which is randomized per run since .NET Core)
				static int StableSeed(string s)
				{
					uint h = 2166136261u;
					foreach (var c in s)
					{
						h = (h ^ (byte) c) * 16777619u;
						h = (h ^ (byte) (c >> 8)) * 16777619u;
					}
					return unchecked((int) h);
				}
			}

			/// <summary>Injects an immediate spurious fire of every watch registered on <paramref name="key"/>, then unregisters them (the FDBV-026 per-key fan-out shape).</summary>
			/// <param name="key">The (fully-encoded) watched key, as registered by <c>tr.Watch(...)</c>.</param>
			/// <returns>The number of watches fired (0 when no watch was armed on the key: a test can assert the injection landed).</returns>
			/// <remarks>This is exactly the real-client entanglement shape - one stale-armed sibling dragging every co-registered watch
			/// on the key - and degenerates to a single spurious fire when one watch is registered. The watched value is NOT changed,
			/// so a correct consumer re-reads and observes no change.</remarks>
			public int FireWatches(Slice key)
			{
				List<WatchNode>? nodes;
				using (this.Store.GlobalLock.GetWriteLock())
				{
					if (!this.Store.ActiveWatches.TryGetValue(key, out nodes) || nodes is null || nodes.Count == 0)
					{
						return 0;
					}
					this.Store.ActiveWatches.Remove(key);
				}

				// trigger OUTSIDE the lock, like the commit path does
				foreach (var node in nodes)
				{
					node.Trigger();
				}
				return nodes.Count;
			}

			/// <summary>Arms a one-shot deferred watch check for <paramref name="key"/>: the next commit-time check that would fire a watch on the key is skipped, leaving the watch registered with its original baseline (the FDBV-027 missed-fire shape).</summary>
			/// <param name="key">The (fully-encoded) watched key, as registered by <c>tr.Watch(...)</c>.</param>
			/// <remarks>
			/// <para>This reproduces the real mechanism, not just the symptom. The watch stack is level-triggered against the expected
			/// value, so a single skipped check self-heals: if a later commit still leaves the value differing from the baseline the
			/// watch fires (late, which the contract permits), and only a net-reverted (ABA) change leaves it pending forever.</para>
			/// <para>Because the skipped check can never fire a watch the contract guarantees (the value still differs at a later
			/// observation would fire it), suppression is always within the contract envelope. Calls stack: N calls skip the next N checks.</para>
			/// </remarks>
			public void SuppressNextWatchCheck(Slice key)
			{
				var copy = key.Copy();
				using (this.Store.GlobalLock.GetWriteLock())
				{
					var map = this.Suppressions ??= new(Slice.Comparer.Default);
					map[copy] = (map.TryGetValue(copy, out var count) ? count : 0) + 1;
				}
			}

			/// <summary>Schedules a spurious fire of the watches on <paramref name="key"/> after <paramref name="delay"/> elapses on the store clock (the timed variant of <see cref="FireWatches"/>, BG-5).</summary>
			/// <param name="key">The (fully-encoded) watched key, as registered by <c>tr.Watch(...)</c>.</param>
			/// <param name="delay">Delay measured on <see cref="FdbEmulatedDatabase.Time"/>.</param>
			/// <remarks>Deterministic only when the store runs on an injectable clock (e.g. a <c>FakeTimeProvider</c>): the test advances
			/// virtual time and the fire lands exactly then. Under the system clock it degrades to wall-clock timing, the same caveat as
			/// the retry backoff - reproducible enough for a soak, not for a byte-exact replay.</remarks>
			public void FireWatchesAfter(Slice key, TimeSpan delay)
			{
				var copy = key.Copy();
				var timer = this.Store.Time.CreateTimer(
					static state =>
					{
						var (facet, k) = ((FakeDbBuggify Facet, Slice Key)) state!;
						facet.FireWatches(k);
					},
					(this, copy),
					delay,
					Timeout.InfiniteTimeSpan
				);
				(this.Timers ??= [ ]).Add(timer);
			}

			/// <summary>Called under the store write lock from the commit-time watch check: decides whether the check for <paramref name="key"/> is skipped this commit (a manual suppression or a chaos deferral), consuming at most one manual suppression per key per commit.</summary>
			internal bool ShouldDeferWatchCheck(Slice key, long commitVersion, ref HashSet<Slice>? decidedThisCommit)
			{
				if (decidedThisCommit?.Contains(key) == true)
				{ // already decided (and consumed) for this key at an earlier branch of the same commit
					return true;
				}

				bool defer = TryConsumeSuppression(key) || (this.Chaos is { } chaos && chaos.ShouldDeferCheck(commitVersion, key));
				if (defer)
				{
					(decidedThisCommit ??= new(Slice.Comparer.Default)).Add(key);
				}
				return defer;
			}

			private bool TryConsumeSuppression(Slice key)
			{
				if (this.Suppressions is null || !this.Suppressions.TryGetValue(key, out var count) || count <= 0)
				{
					return false;
				}
				this.Suppressions[key] = count - 1;
				return true;
			}

			/// <summary>Called at the end of a commit (before the pending watches are triggered): if chaos rolls a spurious fire, picks one armed key deterministically and fans it out into <paramref name="watchesToTrigger"/>, unregistering it.</summary>
			internal void MaybeInjectSpuriousFire(long commitVersion, ref List<WatchNode>? watchesToTrigger)
			{
				if (this.Chaos is not { } chaos || !chaos.ShouldSpuriousFire(commitVersion))
				{
					return;
				}

				List<WatchNode>? fired = null;
				using (this.Store.GlobalLock.GetWriteLock())
				{
					if (this.Store.ActiveWatches.Count == 0)
					{
						return;
					}

					// deterministic pick: sort the armed keys so the choice does not depend on Dictionary iteration order
					var keys = new List<Slice>(this.Store.ActiveWatches.Keys);
					keys.Sort(Slice.Comparer.Default);
					var key = keys[chaos.PickIndex(commitVersion, keys.Count)];

					if (this.Store.ActiveWatches.TryGetValue(key, out var nodes) && nodes is not null)
					{
						this.Store.ActiveWatches.Remove(key);
						fired = nodes;
					}
				}

				if (fired is not null)
				{
					(watchesToTrigger ??= [ ]).AddRange(fired);
				}
			}

		}

		/// <summary>Seeded configuration for automatic watch buggify (see <see cref="FakeDbBuggify.Chaos"/>): every commit may inject a spurious fire or defer a watch check, deterministically under the seed.</summary>
		/// <remarks>
		/// <para>Decisions are pure deterministic hashes of (<see cref="Seed"/>, commit version, key), NOT draws from a stateful PRNG,
		/// so a chaos run is a pure function of (seed, transaction schedule) and never depends on hash-table iteration order - it replays
		/// byte-for-byte. Choose a seed derived from the test name so each test gets a stable, distinct injection profile.</para>
		/// <para>Both modelled classes stay inside the watch contract: the spurious fire is always permitted, and the deferred check only
		/// loses fires a real cluster may lose (reverted transients), so a chaos-on store can run under arbitrary consumer suites without
		/// producing false bug reports.</para>
		/// </remarks>
		[PublicAPI]
		public sealed class FakeDbBuggifyChaos
		{

			/// <summary>Creates a chaos profile with the given seed.</summary>
			public FakeDbBuggifyChaos(int seed)
			{
				this.Seed = seed;
			}

			/// <summary>Seed that fixes the whole injection profile.</summary>
			public int Seed { get; }

			/// <summary>Probability in [0, 1] that a commit injects a per-key fan-out spurious fire on one armed key (the FDBV-026 shape).</summary>
			public double SpuriousFireRate { get; init; } = 0.25;

			/// <summary>Probability in [0, 1] that a commit-time watch check is deferred, i.e. skipped this commit (the FDBV-027 shape).</summary>
			public double DeferredCheckRate { get; init; } = 0.25;

			internal bool ShouldSpuriousFire(long commitVersion) => Fraction(Mix(this.Seed, commitVersion, default, SaltSpurious)) < this.SpuriousFireRate;

			internal bool ShouldDeferCheck(long commitVersion, Slice key) => Fraction(Mix(this.Seed, commitVersion, key, SaltDefer)) < this.DeferredCheckRate;

			internal int PickIndex(long commitVersion, int count) => (int) (Mix(this.Seed, commitVersion, default, SaltPick) % (ulong) count);

			private const ulong SaltSpurious = 0x9E3779B97F4A7C15UL;
			private const ulong SaltDefer = 0xC2B2AE3D27D4EB4FUL;
			private const ulong SaltPick = 0x165667B19E3779F9UL;

			// FNV-1a 64-bit over seed + commit version + salt + key bytes: a cheap, stable, iteration-order-independent hash
			private static ulong Mix(int seed, long commitVersion, Slice key, ulong salt)
			{
				const ulong Prime = 1099511628211UL;
				ulong h = 14695981039346656037UL;
				h = Fold(h, (uint) seed);
				h = Fold(h, (ulong) commitVersion);
				h = Fold(h, salt);
				foreach (var b in key.Span)
				{
					h = (h ^ b) * Prime;
				}
				return h;

				static ulong Fold(ulong h, ulong value)
				{
					for (int i = 0; i < 8; i++)
					{
						h = (h ^ (byte) value) * Prime;
						value >>= 8;
					}
					return h;
				}
			}

			// top 53 bits of the hash mapped to [0, 1), the standard double construction
			private static double Fraction(ulong h) => (h >> 11) * (1.0 / (1UL << 53));

		}

		/// <summary>Read-only implementation of the DirectoryLayer, that can lookup the path of the <see cref="FdbDirectorySubspace"/> that contains a key</summary>
		/// <remarks>Uses the DirectoryLayer metadata from a <see cref="Snapshot"/> to generate a map</remarks>
		public sealed class DirectoryMapper : IFdbDirectoryLayerMapper
		{

			private const int SUBDIRS = 0;

			/// <summary>Maps key prefixes to path of the corresponding directory</summary>
			private ColaRangeDictionary<Slice, (FdbPath Path, int PrefixLen)> KeyMap { get; } = new(Slice.Comparer.Default);

			/// <summary>Maps the path of directories to their corresponding key prefix</summary>
			public Dictionary<FdbPath, Slice> Paths { get; } = new();

			internal DirectoryMapper() { }

			public static DirectoryMapper CreateFromSnapshot(Snapshot snapshot)
			{
				var mapper = new DirectoryMapper();
				mapper.LoadFromSnapshot(snapshot);
				return mapper;
			}

			/// <inheritdoc />
			public IReadOnlyDictionary<FdbPath, Slice> GetPaths() => this.Paths;

			/// <inheritdoc />
			public bool TryMapPath(FdbPath path, out Slice prefix)
			{
				return this.Paths.TryGetValue(path, out prefix);
			}

			/// <inheritdoc />
			public bool TryMapKey(Slice key, out FdbPath path, out Slice mappedKey)
			{
				if (!this.KeyMap.TryGetValue(key, out var entry))
				{
					path = default;
					mappedKey = default;
					return false;
				}

				path = entry.Path;
				mappedKey = key[entry.PrefixLen..];
				return true;
			}

			private void LoadFromSnapshot(Snapshot snapshot)
			{
				//note: this is a very _basic_ implementation of the DL that will only extract the paths in the DL

				this.KeyMap.Clear();
				this.Paths.Clear();

				BrowseRecursive(snapshot, FdbPath.Root);
			}

			private void BrowseRecursive(Snapshot snapshot, FdbPath path)
			{
				var children = ListInternal(snapshot, PartitionDescriptor.Root, path, false);
				if (children is null) return;

				foreach (var p in children)
				{
					this.KeyMap.Mark(p.Prefix, FdbKey.Increment(p.Prefix), (p.Path, p.Prefix.Count));
					this.Paths[p.Path] = p.Prefix;

					BrowseRecursive(snapshot, p.Path);
				}
			}

			[DebuggerDisplay("Path={Path}, Prefix={Prefix}, Layer={Layer}")]
			internal readonly struct Node
			{

				public Node(FdbPath path, Key prefix, string? layer, PartitionDescriptor partition, PartitionDescriptor parentPartition, Key prefixInParentPartition, List<KeyValuePair<Slice, Slice>> validationChain)
				{
					this.Prefix = prefix;
					this.Path = path;
					this.Layer = layer;
					this.Partition = partition;
					this.ParentPartition = parentPartition;
					this.PrefixInParentPartition = prefixInParentPartition;
					this.ValidationChain = validationChain;
				}

				public readonly Key Prefix;
				public readonly FdbPath Path;
				public readonly string? Layer;
				public readonly PartitionDescriptor Partition;
				public readonly PartitionDescriptor ParentPartition;
				public readonly Key PrefixInParentPartition;
				public readonly List<KeyValuePair<Slice, Slice>> ValidationChain;

				public bool Exists => !this.Prefix.IsNull;

			}

			[DebuggerDisplay("Path={Path}, Prefix={ContentPrefix}, Parent=({Parent})")]
			internal sealed class PartitionDescriptor
			{
				public static readonly PartitionDescriptor Root = new PartitionDescriptor(FdbPath.Root, Key.Empty, null);

				public FdbPath Path { get; }

				public PartitionDescriptor? Parent { get; }

				public Key ContentPrefix { get; }

				public Key NodesPrefix { get; }

				public PartitionDescriptor(FdbPath path, Key content, PartitionDescriptor? parent)
				{
					// the last segment must have the expected layer id
					if (path.Count > 0 && string.IsNullOrEmpty(path[^1].LayerId))
					{
						path = path.GetParent()[path[^1].Name, FdbDirectoryPartition.LayerId];
					}

					this.Path = path;
					this.Parent = parent;
					this.ContentPrefix = content;
					this.NodesPrefix = content + FdbKey.DirectoryPrefixSpan;
				}

				/// <summary>Return a child partition of the current partition</summary>
				public PartitionDescriptor CreateChild(FdbPath path, Key prefix)
				{
					return new PartitionDescriptor(path, prefix, this);
				}

			}

			internal static List<(FdbPath Path, Slice Prefix)>? ListInternal(Snapshot snapshot, PartitionDescriptor partition, FdbPath path, bool throwIfMissing)
			{
				var node = Find(snapshot, partition, path);

				if (!node.Exists)
				{
					if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
					return null;
				}

				return SubdirNamesAndNodes(snapshot, node.Partition, node.Prefix, includeLayers: true)
				       .Select(kvp => (
						   node.Path[new FdbPathSegment(kvp.Name, kvp.LayerId)],
						   kvp.Prefix
					   ))
				       .ToList()
					;
			}

			private static readonly Slice LayerAttribute = Slice.FromStringAscii("layer");

			/// <summary>Returns the list of names and nodes of all children of the specified node</summary>
			private static List<(string Name, string? LayerId, Slice Prefix)> SubdirNamesAndNodes(Snapshot snapshot, PartitionDescriptor partition, Key prefix, bool includeLayers)
			{
				var sd = partition.NodesPrefix + TuPack.EncodeKey(prefix.Slice, SUBDIRS);

				var items = snapshot
					.ScanRange(sd, sd + (byte) 0xFF)
					.Select(kvp =>
						{
							var x = kvp.Key[sd.Count..];
							var y = TuPack.DecodeKey<string>(x);
							return (Name: y ?? string.Empty, Prefix: kvp.Value.Slice);
						}
					)
					.ToArray();

				if (items.Length == 0)
				{
					return [ ];
				}

				// fetch the layers from the corresponding directories
				var layers = includeLayers ? items.Select(item => snapshot.Read(partition.NodesPrefix + TuPack.EncodeKey(item.Prefix, LayerAttribute))).ToArray() : null;

				var res = new List<(string, string?, Slice)>(items.Length);
				for (int i = 0; i < items.Length; i++)
				{
					res.Add((items[i].Name, layers != null ? (layers[i].Slice.ToStringUtf8() ?? string.Empty) : null, items[i].Prefix));
				}

				return res;
			}

			/// <summary>Finds a node subspace, given its path, by walking the tree from the root.</summary>
			/// <returns>Node if it was found, or null</returns>
			private static Node Find(Snapshot snapshot, PartitionDescriptor partition, FdbPath path)
			{

				// look for the node by traversing from the root down. jumping over when crossing a partition...

				var current = partition.NodesPrefix;

				var chain = new List<KeyValuePair<Slice, Slice>>();

				int i = 0;
				var layer = FdbDirectoryPartition.LayerId; // the root is by convention a "partition"
				var parent = partition;
				var prefixInParentPartition  = current;
				while (i < path.Count)
				{

					// maybe use the node cache, if allowed
					var key = partition.NodesPrefix + TuPack.EncodeKey(current.Slice, SUBDIRS, path[i].Name);
					current = (Key) snapshot.Read(key);

					if (current.IsNull)
					{
						return new Node(path, default, null, partition, parent, default, chain);
					}

					// get the layer id of this node
					layer = snapshot.Read(partition.NodesPrefix + TuPack.EncodeKey(current.Slice, LayerAttribute)).Slice.ToStringUtf8() ?? string.Empty;

					parent = partition;

					prefixInParentPartition = current;
					if (layer == FdbDirectoryPartition.LayerId)
					{ // jump to that partition's node subspace
						partition = partition.CreateChild(path.Substring(0, i + 1), current);
						current = partition.NodesPrefix;
					}

					++i;
				}

				// patch the layer id, if it is missing from the last segment (can be omitted by caller)
				if (path.Count > 0 && !string.IsNullOrEmpty(layer))
				{
					var lastSeg = path[^1];
					if (lastSeg.LayerId != layer)
					{
						path = path.GetParent()[lastSeg.Name, layer];
					}
				}

				return new Node(path, current, layer, partition, parent, prefixInParentPartition, chain);
			}

		}
	
	}

	/// <summary>Helper method to inspect the internals of a FakeDb, for troubleshooting/testing purpose</summary>
	/// <remarks>
	/// <para>CAUTION: this exposes internal structure that is not guaranteed to be thread-safe, and could cause unexpected behavior or deadlocks!</para>
	/// <para>A committed <see cref="Snapshot"/> is inspected through its own members (<see cref="Snapshot.Count"/>, <see cref="Snapshot.ReadData"/>, <see cref="Snapshot.ReadConflicts"/>, <see cref="Snapshot.Diff"/>), which read through the committed-store seam and therefore work over any storage. What remains here is the in-flight transaction state, which has no storage.</para>
	/// </remarks>
	public static class FakeDbDebugger
	{

		public static ColaRangeDictionary<Key, Mutation> GetSnapshotMutations(FdbEmulatedDatabase.ReadYourWritesSnapshot snapshot) => snapshot.Mutations;

		public static ColaRangeSet<Key> GetSnapshotReadConflicts(FdbEmulatedDatabase.ReadYourWritesSnapshot snapshot) => snapshot.ReadConflicts;

		public static ColaRangeSet<Key> GetSnapshotWriteConflicts(FdbEmulatedDatabase.ReadYourWritesSnapshot snapshot) => snapshot.WriteConflicts;

	}

}
