#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{

	/// <summary>Options used to configure the behavior of a <see cref="Timeline"/></summary>
	public sealed record TimelineOptions
	{

		/// <summary>Number of events to store per chunk</summary>
		public int MaxChunkSize { get; set; } = 128;

		/// <summary>If non-null, maximum number of chunks to keep (older chunks are dropped)</summary>
		public int? MaxChunks { get; set; }

	}

	/// <summary>Container for events that occurred during the execution of a test</summary>
	public class Timeline
	{

		/// <summary>Constructs a new empty <see cref="Timeline"/></summary>
		public Timeline(TimelineOptions options)
		{
			Contract.NotNull(options);

			this.Options = options;
			StartNewChunk();
		}

		/// <summary>Metadata about an event that was recorded in a <see cref="Timeline"/></summary>
		public sealed record Datum
		{

			/// <summary>Instant when this event started</summary>
			public required Instant Start { get; init; }

			/// <summary>Instant when this event ended</summary>
			public Instant? End { get; init; }

			/// <summary>Duration of this event</summary>
			/// <remarks>Returns zero for "point-like" events</remarks>
			public Duration Duration => this.End != null ? this.End.Value - this.Start : Duration.Zero;

			/// <summary>Category tag for this event</summary>
			public required string Category { get; init; }

			/// <summary>Human-readable label for this event</summary>
			public required string Label { get; init; }

			/// <summary>Specifies if this event represents an error or failed operation</summary>
			public bool Failed { get; init; }

			/// <summary>Options source of this event (usually the id of the test component that emitted the event)</summary>
			public string? Source { get; init; }

			/// <summary>ID that can be used to correlate a chain events that happened on multiple nodes, in reaction to the same initial "trigger"</summary>
			public string? CorrelationId { get; init; }

			/// <summary>Additional details for this event</summary>
			public JsonObject? Details { get; init; }

		}

		private List<List<Datum>> Chunks { get; } = [ ];

		private List<Datum> Current { get; set; }

		public TimelineOptions Options { get; set; }

		/// <summary>Records a new event on the timeline</summary>
		public void Record(Datum datum)
		{
			Contract.Debug.Requires(datum != null && datum.Category != null && datum.Label != null);
			lock (this.Chunks)
			{
				var chunk = this.Current;
				chunk.Add(datum);
				if (chunk.Count >= this.Options.MaxChunkSize)
				{
					StartNewChunk();
				}
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[MemberNotNull(nameof(Current))]
		private void StartNewChunk()
		{
			var options = this.Options;

			// start a new empty chunk
			var chunk = new List<Datum>(options.MaxChunkSize);
			this.Current = chunk;

			var chunks = this.Chunks;
			if (chunks.Count == options.MaxChunks)
			{ // drop the extra chunk
				chunks.RemoveAt(0);
			}
			chunks.Add(chunk);
		}

		/// <summary>Returns of list of matching events</summary>
		public List<Datum> Query(/* TODO: filters? */)
		{
			lock (this.Chunks)
			{
				// on veut pre-allouer la liste pour plus éviter trop de resize
				long count = 0;
				foreach (var chunk in this.Chunks)
				{
					count += chunk.Count;
				}

				//REVIEW: quid si on a plus de 2G events?? :D
				var res = new List<Datum>(checked((int) count));
				foreach (var chunk in this.Chunks)
				{
					res.AddRange(chunk);
				}
				return res;
			}
		}

		private const string BarTicksChars = "<[[((|))]]>";
		private const string BarChartChars = ".:;+=xX$&##"; //note: an extra block "just in case" rounding overflows
		private const char FillerChar = '#';

		/// <summary>Generates a textual report of the timeline, that can be output in a text log or console</summary>
		public void DumpReport(StringBuilder sb, string name, Instant testStart, Instant testEnd, TimelineRenderOptions options = TimelineRenderOptions.Default)
		{
			const int DRAW_WIDTH = 60;

			const int TOTAL_WIDTH = 70 + DRAW_WIDTH;

			bool showStartup = (options & TimelineRenderOptions.ShowStartup) != 0;

			sb.Append('═', TOTAL_WIDTH);
			sb.AppendLine();
			sb.AppendLine($"Timeline for {name}");
			List<Datum> data  = Query();

			if (data.Count == 0)
			{
				sb.AppendLine("Timeline is empty!");
			}
			else
			{
				data = data.OrderBy(d => d.Start).ToList();

				var min = testStart;
				if (showStartup)
				{
					min = data.Min(d => d.Start);
				}

				var max = testEnd;
				if (showStartup)
				{
					max = data.Max(d => d.End ?? d.Start);
				}

				var range = (max - min).TotalMilliseconds;
				var scale = DRAW_WIDTH / range;

				var originOffset = (testStart - min).TotalMilliseconds * scale;
				var originStart = (int) originOffset;

				sb.AppendLine($"Recorded {data.Count:N0} events from {min} to {max} ({range:N1} ms)");

				sb.Append('╌', TOTAL_WIDTH);
				sb.AppendLine();

				foreach (var datum in data)
				{
					var start = datum.Start;
					var end = datum.End ?? datum.Start;
					double ratioStart = (start - min).TotalMilliseconds * scale;
					double ratioEnd = (end - min).TotalMilliseconds * scale;
					double l = ratioEnd - ratioStart;

					sb.Append(datum.Failed ? "!!! " : "    ");

					if (start < testStart)
					{
						sb.Append($"T-{(testStart - start).TotalSeconds,7:N03} ︙ ");
					}
					else
					{
						sb.Append($"T+{(start - testStart).TotalSeconds,7:N03} ︙ ");
					}

					if (end == start)
					{
						sb.Append("          ￤ ");
					}
					else if (end < testStart)
					{
						sb.Append($"T-{(testStart - end).TotalSeconds,7:N03} ￤ ");
					}
					else
					{
						sb.Append($"T+{(end - testStart).TotalSeconds,7:N03} ￤ ");
					}

					if (l == 0) sb.Append("           ");
					else sb.Append($"{(end - start).TotalMilliseconds,8:N1} ms");

					sb.Append(" │ ");

					if (start > max)
					{ // overshoots on the right
						sb.Append('_', DRAW_WIDTH - 1);
						sb.Append('>');
					}
					else
					{
						int padLeft;
						int w;

						if (start < min)
						{ // overshoots on the left
							padLeft = 0;
							w = 1;
							sb.Append('<');
						}
						else
						{
							padLeft = (int) ratioStart;
							if (padLeft >= originStart)
							{
								sb.Append('=', originStart);
								sb.Append('-', padLeft - originStart);
							}
							else
							{
								sb.Append('-', padLeft);
							}

							if (l == 0)
							{
								sb.Append(BarTicksChars[(int) ((ratioStart - padLeft) * 11)]);
								w = 1;
							}
							else
							{
								w = (int) (Math.Ceiling(ratioEnd) - Math.Floor(ratioStart));
								if (w <= 1)
								{
									sb.Append(BarChartChars[(int) (l * 10)]);
								}
								else
								{
									sb.Append(BarChartChars[(int) ((ratioStart - padLeft) * 10)]);
									if (w > 2) sb.Append(FillerChar, w - 2);
									sb.Append(BarChartChars[(int) ((1.0 - (Math.Ceiling(ratioEnd) - ratioEnd)) * 10)]);
								}
							}
						}

						int padRight = DRAW_WIDTH - padLeft - w;
						if (padRight > 0)
						{
							sb.Append(' ', padRight);
						}
					}

					sb.Append($" │ {datum.Source,-10} ︙ {datum.Category,-8}");
					sb.Append(" │ ").Append(datum.Label);
					sb.AppendLine();
				}

			}
			sb.Append('═', TOTAL_WIDTH);
			sb.AppendLine();
		}

	}

	[Flags]
	public enum TimelineRenderOptions
	{
		None = 0,

		/// <summary>Includes more details for each event</summary>
		ShowDetails = 1,

		/// <summary>Includes any event that occured during the setup phase of the test</summary>
		ShowStartup = 128,

		Default = None, //TODO!
	}

}
