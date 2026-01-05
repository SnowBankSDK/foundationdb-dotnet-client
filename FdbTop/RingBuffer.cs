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

namespace FdbTop
{

	internal sealed class RingBuffer<T> : IReadOnlyCollection<T>
	{
		private readonly Queue<T> m_store;
		private int m_size;

		public RingBuffer(int capacity)
		{
			Contract.Positive(capacity);

			m_store = new Queue<T>(capacity);
			m_size = capacity;
		}

		public int Count => m_store.Count;

		public int Capacity => m_size;

		public void Clear()
		{
			m_store.Clear();
		}

		public void Resize(int newCapacity)
		{
			Contract.Positive(newCapacity);

			var store = m_store;
			if (newCapacity < store.Count)
			{
				while (store.Count > newCapacity) store.Dequeue();
				store.TrimExcess();
			}
			m_size = newCapacity;
		}

		public void Enqueue(T item)
		{
			var store = m_store;
			if (store.Count >= m_size) store.Dequeue();
			store.Enqueue(item);
		}

		public T Dequeue()
		{
			return m_store.Dequeue();
		}

		public bool TryDequeue([MaybeNullWhen(false)] out T item)
		{
			var store = m_store;
			if (store.Count == 0)
			{
				item = default;
				return false;
			}
			item = store.Dequeue();
			return true;
		}

		public T[] ToArray()
		{
			return m_store.ToArray();
		}

		public void ForEach(Action<T> handler)
		{
			foreach (var item in m_store)
			{
				handler(item);
			}
		}

		public TResult Aggregate<TResult>(TResult seed, Func<T, TResult, TResult> aggregate)
		{
			TResult value = seed;
			foreach (var item in m_store)
			{
				value = aggregate(item, value);
			}
			return value;
		}

		public TResult Aggregate<TState, TResult>(Func<TState> init, Func<T, TState, TState> aggregate, Func<TState, TResult> finish)
		{
			TState state = init();
			foreach (var item in m_store)
			{
				state = aggregate(item, state);
			}
			return finish(state);
		}

		public Queue<T>.Enumerator GetEnumerator()
		{
			return m_store.GetEnumerator();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

	}

}
