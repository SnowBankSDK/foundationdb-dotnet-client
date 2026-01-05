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

namespace SnowBank.Buffers.Tests
{
	using System.Collections.Immutable;
	using System.Runtime.InteropServices;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class BufferFacts : SimpleTest
	{

		[Test]
		public void Test_Buffer_Single_Segment()
		{
			// as long as we don't add more than the initial capacity, it should find in a single chunk
			var buffer = new Buffer<int>(16);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.IsSingleSegment, Is.True);

			buffer.Add(1);
			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.IsSingleSegment, Is.True);

			buffer.Add(2);
			Assert.That(buffer.Count, Is.EqualTo(2));
			Assert.That(buffer.IsSingleSegment, Is.True);

			buffer.Add(3);
			Assert.That(buffer.Count, Is.EqualTo(3));
			Assert.That(buffer.IsSingleSegment, Is.True);

			Assert.That(buffer[0], Is.EqualTo(1));
			Assert.That(buffer[1], Is.EqualTo(2));
			Assert.That(buffer[2], Is.EqualTo(3));
			
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(buffer.ToMemory().Span.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(buffer.ToList(), Is.EqualTo((List<int>) [ 1, 2, 3 ]));
			Assert.That(buffer.ToImmutableArray(), Is.EqualTo((ImmutableArray<int>) [ 1, 2, 3 ]));
			Assert.That(buffer.ToHashSet(), Is.EqualTo((HashSet<int>) [ 1, 2, 3 ]));
			Assert.That(buffer.ToDictionary(x => x, x => x.ToString()), Is.EqualTo(new Dictionary<int, string> { [1] = "1", [2] = "2", [3] = "3" }));
			Assert.That(buffer.ToDictionary(x => x.ToString()), Is.EqualTo(new Dictionary<string, int> { ["1"] = 1, ["2"] = 2, ["3"] = 3 }));

			// get single segment as mutable span
			Assert.That(buffer.TryGetSpan(out var items), Is.True);
			Assert.That(items.Length, Is.EqualTo(3));
			Assert.That(items[0], Is.EqualTo(1));
			Assert.That(items[1], Is.EqualTo(2));
			Assert.That(items[2], Is.EqualTo(3));

			// we can mutate the span in place
			items[1] = -2;
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, -2, 3 ]));

			// we can get a reference and change it also
			ref int r = ref buffer.GetReference(1);
			Assert.That(r, Is.EqualTo(-2));
			r = 2;
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			using (var iter = buffer.EnumerateSegments().GetEnumerator())
			{
				Assert.That(iter.MoveNext(), Is.True);
				Assert.That(iter.Current.Span.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3 ]));
				Assert.That(iter.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Buffer_Multiple_Segments()
		{
			// as long as we don't add more than the initial capacity, it should find in a single chunk
			var buffer = new Buffer<int>(16);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.IsSingleSegment, Is.True);

			// add up to the initial capacity
			for (int i = 0; i < 16; i++)
			{
				buffer.Add(i);
				Assert.That(buffer.Count, Is.EqualTo(i + 1));
				Assert.That(buffer.IsSingleSegment, Is.True);
				Assert.That(buffer.TryGetSpan(out var span), Is.True);
				Assert.That(span.Length, Is.EqualTo(i + 1));
			}

			// adding more items should trigger a chunk shift
			// add up to the initial capacity
			for (int i = 16; i < 32; i++)
			{
				buffer.Add(i);
				Assert.That(buffer.Count, Is.EqualTo(i + 1));
				Assert.That(buffer.IsSingleSegment, Is.False);
				Assert.That(buffer.TryGetSpan(out var span), Is.False);
				Assert.That(span.Length, Is.Zero);
			}

			var expected = Enumerable.Range(0, 32).ToArray();

			Assert.That(buffer.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToMemory().Span.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToList(), Is.EqualTo((List<int>) [ ..expected ]));
			Assert.That(buffer.ToImmutableArray(), Is.EqualTo((ImmutableArray<int>) [ ..expected ]));
			Assert.That(buffer.ToHashSet(), Is.EqualTo((HashSet<int>) [ ..expected ]));
			Assert.That(buffer.ToDictionary(x => x, x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x, x => x.ToString())));
			Assert.That(buffer.ToDictionary(x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x.ToString())));

			// we can get a reference and change it also
			ref int r1 = ref buffer.GetReference(1); // in first chunk
			Assert.That(r1, Is.EqualTo(1));
			ref int r2 = ref buffer.GetReference(24); // in second chunk
			Assert.That(r2, Is.EqualTo(24));

			// add a single item in the next chunk
			buffer.Add(32);
			Assert.That(buffer.Count, Is.EqualTo(33));
			Assert.That(buffer[32], Is.EqualTo(32));

			using (var iter = buffer.EnumerateSegments().GetEnumerator())
			{
				Assert.That(iter.MoveNext(), Is.True);
				Assert.That(iter.Current.Span.ToArray(), Is.EqualTo((int[]) [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 ]));
				Assert.That(iter.MoveNext(), Is.True);
				Assert.That(iter.Current.Span.ToArray(), Is.EqualTo((int[]) [ 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 ]));
				Assert.That(iter.MoveNext(), Is.True);
				Assert.That(iter.Current.Span.ToArray(), Is.EqualTo((int[]) [ 32 ]));
				Assert.That(iter.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Buffer_AddRange_Span()
		{
			var buffer = new Buffer<int>(16);

			buffer.AddRange([ 1, 2, 3 ]);
			Assert.That(buffer.Count, Is.EqualTo(3));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			buffer.AddRange([ 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 ]);
			Assert.That(buffer.Count, Is.EqualTo(14));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 ]));

			buffer.AddRange((int[]) [ 15, 16, 17, 18, 19, 20 ]);
			Assert.That(buffer.Count, Is.EqualTo(20));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]));


			buffer.AddRange(Enumerable.Range(21, 80).ToArray());
			Assert.That(buffer.Count, Is.EqualTo(100));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 100)));

			var expected = Enumerable.Range(1, 100).ToArray();
			Assert.That(buffer.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToMemory().Span.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToList(), Is.EqualTo((List<int>) [ ..expected ]));
			Assert.That(buffer.ToImmutableArray(), Is.EqualTo((ImmutableArray<int>) [ ..expected ]));
			Assert.That(buffer.ToHashSet(), Is.EqualTo((HashSet<int>) [ ..expected ]));
			Assert.That(buffer.ToDictionary(x => x, x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x, x => x.ToString())));
			Assert.That(buffer.ToDictionary(x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x.ToString())));
		}

		[Test]
		public void Test_Buffer_AddRange_Enumerable_Known_Size()
		{
			var buffer = new Buffer<int>(16);

			buffer.AddRange(Enumerable.Range(1, 3));
			Assert.That(buffer.Count, Is.EqualTo(3));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 3)));

			buffer.AddRange(Enumerable.Range(4, 11));
			Assert.That(buffer.Count, Is.EqualTo(14));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 14)));

			buffer.AddRange(Enumerable.Range(15, 6));
			Assert.That(buffer.Count, Is.EqualTo(20));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 20)));

			buffer.AddRange(Enumerable.Range(21, 80));
			Assert.That(buffer.Count, Is.EqualTo(100));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 100)));

			var expected = Enumerable.Range(1, 100).ToArray();
			Assert.That(buffer.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToMemory().Span.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToList(), Is.EqualTo((List<int>) [ ..expected ]));
			Assert.That(buffer.ToImmutableArray(), Is.EqualTo((ImmutableArray<int>) [ ..expected ]));
			Assert.That(buffer.ToHashSet(), Is.EqualTo((HashSet<int>) [ ..expected ]));
			Assert.That(buffer.ToDictionary(x => x, x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x, x => x.ToString())));
			Assert.That(buffer.ToDictionary(x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x.ToString())));
		}

		[Test]
		public void Test_Buffer_AddRange_Enumerable_Unknown_Size()
		{
			var buffer = new Buffer<int>(16);

			static IEnumerable<int> RangeObfuscated(int start, int count)
			{
				for (int i = 0; i < count; i++)
				{
					yield return start + i;
				}
			}

			buffer.AddRange(RangeObfuscated(1, 3));
			Assert.That(buffer.Count, Is.EqualTo(3));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 3)));

			buffer.AddRange(RangeObfuscated(4, 11));
			Assert.That(buffer.Count, Is.EqualTo(14));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 14)));

			buffer.AddRange(RangeObfuscated(15, 6));
			Assert.That(buffer.Count, Is.EqualTo(20));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 20)));

			buffer.AddRange(RangeObfuscated(21, 80));
			Assert.That(buffer.Count, Is.EqualTo(100));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 100)));

			var expected = Enumerable.Range(1, 100).ToArray();
			Assert.That(buffer.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToMemory().Span.ToArray(), Is.EqualTo(expected));
			Assert.That(buffer.ToList(), Is.EqualTo((List<int>) [ ..expected ]));
			Assert.That(buffer.ToImmutableArray(), Is.EqualTo((ImmutableArray<int>) [ ..expected ]));
			Assert.That(buffer.ToHashSet(), Is.EqualTo((HashSet<int>) [ ..expected ]));
			Assert.That(buffer.ToDictionary(x => x, x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x, x => x.ToString())));
			Assert.That(buffer.ToDictionary(x => x.ToString()), Is.EqualTo(expected.ToDictionary(x => x.ToString())));
		}

		[Test]
		public void Test_Buffer_IBufferWriter()
		{
			var buffer = new Buffer<int>(16);

			var span = buffer.GetSpan();
			Assert.That(span.Length, Is.EqualTo(16));
			span[0] = 1;
			buffer.Advance(1);
			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 1)));

			span = buffer.GetSpan();
			Assert.That(span.Length, Is.EqualTo(15));
			span[0] = 2;
			span[1] = 3;
			buffer.Advance(2);
			Assert.That(buffer.Count, Is.EqualTo(3));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 3)));

			// request exactly what remains
			span = buffer.GetSpan(13);
			Assert.That(span.Length, Is.GreaterThanOrEqualTo(13));
			for (int i = 0; i < 13; i++)
			{
				span[i] = 4 + i;
			}
			buffer.Advance(13);
			Assert.That(buffer.Count, Is.EqualTo(16));
			Assert.That(buffer.IsSingleSegment, Is.True);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 16)));

			// ask for more, but without specifying a size
			span = buffer.GetSpan();
			Assert.That(span.Length, Is.EqualTo(32));
			Assert.That(buffer.Count, Is.EqualTo(16));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 16)));

			// fill half of the buffer
			for (int i = 0; i < 16; i++)
			{
				span[i] = 17 + i;
			}
			buffer.Advance(16);
			Assert.That(buffer.Count, Is.EqualTo(32));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 32)));

			// request too much
			span = buffer.GetSpan(100);
			Assert.That(span.Length, Is.GreaterThanOrEqualTo(100));
			for (int i = 0; i < 100; i++)
			{
				span[i] = 33 + i;
			}
			buffer.Advance(100);
			Assert.That(buffer.Count, Is.EqualTo(132));
			Assert.That(buffer.IsSingleSegment, Is.False);
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 132)));
		}

		[Test]
		public void Test_Buffer_Stuffing()
		{
			var rnd = CreateRandomizer();

			var buffer = new Buffer<int>();
			int remaining = 1_000;
			int p = 0;
			while(remaining > 0)
			{
				if (rnd.NextDouble() < 0.5)
				{
					buffer.Add(p++);
					remaining--;
				}
				else
				{
					int n = rnd.Next(1, Math.Min(remaining, 17));
					switch (rnd.NextDouble())
					{
						case < 0.33:
						{
							buffer.AddRange(Enumerable.Range(p, n).ToArray());
							break;
						}
						case < 0.67:
						{
							buffer.AddRange(Enumerable.Range(p, n));
							break;
						}
						default:
						{
							var span = buffer.GetSpan(n);
							for (int i = 0; i < n; i++)
							{
								span[i] = p + i;
							}
							buffer.Advance(n);
							break;
						}
					}

					p += n;
					remaining -= n;
				}
				Assert.That(buffer.Count, Is.EqualTo(p));
			}

			Log($"Count: {buffer.Count}");
			Assert.That(buffer.Count, Is.EqualTo(1_000));
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(0, 1_000)));

			int sum = 0;
			foreach (var chunk in buffer.EnumerateSegments())
			{
				Log($"Chunk: {chunk.Length}");
				Assert.That(chunk.ToArray(), Is.EqualTo(Enumerable.Range(sum, chunk.Length)));
				sum += chunk.Length;
			}
			Assert.That(sum, Is.EqualTo(1_000));

		}

		[Test]
		public void Test_Buffer_Aggregate()
		{
			var buffer = new Buffer<int>();
			for (int i = 0; i < 100; i++)
			{
				buffer.Add(i);
			}

			Assert.That(buffer.AggregateSegments(0, static (sum, chunk) => sum + chunk.Span.Length), Is.EqualTo(100));
			Assert.That(buffer.Aggregate(0, static (sum, item) => sum + item), Is.EqualTo(4950));
		}

		[Test]
		public void Test_Buffer_ForEach()
		{
			var buffer = new Buffer<int>();
			for (int i = 0; i < 100; i++)
			{
				buffer.Add(i);
			}

			var list = new List<int>();
#if NET8_0_OR_GREATER
			buffer.ForEachSegments(list, static (l, chunk) => l.AddRange(chunk.Span));
#else
			buffer.ForEachSegments(list, static (l, chunk) => l.AddRange(chunk.Span.ToArray()));
#endif
			Assert.That(list, Is.EqualTo(Enumerable.Range(0, 100)));

			list.Clear();
			buffer.ForEach(list, static (l, item) => l.Add(item));
			Assert.That(list, Is.EqualTo(Enumerable.Range(0, 100)));
		}

