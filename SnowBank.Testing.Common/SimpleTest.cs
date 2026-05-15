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
	using System.Buffers;
	using System.ComponentModel;
	using System.IO;
	using System.Net;
	using System.Net.NetworkInformation;
	using System.Reflection;
	using System.Runtime;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Runtime.InteropServices;
	using System.Xml;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NodaTime;
	using SnowBank.Data.Tuples;
	using SnowBank.Numerics;
	using SnowBank.Reactive.Disposables;
	using SnowBank.Runtime.Converters;
	using ILogger = Microsoft.Extensions.Logging.ILogger;

	internal static class SimpleTestContractFailureInterceptor
	{

		static SimpleTestContractFailureInterceptor()
		{
			Contract.ContractFailed += ((sender, args) =>
			{
				var st = new StackTrace(skipFrames: 2, fNeedFileInfo: true);
				SimpleTest.Log($"#!# ContractFailed: {args.FailureKind}, condition=\"{args.Condition}\", message=\"{args.Message}\"{Environment.NewLine}{CleanupStackTrace(st)}");
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			});
		}

		public static void InterceptContractFailures()
		{
			// this is just here so that the static ctor has a chance to run
		}

		public static string CleanupStackTrace(StackTrace st)
		{
			var sb = new StringBuilder();
			int skipped = 0;
			foreach (var line in st.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				if (line.Contains(" NUnit.Framework.", StringComparison.Ordinal)
				 || line.Contains(" System.Threading.")
				 || line.Contains(" System.Reflection.")
				 || line.Contains(" SnowBank.Diagnostics.Contracts.Contract.")
				 || line.Contains(" System.Delegate.")
				 || line.Contains(" InvokeStub_ActualValueDelegate")
				)
				{
					++skipped;
					continue;
				}

				if (skipped > 0)
				{
					sb.AppendLine($"   ... (omitted {skipped:N0})");
					skipped = 0;
				}

				string l = line;
				int p;

				if ((p = l.IndexOf(".<>c.", StringComparison.Ordinal)) > 0)
				{
					l = l[..p] + l[(p + 4)..];
				}

				if ((p = l.IndexOf(".<>c__DisplayClass", StringComparison.Ordinal)) > 0)
				{
					int q = l.IndexOf('.', p + ".<>c__DisplayClass".Length);
					if (p > 0) l = l[..p] + l[q..];
				}

				if ((p = l.IndexOf(">b__", StringComparison.Ordinal)) > 0)
				{
					int q = l.AsSpan(0, p).LastIndexOf(".<");
					int r = l.IndexOf('(');
					if (q > 0 && r > q)
					{
						int s = l.IndexOf(')', r);
						if (s > r)
						{
							l = l[..(q + 1)] + l[(q + 2)..p] + l[r..(s + 1)] + ".<lambda_" + l[(p + 4)..r] + ">" + l[(s + 1)..];
						}
					}
				}

				sb.AppendLine(l);
			}

			if (sb.Length >= Environment.NewLine.Length) sb.Length -= Environment.NewLine.Length;
			return sb.ToString();
		}

	}

	/// <summary>Base class for simple unit tests. Provides a set of useful services (logging, cancellation, async helpers, ...)</summary>
	[DebuggerNonUserCode]
	[PublicAPI]
	[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
	public abstract class SimpleTest
	{
		private long m_testStartTimestamp;
		private long m_testEndTimestamp;
		private Instant m_testStartInstant;
		private CancellationTokenSource? m_cts;

		/// <summary>Clock that is controller by the test infrastructure</summary>
		public IClock Clock { get; set; } = SystemClock.Instance;

		static SimpleTest()
		{
#if !NETFRAMEWORK
			//TODO: REVIEW: do we still need to do this hack in .NET 6+? It was required with early .NET Core apps
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

			// JIT warmup of a few common types
			RobustHistogram.Warmup();
			RobustBenchmark.Warmup();
			CrystalJson.Warmup();

			if (!GCSettings.IsServerGC)
			{
				WriteToLog("#########################################################################");
				WriteToLog("WARN: Server GC is *NOT* enabled! Performance may be slower than expected");
				WriteToLog("#########################################################################");
				WriteToLog("");
			}
		}

		/// <summary>Invoked before the first test</summary>
		[OneTimeSetUp][DebuggerNonUserCode]
		public static void BeforeEverything()
		{
			SimpleTestContractFailureInterceptor.InterceptContractFailures();
			WriteToLog(CultureInfo.InvariantCulture, $"### {TestContext.CurrentContext.Test.ClassName} [START] @ {DateTime.Now.TimeOfDay}");
		}

		/// <summary>Invoked after the last test</summary>
		[OneTimeTearDown][DebuggerNonUserCode]
		public static void AfterEverything()
		{
			WriteToLog(CultureInfo.InvariantCulture, $"### {TestContext.CurrentContext.Test.ClassName} [{TestContext.CurrentContext.Result.Outcome}] ({TestContext.CurrentContext.Result.FailCount} failed) @ {DateTime.Now.TimeOfDay}");
		}

		/// <summary>Code that will run before each test method in this test suite.</summary>
		/// <remarks>You can insert your own setup logic by overriding <see cref="OnBeforeEachTest"/></remarks>
		[SetUp]
		public void BeforeEachTest()
		{
			WriteToLog(CultureInfo.InvariantCulture, $"=>= {TestContext.CurrentContext.Test.FullName} @ {DateTime.Now.TimeOfDay}");
			m_cts = new();

			// first we measure the startup duration
			m_testEndTimestamp = 0;
			m_testStartTimestamp = GetTimestamp();
			try
			{
				OnBeforeEachTest();
			}
			catch (Exception e)
			{
				LogError($"### {TestContext.CurrentContext.Test.FullName} [BEFORE CRASH] => {e}");
				throw;
			}
			finally
			{
				// now we measure start time of the actual test execution
				m_testStartInstant = this.Clock.GetCurrentInstant();
				m_testStartTimestamp = GetTimestamp();
			}
		}

		/// <summary>Override this method to insert your own setup logic that should run before each test in this class.</summary>
		protected virtual void OnBeforeEachTest()
		{ }

		/// <summary>Remove test-framework related frames from a capture <see cref="StackTrace"/></summary>
		protected static string CleanupStackTrace(StackTrace st) => SimpleTestContractFailureInterceptor.CleanupStackTrace(st);

		private List<Action<TestContext>> FailedCallbacks { get; } = [ ];

		/// <summary>Code that should be executed whenever the test fails, in order to dump additional state that would be helpful for troubleshooting</summary>
		/// <param name="callback">Callback invoked if the test does not pass.</param>
		/// <returns>Token that can be disposed early to remove this callback before the end of the test.</returns>
		/// <remarks>
		/// <para>The callback will be invoked before the test <see cref="Cancellation"/> token fires</para>
		/// <para>If the token is not disposed, the callback will remain until the end of the method.</para>
		/// </remarks>
		protected IDisposable DumpStateOnFailure(Action<TestContext> callback)
		{
			lock (this.FailedCallbacks)
			{
				this.FailedCallbacks.Add(callback);
			}
			return Disposable.Create(() =>
			{
				lock (this.FailedCallbacks)
				{
					this.FailedCallbacks.Remove(callback);
				}
			});
		}

		/// <summary>Code that will run after each test method in this test suite.</summary>
		/// <remarks>You can insert your own cleanup logic by overriding <see cref="OnAfterEachTest"/></remarks>
		[TearDown]
		public void AfterEachTest()
		{
			var currentContext = TestContext.CurrentContext;
			try
			{
				m_testEndTimestamp = GetTimestamp();

				// handle failure callbacks here
				if (currentContext.Result.FailCount > 0)
				{
					Action<TestContext>[]? callbacks = null;
					lock (this.FailedCallbacks)
					{
						if (this.FailedCallbacks.Count > 0)
						{
							callbacks = this.FailedCallbacks.ToArray();
							this.FailedCallbacks.Clear();
						}
					}

					if (callbacks is not null)
					{
						foreach (var callback in callbacks)
						{
							callback(currentContext);
						}
					}
				}
				else
				{
					lock (this.FailedCallbacks)
					{
						this.FailedCallbacks.Clear();
					}
				}

				m_cts?.Cancel();

				// dispose any IDisposable services that were used during the execution
				if (this.CustomServices != null)
				{
					try
					{
						foreach (var provider in this.CustomServices)
						{
							//HACKHACK: this could block the thread!
							provider.DisposeAsync().GetAwaiter().GetResult();
						}
					}
					finally
					{
						this.CustomServices = null;
					}
				}

				OnAfterEachTest();
			}
			catch (Exception e)
			{
				LogError($"### {currentContext.Test.FullName} [AFTER CRASH] => {e}");
				throw;
			}
			finally
			{
				var elapsed = this.TestElapsed;
				WriteToLog(CultureInfo.InvariantCulture, $"=<= {currentContext.Result.Outcome} {currentContext.Test.Name}() in {elapsed.TotalMilliseconds:N1} ms ({currentContext.AssertCount} asserts, {currentContext.Result.FailCount} failed)");
				m_cts?.Dispose();
			}
		}

		/// <summary>Override this method to insert your own cleanup logic that should run after each test in this class.</summary>
		protected virtual void OnAfterEachTest()
		{ }

		/// <summary>Forces a Full GC, in order to force any pending finalizers to run.</summary>
		[DebuggerNonUserCode]
		protected static void FullGc()
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		#region Timeouts...

		/// <summary>Resets the <see cref="TestStartedAt"/> timestamp to the current time.</summary>
		public void ResetTimer()
		{
			m_testStartTimestamp = GetTimestamp();
		}

		/// <summary>Time elapsed since the start of this test</summary>
		/// <remarks>This will stop counting once the test has completed.</remarks>
		protected TimeSpan TestElapsed
			=> m_testEndTimestamp != 0 ? GetElapsedTime(m_testStartTimestamp, m_testEndTimestamp) : m_testStartTimestamp != 0 ? GetElapsedTime(m_testStartTimestamp) : TimeSpan.Zero;

		/// <summary>Timestamp of the start of this test</summary>
		protected Instant TestStartedAt => m_testStartInstant;

		/// <summary>Returns the time elapsed since the start of the test</summary>
		/// <param name="now">Current timestamp</param>
		/// <returns>Delay between the start of the test and <paramref name="now"/></returns>
		protected Duration ElapsedSinceTestStart(Instant now) => now - m_testStartInstant;

		/// <summary>Token that is linked with the lifetime of this test.</summary>
		/// <remarks>
		/// <para>This token will be cancelled whenever the test completes (successfully or not), or times out.</para>
		/// <para>Any async operation started inside the test <b>MUST</b> use this token; otherwise, test execution may not terminate properly.</para>
		/// <para>Most helper methods on this type will implicitly use this token, if they don't request one as argument.</para>
		/// </remarks>
		public CancellationToken Cancellation
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_cts?.Token ?? CancellationToken.None;
		}

		/// <summary>Configures the test to abort after the specified delay</summary>
		/// <remarks>The <see cref="Cancellation"/> token configured for this test will be cancelled when the delay expires (if the tests has not completed yet)</remarks>
		public void SetExecutionTimeout(TimeSpan delay)
		{
			var cts = m_cts ?? throw new InvalidOperationException("Cannot set execution delay outside of a test");
			Task.Delay(delay, cts.Token).ContinueWith((_) =>
			{
				Log($"### TEST TIMED OUT AFTER {delay} !!! ###");
				cts.Cancel();
			}, TaskContinuationOptions.NotOnCanceled);
		}

		/// <summary>Returns a hi-resolution timestamp, for measure execution time</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long GetTimestamp() => Stopwatch.GetTimestamp();

		/// <summary>Gets the elapsed time since the <paramref name="startingTimestamp"/> value retrieved using <see cref="GetTimestamp"/>.</summary>
		/// <param name="startingTimestamp">The timestamp marking the beginning of the time period.</param>
		/// <returns>A <see cref="TimeSpan"/> for the elapsed time between the starting timestamp and the time of this call.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan GetElapsedTime(long startingTimestamp)
			=> GetElapsedTime(startingTimestamp, GetTimestamp());

		/// <summary>Gets the elapsed time between two timestamps retrieved using <see cref="GetTimestamp"/>.</summary>
		/// <param name="startingTimestamp">The timestamp marking the beginning of the time period.</param>
		/// <param name="endingTimestamp">The timestamp marking the end of the time period.</param>
		/// <returns>A <see cref="TimeSpan"/> for the elapsed time between the starting and ending timestamps.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
#if NET8_0_OR_GREATER
			Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);
