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

	/// <summary>Range of keys, defined by a lower and upper bound.</summary>
	[PublicAPI]
	public interface IFdbKeyRange : ISpanFormattable
	{

		/// <summary>Encode this range into a binary <see cref="KeyRange"/></summary>
		[Pure]
		KeyRange ToKeyRange();

		/// <summary>Returns the encoded "Begin" key of this range</summary>
		[Pure]
		Slice ToBeginKey();

		/// <summary>Returns the encoded "End" key of this range</summary>
		[Pure]
		Slice ToEndKey();

		/// <summary>Returns a <see cref="KeySelector"/> that will resolve the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure]
		KeySelector ToBeginSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will resolve the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure]
		KeySelector ToEndSelector();

		/// <summary>Tests if this range contains the given key</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key would be matched by this range.</returns>
		[Pure]
		bool Contains(ReadOnlySpan<byte> key);

		/// <summary>Tests if this range contains the given key</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key would be matched by this range.</returns>
		[Pure]
		bool Contains(Slice key);

		/// <summary>Tests if this range contains the given key</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key would be matched by this range.</returns>
		[Pure]
		bool Contains<TKey>(in TKey key) where TKey : struct, IFdbKey;

	}

	/// <summary>Defines how a boundary key should be considered in a <see cref="FdbKeyRange"/></summary>
	[PublicAPI]
	public enum KeyRangeMode
	{
		/// <summary>Use the default behavior for this parameter</summary>
		/// <remarks>This is usually <see cref="Inclusive"/> for Begin keys, and <see cref="Exclusive"/> for End keys</remarks>
		Default = 0,

		/// <summary>The key is included in the range</summary>
		/// <remarks>
		/// <para>If this is the Begin key, it is used as-is.</para>
		/// <para>If this is the End key, its Successor will be used (<c>key.`\x00`)</c></para>
		/// </remarks>
		Inclusive,

		/// <summary>The key is excluded from the range</summary>
		/// <remarks>
		/// <para>If this is the Begin key, its successor will be used (<c>key.`\x00`</c>).</para>
		/// <para>If this is the End key, it is used as-is.</para>
		/// </remarks>
		Exclusive,

		/// <summary>The key and all of its children is not included in the range</summary>
		/// <remarks>
		/// <para>The next sibling will be used for both Begin and End Key (<c>increment(key)</c>)</para>
		/// </remarks>
		NextSibling,

		/// <summary>The key and all of its children that can be represented by tuples are included in the range</summary>
		/// <remarks>
		/// <para>This is not allowed for Begin keys.</para>
		/// <para>If this is the End key, its last valid element will be used (<c>key.`\xFF`</c>)</para>
		/// </remarks>
		Last,
	}

}
