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

namespace SnowBank.Benchmarks
{
	using System.Buffers.Binary;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Globalization;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Client.Utils;

	/// <summary>Real-cluster saturation harness for the async FDBFuture callback path.</summary>
	/// <remarks>
	/// <para>Measures the aggregate throughput (ops/sec) and allocation rate of async FDB operations against a LOCAL
	/// loopback cluster (same Docker container as the FoundationDB.Tests suite, memory-friendly single instance), with
	/// enough concurrency that the per-future managed overhead (cookie registration, callback marshaling, task
	/// completion) is a measurable term of the total, while the network round-trip is an amortized floor.</para>
	/// <para>An operation only exercises the machinery under test if its future was NOT ready when the wrapper was
	/// constructed (a ready future completes inline and bypasses callbacks entirely). The harness therefore samples
	/// <see cref="DebugCounters.CallbackHandlesTotal"/> around every segment and reports the callback-to-op ratio:
	/// a segment is only valid when that ratio is ~1.0.</para>
	/// <para>Workloads: <c>grv</c> = one <c>get_read_version</c> per transaction (leanest possible wire payload, no
	/// key/value marshaling); <c>get</c> = pipelined snapshot point-reads of distinct missing keys (realistic variant,
	/// thousands of futures in flight from a few transactions).</para>
	/// <para>Usage: <c>dotnet run -c Release -- storm [grv|get|all] [--workers N] [--batch N] [--duration SECONDS]</c></para>
	/// </remarks>
	public static class FutureStormHarness
	{

		private sealed record StormOptions(string Workload, int GrvWorkers, int GetWorkers, int Batch, TimeSpan Duration, int Keys, int Readers, int Committers);

		private sealed record SegmentResult(string Name, long Ops, TimeSpan Elapsed, long Callbacks, long Futures, long AllocatedBytes, int Gen0, int Gen1, int Gen2, double PauseMs, double P50Us, double P99Us, double P999Us, double MaxUs)
		{
			public double OpsPerSec => this.Ops / this.Elapsed.TotalSeconds;

			public double CallbackRatio => this.Ops != 0 ? (double) this.Callbacks / this.Ops : 0;

			public double BytesPerOp => this.Ops != 0 ? (double) this.AllocatedBytes / this.Ops : 0;

			/// <summary>The dvc and multiget segments do not storm the raw callback path one-to-one (a dvc op is a whole
			/// transaction, a multiget op is one N-key call), so the cb-ratio gate only applies to grv/get.</summary>
			public bool CallbackPathVerified => this.Name is not ("grv" or "get") || this.CallbackRatio >= 0.95;
		}

		/// <summary>Log2-with-16-sub-buckets latency histogram (about 6% resolution), shared by all workers of the
		/// current segment; reset between segments. Recording is two atomic increments, no allocation.</summary>
		private static class StormHistogram
		{

			private static readonly long[] Buckets = new long[64 * 16];

			private static long MaxTicks;

			public static void Reset()
			{
				Array.Clear(Buckets);
				Volatile.Write(ref MaxTicks, 0);
			}

			public static void Record(long ticks)
			{
				if (ticks < 1) ticks = 1;
				int exp = 63 - System.Numerics.BitOperations.LeadingZeroCount((ulong) ticks);
				int mant = exp >= 4 ? (int) ((ticks >> (exp - 4)) & 15) : 0;
				Interlocked.Increment(ref Buckets[(exp << 4) | mant]);
				long max;
				while (ticks > (max = Volatile.Read(ref MaxTicks)) && Interlocked.CompareExchange(ref MaxTicks, ticks, max) != max) { }
			}

			private static double BucketLowerBoundTicks(int index)
			{
				int exp = index >> 4, mant = index & 15;
				return exp >= 4 ? (double) ((16L + mant) << (exp - 4)) : (double) (1L << exp);
			}

