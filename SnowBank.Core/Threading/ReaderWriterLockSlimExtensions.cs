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

namespace SnowBank.Threading
{

	/// <summary>Extensions methods for working with <see cref="ReaderWriterLockSlim"/></summary>
	public static class ReaderWriterLockSlimExtensions
	{

		/// <summary>Takes a disposable read lock</summary>
		/// <param name="self">Lock that should be used, or <c>null</c></param>
		/// <returns>Disposable token that will release the lock when <see cref="ReadLockDisposable.Dispose"/> is called</returns>
		/// <remarks>If <paramref name="self"/> is <c>null</c>, and empty "no-op" token will be returned instead</remarks>
		/// <example><code>using(lock.GetReadLock()) { /* ... read internal state ... */ }</code></example>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadLockDisposable GetReadLock(this ReaderWriterLockSlim? self)
		{
			self?.EnterReadLock();
			return new ReadLockDisposable(self);
		}

		/// <summary>Takes a disposable upgradable read lock</summary>
		/// <param name="self">Lock that should be used, or <c>null</c></param>
		/// <returns>Disposable token that will release the lock when <see cref="ReadLockDisposable.Dispose"/> is called</returns>
		/// <remarks>If <paramref name="self"/> is <c>null</c>, and empty "no-op" token will be returned instead</remarks>
		/// <example><code>using(lock.GetUpgradableReadLock()) { /* ... read internal state ... */ }</code></example>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static UpgradeableReadLockDisposable GetUpgradableReadLock(this ReaderWriterLockSlim? self)
		{
			self?.EnterUpgradeableReadLock();
			return new UpgradeableReadLockDisposable(self);
		}

		/// <summary>Takes a disposable write lock</summary>
		/// <param name="self">Lock that should be used, or <c>null</c></param>
		/// <returns>Disposable token that will release the lock when <see cref="ReadLockDisposable.Dispose"/> is called</returns>
		/// <remarks>If <paramref name="self"/> is <c>null</c>, and empty "no-op" token will be returned instead</remarks>
		/// <example><code>using(lock.GetWriteLock()) { /* ... modify internal state ... */ }</code></example>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static WriteLockDisposable GetWriteLock(this ReaderWriterLockSlim? self)
		{
			self?.EnterWriteLock();
			return new WriteLockDisposable(self);
		}

		/// <summary>Token that represents a read lock on a <see cref="ReaderWriterLockSlim"/></summary>
		/// <remarks>Disposing this instance will automatically release the lock.</remarks>
		public readonly struct ReadLockDisposable : IDisposable
		{
			private readonly ReaderWriterLockSlim? m_lock;

			/// <summary>Wraps a read-lock taken on a <see cref="ReaderWriterLockSlim"/></summary>
			/// <param name="rwLock"></param>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadLockDisposable(ReaderWriterLockSlim? rwLock)
			{
				m_lock = rwLock;
			}

			/// <summary>Releases the lock</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				m_lock?.ExitReadLock();
			}
		}

		/// <summary>Token that represents an upgradable read lock on a <see cref="ReaderWriterLockSlim"/></summary>
		/// <remarks>Disposing this instance will automatically release the lock.</remarks>
		public readonly struct UpgradeableReadLockDisposable : IDisposable
		{
			private readonly ReaderWriterLockSlim? m_lock;

			/// <summary>Wraps an upgradable read-lock taken on a <see cref="ReaderWriterLockSlim"/></summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public UpgradeableReadLockDisposable(ReaderWriterLockSlim? rwLock)
			{
				m_lock = rwLock;
			}

			/// <summary>Releases the lock</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				m_lock?.ExitUpgradeableReadLock();
			}
		}

		/// <summary>Token that represents a write lock on a <see cref="ReaderWriterLockSlim"/></summary>
		/// <remarks>Disposing this instance will automatically release the lock.</remarks>
		public readonly struct WriteLockDisposable : IDisposable
		{
			private readonly ReaderWriterLockSlim? m_lock;

			/// <summary>Wraps a write-lock taken on a <see cref="ReaderWriterLockSlim"/></summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public WriteLockDisposable(ReaderWriterLockSlim? rwLock)
			{
				m_lock = rwLock;
			}

			/// <summary>Releases the lock</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{
				m_lock?.ExitWriteLock();
			}
		}

	}

}
