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

namespace FoundationDB.Layers.Blobs.Tests
{

	[TestFixture]
	public class BlobFacts : FdbTest
	{

		[Test]
		public async Task Test_NotFound_Blob_Is_Empty()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			var blob = new FdbBlob(db.Root);

			long? size = await blob.ReadAsync(db, (tr, state) => state.GetSizeAsync(tr), this.Cancellation);
			Assert.That(size, Is.Null, "Non existing blob should have no size");
		}

		[Test]
		public async Task Test_Can_Append_To_Blob()
		{
			using var db = await this.OpenTestPartitionAsync();
			await this.CleanLocation(db);

			var blob = new FdbBlob(db.Root);

			Log("Insert blob in 3 chunks...");
			await blob.WriteAsync(db, async (tr, state) =>
			{
				await state.AppendAsync(tr, Text("Attack"));
				await state.AppendAsync(tr, Text(" of the "));
				await state.AppendAsync(tr, Text("Blobs!"));

			}, this.Cancellation);
#if DEBUG
			await this.DumpSubspace(db);
#endif

			Log("Checking blob size...");
			long? size = await blob.ReadAsync(db, (tr, state) => state.GetSizeAsync(tr), this.Cancellation);
			Log($"> {size:N0}");
			Assert.That(size, Is.EqualTo(20));

			Log("Checking blob content...");
			var data = await blob.ReadAsync(db, (tr, state) => state.ReadAsync(tr, 0, (int) (size ?? 0)), this.Cancellation);
			Log($"> {data.Count:N0} bytes");
			Assert.That(data.ToUnicode(), Is.EqualTo("Attack of the Blobs!"));
		}

		[Test]
		public async Task Test_Can_Append_Large_Chunks()
		{
			using var db = await this.OpenTestPartitionAsync();
			await this.CleanLocation(db);

			var blob = new FdbBlob(db.Root);

			var data = Slice.Zero(100_000);
			for (int i = 0; i < data.Count; i++) data.Array[data.Offset + i] = (byte)i;

			Log("Construct blob by appending chunks...");
			for (int i = 0; i < 50; i++)
			{
				await blob.WriteAsync(db, (tr, state) => state.AppendAsync(tr, data), this.Cancellation);
			}

			Log("Reading blob size:");
			long? size = await blob.ReadAsync(db, (tr, state) => state.GetSizeAsync(tr), this.Cancellation);
			Log($"> {size:N0}");
			Assert.That(size, Is.EqualTo(50 * data.Count));

			Log("Reading blob content:");
			var s = await blob.ReadAsync(db, (tr, state) => state.ReadAsync(tr, 1234567, 1_000_000), this.Cancellation);
			Log($"> {s.Count:N0} bytes");
			Assert.That(s.Count, Is.EqualTo(1_000_000));

			// should contain the correct data
			for (int i = 0; i < s.Count; i++)
			{
				if (s[i] != (byte) ((1234567 + i) % data.Count))
				{
					Assert.Fail($"Corrupted blob chunk at {i}: {s[i, i + 128]}");
				}
			}
		}

	}

}
