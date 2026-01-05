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

// ReSharper disable HeapView.BoxingAllocation
// ReSharper disable HeapView.ObjectAllocation
#pragma warning disable CS0618 // Type or member is obsolete
namespace System.IO.Hashing.Tests
{

	public class XxHashFacts : SimpleTest
	{

		[Test]
		public void Test_XxHash32_HashToUInt32()
		{
			// verify assumtions
			Assume.That(XxHash32.HashToUInt32(""u8), Is.EqualTo(0x02CC5D05));
			Assume.That(XxHash32.HashToUInt32("ABC"u8), Is.EqualTo(0x80712ED5));
			Assume.That(XxHash32.HashToUInt32("foobar"u8), Is.EqualTo(3986901679));
			Assume.That(XxHash32.HashToUInt32("Hello World"u8), Is.EqualTo(2986153710));
			Assume.That(XxHash32.HashToUInt32("hello world"u8), Is.EqualTo(3468387874), "Case sensitive!");
			Assume.That(XxHash32.HashToUInt32("Hello World "u8), Is.EqualTo(2612576248));
			Assume.That(XxHash32.HashToUInt32("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"u8), Is.EqualTo(3249361536));
			Assume.That(XxHash32.HashToUInt32("foobar"u8.Slice(1, 0)), Is.EqualTo(0x02CC5D05));

			using (Assert.EnterMultipleScope())
			{
				// Slices
				Assert.That(XxHash32.HashToUInt32(Slice.Nil), Is.EqualTo(0x02CC5D05));
				Assert.That(XxHash32.HashToUInt32(Slice.Empty), Is.EqualTo(0x02CC5D05));
				Assert.That(XxHash32.HashToUInt32("ABC"u8.ToSlice()), Is.EqualTo(0x80712ED5));
				Assert.That(XxHash32.HashToUInt32("Hello World"u8.ToSlice()), Is.EqualTo(0xB1FD16EE));
				Assert.That(XxHash32.HashToUInt32("\u0001\u0002\u0003\u0004ABC\u0005\u0006"u8.ToSlice().Substring(4, 3)), Is.EqualTo(0x80712ED5));
				Assert.That(XxHash32.HashToUInt32("ABC"u8.ToSlice().Substring(1, 0)), Is.EqualTo(0x02CC5D05));

				// UTF-8 (default)
				Assert.That(XxHash32.HashToUInt32(""), Is.EqualTo(0x02CC5D05));
				Assert.That(XxHash32.HashToUInt32("foobar"), Is.EqualTo(3986901679));
				Assert.That(XxHash32.HashToUInt32("foobar".ToCharArray()), Is.EqualTo(3986901679));
				Assert.That(XxHash32.HashToUInt32("Hello World"), Is.EqualTo(2986153710));
				Assert.That(XxHash32.HashToUInt32("hello world"), Is.EqualTo(3468387874), "Case sensitive!");
				Assert.That(XxHash32.HashToUInt32("Hello World "), Is.EqualTo(2612576248));
				Assert.That(XxHash32.HashToUInt32("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"), Is.EqualTo(3249361536));
				Assert.That(XxHash32.HashToUInt32("test", 123), Is.EqualTo(0xA46DCA0A));

				// UTF-16
				Assert.That(XxHash32.HashToUInt32("", encoding: Encoding.Unicode), Is.EqualTo(0x02CC5D05));
				Assert.That(XxHash32.HashToUInt32("foobar", encoding: Encoding.Unicode), Is.EqualTo(319326668));
				Assert.That(XxHash32.HashToUInt32("foobar".ToCharArray(), encoding: Encoding.Unicode), Is.EqualTo(319326668));
				Assert.That(XxHash32.HashToUInt32("Hello World", encoding: Encoding.Unicode), Is.EqualTo(690424818));
				Assert.That(XxHash32.HashToUInt32("hello world", encoding: Encoding.Unicode), Is.EqualTo(3418293499), "Case sensitive!");
				Assert.That(XxHash32.HashToUInt32("Hello World ", encoding: Encoding.Unicode), Is.EqualTo(1029714533));
				Assert.That(XxHash32.HashToUInt32("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", encoding: Encoding.Unicode), Is.EqualTo(2453304613));
				Assert.That(XxHash32.HashToUInt32("foobar".AsSpan(1, 0), encoding: Encoding.Unicode), Is.EqualTo(0x02CC5D05));
				Assert.That(XxHash32.HashToUInt32("test", 123, Encoding.Unicode), Is.EqualTo(0x59E65F18));
			}
		}

	}

}
