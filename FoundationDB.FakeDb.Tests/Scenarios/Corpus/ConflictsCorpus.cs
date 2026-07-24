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

	/// <summary>The conflict-exemption scenarios: commits that must SUCCEED although they raced a writer (the losing cases live in the harness scenarios and the conformance facts).</summary>
	public static class ConflictsCorpus
	{

		/// <summary>All the scenarios of the conflicts corpus.</summary>
		public static IEnumerable<Scenario> All()
		{
			yield return WriteWriteNoConflict();
			yield return SnapshotReadNoConflict();
		}

		private static Scenario WriteWriteNoConflict()
		{
			var b = new ScenarioBuilder();

			// A prepares a write to k1, B pins its read version, then A commits FIRST
			b.Begin("A");
			b.Set("A", "k1", "a1");
			b.Begin("B");
			b.Set("B", "k9", "b0"); // B's first key-using step pins its read version, before A's commit
			b.Commit("A");

			// B blindly overwrites the key A just committed: writes never conflict with writes
			b.Set("B", "k1", "b1");
			b.Commit("B");

			// the second writer won
			b.Begin("A");
			b.Get("A", "k1");
			b.Commit("A");

			return b.Build("conflict_write_write_no_conflict",
				"two transactions write the same key and the second commits after the first landed inside its window: blind writes never conflict, last writer wins");
		}

		private static Scenario SnapshotReadNoConflict()
		{
			var b = new ScenarioBuilder();

			// a committed baseline value
			b.Begin("A");
			b.Set("A", "k1", "a1");
			b.Commit("A");

			// B snapshot-reads the key, then A overwrites and commits INSIDE B's window
			b.Begin("B");
			b.Get("B", "k1", snapshot: true);
			b.Begin("A");
			b.Set("A", "k1", "a2");
			b.Commit("A");

			// B writes something and commits: the snapshot read established no conflict range
			b.Set("B", "k2", "b1");
			b.Commit("B");

			// both writes landed
			b.Begin("A");
			b.Get("A", "k1");
			b.Get("A", "k2");
			b.Commit("A");

			return b.Build("conflict_snapshot_read_no_conflict",
				"a snapshot read of a key that a peer overwrites inside the window establishes no conflict: the reader's commit succeeds");
		}

	}

}
