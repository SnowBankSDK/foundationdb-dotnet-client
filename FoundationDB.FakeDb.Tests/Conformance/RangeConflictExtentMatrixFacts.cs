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
	using System.Text;
	using FoundationDB.Testing;

	/// <summary>Exhaustive calibration of the REAL cluster's read-conflict extents for selector-bounded range reads: for every read shape in the envelope, one probe per peer-write position reconstructs the recorded extent as a conflict/clean indicator vector.</summary>
	/// <remarks>
	/// <para>This is the wholesale successor to the incremental <see cref="RangeConflictExtentProbeFacts"/>: instead of one hand-reasoned probe per suspected rule, it enumerates {forward, reverse} x {no limit, limit 1, limit 2} x begin-selector forms x end-selector forms, and probes TWELVE peer positions per shape (each gap and each existing key of the data row <c>b d f h</c>, plus three floor-sentinel positions reachable by floor-anchored begin walks), printing one machine-parseable <c>MATRIX|...</c> line per probe. The extent rules are then fitted OFFLINE to the complete table in one pass.</para>
	/// <para>All probes share one partition: each probe works under its own tuple prefix, with per-prefix absorbing sentinel rows (<c>_0.._3</c> below the data, <c>z0..z3</c> above) so selector walks stay inside the probe's prefix. Each probe is three transactions: the reader (one non-snapshot range read + a dummy write), the peer (one Set), and the reader's commit; CONFLICT/CLEAN is the commit outcome.</para>
	/// </remarks>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class RangeConflictExtentMatrixFacts : FdbTest
	{

		private sealed record SelectorForm(string Pivot, bool OrEqual, int Offset)
		{
			public override string ToString() => $"({this.Pivot},{(this.OrEqual ? "T" : "F")},{this.Offset:+0;-0;0})";
		}

		private static readonly SelectorForm[] BeginForms =
		[
			new("d", false, +1), // canonical fGE on a present key
			new("c", false, +1), // canonical fGE on an absent pivot
			new("d", true, +2),  // orEqual multi-step walk
			new("c", false, -2), // backward walk from an absent pivot
			new("d", false, +3), // wide forward walk
			new("_1", true, +1), // floor anchor: begin resolves inside the floor, below all data
			new("_1", true, +3), // floor walk that lands on the first data key (begin bound far below the end region)
		];

		private static readonly SelectorForm[] EndForms =
		[
			new("f", false, +1), // canonical fGE on a present key
			new("e", false, +1), // canonical fGE on an absent pivot
			new("f", true, +2),  // orEqual multi-step walk
			new("h", false, -1), // backward walk (resolution below the pivot)
			new("e", false, +3), // wide forward walk from an absent pivot
			new("f", false, -2), // deep backward walk
			new("e", true, +2),  // orEqual walk from an absent pivot (resolution base sits below the served span under reverse+limit)
			new("c", true, +2),  // orEqual walk from an absent pivot, base deeper in the row
			new("e", true, +3),  // orEqual walk from an absent pivot into the ceiling
		];

		private static readonly string[] PeerKeys = [ "a", "b", "c", "d", "e", "f", "g", "h", "i", "_1", "_2", "_3" ];

		[Test]
		public async Task Matrix()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			var summary = new StringBuilder();
			int probe = 0, conflicts = 0;

			foreach (var reverse in (bool[]) [ false, true ])
			foreach (var limit in (int?[]) [ null, 1, 2 ])
			foreach (var begin in BeginForms)
			foreach (var end in EndForms)
			{
				var vector = new StringBuilder();
				foreach (var peer in PeerKeys)
				{
					int id = probe++;

					// seed this probe's universe: absorbing sentinels below and above, plus the data row
					await db.WriteAsync(async tr =>
					{
						var s = await db.Root.Resolve(tr);
						for (int i = 0; i < 4; i++)
						{
							tr.Set(s.Key(id, "_" + i), Text("s"));
							tr.Set(s.Key(id, "z" + i), Text("s"));
						}
						tr.Set(s.Key(id, "b"), Text("B"));
						tr.Set(s.Key(id, "d"), Text("D"));
						tr.Set(s.Key(id, "f"), Text("F"));
						tr.Set(s.Key(id, "h"), Text("H"));
					}, this.Cancellation);

					using var tr1 = db.BeginTransaction(this.Cancellation);
					var subspace = await db.Root.Resolve(tr1);
					_ = await tr1.GetRangeAsync(
						new KeySelector(subspace.Key(id, begin.Pivot).ToSlice(), begin.OrEqual, begin.Offset),
						new KeySelector(subspace.Key(id, end.Pivot).ToSlice(), end.OrEqual, end.Offset),
						new FdbRangeOptions { Limit = limit, IsReversed = reverse });

					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var s2 = await db.Root.Resolve(tr2);
						tr2.Set(s2.Key(id, peer), Text("peer"));
						await tr2.CommitAsync();
					}

					tr1.Set(subspace.Key(id, "yy"), Slice.Empty);
					bool conflict;
					try
					{
						await tr1.CommitAsync();
						conflict = false;
					}
					catch (FdbException e) when (e.Code == FdbError.NotCommitted)
					{
						conflict = true;
						conflicts++;
					}
					vector.Append(conflict ? 'X' : '.');
				}

				// one line per shape: the 9-position indicator vector in PeerKeys order (X = conflict)
				var line = $"MATRIX|{(reverse ? "rev" : "fwd")}|{(limit is null ? "all" : "lim" + limit)}|b={begin}|e={end}|{vector}";
				Log(line);
				summary.AppendLine(line);
			}

			Log($"matrix complete: {probe} probes, {conflicts} conflicts");
			Log(summary.ToString());
		}

	}

}
