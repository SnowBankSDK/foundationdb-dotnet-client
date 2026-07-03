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

namespace SnowBank.Testing.Framework
{
	using Microsoft.Extensions.Logging;

	/// <summary>Options used to configure the behavior of a <see cref="Timeline"/></summary>
	public sealed record TimelineOptions
	{

		/// <summary>Number of events to store per chunk</summary>
		public int MaxChunkSize { get; set; } = 128;

		/// <summary>If non-null, maximum number of chunks to keep (older chunks are dropped)</summary>
		public int? MaxChunks { get; set; }

	}

	/// <summary>Container for events that occurred during the execution of a test</summary>
	/// <remarks>This is the unified test journal: ILogger messages, harness lifecycle, captured packets and fdb traces all funnel into one chronologically ordered stream (see <see cref="DumpReport"/>).</remarks>
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

			/// <summary>Severity of this event when it originates from an <see cref="ILogger{T}"/>, or <c>null</c> for structural events (lifecycle, packets, fdb traces)</summary>
			public LogLevel? Level { get; init; }

			/// <summary>Options source of this event (usually the id of the test component that emitted the event)</summary>
			public string? Source { get; init; }

			/// <summary>ID that can be used to correlate a chain events that happened on multiple nodes, in reaction to the same initial "trigger"</summary>
			public string? CorrelationId { get; init; }

			/// <summary>Additional details for this event</summary>
			public JsonObject? Details { get; init; }

			/// <summary>High-resolution monotonic timestamp (from <see cref="Stopwatch.GetTimestamp"/>) used to order events precisely</summary>
			/// <remarks>Assigned automatically by <see cref="Record"/> when left at <c>0</c>; a source that captured its own tick at the real event time (e.g. a packet) may set it explicitly so it is not overwritten. This is the real (wall) clock ordering axis, immune to a virtual/fake <see cref="IClock"/>.</remarks>
			public long Ticks { get; init; }

