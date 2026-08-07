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

namespace FoundationDB.FdbLite
{
	using FoundationDB.Client.Core;
	using FoundationDB.Storage;
	using FoundationDB.Client;

	/// <summary>The persistent backend behind the emulator's committed-store seam: an engine generation seen as an <see cref="IFdbCommittedStore"/>.</summary>
	/// <remarks>
	/// <para>A READABLE store wraps one committed generation (pinned by its owner); <see cref="Copy"/> opens the engine's writable copy-on-write generation, whose mutation surface (<see cref="Remove"/>, <see cref="RemoveRange"/>, the set indexer) drives the tree writer directly - the write path copies caller bytes into pages and extents, so the shared transaction machinery needs no backend allocator.</para>
	/// <para>Memory contract (the ruled two-tier form): every <see cref="Key"/>/<see cref="Value"/> this store returns is a caller-owned heap copy (arena-less, so the machinery's interning treats it as stable); only the <see cref="Read{TState,TResult}"/> delegate leg passes spans straight over engine pages, scoped to the delegate call inside the owner's pin.</para>
	/// </remarks>
	public sealed class FdbLiteCommittedStore : IFdbCommittedStore<FdbLiteCommittedCursor>
	{

		/// <summary>Readable store over a committed generation (the caller owns the pin that keeps it valid).</summary>
		public FdbLiteCommittedStore(FdbLiteEngine engine, uint root, ulong keyCount)
		{
			Contract.NotNull(engine);
			this.Engine = engine;
			this.FrozenRoot = root;
			this.FrozenKeyCount = keyCount;
		}

		/// <summary>Writable store over the in-flight next generation.</summary>
		private FdbLiteCommittedStore(FdbLiteEngine engine, FdbLiteTreeWriter writer, ulong baseKeyCount)
		{
			this.Engine = engine;
			this.Writer = writer;
			this.FrozenKeyCount = baseKeyCount;
		}

		private FdbLiteEngine Engine { get; }

		/// <summary>Tree writer of the generation being built (null on a readable store)</summary>
		public FdbLiteTreeWriter? Writer { get; }

		private uint FrozenRoot { get; }

		private ulong FrozenKeyCount { get; }

		/// <summary>Root to read from: the writer's live root while building, the frozen root otherwise</summary>
		private uint CurrentRoot => this.Writer?.Root ?? this.FrozenRoot;

		/// <summary>The store as this generation sees it: a writable generation reads through the writer's buffered pages, a frozen one straight off the pager.</summary>
		private IFdbLitePager ReadPager => this.Writer?.PagerView ?? this.Engine.Pager;

		/// <inheritdoc />
		public int Count => (int) (this.Writer != null ? (ulong) ((long) this.FrozenKeyCount + this.Writer.KeyCountDelta) : this.FrozenKeyCount);

		#region Reads...

		/// <inheritdoc />
		public bool TryGetValue(Key key, out Value value)
		{
			if (FdbLiteTreeReader.TryGetValue(this.ReadPager, this.CurrentRoot, key.Span, out var span))
			{ // the seam hands out caller-owned memory: copy off the page
				value = new Value(Slice.FromBytes(span));
				return true;
			}
			value = default;
			return false;
		}

		/// <inheritdoc />
		public bool ContainsKey(Key key) => FdbLiteTreeReader.TryGetValue(this.ReadPager, this.CurrentRoot, key.Span, out _);

		/// <inheritdoc />
		public TResult Read<TState, TResult>(Key key, TState state, FdbValueDecoder<TState, TResult> decoder)
		{
			// the zero-copy leg: the span points into engine pages (or a contiguous extent) and only
			// lives for the delegate call, inside the owner's pin
			return FdbLiteTreeReader.TryGetValue(this.ReadPager, this.CurrentRoot, key.Span, out var span)
				? decoder(state, span, true)
				: decoder(state, default, false);
		}

