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
	using System.Collections;
	using System.Collections.Frozen;
	using System.Collections.Immutable;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers;

	/// <summary>Implements a cache with values that do not frequently change</summary>
	/// <typeparam name="TKey">Type of the keys</typeparam>
	/// <typeparam name="TValue">Type of the cached values</typeparam>
	/// <remarks>
	/// <para>This cache is optimized for when the application only handle a small number of 'static' values, which will be quickly populated during startup, and then with very infrequent additions</para>
	/// <para>The cache uses Copy-on-write semantics to publish a new updated snapshot that includes the new cached value.</para>
	/// <para>The cache is guaranteed to be thread-safe. When concurrent threads attempt to insert the same new key, only one will "win" the race, and the others threads will discard their own instance and use the instance created by the winning thread.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class QuasiImmutableCache<TKey, TValue> : ICache<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
		where TKey : notnull
	{

		/// <summary>Current snapshot of the cache</summary>
		/// <remarks>This instance is never modified. Any changes to the cache will attempt to publish a new instance to this field, using a retry loop if needed.</remarks>
		private volatile FrozenDictionary<TKey, TValue> m_root;

		/// <summary>Comparator used for the keys</summary>
		private readonly IEqualityComparer<TValue> m_valueComparer;

		/// <summary>Factory method that is called to create a new value from a given key</summary>
		private readonly Func<TKey, TValue>? m_valueFactory;

		public QuasiImmutableCache()
			: this(valueFactory: null, keyComparer: null, valueComparer: null)
		{ }

		public QuasiImmutableCache(Func<TKey, TValue>? valueFactory)
			: this(FrozenDictionary<TKey, TValue>.Empty, valueFactory, EqualityComparer<TValue>.Default)
		{ }

		public QuasiImmutableCache(IEqualityComparer<TKey>? keyComparer)
			: this(valueFactory: null, keyComparer)
		{ }

		public QuasiImmutableCache(Func<TKey, TValue>? valueFactory, IEqualityComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer = null)
		{
			// note: prefer using ordinal string comparison
			keyComparer ??= typeof(TKey) == typeof(string) ? (IEqualityComparer<TKey>)StringComparer.Ordinal : EqualityComparer<TKey>.Default;
			// note: we need to store the key comparer in the original dictionary, which we cannot do using FrozenDictionary.Empty...
			m_root = Array.Empty<KeyValuePair<TKey, TValue>>().ToFrozenDictionary(keyComparer);
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			m_valueFactory = valueFactory;
		}

		public QuasiImmutableCache(FrozenDictionary<TKey, TValue> items, Func<TKey, TValue>? valueFactory = null, IEqualityComparer<TValue>? valueComparer = null)
		{
			Contract.NotNull(items);
			m_root = items;
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			m_valueFactory = valueFactory;
		}

		/// <summary>Gets the number of elements contained in the cache.</summary>
		public int Count => m_root.Count;

		/// <summary>Instance used to compare the keys of the cache</summary>
		public IEqualityComparer<TKey> KeyComparer => m_root.Comparer;

		/// <summary>Instance used to compare the values of the cache</summary>
		public IEqualityComparer<TValue> ValueComparer => m_valueComparer;

		/// <summary>Factory method (optional) used to construct new values in the cache, if one is not already provided.</summary>
		/// <remarks>Only used by <see cref="GetOrAdd(TKey)"/>.</remarks>
		public Func<TKey, TValue>? Factory => m_valueFactory;

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

		bool ICache<TKey, TValue>.IsCapped => false;

		int ICache<TKey, TValue>.Capacity => int.MaxValue;

		/// <summary>Gets an enumerable collection that contains the keys in the read-only dictionary. </summary>
		public ImmutableArray<TKey> Keys => m_root.Keys;

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => m_root.Keys;

		/// <summary>Determines whether the read-only dictionary contains an element that has the specified key.</summary>
		public bool ContainsKey(TKey key)
		{
			return m_root.ContainsKey(key);
		}

		/// <summary>Gets an enumerable collection that contains the values in the read-only dictionary.</summary>
		public ImmutableArray<TValue> Values => m_root.Values;

		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => m_root.Values;

		/// <summary>Determines whether the read-only dictionary contains an element that has the specified value.</summary>
		[Pure]
		public bool ContainsValue(TValue? value)
		{
			// we can exploit the fact that FrozenDictionary.Values returns an ImmutableArray, from which we can get a ReadOnlySpan!

			var items = m_root.Values.AsSpan();

			if (value is null)
			{ // special case for 'null'
				// ReSharper disable once ForCanBeConvertedToForeach
				for (int i = 0; i < items.Length; i++)
				{
					if (items[i] is null) return true;
				}
			}
			else
			{
				var cmp = m_valueComparer;
				// ReSharper disable once ForCanBeConvertedToForeach
				for(int i = 0; i < items.Length; i++)
				{
					if (cmp.Equals(value, items[i])) return true;
				}
			}

			return false;
		}

		/// <summary>Returns the value of an entry in the cache, if it exists.</summary>
		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			return m_root.TryGetValue(key, out value);
		}

		/// <summary>Returns the value of an entry in the cache, or a default value it is not in the cache.</summary>
		public TValue GetValueOrDefault(TKey key, TValue defaultValue)
		{
			// ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
			return m_root.TryGetValue(key, out var value) ? value : defaultValue;
		}

		/// <summary>Gets the value of an entry in the cache, or <c>default</c> if it is missing.</summary>
		/// <returns>If the entry is not present, it will <b>NOT</b> be automatically created! Please use <see cref="GetOrAdd(TKey)"/> if this is what you need.</returns>
		public TValue this[TKey key]
		{
			[Pure]
			[return:MaybeNull]
			get => m_root.TryGetValue(key, out var result) ? result : default!;
		}

		/// <summary>Gets the value of a key in the cache, or add a new value using the default factory method</summary>
		/// <param name="key">Key in the cache to lookup</param>
		/// <returns>Existing cached value, or newly created value if it was not present before</returns>
		/// <remarks>
		/// <para>This method is only supported if <see cref="Factory"/> is not <c>null</c>.</para>
		/// <para>If multiple threads are attempting to concurrently populate the same key in the cache, they will all observe the same instance.</para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">If this cache does not have a <see cref="Factory">default factory method</see>.</exception>
		public TValue GetOrAdd(TKey key)
		{
			if (m_root.TryGetValue(key, out var result))
			{
				return result;
			}
			return GetOrAddSlow(key, out result);
		}

		private TValue GetOrAddSlow(TKey key, out TValue result)
		{
			// we already know it does not exist, so create it first
			var factory = m_valueFactory;
			if (factory is null) throw new InvalidOperationException("The cache does not have a default Value factory");
			result = factory(key);
			// then attempt to add our value (we may lose the race)
			TryAddInternal(key, result, false, out result);
			return result;
		}

		/// <summary>Gets the value of an entry in the cache, or add an already constructed value it is not present in the cache</summary>
		/// <param name="key">Key of the entry in the cache</param>
		/// <param name="value">Already constructed value that should be added to the cache if the key does not exist already</param>
		/// <returns>Value that was already present in the cache, or <paramref name="value"/> if it was not present.</returns>
		/// <remarks><para>If multiple threads are attempting to concurrently populate the same key in the cache, they will all observe the same instance.</para></remarks>
		public TValue GetOrAdd(TKey key, TValue value)
		{
			if (m_root.TryGetValue(key, out var result))
				return result;

			TryAddInternal(key, value, false, out result);
			return result;
		}

		/// <summary>Gets the value of an entry in the cache, or add a new value created using the provided factory method</summary>
		/// <param name="key">Key of the entry in the cache</param>
		/// <param name="factory">Factory method that will be called if a new value must be created.</param>
		/// <returns>Value that was already present in the cache, or the result of calling <paramref name="factory"/> if it was not present.</returns>
		/// <remarks>
		/// <para>If multiple threads are attempting to concurrently populate the same key in the cache, they will all observe the same instance.</para>
		/// </remarks>
		public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
		{
			Contract.Debug.Requires(factory != null);

			if (m_root.TryGetValue(key, out var result))
			{
				return result;
			}

			result = factory(key);
			TryAddInternal(key, result, false, out result);
			return result;
		}

		/// <summary>Gets the value of an entry in the cache, or add a new value created using the provided factory method</summary>
		/// <param name="key">Key of the entry in the cache</param>
		/// <param name="factory">Factory method that will be called if a new value must be created.</param>
		/// <param name="state">Opaque value that is passed to the <paramref name="factory"/> method.</param>
		/// <returns>Value that was already present in the cache, or the result of calling <paramref name="factory"/> if it was not present.</returns>
		/// <remarks>
		/// <para>If multiple threads are attempting to concurrently populate the same key in the cache, they will all observe the same instance.</para>
		/// </remarks>
		public TValue GetOrAdd<TState>(TKey key, Func<TKey, TState, TValue> factory, TState state)
		{
			Contract.Debug.Requires(factory != null);

			if (m_root.TryGetValue(key, out var result))
			{
				return result;
			}

			result = factory(key, state);
			TryAddInternal(key, result, false, out result);
			return result;
		}

		private void TryAddInternal(TKey key, TValue value, bool allowUpdate, out TValue result)
		{
			// since insertions happen mostly at startup, there is a high probability that multiple concurrent threads are fighting to insert either the same key, or different keys.
			// => we will accept the cost of creating a SpinWait up-front, since this should hopefully not happen a lot.

			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
				var updated = original;

				if (!original.TryGetValue(key, out var local))
				{
					updated = new Dictionary<TKey, TValue>(original, original.Comparer)
					{
						{key, value}
					}.ToFrozenDictionary();
					local = value;
				}
				else if (allowUpdate)
				{
					updated = new Dictionary<TKey, TValue>(original, original.Comparer)
					{
						[key] = value
					}.ToFrozenDictionary();
					local = value;
				}

#pragma warning disable 420
				if (ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{
					result = local;
					return;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Sets the value of an entry in the cache, regardless of its previous state</summary>
		/// <param name="key">Key of the entry to replace</param>
		/// <param name="value">New value for this entry</param>
		public void SetItem(TKey key, TValue value)
		{
			TryAddInternal(key, value, true, out TValue _);
		}

		private void TryAddRangeInternal(ReadOnlySpan<KeyValuePair<TKey, TValue>> items, bool allowUpdate)
		{
			// since insertions happen mostly at startup, there is a high probability that multiple concurrent threads are fighting to insert either the same key, or different keys.
			// => we will accept the cost of creating a SpinWait up-front, since this should hopefully not happen a lot.

			var wait = new SpinWait();

			// since we may retry, we will have to copy the items
			if (items.Length == 0)
			{
				return;
			}

			while (true)
			{
				var original = m_root;
				var builder = new Dictionary<TKey, TValue>(original, original.Comparer);
				foreach(var item in items)
				{
					if (allowUpdate)
					{
						builder[item.Key] = item.Value;
					}
					else
					{
						builder.Add(item.Key, item.Value);
					}
				}
				var updated = builder.ToFrozenDictionary();

#pragma warning disable 420
				if (ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{
					return;
				}

				wait.SpinOnce();
			}
		}

		private void TryAddRangeInternal(Dictionary<TKey, TValue> items, bool allowUpdate)
		{
			// since insertions happen mostly at startup, there is a high probability that multiple concurrent threads are fighting to insert either the same key, or different keys.
			// => we will accept the cost of creating a SpinWait up-front, since this should hopefully not happen a lot.

			var wait = new SpinWait();

			// since we may retry, we will have to copy the items
			if (items.Count == 0)
			{
				return;
			}

			while (true)
			{
				var original = m_root;
				var builder = new Dictionary<TKey, TValue>(original, original.Comparer);
				foreach(var item in items)
				{
					if (allowUpdate)
					{
						builder[item.Key] = item.Value;
					}
					else
					{
						builder.Add(item.Key, item.Value);
					}
				}
				var updated = builder.ToFrozenDictionary();

#pragma warning disable 420
				if (ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{
					return;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Adds or replace multiple entries in the cache, in single "transaction".</summary>
		/// <param name="items">List of all key/value pairs to insert or replace into the cache</param>
		/// <remarks>Entries that were already present in the cache will be overwritten by the new value in <paramref name="items"/></remarks>
		public void SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			Contract.NotNull(items);
			if (items.TryGetSpan(out var span))
			{
				TryAddRangeInternal(span, true);
			}
			else if (items is Dictionary<TKey, TValue> dict)
			{
				TryAddRangeInternal(dict, true);
			}
			else
			{
				var res = items.ToList();
				TryAddRangeInternal(CollectionsMarshal.AsSpan(res), true);
			}
		}

		/// <summary>Adds multiple entries to the cache, in a single "transaction".</summary>
		/// <param name="items">List of all the new key/value pairs to insert into the cache</param>
		/// <remarks>If at least one entry already exists, an exception is thrown and no changes will be made to the cache</remarks>
		/// <exception cref="InvalidOperationException">If at least one key in <paramref name="items"/> already exists in the cache</exception>
		public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			Contract.NotNull(items);
			if (items.TryGetSpan(out var span))
			{
				TryAddRangeInternal(span, false);
			}
			else if (items is Dictionary<TKey, TValue> dict)
			{
				TryAddRangeInternal(dict, false);
			}
			else
			{
				var res = items.ToList();
				TryAddRangeInternal(CollectionsMarshal.AsSpan(res), false);
			}
		}

		/// <summary>Adds or update an entry in the cache</summary>
		/// <param name="key">Key of the entry in the cache</param>
		/// <param name="addValue">Value to add, if the key is not present</param>
		/// <param name="updateValueFactory">Method that will produce an update value, if the key is already present</param>
		/// <returns><c>true</c> if the value was added, or <c>false</c> if it was updated</returns>
		public bool AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
		{
			var wait = new SpinWait();

			while (true)
			{
				bool flag = false;
				var original = m_root;
				Dictionary<TKey, TValue> builder;

				if (original.TryGetValue(key, out var local))
				{ // update!
					builder = new(original, original.Comparer)
					{
						[key] = updateValueFactory(key, local)
					};
				}
				else
				{
					builder = new(original, original.Comparer)
					{
						{ key, addValue }
					};
					flag = true;
				}
				var updated = builder.ToFrozenDictionary();

				if (ReferenceEquals(updated, original))
				{ // The cache already contained this key/value pair, no change required
					return false;
				}

#pragma warning disable 420
				if (ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{ // we won the race, we only need to check if the key was already present in the previous version
					return flag;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Removes en entry from the cache</summary>
		/// <param name="key">Key of the entry to remove from the cache</param>
		/// <returns><c>true</c> if an entry with this key was found and removed; otherwise, <c>false</c>.</returns>
		public bool Remove(TKey key)
		{
			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
				var builder = new Dictionary<TKey, TValue>(original, original.Comparer);
				if (!builder.Remove(key))
				{ // the key does not exist
					return false;
				}
				var updated = builder.ToFrozenDictionary();

#pragma warning disable 420
				if (ReferenceEquals(Interlocked.CompareExchange(ref m_root, updated, original), original))
#pragma warning restore 420
				{ // we won the race
					return true;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Removes an entry from the cache, only if it had the expected value</summary>
		/// <param name="key">Key of the entry to remove</param>
		/// <param name="expectedValue">Value that the entry is expected to have</param>
		/// <param name="valueComparer">Optional value comparer</param>
		/// <returns><c>true</c> if an entry with this key and value was found and removed; otherwise, <c>false</c>.</returns>
		public bool TryRemove(TKey key, TValue expectedValue, IEqualityComparer<TValue>? valueComparer = null)
		{
			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;

				if (!original.TryGetValue(key, out var previous))
				{ // does not exist
					return false;
				}

				valueComparer ??= EqualityComparer<TValue>.Default;
				if (!valueComparer.Equals(previous, expectedValue))
				{ // value does not match expected
					return false;
				}

				// we copy and then remove
				var builder = new Dictionary<TKey, TValue>(original, original.Comparer);
				builder.Remove(key);
				var updated = builder.ToFrozenDictionary();

#pragma warning disable 420
				if (ReferenceEquals(Interlocked.CompareExchange(ref m_root, updated, original), original))
#pragma warning restore 420
				{ // La nouvelle version du cache ne contient plus la clé
					return true;
				}

				wait.SpinOnce();
			}
		}

		/// <inheritdoc />
		/// <exception cref="NotSupportedException">This operation is not supported.</exception>
		int ICache<TKey, TValue>.Cleanup(Func<TKey, TValue, bool> predicate)
		{
			throw new NotSupportedException();
		}

		/// <summary>Removes all items from the cache.</summary>
		public void Clear()
		{
			var empty = FrozenDictionary<TKey, TValue>.Empty;

			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
#pragma warning disable 420
				if (ReferenceEquals(Interlocked.CompareExchange(ref m_root, empty, original), original))
#pragma warning restore 420
				{
					return;
				}
				wait.SpinOnce();
			}
		}

		/// <summary>Returns an enumerator that will list all the entries in the cache</summary>
		/// <returns>The enumerator will capture a snapshot of the cache. Any changes made after this call returns will not be observed by the enumerator.</returns>
		public FrozenDictionary<TKey, TValue>.Enumerator GetEnumerator()
		{
			return m_root.GetEnumerator();
		}

		/// <inheritdoc />
		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return m_root.GetEnumerator();
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <inheritdoc />
		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			SetItem(item.Key, item.Value);
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return m_root.TryGetValue(item.Key, out var value) && m_valueComparer.Equals(value, item.Value);
		}

		/// <inheritdoc />
		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<TKey, TValue>>)m_root).CopyTo(array, arrayIndex);
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			return TryRemove(item.Key, item.Value);
		}

		/// <summary>Returns a list with all the entries in the cache</summary>
		public List<KeyValuePair<TKey, TValue>> ToList()
		{
			var list = new List<KeyValuePair<TKey, TValue>>(m_root.Count);
			list.AddRange(m_root);
			return list;
		}

		/// <summary>Returns an array with all the entries in the cache</summary>
		public KeyValuePair<TKey, TValue>[] ToArray()
		{
			var root = m_root;
			var array = new KeyValuePair<TKey, TValue>[root.Count];
			int i = 0;
			foreach (var kvp in root)
			{
				array[i] = kvp;
			}
			Contract.Debug.Ensures(i == root.Count);
			return array;
		}

	}

}
