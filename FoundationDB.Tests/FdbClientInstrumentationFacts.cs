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

namespace FoundationDB.Client.Tests
{
	using System.Diagnostics.Metrics;

	[TestFixture]
	[NonParallelizable] // the meter is process-wide: a concurrent test would add its own measurements
	public class FdbClientInstrumentationFacts : FdbTest
	{

		private sealed record Measurement(string Instrument, double Value, Dictionary<string, object?> Tags);

		private sealed class MeterProbe : IDisposable
		{

			public MeterProbe()
			{
				this.Listener = new();
				this.Listener.InstrumentPublished = (instrument, listener) =>
				{
					if (instrument.Meter.Name != FdbClientInstrumentation.MeterName) return;
					lock (this.Published)
					{
						this.Published.Add(instrument.Name);
					}
					listener.EnableMeasurementEvents(instrument);
				};
				this.Listener.SetMeasurementEventCallback<int>((i, v, t, _) => Capture(i, v, t));
				this.Listener.SetMeasurementEventCallback<long>((i, v, t, _) => Capture(i, v, t));
				this.Listener.SetMeasurementEventCallback<double>((i, v, t, _) => Capture(i, v, t));
				this.Listener.Start();
			}

			private MeterListener Listener { get; }

			public HashSet<string> Published { get; } = [ ];

			private List<Measurement> Captured { get; } = [ ];

			private void Capture(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
			{
				var flat = new Dictionary<string, object?>(StringComparer.Ordinal);
				foreach (var tag in tags)
				{
					flat[tag.Key] = tag.Value;
				}
				lock (this.Captured)
				{
					this.Captured.Add(new(instrument.Name, value, flat));
				}
			}

			public void Clear()
			{
				lock (this.Captured)
				{
					this.Captured.Clear();
				}
			}

			public List<Measurement> Snapshot(string instrument)
			{
				lock (this.Captured)
				{
					return this.Captured.Where(m => m.Instrument == instrument).ToList();
				}
			}

			public void Dispose() => this.Listener.Dispose();

		}

		[Test]
		public async Task Test_A_Committed_Write_Records_One_Completed_Operation()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			using var probe = new MeterProbe();
			probe.Clear();

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("Hello"), Text("World"));
			}, this.Cancellation);

			var durations = probe.Snapshot("db.fdb.client.operations.duration");
			Log($"operations.duration: {string.Join(", ", durations.Select(m => $"{m.Value:R} {string.Join(",", m.Tags)}"))}");
			Assert.That(durations, Has.Count.EqualTo(1), "a write that commits on its first attempt completes one operation");
			Assert.That(durations[0].Tags["operation.status"], Is.EqualTo(FdbError.Success));
			Assert.That(durations[0].Tags["operation.retries"], Is.EqualTo(0));

			Assert.That(probe.Snapshot("db.fdb.client.operations.size"), Has.Count.EqualTo(1), "the committed attempt records its size once");
			Assert.That(probe.Published, Does.Not.Contain("db.fdb.client.operations.completed"), "the count of the duration histogram gives the completed operations");
		}

		[Test]
		public async Task Test_A_Failed_Attempt_Records_Its_Size_With_Its_Error()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			using var probe = new MeterProbe();
			probe.Clear();

			// transaction_too_large is not retryable: the attempt fails once, with a non-empty transaction
			Assert.That(async () => await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("Hello"), Text("World"));
				throw new FdbException(FdbError.TransactionTooLarge);
			}, this.Cancellation), Throws.InstanceOf<FdbException>());

			var sizes = probe.Snapshot("db.fdb.client.operations.size");
			Log($"operations.size: {string.Join(", ", sizes.Select(m => $"{m.Value:R} {string.Join(",", m.Tags)}"))}");
			Assert.That(sizes, Has.Count.EqualTo(1), "the failed attempt records its size");
			Assert.That(sizes[0].Value, Is.GreaterThan(0));
			Assert.That(sizes[0].Tags["operation.status"], Is.EqualTo(FdbError.TransactionTooLarge), "the size of a failed attempt is tagged with the error of that attempt");

			var durations = probe.Snapshot("db.fdb.client.operations.duration");
			Assert.That(durations, Has.Count.EqualTo(1));
			Assert.That(durations[0].Tags["operation.status"], Is.EqualTo(FdbError.TransactionTooLarge));
		}

	}

}