#else
			new TimeSpan((long) ((endingTimestamp - startingTimestamp) * s_tickFrequency));

		private static readonly double s_tickFrequency = (double) TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

		/// <summary>Measures the execution time of a test function</summary>
		/// <param name="handler">Function to invoke</param>
		/// <returns>Execution time</returns>
		public static TimeSpan Time(Action  handler)
		{
			long start = GetTimestamp();
			handler();
			return GetElapsedTime(start, GetTimestamp());
		}

		/// <summary>Measures the execution time of a test function</summary>
		/// <param name="handler">Function to invoke</param>
		/// <returns>Execution time</returns>
		public static async Task<TimeSpan> Time(Func<Task> handler)
		{
			long start = GetTimestamp();
			await handler().ConfigureAwait(false);
			return GetElapsedTime(start, GetTimestamp());
		}

		/// <summary>Measures the execution time of a test function</summary>
		/// <typeparam name="TResult">Type of the result</typeparam>
		/// <param name="handler">Function to invoke</param>
		/// <returns>Execution time</returns>
		/// <returns>Tuple with the function result, and the execution time</returns>
		public static (TResult Result, TimeSpan Elapsed) Time<TResult>(Func<TResult> handler)
		{
			long start = GetTimestamp();
			var result = handler();
			long end = GetTimestamp();
			return (result, GetElapsedTime(start, end));
		}

		/// <summary>Measures the execution time of a test function</summary>
		/// <typeparam name="TResult">Type of the result</typeparam>
		/// <param name="handler">Function to invoke</param>
		/// <returns>Execution time</returns>
		/// <returns>Tuple with the function result, and the execution time</returns>
		public static async Task<(TResult Result, TimeSpan Elapsed)> Time<TResult>(Func<Task<TResult>> handler)
		{
			long start = GetTimestamp();
			var result = await handler().ConfigureAwait(false);
			long end = GetTimestamp();
			return (result, GetElapsedTime(start, end));
		}

		/// <summary>Wait for the specified delay</summary>
		/// <remarks>
		/// <para>The delay will be interrupted if the test times out during the interval.</para>
		/// </remarks>
		[Obsolete("Warning: waiting for a fixed time delay in a test is not good practice!")]
		public Task Wait(int milliseconds)
		{
			return Wait(TimeSpan.FromMilliseconds(milliseconds));
		}

		/// <summary>Wait for the specified delay</summary>
		/// <remarks>
		/// <para>The delay will be interrupted if the test times out during the interval.</para>
		/// </remarks>
		[Obsolete("Warning: waiting for a fixed time delay in a test is not good practice!")]
		public async Task Wait(TimeSpan delay)
		{
			var start = this.Clock.GetCurrentInstant();
			try
			{
				await Task.Delay(delay, this.Cancellation).ConfigureAwait(false);
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted("Delay", delay.ToString(), success: true, null, start, end).ConfigureAwait(false);
			}
			catch (Exception)
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted("Delay", delay.ToString(), success: false, null, start, end).ConfigureAwait(false);
			}
		}

		/// <summary>Waits until the specified condition becomes satisfied, the specified timeout expires, or the test is aborted.</summary>
		/// <returns>Task that will return the elapsed time for the condition to be satisfied. Since this method uses polling, this will be an upper bound of the actual processing time!</returns>
		public Task<TimeSpan> WaitUntil([InstantHandle] Func<bool> condition, TimeSpan timeout, string message, TimeSpan? ticks = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			return WaitUntil(
				condition,
				timeout,
				(elapsed, error) =>
				{
					Assert.Fail($"Operation took too long to execute: {message}{Environment.NewLine}Condition: {conditionExpression}{Environment.NewLine}Elapsed: {elapsed}{(error != null ? ($"{Environment.NewLine}Exception: {error}") : null)}");
				},
				ticks,
				conditionExpression);
		}

		/// <summary>Waits until a computed result becomes equal to the specified value, the specified timeout expires, or the test is aborted.</summary>
		/// <returns>Task that will return the elapsed time for the condition to be satisfied. Since this method uses polling, this will be an upper bound of the actual processing time!</returns>
		public Task<TimeSpan> WaitUntilEqual<TValue>([InstantHandle] Func<TValue> condition, TValue comparand, TimeSpan timeout, string? message, TimeSpan? ticks = null, IEqualityComparer<TValue>? comparer = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			comparer ??= EqualityComparer<TValue>.Default;
			return WaitUntil(
				() => comparer.Equals(condition(), comparand),
				timeout,
				(elapsed, error) =>
				{
					Assert.That(condition(), Is.EqualTo(comparand), $"Operation took too long to execute: {message}{Environment.NewLine}Condition: {conditionExpression} == {comparand}{Environment.NewLine}Elapsed: {elapsed}{(error != null ? ($"{Environment.NewLine}Exception: {error}") : null)}");
				},
				ticks,
				conditionExpression
			);
		}

		/// <summary>Waits until the specified condition becomes satisfied, the specified timeout expires, or the test is aborted.</summary>
		/// <returns>Task that will return the elapsed time for the condition to be satisfied. Since this method uses polling, this will be an upper bound of the actual processing time!</returns>
		public async Task<TimeSpan> WaitUntil([InstantHandle] Func<bool> condition, TimeSpan timeout, Action<TimeSpan, Exception?>  onFail, TimeSpan? ticks = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			var ct = this.Cancellation;

			var max = ticks ?? TimeSpan.FromMilliseconds(250);
			if (max > timeout) max = TimeSpan.FromTicks(timeout.Ticks / 10);
			var delay = TimeSpan.FromMilliseconds(Math.Min(5, max.TotalMilliseconds));
			// heuristic to get a sensible value
			if (max <= TimeSpan.FromMilliseconds(50)) Assert.Fail("Ticks must be at least greater than 50ms!");

			var startInstant = this.Clock.GetCurrentInstant();
			long startTimestamp = GetTimestamp();
			long endTimestamp = 0;
			bool success = false;
			Exception? error = null;
			try
			{
				while (!ct.IsCancellationRequested)
				{
					try
					{
						if (condition())
						{
							endTimestamp = GetTimestamp();
							success = true;
							break;
						}
					}
					catch (Exception e)
					{
						endTimestamp = GetTimestamp();
						error = e;
						if (e is AssertionException) throw;
						onFail(GetElapsedTime(startTimestamp, endTimestamp), e);
						Assert.Fail($"Operation failed will polling expression '{conditionExpression}': {e}");
					}

					await Task.Delay(delay, this.Cancellation).ConfigureAwait(false);
					var elapsed = GetElapsedTime(startTimestamp);
					if (elapsed >= timeout)
					{
						onFail(elapsed, null);
					}

					delay += delay;
					if (delay > max)
					{
						delay = max;
					}
				}

				ct.ThrowIfCancellationRequested();
				return GetElapsedTime(startTimestamp, endTimestamp);
			}
			finally
			{
				var endInstant = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(WaitUntil), conditionExpression!, success, error, startInstant, endInstant).ConfigureAwait(false);
			}
		}

		/// <summary>Waits until the specified condition becomes satisfied, the specified timeout expires, or the test is aborted.</summary>
		/// <returns>Task that will return the elapsed time for the condition to be satisfied. Since this method uses polling, this will be an upper bound of the actual processing time!</returns>
		public async Task<TimeSpan> WaitUntil([InstantHandle] Func<Task<bool>> condition, TimeSpan timeout, string message, TimeSpan? ticks = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			var ct = this.Cancellation;

			var max = ticks ?? TimeSpan.FromMilliseconds(250);
			if (max > timeout) max = TimeSpan.FromTicks(timeout.Ticks / 10);
			var delay = TimeSpan.FromMilliseconds(Math.Min(5, max.TotalMilliseconds));
			// heuristic to get a sensible value
			if (max <= TimeSpan.FromMilliseconds(50)) Assert.Fail("Ticks must be at least greater than 50ms!");

			var startInstant = this.Clock.GetCurrentInstant();
			bool success = false;
			Exception? error = null;
			try
			{
				long startTimestamp = GetTimestamp();
				long endTimestamp = 0;
				while (!ct.IsCancellationRequested)
				{
					try
					{
						if (await condition().ConfigureAwait(false))
						{
							endTimestamp = GetTimestamp();
							break;
						}
					}
					catch (Exception e)
					{
						endTimestamp = GetTimestamp();
						error = e;
						if (e is AssertionException) throw;
						Assert.Fail($"Operation failed will polling expression '{conditionExpression}': {e}");
					}

					await Task.Delay(delay, this.Cancellation).ConfigureAwait(false);
					var elapsed = GetElapsedTime(startTimestamp);
					if (elapsed >= timeout)
					{
						Assert.Fail($"Operation took too long to execute: {message}{Environment.NewLine}Condition: {conditionExpression}{Environment.NewLine}Elapsed: {elapsed}");
					}

					delay += delay;
					if (delay > max) delay = max;
				}

				ct.ThrowIfCancellationRequested();
				success = true;
				return GetElapsedTime(startTimestamp, endTimestamp);
			}
			finally
			{
				var endInstant = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(WaitUntil), conditionExpression!, success, error, startInstant, endInstant).ConfigureAwait(false);
			}
		}

		/// <summary>Called whenever an <see cref="Await(Task,TimeSpan,string?,string?)"/> operation completes (successfully or not)</summary>
		protected virtual Task OnWaitOperationCompleted(string operation, string conditionExpression, bool success, Exception? error, Instant startedAt, Instant endedAt)
		{
			return Task.CompletedTask;
		}

		#endregion

		#region Async Stuff...

		/// <summary>Waits multiple tasks that should all complete within the specified time.</summary>
		/// <param name="tasks">The list of tasks that will be awaited.</param>
		/// <param name="timeout">The maximum allowed time for all the tasks to complete.</param>
		/// <param name="message">Optional error message</param>
		/// <param name="tasksExpression">Expression that generated the tasks (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if at least one task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the tasks in order for this safety feature to work! If any task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task WhenAll(IEnumerable<Task> tasks, TimeSpan timeout, string? message = null, [CallerArgumentExpression(nameof(tasks))] string? tasksExpression = null)
		{
			var ts = (tasks as Task[]) ?? tasks.ToArray();
			if (m_cts?.IsCancellationRequested ?? false) return Task.FromCanceled(m_cts.Token);
			if (ts.Length == 0) return Task.CompletedTask;
			if (ts.Length == 1 && ts[0].IsCompleted) return ts[0];
			return WaitForInternal(Task.WhenAll(ts), timeout, throwIfExpired: true, null, message, $"WhenAll({tasksExpression})");
		}

		/// <summary>Waits multiple tasks that should all complete within the specified time.</summary>
		/// <param name="tasks">The list of tasks that will be awaited.</param>
		/// <param name="timeout">The maximum allowed time for all the tasks to complete.</param>
		/// <param name="message">Optional error message</param>
		/// <param name="tasksExpression">Expression that generated the tasks (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if at least one task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the tasks in order for this safety feature to work! If any task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public async Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks, TimeSpan timeout, string? message = null, [CallerArgumentExpression(nameof(tasks))] string? tasksExpression = null)
		{
			var ts = (tasks as Task<TResult>[]) ?? tasks.ToArray();
			this.Cancellation.ThrowIfCancellationRequested();
			if (ts.Length == 0) return [ ];
			await WaitForInternal(Task.WhenAll(ts), timeout, throwIfExpired: true, null, message, $"WhenAll({tasksExpression})").ConfigureAwait(false);
			var res = new TResult[ts.Length];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = ts[i].Result;
			}
			return res;
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <param name="task">The task that will be awaited.</param>
		/// <param name="timeoutMs">The maximum allowed time (in milliseconds) for the task to complete.</param>
		/// <param name="message">Optional error message</param>
		/// <param name="taskExpression">Expression that generated the task (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task Await(Task task, int timeoutMs, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), message, taskExpression!);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <param name="task">The task that will be awaited.</param>
		/// <param name="timeoutMs">The maximum allowed time (in milliseconds) for the task to complete.</param>
		/// <param name="message">Optional error message</param>
		/// <param name="taskExpression">Expression that generated the task (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task Await(ValueTask task, int timeoutMs, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), message, taskExpression!);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <param name="task">The task that will be awaited.</param>
		/// <param name="timeout">The maximum allowed time for the task to complete.</param>
		/// <param name="message">Optional error message</param>
		/// <param name="taskExpression">Expression that generated the task (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task Await(Task task, TimeSpan timeout, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
			     : task.IsCompleted ? task
			     : WaitForInternal(task, timeout, throwIfExpired: true, null, message, taskExpression!);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task Await(ValueTask task, TimeSpan timeout, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, throwIfExpired: true, null, message, taskExpression!);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(Task<TResult> task, int timeoutMs, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), message, taskExpression);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(ValueTask<TResult> task, int timeoutMs, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), message, taskExpression);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(Task<TResult> task, TimeSpan timeout, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task
				: WaitForInternal(task, timeout, null, message, taskExpression!);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(Task<TResult> task, TimeSpan timeout, Action onTimeout, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task
				: WaitForInternal(task, timeout, onTimeout, message, taskExpression!);
		}

		/// <summary>Waits for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not be canceled, and could run indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(ValueTask<TResult> task, TimeSpan timeout, string? message = null, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, null, message, taskExpression!);
		}

		[StackTraceHidden]
		private async Task<bool> WaitForInternal(Task task, TimeSpan delay, bool throwIfExpired, Action? onTimeout, string? message, string taskExpression)
		{
			bool success = false;
			Exception? error = null;
			var start = this.Clock.GetCurrentInstant();

			try
			{
				if (!task.IsCompleted)
				{
					var ct = this.Cancellation;
					if (task != (await Task.WhenAny(task, Task.Delay(delay, ct)).ConfigureAwait(false)))
					{ // timeout!
						if (ct.IsCancellationRequested)
						{
							LogError("### Wait aborted due to test cancellation! ###");
							Assert.Fail(message != null
								? $"{message}. Test execution has been aborted because it took too long to execute!"
								: "Test execution has been aborted because it took too long to execute!"
							);
						}

						LogError("### Wait aborted due to timeout! ###");
						onTimeout?.Invoke();
						if (throwIfExpired)
						{
							Assert.Fail(message != null
								? $"{message}. Operation took more than {delay} to execute: {taskExpression}"
								: $"Operation took more than {delay} to execute: {taskExpression}"
							);
						}

						return false;
					}
				}

				if (task.Status != TaskStatus.RanToCompletion)
				{ // re-throw error
					var ex = task.Exception!.Unwrap();
					error = ex;
					Assert.Fail(message != null
						? $"{message}. Task '{taskExpression}' failed with following error: {ex}"
						: $"Task '{taskExpression}' failed with following error: {ex}"
					);
				}

				success = true;
				return true;
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(Await), taskExpression, success, error, start, end).ConfigureAwait(false);
			}
		}

		[StackTraceHidden]
		private async Task<TResult> WaitForInternal<TResult>(Task<TResult> task, TimeSpan delay, Action? onTimeout, string? message, string taskExpression)
		{
			bool? success = null;
			Exception? error = null;
			var start = this.Clock.GetCurrentInstant();
			try
			{
				if (!task.IsCompleted)
				{
					var ct = this.Cancellation;
					if (task != (await Task.WhenAny(task, Task.Delay(delay, ct)).ConfigureAwait(false)))
					{ // timeout!
						success = false;

						if (ct.IsCancellationRequested)
						{
							LogError("### Wait aborted due to test cancellation! ###");
							Assert.Fail(message != null
								? $"{message}. Test execution has been aborted because it took too long to execute!"
								: "Test execution has been aborted because it took too long to execute!"
							);
						}
						else
						{
							LogError("### Wait aborted due to timeout! ###");
							onTimeout?.Invoke();
							Assert.Fail(message != null
								? $"{message}. Operation took more than {delay} to execute: {taskExpression}"
								: $"Operation took more than {delay} to execute: {taskExpression}"
							);
						}
					}

					if (!task.IsCompleted)
					{
						Assert.Fail(message != null
							? $"{message}. Task did not complete in time ({task.Status})"
							: $"Task did not complete in time ({task.Status})"
						);
					}
				}

				// return result or throw error
				try
				{
					return await task.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					success = false;
					error = ex;
					Assert.Fail(message != null
						? $"{message}. Task '{taskExpression}' failed with following error: {ex}"
						: $"Task '{taskExpression}' failed with following error: {ex}"
					);
					throw null!;
				}
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(Await), taskExpression, success ?? true, error, start, end).ConfigureAwait(false);
			}
		}

		#endregion

		#region Files & Paths...

		/// <summary>Computes the path to the folder containing the specified assembly</summary>
		/// <exception cref="InvalidOperationException"></exception>
		protected static string GetAssemblyLocation(Assembly assembly)
		{
			string path = assembly.Location;
			if (string.IsNullOrEmpty(path)) throw new InvalidOperationException($"Failed to get location of assembly {assembly}");

			// Resharper sometimes returns @"file:///xxxxx" or @"file:\xxxx"
			if (path.StartsWith("file:\\", StringComparison.OrdinalIgnoreCase))
			{
				path = path[6..];
			}
			else if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
			{
				path = path[8..];
			}

			path = Path.GetDirectoryName(path) ?? ".";
			return path;
		}

		/// <summary>[TEST ONLY] Computes the path to a file, relative to the source file that is calling this method</summary>
		/// <param name="relativePath">Path, relative to the current source path, of the target file or folder</param>
		/// <param name="path"><b>DO NOT SPECIFY!</b>! The method relies on the fact that the compiler will pass the pass of the current file, which will be used to infer the path to the resource relative to it!</param>
		/// <remarks>Note: this method of computing the path is relatively safe when needing to read test resources (like samples, data sets, ...) but can break if a refactoring moves the files apart!</remarks>
		public string MapPathRelativeToCallerSourcePath(string relativePath, [CallerFilePath] string? path = null)
		{
			if (string.IsNullOrEmpty(relativePath))
			{
				relativePath = ".";
			}
			return Path.Combine(Path.GetDirectoryName(path)!, relativePath);
		}

		/// <summary>Computes the path to a temporary path or file that can safely be used by this test</summary>
		/// <param name="relative">If not null, name of the file or folder appended to the temporary path. By convention, should end with '/' if this is a folder, so that the intent is clearly visible.</param>
		/// <param name="clearFiles">If <see langword="true"/>, will clean the content of the folder (or the file)</param>
		/// <returns>Path to a temporary folder (or file). The folder will be created if required. The file will be removed if already present (from a previous test run).</returns>
		/// <example>
		/// GetTemporaryPath() => "c:\blah\workdir\testoutput\FooBarFacts"
		/// GetTemporaryPath("Zorglub") => "c:\blah\workdir\testoutput\FooBarFacts\Zorglub"
		/// GetTemporaryPath("Zorglub/") => "c:\blah\workdir\testoutput\FooBarFacts\Zorglub\"
		/// GetTemporaryPath("Frobs.zip") => "c:\blah\workdir\testoutput\FooBarFacts\Frobs.zip"
		/// </example>
		public string GetTemporaryPath(string? relative = null, bool clearFiles = false)
		{
			// We want to build: "{WORK_DIR}/{TEST_CLASS_NAME}[/sufix]"

			var context = TestContext.CurrentContext;
			string basePath = context != null! ? context.TestDirectory : Environment.CurrentDirectory;
			if (basePath.IndexOf(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) > 0 || basePath.IndexOf(@"\bin\Release", StringComparison.OrdinalIgnoreCase) > 0)
			{
				basePath = Path.Combine(basePath, "TestOutput");
			}

			// Grab the name of the current test

			string className = context?.Test.ClassName ?? GetType().Name;
			string? testName = context?.Test.MethodName;

			string path = Path.Combine(basePath, className.Replace(".", "_").Replace("`", "_"));
			if (!string.IsNullOrEmpty(testName))
			{
				path = Path.Combine(basePath, testName.Replace(".", "_").Replace("`", "_"));
			}

			if (!string.IsNullOrEmpty(relative))
			{
				path = Path.Combine(path, relative);
				if (path.Contains("/")) path = path.Replace("/", "\\");
			}
			else
			{
				path += "\\";
			}

			path = Path.GetFullPath(path);
#if DEBUG
			Log($"# TempPath(\"{relative}\") => {path}");
#endif

			if (path.EndsWith('\\'))
			{ // this is a folder, create it if required

				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}
				else if (clearFiles)
				{
					Assert.That(path, Does.StartWith(basePath), "*DANGER* Attempted to clear temp folder OUTSIDE the main test folder!");
					foreach(var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
					{
						entry.Delete();
					}
				}
			}
			else
			{ // this is a file, create the parent folder if required
				string containerFolder = Path.GetDirectoryName(path)!;
				if (!Directory.Exists(containerFolder))
				{
					Directory.CreateDirectory(containerFolder);
				}

				if (clearFiles && File.Exists(path))
				{
					File.Delete(path);
				}
			}

			return path;
		}

		/// <summary>Returns the FQDN of the running host (if possible)</summary>
		/// <returns>ex: "srv42.acme.local"</returns>
		public string GetCurrentFqdn()
		{
			string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
			string hostName = Dns.GetHostName();

			domainName = "." + domainName;
			if (!hostName.EndsWith(domainName, StringComparison.OrdinalIgnoreCase)) hostName += domainName;

			return hostName;
		}

		#endregion

		#region Logging...

		/// <summary>If <see langword="true"/>, we are running with an attached debugger that prefers logs to be written to the Trace/Debug output.</summary>
		protected static readonly bool AttachedToDebugger = Debugger.IsAttached;

		/// <summary>If <see langword="true"/>, we are running under Console Test Runner that prefers logs to be written to the Console output</summary>
		public static readonly bool MustOutputLogsOnConsole = DetectConsoleTestRunner();

		private static bool DetectConsoleTestRunner()
		{
			// TeamCity
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")))
			{
				return true;
			}

			string? host = Assembly.GetEntryAssembly()?.GetName().Name;
			return host == "TestDriven.NetCore.AdHoc" // TestDriven.NET
				|| host == "testhost";                // ReSharper Test Runner
		}

		[DebuggerNonUserCode]
		private static void WriteToLog(string? message, bool lineBreak = true)
		{
			if (MustOutputLogsOnConsole)
			{ // force output to the console
				if (lineBreak)
				{
					Console.Out.WriteLine(message);
				}
				else
				{
					Console.Out.Write(message);
				}
			}
			else if (AttachedToDebugger)
			{ // outputs to the Output console (visible while the test is running under a debugger)
				if (lineBreak)
				{
					Trace.WriteLine(message);
				}
				else
				{
					Trace.Write(message);
				}
			}
			else
			{ // output to stdout

				//note: before NUnit 3.6, the text had to be XML encoded, but this has been fixed since v3.6.0 (cf https://github.com/nunit/nunit/issues/1891)
				//message = message.Replace("&", "&amp;").Replace("<", "&lt;");

				if (lineBreak)
				{
					TestContext.Progress.WriteLine(message);
				}
				else
				{
					TestContext.Progress.Write(message);
				}
			}
		}

		[DebuggerNonUserCode]
		private static void WriteToLog(CultureInfo provider, [InterpolatedStringHandlerArgument(nameof(provider))] ref DefaultInterpolatedStringHandler message, bool lineBreak = true)
		{
			WriteToLog(ref message, lineBreak);
		}

		[DebuggerNonUserCode]
		private static void WriteToLog(ref DefaultInterpolatedStringHandler message, bool lineBreak = true)
		{
			if (MustOutputLogsOnConsole)
			{ // force output to the console
				if (lineBreak)
				{
#if NET10_0_OR_GREATER
					Console.Out.WriteLine(message.Text);
#else
					Console.Out.WriteLine(message.ToString());
#endif
				}
				else
				{
#if NET10_0_OR_GREATER
					Console.Out.Write(message.Text);
#else
					Console.Out.Write(message.ToString());
#endif
				}
			}
			else if (AttachedToDebugger)
			{ // outputs to the Output console (visible while the test is running under a debugger)
				if (lineBreak)
				{
					Trace.WriteLine(message.ToString());
				}
				else
				{
					Trace.Write(message.ToString());
				}
			}
			else
			{ // output to stdout
				if (lineBreak)
				{
#if NET10_0_OR_GREATER
					TestContext.Progress.WriteLine(message.Text);
#else
					TestContext.Progress.WriteLine(message.ToString());
#endif
				}
				else
				{
#if NET10_0_OR_GREATER
					TestContext.Progress.Write(message.Text);
#else
					TestContext.Progress.Write(message.ToString());
#endif
				}
			}
#if NET10_0_OR_GREATER
			message.Clear();
#endif
		}

		[DebuggerNonUserCode]
		private static void WriteToErrorLog(string? message)
		{
			if (MustOutputLogsOnConsole)
			{ // force output to the console
				Console.Error.WriteLine(message);
			}
			else if (AttachedToDebugger)
			{ // outputs to the Output console (visible while the test is running under a debugger)
				Trace.WriteLine("ERROR: " + message);
				TestContext.Error.WriteLine(message);
			}
			else
			{ // output to stderr
				TestContext.Error.WriteLine(message);
			}
		}

