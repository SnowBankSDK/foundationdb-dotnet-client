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
	using System.Text;

	/// <summary>One localized difference between two traces of the same scenario.</summary>
	/// <param name="Step">Index of the diverging scenario step, or <c>-1</c> for trace-level differences (final state, format).</param>
	/// <param name="Path">Location of the difference (e.g. <c>events[3].outcome.value</c>, <c>finalState[k2]</c>).</param>
	/// <param name="Expected">Rendered expected-side value (<see langword="null"/> when absent on that side).</param>
	/// <param name="Actual">Rendered actual-side value (<see langword="null"/> when absent on that side).</param>
	public sealed record TraceDivergence(int Step, string Path, string? Expected, string? Actual);

	/// <summary>Structurally compares two traces of the same scenario and reports every divergence (design spec §6.2/§6.4).</summary>
	public static class TraceComparer
	{

		/// <summary>Compares two traces event-by-event and on the final state; the comparison is exact except where a step carries an explicit <see cref="ScenarioTolerance"/> annotation.</summary>
		/// <param name="expected">Reference trace (the committed golden, or the real-cluster side in dual-live mode).</param>
		/// <param name="actual">Trace under test (usually the FakeDb side).</param>
		/// <param name="scenario">The scenario both traces recorded (provides the per-step tolerance annotations).</param>
		public static IReadOnlyList<TraceDivergence> Compare(ScenarioTrace expected, ScenarioTrace actual, Scenario scenario)
		{
			var divergences = new List<TraceDivergence>();

			if (expected.Format != actual.Format)
			{
				divergences.Add(new(-1, "format", expected.Format.ToString(CultureInfo.InvariantCulture), actual.Format.ToString(CultureInfo.InvariantCulture)));
			}

			int common = Math.Min(expected.Events.Count, actual.Events.Count);
			for (int i = 0; i < common; i++)
			{
				CompareEvents(i, expected.Events[i], actual.Events[i], scenario, divergences);
			}
			if (expected.Events.Count != actual.Events.Count)
			{
				divergences.Add(new(common, "events.length", expected.Events.Count.ToString(CultureInfo.InvariantCulture), actual.Events.Count.ToString(CultureInfo.InvariantCulture)));
			}

			CompareFinalState(expected.FinalState, actual.FinalState, divergences);

			return divergences;
		}

		private static void CompareEvents(int index, TraceEvent expected, TraceEvent actual, Scenario scenario, List<TraceDivergence> divergences)
		{
			int step = expected.Step;
			if (expected.Step != actual.Step) divergences.Add(new(step, $"events[{index}].step", expected.Step.ToString(CultureInfo.InvariantCulture), actual.Step.ToString(CultureInfo.InvariantCulture)));
			if (expected.Op != actual.Op) divergences.Add(new(step, $"events[{index}].op", expected.Op, actual.Op));
			if (expected.Actor != actual.Actor) divergences.Add(new(step, $"events[{index}].actor", expected.Actor, actual.Actor));

			DiffJson(step, $"events[{index}].args", expected.Args, actual.Args, divergences);

			if (IsToleratedOutcome(expected, actual, scenario))
			{
				return;
			}
			DiffJson(step, $"events[{index}].outcome", expected.Outcome, actual.Outcome, divergences);
		}

		/// <summary>Checks whether the outcome difference (if any) is covered by the step's tolerance annotation.</summary>
		private static bool IsToleratedOutcome(TraceEvent expected, TraceEvent actual, Scenario scenario)
		{
			if (expected.Step < 0 || expected.Step >= scenario.Steps.Count) return false;
			var step = scenario.Steps[expected.Step];
			switch (step.Tolerance)
			{
				case ScenarioTolerance.AllowSpuriousWatchFire:
				{
					// the fdb contract permits a watch to fire spuriously - including a same-key sibling's fire
					// dragging it along - and in dual-live mode EITHER side of the comparison can be the spurious
					// one: a tolerant pending observation accepts Pending and Fired as equally legal on each side
					if (step.Op != ScenarioOp.ExpectPending) return false;
					var e = expected.Outcome.Get<string?>("watch", null);
					var a = actual.Outcome.Get<string?>("watch", null);
					return e is "Pending" or "Fired" && a is "Pending" or "Fired";
				}
				default:
				{
					return false;
				}
			}
		}

		private static void DiffJson(int step, string path, JsonValue expected, JsonValue actual, List<TraceDivergence> divergences)
		{
			if (expected is JsonObject expObj && actual is JsonObject actObj)
			{
				foreach (var key in expObj.Keys.Union(actObj.Keys, StringComparer.Ordinal))
				{
					DiffJson(step, $"{path}.{key}", expObj[key], actObj[key], divergences);
				}
				return;
			}
			if (expected is JsonArray expArr && actual is JsonArray actArr)
			{
				if (expArr.Count != actArr.Count)
				{
					divergences.Add(new(step, $"{path}.length", expArr.Count.ToString(CultureInfo.InvariantCulture), actArr.Count.ToString(CultureInfo.InvariantCulture)));
					return;
				}
				for (int i = 0; i < expArr.Count; i++)
				{
					DiffJson(step, $"{path}[{i}]", expArr[i], actArr[i], divergences);
				}
				return;
			}
			if (!expected.StrictEquals(actual))
			{
				divergences.Add(new(step, path, RenderValue(expected), RenderValue(actual)));
			}
		}

		// a divergence can pit a container against a scalar (e.g. a GetRange result array on one backend vs an
		// error or missing outcome on the other): render containers as compact JSON rather than binding to string
		private static string? RenderValue(JsonValue value) => value.IsNullOrMissing() ? null : value is JsonArray or JsonObject ? value.ToJsonText() : value.As<string?>();

		private static void CompareFinalState(IReadOnlyList<KeyValuePair<string, string>> expected, IReadOnlyList<KeyValuePair<string, string>> actual, List<TraceDivergence> divergences)
		{
			var expectedByKey = expected.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
			var actualByKey = actual.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
			// note: OrderBy instead of Order, which is not available on the net472 validation target
			foreach (var key in expectedByKey.Keys.Union(actualByKey.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
			{
				string? exp = expectedByKey.TryGetValue(key, out var e) ? e : null;
				string? act = actualByKey.TryGetValue(key, out var a) ? a : null;
				if (exp != act)
				{
					divergences.Add(new(-1, $"finalState[{key}]", exp, act));
				}
			}
		}

		/// <summary>Renders a divergence list as a readable multi-line report.</summary>
		/// <param name="expectedLabel">Name of the expected side (e.g. <c>golden</c>, <c>real</c>).</param>
		/// <param name="actualLabel">Name of the actual side (e.g. <c>fakedb</c>).</param>
		public static string Render(string expectedLabel, string actualLabel, IReadOnlyList<TraceDivergence> divergences)
		{
			if (divergences.Count == 0) return "traces are identical";

			var sb = new StringBuilder();
			sb.Append(CultureInfo.InvariantCulture, $"{divergences.Count} divergence(s) between {expectedLabel} and {actualLabel}:").AppendLine();
			foreach (var d in divergences)
			{
				sb.Append("- ");
				if (d.Step >= 0) sb.Append(CultureInfo.InvariantCulture, $"step {d.Step} @ ");
				sb.Append(CultureInfo.InvariantCulture, $"{d.Path}: {expectedLabel} = {RenderSide(d.Expected)}, {actualLabel} = {RenderSide(d.Actual)}").AppendLine();
			}
			return sb.ToString();

			static string RenderSide(string? value) => value is null ? "<absent>" : $"'{value}'";
		}

	}

}
