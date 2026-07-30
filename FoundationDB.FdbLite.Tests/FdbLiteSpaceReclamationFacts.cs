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
	using FoundationDB.Storage.FdbLite;

	/// <summary>Tests of the space-reclamation train: the aggregate block, the volatility counter, pre-commit consolidation, and the background vacuum.</summary>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteSpaceReclamationFacts : SimpleTest
	{

		private static FdbLiteEngine CreateHeapEngine() => FdbLiteEngine.Create(new FdbLiteHeapPager(FdbLiteGeometry.Default));

		private static byte[] Key(int i) => System.Text.Encoding.ASCII.GetBytes($"key-{i:D6}");

		private static byte[] Value(int i, int length)
		{
			var v = new byte[length];
			new Random(i).NextBytes(v);
			return v;
		}

		/// <summary>Reads a committed page image through the engine's pager.</summary>
		private static ReadOnlySpan<byte> ReadPage(FdbLiteEngine engine, uint pageId)
			=> engine.Pager.ReadBlocks(pageId, engine.Pager.Geometry.BlocksPerPage);

		[Test]
		public void Test_Seal_Restamps_The_Generation_Of_Verbatim_Copies()
		{
			// The copy-verbatim replace path duplicates a committed page image and mutates one value in the
			// copy, so the copy carries the SOURCE generation's stamp. The stamp is diagnostic (an inspector
			// uses it to detect a page reused under its feet), and a page published by generation N stamped
			// N-1 sends any such diagnosis to the wrong generation. Seal is the one point every dirty image
			// passes through exactly once, so the stamp is corrected there.
			using var engine = CreateHeapEngine();

			var writer = engine.BeginWrite();
			for (int i = 0; i < 3; i++)
			{
				writer.Insert(Key(i), Value(i, 32));
			}
			engine.Commit(writer, databaseVersion: 1);

			// same-length replace of a committed value: the first touch of the page takes the copy-verbatim path
			writer = engine.BeginWrite();
			ulong writeGeneration = writer.Generation;
			writer.Insert(Key(1), Value(1000, 32));
			Assert.That(writer.CellsOverwritten, Is.EqualTo(1), "the replace must take the in-place overwrite path (copy-verbatim on first touch), or this test no longer exercises the stamp");
			engine.Commit(writer, databaseVersion: 2);

			var leaf = ReadPage(engine, engine.Durable.RootPageId);
			Assert.That(FdbLitePageHeader.GetPageType(leaf), Is.EqualTo(FdbLitePageType.Leaf), "a 3-key store is a single-leaf tree");
			Assert.That(FdbLitePageHeader.GetGeneration(leaf), Is.EqualTo(writeGeneration), "a page published by a generation must carry that generation's stamp");
		}

	}

}
