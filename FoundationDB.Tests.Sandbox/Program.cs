#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Tests.Sandbox
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using SnowBank.Data.Tuples;
	using FoundationDB.Client;

	class Program
	{
		private static int N;
		private static string NATIVE_PATH;
		private static string CLUSTER_FILE;
		private static bool WARNING;
		private static string SUBSPACE;

		public static void Main(string[] args)
		{
			N = 10 * 1000;
			NATIVE_PATH = null; // set this to the path of the 'bin' folder in your fdb install, like @"C:\Program Files\foundationdb\bin"
			CLUSTER_FILE = null; // set this to the path to your custom fluster file
			SUBSPACE = "Sandbox";
			WARNING = true;

			for (int i = 0; i < args.Length; i++)
			{
				if (args[i].StartsWith("-") || args[i].StartsWith("/"))
				{
					string cmd = args[i].Substring(1);
					string param = String.Empty;
					int p = cmd.IndexOf('=');
					if (p > 0)
					{
						param = cmd.Substring(p + 1);
						cmd = cmd.Substring(0, p);
						if (param.Length >= 2 && param[0] == '\"' && param[^1] == '\"')
						{
							param = param.Substring(1, param.Length - 2);
						}
					}

					switch (cmd.ToLowerInvariant())
					{
						case "cluster":
						{
							CLUSTER_FILE = param;
							break;
						}
						case "no-warn":
						{
							WARNING = false;
							break;
						}
						case "subspace":
						{
							SUBSPACE = param;
							break;
						}
						case "help":
						{
							DisplayHelp();
							return;
						}
					}
				}
			}

			// Make sure we are on 64 bit
			if (IntPtr.Size == 4)
			{
				Console.Error.WriteLine("This process cannot be run in 32-bit mode !");
				Environment.Exit(-1);
			}

			// Warn the user
			if (WARNING)
			{
				Console.WriteLine("WARNING! WARNING! WARNING!");
				Console.WriteLine("This program will clear all data from your database!");
				Console.WriteLine("Are you sure ? CTRL-C to exit, ENTER to continue");
				Console.ReadLine();
			}

			AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			{
				if (e.IsTerminating)
				{
					Console.Error.WriteLine("Fatal unhandled exception: " + e.ExceptionObject);
				}
				else
				{
					Console.Error.WriteLine("Unhandled exception: " + e.ExceptionObject);
				}
			};

			using (var go = new CancellationTokenSource())
			{
				try
				{
					ExecuteAsync(MainAsync, go.Token);
				}
				catch (Exception e)
				{
					if (e is AggregateException aggEx)
					{
						e = aggEx.Flatten().InnerException!;
					}

					Console.Error.WriteLine("Oops! something went wrong:");
					Console.Error.WriteLine(e.ToString());
					go.Cancel();
					Environment.ExitCode = -1;
				}
			}
			Console.WriteLine("[PRESS A KEY TO EXIT]");
			Console.ReadKey();
		}

		private static void DisplayHelp()
		{
			Console.WriteLine("Syntax: fdbsandbox [options] [tests]");
			Console.WriteLine("Options:");
			Console.WriteLine("\t-cluster=path\t\tPath to the cluster file to use");
			Console.WriteLine("\t-db=name\t\tName of the database (defaults to 'DB')");
			Console.WriteLine("\t-subspace=prefix\t\tPrefix to use for all tests (defaults to 'Sandbox')");
			Console.WriteLine("\t-no-warn\t\tDisable warning before clearing the subspace");
			Console.WriteLine("\t-help\t\tShow this help text");
		}

		private static async Task MainAsync(CancellationToken ct)
		{
			// change the path to the native lib if not default
			if (NATIVE_PATH != null) Fdb.Options.SetNativeLibPath(NATIVE_PATH);

			// uncomment this to enable network thread tracing
			// FdbCore.TracePath = Path.Combine(Path.GetTempPath(), "fdb");

			int apiVersion = Fdb.GetMaxApiVersion();
			Console.WriteLine("Max API Version: " + apiVersion);

			try
			{
				Console.WriteLine("Starting network thread...");
				Fdb.Start(Fdb.GetDefaultApiVersion());
				Console.WriteLine("> Up and running");

				var settings = new FdbConnectionOptions()
				{
					ClusterFile = CLUSTER_FILE,
					Root = FdbPath.Parse("/Sandbox"),
				};

				Console.WriteLine("Connecting to local cluster...");
				using (var db = await Fdb.OpenAsync(settings, ct))
				{
					Console.WriteLine("> Connected!");

					// get coordinators
					var cf = await Fdb.System.GetCoordinatorsAsync(db, ct);
					Console.WriteLine($"Coordinators: {cf}");

					// clear everything
					using (var tr = db.BeginTransaction(ct))
					{
						Console.WriteLine($"Clearing subspace {db.Root} ...");
						var subspace = await db.Root.Resolve(tr);
						tr.ClearRange(subspace);
						await tr.CommitAsync();
						Console.WriteLine("> Database cleared");
					}

					Console.WriteLine();

					await TestSimpleTransactionAsync(db, ct);

					await BenchInsertSmallKeysAsync(db, N, 16, ct); // some guid
					await BenchInsertSmallKeysAsync(db, N, 60 * 4, ct); // one Int32 per minutes, over an hour
					await BenchInsertSmallKeysAsync(db, N, 512, ct); // small JSON payload
					await BenchInsertSmallKeysAsync(db, N / 5, 4096, ct); // typical small cunk size
					await BenchInsertSmallKeysAsync(db, N / 100, 65536, ct); // typical medium chunk size
					await BenchInsertSmallKeysAsync(db, 20, 100_000, ct); // Maximum value size (as of beta 1)

					// insert keys in parrallel
					await BenchConcurrentInsert(db, 1,    100, 512, ct);
					await BenchConcurrentInsert(db, 1,  1_000, 512, ct);
					await BenchConcurrentInsert(db, 1, 10_000, 512, ct);

					await BenchConcurrentInsert(db, 1, N, 16, ct);
					await BenchConcurrentInsert(db, 2, N, 16, ct);
					await BenchConcurrentInsert(db, 4, N, 16, ct);
					await BenchConcurrentInsert(db, 8, N, 16, ct);
					await BenchConcurrentInsert(db, 16, N, 16, ct);

					await BenchSerialWriteAsync(db, N, ct);
					await BenchSerialReadAsync(db, N, ct);
					await BenchConcurrentReadAsync(db, N, ct);

					await BenchClearAsync(db, N, ct);

					await BenchUpdateSameKeyLotsOfTimesAsync(db, 1000, ct);

					await BenchUpdateLotsOfKeysAsync(db, 1000, ct);

					await BenchBulkInsertThenBulkReadAsync(db,   100_000,  50, 128, ct);
					await BenchBulkInsertThenBulkReadAsync(db,   100_000, 128,  50, ct);
					await BenchBulkInsertThenBulkReadAsync(db, 1_000_000,  50, 128, ct);

					await BenchMergeSortAsync(db,   100,     3,  20, ct);
					await BenchMergeSortAsync(db, 1_000,    10, 100, ct);
					await BenchMergeSortAsync(db,   100,   100, 100, ct);
					await BenchMergeSortAsync(db,   100, 1_000, 100, ct);

					Console.WriteLine("time to say goodbye...");
				}
			}
			finally
			{
				Console.WriteLine("### DONE ###");
				Fdb.Stop();
			}
#if DEBUG
			Console.ReadLine();
#endif
		}

		#region Tests...

		private static async Task HelloWorld(CancellationToken ct)
		{

			// Connect to the "DB" database on the local cluster
			using (var db = await Fdb.OpenAsync(ct))
			{

				// Writes some data in to the database
				using (var tr = db.BeginTransaction(ct))
				{
					tr.Set(TuPack.EncodeKey("Test", 123), FdbValue.ToTextUtf8("Hello World!"));
					tr.Set(TuPack.EncodeKey("Test", 456), Slice.FromInt64(DateTime.UtcNow.Ticks));
				}

			}

		}

		private static async Task TestSimpleTransactionAsync(IFdbDatabase db, CancellationToken ct)
		{
			Console.WriteLine("=== TestSimpleTransaction() ===");

			Console.WriteLine("Starting new transaction...");
			using (var trans = db.BeginTransaction(ct))
			{
				Console.WriteLine("> Transaction ready");

				Console.WriteLine("Getting read version...");
				var readVersion = await trans.GetReadVersionAsync();
				Console.WriteLine("> Read Version = " + readVersion);

				Console.WriteLine("Resolving root location...");
				var location = await db.Root.Resolve(trans);
				Console.WriteLine("> " + location);

				Console.WriteLine("Getting 'hello'...");
				var result = await trans.GetAsync(location.Key("hello"));
				if (result.IsNull)
					Console.WriteLine("> hello NOT FOUND");
				else
					Console.WriteLine($"> hello = {result:V}");

				Console.WriteLine("Setting 'Foo' = 'Bar'");
				trans.Set(location.Key("Foo"), FdbValue.ToTextUtf8("Bar"));

				Console.WriteLine("Setting 'TopSecret' = rnd(512)");
				var data = Slice.Random(new Random(1234), 512);
				trans.Set(location.Key("TopSecret"), data);

				Console.WriteLine("Committing transaction...");
				await trans.CommitAsync();
				//trans.Commit();
				Console.WriteLine("> Committed!");

				Console.WriteLine("Getting committed version...");
				var writeVersion = trans.GetCommittedVersion();
				Console.WriteLine("> Commited Version = " + writeVersion);
			}
		}

		private static async Task BenchInsertSmallKeysAsync(IFdbDatabase db, int N, int size, CancellationToken ct)
		{
			// insert a lot of small key size, in a single transaction

			Console.WriteLine($"=== BenchInsertSmallKeys(N={N:N0}, size={size:N0}) ===");

			var rnd = new Random();
			var tmp = new byte[size];

			var location = db.Root["Batch"];

			var times = new List<TimeSpan>();
			for (int k = 0; k <= 4; k++)
			{
				var sw = Stopwatch.StartNew();
				using (var trans = db.BeginTransaction(ct))
				{
					var subspace = await location.Resolve(trans);

					rnd.NextBytes(tmp);
					for (int i = 0; i < N; i++)
					{
						tmp[0] = (byte)i;
						tmp[1] = (byte)(i >> 8);
						// (Batch, 1) = [......]
						// (Batch, 2) = [......]
						trans.Set(subspace.Key(k * N + i), tmp.AsSlice());
					}
					await trans.CommitAsync();
				}
				sw.Stop();
				times.Add(sw.Elapsed);
			}
			var min = times.Min();
			var avg = times.Sum(x => x.TotalMilliseconds)/times.Count;
			Console.WriteLine($"[{Environment.CurrentManagedThreadId}] Took {min.TotalSeconds.ToString("N3", CultureInfo.InvariantCulture)} sec to insert {N} {size}-bytes items (min={FormatTimeMicro(min.TotalMilliseconds / N)}/write, avg={FormatTimeMicro(avg)}/write)");
			Console.WriteLine();
		}

		private static async Task BenchConcurrentInsert(IFdbDatabase db, int k, int N, int size, CancellationToken ct)
		{
			// insert a lot of small key size, in multiple batch running in //
			// k = number of threads
			// N = total number of keys
			// size = value size (bytes)
			// n = keys per batch (N/k)

			int n = N / k;
			// make sure that N is multiple of k
			N = n * k;

			Console.WriteLine($"=== BenchConcurrentInsert(k={k:N0}, N={N:N0}, size={size:N0}) ===");
			Console.WriteLine($"Inserting {N:N0} keys in {k:N0} batches of {n:N0} with {size:N0}-bytes values...");

			// store every key under ("Batch", i)
			// total estimated size of all transactions
			long totalPayloadSize = 0;

			var location = db.Root;

			var tasks = new List<Task>();
			var sem = new ManualResetEventSlim();
			for (int j = 0; j < k; j++)
			{
				int offset = j;
				// spin a task for the batch using TaskCreationOptions.LongRunning to make sure it runs in its own thread
				tasks.Add(Task.Factory.StartNew(async () =>
				{
					var rnd = new Random(1234567 * offset);
					var tmp = new byte[size];
					rnd.NextBytes(tmp);

					// block until all threads are ready
					sem.Wait(ct);

					var x = Stopwatch.StartNew();
					using (var trans = db.BeginTransaction(ct))
					{
						x.Stop();
						//Console.WriteLine($"> [{offset}] got transaction in {FormatTimeMilli(x.Elapsed.TotalMilliseconds)}");

						var subspace = await location.Resolve(trans);

						// package the keys...
						x.Restart();
						for (int i = 0; i < n; i++)
						{
							// change the value a little bit
							tmp[0] = (byte)i;
							tmp[1] = (byte)(i >> 8);

							// ("Batch", batch_index, i) = [..random..]
							trans.Set(subspace.Bytes(Slice.FromFixed64BE(i)), tmp.AsSlice());
						}
						x.Stop();
						//Console.WriteLine($"> [{offset}] packaged {n:N0} keys ({trans.Size:N0} bytes) in {FormatTimeMilli(x.Elapsed.TotalMilliseconds)}");

						// commit the transaction
						x.Restart();
						await trans.CommitAsync();
						x.Stop();
						//Console.WriteLine($"> [{offset}] committed {n} keys ({trans.Size:N0} bytes) in {FormatTimeMilli(x.Elapsed.TotalMilliseconds)}");

						Interlocked.Add(ref totalPayloadSize, trans.Size);
					}

				}, TaskCreationOptions.LongRunning).Unwrap());
			}
			// give time for threads to be ready
			await Task.Delay(100, ct);

			// start
			var sw = Stopwatch.StartNew();
			sem.Set();

			// wait for total completion
			await Task.WhenAll(tasks);
			sw.Stop();
			Console.WriteLine($"* Total: {FormatTimeMilli(sw.Elapsed.TotalMilliseconds)}, {FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)} / write, {FormatThroughput(totalPayloadSize, sw.Elapsed.TotalSeconds)}, {N / sw.Elapsed.TotalSeconds:N0} write/sec");
			Console.WriteLine();
		}

		private static async Task BenchSerialWriteAsync(IFdbDatabase db, int N, CancellationToken ct)
		{
			// read a lot of small keys, one by one

			Console.WriteLine($"=== BenchSerialWrite(N={N:N0}) ===");

			var location = db.Root["Hello"];
			var sw = Stopwatch.StartNew();
			IFdbTransaction trans = null;
			IKeySubspace subspace = null;
			try
			{
				for (int i = 0; i < N; i++)
				{
					if (trans == null)
					{
						trans = db.BeginTransaction(ct);
						subspace = await location.Resolve(trans);
					}
					trans.Set(subspace.Key(i), Slice.FromInt32(i));
					if (trans.Size > 100 * 1024)
					{
						await trans.CommitAsync();
						trans.Dispose();
						trans = null;
					}
				}

				if (trans is not null)
				{
					await trans.CommitAsync();
				}
			}
			finally
			{
				trans?.Dispose();
			}
			sw.Stop();
			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to read {N:N0} items ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)}/read, {N/sw.Elapsed.TotalSeconds:N0} read/sec)");
			Console.WriteLine();
		}

		private static async Task BenchSerialReadAsync(IFdbDatabase db, int N, CancellationToken ct)
		{
			// read a lot of small keys, one by one

			Console.WriteLine($"=== BenchSerialRead(N={N:N0}) ===");
			Console.WriteLine($"Reading {N:N0} keys (serial, slow!)");

			var location = db.Root["Hello"];

			var sw = Stopwatch.StartNew();
			for (int k = 0; k < N; k += 1000)
			{
				using (var trans = db.BeginTransaction(ct))
				{
					var subspace = await location.Resolve(trans);
					for (int i = k; i < N && i < k + 1000; i++)
					{
						_ = await trans.GetAsync(subspace.Key(i));
					}
				}
				Console.Write(".");
			}
			Console.WriteLine();
			sw.Stop();
			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to read {N:N0} items ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)}/read, {N / sw.Elapsed.TotalSeconds:N0} read/sec)");
			Console.WriteLine();
		}

		private static async Task BenchConcurrentReadAsync(IFdbDatabase db, int N, CancellationToken ct)
		{
			// read a lot of small keys, concurrently

			Console.WriteLine($"=== BenchConcurrentRead(N={N:N0}) ===");
			Console.WriteLine($"Reading {N:N0} keys (concurrent)");

			var location = db.Root["Hello"];

			Console.WriteLine("# Task.WhenAll+GetAsync");
			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(trans);
				_ = await Task.WhenAll(Enumerable
					.Range(0, N)
					.Select((i) => trans.GetAsync(subspace.Key(i)))
				);
			}
			sw.Stop();
			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to read {N} items ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)}/read, {N / sw.Elapsed.TotalSeconds:N0} read/sec)");
			Console.WriteLine();

			Console.WriteLine("# GetValuesAsync");
			sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(trans);
				_ = await trans.GetValuesAsync(Enumerable.Range(0, N), i => subspace.Key(i));
			}
			sw.Stop();
			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to read {N:N0} items ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)}/read, {N / sw.Elapsed.TotalSeconds:N0} read/sec)");
			Console.WriteLine();

			Console.WriteLine("# GetBatchAsync");
			sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(trans);
				_ = await trans.GetBatchAsync(Enumerable.Range(0, N).Select(i => subspace.Key(i).ToSlice())); //PERF: optimize!
			}
			sw.Stop();
			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to read {N:N0} items ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)}/read, {N / sw.Elapsed.TotalSeconds:N0} read/sec)");
			Console.WriteLine();
		}

		private static async Task BenchClearAsync(IFdbDatabase db, int N, CancellationToken ct)
		{
			// clear a lot of small keys, in a single transaction

			Console.WriteLine($"=== BenchClear(N={N:N0}) ===");

			var location = db.Root["Hello"];

			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(trans);
				for (int i = 0; i < N; i++)
				{
					trans.Clear(subspace.Key(i));
				}

				await trans.CommitAsync();
			}
			sw.Stop();
			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to clear {N:N0} items ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)}/write, {N / sw.Elapsed.TotalSeconds:N0} clear/sec)");
			Console.WriteLine();
		}

		private static async Task BenchUpdateSameKeyLotsOfTimesAsync(IFdbDatabase db, int N, CancellationToken ct)
		{
			// continuously update same key by adding a little bit more

			Console.WriteLine($"=== BenchUpdateSameKeyLotsOfTimes(N={N:N0}) ===");
			Console.WriteLine($"Updating the same list {N:N0} times...");

			var list = new byte[N];
			var update = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				list[i] = (byte)i;
				using (var trans = db.BeginTransaction(ct))
				{
					var subspace = await db.Root.Resolve(trans);
					trans.Set(subspace.Key("list"), list.AsSlice());
					await trans.CommitAsync();
				}
				if (i % 100 == 0) Console.Write($"\r> {i:N0} / {N:N0}");
			}
			update.Stop();

			Console.WriteLine($"\rTook {update.Elapsed.TotalSeconds:N3} sec to fill a byte[{N:N0}] one by one ({FormatTimeMicro(update.Elapsed.TotalMilliseconds / N)}/update, {N / update.Elapsed.TotalSeconds:N0} update/sec)");
			Console.WriteLine();
		}

		private static async Task BenchUpdateLotsOfKeysAsync(IFdbDatabase db, int N, CancellationToken ct)
		{
			// change one byte in a large number of keys

			Console.WriteLine($"=== BenchUpdateLotsOfKeys(N={N:N0}) ===");

			var location = db.Root["Lists"];

			var rnd = new Random();

			Console.WriteLine($"> creating {N:N0} half filled keys");
			var segment = new byte[60];

			for (int i = 0; i < (segment.Length >> 1); i++) segment[i] = (byte) rnd.Next(256);
			using (var trans = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(trans);
				for (int i = 0; i < N; i += 1000)
				{
					for (int k = i; k < i + 1000 && k < N; k++)
					{
						trans.Set(subspace.Key(k), segment.AsSlice());
					}
					await trans.CommitAsync();
					Console.Write("\r" + i + " / " + N);
				}
			}

			Console.WriteLine($"\rChanging one byte in each of the {N:N0} keys...");
			var sw = Stopwatch.StartNew();
			using (var trans = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(trans);

				Console.WriteLine("READ");
				// get all the lists
				var data = await trans.GetValuesAsync(Enumerable.Range(0, N).Select(i => subspace.Key(i)));

				// change them
				Console.WriteLine("CHANGE");
				for (int i = 0; i < data.Length; i++)
				{
					Debug.Assert(!data[i].IsNull);
					var list = data[i].ToArray();
					list[(list.Length >> 1) + 1] = (byte) rnd.Next(256);
					trans.Set(subspace.Key(i), list.AsSlice());
				}

				Console.WriteLine("COMMIT");
				await trans.CommitAsync();
			}
			sw.Stop();

			Console.WriteLine($"Took {sw.Elapsed.TotalSeconds:N3} sec to patch one byte in {N:N0} lists ({FormatTimeMicro(sw.Elapsed.TotalMilliseconds / N)} /update, {N / sw.Elapsed.TotalSeconds:N0} update/sec)");
			Console.WriteLine();
		}

		private static async Task BenchBulkInsertThenBulkReadAsync(IFdbDatabase db, int N, int K, int B, CancellationToken ct, bool instrumented = false)
		{
			// test that we can bulk write / bulk read

			Console.WriteLine($"=== BenchBulkInsertThenBulkRead(N={N:N0}, K={K:N0}, B={B:N0}) ===");

			var timings = instrumented ? new List<KeyValuePair<double, double>>() : null;

			// put test values inside a namespace
			var location = db.Root["BulkInsert"];

			// cleanup everything
			using (var tr = db.BeginTransaction(ct))
			{
				tr.ClearRange(await location.Resolve(tr));
				await tr.CommitAsync();
			}

			// insert all values (batched)
			Console.WriteLine($"Inserting {N:N0} keys: ");
			var insert = Stopwatch.StartNew();
			int batches = 0;
			long bytes = 0;

			var start = Stopwatch.StartNew();

			var tasks = new List<Task>();
			foreach (var worker in FdbKey.Batched(0, N, K, B))
			{
				//hack
				tasks.Add(Task.Run(async () =>
				{
					foreach (var chunk in worker)
					{
						using (var tr = db.BeginTransaction(ct))
						{
							var subspace = await location.Resolve(tr);
							int z = 0;
							foreach (int i in Enumerable.Range(chunk.Key, chunk.Value))
							{
								tr.Set(subspace.Key(i), Slice.Zero(256));
								z++;
							}

							//Console.Write("#");
							//Console.WriteLine("  Commiting batch (" + tr.Size.ToString("N0", CultureInfo.InvariantCulture) + " bytes) " + z + " keys");
							var localStart = start.Elapsed.TotalSeconds;
							await tr.CommitAsync();
							var localDuration = start.Elapsed.TotalSeconds - localStart;
							if (instrumented)
							{
								lock (timings) { timings.Add(new(localStart, localDuration)); }
							}
							Interlocked.Increment(ref batches);
							Interlocked.Add(ref bytes, tr.Size);
						}

					}
				}, ct));

			}
			await Task.WhenAll(tasks);

			insert.Stop();
			Console.WriteLine($"Committed {batches:N0} batches in {FormatTimeMilli(insert.Elapsed.TotalMilliseconds)} ({FormatTimeMilli(insert.Elapsed.TotalMilliseconds / batches)} / batch, {FormatTimeMicro(insert.Elapsed.TotalMilliseconds / N)} / item)");
			Console.WriteLine($"Throughput {FormatThroughput(bytes, insert.Elapsed.TotalSeconds)}, {N / insert.Elapsed.TotalSeconds:N0} write/sec");

			if (instrumented)
			{
				var sb = new StringBuilder();
				foreach (var kvp in timings)
				{
					sb.Append(kvp.Key.ToString()).Append(';').Append((kvp.Key + kvp.Value).ToString()).Append(';').Append(kvp.Value.ToString()).AppendLine();
				}
#if DEBUG
				System.IO.File.WriteAllText(@"c:\temp\fdb\timings_" + N + "_" + K + "_" + B + ".csv", sb.ToString());
#else
				Console.WriteLine(sb.ToString());
#endif
			}

			// Read values

			using (var tr = db.BeginTransaction(ct))
			{
				Console.WriteLine("Reading all keys...");
				var subspace = await location.Resolve(tr);
				var sw = Stopwatch.StartNew();
				var items = await tr.GetRange(subspace.ToRange()).ToListAsync();
				sw.Stop();
				Console.WriteLine($"Took {FormatTimeMilli(sw.Elapsed.TotalMilliseconds)} to get {items.Count.ToString("N0", CultureInfo.InvariantCulture)} results ({items.Count / sw.Elapsed.TotalSeconds:N0} keys/sec)");
			}

			Console.WriteLine();
		}

		private static async Task BenchMergeSortAsync(IFdbDatabase db, int N, int K, int B, CancellationToken ct)
		{
			Console.WriteLine($"=== BenchMergeSort(N={N:N0}, K={K:N0}, B={B:N0}) ===");

			// create multiple lists
			var location = db.Root["MergeSort"];
			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.ClearRange(subspace);
			}, ct);

			var sources = Enumerable.Range(0, K).Select(i => 'A' + i).ToArray();
			var rnd = new Random();

			// insert a number of random number lists
			Console.Write($"> Inserting {(K * N):N0} items... ");
			foreach (var source in sources)
			{
				using (var tr = db.BeginTransaction(ct))
				{
					var list = await location.Resolve(tr);
					for (int i = 0; i < N; i++)
					{
						tr.Set(list.Key(source, rnd.Next()), Slice.FromInt32(i));
					}
					await tr.CommitAsync();
				}
			}
			Console.WriteLine("Done");

			// merge/sort them to get only one (hopefully sorted) list

			using (var tr = db.BeginTransaction(ct))
			{
				var subspace = await location.Resolve(tr);

				var mergesort = tr
					.MergeSort(
						sources.Select(source => KeySelectorPair.StartsWith(subspace.Key(source).ToSlice())),
						(kvp) => subspace.DecodeLast<int>(kvp.Key)
					)
					.Take(B)
					.Select(kvp => subspace.Unpack(kvp.Key));

				Console.Write($"> MergeSort with limit {B:N0}... ");
				var sw = Stopwatch.StartNew();
				var results = await mergesort.ToListAsync();
				sw.Stop();
				Console.WriteLine("Done");

				Console.WriteLine($"Took {FormatTimeMilli(sw.Elapsed.TotalMilliseconds)} to merge sort {results.Count:N0} results from {K} lists of {N} items each");

				//foreach (var result in results)
				//{
				//	Console.WriteLine(result.Get<int>(-1));
				//}
			}
			Console.WriteLine();
		}

		#endregion

		#region Helpers...

		private static void ExecuteAsync(Func<CancellationToken, Task> code, CancellationToken ct)
		{
			// poor man's async main loop
			Task.Run(() => code(ct), ct).GetAwaiter().GetResult();
		}

		private static string FormatTimeMilli(double ms)
		{
			return ms.ToString("N3", CultureInfo.InvariantCulture) + " ms";
		}

		private static string FormatTimeMicro(double ms)
		{
			return (ms * 1E3).ToString("N1", CultureInfo.InvariantCulture) + " µs";
		}

		private static string FormatThroughput(long bytes, double secs)
		{
			if (secs == 0) return "0";

			double bw = bytes / secs;

			if (bw < 1024) return bw.ToString("N0", CultureInfo.InvariantCulture) + " bytes/sec";
			if (bw < (1024 * 1024)) return (bw / 1024).ToString("N1", CultureInfo.InvariantCulture) + " kB/sec";
			return (bw / (1024 * 1024)).ToString("N3", CultureInfo.InvariantCulture) + " MB/sec";
		}

		#endregion

	}
}
