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

namespace FoundationDB.Testing
{
	using System.Runtime.InteropServices;
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using SnowBank.Collections.CacheOblivious;
	using SnowBank.Threading;
	using static FoundationDB.Testing.FakeDbStore;

	/// <summary>Simulates a FoundationDB cluster running in-memory in the local process</summary>
	/// <remarks>This emulator is currently <b>EXPERIMENTAL</b> and may not accurately reproduce the behavior of an actual fdb cluster, most notably due to the absence of network latency!</remarks>
	[PublicAPI]
	[DebuggerDisplay("Version={CurrentSnapshotUnsafe.Version}, Count={CurrentSnapshotUnsafe.Data.Count}")]
	public class FakeDbStore : IFdbDatabaseHandler
	{

		[PublicAPI]
		public enum Operation
		{
			Invalid = 0,
			Set,
			Clear,
			ClearRange,

			//note: FdbMutationType offset by 10
			Add = 10 + FdbMutationType.Add,
			BitAnd = 10 + FdbMutationType.BitAnd,
			BitOr = 10 + FdbMutationType.BitOr,
			BitXor = 10 + FdbMutationType.BitXor,
			AppendIfFits = 10 + FdbMutationType.AppendIfFits,
			Max = 10 + FdbMutationType.Max,
			Min = 10 + FdbMutationType.Min,
			VersionStampedKey = 10 + FdbMutationType.VersionStampedKey,
			VersionStampedValue = 10 + FdbMutationType.VersionStampedValue,
			ByteMin = 10 + FdbMutationType.ByteMin,
			ByteMax = 10 + FdbMutationType.ByteMax,
			CompareAndClear = 10 + FdbMutationType.CompareAndClear,
		}

		[PublicAPI]
		[DebuggerDisplay("{ToString(),nq}")]
		public sealed record Mutation
		{
			public Operation Op { get; }

			public Value Parameter { get; }

			public Mutation? Next { get; internal set; }

			public Mutation? Tail { get; internal set; }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Value GetEffectiveValue() => this.Op is Operation.Clear or Operation.ClearRange ? default : this.Parameter;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private Mutation(Operation op, Value parameter)
			{
				this.Op = op;
				this.Parameter = parameter;
			}

			public bool IsKv() => (this.Op is Operation.Set or Operation.Clear) && this.Next == null;

			public bool IsRange() => this.Op is Operation.ClearRange;

			public bool IsAtomic() => (this.Op >= Operation.Add) || this.Next != null;

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static Mutation Set(Value value) => new(Operation.Set, value);

			public static Mutation Clear() => new(Operation.Clear, default);

			public static Mutation ClearRange() => new(Operation.ClearRange, default);

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static Mutation Atomic(FdbMutationType type, Value parameter) => new((Operation) (10 + type), parameter);

			public override string ToString()
			{
				string literal = this.Op switch
				{
					Operation.Clear => "Clear()",
					Operation.ClearRange => "ClearRange(...)",
					_ => $"{this.Op}({this.Parameter:V})",
				};
				return this.Next is null ? literal : literal + " + " + this.Next.ToString();
			}
		}

		[PublicAPI]
		public sealed record Arena : IDisposable
		{

			private PooledSliceAllocator Keys { get; }

			private PooledSliceAllocator Values { get; }

			public Arena(int keySize, int valueSize, ArrayPool<byte> pool)
			{
				this.Keys = new(keySize, pool);
				this.Values = new(valueSize, pool);
			}

			public void Dispose()
			{
				this.Keys.Dispose();
				this.Values.Dispose();
			}

			internal void Clear()
			{
				this.Keys.Clear();
				this.Values.Clear();
			}

			/// <summary>Allocates a key in this arena (without its content known yet)</summary>
			public Key AllocateKey(int count, bool clear = false)
			{
				var tmp = this.Keys.Allocate(count);
				if (clear) tmp.AsSpan().Clear();
				return new Key(tmp.AsSlice(), this);
			}

			/// <summary>Allocates a value in this arena (without its content known yet)</summary>
			public Value AllocateValue(int count, bool clear = false)
			{
				var tmp = this.Values.Allocate(count);
				if (clear) tmp.AsSpan().Clear();
				return new Value(tmp.AsSlice(), this);
			}

			/// <summary>Copies a <see cref="Key"/> to this Arena, unless it already belongs to it</summary>
			public Key InternKey(Key key)
			{
				return key.Arena == this || key.Arena == null ? key : new Key(this.Keys.Intern(key.Slice), this);
			}

			public Key InternKey(Slice data)
			{
				return data.Count != 0 ? new Key(this.Keys.Intern(data), this) : new Key(data);
			}

			/// <summary>Copies a pair of keys to this Arena, unless they already belong to it</summary>
			public KeyRange InternKeyRange(Key begin, Key end)
			{
				if (begin.Arena == this && end.Arena == this)
				{
					return new KeyRange(begin, end);
				}

				if (end.Slice.StartsWith(begin.Slice))
				{ // begin = ABC, end = ABCDEF, we can merge both!
					if (end.Arena == this)
					{
						return new KeyRange(new Key(end.Slice.Substring(0, begin.Count), this), end);
					}

					var tmp = this.Keys.Intern(end.Slice);
					return new KeyRange(new Key(tmp.Substring(0, begin.Count), this), new Key(tmp, this));
				}

				// need to intern both of them
				return new KeyRange(InternKey(begin), InternKey(end));
			}

