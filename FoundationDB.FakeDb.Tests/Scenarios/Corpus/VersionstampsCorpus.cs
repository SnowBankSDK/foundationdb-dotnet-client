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
	using SnowBank.Data.Tuples;

	/// <summary>Versionstamps campaign corpus: commit-time stamps, key/value placeholders, user-version ordering, and stamp fate on failed commits.</summary>
	public static class VersionstampsCorpus
	{

		/// <summary>All the scenarios of the versionstamps campaign.</summary>
		public static IEnumerable<Scenario> All()
		{
			yield return MonotonicSequential();
			yield return InterleavedCommits();
			yield return KeyOffsetMidKey();
			yield return MultipleStampedOpsUserVersions();
			yield return ValueStamped();
			yield return IncompleteTuPackLayout();
			yield return StampDiffersAfterConflictRetry();
		}

		private static Scenario MonotonicSequential()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "one");
			int vs1 = b.GetVersionstamp("A");
			b.Commit("A");
			b.GetCommittedVersion("A");        // == stamp of the first commit
			b.ExpectVersionstamp(vs1);
			b.Begin("A");
			b.Set("A", "k2", "two");
			int vs2 = b.GetVersionstamp("A");
			b.Commit("A");
			b.GetCommittedVersion("A");
			b.ExpectVersionstamp(vs2);         // strictly later: a new symbol, in order of appearance
			return b.Build("vs_monotonic_sequential", "sequential commits produce strictly increasing, unique stamps equal to their commit versions");
		}

		private static Scenario InterleavedCommits()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Begin("B");
			b.Set("A", "kA", "a");
			int vsA = b.GetVersionstamp("A");
			b.Set("B", "kB", "b");
			int vsB = b.GetVersionstamp("B");
			b.Commit("A");
			b.Commit("B");                     // sequential in script order, so B's stamp is strictly later
			b.ExpectVersionstamp(vsA);
			b.ExpectVersionstamp(vsB);
			return b.Build("vs_interleaved_commits", "two interleaved transactions committing sequentially get distinct, ordered stamps");
		}

		private static Scenario KeyOffsetMidKey()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			// the 10-byte placeholder sits in the MIDDLE of the key: "log-" + <stamp> + "-tail"
			b.SetVersionstampedKey("A", Slice.FromStringAscii("log-") + Slice.Zero(10) + Slice.FromStringAscii("-tail"), 4, "payload");
			int vs = b.GetVersionstamp("A");
			b.Commit("A");
			b.ExpectVersionstamp(vs);          // registers the stamp for rendering: the dump key must show log-<vN#o>-tail
			return b.Build("vs_key_offset_mid_key", "a versionstamp placeholder in the middle of the key is overwritten at the given offset");
		}

		private static Scenario MultipleStampedOpsUserVersions()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			// two stamped keys in one transaction, disambiguated by their 2-byte user-version suffix (part of the key bytes)
			b.SetVersionstampedKey("A", Slice.FromStringAscii("evt-") + VersionStamp.Incomplete(0).ToSlice(), 4, "first");
			b.SetVersionstampedKey("A", Slice.FromStringAscii("evt-") + VersionStamp.Incomplete(1).ToSlice(), 4, "second");
			int vs = b.GetVersionstamp("A");
			b.Commit("A");
			b.ExpectVersionstamp(vs);          // both keys share the commit stamp; the user-version bytes order them
			b.Begin("A");
			b.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("evt-"), OrEqual: false, Offset: 1),
				new ScenarioSelector(Slice.FromStringAscii("evt."), OrEqual: false, Offset: 1)); // '.' > '-' in ascii
			b.Dispose("A");
			return b.Build("vs_multiple_stamped_ops_user_versions", "two stamped keys in one transaction share the commit stamp and are ordered by their user version");
		}

		private static Scenario ValueStamped()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.SetVersionstampedValue("A", "marker", Slice.FromStringAscii("ver=") + Slice.Zero(10), 4);
			int vs = b.GetVersionstamp("A");
			b.Commit("A");
			b.ExpectVersionstamp(vs);
			b.Begin("A");
			b.Get("A", "marker");              // "ver=<vN#o>"
			b.Dispose("A");
			return b.Build("vs_value_stamped", "a versionstamp placeholder inside a value is overwritten at the given offset");
		}

		private static Scenario IncompleteTuPackLayout()
		{
			// the key uses the REAL tuple encoding of an incomplete stamp (type byte + 12-byte placeholder),
			// with the offset computed from the packed layout, what the high-level tuple API does under the hood
			var packed = TuPack.Pack(STuple.Create("evt", VersionStamp.Incomplete(7)));
			int offset = packed.Span.IndexOf(VersionStamp.Incomplete(7).ToSlice().Span);

			var b = new ScenarioBuilder();
			b.Begin("A");
			b.SetVersionstampedKey("A", packed, offset, "tupack");
			int vs = b.GetVersionstamp("A");
			b.Commit("A");
			b.ExpectVersionstamp(vs);
			return b.Build("vs_incomplete_tupack_layout", "a TuPack-encoded incomplete stamp placeholder is completed at commit");
		}

		private static Scenario StampDiffersAfterConflictRetry()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Get("A", "k1");                  // A reads k1...
			b.Begin("B");
			b.Set("B", "k1", "b1");
			int vsB = b.GetVersionstamp("B");
			b.Commit("B");                     // ...B commits first
			b.ExpectVersionstamp(vsB);
			b.Set("A", "k2", "a1");
			int vsA1 = b.GetVersionstamp("A");
			b.Commit("A");                     // conflict: A's commit fails
			b.ExpectVersionstamp(vsA1);        // observation: the stamp future of a failed commit
			b.Begin("A");                      // "retry" as a fresh transaction
			b.Set("A", "k2", "a1");
			int vsA2 = b.GetVersionstamp("A");
			b.Commit("A");
			b.ExpectVersionstamp(vsA2);        // a NEW stamp, different from B's
			return b.Build("vs_stamp_differs_after_conflict_retry", "the stamp future of a conflicted commit fails; the retry gets a fresh, later stamp");
		}

	}

}
