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

	/// <summary>Represents a pair of key selectors that range 'GetKey(Begin) &lt;= key &lt; GetKey(End)'</summary>
	[DebuggerDisplay("[ToString()]")]
	public readonly struct KeySelectorPair : IFormattable
	{
		/// <summary>Start of the range</summary>
		public readonly KeySelector Begin;

		/// <summary>End of the range</summary>
		public readonly KeySelector End;

		/// <summary>Create a new pair of key selectors</summary>
		/// <param name="beginInclusive">Selector for key from which to start iterating</param>
		/// <param name="endExclusive">Selector for key where to stop iterating</param>
		public KeySelectorPair(KeySelector beginInclusive, KeySelector endExclusive)
		{
			this.Begin = beginInclusive;
			this.End = endExclusive;
		}

		/// <summary>Factory method for a pair of key selectors</summary>
		public static KeySelectorPair Create(KeySelector beginInclusive, KeySelector endExclusive)
		{
			return new KeySelectorPair(
				beginInclusive, 
				endExclusive
			);
		}

		/// <summary>Create a new pair of key selectors using <c>FIRST_GREATER_OR_EQUAL</c> on both keys</summary>
		public static KeySelectorPair Create(Slice begin, Slice end)
		{
			return new KeySelectorPair(
				KeySelector.FirstGreaterOrEqual(begin),
				KeySelector.FirstGreaterOrEqual(end)
			);
		}

		/// <summary>Creates a pair of key selectors using <c>FIRST_GREATER_OR_EQUAL</c> on both keys</summary>
		public static KeySelectorPair Create(KeyRange range)
		{
			return new KeySelectorPair(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End)
			);
		}

		/// <summary>Creates a pair of key selectors that will select all the keys that start with the specified prefix</summary>
		/// <param name="prefix">Common prefix of the keys that must be read</param>
		public static KeySelectorPair StartsWith(Slice prefix)
		{
			var range = KeyRange.StartsWith(prefix);

			return new KeySelectorPair(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End)
			);
		}

		/// <summary>Returns a pair of key selectors that will select all the keys that start with the specified prefix, and after a specific cursor</summary>
		/// <param name="prefix">Common prefix of the keys that must be read (usually the containing subspace)</param>
		/// <param name="cursor">Value of the cursor, that is appended to <paramref name="prefix"/>. The caller has already read any keys that are before this point, and wants to resume reading past this point</param>
		/// <param name="orEqual">If <see langword="true"/>, the key <paramref name="prefix"/> + <paramref name="cursor"/> will be included in the range (<c>FIRST_GREATER_OR_EQUAL</c>); otherwise it will be skipped (<c>FIRST_GREATER_THAN</c>)</param>
		/// <returns>Pair of selectors that will read any keys after the cursor (included if <paramref name="orEqual"/> is <see langword="true"/>)</returns>
		/// <remarks>
		/// <para>This is a common usage pattern when consuming a stream of logs or records that are indexed by a <see cref="VersionStamp"/> or a counter.</para>
		/// <para>Depending on the algorithm used, <paramref name="cursor"/> may represent the last read entry, in which case <paramref name="orEqual"/> should be <see langword="false"/>; or it could represent the next expected entry, in which case <paramref name="orEqual"/> should be <see langword="true"/>.</para>
		/// </remarks>
		public static KeySelectorPair Tail(Slice prefix, Slice cursor, bool orEqual)
		{
			// begin: FIRST_GREATER_[OR_EQUAL|THAN](prefix + cursor)
			// end: FIRST_GREATER_OR_EQUAL(inc(prefix))

			var pivot = prefix + cursor;
			var begin = orEqual ? KeySelector.FirstGreaterOrEqual(pivot) : KeySelector.FirstGreaterThan(pivot); // start from the cursor, or from the next key
			var end = KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix)); // first key that follows the common prefix
			return new(begin, end);
		}

		/// <summary>Returns a pair of key selectors that will select all the keys that start with the specified prefix, up until a specific cursor</summary>
		/// <param name="prefix">Common prefix of the keys that must be read (usually the containing subspace)</param>
		/// <param name="cursor">Value of the cursor, that is appended to <paramref name="prefix"/>. The caller wants to read or clear any keys that are before this point</param>
		/// <param name="orEqual">If <see langword="true"/>, the key <paramref name="prefix"/> + <paramref name="cursor"/> will be included in the range (<c>FIRST_GREATER_THAN</c>); otherwise it will be skipped (<c>FIRST_GREATER_OR_EQUAL</c>)</param>
		/// <returns>Pair of selectors that will read any keys after the cursor (included if <paramref name="orEqual"/> is <see langword="true"/>)</returns>
		/// <remarks>
		/// <para>This is a common usage pattern when consuming a stream of logs or records that are indexed by a <see cref="VersionStamp"/> or a counter.</para>
		/// <para>For example in a CLEAR_RANGE operation, <paramref name="cursor"/> may represent the first entry to keep, in which case <paramref name="orEqual"/> should be <see langword="false"/>; or it could represent the last entry to delete, in which case <paramref name="orEqual"/> should be <see langword="true"/>.</para>
		/// </remarks>
		public static KeySelectorPair Head(Slice prefix, Slice cursor, bool orEqual)
		{
			// begin: FIRST_GREATER_OR_EQUAL(prefix)
			// end:   FIRST_GERATER_[OR_EQUAL|THAN](prefix + cursor)

			var pivot = prefix + cursor;
			var begin = KeySelector.FirstGreaterThan(prefix); //note: the prefix itself is NOT included!
			var end = orEqual ? KeySelector.FirstGreaterThan(pivot) : KeySelector.FirstGreaterOrEqual(pivot); // end at the cursor, or on the next key
			return new(begin, end);
		}

		/// <summary>Returns a printable version of the pair of key selectors</summary>
		public override string ToString()
		{
			return $"[ {this.Begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin)}, {this.End.PrettyPrint(FdbKey.PrettyPrintMode.End)} )";
		}

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

		/// <summary>Deconstructs this key selector pair</summary>
		/// <param name="begin">Receives the <see cref="Begin"/> selector</param>
		/// <param name="end">Receives the <see cref="End"/> selector</param>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void Deconstruct(out KeySelector begin, out KeySelector end)
		{
			begin = this.Begin;
			end = this.End;
		}

	}

}
