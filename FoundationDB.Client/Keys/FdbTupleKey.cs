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

namespace FoundationDB.Client
{
	using System.ComponentModel;
	using System.Numerics;

	/// <summary>Key that is composed of a tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey : IFdbKey
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit]
		public FdbTupleKey(IKeySubspace? subspace, IVarTuple items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		public readonly IKeySubspace? Subspace;

		public readonly IVarTuple Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbRawKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.Compare(in this, in other);

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Append(...)

		//REVIEW: should these be renamed to Key(...) ?

		/// <summary>Appends an element to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1>(T1 item1) => new(this.Subspace, this.Items.Append(item1));

		/// <summary>Appends two elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2>(T1 item1, T2 item2) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2)));

		/// <summary>Appends three elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2, item3)));

		/// <summary>Appends four elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2, item3, item4)));

		/// <summary>Appends five elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2, item3, item4, item5)));

		/// <summary>Appends six elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2, item3, item4, item5, item6)));

		/// <summary>Appends seven elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2, item3, item4, item5, item6, item7)));

		/// <summary>Appends eight elements to the key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey Append<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Concat(this.Items, STuple.Create(item1, item2, item3, item4, item5, item6, item7, item8)));

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
			=> TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, this.Items);

		#endregion

	}

	/// <summary>Key that is composed of a 1-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple item</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1> : IFdbKey
		, IEquatable<FdbTupleKey<T1>>, IComparable<FdbTupleKey<T1>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1>, FdbTupleKey<T1>, bool>
		, IComparisonOperators<FdbTupleKey<T1>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1> items)
		{
			this.Subspace = subspace;
			this.Item1 = items.Item1;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1)
		{
			this.Subspace = subspace;
			this.Item1 = item1;
		}

		public readonly IKeySubspace? Subspace;

		public readonly T1 Item1;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleKey<T1> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1>))
			{
				return Equals((FdbTupleKey<T1>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && EqualityComparer<T1>.Default.Equals(this.Item1, other.Item1);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && STuple.Create(this.Item1).Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		public int CompareTo(FdbTupleKey<T1> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = Comparer<T1>.Default.Compare(this.Item1, other.Item1);
			return cmp;
		}

		/// <inheritdoc />
		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = STuple.Create(this.Item1).CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1>))
			{
				return CompareTo((FdbTupleKey<T1>) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1> left, FdbTupleKey<T1> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1> left, FdbTupleKey<T1> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1> left, FdbTupleKey<T1> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1> left, FdbTupleKey<T1> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1> left, FdbTupleKey<T1> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1> left, FdbTupleKey<T1> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2> Key<T2>(T2 item2) => new(this.Subspace, this.Item1, item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Key<T2, T3>(T2 item2, T3 item3) => new(this.Subspace, STuple.Create(this.Item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Key<T2, T3, T4>(T2 item2, T3 item3, T4 item4) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Key<T2, T3, T4, T5>(T2 item2, T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Key<T2, T3, T4, T5, T6>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T2, T3, T4, T5, T6, T7>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T2, T3, T4, T5, T6, T7, T8>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7, item8));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2> Tuple<T2>(in STuple<T2> items) => new(this.Subspace, this.Item1, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Tuple<T2, T3>(in STuple<T2, T3> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Tuple<T2, T3, T4>(in STuple<T2, T3, T4> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T2, T3, T4, T5>(in STuple<T2, T3, T4, T5> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T2, T3, T4, T5, T6>(in STuple<T2, T3, T4, T5, T6> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T2, T3, T4, T5, T6, T7>(in STuple<T2, T3, T4, T5, T6, T7> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T2, T3, T4, T5, T6, T7, T8>(in STuple<T2, T3, T4, T5, T6, T7, T8> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2> Tuple<T2>(in ValueTuple<T2> items) => new(this.Subspace, this.Item1, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Tuple<T2, T3>(in ValueTuple<T2, T3> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Tuple<T2, T3, T4>(in ValueTuple<T2, T3, T4> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T2, T3, T4, T5>(in ValueTuple<T2, T3, T4, T5> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T2, T3, T4, T5, T6>(in ValueTuple<T2, T3, T4, T5, T6> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T2, T3, T4, T5, T6, T7>(in ValueTuple<T2, T3, T4, T5, T6, T7> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T2, T3, T4, T5, T6, T7, T8>(in ValueTuple<T2, T3, T4, T5, T6, T7, T8> items) => new(this.Subspace, STuple.Create(this.Item1, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7));

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{STuple.Create(this.Item1)}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{STuple.Create(this.Item1)}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={STuple.Create(this.Item1)})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{STuple.Create(this.Item1)}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{STuple.Create(this.Item1)}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={STuple.Create(this.Item1)})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint<T1>(this.Item1, embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked((this.Subspace?.GetPrefix().Count ?? 0) + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryEncodeKey(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, this.Item1);

		#endregion

	}

	/// <summary>Key that is composed of a 2-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2>>, IComparable<FdbTupleKey<T1, T2>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2>, FdbTupleKey<T1, T2>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleKey<T1, T2> tuple => this.Equals(tuple),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2>))
			{
				return Equals((FdbTupleKey<T1, T2>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2> other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2>))
			{
				return CompareTo((FdbTupleKey<T1, T2>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2> left, FdbTupleKey<T1, T2> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2> left, FdbTupleKey<T1, T2> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2> left, FdbTupleKey<T1, T2> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2> left, FdbTupleKey<T1, T2> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2> left, FdbTupleKey<T1, T2> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2> left, FdbTupleKey<T1, T2> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Key<T3>(T3 item3) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Key<T3, T4>(T3 item3, T4 item4) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Key<T3, T4, T5>(T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Key<T3, T4, T5, T6>(T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T3, T4, T5, T6, T7>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T3, T4, T5, T6, T7, T8>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7, item8));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Tuple<T3>(in STuple<T3> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Tuple<T3, T4>(in STuple<T3, T4> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T3, T4, T5>(in STuple<T3, T4, T5> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T3, T4, T5, T6>(in STuple<T3, T4, T5, T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3, items.Item4);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T3, T4, T5, T6, T7>(in STuple<T3, T4, T5, T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T3, T4, T5, T6, T7, T8>(in STuple<T3, T4, T5, T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6);

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Tuple<T3>(in ValueTuple<T3> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Tuple<T3, T4>(in ValueTuple<T3, T4> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T3, T4, T5>(in ValueTuple<T3, T4, T5> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T3, T4, T5, T6>(in ValueTuple<T3, T4, T5, T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3, items.Item4);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T3, T4, T5, T6, T7>(in ValueTuple<T3, T4, T5, T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T3, T4, T5, T6, T7, T8>(in ValueTuple<T3, T4, T5, T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion
	}

	/// <summary>Key that is composed of a 3-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3>>, IComparable<FdbTupleKey<T1, T2, T3>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3>, FdbTupleKey<T1, T2, T3>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2, T3> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2, T3 item3)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2, T3> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleKey<T1, T2, T3> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2, T3> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3>))
			{
				return Equals((FdbTupleKey<T1, T2, T3>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2, T3> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2, T3> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3>))
			{
				return CompareTo((FdbTupleKey<T1, T2, T3>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3> left, FdbTupleKey<T1, T2, T3> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3> left, FdbTupleKey<T1, T2, T3> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3> left, FdbTupleKey<T1, T2, T3> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3> left, FdbTupleKey<T1, T2, T3> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3> left, FdbTupleKey<T1, T2, T3> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3> left, FdbTupleKey<T1, T2, T3> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Key<T4>(T4 item4) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Key<T4, T5>(T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Key<T4, T5, T6>(T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T4, T5, T6, T7>(T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T4, T5, T6, T7, T8>(T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7, item8));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Tuple<T4>(in STuple<T4> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T4, T5>(in STuple<T4, T5> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T4, T5, T6>(in STuple<T4, T5, T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T4, T5, T6, T7>(in STuple<T4, T5, T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2, items.Item3, items.Item4);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T4, T5, T6, T7, T8>(in STuple<T4, T5, T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5);

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Tuple<T4>(in ValueTuple<T4> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T4, T5>(in ValueTuple<T4, T5> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T4, T5, T6>(in ValueTuple<T4, T5, T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T4, T5, T6, T7>(in ValueTuple<T4, T5, T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2, items.Item3, items.Item4);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T4, T5, T6, T7, T8>(in ValueTuple<T4, T5, T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion

	}

	/// <summary>Key that is composed of a 4-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4>>, IComparable<FdbTupleKey<T1, T2, T3, T4>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4>, FdbTupleKey<T1, T2, T3, T4>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2, T3, T4> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2, T3, T4> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			FdbTupleKey<T1, T2, T3, T4> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			Slice bytes => this.Equals(bytes),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2, T3, T4> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4>))
			{
				return Equals((FdbTupleKey<T1, T2, T3, T4>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2, T3, T4> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2, T3, T4> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4>))
			{
				return CompareTo((FdbTupleKey<T1, T2, T3, T4>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey<T1, T2, T3, T4> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey<T1, T2, T3, T4> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey<T1, T2, T3, T4> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey<T1, T2, T3, T4> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey<T1, T2, T3, T4> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey<T1, T2, T3, T4> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Key<T5>(T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Key<T5, T6>(T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T5, T6, T7>(T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T5, T6, T7, T8>(T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7, item8));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T5>(in STuple<T5> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T5, T6>(in STuple<T5, T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T5, T6, T7>(in STuple<T5, T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T5, T6, T7, T8>(in STuple<T5, T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1, items.Item2, items.Item3, items.Item4);

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T5>(in ValueTuple<T5> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T5, T6>(in ValueTuple<T5, T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T5, T6, T7>(in ValueTuple<T5, T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T5, T6, T7, T8>(in ValueTuple<T5, T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, items.Item1, items.Item2, items.Item3, items.Item4);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion

	}

	/// <summary>Key that is composed of a 5-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5>>, IComparable<FdbTupleKey<T1, T2, T3, T4, T5>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5>, FdbTupleKey<T1, T2, T3, T4, T5>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2, T3, T4, T5> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2, T3, T4, T5> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			FdbTupleKey<T1, T2, T3, T4, T5> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			Slice bytes => this.Equals(bytes),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2, T3, T4, T5> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5>))
			{
				return Equals((FdbTupleKey<T1, T2, T3, T4, T5>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2, T3, T4, T5> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2, T3, T4, T5> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5>))
			{
				return CompareTo((FdbTupleKey<T1, T2, T3, T4, T5>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey<T1, T2, T3, T4, T5> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey<T1, T2, T3, T4, T5> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey<T1, T2, T3, T4, T5> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey<T1, T2, T3, T4, T5> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey<T1, T2, T3, T4, T5> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey<T1, T2, T3, T4, T5> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Key<T6>(T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T6, T7>(T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T6, T7, T8>(T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7, item8));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4, T5>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T6>(in STuple<T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T6, T7>(in STuple<T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T6, T7, T8>(in STuple<T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, items.Item1, items.Item2, items.Item3);

		#endregion

		#region Tuple(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T6>(in ValueTuple<T6> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T6, T7>(in ValueTuple<T6, T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T6, T7, T8>(in ValueTuple<T6, T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, items.Item1, items.Item2, items.Item3);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion

	}

	/// <summary>Key that is composed of a 6-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5, T6>>, IComparable<FdbTupleKey<T1, T2, T3, T4, T5, T6>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6>, FdbTupleKey<T1, T2, T3, T4, T5, T6>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			FdbTupleKey<T1, T2, T3, T4, T5, T6> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			Slice bytes => this.Equals(bytes),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2, T3, T4, T5, T6> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5, T6>))
			{
				return Equals((FdbTupleKey<T1, T2, T3, T4, T5, T6>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5, T6> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2, T3, T4, T5, T6> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2, T3, T4, T5, T6> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5, T6>))
			{
				return CompareTo((FdbTupleKey<T1, T2, T3, T4, T5, T6>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey<T1, T2, T3, T4, T5, T6> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey<T1, T2, T3, T4, T5, T6> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey<T1, T2, T3, T4, T5, T6> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey<T1, T2, T3, T4, T5, T6> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey<T1, T2, T3, T4, T5, T6> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey<T1, T2, T3, T4, T5, T6> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T7>(T7 item7) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T7, T8>(T7 item7, T8 item8) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7, item8);

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4, T5, T6>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T7>(in STuple<T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T7, T8>(in STuple<T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, items.Item1, items.Item2);

		#endregion

		#region Tuple(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T7>(in ValueTuple<T7> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T7, T8>(in ValueTuple<T7, T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, items.Item1, items.Item2);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion

	}

	/// <summary>Key that is composed of a 7-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>>, IComparable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>))
			{
				return Equals((FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>))
			{
				return CompareTo((FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T8>(T8 item8) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, this.Items.Item7, item8);

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T8>(in STuple<T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, this.Items.Item7, items.Item1);

		#endregion

		#region Tuple(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T8>(in ValueTuple<T8> items) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, this.Items.Item7, items.Item1);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion

	}

	/// <summary>Key that is composed of an 8-tuple inside a subspace</summary>
	/// <remarks>Encoded as the subspace's prefix, following by the packed tuple items</remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>>, IComparable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>>
		, IEquatable<FdbTupleKey>, IComparable<FdbTupleKey>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, FdbTupleKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, FdbRawKey, bool>
		, IComparisonOperators<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, Slice, bool>
		, IComparable
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace? subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7, item8);
		}

		public readonly IKeySubspace? Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7, T8> Items;

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			Slice bytes => this.Equals(bytes),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> key => this.Equals(key),
			FdbTupleKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>))
			{
				return Equals((FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return Equals((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey other) => FdbKeyHelpers.AreEqual(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> key => CompareTo(key),
			FdbTupleKey key => CompareTo(key),
			FdbRawKey key => CompareTo(key.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		public int CompareTo(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> other)
		{
			int cmp = FdbKeyHelpers.Compare(Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		public int CompareTo(FdbTupleKey other)
		{
			int cmp = FdbKeyHelpers.Compare(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Items.CompareTo(other.Items);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.Compare(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.Compare(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Compare(in this, other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>))
			{
				return CompareTo((FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbTupleKey))
			{
				return CompareTo((FdbTupleKey) (object) other);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbTupleKey right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, Slice right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, Slice right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, Slice right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, Slice right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, Slice right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, Slice right) => left.CompareTo(right) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbRawKey right) => left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbRawKey right) => !left.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbRawKey right) => left.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbRawKey right) => left.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbRawKey right) => left.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> left, FdbRawKey right) => left.CompareTo(right.Data) >= 0;

		#endregion

		#endregion

		#region Key(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, STuple<T9>> Key<T9>(T9 item9) => new(this, new(item9));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, STuple<T9, T10>> Key<T9, T10>(T9 item9, T10 item10) => new(this, new(item9, item10));

		#endregion

		#region Tuple(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>, TTuple> Tuple<TTuple>(in TTuple items) where TTuple : IVarTuple => new(this, items);

		#endregion

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
			=> FdbKeyHelpers.IsChildOf(in this, key);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.IsChildOf(in this, in key);

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			null or "" or "D" or "d" => $"\u2026{this.Items}",
			"K" or "k" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"B" or "b" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}",
			"E" or "e" => $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}",
			"X" or "x" => this.ToSlice().ToString(format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? $"{this.Subspace:P}{this.Items}"
				: $"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}",
			"G" or "g" => $"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"\u2026{this.Items}", out charsWritten),
			"K" or "k" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"B" or "b" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Begin)}", out charsWritten),
			"E" or "e" => destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.End)}", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"P" or "p" => this.Subspace is not null && Subspace.GetPath() != FdbPath.Empty
				? destination.TryWrite($"{this.Subspace:P}{this.Items}", out charsWritten)
				: destination.TryWrite($"{FdbKey.PrettyPrint(this.ToSlice(), FdbKey.PrettyPrintMode.Single)}", out charsWritten),
			"G" or "g" => destination.TryWrite($"{this.GetType().GetFriendlyName()}(Subspace={this.Subspace}, Items={this.Items})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!((ITupleSpanPackable) this.Items).TryGetSizeHint(embedded: false, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = this.Subspace is null ? size : checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace is not null ? this.Subspace.GetPrefix().Span : default, in this.Items);

		#endregion

	}

}
