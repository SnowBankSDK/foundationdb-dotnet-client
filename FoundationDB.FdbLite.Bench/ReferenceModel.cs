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

	/// <summary>The oracle of the verify mode: a sorted in-memory map that replays the same log as the engines.</summary>
	/// <remarks>
	/// <para>A <see cref="SortedSet{T}"/> of keys next to a dictionary of values. The set gives ordered range views in O(log n), which is what the scan workloads need at the standard scale; the dictionary gives the O(1) point read.</para>
	/// <para>Batches are no-ops: the model has no notion of durability. Its content after a replay is, by construction, what every engine must hold.</para>
	/// </remarks>
	public sealed class ReferenceModel : IKvDriver
	{

		public string Engine => "model";

		private SortedSet<Slice> Keys { get; } = new(Slice.Comparer.Default);

		private Dictionary<Slice, Slice> Values { get; } = new(Slice.Comparer.Default);

		public void BeginBatch()
		{
		}

		public void Commit()
		{
		}

		public void Set(Slice key, Slice value)
		{
			this.Keys.Add(key);
			this.Values[key] = value;
		}

		public void Clear(Slice key)
		{
			if (this.Keys.Remove(key))
			{
				this.Values.Remove(key);
			}
		}

		public void ClearRange(Slice begin, Slice end)
		{
			var doomed = new List<Slice>();
			foreach (var key in Range(begin, end))
			{
				doomed.Add(key);
			}
			foreach (var key in doomed)
			{
				this.Keys.Remove(key);
				this.Values.Remove(key);
			}
		}

		public int Probe(Slice key) => this.Values.TryGetValue(key, out var value) ? value.Count : -1;

		/// <summary>Keys in <c>[begin, end)</c>, ascending. A Nil <paramref name="end"/> means no upper bound.</summary>
		private IEnumerable<Slice> Range(Slice begin, Slice end)
		{
			if (this.Keys.Count == 0) yield break;
			var upper = end.IsNull ? this.Keys.Max : end;
			if (Slice.Comparer.Default.Compare(begin, upper) > 0) yield break;
			// the view's upper bound is inclusive, and the log's end bound is exclusive
			foreach (var key in this.Keys.GetViewBetween(begin, upper))
			{
				if (!end.IsNull && key.Equals(end)) yield break;
				yield return key;
			}
		}

		public List<KeyValuePair<Slice, Slice>> Scan(Slice begin, Slice end, int limit)
		{
			var res = new List<KeyValuePair<Slice, Slice>>();
			foreach (var key in Range(begin, end))
			{
				res.Add(new(key, this.Values[key]));
				if (limit > 0 && res.Count >= limit) break;
			}
			return res;
		}

		public int ScanCount(Slice begin, Slice end, int limit, bool reverse, bool readValues)
		{
			int n = 0;
			if (!reverse)
			{
				foreach (var _ in Range(begin, end))
				{
					++n;
					if (limit > 0 && n >= limit) break;
				}
				return n;
			}
			if (this.Keys.Count == 0) return 0;
			var upper = end.IsNull ? this.Keys.Max : end;
			if (Slice.Comparer.Default.Compare(begin, upper) > 0) return 0;
			foreach (var key in this.Keys.GetViewBetween(begin, upper).Reverse())
			{
				if (!end.IsNull && key.Equals(end)) continue;
				++n;
				if (limit > 0 && n >= limit) break;
			}
			return n;
		}

		public IEnumerable<KeyValuePair<string, long>> Stats()
		{
			yield return new("keyCount", this.Keys.Count);
		}

		public long FileSize => 0;

		public void Dispose()
		{
		}

	}

}