			public Key InternKeyZero(Slice data)
			{
				int n = data.Count;
				if (n == 0) return new Key(Slice.FromByte(0));
				var tmp = this.Keys.Allocate(n + 1);
				data.CopyTo(tmp);
				// note: writing through AsSpan() because the ArraySegment indexer does not exist on netstandard2.0
				tmp.AsSpan()[n] = 0;
				return new Key(tmp, this);
			}

			public Key InternKey(ReadOnlySpan<byte> data)
			{
				return data.Length != 0 ? new Key(this.Keys.Intern(data), this) : Key.Empty;
			}

			/// <summary>Copies a <see cref="Value"/> to this Arena, unless it already belongs to it</summary>
			public Value InternValue(Value value)
			{
				return value.Arena == this || value.Arena == null ? value : new Value(this.Values.Intern(value.Slice), this);
			}

			public Value InternValue(Slice data)
			{
				return data.Count != 0 ? new Value(this.Values.Intern(data), this) : new Value(data, null);
			}

			public Value InternValue(ReadOnlySpan<byte> data)
			{
				return data.Length != 0 ? new Value(this.Values.Intern(data), this) : Value.Empty;
			}

			internal KeyRange InternKeyRange(Slice key)
			{
				var x = this.Keys.Intern(key, 0);
				return new(new Key(x[..^1], this), new Key(x, this));
			}

			internal KeyRange InternKeyRange(ReadOnlySpan<byte> key)
			{
				var x = this.Keys.Intern(key, 0);
				return new(new Key(x[..^1], this), new Key(x, this));
			}

			internal KeyRange[] InternKeyRanges(ReadOnlySpan<Slice> keys)
			{
				var res = new KeyRange[keys.Length];
				for (int i = 0; i < keys.Length; i++)
				{
					var x = this.Keys.Intern(keys[i], 0);
					res[i] = new (new Key(x[..^1], this), new Key(x, this));
				}
				return res;
			}

			internal Selector InternSelector(in KeySelector selector)
			{
				return new Selector(InternKey(selector.Key), selector.OrEqual, selector.Offset);
			}

			internal Selector InternSelector(in KeySpanSelector selector)
			{
				return new Selector(InternKey(selector.Key), selector.OrEqual, selector.Offset);
			}

			internal Selector[] InternSelectors(ReadOnlySpan<KeySelector> selectors)
			{
				var res = new Selector[selectors.Length];
				for (int i = 0; i < selectors.Length; i++)
				{
					res[i] = InternSelector(in selectors[i]);
				}
				return res;
			}

		}

		[PublicAPI]
		[DebuggerDisplay("{Slice.PrettyPrint(),nq}")]
		public readonly struct Key : IComparable<Key>, IEquatable<Key>, IComparable<Slice>, IEquatable<Slice>, ISpanFormattable
#if NET9_0_OR_GREATER
			, IComparable<ReadOnlySpan<byte>>, IEquatable<ReadOnlySpan<byte>>
#endif
		{
			/// <summary>Key that represents <c>null</c> or <c>not_found</c></summary>
			public static readonly Key Nil;

			/// <summary>Key that is empty</summary>
			public static readonly Key Empty = new (Slice.Empty);

			/// <summary>Contents of the key</summary>
			public readonly Slice Slice;

			/// <summary>Arena used to allocate the key</summary>
			public readonly Arena? Arena;

			public Key(Slice slice, Arena? arena = null)
			{
				this.Slice = slice;
				this.Arena = arena;
			}

			/// <summary>Tests if the key is null</summary>
			public bool IsNull => this.Slice.IsNull;

			/// <summary>Tests if the key is empty</summary>
			public bool IsEmpty => this.Slice.IsEmpty;

			/// <summary>Size (in bytes) of the ky</summary>
			public int Count => this.Slice.Count;

			/// <summary>Exposes the contents of the key as a span of bytes</summary>
			public ReadOnlySpan<byte> Span
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Slice.Span;
			}

			internal Span<byte> UnsafeSpan
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Count != 0 ? this.Slice.Array.AsSpan(this.Slice.Offset, this.Slice.Count) : default;
			}

			/// <summary>Tests if this key belongs to the System Keyspace (0xFF...)</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool IsSystemKey() => this.Slice.Count > 0 && this.Slice[0] == 0xFF;

			/// <summary>Returns a string representation of the key, suitable for logging</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public override string ToString() => this.Slice.ToString("K", null);

			/// <summary>Returns a string representation of the key, suitable for logging</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public string ToString(string? format, IFormatProvider? provider = null) => this.Slice.ToString(format ?? "K", provider);

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Slice.TryFormat(destination, out charsWritten, format.Length > 0 ? format : "K", provider);

			/// <inheritdoc />
			public override bool Equals(object? obj) => obj switch
			{
				Key key      => Equals(key),
				Slice slice  => Equals(slice),
				byte[] bytes => Equals(bytes.AsSlice()),
				_            => false,
			};

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(Slice other) => other.Equals(this.Slice);

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(Key other) => other.Slice.Equals(this.Slice);

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(ReadOnlySpan<byte> other) => this.Span.SequenceEqual(other);

			/// <inheritdoc />
			public override int GetHashCode() => this.Slice.GetHashCode();

			/// <inheritdoc />
			public int CompareTo(ReadOnlySpan<byte> other) => this.Slice.CompareTo(other);

