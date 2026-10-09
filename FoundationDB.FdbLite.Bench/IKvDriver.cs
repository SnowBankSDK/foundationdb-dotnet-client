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

	/// <summary>What a store has to do to replay a <see cref="Workload"/>.</summary>
	/// <remarks>
	/// <para>Deliberately narrow: single-key and range writes grouped in batches, point reads and ordered scans against the committed state. Nothing here needs a transaction layer, so the same log runs on the raw engine, on the emulator over it, and on the reference model.</para>
	/// <para>The optional members (<see cref="Audit"/>, <see cref="Reopen"/>) are the extra checks a persistent engine can offer to the verify mode. The defaults say "nothing to check".</para>
	/// </remarks>
	public interface IKvDriver : IDisposable
	{

		/// <summary>Name under which the results are reported (<c>fdblite</c>, <c>fakedb</c>, ...)</summary>
		string Engine { get; }

		/// <summary>Called between the untimed prepare phase and the timed section, so a driver can reset per-open state that a workload variant measures again (the <c>.verify</c> scan legs re-check page checksums on first touch).</summary>
		void OnTimedSectionStarting(Workload load)
		{
		}

		/// <summary>Opens a write batch. Exactly one is open at a time.</summary>
		void BeginBatch();

		void Set(Slice key, Slice value);

		void Clear(Slice key);

		void ClearRange(Slice begin, Slice end);

		/// <summary>Ends the batch and makes it durable.</summary>
		void Commit();

		/// <summary>Point read against the committed state. Returns the value length, or -1 when the key is absent.</summary>
		/// <remarks>A length rather than the bytes, so the timed read does not pay for a copy. Verification uses <see cref="Scan"/>.</remarks>
		int Probe(Slice key);

		/// <summary>Ordered forward scan returning the pairs visited, for verification. <paramref name="end"/> may be <see cref="Slice.Nil"/> (no upper bound); <paramref name="limit"/> 0 means no limit.</summary>
		List<KeyValuePair<Slice, Slice>> Scan(Slice begin, Slice end, int limit);

		/// <summary>Counted scan, for measurement: visits the same pairs as <see cref="Scan"/> without materializing them.</summary>
		/// <param name="reverse">Walk backwards from <paramref name="end"/> instead of forwards from <paramref name="begin"/>.</param>
		/// <param name="readValues">Touch each value. False stays inside the key area of each page.</param>
		int ScanCount(Slice begin, Slice end, int limit, bool reverse, bool readValues);

		/// <summary>Engine counters after a run, as label/value pairs.</summary>
		IEnumerable<KeyValuePair<string, long>> Stats();

		/// <summary>Bytes the store occupies on disk, 0 for an in-memory store.</summary>
		long FileSize { get; }

		/// <summary>Structural problems found in the committed tree: empty when the tree is sound, null when the engine has no audit.</summary>
		IReadOnlyList<string>? Audit() => null;

		/// <summary>Closes the store and opens it again from disk, so a later <see cref="Scan"/> reads what was made durable. Returns false when the store has no file.</summary>
		bool Reopen() => false;

	}

}
