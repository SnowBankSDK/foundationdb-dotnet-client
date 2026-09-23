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

// ReSharper disable MethodHasAsyncOverload

namespace FoundationDB.Samples.Benchmarks
{
	public class BenchRunner : IAsyncTest
	{

		public enum BenchMode
		{
			GetReadVersion,
			Set,
			Get,
			Get10,
			GetRange,
			Watch,
			Mix,
		}

		public BenchRunner(BenchMode mode, int value = 1)
		{
			this.Mode = mode;
			this.Value = value;
			this.Histo = new RobustHistogram();
		}

		public string Name => "Bench" + this.Mode.ToString();

		public int Value { get; set; }

		public BenchMode Mode { get; }

		// The benchmark measures the latency of one operation, so it resolves the directory once instead of in every
		// transaction; nothing moves the directory during the run.
#pragma warning disable FDB1001
		public IKeySubspace? Subspace { get; private set; }
#pragma warning restore FDB1001

		public RobustHistogram Histo { get; }

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			// open the folder where we will store everything
#pragma warning disable FDB1002 // resolved once on purpose, see the Subspace property
			this.Subspace = await db.ReadWriteAsync(tr => db.Root["Benchmarks"].CreateOrOpenAsync(tr), ct);
#pragma warning restore FDB1002
		}

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			const int WORKERS = 1;
			const int RUN_IN_SECONDS = 100;

			await Init(db, ct);
			log.WriteLine("Initialized for " + this.Mode.ToString());

			var timeline = new RobustTimeLine(
				TimeSpan.FromSeconds(1),
				RobustHistogram.TimeScale.Milliseconds,
				(histo, idx) =>
				{
					if (idx == 0)
					{
						Console.WriteLine("T+s | " + RobustHistogram.GetScale(1, 5000 - 1, RobustHistogram.HorizontalScale) + " | ");
					}
					Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,3} | {1} | {2,6:#,##0.0} ms (+/- {3:#0.000})", idx, histo.GetDistribution(1, 5000 - 1), histo.Median, histo.MAD()));
					if (log != Console.Out) log.WriteLine(histo.GetReport(false));
					return false;
				}
			);

			var foo = this.Subspace!.Key("foo");
			var bar = Slice.FromString("bar");
			var barf = Slice.FromString("barf");

			long total = 0;

			timeline.Start();
			var elapsed = await Program.RunConcurrentWorkersAsync(
				WORKERS,
				async (_, _) =>
				{
					var dur = Stopwatch.StartNew();
					int k = 0;
					while (dur.Elapsed.TotalSeconds < RUN_IN_SECONDS)
					{
						var sw = Stopwatch.StartNew();
						switch(this.Mode)
						{
							case BenchMode.GetReadVersion:
							{
								await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
								break;
							}
							case BenchMode.Get:
							{
								if (this.Value <= 1)
								{
									await db.ReadAsync(tr => tr.GetAsync(foo), ct);
								}
								else
								{
									await db.ReadAsync(tr => tr.GetValuesAsync(Enumerable.Range(1, this.Value), i => foo.Key(i)), ct);
								}
								break;
							}
							case BenchMode.Set:
							{
								await db.WriteAsync(tr => tr.Set(foo, bar), ct);
								break;
							}
							case BenchMode.Watch:
							{
								var (v, w) = await db.ReadWriteAsync(async tr => (await tr.GetAsync(foo), tr.Watch(foo, ct)), ct);

								// swap
								v = (v == bar) ? barf : bar;

								await db.WriteAsync((tr) => tr.Set(foo, v), ct);

								await w;

								break;
							}
						}
						sw.Stop();

						timeline.Add(sw.Elapsed.TotalMilliseconds);
						Console.Write(k.ToString() + "\r");

						++k;
						Interlocked.Increment(ref total);
					}
				},
				ct
			);
			timeline.Stop();
			Console.WriteLine("Done       ");
			Console.WriteLine("# Ran {0} transactions in {1:0.0##} sec", total, elapsed.TotalSeconds);

			var global = timeline.MergeResults();

			log.WriteLine("# Merged results:");
			log.WriteLine(global.GetReport(true));

			if (log != Console.Out)
			{
				Console.WriteLine("# Merged results:");
				Console.WriteLine(global.GetReport(true));
			}
		}

	}
}