			private static double TicksToMicros(double ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

			public static (double P50Us, double P99Us, double P999Us, double MaxUs) Snapshot()
			{
				// only called after the segment's workers have completed: plain reads are sufficient
				long total = 0;
				foreach (var b in Buckets) { total += b; }
				if (total == 0) return (0, 0, 0, 0);

				double p50 = 0, p99 = 0, p999 = 0;
				long cum = 0;
				for (int i = 0; i < Buckets.Length; i++)
				{
					cum += Buckets[i];
					if (p50 == 0 && cum >= total * 0.50) p50 = BucketLowerBoundTicks(i);
					if (p99 == 0 && cum >= total * 0.99) p99 = BucketLowerBoundTicks(i);
					if (p999 == 0 && cum >= total * 0.999) { p999 = BucketLowerBoundTicks(i); break; }
				}
				return (TicksToMicros(p50), TicksToMicros(p99), TicksToMicros(p999), TicksToMicros(Volatile.Read(ref MaxTicks)));
			}

		}

		public static int Run(string[] args)
		{
			var options = ParseOptions(args);

			Console.WriteLine("==================================================================");
			Console.WriteLine("==  CPU-SENSITIVE MEASUREMENT RUN  -  do not run other heavy    ==");
			Console.WriteLine("==  workloads on this machine while the storm is in progress    ==");
			Console.WriteLine("==================================================================");
			Console.WriteLine($"workload={options.Workload} grv-workers={options.GrvWorkers} get-workers={options.GetWorkers} batch={options.Batch} duration={options.Duration.TotalSeconds:N0}s per segment");
			Console.WriteLine();

			try
			{
				return RunAsync(options).GetAwaiter().GetResult();
			}
			finally
			{
				Fdb.Stop();
			}
		}

		private static StormOptions ParseOptions(string[] args)
		{
			string workload = "all";
			int grvWorkers = 256;
			int getWorkers = 32;
			int batch = 64;
			var duration = TimeSpan.FromSeconds(20);
			int keys = 65_536;
			int readers = 24;
			int committers = 8;

			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i].ToLowerInvariant())
				{
					case "grv" or "get" or "all" or "misassoc" or "dvc" or "multiget": workload = args[i].ToLowerInvariant(); break;
					case "--workers": grvWorkers = getWorkers = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
					case "--batch": batch = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
					case "--duration": duration = TimeSpan.FromSeconds(int.Parse(args[++i], CultureInfo.InvariantCulture)); break;
					case "--keys": keys = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
					case "--readers": readers = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
					case "--committers": committers = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
					default: throw new ArgumentException($"Unknown storm argument '{args[i]}'");
				}
			}

