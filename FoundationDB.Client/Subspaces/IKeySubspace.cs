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

	/// <summary>Represents a sub-partition of the global key space.</summary>
	/// <remarks>
	/// A subspace is the logical equivalent of a key prefix that is implicitly prepended to all keys generated from it.
	/// A "vanilla" data subspace does not imply any encoding scheme by default, but can be wrapped into a more complex subspace which includes Key Codec.
	/// </remarks>
	/// 
	/// <example>In pseudocode, and given a 'MySubspaceImpl' that implement <see cref="IKeySubspace"/>:
	/// <code>
	/// subspace = new MySubspaceImpl({ABC})
	/// subspace.ConcatKey({123}) => {ABC123}
	/// subspace.ExtractKey({ABC123}) => {123}
	/// subspace.ExtractKey({DEF123}) => ERROR
	/// </code>
	/// </example>
	[PublicAPI]
	public interface IKeySubspace : ISpanEncodable, IEquatable<IKeySubspace>, IComparable<IKeySubspace>, ISpanFormattable
	{
		// This interface helps solve some type resolution ambiguities at compile time between types that all implement IFdbKey but have different semantics for partitioning and concatenation

		/// <summary>The context from which this subspace came from</summary>
		/// <remarks>
		/// The context is usually the directory or global subspace of the database.
		/// It can also be the <see cref="SubspaceContext.Default"/> context for keys created outside any context
		/// The context is used to track the origin of a subspace, and optionally revoke access to it once the context becomes invalid (ex: cached context no longer being valid)
		/// </remarks>
		ISubspaceContext Context { get; }

		/// <summary>Returns the prefix of this subspace</summary>
		Slice GetPrefix();

		/// <summary>Returns the path of the subspace, if there is one.</summary>
		/// <returns>Path of the subspace, or <see cref="FdbPath.Empty"/> if this subspace is not part of the Directory Layer</returns>
		FdbPath GetPath();

		/// <summary>Return a range that will match all the keys in this subspace, including the prefix itself</summary>
		/// <returns>Return the range: Key &lt;= x &lt;= Increment(Key)</returns>
		[Pure]
		FdbSubspaceKeyRange ToRange(bool inclusive = false);

		/// <summary>Test if a key is inside the range of keys logically contained by this subspace</summary>
		/// <param name="absoluteKey">Key to test</param>
		/// <returns>True if the key can exist inside the current subspace.</returns>
		/// <remarks>Please note that this method does not test if the key *actually* exists in the database, only if the key is not outside the range of keys defined by the subspace.</remarks>
		[Pure]
		bool Contains(ReadOnlySpan<byte> absoluteKey);

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, <see cref="Slice.Empty"/> if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		Slice BoundCheck(Slice key, bool allowSystemKeys);

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, <see cref="Slice.Empty"/> if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		ReadOnlySpan<byte> BoundCheck(ReadOnlySpan<byte> key, bool allowSystemKeys);

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or <see cref="Slice.Nil"/> if the key does not fit inside the namespace</summary>
		/// <param name="absoluteKey">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="absoluteKey"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or <see cref="Slice.Empty"/> if the key is exactly equal to the subspace prefix). If the key is outside the subspace, returns <see cref="Slice.Nil"/></returns>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="absoluteKey"/> is outside the current subspace.</exception>
		[Pure]
		ReadOnlySpan<byte> ExtractKey(ReadOnlySpan<byte> absoluteKey, bool boundCheck = false);

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or <see cref="Slice.Nil"/> if the key does not fit inside the namespace</summary>
		/// <param name="absoluteKey">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="absoluteKey"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or <see cref="Slice.Empty"/> if the key is exactly equal to the subspace prefix). If the key is outside the subspace, returns <see cref="Slice.Nil"/></returns>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="absoluteKey"/> is outside the current subspace.</exception>
		[Pure]
		Slice ExtractKey(Slice absoluteKey, bool boundCheck = false);

		/// <summary>Return a human-friendly string representation of a key, as encoded by this subspace</summary>
		/// <param name="packedKey">Key that was generated from this subspace</param>
		string PrettyPrint(Slice packedKey);

	}

}
