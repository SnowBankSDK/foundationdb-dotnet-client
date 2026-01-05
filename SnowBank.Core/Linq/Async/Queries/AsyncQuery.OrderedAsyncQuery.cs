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

namespace SnowBank.Linq
{
	using System.ComponentModel;
	using SnowBank.Buffers;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Represent an async sequence that returns its elements according to a specific sort order</summary>
		/// <typeparam name="TSource">Type of the elements of the sequence</typeparam>
		internal class OrderedAsyncQuery<TSource> : IOrderedAsyncQuery<TSource>, IAsyncEnumerable<TSource>
		{
			// If an instance of the base <TSource> class is constructed, it will sort by the items themselves (using a Comparer<TSource>)
			// If an instance of the derived <TSource, TKey> class is constructed, then it will sort the key extracted from each item (sing a Comparer<TKey>)

			protected readonly IAsyncQuery<TSource> m_source;
			private readonly IComparer<TSource>? m_comparer; // null if comparing using keys
			protected readonly bool m_descending;
			protected readonly OrderedAsyncQuery<TSource>? m_parent;// null if primary sort key

			public OrderedAsyncQuery(IAsyncQuery<TSource> source, IComparer<TSource>? comparer, bool descending, OrderedAsyncQuery<TSource>? parent)
			{
				Contract.Debug.Requires(source != null);

				m_source = source;
				m_descending = descending;
				m_comparer = comparer;
				m_parent = parent;
			}

			protected OrderedAsyncQuery(IAsyncQuery<TSource> source, bool descending, OrderedAsyncQuery<TSource>? parent)
			{
				Contract.Debug.Requires(source != null);

				m_source = source;
				m_descending = descending;
				m_parent = parent;
			}

			internal virtual SequenceSorter<TSource> GetEnumerableSorter(SequenceSorter<TSource>? next)
			{
				var sorter = new SequenceByElementSorter<TSource>(m_comparer ?? Comparer<TSource>.Default, m_descending, next);
				return m_parent == null ? sorter : m_parent.GetEnumerableSorter(sorter);
			}

			/// <inheritdoc />
			public CancellationToken Cancellation => m_source.Cancellation;

			[MustDisposeResource]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken ct)
				=> GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);

			/// <inheritdoc />
			[MustDisposeResource]
			public IAsyncEnumerator<TSource> GetAsyncEnumerator(AsyncIterationHint mode)
			{
				var ct = m_source.Cancellation;
				ct.ThrowIfCancellationRequested();
				var sorter = GetEnumerableSorter(null);
				var enumerator = default(IAsyncEnumerator<TSource>);
				try
				{
					enumerator = m_source.GetAsyncEnumerator(mode);
					return new OrderedAsyncEnumerator<TSource>(enumerator, sorter);
				}
				catch (Exception)
				{
					//BUGBUG: we are forced to block on DisposeAsync() :(
					enumerator?.DisposeAsync().GetAwaiter().GetResult();
					throw;
				}
			}

			/// <inheritdoc />
			public IOrderedAsyncQuery<TSource> CreateOrderedEnumerable<TKey>(Func<TSource, TKey> keySelector, IComparer<TKey>? comparer, bool descending)
			{
				Contract.NotNull(keySelector);

				return new OrderedAsyncQuery<TSource, TKey>(this, keySelector, comparer, descending, this);
			}

		}

		/// <summary>Represent an async sequence that returns its elements according to a specific sort order</summary>
		/// <typeparam name="TSource">Type of the elements of the sequence</typeparam>
		/// <typeparam name="TKey">Type of the keys used to sort the elements</typeparam>
		internal sealed class OrderedAsyncQuery<TSource, TKey> : OrderedAsyncQuery<TSource>
		{
			private readonly Func<TSource, TKey> m_keySelector;
			private readonly IComparer<TKey> m_keyComparer;

			public OrderedAsyncQuery(IAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer, bool descending, OrderedAsyncQuery<TSource>? parent)
				: base(source, descending, parent)
			{
				Contract.Debug.Requires(keySelector != null);
				m_keySelector = keySelector;
				m_keyComparer = comparer ?? Comparer<TKey>.Default;
			}

			/// <inheritdoc />
			internal override SequenceSorter<TSource> GetEnumerableSorter(SequenceSorter<TSource>? next)
			{
				var sorter = new SequenceByKeySorter<TSource, TKey>(m_keySelector, m_keyComparer, m_descending, next);
				return m_parent == null ? sorter : m_parent.GetEnumerableSorter(sorter);
			}

		}

		/// <summary>Iterator that will sort all the items produced by an inner iterator, before outputting the results all at once</summary>
		internal sealed class OrderedAsyncEnumerator<TSource> : IAsyncEnumerator<TSource>
		{
			// This iterator must first before EVERY item of the source in memory, before being able to sort them.
			// The first MoveNext() will return only once the inner sequence has finished (successfully), which can take some time!
			// Ordering is done in-memory using QuickSort

			private readonly IAsyncEnumerator<TSource> m_inner;
			private readonly SequenceSorter<TSource> m_sorter;

			private ReadOnlyMemory<TSource> m_items;
			private int[]? m_map;
			private int m_offset;
			private TSource? m_current;

			public OrderedAsyncEnumerator(IAsyncEnumerator<TSource> enumerator, SequenceSorter<TSource> sorter)
			{
				Contract.Debug.Requires(enumerator != null && sorter != null);
				m_inner = enumerator;
				m_sorter = sorter;
			}

			private async ValueTask<bool> ReadAllThenSort()
			{
				if (m_offset == -1) return false; // already EOF or Disposed

				// first we need to spool everything from the inner iterator into memory
				var buffer = new Buffer<TSource>();

				var inner = m_inner;
				if (inner is AsyncLinqIterator<TSource> iterator)
				{
					await iterator.ExecuteAsync(buffer, (b, x) => b.Add(x)).ConfigureAwait(false);
				}
				else
				{
					while (await inner.MoveNextAsync().ConfigureAwait(false))
					{
						buffer.Add(inner.Current);
					}
				}

				if (buffer.Count == 0)
				{ // the list was empty!
					Completed();
					return false;
				}

				// then we need to sort everything...
				m_items = buffer.ToMemory();
				m_map = new int[m_items.Length];
				m_sorter.Sort(m_items, m_map);

				// and only then we can start outputting the first value
				// (after that, all MoveNext operations will be non-async)
				Publish(0);
				return true;
			}

			/// <inheritdoc />
			public ValueTask<bool> MoveNextAsync()
			{
				// First call will be slow (and async), but the rest of the calls will use the results already sorted in memory, and should be as fast as possible!

				if (m_map == null)
				{
					return ReadAllThenSort();
				}

				int pos = checked(m_offset + 1);
				if (pos < m_map.Length)
				{
					Publish(pos);
					return new ValueTask<bool>(true);
				}
				else
				{
					Completed();
					return new ValueTask<bool>(false);
				}
			}

			private void Publish(int offset)
			{
				Contract.Debug.Requires(m_map != null && offset >= 0 && offset < m_map.Length);
				m_current = m_items.Span[m_map[offset]];
				m_offset = offset;
			}

			private void Completed()
			{
				m_current = default;
				m_offset = -1;
				m_items = default;
				m_map = null;

			}

			/// <inheritdoc />
			public TSource Current => m_current!;

			/// <inheritdoc />
			public ValueTask DisposeAsync()
			{
				Completed();
				return default;
			}

		}

	}

}