			/// <summary>Monotonic per-timeline sequence number assigned when the event was recorded, used as the tiebreaker for events that share the same <see cref="Ticks"/></summary>
			/// <remarks>Assigned automatically by <see cref="Record"/>; do not set manually.</remarks>
			public long Sequence { get; init; }

		}

		private List<List<Datum>> Chunks { get; } = [ ];

		private List<Datum> Current { get; set; }

		/// <summary>Monotonic counter used to assign <see cref="Datum.Sequence"/>. A field (not a property) because it is mutated via <see cref="Interlocked.Increment(ref long)"/>.</summary>
		private long SeqCounter;

		public TimelineOptions Options { get; set; }

		/// <summary>Records a new event on the timeline</summary>
		public void Record(Datum datum)
		{
			Contract.Debug.Requires(datum != null && datum.Category != null && datum.Label != null);

			// Capture the ordering key BEFORE taking the lock,
			// so that any contention on the storage cannot reorder or skew the recorded timeline.
			// GetTimestamp() is high-resolution, monotonic and process-global; it needs no shared instance.
			// A source that already captured its own tick at the real event time (e.g. a packet observed earlier)
			// sets Ticks explicitly, so we keep it.
			var ticks = datum.Ticks != 0 ? datum.Ticks : Stopwatch.GetTimestamp();
			var seq = Interlocked.Increment(ref this.SeqCounter);
			datum = datum with { Ticks = ticks, Sequence = seq };

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

		/// <summary>Maps a log level to a fixed-width 5-char token, using Watchdoc-style severity weighting: loud levels uppercase, info lowercase, debug/trace reduced to dashes/dots so they recede when scrolling a monochrome log.</summary>
		private static string FormatLevel(LogLevel? level, bool failed) => level switch
		{
			LogLevel.Critical    => "FATAL",
			LogLevel.Error       => "ERROR",
			LogLevel.Warning     => "WARN ",
			LogLevel.Information => "info ",
			LogLevel.Debug       => "-----",
			LogLevel.Trace       => ".....",
			_                    => failed ? "ERROR" : "     ",
		};

		/// <summary>Returns a 2-char left gutter so that loud events stand out when scrolling: "!!" for error/fatal, "! " for warning, blank otherwise.</summary>
		private static string FormatGutter(LogLevel? level, bool failed)
			=> (failed || level is LogLevel.Critical or LogLevel.Error) ? "!!"
			 : level is LogLevel.Warning ? "! "
			 : "  ";

		/// <summary>Maps an event category to a single-letter kind code (L=log, P=packet, F=fdb, T=timeline/harness, X=probe/hook).</summary>
		private static char FormatKind(string category) => category switch
		{
			"LOG"             => 'L',
			"MSG"             => 'M',
			"PKT"             => 'P',
			"FDB"             => 'F',
			"TEST" or "TML"   => 'T',
			"PROBE" or "HOOK" => 'X',
			_                 => category.Length > 0 ? char.ToUpperInvariant(category[0]) : '?',
		};

		/// <summary>Generates a textual report of the timeline, suitable for a test log or console.</summary>
		/// <remarks>Events are ordered chronologically by the high-resolution <see cref="Datum.Ticks"/> (real/wall clock), and the whole block is bracketed by grep-able START/END markers carrying the test name so it can be located in an aggregated parallel test output.</remarks>
		public void DumpReport(StringBuilder sb, string name, Instant testStart, Instant testEnd, TimelineRenderOptions options = TimelineRenderOptions.Default)
		{
			sb.AppendLine($"===== TEST JOURNAL START test={name} =====");
			sb.AppendLine("# columns: <gutter> #seq | T+elapsed | level | kind | source | detail   ::   kind L=log M=message P=packet F=fdb T=timeline X=probe   ::   level ERROR/WARN/FATAL loud, info normal, -----=debug, .....=trace   ::   gutter !!=error/fatal !=warn");

			var data = Query();
			if (data.Count == 0)
			{
				sb.AppendLine("# (timeline is empty)");
			}
			else
			{
				data = data.OrderBy(d => d.Ticks).ThenBy(d => d.Sequence).ToList();

				var min = data.Min(d => d.Start);
				sb.AppendLineInvariant($"# {data.Count:N0} events, test body {(testEnd - testStart).TotalMilliseconds:N1} ms (setup started at T-{(testStart - min).TotalSeconds:N3})");

				foreach (var datum in data)
				{
					// Point events are placed at their instant; span events are recorded (and ordered) at completion, so we display the completion time so the T+ column stays monotonic, and append the elapsed duration.
					var when = datum.End ?? datum.Start;
					var delta = (when - testStart).TotalSeconds;

					sb.Append(FormatGutter(datum.Level, datum.Failed));
					sb.AppendInvariant($" #{datum.Sequence:D4} | ");
					if (delta >= 0)
					{
						sb.AppendInvariant($"T+{delta,7:N3}");
					}
					else
					{
						sb.AppendInvariant($"T-{-delta,7:N3}");
					}
					sb.Append(" | ");
					sb.Append(FormatLevel(datum.Level, datum.Failed));
					sb.Append(" | ");
					sb.Append(FormatKind(datum.Category));
					sb.Append(" | ");
					sb.Append((datum.Source ?? "-").PadRight(8));
					sb.Append(" | ");
					sb.Append(datum.Label);

					if (datum.End is { } end && end != datum.Start)
					{
						sb.AppendInvariant($"  ({(end - datum.Start).TotalMilliseconds:N1} ms)");
					}

					if (datum.CorrelationId is { Length: > 0 } cid)
					{
						sb.Append("  <").Append(cid).Append('>');
					}

					sb.AppendLine();
				}
			}

			sb.AppendLine($"===== TEST JOURNAL END test={name} =====");
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

	/// <summary>Maps a specially-tagged log event (identified by its EventName) to a distinct Timeline "kind".</summary>
	/// <remarks>
	/// <para>Lets a library that emits trace events via <c>ILogger</c> with a well-known <c>EventId</c> name surface them in the
	/// unified test journal as their own kind, without the generic test framework needing any knowledge of that library.</para>
	/// <para>Such events are captured whenever they are emitted (gated only by the logger level), independent of the regular
	/// timeline log-level threshold. Register via the test environment builder's <c>RegisterTimelineEvent</c>.</para>
	/// </remarks>
	[PublicAPI]
	public sealed record TimelineEventRule
	{

		/// <summary>Journal kind to record these events under (e.g. <c>"MSG"</c>, <c>"FDB"</c>); rendered as a single letter in the report.</summary>
		public required string Category { get; init; }

		/// <summary>Optional formatter that turns the log message into the journal label; when <c>null</c>, the message is used as-is.</summary>
		public Func<string?, string>? FormatLabel { get; init; }

	}

}
