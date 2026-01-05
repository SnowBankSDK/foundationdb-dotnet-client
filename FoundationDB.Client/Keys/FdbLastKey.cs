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

	/// <summary>Key that appends the <b>0xFF</b> byte to another key</summary>
	/// <typeparam name="TKey">Type of the parent key</typeparam>
	/// <remarks>
	/// <para>This can be used to represent the upper bound for all keys that can be expressed using the Tuple Encoding.</para>
	/// <para>It is frequently used to generate the upper bound of a range read that should not "escape" into the next subspace, which could lead to "false" conflicts.</para>
	/// </remarks>
	public readonly struct FdbLastKey<TKey> : IFdbKey
		, IEquatable<FdbLastKey<TKey>>, IComparable<FdbLastKey<TKey>>
		where TKey : struct, IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbLastKey(in TKey parent)
		{
			this.Parent = parent;
		}

		public readonly TKey Parent;

		#region IFdbKey...

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			if (this.Parent.TryGetSpan(out var parentSpan))
			{
				return key.Length > parentSpan.Length && key[parentSpan.Length] == 0xFF && key.StartsWith(parentSpan);
			}

			if (typeof(TKey) == typeof(FdbSuffixKey))
			{
				var parent = ((FdbSuffixKey) (object) this.Parent).Subspace.GetPrefix().Span;
				var suffix = ((FdbSuffixKey) (object) this.Parent).Suffix.Span;
				int length = checked(parent.Length + suffix.Length);
				return key.Length > length && key[length] == 0xFF && key.StartsWith(parent) && key[parent.Length..].StartsWith(suffix);
			}

			return FdbKeyHelpers.IsChildOf(in this, key);
		}

		/// <inheritdoc />
		[Pure]
		public bool Contains<TOtherKey>(in TOtherKey key)
			where TOtherKey : struct, IFdbKey
		{
			return FdbKeyHelpers.IsChildOf(in this, in key);
		}

		#endregion

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" => $"{this.Parent}.<FF>",
			"X" or "x" => this.ToSlice().ToString(format),
			"K" or "k" or "B" or "b" or "E" or "e" => $"{this.Parent:K}.<FF>",
			"P" or "p" => $"{this.Parent:P}.<FF>",
			"G" or "g" => $"{nameof(FdbLastKey<>)}({this.Parent:G})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"{this.Parent}.<FF>", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"K" or "k" or "B" or "b" or "E" or "e" => destination.TryWrite($"{this.Parent:K}.<FF>", out charsWritten),
			"P" or "p" => destination.TryWrite($"{this.Parent:P}.<FF>", out charsWritten),
			"G" or "g" => destination.TryWrite($"{nameof(FdbLastKey<>)}({this.Parent:G})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => FdbKeyHelpers.AreEqual(in this, bytes.Span),
			FdbRawKey key => FdbKeyHelpers.AreEqual(in this, key.Span),
			FdbLastKey<TKey> key => FdbKeyHelpers.AreEqual(in this.Parent, in key.Parent),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbRawKey key => FdbKeyHelpers.AreEqual(in this, key.Span),
			FdbLastKey<TKey> key => FdbKeyHelpers.AreEqual(in this.Parent, in key.Parent),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbLastKey<TKey>))
			{
				return FdbKeyHelpers.AreEqual(in this.Parent, in Unsafe.As<TOtherKey, FdbLastKey<TKey>>(ref Unsafe.AsRef(in other)).Parent);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.AreEqual(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbLastKey<TKey> other)
			=> FdbKeyHelpers.AreEqual(in this.Parent, in other.Parent);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other)
			=> FdbKeyHelpers.AreEqual(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other)
			=> FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc cref="Equals(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.AreEqual(in this, other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbLastKey<TKey> key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbLastKey<TKey> other)
			=> FdbKeyHelpers.Compare(in this.Parent, in other.Parent);

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

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			// we cannot modify the original span to append 0x00, so we don't support this operation
			span = default;
			return false;
		}

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Parent.TryGetSizeHint(out var size))
			{
				sizeHint = 0;
				return false;
			}

			// we always add 1 to the original size
			sizeHint = checked(size + 1);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			// first generate the wrapped key
			if (!this.Parent.TryEncode(destination, out var len)
			    || destination.Length <= len)
			{
				bytesWritten = 0;
				return false;
			}

			// appends the 0xFF at the end
			destination[len] = 0xFF;
			bytesWritten = checked(len + 1);
			return true;
		}

		#endregion

	}

}