#if NET10_0_OR_GREATER
		[DebuggerNonUserCode]
		private static void WriteToErrorLog(ref DefaultInterpolatedStringHandler message)
		{
			if (MustOutputLogsOnConsole)
			{ // force output to the console
				Console.Error.WriteLine(message.Text);
			}
			else if (AttachedToDebugger)
			{ // outputs to the Output console (visible while the test is running under a debugger)
				TestContext.Error.WriteLine(message.Text);
				Trace.WriteLine($"ERROR: {message.Text}");
			}
			else
			{ // output to stderr
				TestContext.Error.WriteLine(message.Text);
			}
			message.Clear();
		}
#endif

		/// <summary>Writes a message to the output log, prefixed with the elapsed time since the start of the test.</summary>
		[DebuggerNonUserCode]
		protected void LogElapsed(string? text) => Log($"{this.TestElapsed} {text}");

		/// <summary>Writes a message to the output log, prefixed with the elapsed time since the start of the test.</summary>
		[DebuggerNonUserCode]
		protected void LogElapsed(ref DefaultInterpolatedStringHandler handler) => LogElapsed(handler.ToStringAndClear());

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Log(string? text) => WriteToLog(text);

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(ref DefaultInterpolatedStringHandler handler) => WriteToLog(ref handler);

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(StringBuilder? text) => WriteToLog($"{text}");

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(StringWriter? text) => WriteToLog(text?.ToString());

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(ReadOnlySpan<char> item) => WriteToLog(item.ToString());

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(bool item) => WriteToLog(item ? "(bool) True" : "(bool) False");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(int item) => WriteToLog(StringConverters.ToString(item));

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(uint item) => WriteToLog(StringConverters.ToString(item) + "U");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(long item) => WriteToLog(StringConverters.ToString(item) + "L");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(ulong item) => WriteToLog(StringConverters.ToString(item) + "UL");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(double item) => WriteToLog(StringConverters.ToString(item));

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(float item) => WriteToLog(StringConverters.ToString(item) + "f");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(DateTime item) => WriteToLog(StringConverters.ToString(item));

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(DateTimeOffset item) => WriteToLog($"(DateTimeOffset) {StringConverters.ToString(item)}");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(Slice item) => WriteToLog($"(Slice) {item.PrettyPrint()}");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(Guid item) => WriteToLog(CultureInfo.InvariantCulture, $"(Guid) {item:B}");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(Uuid128 item) => WriteToLog(CultureInfo.InvariantCulture, $"(Uuid128) {item.ToString("B")}");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(ReadOnlySpan<byte> item) => WriteToLog($"(ReadOnlySpan<byte>) {item.PrettyPrint()}");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(Span<byte> item) => WriteToLog($"(Span<byte>) {item.PrettyPrint()}");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(KeyValuePair<string, string> item) => WriteToLog($"[{item.Key}, {item.Value}]");

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(ITuple item) => WriteToLog(item.ToString());

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(IVarTuple item) => WriteToLog(item.ToString());

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(JsonObject? obj) => WriteToLog((obj ?? JsonNull.Null).ToJsonText());

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(JsonArray? obj) => WriteToLog((obj ?? JsonNull.Null).ToJsonText());

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(JsonValue? obj) => WriteToLog((obj ?? JsonNull.Null).ToJsonText());

		/// <summary>Writes a value to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(object? item) => WriteToLog(Stringify(item));

		/// <summary>Writes a message to the output log, without appending a line-break</summary>
		/// <remarks>Please note that some test runner do not support this feature, and will always insert line-breaks.</remarks>
		[DebuggerNonUserCode]
		protected static void LogPartial(string? text) => WriteToLog(text, lineBreak: false);

		/// <summary>Writes a message to the output log, without appending a line-break</summary>
		/// <remarks>Please note that some test runner do not support this feature, and will always insert line-breaks.</remarks>
		[DebuggerNonUserCode]
		protected static void LogPartial(StringBuilder? text) => WriteToLog($"{text}", lineBreak: false);

		/// <summary>Writes an empty line to the output log.</summary>
		[DebuggerNonUserCode]
		public static void Log() => WriteToLog(string.Empty);

		/// <remarks>Writes a message to the error log.</remarks>
		/// <remarks>Please note that some test runner do not distinguish without regular output and error output.</remarks>
		[DebuggerNonUserCode]
		public static void LogError(string? text) => WriteToErrorLog(text);

		/// <remarks>Writes a message to the error log.</remarks>
		/// <remarks>Please note that some test runner do not distinguish without regular output and error output.</remarks>
		[DebuggerNonUserCode]
		public static void LogError(ref DefaultInterpolatedStringHandler handler) => WriteToErrorLog(handler.ToStringAndClear());

		/// <remarks>Writes a message to the error log.</remarks>
		/// <remarks>Please note that some test runner do not distinguish without regular output and error output.</remarks>
		[DebuggerNonUserCode]
		public static void LogError(string? text, Exception? e)
		{
			if (e is not null)
			{
				WriteToErrorLog($"{text}{Environment.NewLine}{e}");
			}
			else
			{
				WriteToErrorLog(text);
			}
		}

		/// <remarks>Writes a message to the error log.</remarks>
		/// <remarks>Please note that some test runner do not distinguish without regular output and error output.</remarks>
		[DebuggerNonUserCode]
		public static void LogError(ref DefaultInterpolatedStringHandler handler, Exception? e)
		{
			if (e is not null)
			{
				handler.AppendLiteral(Environment.NewLine);
				handler.AppendLiteral(e.ToString());
			}
			WriteToErrorLog(handler.ToStringAndClear());
		}

		/// <summary>Writes the current stack trace to the output log</summary>
		/// <param name="skip">Number of stack frames to skip (usually at least 2)</param>
		[DebuggerNonUserCode]
		protected static void DumpStackTrace(int skip = 2)
		{
			var stack = Environment.StackTrace.Split('\n');

			// drop the bottom of the stack (System.*, NUnit.*, ...)
			int last = stack.Length - 1;
			while(last > skip && (stack[last].Contains("   at System.", StringComparison.Ordinal) || stack[last].Contains("   at NUnit.Framework.", StringComparison.Ordinal)))
			{
				--last;
			}

			Log($"> {string.Join("\n> ", stack, skip, last - skip + 1)}");
		}

		/// <summary>Formats as a one-line compact JSON representation of the value</summary>
		[DebuggerNonUserCode]
		protected static string Jsonify(object? item) => CrystalJson.Serialize(item, CrystalJsonSettings.Json);

		/// <summary>Formats as a one-line compact JSON representation of the value</summary>
		[DebuggerNonUserCode]
		protected static string Jsonify<T>(T? item) => CrystalJson.Serialize(item, CrystalJsonSettings.Json);

		/// <summary>Formats as a one-line, human-readable, textual representation of the value</summary>
		[DebuggerNonUserCode]
		protected static string Stringify(object? item)
		{
			switch (item)
			{
				case null: return "<null>";

				case string s: return Escape(s);
				case int i: return i.ToString(NumberFormatInfo.InvariantInfo);
				case long l: return string.CreateInvariant($"{l:R}L");
				case uint ui: return string.CreateInvariant($"{ui:R}U");
				case ulong ul: return string.CreateInvariant($"{ul:R}UL");
				case double d: return string.CreateInvariant($"{d:R}");
				case float f: return string.CreateInvariant($"{f:R}f");
				case TimeSpan ts: return string.CreateInvariant($"(TimeSpan) {ts:c}");
				case DateTime dt: return string.CreateInvariant($"(DateTime) {dt:O}");
				case DateTimeOffset dto: return string.CreateInvariant($"(DateTimeOffset) {dto:O}");
				case DateOnly dt: return string.CreateInvariant($"(DateOnly) {dt:O}");
				case TimeOnly dt: return string.CreateInvariant($"(TimeOnly) {dt:O}");
				case Guid g: return string.CreateInvariant($"(Guid) {g:B}");
				case Uuid128 uuid: return string.CreateInvariant($"(Uuid128) {uuid:B}");
				case Uuid96 uuid: return string.CreateInvariant($"(Uuid96) {uuid:B}");
				case Uuid80 uuid: return string.CreateInvariant($"(Uuid80) {uuid:B}");
				case Uuid64 uuid: return string.CreateInvariant($"(Uuid64) {uuid:B}");
				case Uuid48 uuid: return string.CreateInvariant($"(Uuid48) {uuid:B}");

				case StringBuilder sb: return Escape(sb);
				case char[] s: return Escape(s);
				case byte[] buf: return "(byte[]) " + buf.AsSlice().PrettyPrint();
				case Slice s: return "(Slice) " + s.PrettyPrint();
				case MemoryStream ms: return "(MemoryStream) " + ms.ToSlice().PrettyPrint();

				case JsonValue j: return j.ToJsonText();
				case ITuple t: return t.ToString()!;
				case IJsonSerializable j: return $"({j.GetType().GetFriendlyName()}) {j.ToJsonText()}";
				case IFormattable fmt: return $"({item.GetType().GetFriendlyName()}) {fmt.ToString(null, CultureInfo.InvariantCulture)}";
				case Task: throw new AssertionException("Cannot stringify a Task! You probably forget to add 'await' somewhere the code!");
			}

			var type = item.GetType();

			if (item is Array arr)
			{ // Array
				var elType = type.GetElementType()!;
				if (elType.IsAssignableTo(typeof(IJsonSerializable)))
				{
					return $"({type.GetFriendlyName()}) {CrystalJson.Serialize(item)}";
				}
				if (elType.IsAssignableTo(typeof(IFormattable)))
				{
					return $"({elType.GetFriendlyName()}[{arr.Length}]) [ {string.Join(", ", arr.Cast<IFormattable>().Select(x => x.ToString(null, CultureInfo.InvariantCulture)))} ]";
				}
				return $"({elType.GetFriendlyName()}[{arr.Length}]) {CrystalJson.Serialize(item)}";
			}

			return $"({type.GetFriendlyName()}) {item}";
		}

		private static readonly SearchValues<char> QuotedCharacters = SearchValues.Create(['\\', '\r', '\n', '\0', '\"']);

