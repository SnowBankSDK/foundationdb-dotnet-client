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

// ReSharper disable StringLiteralTypo
// ReSharper disable RedundantCast

namespace FoundationDB.Client.Tests
{
	using System.Text;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbValueFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbValue_Raw_Encoding()
		{
			{ // Slice
				var rawValue = Slice.FromBytes("Hello, World!"u8);

				var value = FdbValue.ToBytes(rawValue);

				Assert.That(value.Data, Is.EqualTo(rawValue));
				Assert.That(value.TypeHint, Is.EqualTo(FdbValueTypeHint.Binary));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Binary));

				Assert.That(value.ToSlice(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Count));
			}
			{ // byte[]
				var rawValue = "Hello, World!"u8.ToArray();

				var value = FdbValue.ToBytes(rawValue);

				Assert.That(value.Data, Is.EqualTo(rawValue));
				Assert.That(value.TypeHint, Is.EqualTo(FdbValueTypeHint.Binary));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Binary));

				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Length));
			}
#if !NETFRAMEWORK
			// FdbValue.ToBytes(MemoryStream) requires FdbValue<TValue, TEncoder>, which is not available on this target (static abstract interface members)
			{ // MemoryStream
				var ms = new MemoryStream();
				ms.Write("Hello, World!"u8);

				var value = FdbValue.ToBytes(ms);

				Assert.That(value.Data, Is.SameAs(ms));
				Assert.That(value.TypeHint, Is.EqualTo(FdbValueTypeHint.Binary));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Binary));

				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(ms.ToArray()));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(ms.ToArray()));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(ms.Length));
			}
#endif
		}

		[Test]
		public void Test_FdbValue_Utf8_Encoding()
		{
			static void Verify(string literal, ReadOnlySpan<byte> expectedUtf8)
			{
				var expected = Slice.FromBytes(expectedUtf8);
				Log($"# [{literal.Length:N0}] \"{literal}\"");
				Log($"> expected: [{expected.Count:N0}] `{expected}`");

				{ // string
					var value = FdbValue.ToTextUtf8(literal);
					Assert.That(value.Text.ToString(), Is.EqualTo(literal));
					Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Utf8));

					var slice = value.ToSlice();
					Log($"> actual  : [{slice.Count:N0}] `{slice}`");
					Assert.That(slice, Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}
				{ // ReadOnlyMemory<char>
					var value = FdbValue.ToTextUtf8(literal.ToArray().AsMemory());
					Assert.That(value.Text.ToString(), Is.EqualTo(literal));
					Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Utf8));

					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}
				{ // char[]
					var value = FdbValue.ToTextUtf8(literal.ToArray());
					Assert.That(value.Text.ToString(), Is.EqualTo(literal));
					Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Utf8));

					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}
#if !NETFRAMEWORK
				// FdbValue.ToTextUtf8(StringBuilder) requires FdbValue<TValue, TEncoder>, which is not available on this target (static abstract interface members)
				{ // StringBuilder
					var sb = new StringBuilder().Append(literal);
					var value = FdbValue.ToTextUtf8(sb);
					Assert.That(value.Data!.ToString(), Is.EqualTo(literal));
					Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Utf8));

					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count)); // GetMaxByteCount() always add 3 bytes to the length (for the BOM??)
				}