			/// <inheritdoc />
			public int CompareTo(Slice other) => this.Slice.CompareTo(other);

			/// <inheritdoc />
			public int CompareTo(Key other) => this.Slice.CompareTo(other.Slice);

			public sealed class Comparer : IEqualityComparer<Key>, IComparer<Key>
			{

				public static readonly Comparer Default = new();

				private Comparer() { }

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public bool Equals(Key x, Key y) => x.Equals(y);

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public int GetHashCode(Key obj) => obj.GetHashCode();

				public int Compare(Key x, Key y) => x.Slice.CompareTo(y.Slice);

			}

			#region Key vs Key

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator ==(Key left, Key right) => left.Slice.Equals(right.Slice);
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator !=(Key left, Key right) => !left.Slice.Equals(right.Slice);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >(Key left, Key right) => left.Slice.CompareTo(right.Slice) > 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >=(Key left, Key right) => left.Slice.CompareTo(right.Slice) >= 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <(Key left, Key right) => left.Slice.CompareTo(right.Slice) < 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <=(Key left, Key right) => left.Slice.CompareTo(right.Slice) <= 0;

			#endregion

			#region Key vs Slice

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator ==(Key left, Slice right) => left.Slice.Equals(right);
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator !=(Key left, Slice right) => !left.Slice.Equals(right);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >(Key left, Slice right) => left.Slice.CompareTo(right) > 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >=(Key left, Slice right) => left.Slice.CompareTo(right) >= 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <(Key left, Slice right) => left.Slice.CompareTo(right) < 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <=(Key left, Slice right) => left.Slice.CompareTo(right) <= 0;

			#endregion

			/// <summary>Generates the successor of this key</summary>
			/// <param name="target">Arena used to allocate the new key</param>
			/// <returns>Smallest key that is greater than <paramref name="target"/></returns>
			/// <exception cref="InvalidOperationException">If this key is <see cref="Nil"/></exception>
			public Key GetSuccessor(Arena? target = null)
			{
				if (this.IsNull) throw new InvalidOperationException("Nil key does not have any successor!");

				target ??= this.Arena;
				return target?.InternKeyZero(this.Slice) ?? new Key(this.Slice + 0);
			}

			public static Key operator +(Key key, Slice suffix) => new(key.Slice.Concat(suffix));

			public static Key operator +(Key key, ReadOnlySpan<byte> suffix) => new(key.Slice.Concat(suffix));

			public static Key operator +(Key key, byte suffix) => new(key.Slice + suffix);

			/// <summary>Returns the byte at the given offset</summary>
			public byte this[int offset] => this.Slice[offset];

			/// <summary>Returns the byte at the given offset</summary>
			public byte this[Index offset] => this.Slice[offset];

			/// <summary>Returns the bytes in the given range</summary>
			public Slice this[Range range] => this.Slice[range];

			/// <summary>Tests if this key starts with the given byte</summary>
			public bool StartsWith(byte prefix) => this.Slice.StartsWith(prefix);

			/// <summary>Tests if this key starts with the given prefix</summary>
			public bool StartsWith(ReadOnlySpan<byte> prefix) => this.Slice.StartsWith(prefix);

			/// <summary>Tests if this key starts with the given prefix</summary>
			public bool StartsWith(Slice prefix) => this.Slice.StartsWith(prefix);

			/// <summary>Tests if this key starts with the given prefix</summary>
			public bool StartsWith(Key prefix) => this.Slice.StartsWith(prefix.Span);

		}

		[PublicAPI]
		[DebuggerDisplay("{ToString(),nq}")]
		public readonly struct KeyRange
		{
			/// <summary>First key (inclusive) of the range</summary>
			public readonly Key Begin;

			/// <summary>Last key (exclusive) of the range</summary>
			public readonly Key End;

			public KeyRange(Key begin, Key end)
			{
				this.Begin = begin;
				this.End = end;
			}

			/// <summary>Tests if the range is empty</summary>
			public bool IsEmpty() => this.End.Slice <= this.Begin.Slice;

			/// <summary>Returns a string representation of this range, suitable for logging</summary>
			public override string ToString()
			{
				return this.IsEmpty() ? this.Begin.ToString() : (this.Begin.ToString() + " ~ " + this.End.ToString());
			}

			/// <summary>Tests if a key belongs to this range</summary>
			public bool Contains(Key key) => this.Begin.Slice <= key.Slice && key.Slice < End.Slice;

			/// <summary>Tests if a key belongs to this range</summary>
			public bool Contains(ReadOnlySpan<byte> key) => this.Begin.Slice <= key && this.End.Slice > key;

			/// <summary>Tests if a key is strictly before this range</summary>
			public bool IsBefore(Key key) => this.Begin.Slice > key.Slice;

			/// <summary>Tests if a key is strictly after this range</summary>
			public bool IsAfter(Key key) => this.End.Slice <= key.Slice;

			/// <summary>Creates a copy of the range, that is not stored in any arena</summary>
			public KeyRange Copy()
			{
				return new(
					new(this.Begin.Slice.Copy()),
					new(this.End.Slice.Copy())
				);
			}

		}

		[DebuggerDisplay("{ToString(),nq}")]
		[PublicAPI]
		public readonly struct Selector
		{
			public readonly Key Key;
			public readonly bool OrEqual;
			public readonly int Offset;

			public Selector(Key key, bool orEqual, int offset)
			{
				this.Key = key;
				this.OrEqual = orEqual;
				this.Offset = offset;
			}

			public KeySelector ToKeySelector() => new(this.Key.Slice, this.OrEqual, this.Offset);

			public override string ToString() => ToKeySelector().ToString();

		}

		[PublicAPI]
		[DebuggerDisplay("{Slice.PrettyPrint(),nq}")]
		public readonly struct Value : IComparable<Value>, IEquatable<Value>, IComparable<Slice>, IEquatable<Slice>, ISpanFormattable
