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
	using FoundationDB.Client;
	using FoundationDB.FakeDb;
	using FoundationDB.Storage;

	/// <summary>Drives a store through the FoundationDB binding API (<see cref="IFdbDatabase"/>), whatever provider sits under it.</summary>
	/// <remarks>
	/// <para>The same workload measured against <see cref="FdbLiteEngineDriver"/> and against this driver isolates the cost of the transaction layer, and this driver over FakeDb and over FdbLite compares the two committed stores behind one API.</para>
	/// <para>The harness is synchronous, so every binding call blocks on its task (a console process without a synchronization context). Reads and scans open one transaction per operation, which is the per-operation price of the API.</para>
	/// </remarks>
	public sealed class BindingDriver : IKvDriver
	{

		private BindingDriver(string engine, FdbEmulatedDatabase store, string? path, FdbLiteGeometry geometry)
		{
			this.Engine = engine;
			this.Store = store;
			this.Db = store.OpenDatabase(FdbPath.Root, readOnly: false, ownsStore: false);
			this.Path = path;
			this.Geometry = geometry;
		}

		public string Engine { get; }

		private IFdbDatabase Db { get; set; }

		/// <summary>The emulated store under <see cref="Db"/>.</summary>
		private FdbEmulatedDatabase Store { get; set; }

		private string? Path { get; }

		private FdbLiteGeometry Geometry { get; }

		private IFdbTransaction? Tr { get; set; }

		private CancellationTokenSource Lifetime { get; } = new();

		/// <summary>Total bytes of the values touched by counted scans.</summary>
		private long Sink;

		/// <summary>The in-memory FakeDb store through the binding.</summary>
		public static BindingDriver ForFakeDb() => new("fakedb", new FakeDbStore(), null, FdbLiteGeometry.Default);

		/// <summary>An FdbLite file store through the binding, at the same geometry as the raw driver.</summary>
		public static BindingDriver ForFdbLite(string path, FdbLiteGeometry geometry)
		{
			if (File.Exists(path)) File.Delete(path);
			return new("fdblite-db", OpenFile(path, geometry), path, geometry);
		}

		/// <summary>The FdbLite engine over the heap pager (no file, every version retained) through the binding: the FakeDb configuration with only the committed store differing.</summary>
		public static BindingDriver ForFdbLiteMem(FdbLiteGeometry geometry)
		{
			var store = FdbLiteStore.CreateInMemory(geometry, retainEveryVersion: true);
			store.Engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Off;
			return new("fdblite-mem", store, null, geometry);
		}

		private static FdbLiteStore OpenFile(string path, FdbLiteGeometry geometry)
		{
			var store = FdbLiteStore.OpenOrCreateFile(path, geometry);
			// the file store opens with the wall-clock driven Adaptive policy: pinned Off, like the raw driver
			store.Engine.PreCommitConsolidation = FdbLitePreCommitConsolidation.Off;
			return store;
		}

		public void BeginBatch()
		{
			Contract.Requires(this.Tr is null, "a batch is already open");
			this.Tr = this.Db.BeginTransaction(FdbTransactionMode.Default, this.Lifetime.Token);
		}

		private IFdbTransaction Current => this.Tr ?? throw new InvalidOperationException("no batch is open");

		public void Set(Slice key, Slice value) => this.Current.Set(key, value);

		public void Clear(Slice key) => this.Current.Clear(key);

		public void ClearRange(Slice begin, Slice end) => this.Current.ClearRange(begin, end);

		public void Commit()
		{
			var tr = this.Current;
			try
			{
				tr.CommitAsync().GetAwaiter().GetResult();
			}
			finally
			{
				tr.Dispose();
				this.Tr = null;
			}
		}

		public int Probe(Slice key)
		{
			using var tr = this.Db.BeginTransaction(FdbTransactionMode.ReadOnly, this.Lifetime.Token);
			var value = tr.GetAsync(key).GetAwaiter().GetResult();
			return value.IsNull ? -1 : value.Count;
		}

		/// <summary>End of the user key space, standing in for a Nil end bound.</summary>
		private static readonly Slice UserSpaceEnd = Slice.FromByteString("\xFF");

		public List<KeyValuePair<Slice, Slice>> Scan(Slice begin, Slice end, int limit)
		{
			using var tr = this.Db.BeginTransaction(FdbTransactionMode.ReadOnly, this.Lifetime.Token);
			var range = tr.GetRange(KeySelector.FirstGreaterOrEqual(begin), KeySelector.FirstGreaterOrEqual(end.IsNull ? UserSpaceEnd : end), new FdbRangeOptions { Limit = limit > 0 ? limit : null });
			var result = new List<KeyValuePair<Slice, Slice>>();
			foreach (var kv in range.ToListAsync().GetAwaiter().GetResult())
			{
				result.Add(new(kv.Key, kv.Value));
			}
			return result;
		}

		public int ScanCount(Slice begin, Slice end, int limit, bool reverse, bool readValues)
		{
			using var tr = this.Db.BeginTransaction(FdbTransactionMode.ReadOnly, this.Lifetime.Token);
			var options = new FdbRangeOptions { Limit = limit > 0 ? limit : null, IsReversed = reverse };
			var chunk = tr.GetRange(KeySelector.FirstGreaterOrEqual(begin), KeySelector.FirstGreaterOrEqual(end.IsNull ? UserSpaceEnd : end), options).ToListAsync().GetAwaiter().GetResult();
			int n = 0;
			foreach (var kv in chunk)
			{
				if (readValues) { this.Sink += kv.Value.Count; }
				++n;
			}
			return n;
		}

		public IEnumerable<KeyValuePair<string, long>> Stats()
		{
			yield return new("sink", this.Sink);
			if (this.Store is FdbLiteStore lite)
			{
				var t = lite.Engine.MeasureTreeStatistics();
				yield return new("keyCount", (long) lite.Engine.Durable.KeyCount);
				yield return new("leafPages", t.LeafPages);
				yield return new("internalPages", t.InternalPages);
			}
		}

		public long FileSize => this.Path is { } p && File.Exists(p) ? new FileInfo(p).Length : 0;

		public IReadOnlyList<string>? Audit()
		{
			if (this.Store is not FdbLiteStore lite) return null;
			var pin = lite.Engine.BeginRead();
			try
			{
				return FdbLiteTreeAudit.Check(lite.Engine.Pager, pin.RootPageId);
			}
			finally
			{
				lite.Engine.EndRead(in pin);
			}
		}

		public bool Reopen()
		{
			if (this.Path is null) return false;
			Contract.Requires(this.Tr is null, "a batch is still open");
			this.Db.Dispose();
			this.Store.Dispose();
			this.Store = OpenFile(this.Path, this.Geometry);
			this.Db = this.Store.OpenDatabase(FdbPath.Root, readOnly: false, ownsStore: false);
			return true;
		}

		public void Dispose()
		{
			this.Tr?.Dispose();
			this.Lifetime.Cancel();
			this.Lifetime.Dispose();
			this.Db.Dispose();
			this.Store.Dispose();
			if (this.Path is { } p) { try { File.Delete(p); } catch { /* best effort */ } }
		}

	}

}
