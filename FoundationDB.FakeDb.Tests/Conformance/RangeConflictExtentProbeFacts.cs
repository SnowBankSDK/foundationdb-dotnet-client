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
	using FoundationDB.Testing;

	/// <summary>Probes the REAL cluster's read-conflict extents for selector-bounded range reads with wide/negative offsets (the FDBV-029 investigation): each probe performs one non-snapshot range read, lets a peer commit one write, then reports whether the reader's commit conflicts.</summary>
	/// <remarks>This is an INTERROGATION harness, not a conformance suite: it asserts nothing, it prints a CONFLICT/CLEAN matrix that calibrates the emulator's extent rules (the family-1 FDBV-022/023 methodology re-run at the wider shapes family 3 generates). Seeded keys are <c>b, d, f, h</c>; every probe runs on a clean partition.</remarks>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class RangeConflictExtentProbeFacts : FdbTest
	{

		// begin-side coverage: does the extent reach the walk base and the walked-over keys?
		[TestCase("P01-walked-first", "b", false, +2, "h", false, +1, 0, false, "Set", "b")]     // begin walks b,d from '' -> d; peer touches walked b
		[TestCase("P02-absent-inside", "b", false, +2, "h", false, +1, 0, false, "Set", "c")]    // same walk; peer materializes absent c inside the walk zone
		[TestCase("P03-walked-mid", "b", true, +2, "h", true, +1, 0, false, "Set", "d")]         // begin base b walks d,f -> f, result {f,h}; peer touches walked d
		[TestCase("P04-base-itself", "d", true, +2, "zz", false, +1, 0, false, "Set", "d")]      // begin base d (orEqual, exists) walks f,h -> h; peer rewrites the base d (cannot change the resolution)
		[TestCase("P05-absent-pivot", "e", false, +1, "h", true, +1, 0, false, "Set", "e")]      // begin pivot e absent, resolves f; peer materializes e (would become the resolution)
		[TestCase("P06-below-pivot", "e", false, +1, "h", true, +1, 0, false, "Set", "d")]       // same read; peer touches d BELOW the pivot (cannot change the resolution)
		// empty-result exemption: does an empty resolved range record anything at all?
		[TestCase("P07-empty-eq", "h", false, 0, "h", false, 0, 0, false, "Set", "f")]           // [lLT(h), lLT(h)) = [f, f) empty; peer touches f (the resolution itself)
		[TestCase("P08-empty-inverted", "b", false, +3, "b", false, +1, 0, false, "Set", "d")]   // begin walks to f, end resolves b -> [f, b) inverted-empty; peer touches walked d
		// end-side coverage: does the extent track the end walk/resolution?
		[TestCase("P09-end-slack", "b", false, +1, "d", false, +2, 0, false, "Set", "e")]        // [b, f) returns b,d; peer materializes absent e between last-returned and end-resolution
		[TestCase("P10-end-negbase", "b", false, +1, "h", false, -1, 0, false, "Set", "f")]      // end walks back from base f to d -> [b, d) = {b}; peer touches the end walk base f
		// limit truncation at wide shapes (the family-1 clamp rule re-checked)
		[TestCase("P11-limit-clamp", "b", false, +1, "h", false, +1, 1, false, "Set", "d")]      // [b, h) limit 1 returns {b}; peer touches d above the clamp
		[TestCase("P12-rev-limit", "b", false, +1, "h", false, +1, 1, true, "Set", "d")]         // reverse limit 1 returns {f}; peer touches d below the clamp
		[TestCase("P13-clamp-endwalk", "b", false, +1, "h", false, -1, 1, false, "Set", "f")]    // forward limit 1 returns {b}; peer touches f, the base of the end selector's backward walk
		[TestCase("P14-clamp-poswalk", "b", false, +1, "f", false, +2, 1, false, "Set", "d")]    // forward limit 1 under a POSITIVE-offset end (walks f,h -> h): the clamp applies like the canonical case (seed 1073)
		[TestCase("P15-rev-endwalk", "b", false, +1, "d", false, +2, 1, true, "Set", "e")]       // reverse limit 1, end walks d,f -> f: a key materializing DEEPER in the walk renames the resolution without moving the served keys - CLEAN (refuted the full-walk-zone model)
		[TestCase("P16-rev-firstgap", "b", false, +1, "c", false, +2, 1, true, "Set", "cc")]     // reverse limit 1, end pivot c absent: a key in the FIRST gap [pivot .. first key at/after it] shifts the whole resolution walk - the single-step zone rule (seed 1308)
		public async Task Probe(string name, string bk, bool be, int bo, string ek, bool ee, int eo, int limit, bool reverse, string peerOp, string peerKey)
		{
			using var db = await OpenTestPartitionAsync(name);
			await CleanLocation(db);

			await db.WriteAsync(async tr =>
			{
				var s = await db.Root.Resolve(tr);
				tr.Set(s.Key("b"), Text("B"));
				tr.Set(s.Key("d"), Text("D"));
				tr.Set(s.Key("f"), Text("F"));
				tr.Set(s.Key("h"), Text("H"));
			}, this.Cancellation);

			using var tr1 = db.BeginTransaction(this.Cancellation);
			var subspace = await db.Root.Resolve(tr1);

			var options = new FdbRangeOptions { Limit = limit > 0 ? limit : null, IsReversed = reverse };
			var chunk = await tr1.GetRangeAsync(
				new KeySelector(subspace.Key(bk).ToSlice(), be, bo),
				new KeySelector(subspace.Key(ek).ToSlice(), ee, eo),
				options);
			Log($"{name}: read {chunk.Count} item(s) [{(chunk.Count > 0 ? $"{Pretty(subspace, chunk.First)}..{Pretty(subspace, chunk.Last)}" : "-")}]");

			using (var tr2 = db.BeginTransaction(this.Cancellation))
			{
				var s2 = await db.Root.Resolve(tr2);
				if (peerOp == "Clear") { tr2.Clear(s2.Key(peerKey)); } else { tr2.Set(s2.Key(peerKey), Text("peer")); }
				await tr2.CommitAsync();
			}

			tr1.Set(subspace.Key("zz"), Slice.Empty);
			try
			{
				await tr1.CommitAsync();
				Log($"{name}: CLEAN (peer {peerOp} {peerKey})");
			}
			catch (FdbException e) when (e.Code == FdbError.NotCommitted)
			{
				Log($"{name}: CONFLICT (peer {peerOp} {peerKey})");
			}
		}

		private static string Pretty(IKeySubspace subspace, Slice absolute)
		{
			var k = subspace.ExtractKey(absolute, boundCheck: false);
			return k.ToStringUtf8() ?? k.ToString();
		}

	}

}
