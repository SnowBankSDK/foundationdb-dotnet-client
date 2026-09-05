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

// ReSharper disable AccessToModifiedClosure
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable InlineOutVariableDeclaration
// ReSharper disable StringLiteralTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable UseUtf8StringLiteral
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace SnowBank.Data.Tuples.Tests
{
	using System.Net;
	using System.Numerics;

	using SnowBank.Buffers;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime;
	using SnowBank.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class TuPackFacts : SimpleTest
	{

		private static void Dump(string label, Slice slice)
		{
			if (slice.IsNull)
			{
				Log($"{label}: <null>");
			}
			else if (slice.IsEmpty)
			{
				Log($"{label}: [ ]");
			}
			else
			{
				Log($"{label}: [{slice.Count}] {slice.PrettyPrint(128)}");
				Log(HexaDump.Format(slice, HexaDump.Options.NoFooter | HexaDump.Options.NoHeader | HexaDump.Options.OmitLastNewLine, indent: 1));
			}
		}

		private static void Dump(string label, IVarTuple? tuple)
		{
			if (tuple is null)
			{
				Log($"{label}: <null>");
			}
			else
			{
				Log($"{label}: [{tuple.Count}] {tuple.ToString()}, {tuple.GetType().GetFriendlyName()}");
				for (int i = 0; i < tuple.Count; i++)
				{
					Log($"- t[{i}] = {tuple[i]}");
				}
			}
		}

		private static void Dump(string label, SlicedTuple? tuple)
		{
			if (tuple is null)
			{
				Log($"{label}: <null>");
			}
			else
			{
				Log($"{label}: [{tuple.Count}] {tuple.ToString()}, {nameof(SlicedTuple)}");
				for (int i = 0; i < tuple.Count; i++)
				{
					Log($"- t[{i}] = ({tuple.GetElementType(i)}) {STuple.Formatter.Stringify(tuple[i])} : [ {tuple.GetSlice(i):X} ]");
				}
			}
		}

		private static void Dump(string label, SpanTuple tuple)
		{
			Log($"{label}: [{tuple.Count}] {tuple.ToString()}, {nameof(SpanTuple)}");
			for (int i = 0; i < tuple.Count; i++)
			{
				Log($"- t[{i}] = ({tuple.GetElementType(i)}) {STuple.Formatter.Stringify(tuple[i])} : [ {tuple.GetSlice(i):X} ]");
			}
		}

		#region Serialization...

		[Test]
		public void Test_TuplePack_Serialize_Bytes()
		{
			// Byte arrays are stored with prefix '01' followed by the bytes, and terminated by '00'. All occurrences of '00' in the byte array are escaped with '00 FF'
			// - Best case:  packed_size = 2 + array_len
			// - Worst case: packed_size = 2 + array_len * 2

			// byte[]
			Assert.That(TuPack.EncodeKey(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 })).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));

			// \x00 should be escaped as \x00\xFF
			Assert.That(TuPack.EncodeKey((byte[]) [ 0x00, 0x42 ]).ToString(), Is.EqualTo("<01><00><FF>B<00>"));
			Assert.That(TuPack.EncodeKey((byte[]) [ 0x42, 0x00 ]).ToString(), Is.EqualTo("<01>B<00><FF><00>"));
			Assert.That(TuPack.EncodeKey((byte[]) [ 0x42, 0x00, 0x42 ]).ToString(), Is.EqualTo("<01>B<00><FF>B<00>"));
			Assert.That(TuPack.EncodeKey((byte[]) [ 0x42, 0x00, 0x00, 0x42 ]).ToString(), Is.EqualTo("<01>B<00><FF><00><FF>B<00>"));

			// Slice
			Assert.That(TuPack.EncodeKey(Slice.FromBytes([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 ])).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Slice.FromBytes([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]))).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));

			// Span-like
			Assert.That(TuPack.EncodeKey(Slice.FromBytes([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 ]).Memory).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Slice.FromBytes([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]).Memory)).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			Assert.That(TuPack.EncodeKey(((byte[]) [ 0x00, 0x42 ]).AsMemory()).ToString(), Is.EqualTo("<01><00><FF>B<00>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Bytes()
		{
			IVarTuple t;

			t = TuPack.Unpack(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }));
			Assert.That(t.Get<Slice>(0).ToHexString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF"));

			t = TuPack.Unpack(Slice.Unescape("<01><42><00><FF><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x42, 0x00 }));
			Assert.That(t.Get<Slice>(0).ToHexString(' '), Is.EqualTo("42 00"));

			t = TuPack.Unpack(Slice.Unescape("<01><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x00, 0x42 }));
			Assert.That(t.Get<Slice>(0).ToHexString(' '), Is.EqualTo("00 42"));

			t = TuPack.Unpack(Slice.Unescape("<01><42><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x42, 0x00, 0x42 }));
			Assert.That(t.Get<Slice>(0).ToHexString(' '), Is.EqualTo("42 00 42"));

			t = TuPack.Unpack(Slice.Unescape("<01><42><00><FF><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x42, 0x00, 0x00, 0x42 }));
			Assert.That(t.Get<Slice>(0).ToHexString(' '), Is.EqualTo("42 00 00 42"));

			Assert.That(TuPack.DecodeKey<byte[]>(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>")), Is.EqualTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }));
			Assert.That(TuPack.DecodeKey<Slice>(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>")), Is.EqualTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }.AsSlice()));
		}

		[Test]
		public void Test_TuplePack_Serialize_Unicode_Strings()
		{
			// Unicode strings are stored with prefix '02' followed by the utf8 bytes, and terminated by '00'. All occurrences of '00' in the UTF8 bytes are escaped with '00 FF'

			Assert.Multiple(() =>
			{
				// simple string
				Assert.That(TuPack.EncodeKey("hello").ToString(), Is.EqualTo("<02>hello<00>"));
				Assert.That(TuPack.EncodeKey("hello, world!").ToString(), Is.EqualTo("<02>hello, world!<00>"));

				Assert.That(TuPack.Pack(ValueTuple.Create("hello")).ToString(), Is.EqualTo("<02>hello<00>"));
				Assert.That(TuPack.Pack(ValueTuple.Create("hello, world!")).ToString(), Is.EqualTo("<02>hello, world!<00>"));

				// empty
				Assert.That(TuPack.EncodeKey(string.Empty).ToString(), Is.EqualTo("<02><00>"));

				// null
				Assert.That(TuPack.EncodeKey(default(string)).ToString(), Is.EqualTo("<00>"));

				// Unicode Latin
				Assert.That(
					TuPack.EncodeKey("Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis").ToString(),
					Is.EqualTo("<02>Voix ambigu<C3><AB> d<E2><80><99>un c<C5><93>ur qui, au z<C3><A9>phyr, pr<C3><A9>f<C3><A8>re les jattes de kiwis<00>")
				);
				Assert.That(
					TuPack.EncodeKey("Fünf jugendliche Wölfe zählten über große Äpfel und böse Kühe").ToString(),
					Is.EqualTo("<02>F<C3><BC>nf jugendliche W<C3><B6>lfe z<C3><A4>hlten <C3><BC>ber gro<C3><9F>e <C3><84>pfel und b<C3><B6>se K<C3><BC>he<00>")
				);
				Assert.That(
					TuPack.EncodeKey("È un viaggio strano, ma l’arte è l’unica vera magia.").ToString(),
					Is.EqualTo("<02><C3><88> un viaggio strano, ma l<E2><80><99>arte <C3><A8> l<E2><80><99>unica vera magia.<00>")
				);
				Assert.That(
					TuPack.EncodeKey("¿Qué fácil es ver la acción en la exposición?").ToString(),
					Is.EqualTo("<02><C2><BF>Qu<C3><A9> f<C3><A1>cil es ver la acci<C3><B3>n en la exposici<C3><B3>n?<00>")
				);

				// Unicode Non-Latin
				Assert.That(TuPack.EncodeKey("\u3053\u3093\u306b\u3061\u306f\u4e16\u754c").ToString(), Is.EqualTo("<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>"));
				Assert.That(TuPack.EncodeKey("\u4e2d\u6587").ToString(), Is.EqualTo("<02><E4><B8><AD><E6><96><87><00>"));
				Assert.That(TuPack.EncodeKey("\u0395\u03bb\u03bb\u03b7\u03bd\u03b9\u03ba\u03ac").ToString(), Is.EqualTo("<02><CE><95><CE><BB><CE><BB><CE><B7><CE><BD><CE><B9><CE><BA><CE><AC><00>"));
				Assert.That(TuPack.EncodeKey("\u0627\u064e\u0644\u0652\u0639\u064e\u0631\u064e\u0628\u0650\u064a\u064e\u0651\u0629\u064f").ToString(), Is.EqualTo("<02><D8><A7><D9><8E><D9><84><D9><92><D8><B9><D9><8E><D8><B1><D9><8E><D8><A8><D9><90><D9><8A><D9><8E><D9><91><D8><A9><D9><8F><00>"));
				Assert.That(TuPack.EncodeKey("\u0e20\u0e32\u0e29\u0e32\u0e44\u0e17\u0e22").ToString(), Is.EqualTo("<02><E0><B8><A0><E0><B8><B2><E0><B8><A9><E0><B8><B2><E0><B9><84><E0><B8><97><E0><B8><A2><00>"));
				Assert.That(TuPack.EncodeKey("\u0421\u043B\u0430\u0432\u0430\u0020\u0423\u043A\u0440\u0430\u0457\u043D\u0456").ToString(), Is.EqualTo("<02><D0><A1><D0><BB><D0><B0><D0><B2><D0><B0> <D0><A3><D0><BA><D1><80><D0><B0><D1><97><D0><BD><D1><96><00>"));

				// Hi/Lo Surrogates
				Assert.That(TuPack.EncodeKey("🐶").ToString(), Is.EqualTo("<02><F0><9F><90><B6><00>"));
				Assert.That(TuPack.EncodeKey("🐉🔥").ToString(), Is.EqualTo("<02><F0><9F><90><89><F0><9F><94><A5><00>"));
				Assert.That(TuPack.EncodeKey("\U0010ffff").ToString(), Is.EqualTo("<02><F4><8F><BF><BF><00>"));

				// ReadOnlyMemory<char>
				Assert.That(TuPack.EncodeKey(default(ReadOnlyMemory<char>)).ToString(), Is.EqualTo("<02><00>"));
				Assert.That(TuPack.EncodeKey("hello, world!".AsMemory()).ToString(), Is.EqualTo("<02>hello, world!<00>"));
				Assert.That(
					TuPack.EncodeKey("Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis".AsMemory()).ToString(),
					Is.EqualTo("<02>Voix ambigu<C3><AB> d<E2><80><99>un c<C5><93>ur qui, au z<C3><A9>phyr, pr<C3><A9>f<C3><A8>re les jattes de kiwis<00>")
				);
				Assert.That(TuPack.EncodeKey("🐶".AsMemory()).ToString(), Is.EqualTo("<02><F0><9F><90><B6><00>"));
			});
		}

		[Test]
		public void Test_TuplePack_Deserialize_Unicode_Strings()
		{
			IVarTuple t;

			// simple string
			t = TuPack.Unpack(Slice.Unescape("<02>hello world<00>"));
			Assert.That(t.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t[0], Is.EqualTo("hello world"));

			// empty
			t = TuPack.Unpack(Slice.Unescape("<02><00>"));
			Assert.That(t.Get<string>(0), Is.EqualTo(String.Empty));
			Assert.That(t[0], Is.EqualTo(String.Empty));

			// null
			t = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(t.Get<string>(0), Is.EqualTo(default(string)));
			Assert.That(t[0], Is.Null);

			// unicode
			t = TuPack.Unpack(Slice.Unescape("<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>"));
			// note: Encoding.UTF8.GetString({ e3 81 93 e3 82 93 e3 81 ab e3 81 a1 e3 81 af e4 b8 96 e7 95 8c }) => "こんにちは世界"
			Assert.That(t.Get<string>(0), Is.EqualTo("こんにちは世界"));
			Assert.That(t[0], Is.EqualTo("こんにちは世界"));

			// ASCII
			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02>hello<00>")), Is.EqualTo("hello"));
			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02>hello, world!<00>")), Is.EqualTo("hello, world!"));

			// Unicode Latin
			Assert.That(
				TuPack.DecodeKey<string>(Slice.Unescape("<02>Voix ambigu<C3><AB> d<E2><80><99>un c<C5><93>ur qui, au z<C3><A9>phyr, pr<C3><A9>f<C3><A8>re les jattes de kiwis<00>")),
				Is.EqualTo("Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis")
			);
			Assert.That(
				TuPack.DecodeKey<string>(Slice.Unescape("<02>F<C3><BC>nf jugendliche W<C3><B6>lfe z<C3><A4>hlten <C3><BC>ber gro<C3><9F>e <C3><84>pfel und b<C3><B6>se K<C3><BC>he<00>")),
				Is.EqualTo("Fünf jugendliche Wölfe zählten über große Äpfel und böse Kühe")
			);
			Assert.That(
				TuPack.DecodeKey<string>(Slice.Unescape("<02><C3><88> un viaggio strano, ma l<E2><80><99>arte <C3><A8> l<E2><80><99>unica vera magia.<00>")),
				Is.EqualTo("È un viaggio strano, ma l’arte è l’unica vera magia.")
			);
			Assert.That(
				TuPack.DecodeKey<string>(Slice.Unescape("<02><C2><BF>Qu<C3><A9> f<C3><A1>cil es ver la acci<C3><B3>n en la exposici<C3><B3>n?<00>")),
				Is.EqualTo("¿Qué fácil es ver la acción en la exposición?")
			);

			// Unicode Non-Latin
			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02><E4><B8><AD><E6><96><87><00>")), Is.EqualTo("中文"));

			// Hi/Lo Surrogates
			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02><F0><9F><90><B6><00>")), Is.EqualTo("🐶"));
			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02><F0><9F><90><89><F0><9F><94><A5><00>")), Is.EqualTo("🐉🔥"));

			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02><F4><8F><BF><BF><00>")), Is.EqualTo("\U0010ffff"));

		}

		[Test]
		public void Test_TuplePack_Serialize_Guids()
		{
			// 128-bit Guids are stored with prefix '30' followed by 16 bytes formatted according to RFC 4122

			// System.Guid are stored in Little-Endian, but RFC 4122's UUIDs are stored in Big Endian, so per convention we will swap them

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			Assert.That(TuPack.EncodeKey(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"))).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			Assert.That(TuPack.EncodeKey(Guid.Empty).ToString(), Is.EqualTo("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Guids()
		{
			// 128-bit Guids are stored with prefix '30' followed by 16 bytes
			// we also accept byte arrays (prefix '01') if they are of length 16

			IVarTuple packed;

			packed = TuPack.Unpack(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = TuPack.Unpack(Slice.Unescape("<30><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			Assert.That(packed[0], Is.EqualTo(Guid.Empty));

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>03020100-0504-0706-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a GUID

			// null maps to Guid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			//note: t[0] returns null, not a GUID

			Assert.That(TuPack.DecodeKey<Guid>(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>")), Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid128s()
		{
			// UUID128s are stored with prefix '30' followed by 16 bytes formatted according to RFC 4122

			// note: new Uuid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			Assert.That(TuPack.EncodeKey(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"))).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			Assert.That(TuPack.EncodeKey(Uuid128.Empty).ToString(), Is.EqualTo("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid128s()
		{
			// UUID128s are stored with prefix '30' followed by 16 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 16

			IVarTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = TuPack.Unpack(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = TuPack.Unpack(Slice.Unescape("<30><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid128.Empty));

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>00010203-0405-0607-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Empty));
			//note: t[0] returns null, not a UUID

			Assert.That(TuPack.DecodeKey<Uuid128>(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>")), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid96s()
		{
			// Uuid96 instances are stored with prefix 0x32 followed by 12 bytes, and should preserve ordering

			Slice packed;

			packed = TuPack.EncodeKey(Uuid96.Parse("00010203-04050607-08090A0B"));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("33 00 01 02 03 04 05 06 07 08 09 0A 0B"));

			packed = TuPack.EncodeKey(Uuid96.Parse("01234567-89ABCDEF-55AA33CC"));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("33 01 23 45 67 89 AB CD EF 55 AA 33 CC"));

			packed = TuPack.EncodeKey(Uuid96.Empty);
			Assert.That(packed.ToHexString(' '), Is.EqualTo("33 00 00 00 00 00 00 00 00 00 00 00 00"));

			packed = TuPack.EncodeKey(Uuid96.FromUpper32Lower64(0x12345678, 0xBADC0FFEE0DDF00Dul));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("33 12 34 56 78 BA DC 0F FE E0 DD F0 0D"));

			packed = TuPack.EncodeKey(Uuid96.FromUpper32Lower64(0xFFFFFFFF, 0xDEADBEEFL));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("33 FF FF FF FF 00 00 00 00 DE AD BE EF"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid96s()
		{
			// Uuid96 instances are stored with prefix 0x33 followed by 12 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 12, and unicode strings (prefix '02')

			IVarTuple packed;

			packed = TuPack.Unpack(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 55 AA 33 CC"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));

			packed = TuPack.Unpack(Slice.FromHexa("33 00 00 00 00 00 00 00 00 00 00 00 00"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Empty));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid96.Empty));

			// 8 bytes
			packed = TuPack.Unpack(Slice.FromHexa("01 01 23 45 67 89 ab cd ef 55 AA 33 CC 00"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>01234567-89abcdef-55aa33cc<00>"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89abcdef-55aa33cc")));
			packed = TuPack.Unpack(Slice.Unescape("<02>0123456789abcdef55aa33cc<00>"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89abcdef-55aa33cc")));

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid80s()
		{
			// Uuid80 instances are stored with prefix 0x32 followed by 10 bytes, and should preserve ordering

			Slice packed;

			packed = TuPack.EncodeKey(Uuid80.Parse("0001-02030405-06070809"));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("32 00 01 02 03 04 05 06 07 08 09"));

			packed = TuPack.EncodeKey(Uuid80.Parse("0123-456789AB-CDEF55AA"));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("32 01 23 45 67 89 AB CD EF 55 AA"));

			packed = TuPack.EncodeKey(Uuid80.Empty);
			Assert.That(packed.ToHexString(' '), Is.EqualTo("32 00 00 00 00 00 00 00 00 00 00"));

			packed = TuPack.EncodeKey(new Uuid80(0x1234, 0xBADC0FFEE0DDF00DUL));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("32 12 34 BA DC 0F FE E0 DD F0 0D"));

			packed = TuPack.EncodeKey(new Uuid80(0xFFFF, 0xDEADBEEFL));
			Assert.That(packed.ToHexString(' '), Is.EqualTo("32 FF FF 00 00 00 00 DE AD BE EF"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid80s()
		{
			// Uuid80 instances are stored with prefix '31' followed by 8 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 8, and unicode strings (prefix '02')

			IVarTuple packed;

			packed = TuPack.Unpack(Slice.FromHexa("32 01 23 45 67 89 AB CD EF 55 AA"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789AB-CDEF55AA")));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid80.Parse("0123-456789AB-CDEF55AA")));

			packed = TuPack.Unpack(Slice.FromHexa("32 00 00 00 00 00 00 00 00 00 00"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Empty));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid80.Empty));

			// 8 bytes
			packed = TuPack.Unpack(Slice.FromHexa("01 01 23 45 67 89 ab cd ef 55 AA 00"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789AB-CDEF55AA")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>0123-456789ab-cdef55aa<00>"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789ab-cdef55aa")));
			packed = TuPack.Unpack(Slice.Unescape("<02>0123456789abcdef55aa<00>"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789ab-cdef55aa")));

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid64s()
		{
			// UUID64s are stored with prefix '31' followed by 8 bytes formatted according to RFC 4122

			// note: new Uuid(bytes from 0 to 7) => "00010203-04050607";
			Assert.That(TuPack.EncodeKey(Uuid64.Parse("00010203-04050607")).ToString(), Is.EqualTo("1<00><01><02><03><04><05><06><07>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Uuid64.Parse("00010203-04050607"))).ToString(), Is.EqualTo("1<00><01><02><03><04><05><06><07>"));

			Assert.That(TuPack.EncodeKey(Uuid64.Parse("01234567-89ABCDEF")).ToString(), Is.EqualTo("1<01>#Eg<89><AB><CD><EF>"));
			Assert.That(TuPack.EncodeKey(Uuid64.Empty).ToString(), Is.EqualTo("1<00><00><00><00><00><00><00><00>"));
			Assert.That(TuPack.EncodeKey(new Uuid64(0xBADC0FFEE0DDF00DUL)).ToString(), Is.EqualTo("1<BA><DC><0F><FE><E0><DD><F0><0D>"));
			Assert.That(TuPack.EncodeKey(new Uuid64(0xDEADBEEFL)).ToString(), Is.EqualTo("1<00><00><00><00><DE><AD><BE><EF>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid64s()
		{
			// UUID64s are stored with prefix '31' followed by 8 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 8, and unicode strings (prefix '02')

			IVarTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = TuPack.Unpack(Slice.Unescape("<31><01><23><45><67><89><AB><CD><EF>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			Assert.That(packed[0], Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));

			packed = TuPack.Unpack(Slice.Unescape("<31><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid64.Empty));

			// 8 bytes
			packed = TuPack.Unpack(Slice.Unescape("<01><01><23><45><67><89><ab><cd><ef><00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>01234567-89abcdef<00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Empty));
			//note: t[0] returns null, not a UUID

			Assert.That(TuPack.DecodeKey<Uuid64>(Slice.Unescape("<31><01><23><45><67><89><AB><CD><EF>")), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
		}

		[Test]
		public void Test_TuplePack_Serialize_Integers()
		{
			// Positive integers are stored with a variable-length encoding.
			// - The prefix is 0x14 + the minimum number of bytes to encode the integer, from 0 to 8, so valid prefixes range from 0x14 to 0x1C
			// - The bytes are stored in High-Endian (ie: the upper bits first)
			// Examples:
			// - 0 => <14>
			// - 1..255 => <15><##>
			// - 256..65535 .. => <16><HH><LL>
			// - ulong.MaxValue => <1C><FF><FF><FF><FF><FF><FF><FF><FF>

			Assert.That(TuPack.EncodeKey(0).ToHexString(' '), Is.EqualTo("14"));
			Assert.That(TuPack.EncodeKey(1).ToHexString(' '), Is.EqualTo("15 01"));
			Assert.That(TuPack.EncodeKey(255).ToHexString(' '), Is.EqualTo("15 FF"));
			Assert.That(TuPack.EncodeKey(256).ToHexString(' '), Is.EqualTo("16 01 00"));
			Assert.That(TuPack.EncodeKey(65535).ToHexString(' '), Is.EqualTo("16 FF FF"));
			Assert.That(TuPack.EncodeKey(65536).ToHexString(' '), Is.EqualTo("17 01 00 00"));
			Assert.That(TuPack.EncodeKey(int.MaxValue).ToHexString(' '), Is.EqualTo("18 7F FF FF FF"));
			Assert.That(TuPack.EncodeKey(long.MaxValue).ToHexString(' '), Is.EqualTo("1C 7F FF FF FF FF FF FF FF"));
			Assert.That(TuPack.EncodeKey(59604644783353249UL).ToHexString(' '), Is.EqualTo("1B D3 C2 1B CF 46 6D A1"));
			Assert.That(TuPack.EncodeKey(ulong.MaxValue).ToHexString(' '), Is.EqualTo("1C FF FF FF FF FF FF FF FF"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Integers()
		{
			static void Verify(string encoded, long value)
			{
				var slice = Slice.Unescape(encoded);
				Assert.That(TuplePackers.DeserializeBoxed(slice), Is.EqualTo(value), $"DeserializeBoxed({encoded})");

				// int64
				Assert.That(TuplePackers.DeserializeInt64(slice.Span), Is.EqualTo(value), $"DeserializeInt64({encoded})");
				Assert.That(TuplePacker<long>.Deserialize(slice), Is.EqualTo(value), $"Deserialize<long>({encoded})");

				// uint64
				if (value >= 0)
				{
					Assert.That(TuplePackers.DeserializeUInt64(slice.Span), Is.EqualTo((ulong) value), $"DeserializeUInt64({encoded})");
					Assert.That(TuplePacker<ulong>.Deserialize(slice), Is.EqualTo((ulong) value), $"Deserialize<ulong>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeUInt64(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeUInt64({encoded})");
				}

				// int32
				if (value <= int.MaxValue && value >= int.MinValue)
				{
					Assert.That(TuplePackers.DeserializeInt32(slice.Span), Is.EqualTo((int) value), $"DeserializeInt32({encoded})");
					Assert.That(TuplePacker<long>.Deserialize(slice), Is.EqualTo((int) value), $"Deserialize<int>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeInt32(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeInt32({encoded})");
				}

				// uint32
				if (value <= uint.MaxValue && value >= 0)
				{
					Assert.That(TuplePackers.DeserializeUInt32(slice.Span), Is.EqualTo((uint) value), $"DeserializeUInt32({encoded})");
					Assert.That(TuplePacker<uint>.Deserialize(slice), Is.EqualTo((uint) value), $"Deserialize<uint>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeUInt32(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeUInt32({encoded})");
				}

				// int16
				if (value <= short.MaxValue && value >= short.MinValue)
				{
					Assert.That(TuplePackers.DeserializeInt16(slice.Span), Is.EqualTo((short) value), $"DeserializeInt16({encoded})");
					Assert.That(TuplePacker<short>.Deserialize(slice), Is.EqualTo((short) value), $"Deserialize<short>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeInt16(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeInt16({encoded})");
				}

				// uint16
				if (value <= ushort.MaxValue && value >= 0)
				{
					Assert.That(TuplePackers.DeserializeUInt16(slice.Span), Is.EqualTo((ushort) value), $"DeserializeUInt16({encoded})");
					Assert.That(TuplePacker<ushort>.Deserialize(slice), Is.EqualTo((ushort) value), $"Deserialize<ushort>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeUInt16(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeUInt16({encoded})");
				}

				// sbyte
				if (value <= sbyte.MaxValue && value >= sbyte.MinValue)
				{
					Assert.That(TuplePackers.DeserializeSByte(slice.Span), Is.EqualTo((sbyte) value), $"DeserializeSByte({encoded})");
					Assert.That(TuplePacker<sbyte>.Deserialize(slice), Is.EqualTo((sbyte) value), $"Deserialize<sbyte>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeSByte(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeSByte({encoded})");
				}

				// byte
				if (value <= 255 && value >= 0)
				{
					Assert.That(TuplePackers.DeserializeByte(slice.Span), Is.EqualTo((byte) value), $"DeserializeByte({encoded})");
					Assert.That(TuplePacker<byte>.Deserialize(slice), Is.EqualTo((byte) value), $"Deserialize<byte>({encoded})");
				}
				else
				{
					Assert.That(() => TuplePackers.DeserializeByte(slice.Span), Throws.InstanceOf<OverflowException>(), $"DeserializeByte({encoded})");
				}
			}

			Verify("<14>", 0);
			Verify("<15>{", 123);
			Verify("<15><80>", 128);
			Verify("<15><FF>", 255);
			Verify("<16><01><00>", 256);
			Verify("<16><04><D2>", 1234);
			Verify("<16><80><00>", 32768);
			Verify("<16><FF><FF>", 65535);
			Verify("<17><01><00><00>", 65536);
			Verify("<13><FE>", -1);
			Verify("<13><00>", -255);
			Verify("<12><FE><FF>", -256);
			Verify("<12><00><00>", -65535);
			Verify("<11><FE><FF><FF>", -65536);
			Verify("<18><7F><FF><FF><FF>", int.MaxValue);
			Verify("<10><7F><FF><FF><FF>", int.MinValue);
			Verify("<1C><7F><FF><FF><FF><FF><FF><FF><FF>", long.MaxValue);
			Verify("<0C><7F><FF><FF><FF><FF><FF><FF><FF>", long.MinValue);
		}

		[Test]
		public void Test_TuplePack_Deserialize_WellKnownValues_Into_Singletons()
		{
			// some very frequent values, such as booleans and very small integers, should returned pre-allocated singletons instead of using boxing
			// ex: (false, false, false) should return 3 times the same singleton

			static void Verify<T>(T value)
			{
				var t = (value, value, value);
				var packed = TuPack.Pack(t);

				// they should all be the same objects when repeated in a single tuple
				var slicedTuple = TuPack.Unpack(packed);
				Assert.That(slicedTuple, Has.Count.EqualTo(3));
				Assert.That(slicedTuple, Is.All.EqualTo(value));

				var singleton = slicedTuple[0];
				Assert.That(singleton, Is.EqualTo(value));
				Assert.That(slicedTuple[1], Is.SameAs(singleton));
				Assert.That(slicedTuple[2], Is.SameAs(singleton));

				// they should also be the same when parsed from different tuples
				var slicedTuple2 = TuPack.Unpack(TuPack.EncodeKey(value));
				Assert.That(slicedTuple2, Has.Count.EqualTo(1));
				Assert.That(slicedTuple2[0], Is.SameAs(singleton));

				// this should behave the same when parsing from spans
				var spanTuple = TuPack.Unpack(packed.Span);
				Assert.That(spanTuple.Count, Is.EqualTo(3));
				Assert.That(spanTuple[0], Is.SameAs(singleton));
				Assert.That(spanTuple[1], Is.SameAs(singleton));
				Assert.That(spanTuple[2], Is.SameAs(singleton));
			}

			Verify(false);
			Verify(true);
			Verify(0);
			Verify(1);
			Verify(2);
			Verify(3);
			Verify(4);
		}

		[Test]
		public void Test_TuplePack_Serialize_Negative_Integers()
		{
			// Negative integers are stored with a variable-length encoding.
			// - The prefix is 0x14 - the minimum number of bytes to encode the integer, from 0 to 8, so valid prefixes range from 0x0C to 0x13
			// - The value is encoded as the one's complement, and stored in High-Endian (ie: the upper bits first)
			// - There is no way to encode '-0', it will be encoded as '0' (<14>)
			// Examples:
			// - -255..-1 => <13><00> .. <13><FE>
			// - -65535..-256 => <12><00>00> .. <12><FE><FF>
			// - long.MinValue => <0C><7F><FF><FF><FF><FF><FF><FF><FF>

			Assert.That(TuPack.EncodeKey(-1).ToHexString(' '), Is.EqualTo("13 FE"));
			Assert.That(TuPack.EncodeKey(-255).ToHexString(' '), Is.EqualTo("13 00"));
			Assert.That(TuPack.EncodeKey(-256).ToHexString(' '), Is.EqualTo("12 FE FF"));
			Assert.That(TuPack.EncodeKey(-257).ToHexString(' '), Is.EqualTo("12 FE FE"));
			Assert.That(TuPack.EncodeKey(-65535).ToHexString(' '), Is.EqualTo("12 00 00"));
			Assert.That(TuPack.EncodeKey(-65536).ToHexString(' '), Is.EqualTo("11 FE FF FF"));
			Assert.That(TuPack.EncodeKey(int.MinValue).ToHexString(' '), Is.EqualTo("10 7F FF FF FF"));
			Assert.That(TuPack.EncodeKey(-59604644783353249).ToHexString(' '), Is.EqualTo("0D 2C 3D E4 30 B9 92 5E"));
			Assert.That(TuPack.EncodeKey(long.MinValue).ToHexString(' '), Is.EqualTo("0C 7F FF FF FF FF FF FF FF"));
		}

		[Test]
		public void Test_TuplePack_Serialize_Singles()
		{
			// 32-bit floats are stored in 5 bytes, using the prefix 0x20 followed by the High-Endian representation of their normalized form

			Assert.Multiple(() =>
			{
				Assert.That(TuPack.EncodeKey(0f).ToHexString(' '), Is.EqualTo("20 80 00 00 00"));
				Assert.That(TuPack.EncodeKey(-0f).ToHexString(' '), Is.EqualTo("20 7F FF FF FF"));
				Assert.That(TuPack.EncodeKey(42f).ToHexString(' '), Is.EqualTo("20 C2 28 00 00"));
				Assert.That(TuPack.EncodeKey(-42f).ToHexString(' '), Is.EqualTo("20 3D D7 FF FF"));

				Assert.That(TuPack.EncodeKey((float) Math.Sqrt(2)).ToHexString(' '), Is.EqualTo("20 BF B5 04 F3"));

				Assert.That(TuPack.EncodeKey(3.14f).ToHexString(' '), Is.EqualTo("20 C0 48 F5 C3"));
				Assert.That(TuPack.EncodeKey(-3.14f).ToHexString(' '), Is.EqualTo("20 3F B7 0A 3C"));
				Assert.That(TuPack.EncodeKey((float) Math.PI).ToHexString(' '), Is.EqualTo("20 C0 49 0F DB"));
				Assert.That(TuPack.EncodeKey((float) (2 * Math.PI) /* == Math.Tau, absent from netfx */).ToHexString(' '), Is.EqualTo("20 C0 C9 0F DB"));

				Assert.That(TuPack.EncodeKey(float.MinValue).ToHexString(' '), Is.EqualTo("20 00 80 00 00"));
				Assert.That(TuPack.EncodeKey(float.MaxValue).ToHexString(' '), Is.EqualTo("20 FF 7F FF FF"));
				Assert.That(TuPack.EncodeKey(float.NegativeInfinity).ToHexString(' '), Is.EqualTo("20 00 7F FF FF"));
				Assert.That(TuPack.EncodeKey(float.PositiveInfinity).ToHexString(' '), Is.EqualTo("20 FF 80 00 00"));
				Assert.That(TuPack.EncodeKey(float.Epsilon).ToHexString(' '), Is.EqualTo("20 80 00 00 01"));
				Assert.That(TuPack.EncodeKey(-float.Epsilon).ToHexString(' '), Is.EqualTo("20 7F FF FF FE"));

				// all possible variants of NaN should all be equal
				Assert.That(TuPack.EncodeKey(float.NaN).ToHexString(' '), Is.EqualTo("20 00 3F FF FF"));
			});

			// cook up a non-standard NaN (with some bits set in the fraction)
			float f = float.NaN; // defined as 1f / 0f
			uint nan;
			unsafe { nan = *((uint*) &f); }
			nan += 123;
			unsafe { f = *((float*) &nan); }
			Assert.That(float.IsNaN(f), Is.True);
			Assert.That(
				TuPack.EncodeKey(f).ToHexString(' '),
				Is.EqualTo("20 00 3F FF FF"),
				"All variants of NaN must be normalized"
			//note: if we have 20 00 3F FF 84, that means that the NaN was not normalized
			);
		}

		[Test]
		public void Test_TuplePack_Deserialize_Singles()
		{
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 80 00 00 00")), Is.EqualTo(0f));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FF")), Is.EqualTo(-0f));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 C2 28 00 00")), Is.EqualTo(42f));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 3D D7 FF FF")), Is.EqualTo(-42f));

				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 BF B5 04 F3")), Is.EqualTo((float) Math.Sqrt(2) /* == MathF.Sqrt(2), absent from netfx */));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 C0 48 F5 C3")), Is.EqualTo(3.14f));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 3F B7 0A 3C")), Is.EqualTo(-3.14f));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 C0 49 0F DB")), Is.EqualTo((float) Math.PI));
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 C0 C9 0F DB")), Is.EqualTo((float) (2 * Math.PI) /* == Math.Tau, absent from netfx */));

				// well known values
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 80 00 00")), Is.EqualTo(float.MinValue), "float.MinValue");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 FF 7F FF FF")), Is.EqualTo(float.MaxValue), "float.MaxValue");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FF")), Is.EqualTo(-0f), "-0f");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 7F FF FF")), Is.EqualTo(float.NegativeInfinity), "float.NegativeInfinity");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 FF 80 00 00")), Is.EqualTo(float.PositiveInfinity), "float.PositiveInfinity");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 80 00 00")), Is.EqualTo(float.MinValue), "float.Epsilon");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 80 00 00 01")), Is.EqualTo(float.Epsilon), "+float.Epsilon");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FE")), Is.EqualTo(-float.Epsilon), "-float.Epsilon");

				// all possible variants of NaN should end up equal and normalized to float.NaN
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 3F FF FF")), Is.EqualTo(float.NaN), "float.NaN");
				Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 3F FF FF")), Is.EqualTo(float.NaN), "float.NaN");
			});
		}

		[Test]
		public void Test_TuplePack_Serialize_Doubles()
		{
			// 64-bit floats are stored in 9 bytes, using the prefix 0x21 followed by the High-Endian representation of their normalized form

			Assert.Multiple(() =>
			{
				Assert.That(TuPack.EncodeKey(0d).ToHexString(' '), Is.EqualTo("21 80 00 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey(-0d).ToHexString(' '), Is.EqualTo("21 7F FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey(42d).ToHexString(' '), Is.EqualTo("21 C0 45 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey(-42d).ToHexString(' '), Is.EqualTo("21 3F BA FF FF FF FF FF FF"));

				Assert.That(TuPack.EncodeKey(1.00).ToHexString(' '), Is.EqualTo("21 BF F0 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey(0.50).ToHexString(' '), Is.EqualTo("21 BF E0 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey(0.25).ToHexString(' '), Is.EqualTo("21 BF D0 00 00 00 00 00 00"));

				Assert.That(TuPack.EncodeKey(3.14).ToHexString(' '), Is.EqualTo("21 C0 09 1E B8 51 EB 85 1F"));
				Assert.That(TuPack.EncodeKey(-3.14).ToHexString(' '), Is.EqualTo("21 3F F6 E1 47 AE 14 7A E0"));
				Assert.That(TuPack.EncodeKey(Math.PI).ToHexString(' '), Is.EqualTo("21 C0 09 21 FB 54 44 2D 18"));
				Assert.That(TuPack.EncodeKey((2 * Math.PI) /* == Math.Tau, absent from netfx */).ToHexString(' '), Is.EqualTo("21 C0 19 21 FB 54 44 2D 18"));
				Assert.That(TuPack.EncodeKey(Math.E).ToHexString(' '), Is.EqualTo("21 C0 05 BF 0A 8B 14 57 69"));
				Assert.That(TuPack.EncodeKey(Math.Sqrt(2)).ToHexString(' '), Is.EqualTo("21 BF F6 A0 9E 66 7F 3B CD"));

				Assert.That(TuPack.EncodeKey(double.MinValue).ToHexString(' '), Is.EqualTo("21 00 10 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey(double.MaxValue).ToHexString(' '), Is.EqualTo("21 FF EF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey(double.NegativeInfinity).ToHexString(' '), Is.EqualTo("21 00 0F FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey(double.PositiveInfinity).ToHexString(' '), Is.EqualTo("21 FF F0 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey(double.Epsilon).ToHexString(' '), Is.EqualTo("21 80 00 00 00 00 00 00 01"));
				Assert.That(TuPack.EncodeKey(-double.Epsilon).ToHexString(' '), Is.EqualTo("21 7F FF FF FF FF FF FF FE"));

				// all possible variants of NaN should all be equal
				Assert.That(TuPack.EncodeKey(double.NaN).ToHexString(' '), Is.EqualTo("21 00 07 FF FF FF FF FF FF"));
			});

			// cook up a non-standard NaN (with some bits set in the fraction)
			double d = double.NaN; // defined as 1d / 0d
			ulong nan;
			unsafe { nan = *((ulong*) &d); }
			nan += 123;
			unsafe { d = *((double*) &nan); }
			Assert.That(double.IsNaN(d), Is.True);
			Assert.That(
				TuPack.EncodeKey(d).ToHexString(' '),
				Is.EqualTo("21 00 07 FF FF FF FF FF FF")
			//note: if we have 21 00 07 FF FF FF FF FF 84, that means that the NaN was not normalized
			);

			// roundtripping vectors of doubles
			var tuple = STuple.Create(Math.PI, Math.E, Math.Log(1), Math.Log(2));
			Assert.That(TuPack.Unpack(TuPack.EncodeKey(Math.PI, Math.E, Math.Log(1), Math.Log(2))), Is.EqualTo(tuple));
			Assert.That(TuPack.Unpack(TuPack.Pack(STuple.Create(Math.PI, Math.E, Math.Log(1), Math.Log(2)))), Is.EqualTo(tuple));
			Assert.That(TuPack.Unpack(TuPack.Pack(STuple.Empty.Append(Math.PI).Append(Math.E).Append(Math.Log(1)).Append(Math.Log(2)))), Is.EqualTo(tuple));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Doubles()
		{
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 80 00 00 00 00 00 00 00")), Is.EqualTo(0d));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 7F FF FF FF FF FF FF FF")), Is.EqualTo(-0d));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 45 00 00 00 00 00 00")), Is.EqualTo(42d));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 3F BA FF FF FF FF FF FF")), Is.EqualTo(-42d));

				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 BF F0 00 00 00 00 00 00")), Is.EqualTo(1.00));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 BF E0 00 00 00 00 00 00")), Is.EqualTo(0.50));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 BF D0 00 00 00 00 00 00")), Is.EqualTo(0.25));

				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 09 1E B8 51 EB 85 1F")), Is.EqualTo(3.14));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 3F F6 E1 47 AE 14 7A E0")), Is.EqualTo(-3.14));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 09 21 FB 54 44 2D 18")), Is.EqualTo(Math.PI));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 19 21 FB 54 44 2D 18")), Is.EqualTo((2 * Math.PI) /* == Math.Tau, absent from netfx */));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 05 BF 0A 8B 14 57 69")), Is.EqualTo(Math.E));

				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 10 00 00 00 00 00 00")), Is.EqualTo(double.MinValue));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 FF EF FF FF FF FF FF FF")), Is.EqualTo(double.MaxValue));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 0F FF FF FF FF FF FF")), Is.EqualTo(double.NegativeInfinity));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 FF F0 00 00 00 00 00 00")), Is.EqualTo(double.PositiveInfinity));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 80 00 00 00 00 00 00 01")), Is.EqualTo(double.Epsilon));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 7F FF FF FF FF FF FF FE")), Is.EqualTo(-double.Epsilon));

				// all possible variants of NaN should end up equal and normalized to double.NaN
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 07 FF FF FF FF FF FF")), Is.EqualTo(double.NaN));
				Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 07 FF FF FF FF FF 84")), Is.EqualTo(double.NaN));
			});
		}

		[Test]
		public void Test_TuplePack_Serialize_Booleans()
		{
			// Booleans are stored as <26> for false, and <27> for true

			// bool
			Assert.That(TuPack.EncodeKey(false).ToHexString(' '), Is.EqualTo("26"));
			Assert.That(TuPack.EncodeKey(true).ToHexString(' '), Is.EqualTo("27"));

			// bool?
			Assert.That(TuPack.EncodeKey(default(bool?)).ToHexString(' '), Is.EqualTo("00"));
			Assert.That(TuPack.EncodeKey((bool?) false).ToHexString(' '), Is.EqualTo("26"));
			Assert.That(TuPack.EncodeKey((bool?) true).ToHexString(' '), Is.EqualTo("27"));

			// tuple containing bools
			Assert.That(TuPack.EncodeKey(true).ToHexString(' '), Is.EqualTo("27"));
			Assert.That(TuPack.EncodeKey(true, default(string), false).ToHexString(' '), Is.EqualTo("27 00 26"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Booleans()
		{
			// Null, 0, and empty byte[]/strings are equivalent to False. All others are equivalent to True

			// Falsy...
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<26>")), Is.False, "False => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<00>")), Is.False, "Null => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<14>")), Is.False, "0 => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<01><00>")), Is.False, "byte[0] => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02><00>")), Is.False, "String.Empty => False");

			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<26>").Span), Is.False, "False => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<00>").Span), Is.False, "Null => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<14>").Span), Is.False, "0 => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<01><00>").Span), Is.False, "byte[0] => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02><00>").Span), Is.False, "String.Empty => False");

			// Truthy
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<27>")), Is.True, "True => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<15><01>")), Is.True, "1 => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<13><FE>")), Is.True, "-1 => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<01>Hello<00>")), Is.True, "'Hello' => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02>Hello<00>")), Is.True, "\"Hello\" => True");
			Assert.That(TuPack.DecodeKey<bool>(TuPack.EncodeKey(123456789)), Is.True, "random int => True");

			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02>True<00>")), Is.True, "\"True\" => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02>False<00>")), Is.True, "\"False\" => True ***");
			// note: even though it would be tempting to convert the string "false" to False, it is not a standard behavior accross all bindings

			// When decoded to object, they should return false and true
			Assert.That(TuplePackers.DeserializeBoxed(TuPack.EncodeKey(false)), Is.False);
			Assert.That(TuplePackers.DeserializeBoxed(TuPack.EncodeKey(true)), Is.True);
		}

		[Test]
		public void Test_TuplePack_Serialize_VersionStamps()
		{
			// incomplete, 80 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete()).ToHexString(' '),
				Is.EqualTo("32 FF FF FF FF FF FF FF FF FF FF")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create(VersionStamp.Incomplete())).ToHexString(' '),
				Is.EqualTo("32 FF FF FF FF FF FF FF FF FF FF")
			);

			// incomplete, 96 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(0)).ToHexString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF 00 00")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(42)).ToHexString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF 00 2A")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(456)).ToHexString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF 01 C8")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(65535)).ToHexString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF FF FF")
			);

			// complete, 80 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234)).ToHexString(' '),
				Is.EqualTo("32 01 23 45 67 89 AB CD EF 04 D2")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create(VersionStamp.Complete(0x0123456789ABCDEF, 1234))).ToHexString(' '),
				Is.EqualTo("32 01 23 45 67 89 AB CD EF 04 D2")
			);

			// complete, 96 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 0)).ToHexString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF 04 D2 00 00")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 42)).ToHexString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF 04 D2 00 2A")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 65535, 42)).ToHexString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF FF FF 00 2A")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 65535)).ToHexString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF 04 D2 FF FF")
			);
		}

		[Test]
		public void Test_TuplePack_Deserialize_VersionStamps()
		{
			Assert.That(TuPack.Unpack(Slice.FromHexa("32 FF FF FF FF FF FF FF FF FF FF")).Get<VersionStamp>(0), Is.EqualTo(VersionStamp.Incomplete()), "Incomplete()");
			Assert.That(TuPack.Unpack(Slice.FromHexa("32 01 23 45 67 89 AB CD EF 04 D2")).Get<VersionStamp>(0), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234)), "Complete(..., 1234)");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("32 FF FF FF FF FF FF FF FF FF FF")), Is.EqualTo(VersionStamp.Incomplete()), "Incomplete()");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF 00 00")), Is.EqualTo(VersionStamp.Incomplete(0)), "Incomplete(0)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF 00 2A")), Is.EqualTo(VersionStamp.Incomplete(42)), "Incomplete(42)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF 01 C8")), Is.EqualTo(VersionStamp.Incomplete(456)), "Incomplete(456)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF FF FF")), Is.EqualTo(VersionStamp.Incomplete(65535)), "Incomplete(65535)");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("32 01 23 45 67 89 AB CD EF 04 D2")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234)), "Complete(..., 1234)");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 04 D2 00 00")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 0)), "Complete(..., 1234, 0)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 04 D2 00 2A")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 42)), "Complete(..., 1234, 42)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF FF FF 00 2A")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 65535, 42)), "Complete(..., 65535, 42)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 04 D2 FF FF")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 65535)), "Complete(..., 1234, 65535)");
		}

		[Test]
		public void Test_TuplePack_Serialize_Big_Integers()
		{
#if NET8_0_OR_GREATER

			// UInt128
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.EncodeKey(UInt128.Zero).ToHexString(' '), Is.EqualTo("14"));
				Assert.That(TuPack.EncodeKey(UInt128.One).ToHexString(' '), Is.EqualTo("15 01"));
				Assert.That(TuPack.EncodeKey((UInt128) 0x1234).ToHexString(' '), Is.EqualTo("16 12 34"));
				Assert.That(TuPack.EncodeKey((UInt128) 0x12345678).ToHexString(' '), Is.EqualTo("18 12 34 56 78"));
				Assert.That(TuPack.EncodeKey((UInt128) uint.MaxValue).ToHexString(' '), Is.EqualTo("18 FF FF FF FF"));
				Assert.That(TuPack.EncodeKey((UInt128.One << 32)).ToHexString(' '), Is.EqualTo("19 01 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((UInt128) 0x0123456789ABCDEF).ToHexString(' '), Is.EqualTo("1C 01 23 45 67 89 AB CD EF"));
				Assert.That(TuPack.EncodeKey((UInt128) long.MaxValue).ToHexString(' '), Is.EqualTo("1C 7F FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey((UInt128.One << 63)).ToHexString(' '), Is.EqualTo("1C 80 00 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((UInt128) ulong.MaxValue).ToHexString(' '), Is.EqualTo("1C FF FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey((UInt128.One << 64)).ToHexString(' '), Is.EqualTo("1D 09 01 00 00 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((UInt128.One << 72) - 1).ToHexString(' '), Is.EqualTo("1D 09 FF FF FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey(UInt128.MaxValue).ToHexString(' '), Is.EqualTo("1D 10 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF"));
			});

			// Int128
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.EncodeKey(Int128.Zero).ToHexString(' '), Is.EqualTo("14"));
				Assert.That(TuPack.EncodeKey(Int128.One).ToHexString(' '), Is.EqualTo("15 01"));
				Assert.That(TuPack.EncodeKey((Int128) 0x1234).ToHexString(' '), Is.EqualTo("16 12 34"));
				Assert.That(TuPack.EncodeKey((Int128) 0x12345678).ToHexString(' '), Is.EqualTo("18 12 34 56 78"));
				Assert.That(TuPack.EncodeKey((Int128) uint.MaxValue).ToHexString(' '), Is.EqualTo("18 FF FF FF FF"));
				Assert.That(TuPack.EncodeKey((Int128.One << 32)).ToHexString(' '), Is.EqualTo("19 01 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((Int128) 59604644783353249).ToHexString(' '), Is.EqualTo("1B D3 C2 1B CF 46 6D A1"));
				Assert.That(TuPack.EncodeKey((Int128) 0x0123456789ABCDEF).ToHexString(' '), Is.EqualTo("1C 01 23 45 67 89 AB CD EF"));
				Assert.That(TuPack.EncodeKey((Int128) long.MaxValue).ToHexString(' '), Is.EqualTo("1C 7F FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey((Int128.One << 63)).ToHexString(' '), Is.EqualTo("1C 80 00 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((Int128) ulong.MaxValue).ToHexString(' '), Is.EqualTo("1C FF FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey((Int128.One << 64)).ToHexString(' '), Is.EqualTo("1D 09 01 00 00 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((Int128.One << 72) - 1).ToHexString(' '), Is.EqualTo("1D 09 FF FF FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey(Int128.MaxValue).ToHexString(' '), Is.EqualTo("1D 10 7F FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF"));

				Assert.That(TuPack.EncodeKey((Int128) (-1)).ToHexString(' '), Is.EqualTo("13 FE"));
				Assert.That(TuPack.EncodeKey((Int128) (-0x1234)).ToHexString(' '), Is.EqualTo("12 ED CB"));
				Assert.That(TuPack.EncodeKey((Int128) (-0x12345678)).ToHexString(' '), Is.EqualTo("10 ED CB A9 87"));
				Assert.That(TuPack.EncodeKey(((Int128) int.MinValue) + 1).ToHexString(' '), Is.EqualTo("10 80 00 00 00"));
				Assert.That(TuPack.EncodeKey((Int128) int.MinValue).ToHexString(' '), Is.EqualTo("10 7F FF FF FF"));
				Assert.That(TuPack.EncodeKey((Int128) (-59604644783353249)).ToHexString(' '), Is.EqualTo("0D 2C 3D E4 30 B9 92 5E"));
				Assert.That(TuPack.EncodeKey((Int128) (-0x0123456789ABCDEF)).ToHexString(' '), Is.EqualTo("0C FE DC BA 98 76 54 32 10"));
				Assert.That(TuPack.EncodeKey((Int128) long.MinValue + 1).ToHexString(' '), Is.EqualTo("0C 80 00 00 00 00 00 00 00"));
				Assert.That(TuPack.EncodeKey((Int128) long.MinValue).ToHexString(' '), Is.EqualTo("0C 7F FF FF FF FF FF FF FF"));
				Assert.That(TuPack.EncodeKey(((Int128) long.MinValue) - 1).ToHexString(' '), Is.EqualTo("0B F7 7F FF FF FF FF FF FF FE"));
				Assert.That(TuPack.EncodeKey(Int128.MinValue).ToHexString(' '), Is.EqualTo("0B EF 7F FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF"));
			});
#endif

			// BigInteger
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.EncodeKey(BigInteger.Zero).ToHexString(' '), Is.EqualTo("14"));
				Assert.That(TuPack.EncodeKey(BigInteger.One).ToHexString(' '), Is.EqualTo("15 01"));

				Assert.That(TuPack.EncodeKey(new BigInteger(0x1234)).ToHexString(' '), Is.EqualTo("16 12 34"));
				Assert.That(TuPack.EncodeKey(new BigInteger(0x12345678)).ToHexString(' '), Is.EqualTo("18 12 34 56 78"));
				Assert.That(TuPack.EncodeKey(new BigInteger(0x0123456789ABCDEF)).ToHexString(' '), Is.EqualTo("1C 01 23 45 67 89 AB CD EF"));

				Assert.That(TuPack.EncodeKey(BigInteger.MinusOne).ToHexString(' '), Is.EqualTo("13 FE"));
				Assert.That(TuPack.EncodeKey(new BigInteger(-0x1234)).ToHexString(' '), Is.EqualTo("12 ED CB"));
				Assert.That(TuPack.EncodeKey(new BigInteger(-0x12345678)).ToHexString(' '), Is.EqualTo("10 ED CB A9 87"));
				Assert.That(TuPack.EncodeKey(new BigInteger(-0x0123456789ABCDEF)).ToHexString(' '), Is.EqualTo("0C FE DC BA 98 76 54 32 10"));

				var p1 = BigInteger.Parse("59604644783353249"); // 0xD3C21BCF466DA1
				var p2 = BigInteger.Parse("523347633027360537213687137"); // 0x01B0E72C08E02B2911A05561
				var p3 = BigInteger.Parse("31193949764804535768713156448991454038458113"); // 0x016616C7819327971A0681C5669B7121B5FF01
				var p4 = BigInteger.Parse("143341119796678074027721637161034238097976392367098600224732157310757655335480353"); // 0x04D5EB06BADA9BAE611C34E67A38930EFA5663F66B09E33528B8EE10E89643552C21
				Assert.That(TuPack.EncodeKey(p1).ToHexString(' '), Is.EqualTo("1B D3 C2 1B CF 46 6D A1"));
				Assert.That(TuPack.EncodeKey(p2).ToHexString(' '), Is.EqualTo("1D 0C 01 B0 E7 2C 08 E0 2B 29 11 A0 55 61"));
				Assert.That(TuPack.EncodeKey(p3).ToHexString(' '), Is.EqualTo("1D 13 01 66 16 C7 81 93 27 97 1A 06 81 C5 66 9B 71 21 B5 FF 01"));
				Assert.That(TuPack.EncodeKey(p4).ToHexString(' '), Is.EqualTo("1D 22 04 D5 EB 06 BA DA 9B AE 61 1C 34 E6 7A 38 93 0E FA 56 63 F6 6B 09 E3 35 28 B8 EE 10 E8 96 43 55 2C 21"));
				Assert.That(TuPack.EncodeKey(-p1).ToHexString(' '), Is.EqualTo("0D 2C 3D E4 30 B9 92 5E"));
				Assert.That(TuPack.EncodeKey(-p2).ToHexString(' '), Is.EqualTo("0B F3 FE 4F 18 D3 F7 1F D4 D6 EE 5F AA 9E"));
				Assert.That(TuPack.EncodeKey(-p3).ToHexString(' '), Is.EqualTo("0B EC FE 99 E9 38 7E 6C D8 68 E5 F9 7E 3A 99 64 8E DE 4A 00 FE"));
				Assert.That(TuPack.EncodeKey(-p4).ToHexString(' '), Is.EqualTo("0B DD FB 2A 14 F9 45 25 64 51 9E E3 CB 19 85 C7 6C F1 05 A9 9C 09 94 F6 1C CA D7 47 11 EF 17 69 BC AA D3 DE"));
			});

		}

		[Test]
		public void Test_TuplePack_Deserialize_BigIntegers()
		{
#if NET8_0_OR_GREATER

			// UInt128
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.DecodeKey<UInt128>(Slice.FromHexa("14")), Is.EqualTo(UInt128.Zero));
				Assert.That(TuPack.DecodeKey<UInt128>(Slice.FromHexa("15 01")), Is.EqualTo(UInt128.One));
				Assert.That(TuPack.DecodeKey<UInt128>(Slice.FromHexa("16 12 34")), Is.EqualTo((UInt128) 0x1234));
				Assert.That(TuPack.DecodeKey<UInt128>(Slice.FromHexa("18 12 34 56 78")), Is.EqualTo((UInt128) 0x12345678));
				Assert.That(TuPack.DecodeKey<UInt128>(Slice.FromHexa("1C 01 23 45 67 89 AB CD EF")), Is.EqualTo((UInt128) 0x0123456789ABCDEF));
				Assert.That(TuPack.DecodeKey<UInt128>(Slice.FromHexa("1D 09 01 00 00 00 00 00 00 00 00")), Is.EqualTo(UInt128.One << 64));
			});

			// Int128
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.DecodeKey<Int128>(Slice.FromHexa("14")), Is.EqualTo(Int128.Zero));
				Assert.That(TuPack.DecodeKey<Int128>(Slice.FromHexa("15 01")), Is.EqualTo(Int128.One));
				Assert.That(TuPack.DecodeKey<Int128>(Slice.FromHexa("13 FE")), Is.EqualTo((Int128) (-1)));
				Assert.That(TuPack.DecodeKey<Int128>(Slice.FromHexa("0B EF 7F FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF")), Is.EqualTo(Int128.MinValue));
			});
