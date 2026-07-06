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

namespace SnowBank.Data.Json.Binary.Tests
{

	/// <summary>Tests that the JSONB binary decoder (<see cref="Jsonb"/>) handles malformed or hostile payloads safely: an inconsistent document is rejected with a <see cref="FormatException"/> rather than reading out of bounds or overflowing the stack.</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public class JsonbSecurityTest : SimpleTest
	{

		static JsonbSecurityTest()
		{
			Jsonb.Warmup();
		}

		// Header / container layout constants (see Jsonb.cs "Format Specifications").
		private const uint JCONTAINER_FLAG_ARRAY = 0x40000000u;

		[Test]
		public void Test_Select_Array_With_Truncated_Entries_Table_Must_Fail_Cleanly()
		{
			// A hand-crafted 9-byte document whose root ARRAY container claims 2 elements:
			//   bytes[0..4] : document header : TOTAL_SIZE = 9, no flags
			//   bytes[4..8] : container header: ARRAY | count(2)
			//   byte [8]    : the first byte of JEntry[0]; the other 3 bytes of that entry are missing
			// The container span is data[4..9] = 5 bytes, so JEntry[0] (container offset 4, size 4) runs off the end.
			// The document sits at the start of a larger array, so any stray read stays inside managed memory and the
			// test is safe to run.
			byte[] backing = new byte[16];
			BinaryPrimitives.WriteUInt32LittleEndian(backing.AsSpan(0), 9u);                        // document TOTAL_SIZE = 9
			BinaryPrimitives.WriteUInt32LittleEndian(backing.AsSpan(4), JCONTAINER_FLAG_ARRAY | 2u); // ARRAY, count = 2
			backing[8] = 0x11;                                                                       // JEntry[0] byte #0 (present)
			backing[9] = 0x22; backing[10] = 0x33; backing[11] = 0x44;                               // bytes outside the document

			// hand the decoder ONLY the 9-byte document window
			Slice doc = backing.AsSlice(0, 9);

			// looking up "[1]" would need all 4 bytes of JEntry[0], but only 1 is inside the container span: an entries
			// table that does not fit inside the container must be rejected with a FormatException
			Assert.That(() => Jsonb.Select(doc, "[1]"), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Select_Array_With_Count_Larger_Than_Buffer_Must_Fail_Cleanly()
		{
			// start from a valid array, then tamper ONLY the container's element count so it claims far more elements
			// than the buffer can hold
			byte[] blob = Jsonb.Encode(JsonArray.Create(1, 2, 3)).ToArray();

			// the root container header lives right after the 4-byte document header
			uint header = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(4));
			Assert.That(header & JCONTAINER_FLAG_ARRAY, Is.EqualTo(JCONTAINER_FLAG_ARRAY), "sanity: root should be an array container");

			// keep the 4 flag bits, replace the 28-bit count with an absurdly large value
			header = (header & 0xF0000000u) | 100_000u;
			BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(4), header);

			// a container that claims more elements than the buffer can hold must be rejected with a FormatException
			Assert.That(() => Jsonb.Select(blob.AsSlice(), "[50000]"), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Select_On_WellFormed_Array_Still_Works()
		{
			// legitimate lookups on a well-formed document must keep working
			byte[] blob = Jsonb.Encode(JsonArray.Create(10, 20, 30)).ToArray();
			using var _ = Assert.EnterMultipleScope();
			Assert.That(Jsonb.Select(blob.AsSlice(), "[0]").ToInt32(), Is.EqualTo(10));
			Assert.That(Jsonb.Select(blob.AsSlice(), "[2]").ToInt32(), Is.EqualTo(30));
			Assert.That(Jsonb.Select(blob.AsSlice(), "[3]"), Is.EqualTo(JsonNull.Missing));
		}

		[Test]
		public void Test_Decode_DeeplyNested_Must_Fail_Cleanly()
		{
			// a document nested beyond the decoder's depth limit must be rejected with a FormatException instead of
			// recursing until the (uncatchable) stack overflow
			JsonValue nested = JsonNumber.Return(1);
			for (int i = 0; i < 150; i++)
			{
				nested = new JsonArray { nested };
			}
			Slice blob = Jsonb.Encode(nested);
			Assert.That(() => Jsonb.Decode(blob), Throws.InstanceOf<FormatException>());
		}

	}

}
