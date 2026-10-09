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

namespace FoundationDB.FdbLite.Bench
{

	/// <summary>Drives FdbLite at the storage level: engine and pager, without the transaction layer.</summary>
	/// <remarks>
	/// <para>Every store opened here runs with pre-commit consolidation pinned to <c>Off</c>. A file store opens with the <c>Adaptive</c> policy by default, which is wall-clock driven, so a run that inherited it would measure the policy on top of the engine and two runs would differ for reasons unrelated to the change under test. The header of every run states the pin.</para>
	/// </remarks>
	public sealed class FdbLiteEngineDriver : IKvDriver
	{

		/// <param name="path">File of the store. An existing file is deleted first, unless <paramref name="reopen"/> is set.</param>
		/// <param name="geometry">Page geometry of a fresh store. An existing file keeps the geometry of its header.</param>
		/// <param name="reopen">Open the file as it stands on disk instead of starting from an empty store.</param>
		/// <param name="initialSizeInBytes">Bytes reserved up front by a fresh store; 0 grows on demand.</param>
		/// <param name="bulkImport">Make <see cref="TryBulkLoad"/> render the dataset through <see cref="FdbLiteEngine.Import"/> instead of reporting no bulk path.</param>
		/// <param name="configure">Applied to every engine this driver opens, including after <see cref="Reopen"/>, after the consolidation pin.</param>
		public FdbLiteEngineDriver(string path, FdbLiteGeometry geometry, bool reopen = false, long initialSizeInBytes = 0, bool bulkImport = false, Action<FdbLiteEngine>? configure = null)
		{
			Contract.NotNullOrEmpty(path);
			this.Path = path;
			this.Geometry = geometry;
			this.InitialSizeInBytes = initialSizeInBytes;
			this.BulkImport = bulkImport;
			this.Configure = configure;
			if (!reopen && File.Exists(path)) File.Delete(path);
			this.Engine = Open();
			this.Version = this.Engine.Durable.DatabaseVersion;
		}

		private FdbLiteEngine Open()
		{
			// the geometry argument is ignored when the file already exists: the header is read back instead
			var engine = FdbLiteEngine.OpenOrCreateFile(this.Path, this.Geometry, initialSizeInBytes: this.InitialSizeInBytes);
			engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Off;
			this.Configure?.Invoke(engine);
			return engine;
		}

		string IKvDriver.Engine => "fdblite";

		private string Path { get; }

		private FdbLiteGeometry Geometry { get; }

		private long InitialSizeInBytes { get; }

		private bool BulkImport { get; }

		private Action<FdbLiteEngine>? Configure { get; }

		/// <summary>The engine of the store currently open. Replaced by <see cref="Reopen"/>.</summary>
		public FdbLiteEngine Engine { get; private set; }

		/// <summary>Delete the store file with the driver. Off when the caller manages the file (a cold series reopens a store it built earlier).</summary>
		public bool DeleteOnDispose { get; init; } = true;

		public bool BuiltViaBulk { get; private set; }

		private FdbLiteTreeWriter? Writer { get; set; }

		private FdbLiteEngine.ReadSnapshot? Pin { get; set; }

		/// <summary>Database version stamped on the next commit. Continues from the durable header after a reopen.</summary>
		private ulong Version { get; set; }

		private long TotalSplits { get; set; }

		private long TotalCopies { get; set; }

		private long TotalPagesWritten { get; set; }

		private long TotalOverwritten { get; set; }

		private long TotalReplacesRebuilt { get; set; }

		private long TotalDescents { get; set; }

		private long TotalAppended { get; set; }

		private long TotalLeafSplits { get; set; }

		private long TotalSpliced { get; set; }

		private long TotalSplitSiblings { get; set; }

		private long TotalStripped { get; set; }

		public long FileSize => new FileInfo(this.Path).Length;

		/// <summary>The <c>.verify</c> scan legs measure the read path that checks each page's checksum again on first touch, which is what a reopened store pays.</summary>
		public void OnTimedSectionStarting(Workload load)
		{
			if (load.Name.EndsWith(".verify", StringComparison.Ordinal))
			{
				this.Engine.Pager.ResetFirstTouch();
			}
		}

