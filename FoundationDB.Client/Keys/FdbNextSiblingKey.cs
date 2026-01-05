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

	/// <summary>Key that increments the last byte (with carry) of another key</summary>
	/// <typeparam name="TKey">Type of the parent key</typeparam>
	/// <remarks>
	/// <para>This key is the first key that comes after all the children of its previous sibling</para>
	/// <para>It is frequently used as the end (exclusive) of a range read.</para>
	/// </remarks>
	public readonly struct FdbNextSiblingKey<TKey> : IFdbKey
		, IEquatable<FdbNextSiblingKey<TKey>>, IComparable<FdbNextSiblingKey<TKey>>
		where TKey : struct, IFdbKey
	{

		public FdbNextSiblingKey(in TKey parent)
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
			// the only thing we can quickly check, is that is key is less than or equal to our parent, then it is NOT a child
			if (this.Parent.TryGetSpan(out var parentSpan))
			{
				if (parentSpan.SequenceCompareTo(key) <= 0)
				{ // if child of previous sibling of parent, it cannot be a child of its next sibling
					return false;
				}
				if (key.StartsWith(parentSpan))
				{ // if child of parent, it cannot be child of its next sibling
					return false;
				}
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
			"" or "D" or "d" => $"{this.Parent}+1",
			"X" or "x" => this.ToSlice().ToString(format),
			"K" or "k" or "B" or "b" or "E" or "e" => $"{this.Parent:K}+1",
			"P" or "p" => $"{this.Parent:P}+1",
			"G" or "g" => $"{nameof(FdbNextSiblingKey<>)}({this.Parent:G})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" => destination.TryWrite($"{this.Parent}+1", out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"K" or "k" or "B" or "b" or "E" or "e" => destination.TryWrite($"{this.Parent:K}+1", out charsWritten),
			"P" or "p" => destination.TryWrite($"{this.Parent:P}+1", out charsWritten),
			"G" or "g" => destination.TryWrite($"{nameof(FdbNextSiblingKey<>)}({this.Parent:G})", out charsWritten),
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
			Slice bytes => this.Equals(bytes.Span),
			FdbRawKey key => this.Equals(key.Span),
			FdbNextSiblingKey<TKey> key => this.Equals(key),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbRawKey key => this.Equals(key.Span),
			FdbNextSiblingKey<TKey> key => FdbKeyHelpers.AreEqual(in this.Parent, in key.Parent),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbNextSiblingKey<TKey>))
			{
				return FdbKeyHelpers.AreEqual(in this.Parent, in Unsafe.As<TOtherKey, FdbNextSiblingKey<TKey>>(ref Unsafe.AsRef(in other)).Parent);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.AreEqual(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbNextSiblingKey<TKey> other)
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
			FdbNextSiblingKey<TKey> key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbNextSiblingKey<TKey> other)
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
			// we cannot modify the original span, so we don't support this operation
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

			// incrementing the key can only produce a key of the same size or smaller, except when the key is empty in which case the successor has size 1
			sizeHint = Math.Max(size, 1);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			// first generate the wrapped key
			if (!this.Parent.TryEncode(destination, out var len))
			{
				goto too_small;
			}

			// if the key is empty (??) we need to return 'x00'
			if (len == 0)
			{
				goto empty_key;
			}

			// increment the buffer in-place
			// => throws if the key is all FF
			FdbKey.Increment(destination[..len], out len);
			bytesWritten = len;
			return true;

		empty_key:
			if (destination.Length == 0)
			{
				goto too_small;
			}
			destination[0] = 0;
			bytesWritten = 1;
			return true;

		too_small:
			bytesWritten = 0;
			return false;

		}

		#endregion

	}

}
