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
	using System.Collections;

	/// <summary>Helper methods to work with hashcodes</summary>
	/// <remarks>Use <see cref="System.HashCode"/> instead!</remarks>
	[PublicAPI]
	[Obsolete("You can now use System.Hashcode which is available via the runtime")]
	public static class HashCodes
	{

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(long value)
		{
			return unchecked((int) value) ^ (int) (value >> 32);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(ulong value)
		{
			return unchecked((int)value) ^ (int)(value >> 32);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(bool value)
		{
			return value ? 1 : 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(Guid value)
		{
			return value.GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(int? value)
		{
			return value ?? -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(long? value)
		{
			return value.HasValue ? Compute(value.Value) : -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(ulong? value)
		{
			return value.HasValue ? Compute(value.Value) : -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(bool? value)
		{
			return value.HasValue ? Compute(value.Value) : -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(Guid? value)
		{
			return value?.GetHashCode() ?? -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(string? value)
		{
			return value?.GetHashCode() ?? 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute<T>(T? value)
			where T : class
		{
			return value?.GetHashCode() ?? 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute<T>(T? value)
			where T : struct
		{
			return value.GetValueOrDefault().GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute<T>(T? value, IEqualityComparer comparer)
			where T : IStructuralEquatable
		{
			return value?.GetHashCode(comparer) ?? 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Combine(int h1, int h2)
		{
			return ((h1 << 5) + h1) ^ h2;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Combine(int h1, int h2, int h3)
		{
			int h = ((h1 << 5) + h1) ^ h2;
			return ((h << 5) + h) ^ h3;
		}

		[Pure]
		public static int Combine(int h1, int h2, int h3, int h4)
		{
			return Combine(Combine(h1, h2), Combine(h3, h4));
		}

		[Pure]
		public static int Combine(int h1, int h2, int h3, int h4, int h5)
		{
			return Combine(Combine(h1, h2, h3), Combine(h4, h5));
		}

		[Pure]
		public static int Combine(int h1, int h2, int h3, int h4, int h5, int h6)
		{
			return Combine(Combine(h1, h2, h3), Combine(h4, h5, h6));
		}

		/// <summary>Test that both hash codes, if present, have the same value</summary>
		/// <returns>False IIF h1 != nul &amp;&amp; h2 != null &amp;&amp; h1 != h2; otherwise, True</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SameOrMissing(int? h1, int? h2)
		{
			return !h1.HasValue || !h2.HasValue || h1.Value == h2.Value;
		}

		#region Flags...

		// Combines one or more booleans into a single value (one bit per boolean)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a)
		{
			return (a ? 1 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b)
		{
			return (a ? 1 : 0) | (b ? 2 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e, bool  f)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0) | (f ? 32 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e, bool  f, bool g)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0) | (f ? 32 : 0) | (g ? 64 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e, bool  f, bool g, bool h)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0) | (f ? 32 : 0) | (g ? 64 : 0) | (h ? 128 : 0);
		}

		#endregion

	}

}
