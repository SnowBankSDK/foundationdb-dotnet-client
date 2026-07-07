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

namespace SnowBank.Testing
{

	/// <summary>Generates parasitic background load for the duration of a <c>using</c> scope, so a test can run a
	/// specific step under controlled starvation instead of requiring machine-wide load generators.</summary>
	/// <remarks>
	/// <para>Load-dependent races (e.g. a background task's completion window being outrun by the test body) often only
	/// reproduce when the CPU is saturated. Saturating the whole machine works, but makes it unusable for everything else,
	/// including analyzing the results between runs. This helper scopes the saturation to exactly the test step that needs
	/// it.</para>
	/// <para>Tests deriving from <see cref="SimpleTest"/> should use <see cref="SimpleTest.UseParasiticCpuLoad"/> instead
	/// of calling this class directly: the wrapper enforces test EXCLUSIVITY (a parasitic load perturbs every test running
	/// concurrently with it) and guarantees the worker threads are stopped when the test completes, even if the scope
	/// leaks.</para>
	/// <para>Safety: worker threads are background threads, honor the provided cancellation token, and self-expire after
	/// <c>maxDuration</c> even if the scope is never disposed, so a hung test cannot leave the machine pegged.</para>
	/// </remarks>
	[PublicAPI]
	public static class ParasiticLoad
	{

		/// <summary>Starts <paramref name="workers"/> dedicated CPU-spinning threads until the returned scope is disposed.</summary>
		/// <param name="workers">Number of spinner threads (e.g. <c>Environment.ProcessorCount / 2</c> for ~50% pressure on an idle machine)</param>
		/// <param name="duty">Fraction of each ~20 ms window spent spinning (1.0 = continuous spin, 0.5 = half load per worker)</param>
		/// <param name="maxDuration">Hard self-expiry even if the scope is never disposed (default 60 s)</param>
		/// <param name="ct">Token that stops the load early if the test itself is cancelled</param>
		public static IDisposable Cpu(int workers, double duty = 1.0, TimeSpan? maxDuration = null, CancellationToken ct = default)
		{
			if (workers <= 0) throw new ArgumentOutOfRangeException(nameof(workers), workers, "At least one worker thread is required.");
			if (duty is < 0.0 or > 1.0) throw new ArgumentOutOfRangeException(nameof(duty), duty, "The duty cycle must be between 0.0 and 1.0.");
			return new CpuLoadScope(workers, duty, maxDuration ?? TimeSpan.FromSeconds(60), ct);
		}

		private sealed class CpuLoadScope : IDisposable
		{

			private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(20);

			private CancellationTokenSource Cts { get; }

			private Thread[] Workers { get; }

			public CpuLoadScope(int workers, double duty, TimeSpan maxDuration, CancellationToken ct)
			{
				this.Cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				this.Cts.CancelAfter(maxDuration);
				this.Workers = new Thread[workers];
				for (int i = 0; i < workers; i++)
				{
					var thread = new Thread(() => Spin(duty, this.Cts.Token))
					{
						IsBackground = true, // never block process exit
						Name = $"ParasiticLoad-{i}",
					};
					this.Workers[i] = thread;
					thread.Start();
				}
			}

			private static void Spin(double duty, CancellationToken ct)
			{
				// note: TimeSpan.FromTicks instead of 'duty * Window': the TimeSpan multiply operator does not exist on net472
				var spinPerWindow = TimeSpan.FromTicks((long) (Window.Ticks * duty));
				var sleepPerWindow = Window - spinPerWindow;
				var sw = new Stopwatch(); // instance-based timing: works on every target framework
				while (!ct.IsCancellationRequested)
				{
					sw.Restart();
					while (sw.Elapsed < spinPerWindow)
					{
						if (ct.IsCancellationRequested) return;
						// busy spin: this is the load
					}
					if (sleepPerWindow > TimeSpan.Zero)
					{
						Thread.Sleep(sleepPerWindow);
					}
				}
			}

			private int m_disposed;

			public void Dispose()
			{
				// idempotent: the test framework force-disposes leftover scopes at teardown, including ones the
				// test body already closed via its own `using`
				if (Interlocked.Exchange(ref m_disposed, 1) != 0)
				{
					return;
				}

				this.Cts.Cancel();
				foreach (var thread in this.Workers)
				{
					thread.Join(TimeSpan.FromSeconds(1));
				}
				this.Cts.Dispose();
			}

		}

	}

}
