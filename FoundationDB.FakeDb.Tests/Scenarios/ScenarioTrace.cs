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
	using System.Globalization;

	/// <summary>Maps the absolute versions observed during a scenario run to stable symbolic ids (<c>v1</c>, <c>v2</c>, ... by first appearance).</summary>
	/// <remarks>
	/// <para>Absolute read/commit versions and versionstamps never match across backends or runs; the symbols preserve what matters, ordering of first appearance, equality, and uniqueness, while ignoring the absolute values.</para>
	/// <para>Versionstamps symbolize their 8-byte transaction version through the <b>same</b> table as plain versions, so the "stamp == committed version" relation survives symbolization.</para>
	/// </remarks>
	public sealed class VersionSymbolizer
	{

		private Dictionary<long, string> Symbols { get; } = new();

		/// <summary>Byte patterns of the observed (complete) versionstamps, substituted by <see cref="Render"/> in rendered keys and values.</summary>
		private List<(Slice Pattern, string Symbol)> ObservedStamps { get; } = [ ];

		/// <summary>Returns the symbol for a read/commit version (<c>none</c> for the "no version" sentinel).</summary>
		public string Version(long version)
		{
			if (version < 0) return "none";
			if (!this.Symbols.TryGetValue(version, out var symbol))
			{
				symbol = "v" + (this.Symbols.Count + 1).ToString(CultureInfo.InvariantCulture);
				this.Symbols[version] = symbol;
			}
			return symbol;
		}

		/// <summary>Returns the symbol for a versionstamp: <c>vN#&lt;order&gt;</c>, plus <c>u&lt;user&gt;</c> when it carries a user version.</summary>
		/// <remarks>Observing a complete stamp also registers its byte pattern, so later <see cref="Render"/> calls substitute it in rendered keys and values (a stamped key in the final dump, a stamped value read back, ...).</remarks>
		public string Stamp(VersionStamp stamp)
		{
			if (stamp.IsIncomplete) return "incomplete";
			var text = $"{Version(unchecked((long) stamp.TransactionVersion))}#{stamp.TransactionOrder}";
			if (stamp.HasUserVersion) text += "u" + stamp.UserVersion.ToString(CultureInfo.InvariantCulture);

			var pattern = stamp.ToSlice();
			if (!this.ObservedStamps.Any(x => x.Pattern.Equals(pattern)))
			{
				this.ObservedStamps.Add((pattern, text));
			}
			return text;
		}

		/// <summary>Renders bytes with the scenario codec, substituting every occurrence of an observed stamp pattern with <c>&lt;vN#o&gt;</c> (absolute stamp bytes never match across backends or runs).</summary>
		public string? Render(Slice bytes)
		{
			if (bytes.IsNull) return null;
			if (bytes.Count == 0) return "";
			if (this.ObservedStamps.Count == 0) return ScenarioText.Encode(bytes);

			var sb = new System.Text.StringBuilder(bytes.Count + 8);
			int offset = 0;
			while (offset < bytes.Count)
			{
				bool matched = false;
				foreach (var (pattern, symbol) in this.ObservedStamps)
				{
					if (offset + pattern.Count <= bytes.Count && bytes.Substring(offset, pattern.Count).Equals(pattern))
					{
						sb.Append('<').Append(symbol).Append('>');
						offset += pattern.Count;
						matched = true;
						break;
					}
				}
				if (!matched)
				{
					// the codec is per-byte, so encoding one byte at a time yields the same text as encoding the whole run
					sb.Append(ScenarioText.Encode(bytes.Substring(offset, 1)));
					offset++;
				}
			}
			return sb.ToString();
		}

	}

	/// <summary>One recorded trace event: the echo of the executed step and its observed outcome.</summary>
	public sealed record TraceEvent
	{

		/// <summary>Index of the scenario step that produced this event.</summary>
		public required int Step { get; init; }

		/// <summary>Echo of the step operation name.</summary>
		public required string Op { get; init; }

		/// <summary>Echo of the acting actor (<see langword="null"/> for global observation steps).</summary>
		public string? Actor { get; init; }

		/// <summary>Echo of the step operands, rendered (relative keys, selectors, ...).</summary>
		public JsonObject Args { get; init; } = new();

		/// <summary>The observed outcome: values read, resolved keys, commit success or error code, symbolized versions, settled watch state, ...</summary>
		public JsonObject Outcome { get; init; } = new();

		/// <summary>Renders this event to its JSON form.</summary>
		public JsonObject ToJson()
		{
			var obj = JsonObject.Create("step", this.Step);
			obj["op"] = this.Op;
			if (this.Actor is not null) obj["actor"] = this.Actor;
			obj["args"] = this.Args;
			obj["outcome"] = this.Outcome;
			return obj;
		}

		/// <summary>Rebuilds an event from its JSON form.</summary>
		public static TraceEvent FromJson(JsonValue value)
		{
			var obj = value.AsObject();
			return new()
			{
				Step = obj.Get<int>("step"),
				Op = obj.Get<string>("op"),
				Actor = obj.Get<string?>("actor", null),
				Args = obj.GetObjectOrEmpty("args").ToMutable(),
				Outcome = obj.GetObjectOrEmpty("outcome").ToMutable(),
			};
		}

	}

	/// <summary>The full recorded execution of a scenario against one backend: one event per step, plus a final dump of the scenario subspace.</summary>
	public sealed record ScenarioTrace
	{

		/// <summary>Version of the trace format (bumped when the capture vocabulary changes, so stale goldens fail loudly).</summary>
		public const int CurrentFormat = 1;

		/// <summary>Name of the scenario that produced this trace.</summary>
		public required string ScenarioName { get; init; }

		/// <summary>Format of this trace (see <see cref="CurrentFormat"/>).</summary>
		public int Format { get; init; } = CurrentFormat;

		/// <summary>The recorded events, one per scenario step, in step order.</summary>
		public required IReadOnlyList<TraceEvent> Events { get; init; }

		/// <summary>Final content of the scenario subspace (relative keys, byte order), captured after the last step.</summary>
		public required IReadOnlyList<KeyValuePair<string, string>> FinalState { get; init; }

		/// <summary>Renders this trace to its JSON form.</summary>
		public JsonObject ToJson()
		{
			var obj = JsonObject.Create("scenario", this.ScenarioName);
			obj["format"] = this.Format;
			obj["events"] = JsonArray.FromValues(this.Events, e => e.ToJson());
			obj["finalState"] = JsonArray.FromValues(this.FinalState, kv => JsonObject.Create([ ("key", kv.Key), ("value", kv.Value) ]));
			return obj;
		}

		/// <summary>Renders this trace as indented JSON text (the golden file format).</summary>
		public string ToJsonText() => ToJson().ToJsonText(CrystalJsonSettings.JsonIndented);

		/// <summary>Rebuilds a trace from its JSON form.</summary>
		public static ScenarioTrace FromJson(JsonValue value)
		{
			var obj = value.AsObject();
			return new()
			{
				ScenarioName = obj.Get<string>("scenario"),
				Format = obj.Get<int>("format", 0),
				Events = obj.GetArray("events").Select(TraceEvent.FromJson).ToList(),
				FinalState = obj.GetArray("finalState").AsObjects().Select(kv => new KeyValuePair<string, string>(kv.Get<string>("key"), kv.Get<string>("value"))).ToList(),
			};
		}

		/// <summary>Parses a golden trace from its JSON text.</summary>
		public static ScenarioTrace FromJsonText(string text) => FromJson(CrystalJson.Parse(text));

	}

}
