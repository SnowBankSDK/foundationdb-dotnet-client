#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{
	using SnowBank.Messaging.Events;

	public sealed class TestEventSink : IEventSink
	{

		public TestEventSink(Timeline timeline, string source)
		{
			this.Timeline = timeline;
			this.Source = source;
		}

		public Timeline Timeline { get; }

		public string Source { get; }

		public bool Async => false;

		public Task Dispatch(IEvent evt, CancellationToken ct)
		{
			this.Timeline.Record(Convert(evt, this.Source));
			return Task.CompletedTask;
		}

		public Task Dispatch(ReadOnlyMemory<IEvent> batch, CancellationToken ct)
		{
			var timeline = this.Timeline;
			var source = this.Source;
			foreach (var evt in batch.Span)
			{
				timeline.Record(Convert(evt, source));
			}
			return Task.CompletedTask;
		}

		private static Timeline.Datum Convert(IEvent evt, string source) => new Timeline.Datum()
		{
			Start = evt.Timestamp,
			Category = "EVENT",
			Label = "Event " + CrystalJson.Serialize(evt, CrystalJsonSettings.JsonCompact),
			Source = source,
			CorrelationId = evt.OperationId,
			//TODO: details!
		};

	}

}
