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

namespace SnowBank.Collections.Caching
{

	public interface ICache<TKey, TElement> : ICollection<KeyValuePair<TKey, TElement>>
	{
		// from ICollection<...>
		// int Count { get; }
		// void Clear();

		/// <summary>Current capacity of the cache (in number of items)</summary>
		/// <remarks>Caches that have no storage capacity limit must return int.MaxValue.</remarks>
		int Capacity { get; }

		/// <summary>Indicates whether the cache has a maximum capacity (true), or has no particular limit (false)</summary>
		/// <remarks>If IsCapped returns true, consult the value of <see cref="Capacity"/> to find the maximum capacity.</remarks>
		bool IsCapped { get; }

		/// <summary>Comparer used for the cache keys</summary>
		IEqualityComparer<TKey> KeyComparer { get; }

		/// <summary>Returns an entry from the cache, if it exists</summary>
		/// <param name="key">Key of the entry being looked up</param>
		/// <param name="value">Receives the cached value if it exists (and is still valid)</param>
		/// <returns>True if the value exists in the cache; false if it does not exist (or is no longer valid)</returns>
		bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TElement value);

		/// <summary>Returns the value of an entry in the cache, creating it if necessary</summary>
		/// <param name="key">Key of the entry being looked up</param>
		/// <param name="addValue">Value that will be added to the cache for this key, if it did not already exist</param>
		/// <returns>Value of the entry if it existed, or <paramref name="addValue"/> if it did not exist</returns>
		TElement GetOrAdd(TKey key, TElement addValue);

		/// <summary>Returns the value of an entry in the cache, creating it if necessary</summary>
		/// <param name="key">Key of the entry being looked up</param>
		/// <param name="factory">Lambda that will be called to generate the value to add, if it did not already exist</param>
		/// <returns>Value of the entry if it existed, or the result of <paramref name="factory"/> if it did not exist</returns>
		/// <remarks>Warning: some caches offer no guarantee that valueFactory will not be called several times!</remarks>
		TElement GetOrAdd(TKey key, [InstantHandle] Func<TKey, TElement> factory);

		/// <summary>Returns the value of an entry in the cache, creating it if necessary</summary>
		/// <param name="key">Key of the entry being looked up</param>
		/// <param name="factory">Lambda that will be called to generate the value to add, if it did not already exist</param>
		/// <param name="state">Value passed as the second parameter to <paramref name="factory"/></param>
		/// <returns>Value of the entry if it existed, or the result of <paramref name="factory"/> if it did not exist</returns>
		/// <remarks>Warning: some caches offer no guarantee that valueFactory will not be called several times!</remarks>
		TElement GetOrAdd<TState>(TKey key, [InstantHandle] Func<TKey, TState, TElement> factory, TState state);

		/// <summary>Overwrites the value of a cache entry, creating it if necessary</summary>
		/// <param name="key">Key of the entry</param>
		/// <param name="newValue">New value</param>
		void SetItem(TKey key, TElement newValue);

		/// <summary>Removes an entry from the cache</summary>
		/// <param name="key">Key of the entry to remove</param>
		/// <returns>True if the entry was removed, false if it did not exist</returns>
		bool Remove(TKey key);

		/// <summary>Removes an entry from the cache, only if it has a specific value</summary>
		/// <param name="key">Key of the entry to remove</param>
		/// <param name="expectedValue">Value that the entry must have to be removed</param>
		/// <param name="valueComparer">Optional comparer for the values</param>
		/// <returns>True if the entry existed and had the expected value, or false otherwise.</returns>
		bool TryRemove(TKey key, TElement expectedValue, IEqualityComparer<TElement>? valueComparer = null);

		/// <summary>Finds and removes entries from the cache</summary>
		/// <param name="predicate">Predicate that returns true for the entries to remove</param>
		/// <returns>Number of entries removed from the cache</returns>
		int Cleanup([InstantHandle] Func<TKey, TElement, bool> predicate);
	}

}