		/// <summary>Reads run against a pinned committed generation; the pin is dropped before the next batch.</summary>
		private FdbLiteEngine.ReadSnapshot AcquirePin()
		{
			this.Pin ??= this.Engine.BeginRead();
			return this.Pin.Value;
		}

		private void ReleasePin()
		{
			if (this.Pin is { } pin)
			{
				this.Engine.EndRead(in pin);
				this.Pin = null;
			}
		}

		public void BeginBatch()
		{
			Contract.Requires(this.Writer is null, "a batch is already open");
			ReleasePin();
			this.Writer = this.Engine.BeginWrite();
		}

		private FdbLiteTreeWriter Current => this.Writer ?? throw new InvalidOperationException("no batch is open");

		public void Set(Slice key, Slice value) => this.Current.Insert(key.Span, value.Span);

		/// <summary>Exclusive upper bound handed to <see cref="FdbLiteEngine.Import"/>, above every key the workloads generate. Import throws on a key outside the range.</summary>
		private static readonly Slice ImportUpperBound = Slice.FromByte(0xFF);

		/// <summary>Renders the whole run into full pages instead of inserting it key by key.</summary>
		/// <remarks>Import owns its own writer and commits its own generation, so the per-writer counters (splits, copies, descents) stay at zero for this path.</remarks>
		public bool TryBulkLoad(IEnumerable<KeyValuePair<Slice, Slice>> sorted, int count)
		{
			if (!this.BulkImport) return false;
			Contract.Requires(this.Writer is null, "a batch is already open");
			ReleasePin();
			this.Engine.Import(sorted, Slice.Empty, ImportUpperBound, FdbLiteImportOptions.Default, ++this.Version);
			this.BuiltViaBulk = true;
			return true;
		}

		public void Clear(Slice key) => this.Current.Remove(key.Span);

		public void ClearRange(Slice begin, Slice end) => this.Current.RemoveRange(begin.Span, end.Span);

		public void Commit()
		{
			var w = this.Current;
			this.Engine.Commit(w, ++this.Version);
			// read after the commit: PagesWritten is incremented by the flush that Commit performs
			this.TotalSplits += w.PageSplits;
			this.TotalCopies += w.PageCopies;
			this.TotalPagesWritten += w.PagesWritten;
			this.TotalOverwritten += w.CellsOverwritten;
			this.TotalReplacesRebuilt += w.ReplacesRebuilt;
			this.TotalDescents += w.LeafDescents;
			this.TotalAppended += w.PagesAppended;
			this.TotalLeafSplits += w.LeafSplits;
			this.TotalSpliced += w.CellsSpliced;
			this.TotalSplitSiblings += w.SplitSiblingsCreated;
			this.TotalStripped += w.PagesStripped;
			this.Writer = null;
		}

		public int Probe(Slice key)
		{
			var pin = AcquirePin();
			return FdbLiteTreeReader.TryGetValue(this.Engine.Pager, pin.RootPageId, key.Span, out var value) ? value.Length : -1;
		}

		public Slice Read(Slice key)
		{
			var pin = AcquirePin();
			// copy: the span points into pager memory and dies with the pin
			return FdbLiteTreeReader.TryGetValue(this.Engine.Pager, pin.RootPageId, key.Span, out var value) ? Slice.FromBytes(value) : Slice.Nil;
		}

		public List<KeyValuePair<Slice, Slice>> Scan(Slice begin, Slice end, int limit)
		{
			var pin = AcquirePin();
			var res = new List<KeyValuePair<Slice, Slice>>();
			var cursor = new FdbLiteTreeCursor(this.Engine.Pager, pin.RootPageId);
			if (!cursor.SeekCeiling(begin.Span)) return res;
			do
			{
				if (end.Count != 0 && cursor.CurrentKey.SequenceCompareTo(end.Span) >= 0) break;
				// copies: the spans point into pager memory and die with the pin
				res.Add(new(Slice.FromBytes(cursor.CurrentKey), Slice.FromBytes(cursor.CurrentValue)));
				if (limit > 0 && res.Count >= limit) break;
			}
			while (cursor.MoveNext());
			return res;
		}

