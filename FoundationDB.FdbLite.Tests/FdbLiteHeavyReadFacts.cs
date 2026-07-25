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
	using System.Diagnostics;

	/// <summary>Point reads over a FILE-BACKED store whose working set dwarfs CPU cache: the measurement the three-region page layout exists to justify.</summary>
	/// <remarks>
	/// <para>An earlier attempt reported "no difference" and the harness was the reason, not the layout: a heap pager, a working set that fitted in cache, and no prefix stripped, so all three payoffs were switched off. **A measurement of a mechanism that never ran reports no benefit and kills a good design.** This one therefore ASSERTS ITS OWN PRECONDITIONS and prints what it actually measured, so a leg that silently degenerates is visible in its own output rather than in the conclusion.</para>
	/// <para>What it can and cannot show: the store is on disk and far larger than any CPU cache, so every probe pays real memory traffic and page locality matters. It is NOT larger than the OS page cache on a 63 GiB machine, which would need tens of GB per leg. So this measures memory-hierarchy locality, not page-fault locality; the disk-bound case remains unmeasured and should be labelled as such.</para>
	/// </remarks>
	[TestFixture]
	public class FdbLiteHeavyReadFacts : SimpleTest
	{

		/// <summary>Index-shaped key: a long shared run then a counter, which is what a directory-scoped index looks like to the engine and what prefix compression has to bite on.</summary>
		private const int KeySize = 36;

		private static void WriteKey(Span<byte> key, int i)
		{
			"\xFE/acme/tenant-0042/orders/idx/"u8.CopyTo(key);
			BinaryPrimitives.WriteInt32BigEndian(key[32..], i);
		}

		private static int Count => int.TryParse(Environment.GetEnvironmentVariable("FDBLITE_HEAVY_KEYS"), out var n) ? n : 2_000_000;

		private static int ValueSize => int.TryParse(Environment.GetEnvironmentVariable("FDBLITE_HEAVY_VALUE"), out var n) ? n : 1024;

		[Test]
		[Explicit("heavy: builds a multi-GB file-backed store; run only inside a granted measurement window")]
		public void Heavy_Read_Point_Lookups()
		{
			int count = Count, valueSize = ValueSize;
			const int PROBES = 200_000;

			var path = Path.Combine(Path.GetTempPath(), $"fdblite-heavy-{Guid.NewGuid():N}.dat");
			try
			{
				var sw = Stopwatch.StartNew();
				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, FdbLiteGeometry.Default))
				{
					var key = new byte[KeySize];
					var value = new byte[valueSize];
					const int BATCH = 100_000;
					ulong v = 1;
					for (int start = 0; start < count; start += BATCH)
					{
						var w = engine.BeginWrite();
						for (int i = start, end = Math.Min(start + BATCH, count); i < end; i++)
						{
							WriteKey(key, i);
							w.Insert(key, value);
						}
						engine.Commit(w, v++);
					}
				}
				sw.Stop();

				long bytes = new FileInfo(path).Length;
				Log($"# BUILD  {count:N0} pairs x {valueSize} B -> {bytes / (1024.0 * 1024 * 1024):N2} GiB on disk in {sw.Elapsed.TotalSeconds:N0}s");

				// PRECONDITION: the data really is on disk and really is large. A leg that quietly built nothing
				// would otherwise sail through and report a fast, meaningless number.
				Assert.That(bytes, Is.GreaterThan((long) count * valueSize / 2), "the file must actually hold the payload");

				using (var engine = FdbLiteEngine.OpenOrCreateFile(path, FdbLiteGeometry.Default))
				{
					var pin = engine.BeginRead();
					try
					{
						Assert.That(pin.KeyCount, Is.EqualTo((ulong) count), "the reopened store must hold every key");

						// report what this leg ACTUALLY has, so "which mechanism was on" is in the output and not
						// inferred later from the commit hash
						var geometry = engine.Pager.Geometry;
						int leaves = 0, stripped = 0, longest = 0;
						for (uint id = 1; id + (uint) geometry.BlocksPerPage <= engine.Pager.BlockCount; id += (uint) geometry.BlocksPerPage)
						{
							var page = engine.Pager.ReadBlocks(id, geometry.BlocksPerPage);
							if (FdbLitePageHeader.GetPageType(page) != FdbLitePageType.Leaf) continue;
							++leaves;
							int p = FdbLitePageHeader.GetPrefixLength(page);
							if (p > 0) { ++stripped; longest = Math.Max(longest, p); }
						}
						Log($"# LAYOUT leaves={leaves:N0} strippedLeaves={stripped:N0} longestPrefix={longest}B pageSize={geometry.PageSize}");

						Log($"# SEEK   {Measure(engine, pin, count, PROBES, expose: false):N0} ns/op   (compression scheme only: no key handed out)");
						Log($"# LOOKUP {Measure(engine, pin, count, PROBES, expose: true):N0} ns/op   (adds the exposure mechanism)");
					}
					finally
					{
						engine.EndRead(in pin);
					}
				}
			}
			finally
			{
				try { File.Delete(path); } catch { /* best effort */ }
			}
		}

		private static double Measure(FdbLiteEngine engine, in FdbLiteEngine.ReadSnapshot pin, int count, int probes, bool expose)
		{
			var cursor = new FdbLiteTreeCursor(engine.Pager, pin.RootPageId);
			var key = new byte[KeySize];
			var probe = new byte[KeySize];

			for (int i = 0; i < 5_000; i++) { WriteKey(key, (int) ((long) i * 1_299_709 % count)); cursor.SeekFloor(key, orEqual: true); }

			long hits = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < probes; i++)
			{
				// a large prime stride, so consecutive probes land in unrelated pages rather than walking one
				int k = (int) ((long) i * 1_299_709 % count);
				WriteKey(key, k);
				if (!cursor.SeekFloor(key, orEqual: true)) continue;
				if (expose)
				{
					// compare the WHOLE key: on a stripped page this forces the assembly, which is the cost the
					// exposure mechanism adds. Checking only the length would not.
					WriteKey(probe, k);
					if (cursor.CurrentKey.SequenceEqual(probe)) { ++hits; }
				}
				else
				{
					++hits;
				}
			}
			sw.Stop();

			// PRECONDITION: every probed key exists, so a leg that silently missed would be caught here rather
			// than reporting a suspiciously fast number
			Assert.That(hits, Is.EqualTo(probes), "every probe must land on its key");
			return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / probes;
		}

	}

}