#if NET9_0_OR_GREATER
			, IComparable<ReadOnlySpan<byte>>, IEquatable<ReadOnlySpan<byte>>
#endif
		{
			public readonly Slice Slice;
			public readonly Arena? Arena;

			public static readonly Value Nil;

			public static readonly Value Empty = new (Slice.Empty, null);

			internal Value(Slice slice, Arena? arena)
			{
				this.Slice = slice;
				this.Arena = arena;
			}

			/// <summary>Wraps caller-owned heap bytes as a value (no arena: interning treats it as already stable)</summary>
			public Value(Slice slice) : this(slice, null) { }

			public bool IsNull => this.Slice.IsNull;

			public bool IsEmpty => this.Slice.IsEmpty;

			public int Count => this.Slice.Count;

			public ReadOnlySpan<byte> Span
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Slice.Span;
			}

			internal Span<byte> UnsafeSpan
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Count != 0 ? this.Slice.Array.AsSpan(this.Slice.Offset, this.Slice.Count) : default;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public override string ToString() => this.Slice.ToString("V", null);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public string ToString(string? format, IFormatProvider? provider = null) => this.Slice.ToString(format ?? "V", provider);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Slice.TryFormat(destination, out charsWritten, format.Length > 0 ? format : "V", provider);

			public override bool Equals(object? obj) => obj switch
			{
				Value value  => Equals(value),
				Slice slice  => Equals(slice),
				byte[] bytes => Equals(bytes.AsSlice()),
				_            => false,
			};

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(Slice other) => other.Equals(this.Slice);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(Value other) => other.Slice.Equals(this.Slice);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(ReadOnlySpan<byte> other) => this.Span.SequenceEqual(other);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public override int GetHashCode() => this.Slice.GetHashCode();

			public int CompareTo(ReadOnlySpan<byte> other) => this.Slice.CompareTo(other);

			public int CompareTo(Slice other) => this.Slice.CompareTo(other);

			public int CompareTo(Value other) => this.Slice.CompareTo(other.Slice);

			public Value Substring(int offset) => new(this.Slice.Substring(offset), this.Arena);

			public Value Substring(int offset, int count) => new(this.Slice.Substring(offset, count), this.Arena);

			public sealed class Comparer : IEqualityComparer<Value>, IComparer<Value>
			{

				public static readonly Comparer Default = new();

				private Comparer() { }

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public bool Equals(Value x, Value y) => x.Equals(y);

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public int GetHashCode(Value obj) => obj.GetHashCode();

				public int Compare(Value x, Value y) => x.Slice.CompareTo(y.Slice);

			}

			#region Value vs Value

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator ==(Value left, Value right) => left.Slice.Equals(right.Slice);
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator !=(Value left, Value right) => !left.Slice.Equals(right.Slice);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >(Value left, Value right) => left.Slice.CompareTo(right.Slice) > 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >=(Value left, Value right) => left.Slice.CompareTo(right.Slice) >= 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <(Value left, Value right) => left.Slice.CompareTo(right.Slice) < 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <=(Value left, Value right) => left.Slice.CompareTo(right.Slice) <= 0;

			#endregion

			#region Value vs Slice

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator ==(Value left, Slice right) => left.Slice.Equals(right);
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator !=(Value left, Slice right) => !left.Slice.Equals(right);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >(Value left, Slice right) => left.Slice.CompareTo(right) > 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator >=(Value left, Slice right) => left.Slice.CompareTo(right) >= 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <(Value left, Slice right) => left.Slice.CompareTo(right) < 0;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool operator <=(Value left, Slice right) => left.Slice.CompareTo(right) <= 0;

			#endregion

			public static explicit operator Key(Value value) => new(value.Slice, value.Arena);

			/// <summary>Returns the byte at the given offset</summary>
			public byte this[int offset] => this.Slice[offset];

			/// <summary>Returns the byte at the given offset</summary>
			public byte this[Index offset] => this.Slice[offset];

			/// <summary>Returns the bytes in the given range</summary>
			public Slice this[Range range] => this.Slice[range];

			/// <summary>Tests if this key starts with the given byte</summary>
			public bool StartsWith(byte prefix) => this.Slice.StartsWith(prefix);

			/// <summary>Tests if this key starts with the given prefix</summary>
			public bool StartsWith(ReadOnlySpan<byte> prefix) => this.Slice.StartsWith(prefix);

			/// <summary>Tests if this key starts with the given prefix</summary>
			public bool StartsWith(Slice prefix) => this.Slice.StartsWith(prefix);

			/// <summary>Tests if this key starts with the given prefix</summary>
			public bool StartsWith(Key prefix) => this.Slice.StartsWith(prefix.Span);

			/// <summary>Tests if this key ends with the given byte</summary>
			public bool EndsWith(byte prefix) => this.Slice.EndsWith(prefix);

			/// <summary>Tests if this key ends with the given prefix</summary>
			public bool EndsWith(ReadOnlySpan<byte> prefix) => this.Slice.EndsWith(prefix);

			/// <summary>Tests if this key ends with the given prefix</summary>
			public bool EndsWith(Slice prefix) => this.Slice.EndsWith(prefix);

			/// <summary>Tests if this key ends with the given prefix</summary>
			public bool EndsWith(Key prefix) => this.Slice.EndsWith(prefix.Span);

		}

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

				var key = iter.Current.Key;
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

				curBefore = itBefore.Current;
				curAfter = itAfter.Current;

				while (true)
				{
					switch (curBefore.Key.CompareTo(curAfter.Key))
					{
						case < 0:
						{ // key removed
							yield return (curBefore.Key, curBefore.Value, Value.Nil);
							if (!itBefore.Next(out curBefore))
							{
								goto before_is_done;
							}
							break;
						}
						case > 0:
						{ // key added
							yield return (curAfter.Key, Value.Nil, curAfter.Value);
							if (!itAfter.Next(out curAfter))
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
							switch((itBefore.Next(out curBefore), itAfter.Next(out curAfter)))
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
				while (itAfter.Next(out curAfter));

				goto all_done;

			after_is_done:
				do
				{
					yield return (curBefore.Key, curBefore.Value, Value.Nil);
				}
				while (itBefore.Next(out curBefore));

				goto all_done;

			all_done:
				yield break;
			}

		}

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
				var resolved = ResolveMerged(selector, accessSystemKeys);

				if (!snapshotRead)
				{
					MarkResolveReadConflict(selector, resolved, merged: true);
				}
				return resolved;
			}

			/// <summary>Resolves a key selector against the merged view (committed snapshot + local uncommitted mutations), without recording any conflict range.</summary>
			/// <remarks>This is the ONLY correct selector semantics over pending writes: the onion iterator's per-layer seek is exact just for the 0/+1 offsets the internal pager uses, so range-read bounds resolve here too.</remarks>
			private Key ResolveMerged(Selector selector, bool accessSystemKeys)
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
				return merged[(int) target];
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
					MarkMergedRangeReadConflict(from, to, atomicsAreLocal: true);
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

			/// <summary>Marks the read conflict of a merged-view range read: the given extent MINUS the segments fully determined by the transaction's own writes (sets and clears; an atomic chain still depends on the database, so its segment stays).</summary>
			/// <remarks>Oracle-pinned contract: the real client subtracts locally-written segments at segment granularity (a peer write under an own set or clear never conflicts), the same exemption the point-read path applies.</remarks>
			/// <param name="atomicsAreLocal">Selector resolution returns keys, not values: an own atomic's presence is locally determined, so its segment is subtracted like a set or clear (oracle-pinned; revisit for visibility-conditional operations like CompareAndClear when the atomics fuzz family runs).</param>
			private void MarkMergedRangeReadConflict(Key fromInclusive, Key toExclusive, bool atomicsAreLocal = false)
			{
				var cursor = fromInclusive;
				foreach (var entry in this.Mutations.IterateOrdered())
				{
					if (entry.Begin >= toExclusive) break;
					if (entry.End <= cursor) continue;
					var mutation = entry.Value;
					if (!mutation.IsKv() && !mutation.IsRange())
					{
						// an UNANCHORED atomic chain reads through to the committed value: its segment stays in the
						// conflict (unless the read returns no values, see atomicsAreLocal); a chain anchored on an
						// own Set or Clear is fully determined locally and is subtracted like the anchor itself
						bool anchored = mutation.Op is Operation.Set or Operation.Clear;
						if (!anchored && !atomicsAreLocal)
						{
							continue;
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

			/// <summary>Checks whether a key is present in the merged view, evaluating atomic chains purely (no coalescing write-back, no conflict marking).</summary>
			private bool IsVisibleInMergedView(Key key)
			{
				var mutation = FindCoveringMutation(key);
				if (mutation != null)
				{
					if (mutation.IsKv()) return !mutation.Parameter.IsNull; // a single-key Clear is IsKv() with a Nil parameter
					if (mutation.IsRange()) return false;
					if (mutation.IsAtomic())
					{
						// evaluate the chain over the committed value; the result decides visibility (e.g. CompareAndClear can erase the key)
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
					var endKey = ResolveMerged(endExclusive, accessSystemKeys);
					if (endKey.IsNull)
					{ // the end selector resolves past the last visible key: the merged scan is only bounded by the system space
						endKey = SpecialKeys.SystemPrefix;
					}
					var beginKey = ResolveMerged(beginInclusive, accessSystemKeys);
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

						switch (mutation.Op)
						{
							case Operation.VersionStampedKey:
							{
								//REVIEW: only for API version >= 520

								// offset in last 32 bits
								int len = kv.Key.Count - 4;
								if (len < 0) throw new InvalidOperationException("TODO: malformed offset in VersionStampedKey");
								int offset = kv.Key.Slice.Substring(len).ToInt32();

								var tmp = arena.AllocateKey(len);
								kv.Key.Span[..^4].CopyTo(tmp.UnsafeSpan);
								stamp.WriteTo(tmp.UnsafeSpan.Slice(offset));

								newData[tmp] = arena.InternValue(mutation.Parameter);
								break;
							}
				
							case Operation.VersionStampedValue:
							{
								//REVIEW: only for API version >= 520

								// offset in last 32 bits
								int len = mutation.Parameter.Count - 4;
								if (len < 0) throw new InvalidOperationException("TODO: malformed offset in VersionStampedValue");
								int offset = mutation.Parameter.Slice.Substring(len).ToInt32();

								var tmp = arena.AllocateValue(len);
								mutation.Parameter.Span[..^4].CopyTo(tmp.UnsafeSpan);
								stamp.WriteTo(tmp.UnsafeSpan.Slice(offset));

								// HACKHACK:
								newData[kv.Key] = tmp;
								break;
							}

							default:
							{
								var value = kv.Value;
								do
								{
									value = CoalesceAtomic(arena, value, mutation);
									mutation = mutation.Next;
								}
								while (mutation != null);

								if (value.IsNull)
								{
									newData.Remove(kv.Key);
								}
								else
								{
									// the coalesced value can be a passthrough of a chain anchor's Parameter (backed by
									// the transaction's recyclable arena): intern it before it becomes committed state
									newData[kv.Key] = arena.InternValue(value);
								}

								break;
							}
						}
					}
					else
					{
						throw new NotSupportedException();
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

		}

		public static class SpecialKeys
		{
			public static readonly Key SystemPrefix = new Key(Slice.FromByte(0xFF));
			public static readonly Key SystemRoot = new Key(Slice.FromByteString("\xFF/"));
			public static readonly Key SystemMetadataVersion = new Key(Slice.FromByteString("\xFF/metadataVersion"));
			public static readonly Key SystemEnd = new Key(Slice.FromByteString("\xFF\xFF"));

			public static readonly Key DirectoryLayerPrefix = new Key(Slice.FromByte(0xFE));
			public static readonly Key DirectoryLayerEnd = new Key(Slice.FromByteString("\xFE\xFF"));
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

		private static ArrayPool<byte> GlobalPool { get; } = ArrayPool<byte>.Create();

		/// <summary>Time source of the simulated cluster, used to schedule the retry backoff (see <see cref="RetryDelayMaximum"/>)</summary>
		/// <remarks>A test that installs a fake provider (e.g. <c>NodaTimeProvider</c> over a <c>FakeTimeProvider</c>) gets a
		/// simulated cluster whose retry timing advances with virtual time, instead of blocking the wall clock.</remarks>
		internal TimeProvider Time { get; }

		/// <summary>Base delay before the first retry after a retryable error (when <see cref="RetryDelayMaximum"/> enables the backoff); defaults to 1 ms</summary>
		public TimeSpan RetryDelayInitial { get; set; } = TimeSpan.FromMilliseconds(1);

		/// <summary>Cap on the exponential retry backoff. <b>Defaults to zero, which disables the wait entirely</b> - a retryable
		/// error retries immediately, so normal tests run at full speed. Raise it (e.g. 1 s) to emulate realistic recovery
		/// timing in a "broken cluster" test; the delay rides <see cref="Time"/>, so under a fake clock it costs zero real time.</summary>
		/// <remarks>The per-transaction <c>MaxRetryDelay</c> option, when set, only tightens this cap (it never enables the backoff).</remarks>
		public TimeSpan RetryDelayMaximum { get; set; } = TimeSpan.Zero;

		public FakeDbStore(int apiVersion = DEFAULT_API_VERSION, int protocolVersion = MAX_API_VERSION, long initialVersion = 0, TimeProvider? time = null)
			: this(apiVersion, protocolVersion, time)
		{
			if (initialVersion <= 0)
			{
				initialVersion = 0xfdb1337000000;
			}
			var initialStamp = MakeVersionStamp(initialVersion, 0);

			var arena = new Arena(128 * 1024, 512 * 1024, GlobalPool);

			var data = new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default);
			data[SpecialKeys.SystemRoot] = arena.InternValue(SystemRootSentinelValue);
			data[SpecialKeys.SystemMetadataVersion] = arena.InternValue(initialStamp.ToSlice());
			data[SpecialKeys.SystemEnd] = Value.Empty;

			var conflicts = new ColaRangeDictionary<Key, long>(Key.Comparer.Default);

			var snapshot = new Snapshot(
				initialVersion,
				new ColaCommittedStore(data),
				conflicts,
				initialStamp,
				arena
			);

			InitializeSnapshot(snapshot);
		}

		/// <summary>Value seeded under <see cref="SpecialKeys.SystemRoot"/> in every fresh store, whichever backend</summary>
		protected static readonly Slice SystemRootSentinelValue = Slice.FromString("You shall not pass!");

		/// <summary>Shared initialization for backend subclasses: the derived constructor MUST call <see cref="InitializeSnapshot"/> (with a snapshot seeding the same system keys a fresh in-memory store gets) before the store is used.</summary>
		protected FakeDbStore(int apiVersion, int protocolVersion, TimeProvider? time)
		{
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
			this.CurrentSnapshotUnsafe = null!;
		}

		/// <summary>Installs the store's initial committed snapshot (once, from the constructor path).</summary>
		protected void InitializeSnapshot(Snapshot snapshot)
		{
			Contract.NotNull(snapshot);
			this.Snapshots[0] = snapshot;
			this.CurrentSnapshotUnsafe = snapshot;
			this.ReadVersion = snapshot.Version;
		}

		[Conditional("FULL_DEBUG")]
		private static void Kenobi(string msg)
		{
#if FULL_DEBUG
			System.Diagnostics.Debug.WriteLine(msg);
			Console.WriteLine(msg);
#endif
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

		public virtual void Dispose()
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

		protected internal virtual Task<ReadYourWritesSnapshot> StartNewSnapshot(Arena arena, CancellationToken ct)
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
				return Task.FromResult(new ReadYourWritesSnapshot(snapshot, arena));
			}
		}

		protected internal virtual Task<ReadYourWritesSnapshot> StartSnapshotAtVersion(Arena arena, long version, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<ReadYourWritesSnapshot>(ct);
			using (this.GlobalLock.GetReadLock())
			{
				if (ct.IsCancellationRequested)
				{
					return Task.FromCanceled<ReadYourWritesSnapshot>(ct);
				}
				return Task.FromResult(new ReadYourWritesSnapshot(this.Snapshots[version], arena));
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
				Contract.Debug.Assert(snapshot != null && snapshot.Version <= rv);

				using (this.GlobalLock.GetWriteLock())
				{
					ct.ThrowIfCancellationRequested();

					// keys for watches that have completely triggered in this commit, and should be removed from the active list
					List<Slice>? deadWatchedKeys = null;

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
									// queue the watch for triggering
									(watchesToTrigger ??= [ ]).Add(w);
									continue;
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

				if (watchesToTrigger is not null)
				{
					foreach (var watch in watchesToTrigger)
					{
						watch.Trigger();
					}
				}

				return commitVersion;
			}

		}

		/// <summary>Backend hook, called under the global write lock: makes a committed snapshot durable, applies the backend's retention policy, and publishes it as the current state; returns the instance the rest of the commit works with.</summary>
		/// <remarks>The in-memory backend keeps every version forever (the test-mode movie) and returns the snapshot unchanged; a persistent backend flushes its generation first, may return a frozen re-wrap, and trims its retained window.</remarks>
		protected virtual Snapshot PublishSnapshot(Snapshot updated, long commitVersion)
		{
			this.CurrentSnapshotUnsafe = updated;
			this.Snapshots[commitVersion] = updated;
			this.ReadVersion = commitVersion;
			return updated;
		}

		/// <summary>Backend hook: a transaction is done with its resolved snapshot (dispose or reset). The in-memory backend does not care; a persistent backend releases the read pin that held the snapshot's generation.</summary>
		protected internal virtual void OnTransactionEnd(ReadYourWritesSnapshot snapshot)
		{
		}

		/// <summary>Backend helper: the committed store under a snapshot (the seam surface a backend implements).</summary>
		protected static IFdbCommittedStore GetSnapshotStore(Snapshot snapshot) => snapshot.Data;

		/// <summary>Backend helper: re-wraps a snapshot around a replacement committed store (a persistent backend freezes its writable store into a readable one at publish).</summary>
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

		public virtual IFdbTransactionHandler CreateTransaction(FdbOperationContext context)
		{
			// closes the FL-15 generic boundary for this backend: the whole handler monomorphizes over the ColaStore cursor
			return new TransactionHandler<ColaCommittedCursor>(this, context);
		}

		#endregion

		public class TransactionHandler<TCursor> : IFdbTransactionHandler
			where TCursor : struct, IFdbCommittedCursor
		{

			private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create();

#if NET9_0_OR_GREATER
			private readonly System.Threading.Lock Lock = new();
#else
			private readonly object Lock = new();
#endif

			public TransactionHandler(FakeDbStore store, FdbOperationContext context)
			{
				this.Store = store;
				this.Context = context;
				this.Scratch = new Arena(16 * 1024, 128 * 1024, BufferPool);
			}

			public FakeDbStore Store { get; }

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

			public ReadYourWritesSnapshot GetSnapshotBlocking()
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
				//TODO: PERF: OPTIMIZE: implement natively instead of first allocated to KV<Slice, Slice> and then visiting!
				//TODO: REVIEW: maybe move this into ReadYourWritesSnapshot itself?

				FdbRangeChunk chunk;
				lock (this.Lock)
				{
					chunk = s.GetRange<TCursor>(beginInclusive, endExclusive, options, iteration, GetEffectiveRyw(isSnapshotRead), isSnapshotRead, this.OptionReadSystemKeys);
				}

				var items = chunk.Items;
				for (int i = 0; i < items.Length; i++)
				{
					ct.ThrowIfCancellationRequested();
					visitor(state, items[i].Key.Span, items[i].Value.Span);
				}

				//TODO: if chunk is pooled, we need to return the buffer to the pool, but we need to keep "First" and "Last" around?

				return new(items.Length, chunk.HasMore, chunk.Iteration, chunk.Options, chunk.First, chunk.Last, chunk.TotalBytes);
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

			public Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected, bool snapshot, CancellationToken ct)
			{
				var k = this.Scratch.InternKeyRange(key);
				return Deferred(this, GetSnapshot(ct), k, expected, snapshot, ct);

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

			/// <summary>Fails every watch this transaction created but never armed (its commit did not succeed), like the real client does.</summary>
			private void FailPendingWatches(FdbError error)
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
					node.Future.TrySetException(new FdbException(error));
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
					this.StampSignal?.TrySetException(new FdbException(FdbError.TransactionInvalidVersion));
					FailPendingWatches(e.Code);
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
						// "broken cluster" test raises FakeDbStore.RetryDelayMaximum to emulate recovery timing.
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
				throw new FdbException(code);
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
					Kenobi($"*** #{this.Id} inner: {this.Inner.Current}");
					this.InnerState = STATE_AVAILABLE;
					for (int i = 0; i < selector.Offset; i++)
					{
						if (!this.Inner.Next())
						{
							this.InnerState = STATE_DEAD;
							break;
						}
						Kenobi($"*** #{this.Id} inner + {i+1}: {this.Inner.Current}");
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
						Kenobi($"*** #{this.Id} inner (from first): {this.Inner.Current}");
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

				Kenobi($"*** #{this.Id} seek initial of {selector}: inner={this.InnerState}:{this.Inner.Current}, outer={this.OuterState}:{this.Outer.Current}");
				// "compute" the current
				Next();
				Kenobi($"*** #{this.Id} seek result of {selector}: inner={this.InnerState}:{this.Inner.Current}, outer={this.OuterState}:{this.Outer.Current} : {this.Current}");

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
					Kenobi($"**** #{this.Id} next({this.InnerState}:{this.Inner.Current.Key:K}, {this.OuterState}:{outer.Current})");

					// advance one or the other
					if (this.InnerState == STATE_UNKNOWN)
					{ // get next from inner
						this.InnerState = this.Inner.Next() ? STATE_AVAILABLE : STATE_DEAD;
						Kenobi($"**** #{this.Id} fetched inner => {this.InnerState}:{this.Inner.Current}");
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
						this.Current = this.Inner.Current;
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

					var innerCurrent = this.Inner.Current;
					int cmp = innerCurrent.Key.CompareTo(outerEntry.Begin);
					Kenobi($"** #{this.Id} ({cmp}) [{innerCurrent.Key:V} = {innerCurrent.Value:V}] vs {mutation.Op} [{outerEntry.Begin:K} ~ {outerEntry.End:K} = {mutation.Parameter:V}]");

					switch (cmp)
					{
						case < 0:
						{ // pass through inner
							this.Current = innerCurrent;
							this.InnerState = STATE_UNKNOWN;
							Kenobi($"*** #{this.Id} use inner, advance inner: {innerCurrent.Key:K} = {innerCurrent.Value:V}");
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
									Kenobi($"*** #{this.Id} skip cleared, advance both: {innerCurrent.Key:K}");
									continue;
								}

								// consume both
								this.Current = new(innerCurrent.Key, mutation.GetEffectiveValue());
								this.InnerState = STATE_UNKNOWN;
								this.OuterState = STATE_UNKNOWN;
								Kenobi($"*** #{this.Id} combine, advance both: {innerCurrent.Key:K} = {this.Current.Value:V}");
								return true;
							}

							if (mutation.IsRange())
							{ // the committed key is the first key of a cleared range: it is masked; keep the range (it may mask more keys)
								this.InnerState = STATE_UNKNOWN;
								Kenobi($"*** #{this.Id} inner masked by clear range, advance inner: {innerCurrent.Key:K}");
								continue;
							}

							if (mutation.IsAtomic())
							{ // coalesce the whole chain over the committed value, transiently (see above)
								var value = innerCurrent.Value;
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
								if (outerEntry.End <= innerCurrent.Key)
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

		/// <summary>Creates a standalone <see cref="Snapshot"/> that contains a set of initial key/value pairs.</summary>
		public static Snapshot CreateSnapshotFrom(IEnumerable<KeyValuePair<Slice, Slice>> items)
		{

			var initialVersion = 0xfdb1337000000;
			var initialStamp = MakeVersionStamp(initialVersion, 0);

			var arena = new Arena(128 * 1024, 512 * 1024, GlobalPool);

			var data = new ColaOrderedDictionary<Key, Value>(Key.Comparer.Default, Value.Comparer.Default);
			data[SpecialKeys.SystemRoot] = arena.InternValue(Slice.FromString("You shall not pass!"));
			data[SpecialKeys.SystemMetadataVersion] = arena.InternValue(initialStamp.ToSlice());
			data[SpecialKeys.SystemEnd] = Value.Empty;

			var conflicts = new ColaRangeDictionary<Key, long>(Key.Comparer.Default);

			foreach (var kv in items)
			{
				data.Add(arena.InternKey(kv.Key), arena.InternValue(kv.Value));
			}

			var snapshot = new Snapshot(
				initialVersion,
				new ColaCommittedStore(data),
				conflicts,
				initialStamp,
				arena
			);

			return snapshot;
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
	/// <remarks>CAUTION: this exposes internal structure that is not guaranteed to be thread-safe, and could cause unexpected behavior or deadlocks!</remarks>
	public static class FakeDbDebugger
	{

		public static ColaOrderedDictionary<FakeDbStore.Key, FakeDbStore.Value> GetSnapshotData(FakeDbStore.Snapshot snapshot) => ((ColaCommittedStore) snapshot.Data).Inner;

		public static ColaRangeDictionary<FakeDbStore.Key, long> GetSnapshotConflictRanges(FakeDbStore.Snapshot snapshot) => snapshot.Conflicts;

		public static ColaRangeDictionary<Key, Mutation> GetSnapshotMutations(FakeDbStore.ReadYourWritesSnapshot snapshot) => snapshot.Mutations;

		public static ColaRangeSet<Key> GetSnapshotReadConflicts(FakeDbStore.ReadYourWritesSnapshot snapshot) => snapshot.ReadConflicts;

		public static ColaRangeSet<Key> GetSnapshotWriteConflicts(FakeDbStore.ReadYourWritesSnapshot snapshot) => snapshot.WriteConflicts;

	}

}
