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

// ReSharper disable AccessToModifiedClosure

namespace FoundationDB.Samples.Benchmarks
{
	using FoundationDB.Client.Utils;

	public class LeakTest : IAsyncTest
	{

		public LeakTest(ISubspaceLocation location, int k, int m, int n, TimeSpan delay)
		{
			this.Location = location;
			this.K = k;
			this.M = m;
			this.N = n;
			this.Delay = delay;
		}

		public ISubspaceLocation Location { get; }

		public int K { get; }

		public int M { get; }

		public int N { get; }

		public TimeSpan Delay { get; }

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			await db.WriteAsync(async tr =>
			{
				// open the folder where we will store everything
				var subspace = await db.DirectoryLayer.CreateOrOpenAsync(tr, this.Location.Path);

				// clear all previous values
				tr.ClearRange(subspace);

				// insert all the classes
				tr.Set(subspace.First(), FdbValue.ToTextUtf8("BEGIN"));
				tr.Set(subspace.Last(), FdbValue.ToTextUtf8("END"));
			}, ct);
		}

		/// <summary>
		/// Simulate a student that is really indecisive
		/// </summary>
		public async Task RunWorker(IFdbDatabase db, int id,  CancellationToken ct)
		{
			string student = "WORKER" + id.ToString("D04");

			var rnd = new Random(id * 7);
			var values = new string[this.M];
			for (int i = 0; i < values.Length;i++)
			{
				values[i] = "initial_value_" + rnd.Next();
			}

			for (int i = 0; i < 1/*this.N*/ && !ct.IsCancellationRequested; i++)
			{
				// randomly mutate values
				var n = rnd.Next(values.Length / 2);
				for (int j = 0; j < n;j++)
				{
					values[rnd.Next(values.Length)] = "value_" + i.ToString() + "_" + rnd.Next().ToString();
				}

				long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
				// write everything

				await db.WriteAsync(async (tr) =>
				{
					if (tr.Context.Retries > 0) Console.Write("!");
					var subspace = await this.Location.Resolve(tr);
					for (int j = 0; j < values.Length; j++)
					{
						tr.Set(subspace.Key(student, j, now), FdbValue.ToTextUtf8(values[j] + new string('A', 100)));
					}
				}, ct);
				Console.Write(".");

				//var r = await db.ReadAsync(async (tr) =>
				//{
				//	if (tr.Context.Retries > 0) Console.Write("!");
				//	return await Task.WhenAll(Enumerable.Range(0, values.Length).Select(x => tr.GetRange(FdbKeyRange.StartsWith(prefix.Pack(x))).LastOrDefaultAsync()));
				//}, ct);
				//if (i % 10 == 0) Console.Write(":");

				//await Task.Delay(this.Delay);
			}

			ct.ThrowIfCancellationRequested();

		}

		#region IAsyncTest...

		public string Name { get { return "LeakTest"; } }

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			await Init(db, ct);
			Console.WriteLine("# Leak test initialized");

			ThreadPool.SetMinThreads(100, 100);

			DateTime start = DateTime.Now;
			long workingSet = Environment.WorkingSet;
			long totalMemory = GC.GetTotalMemory(false);

			void TimerHandler(object? _)
			{
				var now = DateTime.Now;
				long ws = Environment.WorkingSet;
				long tm = GC.GetTotalMemory(false);
				var sb = new StringBuilder();
				sb.AppendLine();
				sb.AppendLine("T+" + (now - start).TotalSeconds.ToString("N1") + "s : WS=" + ws.ToString("N0") + " bytes (" + (ws - workingSet).ToString("N0") + "), NM=" + tm.ToString("N0") + " bytes (" + (tm - totalMemory).ToString("N0") + ")");
				sb.AppendLine("  trans: " + DebugCounters.TransactionHandlesTotal.ToString("N0") + " (" + DebugCounters.TransactionHandles + "), futures: " + DebugCounters.FutureHandlesTotal.ToString("N0") + " (" + DebugCounters.FutureHandles + "), callbacks: " + DebugCounters.CallbackHandlesTotal.ToString("N0") + "(" + DebugCounters.CallbackHandles + ")");
				if (OperatingSystem.IsWindows())
				{
					sb.AppendLine("  cpu: " + PerfCounters.ProcessorTime!.NextValue().ToString("N1") + "%, private: " + PerfCounters.PrivateBytes!.NextValue().ToString("N0") + ", gen0: " + PerfCounters.ClrGen0Collections!.NextValue() + ", gen1: " + PerfCounters.ClrGen1Collections!.NextValue() + ", gen2: " + PerfCounters.ClrGen2Collections!.NextValue());
				}

				Console.Write(sb.ToString());
				workingSet = ws;
				totalMemory = tm;
				/*
				if (now - last >= TimeSpan.FromMinutes(1))
				{
					Console.Write("GC...");
					GC.Collect();
					GC.WaitForPendingFinalizers();
					GC.Collect();
					Console.WriteLine(" Done");
					last = now;
				}
				 */
			}

			TimerHandler(null);

			using (new Timer(TimerHandler, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)))
			{
				for (int k = 0; k < this.N; k++)
				{
					// run multiple students
					var elapsed = await Program.RunConcurrentWorkersAsync(
						this.K,
						(i, _ct) => RunWorker(db, i, _ct),
						ct
					);

					Console.WriteLine();
					Console.WriteLine("# Ran {0} workers in {1:0.0##} sec", this.K, elapsed.TotalSeconds);

					await Task.Delay(this.Delay, ct);
				}
			}

			TimerHandler(null);
		}

#endregion

	}
}