			return new(workload, grvWorkers, getWorkers, batch, duration, keys, readers, committers);
		}

		private static async Task<int> RunAsync(StormOptions options)
		{
			using var lifetime = new CancellationTokenSource();
			Console.CancelKeyPress += (_, e) => { e.Cancel = true; lifetime.Cancel(); };
			var ct = lifetime.Token;

			// same native library probing as the test suite
			var probe = FdbClientNativeExtensions.ProbeNativeLibraryPaths();
			if (probe.Path == null)
			{
				Console.Error.WriteLine($"Could not locate the native client library for platform '{probe.Rid}'. Looked in: {string.Join(", ", probe.ProbedPaths)}");
				Console.Error.WriteLine("Run FoundationDB.Client.Native/DownloadBinaries.ps1 once for this worktree, then rebuild.");
				return 1;
			}
			Fdb.Options.NativeLibPath = probe.Path;
			Fdb.Start(Fdb.GetDefaultApiVersion());

			// same container naming/port scheme as FdbTest, so the harness reuses the test suite's container
			var runtimeVersion = new Version(Environment.Version.Major, Environment.Version.Minor);
			var dockerImageTag = Environment.GetEnvironmentVariable("FDB_TEST_DOCKER_TAG");
			if (string.IsNullOrEmpty(dockerImageTag)) dockerImageTag = "7.4.6";
			var serverVersion = Version.Parse(dockerImageTag);
			int port = 4600 + (((10 * serverVersion.Major + serverVersion.Minor) * 13) + ((10 * runtimeVersion.Major + runtimeVersion.Minor) * 17)) % 100;
			var containerSuffix = string.CreateInvariant($"-{serverVersion.Major}.{serverVersion.Minor}-net{runtimeVersion.Major}.{runtimeVersion.Minor}");
			var container = new FdbServerTestContainer("fdb-test" + containerSuffix, dockerImageTag, port, "fdb-test" + containerSuffix);

			try
			{
				await container.StartContainer(TimeSpan.FromSeconds(20), ct);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"Failed to start the fdb test container (is Docker Desktop running?): {e.Message}");
				return 1;
			}

			var dbOptions = new FdbConnectionOptions
			{
				ConnectionString = container.ConnectionString,
				Root = FdbPath.Root,
				DefaultTimeout = TimeSpan.FromSeconds(15),
			};

			using var db = await Fdb.OpenAsync(dbOptions, ct);

			if (options.Workload is "misassoc")
			{
				return await RunMisassociation(db, options, ct);
			}

			var results = new List<SegmentResult>();

			if (options.Workload is "grv" or "all")
			{
				results.Add(await RunSegment("grv", options.Duration, ct, stop => RunGrvWorkers(db, options.GrvWorkers, stop, ct)));
			}
			if (options.Workload is "get" or "all")
			{
				results.Add(await RunSegment("get", options.Duration, ct, stop => RunGetWorkers(db, options.GetWorkers, options.Batch, stop, ct)));
			}
			if (options.Workload is "dvc")
			{
				// one-time setup: create the benched directory; the warmup pass then primes the metadata cache,
				// so the measured segment runs cached opens that each spawn deferred value-checks
				await db.ReadWriteAsync(async tr => { await db.DirectoryLayer.CreateOrOpenAsync(tr, DvcBenchPath).ConfigureAwait(false); return true; }, ct).ConfigureAwait(false);
				results.Add(await RunSegment("dvc", options.Duration, ct, stop => RunDvcWorkers(db, options.GetWorkers, stop, ct)));
			}
			if (options.Workload is "multiget")
			{
				results.Add(await RunSegment("multiget", options.Duration, ct, stop => RunMultiGetWorkers(db, options.GetWorkers, options.Batch, stop, ct)));
			}

			Console.WriteLine();
			Console.WriteLine("=== STORM RESULTS ===");
			Console.WriteLine($"{"segment",-8} {"ops",12} {"ops/sec",12} {"cb-ratio",9} {"bytes/op",9} {"gen0",6} {"gen1",6} {"gen2",6} {"pause-ms",9} {"p50-us",9} {"p99-us",9} {"p999-us",9} {"max-us",9} {"verified",9}");
			bool allVerified = true;
			foreach (var r in results)
			{
				Console.WriteLine($"{r.Name,-8} {r.Ops,12:N0} {r.OpsPerSec,12:N0} {r.CallbackRatio,9:P1} {r.BytesPerOp,9:N1} {r.Gen0,6} {r.Gen1,6} {r.Gen2,6} {r.PauseMs,9:N1} {r.P50Us,9:N0} {r.P99Us,9:N0} {r.P999Us,9:N0} {r.MaxUs,9:N0} {(r.CallbackPathVerified ? "yes" : "NO !!"),9}");
				allVerified &= r.CallbackPathVerified;
			}
			if (!allVerified)
			{
				Console.WriteLine();
				Console.WriteLine("!!! AT LEAST ONE SEGMENT DID NOT ROUTE THROUGH fdb_future_set_callback (cb-ratio < 95%) !!!");
				Console.WriteLine("!!! Those numbers measured the ready-inline fast path and are MEANINGLESS for this spike !!!");
				return 2;
			}
			return 0;
		}

		/// <summary>Runs one measured workload segment, preceded by a short untimed warmup of the same workload.</summary>
		private static async Task<SegmentResult> RunSegment(string name, TimeSpan duration, CancellationToken ct, Func<CancellationToken, (Task Completion, Func<long> ReadOps)> launcher)
		{
			// warmup (JIT, connection handshake, container caches): same workload, not measured
			Console.WriteLine($"[{name}] warmup...");
			using (var warmupStop = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				var (completion, _) = launcher(warmupStop.Token);
				await Task.Delay(TimeSpan.FromSeconds(3), ct);
				warmupStop.Cancel();
				await completion;
			}

			Console.WriteLine($"[{name}] measuring for {duration.TotalSeconds:N0}s...");

			StormHistogram.Reset();
			long callbacksBefore = Volatile.Read(ref DebugCounters.CallbackHandlesTotal);
			long futuresBefore = Volatile.Read(ref DebugCounters.FutureHandlesTotal);
			int gen0 = GC.CollectionCount(0);
			int gen1 = GC.CollectionCount(1);
			int gen2 = GC.CollectionCount(2);
			long allocated = GC.GetTotalAllocatedBytes(precise: true);
			var pauseBefore = GC.GetTotalPauseDuration();

			using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var sw = Stopwatch.StartNew();
			var (work, readOps) = launcher(stop.Token);

			// owner rule: any run longer than ~30s must emit periodic progress
			while (sw.Elapsed < duration && !ct.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, (duration - sw.Elapsed).TotalSeconds + 0.1)), ct);
				long opsSoFar = readOps();
				long cbSoFar = Volatile.Read(ref DebugCounters.CallbackHandlesTotal) - callbacksBefore;
				double ratio = opsSoFar != 0 ? (double) cbSoFar / opsSoFar : 0;
				Console.WriteLine($"[{name}] {opsSoFar:N0} ops, {sw.Elapsed.TotalSeconds:N0}s/{duration.TotalSeconds:N0}s, {opsSoFar / sw.Elapsed.TotalSeconds:N0} ops/s avg, cb-ratio {ratio:P1}");
			}

			stop.Cancel();
			await work;
			sw.Stop();

			var (p50, p99, p999, max) = StormHistogram.Snapshot();
			return new(
				name,
				Ops: readOps(),
				Elapsed: sw.Elapsed,
				Callbacks: Volatile.Read(ref DebugCounters.CallbackHandlesTotal) - callbacksBefore,
				Futures: Volatile.Read(ref DebugCounters.FutureHandlesTotal) - futuresBefore,
				AllocatedBytes: GC.GetTotalAllocatedBytes(precise: true) - allocated,
				Gen0: GC.CollectionCount(0) - gen0,
				Gen1: GC.CollectionCount(1) - gen1,
				Gen2: GC.CollectionCount(2) - gen2,
				PauseMs: (GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds,
				P50Us: p50,
				P99Us: p99,
				P999Us: p999,
				MaxUs: max
			);
		}

		#region Result mis-association probe...

		/// <summary>Aggregated detectors of the mis-association probe; any non-zero anomaly field fails the run.</summary>
		private sealed class MisassocState
		{
			public long Reads;
			public long Commits;
			/// <summary>A seeded key read back a value that decodes to a DIFFERENT key's payload (cross-wired future)</summary>
			public long WrongValues;
			/// <summary>A seeded key read back Nil (a missing result where one must exist)</summary>
			public long NilValues;
			/// <summary>A committer running strictly sequential RMW on its OWN key got NotCommitted (semantically impossible)</summary>
			public long ImpossibleConflicts;
			/// <summary>A committer read back its own key and found neither its last committed counter nor an unknown-result candidate</summary>
			public long OwnValueMismatches;
			/// <summary>Unexpected exceptions (transport, timeouts...) - not anomalies per se, but reported</summary>
			public long OtherErrors;
			/// <summary>First few anomaly descriptions, for the report</summary>
			public readonly ConcurrentQueue<string> Samples = new();

			public long Anomalies => Volatile.Read(ref this.WrongValues) + Volatile.Read(ref this.NilValues) + Volatile.Read(ref this.ImpossibleConflicts) + Volatile.Read(ref this.OwnValueMismatches);

			public void AddSample(string text)
			{
				if (this.Samples.Count < 16) { this.Samples.Enqueue(text); }
			}
		}

		/// <summary>Targeted probe for result mis-association under saturation: does a future ever settle with ANOTHER
		/// future's result? Readers verify self-describing seeded keys (the stored value IS the key's own index, so a
		/// cross-wired result is directly detectable); committers run strictly sequential read-modify-write cycles on a
		/// key that only they touch, so any NotCommitted is semantically impossible and any foreign value read back is
		/// a cross-wire. Designed so that "the handler is CLEAN over N million ops" is a first-class outcome.</summary>
		private static async Task<int> RunMisassociation(IFdbDatabase db, StormOptions options, CancellationToken ct)
		{
			Console.WriteLine($"[misassoc] seeding {options.Keys:N0} self-describing keys...");

			// seed: key = prefix + 8-byte BE index, value = the 8-byte BE index itself
			const int SEED_CHUNK = 4096;
			for (int start = 0; start < options.Keys; start += SEED_CHUNK)
			{
				int count = Math.Min(SEED_CHUNK, options.Keys - start);
				using var tr = db.BeginTransaction(FdbTransactionMode.Default, ct);
				var key = new byte[23];
				"bench/misassoc/"u8.CopyTo(key);
				var value = new byte[8];
				for (int i = 0; i < count; i++)
				{
					BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(15), start + i);
					BinaryPrimitives.WriteInt64BigEndian(value, start + i);
					tr.Set(key.AsSpan(), value.AsSpan());
				}
				await tr.CommitAsync().ConfigureAwait(false);
			}

			Console.WriteLine($"[misassoc] load: {options.Readers} readers x {options.Batch}-deep batches + {options.Committers} sequential RMW committers, {options.Duration.TotalSeconds:N0}s...");

			var state = new MisassocState();
			long callbacksBefore = Volatile.Read(ref DebugCounters.CallbackHandlesTotal);
			using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var sw = Stopwatch.StartNew();

			var workers = new List<Task>();
			for (int i = 0; i < options.Readers; i++) { workers.Add(MisassocReader(db, options.Keys, options.Batch, i, state, stop.Token, ct)); }
			for (int i = 0; i < options.Committers; i++) { workers.Add(MisassocCommitter(db, i, state, stop.Token, ct)); }
			var all = Task.WhenAll(workers);

			while (sw.Elapsed < options.Duration && !ct.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, (options.Duration - sw.Elapsed).TotalSeconds + 0.1)), ct);
				Console.WriteLine($"[misassoc] {Volatile.Read(ref state.Reads):N0} reads, {Volatile.Read(ref state.Commits):N0} commits, {state.Anomalies} anomalies, {sw.Elapsed.TotalSeconds:N0}s/{options.Duration.TotalSeconds:N0}s");
			}
			stop.Cancel();
			await all;
			sw.Stop();

			long callbacks = Volatile.Read(ref DebugCounters.CallbackHandlesTotal) - callbacksBefore;
			long totalOps = Volatile.Read(ref state.Reads) + Volatile.Read(ref state.Commits);

			Console.WriteLine();
			Console.WriteLine("=== MIS-ASSOCIATION PROBE RESULTS ===");
			Console.WriteLine($"reads               : {Volatile.Read(ref state.Reads),12:N0}");
			Console.WriteLine($"commits             : {Volatile.Read(ref state.Commits),12:N0}");
			Console.WriteLine($"callbacks           : {callbacks,12:N0} (ratio {(totalOps != 0 ? (double) callbacks / totalOps : 0):N2} vs reads+commits; >1 expected, commits also pay GRV futures)");
			Console.WriteLine($"wrong values        : {Volatile.Read(ref state.WrongValues),12:N0}");
			Console.WriteLine($"nil values          : {Volatile.Read(ref state.NilValues),12:N0}");
			Console.WriteLine($"impossible conflicts: {Volatile.Read(ref state.ImpossibleConflicts),12:N0}");
			Console.WriteLine($"own-value mismatches: {Volatile.Read(ref state.OwnValueMismatches),12:N0}");
			Console.WriteLine($"other errors        : {Volatile.Read(ref state.OtherErrors),12:N0}");
			foreach (var sample in state.Samples)
			{
				Console.WriteLine($"  sample: {sample}");
			}
			Console.WriteLine();
			if (state.Anomalies == 0)
			{
				Console.WriteLine($"VERDICT: CLEAN - no result mis-association over {totalOps:N0} verified operations in {sw.Elapsed.TotalSeconds:N0}s");
				return 0;
			}
			Console.WriteLine($"VERDICT: ANOMALOUS - {state.Anomalies:N0} mis-association indicators over {totalOps:N0} operations (see samples above)");
			return 3;
		}

		/// <summary>Pipelined batches of snapshot reads of random seeded keys, verifying every value against the key it was requested for.</summary>
		private static async Task MisassocReader(IFdbDatabase db, int keyCount, int batch, int workerId, MisassocState state, CancellationToken stop, CancellationToken ct)
		{
			await Task.Yield();

			var key = new byte[23];
			"bench/misassoc/"u8.CopyTo(key);
			ulong rng = 0x9E3779B97F4A7C15UL * (ulong) (workerId + 1);

			var tasks = new Task<Slice>[batch];
			var seqs = new long[batch];

			while (!stop.IsCancellationRequested)
			{
				try
				{
					using var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, ct);
					var snapshot = tr.Snapshot;
					var age = Stopwatch.StartNew();

					while (age.Elapsed < TimeSpan.FromSeconds(3) && !stop.IsCancellationRequested)
					{
						for (int i = 0; i < batch; i++)
						{
							// xorshift64
							rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17;
							long seq = (long) (rng % (ulong) keyCount);
							seqs[i] = seq;
							BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(15), seq);
							tasks[i] = snapshot.GetAsync(key);
						}
						await Task.WhenAll(tasks).ConfigureAwait(false);

						for (int i = 0; i < batch; i++)
						{
							var value = tasks[i].Result;
							if (value.Count != 8)
							{
								Interlocked.Increment(ref state.NilValues);
								state.AddSample($"reader {workerId}: key #{seqs[i]} read back {(value.IsNull ? "Nil" : $"{value.Count} bytes")} instead of its 8-byte payload");
							}
							else
							{
								long actual = BinaryPrimitives.ReadInt64BigEndian(value.Span);
								if (actual != seqs[i])
								{
									Interlocked.Increment(ref state.WrongValues);
									state.AddSample($"reader {workerId}: key #{seqs[i]} read back the payload of key #{actual} (CROSS-WIRE)");
								}
							}
						}
						Interlocked.Add(ref state.Reads, batch);
					}
				}
				catch (FdbException)
				{
					Interlocked.Increment(ref state.OtherErrors);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		/// <summary>Strictly sequential read-modify-write cycles on a key only this actor touches: NotCommitted is
		/// semantically impossible (each transaction begins after the previous commit completed, and nobody else
		/// writes the key), and the read-back value must be the actor's own last committed counter.</summary>
		private static async Task MisassocCommitter(IFdbDatabase db, int workerId, MisassocState state, CancellationToken stop, CancellationToken ct)
		{
			await Task.Yield();

			var key = new byte[27];
			"bench/misassoc/own/"u8.CopyTo(key);
			BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(19), workerId);

			var value = new byte[8];
			long counter = 0;
			long lastCommitted = -1;
			bool lastUnknown = false;

			while (!stop.IsCancellationRequested)
			{
				try
				{
					using var tr = db.BeginTransaction(FdbTransactionMode.Default, ct);

					var read = await tr.GetAsync(key).ConfigureAwait(false);
					if (lastCommitted >= 0)
					{
						long actual = read.Count == 8 ? BinaryPrimitives.ReadInt64BigEndian(read.Span) : -1;
						// after commit_unknown_result the previous write may or may not have landed: both values are legal
						if (actual != lastCommitted && !(lastUnknown && actual == lastCommitted - 1))
						{
							Interlocked.Increment(ref state.OwnValueMismatches);
							state.AddSample($"committer {workerId}: own key holds {actual} but last committed counter was {lastCommitted} (unknown-result pending: {lastUnknown})");
						}
					}

					counter++;
					BinaryPrimitives.WriteInt64BigEndian(value, counter);
					tr.Set(key.AsSpan(), value.AsSpan());
					await tr.CommitAsync().ConfigureAwait(false);
					lastCommitted = counter;
					lastUnknown = false;
					Interlocked.Increment(ref state.Commits);
				}
				catch (FdbException e) when (e.Code == FdbError.NotCommitted)
				{
					Interlocked.Increment(ref state.ImpossibleConflicts);
					state.AddSample($"committer {workerId}: NotCommitted on a strictly sequential single-actor RMW (counter {counter})");
					counter--;
				}
				catch (FdbException e) when (e.Code == FdbError.CommitUnknownResult)
				{
					lastCommitted = counter;
					lastUnknown = true;
					Interlocked.Increment(ref state.Commits);
				}
				catch (FdbException)
				{
					Interlocked.Increment(ref state.OtherErrors);
					counter--;
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		#endregion

		/// <summary>The leanest async future the client can produce: one get_read_version per transaction.</summary>
		private static (Task, Func<long>) RunGrvWorkers(IFdbDatabase db, int workers, CancellationToken stop, CancellationToken ct)
		{
			long ops = 0;

			async Task Worker()
			{
				await Task.Yield();
				while (!stop.IsCancellationRequested)
				{
					try
					{
						using var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, ct);
						long t0 = Stopwatch.GetTimestamp();
						await tr.GetReadVersionAsync().ConfigureAwait(false);
						StormHistogram.Record(Stopwatch.GetTimestamp() - t0);
						Interlocked.Increment(ref ops);
					}
					catch (FdbException)
					{
						// transient cluster pushback (batch-priority throttling, etc.): the storm just keeps pounding
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}

			var tasks = new Task[workers];
			for (int i = 0; i < workers; i++) { tasks[i] = Worker(); }
			return (Task.WhenAll(tasks), () => Volatile.Read(ref ops));
		}

		/// <summary>Realistic pipelined variant: batches of snapshot point-reads of distinct (missing) keys.</summary>
		private static (Task, Func<long>) RunGetWorkers(IFdbDatabase db, int workers, int batch, CancellationToken stop, CancellationToken ct)
		{
			long ops = 0;

			async Task Worker(int workerId)
			{
				await Task.Yield();

				// distinct keys defeat both the transaction's read-your-writes cache and any warm answer that would
				// complete the future inline: every read must actually go to the server
				var key = new byte[24];
				"bench/storm/"u8.CopyTo(key);
				BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(12), workerId);
				long seq = 0;

				var tasks = new Task<Slice>[batch];

				while (!stop.IsCancellationRequested)
				{
					try
					{
						using var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, ct);
						var snapshot = tr.Snapshot;
						var age = Stopwatch.StartNew();

						// stay well under the 5s transaction limit, then rotate to a fresh transaction
						while (age.Elapsed < TimeSpan.FromSeconds(3) && !stop.IsCancellationRequested)
						{
							long t0 = Stopwatch.GetTimestamp();
							for (int i = 0; i < batch; i++)
							{
								BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(16), seq++);
								tasks[i] = snapshot.GetAsync(key);
							}
							await Task.WhenAll(tasks).ConfigureAwait(false);
							// note: this records per-BATCH latency (the pipelined await), same shape on every leg
							StormHistogram.Record(Stopwatch.GetTimestamp() - t0);
							Interlocked.Add(ref ops, batch);
						}
					}
					catch (FdbException)
					{
						// transient cluster pushback: rotate the transaction and keep going
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}

			var tasks = new Task[workers];
			for (int i = 0; i < workers; i++) { tasks[i] = Worker(i); }
			return (Task.WhenAll(tasks), () => Volatile.Read(ref ops));
		}

		/// <summary>The benched directory sits INSIDE a partition: the cached-open validation chain is built from the
		/// stamp keys of the partitions traversed by the path, so a partition-free path has an EMPTY chain and spawns
		/// no value-check at all (verified: cb-ratio exactly 1.0). Multi-tenant layouts put collections under tenant
		/// partitions, so one partition in the path is the representative shape.</summary>
		private static readonly FdbPath DvcBenchPath = FdbPath.Root[FdbPathSegment.Create("bench")][FdbPathSegment.Partition("storm-part")][FdbPathSegment.Create("inner")];

		/// <summary>Representative Directory-Layer workload: each op is one read transaction that opens a CACHED
		/// directory (which spawns deferred value-checks to validate the cached metadata) - the pattern a typical
		/// layer runs on every transaction. One op = one whole transaction, retry loop included.</summary>
		private static (Task, Func<long>) RunDvcWorkers(IFdbDatabase db, int workers, CancellationToken stop, CancellationToken ct)
		{
			long ops = 0;

			async Task Worker()
			{
				await Task.Yield();
				while (!stop.IsCancellationRequested)
				{
					try
					{
						long t0 = Stopwatch.GetTimestamp();
						await db.ReadAsync(async tr =>
						{
							var subspace = await db.DirectoryLayer.TryOpenCachedAsync(tr, DvcBenchPath).ConfigureAwait(false);
							return subspace is not null;
						}, ct).ConfigureAwait(false);
						StormHistogram.Record(Stopwatch.GetTimestamp() - t0);
						Interlocked.Increment(ref ops);
					}
					catch (FdbException)
					{
						// transient cluster pushback: keep pounding
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}

			var tasks = new Task[workers];
			for (int i = 0; i < workers; i++) { tasks[i] = Worker(); }
			return (Task.WhenAll(tasks), () => Volatile.Read(ref ops));
		}

		/// <summary>Representative index-style multi-get workload: each op is ONE GetValuesAsync call over
		/// 'batch' distinct (missing) keys - the N-key overlay that spawns one native future per key and folds
		/// them into a single result at the binding level. Missing keys keep the payload at zero so bytes/op
		/// isolates the machinery cost.</summary>
		private static (Task, Func<long>) RunMultiGetWorkers(IFdbDatabase db, int workers, int batch, CancellationToken stop, CancellationToken ct)
		{
			long ops = 0;

			async Task Worker(int workerId)
			{
				await Task.Yield();

				// each key wraps its own preallocated buffer, mutated in place between calls: the native client
				// copies the key bytes synchronously inside GetValuesAsync, so no per-call key allocation is needed
				var keys = new Slice[batch];
				var buffers = new byte[batch][];
				for (int i = 0; i < batch; i++)
				{
					var buf = new byte[24];
					"bench/storm/"u8.CopyTo(buf);
					BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(12), workerId);
					buffers[i] = buf;
					keys[i] = buf.AsSlice();
				}
				long seq = 0;

				while (!stop.IsCancellationRequested)
				{
					try
					{
						using var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, ct);
						var snapshot = tr.Snapshot;
						var age = Stopwatch.StartNew();

						while (age.Elapsed < TimeSpan.FromSeconds(3) && !stop.IsCancellationRequested)
						{
							for (int i = 0; i < batch; i++)
							{
								BinaryPrimitives.WriteInt64BigEndian(buffers[i].AsSpan(16), seq++);
							}
							long t0 = Stopwatch.GetTimestamp();
							await snapshot.GetValuesAsync(keys).ConfigureAwait(false);
							StormHistogram.Record(Stopwatch.GetTimestamp() - t0);
							Interlocked.Increment(ref ops);
						}
					}
					catch (FdbException)
					{
						// transient cluster pushback: rotate the transaction and keep going
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			}

			var tasks = new Task[workers];
			for (int i = 0; i < workers; i++) { tasks[i] = Worker(i); }
			return (Task.WhenAll(tasks), () => Volatile.Read(ref ops));
		}

	}

}