#endif

			// BigInteger
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("14")), Is.EqualTo(BigInteger.Zero));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("15 01")), Is.EqualTo(BigInteger.One));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("13 FE")), Is.EqualTo(BigInteger.MinusOne));

				var p1 = BigInteger.Parse("59604644783353249"); // 0xD3C21BCF466DA1
				var p2 = BigInteger.Parse("523347633027360537213687137"); // 0x01B0E72C08E02B2911A05561
				var p3 = BigInteger.Parse("31193949764804535768713156448991454038458113"); // 0x016616C7819327971A0681C5669B7121B5FF01
				var p4 = BigInteger.Parse("143341119796678074027721637161034238097976392367098600224732157310757655335480353"); // 0x04D5EB06BADA9BAE611C34E67A38930EFA5663F66B09E33528B8EE10E89643552C21

				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("1B D3 C2 1B CF 46 6D A1")), Is.EqualTo(p1));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("1D 0C 01 B0 E7 2C 08 E0 2B 29 11 A0 55 61")), Is.EqualTo(p2));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("1D 13 01 66 16 C7 81 93 27 97 1A 06 81 C5 66 9B 71 21 B5 FF 01")), Is.EqualTo(p3));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("1D 22 04 D5 EB 06 BA DA 9B AE 61 1C 34 E6 7A 38 93 0E FA 56 63 F6 6B 09 E3 35 28 B8 EE 10 E8 96 43 55 2C 21")), Is.EqualTo(p4));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("0D 2C 3D E4 30 B9 92 5E")), Is.EqualTo(-p1));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("0B F3 FE 4F 18 D3 F7 1F D4 D6 EE 5F AA 9E")), Is.EqualTo(-p2));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("0B EC FE 99 E9 38 7E 6C D8 68 E5 F9 7E 3A 99 64 8E DE 4A 00 FE")), Is.EqualTo(-p3));
				Assert.That(TuPack.DecodeKey<BigInteger>(Slice.FromHexa("0B DD FB 2A 14 F9 45 25 64 51 9E E3 CB 19 85 C7 6C F1 05 A9 9C 09 94 F6 1C CA D7 47 11 EF 17 69 BC AA D3 DE")), Is.EqualTo(-p4));
			});

			// A big integer that is NOT the first element decodes from a non-zero cursor.
			// The reader must check the type byte at the cursor, not at buffer[0] (the first element's byte).
			Assert.Multiple(() =>
			{
				var pos = BigInteger.Parse("523347633027360537213687137");
				var neg = -pos;

				// (prefix, bigint): the "A" prefix packs to <02>41<00>, so buffer[0] is 0x02, not the big-integer marker.
				var packedPos = TuPack.EncodeKey("A", pos);
				var packedNeg = TuPack.EncodeKey("A", neg);

				Assert.That(TuPack.DecodeKey<string, BigInteger>(packedPos), Is.EqualTo(("A", pos)));
				Assert.That(TuPack.DecodeKey<string, BigInteger>(packedNeg), Is.EqualTo(("A", neg)));
			});
		}


		[Test]
		public void Test_TuplePack_Serialize_Custom_Types()
		{
			// 64-bit floats are stored in 9 bytes, using the prefix 0x21 followed by the High-Endian representation of their normalized form

			Assert.Multiple(() =>
			{
				Assert.That(TuPack.EncodeKey(TuPackUserType.System).ToHexString(' '), Is.EqualTo("FF"));
				Assert.That(TuPack.EncodeKey(TuPackUserType.SystemKey("/Hello"u8)).ToHexString(' '), Is.EqualTo("FF 2F 48 65 6C 6C 6F"));
				Assert.That(TuPack.EncodeKey(TuPackUserType.SystemKey("/Hello")).ToHexString(' '), Is.EqualTo("FF 2F 48 65 6C 6C 6F"));

				Assert.That(TuPack.EncodeKey(TuPackUserType.Directory).ToHexString(' '), Is.EqualTo("FE"));
				Assert.That(TuPack.EncodeKey(TuPackUserType.Directory, 42, "Hello").ToHexString(' '), Is.EqualTo("FE 15 2A 02 48 65 6C 6C 6F 00"));

				Assert.That(TuPack.EncodeKey(42, TuPackUserType.Directory, "Hello").ToHexString(' '), Is.EqualTo("15 2A FE 02 48 65 6C 6C 6F 00"));
				Assert.That(TuPack.Pack((42, TuPackUserType.Directory, "Hello")).ToHexString(' '), Is.EqualTo("15 2A FE 02 48 65 6C 6C 6F 00"));

				Assert.That(TuPack.EncodeKey(TuPackUserType.System, "Hello").ToHexString(' '), Is.EqualTo("FF 02 48 65 6C 6C 6F 00"));
				Assert.That(TuPack.Pack((TuPackUserType.System, "Hello")).ToHexString(' '), Is.EqualTo("FF 02 48 65 6C 6C 6F 00"));
			});
		}

		[Test]
		public void Test_TuplePack_Deserialize_Custom_Types()
		{
			Assert.Multiple(() =>
			{
				Assert.That(TuPack.DecodeKey<TuPackUserType>(Slice.FromHexString("FF")), Is.EqualTo(TuPackUserType.System));
				Assert.That(TuPack.DecodeKey<TuPackUserType>(Slice.FromHexString("FFFF")), Is.EqualTo(TuPackUserType.SystemKey("\xFF")));
				Assert.That(TuPack.DecodeKey<TuPackUserType>(Slice.FromHexString("FF2F48656C6C6F")), Is.EqualTo(TuPackUserType.SystemKey("/Hello")));

				Assert.That(TuPack.DecodeKey<TuPackUserType>(Slice.FromHexString("FE")), Is.EqualTo(TuPackUserType.Directory));
				Assert.That(TuPack.DecodeKey<int, TuPackUserType, string>(Slice.FromHexString("152AFE0248656C6C6F00")), Is.EqualTo(STuple.Create(42, TuPackUserType.Directory, "Hello")));

				Assert.That(TuPack.Unpack(Slice.FromHexString("FF"))[0], Is.EqualTo(TuPackUserType.System));
				Assert.That(TuPack.Unpack(Slice.FromHexString("FF2F48656C6C6F"))[0], Is.EqualTo(TuPackUserType.SystemKey("/Hello")));
				Assert.That(TuPack.Unpack(Slice.FromHexString("FE"))[0], Is.EqualTo(TuPackUserType.Directory));
				Assert.That(TuPack.Unpack(Slice.FromHexString("152AFE0248656C6C6F00")), Is.EqualTo(STuple.Create(42, TuPackUserType.Directory, "Hello")));
			});
		}

		[Test]
		public void Test_TuplePack_Serialize_IPAddress()
		{
			// IP Addresses are stored as a byte array (<01>..<00>), in network order (big-endian)
			// They will take from 6 to 10 bytes, depending on the number of '.0' in them.

			Assert.That(
				TuPack.EncodeKey(IPAddress.Loopback).ToHexString(' '),
				Is.EqualTo("01 7F 00 FF 00 FF 01 00")
			);

			Assert.That(
				TuPack.EncodeKey(IPAddress.Any).ToHexString(' '),
				Is.EqualTo("01 00 FF 00 FF 00 FF 00 FF 00")
			);

			Assert.That(
				TuPack.EncodeKey(IPAddress.Parse("1.2.3.4")).ToHexString(' '),
				Is.EqualTo("01 01 02 03 04 00")
			);

			Assert.That(
				TuPack.Pack(ValueTuple.Create(IPAddress.Loopback)).ToHexString(' '),
				Is.EqualTo("01 7F 00 FF 00 FF 01 00")
			);

		}

		[Test]
		public void Test_TuplePack_Deserialize_IPAddress()
		{
			Assert.That(TuPack.Unpack(Slice.Unescape("<01><7F><00><FF><00><FF><01><00>")).Get<IPAddress>(0), Is.EqualTo(IPAddress.Parse("127.0.0.1")));

			Assert.That(TuPack.DecodeKey<IPAddress>(Slice.Unescape("<01><7F><00><FF><00><FF><01><00>")), Is.EqualTo(IPAddress.Parse("127.0.0.1")));
			Assert.That(TuPack.DecodeKey<IPAddress>(Slice.Unescape("<01><00><FF><00><FF><00><FF><00><FF><00>")), Is.EqualTo(IPAddress.Parse("0.0.0.0")));
			Assert.That(TuPack.DecodeKey<IPAddress>(Slice.Unescape("<01><01><02><03><04><00>")), Is.EqualTo(IPAddress.Parse("1.2.3.4")));

			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey("127.0.0.1")), Is.EqualTo(IPAddress.Loopback));

			var ip = IPAddress.Parse("192.168.0.1");
			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey(ip.ToString())), Is.EqualTo(ip));
			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey(ip.GetAddressBytes())), Is.EqualTo(ip));
