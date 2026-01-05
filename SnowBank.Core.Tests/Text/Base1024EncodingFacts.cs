#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Text.Tests
{
	using SnowBank.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Base1024EncodingFacts : SimpleTest
	{

		[Test]
		public void Test_Base1024_Encoder_RoundTrips()
		{
			// ensure that we can encode any arbitrary size random buffer into a string literal, and back to the original bytes

			var sourceBuffer = new byte[100 + 16];
			var charBuffer = new char[100 + 16];
			var decodedBuffer = new byte[100 + 16];

			for (int i = 0; i < 10_000; i++)
			{
				// choose random length
				int len = this.Rnd.Next(1, 65);
				sourceBuffer.AsSpan().Fill(0xAA);
				decodedBuffer.AsSpan().Fill(0x55);
				charBuffer.AsSpan().Fill('_');

				var source = sourceBuffer.AsSpan(0, len);

				// fill with random bytes
				this.Rnd.NextBytes(source);

				// encode to string
				Assert.That(Base1024Encoding.TryEncodeTo(source, charBuffer, out var charsWritten), Is.True);
				var encoded = charBuffer.AsSpan(0, charsWritten);

				// ensure that the rest of the buffer is untouched
				if (charBuffer.AsSpan(charsWritten).ContainsAnyExcept('_'))
				{
					DumpHexa(charBuffer.AsSpan(charsWritten));
					Assert.Fail("Tail of chars buffer was modified!");
				}

				// decode back to bytes
				Assert.That(Base1024Encoding.TryDecodeTo(encoded, decodedBuffer, out var bytesWritten), Is.True);
				var decoded = decodedBuffer.AsSpan(0, bytesWritten);

				// ensure that the decoded bytes are valid
				if (decoded.Length != source.Length)
				{
					DumpVersus(source, decoded);
					Assert.That(decoded.Length, Is.EqualTo(source.Length));
				}
				if (!decoded.SequenceEqual(source))
				{
					DumpVersus(source, decoded);
					Assert.That(decoded.ToArray(), Is.EqualTo(source.ToArray()));
				}

				// ensure that the rest of the buffer is untouched
				if (decodedBuffer.AsSpan(bytesWritten).ContainsAnyExcept((byte) 0x55))
				{
					DumpHexa(decodedBuffer.AsSpan(bytesWritten));
					Assert.Fail("Tail of decoded buffer was modified!");
				}

			}
		}

		[Test]
		public void Test_Encode_Integer_Values()
		{
			// int
			Assert.Multiple(() =>
			{
				Assert.That(Base1024Encoding.EncodeInt32Value((int) 0), Is.EqualTo("0000!"));
				Assert.That(Base1024Encoding.EncodeInt32Value((int) 1), Is.EqualTo("000İ!"));
				Assert.That(Base1024Encoding.EncodeInt32Value((int) 0x12345678), Is.EqualTo("x͵ǎ0!"));
				Assert.That(Base1024Encoding.EncodeInt32Value(int.MinValue), Is.EqualTo("\u0230\u0030\u0030\u0030\u0021"));
				Assert.That(Base1024Encoding.EncodeInt32Value(int.MaxValue), Is.EqualTo("\u022f\u042f\u042f\u0330\u0021"));
			});

			// long
			Assert.Multiple(() =>
			{
				Assert.That(Base1024Encoding.EncodeInt64Value((long) 0), Is.EqualTo("0000000"));
				Assert.That(Base1024Encoding.EncodeInt64Value((long) 1), Is.EqualTo("000000p"));
				Assert.That(Base1024Encoding.EncodeInt64Value((long) 0x0123456789abcdef), Is.EqualTo("4ɤƉι˟Ďϰ"));
				Assert.That(Base1024Encoding.EncodeInt64Value(long.MinValue), Is.EqualTo("Ȱ000000"));
				Assert.That(Base1024Encoding.EncodeInt64Value(long.MaxValue), Is.EqualTo("ȯЯЯЯЯЯϰ"));
			});

			// Guid
			Assert.Multiple(() =>
			{
				Assert.That(Base1024Encoding.EncodeGuidValue(Guid.Empty), Is.EqualTo("0000000000000"));
				Assert.That(Base1024Encoding.EncodeGuidValue(Guid.Parse("8fcccfbd-858e-4583-9534-868f427328fe")), Is.EqualTo("ɯüПƵɩ\u0088ĕŤɊĤÌ͘Ш"));
				Assert.That(Base1024Encoding.EncodeGuidValue(Guid.Parse("4a0bfdba-f7b2-41b3-a5ba-1e3cfd6b6095")), Is.EqualTo("ŘïΞ̧˹KęǪ¨ϿΊΐʄ"));
#if NET9_0_OR_GREATER
				Assert.That(Base1024Encoding.EncodeGuidValue(Guid.AllBitsSet), Is.EqualTo("ЯЯЯЯЯЯЯЯЯЯЯЯЬ"));
#endif
			});

			// Uuid128
			Assert.Multiple(() =>
			{
				Assert.That(Base1024Encoding.EncodeUuid128Value(Uuid128.Empty), Is.EqualTo("0000000000000"));
				Assert.That(Base1024Encoding.EncodeUuid128Value(Uuid128.Parse("8fcccfbd-858e-4583-9534-868f427328fe")), Is.EqualTo("ɯüПƵɩ\u0088ĕŤɊĤÌ͘Ш"));
				Assert.That(Base1024Encoding.EncodeUuid128Value(Uuid128.Parse("4a0bfdba-f7b2-41b3-a5ba-1e3cfd6b6095")), Is.EqualTo("ŘïΞ̧˹KęǪ¨ϿΊΐʄ"));
				Assert.That(Base1024Encoding.EncodeUuid128Value(Uuid128.AllBitsSet), Is.EqualTo("ЯЯЯЯЯЯЯЯЯЯЯЯЬ"));
			});

		}

	}

}