#endif
				{ // ReadOnlySpan<char>
					var value = FdbValue.ToTextUtf8(literal.AsSpan());
					Assert.That(value.Text.ToString(), Is.EqualTo(literal));
					Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Utf8));

					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}

				// repeatedly test encoding with various buffer sizes
				Span<byte> buffer = stackalloc byte[expectedUtf8.Length + 1];
				for(int i = buffer.Length; i >= 0; i--)
				{
					buffer.Fill(0xFF);
					var span = buffer[..i];
					span.Fill(0xAA);

					var value = FdbValue.ToTextUtf8(literal);
					if (i >= expectedUtf8.Length)
					{
						Assert.That(value.TryEncode(span, out var bytesWritten), Is.True.WithOutput(bytesWritten).EqualTo(expectedUtf8.Length));
						if (!span[..bytesWritten].SequenceEqual(expectedUtf8))
						{
							Assert.That(Slice.FromBytes(span[..bytesWritten]), Is.EqualTo(expected));
						}
					}
					else
					{
						Assert.That(value.TryEncode(span, out var bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
					}
					Assert.That(buffer[i..].ContainsAnyExcept((byte) 0xFF), Is.False);
				}
			}

			Verify("", ""u8);
			Verify("A", "A"u8);
			Verify("Hello, World!", "Hello, World!"u8);
			Verify("こんにちは世界", "こんにちは世界"u8);
			Verify("Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis", "Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis"u8);

			var largeAscii = GetRandomHexString(1025);
			Verify(largeAscii, Encoding.UTF8.GetBytes(largeAscii));

			var largeUnicode = GetRandomString("こんにちは世界", 513);
			Verify(largeUnicode, Encoding.UTF8.GetBytes(largeUnicode));
		}

		[Test]
		public void Test_FdbValue_Tuple_Encoding()
		{
			static void Verify<TTuple>(in TTuple tuple)
				where TTuple : IVarTuple
			{
				var expectedPacked = TuPack.Pack(tuple);
				var expectedUnpacked = TuPack.Unpack(expectedPacked);
				Log($"# ({tuple.GetType().GetFriendlyName()}) {tuple}: -> [{expectedPacked.Count:N0}] {expectedPacked:x} -> {expectedUnpacked}");

				var value = FdbValue.FromTuple(tuple);
				var slice = value.ToSlice();
				Assert.That(slice, Is.EqualTo(expectedPacked));
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.False.WithOutput(size).Zero);

				var unpacked = TuPack.Unpack(slice);
				Assert.That(unpacked, Is.EqualTo(expectedUnpacked));
			}

			Verify(STuple.Create());
			Verify(STuple.Create("Hello"));
			Verify(STuple.Create("Hello", true));
			Verify(STuple.Create("Hello", true, "World"));
			Verify(STuple.Create("Hello", true, "World", 123));
			Verify(STuple.Create("Hello", true, "World", 123, null, DateTime.Now));
			Verify(STuple.Create("Hello", true, "World", 123, null, DateTime.Now, Guid.NewGuid()));
			Verify(STuple.Create("Hello", true, "World", 123, null, DateTime.Now, Guid.NewGuid(), VersionStamp.Incomplete(0x1234)));

			Verify(STuple.Create(STuple.Create(STuple.Create(STuple.Create()))));
			Verify(STuple.Create("Hello", STuple.Create(true, "World", STuple.Create(123, null, DateTime.Now), Guid.NewGuid())));

			Verify((IVarTuple) STuple.Create("Hello", true, "World", 123));
			Verify(STuple.Create((ReadOnlySpan<object>) [ "Hello", true, "World", 123 ]));
			Verify(SlicedTuple.Unpack(TuPack.EncodeKey("Hello", true, "World", 123)));

			Verify(STuple.Create(default(string), default(bool?), default(int?), default(long?), default(TimeSpan?), default(VersionStamp?), default(Uuid128?)));

			Verify(STuple.Create("ThisIsAVeryLongTupleThatWillSurelyNotFitInTheInitialBufferAndRequireABufferRentedFromAPoolAndMultipleCallsToTheTryEncodeMethod", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
		}

		[Test]
		public void Test_FdbValue_FixedSize_LittleEndian_Encoding()
		{
			static void Verify<TValue>(TValue value, Slice expected)
				where TValue : struct, IFdbValue
			{
				Log($"# {value}");
				var slice = value.ToSlice();
				Log($"> [{slice.Count:N0}] {slice}");
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(expected.Count));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.IntegerLittleEndian));
				Log();
			}

			Assert.Multiple(() =>
			{
				// Int32
				Verify(FdbValue.ToFixed32LittleEndian(0x12), Slice.FromFixed32(0x12));
				Verify(FdbValue.ToFixed32LittleEndian(0x1234), Slice.FromFixed32(0x1234));
				Verify(FdbValue.ToFixed32LittleEndian(0x123456), Slice.FromFixed32(0x123456));
				Verify(FdbValue.ToFixed32LittleEndian(0x12345678), Slice.FromFixed32(0x12345678));
				Verify(FdbValue.ToFixed32LittleEndian(-1), Slice.FromFixed32(-1));
				Verify(FdbValue.ToFixed32LittleEndian(-123456), Slice.FromFixed32(-123456));
				Verify(FdbValue.ToFixed32LittleEndian(int.MinValue), Slice.FromFixed32(int.MinValue));

				// UInt32
				Verify(FdbValue.ToFixed32LittleEndian((uint) 0), Slice.FromFixedU32(0));
				Verify(FdbValue.ToFixed32LittleEndian((uint) 0x12), Slice.FromFixedU32(0x12));
				Verify(FdbValue.ToFixed32LittleEndian((uint) 0x1234), Slice.FromFixedU32(0x1234));
				Verify(FdbValue.ToFixed32LittleEndian((uint) 0x123456), Slice.FromFixedU32(0x123456));
				Verify(FdbValue.ToFixed32LittleEndian((uint) 0x12345678), Slice.FromFixedU32(0x12345678));

				// Int64
				Verify(FdbValue.ToFixed64LittleEndian(0), Slice.FromFixed64(0));
				Verify(FdbValue.ToFixed64LittleEndian(0x12), Slice.FromFixed64(0x12));
				Verify(FdbValue.ToFixed64LittleEndian(0x1234), Slice.FromFixed64(0x1234));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456), Slice.FromFixed64(0x123456));
				Verify(FdbValue.ToFixed64LittleEndian(0x12345678), Slice.FromFixed64(0x12345678));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789A), Slice.FromFixed64(0x123456789A));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789ABC), Slice.FromFixed64(0x123456789ABC));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789ABCDE), Slice.FromFixed64(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789ABCDEF0), Slice.FromFixed64(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64LittleEndian(-1), Slice.FromFixed64(-1));
				Verify(FdbValue.ToFixed64LittleEndian(-123456), Slice.FromFixed64(-123456));
				Verify(FdbValue.ToFixed64LittleEndian(int.MinValue), Slice.FromFixed64(int.MinValue));
				Verify(FdbValue.ToFixed64LittleEndian(long.MinValue), Slice.FromFixed64(long.MinValue));

				// UInt64
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0), Slice.FromFixedU64(0));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x12), Slice.FromFixedU64(0x12));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x1234), Slice.FromFixedU64(0x1234));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x123456), Slice.FromFixedU64(0x123456));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x12345678), Slice.FromFixedU64(0x12345678));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x123456789A), Slice.FromFixedU64(0x123456789A));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x123456789ABC), Slice.FromFixedU64(0x123456789ABC));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x123456789ABCDE), Slice.FromFixedU64(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64LittleEndian((ulong) 0x123456789ABCDEF0), Slice.FromFixedU64(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64LittleEndian(ulong.MaxValue), Slice.FromFixedU64(ulong.MaxValue));
			});

		}

		[Test]
		public void Test_FdbValue_FixedSize_BigEndian_Encoding()
		{
			static void Verify<TValue>(TValue value, Slice expected)
				where TValue : struct, IFdbValue
			{
				Log($"# {value}");
				var slice = value.ToSlice();
				Log($"> [{slice.Count:N0}] {slice}");
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(expected.Count));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.IntegerBigEndian));
				Log();
			}

			Assert.Multiple(() =>
			{
				// Int32
				Verify(FdbValue.ToFixed32BigEndian(0x12), Slice.FromFixed32BE(0x12));
				Verify(FdbValue.ToFixed32BigEndian(0x1234), Slice.FromFixed32BE(0x1234));
				Verify(FdbValue.ToFixed32BigEndian(0x123456), Slice.FromFixed32BE(0x123456));
				Verify(FdbValue.ToFixed32BigEndian(0x12345678), Slice.FromFixed32BE(0x12345678));
				Verify(FdbValue.ToFixed32BigEndian(-1), Slice.FromFixed32BE(-1));
				Verify(FdbValue.ToFixed32BigEndian(-123456), Slice.FromFixed32BE(-123456));
				Verify(FdbValue.ToFixed32BigEndian(int.MinValue), Slice.FromFixed32BE(int.MinValue));

				// UInt32
				Verify(FdbValue.ToFixed32BigEndian((uint) 0), Slice.FromFixedU32BE(0));
				Verify(FdbValue.ToFixed32BigEndian((uint) 0x12), Slice.FromFixedU32BE(0x12));
				Verify(FdbValue.ToFixed32BigEndian((uint) 0x1234), Slice.FromFixedU32BE(0x1234));
				Verify(FdbValue.ToFixed32BigEndian((uint) 0x123456), Slice.FromFixedU32BE(0x123456));
				Verify(FdbValue.ToFixed32BigEndian((uint) 0x12345678), Slice.FromFixedU32BE(0x12345678));

				// Int64
				Verify(FdbValue.ToFixed64BigEndian(0), Slice.FromFixed64BE(0));
				Verify(FdbValue.ToFixed64BigEndian(0x12), Slice.FromFixed64BE(0x12));
				Verify(FdbValue.ToFixed64BigEndian(0x1234), Slice.FromFixed64BE(0x1234));
				Verify(FdbValue.ToFixed64BigEndian(0x123456), Slice.FromFixed64BE(0x123456));
				Verify(FdbValue.ToFixed64BigEndian(0x12345678), Slice.FromFixed64BE(0x12345678));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789A), Slice.FromFixed64BE(0x123456789A));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789ABC), Slice.FromFixed64BE(0x123456789ABC));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789ABCDE), Slice.FromFixed64BE(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789ABCDEF0), Slice.FromFixed64BE(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64BigEndian(-1), Slice.FromFixed64BE(-1));
				Verify(FdbValue.ToFixed64BigEndian(-123456), Slice.FromFixed64BE(-123456));
				Verify(FdbValue.ToFixed64BigEndian(int.MinValue), Slice.FromFixed64BE(int.MinValue));
				Verify(FdbValue.ToFixed64BigEndian(long.MinValue), Slice.FromFixed64BE(long.MinValue));

				// UInt64
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0), Slice.FromFixedU64BE(0));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x12), Slice.FromFixedU64BE(0x12));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x1234), Slice.FromFixedU64BE(0x1234));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x123456), Slice.FromFixedU64BE(0x123456));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x12345678), Slice.FromFixedU64BE(0x12345678));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x123456789A), Slice.FromFixedU64BE(0x123456789A));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x123456789ABC), Slice.FromFixedU64BE(0x123456789ABC));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x123456789ABCDE), Slice.FromFixedU64BE(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64BigEndian((ulong) 0x123456789ABCDEF0), Slice.FromFixedU64BE(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64BigEndian(ulong.MaxValue), Slice.FromFixedU64BE(ulong.MaxValue));
			});

		}

		[Test]
		public void Test_FdbValue_Compact_LittleEndian_Encoding()
		{
			static void Verify<TValue>(TValue value, Slice expected)
				where TValue : struct, IFdbValue
			{
				Log($"# {value}");
				var slice = value.ToSlice();
				Log($"> [{slice.Count:N0}] {slice}");
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.IntegerLittleEndian));
				Log();
			}

			Assert.Multiple(() =>
			{
				// Int32
				Verify(FdbValue.ToCompactLittleEndian(0x12), Slice.FromInt32(0x12));
				Verify(FdbValue.ToCompactLittleEndian(0x1234), Slice.FromInt32(0x1234));
				Verify(FdbValue.ToCompactLittleEndian(0x123456), Slice.FromInt32(0x123456));
				Verify(FdbValue.ToCompactLittleEndian(0x12345678), Slice.FromInt32(0x12345678));
				Verify(FdbValue.ToCompactLittleEndian(-1), Slice.FromInt32(-1));
				Verify(FdbValue.ToCompactLittleEndian(-123456), Slice.FromInt32(-123456));
				Verify(FdbValue.ToCompactLittleEndian(int.MinValue), Slice.FromInt32(int.MinValue));

				// UInt32
				Verify(FdbValue.ToCompactLittleEndian((uint) 0), Slice.FromUInt32(0));
				Verify(FdbValue.ToCompactLittleEndian((uint) 0x12), Slice.FromUInt32(0x12));
				Verify(FdbValue.ToCompactLittleEndian((uint) 0x1234), Slice.FromUInt32(0x1234));
				Verify(FdbValue.ToCompactLittleEndian((uint) 0x123456), Slice.FromUInt32(0x123456));
				Verify(FdbValue.ToCompactLittleEndian((uint) 0x12345678), Slice.FromUInt32(0x12345678));
				Verify(FdbValue.ToCompactLittleEndian(uint.MaxValue), Slice.FromUInt32(uint.MaxValue));

				// Int64
				Verify(FdbValue.ToCompactLittleEndian((long) 0), Slice.FromInt64(0));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x12), Slice.FromInt64(0x12));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x1234), Slice.FromInt64(0x1234));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x123456), Slice.FromInt64(0x123456));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x12345678), Slice.FromInt64(0x12345678));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x123456789A), Slice.FromInt64(0x123456789A));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x123456789ABC), Slice.FromInt64(0x123456789ABC));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x123456789ABCDE), Slice.FromInt64(0x123456789ABCDE));
				Verify(FdbValue.ToCompactLittleEndian((long) 0x123456789ABCDEF0), Slice.FromInt64(0x123456789ABCDEF0));
				Verify(FdbValue.ToCompactLittleEndian((long) -1), Slice.FromInt64(-1));
				Verify(FdbValue.ToCompactLittleEndian((long) -123456), Slice.FromInt64(-123456));
				Verify(FdbValue.ToCompactLittleEndian((long) int.MinValue), Slice.FromInt64(int.MinValue));
				Verify(FdbValue.ToCompactLittleEndian(long.MinValue), Slice.FromInt64(long.MinValue));

				// UInt64
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0), Slice.FromUInt64(0));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x12), Slice.FromUInt64(0x12));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x1234), Slice.FromUInt64(0x1234));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x123456), Slice.FromUInt64(0x123456));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x12345678), Slice.FromUInt64(0x12345678));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x123456789A), Slice.FromUInt64(0x123456789A));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x123456789ABC), Slice.FromUInt64(0x123456789ABC));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x123456789ABCDE), Slice.FromUInt64(0x123456789ABCDE));
				Verify(FdbValue.ToCompactLittleEndian((ulong) 0x123456789ABCDEF0), Slice.FromUInt64(0x123456789ABCDEF0));
				Verify(FdbValue.ToCompactLittleEndian(ulong.MaxValue), Slice.FromUInt64(ulong.MaxValue));
			});

		}

		[Test]
		public void Test_FdbValue_Compact_BigEndian_Encoding()
		{
			static void Verify<TValue>(TValue value, Slice expected)
				where TValue : struct, IFdbValue
			{
				Log($"# {value}");
				var slice = value.ToSlice();
				Log($"> [{slice.Count:N0}] {slice}");
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.IntegerBigEndian));
				Log();
			}

			Assert.Multiple(() =>
			{
				// Int32
				Verify(FdbValue.ToCompactBigEndian(0x12), Slice.FromInt32BE(0x12));
				Verify(FdbValue.ToCompactBigEndian(0x1234), Slice.FromInt32BE(0x1234));
				Verify(FdbValue.ToCompactBigEndian(0x123456), Slice.FromInt32BE(0x123456));
				Verify(FdbValue.ToCompactBigEndian(0x12345678), Slice.FromInt32BE(0x12345678));
				Verify(FdbValue.ToCompactBigEndian(-1), Slice.FromInt32BE(-1));
				Verify(FdbValue.ToCompactBigEndian(-123456), Slice.FromInt32BE(-123456));
				Verify(FdbValue.ToCompactBigEndian(int.MinValue), Slice.FromInt32BE(int.MinValue));

				// UInt32
				Verify(FdbValue.ToCompactBigEndian((uint) 0), Slice.FromUInt32BE(0));
				Verify(FdbValue.ToCompactBigEndian((uint) 0x12), Slice.FromUInt32BE(0x12));
				Verify(FdbValue.ToCompactBigEndian((uint) 0x1234), Slice.FromUInt32BE(0x1234));
				Verify(FdbValue.ToCompactBigEndian((uint) 0x123456), Slice.FromUInt32BE(0x123456));
				Verify(FdbValue.ToCompactBigEndian((uint) 0x12345678), Slice.FromUInt32BE(0x12345678));
				Verify(FdbValue.ToCompactBigEndian(uint.MaxValue), Slice.FromUInt32(uint.MaxValue));

				// Int64
				Verify(FdbValue.ToCompactBigEndian((long) 0), Slice.FromInt64BE(0));
				Verify(FdbValue.ToCompactBigEndian((long) 0x12), Slice.FromInt64BE(0x12));
				Verify(FdbValue.ToCompactBigEndian((long) 0x1234), Slice.FromInt64BE(0x1234));
				Verify(FdbValue.ToCompactBigEndian((long) 0x123456), Slice.FromInt64BE(0x123456));
				Verify(FdbValue.ToCompactBigEndian((long) 0x12345678), Slice.FromInt64BE(0x12345678));
				Verify(FdbValue.ToCompactBigEndian((long) 0x123456789A), Slice.FromInt64BE(0x123456789A));
				Verify(FdbValue.ToCompactBigEndian((long) 0x123456789ABC), Slice.FromInt64BE(0x123456789ABC));
				Verify(FdbValue.ToCompactBigEndian((long) 0x123456789ABCDE), Slice.FromInt64BE(0x123456789ABCDE));
				Verify(FdbValue.ToCompactBigEndian((long) 0x123456789ABCDEF0), Slice.FromInt64BE(0x123456789ABCDEF0));
				Verify(FdbValue.ToCompactBigEndian((long) -1), Slice.FromInt64BE(-1));
				Verify(FdbValue.ToCompactBigEndian((long) -123456), Slice.FromInt64BE(-123456));
				Verify(FdbValue.ToCompactBigEndian((long) int.MinValue), Slice.FromInt64BE(int.MinValue));
				Verify(FdbValue.ToCompactBigEndian((long) long.MinValue), Slice.FromInt64BE(long.MinValue));

				// UInt64
				Verify(FdbValue.ToCompactBigEndian((ulong) 0), Slice.FromUInt64BE(0));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x12), Slice.FromUInt64BE(0x12));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x1234), Slice.FromUInt64BE(0x1234));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x123456), Slice.FromUInt64BE(0x123456));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x12345678), Slice.FromUInt64BE(0x12345678));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x123456789A), Slice.FromUInt64BE(0x123456789A));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x123456789ABC), Slice.FromUInt64BE(0x123456789ABC));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x123456789ABCDE), Slice.FromUInt64BE(0x123456789ABCDE));
				Verify(FdbValue.ToCompactBigEndian((ulong) 0x123456789ABCDEF0), Slice.FromUInt64BE(0x123456789ABCDEF0));
				Verify(FdbValue.ToCompactBigEndian(ulong.MaxValue), Slice.FromUInt64BE(ulong.MaxValue));
			});

		}

		public sealed record Person
		{
			[JsonProperty("firstName")]
			public required string FirstName { get; init; }

			[JsonProperty("familyName")]
			public required string FamilyName { get; init; }
		}

		[Test]
		public void Test_FdbValue_Json_Encoding()
		{
			static void Verify<TValue>(TValue value, Slice expected)
				where TValue : struct, IFdbValue
			{
				Log($"# {value}");
				var slice = value.ToSlice();
				Log($"> [{slice.Count:N0}] {slice}");
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}

				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.False.WithOutput(size).Zero);
				Assert.That(value.GetTypeHint(), Is.EqualTo(FdbValueTypeHint.Json));
				Log();
			}

			Assert.Multiple(() =>
			{
				Verify(FdbValue.ToJson(JsonNull.Null), Slice.FromString("null"));
				Verify(FdbValue.ToJson(123), Slice.FromString("123"));
				Verify(FdbValue.ToJson("Hello, World!"), Slice.FromString("\"Hello, World!\""));
				Verify(FdbValue.ToJson(JsonArray.Create([ 123, 456, 789 ])), Slice.FromString("[123,456,789]"));
				Verify(FdbValue.ToJson(JsonObject.Create([ ("hello", "world"), ("foo", 123), ])), Slice.FromString("{\"hello\":\"world\",\"foo\":123}"));
			});

			Assert.Multiple(() =>
			{
				Verify(FdbValue.ToJson<int>(123), Slice.FromString("123"));
				Verify(FdbValue.ToJson<string>("Hello, World!"), Slice.FromString("\"Hello, World!\""));
				Verify(FdbValue.ToJson<int[]>([ 123, 456, 789 ]), Slice.FromString("[123,456,789]"));
				Verify(FdbValue.ToJson<int[]>([ 123, 456, 789 ], settings: CrystalJsonSettings.Json), Slice.FromString("[ 123, 456, 789 ]"));
				Verify(FdbValue.ToJson<Person>(new() { FirstName = "John", FamilyName = "Wick" }), Slice.FromString("{\"firstName\":\"John\",\"familyName\":\"Wick\"}"));
				Verify(FdbValue.ToJson<Person>(new() { FirstName = "John", FamilyName = "Wick" }, settings: CrystalJsonSettings.Json), Slice.FromString("{ \"firstName\": \"John\", \"familyName\": \"Wick\" }"));
			});

		}

	}

}
