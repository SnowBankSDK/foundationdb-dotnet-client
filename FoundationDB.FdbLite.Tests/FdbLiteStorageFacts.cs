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
	using System.Numerics;
	using FoundationDB.Storage.FdbLite;

	/// <summary>Geometries every engine test should run against: the FL-17 benchmark-matrix candidates.</summary>
	public static class TestGeometries
	{

		/// <summary>The four FL-17 candidates: uniform 16/32/64 KiB and the 16 KiB-block/64 KiB-page split</summary>
		public static IEnumerable<FdbLiteGeometry> All
		{
			get
			{
				yield return FdbLiteGeometry.Uniform(14);
				yield return FdbLiteGeometry.Uniform(15);
				yield return FdbLiteGeometry.Uniform(16);
				yield return FdbLiteGeometry.Hypothesis;
			}
		}

	}

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteGeometryFacts : SimpleTest
	{

		[Test]
		public void Test_Default_Geometry_Is_The_Ruled_32K_Uniform()
		{
			// the FL-17 ruling: shipped default = 32 KiB uniform (geometry stays a per-store runtime knob)
			var geometry = FdbLiteGeometry.Default;
			Assert.That(geometry.BlockSize, Is.EqualTo(32 * 1024));
			Assert.That(geometry.PageSize, Is.EqualTo(32 * 1024));
			Assert.That(geometry.BlocksPerPage, Is.EqualTo(1));
		}

		[Test]
		public void Test_Geometry_Derived_Values()
		{
			var uniform16 = FdbLiteGeometry.Uniform(14);
			Assert.That(uniform16.BlockSize, Is.EqualTo(16 * 1024));
			Assert.That(uniform16.PageSize, Is.EqualTo(16 * 1024));
			Assert.That(uniform16.BlocksPerPage, Is.EqualTo(1));
			Assert.That(uniform16.MaxInlineValueLength, Is.EqualTo(4 * 1024));

			var split = FdbLiteGeometry.Hypothesis;
			Assert.That(split.BlockSize, Is.EqualTo(16 * 1024));
			Assert.That(split.PageSize, Is.EqualTo(64 * 1024));
			Assert.That(split.BlocksPerPage, Is.EqualTo(4));
			Assert.That(split.MaxInlineValueLength, Is.EqualTo(16 * 1024));

			var uniform64 = FdbLiteGeometry.Uniform(16);
			Assert.That(uniform64.BlockSize, Is.EqualTo(64 * 1024));
			Assert.That(uniform64.PageSize, Is.EqualTo(64 * 1024));
			Assert.That(uniform64.BlocksPerPage, Is.EqualTo(1));
		}

		[Test]
		public void Test_Geometry_Rejects_Illegal_Sizes()
		{
			// block below 4 KiB
			Assert.That(() => new FdbLiteGeometry(11, 2), Throws.Exception);
			// page above the 64 KiB u16-offset ceiling
			Assert.That(() => new FdbLiteGeometry(16, 1), Throws.Exception);
			Assert.That(() => new FdbLiteGeometry(14, 3), Throws.Exception);
			// page below the 16 KiB inline-key floor
			Assert.That(() => new FdbLiteGeometry(12, 0), Throws.Exception);
			Assert.That(() => new FdbLiteGeometry(13, 0), Throws.Exception);
			Assert.That(() => new FdbLiteGeometry(12, 1), Throws.Exception);

			// smallest legal page: 4 KiB blocks, 16 KiB pages
			var smallest = new FdbLiteGeometry(12, 2);
			Assert.That(smallest.PageSize, Is.EqualTo(16 * 1024));
		}

		[Test]
		public void Test_Geometry_Equality_And_Header_Fields()
		{
			var a = new FdbLiteGeometry(14, 2);
			var b = FdbLiteGeometry.Hypothesis;
			Assert.That(a, Is.EqualTo(b));
			Assert.That(a.BlockSizeLog2, Is.EqualTo(14));
			Assert.That(a.PageSizeInBlocksLog2, Is.EqualTo(2));
			Assert.That(a, Is.Not.EqualTo(FdbLiteGeometry.Uniform(16)));
		}

	}

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLitePageHeaderFacts : SimpleTest
	{

		[Test]
		public void Test_Format_And_Field_Roundtrip()
		{
			foreach (var geometry in TestGeometries.All)
			{
				var page = new byte[geometry.PageSize];

				FdbLitePageHeader.Format(page, FdbLitePageType.Leaf, generation: 42);
				Assert.That(FdbLitePageHeader.GetPageType(page), Is.EqualTo(FdbLitePageType.Leaf));
				Assert.That(FdbLitePageHeader.GetEncoding(page), Is.EqualTo(FdbLitePageHeader.EncodingPlain));
				Assert.That(FdbLitePageHeader.GetGeneration(page), Is.EqualTo(42));
				Assert.That(FdbLitePageHeader.GetCellCount(page), Is.Zero);
				Assert.That(FdbLitePageHeader.GetCellAreaOffset(page), Is.Zero);

				FdbLitePageHeader.SetCellCount(page, 123);
				FdbLitePageHeader.SetCellAreaOffset(page, 65535);
				FdbLitePageHeader.SetTypeSpecific(page, 7);
				Assert.That(FdbLitePageHeader.GetCellCount(page), Is.EqualTo(123));
				Assert.That(FdbLitePageHeader.GetCellAreaOffset(page), Is.EqualTo(65535));
				Assert.That(FdbLitePageHeader.GetTypeSpecific(page), Is.EqualTo(7));
			}
		}

		[Test]
		public void Test_Checksum_Seal_And_Verify()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			var page = new byte[geometry.PageSize];
			FdbLitePageHeader.Format(page, FdbLitePageType.Internal, generation: 7);
			page[1000] = 0xAB;

			FdbLitePageHeader.Seal(page, firstBlockId: 12);
			Assert.That(FdbLitePageHeader.Verify(page, 12), Is.True, "sealed page should verify at its own location");

			// the checksum is seeded by the block id: the same bytes at another location must fail
			Assert.That(FdbLitePageHeader.Verify(page, 13), Is.False, "page written to the wrong location must fail verification");

			// any payload corruption must fail
			page[1000] ^= 0x01;
			Assert.That(FdbLitePageHeader.Verify(page, 12), Is.False, "corrupted payload must fail verification");
			page[1000] ^= 0x01;
			Assert.That(FdbLitePageHeader.Verify(page, 12), Is.True);

			// header corruption (generation) must fail too
			FdbLitePageHeader.SetGeneration(page, 8);
			Assert.That(FdbLitePageHeader.Verify(page, 12), Is.False, "corrupted header must fail verification");
		}

	}

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLitePagerFacts : SimpleTest
	{

		[Test]
		public void Test_Grow_Rounds_To_Regions_And_Preserves_Data()
		{
			foreach (var geometry in TestGeometries.All)
			{
				using var pager = new FdbLiteHeapPager(geometry);
				Assert.That(pager.BlockCount, Is.Zero);
				Assert.That(BitOperations.IsPow2(pager.RegionSizeInBlocks), Is.True);

				pager.Grow(1);
				Assert.That(pager.BlockCount, Is.EqualTo(pager.RegionSizeInBlocks), $"[{geometry}] growth is region-granular");

				// write a block, grow into a second region, and read it back unchanged
				var block = new byte[geometry.BlockSize];
				Random.Shared.NextBytes(block);
				pager.WriteBlocks(3, block);

				pager.Grow(pager.RegionSizeInBlocks + 1);
				Assert.That(pager.BlockCount, Is.EqualTo(2 * pager.RegionSizeInBlocks));
				Assert.That(pager.ReadBlocks(3, 1).SequenceEqual(block), Is.True, $"[{geometry}] data must survive growth");
			}
		}

		[Test]
		public void Test_Write_Read_Roundtrip_Across_Regions()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			using var pager = new FdbLiteHeapPager(geometry);
			pager.Grow(2 * pager.RegionSizeInBlocks);

			// a multi-block run inside the second region
			var data = new byte[3 * geometry.BlockSize];
			Random.Shared.NextBytes(data);
			uint first = pager.RegionSizeInBlocks + 5;
			pager.WriteBlocks(first, data);
			Assert.That(pager.ReadBlocks(first, 3).SequenceEqual(data), Is.True);

			// the same blocks addressed one by one see the same bytes
			for (int i = 0; i < 3; i++)
			{
				Assert.That(pager.ReadBlocks(first + (uint) i, 1).SequenceEqual(data.AsSpan(i * geometry.BlockSize, geometry.BlockSize)), Is.True);
			}
		}

		[Test]
		public void Test_Rejects_Straddling_And_Out_Of_Bounds_Runs()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			using var pager = new FdbLiteHeapPager(geometry);
			pager.Grow(2 * pager.RegionSizeInBlocks);

			// straddling a region boundary is a bug in the allocator, not a supported read
			Assert.That(() => { _ = pager.ReadBlocks(pager.RegionSizeInBlocks - 1, 2); }, Throws.Exception);

			// out of bounds
			Assert.That(() => { _ = pager.ReadBlocks(pager.BlockCount, 1); }, Throws.Exception);
			Assert.That(() => { _ = pager.ReadBlocks(pager.BlockCount - 1, 2); }, Throws.Exception);
		}

		[Test]
		public void Test_Truncate_Releases_Trailing_Regions()
		{
			var geometry = FdbLiteGeometry.Hypothesis;
			using var pager = new FdbLiteHeapPager(geometry);
			pager.Grow(3 * pager.RegionSizeInBlocks);

			var block = new byte[geometry.BlockSize];
			Random.Shared.NextBytes(block);
			pager.WriteBlocks(1, block);

			pager.Truncate(pager.RegionSizeInBlocks);
			Assert.That(pager.BlockCount, Is.EqualTo(pager.RegionSizeInBlocks));
			Assert.That(pager.ReadBlocks(1, 1).SequenceEqual(block), Is.True, "data below the cut must survive truncation");
			Assert.That(() => { _ = pager.ReadBlocks(pager.RegionSizeInBlocks, 1); }, Throws.Exception, "blocks above the cut are gone");

			// growth after truncation works
			pager.Grow(2 * pager.RegionSizeInBlocks);
			Assert.That(pager.BlockCount, Is.EqualTo(2 * pager.RegionSizeInBlocks));
		}

	}

}
