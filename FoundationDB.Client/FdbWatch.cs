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
	using FoundationDB.Client.Native;

	/// <summary>Watch that triggers when a watched key has changed in the database</summary>
	[DebuggerDisplay("Status={Future.Task.Status}, Key={Key}")]
	[PublicAPI]
	public sealed class FdbWatch : IDisposable
	{

		/// <summary>Creates a <see cref="FdbWatch"/></summary>
		public FdbWatch(FdbFuture<Slice> future, Slice key)
		{
			Contract.Debug.Requires(future != null);
			this.Future = future;
			this.Key = key;
			//REVIEW: should we include a timestamp of when the watch was started, in order to get an estimate of the elapsed time?
		}

		private readonly FdbFuture<Slice> Future;

		/// <summary>Key that is being watched</summary>
		public readonly Slice Key;

		/// <summary><see langword="true"/> if the watch is still active, or <see langword="false"/> if it fired or was cancelled</summary>
		public bool IsAlive => !this.Future.Task.IsCompleted;

		/// <summary>Task that will complete when the watch fires, or is cancelled. It will return the watched key, or an exception.</summary>
		public Task<Slice> Task => this.Future.Task;

		private void EnsureNotDisposed()
		{
			if (this.Future.HasFlag(FdbFuture.Flags.DISPOSED))
			{
				throw ThrowHelper.ObjectDisposedException("Cannot await a watch that has already been disposed");
			}
		}

		/// <summary>Returns an awaiter for the Watch</summary>
		public TaskAwaiter<Slice> GetAwaiter()
		{
			//note: this is to make "await" work directly on the FdbWatch instance, without needing to do "await watch.Task"
			EnsureNotDisposed();
			return this.Future.Task.GetAwaiter();
		}

		/// <summary>Gets a <see cref="Task{Slice}"/> that will complete when this <see cref="FdbWatch">watch</see> fires or when the specified <see cref="T:System.Threading.CancellationToken" /> has cancellation requested.</summary>
		/// <param name="ct">The <see cref="T:System.Threading.CancellationToken" /> to monitor for a cancellation request.</param>
		/// <returns>Task that completes successfully when the watch fires, or fails if the cancellation token is triggered, or the parent <see cref="IFdbDatabase"/> instance is disposed.</returns>
		public async Task WaitAsync(CancellationToken ct)
		{
			EnsureNotDisposed();
			try
			{
				await this.Future.Task.WaitAsync(ct).ConfigureAwait(false);
			}
			catch (Exception)
			{
				this.Cancel(); // does nothing if already fired/cancelled!
				throw;
			}
		}

		/// <summary>Gets a <see cref="Task{Slice}"/> that will complete when this <see cref="FdbWatch">watch</see> fires, when the specified timeout expires, or when the specified <see cref="T:System.Threading.CancellationToken" /> has cancellation requested.</summary>
		/// <param name="timeout">The timeout after which the <see cref="FdbWatch" /> should be cancelled if it hasn't otherwise fired.</param>
		/// <param name="ct">The <see cref="T:System.Threading.CancellationToken" /> to monitor for a cancellation request.</param>
		/// <returns>Task that returns <see langword="true"/> if the watch as fired, or <see langword="false"/> if the timeout has expired.</returns>
		public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
		{
			EnsureNotDisposed();
			try
			{
				await this.Future.Task.WaitAsync(timeout, ct).ConfigureAwait(false);
				return true;
			}
			catch (TimeoutException)
			{
				return false;
			}
			catch (Exception)
			{
				this.Cancel(); // does nothing if already fired/cancelled!
				throw;
			}
		}

		/// <summary>Cancels the watch.</summary>
		/// <remarks>
		/// <para>It will immediately stop monitoring the key.</para>
		/// <para>Has no effect if the watch has already fired</para>
		/// </remarks>
		public void Cancel()
		{
			this.Future.Cancel();
		}

		/// <summary>Dispose the resources allocated by the watch.</summary>
		/// <summary>Invoked when the watch is disposed, so that its owner can forget it</summary>
		internal Action<FdbWatch>? OnDisposed;

		public void Dispose()
		{
			Interlocked.Exchange(ref this.OnDisposed, null)?.Invoke(this);
			this.Future.Dispose();
		}

		/// <inheritdoc />
		public override string ToString() => $"Watch({FdbKey.Dump(this.Key)})";

	}

}