#pragma warning disable 618
			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey(ip.Address)), Is.EqualTo(ip));
#pragma warning restore 618
		}

		[Test]
		public void Test_TuplePack_NullableTypes()
		{
			// Nullable types will either be encoded as <14> for null, or their regular encoding if not null

			// serialize

			Assert.That(TuPack.EncodeKey<int?>(0), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(TuPack.EncodeKey<int?>(123), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.EncodeKey<int?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.EncodeKey<long?>(0L), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(TuPack.EncodeKey<long?>(123L), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.EncodeKey<long?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.EncodeKey<bool?>(true), Is.EqualTo(Slice.Unescape("<27>")));
			Assert.That(TuPack.EncodeKey<bool?>(false), Is.EqualTo(Slice.Unescape("<26>")));
			Assert.That(TuPack.EncodeKey<bool?>(null), Is.EqualTo(Slice.Unescape("<00>")), "Maybe it was File Not Found?");

			Assert.That(TuPack.EncodeKey<Guid?>(Guid.Empty), Is.EqualTo(Slice.Unescape("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>")));
			Assert.That(TuPack.EncodeKey<Guid?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.EncodeKey<TimeSpan?>(TimeSpan.Zero), Is.EqualTo(Slice.Unescape("!<80><00><00><00><00><00><00><00>")));
			Assert.That(TuPack.EncodeKey<TimeSpan?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.Pack(ValueTuple.Create<int?>(123)), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.Pack(ValueTuple.Create<long?>(123)), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.Pack(ValueTuple.Create<bool?>(true)), Is.EqualTo(Slice.Unescape("<27>")));

			// deserialize

			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<14>")), Is.EqualTo(0));
			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<15>{")), Is.EqualTo(123));
			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<00>")), Is.Null);
			Assert.That(TuPack.Unpack(Slice.Unescape("<15>{")).Get<int?>(0), Is.EqualTo(123));
			Assert.That(TuPack.Unpack(Slice.Unescape("<00>")).Get<int?>(0), Is.Null);

			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<14>")), Is.EqualTo(0L));
			Assert.That(TuPack.DecodeKey<long?>(Slice.Unescape("<15>{")), Is.EqualTo(123L));
			Assert.That(TuPack.DecodeKey<long?>(Slice.Unescape("<00>")), Is.Null);
			Assert.That(TuPack.Unpack(Slice.Unescape("<15>{")).Get<long?>(0), Is.EqualTo(123L));
			Assert.That(TuPack.Unpack(Slice.Unescape("<00>")).Get<long?>(0), Is.Null);

			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<27>")), Is.True);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<15><01>")), Is.True);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<26>")), Is.False);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<14>")), Is.False);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(TuPack.DecodeKey<Guid?>(Slice.Unescape("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>")), Is.EqualTo(Guid.Empty));
			Assert.That(TuPack.DecodeKey<Guid?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(TuPack.DecodeKey<TimeSpan?>(Slice.Unescape("<14>")), Is.EqualTo(TimeSpan.Zero));
			Assert.That(TuPack.DecodeKey<TimeSpan?>(Slice.Unescape("<00>")), Is.Null);

		}

		[Test]
		public void Test_TuplePack_Serialize_Embedded_Tuples()
		{
			static void Verify(IVarTuple t, string expected)
			{
				var key = TuPack.Pack(t);
				Assert.That(key.ToHexString(' '), Is.EqualTo(expected));
				var t2 = TuPack.Unpack(key);
				Assert.That(t2, Is.Not.Null);
				Assert.That(t2.Count, Is.EqualTo(t.Count), $"{t2}.Count");
				if (!t2.Equals(t))
				{
					Assert.That(t2, Is.EqualTo(t), $"Failed to unpack {t}: found {t2} instead");
				}
			}

			// Index composite key
			IVarTuple value = STuple.Create(2014, 11, 6); // Indexing a date value (Y, M, D)
			string docId = "Doc123";
			// key would be "(..., value, id)"

			Verify(
				STuple.Empty,
				""
			);
			Verify(
				STuple.Create(42, value, docId),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			Verify(
				STuple.Create([42, value, docId]),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			Verify(
				STuple.Create(42).Append(value).Append(docId),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			Verify(
				STuple.Create(42).Append(value, docId),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);

			// multiple depth
			Verify(
				STuple.Create(1, STuple.Create(2, 3), STuple.Create(STuple.Create(4, 5, 6)), 7),
				"15 01 05 15 02 15 03 00 05 05 15 04 15 05 15 06 00 00 15 07"
			);

			// corner cases
			Verify(
				STuple.Create(STuple.Empty),
				"05 00" // empty tuple should have header and footer
			);
			Verify(
				STuple.Create(STuple.Empty, default(string)),
				"05 00 00" // outer null should not be escaped
			);
			// corner cases
			Verify(
				STuple.Create(STuple.Create(STuple.Empty, STuple.Empty), STuple.Empty),
				"05 05 00 05 00 00 05 00"
			);
			Verify(
				STuple.Create(STuple.Create(default(string)), default(string)),
				"05 00 FF 00 00" // inner null should be escaped, but not outer
			);
			Verify(
				STuple.Create(STuple.Create(0x100, 0x10000, 0x1000000)),
				"05 16 01 00 17 01 00 00 18 01 00 00 00 00"
			);
			Verify(
				STuple.Create(default(string), STuple.Empty, default(string), STuple.Create(default(string)), default(string)),
				"00 05 00 00 05 00 FF 00 00" // inner null should be escaped, but not outer
			);

		}

		[Test]
		public void Test_TuplePack_Deserialize_Embedded_Tuples()
		{
			// ( (42, (2014, 11, 6), "Hello", true), )
			var packed = TuPack.EncodeKey(STuple.Create(42, STuple.Create(2014, 11, 6), "Hello", true));
			Log($"p = {packed:x}");
			Log($"t = {TuPack.Unpack(packed)}");
			Assert.That(packed[0], Is.EqualTo(TupleTypes.EmbeddedTuple), "Missing Embedded Tuple marker");
			{
				var t = TuPack.Unpack(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Count, Is.EqualTo(1));
				var t1 = t.Get<IVarTuple>(0);
				Assert.That(t1.Count, Is.EqualTo(4));
				Assert.That(t1.Get<int>(0), Is.EqualTo(42));
				Assert.That(t1.Get<IVarTuple>(1), Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t1.Get<string>(2), Is.EqualTo("Hello"));
				Assert.That(t1.Get<bool>(3), Is.True);
			}
			{
				var t = TuPack.DecodeKey<IVarTuple>(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Count, Is.EqualTo(4));
				Assert.That(t.Get<int>(0), Is.EqualTo(42));
				Assert.That(t.Get<IVarTuple>(1), Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Get<string>(2), Is.EqualTo("Hello"));
				Assert.That(t.Get<bool>(3), Is.True);
			}
			{
				var t = TuPack.DecodeKey<STuple<int, IVarTuple, string, bool>>(packed);
				Assert.That(t.Item1, Is.EqualTo(42));
				Assert.That(t.Item2, Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Item3, Is.EqualTo("Hello"));
				Assert.That(t.Item4, Is.True);
			}
			{
				var t = TuPack.DecodeKey<(int, IVarTuple, string, bool)>(packed);
				Assert.That(t, Is.EqualTo((42, STuple.Create(2014, 11, 6), "Hello", true)));
			}
			{
				STuple<int, STuple<int, int, int>, string, bool> t = TuPack.DecodeKey<STuple<int, STuple<int, int, int>, string, bool>>(packed);
				Assert.That(t.Item1, Is.EqualTo(42));
				Assert.That(t.Item2, Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Item3, Is.EqualTo("Hello"));
				Assert.That(t.Item4, Is.True);
			}
			{
				var t = TuPack.DecodeKey<(int, (int, int, int), string, bool)>(packed);
				Assert.That(t, Is.EqualTo((42, (2014, 11, 6), "Hello", true)));
			}

			// empty tuples
			{
				Assert.That(TuPack.Unpack(Slice.Empty), Is.EqualTo(STuple.Empty), "()");
				Assert.That(TuPack.Unpack(Slice.Unescape("<05><00>")), Is.EqualTo(STuple.Create(STuple.Empty)), "((),)");
				Assert.That(TuPack.Unpack(Slice.Unescape("<05><05><00><05><00><00><05><00>")), Is.EqualTo(STuple.Create(STuple.Create(STuple.Empty, STuple.Empty), STuple.Empty)), "(((),()),())");
			}

			// (null,)
			packed = Slice.FromByte(0);
			Log($"p = {packed:x}");
			Log($"t = {TuPack.Unpack(packed)}");
			{
				var t = TuPack.DecodeKey<IVarTuple>(packed);
				Assert.That(t, Is.Null);
			}
			{
				var t = TuPack.DecodeKey<STuple<int, STuple<int, int, int>, string, bool>>(packed);
				Assert.That(t.Item1, Is.EqualTo(0));
				Assert.That(t.Item2, Is.EqualTo(default(STuple<int, int, int>)));
				Assert.That(t.Item3, Is.Null);
				Assert.That(t.Item4, Is.False);
			}
			{
				var t = TuPack.DecodeKey<(int, (int, int, int), string, bool)>(packed);
				Assert.That(t, Is.EqualTo((0, (0, 0, 0), default(string), false)));
			}

			//fallback if encoded as slice
			packed = TuPack.EncodeKey(TuPack.EncodeKey(42, STuple.Create(2014, 11, 6), "Hello", true));
			Log($"p = {packed:x}");
			Log($"t = {TuPack.Unpack(packed)}");
			Assert.That(packed[0], Is.EqualTo(TupleTypes.Bytes), "Missing Slice marker");
			{
				var t = TuPack.DecodeKey<STuple<int, STuple<int, int, int>, string, bool>>(packed);
				Assert.That(t.Item1, Is.EqualTo(42));
				Assert.That(t.Item2, Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Item3, Is.EqualTo("Hello"));
				Assert.That(t.Item4, Is.True);
			}
			{
				var t = TuPack.DecodeKey<(int, (int, int, int), string, bool)>(packed);
				Assert.That(t, Is.EqualTo((42, (2014, 11, 6), "Hello", true)));
			}
		}

		[Test]
		public void Test_TuplePack_Deserialize_Embedded_Tuples_As_SlicedTuple()
		{
			// an embedded IVarTuple now decodes to a SlicedTuple that views the source memory, instead of an
			// allocating ListTuple<object?>. Equality, hashing and ordering must stay identical either way.

			var packed = TuPack.EncodeKey(STuple.Create("a", 123L, true));

			// type: every decode path that reaches an embedded IVarTuple returns a SlicedTuple
			Assert.That(TuPack.DecodeKey<IVarTuple>(packed), Is.InstanceOf<SlicedTuple>());
			Assert.That(TuPack.DecodeLast<string, string, IVarTuple>(TuPack.EncodeKey("zone", "node", STuple.Create("a", 123L, true))).Item3, Is.InstanceOf<SlicedTuple>());
			Assert.That(SlicedTuple.Unpack(packed).Get<IVarTuple>(0), Is.InstanceOf<SlicedTuple>());
			Assert.That(TuPack.Unpack(packed)[0], Is.InstanceOf<SlicedTuple>());

			var decoded = TuPack.DecodeKey<IVarTuple>(packed)!;
			var expected = STuple.Create("a", 123L, true);
			var list = new ListTuple<object?>(decoded.ToArray());

			// equality, both ways, against both an STuple and a ListTuple holding the same boxed values
			Assert.That(decoded.Equals(expected), Is.True);
			Assert.That(expected.Equals(decoded), Is.True);
			Assert.That(decoded.Equals(list), Is.True);
			Assert.That(list.Equals(decoded), Is.True);
			Assert.That(decoded, Is.EqualTo(expected));

			// hashing
			Assert.That(decoded.GetHashCode(), Is.EqualTo(list.GetHashCode()));
			Assert.That(decoded.GetHashCode(), Is.EqualTo(expected.GetHashCode()));
			Assert.That(new HashSet<IVarTuple>{ expected }.Contains(decoded), Is.True);
			Assert.That(new Dictionary<IVarTuple, int> { [expected] = 1 }.TryGetValue(decoded, out var found) && found == 1, Is.True);

			// repeat with a 4-element and a 5-element tuple (the h[^2], h[^1] hash branch)
			foreach (var inner in new IVarTuple[] { STuple.Create("a", 123L, true, 4.5), STuple.Create("a", 123L, true, 4.5, "z") })
			{
				var p = TuPack.EncodeKey(inner);
				var d = TuPack.DecodeKey<IVarTuple>(p)!;
				Assert.That(d, Is.InstanceOf<SlicedTuple>());
				Assert.That(d.GetHashCode(), Is.EqualTo(inner.GetHashCode()));
				Assert.That(d, Is.EqualTo(inner));
			}

			// ordering
			Assert.That(decoded.CompareTo(expected), Is.Zero);
			Assert.That(decoded.CompareTo(STuple.Create("a", 124L, true)), Is.LessThan(0));

			// empty embedded tuple
			{
				var d = TuPack.DecodeKey<IVarTuple>(Slice.Unescape("<05><00>"));
				Assert.That(d!.Count, Is.EqualTo(0));
				Assert.That(d, Is.EqualTo(STuple.Empty));
			}

			// nested embedded tuples
			{
				var p = TuPack.EncodeKey(STuple.Create(1, STuple.Create(2, 3)));
				var d = TuPack.DecodeKey<IVarTuple>(p)!;
				Assert.That(d.Get<IVarTuple>(1), Is.EqualTo(STuple.Create(2, 3)));
				Assert.That(d[1], Is.EqualTo(STuple.Create(2, 3)));
			}

			// every element type, decoded through Get<T> on the nested tuple
			{
				var uuid128 = Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f");
				var vs = VersionStamp.Incomplete();
				var inner = STuple.Create("hello", 123L, true, 4.5, uuid128, vs, Slice.FromString("bytes"));
				var p = TuPack.EncodeKey(STuple.Create(inner));
				var d = TuPack.DecodeKey<IVarTuple>(p)!.Get<IVarTuple>(0)!;
				Assert.That(d, Is.InstanceOf<SlicedTuple>());
				Assert.That(d.Get<string>(0), Is.EqualTo("hello"));
				Assert.That(d.Get<long>(1), Is.EqualTo(123L));
				Assert.That(d.Get<bool>(2), Is.True);
				Assert.That(d.Get<double>(3), Is.EqualTo(4.5));
				Assert.That(d.Get<Uuid128>(4), Is.EqualTo(uuid128));
				Assert.That(d.Get<VersionStamp>(5), Is.EqualTo(vs));
				Assert.That(d.Get<Slice>(6), Is.EqualTo(Slice.FromString("bytes")));
			}

			// a span input is copied: mutating the source array after decoding must not affect the result
			{
				var bytes = TuPack.EncodeKey(STuple.Create("x", 1L)).ToArray();
				var d = TuPack.DecodeKey<IVarTuple>(bytes.AsSpan())!;
				Array.Clear(bytes);
				Assert.That(d.Get<string>(0), Is.EqualTo("x"));
				Assert.That(d.Get<long>(1), Is.EqualTo(1L));
			}

			// SlicedTuple cannot be extended: pinned so a future reader finds it
			Assert.That(() => decoded.Append(1), Throws.InstanceOf<NotSupportedException>());
		}

		[Test]
		public void Test_TuplePack_SameBytes()
		{
			// two ways on packing the "same" tuple yield the same binary output
			{
				var expected = TuPack.EncodeKey("Hello World");
				Assert.That(TuPack.Pack(ValueTuple.Create("Hello World")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(["Hello World"])), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World"))), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234).Substring(0, 1)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(["Hello World", 1234]).Substring(0, 1)), Is.EqualTo(expected));
			}
			{
				var expected = TuPack.EncodeKey("Hello World", 1234);
				Assert.That(TuPack.Pack(("Hello World", 1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World", 1234))), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World").Append(1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World")).Append(1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(["Hello World", 1234])), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234, "Foo").Substring(0, 2)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(["Hello World", 1234, "Foo"]).Substring(0, 2)), Is.EqualTo(expected));
			}
			{
				var expected = TuPack.EncodeKey("Hello World", 1234, "Foo");
				Assert.That(TuPack.Pack(("Hello World", 1234, "Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234, "Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World", 1234, "Foo"))), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World").Append(1234).Append("Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World")).Append(1234).Append("Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(["Hello World", 1234, "Foo"])), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234, "Foo", "Bar").Substring(0, 3)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(["Hello World", 1234, "Foo", "Bar"]).Substring(0, 3)), Is.EqualTo(expected));
			}

			// also, there should be no differences between int,long,uint,... if they have the same value
			Assert.That(TuPack.Pack(("Hello", 123)), Is.EqualTo(TuPack.Pack(("Hello", 123L))));
			Assert.That(TuPack.Pack(STuple.Create("Hello", 123)), Is.EqualTo(TuPack.Pack(STuple.Create("Hello", 123L))));
			Assert.That(TuPack.Pack(STuple.Create("Hello", -123)), Is.EqualTo(TuPack.Pack(STuple.Create("Hello", -123L))));

			// GUID / UUID128 should pack the same way
			var g = Guid.NewGuid();
			Assert.That(TuPack.Pack(STuple.Create(g)), Is.EqualTo(TuPack.Pack(STuple.Create((Uuid128) g))), "GUID vs UUID128");
		}

		[Test]
		public void Test_TuplePack_Numbers_Are_Sorted_Lexicographically()
		{
			// pick two numbers 'x' and 'y' at random, and check that the order of 'x' compared to 'y' is the same as 'pack(tuple(x))' compared to 'pack(tuple(y))'

			// ie: ensure that x.CompareTo(y) always has the same sign as Tuple(x).CompareTo(Tuple(y))

			const int N = 1 * 1000 * 1000;
			var rnd = new Random();
			var sw = Stopwatch.StartNew();

			for (int i = 0; i < N; i++)
			{
				int x = rnd.Next() - 1073741824;
				int y = x;
				while (y == x)
				{
					y = rnd.Next() - 1073741824;
				}

				var t1 = TuPack.EncodeKey(x);
				var t2 = TuPack.EncodeKey(y);

				int dint = x.CompareTo(y);
				int dtup = t1.CompareTo(t2);

				if (dtup == 0) Assert.Fail($"Tuples for x={x} and y={y} should not have the same packed value");

				// compare signs
				if (Math.Sign(dint) != Math.Sign(dtup))
				{
					Assert.Fail($"Tuples for x={x} and y={y} are not sorted properly ({dint} / {dtup}): t(x)='{t1.ToString()}' and t(y)='{t2.ToString()}'");
				}
			}
			sw.Stop();
			Log($"Checked {N:N0} tuples in {sw.ElapsedMilliseconds:N1} ms");
		}

		#endregion

		[Test]
		public void Test_TuplePack_Pack()
		{
			Assert.That(
				TuPack.Pack(STuple.Create()),
				Is.EqualTo(Slice.Empty)
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world")).ToString(),
				Is.EqualTo("<02>hello world<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello", "world")).ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 1234, -1234)).ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 })).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界")).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
			Assert.That(
				TuPack.Pack(STuple.Create(["hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }])).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.FromArray(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }, 1, 2)).ToString(),
				Is.EqualTo("<15>{&")
			);
			Assert.That(
				TuPack.Pack(STuple.FromEnumerable(new List<object> { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } })).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
		}

		[Test]
		public void Test_TuplePack_Pack_With_Prefix()
		{
			var prefix = Slice.FromString("ABC");

			Assert.That(
				TuPack.Pack(prefix, STuple.Create()).ToString(),
				Is.EqualTo("ABC")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world")).ToString(),
				Is.EqualTo("ABC<02>hello world<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello", "world")).ToString(),
				Is.EqualTo("ABC<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 1234, -1234)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 })).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界")).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create(["hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }])).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.FromArray(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }, 1, 2)).ToString(),
				Is.EqualTo("ABC<15>{&")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.FromEnumerable(new List<object> { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } })).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);

			// Nil or Empty slice should be equivalent to no prefix
			Assert.That(
				TuPack.Pack(Slice.Nil, STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(Slice.Empty, STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
		}

		[Test]
		public void Test_TuplePack_TryPackTo()
		{
			static void Verify<TTuple>(in TTuple tuple, Slice expected)
				where TTuple : IVarTuple
			{
				Span<byte> buffer = stackalloc byte[512]; // larger than all tuples
				Assert.That(TuPack.TryPackTo(buffer, out int written, in tuple), Is.True);

				if (!expected.Equals(buffer.Slice(0, written)))
				{
					Assert.That(
						buffer.Slice(0, written).ToSlice(),
						Is.EqualTo(expected)
					);
				}
			}

			Verify(
				STuple.Create(),
				Slice.Empty
			);
			Verify(
				STuple.Create("hello world"),
				Slice.Unescape("<02>hello world<00>")
			);
			Verify(
				STuple.Create("hello", "world"),
				Slice.Unescape("<02>hello<00><02>world<00>")
			);
			Verify(
				STuple.Create("hello world", 123),
				Slice.Unescape("<02>hello world<00><15>{")
			);
			Verify(
				STuple.Create("hello world", 1234, -1234),
				Slice.Unescape("<02>hello world<00><16><04><D2><12><FB>-")
			);
			Verify(
				STuple.Create("hello world", 123, false),
				Slice.Unescape("<02>hello world<00><15>{&")
			);
			Verify(
				STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Verify(
				STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Verify(
				STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Verify(
				STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界"),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Verify(
				STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
			Verify(
				STuple.Create(["hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }]),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Verify(
				STuple.FromArray<object>(["hello world", 123, false, (byte[]) [123, 1, 66, 0, 42]], 1, 2),
				Slice.Unescape("<15>{&")
			);
			Verify(
				STuple.FromEnumerable<object>(["hello world", 123, false, (byte[]) [123, 1, 66, 0, 42]]),
				Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
		}

		[Test]
		public void Test_TuplePack_PackTuples()
		{
			{
				Slice[] slices;
				var tuples = new IVarTuple[]
				{
					STuple.Create("hello"),
					STuple.Create(123),
					STuple.Create(false),
					STuple.Create("world", 456, true)
				};

				// array version
				slices = TuPack.PackTuples(tuples);
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => TuPack.Pack(t))));

				// IEnumerable version that is passed an array
				slices = tuples.PackTuples();
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => TuPack.Pack(t))));

				// IEnumerable version but with a "real" enumerable
				slices = tuples.Select(t => t).PackTuples();
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => TuPack.Pack(t))));
			}

			//Optimized STuple<...> versions

			{ // ReadOnlySpan<IVarTuple> (different signatures)
				var packed = TuPack.PackTuples(
					STuple.Create("Hello"),
					STuple.Create(123, true),
					STuple.Create(Math.PI, -1234L)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>Hello<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<15>{'"));
				Assert.That(packed[2].ToString(), Is.EqualTo("!<C0><09>!<FB>TD-<18><12><FB>-"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{ // ReadOnlySpan<STuple<int>> (all the same signature)
				var packed = TuPack.PackTuples(
					STuple.Create(123),
					STuple.Create(456),
					STuple.Create(789)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<15>{"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<16><01><C8>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<16><03><15>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create(123, true),
					STuple.Create(456, false),
					STuple.Create(789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true),
					STuple.Create("bar", 456, false),
					STuple.Create("baz", 789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true, "yes"),
					STuple.Create("bar", 456, false, "yes"),
					STuple.Create("baz", 789, false, "no")
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'<02>yes<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&<02>yes<00>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&<02>no<00>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true, "yes", 7),
					STuple.Create("bar", 456, false, "yes", 42),
					STuple.Create("baz", 789, false, "no", 9)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'<02>yes<00><15><07>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&<02>yes<00><15>*"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&<02>no<00><15><09>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true, "yes", 7, 1.5d),
					STuple.Create("bar", 456, false, "yes", 42, 0.7d),
					STuple.Create("baz", 789, false, "no", 9, 0.66d)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'<02>yes<00><15><07>!<BF><F8><00><00><00><00><00><00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&<02>yes<00><15>*!<BF><E6>ffffff"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&<02>no<00><15><09>!<BF><E5><1E><B8>Q<EB><85><1F>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}
		}

		[Test]
		public void Test_TuplePack_PackTuples_With_Prefix()
		{
			var prefix = Slice.FromString("ABC");

			{
				Slice[] slices;
				var tuples = new IVarTuple[]
				{
					STuple.Create("hello"),
					STuple.Create(123),
					STuple.Create(false),
					STuple.Create("world", 456, true)
				};

				// array version
				slices = TuPack.PackTuples(prefix, tuples);
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => prefix + TuPack.Pack(t))));

				// LINQ version
				slices = TuPack.PackTuples(prefix, tuples.Select(x => x));
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => prefix + TuPack.Pack(t))));

			}

			//Optimized STuple<...> versions

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create("Hello"),
					STuple.Create(123, true),
					STuple.Create(Math.PI, -1234L)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<02>Hello<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<15>{'"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC!<C0><09>!<FB>TD-<18><12><FB>-"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create(123),
					STuple.Create(456),
					STuple.Create(789)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<15>{"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<16><01><C8>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<16><03><15>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create(123, true),
					STuple.Create(456, false),
					STuple.Create(789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create("foo", 123, true),
					STuple.Create("bar", 456, false),
					STuple.Create("baz", 789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<02>foo<00><15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<02>bar<00><16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<02>baz<00><16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same buffer");
			}
		}

		[Test]
		public void Test_TuplePack_EncodeKey()
		{
			Assert.That(
				TuPack.EncodeKey("hello world").ToString(),
				Is.EqualTo("<02>hello world<00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello", "world").ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 1234, -1234).ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界").ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
		}

		[Test]
		public void Test_TuplePack_EncodeKey_With_Prefix()
		{
			var prefix = Slice.FromString("ABC");

			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world").ToString(),
				Is.EqualTo("ABC<02>hello world<00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello", "world").ToString(),
				Is.EqualTo("ABC<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 1234, -1234).ToString(),
				Is.EqualTo("ABC<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界").ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
		}

		[Test]
		public void Test_TuplePack_EncodeKey_Boxed()
		{
			Slice slice;

			slice = TuPack.EncodeKey<object?>(default);
			Assert.That(slice.ToString(), Is.EqualTo("<00>"));

			slice = TuPack.EncodeKey<object>(1);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(1L);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(1U);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(1UL);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(false);
			Assert.That(slice.ToString(), Is.EqualTo("&"));

			slice = TuPack.EncodeKey<object>(true);
			Assert.That(slice.ToString(), Is.EqualTo("'"));

			slice = TuPack.EncodeKey<object>(new byte[] { 4, 5, 6 });
			Assert.That(slice.ToString(), Is.EqualTo("<01><04><05><06><00>"));

			slice = TuPack.EncodeKey<object>("hello");
			Assert.That(slice.ToString(), Is.EqualTo("<02>hello<00>"));
		}

		[Test]
		public void Test_TuplePack_Unpack()
		{
			var packed = TuPack.EncodeKey("hello world");
			Log(packed);

			var tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));

			packed = TuPack.EncodeKey("hello world", 123);
			Log(packed);

			tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(2));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(123));

			packed = TuPack.EncodeKey(1, 256, 257, 65536, int.MaxValue, long.MaxValue);
			Log(packed);

			tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple.Get<int>(0), Is.EqualTo(1));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(256));
			Assert.That(tuple.Get<int>(2), Is.EqualTo(257), ((SlicedTuple) tuple).GetSlice(2).ToString());
			Assert.That(tuple.Get<int>(3), Is.EqualTo(65536));
			Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MaxValue));
			Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MaxValue));

			packed = TuPack.EncodeKey(-1, -256, -257, -65536, int.MinValue, long.MinValue);
			Log(packed);

			tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple, Is.InstanceOf<SlicedTuple>());
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple.Get<int>(0), Is.EqualTo(-1));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(-256));
			Assert.That(tuple.Get<int>(2), Is.EqualTo(-257), "Slice is " + ((SlicedTuple) tuple).GetSlice(2).ToString());
			Assert.That(tuple.Get<int>(3), Is.EqualTo(-65536));
			Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MinValue));
			Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MinValue));
		}

		[Test]
		public void Test_SlicedTuple_Unpack()
		{
			// note: this method is equivalent to TuPack.UnPack, but exposes the concrete SlicedTuple types, and gives more inside into the items

			{
				var packed = TuPack.EncodeKey("hello world");
				Dump("Packed", packed);

				var tuple = SlicedTuple.Unpack(packed);
				Assert.That(tuple, Is.Not.Null);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(1));
				Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
				Assert.That(tuple.Get<string>(^1), Is.EqualTo("hello world"));
				Assert.That(tuple.GetFirst<string>(), Is.EqualTo("hello world"));
				Assert.That(tuple.GetLast<string>(), Is.EqualTo("hello world"));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.Unescape("<02>hello world<00>")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.UnicodeString));
				Assert.That(tuple.IsUnicodeString(0), Is.True);
				Assert.That(tuple.IsBytes(0), Is.False);
				Assert.That(tuple.IsNumber(0), Is.False);
			}

			{
				var packed = TuPack.EncodeKey("hello world", 123);
				Dump("Packed", packed);

				var tuple = SlicedTuple.Unpack(packed);
				Assert.That(tuple, Is.Not.Null);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(2));
				Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
				Assert.That(tuple.Get<int>(1), Is.EqualTo(123));
				Assert.That(tuple.Get<string>(^2), Is.EqualTo("hello world"));
				Assert.That(tuple.Get<int>(^1), Is.EqualTo(123));
				Assert.That(tuple.GetFirst<string>(), Is.EqualTo("hello world"));
				Assert.That(tuple.GetLast<int>(), Is.EqualTo(123));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.Unescape("<02>hello world<00>")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("15 7B")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.UnicodeString));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.IsUnicodeString(0), Is.True);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsNumber(0), Is.False);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsInteger(0), Is.False);
				Assert.That(tuple.IsInteger(1), Is.True);
				Assert.That(tuple.IsFloatingPoint(0), Is.False);
				Assert.That(tuple.IsFloatingPoint(1), Is.False);
			}

			{
				var packed = TuPack.EncodeKey(1, 256, 257, 65536, int.MaxValue, long.MaxValue);
				Dump("Packed", packed);

				var tuple = SlicedTuple.Unpack(packed);
				Assert.That(tuple, Is.Not.Null);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(6));
				Assert.That(tuple.Get<int>(0), Is.EqualTo(1));
				Assert.That(tuple.Get<int>(1), Is.EqualTo(256));
				Assert.That(tuple.Get<int>(2), Is.EqualTo(257), tuple.GetSlice(2).ToString());
				Assert.That(tuple.Get<int>(3), Is.EqualTo(65536));
				Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MaxValue));
				Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MaxValue));
				Assert.That(tuple.Get<int>(^6), Is.EqualTo(1));
				Assert.That(tuple.Get<int>(^5), Is.EqualTo(256));
				Assert.That(tuple.Get<int>(^4), Is.EqualTo(257), tuple.GetSlice(2).ToString());
				Assert.That(tuple.Get<int>(^3), Is.EqualTo(65536));
				Assert.That(tuple.Get<int>(^2), Is.EqualTo(int.MaxValue));
				Assert.That(tuple.Get<long>(^1), Is.EqualTo(long.MaxValue));
				Assert.That(tuple.GetFirst<int>(), Is.EqualTo(1));
				Assert.That(tuple.GetLast<long>(), Is.EqualTo(long.MaxValue));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.FromHexa("15 01")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("16 01 00")));
				Assert.That(tuple.GetSlice(2), Is.EqualTo(Slice.FromHexa("16 01 01")));
				Assert.That(tuple.GetSlice(3), Is.EqualTo(Slice.FromHexa("17 01 00 00")));
				Assert.That(tuple.GetSlice(4), Is.EqualTo(Slice.FromHexa("18 7F FF FF FF")));
				Assert.That(tuple.GetSlice(5), Is.EqualTo(Slice.FromHexa("1C 7F FF FF FF FF FF FF FF")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(2), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(3), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(4), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(5), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.IsUnicodeString(0), Is.False);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsUnicodeString(2), Is.False);
				Assert.That(tuple.IsUnicodeString(3), Is.False);
				Assert.That(tuple.IsUnicodeString(4), Is.False);
				Assert.That(tuple.IsUnicodeString(5), Is.False);
				Assert.That(tuple.IsNumber(0), Is.True);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsNumber(2), Is.True);
				Assert.That(tuple.IsNumber(3), Is.True);
				Assert.That(tuple.IsNumber(4), Is.True);
				Assert.That(tuple.IsNumber(5), Is.True);
			}

			{
				var packed = TuPack.EncodeKey(-1, -256, -257, -65536, int.MinValue, long.MinValue);
				Dump("Packed", packed);

				var tuple = SlicedTuple.Unpack(packed);
				Assert.That(tuple, Is.Not.Null);
				Assert.That(tuple, Is.InstanceOf<SlicedTuple>());
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(6));
				Assert.That(tuple.Get<int>(0), Is.EqualTo(-1));
				Assert.That(tuple.Get<int>(1), Is.EqualTo(-256));
				Assert.That(tuple.Get<int>(2), Is.EqualTo(-257), $"Slice is {tuple.GetSlice(2)}");
				Assert.That(tuple.Get<int>(3), Is.EqualTo(-65536));
				Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MinValue));
				Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MinValue));
				Assert.That(tuple.Get<int>(^6), Is.EqualTo(-1));
				Assert.That(tuple.Get<int>(^5), Is.EqualTo(-256));
				Assert.That(tuple.Get<int>(^4), Is.EqualTo(-257), $"Slice is {tuple.GetSlice(2)}");
				Assert.That(tuple.Get<int>(^3), Is.EqualTo(-65536));
				Assert.That(tuple.Get<int>(^2), Is.EqualTo(int.MinValue));
				Assert.That(tuple.Get<long>(^1), Is.EqualTo(long.MinValue));
				Assert.That(tuple.GetFirst<int>(), Is.EqualTo(-1));
				Assert.That(tuple.GetLast<long>(), Is.EqualTo(long.MinValue));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.FromHexa("13 FE")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("12 FE FF")));
				Assert.That(tuple.GetSlice(2), Is.EqualTo(Slice.FromHexa("12 FE FE")));
				Assert.That(tuple.GetSlice(3), Is.EqualTo(Slice.FromHexa("11 FE FF FF")));
				Assert.That(tuple.GetSlice(4), Is.EqualTo(Slice.FromHexa("10 7F FF FF FF")));
				Assert.That(tuple.GetSlice(5), Is.EqualTo(Slice.FromHexa("0C 7F FF FF FF FF FF FF FF")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(2), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(3), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(4), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(5), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.IsUnicodeString(0), Is.False);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsUnicodeString(2), Is.False);
				Assert.That(tuple.IsUnicodeString(3), Is.False);
				Assert.That(tuple.IsUnicodeString(4), Is.False);
				Assert.That(tuple.IsUnicodeString(5), Is.False);
				Assert.That(tuple.IsNumber(0), Is.True);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsNumber(2), Is.True);
				Assert.That(tuple.IsNumber(3), Is.True);
				Assert.That(tuple.IsNumber(4), Is.True);
				Assert.That(tuple.IsNumber(5), Is.True);
			}

			{
				var packed = TuPack.EncodeKey(0.0, 0.0f, 1.0, 1.0f, Math.PI, (float) Math.PI);
				Dump("Packed", packed);

				var tuple = SlicedTuple.Unpack(packed);
				Assert.That(tuple, Is.Not.Null);
				Assert.That(tuple, Is.InstanceOf<SlicedTuple>());
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(6));
				Assert.That(tuple.Get<double>(0), Is.EqualTo(0));
				Assert.That(tuple.Get<float>(1), Is.EqualTo(0));
				Assert.That(tuple.Get<double>(2), Is.EqualTo(1));
				Assert.That(tuple.Get<float>(3), Is.EqualTo(1));
				Assert.That(tuple.Get<double>(4), Is.EqualTo(Math.PI));
				Assert.That(tuple.Get<float>(5), Is.EqualTo((float) Math.PI));
				Assert.That(tuple.Get<double>(^6), Is.EqualTo(0));
				Assert.That(tuple.Get<float>(^5), Is.EqualTo(0));
				Assert.That(tuple.Get<double>(^4), Is.EqualTo(1));
				Assert.That(tuple.Get<float>(^3), Is.EqualTo(1));
				Assert.That(tuple.Get<double>(^2), Is.EqualTo(Math.PI));
				Assert.That(tuple.Get<float>(^1), Is.EqualTo((float) Math.PI));
				Assert.That(tuple.GetFirst<double>(), Is.EqualTo(0.0));
				Assert.That(tuple.GetLast<float>(), Is.EqualTo((float) Math.PI));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.FromHexa("21 80 00 00 00 00 00 00 00")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("20 80 00 00 00")));
				Assert.That(tuple.GetSlice(2), Is.EqualTo(Slice.FromHexa("21 BF F0 00 00 00 00 00 00")));
				Assert.That(tuple.GetSlice(3), Is.EqualTo(Slice.FromHexa("20 BF 80 00 00")));
				Assert.That(tuple.GetSlice(4), Is.EqualTo(Slice.FromHexa("21 C0 09 21 FB 54 44 2D 18")));
				Assert.That(tuple.GetSlice(5), Is.EqualTo(Slice.FromHexa("20 C0 49 0F DB")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.Double));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Single));
				Assert.That(tuple.GetElementType(2), Is.EqualTo(TupleSegmentType.Double));
				Assert.That(tuple.GetElementType(3), Is.EqualTo(TupleSegmentType.Single));
				Assert.That(tuple.GetElementType(4), Is.EqualTo(TupleSegmentType.Double));
				Assert.That(tuple.GetElementType(5), Is.EqualTo(TupleSegmentType.Single));
				Assert.That(tuple.IsUnicodeString(0), Is.False);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsUnicodeString(2), Is.False);
				Assert.That(tuple.IsUnicodeString(3), Is.False);
				Assert.That(tuple.IsUnicodeString(4), Is.False);
				Assert.That(tuple.IsUnicodeString(5), Is.False);
				Assert.That(tuple.IsNumber(0), Is.True);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsNumber(2), Is.True);
				Assert.That(tuple.IsNumber(3), Is.True);
				Assert.That(tuple.IsNumber(4), Is.True);
				Assert.That(tuple.IsNumber(5), Is.True);
				Assert.That(tuple.IsInteger(0), Is.False);
				Assert.That(tuple.IsInteger(1), Is.False);
				Assert.That(tuple.IsInteger(2), Is.False);
				Assert.That(tuple.IsInteger(3), Is.False);
				Assert.That(tuple.IsInteger(4), Is.False);
				Assert.That(tuple.IsInteger(5), Is.False);
				Assert.That(tuple.IsFloatingPoint(0), Is.True);
				Assert.That(tuple.IsFloatingPoint(1), Is.True);
				Assert.That(tuple.IsFloatingPoint(2), Is.True);
				Assert.That(tuple.IsFloatingPoint(3), Is.True);
				Assert.That(tuple.IsFloatingPoint(4), Is.True);
				Assert.That(tuple.IsFloatingPoint(5), Is.True);
				Assert.That(tuple.IsSingle(0), Is.False);
				Assert.That(tuple.IsSingle(1), Is.True);
				Assert.That(tuple.IsSingle(2), Is.False);
				Assert.That(tuple.IsSingle(3), Is.True);
				Assert.That(tuple.IsSingle(4), Is.False);
				Assert.That(tuple.IsSingle(5), Is.True);
				Assert.That(tuple.IsDouble(0), Is.True);
				Assert.That(tuple.IsDouble(1), Is.False);
				Assert.That(tuple.IsDouble(2), Is.True);
				Assert.That(tuple.IsDouble(3), Is.False);
				Assert.That(tuple.IsDouble(4), Is.True);
				Assert.That(tuple.IsDouble(5), Is.False);
			}
		}

		[Test]
		public void Test_SpanTuple_Unpack()
		{
			// note: this method is equivalent to TuPack.UnPack, but exposes the concrete SpanTuple types, and gives more inside into the items

			{
				var packed = TuPack.EncodeKey("hello world");
				Dump("Packed", packed);

				var tuple = SpanTuple.Unpack(packed);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(1));
				Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
				Assert.That(tuple.Get<string>(^1), Is.EqualTo("hello world"));
				Assert.That(tuple.GetFirst<string>(), Is.EqualTo("hello world"));
				Assert.That(tuple.GetLast<string>(), Is.EqualTo("hello world"));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.Unescape("<02>hello world<00>")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.UnicodeString));
				Assert.That(tuple.IsUnicodeString(0), Is.True);
				Assert.That(tuple.IsBytes(0), Is.False);
				Assert.That(tuple.IsNumber(0), Is.False);
			}

			{
				var packed = TuPack.EncodeKey("hello world", 123);
				Dump("Packed", packed);

				var tuple = SpanTuple.Unpack(packed);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(2));
				Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
				Assert.That(tuple.Get<int>(1), Is.EqualTo(123));
				Assert.That(tuple.Get<string>(^2), Is.EqualTo("hello world"));
				Assert.That(tuple.Get<int>(^1), Is.EqualTo(123));
				Assert.That(tuple.GetFirst<string>(), Is.EqualTo("hello world"));
				Assert.That(tuple.GetLast<int>(), Is.EqualTo(123));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.Unescape("<02>hello world<00>")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("15 7B")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.UnicodeString));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.IsUnicodeString(0), Is.True);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsNumber(0), Is.False);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsInteger(0), Is.False);
				Assert.That(tuple.IsInteger(1), Is.True);
				Assert.That(tuple.IsFloatingPoint(0), Is.False);
				Assert.That(tuple.IsFloatingPoint(1), Is.False);
			}

			{
				var packed = TuPack.EncodeKey(1, 256, 257, 65536, int.MaxValue, long.MaxValue);
				Dump("Packed", packed);

				var tuple = SpanTuple.Unpack(packed);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(6));
				Assert.That(tuple.Get<int>(0), Is.EqualTo(1));
				Assert.That(tuple.Get<int>(1), Is.EqualTo(256));
				Assert.That(tuple.Get<int>(2), Is.EqualTo(257), tuple.GetSlice(2).ToString());
				Assert.That(tuple.Get<int>(3), Is.EqualTo(65536));
				Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MaxValue));
				Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MaxValue));
				Assert.That(tuple.Get<int>(^6), Is.EqualTo(1));
				Assert.That(tuple.Get<int>(^5), Is.EqualTo(256));
				Assert.That(tuple.Get<int>(^4), Is.EqualTo(257), tuple.GetSlice(2).ToString());
				Assert.That(tuple.Get<int>(^3), Is.EqualTo(65536));
				Assert.That(tuple.Get<int>(^2), Is.EqualTo(int.MaxValue));
				Assert.That(tuple.Get<long>(^1), Is.EqualTo(long.MaxValue));
				Assert.That(tuple.GetFirst<int>(), Is.EqualTo(1));
				Assert.That(tuple.GetLast<long>(), Is.EqualTo(long.MaxValue));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.FromHexa("15 01")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("16 01 00")));
				Assert.That(tuple.GetSlice(2), Is.EqualTo(Slice.FromHexa("16 01 01")));
				Assert.That(tuple.GetSlice(3), Is.EqualTo(Slice.FromHexa("17 01 00 00")));
				Assert.That(tuple.GetSlice(4), Is.EqualTo(Slice.FromHexa("18 7F FF FF FF")));
				Assert.That(tuple.GetSlice(5), Is.EqualTo(Slice.FromHexa("1C 7F FF FF FF FF FF FF FF")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(2), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(3), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(4), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(5), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.IsUnicodeString(0), Is.False);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsUnicodeString(2), Is.False);
				Assert.That(tuple.IsUnicodeString(3), Is.False);
				Assert.That(tuple.IsUnicodeString(4), Is.False);
				Assert.That(tuple.IsUnicodeString(5), Is.False);
				Assert.That(tuple.IsNumber(0), Is.True);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsNumber(2), Is.True);
				Assert.That(tuple.IsNumber(3), Is.True);
				Assert.That(tuple.IsNumber(4), Is.True);
				Assert.That(tuple.IsNumber(5), Is.True);
			}

			{
				var packed = TuPack.EncodeKey(-1, -256, -257, -65536, int.MinValue, long.MinValue);
				Dump("Packed", packed);

				var tuple = SpanTuple.Unpack(packed);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(6));
				Assert.That(tuple.Get<int>(0), Is.EqualTo(-1));
				Assert.That(tuple.Get<int>(1), Is.EqualTo(-256));
				Assert.That(tuple.Get<int>(2), Is.EqualTo(-257), $"Slice is {tuple.GetSlice(2)}");
				Assert.That(tuple.Get<int>(3), Is.EqualTo(-65536));
				Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MinValue));
				Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MinValue));
				Assert.That(tuple.Get<int>(^6), Is.EqualTo(-1));
				Assert.That(tuple.Get<int>(^5), Is.EqualTo(-256));
				Assert.That(tuple.Get<int>(^4), Is.EqualTo(-257), $"Slice is {tuple.GetSlice(2)}");
				Assert.That(tuple.Get<int>(^3), Is.EqualTo(-65536));
				Assert.That(tuple.Get<int>(^2), Is.EqualTo(int.MinValue));
				Assert.That(tuple.Get<long>(^1), Is.EqualTo(long.MinValue));
				Assert.That(tuple.GetFirst<int>(), Is.EqualTo(-1));
				Assert.That(tuple.GetLast<long>(), Is.EqualTo(long.MinValue));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.FromHexa("13 FE")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("12 FE FF")));
				Assert.That(tuple.GetSlice(2), Is.EqualTo(Slice.FromHexa("12 FE FE")));
				Assert.That(tuple.GetSlice(3), Is.EqualTo(Slice.FromHexa("11 FE FF FF")));
				Assert.That(tuple.GetSlice(4), Is.EqualTo(Slice.FromHexa("10 7F FF FF FF")));
				Assert.That(tuple.GetSlice(5), Is.EqualTo(Slice.FromHexa("0C 7F FF FF FF FF FF FF FF")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(2), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(3), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(4), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.GetElementType(5), Is.EqualTo(TupleSegmentType.Integer));
				Assert.That(tuple.IsUnicodeString(0), Is.False);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsUnicodeString(2), Is.False);
				Assert.That(tuple.IsUnicodeString(3), Is.False);
				Assert.That(tuple.IsUnicodeString(4), Is.False);
				Assert.That(tuple.IsUnicodeString(5), Is.False);
				Assert.That(tuple.IsNumber(0), Is.True);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsNumber(2), Is.True);
				Assert.That(tuple.IsNumber(3), Is.True);
				Assert.That(tuple.IsNumber(4), Is.True);
				Assert.That(tuple.IsNumber(5), Is.True);
			}

			{
				var packed = TuPack.EncodeKey(0.0, 0.0f, 1.0, 1.0f, Math.PI, (float) Math.PI);
				Dump("Packed", packed);

				var tuple = SpanTuple.Unpack(packed);
				Dump("Unpacked", tuple);

				Assert.That(tuple.Count, Is.EqualTo(6));
				Assert.That(tuple.Get<double>(0), Is.EqualTo(0));
				Assert.That(tuple.Get<float>(1), Is.EqualTo(0));
				Assert.That(tuple.Get<double>(2), Is.EqualTo(1));
				Assert.That(tuple.Get<float>(3), Is.EqualTo(1));
				Assert.That(tuple.Get<double>(4), Is.EqualTo(Math.PI));
				Assert.That(tuple.Get<float>(5), Is.EqualTo((float) Math.PI));
				Assert.That(tuple.Get<double>(^6), Is.EqualTo(0));
				Assert.That(tuple.Get<float>(^5), Is.EqualTo(0));
				Assert.That(tuple.Get<double>(^4), Is.EqualTo(1));
				Assert.That(tuple.Get<float>(^3), Is.EqualTo(1));
				Assert.That(tuple.Get<double>(^2), Is.EqualTo(Math.PI));
				Assert.That(tuple.Get<float>(^1), Is.EqualTo((float) Math.PI));
				Assert.That(tuple.GetFirst<double>(), Is.EqualTo(0.0));
				Assert.That(tuple.GetLast<float>(), Is.EqualTo((float) Math.PI));
				Assert.That(tuple.GetSlice(0), Is.EqualTo(Slice.FromHexa("21 80 00 00 00 00 00 00 00")));
				Assert.That(tuple.GetSlice(1), Is.EqualTo(Slice.FromHexa("20 80 00 00 00")));
				Assert.That(tuple.GetSlice(2), Is.EqualTo(Slice.FromHexa("21 BF F0 00 00 00 00 00 00")));
				Assert.That(tuple.GetSlice(3), Is.EqualTo(Slice.FromHexa("20 BF 80 00 00")));
				Assert.That(tuple.GetSlice(4), Is.EqualTo(Slice.FromHexa("21 C0 09 21 FB 54 44 2D 18")));
				Assert.That(tuple.GetSlice(5), Is.EqualTo(Slice.FromHexa("20 C0 49 0F DB")));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				Assert.That(tuple.GetElementType(0), Is.EqualTo(TupleSegmentType.Double));
				Assert.That(tuple.GetElementType(1), Is.EqualTo(TupleSegmentType.Single));
				Assert.That(tuple.GetElementType(2), Is.EqualTo(TupleSegmentType.Double));
				Assert.That(tuple.GetElementType(3), Is.EqualTo(TupleSegmentType.Single));
				Assert.That(tuple.GetElementType(4), Is.EqualTo(TupleSegmentType.Double));
				Assert.That(tuple.GetElementType(5), Is.EqualTo(TupleSegmentType.Single));
				Assert.That(tuple.IsUnicodeString(0), Is.False);
				Assert.That(tuple.IsUnicodeString(1), Is.False);
				Assert.That(tuple.IsUnicodeString(2), Is.False);
				Assert.That(tuple.IsUnicodeString(3), Is.False);
				Assert.That(tuple.IsUnicodeString(4), Is.False);
				Assert.That(tuple.IsUnicodeString(5), Is.False);
				Assert.That(tuple.IsNumber(0), Is.True);
				Assert.That(tuple.IsNumber(1), Is.True);
				Assert.That(tuple.IsNumber(2), Is.True);
				Assert.That(tuple.IsNumber(3), Is.True);
				Assert.That(tuple.IsNumber(4), Is.True);
				Assert.That(tuple.IsNumber(5), Is.True);
				Assert.That(tuple.IsInteger(0), Is.False);
				Assert.That(tuple.IsInteger(1), Is.False);
				Assert.That(tuple.IsInteger(2), Is.False);
				Assert.That(tuple.IsInteger(3), Is.False);
				Assert.That(tuple.IsInteger(4), Is.False);
				Assert.That(tuple.IsInteger(5), Is.False);
				Assert.That(tuple.IsFloatingPoint(0), Is.True);
				Assert.That(tuple.IsFloatingPoint(1), Is.True);
				Assert.That(tuple.IsFloatingPoint(2), Is.True);
				Assert.That(tuple.IsFloatingPoint(3), Is.True);
				Assert.That(tuple.IsFloatingPoint(4), Is.True);
				Assert.That(tuple.IsFloatingPoint(5), Is.True);
				Assert.That(tuple.IsSingle(0), Is.False);
				Assert.That(tuple.IsSingle(1), Is.True);
				Assert.That(tuple.IsSingle(2), Is.False);
				Assert.That(tuple.IsSingle(3), Is.True);
				Assert.That(tuple.IsSingle(4), Is.False);
				Assert.That(tuple.IsSingle(5), Is.True);
				Assert.That(tuple.IsDouble(0), Is.True);
				Assert.That(tuple.IsDouble(1), Is.False);
				Assert.That(tuple.IsDouble(2), Is.True);
				Assert.That(tuple.IsDouble(3), Is.False);
				Assert.That(tuple.IsDouble(4), Is.True);
				Assert.That(tuple.IsDouble(5), Is.False);
			}
		}

		[Test]
		public void Test_SpanTuple_Unpack_With_Buffer()
		{
			// unpacking with a caller-supplied buffer must produce the same result as the allocating path,
			// both when the buffer is large enough and when it is too small and the decoder falls back to the heap

			static void Check(SpanTuple tuple, SpanTuple reference, int count, Slice packed, IVarTuple expected)
			{
				Assert.That(tuple.Count, Is.EqualTo(count));
				Assert.That(tuple.ToSlice(), Is.EqualTo(packed));
				for (int i = 0; i < count; i++)
				{
					Assert.That(tuple.GetSlice(i), Is.EqualTo(reference.GetSlice(i)), $"element {i}");
					Assert.That(tuple.GetElementType(i), Is.EqualTo(reference.GetElementType(i)), $"element {i}");
				}
				Assert.That(tuple.ToTuple(), Is.EqualTo(expected));
			}

			static void Verify(Slice packed)
			{
				var reference = SpanTuple.Unpack(packed.Span);
				int count = reference.Count;
				var expected = TuPack.Unpack(packed);

				Check(SpanTuple.Unpack(packed.Span, stackalloc Range[8]), reference, count, packed, expected);
				Check(SpanTuple.Unpack(packed.Span, stackalloc Range[1]), reference, count, packed, expected); // forces growth for any tuple with 2 or more elements
				Check(TuPack.Unpack(packed.Span, stackalloc Range[8]), reference, count, packed, expected);

				Assert.That(TuPack.TryUnpack(packed.Span, stackalloc Range[8], out var tried), Is.True);
				Assert.That(tried.Count, Is.EqualTo(count));
			}

			// empty tuple
			Verify(Slice.Empty);

			// one element
			Verify(TuPack.EncodeKey("hello world"));

			// exactly 8 elements (fills stackalloc Range[8] with no growth)
			Verify(TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8));
			// 9 elements (one growth)
			Verify(TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8, 9));
			// 20 elements (three growths)
			Verify(TuPack.Pack(STuple.Create(Enumerable.Range(1, 20).Select(i => (object?) i).ToArray())));

			// mixed integer sizes, positive and negative
			Verify(TuPack.EncodeKey(1, 256, 257, 65536, int.MaxValue, long.MaxValue));
			Verify(TuPack.EncodeKey(-1, -256, -257, -65536, int.MinValue, long.MinValue));

			// one element of every type
			Verify(TuPack.EncodeKey(default(string)));
			Verify(TuPack.EncodeKey(true));
			Verify(TuPack.EncodeKey(false));
			Verify(TuPack.EncodeKey(1.5f));
			Verify(TuPack.EncodeKey(1.5d));
			Verify(TuPack.EncodeKey("héllo wörld 日本語"));
			Verify(TuPack.EncodeKey(Slice.FromString("some bytes")));
			Verify(TuPack.EncodeKey(Guid.NewGuid()));
			Verify(TuPack.EncodeKey(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Verify(TuPack.EncodeKey(Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));
			Verify(TuPack.EncodeKey(Uuid80.Parse("0123-456789AB-CDEF55AA")));
			Verify(TuPack.EncodeKey(Uuid64.Parse("01234567-89ABCDEF")));
			Verify(TuPack.EncodeKey(VersionStamp.Incomplete()));
			Verify(TuPack.EncodeKey(new BigInteger(0x0123456789ABCDEF)));

			// nested tuples
			Verify(TuPack.Pack(STuple.Create(42, STuple.Create(2014, 11, 6), "Hello", true)));
			Verify(Slice.FromHexa("05 00 FF 00 00")); // (("", ),) with the inner null escaped
			Verify(Slice.FromHexa("00 05 00 00 05 00 FF 00 00"));
			Verify(Slice.Unescape("<05><00>")); // ((),) : an embedded empty tuple

			// malformed input: drop the string terminator so parsing fails before reaching the end of the key
			{
				var bad = TuPack.EncodeKey("hello");
				bad = bad[..^1];

				Type? expectedType = null;
				try
				{
					SpanTuple.Unpack(bad.Span);
					Assert.Fail("expected the reference unpack to throw");
				}
				catch (Exception e)
				{
					expectedType = e.GetType();
				}

				Exception? caught = null;
				try
				{
					SpanTuple.Unpack(bad.Span, (Span<Range>) stackalloc Range[8]);
					Assert.Fail("expected the buffered unpack to throw");
				}
				catch (Exception e)
				{
					caught = e;
				}
				Assert.That(caught, Is.Not.Null);
				Assert.That(caught!.GetType(), Is.EqualTo(expectedType));

				Assert.That(TuPack.TryUnpack(bad.Span, out _), Is.False);
				Assert.That(TuPack.TryUnpack(bad.Span, stackalloc Range[8], out _), Is.False);
			}
		}

		[Test]
		public void Test_TuplePack_DecodeKey()
		{
			Assert.That(
				TuPack.DecodeKey<string>(Slice.Unescape("<02>hello world<00>")),
				Is.EqualTo("hello world")
			);
			Assert.That(
				TuPack.DecodeKey<string>("\u0002hello world\0"u8),
				Is.EqualTo("hello world")
			);
			Assert.That(
				TuPack.DecodeKey<int>(Slice.Unescape("<15>{")),
				Is.EqualTo(123)
			);
			Assert.That(
				TuPack.DecodeKey<int>("\x15\x7B"u8),
				Is.EqualTo(123)
			);
			Assert.That(
				TuPack.DecodeKey<string, string>(Slice.Unescape("<02>hello<00><02>world<00>")),
				Is.EqualTo(("hello", "world"))
			);
			Assert.That(
				TuPack.DecodeKey<string, int>(Slice.Unescape("<02>hello world<00><15>{")),
				Is.EqualTo(("hello world", 123))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, long>(Slice.Unescape("<02>hello world<00><16><04><D2><12><FB>-")),
				Is.EqualTo(("hello world", 1234, -1234L))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool>(Slice.Unescape("<02>hello world<00><15>{&")),
				Is.EqualTo(("hello world", 123, false))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")),
				Is.EqualTo(("hello world", 123, false, Slice.FromBytes([123, 1, 66, 0, 42])))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice, double>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")),
				Is.EqualTo(("hello world", 123, false, Slice.FromBytes([123, 1, 66, 0, 42]), Math.PI))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice, double, long>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")),
				Is.EqualTo(("hello world", 123, false, Slice.FromBytes([123, 1, 66, 0, 42]), Math.PI, -1234L))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice, double, long, string>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")),
				Is.EqualTo(("hello world", 123, false, Slice.FromBytes([123, 1, 66, 0, 42]), Math.PI, -1234L, "こんにちは世界"))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice, double, long, string, bool>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00><15><01>")),
				Is.EqualTo(("hello world", 123, false, Slice.FromBytes([123, 1, 66, 0, 42]), Math.PI, -1234L, "こんにちは世界", true))
			);
		}

		[Test]
		public void Test_TuplePack_Unpack_First_And_Last()
		{
			// should only work with tuples having at least one element

			Slice packed;

			packed = TuPack.EncodeKey(1);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(() => TuPack.DecodeFirst<int, int>(packed), Throws.Exception);
			Assert.That(() => TuPack.DecodeLast<int, int>(packed), Throws.Exception);
			Assert.That(() => TuPack.DecodeFirst<int, int, int>(packed), Throws.Exception);
			Assert.That(() => TuPack.DecodeLast<int, int, int>(packed), Throws.Exception);

			packed = TuPack.EncodeKey(1, 2);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(2));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("2"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(2));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("2"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(() => TuPack.DecodeFirst<int, int, int>(packed), Throws.Exception);
			Assert.That(() => TuPack.DecodeLast<int, int, int>(packed), Throws.Exception);

			packed = TuPack.EncodeKey(1, 2, 3);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(3));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("3"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((2, 3)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(3));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("3"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((2, 3)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));

			packed = TuPack.EncodeKey(1, 2, 3, 4);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(4));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("4"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((3, 4)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed), Is.EqualTo((2, 3, 4)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(4));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("4"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((3, 4)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed.Span), Is.EqualTo((2, 3, 4)));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(5));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("5"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((4, 5)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed), Is.EqualTo((3, 4, 5)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(5));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("5"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((4, 5)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed.Span), Is.EqualTo((3, 4, 5)));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(6));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("6"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((5, 6)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed), Is.EqualTo((4, 5, 6)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(6));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("6"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((5, 6)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed.Span), Is.EqualTo((4, 5, 6)));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(7));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("7"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((6, 7)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed), Is.EqualTo((5, 6, 7)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(7));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("7"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((6, 7)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed.Span), Is.EqualTo((5, 6, 7)));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(8));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("8"));
			Assert.That(TuPack.DecodeLast<int, int>(packed), Is.EqualTo((7, 8)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed), Is.EqualTo((6, 7, 8)));
			Assert.That(TuPack.DecodeFirst<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed.Span), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeFirst<int, int>(packed.Span), Is.EqualTo((1, 2)));
			Assert.That(TuPack.DecodeFirst<int, int, int>(packed.Span), Is.EqualTo((1, 2, 3)));
			Assert.That(TuPack.DecodeLast<int>(packed.Span), Is.EqualTo(8));
			Assert.That(TuPack.DecodeLast<string>(packed.Span), Is.EqualTo("8"));
			Assert.That(TuPack.DecodeLast<int, int>(packed.Span), Is.EqualTo((7, 8)));
			Assert.That(TuPack.DecodeLast<int, int, int>(packed.Span), Is.EqualTo((6, 7, 8)));

			// empty
			Assert.That(() => TuPack.DecodeFirst<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string, string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string, string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string, string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string, string, string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string, string, string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string, string, string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string, string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string, string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string, string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string, string, string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string, string, string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string, string, string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_TuplePack_Unpack_First_And_Last_With_Expected_Size()
		{
			var packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6);

			Assert.That(TuPack.DecodeFirst<int>(packed, expectedSize: 6), Is.EqualTo(1));
			Assert.That(() => TuPack.DecodeFirst<int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(TuPack.DecodeLast<int>(packed, expectedSize: 6), Is.EqualTo(6));
			Assert.That(() => TuPack.DecodeLast<int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());

			Assert.That(TuPack.DecodeFirst<int, int>(packed, expectedSize: 6), Is.EqualTo((1, 2)));
			Assert.That(() => TuPack.DecodeFirst<int, int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<int, int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(TuPack.DecodeLast<int, int>(packed, expectedSize: 6), Is.EqualTo((5, 6)));
			Assert.That(() => TuPack.DecodeLast<int, int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<int, int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());

			Assert.That(TuPack.DecodeFirst<int, int, int>(packed, expectedSize: 6), Is.EqualTo((1, 2, 3)));
			Assert.That(() => TuPack.DecodeFirst<int, int, int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<int, int, int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(TuPack.DecodeLast<int, int, int>(packed, expectedSize: 6), Is.EqualTo((4, 5, 6)));
			Assert.That(() => TuPack.DecodeLast<int, int, int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<int, int, int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());

			Assert.That(TuPack.DecodeFirst<int, int, int, int>(packed, expectedSize: 6), Is.EqualTo((1, 2, 3, 4)));
			Assert.That(() => TuPack.DecodeFirst<int, int, int, int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<int, int, int, int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(TuPack.DecodeLast<int, int, int, int>(packed, expectedSize: 6), Is.EqualTo((3, 4, 5, 6)));
			Assert.That(() => TuPack.DecodeLast<int, int, int, int>(packed, expectedSize: 5), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<int, int, int, int>(packed, expectedSize: 7), Throws.InstanceOf<InvalidOperationException>());

			int item1;
			Assert.That(TuPack.TryDecodeLast<int>(packed.Span, out item1), Is.True.WithOutput(item1).EqualTo(6));
			Assert.That(TuPack.TryDecodeLast<int>(packed.Span, expectedSize: 6, out item1), Is.True.WithOutput(item1).EqualTo(6));
			Assert.That(TuPack.TryDecodeLast<int>(packed, out item1), Is.True.WithOutput(item1).EqualTo(6));
			Assert.That(TuPack.TryDecodeLast<int>(packed, expectedSize: 6, out item1), Is.True.WithOutput(item1).EqualTo(6));
			Assert.That(TuPack.TryDecodeLast<int>(packed, expectedSize: 5, out item1), Is.False);
			Assert.That(TuPack.TryDecodeLast<int>(packed, expectedSize: 7, out item1), Is.False);

			Assert.That(TuPack.TryDecodeFirst<int>(packed.Span, out item1), Is.True.WithOutput(item1).EqualTo(1));
			Assert.That(TuPack.TryDecodeFirst<int>(packed.Span, expectedSize: 6, out item1), Is.True.WithOutput(item1).EqualTo(1));
			Assert.That(TuPack.TryDecodeFirst<int>(packed, expectedSize: 6, out item1), Is.True.WithOutput(item1).EqualTo(1));
			Assert.That(TuPack.TryDecodeFirst<int>(packed, expectedSize: 5, out item1), Is.False);
			Assert.That(TuPack.TryDecodeFirst<int>(packed, expectedSize: 7, out item1), Is.False);

			int item2;
			Assert.That(TuPack.TryDecodeLast<int, int>(packed.Span, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int>(packed.Span, expectedSize: 6, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int>(packed, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int>(packed, expectedSize: 6, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int>(packed, expectedSize: 5, out item1, out item2), Is.False);
			Assert.That(TuPack.TryDecodeLast<int, int>(packed, expectedSize: 7, out item1, out item2), Is.False);

			Assert.That(TuPack.TryDecodeFirst<int, int>(packed.Span, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((1, 2)));
			Assert.That(TuPack.TryDecodeFirst<int, int>(packed.Span, expectedSize: 6, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((1, 2)));
			Assert.That(TuPack.TryDecodeFirst<int, int>(packed, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((1, 2)));
			Assert.That(TuPack.TryDecodeFirst<int, int>(packed, expectedSize: 6, out item1, out item2), Is.True.WithOutput((item1, item2)).EqualTo((1, 2)));
			Assert.That(TuPack.TryDecodeFirst<int, int>(packed, expectedSize: 5, out item1, out item2), Is.False);
			Assert.That(TuPack.TryDecodeFirst<int, int>(packed, expectedSize: 7, out item1, out item2), Is.False);

			int item3;
			Assert.That(TuPack.TryDecodeLast<int, int, int>(packed.Span, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int>(packed.Span, expectedSize: 6, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int>(packed, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int>(packed, expectedSize: 6, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int>(packed, expectedSize: 5, out item1, out item2, out item3), Is.False);
			Assert.That(TuPack.TryDecodeLast<int, int, int>(packed, expectedSize: 7, out item1, out item2, out item3), Is.False);

			Assert.That(TuPack.TryDecodeFirst<int, int, int>(packed.Span, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((1, 2, 3)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int>(packed.Span, expectedSize: 6, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((1, 2, 3)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int>(packed, expectedSize: 6, out item1, out item2, out item3), Is.True.WithOutput((item1, item2, item3)).EqualTo((1, 2, 3)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int>(packed, expectedSize: 5, out item1, out item2, out item3), Is.False);
			Assert.That(TuPack.TryDecodeFirst<int, int, int>(packed, expectedSize: 7, out item1, out item2, out item3), Is.False);

			int item4;
			Assert.That(TuPack.TryDecodeLast<int, int, int, int>(packed.Span, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((3, 4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int, int>(packed.Span, expectedSize: 6, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((3, 4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int, int>(packed, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((3, 4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int, int>(packed, expectedSize: 6, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((3, 4, 5, 6)));
			Assert.That(TuPack.TryDecodeLast<int, int, int, int>(packed, expectedSize: 5, out item1, out item2, out item3, out item4), Is.False);
			Assert.That(TuPack.TryDecodeLast<int, int, int, int>(packed, expectedSize: 7, out item1, out item2, out item3, out item4), Is.False);

			Assert.That(TuPack.TryDecodeFirst<int, int, int, int>(packed.Span, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((1, 2, 3, 4)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int, int>(packed.Span, expectedSize: 6, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((1, 2, 3, 4)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int, int>(packed, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((1, 2, 3, 4)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int, int>(packed, expectedSize: 6, out item1, out item2, out item3, out item4), Is.True.WithOutput((item1, item2, item3, item4)).EqualTo((1, 2, 3, 4)));
			Assert.That(TuPack.TryDecodeFirst<int, int, int, int>(packed, expectedSize: 5, out item1, out item2, out item3, out item4), Is.False);
			Assert.That(TuPack.TryDecodeFirst<int, int, int, int>(packed, expectedSize: 7, out item1, out item2, out item3, out item4), Is.False);
		}

		[Test]
		public void Test_TuplePack_UnpackSingle()
		{
			// should only work with tuples having exactly one element

			Slice packed;

			packed = TuPack.EncodeKey(1);
			Assert.That(TuPack.DecodeKey<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeKey<int>(packed.Span), Is.EqualTo(1));
			Assert.That(TuPack.DecodeKey<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeKey<string>(packed.Span), Is.EqualTo("1"));

			packed = TuPack.EncodeKey("Hello\0World");
			Assert.That(TuPack.DecodeKey<string>(packed), Is.EqualTo("Hello\0World"));
			Assert.That(TuPack.DecodeKey<string>(packed.Span), Is.EqualTo("Hello\0World"));

			Assert.That(() => TuPack.DecodeKey<string>(Slice.Nil), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<string>(Slice.Empty), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<string>(default(ReadOnlySpan<byte>)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5, 6)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8)), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_TuPack_ValueTuple_Pack()
		{
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world")).ToString(),
				Is.EqualTo("<02>hello world<00>")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 })).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, false)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-&")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, false, "The End")).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-&<02>The End<00>")
			);

			{ // Embedded Tuples
				var packed = TuPack.Pack(("hello", (123, false), "world"));
				Assert.That(
					packed.ToString(),
					Is.EqualTo("<02>hello<00><05><15>{&<00><02>world<00>")
				);
				var t = TuPack.DecodeKey<string, (int, bool), string>(packed);
				Assert.That(t, Is.EqualTo(("hello", (123, false), "world")));
			}
		}

	}

}
