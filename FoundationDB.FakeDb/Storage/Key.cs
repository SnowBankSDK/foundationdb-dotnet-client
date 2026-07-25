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

	public static class SpecialKeys
	{
		public static readonly Key SystemPrefix = new Key(Slice.FromByte(0xFF));
		public static readonly Key SystemRoot = new Key(Slice.FromByteString("\xFF/"));
		public static readonly Key SystemMetadataVersion = new Key(Slice.FromByteString("\xFF/metadataVersion"));
		public static readonly Key SystemEnd = new Key(Slice.FromByteString("\xFF\xFF"));

		public static readonly Key DirectoryLayerPrefix = new Key(Slice.FromByte(0xFE));
		public static readonly Key DirectoryLayerEnd = new Key(Slice.FromByteString("\xFE\xFF"));
	}
}
