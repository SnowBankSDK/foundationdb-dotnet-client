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
	using FoundationDB.Client;

	/// <summary>Operation performed by a single <see cref="ScenarioStep"/> of a differential scenario.</summary>
	public enum ScenarioOp
	{
		/// <summary>Opens the actor's transaction (an actor owns at most one open transaction at a time).</summary>
		Begin,
		/// <summary>Commits the actor's transaction; the outcome (success or fdb error code) is recorded in the trace and the transaction is disposed.</summary>
		Commit,
		/// <summary>Resets the actor's transaction to a blank state.</summary>
		Reset,
		/// <summary>Disposes the actor's transaction without committing.</summary>
		Dispose,
		/// <summary>Sets <see cref="ScenarioStep.Key"/> to <see cref="ScenarioStep.Value"/>.</summary>
		Set,
		/// <summary>Clears <see cref="ScenarioStep.Key"/>.</summary>
		Clear,
		/// <summary>Clears the range from <see cref="ScenarioStep.Key"/> (inclusive) to <see cref="ScenarioStep.EndKey"/> (exclusive).</summary>
		ClearRange,
		/// <summary>Applies the atomic <see cref="ScenarioStep.Mutation"/> to <see cref="ScenarioStep.Key"/> with parameter <see cref="ScenarioStep.Value"/>.</summary>
		Atomic,
		/// <summary>Sets a key whose placeholder at <see cref="ScenarioStep.StampOffset"/> (relative to the scenario key) is overwritten by the commit versionstamp.</summary>
		SetVersionstampedKey,
		/// <summary>Sets a value whose placeholder at <see cref="ScenarioStep.StampOffset"/> is overwritten by the commit versionstamp.</summary>
		SetVersionstampedValue,
		/// <summary>Reads <see cref="ScenarioStep.Key"/> (snapshot isolation when <see cref="ScenarioStep.Snapshot"/>).</summary>
		Get,
		/// <summary>Resolves the key <see cref="ScenarioStep.Selector"/>.</summary>
		GetKey,
		/// <summary>Reads the range between <see cref="ScenarioStep.Selector"/> and <see cref="ScenarioStep.EndSelector"/>.</summary>
		GetRange,
		/// <summary>Reads the transaction's read version (symbolized in the trace).</summary>
		GetReadVersion,
		/// <summary>Reads the transaction's committed version, after its commit (symbolized in the trace).</summary>
		GetCommittedVersion,
		/// <summary>Registers the transaction's versionstamp future under <see cref="ScenarioStep.HandleId"/>; it settles after the commit and is observed by <see cref="ExpectVersionstamp"/>.</summary>
		GetVersionstamp,
		/// <summary>Creates a watch on <see cref="ScenarioStep.Key"/> under <see cref="ScenarioStep.HandleId"/>; it is observed by <see cref="ExpectFired"/> / <see cref="ExpectPending"/>.</summary>
		Watch,
		/// <summary>Observation point: waits (bounded) for the watch <see cref="ScenarioStep.HandleId"/> to fire.</summary>
		ExpectFired,
		/// <summary>Observation point: samples the watch <see cref="ScenarioStep.HandleId"/> after a short grace delay, expecting it not to have fired.</summary>
		ExpectPending,
		/// <summary>Observation point: waits (bounded) for the versionstamp future <see cref="ScenarioStep.HandleId"/> to settle and records the symbolized stamp.</summary>
		ExpectVersionstamp,
		/// <summary>Applies the transaction option <see cref="ScenarioStep.Option"/> to the actor's transaction.</summary>
		SetOption,
		/// <summary>Bumps the global <c>\xff/metadataVersion</c> key (versionstamped write).</summary>
		TouchMetadataVersion,
		/// <summary>Reads the global metadata version; the outcome is the symbolized stamp (or null when never touched).</summary>
		GetMetadataVersion,
		/// <summary>Creates a watch on the global <c>\xff/metadataVersion</c> key under <see cref="ScenarioStep.HandleId"/> (exempt from the relative-key rule).</summary>
		WatchMetadataVersion,
	}

	/// <summary>Transaction options expressible in a scenario (a deliberate allowlist, extended as campaigns need them).</summary>
	public enum ScenarioTransactionOption
	{
		/// <summary>Reads no longer observe the transaction's own uncommitted writes.</summary>
		ReadYourWritesDisable,
		/// <summary>Snapshot reads no longer observe the transaction's own uncommitted writes.</summary>
		SnapshotReadYourWritesDisable,
	}

	/// <summary>Declares the accepted slack on a step where the real cluster is legitimately nondeterministic (design spec §6.4); the default comparison is exact.</summary>
	public enum ScenarioTolerance
	{
		/// <summary>No tolerance: the outcome must match the golden exactly.</summary>
		None,
		/// <summary>An <see cref="ScenarioOp.ExpectPending"/> observation also accepts a fired watch (the fdb contract permits spurious fires).</summary>
		AllowSpuriousWatchFire,
	}

	/// <summary>Key selector operand of a scenario step; <see cref="Key"/> is relative to the scenario root subspace.</summary>
	public sealed record ScenarioSelector(Slice Key, bool OrEqual, int Offset)
	{

		/// <summary>Renders this selector to its JSON form.</summary>
		public JsonObject ToJson() => JsonObject.Create(
		[
			("key", ScenarioText.Encode(this.Key)),
			("orEqual", this.OrEqual),
			("offset", this.Offset),
		]);

		/// <summary>Rebuilds a selector from its JSON form.</summary>
		public static ScenarioSelector FromJson(JsonValue value)
		{
			var obj = value.AsObject();
			return new(ScenarioText.Decode(obj.Get<string?>("key", null)), obj.Get<bool>("orEqual", false), obj.Get<int>("offset", 0));
		}

	}

	/// <summary>Byte-string operand of a scenario step: accepts either an ASCII string literal or a raw <see cref="Slice"/>.</summary>
	public readonly struct ScenarioBytes
	{

		/// <summary>The operand bytes.</summary>
		public readonly Slice Bytes;

		private ScenarioBytes(Slice bytes) => this.Bytes = bytes;

		/// <summary>Wraps raw bytes.</summary>
		public static implicit operator ScenarioBytes(Slice bytes) => new(bytes);

		/// <summary>Encodes an ASCII string literal.</summary>
		public static implicit operator ScenarioBytes(string literal) => new(Slice.FromStringAscii(literal));

	}

	/// <summary>One step of a scenario: an operation, the actor performing it, and its operands.</summary>
	/// <remarks>All keys (<see cref="Key"/>, <see cref="EndKey"/>, selector keys) are relative to the scenario root subspace; the runner applies the resolved prefix of the backend under test.</remarks>
	public sealed record ScenarioStep
	{

		/// <summary>Operation performed by this step.</summary>
		public required ScenarioOp Op { get; init; }

		/// <summary>Actor performing the step (transaction and mutation/read steps); <see langword="null"/> for global observation steps (<see cref="ScenarioOp.ExpectFired"/>, ...).</summary>
		public string? Actor { get; init; }

		/// <summary>Primary key operand (relative), or <see cref="Slice.Nil"/> when the operation has none.</summary>
		public Slice Key { get; init; }

		/// <summary>Value operand (also the atomic mutation parameter), or <see cref="Slice.Nil"/>.</summary>
		public Slice Value { get; init; }

		/// <summary>End-key operand of <see cref="ScenarioOp.ClearRange"/>, or <see cref="Slice.Nil"/>.</summary>
		public Slice EndKey { get; init; }

		/// <summary>Begin selector of <see cref="ScenarioOp.GetKey"/> / <see cref="ScenarioOp.GetRange"/>.</summary>
		public ScenarioSelector? Selector { get; init; }

		/// <summary>End selector of <see cref="ScenarioOp.GetRange"/>.</summary>
		public ScenarioSelector? EndSelector { get; init; }

		/// <summary>Maximum number of results of a <see cref="ScenarioOp.GetRange"/>.</summary>
		public int? Limit { get; init; }

		/// <summary>Reads the range in reverse order.</summary>
		public bool Reverse { get; init; }

		/// <summary>Performs the read under snapshot isolation (no read conflict).</summary>
		public bool Snapshot { get; init; }

		/// <summary>Mutation type of an <see cref="ScenarioOp.Atomic"/> step.</summary>
		public FdbMutationType? Mutation { get; init; }

		/// <summary>Offset of the 10-byte versionstamp placeholder inside <see cref="Key"/> (for <see cref="ScenarioOp.SetVersionstampedKey"/>) or <see cref="Value"/> (for <see cref="ScenarioOp.SetVersionstampedValue"/>).</summary>
		public int? StampOffset { get; init; }

		/// <summary>Identifier binding a <see cref="ScenarioOp.Watch"/> / <see cref="ScenarioOp.GetVersionstamp"/> registration to its observation steps; unique within the scenario.</summary>
		public int? HandleId { get; init; }

		/// <summary>Accepted nondeterminism on this step (observation steps only); default is exact comparison.</summary>
		public ScenarioTolerance Tolerance { get; init; }

		/// <summary>Transaction option applied by a <see cref="ScenarioOp.SetOption"/> step.</summary>
		public ScenarioTransactionOption? Option { get; init; }

		/// <summary>Renders this step to its JSON form (only non-default fields are written).</summary>
		public JsonObject ToJson()
		{
			var obj = JsonObject.Create("op", this.Op.ToString());
			if (this.Actor is not null) obj["actor"] = this.Actor;
			if (!this.Key.IsNull) obj["key"] = ScenarioText.Encode(this.Key);
			if (!this.Value.IsNull) obj["value"] = ScenarioText.Encode(this.Value);
			if (!this.EndKey.IsNull) obj["endKey"] = ScenarioText.Encode(this.EndKey);
			if (this.Selector is not null) obj["sel"] = this.Selector.ToJson();
			if (this.EndSelector is not null) obj["endSel"] = this.EndSelector.ToJson();
			if (this.Limit is not null) obj["limit"] = this.Limit.Value;
			if (this.Reverse) obj["reverse"] = true;
			if (this.Snapshot) obj["snapshot"] = true;
			if (this.Mutation is not null) obj["mutation"] = this.Mutation.Value.ToString();
			if (this.StampOffset is not null) obj["stampOffset"] = this.StampOffset.Value;
			if (this.HandleId is not null) obj["handle"] = this.HandleId.Value;
			if (this.Tolerance != ScenarioTolerance.None) obj["tolerance"] = this.Tolerance.ToString();
			if (this.Option is not null) obj["option"] = this.Option.Value.ToString();
			return obj;
		}

		/// <summary>Rebuilds a step from its JSON form.</summary>
		/// <exception cref="FormatException">If the operation or an enum field is unknown.</exception>
		public static ScenarioStep FromJson(JsonValue value)
		{
			var obj = value.AsObject();
			return new()
			{
				Op = ParseEnum<ScenarioOp>(obj.Get<string>("op"), "op"),
				Actor = obj.Get<string?>("actor", null),
				Key = ScenarioText.Decode(obj.Get<string?>("key", null)),
				Value = ScenarioText.Decode(obj.Get<string?>("value", null)),
				EndKey = ScenarioText.Decode(obj.Get<string?>("endKey", null)),
				Selector = obj.ContainsKey("sel") ? ScenarioSelector.FromJson(obj["sel"]) : null,
				EndSelector = obj.ContainsKey("endSel") ? ScenarioSelector.FromJson(obj["endSel"]) : null,
				Limit = obj.Get<int?>("limit", null),
				Reverse = obj.Get<bool>("reverse", false),
				Snapshot = obj.Get<bool>("snapshot", false),
				Mutation = obj.ContainsKey("mutation") ? ParseEnum<FdbMutationType>(obj.Get<string>("mutation"), "mutation") : null,
				StampOffset = obj.Get<int?>("stampOffset", null),
				HandleId = obj.Get<int?>("handle", null),
				Tolerance = obj.ContainsKey("tolerance") ? ParseEnum<ScenarioTolerance>(obj.Get<string>("tolerance"), "tolerance") : ScenarioTolerance.None,
				Option = obj.ContainsKey("option") ? ParseEnum<ScenarioTransactionOption>(obj.Get<string>("option"), "option") : null,
			};
		}

		private static TEnum ParseEnum<TEnum>(string text, string field) where TEnum : struct, Enum
		{
			// reject numeric forms (Enum.TryParse would accept "42"): golden files must use the symbolic names
			// note: non-generic Enum.IsDefined, because the generic overload is not available on the net472 validation target
			if (!Enum.TryParse<TEnum>(text, ignoreCase: false, out var parsed) || !Enum.IsDefined(typeof(TEnum), parsed))
			{
				throw new FormatException($"Unknown {field} '{text}' in scenario step.");
			}
			return parsed;
		}

	}

	/// <summary>A deterministic script over N logical actors, expressed as one globally-ordered list of steps; the interleaving is explicit in the step order.</summary>
	public sealed record Scenario
	{

		/// <summary>Unique name of the scenario (also the golden trace's file name).</summary>
		public required string Name { get; init; }

		/// <summary>Optional description of the behavior the scenario pins.</summary>
		public string? Description { get; init; }

		/// <summary>The globally-ordered steps.</summary>
		public required IReadOnlyList<ScenarioStep> Steps { get; init; }

		/// <summary>Renders this scenario to its JSON form (so generator finds can be pinned as data).</summary>
		public JsonObject ToJson()
		{
			var obj = JsonObject.Create("name", this.Name);
			if (this.Description is not null) obj["description"] = this.Description;
			obj["steps"] = JsonArray.FromValues(this.Steps, s => s.ToJson());
			return obj;
		}

		/// <summary>Rebuilds a scenario from its JSON form.</summary>
		public static Scenario FromJson(JsonValue value)
		{
			var obj = value.AsObject();
			return new()
			{
				Name = obj.Get<string>("name"),
				Description = obj.Get<string?>("description", null),
				Steps = obj.GetArray("steps").Select(ScenarioStep.FromJson).ToList(),
			};
		}

	}

	/// <summary>Fluent builder for hand-authored scenarios; steps are appended in call order.</summary>
	public sealed class ScenarioBuilder
	{

		private List<ScenarioStep> Steps { get; } = [ ];

		private int NextHandleId { get; set; }

		private ScenarioBuilder Add(ScenarioStep step)
		{
			this.Steps.Add(step);
			return this;
		}

		/// <summary>Opens the actor's transaction.</summary>
		public ScenarioBuilder Begin(string actor) => Add(new() { Op = ScenarioOp.Begin, Actor = actor });

		/// <summary>Commits the actor's transaction (the trace records success or the fdb error code).</summary>
		public ScenarioBuilder Commit(string actor) => Add(new() { Op = ScenarioOp.Commit, Actor = actor });

		/// <summary>Resets the actor's transaction.</summary>
		public ScenarioBuilder Reset(string actor) => Add(new() { Op = ScenarioOp.Reset, Actor = actor });

		/// <summary>Disposes the actor's transaction without committing.</summary>
		public ScenarioBuilder Dispose(string actor) => Add(new() { Op = ScenarioOp.Dispose, Actor = actor });

		/// <summary>Sets a key to a value.</summary>
		public ScenarioBuilder Set(string actor, ScenarioBytes key, ScenarioBytes value) => Add(new() { Op = ScenarioOp.Set, Actor = actor, Key = key.Bytes, Value = value.Bytes });

		/// <summary>Clears a key.</summary>
		public ScenarioBuilder Clear(string actor, ScenarioBytes key) => Add(new() { Op = ScenarioOp.Clear, Actor = actor, Key = key.Bytes });

		/// <summary>Clears a key range [begin, end).</summary>
		public ScenarioBuilder ClearRange(string actor, ScenarioBytes beginInclusive, ScenarioBytes endExclusive) => Add(new() { Op = ScenarioOp.ClearRange, Actor = actor, Key = beginInclusive.Bytes, EndKey = endExclusive.Bytes });

		/// <summary>Applies an atomic mutation to a key.</summary>
		public ScenarioBuilder Atomic(string actor, ScenarioBytes key, ScenarioBytes param, FdbMutationType mutation) => Add(new() { Op = ScenarioOp.Atomic, Actor = actor, Key = key.Bytes, Value = param.Bytes, Mutation = mutation });

		/// <summary>Sets a key carrying a versionstamp placeholder at <paramref name="stampOffset"/> (relative to the scenario key bytes).</summary>
		public ScenarioBuilder SetVersionstampedKey(string actor, ScenarioBytes keyWithPlaceholder, int stampOffset, ScenarioBytes value) => Add(new() { Op = ScenarioOp.SetVersionstampedKey, Actor = actor, Key = keyWithPlaceholder.Bytes, Value = value.Bytes, StampOffset = stampOffset });

		/// <summary>Sets a value carrying a versionstamp placeholder at <paramref name="stampOffset"/>.</summary>
		public ScenarioBuilder SetVersionstampedValue(string actor, ScenarioBytes key, ScenarioBytes valueWithPlaceholder, int stampOffset) => Add(new() { Op = ScenarioOp.SetVersionstampedValue, Actor = actor, Key = key.Bytes, Value = valueWithPlaceholder.Bytes, StampOffset = stampOffset });

		/// <summary>Reads a key.</summary>
		public ScenarioBuilder Get(string actor, ScenarioBytes key, bool snapshot = false) => Add(new() { Op = ScenarioOp.Get, Actor = actor, Key = key.Bytes, Snapshot = snapshot });

		/// <summary>Resolves a key selector.</summary>
		public ScenarioBuilder GetKey(string actor, ScenarioSelector selector, bool snapshot = false) => Add(new() { Op = ScenarioOp.GetKey, Actor = actor, Selector = selector, Snapshot = snapshot });

		/// <summary>Reads a range of keys between two selectors.</summary>
		public ScenarioBuilder GetRange(string actor, ScenarioSelector begin, ScenarioSelector end, int? limit = null, bool reverse = false, bool snapshot = false, ScenarioTolerance tolerance = ScenarioTolerance.None) => Add(new() { Op = ScenarioOp.GetRange, Actor = actor, Selector = begin, EndSelector = end, Limit = limit, Reverse = reverse, Snapshot = snapshot, Tolerance = tolerance });

		/// <summary>Reads the transaction's read version.</summary>
		public ScenarioBuilder GetReadVersion(string actor) => Add(new() { Op = ScenarioOp.GetReadVersion, Actor = actor });

		/// <summary>Reads the transaction's committed version (place after its <see cref="Commit"/>).</summary>
		public ScenarioBuilder GetCommittedVersion(string actor) => Add(new() { Op = ScenarioOp.GetCommittedVersion, Actor = actor });

		/// <summary>Registers the transaction's versionstamp future (place before its <see cref="Commit"/>); observe it later with <see cref="ExpectVersionstamp"/>.</summary>
		/// <returns>The handle id to pass to <see cref="ExpectVersionstamp"/>.</returns>
		public int GetVersionstamp(string actor)
		{
			int id = this.NextHandleId++;
			Add(new() { Op = ScenarioOp.GetVersionstamp, Actor = actor, HandleId = id });
			return id;
		}

		/// <summary>Creates a watch on a key; observe it later with <see cref="ExpectFired"/> / <see cref="ExpectPending"/>.</summary>
		/// <returns>The handle id to pass to the observation steps.</returns>
		public int Watch(string actor, ScenarioBytes key)
		{
			int id = this.NextHandleId++;
			Add(new() { Op = ScenarioOp.Watch, Actor = actor, Key = key.Bytes, HandleId = id });
			return id;
		}

		/// <summary>Observation point: the watch must have fired (bounded wait).</summary>
		public ScenarioBuilder ExpectFired(int watchId, ScenarioTolerance tolerance = ScenarioTolerance.None) => Add(new() { Op = ScenarioOp.ExpectFired, HandleId = watchId, Tolerance = tolerance });

		/// <summary>Observation point: the watch must still be pending (sampled after a short grace delay).</summary>
		public ScenarioBuilder ExpectPending(int watchId, ScenarioTolerance tolerance = ScenarioTolerance.None) => Add(new() { Op = ScenarioOp.ExpectPending, HandleId = watchId, Tolerance = tolerance });

		/// <summary>Observation point: settles the versionstamp future and records the symbolized stamp (place after the transaction's <see cref="Commit"/>).</summary>
		public ScenarioBuilder ExpectVersionstamp(int vsId) => Add(new() { Op = ScenarioOp.ExpectVersionstamp, HandleId = vsId });

		/// <summary>Applies a transaction option to the actor's open transaction.</summary>
		public ScenarioBuilder SetOption(string actor, ScenarioTransactionOption option) => Add(new() { Op = ScenarioOp.SetOption, Actor = actor, Option = option });

		/// <summary>Bumps the global metadata version key.</summary>
		public ScenarioBuilder TouchMetadataVersion(string actor) => Add(new() { Op = ScenarioOp.TouchMetadataVersion, Actor = actor });

		/// <summary>Reads the global metadata version (symbolized stamp outcome).</summary>
		public ScenarioBuilder GetMetadataVersion(string actor) => Add(new() { Op = ScenarioOp.GetMetadataVersion, Actor = actor });

		/// <summary>Creates a watch on the global metadata version key; observe it with <see cref="ExpectFired"/> / <see cref="ExpectPending"/>.</summary>
		/// <returns>The handle id to pass to the observation steps.</returns>
		public int WatchMetadataVersion(string actor)
		{
			int id = this.NextHandleId++;
			Add(new() { Op = ScenarioOp.WatchMetadataVersion, Actor = actor, HandleId = id });
			return id;
		}

		/// <summary>Freezes the current steps into a <see cref="Scenario"/>.</summary>
		public Scenario Build(string name, string? description = null) => new() { Name = name, Description = description, Steps = this.Steps.ToList() };

	}

}