#if NET9_0_OR_GREATER

		[Test]
		public void Test_Buffer_ForReach_Ref()
		{
			// check that we can iterate a buffer by passing a struct by ref as the state, like a SliceWriter

			var buffer = new Buffer<int>();
			for (int i = 0; i < 100; i++)
			{
				buffer.Add(i);
			}

			{ // segments
				var writer = new SliceWriter();
				buffer.ForEachSegments(
					ref writer,
					static (ref SliceWriter sw, ReadOnlyMemory<int> chunk) => sw.WriteBytes(MemoryMarshal.AsBytes(chunk.Span))
				);
				DumpHexa(writer.ToSlice());
				Assert.That(writer.Position, Is.EqualTo(400));

				var sr = new SliceReader(writer.ToSlice());
				for (int i = 0; i < 100; i++)
				{
					Assert.That(sr.ReadInt32(), Is.EqualTo(i));
				}
				Assert.That(sr.HasMore, Is.False);
			}

			{ // items
				var writer = new SliceWriter();
				buffer.ForEach(
					ref writer,
					static (ref SliceWriter sw, int item) => sw.WriteInt32BE(item)
				);
				DumpHexa(writer.ToSlice());
				Assert.That(writer.Position, Is.EqualTo(400));

				var sr = new SliceReader(writer.ToSlice());
				for (int i = 0; i < 100; i++)
				{
					Assert.That(sr.ReadInt32BE(), Is.EqualTo(i));
				}
				Assert.That(sr.HasMore, Is.False);
			}

		}

		[Test]
		public void Test_Buffer_ForReach_Ref_Struct()
		{
			// check that we can iterate a buffer by passing a ref struct by ref as the state, like a SpanWriter

			var buffer = new Buffer<int>();
			for (int i = 0; i < 100; i++)
			{
				buffer.Add(i);
			}

			{ // segments
				var writer = new SpanWriter(new byte[1000]);

				buffer.ForEachSegments(
					ref writer,
					static (ref SpanWriter sw, ReadOnlyMemory<int> chunk) =>
					{
						sw.WriteBytes(MemoryMarshal.AsBytes(chunk.Span));
					}
				);
				DumpHexa(writer.ToSpan());
				Assert.That(writer.Position, Is.EqualTo(400));

				var sr = new SpanReader(writer.ToSpan());
				for (int i = 0; i < 100; i++)
				{
					Assert.That(sr.ReadInt32(), Is.EqualTo(i));
				}
				Assert.That(sr.HasMore, Is.False);
			}

			{ // items
				var writer = new SpanWriter(new byte[1000]);

				buffer.ForEach(
					ref writer,
					static (ref SpanWriter sw, int item) => sw.WriteInt32BE(item)
				);
				DumpHexa(writer.ToSpan());
				Assert.That(writer.Position, Is.EqualTo(400));

				var sr = new SpanReader(writer.ToSpan());
				for (int i = 0; i < 100; i++)
				{
					Assert.That(sr.ReadInt32BE(), Is.EqualTo(i));
				}
				Assert.That(sr.HasMore, Is.False);
			}

		}

#endif

	}
}
