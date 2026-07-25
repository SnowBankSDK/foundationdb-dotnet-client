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

	/// <summary>Insert throughput on INDEX-SHAPED data: many short keys, empty values.</summary>
	/// <remarks>
	/// <para>This is the shape the three-region layout costs most on, and the reason is worth stating because it is counter-intuitive: a page whose values are empty is ALL key heap, and the key region is the thing an in-place insert has to slide when the slot directory grows. The layout's nicest case for space is its worst case for insertion.</para>
	/// <para>Uses only store-level APIs on purpose, so the identical source compiles and runs on either side of the format change and an old-versus-new comparison is a real bracket rather than two measurements taken weeks apart.</para>
	/// </remarks>
	[TestFixture]
	public class FdbLiteIndexInsertFacts : SimpleTest
	{

		private static double MeasureNanosPerInsert(int count, int keySize, int valueSize)
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

			var key = new byte[keySize];
			var value = new byte[valueSize];

			// one untimed pass so allocation, JIT and page growth are not charged to the measurement
			var warm = engine.BeginWrite();
			for (int i = 0; i < 1_000; i++)
			{
				BinaryPrimitives.WriteInt32BigEndian(key, i);
				warm.Insert(key, value);
			}
			engine.Commit(warm, 1);

			var writer = engine.BeginWrite();
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < count; i++)
			{
				// big-endian so the keys ascend, which is what building an index actually does
				BinaryPrimitives.WriteInt32BigEndian(key, 1_000_000 + i);
				writer.Insert(key, value);
			}
			sw.Stop();
			engine.Commit(writer, 2);

			return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / count;
		}

		/// <summary>Point lookups over a populated tree, measured with the key EXPOSED or not.</summary>
		/// <remarks>
		/// <para>Compression scheme and exposure mechanism are independent, and conflating them would make a good compression scheme look bad on the strength of a placeholder exposure. So:</para>
		/// <para><paramref name="exposeKey"/> false measures the search alone, which is where a smaller searched region and a denser page pay off. True adds reading the whole key back, which on a prefix-stripped page has to assemble it. The DIFFERENCE between the two is the cost of the exposure mechanism, not of the layout.</para>
		/// </remarks>
		private static double MeasureNanosPerLookup(int count, int keySize, int valueSize, bool exposeKey = true)
		{
			using var engine = FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

			var key = new byte[keySize];
			var value = new byte[valueSize];
			var writer = engine.BeginWrite();
			for (int i = 0; i < count; i++)
			{
				BinaryPrimitives.WriteInt32BigEndian(key, i);
				writer.Insert(key, value);
			}
			engine.Commit(writer, 1);

			var pin = engine.BeginRead();
			try
			{
				var cursor = new FdbLiteTreeCursor(engine.Pager, pin.RootPageId);

				// untimed pass so the descent path is warm
				for (int i = 0; i < 1_000; i++)
				{
					BinaryPrimitives.WriteInt32BigEndian(key, i);
					cursor.SeekFloor(key, orEqual: true);
				}

				var sw = Stopwatch.StartNew();
				long hits = 0;
				for (int i = 0; i < count; i++)
				{
					// stride by a large prime so successive probes do not land in the page the last one warmed
					BinaryPrimitives.WriteInt32BigEndian(key, (int) ((long) i * 7919 % count));
					if (!cursor.SeekFloor(key, orEqual: true)) { continue; }
					// exposing the key is what forces an assembly on a stripped page; skipping it measures the
					// search alone
					if (!exposeKey || cursor.CurrentKey.SequenceEqual(key)) { ++hits; }
				}
				sw.Stop();
				Assert.That(hits, Is.EqualTo(count), "every key was inserted, so every lookup must hit");
				return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / count;
			}
			finally
			{
				engine.EndRead(in pin);
			}
		}

		[Test]
		[Explicit("FL-38 measurement: run only inside a granted quiet window, bracketed against the pre-change build")]
		public void FL38_Throughput_By_Shape()
		{
			const int COUNT = 200_000;

			// index shape first: it is the one the FL-38 ruling hangs on. NOTE: prefixLen is 0 throughout, so
			// these runs pay for the prefix machinery and receive none of its benefit; they bound the accessor
			// cost, they are not a verdict on the layout.
			foreach (var (label, keySize, valueSize) in new[]
			{
				("index    (16 B key, EMPTY value)", 16, 0),
				("small    (16 B key,   64 B value)", 16, 64),
				("document (16 B key, 1024 B value)", 16, 1024),
			})
			{
				double wa = MeasureNanosPerInsert(COUNT, keySize, valueSize);
				double wb = MeasureNanosPerInsert(COUNT, keySize, valueSize);
				Log($"# INSERT {label}: {Math.Min(wa, wb):N0} ns/op (best of 2: {wa:N0} / {wb:N0})");

				// SEEK measures the compression scheme: smaller searched region, denser pages, no key handed out
				double sa = MeasureNanosPerLookup(COUNT, keySize, valueSize, exposeKey: false);
				double sb = MeasureNanosPerLookup(COUNT, keySize, valueSize, exposeKey: false);
				Log($"# SEEK   {label}: {Math.Min(sa, sb):N0} ns/op (best of 2: {sa:N0} / {sb:N0})");

				// LOOKUP adds the exposure mechanism: reading the whole key back, which a stripped page assembles
				double ra = MeasureNanosPerLookup(COUNT, keySize, valueSize);
				double rb = MeasureNanosPerLookup(COUNT, keySize, valueSize);
				Log($"# LOOKUP {label}: {Math.Min(ra, rb):N0} ns/op (best of 2: {ra:N0} / {rb:N0})");
			}
		}

	}

}
