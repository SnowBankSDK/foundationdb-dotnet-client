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

namespace System
{
	using SnowBank.Buffers.Binary;

	public partial struct Slice
	{

		/// <summary>Performs optimized equality and comparison checks on Slices</summary>
		public sealed class Comparer : IComparer<Slice>, IEqualityComparer<Slice>, IComparer<KeyValuePair<Slice, Slice>>
		{
			/// <summary>Default instance of the slice comparator</summary>
			public static readonly Comparer Default = new();

			private Comparer()
			{ }

			/// <summary>Lexicographically compare two slices and returns an indication of their relative sort order.</summary>
			/// <param name="x">Slice compared with <paramref name="y"/></param>
			/// <param name="y">Slice compared with <paramref name="x"/></param>
			/// <returns>Returns a NEGATIVE value if <paramref name="x"/> is LESS THAN <paramref name="y"/>, ZERO if <paramref name="x"/> is EQUAL TO <paramref name="y"/>, and a POSITIVE value if <paramref name="x"/> is GREATER THAN <paramref name="y"/>.</returns>
			/// <remarks>
			/// <para>If both <paramref name="x"/> and <paramref name="y"/> are nil or empty, the comparison will return ZERO. If only <paramref name="y"/> is nil or empty, it will return a NEGATIVE value. If only <paramref name="x"/> is nil or empty, it will return a POSITIVE value.</para>
			/// <para>There are no guarantees that non-zero results will be exactly -1 or +1. You should always use comparison operators or the sign of the returned value, instead of testing for equality with -1 or +1.</para>
			/// </remarks>
			public int Compare(Slice x, Slice y)
			{
				//REVIEW: cmp(Nil, Empty) returns 0 but Nil != Empty ?
				if (x.Count == 0) return y.Count == 0 ? 0 : -1;
				if (y.Count == 0) return +1;
				return x.Span.SequenceCompareTo(y.Span);
			}

			/// <summary>Checks if two slices are equal.</summary>
			/// <param name="x">Slice compared with <paramref name="y"/></param>
			/// <param name="y">Slice compared with <paramref name="x"/></param>
			/// <returns>true if <paramref name="x"/> and <paramref name="y"/> have the same size and contain the same sequence of bytes; otherwise, false.</returns>
			public bool Equals(Slice x, Slice y)
			{
				// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				if (x.IsNull)
				{
					return y.IsNull;
				}

				return x.Count == y.Count && y.Array is not null && x.Span.SequenceEqual(y.Span);
				// ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			}

			/// <summary>Computes the hash code of a slice</summary>
			/// <param name="obj">A slice</param>
			/// <returns>A 32-bit signed hash coded calculated from all the bytes in the slice</returns>
			public int GetHashCode(Slice obj)
			{
				// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				return obj.Array is null ? 0 : UnsafeHelpers.ComputeHashCode(obj.Span);
			}

			int IComparer<KeyValuePair<Slice, Slice>>.Compare(KeyValuePair<Slice, Slice> x, KeyValuePair<Slice, Slice> y)
			{
				// only compare the keys
				return Compare(x.Key, y.Key);
			}

		}

	}

}
