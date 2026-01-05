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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable ReplaceAsyncWithTaskReturn

namespace SnowBank.IO.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class PartialReadOnlyStreamFacts : SimpleTest
	{

		private const int DEFAULT_STREAM_SIZE = 1 * 1024 * 1024;

		[Test]
		public void Test_Does_Not_Allow_Writing()
		{
			using(var ms = new MemoryStream(new byte[65536], writable: true)) 
			using(var crs = new PartialReadOnlyStream(ms, 123, 456, ownsStream: false))
			{
				Assert.That(ms.CanWrite, Is.True, "Inner stream can be writable");
				Assert.That(crs.CanWrite, Is.False, "Chunked stream should not be writable");

				Assert.That(() => crs.WriteByte(123), Throws.Exception, "Should not be able to WriteByte()");
				Assert.That(() => crs.Write(new byte[123], 0, 123), Throws.Exception, "Should not be able to Write([])");
				Assert.That(() => crs.Write(new byte[123].AsSpan()), Throws.Exception, "Should not be able to Write(Span)");
				Assert.That(async () => await crs.WriteAsync(new byte[123], 0, 123, this.Cancellation), Throws.Exception, "Should not be able to WriteAsync([])");
				Assert.That(async () => await crs.WriteAsync(new byte[123].AsMemory(), this.Cancellation), Throws.Exception, "Should not be able to WriteAsync(Memory)");
				Assert.That(() => crs.SetLength(123), Throws.Exception, "Should not be able to SetLength()");
				Assert.That(() => crs.Flush(), Throws.Exception, "Should not be able to Flush()");
			}
		}

		[Test]
		public void Test_Dispose_Inner_If_Owned()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;

			var ms = new MemoryStream(data, writable: false);
			var crs = new PartialReadOnlyStream(ms, START, LENGTH, ownsStream: true);
			Assert.That(crs.CanRead, Is.True, "Chunked stream should be readable before disposing");
			Assert.That(crs.CanSeek, Is.True, "Chunked stream should be seekable before disposing");

			// attempting to read or seek before should succeed
			Assert.That(() => crs.ReadByte(), Throws.Nothing);
			Assert.That(() => crs.Seek(0, SeekOrigin.Begin), Throws.Nothing);

			crs.Dispose();
			Assert.That(ms.CanRead, Is.False, "Inner stream should be disposed");
			Assert.That(crs.CanRead, Is.False, "Chunked stream should not be readable after disposing");
			Assert.That(crs.CanSeek, Is.False, "Chunked stream should not be seekable after disponsing");

			// attempting to read or seek after that should fail
			Assert.That(() => crs.ReadByte(), Throws.InstanceOf<ObjectDisposedException>());
			Assert.That(() => crs.Seek(0, SeekOrigin.Begin), Throws.InstanceOf<ObjectDisposedException>());

			// multi-dispose should do nothing
			crs.Close();
			crs.Dispose();
		}

		[Test]
		public void Test_Does_Not_Dispose_Inner_If_Not_Owned()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;

			var ms = new MemoryStream(data, writable: false);
			var crs = new PartialReadOnlyStream(ms, START, LENGTH, ownsStream: false);
			Assert.That(crs.CanRead, Is.True, "Chunked stream should be readable before disposing");
			Assert.That(crs.CanSeek, Is.True, "Chunked stream should be seekable before disposing");

			// attempting to read or seek before should succeed
			Assert.That(() => crs.ReadByte(), Throws.Nothing);
			Assert.That(() => crs.Seek(0, SeekOrigin.Begin), Throws.Nothing);

			crs.Dispose();
			Assert.That(ms.CanRead, Is.True, "Inner stream should NOT be disposed");
			Assert.That(crs.CanRead, Is.False, "Chunked stream should not be readable after disposing");
			Assert.That(crs.CanSeek, Is.False, "Chunked stream should not be seekable after disponsing");

			// attempting to read or seek after that should fail
			Assert.That(() => crs.ReadByte(), Throws.InstanceOf<ObjectDisposedException>());
			Assert.That(() => crs.Seek(0, SeekOrigin.Begin), Throws.InstanceOf<ObjectDisposedException>());

			// multi-dispose should do nothing
			crs.Close();
			crs.Dispose();
			Assert.That(ms.CanRead, Is.True, "Inner stream should NOT be disposed");
		}

		[Test]
		public void Test_Can_ReadByte()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;
			var chunk = data.AsSlice(START, LENGTH);

			using(var ms = new MemoryStream(data, writable: false))
			using(var crs = new PartialReadOnlyStream(ms, START, LENGTH, false))
			{
				// check we are correclty positionned in the stream
				Assert.That(crs.Position, Is.EqualTo(0), "Substream should start at 0");
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be at the start of the chunk");

				// read single bytes from the start
				Assert.That(crs.ReadByte(), Is.EqualTo(chunk[0]), "Should read the first byte of the chunk");
				Assert.That(crs.ReadByte(), Is.EqualTo(chunk[1]), "Should read the second byte of the chunk");
				Assert.That(crs.ReadByte(), Is.EqualTo(chunk[2]), "Should read the third byte of the chunk");
				Assert.That(crs.Position, Is.EqualTo(3));
				Assert.That(ms.Position, Is.EqualTo(START + 3));

				// seek relative to current
				crs.Seek(10, SeekOrigin.Current);
				Assert.That(crs.Position, Is.EqualTo(13));
				Assert.That(ms.Position, Is.EqualTo(START + 13));
				Assert.That(crs.ReadByte(), Is.EqualTo(chunk[13]));

				// seek to the start
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be back at the start of the chunk");
				Assert.That(crs.ReadByte(), Is.EqualTo(chunk[0]));

				// seek just before the end
				crs.Seek(1, SeekOrigin.End);
				Assert.That(crs.Position, Is.EqualTo(LENGTH - 1));
				Assert.That(crs.ReadByte(), Is.EqualTo(chunk[^1]), "Should read the last byte of the chunk");
				// next read should fail
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(crs.ReadByte(), Is.EqualTo(-1), "Reading past the end of the chunk should fail");

				// seek after the end
				crs.Seek(LENGTH + 100, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(LENGTH + 100), "Should allow seeking past the end");
				Assert.That(crs.ReadByte(), Is.EqualTo(-1), "But reading past the end should fail");

				// seek before the start should throw
				Assert.That(() => crs.Seek(-1, SeekOrigin.Begin), Throws.InstanceOf<IOException>());
			}
		}

		[Test]
		public void Test_Can_Read_Array()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;
			var chunk = data.AsSlice(START, LENGTH);

			using(var ms = new MemoryStream(data, writable: false))
			using(var crs = new PartialReadOnlyStream(ms, START, LENGTH, false))
			{
				var buf = new byte[1024];

				// check we are correclty positionned in the stream
				Assert.That(crs.Position, Is.EqualTo(0), "Substream should start at 0");
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be at the start of the chunk");

				// read 3 bytes from the start
				Assert.That(crs.Read(buf, 0, 3), Is.EqualTo(3));
				Assert.That(buf.AsSlice(0, 3), Is.EqualTo(chunk[..3]));
				Assert.That(crs.Position, Is.EqualTo(3));
				Assert.That(ms.Position, Is.EqualTo(START + 3));

				// seek relative to current
				crs.Seek(10, SeekOrigin.Current);
				Assert.That(crs.Position, Is.EqualTo(13));
				Assert.That(ms.Position, Is.EqualTo(START + 13));
				Assert.That(crs.Read(buf, 0, 17), Is.EqualTo(17));
				Assert.That(buf.AsSlice(0, 17), Is.EqualTo(chunk[13..30]));

				// seek to the start
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be back at the start of the chunk");
				Assert.That(crs.Read(buf, 0, 10), Is.EqualTo(10));
				Assert.That(buf.AsSlice(0, 10), Is.EqualTo(chunk[..10]));

				// seek just before the end
				crs.Seek(1, SeekOrigin.End);
				Assert.That(crs.Position, Is.EqualTo(LENGTH - 1));
				Assert.That(crs.Read(buf, 0, 10), Is.EqualTo(1));
				Assert.That(buf[0], Is.EqualTo(chunk[^1]));

				// next read should fail
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(crs.Read(buf, 0, 10), Is.EqualTo(0));

				// seek after the end
				crs.Seek(LENGTH + 100, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(LENGTH + 100), "Should allow seeking past the end");
				Assert.That(crs.Read(buf, 0, 10), Is.EqualTo(0), "But reading past the end should fail");

				// seek to start and read everything with a large buffer
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START));

				Assert.That(crs.Read(buf, 0, buf.Length), Is.EqualTo(LENGTH), "Should read the whole chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));

				Assert.That(buf.AsSlice(0, LENGTH), Is.EqualTo(chunk));
				Assert.That(crs.Read(buf, 0, buf.Length), Is.EqualTo(0), "No more data after that");

				// seek to somewhere in the middle and read everything with a large buffer
				crs.Seek(234, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(234));
				Assert.That(ms.Position, Is.EqualTo(START + 234));

				Assert.That(crs.Read(buf, 0, buf.Length), Is.EqualTo(LENGTH - 234), "Should read the rest of the chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
				Assert.That(buf.AsSlice(0, LENGTH - 234), Is.EqualTo(chunk.Substring(234)));

				Assert.That(crs.Read(buf, 0, buf.Length), Is.EqualTo(0), "No more data after that");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
			}

		}

		[Test]
		public void Test_Can_Read_Span()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;
			var chunk = data.AsSlice(START, LENGTH);

			using(var ms = new MemoryStream(data, writable: false))
			using(var crs = new PartialReadOnlyStream(ms, START, LENGTH, false))
			{
				var buf = new byte[1024];

				// check we are correclty positionned in the stream
				Assert.That(crs.Position, Is.EqualTo(0), "Substream should start at 0");
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be at the start of the chunk");

				// read 3 bytes from the start
				Assert.That(crs.Read(buf.AsSpan(0, 3)), Is.EqualTo(3));
				Assert.That(buf.AsSlice(0, 3), Is.EqualTo(chunk[..3]));
				Assert.That(crs.Position, Is.EqualTo(3));
				Assert.That(ms.Position, Is.EqualTo(START + 3));

				// seek relative to current
				crs.Seek(10, SeekOrigin.Current);
				Assert.That(crs.Position, Is.EqualTo(13));
				Assert.That(ms.Position, Is.EqualTo(START + 13));
				Assert.That(crs.Read(buf.AsSpan(0, 17)), Is.EqualTo(17));
				Assert.That(buf.AsSlice(0, 17), Is.EqualTo(chunk[13..30]));

				// seek to the start
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be back at the start of the chunk");
				Assert.That(crs.Read(buf.AsSpan(0, 10)), Is.EqualTo(10));
				Assert.That(buf.AsSlice(0, 10), Is.EqualTo(chunk[..10]));

				// seek just before the end
				crs.Seek(1, SeekOrigin.End);
				Assert.That(crs.Position, Is.EqualTo(LENGTH - 1));
				Assert.That(crs.Read(buf.AsSpan(0, 10)), Is.EqualTo(1));
				Assert.That(buf[0], Is.EqualTo(chunk[^1]));

				// next read should fail
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(crs.Read(buf.AsSpan(0, 10)), Is.EqualTo(0));

				// seek after the end
				crs.Seek(LENGTH + 100, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(LENGTH + 100), "Should allow seeking past the end");
				Assert.That(crs.Read(buf.AsSpan(0, 10)), Is.EqualTo(0), "But reading past the end should fail");

				// seek to start and read everything with a large buffer
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START));

				Assert.That(crs.Read(buf.AsSpan()), Is.EqualTo(LENGTH), "Should read the whole chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));

				Assert.That(buf.AsSlice(0, LENGTH), Is.EqualTo(chunk));
				Assert.That(crs.Read(buf.AsSpan()), Is.EqualTo(0), "No more data after that");

				// seek to somewhere in the middle and read everything with a large buffer
				crs.Seek(234, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(234));
				Assert.That(ms.Position, Is.EqualTo(START + 234));

				Assert.That(crs.Read(buf.AsSpan()), Is.EqualTo(LENGTH - 234), "Should read the rest of the chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
				Assert.That(buf.AsSlice(0, LENGTH - 234), Is.EqualTo(chunk.Substring(234)));

				Assert.That(crs.Read(buf.AsSpan()), Is.EqualTo(0), "No more data after that");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
			}

		}

		[Test]
		public async Task Test_Can_ReadAsync_Array()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;
			var chunk = data.AsSlice(START, LENGTH);

			using(var ms = new MemoryStream(data, writable: false))
			using(var crs = new PartialReadOnlyStream(ms, START, LENGTH, false))
			{
				var buf = new byte[1024];

				// check we are correclty positionned in the stream
				Assert.That(crs.Position, Is.EqualTo(0), "Substream should start at 0");
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be at the start of the chunk");

				// read 3 bytes from the start
				Assert.That(await crs.ReadAsync(buf, 0, 3, this.Cancellation), Is.EqualTo(3));
				Assert.That(buf.AsSlice(0, 3), Is.EqualTo(chunk[..3]));
				Assert.That(crs.Position, Is.EqualTo(3));
				Assert.That(ms.Position, Is.EqualTo(START + 3));

				// seek relative to current
				crs.Seek(10, SeekOrigin.Current);
				Assert.That(crs.Position, Is.EqualTo(13));
				Assert.That(ms.Position, Is.EqualTo(START + 13));
				Assert.That(await crs.ReadAsync(buf, 0, 17, this.Cancellation), Is.EqualTo(17));
				Assert.That(buf.AsSlice(0, 17), Is.EqualTo(chunk[13..30]));

				// seek to the start
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be back at the start of the chunk");
				Assert.That(await crs.ReadAsync(buf, 0, 10, this.Cancellation), Is.EqualTo(10));
				Assert.That(buf.AsSlice(0, 10), Is.EqualTo(chunk[..10]));

				// seek just before the end
				crs.Seek(1, SeekOrigin.End);
				Assert.That(crs.Position, Is.EqualTo(LENGTH - 1));
				Assert.That(await crs.ReadAsync(buf, 0, 10, this.Cancellation), Is.EqualTo(1));
				Assert.That(buf[0], Is.EqualTo(chunk[^1]));

				// next read should fail
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(await crs.ReadAsync(buf, 0, 10, this.Cancellation), Is.EqualTo(0));

				// seek after the end
				crs.Seek(LENGTH + 100, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(LENGTH + 100), "Should allow seeking past the end");
				Assert.That(await crs.ReadAsync(buf, 0, 10, this.Cancellation), Is.EqualTo(0), "But reading past the end should fail");

				// seek to start and read everything with a large buffer
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START));

				Assert.That(await crs.ReadAsync(buf, 0, buf.Length, this.Cancellation), Is.EqualTo(LENGTH), "Should read the whole chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));

				Assert.That(buf.AsSlice(0, LENGTH), Is.EqualTo(chunk));
				Assert.That(await crs.ReadAsync(buf, 0, buf.Length, this.Cancellation), Is.EqualTo(0), "No more data after that");

				// seek to somewhere in the middle and read everything with a large buffer
				crs.Seek(234, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(234));
				Assert.That(ms.Position, Is.EqualTo(START + 234));

				Assert.That(await crs.ReadAsync(buf, 0, buf.Length, this.Cancellation), Is.EqualTo(LENGTH - 234), "Should read the rest of the chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
				Assert.That(buf.AsSlice(0, LENGTH - 234), Is.EqualTo(chunk.Substring(234)));

				Assert.That(await crs.ReadAsync(buf, 0, buf.Length, this.Cancellation), Is.EqualTo(0), "No more data after that");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
			}

		}

		[Test]
		public async Task Test_Can_ReadAsync_Memory()
		{
			var data = GetRandomData(DEFAULT_STREAM_SIZE);
			const int START = 123;
			const int LENGTH = 456;
			var chunk = data.AsSlice(START, LENGTH);

			using(var ms = new MemoryStream(data, writable: false))
			using(var crs = new PartialReadOnlyStream(ms, START, LENGTH, false))
			{
				var buf = new byte[1024];

				// check we are correclty positionned in the stream
				Assert.That(crs.Position, Is.EqualTo(0), "Substream should start at 0");
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be at the start of the chunk");

				// read 3 bytes from the start
				Assert.That(await crs.ReadAsync(buf.AsMemory(0, 3), this.Cancellation), Is.EqualTo(3));
				Assert.That(buf.AsSlice(0, 3), Is.EqualTo(chunk[..3]));
				Assert.That(crs.Position, Is.EqualTo(3));
				Assert.That(ms.Position, Is.EqualTo(START + 3));

				// seek relative to current
				crs.Seek(10, SeekOrigin.Current);
				Assert.That(crs.Position, Is.EqualTo(13));
				Assert.That(ms.Position, Is.EqualTo(START + 13));
				Assert.That(await crs.ReadAsync(buf.AsMemory(0, 17), this.Cancellation), Is.EqualTo(17));
				Assert.That(buf.AsSlice(0, 17), Is.EqualTo(chunk[13..30]));

				// seek to the start
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START), "Inner stream should be back at the start of the chunk");
				Assert.That(await crs.ReadAsync(buf.AsMemory(0, 10), this.Cancellation), Is.EqualTo(10));
				Assert.That(buf.AsSlice(0, 10), Is.EqualTo(chunk[..10]));

				// seek just before the end
				crs.Seek(1, SeekOrigin.End);
				Assert.That(crs.Position, Is.EqualTo(LENGTH - 1));
				Assert.That(await crs.ReadAsync(buf.AsMemory(0, 10), this.Cancellation), Is.EqualTo(1));
				Assert.That(buf[0], Is.EqualTo(chunk[^1]));

				// next read should fail
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(await crs.ReadAsync(buf.AsMemory(0, 10), this.Cancellation), Is.EqualTo(0));

				// seek after the end
				crs.Seek(LENGTH + 100, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(LENGTH + 100), "Should allow seeking past the end");
				Assert.That(await crs.ReadAsync(buf.AsMemory(0, 10), this.Cancellation), Is.EqualTo(0), "But reading past the end should fail");

				// seek to start and read everything with a large buffer
				crs.Seek(0, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(0));
				Assert.That(ms.Position, Is.EqualTo(START));

				Assert.That(await crs.ReadAsync(buf.AsMemory(), this.Cancellation), Is.EqualTo(LENGTH), "Should read the whole chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));

				Assert.That(buf.AsSlice(0, LENGTH), Is.EqualTo(chunk));
				Assert.That(await crs.ReadAsync(buf.AsMemory(), this.Cancellation), Is.EqualTo(0), "No more data after that");

				// seek to somewhere in the middle and read everything with a large buffer
				crs.Seek(234, SeekOrigin.Begin);
				Assert.That(crs.Position, Is.EqualTo(234));
				Assert.That(ms.Position, Is.EqualTo(START + 234));

				Assert.That(await crs.ReadAsync(buf.AsMemory(), this.Cancellation), Is.EqualTo(LENGTH - 234), "Should read the rest of the chunk");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
				Assert.That(buf.AsSlice(0, LENGTH - 234), Is.EqualTo(chunk.Substring(234)));

				Assert.That(await crs.ReadAsync(buf.AsMemory(), this.Cancellation), Is.EqualTo(0), "No more data after that");
				Assert.That(crs.Position, Is.EqualTo(LENGTH));
				Assert.That(ms.Position, Is.EqualTo(START + LENGTH));
			}

		}

	}

}
