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

	/// <summary>One enumerated behavior of the transaction API surface, tracked by the coverage ledger.</summary>
	/// <remarks>A cell counts as covered only when an ORACLE-BACKED asset hits it: a golden-backed scenario (classified from its script and trace), or a conformance fact carrying a <see cref="CoversCellsAttribute"/> tag (those facts run against the real cluster through their RealCluster head).</remarks>
	public sealed record CoverageCell(string Id, string Description);

	/// <summary>Declares the coverage cells a conformance fact exercises (the explicit-tag leg of the ledger; scenario hits are derived, never declared).</summary>
	/// <remarks>Tag only behaviors the fact genuinely asserts; every id must exist in <see cref="CoverageInventory"/> (the ledger fact fails on unknown ids).</remarks>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class CoversCellsAttribute : Attribute
	{

		/// <summary>The tagged cell ids.</summary>
		public IReadOnlyList<string> Cells { get; }

		public CoversCellsAttribute(params string[] cells) => this.Cells = cells;

	}

	/// <summary>The enumerated transaction-API coverage cells: the DENOMINATOR of the conformance confidence target.</summary>
	/// <remarks>
	/// <para>Cell ids are <c>group/name</c>, stable once published (a rename is a ledger history break). The inventory only grows; a behavior removed from the API retires its cell explicitly.</para>
	/// <para>Group sizing rationale: each cell is one behavior a single test can hit. The errors group is a curated starter set that grows when error-translation assertions become dual-backend testable.</para>
	/// </remarks>
	public static class CoverageInventory
	{

		private static readonly string[] AtomicOps = [ "add", "bit-and", "bit-or", "bit-xor", "max", "min", "byte-min", "byte-max", "append-if-fits", "compare-and-clear" ];

		private static readonly string[] AtomicCases = [ "absent", "shorter", "equal", "longer" ];

		private static readonly string[] SelectorBases = [ "llt", "lle", "fgt", "fge" ];

		private static readonly string[] SelectorOffsets = [ "off0", "pos", "neg" ];

		private static readonly string[] SelectorLandings = [ "hit", "gap", "edge" ];

		private static readonly string[] RywReadKinds = [ "get", "range-fwd", "range-rev", "range-limited", "selector" ];

		private static readonly string[] RywOverlayStates = [ "over-set", "over-clear", "over-clearrange", "over-atomic", "over-chain" ];

		/// <summary>All the cells, grouped; built once.</summary>
		public static IReadOnlyList<CoverageCell> Cells { get; } = BuildCells();

		/// <summary>Index of <see cref="Cells"/> by id.</summary>
		public static IReadOnlyDictionary<string, CoverageCell> ById { get; } = Cells.ToDictionary(c => c.Id);

		private static List<CoverageCell> BuildCells()
		{
			var cells = new List<CoverageCell>();

			// atomics: op x operand case (operand length relative to the existing value; "absent" = no existing value)
			foreach (var op in AtomicOps)
			{
				foreach (var cas in AtomicCases)
				{
					cells.Add(new($"atomics/{op}-{cas}", $"atomic {op} with {(cas == "absent" ? "no existing value" : $"operand {cas} than the existing value")}"));
				}
			}

			// selectors: named base x extra-offset class x landing, plus one system-boundary cell per base
			foreach (var b in SelectorBases)
			{
				foreach (var off in SelectorOffsets)
				{
					foreach (var landing in SelectorLandings)
					{
						cells.Add(new($"selectors/{b}-{off}-{landing}", $"selector {b} with {off} extra offset landing on {landing}"));
					}
				}
				cells.Add(new($"selectors/{b}-system", $"selector {b} resolving at the system-key boundary"));
			}

			// watches: trigger kinds and lifecycle outcomes
			cells.AddRange((CoverageCell[])
			[
				new("watches/fire-on-set", "watch fires on a committed Set of the key"),
				new("watches/fire-on-clear", "watch fires on a committed Clear of the key"),
				new("watches/fire-on-clearrange", "watch fires on a committed ClearRange covering the key"),
				new("watches/fire-on-atomic", "watch fires on a committed atomic mutation of the key"),
				new("watches/fire-on-versionstamped", "watch fires on a committed versionstamped write of the key"),
				new("watches/no-fire-identical-value", "watch does not fire when the committed write stores the identical value"),
				new("watches/no-fire-single-commit-aba", "watch does not fire when one commit writes away and back (single-commit ABA)"),
				new("watches/fire-two-commit-aba", "watch fires when two commits write away then back (two-commit ABA)"),
				new("watches/own-tx-write-baseline", "the watching transaction's own write is the watch baseline"),
				new("watches/settle-on-dispose", "watch settles with TransactionCancelled when its transaction is disposed uncommitted"),
				new("watches/settle-on-reset", "watch settles with TransactionCancelled when its transaction is reset"),
				new("watches/settle-on-conflicted-commit", "watch settles with the commit error when its transaction conflicts"),
				new("watches/metadata-version-watch", "watch on the metadata-version key"),
				new("watches/pending-untouched-key", "watch stays pending while the key is untouched"),
			]);

			// versionstamps: placeholder placement, future fate, ordering relations
			cells.AddRange((CoverageCell[])
			[
				new("versionstamps/placement-key-begin", "stamped key with the placeholder at offset 0"),
				new("versionstamps/placement-key-mid", "stamped key with the placeholder mid-key"),
				new("versionstamps/placement-key-end", "stamped key with the placeholder at the key tail"),
				new("versionstamps/placement-value", "stamped value placeholder"),
				new("versionstamps/placement-tupack", "stamped key built through the tuple-encoding placeholder path"),
				new("versionstamps/fate-committed", "versionstamp future settles with the stamp on a committed transaction"),
				new("versionstamps/fate-conflicted", "versionstamp future fails with TransactionInvalidVersion on a conflicted commit"),
				new("versionstamps/fate-cancelled", "versionstamp future fails when the transaction never commits"),
				new("versionstamps/stamp-equals-commit-version", "the observed stamp equals the transaction's committed version"),
				new("versionstamps/monotonic-sequential", "stamps strictly increase over sequential commits"),
				new("versionstamps/monotonic-interleaved", "stamps strictly increase over interleaved commits"),
				new("versionstamps/two-stamps-user-versions", "two stamped ops in one transaction ordered by user version"),
				new("versionstamps/stamped-key-readback", "a stamped key reads back with the materialized stamp"),
				new("versionstamps/stamped-value-readback", "a stamped value reads back with the materialized stamp"),
			]);

			// ryw: read kind x uncommitted overlay state under the read, plus the option-interaction specials
			foreach (var kind in RywReadKinds)
			{
				foreach (var state in RywOverlayStates)
				{
					cells.Add(new($"ryw/{kind}-{state}", $"{kind} read over an uncommitted {state.Substring("over-".Length)}"));
				}
			}
			cells.AddRange((CoverageCell[])
			[
				new("ryw/snapshot-sees-own-writes-default", "snapshot reads see the transaction's own writes by default"),
				new("ryw/snapshot-ryw-disabled", "snapshot reads stop seeing own writes under SnapshotReadYourWritesDisable"),
				new("ryw/disable-before-read", "ReadYourWritesDisable before any read is accepted and honored"),
				new("ryw/disable-after-read-poisons-reads", "ReadYourWritesDisable after a read fails subsequent reads with ClientInvalidOperation"),
				new("ryw/disable-after-read-poisons-commit", "ReadYourWritesDisable after a read fails the commit with ClientInvalidOperation"),
				new("ryw/disable-writes-only", "ReadYourWritesDisable after writes but before any read (uncharacterized trigger)"),
			]);

			// conflicts
			cells.AddRange((CoverageCell[])
			[
				new("conflicts/read-write-conflict-loses", "a transaction that read a key another transaction committed loses its commit"),
				new("conflicts/conflict-after-range-read", "a range read establishes the conflict that fails the commit"),
				new("conflicts/conflict-after-selector-read", "a selector read establishes the conflict that fails the commit"),
				new("conflicts/write-write-no-conflict", "blind writes to the same key from two transactions both commit"),
				new("conflicts/snapshot-read-no-conflict", "a snapshot read does not establish a conflict"),
				new("conflicts/own-read-own-write-no-conflict", "reading a key the same transaction wrote does not self-conflict"),
				new("conflicts/explicit-read-conflict-range", "an explicitly added read conflict range conflicts like a real read"),
				new("conflicts/explicit-write-conflict-range", "an explicitly added write conflict range conflicts like a real write"),
				new("conflicts/report-conflicting-keys-option", "the ReportConflictingKeys option is accepted and collects ranges"),
				new("conflicts/conflicting-keys-special-keyspace", "the conflicting-keys special keyspace serves boundary pairs after a failed commit"),
			]);

			// errors: curated v1 starter set (grows with dual-backend error-translation assertions)
			cells.AddRange((CoverageCell[])
			[
				new("errors/not-committed", "NotCommitted surfaces on a conflicted commit"),
				new("errors/transaction-cancelled", "TransactionCancelled surfaces on a cancelled transaction's futures"),
				new("errors/transaction-invalid-version", "TransactionInvalidVersion surfaces on a failed commit's versionstamp future"),
				new("errors/transaction-too-old", "TransactionTooOld surfaces past the read-version window"),
				new("errors/client-invalid-operation", "ClientInvalidOperation surfaces on an illegal client-side operation"),
				new("errors/commit-unknown-result", "CommitUnknownResult semantics on an ambiguous commit"),
				new("errors/transaction-timed-out", "TransactionTimedOut surfaces when the transaction timeout lapses"),
				new("errors/invalid-mutation-type", "InvalidMutationType surfaces for a mutation not supported at the selected API level"),
				new("errors/key-outside-legal-range", "KeyOutsideLegalRange surfaces for keys beyond the legal keyspace"),
				new("errors/inverted-range", "InvertedRange surfaces when a range's begin sorts after its end"),
				new("errors/value-too-large", "ValueTooLarge surfaces past the value size limit"),
				new("errors/key-too-large", "KeyTooLarge surfaces past the key size limit"),
				new("errors/transaction-too-large", "TransactionTooLarge surfaces past the transaction size limit"),
				new("errors/invalid-option-value", "InvalidOptionValue surfaces for a malformed option argument"),
				new("errors/used-during-commit", "UsedDuringCommit surfaces for operations issued while the commit is in flight"),
			]);

			// options
			cells.AddRange((CoverageCell[])
			[
				new("options/ryw-disable-accepted", "ReadYourWritesDisable is accepted at a legal time"),
				new("options/snapshot-ryw-disable-accepted", "SnapshotReadYourWritesDisable is accepted"),
				new("options/timeout", "the Timeout option arms and fires"),
				new("options/retry-limit", "the RetryLimit option bounds the retry loop"),
				new("options/size-limit", "the SizeLimit option bounds the transaction size"),
				new("options/access-system-keys", "AccessSystemKeys gates system-keyspace reads"),
				new("options/causal-read-risky", "CausalReadRisky is accepted"),
				new("options/next-write-no-write-conflict-range", "NextWriteNoWriteConflictRange suppresses the next write's conflict range"),
			]);

			// introspection
			cells.AddRange((CoverageCell[])
			[
				new("introspection/read-version", "GetReadVersion returns the transaction's read version"),
				new("introspection/committed-version", "GetCommittedVersion returns the committed version after commit"),
				new("introspection/versionstamp-future", "GetVersionstamp registers the stamp future"),
				new("introspection/metadata-version-get", "the metadata version reads back"),
				new("introspection/metadata-version-touch", "bumping the metadata version is observable"),
				new("introspection/approximate-size", "GetApproximateSize accounts staged mutations"),
				new("introspection/estimated-range-size", "GetEstimatedRangeSizeBytes estimates a range"),
				new("introspection/split-points", "GetRangeSplitPoints returns endpoint-bounded split points"),
				new("introspection/addresses", "GetAddressesForKey returns storage addresses in the level-gated format"),
			]);

			return cells;
		}

	}

}
