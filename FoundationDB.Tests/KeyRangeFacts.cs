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

// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable StringLiteralTypo
// ReSharper disable CanSimplifyStringEscapeSequence

namespace FoundationDB.Client.Tests
{

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class KeyRangeFacts : FdbSimpleTest
	{

		[Test]
		public void Test_KeyRange_Contains()
		{
			KeyRange range;

			// ["", "")
			range = KeyRange.Empty;
			Log(range);
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Literal("\x00")), Is.False);
			Assert.That(range.Contains(Literal("hello")), Is.False);
			Assert.That(range.Contains(Literal("\xFF")), Is.False);

			// ["", "\xFF" )
			range = KeyRange.Create(Slice.Empty, Literal("\xFF"));
			Log(range);
			Assert.That(range.Contains(Slice.Empty), Is.True);
			Assert.That(range.Contains(Literal("\x00")), Is.True);
			Assert.That(range.Contains(Literal("hello")), Is.True);
			Assert.That(range.Contains(Literal("\xFF")), Is.False);

			// ["\x00", "\xFF" )
			range = KeyRange.Create(Literal("\x00"), Literal("\xFF"));
			Log(range);
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Literal("\x00")), Is.True);
			Assert.That(range.Contains(Literal("hello")), Is.True);
			Assert.That(range.Contains(Literal("\xFF")), Is.False);

			// corner cases
			Assert.That(KeyRange.Create(Literal("A"), Literal("A")).Contains(Literal("A")), Is.False, "Equal bounds");
		}

		[Test]
		public void Test_KeyRange_Test()
		{
			const int BEFORE = -1, INSIDE = 0, AFTER = +1;

			KeyRange range;

			// range: [ "A", "Z" )
			range = KeyRange.Create(Literal("A"), Literal("Z"));
			Log(range);

			// Excluding the end: < "Z"
			Assert.That(range.Test(Literal("\x00"), endIncluded: false), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("@"), endIncluded: false), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("A"), endIncluded: false), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Literal("Z"), endIncluded: false), Is.EqualTo(AFTER));
			Assert.That(range.Test(Literal("Z\x00"), endIncluded: false), Is.EqualTo(AFTER));
			Assert.That(range.Test(Literal("\xFF"), endIncluded: false), Is.EqualTo(AFTER));

			// Including the end: <= "Z"
			Assert.That(range.Test(Literal("\x00"), endIncluded: true), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("@"), endIncluded: true), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("A"), endIncluded: true), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Literal("Z"), endIncluded: true), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Literal("Z\x00"), endIncluded: true), Is.EqualTo(AFTER));
			Assert.That(range.Test(Literal("\xFF"), endIncluded: true), Is.EqualTo(AFTER));

			range = KeyRange.Create(TuPack.EncodeKey("A"), TuPack.EncodeKey("Z"));
			Log(range);
			Assert.That(range.Test(TuPack.EncodeKey("@")), Is.EqualTo((BEFORE)));
			Assert.That(range.Test(TuPack.EncodeKey("A")), Is.EqualTo((INSIDE)));
			Assert.That(range.Test(TuPack.EncodeKey("Z")), Is.EqualTo((AFTER)));
			Assert.That(range.Test(TuPack.EncodeKey("Z"), endIncluded: true), Is.EqualTo(INSIDE));
		}

		[Test]
		public void Test_KeyRange_StartsWith()
		{
			KeyRange range;

			// "abc" => [ "abc", "abd" )
			range = KeyRange.StartsWith(Literal("abc"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("abc")));
			Assert.That(range.End, Is.EqualTo(Literal("abd")));

			// "" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Empty), Throws.InstanceOf<ArgumentException>());

			// "\xFF" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Literal("\xFF")), Throws.InstanceOf<ArgumentException>());

			// null => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_KeyRange_PrefixedBy()
		{
			KeyRange range;

			// "abc" => [ "abc\x00", "abd" )
			range = KeyRange.PrefixedBy(Literal("abc"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("abc\x00")));
			Assert.That(range.End, Is.EqualTo(Literal("abd")));

			// "" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Empty), Throws.InstanceOf<ArgumentException>());

			// "\xFF" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Literal("\xFF")), Throws.InstanceOf<ArgumentException>());

			// null => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_KeyRange_FromKey()
		{
			KeyRange range;

			// "" => [ "", "\x00" )
			range = KeyRange.FromKey(Slice.Empty);
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Slice.Empty));
			Assert.That(range.End, Is.EqualTo(Literal("\x00")));

			// "abc" => [ "abc", "abc\x00" )
			range = KeyRange.FromKey(Literal("abc"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("abc")));
			Assert.That(range.End, Is.EqualTo(Literal("abc\x00")));

			// "\xFF" => [ "\xFF", "\xFF\x00" )
			range = KeyRange.FromKey(Literal("\xFF"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("\xFF")));
			Assert.That(range.End, Is.EqualTo(Literal("\xFF\x00")));

			Assert.That(() => KeyRange.FromKey(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKey_PrettyPrint()
		{
			// verify that the pretty printing of keys produce a user-friendly output
			Assert.Multiple(() =>
			{
				Assert.That(FdbKey.Dump(Slice.Nil), Is.EqualTo("<null>"));
				Assert.That(FdbKey.Dump(Slice.Empty), Is.EqualTo("<empty>"));

				Assert.That(FdbKey.Dump(Slice.FromByte(0)), Is.EqualTo("<00>"));
				Assert.That(FdbKey.Dump(Slice.FromByte(255)), Is.EqualTo("<FF>"));

				Assert.That(FdbKey.Dump([ 0, 1, 2, 3, 4, 5, 6, 7 ]), Is.EqualTo("<00><01><02><03><04><05><06><07>"));
				Assert.That(FdbKey.Dump([ 255, 254, 253, 252, 251, 250, 249, 248 ]), Is.EqualTo("(|System:<FE><FD><FC><FB><FA><F9><F8>|,)"));

				Assert.That(FdbKey.Dump(Text("hello")), Is.EqualTo("hello"));
				Assert.That(FdbKey.Dump(Text("héllø")), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

				Assert.That(FdbKey.Dump(Text("hello"u8)), Is.EqualTo("hello"));
				Assert.That(FdbKey.Dump(Text("héllø"u8)), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

				Assert.That(FdbKey.Dump("hello"u8), Is.EqualTo("hello"));
				Assert.That(FdbKey.Dump("héllø"u8), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

				// tuples should be decoded properly

				Assert.That(FdbKey.Dump(TuPack.EncodeKey(123)), Is.EqualTo("(123,)"), "Singleton tuples should end with a ','");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(Literal("hello"))), Is.EqualTo("(`hello`,)"), "ASCII strings should use single back quotes");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey("héllø")), Is.EqualTo("(\"héllø\",)"), "Unicode strings should use double quotes");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(Text("héllø"u8))), Is.EqualTo("(`h<C3><A9>ll<C3><B8>`,)"), "Unicode strings should use double quotes");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(new byte[] { 1, 2, 3 }.AsSlice())), Is.EqualTo("(`<01><02><03>`,)"));
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(123, 456)), Is.EqualTo("(123, 456)"), "Elements should be separated with a space, and not end up with ','");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(default(object), true, false)), Is.EqualTo("(null, true, false)"), "Booleans should be displayed as numbers, and null should be in lowercase"); //note: even though it's tempting to using Python's "Nil", it's not very ".NETty"

				//note: the string representation of double is not identical between NetFx and .NET Core! So we cannot use a constant literal here
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(1.0d, Math.PI, Math.E)), Is.EqualTo("(1, " + Math.PI.ToString("R", CultureInfo.InvariantCulture) + ", " + Math.E.ToString("R", CultureInfo.InvariantCulture) + ")"), "Doubles should used dot and have full precision (17 digits)");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(1.0f, (float)Math.PI, (float)Math.E)), Is.EqualTo("(1, " + ((float) Math.PI).ToString("R", CultureInfo.InvariantCulture)+ ", " + ((float) Math.E).ToString("R", CultureInfo.InvariantCulture) + ")"), "Singles should used dot and have full precision (10 digits)");

				var guid = Guid.NewGuid();
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(guid)), Is.EqualTo($"({guid:B},)"), "GUIDs should be displayed as a string literal, surrounded by {{...}}, and without quotes");
				var uuid128 = Uuid128.NewUuid();
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(uuid128)), Is.EqualTo($"({uuid128:B},)"), "Uuid128s should be displayed as a string literal, surrounded by {{...}}, and without quotes");
				var uuid64 = Uuid64.NewUuid();
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(uuid64)), Is.EqualTo($"({uuid64:B},)"), "Uuid64s should be displayed as a string literal, surrounded by {{...}}, and without quotes");

				// ranges should be decoded when possible
				var key = KeyRange.StartsWith(TuPack.EncodeKey("hello"));
				// "<02>hello<00>" .. "<02>hello<01>"
				Assert.That(FdbKey.PrettyPrint(key.Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(\"hello\",)"));
				Assert.That(FdbKey.PrettyPrint(key.End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(\"hello\",) + 1"));

				var t = TuPack.EncodeKey(123);
				Assert.That(FdbKey.PrettyPrint(t, FdbKey.PrettyPrintMode.Single), Is.EqualTo("(123,)"));
				Assert.That(FdbKey.PrettyPrint(t + 0x00, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(123,).<00>"));
				Assert.That(FdbKey.PrettyPrint(t + 0xFF, FdbKey.PrettyPrintMode.End), Is.EqualTo("(123,).<FF>"));

				// Directory Layer Keys
				Assert.That(FdbKey.Dump(Slice.FromBytes([ 0xFE, 0x01, (byte) 'v', (byte) 'e', (byte) 'r', (byte) 's', (byte) 'i', (byte) 'o', (byte) 'n', 0x00 ])), Is.EqualTo("(|Directory|, `version`)"));
				Assert.That(FdbKey.Dump(Slice.FromBytes([ 0xFE, 0x01, 0xFE, 0x00, 0x01, (byte) 's', (byte) 't', (byte) 'a', (byte) 'm', (byte) 'p', 0x00 ])), Is.EqualTo("(|Directory|, `<FE>`, `stamp`)"));
				Assert.That(FdbKey.Dump(Slice.FromBytes([ 0xFE, 0x01, 0x15, 0x2A, 0x00, 0x01, (byte) 'l', (byte) 'a', (byte) 'y', (byte) 'e', (byte) 'r', 0x00 ])), Is.EqualTo("(|Directory|, `<15>*`, `layer`)"));
			});
		}

		[Test]
		public void Test_KeyRange_Intersects()
		{
			static KeyRange MakeRange(byte x, byte y) => KeyRange.Create(Slice.FromByte(x), Slice.FromByte(y));

			Assert.Multiple(() =>
			{
				#region Not Intersecting...

				// [0, 1) [2, 3)
				// #X
				//   #X
				Assert.That(MakeRange(0, 1).Intersects(MakeRange(2, 3)), Is.False);
				// [2, 3) [0, 1)
				//   #X
				// #X
				Assert.That(MakeRange(2, 3).Intersects(MakeRange(0, 1)), Is.False);

				// [0, 1) [1, 2)
				// #X
				//  #X
				Assert.That(MakeRange(0, 1).Intersects(MakeRange(1, 2)), Is.False);
				// [1, 2) [0, 1)
				//  #X
				// #X
				Assert.That(MakeRange(1, 2).Intersects(MakeRange(0, 1)), Is.False);

				#endregion

				#region Intersecting...

				// [0, 2) [1, 3)
				// ##X
				//  ##X
				Assert.That(MakeRange(0, 2).Intersects(MakeRange(1, 3)), Is.True);
				// [1, 3) [0, 2)
				//  ##X
				// ##X
				Assert.That(MakeRange(1, 3).Intersects(MakeRange(0, 2)), Is.True);

				// [0, 1) [0, 2)
				// #X
				// ##X
				Assert.That(MakeRange(0, 1).Intersects(MakeRange(0, 2)), Is.True);
				// [0, 2) [0, 1)
				// ##X
				// #X
				Assert.That(MakeRange(0, 2).Intersects(MakeRange(0, 1)), Is.True);

				// [0, 2) [1, 2)
				// ##X
				//  #X
				Assert.That(MakeRange(0, 2).Intersects(MakeRange(1, 2)), Is.True);
				// [1, 2) [0, 2)
				//  #X
				// ##X
				Assert.That(MakeRange(1, 2).Intersects(MakeRange(0, 2)), Is.True);

				#endregion
			});

		}

		[Test]
		public void Test_KeyRange_Disjoint()
		{
			static KeyRange MakeRange(byte x, byte y) => KeyRange.Create(Slice.FromByte(x), Slice.FromByte(y));

			Assert.Multiple(() =>
			{
				#region Disjoint...

				// [0, 1) [2, 3)
				// #X
				//   #X
				Assert.That(MakeRange(0, 1).Disjoint(MakeRange(2, 3)), Is.True);
				// [2, 3) [0, 1)
				//   #X
				// #X
				Assert.That(MakeRange(2, 3).Disjoint(MakeRange(0, 1)), Is.True);

				#endregion

				#region Not Disjoint...

				// [0, 1) [1, 2)
				// #X
				//  #X
				Assert.That(MakeRange(0, 1).Disjoint(MakeRange(1, 2)), Is.False);
				// [1, 2) [0, 1)
				//  #X
				// #X
				Assert.That(MakeRange(1, 2).Disjoint(MakeRange(0, 1)), Is.False);

				// [0, 2) [1, 3)
				// ##X
				//  ##X
				Assert.That(MakeRange(0, 2).Disjoint(MakeRange(1, 3)), Is.False);
				// [1, 3) [0, 2)
				//  ##X
				// ##X
				Assert.That(MakeRange(1, 3).Disjoint(MakeRange(0, 2)), Is.False);

				// [0, 1) [0, 2)
				// #X
				// ##X
				Assert.That(MakeRange(0, 1).Disjoint(MakeRange(0, 2)), Is.False);
				// [0, 2) [0, 1)
				// ##X
				// #X
				Assert.That(MakeRange(0, 2).Disjoint(MakeRange(0, 1)), Is.False);

				// [0, 2) [1, 2)
				// ##X
				//  #X
				Assert.That(MakeRange(0, 2).Disjoint(MakeRange(1, 2)), Is.False);
				// [1, 2) [0, 2)
				//  #X
				// ##X
				Assert.That(MakeRange(1, 2).Disjoint(MakeRange(0, 2)), Is.False);

				#endregion
			});
		}

	}

}