		/// <inheritdoc />
		public FdbLiteCommittedCursor GetCursor() => new(this.ReadPager, this.CurrentRoot);

		/// <inheritdoc />
		IFdbCommittedCursor IFdbCommittedStore.GetCursor() => GetCursor();

		/// <inheritdoc />
		public IEnumerable<KeyValuePair<Key, Value>> Scan(Key begin, Key end, bool reversed)
		{
			var cursor = new FdbLiteTreeCursor(this.ReadPager, this.CurrentRoot);
			if (!reversed)
			{
				if (!cursor.SeekCeiling(begin.Span))
				{
					yield break;
				}
				do
				{
					if (cursor.CurrentKey.SequenceCompareTo(end.Span) >= 0)
					{
						yield break;
					}
					yield return Materialize(ref cursor);
				}
				while (cursor.MoveNext());
			}
			else
			{
				if (!cursor.SeekFloor(end.Span, orEqual: false))
				{
					yield break;
				}
				do
				{
					if (cursor.CurrentKey.SequenceCompareTo(begin.Span) < 0)
					{
						yield break;
					}
					yield return Materialize(ref cursor);
				}
				while (cursor.MovePrevious());
			}
		}

		/// <inheritdoc />
		public IEnumerable<KeyValuePair<Key, Value>> IterateOrdered()
		{
			var cursor = new FdbLiteTreeCursor(this.ReadPager, this.CurrentRoot);
			if (cursor.SeekFirst())
			{
				do { yield return Materialize(ref cursor); } while (cursor.MoveNext());
			}
		}

		private static KeyValuePair<Key, Value> Materialize(ref FdbLiteTreeCursor cursor)
			=> new(new Key(Slice.FromBytes(cursor.CurrentKey)), new Value(Slice.FromBytes(cursor.CurrentValue)));

		/// <inheritdoc />
		public void VisitRange<TState>(Key begin, Key end, bool reversed, TState state, FdbCommittedRangeVisitor<TState> visitor)
		{
			// the O(1)-in-N read: the visitor sees key/value spans straight over the mapped pages, no per-pair copy;
			// only the cursor's own path arrays allocate, once per call (mirrors Scan's bounds exactly)
			var cursor = new FdbLiteTreeCursor(this.ReadPager, this.CurrentRoot);
			if (!reversed)
			{
				if (!cursor.SeekCeiling(begin.Span)) return;
				do
				{
					if (cursor.CurrentKey.SequenceCompareTo(end.Span) >= 0) return;
					if (!visitor(state, cursor.CurrentKey, cursor.CurrentValue)) return;
				}
				while (cursor.MoveNext());
			}
			else
			{
				if (!cursor.SeekFloor(end.Span, orEqual: false)) return;
				do
				{
					if (cursor.CurrentKey.SequenceCompareTo(begin.Span) < 0) return;
					if (!visitor(state, cursor.CurrentKey, cursor.CurrentValue)) return;
				}
				while (cursor.MovePrevious());
			}
		}

		#endregion

		#region Mutation / publish surface...

		/// <inheritdoc />
		public IFdbCommittedStore Copy()
		{
			Contract.Requires(this.Writer == null, "the writable generation cannot be copied");
			return new FdbLiteCommittedStore(this.Engine, this.Engine.BeginWrite(), this.FrozenKeyCount);
		}

		/// <inheritdoc />
		/// <remarks>Idempotent, commit-aware (a published writer is no longer in flight, so a late Discard is a no-op), and a no-op on a frozen (writer-less) store; the engine rolls an abandoned generation's allocations, buffered pages, and recorded frees all the way back.</remarks>
		public void Discard()
		{
			if (this.Writer is { } writer)
			{
				this.Engine.TryAbandon(writer);
			}
		}

		/// <inheritdoc />
		public bool Remove(Key key)
		{
			Contract.Requires(this.Writer != null);
			return this.Writer!.Remove(key.Span);
		}