#if NET9_0_OR_GREATER
		private static string Escape(ReadOnlySpan<char> text)
		{
			int len = text.Length;
			if (len == 0) return "\"\"";

			// pre-compute the final string size
			int extra = text.CountAny(SimpleTest.QuotedCharacters);

			if (extra == 0)
			{ // clean string
				return $"\"{text}\"";
			}

			// add the number of extra '\', plus 2 for the surrounding double quotes
			int finalLength = checked(len + extra + 2);

			// format the string
			return string.Create(finalLength, text, (dest, state) =>
			{
				var buffer = dest;

				buffer[0] = '"';
				buffer = buffer[1..];

				var source = state;
				while (!source.IsEmpty)
				{
					int index = source.IndexOfAny(SimpleTest.QuotedCharacters);

					if (index == -1)
					{ // the rest of the chunk is clean
						source.CopyTo(buffer);
						buffer = buffer[source.Length..];
						break;
					}

					if (index > 0)
					{ // the head of the chunk is clean
						source[..index].CopyTo(buffer);
						buffer = buffer[index..];
						source = source[index..];
					}

					char c = source[0];
					buffer[0] = '\\';
					buffer[1] = c != '\0' ? c : '0'; // only '\0' uses a different token

					buffer = buffer[2..];
					source = source[1..];
				}
				Contract.Debug.Ensures(buffer.Length == 1);
				buffer[0] = '"';
			});
		}