		public int ScanCount(Slice begin, Slice end, int limit, bool reverse, bool readValues)
		{
			var pin = AcquirePin();
			int n = 0;
			var cursor = new FdbLiteTreeCursor(this.Engine.Pager, pin.RootPageId);
			// with a row limit the end bound is never reached, so the key is not assembled per row
			bool bounded = limit <= 0 && end.Count != 0;
			if (!reverse)
			{
				if (!cursor.SeekCeiling(begin.Span)) return 0;
				do
				{
					if (bounded && cursor.CurrentKey.SequenceCompareTo(end.Span) >= 0) break;
					if (readValues) { Sink += cursor.CurrentValue.Length; }
					++n;
					if (limit > 0 && n >= limit) break;
				}
				while (cursor.MoveNext());
			}
			else
			{
				// orEqual false: end is exclusive, so start at the last key strictly below it
				if (!cursor.SeekFloor(end.Span, orEqual: false)) return 0;
				do
				{
					if (limit <= 0 && cursor.CurrentKey.SequenceCompareTo(begin.Span) < 0) break;
					if (readValues) { Sink += cursor.CurrentValue.Length; }
					++n;
					if (limit > 0 && n >= limit) break;
				}
				while (cursor.MovePrevious());
			}
			return n;
		}

		/// <summary>Keeps the value read observable when <c>readValues</c> is set.</summary>
		private static long Sink;

		public IEnumerable<KeyValuePair<string, long>> Stats()
		{
			ReleasePin();
			var s = this.Engine.GetStats();
			yield return new("pageSplits", this.TotalSplits);
			yield return new("pageCopies", this.TotalCopies);
			yield return new("pagesWritten", this.TotalPagesWritten);
			yield return new("cellsOverwritten", this.TotalOverwritten);
			yield return new("replacesRebuilt", this.TotalReplacesRebuilt);
			yield return new("leafDescents", this.TotalDescents);
			yield return new("pagesAppended", this.TotalAppended);
			yield return new("leafSplits", this.TotalLeafSplits);
			yield return new("cellsSpliced", this.TotalSpliced);
			yield return new("splitSiblings", this.TotalSplitSiblings);
			yield return new("pagesStripped", this.TotalStripped);
			yield return new("keyCount", (long) this.Engine.Durable.KeyCount);
			// the file length is a reservation that grows in large steps; the frontier is what the allocator handed out
			yield return new("allocationFrontier", (long) this.Engine.Durable.AllocationFrontier);
			yield return new("pendingReclaimBlocks", s.PendingReclaimBlocks);
			var (reusable, pending) = this.Engine.MeasureFreeSpace();
			yield return new("freeReusableBytes", reusable);
			yield return new("freePendingBytes", pending);
			var t = this.Engine.MeasureTreeStatistics();
			yield return new("leafPages", t.LeafPages);
			yield return new("internalPages", t.InternalPages);
			yield return new("cellCount", t.CellCount);
			yield return new("leafLiveBytes", t.LeafLiveBytes);
			yield return new("wastedBytes", t.WastedBytes);
			yield return new("freeGapBytes", t.FreeGapBytes);
			long leafCapacity = (long) t.LeafPages * this.Engine.Pager.Geometry.PageSize;
			yield return new("leafFillPct", leafCapacity == 0 ? 0 : (long) (100.0 * t.LeafLiveBytes / leafCapacity));
			yield return new("fileBytes", this.FileSize);
		}

		public IReadOnlyList<string>? Audit()
		{
			var pin = AcquirePin();
			return FdbLiteTreeAudit.Check(this.Engine.Pager, pin.RootPageId);
		}

		public bool Reopen()
		{
			Contract.Requires(this.Writer is null, "a batch is still open");
			ReleasePin();
			this.Engine.Dispose();
			this.Engine = Open();
			this.Version = this.Engine.Durable.DatabaseVersion;
			return true;
		}

		public void Dispose()
		{
			ReleasePin();
			this.Engine.Dispose();
			if (this.DeleteOnDispose)
			{
				try { File.Delete(this.Path); } catch { /* best effort */ }
			}
		}

	}

}