		/// <inheritdoc />
		public bool TryGetKeyValue(Key key, out KeyValuePair<Key, Value> entry)
		{
			var cursor = new FdbLiteTreeCursor(this.ReadPager, this.CurrentRoot);
			if (cursor.SeekFloor(key.Span, orEqual: true) && cursor.CurrentKey.SequenceEqual(key.Span))
			{
				entry = Materialize(ref cursor);
				return true;
			}
			entry = default;
			return false;
		}

		/// <inheritdoc />
		public int RemoveRange(Key begin, Key end)
		{
			Contract.Requires(this.Writer != null);
			return this.Writer!.RemoveRange(begin.Span, end.Span);
		}

		/// <inheritdoc />
		public Value this[Key key]
		{
			set
			{
				Contract.Requires(this.Writer != null);
				this.Writer!.Insert(key.Span, value.Span);
			}
		}

		#endregion

	}

	/// <summary>Ordered bidirectional cursor over one committed engine generation, as the seam's concrete struct (the FL-15 monomorphization target).</summary>
	/// <remarks>
	/// <para><see cref="CurrentKey"/>/<see cref="CurrentValue"/> point straight into pager memory (zero-copy, valid until the next move or the generation's pin release); <see cref="CopyKey"/>/<see cref="CopyValue"/>/<see cref="CopyCurrent"/> copy the bytes off the page for the entries the machinery retains. The engine's cost lives entirely in those <c>Copy*</c> calls, so a selector walk that only compares keys pays nothing per step.</para>
	/// <para>Copies of this struct share position state through the wrapped tree cursor: advance one instance, not copies of it.</para>
	/// </remarks>
	public struct FdbLiteCommittedCursor : IFdbCommittedCursor
	{

		public FdbLiteCommittedCursor(IFdbLitePager pager, uint root)
		{
			this.Tree = new FdbLiteTreeCursor(pager, root);
		}

		private FdbLiteTreeCursor Tree;

		/// <summary>The cursor is positioned on a real key (false before the first key, or after a failed seek)</summary>
		private bool Positioned;

		private bool BeforeFirst;

		/// <inheritdoc />
		public ReadOnlySpan<byte> CurrentKey => this.Positioned ? this.Tree.CurrentKey : default;

		/// <inheritdoc />
		public ReadOnlySpan<byte> CurrentValue => this.Positioned ? this.Tree.CurrentValue : default;

		/// <inheritdoc />
		public Key CopyKey() => this.Positioned ? new Key(Slice.FromBytes(this.Tree.CurrentKey)) : Key.Nil;

		/// <inheritdoc />
		public Value CopyValue() => this.Positioned ? new Value(Slice.FromBytes(this.Tree.CurrentValue)) : Value.Nil;

		/// <inheritdoc />
		public KeyValuePair<Key, Value> CopyCurrent() => this.Positioned ? new(CopyKey(), CopyValue()) : default;

		/// <inheritdoc />
		public bool Seek(Key key, bool orEqual)
		{
			this.BeforeFirst = false;
			return this.Positioned = this.Tree.SeekFloor(key.Span, orEqual);
		}

		/// <inheritdoc />
		public bool SeekFirst()
		{
			this.BeforeFirst = false;
			return this.Positioned = this.Tree.SeekFirst();
		}

		/// <inheritdoc />
		public void SeekBeforeFirst()
		{
			this.BeforeFirst = true;
			this.Positioned = false;
		}

		/// <inheritdoc />
		public bool Next()
		{
			if (this.BeforeFirst)
			{
				return SeekFirst();
			}
			// a failed step keeps the cursor on the last key (Positioned stays true), matching the seam contract
			return this.Positioned && this.Tree.MoveNext();
		}

		/// <inheritdoc />
		public bool Previous()
		{
			if (this.BeforeFirst)
			{
				return false;
			}
			return this.Positioned && this.Tree.MovePrevious();
		}

	}

}
