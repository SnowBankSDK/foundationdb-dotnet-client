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

namespace SnowBank.Linq.Iterators
{
	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	public abstract class Iterator<TResult> : IEnumerable<TResult>, IEnumerator<TResult>
	{
		//REVIEW: we could need an IAsyncIterator<T> interface that holds all the Select(),Where(),Take(),... so that it can be used by AsyncEnumerable to either call them directly (if the query supports it) or use a generic implementation
		// => this would be implemented by AsyncIterator<T> as well as FdbRangeQuery<T> (and ony other 'self optimizing' class)

		private const int STATE_SEQ = 0;
		private const int STATE_INIT = 1;
		private const int STATE_ITERATING = 2;
		private const int STATE_COMPLETED = 3;
		private const int STATE_DISPOSED = -1;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		protected TResult m_current;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		protected int m_state;

		#region IEnumerable<TResult>...

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public IEnumerator<TResult> GetEnumerator()
		{
			// reuse the same instance the first time
			if (Interlocked.CompareExchange(ref m_state, STATE_INIT, STATE_SEQ) == STATE_SEQ)
			{
				return this;
			}
			// create a new one
			var iter = Clone();
			Volatile.Write(ref iter.m_state, STATE_INIT);
			return iter;
		}

		protected abstract Iterator<TResult> Clone();

		#endregion

		#region IEnumerator<TResult>...

		void System.Collections.IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		object System.Collections.IEnumerator.Current => this.Current!;

		public TResult Current
		{
			get
			{
				if (Volatile.Read(ref m_state) != STATE_ITERATING) ThrowInvalidState();
				return m_current;
			}
		}

		public bool MoveNext()
		{
			var state = Volatile.Read(ref m_state);

			if (state == STATE_COMPLETED)
			{
				return false;
			}
			if (state != STATE_INIT && state != STATE_ITERATING)
			{
				ThrowInvalidState();
				return false;
			}

			try
			{
				if (state == STATE_INIT)
				{
					if (!OnFirst())
					{ // did not start at all ?
						return Completed();
					}

					if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_INIT) != STATE_INIT)
					{ // something happened while we where starting ?
						return false;
					}
				}

				return OnNext();
			}
			catch (Exception)
			{
				MarkAsFailed();
				throw;
			}
		}

		#endregion

		#region Iterator Impl...

		protected abstract bool OnFirst();

		protected abstract bool OnNext();

		protected bool Publish(TResult current)
		{
			if (Volatile.Read(ref m_state) == STATE_ITERATING)
			{
				m_current = current;
				return true;
			}
			return false;
		}

		protected bool Completed()
		{
			if (Volatile.Read(ref m_state) == STATE_INIT)
			{ // nothing should have been done by the iterator..
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
			}
			else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
			{ // the iterator has done at least something, so we can clean it up
				this.Cleanup();
			}
			return false;
		}

		/// <summary>Mark the current iterator as failed, and clean up the state</summary>
		protected void MarkAsFailed()
		{
			//TODO: store the state "failed" somewhere?
			Dispose();
		}

		protected void ThrowInvalidState()
		{
			switch (Volatile.Read(ref m_state))
			{
				case STATE_SEQ:
					throw new InvalidOperationException("The async iterator should have been initialized with a call to GetEnumerator()");

				case STATE_ITERATING:
					break;

				case STATE_DISPOSED:
					throw new ObjectDisposedException(null, "The async iterator has already been closed");

				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

		protected abstract void Cleanup();

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					Cleanup();
				}
				finally
				{
					m_current = default!;
				}
			}
		}

		#endregion

	}

}
