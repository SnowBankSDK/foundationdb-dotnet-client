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

namespace SnowBank.Analyzers.Tests
{

	[TestFixture]
	public class PooledBufferFacts
	{

		private const string Usings = """
			using System;
			using System.Buffers;
			using System.Collections.Generic;
			using System.Threading.Tasks;
			using SnowBank.Buffers;

			""";

		private static DiagnosticResult Escape(int location, string expression) => Verify.Diagnostic(SbkDescriptors.PooledBufferEscape).WithLocation(location).WithArguments(expression);

		private static DiagnosticResult Leak(int location, string name) => Verify.Diagnostic(SbkDescriptors.PooledBufferNeverReturned).WithLocation(location).WithArguments(name);

		#region SBK1001...

		[Test]
		public Task Reports_A_Returned_View() => Verify.Analyzer<PooledBufferAnalyzer>(Usings + """
			class C
			{
				Slice M(byte[] payload)
				{
					using var owner = Slice.FromBytes(payload, ArrayPool<byte>.Shared);
					return {|#0:owner.Data|};
				}
				Slice N()
				{
					using (var w = new SliceWriter(ArrayPool<byte>.Shared))
					{
						w.WriteInt32(1);
						return {|#1:w.ToSlice()|};
					}
				}
				ReadOnlySpan<byte> O(byte[] payload)
				{
					using var owner = SliceOwner.Copy(payload, ArrayPool<byte>.Shared);
					return {|#2:owner.Span|};
				}
			}
			""", Escape(0, "owner.Data"), Escape(1, "w.ToSlice()"), Escape(2, "owner.Span"));

		[Test]
		public Task Reports_Stores_Adds_And_Captures() => Verify.Analyzer<PooledBufferAnalyzer>(Usings + """
			class C
			{
				Slice field;
				Slice Property { get; set; }
				void M(List<Slice> list, PooledSliceAllocator alloc)
				{
					using var w = new PooledSliceWriter(ArrayPool<byte>.Shared);
					this.field = {|#0:w.WrittenSlice|};
					this.Property = {|#1:w.GetSlice(4).AsSlice()|};
					list.Add({|#2:w.WrittenSlice|});
					using var a = new PooledSliceAllocator(ArrayPool<byte>.Shared);
					var chunk = a.Allocate(16);
					list.Add({|#3:chunk.AsSlice()|});
					Task.Run(() => Console.WriteLine({|#4:w.WrittenSlice|}));
				}
			}
			""", Escape(0, "w.WrittenSlice"), Escape(1, "w.GetSlice(4).AsSlice()"), Escape(2, "w.WrittenSlice"), Escape(3, "chunk.AsSlice()"), Escape(4, "w.WrittenSlice"));

		[Test]
		public Task Ignores_Copies_And_Handed_Off_Owners() => Verify.Analyzer<PooledBufferAnalyzer>(Usings + """
			class C
			{
				Slice field;
				Slice M(byte[] payload)
				{
					using var owner = Slice.FromBytes(payload, ArrayPool<byte>.Shared);
					this.field = Slice.Copy(owner.Span);
					return owner.Data.ToArray().AsSlice();
				}
				SliceOwner N()
				{
					using var w = new SliceWriter(ArrayPool<byte>.Shared);
					w.WriteInt32(1);
					return w.ToSliceOwner();
				}
				Slice O()
				{
					using var w = new SliceWriter(64);
					w.WriteInt32(1);
					return w.ToSlice();
				}
				Slice P(SliceOwner owner)
				{
					return owner.Data;
				}
				int Q(byte[] payload)
				{
					using var owner = Slice.FromBytes(payload, ArrayPool<byte>.Shared);
					return owner.Data.Count;
				}
			}
			""");

		#endregion

		#region SBK2001...

		[Test]
		public Task Reports_A_Writer_Never_Disposed() => Verify.Analyzer<PooledBufferAnalyzer>(Usings + """
			class C
			{
				void Send(byte[] bytes) { }
				void M()
				{
					var {|#0:w = new SliceWriter(ArrayPool<byte>.Shared)|};
					w.WriteInt32(1);
					Send(w.GetBytes());
				}
			}
			""", Leak(0, "w"));

		[Test]
		public Task Reports_Each_Pooled_Creation() => Verify.Analyzer<PooledBufferAnalyzer>(Usings + """
			class C
			{
				void M(byte[] payload, SliceWriter source)
				{
					var {|#0:a = new SliceWriter(128, ArrayPool<byte>.Shared)|};
					var {|#1:b = new PooledSliceWriter()|};
					var {|#2:c = new PooledSliceAllocator(ArrayPool<byte>.Shared)|};
					var {|#3:d = SliceOwner.Create(payload.AsSlice(), ArrayPool<byte>.Shared)|};
					var {|#4:e = SliceOwner.Copy(payload, ArrayPool<byte>.Shared)|};
					var {|#5:f = Slice.FromBytes(payload, ArrayPool<byte>.Shared)|};
					var {|#6:g = source.ToSliceOwner()|};
					Console.WriteLine(a.Position + b.WrittenCount + c.TotalAllocated + d.Count + e.Count + f.Count + g.Count);
				}
			}
			""", Leak(0, "a"), Leak(1, "b"), Leak(2, "c"), Leak(3, "d"), Leak(4, "e"), Leak(5, "f"), Leak(6, "g"));

		[Test]
		public Task Ignores_Disposed_Escaping_And_Heap_Writers() => Verify.Analyzer<PooledBufferAnalyzer>(Usings + """
			class C
			{
				SliceWriter field;
				void Keep(SliceWriter w) { }
				void M(ArrayPool<byte> pool)
				{
					var a = new SliceWriter(pool);
					a.WriteInt32(1);
					a.Dispose();
					var b = new SliceWriter(pool);
					b.Release();
					var c = new SliceWriter(pool);
					using (c) { c.WriteInt32(1); }
					var d = new SliceWriter(pool);
					this.field = d;
					var e = new SliceWriter(pool);
					Keep(e);
					var f = new SliceWriter(pool);
					var alias = f;
					var g = new SliceWriter(pool);
					_ = g.ToSliceOwner();
					var h = new SliceWriter();
					var i = new SliceWriter(64);
					var j = new SliceWriter(pool: null);
					var k = SliceOwner.Wrap(Slice.Empty);
					using var l = new SliceWriter(pool);
				}
				SliceWriter N(ArrayPool<byte> pool)
				{
					var w = new SliceWriter(pool);
					return w;
				}
			}
			""");

		[Test]
		public Task Fixes_By_Adding_Using() => Verify.CodeFix<PooledBufferAnalyzer, PooledBufferCodeFix>(Usings + """
			class C
			{
				void Send(byte[] bytes) { }
				void M()
				{
					// build the payload
					var {|#0:w = new SliceWriter(ArrayPool<byte>.Shared)|};
					w.WriteInt32(1);
					Send(w.GetBytes());
				}
			}
			""", Usings + """
			class C
			{
				void Send(byte[] bytes) { }
				void M()
				{
					// build the payload
					using var w = new SliceWriter(ArrayPool<byte>.Shared);
					w.WriteInt32(1);
					Send(w.GetBytes());
				}
			}
			""", Leak(0, "w"));

		#endregion

	}
}
