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

namespace FoundationDB.Storage.FdbLite.Tests
{
	using System.Buffers.Binary;
	using FoundationDB.Storage.FdbLite;

	/// <summary>Independent page-accounting oracle over the in-place mutation paths (overwrite, relocate, remove).</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLitePageAccountingFacts : SimpleTest
	{

		private static byte[] Key(int i)
		{
			var key = new byte[8];
			BinaryPrimitives.WriteInt64BigEndian(key, i);
			return key;
		}

		/// <summary>Re-derives, from the slot directory alone, what a leaf page's header CLAIMS about itself.</summary>
		/// <remarks>Deliberately duplicated instead of calling the internal accessors: an oracle that shares code with the thing it audits cannot arbitrate it.</remarks>
		private static void AuditLeafAccounting(ReadOnlySpan<byte> page, uint pageId, int pageSize, List<string> problems)
		{
			int count = FdbLitePageHeader.GetCellCount(page);
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			int prefixRegion = (prefixLen + 1) & ~1;
			int slotsAt = 128 + prefixRegion; // the 128-byte universal header, spelled as this oracle's own constant
			// The directory reserves slots AHEAD of the cell count, so the key heap begins after the reserved
			// span, not after the live one. Re-derived here from the header rather than by calling the engine's
			// own helper, which would forfeit this oracle's independence. A capacity of 0 means "no headroom",
			// which is what a page built by a rebuild carries.
			int capacity = FdbLitePageHeader.GetSlotCapacity(page);
			int keyBase = slotsAt + (Math.Max(capacity, count) * 2);
			int keyUsed = FdbLitePageHeader.GetKeyAreaLength(page);
			int area = FdbLitePageHeader.GetCellAreaOffset(page);
			int frontier = area != 0 ? area : pageSize;
			int waste = FdbLitePageHeader.GetWastedBytes(page);

			if (keyBase + keyUsed > frontier)
			{
				problems.Add($"leaf {pageId}: heaps CROSS (key heap ends at {keyBase + keyUsed}, values start at {frontier})");
				return;
			}

			// live bytes, gathered through the directory (which is the only thing that says what is alive)
			long liveEntries = 0, liveValues = 0;
			var keyCover = new bool[Math.Max(keyUsed, 1)];
			var valueCover = new bool[Math.Max(pageSize - frontier, 1)];
			for (int i = 0; i < count; i++)
			{
				int rel = BinaryPrimitives.ReadUInt16LittleEndian(page[(slotsAt + (i * 2))..]);
				int entry = keyBase + rel;
				int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]);
				int entryBytes = 2 + keyLen + 5;
				int f = entry + 2 + keyLen;
				int valueOffset = BinaryPrimitives.ReadUInt16LittleEndian(page[f..]);
				int valueLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(f + 2)..]);

				if (rel + entryBytes > keyUsed)
				{
					problems.Add($"leaf {pageId} cell {i}: key entry [{rel}..{rel + entryBytes}) runs past the declared key area ({keyUsed})");
					return;
				}
				for (int b = rel; b < rel + entryBytes; b++)
				{
					if (keyCover[b]) { problems.Add($"leaf {pageId} cell {i}: key entry OVERLAPS another at heap offset {b}"); return; }
					keyCover[b] = true;
				}

				if (valueLen > 0)
				{
					if (valueOffset < frontier || valueOffset + valueLen > pageSize)
					{
						problems.Add($"leaf {pageId} cell {i}: value [{valueOffset}..{valueOffset + valueLen}) is outside the value heap [{frontier}..{pageSize})");
						return;
					}
					for (int b = valueOffset - frontier; b < valueOffset - frontier + valueLen; b++)
					{
						if (valueCover[b]) { problems.Add($"leaf {pageId} cell {i}: value OVERLAPS another at page offset {frontier + b}"); return; }
						valueCover[b] = true;
					}
				}

				liveEntries += entryBytes;
				liveValues += valueLen;
			}

			// THE IDENTITY: every byte inside the two heaps is either reachable through a slot or booked as wasted
			long deadInKeyHeap = keyUsed - liveEntries;
			long deadInValueHeap = (pageSize - frontier) - liveValues;
			if (deadInKeyHeap + deadInValueHeap != waste)
			{
				problems.Add($"leaf {pageId}: WASTE MISCOUNTED - {deadInKeyHeap} dead key bytes + {deadInValueHeap} dead value bytes = {deadInKeyHeap + deadInValueHeap}, header says {waste} ({count} cells, prefixLen={prefixLen})");
			}
		}

		private static List<string> AuditAllLeaves(IFdbLitePager pager)
		{
			var problems = new List<string>();
			int pageSize = pager.Geometry.PageSize;
			uint step = (uint) pager.Geometry.BlocksPerPage;
			int leaves = 0;
			for (uint id = step; id + step <= pager.BlockCount; id += step)
			{
				var page = pager.ReadBlocks(id, pager.Geometry.BlocksPerPage);
				// only pages that verify against their own location are real tree pages
				if (!FdbLitePageHeader.Verify(page, id)) continue;
				if (FdbLitePageHeader.GetPageType(page) != FdbLitePageType.Leaf) continue;
				leaves++;
				AuditLeafAccounting(page, id, pageSize, problems);
			}
			Log($"# audited {leaves} leaf pages");
			return problems;
		}

		[Test]
		public void Probe_InPlace_Delete_Fires_And_Page_Accounting_Holds()
		{
			foreach (var geometry in new[] { FdbLiteGeometry.Uniform(14), FdbLiteGeometry.Default, FdbLiteGeometry.Hypothesis })
			{
				using var pager = new FdbLiteHeapPager(geometry);
				var engine = FdbLiteEngine.Create(pager);
				var model = new SortedDictionary<int, byte[]>();

				const int N = 20_000;
				var seed = engine.BeginWrite();
				for (int i = 0; i < N; i++)
				{
					var v = new byte[16];
					BinaryPrimitives.WriteInt32BigEndian(v, i);
					seed.Insert(Key(i), v);
					model[i] = v;
				}
				engine.Commit(seed, 1);

				// one generation stacking ALL THREE in-place mutations on the same pages:
				// shrink-replace (leaves a hole inside a value), grow-replace (relocates and vacates),
				// delete (closes the directory), then re-insert into the gap that left.
				var w = engine.BeginWrite();
				for (int i = 0; i < N; i++)
				{
					if (i % 4 == 0)
					{ // shrink
						var v = new byte[4];
						BinaryPrimitives.WriteInt32BigEndian(v, i);
						w.Insert(Key(i), v);
						model[i] = v;
					}
					else if (i % 4 == 1)
					{ // grow
						var v = new byte[40];
						BinaryPrimitives.WriteInt32BigEndian(v, i);
						w.Insert(Key(i), v);
						model[i] = v;
					}
					else if (i % 4 == 2)
					{ // delete
						Assert.That(w.Remove(Key(i)), Is.True, $"[{geometry}] key {i} must be present");
						model.Remove(i);
					}
				}
				// and re-insert half of what was deleted, so the freed room is reused in the same generation
				for (int i = 2; i < N; i += 8)
				{
					var v = new byte[24];
					BinaryPrimitives.WriteInt32BigEndian(v, i);
					w.Insert(Key(i), v);
					model[i] = v;
				}

				Log($"[{geometry}] overwritten={w.CellsOverwritten:N0} removedInPlace={w.CellsRemovedInPlace:N0} spliced={w.CellsSpliced:N0} rebuilt={w.ReplacesRebuilt:N0} splits={w.PageSplits:N0} descents={w.LeafDescents:N0}");
				engine.Commit(w, 2);

				Assert.That(w.CellsRemovedInPlace, Is.GreaterThan(0), $"[{geometry}] the in-place delete path must actually fire, or this probe proves nothing");

				var pin = engine.BeginRead();
				try
				{
					// 1. the cross-level structural audit must be SILENT, not merely logged
					var structural = FdbLiteTreeAudit.Check(pager, pin.RootPageId, maxProblems: 8);
					foreach (var p in structural) { Log($"# STRUCT {p}"); }

					// 2. the page-accounting oracle
					var accounting = AuditAllLeaves(pager);
					foreach (var p in accounting.Take(8)) { Log($"# ACCOUNT {p}"); }

					// 3. the content model
					var keys = new List<int>();
					var cursor = new FdbLiteTreeCursor(pager, pin.RootPageId);
					if (cursor.SeekFirst())
					{
						do { keys.Add((int) BinaryPrimitives.ReadInt64BigEndian(cursor.CurrentKey)); } while (cursor.MoveNext());
					}

					Assert.That(structural, Is.Empty, $"[{geometry}] structural audit");
					Assert.That(accounting, Is.Empty, $"[{geometry}] page accounting");
					Assert.That(keys, Is.EqualTo(model.Keys.ToList()).AsCollection, $"[{geometry}] scan does not match the model");
					Assert.That(engine.Durable.KeyCount, Is.EqualTo((ulong) model.Count), $"[{geometry}] KeyCount");

					foreach (var kv in model)
					{
						Assert.That(FdbLiteTreeReader.TryGetValue(pager, pin.RootPageId, Key(kv.Key), out var v), Is.True, $"[{geometry}] key {kv.Key} missing");
						if (!v.SequenceEqual(kv.Value))
						{
							Assert.Fail($"[{geometry}] key {kv.Key}: value is {v.Length} B, model says {kv.Value.Length} B");
						}
					}
				}
				finally
				{
					engine.EndRead(in pin);
				}
			}
		}

		/// <summary>The statistics walk exposes the per-page waste counters as an exact per-generation total.</summary>
		[Test]
		public void Measure_Tree_Statistics_Exposes_The_Booked_Waste()
		{
			var geometry = FdbLiteGeometry.Uniform(14);
			using var pager = new FdbLiteHeapPager(geometry);
			var engine = FdbLiteEngine.Create(pager);

			const int N = 10_000;
			var seed = engine.BeginWrite();
			for (int i = 0; i < N; i++)
			{
				var v = new byte[16];
				BinaryPrimitives.WriteInt32BigEndian(v, i);
				seed.Insert(Key(i), v);
			}
			engine.Commit(seed, 1);

			var fresh = engine.MeasureTreeStatistics();
			Log($"# fresh: {fresh}");
			Assert.That(fresh.LeafPages, Is.GreaterThan(0));
			Assert.That(fresh.CellCount, Is.EqualTo(N));
			Assert.That(fresh.WastedBytes, Is.Zero, "a tree of freshly built pages is compact");

			// shrink every fourth value 16 -> 4 bytes: each shrink stays in its slot and books EXACTLY its
			// 12 bytes of slack, whether it went through copy-and-overwrite (first touch) or the splice path
			var w = engine.BeginWrite();
			int shrunk = 0;
			for (int i = 0; i < N; i += 4)
			{
				var v = new byte[4];
				BinaryPrimitives.WriteInt32BigEndian(v, i);
				w.Insert(Key(i), v);
				shrunk++;
			}
			engine.Commit(w, 2);

			var after = engine.MeasureTreeStatistics();
			Log($"# after {shrunk:N0} shrinks: {after}");
			Assert.That(after.CellCount, Is.EqualTo(N), "a replace changes no count");
			Assert.That(after.WastedBytes, Is.EqualTo(12L * shrunk), "every shrink books exactly its slack, nothing more");
			Assert.That(after.MaxWastedBytesPerPage, Is.GreaterThan(0));
		}

		/// <summary>The same, with random keys and a random op mix, run against a model - the shape the existing fuzz has but with the two oracles wired in.</summary>
		[Test]
		public void Probe_Random_Churn_Against_Both_Oracles()
		{
			foreach (var geometry in new[] { FdbLiteGeometry.Uniform(14), FdbLiteGeometry.Default })
			{
				using var pager = new FdbLiteHeapPager(geometry);
				var engine = FdbLiteEngine.Create(pager);
				var model = new SortedDictionary<int, byte[]>();
				var rnd = new Random(987654);

				ulong version = 1;
				for (int round = 0; round < 12; round++)
				{
					var w = engine.BeginWrite();
					for (int step = 0; step < 4_000; step++)
					{
						int i = rnd.Next(6_000);
						int op = rnd.Next(10);
						if (op < 6 || model.Count == 0)
						{
							var v = new byte[rnd.Next(0, 200)];
							rnd.NextBytes(v);
							w.Insert(Key(i), v);
							model[i] = v;
						}
						else
						{
							bool had = model.Remove(i);
							Assert.That(w.Remove(Key(i)), Is.EqualTo(had), $"[{geometry}] round {round} remove({i})");
						}
					}
					engine.Commit(w, version++);
				}

				var pin = engine.BeginRead();
				try
				{
					var structural = FdbLiteTreeAudit.Check(pager, pin.RootPageId, maxProblems: 8);
					foreach (var p in structural) { Log($"# STRUCT {p}"); }
					var accounting = AuditAllLeaves(pager);
					foreach (var p in accounting.Take(8)) { Log($"# ACCOUNT {p}"); }

					var keys = new List<int>();
					var cursor = new FdbLiteTreeCursor(pager, pin.RootPageId);
					if (cursor.SeekFirst())
					{
						do { keys.Add((int) BinaryPrimitives.ReadInt64BigEndian(cursor.CurrentKey)); } while (cursor.MoveNext());
					}

					Assert.That(structural, Is.Empty, $"[{geometry}] structural audit");
					Assert.That(accounting, Is.Empty, $"[{geometry}] page accounting");
					Assert.That(keys, Is.EqualTo(model.Keys.ToList()).AsCollection, $"[{geometry}] scan does not match the model");
					foreach (var kv in model)
					{
						Assert.That(FdbLiteTreeReader.TryGetValue(pager, pin.RootPageId, Key(kv.Key), out var v), Is.True, $"[{geometry}] key {kv.Key} missing");
						if (!v.SequenceEqual(kv.Value)) { Assert.Fail($"[{geometry}] key {kv.Key} value mismatch ({v.Length} vs {kv.Value.Length} B)"); }
					}
				}
				finally
				{
					engine.EndRead(in pin);
				}
			}
		}

	}

}
