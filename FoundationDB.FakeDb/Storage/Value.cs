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
}
