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

namespace FoundationDB.Samples.Tutorials
{
	using FoundationDB.Layers.Messaging;

	public class MessageQueueRunner : IAsyncTest
	{

		public enum AgentRole
		{
			Producer,
			Worker,
			Clear,
			Status
		}

		public MessageQueueRunner(ISubspaceLocation location, string id, AgentRole role, TimeSpan delayMin, TimeSpan delayMax)
		{
			this.Location = location;
			this.Id = id;
			this.Role = role;
			this.DelayMin = delayMin;
			this.DelayMax = delayMax;
			this.TimeLine = new RobustTimeLine(
				TimeSpan.FromSeconds(5),
				onCompleted: (histo, idx) =>
				{
					Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,3} | {1} | {2,6:#,##0.0} ms (+/- {3:#0.0})", idx, histo.GetDistribution(1, 5000 - 1), histo.Median, histo.MAD()));
					if (idx % 30 == 29)
					{
						Console.WriteLine(this.TimeLine?.MergeResults().GetReport(true));
						return true;
					}
					return false;
				}
			);

			this.WorkerPool = new FdbWorkerPool(this.Location);
		}

		public string Id { get; }

		public AgentRole Role { get; }

		public TimeSpan DelayMin { get; }

		public TimeSpan DelayMax { get; }

		public FdbWorkerPool? WorkerPool { get; }

		public RobustTimeLine TimeLine { get; }

		public ISubspaceLocation Location { get; }

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			// open the folder where we will store everything
			await db.WriteAsync(async tr => { await db.DirectoryLayer.CreateOrOpenAsync(tr, this.Location.Path); }, ct);
		}

		/// <summary>
		/// Simulate a student that is really indecisive
		/// </summary>
		public async Task RunProducer(IFdbDatabase db, CancellationToken ct)
		{
			int cnt = 0;

			var rnd = new Random();
			this.TimeLine.Start();
			while (!ct.IsCancellationRequested)
			{
				int k = cnt++;
				var taskId = TuPack.EncodeKey(this.Id.GetHashCode(), k);

				string msg = "Message #" + k + " from producer " + this.Id + " (" + DateTime.UtcNow.ToString("O") + ")";

				var latency = Stopwatch.StartNew();
				await this.WorkerPool!.ScheduleTaskAsync(db, taskId, Slice.FromString(msg), ct).ConfigureAwait(false);
				latency.Stop();
				Console.Write(k.ToString("N0") + "\r");

				this.TimeLine.Add(latency.Elapsed.TotalMilliseconds);

				TimeSpan delay = TimeSpan.FromTicks(rnd.Next((int)this.DelayMin.Ticks, (int)this.DelayMax.Ticks));
				await Task.Delay(delay, ct).ConfigureAwait(false);
			}
			this.TimeLine.Stop();

			ct.ThrowIfCancellationRequested();

		}

		public async Task RunConsumer(IFdbDatabase db, CancellationToken ct)
		{
			var rnd = new Random();

			int received = 0;

			this.TimeLine.Start();
			await this.WorkerPool!.RunWorkerAsync(db, async (msg, _) =>
			{
				var latency = msg.Received - msg.Scheduled;
				Interlocked.Increment(ref received);

				Console.Write($"[{received:N0} msg, ~{latency.TotalMilliseconds:N3} ms] {msg.Id:P}            \r");

				this.TimeLine.Add(latency.TotalMilliseconds);

				TimeSpan delay = TimeSpan.FromTicks(rnd.Next((int)this.DelayMin.Ticks, (int)this.DelayMax.Ticks));
				await Task.Delay(delay, ct).ConfigureAwait(false);
			}, ct);
			this.TimeLine.Stop();

		}

		public async Task RunClear(IFdbDatabase db, CancellationToken ct)
		{
			// clear everything
			await db.WriteAsync(async tr =>
			{
				var subspace = await this.Location.Resolve(tr);
				tr.ClearRange(subspace);
			}, ct);
		}

		public async Task RunStatus(IFdbDatabase db, CancellationToken ct)
		{
			//var countersLocation = this.WorkerPool!.Location.Partition.ByKey(Slice.FromChar('C'));
			//var idleLocation = this.WorkerPool.Location.Partition.ByKey(Slice.FromChar('I'));
			//var busyLocation = this.WorkerPool.Location.Partition.ByKey(Slice.FromChar('B'));
			//var tasksLocation = this.WorkerPool.Location.Partition.ByKey(Slice.FromChar('T'));
			//var unassignedLocation = this.WorkerPool.Location.Partition.ByKey(Slice.FromChar('U'));

			await db.ReadAsync(async tr =>
			{
				var subspace = await this.Location.Resolve(tr);

				var counters = await tr.Snapshot
					.GetRange(subspace.Key(FdbWorkerPool.COUNTERS).ToRange())
					.Select(kvp => new KeyValuePair<string, long>(subspace.DecodeLast<string>(kvp.Key)!, kvp.Value.ToInt64()))
					.ToListAsync()
					.ConfigureAwait(false);

				Console.WriteLine("Status at " + DateTimeOffset.Now.ToString("O"));
				foreach (var counter in counters)
				{
					Console.WriteLine(" - " + counter.Key + " = " + counter.Value);
				}

				Console.WriteLine("Dump:");

				Console.WriteLine("> Idle");
				await foreach(var kvp in tr.Snapshot.GetRange(subspace.Key(FdbWorkerPool.IDLE).ToRange()))
				{
					Console.WriteLine($"- Idle.{subspace.Unpack(kvp.Key)[1..]} = {kvp.Value:V}");
				}

				Console.WriteLine("> Busy");
				await tr.Snapshot.GetRange(subspace.Key(FdbWorkerPool.BUSY).ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine($"- Busy.{subspace.Unpack(kvp.Key)[1..]} = {kvp.Value:V}");
				});

				Console.WriteLine("> Unassigned");
				await tr.Snapshot.GetRange(subspace.Key(FdbWorkerPool.UNASSIGNED).ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine($"- Unassigned.{subspace.Unpack(kvp.Key)[1..]} = {kvp.Value:V}");
				});

				Console.WriteLine("> Tasks");
				await tr.Snapshot.GetRange(subspace.Key(FdbWorkerPool.TASKS).ToRange()).ForEachAsync((kvp) =>
				{
					Console.WriteLine($"- Tasks.{subspace.Unpack(kvp.Key)[1..]} = {kvp.Value:V}");
				});

				Console.WriteLine("<");
			}, ct);
		}

		#region IAsyncTest...

		public string Name => "MessageQueueTest";

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			await Init(db, ct);
			log.WriteLine("Message queue test initialized");

			switch(this.Role)
			{
				case AgentRole.Worker:
				{
					log.WriteLine("Running as Consumer '" + this.Id + "'");
					await RunConsumer(db, ct);
					break;
				}
				case AgentRole.Producer:
				{
					log.WriteLine("Running as Producer '" + this.Id + "'");
					await RunProducer(db, ct);
					break;
				}
				case AgentRole.Clear:
				{
					log.WriteLine("Running as Clear '" + this.Id + "'");
					await RunClear(db, ct);
					break;
				}
				case AgentRole.Status:
				{
					log.WriteLine("Running as Observer '" + this.Id + "'");
					await RunStatus(db, ct);
					break;
				}
			}
		}

		#endregion

	}

}
