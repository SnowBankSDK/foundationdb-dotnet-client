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

namespace SnowBank.Text
{
	using System.Text;

	public static class StringBuilderCache
	{
		//README: this is a copy of the BCL's StringBuilderCache which is internal
		//TODO: update the magic numbers that may or may not be appropriate for our use cases!

		private const int MAX_BUILDER_SIZE = 360;

		[ThreadStatic] private static StringBuilder? CachedInstance;

		public static StringBuilder Acquire(int capacity = 16)
		{
			if (capacity <= MAX_BUILDER_SIZE)
			{
				var sb = CachedInstance;
				if (sb != null)
				{
					// Avoid stringbuilder block fragmentation by getting a new StringBuilder
					// when the requested size is larger than the current capacity
					if (capacity <= sb.Capacity)
					{
						CachedInstance = null;
						sb.Clear();
						return sb;
					}
				}
			}
			return new StringBuilder(capacity);
		}

		public static void Release(StringBuilder sb)
		{
			if (sb.Capacity <= MAX_BUILDER_SIZE)
			{
				CachedInstance = sb;
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string GetStringAndRelease(StringBuilder sb)
		{
			string result = sb.ToString();
			Release(sb);
			return result;
		}
	}

}
