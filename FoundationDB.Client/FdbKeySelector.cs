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
	using SnowBank.Buffers.Text;

	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public static class FdbKeySelector
	{

		/// <summary>Creates a key selector that will select the last key in the database that is less than <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> LastLessThan<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> new(key, false, 0);

		/// <summary>Creates a key selector that will select the last key in the database that is less than or equal to <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> LastLessOrEqual<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> new(key, true, 0);

		/// <summary>Creates a key selector that will select the first key in the database that is greater than <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> FirstGreaterThan<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> new(key, true, 1);

		/// <summary>Creates a key selector that will select the first key in the database that is greater than or equal to <paramref name="key"/></summary>
		public static FdbKeySelector<TKey> FirstGreaterOrEqual<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> new(key, false, 1);

		/// <summary>Creates a key selector that will select the last key in the database that is less than <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<FdbRawKey> LastLessThan(Slice key)
			=> new(FdbKey.FromBytes(key), false, 0);

		/// <summary>Creates a key selector that will select the last key in the database that is less than or equal to <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<FdbRawKey> LastLessOrEqual(Slice key)
			=> new(FdbKey.FromBytes(key), true, 0);

		/// <summary>Creates a key selector that will select the first key in the database that is greater than <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<FdbRawKey> FirstGreaterThan(Slice key)
			=> new(FdbKey.FromBytes(key), true, 1);

		/// <summary>Creates a key selector that will select the first key in the database that is greater than or equal to <paramref name="key"/></summary>
		public static FdbKeySelector<FdbRawKey> FirstGreaterOrEqual(Slice key)
			=> new(FdbKey.FromBytes(key), false, 1);

	}

	internal static class FdbKeySelectorHelpers
	{

		public static bool TryGetSpan<TKey>(in FdbKeySelector<TKey> selector, out KeySpanSelector spanSelector)
			where TKey : struct, IFdbKey
		{
			if (selector.Key.TryGetSpan(out var span))
			{
				spanSelector = new(span, selector.OrEqual, selector.Offset);
				return true;
			}

			spanSelector = default;
			return false;
		}

		public static KeySelector Encode<TKey>(in FdbKeySelector<TKey> selector, ref byte[]? buffer, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			var key = FdbKeyHelpers.ToSlice(in selector.Key, ref buffer, pool);
			return new(key, selector.OrEqual, selector.Offset);
		}

	}

	public readonly struct FdbKeySelector<TKey> : IFormattable
		where TKey : struct, IFdbKey
	{

		/// <summary>Key of the selector</summary>
		public readonly TKey Key;

		/// <summary>If true, the selected key can be equal to <see cref="Key"/>.</summary>
		public readonly bool OrEqual;

		/// <summary>Offset of the selected key</summary>
		public readonly int Offset;

		/// <summary>Creates a new selector</summary>
		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector(TKey key, bool orEqual, int offset)
		{
			this.Key = key;
			this.OrEqual = orEqual;
			this.Offset = offset;
		}

		public KeySelector ToSelector()
		{
			return new(this.Key.ToSlice(), this.OrEqual, this.Offset);
		}

		/// <summary>Adds a value to the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns><c>fGE{'abc'} + 7</c></returns>
		public static FdbKeySelector<TKey> operator +(FdbKeySelector<TKey> selector, int offset)
		{
			return new(selector.Key, selector.OrEqual, checked(selector.Offset + offset));
		}

		/// <summary>Subtracts a value from the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns><c>fGE{'abc'} - 7</c></returns>
		public static FdbKeySelector<TKey> operator -(FdbKeySelector<TKey> selector, int offset)
		{
			return new(selector.Key, selector.OrEqual, checked(selector.Offset - offset));
		}

		/// <summary>Increments the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <returns><c>fGE{'abc'} + 1</c></returns>
		public static FdbKeySelector<TKey> operator ++(FdbKeySelector<TKey> selector)
		{
			return new(selector.Key, selector.OrEqual, checked(selector.Offset + 1));
		}

		/// <summary>Converts the value of the current <see cref="KeySelector"/> object into its equivalent string representation</summary>
		public override string ToString() => ToString(null);

		/// <summary>Converts the value of the current <see cref="KeySelector"/> object into its equivalent string representation</summary>
		public string ToString(string? format, IFormatProvider? formatProvider = null) => PrettyPrint(FdbKey.PrettyPrintMode.Single);

		/// <summary>Decrement the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <returns><c>fGE{'abc'} - 1</c></returns>
		public static FdbKeySelector<TKey> operator --(FdbKeySelector<TKey> selector)
		{
			return new(selector.Key, selector.OrEqual, checked(selector.Offset - 1));
		}

		/// <summary>Returns a displayable representation of the key selector</summary>
		[Pure]
		public string PrettyPrint(FdbKey.PrettyPrintMode mode)
		{
			Span<char> tmp = stackalloc char[128];
			using var sb = new FastStringBuilder(tmp);

			int offset = this.Offset;
			if (offset < 1)
			{
				sb.Append(this.OrEqual ? "lLE{" : "lLT{");
			}
			else
			{
				--offset;
				sb.Append(this.OrEqual ? "fGT{" : "fGE{");
			}
			//PERF: optimize!
			sb.Append(FdbKey.PrettyPrint(this.Key.ToSlice(), mode));
			sb.Append('}');

			if (offset > 0)
			{
				sb.Append(" + ");
				sb.Append(offset);
			}
			else if (offset < 0)
			{
				sb.Append(" - ");
				sb.Append(-offset);
			}

			return sb.ToString();
		}

	}

}