#else // fallback that allocates on .NET 8
		private static string Escape(string text)
		{
			// old school Replace chain!
			return string.IsNullOrEmpty(text)
				? "\"\""
				: $"\"{text.Replace(@"\", @"\\").Replace("\r", @"\r").Replace("\n", @"\n").Replace("\0", @"\0").Replace(@"""", @"\""")}\"";
		}

		private static string Escape(char[] text)
		{
			return text.Length == 0
				? "\"\""
				: Escape(new string(text));
		}
#endif

		private static string Escape(StringBuilder sb)
		{
			Contract.Debug.Requires(sb is not null);

			int len = sb.Length;
			if (len == 0) return "\"\"";

#if NET9_0_OR_GREATER

			// pre-compute the final string size
			int extra = 0;
			foreach (ReadOnlyMemory<char> chunk in sb.GetChunks())
			{
				// all quoted characters will require an extra '\'
				extra += chunk.Span.CountAny(SimpleTest.QuotedCharacters);
			}
			// add the number of extra '\', plus 2 for the surrounding double quotes
			int finalLength = checked(len + extra + 2);

			// format the string
			return string.Create(finalLength, sb, (dest, state) =>
			{
				var buffer = dest;
				buffer[0] = '"';
				buffer = buffer[1..];

				foreach (var chunk in state.GetChunks())
				{
					var source = chunk.Span;
					while (!source.IsEmpty)
					{
						int index = source.IndexOfAny(SimpleTest.QuotedCharacters);

						if (index == -1)
						{ // the rest of the chunk is clean
							source.CopyTo(buffer);
							buffer = buffer[source.Length..];
							break;
						}

						if (index > 0)
						{ // the head of the chunk is clean
							source[..index].CopyTo(buffer);
							buffer = buffer[index..];
							source = source[index..];
						}

						char c = source[0];
						buffer[0] = '\\';
						buffer[1] = c != '\0' ? c : '0'; // only '\0' uses a different token

						buffer = buffer[2..];
						source = source[1..];
					}
				}
				Contract.Debug.Ensures(buffer.Length == 1);
				buffer[0] = '"';
			});
#else
			return Escape(sb.ToString());
#endif
		}

		/// <summary>Returns a factory that can create <see cref="ILogger{TCategoryName}"/> instances that will output to the test log.</summary>
		protected virtual ILoggerFactory Loggers => TestLoggerFactory.Instance;

		protected static void ConfigureLogging(IServiceCollection services, Action<ILoggingBuilder>? configure = null)
		{
			services.AddLogging(logging =>
			{
				logging.AddProvider(TestLoggerProvider.Instance);
				configure?.Invoke(logging);
			});
		}

		/// <summary>Log Provider that will forward logs to the test runner's output.</summary>
		private class TestLoggerProvider : ILoggerProvider
		{

			public static readonly ILoggerProvider Instance = new TestLoggerProvider();

			public void Dispose() { }

			public ILogger CreateLogger(string categoryName)
			{
				return new TestLogger(categoryName);
			}

		}

		/// <summary>Log Factory that will forward logs to the test runner's output.</summary>
		private class TestLoggerFactory : ILoggerFactory
		{

			public static readonly ILoggerFactory Instance = new TestLoggerFactory();

			public void Dispose() { }

			public ILogger CreateLogger(string categoryName)
			{
				return new TestLogger(categoryName);
			}

			public void AddProvider(ILoggerProvider provider)
			{
				//
			}
		}

		/// <summary>Logger that will forward logs to the test runner's output.</summary>
		internal class TestLogger : ILogger
		{

			public string Category { get; }

			private string Prefix { get; }

			public TestLogger(string category)
			{
				this.Category = category;
				this.Prefix = "[" + category + "] ";
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
			{
				string msg = this.Prefix + formatter(state, exception);
				switch (logLevel)
				{
					case LogLevel.Trace:
					case LogLevel.Debug:
					case LogLevel.Information:
					{
						SimpleTest.Log(msg);
						break;
					}
					case LogLevel.Warning:
					case LogLevel.Error:
					case LogLevel.Critical:
					{
						SimpleTest.LogError(msg, exception);
						break;
					}
				}
			}

			public bool IsEnabled(LogLevel logLevel) => true;

			public IDisposable BeginScope<TState>(TState state) where TState : notnull => Disposable.Create(() => {});

		}

		#endregion

		#region Dependency Injection...

		/// <summary>List of any service provider that was created by the test method</summary>
		private List<ServiceProvider>? CustomServices { get; set; }

		/// <summary>Creates a new <see cref="IServiceProvider">service provider</see> for use inside this test method</summary>
		/// <param name="configure">Handler that will customize the service provider</param>
		/// <returns>Service provider that can be used during this method</returns>
		protected IServiceProvider CreateServices(Action<IServiceCollection> configure)
		{
			var services = new ServiceCollection();

			services.AddSingleton(TestContext.CurrentContext);
			services.AddSingleton(TestContext.Parameters);
			services.AddSingleton(this);
			services.AddSingleton(this.Clock);
			services.AddSingleton(this.Rnd);
			ConfigureLogging(services);
			configure(services);

			var provider = services.BuildServiceProvider(new ServiceProviderOptions() { ValidateOnBuild = true, });
			(this.CustomServices ??= []).Add(provider);
			return provider;
		}

		#endregion

		#region Dump JSON...

		/// <summary>Outputs a human-readable representation of a JSON Value</summary>
		[DebuggerNonUserCode]
		public static void Dump(JsonValue? value)
		{
			WriteToLog(value?.ToJsonText(CrystalJsonSettings.JsonIndented) ?? "<null>");
		}

		/// <summary>Outputs a human-readable representation of a JSON Array</summary>
		[DebuggerNonUserCode]
		public static void Dump(JsonArray? value)
		{
			if (value == null)
			{
				WriteToLog("[0] <null_array>");
				return;
			}

			if (value.Count == 0)
			{
				WriteToLog("[0] <empty_array>");
				return;
			}

			WriteToLog(CultureInfo.InvariantCulture, $"[{value.Count}] ", lineBreak: false);
			if (value.All(JsonType.Number) || value.All(JsonType.Boolean))
			{ // vector of numbers
				WriteToLog(value.ToJsonText());
				return;
			}
			WriteToLog(value.ToJsonText(CrystalJsonSettings.JsonIndented));
		}

		/// <summary>Outputs a human-readable representation of a JSON Object</summary>
		[DebuggerNonUserCode]
		public static void Dump(JsonObject? value)
		{
			WriteToLog(value?.ToJsonText(CrystalJsonSettings.JsonIndented) ?? "<null>");
		}

		#endregion

		#region Dump XML..

		/// <summary>Outputs a human-readable representation of an XML snippet</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(string xmlText, string? label = null, int indent = 0)
		{
			var doc = new XmlDocument();
			doc.LoadXml(xmlText);
			DumpXml(doc, label, indent);
		}

		/// <summary>Outputs a human-readable representation of an XML document</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(XmlDocument? doc, string? label = null, int indent = 0)
		{
			DumpXml(doc?.DocumentElement, label, indent);
		}

		/// <summary>Outputs a human-readable representation of an XML node</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(XmlNode? node, string? label = null, int indent = 0)
		{
			if (node == null) { Log($"{label}: <null>"); return; }

			if (label != null) Log($"{label}:");

			var sb = new StringBuilder();
			var settings = new XmlWriterSettings
			{
				Indent = true
			};
			using (var writer = XmlWriter.Create(sb, settings))
			{
				writer.WriteNode(node.CreateNavigator()!, true);
			}

			string xml = sb.ToString();
			if (indent > 0)
			{ // poor man's indentation (pattent pending)
				xml = xml.Replace("\n", "\n" + new string('\t', indent));
			}
			WriteToLog(xml);
		}

		/// <summary>Outputs a human-readable representation of an XML document</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(System.Xml.Linq.XDocument? doc, string? label = null, int indent = 0)
		{
			DumpXml(doc?.Root, label, indent);
		}

		/// <summary>Outputs a human-readable representation of an XML element</summary>
		public static void DumpXml(System.Xml.Linq.XElement? node, string? label = null, int indent = 0)
		{
			if (node == null) { Log($"{label}: <null>"); return; }

			if (label != null) Log($"{label}:");

			string xml = node.ToString();
			if (indent > 0)
			{ // poor man's indentation (pattent pending)
				xml = xml.Replace("\n", "\n" + new string('\t', indent));
			}
			WriteToLog(xml);
		}

		#endregion

		#region Dump (generic)...

		/// <summary>Tests if a type is one of the many shapes of Task or ValueTask</summary>
		/// <remarks>Used to detect common mistakes like passing a task to Dump(...) without first awaiting it</remarks>
		protected static bool IsTaskLike(Type t)
		{
			if (typeof(Task).IsAssignableFrom(t)) return true;
			if (t == typeof(ValueTask)) return true;
			if (t is { IsGenericType: true, Name: "ValueTask`1", Namespace: "System.Threading.Tasks" }) return true;
			return false;
		}

		/// <summary>ERROR: you should await the task first, before dumping the result!</summary>
		[Obsolete("You forgot to await the task!", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		protected static void Dump<T>(Task<T> value) => Assert.Fail($"Cannot dump the content of a Task<{typeof(T).GetFriendlyName()}>! Most likely you forgot to 'await' the method that produced this value.");

		/// <summary>ERROR: you should await the task first, before dumping the result!</summary>
		[Obsolete("You forgot to await the task!", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		protected static void Dump<T>(ValueTask<T> value) => Assert.Fail($"Cannot dump the content of a ValueTask<{typeof(T).GetFriendlyName()}>! Most likely you forgot to 'await' the method that produced this value.");

		/// <summary>Outputs a human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void Dump<T>(T value)
		{
			if (IsTaskLike(typeof(T)))
			{
				Assert.Fail($"Cannot dump the content of a {typeof(T).GetFriendlyName()}! Most likely you work to 'await' the method that produced this value!");
			}
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.JsonIndented.WithNullMembers().WithEnumAsStrings()));
		}

		/// <summary>Outputs a human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void Dump<T>(string label, T value)
		{
			WriteToLog($"{label}: <{(value != null ? value.GetType() : typeof(T)).GetFriendlyName()}>");
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.JsonIndented.WithEnumAsStrings()));
		}

		/// <summary>Output a compact human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void DumpCompact<T>(T value)
		{
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.Json));
		}

		/// <summary>Output a compact human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void DumpCompact<T>(string label, T value)
		{
			WriteToLog($"{label,-10}: {CrystalJson.Serialize(value, CrystalJsonSettings.Json)}");
		}

		#endregion

		#region Dump Hexa...

		/// <summary>Outputs a hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(byte[] buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			DumpHexa(buffer.AsSlice(), options);
		}

		/// <summary>Outputs a hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(Slice buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer, options), lineBreak: false);
		}

		/// <summary>Outputs a hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(ReadOnlySpan<byte> buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer, options), lineBreak: false);
		}

		/// <summary>Outputs a hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(ReadOnlyMemory<byte> buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer.Span, options), lineBreak: false);
		}

		/// <summary>Outputs a hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa<T>(ReadOnlySpan<T> array, HexaDump.Options options = HexaDump.Options.Default)
			where T : struct
		{
			WriteToLog(CultureInfo.InvariantCulture, $"Dumping memory content of {typeof(T).GetFriendlyName()}[{array.Length:N0}]:");
			WriteToLog(HexaDump.Format(MemoryMarshal.AsBytes(array), options), lineBreak: false);
		}

		/// <summary>Outputs a hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(byte[] left, byte[] right)
		{
			DumpVersus(left.AsSlice(), right.AsSlice());
		}

		/// <summary>Outputs a hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(Slice left, Slice right)
		{
			WriteToLog(HexaDump.Versus(left, right), lineBreak: false);
		}

		/// <summary>Outputs a hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			WriteToLog(HexaDump.Versus(left, right), lineBreak: false);
		}

		/// <summary>Outputs a hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
		{
			WriteToLog(HexaDump.Versus(left.Span, right.Span), lineBreak: false);
		}

		#endregion

		#region Randomness...

		private Random? m_rnd;

		/// <summary>Returns the random generator used by this test run</summary>
		protected Random Rnd
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_rnd ??= TestContext.CurrentContext.Random;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => m_rnd = value;
		}

		protected Random CreateRandomizer(int? seed = null)
		{
			seed ??= new Random().Next();
			var rnd = new Random(seed.Value);
			Log($"# Using random seed {seed.Value}");
			m_rnd = rnd;
			return rnd;
		}

		/// <summary>Pick a random element in a set</summary>
		protected TItem Choose<TItem>(ReadOnlySpan<TItem> items) => items[NextInt32(items.Length)];

		/// <summary>Pick a random element in a set</summary>
		protected TItem Choose<TItem>(Span<TItem> items) => items[NextInt32(items.Length)];

		/// <summary>Pick a random element in a set</summary>
		protected TItem Choose<TItem>(TItem[] items) => items[NextInt32(items.Length)];
		
		/// <summary>Pick a random element in a set</summary>
		protected TItem Choose<TItem>(ICollection<TItem> items)
		{
			if (items.TryGetSpan(out var span))
			{
				return Choose(span);
			}

			if (items is IList<TItem> list)
			{
				return list[NextInt32(list.Count)];
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{
				var index = NextInt32(count);
				if (index == 0) return items.First();
				return items.Skip(index - 1).First();
			}
			
			// we have to copy the collection :(
			return Choose(CollectionsMarshal.AsSpan(items.ToList()));
		}

		/// <summary>Pick a random key/value in a dictionary</summary>
		protected KeyValuePair<TKey, TValue> Choose<TKey, TValue>(Dictionary<TKey, TValue> items) where TKey : notnull
		{
			var key = Choose(items.Keys);
			return new(key, items[key]);
		}

		/// <summary>Performs an in-place shuffle of an array.</summary>
		protected void Shuffle<T>(T[] items) => Shuffle<T>(this.Rnd, items.AsSpan());

		/// <summary>Performs an in-place shuffle of a span.</summary>
		protected void Shuffle<T>(Span<T> items) => Shuffle<T>(this.Rnd, items);

		/// <summary>Performs an in-place shuffle of an array.</summary>
		protected static void Shuffle<T>(Random rnd, T[] items) => Shuffle(rnd, items.AsSpan());

		/// <summary>Performs an in-place shuffle of a span.</summary>
		protected static void Shuffle<T>(Random rnd, Span<T> items)
		{
#if NET8_0_OR_GREATER
			rnd.Shuffle(items);
#else
			int length = items.Length;
			for (int i = 0; i < length - 1; ++i)
			{
				int j = rnd.Next(i, length);
				if (j != i)
				{
					(items[i], items[j]) = (items[j], items[i]);
				}
			}
#endif
		}

		/// <summary>Generates an array filled with random bytes</summary>
		/// <remarks>The array may contain values equal to <b>0</b></remarks>
		protected byte[] GetRandomData(int count) => GetRandomData(this.Rnd, count);

		/// <summary>Generates an array filled with random bytes</summary>
		/// <remarks>The array may contain values equal to <b>0</b></remarks>
		protected static byte[] GetRandomData(Random rnd, int count)
		{
			var tmp = new byte[count];
			rnd.NextBytes(tmp);
			return tmp;
		}

		/// <summary>Creates a string populated with characters chosen at random from <paramref name="choices"/>.</summary>
		protected string GetRandomString(ReadOnlySpan<char> choices, int count)
		{
#if NET10_0_OR_GREATER
			return this.Rnd.GetString(choices, count);
#else
			if (choices.IsEmpty)
			{
				throw new ArgumentException("Span cannot be empty.", nameof(choices));
			}

			if (count <= 0)
			{
				ArgumentOutOfRangeException.ThrowIfNegative(count);
				return string.Empty;
			}

			Span<char> chars = stackalloc char[count];
			this.Rnd.GetItems(choices, chars);
			return chars.ToString();
#endif
		}

		/// <summary>Creates a string filled with random hexadecimal characters.</summary>
		protected string GetRandomHexString(int count, bool lowercase = false)
		{
#if NET10_0_OR_GREATER
			return this.Rnd.GetHexString(count);
#else
			return GetRandomString(GetHexChoices(lowercase), count);

			static ReadOnlySpan<char> GetHexChoices(bool lowercase) => lowercase ? "0123456789abcdef" : "0123456789ABCDEF";
#endif
		}

		/// <summary>Generates a <see cref="Slice"/> filled with random bytes</summary>
		/// <remarks>The array may contain values equal to <b>0</b></remarks>
		protected Slice GetRandomSlice(int count) => GetRandomSlice(this.Rnd, count);

		/// <summary>Generates a <see cref="Slice"/> filled with random bytes</summary>
		/// <remarks>The array may contain values equal to <b>0</b></remarks>
		protected static Slice GetRandomSlice(Random rnd, int count) => Slice.Random(rnd, count);

		/// <summary>Returns the next chunk with a random size</summary>
		protected ReadOnlySpan<T> NextRandomChunk<T>(ReadOnlySpan<T> remaining, int? maxSize = null) => NextRandomChunk(this.Rnd, remaining, maxSize);

		/// <summary>Returns the next chunk with a random size</summary>
		protected static ReadOnlySpan<T> NextRandomChunk<T>(Random rnd, ReadOnlySpan<T> remaining, int? maxSize = null)
		{
			if (remaining.Length == 0) return default;

			int sz = NextInt32(rnd, 1, Math.Min(remaining.Length, maxSize ?? remaining.Length));
			return remaining[..sz];
		}

		/// <summary>Returns the next chunk with a random size</summary>
		protected Slice NextRandomChunk(Slice remaining, int? maxSize = null) => NextRandomChunk(this.Rnd, remaining, maxSize);

		/// <summary>Returns the next chunk with a random size</summary>
		protected static Slice NextRandomChunk(Random rnd, Slice remaining, int? maxSize = null)
		{
			if (remaining.Count == 0) return Slice.Nil;

			int sz = NextInt32(rnd, 1, Math.Min(remaining.Count, maxSize ?? remaining.Count));
			return remaining.Substring(0, sz);
		}

		#region 32-bit...

		/// <summary>Returns a list of random 32-bit numbers</summary>
		protected int[] GetRandomNumbers(int count, int min = 0, int max = int.MaxValue) => GetRandomNumbers(this.Rnd, count, min, max);

		/// <summary>Returns a list of random 32-bit numbers</summary>
		protected int[] GetRandomNumbers(Random rnd, int count, int min = 0, int max = int.MaxValue)
		{
			var numbers = new int[count];
			for (int i = 0; i < numbers.Length; i++)
			{
				numbers[i] = rnd.Next(min, max);
			}
			return numbers;
		}

		/// <summary>Return a random 32-bit positive integer</summary>
		/// <returns>Random value from <see langword="0"/> to <see langword="0xFFFFFFFFu"/> (included)</returns>
		protected static uint NextRawBits32(Random rnd) => (uint) rnd.NextInt64(0, 0x100000000L);

		/// <summary>Return a random 32-bit positive integer, with only the specified number of bits of enthropy</summary>
		/// <returns>Random value from <see langword="0"/> to <c>(<see langword="1u"/> &lt;&lt; <paramref name="bits"/>) - <see langword="1"/></c> (included)</returns>
		/// <example><c>NextRawBits32(rnd, 9)</c> will return values that range from <see langword="0"/> to <see langword="511"/> (included)</example>
		protected static uint NextRawBits32(Random rnd, int bits) => bits switch
		{
			32 => NextRawBits32(rnd),
			>= 1 and < 32 => (uint) rnd.NextInt64(0, 0x1L << bits),
			_ => throw new ArgumentOutOfRangeException(nameof(bits), bits, "Can only generate between 1 and 32 bits or randomness")
		};

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="int.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected int NextInt32() => NextInt32(this.Rnd);

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected int NextInt32(int max) => NextInt32(this.Rnd, max);

		/// <summary>Return a random 33-bit integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/>></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected int NextInt32(int min, int max) => NextInt32(this.Rnd, min, max);

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="int.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected static int NextInt32(Random rnd) => rnd.Next();

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected static int NextInt32(Random rnd, int max) => rnd.Next(max);

		/// <summary>Return a random 33-bit integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/>></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected static int NextInt32(Random rnd, int min, int max) => rnd.Next(min, max);

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="uint.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="uint.MaxValue"/></remarks>
		protected uint NextUInt32() => NextUInt32(this.Rnd);

		/// <summary>Return a random 32-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected uint NextUInt32(uint max) => NextUInt32(this.Rnd, max);

		/// <summary>Return a random 32-bit positive integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected uint NextUInt32(uint min, uint max) => NextUInt32(this.Rnd, min, max);

		/// <summary>Return a random 32-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="uint.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="uint.MaxValue"/></remarks>
		protected static uint NextUInt32(Random rnd) => (uint) rnd.NextInt64(0, uint.MaxValue);

		/// <summary>Return a random 32-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static uint NextUInt32(Random rnd, uint max) => (uint) rnd.NextInt64(0, max);

		/// <summary>Return a random 32-bit positive integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static uint NextUInt32(Random rnd, uint min, uint max) => (uint) rnd.NextInt64(min, max);

		#endregion

		#region 64-bit...

		/// <summary>Returns a list of random 64-bit numbers</summary>
		protected long[] GetRandomNumbers64(int count, long min = 0, long max = long.MaxValue) => GetRandomNumbers64(this.Rnd, count, min, max);

		/// <summary>Returns a list of random 64-bit numbers</summary>
		protected long[] GetRandomNumbers64(Random rnd, int count, long min = 0, long max = long.MaxValue)
		{
			var numbers = new long[count];
			for (int i = 0; i < numbers.Length; i++)
			{
				numbers[i] = rnd.NextInt64(min, max);
			}
			return numbers;
		}

		/// <summary>Return a random 64-bit positive integer</summary>
		/// <remarks>This method can return <see cref="ulong.MaxValue"/></remarks>
		protected static ulong NextRawBits64(Random rnd)
		{
			// genereate 2 x 32 bits of enthropy
			ulong a = (ulong) rnd.NextInt64(0, 0x100000000L);
			ulong b = (ulong) rnd.NextInt64(0, 0x100000000L);
			return (a << 32) | b;
		}

		/// <summary>Return a random 64-bit positive integer, with up to <paramref name="bits"/> bits of enthropy</summary>
		/// <returns>Random value from <see langword="0"/> to <c>(<see langword="1u"/> &lt;&lt; <paramref name="bits"/>) - <see langword="1"/></c> (included)</returns>
		/// <example><c>NextRawBits64(rnd, 42)</c> will return values that range from <see langword="0"/> to <see langword="4_398_046_511_103"/> (included)</example>
		protected static ulong NextRawBits64(Random rnd, int bits) => bits switch
		{
			64 => NextRawBits64(rnd),
			32 => NextRawBits32(rnd),
			>= 1 and < 32 => (ulong) rnd.NextInt64(0, 0x1L << (bits - 1)),
			< 64 => ((ulong) rnd.NextInt64(0, 0x1L << (bits - 32)) << 32) | NextRawBits32(rnd),
			_ => throw new ArgumentOutOfRangeException(nameof(bits), bits, "Can only generate between 1 and 64 bits or randomness")
		};

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="long.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="long.MaxValue"/></remarks>
		protected long NextInt64() => NextInt64(this.Rnd);

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="long.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="long.MaxValue"/></remarks>
		protected static long NextInt64(Random rnd) => rnd.NextInt64();

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <paramref name="max"/></remarks>
		protected static long NextInt64(Random rnd, long max) => rnd.NextInt64(0, max);

		/// <summary>Return a random 63-bit positive integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <paramref name="max"/></remarks>
		protected static long NextInt64(Random rnd, long min, long max) => rnd.NextInt64(min, max);

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="ulong.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected ulong NextUInt64() => NextUInt64(this.Rnd);

		/// <summary>Return a random 64-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="ulong.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected static ulong NextUInt64(Random rnd) => (ulong) rnd.NextInt64(long.MinValue, long.MaxValue);

		/// <summary>Return a random 64-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static ulong NextUInt64(Random rnd, ulong max)
		{
			if (max <= long.MaxValue)
			{
				return (ulong) rnd.NextInt64(0, (long) max);
			}

			if (max == ulong.MaxValue)
			{ // full range
				return (ulong) rnd.NextInt64(long.MinValue, long.MaxValue);
			}

			ulong a = (ulong) rnd.NextInt64(); // 0 <= a < long.MaxValue
			ulong b = (ulong) rnd.NextInt64(0, (long) (max + 1 - long.MaxValue)); // 0 <= a <= (max - long.MaxValue)
			return a + b;
		}

		/// <summary>Return a random 64-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static ulong NextUInt64(Random rnd, ulong min, ulong max)
		{
			if (min <= long.MaxValue && max <= long.MaxValue)
			{ // max 63 bits
				return (ulong) rnd.NextInt64((long) min, (long) max);
			}

			if (min == 0 && max == ulong.MaxValue)
			{ // full range
				return (ulong) rnd.NextInt64(long.MinValue, long.MaxValue);
			}

			//REVIEW: I'm not sure if we are missing 1 value or not? for ex if min=1 && max=ulong.MaxValue ?
			ulong r = max - min;
			ulong a = (ulong) rnd.NextInt64(); // 0 <= a < long.MaxValue
			ulong b = (ulong) rnd.NextInt64(0, (long) (r + 1 - long.MaxValue)); // 0 <= a <= (max - long.MaxValue)
			return min + a + b;
		}

		#endregion

		#region 128-bit...

#if NET8_0_OR_GREATER

		/// <summary>Return a random 127-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="Int128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="Int128.MaxValue"/></remarks>
		protected Int128 NextInt128() => NextInt128(this.Rnd);

		/// <summary>Return a random 127-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="Int128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="Int128.MaxValue"/></remarks>
		protected static Int128 NextInt128(Random rnd) => new((ulong) NextInt64(rnd), NextRawBits64(rnd));

		/// <summary>Return a random 128-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="UInt128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected UInt128 NextUInt128() => NextUInt128(this.Rnd);

		/// <summary>Return a random 128-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="UInt128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected static UInt128 NextUInt128(Random rnd) => new(NextUInt64(rnd), NextRawBits64(rnd));

#endif

		#endregion

		#region Single...

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected float NextSingle() => NextSingle(this.Rnd);

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected float NextSingle(float max) => NextSingle(this.Rnd, max);

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static float NextSingle(Random rnd) => (float) rnd.NextDouble();

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static float NextSingle(Random rnd, float max) => max * (float) rnd.NextDouble();

		#endregion

		#region Double...

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected double NextDouble() => NextDouble(this.Rnd);

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <param name="max"></param></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected double NextDouble(double max) => NextDouble(this.Rnd, max);

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static double NextDouble(Random rnd) => rnd.NextDouble();

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that 0.0 &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static double NextDouble(Random rnd, double max) => max * rnd.NextDouble();

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/>/></remarks>
		protected static double NextDouble(Random rnd, double min, double max) => min + ((max - min) * rnd.NextDouble());

		#endregion

		#endregion

		#region It!

		// Fluent API used to write tests broken in steps that give a short description of the sub-operation:
		// {
		//     It("should go to the Moon, and do the other things", () =>
		//     {
		//         Assert.That(DoIt(123, 456), Is.EqualTo(789));
		//     });
		//     It("should not do this bad thing in this weird case that will never ever happen in production, I'm pretty sure...", () =>
		//     {
		//         Assert.That(() => DoIt(null, double.NaN), Throws.Exception);
		//     });
		//     // ...
		// }

		/// <summary>Executes a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static void It(string what, Action action)
		{
			Log($"=== `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			long start = GetTimestamp();
			try
			{
				action();
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: " + e);
			}
			finally
			{
				var elapsed = GetElapsedTime(start);
				Log($"> ({elapsed})");
			}
		}

		/// <summary>Executes a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static TResult It<TResult>(string what, Func<TResult> action)
		{
			Log($"=== `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			long start = GetTimestamp();
			try
			{
				return action();
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: {e}");
				return default!; // never reached
			}
			finally
			{
				var elapsed = GetElapsedTime(start);
				Log($"> ({elapsed})");
			}
		}

		/// <summary>Executes a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static async Task It(string what, Func<Task> action)
		{
			Log($"--- `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			long start = GetTimestamp();
			try
			{
				await action().ConfigureAwait(false);
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: " + e);
			}
			finally
			{
				var elapsed = GetElapsedTime(start);
				Log($"> ({elapsed})");
			}
		}

		/// <summary>Executes a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static async Task<TResult> It<TResult>(string what, Func<Task<TResult>> action)
		{
			Log($"--- `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			long start = GetTimestamp();
			try
			{
				return await action().ConfigureAwait(false);
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: " + e);
				return default!; // never reached
			}
			finally
			{
				var elapsed = GetElapsedTime(start);
				Log($"> ({elapsed})");
			}
		}

		#endregion

		#region Call Tracking...

		/// <summary>Instance for tracking and awaiting calls to asynchronous callbacks, from the main thread of the test method</summary>
		/// <typeparam name="T">Type of the metadata linked to each calls</typeparam>
		[DebuggerDisplay("Label={Label}, WasCalled={WasCalled}, Sample={Sample}")]
		protected class CallTracker<T>
		{

			internal CallTracker(string label, CancellationToken lifetime)
			{
				this.Label = label;
				this.StartedAt = Stopwatch.GetTimestamp();
				this.Token = Guid.NewGuid();
				this.Cancellation = lifetime;
			}

			/// <summary>Custom label</summary>
			public string Label { get; }

			/// <summary>Token for the current run</summary>
			/// <remarks>This token is changed after each call to <see cref="Reset"/>, and is used to track "late calls" from callbacks from a previous step.</remarks>
			public Guid Token { get; private set; }

			/// <summary>Cancellation token of the running test</summary>
			private CancellationToken Cancellation { get; }

			/// <summary>Flag that is set to <c>true</c> whenever <see cref="NotifySuccess"/> is called.</summary>
			/// <remarks>This flag is reset to <c>false</c> whenever <see cref="Reset"/> is called.</remarks>
			public bool WasCalled { get; private set; }

			/// <summary>Exception that was passed to <see cref="NotifyError"/>.</summary>
			/// <remarks>This value is reset to <c>null</c> whenever <see cref="Reset"/> is called.</remarks>
			public ExceptionDispatchInfo? Error { get; private set; }

			/// <summary>Timestamp of the start of the current run</summary>
			/// <remarks>This is updated everytime <see cref="Reset"/> is called</remarks>
			private long StartedAt { get; set; }

			/// <summary>Timestamp of the end of the current run</summary>
			/// <remarks>This is updated everytime <see cref="NotifySuccess"/> or <see cref="NotifyError"/> are called, and reset to <c>null</c> when <see cref="Reset"/> is called.</remarks>
			private long? CompletedAt { get; set; }

			/// <summary>Value that was passed by the callback to either <see cref="NotifySuccess"/> or <see cref="NotifyError"/></summary>
			/// <remarks>This value is reset to <c>null</c> whenever <see cref="Reset"/> is called.</remarks>
			public T? Sample { get; private set; }

			/// <summary>Signal used to wake up the main test thread when either <see cref="NotifySuccess"/> or <see cref="NotifyError"/> is called.</summary>
			private TaskCompletionSource<T?> Signal { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

#if NET9_0_OR_GREATER
			private System.Threading.Lock Lock { get; } = new();
#else
			private object Lock { get; } = new();
#endif

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			private bool IsValidToken(Guid? token)
			{
				return token is null || this.Token == token.Value;
			}

			/// <summary>Notify that the asynchronous callback has fired</summary>
			/// <param name="value">Value that the callback wants to forward to the main test thread.</param>
			/// <param name="token">Token that should either be <c>null</c>, or match the current value of <see cref="Token"/>. Any other value will be ignored.</param>
			/// <remarks>
			/// <para>This should be called from inside the async callback.</para>
			/// </remarks>
			public void NotifySuccess(T? value = default, Guid? token = null)
			{
				// capture the timestamp early, we don't want to include too much framework overhead.
				var now = Stopwatch.GetTimestamp();

				if (this.Cancellation.IsCancellationRequested)
				{ // too late!
					return;
				}

				lock (this.Lock)
				{
					if (this.Cancellation.IsCancellationRequested || !IsValidToken(token))
					{ // this is a lagging call from a previous run
						return;
					}

					this.Sample = value;
					this.WasCalled = true;
					if (this.Signal.TrySetResult(value))
					{
						this.CompletedAt = now;
					}
				}
			}

			/// <summary>Notify that the asynchronous callback has failed, or when it is known that the callback will never fire.</summary>
			/// <param name="value">Value that the callback wants to forward to the main test thread.</param>
			/// <param name="error">Exception that was captured</param>
			/// <param name="token">Token that should either be <c>null</c>, or match the current value of <see cref="Token"/>. Any other value will be ignored.</param>
			/// <remarks>
			/// <para>This should be called from inside the async callback if it wants to propagate an exception to the main test thread, or by some other thread that wants to cancel the operation.</para>
			/// <para>If the system under test has an <c>OnError</c> (or similar) event, it is a good place to call to this method from there, in order to immediately fail the test.</para>
			/// </remarks>
			public void NotifyError(T? value = default, Exception? error = null, Guid? token = null)
			{
				// capture the timestamp early, we don't want to include too much framework overhead.
				var now = Stopwatch.GetTimestamp();

				if (this.Cancellation.IsCancellationRequested)
				{ // too late!
					return;
				}

				error ??= new InvalidOperationException("The asynchronous callback failed without providing an explicit exception.");

				lock (this.Lock)
				{
					if (this.Cancellation.IsCancellationRequested || !IsValidToken(token))
					{ // this is a lagging call from a previous run
						return;
					}

					this.Error = ExceptionDispatchInfo.Capture(error);

					if (this.Signal.TrySetException(error))
					{
						this.CompletedAt = now;
					}
				}
			}

			/// <summary>Time elapsed from the start of the current and the first call to <see cref="NotifySuccess"/> or <see cref="NotifyError"/>, or the current instant if the tracker has still not been triggered.</summary>
			public TimeSpan Elapsed => Stopwatch.GetElapsedTime(this.StartedAt, this.CompletedAt ?? Stopwatch.GetTimestamp());

			/// <summary>Waits until the tracker is triggered, a timeout expires, of the test is cancelled</summary>
			/// <param name="timeout">Maximum delay allowed for the tracker to be called</param>
			/// <returns>Task that completes successfully if the tracker is called before the specified <paramref name="timeout"/>; or fails if the timeout expires, or the tests is cancelled</returns>
			public Task<T?> WaitAsync(TimeSpan timeout)
			{
				if (timeout <= TimeSpan.Zero)
				{
					throw new ArgumentException("Timeout must be greater than zero, and cannot be infinite.", nameof(timeout));
				}
				Contract.Debug.Requires(timeout > TimeSpan.Zero);

				if (this.Cancellation.IsCancellationRequested)
				{ // test was already canceled
					return HandleCancellation();
				}

				Task<T?> t;
				lock (this.Lock)
				{
					if (this.Cancellation.IsCancellationRequested)
					{ // test was just canceled
						return HandleCancellation();
					}

					t = this.Signal.Task;
				}

				return t.IsCompleted ? t : t.WaitAsync(timeout, this.Cancellation);

				Task<T?> HandleCancellation()
				{
					if (this.Signal.TrySetCanceled(this.Cancellation))
					{
						this.CompletedAt = Stopwatch.GetTimestamp();
					}
					return Task.FromCanceled<T?>(this.Cancellation);
				}

			}

			/// <summary>Resets the tracker, so that it can be used for the next async operation</summary>
			/// <returns>New tracking id</returns>
			public Guid Reset()
			{
				var token = Guid.NewGuid();
				lock (this.Lock)
				{
					this.Signal.TrySetCanceled();
					this.Signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
					this.Token = token;
					this.CompletedAt = null;
					this.WasCalled = false;
					this.Sample = default;
					this.StartedAt = Stopwatch.GetTimestamp();
					return token;
				}
			}

		}

		private int TrackerCounter;

		/// <summary>Returns a tracker that will capture asynchronous callback invocations and notify the main thread when they happen.</summary>
		/// <typeparam name="T">Type of value that can be used to distinguish multiple calls, or capture some interesting state that must be checked.</typeparam>
		/// <param name="label">Name for this tracker (optional)</param>
		/// <returns>Tuple with the tracker, and the initial token value.</returns>
		/// <remarks>
		/// <para>A tracker can be used to safely "await" some asynchronous event, from the main test thread, within the safety of a timeout.</para>
		/// <para>The asynchronous callback should call either <see cref="CallTracker{T}.NotifySuccess"/> or <see cref="CallTracker{T}.NotifyError"/> to wake up the main test thread.</para>
		/// <para>A tracker can be used multiple times by calling <see cref="CallTracker{T}.Reset"/> between each step, and capturing the corresponding token.</para>
		/// <para>The callback can forward a <typeparamref name="T"/> value to the main test thread.</para>
		/// </remarks>
		/// <example>
		/// <code>
		/// // create a	tracker
		/// var (tracker, token) = CreateTracker&lt;string&gt;("onChangedCallback");
		/// // configure the SuT
		/// var foo = ....;
		/// foo.OnChanged = (id, ...) =>
		/// {
		///    // your test logic
		/// 
		///    tracker.NotifySuccess(id, token); // capture "id", will be exposed via the tracker.Sample property
		/// };
		/// 
		/// // perform the first test
		/// foo.DoSomething(...); // => should trigger the OnChanged callback under the hood
		///
		/// // wait for at most 1 second for the callback to fire
		/// var capturedId = await tracker.WaitAsync(TimeSpan.FromSeconds(1));
		/// // => succeeds if the "OnChanged" callback is invoked
		/// // => fails if nothing happens within 1 second
		///
		/// // test the value captured in the callback is as expected
		/// Assert.That(capturedId, Is.EqualTo("foo123"));
		/// // note: tracker.Sample will also hold the captured value
		/// </code>
		/// </example>
		protected (CallTracker<T> Tracker, Guid Token) CreateTracker<T>(string? label = null)
		{
			// generate a label, if none is provided
			if (string.IsNullOrWhiteSpace(label))
			{
				var counter = Interlocked.Increment(ref this.TrackerCounter);
				label = $"Tracker #{counter}";
			}

			var tracker = new CallTracker<T>(label, this.Cancellation);
			return (tracker, tracker.Token);
		}

		#endregion

	}

}
